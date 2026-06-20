using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Windows.Threading;
using KokonoeAssistant.Services;
using Newtonsoft.Json;
using WpfApp = System.Windows.Application;

namespace KokonoeAssistant
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly DispatcherTimer _wearBridgeTimer;

        public SettingsWindow(AppSettings settings)
        {
            Resources.MergedDictionaries.Add(WpfApp.Current.Resources);
            InitializeComponent();
            _settings = settings;
            LoadToUI();
            _wearBridgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _wearBridgeTimer.Tick += (_, _) => RefreshWearBridgeUi();
            _wearBridgeTimer.Start();
            RefreshWearBridgeUi();
        }

        private void LoadToUI()
        {
            // LLM Provider
            ProviderLmStudio.IsChecked   = _settings.LlmProvider.Equals("lmstudio", StringComparison.OrdinalIgnoreCase);
            ProviderClaude.IsChecked     = _settings.LlmProvider.Equals("claude", StringComparison.OrdinalIgnoreCase);
            ProviderOllamaCloud.IsChecked = _settings.LlmProvider.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase);
            UpdateProviderVisibility();

            LmUrlBox.Text          = _settings.LmUrl;
            ModelBox.Text          = _settings.Model;
            ClaudeApiKeyBox.Text   = _settings.ClaudeApiKey;
            ClaudeModelBox.Text    = _settings.ClaudeModel;
            OllamaApiKeyBox.Text   = _settings.OllamaApiKey;
            OllamaUrlBox.Text      = _settings.OllamaUrl;
            OllamaModelBox.Text    = _settings.OllamaModel;
            OpenAiKeyBox.Text      = _settings.OpenAiApiKey;
            VaultPathBox.Text      = _settings.VaultPath;
            TgEnabledBox.IsChecked  = _settings.TelegramEnabled;
            TgTokenBox.Text         = _settings.TelegramToken;
            TgChatIdBox.Text        = _settings.TelegramChatId > 0 ? _settings.TelegramChatId.ToString() : "";
            TgUserEnabledBox.IsChecked = _settings.TgUserEnabled;
            TgApiIdBox.Text         = _settings.TgApiId > 0 ? _settings.TgApiId.ToString() : "";
            TgApiHashBox.Text       = _settings.TgApiHash;
            TgPhoneBox.Text         = _settings.TgPhone;
            TgDmOnlyBox.IsChecked   = _settings.TgDmOnly;
            WearBridgeEnabledBox.IsChecked = _settings.WearBridgeEnabled;
            WearBridgePromptBox.IsChecked = _settings.WearBridgeIncludePromptContext;
            WearBridgePortBox.Text = _settings.WearBridgePort.ToString();
            WearBridgeExternalUrlsBox.Text = _settings.WearBridgeExternalUrls;
            SpontBox.IsChecked     = _settings.SpontaneousEnabled;
            SpontIntervalBox.Text   = _settings.SpontaneousIntervalMins.ToString();
            ProactiveLevelBox.Text  = _settings.ProactiveAutonomyLevel.ToString();
            TrayBox.IsChecked      = _settings.MinimizeToTray;
            VoiceBox.IsChecked     = _settings.VoiceInputEnabled;
            TtsBox.IsChecked       = _settings.TtsEnabled;
            MatrixColorBox.Text    = _settings.MatrixColor;

            // Subscribe to provider changes
            ProviderLmStudio.Checked    += Provider_Changed;
            ProviderClaude.Checked      += Provider_Changed;
            ProviderOllamaCloud.Checked += Provider_Changed;
        }

        private void UpdateProviderVisibility()
        {
            var lm     = ProviderLmStudio.IsChecked == true;
            var claude = ProviderClaude.IsChecked == true;
            var ollama = ProviderOllamaCloud.IsChecked == true;
            LmStudioSettings.Visibility    = lm     ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            ClaudeSettings.Visibility      = claude ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            OllamaCloudSettings.Visibility = ollama ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            UpdateProviderVisibility();
        }

        private void ClaudeApiKey_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Visual feedback for API key presence
            var hasKey = !string.IsNullOrWhiteSpace(ClaudeApiKeyBox.Text);
            ClaudeApiKeyBox.BorderBrush = hasKey
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x4E));
        }

        private void OllamaApiKey_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var hasKey = !string.IsNullOrWhiteSpace(OllamaApiKeyBox.Text);
            OllamaApiKeyBox.BorderBrush = hasKey
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x4E));
        }

                private void CustomColorSwatch_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.ColorDialog())
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    MatrixColorBox.Text = hex;
                }
            }
        }

        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string hex)
            {
                MatrixColorBox.Text = hex;
            }
        }

        private void Save_Click(object s, RoutedEventArgs e)
        {
            // LLM Provider
            _settings.LlmProvider        = ProviderClaude.IsChecked == true       ? "claude"
                                         : ProviderOllamaCloud.IsChecked == true  ? "ollama-cloud"
                                                                                  : "lmstudio";
            _settings.LmUrl              = LmUrlBox.Text.Trim();
            _settings.Model              = ModelBox.Text.Trim();
            var claudeApiKeyText = ClaudeApiKeyBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(claudeApiKeyText))
                _settings.ClaudeApiKey = claudeApiKeyText;
            _settings.ClaudeModel        = ClaudeModelBox.Text?.Trim() ?? "claude-sonnet-4-20250514";
            var ollamaApiKeyText = OllamaApiKeyBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ollamaApiKeyText))
                _settings.OllamaApiKey = ollamaApiKeyText;
            _settings.OllamaUrl          = string.IsNullOrWhiteSpace(OllamaUrlBox.Text) ? "https://ollama.com/v1/chat/completions" : OllamaUrlBox.Text.Trim();
            _settings.OllamaModel        = string.IsNullOrWhiteSpace(OllamaModelBox.Text)
                                         ? AppSettings.DefaultOllamaCloudModel
                                         : OllamaModelBox.Text.Trim();
            var openAiKeyText = OpenAiKeyBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(openAiKeyText))
                _settings.OpenAiApiKey = openAiKeyText;
            _settings.VaultPath          = VaultPathBox.Text.Trim();
            _settings.TelegramEnabled    = TgEnabledBox.IsChecked == true;
            var telegramTokenText = TgTokenBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(telegramTokenText))
                _settings.TelegramToken = telegramTokenText;
            _settings.TgUserEnabled      = TgUserEnabledBox.IsChecked == true;
            var tgApiHashText = TgApiHashBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(tgApiHashText))
                _settings.TgApiHash = tgApiHashText;
            var tgPhoneText = TgPhoneBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(tgPhoneText))
                _settings.TgPhone = tgPhoneText;
            _settings.TgDmOnly           = TgDmOnlyBox.IsChecked == true;
            if (int.TryParse(TgApiIdBox.Text.Trim(), out var apiId))
                _settings.TgApiId = apiId;
            _settings.SpontaneousEnabled = SpontBox.IsChecked == true;
            if (int.TryParse(SpontIntervalBox.Text.Trim(), out var spontMins))
                _settings.SpontaneousIntervalMins = Math.Clamp(spontMins, 10, 240);
            if (int.TryParse(ProactiveLevelBox.Text.Trim(), out var proactiveLevel))
                _settings.ProactiveAutonomyLevel = Math.Clamp(proactiveLevel, 0, 3);
            _settings.WearBridgeEnabled = WearBridgeEnabledBox.IsChecked == true;
            _settings.WearBridgeIncludePromptContext = WearBridgePromptBox.IsChecked == true;
            if (int.TryParse(WearBridgePortBox.Text.Trim(), out var wearPort))
                _settings.WearBridgePort = Math.Clamp(wearPort, 1024, 65535);
            _settings.WearBridgeExternalUrls = WearBridgeExternalUrlsBox.Text.Trim();
            _settings.MinimizeToTray     = TrayBox.IsChecked == true;
            _settings.VoiceInputEnabled  = VoiceBox.IsChecked == true;
            _settings.TtsEnabled         = TtsBox.IsChecked == true;
            _settings.MatrixColor        = string.IsNullOrWhiteSpace(MatrixColorBox.Text) ? "#00E676" : MatrixColorBox.Text.Trim();
            if (long.TryParse(TgChatIdBox.Text.Trim(), out var cid))
                _settings.TelegramChatId = cid;

            _settings.Save();

            // Apply the theme across the whole application immediately
            ThemeManager.ApplyTheme(_settings.MatrixColor);

            // Reload LLM settings to apply provider change
            ServiceContainer.LlmService?.ReloadSettings();
            ServiceContainer.ReloadWearableBridge();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object s, RoutedEventArgs e) => Close();
        private void Close_Click(object s, RoutedEventArgs e)  => Close();

        protected override void OnClosed(EventArgs e)
        {
            _wearBridgeTimer.Stop();
            base.OnClosed(e);
        }

        private void RefreshWearBridgeUi()
        {
            try
            {
                var bridge = ServiceContainer.WearableBridge;
                var telemetry = ServiceContainer.WearableTelemetry.State;
                var diagnostics = bridge.Diagnostics;
                var connection = bridge.GetConnectionSnapshot(telemetry);

                WearBridgeLinkText.Text = FormatBridgeState(connection.State);
                WearBridgeLinkText.Foreground = BrushForWearBridgeState(connection.State);

                WearBridgeStatusText.Text =
                    $"Status: {(bridge.IsRunning ? "running" : "stopped")} | PC {bridge.PcId} | port {bridge.Port} | pending {diagnostics.PendingCommands}";
                WearBridgeServerText.Text =
                    $"Base {bridge.BaseUrl}; LAN URLs {bridge.LanUrls.Count}; fallback URLs {bridge.ExternalUrls.Count}; last status {FormatUtc(diagnostics.LastStatusAtUtc)}";
                WearBridgeLinkDetailText.Text =
                    $"paired {(connection.IsPaired ? diagnostics.LastPairedDeviceId : "-")}; authorized {FormatUtc(diagnostics.LastAuthorizedAtUtc)}; " +
                    $"last seen {FormatUtc(connection.LastSeenAtUtc)}; reason {NullDash(connection.Reason)}";
                WearBridgeTrafficText.Text =
                    $"samples {diagnostics.TotalSamples}; batches {diagnostics.TotalBatchRequests}; duplicates {diagnostics.TotalDuplicateSamples}; auth failures {diagnostics.TotalAuthFailures}";
                WearBridgeCommandText.Text =
                    $"polls {diagnostics.TotalCommandPolls}; pending {diagnostics.PendingCommands}; acks {diagnostics.TotalCommandAcks}; " +
                    $"queued {NullDash(diagnostics.LastQueuedCommandAction)} {FormatUtc(diagnostics.LastQueuedCommandAtUtc)}; " +
                    $"delivered {NullDash(diagnostics.LastDeliveredCommandAction)} {FormatUtc(diagnostics.LastDeliveredCommandAtUtc)}; " +
                    $"ack {NullDash(diagnostics.LastAckAction)} {(diagnostics.LastAckOk ? "ok" : "not ok")} {FormatUtc(diagnostics.LastCommandAckAtUtc)}";
                WearBridgeEndpointText.Text =
                    $"remote {NullDash(diagnostics.LastRemoteEndpoint)}; user-agent {NullDash(diagnostics.LastUserAgent)}";
                WearBridgeSampleIdsText.Text =
                    $"device {NullDash(diagnostics.LastDeviceId)}; accepted id {NullDash(diagnostics.LastAcceptedSampleId)}; duplicate id {NullDash(diagnostics.LastDuplicateSampleId)}";
                WearBridgeTokenBox.Text = bridge.Token;
                WearBridgeUrlsBox.Text = string.Join(Environment.NewLine, bridge.LanUrls.Concat(bridge.ExternalUrls).Distinct());
                WearBridgeTelemetryText.Text =
                    connection.TelemetryFresh
                        ? $"Last telemetry: {telemetry.LastSampleUtc:u}; {telemetry.Summary}; battery {(telemetry.BatteryPercent?.ToString("F0") ?? "-")}%\n" +
                          $"Bridge: last sample {FormatUtc(diagnostics.LastSampleAtUtc)}; last pair {FormatUtc(diagnostics.LastPairAtUtc)}"
                        : $"Last telemetry: no fresh wearable sample.\n" +
                          $"Bridge: last sample {FormatUtc(diagnostics.LastSampleAtUtc)}; last pair {FormatUtc(diagnostics.LastPairAtUtc)}";
                WearBridgeTelemetryText.Text +=
                    $"\nCommand ack: {diagnostics.TotalCommandAcks} total; last {NullDash(diagnostics.LastAckAction)} " +
                    $"({(diagnostics.LastAckOk ? "ok" : "not ok")}) at {FormatUtc(diagnostics.LastCommandAckAtUtc)}; {NullDash(diagnostics.LastAckDetail)}";
                WearBridgeLastErrorText.Text = string.IsNullOrWhiteSpace(bridge.LastError) ? "No bridge error." : bridge.LastError;
            }
            catch (Exception ex)
            {
                WearBridgeStatusText.Text = "Status: unavailable";
                WearBridgeLinkText.Text = "ERROR";
                WearBridgeLinkText.Foreground = BrushForWearBridgeState("ERROR");
                WearBridgeLastErrorText.Text = ex.Message;
            }
        }

        private void WearBridgeStart_Click(object sender, RoutedEventArgs e)
        {
            ServiceContainer.WearableBridge.Start();
            RefreshWearBridgeUi();
        }

        private void WearBridgeStop_Click(object sender, RoutedEventArgs e)
        {
            ServiceContainer.WearableBridge.Stop();
            RefreshWearBridgeUi();
        }

        private void WearBridgeCopyToken_Click(object sender, RoutedEventArgs e)
        {
            try { System.Windows.Clipboard.SetText(ServiceContainer.WearableBridge.Token); } catch (Exception suppressedEx265) { KokoSystemLog.Write("SETTINGSWINDOW.XAML-CATCH", "WearBridgeCopyToken_Click failed near source line 265: " + suppressedEx265); }
        }

        private void WearBridgeCopyDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bridge = ServiceContainer.WearableBridge;
                var telemetry = ServiceContainer.WearableTelemetry.State;
                var payload = new
                {
                    capturedAtUtc = DateTime.UtcNow,
                    bridge.PcId,
                    bridge.Port,
                    bridge.BaseUrl,
                    bridge.LanUrls,
                    bridge.ExternalUrls,
                    connection = bridge.GetConnectionSnapshot(telemetry),
                    diagnostics = bridge.Diagnostics,
                    wearable = telemetry
                };
                System.Windows.Clipboard.SetText(JsonConvert.SerializeObject(payload, Formatting.Indented));
            }
            catch (Exception suppressedEx288) { KokoSystemLog.Write("SETTINGSWINDOW.XAML-CATCH", "WearBridgeCopyDiagnostics_Click failed near source line 288: " + suppressedEx288); }
        }

        private void WearBridgeRefreshPair_Click(object sender, RoutedEventArgs e)
        {
            ServiceContainer.WearableBridge.QueueCommand("refresh_pairing");
            RefreshWearBridgeUi();
        }

        private void WearBridgeRestartWatch_Click(object sender, RoutedEventArgs e)
        {
            ServiceContainer.WearableBridge.QueueCommand("restart_service");
            RefreshWearBridgeUi();
        }

        private void WearBridgeClearQueue_Click(object sender, RoutedEventArgs e)
        {
            ServiceContainer.WearableBridge.QueueCommand("clear_queue");
            RefreshWearBridgeUi();
        }

        private static string FormatUtc(DateTime? value) => value.HasValue ? value.Value.ToString("u") : "-";
        private static string NullDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        private static string FormatBridgeState(string state)
            => string.IsNullOrWhiteSpace(state) ? "UNKNOWN" : state.Replace('_', ' ');

        private static System.Windows.Media.SolidColorBrush BrushForWearBridgeState(string state)
        {
            var color = state switch
            {
                "LINKED" => System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E),
                "WAITING_FOR_WATCH" => System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B),
                "WAITING_FOR_PAIR" => System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B),
                "ERROR" => System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44),
                _ => System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80)
            };
            return new System.Windows.Media.SolidColorBrush(color);
        }

        private void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
