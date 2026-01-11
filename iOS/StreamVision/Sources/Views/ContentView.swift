import SwiftUI
import AVKit

struct ContentView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        TabView(selection: $viewModel.currentTab) {
            ChannelsView()
                .tabItem {
                    Label("Channels", systemImage: "tv")
                }
                .tag(MainViewModel.Tab.channels)

            FavoritesView()
                .tabItem {
                    Label("Favorites", systemImage: "star.fill")
                }
                .tag(MainViewModel.Tab.favorites)

            RecentView()
                .tabItem {
                    Label("Recent", systemImage: "clock")
                }
                .tag(MainViewModel.Tab.recent)

            SettingsView()
                .tabItem {
                    Label("Settings", systemImage: "gear")
                }
                .tag(MainViewModel.Tab.settings)
        }
        .task {
            await viewModel.loadData()
        }
        .sheet(isPresented: $viewModel.showAddPlaylistSheet) {
            AddPlaylistView()
        }
        .fullScreenCover(isPresented: $viewModel.showPlayerFullScreen) {
            FullScreenPlayerView()
        }
        .alert("Error", isPresented: $viewModel.showError) {
            Button("OK", role: .cancel) { }
        } message: {
            Text(viewModel.errorMessage ?? "Unknown error")
        }
    }
}

// MARK: - Channels View
struct ChannelsView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                // Mini Player
                if viewModel.currentChannel != nil {
                    MiniPlayerView()
                }

                // Channel List
                List {
                    if viewModel.playlistSources.isEmpty {
                        EmptyStateView(
                            icon: "tv.slash",
                            title: "No Playlists",
                            message: "Add a playlist to get started"
                        )
                    } else {
                        ForEach(viewModel.channelGroups) { group in
                            Section(header: Text(group.name)) {
                                ForEach(group.channels) { channel in
                                    ChannelRowView(channel: channel)
                                        .onTapGesture {
                                            viewModel.playChannel(channel)
                                        }
                                }
                            }
                        }
                    }
                }
                .listStyle(.insetGrouped)
                .searchable(text: $viewModel.searchQuery, prompt: "Search channels")
                .refreshable {
                    await viewModel.refreshPlaylist()
                }
            }
            .navigationTitle("Channels")
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    if !viewModel.playlistSources.isEmpty {
                        Menu {
                            ForEach(viewModel.playlistSources) { source in
                                Button(action: {
                                    viewModel.selectedSource = source
                                    Task { await viewModel.loadChannelsForSource(source) }
                                }) {
                                    HStack {
                                        Text(source.name)
                                        if source.id == viewModel.selectedSource?.id {
                                            Image(systemName: "checkmark")
                                        }
                                    }
                                }
                            }
                        } label: {
                            HStack {
                                Text(viewModel.selectedSource?.name ?? "Select")
                                    .lineLimit(1)
                                Image(systemName: "chevron.down")
                            }
                        }
                    }
                }

                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(action: { viewModel.showAddPlaylistSheet = true }) {
                        Image(systemName: "plus")
                    }
                }
            }
            .overlay {
                if viewModel.isLoading {
                    ProgressView()
                        .scaleEffect(1.5)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                        .background(Color.black.opacity(0.3))
                }
            }
        }
    }
}

// MARK: - Channel Row View
struct ChannelRowView: View {
    @EnvironmentObject var viewModel: MainViewModel
    let channel: Channel

    var body: some View {
        HStack(spacing: 12) {
            // Logo
            AsyncImage(url: URL(string: channel.logoUrl ?? "")) { image in
                image
                    .resizable()
                    .aspectRatio(contentMode: .fill)
            } placeholder: {
                RoundedRectangle(cornerRadius: 8)
                    .fill(Color.secondary.opacity(0.2))
                    .overlay(
                        Image(systemName: "tv")
                            .foregroundColor(.secondary)
                    )
            }
            .frame(width: 50, height: 50)
            .clipShape(RoundedRectangle(cornerRadius: 8))

            // Info
            VStack(alignment: .leading, spacing: 4) {
                Text(channel.name)
                    .font(.headline)
                    .lineLimit(1)
                Text(channel.groupTitle)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(1)
            }

            Spacer()

            // Playing Indicator
            if viewModel.currentChannel?.id == channel.id {
                Image(systemName: "speaker.wave.2.fill")
                    .foregroundColor(.accentColor)
                    .symbolEffect(.pulse)
            }

            // Favorite Button
            Button(action: {
                Task { await viewModel.toggleFavorite(channel) }
            }) {
                Image(systemName: channel.isFavorite ? "star.fill" : "star")
                    .foregroundColor(channel.isFavorite ? .yellow : .secondary)
            }
            .buttonStyle(.plain)
        }
        .padding(.vertical, 4)
        .contentShape(Rectangle())
    }
}

