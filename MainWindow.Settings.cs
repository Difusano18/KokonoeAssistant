using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KokonoeAssistant.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TgUpdate   = Telegram.Bot.Types.Update;
using WMsgBox    = System.Windows.MessageBox;
using WButton    = System.Windows.Controls.Button;
using WKeyArgs   = System.Windows.Input.KeyEventArgs;
using WDragArgs  = System.Windows.DragEventArgs;
using WClipboard = System.Windows.Clipboard;
using WDataFmts  = System.Windows.DataFormats;
using WTextBox   = System.Windows.Controls.TextBox;
using MediaBrush   = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor   = System.Windows.Media.Color;
using WpfRect      = System.Windows.Shapes.Rectangle;
using WpfFF        = System.Windows.Media.FontFamily;
using WpfSz        = System.Windows.Size;
using WpfOri       = System.Windows.Controls.Orientation;
using WinForms     = System.Windows.Forms;

namespace KokonoeAssistant
{
    public partial class MainWindow
    {
        // ------------------------------------------------------------
        // SETTINGS PANEL (inline slide-in)
        // ------------------------------------------------------------

        private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsPanel();

        // Робочий снапшот поки панель відкрита — щоб не губити незбережені правки
        // моделі/ключа при переключенні провайдера в UI.
        private AppSettings? _panelSettings;
        private string _panelLastProvider = "";

        // Ollama Cloud key-pool VM
        private readonly System.Collections.ObjectModel.ObservableCollection<OllamaKeyRowVM> _ollamaKeysVM = new();
        private System.Windows.Threading.DispatcherTimer? _poolRefreshTimer;

        private string CurrentSelectedProvider()
        {
            if (SP_ProviderClaude.IsChecked == true)       return "claude";
            if (SP_ProviderOllamaCloud.IsChecked == true)  return "ollama-cloud";
            if (SP_ProviderOllama.IsChecked == true)       return "ollama";
            return "lmstudio";
        }

        private void OpenSettingsPanel()
        {
            var s = AppSettings.Load();
            _panelSettings = s;

            // Прив'язуємо ItemsControl до VM-колекції (один раз — ItemsSource не міняємо)
            if (SP_OllamaKeysList.ItemsSource == null)
                SP_OllamaKeysList.ItemsSource = _ollamaKeysVM;

            // LLM Provider
            SP_ProviderLmStudio.IsChecked    = s.LlmProvider.Equals("lmstudio", StringComparison.OrdinalIgnoreCase);
            SP_ProviderOllama.IsChecked      = s.LlmProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase);
            SP_ProviderOllamaCloud.IsChecked = s.LlmProvider.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase);
            SP_ProviderClaude.IsChecked      = s.LlmProvider.Equals("claude", StringComparison.OrdinalIgnoreCase);
            // Якщо у settings.json ще "lmstudio" як default, нічого не вибрано — підхопимо LM Studio
            if (SP_ProviderLmStudio.IsChecked != true && SP_ProviderOllama.IsChecked != true
                && SP_ProviderOllamaCloud.IsChecked != true && SP_ProviderClaude.IsChecked != true)
            {
                SP_ProviderLmStudio.IsChecked = true;
            }

            _panelLastProvider = CurrentSelectedProvider();
            LoadLlmFieldsForProvider(_panelLastProvider, s);

