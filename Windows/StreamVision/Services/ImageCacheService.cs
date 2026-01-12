using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace StreamVision.Services
{
    /// <summary>
    /// Thread-safe image caching service with disk persistence.
    /// Provides instant loading for previously downloaded images and offline support.
    /// </summary>
    public class ImageCacheService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private readonly ConcurrentDictionary<string, Task<BitmapImage?>> _downloadTasks = new();
        private readonly ConcurrentDictionary<string, BitmapImage> _memoryCache = new();
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);
        private bool _isDisposed;

        private const int MaxMemoryCacheItems = 200;
        private const int MaxCacheSizeMB = 500;
        private const int CacheExpirationDays = 30;
        private const int DownloadTimeoutSeconds = 30;

        // Placeholder image for loading state
        private static BitmapImage? _placeholderImage;
        private static BitmapImage? _errorImage;

        public ImageCacheService()
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StreamVision",
                "ImageCache"
            );

            Directory.CreateDirectory(_cacheDirectory);

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(DownloadTimeoutSeconds)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("StreamVision/1.0");

            // Start background cleanup
            _ = CleanupOldCacheFilesAsync();
        }

        /// <summary>
        /// Get an image from cache or download it. Returns placeholder during download.
        /// </summary>
        public async Task<BitmapImage?> GetImageAsync(string? url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // Check memory cache first (fastest)
            if (_memoryCache.TryGetValue(url, out var cachedImage))
                return cachedImage;

            // Check disk cache
            var cacheFile = GetCacheFilePath(url);
            if (File.Exists(cacheFile))
            {
                try
                {
                    var image = await LoadFromDiskAsync(cacheFile).ConfigureAwait(false);
                    if (image != null)
                    {
                        AddToMemoryCache(url, image);
                        return image;
                    }
                }
                catch (Exception ex)
                {
                    App.LogWarning($"Failed to load cached image: {ex.Message}");
                    // Cache file might be corrupted, delete it
                    try { File.Delete(cacheFile); } catch { }
                }
            }

            // Download and cache
            return await DownloadAndCacheAsync(url, cacheFile, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Get image synchronously from cache only (no download). For UI binding.
        /// </summary>
        public BitmapImage? GetCachedImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // Memory cache
            if (_memoryCache.TryGetValue(url, out var cachedImage))
                return cachedImage;

            // Disk cache (synchronous for UI)
            var cacheFile = GetCacheFilePath(url);
            if (File.Exists(cacheFile))
            {
                try
                {
                    var image = LoadFromDiskSync(cacheFile);
                    if (image != null)
                    {
                        AddToMemoryCache(url, image);
                        return image;
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Preload multiple images in the background for smoother scrolling
        /// </summary>
        public async Task PreloadImagesAsync(IEnumerable<string?> urls, CancellationToken ct = default)
        {
            var tasks = new List<Task>();
            foreach (var url in urls.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                if (ct.IsCancellationRequested) break;
                tasks.Add(GetImageAsync(url, ct));
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.LogWarning($"Image preload error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if an image is cached (memory or disk)
        /// </summary>
        public bool IsCached(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (_memoryCache.ContainsKey(url))
                return true;

            var cacheFile = GetCacheFilePath(url);
            return File.Exists(cacheFile);
        }

        /// <summary>
        /// Clear all cached images
        /// </summary>
        public async Task ClearCacheAsync()
        {
            _memoryCache.Clear();
            _downloadTasks.Clear();

            await _cleanupLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    foreach (var file in Directory.GetFiles(_cacheDirectory, "*.jpg"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                App.LogInfo("Image cache cleared");
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public (int fileCount, long totalSizeMB, int memoryCacheCount) GetCacheStats()
        {
            var files = Directory.Exists(_cacheDirectory)
                ? Directory.GetFiles(_cacheDirectory, "*.jpg")
                : Array.Empty<string>();

            long totalSize = 0;
            foreach (var file in files)
            {
                try { totalSize += new FileInfo(file).Length; } catch { }
            }

            return (files.Length, totalSize / (1024 * 1024), _memoryCache.Count);
        }

        private async Task<BitmapImage?> DownloadAndCacheAsync(string url, string cacheFile, CancellationToken ct)
        {
            // Use GetOrAdd to prevent duplicate downloads
            var downloadTask = _downloadTasks.GetOrAdd(url, _ => DownloadImageInternalAsync(url, cacheFile, ct));

            try
            {
                return await downloadTask.ConfigureAwait(false);
            }
            finally
            {
                _downloadTasks.TryRemove(url, out _);
            }
        }

        private async Task<BitmapImage?> DownloadImageInternalAsync(string url, string cacheFile, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    App.LogWarning($"Image download failed ({response.StatusCode}): {url}");
                    return null;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    App.LogWarning($"Invalid content type for image: {contentType}");
                    return null;
                }

                // Download to temp file first, then move (atomic)
                var tempFile = cacheFile + ".tmp";
                await using (var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    await responseStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
                }

                // Atomic move
                File.Move(tempFile, cacheFile, overwrite: true);

                // Load and return
                var image = await LoadFromDiskAsync(cacheFile).ConfigureAwait(false);
                if (image != null)
                {
                    AddToMemoryCache(url, image);
                }
                return image;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                App.LogWarning($"Image download error: {ex.Message}");
                return null;
            }
        }

        private async Task<BitmapImage?> LoadFromDiskAsync(string filePath)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                return LoadFromDiskSync(filePath);
            });
        }

        private BitmapImage? LoadFromDiskSync(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze(); // Make thread-safe
                return bitmap;
            }
            catch (Exception ex)
            {
                App.LogWarning($"Failed to load image from disk: {ex.Message}");
                return null;
            }
        }

        private string GetCacheFilePath(string url)
        {
            // Create a hash of the URL for the filename
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            var hash = Convert.ToHexString(hashBytes)[..32]; // First 32 chars
            return Path.Combine(_cacheDirectory, $"{hash}.jpg");
        }

        private void AddToMemoryCache(string url, BitmapImage image)
        {
            // Evict old entries if cache is too large
            if (_memoryCache.Count >= MaxMemoryCacheItems)
            {
                // Remove ~20% of entries (simple FIFO-ish eviction)
                var keysToRemove = _memoryCache.Keys.Take(_memoryCache.Count / 5).ToList();
                foreach (var key in keysToRemove)
                {
                    _memoryCache.TryRemove(key, out _);
                }
            }

            _memoryCache.TryAdd(url, image);
        }

        private async Task CleanupOldCacheFilesAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(30)).ConfigureAwait(false); // Delay startup

            await _cleanupLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                    return;

                var files = Directory.GetFiles(_cacheDirectory, "*.jpg");
                var now = DateTime.Now;
                var deletedCount = 0;
                long totalSize = 0;

                // Get all file info
                var fileInfos = files
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastAccessTime)
                    .ToList();

                foreach (var fileInfo in fileInfos)
                {
                    totalSize += fileInfo.Length;

                    // Delete if too old
                    if ((now - fileInfo.LastAccessTime).TotalDays > CacheExpirationDays)
                    {
                        try
                        {
                            fileInfo.Delete();
                            deletedCount++;
                            totalSize -= fileInfo.Length;
                        }
                        catch { }
                    }
                    // Delete oldest files if cache too large
                    else if (totalSize > MaxCacheSizeMB * 1024 * 1024)
                    {
                        try
                        {
                            fileInfo.Delete();
                            deletedCount++;
                            totalSize -= fileInfo.Length;
                        }
                        catch { }
                    }
                }

                if (deletedCount > 0)
                {
                    App.LogInfo($"Image cache cleanup: deleted {deletedCount} files");
                }
            }
            catch (Exception ex)
            {
                App.LogWarning($"Cache cleanup error: {ex.Message}");
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        /// <summary>
        /// Get a placeholder image for loading state
        /// </summary>
        public static BitmapImage? GetPlaceholder()
        {
            if (_placeholderImage != null)
                return _placeholderImage;

            try
            {
                // Create a simple gray placeholder
                _placeholderImage = CreateSolidColorImage(128, 128, 0xFF2D2D2D);
                return _placeholderImage;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get an error image for failed loads
        /// </summary>
        public static BitmapImage? GetErrorImage()
        {
            if (_errorImage != null)
                return _errorImage;

            try
            {
                _errorImage = CreateSolidColorImage(128, 128, 0xFF1A1A1A);
                return _errorImage;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage CreateSolidColorImage(int width, int height, uint color)
        {
            // Create a simple solid color image using WriteableBitmap
            var writeableBitmap = new System.Windows.Media.Imaging.WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);

            var pixels = new uint[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            writeableBitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);

            // Convert to BitmapImage via stream
            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
            encoder.Save(stream);
            stream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _httpClient.Dispose();
                _cleanupLock.Dispose();
                _memoryCache.Clear();
                _downloadTasks.Clear();
            }

            _isDisposed = true;
        }
    }
}
