import Foundation
import AVFoundation
import Combine

@MainActor
class MainViewModel: ObservableObject {

    // MARK: - Published Properties

    @Published var playlistSources: [PlaylistSource] = []
    @Published var selectedSource: PlaylistSource?
    @Published var channelGroups: [ChannelGroup] = []
    @Published var filteredChannels: [Channel] = []
    @Published var favoriteChannels: [Channel] = []
    @Published var recentChannels: [Channel] = []

    @Published var currentChannel: Channel?
    @Published var currentProgram: EpgProgram?

    @Published var searchQuery: String = "" {
        didSet { filterChannels() }
    }

    @Published var isLoading: Bool = false
    @Published var statusMessage: String = "Ready"

    @Published var isPlaying: Bool = false
    @Published var isMuted: Bool = false
    @Published var volume: Float = 1.0 {
        didSet { player?.volume = volume }
    }

    @Published var currentView: NavigationView = .channels
    @Published var showAddPlaylistSheet: Bool = false

    // MARK: - Player

    private(set) var player: AVPlayer?
    private var playerObserver: Any?

    // MARK: - Services

    private let dataStore = DataStore()
    private let m3uParser = M3UParser()
    private let xtreamService = XtreamCodesService()

    private var allChannels: [Channel] = []
    private var favoriteIds: Set<UUID> = []

    // MARK: - Navigation

    enum NavigationView {
        case channels
        case favorites
        case recent
        case epg
    }

    // MARK: - Initialization

    init() {
        player = AVPlayer()
        setupPlayerObserver()
    }

    private func setupPlayerObserver() {
        playerObserver = player?.observe(\.timeControlStatus, options: [.new]) { [weak self] player, _ in
            Task { @MainActor in
                self?.isPlaying = player.timeControlStatus == .playing
            }
        }
    }

    // MARK: - Loading Data

    func loadData() async {
        isLoading = true
        statusMessage = "Loading..."

        do {
            playlistSources = try await dataStore.loadPlaylistSources()
            favoriteIds = try await dataStore.loadFavorites()

            if let firstSource = playlistSources.first {
                selectedSource = firstSource
                await loadChannelsForSource(firstSource)
            }

            await loadFavoriteChannels()
            await loadRecentChannels()

            statusMessage = "Ready"
        } catch {
            statusMessage = "Error loading data: \(error.localizedDescription)"
        }

        isLoading = false
    }

    func loadChannelsForSource(_ source: PlaylistSource) async {
        isLoading = true
        statusMessage = "Loading channels..."

        do {
            allChannels = try await dataStore.loadChannels(forSourceId: source.id)

            // Update favorite status
            for i in allChannels.indices {
                allChannels[i].isFavorite = favoriteIds.contains(allChannels[i].id)
            }

            organizeChannelsIntoGroups()
            statusMessage = "Loaded \(allChannels.count) channels"
        } catch {
            statusMessage = "Error: \(error.localizedDescription)"
        }

        isLoading = false
    }

    private func organizeChannelsIntoGroups() {
        let grouped = Dictionary(grouping: allChannels) { $0.groupTitle }
        channelGroups = grouped.map { ChannelGroup(name: $0.key, channels: $0.value.sorted { $0.order < $1.order }) }
            .sorted { $0.name < $1.name }
        filterChannels()
    }

    private func filterChannels() {
        if searchQuery.isEmpty {
            filteredChannels = allChannels
        } else {
            filteredChannels = allChannels.filter {
                $0.name.localizedCaseInsensitiveContains(searchQuery) ||
                $0.groupTitle.localizedCaseInsensitiveContains(searchQuery)
            }
        }
    }

    private func loadFavoriteChannels() async {
        favoriteChannels = allChannels.filter { favoriteIds.contains($0.id) }
    }

    private func loadRecentChannels() async {
        do {
            let recentIds = try await dataStore.loadRecentChannels()
            recentChannels = recentIds.compactMap { id in
                allChannels.first { $0.id == id }
            }
        } catch {
            recentChannels = []
        }
    }

    // MARK: - Playlist Management

    func addM3UPlaylist(name: String, url: String, epgUrl: String?) async {
        isLoading = true
        statusMessage = "Adding M3U playlist..."

        do {
            guard let playlistURL = URL(string: url) else {
                statusMessage = "Invalid URL"
                isLoading = false
                return
            }

            var source = PlaylistSource(
                name: name.isEmpty ? "M3U Playlist" : name,
                type: .m3u,
                url: url,
                epgUrl: epgUrl
            )

            let channels = try await m3uParser.parseFromURL(playlistURL, sourceId: source.id)
            source.channelCount = channels.count

            try await dataStore.saveChannels(channels, forSourceId: source.id)

            playlistSources.append(source)
            try await dataStore.savePlaylistSources(playlistSources)

            selectedSource = source
            await loadChannelsForSource(source)

            statusMessage = "Added \(channels.count) channels"
        } catch {
            statusMessage = "Error: \(error.localizedDescription)"
        }

        isLoading = false
    }

