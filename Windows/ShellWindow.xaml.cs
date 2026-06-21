using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using KokonoeAssistant.Services;

namespace KokonoeAssistant.Windows
{
    public partial class ShellWindow : Window
    {
        private KokoWebBridgeService? _bridge;
        private KokoWebChatBridgeService? _chatBridge;
        private KokoWebAgentBridgeService? _agentBridge;
        private KokoWebVaultBridgeService? _vaultBridge;
        private KokoWebSettingsBridgeService? _settingsBridge;

        public ShellWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += (_, _) =>
            {
                _settingsBridge?.Dispose();
                _vaultBridge?.Dispose();
                _agentBridge?.Dispose();
                _chatBridge?.Dispose();
                _bridge?.Dispose();
                WebView.Dispose();
            };
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
                _bridge = new KokoWebBridgeService(WebView.CoreWebView2);
                _chatBridge = new KokoWebChatBridgeService(
                    _bridge,
                    ServiceContainer.LlmService,
                    text => ServiceContainer.IsInitialized
                        ? ServiceContainer.BrainEngine.BuildUnifiedExternalContext("web", text)
                        : null);
                _agentBridge = new KokoWebAgentBridgeService(_bridge, ServiceContainer.AgentTasks);
                _vaultBridge = new KokoWebVaultBridgeService(_bridge, ServiceContainer.ObsidianMcp);
                _settingsBridge = new KokoWebSettingsBridgeService(
                    _bridge,
                    settings => ThemeManager.ApplyTheme(settings.MatrixColor));
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
