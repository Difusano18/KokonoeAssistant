using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using KokonoeAssistant.Services;
using Microsoft.Web.WebView2.Core;

namespace KokonoeAssistant.Windows
{
    public partial class ShellWindow : Window
    {
        private KokoWebBridgeService? _bridge;
        private KokoWebChatBridgeService? _chatBridge;
        private KokoWebAgentBridgeService? _agentBridge;
        private KokoWebVaultBridgeService? _vaultBridge;
        private KokoWebMemoryBridgeService? _memoryBridge;
        private KokoWebSettingsBridgeService? _settingsBridge;
        private KokoWebTelegramBridgeService? _telegramBridge;
        private KokoWebRuntimeBridgeService? _runtimeBridge;
        private KokoWebSystemBridgeService? _systemBridge;
        private Action<string, string>? _brainMessageHandler;
        private bool _preserveServicesOnClose;

        public event Action<string>? InitializationFailed;

        public ShellWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += (_, _) =>
            {
                try
                {
                    if (_brainMessageHandler != null && ServiceContainer.IsInitialized &&
                        ServiceContainer.BrainEngine.OnNewMessage == _brainMessageHandler)
                        ServiceContainer.BrainEngine.OnNewMessage = null;
                }
                catch (Exception ex) { KokoSystemLog.Write("WEB-SHELL", "brain callback cleanup failed: " + ex.Message); }
                _telegramBridge?.Dispose();
                _runtimeBridge?.Dispose();
                _systemBridge?.Dispose();
                _settingsBridge?.Dispose();
                _memoryBridge?.Dispose();
                _vaultBridge?.Dispose();
                _agentBridge?.Dispose();
                _chatBridge?.Dispose();
                _bridge?.Dispose();
                WebView.Dispose();
                if (!_preserveServicesOnClose)
                {
                    try { ServiceContainer.BrainEngine.RecordClose(); } catch (Exception ex) { KokoSystemLog.Write("WEB-SHELL", "record close failed: " + ex.Message); }
                    ServiceContainer.Disposing();
                }
            };
        }

        public void TransferServiceLifetimeToFallback()
            => _preserveServicesOnClose = true;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            try
            {
                var settings = AppSettings.Load();
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(settings.VaultPath);
                    ServiceContainer.Initialize(settings.VaultPath);
                    _ = ServiceContainer.BrainEngine;
                });

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
                _memoryBridge = new KokoWebMemoryBridgeService(_bridge, ServiceContainer.KokoMemory);
                _settingsBridge = new KokoWebSettingsBridgeService(
                    _bridge,
                    settings => ThemeManager.ApplyTheme(settings.MatrixColor));
                _telegramBridge = new KokoWebTelegramBridgeService(_bridge, ServiceContainer.TelegramStatus);
                _runtimeBridge = new KokoWebRuntimeBridgeService(_bridge);
                _systemBridge = new KokoWebSystemBridgeService(_bridge, ServiceContainer.SystemOverlord);
                _brainMessageHandler = (role, content) => _chatBridge?.PublishExternalMessage(role, content);
                ServiceContainer.BrainEngine.OnNewMessage = _brainMessageHandler;
                _ = Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine.InitVault(); }
                    catch (Exception ex) { KokoSystemLog.Write("WEB-SHELL", "vault brain init failed: " + ex.Message); }
                });
                WebView.NavigationCompleted += (_, args) =>
                {
                    var state = args.IsSuccess ? "ready" : $"failed:{args.WebErrorStatus}";
                    KokoSystemLog.Write("WEB-SHELL", $"navigation {state} source={WebView.Source}");
                };
                var developmentUri = ResolveDevelopmentUri(Environment.GetEnvironmentVariable("KOKONOE_WEB_DEV_URL"));
                if (developmentUri != null)
                {
                    KokoSystemLog.Write("WEB-SHELL", $"using development frontend {developmentUri}");
                    WebView.Source = developmentUri;
                }
                else
                {
                    var frontendRoot = ResolveFrontendRootPath();
                    var indexPath = Path.Combine(frontendRoot, "index.html");
                    if (!File.Exists(indexPath))
                        throw new FileNotFoundException("Built web shell entry point was not copied to the output directory.", indexPath);
                    WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "kokonoe.local",
                        frontendRoot,
                        CoreWebView2HostResourceAccessKind.DenyCors);
                    WebView.Source = new Uri("https://kokonoe.local/index.html");
                }
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("WEB-SHELL", "initialization failed: " + ex);
                Debug.WriteLine("[WEB-SHELL] " + ex);
                var coreReady = WebView.CoreWebView2 != null;
                if (InitializationFailed != null && ShouldUseLegacyFallback(ex, coreReady))
                {
                    KokoSystemLog.Write("WEB-SHELL", "delegating to legacy fallback; coreReady=" + coreReady);
                    InitializationFailed(ex.Message);
                    return;
                }
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

        internal static string ResolveFrontendRootPath()
            => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "frontend"));

        internal static Uri? ResolveDevelopmentUri(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
                !uri.IsLoopback ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return null;
            return uri;
        }

        internal static bool ShouldUseLegacyFallback(Exception error, bool coreReady)
        {
            if (!coreReady)
                return true;

            return error is WebView2RuntimeNotFoundException ||
                   error is DllNotFoundException ||
                   error is BadImageFormatException;
        }

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
