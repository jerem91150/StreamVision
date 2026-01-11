package com.streamvision.app.data.models

import androidx.room.Entity
import androidx.room.PrimaryKey
import java.util.Date
import java.util.UUID

enum class SourceType {
    M3U,
    XTREAM_CODES,
    STALKER_PORTAL
}

@Entity(tableName = "playlist_sources")
data class PlaylistSource(
    @PrimaryKey
    val id: String = UUID.randomUUID().toString(),
    val name: String,
    val type: SourceType,
    val url: String,
    val username: String? = null,
    val password: String? = null,
    val macAddress: String? = null,
    val epgUrl: String? = null,
    val lastSync: Long = System.currentTimeMillis(),
    val isActive: Boolean = true,
    val channelCount: Int = 0
)

@Entity(tableName = "channels")
data class Channel(
    @PrimaryKey
    val id: String = UUID.randomUUID().toString(),
    val sourceId: String,
    val name: String,
    val logoUrl: String? = null,
    val streamUrl: String,
    val groupTitle: String = "Uncategorized",
    val epgId: String? = null,
    val isFavorite: Boolean = false,
    val catchupDays: Int = 0,
    val order: Int = 0
)

data class ChannelGroup(
    val name: String,
    val channels: List<Channel>,
    val isExpanded: Boolean = true
) {
    val channelCount: Int get() = channels.size
}

@Entity(tableName = "epg_programs")
data class EpgProgram(
    @PrimaryKey
    val id: String = UUID.randomUUID().toString(),
    val channelId: String,
    val title: String,
    val description: String? = null,
    val startTime: Long,
    val endTime: Long,
    val category: String? = null,
    val iconUrl: String? = null
) {
    val isLive: Boolean
        get() {
            val now = System.currentTimeMillis()
            return now in startTime..endTime
        }

    val progress: Float
        get() {
            val now = System.currentTimeMillis()
            if (now < startTime) return 0f
            if (now > endTime) return 1f
            val total = endTime - startTime
            val elapsed = now - startTime
            return elapsed.toFloat() / total.toFloat()
        }

    val timeRange: String
        get() {
            val format = java.text.SimpleDateFormat("HH:mm", java.util.Locale.getDefault())
            return "${format.format(Date(startTime))} - ${format.format(Date(endTime))}"
        }
}

@Entity(tableName = "recent_channels")
data class RecentChannel(
    @PrimaryKey
    val channelId: String,
    val lastWatched: Long = System.currentTimeMillis(),
    val watchCount: Int = 1
)

data class XtreamAccountInfo(
    val username: String,
    val status: String,
    val expDate: String,
    val maxConnections: String,
    val activeConnections: String,
    val serverUrl: String
)

data class XtreamCategory(
    val categoryId: String,
    val categoryName: String,
    val parentId: String = ""
)

data class AppSettings(
    val bufferSize: Int = 2000,
    val autoPlayOnSelect: Boolean = true,
    val pipEnabled: Boolean = true,
    val backgroundPlayback: Boolean = true,
    val lastSelectedSourceId: String? = null
)
