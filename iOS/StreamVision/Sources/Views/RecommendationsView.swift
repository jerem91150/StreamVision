import SwiftUI

// MARK: - Recommendations View
struct RecommendationsView: View {
    let sections: [RecommendationSection]
    let userStats: UserStats?
    let isLoading: Bool
    let onChannelSelect: (RecommendationItem) -> Void

    var body: some View {
        Group {
            if isLoading {
                ProgressView("Loading recommendations...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if sections.isEmpty {
                EmptyRecommendationsView()
            } else {
                ScrollView {
                    LazyVStack(spacing: 24) {
                        // User Stats Header
                        if let stats = userStats {
                            UserStatsCard(stats: stats)
                                .padding(.horizontal)
                        }

                        // Recommendation Sections
                        ForEach(sections) { section in
                            RecommendationSectionRow(
                                section: section,
                                onItemSelect: onChannelSelect
                            )
                        }
                    }
                    .padding(.vertical)
                }
            }
        }
    }
}

// MARK: - Empty State
struct EmptyRecommendationsView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "sparkles.rectangle.stack")
                .font(.system(size: 60))
                .foregroundColor(.secondary)

            Text("Start Watching to Get Recommendations")
                .font(.headline)
                .foregroundColor(.secondary)

            Text("We'll learn your preferences as you watch")
                .font(.subheadline)
                .foregroundColor(.secondary.opacity(0.7))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

// MARK: - User Stats Card
struct UserStatsCard: View {
    let stats: UserStats

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Your Viewing Stats")
                .font(.headline)

            HStack(spacing: 24) {
                StatItem(
                    icon: "timer",
                    value: formatWatchTime(stats.totalWatchTimeMinutes),
                    label: "Watch Time"
                )

                Divider()
                    .frame(height: 40)

                StatItem(
                    icon: "tv",
                    value: "\(stats.totalChannelsWatched)",
                    label: "Channels"
                )

                Divider()
                    .frame(height: 40)

                StatItem(
                    icon: "heart.fill",
                    value: stats.favoriteCategory,
                    label: "Top Category"
                )
            }
        }
        .padding()
        .background(Color.accentColor.opacity(0.1))
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
        VStack(spacing: 4) {
            Image(systemName: icon)
                .foregroundColor(.accentColor)
            Text(value)
                .font(.headline)
            Text(label)
                .font(.caption)
                .foregroundColor(.secondary)
        }
    }
}

// MARK: - Recommendation Section Row
struct RecommendationSectionRow: View {
    let section: RecommendationSection
    let onItemSelect: (RecommendationItem) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            // Section Header
            HStack(spacing: 8) {
                Image(systemName: getSectionIcon(section.type))
                    .foregroundColor(getSectionColor(section.type))
                    .font(.title3)

                VStack(alignment: .leading, spacing: 2) {
                    Text(section.title)
                        .font(.headline)
                    Text(section.subtitle)
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
            .padding(.horizontal)

            // Horizontal scroll of items
            ScrollView(.horizontal, showsIndicators: false) {
                LazyHStack(spacing: 12) {
                    ForEach(section.items) { item in
                        RecommendationCard(item: item)
                            .onTapGesture {
                                onItemSelect(item)
                            }
                    }
                }
                .padding(.horizontal)
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

// MARK: - Recommendation Card
struct RecommendationCard: View {
    let item: RecommendationItem

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
                                .frame(width: 60, height: 60)
                                .clipShape(Circle())
                        default:
                            Image(systemName: "tv")
                                .font(.system(size: 40))
                                .foregroundColor(.white)
                        }
                    }
                } else {
                    Image(systemName: "tv")
                        .font(.system(size: 40))
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
                                    .frame(height: 4)
                                Rectangle()
                                    .fill(Color.accentColor)
                                    .frame(width: geo.size.width * CGFloat(item.watchedPercentage) / 100, height: 4)
                            }
                        }
                        .frame(height: 4)
                    }
                }

                // Top badge for high score
                if item.score > 0.8 {
                    VStack {
                        HStack {
                            Spacer()
                            Text("TOP")
                                .font(.caption2)
                                .fontWeight(.bold)
                                .foregroundColor(.black)
                                .padding(.horizontal, 6)
                                .padding(.vertical, 2)
                                .background(Color.yellow)
                                .cornerRadius(4)
                                .padding(8)
                        }
                        Spacer()
                    }
                }
            }
            .frame(width: 160, height: 100)
            .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))

            // Info
            VStack(alignment: .leading, spacing: 4) {
                Text(item.channelName)
                    .font(.subheadline)
                    .fontWeight(.semibold)
                    .lineLimit(1)

                Text(item.category)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(1)

                Text(item.reason)
                    .font(.caption2)
                    .foregroundColor(.accentColor)
                    .lineLimit(1)
            }
            .padding(12)
        }
        .frame(width: 160)
        .background(Color(.systemBackground))
        .cornerRadius(12)
        .shadow(color: Color.black.opacity(0.1), radius: 4, x: 0, y: 2)
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
                        channelName: "Sports Channel",
                        logoUrl: nil,
                        category: "Sports",
                        streamUrl: "",
                        score: 0.9,
                        reason: "75% watched",
                        type: .continueWatching,
                        watchedPercentage: 75
                    )
                ]
            )
        ],
        userStats: UserStats(
            totalWatchTimeMinutes: 1250,
            totalChannelsWatched: 45,
            favoriteCategory: "Movies",
            watchSessionCount: 120,
            averageSessionMinutes: 35.5
        ),
        isLoading: false,
        onChannelSelect: { _ in }
    )
}
