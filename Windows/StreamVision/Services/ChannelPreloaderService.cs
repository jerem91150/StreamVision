using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Service for preloading adjacent channels for instant zapping
    /// Keeps next/previous channels ready in memory for < 500ms channel switch
    /// </summary>
    public class ChannelPreloaderService : IDisposable
    {
        private readonly LibVLC _libVLC;
        private readonly Dictionary<string, PreloadedChannel> _preloadedChannels = new();
        private readonly object _lock = new();
        private CancellationTokenSource? _preloadCts;

        // Configuration
        private const int MAX_PRELOADED_CHANNELS = 3; // Current + 2 adjacent
        private const int PRELOAD_BUFFER_MS = 2000;   // Light buffer for preload
        private const int PRELOAD_DELAY_MS = 1500;    // Wait before preloading to avoid spam

        // Current state
        private List<MediaItem> _channelList = new();
        private int _currentIndex = -1;
        private string? _currentChannelId;

        // Events
        public event Action<string>? OnPreloadStatusChanged;
        public event Action<MediaItem>? OnChannelReady;

        public bool IsPreloading { get; private set; }

        public ChannelPreloaderService(LibVLC libVLC)
        {
            _libVLC = libVLC;
        }

        /// <summary>
        /// Set the channel list for navigation
        /// </summary>
        public void SetChannelList(IEnumerable<MediaItem> channels)
        {
            lock (_lock)
            {
                _channelList = channels.Where(c => c.MediaType == ContentType.Live).ToList();
                LogPreloader($"Channel list set: {_channelList.Count} live channels");
            }
        }

        /// <summary>
        /// Notify when a channel starts playing - triggers preload of adjacent channels
        /// </summary>
        public async Task OnChannelStartedAsync(MediaItem channel)
        {
            // Cancel any ongoing preload
            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();

            lock (_lock)
            {
                _currentChannelId = channel.Id;
                _currentIndex = _channelList.FindIndex(c => c.Id == channel.Id);
            }

            if (_currentIndex < 0)
            {
                LogPreloader($"Channel not found in list: {channel.Name}");
                return;
            }

            LogPreloader($"Current channel: {channel.Name} (index {_currentIndex})");

            // Wait a bit before preloading (user might be zapping)
            try
            {
                await Task.Delay(PRELOAD_DELAY_MS, _preloadCts.Token);
                await PreloadAdjacentChannelsAsync(_preloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                // User changed channel, that's fine
            }
        }

        /// <summary>
        /// Get preloaded media player for instant switch
        /// Returns null if channel not preloaded
        /// </summary>
        public PreloadedChannel? GetPreloadedChannel(string channelId)
        {
            lock (_lock)
            {
                if (_preloadedChannels.TryGetValue(channelId, out var preloaded))
                {
                    if (preloaded.IsReady)
                    {
                        LogPreloader($"Returning preloaded channel: {preloaded.Channel.Name}");
                        return preloaded;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get the next channel in the list
        /// </summary>
        public MediaItem? GetNextChannel()
        {
            lock (_lock)
            {
                if (_channelList.Count == 0 || _currentIndex < 0) return null;
                var nextIndex = (_currentIndex + 1) % _channelList.Count;
                return _channelList[nextIndex];
            }
        }

        /// <summary>
        /// Get the previous channel in the list
        /// </summary>
        public MediaItem? GetPreviousChannel()
        {
            lock (_lock)
            {
                if (_channelList.Count == 0 || _currentIndex < 0) return null;
                var prevIndex = _currentIndex == 0 ? _channelList.Count - 1 : _currentIndex - 1;
                return _channelList[prevIndex];
            }
        }

        /// <summary>
        /// Quick zap to next channel
        /// </summary>
        public MediaItem? ZapNext()
        {
            var next = GetNextChannel();
            if (next != null)
            {
                _currentIndex = (_currentIndex + 1) % _channelList.Count;
                _currentChannelId = next.Id;
            }
            return next;
        }

        /// <summary>
        /// Quick zap to previous channel
        /// </summary>
        public MediaItem? ZapPrevious()
        {
            var prev = GetPreviousChannel();
            if (prev != null)
            {
                _currentIndex = _currentIndex == 0 ? _channelList.Count - 1 : _currentIndex - 1;
                _currentChannelId = prev.Id;
            }
            return prev;
        }

        /// <summary>
        /// Zap to a specific channel number (1-based)
        /// </summary>
        public MediaItem? ZapToNumber(int channelNumber)
        {
            lock (_lock)
            {
                if (channelNumber < 1 || channelNumber > _channelList.Count) return null;
                _currentIndex = channelNumber - 1;
                var channel = _channelList[_currentIndex];
                _currentChannelId = channel.Id;
                return channel;
            }
        }

        #region Private Methods

        private async Task PreloadAdjacentChannelsAsync(CancellationToken ct)
        {
            IsPreloading = true;
            OnPreloadStatusChanged?.Invoke("Préchargement...");

            try
            {
                // Clean up old preloaded channels
                CleanupOldPreloads();

                // Get channels to preload (next and previous)
                var channelsToPreload = new List<MediaItem>();

                lock (_lock)
                {
                    if (_channelList.Count < 2) return;

                    // Next channel
                    var nextIndex = (_currentIndex + 1) % _channelList.Count;
                    channelsToPreload.Add(_channelList[nextIndex]);

                    // Previous channel
                    var prevIndex = _currentIndex == 0 ? _channelList.Count - 1 : _currentIndex - 1;
                    if (prevIndex != nextIndex) // Avoid duplicate if only 2 channels
                    {
                        channelsToPreload.Add(_channelList[prevIndex]);
                    }
                }

                // Preload each channel
                foreach (var channel in channelsToPreload)
                {
                    if (ct.IsCancellationRequested) break;

                    // Skip if already preloaded
                    lock (_lock)
                    {
                        if (_preloadedChannels.ContainsKey(channel.Id)) continue;
                    }

                    await PreloadChannelAsync(channel, ct);
                }

                OnPreloadStatusChanged?.Invoke("Prêt");
            }
            catch (Exception ex)
            {
                LogPreloader($"Preload error: {ex.Message}");
            }
            finally
            {
                IsPreloading = false;
            }
        }

        private async Task PreloadChannelAsync(MediaItem channel, CancellationToken ct)
        {
            try
            {
                LogPreloader($"Preloading: {channel.Name}");

                var media = new Media(_libVLC, new Uri(channel.StreamUrl));
                media.AddOption($":network-caching={PRELOAD_BUFFER_MS}");
                media.AddOption(":no-video-title-show");

                var preloaded = new PreloadedChannel
                {
                    Channel = channel,
                    Media = media,
                    PreloadedAt = DateTime.Now,
                    IsReady = false
                };

                // Parse media to start buffering metadata
                await media.Parse(MediaParseOptions.ParseNetwork, timeout: 5000, ct);

                preloaded.IsReady = true;

                lock (_lock)
                {
                    _preloadedChannels[channel.Id] = preloaded;
                }

                LogPreloader($"Preloaded: {channel.Name}");
                OnChannelReady?.Invoke(channel);
            }
            catch (Exception ex)
            {
                LogPreloader($"Failed to preload {channel.Name}: {ex.Message}");
            }
        }

        private void CleanupOldPreloads()
        {
            lock (_lock)
            {
                // Remove preloaded channels that are no longer adjacent
                var toRemove = new List<string>();

                foreach (var kvp in _preloadedChannels)
                {
                    // Keep current, next, and previous
                    var index = _channelList.FindIndex(c => c.Id == kvp.Key);
                    if (index < 0)
                    {
                        toRemove.Add(kvp.Key);
                        continue;
                    }

                    var distance = Math.Min(
                        Math.Abs(index - _currentIndex),
                        _channelList.Count - Math.Abs(index - _currentIndex)
                    );

                    if (distance > 1) // Not adjacent
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var id in toRemove)
                {
                    if (_preloadedChannels.TryGetValue(id, out var preloaded))
                    {
                        preloaded.Media?.Dispose();
                        _preloadedChannels.Remove(id);
                        LogPreloader($"Cleaned up: {preloaded.Channel.Name}");
                    }
                }
            }
        }

        private static void LogPreloader(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "preloader.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #endregion

        public void Dispose()
        {
            _preloadCts?.Cancel();
            _preloadCts?.Dispose();

            lock (_lock)
            {
                foreach (var preloaded in _preloadedChannels.Values)
                {
                    preloaded.Media?.Dispose();
                }
                _preloadedChannels.Clear();
            }
        }
    }

    /// <summary>
    /// Represents a preloaded channel ready for instant playback
    /// </summary>
    public class PreloadedChannel
    {
        public MediaItem Channel { get; set; } = null!;
        public Media? Media { get; set; }
        public DateTime PreloadedAt { get; set; }
        public bool IsReady { get; set; }

        /// <summary>
        /// Check if preload is still valid (not too old)
        /// </summary>
        public bool IsValid => IsReady && (DateTime.Now - PreloadedAt).TotalMinutes < 5;
    }
}
