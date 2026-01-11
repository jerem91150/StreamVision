using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// EPG reminder service - notifies user before favorite programs start
    /// </summary>
    public class EpgReminderService : IDisposable
    {
        private readonly List<ProgramReminder> _reminders = new();
        private readonly object _lock = new();
        private CancellationTokenSource? _checkCts;
        private bool _isRunning;

        // Settings
        private int _defaultReminderMinutes = 5;

        // Events
        public event Action<ProgramReminder>? OnReminderTriggered;
        public event Action<ProgramReminder>? OnReminderAdded;
        public event Action<ProgramReminder>? OnReminderRemoved;
        public event Action<string>? OnStatusChanged;

        public IReadOnlyList<ProgramReminder> Reminders => _reminders.AsReadOnly();
        public int DefaultReminderMinutes
        {
            get => _defaultReminderMinutes;
            set => _defaultReminderMinutes = Math.Clamp(value, 1, 60);
        }

        /// <summary>
        /// Start the reminder service
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _checkCts = new CancellationTokenSource();
            _ = CheckRemindersLoopAsync(_checkCts.Token);
            LogReminder("Reminder service started");
        }

        /// <summary>
        /// Stop the reminder service
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _checkCts?.Cancel();
            _checkCts?.Dispose();
            _checkCts = null;
            LogReminder("Reminder service stopped");
        }

        /// <summary>
        /// Add a reminder for a program
        /// </summary>
        public ProgramReminder AddReminder(EpgProgram program, MediaItem channel, int minutesBefore = -1)
        {
            if (minutesBefore < 0)
                minutesBefore = _defaultReminderMinutes;

            var reminder = new ProgramReminder
            {
                Id = Guid.NewGuid().ToString(),
                Program = program,
                Channel = channel,
                ReminderTime = program.StartTime.AddMinutes(-minutesBefore),
                MinutesBefore = minutesBefore,
                IsTriggered = false,
                CreatedAt = DateTime.Now
            };

            lock (_lock)
            {
                // Check for duplicate
                var existing = _reminders.FirstOrDefault(r =>
                    r.Program.Title == program.Title &&
                    r.Program.StartTime == program.StartTime &&
                    r.Channel.Id == channel.Id);

                if (existing != null)
                {
                    return existing;
                }

                _reminders.Add(reminder);
            }

            OnReminderAdded?.Invoke(reminder);
            LogReminder($"Added reminder: {program.Title} on {channel.Name} at {program.StartTime}");
            OnStatusChanged?.Invoke($"Rappel ajouté: {program.Title}");

            return reminder;
        }

        /// <summary>
        /// Remove a reminder
        /// </summary>
        public bool RemoveReminder(string reminderId)
        {
            ProgramReminder? reminder;
            lock (_lock)
            {
                reminder = _reminders.FirstOrDefault(r => r.Id == reminderId);
                if (reminder != null)
                {
                    _reminders.Remove(reminder);
                }
            }

            if (reminder != null)
            {
                OnReminderRemoved?.Invoke(reminder);
                LogReminder($"Removed reminder: {reminder.Program.Title}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove all reminders for a channel
        /// </summary>
        public void RemoveRemindersForChannel(string channelId)
        {
            List<ProgramReminder> toRemove;
            lock (_lock)
            {
                toRemove = _reminders.Where(r => r.Channel.Id == channelId).ToList();
                foreach (var r in toRemove)
                {
                    _reminders.Remove(r);
                    OnReminderRemoved?.Invoke(r);
                }
            }

            LogReminder($"Removed {toRemove.Count} reminders for channel {channelId}");
        }

        /// <summary>
        /// Check if a program has a reminder set
        /// </summary>
        public bool HasReminder(EpgProgram program, string channelId)
        {
            lock (_lock)
            {
                return _reminders.Any(r =>
                    r.Program.Title == program.Title &&
                    r.Program.StartTime == program.StartTime &&
                    r.Channel.Id == channelId);
            }
        }

        /// <summary>
        /// Get upcoming reminders (next 24 hours)
        /// </summary>
        public List<ProgramReminder> GetUpcomingReminders()
        {
            var cutoff = DateTime.Now.AddHours(24);
            lock (_lock)
            {
                return _reminders
                    .Where(r => !r.IsTriggered && r.ReminderTime <= cutoff)
                    .OrderBy(r => r.ReminderTime)
                    .ToList();
            }
        }

        /// <summary>
        /// Clear all reminders
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                _reminders.Clear();
            }
            OnStatusChanged?.Invoke("Tous les rappels supprimés");
        }

        #region Private Methods

        private async Task CheckRemindersLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    await Task.Delay(30000, ct); // Check every 30 seconds

                    var now = DateTime.Now;
                    List<ProgramReminder> toTrigger;

                    lock (_lock)
                    {
                        toTrigger = _reminders
                            .Where(r => !r.IsTriggered && r.ReminderTime <= now)
                            .ToList();
                    }

                    foreach (var reminder in toTrigger)
                    {
                        reminder.IsTriggered = true;
                        OnReminderTriggered?.Invoke(reminder);
                        LogReminder($"Triggered reminder: {reminder.Program.Title}");
                    }

                    // Clean up old triggered reminders (programs that already ended)
                    CleanupOldReminders();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogReminder($"Error in reminder loop: {ex.Message}");
                }
            }
        }

        private void CleanupOldReminders()
        {
            var now = DateTime.Now;
            lock (_lock)
            {
                _reminders.RemoveAll(r =>
                    r.IsTriggered &&
                    r.Program.EndTime < now);
            }
        }

        private static void LogReminder(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "reminders.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #endregion

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// A program reminder
    /// </summary>
    public class ProgramReminder
    {
        public string Id { get; set; } = "";
        public EpgProgram Program { get; set; } = null!;
        public MediaItem Channel { get; set; } = null!;
        public DateTime ReminderTime { get; set; }
        public int MinutesBefore { get; set; }
        public bool IsTriggered { get; set; }
        public DateTime CreatedAt { get; set; }

        public string DisplayText => $"{Program.Title} sur {Channel.Name}";
        public string TimeDisplay => $"Dans {MinutesBefore} min - {Program.StartTime:HH:mm}";
    }
}
