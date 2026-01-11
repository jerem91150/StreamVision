import Foundation

actor DataStore {
    private let fileManager = FileManager.default
    private let documentsDir: URL

    init() {
        documentsDir = fileManager.urls(for: .documentDirectory, in: .userDomainMask).first!
    }

    // MARK: - Playlist Sources

    func savePlaylistSources(_ sources: [PlaylistSource]) throws {
        let url = documentsDir.appendingPathComponent("playlists.json")
        let data = try JSONEncoder().encode(sources)
        try data.write(to: url)
    }

    func loadPlaylistSources() throws -> [PlaylistSource] {
        let url = documentsDir.appendingPathComponent("playlists.json")
        guard fileManager.fileExists(atPath: url.path) else { return [] }
        let data = try Data(contentsOf: url)
        return try JSONDecoder().decode([PlaylistSource].self, from: data)
    }

    // MARK: - Channels

    func saveChannels(_ channels: [Channel], forSourceId sourceId: UUID) throws {
        let url = documentsDir.appendingPathComponent("channels_\(sourceId.uuidString).json")
        let data = try JSONEncoder().encode(channels)
        try data.write(to: url)
    }

    func loadChannels(forSourceId sourceId: UUID) throws -> [Channel] {
        let url = documentsDir.appendingPathComponent("channels_\(sourceId.uuidString).json")
        guard fileManager.fileExists(atPath: url.path) else { return [] }
        let data = try Data(contentsOf: url)
        return try JSONDecoder().decode([Channel].self, from: data)
    }

    func deleteChannels(forSourceId sourceId: UUID) throws {
        let url = documentsDir.appendingPathComponent("channels_\(sourceId.uuidString).json")
        if fileManager.fileExists(atPath: url.path) {
            try fileManager.removeItem(at: url)
        }
    }

    // MARK: - Favorites

    func saveFavorites(_ channelIds: Set<UUID>) throws {
        let url = documentsDir.appendingPathComponent("favorites.json")
        let data = try JSONEncoder().encode(Array(channelIds))
        try data.write(to: url)
    }

    func loadFavorites() throws -> Set<UUID> {
        let url = documentsDir.appendingPathComponent("favorites.json")
        guard fileManager.fileExists(atPath: url.path) else { return [] }
        let data = try Data(contentsOf: url)
        let array = try JSONDecoder().decode([UUID].self, from: data)
        return Set(array)
    }

    // MARK: - Settings

    func saveSettings(_ settings: AppSettings) throws {
        let url = documentsDir.appendingPathComponent("settings.json")
        let data = try JSONEncoder().encode(settings)
        try data.write(to: url)
    }

    func loadSettings() throws -> AppSettings {
        let url = documentsDir.appendingPathComponent("settings.json")
        guard fileManager.fileExists(atPath: url.path) else { return AppSettings() }
        let data = try Data(contentsOf: url)
        return try JSONDecoder().decode(AppSettings.self, from: data)
    }

    // MARK: - Recent Channels

    func addRecentChannel(_ channelId: UUID) throws {
        var recent = try loadRecentChannels()
        recent.removeAll { $0 == channelId }
        recent.insert(channelId, at: 0)
        if recent.count > 50 {
            recent = Array(recent.prefix(50))
        }

        let url = documentsDir.appendingPathComponent("recent.json")
        let data = try JSONEncoder().encode(recent)
        try data.write(to: url)
    }

    func loadRecentChannels() throws -> [UUID] {
        let url = documentsDir.appendingPathComponent("recent.json")
        guard fileManager.fileExists(atPath: url.path) else { return [] }
        let data = try Data(contentsOf: url)
        return try JSONDecoder().decode([UUID].self, from: data)
    }
}
