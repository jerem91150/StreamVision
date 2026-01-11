using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StreamVision.Models;
using StreamVision.Services;
using StreamVision.ViewModels;

namespace StreamVision.Views
{
    public partial class EpgWindow : Window
    {
        private readonly HomeViewModel _viewModel;
        private readonly EpgService _epgService;
        private readonly XtreamCodesService _xtreamService;
        private DateTime _selectedDate = DateTime.Today;
        private List<MediaItem> _liveChannels = new();

        public EpgWindow(HomeViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _epgService = new EpgService();
            _xtreamService = new XtreamCodesService();

            UpdateDateLabel();
            GenerateTimeHeaders();
            Loaded += async (s, e) => await LoadEpgDataAsync();
        }

        private void UpdateDateLabel()
        {
            if (_selectedDate.Date == DateTime.Today)
            {
                DateLabel.Text = "Aujourd'hui";
            }
            else if (_selectedDate.Date == DateTime.Today.AddDays(1))
            {
                DateLabel.Text = "Demain";
            }
            else if (_selectedDate.Date == DateTime.Today.AddDays(-1))
            {
                DateLabel.Text = "Hier";
            }
            else
            {
                DateLabel.Text = _selectedDate.ToString("dddd d MMMM");
            }
        }

        private void GenerateTimeHeaders()
        {
            TimeHeaders.Items.Clear();
            for (int hour = 0; hour < 24; hour++)
            {
                var timeText = new TextBlock
                {
                    Text = $"{hour:00}:00",
                    Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 100
                };
                TimeHeaders.Items.Add(timeText);
            }
        }

        private async Task LoadEpgDataAsync()
        {
            try
            {
                // Get live channels from ViewModel
                _liveChannels = _viewModel.LiveChannels?.ToList() ?? new List<MediaItem>();

                if (!_liveChannels.Any())
                {
                    EpgGrid.Items.Clear();
                    EpgGrid.Items.Add(CreateNoDataMessage("Aucune chaîne live disponible"));
                    return;
                }

                // Display channels with EPG data
                await BuildEpgGridAsync();
            }
            catch (Exception ex)
            {
                EpgGrid.Items.Clear();
                EpgGrid.Items.Add(CreateNoDataMessage($"Erreur: {ex.Message}"));
            }
        }

        private async Task BuildEpgGridAsync()
        {
            EpgGrid.Items.Clear();

            foreach (var channel in _liveChannels.Take(50)) // Limit to 50 channels for performance
            {
                var channelRow = await CreateChannelRowAsync(channel);
                EpgGrid.Items.Add(channelRow);
            }
        }

        private async Task<Border> CreateChannelRowAsync(MediaItem channel)
        {
            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 33, 62)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Channel info
            var channelPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var logoContainer = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(31, 43, 71)),
                Margin = new Thickness(0, 0, 10, 0)
            };

            if (!string.IsNullOrEmpty(channel.LogoUrl))
            {
                try
                {
                    var image = new Image
                    {
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(channel.LogoUrl)),
                        Stretch = Stretch.UniformToFill
                    };
                    logoContainer.Child = image;
                }
                catch { }
            }

            var channelName = new TextBlock
            {
                Text = channel.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 140
            };

            channelPanel.Children.Add(logoContainer);
            channelPanel.Children.Add(channelName);
            Grid.SetColumn(channelPanel, 0);
            grid.Children.Add(channelPanel);

            // Program timeline
            var programsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Get EPG programs for this channel
            var programs = _epgService.GetProgramsForChannel(channel.EpgId ?? channel.Id, _selectedDate);

            if (programs.Any())
            {
                foreach (var program in programs.Take(10))
                {
                    var programBlock = CreateProgramBlock(program, channel);
                    programsPanel.Children.Add(programBlock);
                }
            }
            else
            {
                // No EPG data - show current time slot
                var noDataBlock = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(31, 43, 71)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(2),
                    MinWidth = 200
                };
                noDataBlock.Child = new TextBlock
                {
                    Text = "Programme non disponible",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                programsPanel.Children.Add(noDataBlock);
            }

            Grid.SetColumn(programsPanel, 1);
            grid.Children.Add(programsPanel);

            row.Child = grid;
            return row;
        }

        private Border CreateProgramBlock(EpgProgram program, MediaItem channel)
        {
            var now = DateTime.Now;
            var isCurrent = now >= program.StartTime && now <= program.EndTime;

            var block = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(2),
                MinWidth = 150,
                MaxWidth = 300,
                Cursor = Cursors.Hand
            };

            if (isCurrent)
            {
                block.Background = new LinearGradientBrush(
                    Color.FromRgb(30, 58, 95),
                    Color.FromRgb(15, 52, 96),
                    0);
                block.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 212, 255));
                block.BorderThickness = new Thickness(2);
            }
            else
            {
                block.Background = new SolidColorBrush(Color.FromRgb(31, 43, 71));
                block.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                block.BorderThickness = new Thickness(1);
            }

            var content = new StackPanel();

            var title = new TextBlock
            {
                Text = program.Title ?? "Sans titre",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var time = new TextBlock
            {
                Text = $"{program.StartTime:HH:mm} - {program.EndTime:HH:mm}",
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0)
            };

            content.Children.Add(title);
            content.Children.Add(time);

            block.Child = content;

            // Click to play channel
            block.MouseLeftButtonDown += (s, e) =>
            {
                _viewModel.PlayItemCommand.Execute(channel);
                this.Close();
            };

            return block;
        }

        private TextBlock CreateNoDataMessage(string message)
        {
            return new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0)
            };
        }

        private void PreviousDay_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-1);
            UpdateDateLabel();
            _ = LoadEpgDataAsync();
        }

        private void NextDay_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(1);
            UpdateDateLabel();
            _ = LoadEpgDataAsync();
        }
    }
}
