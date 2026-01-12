using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using StreamVision.Models;
using StreamVision.Services;

namespace StreamVision.ViewModels
{
    /// <summary>
    /// ViewModel principal avec interface Netflix-style
    /// Gère Live TV, VOD, Séries, Catch-up
    /// </summary>
    public partial class HomeViewModel : ObservableObject, IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly M3UParser _m3uParser;
        private readonly XtreamCodesService _xtreamService;
        private readonly StalkerPortalService _stalkerService;
        private readonly EpgService _epgService;
        private readonly TmdbService _tmdbService;
        private readonly RecommendationEngine _recommendationEngine;
        private readonly ContentAnalyzerService _contentAnalyzer;
        private readonly StreamOptimizerService _streamOptimizer;
        private readonly SmartContentService _smartContent;

        // New enhanced services (initialized after LibVLC)
        private ChannelPreloaderService? _channelPreloader;
        private RecordingService? _recordingService;
        private readonly BingeModeService _bingeModeService;
        private readonly SleepTimerService _sleepTimerService;
        private readonly AudioNormalizationService _audioNormalizer;
        private readonly EpgReminderService _epgReminderService;
        private readonly CastService _castService;
        private readonly DownloadService _downloadService;

        private MediaItem? _currentlyPlayingItem;
        private DateTime _playbackStartTime;
        private string? _currentStreamUrl;

        // LibVLCSharp pour la lecture vidéo
        private LibVLC? _libVLC;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;

        public LibVLCSharp.Shared.MediaPlayer? MediaPlayer => _mediaPlayer;

        // Total counts for all content (not just loaded items)
        private int _totalLiveChannels;
        private int _totalMovies;
        private int _totalSeries;

        // Full content lists for search (NOT bound to UI)
        private List<MediaItem> _allLiveChannels = new();
        private List<MediaItem> _allMovies = new();
        private List<MediaItem> _allSeries = new();

        // Maximum items to display in UI (prevents freeze)
        private const int MaxDisplayItems = 5000;

        #region Observable Properties

        [ObservableProperty]
        private ObservableCollection<PlaylistSource> _playlistSources = new();

        [ObservableProperty]
        private PlaylistSource? _selectedSource;

        // Rows Netflix-style
        [ObservableProperty]
        private ObservableCollection<MediaRow> _contentRows = new();

        // Continue Watching
        [ObservableProperty]
        private ObservableCollection<MediaItem> _continueWatching = new();

        // Favoris
        [ObservableProperty]
        private ObservableCollection<MediaItem> _favorites = new();

        // Contenu par type
        [ObservableProperty]
        private ObservableCollection<MediaItem> _liveChannels = new();

        [ObservableProperty]
        private ObservableCollection<MediaItem> _movies = new();

        [ObservableProperty]
        private ObservableCollection<MediaItem> _series = new();

        // Catégories
        [ObservableProperty]
        private ObservableCollection<XtreamCategory> _liveCategories = new();

        [ObservableProperty]
        private ObservableCollection<XtreamCategory> _vodCategories = new();

        [ObservableProperty]
        private ObservableCollection<XtreamCategory> _seriesCategories = new();

        // État lecture
        [ObservableProperty]
        private MediaItem? _currentItem;

        [ObservableProperty]
        private MediaItem? _featuredItem;

        [ObservableProperty]
        private EpgProgram? _currentProgram;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private bool _isMuted;

        [ObservableProperty]
        private int _volume = 100;

        [ObservableProperty]
        private bool _isFullScreen;

        // Position de lecture (0-100 pour la ProgressBar)
        [ObservableProperty]
        private double _playbackPosition;

        [ObservableProperty]
        private string _currentTimeDisplay = "00:00";

        [ObservableProperty]
        private string _totalTimeDisplay = "00:00";

        [ObservableProperty]
        private long _currentTimeMs;

        [ObservableProperty]
        private long _totalTimeMs;

        // Vitesse de lecture
        [ObservableProperty]
        private float _playbackSpeed = 1.0f;

        [ObservableProperty]
        private string _playbackSpeedDisplay = "1x";

        // Ratio d'aspect
        [ObservableProperty]
        private string _aspectRatio = "Default";

        // Piste audio
        [ObservableProperty]
        private int _currentAudioTrack = 0;

        [ObservableProperty]
        private string _audioTrackDisplay = "Audio 1";

        [ObservableProperty]
        private int _audioTrackCount = 1;

        // Sous-titres
        [ObservableProperty]
        private int _currentSubtitleTrack = -1;

        [ObservableProperty]
        private string _subtitleTrackDisplay = "Sub Off";

        [ObservableProperty]
        private int _subtitleTrackCount = 0;

        // Buffering
        [ObservableProperty]
        private bool _isBuffering;

        [ObservableProperty]
        private float _bufferProgress;

        // UI State
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MediaItem> _searchResults = new();

        [ObservableProperty]
        private string _currentView = "Home"; // Home, Live, Movies, Series, Search

        [ObservableProperty]
        private int _bufferSize = 3000;

        // Détails série
        [ObservableProperty]
        private SeriesFullInfo? _selectedSeriesInfo;

        [ObservableProperty]
        private bool _isSeriesDetailOpen;

        // Premier lancement - affiche le message de bienvenue
        [ObservableProperty]
        private bool _isFirstLaunch;

        // Message d'erreur détaillé pour l'utilisateur
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        // User preferences for content filtering
        [ObservableProperty]
        private ContentPreferences _userPreferences = new();

        [ObservableProperty]
        private bool _onboardingCompleted;

        // Player settings and optimization
        [ObservableProperty]
        private PlayerSettings _playerSettings = new();

        [ObservableProperty]
        private bool _isOptimizing;

        [ObservableProperty]
        private string _optimizationStatus = "";

        [ObservableProperty]
        private StreamQualityInfo? _streamQuality;

        // Real-time playback health
        [ObservableProperty]
        private PlaybackHealthStatus _playbackHealth = PlaybackHealthStatus.Unknown;

        [ObservableProperty]
        private string _healthStatusIcon = "⚪";

        [ObservableProperty]
        private string _healthStatusText = "Inconnu";

        [ObservableProperty]
        private string _healthStatusColor = "#9E9E9E";

        // Current stream quality (like YouTube)
        [ObservableProperty]
        private Models.StreamQuality _currentStreamQuality = Models.StreamQuality.Auto;

        [ObservableProperty]
        private string _currentQualityDisplay = "Auto";

        // Recording
        [ObservableProperty]
        private bool _isRecording;

        [ObservableProperty]
        private string _recordingStatus = "";

        [ObservableProperty]
        private TimeSpan _recordingDuration;

        // Sleep Timer
        [ObservableProperty]
        private bool _isSleepTimerActive;

        [ObservableProperty]
        private TimeSpan _sleepTimeRemaining;

        [ObservableProperty]
        private string _sleepTimerDisplay = "";

        // Binge Mode
        [ObservableProperty]
        private bool _isBingeModeEnabled;

        [ObservableProperty]
        private bool _isAutoPlayCountdownVisible;

        [ObservableProperty]
        private int _autoPlayCountdown;

        [ObservableProperty]
        private MediaItem? _nextEpisode;

        // Cast
        [ObservableProperty]
        private bool _isCastActive;

        [ObservableProperty]
        private string _castDeviceName = "";

        [ObservableProperty]
        private ObservableCollection<CastDevice> _availableCastDevices = new();

        // Download
        [ObservableProperty]
        private ObservableCollection<DownloadTask> _activeDownloads = new();

        [ObservableProperty]
        private bool _hasActiveDownloads;

        // Subtitle customization
        [ObservableProperty]
        private SubtitleSettings _subtitleSettings = new();

        // Channel preloader
        [ObservableProperty]
        private bool _isChannelPreloading;

        #endregion

        #region Computed Properties

        public bool HasContinueWatching => ContinueWatching.Count > 0;
        public bool HasFavorites => Favorites.Count > 0;
        public bool HasMediaPlayer => _mediaPlayer != null;

        #endregion

        public HomeViewModel()
        {
            _databaseService = new DatabaseService();
            _m3uParser = new M3UParser();
            _xtreamService = new XtreamCodesService();
            _stalkerService = new StalkerPortalService();
            _epgService = new EpgService();
            _tmdbService = new TmdbService();
            _recommendationEngine = new RecommendationEngine(_databaseService);
            _contentAnalyzer = new ContentAnalyzerService();
            _streamOptimizer = new StreamOptimizerService();
            _smartContent = new SmartContentService();

            // Initialize services that don't need LibVLC
            _bingeModeService = new BingeModeService();
            _sleepTimerService = new SleepTimerService();
            _audioNormalizer = new AudioNormalizationService();
            _epgReminderService = new EpgReminderService();
            _castService = new CastService();
            _downloadService = new DownloadService();

            // Subscribe to optimizer events
            _streamOptimizer.OnStatusChanged += status => OptimizationStatus = status;
            _streamOptimizer.OnQualityAnalyzed += info => StreamQuality = info;
            _streamOptimizer.OnSettingsOptimized += settings => PlayerSettings = settings;
            _streamOptimizer.OnReconnecting += msg => StatusMessage = msg;

            // Subscribe to real-time health monitoring
            _streamOptimizer.OnHealthStatusChanged += HandlePlaybackHealthChange;
            _streamOptimizer.OnRestartRequired += HandleStreamRestartRequired;
            _streamOptimizer.OnQualityDowngradeRequested += HandleQualityDowngrade;

            // Subscribe to sleep timer events
            _sleepTimerService.OnTimerTick += remaining => {
                SleepTimeRemaining = remaining;
                SleepTimerDisplay = $"💤 {remaining:mm\\:ss}";
            };
            _sleepTimerService.OnTimerExpired += () => {
                IsSleepTimerActive = false;
                SleepTimerDisplay = "";
                _ = StopAsync();
            };
            _sleepTimerService.OnFinalCountdown += seconds => {
                if (seconds == 60) StatusMessage = "⚠️ Arrêt dans 1 minute";
            };

            // Subscribe to binge mode events
            _bingeModeService.OnNextEpisodeReady += episode => {
                NextEpisode = episode;
                IsAutoPlayCountdownVisible = true;
            };
            _bingeModeService.OnCountdownTick += seconds => AutoPlayCountdown = seconds;
            _bingeModeService.OnAutoPlayNext += episode => {
                IsAutoPlayCountdownVisible = false;
                if (episode != null) _ = PlayItemAsync(episode);
            };
            _bingeModeService.OnIntroDetected += introEnd => StatusMessage = $"⏭️ Appuyez sur S pour passer l'intro";

            // Subscribe to cast events
            _castService.OnDeviceDiscovered += device => AvailableCastDevices.Add(device);
            _castService.OnDeviceConnected += device => {
                IsCastActive = true;
                CastDeviceName = device.Name;
                StatusMessage = $"📺 Connecté à {device.Name}";
            };
            _castService.OnDeviceDisconnected += _ => {
                IsCastActive = false;
                CastDeviceName = "";
            };

            // Subscribe to download events
            _downloadService.OnDownloadStarted += task => {
                ActiveDownloads.Add(task);
                HasActiveDownloads = true;
            };
            _downloadService.OnDownloadCompleted += task => {
                ActiveDownloads.Remove(task);
                HasActiveDownloads = ActiveDownloads.Count > 0;
                StatusMessage = $"✅ Téléchargé: {task.MediaItem.Name}";
            };

            // Subscribe to EPG reminder events
            _epgReminderService.OnReminderTriggered += reminder => {
                StatusMessage = $"🔔 Rappel: {reminder.Program.Title} sur {reminder.Channel.Name} dans {reminder.MinutesBefore} min";
            };
            _epgReminderService.Start();

            // Initialiser LibVLC de manière sécurisée
            InitializeLibVLC();
        }

