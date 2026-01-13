using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StreamVision.Models;
using StreamVision.Services;

namespace StreamVision.Views
{
    public partial class OnboardingWindow : Window
    {
        private int _currentStep = 1;
        private const int TotalSteps = 7;

        private readonly ContentPreferences _preferences = new();
        private readonly UserAccount _userAccount = new();
        private readonly DatabaseService _databaseService = new();

        private readonly HashSet<string> _selectedLanguages = new() { "French" };
        private readonly HashSet<string> _selectedContentTypes = new() { "Movies", "Series", "Live" };
        private bool _showAnime = true;
        private string _animeVersionPreference = "VOSTFR";
        private PlaylistType _selectedPlaylistType = PlaylistType.M3U;

        public ContentPreferences Preferences => _preferences;
        public UserAccount UserAccount => _userAccount;

        public OnboardingWindow()
        {
            InitializeComponent();
            UpdateLanguageCards();
            UpdateContentTypeCards();
            UpdatePlaylistTypeCards();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _preferences.OnboardingCompleted = true;
            DialogResult = true;
            Close();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            // Validation for step 2 (profile)
            if (_currentStep == 2)
            {
                if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                {
                    MessageBox.Show("Veuillez entrer un nom d'utilisateur", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _userAccount.Username = UsernameTextBox.Text.Trim();
                _userAccount.Email = EmailTextBox.Text.Trim();
            }

            if (_currentStep < TotalSteps)
            {
                _currentStep++;
                UpdateStepVisibility();
                UpdateStepDots();

                if (_currentStep == TotalSteps)
                {
                    UpdateSummary();
                }
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepVisibility();
                UpdateStepDots();
            }
        }

        private void PlaylistType_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string type)
            {
                _selectedPlaylistType = type == "M3U" ? PlaylistType.M3U : PlaylistType.Xtream;
                UpdatePlaylistTypeCards();
            }
        }

        private void UpdatePlaylistTypeCards()
        {
            UpdateCard(M3UTypeCard, _selectedPlaylistType == PlaylistType.M3U);
            UpdateCard(XtreamTypeCard, _selectedPlaylistType == PlaylistType.Xtream);

            // Update text colors
            var m3uTextBlock = (M3UTypeCard.Child as StackPanel)?.Children[0] as TextBlock;
            var xtreamTextBlock = (XtreamTypeCard.Child as StackPanel)?.Children[0] as TextBlock;

            if (m3uTextBlock != null)
                m3uTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    _selectedPlaylistType == PlaylistType.M3U ? "#60A5FA" : "#A1A1AA"));

