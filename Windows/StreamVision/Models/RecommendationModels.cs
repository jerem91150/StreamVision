using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace StreamVision.Models
{
    // Watch History Entry
    public class WatchHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ChannelId { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationSeconds { get; set; }
        public double CompletionPercentage { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public int HourOfDay { get; set; }
    }

    // User Preferences Profile
    public class UserPreferences
    {
        public string UserId { get; set; } = "default";
        public Dictionary<string, double> CategoryAffinities { get; set; } = new();
        public Dictionary<int, List<string>> TimeSlotPreferences { get; set; } = new(); // Hour -> Categories
        public List<string> FavoriteChannelIds { get; set; } = new();
        public List<string> DislikedChannelIds { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    // Recommendation Item
    public class RecommendationItem : INotifyPropertyChanged
    {
        private string _channelId = string.Empty;
        private string _channelName = string.Empty;
        private string _logoUrl = string.Empty;
        private string _category = string.Empty;
        private double _score;
        private string _reason = string.Empty;
        private RecommendationType _type;

        public string ChannelId
        {
            get => _channelId;
            set { _channelId = value; OnPropertyChanged(nameof(ChannelId)); }
        }

        public string ChannelName
        {
            get => _channelName;
            set { _channelName = value; OnPropertyChanged(nameof(ChannelName)); }
        }

        public string LogoUrl
        {
            get => _logoUrl;
            set { _logoUrl = value; OnPropertyChanged(nameof(LogoUrl)); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }

        public double Score
        {
            get => _score;
            set { _score = value; OnPropertyChanged(nameof(Score)); }
        }

        public string Reason
        {
            get => _reason;
            set { _reason = value; OnPropertyChanged(nameof(Reason)); }
        }

        public RecommendationType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public string StreamUrl { get; set; } = string.Empty;
        public int WatchedPercentage { get; set; }
        public DateTime? LastWatched { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum RecommendationType
    {
        ContinueWatching,
        BecauseYouWatched,
        TopPicksForYou,
        TrendingNow,
        NewReleases,
        CategoryRecommendation,
        HiddenGems,
        SimilarContent,
        TimeBasedPicks
    }

    // User Statistics
    public class UserStats
    {
        public int TotalWatchTimeMinutes { get; set; }
        public int TotalChannelsWatched { get; set; }
        public string FavoriteCategory { get; set; } = "None";
        public int WatchSessionCount { get; set; }
        public double AverageSessionMinutes { get; set; }
    }

    // Recommendation Section for UI
    public class RecommendationSection : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private RecommendationType _type;
        private List<RecommendationItem> _items = new();

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; OnPropertyChanged(nameof(Subtitle)); }
        }

        public RecommendationType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public List<RecommendationItem> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(nameof(Items)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Category Statistics
    public class CategoryStats
    {
        public string Category { get; set; } = string.Empty;
        public int TotalWatchTimeMinutes { get; set; }
        public int WatchCount { get; set; }
        public double AverageSessionMinutes { get; set; }
        public double AffinityScore { get; set; }
        public Dictionary<int, int> HourlyDistribution { get; set; } = new();
    }

    // Content Similarity Score
    public class ContentSimilarity
    {
        public string ChannelIdA { get; set; } = string.Empty;
        public string ChannelIdB { get; set; } = string.Empty;
        public double SimilarityScore { get; set; }
        public List<string> CommonTags { get; set; } = new();
    }
}
