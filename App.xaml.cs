using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WinApp    = System.Windows.Application;
using WMsgBox   = System.Windows.MessageBox;

namespace KokonoeAssistant
{
    public partial class App : WinApp
    {
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
            catch { }

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
        }

        private static void LogCrash(string msg)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{msg}\n\n");
            }
            catch { }
        }
    }
}
