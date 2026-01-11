using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StreamVision.Models;

namespace StreamVision.Views
{
    public partial class ChannelSelectorWindow : Window
    {
        private readonly List<MediaItem> _allChannels;
        public MediaItem? SelectedChannel { get; private set; }

        public ChannelSelectorWindow(List<MediaItem> channels)
        {
            InitializeComponent();
            _allChannels = channels;
            ChannelList.ItemsSource = _allChannels.Take(100).ToList(); // Limit initial display
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.ToLower().Trim();
            if (string.IsNullOrEmpty(query))
            {
                ChannelList.ItemsSource = _allChannels.Take(100).ToList();
            }
            else
            {
                var filtered = _allChannels
                    .Where(c => c.Name.ToLower().Contains(query) ||
                                (c.GroupTitle?.ToLower().Contains(query) ?? false))
                    .Take(100)
                    .ToList();
                ChannelList.ItemsSource = filtered;
            }
        }

        private void Channel_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ChannelList.SelectedItem is MediaItem channel)
            {
                SelectedChannel = channel;
                DialogResult = true;
                Close();
            }
        }
    }
}
