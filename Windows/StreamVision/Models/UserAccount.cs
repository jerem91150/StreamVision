using System;

namespace StreamVision.Models
{
    /// <summary>
    /// Local user account for storing profile and playlist information
    /// </summary>
    public class UserAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Profile info
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        // Playlist configuration (stored securely)
        public string? PlaylistUrl { get; set; }
        public string? XtreamUsername { get; set; }
        public string? XtreamPassword { get; set; }
        public string? XtreamServer { get; set; }
        public PlaylistType PlaylistType { get; set; } = PlaylistType.None;

        // Account status
        public bool IsConfigured { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastLoginAt { get; set; } = DateTime.Now;

        // Helper to check if playlist is configured
        public bool HasPlaylistConfigured => PlaylistType switch
        {
            PlaylistType.M3U => !string.IsNullOrEmpty(PlaylistUrl),
            PlaylistType.Xtream => !string.IsNullOrEmpty(XtreamServer) &&
                                   !string.IsNullOrEmpty(XtreamUsername) &&
                                   !string.IsNullOrEmpty(XtreamPassword),
            _ => false
        };
    }

    public enum PlaylistType
    {
        None = 0,
        M3U = 1,
        Xtream = 2
    }
}
