using System;
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
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly M3UParser _m3uParser;
        private readonly XtreamCodesService _xtreamService;
        private readonly EpgService _epgService;

        // LibVLCSharp pour la lecture vidéo
        private readonly LibVLC _libVLC;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;

        public LibVLCSharp.Shared.MediaPlayer? MediaPlayer => _mediaPlayer;

        [ObservableProperty]
        private ObservableCollection<PlaylistSource> _playlistSources = new();

        [ObservableProperty]
        private ObservableCollection<ChannelGroup> _channelGroups = new();

        [ObservableProperty]
        private ObservableCollection<Channel> _filteredChannels = new();

        [ObservableProperty]
        private ObservableCollection<Channel> _favoriteChannels = new();

        [ObservableProperty]
        private ObservableCollection<Channel> _recentChannels = new();

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private PlaylistSource? _selectedSource;

        // Computed properties for Netflix-style UI
        public bool HasRecentChannels => RecentChannels.Count > 0;
        public bool HasFavorites => FavoriteChannels.Count > 0;

        [ObservableProperty]
        private Channel? _currentChannel;

        [ObservableProperty]
        private EpgProgram? _currentProgram;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private bool _isMuted;

        [ObservableProperty]
        private int _volume = 100;

        [ObservableProperty]
        private bool _isFullScreen;

        [ObservableProperty]
        private string _currentView = "Channels";

        [ObservableProperty]
        private int _bufferSize = 2000;

        // Content type: Live, VOD, Series (comme Smarters Pro)
        [ObservableProperty]
        private string _contentType = "Live";

        [ObservableProperty]
        private ObservableCollection<MediaItem> _vodItems = new();

        [ObservableProperty]
        private ObservableCollection<MediaItem> _seriesItems = new();

        [ObservableProperty]
        private ObservableCollection<XtreamCategory> _currentCategories = new();

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
            _m3uParser = new M3UParser();
            _xtreamService = new XtreamCodesService();
            _epgService = new EpgService();

            // Initialiser LibVLC
            Core.Initialize();
            _libVLC = new LibVLC("--network-caching=2000", "--live-caching=2000");
            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            _mediaPlayer.Volume = _volume;
        }

        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Initializing...";

                // VLC initialization removed - video playback disabled

                // Initialize database
                await _databaseService.InitializeAsync();

                // Load data
                await LoadPlaylistSourcesAsync();
                await LoadFavoriteChannelsAsync();
                await LoadRecentChannelsAsync();

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

        private async Task LoadFavoriteChannelsAsync()
        {
            var favorites = await _databaseService.GetFavoriteChannelsAsync();
            FavoriteChannels.Clear();
            foreach (var channel in favorites)
            {
                FavoriteChannels.Add(channel);
            }
            OnPropertyChanged(nameof(HasFavorites));
        }

        private async Task LoadRecentChannelsAsync()
        {
            var recent = await _databaseService.GetRecentChannelsAsync();
            RecentChannels.Clear();
            foreach (var channel in recent)
            {
                RecentChannels.Add(channel);
            }
            OnPropertyChanged(nameof(HasRecentChannels));
        }

        partial void OnSelectedSourceChanged(PlaylistSource? value)
        {
            if (value != null)
            {
                _ = LoadChannelsForSourceAsync(value);
            }
        }

        private async Task LoadChannelsForSourceAsync(PlaylistSource source)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"Loading channels from {source.Name}...";

                System.Collections.Generic.List<Channel> channels;

                // Charger dynamiquement depuis l'API (comme Smarters Pro)
                if (source.Type == SourceType.XtreamCodes &&
                    !string.IsNullOrEmpty(source.Username) &&
                    !string.IsNullOrEmpty(source.Password))
                {
                    StatusMessage = "Connecting to Xtream Codes API...";
                    channels = await _xtreamService.GetLiveStreamsAsChannelsAsync(
                        source.Url, source.Username, source.Password, source.Id);
                }
                else
                {
                    // Fallback pour M3U (charger depuis cache DB)
                    channels = await _databaseService.GetChannelsAsync(source.Id);
                }

                OrganizeChannelsIntoGroups(channels);

                source.ChannelCount = channels.Count;
                StatusMessage = $"Loaded {channels.Count} channels";
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

        private void OrganizeChannelsIntoGroups(System.Collections.Generic.List<Channel> channels)
        {
            ChannelGroups.Clear();
            Categories.Clear();

            var grouped = channels.GroupBy(c => c.GroupTitle).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var channelGroup = new ChannelGroup { Name = group.Key };
                foreach (var channel in group.OrderBy(c => c.Order).ThenBy(c => c.Name))
                {
                    channelGroup.Channels.Add(channel);
                }
                ChannelGroups.Add(channelGroup);
                Categories.Add(group.Key);
            }

            FilterChannels();
        }

        public void FilterByCategory(string category)
        {
            SearchQuery = string.Empty;
            FilteredChannels.Clear();

            foreach (var group in ChannelGroups)
            {
                if (group.Name == category)
                {
                    foreach (var channel in group.Channels)
                    {
                        FilteredChannels.Add(channel);
                    }
                    break;
                }
            }

            OnPropertyChanged(nameof(FilteredChannels));
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterChannels();
        }

        private void FilterChannels()
        {
            FilteredChannels.Clear();

            foreach (var group in ChannelGroups)
            {
                foreach (var channel in group.Channels)
                {
                    if (string.IsNullOrWhiteSpace(SearchQuery) ||
                        channel.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        channel.GroupTitle.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        FilteredChannels.Add(channel);
                    }
                }
            }
        }

        [RelayCommand]
        private async Task AddM3UPlaylistAsync(string url)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Adding M3U playlist...";

                var source = new PlaylistSource
                {
                    Name = "M3U Playlist",
                    Type = SourceType.M3U,
                    Url = url,
                    LastSync = DateTime.Now
                };

                var channels = await _m3uParser.ParseFromUrlAsync(url, source.Id);
                source.ChannelCount = channels.Count;

                await _databaseService.SavePlaylistSourceAsync(source);
                await _databaseService.SaveChannelsAsync(channels);

                PlaylistSources.Add(source);
                SelectedSource = source;

                StatusMessage = $"Added {channels.Count} channels";
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
        private async Task AddXtreamPlaylistAsync((string url, string username, string password) credentials)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Connecting to Xtream Codes...";

                var accountInfo = await _xtreamService.AuthenticateAsync(
                    credentials.url, credentials.username, credentials.password);

                if (accountInfo == null)
                {
                    StatusMessage = "Authentication failed - check credentials";
                    return;
                }

                // Stocker seulement les credentials (comme Smarters Pro)
                var source = new PlaylistSource
                {
                    Name = $"IPTV - {credentials.username}",
                    Type = SourceType.XtreamCodes,
                    Url = credentials.url,
                    Username = credentials.username,
                    Password = credentials.password,
                    LastSync = DateTime.Now
                };

                // Sauvegarder uniquement la source (pas les chaînes)
                await _databaseService.SavePlaylistSourceAsync(source);

                PlaylistSources.Add(source);
                SelectedSource = source; // Ceci déclenchera LoadChannelsForSourceAsync automatiquement

                StatusMessage = $"Account added - Status: {accountInfo.Status}";
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
        private async Task RefreshPlaylistAsync()
        {
            if (SelectedSource == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing playlist...";

                await _databaseService.DeleteChannelsForSourceAsync(SelectedSource.Id);

                System.Collections.Generic.List<Channel> channels;
                if (SelectedSource.Type == SourceType.XtreamCodes)
                {
                    channels = await _xtreamService.GetLiveStreamsAsChannelsAsync(
                        SelectedSource.Url,
                        SelectedSource.Username!,
                        SelectedSource.Password!,
                        SelectedSource.Id);
                }
                else
                {
                    channels = await _m3uParser.ParseFromUrlAsync(SelectedSource.Url, SelectedSource.Id);
                }

                await _databaseService.SaveChannelsAsync(channels);
                SelectedSource.LastSync = DateTime.Now;
                SelectedSource.ChannelCount = channels.Count;
                await _databaseService.SavePlaylistSourceAsync(SelectedSource);

                await LoadChannelsForSourceAsync(SelectedSource);

                StatusMessage = $"Refreshed {channels.Count} channels";
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
        private async Task DeletePlaylistAsync(PlaylistSource source)
        {
            try
            {
                await _databaseService.DeletePlaylistSourceAsync(source.Id);
                PlaylistSources.Remove(source);

                if (SelectedSource == source)
                {
                    SelectedSource = PlaylistSources.FirstOrDefault();
                    if (SelectedSource == null)
                    {
                        ChannelGroups.Clear();
                        FilteredChannels.Clear();
                    }
                }

                StatusMessage = "Playlist deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PlayChannelAsync(Channel channel)
        {
            try
            {
                // Update current channel
                if (CurrentChannel != null)
                    CurrentChannel.IsPlaying = false;

                CurrentChannel = channel;
                channel.IsPlaying = true;

                // Lecture avec LibVLC
                if (_mediaPlayer != null && !string.IsNullOrEmpty(channel.StreamUrl))
                {
                    _mediaPlayer.Stop();
                    using var media = new Media(_libVLC, new Uri(channel.StreamUrl));
                    _mediaPlayer.Play(media);
                    IsPlaying = true;
                    StatusMessage = $"Playing: {channel.Name}";
                }

                // Update EPG
                if (!string.IsNullOrEmpty(channel.EpgId))
                {
                    CurrentProgram = _epgService.GetCurrentProgram(channel.EpgId);
                }

                // Add to recent
                await _databaseService.AddRecentChannelAsync(channel.Id);
                await LoadRecentChannelsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void PlayPause()
        {
            if (_mediaPlayer == null) return;

            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                IsPlaying = false;
            }
            else
            {
                _mediaPlayer.Play();
                IsPlaying = true;
            }
        }

        [RelayCommand]
        private void Stop()
        {
            _mediaPlayer?.Stop();
            if (CurrentChannel != null)
                CurrentChannel.IsPlaying = false;
            CurrentChannel = null;
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

        partial void OnVolumeChanged(int value)
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Volume = value;
        }

        [RelayCommand]
        private async Task ToggleFavoriteAsync(Channel channel)
        {
            channel.IsFavorite = !channel.IsFavorite;
            await _databaseService.UpdateChannelFavoriteAsync(channel.Id, channel.IsFavorite);
            await LoadFavoriteChannelsAsync();
        }

        [RelayCommand]
        private async Task LoadEpgAsync(string epgUrl)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading EPG...";

                await _epgService.LoadEpgFromUrlAsync(epgUrl);

                if (SelectedSource != null)
                {
                    SelectedSource.EpgUrl = epgUrl;
                    await _databaseService.SavePlaylistSourceAsync(SelectedSource);
                }

                StatusMessage = "EPG loaded successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading EPG: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ChangeView(string view)
        {
            CurrentView = view;
        }

        [RelayCommand]
        private async Task SwitchContentTypeAsync(string contentType)
        {
            if (SelectedSource == null || SelectedSource.Type != SourceType.XtreamCodes) return;

            ContentType = contentType;

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading {contentType}...";

                switch (contentType)
                {
                    case "Live":
                        await LoadChannelsForSourceAsync(SelectedSource);
                        break;
                    case "VOD":
                        await LoadVodContentAsync();
                        break;
                    case "Series":
                        await LoadSeriesContentAsync();
                        break;
                }
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

        private async Task LoadVodContentAsync()
        {
            if (SelectedSource == null) return;

            var items = await _xtreamService.GetVodStreamsAsync(
                SelectedSource.Url,
                SelectedSource.Username!,
                SelectedSource.Password!,
                SelectedSource.Id);

            VodItems.Clear();
            foreach (var item in items)
            {
                VodItems.Add(item);
            }

            // Organiser par catégorie
            var categories = await _xtreamService.GetVodCategoriesAsync(
                SelectedSource.Url,
                SelectedSource.Username!,
                SelectedSource.Password!);

            CurrentCategories.Clear();
            foreach (var cat in categories)
            {
                CurrentCategories.Add(cat);
            }

            StatusMessage = $"Loaded {items.Count} movies";
        }

        private async Task LoadSeriesContentAsync()
        {
            if (SelectedSource == null) return;

            var items = await _xtreamService.GetSeriesAsync(
                SelectedSource.Url,
                SelectedSource.Username!,
                SelectedSource.Password!,
                SelectedSource.Id);

            SeriesItems.Clear();
            foreach (var item in items)
            {
                SeriesItems.Add(item);
            }

            // Organiser par catégorie
            var categories = await _xtreamService.GetSeriesCategoriesAsync(
                SelectedSource.Url,
                SelectedSource.Username!,
                SelectedSource.Password!);

            CurrentCategories.Clear();
            foreach (var cat in categories)
            {
                CurrentCategories.Add(cat);
            }

            StatusMessage = $"Loaded {items.Count} series";
        }

        [RelayCommand]
        private async Task LoadSeriesEpisodesAsync(string seriesId)
        {
            if (SelectedSource == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Loading episodes...";

                var seriesInfo = await _xtreamService.GetSeriesInfoAsync(
                    SelectedSource.Url,
                    SelectedSource.Username!,
                    SelectedSource.Password!,
                    seriesId);

                if (seriesInfo != null)
                {
                    var totalEpisodes = seriesInfo.Seasons.Sum(s => s.Episodes.Count);
                    StatusMessage = $"Loaded {seriesInfo.Seasons.Count} seasons, {totalEpisodes} episodes";
                    // TODO: Display in UI
                }
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
        private async Task PlayMediaItemAsync(MediaItem item)
        {
            try
            {
                StatusMessage = $"Playing: {item.Name}";
                // TODO: Integrate video player
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ToggleFullScreen()
        {
            IsFullScreen = !IsFullScreen;
        }

        [RelayCommand]
        private void PreviousChannel()
        {
            if (CurrentChannel == null) return;

            var currentIndex = FilteredChannels.IndexOf(CurrentChannel);
            if (currentIndex > 0)
            {
                PlayChannelCommand.Execute(FilteredChannels[currentIndex - 1]);
            }
        }

        [RelayCommand]
        private void NextChannel()
        {
            if (CurrentChannel == null) return;

            var currentIndex = FilteredChannels.IndexOf(CurrentChannel);
            if (currentIndex < FilteredChannels.Count - 1)
            {
                PlayChannelCommand.Execute(FilteredChannels[currentIndex + 1]);
            }
        }

        public void Dispose()
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _databaseService.Dispose();
        }
    }
}
