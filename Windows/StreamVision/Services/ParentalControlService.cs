using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using StreamVision.Models;
using StreamVision.Views;

namespace StreamVision.Services
{
    public class ParentalControlService
    {
        private bool _isEnabled;
        private string? _pin;
        private bool _blockAdult;
        private bool _blockViolence;
        private DateTime _lastAuthenticated = DateTime.MinValue;
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

        // Keywords that indicate adult content
        private static readonly string[] AdultKeywords = new[]
        {
            "adult", "xxx", "18+", "+18", "x-rated", "xrated", "erotic",
            "adulte", "adultes", "porno", "porn", "sexy", "sexe", "sex"
        };

        // Keywords that indicate violence content
        private static readonly string[] ViolenceKeywords = new[]
        {
            "violence", "violent", "gore", "horror", "horreur", "sanglant"
        };

        // Categories that are typically adult content
        private static readonly string[] AdultCategories = new[]
        {
            "adult", "xxx", "adults", "adulte", "adultes", "18+", "+18",
            "for adults", "mature", "erotic"
        };

        public async Task LoadSettingsAsync()
        {
            using var db = new DatabaseService();
            await db.InitializeAsync();

            var enabled = await db.GetSettingAsync("ParentalControlEnabled");
            var pin = await db.GetSettingAsync("ParentalControlPin");
            var blockAdult = await db.GetSettingAsync("BlockAdultContent");
            var blockViolence = await db.GetSettingAsync("BlockViolenceContent");

            _isEnabled = enabled == "true";
            _pin = pin;
            _blockAdult = blockAdult == "true";
            _blockViolence = blockViolence == "true";
        }

        public bool IsEnabled => _isEnabled && !string.IsNullOrEmpty(_pin);

        public bool IsContentBlocked(MediaItem item)
        {
            if (!IsEnabled) return false;

            // Check if session is still valid
            if (DateTime.Now - _lastAuthenticated < _sessionTimeout)
            {
                return false; // User recently authenticated
            }

            return IsAdultContent(item) || IsViolenceContent(item);
        }

        public bool IsContentBlocked(Channel channel)
        {
            if (!IsEnabled) return false;

            // Check if session is still valid
            if (DateTime.Now - _lastAuthenticated < _sessionTimeout)
            {
                return false;
            }

            return IsAdultContent(channel) || IsViolenceContent(channel);
        }

        private bool IsAdultContent(MediaItem item)
        {
            if (!_blockAdult) return false;

            var name = item.Name?.ToLowerInvariant() ?? "";
            var group = item.GroupTitle?.ToLowerInvariant() ?? "";

            // Check category/group first (most reliable)
            if (AdultCategories.Any(c => group.Contains(c)))
                return true;

            // Check name for keywords
            if (AdultKeywords.Any(k => name.Contains(k)))
                return true;

            return false;
        }

        private bool IsAdultContent(Channel channel)
        {
            if (!_blockAdult) return false;

            var name = channel.Name?.ToLowerInvariant() ?? "";
            var group = channel.GroupTitle?.ToLowerInvariant() ?? "";

            // Check category
            if (AdultCategories.Any(c => group.Contains(c)))
                return true;

            // Check name for keywords
            if (AdultKeywords.Any(k => name.Contains(k)))
                return true;

            return false;
        }

        private bool IsViolenceContent(MediaItem item)
        {
            if (!_blockViolence) return false;

            var name = item.Name?.ToLowerInvariant() ?? "";
            var group = item.GroupTitle?.ToLowerInvariant() ?? "";

            return ViolenceKeywords.Any(k => name.Contains(k) || group.Contains(k));
        }

        private bool IsViolenceContent(Channel channel)
        {
            if (!_blockViolence) return false;

            var name = channel.Name?.ToLowerInvariant() ?? "";
            var group = channel.GroupTitle?.ToLowerInvariant() ?? "";

            return ViolenceKeywords.Any(k => name.Contains(k) || group.Contains(k));
        }

        /// <summary>
        /// Shows PIN dialog and returns true if user authenticated successfully
        /// </summary>
        public bool RequestAccess(Window owner)
        {
            if (!IsEnabled || string.IsNullOrEmpty(_pin))
            {
                return true;
            }

            var pinDialog = new PinDialog(_pin);
            pinDialog.Owner = owner;

            if (pinDialog.ShowDialog() == true && pinDialog.IsAuthenticated)
            {
                _lastAuthenticated = DateTime.Now;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if content is blocked and if so, requests PIN. Returns true if access is granted.
        /// </summary>
        public bool CheckAndRequestAccess(MediaItem item, Window owner)
        {
            if (!IsContentBlocked(item))
            {
                return true;
            }

            return RequestAccess(owner);
        }

        /// <summary>
        /// Checks if content is blocked and if so, requests PIN. Returns true if access is granted.
        /// </summary>
        public bool CheckAndRequestAccess(Channel channel, Window owner)
        {
            if (!IsContentBlocked(channel))
            {
                return true;
            }

            return RequestAccess(owner);
        }

        /// <summary>
        /// Clears the authentication session
        /// </summary>
        public void ClearSession()
        {
            _lastAuthenticated = DateTime.MinValue;
        }
    }
}
