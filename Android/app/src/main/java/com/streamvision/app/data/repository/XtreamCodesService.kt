package com.streamvision.app.data.repository

import android.util.Base64
import com.streamvision.app.data.models.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.net.URL
import java.text.SimpleDateFormat
import java.util.*
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Service complet pour l'API Xtream Codes
 * Supporte Live, VOD, Series, Catch-up
 */
@Singleton
class XtreamCodesService @Inject constructor() {

    private var currentAccount: XtreamAccountInfo? = null

    // region Authentication

    suspend fun authenticate(
        serverUrl: String,
        username: String,
        password: String
    ): XtreamAccountInfo? = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            val url = "$normalizedUrl/player_api.php?username=$username&password=$password"
            val response = URL(url).readText()
            val json = JSONObject(response)

            val userInfo = json.optJSONObject("user_info") ?: return@withContext null
            val serverInfo = json.optJSONObject("server_info")

            currentAccount = XtreamAccountInfo(
                username = userInfo.optString("username", ""),
                status = userInfo.optString("status", ""),
                expDate = userInfo.optString("exp_date", ""),
                maxConnections = userInfo.optString("max_connections", ""),
                activeConnections = userInfo.optString("active_cons", ""),
                serverUrl = normalizedUrl
            )
            currentAccount
        } catch (e: Exception) {
            null
        }
    }

    // endregion

    // region Categories

    suspend fun getLiveCategories(
        serverUrl: String,
        username: String,
        password: String
    ): List<XtreamCategory> = getCategories(serverUrl, username, password, "get_live_categories")

    suspend fun getVodCategories(
        serverUrl: String,
        username: String,
        password: String
    ): List<XtreamCategory> = getCategories(serverUrl, username, password, "get_vod_categories")

    suspend fun getSeriesCategories(
        serverUrl: String,
        username: String,
        password: String
    ): List<XtreamCategory> = getCategories(serverUrl, username, password, "get_series_categories")

    private suspend fun getCategories(
        serverUrl: String,
        username: String,
        password: String,
        action: String
    ): List<XtreamCategory> = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            val url = "$normalizedUrl/player_api.php?username=$username&password=$password&action=$action"
            val response = URL(url).readText()
            val jsonArray = JSONArray(response)

            (0 until jsonArray.length()).mapNotNull { i ->
                val item = jsonArray.getJSONObject(i)
                XtreamCategory(
                    categoryId = item.optString("category_id", ""),
                    categoryName = item.optString("category_name", ""),
                    parentId = item.optString("parent_id", "")
                )
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    // endregion

    // region Live Streams

    suspend fun getLiveStreams(
        serverUrl: String,
        username: String,
        password: String,
        sourceId: String
    ): List<MediaItem> = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            val url = "$normalizedUrl/player_api.php?username=$username&password=$password&action=get_live_streams"
            val response = URL(url).readText()
            val jsonArray = JSONArray(response)

            val categories = getLiveCategories(serverUrl, username, password)
            val categoryMap = categories.associateBy { it.categoryId }

            (0 until jsonArray.length()).mapNotNull { i ->
                val item = jsonArray.getJSONObject(i)
                val streamId = item.optString("stream_id", "")
                val categoryId = item.optString("category_id", "")
                val catchupDays = item.optInt("tv_archive_duration", 0)

                MediaItem(
                    sourceId = sourceId,
                    name = item.optString("name", ""),
                    logoUrl = item.optString("stream_icon", null),
                    streamUrl = "$normalizedUrl/live/$username/$password/$streamId.m3u8",
                    groupTitle = categoryMap[categoryId]?.categoryName ?: "Uncategorized",
                    categoryId = categoryId,
                    epgId = item.optString("epg_channel_id", null),
                    catchupDays = catchupDays,
                    order = i,
                    mediaType = ContentType.LIVE
                )
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    // Compatibilite avec l'ancien code
    suspend fun getLiveStreamsAsChannels(
        serverUrl: String,
        username: String,
        password: String,
        sourceId: String
    ): List<Channel> {
        val mediaItems = getLiveStreams(serverUrl, username, password, sourceId)
        return mediaItems.map { m ->
            Channel(
                id = m.id,
                sourceId = m.sourceId,
                name = m.name,
                logoUrl = m.logoUrl,
                streamUrl = m.streamUrl,
                groupTitle = m.groupTitle,
                epgId = m.epgId,
                catchupDays = m.catchupDays,
                order = m.order
            )
        }
    }

    // endregion

    // region VOD (Movies)

    suspend fun getVodStreams(
        serverUrl: String,
        username: String,
        password: String,
        sourceId: String,
        categoryId: String? = null
    ): List<MediaItem> = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            var url = "$normalizedUrl/player_api.php?username=$username&password=$password&action=get_vod_streams"
            if (!categoryId.isNullOrEmpty()) {
                url += "&category_id=$categoryId"
            }

            val response = URL(url).readText()
            val jsonArray = JSONArray(response)

            val categories = getVodCategories(serverUrl, username, password)
            val categoryMap = categories.associateBy { it.categoryId }

            (0 until jsonArray.length()).mapNotNull { i ->
                val item = jsonArray.getJSONObject(i)
                val streamId = item.optString("stream_id", "")
                val extension = item.optString("container_extension", "mp4")
                val catId = item.optString("category_id", "")

                MediaItem(
                    sourceId = sourceId,
                    name = item.optString("name", ""),
                    posterUrl = item.optString("stream_icon", null),
                    streamUrl = "$normalizedUrl/movie/$username/$password/$streamId.$extension",
                    groupTitle = categoryMap[catId]?.categoryName ?: "Films",
                    categoryId = catId,
                    order = i,
                    mediaType = ContentType.MOVIE,
                    containerExtension = extension,
                    rating = item.optDouble("rating", 0.0),
                    tmdbId = item.optString("tmdb_id", null)?.toIntOrNull(),
                    releaseDate = item.optString("added", null)?.toLongOrNull()?.let { it * 1000 }
                )
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    suspend fun getVodInfo(
        serverUrl: String,
        username: String,
        password: String,
        vodId: String
    ): VodInfo? = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            val url = "$normalizedUrl/player_api.php?username=$username&password=$password&action=get_vod_info&vod_id=$vodId"
            val response = URL(url).readText()
            val json = JSONObject(response)

            val info = json.optJSONObject("info")
            val movieData = json.optJSONObject("movie_data")

            VodInfo(
                streamId = vodId,
                name = info?.optString("name") ?: movieData?.optString("name") ?: "",
                overview = info?.optString("plot") ?: info?.optString("description") ?: "",
                posterUrl = info?.optString("movie_image") ?: info?.optString("cover_big"),
                backdropUrl = info?.optJSONArray("backdrop_path")?.optString(0),
                rating = info?.optDouble("rating", 0.0) ?: 0.0,
                duration = info?.optString("duration") ?: "",
                releaseDate = info?.optString("releasedate") ?: info?.optString("release_date") ?: "",
                genre = info?.optString("genre") ?: "",
                director = info?.optString("director") ?: "",
                cast = info?.optString("cast") ?: info?.optString("actors") ?: "",
                tmdbId = info?.optString("tmdb_id")?.toIntOrNull(),
                trailerUrl = info?.optString("youtube_trailer"),
                containerExtension = movieData?.optString("container_extension") ?: "mp4"
            )
        } catch (e: Exception) {
            null
        }
    }

    // endregion

    // region Series

    suspend fun getSeries(
        serverUrl: String,
        username: String,
        password: String,
        sourceId: String,
        categoryId: String? = null
    ): List<MediaItem> = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            var url = "$normalizedUrl/player_api.php?username=$username&password=$password&action=get_series"
            if (!categoryId.isNullOrEmpty()) {
                url += "&category_id=$categoryId"
            }

            val response = URL(url).readText()
            val jsonArray = JSONArray(response)

            val categories = getSeriesCategories(serverUrl, username, password)
            val categoryMap = categories.associateBy { it.categoryId }

            (0 until jsonArray.length()).mapNotNull { i ->
                val item = jsonArray.getJSONObject(i)
                val seriesId = item.optString("series_id", "")
                val catId = item.optString("category_id", "")

                MediaItem(
                    sourceId = sourceId,
                    seriesId = seriesId,
                    name = item.optString("name", ""),
                    posterUrl = item.optString("cover", null),
                    backdropUrl = item.optJSONArray("backdrop_path")?.optString(0),
                    streamUrl = "", // Les series n'ont pas d'URL directe
                    groupTitle = categoryMap[catId]?.categoryName ?: "Series",
                    categoryId = catId,
                    order = i,
                    mediaType = ContentType.SERIES,
                    rating = item.optDouble("rating", 0.0),
                    tmdbId = item.optString("tmdb_id", null)?.toIntOrNull(),
                    overview = item.optString("plot", ""),
                    genres = item.optString("genre", ""),
                    cast = item.optString("cast", "")
                )
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    suspend fun getSeriesInfo(
        serverUrl: String,
        username: String,
        password: String,
        seriesId: String
    ): SeriesFullInfo? = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            val url = "$normalizedUrl/player_api.php?username=$username&password=$password&action=get_series_info&series_id=$seriesId"
            val response = URL(url).readText()
            val json = JSONObject(response)

            val info = json.optJSONObject("info")
            val episodes = json.optJSONObject("episodes")

            val seasons = mutableListOf<SeriesSeasonInfo>()

            if (episodes != null) {
                val seasonKeys = episodes.keys()
                while (seasonKeys.hasNext()) {
                    val seasonKey = seasonKeys.next()
                    val seasonNum = seasonKey.toIntOrNull() ?: 0
                    val episodesArray = episodes.optJSONArray(seasonKey)

                    val episodeList = mutableListOf<EpisodeInfo>()
                    if (episodesArray != null) {
                        for (j in 0 until episodesArray.length()) {
                            val ep = episodesArray.getJSONObject(j)
                            val episodeId = ep.optString("id", "")
                            val extension = ep.optString("container_extension", "mp4")

                            episodeList.add(EpisodeInfo(
                                id = episodeId,
                                episodeNumber = ep.optInt("episode_num", 0),
                                title = ep.optString("title", "Episode"),
                                overview = ep.optString("plot", ""),
                                duration = ep.optString("duration", ""),
                                posterUrl = ep.optJSONObject("info")?.optString("movie_image"),
                                streamUrl = "$normalizedUrl/series/$username/$password/$episodeId.$extension",
                                containerExtension = extension,
                                rating = ep.optDouble("rating", 0.0)
                            ))
                        }
                    }

                    seasons.add(SeriesSeasonInfo(
                        seasonNumber = seasonNum,
                        episodes = episodeList
                    ))
                }
            }

            SeriesFullInfo(
                seriesId = seriesId,
                name = info?.optString("name") ?: "",
                overview = info?.optString("plot") ?: "",
                posterUrl = info?.optString("cover"),
                backdropUrl = info?.optJSONArray("backdrop_path")?.optString(0),
                rating = info?.optDouble("rating", 0.0) ?: 0.0,
                genre = info?.optString("genre") ?: "",
                director = info?.optString("director") ?: "",
                cast = info?.optString("cast") ?: "",
                releaseDate = info?.optString("releaseDate") ?: "",
                tmdbId = info?.optString("tmdb_id")?.toIntOrNull(),
                seasons = seasons.sortedBy { it.seasonNumber }
            )
        } catch (e: Exception) {
            null
        }
    }

    // endregion

    // region Catch-up / Timeshift

    fun getCatchupUrl(
        serverUrl: String,
        username: String,
        password: String,
        streamId: String,
        startTime: Date,
        durationMinutes: Int
    ): String {
        val normalizedUrl = normalizeServerUrl(serverUrl)
        val format = SimpleDateFormat("yyyy-MM-dd:HH-mm", Locale.US).apply {
            timeZone = TimeZone.getTimeZone("UTC")
        }
        val startUtc = format.format(startTime)
        return "$normalizedUrl/timeshift/$username/$password/$durationMinutes/$startUtc/$streamId.m3u8"
    }

    fun getCatchupUrlSimple(
        serverUrl: String,
        username: String,
        password: String,
        streamId: String,
        startTime: Date
    ): String {
        val normalizedUrl = normalizeServerUrl(serverUrl)
        val start = startTime.time / 1000
        return "$normalizedUrl/streaming/timeshift.php?username=$username&password=$password&stream=$streamId&start=$start"
    }

    // endregion

    // region Short EPG

    suspend fun getShortEpg(
        serverUrl: String,
        username: String,
        password: String,
        streamId: String,
        limit: Int = 10
    ): List<XtreamEpgEntry> = withContext(Dispatchers.IO) {
        try {
            val normalizedUrl = normalizeServerUrl(serverUrl)
            val url = "$normalizedUrl/player_api.php?username=$username&password=$password&action=get_short_epg&stream_id=$streamId&limit=$limit"
            val response = URL(url).readText()
            val json = JSONObject(response)

            val epgListings = json.optJSONArray("epg_listings") ?: return@withContext emptyList()

            (0 until epgListings.length()).mapNotNull { i ->
                val item = epgListings.getJSONObject(i)
                XtreamEpgEntry(
                    id = item.optString("id", ""),
                    title = decodeBase64(item.optString("title", "")),
                    description = decodeBase64(item.optString("description", "")),
                    start = parseEpgDateTime(item.optString("start")),
                    end = parseEpgDateTime(item.optString("end")),
                    startTimestamp = item.optString("start_timestamp", ""),
                    stopTimestamp = item.optString("stop_timestamp", "")
                )
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    // endregion

    // region Helpers

    private fun normalizeServerUrl(url: String): String {
        var normalized = url.trim().trimEnd('/')
        if (!normalized.startsWith("http://") && !normalized.startsWith("https://")) {
            normalized = "http://$normalized"
        }
        return normalized
    }

    private fun decodeBase64(base64: String): String {
        if (base64.isEmpty()) return ""
        return try {
            String(Base64.decode(base64, Base64.DEFAULT), Charsets.UTF_8)
        } catch (e: Exception) {
            base64 // Return as-is if not base64
        }
    }

    private fun parseEpgDateTime(dateStr: String?): Long? {
        if (dateStr.isNullOrEmpty()) return null
        return try {
            SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).parse(dateStr)?.time
        } catch (e: Exception) {
            null
        }
    }

    // endregion
}