    func addXtreamPlaylist(name: String, serverUrl: String, username: String, password: String) async {
        isLoading = true
        statusMessage = "Connecting to Xtream Codes..."

        do {
            guard let _ = try await xtreamService.authenticate(serverUrl: serverUrl, username: username, password: password) else {
                statusMessage = "Authentication failed"
                isLoading = false
                return
            }

            var source = PlaylistSource(
                name: name.isEmpty ? "Xtream - \(username)" : name,
                type: .xtreamCodes,
                url: serverUrl,
                username: username,
                password: password
            )

            statusMessage = "Loading channels..."
            let channels = try await xtreamService.getLiveStreams(
                serverUrl: serverUrl,
                username: username,
                password: password,
                sourceId: source.id
            )
            source.channelCount = channels.count

            try await dataStore.saveChannels(channels, forSourceId: source.id)

            playlistSources.append(source)
            try await dataStore.savePlaylistSources(playlistSources)

            selectedSource = source
            await loadChannelsForSource(source)

            statusMessage = "Added \(channels.count) channels"
        } catch {
            statusMessage = "Error: \(error.localizedDescription)"
        }

        isLoading = false
    }

    func refreshPlaylist() async {
        guard let source = selectedSource else { return }

        isLoading = true
        statusMessage = "Refreshing playlist..."

        do {
            try await dataStore.deleteChannels(forSourceId: source.id)

            var channels: [Channel]
            if source.type == .xtreamCodes, let username = source.username, let password = source.password {
                channels = try await xtreamService.getLiveStreams(
                    serverUrl: source.url,
                    username: username,
                    password: password,
                    sourceId: source.id
                )
            } else {
                guard let url = URL(string: source.url) else {
                    statusMessage = "Invalid URL"
                    isLoading = false
                    return
                }
                channels = try await m3uParser.parseFromURL(url, sourceId: source.id)
            }

            try await dataStore.saveChannels(channels, forSourceId: source.id)

            // Update source
            if let index = playlistSources.firstIndex(where: { $0.id == source.id }) {
                playlistSources[index].lastSync = Date()
                playlistSources[index].channelCount = channels.count
                selectedSource = playlistSources[index]
                try await dataStore.savePlaylistSources(playlistSources)
            }

            await loadChannelsForSource(source)
            statusMessage = "Refreshed \(channels.count) channels"
        } catch {
            statusMessage = "Error: \(error.localizedDescription)"
        }

        isLoading = false
    }

    func deletePlaylist(_ source: PlaylistSource) async {
        do {
            try await dataStore.deleteChannels(forSourceId: source.id)
            playlistSources.removeAll { $0.id == source.id }
            try await dataStore.savePlaylistSources(playlistSources)

            if selectedSource?.id == source.id {
                selectedSource = playlistSources.first
                if let newSource = selectedSource {
                    await loadChannelsForSource(newSource)
                } else {
                    channelGroups = []
                    filteredChannels = []
                }
            }

            statusMessage = "Playlist deleted"
        } catch {
            statusMessage = "Error: \(error.localizedDescription)"
        }
    }

    // MARK: - Playback

    func playChannel(_ channel: Channel) {
        currentChannel?.isFavorite = favoriteIds.contains(currentChannel?.id ?? UUID())

        currentChannel = channel

        guard let url = URL(string: channel.streamUrl) else {
            statusMessage = "Invalid stream URL"
            return
        }

        let playerItem = AVPlayerItem(url: url)
        player?.replaceCurrentItem(with: playerItem)
        player?.play()

        statusMessage = "Playing: \(channel.name)"

        // Add to recent
        Task {
            try? await dataStore.addRecentChannel(channel.id)
            await loadRecentChannels()
        }
    }

    func togglePlayPause() {
        if isPlaying {
            player?.pause()
        } else {
            player?.play()
        }
    }

    func stop() {
        player?.pause()
        player?.replaceCurrentItem(with: nil)
        currentChannel = nil
        currentProgram = nil
        statusMessage = "Stopped"
    }

    func toggleMute() {
        isMuted.toggle()
        player?.isMuted = isMuted
    }

    func previousChannel() {
        guard let current = currentChannel,
              let index = filteredChannels.firstIndex(of: current),
              index > 0 else { return }
        playChannel(filteredChannels[index - 1])
    }

    func nextChannel() {
        guard let current = currentChannel,
              let index = filteredChannels.firstIndex(of: current),
              index < filteredChannels.count - 1 else { return }
        playChannel(filteredChannels[index + 1])
    }

    // MARK: - Favorites

    func toggleFavorite(_ channel: Channel) async {
        if favoriteIds.contains(channel.id) {
            favoriteIds.remove(channel.id)
        } else {
            favoriteIds.insert(channel.id)
        }

        // Update channel in list
        if let index = allChannels.firstIndex(where: { $0.id == channel.id }) {
            allChannels[index].isFavorite = favoriteIds.contains(channel.id)
        }

        do {
            try await dataStore.saveFavorites(favoriteIds)
            await loadFavoriteChannels()
            filterChannels()
        } catch {
            statusMessage = "Error saving favorites"
        }
    }
}
