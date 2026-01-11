using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Analyzes content names to extract language, version, and category information
    /// Supports anime with VOSTFR/VF detection and adapts to all languages
    /// </summary>
    public class ContentAnalyzerService
    {
        // Version patterns for different languages
        private static readonly Dictionary<string, VersionPattern[]> VersionPatterns = new()
        {
            {
                "French", new[]
                {
                    new VersionPattern("VOSTFR", ContentVersion.SubbedOriginal, "Sous-titré français"),
                    new VersionPattern("VOST", ContentVersion.SubbedOriginal, "Sous-titré"),
                    new VersionPattern("VFF", ContentVersion.Dubbed, "Version française"),
                    new VersionPattern("VF", ContentVersion.Dubbed, "Version française"),
                    new VersionPattern("TRUEFRENCH", ContentVersion.Dubbed, "TrueFrench"),
                    new VersionPattern("FRENCH", ContentVersion.Dubbed, "Français"),
                    new VersionPattern("FR", ContentVersion.Dubbed, "Français"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "Multi-langues"),
                }
            },
            {
                "English", new[]
                {
                    new VersionPattern("SUBBED", ContentVersion.SubbedOriginal, "Subbed"),
                    new VersionPattern("SUB", ContentVersion.SubbedOriginal, "Subbed"),
                    new VersionPattern("DUBBED", ContentVersion.Dubbed, "Dubbed"),
                    new VersionPattern("DUB", ContentVersion.Dubbed, "Dubbed"),
                    new VersionPattern("ENG", ContentVersion.Dubbed, "English"),
                    new VersionPattern("EN", ContentVersion.Dubbed, "English"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "Multi-language"),
                }
            },
            {
                "Spanish", new[]
                {
                    new VersionPattern("VOSE", ContentVersion.SubbedOriginal, "Subtitulado español"),
                    new VersionPattern("SUB ESP", ContentVersion.SubbedOriginal, "Subtitulado"),
                    new VersionPattern("CASTELLANO", ContentVersion.Dubbed, "Castellano"),
                    new VersionPattern("LATINO", ContentVersion.Dubbed, "Latino"),
                    new VersionPattern("ESP", ContentVersion.Dubbed, "Español"),
                    new VersionPattern("ES", ContentVersion.Dubbed, "Español"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "Multi-idioma"),
                }
            },
            {
                "German", new[]
                {
                    new VersionPattern("UNTERTITEL", ContentVersion.SubbedOriginal, "Untertitelt"),
                    new VersionPattern("SUB GER", ContentVersion.SubbedOriginal, "Untertitelt"),
                    new VersionPattern("GERMAN", ContentVersion.Dubbed, "Deutsch"),
                    new VersionPattern("GER", ContentVersion.Dubbed, "Deutsch"),
                    new VersionPattern("DE", ContentVersion.Dubbed, "Deutsch"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "Mehrsprachig"),
                }
            },
            {
                "Italian", new[]
                {
                    new VersionPattern("SUB ITA", ContentVersion.SubbedOriginal, "Sottotitolato"),
                    new VersionPattern("ITALIAN", ContentVersion.Dubbed, "Italiano"),
                    new VersionPattern("ITA", ContentVersion.Dubbed, "Italiano"),
                    new VersionPattern("IT", ContentVersion.Dubbed, "Italiano"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "Multilingue"),
                }
            },
            {
                "Portuguese", new[]
                {
                    new VersionPattern("LEGENDADO", ContentVersion.SubbedOriginal, "Legendado"),
                    new VersionPattern("SUB PT", ContentVersion.SubbedOriginal, "Legendado"),
                    new VersionPattern("DUBLADO", ContentVersion.Dubbed, "Dublado"),
                    new VersionPattern("PT-BR", ContentVersion.Dubbed, "Português BR"),
                    new VersionPattern("PT", ContentVersion.Dubbed, "Português"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "Multi-idioma"),
                }
            },
            {
                "Arabic", new[]
                {
                    new VersionPattern("مترجم", ContentVersion.SubbedOriginal, "مترجم"),
                    new VersionPattern("SUB AR", ContentVersion.SubbedOriginal, "مترجم"),
                    new VersionPattern("ARABIC", ContentVersion.Dubbed, "عربي"),
                    new VersionPattern("AR", ContentVersion.Dubbed, "عربي"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "متعدد اللغات"),
                }
            },
            {
                "Turkish", new[]
                {
                    new VersionPattern("ALTYAZI", ContentVersion.SubbedOriginal, "Altyazılı"),
                    new VersionPattern("SUB TR", ContentVersion.SubbedOriginal, "Altyazılı"),
                    new VersionPattern("TURKCE", ContentVersion.Dubbed, "Türkçe"),
                    new VersionPattern("TR", ContentVersion.Dubbed, "Türkçe"),
                    new VersionPattern("MULTI", ContentVersion.Multi, "Çok dilli"),
                }
            }
        };

        // Anime detection patterns
        private static readonly string[] AnimeIndicators = new[]
        {
            "anime", "vostfr", "vostfr", "manga", "shonen", "seinen", "shojo",
            "isekai", "mecha", "ecchi", "hentai", "oav", "ova", "ona",
            "saison", "season", "s0", "s1", "s2", "s3", "s4", "s5",
            "crunchyroll", "funimation", "wakanim", "adn"
        };

        // Category patterns
        private static readonly Dictionary<string, string[]> CategoryPatterns = new()
        {
            { "Anime", new[] { "anime", "manga", "vostfr", "shonen", "seinen", "shojo", "isekai", "oav", "ova" } },
            { "Documentary", new[] { "documentary", "documentaire", "dokument", "documental" } },
            { "Sport", new[] { "sport", "football", "soccer", "basketball", "tennis", "f1", "nba", "nfl" } },
            { "Kids", new[] { "kids", "enfant", "kinder", "children", "cartoon", "disney", "pixar", "dreamworks" } },
            { "News", new[] { "news", "info", "actualité", "nachrichten", "noticias" } },
        };

        /// <summary>
        /// Analyze a media item and return detailed content info
        /// </summary>
        public ContentAnalysis Analyze(MediaItem item)
        {
            var analysis = new ContentAnalysis
            {
                OriginalName = item.Name,
                CleanName = CleanName(item.Name),
                IsAnime = DetectAnime(item.Name, item.GroupTitle),
                DetectedVersions = new List<DetectedVersion>(),
                DetectedCategory = DetectCategory(item.Name, item.GroupTitle),
                DetectedLanguages = new List<string>()
            };

            // Detect versions for each language
            var nameUpper = item.Name.ToUpperInvariant();
            var groupUpper = (item.GroupTitle ?? "").ToUpperInvariant();
            var combined = $"{nameUpper} {groupUpper}";

            foreach (var langPatterns in VersionPatterns)
            {
                foreach (var pattern in langPatterns.Value)
                {
                    if (combined.Contains(pattern.Tag))
                    {
                        analysis.DetectedVersions.Add(new DetectedVersion
                        {
                            Language = langPatterns.Key,
                            Version = pattern.Version,
                            Tag = pattern.Tag,
                            DisplayName = pattern.DisplayName
                        });

                        if (!analysis.DetectedLanguages.Contains(langPatterns.Key))
                        {
                            analysis.DetectedLanguages.Add(langPatterns.Key);
                        }
                    }
                }
            }

            // If no version detected, try to infer from group/category
            if (analysis.DetectedVersions.Count == 0)
            {
                analysis.DetectedVersions.Add(new DetectedVersion
                {
                    Language = "Unknown",
                    Version = ContentVersion.Unknown,
                    Tag = "",
                    DisplayName = "Non déterminé"
                });
            }

            return analysis;
        }

        /// <summary>
        /// Check if content matches user preferences
        /// </summary>
        public bool MatchesPreferences(MediaItem item, ContentPreferences prefs)
        {
            var analysis = Analyze(item);

            // Check content type
            if (!prefs.MatchesContentType(item.MediaType))
                return false;

            // Check anime preference
            if (analysis.IsAnime && !prefs.ShowAnime)
                return false;

            // If no language preference set, show everything
            if (prefs.PreferredLanguages.Count == 0)
                return true;

            // Check if any detected language/version matches preferences
            foreach (var version in analysis.DetectedVersions)
            {
                // Direct language match
                if (prefs.PreferredLanguages.Contains(version.Language))
                {
                    // For anime, check version preference
                    if (analysis.IsAnime)
                    {
                        if (prefs.AnimePreferSubbed && version.Version == ContentVersion.SubbedOriginal)
                            return true;
                        if (prefs.AnimePreferDubbed && version.Version == ContentVersion.Dubbed)
                            return true;
                        if (version.Version == ContentVersion.Multi)
                            return true;
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            // If content has no language tag, include it (might be generic content)
            if (analysis.DetectedVersions.All(v => v.Language == "Unknown"))
                return true;

            // Multi-language content matches everyone
            if (analysis.DetectedVersions.Any(v => v.Version == ContentVersion.Multi))
                return true;

            return false;
        }

        /// <summary>
        /// Get a display badge for the content (e.g., "VF", "VOSTFR", "MULTI")
        /// </summary>
        public string GetVersionBadge(MediaItem item, string preferredLanguage = "French")
        {
            var analysis = Analyze(item);

            // Find version for preferred language
            var version = analysis.DetectedVersions
                .FirstOrDefault(v => v.Language == preferredLanguage);

            if (version != null)
                return version.Tag;

            // Return first detected version
            var first = analysis.DetectedVersions.FirstOrDefault();
            return first?.Tag ?? "";
        }

        private bool DetectAnime(string name, string? group)
        {
            var combined = $"{name} {group ?? ""}".ToLowerInvariant();
            return AnimeIndicators.Any(ind => combined.Contains(ind));
        }

        private string DetectCategory(string name, string? group)
        {
            var combined = $"{name} {group ?? ""}".ToLowerInvariant();

            foreach (var category in CategoryPatterns)
            {
                if (category.Value.Any(pattern => combined.Contains(pattern)))
                    return category.Key;
            }

            return "General";
        }

        private string CleanName(string name)
        {
            // Remove common tags from name for cleaner display
            var cleaned = name;

            // Remove version tags
            var tagsToRemove = new[]
            {
                "VOSTFR", "VOST", "VFF", "VF", "TRUEFRENCH", "FRENCH", "MULTI",
                "1080p", "720p", "480p", "4K", "UHD", "HDR",
                "HDTV", "WEBRIP", "BLURAY", "BDRIP", "DVDRIP",
                "x264", "x265", "HEVC", "AAC", "AC3", "DTS",
                "[", "]", "(", ")", "{", "}"
            };

            foreach (var tag in tagsToRemove)
            {
                cleaned = Regex.Replace(cleaned, Regex.Escape(tag), "", RegexOptions.IgnoreCase);
            }

            // Clean up extra spaces
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            cleaned = Regex.Replace(cleaned, @"\s*-\s*$", "").Trim();

            return cleaned;
        }
    }

    /// <summary>
    /// Result of content analysis
    /// </summary>
    public class ContentAnalysis
    {
        public string OriginalName { get; set; } = "";
        public string CleanName { get; set; } = "";
        public bool IsAnime { get; set; }
        public string DetectedCategory { get; set; } = "General";
        public List<DetectedVersion> DetectedVersions { get; set; } = new();
        public List<string> DetectedLanguages { get; set; } = new();
    }

    /// <summary>
    /// Detected version info
    /// </summary>
    public class DetectedVersion
    {
        public string Language { get; set; } = "";
        public ContentVersion Version { get; set; }
        public string Tag { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    /// <summary>
    /// Content version types
    /// </summary>
    public enum ContentVersion
    {
        Unknown,
        Dubbed,          // VF, Dubbed - Audio in target language
        SubbedOriginal,  // VOSTFR, Subbed - Original audio with subtitles
        Multi,           // MULTI - Multiple audio tracks available
        Original         // VO - Original version without subs
    }

    /// <summary>
    /// Pattern for version detection
    /// </summary>
    public class VersionPattern
    {
        public string Tag { get; }
        public ContentVersion Version { get; }
        public string DisplayName { get; }

        public VersionPattern(string tag, ContentVersion version, string displayName)
        {
            Tag = tag;
            Version = version;
            DisplayName = displayName;
        }
    }
}
