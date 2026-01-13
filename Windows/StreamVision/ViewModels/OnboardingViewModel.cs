using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVision.Models;
using StreamVision.Services;

namespace StreamVision.ViewModels
{
    public partial class OnboardingViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly M3UParser _m3uParser;
        private readonly XtreamCodesService _xtreamService;

        // Onboarding steps
        public enum OnboardingStep
        {
            Welcome = 0,
            CreateProfile = 1,
            PlaylistSetup = 2,
            Preferences = 3,
            Loading = 4,
            Complete = 5
        }

        [ObservableProperty]
        private OnboardingStep _currentStep = OnboardingStep.Welcome;

        [ObservableProperty]
        private int _currentStepIndex = 0;

        [ObservableProperty]
        private int _totalSteps = 5;

        // Profile data
        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        // Playlist data
        [ObservableProperty]
        private PlaylistType _selectedPlaylistType = PlaylistType.M3U;

        [ObservableProperty]
        private string _playlistUrl = string.Empty;

        [ObservableProperty]
        private string _xtreamServer = string.Empty;

        [ObservableProperty]
        private string _xtreamUsername = string.Empty;

        [ObservableProperty]
        private string _xtreamPassword = string.Empty;

        // Preferences
        [ObservableProperty]
        private ObservableCollection<SelectableItem> _languages = new();

        [ObservableProperty]
        private ObservableCollection<SelectableItem> _genres = new();

        [ObservableProperty]
        private ObservableCollection<SelectableItem> _sports = new();

        // Loading state
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _loadingMessage = "Chargement...";

        [ObservableProperty]
        private int _loadingProgress;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        // Navigation
        [ObservableProperty]
        private bool _canGoBack;

        [ObservableProperty]
        private bool _canGoNext = true;

        [ObservableProperty]
        private string _nextButtonText = "Commencer";

        // Event for completion
        public event EventHandler? OnboardingCompleted;

        public OnboardingViewModel()
        {
            _databaseService = new DatabaseService();
            _m3uParser = new M3UParser();
            _xtreamService = new XtreamCodesService();

            InitializeOptions();
        }

