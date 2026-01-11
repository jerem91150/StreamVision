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
}
