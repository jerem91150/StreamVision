using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamVision.Models;
using StreamVision.Services;
using StreamVision.ViewModels;

namespace StreamVision.Views
{
    public partial class MediaDetailWindow : Window
    {
        private readonly MediaItem _item;
        private readonly HomeViewModel _viewModel;
        private readonly ContentAnalyzerService _contentAnalyzer;
        private readonly DatabaseService _databaseService;
        private readonly RecommendationEngine _recommendationEngine;
        private int _userRating = 0;
        private string _qualityRating = "";

        public MediaDetailWindow(MediaItem item, HomeViewModel viewModel)
        {
            InitializeComponent();
            _item = item;
            _viewModel = viewModel;
            _contentAnalyzer = new ContentAnalyzerService();
            _databaseService = new DatabaseService();
            _recommendationEngine = new RecommendationEngine(_databaseService);

            LoadContent();
            LoadUserRating();
            LoadSimilarContentAsync();
        }

        private void LoadContent()
        {
            // Load images
            if (!string.IsNullOrEmpty(_item.BackdropUrl))
            {
                try
                {
                    BackdropBrush.ImageSource = new BitmapImage(new Uri(_item.BackdropUrl));
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(_item.PosterUrl))
            {
                try
                {
                    PosterBrush.ImageSource = new BitmapImage(new Uri(_item.PosterUrl));
                }
                catch { }
            }
            else if (!string.IsNullOrEmpty(_item.LogoUrl))
            {
                try
                {
                    PosterBrush.ImageSource = new BitmapImage(new Uri(_item.LogoUrl));
                }
                catch { }
            }

            // Analyze content for versions/languages
            var analysis = _contentAnalyzer.Analyze(_item);

            // Add version badges
            foreach (var version in analysis.DetectedVersions.Where(v => v.Language != "Unknown"))
            {
                var badge = CreateVersionBadge(version.Tag, version.DisplayName);
                VersionBadgesPanel.Children.Add(badge);
            }

            // If anime, add anime badge
            if (analysis.IsAnime)
            {
                var animeBadge = CreateBadge("🎌 Anime", "#7C3AED");
                VersionBadgesPanel.Children.Insert(0, animeBadge);
            }

            // Title
            TitleText.Text = analysis.CleanName;

            // Original title if different
            if (analysis.CleanName != _item.Name)
            {
                OriginalTitleText.Text = _item.Name;
                OriginalTitleText.Visibility = Visibility.Visible;
            }

            // Year
            YearText.Text = _item.ReleaseDate?.Year.ToString() ?? DateTime.Now.Year.ToString();

            // Duration
            if (!string.IsNullOrEmpty(_item.RuntimeDisplay))
            {
                DurationText.Text = _item.RuntimeDisplay;
                DurationBadge.Visibility = Visibility.Visible;
            }

            // TMDb Rating
            if (_item.Rating > 0)
            {
                TmdbRatingText.Text = _item.Rating.ToString("F1");
                TmdbRatingBadge.Visibility = Visibility.Visible;
            }

            // Category
            CategoryText.Text = analysis.DetectedCategory;

            // Genres
            if (!string.IsNullOrEmpty(_item.Genres))
            {
                var genres = _item.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var genre in genres.Take(5))
                {
                    var genreBadge = CreateBadge(genre.Trim(), "#374151");
                    GenresPanel.Children.Add(genreBadge);
                }
            }

            // Favorite state
            UpdateFavoriteButton();

            // Trailer
            if (!string.IsNullOrEmpty(_item.TrailerUrl))
            {
                TrailerButton.Visibility = Visibility.Visible;
            }

            // Overview
            OverviewText.Text = !string.IsNullOrEmpty(_item.Overview)
                ? _item.Overview
                : "Aucun synopsis disponible pour ce contenu.";

            // Audio languages (detected from name/group)
            PopulateAudioLanguages(analysis);

            // Subtitles (detected from name/group)
            PopulateSubtitles(analysis);

            // Cast
            if (!string.IsNullOrEmpty(_item.Cast))
            {
                CastText.Text = _item.Cast;
                CastSection.Visibility = Visibility.Visible;
            }

            // Director
            if (!string.IsNullOrEmpty(_item.Director))
            {
                DirectorText.Text = _item.Director;
                DirectorText.Visibility = Visibility.Visible;
                DirectorLabel.Visibility = Visibility.Visible;
            }

            // Technical info
            MediaTypeText.Text = _item.MediaType switch
            {
                ContentType.Movie => "Film",
                ContentType.Series => "Série",
                ContentType.Live => "TV en direct",
                _ => "Inconnu"
            };

            SourceText.Text = !string.IsNullOrEmpty(_item.SourceId) ? "IPTV" : "Local";
            GroupText.Text = _item.GroupTitle ?? "Non catégorisé";

            // Detected version
            var mainVersion = analysis.DetectedVersions.FirstOrDefault(v => v.Language != "Unknown");
            DetectedVersionText.Text = mainVersion != null
                ? $"{mainVersion.Tag} ({mainVersion.DisplayName})"
                : "Non déterminé";
        }

