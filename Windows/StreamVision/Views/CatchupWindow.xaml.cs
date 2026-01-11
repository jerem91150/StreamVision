using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StreamVision.Models;
using StreamVision.Services;
using StreamVision.ViewModels;

namespace StreamVision.Views
{
    public partial class CatchupWindow : Window
    {
        private readonly HomeViewModel _viewModel;
        private readonly XtreamCodesService _xtreamService;
        private List<CatchupChannel> _catchupChannels = new();
        private CatchupChannel? _selectedChannel;
        private int _selectedDaysAgo = 1;
        private PlaylistSource? _activeSource;

        public CatchupWindow(HomeViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _xtreamService = new XtreamCodesService();
            Loaded += CatchupWindow_Loaded;
        }

        private async void CatchupWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCatchupChannelsAsync();
        }

        private async Task LoadCatchupChannelsAsync()
        {
            _catchupChannels.Clear();

            // Get channels with catch-up enabled
            if (_viewModel.LiveChannels != null)
            {
                foreach (var channel in _viewModel.LiveChannels.Where(c => c.CatchupDays > 0))
                {
                    _catchupChannels.Add(new CatchupChannel
                    {
                        Id = channel.Id,
                        Name = channel.Name,
                        LogoUrl = channel.LogoUrl,
                        StreamUrl = channel.StreamUrl,
                        CatchupDays = channel.CatchupDays,
                        SourceId = channel.SourceId,
                        EpgId = channel.EpgId
                    });
                }
            }

            // Get active source for API calls
            _activeSource = await GetActiveSourceAsync();

            ChannelListBox.ItemsSource = _catchupChannels;
        }

        private async Task<PlaylistSource?> GetActiveSourceAsync()
        {
            // Try to find the source from the first channel
            if (_catchupChannels.Any())
            {
                var sourceId = _catchupChannels.First().SourceId;
                // Get from database
                var db = new DatabaseService();
                var sources = await db.GetPlaylistSourcesAsync();
                return sources.FirstOrDefault(s => s.Id == sourceId);
            }
            return null;
        }

