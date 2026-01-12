using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Service intelligent pour recommandations, tri, et recherche avancée
    /// </summary>
    public class SmartContentService
    {
        // Historique de visionnage (channelId -> nombre de vues + dernière vue)
        private readonly Dictionary<string, WatchStats> _watchHistory = new();

        // Score de popularité par groupe/catégorie
        private readonly Dictionary<string, int> _categoryPopularity = new();

        // Chaînes favorites implicites (basées sur la fréquence)
        private readonly HashSet<string> _implicitFavorites = new();

        // Seuil pour considérer comme favori implicite
        private const int FavoriteThreshold = 3;

        #region Watch History & Recommendations

        /// <summary>
        /// Enregistre qu'un contenu a été regardé
        /// </summary>
        public void RecordWatch(MediaItem item)
        {
            if (item == null) return;

            // Mettre à jour l'historique
            if (!_watchHistory.ContainsKey(item.Id))
            {
                _watchHistory[item.Id] = new WatchStats { ItemId = item.Id, Name = item.Name, GroupTitle = item.GroupTitle };
            }

            var stats = _watchHistory[item.Id];
            stats.WatchCount++;
            stats.LastWatched = DateTime.Now;

            // Mettre à jour la popularité de la catégorie
            if (!string.IsNullOrEmpty(item.GroupTitle))
            {
                if (!_categoryPopularity.ContainsKey(item.GroupTitle))
                    _categoryPopularity[item.GroupTitle] = 0;
                _categoryPopularity[item.GroupTitle]++;
            }

            // Marquer comme favori implicite si regardé souvent
            if (stats.WatchCount >= FavoriteThreshold)
            {
                _implicitFavorites.Add(item.Id);
            }

            Console.WriteLine($"[Smart] Recorded watch: {item.Name} (count: {stats.WatchCount})");
        }

        /// <summary>
        /// Obtient les recommandations basées sur l'historique
        /// </summary>
        public List<MediaItem> GetRecommendations(IEnumerable<MediaItem> allItems, int count = 30)
        {
            var recommendations = new List<MediaItem>();
            var allList = allItems.ToList();

            if (_watchHistory.Count == 0)
            {
                // Pas d'historique - retourner du contenu aléatoire varié
                return allList.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
            }

            // Trouver les catégories les plus regardées
            var topCategories = _categoryPopularity
                .OrderByDescending(x => x.Value)
                .Take(5)
                .Select(x => x.Key)
                .ToList();

            // Ajouter du contenu des catégories préférées (pas encore regardé)
            var watchedIds = new HashSet<string>(_watchHistory.Keys);

            foreach (var category in topCategories)
            {
                var categoryItems = allList
                    .Where(x => x.GroupTitle == category && !watchedIds.Contains(x.Id))
                    .Take(count / topCategories.Count)
                    .ToList();

                recommendations.AddRange(categoryItems);
            }

            // Compléter avec du contenu similaire (même mot dans le nom)
            var watchedNames = _watchHistory.Values
                .OrderByDescending(x => x.WatchCount)
                .Take(10)
                .SelectMany(x => ExtractKeywords(x.Name))
                .Distinct()
                .ToList();

            foreach (var keyword in watchedNames.Take(5))
            {
                var similar = allList
                    .Where(x => x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) && !watchedIds.Contains(x.Id))
                    .Take(5);
                recommendations.AddRange(similar);
            }

            return recommendations.Distinct().Take(count).ToList();
        }

        /// <summary>
        /// Obtient "Continuer à regarder" - contenus récemment vus
        /// </summary>
        public List<string> GetRecentlyWatchedIds(int count = 20)
        {
            return _watchHistory.Values
                .OrderByDescending(x => x.LastWatched)
                .Take(count)
                .Select(x => x.ItemId)
                .ToList();
        }

        #endregion

        #region Smart Sorting

        /// <summary>
        /// Trie les contenus de manière intelligente
        /// </summary>
        public List<MediaItem> SmartSort(IEnumerable<MediaItem> items)
        {
            return items
                .Select(item => new { Item = item, Score = CalculateSmartScore(item) })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Item)
                .ToList();
        }

        private double CalculateSmartScore(MediaItem item)
        {
            double score = 0;

            // Bonus si dans les favoris implicites (+100)
            if (_implicitFavorites.Contains(item.Id))
                score += 100;

            // Bonus basé sur l'historique de visionnage (+50 * nombre de vues)
            if (_watchHistory.TryGetValue(item.Id, out var stats))
            {
                score += stats.WatchCount * 50;

                // Bonus récence (vu récemment = +30)
                var hoursSinceWatch = (DateTime.Now - stats.LastWatched).TotalHours;
                if (hoursSinceWatch < 24) score += 30;
                else if (hoursSinceWatch < 72) score += 15;
            }

            // Bonus si la catégorie est populaire
            if (!string.IsNullOrEmpty(item.GroupTitle) && _categoryPopularity.TryGetValue(item.GroupTitle, out var catPop))
            {
                score += Math.Min(catPop * 5, 50); // Max +50
            }

            // Bonus si contenu français détecté (+20)
            if (IsFrenchContent(item))
                score += 20;

            // Bonus si a une image (+5)
            if (!string.IsNullOrEmpty(item.PosterUrl) || !string.IsNullOrEmpty(item.LogoUrl))
                score += 5;

            return score;
        }

        #endregion

        #region Fuzzy Search

        /// <summary>
        /// Recherche avec tolérance aux fautes de frappe
        /// </summary>
        public List<MediaItem> FuzzySearch(IEnumerable<MediaItem> items, string query, int maxResults = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<MediaItem>();

            var queryLower = query.ToLowerInvariant().Trim();
            var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var results = items
                .Select(item => new
                {
                    Item = item,
                    Score = CalculateFuzzyScore(item, queryLower, queryWords)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x => x.Item)
                .ToList();

            return results;
        }

        private double CalculateFuzzyScore(MediaItem item, string query, string[] queryWords)
        {
            var nameLower = item.Name.ToLowerInvariant();
            var groupLower = (item.GroupTitle ?? "").ToLowerInvariant();
            double score = 0;

            // Match exact = score maximum
            if (nameLower.Contains(query))
            {
                score += 100;
                // Bonus si commence par la query
                if (nameLower.StartsWith(query))
                    score += 50;
            }

            // Match dans le groupe
            if (groupLower.Contains(query))
                score += 30;

            // Match par mots individuels
            foreach (var word in queryWords)
            {
                if (word.Length < 2) continue;

                if (nameLower.Contains(word))
                    score += 20;
                else
                {
                    // Fuzzy match - distance de Levenshtein
                    var nameWords = nameLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var nameWord in nameWords)
                    {
                        var distance = LevenshteinDistance(word, nameWord);
                        var maxLen = Math.Max(word.Length, nameWord.Length);
                        var similarity = 1.0 - ((double)distance / maxLen);

                        // Si similarité > 70%, considérer comme match
                        if (similarity > 0.7)
                        {
                            score += similarity * 15;
                        }
                    }
                }
            }

            // Boost pour les favoris implicites
            if (_implicitFavorites.Contains(item.Id))
                score += 25;

            return score;
        }

        /// <summary>
        /// Calcule la distance de Levenshtein entre deux chaînes
        /// </summary>
        private static int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[s1.Length, s2.Length];
        }

        #endregion

        #region French Detection

        /// <summary>
        /// Détecte si le contenu est français avec plusieurs méthodes
        /// </summary>
        public bool IsFrenchContent(MediaItem item)
        {
            var name = item.Name.ToUpperInvariant();
            var group = (item.GroupTitle ?? "").ToUpperInvariant();
            var combined = $"{name} {group}";

            // Patterns explicites
            var frenchPatterns = new[]
            {
                "FR:", "FR |", "FR-", "|FR|", "(FR)", "[FR]",
                "VOSTFR", "VFF", "VF ", "TRUEFRENCH", "FRENCH", "FRANCE",
                "MULTI"
            };

            if (frenchPatterns.Any(p => combined.Contains(p)))
                return true;

            // Chaînes françaises connues
            var frenchChannels = new[]
            {
                "TF1", "FRANCE 2", "FRANCE 3", "FRANCE 4", "FRANCE 5", "FRANCE 24",
                "CANAL+", "CANAL PLUS", "C8", "CSTAR",
                "M6", "W9", "6TER", "GULLI", "TIJI",
                "ARTE", "TMC", "TFX", "NRJ", "CHERIE",
                "BFM", "CNEWS", "LCI", "FRANCEINFO",
                "RMC", "SPORT", "EQUIPE",
                "OCS", "CINE+", "PARAMOUNT", "DISNEY",
                "BEIN", "EUROSPORT",
                "RTL", "EUROPE 1", "RADIO"
            };

            if (frenchChannels.Any(ch => combined.Contains(ch)))
                return true;

            // Mots français courants dans les titres
            var frenchWords = new[]
            {
                " LE ", " LA ", " LES ", " DES ", " DU ", " DE ", " ET ", " EN ",
                " SAISON ", " EPISODE ", " FILM ", " SERIE ",
                " NOUVEAU", " DIRECT", " EMISSION", " JOURNAL", " METEO"
            };

            if (frenchWords.Any(w => $" {combined} ".Contains(w)))
                return true;

            // Groupes contenant des indicateurs français
            var frenchGroupIndicators = new[]
            {
                "FRANCE", "FRANÇAIS", "FRANCAIS", "FRENCH", "FR ", " FR"
            };

            if (frenchGroupIndicators.Any(g => group.Contains(g)))
                return true;

            return false;
        }

        #endregion

        #region Helpers

        private List<string> ExtractKeywords(string name)
        {
            // Extraire les mots significatifs (> 3 caractères)
            return Regex.Split(name, @"\W+")
                .Where(w => w.Length > 3)
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Obtient les IDs des favoris implicites
        /// </summary>
        public HashSet<string> GetImplicitFavoriteIds() => _implicitFavorites;

        /// <summary>
        /// Charge l'historique depuis la base de données
        /// </summary>
        public void LoadHistory(IEnumerable<WatchStats> history)
        {
            foreach (var stat in history)
            {
                _watchHistory[stat.ItemId] = stat;
                if (stat.WatchCount >= FavoriteThreshold)
                    _implicitFavorites.Add(stat.ItemId);

                if (!string.IsNullOrEmpty(stat.GroupTitle))
                {
                    if (!_categoryPopularity.ContainsKey(stat.GroupTitle))
                        _categoryPopularity[stat.GroupTitle] = 0;
                    _categoryPopularity[stat.GroupTitle] += stat.WatchCount;
                }
            }
            Console.WriteLine($"[Smart] Loaded {_watchHistory.Count} watch history entries, {_implicitFavorites.Count} implicit favorites");
        }

        /// <summary>
        /// Exporte l'historique pour sauvegarde
        /// </summary>
        public List<WatchStats> ExportHistory() => _watchHistory.Values.ToList();

        #endregion
    }

    /// <summary>
    /// Statistiques de visionnage pour un contenu
    /// </summary>
    public class WatchStats
    {
        public string ItemId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? GroupTitle { get; set; }
        public int WatchCount { get; set; }
        public DateTime LastWatched { get; set; }
    }
}
