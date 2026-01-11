using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ImportTool;

/// <summary>
/// Script d'importation IPTV - Récupère tout le contenu du serveur Xtream Codes
/// </summary>
public class Program
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private const string SERVER_URL = "http://yuybjsdw.mexamo.xyz";
    private const string USERNAME = "VRFZVGDH";
    private const string PASSWORD = "LQRCYT89";

    static Program()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔═══════════════════════════════════════════════╗");
        Console.WriteLine("║     StreamVision IPTV Import Tool             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════╝\n");

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StreamVision", "streamvision.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        Console.WriteLine($"📁 Database: {dbPath}\n");

        var connectionString = $"Data Source={dbPath};Version=3;";

        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync();

        // Créer les tables
        await CreateTablesAsync(connection);

        // Tester l'authentification
        Console.WriteLine("1️⃣  Authentification...");
        var authResult = await AuthenticateAsync();
        if (authResult == null)
        {
            Console.WriteLine("❌ Échec de l'authentification");
            return;
        }
        Console.WriteLine($"   ✅ Connecté - Status: {authResult["user_info"]?["status"]} - Expire: {GetExpiryDate(authResult)}");

        // Créer la source
        var sourceId = Guid.NewGuid().ToString();
        await SavePlaylistSourceAsync(connection, sourceId, "IPTV Mexamo", SERVER_URL, USERNAME, PASSWORD);
        Console.WriteLine($"   ✅ Source créée\n");

        // Récupérer les chaînes Live
        Console.WriteLine("2️⃣  Récupération des chaînes Live...");
        var liveChannels = await GetAllLiveStreamsAsync(sourceId);
        Console.WriteLine($"   📺 Trouvé: {liveChannels.Count} chaînes");
        await SaveChannelsAsync(connection, liveChannels);
        Console.WriteLine($"   ✅ {liveChannels.Count} chaînes Live importées\n");

        // Récupérer les VOD
        Console.WriteLine("3️⃣  Récupération des films VOD...");
        var vodChannels = await GetAllVodStreamsAsync(sourceId);
        Console.WriteLine($"   🎬 Trouvé: {vodChannels.Count} films");
        await SaveChannelsAsync(connection, vodChannels);
        Console.WriteLine($"   ✅ {vodChannels.Count} films VOD importés\n");

        // Récupérer les séries
        Console.WriteLine("4️⃣  Récupération des séries...");
        var seriesChannels = await GetAllSeriesAsync(sourceId);
        Console.WriteLine($"   📺 Trouvé: {seriesChannels.Count} séries");
        await SaveChannelsAsync(connection, seriesChannels);
        Console.WriteLine($"   ✅ {seriesChannels.Count} séries importées\n");

        // Résumé
        var totalCount = liveChannels.Count + vodChannels.Count + seriesChannels.Count;
        Console.WriteLine("╔═══════════════════════════════════════════════╗");
        Console.WriteLine("║              IMPORT TERMINÉ                   ║");
        Console.WriteLine("╠═══════════════════════════════════════════════╣");
        Console.WriteLine($"║  Total: {totalCount,6} éléments importés           ║");
        Console.WriteLine($"║  - Chaînes Live: {liveChannels.Count,6}                    ║");
        Console.WriteLine($"║  - Films VOD:    {vodChannels.Count,6}                    ║");
        Console.WriteLine($"║  - Séries:       {seriesChannels.Count,6}                    ║");
        Console.WriteLine("╚═══════════════════════════════════════════════╝");
    }

    private static string GetExpiryDate(JObject auth)
    {
        var expDate = auth["user_info"]?["exp_date"]?.ToString();
        if (long.TryParse(expDate, out var timestamp))
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime.ToString("g");
        }
        return "N/A";
    }

    private static async Task CreateTablesAsync(SQLiteConnection connection)
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
        ";

        using var cmd = new SQLiteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<JObject?> AuthenticateAsync()
    {
        try
        {
            var url = $"{SERVER_URL}/player_api.php?username={USERNAME}&password={PASSWORD}";
            var response = await _httpClient.GetStringAsync(url);
            return JObject.Parse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   Erreur auth: {ex.Message}");
            return null;
        }
    }

    private static async Task SavePlaylistSourceAsync(SQLiteConnection connection, string id, string name, string url, string username, string password)
    {
        // Supprimer l'ancienne source si elle existe
        var deleteSql = "DELETE FROM PlaylistSources WHERE Url = @Url AND Username = @Username";
        using (var deleteCmd = new SQLiteCommand(deleteSql, connection))
        {
            deleteCmd.Parameters.AddWithValue("@Url", url);
            deleteCmd.Parameters.AddWithValue("@Username", username);
            await deleteCmd.ExecuteNonQueryAsync();
        }

        // Supprimer les anciennes chaînes
        var deleteChannelsSql = "DELETE FROM Channels WHERE SourceId IN (SELECT Id FROM PlaylistSources WHERE Url = @Url)";
        using (var deleteCmd = new SQLiteCommand(deleteChannelsSql, connection))
        {
            deleteCmd.Parameters.AddWithValue("@Url", url);
            await deleteCmd.ExecuteNonQueryAsync();
        }

        var sql = @"INSERT INTO PlaylistSources (Id, Name, Type, Url, Username, Password, LastSync, IsActive)
                    VALUES (@Id, @Name, 1, @Url, @Username, @Password, @LastSync, 1)";

        using var cmd = new SQLiteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@Url", url);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@Password", password);
        cmd.Parameters.AddWithValue("@LastSync", DateTime.Now.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<ChannelData>> GetAllLiveStreamsAsync(string sourceId)
    {
        var channels = new List<ChannelData>();

        try
        {
            // Récupérer les catégories
            Console.WriteLine("   📂 Chargement des catégories Live...");
            var catUrl = $"{SERVER_URL}/player_api.php?username={USERNAME}&password={PASSWORD}&action=get_live_categories";
            var catResponse = await _httpClient.GetStringAsync(catUrl);
            var categories = JArray.Parse(catResponse);
            var categoryDict = new Dictionary<string, string>();

            foreach (var cat in categories)
            {
                var catId = cat["category_id"]?.ToString() ?? "";
                var catName = cat["category_name"]?.ToString() ?? "";
                categoryDict[catId] = catName;
            }
            Console.WriteLine($"   📂 {categories.Count} catégories chargées");

            // Récupérer les chaînes
            Console.WriteLine("   📡 Chargement des chaînes...");
            var url = $"{SERVER_URL}/player_api.php?username={USERNAME}&password={PASSWORD}&action=get_live_streams";
            var response = await _httpClient.GetStringAsync(url);
            var streams = JArray.Parse(response);

            int order = 0;
            foreach (var stream in streams)
            {
                var streamId = stream["stream_id"]?.ToString() ?? "";
                var catId = stream["category_id"]?.ToString() ?? "";
                var catchupDays = int.TryParse(stream["tv_archive_duration"]?.ToString(), out var days) ? days : 0;

                channels.Add(new ChannelData
                {
                    Id = $"live_{streamId}",
                    SourceId = sourceId,
                    Name = stream["name"]?.ToString() ?? "",
                    LogoUrl = stream["stream_icon"]?.ToString(),
                    StreamUrl = $"{SERVER_URL}/live/{USERNAME}/{PASSWORD}/{streamId}.m3u8",
                    GroupTitle = categoryDict.GetValueOrDefault(catId, "Live"),
                    EpgId = stream["epg_channel_id"]?.ToString(),
                    CatchupDays = catchupDays,
                    Order = order++
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erreur Live: {ex.Message}");
        }

        return channels;
    }

    private static async Task<List<ChannelData>> GetAllVodStreamsAsync(string sourceId)
    {
        var channels = new List<ChannelData>();

        try
        {
            // Récupérer les catégories
            Console.WriteLine("   📂 Chargement des catégories VOD...");
            var catUrl = $"{SERVER_URL}/player_api.php?username={USERNAME}&password={PASSWORD}&action=get_vod_categories";
            var catResponse = await _httpClient.GetStringAsync(catUrl);
            var categories = JArray.Parse(catResponse);
            var categoryDict = new Dictionary<string, string>();

            foreach (var cat in categories)
            {
                var catId = cat["category_id"]?.ToString() ?? "";
                var catName = cat["category_name"]?.ToString() ?? "";
                categoryDict[catId] = catName;
            }
            Console.WriteLine($"   📂 {categories.Count} catégories chargées");

            // Récupérer les films
            Console.WriteLine("   🎬 Chargement des films...");
            var url = $"{SERVER_URL}/player_api.php?username={USERNAME}&password={PASSWORD}&action=get_vod_streams";
            var response = await _httpClient.GetStringAsync(url);
            var streams = JArray.Parse(response);

            int order = 0;
            foreach (var stream in streams)
            {
                var streamId = stream["stream_id"]?.ToString() ?? "";
                var extension = stream["container_extension"]?.ToString() ?? "mp4";
                var catId = stream["category_id"]?.ToString() ?? "";

                channels.Add(new ChannelData
                {
                    Id = $"vod_{streamId}",
                    SourceId = sourceId,
                    Name = stream["name"]?.ToString() ?? "",
                    LogoUrl = stream["stream_icon"]?.ToString(),
                    StreamUrl = $"{SERVER_URL}/movie/{USERNAME}/{PASSWORD}/{streamId}.{extension}",
                    GroupTitle = categoryDict.GetValueOrDefault(catId, "Films"),
                    Order = order++
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erreur VOD: {ex.Message}");
        }

        return channels;
    }

    private static async Task<List<ChannelData>> GetAllSeriesAsync(string sourceId)
    {
        var channels = new List<ChannelData>();

        try
        {
            // Récupérer les catégories
            Console.WriteLine("   📂 Chargement des catégories Séries...");
            var catUrl = $"{SERVER_URL}/player_api.php?username={USERNAME}&password={PASSWORD}&action=get_series_categories";
            var catResponse = await _httpClient.GetStringAsync(catUrl);
            var categories = JArray.Parse(catResponse);
            var categoryDict = new Dictionary<string, string>();

            foreach (var cat in categories)
            {
                var catId = cat["category_id"]?.ToString() ?? "";
                var catName = cat["category_name"]?.ToString() ?? "";
                categoryDict[catId] = catName;
            }
            Console.WriteLine($"   📂 {categories.Count} catégories chargées");

            // Récupérer les séries
            Console.WriteLine("   📺 Chargement des séries...");
            var url = $"{SERVER_URL}/player_api.php?username={USERNAME}&password={PASSWORD}&action=get_series";
            var response = await _httpClient.GetStringAsync(url);
            var streams = JArray.Parse(response);

            int order = 0;
            foreach (var stream in streams)
            {
                var seriesId = stream["series_id"]?.ToString() ?? "";
                var catId = stream["category_id"]?.ToString() ?? "";

                channels.Add(new ChannelData
                {
                    Id = $"series_{seriesId}",
                    SourceId = sourceId,
                    Name = stream["name"]?.ToString() ?? "",
                    LogoUrl = stream["cover"]?.ToString(),
                    StreamUrl = $"series://{seriesId}", // Placeholder - les épisodes sont chargés séparément
                    GroupTitle = categoryDict.GetValueOrDefault(catId, "Séries"),
                    Order = order++
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erreur Séries: {ex.Message}");
        }

        return channels;
    }

    private static async Task SaveChannelsAsync(SQLiteConnection connection, List<ChannelData> channels)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            var sql = @"INSERT OR REPLACE INTO Channels
                (Id, SourceId, Name, LogoUrl, StreamUrl, GroupTitle, EpgId, IsFavorite, CatchupDays, SortOrder)
                VALUES (@Id, @SourceId, @Name, @LogoUrl, @StreamUrl, @GroupTitle, @EpgId, 0, @CatchupDays, @SortOrder)";

            foreach (var channel in channels)
            {
                using var cmd = new SQLiteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", channel.Id);
                cmd.Parameters.AddWithValue("@SourceId", channel.SourceId);
                cmd.Parameters.AddWithValue("@Name", channel.Name);
                cmd.Parameters.AddWithValue("@LogoUrl", channel.LogoUrl ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@StreamUrl", channel.StreamUrl);
                cmd.Parameters.AddWithValue("@GroupTitle", channel.GroupTitle);
                cmd.Parameters.AddWithValue("@EpgId", channel.EpgId ?? (object)DBNull.Value);
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

    private class ChannelData
    {
        public string Id { get; set; } = "";
        public string SourceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? LogoUrl { get; set; }
        public string StreamUrl { get; set; } = "";
        public string GroupTitle { get; set; } = "";
        public string? EpgId { get; set; }
        public int CatchupDays { get; set; }
        public int Order { get; set; }
    }
}
