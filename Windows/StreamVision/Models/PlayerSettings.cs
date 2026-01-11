using System;

namespace StreamVision.Models
{
    /// <summary>
    /// Advanced player settings for optimal IPTV streaming experience
    /// </summary>
    public class PlayerSettings
    {
        public string Id { get; set; } = "default";

        #region Buffer Settings

        /// <summary>
        /// Network caching in milliseconds (higher = more stable, more delay)
        /// Range: 500-30000ms, Default: 3000ms
        /// </summary>
        public int NetworkCaching { get; set; } = 3000;

        /// <summary>
        /// Live stream caching in milliseconds
        /// Range: 500-10000ms, Default: 3000ms
        /// </summary>
        public int LiveCaching { get; set; } = 3000;

        /// <summary>
        /// File caching for VOD content in milliseconds
        /// Range: 500-10000ms, Default: 1500ms
        /// </summary>
        public int FileCaching { get; set; } = 1500;

        /// <summary>
        /// Disc caching in milliseconds
        /// Range: 500-10000ms, Default: 1500ms
        /// </summary>
        public int DiscCaching { get; set; } = 1500;

        #endregion

        #region Decoding Settings

        /// <summary>
        /// Enable hardware acceleration (GPU decoding)
        /// Can reduce CPU usage but may cause issues on some systems
        /// </summary>
        public bool HardwareAcceleration { get; set; } = true;

        /// <summary>
        /// Hardware acceleration type
        /// Options: auto, d3d11va, dxva2, none
        /// </summary>
        public string HardwareAccelerationType { get; set; } = "auto";

        /// <summary>
        /// Skip frames when running late to maintain sync
        /// </summary>
        public bool SkipFramesOnLag { get; set; } = true;

        /// <summary>
        /// Number of threads for decoding (0 = auto)
        /// </summary>
        public int DecodingThreads { get; set; } = 0;

        /// <summary>
        /// Enable fast seek (less accurate but faster)
        /// </summary>
        public bool FastSeek { get; set; } = true;

        #endregion

        #region Network Settings

        /// <summary>
        /// Connection timeout in milliseconds
        /// Range: 5000-60000ms, Default: 10000ms
        /// </summary>
        public int ConnectionTimeout { get; set; } = 10000;

        /// <summary>
        /// Read timeout in milliseconds
        /// Range: 5000-60000ms, Default: 15000ms
        /// </summary>
        public int ReadTimeout { get; set; } = 15000;

        /// <summary>
        /// Enable automatic reconnection on stream failure
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Number of reconnection attempts
        /// Range: 1-10, Default: 3
        /// </summary>
        public int ReconnectAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between reconnection attempts in milliseconds
        /// Range: 1000-10000ms, Default: 2000ms
        /// </summary>
        public int ReconnectDelay { get; set; } = 2000;

        /// <summary>
        /// Custom User-Agent string (empty = default VLC)
        /// </summary>
        public string UserAgent { get; set; } = "VLC/3.0.20 LibVLC/3.0.20";

        /// <summary>
        /// Custom HTTP Referrer (empty = none)
        /// </summary>
        public string HttpReferrer { get; set; } = "";

        /// <summary>
        /// Force HTTP instead of HTTPS when possible
        /// Can help with some problematic servers
        /// </summary>
        public bool ForceHttp { get; set; } = false;

        /// <summary>
        /// Prefer IPv4 over IPv6
        /// </summary>
        public bool PreferIPv4 { get; set; } = true;

        #endregion

        #region Audio Settings

        /// <summary>
        /// Audio desynchronization compensation in milliseconds
        /// Range: -5000 to 5000ms, Default: 0
        /// </summary>
        public int AudioDelay { get; set; } = 0;

        /// <summary>
        /// Audio resampling quality (0 = fast, 4 = best)
        /// </summary>
        public int AudioResamplingQuality { get; set; } = 2;

        /// <summary>
        /// Enable audio time stretching (maintains pitch when speed changes)
        /// </summary>
        public bool AudioTimeStretch { get; set; } = true;

        /// <summary>
        /// Audio output module (empty = auto)
        /// Options: directsound, wasapi, waveout
        /// </summary>
        public string AudioOutput { get; set; } = "";

        /// <summary>
        /// Volume boost percentage (0-200%)
        /// </summary>
        public int VolumeBoost { get; set; } = 100;

        /// <summary>
        /// Normalize audio levels
        /// </summary>
        public bool AudioNormalize { get; set; } = false;

        #endregion

        #region Video Settings

        /// <summary>
        /// Subtitle delay in milliseconds
        /// Range: -5000 to 5000ms, Default: 0
        /// </summary>
        public int SubtitleDelay { get; set; } = 0;

        /// <summary>
        /// Deinterlace mode
        /// Options: auto, off, on
        /// </summary>
        public string Deinterlace { get; set; } = "auto";

        /// <summary>
        /// Deinterlace algorithm
        /// Options: blend, bob, discard, linear, mean, x, yadif, yadif2x
        /// </summary>
        public string DeinterlaceMode { get; set; } = "yadif";

        /// <summary>
        /// Video output module (empty = auto)
        /// Options: d3d11, d3d9, gl, directdraw
        /// </summary>
        public string VideoOutput { get; set; } = "";

        /// <summary>
        /// Enable post-processing filters
        /// </summary>
        public bool PostProcessing { get; set; } = false;

        /// <summary>
        /// Post-processing quality (1-6, higher = better quality, more CPU)
        /// </summary>
        public int PostProcessingQuality { get; set; } = 3;

        #endregion

        #region Stream Quality Settings

        /// <summary>
        /// Preferred stream quality
        /// Options: auto, low, medium, high, best
        /// </summary>
        public string PreferredQuality { get; set; } = "auto";

