using System;
using System.Collections.Generic;
using System.Linq;

namespace StreamVision.Models
{
    /// <summary>
    /// Represents content filter options for movies/series
    /// </summary>
    public class ContentFilter
    {
        public List<string> Genres { get; set; } = new();
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
        public double? MinRating { get; set; }
        public double? MaxRating { get; set; }
        public List<string> Languages { get; set; } = new();
        public int? MinDuration { get; set; }  // minutes
        public int? MaxDuration { get; set; }  // minutes
        public bool? HasSubtitles { get; set; }
        public ContentType? Type { get; set; }
        public bool? IsFavorite { get; set; }
        public bool? HasProgress { get; set; }  // Continue watching
        public string? SearchQuery { get; set; }

        /// <summary>
        /// Check if any filter is active
        /// </summary>
        public bool HasActiveFilters =>
            Genres.Any() ||
            MinYear.HasValue ||
            MaxYear.HasValue ||
            MinRating.HasValue ||
            MaxRating.HasValue ||
            Languages.Any() ||
            MinDuration.HasValue ||
            MaxDuration.HasValue ||
            HasSubtitles.HasValue ||
            Type.HasValue ||
            IsFavorite.HasValue ||
            HasProgress.HasValue ||
            !string.IsNullOrWhiteSpace(SearchQuery);

        /// <summary>
        /// Get a human-readable summary of active filters (for XAML binding)
        /// </summary>
        public string Summary => GetSummary();

        /// <summary>
        /// Apply filter to a collection of media items
        /// </summary>
        public IEnumerable<MediaItem> Apply(IEnumerable<MediaItem> items)
        {
            if (items == null) yield break;

            foreach (var item in items)
            {
                if (Matches(item))
                    yield return item;
            }
        }

        /// <summary>
        /// Check if a single item matches the filter
        /// </summary>
        public bool Matches(MediaItem item)
        {
            if (item == null) return false;

            // Search query
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLowerInvariant();
                var nameMatch = item.Name?.ToLowerInvariant().Contains(query) == true;
                var overviewMatch = item.Overview?.ToLowerInvariant().Contains(query) == true;
                var castMatch = item.Cast?.ToLowerInvariant().Contains(query) == true;
                var directorMatch = item.Director?.ToLowerInvariant().Contains(query) == true;

                if (!nameMatch && !overviewMatch && !castMatch && !directorMatch)
                    return false;
            }

            // Content type
            if (Type.HasValue && item.MediaType != Type.Value)
                return false;

            // Year range
            if (MinYear.HasValue && (item.ReleaseDate?.Year ?? 0) < MinYear.Value)
                return false;
            if (MaxYear.HasValue && (item.ReleaseDate?.Year ?? int.MaxValue) > MaxYear.Value)
                return false;

            // Rating range
            if (MinRating.HasValue && item.Rating < MinRating.Value)
                return false;
            if (MaxRating.HasValue && item.Rating > MaxRating.Value)
                return false;

            // Duration range
            if (MinDuration.HasValue && item.Runtime < MinDuration.Value)
                return false;
            if (MaxDuration.HasValue && item.Runtime > MaxDuration.Value)
                return false;

            // Genres
            if (Genres.Any())
            {
                var itemGenres = item.Genres?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim().ToLowerInvariant())
                    .ToHashSet() ?? new HashSet<string>();

                var filterGenres = Genres.Select(g => g.ToLowerInvariant()).ToHashSet();

                if (!itemGenres.Overlaps(filterGenres))
                    return false;
            }

            // Languages (check name for language indicators)
            if (Languages.Any())
            {
                var hasLanguageMatch = Languages.Any(lang =>
                    item.Name?.Contains(lang, StringComparison.OrdinalIgnoreCase) == true ||
                    item.GroupTitle?.Contains(lang, StringComparison.OrdinalIgnoreCase) == true);

                if (!hasLanguageMatch)
                    return false;
            }

            // Favorite filter
            if (IsFavorite.HasValue && item.IsFavorite != IsFavorite.Value)
                return false;

