using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// Service pour récupérer les métadonnées depuis TMDb (The Movie Database)
    /// </summary>
    public class TmdbService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.themoviedb.org/3";
        private const string ImageBaseUrl = "https://image.tmdb.org/t/p";

        // Cache pour éviter les requêtes répétées
        private readonly Dictionary<string, JObject?> _searchCache = new();
        private readonly Dictionary<int, JObject?> _movieCache = new();
        private readonly Dictionary<int, JObject?> _tvCache = new();

        public TmdbService(string? apiKey = null)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // Clé API TMDb (v3) - gratuite avec inscription sur themoviedb.org
            _apiKey = apiKey ?? ""; // L'utilisateur devra fournir sa clé
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        /// <summary>
        /// URLs d'images selon la taille
        /// </summary>
        public static string GetPosterUrl(string? path, string size = "w500")
            => string.IsNullOrEmpty(path) ? "" : $"{ImageBaseUrl}/{size}{path}";

        public static string GetBackdropUrl(string? path, string size = "w1280")
            => string.IsNullOrEmpty(path) ? "" : $"{ImageBaseUrl}/{size}{path}";

        /// <summary>
        /// Recherche un film par nom
        /// </summary>
        public async Task<TmdbSearchResult?> SearchMovieAsync(string query, int? year = null)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(query)) return null;

            try
            {
                var cleanQuery = CleanTitle(query);
                var cacheKey = $"movie:{cleanQuery}:{year}";

                if (_searchCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached != null ? ParseSearchResult(cached, true) : null;
                }

                var url = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(cleanQuery)}&language=fr-FR";
                if (year.HasValue)
                    url += $"&year={year}";

                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                var results = json["results"] as JArray;

                if (results == null || results.Count == 0)
                {
                    _searchCache[cacheKey] = null;
                    return null;
                }

                var first = results[0] as JObject;
                _searchCache[cacheKey] = first;
                return ParseSearchResult(first!, true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recherche une série TV par nom
        /// </summary>
        public async Task<TmdbSearchResult?> SearchTvShowAsync(string query, int? year = null)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(query)) return null;

            try
            {
                var cleanQuery = CleanTitle(query);
                var cacheKey = $"tv:{cleanQuery}:{year}";

                if (_searchCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached != null ? ParseSearchResult(cached, false) : null;
                }

                var url = $"{BaseUrl}/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(cleanQuery)}&language=fr-FR";
                if (year.HasValue)
                    url += $"&first_air_date_year={year}";

                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                var results = json["results"] as JArray;

                if (results == null || results.Count == 0)
                {
                    _searchCache[cacheKey] = null;
                    return null;
                }

                var first = results[0] as JObject;
                _searchCache[cacheKey] = first;
                return ParseSearchResult(first!, false);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Récupère les détails complets d'un film
        /// </summary>
        public async Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId)
        {
            if (!IsConfigured) return null;

            try
            {
                if (_movieCache.TryGetValue(tmdbId, out var cached) && cached != null)
                {
                    return ParseMovieDetails(cached);
                }

                var url = $"{BaseUrl}/movie/{tmdbId}?api_key={_apiKey}&language=fr-FR&append_to_response=credits,videos";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                _movieCache[tmdbId] = json;
                return ParseMovieDetails(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Récupère les détails complets d'une série TV
        /// </summary>
        public async Task<TmdbTvDetails?> GetTvDetailsAsync(int tmdbId)
        {
            if (!IsConfigured) return null;

            try
            {
                if (_tvCache.TryGetValue(tmdbId, out var cached) && cached != null)
                {
                    return ParseTvDetails(cached);
                }

                var url = $"{BaseUrl}/tv/{tmdbId}?api_key={_apiKey}&language=fr-FR&append_to_response=credits,videos";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                _tvCache[tmdbId] = json;
                return ParseTvDetails(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Enrichit un MediaItem avec les données TMDb
        /// </summary>
        public async Task EnrichMediaItemAsync(MediaItem item)
        {
            if (!IsConfigured) return;

            try
            {
                if (item.MediaType == ContentType.Movie)
                {
                    var year = ExtractYearFromTitle(item.Name);
                    var result = await SearchMovieAsync(item.Name, year);

                    if (result != null)
                    {
                        item.TmdbId = result.Id;
                        item.PosterUrl = result.PosterUrl;
                        item.BackdropUrl = result.BackdropUrl;
                        item.Overview = result.Overview;
                        item.Rating = result.Rating;
                        item.VoteCount = result.VoteCount;
                        item.ReleaseDate = result.ReleaseDate;

                        // Détails supplémentaires
                        var details = await GetMovieDetailsAsync(result.Id);
                        if (details != null)
                        {
                            item.Runtime = details.Runtime;
                            item.Genres = details.Genres;
                            item.Director = details.Director;
                            item.Cast = details.Cast;
                            item.TrailerUrl = details.TrailerUrl;
                        }
                    }
                }
                else if (item.MediaType == ContentType.Series)
                {
                    var result = await SearchTvShowAsync(item.Name);

                    if (result != null)
                    {
                        item.TmdbId = result.Id;
                        item.PosterUrl = result.PosterUrl;
                        item.BackdropUrl = result.BackdropUrl;
                        item.Overview = result.Overview;
                        item.Rating = result.Rating;
                        item.VoteCount = result.VoteCount;
                        item.ReleaseDate = result.ReleaseDate;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Enrichit plusieurs MediaItems en parallèle (avec rate limiting)
        /// </summary>
        public async Task EnrichMediaItemsAsync(IEnumerable<MediaItem> items, int maxConcurrency = 5)
        {
            var semaphore = new System.Threading.SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();

            foreach (var item in items)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await EnrichMediaItemAsync(item);
                        await Task.Delay(100); // Rate limiting
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        // Helpers privés

        private static string CleanTitle(string title)
        {
            // Enlève l'année entre parenthèses, les tags de qualité, etc.
            var cleaned = Regex.Replace(title, @"\s*\(\d{4}\)\s*", " ");
            cleaned = Regex.Replace(cleaned, @"\s*\[.*?\]\s*", " ");
            cleaned = Regex.Replace(cleaned, @"\s*(720p|1080p|4K|HDR|WEB-DL|BluRay|HDTV|VOSTFR|FRENCH|MULTI).*", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            return cleaned.Trim();
        }

        private static int? ExtractYearFromTitle(string title)
        {
            var match = Regex.Match(title, @"\((\d{4})\)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
                return year;
            return null;
        }

        private TmdbSearchResult ParseSearchResult(JObject json, bool isMovie)
        {
            return new TmdbSearchResult
            {
                Id = json["id"]?.Value<int>() ?? 0,
                Title = (isMovie ? json["title"]?.ToString() : json["name"]?.ToString()) ?? "",
                OriginalTitle = (isMovie ? json["original_title"]?.ToString() : json["original_name"]?.ToString()) ?? "",
                Overview = json["overview"]?.ToString() ?? "",
                PosterUrl = GetPosterUrl(json["poster_path"]?.ToString()),
                BackdropUrl = GetBackdropUrl(json["backdrop_path"]?.ToString()),
                Rating = json["vote_average"]?.Value<double>() ?? 0,
                VoteCount = json["vote_count"]?.Value<int>() ?? 0,
                ReleaseDate = DateTime.TryParse(
                    (isMovie ? json["release_date"]?.ToString() : json["first_air_date"]?.ToString()),
                    out var date) ? date : null,
                IsMovie = isMovie
            };
        }

        private TmdbMovieDetails ParseMovieDetails(JObject json)
        {
            var credits = json["credits"];
            var videos = json["videos"]?["results"] as JArray;

            // Réalisateur
            var directors = new List<string>();
            if (credits?["crew"] is JArray crew)
            {
                foreach (var member in crew)
                {
                    if (member["job"]?.ToString() == "Director")
                        directors.Add(member["name"]?.ToString() ?? "");
                }
            }

            // Acteurs principaux
            var actors = new List<string>();
            if (credits?["cast"] is JArray cast)
            {
                for (int i = 0; i < Math.Min(5, cast.Count); i++)
                {
                    actors.Add(cast[i]["name"]?.ToString() ?? "");
                }
            }

            // Genres
            var genres = new List<string>();
            if (json["genres"] is JArray genreArray)
            {
                foreach (var genre in genreArray)
                {
                    genres.Add(genre["name"]?.ToString() ?? "");
                }
            }

            // Trailer YouTube
            string? trailerUrl = null;
            if (videos != null)
            {
                foreach (var video in videos)
                {
                    if (video["site"]?.ToString() == "YouTube" &&
                        (video["type"]?.ToString() == "Trailer" || video["type"]?.ToString() == "Teaser"))
                    {
                        trailerUrl = $"https://www.youtube.com/watch?v={video["key"]}";
                        break;
                    }
                }
            }

            return new TmdbMovieDetails
            {
                Id = json["id"]?.Value<int>() ?? 0,
                Runtime = json["runtime"]?.Value<int>() ?? 0,
                Genres = string.Join(", ", genres),
                Director = string.Join(", ", directors),
                Cast = string.Join(", ", actors),
                TrailerUrl = trailerUrl,
                Budget = json["budget"]?.Value<long>() ?? 0,
                Revenue = json["revenue"]?.Value<long>() ?? 0
            };
        }

        private TmdbTvDetails ParseTvDetails(JObject json)
        {
            var credits = json["credits"];

            // Créateurs
            var creators = new List<string>();
            if (json["created_by"] is JArray createdBy)
            {
                foreach (var creator in createdBy)
                {
                    creators.Add(creator["name"]?.ToString() ?? "");
                }
            }

            // Acteurs
            var actors = new List<string>();
            if (credits?["cast"] is JArray cast)
            {
                for (int i = 0; i < Math.Min(5, cast.Count); i++)
                {
                    actors.Add(cast[i]["name"]?.ToString() ?? "");
                }
            }

            // Genres
            var genres = new List<string>();
            if (json["genres"] is JArray genreArray)
            {
                foreach (var genre in genreArray)
                {
                    genres.Add(genre["name"]?.ToString() ?? "");
                }
            }

            // Saisons
            var seasons = new List<TmdbSeason>();
            if (json["seasons"] is JArray seasonArray)
            {
                foreach (var season in seasonArray)
                {
                    seasons.Add(new TmdbSeason
                    {
                        SeasonNumber = season["season_number"]?.Value<int>() ?? 0,
                        Name = season["name"]?.ToString() ?? "",
                        EpisodeCount = season["episode_count"]?.Value<int>() ?? 0,
                        PosterUrl = GetPosterUrl(season["poster_path"]?.ToString()),
                        AirDate = DateTime.TryParse(season["air_date"]?.ToString(), out var d) ? d : null
                    });
                }
            }

            return new TmdbTvDetails
            {
                Id = json["id"]?.Value<int>() ?? 0,
                NumberOfSeasons = json["number_of_seasons"]?.Value<int>() ?? 0,
                NumberOfEpisodes = json["number_of_episodes"]?.Value<int>() ?? 0,
                EpisodeRuntime = (json["episode_run_time"] as JArray)?.FirstOrDefault()?.Value<int>() ?? 0,
                Genres = string.Join(", ", genres),
                Creators = string.Join(", ", creators),
                Cast = string.Join(", ", actors),
                Seasons = seasons,
                Status = json["status"]?.ToString() ?? ""
            };
        }
    }

    // DTOs TMDb
    public class TmdbSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string OriginalTitle { get; set; } = "";
        public string Overview { get; set; } = "";
        public string PosterUrl { get; set; } = "";
        public string BackdropUrl { get; set; } = "";
        public double Rating { get; set; }
        public int VoteCount { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public bool IsMovie { get; set; }
    }

    public class TmdbMovieDetails
    {
        public int Id { get; set; }
        public int Runtime { get; set; }
        public string Genres { get; set; } = "";
        public string Director { get; set; } = "";
        public string Cast { get; set; } = "";
        public string? TrailerUrl { get; set; }
        public long Budget { get; set; }
        public long Revenue { get; set; }
    }

    public class TmdbTvDetails
    {
        public int Id { get; set; }
        public int NumberOfSeasons { get; set; }
        public int NumberOfEpisodes { get; set; }
        public int EpisodeRuntime { get; set; }
        public string Genres { get; set; } = "";
        public string Creators { get; set; } = "";
        public string Cast { get; set; } = "";
        public List<TmdbSeason> Seasons { get; set; } = new();
        public string Status { get; set; } = "";
    }

    public class TmdbSeason
    {
        public int SeasonNumber { get; set; }
        public string Name { get; set; } = "";
        public int EpisodeCount { get; set; }
        public string PosterUrl { get; set; } = "";
        public DateTime? AirDate { get; set; }
    }
}
