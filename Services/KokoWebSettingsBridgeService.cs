using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebSettingsBridgeService : IDisposable
    {
        private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
        private static readonly HashSet<string> LlmProviders = new(StringComparer.OrdinalIgnoreCase)
        {
            "lmstudio", "ollama", "ollama-cloud", "claude", "ollama-cloud-proxy"
        };
        private readonly KokoWebBridgeService _bridge;
        private readonly Func<AppSettings> _load;
        private readonly Func<AppSettings, string?> _save;
        private readonly Action<AppSettings>? _applied;
        private bool _disposed;

        public KokoWebSettingsBridgeService(
            KokoWebBridgeService bridge,
            Action<AppSettings>? applied = null)
            : this(
                bridge,
                AppSettings.Load,
                settings => settings.TrySave(out var error) ? null : error,
                applied)
        {
        }

        public KokoWebSettingsBridgeService(
            KokoWebBridgeService bridge,
            Func<AppSettings> load,
            Func<AppSettings, string?> save,
            Action<AppSettings>? applied = null)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _load = load ?? throw new ArgumentNullException(nameof(load));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _applied = applied;
            _bridge.Register("settings.get", HandleGetAsync);
            _bridge.Register("settings.update", HandleUpdateAsync);
            _bridge.Register("load_settings", HandleGetAsync);
            _bridge.Register("save_settings", HandleUpdateAsync);
        }

        private async Task<object?> HandleGetAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            return BuildSnapshot(_load());
        }

        private async Task<object?> HandleUpdateAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (payload is not JObject values)
                throw new InvalidOperationException("Settings payload must be an object.");

            var settings = _load();
            var changed = new List<string>();
            var restartRequired = false;

            KokoSystemLog.Write("SETTINGS-SAVE",
                $"Received {values.Count} fields. Secrets: " +
                string.Join(", ", new[] { "ollamaApiKey", "ollamaCloudProxyApiKey", "claudeApiKey", "tavilyApiKey" }
                    .Where(values.ContainsKey)
                    .Select(k => $"{k}={DescribeSecretToken(values[k])}")));

            ApplyInt(values, "proactiveAutonomyLevel", 0, 3, settings.ProactiveAutonomyLevel,
                value => settings.ProactiveAutonomyLevel = value, changed);
            ApplyBool(values, "spontaneousEnabled", settings.SpontaneousEnabled,
                value => settings.SpontaneousEnabled = value, changed);
            ApplyInt(values, "spontaneousIntervalMins", 10, 240, settings.SpontaneousIntervalMins,
                value => settings.SpontaneousIntervalMins = value, changed);
            ApplyBool(values, "screenAwarenessEnabled", settings.ScreenAwarenessEnabled,
                value => settings.ScreenAwarenessEnabled = value, changed);
            ApplyBool(values, "screenAwarenessSendComments", settings.ScreenAwarenessSendComments,
                value => settings.ScreenAwarenessSendComments = value, changed);
            ApplyInt(values, "screenAwarenessIntervalMins", 10, 240, settings.ScreenAwarenessIntervalMins,
                value => settings.ScreenAwarenessIntervalMins = value, changed);
            ApplyInt(values, "screenAwarenessCommentCooldownMins", 5, 240, settings.ScreenAwarenessCommentCooldownMins,
                value => settings.ScreenAwarenessCommentCooldownMins = value, changed);
            ApplyBool(values, "voiceInputEnabled", settings.VoiceInputEnabled,
                value => settings.VoiceInputEnabled = value, changed);
            ApplyBool(values, "ttsEnabled", settings.TtsEnabled,
                value => settings.TtsEnabled = value, changed);
            ApplyBool(values, "minimizeToTray", settings.MinimizeToTray,
                value => settings.MinimizeToTray = value, changed);
            ApplyBool(values, "neuralGovernorEnabled", settings.NeuralGovernorEnabled,
                value => settings.NeuralGovernorEnabled = value, changed);
            ApplyBool(values, "systemOverlordEnabled", settings.SystemOverlordEnabled,
                value => settings.SystemOverlordEnabled = value, changed);
            var browserEnabledBefore = settings.BrowserEnabled;
            ApplyBool(values, "browserEnabled", settings.BrowserEnabled,
                value => settings.BrowserEnabled = value, changed);
            if (settings.BrowserEnabled != browserEnabledBefore)
                restartRequired = true;
            // BrowserHeadless does not need a restart: it's only read the next
            // time the browser session launches (EnsureInitializedAsync), and
            // the session can be closed/reopened without restarting the app.
            ApplyBool(values, "browserHeadless", settings.BrowserHeadless,
                value => settings.BrowserHeadless = value, changed);

            if (values.TryGetValue("llmProvider", StringComparison.OrdinalIgnoreCase, out var providerToken))
            {
                var provider = providerToken?.ToString()?.Trim() ?? "";
                if (!LlmProviders.Contains(provider))
                    throw new InvalidOperationException("llmProvider must be one of: " + string.Join(", ", LlmProviders));
                if (!string.Equals(settings.LlmProvider, provider, StringComparison.OrdinalIgnoreCase))
                {
                    settings.LlmProvider = provider;
                    changed.Add("llmProvider");
                }
            }
            ApplyString(values, "ollamaUrl", 2048, settings.OllamaUrl,
                value => settings.OllamaUrl = value, changed);
            ApplyString(values, "ollamaModel", 256, settings.OllamaModel,
                value => settings.OllamaModel = value, changed);
            ApplyString(values, "ollamaCloudProxyModel", 256, settings.OllamaCloudProxyModel,
                value => settings.OllamaCloudProxyModel = value, changed);
            // LlmService.ResolveOllamaKey checks the OllamaKeys pool before OllamaApiKey,
            // and the pool only ever seeds itself from OllamaApiKey once, the first time
            // it's empty — after that it never looks at this field again. Replacing always
            // resets the pool to just this key so the field stays the actual source of
            // truth; clearing empties the pool too so a cleared key can't keep authorizing
            // requests via a stale pool entry.
            ApplySecret(values, "ollamaApiKey",
                value =>
                {
                    settings.OllamaApiKey = value;
                    settings.OllamaKeys = new List<OllamaKeyEntry>
                    {
                        new OllamaKeyEntry { Name = "Account 1", Key = value, Enabled = true }
                    };
                    settings.OllamaActiveKeyIndex = 0;
                },
                () =>
                {
                    settings.OllamaApiKey = "";
                    settings.OllamaKeys = new List<OllamaKeyEntry>();
                    settings.OllamaActiveKeyIndex = 0;
                },
                changed);
            ApplySecret(values, "ollamaCloudProxyApiKey",
                value => settings.OllamaCloudProxyApiKey = value,
                () => settings.OllamaCloudProxyApiKey = "",
                changed);
            ApplyString(values, "lmUrl", 2048, settings.LmUrl,
                value => settings.LmUrl = value, changed);
            ApplyString(values, "lmModel", 256, settings.Model,
                value => settings.Model = value, changed);
            ApplyString(values, "claudeModel", 256, settings.ClaudeModel,
                value => settings.ClaudeModel = value, changed);
            ApplySecret(values, "claudeApiKey",
                value => settings.ClaudeApiKey = value,
                () => settings.ClaudeApiKey = "",
                changed);
            ApplySecret(values, "tavilyApiKey",
                value => settings.TavilyApiKey = value,
                () => settings.TavilyApiKey = "",
                changed);
            ApplyInt(values, "maxTokens", 256, 16384, settings.MaxTokens,
                value => settings.MaxTokens = value, changed);

            var wearBridgeBefore = settings.WearBridgeEnabled;
            ApplyBool(values, "wearBridgeEnabled", settings.WearBridgeEnabled,
                value => settings.WearBridgeEnabled = value, changed);
            ApplyBool(values, "wearBridgeIncludePromptContext", settings.WearBridgeIncludePromptContext,
                value => settings.WearBridgeIncludePromptContext = value, changed);
            if (settings.WearBridgeEnabled != wearBridgeBefore)
                restartRequired = true;

            if (values.TryGetValue("matrixColor", StringComparison.OrdinalIgnoreCase, out var colorToken))
            {
                var color = colorToken?.ToString()?.Trim() ?? "";
                if (!HexColor.IsMatch(color))
                    throw new InvalidOperationException("matrixColor must use #RRGGBB format.");
                if (!string.Equals(settings.MatrixColor, color, StringComparison.OrdinalIgnoreCase))
                {
                    settings.MatrixColor = color.ToUpperInvariant();
                    changed.Add("matrixColor");
                }
            }

            if (changed.Count > 0)
            {
                var saveError = _save(settings);
                if (!string.IsNullOrWhiteSpace(saveError))
                    throw new IOException("Settings save failed: " + saveError);
                _applied?.Invoke(settings);
                KokoSystemLog.Write("SETTINGS-SAVE", "Applied changes: " + string.Join(", ", changed));
            }
            else
            {
                KokoSystemLog.Write("SETTINGS-SAVE", "Applied changes: (none)");
            }

            return new
            {
                settings = BuildSnapshot(settings),
                changed,
                restartRequired
            };
        }

        private static object BuildSnapshot(AppSettings settings) => new
        {
            values = new
            {
                proactiveAutonomyLevel = settings.ProactiveAutonomyLevel,
                spontaneousEnabled = settings.SpontaneousEnabled,
                spontaneousIntervalMins = settings.SpontaneousIntervalMins,
                screenAwarenessEnabled = settings.ScreenAwarenessEnabled,
                screenAwarenessSendComments = settings.ScreenAwarenessSendComments,
                screenAwarenessIntervalMins = settings.ScreenAwarenessIntervalMins,
                screenAwarenessCommentCooldownMins = settings.ScreenAwarenessCommentCooldownMins,
                voiceInputEnabled = settings.VoiceInputEnabled,
                ttsEnabled = settings.TtsEnabled,
                minimizeToTray = settings.MinimizeToTray,
                neuralGovernorEnabled = settings.NeuralGovernorEnabled,
                systemOverlordEnabled = settings.SystemOverlordEnabled,
                browserEnabled = settings.BrowserEnabled,
                browserHeadless = settings.BrowserHeadless,
                wearBridgeEnabled = settings.WearBridgeEnabled,
                wearBridgeIncludePromptContext = settings.WearBridgeIncludePromptContext,
                matrixColor = settings.MatrixColor,
                llmProvider = settings.LlmProvider,
                ollamaUrl = settings.OllamaUrl,
                ollamaModel = settings.OllamaModel,
                ollamaCloudProxyModel = settings.OllamaCloudProxyModel,
                lmUrl = settings.LmUrl,
                lmModel = settings.Model,
                claudeModel = settings.ClaudeModel,
                maxTokens = settings.MaxTokens
            },
            credentials = new
            {
                telegramBot = !string.IsNullOrWhiteSpace(settings.TelegramToken),
                telegramUser = settings.TgApiId > 0 && !string.IsNullOrWhiteSpace(settings.TgApiHash),
                openAi = !string.IsNullOrWhiteSpace(settings.OpenAiApiKey),
                claude = !string.IsNullOrWhiteSpace(settings.ClaudeApiKey),
                ollama = !string.IsNullOrWhiteSpace(settings.OllamaApiKey) || (settings.OllamaKeys?.Count ?? 0) > 0,
                ollamaCloudProxy = !string.IsNullOrWhiteSpace(settings.OllamaCloudProxyApiKey),
                tavily = !string.IsNullOrWhiteSpace(settings.TavilyApiKey)
            }
        };

        private static void ApplyBool(
            JObject source,
            string name,
            bool current,
            Action<bool> assign,
            ICollection<string> changed)
        {
            if (!source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token))
                return;
            if (token?.Type != JTokenType.Boolean)
                throw new InvalidOperationException(name + " must be boolean.");
            var value = token.Value<bool>();
            if (value == current)
                return;
            assign(value);
            changed.Add(name);
        }

        private static void ApplyInt(
            JObject source,
            string name,
            int min,
            int max,
            int current,
            Action<int> assign,
            ICollection<string> changed)
        {
            if (!source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token))
                return;
            if (token == null || !int.TryParse(token.ToString(), out var value) || value < min || value > max)
                throw new InvalidOperationException($"{name} must be between {min} and {max}.");
            if (value == current)
                return;
            assign(value);
            changed.Add(name);
        }

        // Describes a secret field's incoming op for the audit log without ever logging the
        // actual key value.
        private static string DescribeSecretToken(JToken? token) => token switch
        {
            JObject opToken => opToken["op"]?.ToString() ?? "unchanged",
            null => "unchanged",
            _ => string.IsNullOrWhiteSpace(token.ToString()) ? "unchanged" : "replace(raw)"
        };

        // Secrets are tri-state: { op: "unchanged" } (default — leave as-is), { op: "replace",
        // value } (set to a new key), or { op: "clear" } (wipe it). A bare string is accepted
        // too for back-compat with non-frontend callers: empty means unchanged, non-empty means
        // replace — matching the old behavior, which had no way to express "clear".
        private static void ApplySecret(
            JObject source,
            string name,
            Action<string> replace,
            Action clear,
            ICollection<string> changed)
        {
            if (!source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token) || token == null)
                return;

            string op;
            string value;
            if (token is JObject opToken)
            {
                op = opToken["op"]?.ToString() ?? "unchanged";
                value = opToken["value"]?.ToString()?.Trim() ?? "";
            }
            else
            {
                value = token.ToString()?.Trim() ?? "";
                op = value.Length == 0 ? "unchanged" : "replace";
            }

            switch (op)
            {
                case "clear":
                    clear();
                    changed.Add(name);
                    KokoSystemLog.Write("SECRET", $"{name} cleared by user");
                    break;
                case "replace":
                    if (value.Length > 2048)
                        throw new InvalidOperationException($"{name} exceeds 2048 characters.");
                    if (value.Length > 0)
                    {
                        replace(value);
                        changed.Add(name);
                        KokoSystemLog.Write("SECRET", $"{name} replaced");
                    }
                    break;
            }
        }

        private static void ApplyString(
            JObject source,
            string name,
            int maxLength,
            string current,
            Action<string> assign,
            ICollection<string> changed)
        {
            if (!source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token))
                return;
            var value = token?.ToString()?.Trim() ?? "";
            if (value.Length > maxLength)
                throw new InvalidOperationException($"{name} exceeds {maxLength} characters.");
            if (string.Equals(value, current, StringComparison.Ordinal))
                return;
            assign(value);
            changed.Add(name);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebSettingsBridgeService));
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
