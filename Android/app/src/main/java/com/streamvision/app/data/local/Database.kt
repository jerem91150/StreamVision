package com.streamvision.app.data.local

import androidx.room.*
import com.streamvision.app.data.models.*
import kotlinx.coroutines.flow.Flow

@Database(
    entities = [
        PlaylistSource::class,
        Channel::class,
        EpgProgram::class,
        RecentChannel::class,
        WatchHistoryEntry::class,
        UserPreferencesEntity::class
    ],
    version = 2,
    exportSchema = false
)
@TypeConverters(Converters::class)
abstract class AppDatabase : RoomDatabase() {
    abstract fun playlistSourceDao(): PlaylistSourceDao
    abstract fun channelDao(): ChannelDao
    abstract fun epgProgramDao(): EpgProgramDao
    abstract fun recentChannelDao(): RecentChannelDao
    abstract fun watchHistoryDao(): WatchHistoryDao
    abstract fun userPreferencesDao(): UserPreferencesDao
}

// Type alias for RecommendationEngine compatibility
typealias StreamDatabase = AppDatabase

class Converters {
    @TypeConverter
    fun fromSourceType(value: SourceType): String = value.name

    @TypeConverter
    fun toSourceType(value: String): SourceType = SourceType.valueOf(value)
}

@Dao
interface PlaylistSourceDao {
    @Query("SELECT * FROM playlist_sources ORDER BY name")
    fun getAllSources(): Flow<List<PlaylistSource>>

    @Query("SELECT * FROM playlist_sources WHERE id = :id")
    suspend fun getSourceById(id: String): PlaylistSource?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertSource(source: PlaylistSource)

    @Update
    suspend fun updateSource(source: PlaylistSource)

    @Delete
    suspend fun deleteSource(source: PlaylistSource)

    @Query("DELETE FROM playlist_sources WHERE id = :id")
    suspend fun deleteSourceById(id: String)
}

@Dao
interface ChannelDao {
    @Query("SELECT * FROM channels WHERE sourceId = :sourceId ORDER BY groupTitle, `order`, name")
    fun getChannelsBySource(sourceId: String): Flow<List<Channel>>

    @Query("SELECT * FROM channels WHERE isFavorite = 1 ORDER BY `order`, name")
    fun getFavoriteChannels(): Flow<List<Channel>>

    @Query("SELECT * FROM channels WHERE id = :id")
    suspend fun getChannelById(id: String): Channel?

    @Query("SELECT * FROM channels WHERE name LIKE '%' || :query || '%' OR groupTitle LIKE '%' || :query || '%'")
    fun searchChannels(query: String): Flow<List<Channel>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertChannels(channels: List<Channel>)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertChannel(channel: Channel)

    @Update
    suspend fun updateChannel(channel: Channel)

    @Query("UPDATE channels SET isFavorite = :isFavorite WHERE id = :channelId")
    suspend fun updateFavorite(channelId: String, isFavorite: Boolean)

    @Query("DELETE FROM channels WHERE sourceId = :sourceId")
    suspend fun deleteChannelsBySource(sourceId: String)
}

@Dao
interface EpgProgramDao {
    @Query("SELECT * FROM epg_programs WHERE channelId = :channelId AND startTime >= :startTime AND endTime <= :endTime ORDER BY startTime")
    fun getProgramsForChannel(channelId: String, startTime: Long, endTime: Long): Flow<List<EpgProgram>>

    @Query("SELECT * FROM epg_programs WHERE channelId = :channelId AND startTime <= :now AND endTime >= :now LIMIT 1")
    suspend fun getCurrentProgram(channelId: String, now: Long = System.currentTimeMillis()): EpgProgram?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertPrograms(programs: List<EpgProgram>)

    @Query("DELETE FROM epg_programs WHERE channelId IN (SELECT epgId FROM channels WHERE sourceId = :sourceId)")
    suspend fun deleteProgramsForSource(sourceId: String)
}

@Dao
interface RecentChannelDao {
    @Query("""
        SELECT c.* FROM channels c
        INNER JOIN recent_channels r ON c.id = r.channelId
        ORDER BY r.lastWatched DESC
        LIMIT :limit
    """)
    fun getRecentChannels(limit: Int = 20): Flow<List<Channel>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertRecentChannel(recentChannel: RecentChannel)

    @Query("SELECT * FROM recent_channels WHERE channelId = :channelId")
    suspend fun getRecentChannel(channelId: String): RecentChannel?

    @Query("DELETE FROM recent_channels")
    suspend fun clearRecent()
}

@Dao
interface WatchHistoryDao {
    @Query("SELECT * FROM watch_history ORDER BY startTime DESC")
    suspend fun getAllHistory(): List<WatchHistoryEntry>

    @Query("SELECT * FROM watch_history ORDER BY startTime DESC LIMIT :limit")
    suspend fun getRecentHistory(limit: Int): List<WatchHistoryEntry>

    @Query("SELECT * FROM watch_history WHERE completionPercentage >= :minCompletion AND completionPercentage <= :maxCompletion ORDER BY startTime DESC")
    suspend fun getIncompleteWatches(minCompletion: Double, maxCompletion: Double): List<WatchHistoryEntry>

    @Query("SELECT * FROM watch_history WHERE category = :category ORDER BY startTime DESC")
    suspend fun getHistoryByCategory(category: String): List<WatchHistoryEntry>

    @Query("SELECT DISTINCT channelId FROM watch_history")
    suspend fun getAllWatchedChannelIds(): List<String>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entry: WatchHistoryEntry)

    @Query("UPDATE watch_history SET endTime = :endTime, durationSeconds = :durationSeconds, completionPercentage = :completionPercentage WHERE id = :id")
    suspend fun updateWatchEnd(id: String, endTime: Long, durationSeconds: Int, completionPercentage: Double)

    @Query("DELETE FROM watch_history WHERE startTime < :before")
    suspend fun deleteOldHistory(before: Long)
}

@Dao
interface UserPreferencesDao {
    @Query("SELECT * FROM user_preferences WHERE id = 'default' LIMIT 1")
    suspend fun getPreferences(): UserPreferencesEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(preferences: UserPreferencesEntity)

    @Query("DELETE FROM user_preferences")
    suspend fun clear()
}
