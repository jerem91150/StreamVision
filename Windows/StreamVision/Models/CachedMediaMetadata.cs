using System;

namespace StreamVision.Models
{
    /// <summary>
    /// Cached metadata from TMDb for faster loading and offline support
    /// </summary>
    public class CachedMediaMetadata
    {
        public string MediaId { get; set; } = string.Empty;
        public int TmdbId { get; set; }
        public string? Title { get; set; }
        public string? OriginalTitle { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }
        public double Rating { get; set; }
        public int VoteCount { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? Genres { get; set; }
        public string? Cast { get; set; }
        public string? Director { get; set; }
        public int Runtime { get; set; }
        public string? TrailerUrl { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddDays(30);

        /// <summary>
        /// Check if this cache entry is still valid
        /// </summary>
        public bool IsValid => DateTime.Now < ExpiresAt;

        /// <summary>
        /// Full poster URL (TMDb CDN)
        /// </summary>
        public string? PosterUrl => !string.IsNullOrEmpty(PosterPath)
            ? $"https://image.tmdb.org/t/p/w500{PosterPath}"
            : null;

        /// <summary>
        /// Full backdrop URL (TMDb CDN)
        /// </summary>
        public string? BackdropUrl => !string.IsNullOrEmpty(BackdropPath)
            ? $"https://image.tmdb.org/t/p/w1280{BackdropPath}"
            : null;

        /// <summary>
        /// Apply cached metadata to a MediaItem
        /// </summary>
        public void ApplyTo(MediaItem item)
        {
            if (item == null) return;

            if (TmdbId > 0) item.TmdbId = TmdbId;
            if (!string.IsNullOrEmpty(Overview)) item.Overview = Overview;
            if (!string.IsNullOrEmpty(PosterUrl)) item.PosterUrl = PosterUrl;
            if (!string.IsNullOrEmpty(BackdropUrl)) item.BackdropUrl = BackdropUrl;
            if (Rating > 0) item.Rating = Rating;
            if (VoteCount > 0) item.VoteCount = VoteCount;
            if (ReleaseDate.HasValue) item.ReleaseDate = ReleaseDate;
            if (!string.IsNullOrEmpty(Genres)) item.Genres = Genres;
            if (!string.IsNullOrEmpty(Cast)) item.Cast = Cast;
            if (!string.IsNullOrEmpty(Director)) item.Director = Director;
            if (Runtime > 0) item.Runtime = Runtime;
            if (!string.IsNullOrEmpty(TrailerUrl)) item.TrailerUrl = TrailerUrl;
        }

        /// <summary>
        /// Create a CachedMediaMetadata from a MediaItem
        /// </summary>
        public static CachedMediaMetadata FromMediaItem(MediaItem item)
        {
            return new CachedMediaMetadata
            {
                MediaId = item.Id,
                TmdbId = item.TmdbId ?? 0,
                Title = item.Name,
                OriginalTitle = item.OriginalName,
                Overview = item.Overview,
                PosterPath = ExtractTmdbPath(item.PosterUrl),
                BackdropPath = ExtractTmdbPath(item.BackdropUrl),
                Rating = item.Rating,
                VoteCount = item.VoteCount,
                ReleaseDate = item.ReleaseDate,
                Genres = item.Genres,
                Cast = item.Cast,
                Director = item.Director,
                Runtime = item.Runtime,
                TrailerUrl = item.TrailerUrl
            };
        }

        private static string? ExtractTmdbPath(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            // Extract path from TMDb URL: https://image.tmdb.org/t/p/w500/abc123.jpg -> /abc123.jpg
            const string tmdbBase = "image.tmdb.org/t/p/";
            var idx = url.IndexOf(tmdbBase, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var pathStart = url.IndexOf('/', idx + tmdbBase.Length);
                if (pathStart >= 0)
                {
                    return url[pathStart..];
                }
            }

            return null;
        }
    }
}
