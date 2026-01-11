package com.streamvision.app.data.repository

import com.streamvision.app.data.models.ContentType
import com.streamvision.app.data.models.MediaItem
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.URL
import java.net.URLEncoder
import java.text.SimpleDateFormat
import java.util.*
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Service pour l'API TMDb (The Movie Database)
 * Recupere les affiches, metadonnees et informations des films/series
 */
@Singleton
class TmdbService @Inject constructor() {

    companion object {
        private const val BASE_URL = "https://api.themoviedb.org/3"
        private const val IMAGE_BASE_URL = "https://image.tmdb.org/t/p"

        // Tailles d'images disponibles
        const val POSTER_SIZE_SMALL = "w185"
        const val POSTER_SIZE_MEDIUM = "w342"
        const val POSTER_SIZE_LARGE = "w500"
        const val POSTER_SIZE_ORIGINAL = "original"

        const val BACKDROP_SIZE_SMALL = "w300"
        const val BACKDROP_SIZE_MEDIUM = "w780"
        const val BACKDROP_SIZE_LARGE = "w1280"
        const val BACKDROP_SIZE_ORIGINAL = "original"
    }

    private var apiKey: String = ""

    fun setApiKey(key: String) {
        apiKey = key
    }

    fun isConfigured(): Boolean = apiKey.isNotBlank()

    // region Search

    suspend fun searchMovie(
        query: String,
        year: Int? = null,
        language: String = "fr-FR"
    ): TmdbSearchResult? = withContext(Dispatchers.IO) {
        if (!isConfigured()) return@withContext null

        try {
            val encodedQuery = URLEncoder.encode(query, "UTF-8")
            var url = "$BASE_URL/search/movie?api_key=$apiKey&query=$encodedQuery&language=$language"
            if (year != null) {
                url += "&year=$year"
            }

            val response = URL(url).readText()
            val json = JSONObject(response)
            val results = json.optJSONArray("results")

            if (results != null && results.length() > 0) {
                val first = results.getJSONObject(0)
                parseTmdbResult(first, isMovie = true)
            } else null
        } catch (e: Exception) {
            null
        }
    }

    suspend fun searchTvShow(
        query: String,
        year: Int? = null,
        language: String = "fr-FR"
    ): TmdbSearchResult? = withContext(Dispatchers.IO) {
        if (!isConfigured()) return@withContext null

        try {
            val encodedQuery = URLEncoder.encode(query, "UTF-8")
            var url = "$BASE_URL/search/tv?api_key=$apiKey&query=$encodedQuery&language=$language"
            if (year != null) {
                url += "&first_air_date_year=$year"
            }

            val response = URL(url).readText()
            val json = JSONObject(response)
            val results = json.optJSONArray("results")

            if (results != null && results.length() > 0) {
                val first = results.getJSONObject(0)
                parseTmdbResult(first, isMovie = false)
            } else null
        } catch (e: Exception) {
            null
        }
    }

    // endregion

    // region Details

    suspend fun getMovieDetails(
        tmdbId: Int,
        language: String = "fr-FR"
    ): TmdbDetails? = withContext(Dispatchers.IO) {
        if (!isConfigured()) return@withContext null

        try {
            val url = "$BASE_URL/movie/$tmdbId?api_key=$apiKey&language=$language&append_to_response=credits,videos"
            val response = URL(url).readText()
            val json = JSONObject(response)
            parseTmdbDetails(json, isMovie = true)
        } catch (e: Exception) {
            null
        }
    }

    suspend fun getTvShowDetails(
        tmdbId: Int,
        language: String = "fr-FR"
    ): TmdbDetails? = withContext(Dispatchers.IO) {
        if (!isConfigured()) return@withContext null

        try {
            val url = "$BASE_URL/tv/$tmdbId?api_key=$apiKey&language=$language&append_to_response=credits,videos"
            val response = URL(url).readText()
            val json = JSONObject(response)
            parseTmdbDetails(json, isMovie = false)
        } catch (e: Exception) {
            null
        }
    }

    // endregion