        /// <summary>
        /// Adaptive quality - automatically reduce quality on poor connection
        /// </summary>
        public bool AdaptiveQuality { get; set; } = true;

        /// <summary>
        /// Minimum buffer percentage before playback starts
        /// Range: 0-100%, Default: 20%
        /// </summary>
        public int MinBufferBeforePlay { get; set; } = 20;

        #endregion

        #region Playback Settings

        /// <summary>
        /// Default playback speed (1.0 = normal)
        /// </summary>
        public float DefaultSpeed { get; set; } = 1.0f;

        /// <summary>
        /// Remember playback position for VOD
        /// </summary>
        public bool RememberPosition { get; set; } = true;

        /// <summary>
        /// Auto-play next episode
        /// </summary>
        public bool AutoPlayNext { get; set; } = true;

        /// <summary>
        /// Skip intro automatically (seconds, 0 = disabled)
        /// </summary>
        public int SkipIntroSeconds { get; set; } = 0;

        /// <summary>
        /// Skip outro/credits (seconds before end, 0 = disabled)
        /// </summary>
        public int SkipOutroSeconds { get; set; } = 0;

        #endregion

        #region Presets

        /// <summary>
        /// Apply preset settings for common scenarios
        /// </summary>
        public static PlayerSettings GetPreset(string preset)
        {
            return preset switch
            {
                "stable" => new PlayerSettings
                {
                    NetworkCaching = 5000,
                    LiveCaching = 5000,
                    FileCaching = 3000,
                    HardwareAcceleration = true,
                    SkipFramesOnLag = true,
                    AutoReconnect = true,
                    ReconnectAttempts = 5,
                    MinBufferBeforePlay = 30,
                    AdaptiveQuality = true
                },
                "lowlatency" => new PlayerSettings
                {
                    NetworkCaching = 1000,
                    LiveCaching = 1000,
                    FileCaching = 500,
                    HardwareAcceleration = true,
                    SkipFramesOnLag = true,
                    FastSeek = true,
                    MinBufferBeforePlay = 10
                },
                "quality" => new PlayerSettings
                {
                    NetworkCaching = 8000,
                    LiveCaching = 8000,
                    FileCaching = 5000,
                    HardwareAcceleration = true,
                    SkipFramesOnLag = false,
                    DeinterlaceMode = "yadif2x",
                    PostProcessing = true,
                    PostProcessingQuality = 5,
                    MinBufferBeforePlay = 40
                },
                "slowconnection" => new PlayerSettings
                {
                    NetworkCaching = 10000,
                    LiveCaching = 10000,
                    FileCaching = 5000,
                    HardwareAcceleration = true,
                    SkipFramesOnLag = true,
                    AutoReconnect = true,
                    ReconnectAttempts = 5,
                    ReconnectDelay = 3000,
                    ConnectionTimeout = 30000,
                    ReadTimeout = 30000,
                    AdaptiveQuality = true,
                    MinBufferBeforePlay = 50,
                    PreferredQuality = "medium"
                },
                "compatibility" => new PlayerSettings
                {
                    NetworkCaching = 5000,
                    LiveCaching = 5000,
                    HardwareAcceleration = false,
                    HardwareAccelerationType = "none",
                    VideoOutput = "d3d9",
                    AudioOutput = "directsound",
                    ForceHttp = true,
                    PreferIPv4 = true
                },
                _ => new PlayerSettings() // default
            };
        }

        #endregion

        /// <summary>
        /// Generate LibVLC options array from settings
        /// </summary>
        public string[] ToLibVLCOptions()
        {
            var options = new System.Collections.Generic.List<string>
            {
                $"--network-caching={NetworkCaching}",
                $"--live-caching={LiveCaching}",
                $"--file-caching={FileCaching}",
                $"--disc-caching={DiscCaching}"
            };

            // Hardware acceleration
            if (HardwareAcceleration)
            {
                options.Add($"--avcodec-hw={HardwareAccelerationType}");
            }
            else
            {
                options.Add("--avcodec-hw=none");
            }

            // Skip frames
            if (SkipFramesOnLag)
            {
                options.Add("--avcodec-skip-frame=1");
                options.Add("--avcodec-skip-idct=1");
            }

            // Decoding threads
            if (DecodingThreads > 0)
            {
                options.Add($"--avcodec-threads={DecodingThreads}");
            }

            // Fast seek
            if (FastSeek)
            {
                options.Add("--avcodec-fast");
            }

            // Network options
            options.Add($"--http-reconnect");
            if (PreferIPv4)
            {
                options.Add("--ipv4");
            }

            // User agent
            if (!string.IsNullOrEmpty(UserAgent))
            {
                options.Add($"--http-user-agent={UserAgent}");
            }

            // Referrer
            if (!string.IsNullOrEmpty(HttpReferrer))
            {
                options.Add($"--http-referrer={HttpReferrer}");
            }

            // Audio options
            if (AudioTimeStretch)
            {
                options.Add("--audio-time-stretch");
            }

            if (!string.IsNullOrEmpty(AudioOutput))
            {
                options.Add($"--aout={AudioOutput}");
            }

            if (AudioNormalize)
            {
                options.Add("--audio-filter=normvol");
            }

            // Video options
            if (!string.IsNullOrEmpty(VideoOutput))
            {
                options.Add($"--vout={VideoOutput}");
            }

            if (Deinterlace != "off")
            {
                options.Add($"--deinterlace={Deinterlace}");
                options.Add($"--deinterlace-mode={DeinterlaceMode}");
            }

            if (PostProcessing)
            {
                options.Add($"--postproc-q={PostProcessingQuality}");
            }

            // General
            options.Add("--no-sub-autodetect-file");
            options.Add("--no-video-title-show");

            return options.ToArray();
        }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
