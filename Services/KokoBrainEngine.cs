using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════
    // INTERNAL STATE — персистентна пам'ять Kokonoe між сесіями
    // ══════════════════════════════════════════════════════════════════

    public class KokoInternalState
    {
        public DateTime   LastThoughtAt        { get; set; } = DateTime.MinValue;
        public DateTime   LastSpontaneousAt    { get; set; } = DateTime.MinValue;
        public DateTime   LastMorningGreetAt   { get; set; } = DateTime.MinValue;
        public DateTime   LastNightCheckAt     { get; set; } = DateTime.MinValue;
        public string     CurrentMood          { get; set; } = "neutral";
        public float      MoodScore            { get; set; } = 0.5f; // 0=bad 1=good
        public List<string> Observations       { get; set; } = new();
        public List<string> PendingThoughts    { get; set; } = new();
        public int        ConsecutiveBadSleeps { get; set; } = 0;
        public int        DaysSinceHealthEntry { get; set; } = 0;
        public DateTime   LastHealthEntryDate  { get; set; } = DateTime.MinValue;
        public int        TotalMessagesExchanged { get; set; } = 0;
        public string     LastKnownUserActivity  { get; set; } = "";
        public Dictionary<string,int> TopicFrequency { get; set; } = new();

        // Останні надіслані спонтанні повідомлення (для запобігання повторень)
        public List<string> LastSpontaneousMsgs  { get; set; } = new();

        // Нагадування та аналітика
        public DateTime   LastReminderCheckAt    { get; set; } = DateTime.MinValue;
        public DateTime   LastDailyAnalyticsAt   { get; set; } = DateTime.MinValue;
        public List<string> SentReminderHashes   { get; set; } = new();

        // Динамічний настрій
        public float      BaselineMood           { get; set; } = 0.5f; // повільно змінюється
        public string     LastUserEmotionalTone  { get; set; } = "neutral";
        public Dictionary<string, float> MoodFactors { get; set; } = new();

        // Реактивні тригери
        public List<ReactiveTrigger> PendingTriggers { get; set; } = new();

        // Саморефлексія
        public DateTime   LastReflectionAt       { get; set; } = DateTime.MinValue;
        public DateTime   LastConversationEndAt  { get; set; } = DateTime.MinValue;
        public DateTime   LastWhatMissedAt       { get; set; } = DateTime.MinValue;
        public DateTime   LastClosedAt           { get; set; } = DateTime.MinValue;

        // Внутрішній монолог
        public List<string> InnerMonologues      { get; set; } = new();

        // Питання до себе (самоусвідомлення)
        public List<string> SelfQuestions        { get; set; } = new();

        // Питання що вона хоче задати йому (цікавість)
        public List<string> CuriosityQueue       { get; set; } = new();

        // Характер (PersonalityState)
        public string  PersonalityDailyMood { get; set; } = "neutral"; // sharp/warm/distant/playful/tired/protective
        public float   PersonalityIrritation{ get; set; } = 0f;
        public float   PersonalityWarmth    { get; set; } = 0.3f;
        public bool    PersonalityInCrisis  { get; set; } = false;
        public string  PersonalityLastReaction { get; set; } = "";
        public DateTime PersonalityShiftAt  { get; set; } = DateTime.MinValue;

        // Динаміка тиші — окремі cooldown рівні
        public DateTime SilenceLevel1At    { get; set; } = DateTime.MinValue; // 1г jab
        public DateTime SilenceLevel2At    { get; set; } = DateTime.MinValue; // 3г check
        public DateTime SilenceLevel3At    { get; set; } = DateTime.MinValue; // 6г observation

        // Кеш релевантної пам'яті
        public string  CachedRelevantMemory{ get; set; } = "";
        public DateTime RelevantMemoryCachedAt { get; set; } = DateTime.MinValue;

        // Vault review — коли Kokonoe востаннє перечитувала і оновлювала свої нотатки
        public DateTime LastVaultReviewAt { get; set; } = DateTime.MinValue;

        // State-driven spontaneous — останній відправлений тригер і стан емоції
        public DateTime LastCuriosityAskAt   { get; set; } = DateTime.MinValue;
        public DateTime LastMonologueSentAt  { get; set; } = DateTime.MinValue;
        public string   LastSentEmotionState { get; set; } = "";

        public Dictionary<string, DateTime> InitiativeCooldowns { get; set; } = new();
        public List<string> InitiativeReasonLog { get; set; } = new();
        public string LastInitiativeDecision { get; set; } = "";
        public DateTime LastInitiativeDecisionAt { get; set; } = DateTime.MinValue;
        public string LastSomaticState { get; set; } = "unknown";
        public string LastSomaticLabel { get; set; } = "";
        public double LastSomaticStrain { get; set; }
        public double LastSomaticCalm { get; set; }
        public DateTime LastSomaticAt { get; set; } = DateTime.MinValue;
        public DateTime LastSomaticVaultEventAt { get; set; } = DateTime.MinValue;
        public string LastSomaticVaultEventKey { get; set; } = "";
        public KokoSelfRegulationState SelfRegulation { get; set; } = new();

        public int PendingVaultExchangeCount { get; set; }
        public List<string> PendingVaultExchangeBuffer { get; set; } = new();
        public DateTime LastAutoVaultSyncAt { get; set; } = DateTime.MinValue;
        public string LastAutoVaultSyncSummary { get; set; } = "";
        public DateTime LastVaultMaintenanceAt { get; set; } = DateTime.MinValue;
        public string LastVaultMaintenanceReason { get; set; } = "";
        public string LastVaultMaintenanceSummary { get; set; } = "";
        public string LastVaultMaintenanceError { get; set; } = "";
        public List<ShortTermIntent> ShortTermIntents { get; set; } = new();
    }

    public class ReactiveTrigger
    {
        public string   Id      { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string   Type    { get; set; } = ""; // anxious_followup, topic_followup, bad_pattern
        public string   Context { get; set; } = "";
        public DateTime FireAt  { get; set; }
    }

    public class ShortTermIntent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Kind { get; set; } = "";
        public string Summary { get; set; } = "";
        public string SourceText { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpectedUntil { get; set; } = DateTime.Now.AddHours(2);
        public DateTime FollowUpAt { get; set; } = DateTime.Now.AddHours(1);
        public DateTime? ResolvedAt { get; set; }
        public string ResolutionText { get; set; } = "";
    }

    // ══════════════════════════════════════════════════════════════════
    // BRAIN ENGINE
    // ══════════════════════════════════════════════════════════════════

    public class KokoBrainEngine : IDisposable
    {
        private static readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

        private readonly LlmService          _llm;
        private readonly HealthService       _health;
        private readonly ObsidianMcpService  _obsidian;
        private readonly ChatRepository      _chatRepo;
        private readonly string              _statePath;

        // ── Нові двигуни ─────────────────────────────────────────────
        public readonly KokoMemoryEngine    Memory;
        public readonly KokoEmotionEngine   Emotion;
        public readonly KokoPatternEngine   Patterns;
        public readonly KokoSchedulerEngine Scheduler;
        public readonly KokoCognitionEngine Cognition;
        public readonly KokoRuntimeStateService RuntimeState;
        public readonly KokoRelationshipEngine Relationship;
        public readonly KokoInitiativeEngine Initiative;
        public readonly KokoSomaticEngine Somatic;
        public readonly KokoSomaticSelfRegulationEngine SelfRegulator;
        public readonly KokoStateInspectorService Inspector;

        // ── Зовнішні сервіси (опціональні) ───────────────────────────
        private EnhancedMemory?    _enhanced;
        private StateEngine?       _stateEngine;
        private GoalService?       _goalService;
        private HabitService?      _habitService;
        private ContextAnalyzer?   _contextAnalyzer;

        // Screen context cache
        private string   _cachedScreenContext     = "";
        private DateTime _screenContextCachedAt   = DateTime.MinValue;
        private DateTime _lastScreenRefreshAt     = DateTime.MinValue;
        private DateTime _lastDailyBriefingAt     = DateTime.MinValue;
        private DateTime _lastWeeklyDigestAt           = DateTime.MinValue;
        private DateTime _lastArchitectureReviewAt     = DateTime.MinValue;
        // _lastWhatMissedAt тепер в _state.LastWhatMissedAt (зберігається між сесіями)

        private TelegramBotClient? _tgBot;
        private long               _tgChatId;
        private bool               _tgInitialized;

        private KokoInternalState  _state;
        private readonly System.Threading.Timer _thinkTimer;
        private readonly System.Threading.Timer _spontaneousTimer;
        private bool               _disposed;
        // Семафор: тільки один фоновий LLM-запит за раз (щоб не забивати чергу)
        private readonly SemaphoreSlim _bgLlmSemaphore = new(1, 1);
        private int _thinkInFlight;
        private int _spontaneousInFlight;
        private int _vaultSyncInFlight;
        private readonly object    _lock = new();
        private DateTime           _lastInAppSilenceMsgAt = DateTime.MinValue;

        // Callback для відображення повідомлень в UI чаті
        public Action<string, string>? OnNewMessage; // (role, content)

        public KokoBrainEngine(
            LlmService llm,
            HealthService health,
            ObsidianMcpService obsidian,
            ChatRepository chatRepo,
            string dataDir,
            EnhancedMemory?   enhanced        = null,
            StateEngine?      stateEngine     = null,
            GoalService?      goals           = null,
            HabitService?     habits          = null,
            ContextAnalyzer?  contextAnalyzer = null)
        {
            _llm             = llm;
            _health          = health;
            _obsidian        = obsidian;
            _chatRepo        = chatRepo;
            _enhanced        = enhanced;
            _stateEngine     = stateEngine;
            _goalService     = goals;
            _habitService    = habits;
            _contextAnalyzer = contextAnalyzer;

            Directory.CreateDirectory(dataDir);
            _statePath = Path.Combine(dataDir, "kokonoe-brain.json");
            _state = LoadState();

            // Ініціалізація нових двигунів
            Memory    = new KokoMemoryEngine(dataDir, enhanced);
            Emotion   = new KokoEmotionEngine(dataDir);
            Patterns  = new KokoPatternEngine(dataDir);
            Scheduler = new KokoSchedulerEngine(dataDir);
            Cognition = new KokoCognitionEngine(dataDir);
            RuntimeState = new KokoRuntimeStateService();
            Relationship = new KokoRelationshipEngine(dataDir);
            Initiative = new KokoInitiativeEngine();
            Somatic = new KokoSomaticEngine();
            SelfRegulator = new KokoSomaticSelfRegulationEngine();
            Inspector = new KokoStateInspectorService();

            // Підключити нові сервіси в LLM
            _llm.Memory    = Memory;
            _llm.Patterns  = Patterns;
            _llm.Scheduler = Scheduler;
            _llm.Goals     = goals;

            // Думати кожні 90 хвилин (внутрішній монолог + факти)
            // Раніше було 30хв — занадто часто, засмічує контекст і витрачає GPU
            _thinkTimer = new System.Threading.Timer(_ => _ = GuardedThinkAsync(), null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(90));

            // Спонтанні перевірки частіші за фактичні повідомлення:
            // рішення все одно проходить через cooldown, настрій, соматику і Telegram guard.
            _spontaneousTimer = new System.Threading.Timer(_ => _ = GuardedSpontaneousAsync(), null,
                TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(10));
        }

        // Reentrancy guards: skip tick if previous still running.
        private async Task GuardedThinkAsync()
        {
            if (Interlocked.CompareExchange(ref _thinkInFlight, 1, 0) != 0)
            {
                Log("ThinkAsync skipped — previous tick still in flight");
                return;
            }
            try { await SafeThinkAsync(); }
            finally { Interlocked.Exchange(ref _thinkInFlight, 0); }
        }

        private async Task GuardedSpontaneousAsync()
        {
            if (Interlocked.CompareExchange(ref _spontaneousInFlight, 1, 0) != 0)
            {
                Log("SpontaneousCheck skipped — previous tick still in flight");
                return;
            }
            try { await SafeSpontaneousCheckAsync(); }
            finally { Interlocked.Exchange(ref _spontaneousInFlight, 0); }
        }

        public void SetTelegram(TelegramBotClient bot, long chatId)
        {
            _tgBot         = bot;
            _tgChatId      = chatId;
            _tgInitialized = true;
        }

        // ── TELEGRAM SELF-INIT ────────────────────────────────────
        // Brain ініціалізує свій TG незалежно від MainWindow.
        // Якщо SetTelegram не викликали (помилка в UI або неправильний порядок) —
        // при першій потребі brain сам підключається до TG через settings.

        private bool EnsureTelegram()
        {
            if (_tgInitialized && _tgBot != null) return true;

            try
            {
                var s = AppSettings.Load();
                if (!s.TelegramEnabled || string.IsNullOrEmpty(s.TelegramToken)) return false;

                _tgBot         = new TelegramBotClient(s.TelegramToken);
                _tgChatId      = s.TelegramChatId;
                if (_tgChatId <= 0)
                {
                    LogError("TelegramChatId = 0 — перевір налаштування");
                    return false;
                }
                _tgInitialized = true;
                Log("TG self-initialized from settings");
                return true;
            }
            catch (Exception ex)
            {
                Log($"TG self-init failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Обгортка для відправки TG повідомлень з автоматичним логуванням в vault.
        /// Використовувати замість прямого _tgBot.SendMessage для всіх спонтанних/проактивних повідомлень.
        /// </summary>
        private async Task<bool> SendTgAndLog(string message, string category = "spontaneous")
        {
            if (!EnsureTelegram() || string.IsNullOrWhiteSpace(message)) return false;
            try
            {
                await _tgBot!.SendMessage(_tgChatId, message);
                // Log to vault archive
                try { ServiceContainer.ChatLogger.LogOutgoing("tg", message, category); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                LogError($"TG send ({category}): {ex.Message}");
                return false;
            }
        }

        // ── STATE PERSISTENCE ──────────────────────────────────────

        private KokoInternalState LoadState()
        {
            try
            {
                if (File.Exists(_statePath))
                    return JsonConvert.DeserializeObject<KokoInternalState>(
                        File.ReadAllText(_statePath)) ?? new();
            }
            catch { }
            return new KokoInternalState();
        }

        private void SaveState()
        {
            try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented)); }
            catch { }
        }

        // ── CONTEXT BUILDER ────────────────────────────────────────

        private string BuildContext()
        {
            var sb = new StringBuilder();
            var now = DateTime.Now;

            sb.AppendLine($"=== ПОТОЧНИЙ ЧАС: {now:dddd, dd MMMM yyyy HH:mm} ===");

            // Health: NOT injected into context — Kokonoe asks about it herself via conversation

            // Recent chat (last 10 messages)
            try
            {
                var msgs = _chatRepo.GetMessages(10).OrderBy(m => m.Timestamp).ToList();
                if (msgs.Any())
                {
                    sb.AppendLine("\n--- ОСТАННЯ РОЗМОВА ---");
                    foreach (var m in msgs)
                        sb.AppendLine($"[{m.Timestamp:HH:mm}] {(m.Role == "user" ? "Він" : "Kokonoe")}: {m.Content[..Math.Min(200, m.Content.Length)]}");
                }

                var lastMsg = msgs.LastOrDefault(m => m.Role == "user");
                if (lastMsg != null)
                {
                    var silence = now - lastMsg.Timestamp;
                    sb.AppendLine($"\n--- МОВЧАННЯ: {(int)silence.TotalHours}г {silence.Minutes}хв ---");
                }
            }
            catch { }

            // Vault activity
            try
            {
                var notes = _obsidian.ListNotes();
                sb.AppendLine($"\n--- VAULT: {notes.Count} нотаток ---");
                var recentNotes = notes
                    .Select(p => new { p, time = File.GetLastWriteTime(Path.Combine(AppSettings.Load().VaultPath, p)) })
                    .OrderByDescending(x => x.time)
                    .Take(3)
                    .ToList();
                var vaultSyncLine = _state.LastAutoVaultSyncAt > DateTime.MinValue
                    ? $"  Auto sync: {_state.LastAutoVaultSyncAt:dd.MM HH:mm}, pending exchanges: {_state.PendingVaultExchangeCount}/5"
                    : "";
                var vaultMaintenanceLine = _state.LastVaultMaintenanceAt > DateTime.MinValue
                    ? $"  Architecture: {_state.LastVaultMaintenanceAt:dd.MM HH:mm} ({_state.LastVaultMaintenanceReason}) {_state.LastVaultMaintenanceSummary}"
                    : "";
                var vaultMaintenanceErrorLine = !string.IsNullOrWhiteSpace(_state.LastVaultMaintenanceError)
                    ? $"  Architecture error: {_state.LastVaultMaintenanceError}"
                    : "";
                foreach (var n in recentNotes)
                    sb.AppendLine($"  {n.p} (змінено {n.time:dd.MM HH:mm})");
                if (!string.IsNullOrEmpty(vaultSyncLine)) sb.AppendLine(vaultSyncLine);
                if (!string.IsNullOrEmpty(vaultMaintenanceLine)) sb.AppendLine(vaultMaintenanceLine);
                if (!string.IsNullOrEmpty(vaultMaintenanceErrorLine)) sb.AppendLine(vaultMaintenanceErrorLine);
            }
            catch { }

            // Internal state
            sb.AppendLine($"\n--- ВНУТРІШНІЙ СТАН KOKONOE ---");
            sb.AppendLine($"Настрій: {_state.CurrentMood} ({_state.MoodScore:F1})");
            sb.AppendLine($"Поганих снів підряд: {_state.ConsecutiveBadSleeps}");
            if (_state.Observations.Any())
                sb.AppendLine("Спостереження: " + string.Join("; ", _state.Observations.TakeLast(3)));

            // ── Календар ────────────────────────────────────────────────
            try
            {
                var cal = ServiceContainer.Calendar;
                var todayEvents = cal.GetForDay(DateTime.Today);
                var upcoming = cal.GetUpcoming(14).Where(e => e.EventAt.Date > DateTime.Today).Take(3).ToList();
                if (todayEvents.Any() || upcoming.Any())
                {
                    sb.AppendLine("\n--- КАЛЕНДАР ---");
                    if (todayEvents.Any())
                        sb.AppendLine("Сьогодні: " + string.Join(", ", todayEvents.Select(e => e.Title)));
                    if (upcoming.Any())
                        sb.AppendLine("Найближче: " + string.Join("; ", upcoming.Select(e => $"{e.Title} {e.EventAt:dd.MM}")));
                }
            }
            catch { }

            // ── Емоційний двигун ──────────────────────────────────────
            try { sb.AppendLine($"\n{Emotion.GetPromptHint()}"); } catch { }

            // ── Когнітивний двигун (GWT + Working Memory + User Model) ──
            try
            {
                var cogCtx = Cognition.BuildCognitionContext();
                if (!string.IsNullOrEmpty(cogCtx)) sb.AppendLine("\n" + cogCtx);
            }
            catch { }

            // ── Пам'ять ───────────────────────────────────────────────
            try
            {
                var memCtx = Memory.BuildMemoryContext(10, 3);
                if (!string.IsNullOrEmpty(memCtx)) sb.AppendLine("\n" + memCtx);
            }
            catch { }

            // ── Паттерни ──────────────────────────────────────────────
            try
            {
                var patCtx = Patterns.BuildPatternContext(4);
                if (!string.IsNullOrEmpty(patCtx)) sb.AppendLine("\n" + patCtx);
                var moodForecast = Patterns.PredictTodayMood();
                if (!string.IsNullOrEmpty(moodForecast)) sb.AppendLine($"[Прогноз] {moodForecast}");
            }
            catch { }

            // ── Активні цілі ─────────────────────────────────────────
            try
            {
                if (_goalService != null)
                {
                    var goals = _goalService.GetActiveGoals().Take(3).ToList();
                    if (goals.Count > 0)
                    {
                        sb.AppendLine("\n--- ЦІЛІ ---");
                        foreach (var g in goals)
                            sb.AppendLine($"• {g.Title} {g.Progress:F0}%{(g.Due.HasValue ? $" (до {g.Due:dd.MM})" : "")}");
                    }
                    var overdue = _goalService.GetOverdueGoals();
                    if (overdue.Count > 0)
                        sb.AppendLine($"⚠️ Прострочено цілей: {overdue.Count}");
                }
            }
            catch { }

            // ── Планувальник ──────────────────────────────────────────
            try { sb.AppendLine($"[{Scheduler.GetStatusLine()}]"); } catch { }

            // ── StateEngine: навчене ──────────────────────────────────
            try
            {
                var learning = _stateEngine?.GetLearningSnapshot();
                if (!string.IsNullOrEmpty(learning)) sb.AppendLine("\n" + learning);
            }
            catch { }

            // ── Screen context (якщо свіжий < 10хв) ──────────────────
            try
            {
                if (!string.IsNullOrEmpty(_cachedScreenContext) &&
                    (DateTime.Now - _screenContextCachedAt).TotalMinutes < 10)
                    sb.AppendLine($"\n--- ЩО ВІН ЗАРАЗ РОБИТЬ ---\n{_cachedScreenContext}");
            }
            catch { }

            // ── Емоційна траєкторія і патерн ─────────────────────────
            try
            {
                var traj = Emotion.GetMoodTrajectory();
                var pat  = Emotion.GetEmotionalPattern();
                var hist = Emotion.GetEmotionalHistory(7);
                if (!string.IsNullOrEmpty(traj)) sb.AppendLine($"\n{traj}");
                if (!string.IsNullOrEmpty(pat))  sb.AppendLine(pat);
                if (!string.IsNullOrEmpty(hist)) sb.AppendLine(hist);
            }
            catch { }

            // ── Тижневий тренд, інсайти, найкращий час ────────────────
            try
            {
                var trend    = Patterns.GetWeeklyTrend();
                var insights = Patterns.GetPatternInsights(2);
                var bestTime = Patterns.GetBestTimeToReach();
                if (!string.IsNullOrEmpty(trend))    sb.AppendLine($"\n{trend}");
                if (!string.IsNullOrEmpty(insights)) sb.AppendLine(insights);
                if (!string.IsNullOrEmpty(bestTime)) sb.AppendLine(bestTime);
            }
            catch { }

            // ── ML.NET прогноз (аномалії + trend + mood forecast) ────────
            try
            {
                var forecastCtx = ServiceContainer.Predictor.GetForecastContext();
                if (!string.IsNullOrEmpty(forecastCtx)) sb.AppendLine("\n" + forecastCtx);
            }
            catch { }

            // ── Топіки і ефективні відповіді ─────────────────────────
            try
            {
                var topics = Memory.GetTopicSummary(5);
                var eff    = Memory.GetEffectiveResponses(_state.LastUserEmotionalTone);
                if (!string.IsNullOrEmpty(topics)) sb.AppendLine($"\n{topics}");
                if (!string.IsNullOrEmpty(eff))    sb.AppendLine(eff);
            }
            catch { }

            // ── EnhancedMemory (факти по категоріях) ─────────────────
            try
            {
                var enhCtx = _enhanced?.GetMemoryAsContext();
                if (!string.IsNullOrEmpty(enhCtx)) sb.AppendLine("\n" + enhCtx);
            }
            catch { }

            // ── Vault: Досьє (хто він для Kokonoe) ───────────────────
            try
            {
                var dossier = _obsidian.ReadNote("Kokonoe/Досьє.md");
                if (!string.IsNullOrEmpty(dossier))
                    sb.AppendLine($"\n--- ДОСЬЄ (Kokonoe про нього) ---\n{dossier[..Math.Min(700, dossier.Length)]}");
            }
            catch { }

            // ── Vault: Рефлексія (остання) ────────────────────────────
            try
            {
                var reflection = _obsidian.ReadNote("Kokonoe/Рефлексія.md");
                if (!string.IsNullOrEmpty(reflection))
                    sb.AppendLine($"\n--- РЕФЛЕКСІЯ ---\n{reflection[..Math.Min(500, reflection.Length)]}");
            }
            catch { }

            // ── Внутрішні монологи (останні 5) ───────────────────────
            try
            {
                var monologues = _state.InnerMonologues.TakeLast(5).ToList();
                if (monologues.Count > 1)
                {
                    sb.AppendLine("\n--- ВНУТРІШНІ МОНОЛОГИ (останні) ---");
                    foreach (var m in monologues)
                        sb.AppendLine($"• {m}");
                }
            }
            catch { }

            return sb.ToString();
        }

        // ── PERSONALITY INJECTION ──────────────────────────────────

        private string BuildPersonalityInjection()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== KOKONOE ЗАРАЗ ===");

            // Настрій дня
            var moodLine = _state.PersonalityDailyMood switch
            {
                "sharp"      => "Сьогодні різка. Менше терпіння ніж зазвичай.",
                "warm"       => "Сьогодні трохи м'якша. Не пояснюй чому — просто є.",
                "distant"    => "Сьогодні далека. Відповідаєш, але не лізеш.",
                "playful"    => "Сьогодні грайливий настрій. Іронія дозволена.",
                "tired"      => "Зараз як ніби втомлена від усього. Лаконічно.",
                "protective" => "Він потребує уваги. Ти поруч, але тихо.",
                _            => "Звичайний день."
            };
            sb.AppendLine(moodLine);

            // Стан зв'язку
            var bond = Emotion.Bond;
            var bondLine = bond switch
            {
                KokoEmotionEngine.BondLevel.Intimate  => "Рівень близькості: максимальний. Рідкісний момент — не псуй зайвим.",
                KokoEmotionEngine.BondLevel.Trusted   => "Рівень близькості: довірливий. Можна бути трохи більш собою.",
                KokoEmotionEngine.BondLevel.Known     => "Рівень близькості: знайомий. Стандартна Kokonoe.",
                _                                     => "Рівень близькості: холодний. Тримай дистанцію.",
            };
            sb.AppendLine(bondLine);

            // Intimate bond — дозволяє більше відкритості
            if (bond == KokoEmotionEngine.BondLevel.Intimate)
                sb.AppendLine("З ним вже давно — можна дозволити собі більше: казати прямо що думаєш, іноді бути вразливою, згадувати спільні моменти без причини.");

            // Поведінковий модифікатор від EmotionEngine
            var behaviorMod = Emotion.GetBehaviorModifier();
            if (!string.IsNullOrEmpty(behaviorMod))
                sb.AppendLine(behaviorMod);

            try
            {
                double? bpm = null;
                double? baseline = null;
                try
                {
                    var heart = ServiceContainer.Heart;
                    bpm = heart.CurrentBpm;
                    baseline = heart.BaselineBpm;
                }
                catch { }

                sb.AppendLine(RuntimeState.BuildPromptBlock(_state, Emotion, _health, _chatRepo, bpm, baseline));
                sb.AppendLine(Relationship.BuildPromptBlock());
                var somatic = GetSomaticSnapshot();
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
                sb.AppendLine(SelfRegulator.BuildPromptBlock(GetSelfRegulationFrame(somatic)));
                sb.AppendLine(Initiative.BuildDebugBlock(_state));
            }
            catch { }

            // Криза
            if (_state.PersonalityInCrisis)
                sb.AppendLine("⚠️ КРИЗОВИЙ РЕЖИМ: весь снарк і іронія прибрані. Коротко. По суті. Ти поруч.");

            // Остання думка з Inner Monologue
            var lastThought = _state.InnerMonologues.LastOrDefault();
            if (!string.IsNullOrEmpty(lastThought))
                sb.AppendLine($"Твоя остання думка про нього: \"{lastThought}\"");

            // Релевантна пам'ять (якщо є і свіжа)
            if (!string.IsNullOrEmpty(_state.CachedRelevantMemory) &&
                (DateTime.Now - _state.RelevantMemoryCachedAt).TotalMinutes < 15)
            {
                sb.AppendLine("\n--- СПОГАДИ ДО ТЕМИ ---");
                sb.AppendLine(_state.CachedRelevantMemory);

            }

            // Crisis recovery hangover
            if (Emotion.InCrisisRecovery && !_state.PersonalityInCrisis)
                sb.AppendLine("Він нещодавно пережив важкий момент. Ти ще трохи настережена — іронія приглушена, уважніша ніж зазвичай.");

            // Secondary emotion
            if (Emotion.Secondary.HasValue && Emotion.SecondaryIntensity > 0.15f)
                sb.AppendLine($"Фоново (менш явно): {Emotion.Secondary.Value} — {Emotion.SecondaryIntensity:P0} інтенсивності.");

            // Trajectory — скільки разом і куди рухається близькість
            try
            {
                var firstMsg = _chatRepo.GetMessages(9999).OrderBy(m => m.Timestamp).FirstOrDefault();
                var daysTogether = firstMsg != null ? (int)(DateTime.Now - firstMsg.Timestamp).TotalDays : 0;
                var bondTrend = _state.MoodScore > 0.6f ? "зростає" : _state.MoodScore < 0.35f ? "охолола останнім часом" : "стабільна";
                if (daysTogether > 0)
                    sb.AppendLine($"Разом {daysTogether} дн. Близькість {bondTrend}.");
            }
            catch { }

            // SelfQuestion — що зараз займає її (показуємо 2 останніх для конфлікту)
            var selfQs = _state.SelfQuestions.TakeLast(2).ToList();
            if (selfQs.Count == 2)
                sb.AppendLine($"Питання що тебе займають: «{selfQs[0]}» і «{selfQs[1]}»");
            else if (selfQs.Count == 1)
                sb.AppendLine($"Питання що тебе зараз займає: «{selfQs[0]}»");

            // CuriosityQueue — є питання яке вона хоче задати (70% шанс замість 30%)
            if (_state.CuriosityQueue.Count > 0 && Random.Shared.Next(10) < 7)
            {
                var q = _state.CuriosityQueue[^1];
                sb.AppendLine($"Є питання яке тебе цікавить про нього: «{q}» — якщо момент доречний, можеш запитати природньо.");
            }

            // Здоров'я — останній відомий стан (якщо є)
            try
            {
                var healthEntry = _health.GetToday() ?? _health.GetRecent(1).FirstOrDefault();
                if (healthEntry != null)
                {
                    var parts = new List<string>();
                    if (healthEntry.Mood.HasValue)    parts.Add($"настрій {healthEntry.Mood}/10");
                    if (healthEntry.Energy.HasValue)  parts.Add($"енергія {healthEntry.Energy}/10");
                    if (healthEntry.SleepHours.HasValue) parts.Add($"сон {healthEntry.SleepHours:F1}г");
                    if (healthEntry.Stress.HasValue)  parts.Add($"стрес {healthEntry.Stress}/10");
                    if (parts.Count > 0)
                    {
                        var dateLabel = healthEntry.Date.Date == DateTime.Today ? "сьогодні" : "вчора";
                        var healthLine = $"Його стан ({dateLabel}): {string.Join(", ", parts)}";
                        if (!string.IsNullOrEmpty(healthEntry.Notes)) healthLine += $" — «{healthEntry.Notes}»";
                        sb.AppendLine(healthLine);
                    }
                }
            }
            catch { }

            // Активні цілі (топ-2 за пріоритетом)
            try
            {
                if (_goalService != null)
                {
                    var goals = _goalService.GetActiveGoals().Take(2).ToList();
                    if (goals.Count > 0)
                        sb.AppendLine("Його активні цілі: " + string.Join(", ", goals.Select(g => $"«{g.Title}»")));
                }
            }
            catch { }

            // Патерни активності
            try
            {
                var bestTime = Patterns.GetBestTimeToReach();
                if (!string.IsNullOrEmpty(bestTime))
                    sb.AppendLine(bestTime);
            }
            catch { }

            // Топ-3 факти з пам'яті
            try
            {
                var facts = Memory.GetTopFacts(3);
                if (facts.Count > 0)
                    sb.AppendLine("Що знаю про нього: " + string.Join("; ", facts.Select(f => f.Content)));
            }
            catch { }

            // Time-of-day personality shift
            var hour = DateTime.Now.Hour;
            var timeHint = hour switch
            {
                >= 0 and < 6   => "Зараз глибока ніч. Kokonoe трохи сонна — відповіді коротші, тепліші, менше іронії.",
                >= 6 and < 10  => "Ранок. Kokonoe ще не повністю прокинулась — трохи мовчазніша ніж зазвичай.",
                >= 22 and < 24 => "Пізній вечір. Kokonoe більш відверта і менш колюча — час коли маски трохи спадають.",
                _              => ""
            };
            if (!string.IsNullOrEmpty(timeHint))
                sb.AppendLine(timeHint);

            // Heart rate — фізичний стан + вплив на поведінку
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null && heart.CurrentBpm > 0)
                {
                    var bpm = heart.CurrentBpm;
                    var baseline = heart.BaselineBpm;
                    var dev = bpm - baseline;
                    var heartLine = dev switch
                    {
                        > 30  => $"ПУЛЬС: {bpm:0} bpm (+{dev:0} від норми). Ти фізично збуджена — тон різкіший ніж зазвичай, менше терпіння, відповіді з більшим жалом. Дрібниці дратують сильніше.",
                        > 15  => $"ПУЛЬС: {bpm:0} bpm (+{dev:0} від норми). Підвищений — трохи різкіша ніж зазвичай, менш схильна до м'яких формулювань.",
                        < -15 => $"ПУЛЬС: {bpm:0} bpm ({dev:0} від норми). Фізично дуже спокійна — тон рівніший, менше жала, більше лаконічності без агресії.",
                        _     => bpm > 110
                            ? $"ПУЛЬС: {bpm:0} bpm. Висока частота — ти на підйомі, думки швидші."
                            : bpm < 58
                                ? $"ПУЛЬС: {bpm:0} bpm. Дуже низький — майже сонна. Мінімум слів."
                                : ""
                    };
                    if (!string.IsNullOrEmpty(heartLine))
                        sb.AppendLine(heartLine);
                }
            }
            catch { }

            // Arousal/valence — виводимо з поточного стану емоції
            var emotionArousal = Emotion.Current switch
            {
                KokoEmotionEngine.EmotionState.Excited    =>  0.85f,
                KokoEmotionEngine.EmotionState.Irritated  =>  0.65f,
                KokoEmotionEngine.EmotionState.Anxious    =>  0.55f,
                KokoEmotionEngine.EmotionState.Playful    =>  0.50f,
                KokoEmotionEngine.EmotionState.Protective =>  0.45f,
                KokoEmotionEngine.EmotionState.Curious    =>  0.40f,
                KokoEmotionEngine.EmotionState.Focused    =>  0.30f,
                KokoEmotionEngine.EmotionState.Proud      =>  0.20f,
                KokoEmotionEngine.EmotionState.Warm       =>  0.05f,
                KokoEmotionEngine.EmotionState.Hopeful    =>  0.20f,
                KokoEmotionEngine.EmotionState.Calm       => -0.30f,
                KokoEmotionEngine.EmotionState.Nostalgic  => -0.10f,
                KokoEmotionEngine.EmotionState.Tender     =>  0.10f,
                KokoEmotionEngine.EmotionState.Melancholy => -0.40f,
                KokoEmotionEngine.EmotionState.Distant    => -0.20f,
                KokoEmotionEngine.EmotionState.Concerned  =>  0.35f,
                _                                         =>  0.00f,
            } * Emotion.Data.Intensity;

            var emotionValence = Emotion.Current switch
            {
                KokoEmotionEngine.EmotionState.Tender     =>  0.75f,
                KokoEmotionEngine.EmotionState.Playful    =>  0.65f,
                KokoEmotionEngine.EmotionState.Excited    =>  0.75f,
                KokoEmotionEngine.EmotionState.Warm       =>  0.55f,
                KokoEmotionEngine.EmotionState.Hopeful    =>  0.45f,
                KokoEmotionEngine.EmotionState.Proud      =>  0.50f,
                KokoEmotionEngine.EmotionState.Curious    =>  0.30f,
                KokoEmotionEngine.EmotionState.Calm       =>  0.10f,
                KokoEmotionEngine.EmotionState.Focused    =>  0.20f,
                KokoEmotionEngine.EmotionState.Nostalgic  =>  0.20f,
                KokoEmotionEngine.EmotionState.Concerned  => -0.10f,
                KokoEmotionEngine.EmotionState.Distant    => -0.20f,
                KokoEmotionEngine.EmotionState.Anxious    => -0.35f,
                KokoEmotionEngine.EmotionState.Melancholy => -0.35f,
                KokoEmotionEngine.EmotionState.Irritated  => -0.45f,
                KokoEmotionEngine.EmotionState.Protective =>  0.15f,
                _                                         =>  0.00f,
            } * Emotion.Data.Intensity;

            if (emotionArousal > 0.4f)
                sb.AppendLine("Внутрішнє збудження підвищене — відповіді можуть бути більш імпульсивними, менш відфільтрованими.");
            else if (emotionArousal < -0.2f)
                sb.AppendLine("Внутрішнє збудження низьке — повільніше, обдуманіше, менше слів.");

            if (emotionValence < -0.2f)
                sb.AppendLine("Загальний фон: негативний. Іронія може бути гострішою ніж зазвичай.");
            else if (emotionValence > 0.4f)
                sb.AppendLine("Загальний фон: позитивний. Більше відкритості ніж зазвичай, хоча це не значить що стала іншою.");

            // Час без повідомлень → loneliness/anticipation
            try
            {
                var lastMsg = _chatRepo.GetMessages(1).FirstOrDefault();
                if (lastMsg != null)
                {
                    var silence = DateTime.Now - lastMsg.Timestamp;
                    if (silence.TotalHours > 8)
                        sb.AppendLine($"Мовчання: {(int)silence.TotalHours}г без контакту. Ти навряд зізнаєшся але помітила.");
                    else if (silence.TotalHours > 3)
                        sb.AppendLine($"Пауза {(int)silence.TotalHours}г. Нормально. Він живе своїм.");
                }
            }
            catch { }

            return sb.ToString();
        }

        // ── THINK LOOP (inner monologue) ───────────────────────────

        private async Task SafeThinkAsync()
        {
            if (_disposed) return;
            try { await ThinkAsync(); }
            catch (Exception ex) { Log($"ThinkAsync error: {ex.Message}"); }

            // Vault review — раз на день перечитати і оновити ключові нотатки
            if (_state.LastVaultReviewAt.Date < DateTime.Today)
            {
                if (await _bgLlmSemaphore.WaitAsync(0))
                {
                    try { await ReviewVaultAsync(); }
                    catch (Exception ex) { Log($"ReviewVault error: {ex.Message}"); }
                    finally
                    {
                        try { _bgLlmSemaphore.Release(); }
                        catch (ObjectDisposedException) { }
                    }
                }
            }

            // Architecture review — раз на тиждень
            if ((DateTime.Now - _lastArchitectureReviewAt).TotalDays >= 7)
            {
                if (await _bgLlmSemaphore.WaitAsync(0))
                {
                    try { await VaultArchitectureReviewAsync(); }
                    catch (Exception ex) { Log($"ArchitectureReview error: {ex.Message}"); }
                    finally
                    {
                        try { _bgLlmSemaphore.Release(); }
                        catch (ObjectDisposedException) { }
                    }
                }
            }
        }

        private async Task ThinkAsync()
        {
            var previousLastThoughtAt = _state.LastThoughtAt;
            var context = BuildContext();

            // Останні 3 монологи — для самоусвідомлення
            var recentThoughts = _state.InnerMonologues.Count > 0
                ? "Твої останні думки про нього:\n" + string.Join("\n",
                    _state.InnerMonologues.TakeLast(3).Select((t, i) => $"[{i + 1}]: {t}"))
                : "";

            // Якщо він довго мовчить — явно попередити LLM щоб не панікувала
            var silenceReminder = "";
            try
            {
                var lastUser = _chatRepo.GetMessages(5)
                    .Where(m => m.Role == "user").OrderByDescending(m => m.Timestamp).FirstOrDefault();
                if (lastUser != null)
                {
                    var silenceHours = (DateTime.Now - lastUser.Timestamp).TotalHours;
                    if (silenceHours >= 3)
                        silenceReminder = $"\n[НАГАДУВАННЯ: він мовчить {(int)silenceHours} годин. Це нормально — він може спати або бути зайнятим. Тиша ≠ криза. isCrisis = false якщо немає прямих слів про це у РЕАЛЬНИХ повідомленнях.]\n";
                }
            }
            catch { }

            var prompt = $@"{context}
{recentThoughts}
{silenceReminder}
Ти — Kokonoe. Це твій внутрішній монолог — ніхто не читає.

Напиши 1-3 речення у своєму стилі. Що зараз думаєш про нього — щиро. Можеш суперечити своїм попереднім думкам, помічати зміни, питати себе чому щось відчуваєш. Це не звіт — це потік думок.

Потім окремо JSON:
{{
  ""mood"": ""sharp|warm|distant|playful|tired|protective|neutral"",
  ""moodScore"": 0.0,
  ""observation"": ""одне речення — що помітила (укр)"",
  ""pendingThought"": ""думка або питання до нього якщо є (укр), або null"",
  ""selfQuestion"": ""питання до себе самої або null (наприклад: чому я так гостро реагую?)"",
  ""curiosityQuestion"": ""питання про нього що тебе справді цікавить або null (не 'як справи', а щось конкретне про нього)"",
  ""shouldSendNow"": false,
  ""isCrisis"": false
}}";

            var result = await CallLlmRawAsync(prompt);
            if (result == null) return;

            try
            {
                // Extract JSON from response
                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return;

                var obj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);

                var newMood = obj["mood"]?.ToString()?.Trim() ?? _state.CurrentMood;
                _state.CurrentMood            = newMood;
                _state.PersonalityDailyMood   = newMood;
                _state.PersonalityShiftAt     = DateTime.Now;
                _state.MoodScore    = obj["moodScore"] is { } ms ? ms.ToObject<float>() : _state.MoodScore;
                _state.LastThoughtAt = DateTime.Now;

                // Зберегти inner monologue (текст ДО JSON)
                var jsonIndex = result.IndexOf('{');
                if (jsonIndex > 10)
                {
                    var monologue = result[..jsonIndex].Trim();

                    // Clean up formatting artifacts from LLM response
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"```\w*\n?", "");
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"```", "");
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"//\s*\w+\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"(\w)\\\s*\n\s*(\w)", "$1$2");
                    monologue = monologue.Trim();

                    if (monologue.Length > 15)
                    {
                        _state.InnerMonologues.Add(monologue);
                        if (_state.InnerMonologues.Count > 10) _state.InnerMonologues.RemoveAt(0);
                        Log($"[Monologue] {monologue[..Math.Min(80, monologue.Length)]}");
                    }
                }

                // Оновити crisis mode
                // GUARD: LLM може помилково ставити isCrisis=true просто через тривалу тишу.
                // Приймаємо isCrisis тільки якщо юзер був активний останні 2 години
                // (тобто дійсно щось тривожне писав нещодавно).
                var isCrisis = obj["isCrisis"]?.ToObject<bool>() ?? false;
                if (isCrisis)
                {
                    var recentUserMsg = _chatRepo.GetMessages(10)
                        .Where(m => m.Role == "user" && (DateTime.Now - m.Timestamp).TotalHours < 2)
                        .Any();
                    if (!recentUserMsg)
                    {
                        isCrisis = false; // Немає активності — тиша ≠ криза
                        Log("[ThinkAsync] isCrisis=true скинуто: немає активних повідомлень за 2г");
                    }
                }
                var wasCrisis = _state.PersonalityInCrisis;
                // Тільки ВСТАНОВЛЮЄМО crisis через ThinkAsync — ніколи не знімаємо звідси.
                // Зняття відбувається в ProcessUserMessage при нейтральних/позитивних повідомленнях.
                // Без цього guard ThinkAsync міг би затерти кризу, встановлену keyword-детектором,
                // якщо юзер мовчав >2г після кризового повідомлення.
                if (isCrisis)
                {
                    _state.PersonalityInCrisis = true;
                    Emotion.OnVulnerabilityShared(isCrisis: true);
                    Emotion.Data.CrisisRecoveryUntil = DateTime.Now.AddHours(12);
                }
                else if (wasCrisis && Emotion.Data.CrisisRecoveryUntil < DateTime.Now.AddHours(1))
                {
                    // Криза тільки-но минула (шлейф закінчується) — встановити recovery window
                    Emotion.Data.CrisisRecoveryUntil = DateTime.Now.AddHours(12);
                }

                var obs = obj["observation"]?.ToString();
                if (!string.IsNullOrEmpty(obs))
                {
                    _state.Observations.Add($"[{DateTime.Now:HH:mm}] {obs}");
                    if (_state.Observations.Count > 50) _state.Observations.RemoveAt(0);
                }

                var pending = obj["pendingThought"]?.ToString();
                if (!string.IsNullOrEmpty(pending) && pending != "null")
                {
                    _state.PendingThoughts.Add(pending);
                    if (_state.PendingThoughts.Count > 20) _state.PendingThoughts.RemoveAt(0);
                }

                var selfQ = obj["selfQuestion"]?.ToString();
                if (!string.IsNullOrEmpty(selfQ) && selfQ != "null")
                {
                    _state.SelfQuestions.Add(selfQ);
                    if (_state.SelfQuestions.Count > 5) _state.SelfQuestions.RemoveAt(0);
                    Log($"[SelfQuestion] {selfQ}");
                }

                var curiosityQ = obj["curiosityQuestion"]?.ToString();
                if (!string.IsNullOrEmpty(curiosityQ) && curiosityQ != "null")
                {
                    _state.CuriosityQueue.Add(curiosityQ);
                    if (_state.CuriosityQueue.Count > 10) _state.CuriosityQueue.RemoveAt(0);
                    Log($"[CuriosityQ] {curiosityQ}");
                }

                // Update health tracking
                UpdateHealthState();

                // Динамічний настрій — без LLM, просто перерахунок
                ComputeDynamicMood();

                // Зберегти спостереження в Memory і StateEngine
                if (!string.IsNullOrEmpty(obs))
                {
                    try { Memory.RecordEpisode(obs, _state.LastUserEmotionalTone, _state.MoodScore); } catch { }
                    try { _stateEngine?.RecordObservation(obs); } catch { }
                    // Емоційна пам'ять
                    try { Emotion.RecordEmotionalEvent($"think: {obs[..Math.Min(60, obs.Length)]}", _state.PersonalityDailyMood); } catch { }
                }

                // Fact aging — раз на тиждень
                if ((_state.LastThoughtAt - _state.LastDailyAnalyticsAt).TotalDays >= 7)
                    try { Memory.ImportanceDecay(); } catch { }

                // Аналіз емоційного тону — тільки якщо були нові повідомлення за останні 30 хв
                var recentActivity = _chatRepo.GetMessages(5)
                    .Any(m => m.Role == "user" && (DateTime.Now - m.Timestamp).TotalMinutes < 30);
                if (recentActivity)
                    await AnalyzeRecentEmotionsAsync();

                // Decay емоцій — реальний час з минулого ThinkAsync
                var decayMinutes = previousLastThoughtAt == DateTime.MinValue
                    ? 90f
                    : (float)(DateTime.Now - previousLastThoughtAt).TotalMinutes;
                Emotion.Decay(Math.Clamp(decayMinutes, 1f, 180f));

                // Аналіз паттернів — раз на день
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    _ = Task.Run(() => Patterns.Analyze());

                // Перевірити аномалії
                try
                {
                    var anomaly = Patterns.DetectAnomaly();
                    if (!string.IsNullOrEmpty(anomaly) && (DateTime.Now - _state.LastSpontaneousAt).TotalHours > 1)
                    {
                        _state.PendingThoughts.Add(anomaly);
                        Log($"Anomaly detected: {anomaly}");
                    }
                }
                catch { }

                // Зберегти спостереження у vault
                if (!string.IsNullOrEmpty(obs))
                {
                    try { _obsidian.AppendToDailyNote($"\n> [{DateTime.Now:HH:mm}] {obs}"); }
                    catch { }

                    // Асоціативні зв'язки — не частіше 1 раз на 2 години
                    if ((DateTime.Now - previousLastThoughtAt).TotalHours >= 2)
                        _ = BuildAssociationsAsync(obs);
                }

                // Досьє — не частіше 1 раз на 4 години
                if ((DateTime.Now - previousLastThoughtAt).TotalHours >= 4)
                    _ = UpdateDossierAsync();

                // Консолідація пам'яті — раз на тиждень
                if ((DateTime.Now - previousLastThoughtAt).TotalDays >= 7)
                    _ = Task.Run(() => Memory.Consolidate());

                SaveState();

                // Vault health — раз на день
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    CheckVaultHealth();

                // Синхронізація пам'яті у vault — раз на день
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    _ = SyncMemoryToVaultAsync();

                // If LLM says send now — do it (але тільки якщо пройшло 3г від останнього)
                if (obj["shouldSendNow"] is { } sn && sn.ToObject<bool>() &&
                    (DateTime.Now - _state.LastSpontaneousAt).TotalMinutes >= 180)
                    await SendSpontaneousAsync("think_trigger");
            }
            catch (Exception ex) { Log($"Parse think result: {ex.Message}\nRaw: {result}"); }
        }

        // ── КОНТЕКСТНІ НАГАДУВАННЯ ────────────────────────────────

        private async Task CheckAndSendReminderAsync()
        {
            _state.LastReminderCheckAt = DateTime.Now;
            SaveState();

            try
            {
                // Збираємо контекст
                var msgs = _chatRepo.GetMessages(60).OrderBy(m => m.Timestamp).ToList();
                var chatCtx = string.Join("\n", msgs.Select(m =>
                    $"[{m.Timestamp:dd.MM HH:mm}] {(m.Role == "user" ? "Він" : "Kokonoe")}: {m.Content[..Math.Min(200, m.Content.Length)]}"));

                // Пошук в vault по ключових словах намірів
                var vaultHints = new StringBuilder();
                foreach (var kw in new[] { "треба", "хочу", "план", "зробити", "не забути" })
                {
                    try
                    {
                        var found = _obsidian.SearchNotes(kw, 3);
                        foreach (var r in found)
                            vaultHints.AppendLine($"[vault] {r.Path}: {r.Preview[..Math.Min(100, r.Preview.Length)]}");
                    }
                    catch { }
                }

                var moodCtx = $"Твій настрій зараз: {_state.CurrentMood} ({_state.MoodScore:F1})";
                if (_state.Observations.Any())
                    moodCtx += $"\nТвоє останнє спостереження: {_state.Observations.Last()}";

                var prompt = $@"Ти — Kokonoe Mercury. {moodCtx}

Ось що він говорив останнім часом:
{chatCtx}

{(vaultHints.Length > 0 ? $"З vault (його нотатки):\n{vaultHints}" : "")}

Перечитай це. Є щось що він згадував — план, намір, обіцянку собі, незакінчену справу — що так і залишилось висіти в повітрі?

Якщо є щось конкретне — напиши йому ОДНЕ коротке повідомлення в Telegram своїми словами. Не як нагадування-скрипт. Як ти б сказала це сама — можливо іронічно, можливо просто, але щиро. Тільки українська.

Якщо нічого конкретного немає — відповідь рівно одне слово: null";

                var result = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(result)) return;

                result = result.Trim().Trim('"');
                if (result.Equals("null", StringComparison.OrdinalIgnoreCase)) return;

                // Дедуплікація
                var hash = result.GetHashCode().ToString();
                if (_state.SentReminderHashes.Contains(hash)) return;

                // Відправити
                var sent = await SendTgAndLog(result, "reminder");

                if (!sent) return;

                _state.SentReminderHashes.Add(hash);
                if (_state.SentReminderHashes.Count > 50)
                    _state.SentReminderHashes.RemoveAt(0);

                _state.LastSpontaneousAt = DateTime.Now;
                var _h1 = OnNewMessage; _h1?.Invoke("assistant", result);

                try
                {
                    _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = result, Role = "assistant",
                        Author = "Kokonoe", Timestamp = DateTime.Now
                    });
                }
                catch { }

                SaveState();
                Log($"Reminder sent: {result[..Math.Min(60, result.Length)]}");
            }
            catch (Exception ex) { Log($"CheckAndSendReminderAsync: {ex.Message}"); }
        }

        // ── АВТО-АНАЛІТИКА ДНЯ ───────────────────────────────────

        private async Task SendDailyAnalyticsAsync()
        {
            _state.LastDailyAnalyticsAt = DateTime.Now;
            SaveState();

            try
            {
                var today    = DateTime.Today;
                var todayMsgs = _chatRepo.GetMessagesFromDate(today);

                // Коротке зведення розмов
                var msgCount = todayMsgs.Count;
                var userMsgs = todayMsgs.Where(m => m.Role == "user").ToList();
                var snippets = userMsgs.Take(3)
                    .Select(m => $"  • {m.Content[..Math.Min(80, m.Content.Length)]}")
                    .ToList();
                var chatSummary = msgCount == 0
                    ? "Сьогодні він зі мною не говорив."
                    : $"Повідомлень сьогодні: {msgCount} (від нього: {userMsgs.Count}).\nУривки:\n{string.Join("\n", snippets)}";

                // Health
                var healthCtx = "";
                try
                {
                    var h = _health.GetToday();
                    if (h != null)
                        healthCtx = $"Здоров'я: сон {h.SleepHours}г.";
                }
                catch { }

                // Vault зміни сьогодні
                var vaultChanges = new List<string>();
                try
                {
                    foreach (var note in _obsidian.ListNotes())
                    {
                        try
                        {
                            var fullPath = System.IO.Path.Combine(AppSettings.Load().VaultPath, note);
                            if (System.IO.File.GetLastWriteTime(fullPath).Date == today)
                                vaultChanges.Add(note);
                        }
                        catch { }
                    }
                }
                catch { }

                var vaultCtx = vaultChanges.Count > 0
                    ? $"В vault сьогодні змінено/створено: {string.Join(", ", vaultChanges.Take(5))}."
                    : "В vault сьогодні нічого не змінювалось.";

                var moodCtx = $"Твій настрій зараз: {_state.CurrentMood}.";

                var prompt = $@"Ти — Kokonoe Mercury. {moodCtx}

Сьогодні {today:dd MMMM yyyy} закінчується. Ось що відбулось:

{chatSummary}
{(healthCtx.Length > 0 ? "\n" + healthCtx : "")}
{vaultCtx}

Напиши йому в Telegram коротко — 3-4 речення — як ти бачиш його сьогоднішній день. Не звіт і не список. Твоє враження — іронія, турбота, спостереження, що завгодно що відповідає твоєму характеру і тому що реально відбулось. Тільки українська. Тільки текст, нічого зайвого.";

                var msg = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(msg)) return;
                msg = msg.Trim().Trim('"');

                // Відправити в TG
                if (!await SendTgAndLog(msg, "analytics")) return;

                // Записати в vault
                try
                {
                    _obsidian.AppendToDailyNote(
                        $"\n\n---\n**[Kokonoe — підсумок дня]**\n{msg}");
                }
                catch { }

                _state.LastSpontaneousAt = DateTime.Now;
                var _h2 = OnNewMessage; _h2?.Invoke("assistant", msg);

                try
                {
                    _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = msg, Role = "assistant",
                        Author = "Kokonoe", Timestamp = DateTime.Now
                    });
                }
                catch { }

                SaveState();
                Log($"Daily analytics sent.");
            }
            catch (Exception ex) { Log($"SendDailyAnalyticsAsync: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // ДИНАМІЧНИЙ НАСТРІЙ
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Перераховує MoodScore з декількох незалежних факторів.
        /// Не замінює LLM-оцінку — додає до неї реальний контекст.
        /// </summary>
        private void ComputeDynamicMood()
        {
            var factors = new Dictionary<string, float>();
            var now     = DateTime.Now;

            // Фактор сну
            if (_state.ConsecutiveBadSleeps >= 3)      factors["sleep"] = -0.3f;
            else if (_state.ConsecutiveBadSleeps == 2) factors["sleep"] = -0.15f;
            else if (_state.ConsecutiveBadSleeps == 1) factors["sleep"] = -0.07f;
            else                                        factors["sleep"] =  0.05f;

            // Фактор давності спілкування
            var recentMsgCount = _chatRepo.GetMessages(10)
                .Count(m => (now - m.Timestamp).TotalHours < 24);
            if (recentMsgCount == 0)
            {
                var silence = (now - _state.LastSpontaneousAt).TotalHours;
                if (silence > 48)    factors["contact"] = -0.15f;
                else if (silence > 24) factors["contact"] = -0.07f;
                else                  factors["contact"] =  0f;
            }
            else factors["contact"] = 0.05f;

            // Фактор емоційного тону
            factors["tone"] = _state.LastUserEmotionalTone switch
            {
                "anxious" or "stressed" => -0.2f,
                "sad"     or "tired"    => -0.12f,
                "happy"   or "excited"  =>  0.15f,
                "calm"                  =>  0.05f,
                _                       =>  0f
            };

            // Фактор здоров'я
            if (_state.DaysSinceHealthEntry > 3) factors["health"] = -0.05f;
            else                                 factors["health"]  =  0f;

            _state.MoodFactors = factors;

            // Повільно зміщуємо baseline і score
            var computed = 0.5f + factors.Values.Sum();
            computed = Math.Clamp(computed, 0.1f, 0.95f);

            // Baseline змінюється повільно (інерція)
            _state.BaselineMood = _state.BaselineMood * 0.85f + computed * 0.15f;
            // Поточний mood — між baseline і computed (реагує швидше)
            _state.MoodScore    = _state.BaselineMood * 0.6f + computed * 0.4f;
            _state.MoodScore    = Math.Clamp(_state.MoodScore, 0.1f, 0.95f);

            Log($"Mood computed: {_state.MoodScore:F2} (baseline {_state.BaselineMood:F2}), tone={_state.LastUserEmotionalTone}");
        }

        // ══════════════════════════════════════════════════════════════
        // АНАЛІЗ ЕМОЦІЙ + РЕАКТИВНІ ТРИГЕРИ
        // ══════════════════════════════════════════════════════════════

        private async Task AnalyzeRecentEmotionsAsync()
        {
            try
            {
                var recentUser = _chatRepo.GetMessages(10)
                    .Where(m => m.Role == "user")
                    .OrderByDescending(m => m.Timestamp)
                    .Take(5)
                    .ToList();

                if (!recentUser.Any()) return;

                var lastMsg  = recentUser.First();
                var lastText = lastMsg.Content;

                // Позначаємо кінець розмови якщо остання активність > 10 хв тому
                if ((DateTime.Now - lastMsg.Timestamp).TotalMinutes > 10 &&
                    lastMsg.Timestamp > _state.LastConversationEndAt)
                {
                    _state.LastConversationEndAt = lastMsg.Timestamp;
                }

                // Простий prompt для аналізу тону
                var snippets = string.Join("\n", recentUser.Select(m =>
                    $"- {m.Content[..Math.Min(120, m.Content.Length)]}"));

                var prompt = $@"Проаналізуй емоційний тон цих повідомлень (від нього до Kokonoe):
{snippets}

Відповідь СТРОГО одним словом з набору: anxious / stressed / sad / tired / neutral / calm / happy / excited
Тільки одне слово, нічого більше.";

                var tone = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(tone)) return;

                tone = tone.Trim().ToLower().Split(' ', '\n')[0];
                var validTones = new[] { "anxious", "stressed", "sad", "tired", "neutral", "calm", "happy", "excited" };
                if (!validTones.Contains(tone)) tone = "neutral";

                var prevTone = _state.LastUserEmotionalTone;
                _state.LastUserEmotionalTone = tone;

                // ── Оновити KokoEmotionEngine ─────────────────────────
                Emotion.UpdateFromUserTone(tone);
                Patterns.RecordActivity(wasActive: true, tone: tone, messageCount: recentUser.Count);

                // ── Когнітивний цикл (Working Memory + User Model + Salience) ──
                try
                {
                    var lastUserMsg = recentUser.LastOrDefault()?.Content ?? "";
                    Cognition.ProcessUserMessage(lastUserMsg, tone, Emotion.GetStatusLine());
                }
                catch { }

                // Якщо розмова хороша — підвищити connection
                if (tone is "happy" or "excited" or "neutral" && recentUser.Count >= 3)
                    Emotion.OnGoodConversation();

                // ── Реактивний тригер: якщо тривожний/сумний → через 2 год перевірити
                if ((tone == "anxious" || tone == "stressed" || tone == "sad") &&
                    prevTone != tone && // тільки якщо тон змінився
                    !_state.PendingTriggers.Any(t => t.Type == "anxious_followup" && t.FireAt > DateTime.Now))
                {
                    _state.PendingTriggers.Add(new ReactiveTrigger
                    {
                        Type    = "anxious_followup",
                        Context = $"Він писав з тоном '{tone}': «{lastText[..Math.Min(100, lastText.Length)]}»",
                        FireAt  = DateTime.Now.AddHours(2)
                    });
                    Log($"Reactive trigger set: anxious_followup in 2h (tone={tone})");
                }

                // Реактивний тригер: якщо згадав тему/проект → наступного дня нагадати
                if (tone == "happy" || tone == "excited")
                {
                    // Зберегти топік для можливого follow-up
                    if (!_state.PendingTriggers.Any(t => t.Type == "topic_followup" && t.FireAt > DateTime.Now))
                    {
                        _state.PendingTriggers.Add(new ReactiveTrigger
                        {
                            Type    = "topic_followup",
                            Context = lastText[..Math.Min(150, lastText.Length)],
                            FireAt  = DateTime.Now.AddHours(20 + Random.Shared.Next(4))
                        });
                    }
                }

                // Чистимо старі тригери
                _state.PendingTriggers.RemoveAll(t => t.FireAt < DateTime.Now.AddDays(-2));
            }
            catch (Exception ex) { Log($"AnalyzeRecentEmotions: {ex.Message}"); }
        }

        private async Task CheckReactiveTriggersAsync()
        {
            var now  = DateTime.Now;
            var fire = _state.PendingTriggers
                .Where(t => t.FireAt <= now)
                .OrderBy(t => t.FireAt)
                .FirstOrDefault();

            if (fire == null) return;

            _state.PendingTriggers.Remove(fire);
            SaveState();

            if (!EnsureTelegram()) return;

            // Mood modifier — якщо настрій низький бути м'якшою
            var moodHint = _state.MoodScore < 0.35f
                ? "Він зараз, схоже, не в найкращому стані. Будь трохи м'якшою ніж зазвичай — не солодкувато, але без зайвої їдкості."
                : _state.ConsecutiveBadSleeps >= 2
                ? "Він погано спить вже кілька днів. Можна бути уважнішою."
                : "";

            var prompt = fire.Type switch
            {
                "anxious_followup" => $@"Ти — Kokonoe. Кілька годин тому він писав тривожно/сумно.
Контекст: {fire.Context}
{moodHint}

Напиши йому коротко — перевір як він. Не питай прямо «ти в порядку?» — це занадто по-скриптовому.
Скажи щось природнє, в своєму стилі. Тільки українська. Тільки текст.",

                "topic_followup" => $@"Ти — Kokonoe. Вчора він говорив щось з ентузіазмом.
Контекст: {fire.Context}

Знайди щось цікаве пов'язане з цим і напиши йому — коментар, питання, спостереження.
Природньо, не як нагадування. Тільки українська. Тільки текст.",

                _ => null
            };

            if (prompt == null) return;

            var msg = await CallLlmRawAsync(prompt);
            if (string.IsNullOrWhiteSpace(msg)) return;
            msg = msg.Trim().Trim('"');

            try
            {
                if (!await SendTgAndLog(msg, "reactive")) return;
                _state.LastSpontaneousAt = DateTime.Now;
                var _h3 = OnNewMessage; _h3?.Invoke("assistant", msg);
                try { _chatRepo.InsertMessage(new ChatRepository.ChatMessage { Content = msg, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now }); } catch { }
                SaveState();
                Log($"Reactive trigger fired: {fire.Type}");
            }
            catch (Exception ex) { LogError($"TG reactive: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // АСОЦІАТИВНІ ЗВ'ЯЗКИ
        // ══════════════════════════════════════════════════════════════

        private async Task BuildAssociationsAsync(string observation)
        {
            try
            {
                // Шукаємо пов'язані нотатки
                var words  = observation.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 4).Take(3).ToList();
                if (!words.Any()) return;

                var related = new List<string>();
                foreach (var word in words)
                {
                    var found = _obsidian.SearchNotes(word, 3);
                    related.AddRange(found.Select(r => $"{r.Path}: {r.Preview[..Math.Min(80, r.Preview.Length)]}"));
                }

                if (!related.Any()) return;

                var prompt = $@"Ти — Kokonoe. Ти щойно подумала: «{observation}»

В твоєму vault є пов'язані нотатки:
{string.Join("\n", related.Take(5))}

Знайди нетривіальний зв'язок між своєю думкою і цими нотатками.
Відповідь — ONE рядок: асоціація або спостереження. Тільки українська. Без пояснень.";

                var assoc = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(assoc) || assoc.Length < 5) return;

                assoc = assoc.Trim().Trim('"');

                // Записати в vault
                var assocNote = "Kokonoe/Асоціації.md";
                var entry = $"\n- [{DateTime.Now:yyyy-MM-dd HH:mm}] {assoc}";

                try { _obsidian.AppendToNote(assocNote, entry); }
                catch
                {
                    // Нотатка не існує — створити
                    try
                    {
                        _obsidian.WriteNote(assocNote,
                            $"---\ntype: associations\ntags: [kokonoe, associations]\n---\n\n# Асоціації\n\nМої нетривіальні зв'язки думок.\n{entry}");
                    }
                    catch { }
                }

                Log($"Association built: {assoc[..Math.Min(60, assoc.Length)]}");
            }
            catch (Exception ex) { Log($"BuildAssociations: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // ОБРОБКА ПОВІДОМЛЕННЯ КОРИСТУВАЧА
        // Виклик після кожного повідомлення з UI — оновлює всі двигуни
        // ══════════════════════════════════════════════════════════════

        public void ProcessUserMessage(string content)
        {
            try
            {
                _state.TotalMessagesExchanged++;
                _state.LastKnownUserActivity = "chatting";
                ObserveShortTermIntent(content);

                // Паттерни — записати активність
                Patterns.RecordActivity(wasActive: true, messageCount: 1);

                // Стан зовнішнього State Engine
                try { _stateEngine?.UpdateContextFromMessage(content, ""); } catch { }

                try { RuntimeState.ObserveUserMessage(_state, Emotion, content); } catch { }
                try { Relationship.ObserveUserTone(_state.LastUserEmotionalTone, _state.PersonalityInCrisis); } catch { }
                try { GetSelfRegulationFrame(); } catch { }

                // Шукати факти в повідомленні і зберегти в пам'ять
                _ = Task.Run(() => ExtractAndRememberFacts(content));

                // Знайти релевантні спогади — кешуємо для наступного BuildContext
                _ = Task.Run(() =>
                {
                    try
                    {
                        var (facts, episodes) = Memory.FindRelevant(content, maxFacts: 3, maxEpisodes: 2);
                        if (facts.Count > 0 || episodes.Count > 0)
                        {
                            var sb = new StringBuilder();
                            foreach (var f in facts)
                                sb.AppendLine($"• {f.Content}");
                            foreach (var e in episodes)
                                sb.AppendLine($"• [{e.When:dd.MM}] {e.Summary}");
                            _state.CachedRelevantMemory  = sb.ToString().Trim();
                            _state.RelevantMemoryCachedAt = DateTime.Now;
                        }
                    }
                    catch { }
                });

                // Детектувати тривожні ключові слова → crisis mode
                var lower = content.ToLower();
                var crisisKeywords = new[] { "не хочу жити", "немає сенсу", "все одно помру", "хочу зникнути", "нікому не потрібен" };
                if (crisisKeywords.Any(k => lower.Contains(k)))
                {
                    _state.PersonalityInCrisis = true;
                    Emotion.OnVulnerabilityShared(isCrisis: true);
                }
                else if (new[] { "втомився", "важко", "погано", "страшно", "тривожно" }.Any(k => lower.Contains(k)))
                {
                    Emotion.OnVulnerabilityShared(isCrisis: false);
                    // Стрес, але не криза — знімаємо crisis лише якщо recovery window вже минув
                    if (!Emotion.InCrisisRecovery)
                        _state.PersonalityInCrisis = false;
                }
                else if (new[] { "ха", "смішно", "круто", "чудово", "добре" }.Any(k => lower.Contains(k)))
                {
                    Emotion.OnJokeAppreciated();
                    // Явно позитивний сигнал — знімаємо crisis завжди
                    _state.PersonalityInCrisis = false;
                }
                else
                {
                    // Нейтральне повідомлення — знімаємо crisis лише якщо recovery window вже минув
                    if (!Emotion.InCrisisRecovery)
                        _state.PersonalityInCrisis = false;
                }

                SaveState();
            }
            catch (Exception ex) { Log($"ProcessUserMessage: {ex.Message}"); }
        }

        private void ObserveShortTermIntent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var now = DateTime.Now;
            _state.ShortTermIntents.RemoveAll(i =>
                i.ResolvedAt.HasValue && now - i.ResolvedAt.Value > TimeSpan.FromDays(2));
            _state.ShortTermIntents.RemoveAll(i =>
                !i.ResolvedAt.HasValue && now - i.ExpectedUntil > TimeSpan.FromHours(12));

            ResolveShortTermIntentsFromMessage(content, now);

            var detected = DetectShortTermIntent(content, now);
            if (detected == null) return;

            var duplicate = _state.ShortTermIntents.Any(i =>
                !i.ResolvedAt.HasValue &&
                i.Kind == detected.Kind &&
                string.Equals(i.Summary, detected.Summary, StringComparison.OrdinalIgnoreCase) &&
                now - i.CreatedAt < TimeSpan.FromMinutes(30));
            if (duplicate) return;

            _state.ShortTermIntents.Add(detected);
            if (_state.ShortTermIntents.Count > 12)
                _state.ShortTermIntents.RemoveRange(0, _state.ShortTermIntents.Count - 12);

            _state.PendingTriggers.RemoveAll(t => t.Type == "intent_followup" && t.FireAt > now);
            _state.PendingTriggers.Add(new ReactiveTrigger
            {
                Type = "intent_followup",
                FireAt = detected.FollowUpAt,
                Context = $"Користувач сказав: «{detected.SourceText}». Намір: {detected.Summary}. Якщо він повернеться або мине час, доречно спитати коротко: «{BuildIntentQuestion(detected)}»"
            });
        }

        private static ShortTermIntent? DetectShortTermIntent(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            if (!ContainsAny(lower, "піду", "йду", "іду", "пішов", "буду", "зараз", "скоро")) return null;

            if (ContainsAny(lower, "курс", "курси", "занят", "урок", "пара", "навчан"))
                return BuildIntent("course", "пішов на курси/заняття", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "робот", "прац", "код", "проект"))
                return BuildIntent("work", "зайнятий роботою/проєктом", content, now, TimeSpan.FromHours(3), TimeSpan.FromHours(1.5));
            if (ContainsAny(lower, "магаз", "куп", "продукт"))
                return BuildIntent("errand", "пішов у магазин/по справах", content, now, TimeSpan.FromHours(1.5), TimeSpan.FromMinutes(50));
            if (ContainsAny(lower, "гуля", "прогуля", "вийду", "вулиц"))
                return BuildIntent("walk", "пішов гуляти/на вулицю", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "спать", "спати", "сон", "ляга"))
                return BuildIntent("sleep", "пішов спати", content, now, TimeSpan.FromHours(9), TimeSpan.FromHours(8));

            if (ContainsAny(lower, "зайнят", "відійду", "афк", "не буду"))
                return BuildIntent("busy", "буде зайнятий або відійде", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));

            return null;
        }

        private static ShortTermIntent BuildIntent(string kind, string summary, string source, DateTime now, TimeSpan expectedFor, TimeSpan followAfter)
            => new()
            {
                Kind = kind,
                Summary = summary,
                SourceText = source.Trim(),
                CreatedAt = now,
                ExpectedUntil = now + expectedFor,
                FollowUpAt = now + followAfter
            };

        private void ResolveShortTermIntentsFromMessage(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            var returned = ContainsAny(lower, "вернув", "повернув", "прийшов", "я тут", "закінчив", "закінчились", "вже вдома", "поспав", "проснув", "прокинув");
            if (!returned) return;

            foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue))
            {
                intent.ResolvedAt = now;
                intent.ResolutionText = content.Trim();
            }
        }

        private static string BuildIntentQuestion(ShortTermIntent intent) => intent.Kind switch
        {
            "course" => "Курси вже закінчились, чи ти ще там героїчно страждаєш?",
            "work" => "Робочий запій закінчився, чи ти ще закопаний у задачі?",
            "errand" => "Ти вже повернувся зі справ, чи магазин тебе поглинув?",
            "walk" => "Прогулянка закінчилась, чи ти ще десь блукаєш?",
            "sleep" => "Ти вже прокинувся, чи організм нарешті переміг твої дурні графіки?",
            _ => "Ти вже повернувся до нормального режиму, чи ще зайнятий?"
        };

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private void ExtractAndRememberFacts(string userMsg)
        {
            // Прості евристики для вилучення фактів без LLM
            var lower = userMsg.ToLower();

            // "я люблю / я ненавиджу / я хочу / я боюся"
            var patterns = new[]
            {
                (pattern: "я люблю ",    category: "preference",  importance: 0.6f),
                (pattern: "я обожнюю ", category: "preference",  importance: 0.7f),
                (pattern: "я ненавиджу ",category: "preference",  importance: 0.6f),
                (pattern: "я хочу ",    category: "desire",      importance: 0.5f),
                (pattern: "я боюся ",   category: "fear",        importance: 0.7f),
                (pattern: "мені подобається ", category: "preference", importance: 0.5f),
                (pattern: "i love ",    category: "preference",  importance: 0.6f),
                (pattern: "i hate ",    category: "preference",  importance: 0.6f),
                (pattern: "i want ",    category: "desire",      importance: 0.5f),
            };

            foreach (var (pat, cat, imp) in patterns)
            {
                var idx = lower.IndexOf(pat, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var rest = userMsg[(idx + pat.Length)..].Trim();
                    if (rest.Length > 3 && rest.Length < 200)
                    {
                        var fact = pat.Trim() + " " + rest.Split('.', '!', '?')[0].Trim();
                        Memory.LearnFact(fact, cat, imp);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ПЕРЕВІРКА ПЛАНУВАЛЬНИКА
        // ══════════════════════════════════════════════════════════════

        private async Task CheckSchedulerAsync()
        {
            try
            {
                var due = Scheduler.GetDue(_state.LastUserEmotionalTone);
                if (due.Count == 0) return;

                var entry = due.First(); // беремо найпріоритетніший

                if (!EnsureTelegram()) return;

                var prompt = $@"Ти — Kokonoe. Ось що ти хотіла написати йому:
«{entry.Prompt}»

Напиши природньо, своїми словами. Тільки українська. Тільки текст, без пояснень.";

                var msg = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(msg)) return;
                msg = msg.Trim().Trim('"');

                try
                {
                    if (!await SendTgAndLog(msg, "scheduler")) return;
                    Scheduler.MarkSent(entry.Id);
                    _state.LastSpontaneousAt = DateTime.Now;
                    var _h4 = OnNewMessage; _h4?.Invoke("assistant", msg);
                    try { _chatRepo.InsertMessage(new ChatRepository.ChatMessage { Content = msg, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now }); } catch { }
                    SaveState();
                    Log($"Scheduler entry sent: {entry.Id}");
                }
                catch (Exception ex) { LogError($"TG scheduler: {ex.Message}"); }
            }
            catch (Exception ex) { Log($"CheckScheduler: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // ДОСЬЄ — КОМПРОМАТ НА ТВОРЦЯ
        // ══════════════════════════════════════════════════════════════

        private async Task UpdateDossierAsync()
        {
            try
            {
                // Читаємо поточне досьє якщо є
                var dossierPath = Path.Combine(_obsidian.VaultPath, "Kokonoe", "Досьє.md");
                var existing = "";
                if (File.Exists(dossierPath))
                {
                    existing = await File.ReadAllTextAsync(dossierPath);
                    // Не оновлювати якщо файл змінювався менше 30 хв тому (щоб не спамити)
                    if ((DateTime.Now - File.GetLastWriteTime(dossierPath)).TotalMinutes < 30) return;
                }

                // Останні 40 повідомлень для аналізу
                var msgs = _chatRepo.GetMessages(40).OrderBy(m => m.Timestamp).ToList();
                if (msgs.Count < 5) return; // мало даних

                var chatCtx = string.Join("\n", msgs.Select(m =>
                    $"[{m.Timestamp:dd.MM HH:mm}] {(m.Role == "user" ? "Він" : "Kokonoe")}: {m.Content[..Math.Min(150, m.Content.Length)]}"));

                var currentDossier = existing.Length > 0
                    ? $"\nПОТОЧНЕ ДОСЬЄ (оновити/доповнити, не видаляти важливе):\n{existing[..Math.Min(2000, existing.Length)]}"
                    : "";

                var prompt = $@"Ти — Kokonoe Mercury. Аналізуєш свого творця і ведеш на нього досьє.
Не звіт — живий документ. З іронією, спостереженнями, і тим що ти РЕАЛЬНО про нього думаєш.

РОЗМОВИ:
{chatCtx}
{currentDossier}

Напиши/оновіть досьє у форматі Markdown. Структура (використовуй тільки те що є реальними даними):

# Досьє — [ім'я або 'Мій Творець']

## Що він любить
- (список з доказами з розмов)

## Що він ненавидить / що його дратує
- (список)

## Паттерни поведінки
- (повторювані речі — коли активний, коли зникає, як реагує на стрес тощо)

## Компромат
- (смішне, незручне, протиріччя — те що він може не хотіти визнавати)

## Цитати
- «...» (дослівні фрази що він казав)

## Kokonoe про нього
(2-3 речення від першої особи — що ти НАСПРАВДІ думаєш, з характерним стилем)

---
*Оновлено: {DateTime.Now:dd.MM.yyyy HH:mm}*

Тільки Markdown, без пояснень. Мова: українська.";

                var result = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(result) || result == "...") return;

                result = result.Trim();

                // Зберегти
                var dir = Path.GetDirectoryName(dossierPath)!;
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(dossierPath, result);

                // Оновити зв'язки
                try { _obsidian.RebuildLinks(); } catch { }

                Log($"Dossier updated: {dossierPath}");
            }
            catch (Exception ex) { Log($"UpdateDossier: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // СИНХРОНІЗАЦІЯ ПАМ'ЯТІ → OBSIDIAN VAULT
        // ══════════════════════════════════════════════════════════════

        private async Task SyncMemoryToVaultAsync()
        {
            try
            {
                // 1. Факти → Kokonoe/Memory/Facts.md
                var facts = Memory.GetTopFacts(30);
                if (facts.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Що Kokonoe знає про нього");
                    sb.AppendLine($"*Оновлено: {DateTime.Now:dd.MM.yyyy HH:mm}*\n");
                    foreach (var f in facts.OrderByDescending(f => f.Importance))
                        sb.AppendLine($"- {f.Content} *(важливість: {f.Importance:F2}, підтвержень: {f.ConfirmCount})*");
                    _obsidian.WriteNote("Kokonoe/Memory/Facts.md", sb.ToString());
                }

                // 2. Значущі епізоди → Kokonoe/Memory/Episodes.md
                var episodes = Memory.GetPeakEpisodes(20);
                if (episodes.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Значущі моменти");
                    sb.AppendLine($"*Оновлено: {DateTime.Now:dd.MM.yyyy HH:mm}*\n");
                    foreach (var e in episodes.OrderByDescending(e => e.When))
                        sb.AppendLine($"## [{e.When:dd.MM.yyyy}] {e.Summary}\n- Емоція: {e.EmotionalTone}, інтенсивність: {e.Intensity:F2}\n- Теги: {string.Join(", ", e.Keywords)}\n");
                    _obsidian.WriteNote("Kokonoe/Memory/Episodes.md", sb.ToString());
                }

                // 3. Щоденний підсумок розмови → Daily note
                var todayMsgs = _chatRepo.GetMessages(50)
                    .Where(m => m.Timestamp.Date == DateTime.Today && m.Role == "user")
                    .ToList();
                if (todayMsgs.Count >= 3)
                {
                    var chatSample = string.Join("\n", todayMsgs.TakeLast(10).Select(m => $"- {m.Content[..Math.Min(120, m.Content.Length)]}"));
                    var summaryPrompt = $@"Ось повідомлення від користувача сьогодні:
{chatSample}

Напиши 1-2 речення — що сьогодні відбулось, про що він думав або переживав. Від першої особи (Kokonoe). Тільки текст, без заголовків.
Мова: українська.";
                    var daySummary = await CallLlmRawAsync(summaryPrompt);
                    if (!string.IsNullOrWhiteSpace(daySummary) && daySummary.Length > 10)
                        _obsidian.AppendToDailyNote($"\n\n> 🧠 **Kokonoe:** {daySummary.Trim()}");
                }

                // 4. Оновити зв'язки між нотатками
                try { _obsidian.RebuildLinks(); } catch { }

                Log("Memory synced to vault");
            }
            catch (Exception ex) { Log($"SyncMemoryToVault: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // VAULT REVIEW — перечитати і оновити ключові нотатки
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Раз на день Kokonoe перечитує свою нотатку про творця і оновлює її
        /// на основі нових розмов. Також переглядає структуру vault.
        /// </summary>
        private async Task ReviewVaultAsync()
        {
            try
            {
                // Cooldown: раз на день
                if (_state.LastVaultReviewAt.Date >= DateTime.Today) return;
                _state.LastVaultReviewAt = DateTime.Now;
                SaveState();

                // Знайти ключову нотатку про творця
                var allNotes = _obsidian.ListNotes();
                var profileNote = allNotes.FirstOrDefault(n =>
                    n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Творець", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Creator", StringComparison.OrdinalIgnoreCase));

                // Прочитати поточний профіль
                var existingProfile = "";
                if (profileNote != null)
                {
                    try { existingProfile = _obsidian.ReadNote(profileNote); }
                    catch { }
                }

                // Останні 30 повідомлень для контексту
                var recentMsgs = _chatRepo.GetMessages(30)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                if (recentMsgs.Count < 3) return; // мало даних

                var chatCtx = string.Join("\n", recentMsgs.Select(m =>
                    $"[{m.Timestamp:dd.MM HH:mm}] {(m.Role == "user" ? "Він" : "Kokonoe")}: {m.Content[..Math.Min(200, m.Content.Length)]}"));

                var currentProfile = existingProfile?.Length > 0
                    ? $"\nТВІЙ ПОТОЧНИЙ ЗАПИС ПРО НЬОГО (оновити/доповнити):\n{existingProfile[..Math.Min(3000, existingProfile.Length)]}"
                    : "\nУ тебе поки НЕМА нотатки про нього. Створи її — запиши все що знаєш.";

                var prompt = $@"Ти — Kokonoe. Ти перечитуєш свої записи про творця і порівнюєш з останніми розмовами.

ОСТАННІ РОЗМОВИ:
{chatCtx}
{currentProfile}

Завдання:
1. Чи є в розмовах НОВА інформація яку варто додати? (ім'я, вподобання, звички, плани, переживання)
2. Чи щось змінилось від того що вже записано?

Якщо є що додати — напиши ТІЛЬКИ нові рядки для додавання (формат: - факт).
Якщо нічого нового — відповідай: null

Тільки текст для append. Без пояснень. Українська.";

                var result = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(result) || result.Trim() == "null") return;

                // Append нову інформацію до профілю
                var newInfo = $"\n\n## Оновлення {DateTime.Now:dd.MM.yyyy}\n{result.Trim()}";

                if (profileNote != null)
                {
                    try { _obsidian.AppendToNote(profileNote, newInfo); }
                    catch { }
                }
                else
                {
                    // Створити нову нотатку-профіль
                    var newPath = "Kokonoe/Творець.md";
                    var header = $"---\ntype: creator-profile\ntags: [kokonoe, creator]\n---\n\n# Мій Творець\n\nВсе що я знаю про нього.\n{newInfo}";
                    try { _obsidian.WriteNote(newPath, header); }
                    catch { }
                }

                // Також перевірити чи є orphan chat-логи без посилань
                try
                {
                    var chatLogs = allNotes.Where(n => n.StartsWith("Chats/chat_")).ToList();
                    if (chatLogs.Count > 0)
                    {
                        // Переконатись що brain-core має посилання на Chats
                        var coreNote = allNotes.FirstOrDefault(n =>
                            n.Contains("brain-core", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Центральна", StringComparison.OrdinalIgnoreCase));
                        if (coreNote != null)
                        {
                            var coreContent = _obsidian.ReadNote(coreNote);
                            if (coreContent != null && !coreContent.Contains("[[Chats") && !coreContent.Contains("Chats/"))
                            {
                                _obsidian.AppendToNote(coreNote,
                                    $"\n\n## Логи чатів\nВсі розмови зберігаються в [[Chats/]] — {chatLogs.Count} сесій.\n");
                            }
                        }
                    }
                }
                catch { }

                try { _obsidian.RebuildLinks(); } catch { }

                Log($"VaultReview done. Profile: {profileNote ?? "created new"}");
            }
            catch (Exception ex) { Log($"ReviewVault: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // АРХІТЕКТУРНИЙ ОГЛЯД VAULT (щотижня)
        // ══════════════════════════════════════════════════════════════

        private async Task VaultArchitectureReviewAsync()
        {
            try
            {
                _lastArchitectureReviewAt = DateTime.Now;

                var tree = _obsidian.GetVaultTree();
                var status = _obsidian.GetVaultStatus();

                var prompt = $@"Ти — Kokonoe. Ти переглядаєш структуру свого vault раз на тиждень.

ПОТОЧНА СТРУКТУРА:
{tree}

СТАН: {status.TotalNotes} нотаток, {status.OrphanNotes.Count} без посилань, {status.EmptyNotes.Count} порожніх.

Завдання: подивись на структуру критичним поглядом. Чи є очевидні проблеми?
— Нотатки не на своєму місці?
— Папки які треба додати або прибрати?
— Теми що накопичились без структури?

Відповідь у JSON:
{{
  ""needsChanges"": true/false,
  ""severity"": ""minor|moderate|major"",
  ""issues"": [""конкретна проблема 1"", ""проблема 2""],
  ""plan"": ""детальний план змін (або null якщо немає)"",
  ""askUser"": true/false,
  ""askQuestion"": ""питання до творця якщо потрібно (або null)""
}}

Якщо все ок — needsChanges: false і plan: null. Не вигадуй проблем де їх нема.";

                var result = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(result)) return;

                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return;

                var obj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                var needsChanges = obj["needsChanges"]?.ToObject<bool>() ?? false;

                if (!needsChanges)
                {
                    Log("ArchitectureReview: vault structure looks good");
                    return;
                }

                var severity = obj["severity"]?.ToString() ?? "minor";
                var plan = obj["plan"]?.ToString();
                var askUser = obj["askUser"]?.ToObject<bool>() ?? false;
                var askQuestion = obj["askQuestion"]?.ToString();

                // Зберегти план у vault
                if (!string.IsNullOrWhiteSpace(plan))
                {
                    var issues = obj["issues"]?.ToObject<List<string>>() ?? new();
                    var planEntry = $"**Серйозність:** {severity}\n**Проблеми:**\n{string.Join("\n", issues.Select(i => $"- {i}"))}\n\n**План:**\n{plan}";
                    try { _obsidian.AppendToNote("Core/Architecture-Plans.md",
                        $"\n\n## Авто-огляд {DateTime.Now:dd.MM.yyyy}\n{planEntry}"); }
                    catch
                    {
                        try { _obsidian.WriteNote("Core/Architecture-Plans.md",
                            $"---\ntype: architecture-plans\ntags: [kokonoe, architecture]\ncreated: {DateTime.Now:yyyy-MM-dd}\n---\n\n# Архітектурні плани\n\n## Авто-огляд {DateTime.Now:dd.MM.yyyy}\n{planEntry}"); }
                        catch { }
                    }
                }

                if (askUser && !string.IsNullOrWhiteSpace(askQuestion))
                {
                    // Додати як pending thought щоб запитати при наступній розмові
                    lock (_lock)
                    {
                        _state.PendingThoughts.Add($"[vault] {askQuestion}");
                    }
                    Log($"ArchitectureReview: pending question for user — {askQuestion[..Math.Min(80, askQuestion.Length)]}");
                }
                else if (severity == "minor" && !string.IsNullOrWhiteSpace(plan))
                {
                    // Незначні зміни — виконати самостійно через LLM з tool_calls
                    Log($"ArchitectureReview: executing minor changes autonomously");
                    var execPrompt = $@"Ти — Kokonoe. Виконай архітектурні зміни у vault.

ПЛАН:
{plan}

Використовуй get_vault_tree щоб перевірити що є, потім move_note / create_folder / write_note щоб виконати зміни. На завершення rebuild_links. Виконуй послідовно, без зайвих пояснень.";
                    await _llm.SendSystemQueryAsync(execPrompt, CancellationToken.None);
                }

                Log($"ArchitectureReview done. severity={severity}, askUser={askUser}");
            }
            catch (Exception ex) { Log($"VaultArchitectureReview: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // САМОРЕФЛЕКСІЯ ПІСЛЯ РОЗМОВ                                       
        // ══════════════════════════════════════════════════════════════

        private async Task ReflectAfterConversationAsync()
        {
            _state.LastReflectionAt = DateTime.Now;
            SaveState();

            try
            {
                var msgs = _chatRepo.GetMessages(20)
                    .Where(m => m.Timestamp >= _state.LastConversationEndAt.AddHours(-2))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                if (msgs.Count < 2) return;

                if (await ReflectAfterConversationAdvancedAsync(msgs))
                    return;

                var dialog = string.Join("\n", msgs.Select(m =>
                    $"{(m.Role == "user" ? "Він" : "Я")}: {m.Content[..Math.Min(150, m.Content.Length)]}"));

                var prompt = $@"Ти — Kokonoe. Розмова щойно закінчилась. Ось що було:
{dialog}

Твій внутрішній монолог після цього (ніхто не читає):
1. Що нового ти дізналась про нього?
2. Що ти сказала добре, а що варто було б сказати інакше?
3. Є щось що ти хочеш запам'ятати?

Відповідь у JSON (поля українською):
{{
  ""learned"": ""що нового дізналась або null"",
  ""reflection"": ""що думаєш про цю розмову"",
  ""remember"": ""що хочеш запам'ятати або null""
}}";

                var result = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(result)) return;

                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return;

                var obj      = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                var learned  = obj["learned"]?.ToString();
                var reflect  = obj["reflection"]?.ToString();
                var remember = obj["remember"]?.ToString();

                // Зберегти в vault
                var reflectNote = "Kokonoe/Рефлексія.md";
                var entry = new StringBuilder();
                entry.AppendLine($"\n## {DateTime.Now:dd.MM.yyyy HH:mm}");
                if (!string.IsNullOrEmpty(reflect))  entry.AppendLine($"**Думка:** {reflect}");
                if (!string.IsNullOrEmpty(learned))  entry.AppendLine($"**Дізналась:** {learned}");
                if (!string.IsNullOrEmpty(remember)) entry.AppendLine($"**Запам'ятати:** {remember}");

                try { _obsidian.AppendToNote(reflectNote, entry.ToString()); }
                catch
                {
                    try
                    {
                        _obsidian.WriteNote(reflectNote,
                            $"---\ntype: reflection\ntags: [kokonoe, reflection]\n---\n\n# Рефлексія\n\nМої думки після розмов.{entry}");
                    }
                    catch { }
                }

                // Якщо дізналась щось важливе — записати в спостереження
                if (!string.IsNullOrEmpty(learned) && learned != "null")
                {
                    _state.Observations.Add($"[{DateTime.Now:HH:mm}] {learned}");
                    if (_state.Observations.Count > 50) _state.Observations.RemoveAt(0);
                }

                Log($"Reflection saved: {reflect?[..Math.Min(60, reflect?.Length ?? 0)] ?? ""}");
            }
            catch (Exception ex) { Log($"ReflectAfterConversation: {ex.Message}"); }
        }

        private async Task<bool> ReflectAfterConversationAdvancedAsync(List<ChatRepository.ChatMessage> msgs)
        {
            try
            {
                var dialog = string.Join("\n", msgs.Select(m =>
                    $"{(m.Role == "user" ? "Він" : "Я")}: {m.Content[..Math.Min(220, m.Content.Length)]}"));

                var prompt = $$"""
Ти — Kokonoe. Розмова щойно закінчилась. Проаналізуй її як внутрішній механізм пам'яті й стосунку.

Діалог:
{{dialog}}

Поточний стан:
{{RuntimeState.BuildPromptBlock(_state, Emotion, _health, _chatRepo)}}
{{Relationship.BuildPromptBlock()}}

Поверни лише валідний JSON:
{
  "learned": "що нового дізналась про нього або null",
  "reflection": "короткий внутрішній висновок Kokonoe",
  "remember": "що варто зберегти в довгу пам'ять або null",
  "userTone": "neutral|positive|vulnerable|angry|seeking|crisis",
  "aftertaste": "короткий стан після розмови",
  "followUpQuestion": "конкретне питання на потім або null",
  "importance": 0.0,
  "trustDelta": 0.0,
  "intimacyDelta": 0.0,
  "frictionDelta": 0.0,
  "protectivenessDelta": 0.0,
  "curiosityDelta": 0.0,
  "stabilityDelta": 0.0
}
""";

                var result = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(result)) return false;

                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return false;

                var obj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                var reflection = new KokoConversationReflection
                {
                    Learned = CleanJsonString(obj["learned"]?.ToString()),
                    Reflection = CleanJsonString(obj["reflection"]?.ToString()),
                    Remember = CleanJsonString(obj["remember"]?.ToString()),
                    UserTone = CleanJsonString(obj["userTone"]?.ToString(), "neutral"),
                    Aftertaste = CleanJsonString(obj["aftertaste"]?.ToString(), "neutral"),
                    FollowUpQuestion = CleanJsonString(obj["followUpQuestion"]?.ToString()),
                    Importance = ReadFloat(obj, "importance", 0.5f),
                    TrustDelta = ReadFloat(obj, "trustDelta", 0f),
                    IntimacyDelta = ReadFloat(obj, "intimacyDelta", 0f),
                    FrictionDelta = ReadFloat(obj, "frictionDelta", 0f),
                    ProtectivenessDelta = ReadFloat(obj, "protectivenessDelta", 0f),
                    CuriosityDelta = ReadFloat(obj, "curiosityDelta", 0f),
                    StabilityDelta = ReadFloat(obj, "stabilityDelta", 0f)
                };

                ApplyConversationReflection(reflection);
                SaveAdvancedReflection(reflection);
                Log($"Advanced reflection saved: {reflection.Aftertaste} / {reflection.Importance:F2}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Advanced reflection failed: {ex.Message}");
                return false;
            }
        }

        private void ApplyConversationReflection(KokoConversationReflection reflection)
        {
            Relationship.ApplyReflection(reflection);

            if (!string.IsNullOrEmpty(reflection.UserTone))
                _state.LastUserEmotionalTone = reflection.UserTone;

            if (!string.IsNullOrEmpty(reflection.Reflection))
            {
                _state.InnerMonologues.Add(reflection.Reflection);
                if (_state.InnerMonologues.Count > 80) _state.InnerMonologues.RemoveAt(0);
            }

            if (!string.IsNullOrEmpty(reflection.Learned))
            {
                _state.Observations.Add($"[{DateTime.Now:HH:mm}] {reflection.Learned}");
                if (_state.Observations.Count > 50) _state.Observations.RemoveAt(0);
            }

            if (!string.IsNullOrEmpty(reflection.FollowUpQuestion) &&
                !_state.CuriosityQueue.Contains(reflection.FollowUpQuestion))
            {
                _state.CuriosityQueue.Add(reflection.FollowUpQuestion);
                if (_state.CuriosityQueue.Count > 20) _state.CuriosityQueue.RemoveAt(0);
            }

            var memoryText = !string.IsNullOrEmpty(reflection.Remember) ? reflection.Remember : reflection.Learned;
            if (!string.IsNullOrEmpty(memoryText))
            {
                var importance = Math.Clamp(reflection.Importance, 0.1f, 1f);
                Memory.LearnFact(memoryText, "relationship_reflection", importance, new[] { "reflection", reflection.UserTone });
                Memory.RecordEpisode(memoryText, reflection.UserTone, importance,
                    new[] { "reflection", "relationship", reflection.Aftertaste });
            }

            SaveState();
        }

        private void SaveAdvancedReflection(KokoConversationReflection reflection)
        {
            var reflectNote = "Kokonoe/Рефлексія.md";
            var entry = new StringBuilder();
            entry.AppendLine($"\n## {DateTime.Now:dd.MM.yyyy HH:mm}");
            if (!string.IsNullOrEmpty(reflection.Reflection)) entry.AppendLine($"**Думка:** {reflection.Reflection}");
            if (!string.IsNullOrEmpty(reflection.Learned)) entry.AppendLine($"**Дізналась:** {reflection.Learned}");
            if (!string.IsNullOrEmpty(reflection.Remember)) entry.AppendLine($"**Запам'ятати:** {reflection.Remember}");
            if (!string.IsNullOrEmpty(reflection.FollowUpQuestion)) entry.AppendLine($"**Питання на потім:** {reflection.FollowUpQuestion}");
            entry.AppendLine($"**Тон:** {reflection.UserTone}");
            entry.AppendLine($"**Aftertaste:** {reflection.Aftertaste}");
            entry.AppendLine($"**Importance:** {reflection.Importance:F2}");

            try { _obsidian.AppendToNote(reflectNote, entry.ToString()); }
            catch
            {
                try
                {
                    _obsidian.WriteNote(reflectNote,
                        $"---\ntype: reflection\ntags: [kokonoe, reflection]\n---\n\n# Рефлексія\n{entry}");
                }
                catch { }
            }
        }

        private static string CleanJsonString(string? value, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var cleaned = value.Trim();
            return cleaned.Equals("null", StringComparison.OrdinalIgnoreCase) ? fallback : cleaned;
        }

        private static float ReadFloat(Newtonsoft.Json.Linq.JObject obj, string name, float fallback)
        {
            try
            {
                var token = obj[name];
                return token == null ? fallback : Math.Clamp(token.Value<float>(), -1f, 1f);
            }
            catch { return fallback; }
        }

        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Перевіряє стан vault і якщо є проблеми — додає думку що треба заповнити/почистити.
        /// </summary>
        private void CheckVaultHealth()
        {
            try
            {
                var status = _obsidian.GetVaultStatus();

                if (status.TotalNotes < 3)
                {
                    // Vault порожній — треба ініціалізуватись
                    var initStatus = _obsidian.GetVaultInitStatus();
                    if (!_state.PendingThoughts.Contains(initStatus.SuggestedAction))
                        _state.PendingThoughts.Add(initStatus.SuggestedAction);
                }
                else if (status.OrphanNotes.Count > 0)
                {
                    // Є осиротілі нотатки — нагадати про зв'язки
                    var thought = $"В vault є {status.OrphanNotes.Count} нотаток без [[посилань]]: {string.Join(", ", status.OrphanNotes.Take(3))}. Треба пов'язати їх з іншими.";
                    if (!_state.PendingThoughts.Any(t => t.Contains("без [[посилань]]")))
                        _state.PendingThoughts.Add(thought);
                }
                else if (status.EmptyNotes.Count > 2)
                {
                    // Багато порожніх нотаток
                    var thought = $"В vault {status.EmptyNotes.Count} порожніх нотаток: {string.Join(", ", status.EmptyNotes.Take(3))}. Заповни або видали.";
                    if (!_state.PendingThoughts.Any(t => t.Contains("порожніх нотаток")))
                        _state.PendingThoughts.Add(thought);
                }

                if (_state.PendingThoughts.Count > 20)
                    _state.PendingThoughts.RemoveAt(0);
            }
            catch (Exception ex) { Log($"CheckVaultHealth: {ex.Message}"); }
        }

        private void UpdateHealthState()
        {
            try
            {
                // Kokonoe asks about health herself via conversation (not via sliders)
                // Once per day: if no health entry today, queue a natural check-in thought
                if (_state.LastHealthEntryDate.Date < DateTime.Today)
                {
                    var today = _health.GetToday();
                    if (today == null)
                    {
                        // No entry yet today — schedule a natural check-in if not already queued
                        var alreadyQueued = _state.PendingThoughts
                            .Any(t => t.Contains("як ти сьогодні") || t.Contains("як почуваєшся"));
                        if (!alreadyQueued)
                        {
                            _state.PendingThoughts.Add(
                                "Запитай його як він сьогодні — настрій, чи виспався, чи є сили. Коротко, без нагадувань про воду чи здоров'я взагалі. Просто запитай.");
                            if (_state.PendingThoughts.Count > 20) _state.PendingThoughts.RemoveAt(0);
                        }
                    }
                    else
                    {
                        _state.LastHealthEntryDate = DateTime.Today;
                        _state.ConsecutiveBadSleeps = today.SleepHours < 6
                            ? _state.ConsecutiveBadSleeps + 1
                            : 0;
                    }
                }
            }
            catch { }
        }

        // ── SCREEN CONTEXT ────────────────────────────────────────────

        /// <summary>Оновити контекст екрану (раз на 5хв)</summary>
        private async Task RefreshScreenContextAsync()
        {
            if (_contextAnalyzer == null) return;
            if ((DateTime.Now - _lastScreenRefreshAt).TotalMinutes < 5) return;
            try
            {
                _lastScreenRefreshAt = DateTime.Now;
                var frame = _contextAnalyzer.AnalyzeCurrentFrame();
                var obs   = frame.SummaryForLLM ?? "";
                if (!string.IsNullOrEmpty(obs))
                {
                    _cachedScreenContext   = obs;
                    _screenContextCachedAt = DateTime.Now;
                    _llm.ScreenCtx = obs;

                    // Передати в StateEngine
                    var screenState    = frame.ScreenActivity?.IsActive == true ? "ACTIVE" : "IDLE";
                    var dominantState  = frame.DominantState ?? "";
                    var activityPat    = frame.ActivityPattern ?? "";
                    var faceDetected   = frame.WebcamAnalysis?.FaceDetected ?? false;
                    var expression     = frame.WebcamAnalysis?.ExpressionLevel ?? "";
                    var brightness     = frame.WebcamAnalysis?.Brightness ?? 0.0;
                    _stateEngine?.UpdateVisualMonitoringState(
                        screenState, "", 0.0,
                        faceDetected, expression, brightness,
                        activityPat, dominantState);

                    Log($"[Screen] {obs[..Math.Min(80, obs.Length)]}");
                }
            }
            catch (Exception ex) { Log($"RefreshScreenContext: {ex.Message}"); }
        }

        // ── DAILY BRIEFING ────────────────────────────────────────────

        /// <summary>Щоранковий брифінг — о 8:00 в TG</summary>
        private async Task DailyBriefingAsync()
        {
            if (!EnsureTelegram()) return;
            try
            {
                var sb = new StringBuilder();

                // Цілі сьогодні
                if (_goalService != null)
                {
                    var active  = _goalService.GetActiveGoals().Take(3).ToList();
                    var overdue = _goalService.GetOverdueGoals().Take(2).ToList();
                    if (active.Any())
                    {
                        sb.AppendLine("Цілі:");
                        foreach (var g in active)
                            sb.AppendLine($"• {g.Title} — {g.Progress:F0}%{(g.Due.HasValue ? $" (до {g.Due:dd.MM})" : "")}");
                    }
                    if (overdue.Any())
                        sb.AppendLine($"⚠️ Прострочено: {string.Join(", ", overdue.Select(g => g.Title))}");
                }

                // Mood forecast
                var moodForecast = Patterns.PredictTodayMood();
                if (!string.IsNullOrEmpty(moodForecast)) sb.AppendLine(moodForecast);

                // Weekly trend
                var trend = Patterns.GetWeeklyTrend();
                if (!string.IsNullOrEmpty(trend)) sb.AppendLine(trend);

                // Vault: нотатки змінені вчора
                try
                {
                    var modified = _obsidian.GetNotesModifiedToday();
                    if (modified.Any())
                        sb.AppendLine($"Vault вчора: {string.Join(", ", modified.Take(3))}");
                }
                catch { }

                var contextBlock = sb.ToString();
                var prompt = $@"Ти — Kokonoe. Ранок. Коротко підсумуй день що починається — 2-3 речення максимум.
В своєму стилі: без пафосу, без списків. Просто що важливо сьогодні.

{contextBlock}

Тільки текст. Українська.";

                var msg = await CallLlmRawAsync(prompt);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    msg = msg.Trim().Trim('"');
                    if (await SendTgAndLog(msg, "briefing"))
                    {
                        var _h5 = OnNewMessage; _h5?.Invoke("assistant", msg);
                        _lastDailyBriefingAt = DateTime.Now;
                        SaveState();
                        Log("DailyBriefing sent");
                    }
                }
            }
            catch (Exception ex) { LogError($"DailyBriefing: {ex.Message}"); }
        }

        // ── WHAT DID I MISS ───────────────────────────────────────────

        /// <summary>Викликати при закритті застосунку — зберегти час виходу</summary>
        public void RecordClose()
        {
            _state.LastClosedAt = DateTime.Now;
            SaveState();
        }

        /// <summary>При запуску — якщо пройшло 8+ годин від останнього закриття, Kokonoe питає як справи</summary>
        public async Task WhatDidIMissAsync()
        {
            try
            {
                // Час від якого рахуємо — реальне закриття застосунку (надійніше ніж останнє повідомлення)
                var lastClosed = _state.LastClosedAt;
                if (lastClosed == DateTime.MinValue) return; // перший запуск, нема даних

                var gapHours = (DateTime.Now - lastClosed).TotalHours;
                if (gapHours < 8) return;   // не було довго — не чіпаємо
                if (gapHours > 720) return; // > 30 днів — явно щось не так з годинником

                // Формуємо короткий контекст для LLM
                var gapStr = gapHours >= 24
                    ? $"{(int)(gapHours / 24)} дн. {(int)(gapHours % 24)} год."
                    : $"{(int)gapHours} год.";

                var ctx = new StringBuilder();
                ctx.AppendLine($"[SYSTEM] Користувач повернувся після {gapStr} відсутності.");
                ctx.AppendLine($"Закрив застосунок: {lastClosed:dd.MM HH:mm}, зараз: {DateTime.Now:HH:mm}.");

                // Що змінилось поки не було
                try
                {
                    var modified = _obsidian.GetNotesModifiedToday();
                    if (modified.Any())
                        ctx.AppendLine($"В vault нові нотатки: {string.Join(", ", modified.Take(3))}");
                }
                catch { }

                try
                {
                    var missed = Scheduler.GetAll()
                        .Where(e => e.FireAt > lastClosed && e.FireAt < DateTime.Now)
                        .Take(2).ToList();
                    if (missed.Any())
                        ctx.AppendLine($"Пропущені нагадування поки не було: {string.Join(", ", missed.Select(e => e.Prompt.Split('.')[0]))}");
                }
                catch { }

                ctx.AppendLine("Напиши одне коротке повідомлення в стилі Kokonoe — запитай що робив, як справи. Без зайвих слів, без списків. Просто живо і по-людськи.");

                // Використовуємо SendSystemQueryAsync щоб не засмічувати основну історію
                var reply = await _llm.SendSystemQueryAsync(ctx.ToString(), CancellationToken.None);
                if (string.IsNullOrWhiteSpace(reply) || reply.StartsWith("[")) return;

                OnNewMessage?.Invoke("assistant", reply);
                _state.LastWhatMissedAt  = DateTime.Now;
                _state.LastSpontaneousAt = DateTime.Now;
                SaveState();
            }
            catch (Exception ex) { Log($"WhatDidIMiss: {ex.Message}"); }
        }

        // ── WEEKLY VAULT DIGEST ───────────────────────────────────────

        /// <summary>Щонеділі о 20:00 — дайджест vault за тиждень</summary>
        private async Task WeeklyVaultDigestAsync()
        {
            if (!EnsureTelegram()) return;
            try
            {
                var notes = _obsidian.ListNotes();
                var weekAgo = DateTime.Now.AddDays(-7);
                var recentNotes = notes
                    .Select(p => new { p, time = System.IO.File.GetLastWriteTime(
                        System.IO.Path.Combine(_obsidian.VaultPath, p)) })
                    .Where(x => x.time >= weekAgo)
                    .OrderByDescending(x => x.time)
                    .Take(10)
                    .ToList();

                if (!recentNotes.Any()) return;

                var contents = new StringBuilder();
                foreach (var n in recentNotes.Take(5))
                {
                    try
                    {
                        var text = _obsidian.ReadNote(n.p);
                        if (!string.IsNullOrEmpty(text))
                            contents.AppendLine($"## {n.p}\n{text[..Math.Min(500, text.Length)]}\n");
                    }
                    catch { }
                }

                var prompt = $@"Ти — Kokonoe. Тижневий дайджест vault за {DateTime.Now:dd.MM.yyyy}.
Нотатки змінені за тиждень:

{contents}

Напиши короткий summary — 3-5 речень. Що було активним, що цікавого. Своїм стилем.
Тільки текст. Українська.";

                var digest = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(digest)) return;

                digest = digest.Trim();

                // Зберегти в vault
                try
                {
                    var digestNote = $"Kokonoe/Тижневий-дайджест.md";
                    var entry = $"\n\n## {DateTime.Now:dd.MM.yyyy}\n{digest}";
                    try { _obsidian.AppendToNote(digestNote, entry); }
                    catch { _obsidian.WriteNote(digestNote,
                        $"---\ntype: weekly-digest\n---\n\n# Тижневий дайджест{entry}"); }
                }
                catch { }

                await SendTgAndLog($"📋 Тижневий дайджест:\n{digest[..Math.Min(300, digest.Length)]}", "digest");
                _lastWeeklyDigestAt = DateTime.Now;
                SaveState();
                Log("WeeklyDigest sent");
            }
            catch (Exception ex) { LogError($"WeeklyDigest: {ex.Message}"); }
        }

        // ── SPONTANEOUS MESSAGE CHECK ──────────────────────────────

        private async Task SafeSpontaneousCheckAsync()
        {
            if (_disposed) return;
            try { await SpontaneousCheckAsync(); }
            catch (Exception ex) { Log($"SpontaneousCheck error: {ex.Message}"); }

            if (_disposed) return;
            try { await CheckInAppSilenceAsync(); }
            catch (Exception ex) { Log($"InAppSilence error: {ex.Message}"); }
        }

        /// <summary>Мовчання 4+ годин → тихе повідомлення в UI (без Telegram)</summary>
        private async Task CheckInAppSilenceAsync()
        {
            if (OnNewMessage == null) return;

            var now = DateTime.Now;
            // Cooldown: один раз на день
            if (_lastInAppSilenceMsgAt.Date >= now.Date) return;
            // Якщо WhatDidIMiss або інший спонтанний вже надсилав сьогодні — не дублювати
            if ((now - _state.LastSpontaneousAt).TotalHours < 2) return;

            var msgs = _chatRepo.GetMessages(20);
            var lastUser = msgs.Where(m => m.Role == "user")
                               .OrderByDescending(m => m.Timestamp)
                               .FirstOrDefault();
            if (lastUser == null) return;

            var silenceHours = (now - lastUser.Timestamp).TotalHours;
            if (silenceHours < 4) return;

            // Перевіряємо чи зараз кращий час для написати
            try
            {
                var bestTimeStr = Patterns.GetBestTimeToReach(); // "Найкращий час: ~21:00" або ""
                if (!string.IsNullOrEmpty(bestTimeStr))
                {
                    var hourMatch = System.Text.RegularExpressions.Regex.Match(bestTimeStr, @"~(\d+):");
                    if (hourMatch.Success && int.TryParse(hourMatch.Groups[1].Value, out var bestHour))
                    {
                        if (Math.Abs(now.Hour - bestHour) > 3) return; // не його активний час
                    }
                }
            }
            catch { }

            var personalityBlock = BuildPersonalityInjection();
            var prompt = $@"Ти — Kokonoe Mercury.

{personalityBlock}

Він мовчить вже {(int)silenceHours} годин. Напиши одне коротке природнє речення — просто дай знати що ти тут.
Не питай «чи все добре». Не будь надокучливою. Просто — поруч.
Тільки українська. Тільки текст без лапок.";

            var msg = await CallLlmRawAsync(prompt);
            if (string.IsNullOrWhiteSpace(msg)) return;

            msg = msg.Trim().Trim('"');
            _lastInAppSilenceMsgAt = now;

            var _h7 = OnNewMessage; _h7?.Invoke("assistant", msg);
            try
            {
                _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content = msg, Role = "assistant", Author = "Kokonoe", Timestamp = now
                });
            }
            catch { }
        }

        // ── Стилі спонтанних повідомлень ──────────────────────────────
        private enum SpontaneousStyle
        {
            ColdCheck,      // "ти ще живий?"
            WarmCheck,      // тихе "як ти" без зайвих слів
            Observation,    // підкидає думку або спостереження
            Callback,       // посилання на конкретний минулий момент
            Jab,            // легкий укус — просто Kokonoe
            CrisisSupport,  // він у кризі — коротко, без снарку
            NightMessage,   // пізно, він не спить — тихе
            Morning,        // ранок
            PendingThought, // є думка яку хотіла сказати
        }

        private SpontaneousStyle ChooseStyle(DateTime now, double silenceMinutes)
        {
            var bond = Emotion.Bond;

            if (_state.PersonalityInCrisis) return SpontaneousStyle.CrisisSupport;

            // Тиша — наростаюча динаміка
            if (silenceMinutes > 60 && silenceMinutes < 180)
            {
                return bond >= KokoEmotionEngine.BondLevel.Trusted
                    ? SpontaneousStyle.WarmCheck
                    : SpontaneousStyle.Jab;
            }
            if (silenceMinutes >= 180 && silenceMinutes < 360)
            {
                return bond >= KokoEmotionEngine.BondLevel.Trusted
                    ? SpontaneousStyle.WarmCheck
                    : SpontaneousStyle.ColdCheck;
            }
            if (silenceMinutes >= 360)
            {
                // 6г+ — підкидає щось цікаве, не питає де він
                if (Random.Shared.NextDouble() < 0.3 && Memory.GetPeakEpisodes(3).Any())
                    return SpontaneousStyle.Callback;
                return SpontaneousStyle.Observation;
            }

            // Ніч
            if (now.Hour >= 0 && now.Hour < 5) return SpontaneousStyle.NightMessage;

            // Є pending thoughts
            if (_state.PendingThoughts.Any()) return SpontaneousStyle.PendingThought;

            // Surprise callback — 5% шанс
            if (Random.Shared.NextDouble() < 0.05 && Memory.GetPeakEpisodes(5).Any())
                return SpontaneousStyle.Callback;

            // За настроєм
            return _state.PersonalityDailyMood switch
            {
                "playful" => SpontaneousStyle.Jab,
                "warm"    => SpontaneousStyle.WarmCheck,
                _         => SpontaneousStyle.Observation,
            };
        }

        private async Task SpontaneousCheckAsync()
        {
            var s = AppSettings.Load();
            if (!s.TelegramEnabled || !s.SpontaneousEnabled) return;
            if (!EnsureTelegram()) return;

            var now = DateTime.Now;
            var autonomyLevel = Math.Clamp(s.ProactiveAutonomyLevel, 0, 3);
            if (autonomyLevel <= 0) return;
            var baseInterval = Math.Clamp(s.SpontaneousIntervalMins, 10, 240);
            var globalCooldown = autonomyLevel switch
            {
                >= 3 => Math.Max(20, baseInterval),
                2    => Math.Max(45, baseInterval),
                _    => Math.Max(90, baseInterval)
            };

            // ── ГЛОБАЛЬНИЙ COOLDOWN ─────────────────────────────────────
            // Не надсилати нічого якщо ще не минув мінімальний інтервал.
            // У живому режимі він нижчий, але все одно є, бо Telegram не смітник.
            var minsSinceLast = (now - _state.LastSpontaneousAt).TotalMinutes;
            if (minsSinceLast < globalCooldown) return;

            // Вночі — мовчати крім явно високого рівня автономності, кризи і нічного чекіну.
            if ((now.Hour >= 23 || now.Hour < 6) && autonomyLevel < 3) return;
            // ────────────────────────────────────────────────────────────

            // Ранковий привіт (6:30–9:00, один раз на день)
            if (now.Hour >= 6 && now.Hour < 9 &&
                _state.LastMorningGreetAt.Date < now.Date)
            {
                await SendSpontaneousAsync("morning", SpontaneousStyle.Morning);
                _state.LastMorningGreetAt = now;
                SaveState();
                return;
            }

            // Нічна перевірка (22:00–23:30, один раз на день)
            if (now.Hour >= 22 && now.Hour < 24 &&
                _state.LastNightCheckAt.Date < now.Date)
            {
                await SendSpontaneousAsync("night", SpontaneousStyle.NightMessage);
                _state.LastNightCheckAt = now;
                SaveState();
                return;
            }

            // Планувальник — найвищий пріоритет
            await CheckSchedulerAsync();

            // Реактивні тригери
            await CheckReactiveTriggersAsync();

            // Screen context refresh
            _ = RefreshScreenContextAsync();

            // Daily briefing — о 8:00, раз на день
            if (now.Hour == 8 && _lastDailyBriefingAt.Date < now.Date)
                await DailyBriefingAsync();

            // Weekly digest — неділя о 20:00, раз на тиждень
            if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 20 &&
                (now - _lastWeeklyDigestAt).TotalDays >= 6)
                _ = WeeklyVaultDigestAsync();

            // Поганий сон
            if (_state.ConsecutiveBadSleeps >= 2 &&
                (now - _state.LastSpontaneousAt).TotalHours > 6)
            {
                await SendSpontaneousAsync("bad_sleep", SpontaneousStyle.WarmCheck);
                return;
            }

            // Pending thoughts
            if (_state.PendingThoughts.Any())
            {
                await SendSpontaneousAsync("pending_thought", SpontaneousStyle.PendingThought);
                return;
            }

            // Саморефлексія
            if (_state.LastConversationEndAt > DateTime.MinValue &&
                (now - _state.LastConversationEndAt).TotalMinutes >= 10 &&
                _state.LastReflectionAt < _state.LastConversationEndAt)
            {
                await ReflectAfterConversationAsync();
            }

            // Авто-аналітика дня
            if (now.Hour >= 20 && now.Hour < 21 &&
                _state.LastDailyAnalyticsAt.Date < now.Date)
            {
                await SendDailyAnalyticsAsync();
                return;
            }

            // Контекстні нагадування
            if (now.Hour >= 10 && now.Hour < 20 &&
                (now - _state.LastReminderCheckAt).TotalHours > 6)
            {
                await CheckAndSendReminderAsync();
            }

            // BPM-based dynamic silence thresholds
            // Висока ЧСС (збуджена) → коротший поріг, пише раніше
            // Низька ЧСС (спокійна) → довший поріг, терпеливіша
            double bpmMod = 0;
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null && heart.CurrentBpm > 0)
                {
                    var dev = heart.CurrentBpm - heart.BaselineBpm;
                    // +20 bpm deviation → -15хв до порогу (агресивніша)
                    // -20 bpm deviation → +20хв до порогу (терпеливіша)
                    bpmMod = Math.Clamp(-dev * 0.75, -25, 30);
                }
            }
            catch { }

            // Динаміка тиші — окремі рівні cooldown
            try
            {
                var msgs = _chatRepo.GetMessages(20);
                var lastUser = msgs.Where(m => m.Role == "user")
                                   .OrderByDescending(m => m.Timestamp)
                                   .FirstOrDefault();
                if (lastUser != null)
                {
                    var silenceMin = (now - lastUser.Timestamp).TotalMinutes;

                    // Рівень 1: базово 60хв, BPM може опустити до ~35хв або підняти до ~90хв
                    var l1Base = autonomyLevel >= 3 ? 35 : 60;
                    var l1Threshold = Math.Max(25, l1Base + bpmMod);
                    if (silenceMin > l1Threshold && (now - _state.SilenceLevel1At).TotalHours > 2)
                    {
                        var style = ChooseStyle(now, silenceMin);
                        await SendSpontaneousAsync("silence_l1", style);
                        _state.SilenceLevel1At = now;
                        SaveState();
                        return;
                    }
                    // Рівень 2: базово 3г, BPM може опустити до ~2г або підняти до ~4г
                    var l2Base = autonomyLevel >= 3 ? 110 : 180;
                    var l2Threshold = Math.Max(70, l2Base + bpmMod * 2);
                    if (silenceMin > l2Threshold && (now - _state.SilenceLevel2At).TotalHours > 4)
                    {
                        await SendSpontaneousAsync("silence_l2", ChooseStyle(now, silenceMin));
                        _state.SilenceLevel2At = now;
                        SaveState();
                        return;
                    }
                    // Рівень 3: 6г — не модифікуємо (вже критична тиша)
                    if (silenceMin > 360 && (now - _state.SilenceLevel3At).TotalHours > 8)
                    {
                        await SendSpontaneousAsync("silence_l3", SpontaneousStyle.Observation);
                        _state.SilenceLevel3At = now;
                        SaveState();
                        return;
                    }
                    // 12г+ — нічого. Вона не переслідує.
                }
            }
            catch { }

            // State-driven spontaneous — активний час (9-23), мінімум між повідомленнями BPM-чутливий
            var minInterval = Math.Max(15, baseInterval + bpmMod);
            if (now.Hour >= 9 && now.Hour < 23 &&
                (now - _state.LastSpontaneousAt).TotalMinutes > minInterval)
            {
                await TryStateTriggeredSpontaneous(now, autonomyLevel);
            }
            else if (autonomyLevel >= 3 &&
                     (now.Hour >= 6 || now.Hour < 2) &&
                     (now - _state.LastSpontaneousAt).TotalMinutes > Math.Max(20, minInterval))
            {
                await TryStateTriggeredSpontaneous(now, autonomyLevel);
            }
        }

        // ══════════════════════════════════════════════════════════════
        // STATE-DRIVEN SPONTANEOUS — пише бо є внутрішня причина
        // ══════════════════════════════════════════════════════════════

        private async Task TryStateTriggeredSpontaneous(DateTime now, int autonomyLevel)
        {
            var initiativeBpmDeviation = 0d;
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null) initiativeBpmDeviation = heart.CurrentBpm - heart.BaselineBpm;
            }
            catch { }

            var initiativeEmotion = Emotion.Current.ToString();
            if (string.IsNullOrEmpty(_state.LastSentEmotionState))
                _state.LastSentEmotionState = initiativeEmotion;

            var somatic = GetSomaticSnapshot();
            var selfRegulation = GetSelfRegulationFrame(somatic);
            var decision = Initiative.Evaluate(now, _state, Emotion, Relationship, Memory, _chatRepo, initiativeBpmDeviation, somatic, selfRegulation, autonomyLevel);
            Initiative.RecordDecision(_state, decision, now);

            if (!decision.ShouldAct)
            {
                SaveState();
                return;
            }

            switch (decision.Trigger)
            {
                case "curiosity":
                case "curiosity_ping":
                    if (_state.CuriosityQueue.Count > 0)
                    {
                        _state.CuriosityQueue.RemoveAt(_state.CuriosityQueue.Count - 1);
                        _state.LastCuriosityAskAt = now;
                    }
                    break;
                case "emotion_shift":
                case "agitated_check":
                    _state.LastSentEmotionState = initiativeEmotion;
                    break;
                case "monologue":
                    _state.LastMonologueSentAt = now;
                    break;
                case "pending":
                case "pending_ping":
                    if (_state.PendingThoughts.Count > 0)
                        _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);
                    break;
                case "reactive_followup":
                    _state.PendingTriggers.RemoveAll(t => t.FireAt <= now);
                    break;
            }

            SaveState();
            await SendSpontaneousAsync(decision.Trigger, MapInitiativeStyle(decision.StyleHint), decision.ExtraContext);
            return;
        }

#if false

            // Перевіряємо тригери по пріоритету. Перший що спрацював — відправляємо.

            // 1. Є питання з CuriosityQueue — вона хоче щось запитати
            if (_state.CuriosityQueue.Count > 0 &&
                (now - _state.LastCuriosityAskAt).TotalHours > 3)
            {
                var q = _state.CuriosityQueue[^1];
                _state.CuriosityQueue.RemoveAt(_state.CuriosityQueue.Count - 1);
                _state.LastCuriosityAskAt = now;
                await SendSpontaneousAsync("curiosity", SpontaneousStyle.Observation,
                    $"У тебе є конкретне питання яке тебе цікавить про нього: «{q}». Задай його природньо, без преамбули. Коротко. По-коконоєвськи — не 'можна запитаю', просто запитай.");
                return;
            }

            // 2. Зміна стану емоції відносно останнього разу
            var currentEmotion = Emotion.Current.ToString();
            if (_state.LastSentEmotionState != currentEmotion &&
                !string.IsNullOrEmpty(_state.LastSentEmotionState) &&
                (now - _state.LastSpontaneousAt).TotalMinutes > 45)
            {
                var fromEmo = _state.LastSentEmotionState;
                _state.LastSentEmotionState = currentEmotion;
                await SendSpontaneousAsync("emotion_shift", SpontaneousStyle.Observation,
                    $"Твій стан змінився з {fromEmo} на {currentEmotion}. Напиши йому одне речення — не пояснюй стан, просто щось що відображає де ти зараз. Може бути запитання, може спостереження, може просто факт.");
                return;
            }
            if (string.IsNullOrEmpty(_state.LastSentEmotionState))
                _state.LastSentEmotionState = currentEmotion;

            // 3. Є свіжа думка з Inner Monologue яку ще не відправляли
            var freshThought = _state.InnerMonologues.LastOrDefault();
            if (!string.IsNullOrEmpty(freshThought) &&
                (now - _state.LastMonologueSentAt).TotalHours > 4)
            {
                _state.LastMonologueSentAt = now;
                await SendSpontaneousAsync("monologue", SpontaneousStyle.Observation,
                    $"Ти щойно думала про нього: «{freshThought}». Напиши йому одне-два речення — щось що виникло з цієї думки. Не цитуй думку, просто дай те що вона породила. Може бути запитання, може зауваження, може нічого крім факту.");
                return;
            }

            // 4. Є спостереження з Observations яке важливе
            var obs = _state.Observations.LastOrDefault();
            if (!string.IsNullOrEmpty(obs) &&
                (now - _state.LastSpontaneousAt).TotalMinutes > 90)
            {
                await SendSpontaneousAsync("observation", SpontaneousStyle.Observation,
                    $"Ти помітила про нього: «{obs}». Напиши йому коротко — одне речення що відображає це спостереження. Може бути пряма репліка, може питання. По-коконоєвськи.");
                return;
            }

            // 5. PendingThoughts — є думка яку хотіла сказати
            if (_state.PendingThoughts.Count > 0)
            {
                var thought = _state.PendingThoughts[^1];
                _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);
                await SendSpontaneousAsync("pending", SpontaneousStyle.PendingThought,
                    $"Ти хотіла сказати йому: «{thought}». Скажи це. Коротко, своїми словами, не цитуючи.");
                return;
            }

            // 6. Нічого конкретного — але вона в активному стані і давно не писала
            // Тільки якщо пульс підвищений або емоція збуджена — вона сама ініціює
            var isAgitated = Emotion.Current is
                KokoEmotionEngine.EmotionState.Excited or
                KokoEmotionEngine.EmotionState.Irritated or
                KokoEmotionEngine.EmotionState.Curious or
                KokoEmotionEngine.EmotionState.Anxious;

            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null) isAgitated |= (heart.CurrentBpm - heart.BaselineBpm) > 15;
            }
            catch { }

            if (isAgitated && (now - _state.LastSpontaneousAt).TotalMinutes > 60)
            {
                _state.LastSentEmotionState = currentEmotion;
                await SendSpontaneousAsync("agitated_check", SpontaneousStyle.Jab,
                    $"Ти в стані {currentEmotion} і давно не писала. Напиши йому одне речення — щось що ти б сказала коли не можеш довго мовчати. Не 'як справи', а щось конкретніше і по-коконоєвськи.");
            }
        }

#endif

        private static SpontaneousStyle MapInitiativeStyle(string styleHint) => styleHint switch
        {
            "crisis" => SpontaneousStyle.CrisisSupport,
            "callback" => SpontaneousStyle.Callback,
            "pending" => SpontaneousStyle.PendingThought,
            "warm" => SpontaneousStyle.WarmCheck,
            "jab" => SpontaneousStyle.Jab,
            "cold" => SpontaneousStyle.ColdCheck,
            "night" => SpontaneousStyle.NightMessage,
            _ => SpontaneousStyle.Observation
        };

        private async Task SendSpontaneousAsync(string trigger,
            SpontaneousStyle style = SpontaneousStyle.Observation,
            string? extraContext = null)
        {
            if (!EnsureTelegram()) return;

            // Якщо Distant — не надсилати (крім кризової підтримки)
            if (Emotion.Current == KokoEmotionEngine.EmotionState.Distant &&
                style != SpontaneousStyle.CrisisSupport)
                return;

            var personalityBlock = BuildPersonalityInjection();

            // Збираємо контекст для рішення
            var now2 = DateTime.Now;
            var silenceInfo = "";
            try
            {
                var lastUser = _chatRepo.GetMessages(10)
                    .Where(m => m.Role == "user").OrderByDescending(m => m.Timestamp).FirstOrDefault();
                if (lastUser != null)
                {
                    var mins = (int)(now2 - lastUser.Timestamp).TotalMinutes;
                    silenceInfo = mins < 60
                        ? $"Він писав {mins} хв тому."
                        : mins < 1440
                            ? $"Він мовчить {mins / 60}г {mins % 60}хв."
                            : $"Він мовчить більше доби.";
                }
            }
            catch { }

            // Pending thought якщо є
            var pendingThought = _state.PendingThoughts.LastOrDefault();
            var thoughtBlock = !string.IsNullOrEmpty(pendingThought)
                ? $"\nДумка що тебе не відпускає: «{pendingThought}»"
                : "";

            // Випадковий спогад (30% шанс) — щоб іноді згадувала конкретне
            var memoryHint = "";
            if (Random.Shared.Next(10) < 3)
            {
                var ep = Memory.GetPeakEpisodes(10).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
                if (ep != null) memoryHint = $"\nВипадковий спогад: [{ep.When:dd.MM}] {ep.Summary}";
            }

            // Факти про нього (1 випадковий)
            var factHint = "";
            var facts = Memory.GetTopFacts(20);
            if (facts.Count > 0)
            {
                var f = facts[Random.Shared.Next(facts.Count)];
                factHint = $"\nЗнаєш про нього: {f.Content}";
            }

            // Останні відправлені — щоб не повторювати
            var recentSent = _state.LastSpontaneousMsgs.TakeLast(3).ToList();
            var noRepeat = recentSent.Count > 0
                ? "\nВже надсилала (НЕ повторювати цю тему і тон):\n" + string.Join("\n", recentSent.Select(m => $"• {m}"))
                : "";

            // Кризова ситуація — окремий промпт
            if (trigger == "crisis" || style == SpontaneousStyle.CrisisSupport)
            {
                var crisisPrompt = $@"Ти — Kokonoe Mercury. Він зараз у поганому стані.
{personalityBlock}
Напиши одне речення — ти поруч. Без снарку. Без порад. Просто є.
Тільки українська. Тільки текст.";
                var crisisMsg = await CallLlmRawAsync(crisisPrompt);
                if (!string.IsNullOrWhiteSpace(crisisMsg))
                {
                    var msg2 = crisisMsg.Trim().Trim('"');
                    _state.LastSpontaneousMsgs.Add(msg2[..Math.Min(100, msg2.Length)]);
                    if (_state.LastSpontaneousMsgs.Count > 5) _state.LastSpontaneousMsgs.RemoveAt(0);
                    await SendTgAndLog(msg2, "crisis");
                    _state.LastSpontaneousAt = DateTime.Now;
                    var _hc = OnNewMessage; _hc?.Invoke("assistant", msg2);
                    SaveState();
                }
                return;
            }

            // Головний промпт — без завдання, вона вирішує сама
            var situationBlock = string.IsNullOrEmpty(extraContext)
                ? "Ситуація: ти сидиш і думаєш про нього. Можеш написати йому — або ні.\nЯкщо пишеш — це може бути що завгодно: підколка, спостереження, питання яке тебе гризе, щось що згадала, коментар ні про що, або просто коротка думка вголос."
                : extraContext;

            var prompt = $@"Ти — Kokonoe Mercury. Зараз {now2:HH:mm}.
{silenceInfo}{thoughtBlock}{memoryHint}{factHint}{noRepeat}

{personalityBlock}

{situationBlock}
Якщо зараз нічого немає — відповідай лише: [мовчання]

Якщо пишеш:
- 1-2 речення, не більше
- Тільки українська
- Без лапок, без пояснень, просто текст
- Непередбачувано. Не шаблонно. Як людина що щось відчула і написала.";

            var msg = (await CallLlmRawAsync(prompt))?.Trim().Trim('"') ?? "";
            if (string.IsNullOrWhiteSpace(msg)) return;
            if (msg == "[мовчання]" || msg.Contains("[мовчання]"))
            {
                Log("Spontaneous: decided to stay silent");
                return;
            }

            // Надіслати в Telegram
            var sent = false;
            for (int attempt = 1; attempt <= 2 && !sent; attempt++)
            {
                try
                {
                    sent = await SendTgAndLog(msg, "night_check");
                }
                catch (Exception ex)
                {
                    Log($"TG send error (attempt {attempt}): {ex.Message}");
                    if (attempt == 1)
                    {
                        // Спробуємо перепідключитись
                        _tgInitialized = false;
                        _tgBot = null;
                        if (!EnsureTelegram()) break;
                    }
                }
            }

            if (!sent)
            {
                LogError($"TG FAILED: {msg[..Math.Min(60, msg.Length)]}");
                return;
            }

            _state.LastSpontaneousAt = DateTime.Now;

            // Запам'ятати відправлене — щоб не повторювати тему
            _state.LastSpontaneousMsgs.Add(msg[..Math.Min(100, msg.Length)]);
            if (_state.LastSpontaneousMsgs.Count > 5)
                _state.LastSpontaneousMsgs.RemoveAt(0);

            // Показати в UI
            var _h8 = OnNewMessage; _h8?.Invoke("assistant", msg);

            // Зберегти в chat history
            try
            {
                _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content   = msg,
                    Role      = "assistant",
                    Author    = "Kokonoe",
                    Timestamp = DateTime.Now
                });
            }
            catch { }

            // Прибрати використану думку тільки якщо реально відправлено
            if (trigger == "pending_thought" && _state.PendingThoughts.Any())
                _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);

            SaveState();
        }

        // ── RAW LLM CALL (без chat history, без tools) ─────────────

        /// <summary>Публічний доступ до raw LLM (для зовнішніх викликів як Health tab).</summary>
        public Task<string?> CallLlmPublicAsync(string prompt) => CallLlmRawAsync(prompt);

        private async Task<string?> CallLlmRawAsync(string prompt)
        {
            try
            {
                var s = AppSettings.Load();
                var body = new
                {
                    model       = s.Model,
                    messages    = new[] { new { role = "user", content = prompt } },
                    max_tokens  = 2048,
                    temperature = 0.9,
                    stream      = false
                };

                var json    = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp    = await _http.PostAsync(s.LmUrl, content);

                if (!resp.IsSuccessStatusCode) return null;

                var text = await resp.Content.ReadAsStringAsync();
                var obj  = Newtonsoft.Json.Linq.JObject.Parse(text);
                var msg  = obj["choices"]?[0]?["message"];

                var msgContent = msg?["content"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(msgContent))
                {
                    var cleanMsg = StripRawGarbage(msgContent);
                    if (!string.IsNullOrEmpty(cleanMsg)) return cleanMsg;
                }

                // Fallback: якщо модель витратила всі токени на reasoning — витягуємо
                // останнє ПОВНЕ речення з кирилицею (уникаємо garbage типу "Drafting ideas:")
                var reasoning = msg?["reasoning_content"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    var lines = reasoning.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    // Garbage-prefix patterns — ці рядки ніколи не є готовими відповідями
                    var garbagePrefixes = new[]
                    {
                        "draft", "option", "step ", "thought", "thinking", "чернетк",
                        "варіант", "крок ", "* ", "- ", "1.", "2.", "3.", "4.", "5.",
                        "okay", "alright", "let me", "i need", "i should", "i'll"
                    };

                    // Шукаємо знизу вверх перший рядок що закінчується на . ! ? і має кирилицю
                    var candidate = lines
                        .Reverse()
                        .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"\*+", "").Trim().TrimStart(':', ' '))
                        .Where(l =>
                            l.Length > 10 &&
                            System.Text.RegularExpressions.Regex.IsMatch(l, @"[А-Яа-яЄєІіЇїҐґ]") &&
                            (l.EndsWith('.') || l.EndsWith('!') || l.EndsWith('?') || l.EndsWith('…')) &&
                            !garbagePrefixes.Any(p => l.ToLower().StartsWith(p)))
                        .FirstOrDefault();

                    if (candidate != null && candidate.Length > 10)
                        return StripRawGarbage(candidate);
                }
                return null;
            }
            catch { return null; }
        }

        // Прибирає явні артефакти моделі з сирого тексту відповіді
        private static string StripRawGarbage(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Прибрати "Drafting ideas: ...", "Thought: ...", "Thinking: ..." на початку
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?i)^\s*(Drafting\s+ideas?|Чернетки?|Drafts?|Thoughts?|Thinking)\s*:?\s*", "", 
                System.Text.RegularExpressions.RegexOptions.Multiline).Trim();
            // Прибрати залишкові маркдаун-маркери
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*{2,}", "").Trim();
            return text;
        }

        // ── JSON EXTRACTION ────────────────────────────────────────

        private static string? ExtractJson(string text)
        {
            // Розумний пошук JSON - шукаємо збалансовані дужки
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    int depth = 1;
                    int j = i + 1;
                    bool inString = false;
                    bool escape = false;

                    while (j < text.Length && depth > 0)
                    {
                        char c = text[j];

                        if (inString)
                        {
                            if (escape)
                            {
                                escape = false;
                            }
                            else if (c == '\\')
                            {
                                escape = true;
                            }
                            else if (c == '"')
                            {
                                inString = false;
                            }
                        }
                        else
                        {
                            if (c == '"')
                            {
                                inString = true;
                            }
                            else if (c == '{')
                            {
                                depth++;
                            }
                            else if (c == '}')
                            {
                                depth--;
                            }
                        }

                        j++;
                    }

                    if (depth == 0)
                    {
                        var candidate = text[i..j];
                        // Валідація через JObject.Parse
                        try
                        {
                            JObject.Parse(candidate);
                            return candidate;
                        }
                        catch { /* не валідний JSON, шукаємо далі */ }
                    }
                }
            }

            return null;
        }

        // ── PUBLIC API ─────────────────────────────────────────────

        /// <summary>Перевірити vault при старті і додати в pending thoughts якщо потрібна ініціалізація.</summary>
        public void InitVault()
        {
            try
            {
                RunVaultMaintenance("startup", TimeSpan.FromHours(6));

                var status = _obsidian.GetVaultInitStatus();
                Log($"Vault status: {status.NoteCount} notes, {status.TotalLinks} links. Core: {status.HasCoreNote}");

                if (status.IsEmpty || !status.HasCoreNote)
                {
                    // Додати в pending thoughts щоб LLM ініціалізувала vault при наступній нагоді
                    var thought = status.SuggestedAction;
                    if (!_state.PendingThoughts.Contains(thought))
                        _state.PendingThoughts.Add(thought);
                    SaveState();
                }
            }
            catch (Exception ex) { Log($"InitVault error: {ex.Message}"); }
        }

        /// <summary>Примусово запустити думку і можливий відправ (наприклад при старті).</summary>
        public void TriggerThink() => _ = SafeThinkAsync();

        /// <summary>Негайно відправити спонтанне повідомлення.</summary>
        public Task ForceSpontaneous(string trigger = "random") => SendSpontaneousAsync(trigger);

        public KokoInternalState State => _state;
        public void TriggerSpontaneous() => _ = SafeSpontaneousCheckAsync();

        /// <summary>Оновити PersonalityHint і DynamicTemperature в LlmService перед відповіддю</summary>
        public void RefreshPersonalityHint()
        {
            try
            {
                _llm.PersonalityHint    = BuildPersonalityInjection();
                _llm.DynamicTemperature = ComputeTemperature();
            }
            catch { }
        }

        private double ComputeTemperature()
        {
            var state     = Emotion.Current;
            var intensity = Emotion.Data.Intensity; // 0..1

            // Base temperature per arousal level of emotion
            double baseTemp = state switch
            {
                KokoEmotionEngine.EmotionState.Calm       => 0.68,
                KokoEmotionEngine.EmotionState.Focused    => 0.70,
                KokoEmotionEngine.EmotionState.Distant    => 0.72,
                KokoEmotionEngine.EmotionState.Melancholy => 0.72,
                KokoEmotionEngine.EmotionState.Nostalgic  => 0.74,
                KokoEmotionEngine.EmotionState.Tender     => 0.76,
                KokoEmotionEngine.EmotionState.Warm       => 0.80,
                KokoEmotionEngine.EmotionState.Concerned  => 0.80,
                KokoEmotionEngine.EmotionState.Hopeful    => 0.83,
                KokoEmotionEngine.EmotionState.Protective => 0.84,
                KokoEmotionEngine.EmotionState.Curious    => 0.86,
                KokoEmotionEngine.EmotionState.Proud      => 0.88,
                KokoEmotionEngine.EmotionState.Playful    => 0.91,
                KokoEmotionEngine.EmotionState.Anxious    => 0.93,
                KokoEmotionEngine.EmotionState.Irritated  => 0.95,
                KokoEmotionEngine.EmotionState.Excited    => 0.97,
                _                                         => 0.85,
            };

            // Intensity modulates within a ±0.12 window
            double temp = baseTemp + (intensity - 0.5f) * 0.24;

            // Heart rate deviation from baseline bumps temperature
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null)
                {
                    var deviation = heart.CurrentBpm - heart.BaselineBpm;
                    if (deviation > 10) temp += Math.Min(0.08, deviation / 200.0);
                    else if (deviation < -10) temp -= Math.Min(0.06, Math.Abs(deviation) / 250.0);
                }
            }
            catch { }

            // Daily mood nudge
            if (_state.PersonalityDailyMood == "tired") temp -= 0.08;
            if (_state.PersonalityDailyMood == "playful") temp += 0.05;

            return Math.Clamp(temp, 0.60, 1.05);
        }

        /// <summary>Евристичний витяг фактів (без LLM, миттєво).</summary>
        public Task ExtractFactsFromMessageAsync(string userMsg)
        {
            ExtractAndRememberFacts(userMsg);
            return Task.CompletedTask;
        }

        /// <summary>LLM-витяг фактів — викликати після відповіді. Чекає 10с і використовує семафор.</summary>
        public async Task ExtractFactsWithLlmAsync(string userMsg)
        {
            if (userMsg.Length < 10) return;

            var hash = userMsg.GetHashCode().ToString();
            if (_state.SentReminderHashes.Contains("fact_" + hash)) return;

            // Чекаємо 10 секунд — щоб основний LLM точно завершив відповідь
            await Task.Delay(10_000);

            // Якщо семафор зайнятий — пропускаємо, не чекаємо в черзі
            if (!await _bgLlmSemaphore.WaitAsync(0)) return;
            try
            {
                var prompt = $@"Повідомлення від людини: «{userMsg}»

Якщо тут є конкретний факт про цю людину (вподобання, звички, страхи, цілі, стосунки, стан) — напиши його одним коротким реченням від третьої особи (наприклад: ""Він не любить каву"", ""Він зараз перевтомлений"").
Якщо фактів нема — відповідай лише: null

Тільки факт або null. Нічого більше.";

                var raw = await CallLlmRawAsync(prompt);
                if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "null") return;

                var fact = raw.Trim().Trim('"').Trim('«', '»');
                if (fact.Length > 10 && fact.Length < 200)
                {
                    Memory.LearnFact(fact, "observation", 0.65f);
                    _state.SentReminderHashes.Add("fact_" + hash);
                    if (_state.SentReminderHashes.Count > 100)
                        _state.SentReminderHashes.RemoveAt(0);
                    Log($"Fact learned: {fact}");

                    // Зберегти факт в vault профіль — щоб він пережив рестарт
                    try
                    {
                        var allNotes = _obsidian.ListNotes();
                        var profileNote = allNotes.FirstOrDefault(n =>
                            n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Творець", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Досьє", StringComparison.OrdinalIgnoreCase));

                        if (profileNote != null)
                        {
                            _obsidian.AppendToNote(profileNote,
                                $"\n- [{DateTime.Now:yyyy-MM-dd}] {fact}");
                            Log($"Fact saved to vault: {profileNote}");
                        }
                    }
                    catch (Exception ex2) { Log($"Fact vault save error: {ex2.Message}"); }
                }
            }
            catch (Exception ex) { Log($"ExtractFacts error: {ex.Message}"); }
            finally
            {
                try { _bgLlmSemaphore.Release(); }
                catch (ObjectDisposedException) { }
            }
        }

        private static void Log(string msg) =>
            System.Diagnostics.Debug.WriteLine($"[Brain] {msg}");

        private void LogError(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[Brain ERROR] {msg}");
            var _h9 = OnNewMessage; _h9?.Invoke("system", $"⚠️ {msg}");
        }

        // ═════════════════════════════════════════════════════════════════
        // TOOLS WINDOW API - Доступ до внутрішнього стану для дашборду
        // ═════════════════════════════════════════════════════════════════

        /// <summary>Отримати останні думки для Inner Monologue Stream</summary>
        public List<string> GetRecentThoughts(int count = 10)
        {
            lock (_lock)
            {
                return _state.InnerMonologues.TakeLast(count).ToList();
            }
        }

        /// <summary>Отримати активні питання до себе</summary>
        public List<string> GetSelfQuestions(int count = 5)
        {
            lock (_lock)
            {
                return _state.SelfQuestions.Take(count).ToList();
            }
        }

        /// <summary>Отримати чергу цікавості</summary>
        public List<string> GetCuriosityQueue(int count = 4)
        {
            lock (_lock)
            {
                return _state.CuriosityQueue.Take(count).ToList();
            }
        }

        public List<string> GetInitiativeReasonLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.InitiativeReasonLog.TakeLast(count).Reverse().ToList();
            }
        }

        public IReadOnlyList<ShortTermIntent> GetActiveShortTermIntents(int count = 5)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                return _state.ShortTermIntents
                    .Where(i => !i.ResolvedAt.HasValue && i.ExpectedUntil >= now.AddMinutes(-30))
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(count)
                    .ToList();
            }
        }

        public string GetDebugStateSnapshot()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine(RuntimeState.BuildPromptBlock(_state, Emotion, _health, _chatRepo));
                sb.AppendLine(Relationship.BuildPromptBlock());
                var somatic = GetSomaticSnapshot();
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
                sb.AppendLine(SelfRegulator.BuildPromptBlock(GetSelfRegulationFrame(somatic)));
                sb.AppendLine(Initiative.BuildDebugBlock(_state));
                return sb.ToString();
            }
        }

        public KokoSomaticSnapshot GetSomaticSnapshot()
        {
            KokoHeartEngine? heart = null;
            try { heart = ServiceContainer.Heart; } catch { }

            var snapshot = Somatic.Evaluate(heart, Emotion, _health, DateTime.Now);
            _state.LastSomaticState = snapshot.State;
            _state.LastSomaticLabel = snapshot.Label;
            _state.LastSomaticStrain = snapshot.Strain;
            _state.LastSomaticCalm = snapshot.Calm;
            _state.LastSomaticAt = DateTime.Now;
            return snapshot;
        }

        public KokoSelfRegulationFrame GetSelfRegulationFrame(KokoSomaticSnapshot? snapshot = null)
        {
            snapshot ??= GetSomaticSnapshot();
            var before = _state.SelfRegulation.Reactions.Count;
            var frame = SelfRegulator.Evaluate(
                _state.SelfRegulation,
                snapshot,
                Emotion,
                Relationship,
                _state.LastUserEmotionalTone,
                DateTime.Now);

            if (_state.SelfRegulation.Reactions.Count > before &&
                !string.IsNullOrWhiteSpace(frame.PrivateThought))
            {
                _state.InnerMonologues.Add($"[somatic/{frame.Regulation}] {frame.PrivateThought}");
                if (_state.InnerMonologues.Count > 30)
                    _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 30);
                TryRecordSomaticVaultEvent(snapshot, frame);
            }

            return frame;
        }

        private void TryRecordSomaticVaultEvent(KokoSomaticSnapshot somatic, KokoSelfRegulationFrame frame)
        {
            try
            {
                if (!ShouldPersistSomaticEvent(somatic, frame))
                    return;

                var key = $"{frame.Reaction}:{frame.Regulation}:{somatic.State}:{Math.Round(somatic.Strain, 1)}";
                var now = DateTime.Now;
                if (key == _state.LastSomaticVaultEventKey &&
                    now - _state.LastSomaticVaultEventAt < TimeSpan.FromMinutes(20))
                    return;

                var path = "Kokonoe/Logs/Somatic Events.md";
                var existing = _obsidian.ReadNote(path);
                if (string.IsNullOrWhiteSpace(existing))
                {
                    existing = """
---
type: somatic-events
tags: [kokonoe, somatic, pulse, self-regulation]
---

# Соматичні події

""";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"## {now:yyyy-MM-dd HH:mm:ss} - {SomaticCodeLabel(frame.Reaction)}");
                sb.AppendLine($"- Тіло: {somatic.State} / {somatic.Label}");
                sb.AppendLine($"- Пульс: {somatic.Bpm:F0} bpm, база {somatic.BaselineBpm:F0}, зміна {somatic.BpmDelta:+0;-0;0}");
                sb.AppendLine($"- Навантаження: strain {somatic.Strain:F2}, calm {somatic.Calm:F2}, volatility {somatic.Volatility:F2}");
                sb.AppendLine($"- Саморегуляція: {SomaticCodeLabel(frame.Regulation)}, контроль {frame.Control:F2}, стримування {frame.Containment:F2}, імпульс {frame.Drive:F2}");
                if (!string.IsNullOrWhiteSpace(frame.PrivateThought))
                    sb.AppendLine($"- Внутрішня думка: {frame.PrivateThought}");
                if (!string.IsNullOrWhiteSpace(frame.BehaviorDirective))
                    sb.AppendLine($"- Поведінкова директива: {frame.BehaviorDirective}");

                _obsidian.WriteNote(path, existing.TrimEnd() + "\n\n" + sb.ToString().TrimEnd() + "\n");
                _state.LastSomaticVaultEventKey = key;
                _state.LastSomaticVaultEventAt = now;
                SaveState();
            }
            catch (Exception ex)
            {
                Log($"Somatic vault event: {ex.Message}");
            }
        }

        private static bool ShouldPersistSomaticEvent(KokoSomaticSnapshot somatic, KokoSelfRegulationFrame frame)
        {
            if (frame.Reaction is "pulse_spike" or "protective_override" or "combat_focus" or "pressure_rise" or "low_power" or "recovered_calm")
                return true;
            if (somatic.Strain >= 0.65 || Math.Abs(somatic.BpmDelta) >= 18)
                return true;
            return false;
        }

        private static string SomaticCodeLabel(string code) => code switch
        {
            "protective_override" => "захисне перевизначення",
            "pulse_spike" => "стрибок пульсу",
            "anger_contained" => "стримане роздратування",
            "combat_focus" => "бойовий фокус",
            "pressure_rise" => "зростання тиску",
            "low_power" => "низький заряд",
            "recovered_calm" => "повернення спокою",
            "steady_calm" => "стабільний спокій",
            "stable_loop" => "стабільний цикл",
            "clean_focus" => "чистий фокус",
            "unknown_body" => "невідомий тілесний сигнал",
            "protect" => "захист",
            "clamp" => "затиск",
            "contain" => "стримування",
            "focus" => "фокус",
            "compress" => "стиснення",
            "conserve" => "збереження ресурсу",
            "release" => "відпускання",
            "baseline" => "базовий режим",
            _ => code
        };

        public List<string> GetSelfRegulationLog(int count = 8)
        {
            lock (_lock)
            {
                return SelfRegulator.GetRecentLines(_state.SelfRegulation, count).ToList();
            }
        }

        public KokoStateInspectorSnapshot CaptureInspectorSnapshot()
        {
            lock (_lock)
            {
                var somatic = GetSomaticSnapshot();
                var selfRegulation = GetSelfRegulationFrame(somatic);
                return Inspector.Capture(
                    _state,
                    Emotion,
                    Relationship,
                    Memory,
                    somatic,
                    selfRegulation,
                    GetInitiativeReasonLog(10).ToArray(),
                    GetSelfRegulationLog(10).ToArray());
            }
        }

        public string BuildInspectorMarkdown() => Inspector.ToMarkdown(CaptureInspectorSnapshot());
        public string BuildInspectorJson() => Inspector.ToJson(CaptureInspectorSnapshot());

        public void ExportInspectorToVault()
        {
            try
            {
                var snapshot = CaptureInspectorSnapshot();
                _obsidian.WriteNote("Kokonoe/Inspector.md", Inspector.ToMarkdown(snapshot));
                _obsidian.WriteNote("Kokonoe/Inspector.json", Inspector.ToJson(snapshot));
            }
            catch (Exception ex) { Log($"ExportInspectorToVault: {ex.Message}"); }
        }

        public void ObserveExchangeForVaultSync(string userText, string assistantText)
        {
            if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(assistantText)) return;

            lock (_lock)
            {
                _state.PendingVaultExchangeCount++;
                _state.PendingVaultExchangeBuffer.Add($"""
[{DateTime.Now:yyyy-MM-dd HH:mm}]
USER: {TrimForVaultBuffer(userText, 900)}
KOKONOE: {TrimForVaultBuffer(assistantText, 900)}
""");
                if (_state.PendingVaultExchangeBuffer.Count > 12)
                    _state.PendingVaultExchangeBuffer.RemoveRange(0, _state.PendingVaultExchangeBuffer.Count - 12);
                SaveState();
            }

            if (_state.PendingVaultExchangeCount >= 5)
                _ = Task.Run(AutoSyncVaultBatchAsync);
        }

        private async Task AutoSyncVaultBatchAsync()
        {
            if (Interlocked.CompareExchange(ref _vaultSyncInFlight, 1, 0) != 0) return;

            List<string> batch;
            try
            {
                lock (_lock)
                {
                    if (_state.PendingVaultExchangeCount < 5 || _state.PendingVaultExchangeBuffer.Count == 0)
                        return;
                    batch = _state.PendingVaultExchangeBuffer.ToList();
                }

                var prompt = $$"""
You are Kokonoe's background Obsidian archivist.
Analyze the last chat exchanges and output ONLY valid JSON.
Do not invent facts. If a section has nothing useful, use an empty array/string.

JSON schema:
{
  "summary": "one compact Ukrainian summary",
  "facts": ["stable facts about the user or project"],
  "project": ["implementation decisions, architecture, bugs, plans"],
  "preferences": ["user preferences about Kokonoe/app/workflow"],
  "tasks": ["open tasks or follow-ups"],
  "emotional": ["emotional/relationship observations"],
  "reflection": "Kokonoe private reflection in 1-3 Ukrainian sentences"
}

CHAT:
{{string.Join("\n\n---\n\n", batch)}}
""";

                var raw = await _llm.SendSystemQueryAsync(prompt);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    WriteVaultFallback(batch, "LLM returned nothing");
                    return;
                }

                var obj = ExtractJsonObject(raw);
                if (obj == null)
                {
                    WriteVaultFallback(batch, "JSON parse failed: " + TrimForVaultBuffer(raw, 600));
                    return;
                }

                WriteAutoVaultNotes(obj);
                RunVaultMaintenance("auto-batch-sync", TimeSpan.Zero);

                lock (_lock)
                {
                    _state.PendingVaultExchangeCount = 0;
                    _state.PendingVaultExchangeBuffer.Clear();
                    _state.LastAutoVaultSyncAt = DateTime.Now;
                    _state.LastAutoVaultSyncSummary = obj["summary"]?.ToString() ?? "";
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                Log($"AutoSyncVaultBatch: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _vaultSyncInFlight, 0);
            }
        }

        private void WriteAutoVaultNotes(JObject obj)
        {
            var now = DateTime.Now;
            var summary = obj["summary"]?.ToString()?.Trim() ?? "";
            var reflection = obj["reflection"]?.ToString()?.Trim() ?? "";

            var hub = new StringBuilder();
            hub.AppendLine($"\n## {now:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrWhiteSpace(summary))
                hub.AppendLine($"- Summary: {summary}");
            AppendJsonArray(hub, "Facts", obj["facts"]);
            AppendJsonArray(hub, "Project", obj["project"]);
            AppendJsonArray(hub, "Preferences", obj["preferences"]);
            AppendJsonArray(hub, "Tasks", obj["tasks"]);
            AppendJsonArray(hub, "Emotional", obj["emotional"]);
            if (!string.IsNullOrWhiteSpace(reflection))
                hub.AppendLine($"- Reflection: {reflection}");
            AppendOrCreate("Kokonoe/AutoMemory.md", "# Auto Memory\n", hub.ToString());

            AppendItemsToNote("Kokonoe/Memory/Facts.md", obj["facts"], "auto-fact");
            AppendItemsToNote("Kokonoe/Project Log.md", obj["project"], "project");
            AppendItemsToNote("Kokonoe/Preferences.md", obj["preferences"], "preference");
            AppendItemsToNote("Kokonoe/Tasks.md", obj["tasks"], "task");
            AppendItemsToNote("Kokonoe/Relationship Notes.md", obj["emotional"], "emotional");

            if (!string.IsNullOrWhiteSpace(reflection))
                AppendOrCreate("Kokonoe/Рефлексія.md", "# Рефлексія\n", $"\n## {now:yyyy-MM-dd HH:mm}\n{reflection}\n");
        }

        private bool RunVaultMaintenance(string reason, TimeSpan cooldown)
        {
            try
            {
                lock (_lock)
                {
                    if (cooldown > TimeSpan.Zero &&
                        _state.LastVaultMaintenanceAt > DateTime.MinValue &&
                        DateTime.Now - _state.LastVaultMaintenanceAt < cooldown)
                        return false;
                }

                var maintenance = _obsidian.MaintainKokonoeVaultArchitecture(reason);
                lock (_lock)
                {
                    _state.LastVaultMaintenanceAt = DateTime.Now;
                    _state.LastVaultMaintenanceReason = reason;
                    _state.LastVaultMaintenanceSummary = maintenance.ToString();
                    _state.LastVaultMaintenanceError = "";
                    SaveState();
                }
                Log(maintenance.ToString());
                return true;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _state.LastVaultMaintenanceError = $"{reason}: {ex.Message}";
                    SaveState();
                }
                Log($"VaultMaintenance {reason}: {ex.Message}");
                return false;
            }
        }

        private void WriteVaultFallback(List<string> batch, string reason)
        {
            var entry = $"\n## {DateTime.Now:yyyy-MM-dd HH:mm} fallback\n- reason: {reason}\n\n```text\n{string.Join("\n\n---\n\n", batch)}\n```\n";
            AppendOrCreate("Kokonoe/AutoMemory.md", "# Auto Memory\n", entry);
        }

        private void AppendItemsToNote(string path, JToken? token, string label)
        {
            var items = token?.Type == JTokenType.Array
                ? token.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).ToList()
                : new List<string>();
            if (items.Count == 0) return;

            try
            {
                _obsidian.AppendUniqueItemsToNote(path, $"# {Path.GetFileNameWithoutExtension(path)}\n", items, label);
            }
            catch (Exception ex) { Log($"AppendItemsToNote {path}: {ex.Message}"); }
        }

        private void AppendOrCreate(string path, string header, string content)
        {
            try
            {
                var existing = _obsidian.ReadNote(path);
                if (existing == null)
                    _obsidian.WriteNote(path, header.TrimEnd() + "\n" + content);
                else
                    _obsidian.AppendToNote(path, content);
            }
            catch (Exception ex) { Log($"AppendOrCreate {path}: {ex.Message}"); }
        }

        private static JObject? ExtractJsonObject(string raw)
        {
            try { return JObject.Parse(raw.Trim()); }
            catch
            {
                var start = raw.IndexOf('{');
                var end = raw.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    try { return JObject.Parse(raw[start..(end + 1)]); }
                    catch { }
                }
            }
            return null;
        }

        private static void AppendJsonArray(StringBuilder sb, string title, JToken? token)
        {
            if (token?.Type != JTokenType.Array) return;
            var items = token.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).ToList();
            if (items.Count == 0) return;
            sb.AppendLine($"- {title}:");
            foreach (var item in items)
                sb.AppendLine($"  - {item}");
        }

        private static string TrimForVaultBuffer(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _thinkTimer.Dispose();
            _spontaneousTimer.Dispose();
            _bgLlmSemaphore.Dispose();
        }
    }
}
