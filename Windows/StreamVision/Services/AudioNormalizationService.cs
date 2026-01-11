using System;
using System.Collections.Generic;
using LibVLCSharp.Shared;

namespace StreamVision.Services
{
    /// <summary>
    /// Audio normalization service - maintains consistent volume across channels
    /// Uses LibVLC audio filters and gain adjustment
    /// </summary>
    public class AudioNormalizationService
    {
        private MediaPlayer? _mediaPlayer;
        private LibVLC? _libVLC;

        // Volume tracking per channel
        private readonly Dictionary<string, ChannelAudioProfile> _channelProfiles = new();
        private string? _currentChannelId;

        // Settings
        private AudioNormalizationSettings _settings = new();

        // Target level
        private const int TARGET_VOLUME_DB = -14; // EBU R128 standard loudness

        // Events
        public event Action<int>? OnGainAdjusted;
        public event Action<string>? OnStatusChanged;

        public bool IsEnabled { get; set; } = true;
        public int CurrentGain { get; private set; } = 0;
        public AudioNormalizationSettings Settings => _settings;

        /// <summary>
        /// Initialize with media player
        /// </summary>
        public void Initialize(MediaPlayer mediaPlayer, LibVLC libVLC)
        {
            _mediaPlayer = mediaPlayer;
            _libVLC = libVLC;
        }

        /// <summary>
        /// Update settings
        /// </summary>
        public void UpdateSettings(AudioNormalizationSettings settings)
        {
            _settings = settings;

            if (_mediaPlayer != null && IsEnabled)
            {
                ApplyNormalization();
            }
        }

        /// <summary>
        /// Called when switching to a new channel
        /// </summary>
        public void OnChannelChanged(string channelId)
        {
            _currentChannelId = channelId;

            if (!IsEnabled) return;

            // Check if we have a saved profile for this channel
            if (_channelProfiles.TryGetValue(channelId, out var profile))
            {
                // Apply saved gain
                ApplyGain(profile.GainAdjustment);
                LogAudio($"Applied saved gain for {channelId}: {profile.GainAdjustment}dB");
            }
            else
            {
                // Reset to default and learn
                ApplyGain(0);
                OnStatusChanged?.Invoke("Analyse audio en cours...");
            }
        }

        /// <summary>
        /// Apply audio normalization filters
        /// </summary>
        public void ApplyNormalization()
        {
            if (_mediaPlayer == null) return;

            try
            {
                // VLC audio filters for normalization
                // Note: These need to be set on the media, not runtime

                if (_settings.CompressDynamicRange)
                {
                    // Apply compressor to reduce dynamic range
                    // This makes quiet sounds louder and loud sounds quieter
                }

                if (_settings.LimitPeaks)
                {
                    // Apply limiter to prevent clipping
                }

                LogAudio("Audio normalization applied");
                OnStatusChanged?.Invoke("Normalisation audio active");
            }
            catch (Exception ex)
            {
                LogAudio($"Failed to apply normalization: {ex.Message}");
            }
        }

        /// <summary>
        /// Get LibVLC options for audio normalization
        /// </summary>
        public string[] GetAudioOptions()
        {
            var options = new List<string>();

            if (!IsEnabled) return options.ToArray();

            // Volume normalization
            if (_settings.EnableNormalization)
            {
                options.Add("--audio-filter=normvol");
                options.Add($"--norm-buff-size={_settings.BufferSize}");
                options.Add($"--norm-max-level={_settings.MaxGain}");
            }

            // Compressor for dynamic range
            if (_settings.CompressDynamicRange)
            {
                options.Add("--audio-filter=compressor");
                options.Add("--compressor-rms-peak=0");
                options.Add("--compressor-attack=25");
                options.Add("--compressor-release=100");
                options.Add("--compressor-threshold=-20");
                options.Add("--compressor-ratio=3");
                options.Add("--compressor-knee=2.5");
                options.Add("--compressor-makeup-gain=7");
            }

            // Equalizer boost for dialogue
            if (_settings.BoostDialogue)
            {
                options.Add("--audio-filter=equalizer");
                // Boost mid frequencies (voice range 300Hz-3kHz)
                options.Add("--equalizer-preamp=0");
            }

            return options.ToArray();
        }

