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
