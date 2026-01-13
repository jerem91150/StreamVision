using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using StreamVision.Services;
using StreamVision.Views;

namespace StreamVision
{
    public partial class App : Application
    {
        private static int _errorCount = 0;
        private const int MaxErrorsBeforeRestart = 5;
        private static readonly object _logLock = new();

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Log startup
            LogInfo("App starting...");

            try
            {
                // Prevent automatic shutdown - require explicit shutdown
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Handle unhandled exceptions - SILENTLY log and continue
                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    var ex = args.ExceptionObject as Exception;
                    LogError("Domain Exception", ex?.ToString() ?? "Unknown error");
                    // Ne pas faire de MessageBox - laisser l'app continuer si possible
                };

                DispatcherUnhandledException += (s, args) =>
                {
                    _errorCount++;
                    LogError($"Dispatcher Exception #{_errorCount}", args.Exception.ToString());

                    // Toujours marquer comme handled pour éviter le crash
                    args.Handled = true;

                    // Si trop d'erreurs, proposer de redémarrer
                    if (_errorCount >= MaxErrorsBeforeRestart)
                    {
                        LogInfo($"Too many errors ({_errorCount}), suggesting restart");
                        var result = MessageBox.Show(
                            "L'application a rencontré plusieurs erreurs. Voulez-vous la redémarrer ?",
                            "StreamVision - Erreurs multiples",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            RestartApplication();
                        }
                        else
                        {
                            _errorCount = 0; // Reset counter
                        }
                    }
                };

                // Catch async exceptions - SILENTLY
                TaskScheduler.UnobservedTaskException += (s, args) =>
                {
                    LogError("Task Exception", args.Exception?.ToString() ?? "Unknown task error");
                    args.SetObserved(); // Empêche le crash
                };

                LogInfo("Exception handlers set up (silent mode)");
                base.OnStartup(e);

                // Check if onboarding is needed
                await CheckAndShowOnboardingAsync();

                LogInfo("OnStartup completed, ShutdownMode=" + ShutdownMode);
            }
            catch (Exception ex)
            {
                LogError("Startup Exception", ex.ToString());
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogInfo($"OnExit called - ExitCode: {e.ApplicationExitCode}");
            base.OnExit(e);
        }

        private async Task CheckAndShowOnboardingAsync()
        {
            try
            {
                var databaseService = new DatabaseService();
                await databaseService.InitializeAsync();

                var preferences = await databaseService.GetUserPreferencesAsync();
                var userAccount = await databaseService.GetUserAccountAsync();

                // Show onboarding if:
                // 1. No preferences exist, or onboarding not completed
                // 2. No user account exists or not configured
                bool needsOnboarding = preferences == null ||
                                       !preferences.OnboardingCompleted ||
                                       userAccount == null ||
                                       !userAccount.IsConfigured;

                if (needsOnboarding)
                {
                    LogInfo("Showing onboarding window");

                    // Hide the auto-started HomeWindow
                    if (MainWindow != null)
                    {
                        MainWindow.Hide();
                    }

                    var onboardingWindow = new OnboardingWindow();
                    var result = onboardingWindow.ShowDialog();

                    if (result == true)
                    {
                        LogInfo("Onboarding completed successfully");
                        // Show the main window after onboarding
                        if (MainWindow != null)
                        {
                            MainWindow.Show();
                        }
                    }
                    else
                    {
                        LogInfo("Onboarding cancelled, shutting down");
                        Shutdown();
                    }
                }
                else
                {
                    LogInfo("Onboarding already completed, showing main window");
                }
            }
            catch (Exception ex)
            {
                LogError("Onboarding check failed", ex.ToString());
                // Continue to main window even if check fails
            }
        }

        private void RestartApplication()
        {
            try
            {
                LogInfo("Restarting application...");
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    System.Diagnostics.Process.Start(exePath);
                }
                Shutdown();
            }
            catch (Exception ex)
            {
                LogError("Restart failed", ex.ToString());
            }
        }

        private void LogInfo(string message)
        {
            try
            {
                lock (_logLock)
                {
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "StreamVision", "startup.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n");
                }
            }
            catch { }
        }

        private void LogError(string type, string message)
        {
            try
            {
                lock (_logLock)
                {
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "StreamVision", "error.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                    // Limiter la taille du message d'erreur
                    var truncatedMessage = message.Length > 2000 ? message.Substring(0, 2000) + "... [truncated]" : message;
                    File.AppendAllText(logPath, $"[{DateTime.Now}] {type}: {truncatedMessage}\n\n");
                }
            }
            catch { }
        }

        /// <summary>
        /// Force garbage collection pour libérer la mémoire
        /// </summary>
        public static void ForceGarbageCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
