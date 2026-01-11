import SwiftUI
import AVFoundation

@main
struct StreamVisionApp: App {
    @StateObject private var viewModel = MainViewModel()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(viewModel)
        }
        .windowStyle(.hiddenTitleBar)
        .commands {
            CommandGroup(replacing: .newItem) { }

            CommandMenu("Playback") {
                Button("Play/Pause") {
                    viewModel.togglePlayPause()
                }
                .keyboardShortcut(.space, modifiers: [])

                Button("Stop") {
                    viewModel.stop()
                }
                .keyboardShortcut("s", modifiers: .command)

                Divider()

                Button("Previous Channel") {
                    viewModel.previousChannel()
                }
                .keyboardShortcut(.upArrow, modifiers: [])

                Button("Next Channel") {
                    viewModel.nextChannel()
                }
                .keyboardShortcut(.downArrow, modifiers: [])

                Divider()

                Button("Toggle Mute") {
                    viewModel.toggleMute()
                }
                .keyboardShortcut("m", modifiers: .command)
            }

            CommandMenu("Playlist") {
                Button("Add Playlist...") {
                    viewModel.showAddPlaylistSheet = true
                }
                .keyboardShortcut("n", modifiers: .command)

                Button("Refresh Playlist") {
                    Task { await viewModel.refreshPlaylist() }
                }
                .keyboardShortcut("r", modifiers: .command)
            }
        }

        Settings {
            SettingsView()
                .environmentObject(viewModel)
        }
    }
}
