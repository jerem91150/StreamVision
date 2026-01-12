using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StreamVision.Models;

namespace StreamVision.Services
{
    public class RecommendationEngine
    {
        private readonly DatabaseService _databaseService;
        private UserPreferences _userPreferences = new();
        private List<WatchHistoryEntry> _watchHistory = new();
        private Dictionary<string, CategoryStats> _categoryStats = new();

        // Algorithm weights
        private const double CATEGORY_AFFINITY_WEIGHT = 0.35;
        private const double TIME_RELEVANCE_WEIGHT = 0.20;
        private const double POPULARITY_WEIGHT = 0.15;
        private const double FRESHNESS_WEIGHT = 0.10;
        private const double SIMILARITY_WEIGHT = 0.20;

        // Decay settings
        private const int AFFINITY_DECAY_DAYS = 7;
        private const int MIN_WATCH_SECONDS = 10; // Ignore very short watches

        public RecommendationEngine(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task InitializeAsync()
        {
            await LoadUserDataAsync();
            await CalculateCategoryStatsAsync();
        }

        private async Task LoadUserDataAsync()
        {
            // Load watch history and preferences from database
            var historyJson = await _databaseService.GetSettingAsync("watch_history");
            var prefsJson = await _databaseService.GetSettingAsync("user_preferences");

            if (!string.IsNullOrEmpty(historyJson))
            {
                _watchHistory = Newtonsoft.Json.JsonConvert.DeserializeObject<List<WatchHistoryEntry>>(historyJson) ?? new();
            }

            if (!string.IsNullOrEmpty(prefsJson))
            {
                _userPreferences = Newtonsoft.Json.JsonConvert.DeserializeObject<UserPreferences>(prefsJson) ?? new();
            }
        }

        private async Task SaveUserDataAsync()
        {
            var historyJson = Newtonsoft.Json.JsonConvert.SerializeObject(_watchHistory);
            var prefsJson = Newtonsoft.Json.JsonConvert.SerializeObject(_userPreferences);

            await _databaseService.SaveSettingAsync("watch_history", historyJson);
            await _databaseService.SaveSettingAsync("user_preferences", prefsJson);
        }

        // Track when user starts watching
        public void StartWatching(Channel channel)
        {
            var entry = new WatchHistoryEntry
            {
                ChannelId = channel.Id,
                ChannelName = channel.Name,
                Category = channel.GroupTitle,
                StartTime = DateTime.Now,
                DayOfWeek = DateTime.Now.DayOfWeek,
                HourOfDay = DateTime.Now.Hour
            };

            _watchHistory.Add(entry);
        }

        // Track when user starts watching (MediaItem version)
        public void StartWatching(MediaItem item)
        {
            var entry = new WatchHistoryEntry
            {
                ChannelId = item.Id,
                ChannelName = item.Name,
                Category = item.GroupTitle ?? "Unknown",
                StartTime = DateTime.Now,
                DayOfWeek = DateTime.Now.DayOfWeek,
                HourOfDay = DateTime.Now.Hour
            };

            _watchHistory.Add(entry);
        }

        // Track when user stops watching
        public async Task StopWatchingAsync(Channel channel, int totalDurationSeconds, double completionPercentage)
        {
            var entry = _watchHistory.LastOrDefault(e => e.ChannelId == channel.Id && e.EndTime == null);
            if (entry != null)
            {
                entry.EndTime = DateTime.Now;
                entry.DurationSeconds = totalDurationSeconds;
                entry.CompletionPercentage = completionPercentage;

                // Only count if watched for more than minimum threshold
                if (totalDurationSeconds >= MIN_WATCH_SECONDS)
                {
                    await UpdateAffinitiesAsync(entry);
                    await SaveUserDataAsync();
                }
            }
        }

        // Track when user stops watching (MediaItem version)
        public async Task StopWatchingAsync(MediaItem item, int totalDurationSeconds, double completionPercentage = 0)
        {
            var entry = _watchHistory.LastOrDefault(e => e.ChannelId == item.Id && e.EndTime == null);
            if (entry != null)
            {
                entry.EndTime = DateTime.Now;
                entry.DurationSeconds = totalDurationSeconds;
                entry.CompletionPercentage = completionPercentage;

                // Only count if watched for more than minimum threshold
                if (totalDurationSeconds >= MIN_WATCH_SECONDS)
                {
                    await UpdateAffinitiesAsync(entry);
                    await SaveUserDataAsync();
                }
            }
        }

        private async Task UpdateAffinitiesAsync(WatchHistoryEntry entry)
        {
            // Update category affinity
            if (!_userPreferences.CategoryAffinities.ContainsKey(entry.Category))
            {
                _userPreferences.CategoryAffinities[entry.Category] = 0;
            }

            // Score based on watch duration and completion
            double watchScore = Math.Min(entry.DurationSeconds / 60.0, 30) / 30.0; // Max 30 min for full score
            double completionBonus = entry.CompletionPercentage > 0.8 ? 0.5 : 0;

            _userPreferences.CategoryAffinities[entry.Category] += watchScore + completionBonus;

            // Update time slot preferences
            int hourSlot = entry.HourOfDay / 4; // 6 time slots of 4 hours each
            if (!_userPreferences.TimeSlotPreferences.ContainsKey(hourSlot))
            {
                _userPreferences.TimeSlotPreferences[hourSlot] = new List<string>();
            }

            if (!_userPreferences.TimeSlotPreferences[hourSlot].Contains(entry.Category))
            {
                _userPreferences.TimeSlotPreferences[hourSlot].Add(entry.Category);
            }

            _userPreferences.LastUpdated = DateTime.Now;

            await Task.Run(() => CalculateCategoryStatsAsync());
        }

        private Task CalculateCategoryStatsAsync()
        {
            _categoryStats.Clear();

            var recentHistory = _watchHistory
                .Where(h => h.StartTime > DateTime.Now.AddDays(-30))
                .Where(h => h.DurationSeconds >= MIN_WATCH_SECONDS)
                .ToList();

            var grouped = recentHistory.GroupBy(h => h.Category);

            foreach (var group in grouped)
            {
                var stats = new CategoryStats
                {
                    Category = group.Key,
                    TotalWatchTimeMinutes = group.Sum(h => h.DurationSeconds) / 60,
                    WatchCount = group.Count(),
                    AverageSessionMinutes = group.Average(h => h.DurationSeconds) / 60.0
                };

                // Calculate hourly distribution
                foreach (var entry in group)
                {
                    if (!stats.HourlyDistribution.ContainsKey(entry.HourOfDay))
                        stats.HourlyDistribution[entry.HourOfDay] = 0;
                    stats.HourlyDistribution[entry.HourOfDay]++;
                }

                // Calculate affinity with time decay
                double decayedScore = 0;
                foreach (var entry in group)
                {
                    int daysSinceWatch = (DateTime.Now - entry.StartTime).Days;
                    double decayFactor = Math.Exp(-daysSinceWatch / (double)AFFINITY_DECAY_DAYS);
                    decayedScore += (entry.DurationSeconds / 60.0) * decayFactor;
                }
                stats.AffinityScore = decayedScore;

                _categoryStats[group.Key] = stats;
            }

            return Task.CompletedTask;
        }

        // Generate all recommendation sections
        public async Task<List<RecommendationSection>> GetRecommendationsAsync(List<Channel> allChannels)
        {
            var sections = new List<RecommendationSection>();

            // 1. Continue Watching
            var continueWatching = await GetContinueWatchingAsync(allChannels);
            if (continueWatching.Items.Count > 0)
                sections.Add(continueWatching);

            // 2. Top Picks For You
            var topPicks = await GetTopPicksAsync(allChannels);
            if (topPicks.Items.Count > 0)
                sections.Add(topPicks);

            // 3. Because You Watched (based on last watched category)
            var becauseYouWatched = await GetBecauseYouWatchedAsync(allChannels);
            if (becauseYouWatched.Items.Count > 0)
                sections.Add(becauseYouWatched);

            // 4. Category-specific recommendations
            var topCategories = _categoryStats
                .OrderByDescending(c => c.Value.AffinityScore)
                .Take(3)
                .ToList();

            foreach (var category in topCategories)
            {
                var categorySection = await GetCategoryRecommendationsAsync(allChannels, category.Key);
                if (categorySection.Items.Count > 0)
                    sections.Add(categorySection);
            }

            // 5. Trending Now (popular content)
            var trending = await GetTrendingAsync(allChannels);
            if (trending.Items.Count > 0)
                sections.Add(trending);

            // 6. Hidden Gems
            var hiddenGems = await GetHiddenGemsAsync(allChannels);
            if (hiddenGems.Items.Count > 0)
                sections.Add(hiddenGems);

            // 7. Time-based recommendations
            var timeBasedSection = await GetTimeBasedRecommendationsAsync(allChannels);
            if (timeBasedSection.Items.Count > 0)
                sections.Add(timeBasedSection);

            return sections;
        }

        private Task<RecommendationSection> GetContinueWatchingAsync(List<Channel> allChannels)
        {
            var section = new RecommendationSection
            {
                Title = "Continue Watching",
                Subtitle = "Pick up where you left off",
                Type = RecommendationType.ContinueWatching
            };

            var incompleteWatches = _watchHistory
                .Where(h => h.CompletionPercentage > 0.1 && h.CompletionPercentage < 0.9)
                .OrderByDescending(h => h.StartTime)
                .GroupBy(h => h.ChannelId)
                .Select(g => g.First())
                .Take(10);

            foreach (var watch in incompleteWatches)
            {
                var channel = allChannels.FirstOrDefault(c => c.Id == watch.ChannelId);
                if (channel != null)
                {
                    section.Items.Add(new RecommendationItem
                    {
                        ChannelId = channel.Id,
                        ChannelName = channel.Name,
                        LogoUrl = channel.LogoUrl ?? "",
                        Category = channel.GroupTitle,
                        StreamUrl = channel.StreamUrl,
                        Score = 1.0,
                        Reason = $"{(int)(watch.CompletionPercentage * 100)}% watched",
                        Type = RecommendationType.ContinueWatching,
                        WatchedPercentage = (int)(watch.CompletionPercentage * 100),
                        LastWatched = watch.StartTime
                    });
                }
            }

            return Task.FromResult(section);
        }

        private Task<RecommendationSection> GetTopPicksAsync(List<Channel> allChannels)
        {
            var section = new RecommendationSection
            {
                Title = "Top Picks For You",
                Subtitle = "Based on your viewing history",
                Type = RecommendationType.TopPicksForYou
            };

            var scoredChannels = allChannels
                .Select(channel => new
                {
                    Channel = channel,
                    Score = CalculateChannelScore(channel)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(15);

            foreach (var item in scoredChannels)
            {
                var reasons = GetScoreReasons(item.Channel);
                section.Items.Add(new RecommendationItem
                {
                    ChannelId = item.Channel.Id,
                    ChannelName = item.Channel.Name,
                    LogoUrl = item.Channel.LogoUrl ?? "",
                    Category = item.Channel.GroupTitle,
                    StreamUrl = item.Channel.StreamUrl,
                    Score = item.Score,
                    Reason = reasons,
                    Type = RecommendationType.TopPicksForYou
                });
            }

            return Task.FromResult(section);
        }

        private Task<RecommendationSection> GetBecauseYouWatchedAsync(List<Channel> allChannels)
        {
            var lastWatched = _watchHistory
                .Where(h => h.DurationSeconds >= 60)
                .OrderByDescending(h => h.StartTime)
                .FirstOrDefault();

            var section = new RecommendationSection
            {
                Title = lastWatched != null
                    ? $"Because You Watched {lastWatched.ChannelName}"
                    : "Recommended For You",
                Subtitle = "Similar content you might enjoy",
                Type = RecommendationType.BecauseYouWatched
            };

            if (lastWatched != null)
            {
                var similarChannels = allChannels
                    .Where(c => c.GroupTitle == lastWatched.Category && c.Id != lastWatched.ChannelId)
                    .OrderBy(_ => Guid.NewGuid()) // Shuffle
                    .Take(10);

                foreach (var channel in similarChannels)
                {
                    section.Items.Add(new RecommendationItem
                    {
                        ChannelId = channel.Id,
                        ChannelName = channel.Name,
                        LogoUrl = channel.LogoUrl ?? "",
                        Category = channel.GroupTitle,
                        StreamUrl = channel.StreamUrl,
                        Score = 0.8,
                        Reason = $"Similar to {lastWatched.ChannelName}",
                        Type = RecommendationType.BecauseYouWatched
                    });
                }
            }

            return Task.FromResult(section);
        }

        private Task<RecommendationSection> GetCategoryRecommendationsAsync(List<Channel> allChannels, string category)
        {
            var section = new RecommendationSection
            {
                Title = $"{category} For You",
                Subtitle = $"Because you love {category}",
                Type = RecommendationType.CategoryRecommendation
            };

            var watchedInCategory = _watchHistory
                .Where(h => h.Category == category)
                .Select(h => h.ChannelId)
                .ToHashSet();

            var unwatchedInCategory = allChannels
                .Where(c => c.GroupTitle == category && !watchedInCategory.Contains(c.Id))
                .OrderBy(_ => Guid.NewGuid())
                .Take(10);

            foreach (var channel in unwatchedInCategory)
            {
                section.Items.Add(new RecommendationItem
                {
                    ChannelId = channel.Id,
                    ChannelName = channel.Name,
                    LogoUrl = channel.LogoUrl ?? "",
                    Category = channel.GroupTitle,
                    StreamUrl = channel.StreamUrl,
                    Score = _categoryStats.ContainsKey(category) ? _categoryStats[category].AffinityScore / 100 : 0.5,
                    Reason = "New in your favorite category",
                    Type = RecommendationType.CategoryRecommendation
                });
            }

            return Task.FromResult(section);
        }

        private Task<RecommendationSection> GetTrendingAsync(List<Channel> allChannels)
        {
            var section = new RecommendationSection
            {
                Title = "Trending Now",
                Subtitle = "Popular content",
                Type = RecommendationType.TrendingNow
            };

            // Count watches per channel in last 7 days
            var recentWatches = _watchHistory
                .Where(h => h.StartTime > DateTime.Now.AddDays(-7))
                .GroupBy(h => h.ChannelId)
                .ToDictionary(g => g.Key, g => g.Count());

            var trending = allChannels
                .Select(c => new { Channel = c, Count = recentWatches.GetValueOrDefault(c.Id, 0) })
                .OrderByDescending(x => x.Count)
                .Take(10);

            foreach (var item in trending)
            {
                section.Items.Add(new RecommendationItem
                {
                    ChannelId = item.Channel.Id,
                    ChannelName = item.Channel.Name,
                    LogoUrl = item.Channel.LogoUrl ?? "",
                    Category = item.Channel.GroupTitle,
                    StreamUrl = item.Channel.StreamUrl,
                    Score = item.Count > 0 ? Math.Min(item.Count / 10.0, 1.0) : 0.3,
                    Reason = item.Count > 0 ? $"Watched {item.Count} times recently" : "Popular content",
                    Type = RecommendationType.TrendingNow
                });
            }

            return Task.FromResult(section);
        }

        private Task<RecommendationSection> GetHiddenGemsAsync(List<Channel> allChannels)
        {
            var section = new RecommendationSection
            {
                Title = "Hidden Gems",
                Subtitle = "Discover something new",
                Type = RecommendationType.HiddenGems
            };

            var watchedChannels = _watchHistory.Select(h => h.ChannelId).ToHashSet();

            // Get channels from favorite categories that haven't been watched
            var favoriteCategories = _categoryStats
                .OrderByDescending(c => c.Value.AffinityScore)
                .Take(5)
                .Select(c => c.Key)
                .ToHashSet();

            var hiddenGems = allChannels
                .Where(c => favoriteCategories.Contains(c.GroupTitle) && !watchedChannels.Contains(c.Id))
                .OrderBy(_ => Guid.NewGuid())
                .Take(10);

            foreach (var channel in hiddenGems)
            {
                section.Items.Add(new RecommendationItem
                {
                    ChannelId = channel.Id,
                    ChannelName = channel.Name,
                    LogoUrl = channel.LogoUrl ?? "",
                    Category = channel.GroupTitle,
                    StreamUrl = channel.StreamUrl,
                    Score = 0.6,
                    Reason = "Undiscovered content you might like",
                    Type = RecommendationType.HiddenGems
                });
            }

            return Task.FromResult(section);
        }

        private Task<RecommendationSection> GetTimeBasedRecommendationsAsync(List<Channel> allChannels)
        {
            int currentHour = DateTime.Now.Hour;
            int hourSlot = currentHour / 4;

            string timeOfDay = currentHour switch
            {
                >= 5 and < 12 => "Morning",
                >= 12 and < 17 => "Afternoon",
                >= 17 and < 21 => "Evening",
                _ => "Night"
            };

            var section = new RecommendationSection
            {
                Title = $"Perfect For {timeOfDay}",
                Subtitle = "Based on when you usually watch",
                Type = RecommendationType.TopPicksForYou
            };

            if (_userPreferences.TimeSlotPreferences.TryGetValue(hourSlot, out var preferredCategories))
            {
                var recommendations = allChannels
                    .Where(c => preferredCategories.Contains(c.GroupTitle))
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(10);

                foreach (var channel in recommendations)
                {
                    section.Items.Add(new RecommendationItem
                    {
                        ChannelId = channel.Id,
                        ChannelName = channel.Name,
                        LogoUrl = channel.LogoUrl ?? "",
                        Category = channel.GroupTitle,
                        StreamUrl = channel.StreamUrl,
                        Score = 0.7,
                        Reason = $"You usually watch {channel.GroupTitle} around this time",
                        Type = RecommendationType.TopPicksForYou
                    });
                }
            }

            return Task.FromResult(section);
        }

        private double CalculateChannelScore(Channel channel)
        {
            double score = 0;

            // 1. Category Affinity (35%)
            if (_categoryStats.TryGetValue(channel.GroupTitle, out var stats))
            {
                double maxAffinity = _categoryStats.Values.Max(s => s.AffinityScore);
                if (maxAffinity > 0)
                {
                    score += (stats.AffinityScore / maxAffinity) * CATEGORY_AFFINITY_WEIGHT;
                }
            }

            // 2. Time Relevance (20%)
            int currentHourSlot = DateTime.Now.Hour / 4;
            if (_userPreferences.TimeSlotPreferences.TryGetValue(currentHourSlot, out var preferredCategories))
            {
                if (preferredCategories.Contains(channel.GroupTitle))
                {
                    score += TIME_RELEVANCE_WEIGHT;
                }
            }

            // 3. Popularity (15%)
            var watchCount = _watchHistory.Count(h => h.ChannelId == channel.Id);
            if (watchCount > 0)
            {
                score += Math.Min(watchCount / 10.0, 1.0) * POPULARITY_WEIGHT;
            }

            // 4. Not recently watched bonus (10%) - freshness
            var lastWatch = _watchHistory
                .Where(h => h.ChannelId == channel.Id)
                .OrderByDescending(h => h.StartTime)
                .FirstOrDefault();

            if (lastWatch == null || lastWatch.StartTime < DateTime.Now.AddDays(-3))
            {
                score += FRESHNESS_WEIGHT;
            }

            // 5. Similarity to favorites (20%)
            if (channel.IsFavorite)
            {
                score += SIMILARITY_WEIGHT;
            }
            else
            {
                // Check if category matches favorite channels
                var favoriteCategories = _userPreferences.FavoriteChannelIds
                    .Select(id => _watchHistory.FirstOrDefault(h => h.ChannelId == id)?.Category)
                    .Where(c => c != null)
                    .Distinct();

                if (favoriteCategories.Contains(channel.GroupTitle))
                {
                    score += SIMILARITY_WEIGHT * 0.5;
                }
            }

            return score;
        }

        private string GetScoreReasons(Channel channel)
        {
            var reasons = new List<string>();

            if (_categoryStats.TryGetValue(channel.GroupTitle, out var stats))
            {
                if (stats.AffinityScore > 0)
                {
                    reasons.Add($"You enjoy {channel.GroupTitle}");
                }
            }

            int currentHourSlot = DateTime.Now.Hour / 4;
            if (_userPreferences.TimeSlotPreferences.TryGetValue(currentHourSlot, out var preferredCategories))
            {
                if (preferredCategories.Contains(channel.GroupTitle))
                {
                    reasons.Add("Great for this time");
                }
            }

            if (channel.IsFavorite)
            {
                reasons.Add("In your favorites");
            }

            return reasons.Count > 0 ? string.Join(" • ", reasons) : "Recommended for you";
        }

        // Get user statistics for display
        public UserStats GetUserStats()
        {
            var totalWatchTime = _watchHistory.Sum(h => h.DurationSeconds) / 60;
            var topCategory = _categoryStats
                .OrderByDescending(c => c.Value.AffinityScore)
                .FirstOrDefault();

            return new UserStats
            {
                TotalWatchTimeMinutes = totalWatchTime,
                TotalChannelsWatched = _watchHistory.Select(h => h.ChannelId).Distinct().Count(),
                FavoriteCategory = topCategory.Key ?? "None",
                WatchSessionCount = _watchHistory.Count,
                AverageSessionMinutes = _watchHistory.Count > 0
                    ? _watchHistory.Average(h => h.DurationSeconds) / 60.0
                    : 0
            };
        }

        // Clear user data
        public async Task ClearUserDataAsync()
        {
            _watchHistory.Clear();
            _userPreferences = new UserPreferences();
            _categoryStats.Clear();
            await SaveUserDataAsync();
        }

        #region Content-Based Similarity for MediaItems

        /// <summary>
        /// Calculate content similarity between two media items.
        /// Uses genres, director, cast, decade, and rating for scoring.
        /// </summary>
        public double CalculateContentSimilarity(MediaItem a, MediaItem b)
        {
            if (a == null || b == null || a.Id == b.Id) return 0;

            double score = 0;

            // 1. Genres in common (40% weight)
            score += CalculateGenreSimilarity(a, b) * 0.40;

            // 2. Same director (20% weight)
            score += CalculateDirectorSimilarity(a, b) * 0.20;

            // 3. Cast overlap (20% weight)
            score += CalculateCastSimilarity(a, b) * 0.20;

            // 4. Same decade (10% weight)
            score += CalculateDecadeSimilarity(a, b) * 0.10;

            // 5. Similar rating ±1 (10% weight)
            score += CalculateRatingSimilarity(a, b) * 0.10;

            return Math.Min(score, 1.0);
        }

        private double CalculateGenreSimilarity(MediaItem a, MediaItem b)
        {
            var genresA = ParseList(a.Genres);
            var genresB = ParseList(b.Genres);

            if (genresA.Count == 0 || genresB.Count == 0) return 0;

            var commonGenres = genresA.Intersect(genresB, StringComparer.OrdinalIgnoreCase).Count();
            return (double)commonGenres / Math.Max(genresA.Count, genresB.Count);
        }

        private double CalculateDirectorSimilarity(MediaItem a, MediaItem b)
        {
            if (string.IsNullOrWhiteSpace(a.Director) || string.IsNullOrWhiteSpace(b.Director))
                return 0;

            // Parse directors (could be multiple)
            var directorsA = ParseList(a.Director);
            var directorsB = ParseList(b.Director);

            return directorsA.Intersect(directorsB, StringComparer.OrdinalIgnoreCase).Any() ? 1.0 : 0;
        }

        private double CalculateCastSimilarity(MediaItem a, MediaItem b)
        {
            var castA = ParseList(a.Cast);
            var castB = ParseList(b.Cast);

            if (castA.Count == 0 || castB.Count == 0) return 0;

            var commonCast = castA.Intersect(castB, StringComparer.OrdinalIgnoreCase).Count();
            // More lenient: any 2 common actors is a good signal
            return Math.Min((double)commonCast / 2.0, 1.0);
        }

        private double CalculateDecadeSimilarity(MediaItem a, MediaItem b)
        {
            if (!a.ReleaseDate.HasValue || !b.ReleaseDate.HasValue) return 0;

            int decadeA = a.ReleaseDate.Value.Year / 10;
            int decadeB = b.ReleaseDate.Value.Year / 10;

            return decadeA == decadeB ? 1.0 : 0;
        }

        private double CalculateRatingSimilarity(MediaItem a, MediaItem b)
        {
            // Both should have valid ratings
            if (a.Rating <= 0 || b.Rating <= 0) return 0;

            double ratingDiff = Math.Abs(a.Rating - b.Rating);
            // Full score if within 1 point, partial for up to 2 points
            if (ratingDiff <= 1) return 1.0;
            if (ratingDiff <= 2) return 0.5;
            return 0;
        }

        private List<string> ParseList(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();

            return value.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        /// <summary>
        /// Get content similar to a given media item based on content attributes.
        /// </summary>
        public IEnumerable<MediaItem> GetSimilarContent(MediaItem sourceItem, IEnumerable<MediaItem> allItems, int count = 10)
        {
            if (sourceItem == null || allItems == null) return Enumerable.Empty<MediaItem>();

            return allItems
                .Where(item => item.Id != sourceItem.Id)
                .Select(item => new { Item = item, Score = CalculateContentSimilarity(sourceItem, item) })
                .Where(x => x.Score > 0.15) // Minimum similarity threshold
                .OrderByDescending(x => x.Score)
                .Take(count)
                .Select(x => x.Item);
        }

        /// <summary>
        /// Get recommendations for media items (movies/series) using content-based filtering.
        /// </summary>
        public async Task<List<RecommendationSection>> GetMediaRecommendationsAsync(
            IEnumerable<MediaItem> allItems,
            MediaItem? lastWatchedItem = null)
        {
            var sections = new List<RecommendationSection>();
            var itemsList = allItems.ToList();

            // 1. Continue Watching (items with progress)
            var continueWatching = GetContinueWatchingMedia(itemsList);
            if (continueWatching.Items.Count > 0)
                sections.Add(continueWatching);

            // 2. Similar to last watched
            if (lastWatchedItem != null)
            {
                var similarSection = GetSimilarToSection(lastWatchedItem, itemsList);
                if (similarSection.Items.Count > 0)
                    sections.Add(similarSection);
            }

            // 3. Top Rated
            var topRated = GetTopRatedMedia(itemsList);
            if (topRated.Items.Count > 0)
                sections.Add(topRated);

            // 4. Hidden Gems (good rating but less popular)
            var hiddenGems = GetHiddenGemsMedia(itemsList);
            if (hiddenGems.Items.Count > 0)
                sections.Add(hiddenGems);

            // 5. New Releases
            var newReleases = GetNewReleasesMedia(itemsList);
            if (newReleases.Items.Count > 0)
                sections.Add(newReleases);

            // 6. Genre-based recommendations (based on user's favorite genres)
            var genreSections = await GetGenreBasedSectionsAsync(itemsList);
            sections.AddRange(genreSections);

            return sections;
        }

        private RecommendationSection GetContinueWatchingMedia(List<MediaItem> items)
        {
            var section = new RecommendationSection
            {
                Title = "Reprendre la lecture",
                Subtitle = "Continuez où vous en étiez",
                Type = RecommendationType.ContinueWatching
            };

            var continueItems = items
                .Where(i => i.HasProgress && i.WatchProgress < 90)
                .OrderByDescending(i => i.LastWatched)
                .Take(15);

            foreach (var item in continueItems)
            {
                section.Items.Add(CreateRecommendationItem(item,
                    $"{item.WatchProgress}% regardé",
                    RecommendationType.ContinueWatching,
                    1.0));
            }

            return section;
        }

        private RecommendationSection GetSimilarToSection(MediaItem sourceItem, List<MediaItem> items)
        {
            var section = new RecommendationSection
            {
                Title = $"Similaire à {sourceItem.Name}",
                Subtitle = "Vous pourriez aussi aimer",
                Type = RecommendationType.BecauseYouWatched
            };

            var similarItems = GetSimilarContent(sourceItem, items, 15);

            foreach (var item in similarItems)
            {
                var score = CalculateContentSimilarity(sourceItem, item);
                var reason = BuildSimilarityReason(sourceItem, item);
                section.Items.Add(CreateRecommendationItem(item, reason, RecommendationType.BecauseYouWatched, score));
            }

            return section;
        }

        private string BuildSimilarityReason(MediaItem source, MediaItem target)
        {
            var reasons = new List<string>();

            // Check genre match
            var sourceGenres = ParseList(source.Genres);
            var targetGenres = ParseList(target.Genres);
            var commonGenres = sourceGenres.Intersect(targetGenres, StringComparer.OrdinalIgnoreCase).Take(2).ToList();
            if (commonGenres.Count > 0)
            {
                reasons.Add(string.Join(", ", commonGenres));
            }

            // Check director match
            if (!string.IsNullOrWhiteSpace(source.Director) &&
                source.Director.Equals(target.Director, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"Même réalisateur");
            }

            // Check cast overlap
            var sourceCast = ParseList(source.Cast);
            var targetCast = ParseList(target.Cast);
            var commonActor = sourceCast.Intersect(targetCast, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (commonActor != null)
            {
                reasons.Add($"Avec {commonActor}");
            }

            return reasons.Count > 0 ? string.Join(" • ", reasons.Take(2)) : "Contenu similaire";
        }

        private RecommendationSection GetTopRatedMedia(List<MediaItem> items)
        {
            var section = new RecommendationSection
            {
                Title = "Les mieux notés",
                Subtitle = "Plébiscités par les spectateurs",
                Type = RecommendationType.TopPicksForYou
            };

            var topRated = items
                .Where(i => i.Rating >= 7.0 && i.VoteCount >= 100)
                .OrderByDescending(i => i.Rating)
                .ThenByDescending(i => i.VoteCount)
                .Take(20);

            foreach (var item in topRated)
            {
                section.Items.Add(CreateRecommendationItem(item,
                    $"★ {item.Rating:F1} ({item.VoteCount} votes)",
                    RecommendationType.TopPicksForYou,
                    item.Rating / 10.0));
            }

            return section;
        }

        private RecommendationSection GetHiddenGemsMedia(List<MediaItem> items)
        {
            var section = new RecommendationSection
            {
                Title = "Pépites cachées",
                Subtitle = "Des trésors à découvrir",
                Type = RecommendationType.HiddenGems
            };

            // Good rating but fewer votes (less mainstream)
            var hiddenGems = items
                .Where(i => i.Rating >= 7.5 && i.VoteCount > 10 && i.VoteCount < 1000)
                .OrderByDescending(i => i.Rating)
                .Take(15);

            foreach (var item in hiddenGems)
            {
                section.Items.Add(CreateRecommendationItem(item,
                    $"★ {item.Rating:F1} - À découvrir",
                    RecommendationType.HiddenGems,
                    item.Rating / 10.0));
            }

            return section;
        }

        private RecommendationSection GetNewReleasesMedia(List<MediaItem> items)
        {
            var section = new RecommendationSection
            {
                Title = "Sorties récentes",
                Subtitle = "Les dernières nouveautés",
                Type = RecommendationType.TrendingNow
            };

            var cutoffDate = DateTime.Now.AddYears(-2);
            var newReleases = items
                .Where(i => i.ReleaseDate.HasValue && i.ReleaseDate.Value >= cutoffDate)
                .OrderByDescending(i => i.ReleaseDate)
                .Take(20);

            foreach (var item in newReleases)
            {
                var year = item.ReleaseDate?.Year.ToString() ?? "Récent";
                section.Items.Add(CreateRecommendationItem(item,
                    year,
                    RecommendationType.TrendingNow,
                    0.8));
            }

            return section;
        }

        private Task<List<RecommendationSection>> GetGenreBasedSectionsAsync(List<MediaItem> items)
        {
            var sections = new List<RecommendationSection>();

            // Extract all genres and count
            var genreCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                foreach (var genre in ParseList(item.Genres))
                {
                    if (!genreCounts.ContainsKey(genre))
                        genreCounts[genre] = 0;
                    genreCounts[genre]++;
                }
            }

            // Get top 3 genres with enough content
            var topGenres = genreCounts
                .Where(g => g.Value >= 5)
                .OrderByDescending(g => g.Value)
                .Take(3)
                .Select(g => g.Key);

            foreach (var genre in topGenres)
            {
                var section = new RecommendationSection
                {
                    Title = genre,
                    Subtitle = $"Les meilleurs films {genre.ToLower()}",
                    Type = RecommendationType.CategoryRecommendation
                };

                var genreItems = items
                    .Where(i => ParseList(i.Genres).Contains(genre, StringComparer.OrdinalIgnoreCase))
                    .Where(i => i.Rating >= 6.0)
                    .OrderByDescending(i => i.Rating)
                    .Take(15);

                foreach (var item in genreItems)
                {
                    section.Items.Add(CreateRecommendationItem(item,
                        $"★ {item.Rating:F1}",
                        RecommendationType.CategoryRecommendation,
                        item.Rating / 10.0));
                }

                if (section.Items.Count >= 3)
                    sections.Add(section);
            }

            return Task.FromResult(sections);
        }

        /// <summary>
        /// Get personalized recommendations based on user's watch history and preferences.
        /// Uses collaborative filtering combined with content-based filtering.
        /// </summary>
        public async Task<List<RecommendationSection>> GetPersonalizedMediaRecommendationsAsync(
            IEnumerable<MediaItem> allItems)
        {
            var sections = new List<RecommendationSection>();
            var itemsList = allItems.ToList();

            // Build user profile from watch history
            var userProfile = BuildUserProfile(itemsList);

            // 1. For You - based on weighted user preferences
            var forYouSection = GetForYouSection(itemsList, userProfile);
            if (forYouSection.Items.Count > 0)
                sections.Add(forYouSection);

            // 2. Because you liked [genre]
            foreach (var favoriteGenre in userProfile.TopGenres.Take(2))
            {
                var genreSection = GetBecauseYouLikedGenreSection(itemsList, favoriteGenre, userProfile);
                if (genreSection.Items.Count > 0)
                    sections.Add(genreSection);
            }

            return sections;
        }

        private UserMediaProfile BuildUserProfile(List<MediaItem> items)
        {
            var profile = new UserMediaProfile();

            // Get watched/favorited items
            var interactedItems = items
                .Where(i => i.IsFavorite || i.HasProgress)
                .ToList();

            if (interactedItems.Count == 0)
            {
                // Cold start: use all items to determine popular genres
                foreach (var item in items.Take(100))
                {
                    foreach (var genre in ParseList(item.Genres))
                    {
                        if (!profile.GenreScores.ContainsKey(genre))
                            profile.GenreScores[genre] = 0;
                        profile.GenreScores[genre] += 0.1;
                    }
                }
            }
            else
            {
                // Build profile from user interactions
                foreach (var item in interactedItems)
                {
                    double weight = item.IsFavorite ? 2.0 : 1.0;
                    if (item.WatchProgress > 80) weight *= 1.5;

                    foreach (var genre in ParseList(item.Genres))
                    {
                        if (!profile.GenreScores.ContainsKey(genre))
                            profile.GenreScores[genre] = 0;
                        profile.GenreScores[genre] += weight;
                    }

                    if (!string.IsNullOrWhiteSpace(item.Director))
                    {
                        foreach (var director in ParseList(item.Director))
                        {
                            if (!profile.DirectorScores.ContainsKey(director))
                                profile.DirectorScores[director] = 0;
                            profile.DirectorScores[director] += weight;
                        }
                    }

                    foreach (var actor in ParseList(item.Cast).Take(3))
                    {
                        if (!profile.ActorScores.ContainsKey(actor))
                            profile.ActorScores[actor] = 0;
                        profile.ActorScores[actor] += weight;
                    }
                }
            }

            // Determine top genres
            profile.TopGenres = profile.GenreScores
                .OrderByDescending(g => g.Value)
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            return profile;
        }

        private RecommendationSection GetForYouSection(List<MediaItem> items, UserMediaProfile profile)
        {
            var section = new RecommendationSection
            {
                Title = "Pour vous",
                Subtitle = "Sélection personnalisée",
                Type = RecommendationType.TopPicksForYou
            };

            var watchedIds = items
                .Where(i => i.HasProgress && i.WatchProgress > 80)
                .Select(i => i.Id)
                .ToHashSet();

            var scoredItems = items
                .Where(i => !watchedIds.Contains(i.Id))
                .Select(i => new { Item = i, Score = CalculatePersonalizedScore(i, profile) })
                .Where(x => x.Score > 0.2)
                .OrderByDescending(x => x.Score)
                .Take(20);

            foreach (var scored in scoredItems)
            {
                var reason = GetPersonalizedReason(scored.Item, profile);
                section.Items.Add(CreateRecommendationItem(scored.Item, reason,
                    RecommendationType.TopPicksForYou, scored.Score));
            }

            return section;
        }

        private double CalculatePersonalizedScore(MediaItem item, UserMediaProfile profile)
        {
            double score = 0;
            double maxGenreScore = profile.GenreScores.Values.DefaultIfEmpty(1).Max();

            // Genre match (50%)
            foreach (var genre in ParseList(item.Genres))
            {
                if (profile.GenreScores.TryGetValue(genre, out var genreScore))
                {
                    score += (genreScore / maxGenreScore) * 0.50 / ParseList(item.Genres).Count;
                }
            }

            // Director match (20%)
            foreach (var director in ParseList(item.Director))
            {
                if (profile.DirectorScores.ContainsKey(director))
                {
                    score += 0.20;
                    break;
                }
            }

            // Cast match (20%)
            foreach (var actor in ParseList(item.Cast))
            {
                if (profile.ActorScores.ContainsKey(actor))
                {
                    score += 0.05; // Up to 0.20 with 4 matching actors
                }
            }
            score = Math.Min(score, 0.90); // Cap cast contribution

            // Rating boost (10%)
            if (item.Rating >= 7.0)
            {
                score += 0.10 * (item.Rating / 10.0);
            }

            return Math.Min(score, 1.0);
        }

        private string GetPersonalizedReason(MediaItem item, UserMediaProfile profile)
        {
            var reasons = new List<string>();

            // Find matching genre
            var matchingGenre = ParseList(item.Genres)
                .FirstOrDefault(g => profile.TopGenres.Contains(g, StringComparer.OrdinalIgnoreCase));
            if (matchingGenre != null)
            {
                reasons.Add(matchingGenre);
            }

            // Find matching director
            var matchingDirector = ParseList(item.Director)
                .FirstOrDefault(d => profile.DirectorScores.ContainsKey(d));
            if (matchingDirector != null)
            {
                reasons.Add($"De {matchingDirector}");
            }

            // Find matching actor
            var matchingActor = ParseList(item.Cast)
                .FirstOrDefault(a => profile.ActorScores.ContainsKey(a));
            if (matchingActor != null)
            {
                reasons.Add($"Avec {matchingActor}");
            }

            if (item.Rating >= 8.0)
            {
                reasons.Add($"★ {item.Rating:F1}");
            }

            return reasons.Count > 0 ? string.Join(" • ", reasons.Take(2)) : "Recommandé pour vous";
        }

        private RecommendationSection GetBecauseYouLikedGenreSection(
            List<MediaItem> items, string genre, UserMediaProfile profile)
        {
            var section = new RecommendationSection
            {
                Title = $"Parce que vous aimez {genre}",
                Subtitle = "D'autres films du même genre",
                Type = RecommendationType.CategoryRecommendation
            };

            var watchedIds = items
                .Where(i => i.HasProgress && i.WatchProgress > 50)
                .Select(i => i.Id)
                .ToHashSet();

            var genreItems = items
                .Where(i => ParseList(i.Genres).Contains(genre, StringComparer.OrdinalIgnoreCase))
                .Where(i => !watchedIds.Contains(i.Id))
                .Where(i => i.Rating >= 6.5)
                .OrderByDescending(i => i.Rating)
                .ThenByDescending(i => i.VoteCount)
                .Take(15);

            foreach (var item in genreItems)
            {
                section.Items.Add(CreateRecommendationItem(item,
                    $"★ {item.Rating:F1}",
                    RecommendationType.CategoryRecommendation,
                    item.Rating / 10.0));
            }

            return section;
        }

        private RecommendationItem CreateRecommendationItem(MediaItem item, string reason,
            RecommendationType type, double score)
        {
            return new RecommendationItem
            {
                ChannelId = item.Id,
                ChannelName = item.Name ?? "Unknown",
                LogoUrl = item.PosterUrl ?? item.LogoUrl ?? "",
                Category = item.GroupTitle ?? item.Genres?.Split(',').FirstOrDefault() ?? "Unknown",
                StreamUrl = item.StreamUrl ?? "",
                Score = score,
                Reason = reason,
                Type = type,
                WatchedPercentage = (int)item.WatchProgress,
                LastWatched = item.LastWatched
            };
        }

        #endregion
    }

    /// <summary>
    /// User media profile for personalized recommendations
    /// </summary>
    public class UserMediaProfile
    {
        public Dictionary<string, double> GenreScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> DirectorScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> ActorScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> TopGenres { get; set; } = new();
    }

    public class UserStats
    {
        public int TotalWatchTimeMinutes { get; set; }
        public int TotalChannelsWatched { get; set; }
        public string FavoriteCategory { get; set; } = "";
        public int WatchSessionCount { get; set; }
        public double AverageSessionMinutes { get; set; }
    }
}
