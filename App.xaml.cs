using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using KokonoeAssistant.Services;
using WinApp    = System.Windows.Application;
using WMsgBox   = System.Windows.MessageBox;

namespace KokonoeAssistant
{
    public partial class App : WinApp
    {
        private static DateTime _lastUiHeartbeat = DateTime.UtcNow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // base MUST run first so App.xaml BAML resources are loaded
            // before ThemeManager overwrites them — otherwise WPF throws
            // "Cannot re-initialize ResourceDictionary instance"
            base.OnStartup(e);

            try
            {
                var settings = AppSettings.Load();
                ThemeManager.ApplyTheme(settings.MatrixColor);
            }
            catch (Exception suppressedEx26) { KokoSystemLog.Write("APP.XAML-CATCH", "OnStartup failed near source line 26: " + suppressedEx26); }

            // Global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var msg = ex.ExceptionObject?.ToString() ?? "Unknown error";
                LogCrash(msg);
                WMsgBox.Show(msg, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                // Dig to the root cause
                var e = ex.Exception;
                var depth = 0;
                while (e.InnerException != null && depth++ < 10) e = e.InnerException;

                var msg = $"ROOT: {e.GetType().Name}\n{e.Message}\n\n--- Full ---\n{ex.Exception}";
                LogCrash(msg);
                WMsgBox.Show(msg, "UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                LogCrash(ex.Exception.ToString());
                ex.SetObserved();
            };

            StartUiWatchdog();
        }

        private void StartUiWatchdog()
        {
            _lastUiHeartbeat = DateTime.UtcNow;
            var startedAt = DateTime.UtcNow;
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (_, _) => _lastUiHeartbeat = DateTime.UtcNow;
            timer.Start();

            _ = Task.Run(async () =>
            {
                var lastLogged = DateTime.MinValue;
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    if (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(20))
                        continue;

                    var lag = DateTime.UtcNow - _lastUiHeartbeat;
                    if (lag < TimeSpan.FromSeconds(8))
                        continue;
                    if (DateTime.UtcNow - lastLogged < TimeSpan.FromSeconds(30))
                        continue;

                    lastLogged = DateTime.UtcNow;
                    LogCrash($"UI hang suspected. Dispatcher heartbeat lag: {lag.TotalSeconds:F1}s");
                }
            });
        }

        private static void LogCrash(string msg)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{msg}\n\n");
            }
            catch (Exception suppressedEx97) { KokoSystemLog.Write("APP.XAML-CATCH", "LogCrash failed near source line 97: " + suppressedEx97); }
        }
    }
}
