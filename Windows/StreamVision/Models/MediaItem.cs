using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace StreamVision.Models
{
    /// <summary>
    /// Stream quality options for adaptive playback
    /// </summary>
    public enum StreamQuality
    {
        Auto,       // Automatic quality selection
        Low,        // SD - saves bandwidth
        Medium,     // 720p
        High,       // 1080p
        Ultra       // 4K if available
    }

    /// <summary>
    /// Available stream URL for a specific quality
    /// </summary>
    public class QualityOption
    {
        public StreamQuality Quality { get; set; }
        public string Label { get; set; } = "";
        public string Url { get; set; } = "";
        public int? Bitrate { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }

        public string DisplayLabel => !string.IsNullOrEmpty(Label) ? Label : Quality switch
        {
            StreamQuality.Low => "SD",
            StreamQuality.Medium => "720p",
            StreamQuality.High => "1080p",
            StreamQuality.Ultra => "4K",
            _ => "Auto"
        };
    }

    /// <summary>
    /// Type de contenu média
    /// </summary>
    public enum ContentType
    {
        Live,       // Chaîne TV en direct
        Movie,      // Film VOD
        Series,     // Série TV
        Episode,    // Épisode de série
        Catchup     // Replay/Catch-up
    }

    /// <summary>
    /// Classe de base pour tout contenu média (unifié)
    /// </summary>
    public class MediaItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _sourceId = string.Empty;
        private string _name = string.Empty;
        private string? _originalName;
        private ContentType _mediaType = ContentType.Live;
        private string? _posterUrl;
        private string? _backdropUrl;
        private string? _logoUrl;
        private string _streamUrl = string.Empty;
        private string _groupTitle = string.Empty;
        private string? _categoryId;
        private string? _epgId;
        private bool _isFavorite;
        private int _order;
        private bool _isPlaying;

        // Métadonnées enrichies (TMDb)
        private int? _tmdbId;
        private string? _overview;
        private double _rating;
        private int _voteCount;
        private DateTime? _releaseDate;
        private int _runtime; // en minutes
        private string? _genres;
        private string? _director;
        private string? _cast;
        private string? _trailerUrl;

        // Pour les séries
        private string? _seriesId;
        private int _seasonNumber;
        private int _episodeNumber;
        private int _totalSeasons;
        private int _totalEpisodes;

        // Pour le streaming
        private string? _containerExtension;
        private int _catchupDays;
        private long _watchedPosition; // Position de lecture en ms
        private long _duration; // Durée totale en ms
        private DateTime? _lastWatched;

        // Quality options for adaptive streaming
        private List<QualityOption> _qualityOptions = new();
        private StreamQuality _currentQuality = StreamQuality.Auto;
        private StreamQuality _preferredQuality = StreamQuality.High;

        // Propriétés de base
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

        public string? OriginalName
        {
            get => _originalName;
            set { _originalName = value; OnPropertyChanged(nameof(OriginalName)); }
        }

        public ContentType MediaType
        {
            get => _mediaType;
            set { _mediaType = value; OnPropertyChanged(nameof(MediaType)); OnPropertyChanged(nameof(IsLive)); OnPropertyChanged(nameof(IsVod)); OnPropertyChanged(nameof(IsSeries)); }
        }

        public bool IsLive => MediaType == ContentType.Live || MediaType == ContentType.Catchup;
        public bool IsVod => MediaType == ContentType.Movie;
        public bool IsSeries => MediaType == ContentType.Series || MediaType == ContentType.Episode;

        public string? PosterUrl
        {
            get => _posterUrl;
            set { _posterUrl = value; OnPropertyChanged(nameof(PosterUrl)); OnPropertyChanged(nameof(DisplayImageUrl)); }
        }

        public string? BackdropUrl
        {
            get => _backdropUrl;
            set { _backdropUrl = value; OnPropertyChanged(nameof(BackdropUrl)); }
        }

        public string? LogoUrl
        {
            get => _logoUrl;
            set { _logoUrl = value; OnPropertyChanged(nameof(LogoUrl)); OnPropertyChanged(nameof(DisplayImageUrl)); }
        }

        // Image à afficher (poster pour VOD, logo pour live)
        public string? DisplayImageUrl => MediaType == ContentType.Live ? LogoUrl : (PosterUrl ?? LogoUrl);

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

        public string? CategoryId
        {
            get => _categoryId;
            set { _categoryId = value; OnPropertyChanged(nameof(CategoryId)); }
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

        // Métadonnées TMDb
        public int? TmdbId
        {
            get => _tmdbId;
            set { _tmdbId = value; OnPropertyChanged(nameof(TmdbId)); }
        }

        public string? Overview
        {
            get => _overview;
            set { _overview = value; OnPropertyChanged(nameof(Overview)); }
        }

        public double Rating
        {
            get => _rating;
            set { _rating = value; OnPropertyChanged(nameof(Rating)); OnPropertyChanged(nameof(RatingDisplay)); }
        }

        public string RatingDisplay => Rating > 0 ? $"{Rating:F1}/10" : "";

        public int VoteCount
        {
            get => _voteCount;
            set { _voteCount = value; OnPropertyChanged(nameof(VoteCount)); }
        }

        public DateTime? ReleaseDate
        {
            get => _releaseDate;
            set { _releaseDate = value; OnPropertyChanged(nameof(ReleaseDate)); OnPropertyChanged(nameof(Year)); }
        }

        public int? Year => ReleaseDate?.Year;

        public int Runtime
        {
            get => _runtime;
            set { _runtime = value; OnPropertyChanged(nameof(Runtime)); OnPropertyChanged(nameof(RuntimeDisplay)); }
        }

        public string RuntimeDisplay => Runtime > 0 ? $"{Runtime / 60}h {Runtime % 60}min" : "";

        public string? Genres
        {
            get => _genres;
            set { _genres = value; OnPropertyChanged(nameof(Genres)); }
        }

        public string? Director
        {
            get => _director;
            set { _director = value; OnPropertyChanged(nameof(Director)); }
        }

        public string? Cast
        {
            get => _cast;
            set { _cast = value; OnPropertyChanged(nameof(Cast)); }
        }

        public string? TrailerUrl
        {
            get => _trailerUrl;
            set { _trailerUrl = value; OnPropertyChanged(nameof(TrailerUrl)); }
        }

        // Séries
        public string? SeriesId
        {
            get => _seriesId;
            set { _seriesId = value; OnPropertyChanged(nameof(SeriesId)); }
        }

        public int SeasonNumber
        {
            get => _seasonNumber;
            set { _seasonNumber = value; OnPropertyChanged(nameof(SeasonNumber)); OnPropertyChanged(nameof(EpisodeDisplay)); }
        }

        public int EpisodeNumber
        {
            get => _episodeNumber;
            set { _episodeNumber = value; OnPropertyChanged(nameof(EpisodeNumber)); OnPropertyChanged(nameof(EpisodeDisplay)); }
        }

        public string EpisodeDisplay => SeasonNumber > 0 ? $"S{SeasonNumber:D2}E{EpisodeNumber:D2}" : "";

        public int TotalSeasons
        {
            get => _totalSeasons;
            set { _totalSeasons = value; OnPropertyChanged(nameof(TotalSeasons)); }
        }

        public int TotalEpisodes
        {
            get => _totalEpisodes;
            set { _totalEpisodes = value; OnPropertyChanged(nameof(TotalEpisodes)); }
        }

        // Streaming
        public string? ContainerExtension
        {
            get => _containerExtension;
            set { _containerExtension = value; OnPropertyChanged(nameof(ContainerExtension)); }
        }

        public int CatchupDays
        {
            get => _catchupDays;
            set { _catchupDays = value; OnPropertyChanged(nameof(CatchupDays)); OnPropertyChanged(nameof(HasCatchup)); }
        }

        public bool HasCatchup => CatchupDays > 0;

        public long WatchedPosition
        {
            get => _watchedPosition;
            set { _watchedPosition = value; OnPropertyChanged(nameof(WatchedPosition)); OnPropertyChanged(nameof(WatchProgress)); OnPropertyChanged(nameof(HasProgress)); }
        }

        public long Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(nameof(Duration)); OnPropertyChanged(nameof(WatchProgress)); }
        }

        public double WatchProgress => Duration > 0 ? (double)WatchedPosition / Duration : 0;
        public bool HasProgress => WatchedPosition > 0 && WatchProgress < 0.95;

        public DateTime? LastWatched
        {
            get => _lastWatched;
            set { _lastWatched = value; OnPropertyChanged(nameof(LastWatched)); }
        }

        // Quality options
        public List<QualityOption> QualityOptions
        {
            get => _qualityOptions;
            set { _qualityOptions = value; OnPropertyChanged(nameof(QualityOptions)); OnPropertyChanged(nameof(HasQualityOptions)); }
        }

        public bool HasQualityOptions => _qualityOptions.Count > 1;

        public StreamQuality CurrentQuality
        {
            get => _currentQuality;
            set { _currentQuality = value; OnPropertyChanged(nameof(CurrentQuality)); OnPropertyChanged(nameof(CurrentQualityDisplay)); }
        }

        public string CurrentQualityDisplay => CurrentQuality switch
        {
            StreamQuality.Low => "SD",
            StreamQuality.Medium => "720p",
            StreamQuality.High => "1080p",
            StreamQuality.Ultra => "4K",
            _ => "Auto"
        };

        public StreamQuality PreferredQuality
        {
            get => _preferredQuality;
            set { _preferredQuality = value; OnPropertyChanged(nameof(PreferredQuality)); }
        }

        /// <summary>
        /// Get the stream URL for a specific quality, or the best available
        /// </summary>
        public string GetStreamUrlForQuality(StreamQuality quality)
        {
            if (_qualityOptions.Count == 0) return StreamUrl;

            var option = _qualityOptions.FirstOrDefault(q => q.Quality == quality);
            if (option != null) return option.Url;

            // Fallback to closest quality
            return quality switch
            {
                StreamQuality.Ultra => _qualityOptions.FirstOrDefault(q => q.Quality == StreamQuality.High)?.Url
                                     ?? _qualityOptions.FirstOrDefault(q => q.Quality == StreamQuality.Medium)?.Url
                                     ?? StreamUrl,
                StreamQuality.High => _qualityOptions.FirstOrDefault(q => q.Quality == StreamQuality.Medium)?.Url
                                    ?? _qualityOptions.FirstOrDefault(q => q.Quality == StreamQuality.Low)?.Url
                                    ?? StreamUrl,
                StreamQuality.Medium => _qualityOptions.FirstOrDefault(q => q.Quality == StreamQuality.Low)?.Url
                                      ?? StreamUrl,
                _ => StreamUrl
            };
        }

        /// <summary>
        /// Get a lower quality URL than the current one
        /// </summary>
        public string? GetLowerQualityUrl()
        {
            if (_qualityOptions.Count == 0) return null;

            var currentIndex = (int)CurrentQuality;
            if (currentIndex <= 1) return null; // Already at lowest or Auto

            // Find next lower quality
            for (int i = currentIndex - 1; i >= (int)StreamQuality.Low; i--)
            {
                var option = _qualityOptions.FirstOrDefault(q => (int)q.Quality == i);
                if (option != null) return option.Url;
            }

            return null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Représente une série avec ses saisons et épisodes
    /// </summary>
    public class SeriesInfo : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string? _posterUrl;
        private string? _backdropUrl;
        private string? _overview;
        private double _rating;
        private int? _tmdbId;
        private ObservableCollection<SeasonInfo> _seasons = new();

        public string Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public string? PosterUrl { get => _posterUrl; set { _posterUrl = value; OnPropertyChanged(nameof(PosterUrl)); } }
        public string? BackdropUrl { get => _backdropUrl; set { _backdropUrl = value; OnPropertyChanged(nameof(BackdropUrl)); } }
        public string? Overview { get => _overview; set { _overview = value; OnPropertyChanged(nameof(Overview)); } }
        public double Rating { get => _rating; set { _rating = value; OnPropertyChanged(nameof(Rating)); } }
        public int? TmdbId { get => _tmdbId; set { _tmdbId = value; OnPropertyChanged(nameof(TmdbId)); } }
        public ObservableCollection<SeasonInfo> Seasons { get => _seasons; set { _seasons = value; OnPropertyChanged(nameof(Seasons)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class SeasonInfo : INotifyPropertyChanged
    {
        private int _seasonNumber;
        private string _name = string.Empty;
        private string? _posterUrl;
        private ObservableCollection<MediaItem> _episodes = new();

        public int SeasonNumber { get => _seasonNumber; set { _seasonNumber = value; OnPropertyChanged(nameof(SeasonNumber)); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public string? PosterUrl { get => _posterUrl; set { _posterUrl = value; OnPropertyChanged(nameof(PosterUrl)); } }
        public ObservableCollection<MediaItem> Episodes { get => _episodes; set { _episodes = value; OnPropertyChanged(nameof(Episodes)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Catégorie de contenu pour l'affichage
    /// </summary>
    public class MediaCategory : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private ContentType _mediaType;
        private ObservableCollection<MediaItem> _items = new();
        private int _itemCount;

        public string Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public ContentType MediaType { get => _mediaType; set { _mediaType = value; OnPropertyChanged(nameof(MediaType)); } }
        public ObservableCollection<MediaItem> Items { get => _items; set { _items = value; OnPropertyChanged(nameof(Items)); } }
        public int ItemCount { get => _itemCount; set { _itemCount = value; OnPropertyChanged(nameof(ItemCount)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Row Netflix-style pour l'affichage
    /// </summary>
    public class MediaRow : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _icon = string.Empty;
        private ObservableCollection<MediaItem> _items = new();
        private bool _isLoading;

        public string Title { get => _title; set { _title = value; OnPropertyChanged(nameof(Title)); } }
        public string Icon { get => _icon; set { _icon = value; OnPropertyChanged(nameof(Icon)); } }
        public ObservableCollection<MediaItem> Items { get => _items; set { _items = value; OnPropertyChanged(nameof(Items)); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