            SP_TgEnabled.IsChecked  = s.TelegramEnabled;
            SP_TgToken.Text         = s.TelegramToken;
            SP_TgChatId.Text        = s.TelegramChatId > 0 ? s.TelegramChatId.ToString() : "";
            SP_TgUserEnabled.IsChecked = s.TgUserEnabled;
            SP_TgApiId.Text         = s.TgApiId > 0 ? s.TgApiId.ToString() : "";
            SP_TgApiHash.Text       = s.TgApiHash;
            SP_TgPhone.Text         = s.TgPhone;
            SP_TgDmOnly.IsChecked   = s.TgDmOnly;
            SP_Voice.IsChecked      = s.VoiceInputEnabled;
            SP_OpenAiKey.Text       = s.OpenAiApiKey;
            SP_VaultPath.Text       = s.VaultPath;
            SP_SystemOverlordEnabled.IsChecked = s.SystemOverlordEnabled;
            SP_SystemOverlordRoots.Text = s.SystemOverlordRoots;
            SP_SystemOverlordMaxFiles.Text = s.SystemOverlordMaxFiles.ToString();
            SP_SystemOverlordCloud.IsChecked = s.SystemOverlordCloudAnalysisEnabled;
            SP_Spont.IsChecked      = s.SpontaneousEnabled;
            SP_SpontInterval.Text   = s.SpontaneousIntervalMins.ToString();
            SP_ProactiveLevel.Text  = s.ProactiveAutonomyLevel.ToString();
            SP_Tray.IsChecked       = s.MinimizeToTray;
            SP_AccentColor.Text     = s.MatrixColor;
            UpdateColorPreview(s.MatrixColor);

            // (LLM-поля вже завантажені через LoadLlmFieldsForProvider)

            // Показуємо overlay
            SettingsOverlay.Visibility = Visibility.Visible;

