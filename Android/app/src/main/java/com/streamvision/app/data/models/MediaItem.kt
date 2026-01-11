package com.streamvision.app.data.models

import androidx.room.Entity
import androidx.room.PrimaryKey
import java.util.UUID

/**
 * Type de contenu media unifie
 */
enum class ContentType {
    LIVE,       // Chaine TV en direct
    MOVIE,      // Film VOD
    SERIES,     // Serie TV
    EPISODE,    // Episode de serie
    CATCHUP     // Replay/Catch-up
}

/**
 * Modele unifie pour tout contenu media (Live, VOD, Series)
 */
@Entity(tableName = "media_items")
data class MediaItem(
    @PrimaryKey
    val id: String = UUID.randomUUID().toString(),
    val sourceId: String,
    val name: String,
    val originalName: String? = null,
    val mediaType: ContentType = ContentType.LIVE,

    // Images
    val posterUrl: String? = null,
    val backdropUrl: String? = null,
    val logoUrl: String? = null,

    // Streaming
    val streamUrl: String = "",
    val groupTitle: String = "Uncategorized",
    val categoryId: String? = null,
    val epgId: String? = null,
    val containerExtension: String? = null,

    // Etat
    val isFavorite: Boolean = false,
    val order: Int = 0,
    val catchupDays: Int = 0,

    // Metadonnees TMDb
    val tmdbId: Int? = null,
    val overview: String? = null,
    val rating: Double = 0.0,
    val voteCount: Int = 0,
    val releaseDate: Long? = null,
    val runtime: Int = 0, // en minutes
    val genres: String? = null,
    val director: String? = null,
    val cast: String? = null,
    val trailerUrl: String? = null,

    // Series
    val seriesId: String? = null,
    val seasonNumber: Int = 0,
    val episodeNumber: Int = 0,
    val totalSeasons: Int = 0,
    val totalEpisodes: Int = 0,

    // Watch progress
    val watchedPosition: Long = 0, // Position en ms
    val duration: Long = 0, // Duree totale en ms
    val lastWatched: Long? = null
) {
    val isLive: Boolean get() = mediaType == ContentType.LIVE || mediaType == ContentType.CATCHUP
    val isVod: Boolean get() = mediaType == ContentType.MOVIE
    val isSeries: Boolean get() = mediaType == ContentType.SERIES || mediaType == ContentType.EPISODE

    val displayImageUrl: String? get() = if (mediaType == ContentType.LIVE) logoUrl else (posterUrl ?: logoUrl)

    val year: Int? get() = releaseDate?.let {
        java.util.Calendar.getInstance().apply { timeInMillis = it }.get(java.util.Calendar.YEAR)
    }

    val ratingDisplay: String get() = if (rating > 0) String.format("%.1f/10", rating) else ""

    val runtimeDisplay: String get() = if (runtime > 0) "${runtime / 60}h ${runtime % 60}min" else ""

    val episodeDisplay: String get() = if (seasonNumber > 0) "S${seasonNumber.toString().padStart(2, '0')}E${episodeNumber.toString().padStart(2, '0')}" else ""

    val watchProgress: Float get() = if (duration > 0) watchedPosition.toFloat() / duration.toFloat() else 0f

    val hasProgress: Boolean get() = watchedPosition > 0 && watchProgress < 0.95f

    val hasCatchup: Boolean get() = catchupDays > 0
}

/**
 * Row Netflix-style pour l'affichage
 */
data class MediaRow(
    val title: String,
    val icon: String = "",
    val items: List<MediaItem> = emptyList(),
    val isLoading: Boolean = false
)

/**
 * Categorie de contenu
 */
data class MediaCategory(
    val id: String,
    val name: String,
    val mediaType: ContentType,
    val items: List<MediaItem> = emptyList(),
    val itemCount: Int = 0
)

/**
 * Info complete d'une serie
 */
data class SeriesFullInfo(
    val seriesId: String,
    val name: String,
    val overview: String = "",
    val posterUrl: String? = null,
    val backdropUrl: String? = null,
    val rating: Double = 0.0,
    val genre: String = "",
    val director: String = "",
    val cast: String = "",
    val releaseDate: String = "",
    val tmdbId: Int? = null,
    val seasons: List<SeriesSeasonInfo> = emptyList()
)

data class SeriesSeasonInfo(
    val seasonNumber: Int,
    val name: String = "Saison $seasonNumber",
    val episodes: List<EpisodeInfo> = emptyList()
)

data class EpisodeInfo(
    val id: String,
    val episodeNumber: Int,
    val title: String,
    val overview: String = "",
    val duration: String = "",
    val posterUrl: String? = null,
    val streamUrl: String,
    val containerExtension: String = "",
    val rating: Double = 0.0
)

/**
 * Info VOD detaillee
 */
data class VodInfo(
    val streamId: String,
    val name: String,
    val overview: String = "",
    val posterUrl: String? = null,
    val backdropUrl: String? = null,
    val rating: Double = 0.0,
    val duration: String = "",
    val releaseDate: String = "",
    val genre: String = "",
    val director: String = "",
    val cast: String = "",
    val tmdbId: Int? = null,
    val trailerUrl: String? = null,
    val containerExtension: String = "mp4"
)

/**
 * EPG Xtream
 */
data class XtreamEpgEntry(
    val id: String,
    val title: String,
    val description: String = "",
    val start: Long? = null,
    val end: Long? = null,
    val startTimestamp: String = "",
    val stopTimestamp: String = ""
) {
    val isNow: Boolean get() {
        val now = System.currentTimeMillis()
        return start != null && end != null && now in start..end
    }
    val isPast: Boolean get() = end != null && System.currentTimeMillis() >= end
}
