import Foundation

// MARK: - Content Type
enum ContentType: String, Codable, CaseIterable {
    case live = "live"
    case movie = "movie"
    case series = "series"
    case episode = "episode"
    case catchup = "catchup"
}

// MARK: - MediaItem (Unified Model)
struct MediaItem: Identifiable, Codable, Hashable {
    var id: UUID = UUID()
    var sourceId: UUID
    var name: String
    var originalName: String?
    var mediaType: ContentType = .live

    // Images
    var posterUrl: String?
    var backdropUrl: String?
    var logoUrl: String?

    // Streaming
    var streamUrl: String = ""
    var groupTitle: String = "Uncategorized"
    var categoryId: String?
    var epgId: String?
    var containerExtension: String?

    // State
    var isFavorite: Bool = false
    var order: Int = 0
    var catchupDays: Int = 0

    // TMDb Metadata
    var tmdbId: Int?
    var overview: String?
    var rating: Double = 0.0
    var voteCount: Int = 0
    var releaseDate: Date?
    var runtime: Int = 0 // in minutes
    var genres: String?
    var director: String?
    var cast: String?
    var trailerUrl: String?

    // Series
    var seriesId: String?
    var seasonNumber: Int = 0
    var episodeNumber: Int = 0
    var totalSeasons: Int = 0
    var totalEpisodes: Int = 0

    // Watch Progress
    var watchedPosition: TimeInterval = 0 // in seconds
    var duration: TimeInterval = 0 // in seconds
    var lastWatched: Date?

    // MARK: - Computed Properties

    var isLive: Bool { mediaType == .live || mediaType == .catchup }
    var isVod: Bool { mediaType == .movie }
    var isSeries: Bool { mediaType == .series || mediaType == .episode }

    var displayImageUrl: String? {
        mediaType == .live ? logoUrl : (posterUrl ?? logoUrl)
    }

    var year: Int? {
        guard let date = releaseDate else { return nil }
        return Calendar.current.component(.year, from: date)
    }

    var ratingDisplay: String {
        rating > 0 ? String(format: "%.1f/10", rating) : ""
    }

    var runtimeDisplay: String {
        guard runtime > 0 else { return "" }
        let hours = runtime / 60
        let minutes = runtime % 60
        return "\(hours)h \(minutes)min"
    }

    var episodeDisplay: String {
        guard seasonNumber > 0 else { return "" }
        return String(format: "S%02dE%02d", seasonNumber, episodeNumber)
    }

    var watchProgress: Double {
        duration > 0 ? watchedPosition / duration : 0
    }

    var hasProgress: Bool {
        watchedPosition > 0 && watchProgress < 0.95
    }

    var hasCatchup: Bool { catchupDays > 0 }

    // MARK: - Hashable

    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }

    static func == (lhs: MediaItem, rhs: MediaItem) -> Bool {
        lhs.id == rhs.id
    }
}

// MARK: - MediaRow (Netflix-style Row)
struct MediaRow: Identifiable {
    var id: String { title }
    var title: String
    var icon: String = ""
    var items: [MediaItem] = []
    var isLoading: Bool = false
}

// MARK: - MediaCategory
struct MediaCategory: Identifiable {
    var id: String
    var name: String
    var mediaType: ContentType
    var items: [MediaItem] = []
    var itemCount: Int = 0
}

// MARK: - Series Full Info
struct SeriesFullInfo: Codable {
    var seriesId: String
    var name: String
    var overview: String = ""
    var posterUrl: String?
    var backdropUrl: String?
    var rating: Double = 0.0
    var genre: String = ""
    var director: String = ""
    var cast: String = ""
    var releaseDate: String = ""
    var tmdbId: Int?
    var seasons: [SeriesSeasonInfo] = []
}

struct SeriesSeasonInfo: Codable, Identifiable {
    var id: Int { seasonNumber }
    var seasonNumber: Int
    var name: String { "Saison \(seasonNumber)" }
    var episodes: [EpisodeInfo] = []
}

struct EpisodeInfo: Codable, Identifiable {
    var id: String
    var episodeNumber: Int
    var title: String
    var overview: String = ""
    var duration: String = ""
    var posterUrl: String?
    var streamUrl: String
    var containerExtension: String = ""
    var rating: Double = 0.0
}

// MARK: - VOD Info
struct VodInfo: Codable {
    var streamId: String
    var name: String
    var overview: String = ""
    var posterUrl: String?
    var backdropUrl: String?
    var rating: Double = 0.0
    var duration: String = ""
    var releaseDate: String = ""
    var genre: String = ""
    var director: String = ""
    var cast: String = ""
    var tmdbId: Int?
    var trailerUrl: String?
    var containerExtension: String = "mp4"
}

// MARK: - Xtream EPG Entry
struct XtreamEpgEntry: Codable, Identifiable {
    var id: String
    var title: String
    var description: String = ""
    var start: Date?
    var end: Date?
    var startTimestamp: String = ""
    var stopTimestamp: String = ""

    var isNow: Bool {
        guard let start = start, let end = end else { return false }
        let now = Date()
        return now >= start && now < end
    }

    var isPast: Bool {
        guard let end = end else { return false }
        return Date() >= end
    }
}
