import SwiftUI
import AVKit

struct ContentView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        NavigationSplitView {
            SidebarView()
        } detail: {
            MainPlayerView()
        }
        .navigationSplitViewStyle(.balanced)
        .frame(minWidth: 1200, minHeight: 700)
        .background(Color(nsColor: .windowBackgroundColor))
        .task {
            await viewModel.loadData()
        }
        .sheet(isPresented: $viewModel.showAddPlaylistSheet) {
            AddPlaylistView()
        }
    }
}

// MARK: - Sidebar View
struct SidebarView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // Header
            VStack(alignment: .leading, spacing: 4) {
                Text("StreamVision")
                    .font(.title.bold())
                    .foregroundColor(.primary)
                Text("Universal Media Player")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            .padding()

            // Playlist Selector
            if !viewModel.playlistSources.isEmpty {
                Picker("Playlist", selection: $viewModel.selectedSource) {
                    ForEach(viewModel.playlistSources) { source in
                        Text(source.name).tag(source as PlaylistSource?)
                    }
                }
                .pickerStyle(.menu)
                .padding(.horizontal)
                .onChange(of: viewModel.selectedSource) { _, newValue in
                    if let source = newValue {
                        Task { await viewModel.loadChannelsForSource(source) }
                    }
                }
            }

            // Search
            HStack {
                Image(systemName: "magnifyingglass")
                    .foregroundColor(.secondary)
                TextField("Search channels...", text: $viewModel.searchQuery)
                    .textFieldStyle(.plain)

                if !viewModel.searchQuery.isEmpty {
                    Button(action: { viewModel.searchQuery = "" }) {
                        Image(systemName: "xmark.circle.fill")
                            .foregroundColor(.secondary)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(10)
            .background(Color(nsColor: .controlBackgroundColor))
            .cornerRadius(8)
            .padding()

            // Navigation Tabs
            HStack(spacing: 0) {
                NavigationTabButton(title: "All", view: .channels, currentView: $viewModel.currentView)
                NavigationTabButton(title: "Favorites", view: .favorites, currentView: $viewModel.currentView)
                NavigationTabButton(title: "Recent", view: .recent, currentView: $viewModel.currentView)
            }
            .padding(.horizontal)

            Divider()
                .padding(.top, 8)

            // Channel List
            ScrollView {
                LazyVStack(spacing: 2) {
                    ForEach(currentChannels) { channel in
                        ChannelRowView(channel: channel)
                            .onTapGesture {
                                viewModel.playChannel(channel)
                            }
                    }
                }
                .padding(.vertical, 8)
            }

            Divider()

            // Add Playlist Button
            Button(action: { viewModel.showAddPlaylistSheet = true }) {
                HStack {
                    Image(systemName: "plus.circle.fill")
                    Text("Add Playlist")
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
            }
            .buttonStyle(.plain)
            .background(Color.accentColor.opacity(0.1))
            .cornerRadius(8)
            .padding()
        }
        .frame(minWidth: 280)
    }

    private var currentChannels: [Channel] {
        switch viewModel.currentView {
        case .channels:
            return viewModel.filteredChannels
        case .favorites:
            return viewModel.favoriteChannels
        case .recent:
            return viewModel.recentChannels
        case .epg:
            return viewModel.filteredChannels
        }
    }
}

// MARK: - Navigation Tab Button
struct NavigationTabButton: View {
    let title: String
    let view: MainViewModel.NavigationView
    @Binding var currentView: MainViewModel.NavigationView

    var body: some View {
        Button(action: { currentView = view }) {
            Text(title)
                .font(.subheadline.weight(currentView == view ? .semibold : .regular))
                .foregroundColor(currentView == view ? .accentColor : .secondary)
                .padding(.vertical, 8)
                .padding(.horizontal, 12)
                .background(
                    currentView == view ?
                    Color.accentColor.opacity(0.15) : Color.clear
                )
                .cornerRadius(6)
        }
        .buttonStyle(.plain)
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
                RoundedRectangle(cornerRadius: 6)
                    .fill(Color.secondary.opacity(0.2))
                    .overlay(
                        Image(systemName: "tv")
                            .foregroundColor(.secondary)
                    )
            }
            .frame(width: 40, height: 40)
            .clipShape(RoundedRectangle(cornerRadius: 6))

            // Info
            VStack(alignment: .leading, spacing: 2) {
                Text(channel.name)
                    .font(.subheadline.weight(.medium))
                    .lineLimit(1)
                Text(channel.groupTitle)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(1)
            }

            Spacer()

            // Favorite Button
            Button(action: {
                Task { await viewModel.toggleFavorite(channel) }
            }) {
                Image(systemName: channel.isFavorite ? "star.fill" : "star")
                    .foregroundColor(channel.isFavorite ? .yellow : .secondary)
            }
            .buttonStyle(.plain)

            // Playing Indicator
            if viewModel.currentChannel?.id == channel.id {
                Image(systemName: "speaker.wave.2.fill")
                    .foregroundColor(.accentColor)
                    .symbolEffect(.pulse)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(
            viewModel.currentChannel?.id == channel.id ?
            Color.accentColor.opacity(0.15) : Color.clear
        )
        .cornerRadius(8)
        .contentShape(Rectangle())
    }
}

// MARK: - Main Player View
struct MainPlayerView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        VStack(spacing: 0) {
            // Video Player
            ZStack {
                Color.black

                if let player = viewModel.player {
                    VideoPlayer(player: player)
                }

                if viewModel.isLoading {
                    ProgressView()
                        .scaleEffect(1.5)
                        .progressViewStyle(.circular)
                }

                if viewModel.currentChannel == nil {
                    VStack(spacing: 16) {
                        Image(systemName: "tv")
                            .font(.system(size: 64))
                            .foregroundColor(.secondary)
                        Text("Select a channel to start watching")
                            .font(.title3)
                            .foregroundColor(.secondary)
                    }
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .clipShape(RoundedRectangle(cornerRadius: 12))
            .padding()

            // Program Info
            if let channel = viewModel.currentChannel {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(channel.name)
                            .font(.headline)
                        if let program = viewModel.currentProgram {
                            Text(program.title)
                                .font(.subheadline)
                                .foregroundColor(.secondary)
                            Text(program.timeRange)
                                .font(.caption)
                                .foregroundColor(.accentColor)
                        }
                    }

                    Spacer()

                    Button("EPG Guide") {
                        viewModel.currentView = .epg
                    }
                    .buttonStyle(.bordered)
                }
                .padding(.horizontal)
            }

            // Playback Controls
            PlaybackControlsView()
                .padding()
        }
    }
}

