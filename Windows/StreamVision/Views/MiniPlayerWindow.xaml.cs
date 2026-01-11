using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace StreamVision.Views
{
    /// <summary>
    /// Floating mini player window (Picture-in-Picture)
    /// </summary>
    public partial class MiniPlayerWindow : Window
    {
        private MediaPlayer? _mediaPlayer;
        private readonly DispatcherTimer _hideControlsTimer;
        private bool _isDragging;

        // Events
        public event Action? OnExpandRequested;
        public event Action? OnCloseRequested;
        public event Action? OnPlayPauseRequested;
        public event Action<int>? OnVolumeChanged;
        public event Action? OnMuteRequested;

        public MiniPlayerWindow()
        {
            InitializeComponent();

            // Timer to auto-hide controls
            _hideControlsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _hideControlsTimer.Tick += (s, e) =>
            {
                if (!_isDragging)
                {
                    ControlsOverlay.Visibility = Visibility.Collapsed;
                }
                _hideControlsTimer.Stop();
            };

            // Position at bottom-right of screen
            Loaded += (s, e) =>
            {
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 20;
                Top = workArea.Bottom - Height - 20;
            };
        }

        /// <summary>
        /// Set the media player to display
        /// </summary>
        public void SetMediaPlayer(MediaPlayer mediaPlayer)
        {
            _mediaPlayer = mediaPlayer;
            MiniVideoView.MediaPlayer = mediaPlayer;

            // Subscribe to events
            _mediaPlayer.TimeChanged += OnTimeChanged;
            _mediaPlayer.Playing += (s, e) => Dispatcher.Invoke(() => PlayPauseIcon.Text = "⏸");
            _mediaPlayer.Paused += (s, e) => Dispatcher.Invoke(() => PlayPauseIcon.Text = "▶");
            _mediaPlayer.Stopped += (s, e) => Dispatcher.Invoke(() => PlayPauseIcon.Text = "▶");

            // Set initial volume
            VolumeSlider.Value = mediaPlayer.Volume;
        }

        /// <summary>
        /// Set the title to display
        /// </summary>
        public void SetTitle(string title)
        {
            TitleText.Text = title;
        }

        /// <summary>
        /// Update play/pause icon
        /// </summary>
        public void UpdatePlayState(bool isPlaying)
        {
            PlayPauseIcon.Text = isPlaying ? "⏸" : "▶";
        }

        #region Event Handlers

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var time = TimeSpan.FromMilliseconds(e.Time);
                TimeText.Text = $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
            });
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            ControlsOverlay.Visibility = Visibility.Visible;
            _hideControlsTimer.Stop();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            _hideControlsTimer.Start();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click to expand
                Expand_Click(sender, e);
            }
            else
            {
                // Drag window
                _isDragging = true;
                DragMove();
                _isDragging = false;
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            OnPlayPauseRequested?.Invoke();
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            OnMuteRequested?.Invoke();

            // Update icon
            if (_mediaPlayer != null)
            {
                VolumeIcon.Text = _mediaPlayer.Mute ? "🔇" : "🔊";
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            OnVolumeChanged?.Invoke((int)e.NewValue);
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            OnExpandRequested?.Invoke();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            OnCloseRequested?.Invoke();
            Close();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.TimeChanged -= OnTimeChanged;
            }
            base.OnClosed(e);
        }
    }
}
