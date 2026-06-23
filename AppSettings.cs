using System;
using System.Collections.Generic;
using System.IO;
using KokonoeAssistant.Services;
using Newtonsoft.Json;

namespace KokonoeAssistant
{
    public class OllamaKeyEntry
    {
        public string Name { get; set; } = "";
        public string Key  { get; set; } = "";
        public bool   Enabled { get; set; } = true;
        // Sliding-window timestamps (UTC) успішних запитів за останню годину
        public List<DateTime> RecentRequests { get; set; } = new();
        // null = доступний; інакше — заблокований до вказаного UTC після 429
        public DateTime? CooldownUntil { get; set; }
    }

    public class KokoAgentLlmProfile
    {
        public string AgentId { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string LlmProvider { get; set; } = "";
        public string Url { get; set; } = "";
        public string Model { get; set; } = "";
        public string OllamaApiKey { get; set; } = "";
        public List<OllamaKeyEntry> OllamaKeys { get; set; } = new();
        public int OllamaActiveKeyIndex { get; set; } = 0;
        public double? Temperature { get; set; }
    }

    public class AppSettings
    {
        private static readonly object SettingsIoLock = new();
        public const string DefaultOllamaCloudModel = "gemma4:31b-cloud";
        public const string DefaultVisionModel = "gemma4:31b-cloud";
        public const string FallbackVisionModel = "";

        // LLM Provider: "lmstudio" | "ollama" | "claude" | "ollama-cloud"
        // Default to ollama-cloud: lmstudio needs a local server running, which is the
        // exact "cursor with no reply" failure this default was causing.
        public string LlmProvider { get; set; } = "ollama-cloud";

        // LM Studio
        public string LmUrl   { get; set; } = "http://localhost:1234/v1/chat/completions";
        public string Model   { get; set; } = "google/gemma-4-26b-a4b";

        // Claude API
        public string ClaudeApiKey { get; set; } = "";
        public string ClaudeModel  { get; set; } = "claude-sonnet-4-20250514";

        // Ollama Cloud (OpenAI-compatible endpoint, Bearer auth)
        // OllamaApiKey — legacy (single-key); тепер пул у OllamaKeys (нижче)
        public string OllamaApiKey { get; set; } = "";
        public string OllamaUrl    { get; set; } = "https://ollama.com/v1/chat/completions";
        public string OllamaModel  { get; set; } = DefaultOllamaCloudModel;

        // Vision model — використовується замість основної коли є вкладене зображення
        public string VisionModel  { get; set; } = DefaultVisionModel;
        // Vision URL — якщо заповнений, image requests йдуть сюди (локальний Ollama/LM Studio), а не на OllamaUrl
        // Порожньо = той самий OllamaUrl, але з VisionModel
        public string VisionUrl    { get; set; } = "";

        // Ollama Cloud — пул ключів з round-robin ротацією
        public List<OllamaKeyEntry> OllamaKeys { get; set; } = new();
        public int    OllamaPoolMaxPerHour    { get; set; } = 20;
        public double OllamaPoolRotateAt      { get; set; } = 0.9;
        public int    OllamaPoolCooldownMins  { get; set; } = 60;
        public int    OllamaActiveKeyIndex    { get; set; } = 0;

        // Optional per-agent overrides. Keys are logical agent ids:
        // obsidian, system, research, coder, vision, chat.
        public Dictionary<string, KokoAgentLlmProfile> AgentLlmProfiles { get; set; } = new();

        // OpenAI (for Whisper STT)
        public string OpenAiApiKey { get; set; } = "";

        // Vault
        public string VaultPath { get; set; } = @"O:\App\Obsidian\MyBrain\Yasus";

        // Telegram Bot (старий)
        public string TelegramToken   { get; set; } = "";
        public long   TelegramChatId  { get; set; } = 0;
        public bool   TelegramEnabled { get; set; } = true;
        public int    MiniAppPort     { get; set; } = 8787;
        public string MiniAppPublicUrl { get; set; } = "";

        // WearBridge
        public bool WearBridgeEnabled { get; set; } = true;
        public int WearBridgePort { get; set; } = 8787;
        public bool WearBridgeIncludePromptContext { get; set; } = true;
        public string WearBridgeExternalUrls { get; set; } = "";



