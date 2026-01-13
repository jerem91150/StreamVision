using System;
using System.Collections.Generic;

namespace StreamVision.Models
{
    /// <summary>
    /// Content preferences for personalized content filtering
    /// </summary>
    public class ContentPreferences
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Language preferences
        public List<string> PreferredLanguages { get; set; } = new() { "French" };

        // Content type preferences
        public bool ShowMovies { get; set; } = true;
        public bool ShowSeries { get; set; } = true;
        public bool ShowLiveTV { get; set; } = true;

        // Anime preferences
        public bool ShowAnime { get; set; } = true;
        public bool AnimePreferSubbed { get; set; } = true;  // VOSTFR, Subbed
        public bool AnimePreferDubbed { get; set; } = false; // VF, Dubbed

        // Genre preferences (optional)
        public List<string> PreferredGenres { get; set; } = new();
        public List<string> ExcludedGenres { get; set; } = new();

        // Sports preferences
        public List<string> PreferredSports { get; set; } = new();

        // Content filtering
        public bool AdultContentEnabled { get; set; } = false;
        public bool KidsMode { get; set; } = false;

        // UI preferences
        public bool OnboardingCompleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Helper methods for content filtering
        public bool MatchesLanguage(string language)
        {
            if (PreferredLanguages.Count == 0) return true;

            foreach (var pref in PreferredLanguages)
            {
                if (language.Contains(pref, StringComparison.OrdinalIgnoreCase) ||
                    pref.Contains(language, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Handle common variations
                if (pref.Equals("French", StringComparison.OrdinalIgnoreCase) &&
                    (language.Contains("FR", StringComparison.OrdinalIgnoreCase) ||
                     language.Contains("VF", StringComparison.OrdinalIgnoreCase) ||
                     language.Contains("Français", StringComparison.OrdinalIgnoreCase)))
                    return true;

                if (pref.Equals("English", StringComparison.OrdinalIgnoreCase) &&
                    (language.Contains("EN", StringComparison.OrdinalIgnoreCase) ||
                     language.Contains("VO", StringComparison.OrdinalIgnoreCase) ||
                     language.Contains("ENG", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        public bool MatchesContentType(ContentType type)
        {
            return type switch
            {
                ContentType.Movie => ShowMovies,
                ContentType.Series => ShowSeries,
                ContentType.Live => ShowLiveTV,
                _ => true
            };
        }
    }

    /// <summary>
    /// Available languages for content
    /// </summary>
    public static class AvailableLanguages
    {
        public static readonly List<LanguageOption> All = new()
        {
            new("French", "Français", "FR"),
            new("English", "English", "EN"),
            new("Spanish", "Español", "ES"),
            new("German", "Deutsch", "DE"),
            new("Italian", "Italiano", "IT"),
            new("Portuguese", "Português", "PT"),
            new("Arabic", "العربية", "AR"),
            new("Turkish", "Türkçe", "TR")
        };
    }

    public class LanguageOption
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Code { get; set; }
        public bool IsSelected { get; set; }

        public LanguageOption(string id, string displayName, string code)
        {
            Id = id;
            DisplayName = displayName;
            Code = code;
        }
    }

    /// <summary>
    /// Available sports for preferences
    /// </summary>
    public static class AvailableSports
    {
        public static readonly List<SportOption> All = new()
        {
            new("Football", "Football", "soccer"),
            new("Basketball", "Basketball", "basketball"),
            new("Tennis", "Tennis", "tennis"),
            new("F1", "Formule 1", "f1"),
            new("MotoGP", "MotoGP", "motogp"),
            new("Rugby", "Rugby", "rugby"),
            new("Boxing", "Boxe / MMA", "boxing"),
            new("Cycling", "Cyclisme", "cycling"),
            new("Golf", "Golf", "golf"),
            new("Hockey", "Hockey", "hockey"),
            new("American Football", "Football Am\u00e9ricain", "nfl"),
            new("Baseball", "Baseball", "baseball")
        };
    }

    public class SportOption
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }
        public bool IsSelected { get; set; }

        public SportOption(string id, string displayName, string icon)
        {
            Id = id;
            DisplayName = displayName;
            Icon = icon;
        }
    }

    /// <summary>
    /// Available genres for movies/series preferences
    /// </summary>
    public static class AvailableGenres
    {
        public static readonly List<GenreOption> All = new()
        {
            new("Action", "Action", "action"),
            new("Comedy", "Com\u00e9die", "comedy"),
            new("Drama", "Drame", "drama"),
            new("Horror", "Horreur", "horror"),
            new("SciFi", "Science-Fiction", "scifi"),
            new("Thriller", "Thriller", "thriller"),
            new("Romance", "Romance", "romance"),
            new("Animation", "Animation", "animation"),
            new("Documentary", "Documentaire", "documentary"),
            new("Crime", "Policier", "crime"),
            new("Fantasy", "Fantastique", "fantasy"),
            new("War", "Guerre", "war"),
            new("Western", "Western", "western"),
            new("Musical", "Musical", "musical"),
            new("Biography", "Biographie", "biography"),
            new("History", "Histoire", "history")
        };
    }

    public class GenreOption
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }
        public bool IsSelected { get; set; }

        public GenreOption(string id, string displayName, string icon)
        {
            Id = id;
            DisplayName = displayName;
            Icon = icon;
        }
    }
}