// MARK: - Mini Player View
struct MiniPlayerView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        HStack(spacing: 12) {
            // Thumbnail
            if let player = viewModel.player {
                VideoPlayer(player: player)
                    .frame(width: 80, height: 45)
                    .clipShape(RoundedRectangle(cornerRadius: 6))
                    .onTapGesture {
                        viewModel.showPlayerFullScreen = true
                    }
            }

            // Info
            VStack(alignment: .leading, spacing: 2) {
                Text(viewModel.currentChannel?.name ?? "")
                    .font(.subheadline.weight(.semibold))
                    .lineLimit(1)
                Text(viewModel.statusMessage)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(1)
            }

            Spacer()

            // Controls
            HStack(spacing: 16) {
                Button(action: viewModel.togglePlayPause) {
                    Image(systemName: viewModel.isPlaying ? "pause.fill" : "play.fill")
                        .font(.title2)
                }

                Button(action: viewModel.stop) {
                    Image(systemName: "xmark")
                        .font(.title3)
                }
            }
            .foregroundColor(.primary)
        }
        .padding()
        .background(Color(uiColor: .secondarySystemBackground))
    }
}

// MARK: - Full Screen Player View
struct FullScreenPlayerView: View {
    @EnvironmentObject var viewModel: MainViewModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ZStack {
            Color.black.edgesIgnoringSafeArea(.all)

            if let player = viewModel.player {
                VideoPlayer(player: player)
                    .edgesIgnoringSafeArea(.all)
            }

            VStack {
                HStack {
                    Button(action: { dismiss() }) {
                        Image(systemName: "chevron.down")
                            .font(.title2)
                            .foregroundColor(.white)
                            .padding()
                            .background(Circle().fill(Color.black.opacity(0.5)))
                    }

                    Spacer()

                    VStack(alignment: .trailing) {
                        Text(viewModel.currentChannel?.name ?? "")
                            .font(.headline)
                            .foregroundColor(.white)
                        if let program = viewModel.currentProgram {
                            Text(program.title)
                                .font(.subheadline)
                                .foregroundColor(.white.opacity(0.8))
                        }
                    }
                }
                .padding()

                Spacer()

                // Bottom Controls
                HStack(spacing: 40) {
                    Button(action: viewModel.previousChannel) {
                        Image(systemName: "backward.fill")
                            .font(.title)
                    }

                    Button(action: viewModel.togglePlayPause) {
                        Image(systemName: viewModel.isPlaying ? "pause.fill" : "play.fill")
                            .font(.largeTitle)
                    }

                    Button(action: viewModel.nextChannel) {
                        Image(systemName: "forward.fill")
                            .font(.title)
                    }
                }
                .foregroundColor(.white)
                .padding()
                .background(Capsule().fill(Color.black.opacity(0.5)))
                .padding(.bottom, 40)
            }
        }
        .statusBarHidden()
    }
}

// MARK: - Favorites View
struct FavoritesView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                if viewModel.currentChannel != nil {
                    MiniPlayerView()
                }

                List {
                    if viewModel.favoriteChannels.isEmpty {
                        EmptyStateView(
                            icon: "star.slash",
                            title: "No Favorites",
                            message: "Tap the star on a channel to add it here"
                        )
                    } else {
                        ForEach(viewModel.favoriteChannels) { channel in
                            ChannelRowView(channel: channel)
                                .onTapGesture {
                                    viewModel.playChannel(channel)
                                }
                        }
                    }
                }
                .listStyle(.insetGrouped)
            }
            .navigationTitle("Favorites")
        }
    }
}

