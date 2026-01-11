package com.streamvision.app.data.repository

import com.streamvision.app.data.local.StreamDatabase
import com.streamvision.app.data.models.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.util.*
import javax.inject.Inject
import javax.inject.Singleton
import kotlin.math.exp
import kotlin.math.ln
import kotlin.math.max
import kotlin.math.min

@Singleton
class RecommendationEngine @Inject constructor(
    private val database: StreamDatabase
) {
    // Scoring weights
    private val categoryAffinityWeight = 0.35
    private val timeRelevanceWeight = 0.20
    private val popularityWeight = 0.15
    private val freshnessWeight = 0.10
    private val similarityWeight = 0.20

    // Time decay factor (half-life in days)
    private val timeDecayHalfLife = 7.0

    // Cache for performance
    private var cachedPreferences: UserPreferencesEntity? = null
    private var cachedCategoryStats: Map<String, CategoryStats>? = null
    private var lastCacheUpdate: Long = 0
    private val cacheValidityMs = 5 * 60 * 1000L // 5 minutes

    // Track watch session
    suspend fun trackWatchStart(channel: Channel) = withContext(Dispatchers.IO) {
        val calendar = Calendar.getInstance()
        val entry = WatchHistoryEntry(
            channelId = channel.id,
            channelName = channel.name,
            category = channel.groupTitle,
            startTime = System.currentTimeMillis(),
            dayOfWeek = calendar.get(Calendar.DAY_OF_WEEK),
            hourOfDay = calendar.get(Calendar.HOUR_OF_DAY)
        )
        database.watchHistoryDao().insert(entry)
        entry.id
    }

    suspend fun trackWatchEnd(entryId: String, durationSeconds: Int, completionPercentage: Double) = withContext(Dispatchers.IO) {
        database.watchHistoryDao().updateWatchEnd(
            id = entryId,
            endTime = System.currentTimeMillis(),
            durationSeconds = durationSeconds,
            completionPercentage = completionPercentage
        )
        invalidateCache()
        updateUserPreferences()
    }

    // Generate all recommendation sections
    fun getRecommendations(channels: List<Channel>): Flow<List<RecommendationSection>> = flow {
        val sections = mutableListOf<RecommendationSection>()

        // Continue Watching
        val continueWatching = getContinueWatching(channels)
        if (continueWatching.isNotEmpty()) {
            sections.add(RecommendationSection(
                title = "Continue Watching",
                subtitle = "Pick up where you left off",
                type = RecommendationType.CONTINUE_WATCHING,
                items = continueWatching
            ))
        }

        // Top Picks For You
        val topPicks = getTopPicksForYou(channels)
        if (topPicks.isNotEmpty()) {
            sections.add(RecommendationSection(
                title = "Top Picks For You",
                subtitle = "Based on your viewing history",
                type = RecommendationType.TOP_PICKS_FOR_YOU,
                items = topPicks
            ))
        }

        // Time-Based Recommendations
        val timeBasedPicks = getTimeBasedRecommendations(channels)
        if (timeBasedPicks.isNotEmpty()) {
            sections.add(RecommendationSection(
                title = "Perfect For Right Now",
                subtitle = "Channels you usually watch at this time",
                type = RecommendationType.TIME_BASED_PICKS,
                items = timeBasedPicks
            ))
        }

        // Because You Watched (based on recent history)
        val recentHistory = withContext(Dispatchers.IO) {
            database.watchHistoryDao().getRecentHistory(limit = 5)
        }
        for (historyEntry in recentHistory.take(2)) {
            val similar = getSimilarContent(historyEntry, channels)
            if (similar.isNotEmpty()) {
                sections.add(RecommendationSection(
                    title = "Because You Watched ${historyEntry.channelName}",
                    subtitle = "Similar content you might enjoy",
                    type = RecommendationType.BECAUSE_YOU_WATCHED,
                    items = similar
                ))
            }
        }

        // Category-based recommendations
        val categoryStats = getCategoryStats()
        val topCategories = categoryStats.values
            .sortedByDescending { it.affinityScore }
            .take(3)

        for (categoryStat in topCategories) {
            val categoryItems = getCategoryRecommendations(categoryStat.category, channels)
            if (categoryItems.isNotEmpty()) {
                sections.add(RecommendationSection(
                    title = "Best in ${categoryStat.category}",
                    subtitle = "Top rated in your favorite category",
                    type = RecommendationType.CATEGORY_RECOMMENDATION,
                    items = categoryItems
                ))
            }
        }

        // Hidden Gems
        val hiddenGems = getHiddenGems(channels)
        if (hiddenGems.isNotEmpty()) {
            sections.add(RecommendationSection(
                title = "Hidden Gems",
                subtitle = "Discover something new",
                type = RecommendationType.HIDDEN_GEMS,
                items = hiddenGems
            ))
        }

        emit(sections)
    }

    // Continue Watching - incomplete sessions
    private suspend fun getContinueWatching(channels: List<Channel>): List<RecommendationItem> = withContext(Dispatchers.IO) {
        val incompleteHistory = database.watchHistoryDao().getIncompleteWatches(
            minCompletion = 0.1,
            maxCompletion = 0.9
        )

        incompleteHistory.mapNotNull { history ->
            channels.find { it.id == history.channelId }?.let { channel ->
                RecommendationItem(
                    channelId = channel.id,
                    channelName = channel.name,
                    logoUrl = channel.logoUrl,
                    category = channel.groupTitle,
                    streamUrl = channel.streamUrl,
                    score = 1.0 - (history.completionPercentage * 0.5), // Higher score for less completed
                    reason = "${(history.completionPercentage * 100).toInt()}% watched",
                    type = RecommendationType.CONTINUE_WATCHING,
                    watchedPercentage = (history.completionPercentage * 100).toInt(),
                    lastWatched = history.startTime
                )
            }
        }.sortedByDescending { it.lastWatched }.take(10)
    }

    // Top Picks - personalized scoring
    private suspend fun getTopPicksForYou(channels: List<Channel>): List<RecommendationItem> = withContext(Dispatchers.IO) {
        val preferences = getUserPreferences()
        val categoryStats = getCategoryStats()
        val watchedChannelIds = database.watchHistoryDao().getAllWatchedChannelIds().toSet()

        channels
            .filter { it.id !in watchedChannelIds } // Exclude already watched
            .map { channel ->
                val score = calculateScore(channel, preferences, categoryStats)
                RecommendationItem(
                    channelId = channel.id,
                    channelName = channel.name,
                    logoUrl = channel.logoUrl,
                    category = channel.groupTitle,
                    streamUrl = channel.streamUrl,
                    score = score,
                    reason = generateReason(channel, preferences, categoryStats),
                    type = RecommendationType.TOP_PICKS_FOR_YOU
                )
            }
            .sortedByDescending { it.score }
            .take(15)
    }

    // Time-based recommendations
    private suspend fun getTimeBasedRecommendations(channels: List<Channel>): List<RecommendationItem> = withContext(Dispatchers.IO) {
        val currentHour = Calendar.getInstance().get(Calendar.HOUR_OF_DAY)
        val preferences = getUserPreferences()

        val categoryAffinities = parseJsonToMap(preferences.categoryAffinitiesJson)
        val timeSlotPreferences = parseTimeSlotPreferences(preferences.timeSlotPreferencesJson)

        // Get categories typically watched at this hour
        val hourlyCategories = timeSlotPreferences[currentHour] ?: emptyList()

        if (hourlyCategories.isEmpty()) {
            return@withContext emptyList()
        }

        channels
            .filter { it.groupTitle in hourlyCategories }
            .map { channel ->
                val categoryAffinity = categoryAffinities[channel.groupTitle] ?: 0.5
                RecommendationItem(
                    channelId = channel.id,
                    channelName = channel.name,
                    logoUrl = channel.logoUrl,
                    category = channel.groupTitle,
                    streamUrl = channel.streamUrl,
                    score = categoryAffinity,
                    reason = "Usually watched at ${formatHour(currentHour)}",
                    type = RecommendationType.TIME_BASED_PICKS
                )
            }
            .sortedByDescending { it.score }
            .take(10)
    }

    // Similar content based on a watched item
    private suspend fun getSimilarContent(historyEntry: WatchHistoryEntry, channels: List<Channel>): List<RecommendationItem> = withContext(Dispatchers.IO) {
        val watchedIds = database.watchHistoryDao().getAllWatchedChannelIds().toSet()

        channels
            .filter { it.id != historyEntry.channelId && it.id !in watchedIds }
            .filter { it.groupTitle == historyEntry.category }
            .map { channel ->
                val similarityScore = calculateSimilarity(historyEntry, channel)
                RecommendationItem(
                    channelId = channel.id,
                    channelName = channel.name,
                    logoUrl = channel.logoUrl,
                    category = channel.groupTitle,
                    streamUrl = channel.streamUrl,
                    score = similarityScore,
                    reason = "Similar to ${historyEntry.channelName}",
                    type = RecommendationType.SIMILAR_CONTENT
                )
            }
            .sortedByDescending { it.score }
            .take(8)
    }

    // Category-specific recommendations
    private suspend fun getCategoryRecommendations(category: String, channels: List<Channel>): List<RecommendationItem> = withContext(Dispatchers.IO) {
        val watchedIds = database.watchHistoryDao().getAllWatchedChannelIds().toSet()
        val categoryHistory = database.watchHistoryDao().getHistoryByCategory(category)

        channels
            .filter { it.groupTitle == category && it.id !in watchedIds }
            .map { channel ->
                RecommendationItem(
                    channelId = channel.id,
                    channelName = channel.name,
                    logoUrl = channel.logoUrl,
                    category = channel.groupTitle,
                    streamUrl = channel.streamUrl,
                    score = 0.7 + (Math.random() * 0.3), // Base score with some variation
                    reason = "Popular in $category",
                    type = RecommendationType.CATEGORY_RECOMMENDATION
                )
            }
            .sortedByDescending { it.score }
            .take(10)
    }

    // Hidden Gems - unwatched content in unexplored categories
    private suspend fun getHiddenGems(channels: List<Channel>): List<RecommendationItem> = withContext(Dispatchers.IO) {
        val categoryStats = getCategoryStats()
        val watchedIds = database.watchHistoryDao().getAllWatchedChannelIds().toSet()

        // Find categories with low watch time
        val exploredCategories = categoryStats.keys
        val allCategories = channels.map { it.groupTitle }.distinct()
        val unexploredCategories = allCategories.filter { it !in exploredCategories }

        val hiddenGemCategories = if (unexploredCategories.isNotEmpty()) {
            unexploredCategories
        } else {
            categoryStats.entries
                .sortedBy { it.value.watchCount }
                .take(3)
                .map { it.key }
        }

        channels
            .filter { it.groupTitle in hiddenGemCategories && it.id !in watchedIds }
            .shuffled()
            .take(10)
            .map { channel ->
                RecommendationItem(
                    channelId = channel.id,
                    channelName = channel.name,
                    logoUrl = channel.logoUrl,
                    category = channel.groupTitle,
                    streamUrl = channel.streamUrl,
                    score = 0.6 + (Math.random() * 0.4),
                    reason = "Explore ${channel.groupTitle}",
                    type = RecommendationType.HIDDEN_GEMS
                )
            }
    }

    // Calculate overall score for a channel
    private fun calculateScore(
        channel: Channel,
        preferences: UserPreferencesEntity,
        categoryStats: Map<String, CategoryStats>
    ): Double {
        val categoryAffinities = parseJsonToMap(preferences.categoryAffinitiesJson)

        // Category affinity score
        val categoryScore = categoryAffinities[channel.groupTitle] ?: 0.3

        // Time relevance score
        val currentHour = Calendar.getInstance().get(Calendar.HOUR_OF_DAY)
        val timeSlotPrefs = parseTimeSlotPreferences(preferences.timeSlotPreferencesJson)
        val hourCategories = timeSlotPrefs[currentHour] ?: emptyList()
        val timeScore = if (channel.groupTitle in hourCategories) 1.0 else 0.5

        // Popularity score (based on category watch count)
        val catStats = categoryStats[channel.groupTitle]
        val popularityScore = if (catStats != null) {
            min(1.0, catStats.watchCount / 50.0)
        } else 0.3

        // Freshness score (channels not watched recently get higher scores)
        val freshnessScore = 0.7 // Default for unwatched channels

        // Similarity score (placeholder - could be enhanced with content analysis)
        val similarityScore = 0.5

        return (categoryScore * categoryAffinityWeight) +
               (timeScore * timeRelevanceWeight) +
               (popularityScore * popularityWeight) +
               (freshnessScore * freshnessWeight) +
               (similarityScore * similarityWeight)
    }

    // Calculate similarity between a history entry and a channel
    private fun calculateSimilarity(history: WatchHistoryEntry, channel: Channel): Double {
        var score = 0.0

        // Same category is a strong signal
        if (history.category == channel.groupTitle) {
            score += 0.6
        }

        // Name similarity (basic word matching)
        val historyWords = history.channelName.lowercase().split(" ", "-", "_")
        val channelWords = channel.name.lowercase().split(" ", "-", "_")
        val commonWords = historyWords.intersect(channelWords.toSet()).size
        if (commonWords > 0) {
            score += min(0.3, commonWords * 0.1)
        }

        // Add some randomness for variety
        score += Math.random() * 0.1

        return min(1.0, score)
    }

    // Generate human-readable reason
    private fun generateReason(
        channel: Channel,
        preferences: UserPreferencesEntity,
        categoryStats: Map<String, CategoryStats>
    ): String {
        val categoryAffinities = parseJsonToMap(preferences.categoryAffinitiesJson)
        val affinity = categoryAffinities[channel.groupTitle] ?: 0.0

        return when {
            affinity > 0.8 -> "You love ${channel.groupTitle}"
            affinity > 0.6 -> "Based on your ${channel.groupTitle} history"
            affinity > 0.4 -> "You might enjoy this"
            else -> "Recommended for you"
        }
    }

    // Update user preferences based on watch history
    private suspend fun updateUserPreferences() = withContext(Dispatchers.IO) {
        val history = database.watchHistoryDao().getAllHistory()
        if (history.isEmpty()) return@withContext

        val now = System.currentTimeMillis()

        // Calculate category affinities with time decay
        val categoryWatchTime = mutableMapOf<String, Double>()
        val categoryHourDistribution = mutableMapOf<String, MutableMap<Int, Int>>()

        for (entry in history) {
            val ageInDays = (now - entry.startTime) / (1000.0 * 60 * 60 * 24)
            val decayFactor = exp(-ln(2.0) * ageInDays / timeDecayHalfLife)
            val weightedDuration = entry.durationSeconds * decayFactor

            categoryWatchTime[entry.category] =
                (categoryWatchTime[entry.category] ?: 0.0) + weightedDuration

            // Track hourly distribution
            val hourDist = categoryHourDistribution.getOrPut(entry.category) { mutableMapOf() }
            hourDist[entry.hourOfDay] = (hourDist[entry.hourOfDay] ?: 0) + 1
        }

        // Normalize to 0-1 range
        val maxWatchTime = categoryWatchTime.values.maxOrNull() ?: 1.0
        val categoryAffinities = categoryWatchTime.mapValues { (_, time) ->
            time / maxWatchTime
        }

        // Build time slot preferences
        val timeSlotPreferences = mutableMapOf<Int, List<String>>()
        for (hour in 0..23) {
            val categoriesAtHour = categoryHourDistribution
                .filter { (_, hourDist) -> (hourDist[hour] ?: 0) > 0 }
                .map { (category, hourDist) -> category to (hourDist[hour] ?: 0) }
                .sortedByDescending { it.second }
                .take(3)
                .map { it.first }

            if (categoriesAtHour.isNotEmpty()) {
                timeSlotPreferences[hour] = categoriesAtHour
            }
        }

        // Get favorite channels (most watched)
        val channelWatchCounts = history.groupBy { it.channelId }
            .mapValues { it.value.size }
            .entries
            .sortedByDescending { it.value }
            .take(20)
            .map { it.key }

        // Save preferences
        val prefsEntity = UserPreferencesEntity(
            id = "default",
            categoryAffinitiesJson = JSONObject(categoryAffinities).toString(),
            timeSlotPreferencesJson = buildTimeSlotJson(timeSlotPreferences),
            favoriteChannelIdsJson = JSONArray(channelWatchCounts).toString(),
            dislikedChannelIdsJson = "[]",
            lastUpdated = now
        )

        database.userPreferencesDao().insert(prefsEntity)
        cachedPreferences = prefsEntity
    }

    // Get user preferences (with caching)
    private suspend fun getUserPreferences(): UserPreferencesEntity = withContext(Dispatchers.IO) {
        if (cachedPreferences != null && isCacheValid()) {
            return@withContext cachedPreferences!!
        }

        val prefs = database.userPreferencesDao().getPreferences()
            ?: UserPreferencesEntity()
        cachedPreferences = prefs
        lastCacheUpdate = System.currentTimeMillis()
        prefs
    }

    // Get category statistics
    private suspend fun getCategoryStats(): Map<String, CategoryStats> = withContext(Dispatchers.IO) {
        if (cachedCategoryStats != null && isCacheValid()) {
            return@withContext cachedCategoryStats!!
        }

        val history = database.watchHistoryDao().getAllHistory()
        val stats = mutableMapOf<String, CategoryStats>()

        history.groupBy { it.category }.forEach { (category, entries) ->
            val totalMinutes = entries.sumOf { it.durationSeconds } / 60
            val watchCount = entries.size
            val avgSession = if (watchCount > 0) totalMinutes.toDouble() / watchCount else 0.0

            val hourlyDist = entries.groupBy { it.hourOfDay }
                .mapValues { it.value.size }

            val preferences = getUserPreferences()
            val affinities = parseJsonToMap(preferences.categoryAffinitiesJson)
            val affinity = affinities[category] ?: 0.5

            stats[category] = CategoryStats(
                category = category,
                totalWatchTimeMinutes = totalMinutes,
                watchCount = watchCount,
                averageSessionMinutes = avgSession,
                affinityScore = affinity,
                hourlyDistribution = hourlyDist
            )
        }

        cachedCategoryStats = stats
        stats
    }

    // Get user statistics
    suspend fun getUserStats(): UserStats = withContext(Dispatchers.IO) {
        val history = database.watchHistoryDao().getAllHistory()

        if (history.isEmpty()) {
            return@withContext UserStats(
                totalWatchTimeMinutes = 0,
                totalChannelsWatched = 0,
                favoriteCategory = "None",
                watchSessionCount = 0,
                averageSessionMinutes = 0.0
            )
        }

        val totalMinutes = history.sumOf { it.durationSeconds } / 60
        val uniqueChannels = history.map { it.channelId }.distinct().size
        val sessionCount = history.size
        val avgSession = totalMinutes.toDouble() / sessionCount

        val favoriteCategory = history
            .groupBy { it.category }
            .maxByOrNull { it.value.sumOf { e -> e.durationSeconds } }
            ?.key ?: "None"

        UserStats(
            totalWatchTimeMinutes = totalMinutes,
            totalChannelsWatched = uniqueChannels,
            favoriteCategory = favoriteCategory,
            watchSessionCount = sessionCount,
            averageSessionMinutes = avgSession
        )
    }

    // Helper functions
    private fun isCacheValid(): Boolean {
        return System.currentTimeMillis() - lastCacheUpdate < cacheValidityMs
    }

    private fun invalidateCache() {
        cachedPreferences = null
        cachedCategoryStats = null
        lastCacheUpdate = 0
    }

    private fun parseJsonToMap(json: String): Map<String, Double> {
        return try {
            val obj = JSONObject(json)
            obj.keys().asSequence().associateWith { obj.getDouble(it) }
        } catch (e: Exception) {
            emptyMap()
        }
    }

    private fun parseTimeSlotPreferences(json: String): Map<Int, List<String>> {
        return try {
            val obj = JSONObject(json)
            obj.keys().asSequence().associate { key ->
                val arr = obj.getJSONArray(key)
                key.toInt() to (0 until arr.length()).map { arr.getString(it) }
            }
        } catch (e: Exception) {
            emptyMap()
        }
    }

    private fun buildTimeSlotJson(prefs: Map<Int, List<String>>): String {
        val obj = JSONObject()
        prefs.forEach { (hour, categories) ->
            obj.put(hour.toString(), JSONArray(categories))
        }
        return obj.toString()
    }

    private fun formatHour(hour: Int): String {
        return when {
            hour == 0 -> "12 AM"
            hour < 12 -> "$hour AM"
            hour == 12 -> "12 PM"
            else -> "${hour - 12} PM"
        }
    }
}