            // Slide-in анімація
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                480, 0,
                new System.Windows.Duration(TimeSpan.FromMilliseconds(280)))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            SettingsPanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.XProperty, anim);
        }

        private void Provider_Checked(object sender, RoutedEventArgs e)
        {
            if (_panelSettings == null) return; // викликано до OpenSettingsPanel — ігноруємо

            // 1) Зберегти поточний текст полів у in-memory snapshot для попереднього провайдера
            CommitLlmFieldsToSnapshot(_panelLastProvider);

            // 2) Підвантажити поля для нового
            var newProvider = CurrentSelectedProvider();
            LoadLlmFieldsForProvider(newProvider, _panelSettings);
            _panelLastProvider = newProvider;
        }

        private void LoadLlmFieldsForProvider(string provider, AppSettings s)
        {
            // За замовчуванням — пул ховаємо; покажемо тільки для ollama-cloud.
            SP_OllamaPoolGroup.Visibility = Visibility.Collapsed;
            SP_ModelLabel.Visibility = Visibility.Visible;
            SP_ModelBox.Visibility = Visibility.Visible;
            SP_ModelHint.Visibility = Visibility.Visible;
            StopPoolRefreshTimer();

            switch (provider)
            {
                case "ollama":
                    SP_UrlGroup.Visibility    = Visibility.Visible;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed;
                    SP_LmUrl.Text     = string.IsNullOrWhiteSpace(s.LmUrl) || s.LmUrl.Contains(":1234")
                                        ? "http://localhost:11434/v1/chat/completions" : s.LmUrl;
                    SP_UrlHint.Text   = "Локальна Ollama. Дефолт: http://localhost:11434/v1/chat/completions";
                    SP_ModelBox.Text  = string.IsNullOrWhiteSpace(s.Model) ? "llama3.2" : s.Model;
                    SP_ModelHint.Text = "Назва локальної моделі (напр. llama3.2, qwen2.5, mistral). Має бути запущена в `ollama serve`.";
                    SP_VisionModelBox.Text = s.VisionModel;
                    break;

                case "ollama-cloud":
                    SP_UrlGroup.Visibility    = Visibility.Collapsed;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed; // single-key UI більше не для Ollama Cloud
                    SP_OllamaPoolGroup.Visibility = Visibility.Visible;
                    SP_ModelLabel.Visibility = Visibility.Collapsed;
                    SP_ModelBox.Visibility = Visibility.Collapsed;
                    SP_ModelHint.Visibility = Visibility.Collapsed;

                    SP_OllamaMaxPerHour.Text = s.OllamaPoolMaxPerHour.ToString();
                    SP_OllamaRotateAt.Text   = ((int)Math.Round(s.OllamaPoolRotateAt * 100)).ToString();
                    SP_OllamaCooldown.Text   = s.OllamaPoolCooldownMins.ToString();

                    LoadOllamaKeysVM(s);
                    LoadAgentProfilesVM(s);

                    SP_ModelBox.Text = string.IsNullOrWhiteSpace(s.OllamaModel) ? AppSettings.DefaultOllamaCloudModel : s.OllamaModel;
                    SP_ModelHint.Text = "";
                    SP_VisionModelBox.Text = s.VisionModel;

                    StartPoolRefreshTimer();
                    break;

                case "claude":
                    SP_UrlGroup.Visibility    = Visibility.Collapsed;
                    SP_ApiKeyGroup.Visibility = Visibility.Visible;
                    SP_ApiKeyLabel.Text  = "Claude API Key";
                    SP_ApiKeyBox.Text    = s.ClaudeApiKey;
                    SP_ApiKeyHint.Text   = "Отримай ключ на https://console.anthropic.com/settings/keys.";
                    SP_ModelBox.Text     = string.IsNullOrWhiteSpace(s.ClaudeModel) ? "claude-sonnet-4-20250514" : s.ClaudeModel;
                    SP_ModelHint.Text    = "Напр. claude-sonnet-4-20250514, claude-opus-4-20250514, claude-3-5-sonnet-20241022.";
                    SP_VisionModelBox.Text = s.VisionModel;
                    HighlightApiKey();
                    break;

                default: // lmstudio
                    SP_UrlGroup.Visibility    = Visibility.Visible;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed;
                    SP_LmUrl.Text     = string.IsNullOrWhiteSpace(s.LmUrl) || s.LmUrl.Contains(":11434")
                                        ? "http://localhost:1234/v1/chat/completions" : s.LmUrl;
                    SP_UrlHint.Text   = "LM Studio. Дефолт: http://localhost:1234/v1/chat/completions";
                    SP_ModelBox.Text  = string.IsNullOrWhiteSpace(s.Model) ? "google/gemma-4-26b-a4b" : s.Model;
                    SP_ModelHint.Text = "Точна назва моделі з LM Studio (вкладка Local Server → Loaded Model).";
                    SP_VisionModelBox.Text = s.VisionModel;
                    break;
            }
        }

        // ---- Ollama Cloud key-pool helpers ----

        private void LoadOllamaKeysVM(AppSettings s)
        {
            _ollamaKeysVM.Clear();

            // Якщо у settings є ключі — підвантажуємо. Якщо порожньо але є legacy single — теж додаємо.
            if (s.OllamaKeys != null && s.OllamaKeys.Count > 0)
            {
                foreach (var k in s.OllamaKeys)
                    _ollamaKeysVM.Add(new OllamaKeyRowVM { Name = k.Name, Key = k.Key });
            }
            else if (!string.IsNullOrWhiteSpace(s.OllamaApiKey))
            {
                _ollamaKeysVM.Add(new OllamaKeyRowVM { Name = "Account 1", Key = s.OllamaApiKey });
            }

            RefreshPoolStatus();
        }

        private void LoadAgentProfilesVM(AppSettings s)
        {
            var chat = GetOrCreateAgentProfile(s, "chat", 0.85);
            var coder = GetOrCreateAgentProfile(s, "coder", 0.35);

            SP_AgentChatKey.Text = FormatAgentKeys(chat);
            SP_AgentChatModelBox.Text = string.IsNullOrWhiteSpace(chat.Model) ? AppSettings.DefaultOllamaCloudModel : chat.Model;
            SP_AgentChatTemp.Text = chat.Temperature?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "0.85";

            SP_AgentCoderKey.Text = FormatAgentKeys(coder);
            SP_AgentCoderModelBox.Text = string.IsNullOrWhiteSpace(coder.Model) ? "qwen3-coder:480b-cloud" : coder.Model;
            SP_AgentCoderTemp.Text = coder.Temperature?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "0.35";
        }

        private static string FormatAgentKeys(KokoAgentLlmProfile profile)
        {
            var keys = profile.OllamaKeys?
                .Where(k => !string.IsNullOrWhiteSpace(k.Key))
                .Select(k => k.Key.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();
            if (keys.Count == 0 && !string.IsNullOrWhiteSpace(profile.OllamaApiKey))
                keys.Add(profile.OllamaApiKey.Trim());
            return string.Join(Environment.NewLine, keys);
        }

        private static KokoAgentLlmProfile GetOrCreateAgentProfile(AppSettings settings, string agentId, double temperature)
        {
            settings.AgentLlmProfiles ??= new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase);
            if (!settings.AgentLlmProfiles.TryGetValue(agentId, out var profile) || profile == null)
            {
                profile = new KokoAgentLlmProfile
                {
                    AgentId = agentId,
                    Enabled = true,
                    Temperature = temperature
                };
                settings.AgentLlmProfiles[agentId] = profile;
            }

            if (string.IsNullOrWhiteSpace(profile.AgentId))
                profile.AgentId = agentId;
            if (!profile.Temperature.HasValue)
                profile.Temperature = temperature;
            return profile;
        }

        private void CommitAgentProfilesToSnapshot()
        {
            if (_panelSettings == null) return;
            SaveAgentProfile(_panelSettings, "chat", SP_AgentChatKey.Text, SP_AgentChatModelBox.Text, SP_AgentChatTemp.Text, 0.85);
            SaveAgentProfile(_panelSettings, "coder", SP_AgentCoderKey.Text, SP_AgentCoderModelBox.Text, SP_AgentCoderTemp.Text, 0.35);
        }

        private static void SaveAgentProfile(
            AppSettings settings,
            string agentId,
            string key,
            string model,
            string temperatureText,
            double fallbackTemperature)
        {
            var profile = GetOrCreateAgentProfile(settings, agentId, fallbackTemperature);
            profile.Enabled = true;
            profile.LlmProvider = "ollama-cloud";
            var keys = ParseAgentKeys(key);
            var existing = profile.OllamaKeys ?? new List<OllamaKeyEntry>();
            profile.OllamaKeys = keys.Select((k, i) =>
            {
                var prior = existing.FirstOrDefault(e => e.Key == k);
                return new OllamaKeyEntry
                {
                    Name = prior?.Name is { Length: > 0 } ? prior.Name : $"Key {i + 1}",
                    Key = k,
                    Enabled = true,
                    RecentRequests = prior?.RecentRequests ?? new List<DateTime>(),
                    CooldownUntil = prior?.CooldownUntil
                };
            }).ToList();
            profile.OllamaApiKey = profile.OllamaKeys.FirstOrDefault()?.Key ?? "";
            if (profile.OllamaKeys.Count > 0)
                profile.OllamaActiveKeyIndex = Math.Clamp(profile.OllamaActiveKeyIndex, 0, profile.OllamaKeys.Count - 1);
            profile.Model = model.Trim();
            if (double.TryParse(temperatureText.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temp))
                profile.Temperature = Math.Clamp(temp, 0.0, 2.0);
            else
                profile.Temperature = fallbackTemperature;
        }

        private static List<string> ParseAgentKeys(string raw)
            => (raw ?? "")
                .Split(new[] { "\r\n", "\n", "\r", ",", ";", " " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 8)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private void RefreshPoolStatus()
        {
            var pool = ServiceContainer.OllamaPool;
            // Snapshot bere stan з реального пулу — назви ключів зіставляємо за значенням Key.
            var snap = pool.Snapshot();
            for (int i = 0; i < _ollamaKeysVM.Count; i++)
            {
                var vm = _ollamaKeysVM[i];
                var status = snap.FirstOrDefault(s => Mask(vm.Key) == s.MaskedKey);

                if (string.IsNullOrWhiteSpace(vm.Key))
                {
                    vm.StatusText = "-";
                    vm.StatusBrush = System.Windows.Media.Brushes.Gray;
                    vm.ActiveDotBrush = System.Windows.Media.Brushes.Transparent;
                    continue;
                }

                if (status != null)
                {
                    if (status.OnCooldown && status.CooldownUntil.HasValue)
                    {
                        var mins = (int)Math.Ceiling((status.CooldownUntil.Value - DateTime.UtcNow).TotalMinutes);
                        vm.StatusText = $"cooldown {mins}m";
                        vm.StatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B));
                    }
                    else
                    {
                        vm.StatusText = $"{status.RequestsLastHour}/{status.Limit}";
                        vm.StatusBrush = status.UsagePct >= 0.9
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xC8, 0x4E))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9D, 0xB5, 0xA8));
                    }
                    vm.ActiveDotBrush = status.Active
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xFF, 0xAD))
                        : System.Windows.Media.Brushes.Transparent;
                }
                else
                {
                    // Ключ ще не в пулі (щойно додали в UI, ще не зберегли) — статус нейтральний
                    vm.StatusText = "новий";
                    vm.StatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x6B, 0x80));
                    vm.ActiveDotBrush = System.Windows.Media.Brushes.Transparent;
                }
            }
        }

        private static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "(empty)";
            if (key.Length <= 8) return new string('*', key.Length);
            return key[..4] + "..." + key[^4..];
        }

        private void StartPoolRefreshTimer()
        {
            StopPoolRefreshTimer();
            _poolRefreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _poolRefreshTimer.Tick += (_, _) => RefreshPoolStatus();
            _poolRefreshTimer.Start();
        }

        private void StopPoolRefreshTimer()
        {
            if (_poolRefreshTimer != null)
            {
                _poolRefreshTimer.Stop();
                _poolRefreshTimer = null;
            }
        }

        private void SP_AddKey_Click(object sender, RoutedEventArgs e)
        {
            _ollamaKeysVM.Add(new OllamaKeyRowVM
            {
                Name = $"Account {_ollamaKeysVM.Count + 1}",
                Key  = ""
            });
            RefreshPoolStatus();
        }

        private void SP_RemoveKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is OllamaKeyRowVM vm)
            {
                _ollamaKeysVM.Remove(vm);
                RefreshPoolStatus();
            }
        }

        // Зберігає поточні значення полів LLM у in-memory snapshot _panelSettings,
        // щоб переключення між провайдерами не губило незбережені правки
        private void CommitLlmFieldsToSnapshot(string provider)
        {
            if (_panelSettings == null) return;
            switch (provider)
            {
                case "ollama":
                case "lmstudio":
                    _panelSettings.LmUrl = SP_LmUrl.Text.Trim();
                    _panelSettings.Model = SP_ModelBox.Text.Trim();
                    break;
                case "ollama-cloud":
                    var coderModel = SP_AgentCoderModelBox.Text.Trim();
                    var chatModel = SP_AgentChatModelBox.Text.Trim();
                    _panelSettings.OllamaModel = !string.IsNullOrWhiteSpace(coderModel)
                        ? coderModel
                        : !string.IsNullOrWhiteSpace(chatModel)
                            ? chatModel
                            : AppSettings.DefaultOllamaCloudModel;
                    CommitAgentProfilesToSnapshot();
                    // Параметри пулу — з безпечним парсингом (залишаємо попереднє значення при кривому вводі)
                    if (int.TryParse(SP_OllamaMaxPerHour.Text.Trim(), out var maxH) && maxH > 0)
                        _panelSettings.OllamaPoolMaxPerHour = maxH;
                    if (int.TryParse(SP_OllamaRotateAt.Text.Trim(), out var rotPct) && rotPct > 0 && rotPct <= 100)
                        _panelSettings.OllamaPoolRotateAt = rotPct / 100.0;
                    if (int.TryParse(SP_OllamaCooldown.Text.Trim(), out var cdMin) && cdMin > 0)
                        _panelSettings.OllamaPoolCooldownMins = cdMin;

                    // Перебудовуємо OllamaKeys з ObservableCollection — фільтр: тільки рядки з непорожнім Key.
                    // Зберігаємо існуючі RecentRequests/CooldownUntil для ключів що залишились (за збігом Key).
                    var existing = _panelSettings.OllamaKeys ?? new System.Collections.Generic.List<OllamaKeyEntry>();
                    var rebuilt = new System.Collections.Generic.List<OllamaKeyEntry>();
                    foreach (var vm in _ollamaKeysVM)
                    {
                        var keyTrim = vm.Key?.Trim() ?? "";
                        if (string.IsNullOrEmpty(keyTrim)) continue;
                        var prior = existing.FirstOrDefault(k => k.Key == keyTrim);
                        rebuilt.Add(new OllamaKeyEntry
                        {
                            Name = string.IsNullOrWhiteSpace(vm.Name) ? $"Account {rebuilt.Count + 1}" : vm.Name.Trim(),
                            Key  = keyTrim,
                            Enabled = true,
                            RecentRequests = prior?.RecentRequests ?? new System.Collections.Generic.List<DateTime>(),
                            CooldownUntil  = prior?.CooldownUntil
                        });
                    }
                    _panelSettings.OllamaKeys = rebuilt;
                    // Legacy single — синхронізуємо з першим ключем для backwards-compat
                    _panelSettings.OllamaApiKey = rebuilt.Count > 0 ? rebuilt[0].Key : "";
                    break;
                case "claude":
                    _panelSettings.ClaudeApiKey = SP_ApiKeyBox.Text.Trim();
                    _panelSettings.ClaudeModel  = SP_ModelBox.Text.Trim();
                    break;
            }
            _panelSettings.VisionModel = SP_VisionModelBox.Text.Trim();
        }

        private void SP_ApiKey_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => HighlightApiKey();

        private void HighlightApiKey()
        {
            var hasKey = !string.IsNullOrWhiteSpace(SP_ApiKeyBox.Text);
            SP_ApiKeyBox.BorderBrush = hasKey
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x4E));
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            StopPoolRefreshTimer();

            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                0, 480,
                new System.Windows.Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            anim.Completed += (_, _) => SettingsOverlay.Visibility = Visibility.Collapsed;
            SettingsPanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.XProperty, anim);
        }

        private void SP_Save_Click(object sender, RoutedEventArgs e)
        {
            var s = AppSettings.Load();

            // LLM Provider — комітимо поточні поля у in-memory snapshot щоб не загубити
            // редагування поточного провайдера, потім переносимо ВСІ LLM-поля з snapshot у s.
            var provider = CurrentSelectedProvider();
            CommitLlmFieldsToSnapshot(provider);
            if (_panelSettings != null)
            {
                s.LmUrl        = _panelSettings.LmUrl;
                s.Model        = _panelSettings.Model;
                s.ClaudeApiKey = _panelSettings.ClaudeApiKey;
                s.ClaudeModel  = _panelSettings.ClaudeModel;
                s.OllamaApiKey = _panelSettings.OllamaApiKey;
                s.OllamaUrl    = _panelSettings.OllamaUrl;
                s.OllamaModel  = _panelSettings.OllamaModel;
                // Pool
                s.OllamaKeys             = _panelSettings.OllamaKeys ?? new System.Collections.Generic.List<OllamaKeyEntry>();
                s.OllamaPoolMaxPerHour   = _panelSettings.OllamaPoolMaxPerHour;
                s.OllamaPoolRotateAt     = _panelSettings.OllamaPoolRotateAt;
                s.OllamaPoolCooldownMins = _panelSettings.OllamaPoolCooldownMins;
                s.OllamaActiveKeyIndex   = _panelSettings.OllamaActiveKeyIndex;
                s.VisionModel            = _panelSettings.VisionModel;
                s.AgentLlmProfiles       = _panelSettings.AgentLlmProfiles ?? new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase);
            }
            s.LlmProvider = provider;

            s.TelegramEnabled    = SP_TgEnabled.IsChecked == true;
            var telegramTokenText = SP_TgToken.Text.Trim();
            if (!string.IsNullOrWhiteSpace(telegramTokenText))
                s.TelegramToken = telegramTokenText;
            s.TgUserEnabled      = SP_TgUserEnabled.IsChecked == true;
            s.TgDmOnly           = SP_TgDmOnly.IsChecked == true;
            s.VoiceInputEnabled  = SP_Voice.IsChecked == true;
            var openAiKeyText = SP_OpenAiKey.Text.Trim();
            if (!string.IsNullOrWhiteSpace(openAiKeyText))
                s.OpenAiApiKey = openAiKeyText;
            s.VaultPath          = SP_VaultPath.Text.Trim();
            s.SystemOverlordEnabled = SP_SystemOverlordEnabled.IsChecked == true;
            s.SystemOverlordRoots = string.IsNullOrWhiteSpace(SP_SystemOverlordRoots.Text)
                ? "%USERPROFILE%\\Downloads\r\n%USERPROFILE%\\Pictures"
                : SP_SystemOverlordRoots.Text.Trim();
            if (int.TryParse(SP_SystemOverlordMaxFiles.Text.Trim(), out var overlordMax))
                s.SystemOverlordMaxFiles = Math.Clamp(overlordMax, 50, 5000);
            s.SystemOverlordCloudAnalysisEnabled = SP_SystemOverlordCloud.IsChecked == true;
            s.SpontaneousEnabled = SP_Spont.IsChecked == true;
            if (int.TryParse(SP_SpontInterval.Text.Trim(), out var spontMins))
                s.SpontaneousIntervalMins = Math.Clamp(spontMins, 10, 240);
            if (int.TryParse(SP_ProactiveLevel.Text.Trim(), out var proactiveLevel))
                s.ProactiveAutonomyLevel = Math.Clamp(proactiveLevel, 0, 3);
            s.MinimizeToTray     = SP_Tray.IsChecked == true;
            s.MatrixColor        = string.IsNullOrWhiteSpace(SP_AccentColor.Text) ? "#68E6D6" : SP_AccentColor.Text.Trim();

            if (long.TryParse(SP_TgChatId.Text.Trim(), out var cid)) s.TelegramChatId = cid;

            // Не перезаписуємо якщо поле порожнє — захист від випадкового обнулення
            if (int.TryParse(SP_TgApiId.Text.Trim(), out var apiId) && apiId > 0)
                s.TgApiId = apiId;
            if (!string.IsNullOrEmpty(SP_TgApiHash.Text.Trim()))
                s.TgApiHash = SP_TgApiHash.Text.Trim();
            if (!string.IsNullOrEmpty(SP_TgPhone.Text.Trim()))
                s.TgPhone = SP_TgPhone.Text.Trim();

            s.Save();
            ThemeManager.ApplyTheme(s.MatrixColor);
            _llm.ReloadSettings();

            CloseSettings_Click(sender, e);
        }

        private void UpdateColorPreview(string hex)
        {
            try
            {
                var color = (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(hex);
                SP_ColorPreview.Background = new System.Windows.Media.SolidColorBrush(color);
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "UpdateColorPreview failed near source line 10426: " + ex); }
        }

        private void SP_ColorPreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            using var dlg = new System.Windows.Forms.ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                SP_AccentColor.Text = hex;
                UpdateColorPreview(hex);
            }
        }

        private void SP_Swatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border b && b.Tag is string hex)
            {
                SP_AccentColor.Text = hex;
                UpdateColorPreview(hex);
            }
        }
    }
}
