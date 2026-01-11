using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using StreamVision.Models;
using StreamVision.ViewModels;

namespace StreamVision.Views
{
    public partial class MultiViewWindow : Window
    {
        private readonly HomeViewModel _viewModel;
        private LibVLC? _libVLC;
        private readonly MediaPlayer?[] _mediaPlayers = new MediaPlayer?[4];
        private readonly MediaItem?[] _channels = new MediaItem?[4];
        private int _activeAudioIndex = 0;
        private string _currentLayout = "2x2";
        private WindowState _previousWindowState;
        private bool _isFullScreen;

        public MultiViewWindow(HomeViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            Loaded += MultiViewWindow_Loaded;
            Closed += MultiViewWindow_Closed;
        }

        private void MultiViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Core.Initialize();
                _libVLC = new LibVLC("--no-xlib", "--quiet");

                // Create 4 media players
                for (int i = 0; i < 4; i++)
                {
                    _mediaPlayers[i] = new MediaPlayer(_libVLC);
                    _mediaPlayers[i].Volume = i == 0 ? 100 : 0; // Only first has audio
                }

                // Assign to VideoViews
                VideoView1.MediaPlayer = _mediaPlayers[0];
                VideoView2.MediaPlayer = _mediaPlayers[1];
                VideoView3.MediaPlayer = _mediaPlayers[2];
                VideoView4.MediaPlayer = _mediaPlayers[3];

                UpdateAudioIndicators();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Multi-View: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MultiViewWindow_Closed(object? sender, EventArgs e)
        {
            // Stop and dispose all players
            for (int i = 0; i < 4; i++)
            {
                _mediaPlayers[i]?.Stop();
                _mediaPlayers[i]?.Dispose();
            }
            _libVLC?.Dispose();
        }