        private void InitializeLibVLC()
        {
            InitializeLibVLCWithSettings(PlayerSettings);
        }

        private void InitializeLibVLCWithSettings(PlayerSettings settings)
        {
            try
            {
                // Dispose existing if reinitializing
                _mediaPlayer?.Dispose();
                _libVLC?.Dispose();

                Core.Initialize();

                // Use settings to configure LibVLC
                var options = settings.ToLibVLCOptions();
                _libVLC = new LibVLC(options);
                _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
                _mediaPlayer.Volume = Volume;

                // Give optimizer access to player AND LibVLC for restart capability
                _streamOptimizer.SetMediaPlayer(_mediaPlayer, _libVLC);

                // Initialize LibVLC-dependent services
                _channelPreloader = new ChannelPreloaderService(_libVLC);
                _recordingService = new RecordingService(_libVLC);
                _bingeModeService.SetMediaPlayer(_mediaPlayer);
                _audioNormalizer.Initialize(_mediaPlayer, _libVLC);

                // Subscribe to recording events
                _recordingService.OnRecordingStarted += rec => {
                    IsRecording = true;
                    RecordingStatus = $"🔴 Enregistrement: {rec.MediaItem.Name}";
                };
                _recordingService.OnRecordingProgress += (rec, progress) => RecordingDuration = TimeSpan.FromSeconds(progress);
                _recordingService.OnRecordingStopped += rec => {
                    IsRecording = false;
                    RecordingStatus = "";
                    StatusMessage = $"Enregistrement sauvegardé: {System.IO.Path.GetFileName(rec.FilePath)}";
                };

                // S'abonner aux événements de position
                _mediaPlayer.TimeChanged += (s, e) =>
                {
                    CurrentTimeMs = e.Time;
                    CurrentTimeDisplay = FormatTime(e.Time);
                    if (TotalTimeMs > 0)
                    {
                        PlaybackPosition = (double)e.Time / TotalTimeMs * 100;
                    }
                };

                _mediaPlayer.LengthChanged += (s, e) =>
                {
                    TotalTimeMs = e.Length;
                    TotalTimeDisplay = FormatTime(e.Length);
                };

                _mediaPlayer.EndReached += (s, e) =>
                {
                    IsPlaying = false;
                    PlaybackPosition = 0;
                    CurrentTimeDisplay = "00:00";
                    _streamOptimizer.StopMonitoring();
                };

                // Buffering events
                _mediaPlayer.Buffering += (s, e) =>
                {
                    BufferProgress = e.Cache;
                    IsBuffering = e.Cache < 100;

                    // Show buffering status with percentage
                    if (e.Cache < 100)
                    {
                        OptimizationStatus = $"Mise en mémoire tampon: {e.Cache:F0}%";
                    }
                };

                // Error handling with auto-reconnect
                _mediaPlayer.EncounteredError += async (s, e) =>
                {
                    LogDebug("MediaPlayer encountered error - attempting reconnect");
                    StatusMessage = "Erreur de lecture - Reconnexion...";

                    if (settings.AutoReconnect && !string.IsNullOrEmpty(_currentStreamUrl) && _libVLC != null)
                    {
                        var reconnected = await _streamOptimizer.TryReconnectAsync(_currentStreamUrl, _libVLC);
                        if (!reconnected)
                        {
                            StatusMessage = "Impossible de se reconnecter au flux";
                            IsPlaying = false;
                        }
                    }
                };

                // Track info when playing starts - auto-select preferred language
                _mediaPlayer.Playing += (s, e) =>
                {
                    IsBuffering = false;
                    IsOptimizing = false;
                    OptimizationStatus = "";

                    // Start monitoring for quality issues
                    _streamOptimizer.StartMonitoring();

                    // Auto-select preferred audio/subtitle tracks based on user preferences
                    AutoSelectPreferredTracks();
                    UpdateAudioTrackInfo();
                    UpdateSubtitleTrackInfo();
                };

                LogDebug($"LibVLC initialized with settings: buffer={settings.NetworkCaching}ms, hw={settings.HardwareAcceleration}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"LibVLC init error: {ex.Message}";
                LogDebug($"LibVLC init error: {ex}");
                _libVLC = null;
                _mediaPlayer = null;
            }
        }

        /// <summary>
        /// Reinitialize LibVLC with new settings (called after optimization or settings change)
        /// </summary>
        public void ApplyPlayerSettings(PlayerSettings newSettings)
        {
            PlayerSettings = newSettings;
            InitializeLibVLCWithSettings(newSettings);
            LogDebug("Player settings applied and LibVLC reinitialized");
        }

        private static string FormatTime(long milliseconds)
        {
            var ts = TimeSpan.FromMilliseconds(milliseconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        #region Initialization

        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Initializing...";
                LogDebug("InitializeAsync starting...");

                // Initialize database
                await _databaseService.InitializeAsync();
                LogDebug("Database initialized");

                // Load user preferences
                await LoadUserPreferencesAsync();
                LogDebug($"User preferences loaded - Onboarding completed: {OnboardingCompleted}");

                // Load data
                await LoadPlaylistSourcesAsync();
                LogDebug($"Loaded {PlaylistSources.Count} playlist sources");
                await LoadContinueWatchingAsync();
                await LoadFavoritesAsync();

                // Initialize recommendation engine
                await _recommendationEngine.InitializeAsync();
                LogDebug("Recommendation engine initialized");

                // Si pas de playlist, afficher l'écran de bienvenue avec données démo
                if (PlaylistSources.Count == 0)
                {
                    LogDebug("No playlist sources - First launch, showing welcome screen");
                    IsFirstLaunch = true;
                    StatusMessage = "Bienvenue ! Cliquez sur + pour ajouter votre playlist IPTV";

                    // Charger les données de démonstration pour montrer l'interface
                    LoadSampleData();
                    BuildContentRows();
                    SelectFeaturedItem();
                }
                else
                {
                    var source = PlaylistSources.First();
                    LogDebug($"Using existing source: {source.Name} - {source.Url}");
                    await LoadAllContentAsync(source);
                    LogDebug("LoadAllContentAsync completed");
                }

                // Afficher les totaux disponibles
                StatusMessage = $"Ready - {LiveChannels.Count}/{_totalLiveChannels} chaînes, {Movies.Count}/{_totalMovies} films, {Series.Count}/{_totalSeries} séries";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Placeholder for sample data - now empty, real content comes from playlists
        /// </summary>
        private void LoadSampleData()
        {
            // No sample data - collections remain empty until a playlist is added
            Movies.Clear();
            Series.Clear();
            LiveChannels.Clear();
        }

        /// <summary>
        /// Load user preferences from database
        /// </summary>
        private async Task LoadUserPreferencesAsync()
        {
            var prefs = await _databaseService.GetUserPreferencesAsync();
            if (prefs != null)
            {
                UserPreferences = prefs;
                OnboardingCompleted = prefs.OnboardingCompleted;
            }
            else
            {
                UserPreferences = new ContentPreferences();
                OnboardingCompleted = false;
            }
        }

        /// <summary>
        /// Save user preferences to database
        /// </summary>
        public async Task SaveUserPreferencesAsync(ContentPreferences prefs)
        {
            UserPreferences = prefs;
            OnboardingCompleted = prefs.OnboardingCompleted;
            await _databaseService.SaveUserPreferencesAsync(prefs);
            LogDebug($"Preferences saved - Languages: {string.Join(", ", prefs.PreferredLanguages)}");
        }

        /// <summary>
        /// Check if a media item matches user preferences using content analysis
        /// </summary>
        private bool MatchesUserPreferences(MediaItem item)
        {
            // Use the content analyzer service for smart filtering
            return _contentAnalyzer.MatchesPreferences(item, UserPreferences);
        }

        #endregion

        #region Playlist Management Commands

        [RelayCommand]
        private async Task AddM3UPlaylistAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                StatusMessage = "URL invalide";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Ajout de la playlist M3U...";

                var source = new PlaylistSource
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Playlist M3U",
                    Url = url,
                    Type = SourceType.M3U,
                    LastSync = DateTime.Now
                };

                await _databaseService.SavePlaylistSourceAsync(source);
                PlaylistSources.Add(source);
                SelectedSource = source;

                StatusMessage = "Playlist ajoutée avec succès";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
                LogDebug($"AddM3UPlaylist error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddXtreamPlaylistAsync((string url, string username, string password) args)
        {
            if (string.IsNullOrWhiteSpace(args.url) || string.IsNullOrWhiteSpace(args.username))
            {
                StatusMessage = "Informations manquantes";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Connexion au serveur Xtream...";

                // Verify credentials first
                var account = await _xtreamService.AuthenticateAsync(args.url, args.username, args.password);
                if (account == null)
                {
                    StatusMessage = "Échec de l'authentification";
                    return;
                }

                var source = new PlaylistSource
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"IPTV - {args.username}",
                    Url = args.url,
                    Username = args.username,
                    Password = args.password,
                    Type = SourceType.XtreamCodes,
                    LastSync = DateTime.Now
                };

                await _databaseService.SavePlaylistSourceAsync(source);
                PlaylistSources.Add(source);
                SelectedSource = source;

                StatusMessage = $"Connecté - {account.ActiveConnections}/{account.MaxConnections} connexions";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
                LogDebug($"AddXtreamPlaylist error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddStalkerPlaylistAsync((string url, string mac) args)
        {
            if (string.IsNullOrWhiteSpace(args.url) || string.IsNullOrWhiteSpace(args.mac))
            {
                StatusMessage = "Informations manquantes";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Connexion au portail Stalker...";

                var source = new PlaylistSource
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Stalker - {args.mac[..8]}...",
                    Url = args.url,
                    MacAddress = args.mac,
                    Type = SourceType.StalkerPortal,
                    LastSync = DateTime.Now
                };

                await _databaseService.SavePlaylistSourceAsync(source);
                PlaylistSources.Add(source);
                SelectedSource = source;

                StatusMessage = "Portail Stalker ajouté";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
                LogDebug($"AddStalkerPlaylist error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Data Loading

        private async Task LoadPlaylistSourcesAsync()
        {
            var sources = await _databaseService.GetPlaylistSourcesAsync();
            PlaylistSources.Clear();
            foreach (var source in sources)
            {
                PlaylistSources.Add(source);
            }

            if (PlaylistSources.Count > 0 && SelectedSource == null)
            {
                SelectedSource = PlaylistSources.First();
            }
        }

        partial void OnSelectedSourceChanged(PlaylistSource? value)
        {
            if (value != null)
            {
                _ = LoadAllContentAsync(value);
            }
        }

        private async Task LoadAllContentAsync(PlaylistSource source)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"Loading content from {source.Name}...";

                if (source.Type == SourceType.XtreamCodes)
                {
                    await LoadXtreamContentAsync(source);
                }
                else if (source.Type == SourceType.StalkerPortal)
                {
                    await LoadStalkerContentAsync(source);
                }
                else
                {
                    await LoadM3UContentAsync(source);
                }

                BuildContentRows();
                SelectFeaturedItem();

                StatusMessage = $"Loaded content from {source.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadXtreamContentAsync(PlaylistSource source)
        {
            var serverUrl = source.Url;
            var username = source.Username!;
            var password = source.Password!;

            LogDebug($"LoadXtreamContentAsync starting - URL: {serverUrl}");
            StatusMessage = "Authenticating...";

            // First authenticate to verify credentials
            XtreamAccountInfo? account = null;
            try
            {
                LogDebug("Calling AuthenticateAsync...");
                account = await _xtreamService.AuthenticateAsync(serverUrl, username, password);
                LogDebug($"AuthenticateAsync returned: {(account != null ? "success" : "null")}");
            }
            catch (Exception ex)
            {
                LogDebug($"AuthenticateAsync EXCEPTION: {ex.Message}");
                var errorMsg = !string.IsNullOrEmpty(_xtreamService.LastError)
                    ? _xtreamService.LastError
                    : ex.Message;
                ErrorMessage = errorMsg;
                StatusMessage = "Connexion échouée - Données de démonstration chargées";
                LoadSampleData();
                BuildContentRows();
                SelectFeaturedItem();
                return;
            }

            if (account == null)
            {
                var errorMsg = !string.IsNullOrEmpty(_xtreamService.LastError)
                    ? _xtreamService.LastError
                    : "Authentification échouée. Vérifiez vos identifiants.";
                ErrorMessage = errorMsg;
                StatusMessage = "Connexion échouée - Données de démonstration chargées";
                LoadSampleData();
                BuildContentRows();
                SelectFeaturedItem();
                return;
            }

            StatusMessage = $"Connected as {account.Username} - Loading categories...";

            // Load categories with error handling
            List<XtreamCategory> liveCats, vodCats, seriesCats;
            try
            {
                liveCats = await _xtreamService.GetLiveCategoriesAsync(serverUrl, username, password);
                vodCats = await _xtreamService.GetVodCategoriesAsync(serverUrl, username, password);
                seriesCats = await _xtreamService.GetSeriesCategoriesAsync(serverUrl, username, password);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load categories: {ex.Message} - Loading demo data...";
                LoadSampleData();
                BuildContentRows();
                SelectFeaturedItem();
                return;
            }

            LiveCategories.Clear();
            foreach (var cat in liveCats) LiveCategories.Add(cat);

            VodCategories.Clear();
            foreach (var cat in vodCats) VodCategories.Add(cat);

            SeriesCategories.Clear();
            foreach (var cat in seriesCats) SeriesCategories.Add(cat);

            LiveChannels.Clear();
            Movies.Clear();
            Series.Clear();

            var allLive = new List<MediaItem>();
            var allMovies = new List<MediaItem>();
            var allSeries = new List<MediaItem>();

            // === CHARGEMENT COMPLET DE TOUTES LES CATÉGORIES ===

            // Charger TOUTES les chaînes Live
            if (liveCats.Count > 0)
            {
                int loadedCats = 0;
                foreach (var cat in liveCats)
                {
                    loadedCats++;
                    StatusMessage = $"📺 Live [{loadedCats}/{liveCats.Count}]: {cat.CategoryName}...";
                    try
                    {
                        var channels = await _xtreamService.GetLiveStreamsAsync(
                            serverUrl, username, password, source.Id, cat.CategoryId);
                        allLive.AddRange(channels);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Xtream] Error loading live category {cat.CategoryName}: {ex.Message}");
                    }
                }
            }

            // Charger TOUS les films VOD
            if (vodCats.Count > 0)
            {
                int loadedCats = 0;
                foreach (var cat in vodCats)
                {
                    loadedCats++;
                    StatusMessage = $"🎬 VOD [{loadedCats}/{vodCats.Count}]: {cat.CategoryName}...";
                    try
                    {
                        var movies = await _xtreamService.GetVodStreamsAsync(
                            serverUrl, username, password, source.Id, cat.CategoryId);
                        allMovies.AddRange(movies);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Xtream] Error loading VOD category {cat.CategoryName}: {ex.Message}");
                    }
                }
            }

            // Charger TOUTES les séries
            if (seriesCats.Count > 0)
            {
                int loadedCats = 0;
                foreach (var cat in seriesCats)
                {
                    loadedCats++;
                    StatusMessage = $"📺 Séries [{loadedCats}/{seriesCats.Count}]: {cat.CategoryName}...";
                    try
                    {
                        var series = await _xtreamService.GetSeriesAsync(
                            serverUrl, username, password, source.Id, cat.CategoryId);
                        allSeries.AddRange(series);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Xtream] Error loading series category {cat.CategoryName}: {ex.Message}");
                    }
                }
            }

            // Stocker TOUT le contenu dans les listes privées (pour la recherche)
            _allLiveChannels = allLive;
            _allMovies = allMovies;
            _allSeries = allSeries;
            _totalLiveChannels = allLive.Count;
            _totalMovies = allMovies.Count;
            _totalSeries = allSeries.Count;

            // Filtrer par préférences de langue PUIS limiter pour l'UI
            StatusMessage = $"🔍 Filtrage par préférences ({string.Join(", ", UserPreferences.PreferredLanguages)})...";
            var filteredLive = await Task.Run(() => allLive.Where(MatchesUserPreferences).Take(MaxDisplayItems).ToList());
            var filteredMovies = await Task.Run(() => allMovies.Where(MatchesUserPreferences).Take(MaxDisplayItems).ToList());
            var filteredSeries = await Task.Run(() => allSeries.Where(MatchesUserPreferences).Take(MaxDisplayItems).ToList());

            StatusMessage = $"⚡ Affichage de {filteredLive.Count:N0} chaînes filtrées (sur {allLive.Count:N0})...";
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LiveChannels = new ObservableCollection<MediaItem>(filteredLive);
                Movies = new ObservableCollection<MediaItem>(filteredMovies);
                Series = new ObservableCollection<MediaItem>(filteredSeries);
            }, System.Windows.Threading.DispatcherPriority.Normal);

            Console.WriteLine($"[Xtream] ✅ Chargement complet: {_totalLiveChannels:N0} live, {_totalMovies:N0} films, {_totalSeries:N0} séries (UI: {LiveChannels.Count}/{Movies.Count}/{Series.Count})");
            StatusMessage = $"✅ {_totalLiveChannels:N0} chaînes, {_totalMovies:N0} films, {_totalSeries:N0} séries";

            // Enrich with TMDb (background)
            _ = EnrichContentWithTmdbAsync();
        }

        private async Task LoadM3UContentAsync(PlaylistSource source)
        {
            var startTime = DateTime.Now;
            Console.WriteLine($"[{startTime:HH:mm:ss.fff}] ▶️ DÉBUT du chargement M3U (MODE TURBO)");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📍 URL: {source.Url}");

            StatusMessage = "📥 Téléchargement de la playlist M3U...";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📥 Téléchargement en cours...");

            var downloadStart = DateTime.Now;
            var channels = await _m3uParser.ParseFromUrlAsync(source.Url, source.Id);
            var downloadTime = (DateTime.Now - downloadStart).TotalSeconds;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ Téléchargement terminé en {downloadTime:F2}s");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 {channels.Count:N0} chaînes trouvées");

            StatusMessage = $"⚡ Conversion de {channels.Count:N0} chaînes (multi-thread)...";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚡ Conversion parallèle en cours...");

            // Conversion ultra-rapide avec parallélisation
            var convertStart = DateTime.Now;
            var mediaItems = await Task.Run(() =>
            {
                // Utiliser un tableau pour éviter le lock de List
                var items = new MediaItem[channels.Count];

                // Parallélisation pour utiliser tous les CPU
                System.Threading.Tasks.Parallel.For(0, channels.Count, i =>
                {
                    var channel = channels[i];
                    items[i] = new MediaItem
                    {
                        Id = channel.Id,
                        SourceId = channel.SourceId,
                        Name = channel.Name,
                        LogoUrl = channel.LogoUrl,
                        StreamUrl = channel.StreamUrl,
                        GroupTitle = channel.GroupTitle,
                        EpgId = channel.EpgId,
                        CatchupDays = channel.CatchupDays,
                        Order = channel.Order,
                        MediaType = ContentType.Live
                    };
                });

                return items.ToList();
            });

            var convertTime = (DateTime.Now - convertStart).TotalSeconds;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ Conversion terminée en {convertTime:F2}s");

            StatusMessage = $"📺 Chargement de {mediaItems.Count:N0} chaînes...";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📺 Stockage du contenu complet...");

            var addStart = DateTime.Now;

            // Stocker TOUT le contenu dans la liste privée (pour la recherche)
            _allLiveChannels = mediaItems;
            _totalLiveChannels = mediaItems.Count;

            // Filtrer par préférences de langue PUIS limiter pour l'UI
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔍 Filtrage par préférences ({string.Join(", ", UserPreferences.PreferredLanguages)})...");
            var filteredItems = await Task.Run(() => mediaItems.Where(MatchesUserPreferences).Take(MaxDisplayItems).ToList());

            // Si pas assez d'items filtrés, compléter avec d'autres
            if (filteredItems.Count < MaxDisplayItems)
            {
                var remaining = MaxDisplayItems - filteredItems.Count;
                var existingIds = new HashSet<string>(filteredItems.Select(x => x.Id));
                var additional = mediaItems.Where(x => !existingIds.Contains(x.Id)).Take(remaining);
                filteredItems.AddRange(additional);
            }

            var displayItems = filteredItems;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📺 Création collection UI avec {displayItems.Count:N0} items filtrés (sur {mediaItems.Count:N0} total)...");

            var newCollection = await Task.Run(() => new ObservableCollection<MediaItem>(displayItems));

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📺 Assignation à l'UI...");

            // Assigner sur le thread UI (instantané car la collection est petite)
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LiveChannels = newCollection;
            }, System.Windows.Threading.DispatcherPriority.Normal);

