using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using KokonoeAssistant.Services;
using KokonoeAssistant.Windows;
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

            AppSettings settings;
            try
            {
                settings = AppSettings.Load();
                ThemeManager.ApplyTheme(settings.MatrixColor);
            }
            catch (Exception suppressedEx26)
            {
                KokoSystemLog.Write("APP.XAML-CATCH", "OnStartup failed near source line 26: " + suppressedEx26);
                settings = new AppSettings();
            }

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
            OpenInitialWindow(e.Args, settings);
        }

        private void OpenInitialWindow(string[] args, AppSettings settings)
        {
            var decision = KokoUiStartupPolicy.Resolve(
                args,
                Environment.GetEnvironmentVariable(KokoUiStartupPolicy.EnvironmentVariable),
                settings.UiShell);
            KokoSystemLog.Write("UI-STARTUP", $"mode={decision.Mode} source={decision.Source}");

            if (decision.Mode == KokoUiMode.LegacyWpf)
            {
                ShowAsMainWindow(new MainWindow());
                return;
            }

            var shell = new ShellWindow();
            shell.InitializationFailed += error => Dispatcher.Invoke(() => ShowWebShellFailure(error));
            ShowAsMainWindow(shell);
        }

        // Used to silently fall back to the legacy WPF MainWindow, which had its own
        // Settings panel with only 4 hardcoded providers (no ollama-cloud-proxy) - saving
        // through it would silently downgrade the active provider out from under the user.
        // Removed instead of patched: maintaining two independent settings UIs is exactly
        // how that drifted out of sync in the first place.
        private void ShowWebShellFailure(string error)
        {
            KokoSystemLog.Write("UI-STARTUP", "Web shell failed to initialize: " + error);
            WMsgBox.Show(
                "WebView2 не вдалося ініціалізувати.\n\n" +
                "Встанови WebView2 Runtime:\n" +
                "https://go.microsoft.com/fwlink/p/?LinkId=2124703\n\n" +
                "Деталі: " + error + "\n\n" +
                "Застосунок закриється.",
                "Kokonoe — помилка запуску",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }

        private void ShowAsMainWindow(Window window)
        {
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
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
