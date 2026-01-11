using System;
using System.ComponentModel;

namespace StreamVision.Models
{
    public class EpgProgram : INotifyPropertyChanged
    {
        private string _channelId = string.Empty;
        private string _title = string.Empty;
        private string? _description;
        private DateTime _startTime;
        private DateTime _endTime;
        private string? _category;
        private string? _iconUrl;

        public string ChannelId
        {
            get => _channelId;
            set { _channelId = value; OnPropertyChanged(nameof(ChannelId)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string? Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(nameof(StartTime)); }
        }

        public DateTime EndTime
        {
            get => _endTime;
            set { _endTime = value; OnPropertyChanged(nameof(EndTime)); }
        }

        public string? Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }

        public string? IconUrl
        {
            get => _iconUrl;
            set { _iconUrl = value; OnPropertyChanged(nameof(IconUrl)); }
        }

        public double Progress
        {
            get
            {
                if (DateTime.Now < StartTime) return 0;
                if (DateTime.Now > EndTime) return 100;
                var total = (EndTime - StartTime).TotalMinutes;
                var elapsed = (DateTime.Now - StartTime).TotalMinutes;
                return (elapsed / total) * 100;
            }
        }

        public bool IsLive => DateTime.Now >= StartTime && DateTime.Now <= EndTime;

        public string TimeRange => $"{StartTime:HH:mm} - {EndTime:HH:mm}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
