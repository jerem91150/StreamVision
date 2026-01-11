using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace StreamVision
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Log startup
            LogInfo("App starting...");

            try
            {
                // Prevent automatic shutdown - require explicit shutdown
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Handle unhandled exceptions
                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    var ex = args.ExceptionObject as Exception;
                    LogError("Domain Exception", ex?.ToString() ?? "Unknown error");
                };

                DispatcherUnhandledException += (s, args) =>
                {
                    LogError("Dispatcher Exception", args.Exception.ToString());
                    args.Handled = true;
                };

                // Catch async exceptions
                TaskScheduler.UnobservedTaskException += (s, args) =>
                {
                    LogError("Task Exception", args.Exception?.ToString() ?? "Unknown task error");
                    args.SetObserved();
                };

                LogInfo("Exception handlers set up");
                base.OnStartup(e);
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

        private void LogInfo(string message)
        {
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "startup.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n");
            }
            catch { }
        }

        private void LogError(string type, string message)
        {
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now}] {type}: {message}\n\n");
                MessageBox.Show($"{type}: {message}", "StreamVision Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }
}
