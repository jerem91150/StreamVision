package com.streamvision.app.data.repository

import com.streamvision.app.data.local.ChannelDao
import com.streamvision.app.data.local.PlaylistSourceDao
import com.streamvision.app.data.local.RecentChannelDao
import com.streamvision.app.data.models.*
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class StreamRepository @Inject constructor(
    private val playlistSourceDao: PlaylistSourceDao,
    private val channelDao: ChannelDao,
    private val recentChannelDao: RecentChannelDao,
    private val m3uParser: M3UParser,
    private val xtreamService: XtreamCodesService
) {
    // Playlist Sources
    fun getAllSources(): Flow<List<PlaylistSource>> = playlistSourceDao.getAllSources()

    suspend fun getSourceById(id: String): PlaylistSource? = playlistSourceDao.getSourceById(id)

    suspend fun saveSource(source: PlaylistSource) = playlistSourceDao.insertSource(source)

    suspend fun updateSource(source: PlaylistSource) = playlistSourceDao.updateSource(source)

    suspend fun deleteSource(source: PlaylistSource) {
        channelDao.deleteChannelsBySource(source.id)
        playlistSourceDao.deleteSource(source)
    }

    // Channels
    fun getChannelsBySource(sourceId: String): Flow<List<Channel>> =
        channelDao.getChannelsBySource(sourceId)

    fun getFavoriteChannels(): Flow<List<Channel>> = channelDao.getFavoriteChannels()

    fun getRecentChannels(): Flow<List<Channel>> = recentChannelDao.getRecentChannels()

    fun searchChannels(query: String): Flow<List<Channel>> = channelDao.searchChannels(query)

    suspend fun toggleFavorite(channel: Channel) {
        channelDao.updateFavorite(channel.id, !channel.isFavorite)
    }

    suspend fun addToRecent(channelId: String) {
        val existing = recentChannelDao.getRecentChannel(channelId)
        val recentChannel = RecentChannel(
            channelId = channelId,
            lastWatched = System.currentTimeMillis(),
            watchCount = (existing?.watchCount ?: 0) + 1
        )
        recentChannelDao.insertRecentChannel(recentChannel)
    }

    // Import Playlists
    suspend fun importM3UPlaylist(
        name: String,
        url: String,
        epgUrl: String?
    ): Result<PlaylistSource> = runCatching {
        val source = PlaylistSource(
            name = name.ifEmpty { "M3U Playlist" },
            type = SourceType.M3U,
            url = url,
            epgUrl = epgUrl
        )

        val channels = m3uParser.parseFromUrl(url, source.id)

        channelDao.insertChannels(channels)
        val updatedSource = source.copy(channelCount = channels.size)
        playlistSourceDao.insertSource(updatedSource)

        updatedSource
    }

    suspend fun importXtreamPlaylist(
        name: String,
        serverUrl: String,
        username: String,
        password: String
    ): Result<PlaylistSource> = runCatching {
        val accountInfo = xtreamService.authenticate(serverUrl, username, password)
            ?: throw Exception("Authentication failed")

        val source = PlaylistSource(
            name = name.ifEmpty { "Xtream - $username" },
            type = SourceType.XTREAM_CODES,
            url = serverUrl,
            username = username,
            password = password
        )

        val channels = xtreamService.getLiveStreams(serverUrl, username, password, source.id)

        channelDao.insertChannels(channels)
        val updatedSource = source.copy(channelCount = channels.size)
        playlistSourceDao.insertSource(updatedSource)

        updatedSource
    }

    suspend fun refreshPlaylist(source: PlaylistSource): Result<Int> = runCatching {
        channelDao.deleteChannelsBySource(source.id)

        val channels = when (source.type) {
            SourceType.XTREAM_CODES -> {
                xtreamService.getLiveStreams(
                    source.url,
                    source.username ?: "",
                    source.password ?: "",
                    source.id
                )
            }
            else -> {
                m3uParser.parseFromUrl(source.url, source.id)
            }
        }

        channelDao.insertChannels(channels)
        playlistSourceDao.updateSource(
            source.copy(
                channelCount = channels.size,
                lastSync = System.currentTimeMillis()
            )
        )

        channels.size
    }
}