        private void Day_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int daysAgo))
            {
                _selectedDaysAgo = daysAgo;
                var targetDate = DateTime.Now.AddDays(-daysAgo);
                SelectedDateText.Text = $"Programmes du {targetDate:dddd d MMMM yyyy}";

                // Highlight selected day button
                foreach (var child in ((StackPanel)btn.Parent).Children)
                {
                    if (child is Button dayBtn)
                    {
                        dayBtn.Background = dayBtn == btn
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 69, 96))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 100));
                    }
                }

                LoadProgramsForSelectedChannel();
            }
        }

        private async void Channel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelListBox.SelectedItem is CatchupChannel channel)
            {
                _selectedChannel = channel;
                await LoadProgramsForChannelAsync(channel);
            }
        }

        private async void LoadProgramsForSelectedChannel()
        {
            if (_selectedChannel != null)
            {
                await LoadProgramsForChannelAsync(_selectedChannel);
            }
        }

        private async Task LoadProgramsForChannelAsync(CatchupChannel channel)
        {
            LoadingText.Text = "Chargement des programmes...";
            LoadingText.Visibility = Visibility.Visible;
            ProgramListBox.ItemsSource = null;

            var programs = new List<CatchupProgram>();

            // Try to get EPG data for the selected day
            var targetDate = DateTime.Now.AddDays(-_selectedDaysAgo).Date;

            if (_activeSource != null && _activeSource.Type == SourceType.XtreamCodes)
            {
                // Extract stream ID from URL
                var streamId = ExtractStreamId(channel.StreamUrl);

                if (!string.IsNullOrEmpty(streamId))
                {
                    // Get EPG entries for past programs
                    var epgEntries = await _xtreamService.GetShortEpgAsync(
                        _activeSource.Url,
                        _activeSource.Username ?? "",
                        _activeSource.Password ?? "",
                        streamId,
                        100 // Get more entries to cover past days
                    );

                    // Filter for the selected day
                    foreach (var entry in epgEntries.Where(e =>
                        e.Start.HasValue && e.Start.Value.Date == targetDate))
                    {
                        var duration = entry.End.HasValue && entry.Start.HasValue
                            ? (entry.End.Value - entry.Start.Value).TotalMinutes
                            : 60;

                        programs.Add(new CatchupProgram
                        {
                            Id = entry.Id,
                            Title = entry.Title,
                            Description = entry.Description,
                            StartTime = entry.Start ?? DateTime.Now,
                            EndTime = entry.End ?? DateTime.Now,
                            DurationMinutes = (int)duration,
                            StreamId = streamId,
                            ChannelName = channel.Name
                        });
                    }
                }
            }

            // If no EPG data, create sample time slots
            if (!programs.Any())
            {
                programs = GenerateSamplePrograms(targetDate, channel.Name);
            }

            programs = programs.OrderBy(p => p.StartTime).ToList();

            if (programs.Any())
            {
                LoadingText.Visibility = Visibility.Collapsed;
                ProgramListBox.ItemsSource = programs;
            }
            else
            {
                LoadingText.Text = "Aucun programme disponible pour cette date";
            }
        }

        private List<CatchupProgram> GenerateSamplePrograms(DateTime date, string channelName)
        {
            var programs = new List<CatchupProgram>();
            var currentTime = date;

            // Generate time slots for the day
            while (currentTime.Date == date)
            {
                var duration = new[] { 30, 60, 90, 120 }[new Random().Next(4)];
                programs.Add(new CatchupProgram
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"Programme de {currentTime:HH:mm}",
                    Description = $"Diffusé sur {channelName}",
                    StartTime = currentTime,
                    EndTime = currentTime.AddMinutes(duration),
                    DurationMinutes = duration,
                    StreamId = "",
                    ChannelName = channelName
                });
                currentTime = currentTime.AddMinutes(duration);
            }

            return programs;
        }

        private string ExtractStreamId(string streamUrl)
        {
            // Extract stream ID from URL like: http://server/live/user/pass/12345.m3u8
            try
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(streamUrl);
                if (int.TryParse(fileName, out _))
                    return fileName;
            }
            catch { }
            return "";
        }

        private void Program_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CatchupProgram program)
            {
                PlayProgram(program);
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CatchupProgram program)
            {
                PlayProgram(program);
            }
        }

        private void PlayProgram(CatchupProgram program)
        {
            if (_selectedChannel == null || _activeSource == null) return;

            var streamId = ExtractStreamId(_selectedChannel.StreamUrl);
            if (string.IsNullOrEmpty(streamId))
            {
                MessageBox.Show("Impossible de lire ce programme en rattrapage.", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Generate catch-up URL
            var catchupUrl = _xtreamService.GetCatchupUrl(
                _activeSource.Url,
                _activeSource.Username ?? "",
                _activeSource.Password ?? "",
                streamId,
                program.StartTime,
                program.DurationMinutes
            );

            // Create a temporary media item for playback
            var catchupItem = new MediaItem
            {
                Name = $"{program.Title} - {_selectedChannel.Name}",
                StreamUrl = catchupUrl,
                LogoUrl = _selectedChannel.LogoUrl,
                MediaType = ContentType.Live
            };

            _viewModel.PlayItemCommand.Execute(catchupItem);
            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class CatchupChannel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? LogoUrl { get; set; }
        public string StreamUrl { get; set; } = "";
        public int CatchupDays { get; set; }
        public string SourceId { get; set; } = "";
        public string? EpgId { get; set; }

        public string CatchupDaysText => CatchupDays == 1
            ? "1 jour de rattrapage"
            : $"{CatchupDays} jours de rattrapage";
    }

    public class CatchupProgram
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string StreamId { get; set; } = "";
        public string ChannelName { get; set; } = "";

        public string TimeRange => $"{StartTime:HH:mm} - {EndTime:HH:mm}";
        public string Duration => DurationMinutes >= 60
            ? $"{DurationMinutes / 60}h{(DurationMinutes % 60 > 0 ? $"{DurationMinutes % 60:00}" : "")}"
            : $"{DurationMinutes} min";
    }
}
