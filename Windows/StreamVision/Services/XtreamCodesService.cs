using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Service complet pour l'API Xtream Codes
    /// Supporte Live, VOD, Series, Catch-up
    /// </summary>
    public class XtreamCodesService
    {
        private readonly HttpClient _httpClient;
        private XtreamAccountInfo? _currentAccount;

        public XtreamCodesService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            // User-Agent requis par certains serveurs IPTV qui bloquent les requêtes sans header navigateur
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public XtreamAccountInfo? CurrentAccount => _currentAccount;

        // Dernier message d'erreur pour l'utilisateur
        public string LastError { get; private set; } = string.Empty;

        #region Authentication

        public async Task<XtreamAccountInfo?> AuthenticateAsync(string serverUrl, string username, string password)
        {
            LastError = string.Empty;

            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}";

                HttpResponseMessage httpResponse;
                try
                {
                    httpResponse = await _httpClient.GetAsync(url);
                }
                catch (HttpRequestException ex)
                {
                    LastError = $"Impossible de contacter le serveur. Vérifiez l'URL et votre connexion internet.\n\nDétails: {ex.Message}";
                    return null;
                }
                catch (TaskCanceledException)
                {
                    LastError = "Le serveur met trop de temps à répondre (timeout). Vérifiez l'URL ou réessayez plus tard.";
                    return null;
                }

                if (!httpResponse.IsSuccessStatusCode)
                {
                    LastError = $"Le serveur a retourné une erreur: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}";
                    return null;
                }

                var response = await httpResponse.Content.ReadAsStringAsync();

                JObject json;
                try
                {
                    json = JObject.Parse(response);
                }
                catch
                {
                    LastError = "Réponse invalide du serveur. Ce n'est peut-être pas un serveur Xtream Codes.";
                    return null;
                }

                if (json["user_info"] == null)
                {
                    // Vérifier si c'est une erreur d'authentification
                    var authStatus = json["user_info"]?["auth"]?.ToString();
                    if (authStatus == "0")
                    {
                        LastError = "Identifiants incorrects. Vérifiez votre nom d'utilisateur et mot de passe.";
                    }
                    else
                    {
                        LastError = "Authentification échouée. Vérifiez vos identifiants.";
                    }
                    return null;
                }

                var userInfo = json["user_info"];
                var status = userInfo?["status"]?.ToString() ?? "";
                var expDateStr = userInfo?["exp_date"]?.ToString() ?? "";

                // Vérifier si le compte est actif
                if (status.ToLower() == "disabled" || status.ToLower() == "banned")
                {
                    LastError = $"Ce compte est désactivé ou banni. Contactez votre fournisseur IPTV.";
                    return null;
                }

                // Vérifier si le compte est expiré
                if (!string.IsNullOrEmpty(expDateStr) && long.TryParse(expDateStr, out long expTimestamp))
                {
                    var expDate = DateTimeOffset.FromUnixTimeSeconds(expTimestamp).DateTime;
                    if (expDate < DateTime.Now)
                    {
                        LastError = $"Ce compte a expiré le {expDate:dd/MM/yyyy}. Renouvelez votre abonnement.";
                        return null;
                    }
                }

                var serverInfo = json["server_info"];
                _currentAccount = new XtreamAccountInfo
                {
                    Username = userInfo?["username"]?.ToString() ?? username,
                    Status = status,
                    ExpDate = expDateStr,
                    MaxConnections = userInfo?["max_connections"]?.ToString() ?? "",
                    ActiveConnections = userInfo?["active_cons"]?.ToString() ?? "0",
                    ServerUrl = serverUrl,
                    ServerPort = serverInfo?["port"]?.ToString() ?? "80",
                    HttpsPort = serverInfo?["https_port"]?.ToString() ?? "443",
                    ServerProtocol = serverInfo?["server_protocol"]?.ToString() ?? "http",
                    TimeZone = serverInfo?["timezone"]?.ToString() ?? ""
                };

                return _currentAccount;
            }
            catch (Exception ex)
            {
                LastError = $"Erreur inattendue: {ex.Message}";
                return null;
            }
        }

        #endregion

        #region Categories

        public async Task<List<XtreamCategory>> GetLiveCategoriesAsync(string serverUrl, string username, string password)
        {
            return await GetCategoriesAsync(serverUrl, username, password, "get_live_categories");
        }

        public async Task<List<XtreamCategory>> GetVodCategoriesAsync(string serverUrl, string username, string password)
        {
            return await GetCategoriesAsync(serverUrl, username, password, "get_vod_categories");
        }

        public async Task<List<XtreamCategory>> GetSeriesCategoriesAsync(string serverUrl, string username, string password)
        {
            return await GetCategoriesAsync(serverUrl, username, password, "get_series_categories");
        }

        private async Task<List<XtreamCategory>> GetCategoriesAsync(string serverUrl, string username, string password, string action)
        {
            var categories = new List<XtreamCategory>();
            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}&action={action}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JArray.Parse(response);

                foreach (var item in json)
                {
                    categories.Add(new XtreamCategory
                    {
                        CategoryId = item["category_id"]?.ToString() ?? "",
                        CategoryName = item["category_name"]?.ToString() ?? "",
                        ParentId = item["parent_id"]?.ToString() ?? "0"
                    });
                }
            }
            catch { }
            return categories;
        }

        #endregion

        #region Live Streams

        public async Task<List<MediaItem>> GetLiveStreamsAsync(string serverUrl, string username, string password, string sourceId, string? categoryId = null)
        {
            var items = new List<MediaItem>();
            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}&action=get_live_streams";
                if (!string.IsNullOrEmpty(categoryId))
                    url += $"&category_id={categoryId}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JArray.Parse(response);

                var categories = await GetLiveCategoriesAsync(serverUrl, username, password);
                var categoryDict = categories.ToDictionary(c => c.CategoryId, c => c.CategoryName);

                int order = 0;
                foreach (var item in json)
                {
                    var streamId = item["stream_id"]?.ToString() ?? "";
                    var catId = item["category_id"]?.ToString() ?? "";
                    var catchupDays = int.TryParse(item["tv_archive_duration"]?.ToString(), out var days) ? days : 0;

                    items.Add(new MediaItem
                    {
                        SourceId = sourceId,
                        Name = item["name"]?.ToString() ?? "",
                        LogoUrl = item["stream_icon"]?.ToString(),
                        StreamUrl = $"{serverUrl}/live/{username}/{password}/{streamId}.m3u8",
                        GroupTitle = categoryDict.GetValueOrDefault(catId, "Uncategorized"),
                        CategoryId = catId,
                        EpgId = item["epg_channel_id"]?.ToString(),
                        CatchupDays = catchupDays,
                        Order = order++,
                        MediaType = ContentType.Live
                    });
                }
            }
            catch { }
            return items;
        }

        // Compatibilité avec l'ancien code
        public async Task<List<Channel>> GetLiveStreamsAsChannelsAsync(string serverUrl, string username, string password, string sourceId)
        {
            var mediaItems = await GetLiveStreamsAsync(serverUrl, username, password, sourceId);
            return mediaItems.Select(m => new Channel
            {
                Id = m.Id,
                SourceId = m.SourceId,
                Name = m.Name,
                LogoUrl = m.LogoUrl,
                StreamUrl = m.StreamUrl,
                GroupTitle = m.GroupTitle,
                EpgId = m.EpgId,
                CatchupDays = m.CatchupDays,
                Order = m.Order
            }).ToList();
        }

        #endregion

        #region VOD (Movies)

        public async Task<List<MediaItem>> GetVodStreamsAsync(string serverUrl, string username, string password, string sourceId, string? categoryId = null)
        {
            var items = new List<MediaItem>();
            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}&action=get_vod_streams";
                if (!string.IsNullOrEmpty(categoryId))
                    url += $"&category_id={categoryId}";

                var response = await _httpClient.GetStringAsync(url);
                var json = JArray.Parse(response);

                var categories = await GetVodCategoriesAsync(serverUrl, username, password);
                var categoryDict = categories.ToDictionary(c => c.CategoryId, c => c.CategoryName);

                int order = 0;
                foreach (var item in json)
                {
                    var streamId = item["stream_id"]?.ToString() ?? "";
                    var extension = item["container_extension"]?.ToString() ?? "mp4";
                    var catId = item["category_id"]?.ToString() ?? "";

                    var mediaItem = new MediaItem
                    {
                        SourceId = sourceId,
                        Name = item["name"]?.ToString() ?? "",
                        PosterUrl = item["stream_icon"]?.ToString(),
                        StreamUrl = $"{serverUrl}/movie/{username}/{password}/{streamId}.{extension}",
                        GroupTitle = categoryDict.GetValueOrDefault(catId, "Films"),
                        CategoryId = catId,
                        Order = order++,
                        MediaType = ContentType.Movie,
                        ContainerExtension = extension,
                        Rating = double.TryParse(item["rating"]?.ToString(), out var r) ? r : 0,
                        TmdbId = int.TryParse(item["tmdb_id"]?.ToString(), out var tid) ? tid : null
                    };

                    // Parse additional info if available
                    if (item["added"] != null)
                    {
                        if (long.TryParse(item["added"].ToString(), out var timestamp))
                            mediaItem.ReleaseDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    }

                    items.Add(mediaItem);
                }
            }
            catch { }
            return items;
        }

        public async Task<VodInfo?> GetVodInfoAsync(string serverUrl, string username, string password, string vodId)
        {
            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}&action=get_vod_info&vod_id={vodId}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var info = json["info"];
                var movieData = json["movie_data"];

                return new VodInfo
                {
                    StreamId = vodId,
                    Name = info?["name"]?.ToString() ?? movieData?["name"]?.ToString() ?? "",
                    Overview = info?["plot"]?.ToString() ?? info?["description"]?.ToString() ?? "",
                    PosterUrl = info?["movie_image"]?.ToString() ?? info?["cover_big"]?.ToString() ?? "",
                    BackdropUrl = info?["backdrop_path"]?.FirstOrDefault()?.ToString() ?? "",
                    Rating = double.TryParse(info?["rating"]?.ToString(), out var r) ? r : 0,
                    Duration = info?["duration"]?.ToString() ?? "",
                    ReleaseDate = info?["releasedate"]?.ToString() ?? info?["release_date"]?.ToString() ?? "",
                    Genre = info?["genre"]?.ToString() ?? "",
                    Director = info?["director"]?.ToString() ?? "",
                    Cast = info?["cast"]?.ToString() ?? info?["actors"]?.ToString() ?? "",
                    TmdbId = int.TryParse(info?["tmdb_id"]?.ToString(), out var tid) ? tid : null,
                    TrailerUrl = info?["youtube_trailer"]?.ToString(),
                    ContainerExtension = movieData?["container_extension"]?.ToString() ?? "mp4"
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Series

        public async Task<List<MediaItem>> GetSeriesAsync(string serverUrl, string username, string password, string sourceId, string? categoryId = null)
        {
            var items = new List<MediaItem>();
            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}&action=get_series";
                if (!string.IsNullOrEmpty(categoryId))
                    url += $"&category_id={categoryId}";

                var response = await _httpClient.GetStringAsync(url);
                var json = JArray.Parse(response);

                var categories = await GetSeriesCategoriesAsync(serverUrl, username, password);
                var categoryDict = categories.ToDictionary(c => c.CategoryId, c => c.CategoryName);

                int order = 0;
                foreach (var item in json)
                {
                    var seriesId = item["series_id"]?.ToString() ?? "";
                    var catId = item["category_id"]?.ToString() ?? "";

                    items.Add(new MediaItem
                    {
                        SourceId = sourceId,
                        SeriesId = seriesId,
                        Name = item["name"]?.ToString() ?? "",
                        PosterUrl = item["cover"]?.ToString(),
                        BackdropUrl = item["backdrop_path"]?.FirstOrDefault()?.ToString(),
                        StreamUrl = "", // Les séries n'ont pas d'URL directe
                        GroupTitle = categoryDict.GetValueOrDefault(catId, "Séries"),
                        CategoryId = catId,
                        Order = order++,
                        MediaType = ContentType.Series,
                        Rating = double.TryParse(item["rating"]?.ToString(), out var r) ? r : 0,
                        TmdbId = int.TryParse(item["tmdb_id"]?.ToString(), out var tid) ? tid : null,
                        Overview = item["plot"]?.ToString() ?? "",
                        Genres = item["genre"]?.ToString() ?? "",
                        Cast = item["cast"]?.ToString() ?? "",
                        ReleaseDate = DateTime.TryParse(item["releaseDate"]?.ToString() ?? item["last_modified"]?.ToString(), out var d) ? d : null
                    });
                }
            }
            catch { }
            return items;
        }

        public async Task<SeriesFullInfo?> GetSeriesInfoAsync(string serverUrl, string username, string password, string seriesId)
        {
            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}&action=get_series_info&series_id={seriesId}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var info = json["info"];
                var episodes = json["episodes"];

                var seriesInfo = new SeriesFullInfo
                {
                    SeriesId = seriesId,
                    Name = info?["name"]?.ToString() ?? "",
                    Overview = info?["plot"]?.ToString() ?? "",
                    PosterUrl = info?["cover"]?.ToString() ?? "",
                    BackdropUrl = (info?["backdrop_path"] as JArray)?.FirstOrDefault()?.ToString() ?? "",
                    Rating = double.TryParse(info?["rating"]?.ToString(), out var r) ? r : 0,
                    Genre = info?["genre"]?.ToString() ?? "",
                    Director = info?["director"]?.ToString() ?? "",
                    Cast = info?["cast"]?.ToString() ?? "",
                    ReleaseDate = info?["releaseDate"]?.ToString() ?? "",
                    TmdbId = int.TryParse(info?["tmdb_id"]?.ToString(), out var tid) ? tid : null,
                    Seasons = new List<SeriesSeasonInfo>()
                };

                // Parse seasons and episodes
                if (episodes is JObject episodesObj)
                {
                    foreach (var seasonProp in episodesObj.Properties())
                    {
                        var seasonNum = int.TryParse(seasonProp.Name, out var s) ? s : 0;
                        var seasonInfo = new SeriesSeasonInfo
                        {
                            SeasonNumber = seasonNum,
                            Episodes = new List<EpisodeInfo>()
                        };

                        if (seasonProp.Value is JArray episodesArray)
                        {
                            foreach (var ep in episodesArray)
                            {
                                var episodeId = ep["id"]?.ToString() ?? "";
                                var extension = ep["container_extension"]?.ToString() ?? "mp4";

                                seasonInfo.Episodes.Add(new EpisodeInfo
                                {
                                    Id = episodeId,
                                    EpisodeNumber = int.TryParse(ep["episode_num"]?.ToString(), out var en) ? en : 0,
                                    Title = ep["title"]?.ToString() ?? $"Episode {en}",
                                    Overview = ep["plot"]?.ToString() ?? "",
                                    Duration = ep["duration"]?.ToString() ?? "",
                                    PosterUrl = ep["info"]?["movie_image"]?.ToString() ?? "",
                                    StreamUrl = $"{serverUrl}/series/{username}/{password}/{episodeId}.{extension}",
                                    ContainerExtension = extension,
                                    Rating = double.TryParse(ep["rating"]?.ToString(), out var er) ? er : 0
                                });
                            }
                        }

                        seriesInfo.Seasons.Add(seasonInfo);
                    }
                }

                return seriesInfo;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Catch-up / Timeshift

        public string GetCatchupUrl(string serverUrl, string username, string password, string streamId, DateTime startTime, int durationMinutes)
        {
            serverUrl = NormalizeServerUrl(serverUrl);
            var startUtc = startTime.ToUniversalTime().ToString("yyyy-MM-dd:HH-mm");
            return $"{serverUrl}/timeshift/{username}/{password}/{durationMinutes}/{startUtc}/{streamId}.m3u8";
        }

        public string GetCatchupUrlSimple(string serverUrl, string username, string password, string streamId, DateTime startTime)
        {
            serverUrl = NormalizeServerUrl(serverUrl);
            var start = ((DateTimeOffset)startTime.ToUniversalTime()).ToUnixTimeSeconds();
            return $"{serverUrl}/streaming/timeshift.php?username={username}&password={password}&stream={streamId}&start={start}";
        }

        #endregion

        #region Short EPG

        public async Task<List<XtreamEpgEntry>> GetShortEpgAsync(string serverUrl, string username, string password, string streamId, int limit = 10)
        {
            var entries = new List<XtreamEpgEntry>();
            try
            {
                serverUrl = NormalizeServerUrl(serverUrl);
                var url = $"{serverUrl}/player_api.php?username={username}&password={password}&action=get_short_epg&stream_id={streamId}&limit={limit}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var epgListings = json["epg_listings"] as JArray;
                if (epgListings != null)
                {
                    foreach (var item in epgListings)
                    {
                        entries.Add(new XtreamEpgEntry
                        {
                            Id = item["id"]?.ToString() ?? "",
                            Title = DecodeBase64(item["title"]?.ToString() ?? ""),
                            Description = DecodeBase64(item["description"]?.ToString() ?? ""),
                            Start = ParseEpgDateTime(item["start"]?.ToString()),
                            End = ParseEpgDateTime(item["end"]?.ToString()),
                            StartTimestamp = item["start_timestamp"]?.ToString() ?? "",
                            StopTimestamp = item["stop_timestamp"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch { }
            return entries;
        }

        #endregion

        #region Helpers

        private static string NormalizeServerUrl(string url)
        {
            url = url.Trim().TrimEnd('/');
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "http://" + url;
            return url;
        }

        private static string DecodeBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return "";
            try
            {
                var bytes = Convert.FromBase64String(base64);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return base64; // Return as-is if not base64
            }
        }

        private static DateTime? ParseEpgDateTime(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return null;
            if (DateTime.TryParse(dateStr, out var date)) return date;
            return null;
        }

        #endregion
    }

    #region DTOs

    public class XtreamAccountInfo
    {
        public string Username { get; set; } = "";
        public string Status { get; set; } = "";
        public string ExpDate { get; set; } = "";
        public string MaxConnections { get; set; } = "";
        public string ActiveConnections { get; set; } = "";
        public string ServerUrl { get; set; } = "";
        public string ServerPort { get; set; } = "";
        public string HttpsPort { get; set; } = "";
        public string ServerProtocol { get; set; } = "";
        public string TimeZone { get; set; } = "";

        public bool IsExpired
        {
            get
            {
                if (string.IsNullOrEmpty(ExpDate) || ExpDate == "null") return false;
                if (long.TryParse(ExpDate, out var timestamp))
                {
                    var expiry = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    return expiry < DateTime.Now;
                }
                return false;
            }
        }

        public DateTime? ExpiryDate
        {
            get
            {
                if (string.IsNullOrEmpty(ExpDate) || ExpDate == "null") return null;
                if (long.TryParse(ExpDate, out var timestamp))
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                return null;
            }
        }
    }

    public class XtreamCategory
    {
        public string CategoryId { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string ParentId { get; set; } = "";
    }

    public class VodInfo
    {
        public string StreamId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Overview { get; set; } = "";
        public string PosterUrl { get; set; } = "";
        public string BackdropUrl { get; set; } = "";
        public double Rating { get; set; }
        public string Duration { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Director { get; set; } = "";
        public string Cast { get; set; } = "";
        public int? TmdbId { get; set; }
        public string? TrailerUrl { get; set; }
        public string ContainerExtension { get; set; } = "mp4";
    }

    public class SeriesFullInfo
    {
        public string SeriesId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Overview { get; set; } = "";
        public string PosterUrl { get; set; } = "";
        public string BackdropUrl { get; set; } = "";
        public double Rating { get; set; }
        public string Genre { get; set; } = "";
        public string Director { get; set; } = "";
        public string Cast { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public int? TmdbId { get; set; }
        public List<SeriesSeasonInfo> Seasons { get; set; } = new();
    }

    public class SeriesSeasonInfo
    {
        public int SeasonNumber { get; set; }
        public string Name => $"Saison {SeasonNumber}";
        public List<EpisodeInfo> Episodes { get; set; } = new();
    }

    public class EpisodeInfo
    {
        public string Id { get; set; } = "";
        public int EpisodeNumber { get; set; }
        public string Title { get; set; } = "";
        public string Overview { get; set; } = "";
        public string Duration { get; set; } = "";
        public string PosterUrl { get; set; } = "";
        public string StreamUrl { get; set; } = "";
        public string ContainerExtension { get; set; } = "";
        public double Rating { get; set; }
    }

    public class XtreamEpgEntry
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string StartTimestamp { get; set; } = "";
        public string StopTimestamp { get; set; } = "";

        public bool IsNow => Start.HasValue && End.HasValue && DateTime.Now >= Start && DateTime.Now < End;
        public bool IsPast => End.HasValue && DateTime.Now >= End;
    }

    #endregion
}
