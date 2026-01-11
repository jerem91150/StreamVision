using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StreamVision.Models;

namespace StreamVision.Services
{
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private SQLiteConnection? _connection;

        public DatabaseService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StreamVision");

            Directory.CreateDirectory(appDataPath);
            var dbPath = Path.Combine(appDataPath, "streamvision.db");
            _connectionString = $"Data Source={dbPath};Version=3;";
        }

        public async Task InitializeAsync()
        {
            _connection = new SQLiteConnection(_connectionString);
            await _connection.OpenAsync();
            await CreateTablesAsync();
        }

        private async Task CreateTablesAsync()
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS PlaylistSources (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    Url TEXT NOT NULL,
                    Username TEXT,
                    Password TEXT,
                    MacAddress TEXT,
                    EpgUrl TEXT,
                    LastSync TEXT,
                    IsActive INTEGER DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Channels (
                    Id TEXT PRIMARY KEY,
                    SourceId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    LogoUrl TEXT,
                    StreamUrl TEXT NOT NULL,
                    GroupTitle TEXT,
                    EpgId TEXT,
                    IsFavorite INTEGER DEFAULT 0,
                    CatchupDays INTEGER DEFAULT 0,
                    SortOrder INTEGER DEFAULT 0,
                    FOREIGN KEY (SourceId) REFERENCES PlaylistSources(Id)
                );

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                );

                CREATE TABLE IF NOT EXISTS RecentChannels (
                    ChannelId TEXT PRIMARY KEY,
                    LastWatched TEXT,
                    WatchCount INTEGER DEFAULT 1,
                    FOREIGN KEY (ChannelId) REFERENCES Channels(Id)
                );

                CREATE INDEX IF NOT EXISTS idx_channels_source ON Channels(SourceId);
                CREATE INDEX IF NOT EXISTS idx_channels_group ON Channels(GroupTitle);
                CREATE INDEX IF NOT EXISTS idx_channels_favorite ON Channels(IsFavorite);

                CREATE TABLE IF NOT EXISTS UserPreferences (
                    Id TEXT PRIMARY KEY,
                    PreferredLanguages TEXT,
                    ShowMovies INTEGER DEFAULT 1,
                    ShowSeries INTEGER DEFAULT 1,
                    ShowLiveTV INTEGER DEFAULT 1,
                    ShowAnime INTEGER DEFAULT 1,
                    AnimePreferSubbed INTEGER DEFAULT 1,
                    AnimePreferDubbed INTEGER DEFAULT 0,
                    PreferredGenres TEXT,
                    ExcludedGenres TEXT,
                    AdultContentEnabled INTEGER DEFAULT 0,
                    KidsMode INTEGER DEFAULT 0,
                    OnboardingCompleted INTEGER DEFAULT 0,
                    CreatedAt TEXT,
                    UpdatedAt TEXT
                );

                CREATE TABLE IF NOT EXISTS UserRatings (
                    Id TEXT PRIMARY KEY,
                    MediaId TEXT NOT NULL UNIQUE,
                    StarRating INTEGER DEFAULT 0,
                    QualityRating TEXT,
                    RatedAt TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_userratings_media ON UserRatings(MediaId);
            ";

            using var cmd = new SQLiteCommand(sql, _connection);
            await cmd.ExecuteNonQueryAsync();
        }

        // Playlist Sources
        public async Task<List<PlaylistSource>> GetPlaylistSourcesAsync()
        {
            var sources = new List<PlaylistSource>();
            var sql = "SELECT * FROM PlaylistSources ORDER BY Name";

            using var cmd = new SQLiteCommand(sql, _connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sources.Add(new PlaylistSource
                {
                    Id = reader["Id"].ToString() ?? "",
                    Name = reader["Name"].ToString() ?? "",
                    Type = (SourceType)Convert.ToInt32(reader["Type"]),
                    Url = reader["Url"].ToString() ?? "",
                    Username = reader["Username"]?.ToString(),
                    Password = reader["Password"]?.ToString(),
                    MacAddress = reader["MacAddress"]?.ToString(),
                    EpgUrl = reader["EpgUrl"]?.ToString(),
                    LastSync = DateTime.TryParse(reader["LastSync"]?.ToString(), out var dt) ? dt : DateTime.MinValue,
                    IsActive = Convert.ToInt32(reader["IsActive"]) == 1
                });
            }

            return sources;
        }

        public async Task SavePlaylistSourceAsync(PlaylistSource source)
        {
            var sql = @"
                INSERT OR REPLACE INTO PlaylistSources
                (Id, Name, Type, Url, Username, Password, MacAddress, EpgUrl, LastSync, IsActive)
                VALUES (@Id, @Name, @Type, @Url, @Username, @Password, @MacAddress, @EpgUrl, @LastSync, @IsActive)";

            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Id", source.Id);
            cmd.Parameters.AddWithValue("@Name", source.Name);
            cmd.Parameters.AddWithValue("@Type", (int)source.Type);
            cmd.Parameters.AddWithValue("@Url", source.Url);
            cmd.Parameters.AddWithValue("@Username", source.Username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", source.Password ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MacAddress", source.MacAddress ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EpgUrl", source.EpgUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LastSync", source.LastSync.ToString("o"));
            cmd.Parameters.AddWithValue("@IsActive", source.IsActive ? 1 : 0);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeletePlaylistSourceAsync(string sourceId)
        {
            await DeleteChannelsForSourceAsync(sourceId);

            var sql = "DELETE FROM PlaylistSources WHERE Id = @Id";
            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Id", sourceId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Channels
        public async Task<List<Channel>> GetChannelsAsync(string? sourceId = null)
        {
            var channels = new List<Channel>();
            var sql = sourceId != null
                ? "SELECT * FROM Channels WHERE SourceId = @SourceId ORDER BY GroupTitle, SortOrder, Name"
                : "SELECT * FROM Channels ORDER BY GroupTitle, SortOrder, Name";

            using var cmd = new SQLiteCommand(sql, _connection);
            if (sourceId != null)
                cmd.Parameters.AddWithValue("@SourceId", sourceId);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                channels.Add(new Channel
                {
                    Id = reader["Id"].ToString() ?? "",
                    SourceId = reader["SourceId"].ToString() ?? "",
                    Name = reader["Name"].ToString() ?? "",
                    LogoUrl = reader["LogoUrl"]?.ToString(),
                    StreamUrl = reader["StreamUrl"].ToString() ?? "",
                    GroupTitle = reader["GroupTitle"]?.ToString() ?? "Uncategorized",
                    EpgId = reader["EpgId"]?.ToString(),
                    IsFavorite = Convert.ToInt32(reader["IsFavorite"]) == 1,
                    CatchupDays = Convert.ToInt32(reader["CatchupDays"]),
                    Order = Convert.ToInt32(reader["SortOrder"])
                });
            }

            return channels;
        }

        public async Task<List<Channel>> GetFavoriteChannelsAsync()
        {
            var channels = new List<Channel>();
            var sql = "SELECT * FROM Channels WHERE IsFavorite = 1 ORDER BY SortOrder, Name";

            using var cmd = new SQLiteCommand(sql, _connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                channels.Add(new Channel
                {
                    Id = reader["Id"].ToString() ?? "",
                    SourceId = reader["SourceId"].ToString() ?? "",
                    Name = reader["Name"].ToString() ?? "",
                    LogoUrl = reader["LogoUrl"]?.ToString(),
                    StreamUrl = reader["StreamUrl"].ToString() ?? "",
                    GroupTitle = reader["GroupTitle"]?.ToString() ?? "Uncategorized",
                    EpgId = reader["EpgId"]?.ToString(),
                    IsFavorite = true,
                    CatchupDays = Convert.ToInt32(reader["CatchupDays"]),
                    Order = Convert.ToInt32(reader["SortOrder"])
                });
            }

            return channels;
        }

        public async Task SaveChannelsAsync(List<Channel> channels)
        {
            using var transaction = _connection!.BeginTransaction();

            try
            {
                var sql = @"
                    INSERT OR REPLACE INTO Channels
                    (Id, SourceId, Name, LogoUrl, StreamUrl, GroupTitle, EpgId, IsFavorite, CatchupDays, SortOrder)
                    VALUES (@Id, @SourceId, @Name, @LogoUrl, @StreamUrl, @GroupTitle, @EpgId, @IsFavorite, @CatchupDays, @SortOrder)";

                foreach (var channel in channels)
                {
                    using var cmd = new SQLiteCommand(sql, _connection);
                    cmd.Parameters.AddWithValue("@Id", channel.Id);
                    cmd.Parameters.AddWithValue("@SourceId", channel.SourceId);
                    cmd.Parameters.AddWithValue("@Name", channel.Name);
                    cmd.Parameters.AddWithValue("@LogoUrl", channel.LogoUrl ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@StreamUrl", channel.StreamUrl);
                    cmd.Parameters.AddWithValue("@GroupTitle", channel.GroupTitle);
                    cmd.Parameters.AddWithValue("@EpgId", channel.EpgId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsFavorite", channel.IsFavorite ? 1 : 0);
                    cmd.Parameters.AddWithValue("@CatchupDays", channel.CatchupDays);
                    cmd.Parameters.AddWithValue("@SortOrder", channel.Order);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task UpdateChannelFavoriteAsync(string channelId, bool isFavorite)
        {
            var sql = "UPDATE Channels SET IsFavorite = @IsFavorite WHERE Id = @Id";
            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Id", channelId);
            cmd.Parameters.AddWithValue("@IsFavorite", isFavorite ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteChannelsForSourceAsync(string sourceId)
        {
            var sql = "DELETE FROM Channels WHERE SourceId = @SourceId";
            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@SourceId", sourceId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Settings
        public async Task<string?> GetSettingAsync(string key)
        {
            var sql = "SELECT Value FROM Settings WHERE Key = @Key";
            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Key", key);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            var sql = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@Key, @Value)";
            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@Value", value);
            await cmd.ExecuteNonQueryAsync();
        }

        // Recent Channels
        public async Task AddRecentChannelAsync(string channelId)
        {
            var sql = @"
                INSERT INTO RecentChannels (ChannelId, LastWatched, WatchCount)
                VALUES (@ChannelId, @LastWatched, 1)
                ON CONFLICT(ChannelId) DO UPDATE SET
                    LastWatched = @LastWatched,
                    WatchCount = WatchCount + 1";

            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@ChannelId", channelId);
            cmd.Parameters.AddWithValue("@LastWatched", DateTime.Now.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<Channel>> GetRecentChannelsAsync(int limit = 20)
        {
            var channels = new List<Channel>();
            var sql = @"
                SELECT c.* FROM Channels c
                INNER JOIN RecentChannels r ON c.Id = r.ChannelId
                ORDER BY r.LastWatched DESC
                LIMIT @Limit";

            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Limit", limit);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                channels.Add(new Channel
                {
                    Id = reader["Id"].ToString() ?? "",
                    SourceId = reader["SourceId"].ToString() ?? "",
                    Name = reader["Name"].ToString() ?? "",
                    LogoUrl = reader["LogoUrl"]?.ToString(),
                    StreamUrl = reader["StreamUrl"].ToString() ?? "",
                    GroupTitle = reader["GroupTitle"]?.ToString() ?? "Uncategorized",
                    EpgId = reader["EpgId"]?.ToString(),
                    IsFavorite = Convert.ToInt32(reader["IsFavorite"]) == 1,
                    CatchupDays = Convert.ToInt32(reader["CatchupDays"]),
                    Order = Convert.ToInt32(reader["SortOrder"])
                });
            }

            return channels;
        }

        // User Preferences
        public async Task<ContentPreferences?> GetUserPreferencesAsync()
        {
            var sql = "SELECT * FROM UserPreferences LIMIT 1";
            using var cmd = new SQLiteCommand(sql, _connection);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new ContentPreferences
                {
                    Id = reader["Id"].ToString() ?? "",
                    PreferredLanguages = ParseJsonList(reader["PreferredLanguages"]?.ToString()),
                    ShowMovies = Convert.ToInt32(reader["ShowMovies"]) == 1,
                    ShowSeries = Convert.ToInt32(reader["ShowSeries"]) == 1,
                    ShowLiveTV = Convert.ToInt32(reader["ShowLiveTV"]) == 1,
                    ShowAnime = GetInt32Safe(reader, "ShowAnime", 1) == 1,
                    AnimePreferSubbed = GetInt32Safe(reader, "AnimePreferSubbed", 1) == 1,
                    AnimePreferDubbed = GetInt32Safe(reader, "AnimePreferDubbed", 0) == 1,
                    PreferredGenres = ParseJsonList(reader["PreferredGenres"]?.ToString()),
                    ExcludedGenres = ParseJsonList(reader["ExcludedGenres"]?.ToString()),
                    AdultContentEnabled = Convert.ToInt32(reader["AdultContentEnabled"]) == 1,
                    KidsMode = Convert.ToInt32(reader["KidsMode"]) == 1,
                    OnboardingCompleted = Convert.ToInt32(reader["OnboardingCompleted"]) == 1,
                    CreatedAt = DateTime.TryParse(reader["CreatedAt"]?.ToString(), out var created) ? created : DateTime.Now,
                    UpdatedAt = DateTime.TryParse(reader["UpdatedAt"]?.ToString(), out var updated) ? updated : DateTime.Now
                };
            }

            return null;
        }

        public async Task SaveUserPreferencesAsync(ContentPreferences prefs)
        {
            var sql = @"
                INSERT OR REPLACE INTO UserPreferences
                (Id, PreferredLanguages, ShowMovies, ShowSeries, ShowLiveTV, ShowAnime, AnimePreferSubbed, AnimePreferDubbed, PreferredGenres, ExcludedGenres, AdultContentEnabled, KidsMode, OnboardingCompleted, CreatedAt, UpdatedAt)
                VALUES (@Id, @PreferredLanguages, @ShowMovies, @ShowSeries, @ShowLiveTV, @ShowAnime, @AnimePreferSubbed, @AnimePreferDubbed, @PreferredGenres, @ExcludedGenres, @AdultContentEnabled, @KidsMode, @OnboardingCompleted, @CreatedAt, @UpdatedAt)";

            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Id", prefs.Id);
            cmd.Parameters.AddWithValue("@PreferredLanguages", ToJsonList(prefs.PreferredLanguages));
            cmd.Parameters.AddWithValue("@ShowMovies", prefs.ShowMovies ? 1 : 0);
            cmd.Parameters.AddWithValue("@ShowSeries", prefs.ShowSeries ? 1 : 0);
            cmd.Parameters.AddWithValue("@ShowLiveTV", prefs.ShowLiveTV ? 1 : 0);
            cmd.Parameters.AddWithValue("@ShowAnime", prefs.ShowAnime ? 1 : 0);
            cmd.Parameters.AddWithValue("@AnimePreferSubbed", prefs.AnimePreferSubbed ? 1 : 0);
            cmd.Parameters.AddWithValue("@AnimePreferDubbed", prefs.AnimePreferDubbed ? 1 : 0);
            cmd.Parameters.AddWithValue("@PreferredGenres", ToJsonList(prefs.PreferredGenres));
            cmd.Parameters.AddWithValue("@ExcludedGenres", ToJsonList(prefs.ExcludedGenres));
            cmd.Parameters.AddWithValue("@AdultContentEnabled", prefs.AdultContentEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@KidsMode", prefs.KidsMode ? 1 : 0);
            cmd.Parameters.AddWithValue("@OnboardingCompleted", prefs.OnboardingCompleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@CreatedAt", prefs.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@UpdatedAt", prefs.UpdatedAt.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }

        // User Ratings
        public async Task<UserRating?> GetUserRatingAsync(string mediaId)
        {
            var sql = "SELECT * FROM UserRatings WHERE MediaId = @MediaId";
            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@MediaId", mediaId);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new UserRating
                {
                    Id = reader["Id"].ToString() ?? "",
                    MediaId = reader["MediaId"].ToString() ?? "",
                    StarRating = Convert.ToInt32(reader["StarRating"]),
                    QualityRating = reader["QualityRating"]?.ToString() ?? "",
                    RatedAt = DateTime.TryParse(reader["RatedAt"]?.ToString(), out var dt) ? dt : DateTime.Now
                };
            }

            return null;
        }

        public async Task SaveUserRatingAsync(UserRating rating)
        {
            var sql = @"
                INSERT OR REPLACE INTO UserRatings
                (Id, MediaId, StarRating, QualityRating, RatedAt)
                VALUES (@Id, @MediaId, @StarRating, @QualityRating, @RatedAt)";

            using var cmd = new SQLiteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@Id", rating.Id);
            cmd.Parameters.AddWithValue("@MediaId", rating.MediaId);
            cmd.Parameters.AddWithValue("@StarRating", rating.StarRating);
            cmd.Parameters.AddWithValue("@QualityRating", rating.QualityRating ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RatedAt", rating.RatedAt.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }

        private static List<string> ParseJsonList(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new List<string>();
            try
            {
                // Simple parsing for ["item1","item2"] format
                json = json.Trim('[', ']');
                if (string.IsNullOrEmpty(json)) return new List<string>();
                return json.Split(',')
                    .Select(s => s.Trim().Trim('"'))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string ToJsonList(List<string> items)
        {
            if (items == null || items.Count == 0) return "[]";
            return "[" + string.Join(",", items.Select(i => $"\"{i}\"")) + "]";
        }

        private static int GetInt32Safe(System.Data.Common.DbDataReader reader, string columnName, int defaultValue)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal)) return defaultValue;
                return Convert.ToInt32(reader[ordinal]);
            }
            catch
            {
                return defaultValue;
            }
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
