using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LibVLCSharp.Shared;
using StreamVision.Models;
using StreamVision.Services;
using StreamVision.ViewModels;
using Forms = System.Windows.Forms;

namespace StreamVision.Views
{
    public partial class HomeWindow : Window
    {
        private readonly HomeViewModel _viewModel;
        private readonly ParentalControlService _parentalControl;
        private bool _isFullScreen;
        private WindowState _previousWindowState;
        private Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting = false;

        public HomeWindow()
        {
            LogInfo("HomeWindow constructor starting...");
            try
            {
                InitializeComponent();
                LogInfo("InitializeComponent done");
                _viewModel = new HomeViewModel();
                LogInfo("ViewModel created");
                DataContext = _viewModel;
                LogInfo("DataContext set");

                _parentalControl = new ParentalControlService();
                LogInfo("Parental control service created");

                InitializeSystemTray();
                LogInfo("System tray initialized");
            }
            catch (Exception ex)
            {
                LogInfo($"ERROR in constructor: {ex}");
                throw;
            }
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "StreamVision",
                Visible = true
            };

            // Create icon from embedded resource or default
            try
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            // Context menu
            var contextMenu = new Forms.ContextMenuStrip();

            var showItem = new Forms.ToolStripMenuItem("Afficher StreamVision");
            showItem.Click += (s, e) => ShowFromTray();
            showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(showItem);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var playPauseItem = new Forms.ToolStripMenuItem("Lecture/Pause");
            playPauseItem.Click += (s, e) => Dispatcher.Invoke(() => _viewModel.PlayPauseCommand.Execute(null));
            contextMenu.Items.Add(playPauseItem);

            var stopItem = new Forms.ToolStripMenuItem("Arrêter");
            stopItem.Click += (s, e) => Dispatcher.Invoke(() => _viewModel.StopCommand.Execute(null));
            contextMenu.Items.Add(stopItem);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var exitItem = new Forms.ToolStripMenuItem("Quitter");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double-click to show window
            _notifyIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void ShowFromTray()
        {
            Dispatcher.Invoke(() =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                this.Focus();
            });
        }

