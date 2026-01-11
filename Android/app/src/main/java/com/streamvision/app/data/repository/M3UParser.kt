package com.streamvision.app.data.repository

import com.streamvision.app.data.models.Channel
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.net.URL
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class M3UParser @Inject constructor() {

    suspend fun parseFromUrl(url: String, sourceId: String): List<Channel> = withContext(Dispatchers.IO) {
        val content = URL(url).readText()
        parseContent(content, sourceId)
    }

    fun parseContent(content: String, sourceId: String): List<Channel> {
        val channels = mutableListOf<Channel>()
        val lines = content.lines()

        var currentChannel: Channel? = null
        var order = 0

        for (line in lines) {
            val trimmedLine = line.trim()

            when {
                trimmedLine.startsWith("#EXTM3U") -> continue

                trimmedLine.startsWith("#EXTINF:") -> {
                    currentChannel = parseExtInf(trimmedLine, sourceId, order++)
                }

                !trimmedLine.startsWith("#") && trimmedLine.isNotEmpty() -> {
                    currentChannel?.let { channel ->
                        channels.add(channel.copy(streamUrl = trimmedLine))
                        currentChannel = null
                    }
                }
            }
        }

        return channels
    }

    private fun parseExtInf(line: String, sourceId: String, order: Int): Channel {
        var name = ""
        var logoUrl: String? = null
        var groupTitle = "Uncategorized"
        var epgId: String? = null
        var catchupDays = 0

        // Parse title (after the comma)
        val commaIndex = line.lastIndexOf(',')
        if (commaIndex != -1) {
            name = line.substring(commaIndex + 1).trim()
        }

        // Parse attributes
        val attributePattern = """([\w-]+)="([^"]*)"""".toRegex()
        attributePattern.findAll(line).forEach { match ->
            val (key, value) = match.destructured
            when (key.lowercase()) {
                "tvg-id" -> epgId = value
                "tvg-logo" -> logoUrl = value
                "group-title" -> if (value.isNotEmpty()) groupTitle = value
                "tvg-name" -> if (name.isEmpty()) name = value
                "catchup-days" -> catchupDays = value.toIntOrNull() ?: 0
            }
        }

        return Channel(
            sourceId = sourceId,
            name = name,
            logoUrl = logoUrl,
            streamUrl = "",
            groupTitle = groupTitle,
            epgId = epgId,
            catchupDays = catchupDays,
            order = order
        )
    }
}
