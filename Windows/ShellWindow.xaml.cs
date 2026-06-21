using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using KokonoeAssistant.Services;

namespace KokonoeAssistant.Windows
{
    public partial class ShellWindow : Window
    {
        public ShellWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += (_, _) => WebView.Dispose();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            try
            {
                var indexPath = ResolveIndexPath();
                if (!File.Exists(indexPath))
                    throw new FileNotFoundException("Web shell entry point was not copied to the output directory.", indexPath);

                await WebView.EnsureCoreWebView2Async();
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                WebView.NavigationCompleted += (_, args) =>
                {
                    var state = args.IsSuccess ? "ready" : $"failed:{args.WebErrorStatus}";
                    KokoSystemLog.Write("WEB-SHELL", $"navigation {state} source={WebView.Source}");
                };
                WebView.Source = new Uri(indexPath, UriKind.Absolute);
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("WEB-SHELL", "initialization failed: " + ex);
                Debug.WriteLine("[WEB-SHELL] " + ex);
                try
                {
                    WebView.NavigateToString(BuildFailurePage(ex.Message));
                }
                catch (Exception fallbackEx)
                {
                    KokoSystemLog.Write("WEB-SHELL", "failure page unavailable: " + fallbackEx.Message);
                }
            }
        }

        internal static string ResolveIndexPath()
            => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "frontend", "index.html"));

        private static string BuildFailurePage(string message)
        {
            var safe = System.Net.WebUtility.HtmlEncode(message);
            return $"""
                <!doctype html><html><body style="margin:0;background:#080b10;color:#d9e2ea;font:14px Segoe UI;padding:32px">
                <strong>Web shell failed to initialize.</strong><p style="color:#d96570">{safe}</p></body></html>
                """;
        }
    }
}
