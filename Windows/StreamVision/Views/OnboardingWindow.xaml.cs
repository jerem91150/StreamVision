using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StreamVision.Models;

namespace StreamVision.Views
{
    public partial class OnboardingWindow : Window
    {
        private int _currentStep = 1;
        private readonly ContentPreferences _preferences = new();
        private readonly HashSet<string> _selectedLanguages = new() { "French" };
        private readonly HashSet<string> _selectedContentTypes = new() { "Movies", "Series", "Live" };
        private bool _showAnime = true;
        private string _animeVersionPreference = "VOSTFR"; // VOSTFR, VF, or Both

        public ContentPreferences Preferences => _preferences;

        public OnboardingWindow()
        {
            InitializeComponent();
            UpdateLanguageCards();
            UpdateContentTypeCards();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Default preferences if closing early
            _preferences.OnboardingCompleted = true;
            DialogResult = true;
            Close();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 5)
            {
                _currentStep++;
                UpdateStepVisibility();
                UpdateStepDots();

                if (_currentStep == 5)
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

        private void Language_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string language)
            {
                if (_selectedLanguages.Contains(language))
                {
                    // Don't allow deselecting all languages
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
                    // Don't allow deselecting all content types
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

                // Update text colors for version cards
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
                // Update icon color
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
        }

        private void UpdateSummary()
        {
            // Build language summary
            var langNames = new Dictionary<string, string>
            {
                { "French", "Français" },
                { "English", "English" },
                { "Spanish", "Español" },
                { "German", "Deutsch" },
                { "Italian", "Italiano" },
                { "Portuguese", "Português" },
                { "Arabic", "العربية" },
                { "Turkish", "Türkçe" }
            };

            var selectedLangNames = _selectedLanguages.Select(l => langNames.GetValueOrDefault(l, l));
            LangSummary.Text = $"Langues : {string.Join(", ", selectedLangNames)}";

            // Build content type summary
            var contentNames = new List<string>();
            if (_selectedContentTypes.Contains("Movies")) contentNames.Add("Films");
            if (_selectedContentTypes.Contains("Series")) contentNames.Add("Séries");
            if (_selectedContentTypes.Contains("Live")) contentNames.Add("TV en direct");
            ContentSummary.Text = $"Contenu : {string.Join(", ", contentNames)}";

            // Build anime summary
            if (_showAnime)
            {
                AnimeSummaryPanel.Visibility = Visibility.Visible;
                var versionText = _animeVersionPreference switch
                {
                    "VOSTFR" => "VOSTFR préféré",
                    "VF" => "VF préféré",
                    "Both" => "VOSTFR et VF",
                    _ => "Activé"
                };
                AnimeSummary.Text = $"Animés : {versionText}";
            }
            else
            {
                AnimeSummaryPanel.Visibility = Visibility.Collapsed;
            }

            // Update summary text
            if (_selectedLanguages.Count == 1 && _selectedLanguages.Contains("French"))
            {
                SummaryText.Text = "Vous ne verrez que du contenu en français";
            }
            else if (_selectedLanguages.Count > 1)
            {
                SummaryText.Text = $"Contenu en {_selectedLanguages.Count} langues";
            }
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            // Save preferences
            _preferences.PreferredLanguages = _selectedLanguages.ToList();
            _preferences.ShowMovies = _selectedContentTypes.Contains("Movies");
            _preferences.ShowSeries = _selectedContentTypes.Contains("Series");
            _preferences.ShowLiveTV = _selectedContentTypes.Contains("Live");

            // Anime preferences
            _preferences.ShowAnime = _showAnime;
            _preferences.AnimePreferSubbed = _animeVersionPreference == "VOSTFR" || _animeVersionPreference == "Both";
            _preferences.AnimePreferDubbed = _animeVersionPreference == "VF" || _animeVersionPreference == "Both";

            _preferences.OnboardingCompleted = true;
            _preferences.UpdatedAt = DateTime.Now;

            DialogResult = true;
            Close();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // Allow window dragging
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