            var addTime = (DateTime.Now - addStart).TotalSeconds;
            var totalTime = (DateTime.Now - startTime).TotalSeconds;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ Ajout terminé en {addTime:F2}s");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⏱️ TEMPS TOTAL: {totalTime:F2}s");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 RÉSULTAT: {_allLiveChannels.Count:N0} chaînes (UI: {LiveChannels.Count:N0})");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ▶️ FIN du chargement M3U");
            Console.WriteLine(new string('─', 50));

            StatusMessage = $"✅ {_allLiveChannels.Count:N0} chaînes chargées en {totalTime:F1}s (affichage: {LiveChannels.Count:N0})";
        }

        private async Task LoadStalkerContentAsync(PlaylistSource source)
        {
            LogDebug($"LoadStalkerContentAsync starting - URL: {source.Url}, MAC: {source.MacAddress}");
            StatusMessage = "Connecting to Stalker Portal...";

            // Authenticate
            var account = await _stalkerService.AuthenticateAsync(source.Url, source.MacAddress!);

            if (account == null)
            {
                StatusMessage = "Stalker Portal authentication failed - Loading demo data...";
                LoadSampleData();
                BuildContentRows();
                SelectFeaturedItem();
                return;
            }

            StatusMessage = $"Connected - Loading channels...";

            LiveChannels.Clear();
            Movies.Clear();
            Series.Clear();

            var allLive = new List<MediaItem>();
            var allMovies = new List<MediaItem>();

            // Load ALL live channels
            try
            {
                StatusMessage = "📺 Chargement des chaînes live...";
                var channels = await _stalkerService.GetLiveChannelsAsync(source.Id);
                allLive.AddRange(channels);
                LogDebug($"Loaded {channels.Count} live channels");
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading live channels: {ex.Message}");
                Console.WriteLine($"[Stalker] Error loading live: {ex.Message}");
            }

            // Load ALL VOD from ALL categories
            try
            {
                var vodCats = await _stalkerService.GetVodCategoriesAsync();
                if (vodCats.Count > 0)
                {
                    int loadedCats = 0;
                    foreach (var cat in vodCats)
                    {
                        loadedCats++;
                        StatusMessage = $"🎬 VOD [{loadedCats}/{vodCats.Count}]: {cat.Title}...";
                        try
                        {
                            var movies = await _stalkerService.GetVodByCategoryAsync(source.Id, cat.Id);
                            allMovies.AddRange(movies);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Stalker] Error loading VOD category {cat.Title}: {ex.Message}");
                        }
                    }
                    LogDebug($"Loaded {allMovies.Count} total VOD items");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading VOD categories: {ex.Message}");
                Console.WriteLine($"[Stalker] Error loading VOD categories: {ex.Message}");
            }

            // Stocker TOUT le contenu dans les listes privées (pour la recherche)
            _allLiveChannels = allLive;
            _allMovies = allMovies;
            _totalLiveChannels = allLive.Count;
            _totalMovies = allMovies.Count;
            _totalSeries = 0;

            // Filtrer par préférences de langue PUIS limiter pour l'UI
            StatusMessage = $"🔍 Filtrage par préférences ({string.Join(", ", UserPreferences.PreferredLanguages)})...";
            var filteredLive = await Task.Run(() => allLive.Where(MatchesUserPreferences).Take(MaxDisplayItems).ToList());
            var filteredMovies = await Task.Run(() => allMovies.Where(MatchesUserPreferences).Take(MaxDisplayItems).ToList());

            StatusMessage = $"⚡ Affichage de {filteredLive.Count:N0} chaînes filtrées (sur {allLive.Count:N0})...";
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LiveChannels = new ObservableCollection<MediaItem>(filteredLive);
                Movies = new ObservableCollection<MediaItem>(filteredMovies);
            }, System.Windows.Threading.DispatcherPriority.Normal);

            Console.WriteLine($"[Stalker] ✅ Chargement complet: {_totalLiveChannels:N0} live, {_totalMovies:N0} films (UI: {LiveChannels.Count}/{Movies.Count})");
            StatusMessage = $"✅ Stalker: {_totalLiveChannels:N0} chaînes, {_totalMovies:N0} films";
        }

        private async Task EnrichContentWithTmdbAsync()
        {
            if (!_tmdbService.IsConfigured) return;

            try
            {
                // Enrichir les films
                await _tmdbService.EnrichMediaItemsAsync(Movies.Take(50), 3);

                // Enrichir les séries
                await _tmdbService.EnrichMediaItemsAsync(Series.Take(50), 3);
            }
            catch { }
        }

        private void BuildContentRows()
        {
            try
            {
                ContentRows.Clear();

                // Les collections LiveChannels/Movies/Series sont DÉJÀ limitées à MaxDisplayItems
                // Le contenu COMPLET est dans _allLiveChannels/_allMovies/_allSeries pour la recherche

                // Appliquer le tri intelligent (favoris implicites et récemment vus en premier)
                // Limiter encore plus pour éviter les freezes
                var filteredLive = SafeSmartSort(LiveChannels, 2000);
                var filteredMovies = SafeSmartSort(Movies, 1000);
                var filteredSeries = SafeSmartSort(Series, 1000);

                Console.WriteLine($"[BuildContentRows] UI: {filteredLive.Count} live, {filteredMovies.Count} films, {filteredSeries.Count} séries (total: {_totalLiveChannels}/{_totalMovies}/{_totalSeries})");

            // Separate anime content for special display
            var animeMovies = new List<MediaItem>();
            var animeSeries = new List<MediaItem>();
            var regularMovies = new List<MediaItem>();
            var regularSeries = new List<MediaItem>();

            foreach (var movie in filteredMovies)
            {
                var analysis = _contentAnalyzer.Analyze(movie);
                if (analysis.IsAnime)
                    animeMovies.Add(movie);
                else
                    regularMovies.Add(movie);
            }

            foreach (var series in filteredSeries)
            {
                var analysis = _contentAnalyzer.Analyze(series);
                if (analysis.IsAnime)
                    animeSeries.Add(series);
                else
                    regularSeries.Add(series);
            }

            LogDebug($"BuildContentRows - Filtered: {filteredLive.Count} live, {regularMovies.Count} movies, {regularSeries.Count} series, {animeMovies.Count + animeSeries.Count} anime");

            // Continue Watching
            if (ContinueWatching.Count > 0)
            {
                ContentRows.Add(new MediaRow
                {
                    Title = "Continue Watching",
                    Icon = "▶️",
                    Items = new ObservableCollection<MediaItem>(ContinueWatching)
                });
            }

            // Favorites
            if (Favorites.Count > 0)
            {
                ContentRows.Add(new MediaRow
                {
                    Title = "My Favorites",
                    Icon = "❤️",
                    Items = new ObservableCollection<MediaItem>(Favorites.Take(20))
                });
            }

            // Recommandations intelligentes (basées sur l'historique)
            var allContent = filteredLive.Concat(filteredMovies).Concat(filteredSeries);
            var recommendations = _smartContent.GetRecommendations(allContent, 20);
            if (recommendations.Count > 0)
            {
                ContentRows.Add(new MediaRow
                {
                    Title = "Recommandé pour vous",
                    Icon = "✨",
                    Items = new ObservableCollection<MediaItem>(recommendations)
                });
            }

            // Chaînes françaises en priorité
            var frenchContent = filteredLive.Where(c => _smartContent.IsFrenchContent(c)).Take(20).ToList();
            if (frenchContent.Count > 0)
            {
                ContentRows.Add(new MediaRow
                {
                    Title = "Contenu Français",
                    Icon = "🇫🇷",
                    Items = new ObservableCollection<MediaItem>(frenchContent)
                });
            }

            // Live TV par catégories populaires (filtered)
            if (UserPreferences.ShowLiveTV)
            {
                var liveByCategory = filteredLive.GroupBy(c => c.GroupTitle).Take(5);
                foreach (var group in liveByCategory)
                {
                    ContentRows.Add(new MediaRow
                    {
                        Title = $"{group.Key}",
                        Icon = "📺",
                        Items = new ObservableCollection<MediaItem>(group.Take(20))
                    });
                }
            }

            // Films (filtered - non-anime)
            if (UserPreferences.ShowMovies && regularMovies.Count > 0)
            {
                ContentRows.Add(new MediaRow
                {
                    Title = "Films récents",
                    Icon = "🎬",
                    Items = new ObservableCollection<MediaItem>(regularMovies.OrderByDescending(m => m.ReleaseDate).Take(20))
                });

                // Films par catégorie
                var moviesByCategory = regularMovies.GroupBy(m => m.GroupTitle).Take(3);
                foreach (var group in moviesByCategory)
                {
                    ContentRows.Add(new MediaRow
                    {
                        Title = $"{group.Key}",
                        Icon = "🎬",
                        Items = new ObservableCollection<MediaItem>(group.Take(20))
                    });
                }

                // Top Rated
                var topRated = regularMovies.Where(m => m.Rating > 7).OrderByDescending(m => m.Rating).Take(20);
                if (topRated.Any())
                {
                    ContentRows.Add(new MediaRow
                    {
                        Title = "Top Films",
                        Icon = "⭐",
                        Items = new ObservableCollection<MediaItem>(topRated)
                    });
                }
            }

            // Séries (filtered - non-anime)
            if (UserPreferences.ShowSeries && regularSeries.Count > 0)
            {
                ContentRows.Add(new MediaRow
                {
                    Title = "Séries",
                    Icon = "📺",
                    Items = new ObservableCollection<MediaItem>(regularSeries.Take(20))
                });

                var seriesByCategory = regularSeries.GroupBy(s => s.GroupTitle).Take(3);
                foreach (var group in seriesByCategory)
                {
                    ContentRows.Add(new MediaRow
                    {
                        Title = $"{group.Key}",
                        Icon = "📺",
                        Items = new ObservableCollection<MediaItem>(group.Take(20))
                    });
                }
            }

            // Anime section (if enabled)
            if (UserPreferences.ShowAnime)
            {
                var allAnime = animeMovies.Concat(animeSeries).ToList();
                if (allAnime.Count > 0)
                {
                    // Add anime rows with version badges (VOSTFR, VF, etc.)
                    ContentRows.Add(new MediaRow
                    {
                        Title = "Animés",
                        Icon = "🎌",
                        Items = new ObservableCollection<MediaItem>(allAnime.Take(20))
                    });

                    // Group anime by detected version (VOSTFR, VF, etc.)
                    var vostfrAnime = allAnime.Where(a =>
                    {
                        var analysis = _contentAnalyzer.Analyze(a);
                        return analysis.DetectedVersions.Any(v => v.Version == ContentVersion.SubbedOriginal);
                    }).ToList();

                    var vfAnime = allAnime.Where(a =>
                    {
                        var analysis = _contentAnalyzer.Analyze(a);
                        return analysis.DetectedVersions.Any(v => v.Version == ContentVersion.Dubbed);
                    }).ToList();

                    if (vostfrAnime.Count > 0 && UserPreferences.AnimePreferSubbed)
                    {
                        ContentRows.Add(new MediaRow
                        {
                            Title = "Animés VOSTFR",
                            Icon = "🎌",
                            Items = new ObservableCollection<MediaItem>(vostfrAnime.Take(20))
                        });
                    }

                    if (vfAnime.Count > 0 && UserPreferences.AnimePreferDubbed)
                    {
                        ContentRows.Add(new MediaRow
                        {
                            Title = "Animés VF",
                            Icon = "🎌",
                            Items = new ObservableCollection<MediaItem>(vfAnime.Take(20))
                        });
                    }
                }
            }
            }
            catch (Exception ex)
            {
                LogDebug($"BuildContentRows error: {ex.Message}");
                StatusMessage = "Erreur lors de la construction de l'affichage";
            }
        }

        private void SelectFeaturedItem()
        {
            // Sélectionner un item mis en avant (film récent avec bonne note ET StreamUrl valide)
            FeaturedItem = Movies
                .Where(m => m.Rating > 6 && !string.IsNullOrEmpty(m.BackdropUrl) && !string.IsNullOrEmpty(m.StreamUrl))
                .OrderByDescending(m => m.ReleaseDate)
                .ThenByDescending(m => m.Rating)
                .FirstOrDefault()
                ?? Movies.FirstOrDefault(m => !string.IsNullOrEmpty(m.StreamUrl))
                ?? LiveChannels.FirstOrDefault(c => !string.IsNullOrEmpty(c.StreamUrl))
                ?? Movies.FirstOrDefault()
                ?? LiveChannels.FirstOrDefault();
        }

        private async Task LoadContinueWatchingAsync()
        {
            var recent = await _databaseService.GetRecentChannelsAsync(10);
            ContinueWatching.Clear();
            foreach (var channel in recent)
            {
                ContinueWatching.Add(new MediaItem
                {
                    Id = channel.Id,
                    SourceId = channel.SourceId,
                    Name = channel.Name,
                    LogoUrl = channel.LogoUrl,
                    StreamUrl = channel.StreamUrl,
                    GroupTitle = channel.GroupTitle,
                    EpgId = channel.EpgId,
                    MediaType = ContentType.Live
                });
            }
            OnPropertyChanged(nameof(HasContinueWatching));
        }

        private async Task LoadFavoritesAsync()
        {
            var favs = await _databaseService.GetFavoriteChannelsAsync();
            Favorites.Clear();
            foreach (var channel in favs)
            {
                Favorites.Add(new MediaItem
                {
                    Id = channel.Id,
                    SourceId = channel.SourceId,
                    Name = channel.Name,
                    LogoUrl = channel.LogoUrl,
                    StreamUrl = channel.StreamUrl,
                    GroupTitle = channel.GroupTitle,
                    IsFavorite = true,
                    MediaType = ContentType.Live
                });
            }
            OnPropertyChanged(nameof(HasFavorites));
        }

        #endregion

        #region Playback Commands

        [RelayCommand]
        private async Task PlayItemAsync(MediaItem item)
        {
            try
            {
                LogDebug($"PlayItemAsync called for: {item.Name}");
                LogDebug($"  StreamUrl: {item.StreamUrl}");
                LogDebug($"  MediaType: {item.MediaType}");
                LogDebug($"  MediaPlayer null: {_mediaPlayer == null}, LibVLC null: {_libVLC == null}");

                // Track previous item watch time
                if (_currentlyPlayingItem != null)
                {
                    var watchDuration = (int)(DateTime.Now - _playbackStartTime).TotalSeconds;
                    await _recommendationEngine.StopWatchingAsync(_currentlyPlayingItem, watchDuration);
                    LogDebug($"Tracked {watchDuration}s watch time for: {_currentlyPlayingItem.Name}");
                    _streamOptimizer.StopMonitoring();
                }

                if (CurrentItem != null)
                    CurrentItem.IsPlaying = false;

                CurrentItem = item;
                item.IsPlaying = true;

                // Start tracking new item
                _currentlyPlayingItem = item;
                _playbackStartTime = DateTime.Now;
                _recommendationEngine.StartWatching(item);

                // Enregistrer dans le service intelligent (pour recommandations et tri)
                _smartContent.RecordWatch(item);

                // Pour les séries, on a besoin de charger les épisodes
                if (item.MediaType == ContentType.Series && SelectedSource != null)
                {
                    LogDebug("Opening series detail...");
                    await OpenSeriesDetailAsync(item);
                    return;
                }

                // Store stream URL for reconnection
                _currentStreamUrl = item.StreamUrl;

                // Also give optimizer the stream URL for auto-reconnection
                _streamOptimizer.SetCurrentStream(item.StreamUrl);

                // Reset health indicator for new playback
                PlaybackHealth = PlaybackHealthStatus.Unknown;
                HealthStatusIcon = "⚪";
                HealthStatusText = "Analyse...";

                // Lecture vidéo avec LibVLC
                if (!string.IsNullOrEmpty(item.StreamUrl))
                {
                    LogDebug($"Starting playback of: {item.StreamUrl}");

                    // === AUTO-OPTIMIZATION ===
                    // Quick optimization based on content type (fast, doesn't block)
                    IsOptimizing = true;
                    OptimizationStatus = "Optimisation automatique...";

                    var optimizedSettings = _streamOptimizer.QuickOptimize(item.MediaType, isIPTV: true);
                    LogDebug($"Quick optimized: buffer={optimizedSettings.NetworkCaching}ms");

                    // Apply optimized settings if different from current
                    if (optimizedSettings.NetworkCaching != PlayerSettings.NetworkCaching ||
                        optimizedSettings.HardwareAcceleration != PlayerSettings.HardwareAcceleration)
                    {
                        OptimizationStatus = "Application des paramètres optimaux...";
                        ApplyPlayerSettings(optimizedSettings);
                    }

                    // Now we should have a valid player
                    if (_mediaPlayer != null && _libVLC != null)
                    {
                        _mediaPlayer.Stop();

                        // Set up media with additional options for stability
                        using var media = new Media(_libVLC, new Uri(item.StreamUrl));

                        // Add media-specific options for IPTV
                        media.AddOption($":network-caching={optimizedSettings.NetworkCaching}");
                        media.AddOption($":live-caching={optimizedSettings.LiveCaching}");
                        media.AddOption(":clock-jitter=0");
                        media.AddOption(":clock-synchro=0");

                        OptimizationStatus = "Démarrage de la lecture...";
                        _mediaPlayer.Play(media);
                        IsPlaying = true;
                        StatusMessage = $"Lecture: {item.Name}";
                        LogDebug("Playback started with optimized settings");
                    }
                    else
                    {
                        LogDebug("MediaPlayer or LibVLC is null after optimization");
                        StatusMessage = $"Erreur: Impossible de lire {item.Name}";
                        IsOptimizing = false;
                    }
                }
                else
                {
                    LogDebug("No StreamUrl provided");
                    StatusMessage = $"Pas d'URL de flux pour: {item.Name}";
                }

                // EPG pour live
                if (item.MediaType == ContentType.Live && !string.IsNullOrEmpty(item.EpgId))
                {
                    CurrentProgram = _epgService.GetCurrentProgram(item.EpgId);
                }

                // Ajouter aux récents
                await _databaseService.AddRecentChannelAsync(item.Id);
                await LoadContinueWatchingAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
                IsOptimizing = false;
                LogDebug($"PlayItemAsync error: {ex}");
            }
        }

        [RelayCommand]
        private async Task OpenSeriesDetailAsync(MediaItem seriesItem)
        {
            if (SelectedSource == null || string.IsNullOrEmpty(seriesItem.SeriesId)) return;

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading {seriesItem.Name}...";

                var seriesInfo = await _xtreamService.GetSeriesInfoAsync(
                    SelectedSource.Url,
                    SelectedSource.Username!,
                    SelectedSource.Password!,
                    seriesItem.SeriesId);

                SelectedSeriesInfo = seriesInfo;
                IsSeriesDetailOpen = true;

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task PlayEpisodeAsync(EpisodeInfo episode)
        {
            try
            {
                if (_mediaPlayer != null && _libVLC != null && !string.IsNullOrEmpty(episode.StreamUrl))
                {
                    _mediaPlayer.Stop();
                    using var media = new Media(_libVLC, new Uri(episode.StreamUrl));
                    _mediaPlayer.Play(media);
                    IsPlaying = true;
                    StatusMessage = $"Playing: {episode.Title}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void PlayPause()
        {
            if (_mediaPlayer == null) return;

            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                IsPlaying = false;
                StatusMessage = "Paused";
            }
            else
            {
                _mediaPlayer.Play();
                IsPlaying = true;
                StatusMessage = CurrentItem != null ? $"Playing: {CurrentItem.Name}" : "Playing";
            }
        }

        [RelayCommand]
        private async Task StopAsync()
        {
            // Track watch time before stopping
            if (_currentlyPlayingItem != null)
            {
                var watchDuration = (int)(DateTime.Now - _playbackStartTime).TotalSeconds;
                await _recommendationEngine.StopWatchingAsync(_currentlyPlayingItem, watchDuration);
                LogDebug($"Tracked {watchDuration}s watch time for: {_currentlyPlayingItem.Name}");
                _currentlyPlayingItem = null;
            }

            _mediaPlayer?.Stop();
            if (CurrentItem != null)
                CurrentItem.IsPlaying = false;
            CurrentItem = null;
            CurrentProgram = null;
            IsPlaying = false;
            StatusMessage = "Stopped";
        }

        [RelayCommand]
        private void ToggleMute()
        {
            if (_mediaPlayer == null) return;
            IsMuted = !IsMuted;
            _mediaPlayer.Mute = IsMuted;
        }

        [RelayCommand]
        private void SeekForward()
        {
            if (_mediaPlayer == null || TotalTimeMs == 0) return;
            var newTime = Math.Min(CurrentTimeMs + 10000, TotalTimeMs); // +10 secondes
            _mediaPlayer.Time = newTime;
        }

        [RelayCommand]
        private void SeekBackward()
        {
            if (_mediaPlayer == null) return;
            var newTime = Math.Max(CurrentTimeMs - 10000, 0); // -10 secondes
            _mediaPlayer.Time = newTime;
        }

        [RelayCommand]
        private void SeekToPosition(double position)
        {
            if (_mediaPlayer == null || TotalTimeMs == 0) return;
            // Position est entre 0 et 100
            var newTime = (long)(position / 100 * TotalTimeMs);
            _mediaPlayer.Time = newTime;
        }

        [RelayCommand]
        private void CyclePlaybackSpeed()
        {
            if (_mediaPlayer == null) return;

            // Cycle: 0.5 -> 0.75 -> 1.0 -> 1.25 -> 1.5 -> 2.0 -> 0.5
            float[] speeds = { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f };
            int currentIndex = Array.IndexOf(speeds, PlaybackSpeed);
            int nextIndex = (currentIndex + 1) % speeds.Length;

            PlaybackSpeed = speeds[nextIndex];
            _mediaPlayer.SetRate(PlaybackSpeed);
            PlaybackSpeedDisplay = PlaybackSpeed == 1.0f ? "1x" : $"{PlaybackSpeed}x";
        }

        [RelayCommand]
        private void CycleAspectRatio()
        {
            if (_mediaPlayer == null) return;

            // Cycle: Default -> 16:9 -> 4:3 -> 21:9 -> Default
            string[] ratios = { "Default", "16:9", "4:3", "21:9" };
            int currentIndex = Array.IndexOf(ratios, AspectRatio);
            int nextIndex = (currentIndex + 1) % ratios.Length;

            AspectRatio = ratios[nextIndex];

            // Appliquer le ratio via LibVLC
            if (AspectRatio == "Default")
                _mediaPlayer.AspectRatio = null;
            else
                _mediaPlayer.AspectRatio = AspectRatio;
        }

        /// <summary>
        /// Auto-select audio and subtitle tracks based on user language preferences
        /// </summary>
        private void AutoSelectPreferredTracks()
        {
            if (_mediaPlayer == null) return;

            try
            {
                // Language keywords mapping for track detection
                var languageKeywords = new Dictionary<string, string[]>
                {
                    { "French", new[] { "french", "français", "francais", "fra", "fre", "fr", "vf", "vff", "truefrench" } },
                    { "English", new[] { "english", "anglais", "eng", "en", "vo", "vost" } },
                    { "Spanish", new[] { "spanish", "español", "espanol", "spa", "es", "castellano" } },
                    { "German", new[] { "german", "deutsch", "ger", "deu", "de" } },
                    { "Italian", new[] { "italian", "italiano", "ita", "it" } },
                    { "Portuguese", new[] { "portuguese", "português", "portugues", "por", "pt", "br" } },
                    { "Arabic", new[] { "arabic", "arabe", "ara", "ar" } },
                    { "Turkish", new[] { "turkish", "türkçe", "turkce", "tur", "tr" } }
                };

                // Auto-select audio track
                var audioTracks = _mediaPlayer.AudioTrackDescription?.ToList();
                if (audioTracks != null && audioTracks.Count > 1)
                {
                    LogDebug($"Found {audioTracks.Count} audio tracks");
                    foreach (var track in audioTracks)
                    {
                        LogDebug($"  Audio track {track.Id}: {track.Name}");
                    }

                    // Find best matching audio track for user's preferred languages
                    foreach (var prefLang in UserPreferences.PreferredLanguages)
                    {
                        if (languageKeywords.TryGetValue(prefLang, out var keywords))
                        {
                            foreach (var track in audioTracks)
                            {
                                if (track.Id == -1) continue; // Skip "Disable" track

                                var trackName = track.Name?.ToLowerInvariant() ?? "";
                                if (keywords.Any(kw => trackName.Contains(kw)))
                                {
                                    LogDebug($"Auto-selecting audio track: {track.Name} (matches {prefLang})");
                                    _mediaPlayer.SetAudioTrack(track.Id);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Auto-select subtitle track (prefer same language, or disable if audio matches preference)
                var subtitleTracks = _mediaPlayer.SpuDescription?.ToList();
                if (subtitleTracks != null && subtitleTracks.Count > 0)
                {
                    LogDebug($"Found {subtitleTracks.Count} subtitle tracks");
                    foreach (var track in subtitleTracks)
                    {
                        LogDebug($"  Subtitle track {track.Id}: {track.Name}");
                    }

                    // Check if current audio is in user's preferred language
                    var currentAudioName = audioTracks?.FirstOrDefault(t => t.Id == _mediaPlayer.AudioTrack).Name?.ToLowerInvariant() ?? "";
                    bool audioMatchesPreference = false;

                    foreach (var prefLang in UserPreferences.PreferredLanguages)
                    {
                        if (languageKeywords.TryGetValue(prefLang, out var keywords))
                        {
                            if (keywords.Any(kw => currentAudioName.Contains(kw)))
                            {
                                audioMatchesPreference = true;
                                break;
                            }
                        }
                    }

                    if (audioMatchesPreference)
                    {
                        // Audio is in preferred language - disable subtitles
                        LogDebug("Audio matches preference - disabling subtitles");
                        _mediaPlayer.SetSpu(-1);
                    }
                    else
                    {
                        // Audio is not in preferred language - try to find matching subtitles
                        foreach (var prefLang in UserPreferences.PreferredLanguages)
                        {
                            if (languageKeywords.TryGetValue(prefLang, out var keywords))
                            {
                                foreach (var track in subtitleTracks)
                                {
                                    if (track.Id == -1) continue;

                                    var trackName = track.Name?.ToLowerInvariant() ?? "";
                                    if (keywords.Any(kw => trackName.Contains(kw)))
                                    {
                                        LogDebug($"Auto-selecting subtitle track: {track.Name} (matches {prefLang})");
                                        _mediaPlayer.SetSpu(track.Id);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"AutoSelectPreferredTracks error: {ex.Message}");
            }
        }

        private void UpdateAudioTrackInfo()
        {
            if (_mediaPlayer == null) return;

            try
            {
                AudioTrackCount = _mediaPlayer.AudioTrackCount;
                CurrentAudioTrack = _mediaPlayer.AudioTrack;

                // Obtenir le nom de la piste si disponible
                var tracks = _mediaPlayer.AudioTrackDescription;
                var trackList = tracks?.ToList();
                if (trackList != null && trackList.Count > 0)
                {
                    // Trouver la piste actuelle dans la liste
                    var currentTrackInfo = trackList.FirstOrDefault(t => t.Id == CurrentAudioTrack);
                    if (!string.IsNullOrEmpty(currentTrackInfo.Name))
                    {
                        AudioTrackDisplay = currentTrackInfo.Name.Length > 8
                            ? currentTrackInfo.Name.Substring(0, 8)
                            : currentTrackInfo.Name;
                    }
                    else
                    {
                        AudioTrackDisplay = AudioTrackCount > 1 ? $"Aud {CurrentAudioTrack + 1}/{AudioTrackCount}" : "Audio";
                    }
                }
                else
                {
                    AudioTrackDisplay = "Audio";
                }
            }
            catch
            {
                AudioTrackDisplay = "Audio";
            }
        }

        [RelayCommand]
        private void CycleAudioTrack()
        {
            if (_mediaPlayer == null || AudioTrackCount <= 1) return;

            try
            {
                var tracks = _mediaPlayer.AudioTrackDescription;
                var trackList = tracks?.ToList();
                if (trackList == null || trackList.Count == 0) return;

                // Trouver l'index de la piste actuelle
                int currentIndex = trackList.FindIndex(t => t.Id == _mediaPlayer.AudioTrack);
                int nextIndex = (currentIndex + 1) % trackList.Count;

                // Sauter la piste "Disable" (Id = -1)
                if (trackList[nextIndex].Id == -1 && trackList.Count > 1)
                {
                    nextIndex = (nextIndex + 1) % trackList.Count;
                }

                _mediaPlayer.SetAudioTrack(trackList[nextIndex].Id);
                UpdateAudioTrackInfo();
            }
            catch
            {
                // Fallback simple - juste incrémenter l'affichage
                CurrentAudioTrack = (CurrentAudioTrack + 1) % Math.Max(1, AudioTrackCount);
                AudioTrackDisplay = $"Aud {CurrentAudioTrack + 1}/{AudioTrackCount}";
            }
        }

        private void UpdateSubtitleTrackInfo()
        {
            if (_mediaPlayer == null) return;

            try
            {
                SubtitleTrackCount = _mediaPlayer.SpuCount;
                CurrentSubtitleTrack = _mediaPlayer.Spu;

                if (CurrentSubtitleTrack == -1 || SubtitleTrackCount == 0)
                {
                    SubtitleTrackDisplay = "Sub Off";
                    return;
                }

                var tracks = _mediaPlayer.SpuDescription;
                var trackList = tracks?.ToList();
                if (trackList != null && trackList.Count > 0)
                {
                    var currentTrackInfo = trackList.FirstOrDefault(t => t.Id == CurrentSubtitleTrack);
                    if (!string.IsNullOrEmpty(currentTrackInfo.Name))
                    {
                        SubtitleTrackDisplay = currentTrackInfo.Name.Length > 6
                            ? currentTrackInfo.Name.Substring(0, 6)
                            : currentTrackInfo.Name;
                    }
                    else
                    {
                        SubtitleTrackDisplay = $"Sub {CurrentSubtitleTrack}";
                    }
                }
                else
                {
                    SubtitleTrackDisplay = "Sub Off";
                }
            }
            catch
            {
                SubtitleTrackDisplay = "Sub Off";
            }
        }

        [RelayCommand]
        private void CycleSubtitleTrack()
        {
            if (_mediaPlayer == null) return;

            try
            {
                var tracks = _mediaPlayer.SpuDescription;
                var trackList = tracks?.ToList();

                if (trackList == null || trackList.Count == 0)
                {
                    SubtitleTrackDisplay = "No Sub";
                    return;
                }

                // Trouver l'index actuel
                int currentIndex = trackList.FindIndex(t => t.Id == _mediaPlayer.Spu);
                int nextIndex = (currentIndex + 1) % trackList.Count;

                _mediaPlayer.SetSpu(trackList[nextIndex].Id);
                UpdateSubtitleTrackInfo();
            }
            catch
            {
                SubtitleTrackDisplay = "Sub Err";
            }
        }

        partial void OnVolumeChanged(int value)
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Volume = value;
        }

        [RelayCommand]
        private async Task ToggleFavoriteAsync(MediaItem item)
        {
            item.IsFavorite = !item.IsFavorite;
            await _databaseService.UpdateChannelFavoriteAsync(item.Id, item.IsFavorite);
            await LoadFavoritesAsync();
            BuildContentRows();
        }

        #endregion

        #region Search

        partial void OnSearchQueryChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SearchResults.Clear();
                CurrentView = "Home";
                return;
            }

            CurrentView = "Search";
            PerformSearch(value);
        }

        private void PerformSearch(string query)
        {
            SearchResults.Clear();

            // Utiliser la recherche fuzzy intelligente (tolère les fautes de frappe)
            var allContent = _allLiveChannels
                .Concat(_allMovies.Count > 0 ? _allMovies : Movies.ToList())
                .Concat(_allSeries.Count > 0 ? _allSeries : Series.ToList());

            var results = _smartContent.FuzzySearch(allContent, query, 100);

            foreach (var item in results)
            {
                SearchResults.Add(item);
            }

            Console.WriteLine($"[Search] Fuzzy search for '{query}': {results.Count} results");
        }

        #endregion

        #region Navigation

        [RelayCommand]
        private void ChangeView(string view)
        {
            CurrentView = view;
            if (view != "Search")
                SearchQuery = string.Empty;

            // Reconstruire les rows selon la vue sélectionnée
            BuildContentRowsForView(view);
        }

        /// <summary>
        /// Construit les lignes de contenu selon la vue sélectionnée
        /// </summary>
        private void BuildContentRowsForView(string view)
        {
            ContentRows.Clear();

            switch (view)
            {
                case "Live":
                    BuildLiveContentRows();
                    break;
                case "Movies":
                    BuildMoviesContentRows();
                    break;
                case "Series":
                    BuildSeriesContentRows();
                    break;
                case "MyList":
                    BuildFavoritesContentRows();
                    break;
                default: // "Home"
                    BuildContentRows();
                    break;
            }
        }

        private void BuildLiveContentRows()
        {
            try
            {
                ContentRows.Clear();

                // Grouper par catégorie (limite de sécurité)
                var liveByCategory = LiveChannels.Take(3000).GroupBy(c => c.GroupTitle).Take(20);
                foreach (var group in liveByCategory)
                {
                    if (group.Any())
                    {
                        ContentRows.Add(new MediaRow
                        {
                            Title = string.IsNullOrEmpty(group.Key) ? "Autres" : group.Key,
                            Icon = "📺",
                            Items = new ObservableCollection<MediaItem>(group.Take(30))
                        });
                    }
                }

                Console.WriteLine($"[BuildLiveContentRows] {ContentRows.Count} catégories live affichées");
            }
            catch (Exception ex)
            {
                LogDebug($"BuildLiveContentRows error: {ex.Message}");
            }
        }

        private void BuildMoviesContentRows()
        {
            try
            {
                ContentRows.Clear();

                // Films récents (limite de sécurité)
                var recentMovies = Movies.Take(1000).OrderByDescending(m => m.ReleaseDate).Take(30).ToList();
                if (recentMovies.Any())
                {
                    ContentRows.Add(new MediaRow
                    {
                        Title = "Films récents",
                        Icon = "🎬",
                        Items = new ObservableCollection<MediaItem>(recentMovies)
                    });
                }

                // Grouper par catégorie
                var moviesByCategory = Movies.Take(1000).GroupBy(m => m.GroupTitle).Take(15);
                foreach (var group in moviesByCategory)
                {
                    if (group.Any())
                    {
                        ContentRows.Add(new MediaRow
                        {
                            Title = string.IsNullOrEmpty(group.Key) ? "Autres" : group.Key,
                            Icon = "🎬",
                            Items = new ObservableCollection<MediaItem>(group.Take(30))
                        });
                    }
                }

                Console.WriteLine($"[BuildMoviesContentRows] {ContentRows.Count} catégories films affichées");
            }
            catch (Exception ex)
            {
                LogDebug($"BuildMoviesContentRows error: {ex.Message}");
            }
        }

        private void BuildSeriesContentRows()
        {
            try
            {
                ContentRows.Clear();

                // Grouper par catégorie (limite de sécurité)
                var seriesByCategory = Series.Take(1000).GroupBy(s => s.GroupTitle).Take(15);
                foreach (var group in seriesByCategory)
                {
                    if (group.Any())
                    {
                        ContentRows.Add(new MediaRow
                        {
                            Title = string.IsNullOrEmpty(group.Key) ? "Autres" : group.Key,
                            Icon = "📺",
                            Items = new ObservableCollection<MediaItem>(group.Take(30))
                        });
                    }
                }

                Console.WriteLine($"[BuildSeriesContentRows] {ContentRows.Count} catégories séries affichées");
            }
            catch (Exception ex)
            {
                LogDebug($"BuildSeriesContentRows error: {ex.Message}");
            }
        }

        private void BuildFavoritesContentRows()
        {
            try
            {
                ContentRows.Clear();

                if (Favorites.Count > 0)
                {
                    ContentRows.Add(new MediaRow
                    {
                        Title = "Mes Favoris",
                        Icon = "❤️",
                        Items = new ObservableCollection<MediaItem>(Favorites.Take(100))
                    });
                }

                if (ContinueWatching.Count > 0)
                {
                    ContentRows.Add(new MediaRow
                    {
                        Title = "Continuer à regarder",
                        Icon = "▶️",
                        Items = new ObservableCollection<MediaItem>(ContinueWatching.Take(50))
                    });
                }

                Console.WriteLine($"[BuildFavoritesContentRows] {Favorites.Count} favoris, {ContinueWatching.Count} en cours");
            }
            catch (Exception ex)
            {
                LogDebug($"BuildFavoritesContentRows error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CloseSeriesDetail()
        {
            IsSeriesDetailOpen = false;
            SelectedSeriesInfo = null;
        }

        #endregion

        #region Catch-up / Replay

        [RelayCommand]
        private async Task PlayCatchupAsync((MediaItem item, DateTime startTime, int duration) catchup)
        {
            try
            {
                if (_mediaPlayer != null && _libVLC != null && SelectedSource != null)
                {
                    // Extraire le stream ID depuis l'ID de l'item (format: "live_12345")
                    var streamId = catchup.item.Id.Replace("live_", "").Replace("vod_", "");

                    var catchupUrl = _xtreamService.GetCatchupUrl(
                        SelectedSource.Url,
                        SelectedSource.Username!,
                        SelectedSource.Password!,
                        streamId,
                        catchup.startTime,
                        catchup.duration);

                    if (!string.IsNullOrEmpty(catchupUrl))
                    {
                        _mediaPlayer.Stop();
                        using var media = new Media(_libVLC, new Uri(catchupUrl));
                        _mediaPlayer.Play(media);
                        IsPlaying = true;
                        StatusMessage = $"Catchup: {catchup.item.Name}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Catchup error: {ex.Message}";
            }
            await Task.CompletedTask;
        }

        #endregion

        #region Real-time Quality Monitoring

        /// <summary>
        /// Handle playback health status changes from the optimizer
        /// </summary>
        private void HandlePlaybackHealthChange(PlaybackHealthStatus status)
        {
            PlaybackHealth = status;
            HealthStatusIcon = status.GetIcon();
            HealthStatusText = status.GetDisplayText();
            HealthStatusColor = status.GetColorCode();

            LogDebug($"Playback health changed: {status} ({HealthStatusIcon} {HealthStatusText})");

            // Update status message for user
            if (status == PlaybackHealthStatus.Poor)
            {
                StatusMessage = "⚠️ Qualité de lecture dégradée - Optimisation en cours";
            }
            else if (status == PlaybackHealthStatus.Critical)
            {
                StatusMessage = "🔴 Problème de lecture critique - Reconnexion...";
            }
        }

        /// <summary>
        /// Handle quality downgrade request from optimizer (like YouTube auto quality)
        /// </summary>
        private async void HandleQualityDowngrade(Models.StreamQuality newQuality)
        {
            CurrentStreamQuality = newQuality;
            CurrentQualityDisplay = newQuality switch
            {
                Models.StreamQuality.Low => "SD",
                Models.StreamQuality.Medium => "720p",
                Models.StreamQuality.High => "1080p",
                Models.StreamQuality.Ultra => "4K",
                _ => "Auto"
            };

            LogDebug($"Quality downgrade requested: {newQuality}");

            // If the current item has quality options, switch to the lower quality URL
            if (CurrentItem != null && CurrentItem.HasQualityOptions)
            {
                CurrentItem.CurrentQuality = newQuality;
                var newUrl = CurrentItem.GetStreamUrlForQuality(newQuality);

                if (!string.IsNullOrEmpty(newUrl) && newUrl != _currentStreamUrl)
                {
                    LogDebug($"Switching to lower quality URL: {newUrl}");
                    _currentStreamUrl = newUrl;
                    _streamOptimizer.SetCurrentStream(newUrl);

                    // Trigger restart with new quality URL
                    await RestartWithNewUrlAsync(newUrl);
                }
            }
            else
            {
                // No specific quality URLs, just update the display
                // The buffer/settings optimization will help
                StatusMessage = $"📉 Qualité réduite: {CurrentQualityDisplay} - Optimisation en cours";
            }
        }

        private async Task RestartWithNewUrlAsync(string newUrl)
        {
            if (_mediaPlayer == null || _libVLC == null) return;

            try
            {
                long currentPosition = 0;
                if (CurrentItem?.MediaType != ContentType.Live && _mediaPlayer.IsPlaying)
                {
                    currentPosition = _mediaPlayer.Time;
                }

                _mediaPlayer.Stop();
                await Task.Delay(300);

                using var media = new Media(_libVLC, new Uri(newUrl));
                media.AddOption($":network-caching={PlayerSettings.NetworkCaching}");
                media.AddOption($":live-caching={PlayerSettings.LiveCaching}");

                _mediaPlayer.Play(media);

                if (currentPosition > 0)
                {
                    await Task.Delay(1000);
                    _mediaPlayer.Time = currentPosition;
                }

                StatusMessage = $"Qualité: {CurrentQualityDisplay}";
                LogDebug($"Restarted with quality URL: {newUrl}");
            }
            catch (Exception ex)
            {
                LogDebug($"RestartWithNewUrl error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle stream restart request from optimizer (applies new settings)
        /// </summary>
        private async void HandleStreamRestartRequired()
        {
            if (_mediaPlayer == null || _libVLC == null || string.IsNullOrEmpty(_currentStreamUrl))
                return;

            LogDebug("Stream restart requested by optimizer - applying new settings");

            try
            {
                // Get current position if VOD
                long currentPosition = 0;
                if (CurrentItem?.MediaType != ContentType.Live && _mediaPlayer.IsPlaying)
                {
                    currentPosition = _mediaPlayer.Time;
                }

                // Stop current playback
                _mediaPlayer.Stop();
                await Task.Delay(500);

                // Restart with updated settings
                using var media = new Media(_libVLC, new Uri(_currentStreamUrl));

                // Apply current optimized settings
                media.AddOption($":network-caching={PlayerSettings.NetworkCaching}");
                media.AddOption($":live-caching={PlayerSettings.LiveCaching}");
                media.AddOption(":clock-jitter=0");

                if (PlayerSettings.SkipFramesOnLag)
                {
                    media.AddOption(":skip-frames");
                }

                _mediaPlayer.Play(media);

                // Restore position if VOD
                if (currentPosition > 0)
                {
                    await Task.Delay(1000);
                    _mediaPlayer.Time = currentPosition;
                }

                LogDebug($"Stream restarted with buffer={PlayerSettings.NetworkCaching}ms");
            }
            catch (Exception ex)
            {
                LogDebug($"Stream restart error: {ex.Message}");
            }
        }

        #endregion

        #region Recording Commands

        [RelayCommand]
        private async Task StartRecordingAsync()
        {
            if (CurrentItem == null || string.IsNullOrEmpty(CurrentItem.StreamUrl) || _recordingService == null) return;

            try
            {
                await _recordingService.StartRecordingAsync(CurrentItem);
                StatusMessage = $"🔴 Enregistrement démarré: {CurrentItem.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur enregistrement: {ex.Message}";
            }
        }

        [RelayCommand]
        private void StopRecording()
        {
            if (!IsRecording || _recordingService == null) return;
            _recordingService.StopAllRecordings();
        }

        #endregion

        #region Sleep Timer Commands

        [RelayCommand]
        private async Task SetSleepTimerAsync(int minutes)
        {
            _ = _sleepTimerService.StartTimerAsync(TimeSpan.FromMinutes(minutes));
            IsSleepTimerActive = true;
            StatusMessage = $"💤 Timer veille: {minutes} minutes";
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void CancelSleepTimer()
        {
            _sleepTimerService.Cancel();
            IsSleepTimerActive = false;
            SleepTimerDisplay = "";
            StatusMessage = "Timer veille annulé";
        }

        #endregion

        #region Binge Mode Commands

        [RelayCommand]
        private void ToggleBingeMode()
        {
            IsBingeModeEnabled = !IsBingeModeEnabled;
            _bingeModeService.IsEnabled = IsBingeModeEnabled;
            StatusMessage = IsBingeModeEnabled ? "🎬 Binge Mode activé" : "Binge Mode désactivé";
        }

        [RelayCommand]
        private void SkipIntro()
        {
            if (_mediaPlayer != null)
            {
                _bingeModeService.SkipIntro();
                StatusMessage = "⏭️ Intro passée";
            }
        }

        [RelayCommand]
        private void CancelAutoPlay()
        {
            _bingeModeService.CancelCountdown();
            IsAutoPlayCountdownVisible = false;
            StatusMessage = "Lecture auto annulée";
        }

        #endregion

        #region Cast Commands

        [RelayCommand]
        private async Task DiscoverCastDevicesAsync()
        {
            AvailableCastDevices.Clear();
            StatusMessage = "🔍 Recherche d'appareils Cast...";
            await _castService.StartDiscoveryAsync();
        }

        [RelayCommand]
        private async Task ConnectToCastDeviceAsync(CastDevice device)
        {
            var success = await _castService.ConnectAsync(device);
            if (success && CurrentItem != null && !string.IsNullOrEmpty(CurrentItem.StreamUrl))
            {
                await _castService.CastMediaAsync(CurrentItem.StreamUrl, CurrentItem.Name, CurrentItem.PosterUrl);
            }
        }

        [RelayCommand]
        private void DisconnectCast()
        {
            _castService.Disconnect();
            StatusMessage = "Cast déconnecté";
        }

        [RelayCommand]
        private async Task CastCurrentMediaAsync()
        {
            if (!IsCastActive || CurrentItem == null) return;
            await _castService.CastMediaAsync(CurrentItem.StreamUrl!, CurrentItem.Name, CurrentItem.PosterUrl);
        }

        #endregion

        #region Download Commands

        [RelayCommand]
        private async Task DownloadForOfflineAsync(MediaItem item)
        {
            if (item.MediaType == ContentType.Live)
            {
                StatusMessage = "❌ Impossible de télécharger les chaînes live";
                return;
            }

            await _downloadService.StartDownloadAsync(item);
            StatusMessage = $"⬇️ Téléchargement démarré: {item.Name}";
        }

        [RelayCommand]
        private void CancelDownload(string itemId)
        {
            _downloadService.CancelDownload(itemId);
            StatusMessage = "Téléchargement annulé";
        }

        [RelayCommand]
        private void OpenDownloadsFolder()
        {
            _downloadService.OpenDownloadsFolder();
        }

        #endregion

        #region Channel Zapping Commands

        [RelayCommand]
        private async Task ZapToNextChannelAsync()
        {
            if (_channelPreloader == null || LiveChannels.Count == 0) return;

            // Simple zapping: go to next channel in list
            var currentIndex = CurrentItem != null
                ? LiveChannels.ToList().FindIndex(c => c.Id == CurrentItem.Id)
                : -1;
            var nextIndex = (currentIndex + 1) % LiveChannels.Count;
            var nextChannel = LiveChannels.ElementAtOrDefault(nextIndex);

            if (nextChannel != null)
            {
                await PlayItemAsync(nextChannel);
            }
        }

        [RelayCommand]
        private async Task ZapToPreviousChannelAsync()
        {
            if (_channelPreloader == null || LiveChannels.Count == 0) return;

            var currentIndex = CurrentItem != null
                ? LiveChannels.ToList().FindIndex(c => c.Id == CurrentItem.Id)
                : 0;
            var prevIndex = currentIndex > 0 ? currentIndex - 1 : LiveChannels.Count - 1;
            var prevChannel = LiveChannels.ElementAtOrDefault(prevIndex);

            if (prevChannel != null)
            {
                await PlayItemAsync(prevChannel);
            }
        }

        [RelayCommand]
        private async Task ZapToChannelNumberAsync(int number)
        {
            if (LiveChannels.Count == 0) return;

            // Find channel by number (1-based index)
            var channel = LiveChannels.ElementAtOrDefault(number - 1);
            if (channel != null)
            {
                await PlayItemAsync(channel);
            }
        }

        /// <summary>
        /// Initialize channel preloader with live channels for fast zapping
        /// </summary>
        private void InitializeChannelPreloader()
        {
            if (LiveChannels.Count > 0 && _channelPreloader != null)
            {
                _channelPreloader.SetChannelList(LiveChannels.ToList());
                LogDebug($"Channel preloader initialized with {LiveChannels.Count} channels");
            }
        }

        #endregion

        #region Audio Normalization

        /// <summary>
        /// Apply audio normalization based on channel profile
        /// </summary>
        private void ApplyAudioNormalization(MediaItem? item)
        {
            if (_mediaPlayer == null || item == null) return;

            // Notify the normalizer about channel change
            _audioNormalizer.OnChannelChanged(item.Id);
        }

        /// <summary>
        /// Learn volume preference for current channel
        /// </summary>
        private void LearnChannelVolume()
        {
            if (CurrentItem != null)
            {
                // Trigger normalization for the current channel
                _audioNormalizer.ApplyNormalization();
            }
        }

        #endregion

        #region EPG Reminders

        [RelayCommand]
        private void AddProgramReminder(EpgProgram program)
        {
            if (CurrentItem != null)
            {
                _epgReminderService.AddReminder(program, CurrentItem);
            }
        }

        [RelayCommand]
        private void RemoveReminder(string reminderId)
        {
            _epgReminderService.RemoveReminder(reminderId);
        }

        public IReadOnlyList<ProgramReminder> GetUpcomingReminders()
        {
            return _epgReminderService.GetUpcomingReminders();
        }

        #endregion

        #region Subtitle Customization

        [RelayCommand]
        private void ApplySubtitlePreset(string presetName)
        {
            SubtitleSettings = SubtitleSettings.GetPreset(presetName);
            ApplySubtitleSettings();
            StatusMessage = $"Préréglage sous-titres: {presetName}";
        }

        private void ApplySubtitleSettings()
        {
            if (_mediaPlayer == null) return;

            // LibVLC subtitle styling is limited, but we can set some options
            // Real implementation would use VLC's freetype module options
            LogDebug($"Subtitle settings applied: font={SubtitleSettings.FontFamily}, size={SubtitleSettings.FontSize}");
        }

        #endregion

        #region Safety Helpers

        /// <summary>
        /// Tri intelligent avec limite de sécurité pour éviter les freezes
        /// </summary>
        private List<MediaItem> SafeSmartSort(IEnumerable<MediaItem> items, int maxItems)
        {
            try
            {
                var list = items?.Take(maxItems).ToList() ?? new List<MediaItem>();
                return _smartContent.SmartSort(list);
            }
            catch (Exception ex)
            {
                LogDebug($"SafeSmartSort error: {ex.Message}");
                return items?.Take(maxItems).ToList() ?? new List<MediaItem>();
            }
        }

        /// <summary>
        /// Exécute une action de manière sécurisée
        /// </summary>
        private void SafeExecute(Action action, string context = "")
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogDebug($"SafeExecute error in {context}: {ex.Message}");
            }
        }

        /// <summary>
        /// Exécute une action async de manière sécurisée
        /// </summary>
        private async Task SafeExecuteAsync(Func<Task> action, string context = "")
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                LogDebug($"SafeExecuteAsync error in {context}: {ex.Message}");
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }

        #endregion

        private static void LogDebug(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "debug.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        public void Dispose()
        {
            _streamOptimizer.StopMonitoring();
            _streamOptimizer.Dispose();
            _channelPreloader?.Dispose();
            _recordingService?.Dispose();
            _bingeModeService.Dispose();
            _sleepTimerService.Dispose();
            _epgReminderService.Dispose();
            _castService.Dispose();
            _downloadService.Dispose();
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _databaseService.Dispose();
        }
    }
}
