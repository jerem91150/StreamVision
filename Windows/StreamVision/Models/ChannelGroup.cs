using System.Collections.ObjectModel;
using System.ComponentModel;

namespace StreamVision.Models
{
    public class ChannelGroup : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private ObservableCollection<Channel> _channels = new();
        private bool _isExpanded = true;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public ObservableCollection<Channel> Channels
        {
            get => _channels;
            set { _channels = value; OnPropertyChanged(nameof(Channels)); OnPropertyChanged(nameof(ChannelCount)); }
        }

        public int ChannelCount => Channels.Count;

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
