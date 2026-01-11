using System;
using System.ComponentModel;

namespace StreamVision.Models
{
    public class Channel : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _sourceId = string.Empty;
        private string _name = string.Empty;
        private string? _logoUrl;
        private string _streamUrl = string.Empty;
        private string _groupTitle = string.Empty;
        private string? _epgId;
        private bool _isFavorite;
        private int _catchupDays;
        private int _order;
        private bool _isPlaying;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string SourceId
        {
            get => _sourceId;
            set { _sourceId = value; OnPropertyChanged(nameof(SourceId)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string? LogoUrl
        {
            get => _logoUrl;
            set { _logoUrl = value; OnPropertyChanged(nameof(LogoUrl)); }
        }

        public string StreamUrl
        {
            get => _streamUrl;
            set { _streamUrl = value; OnPropertyChanged(nameof(StreamUrl)); }
        }

        public string GroupTitle
        {
            get => _groupTitle;
            set { _groupTitle = value; OnPropertyChanged(nameof(GroupTitle)); }
        }

        public string? EpgId
        {
            get => _epgId;
            set { _epgId = value; OnPropertyChanged(nameof(EpgId)); }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(nameof(IsFavorite)); }
        }

        public int CatchupDays
        {
            get => _catchupDays;
            set { _catchupDays = value; OnPropertyChanged(nameof(CatchupDays)); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(nameof(Order)); }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(nameof(IsPlaying)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