        // Telegram UserClient (MTProto — повний акаунт)
        public int    TgApiId         { get; set; } = 0;
        public string TgApiHash       { get; set; } = "";
        public string TgPhone         { get; set; } = "";
        public bool   TgUserEnabled   { get; set; } = false;
        // Коконое відповідає тільки на DM (false = і групи теж)
        public bool   TgDmOnly        { get; set; } = false;
        public bool   TgRespondToOutgoing { get; set; } = true;
        public bool   TgFastReplyEnabled { get; set; } = true;
        public int    TgFastReplyMaxChars { get; set; } = 360;

        // Spontaneous messages
        public bool SpontaneousEnabled      { get; set; } = true;
        public int  SpontaneousIntervalMins { get; set; } = 25;
        public int  ProactiveAutonomyLevel  { get; set; } = 2; // 0=тихо, 1=обережно, 2=нормально, 3=живий режим

        public bool NeuralGovernorEnabled   { get; set; } = true;
        public int  NeuralGovernorTimeoutMs { get; set; } = 1800;

        // Screen awareness
        public bool ScreenAwarenessEnabled { get; set; } = true;
        public bool ScreenAwarenessSendComments { get; set; } = true;
        public int  ScreenAwarenessIntervalMins { get; set; } = 10;
        public int  ScreenAwarenessCommentCooldownMins { get; set; } = 15;
        public int  GameScreenAwarenessIntervalMins { get; set; } = 5;
        public int  GameScreenAwarenessCommentCooldownMins { get; set; } = 10;

        // System Overlord / local metadata fabric
        public bool SystemOverlordEnabled { get; set; } = true;
        public string SystemOverlordRoots { get; set; } = "%USERPROFILE%\\Downloads\r\n%USERPROFILE%\\Pictures";
        public int SystemOverlordMaxFiles { get; set; } = 700;
        public bool SystemOverlordCloudAnalysisEnabled { get; set; } = false;

        // System
        public bool MinimizeToTray      { get; set; } = true;
        public int  MemoryUpdateEveryN  { get; set; } = 6;
        public string MatrixColor       { get; set; } = "#6366F1";
        public string UiShell           { get; set; } = "web";

        // Voice
        public bool VoiceInputEnabled { get; set; } = true;
        public bool TtsEnabled        { get; set; } = false;

        // ── persistence ──────────────────────────────────────────
        // Зберігаємо в AppData щоб налаштування не збивались при білді/деплої
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KokonoeAssistant",
            "settings.json");
        private static readonly string _backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KokonoeAssistant",
            "settings.backup.json");

        public static AppSettings Load()
        {
            lock (SettingsIoLock)
            {
                if (TryLoadFile(_path, out var loaded, out var primaryError))
                {
                    var changed = RecoverMissingSecretsFromLegacySettings(loaded!);
                    if (NormalizeDefaults(loaded!))
                        changed = true;
                    if (changed)
                        loaded!.TrySave(out _);
                    return loaded!;
                }

                if (File.Exists(_path))
                {
                    KokoSystemLog.Write("APPSETTINGS", "Primary settings unreadable: " + primaryError);
                    if (TryLoadFile(_backupPath, out var backup, out var backupError))
                    {
                        RecoverMissingSecretsFromLegacySettings(backup!);
                        NormalizeDefaults(backup!);
                        if (TryRestoreBackup(out var restoreError))
                            KokoSystemLog.Write("APPSETTINGS", "Recovered settings from atomic backup.");
                        else
                            KokoSystemLog.Write("APPSETTINGS", "Backup loaded but primary restore failed: " + restoreError);
                        return backup!;
                    }
                    KokoSystemLog.Write("APPSETTINGS", "Settings backup unavailable: " + backupError);
                    var safeFallback = new AppSettings();
                    RecoverMissingSecretsFromLegacySettings(safeFallback);
                    NormalizeDefaults(safeFallback);
                    return safeFallback;
                }

                var def = new AppSettings();
                RecoverMissingSecretsFromLegacySettings(def);
                NormalizeDefaults(def);
                def.TrySave(out _);
                return def;
            }
        }

