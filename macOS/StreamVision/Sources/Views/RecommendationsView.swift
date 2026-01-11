import SwiftUI

// MARK: - Recommendations View (macOS)
struct RecommendationsView: View {
    let sections: [RecommendationSection]
    let userStats: UserStats?
    let isLoading: Bool
    let onChannelSelect: (RecommendationItem) -> Void

    var body: some View {
        Group {
            if isLoading {
                VStack {
                    ProgressView()
                    Text("Loading recommendations...")
                        .foregroundColor(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if sections.isEmpty {
                EmptyRecommendationsView()
            } else {
                ScrollView {
                    LazyVStack(spacing: 32) {
                        // User Stats Header
                        if let stats = userStats {
                            UserStatsCard(stats: stats)
                                .padding(.horizontal, 24)
                        }

                        // Recommendation Sections
                        ForEach(sections) { section in
                            RecommendationSectionRow(
                                section: section,
                                onItemSelect: onChannelSelect
                            )
                        }
                    }
                    .padding(.vertical, 24)
                }
            }
        }
    }
}

// MARK: - Empty State
struct EmptyRecommendationsView: View {
    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "sparkles.rectangle.stack")
                .font(.system(size: 80))
                .foregroundColor(.secondary)

            Text("Start Watching to Get Recommendations")
                .font(.title2)
                .foregroundColor(.secondary)

            Text("We'll learn your preferences as you watch")
                .font(.body)
                .foregroundColor(.secondary.opacity(0.7))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

// MARK: - User Stats Card
struct UserStatsCard: View {
    let stats: UserStats

    var body: some View {
        HStack(spacing: 32) {
            VStack(alignment: .leading, spacing: 8) {
                Text("Your Viewing Stats")
                    .font(.title3)
                    .fontWeight(.semibold)
                Text("Based on your watch history")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }

            Spacer()

            HStack(spacing: 40) {
                StatItem(
                    icon: "timer",
                    value: formatWatchTime(stats.totalWatchTimeMinutes),
                    label: "Watch Time"
                )

                StatItem(
                    icon: "tv",
                    value: "\(stats.totalChannelsWatched)",
                    label: "Channels"
                )

                StatItem(
                    icon: "heart.fill",
                    value: stats.favoriteCategory,
                    label: "Top Category"
                )

                StatItem(
                    icon: "play.circle",
                    value: "\(stats.watchSessionCount)",
                    label: "Sessions"
                )
            }
        }
        .padding(20)
        .background(Color.accentColor.opacity(0.08))
        .cornerRadius(16)
    }

    private func formatWatchTime(_ minutes: Int) -> String {
        if minutes < 60 {
            return "\(minutes)m"
        } else if minutes < 1440 {
            return "\(minutes / 60)h \(minutes % 60)m"
        } else {
            return "\(minutes / 1440)d \((minutes % 1440) / 60)h"
        }
    }
}

struct StatItem: View {
    let icon: String
    let value: String
    let label: String

    var body: some View {
        VStack(spacing: 6) {
            Image(systemName: icon)
                .font(.title2)
                .foregroundColor(.accentColor)
            Text(value)
                .font(.headline)
            Text(label)
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .frame(minWidth: 80)
    }
}

// MARK: - Recommendation Section Row
struct RecommendationSectionRow: View {
    let section: RecommendationSection
    let onItemSelect: (RecommendationItem) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            // Section Header
            HStack(spacing: 12) {
                Image(systemName: getSectionIcon(section.type))
                    .foregroundColor(getSectionColor(section.type))
                    .font(.title2)

                VStack(alignment: .leading, spacing: 2) {
                    Text(section.title)
                        .font(.title3)
                        .fontWeight(.semibold)
                    Text(section.subtitle)
                        .font(.caption)
                        .foregroundColor(.secondary)
                }

                Spacer()

                Button("See All") {
                    // Show all items in this section
                }
                .buttonStyle(.link)
            }
            .padding(.horizontal, 24)

            // Horizontal scroll of items
            ScrollView(.horizontal, showsIndicators: false) {
                LazyHStack(spacing: 16) {
                    ForEach(section.items) { item in
                        RecommendationCard(item: item)
                            .onTapGesture {
                                onItemSelect(item)
                            }
                    }
                }
                .padding(.horizontal, 24)
            }
        }
    }

    private func getSectionIcon(_ type: RecommendationType) -> String {
        switch type {
        case .continueWatching: return "play.circle.fill"
        case .becauseYouWatched: return "clock.arrow.circlepath"
        case .topPicksForYou: return "star.fill"
        case .trendingNow: return "chart.line.uptrend.xyaxis"
        case .newReleases: return "sparkles"
        case .categoryRecommendation: return "square.grid.2x2.fill"
        case .hiddenGems: return "diamond.fill"
        case .similarContent: return "arrow.left.arrow.right"
        case .timeBasedPicks: return "clock.fill"
        }
    }

    private func getSectionColor(_ type: RecommendationType) -> Color {
        switch type {
        case .continueWatching: return .green
        case .becauseYouWatched: return .blue
        case .topPicksForYou: return .orange
        case .trendingNow: return .red
        case .newReleases: return .purple
        case .categoryRecommendation: return .cyan
        case .hiddenGems: return .pink
        case .similarContent: return .indigo
        case .timeBasedPicks: return .gray
        }
    }
}

// MARK: - Recommendation Card (macOS - larger)
struct RecommendationCard: View {
    let item: RecommendationItem
    @State private var isHovered = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // Thumbnail
            ZStack {
                LinearGradient(
                    colors: [getCategoryColor(item.category), getCategoryColor(item.category).opacity(0.7)],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )

                if let logoUrl = item.logoUrl, let url = URL(string: logoUrl) {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let image):
                            image
                                .resizable()
                                .aspectRatio(contentMode: .fit)
                                .frame(width: 80, height: 80)
                                .clipShape(Circle())
                        default:
                            Image(systemName: "tv")
                                .font(.system(size: 50))
                                .foregroundColor(.white)
                        }
                    }
                } else {
                    Image(systemName: "tv")
                        .font(.system(size: 50))
                        .foregroundColor(.white)
                }

