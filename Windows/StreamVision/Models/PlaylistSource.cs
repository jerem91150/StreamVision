using System;
using System.ComponentModel;

namespace StreamVision.Models
{
    public enum SourceType
    {
        M3U,
        XtreamCodes,
        StalkerPortal
    }

    public class PlaylistSource : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = string.Empty;
        private SourceType _type;
        private string _url = string.Empty;
        private string? _username;
        private string? _password;
        private string? _macAddress;
        private string? _epgUrl;
        private DateTime _lastSync;
        private bool _isActive = true;
        private int _channelCount;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public SourceType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(nameof(Url)); }
        }

        public string? Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string? Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(nameof(Password)); }
        }

        public string? MacAddress
        {
            get => _macAddress;
            set { _macAddress = value; OnPropertyChanged(nameof(MacAddress)); }
        }

        public string? EpgUrl
        {
            get => _epgUrl;
            set { _epgUrl = value; OnPropertyChanged(nameof(EpgUrl)); }
        }

        public DateTime LastSync
        {
            get => _lastSync;
            set { _lastSync = value; OnPropertyChanged(nameof(LastSync)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public int ChannelCount
        {
            get => _channelCount;
            set { _channelCount = value; OnPropertyChanged(nameof(ChannelCount)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