            if (xtreamTextBlock != null)
                xtreamTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    _selectedPlaylistType == PlaylistType.Xtream ? "#60A5FA" : "#A1A1AA"));

            // Show/hide fields
            M3UFieldsPanel.Visibility = _selectedPlaylistType == PlaylistType.M3U ? Visibility.Visible : Visibility.Collapsed;
            XtreamFieldsPanel.Visibility = _selectedPlaylistType == PlaylistType.Xtream ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void ValidatePlaylist_Click(object sender, RoutedEventArgs e)
        {
            PlaylistErrorText.Text = "";

            if (_selectedPlaylistType == PlaylistType.M3U)
            {
                var url = PlaylistUrlTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    PlaylistErrorText.Text = "Veuillez entrer l'URL de votre playlist";
                    return;
                }
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    PlaylistErrorText.Text = "L'URL doit commencer par http:// ou https://";
                    return;
                }

                _userAccount.PlaylistType = PlaylistType.M3U;
                _userAccount.PlaylistUrl = url;
            }
            else
            {
                var server = XtreamServerTextBox.Text.Trim();
                var username = XtreamUsernameTextBox.Text.Trim();
                var password = XtreamPasswordBox.Password;

                if (string.IsNullOrWhiteSpace(server))
                {
                    PlaylistErrorText.Text = "Veuillez entrer l'adresse du serveur";
                    return;
                }
                if (string.IsNullOrWhiteSpace(username))
                {
                    PlaylistErrorText.Text = "Veuillez entrer votre nom d'utilisateur";
                    return;
                }
                if (string.IsNullOrWhiteSpace(password))
                {
                    PlaylistErrorText.Text = "Veuillez entrer votre mot de passe";
                    return;
                }

                _userAccount.PlaylistType = PlaylistType.Xtream;
                _userAccount.XtreamServer = server;
                _userAccount.XtreamUsername = username;
                _userAccount.XtreamPassword = password;
            }

            _userAccount.IsConfigured = true;

            // Move to next step
            _currentStep++;
            UpdateStepVisibility();
            UpdateStepDots();
        }

        private void Language_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string language)
            {
                if (_selectedLanguages.Contains(language))
                {
                    if (_selectedLanguages.Count > 1)
                    {
                        _selectedLanguages.Remove(language);
                    }
                }
                else
                {
                    _selectedLanguages.Add(language);
                }

                UpdateLanguageCards();
            }
        }

        private void ContentType_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string contentType)
            {
                if (_selectedContentTypes.Contains(contentType))
                {
                    if (_selectedContentTypes.Count > 1)
                    {
                        _selectedContentTypes.Remove(contentType);
                    }
                }
                else
                {
                    _selectedContentTypes.Add(contentType);
                }

                UpdateContentTypeCards();
            }
        }

        private void UpdateLanguageCards()
        {
            UpdateCard(LangFR, _selectedLanguages.Contains("French"));
            UpdateCard(LangEN, _selectedLanguages.Contains("English"));
            UpdateCard(LangES, _selectedLanguages.Contains("Spanish"));
            UpdateCard(LangDE, _selectedLanguages.Contains("German"));
            UpdateCard(LangIT, _selectedLanguages.Contains("Italian"));
            UpdateCard(LangPT, _selectedLanguages.Contains("Portuguese"));
            UpdateCard(LangAR, _selectedLanguages.Contains("Arabic"));
            UpdateCard(LangTR, _selectedLanguages.Contains("Turkish"));
        }

        private void UpdateContentTypeCards()
        {
            UpdateCard(TypeMovies, _selectedContentTypes.Contains("Movies"));
            UpdateCard(TypeSeries, _selectedContentTypes.Contains("Series"));
            UpdateCard(TypeLive, _selectedContentTypes.Contains("Live"));
        }

        private void AnimeToggle_Click(object sender, MouseButtonEventArgs e)
        {
            _showAnime = !_showAnime;
            UpdateAnimeCards();
        }

        private void AnimeVersion_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string version)
            {
                _animeVersionPreference = version;
                UpdateAnimeCards();
            }
        }

        private void SkipAnime_Click(object sender, RoutedEventArgs e)
        {
            _showAnime = false;
            _currentStep++;
            UpdateStepVisibility();
            UpdateStepDots();
            UpdateSummary();
        }

        private void UpdateAnimeCards()
        {
            UpdateCard(AnimeToggle, _showAnime);
            AnimeVersionPanel.Visibility = _showAnime ? Visibility.Visible : Visibility.Collapsed;

            if (_showAnime)
            {
                UpdateCard(AnimeVOSTFR, _animeVersionPreference == "VOSTFR");
                UpdateCard(AnimeVF, _animeVersionPreference == "VF");
                UpdateCard(AnimeBoth, _animeVersionPreference == "Both");

                UpdateAnimeVersionCardColors(AnimeVOSTFR, _animeVersionPreference == "VOSTFR");
                UpdateAnimeVersionCardColors(AnimeVF, _animeVersionPreference == "VF");
                UpdateAnimeVersionCardColors(AnimeBoth, _animeVersionPreference == "Both");
            }
        }

        private void UpdateAnimeVersionCardColors(Border card, bool isSelected)
        {
            var stackPanel = card.Child as StackPanel;
            if (stackPanel?.Children[0] is TextBlock titleBlock)
            {
                titleBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    isSelected ? "#60A5FA" : "#A1A1AA"));
            }
        }

        private void UpdateCard(Border card, bool isSelected)
        {
            if (isSelected)
            {
                card.Style = (Style)FindResource("SelectedCard");
                var stackPanel = card.Child as StackPanel;
                if (stackPanel?.Children[0] is TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"));
                }
                else if (stackPanel?.Children[0] is Border iconBorder)
                {
                    iconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F"));
                }
            }
            else
            {
                card.Style = (Style)FindResource("SelectionCard");
                var stackPanel = card.Child as StackPanel;
                if (stackPanel?.Children[0] is TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
                }
                else if (stackPanel?.Children[0] is Border iconBorder)
                {
                    iconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A"));
                }
            }
        }

        private void UpdateStepVisibility()
        {
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
            Step5Panel.Visibility = _currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;
            Step6Panel.Visibility = _currentStep == 6 ? Visibility.Visible : Visibility.Collapsed;
            Step7Panel.Visibility = _currentStep == 7 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStepDots()
        {
            var activeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"));
            var inactiveColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));

            Step1Dot.Fill = _currentStep >= 1 ? activeColor : inactiveColor;
            Step2Dot.Fill = _currentStep >= 2 ? activeColor : inactiveColor;
            Step3Dot.Fill = _currentStep >= 3 ? activeColor : inactiveColor;
            Step4Dot.Fill = _currentStep >= 4 ? activeColor : inactiveColor;
            Step5Dot.Fill = _currentStep >= 5 ? activeColor : inactiveColor;
            Step6Dot.Fill = _currentStep >= 6 ? activeColor : inactiveColor;
            Step7Dot.Fill = _currentStep >= 7 ? activeColor : inactiveColor;
        }

        private void UpdateSummary()
        {
            var langNames = new Dictionary<string, string>
            {
                { "French", "Francais" },
                { "English", "English" },
                { "Spanish", "Espanol" },
                { "German", "Deutsch" },
                { "Italian", "Italiano" },
                { "Portuguese", "Portugues" },
                { "Arabic", "العربية" },
                { "Turkish", "Turkce" }
            };

            var selectedLangNames = _selectedLanguages.Select(l => langNames.GetValueOrDefault(l, l));
            LangSummary.Text = $"Langues : {string.Join(", ", selectedLangNames)}";

            var contentNames = new List<string>();
            if (_selectedContentTypes.Contains("Movies")) contentNames.Add("Films");
            if (_selectedContentTypes.Contains("Series")) contentNames.Add("Series");
            if (_selectedContentTypes.Contains("Live")) contentNames.Add("TV en direct");
            ContentSummary.Text = $"Contenu : {string.Join(", ", contentNames)}";

            if (_showAnime)
            {
                AnimeSummaryPanel.Visibility = Visibility.Visible;
                var versionText = _animeVersionPreference switch
                {
                    "VOSTFR" => "VOSTFR prefere",
                    "VF" => "VF prefere",
                    "Both" => "VOSTFR et VF",
                    _ => "Active"
                };
                AnimeSummary.Text = $"Animes : {versionText}";
            }
            else
            {
                AnimeSummaryPanel.Visibility = Visibility.Collapsed;
            }

            SummaryText.Text = $"Bienvenue {_userAccount.Username} !";
        }

        private async void Finish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _databaseService.InitializeAsync();

                // Save user account
                await _databaseService.SaveUserAccountAsync(_userAccount);

                // Save preferences
                _preferences.PreferredLanguages = _selectedLanguages.ToList();
                _preferences.ShowMovies = _selectedContentTypes.Contains("Movies");
                _preferences.ShowSeries = _selectedContentTypes.Contains("Series");
                _preferences.ShowLiveTV = _selectedContentTypes.Contains("Live");
                _preferences.ShowAnime = _showAnime;
                _preferences.AnimePreferSubbed = _animeVersionPreference == "VOSTFR" || _animeVersionPreference == "Both";
                _preferences.AnimePreferDubbed = _animeVersionPreference == "VF" || _animeVersionPreference == "Both";
                _preferences.OnboardingCompleted = true;
                _preferences.UpdatedAt = DateTime.Now;

                await _databaseService.SaveUserPreferencesAsync(_preferences);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la sauvegarde: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
