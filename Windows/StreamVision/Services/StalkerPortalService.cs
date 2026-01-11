using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Service for interacting with Stalker Portal / Ministra IPTV middleware
    /// Uses MAC address authentication
    /// </summary>
    public class StalkerPortalService
    {
        private readonly HttpClient _httpClient;
        private string? _token;
        private string? _serverUrl;
        private string? _macAddress;
        private const string UserAgent = "Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 (KHTML, like Gecko) MAG200 stbapp ver: 2 rev: 250 Safari/533.3";

        public StalkerPortalService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Authenticate with the Stalker Portal using MAC address
        /// </summary>
        public async Task<StalkerAccountInfo?> AuthenticateAsync(string serverUrl, string macAddress)
        {
            try
            {
                _serverUrl = serverUrl.TrimEnd('/');
                _macAddress = macAddress.ToUpperInvariant();

                // Ensure MAC address format is correct (XX:XX:XX:XX:XX:XX)
                if (!_macAddress.Contains(':'))
                {
                    // Convert from format XXXXXXXXXXXX to XX:XX:XX:XX:XX:XX
                    if (_macAddress.Length == 12)
                    {
                        _macAddress = string.Join(":", Enumerable.Range(0, 6).Select(i => _macAddress.Substring(i * 2, 2)));
                    }
                }

                SetupHeaders();

                // Step 1: Handshake
                var handshakeUrl = $"{_serverUrl}/portal.php?type=stb&action=handshake&token=&JsHttpRequest=1-xml";
                var handshakeResponse = await GetJsonAsync<StalkerResponse>(handshakeUrl);

                if (handshakeResponse?.Js?.Token == null)
                {
                    return null;
                }

                _token = handshakeResponse.Js.Token;
                SetupHeaders(); // Update headers with token

                // Step 2: Get profile
                var profileUrl = $"{_serverUrl}/portal.php?type=stb&action=get_profile&JsHttpRequest=1-xml";
                var profileResponse = await GetJsonAsync<StalkerProfileResponse>(profileUrl);

                if (profileResponse?.Js == null)
                {
                    return null;
                }

                return new StalkerAccountInfo
                {
                    MacAddress = _macAddress,
                    ExpDate = profileResponse.Js.ExpDate ?? "N/A",
                    Login = profileResponse.Js.Login ?? _macAddress,
                    Status = profileResponse.Js.Status == 1 ? "Active" : "Inactive",
                    ServerUrl = _serverUrl
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get all live TV channels
        /// </summary>
        public async Task<List<MediaItem>> GetLiveChannelsAsync(string sourceId)
        {
            var channels = new List<MediaItem>();
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_serverUrl)) return channels;

            try
            {
                var url = $"{_serverUrl}/portal.php?type=itv&action=get_all_channels&JsHttpRequest=1-xml";
                var response = await GetJsonAsync<StalkerChannelsResponse>(url);

                if (response?.Js?.Data == null) return channels;

                foreach (var ch in response.Js.Data)
                {
                    channels.Add(new MediaItem
                    {
                        Id = $"stk_{ch.Id}",
                        SourceId = sourceId,
                        Name = ch.Name ?? "Unknown",
                        LogoUrl = ch.Logo,
                        StreamUrl = GetStreamUrl(ch.Cmd),
                        GroupTitle = ch.TvGenreId ?? "General",
                        EpgId = ch.EpgId,
                        MediaType = ContentType.Live
                    });
                }
            }
            catch { }

            return channels;
        }

        /// <summary>
        /// Get live TV categories
        /// </summary>
        public async Task<List<StalkerCategory>> GetLiveCategoriesAsync()
        {
            var categories = new List<StalkerCategory>();
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_serverUrl)) return categories;

            try
            {
                var url = $"{_serverUrl}/portal.php?type=itv&action=get_genres&JsHttpRequest=1-xml";
                var response = await GetJsonAsync<StalkerCategoriesResponse>(url);

                if (response?.Js != null)
                {
                    categories.AddRange(response.Js.Select(c => new StalkerCategory
                    {
                        Id = c.Id ?? "",
                        Title = c.Title ?? "Unknown",
                        Alias = c.Alias
                    }));
                }
            }
            catch { }

            return categories;
        }

        /// <summary>
        /// Get channels by category
        /// </summary>
        public async Task<List<MediaItem>> GetChannelsByCategoryAsync(string sourceId, string genreId, int page = 1)
        {
            var channels = new List<MediaItem>();
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_serverUrl)) return channels;

            try
            {
                var url = $"{_serverUrl}/portal.php?type=itv&action=get_ordered_list&genre={genreId}&p={page}&JsHttpRequest=1-xml";
                var response = await GetJsonAsync<StalkerOrderedListResponse>(url);

                if (response?.Js?.Data == null) return channels;

                foreach (var ch in response.Js.Data)
                {
                    channels.Add(new MediaItem
                    {
                        Id = $"stk_{ch.Id}",
                        SourceId = sourceId,
                        Name = ch.Name ?? "Unknown",
                        LogoUrl = ch.Logo,
                        StreamUrl = GetStreamUrl(ch.Cmd),
                        GroupTitle = genreId,
                        MediaType = ContentType.Live
                    });
                }
            }
            catch { }

            return channels;
        }

        /// <summary>
        /// Get VOD categories
        /// </summary>
        public async Task<List<StalkerCategory>> GetVodCategoriesAsync()
        {
            var categories = new List<StalkerCategory>();
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_serverUrl)) return categories;

            try
            {
                var url = $"{_serverUrl}/portal.php?type=vod&action=get_categories&JsHttpRequest=1-xml";
                var response = await GetJsonAsync<StalkerCategoriesResponse>(url);

                if (response?.Js != null)
                {
                    categories.AddRange(response.Js.Select(c => new StalkerCategory
                    {
                        Id = c.Id ?? "",
                        Title = c.Title ?? "Unknown",
                        Alias = c.Alias
                    }));
                }
            }
            catch { }

            return categories;
        }

        /// <summary>
        /// Get VOD movies by category
        /// </summary>
        public async Task<List<MediaItem>> GetVodByCategoryAsync(string sourceId, string categoryId, int page = 1)
        {
            var movies = new List<MediaItem>();
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_serverUrl)) return movies;

            try
            {
                var url = $"{_serverUrl}/portal.php?type=vod&action=get_ordered_list&category={categoryId}&p={page}&JsHttpRequest=1-xml";
                var response = await GetJsonAsync<StalkerVodResponse>(url);

                if (response?.Js?.Data == null) return movies;

                foreach (var vod in response.Js.Data)
                {
                    movies.Add(new MediaItem
                    {
                        Id = $"vod_{vod.Id}",
                        SourceId = sourceId,
                        Name = vod.Name ?? "Unknown",
                        LogoUrl = vod.ScreenshotUri ?? vod.Logo,
                        StreamUrl = GetStreamUrl(vod.Cmd),
                        GroupTitle = categoryId,
                        Overview = vod.Description,
                        ReleaseDate = ParseYear(vod.Year),
                        Director = vod.Director,
                        MediaType = ContentType.Movie
                    });
                }
            }
            catch { }

            return movies;
        }

        /// <summary>
        /// Get series categories
        /// </summary>
        public async Task<List<StalkerCategory>> GetSeriesCategoriesAsync()
        {
            var categories = new List<StalkerCategory>();
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_serverUrl)) return categories;

            try
            {
                var url = $"{_serverUrl}/portal.php?type=series&action=get_categories&JsHttpRequest=1-xml";
                var response = await GetJsonAsync<StalkerCategoriesResponse>(url);

                if (response?.Js != null)
                {
                    categories.AddRange(response.Js.Select(c => new StalkerCategory
                    {
                        Id = c.Id ?? "",
                        Title = c.Title ?? "Unknown",
                        Alias = c.Alias
                    }));
                }
            }
            catch { }

            return categories;
        }

        /// <summary>
        /// Create stream link for playback
        /// </summary>
        public async Task<string?> CreateLinkAsync(string cmd)
        {
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_serverUrl)) return null;

            try
            {
                var cleanCmd = cmd.Replace("ffmpeg ", "").Trim();
                var url = $"{_serverUrl}/portal.php?type=itv&action=create_link&cmd={Uri.EscapeDataString(cleanCmd)}&JsHttpRequest=1-xml";
                var response = await GetJsonAsync<StalkerLinkResponse>(url);

                return response?.Js?.Cmd?.Replace("ffmpeg ", "").Trim();
            }
            catch
            {
                return null;
            }
        }

        private void SetupHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            _httpClient.DefaultRequestHeaders.Add("X-User-Agent", "Model 1 realHTTP/1.1");

            if (!string.IsNullOrEmpty(_macAddress))
            {
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"mac={Uri.EscapeDataString(_macAddress)}; stb_lang=fr; timezone=Europe%2FParis");
            }

            if (!string.IsNullOrEmpty(_token))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
            }
        }

        private async Task<T?> GetJsonAsync<T>(string url) where T : class
        {
            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<T>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private string GetStreamUrl(string? cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return "";
            // Remove ffmpeg prefix if present
            return cmd.Replace("ffmpeg ", "").Trim();
        }

        private DateTime ParseYear(string? year)
        {
            if (int.TryParse(year, out int y) && y > 1900 && y < 2100)
            {
                return new DateTime(y, 1, 1);
            }
            return DateTime.MinValue;
        }
    }

    #region Stalker Response Models

    public class StalkerAccountInfo
    {
        public string MacAddress { get; set; } = "";
        public string ExpDate { get; set; } = "";
        public string Login { get; set; } = "";
        public string Status { get; set; } = "";
        public string ServerUrl { get; set; } = "";
    }

    public class StalkerCategory
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Alias { get; set; }
    }

    // JSON Response Models
    public class StalkerResponse
    {
        public StalkerJs? Js { get; set; }
    }

    public class StalkerJs
    {
        public string? Token { get; set; }
    }

    public class StalkerProfileResponse
    {
        public StalkerProfile? Js { get; set; }
    }

    public class StalkerProfile
    {
        public string? ExpDate { get; set; }
        public string? Login { get; set; }
        public int Status { get; set; }
    }

    public class StalkerChannelsResponse
    {
        public StalkerChannelsList? Js { get; set; }
    }

    public class StalkerChannelsList
    {
        public List<StalkerChannel>? Data { get; set; }
    }

    public class StalkerChannel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Logo { get; set; }
        public string? Cmd { get; set; }
        public string? TvGenreId { get; set; }
        public string? EpgId { get; set; }
    }

    public class StalkerCategoriesResponse
    {
        public List<StalkerCategoryItem>? Js { get; set; }
    }

    public class StalkerCategoryItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Alias { get; set; }
    }

    public class StalkerOrderedListResponse
    {
        public StalkerOrderedList? Js { get; set; }
    }

    public class StalkerOrderedList
    {
        public List<StalkerChannel>? Data { get; set; }
        public int TotalItems { get; set; }
    }

    public class StalkerVodResponse
    {
        public StalkerVodList? Js { get; set; }
    }

    public class StalkerVodList
    {
        public List<StalkerVodItem>? Data { get; set; }
        public int TotalItems { get; set; }
    }

    public class StalkerVodItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Logo { get; set; }
        public string? ScreenshotUri { get; set; }
        public string? Cmd { get; set; }
        public string? Description { get; set; }
        public string? Year { get; set; }
        public string? Director { get; set; }
    }

    public class StalkerLinkResponse
    {
        public StalkerLink? Js { get; set; }
    }

    public class StalkerLink
    {
        public string? Cmd { get; set; }
    }

    #endregion
}