        private static bool TryLoadFile(string path, out AppSettings? settings, out string error)
        {
            settings = null;
            error = "not found";
            if (!File.Exists(path))
                return false;
            try
            {
                settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(path));
                if (settings == null)
                {
                    error = "deserialized to null";
                    return false;
                }
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryRestoreBackup(out string error)
        {
            string? tempPath = null;
            try
            {
                var dir = Path.GetDirectoryName(_path)!;
                tempPath = Path.Combine(dir, $"settings.restore.{Guid.NewGuid():N}.tmp");
                File.Copy(_backupPath, tempPath, overwrite: true);
                File.Move(tempPath, _path, overwrite: true);
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }
                return false;
            }
        }

        private static bool RecoverMissingSecretsFromLegacySettings(AppSettings settings)
        {
            var changed = false;
            foreach (var path in LegacySettingsPaths().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path) ||
                        string.Equals(Path.GetFullPath(path), Path.GetFullPath(_path), StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(path))
                        continue;

                    var legacy = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(path));
                    if (legacy == null)
                        continue;

                    if (CopyIfMissing(settings.TelegramToken, legacy.TelegramToken, out var telegramToken))
                    {
                        settings.TelegramToken = telegramToken;
                        changed = true;
                    }
                    if (settings.TelegramChatId <= 0 && legacy.TelegramChatId > 0)
                    {
                        settings.TelegramChatId = legacy.TelegramChatId;
                        changed = true;
                    }

                    if (CopyIfMissing(settings.TgApiHash, legacy.TgApiHash, out var tgApiHash))
                    {
                        settings.TgApiHash = tgApiHash;
                        changed = true;
                    }
                    if (CopyIfMissing(settings.TgPhone, legacy.TgPhone, out var tgPhone))
                    {
                        settings.TgPhone = tgPhone;
                        changed = true;
                    }
                    if (settings.TgApiId <= 0 && legacy.TgApiId > 0)
                    {
                        settings.TgApiId = legacy.TgApiId;
                        changed = true;
                    }