// MARK: - Recent View
struct RecentView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                if viewModel.currentChannel != nil {
                    MiniPlayerView()
                }

                List {
                    if viewModel.recentChannels.isEmpty {
                        EmptyStateView(
                            icon: "clock",
                            title: "No Recent Channels",
                            message: "Channels you watch will appear here"
                        )
                    } else {
                        ForEach(viewModel.recentChannels) { channel in
                            ChannelRowView(channel: channel)
                                .onTapGesture {
                                    viewModel.playChannel(channel)
                                }
                        }
                    }
                }
                .listStyle(.insetGrouped)
            }
            .navigationTitle("Recent")
        }
    }
}

// MARK: - Settings View
struct SettingsView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        NavigationStack {
            List {
                Section("Playlists") {
                    ForEach(viewModel.playlistSources) { source in
                        HStack {
                            VStack(alignment: .leading) {
                                Text(source.name)
                                    .font(.headline)
                                Text("\(source.channelCount) channels")
                                    .font(.caption)
                                    .foregroundColor(.secondary)
                            }
                            Spacer()
                            Text(source.type.rawValue)
                                .font(.caption)
                                .foregroundColor(.secondary)
                        }
                    }
                    .onDelete { indexSet in
                        Task {
                            for index in indexSet {
                                await viewModel.deletePlaylist(viewModel.playlistSources[index])
                            }
                        }
                    }

                    Button(action: { viewModel.showAddPlaylistSheet = true }) {
                        Label("Add Playlist", systemImage: "plus")
                    }
                }

                Section("Playback") {
                    Toggle("Picture in Picture", isOn: .constant(true))
                    Toggle("Background Playback", isOn: .constant(true))
                }

                Section("About") {
                    HStack {
                        Text("Version")
                        Spacer()
                        Text("1.0.0")
                            .foregroundColor(.secondary)
                    }
                }
            }
            .navigationTitle("Settings")
        }
    }
}

// MARK: - Add Playlist View
struct AddPlaylistView: View {
    @EnvironmentObject var viewModel: MainViewModel
    @Environment(\.dismiss) private var dismiss

    @State private var selectedType: SourceType = .m3u
    @State private var name = ""
    @State private var url = ""
    @State private var username = ""
    @State private var password = ""
    @State private var epgUrl = ""

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Picker("Type", selection: $selectedType) {
                        ForEach(SourceType.allCases, id: \.self) { type in
                            Text(type.rawValue).tag(type)
                        }
                    }
                    .pickerStyle(.segmented)
                }

                Section("Details") {
                    TextField("Playlist Name", text: $name)

                    TextField(selectedType == .xtreamCodes ? "Server URL" : "Playlist URL", text: $url)
                        .textContentType(.URL)
                        .autocapitalization(.none)
                        .keyboardType(.URL)

                    if selectedType == .xtreamCodes {
                        TextField("Username", text: $username)
                            .autocapitalization(.none)
                        SecureField("Password", text: $password)
                    }
                }

                Section("Optional") {
                    TextField("EPG URL", text: $epgUrl)
                        .textContentType(.URL)
                        .autocapitalization(.none)
                        .keyboardType(.URL)
                }
            }
            .navigationTitle("Add Playlist")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Add") {
                        Task {
                            if selectedType == .xtreamCodes {
                                await viewModel.addXtreamPlaylist(
                                    name: name,
                                    serverUrl: url,
                                    username: username,
                                    password: password
                                )
                            } else {
                                await viewModel.addM3UPlaylist(
                                    name: name,
                                    url: url,
                                    epgUrl: epgUrl.isEmpty ? nil : epgUrl
                                )
                            }
                            dismiss()
                        }
                    }
                    .disabled(url.isEmpty)
                }
            }
        }
    }
}

// MARK: - Empty State View
struct EmptyStateView: View {
    let icon: String
    let title: String
    let message: String

    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: icon)
                .font(.system(size: 48))
                .foregroundColor(.secondary)
            Text(title)
                .font(.headline)
            Text(message)
                .font(.subheadline)
                .foregroundColor(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}

#Preview {
    ContentView()
        .environmentObject(MainViewModel())
}
