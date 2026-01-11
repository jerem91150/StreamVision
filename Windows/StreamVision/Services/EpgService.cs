using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using StreamVision.Models;

namespace StreamVision.Services
{
    public class EpgService
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<EpgProgram>> _epgCache = new();

        public EpgService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task<Dictionary<string, List<EpgProgram>>> LoadEpgFromUrlAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                Stream stream;
                if (url.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    var compressedStream = await response.Content.ReadAsStreamAsync();
                    stream = new GZipStream(compressedStream, CompressionMode.Decompress);
                }
                else
                {
                    stream = await response.Content.ReadAsStreamAsync();
                }

                return await ParseXmlTvAsync(stream);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load EPG: {ex.Message}", ex);
            }
        }

        public async Task<Dictionary<string, List<EpgProgram>>> LoadEpgFromFileAsync(string filePath)
        {
            try
            {
                Stream stream;
                if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    var fileStream = File.OpenRead(filePath);
                    stream = new GZipStream(fileStream, CompressionMode.Decompress);
                }
                else
                {
                    stream = File.OpenRead(filePath);
                }

                return await ParseXmlTvAsync(stream);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load EPG file: {ex.Message}", ex);
            }
        }

        private async Task<Dictionary<string, List<EpgProgram>>> ParseXmlTvAsync(Stream stream)
        {
            var epgData = new Dictionary<string, List<EpgProgram>>(StringComparer.OrdinalIgnoreCase);

            using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });

            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "programme")
                {
                    var program = ParseProgramme(reader);
                    if (program != null)
                    {
                        if (!epgData.ContainsKey(program.ChannelId))
                        {
                            epgData[program.ChannelId] = new List<EpgProgram>();
                        }
                        epgData[program.ChannelId].Add(program);
                    }
                }
            }

            _epgCache.Clear();
            foreach (var kvp in epgData)
            {
                _epgCache[kvp.Key] = kvp.Value;
            }

            return epgData;
        }

        private EpgProgram? ParseProgramme(XmlReader reader)
        {
            try
            {
                var channelId = reader.GetAttribute("channel");
                var startStr = reader.GetAttribute("start");
                var endStr = reader.GetAttribute("stop");

                if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(startStr) || string.IsNullOrEmpty(endStr))
                    return null;

                var program = new EpgProgram
                {
                    ChannelId = channelId,
                    StartTime = ParseXmlTvDateTime(startStr),
                    EndTime = ParseXmlTvDateTime(endStr)
                };

                var subtree = reader.ReadSubtree();
                while (subtree.Read())
                {
                    if (subtree.NodeType == XmlNodeType.Element)
                    {
                        switch (subtree.Name)
                        {
                            case "title":
                                program.Title = subtree.ReadElementContentAsString();
                                break;
                            case "desc":
                                program.Description = subtree.ReadElementContentAsString();
                                break;
                            case "category":
                                program.Category = subtree.ReadElementContentAsString();
                                break;
                            case "icon":
                                program.IconUrl = subtree.GetAttribute("src");
                                break;
                        }
                    }
                }

                return program;
            }
            catch
            {
                return null;
            }
        }

        private DateTime ParseXmlTvDateTime(string dateTimeStr)
        {
            // Format: 20231215143000 +0100
            if (dateTimeStr.Length >= 14)
            {
                var year = int.Parse(dateTimeStr.Substring(0, 4));
                var month = int.Parse(dateTimeStr.Substring(4, 2));
                var day = int.Parse(dateTimeStr.Substring(6, 2));
                var hour = int.Parse(dateTimeStr.Substring(8, 2));
                var minute = int.Parse(dateTimeStr.Substring(10, 2));
                var second = int.Parse(dateTimeStr.Substring(12, 2));

                var dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

                // Handle timezone offset
                if (dateTimeStr.Length > 14)
                {
                    var offsetStr = dateTimeStr.Substring(14).Trim();
                    if (offsetStr.StartsWith("+") || offsetStr.StartsWith("-"))
                    {
                        var sign = offsetStr[0] == '+' ? 1 : -1;
                        offsetStr = offsetStr.Substring(1);
                        if (int.TryParse(offsetStr.Substring(0, 2), out var hours))
                        {
                            var minutes = offsetStr.Length >= 4 ? int.Parse(offsetStr.Substring(2, 2)) : 0;
                            var offset = new TimeSpan(hours, minutes, 0);
                            dt = dt.AddHours(-sign * offset.TotalHours);
                        }
                    }
                }

                return dt.ToLocalTime();
            }

            return DateTime.Now;
        }

        public List<EpgProgram> GetProgramsForChannel(string channelId, DateTime date)
        {
            var programs = new List<EpgProgram>();

            if (_epgCache.TryGetValue(channelId, out var allPrograms))
            {
                foreach (var program in allPrograms)
                {
                    if (program.StartTime.Date == date.Date || program.EndTime.Date == date.Date)
                    {
                        programs.Add(program);
                    }
                }
            }

            programs.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            return programs;
        }

        public EpgProgram? GetCurrentProgram(string channelId)
        {
            if (_epgCache.TryGetValue(channelId, out var programs))
            {
                var now = DateTime.Now;
                foreach (var program in programs)
                {
                    if (now >= program.StartTime && now <= program.EndTime)
                    {
                        return program;
                    }
                }
            }
            return null;
        }

        public EpgProgram? GetNextProgram(string channelId)
        {
            if (_epgCache.TryGetValue(channelId, out var programs))
            {
                var now = DateTime.Now;
                EpgProgram? next = null;
                foreach (var program in programs)
                {
                    if (program.StartTime > now)
                    {
                        if (next == null || program.StartTime < next.StartTime)
                        {
                            next = program;
                        }
                    }
                }
                return next;
            }
            return null;
        }
    }
}
