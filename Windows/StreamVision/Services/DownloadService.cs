using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Service for downloading VOD content for offline viewing
    /// </summary>
    public class DownloadService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _downloadPath;
        private readonly Dictionary<string, DownloadTask> _activeDownloads = new();
        private readonly List<DownloadedMedia> _downloadedMedia = new();
        private readonly object _lock = new();

        // Events
        public event Action<DownloadTask>? OnDownloadStarted;
        public event Action<DownloadTask, double>? OnDownloadProgress;
        public event Action<DownloadTask>? OnDownloadCompleted;
        public event Action<DownloadTask, string>? OnDownloadFailed;
        public event Action<DownloadTask>? OnDownloadCancelled;
        public event Action<string>? OnStatusChanged;

        public IReadOnlyList<DownloadedMedia> DownloadedMedia => _downloadedMedia.AsReadOnly();
        public bool HasActiveDownloads => _activeDownloads.Count > 0;
        public int ActiveDownloadCount => _activeDownloads.Count;

        public DownloadService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromHours(4); // Long timeout for large files

            _downloadPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "StreamVision", "Downloads");
            Directory.CreateDirectory(_downloadPath);

            LoadDownloadedMedia();
        }

        /// <summary>
        /// Start downloading a media item
        /// </summary>
        public async Task<DownloadTask?> StartDownloadAsync(MediaItem item, StreamQuality quality = StreamQuality.High)
        {
            if (string.IsNullOrEmpty(item.StreamUrl))
            {
                OnDownloadFailed?.Invoke(null!, "URL invalide");
                return null;
            }

            // Check if already downloading
            lock (_lock)
            {
                if (_activeDownloads.ContainsKey(item.Id))
                {
                    OnStatusChanged?.Invoke("Téléchargement déjà en cours");
                    return _activeDownloads[item.Id];
                }
            }

            // Check if already downloaded
            var existing = GetDownloadedMedia(item.Id);
            if (existing != null)
            {
                OnStatusChanged?.Invoke("Déjà téléchargé");
                return null;
            }

            try
            {
                // Create download task
                var safeName = SanitizeFileName(item.Name);
                var extension = GetExtension(item.StreamUrl);
                var fileName = $"{safeName}{extension}";
                var filePath = Path.Combine(_downloadPath, fileName);

                // Ensure unique filename
                var counter = 1;
                while (File.Exists(filePath))
                {
                    fileName = $"{safeName}_{counter}{extension}";
                    filePath = Path.Combine(_downloadPath, fileName);
                    counter++;
                }

                var task = new DownloadTask
                {
                    Id = Guid.NewGuid().ToString(),
                    MediaItem = item,
                    FilePath = filePath,
                    Quality = quality,
                    Status = DownloadStatus.Starting,
                    StartTime = DateTime.Now,
                    CancellationSource = new CancellationTokenSource()
                };

                lock (_lock)
                {
                    _activeDownloads[item.Id] = task;
                }

                // Start download
                _ = DownloadFileAsync(task);

                OnDownloadStarted?.Invoke(task);
                OnStatusChanged?.Invoke($"Téléchargement de {item.Name}...");
                LogDownload($"Started download: {item.Name}");

                return task;
            }
            catch (Exception ex)
            {
                LogDownload($"Failed to start download: {ex.Message}");
                OnDownloadFailed?.Invoke(null!, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Cancel a download
        /// </summary>
        public void CancelDownload(string itemId)
        {
            DownloadTask? task;
            lock (_lock)
            {
                if (!_activeDownloads.TryGetValue(itemId, out task))
                {
                    return;
                }
                _activeDownloads.Remove(itemId);
            }

            task.CancellationSource.Cancel();
            task.Status = DownloadStatus.Cancelled;

            // Delete partial file
            try
            {
                if (File.Exists(task.FilePath))
                {
                    File.Delete(task.FilePath);
                }
            }
            catch { }

            OnDownloadCancelled?.Invoke(task);
            LogDownload($"Cancelled download: {task.MediaItem.Name}");
        }

        /// <summary>
        /// Cancel all active downloads
        /// </summary>
        public void CancelAllDownloads()
        {
            List<string> ids;
            lock (_lock)
            {
                ids = _activeDownloads.Keys.ToList();
            }

            foreach (var id in ids)
            {
                CancelDownload(id);
            }
        }

        /// <summary>
        /// Get download progress for an item
        /// </summary>
        public DownloadTask? GetDownloadProgress(string itemId)
        {
            lock (_lock)
            {
                return _activeDownloads.TryGetValue(itemId, out var task) ? task : null;
            }
        }

        /// <summary>
        /// Get downloaded media by item ID
        /// </summary>
        public DownloadedMedia? GetDownloadedMedia(string itemId)
        {
            lock (_lock)
            {
                return _downloadedMedia.FirstOrDefault(m => m.OriginalId == itemId);
            }
        }

        /// <summary>
        /// Delete a downloaded file
        /// </summary>
        public bool DeleteDownload(string downloadId)
        {
            DownloadedMedia? media;
            lock (_lock)
            {
                media = _downloadedMedia.FirstOrDefault(m => m.Id == downloadId);
                if (media != null)
                {
                    _downloadedMedia.Remove(media);
                }
            }

            if (media != null)
            {
                try
                {
                    if (File.Exists(media.FilePath))
                    {
                        File.Delete(media.FilePath);
                    }
                    SaveDownloadedMedia();
                    LogDownload($"Deleted: {media.Title}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogDownload($"Delete failed: {ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// Get total size of downloaded content
        /// </summary>
        public long GetTotalDownloadedSize()
        {
            lock (_lock)
            {
                return _downloadedMedia.Sum(m => m.FileSize);
            }
        }

        /// <summary>
        /// Open downloads folder
        /// </summary>
        public void OpenDownloadsFolder()
        {
            Process.Start("explorer.exe", _downloadPath);
        }

        #region Private Methods

        private async Task DownloadFileAsync(DownloadTask task)
        {
            var token = task.CancellationSource.Token;

            try
            {
                task.Status = DownloadStatus.Downloading;

                // Get file size first
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, task.MediaItem.StreamUrl);
                using var headResponse = await _httpClient.SendAsync(headRequest, token);

                if (headResponse.Content.Headers.ContentLength.HasValue)
                {
                    task.TotalBytes = headResponse.Content.Headers.ContentLength.Value;
                }

                // Download with progress
                using var response = await _httpClient.GetAsync(
                    task.MediaItem.StreamUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    token);

                response.EnsureSuccessStatusCode();

                if (task.TotalBytes == 0 && response.Content.Headers.ContentLength.HasValue)
                {
                    task.TotalBytes = response.Content.Headers.ContentLength.Value;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream = new FileStream(task.FilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                var lastProgressUpdate = DateTime.Now;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                    totalRead += bytesRead;
                    task.DownloadedBytes = totalRead;

                    // Update progress every 250ms
                    if ((DateTime.Now - lastProgressUpdate).TotalMilliseconds > 250)
                    {
                        var progress = task.TotalBytes > 0 ? (double)totalRead / task.TotalBytes : 0;
                        OnDownloadProgress?.Invoke(task, progress);
                        lastProgressUpdate = DateTime.Now;
                    }
                }

                // Download complete
                task.Status = DownloadStatus.Completed;
                task.EndTime = DateTime.Now;

                // Add to downloaded media
                var downloaded = new DownloadedMedia
                {
                    Id = Guid.NewGuid().ToString(),
                    OriginalId = task.MediaItem.Id,
                    Title = task.MediaItem.Name,
                    FilePath = task.FilePath,
                    FileSize = totalRead,
                    DownloadedAt = DateTime.Now,
                    MediaType = task.MediaItem.MediaType,
                    PosterUrl = task.MediaItem.PosterUrl,
                    Duration = task.MediaItem.Duration
                };

                lock (_lock)
                {
                    _downloadedMedia.Add(downloaded);
                    _activeDownloads.Remove(task.MediaItem.Id);
                }

                SaveDownloadedMedia();
                OnDownloadCompleted?.Invoke(task);
                OnStatusChanged?.Invoke($"Téléchargement terminé: {task.MediaItem.Name}");
                LogDownload($"Completed: {task.MediaItem.Name} ({FormatFileSize(totalRead)})");
            }
            catch (OperationCanceledException)
            {
                // Already handled in CancelDownload
            }
            catch (Exception ex)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = ex.Message;

                lock (_lock)
                {
                    _activeDownloads.Remove(task.MediaItem.Id);
                }

                // Clean up partial file
                try
                {
                    if (File.Exists(task.FilePath))
                    {
                        File.Delete(task.FilePath);
                    }
                }
                catch { }

                OnDownloadFailed?.Invoke(task, ex.Message);
                LogDownload($"Failed: {task.MediaItem.Name} - {ex.Message}");
            }
        }

        private void LoadDownloadedMedia()
        {
            try
            {
                var metadataPath = Path.Combine(_downloadPath, "metadata.json");
                if (File.Exists(metadataPath))
                {
                    var json = File.ReadAllText(metadataPath);
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<DownloadedMedia>>(json);
                    if (items != null)
                    {
                        // Verify files still exist
                        foreach (var item in items.Where(i => File.Exists(i.FilePath)))
                        {
                            _downloadedMedia.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDownload($"Failed to load metadata: {ex.Message}");
            }
        }

        private void SaveDownloadedMedia()
        {
            try
            {
                var metadataPath = Path.Combine(_downloadPath, "metadata.json");
                var json = System.Text.Json.JsonSerializer.Serialize(_downloadedMedia);
                File.WriteAllText(metadataPath, json);
            }
            catch (Exception ex)
            {
                LogDownload($"Failed to save metadata: {ex.Message}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            return sanitized.Length > 100 ? sanitized.Substring(0, 100) : sanitized;
        }

        private static string GetExtension(string url)
        {
            try
            {
                var uri = new Uri(url);
                var ext = Path.GetExtension(uri.AbsolutePath);
                return string.IsNullOrEmpty(ext) ? ".mp4" : ext;
            }
            catch
            {
                return ".mp4";
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private static void LogDownload(string message)
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "download.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #endregion

        public void Dispose()
        {
            CancelAllDownloads();
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// Active download task
    /// </summary>
    public class DownloadTask
    {
        public string Id { get; set; } = "";
        public MediaItem MediaItem { get; set; } = null!;
        public string FilePath { get; set; } = "";
        public StreamQuality Quality { get; set; }
        public DownloadStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public string? ErrorMessage { get; set; }
        public CancellationTokenSource CancellationSource { get; set; } = null!;

        public double Progress => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes : 0;
        public string ProgressDisplay => $"{Progress:P0}";
        public string SizeDisplay => TotalBytes > 0
            ? $"{FormatSize(DownloadedBytes)} / {FormatSize(TotalBytes)}"
            : FormatSize(DownloadedBytes);

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }

    /// <summary>
    /// Downloaded media info
    /// </summary>
    public class DownloadedMedia
    {
        public string Id { get; set; } = "";
        public string OriginalId { get; set; } = "";
        public string Title { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime DownloadedAt { get; set; }
        public ContentType MediaType { get; set; }
        public string? PosterUrl { get; set; }
        public long Duration { get; set; }

        public string FileSizeDisplay => FormatSize(FileSize);
        public string DurationDisplay => TimeSpan.FromMilliseconds(Duration).ToString(@"hh\:mm\:ss");

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }

    /// <summary>
    /// Download status
    /// </summary>
    public enum DownloadStatus
    {
        Queued,
        Starting,
        Downloading,
        Paused,
        Completed,
        Failed,
        Cancelled
    }
}