        private void PopulateAudioLanguages(ContentAnalysis analysis)
        {
            // Add detected audio languages
            foreach (var version in analysis.DetectedVersions.Where(v => v.Language != "Unknown"))
            {
                string audioLabel;
                if (version.Version == ContentVersion.Dubbed)
                {
                    audioLabel = GetLanguageFlag(version.Language) + " " + GetLocalizedLanguageName(version.Language);
                }
                else if (version.Version == ContentVersion.SubbedOriginal)
                {
                    audioLabel = "🇯🇵 Japonais"; // For anime VOSTFR
                }
                else if (version.Version == ContentVersion.Multi)
                {
                    audioLabel = "🌐 Multi-audio";
                }
                else
                {
                    continue;
                }

                var badge = CreateLanguageBadge(audioLabel);
                AudioLanguagesPanel.Children.Add(badge);
            }

            // If no audio detected, add default
            if (AudioLanguagesPanel.Children.Count == 0)
            {
                var badge = CreateLanguageBadge("🔊 Audio disponible");
                AudioLanguagesPanel.Children.Add(badge);
            }
        }

        private void PopulateSubtitles(ContentAnalysis analysis)
        {
            // Add detected subtitles
            foreach (var version in analysis.DetectedVersions.Where(v => v.Language != "Unknown"))
            {
                if (version.Version == ContentVersion.SubbedOriginal)
                {
                    var label = GetLanguageFlag(version.Language) + " " + GetLocalizedLanguageName(version.Language);
                    var badge = CreateLanguageBadge(label);
                    SubtitlesPanel.Children.Add(badge);
                }
            }

            // If MULTI, add multi subtitle indication
            if (analysis.DetectedVersions.Any(v => v.Version == ContentVersion.Multi))
            {
                var badge = CreateLanguageBadge("🌐 Multi-sous-titres");
                SubtitlesPanel.Children.Add(badge);
            }

            // If no subtitles detected
            if (SubtitlesPanel.Children.Count == 0)
            {
                var badge = CreateLanguageBadge("Aucun détecté");
                badge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A")!);
                SubtitlesPanel.Children.Add(badge);
            }
        }

        private string GetLanguageFlag(string language)
        {
            return language switch
            {
                "French" => "🇫🇷",
                "English" => "🇬🇧",
                "Spanish" => "🇪🇸",
                "German" => "🇩🇪",
                "Italian" => "🇮🇹",
                "Portuguese" => "🇵🇹",
                "Arabic" => "🇸🇦",
                "Turkish" => "🇹🇷",
                _ => "🌐"
            };
        }

        private string GetLocalizedLanguageName(string language)
        {
            return language switch
            {
                "French" => "Français",
                "English" => "Anglais",
                "Spanish" => "Espagnol",
                "German" => "Allemand",
                "Italian" => "Italien",
                "Portuguese" => "Portugais",
                "Arabic" => "Arabe",
                "Turkish" => "Turc",
                _ => language
            };
        }

        private Border CreateVersionBadge(string tag, string displayName)
        {
            var color = tag switch
            {
                "VOSTFR" or "VOST" or "SUBBED" or "SUB" => "#7C3AED", // Purple for subbed
                "VF" or "VFF" or "TRUEFRENCH" or "DUBBED" or "DUB" => "#2563EB", // Blue for dubbed
                "MULTI" => "#059669", // Green for multi
                _ => "#60A5FA"
            };

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 10, 0)
            };

            var text = new TextBlock
            {
                Text = tag,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            border.Child = text;
            return border;
        }

        private Border CreateBadge(string text, string color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = Brushes.White
            };

