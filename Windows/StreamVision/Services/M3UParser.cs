using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StreamVision.Models;

namespace StreamVision.Services
{
    public class M3UParser
    {
        private static readonly HttpClient _httpClient;

        static M3UParser()
        {
            _httpClient = new HttpClient();
            // User-Agent requis par certains serveurs IPTV
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
        private static readonly Regex _extinfoRegex = new(
            @"#EXTINF:(?<duration>-?\d+)\s*(?<attributes>.*?),\s*(?<title>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _attributeRegex = new(
            @"(?<key>[\w-]+)=""(?<value>[^""]*)""",
            RegexOptions.Compiled);

        public async Task<List<Channel>> ParseFromUrlAsync(string url, string sourceId)
        {
            try
            {
                var content = await _httpClient.GetStringAsync(url);
                return ParseContent(content, sourceId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download M3U from URL: {ex.Message}", ex);
            }
        }

        public async Task<List<Channel>> ParseFromFileAsync(string filePath, string sourceId)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                return ParseContent(content, sourceId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read M3U file: {ex.Message}", ex);
            }
        }

        private List<Channel> ParseContent(string content, string sourceId)
        {
            var channels = new List<Channel>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Channel? currentChannel = null;
            int order = 0;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("#EXTM3U"))
                    continue;

                if (trimmedLine.StartsWith("#EXTINF:"))
                {
                    currentChannel = ParseExtInf(trimmedLine, sourceId, order++);
                }
                else if (!trimmedLine.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (currentChannel != null)
                    {
                        currentChannel.StreamUrl = trimmedLine;
                        channels.Add(currentChannel);
                        currentChannel = null;
                    }
                }
            }

            return channels;
        }

        private Channel ParseExtInf(string line, string sourceId, int order)
        {
            var channel = new Channel
            {
                SourceId = sourceId,
                Order = order,
                GroupTitle = "Uncategorized"
            };

            var match = _extinfoRegex.Match(line);
            if (match.Success)
            {
                channel.Name = match.Groups["title"].Value.Trim();

                var attributesStr = match.Groups["attributes"].Value;
                var attributes = ParseAttributes(attributesStr);

                if (attributes.TryGetValue("tvg-id", out var tvgId))
                    channel.EpgId = tvgId;

                if (attributes.TryGetValue("tvg-logo", out var logo))
                    channel.LogoUrl = logo;

                if (attributes.TryGetValue("group-title", out var group))
                    channel.GroupTitle = string.IsNullOrWhiteSpace(group) ? "Uncategorized" : group;

                if (attributes.TryGetValue("tvg-name", out var name) && string.IsNullOrWhiteSpace(channel.Name))
                    channel.Name = name;

                if (attributes.TryGetValue("catchup-days", out var catchupDaysStr) &&
                    int.TryParse(catchupDaysStr, out var catchupDays))
                    channel.CatchupDays = catchupDays;
            }

            return channel;
        }

        private Dictionary<string, string> ParseAttributes(string attributesStr)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matches = _attributeRegex.Matches(attributesStr);

            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value;
                var value = match.Groups["value"].Value;
                attributes[key] = value;
            }

            return attributes;
        }
    }
}