            // Has progress filter
            if (HasProgress.HasValue)
            {
                var hasActualProgress = item.HasProgress;
                if (hasActualProgress != HasProgress.Value)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Reset all filters
        /// </summary>
        public void Clear()
        {
            Genres.Clear();
            MinYear = null;
            MaxYear = null;
            MinRating = null;
            MaxRating = null;
            Languages.Clear();
            MinDuration = null;
            MaxDuration = null;
            HasSubtitles = null;
            Type = null;
            IsFavorite = null;
            HasProgress = null;
            SearchQuery = null;
        }

        /// <summary>
        /// Create a copy of this filter
        /// </summary>
        public ContentFilter Clone()
        {
            return new ContentFilter
            {
                Genres = new List<string>(Genres),
                MinYear = MinYear,
                MaxYear = MaxYear,
                MinRating = MinRating,
                MaxRating = MaxRating,
                Languages = new List<string>(Languages),
                MinDuration = MinDuration,
                MaxDuration = MaxDuration,
                HasSubtitles = HasSubtitles,
                Type = Type,
                IsFavorite = IsFavorite,
                HasProgress = HasProgress,
                SearchQuery = SearchQuery
            };
        }

        /// <summary>
        /// Get a human-readable summary of active filters
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
                parts.Add($"\"{SearchQuery}\"");

            if (Genres.Any())
                parts.Add(string.Join(", ", Genres.Take(3)));

            if (MinYear.HasValue || MaxYear.HasValue)
            {
                if (MinYear.HasValue && MaxYear.HasValue)
                    parts.Add($"{MinYear}-{MaxYear}");
                else if (MinYear.HasValue)
                    parts.Add($"Depuis {MinYear}");
                else
                    parts.Add($"Jusqu'à {MaxYear}");
            }

            if (MinRating.HasValue)
                parts.Add($"Note >= {MinRating:F1}");

            if (IsFavorite == true)
                parts.Add("Favoris");

            if (HasProgress == true)
                parts.Add("En cours");

            return parts.Any() ? string.Join(" • ", parts) : "Aucun filtre";
        }
    }

    /// <summary>
    /// Sort options for content
    /// </summary>
    public enum SortOption
    {
        Default,        // Smart score (intelligent sorting)
        NameAsc,        // A-Z
        NameDesc,       // Z-A
        DateNewest,     // Most recent first
        DateOldest,     // Oldest first
        RatingHighest,  // Best rated first
        RatingLowest,   // Lowest rated first
        Popularity,     // Most watched first
        Duration,       // Longest first
        RecentlyWatched // Recently watched first
    }

    /// <summary>
    /// Extension methods for sorting
    /// </summary>
    public static class SortExtensions
    {
        public static IEnumerable<MediaItem> ApplySort(this IEnumerable<MediaItem> items, SortOption sortOption)
        {
            return sortOption switch
            {
                SortOption.NameAsc => items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortOption.NameDesc => items.OrderByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortOption.DateNewest => items.OrderByDescending(i => i.ReleaseDate ?? DateTime.MinValue),
                SortOption.DateOldest => items.OrderBy(i => i.ReleaseDate ?? DateTime.MaxValue),
                SortOption.RatingHighest => items.OrderByDescending(i => i.Rating).ThenByDescending(i => i.VoteCount),
                SortOption.RatingLowest => items.OrderBy(i => i.Rating),
                SortOption.Popularity => items.OrderByDescending(i => i.VoteCount),
                SortOption.Duration => items.OrderByDescending(i => i.Runtime),
                SortOption.RecentlyWatched => items.OrderByDescending(i => i.LastWatched ?? DateTime.MinValue),
                _ => items // Default - keep original order
            };
        }

        public static string GetDisplayName(this SortOption sortOption)
        {
            return sortOption switch
            {
                SortOption.Default => "Recommandé",
                SortOption.NameAsc => "Nom (A-Z)",
                SortOption.NameDesc => "Nom (Z-A)",
                SortOption.DateNewest => "Plus récent",
                SortOption.DateOldest => "Plus ancien",
                SortOption.RatingHighest => "Meilleures notes",
                SortOption.RatingLowest => "Notes les plus basses",
                SortOption.Popularity => "Popularité",
                SortOption.Duration => "Durée",
                SortOption.RecentlyWatched => "Récemment vu",
                _ => "Recommandé"
            };
        }
    }
}
