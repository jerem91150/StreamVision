import Foundation

/// Service pour l'API TMDb (The Movie Database)
/// Recupere les affiches, metadonnees et informations des films/series
actor TmdbService {

    static let shared = TmdbService()

    private let session: URLSession
    private var apiKey: String = ""

    private static let baseURL = "https://api.themoviedb.org/3"
    private static let imageBaseURL = "https://image.tmdb.org/t/p"

    // Image sizes
    static let posterSizeSmall = "w185"
    static let posterSizeMedium = "w342"
    static let posterSizeLarge = "w500"
    static let posterSizeOriginal = "original"

    static let backdropSizeSmall = "w300"
    static let backdropSizeMedium = "w780"
    static let backdropSizeLarge = "w1280"
    static let backdropSizeOriginal = "original"

    init() {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 15
        self.session = URLSession(configuration: config)
    }

    func setApiKey(_ key: String) {
        apiKey = key
    }

    var isConfigured: Bool { !apiKey.isEmpty }

    // MARK: - Search

    func searchMovie(query: String, year: Int? = nil, language: String = "fr-FR") async -> TmdbSearchResult? {
        guard isConfigured else { return nil }

        do {
            guard let encodedQuery = query.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) else { return nil }
            var urlString = "\(Self.baseURL)/search/movie?api_key=\(apiKey)&query=\(encodedQuery)&language=\(language)"
            if let year = year {
                urlString += "&year=\(year)"
            }

            guard let url = URL(string: urlString) else { return nil }

            let (data, _) = try await session.data(from: url)

            guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let results = json["results"] as? [[String: Any]],
                  let first = results.first else {
                return nil
            }

            return parseTmdbResult(first, isMovie: true)
        } catch {
            return nil
        }
    }

    func searchTvShow(query: String, year: Int? = nil, language: String = "fr-FR") async -> TmdbSearchResult? {
        guard isConfigured else { return nil }

        do {
            guard let encodedQuery = query.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) else { return nil }
            var urlString = "\(Self.baseURL)/search/tv?api_key=\(apiKey)&query=\(encodedQuery)&language=\(language)"
            if let year = year {
                urlString += "&first_air_date_year=\(year)"
            }

            guard let url = URL(string: urlString) else { return nil }

            let (data, _) = try await session.data(from: url)

            guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let results = json["results"] as? [[String: Any]],
                  let first = results.first else {
                return nil
            }

            return parseTmdbResult(first, isMovie: false)
        } catch {
            return nil
        }
    }

    // MARK: - Details

    func getMovieDetails(tmdbId: Int, language: String = "fr-FR") async -> TmdbDetails? {
        guard isConfigured else { return nil }

        do {
            let urlString = "\(Self.baseURL)/movie/\(tmdbId)?api_key=\(apiKey)&language=\(language)&append_to_response=credits,videos"
            guard let url = URL(string: urlString) else { return nil }

            let (data, _) = try await session.data(from: url)

            guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                return nil
            }

            return parseTmdbDetails(json, isMovie: true)
        } catch {
            return nil
        }
    }

    func getTvShowDetails(tmdbId: Int, language: String = "fr-FR") async -> TmdbDetails? {
        guard isConfigured else { return nil }

        do {
            let urlString = "\(Self.baseURL)/tv/\(tmdbId)?api_key=\(apiKey)&language=\(language)&append_to_response=credits,videos"
            guard let url = URL(string: urlString) else { return nil }

            let (data, _) = try await session.data(from: url)

            guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                return nil
            }

            return parseTmdbDetails(json, isMovie: false)
        } catch {
            return nil
        }
    }

    // MARK: - Enrich MediaItem

    func enrichMediaItem(_ item: MediaItem) async -> MediaItem {
        guard isConfigured else { return item }

        let cleanName = cleanTitleForSearch(item.name)
        let year = item.year

        var result: TmdbSearchResult?

        switch item.mediaType {
        case .movie:
            if let tmdbId = item.tmdbId {
                result = await getMovieDetails(tmdbId: tmdbId).map { convertDetailsToResult($0) }
            } else {
                result = await searchMovie(query: cleanName, year: year)
            }
        case .series:
            if let tmdbId = item.tmdbId {
                result = await getTvShowDetails(tmdbId: tmdbId).map { convertDetailsToResult($0) }
            } else {
                result = await searchTvShow(query: cleanName, year: year)
            }
        default:
            break
        }

        guard let result = result else { return item }

        var enrichedItem = item
        enrichedItem.posterUrl = item.posterUrl ?? result.posterUrl
        enrichedItem.backdropUrl = item.backdropUrl ?? result.backdropUrl
        enrichedItem.overview = (item.overview?.isEmpty ?? true) ? result.overview : item.overview
        enrichedItem.rating = item.rating == 0 ? result.rating : item.rating
        enrichedItem.voteCount = item.voteCount == 0 ? result.voteCount : item.voteCount
        enrichedItem.releaseDate = item.releaseDate ?? result.releaseDate
        enrichedItem.tmdbId = item.tmdbId ?? result.tmdbId

        return enrichedItem
    }

    // MARK: - Helpers

    private func parseTmdbResult(_ json: [String: Any], isMovie: Bool) -> TmdbSearchResult {
        let posterPath = json["poster_path"] as? String
        let backdropPath = json["backdrop_path"] as? String
        let releaseDateStr = isMovie ? json["release_date"] as? String : json["first_air_date"] as? String

        return TmdbSearchResult(
            tmdbId: json["id"] as? Int ?? 0,
            title: isMovie ? json["title"] as? String ?? "" : json["name"] as? String ?? "",
            originalTitle: isMovie ? json["original_title"] as? String ?? "" : json["original_name"] as? String ?? "",
            overview: json["overview"] as? String ?? "",
            posterUrl: posterPath.map { getImageUrl(path: $0, size: Self.posterSizeLarge) },
            backdropUrl: backdropPath.map { getImageUrl(path: $0, size: Self.backdropSizeLarge) },
            rating: json["vote_average"] as? Double ?? 0.0,
            voteCount: json["vote_count"] as? Int ?? 0,
            releaseDate: parseDate(releaseDateStr),
            isMovie: isMovie
        )
    }

    private func parseTmdbDetails(_ json: [String: Any], isMovie: Bool) -> TmdbDetails {
        let posterPath = json["poster_path"] as? String
        let backdropPath = json["backdrop_path"] as? String
        let releaseDateStr = isMovie ? json["release_date"] as? String : json["first_air_date"] as? String

        // Genres
        var genres: [String] = []
        if let genresArray = json["genres"] as? [[String: Any]] {
            genres = genresArray.compactMap { $0["name"] as? String }
        }

        // Credits
        var cast: [String] = []
        var director: String?

        if let credits = json["credits"] as? [String: Any] {
            if let castArray = credits["cast"] as? [[String: Any]] {
                cast = Array(castArray.prefix(5).compactMap { $0["name"] as? String })
            }

            if let crewArray = credits["crew"] as? [[String: Any]] {
                for member in crewArray {
                    if member["job"] as? String == "Director" {
                        director = member["name"] as? String
                        break
                    }
                }
            }
        }

        // Trailer
        var trailerUrl: String?
        if let videos = json["videos"] as? [String: Any],
           let results = videos["results"] as? [[String: Any]] {
            for video in results {
                if video["type"] as? String == "Trailer" && video["site"] as? String == "YouTube" {
                    if let key = video["key"] as? String {
                        trailerUrl = "https://www.youtube.com/watch?v=\(key)"
                        break
                    }
                }
            }
        }

        return TmdbDetails(
            tmdbId: json["id"] as? Int ?? 0,
            title: isMovie ? json["title"] as? String ?? "" : json["name"] as? String ?? "",
            originalTitle: isMovie ? json["original_title"] as? String ?? "" : json["original_name"] as? String ?? "",
            overview: json["overview"] as? String ?? "",
            posterUrl: posterPath.map { getImageUrl(path: $0, size: Self.posterSizeLarge) },
            backdropUrl: backdropPath.map { getImageUrl(path: $0, size: Self.backdropSizeLarge) },
            rating: json["vote_average"] as? Double ?? 0.0,
            voteCount: json["vote_count"] as? Int ?? 0,
            releaseDate: parseDate(releaseDateStr),
            runtime: isMovie ? json["runtime"] as? Int ?? 0 : (json["episode_run_time"] as? [Int])?.first ?? 0,
            genres: genres.joined(separator: ", "),
            director: director,
            cast: cast.joined(separator: ", "),
            trailerUrl: trailerUrl,
            isMovie: isMovie,
            totalSeasons: !isMovie ? json["number_of_seasons"] as? Int ?? 0 : 0,
            totalEpisodes: !isMovie ? json["number_of_episodes"] as? Int ?? 0 : 0
        )
    }

    private func convertDetailsToResult(_ details: TmdbDetails) -> TmdbSearchResult {
        TmdbSearchResult(
            tmdbId: details.tmdbId,
            title: details.title,
            originalTitle: details.originalTitle,
            overview: details.overview,
            posterUrl: details.posterUrl,
            backdropUrl: details.backdropUrl,
            rating: details.rating,
            voteCount: details.voteCount,
            releaseDate: details.releaseDate,
            isMovie: details.isMovie
        )
    }

    private func getImageUrl(path: String, size: String) -> String {
        "\(Self.imageBaseURL)/\(size)\(path)"
    }

    private func cleanTitleForSearch(_ title: String) -> String {
        var cleaned = title

        // Remove S01E01 patterns
        if let range = cleaned.range(of: #"\s*[Ss]\d+[Ee]\d+.*"#, options: .regularExpression) {
            cleaned = String(cleaned[..<range.lowerBound])
        }

        // Remove year in parentheses
        if let range = cleaned.range(of: #"\s*\(\d{4}\)"#, options: .regularExpression) {
            cleaned = String(cleaned[..<range.lowerBound])
        }

        // Remove quality indicators
        if let range = cleaned.range(of: #"\s*(720p|1080p|4K|HDR|HEVC|x264|x265).*"#, options: [.regularExpression, .caseInsensitive]) {
            cleaned = String(cleaned[..<range.lowerBound])
        }

        // Remove language tags
        if let range = cleaned.range(of: #"\s*(FRENCH|VOSTFR|MULTI|TRUEFRENCH).*"#, options: [.regularExpression, .caseInsensitive]) {
            cleaned = String(cleaned[..<range.lowerBound])
        }

        return cleaned.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func parseDate(_ dateStr: String?) -> Date? {
        guard let dateStr = dateStr, !dateStr.isEmpty else { return nil }
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter.date(from: dateStr)
    }
}

// MARK: - DTOs

struct TmdbSearchResult {
    let tmdbId: Int
    let title: String
    let originalTitle: String
    let overview: String
    let posterUrl: String?
    let backdropUrl: String?
    let rating: Double
    let voteCount: Int
    let releaseDate: Date?
    let isMovie: Bool
}

struct TmdbDetails {
    let tmdbId: Int
    let title: String
    let originalTitle: String
    let overview: String
    let posterUrl: String?
    let backdropUrl: String?
    let rating: Double
    let voteCount: Int
    let releaseDate: Date?
    let runtime: Int
    let genres: String
    let director: String?
    let cast: String
    let trailerUrl: String?
    let isMovie: Bool
    let totalSeasons: Int
    let totalEpisodes: Int
}
