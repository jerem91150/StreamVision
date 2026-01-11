import Foundation

// MARK: - Source Type
enum SourceType: String, Codable, CaseIterable {
    case m3u = "M3U"
    case xtreamCodes = "Xtream Codes"
    case stalkerPortal = "Stalker Portal"
}

// MARK: - Playlist Source
struct PlaylistSource: Identifiable, Codable, Hashable {
    var id: UUID = UUID()
    var name: String
    var type: SourceType
    var url: String
    var username: String?
    var password: String?
    var macAddress: String?
    var epgUrl: String?
    var lastSync: Date = Date()
    var isActive: Bool = true
    var channelCount: Int = 0
}

// MARK: - Channel
struct Channel: Identifiable, Codable, Hashable {
    var id: UUID = UUID()
    var sourceId: UUID
    var name: String
    var logoUrl: String?
    var streamUrl: String
    var groupTitle: String = "Uncategorized"
    var epgId: String?
    var isFavorite: Bool = false
    var catchupDays: Int = 0
    var order: Int = 0

    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }

    static func == (lhs: Channel, rhs: Channel) -> Bool {
        lhs.id == rhs.id
    }
}

// MARK: - Channel Group
struct ChannelGroup: Identifiable {
    var id: String { name }
    var name: String
    var channels: [Channel]
    var isExpanded: Bool = true

    var channelCount: Int { channels.count }
}

// MARK: - EPG Program
struct EpgProgram: Identifiable, Codable {
    var id: UUID = UUID()
    var channelId: String
    var title: String
    var description: String?
    var startTime: Date
    var endTime: Date
    var category: String?
    var iconUrl: String?

    var isLive: Bool {
        let now = Date()
        return now >= startTime && now <= endTime
    }

    var progress: Double {
        let now = Date()
        if now < startTime { return 0 }
        if now > endTime { return 1 }
        let total = endTime.timeIntervalSince(startTime)
        let elapsed = now.timeIntervalSince(startTime)
        return elapsed / total
    }

    var timeRange: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        return "\(formatter.string(from: startTime)) - \(formatter.string(from: endTime))"
    }
}

// MARK: - Xtream Account Info
struct XtreamAccountInfo: Codable {
    var username: String
    var status: String
    var expDate: String
    var maxConnections: String
    var activeConnections: String
    var serverUrl: String
}

// MARK: - Xtream Category
struct XtreamCategory: Codable {
    var categoryId: String
    var categoryName: String
    var parentId: String
}

// MARK: - App Settings
struct AppSettings: Codable {
    var bufferSize: Int = 2000
    var autoPlayOnSelect: Bool = true
    var showEpgOnHover: Bool = true
    var theme: String = "dark"
    var lastSelectedSourceId: UUID?
    var pipEnabled: Bool = true
    var backgroundPlayback: Bool = true
}