        private void Cell_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && int.TryParse(element.Tag?.ToString(), out int cellIndex))
            {
                ShowChannelSelector(cellIndex - 1);
            }
        }

        private void ShowChannelSelector(int cellIndex)
        {
            var selector = new ChannelSelectorWindow(_viewModel.LiveChannels?.ToList() ?? new List<MediaItem>());
            selector.Owner = this;

            if (selector.ShowDialog() == true && selector.SelectedChannel != null)
            {
                PlayInCell(cellIndex, selector.SelectedChannel);
            }
        }

        private void PlayInCell(int cellIndex, MediaItem channel)
        {
            if (_libVLC == null || cellIndex < 0 || cellIndex > 3) return;

            var player = _mediaPlayers[cellIndex];
            if (player == null) return;

            try
            {
                var media = new Media(_libVLC, channel.StreamUrl, FromType.FromLocation);
                player.Play(media);
                _channels[cellIndex] = channel;

                // Update label
                UpdateCellLabel(cellIndex, channel.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur de lecture: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCellLabel(int cellIndex, string name)
        {
            switch (cellIndex)
            {
                case 0: Label1.Text = name; break;
                case 1: Label2.Text = name; break;
                case 2: Label3.Text = name; break;
                case 3: Label4.Text = name; break;
            }
        }

        private void SetActiveAudio(int index)
        {
            _activeAudioIndex = index;
            for (int i = 0; i < 4; i++)
            {
                if (_mediaPlayers[i] != null)
                {
                    _mediaPlayers[i]!.Volume = i == index ? 100 : 0;
                }
            }
            UpdateAudioIndicators();
        }

        private void UpdateAudioIndicators()
        {
            Audio1.Visibility = _activeAudioIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            Audio2.Visibility = _activeAudioIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            Audio3.Visibility = _activeAudioIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            Audio4.Visibility = _activeAudioIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Layout_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string layout)
            {
                SetLayout(layout);
            }
        }

        private void SetLayout(string layout)
        {
            _currentLayout = layout;
            LayoutText.Text = $" - {layout}";

            // Reset styles
            Layout2x1Btn.Style = (Style)FindResource("ViewBtn");
            Layout2x2Btn.Style = (Style)FindResource("ViewBtn");
            Layout1x2Btn.Style = (Style)FindResource("ViewBtn");
            LayoutPipBtn.Style = (Style)FindResource("ViewBtn");

            // Reset grid
            VideoGrid.RowDefinitions.Clear();
            VideoGrid.ColumnDefinitions.Clear();

            switch (layout)
            {
                case "2x1":
                    Layout2x1Btn.Style = (Style)FindResource("ActiveViewBtn");
                    VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    VideoGrid.RowDefinitions.Add(new RowDefinition());

                    Grid.SetRow(Cell1, 0); Grid.SetColumn(Cell1, 0);
                    Grid.SetRow(Cell2, 0); Grid.SetColumn(Cell2, 1);
                    Cell1.Visibility = Visibility.Visible;
                    Cell2.Visibility = Visibility.Visible;
                    Cell3.Visibility = Visibility.Collapsed;
                    Cell4.Visibility = Visibility.Collapsed;
                    break;

                case "1x2":
                    Layout1x2Btn.Style = (Style)FindResource("ActiveViewBtn");
                    VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    VideoGrid.RowDefinitions.Add(new RowDefinition());
                    VideoGrid.RowDefinitions.Add(new RowDefinition());

                    Grid.SetRow(Cell1, 0); Grid.SetColumn(Cell1, 0);
                    Grid.SetRow(Cell2, 1); Grid.SetColumn(Cell2, 0);
                    Cell1.Visibility = Visibility.Visible;
                    Cell2.Visibility = Visibility.Visible;
                    Cell3.Visibility = Visibility.Collapsed;
                    Cell4.Visibility = Visibility.Collapsed;
                    break;

                case "2x2":
                    Layout2x2Btn.Style = (Style)FindResource("ActiveViewBtn");
                    VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    VideoGrid.RowDefinitions.Add(new RowDefinition());
                    VideoGrid.RowDefinitions.Add(new RowDefinition());

                    Grid.SetRow(Cell1, 0); Grid.SetColumn(Cell1, 0);
                    Grid.SetRow(Cell2, 0); Grid.SetColumn(Cell2, 1);
                    Grid.SetRow(Cell3, 1); Grid.SetColumn(Cell3, 0);
                    Grid.SetRow(Cell4, 1); Grid.SetColumn(Cell4, 1);
                    Cell1.Visibility = Visibility.Visible;
                    Cell2.Visibility = Visibility.Visible;
                    Cell3.Visibility = Visibility.Visible;
                    Cell4.Visibility = Visibility.Visible;
                    break;

                case "PIP":
                    LayoutPipBtn.Style = (Style)FindResource("ActiveViewBtn");
                    VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    VideoGrid.RowDefinitions.Add(new RowDefinition());

                    Grid.SetRow(Cell1, 0); Grid.SetColumn(Cell1, 0);
                    Cell1.Visibility = Visibility.Visible;

                    // Make Cell2 a PIP overlay
                    Grid.SetRow(Cell2, 0); Grid.SetColumn(Cell2, 0);
                    Cell2.Width = 320;
                    Cell2.Height = 180;
                    Cell2.HorizontalAlignment = HorizontalAlignment.Right;
                    Cell2.VerticalAlignment = VerticalAlignment.Bottom;
                    Cell2.Margin = new Thickness(0, 0, 20, 20);
                    Cell2.Visibility = Visibility.Visible;

                    Cell3.Visibility = Visibility.Collapsed;
                    Cell4.Visibility = Visibility.Collapsed;
                    break;
            }

            // Reset Cell2 properties if not PIP
            if (layout != "PIP")
            {
                Cell2.Width = double.NaN;
                Cell2.Height = double.NaN;
                Cell2.HorizontalAlignment = HorizontalAlignment.Stretch;
                Cell2.VerticalAlignment = VerticalAlignment.Stretch;
                Cell2.Margin = new Thickness(2);
            }
        }

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 4; i++)
            {
                _mediaPlayers[i]?.Stop();
                _channels[i] = null;
                UpdateCellLabel(i, "Cliquez pour ajouter");
            }
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ToggleFullscreen()
        {
            if (_isFullScreen)
            {
                WindowState = _previousWindowState;
                WindowStyle = WindowStyle.None;
                _isFullScreen = false;
            }
            else
            {
                _previousWindowState = WindowState;
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
                _isFullScreen = true;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.D1:
                case Key.NumPad1:
                    SetActiveAudio(0);
                    e.Handled = true;
                    break;
                case Key.D2:
                case Key.NumPad2:
                    SetActiveAudio(1);
                    e.Handled = true;
                    break;
                case Key.D3:
                case Key.NumPad3:
                    SetActiveAudio(2);
                    e.Handled = true;
                    break;
                case Key.D4:
                case Key.NumPad4:
                    SetActiveAudio(3);
                    e.Handled = true;
                    break;
                case Key.F11:
                case Key.F:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (_isFullScreen)
                    {
                        ToggleFullscreen();
                        e.Handled = true;
                    }
                    else
                    {
                        Close();
                    }
                    break;
            }
        }

        #region Window Controls

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
    }
}