        private void InitializeOptions()
        {
            // Initialize languages
            foreach (var lang in AvailableLanguages.All)
            {
                Languages.Add(new SelectableItem
                {
                    Id = lang.Id,
                    DisplayName = lang.DisplayName,
                    IsSelected = lang.Id == "French" // French selected by default
                });
            }

            // Initialize genres
            foreach (var genre in AvailableGenres.All)
            {
                Genres.Add(new SelectableItem
                {
                    Id = genre.Id,
                    DisplayName = genre.DisplayName
                });
            }

            // Initialize sports
            foreach (var sport in AvailableSports.All)
            {
                Sports.Add(new SelectableItem
                {
                    Id = sport.Id,
                    DisplayName = sport.DisplayName
                });
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (CurrentStepIndex > 0)
            {
                CurrentStepIndex--;
                CurrentStep = (OnboardingStep)CurrentStepIndex;
                UpdateNavigationState();
            }
        }

        [RelayCommand]
        private async Task GoNext()
        {
            ErrorMessage = string.Empty;

            // Validate current step
            if (!ValidateCurrentStep())
                return;

            if (CurrentStep == OnboardingStep.PlaylistSetup)
            {
                // Move to preferences, then start loading in background
                CurrentStepIndex++;
                CurrentStep = OnboardingStep.Preferences;
                UpdateNavigationState();
            }
            else if (CurrentStep == OnboardingStep.Preferences)
            {
                // Start loading playlist
                CurrentStepIndex++;
                CurrentStep = OnboardingStep.Loading;
                UpdateNavigationState();
                await LoadPlaylistAsync();
            }
            else if (CurrentStep == OnboardingStep.Complete)
            {
                // Finish onboarding
                await CompleteOnboardingAsync();
            }
            else
            {
                CurrentStepIndex++;
                CurrentStep = (OnboardingStep)CurrentStepIndex;
                UpdateNavigationState();
            }
        }

        private bool ValidateCurrentStep()
        {
            switch (CurrentStep)
            {
                case OnboardingStep.CreateProfile:
                    if (string.IsNullOrWhiteSpace(Username))
                    {
                        ErrorMessage = "Veuillez entrer un nom d'utilisateur";
                        return false;
                    }
                    break;

                case OnboardingStep.PlaylistSetup:
                    if (SelectedPlaylistType == PlaylistType.M3U)
                    {
                        if (string.IsNullOrWhiteSpace(PlaylistUrl))
                        {
                            ErrorMessage = "Veuillez entrer l'URL de votre playlist M3U";
                            return false;
                        }
                        if (!PlaylistUrl.StartsWith("http://") && !PlaylistUrl.StartsWith("https://"))
                        {
                            ErrorMessage = "L'URL doit commencer par http:// ou https://";
                            return false;
                        }
                    }
                    else if (SelectedPlaylistType == PlaylistType.Xtream)
                    {
                        if (string.IsNullOrWhiteSpace(XtreamServer))
                        {
                            ErrorMessage = "Veuillez entrer l'adresse du serveur";
                            return false;
                        }
                        if (string.IsNullOrWhiteSpace(XtreamUsername))
                        {
                            ErrorMessage = "Veuillez entrer votre nom d'utilisateur";
                            return false;
                        }
                        if (string.IsNullOrWhiteSpace(XtreamPassword))
                        {
                            ErrorMessage = "Veuillez entrer votre mot de passe";
                            return false;
                        }
                    }
                    break;

                case OnboardingStep.Preferences:
                    // At least one language should be selected
                    if (!Languages.Any(l => l.IsSelected))
                    {
                        ErrorMessage = "Veuillez choisir au moins une langue";
                        return false;
                    }
                    break;
            }

            return true;
        }

        private void UpdateNavigationState()
        {
            CanGoBack = CurrentStepIndex > 0 && CurrentStep != OnboardingStep.Loading && CurrentStep != OnboardingStep.Complete;

            NextButtonText = CurrentStep switch
            {
                OnboardingStep.Welcome => "Commencer",
                OnboardingStep.CreateProfile => "Suivant",
                OnboardingStep.PlaylistSetup => "Suivant",
                OnboardingStep.Preferences => "Charger ma playlist",
                OnboardingStep.Loading => "Chargement...",
                OnboardingStep.Complete => "Terminer",
                _ => "Suivant"
            };

            CanGoNext = CurrentStep != OnboardingStep.Loading;
        }

        private async Task LoadPlaylistAsync()
        {
            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                await _databaseService.InitializeAsync();

                // Save user account
                LoadingMessage = "Sauvegarde du profil...";
                LoadingProgress = 10;

                var account = new UserAccount
                {
                    Username = Username,
                    Email = Email,
                    PlaylistType = SelectedPlaylistType,
                    PlaylistUrl = SelectedPlaylistType == PlaylistType.M3U ? PlaylistUrl : null,
                    XtreamServer = SelectedPlaylistType == PlaylistType.Xtream ? XtreamServer : null,
                    XtreamUsername = SelectedPlaylistType == PlaylistType.Xtream ? XtreamUsername : null,
                    XtreamPassword = SelectedPlaylistType == PlaylistType.Xtream ? XtreamPassword : null,
                    IsConfigured = true
                };

                await _databaseService.SaveUserAccountAsync(account);

                // Save preferences
                LoadingMessage = "Sauvegarde des pr\u00e9f\u00e9rences...";
                LoadingProgress = 20;

                var prefs = new ContentPreferences
                {
                    PreferredLanguages = Languages.Where(l => l.IsSelected).Select(l => l.Id).ToList(),
                    PreferredGenres = Genres.Where(g => g.IsSelected).Select(g => g.Id).ToList(),
                    PreferredSports = Sports.Where(s => s.IsSelected).Select(s => s.Id).ToList(),
                    OnboardingCompleted = true,
                    UpdatedAt = DateTime.Now
                };

                await _databaseService.SaveUserPreferencesAsync(prefs);

                // Load playlist
                LoadingMessage = "Connexion au serveur...";
                LoadingProgress = 30;

                List<Channel> channels;

                if (SelectedPlaylistType == PlaylistType.M3U)
                {
                    LoadingMessage = "T\u00e9l\u00e9chargement de la playlist...";
                    channels = await _m3uParser.ParseFromUrlAsync(PlaylistUrl, new Progress<int>(p =>
                    {
                        LoadingProgress = 30 + (p * 50 / 100);
                        LoadingMessage = $"Analyse de la playlist... {p}%";
                    }));
                }
                else
                {
                    LoadingMessage = "Connexion \u00e0 Xtream Codes...";
                    _xtreamService.Initialize(XtreamServer, XtreamUsername, XtreamPassword);

                    var liveChannels = await _xtreamService.GetLiveStreamsAsync();
                    channels = liveChannels.Select(x => new Channel
                    {
                        Id = x.StreamId.ToString(),
                        Name = x.Name ?? "",
                        LogoUrl = x.StreamIcon,
                        StreamUrl = _xtreamService.GetLiveStreamUrl(x.StreamId),
                        GroupTitle = x.CategoryId?.ToString() ?? "Uncategorized"
                    }).ToList();

                    LoadingProgress = 80;
                }

                // Save to database
                LoadingMessage = "Sauvegarde des cha\u00eenes...";
                LoadingProgress = 85;

                var source = new PlaylistSource
                {
                    Name = SelectedPlaylistType == PlaylistType.M3U ? "Ma Playlist M3U" : "Xtream Codes",
                    Type = SelectedPlaylistType == PlaylistType.M3U ? SourceType.M3U : SourceType.XtreamCodes,
                    Url = SelectedPlaylistType == PlaylistType.M3U ? PlaylistUrl : XtreamServer,
                    Username = XtreamUsername,
                    Password = XtreamPassword,
                    IsActive = true,
                    LastSync = DateTime.Now
                };

                await _databaseService.SavePlaylistSourceAsync(source);

                foreach (var channel in channels)
                {
                    channel.SourceId = source.Id;
                }

                await _databaseService.SaveChannelsAsync(channels, new Progress<int>(p =>
                {
                    LoadingProgress = 85 + (p * 10 / channels.Count);
                }));

                LoadingProgress = 100;
                LoadingMessage = $"Termin\u00e9 ! {channels.Count} cha\u00eenes charg\u00e9es.";

                await Task.Delay(1000);

                // Move to complete step
                CurrentStep = OnboardingStep.Complete;
                CurrentStepIndex = (int)OnboardingStep.Complete;
                UpdateNavigationState();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur lors du chargement: {ex.Message}";
                // Go back to playlist setup on error
                CurrentStep = OnboardingStep.PlaylistSetup;
                CurrentStepIndex = (int)OnboardingStep.PlaylistSetup;
                UpdateNavigationState();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CompleteOnboardingAsync()
        {
            try
            {
                await _databaseService.InitializeAsync();

                // Mark onboarding as completed
                var prefs = await _databaseService.GetUserPreferencesAsync();
                if (prefs != null)
                {
                    prefs.OnboardingCompleted = true;
                    prefs.UpdatedAt = DateTime.Now;
                    await _databaseService.SaveUserPreferencesAsync(prefs);
                }

                OnboardingCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SelectPlaylistType(string type)
        {
            SelectedPlaylistType = type == "M3U" ? PlaylistType.M3U : PlaylistType.Xtream;
        }

        [RelayCommand]
        private void ToggleLanguage(SelectableItem item)
        {
            item.IsSelected = !item.IsSelected;
        }

        [RelayCommand]
        private void ToggleGenre(SelectableItem item)
        {
            item.IsSelected = !item.IsSelected;
        }

        [RelayCommand]
        private void ToggleSport(SelectableItem item)
        {
            item.IsSelected = !item.IsSelected;
        }
    }

    // Helper class for selectable items
    public partial class SelectableItem : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }
}
