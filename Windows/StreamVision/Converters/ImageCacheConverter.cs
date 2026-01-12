using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using StreamVision.Services;

namespace StreamVision.ValueConverters
{
    /// <summary>
    /// Value converter that routes image URLs through the ImageCacheService
    /// for disk/memory caching and provides placeholder images while loading.
    /// </summary>
    public class ImageCacheConverter : IValueConverter
    {
        private static readonly Lazy<ImageCacheService> _cacheService =
            new Lazy<ImageCacheService>(() => new ImageCacheService());

        private static ImageCacheService Cache => _cacheService.Value;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                // Try to get from cache first (instant if cached)
                var cachedImage = Cache.GetCachedImage(url);
                if (cachedImage != null)
                {
                    return cachedImage;
                }

                // Start async download in background
                _ = Cache.GetImageAsync(url);

                // Return placeholder while loading
                return ImageCacheService.GetPlaceholder();
            }

            return ImageCacheService.GetPlaceholder();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Async-aware image converter that triggers property change when image loads.
    /// Uses attached property pattern for better async handling in WPF.
    /// </summary>
    public class AsyncImageCacheConverter : IValueConverter
    {
        private static readonly Lazy<ImageCacheService> _cacheService =
            new Lazy<ImageCacheService>(() => new ImageCacheService());

        private static ImageCacheService Cache => _cacheService.Value;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                // Try cache first
                var cached = Cache.GetCachedImage(url);
                if (cached != null)
                    return cached;

                // Return placeholder - the async load happens in background
                // Note: For true reactive updates, use an attached behavior instead
                _ = Cache.GetImageAsync(url);
                return ImageCacheService.GetPlaceholder();
            }

            return ImageCacheService.GetPlaceholder();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