                // Play button on hover
                if isHovered {
                    Color.black.opacity(0.3)
                    Image(systemName: "play.circle.fill")
                        .font(.system(size: 50))
                        .foregroundColor(.white)
                }

                // Progress bar for continue watching
                if item.type == .continueWatching && item.watchedPercentage > 0 {
                    VStack {
                        Spacer()
                        GeometryReader { geo in
                            ZStack(alignment: .leading) {
                                Rectangle()
                                    .fill(Color.black.opacity(0.3))
                                    .frame(height: 5)
                                Rectangle()
                                    .fill(Color.accentColor)
                                    .frame(width: geo.size.width * CGFloat(item.watchedPercentage) / 100, height: 5)
                            }
                        }
                        .frame(height: 5)
                    }
                }

                // Top badge for high score
                if item.score > 0.8 {
                    VStack {
                        HStack {
                            Spacer()
                            Text("TOP PICK")
                                .font(.caption2)
                                .fontWeight(.bold)
                                .foregroundColor(.black)
                                .padding(.horizontal, 8)
                                .padding(.vertical, 4)
                                .background(Color.yellow)
                                .cornerRadius(4)
                                .padding(10)
                        }
                        Spacer()
                    }
                }
            }
            .frame(width: 200, height: 120)
            .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))

            // Info
            VStack(alignment: .leading, spacing: 4) {
                Text(item.channelName)
                    .font(.headline)
                    .lineLimit(1)

                Text(item.category)
                    .font(.subheadline)
                    .foregroundColor(.secondary)
                    .lineLimit(1)

                Text(item.reason)
                    .font(.caption)
                    .foregroundColor(.accentColor)
                    .lineLimit(1)

                // Match score indicator
                HStack(spacing: 4) {
                    ForEach(0..<5) { index in
                        Circle()
                            .fill(index < Int(item.score * 5) ? Color.accentColor : Color.gray.opacity(0.3))
                            .frame(width: 6, height: 6)
                    }
                    Text("\(Int(item.score * 100))% Match")
                        .font(.caption2)
                        .foregroundColor(.secondary)
                }
            }
            .padding(14)
        }
        .frame(width: 200)
        .background(Color(NSColor.controlBackgroundColor))
        .cornerRadius(12)
        .shadow(color: Color.black.opacity(isHovered ? 0.2 : 0.1), radius: isHovered ? 8 : 4, x: 0, y: isHovered ? 4 : 2)
        .scaleEffect(isHovered ? 1.02 : 1.0)
        .animation(.easeInOut(duration: 0.2), value: isHovered)
        .onHover { hovering in
            isHovered = hovering
        }
    }

    private func getCategoryColor(_ category: String) -> Color {
        let hash = abs(category.hashValue)
        let colors: [Color] = [.blue, .green, .red, .purple, .orange, .cyan, .indigo, .pink]
        return colors[hash % colors.count]
    }
}

// MARK: - Preview
#Preview {
    RecommendationsView(
        sections: [
            RecommendationSection(
                title: "Continue Watching",
                subtitle: "Pick up where you left off",
                type: .continueWatching,
                items: [
                    RecommendationItem(
                        channelId: UUID(),
                        channelName: "Sports Channel HD",
                        logoUrl: nil,
                        category: "Sports",
                        streamUrl: "",
                        score: 0.92,
                        reason: "75% watched",
                        type: .continueWatching,
                        watchedPercentage: 75
                    ),
                    RecommendationItem(
                        channelId: UUID(),
                        channelName: "Movie Central",
                        logoUrl: nil,
                        category: "Movies",
                        streamUrl: "",
                        score: 0.85,
                        reason: "45% watched",
                        type: .continueWatching,
                        watchedPercentage: 45
                    )
                ]
            ),
            RecommendationSection(
                title: "Top Picks For You",
                subtitle: "Based on your viewing history",
                type: .topPicksForYou,
                items: [
                    RecommendationItem(
                        channelId: UUID(),
                        channelName: "Documentary Plus",
                        logoUrl: nil,
                        category: "Documentary",
                        streamUrl: "",
                        score: 0.88,
                        reason: "You love documentaries",
                        type: .topPicksForYou
                    )
                ]
            )
        ],
        userStats: UserStats(
            totalWatchTimeMinutes: 2580,
            totalChannelsWatched: 87,
            favoriteCategory: "Movies",
            watchSessionCount: 245,
            averageSessionMinutes: 42.5
        ),
        isLoading: false,
        onChannelSelect: { _ in }
    )
    .frame(width: 1000, height: 700)
}
