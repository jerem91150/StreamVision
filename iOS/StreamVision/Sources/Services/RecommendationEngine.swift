import Foundation

actor RecommendationEngine {

    // MARK: - Properties
    private var userPreferences = UserPreferences()
    private var watchHistory: [WatchHistoryEntry] = []
    private var categoryStats: [String: CategoryStats] = [:]

    // Algorithm weights
    private let categoryAffinityWeight = 0.35
    private let timeRelevanceWeight = 0.20
    private let popularityWeight = 0.15
    private let freshnessWeight = 0.10
    private let similarityWeight = 0.20

    // Settings
    private let affinityDecayDays = 7
    private let minWatchSeconds = 10

    private let fileManager = FileManager.default
    private let documentsDir: URL

    // MARK: - Initialization
    init() {
        documentsDir = fileManager.urls(for: .documentDirectory, in: .userDomainMask).first!
    }

    // MARK: - Persistence
    func initialize() async {
        await loadUserData()
        calculateCategoryStats()
    }

    private func loadUserData() async {
        // Load watch history
        let historyUrl = documentsDir.appendingPathComponent("watch_history.json")
        if let data = try? Data(contentsOf: historyUrl) {
            watchHistory = (try? JSONDecoder().decode([WatchHistoryEntry].self, from: data)) ?? []
        }

        // Load preferences
        let prefsUrl = documentsDir.appendingPathComponent("user_preferences.json")
        if let data = try? Data(contentsOf: prefsUrl) {
            userPreferences = (try? JSONDecoder().decode(UserPreferences.self, from: data)) ?? UserPreferences()
        }
    }

    private func saveUserData() async {
        // Save watch history
        let historyUrl = documentsDir.appendingPathComponent("watch_history.json")
        if let data = try? JSONEncoder().encode(watchHistory) {
            try? data.write(to: historyUrl)
        }

        // Save preferences
        let prefsUrl = documentsDir.appendingPathComponent("user_preferences.json")
        if let data = try? JSONEncoder().encode(userPreferences) {
            try? data.write(to: prefsUrl)
        }
    }

    // MARK: - Watch Tracking
    func startWatching(channel: Channel) {
        let entry = WatchHistoryEntry(
            channelId: channel.id,
            channelName: channel.name,
            category: channel.groupTitle,
            startTime: Date(),
            durationSeconds: 0,
            completionPercentage: 0,
            dayOfWeek: Calendar.current.component(.weekday, from: Date()),
            hourOfDay: Calendar.current.component(.hour, from: Date())
        )
        watchHistory.append(entry)
    }

    func stopWatching(channel: Channel, durationSeconds: Int, completionPercentage: Double) async {
        if let index = watchHistory.lastIndex(where: { $0.channelId == channel.id && $0.endTime == nil }) {
            watchHistory[index].endTime = Date()
            watchHistory[index].durationSeconds = durationSeconds
            watchHistory[index].completionPercentage = completionPercentage

            if durationSeconds >= minWatchSeconds {
                updateAffinities(entry: watchHistory[index])
                await saveUserData()
            }
        }
    }

    private func updateAffinities(entry: WatchHistoryEntry) {
        // Update category affinity
        let currentAffinity = userPreferences.categoryAffinities[entry.category] ?? 0
        let watchScore = min(Double(entry.durationSeconds) / 1800.0, 1.0) // Max 30 min for full score
        let completionBonus = entry.completionPercentage > 0.8 ? 0.5 : 0
        userPreferences.categoryAffinities[entry.category] = currentAffinity + watchScore + completionBonus

        // Update time slot preferences
        let hourSlot = entry.hourOfDay / 4
        var slotCategories = userPreferences.timeSlotPreferences[hourSlot] ?? []
        if !slotCategories.contains(entry.category) {
            slotCategories.append(entry.category)
            userPreferences.timeSlotPreferences[hourSlot] = slotCategories
        }

        userPreferences.lastUpdated = Date()
        calculateCategoryStats()
    }

    private func calculateCategoryStats() {
        categoryStats.removeAll()

        let thirtyDaysAgo = Calendar.current.date(byAdding: .day, value: -30, to: Date())!
        let recentHistory = watchHistory.filter { $0.startTime > thirtyDaysAgo && $0.durationSeconds >= minWatchSeconds }

        let grouped = Dictionary(grouping: recentHistory) { $0.category }

        for (category, entries) in grouped {
            var stats = CategoryStats(
                category: category,
                totalWatchTimeMinutes: entries.reduce(0) { $0 + $1.durationSeconds } / 60,
                watchCount: entries.count,
                averageSessionMinutes: Double(entries.reduce(0) { $0 + $1.durationSeconds }) / Double(entries.count) / 60.0,
                affinityScore: 0
            )

            // Hourly distribution
            for entry in entries {
                stats.hourlyDistribution[entry.hourOfDay, default: 0] += 1
            }

            // Decayed affinity
            var decayedScore = 0.0
            for entry in entries {
                let daysSinceWatch = Calendar.current.dateComponents([.day], from: entry.startTime, to: Date()).day ?? 0
                let decayFactor = exp(-Double(daysSinceWatch) / Double(affinityDecayDays))
                decayedScore += (Double(entry.durationSeconds) / 60.0) * decayFactor
            }
            stats.affinityScore = decayedScore

            categoryStats[category] = stats
        }
    }

    // MARK: - Get Recommendations
    func getRecommendations(allChannels: [Channel]) async -> [RecommendationSection] {
        var sections: [RecommendationSection] = []

        // 1. Continue Watching
        let continueWatching = getContinueWatching(allChannels: allChannels)
        if !continueWatching.items.isEmpty {
            sections.append(continueWatching)
        }

        // 2. Top Picks For You
        let topPicks = getTopPicks(allChannels: allChannels)
        if !topPicks.items.isEmpty {
            sections.append(topPicks)
        }

        // 3. Because You Watched
        let becauseYouWatched = getBecauseYouWatched(allChannels: allChannels)
        if !becauseYouWatched.items.isEmpty {
            sections.append(becauseYouWatched)
        }

        // 4. Top Category Recommendations
        let topCategories = categoryStats.values
            .sorted { $0.affinityScore > $1.affinityScore }
            .prefix(3)

        for stats in topCategories {
            let categorySection = getCategoryRecommendations(allChannels: allChannels, category: stats.category)
            if !categorySection.items.isEmpty {
                sections.append(categorySection)
            }
        }

        // 5. Trending
        let trending = getTrending(allChannels: allChannels)
        if !trending.items.isEmpty {
            sections.append(trending)
        }

        // 6. Hidden Gems
        let hiddenGems = getHiddenGems(allChannels: allChannels)
        if !hiddenGems.items.isEmpty {
            sections.append(hiddenGems)
        }

        // 7. Time-based
        let timeBased = getTimeBasedRecommendations(allChannels: allChannels)
        if !timeBased.items.isEmpty {
            sections.append(timeBased)
        }

        return sections
    }

    private func getContinueWatching(allChannels: [Channel]) -> RecommendationSection {
        let incompleteWatches = watchHistory
            .filter { $0.completionPercentage > 0.1 && $0.completionPercentage < 0.9 }
            .sorted { $0.startTime > $1.startTime }

        var seenIds = Set<UUID>()
        var items: [RecommendationItem] = []

        for watch in incompleteWatches {
            guard !seenIds.contains(watch.channelId),
                  let channel = allChannels.first(where: { $0.id == watch.channelId }) else { continue }

            seenIds.insert(watch.channelId)
            items.append(RecommendationItem(
                channelId: channel.id,
                channelName: channel.name,
                logoUrl: channel.logoUrl,
                category: channel.groupTitle,
                streamUrl: channel.streamUrl,
                score: 1.0,
                reason: "\(Int(watch.completionPercentage * 100))% watched",
                type: .continueWatching,
                watchedPercentage: Int(watch.completionPercentage * 100),
                lastWatched: watch.startTime
            ))

            if items.count >= 10 { break }
        }

        return RecommendationSection(
            title: "Continue Watching",
            subtitle: "Pick up where you left off",
            type: .continueWatching,
            items: items
        )
    }

    private func getTopPicks(allChannels: [Channel]) -> RecommendationSection {
        let scored = allChannels
            .map { (channel: $0, score: calculateChannelScore(channel: $0)) }
            .filter { $0.score > 0 }
            .sorted { $0.score > $1.score }
            .prefix(15)

        let items = scored.map { item in
            RecommendationItem(
                channelId: item.channel.id,
                channelName: item.channel.name,
                logoUrl: item.channel.logoUrl,
                category: item.channel.groupTitle,
                streamUrl: item.channel.streamUrl,
                score: item.score,
                reason: getScoreReasons(channel: item.channel),
                type: .topPicksForYou
            )
        }

        return RecommendationSection(
            title: "Top Picks For You",
            subtitle: "Based on your viewing history",
            type: .topPicksForYou,
            items: Array(items)
        )
    }

    private func getBecauseYouWatched(allChannels: [Channel]) -> RecommendationSection {
        let lastWatched = watchHistory
            .filter { $0.durationSeconds >= 60 }
            .sorted { $0.startTime > $1.startTime }
            .first

        var items: [RecommendationItem] = []

        if let lastWatched = lastWatched {
            let similarChannels = allChannels
                .filter { $0.groupTitle == lastWatched.category && $0.id != lastWatched.channelId }
                .shuffled()
                .prefix(10)

            items = similarChannels.map { channel in
                RecommendationItem(
                    channelId: channel.id,
                    channelName: channel.name,
                    logoUrl: channel.logoUrl,
                    category: channel.groupTitle,
                    streamUrl: channel.streamUrl,
                    score: 0.8,
                    reason: "Similar to \(lastWatched.channelName)",
                    type: .becauseYouWatched
                )
            }
        }

        return RecommendationSection(
            title: lastWatched != nil ? "Because You Watched \(lastWatched!.channelName)" : "Recommended For You",
            subtitle: "Similar content you might enjoy",
            type: .becauseYouWatched,
            items: items
        )
    }

    private func getCategoryRecommendations(allChannels: [Channel], category: String) -> RecommendationSection {
        let watchedIds = Set(watchHistory.filter { $0.category == category }.map { $0.channelId })

        let unwatched = allChannels
            .filter { $0.groupTitle == category && !watchedIds.contains($0.id) }
            .shuffled()
            .prefix(10)

        let items = unwatched.map { channel in
            RecommendationItem(
                channelId: channel.id,
                channelName: channel.name,
                logoUrl: channel.logoUrl,
                category: channel.groupTitle,
                streamUrl: channel.streamUrl,
                score: categoryStats[category]?.affinityScore ?? 0.5 / 100,
                reason: "New in your favorite category",
                type: .categoryRecommendation
            )
        }

        return RecommendationSection(
            title: "\(category) For You",
            subtitle: "Because you love \(category)",
            type: .categoryRecommendation,
            items: Array(items)
        )
    }

    private func getTrending(allChannels: [Channel]) -> RecommendationSection {
        let sevenDaysAgo = Calendar.current.date(byAdding: .day, value: -7, to: Date())!
        let recentWatches = Dictionary(
            grouping: watchHistory.filter { $0.startTime > sevenDaysAgo },
            by: { $0.channelId }
        ).mapValues { $0.count }

        let trending = allChannels
            .map { (channel: $0, count: recentWatches[$0.id] ?? 0) }
            .sorted { $0.count > $1.count }
            .prefix(10)

        let items = trending.map { item in
            RecommendationItem(
                channelId: item.channel.id,
                channelName: item.channel.name,
                logoUrl: item.channel.logoUrl,
                category: item.channel.groupTitle,
                streamUrl: item.channel.streamUrl,
                score: item.count > 0 ? min(Double(item.count) / 10.0, 1.0) : 0.3,
                reason: item.count > 0 ? "Watched \(item.count) times recently" : "Popular content",
                type: .trendingNow
            )
        }

        return RecommendationSection(
            title: "Trending Now",
            subtitle: "Popular content",
            type: .trendingNow,
            items: Array(items)
        )
    }

    private func getHiddenGems(allChannels: [Channel]) -> RecommendationSection {
        let watchedIds = Set(watchHistory.map { $0.channelId })
        let favoriteCategories = Set(
            categoryStats.values
                .sorted { $0.affinityScore > $1.affinityScore }
                .prefix(5)
                .map { $0.category }
        )

        let hiddenGems = allChannels
            .filter { favoriteCategories.contains($0.groupTitle) && !watchedIds.contains($0.id) }
            .shuffled()
            .prefix(10)

        let items = hiddenGems.map { channel in
            RecommendationItem(
                channelId: channel.id,
                channelName: channel.name,
                logoUrl: channel.logoUrl,
                category: channel.groupTitle,
                streamUrl: channel.streamUrl,
                score: 0.6,
                reason: "Undiscovered content you might like",
                type: .hiddenGems
            )
        }

        return RecommendationSection(
            title: "Hidden Gems",
            subtitle: "Discover something new",
            type: .hiddenGems,
            items: Array(items)
        )
    }

    private func getTimeBasedRecommendations(allChannels: [Channel]) -> RecommendationSection {
        let currentHour = Calendar.current.component(.hour, from: Date())
        let hourSlot = currentHour / 4

        let timeOfDay: String = {
            switch currentHour {
            case 5..<12: return "Morning"
            case 12..<17: return "Afternoon"
            case 17..<21: return "Evening"
            default: return "Night"
            }
        }()

        var items: [RecommendationItem] = []

        if let preferredCategories = userPreferences.timeSlotPreferences[hourSlot] {
            let recommendations = allChannels
                .filter { preferredCategories.contains($0.groupTitle) }
                .shuffled()
                .prefix(10)

            items = recommendations.map { channel in
                RecommendationItem(
                    channelId: channel.id,
                    channelName: channel.name,
                    logoUrl: channel.logoUrl,
                    category: channel.groupTitle,
                    streamUrl: channel.streamUrl,
                    score: 0.7,
                    reason: "You usually watch \(channel.groupTitle) around this time",
                    type: .timeBasedPicks
                )
            }
        }

        return RecommendationSection(
            title: "Perfect For \(timeOfDay)",
            subtitle: "Based on when you usually watch",
            type: .timeBasedPicks,
            items: items
        )
    }

    private func calculateChannelScore(channel: Channel) -> Double {
        var score = 0.0

        // 1. Category Affinity
        if let stats = categoryStats[channel.groupTitle],
           let maxAffinity = categoryStats.values.map({ $0.affinityScore }).max(),
           maxAffinity > 0 {
            score += (stats.affinityScore / maxAffinity) * categoryAffinityWeight
        }

        // 2. Time Relevance
        let currentHourSlot = Calendar.current.component(.hour, from: Date()) / 4
        if let preferredCategories = userPreferences.timeSlotPreferences[currentHourSlot],
           preferredCategories.contains(channel.groupTitle) {
            score += timeRelevanceWeight
        }

        // 3. Popularity
        let watchCount = watchHistory.filter { $0.channelId == channel.id }.count
        if watchCount > 0 {
            score += min(Double(watchCount) / 10.0, 1.0) * popularityWeight
        }

        // 4. Freshness
        let lastWatch = watchHistory
            .filter { $0.channelId == channel.id }
            .sorted { $0.startTime > $1.startTime }
            .first

        let threeDaysAgo = Calendar.current.date(byAdding: .day, value: -3, to: Date())!
        if lastWatch == nil || lastWatch!.startTime < threeDaysAgo {
            score += freshnessWeight
        }

        // 5. Similarity to favorites
        if channel.isFavorite {
            score += similarityWeight
        }

        return score
    }

    private func getScoreReasons(channel: Channel) -> String {
        var reasons: [String] = []

        if let stats = categoryStats[channel.groupTitle], stats.affinityScore > 0 {
            reasons.append("You enjoy \(channel.groupTitle)")
        }

        let currentHourSlot = Calendar.current.component(.hour, from: Date()) / 4
        if let preferredCategories = userPreferences.timeSlotPreferences[currentHourSlot],
           preferredCategories.contains(channel.groupTitle) {
            reasons.append("Great for this time")
        }

        if channel.isFavorite {
            reasons.append("In your favorites")
        }

        return reasons.isEmpty ? "Recommended for you" : reasons.joined(separator: " • ")
    }

    // MARK: - User Statistics
    func getUserStats() -> UserStats {
        let totalWatchTime = watchHistory.reduce(0) { $0 + $1.durationSeconds } / 60
        let topCategory = categoryStats.values
            .sorted { $0.affinityScore > $1.affinityScore }
            .first

        return UserStats(
            totalWatchTimeMinutes: totalWatchTime,
            totalChannelsWatched: Set(watchHistory.map { $0.channelId }).count,
            favoriteCategory: topCategory?.category ?? "None",
            watchSessionCount: watchHistory.count,
            averageSessionMinutes: watchHistory.isEmpty ? 0 : Double(watchHistory.reduce(0) { $0 + $1.durationSeconds }) / Double(watchHistory.count) / 60.0
        )
    }

    // MARK: - Clear Data
    func clearUserData() async {
        watchHistory.removeAll()
        userPreferences = UserPreferences()
        categoryStats.removeAll()
        await saveUserData()
    }
}
