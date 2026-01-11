using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using StreamVision.Models;

namespace StreamVision.Views
{
    public partial class RecommendationsView : UserControl
    {
        public event EventHandler<RecommendationItem>? ChannelSelected;

        public RecommendationsView()
        {
            InitializeComponent();
        }

        public void SetLoading(bool isLoading)
        {
            LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            ContentPanel.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
            EmptyPanel.Visibility = Visibility.Collapsed;
        }

        public void SetSections(List<RecommendationSection> sections)
        {
            if (sections == null || sections.Count == 0)
            {
                EmptyPanel.Visibility = Visibility.Visible;
                ContentPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyPanel.Visibility = Visibility.Collapsed;
                ContentPanel.Visibility = Visibility.Visible;
                SectionsItemsControl.ItemsSource = sections;
            }
        }

        public void SetUserStats(UserStats? stats)
        {
            if (stats == null) return;

            WatchTimeText.Text = FormatWatchTime(stats.TotalWatchTimeMinutes);
            ChannelsText.Text = stats.TotalChannelsWatched.ToString();
            FavoriteCategoryText.Text = stats.FavoriteCategory;
            SessionsText.Text = stats.WatchSessionCount.ToString();
        }

        private string FormatWatchTime(int minutes)
        {
            if (minutes < 60)
                return $"{minutes}m";
            else if (minutes < 1440)
                return $"{minutes / 60}h {minutes % 60}m";
            else
                return $"{minutes / 1440}d {(minutes % 1440) / 60}h";
        }

        private void OnRecommendationClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is RecommendationItem item)
            {
                ChannelSelected?.Invoke(this, item);
            }
        }
    }

    // Value Converters
    public class RecommendationTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RecommendationType type)
            {
                return type switch
                {
                    RecommendationType.ContinueWatching => "\uE768",    // Play
                    RecommendationType.BecauseYouWatched => "\uE81C",   // History
                    RecommendationType.TopPicksForYou => "\uE735",     // Star
                    RecommendationType.TrendingNow => "\uE8B1",        // Trending
                    RecommendationType.NewReleases => "\uE8C8",        // Sparkle
                    RecommendationType.CategoryRecommendation => "\uE80A", // Grid
                    RecommendationType.HiddenGems => "\uE7C1",         // Diamond
                    RecommendationType.SimilarContent => "\uE8AB",     // Compare
                    RecommendationType.TimeBasedPicks => "\uE121",     // Clock
                    _ => "\uE7F4"                                      // TV
                };
            }
            return "\uE7F4";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RecommendationTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RecommendationType type)
            {
                var color = type switch
                {
                    RecommendationType.ContinueWatching => Color.FromRgb(76, 175, 80),
                    RecommendationType.BecauseYouWatched => Color.FromRgb(33, 150, 243),
                    RecommendationType.TopPicksForYou => Color.FromRgb(255, 152, 0),
                    RecommendationType.TrendingNow => Color.FromRgb(244, 67, 54),
                    RecommendationType.NewReleases => Color.FromRgb(156, 39, 176),
                    RecommendationType.CategoryRecommendation => Color.FromRgb(0, 188, 212),
                    RecommendationType.HiddenGems => Color.FromRgb(233, 30, 99),
                    RecommendationType.SimilarContent => Color.FromRgb(63, 81, 181),
                    RecommendationType.TimeBasedPicks => Color.FromRgb(96, 125, 139),
                    _ => Color.FromRgb(100, 100, 100)
                };
                return new SolidColorBrush(color);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ScoreToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score && score > 0.8)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PercentageToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int percentage && parameter is string maxWidthStr)
            {
                if (double.TryParse(maxWidthStr, out double maxWidth))
                {
                    return maxWidth * percentage / 100.0;
                }
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MinutesToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int minutes)
            {
                if (minutes < 60)
                    return $"{minutes}m";
                else if (minutes < 1440)
                    return $"{minutes / 60}h {minutes % 60}m";
                else
                    return $"{minutes / 1440}d {(minutes % 1440) / 60}h";
            }
            return "0m";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ScoreToDotsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dots = new List<SolidColorBrush>();
            var accentColor = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            var grayColor = new SolidColorBrush(Color.FromRgb(200, 200, 200));

            if (value is double score)
            {
                int filledDots = (int)(score * 5);
                for (int i = 0; i < 5; i++)
                {
                    dots.Add(i < filledDots ? accentColor : grayColor);
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    dots.Add(grayColor);
                }
            }

            return dots;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
