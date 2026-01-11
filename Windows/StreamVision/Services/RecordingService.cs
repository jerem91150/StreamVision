using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Service for recording live TV streams and VOD content
    /// </summary>
    public class RecordingService : IDisposable
    {
        private readonly LibVLC _libVLC;
        private readonly string _recordingsPath;
        private readonly Dictionary<string, ActiveRecording> _activeRecordings = new();
        private readonly List<RecordingInfo> _recordingHistory = new();
        private readonly object _lock = new();

        // Events
        public event Action<RecordingInfo>? OnRecordingStarted;
        public event Action<RecordingInfo>? OnRecordingStopped;
        public event Action<RecordingInfo, double>? OnRecordingProgress;
        public event Action<string>? OnRecordingError;

        public bool HasActiveRecordings => _activeRecordings.Count > 0;
        public IReadOnlyList<RecordingInfo> RecordingHistory => _recordingHistory.AsReadOnly();

        public RecordingService(LibVLC libVLC)
        {
            _libVLC = libVLC;
            _recordingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "StreamVision", "Recordings");
            Directory.CreateDirectory(_recordingsPath);
        }

        /// <summary>
        /// Start recording a stream
        /// </summary>
        public async Task<RecordingInfo?> StartRecordingAsync(MediaItem item, TimeSpan? duration = null)
        {
            if (string.IsNullOrEmpty(item.StreamUrl))
            {
                OnRecordingError?.Invoke("URL du flux invalide");
                return null;
            }

            // Check if already recording this stream
            lock (_lock)
            {
                if (_activeRecordings.ContainsKey(item.Id))
                {
                    OnRecordingError?.Invoke("Enregistrement déjà en cours pour ce flux");
                    return null;
                }
            }

            try
            {
                // Create recording info
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var safeName = SanitizeFileName(item.Name);
                var fileName = $"{safeName}_{timestamp}.ts";
                var filePath = Path.Combine(_recordingsPath, fileName);

                var recording = new RecordingInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    MediaItem = item,
                    FilePath = filePath,
                    StartTime = DateTime.Now,
                    PlannedDuration = duration,
                    Status = RecordingStatus.Starting
                };

                // Create media with recording options
                var media = new Media(_libVLC, new Uri(item.StreamUrl));
                media.AddOption(":sout=#duplicate{dst=std{access=file,mux=ts,dst='" + filePath.Replace("\\", "/") + "'}}");
                media.AddOption(":sout-keep");
                media.AddOption(":network-caching=3000");

                var player = new MediaPlayer(_libVLC);
                player.Media = media;

                // Create active recording
                var active = new ActiveRecording
                {
                    Info = recording,
                    Player = player,
                    Media = media,
                    CancellationSource = new CancellationTokenSource()
                };

                // Subscribe to events
                player.Playing += (s, e) =>
                {
                    recording.Status = RecordingStatus.Recording;
                    LogRecording($"Recording started: {item.Name}");
                    OnRecordingStarted?.Invoke(recording);
                };

                player.EncounteredError += (s, e) =>
                {
                    recording.Status = RecordingStatus.Error;
                    OnRecordingError?.Invoke($"Erreur d'enregistrement: {item.Name}");
                };

                player.EndReached += (s, e) =>
                {
                    StopRecording(item.Id);
                };

                lock (_lock)
                {
                    _activeRecordings[item.Id] = active;
                }

                // Start playback (which triggers recording)
                player.Play();

                // Start progress monitoring
                _ = MonitorRecordingProgressAsync(active);

                // If duration specified, schedule auto-stop
                if (duration.HasValue)
                {
                    _ = ScheduleRecordingStopAsync(item.Id, duration.Value, active.CancellationSource.Token);
                }

                return recording;
            }
            catch (Exception ex)
            {
                LogRecording($"Failed to start recording: {ex.Message}");
                OnRecordingError?.Invoke($"Échec démarrage enregistrement: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Stop an active recording
        /// </summary>
        public RecordingInfo? StopRecording(string itemId)
        {
            ActiveRecording? active;
            lock (_lock)
            {
                if (!_activeRecordings.TryGetValue(itemId, out active))
                {
                    return null;
                }
                _activeRecordings.Remove(itemId);
            }

            try
            {
                active.CancellationSource.Cancel();
                active.Player.Stop();
                active.Player.Dispose();
                active.Media.Dispose();

                active.Info.EndTime = DateTime.Now;
                active.Info.Status = RecordingStatus.Completed;

                // Get file size
                if (File.Exists(active.Info.FilePath))
                {
                    var fileInfo = new FileInfo(active.Info.FilePath);
                    active.Info.FileSize = fileInfo.Length;
                }

                lock (_lock)
                {
                    _recordingHistory.Add(active.Info);
                }

                LogRecording($"Recording stopped: {active.Info.MediaItem.Name}, Size: {FormatFileSize(active.Info.FileSize)}");
                OnRecordingStopped?.Invoke(active.Info);

                return active.Info;
            }
            catch (Exception ex)
            {
                LogRecording($"Error stopping recording: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Stop all active recordings
        /// </summary>
        public void StopAllRecordings()
        {
            List<string> ids;
            lock (_lock)
            {
                ids = _activeRecordings.Keys.ToList();
            }

            foreach (var id in ids)
            {
                StopRecording(id);
            }
        }

        /// <summary>
        /// Get active recording for an item
        /// </summary>
        public RecordingInfo? GetActiveRecording(string itemId)
        {
            lock (_lock)
            {
                if (_activeRecordings.TryGetValue(itemId, out var active))
                {
                    return active.Info;
                }
            }
            return null;
        }

        /// <summary>
        /// Get all active recordings
        /// </summary>
        public List<RecordingInfo> GetActiveRecordings()
        {
            lock (_lock)
            {
                return _activeRecordings.Values.Select(a => a.Info).ToList();
            }
        }

        /// <summary>
        /// Schedule a recording for later
        /// </summary>
        public async Task<bool> ScheduleRecordingAsync(MediaItem item, DateTime startTime, TimeSpan duration)
        {
            var delay = startTime - DateTime.Now;
            if (delay.TotalSeconds < 0)
            {
                OnRecordingError?.Invoke("L'heure de début est dans le passé");
                return false;
            }

            LogRecording($"Scheduled recording: {item.Name} at {startTime}");

            // Wait until start time
            await Task.Delay(delay);

            // Start recording
            var result = await StartRecordingAsync(item, duration);
            return result != null;
        }

        /// <summary>
        /// Delete a recorded file
        /// </summary>
        public bool DeleteRecording(string recordingId)
        {
            RecordingInfo? recording;
            lock (_lock)
            {
                recording = _recordingHistory.FirstOrDefault(r => r.Id == recordingId);
                if (recording != null)
                {
                    _recordingHistory.Remove(recording);
                }
            }

            if (recording != null && File.Exists(recording.FilePath))
            {
                try
                {
                    File.Delete(recording.FilePath);
                    LogRecording($"Deleted recording: {recording.MediaItem.Name}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogRecording($"Failed to delete recording: {ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// Get the recordings folder path
        /// </summary>
        public string GetRecordingsPath() => _recordingsPath;

        /// <summary>
        /// Open the recordings folder in Explorer
        /// </summary>
        public void OpenRecordingsFolder()
        {
            Process.Start("explorer.exe", _recordingsPath);
        }

        #region Private Methods

        private async Task MonitorRecordingProgressAsync(ActiveRecording active)
        {
            var token = active.CancellationSource.Token;
            var startTime = DateTime.Now;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token);

                    var elapsed = DateTime.Now - startTime;
                    active.Info.Duration = elapsed;

                    // Update file size
                    if (File.Exists(active.Info.FilePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(active.Info.FilePath);
                            active.Info.FileSize = fileInfo.Length;
                        }
                        catch { }
                    }

                    // Calculate progress if planned duration
                    if (active.Info.PlannedDuration.HasValue)
                    {
                        var progress = elapsed.TotalSeconds / active.Info.PlannedDuration.Value.TotalSeconds;
                        OnRecordingProgress?.Invoke(active.Info, Math.Min(progress, 1.0));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ScheduleRecordingStopAsync(string itemId, TimeSpan duration, CancellationToken ct)
        {
            try
            {
                await Task.Delay(duration, ct);
                StopRecording(itemId);
            }
            catch (OperationCanceledException)
            {
                // Recording was stopped manually
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private static void LogRecording(string message)
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "recording.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #endregion

        public void Dispose()
        {
            StopAllRecordings();
        }
    }

    /// <summary>
    /// Information about a recording
    /// </summary>
    public class RecordingInfo
    {
        public string Id { get; set; } = "";
        public MediaItem MediaItem { get; set; } = null!;
        public string FilePath { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? PlannedDuration { get; set; }
        public TimeSpan Duration { get; set; }
        public long FileSize { get; set; }
        public RecordingStatus Status { get; set; }

        public string DurationDisplay => $"{(int)Duration.TotalHours:D2}:{Duration.Minutes:D2}:{Duration.Seconds:D2}";
        public string FileSizeDisplay => FormatFileSize(FileSize);

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }

    /// <summary>
    /// Active recording state
    /// </summary>
    internal class ActiveRecording
    {
        public RecordingInfo Info { get; set; } = null!;
        public MediaPlayer Player { get; set; } = null!;
        public Media Media { get; set; } = null!;
        public CancellationTokenSource CancellationSource { get; set; } = null!;
    }

    /// <summary>
    /// Recording status
    /// </summary>
    public enum RecordingStatus
    {
        Scheduled,
        Starting,
        Recording,
        Paused,
        Completed,
        Error,
        Cancelled
    }
}
