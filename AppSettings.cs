using System;
using System.Collections.Generic;
using System.IO;
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

    public class AppSettings
    {
        public const string DefaultOllamaCloudModel = "gemma4:31b-cloud";
        public const string DefaultVisionModel = "gemma4:31b-cloud";
        public const string FallbackVisionModel = "";

        // LLM Provider: "lmstudio" | "claude" | "ollama-cloud"
        public string LlmProvider { get; set; } = "lmstudio";

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

        // OpenAI (for Whisper STT)
        public string OpenAiApiKey { get; set; } = "";

        // Vault
        public string VaultPath { get; set; } = @"O:\App\Obsidian\MyBrain\Yasus";

        // Telegram Bot (старий)
        public string TelegramToken   { get; set; } = "";
        public long   TelegramChatId  { get; set; } = 0;
        public bool   TelegramEnabled { get; set; } = true;

        // Mini App (TG Web App)
        public bool   MiniAppEnabled    { get; set; } = true;
        public int    MiniAppPort       { get; set; } = 8765;
        // Публічний HTTPS URL з тунелю (ngrok / cloudflared). Без слешу в кінці.
        // Приклад: https://koko.example.trycloudflare.com
        public string MiniAppPublicUrl  { get; set; } = "";

        // Telegram UserClient (MTProto — повний акаунт)
        public int    TgApiId         { get; set; } = 0;
        public string TgApiHash       { get; set; } = "";
        public string TgPhone         { get; set; } = "";
        public bool   TgUserEnabled   { get; set; } = false;
        // Коконое відповідає тільки на DM (false = і групи теж)
        public bool   TgDmOnly        { get; set; } = false;

        // Spontaneous messages
        public bool SpontaneousEnabled      { get; set; } = true;
        public int  SpontaneousIntervalMins { get; set; } = 25;
        public int  ProactiveAutonomyLevel  { get; set; } = 3; // 0=тихо, 1=обережно, 2=нормально, 3=живий режим

        // Screen awareness
        public bool ScreenAwarenessEnabled { get; set; } = true;
        public bool ScreenAwarenessSendComments { get; set; } = true;
        public int  ScreenAwarenessIntervalMins { get; set; } = 30;
        public int  ScreenAwarenessCommentCooldownMins { get; set; } = 30;
        public int  GameScreenAwarenessIntervalMins { get; set; } = 5;
        public int  GameScreenAwarenessCommentCooldownMins { get; set; } = 10;

        // System
        public bool MinimizeToTray      { get; set; } = true;
        public int  MemoryUpdateEveryN  { get; set; } = 6;
        public string MatrixColor       { get; set; } = "#6366F1";

        // Voice
        public bool VoiceInputEnabled { get; set; } = true;
        public bool TtsEnabled        { get; set; } = false;

        // ── persistence ──────────────────────────────────────────
        // Зберігаємо в AppData щоб налаштування не збивались при білді/деплої
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KokonoeAssistant",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var loaded = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(_path))
                                 ?? new AppSettings();
                    if (NormalizeDefaults(loaded))
                        loaded.Save();
                    return loaded;
                }
            }
            catch { }

            var def = new AppSettings();
            def.Save();
            return def;
        }

        private static bool NormalizeDefaults(AppSettings settings)
        {
            var changed = false;

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

            if (settings.ScreenAwarenessIntervalMins < 30)
            {
                settings.ScreenAwarenessIntervalMins = 30;
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

            return changed;
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_path)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(_path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { }
        }
    }
}
