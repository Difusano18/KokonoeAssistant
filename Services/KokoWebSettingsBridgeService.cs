using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebSettingsBridgeService : IDisposable
    {
        private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
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
        }

        private Task<object?> HandleGetAsync(JToken? payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            return Task.FromResult<object?>(BuildSnapshot(_load()));
        }

        private Task<object?> HandleUpdateAsync(JToken? payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (payload is not JObject values)
                throw new InvalidOperationException("Settings payload must be an object.");

            var settings = _load();
            var changed = new List<string>();
            var restartRequired = false;

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
                KokoSystemLog.Write("WEB-SETTINGS", "updated: " + string.Join(",", changed));
            }

            return Task.FromResult<object?>(new
            {
                settings = BuildSnapshot(settings),
                changed,
                restartRequired
            });
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
                wearBridgeEnabled = settings.WearBridgeEnabled,
                wearBridgeIncludePromptContext = settings.WearBridgeIncludePromptContext,
                matrixColor = settings.MatrixColor
            },
            credentials = new
            {
                telegramBot = !string.IsNullOrWhiteSpace(settings.TelegramToken),
                telegramUser = settings.TgApiId > 0 && !string.IsNullOrWhiteSpace(settings.TgApiHash),
                openAi = !string.IsNullOrWhiteSpace(settings.OpenAiApiKey),
                claude = !string.IsNullOrWhiteSpace(settings.ClaudeApiKey),
                ollama = !string.IsNullOrWhiteSpace(settings.OllamaApiKey) || (settings.OllamaKeys?.Count ?? 0) > 0
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
