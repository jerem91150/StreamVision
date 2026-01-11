using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StreamVision
{
    public static class Converters
    {
        public static IValueConverter BoolToVisibility { get; } = new BoolToVisibilityConverter();
        public static IValueConverter NullToVisibility { get; } = new NullToVisibilityConverter();
        public static IValueConverter NotNullToVisibility { get; } = new NotNullToVisibilityConverter();
        public static IValueConverter BoolToPlayPause { get; } = new BoolToPlayPauseConverter();
        public static IValueConverter BoolToVolume { get; } = new BoolToVolumeConverter();
        public static IValueConverter BoolToStar { get; } = new BoolToStarConverter();
        public static IValueConverter PercentageToWidth { get; } = new PercentageToWidthConverter();
        public static IValueConverter RatingToVisibility { get; } = new RatingToVisibilityConverter();
        public static IValueConverter ProgressToWidth { get; } = new ProgressToWidthConverter();
        public static IValueConverter InverseBoolToVisibility { get; } = new InverseBoolToVisibilityConverter();
        public static IValueConverter StringToVisibility { get; } = new StringToVisibilityConverter();
        public static IValueConverter PercentToWidth { get; } = new PercentToWidthConverter();
        public static IValueConverter HasChannelToVisibility { get; } = new HasChannelToVisibilityConverter();
        public static IValueConverter BoolToVisibilityInverse { get; } = new InverseBoolToVisibilityConverter();
        public static IValueConverter BoolToAccentBrush { get; } = new BoolToAccentBrushConverter();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v != Visibility.Visible;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToPlayPauseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "⏸" : "▶";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVolumeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "🔇" : "🔊";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "★" : "☆";
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
            if (value is double d)
            {
                return d * 1.2; // Assuming 120px width for the slider track
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RatingToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return d > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value is int i)
                return i > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProgressToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress)
            {
                // Retourne un pourcentage pour Width binding avec *
                return progress * 320; // Width of WideCard
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PercentToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                // Parameter est la largeur max, défaut à 800
                double maxWidth = 800;
                if (parameter != null && double.TryParse(parameter.ToString(), out double parsed))
                {
                    maxWidth = parsed;
                }
                return percent / 100.0 * maxWidth;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HasChannelToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text != "Cliquez pour ajouter" ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToAccentBrushConverter : IValueConverter
    {
        private static readonly System.Windows.Media.SolidColorBrush AccentBrush =
            new(System.Windows.Media.Color.FromRgb(0x60, 0xA5, 0xFA)); // AccentBlue
        private static readonly System.Windows.Media.SolidColorBrush DefaultBrush =
            new(System.Windows.Media.Color.FromRgb(0xA1, 0xA1, 0xAA)); // TextSecondary

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? AccentBrush : DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