            border.Child = textBlock;
            return border;
        }

        private Border CreateLanguageBadge(string text)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F")!),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA")!)
            };

            border.Child = textBlock;
            return border;
        }

        private async void LoadUserRating()
        {
            try
            {
                await _databaseService.InitializeAsync();
                var rating = await _databaseService.GetUserRatingAsync(_item.Id);
                if (rating != null)
                {
                    _userRating = rating.StarRating;
                    _qualityRating = rating.QualityRating;
                    UpdateStarDisplay();
                    UpdateQualityButtons();
                }
            }
            catch { }
        }

        private void Star_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagStr && int.TryParse(tagStr, out int rating))
            {
                _userRating = rating;
                UpdateStarDisplay();
                SaveRating();
            }
        }

        private void UpdateStarDisplay()
        {
            Button[] stars = { Star1, Star2, Star3, Star4, Star5 };

            for (int i = 0; i < stars.Length; i++)
            {
                var star = stars[i];
                var textBlock = GetStarTextBlock(star);
                if (textBlock != null)
                {
                    if (i < _userRating)
                    {
                        textBlock.Text = "★";
                        textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24")!);
                    }
                    else
                    {
                        textBlock.Text = "☆";
                        textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")!);
                    }
                }
            }

            // Update rating text
            RatingText.Text = _userRating switch
            {
                0 => "Cliquez pour noter",
                1 => "Mauvais",
                2 => "Passable",
                3 => "Bien",
                4 => "Très bien",
                5 => "Excellent",
                _ => "Noté"
            };
        }

        private TextBlock? GetStarTextBlock(Button button)
        {
            // Navigate through the button's template to find the TextBlock
            if (VisualTreeHelper.GetChildrenCount(button) > 0)
            {
                var child = VisualTreeHelper.GetChild(button, 0);
                if (child is TextBlock tb) return tb;
                if (VisualTreeHelper.GetChildrenCount(child) > 0)
                {
                    var innerChild = VisualTreeHelper.GetChild(child, 0);
                    if (innerChild is TextBlock innerTb) return innerTb;
                }
            }
            return null;
        }

        private void Quality_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string quality)
            {
                _qualityRating = quality;
                UpdateQualityButtons();
                SaveRating();
            }
        }

        private void UpdateQualityButtons()
        {
            // Reset both buttons
            QualityBad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A")!);
            QualityGood.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A")!);

            // Highlight selected
            if (_qualityRating == "bad")
            {
                QualityBad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")!);
            }
            else if (_qualityRating == "good")
            {
                QualityGood.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")!);
            }
        }

        private async void SaveRating()
        {
            try
            {
                var rating = new UserRating
                {
                    MediaId = _item.Id,
                    StarRating = _userRating,
                    QualityRating = _qualityRating,
                    RatedAt = DateTime.Now
                };
                await _databaseService.SaveUserRatingAsync(rating);
            }
            catch { }
        }

        private void UpdateFavoriteButton()
        {
            if (_item.IsFavorite)
            {
                FavoriteIcon.Text = "♥";
                FavoriteIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")!);
                FavoriteText.Text = "Retirer";
            }
            else
            {
                FavoriteIcon.Text = "♡";
                FavoriteIcon.Foreground = Brushes.White;
                FavoriteText.Text = "Favoris";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PlayItemCommand.Execute(_item);
            Close();
        }

        private void AddToList_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleFavoriteCommand.Execute(_item);
            _item.IsFavorite = !_item.IsFavorite;
            UpdateFavoriteButton();
        }

        private void Trailer_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_item.TrailerUrl))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _item.TrailerUrl,
                    UseShellExecute = true
                });
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

        /// <summary>
        /// Load similar content based on genres, director, cast, and rating
        /// </summary>
        private async void LoadSimilarContentAsync()
        {
            try
            {
                // Get all content from view model
                var allContent = new List<MediaItem>();
                allContent.AddRange(_viewModel.Movies);
                allContent.AddRange(_viewModel.Series);

                // Find similar content using the recommendation engine
                var similarItems = _recommendationEngine.GetSimilarContent(_item, allContent, 10);

                if (similarItems.Any())
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        SimilarContentList.ItemsSource = similarItems;
                        SimilarContentSection.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading similar content: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle click on similar content item
        /// </summary>
        private void SimilarItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is MediaItem item)
            {
                // Close current window and open new detail window for the selected item
                var detailWindow = new MediaDetailWindow(item, _viewModel)
                {
                    Owner = this.Owner
                };
                Close();
                detailWindow.ShowDialog();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Dispose services
            try { _databaseService?.Dispose(); } catch { }
            base.OnClosed(e);
        }
    }
}
