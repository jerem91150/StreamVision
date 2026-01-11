import Foundation

/// Service complet pour l'API Xtream Codes
/// Supporte Live, VOD, Series, Catch-up
actor XtreamCodesService {

    private let session: URLSession
    private var currentAccount: XtreamAccountInfo?

    init() {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 30
        self.session = URLSession(configuration: config)
    }

    // MARK: - Authentication

    func authenticate(serverUrl: String, username: String, password: String) async throws -> XtreamAccountInfo? {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        guard let url = URL(string: "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)") else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let userInfo = json["user_info"] as? [String: Any] else {
            return nil
        }

        let account = XtreamAccountInfo(
            username: userInfo["username"] as? String ?? "",
            status: userInfo["status"] as? String ?? "",
            expDate: userInfo["exp_date"] as? String ?? "",
            maxConnections: userInfo["max_connections"] as? String ?? "",
            activeConnections: userInfo["active_cons"] as? String ?? "",
            serverUrl: normalizedUrl
        )
        currentAccount = account
        return account
    }

    // MARK: - Categories

    func getLiveCategories(serverUrl: String, username: String, password: String) async throws -> [XtreamCategory] {
        return try await getCategories(serverUrl: serverUrl, username: username, password: password, action: "get_live_categories")
    }

    func getVodCategories(serverUrl: String, username: String, password: String) async throws -> [XtreamCategory] {
        return try await getCategories(serverUrl: serverUrl, username: username, password: password, action: "get_vod_categories")
    }

    func getSeriesCategories(serverUrl: String, username: String, password: String) async throws -> [XtreamCategory] {
        return try await getCategories(serverUrl: serverUrl, username: username, password: password, action: "get_series_categories")
    }

    private func getCategories(serverUrl: String, username: String, password: String, action: String) async throws -> [XtreamCategory] {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        guard let url = URL(string: "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)&action=\(action)") else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let jsonArray = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else {
            return []
        }

        return jsonArray.compactMap { item in
            guard let categoryId = (item["category_id"] as? String) ?? (item["category_id"] as? Int).map(String.init),
                  let categoryName = item["category_name"] as? String else {
                return nil
            }
            return XtreamCategory(
                categoryId: categoryId,
                categoryName: categoryName,
                parentId: item["parent_id"] as? String ?? ""
            )
        }
    }

    // MARK: - Live Streams

    func getLiveStreams(serverUrl: String, username: String, password: String, sourceId: UUID) async throws -> [MediaItem] {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        guard let url = URL(string: "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)&action=get_live_streams") else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let jsonArray = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else {
            return []
        }

        let categories = try await getLiveCategories(serverUrl: serverUrl, username: username, password: password)
        let categoryDict = Dictionary(uniqueKeysWithValues: categories.map { ($0.categoryId, $0.categoryName) })

        var items: [MediaItem] = []
        var order = 0

        for item in jsonArray {
            guard let streamId = (item["stream_id"] as? Int) ?? Int(item["stream_id"] as? String ?? "") else {
                continue
            }

            let categoryId = (item["category_id"] as? String) ?? (item["category_id"] as? Int).map(String.init) ?? ""
            let archiveDuration = (item["tv_archive_duration"] as? Int) ?? Int(item["tv_archive_duration"] as? String ?? "") ?? 0

            let mediaItem = MediaItem(
                sourceId: sourceId,
                name: item["name"] as? String ?? "",
                mediaType: .live,
                logoUrl: item["stream_icon"] as? String,
                streamUrl: "\(normalizedUrl)/live/\(username)/\(password)/\(streamId).m3u8",
                groupTitle: categoryDict[categoryId] ?? "Uncategorized",
                categoryId: categoryId,
                epgId: item["epg_channel_id"] as? String,
                catchupDays: archiveDuration,
                order: order
            )

            items.append(mediaItem)
            order += 1
        }

        return items
    }

    // Compatibilite avec l'ancien code
    func getLiveStreamsAsChannels(serverUrl: String, username: String, password: String, sourceId: UUID) async throws -> [Channel] {
        let mediaItems = try await getLiveStreams(serverUrl: serverUrl, username: username, password: password, sourceId: sourceId)
        return mediaItems.map { m in
            Channel(
                id: m.id,
                sourceId: m.sourceId,
                name: m.name,
                logoUrl: m.logoUrl,
                streamUrl: m.streamUrl,
                groupTitle: m.groupTitle,
                epgId: m.epgId,
                catchupDays: m.catchupDays,
                order: m.order
            )
        }
    }

    // MARK: - VOD (Movies)

    func getVodStreams(serverUrl: String, username: String, password: String, sourceId: UUID, categoryId: String? = nil) async throws -> [MediaItem] {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        var urlString = "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)&action=get_vod_streams"
        if let categoryId = categoryId, !categoryId.isEmpty {
            urlString += "&category_id=\(categoryId)"
        }

        guard let url = URL(string: urlString) else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let jsonArray = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else {
            return []
        }

        let categories = try await getVodCategories(serverUrl: serverUrl, username: username, password: password)
        let categoryDict = Dictionary(uniqueKeysWithValues: categories.map { ($0.categoryId, $0.categoryName) })

        var items: [MediaItem] = []
        var order = 0

        for item in jsonArray {
            guard let streamId = (item["stream_id"] as? Int) ?? Int(item["stream_id"] as? String ?? "") else {
                continue
            }

            let extension_ = item["container_extension"] as? String ?? "mp4"
            let catId = (item["category_id"] as? String) ?? (item["category_id"] as? Int).map(String.init) ?? ""

            var mediaItem = MediaItem(
                sourceId: sourceId,
                name: item["name"] as? String ?? "",
                mediaType: .movie,
                posterUrl: item["stream_icon"] as? String,
                streamUrl: "\(normalizedUrl)/movie/\(username)/\(password)/\(streamId).\(extension_)",
                groupTitle: categoryDict[catId] ?? "Films",
                categoryId: catId,
                containerExtension: extension_,
                order: order,
                rating: item["rating"] as? Double ?? Double(item["rating"] as? String ?? "") ?? 0.0,
                tmdbId: Int(item["tmdb_id"] as? String ?? "")
            )

            if let added = item["added"] as? String, let timestamp = TimeInterval(added) {
                mediaItem.releaseDate = Date(timeIntervalSince1970: timestamp)
            }

            items.append(mediaItem)
            order += 1
        }

        return items
    }

    func getVodInfo(serverUrl: String, username: String, password: String, vodId: String) async throws -> VodInfo? {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        guard let url = URL(string: "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)&action=get_vod_info&vod_id=\(vodId)") else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return nil
        }

        let info = json["info"] as? [String: Any]
        let movieData = json["movie_data"] as? [String: Any]

        return VodInfo(
            streamId: vodId,
            name: info?["name"] as? String ?? movieData?["name"] as? String ?? "",
            overview: info?["plot"] as? String ?? info?["description"] as? String ?? "",
            posterUrl: info?["movie_image"] as? String ?? info?["cover_big"] as? String,
            backdropUrl: (info?["backdrop_path"] as? [String])?.first,
            rating: info?["rating"] as? Double ?? Double(info?["rating"] as? String ?? "") ?? 0.0,
            duration: info?["duration"] as? String ?? "",
            releaseDate: info?["releasedate"] as? String ?? info?["release_date"] as? String ?? "",
            genre: info?["genre"] as? String ?? "",
            director: info?["director"] as? String ?? "",
            cast: info?["cast"] as? String ?? info?["actors"] as? String ?? "",
            tmdbId: Int(info?["tmdb_id"] as? String ?? ""),
            trailerUrl: info?["youtube_trailer"] as? String,
            containerExtension: movieData?["container_extension"] as? String ?? "mp4"
        )
    }

    // MARK: - Series

    func getSeries(serverUrl: String, username: String, password: String, sourceId: UUID, categoryId: String? = nil) async throws -> [MediaItem] {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        var urlString = "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)&action=get_series"
        if let categoryId = categoryId, !categoryId.isEmpty {
            urlString += "&category_id=\(categoryId)"
        }

        guard let url = URL(string: urlString) else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let jsonArray = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else {
            return []
        }

        let categories = try await getSeriesCategories(serverUrl: serverUrl, username: username, password: password)
        let categoryDict = Dictionary(uniqueKeysWithValues: categories.map { ($0.categoryId, $0.categoryName) })

        var items: [MediaItem] = []
        var order = 0

        for item in jsonArray {
            let seriesId = item["series_id"] as? String ?? "\(item["series_id"] as? Int ?? 0)"
            let catId = (item["category_id"] as? String) ?? (item["category_id"] as? Int).map(String.init) ?? ""

            let mediaItem = MediaItem(
                sourceId: sourceId,
                name: item["name"] as? String ?? "",
                mediaType: .series,
                posterUrl: item["cover"] as? String,
                backdropUrl: (item["backdrop_path"] as? [String])?.first,
                groupTitle: categoryDict[catId] ?? "Series",
                categoryId: catId,
                seriesId: seriesId,
                order: order,
                rating: item["rating"] as? Double ?? Double(item["rating"] as? String ?? "") ?? 0.0,
                tmdbId: Int(item["tmdb_id"] as? String ?? ""),
                overview: item["plot"] as? String,
                genres: item["genre"] as? String,
                cast: item["cast"] as? String
            )

            items.append(mediaItem)
            order += 1
        }

        return items
    }

    func getSeriesInfo(serverUrl: String, username: String, password: String, seriesId: String) async throws -> SeriesFullInfo? {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        guard let url = URL(string: "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)&action=get_series_info&series_id=\(seriesId)") else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return nil
        }

        let info = json["info"] as? [String: Any]
        let episodes = json["episodes"] as? [String: [[String: Any]]]

        var seasons: [SeriesSeasonInfo] = []

        if let episodes = episodes {
            for (seasonKey, episodesArray) in episodes {
                let seasonNum = Int(seasonKey) ?? 0

                let episodeList: [EpisodeInfo] = episodesArray.compactMap { ep in
                    guard let episodeId = ep["id"] as? String ?? (ep["id"] as? Int).map(String.init) else {
                        return nil
                    }

                    let extension_ = ep["container_extension"] as? String ?? "mp4"

                    return EpisodeInfo(
                        id: episodeId,
                        episodeNumber: ep["episode_num"] as? Int ?? Int(ep["episode_num"] as? String ?? "") ?? 0,
                        title: ep["title"] as? String ?? "Episode",
                        overview: ep["plot"] as? String ?? "",
                        duration: ep["duration"] as? String ?? "",
                        posterUrl: (ep["info"] as? [String: Any])?["movie_image"] as? String,
                        streamUrl: "\(normalizedUrl)/series/\(username)/\(password)/\(episodeId).\(extension_)",
                        containerExtension: extension_,
                        rating: ep["rating"] as? Double ?? 0.0
                    )
                }

                seasons.append(SeriesSeasonInfo(
                    seasonNumber: seasonNum,
                    episodes: episodeList
                ))
            }
        }

        return SeriesFullInfo(
            seriesId: seriesId,
            name: info?["name"] as? String ?? "",
            overview: info?["plot"] as? String ?? "",
            posterUrl: info?["cover"] as? String,
            backdropUrl: (info?["backdrop_path"] as? [String])?.first,
            rating: info?["rating"] as? Double ?? 0.0,
            genre: info?["genre"] as? String ?? "",
            director: info?["director"] as? String ?? "",
            cast: info?["cast"] as? String ?? "",
            releaseDate: info?["releaseDate"] as? String ?? "",
            tmdbId: Int(info?["tmdb_id"] as? String ?? ""),
            seasons: seasons.sorted { $0.seasonNumber < $1.seasonNumber }
        )
    }

    // MARK: - Catch-up / Timeshift

    func getCatchupURL(serverUrl: String, username: String, password: String, streamId: String, startTime: Date, durationMinutes: Int) -> String {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd:HH-mm"
        formatter.timeZone = TimeZone(identifier: "UTC")
        let startUtc = formatter.string(from: startTime)

        return "\(normalizedUrl)/timeshift/\(username)/\(password)/\(durationMinutes)/\(startUtc)/\(streamId).m3u8"
    }

    func getCatchupURLSimple(serverUrl: String, username: String, password: String, streamId: String, startTime: Date) -> String {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        let start = Int(startTime.timeIntervalSince1970)

        return "\(normalizedUrl)/streaming/timeshift.php?username=\(username)&password=\(password)&stream=\(streamId)&start=\(start)"
    }

    // MARK: - Short EPG

    func getShortEpg(serverUrl: String, username: String, password: String, streamId: String, limit: Int = 10) async throws -> [XtreamEpgEntry] {
        let normalizedUrl = normalizeServerUrl(serverUrl)
        guard let url = URL(string: "\(normalizedUrl)/player_api.php?username=\(username)&password=\(password)&action=get_short_epg&stream_id=\(streamId)&limit=\(limit)") else {
            throw XtreamError.invalidURL
        }

        let (data, _) = try await session.data(from: url)

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let epgListings = json["epg_listings"] as? [[String: Any]] else {
            return []
        }

        return epgListings.compactMap { item in
            XtreamEpgEntry(
                id: item["id"] as? String ?? "",
                title: decodeBase64(item["title"] as? String ?? ""),
                description: decodeBase64(item["description"] as? String ?? ""),
                start: parseEpgDateTime(item["start"] as? String),
                end: parseEpgDateTime(item["end"] as? String),
                startTimestamp: item["start_timestamp"] as? String ?? "",
                stopTimestamp: item["stop_timestamp"] as? String ?? ""
            )
        }
    }

    // MARK: - Helpers

    private func normalizeServerUrl(_ url: String) -> String {
        var normalized = url.trimmingCharacters(in: .whitespacesAndNewlines)
        if normalized.hasSuffix("/") {
            normalized = String(normalized.dropLast())
        }
        if !normalized.hasPrefix("http://") && !normalized.hasPrefix("https://") {
            normalized = "http://\(normalized)"
        }
        return normalized
    }

    private func decodeBase64(_ base64: String) -> String {
        guard !base64.isEmpty,
              let data = Data(base64Encoded: base64),
              let decoded = String(data: data, encoding: .utf8) else {
            return base64
        }
        return decoded
    }

    private func parseEpgDateTime(_ dateStr: String?) -> Date? {
        guard let dateStr = dateStr, !dateStr.isEmpty else { return nil }
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
        return formatter.date(from: dateStr)
    }
}

// MARK: - Errors

enum XtreamError: Error {
    case invalidURL
    case authenticationFailed
    case networkError(Error)
}
