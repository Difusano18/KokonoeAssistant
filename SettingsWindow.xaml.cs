using System.Windows;
using System.Windows.Input;
using WpfApp = System.Windows.Application;

namespace KokonoeAssistant
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow(AppSettings settings)
        {
            Resources.MergedDictionaries.Add(WpfApp.Current.Resources);
            InitializeComponent();
            _settings = settings;
            LoadToUI();
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
            _settings.ClaudeApiKey       = ClaudeApiKeyBox.Text.Trim();
            _settings.ClaudeModel        = ClaudeModelBox.Text?.Trim() ?? "claude-sonnet-4-20250514";
            _settings.OllamaApiKey       = OllamaApiKeyBox.Text.Trim();
            _settings.OllamaUrl          = string.IsNullOrWhiteSpace(OllamaUrlBox.Text) ? "https://ollama.com/v1/chat/completions" : OllamaUrlBox.Text.Trim();
            _settings.OllamaModel        = OllamaModelBox.Text?.Trim() ?? "gpt-oss:120b-cloud";
            _settings.OpenAiApiKey       = OpenAiKeyBox.Text.Trim();
            _settings.VaultPath          = VaultPathBox.Text.Trim();
            _settings.TelegramEnabled    = TgEnabledBox.IsChecked == true;
            _settings.TelegramToken      = TgTokenBox.Text.Trim();
            _settings.TgUserEnabled      = TgUserEnabledBox.IsChecked == true;
            _settings.TgApiHash          = TgApiHashBox.Text.Trim();
            _settings.TgPhone            = TgPhoneBox.Text.Trim();
            _settings.TgDmOnly           = TgDmOnlyBox.IsChecked == true;
            if (int.TryParse(TgApiIdBox.Text.Trim(), out var apiId))
                _settings.TgApiId = apiId;
            _settings.SpontaneousEnabled = SpontBox.IsChecked == true;
            if (int.TryParse(SpontIntervalBox.Text.Trim(), out var spontMins))
                _settings.SpontaneousIntervalMins = Math.Clamp(spontMins, 10, 240);
            if (int.TryParse(ProactiveLevelBox.Text.Trim(), out var proactiveLevel))
                _settings.ProactiveAutonomyLevel = Math.Clamp(proactiveLevel, 0, 3);
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

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object s, RoutedEventArgs e) => Close();
        private void Close_Click(object s, RoutedEventArgs e)  => Close();

        private void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}