                    if (CopyIfMissing(settings.OpenAiApiKey, legacy.OpenAiApiKey, out var openAiApiKey))
                    {
                        settings.OpenAiApiKey = openAiApiKey;
                        changed = true;
                    }
                    if (CopyIfMissing(settings.ClaudeApiKey, legacy.ClaudeApiKey, out var claudeApiKey))
                    {
                        settings.ClaudeApiKey = claudeApiKey;
                        changed = true;
                    }
                    if (CopyIfMissing(settings.OllamaApiKey, legacy.OllamaApiKey, out var ollamaApiKey))
                    {
                        settings.OllamaApiKey = ollamaApiKey;
                        changed = true;
                    }
                }
                catch
                {
                    // Legacy recovery must never block startup.
                }
            }

            return changed;
        }

        private static IEnumerable<string> LegacySettingsPaths()
        {
            var cwd = Directory.GetCurrentDirectory();
            var baseDir = AppContext.BaseDirectory;
            yield return Path.Combine(cwd, "settings.json");
            yield return Path.Combine(cwd, "publish", "settings.json");
            yield return Path.Combine(baseDir, "settings.json");
            yield return Path.Combine(baseDir, "publish", "settings.json");
        }

        private static bool CopyIfMissing(string? target, string? source, out string value)
        {
            value = target ?? "";
            if (!string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(source))
                return false;
            value = source.Trim();
            return true;
        }

        private static bool NormalizeDefaults(AppSettings settings)
        {
            var changed = false;

            var normalizedUiShell = KokoUiStartupPolicy.NormalizeConfiguredMode(settings.UiShell);
            if (!string.Equals(settings.UiShell, normalizedUiShell, StringComparison.Ordinal))
            {
                settings.UiShell = normalizedUiShell;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.OllamaModel) ||
                settings.OllamaModel.Equals("gpt-oss:120b-cloud", StringComparison.OrdinalIgnoreCase))
            {
                settings.OllamaModel = DefaultOllamaCloudModel;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.VisionModel) ||
                settings.VisionModel.Equals("qwen3-vl:235b-instruct", StringComparison.OrdinalIgnoreCase))
            {
                settings.VisionModel = DefaultVisionModel;
                changed = true;
            }

            var normalizedAutonomy = Math.Clamp(settings.ProactiveAutonomyLevel, 0, 3);
            if (settings.ProactiveAutonomyLevel != normalizedAutonomy)
            {
                settings.ProactiveAutonomyLevel = normalizedAutonomy;
                changed = true;
            }

            if (settings.ScreenAwarenessIntervalMins == 30 || settings.ScreenAwarenessIntervalMins < 10)
            {
                settings.ScreenAwarenessIntervalMins = 10;
                changed = true;
            }

            if (settings.ScreenAwarenessCommentCooldownMins == 30 || settings.ScreenAwarenessCommentCooldownMins < 5)
            {
                settings.ScreenAwarenessCommentCooldownMins = 15;
                changed = true;
            }

            if (settings.GameScreenAwarenessIntervalMins < 3)
            {
                settings.GameScreenAwarenessIntervalMins = 3;
                changed = true;
            }

            if (settings.GameScreenAwarenessCommentCooldownMins < 5)
            {
                settings.GameScreenAwarenessCommentCooldownMins = 5;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.SystemOverlordRoots))
            {
                settings.SystemOverlordRoots = "%USERPROFILE%\\Downloads\r\n%USERPROFILE%\\Pictures";
                changed = true;
            }

            var normalizedOverlordMax = Math.Clamp(settings.SystemOverlordMaxFiles, 50, 5000);
            if (settings.SystemOverlordMaxFiles != normalizedOverlordMax)
            {
                settings.SystemOverlordMaxFiles = normalizedOverlordMax;
                changed = true;
            }

            settings.AgentLlmProfiles ??= new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase);
            changed |= EnsureAgentProfile(settings.AgentLlmProfiles, "chat", 0.85);
            changed |= EnsureAgentProfile(settings.AgentLlmProfiles, "coder", 0.35);
            changed |= EnsureAgentProfile(settings.AgentLlmProfiles, "system", 0.25);
            changed |= EnsureAgentProfile(settings.AgentLlmProfiles, "research", 0.45);
            changed |= EnsureAgentProfile(settings.AgentLlmProfiles, "obsidian", 0.35);
            changed |= EnsureAgentProfile(settings.AgentLlmProfiles, "vision-observer", 0.40);
            changed |= EnsureAgentProfile(settings.AgentLlmProfiles, "system-overlord", 0.25);

            return changed;
        }

        private static bool EnsureAgentProfile(Dictionary<string, KokoAgentLlmProfile> profiles, string agentId, double temperature)
        {
            if (profiles.TryGetValue(agentId, out var profile) && profile != null)
            {
                var changed = false;
                if (string.IsNullOrWhiteSpace(profile.AgentId))
                {
                    profile.AgentId = agentId;
                    changed = true;
                }

                profile.OllamaKeys ??= new List<OllamaKeyEntry>();
                if (profile.OllamaKeys.Count == 0 && !string.IsNullOrWhiteSpace(profile.OllamaApiKey))
                {
                    profile.OllamaKeys.Add(new OllamaKeyEntry
                    {
                        Name = "Key 1",
                        Key = profile.OllamaApiKey.Trim(),
                        Enabled = true
                    });
                    changed = true;
                }

                return changed;
            }

            profiles[agentId] = new KokoAgentLlmProfile
            {
                AgentId = agentId,
                Enabled = true,
                Temperature = temperature
            };
            return true;
        }

        public void Save()
        {
            if (!TrySave(out var error))
                KokoSystemLog.Write("APPSETTINGS-CATCH", "Save failed: " + error);
        }

        public bool TrySave(out string error)
        {
            lock (SettingsIoLock)
            {
                string? tempPath = null;
                try
                {
                    var dir = Path.GetDirectoryName(_path)!;
                    Directory.CreateDirectory(dir);
                    tempPath = Path.Combine(dir, $"settings.{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(tempPath, JsonConvert.SerializeObject(this, Formatting.Indented));
                    if (File.Exists(_path))
                        File.Replace(tempPath, _path, _backupPath, ignoreMetadataErrors: true);
                    else
                        File.Move(tempPath, _path);
                    error = "";
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                    return false;
                }
            }
        }
    }
}
