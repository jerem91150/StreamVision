using System;
using System.ComponentModel;

namespace StreamVision.Models
{
    /// <summary>
    /// Subtitle customization settings
    /// </summary>
    public class SubtitleSettings : INotifyPropertyChanged
    {
        private bool _enabled = true;
        private int _fontSize = 24;
        private string _fontFamily = "Arial";
        private string _fontColor = "#FFFFFF";
        private string _backgroundColor = "#80000000";
        private string _outlineColor = "#000000";
        private int _outlineWidth = 2;
        private SubtitlePosition _position = SubtitlePosition.Bottom;
        private int _marginBottom = 50;
        private int _marginSide = 50;
        private bool _bold = false;
        private bool _italic = false;
        private double _opacity = 1.0;
        private SubtitleEncoding _encoding = SubtitleEncoding.UTF8;
        private int _delay = 0; // ms

        // Basic settings
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(nameof(Enabled)); }
        }

        public int FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnPropertyChanged(nameof(FontSize)); OnPropertyChanged(nameof(FontSizeDisplay)); }
        }

        public string FontSizeDisplay => $"{_fontSize}px";

        public string FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value; OnPropertyChanged(nameof(FontFamily)); }
        }

        public string FontColor
        {
            get => _fontColor;
            set { _fontColor = value; OnPropertyChanged(nameof(FontColor)); }
        }

        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); }
        }

        public string OutlineColor
        {
            get => _outlineColor;
            set { _outlineColor = value; OnPropertyChanged(nameof(OutlineColor)); }
        }

        public int OutlineWidth
        {
            get => _outlineWidth;
            set { _outlineWidth = value; OnPropertyChanged(nameof(OutlineWidth)); }
        }

        // Position
        public SubtitlePosition Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(nameof(Position)); }
        }

        public int MarginBottom
        {
            get => _marginBottom;
            set { _marginBottom = value; OnPropertyChanged(nameof(MarginBottom)); }
        }

        public int MarginSide
        {
            get => _marginSide;
            set { _marginSide = value; OnPropertyChanged(nameof(MarginSide)); }
        }

        // Style
        public bool Bold
        {
            get => _bold;
            set { _bold = value; OnPropertyChanged(nameof(Bold)); }
        }

        public bool Italic
        {
            get => _italic;
            set { _italic = value; OnPropertyChanged(nameof(Italic)); }
        }

        public double Opacity
        {
            get => _opacity;
            set { _opacity = value; OnPropertyChanged(nameof(Opacity)); OnPropertyChanged(nameof(OpacityPercent)); }
        }

        public int OpacityPercent => (int)(_opacity * 100);

        // Encoding & Sync
        public SubtitleEncoding Encoding
        {
            get => _encoding;
            set { _encoding = value; OnPropertyChanged(nameof(Encoding)); }
        }

        public int Delay
        {
            get => _delay;
            set { _delay = value; OnPropertyChanged(nameof(Delay)); OnPropertyChanged(nameof(DelayDisplay)); }
        }

        public string DelayDisplay => _delay == 0 ? "0" : $"{(_delay > 0 ? "+" : "")}{_delay}ms";

        /// <summary>
        /// Get LibVLC subtitle options
        /// </summary>
        public string[] ToLibVLCOptions()
        {
            var options = new System.Collections.Generic.List<string>();

            if (!Enabled)
            {
                options.Add("--no-sub-autodetect-file");
                return options.ToArray();
            }

            // Font
            options.Add($"--freetype-font={FontFamily}");
            options.Add($"--freetype-fontsize={FontSize}");
            options.Add($"--freetype-color={ColorToVLC(FontColor)}");
            options.Add($"--freetype-background-color={ColorToVLC(BackgroundColor)}");
            options.Add($"--freetype-outline-color={ColorToVLC(OutlineColor)}");
            options.Add($"--freetype-outline-thickness={OutlineWidth}");

            if (Bold) options.Add("--freetype-bold");
            if (Italic) options.Add("--freetype-italic");

            // Position
            options.Add($"--sub-margin={MarginBottom}");

            // Delay
            if (Delay != 0)
            {
                options.Add($"--sub-delay={Delay}");
            }

            return options.ToArray();
        }

        /// <summary>
        /// Apply to a media player
        /// </summary>
        public void ApplyToPlayer(LibVLCSharp.Shared.MediaPlayer player)
        {
            if (player == null) return;

            // Apply delay
            player.SetSpuDelay(Delay * 1000); // VLC uses microseconds

            // Note: Most styling requires media options at creation time
            // or using ASS/SSA subtitle styling
        }

        /// <summary>
        /// Get preset settings
        /// </summary>
        public static SubtitleSettings GetPreset(string presetName)
        {
            return presetName switch
            {
                "default" => new SubtitleSettings(),
                "large" => new SubtitleSettings
                {
                    FontSize = 32,
                    Bold = true,
                    OutlineWidth = 3
                },
                "cinema" => new SubtitleSettings
                {
                    FontSize = 28,
                    FontColor = "#FFFF00",
                    BackgroundColor = "#00000000",
                    OutlineWidth = 2,
                    MarginBottom = 80
                },
                "minimal" => new SubtitleSettings
                {
                    FontSize = 20,
                    BackgroundColor = "#00000000",
                    OutlineWidth = 1
                },
                "accessibility" => new SubtitleSettings
                {
                    FontSize = 36,
                    Bold = true,
                    FontColor = "#FFFFFF",
                    BackgroundColor = "#CC000000",
                    OutlineWidth = 0
                },
                _ => new SubtitleSettings()
            };
        }

        private static int ColorToVLC(string hexColor)
        {
            // VLC uses BGR format for colors
            if (hexColor.StartsWith("#"))
                hexColor = hexColor.Substring(1);

            if (hexColor.Length >= 6)
            {
                var r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                var g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                var b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                return (b << 16) | (g << 8) | r;
            }

            return 0xFFFFFF;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Subtitle position on screen
    /// </summary>
    public enum SubtitlePosition
    {
        Top,
        TopLeft,
        TopRight,
        Center,
        Bottom,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// Subtitle file encoding
    /// </summary>
    public enum SubtitleEncoding
    {
        UTF8,
        Latin1,
        Windows1252,
        ISO88591,
        Auto
    }

    /// <summary>
    /// Available font colors
    /// </summary>
    public static class SubtitleColors
    {
        public static readonly (string Name, string Hex)[] FontColors = new[]
        {
            ("Blanc", "#FFFFFF"),
            ("Jaune", "#FFFF00"),
            ("Cyan", "#00FFFF"),
            ("Vert", "#00FF00"),
            ("Magenta", "#FF00FF"),
            ("Rouge", "#FF0000"),
            ("Orange", "#FFA500")
        };

        public static readonly (string Name, string Hex)[] BackgroundColors = new[]
        {
            ("Transparent", "#00000000"),
            ("Noir 25%", "#40000000"),
            ("Noir 50%", "#80000000"),
            ("Noir 75%", "#BF000000"),
            ("Noir 100%", "#FF000000")
        };
    }
}