    // region Enrich MediaItem

    suspend fun enrichMediaItem(item: MediaItem): MediaItem {
        if (!isConfigured()) return item

        return try {
            // Nettoyer le nom pour la recherche
            val cleanName = cleanTitleForSearch(item.name)
            val year = item.year

            val result = when (item.mediaType) {
                ContentType.MOVIE -> {
                    // Chercher d'abord par TMDb ID si disponible
                    if (item.tmdbId != null) {
                        getMovieDetails(item.tmdbId)?.let { convertDetailsToResult(it) }
                    } else {
                        searchMovie(cleanName, year)
                    }
                }
                ContentType.SERIES -> {
                    if (item.tmdbId != null) {
                        getTvShowDetails(item.tmdbId)?.let { convertDetailsToResult(it) }
                    } else {
                        searchTvShow(cleanName, year)
                    }
                }
                else -> null
            }

            if (result != null) {
                item.copy(
                    posterUrl = item.posterUrl ?: result.posterUrl,
                    backdropUrl = item.backdropUrl ?: result.backdropUrl,
                    overview = if (item.overview.isNullOrBlank()) result.overview else item.overview,
                    rating = if (item.rating == 0.0) result.rating else item.rating,
                    voteCount = if (item.voteCount == 0) result.voteCount else item.voteCount,
                    releaseDate = item.releaseDate ?: result.releaseDate,
                    tmdbId = item.tmdbId ?: result.tmdbId
                )
            } else item
        } catch (e: Exception) {
            item
        }
    }

    // endregion

    // region Helpers

    private fun parseTmdbResult(json: JSONObject, isMovie: Boolean): TmdbSearchResult {
        val posterPath = json.optString("poster_path", null)
        val backdropPath = json.optString("backdrop_path", null)
        val releaseDateStr = if (isMovie) json.optString("release_date") else json.optString("first_air_date")

        return TmdbSearchResult(
            tmdbId = json.optInt("id"),
            title = if (isMovie) json.optString("title", "") else json.optString("name", ""),
            originalTitle = if (isMovie) json.optString("original_title", "") else json.optString("original_name", ""),
            overview = json.optString("overview", ""),
            posterUrl = posterPath?.let { getImageUrl(it, POSTER_SIZE_LARGE) },
            backdropUrl = backdropPath?.let { getImageUrl(it, BACKDROP_SIZE_LARGE) },
            rating = json.optDouble("vote_average", 0.0),
            voteCount = json.optInt("vote_count", 0),
            releaseDate = parseDate(releaseDateStr),
            isMovie = isMovie
        )
    }

    private fun parseTmdbDetails(json: JSONObject, isMovie: Boolean): TmdbDetails {
        val posterPath = json.optString("poster_path", null)
        val backdropPath = json.optString("backdrop_path", null)
        val releaseDateStr = if (isMovie) json.optString("release_date") else json.optString("first_air_date")

        // Genres
        val genresArray = json.optJSONArray("genres")
        val genres = mutableListOf<String>()
        if (genresArray != null) {
            for (i in 0 until genresArray.length()) {
                genres.add(genresArray.getJSONObject(i).optString("name", ""))
            }
        }

        // Credits
        val credits = json.optJSONObject("credits")
        val cast = mutableListOf<String>()
        var director: String? = null

        if (credits != null) {
            // Cast (top 5)
            val castArray = credits.optJSONArray("cast")
            if (castArray != null) {
                for (i in 0 until minOf(5, castArray.length())) {
                    cast.add(castArray.getJSONObject(i).optString("name", ""))
                }
            }

            // Director
            val crewArray = credits.optJSONArray("crew")
            if (crewArray != null) {
                for (i in 0 until crewArray.length()) {
                    val member = crewArray.getJSONObject(i)
                    if (member.optString("job") == "Director") {
                        director = member.optString("name")
                        break
                    }
                }
            }
        }

        // Trailer
        var trailerUrl: String? = null
        val videos = json.optJSONObject("videos")?.optJSONArray("results")
        if (videos != null) {
            for (i in 0 until videos.length()) {
                val video = videos.getJSONObject(i)
                if (video.optString("type") == "Trailer" && video.optString("site") == "YouTube") {
                    trailerUrl = "https://www.youtube.com/watch?v=${video.optString("key")}"
                    break
                }
            }
        }

        return TmdbDetails(
            tmdbId = json.optInt("id"),
            title = if (isMovie) json.optString("title", "") else json.optString("name", ""),
            originalTitle = if (isMovie) json.optString("original_title", "") else json.optString("original_name", ""),
            overview = json.optString("overview", ""),
            posterUrl = posterPath?.let { getImageUrl(it, POSTER_SIZE_LARGE) },
            backdropUrl = backdropPath?.let { getImageUrl(it, BACKDROP_SIZE_LARGE) },
            rating = json.optDouble("vote_average", 0.0),
            voteCount = json.optInt("vote_count", 0),
            releaseDate = parseDate(releaseDateStr),
            runtime = if (isMovie) json.optInt("runtime", 0) else json.optJSONArray("episode_run_time")?.optInt(0) ?: 0,
            genres = genres.joinToString(", "),
            director = director,
            cast = cast.joinToString(", "),
            trailerUrl = trailerUrl,
            isMovie = isMovie,
            totalSeasons = if (!isMovie) json.optInt("number_of_seasons", 0) else 0,
            totalEpisodes = if (!isMovie) json.optInt("number_of_episodes", 0) else 0
        )
    }

