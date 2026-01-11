import Foundation

actor M3UParser {

    func parseFromURL(_ url: URL, sourceId: UUID) async throws -> [Channel] {
        let (data, _) = try await URLSession.shared.data(from: url)
        guard let content = String(data: data, encoding: .utf8) else {
            throw M3UError.invalidEncoding
        }
        return parseContent(content, sourceId: sourceId)
    }

    func parseFromFile(_ fileURL: URL, sourceId: UUID) throws -> [Channel] {
        let content = try String(contentsOf: fileURL, encoding: .utf8)
        return parseContent(content, sourceId: sourceId)
    }

    private func parseContent(_ content: String, sourceId: UUID) -> [Channel] {
        var channels: [Channel] = []
        let lines = content.components(separatedBy: .newlines)

        var currentChannel: Channel?
        var order = 0

        for line in lines {
            let trimmedLine = line.trimmingCharacters(in: .whitespaces)

            if trimmedLine.hasPrefix("#EXTM3U") {
                continue
            }

            if trimmedLine.hasPrefix("#EXTINF:") {
                currentChannel = parseExtInf(trimmedLine, sourceId: sourceId, order: order)
                order += 1
            } else if !trimmedLine.hasPrefix("#") && !trimmedLine.isEmpty {
                if var channel = currentChannel {
                    channel.streamUrl = trimmedLine
                    channels.append(channel)
                    currentChannel = nil
                }
            }
        }

        return channels
    }

    private func parseExtInf(_ line: String, sourceId: UUID, order: Int) -> Channel {
        var channel = Channel(
            sourceId: sourceId,
            name: "",
            streamUrl: "",
            order: order
        )

        if let commaIndex = line.lastIndex(of: ",") {
            let titleStart = line.index(after: commaIndex)
            channel.name = String(line[titleStart...]).trimmingCharacters(in: .whitespaces)
        }

        let attributes = parseAttributes(line)

        if let tvgId = attributes["tvg-id"] {
            channel.epgId = tvgId
        }

        if let logo = attributes["tvg-logo"] {
            channel.logoUrl = logo
        }

        if let group = attributes["group-title"], !group.isEmpty {
            channel.groupTitle = group
        }

        if let tvgName = attributes["tvg-name"], channel.name.isEmpty {
            channel.name = tvgName
        }

        if let catchupDaysStr = attributes["catchup-days"],
           let catchupDays = Int(catchupDaysStr) {
            channel.catchupDays = catchupDays
        }

        return channel
    }

    private func parseAttributes(_ line: String) -> [String: String] {
        var attributes: [String: String] = [:]

        let pattern = #"([\w-]+)="([^"]*)""#
        guard let regex = try? NSRegularExpression(pattern: pattern, options: []) else {
            return attributes
        }

        let range = NSRange(line.startIndex..., in: line)
        let matches = regex.matches(in: line, options: [], range: range)

        for match in matches {
            if let keyRange = Range(match.range(at: 1), in: line),
               let valueRange = Range(match.range(at: 2), in: line) {
                let key = String(line[keyRange]).lowercased()
                let value = String(line[valueRange])
                attributes[key] = value
            }
        }

        return attributes
    }
}

enum M3UError: Error {
    case invalidEncoding
    case invalidFormat
    case networkError(Error)
}