// MARK: - Playback Controls View
struct PlaybackControlsView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        HStack(spacing: 20) {
            // Playback Buttons
            HStack(spacing: 12) {
                Button(action: viewModel.previousChannel) {
                    Image(systemName: "backward.fill")
                        .font(.title2)
                }
                .buttonStyle(.plain)

                Button(action: viewModel.togglePlayPause) {
                    Image(systemName: viewModel.isPlaying ? "pause.fill" : "play.fill")
                        .font(.title)
                }
                .buttonStyle(.plain)

                Button(action: viewModel.stop) {
                    Image(systemName: "stop.fill")
                        .font(.title2)
                }
                .buttonStyle(.plain)

                Button(action: viewModel.nextChannel) {
                    Image(systemName: "forward.fill")
                        .font(.title2)
                }
                .buttonStyle(.plain)
            }

            Spacer()

            // Status
            Text(viewModel.statusMessage)
                .font(.caption)
                .foregroundColor(.secondary)

            Spacer()

            // Volume
            HStack(spacing: 8) {
                Button(action: viewModel.toggleMute) {
                    Image(systemName: viewModel.isMuted ? "speaker.slash.fill" : "speaker.wave.2.fill")
                        .font(.title3)
                }
                .buttonStyle(.plain)

                Slider(value: $viewModel.volume, in: 0...1)
                    .frame(width: 100)
            }
        }
        .padding()
        .background(Color(nsColor: .controlBackgroundColor))
        .cornerRadius(12)
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
        VStack(spacing: 20) {
            Text("Add Playlist")
                .font(.title.bold())

            Picker("Type", selection: $selectedType) {
                ForEach(SourceType.allCases, id: \.self) { type in
                    Text(type.rawValue).tag(type)
                }
            }
            .pickerStyle(.segmented)

            Form {
                TextField("Playlist Name", text: $name)

                TextField(selectedType == .xtreamCodes ? "Server URL" : "Playlist URL", text: $url)

                if selectedType == .xtreamCodes {
                    TextField("Username", text: $username)
                    SecureField("Password", text: $password)
                }

                TextField("EPG URL (Optional)", text: $epgUrl)
            }
            .formStyle(.grouped)

            HStack {
                Button("Cancel") {
                    dismiss()
                }
                .keyboardShortcut(.escape)

                Spacer()

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
                .keyboardShortcut(.return)
                .buttonStyle(.borderedProminent)
                .disabled(url.isEmpty)
            }
        }
        .padding(24)
        .frame(width: 450)
    }
}

// MARK: - Settings View
struct SettingsView: View {
    @EnvironmentObject var viewModel: MainViewModel

    var body: some View {
        TabView {
            GeneralSettingsView()
                .tabItem {
                    Label("General", systemImage: "gear")
                }

            PlaybackSettingsView()
                .tabItem {
                    Label("Playback", systemImage: "play.circle")
                }
        }
        .frame(width: 450, height: 300)
    }
}

struct GeneralSettingsView: View {
    var body: some View {
        Form {
            Toggle("Launch at Login", isOn: .constant(false))
            Toggle("Check for Updates Automatically", isOn: .constant(true))
        }
        .formStyle(.grouped)
        .padding()
    }
}

struct PlaybackSettingsView: View {
    @State private var bufferSize = 2000.0

    var body: some View {
        Form {
            Slider(value: $bufferSize, in: 500...5000, step: 100) {
                Text("Buffer Size: \(Int(bufferSize))ms")
            }

            Toggle("Auto-play on channel select", isOn: .constant(true))
            Toggle("Hardware Acceleration", isOn: .constant(true))
        }
        .formStyle(.grouped)
        .padding()
    }
}

#Preview {
    ContentView()
        .environmentObject(MainViewModel())
}