        private void ExitApplication()
        {
            _isExiting = true;
            Dispatcher.Invoke(() =>
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
                Close();
            });
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            // Minimize to tray
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                _notifyIcon?.ShowBalloonTip(1000, "StreamVision", "L'application continue en arrière-plan", Forms.ToolTipIcon.Info);
            }
        }

        private static void LogInfo(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "startup.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] HomeWindow: {message}\n");
            }
            catch { }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogInfo("Window_Loaded starting...");
            try
            {
                // Load parental control settings
                await _parentalControl.LoadSettingsAsync();
                LogInfo($"Parental control enabled: {_parentalControl.IsEnabled}");

                LogInfo("Calling InitializeAsync...");
                await _viewModel.InitializeAsync();
                LogInfo("InitializeAsync completed");

                // Show onboarding on first launch
                if (!_viewModel.OnboardingCompleted)
                {
                    LogInfo("First launch detected - showing onboarding");
                    await ShowOnboardingAsync();
                }

                if (_viewModel.MediaPlayer != null)
                {
                    LogInfo("Assigning MediaPlayer to VideoView...");
                    MiniVideoView.MediaPlayer = _viewModel.MediaPlayer;
                    LogInfo("MediaPlayer assigned");

                    // S'abonner à l'événement de lecture pour afficher le player
                    _viewModel.MediaPlayer.Playing += (s, e) => Dispatcher.Invoke(() => {
                        PlayerContainer.Visibility = Visibility.Visible;
                    });
                    _viewModel.MediaPlayer.Stopped += (s, e) => Dispatcher.Invoke(() => {
                        if (!_isFullScreen)
                        {
                            PlayerContainer.Visibility = Visibility.Collapsed;
                        }
                    });
                }
                else
                {
                    LogInfo("MediaPlayer is null, skipping VideoView assignment");
                }

                // Force window to center on screen
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                this.Left = (screenWidth - this.Width) / 2;
                this.Top = (screenHeight - this.Height) / 2;

                // Force window visibility
                this.Visibility = Visibility.Visible;
                this.Show();
                this.Activate();
                this.Focus();
                LogInfo($"Window shown - State: {WindowState}, Visibility: {Visibility}, Size: {ActualWidth}x{ActualHeight}, Position: {Left},{Top}, Screen: {screenWidth}x{screenHeight}");
                LogInfo("Window_Loaded completed successfully");
            }
            catch (Exception ex)
            {
                LogInfo($"ERROR in Window_Loaded: {ex}");
                MessageBox.Show($"Initialization error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Title Bar

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

        #endregion

        #region Media Card Clicks

        private void MediaCard_Click(object sender, MouseButtonEventArgs e)
        {
            LogInfo("MediaCard_Click called");
            LogInfo($"  sender type: {sender?.GetType().Name}");

            if (sender is FrameworkElement element)
            {
                LogInfo($"  element.Tag type: {element.Tag?.GetType().Name}");
                LogInfo($"  element.Tag is MediaItem: {element.Tag is MediaItem}");

                if (element.Tag is MediaItem item)
                {
                    LogInfo($"  Item: {item.Name}, Type: {item.MediaType}");
                    LogInfo($"  StreamUrl: {item.StreamUrl}");

                    // Check parental control
                    if (_parentalControl.IsEnabled && _parentalControl.IsContentBlocked(item))
                    {
                        LogInfo($"  Content blocked by parental control: {item.Name}");
                        if (!_parentalControl.RequestAccess(this))
                        {
                            LogInfo("  Access denied - PIN incorrect or cancelled");
                            return;
                        }
                        LogInfo("  Access granted after PIN verification");
                    }

                    // For Movies and Series, show detail window first
                    if (item.MediaType == ContentType.Movie || item.MediaType == ContentType.Series)
                    {
                        LogInfo("  Opening detail window for Movie/Series");
                        var detailWindow = new MediaDetailWindow(item, _viewModel);
                        detailWindow.Owner = this;
                        detailWindow.ShowDialog();
                    }
                    else
                    {
                        // For Live TV, play directly
                        LogInfo("  Playing Live TV directly");
                        _viewModel.PlayItemCommand.Execute(item);
                    }
                }
                else
                {
                    LogInfo("  Tag is not MediaItem!");
                }
            }
            else
            {
                LogInfo("  sender is not FrameworkElement!");
            }
        }

        #endregion

        #region Horizontal Scroll

        private void Row_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // Horizontal scroll with mouse wheel
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        #endregion

        #region Fullscreen

        private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullScreen();
        }

        private void ExitFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (_isFullScreen)
            {
                ToggleFullScreen();
            }
        }

        private void ToggleFullScreen()
        {
            LogInfo($"ToggleFullScreen called - current state: {_isFullScreen}");
            if (_isFullScreen)
            {
                // Exit fullscreen - revenir à la fenêtre normale avec player visible
                WindowState = _previousWindowState;
                WindowStyle = WindowStyle.None;
                MainScrollViewer.Visibility = Visibility.Visible;
                ClosePlayerBtn.Visibility = Visibility.Visible;
                FullscreenIcon.Text = "⛶";
                _isFullScreen = false;
                LogInfo("Exited fullscreen");
            }
            else
            {
                // Enter fullscreen - maximiser la fenêtre, le player reste visible
                _previousWindowState = WindowState;
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
                MainScrollViewer.Visibility = Visibility.Collapsed;
                ClosePlayerBtn.Visibility = Visibility.Collapsed;
                FullscreenIcon.Text = "⛶"; // Icône pour quitter plein écran
                _isFullScreen = true;
                LogInfo($"Entered fullscreen");
            }

            _viewModel.IsFullScreen = _isFullScreen;
        }

        private void ClosePlayer_Click(object sender, RoutedEventArgs e)
        {
            LogInfo("ClosePlayer_Click called");
            _viewModel.StopCommand.Execute(null);
            PlayerContainer.Visibility = Visibility.Collapsed;

            // Sortir du plein écran si on y était
            if (_isFullScreen)
            {
                WindowState = _previousWindowState;
                MainScrollViewer.Visibility = Visibility.Visible;
                _isFullScreen = false;
                _viewModel.IsFullScreen = false;
            }
        }

        private void ProgressSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Slider slider)
            {
                _viewModel.SeekToPositionCommand.Execute(slider.Value);
            }
        }

        private void FullscreenControls_MouseEnter(object sender, MouseEventArgs e)
        {
            // Show controls
        }

        private void FullscreenControls_MouseLeave(object sender, MouseEventArgs e)
        {
            // Auto-hide controls after delay
        }

        #endregion

        #region Keyboard Shortcuts

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
                case Key.Left:
                    _viewModel.SeekBackwardCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Right:
                    _viewModel.SeekForwardCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _viewModel.Volume = Math.Min(100, _viewModel.Volume + 5);
                    e.Handled = true;
                    break;
                case Key.Down:
                    _viewModel.Volume = Math.Max(0, _viewModel.Volume - 5);
                    e.Handled = true;
                    break;
                case Key.A:
                    _viewModel.CycleAudioTrackCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.C:
                    _viewModel.CycleSubtitleTrackCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.R:
                    _viewModel.CycleAspectRatioCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                case Key.Add:
                    _viewModel.CyclePlaybackSpeedCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.S:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // Focus search box
                        e.Handled = true;
                    }
                    break;
            }
        }

        #endregion

        #region Onboarding

        private async System.Threading.Tasks.Task ShowOnboardingAsync()
        {
            var onboarding = new OnboardingWindow();
            onboarding.Owner = this;

            if (onboarding.ShowDialog() == true)
            {
                // Save preferences to ViewModel and database
                await _viewModel.SaveUserPreferencesAsync(onboarding.Preferences);
                LogInfo($"Onboarding completed - Languages: {string.Join(", ", onboarding.Preferences.PreferredLanguages)}");
            }
        }

        #endregion

        #region Dialogs

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
                    case "Stalker":
                        _viewModel.AddStalkerPlaylistCommand.Execute(
                            (dialog.PlaylistUrl, dialog.MacAddress));
                        break;
                }
            }
        }

        private async void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow();
            settings.Owner = this;
            settings.ShowDialog();

            // Reload parental control settings after settings dialog closes
            await _parentalControl.LoadSettingsAsync();
            LogInfo($"Parental control reloaded: enabled={_parentalControl.IsEnabled}");
        }

        private void ShowEpg_Click(object sender, RoutedEventArgs e)
        {
            var epgWindow = new EpgWindow(_viewModel);
            epgWindow.Owner = this;
            epgWindow.ShowDialog();
        }

        private void ShowCatchup_Click(object sender, RoutedEventArgs e)
        {
            var catchupWindow = new CatchupWindow(_viewModel);
            catchupWindow.Owner = this;
            catchupWindow.ShowDialog();
        }

        private void ShowMultiView_Click(object sender, RoutedEventArgs e)
        {
            var multiViewWindow = new MultiViewWindow(_viewModel);
            multiViewWindow.Show();
        }

        private void ShowPlayerSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new PlayerSettingsWindow(_viewModel.PlayerSettings);
            settingsWindow.Owner = this;

            if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsChanged)
            {
                // Apply new settings to the player
                _viewModel.ApplyPlayerSettings(settingsWindow.Settings);
                LogInfo($"Player settings updated - Buffer: {settingsWindow.Settings.NetworkCaching}ms");
            }
        }

        private void ShowInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.FeaturedItem != null)
            {
                var detailWindow = new MediaDetailWindow(_viewModel.FeaturedItem, _viewModel);
                detailWindow.Owner = this;
                detailWindow.ShowDialog();
            }
        }

        private void ShowKeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var shortcutsOverlay = new KeyboardShortcutsOverlay();
            shortcutsOverlay.Owner = this;
            shortcutsOverlay.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            shortcutsOverlay.ShowDialog();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            LogInfo($"OnClosed called - Window closing");
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            _viewModel.Dispose();
            base.OnClosed(e);
            LogInfo("OnClosed completed - Shutting down app");
            Application.Current.Shutdown();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            LogInfo($"OnContentRendered - Window content rendered, Size: {ActualWidth}x{ActualHeight}");
        }
    }
}
