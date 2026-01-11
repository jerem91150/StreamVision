using System;
using System.Threading;
using System.Threading.Tasks;

namespace StreamVision.Services
{
    /// <summary>
    /// Sleep timer service - automatically stops playback after specified time
    /// </summary>
    public class SleepTimerService : IDisposable
    {
        private CancellationTokenSource? _timerCts;
        private DateTime? _endTime;
        private TimeSpan _duration;

        // Events
        public event Action? OnTimerStarted;
        public event Action<TimeSpan>? OnTimerTick;
        public event Action? OnTimerExpired;
        public event Action? OnTimerCancelled;
        public event Action<int>? OnFinalCountdown; // Last 60 seconds warning

        public bool IsActive => _endTime.HasValue && _endTime > DateTime.Now;
        public TimeSpan? TimeRemaining => _endTime.HasValue ? _endTime.Value - DateTime.Now : null;
        public DateTime? EndTime => _endTime;

        /// <summary>
        /// Start the sleep timer
        /// </summary>
        /// <param name="duration">How long until sleep</param>
        public async Task StartTimerAsync(TimeSpan duration)
        {
            // Cancel any existing timer
            Cancel();

            _duration = duration;
            _endTime = DateTime.Now + duration;
            _timerCts = new CancellationTokenSource();

            LogTimer($"Sleep timer started: {duration.TotalMinutes} minutes");
            OnTimerStarted?.Invoke();

            try
            {
                var remaining = duration;
                var finalCountdownStarted = false;

                while (remaining.TotalSeconds > 0 && !_timerCts.Token.IsCancellationRequested)
                {
                    // Update every second
                    await Task.Delay(1000, _timerCts.Token);
                    remaining = _endTime.Value - DateTime.Now;

                    OnTimerTick?.Invoke(remaining);

                    // Final countdown (last 60 seconds)
                    if (remaining.TotalSeconds <= 60 && remaining.TotalSeconds > 0)
                    {
                        if (!finalCountdownStarted)
                        {
                            finalCountdownStarted = true;
                            LogTimer("Final countdown started");
                        }
                        OnFinalCountdown?.Invoke((int)remaining.TotalSeconds);
                    }
                }

                if (!_timerCts.Token.IsCancellationRequested)
                {
                    LogTimer("Sleep timer expired");
                    OnTimerExpired?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                LogTimer("Sleep timer cancelled");
            }
            finally
            {
                _endTime = null;
            }
        }

        /// <summary>
        /// Start timer with preset minutes
        /// </summary>
        public Task StartTimerAsync(int minutes)
        {
            return StartTimerAsync(TimeSpan.FromMinutes(minutes));
        }

        /// <summary>
        /// Add time to current timer
        /// </summary>
        public void AddTime(TimeSpan additional)
        {
            if (_endTime.HasValue)
            {
                _endTime = _endTime.Value + additional;
                LogTimer($"Added {additional.TotalMinutes} minutes to timer");
            }
        }

        /// <summary>
        /// Cancel the timer
        /// </summary>
        public void Cancel()
        {
            if (_timerCts != null && !_timerCts.IsCancellationRequested)
            {
                _timerCts.Cancel();
                _timerCts.Dispose();
                _timerCts = null;
                _endTime = null;
                OnTimerCancelled?.Invoke();
            }
        }

        /// <summary>
        /// Get formatted time remaining
        /// </summary>
        public string GetTimeRemainingDisplay()
        {
            if (!TimeRemaining.HasValue) return "";

            var ts = TimeRemaining.Value;
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}min";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}min {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        private static void LogTimer(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "timer.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        public void Dispose()
        {
            Cancel();
        }
    }

    /// <summary>
    /// Preset sleep timer options
    /// </summary>
    public static class SleepTimerPresets
    {
        public static readonly (string Label, int Minutes)[] Presets = new[]
        {
            ("15 minutes", 15),
            ("30 minutes", 30),
            ("45 minutes", 45),
            ("1 heure", 60),
            ("1h30", 90),
            ("2 heures", 120),
            ("Fin de l'épisode", -1), // Special: end of current media
            ("Fin du film", -2)       // Special: end of current media
        };
    }
}
