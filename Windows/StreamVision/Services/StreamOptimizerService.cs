using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Automatic stream optimization service with REAL-TIME adaptation
    /// Analyzes connection, monitors playback, and auto-adjusts settings during playback
    /// </summary>
    public class StreamOptimizerService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
        private LibVLC? _libVLC;
        private CancellationTokenSource? _monitoringCts;
        private PlayerSettings _settings;
        private string? _currentStreamUrl;

        // Real-time monitoring state
        private int _bufferingCount = 0;
        private int _errorCount = 0;
        private int _stallCount = 0;
        private DateTime _lastBufferingTime = DateTime.MinValue;
        private DateTime _playbackStartTime;
        private DateTime _lastStallTime = DateTime.MinValue;
        private bool _isMonitoring = false;
        private bool _isRecovering = false;

        // Quality tracking
        private readonly Queue<float> _bufferHistory = new();
        private readonly Queue<DateTime> _bufferingEvents = new();
        private float _minBufferSeen = 100f;
        private float _avgBufferLevel = 100f;
        private int _consecutiveLowBuffer = 0;
        private const int BUFFER_HISTORY_SIZE = 30; // 30 samples
        private const float CRITICAL_BUFFER_THRESHOLD = 30f;
        private const float WARNING_BUFFER_THRESHOLD = 50f;
        private const int MAX_BUFFERING_EVENTS_WINDOW = 60; // seconds

        // Callbacks for UI updates
        public event Action<string>? OnStatusChanged;
        public event Action<StreamQualityInfo>? OnQualityAnalyzed;
        public event Action<PlayerSettings>? OnSettingsOptimized;
        public event Action<string>? OnReconnecting;
        public event Action<PlaybackHealthStatus>? OnHealthStatusChanged;
        public event Action? OnRestartRequired;
        public event Action<StreamQuality>? OnQualityDowngradeRequested;

        public PlayerSettings CurrentSettings => _settings;
        public bool IsOptimizing { get; private set; }
        public PlaybackHealthStatus CurrentHealth { get; private set; } = PlaybackHealthStatus.Unknown;
        public StreamQuality CurrentQuality { get; private set; } = StreamQuality.Auto;
        private int _qualityDowngradeCount = 0;

        public StreamOptimizerService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _settings = new PlayerSettings();
        }

        /// <summary>
        /// Set the media player and LibVLC instance for monitoring and restart capability
        /// </summary>
        public void SetMediaPlayer(LibVLCSharp.Shared.MediaPlayer mediaPlayer, LibVLC? libVLC = null)
        {
            _mediaPlayer = mediaPlayer;
            _libVLC = libVLC;
        }

        /// <summary>
        /// Set the current stream URL for reconnection
        /// </summary>
        public void SetCurrentStream(string streamUrl)
        {
            _currentStreamUrl = streamUrl;
        }

        /// <summary>
        /// Reset monitoring state (call when starting new playback)
        /// </summary>
        private void ResetMonitoringState()
        {
            _bufferingCount = 0;
            _errorCount = 0;
            _stallCount = 0;
            _consecutiveLowBuffer = 0;
            _minBufferSeen = 100f;
            _avgBufferLevel = 100f;
            _bufferHistory.Clear();
            _bufferingEvents.Clear();
            _lastBufferingTime = DateTime.MinValue;
            _lastStallTime = DateTime.MinValue;
            _isRecovering = false;
            _qualityDowngradeCount = 0;
            CurrentHealth = PlaybackHealthStatus.Excellent;
            CurrentQuality = StreamQuality.Auto;
        }

        /// <summary>
        /// Set the preferred quality level
        /// </summary>
        public void SetQuality(StreamQuality quality)
        {
            CurrentQuality = quality;
            LogOptimizer($"Quality manually set to: {quality}");
        }

        /// <summary>
        /// Request a quality downgrade to save bandwidth
        /// Returns the new quality level, or null if already at lowest
        /// </summary>
        public StreamQuality? RequestQualityDowngrade()
        {
            // Determine next lower quality
            StreamQuality? newQuality = CurrentQuality switch
            {
                StreamQuality.Ultra => StreamQuality.High,
                StreamQuality.High => StreamQuality.Medium,
                StreamQuality.Medium => StreamQuality.Low,
                StreamQuality.Auto => StreamQuality.Medium, // From auto, first try medium
                _ => null
            };

            if (newQuality.HasValue)
            {
                CurrentQuality = newQuality.Value;
                _qualityDowngradeCount++;
                LogOptimizer($"Quality downgraded to: {newQuality.Value} (count: {_qualityDowngradeCount})");
                OnQualityDowngradeRequested?.Invoke(newQuality.Value);
            }

            return newQuality;
        }

        /// <summary>
        /// Analyze stream and optimize settings before playback
        /// </summary>
        public async Task<PlayerSettings> OptimizeForStreamAsync(string streamUrl, ContentType contentType)
        {
            IsOptimizing = true;
            OnStatusChanged?.Invoke("Analyse du flux en cours...");

            try
            {
                // Step 1: Test connection speed and latency
                var connectionInfo = await TestConnectionAsync(streamUrl);
                OnStatusChanged?.Invoke($"Connexion: {connectionInfo.SpeedCategory}");

                // Step 2: Determine optimal settings based on analysis
                _settings = DetermineOptimalSettings(connectionInfo, contentType);

                // Step 3: Notify UI
                OnQualityAnalyzed?.Invoke(new StreamQualityInfo
                {
                    Latency = connectionInfo.Latency,
                    DownloadSpeed = connectionInfo.DownloadSpeedMbps,
                    RecommendedBuffer = _settings.NetworkCaching,
                    QualityLevel = connectionInfo.SpeedCategory,
                    ContentType = contentType
                });

                OnSettingsOptimized?.Invoke(_settings);
                OnStatusChanged?.Invoke("Optimisation terminée");

                return _settings;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Optimisation échouée: {ex.Message}");
                // Return safe defaults
                return PlayerSettings.GetPreset("stable");
            }
            finally
            {
                IsOptimizing = false;
            }
        }

        /// <summary>
        /// Quick optimization without full analysis (for fast startup)
        /// </summary>
        public PlayerSettings QuickOptimize(ContentType contentType, bool isIPTV = true)
        {
            // Use presets based on content type
            if (isIPTV)
            {
                // IPTV typically needs more buffer
                _settings = contentType switch
                {
                    ContentType.Live => new PlayerSettings
                    {
                        NetworkCaching = 4000,
                        LiveCaching = 4000,
                        HardwareAcceleration = true,
                        SkipFramesOnLag = true,
                        AutoReconnect = true,
                        ReconnectAttempts = 5,
                        MinBufferBeforePlay = 25
                    },
                    ContentType.Movie => new PlayerSettings
                    {
                        NetworkCaching = 6000,
                        LiveCaching = 3000,
                        FileCaching = 4000,
                        HardwareAcceleration = true,
                        SkipFramesOnLag = true,
                        AutoReconnect = true,
                        ReconnectAttempts = 3,
                        MinBufferBeforePlay = 30,
                        RememberPosition = true
                    },
                    ContentType.Series => new PlayerSettings
                    {
                        NetworkCaching = 5000,
                        LiveCaching = 3000,
                        FileCaching = 3000,
                        HardwareAcceleration = true,
                        SkipFramesOnLag = true,
                        AutoReconnect = true,
                        ReconnectAttempts = 3,
                        MinBufferBeforePlay = 25,
                        RememberPosition = true,
                        AutoPlayNext = true
                    },
                    _ => PlayerSettings.GetPreset("stable")
                };
            }
            else
            {
                _settings = PlayerSettings.GetPreset("default");
            }

            return _settings;
        }

        /// <summary>
        /// Start real-time monitoring of playback quality with auto-adaptation
        /// </summary>
        public void StartMonitoring()
        {
            if (_mediaPlayer == null || _isMonitoring) return;

            _isMonitoring = true;
            _playbackStartTime = DateTime.Now;
            ResetMonitoringState();
            _monitoringCts = new CancellationTokenSource();

            // Subscribe to player events
            _mediaPlayer.Buffering += OnBuffering;
            _mediaPlayer.EncounteredError += OnError;
            _mediaPlayer.Stopped += OnStopped;
            _mediaPlayer.EndReached += OnEndReached;

            // Start background monitoring task with aggressive real-time adaptation
            _ = MonitorPlaybackQualityAsync(_monitoringCts.Token);

            LogOptimizer("Started real-time playback monitoring");
        }

        private static void LogOptimizer(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "optimizer.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitoringCts?.Cancel();

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Buffering -= OnBuffering;
                _mediaPlayer.EncounteredError -= OnError;
                _mediaPlayer.Stopped -= OnStopped;
                _mediaPlayer.EndReached -= OnEndReached;
            }
        }

        /// <summary>
        /// Attempt to reconnect to stream
        /// </summary>
        public async Task<bool> TryReconnectAsync(string streamUrl, LibVLC libVLC)
        {
            if (_mediaPlayer == null) return false;

            for (int attempt = 1; attempt <= _settings.ReconnectAttempts; attempt++)
            {
                OnReconnecting?.Invoke($"Reconnexion... Tentative {attempt}/{_settings.ReconnectAttempts}");

                try
                {
                    // Wait before retry
                    await Task.Delay(_settings.ReconnectDelay);

                    // Stop current playback
                    _mediaPlayer.Stop();

                    // Wait a bit
                    await Task.Delay(500);

                    // Try to reconnect
                    using var media = new Media(libVLC, new Uri(streamUrl));
                    _mediaPlayer.Play(media);

                    // Wait to see if it works
                    await Task.Delay(2000);

                    if (_mediaPlayer.IsPlaying)
                    {
                        OnStatusChanged?.Invoke("Reconnexion réussie");
                        _errorCount = 0;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    OnStatusChanged?.Invoke($"Échec tentative {attempt}: {ex.Message}");
                }
            }

            OnStatusChanged?.Invoke("Reconnexion échouée");
            return false;
        }

        #region Private Methods

        private async Task<ConnectionInfo> TestConnectionAsync(string streamUrl)
        {
            var info = new ConnectionInfo();
            var sw = Stopwatch.StartNew();

            try
            {
                // Test 1: Measure latency
                using var response = await _httpClient.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead);
                info.Latency = (int)sw.ElapsedMilliseconds;

                // Test 2: Download a small chunk to measure speed
                sw.Restart();
                var buffer = new byte[512 * 1024]; // 512KB test
                using var stream = await response.Content.ReadAsStreamAsync();
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                var downloadTime = sw.ElapsedMilliseconds;

                if (downloadTime > 0)
                {
                    info.DownloadSpeedMbps = (bytesRead * 8.0 / 1_000_000) / (downloadTime / 1000.0);
                }

                // Determine speed category
                info.SpeedCategory = info.DownloadSpeedMbps switch
                {
                    >= 50 => "Excellente",
                    >= 20 => "Très bonne",
                    >= 10 => "Bonne",
                    >= 5 => "Moyenne",
                    >= 2 => "Faible",
                    _ => "Très faible"
                };

                info.IsStable = info.Latency < 500 && info.DownloadSpeedMbps > 5;
            }
            catch
            {
                // If test fails, assume average connection
                info.Latency = 500;
                info.DownloadSpeedMbps = 5;
                info.SpeedCategory = "Inconnue";
                info.IsStable = false;
            }

            return info;
        }

        private PlayerSettings DetermineOptimalSettings(ConnectionInfo connection, ContentType contentType)
        {
            var settings = new PlayerSettings();

            // Adjust buffer based on connection quality
            if (connection.DownloadSpeedMbps >= 50)
            {
                // Excellent connection - minimal buffer for low latency
                settings.NetworkCaching = 2000;
                settings.LiveCaching = 1500;
                settings.FileCaching = 1500;
                settings.MinBufferBeforePlay = 15;
            }
            else if (connection.DownloadSpeedMbps >= 20)
            {
                // Good connection
                settings.NetworkCaching = 3000;
                settings.LiveCaching = 2500;
                settings.FileCaching = 2000;
                settings.MinBufferBeforePlay = 20;
            }
            else if (connection.DownloadSpeedMbps >= 10)
            {
                // Average connection
                settings.NetworkCaching = 5000;
                settings.LiveCaching = 4000;
                settings.FileCaching = 3000;
                settings.MinBufferBeforePlay = 30;
            }
            else if (connection.DownloadSpeedMbps >= 5)
            {
                // Slow connection
                settings.NetworkCaching = 8000;
                settings.LiveCaching = 6000;
                settings.FileCaching = 5000;
                settings.MinBufferBeforePlay = 40;
                settings.AdaptiveQuality = true;
            }
            else
            {
                // Very slow connection
                settings.NetworkCaching = 12000;
                settings.LiveCaching = 10000;
                settings.FileCaching = 8000;
                settings.MinBufferBeforePlay = 50;
                settings.AdaptiveQuality = true;
                settings.PreferredQuality = "low";
            }

            // Adjust for latency
            if (connection.Latency > 300)
            {
                settings.NetworkCaching = Math.Max(settings.NetworkCaching, connection.Latency * 5);
                settings.ConnectionTimeout = Math.Max(settings.ConnectionTimeout, connection.Latency * 10);
            }

            // Content type specific adjustments
            switch (contentType)
            {
                case ContentType.Live:
                    // Live TV needs faster response
                    settings.LiveCaching = Math.Max(2000, settings.NetworkCaching - 1000);
                    settings.SkipFramesOnLag = true;
                    break;

                case ContentType.Movie:
                    // Movies can buffer more for stability
                    settings.NetworkCaching = (int)(settings.NetworkCaching * 1.3);
                    settings.RememberPosition = true;
                    break;

                case ContentType.Series:
                    settings.RememberPosition = true;
                    settings.AutoPlayNext = true;
                    break;
            }

            // Always enable auto-reconnect for IPTV
            settings.AutoReconnect = true;
            settings.ReconnectAttempts = connection.IsStable ? 3 : 5;
            settings.ReconnectDelay = connection.IsStable ? 2000 : 3000;

            // Hardware acceleration - always try
            settings.HardwareAcceleration = true;
            settings.HardwareAccelerationType = "auto";

            return settings;
        }

        private async Task MonitorPlaybackQualityAsync(CancellationToken ct)
        {
            int checkIntervalMs = 2000; // Check every 2 seconds for faster response
            int adaptationCount = 0;

            while (!ct.IsCancellationRequested && _isMonitoring)
            {
                try
                {
                    await Task.Delay(checkIntervalMs, ct);
                    if (!_isMonitoring || _mediaPlayer == null) break;

                    // Calculate current health status
                    var previousHealth = CurrentHealth;
                    CurrentHealth = CalculateHealthStatus();

                    // Notify UI if health changed
                    if (previousHealth != CurrentHealth)
                    {
                        OnHealthStatusChanged?.Invoke(CurrentHealth);
                        LogOptimizer($"Health changed: {previousHealth} -> {CurrentHealth}");
                    }

                    // Clean old buffering events (keep only last 60 seconds)
                    var cutoff = DateTime.Now.AddSeconds(-MAX_BUFFERING_EVENTS_WINDOW);
                    while (_bufferingEvents.Count > 0 && _bufferingEvents.Peek() < cutoff)
                    {
                        _bufferingEvents.Dequeue();
                    }

                    // === REAL-TIME ADAPTATION LOGIC ===

                    // Level 0: First try quality downgrade (like YouTube auto quality)
                    // This is less disruptive than buffer changes
                    if (_bufferingCount >= 1 && _qualityDowngradeCount < 3 &&
                        CurrentQuality != StreamQuality.Low && _settings.AdaptiveQuality)
                    {
                        var newQuality = RequestQualityDowngrade();
                        if (newQuality.HasValue)
                        {
                            OnStatusChanged?.Invoke($"📉 Réduction qualité: {newQuality.Value.ToString()} pour économiser la bande passante");
                            _bufferingCount = 0;
                            adaptationCount++;
                            // Give the quality change time to take effect
                            await Task.Delay(3000, ct);
                            continue;
                        }
                    }

                    // Level 1: Frequent buffering - increase buffer slightly
                    if (_bufferingCount >= 2 && !_isRecovering)
                    {
                        var newBuffer = Math.Min(_settings.NetworkCaching + 1000, 10000);
                        if (newBuffer != _settings.NetworkCaching)
                        {
                            _settings.NetworkCaching = newBuffer;
                            _settings.LiveCaching = Math.Min(_settings.LiveCaching + 500, 8000);
                            OnStatusChanged?.Invoke($"⚠️ Buffering détecté - Augmentation buffer à {newBuffer}ms");
                            OnSettingsOptimized?.Invoke(_settings);
                            adaptationCount++;
                            LogOptimizer($"Level 1 adaptation: buffer increased to {newBuffer}ms");
                        }
                        _bufferingCount = 0;
                    }

                    // Level 2: Very frequent buffering (stalling) - try quality downgrade + buffer increase
                    if (_stallCount >= 2 || _bufferingEvents.Count >= 4)
                    {
                        // First, try another quality downgrade
                        if (CurrentQuality != StreamQuality.Low && _qualityDowngradeCount < 5)
                        {
                            RequestQualityDowngrade();
                            OnStatusChanged?.Invoke($"📉 Stall détecté - Passage en qualité {CurrentQuality}");
                        }

                        var newBuffer = Math.Min(_settings.NetworkCaching + 3000, 15000);
                        if (newBuffer != _settings.NetworkCaching && !_isRecovering)
                        {
                            _isRecovering = true;
                            _settings.NetworkCaching = newBuffer;
                            _settings.LiveCaching = Math.Min(_settings.LiveCaching + 2000, 12000);
                            _settings.SkipFramesOnLag = true;
                            OnStatusChanged?.Invoke($"⚡ Problème de flux - Optimisation agressive (buffer: {newBuffer}ms)");
                            OnSettingsOptimized?.Invoke(_settings);
                            adaptationCount++;
                            LogOptimizer($"Level 2 adaptation: aggressive buffer {newBuffer}ms, skip frames enabled");

                            // Trigger stream restart with new settings
                            OnRestartRequired?.Invoke();

                            _stallCount = 0;
                            _bufferingEvents.Clear();

                            // Wait before next adaptation
                            await Task.Delay(5000, ct);
                            _isRecovering = false;
                        }
                    }

                    // Level 3: Critical - multiple errors or complete stall, force restart
                    if (_errorCount >= 2 || CurrentHealth == PlaybackHealthStatus.Critical)
                    {
                        if (!_isRecovering && _libVLC != null && !string.IsNullOrEmpty(_currentStreamUrl))
                        {
                            _isRecovering = true;
                            LogOptimizer($"Level 3: Critical state - triggering reconnection");

                            // Increase settings to maximum stability
                            _settings.NetworkCaching = 15000;
                            _settings.LiveCaching = 12000;
                            _settings.SkipFramesOnLag = true;
                            _settings.ReconnectAttempts = 5;
                            OnSettingsOptimized?.Invoke(_settings);

                            OnStatusChanged?.Invoke("🔄 Reconnexion automatique en cours...");
                            var reconnected = await TryReconnectAsync(_currentStreamUrl, _libVLC);

                            if (reconnected)
                            {
                                OnStatusChanged?.Invoke("✅ Reconnexion réussie - Lecture stabilisée");
                                _errorCount = 0;
                                ResetMonitoringState();
                            }
                            else
                            {
                                OnStatusChanged?.Invoke("❌ Échec de la reconnexion");
                            }
                            _isRecovering = false;
                        }
                    }

                    // If stable for a while, gradually reduce buffer (optimization)
                    if (CurrentHealth == PlaybackHealthStatus.Excellent &&
                        _bufferingEvents.Count == 0 &&
                        (DateTime.Now - _playbackStartTime).TotalSeconds > 120 &&
                        adaptationCount == 0)
                    {
                        var newBuffer = Math.Max(_settings.NetworkCaching - 500, 3000);
                        if (newBuffer != _settings.NetworkCaching)
                        {
                            _settings.NetworkCaching = newBuffer;
                            OnStatusChanged?.Invoke($"📊 Flux stable - Optimisation (buffer: {newBuffer}ms)");
                            OnSettingsOptimized?.Invoke(_settings);
                            LogOptimizer($"Stable optimization: reduced buffer to {newBuffer}ms");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogOptimizer($"Monitoring error: {ex.Message}");
                }
            }
        }

        private PlaybackHealthStatus CalculateHealthStatus()
        {
            // Recent buffering events
            var recentBuffering = _bufferingEvents.Count;

            // Average buffer level
            if (_bufferHistory.Count > 0)
            {
                float sum = 0;
                foreach (var b in _bufferHistory) sum += b;
                _avgBufferLevel = sum / _bufferHistory.Count;
            }

            // Determine health based on multiple factors
            if (_errorCount > 0 || _avgBufferLevel < 20 || recentBuffering >= 5)
                return PlaybackHealthStatus.Critical;

            if (_avgBufferLevel < CRITICAL_BUFFER_THRESHOLD || recentBuffering >= 3 || _stallCount > 0)
                return PlaybackHealthStatus.Poor;

            if (_avgBufferLevel < WARNING_BUFFER_THRESHOLD || recentBuffering >= 2)
                return PlaybackHealthStatus.Fair;

            if (_avgBufferLevel < 80 || recentBuffering >= 1)
                return PlaybackHealthStatus.Good;

            return PlaybackHealthStatus.Excellent;
        }

        private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e)
        {
            // Track buffer level history
            _bufferHistory.Enqueue(e.Cache);
            while (_bufferHistory.Count > BUFFER_HISTORY_SIZE)
                _bufferHistory.Dequeue();

            // Track minimum buffer seen
            if (e.Cache < _minBufferSeen)
                _minBufferSeen = e.Cache;

            // Detect buffering states
            if (e.Cache < 100)
            {
                // Critical stall (playback blocked)
                if (e.Cache < 10)
                {
                    _stallCount++;
                    _lastStallTime = DateTime.Now;
                    _bufferingEvents.Enqueue(DateTime.Now);
                    LogOptimizer($"STALL detected: buffer at {e.Cache}%");
                }
                // Low buffer warning
                else if (e.Cache < 50)
                {
                    _bufferingCount++;
                    _lastBufferingTime = DateTime.Now;
                    _consecutiveLowBuffer++;

                    // If buffer has been low for multiple samples, record as event
                    if (_consecutiveLowBuffer >= 3)
                    {
                        _bufferingEvents.Enqueue(DateTime.Now);
                        _consecutiveLowBuffer = 0;
                    }
                }
                else
                {
                    _consecutiveLowBuffer = 0;
                }
            }
            else
            {
                // Buffer is full, reset low buffer counter
                _consecutiveLowBuffer = 0;
            }
        }

        private void OnError(object? sender, EventArgs e)
        {
            _errorCount++;
            OnStatusChanged?.Invoke("Erreur de lecture détectée");

            // If auto-reconnect is enabled, we'll handle this elsewhere
        }

        private void OnStopped(object? sender, EventArgs e)
        {
            StopMonitoring();
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            StopMonitoring();
        }

        #endregion

        public void Dispose()
        {
            StopMonitoring();
            _httpClient.Dispose();
            _monitoringCts?.Dispose();
        }
    }

    /// <summary>
    /// Connection test results
    /// </summary>
    public class ConnectionInfo
    {
        public int Latency { get; set; }
        public double DownloadSpeedMbps { get; set; }
        public string SpeedCategory { get; set; } = "Unknown";
        public bool IsStable { get; set; }
    }

    /// <summary>
    /// Stream quality analysis result
    /// </summary>
    public class StreamQualityInfo
    {
        public int Latency { get; set; }
        public double DownloadSpeed { get; set; }
        public int RecommendedBuffer { get; set; }
        public string QualityLevel { get; set; } = "";
        public ContentType ContentType { get; set; }
        public PlaybackHealthStatus HealthStatus { get; set; } = PlaybackHealthStatus.Unknown;

        public string GetSummary()
        {
            return $"Latence: {Latency}ms | Débit: {DownloadSpeed:F1} Mbps | Buffer recommandé: {RecommendedBuffer}ms";
        }
    }

    /// <summary>
    /// Real-time playback health status for UI display
    /// </summary>
    public enum PlaybackHealthStatus
    {
        Unknown = 0,
        Excellent = 1,  // No buffering, stable playback
        Good = 2,       // Occasional minor buffering
        Fair = 3,       // Some buffering events
        Poor = 4,       // Frequent buffering
        Critical = 5    // Severe issues, may need restart
    }

    /// <summary>
    /// Extension methods for PlaybackHealthStatus
    /// </summary>
    public static class PlaybackHealthStatusExtensions
    {
        /// <summary>
        /// Get display icon for health status
        /// </summary>
        public static string GetIcon(this PlaybackHealthStatus status)
        {
            return status switch
            {
                PlaybackHealthStatus.Excellent => "🟢",
                PlaybackHealthStatus.Good => "🟢",
                PlaybackHealthStatus.Fair => "🟡",
                PlaybackHealthStatus.Poor => "🟠",
                PlaybackHealthStatus.Critical => "🔴",
                _ => "⚪"
            };
        }

        /// <summary>
        /// Get display text for health status
        /// </summary>
        public static string GetDisplayText(this PlaybackHealthStatus status)
        {
            return status switch
            {
                PlaybackHealthStatus.Excellent => "Excellent",
                PlaybackHealthStatus.Good => "Bon",
                PlaybackHealthStatus.Fair => "Correct",
                PlaybackHealthStatus.Poor => "Mauvais",
                PlaybackHealthStatus.Critical => "Critique",
                _ => "Inconnu"
            };
        }

        /// <summary>
        /// Get color code for UI display (hex color)
        /// </summary>
        public static string GetColorCode(this PlaybackHealthStatus status)
        {
            return status switch
            {
                PlaybackHealthStatus.Excellent => "#00C853",  // Green
                PlaybackHealthStatus.Good => "#64DD17",       // Light green
                PlaybackHealthStatus.Fair => "#FFD600",       // Yellow
                PlaybackHealthStatus.Poor => "#FF6D00",       // Orange
                PlaybackHealthStatus.Critical => "#D50000",   // Red
                _ => "#9E9E9E"                                // Grey
            };
        }
    }
}