    private fun convertDetailsToResult(details: TmdbDetails): TmdbSearchResult {
        return TmdbSearchResult(
            tmdbId = details.tmdbId,
            title = details.title,
            originalTitle = details.originalTitle,
            overview = details.overview,
            posterUrl = details.posterUrl,
            backdropUrl = details.backdropUrl,
            rating = details.rating,
            voteCount = details.voteCount,
            releaseDate = details.releaseDate,
            isMovie = details.isMovie
        )
    }

    private fun getImageUrl(path: String, size: String): String {
        return "$IMAGE_BASE_URL/$size$path"
    }

    private fun cleanTitleForSearch(title: String): String {
        // Supprimer les patterns courants comme "S01E01", annee entre parentheses, qualite, etc.
        return title
            .replace(Regex("\\s*[Ss]\\d+[Ee]\\d+.*"), "")
            .replace(Regex("\\s*\\(\\d{4}\\)"), "")
            .replace(Regex("\\s*\\[.*?]"), "")
            .replace(Regex("\\s*(720p|1080p|4K|HDR|HEVC|x264|x265).*", RegexOption.IGNORE_CASE), "")
            .replace(Regex("\\s*(FRENCH|VOSTFR|MULTI|TRUEFRENCH).*", RegexOption.IGNORE_CASE), "")
            .trim()
    }

    private fun parseDate(dateStr: String?): Long? {
        if (dateStr.isNullOrBlank()) return null
        return try {
            SimpleDateFormat("yyyy-MM-dd", Locale.US).parse(dateStr)?.time
        } catch (e: Exception) {
            null
        }
    }

    // endregion
}

// region DTOs

data class TmdbSearchResult(
    val tmdbId: Int,
    val title: String,
    val originalTitle: String = "",
    val overview: String = "",
    val posterUrl: String? = null,
    val backdropUrl: String? = null,
    val rating: Double = 0.0,
    val voteCount: Int = 0,
    val releaseDate: Long? = null,
    val isMovie: Boolean = true
)

data class TmdbDetails(
    val tmdbId: Int,
    val title: String,
    val originalTitle: String = "",
    val overview: String = "",
    val posterUrl: String? = null,
    val backdropUrl: String? = null,
    val rating: Double = 0.0,
    val voteCount: Int = 0,
    val releaseDate: Long? = null,
    val runtime: Int = 0,
    val genres: String = "",
    val director: String? = null,
    val cast: String = "",
    val trailerUrl: String? = null,
    val isMovie: Boolean = true,
    val totalSeasons: Int = 0,
    val totalEpisodes: Int = 0
)

// endregion
