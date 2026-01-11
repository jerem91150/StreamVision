import Foundation

// MARK: - Watch History Entry
struct WatchHistoryEntry: Codable, Identifiable {
    var id: UUID = UUID()
    var channelId: UUID
    var channelName: String
    var category: String
    var startTime: Date
    var endTime: Date?
    var durationSeconds: Int
    var completionPercentage: Double
    var dayOfWeek: Int
    var hourOfDay: Int
}

// MARK: - User Preferences Profile
struct UserPreferences: Codable {
    var userId: String = "default"
    var categoryAffinities: [String: Double] = [:]
    var timeSlotPreferences: [Int: [String]] = [:] // Hour slot -> Categories
    var favoriteChannelIds: [UUID] = []
    var dislikedChannelIds: [UUID] = []
    var lastUpdated: Date = Date()
}

// MARK: - Recommendation Item
struct RecommendationItem: Identifiable {
    var id: UUID { channelId }
    var channelId: UUID
    var channelName: String
    var logoUrl: String?
    var category: String
    var streamUrl: String
    var score: Double
    var reason: String
    var type: RecommendationType
    var watchedPercentage: Int = 0
    var lastWatched: Date?
}

// MARK: - Recommendation Type
enum RecommendationType: String, CaseIterable {
    case continueWatching = "Continue Watching"
    case becauseYouWatched = "Because You Watched"
    case topPicksForYou = "Top Picks For You"
    case trendingNow = "Trending Now"
    case newReleases = "New Releases"
    case categoryRecommendation = "Category"
    case hiddenGems = "Hidden Gems"
    case similarContent = "Similar Content"
    case timeBasedPicks = "Perfect For Now"
}

// MARK: - Recommendation Section
struct RecommendationSection: Identifiable {
    var id: String { title }
    var title: String
    var subtitle: String
    var type: RecommendationType
    var items: [RecommendationItem]
}

// MARK: - Category Statistics
struct CategoryStats {
    var category: String
    var totalWatchTimeMinutes: Int
    var watchCount: Int
    var averageSessionMinutes: Double
    var affinityScore: Double
    var hourlyDistribution: [Int: Int] = [:]
}

// MARK: - User Statistics
struct UserStats {
    var totalWatchTimeMinutes: Int
    var totalChannelsWatched: Int
    var favoriteCategory: String
    var watchSessionCount: Int
    var averageSessionMinutes: Double
}
