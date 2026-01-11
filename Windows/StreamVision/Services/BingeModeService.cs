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
    /// Binge watching mode service for series
    /// Features: Auto-play next episode, Skip Intro/Outro, Countdown
    /// </summary>
    public class BingeModeService : IDisposable
    {
        private MediaPlayer? _mediaPlayer;
        private CancellationTokenSource? _countdownCts;

        // Configuration
        private BingeModeSettings _settings = new();

        // Current state
        private MediaItem? _currentEpisode;
        private List<MediaItem> _episodeList = new();
        private int _currentIndex = -1;
        private bool _isCountingDown;
        private int _countdownSeconds;

        // Intro/Outro detection
        private TimeSpan? _detectedIntroEnd;
        private TimeSpan? _detectedOutroStart;
        private bool _introSkipped;
        private bool _outroDetected;

        // Events
        public event Action<MediaItem>? OnNextEpisodeReady;
        public event Action<int>? OnCountdownTick;
        public event Action? OnCountdownCancelled;
        public event Action<MediaItem>? OnAutoPlayNext;
        public event Action<TimeSpan>? OnIntroDetected;
        public event Action? OnIntroSkipped;
        public event Action<TimeSpan>? OnOutroDetected;
        public event Action<string>? OnStatusChanged;

        public bool IsEnabled { get; set; } = true;
        public bool IsCountingDown => _isCountingDown;
        public int CountdownRemaining => _countdownSeconds;
        public MediaItem? NextEpisode => GetNextEpisode();
        public BingeModeSettings Settings => _settings;

        /// <summary>
        /// Set the media player to monitor
        /// </summary>
        public void SetMediaPlayer(MediaPlayer player)
        {
            // Unsubscribe from old player
            if (_mediaPlayer != null)
            {
                _mediaPlayer.TimeChanged -= OnTimeChanged;
                _mediaPlayer.EndReached -= OnEndReached;
                _mediaPlayer.PositionChanged -= OnPositionChanged;
            }

            _mediaPlayer = player;

            // Subscribe to new player
            if (_mediaPlayer != null)
            {
                _mediaPlayer.TimeChanged += OnTimeChanged;
                _mediaPlayer.EndReached += OnEndReached;
                _mediaPlayer.PositionChanged += OnPositionChanged;
            }
        }

        /// <summary>
        /// Set the episode list for the current series
        /// </summary>
        public void SetEpisodeList(IEnumerable<MediaItem> episodes)
        {
            _episodeList = episodes
                .Where(e => e.MediaType == ContentType.Episode || e.MediaType == ContentType.Series)
                .OrderBy(e => e.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .ToList();

            LogBinge($"Episode list set: {_episodeList.Count} episodes");
        }

        /// <summary>
        /// Notify when an episode starts playing
        /// </summary>
        public void OnEpisodeStarted(MediaItem episode)
        {
            _currentEpisode = episode;
            _currentIndex = _episodeList.FindIndex(e => e.Id == episode.Id);
            _introSkipped = false;
            _outroDetected = false;
            _detectedIntroEnd = null;
            _detectedOutroStart = null;

            CancelCountdown();

            LogBinge($"Started episode: {episode.Name} (S{episode.SeasonNumber}E{episode.EpisodeNumber})");

            // Check if there's a next episode
            var next = GetNextEpisode();
            if (next != null)
            {
                OnNextEpisodeReady?.Invoke(next);
            }
        }

        /// <summary>
        /// Update settings
        /// </summary>
        public void UpdateSettings(BingeModeSettings settings)
        {
            _settings = settings;
            LogBinge($"Settings updated: AutoPlay={settings.AutoPlayNext}, SkipIntro={settings.AutoSkipIntro}");
        }

        /// <summary>
        /// Skip intro manually
        /// </summary>
        public void SkipIntro()
        {
            if (_mediaPlayer == null || _introSkipped) return;

            // Use detected intro end or default skip time
            var skipTo = _detectedIntroEnd ?? TimeSpan.FromSeconds(_settings.DefaultIntroLength);

            if (_mediaPlayer.Length > 0)
            {
                var position = (float)(skipTo.TotalMilliseconds / _mediaPlayer.Length);
                _mediaPlayer.Position = Math.Min(position, 0.9f);
                _introSkipped = true;
                OnIntroSkipped?.Invoke();
                OnStatusChanged?.Invoke("Intro passée");
                LogBinge($"Intro skipped to {skipTo.TotalSeconds}s");
            }
        }

        /// <summary>
        /// Play next episode immediately
        /// </summary>
        public MediaItem? PlayNextEpisode()
        {
            CancelCountdown();
            var next = GetNextEpisode();
            if (next != null)
            {
                _currentIndex++;
                OnAutoPlayNext?.Invoke(next);
                LogBinge($"Playing next: {next.Name}");
            }
            return next;
        }

        /// <summary>
        /// Cancel auto-play countdown
        /// </summary>
        public void CancelCountdown()
        {
            _countdownCts?.Cancel();
            _isCountingDown = false;
            _countdownSeconds = 0;
            OnCountdownCancelled?.Invoke();
        }

        /// <summary>
        /// Get next episode in list
        /// </summary>
        public MediaItem? GetNextEpisode()
        {
            if (_currentIndex < 0 || _currentIndex >= _episodeList.Count - 1)
                return null;

            return _episodeList[_currentIndex + 1];
        }

        /// <summary>
        /// Get previous episode in list
        /// </summary>
        public MediaItem? GetPreviousEpisode()
        {
            if (_currentIndex <= 0)
                return null;

            return _episodeList[_currentIndex - 1];
        }

        #region Event Handlers

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            if (_mediaPlayer == null || _currentEpisode == null) return;

            var currentTime = TimeSpan.FromMilliseconds(e.Time);
            var totalTime = TimeSpan.FromMilliseconds(_mediaPlayer.Length);

            // Intro detection/skip (first 2 minutes)
            if (!_introSkipped && currentTime.TotalSeconds < 120)
            {
                CheckForIntro(currentTime);
            }

            // Outro detection (last 3 minutes)
            if (!_outroDetected && totalTime.TotalSeconds > 0)
            {
                var remaining = totalTime - currentTime;
                if (remaining.TotalSeconds < 180)
                {
                    CheckForOutro(currentTime, totalTime);
                }
            }
        }

        private void OnPositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e)
        {
            // Additional position-based checks if needed
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            if (!IsEnabled || !_settings.AutoPlayNext) return;

            var next = GetNextEpisode();
            if (next == null) return;

            // Start countdown
            _ = StartCountdownAsync();
        }

        #endregion

        #region Private Methods

        private async Task StartCountdownAsync()
        {
            var next = GetNextEpisode();
            if (next == null) return;

            _isCountingDown = true;
            _countdownSeconds = _settings.CountdownSeconds;
            _countdownCts = new CancellationTokenSource();

            LogBinge($"Starting countdown: {_countdownSeconds}s until {next.Name}");
            OnStatusChanged?.Invoke($"Prochain épisode dans {_countdownSeconds}s");

            try
            {
                while (_countdownSeconds > 0 && !_countdownCts.Token.IsCancellationRequested)
                {
                    OnCountdownTick?.Invoke(_countdownSeconds);
                    await Task.Delay(1000, _countdownCts.Token);
                    _countdownSeconds--;
                }

                if (!_countdownCts.Token.IsCancellationRequested)
                {
                    PlayNextEpisode();
                }
            }
            catch (OperationCanceledException)
            {
                LogBinge("Countdown cancelled");
            }
            finally
            {
                _isCountingDown = false;
            }
        }

        private void CheckForIntro(TimeSpan currentTime)
        {
            // Simple heuristic: if we're past the typical intro time and settings allow
            if (_settings.AutoSkipIntro && currentTime.TotalSeconds >= 5 && currentTime.TotalSeconds <= 10)
            {
                // Could integrate with audio fingerprinting or scene detection
                // For now, use default intro length
                _detectedIntroEnd = TimeSpan.FromSeconds(_settings.DefaultIntroLength);
                OnIntroDetected?.Invoke(_detectedIntroEnd.Value);

                if (_settings.AutoSkipIntro)
                {
                    SkipIntro();
                }
            }
        }

        private void CheckForOutro(TimeSpan currentTime, TimeSpan totalTime)
        {
            var remaining = totalTime - currentTime;

            // Detect when we're near the end (credits typically start)
            if (remaining.TotalSeconds <= _settings.DefaultOutroLength && !_outroDetected)
            {
                _detectedOutroStart = currentTime;
                _outroDetected = true;
                OnOutroDetected?.Invoke(currentTime);

                // If skip outro enabled and next episode exists, show prompt
                if (_settings.SkipOutroToNext && GetNextEpisode() != null)
                {
                    OnStatusChanged?.Invoke("Passer au prochain épisode ?");
                }
            }
        }

        private static void LogBinge(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "binge.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #endregion

        public void Dispose()
        {
            CancelCountdown();
            _countdownCts?.Dispose();

            if (_mediaPlayer != null)
            {
                _mediaPlayer.TimeChanged -= OnTimeChanged;
                _mediaPlayer.EndReached -= OnEndReached;
                _mediaPlayer.PositionChanged -= OnPositionChanged;
            }
        }
    }

    /// <summary>
    /// Binge mode settings
    /// </summary>
    public class BingeModeSettings
    {
        /// <summary>
        /// Auto-play next episode when current ends
        /// </summary>
        public bool AutoPlayNext { get; set; } = true;

        /// <summary>
        /// Countdown seconds before auto-playing next
        /// </summary>
        public int CountdownSeconds { get; set; } = 10;

        /// <summary>
        /// Automatically skip intro
        /// </summary>
        public bool AutoSkipIntro { get; set; } = false;

        /// <summary>
        /// Show skip intro button (even if not auto)
        /// </summary>
        public bool ShowSkipIntroButton { get; set; } = true;

        /// <summary>
        /// Default intro length in seconds
        /// </summary>
        public int DefaultIntroLength { get; set; } = 90; // 1:30

        /// <summary>
        /// Default outro/credits length in seconds
        /// </summary>
        public int DefaultOutroLength { get; set; } = 120; // 2:00

        /// <summary>
        /// Skip to next episode when outro starts
        /// </summary>
        public bool SkipOutroToNext { get; set; } = false;

        /// <summary>
        /// Remember intro skip time per series
        /// </summary>
        public bool RememberIntroTimes { get; set; } = true;
    }
}
