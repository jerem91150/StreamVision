using System;
using System.Windows;
using System.Windows.Input;
using LibVLCSharp.Shared;
using StreamVision.Models;
using StreamVision.ViewModels;

namespace StreamVision.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _isFullScreen;
        private WindowState _previousWindowState;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();

            // Assigner le MediaPlayer aux VideoViews
            if (_viewModel.MediaPlayer != null)
            {
                MainVideoView.MediaPlayer = _viewModel.MediaPlayer;
                MiniVideoView.MediaPlayer = _viewModel.MediaPlayer;
                FullscreenVideoView.MediaPlayer = _viewModel.MediaPlayer;
            }
        }

        // Custom title bar - drag to move window
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Maximize_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        // Window control buttons
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Channel card click
        private void Channel_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Channel channel)
            {
                _viewModel.CurrentChannel = channel;
                _viewModel.PlayChannelCommand.Execute(channel);
            }
        }

        // Category click
        private void Category_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string category)
            {
                _viewModel.FilterByCategory(category);
            }
        }

        // Exit fullscreen mode
        private void ExitFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (_isFullScreen)
            {
                ToggleFullScreen();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F11:
                case Key.F:
                    ToggleFullScreen();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (_isFullScreen)
                    {
                        ToggleFullScreen();
                        e.Handled = true;
                    }
                    break;
                case Key.Space:
                    _viewModel.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.M:
                    _viewModel.ToggleMuteCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _viewModel.PreviousChannelCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Down:
                    _viewModel.NextChannelCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        private void ToggleFullScreen()
        {
            if (_isFullScreen)
            {
                // Exit fullscreen
                WindowState = _previousWindowState;
                FullscreenPlayer.Visibility = Visibility.Collapsed;
                MainScrollViewer.Visibility = Visibility.Visible;
                _isFullScreen = false;
            }
            else
            {
                // Enter fullscreen
                _previousWindowState = WindowState;
                WindowState = WindowState.Maximized;
                FullscreenPlayer.Visibility = Visibility.Visible;
                MainScrollViewer.Visibility = Visibility.Collapsed;
                // VLC MediaPlayer binding removed
                _isFullScreen = true;
            }

            _viewModel.IsFullScreen = _isFullScreen;
        }

        private void AddPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddPlaylistDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                switch (dialog.SelectedType)
                {
                    case "M3U":
                        _viewModel.AddM3UPlaylistCommand.Execute(dialog.PlaylistUrl);
                        break;
                    case "Xtream":
                        _viewModel.AddXtreamPlaylistCommand.Execute(
                            (dialog.PlaylistUrl, dialog.Username, dialog.Password));
                        break;
                }
            }
        }

        // EPG functionality moved to HomeWindow
        // private void ShowEpg_Click(object sender, RoutedEventArgs e) { }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