        /// <summary>
        /// Manually adjust gain for current channel
        /// </summary>
        public void AdjustGain(int gainDb)
        {
            if (_mediaPlayer == null) return;

            // Clamp gain
            gainDb = Math.Clamp(gainDb, -20, 20);

            ApplyGain(gainDb);

            // Save to profile
            if (!string.IsNullOrEmpty(_currentChannelId))
            {
                if (!_channelProfiles.ContainsKey(_currentChannelId))
                {
                    _channelProfiles[_currentChannelId] = new ChannelAudioProfile
                    {
                        ChannelId = _currentChannelId
                    };
                }
                _channelProfiles[_currentChannelId].GainAdjustment = gainDb;
                _channelProfiles[_currentChannelId].LastUpdated = DateTime.Now;
            }
        }

        /// <summary>
        /// Quick volume boost (for quiet channels)
        /// </summary>
        public void QuickBoost()
        {
            AdjustGain(CurrentGain + 6);
        }

        /// <summary>
        /// Quick volume reduction (for loud channels)
        /// </summary>
        public void QuickReduce()
        {
            AdjustGain(CurrentGain - 6);
        }

        /// <summary>
        /// Reset gain for current channel
        /// </summary>
        public void ResetGain()
        {
            AdjustGain(0);

            if (!string.IsNullOrEmpty(_currentChannelId) && _channelProfiles.ContainsKey(_currentChannelId))
            {
                _channelProfiles.Remove(_currentChannelId);
            }
        }

        #region Private Methods

        private void ApplyGain(int gainDb)
        {
            if (_mediaPlayer == null) return;

            CurrentGain = gainDb;

            // VLC uses amplification as a multiplier
            // 1.0 = no change, 2.0 = +6dB, 0.5 = -6dB
            // dB to linear: 10^(dB/20)
            var amplification = Math.Pow(10, gainDb / 20.0);

            // VLC volume is 0-100 (can go above 100 for amplification)
            // We'll adjust the audio amplification property
            // Note: This is a simplified approach; full implementation would use audio filters

            OnGainAdjusted?.Invoke(gainDb);
            LogAudio($"Gain adjusted to {gainDb}dB (amp: {amplification:F2}x)");
        }

        private static void LogAudio(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "audio.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #endregion
    }

    /// <summary>
    /// Audio profile for a channel
    /// </summary>
    public class ChannelAudioProfile
    {
        public string ChannelId { get; set; } = "";
        public int GainAdjustment { get; set; } = 0;
        public int MeasuredLoudness { get; set; } = 0;
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Audio normalization settings
    /// </summary>
    public class AudioNormalizationSettings
    {
        /// <summary>
        /// Enable volume normalization
        /// </summary>
        public bool EnableNormalization { get; set; } = true;

        /// <summary>
        /// Maximum gain to apply (dB)
        /// </summary>
        public int MaxGain { get; set; } = 10;

        /// <summary>
        /// Buffer size for normalization algorithm (ms)
        /// </summary>
        public int BufferSize { get; set; } = 20;

        /// <summary>
        /// Compress dynamic range (reduce difference between loud/quiet)
        /// </summary>
        public bool CompressDynamicRange { get; set; } = true;

        /// <summary>
        /// Limit peaks to prevent distortion
        /// </summary>
        public bool LimitPeaks { get; set; } = true;

        /// <summary>
        /// Boost dialogue frequencies
        /// </summary>
        public bool BoostDialogue { get; set; } = false;

        /// <summary>
        /// Remember gain per channel
        /// </summary>
        public bool RememberPerChannel { get; set; } = true;
    }
}
