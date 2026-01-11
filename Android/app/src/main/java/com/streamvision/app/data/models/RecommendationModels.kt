package com.streamvision.app.data.models

import androidx.room.Entity
import androidx.room.PrimaryKey
import java.util.UUID

// Watch History Entry
@Entity(tableName = "watch_history")
data class WatchHistoryEntry(
    @PrimaryKey
    val id: String = UUID.randomUUID().toString(),
    val channelId: String,
    val channelName: String,
    val category: String,
    val startTime: Long,
    val endTime: Long? = null,
    val durationSeconds: Int = 0,
    val completionPercentage: Double = 0.0,
    val dayOfWeek: Int,
    val hourOfDay: Int
)

// User Preferences
@Entity(tableName = "user_preferences")
data class UserPreferencesEntity(
    @PrimaryKey
    val id: String = "default",
    val categoryAffinitiesJson: String = "{}",
    val timeSlotPreferencesJson: String = "{}",
    val favoriteChannelIdsJson: String = "[]",
    val dislikedChannelIdsJson: String = "[]",
    val lastUpdated: Long = System.currentTimeMillis()
)

// Recommendation Item
data class RecommendationItem(
    val channelId: String,
    val channelName: String,
    val logoUrl: String? = null,
    val category: String,
    val streamUrl: String,
    val score: Double,
    val reason: String,
    val type: RecommendationType,
    val watchedPercentage: Int = 0,
    val lastWatched: Long? = null
)

// Recommendation Type
enum class RecommendationType {
    CONTINUE_WATCHING,
    BECAUSE_YOU_WATCHED,
    TOP_PICKS_FOR_YOU,
    TRENDING_NOW,
    NEW_RELEASES,
    CATEGORY_RECOMMENDATION,
    HIDDEN_GEMS,
    SIMILAR_CONTENT,
    TIME_BASED_PICKS
}

// Recommendation Section
data class RecommendationSection(
    val title: String,
    val subtitle: String,
    val type: RecommendationType,
    val items: List<RecommendationItem>
)

// Category Statistics
data class CategoryStats(
    val category: String,
    val totalWatchTimeMinutes: Int,
    val watchCount: Int,
    val averageSessionMinutes: Double,
    val affinityScore: Double,
    val hourlyDistribution: Map<Int, Int> = emptyMap()
)

// User Statistics
data class UserStats(
    val totalWatchTimeMinutes: Int,
    val totalChannelsWatched: Int,
    val favoriteCategory: String,
    val watchSessionCount: Int,
    val averageSessionMinutes: Double
)
