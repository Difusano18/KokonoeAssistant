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
    // ------------------------------------------------------------
    // INTERNAL STATE — персистентна пам'ять Kokonoe між сесіями
    // ------------------------------------------------------------

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
        public DateTime   LastUserMessageAt      { get; set; } = DateTime.MinValue;
        public Dictionary<string,int> TopicFrequency { get; set; } = new();

        // Останні надіслані спонтанні повідомлення (для запобігання повторень)
        public List<string> LastSpontaneousMsgs  { get; set; } = new();
        public List<RecentThoughtFingerprint> RecentThoughtBuffer { get; set; } = new();

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
        public double PersonaEnergyLevel { get; set; } = 0.62;
        public double PersonaPatienceLevel { get; set; } = 0.58;
        public string PersonaTemperamentState { get; set; } = "standard_cranky";
        public DateTime LastTemperamentAt { get; set; } = DateTime.MinValue;
        public string LastTemperamentTrace { get; set; } = "";
        public DateTime LastPersonaInterjectionAt { get; set; } = DateTime.MinValue;
        public string LastPersonaInterjection { get; set; } = "";
        public int PersonaFavorDebt { get; set; }
        public string PersonaFocusTopic { get; set; } = "";
        public string LastLivingConversationMode { get; set; } = "steady";
        public string LastLivingConversationTrace { get; set; } = "";
        public DateTime LastLivingConversationAt { get; set; } = DateTime.MinValue;
        public double LivingConversationVariability { get; set; } = 0.35;
        public List<string> RecentConversationMoves { get; set; } = new();

        // Динаміка тиші — окремі cooldown рівні
        public DateTime SilenceLevel1At    { get; set; } = DateTime.MinValue; // 1h jab
        public DateTime SilenceLevel2At    { get; set; } = DateTime.MinValue; // 3h check
        public DateTime SilenceLevel3At    { get; set; } = DateTime.MinValue; // 6h observation

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
        public DateTime LastPresenceAt { get; set; } = DateTime.MinValue;
        public DateTime LastPresenceInterruptAt { get; set; } = DateTime.MinValue;
        public string LastPresenceSummary { get; set; } = "";
        public string LastPresenceSituation { get; set; } = "unknown";
        public string LastPresenceTone { get; set; } = "default";
        public List<string> PresenceTrace { get; set; } = new();
        public string EmotionalSessionMood { get; set; } = "neutral";
        public string EmotionalExitStyle { get; set; } = "unknown";
        public string EmotionalMannersState { get; set; } = "neutral";
        public double EmotionalGrudgeScore { get; set; }
        public DateTime LastEmotionalMemoryAt { get; set; } = DateTime.MinValue;
        public List<string> EmotionalMemoryTrace { get; set; } = new();
        public string LastRawContextHydration { get; set; } = "";
        public DateTime LastRawContextHydrationAt { get; set; } = DateTime.MinValue;
        public string LastVisualMemoryAnchor { get; set; } = "";
        public DateTime LastVisualMemoryAnchorAt { get; set; } = DateTime.MinValue;
        public DateTime LastInternalDayAt { get; set; } = DateTime.MinValue;
        public DateTime LastInternalDayVaultAt { get; set; } = DateTime.MinValue;
        public string LastInternalDayPhase { get; set; } = "unknown";
        public string LastInternalDaySummary { get; set; } = "";
        public string LastInternalDayFocus { get; set; } = "";
        public List<string> InternalDayTrace { get; set; } = new();
        public string LastAutonomyDecision { get; set; } = "";
        public DateTime LastAutonomyDecisionAt { get; set; } = DateTime.MinValue;
        public List<string> AutonomyDecisionLog { get; set; } = new();
        public bool LastAutonomyShouldAct { get; set; }
        public string LastAutonomySource { get; set; } = "";
        public string LastAutonomyTrigger { get; set; } = "";
        public string LastAutonomyReason { get; set; } = "";
        public string LastAutonomySilenceReason { get; set; } = "";
        public int LastAutonomyPriority { get; set; }
        public string LastTimelineSummary { get; set; } = "";
        public string LastTimelineState { get; set; } = "";
        public string LastPostReplyGuard { get; set; } = "";
        public DateTime LastPostReplyGuardAt { get; set; } = DateTime.MinValue;
        public string LastPersonaDecision { get; set; } = "";
        public DateTime LastPersonaDecisionAt { get; set; } = DateTime.MinValue;
        public List<string> PersonaDecisionLog { get; set; } = new();
        public string LastResponsePlan { get; set; } = "";
        public string LastResponsePlanTrace { get; set; } = "";
        public DateTime LastResponsePlanAt { get; set; } = DateTime.MinValue;
        public List<string> ResponsePlanLog { get; set; } = new();
        public string LastMemoryPolicyDecision { get; set; } = "";
        public DateTime LastMemoryPolicyAt { get; set; } = DateTime.MinValue;
        public List<string> MemoryPolicyLog { get; set; } = new();
        public string LastContinuitySummary { get; set; } = "";
        public DateTime LastContinuityAt { get; set; } = DateTime.MinValue;
        public DateTime LastStateRefreshAt { get; set; } = DateTime.MinValue;
        public string LastStateRefreshSummary { get; set; } = "";
        public bool LastStateRefreshChanged { get; set; }
        public string LastFoodStatus { get; set; } = "";
        public DateTime LastFoodMentionAt { get; set; } = DateTime.MinValue;
        public string LastFoodMentionText { get; set; } = "";
        public string LastSleepStatus { get; set; } = "";
        public DateTime LastSleepMentionAt { get; set; } = DateTime.MinValue;
        public string LastSleepMentionText { get; set; } = "";

        public int PendingVaultExchangeCount { get; set; }
        public List<string> PendingVaultExchangeBuffer { get; set; } = new();
        public DateTime LastAutoVaultSyncAt { get; set; } = DateTime.MinValue;
        public string LastAutoVaultSyncSummary { get; set; } = "";
        public DateTime LastVaultMaintenanceAt { get; set; } = DateTime.MinValue;
        public string LastVaultMaintenanceReason { get; set; } = "";
        public string LastVaultMaintenanceSummary { get; set; } = "";
        public string LastVaultMaintenanceError { get; set; } = "";
        public DateTime LastBackgroundVaultScanAt { get; set; } = DateTime.MinValue;
        public string LastBackgroundVaultScanSummary { get; set; } = "";
        public List<KokoPromiseRecord> PromiseLedger { get; set; } = new();
        public DateTime LastPromiseAuditAt { get; set; } = DateTime.MinValue;
        public string LastPromiseAuditSummary { get; set; } = "";
        public List<string> ActionJournal { get; set; } = new();
        public DateTime LastActionJournalAt { get; set; } = DateTime.MinValue;
        public string LastActionJournalSummary { get; set; } = "";
        public List<ShortTermIntent> ShortTermIntents { get; set; } = new();
        public DateTime LastScreenAwarenessAt { get; set; } = DateTime.MinValue;
        public DateTime LastScreenAwarenessCommentAt { get; set; } = DateTime.MinValue;
        public string LastScreenAwarenessHash { get; set; } = "";
        public string LastScreenAwarenessSummary { get; set; } = "";
        public string LastScreenAwarenessActivity { get; set; } = "";
        public string LastScreenAwarenessComment { get; set; } = "";
        public string LastScreenAwarenessWindow { get; set; } = "";
        public string LastScreenAwarenessMode { get; set; } = "";
        public string LastScreenSituationTask { get; set; } = "";
        public string LastScreenSituationProgress { get; set; } = "";
        public string LastScreenSituationBlocker { get; set; } = "";
        public string LastScreenSituationBehavior { get; set; } = "";
        public string LastScreenSituationReason { get; set; } = "";
        public DateTime LastSemanticVisionAt { get; set; } = DateTime.MinValue;
        public string LastSemanticVisionFlow { get; set; } = "";
        public string LastSemanticVisionIntent { get; set; } = "";
        public string LastSemanticVisionSummary { get; set; } = "";
        public string LastSemanticVisionOcr { get; set; } = "";
        public string LastSemanticVisionAssistHint { get; set; } = "";
        public string LastSemanticVisionResearchTopic { get; set; } = "";
        public double LastSemanticVisionConfidence { get; set; }
        public List<string> SemanticVisionTrace { get; set; } = new();
        public Dictionary<string, ScreenPatternStats> ScreenPatterns { get; set; } = new();
        public DateTime LastVisionFailureAt { get; set; } = DateTime.MinValue;
        public DateTime VisionBackoffUntil { get; set; } = DateTime.MinValue;
        public int VisionFailureCount { get; set; }
        public string LastVisionFailureSummary { get; set; } = "";
        public DateTime LastVisionSelfHealAt { get; set; } = DateTime.MinValue;
        public DateTime LastBridgeSelfHealAt { get; set; } = DateTime.MinValue;
        public DateTime LastReflectiveInsightAt { get; set; } = DateTime.MinValue;
        public string LastReflectiveInsightSummary { get; set; } = "";
        public DateTime LastMorningBriefingAt { get; set; } = DateTime.MinValue;
        public string LastMorningBriefingSummary { get; set; } = "";
        public DateTime LastDailySelfReviewAt { get; set; } = DateTime.MinValue;
        public string LastDailySelfReviewSummary { get; set; } = "";
        public string CurrentSomaticAutomationMode { get; set; } = "";
        public string LastSomaticToneDirective { get; set; } = "";
        public DateTime LastSomaticBreakPromptAt { get; set; } = DateTime.MinValue;
        public DateTime LastWatchActionAt { get; set; } = DateTime.MinValue;
        public string LastWatchAction { get; set; } = "";
        public DateTime ScreenAwarenessObserveOnlyUntil { get; set; } = DateTime.MinValue;
        public DateTime ProactiveMutedUntil { get; set; } = DateTime.MinValue;
        public string ProactiveMuteReason { get; set; } = "";
        public DateTime LastPredictiveContextAt { get; set; } = DateTime.MinValue;
        public string LastPredictiveContextMode { get; set; } = "";
        public string LastPredictiveContextSummary { get; set; } = "";
        public string LastPredictiveContextNotes { get; set; } = "";
        public int ConversationLoopCount { get; set; }
        public string LastShortUserPhraseNormalized { get; set; } = "";
        public DateTime LastShortUserPhraseAt { get; set; } = DateTime.MinValue;
        public string LastForcedTopic { get; set; } = "";
        public DateTime LastForcedTopicAt { get; set; } = DateTime.MinValue;
        public DateTime LastMemorySelfHealAt { get; set; } = DateTime.MinValue;
        public string LastMemorySelfHealSummary { get; set; } = "";
        public string LastMemorySelfHealError { get; set; } = "";
        public DateTime LastResourceGuardianAt { get; set; } = DateTime.MinValue;
        public DateTime LastResourceGuardianPromptAt { get; set; } = DateTime.MinValue;
        public string LastResourceGuardianSummary { get; set; } = "";
        public string LastWorkMode { get; set; } = "Unknown";
    }

    public class ReactiveTrigger
    {
        public string   Id      { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string   Type    { get; set; } = ""; // anxious_followup, topic_followup, bad_pattern
        public string   Context { get; set; } = "";
        public DateTime FireAt  { get; set; }
    }

    public class RecentThoughtFingerprint
    {
        public DateTime At { get; set; } = DateTime.MinValue;
        public string Category { get; set; } = "";
        public string Hash { get; set; } = "";
        public string Canonical { get; set; } = "";
        public string Preview { get; set; } = "";
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

    public class KokoPromiseRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Source { get; set; } = "chat";
        public string Summary { get; set; } = "";
        public string SourceUserText { get; set; } = "";
        public string SourceAssistantText { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime DueAt { get; set; } = DateTime.Now.AddHours(2);
        public string Status { get; set; } = "open";
        public DateTime? CompletedAt { get; set; }
        public DateTime? LastAuditAt { get; set; }
        public string Evidence { get; set; } = "";
        public string FailureReason { get; set; } = "";
        public bool UserVisible { get; set; }
    }

    public class ScreenPatternStats
    {
        public string Key { get; set; } = "";
        public string Text { get; set; } = "";
        public string Mode { get; set; } = "";
        public int Count { get; set; }
        public DateTime FirstSeenAt { get; set; } = DateTime.MinValue;
        public DateTime LastSeenAt { get; set; } = DateTime.MinValue;
        public DateTime LastWrittenAt { get; set; } = DateTime.MinValue;
    }

    // ------------------------------------------------------------
    // BRAIN ENGINE
    // ------------------------------------------------------------

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
        public readonly KokoPresenceContinuityEngine Presence;
        public readonly KokoInternalDayEngine InternalDay;
        public readonly KokoAutonomyDecisionEngine Autonomy;
        public readonly KokoSelfReviewEngine SelfReview;
        public readonly KokoScenarioSimulationService Scenarios;
        public readonly KokoConversationTimelineEngine Timeline;
        public readonly KokoPostReplyGuard PostReplyGuard;
        public readonly KokoPersonaEngine Persona;
        public readonly KokoTemperamentEngine Temperament;
        public readonly KokoLivingConversationEngine LivingConversation;
        public readonly KokoResponsePlannerEngine ResponsePlanner;
        public readonly KokoSocialEngine Social;
        public readonly KokoNeuralGovernorService NeuralGovernor;
        public readonly KokoEmotionalMemoryService EmotionalMemory;
        public readonly KokoMemoryWritePolicyEngine MemoryWritePolicy;
        public readonly KokoContinuityEngine Continuity;
        public readonly KokoStateFreshnessService StateFreshness;
        public readonly KokoProactiveContextService ProactiveContext;
        public readonly KokoScreenAwarenessService ScreenAwareness;
        public readonly KokoSemanticVisionEngine SemanticVision;

        // ── Зовнішні сервіси (опціональні) ───────────────────────────
        private EnhancedMemory?    _enhanced;
        private StateEngine?       _stateEngine;
        private GoalService?       _goalService;
        private HabitService?      _habitService;
        private ContextAnalyzer?   _contextAnalyzer;
        private readonly ActivityAnalyzer _screenActivityAnalyzer = new();

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
        private readonly System.Threading.Timer _screenAwarenessTimer;
        private readonly System.Threading.Timer _resourceGuardianTimer;
        private readonly System.Threading.Timer _stateCheckpointTimer;
        private readonly System.Threading.Timer _dailyReviewTimer;
        private bool               _disposed;
        // Семафор: тільки один фоновий LLM-запит за раз (щоб не забивати чергу)
        private readonly SemaphoreSlim _bgLlmSemaphore = new(1, 1);
        private int _thinkInFlight;
        private int _spontaneousInFlight;
        private int _screenAwarenessInFlight;
        private int _resourceGuardianInFlight;
        private int _vaultSyncInFlight;
        private int _memorySelfHealInFlight;
        private DateTime _lastVaultFreshnessCheckAt = DateTime.MinValue;
        private DateTime _lastAutonomousAgentTaskAt = DateTime.MinValue;
        private DateTime _lastSomaticVisionTriggerAt = DateTime.MinValue;
        private DateTime _lastPredictiveIdlePromptAt = DateTime.MinValue;
        private DateTime _lastReflectiveInsightAt = DateTime.MinValue;
        private bool _agentCompletionEventsHooked;
        private bool _wearableEventsHooked;
        private bool _wearableActionEventsHooked;
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
            ContextAnalyzer?  contextAnalyzer = null,
            KokoEmbeddingService? embeddings  = null)
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
            Memory    = new KokoMemoryEngine(dataDir, enhanced, embeddings);
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
            Presence = new KokoPresenceContinuityEngine();
            InternalDay = new KokoInternalDayEngine();
            Autonomy = new KokoAutonomyDecisionEngine();
            SelfReview = new KokoSelfReviewEngine();
            Scenarios = new KokoScenarioSimulationService();
            Timeline = new KokoConversationTimelineEngine();
            PostReplyGuard = new KokoPostReplyGuard();
            Persona = new KokoPersonaEngine();
            Temperament = new KokoTemperamentEngine();
            LivingConversation = new KokoLivingConversationEngine();
            ResponsePlanner = new KokoResponsePlannerEngine();
            Social = new KokoSocialEngine();
            NeuralGovernor = new KokoNeuralGovernorService(_llm);
            EmotionalMemory = new KokoEmotionalMemoryService();
            MemoryWritePolicy = new KokoMemoryWritePolicyEngine();
            Continuity = new KokoContinuityEngine(dataDir);
            StateFreshness = new KokoStateFreshnessService();
            ProactiveContext = new KokoProactiveContextService();
            ScreenAwareness = new KokoScreenAwarenessService();
            SemanticVision = new KokoSemanticVisionEngine();

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

            _screenAwarenessTimer = new System.Threading.Timer(_ => _ = GuardedScreenAwarenessAsync(), null,
                TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5));

            _resourceGuardianTimer = new System.Threading.Timer(_ => _ = GuardedResourceGuardianAsync(), null,
                TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));

            _stateCheckpointTimer = new System.Threading.Timer(_ => CheckpointStateAndHeartbeat(), null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            _dailyReviewTimer = new System.Threading.Timer(_ => _ = TryDailySelfReviewAsync("daily-review-timer"), null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));

            HookAgentCompletionEvents();
            HookWearableTelemetryEvents();
            HookWearableActionEvents();
            CheckpointStateAndHeartbeat();
        }

        // Reentrancy guards: skip tick if previous still running.
        private async Task GuardedThinkAsync()
        {
            if (Interlocked.CompareExchange(ref _thinkInFlight, 1, 0) != 0)
            {
                Log("ThinkAsync skipped — previous tick still in flight");
                return;
            }
            try
            {
                await SafeThinkAsync();
                CheckForAutonomousObjectives("think-timer");
                TryQueueAutonomousAgentCycle("think-timer");
            }
            finally { Interlocked.Exchange(ref _thinkInFlight, 0); }
        }

        private async Task GuardedSpontaneousAsync()
        {
            if (Interlocked.CompareExchange(ref _spontaneousInFlight, 1, 0) != 0)
            {
                Log("SpontaneousCheck skipped — previous tick still in flight");
                return;
            }
            try
            {
                await SafeSpontaneousCheckAsync();
                CheckForAutonomousObjectives("spontaneous-timer");
                TryQueueAutonomousAgentCycle("spontaneous-timer");
            }
            finally { Interlocked.Exchange(ref _spontaneousInFlight, 0); }
        }

        private void HookAgentCompletionEvents()
        {
            if (_agentCompletionEventsHooked) return;
            _agentCompletionEventsHooked = true;
            try
            {
                ServiceContainer.AgentTasks.TaskCompleted += (task, notice) =>
                {
                    try { ObserveAgentTaskCompletion(task, notice); }
                    catch (Exception ex) { Log($"ObserveAgentTaskCompletion: {ex.Message}"); }
                };
            }
            catch (Exception ex)
            {
                Log($"HookAgentCompletionEvents: {ex.Message}");
            }
        }

        private void HookWearableTelemetryEvents()
        {
            if (_wearableEventsHooked) return;
            _wearableEventsHooked = true;
            try
            {
                ServiceContainer.WearableTelemetry.SampleAccepted += (result, sample) =>
                {
                    if (result.Accepted)
                        _ = Task.Run(() => HandleWearableSomaticEventAsync(result, sample));
                };
                Log("Wearable telemetry events hooked");
            }
            catch (Exception ex)
            {
                Log($"HookWearableTelemetryEvents: {ex.Message}");
            }
        }

        private void HookWearableActionEvents()
        {
            if (_wearableActionEventsHooked) return;
            _wearableActionEventsHooked = true;
            try
            {
                ServiceContainer.WearableBridge.ActionReceived += action =>
                {
                    _ = Task.Run(() => HandleWatchActionAsync(action));
                };
                Log("Wearable action events hooked");
            }
            catch (Exception ex)
            {
                Log($"HookWearableActionEvents: {ex.Message}");
            }
        }

        private async Task HandleWatchActionAsync(KokoWearableBridgeService.WearableActionRequest action)
        {
            try
            {
                var name = (action.Action ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(name))
                    return;

                lock (_lock)
                {
                    _state.LastWatchActionAt = DateTime.Now;
                    _state.LastWatchAction = name;
                    _state.AutonomyDecisionLog.Add($"[{DateTime.Now:HH:mm}] watch action: {name}");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }

                ServiceContainer.Blackboard.Publish("heart-agent", "watch_action", $"{name}: {action.Payload}", 0.9, action);
                ServiceContainer.Heartbeat.Update("WATCH_ACTION", "received", name);
                Log($"Watch action received: {name}; payload={TrimForLog(action.Payload, 120)}");

                switch (name)
                {
                    case "look_screen_now":
                    case "screen_now":
                    case "look":
                        await ForceScreenAwarenessAsync("watch_action:look_screen_now", "The user explicitly asked from the watch. Give a concise useful observation.");
                        break;
                    case "note_this":
                    case "note":
                        TryWriteWatchNote(action);
                        break;
                    case "im_stressed":
                    case "stress":
                        await ForceScreenAwarenessAsync("watch_action:im_stressed", "Protective tone. Look for what might be stressing the user and suggest one low-risk next step.");
                        ServiceContainer.WearableBridge.QueueCommand("vibrate", "Kokonoe: бачу сигнал стресу. Зроби коротку паузу.");
                        break;
                    default:
                        Log($"Watch action ignored: unknown action {name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"HandleWatchActionAsync: {ex.Message}");
            }
        }

        private void TryWriteWatchNote(KokoWearableBridgeService.WearableActionRequest action)
        {
            try
            {
                var now = DateTime.Now;
                var foreground = ServiceContainer.PcControl.GetForegroundWindow();
                var text = string.IsNullOrWhiteSpace(action.Payload)
                    ? $"Watch note requested. Foreground: {foreground.ProcessName} / {foreground.Title}"
                    : action.Payload.Trim();
                var path = $"Kokonoe/Watch Notes/{now:yyyy-MM-dd HHmmss} - quick note.md";
                _obsidian.WriteNote(path, $"---\ntype: watch-note\ntags: [kokonoe, wearable, quick-note]\ncreated: {now:O}\n---\n\n# Watch note\n\n{text}\n\nForeground: `{foreground.ProcessName}` / {foreground.Title}\n");
                Memory.RecordEpisodeBlocking(text, "watch_note", 0.72f, new[] { "wearable", "watch-action" });
                ServiceContainer.Blackboard.Publish("vault-agent", "watch_note", path, 0.78);
                Log($"Watch note wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"TryWriteWatchNote: {ex.Message}");
            }
        }

        private async Task HandleWearableSomaticEventAsync(KokoWearableIngestResult result, KokoWearableSample sample)
        {
            try
            {
                var now = DateTime.Now;
                TryApplySomaticAutomation(result, sample, now);

                if (string.IsNullOrWhiteSpace(result.EventKind))
                    return;

                if (now - _lastSomaticVisionTriggerAt < TimeSpan.FromMinutes(4))
                {
                    Log($"Wearable somatic event suppressed: cooldown {result.EventKind} {result.EventReason}");
                    return;
                }

                _lastSomaticVisionTriggerAt = now;
                var protective = result.EventKind is "stress_spike" or "high_strain";
                var tone = protective
                    ? "Protective/concerned: be concrete, low-pressure, and reduce teasing."
                    : "Fatigue-aware: shorter, quieter, avoid pushing.";
                var reason = $"wearable_{result.EventKind}: {result.EventReason}";

                Log($"Triggering Scan: {reason}");
                KokoSystemLog.Write("BRAIN", $"Triggering Scan: {reason}");

                if (protective)
                {
                    try
                    {
                        ServiceContainer.WearableBridge.QueueCommand(
                            "vibrate",
                            "Kokonoe: коротка пауза. Пульс підскочив, генію.");
                    }
                    catch (Exception ex)
                    {
                        Log($"Wearable vibrate command failed: {ex.Message}");
                    }
                }

                await ForceScreenAwarenessAsync(reason, tone).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"HandleWearableSomaticEventAsync: {ex.Message}");
            }
        }

        private void TryApplySomaticAutomation(KokoWearableIngestResult result, KokoWearableSample sample, DateTime now)
        {
            try
            {
                var state = result.State;
                if (state == null)
                    return;

                ServiceContainer.Heartbeat.Update("HEART_AGENT", "sample", state.Summary);
                ServiceContainer.Blackboard.Publish("heart-agent", "sample", state.Summary, state.LiveStressScore / 100.0);

                if (state.ContextSignal == "woke_up" &&
                    (_state.LastMorningBriefingAt <= DateTime.MinValue || now - _state.LastMorningBriefingAt > TimeSpan.FromHours(8)))
                {
                    TryWriteMorningBriefing(now, state);
                }

                var activity = $"{state.Activity} {sample.Activity}".ToLowerInvariant();
                var workout = activity.Contains("running") || activity.Contains("workout") ||
                              activity.Contains("exercise") || ((state.Motion ?? 0) >= 0.70 && state.CurrentBpm >= 95);
                if (workout)
                {
                    lock (_lock)
                    {
                        _state.CurrentSomaticAutomationMode = "coach";
                        _state.ProactiveMutedUntil = now.AddMinutes(45);
                        _state.ProactiveMuteReason = "workout_nonessential_notifications_muted";
                        _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] somatic mode coach; workout detected; notifications muted");
                        if (_state.AutonomyDecisionLog.Count > 80)
                            _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                        SaveState();
                    }
                    ServiceContainer.Heartbeat.Update("SOMATIC_MODE", "coach", "workout/running detected");
                    ServiceContainer.Blackboard.Publish("heart-agent", "somatic_mode", "coach mode; nonessential proactive muted", 0.82);
                }

                var highStrain = state.LiveStressScore >= 78 ||
                    (state.CurrentBpm > 0 && state.BaselineBpm > 0 && state.CurrentBpm >= state.BaselineBpm + 24);
                if (highStrain && now - _state.LastSomaticBreakPromptAt > TimeSpan.FromMinutes(25))
                {
                    lock (_lock)
                    {
                        _state.CurrentSomaticAutomationMode = "quiet_operator";
                        _state.LastSomaticBreakPromptAt = now;
                        _state.LastSomaticToneDirective = "quiet_operator: stress rising; concrete help, low noise";
                        _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] somatic quiet_operator; stress={state.LiveStressScore}/100 bpm={state.CurrentBpm:F0}");
                        if (_state.AutonomyDecisionLog.Count > 80)
                            _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                        SaveState();
                    }

                    try
                    {
                        ServiceContainer.WearableBridge.QueueCommand(
                            "vibrate",
                            $"Kokonoe: breathe. {state.CurrentBpm:F0} bpm is not a trophy.");
                    }
                    catch (Exception ex)
                    {
                        Log($"Somatic high-strain vibrate failed: {ex.Message}");
                    }

                    ServiceContainer.Heartbeat.Update("SOMATIC_MODE", "quiet_operator", $"stress {state.LiveStressScore}/100");
                    ServiceContainer.Blackboard.Publish("heart-agent", "quiet_operator", state.Summary, 0.88);
                }
            }
            catch (Exception ex)
            {
                Log($"TryApplySomaticAutomation: {ex.Message}");
            }
        }

        private void TryWriteMorningBriefing(DateTime now, KokoWearableState state)
        {
            try
            {
                var recent = string.Join("\n", _state.Observations.TakeLast(12).Select(o => "- " + o));
                var insight = string.IsNullOrWhiteSpace(_state.LastReflectiveInsightSummary)
                    ? "No fresh reflective insight yet."
                    : _state.LastReflectiveInsightSummary;
                var path = $"Kokonoe/Morning Briefings/{now:yyyy-MM-dd} - wearable wake briefing.md";
                var body = $"""
---
type: morning-briefing
tags: [kokonoe, wearable, briefing]
created: {now:O}
---

# Morning Briefing

Wake signal: {state.ContextSignal}
Wearable: {state.Summary}

## Planned While You Were Away

{insight}

## Recent Observations

{recent}
""";
                _obsidian.WriteNote(path, body);
                lock (_lock)
                {
                    _state.LastMorningBriefingAt = now;
                    _state.LastMorningBriefingSummary = path;
                    SaveState();
                }
                ServiceContainer.Blackboard.Publish("vault-agent", "morning_briefing", path, 0.86);
                Log($"Morning briefing wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"TryWriteMorningBriefing: {ex.Message}");
            }
        }

        private void CheckpointStateAndHeartbeat()
        {
            try
            {
                lock (_lock)
                {
                    AuditPromiseLedgerLocked("heartbeat");
                    SaveState();
                }
                TrySelfHealWearableBridge("heartbeat");
                ServiceContainer.Heartbeat.Update("BRAIN", "online", $"mood={_state.CurrentMood}; thoughts={_state.PendingThoughts.Count}; autonomy={TrimForLog(_state.LastAutonomyDecision, 80)}");
                ServiceContainer.Heartbeat.Update("VISION", _screenAwarenessInFlight == 1 ? "scanning" : "idle", $"last={FormatAge(_state.LastScreenAwarenessAt)}; failures={_state.VisionFailureCount}");
                ServiceContainer.Heartbeat.Update("WATCH", ServiceContainer.WearableBridge.GetConnectionSnapshot().State, ServiceContainer.WearableTelemetry.State.Summary);
                ServiceContainer.Heartbeat.Update("BLACKBOARD", "online", $"{ServiceContainer.Blackboard.Recent(10).Count} recent events");
                KokoSystemLog.Write("BRAIN", "state checkpoint saved");
            }
            catch (Exception ex)
            {
                Log($"CheckpointStateAndHeartbeat: {ex.Message}");
            }
        }

        private void TrySelfHealWearableBridge(string reason)
        {
            try
            {
                var now = DateTime.Now;
                var bridge = ServiceContainer.WearableBridge;
                var diagnostics = bridge.Diagnostics;
                var connection = bridge.GetConnectionSnapshot();
                if (bridge.IsRunning)
                {
                    if (!string.IsNullOrWhiteSpace(diagnostics.LastError) &&
                        !connection.IsLinked &&
                        now - _state.LastBridgeSelfHealAt > TimeSpan.FromMinutes(10))
                    {
                        lock (_lock)
                        {
                            _state.LastBridgeSelfHealAt = now;
                            AppendActionJournalLocked("self-heal.bridge.observe", diagnostics.LastError, "attention", reason);
                            SaveState();
                        }
                    }
                    return;
                }

                lock (_lock)
                {
                    if (now - _state.LastBridgeSelfHealAt < TimeSpan.FromMinutes(5))
                        return;
                    _state.LastBridgeSelfHealAt = now;
                    AppendActionJournalLocked("self-heal.bridge.restart", "wearable bridge was stopped; attempting Start()", "started", reason);
                    SaveState();
                }
                bridge.Start();
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("SELF_HEAL", "wearable bridge heal failed: " + ex.Message);
            }
        }

        private async Task TryDailySelfReviewAsync(string source)
        {
            try
            {
                var now = DateTime.Now;
                if (_state.LastDailySelfReviewAt.Date == now.Date || now.Hour < 4)
                    return;

                var autonomyTail = string.Join("\n", _state.AutonomyDecisionLog.TakeLast(12).Select(x => "- " + x));
                var observations = string.Join("\n", _state.Observations.TakeLast(16).Select(x => "- " + x));
                var stabilityDelta = _state.VisionFailureCount >= 3 ? -0.03f : 0.01f;
                _state.MoodFactors["self_review_stability"] = Math.Clamp(
                    (_state.MoodFactors.TryGetValue("self_review_stability", out var old) ? old : 0f) + stabilityDelta,
                    -0.25f,
                    0.25f);

                var summary = $"vision_failures={_state.VisionFailureCount}; resource={_state.LastResourceGuardianSummary}; wearable={ServiceContainer.WearableTelemetry.State.Summary}";
                var path = $"Kokonoe/Daily Reviews/{now:yyyy-MM-dd} - autonomous self review.md";
                var body = $"""
---
type: daily-self-review
tags: [kokonoe, self-review, autonomy]
created: {now:O}
source: {source}
---

# Autonomous Self Review

Summary: {summary}

## Autonomy Decisions

{autonomyTail}

## Observations

{observations}

## Adjustment

- mood factor `self_review_stability`: {_state.MoodFactors["self_review_stability"]:F2}
- vision failure count considered: {_state.VisionFailureCount}
""";
                await Task.Run(() => _obsidian.WriteNote(path, body)).ConfigureAwait(false);
                lock (_lock)
                {
                    _state.LastDailySelfReviewAt = now;
                    _state.LastDailySelfReviewSummary = summary;
                    SaveState();
                }
                ServiceContainer.Blackboard.Publish("brain-agent", "daily_self_review", summary, 0.72);
                ServiceContainer.Heartbeat.Update("SELF_REVIEW", "written", path);
                Log($"Daily self-review wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"TryDailySelfReviewAsync: {ex.Message}");
            }
        }

        private static string FormatAge(DateTime at)
        {
            if (at <= DateTime.MinValue)
                return "never";
            var age = DateTime.Now - at;
            if (age.TotalSeconds < 90)
                return $"{age.TotalSeconds:0}s ago";
            if (age.TotalMinutes < 90)
                return $"{age.TotalMinutes:0}m ago";
            return $"{age.TotalHours:0.0}h ago";
        }

        private void ObserveAgentTaskCompletion(KokoAgentTask task, KokoAgentCompletionNotice notice)
        {
            if (task.Steps.All(s => s.Kind != KokoAgentStepKind.InsightExtraction))
                return;

            var result = task.Steps
                .Where(s => s.Kind == KokoAgentStepKind.InsightExtraction && !string.IsNullOrWhiteSpace(s.Result))
                .OrderByDescending(s => s.FinishedAt ?? DateTime.MinValue)
                .Select(s => s.Result.Trim())
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
                result = notice.Notice;

            lock (_lock)
            {
                var thought = "[agent-insight] " + TrimStateMention(result);
                if (!_state.PendingThoughts.Any(t => string.Equals(t, thought, StringComparison.OrdinalIgnoreCase)))
                    _state.PendingThoughts.Add(thought);
                if (_state.PendingThoughts.Count > 20)
                    _state.PendingThoughts.RemoveRange(0, _state.PendingThoughts.Count - 20);

                _state.LastBackgroundVaultScanAt = DateTime.Now;
                _state.LastBackgroundVaultScanSummary = TrimStateMention(result);
                _state.LastAutonomyDecision = $"agent_completed:{task.Id}";
                _state.LastAutonomyDecisionAt = DateTime.Now;
                _state.AutonomyDecisionLog.Add($"[{DateTime.Now:HH:mm}] insight task {task.Id} completed");
                if (_state.AutonomyDecisionLog.Count > 80)
                    _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                SaveState();
            }
        }

        private void CheckForAutonomousObjectives(string source)
        {
            try
            {
                var now = DateTime.Now;
                var level = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
                if (level <= 0)
                    return;

                TryPredictiveIdlePrompt(source, now, level);
                TrySelfHealWearableBridge(source, now);

                var lastUser = _chatRepo.GetMessages(20)
                    .Where(m => m.Role == "user")
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();
                if (lastUser == null)
                    return;

                var silence = now - lastUser.Timestamp;
                if (silence < TimeSpan.FromMinutes(30))
                    return;

                TryRunMemorySelfHealing("autonomous-objectives");
                TryWriteReflectiveInsight(source, now, silence);

                DateTime lastScan;
                lock (_lock) { lastScan = _state.LastBackgroundVaultScanAt; }
                if (now - lastScan < TimeSpan.FromHours(6))
                    return;

                var existing = ServiceContainer.AgentTasks.GetSnapshot().Tasks.Any(t =>
                    t.Status is KokoAgentTaskStatus.Pending or KokoAgentTaskStatus.Running &&
                    t.Objective.Contains("Background Vault Scanner", StringComparison.OrdinalIgnoreCase));
                if (existing)
                    return;

                var objective = "Background Vault Scanner: Проаналізуй останні 10 змінених нотаток в Obsidian, запусти vault_cluster.py для тематичного групування, знайди цікаві факти, суперечності або задачі. Запиши результат як insight, без очікування команди користувача.";
                var task = ServiceContainer.AgentTasks.AddTask(objective, priority: 3);
                ServiceContainer.AgentTasks.Start();

                lock (_lock)
                {
                    _state.LastBackgroundVaultScanAt = now;
                    _state.LastAutonomyDecision = $"queued_background_vault_scan:{task.Id}";
                    _state.LastAutonomyDecisionAt = now;
                    _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] {source} queued background vault scan {task.Id}");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                Log($"CheckForAutonomousObjectives: {ex.Message}");
            }
        }

        private void TryRunMemorySelfHealing(string source)
        {
            var settings = AppSettings.Load();
            if (Math.Clamp(settings.ProactiveAutonomyLevel, 0, 3) < 2)
                return;

            lock (_lock)
            {
                if (_state.LastMemorySelfHealAt > DateTime.MinValue &&
                    DateTime.Now - _state.LastMemorySelfHealAt < TimeSpan.FromHours(12))
                    return;
            }

            if (Interlocked.CompareExchange(ref _memorySelfHealInFlight, 1, 0) != 0)
                return;

            _ = Task.Run(() =>
            {
                try
                {
                    var result = _obsidian.SelfHealMemoryConflicts();
                    lock (_lock)
                    {
                        _state.LastMemorySelfHealAt = DateTime.Now;
                        _state.LastMemorySelfHealSummary = result;
                        _state.LastMemorySelfHealError = "";
                        _state.AutonomyDecisionLog.Add($"[{DateTime.Now:HH:mm}] {source} memory self-heal: {TrimStateMention(result)}");
                        if (_state.AutonomyDecisionLog.Count > 80)
                            _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                        SaveState();
                    }
                    Log("MemorySelfHeal: " + result);
                }
                catch (Exception ex)
                {
                    lock (_lock)
                    {
                        _state.LastMemorySelfHealAt = DateTime.Now;
                        _state.LastMemorySelfHealError = ex.Message;
                        SaveState();
                    }
                    Log($"MemorySelfHeal failed: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _memorySelfHealInFlight, 0);
                }
            });
        }

        private void TryPredictiveIdlePrompt(string source, DateTime now, int autonomyLevel)
        {
            if (autonomyLevel < 2)
                return;
            if (now.Hour != 9 || now.Minute < 10 || now.Minute > 45)
                return;
            if (now - _lastPredictiveIdlePromptAt < TimeSpan.FromHours(6))
                return;

            TimeSpan idle;
            try { idle = ServiceContainer.PcControl.GetSystemInfo().IdleTime; }
            catch (Exception ex)
            {
                Log($"PredictiveIdlePrompt skipped: idle read failed: {ex.Message}");
                return;
            }

            if (idle < TimeSpan.FromMinutes(10))
                return;

            string forecast;
            try { forecast = ServiceContainer.Predictor.GetForecastContext(); }
            catch (Exception ex) { forecast = $"predictor unavailable: {ex.Message}"; }

            _lastPredictiveIdlePromptAt = now;
            Log($"PredictiveIdlePrompt firing: source={source}; idle={idle.TotalMinutes:0.0}m; forecast={TrimStateMention(forecast)}");
            _ = Task.Run(async () =>
            {
                var message = "09:10, а комп'ютер досі в ступорі. Ти живий, чи мені вмикати ранковий запуск мозку примусово?";
                await SendTgAndLog(message, "predictive_observation").ConfigureAwait(false);
            });
        }

        private void TryWriteReflectiveInsight(string source, DateTime now, TimeSpan silence)
        {
            if (silence < TimeSpan.FromHours(4))
                return;

            DateTime lastInsight;
            lock (_lock) lastInsight = _state.LastReflectiveInsightAt;
            if (lastInsight > DateTime.MinValue && now - lastInsight < TimeSpan.FromHours(4))
                return;
            if (now - _lastReflectiveInsightAt < TimeSpan.FromMinutes(30))
                return;
            _lastReflectiveInsightAt = now;

            try
            {
                List<string> observations;
                List<string> thoughts;
                List<string> initiatives;
                string screenSummary;
                lock (_lock)
                {
                    observations = _state.Observations.TakeLast(12).ToList();
                    thoughts = _state.PendingThoughts.TakeLast(8).ToList();
                    initiatives = _state.InitiativeReasonLog.TakeLast(8).ToList();
                    screenSummary = $"{_state.LastScreenAwarenessMode}: {_state.LastScreenAwarenessSummary}; {_state.LastScreenSituationProgress}; {_state.LastScreenSituationBehavior}";
                }

                var insight = BuildReflectiveInsightText(now, source, silence, observations, thoughts, initiatives, screenSummary);
                var path = $"Kokonoe/Reflective Insights/{now:yyyy-MM-dd HHmm} - autonomous insight.md";
                _obsidian.WriteNote(path, insight);
                Memory.RecordEpisodeBlocking(
                    $"Autonomous reflective insight: {TrimStateMention(string.Join("; ", observations.TakeLast(3)))}",
                    "reflective",
                    0.62f,
                    new[] { "autonomy", "reflection", "screen", "patterns" });

                lock (_lock)
                {
                    _state.LastReflectiveInsightAt = now;
                    _state.LastReflectiveInsightSummary = TrimStateMention(insight);
                    _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] {source} wrote reflective insight after {silence.TotalHours:0.0}h inactivity");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }
                Log($"ReflectiveInsight wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"ReflectiveInsight failed: {ex.Message}");
            }
        }

        private static string BuildReflectiveInsightText(
            DateTime now,
            string source,
            TimeSpan silence,
            IReadOnlyList<string> observations,
            IReadOnlyList<string> thoughts,
            IReadOnlyList<string> initiatives,
            string screenSummary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"created: {now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("tags: [kokonoe, reflective-insight, autonomous]");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Autonomous Reflective Insight");
            sb.AppendLine();
            sb.AppendLine($"Source: {source}");
            sb.AppendLine($"User inactivity window: {silence.TotalHours:0.0}h");
            sb.AppendLine($"Last screen: {screenSummary}");
            sb.AppendLine();
            AppendInsightList(sb, "Recent observations", observations);
            AppendInsightList(sb, "Pending thoughts", thoughts);
            AppendInsightList(sb, "Initiative trace", initiatives);
            sb.AppendLine("## Pattern");
            sb.AppendLine("- If the same screen state, silence, or task residue repeats, prefer one concrete next action over generic checking.");
            sb.AppendLine("- Treat wearable/screen/file signals as context, not certainty. Useful suspicion, not prophecy.");
            return sb.ToString().Trim() + Environment.NewLine;
        }

        private static void AppendInsightList(StringBuilder sb, string title, IReadOnlyList<string> items)
        {
            sb.AppendLine($"## {title}");
            if (items.Count == 0)
            {
                sb.AppendLine("- none");
                sb.AppendLine();
                return;
            }
            foreach (var item in items)
                sb.AppendLine("- " + TrimStateMention(item));
            sb.AppendLine();
        }

        private void TrySelfHealWearableBridge(string source, DateTime now)
        {
            try
            {
                var bridge = ServiceContainer.WearableBridge;
                var diagnostics = bridge.Diagnostics;
                var shouldRestart = !diagnostics.IsRunning || !string.IsNullOrWhiteSpace(diagnostics.LastError);
                if (!shouldRestart)
                    return;

                DateTime lastHeal;
                lock (_lock) lastHeal = _state.LastBridgeSelfHealAt;
                if (lastHeal > DateTime.MinValue && now - lastHeal < TimeSpan.FromMinutes(5))
                    return;

                Log($"Bridge self-heal: source={source}; running={diagnostics.IsRunning}; error={diagnostics.LastError}; port={diagnostics.Port}");
                ServiceContainer.ReloadWearableBridge();
                lock (_lock)
                {
                    _state.LastBridgeSelfHealAt = now;
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                Log($"Bridge self-heal failed: {ex.Message}");
            }
        }

        private void TryQueueAutonomousAgentCycle(string source)
        {
            try
            {
                var now = DateTime.Now;
                if (now - _lastAutonomousAgentTaskAt < TimeSpan.FromMinutes(20))
                    return;

                string? objective = null;
                lock (_lock)
                {
                    objective = _state.PendingThoughts
                        .LastOrDefault(t => !string.IsNullOrWhiteSpace(t) && t.Length > 18);
                    if (string.IsNullOrWhiteSpace(objective) && _state.LastAutonomyShouldAct && !string.IsNullOrWhiteSpace(_state.LastAutonomyReason))
                        objective = _state.LastAutonomyReason;
                }

                if (string.IsNullOrWhiteSpace(objective))
                    return;

                var task = ServiceContainer.AgentTasks.AddTask($"APEO/{source}: {objective}", priority: 3);
                ServiceContainer.AgentTasks.Start();
                _lastAutonomousAgentTaskAt = now;

                lock (_lock)
                {
                    _state.LastAutonomyDecision = $"queued_agent_task:{task.Id}";
                    _state.LastAutonomyDecisionAt = now;
                    _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] {source} queued agent task {task.Id}");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                Log($"TryQueueAutonomousAgentCycle: {ex.Message}");
            }
        }

        private async Task GuardedScreenAwarenessAsync()
        {
            if (Interlocked.CompareExchange(ref _screenAwarenessInFlight, 1, 0) != 0)
            {
                Log("ScreenAwareness skipped — previous tick still in flight");
                return;
            }
            try { await SafeScreenAwarenessAsync(); }
            finally { Interlocked.Exchange(ref _screenAwarenessInFlight, 0); }
        }

        public async Task ForceScreenAwarenessAsync(string reason, string toneDirective = "")
        {
            if (Interlocked.CompareExchange(ref _screenAwarenessInFlight, 1, 0) != 0)
            {
                Log($"Forced ScreenAwareness skipped: previous tick still in flight; reason={reason}");
                return;
            }

            try { await SafeScreenAwarenessAsync(reason, toneDirective); }
            finally { Interlocked.Exchange(ref _screenAwarenessInFlight, 0); }
        }

        private async Task GuardedResourceGuardianAsync()
        {
            if (Interlocked.CompareExchange(ref _resourceGuardianInFlight, 1, 0) != 0)
            {
                Log("ResourceGuardian skipped - previous tick still in flight");
                return;
            }

            try { await SafeResourceGuardianAsync(); }
            finally { Interlocked.Exchange(ref _resourceGuardianInFlight, 0); }
        }

        private async Task SafeResourceGuardianAsync()
        {
            if (_disposed) return;

            var now = DateTime.Now;
            DateTime lastAt;
            DateTime lastPromptAt;
            string modeSeed;
            lock (_lock)
            {
                lastAt = _state.LastResourceGuardianAt;
                lastPromptAt = _state.LastResourceGuardianPromptAt;
                modeSeed = string.IsNullOrWhiteSpace(_state.LastScreenAwarenessMode)
                    ? _state.LastScreenAwarenessWindow
                    : _state.LastScreenAwarenessMode;
            }

            if (lastAt > DateTime.MinValue && now - lastAt < TimeSpan.FromMinutes(5))
                return;

            SystemInfo info;
            try
            {
                info = await Task.Run(() => ServiceContainer.PcControl.GetSystemInfo()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"ResourceGuardian system info failed: {ex.Message}");
                return;
            }

            var decision = KokoResourceGuardianService.Evaluate(info, modeSeed, now, lastPromptAt);
            lock (_lock)
            {
                _state.LastResourceGuardianAt = now;
                _state.LastWorkMode = decision.WorkMode;
                _state.LastResourceGuardianSummary =
                    $"{decision.WorkMode}; cpu {info.CpuPercent:F1}%; ram {info.RamUsedGb:F1}/{info.RamTotalGb:F1} GB; {decision.Reason}";
                _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] resource guardian: {_state.LastResourceGuardianSummary}");
                if (_state.AutonomyDecisionLog.Count > 80)
                    _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                SaveState();
            }

            if (!decision.ShouldPrompt || string.IsNullOrWhiteSpace(decision.Message))
                return;

            var sent = await SendTgAndLog(decision.Message, "resource_guardian");
            lock (_lock)
            {
                _state.LastResourceGuardianPromptAt = now;
                if (sent)
                    _state.LastSpontaneousAt = now;
                SaveState();
            }
            if (sent)
            {
                try { OnNewMessage?.Invoke("assistant", decision.Message); } catch { }
            }
        }

        public void SetTelegram(TelegramBotClient bot, long chatId)
        {
            _tgBot         = bot;
            _tgChatId      = chatId;
            _tgInitialized = true;
        }

        // ---- TELEGRAM SELF-INIT ----
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
            if (ShouldSuppressAutomatedTelegram(category, out var suppressionReason))
            {
                Log($"TG send suppressed ({category}): {suppressionReason}");
                return false;
            }
            if (ShouldSuppressRecentThought(message, category, out var duplicateReason))
            {
                Log($"TG send suppressed duplicate ({category}): {duplicateReason}");
                return false;
            }

            try
            {
                await _tgBot!.SendMessage(_tgChatId, message);
                RecordRecentThought(message, category, DateTime.Now);
                // Log to vault archive
                try { ServiceContainer.ChatLogger.LogOutgoing("tg", message, category); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                LogTelegramDeliveryFailure($"send ({category}): {ex.Message}");
                return false;
            }
        }

        private bool ShouldSuppressAutomatedTelegram(string category)
            => ShouldSuppressAutomatedTelegram(category, out _);

        private bool ShouldSuppressAutomatedTelegram(string category, out string reason)
        {
            reason = "";
            if (category.Contains("crisis", StringComparison.OrdinalIgnoreCase))
                return false;

            if (_state.ProactiveMutedUntil > DateTime.Now)
            {
                reason = $"proactive muted until {_state.ProactiveMutedUntil:HH:mm}";
                return true;
            }

            if (HasActiveSleepIntent(DateTime.Now) && IsSleepInterruptCategory(category))
            {
                reason = "active sleep intent; suppress automated Telegram until explicit wake/safe biometric wake";
                return true;
            }

            if (IsLowActivityState(DateTime.Now) &&
                DateTime.Now - _state.LastSpontaneousAt < TimeSpan.FromMinutes(60) &&
                IsSpontaneousLikeCategory(category))
            {
                reason = "low activity cooldown 60m";
                return true;
            }

            var lastUserAt = _state.LastUserMessageAt;
            if (lastUserAt <= DateTime.MinValue)
                return false;

            var cooldownMinutes = IsFastProactiveCategory(category) ? 5 : 10;
            var elapsed = DateTime.Now - lastUserAt;
            if (elapsed >= TimeSpan.FromMinutes(cooldownMinutes))
                return false;

            reason = $"recent user message cooldown {elapsed.TotalMinutes:0.0}m < {cooldownMinutes}m";
            return true;
        }

        private bool ShouldSuppressRecentThought(string message, string category, out string reason)
        {
            reason = "";
            if (category.Contains("crisis", StringComparison.OrdinalIgnoreCase))
                return false;

            var now = DateTime.Now;
            _state.RecentThoughtBuffer.RemoveAll(t => now - t.At > TimeSpan.FromHours(6));
            var canonical = NormalizeThoughtForBuffer(message);
            if (canonical.Length < 8)
                return false;

            var isIdle = LooksLikeIdleOrStuckThought(canonical, category);
            var similar = _state.RecentThoughtBuffer
                .Where(t => now - t.At <= TimeSpan.FromHours(6))
                .Select(t => new { Thought = t, Score = ThoughtSimilarity(canonical, t.Canonical) })
                .Where(x => x.Score >= 0.72 || x.Thought.Hash == canonical)
                .OrderByDescending(x => x.Score)
                .ToList();

            if (similar.Any())
            {
                var hit = similar[0].Thought;
                reason = $"similar thought already sent at {hit.At:HH:mm}; score={similar[0].Score:F2}; preview={hit.Preview}";
                return true;
            }

            if (isIdle)
            {
                var idleCount = _state.RecentThoughtBuffer.Count(t =>
                    now - t.At <= TimeSpan.FromHours(6) &&
                    LooksLikeIdleOrStuckThought(t.Canonical, t.Category));
                if (idleCount >= 2)
                {
                    reason = $"idle/stuck observation already sent {idleCount} times in 6h";
                    return true;
                }
            }

            if (WouldRepeatPresenceTrace(canonical, now))
            {
                reason = "presence trace already contains this idle/away/sleep observation";
                return true;
            }

            return false;
        }

        private void RecordRecentThought(string message, string category, DateTime now)
        {
            var canonical = NormalizeThoughtForBuffer(message);
            if (canonical.Length < 8)
                return;

            _state.RecentThoughtBuffer.RemoveAll(t => now - t.At > TimeSpan.FromHours(6));
            _state.RecentThoughtBuffer.Add(new RecentThoughtFingerprint
            {
                At = now,
                Category = category,
                Hash = canonical,
                Canonical = canonical,
                Preview = TrimStateMention(message)
            });
            if (_state.RecentThoughtBuffer.Count > 80)
                _state.RecentThoughtBuffer.RemoveRange(0, _state.RecentThoughtBuffer.Count - 80);
        }

        private static string NormalizeThoughtForBuffer(string text)
        {
            var chars = (text ?? "")
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ')
                .ToArray();
            var normalized = new string(chars);
            while (normalized.Contains("  ", StringComparison.Ordinal))
                normalized = normalized.Replace("  ", " ");
            return normalized.Trim();
        }

        private static double ThoughtSimilarity(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return 0;
            if (left == right)
                return 1;

            var a = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var b = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (a.Count == 0 || b.Count == 0)
                return 0;
            var intersection = a.Count(t => b.Contains(t));
            var union = a.Count + b.Count - intersection;
            return union <= 0 ? 0 : intersection / (double)union;
        }

        private static bool LooksLikeIdleOrStuckThought(string canonical, string category)
        {
            var text = $"{canonical} {category}".ToLowerInvariant();
            return ContainsAny(text,
                "idle", "stuck", "silence", "away", "ghost", "zombie", "завис", "пропав", "мовч", "тиша", "idle_staring");
        }

        private bool WouldRepeatPresenceTrace(string canonical, DateTime now)
        {
            if (!LooksLikeIdleOrStuckThought(canonical, ""))
                return false;

            return _state.PresenceTrace
                .TakeLast(8)
                .Any(t => ContainsAny(t.ToLowerInvariant(), "idle", "away", "ghost", "sleep", "stuck", "завис", "відійшов"));
        }

        private bool HasActiveSleepIntent(DateTime now)
            => _state.ShortTermIntents.Any(i => !i.ResolvedAt.HasValue && i.Kind == "sleep" && now - i.CreatedAt < TimeSpan.FromHours(24));

        private bool IsLowActivityState(DateTime now)
        {
            if (HasActiveSleepIntent(now))
                return true;
            if (_state.LastPresenceSituation is "away" or "ghost_mode" or "watch_sleeping" or "sleep_locked" or "sleeping")
                return true;

            try
            {
                if (ServiceContainer.PcControl.GetSystemInfo().IdleTime >= TimeSpan.FromMinutes(30))
                    return true;
            }
            catch { }

            try
            {
                var wearable = ServiceContainer.WearableTelemetry?.State;
                if (wearable != null && wearable.IsFresh(now.ToUniversalTime()) &&
                    wearable.OnWrist &&
                    ((wearable.Motion ?? 0) <= 0.01 || wearable.SleepState is "probably_asleep" or "drowsy_or_resting" or "quiet_night"))
                    return true;
            }
            catch { }

            return false;
        }

        private static bool IsSpontaneousLikeCategory(string category)
            => category.Contains("spontaneous", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("jab", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("observation", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("reactive", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("night", StringComparison.OrdinalIgnoreCase);

        private static bool IsSleepInterruptCategory(string category)
            => IsSpontaneousLikeCategory(category) ||
               category.Contains("briefing", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("digest", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("analytics", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("resource_guardian", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("predictive", StringComparison.OrdinalIgnoreCase);

        private static bool IsFastProactiveCategory(string category)
            => category.Contains("jab", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("observation", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("observe", StringComparison.OrdinalIgnoreCase);

        // ---- STATE PERSISTENCE ----

        private KokoInternalState LoadState()
        {
            try
            {
                if (File.Exists(_statePath))
                {
                    var state = JsonConvert.DeserializeObject<KokoInternalState>(
                        File.ReadAllText(_statePath, Encoding.UTF8)) ?? new();
                    if (RepairMojibakeObject(state))
                    {
                        try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(state, Formatting.Indented), Encoding.UTF8); }
                        catch { }
                    }
                    return state;
                }
            }
            catch { }
            return new KokoInternalState();
        }

        private void SaveState()
        {
            try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented), Encoding.UTF8); }
            catch { }
        }

        // ---- CONTEXT BUILDER ----

        private static bool RepairMojibakeObject(object? value, HashSet<object>? seen = null)
        {
            if (value == null || value is string) return false;
            var type = value.GetType();
            if (type.IsValueType || type.IsEnum) return false;

            seen ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (!seen.Add(value)) return false;

            var changed = false;
            if (value is System.Collections.IList list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is string s)
                    {
                        var fixedText = RepairMojibakeString(s);
                        if (!string.Equals(s, fixedText, StringComparison.Ordinal))
                        {
                            list[i] = fixedText;
                            changed = true;
                        }
                    }
                    else
                    {
                        changed |= RepairMojibakeObject(list[i], seen);
                    }
                }
                return changed;
            }

            if (value is System.Collections.IDictionary dict)
            {
                foreach (var key in dict.Keys.Cast<object>().ToList())
                {
                    if (dict[key] is string s)
                    {
                        var fixedText = RepairMojibakeString(s);
                        if (!string.Equals(s, fixedText, StringComparison.Ordinal))
                        {
                            dict[key] = fixedText;
                            changed = true;
                        }
                    }
                    else
                    {
                        changed |= RepairMojibakeObject(dict[key], seen);
                    }
                }
                return changed;
            }

            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                object? current;
                try { current = prop.GetValue(value); }
                catch { continue; }

                if (prop.PropertyType == typeof(string) && prop.CanWrite && current is string s)
                {
                    var fixedText = RepairMojibakeString(s);
                    if (!string.Equals(s, fixedText, StringComparison.Ordinal))
                    {
                        prop.SetValue(value, fixedText);
                        changed = true;
                    }
                }
                else if (current != null && !prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                {
                    changed |= RepairMojibakeObject(current, seen);
                }
            }

            return changed;
        }

        private static string RepairMojibakeString(string text)
        {
            if (!LooksMojibake(text)) return text;

            var best = text;
            var bestScore = MojibakeScore(text);
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var cp1251 = Encoding.GetEncoding(1251);
                for (var i = 0; i < 2; i++)
                {
                    var candidate = Encoding.UTF8.GetString(cp1251.GetBytes(best));
                    var score = MojibakeScore(candidate);
                    if (score >= bestScore) break;
                    best = candidate;
                    bestScore = score;
                    if (!LooksMojibake(best)) break;
                }
            }
            catch { }

            return best;
        }

        private static bool LooksMojibake(string text) => MojibakeScore(text) >= 2;

        private static int MojibakeScore(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var markers = new[]
            {
                "\u0420\u040E", "\u0420\u045A", "\u0420\u040F", "\u0420\u00A0\u0412\u00B0",
                "\u0420\u00A0\u0421\u2018", "\u0420\u00A0\u0412\u00B5", "\u0421\u040A",
                "\u0421\u2013", "\u0421\u2014", "\u0420\u040E\u0421\u201C",
                "\u0420\u00A0\u0420\u2020\u0420\u00A0\u0432\u201A\u0459",
                "\u0420\u2019\u0412\u00AB", "\u0412\u00BB", "\u0420\u0406\u0432\u20AC\u201C",
                "\u0420\u0406\u0432\u20AC\u2020"
            };
            return markers.Sum(m => CountOccurrences(text, m));
        }

        private static int CountOccurrences(string text, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        private async Task<string> BuildContextAsync(string? query = null)
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

                var continuationBlock = BuildNarrativeContinuationBlock(query);
                if (!string.IsNullOrWhiteSpace(continuationBlock))
                    sb.AppendLine(continuationBlock);

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
            var foodSleep = BuildFoodSleepContinuityBlock(now);
            if (!string.IsNullOrWhiteSpace(foodSleep))
                sb.AppendLine(foodSleep);
            try
            {
                var autonomyLevel = AppSettings.Load().ProactiveAutonomyLevel;
                var msgs = _chatRepo.GetMessages(20).OrderBy(m => m.Timestamp).ToList();
                
                // Refresh state freshness before evaluating presence
                StateFreshness.Refresh(_state, msgs, now);
                
                var presence = Presence.Evaluate(_state, msgs, now, autonomyLevel);
                var somatic = Somatic.Evaluate(ServiceContainer.Heart, Emotion, _health, now);
                var dayFrame = InternalDay.Evaluate(_state, presence, somatic, now, autonomyLevel);
                
                sb.AppendLine("\n--- ПРИСУТНІСТЬ І ВНУТРІШНІЙ ДЕНЬ ---");
                sb.AppendLine(presence.ExtraContext);
                sb.AppendLine(dayFrame.PromptBlock);
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
                if (AppSettings.Load().WearBridgeIncludePromptContext)
                    sb.AppendLine(ServiceContainer.WearableTelemetry.BuildPromptBlock(now.ToUniversalTime()));
            }
            catch { }

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
                var cogCtx = await Cognition.BuildCognitionContextAsync();
                if (!string.IsNullOrEmpty(cogCtx)) sb.AppendLine("\n" + cogCtx);
            }
            catch { }

            // ── Пам'ять ───────────────────────────────────────────────
            try
            {
                var memCtx = await Memory.BuildMemoryContextAsync(10, 3, query);
                if (!string.IsNullOrEmpty(memCtx)) sb.AppendLine("\n" + memCtx);
            }
            catch { }

            // ── Паттерни ──────────────────────────────────────────────
            try
            {
                var patCtx = Patterns.BuildPatternContext(4);
                if (!string.IsNullOrEmpty(patCtx)) sb.AppendLine("\n" + patCtx);
                sb.AppendLine("\n" + Patterns.BuildRhythmContext(now));
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
                if (_state.LastPredictiveContextAt > DateTime.MinValue &&
                    DateTime.Now - _state.LastPredictiveContextAt < TimeSpan.FromMinutes(60) &&
                    !string.IsNullOrWhiteSpace(_state.LastPredictiveContextSummary))
                {
                    sb.AppendLine("\n--- PREDICTIVE CONTEXT WARM-UP ---");
                    sb.AppendLine($"Mode: {_state.LastPredictiveContextMode}");
                    sb.AppendLine($"Summary: {_state.LastPredictiveContextSummary}");
                    if (!string.IsNullOrWhiteSpace(_state.LastPredictiveContextNotes))
                        sb.AppendLine($"Notes: {_state.LastPredictiveContextNotes}");
                }
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

        // ---- PERSONALITY INJECTION ----

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
                var living = LivingConversation.BuildPromptBlock(_state, Emotion, DateTime.Now);
                if (!string.IsNullOrWhiteSpace(living))
                    sb.AppendLine(living);
            }
            catch { }
            try { sb.AppendLine(Emotion.BuildEmotionalContextBlock(BuildNarrativeThreadSummary(DateTime.Now))); } catch { }

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
                sb.AppendLine(Relationship.BuildBehaviorDirectiveBlock());
                var somatic = GetSomaticSnapshot();
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
                sb.AppendLine(SelfRegulator.BuildPromptBlock(GetSelfRegulationFrame(somatic)));
                sb.AppendLine(BuildSocialContextBlock(null, DateTime.Now));
                sb.AppendLine(Temperament.BuildPromptBlock(_state, DateTime.Now));
                var recentForContext = _chatRepo.GetMessages(30);
                sb.AppendLine(EmotionalMemory.BuildPromptBlock(_state, recentForContext, DateTime.Now));
                sb.AppendLine(EmotionalMemory.BuildRawHydrationBlock(_state, recentForContext, DateTime.Now));
                sb.AppendLine(Initiative.BuildDebugBlock(_state));
                sb.AppendLine(Presence.BuildDebugBlock(_state));
                sb.AppendLine(InternalDay.BuildDebugBlock(_state));
                sb.AppendLine(Autonomy.BuildDebugBlock(_state));
                sb.AppendLine(BuildSemanticVisionPromptBlock(DateTime.Now));
                sb.AppendLine(ResponsePlanner.BuildDebugBlock(_state));
                sb.AppendLine(MemoryWritePolicy.BuildDebugBlock(_state));
                var foodSleep = BuildFoodSleepContinuityBlock(DateTime.Now);
                if (!string.IsNullOrWhiteSpace(foodSleep))
                    sb.AppendLine(foodSleep);
            }
            catch { }

            try
            {
                var continuity = Continuity.BuildPromptBlock();
                if (!string.IsNullOrWhiteSpace(continuity))
                    sb.AppendLine(continuity);
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(_state.LastResponsePlan) &&
                (DateTime.Now - _state.LastResponsePlanAt).TotalMinutes < 30)
            {
                sb.AppendLine(_state.LastResponsePlan);
            }

            if (!string.IsNullOrWhiteSpace(_state.LastPersonaDecision) &&
                (DateTime.Now - _state.LastPersonaDecisionAt).TotalMinutes < 30)
            {
                sb.AppendLine(_state.LastPersonaDecision);
            }

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

        private string BuildFoodSleepContinuityBlock(DateTime now)
        {
            var lines = new List<string>();

            if (_state.LastFoodMentionAt > DateTime.MinValue &&
                now - _state.LastFoodMentionAt < TimeSpan.FromHours(24))
            {
                var status = _state.LastFoodStatus switch
                {
                    "ate" => "останній сигнал: він їв",
                    "not_eaten" => "останній сигнал: він ще не їв",
                    "hungry" => "останній сигнал: він голодний/хоче їсти",
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(status))
                    lines.Add($"Їжа: {status} о {_state.LastFoodMentionAt:HH:mm}. Репліка: \"{_state.LastFoodMentionText}\".");
            }

            if (_state.LastSleepMentionAt > DateTime.MinValue &&
                now - _state.LastSleepMentionAt < TimeSpan.FromHours(36))
            {
                var status = _state.LastSleepStatus switch
                {
                    "slept" => "останній сигнал: він спав/заснув",
                    "going_to_sleep" => "останній сигнал: він збирався спати",
                    "woke_or_returned" => "останній сигнал: він прокинувся/повернувся",
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(status))
                    lines.Add($"Сон: {status} о {_state.LastSleepMentionAt:HH:mm}. Репліка: \"{_state.LastSleepMentionText}\".");
            }

            if (lines.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("\n--- СВІЖИЙ СТАН ЇЖІ/СНУ ---");
            foreach (var line in lines)
                sb.AppendLine(line);
            sb.AppendLine("Правило: не супереч останньому сигналу. Якщо він сказав, що їв — не кажи, що він нічого не їв. Якщо сказав, що заснув/спав — не заперечуй сон і не називай це гібернацією чи комою.");
            return sb.ToString();
        }

        // ---- THINK LOOP (inner monologue) ----

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
            var context = await BuildContextAsync();

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

            var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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
                    try { Memory.RecordEpisodeBlocking(obs, _state.LastUserEmotionalTone, _state.MoodScore); } catch { }
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
                var context = await BuildContextAsync("його плани та наміри");
                var prompt = $@"{context}
Ти — Kokonoe Mercury.

Перечитай контекст. Є щось що він згадував — план, намір, обіцянку собі, незакінчену справу — що так і залишилось висіти в повітрі?

Якщо є щось конкретне — напиши йому ОДНЕ коротке повідомлення в Telegram своїми словами. Не як нагадування-скрипт. Як ти б сказала це сама — можливо іронічно, можливо просто, але щиро. Тільки українська.

Якщо нічого конкретного немає — відповідь рівно одне слово: null";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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
                var today = DateTime.Today;
                var context = await BuildContextAsync("підсумок дня та важливі події");
                
                var prompt = $@"{context}
Ти — Kokonoe Mercury.

Сьогодні {today:dd MMMM yyyy} закінчується. Переглянь контекст вище.
Напиши йому в Telegram коротко — 3-4 речення — як ти бачиш його сьогоднішній день. Не звіт і не список. Твоє враження — іронія, турбота, спостереження, що завгодно що відповідає твоєму характеру і тому що реально відбулось. Тільки українська. Тільки текст, нічого зайвого.";

                var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

        // ------------------------------------------------------------
        // ДИНАМІЧНИЙ НАСТРІЙ
        // ------------------------------------------------------------

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

        // ------------------------------------------------------------
        // АНАЛІЗ ЕМОЦІЙ + РЕАКТИВНІ ТРИГЕРИ
        // ------------------------------------------------------------

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

                var tone = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

        private async Task<bool> CheckReactiveTriggersAsync()
        {
            var now  = DateTime.Now;
            if (ShouldSuppressProactiveForSleep(now))
                return false;

            var fire = _state.PendingTriggers
                .Where(t => t.FireAt <= now)
                .OrderBy(t => t.FireAt)
                .FirstOrDefault();

            if (fire == null) return false;

            _state.PendingTriggers.Remove(fire);
            SaveState();

            if (!EnsureTelegram()) return false;

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

                "intent_followup" => $@"Ти — Kokonoe. Це автоматичний follow-up за короткостроковим наміром користувача.
Контекст: {fire.Context}
{moodHint}

Напиши йому сама, без очікування нового повідомлення. 1 коротке речення.
Це має звучати природно: питання, підколка або сухий коментар.
Не пояснюй, що це нагадування або автоматична перевірка.
Тільки українська. Тільки текст.",

                _ => null
            };

            if (prompt == null) return false;

            var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
            if (string.IsNullOrWhiteSpace(msg)) return false;
            msg = msg.Trim().Trim('"');

            try
            {
                if (!await SendTgAndLog(msg, "reactive")) return false;
                _state.LastSpontaneousAt = DateTime.Now;
                if (fire.Type == "intent_followup")
                {
                    foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue && i.FollowUpAt <= DateTime.Now.AddMinutes(1)))
                    {
                        if (intent.Kind == "sleep")
                        {
                            LogSleepIntent("kept active after blocked/legacy follow-up path");
                            continue;
                        }

                        ResolveIntent(intent, DateTime.Now, "follow-up sent once; do not repeat stale intent");
                    }
                    _state.PendingTriggers.RemoveAll(t => t.Type == "intent_followup");
                    _state.SilenceLevel1At = DateTime.Now;
                    _state.SilenceLevel2At = DateTime.Now;
                }
                var _h3 = OnNewMessage; _h3?.Invoke("assistant", msg);
                try { _chatRepo.InsertMessage(new ChatRepository.ChatMessage { Content = msg, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now }); } catch { }
                SaveState();
                Log($"Reactive trigger fired: {fire.Type}");
                return true;
            }
            catch (Exception ex) { LogError($"TG reactive: {ex.Message}"); }
            return false;
        }

        // ==============================================================
        // ????????? ??'????
        // ==============================================================

        private async Task BuildAssociationsAsync(string observation)
        {
            try
            {
                // ?????? ???'????? ???????
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

                var assoc = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

        // ------------------------------------------------------------
        // ОБРОБКА ПОВІДОМЛЕННЯ КОРИСТУВАЧА
        // Виклик після кожного повідомлення з UI — оновлює всі двигуни
        // ------------------------------------------------------------

        public void ProcessUserMessage(string content)
        {
            try
            {
                _state.TotalMessagesExchanged++;
                _state.LastKnownUserActivity = "chatting";
                var now = DateTime.Now;
                _state.LastUserMessageAt = now;
                // A direct user message is already engagement. Do not let background
                // proactive timers immediately answer with stale "are you there" pings.
                _state.LastSpontaneousAt = now;
                ApplyUserControlCommand(content, now);
                KokoConversationStagnationGuard.Observe(_state, content, now);
                var autonomyLevel = AppSettings.Load().ProactiveAutonomyLevel;
                var msgs = _chatRepo.GetMessages(20).OrderBy(m => m.Timestamp).ToList();

                // 1. Freshness pass: resolve stale intents and detect return/wake signals
                try { StateFreshness.Refresh(_state, msgs, now); } catch { }

                // 2. Presence & Day state updates
                try { Presence.ObserveUserMessage(_state, content, now); } catch { }
                try { EmotionalMemory.ObserveUserMessage(_state, content, msgs, now); } catch { }
                try 
                { 
                    var presence = Presence.Evaluate(_state, msgs, now, autonomyLevel);
                    var somatic = Somatic.Evaluate(ServiceContainer.Heart, Emotion, _health, now);
                    var dayFrame = InternalDay.Evaluate(_state, presence, somatic, now, autonomyLevel);
                    InternalDay.Record(_state, dayFrame, now); 
                } catch { }

                ObserveFoodSleepState(content, now);
                ObserveShortTermIntent(content);
                ApplyScreenAwarenessUserPreference(content, now);
                
                RecordPersonaDecision(content, now);
                RecordResponsePlan(content, now);
                RecordMemoryPolicyAndContinuity(content, now);

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
                        var (facts, episodes) = Memory.FindRelevantBlocking(content, maxFacts: 3, maxEpisodes: 2);
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

        public bool TryApplyUserControlCommand(string content, out string reply)
        {
            reply = "";
            if (string.IsNullOrWhiteSpace(content)) return false;

            var now = DateTime.Now;
            if (ApplyUserControlCommand(content, now))
            {
                reply = _state.ProactiveMutedUntil > now
                    ? $"Добре. Я прибрала зайві нагадування до {_state.ProactiveMutedUntil:HH:mm}. Мовчу, поки ти сам не смикнеш мене."
                    : "Добре. Можеш знову смикати мене, якщо зовсім знудишся.";
                SaveState();
                return true;
            }

            return false;
        }

        private bool ApplyUserControlCommand(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            var wantsQuiet = ContainsAny(lower,
                "іди відпочинь", "йди відпочинь", "відпочинь", "спи", "іди спати", "йди спати",
                "замовкни", "мовчи", "не пиши", "не чіпай", "не турбуй", "не нагадуй",
                "зупинись", "зупиняємось", "стоп", "пауза",
                "мовчи", "не пиши", "не чіпай", "зупинись");
            var resume = LooksLikeExplicitResumeControl(lower);

            if (resume)
            {
                _state.ProactiveMutedUntil = DateTime.MinValue;
                _state.ProactiveMuteReason = "";
                return true;
            }

            if (!wantsQuiet) return false;

            _state.ProactiveMutedUntil = now.AddHours(6);
            _state.ProactiveMuteReason = TrimStateMention(content);
            _state.PendingTriggers.Clear();
            foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue))
            {
                if (intent.Kind == "sleep")
                {
                    LogSleepIntent("kept active while user muted proactive follow-ups: " + TrimStateMention(content));
                    continue;
                }

                ResolveIntent(intent, now, "user muted proactive follow-ups: " + TrimStateMention(content));
            }
            _state.SilenceLevel1At = now;
            _state.SilenceLevel2At = now;
            _state.SilenceLevel3At = now;
            _state.LastSpontaneousAt = now;
            return true;
        }

        private bool LooksLikeExplicitResumeControl(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower)) return false;

            if (ContainsAny(lower,
                    "повернись", "можеш писати", "можеш знову писати", "пиши знову",
                    "розбудись", "активуйся", "слухай далі", "вернись",
                    "повертайся", "виходь з паузи", "зніми паузу",
                    "можешь писать", "пиши снова", "активируйся"))
                return true;

            if (_state.ProactiveMutedUntil <= DateTime.Now)
                return false;

            return ContainsAny(lower,
                "продовжуй писати", "продовжуй нагадувати", "продовжуй пінгувати",
                "continue messaging", "resume messaging", "resume reminders");
        }

        private static bool LooksLikeNarrativeContinuationCommand(string? text)
        {
            var lower = (text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var compact = new string(lower.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            while (compact.Contains("  ", StringComparison.Ordinal))
                compact = compact.Replace("  ", " ");
            compact = compact.Trim();

            if (compact is "продовжуй" or "продовж" or "далі" or "дальше" or "продолжай" or "continue" or "go on")
                return true;

            return compact.StartsWith("продовжуй ", StringComparison.Ordinal) ||
                   compact.StartsWith("продовж ", StringComparison.Ordinal) ||
                   compact.StartsWith("давай далі", StringComparison.Ordinal) ||
                   compact.StartsWith("давай дальше", StringComparison.Ordinal) ||
                   compact.StartsWith("continue ", StringComparison.Ordinal) ||
                   compact.StartsWith("go on", StringComparison.Ordinal);
        }

        private string BuildNarrativeContinuationBlock(string? userText)
        {
            if (!LooksLikeNarrativeContinuationCommand(userText))
                return "";

            var recent = _chatRepo.GetMessages(8)
                .OrderBy(m => m.Timestamp)
                .Where(m => !LooksLikeInternalStatusLeak(m.Content))
                .TakeLast(6)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== CONTINUATION OVERRIDE ===");
            sb.AppendLine("Latest user message is a continuation command. Treat it as conversational/narrative continuation, not as a system resume, autoping, scheduler, or background-task status request.");
            sb.AppendLine("Priority: continue the latest active human thread from recent chat. If no coherent thread exists, ask one sharp concrete question. Do not mention autoping, follow-up queues, scheduler ids, or background mechanics.");
            if (recent.Count > 0)
            {
                sb.AppendLine("Recent thread to continue:");
                foreach (var m in recent)
                {
                    var role = m.Role == "user" ? "user" : "Kokonoe";
                    var content = TrimStateMention(m.Content);
                    sb.AppendLine($"- [{m.Timestamp:HH:mm}] {role}: {content}");
                }
            }

            return sb.ToString();
        }

        private string BuildNarrativeThreadSummary(DateTime now)
        {
            try
            {
                var recent = _chatRepo.GetMessages(10)
                    .OrderBy(m => m.Timestamp)
                    .Where(m => !LooksLikeInternalStatusLeak(m.Content))
                    .TakeLast(5)
                    .Select(m =>
                    {
                        var role = m.Role == "user" ? "user" : "Kokonoe";
                        var text = TrimStateMention(m.Content ?? "");
                        if (text.Length > 120) text = text[..120] + "...";
                        return $"{role}@{m.Timestamp:HH:mm}: {text}";
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (recent.Count == 0)
                    return $"no active narrative thread as of {now:HH:mm}";

                var screenSummary = TrimStateMention(_state.LastScreenAwarenessSummary);
                var screen = string.IsNullOrWhiteSpace(screenSummary)
                    ? ""
                    : $" | screen={screenSummary[..Math.Min(120, screenSummary.Length)]}";
                return string.Join(" || ", recent) + screen;
            }
            catch
            {
                return $"narrative thread unavailable as of {now:HH:mm}";
            }
        }

        private static bool LooksLikeInternalStatusLeak(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            return string.IsNullOrWhiteSpace(lower) ||
                   lower.Contains("[action:", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("autoping", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("автоп", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("follow-up", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("scheduler:", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("запис у scheduler", StringComparison.OrdinalIgnoreCase);
        }

        private void ObserveFoodSleepState(string content, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var lower = content.ToLowerInvariant();
            var compact = TrimStateMention(content);

            if (SaysNotEaten(lower))
            {
                _state.LastFoodStatus = "not_eaten";
                _state.LastFoodMentionAt = now;
                _state.LastFoodMentionText = compact;
            }
            else if (SaysAte(lower))
            {
                _state.LastFoodStatus = "ate";
                _state.LastFoodMentionAt = now;
                _state.LastFoodMentionText = compact;
            }
            else if (ContainsAny(lower, "голод", "хочу їсти", "хочу есть", "їсти хочу", "есть хочу"))
            {
                _state.LastFoodStatus = "hungry";
                _state.LastFoodMentionAt = now;
                _state.LastFoodMentionText = compact;
            }

            if (ContainsAny(lower, "прокин", "проснув", "встав", "поспав"))
            {
                _state.LastSleepStatus = "woke_or_returned";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
            else if (ContainsAny(lower, "заснув", "спав", "ліг спати", "ліг спать", "ляг спати", "ляг спать"))
            {
                _state.LastSleepStatus = "slept";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
            else if (ContainsAny(lower, "я спать", "я спати", "піду спати", "пішов спати", "лягаю"))
            {
                _state.LastSleepStatus = "going_to_sleep";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
        }

        private static bool SaysNotEaten(string lower)
            => ContainsAny(lower,
                "не їв", "не ів", "не ел", "не їла", "не їли",
                "нічого не їв", "ничего не ел", "ще нічого не їв", "ще не їв", "ще не їла",
                "не їв зранку", "без їжі", "без еды");

        private static bool SaysAte(string lower)
            => ContainsAny(lower,
                "я їв", "я ів", "я ел", "поїв", "поів", "поел",
                "з'їв", "з’їв", "зїв", "з'ів", "з’ів", "з'ела", "з’ела",
                "піц", "снідав", "обідав", "вечеряв", "їв ", " їв", "їла", "ел ");

        private static string TrimStateMention(string text)
        {
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ");
            return text.Length <= 120 ? text : text[..120].TrimEnd() + "...";
        }

        private void ObserveShortTermIntent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var now = DateTime.Now;
            _state.ShortTermIntents.RemoveAll(i =>
                i.ResolvedAt.HasValue && now - i.ResolvedAt.Value > TimeSpan.FromDays(2));
            _state.ShortTermIntents.RemoveAll(i =>
                !i.ResolvedAt.HasValue && i.Kind != "sleep" && now - i.ExpectedUntil > TimeSpan.FromHours(12));

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
            if (detected.Kind != "sleep")
            {
                _state.PendingTriggers.Add(new ReactiveTrigger
                {
                    Type = "intent_followup",
                    FireAt = detected.FollowUpAt,
                    Context = $"Користувач сказав: «{detected.SourceText}». Намір: {detected.Summary}. Якщо він повернеться або мине час, доречно спитати коротко: «{BuildIntentQuestion(detected)}»"
                });
            }
        }

        private void ApplyScreenAwarenessUserPreference(string content, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var lower = content.ToLowerInvariant();
            var allow = new[] { "можеш дивитись", "можеш підглядати", "дивись екран", "слідкуй за екраном" };
            if (allow.Any(p => lower.Contains(p)))
            {
                _state.ScreenAwarenessObserveOnlyUntil = DateTime.MinValue;
                return;
            }

            var block = new[] { "не підглядуй", "не дивись", "не слідкуй", "не спостерігай", "не чіпай екран" };
            if (block.Any(p => lower.Contains(p)))
                _state.ScreenAwarenessObserveOnlyUntil = now.AddMinutes(30);
        }

        private static ShortTermIntent? DetectShortTermIntent(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            if (LooksLikeSleepOrGoodbye(lower))
                return BuildSleepIntent(
                    KokoConversationBoundary.LooksLikeClosedUntilMorning(content)
                        ? "закрив розмову до ранку"
                        : "пішов спати/попрощався",
                    content,
                    now);

            if (!ContainsAny(lower, "піду", "йду", "іду", "пішов", "буду", "зараз", "скоро")) return null;

            var returnHome = TryDetectReturnHomeIntent(content, lower, now);
            if (returnHome != null) return returnHome;

            if (ContainsAny(lower, "курс", "курси", "занят", "урок", "пара", "навчан"))
                return BuildIntent("course", "пішов на курси/заняття", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "робот", "прац", "код", "проект"))
                return BuildIntent("work", "зайнятий роботою/проєктом", content, now, TimeSpan.FromHours(3), TimeSpan.FromHours(1.5));
            if (ContainsAny(lower, "магаз", "куп", "продукт"))
                return BuildIntent("errand", "пішов у магазин/по справах", content, now, TimeSpan.FromHours(1.5), TimeSpan.FromMinutes(50));
            if (ContainsAny(lower, "гуля", "прогуля", "вийду", "вулиц"))
                return BuildIntent("walk", "пішов гуляти/на вулицю", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "спать", "спати", "сон", "ляга"))
                return BuildSleepIntent("пішов спати", content, now);

            if (ContainsAny(lower, "зайнят", "відійду", "афк", "не буду"))
                return BuildIntent("busy", "буде зайнятий або відійде", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));

            return null;
        }

        private static ShortTermIntent? TryDetectReturnHomeIntent(string content, string lower, DateTime now)
        {
            if (!ContainsAny(lower, "дома", "вдома", "додому", "домой", "хату", "хата")) return null;
            if (!ContainsAny(lower, "буду", "поверн", "верн", "прийду", "приїду", "зайду")) return null;

            var match = System.Text.RegularExpressions.Regex.Match(lower, @"(?:в|о|об)\s*(\d{1,2})(?::(\d{2}))?");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var hour)) return null;

            hour = Math.Clamp(hour, 0, 23);
            var minute = 0;
            if (match.Groups[2].Success)
                int.TryParse(match.Groups[2].Value, out minute);
            minute = Math.Clamp(minute, 0, 59);

            var expectedAt = now.Date.AddHours(hour).AddMinutes(minute);
            if (expectedAt < now.AddMinutes(-30))
                expectedAt = expectedAt.AddDays(1);

            return BuildIntent(
                "return_home",
                $"має бути вдома близько {expectedAt:HH:mm}",
                content,
                now,
                expectedAt,
                expectedAt.AddMinutes(12));
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

        private static ShortTermIntent BuildSleepIntent(string summary, string source, DateTime now)
        {
            var expectedUntil = BuildSleepExpectedUntil(now);
            return BuildIntent(
                "sleep",
                summary,
                source,
                now,
                expectedUntil,
                expectedUntil);
        }

        private static DateTime BuildSleepExpectedUntil(DateTime now)
        {
            // Night sleep should resolve around the next morning, not "now + 10h".
            // Yes, 04:42 + 10h = 14:42. Arithmetic obeyed; common sense did not.
            if (now.Hour >= 20)
                return now.Date.AddDays(1).AddHours(9);
            if (now.Hour < 8)
                return now.Date.AddHours(9);
            return now.AddHours(2.5);
        }

        private static ShortTermIntent BuildIntent(string kind, string summary, string source, DateTime now, DateTime expectedUntil, DateTime followUpAt)
            => new()
            {
                Kind = kind,
                Summary = summary,
                SourceText = source.Trim(),
                CreatedAt = now,
                ExpectedUntil = expectedUntil,
                FollowUpAt = followUpAt
            };

        private void ResolveShortTermIntentsFromMessage(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            var returned = ContainsAny(lower,
                "повернув", "прийшов", "я тут", "тут", "угу", "всм", "закінчив", "закінчились", "вже вдома",
                "поспав", "проснув", "прокинув", "поїв", "поів", "їв", "норм поїв", "відпочиваю", "відпочив",
                "вернув", "повернув", "прийшов", "я тут", "закінчив", "закінчились", "вже вдома", "поспав", "проснув", "прокинув");
            if (!returned) return;
            var explicitWake = LooksLikeExplicitWakeMessage(lower);

            foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue))
            {
                if (intent.Kind == "sleep" && !explicitWake)
                    continue;

                ResolveIntent(intent, now, "user message resolved intent: " + TrimStateMention(content));
            }
            _state.PendingTriggers.RemoveAll(t => t.Type == "intent_followup");
        }

        private void ResolveIntent(ShortTermIntent intent, DateTime now, string reason)
        {
            intent.ResolvedAt = now;
            intent.ResolutionText = reason;
            if (intent.Kind == "sleep")
                LogSleepIntent("deactivated: " + reason);
        }

        private static bool LooksLikeExplicitWakeMessage(string lower)
            => ContainsAny(lower,
                "прокин", "проснув", "поспав", "встав", "я встав", "я прокинув", "я проснув",
                "woke", "wake", "awake", "i am up", "im up",
                "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ");

        private static void LogSleepIntent(string message)
        {
            try { KokoSystemLog.Write("SLEEP_INTENT", message); }
            catch { }
        }

        private static string BuildIntentQuestion(ShortTermIntent intent) => intent.Kind switch
        {
            "course" => "Курси вже закінчились, чи ти ще там героїчно страждаєш?",
            "work" => "Робочий запій закінчився, чи ти ще закопаний у задачі?",
            "errand" => "Ти вже повернувся зі справ, чи магазин тебе поглинув?",
            "walk" => "Прогулянка закінчилась, чи ти ще десь блукаєш?",
            "sleep" => "Ти вже прокинувся, чи організм нарешті переміг твої дурні графіки?",
            "return_home" => "Ти вже вдома, чи твій маршрут знову вирішив стати побічним квестом?",
            _ => "Ти вже повернувся до нормального режиму, чи ще зайнятий?"
        };

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static bool LooksLikeSleepOrGoodbye(string lower)
            => KokoConversationBoundary.LooksLikeClosedUntilMorning(lower) ||
               ContainsAny(lower,
                "\u0431\u0430\u0439 \u0431\u0430\u0439", "\u0431\u0430\u0439-\u0431\u0430\u0439", "\u0431\u0443\u0432\u0430\u0439", "\u043f\u043e\u043a\u0430",
                "\u0434\u043e\u0431\u0440\u0430\u043d\u0456\u0447", "\u0434\u043e\u0431\u0440\u043e\u0457 \u043d\u043e\u0447\u0456", "\u0441\u043f\u043e\u043a\u0456\u0439\u043d\u043e\u0457", "\u0441\u043f\u043e\u043a\u043e\u0439\u043d\u043e\u0439",
                "\u044f \u0441\u043f\u0430\u0442\u044c", "\u044f \u0441\u043f\u0430\u0442\u0438", "\u043f\u0456\u0434\u0443 \u0441\u043f\u0430\u0442\u0438", "\u043f\u0456\u0448\u043e\u0432 \u0441\u043f\u0430\u0442\u0438", "\u043b\u044f\u0433\u0430\u044e",
                "бай бай", "бай-бай", "баю бай", "баю-бай", "бувай", "пока",
                "добраніч", "доброй ночи", "спокійної", "спокойной",
                "я спать", "я спати", "піду спати", "пішов спати", "лягаю");

        private bool ShouldSuppressProactiveForSleep(DateTime now)
        {
            lock (_lock)
            {
                return _state.ShortTermIntents.Any(i =>
                    !i.ResolvedAt.HasValue &&
                    i.Kind == "sleep" &&
                    now < i.ExpectedUntil.AddHours(2));
            }
        }

        private void ExtractAndRememberFacts(string userMsg)
        {
            var policy = MemoryWritePolicy.EvaluateAsync(userMsg, DateTime.Now, Memory, Emotion).GetAwaiter().GetResult();
            if (policy.Action is "ignore" or "daily_log" or "review" or "reinforce_existing")
                return;

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
                        Memory.LearnFactBlocking(fact, cat, imp);
                    }
                }
            }
        }

        private KokoPersonaFrame RecordPersonaDecision(string userText, DateTime now)
        {
            var frame = Persona.Build(userText, _state, now, Emotion.Bond);
            var temperament = Temperament.Update(_state, userText, frame.Mode, now);
            var social = BuildSocialFrame(userText, now);
            var living = LivingConversation.Update(_state, userText, frame.Mode, Emotion, social, now);
            _state.LastPersonaDecision = frame.PromptBlock + "\n" + temperament.PromptBlock + "\n" + social.PromptBlock + "\n" + living.PromptBlock;
            _state.LastPersonaDecisionAt = now;
            _state.PersonaDecisionLog.Add(frame.TraceLine);
            _state.PersonaDecisionLog.Add(temperament.TraceLine);
            _state.PersonaDecisionLog.Add(social.TraceLine);
            _state.PersonaDecisionLog.Add(living.TraceLine);
            if (_state.PersonaDecisionLog.Count > 40)
                _state.PersonaDecisionLog.RemoveRange(0, _state.PersonaDecisionLog.Count - 40);

            _state.InnerMonologues.Add($"[persona/{frame.Mode}] {frame.Stance}. {frame.ReasonUk}");
            _state.InnerMonologues.Add($"[temperament/{temperament.MoodState}] energy={temperament.EnergyLevel:F2}; patience={temperament.PatienceLevel:F2}");
            _state.InnerMonologues.Add($"[living/{living.Mode}] move={living.CurrentMove}; color={living.EmotionalColor}; {living.Reason}");
            if (_state.InnerMonologues.Count > 80)
                _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);

            return frame;
        }

        private KokoSocialFrame BuildSocialFrame(string? userText, DateTime now)
        {
            KokoWearableState? wearable = null;
            try { wearable = ServiceContainer.IsInitialized ? ServiceContainer.WearableTelemetry.State : null; } catch { }
            return Social.Analyze(userText ?? "", _state, _chatRepo.GetMessages(12), wearable, now);
        }

        private string BuildSocialContextBlock(string? userText, DateTime now)
        {
            try { return BuildSocialFrame(userText, now).PromptBlock; }
            catch (Exception ex)
            {
                KokoSystemLog.Write("SOCIAL", "context failed: " + ex.Message);
                return "SOCIAL SUBTEXT / PERSONALITY FLUX\nsubtext: unavailable\n";
            }
        }

        private KokoResponsePlanFrame BuildGovernedResponsePlan(string userText, DateTime now)
        {
            var social = BuildSocialFrame(userText, now);
            var recentForPlanning = _chatRepo.GetMessages(30);
            if (LooksLikeNarrativeContinuationCommand(userText))
                recentForPlanning = recentForPlanning.Where(m => !LooksLikeInternalStatusLeak(m.Content)).ToList();
            var emotional = EmotionalMemory.BuildPromptBlock(_state, recentForPlanning, now, userText);
            var rawHydration = EmotionalMemory.BuildRawHydrationBlock(_state, recentForPlanning, now);
            var settings = AppSettings.Load();
            if (settings.NeuralGovernorEnabled && ServiceContainer.IsInitialized)
            {
                try
                {
                    using var cts = new CancellationTokenSource(Math.Clamp(settings.NeuralGovernorTimeoutMs, 500, 6000));
                    KokoWearableState? wearable = null;
                    try { wearable = ServiceContainer.WearableTelemetry.State; } catch { }
                    var memoryContext = "";
                    try
                    {
                        if (KokoResponsePlannerEngine.NeedsVaultRead(userText.ToLowerInvariant(), "memory") ||
                            KokoProfileUpdateService.LooksLikeProfileUpdateRequest(userText.ToLowerInvariant()))
                            memoryContext = new ObsidianPreflightContextService(_obsidian).Build(userText, now, 1600) ?? "";
                    }
                    catch { }

                    var neural = NeuralGovernor.TryBuildFrameAsync(
                            userText,
                            _state,
                            social,
                            emotional,
                            rawHydration,
                            recentForPlanning.Take(24).ToList(),
                            memoryContext,
                            _cachedScreenContext,
                            wearable,
                            now,
                            cts.Token)
                        .GetAwaiter()
                        .GetResult();
                    if (neural != null)
                    {
                        neural.PromptBlock += "\n" + social.PromptBlock;
                        neural.PromptBlock += "\n" + emotional;
                        neural.PromptBlock += "\n" + rawHydration;
                        return neural;
                    }
                }
                catch (Exception ex)
                {
                    KokoSystemLog.Write("NEURAL-GOVERNOR", "sync route failed: " + ex.Message);
                }
            }

            var fallback = ResponsePlanner.Build(userText, _state, Cognition, now);
            fallback.PromptBlock += "\n" + social.PromptBlock;
            fallback.PromptBlock += "\n" + emotional;
            fallback.PromptBlock += "\n" + rawHydration;
            fallback.TraceLine += "; governor=fallback";
            KokoSystemLog.Write("NEURAL-GOVERNOR", "fallback used: " + fallback.TraceLine);
            return fallback;
        }

        private KokoResponsePlanFrame RecordResponsePlan(string userText, DateTime now)
        {
            var frame = BuildGovernedResponsePlan(userText, now);
            _state.LastResponsePlan = frame.PromptBlock;
            _state.LastResponsePlanTrace = frame.TraceLine;
            _state.LastResponsePlanAt = now;
            _state.ResponsePlanLog.Add(frame.TraceLine);
            if (_state.ResponsePlanLog.Count > 60)
                _state.ResponsePlanLog.RemoveRange(0, _state.ResponsePlanLog.Count - 60);

            _state.InnerMonologues.Add($"[plan/{frame.Intent}] {frame.InnerMonologue}");
            if (frame.CritiqueSteps.Count > 0)
                _state.InnerMonologues.Add($"[critique/{frame.Intent}] {string.Join(" -> ", frame.CritiqueSteps.Take(3))}");
            if (_state.InnerMonologues.Count > 80)
                _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);

            return frame;
        }

        private KokoMemoryWriteDecision RecordMemoryPolicyAndContinuity(string userText, DateTime now)
        {
            var decision = MemoryWritePolicy.EvaluateAsync(userText, now, Memory, Emotion).GetAwaiter().GetResult();
            _state.LastMemoryPolicyDecision = decision.TraceLine;
            _state.LastMemoryPolicyAt = now;
            _state.MemoryPolicyLog.Add(decision.TraceLine);
            if (_state.MemoryPolicyLog.Count > 60)
                _state.MemoryPolicyLog.RemoveRange(0, _state.MemoryPolicyLog.Count - 60);

            var belief = Continuity.ApplyMemoryDecision(decision, now);
            _state.LastContinuitySummary = belief == null
                ? Continuity.BuildDebugLine()
                : $"{belief.Status}/{belief.Kind}: {belief.Claim}";
            _state.LastContinuityAt = now;

            if (decision.Action != "ignore")
            {
                _state.InnerMonologues.Add($"[memory/{decision.Action}] {decision.ReasonUk}");
                if (_state.InnerMonologues.Count > 80)
                    _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);
            }

            return decision;
        }

        // ------------------------------------------------------------
        // ПЕРЕВІРКА ПЛАНУВАЛЬНИКА
        // ------------------------------------------------------------

        private async Task CheckSchedulerAsync()
        {
            try
            {
                var due = Scheduler.GetDue(_state.LastUserEmotionalTone);
                if (due.Count == 0) return;

                var entry = due.First(); // беремо найпріоритетніший

                if (!EnsureTelegram()) return;

                var prompt = BuildSchedulerDeliveryPrompt(entry);

                var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

        private static string BuildSchedulerDeliveryPrompt(KokoSchedulerEngine.ScheduledEntry entry)
        {
            return $@"Ти — Kokonoe. Це заплановане нагадування для користувача.

Сирий запис scheduler:
«{entry.Prompt}»

Правила:
- Якщо в сирому записі є ""я піду"", ""я буду"", ""мені"", це майже завжди слова користувача з моменту створення нагадування, не твій власний розклад.
- Не кажи, що ти йдеш на курси, зайнята, маєш дедлайн або власний розклад, якщо це прямо не написано як подія Kokonoe.
- Перепиши як нагадування користувачу: ""ти планував..."", ""ти просив..."", ""час..."", без службового слова scheduler.
- Якщо запис виглядає простроченим або контекст слабкий, не вигадуй; скажи коротко, що нагадування було про його план.
- Тільки українська. 1-3 речення. Тільки фінальний текст.";
        }

        // ==============================================================
        // ???? ? ????????? ?? ??????
        // ==============================================================

        private async Task UpdateDossierAsync()
        {
            try
            {
                // ?????? ??????? ????? ???? ?
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

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

        // ==============================================================
        // ??????????? ???'?? > OBSIDIAN VAULT
        // ==============================================================

        private async Task SyncMemoryToVaultAsync()
        {
            try
            {
                // 1. ????? > Kokonoe/Memory/Facts.md
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
                    var daySummary = await _llm.SendSystemQueryAsync(summaryPrompt, useTools: true);
                    if (!string.IsNullOrWhiteSpace(daySummary) && daySummary.Length > 10)
                        _obsidian.AppendToDailyNote($"\n\n> 🧠 **Kokonoe:** {daySummary.Trim()}");
                }

                // 4. Оновити зв'язки між нотатками
                try { _obsidian.RebuildLinks(); } catch { }

                Log("Memory synced to vault");
            }
            catch (Exception ex) { Log($"SyncMemoryToVault: {ex.Message}"); }
        }

        // ==============================================================
        // VAULT REVIEW ? ?????????? ? ??????? ?????? ???????
        // ==============================================================

        /// <summary>
        /// ??? ?? ???? Kokonoe ???????? ???? ??????? ??? ?????? ? ??????? ??
        /// ?? ????? ????? ??????. ????? ????????? ????????? vault.
        /// </summary>
        private async Task ReviewVaultAsync()
        {
            try
            {
                // Cooldown: ??? ?? ????
                if (_state.LastVaultReviewAt.Date >= DateTime.Today) return;
                _state.LastVaultReviewAt = DateTime.Now;
                SaveState();

                // ?????? ??????? ??????? ??? ??????
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

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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
                                    $"\n\n## Логи чатів\nВсі розмови зберігаються в `Chats/` — {chatLogs.Count} сесій.\n");
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

        // ==============================================================
        // ???????????? ????? VAULT (???????)
        // ==============================================================

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

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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
                    await _llm.SendSystemQueryAsync(execPrompt, ct: CancellationToken.None);
                }

                Log($"ArchitectureReview done. severity={severity}, askUser={askUser}");
            }
            catch (Exception ex) { Log($"VaultArchitectureReview: {ex.Message}"); }
        }

        // ==============================================================
        // ???????????? ???? ??????
        // ==============================================================

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

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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
                Memory.LearnFactBlocking(memoryText, "relationship_reflection", importance, new[] { "reflection", reflection.UserTone });
                Memory.RecordEpisodeBlocking(memoryText, reflection.UserTone, importance,
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

        // ==============================================================

        /// <summary>
        /// ???????? ???? vault ? ???? ? ???????? ? ???? ????? ?? ????? ?????????/?????????.
        /// </summary>
        private void CheckVaultHealth()
        {
            try
            {
                var status = _obsidian.GetVaultStatus();

                if (status.TotalNotes < 3)
                {
                    // Vault ???????? ? ????? ???????????????
                    var initStatus = _obsidian.GetVaultInitStatus();
                    if (!_state.PendingThoughts.Contains(initStatus.SuggestedAction))
                        _state.PendingThoughts.Add(initStatus.SuggestedAction);
                }
                else if (status.OrphanNotes.Count > 0)
                {
                    // ? ??????? ??????? ? ???????? ??? ??'????
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

        // ---- SCREEN CONTEXT ----

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

        private async Task SafeScreenAwarenessAsync(string forceReason = "", string toneDirective = "")
        {
            var settings = AppSettings.Load();
            if (!settings.ScreenAwarenessEnabled) return;

            var now = DateTime.Now;
            var interval = GetEffectiveScreenAwarenessInterval(settings, now);
            var forceBySignal = !string.IsNullOrWhiteSpace(forceReason);
            var forceByInitiative = !forceBySignal && ShouldForceScreenAwarenessFromAutonomy(now);
            var sinceLastScreen = (now - _state.LastScreenAwarenessAt).TotalMinutes;
            if (!forceBySignal && !forceByInitiative && sinceLastScreen < interval)
            {
                Log($"ScreenAwareness suppressed: interval {sinceLastScreen:0.0}m < {interval}m");
                return;
            }
            if (forceBySignal)
                Log($"ScreenAwareness forced: {forceReason}");
            if (forceByInitiative)
                Log("ScreenAwareness forced: autonomy curiosity/observation trigger");
            if (now < _state.VisionBackoffUntil)
            {
                Log($"ScreenAwareness skipped: vision backoff until {_state.VisionBackoffUntil:HH:mm}");
                return;
            }

            if (ShouldSuppressProactiveForSleep(now))
            {
                Log("ScreenAwareness suppressed: sleep/goodbye context is active");
                return;
            }

            try
            {
                var diag = _llm.GetDiagnosticsSnapshot();
                if (diag.InFlight > 0)
                {
                    Log("ScreenAwareness skipped: foreground LLM is busy");
                    return;
                }
            }
            catch { }

            if (!await _bgLlmSemaphore.WaitAsync(0))
            {
                Log("ScreenAwareness skipped: background LLM is busy");
                return;
            }

            try
            {
                byte[] screenshot;
                try
                {
                    screenshot = await Task.Run(() => ServiceContainer.PcControl.TakeScreenshot());
                }
                catch (Exception ex)
                {
                    Log($"ScreenAwareness capture failed: {ex.Message}");
                    return;
                }

                if (screenshot.Length == 0) return;

                var activity = _screenActivityAnalyzer.AnalyzeScreenshot(screenshot);
                var foreground = ServiceContainer.PcControl.GetForegroundWindow();
                if (string.IsNullOrWhiteSpace(activity.ActiveWindowTitle) && !string.IsNullOrWhiteSpace(foreground.Title))
                    activity.ActiveWindowTitle = foreground.Title;
                var idleTime = TimeSpan.Zero;
                try { idleTime = ServiceContainer.PcControl.GetSystemInfo().IdleTime; }
                catch (Exception ex) { Log($"ScreenAwareness idle-time read failed: {ex.Message}"); }
                var hash = _screenActivityAnalyzer.GenerateScreenshotHash(screenshot);
                var screenChanged = activity.IsActive ||
                    (!string.IsNullOrWhiteSpace(_state.LastScreenAwarenessHash) &&
                     hash != _state.LastScreenAwarenessHash &&
                     activity.PixelDifferencePercentage >= 1.0);
                var prompt = ScreenAwareness.BuildVisionPrompt(
                    activity,
                    _state.LastScreenAwarenessSummary,
                    _state.LastScreenAwarenessComment,
                    now,
                    foreground,
                    idleTime,
                    BuildVisionMultimodalContext(now, forceReason, toneDirective));

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var raw = await _llm.SendSystemVisionQueryAsync(prompt, screenshot, "image/jpeg", cts.Token);
                if (LooksLikeVisionFailure(raw))
                {
                    try
                    {
                        var enhanced = ImageProcessingService.EnhanceForVision(screenshot);
                        var retryPrompt = VisionResponseQuality.BuildRetryPrompt(prompt, foreground.ToString());
                        using var retryCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                        var retryRaw = await _llm.SendSystemVisionQueryAsync(
                            retryPrompt,
                            enhanced.Length > 0 ? enhanced : screenshot,
                            "image/jpeg",
                            retryCts.Token,
                            maxTokensOverride: 2048);
                        if (!LooksLikeVisionFailure(retryRaw))
                            raw = retryRaw;
                    }
                    catch (Exception ex)
                    {
                        Log($"ScreenAwareness vision repair failed: {ex.Message}");
                    }
                }

                if (LooksLikeVisionFailure(raw))
                {
                    RegisterVisionFailure(raw, now, activity);
                    SaveState();
                    return;
                }
                var analysis = ScreenAwareness.Parse(raw);
                var previousSituation = new KokoScreenSituation
                {
                    CurrentTask = _state.LastScreenSituationTask,
                    Progress = _state.LastScreenSituationProgress,
                    Blocker = _state.LastScreenSituationBlocker,
                    RecommendedBehavior = string.IsNullOrWhiteSpace(_state.LastScreenSituationBehavior)
                        ? "observe"
                        : _state.LastScreenSituationBehavior,
                    Reason = _state.LastScreenSituationReason
                };
                var situation = ScreenAwareness.BuildSituation(analysis, activity, previousSituation);
                var context = ScreenAwareness.BuildCompactContext(analysis, activity, situation);
                var patternCandidate = ScreenAwareness.BuildPatternCandidate(analysis, situation, now);
                var predictiveWarmup = ProactiveContext.BuildScreenWarmup(analysis, activity, _obsidian, now);
                if (predictiveWarmup.HasContext)
                    context += "\n\n" + predictiveWarmup.PromptBlock;
                var semanticFrame = SemanticVision.BuildFrame(analysis, activity, situation, _state, now);
                context += "\n\n" + semanticFrame.PromptBlock;

                lock (_lock)
                {
                    _state.LastScreenAwarenessAt = now;
                    _state.LastScreenAwarenessHash = hash;
                    _state.LastScreenAwarenessSummary = analysis.SummaryUk;
                    _state.LastScreenAwarenessActivity = analysis.ActivityUk;
                    _state.LastScreenAwarenessMode = analysis.ScreenMode;
                    _state.LastScreenAwarenessWindow = activity.ActiveWindowTitle ?? "";
                    _state.LastScreenSituationTask = situation.CurrentTask;
                    _state.LastScreenSituationProgress = situation.Progress;
                    _state.LastScreenSituationBlocker = situation.Blocker;
                    _state.LastScreenSituationBehavior = situation.RecommendedBehavior;
                    _state.LastScreenSituationReason = situation.Reason;
                    SemanticVision.ApplyToState(_state, semanticFrame);
                    if (semanticFrame.ShouldResearch && !string.IsNullOrWhiteSpace(semanticFrame.ResearchTopic))
                    {
                        var curiosity = "[semantic-vision] Research or inspect: " + semanticFrame.ResearchTopic;
                        if (!_state.CuriosityQueue.Any(q => q.Contains(semanticFrame.ResearchTopic, StringComparison.OrdinalIgnoreCase)))
                        {
                            _state.CuriosityQueue.Add(curiosity);
                            if (_state.CuriosityQueue.Count > 20)
                                _state.CuriosityQueue.RemoveRange(0, _state.CuriosityQueue.Count - 20);
                        }
                    }
                    _state.VisionFailureCount = 0;
                    _state.VisionBackoffUntil = DateTime.MinValue;
                    _state.LastKnownUserActivity = string.IsNullOrWhiteSpace(analysis.SummaryUk)
                        ? (activity.ActiveWindowTitle ?? "")
                        : analysis.SummaryUk;
                    if (predictiveWarmup.HasContext)
                    {
                        var isNewWarmup = !string.Equals(_state.LastPredictiveContextMode, predictiveWarmup.Mode, StringComparison.OrdinalIgnoreCase) ||
                                          now - _state.LastPredictiveContextAt > TimeSpan.FromMinutes(30);
                        _state.LastPredictiveContextAt = now;
                        _state.LastPredictiveContextMode = predictiveWarmup.Mode;
                        _state.LastPredictiveContextSummary = predictiveWarmup.Summary;
                        _state.LastPredictiveContextNotes = string.Join("; ", predictiveWarmup.SourcePaths.Take(6));
                        if (string.Equals(predictiveWarmup.AppKey, "browser", StringComparison.OrdinalIgnoreCase) &&
                            activity.TimeSinceLastChange >= TimeSpan.FromMinutes(10))
                        {
                            _state.CachedRelevantMemory = predictiveWarmup.PromptBlock;
                            _state.RelevantMemoryCachedAt = now;
                        }
                        if (isNewWarmup)
                        {
                            _state.PendingThoughts.Add("[screen-warmup] " + TrimStateMention(predictiveWarmup.Summary));
                            if (_state.PendingThoughts.Count > 20)
                                _state.PendingThoughts.RemoveRange(0, _state.PendingThoughts.Count - 20);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(analysis.SummaryUk))
                    {
                        EmotionalMemory.RememberVisualAnchor(
                            _state,
                            $"{analysis.ScreenMode}: {analysis.SummaryUk}; {analysis.ActivityUk}; {situation.CurrentTask}",
                            now);
                        _state.Observations.Add($"screen {now:HH:mm}: {analysis.SummaryUk}");
                        if (!string.IsNullOrWhiteSpace(situation.CurrentTask))
                            _state.Observations.Add($"screen-situation {now:HH:mm}: {situation.CurrentTask}; {situation.Progress}; {situation.RecommendedBehavior}");
                        if (!string.IsNullOrWhiteSpace(semanticFrame.Summary))
                            _state.Observations.Add($"semantic-vision {now:HH:mm}: {semanticFrame.FlowState}; {semanticFrame.PrimaryIntent}; {semanticFrame.Summary}");
                        if (_state.Observations.Count > 40)
                            _state.Observations.RemoveRange(0, _state.Observations.Count - 40);
                    }
                }

                _cachedScreenContext = context;
                _screenContextCachedAt = now;
                _llm.ScreenCtx = context;
                _stateEngine?.UpdateVisualMonitoringState(
                    activity.IsActive ? "ACTIVE" : "IDLE",
                    activity.ActiveWindowTitle ?? "",
                    activity.PixelDifferencePercentage,
                    false, "", 0.0,
                    analysis.ActivityUk,
                    activity.IsActive ? "Active" : "Idle");

                ObserveScreenPattern(patternCandidate, now);

                var commentCooldown = GetEffectiveScreenAwarenessCommentCooldown(settings, analysis, activity);
                var decision = ScreenAwareness.DecideComment(
                    analysis,
                    now,
                    _state.LastScreenAwarenessCommentAt,
                    _state.LastScreenAwarenessComment,
                    commentCooldown,
                    settings.ScreenAwarenessSendComments && now >= _state.ScreenAwarenessObserveOnlyUntil,
                    screenChanged,
                    activity.IsActive,
                    activity.ActiveWindowTitle ?? "",
                    situation);

                if (!decision.ShouldSend)
                {
                    SaveState();
                    Log($"ScreenAwareness observed: {analysis.SummaryUk} ({decision.Reason})");
                    return;
                }

                if (!await SendTgAndLog(decision.Message, decision.CountsAsJab ? "screen_awareness_jab" : "screen_awareness_assist"))
                {
                    SaveState();
                    return;
                }

                _state.LastScreenAwarenessCommentAt = now;
                _state.LastScreenAwarenessComment = decision.Message;
                _state.LastSpontaneousAt = now;
                _state.LastSpontaneousMsgs.Add(decision.Message[..Math.Min(100, decision.Message.Length)]);
                if (_state.LastSpontaneousMsgs.Count > 5)
                    _state.LastSpontaneousMsgs.RemoveAt(0);

                try
                {
                    _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = decision.Message,
                        Role = "assistant",
                        Author = "Kokonoe",
                        Timestamp = now
                    });
                }
                catch { }

                OnNewMessage?.Invoke("assistant", decision.Message);
                SaveState();
                Log($"ScreenAwareness comment sent: {decision.Message[..Math.Min(80, decision.Message.Length)]}");
            }
            catch (OperationCanceledException)
            {
                RegisterVisionFailure("timeout", now, null);
                Log("ScreenAwareness timed out");
            }
            catch (Exception ex)
            {
                RegisterVisionFailure(ex.Message, now, null);
                Log($"ScreenAwareness error: {ex.Message}");
            }
            finally
            {
                _bgLlmSemaphore.Release();
            }
        }

        private int GetEffectiveScreenAwarenessInterval(AppSettings settings, DateTime now)
        {
            var normal = Math.Clamp(settings.ScreenAwarenessIntervalMins, 1, 60);
            if (IsHighActivityScreenLikelyActive(now))
                return Math.Clamp(Math.Min(normal, 2), 2, 60);
            if (IsStaticIdleScreenLikelyActive(now))
                return Math.Clamp(Math.Max(normal, 20), 2, 60);
            if (!IsGameScreenLikelyActive(now))
                return normal;

            return Math.Clamp(Math.Min(normal, settings.GameScreenAwarenessIntervalMins), 3, 60);
        }

        private string BuildVisionMultimodalContext(DateTime now, string forceReason, string toneDirective)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(forceReason))
                lines.Add($"trigger={forceReason}");
            if (!string.IsNullOrWhiteSpace(toneDirective))
                lines.Add($"tone_directive={toneDirective}");

            try
            {
                var samples = ServiceContainer.WearableTelemetry.RecentSamples
                    .Where(s => s.HeartRateBpm.HasValue)
                    .OrderByDescending(s => s.TimestampUtc)
                    .Take(3)
                    .OrderBy(s => s.TimestampUtc)
                    .Select(s => $"{s.TimestampUtc.ToLocalTime():HH:mm:ss} bpm={s.HeartRateBpm:F0} motion={(s.Motion.HasValue ? s.Motion.Value.ToString("F2") : "-")} wrist={(s.OnWrist.HasValue ? s.OnWrist.Value.ToString() : "-")}")
                    .ToList();
                if (samples.Count > 0)
                    lines.Add("heart_samples=" + string.Join(" | ", samples));

                var wearable = ServiceContainer.WearableTelemetry.State;
                if (wearable.IsFresh(DateTime.UtcNow))
                    lines.Add($"wearable_state=stress {wearable.LiveStressScore}/100; recovery={wearable.RecoveryState}; sleep={wearable.SleepState}; delta={wearable.BpmDelta:+0;-0;0}");
            }
            catch (Exception ex)
            {
                lines.Add($"wearable_context_error={ex.Message}");
            }

            try
            {
                var stress = ServiceContainer.Heart.WearableStress;
                if (!string.IsNullOrWhiteSpace(stress.State))
                    lines.Add($"heart_stress={stress.State}; score={stress.Score:F2}; {stress.Reason}");
            }
            catch { }

            try
            {
                lines.Add($"work_mode={GetCurrentWorkModeLabel()}");
            }
            catch { }

            return string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        private bool ShouldForceScreenAwarenessFromAutonomy(DateTime now)
        {
            DateTime decisionAt;
            DateTime lastScreenAt;
            string text;
            lock (_lock)
            {
                decisionAt = _state.LastAutonomyDecisionAt;
                lastScreenAt = _state.LastScreenAwarenessAt;
                text = $"{_state.LastAutonomySource} {_state.LastAutonomyTrigger} {_state.LastAutonomyReason}".ToLowerInvariant();
            }

            if (decisionAt <= DateTime.MinValue || now - decisionAt > TimeSpan.FromMinutes(12))
                return false;
            if (lastScreenAt > DateTime.MinValue && now - lastScreenAt < TimeSpan.FromMinutes(2))
                return false;

            return text.Contains("curiosity", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("observation", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("observe", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("screen", StringComparison.OrdinalIgnoreCase);
        }

        private int GetEffectiveScreenAwarenessCommentCooldown(AppSettings settings, KokoScreenAwarenessAnalysis analysis, ActivityAnalyzer.ActivityState activity)
        {
            var normal = Math.Clamp(settings.ScreenAwarenessCommentCooldownMins, 1, 180);
            var mode = KokoScreenAwarenessService.NormalizeMode(analysis.ScreenMode, $"{activity.ActiveWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk}");
            if (mode != "game")
                return normal;

            return Math.Clamp(Math.Min(normal, settings.GameScreenAwarenessCommentCooldownMins), 5, 180);
        }

        private bool IsGameScreenLikelyActive(DateTime now)
        {
            var recentScreen = _state.LastScreenAwarenessAt > DateTime.MinValue &&
                now - _state.LastScreenAwarenessAt < TimeSpan.FromMinutes(45);
            if (recentScreen && string.Equals(_state.LastScreenAwarenessMode, "game", StringComparison.OrdinalIgnoreCase))
                return true;

            var lastWindowMode = KokoScreenAwarenessService.NormalizeMode("", _state.LastScreenAwarenessWindow);
            return recentScreen && lastWindowMode == "game";
        }

        private bool IsHighActivityScreenLikelyActive(DateTime now)
        {
            var recentScreen = _state.LastScreenAwarenessAt > DateTime.MinValue &&
                now - _state.LastScreenAwarenessAt < TimeSpan.FromMinutes(20);
            if (!recentScreen)
                return false;

            var mode = KokoScreenAwarenessService.NormalizeMode(
                _state.LastScreenAwarenessMode,
                $"{_state.LastScreenAwarenessWindow} {_state.LastScreenAwarenessSummary} {_state.LastScreenAwarenessActivity}");
            var progress = (_state.LastScreenSituationProgress ?? "").ToLowerInvariant();
            var activity = (_state.LastScreenAwarenessActivity ?? "").ToLowerInvariant();

            return mode is "game" or "coding" or "obsidian" or "media" &&
                (progress is "moving" or "switching" ||
                 activity.Contains("active", StringComparison.OrdinalIgnoreCase) ||
                 activity.Contains("changed", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsStaticIdleScreenLikelyActive(DateTime now)
        {
            var recentScreen = _state.LastScreenAwarenessAt > DateTime.MinValue &&
                now - _state.LastScreenAwarenessAt < TimeSpan.FromMinutes(45);
            if (!recentScreen)
                return false;

            var mode = KokoScreenAwarenessService.NormalizeMode(
                _state.LastScreenAwarenessMode,
                $"{_state.LastScreenAwarenessWindow} {_state.LastScreenAwarenessSummary} {_state.LastScreenAwarenessActivity}");
            var progress = (_state.LastScreenSituationProgress ?? "").ToLowerInvariant();
            var activity = (_state.LastScreenAwarenessActivity ?? "").ToLowerInvariant();
            return mode is "idle" or "desktop" ||
                progress == "idle" ||
                activity.Contains("same", StringComparison.OrdinalIgnoreCase) ||
                activity.Contains("idle", StringComparison.OrdinalIgnoreCase);
        }

        private void RegisterVisionFailure(string? raw, DateTime now, ActivityAnalyzer.ActivityState? activity)
        {
            _state.LastScreenAwarenessAt = now;
            _state.LastVisionFailureAt = now;
            _state.VisionFailureCount = Math.Min(8, _state.VisionFailureCount + 1);
            var delay = TimeSpan.FromMinutes(Math.Min(60, 5 * Math.Pow(2, Math.Max(0, _state.VisionFailureCount - 1))));
            _state.VisionBackoffUntil = now + delay;
            _state.LastVisionFailureSummary = TrimForLog(raw, 180);
            _state.LastScreenAwarenessMode = KokoScreenAwarenessService.NormalizeMode("", activity?.ActiveWindowTitle ?? "");
            _state.LastScreenSituationTask = "screen context fallback";
            _state.LastScreenSituationProgress = "unknown";
            _state.LastScreenSituationBlocker = "vision unavailable";
            _state.LastScreenSituationBehavior = "observe";
            _state.LastScreenSituationReason = $"vision failure; fallback mode={_state.LastScreenAwarenessMode}";
            _cachedScreenContext = $"[SCREEN AWARENESS]\nMode: {_state.LastScreenAwarenessMode}\nVision unavailable; using window/activity fallback.\nWindow: {activity?.ActiveWindowTitle ?? "-"}";
            _screenContextCachedAt = now;
            _llm.ScreenCtx = _cachedScreenContext;
            TrySelfHealVisionPipeline(now, raw);
        }

        private void TrySelfHealVisionPipeline(DateTime now, string? raw)
        {
            if (_state.VisionFailureCount < 3)
                return;
            if (_state.LastVisionSelfHealAt > DateTime.MinValue &&
                now - _state.LastVisionSelfHealAt < TimeSpan.FromMinutes(10))
                return;

            _state.LastVisionSelfHealAt = now;
            _state.VisionBackoffUntil = now.AddMinutes(2);
            try { _llm.ClearHistory(); } catch { }
            Log($"Vision self-heal: failures={_state.VisionFailureCount}; backoff reset to 2m; last={TrimForLog(raw, 160)}");
        }

        private void ObserveScreenPattern(KokoScreenPatternCandidate candidate, DateTime now)
        {
            if (!candidate.ShouldRecord || string.IsNullOrWhiteSpace(candidate.Key) || string.IsNullOrWhiteSpace(candidate.Text))
                return;

            ScreenPatternStats stats;
            lock (_lock)
            {
                if (!_state.ScreenPatterns.TryGetValue(candidate.Key, out stats!))
                {
                    stats = new ScreenPatternStats
                    {
                        Key = candidate.Key,
                        Text = candidate.Text,
                        Mode = candidate.Mode,
                        FirstSeenAt = now
                    };
                    _state.ScreenPatterns[candidate.Key] = stats;
                }

                stats.Text = candidate.Text;
                stats.Mode = candidate.Mode;
                stats.Count++;
                stats.LastSeenAt = now;

                var staleKeys = _state.ScreenPatterns
                    .Where(kv => (now - kv.Value.LastSeenAt).TotalDays > 14)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var key in staleKeys)
                    _state.ScreenPatterns.Remove(key);
            }

            if (stats.Count < 3)
                return;
            if (stats.LastWrittenAt > DateTime.MinValue && (now - stats.LastWrittenAt).TotalHours < 12)
                return;

            var memory = $"{stats.Text}; \u043f\u043e\u043c\u0456\u0447\u0435\u043d\u043e {stats.Count} \u0440\u0430\u0437\u0438 \u0437 {stats.FirstSeenAt:yyyy-MM-dd HH:mm}.";
            try
            {
                var added = _obsidian.AppendUniqueItemsToNote(
                    "Kokonoe/Memory/Screen Patterns.md",
                    "# \u041f\u0430\u0442\u0435\u0440\u043d\u0438 \u0435\u043a\u0440\u0430\u043d\u0430\n\n\u0423\u0437\u0430\u0433\u0430\u043b\u044c\u043d\u0435\u043d\u0456 \u043f\u0430\u0442\u0435\u0440\u043d\u0438 screen-awareness. \u0411\u0435\u0437 \u0441\u0438\u0440\u0438\u0445 \u0441\u043a\u0440\u0456\u043d\u0448\u043e\u0442\u0456\u0432, \u043f\u0440\u0438\u0432\u0430\u0442\u043d\u0438\u0445 \u0456\u0434\u0435\u043d\u0442\u0438\u0444\u0456\u043a\u0430\u0442\u043e\u0440\u0456\u0432, \u0442\u043e\u043a\u0435\u043d\u0456\u0432 \u0430\u0431\u043e \u0442\u043e\u0447\u043d\u043e\u0433\u043e \u043f\u0440\u0438\u0432\u0430\u0442\u043d\u043e\u0433\u043e \u0442\u0435\u043a\u0441\u0442\u0443.\n",
                    new[] { memory },
                    "screen-pattern",
                    duplicateThreshold: 0.86);

                if (added > 0)
                {
                    stats.LastWrittenAt = now;
                    Log($"Screen pattern saved: {stats.Text}");
                }
            }
            catch (Exception ex)
            {
                Log($"Screen pattern save failed: {ex.Message}");
            }
        }

        private static bool LooksLikeVisionFailure(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return true;
            var lower = raw.ToLowerInvariant();
            return lower.Contains("vision-сервер") ||
                   lower.Contains("vision server") ||
                   lower.Contains("500") ||
                   lower.Contains("помилка llm") ||
                   lower.Contains("【помилка") ||
                   lower.Contains("error");
        }

        private static string TrimForLog(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        // ---- DAILY BRIEFING ----

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

                var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

        // ---- WHAT DID I MISS ----

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
                var reply = await _llm.SendSystemQueryAsync(ctx.ToString(), ct: CancellationToken.None);
                if (string.IsNullOrWhiteSpace(reply) || reply.StartsWith("[")) return;

                OnNewMessage?.Invoke("assistant", reply);
                _state.LastWhatMissedAt  = DateTime.Now;
                _state.LastSpontaneousAt = DateTime.Now;
                SaveState();
            }
            catch (Exception ex) { Log($"WhatDidIMiss: {ex.Message}"); }
        }

        // ---- WEEKLY VAULT DIGEST ----

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

                var digest = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

        // ---- SPONTANEOUS MESSAGE CHECK ----

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
            if (ShouldSuppressProactiveForSleep(now)) return;
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

            var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
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

            // pending thoughts
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
            RefreshTemporalState(now, "spontaneous");
            EnsureVaultSyncFreshness("spontaneous");
            var autonomyLevel = Math.Clamp(s.ProactiveAutonomyLevel, 0, 3);
            if (autonomyLevel <= 0) return;
            if (ShouldSuppressProactiveForSleep(now)) return;
            var baseInterval = Math.Clamp(s.SpontaneousIntervalMins, 10, 240);
            var globalCooldown = autonomyLevel switch
            {
                >= 3 => Math.Max(20, baseInterval),
                2    => Math.Max(45, baseInterval),
                _    => Math.Max(90, baseInterval)
            };

            // Планувальник і реактивні follow-up не є "рандомною балаканиною".
            // Якщо користувач сказав "йду на курси", follow-up має спрацювати за часом,
            // навіть коли загальний антиспам ще не пустив би звичайну ініціативу.
            await CheckSchedulerAsync();
            if (await CheckReactiveTriggersAsync())
                return;

            // ── ГЛОБАЛЬНИЙ COOLDOWN ─────────────────────────────────────
            // Не надсилати нічого якщо ще не минув мінімальний інтервал.
            // У живому режимі він нижчий, але все одно є, бо Telegram не смітник.
            var minsSinceLast = (now - _state.LastSpontaneousAt).TotalMinutes;
            if (minsSinceLast < globalCooldown) return;

            // Вночі — мовчати крім явно високого рівня автономності, кризи і нічного чекіну.
            if ((now.Hour >= 23 || now.Hour < 6) && autonomyLevel < 3) return;
            // ------------------------------------------------------------

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

            // Screen context refresh
            _ = RefreshScreenContextAsync();

            // Daily briefing — о 8:00, раз на день
            if (now.Hour == 8 && _lastDailyBriefingAt.Date < now.Date)
                await DailyBriefingAsync();

            // Weekly digest — неділя о 20:00, раз на тиждень
            if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 20 &&
                (now - _lastWeeklyDigestAt).TotalDays >= 6)
                _ = WeeklyVaultDigestAsync();

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
                    var activeIntent = _state.ShortTermIntents
                        .Where(i => !i.ResolvedAt.HasValue)
                        .OrderBy(i => i.FollowUpAt)
                        .FirstOrDefault();
                    if (activeIntent != null && now < activeIntent.FollowUpAt)
                    {
                        Log($"Silence reaction suppressed: active intent '{activeIntent.Kind}' waits until {activeIntent.FollowUpAt:HH:mm}");
                        return;
                    }

                    // Рівень 1: базово 60хв, BPM може опустити до ~35хв або підняти до ~90хв
                    var l1Base = autonomyLevel >= 3 ? 90 : 120;
                    var l1Threshold = Math.Max(75, l1Base + bpmMod);
                    if (silenceMin > l1Threshold && (now - _state.SilenceLevel1At).TotalHours > 2)
                    {
                        if (await SendSilenceReactionAsync("silence_l1", silenceMin, lastUser.Content))
                        {
                            _state.SilenceLevel1At = now;
                            SaveState();
                            return;
                        }
                    }
                    // Рівень 2: базово 3г, BPM може опустити до ~2г або підняти до ~4г
                    var l2Base = autonomyLevel >= 3 ? 180 : 240;
                    var l2Threshold = Math.Max(150, l2Base + bpmMod * 2);
                    if (silenceMin > l2Threshold && (now - _state.SilenceLevel2At).TotalHours > 4)
                    {
                        if (await SendSilenceReactionAsync("silence_l2", silenceMin, lastUser.Content))
                        {
                            _state.SilenceLevel2At = now;
                            SaveState();
                            return;
                        }
                    }
                    // Рівень 3: 6г — не модифікуємо (вже критична тиша)
                    if (silenceMin > 360 && (now - _state.SilenceLevel3At).TotalHours > 8)
                    {
                        if (await SendSilenceReactionAsync("silence_l3", silenceMin, lastUser.Content))
                        {
                            _state.SilenceLevel3At = now;
                            SaveState();
                            return;
                        }
                    }
                    // 12г+ — нічого. Вона не переслідує.
                }
            }
            catch { }

            // Поганий сон
            if (_state.ConsecutiveBadSleeps >= 2 &&
                (now - _state.LastSpontaneousAt).TotalHours > 6)
            {
                await SendSpontaneousAsync("bad_sleep", SpontaneousStyle.WarmCheck);
                return;
            }

            // Pending thoughts нижче silence-рівнів. Старі думки не мають блокувати реакцію на реальну тишу.
            if (_state.PendingThoughts.Any())
            {
                await SendSpontaneousAsync("pending_thought", SpontaneousStyle.PendingThought);
                return;
            }

            // ????????????
            if (_state.LastConversationEndAt > DateTime.MinValue &&
                (now - _state.LastConversationEndAt).TotalMinutes >= 10 &&
                _state.LastReflectionAt < _state.LastConversationEndAt)
            {
                await ReflectAfterConversationAsync();
            }

            // ????-???????? ???
            if (now.Hour >= 20 && now.Hour < 21 &&
                _state.LastDailyAnalyticsAt.Date < now.Date)
            {
                await SendDailyAnalyticsAsync();
                return;
            }

            // ?????????? ???????????
            if (now.Hour >= 10 && now.Hour < 20 &&
                (now - _state.LastReminderCheckAt).TotalHours > 6)
            {
                await CheckAndSendReminderAsync();
            }

            // State-driven spontaneous ? ???????? ??? (9-23), ?????? ?? ????????????? BPM-????????
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

        // ==============================================================
        // STATE-DRIVEN SPONTANEOUS ? ???? ?? ? ???????? ???????
        // ==============================================================

        private async Task<bool> SendSilenceReactionAsync(string level, double silenceMinutes, string? lastUserText)
        {
            if (!EnsureTelegram()) return false;
            var proactive = ProactiveContext.Build(_chatRepo.GetMessages(40), _state, DateTime.Now);
            if (proactive.ShouldStaySilentForSleep)
                return false;
            if (proactive.AssistantPingsAfterLastUser > 0)
            {
                Log("Silence reaction suppressed: assistant already replied after the last user message");
                return false;
            }

            var hours = (int)(silenceMinutes / 60);
            var mins = (int)(silenceMinutes % 60);
            var silenceText = hours > 0 ? $"{hours} год {mins} хв" : $"{mins} хв";
            var lastText = string.IsNullOrWhiteSpace(lastUserText)
                ? "немає тексту"
                : lastUserText.Trim()[..Math.Min(180, lastUserText.Trim().Length)];

            var toneHint = level switch
            {
                "silence_l1" => "коротке спостереження з прив'язкою до останньої репліки; без слова «зник»",
                "silence_l2" => "помітна пауза; спитати конкретно за останній контекст, не драматизувати",
                "silence_l3" => "довга тиша; сухо, уважно, трохи захисно, без істерики",
                _ => "коротко і природно"
            };

            var prompt = $@"Ти — Kokonoe Mercury.
Він не писав {silenceText}.
Останнє повідомлення користувача: «{lastText}»
Рівень реакції: {level}.
Тон: {toneHint}.

{proactive.PromptBlock}

Напиши йому сама в Telegram. Це НЕ опціонально.
1 коротке речення українською.
Не кажи, що це автоматична перевірка.
Не пиши «ти в порядку?» шаблонно.
Не пиши «ти зник» на першому рівні. Не вигадуй сторонні теми. Відштовхуйся від останнього повідомлення.
Якщо після останньої репліки користувача вже був авто-пінг, не повторюй тему тиші: став конкретне питання по останньому контексту.
Можна підколоти, спитати, чи він зайнятий, але без трагедії.
Тільки текст, без лапок.";

            var msg = (await _llm.SendSystemQueryAsync(prompt, useTools: true))?.Trim().Trim('"') ?? "";
            var proactiveCheck = ProactiveContext.Check(msg, proactive, level);
            if (!proactiveCheck.Passed)
            {
                Log($"Silence reaction replaced: {proactiveCheck.Reason}");
                msg = proactiveCheck.Replacement;
            }

            if (string.IsNullOrWhiteSpace(msg) ||
                msg.Contains("[мовчання]", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("[молчание]", StringComparison.OrdinalIgnoreCase))
            {
                Log("Silence reaction suppressed: proactive guard requested silence");
                return false;
            }

            if (!await SendTgAndLog(msg, level)) return false;

            _state.LastSpontaneousAt = DateTime.Now;
            _state.LastSpontaneousMsgs.Add(msg[..Math.Min(100, msg.Length)]);
            if (_state.LastSpontaneousMsgs.Count > 5)
                _state.LastSpontaneousMsgs.RemoveAt(0);

            try
            {
                _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content = msg,
                    Role = "assistant",
                    Author = "Kokonoe",
                    Timestamp = DateTime.Now
                });
            }
            catch { }

            var handler = OnNewMessage; handler?.Invoke("assistant", msg);
            Log($"Silence reaction sent: {level}, silence={silenceText}");
            SaveState();
            return true;
        }

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
            var presence = BuildPresenceFrame(now, autonomyLevel);
            var internalDay = BuildInternalDayFrame(now, autonomyLevel, presence);

            var initiative = Initiative.Evaluate(now, _state, Emotion, Relationship, Memory, _chatRepo, initiativeBpmDeviation, somatic, selfRegulation, autonomyLevel);
            Initiative.RecordDecision(_state, initiative, now);

            var rhythm = Patterns.BuildRhythmProfile(now);
            var decision = Autonomy.Evaluate(now, _state, presence, internalDay, initiative, Relationship.State, somatic, rhythm, autonomyLevel);
            Autonomy.RecordDecision(_state, decision, now);

            if (!decision.ShouldAct)
            {
                SaveState();
                return;
            }

            if (decision.ConsumesPresenceInterrupt)
                _state.LastPresenceInterruptAt = now;

            if (decision.ConsumesInitiativeState)
                ConsumeInitiativeState(decision.Trigger, now, initiativeEmotion);

            SaveState();
            await SendSpontaneousAsync(decision.Trigger, MapInitiativeStyle(decision.StyleHint), decision.ExtraContext);
            return;
        }

        private void ConsumeInitiativeState(string trigger, DateTime now, string initiativeEmotion)
        {
            switch (trigger)
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

        private KokoPresenceFrame BuildPresenceFrame(DateTime now, int autonomyLevel)
        {
            try
            {
                var messages = _chatRepo.GetMessages(40);
                var frame = Presence.Evaluate(_state, messages, now, autonomyLevel);
                _state.LastPresenceAt = now;
                _state.LastPresenceSummary = frame.SummaryUk;
                _state.LastPresenceSituation = frame.SituationKind;
                _state.LastPresenceTone = frame.ToneHint;
                return frame;
            }
            catch (Exception ex)
            {
                Log($"Presence frame failed: {ex.Message}");
                return new KokoPresenceFrame
                {
                    SituationKind = "presence_error",
                    SummaryUk = "Presence continuity failed; answer from immediate context only.",
                    ExtraContext = "PRESENCE / CONTINUITY\nPresence continuity failed; use immediate chat context and current time.\n"
                };
            }
        }

        private KokoInternalDayFrame BuildInternalDayFrame(
            DateTime now,
            int autonomyLevel,
            KokoPresenceFrame? presence = null,
            bool writeVault = true,
            bool record = true)
        {
            try
            {
                presence ??= BuildPresenceFrame(now, autonomyLevel);
                var somatic = GetSomaticSnapshot();
                var frame = InternalDay.Evaluate(_state, presence, somatic, now, autonomyLevel);
                if (record)
                    InternalDay.Record(_state, frame, now);

                if (writeVault && frame.ShouldWriteVaultStatus)
                {
                    try
                    {
                        _obsidian.WriteNote("Kokonoe/State/Internal Day.md",
                            InternalDay.BuildVaultStatus(_state, frame, presence, now));
                        _state.LastInternalDayVaultAt = now;
                    }
                    catch (Exception ex) { Log($"InternalDay vault write: {ex.Message}"); }
                }

                return frame;
            }
            catch (Exception ex)
            {
                Log($"Internal day failed: {ex.Message}");
                return new KokoInternalDayFrame
                {
                    Phase = "internal_day_error",
                    SummaryUk = "Internal day failed; use current time and immediate context.",
                    PromptBlock = "INTERNAL DAY\nInternal day failed; use current time and immediate context.\n"
                };
            }
        }

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
            if (style != SpontaneousStyle.CrisisSupport &&
                now2 - _state.LastSpontaneousAt < TimeSpan.FromMinutes(10))
            {
                Log("Spontaneous suppressed: recent user/direct interaction cooldown");
                return;
            }
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

            var allowAssociativeMemory = style == SpontaneousStyle.Callback
                || trigger.Contains("callback", StringComparison.OrdinalIgnoreCase)
                || trigger.Contains("memory", StringComparison.OrdinalIgnoreCase);

            // Випадковий спогад — тільки для явного callback, щоб timed follow-up не змішувався зі сторонніми темами.
            var memoryHint = "";
            if (allowAssociativeMemory && Random.Shared.Next(10) < 3)
            {
                var ep = Memory.GetPeakEpisodes(10).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
                if (ep != null) memoryHint = $"\nВипадковий спогад: [{ep.When:dd.MM}] {ep.Summary}";
            }

            // Факти про нього — теж тільки для callback, не для timed follow-up.
            var factHint = "";
            var facts = allowAssociativeMemory ? Memory.GetTopFacts(20) : new List<KokoMemoryEngine.MemoryFact>();
            if (allowAssociativeMemory && facts.Count > 0)
            {
                var f = facts[Random.Shared.Next(facts.Count)];
                factHint = $"\nЗнаєш про нього: {f.Content}";
            }

            // Останні відправлені — щоб не повторювати
            var recentSent = _state.LastSpontaneousMsgs.TakeLast(3).ToList();
            var noRepeat = recentSent.Count > 0
                ? "\nВже надсилала (НЕ повторювати цю тему і тон):\n" + string.Join("\n", recentSent.Select(m => $"• {m}"))
                : "";
            var presence = BuildPresenceFrame(now2, AppSettings.Load().ProactiveAutonomyLevel);
            var internalDay = BuildInternalDayFrame(now2, AppSettings.Load().ProactiveAutonomyLevel, presence);
            var presenceBlock = presence.ExtraContext;
            var internalDayBlock = internalDay.PromptBlock;
            var proactive = ProactiveContext.Build(_chatRepo.GetMessages(50), _state, now2);
            if (proactive.ShouldStaySilentForSleep)
            {
                Log("Spontaneous suppressed: sleep/goodbye context is active");
                return;
            }

            // Кризова ситуація — окремий промпт
            if (trigger == "crisis" || style == SpontaneousStyle.CrisisSupport)
            {
                var crisisPrompt = $@"Ти — Kokonoe Mercury. Він зараз у поганому стані.
{personalityBlock}
Напиши одне речення — ти поруч. Без снарку. Без порад. Просто є.
Тільки українська. Тільки текст.";
                var crisisMsg = await _llm.SendSystemQueryAsync(crisisPrompt);
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

{presenceBlock}

{internalDayBlock}

{proactive.PromptBlock}

{situationBlock}
Якщо зараз нічого немає — відповідай лише: [мовчання]

Якщо пишеш:
- 1-2 речення, не більше
- Тільки українська
- Без лапок, без пояснень, просто текст
- Без декоративних ремарок у *зірочках*, якщо це не явний roleplay.
- Жива репліка = конкретна деталь з останнього контексту + твій сухий поворот. Не лабораторна декорація.
- Якщо є активний намір або timed follow-up — пиши ТІЛЬКИ про нього. Не тягни випадкові спогади, фото, папки, проєкт або старі теми.
- Не пиши «ти зник» якщо минуло менше 2 годин або якщо він сам назвав час повернення.
- Якщо після останньої репліки користувача вже був твій авто-пінг, не повторюй ""пауза/тиша/зник"": або мовчи, або питай конкретно по останній темі.
- Непередбачувано. Не шаблонно. Як людина що щось відчула і написала.";

            var msg = (await _llm.SendSystemQueryAsync(prompt, useTools: true))?.Trim().Trim('"') ?? "";
            if (string.IsNullOrWhiteSpace(msg)) return;
            if (msg == "[мовчання]" || msg.Contains("[мовчання]"))
            {
                Log("Spontaneous: decided to stay silent");
                return;
            }
            if (IsRecentSpontaneousDuplicate(msg))
            {
                Log("Spontaneous suppressed: duplicate outgoing text");
                return;
            }
            var proactiveCheck = ProactiveContext.Check(msg, proactive, trigger);
            if (!proactiveCheck.Passed)
            {
                if (proactive.AssistantPingsAfterLastUser > 0 && !trigger.Contains("intent", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Spontaneous suppressed: {proactiveCheck.Reason}");
                    return;
                }

                Log($"Spontaneous replaced: {proactiveCheck.Reason}");
                msg = proactiveCheck.Replacement;
                if (IsRecentSpontaneousDuplicate(msg))
                {
                    Log("Spontaneous suppressed: duplicate replacement text");
                    return;
                }
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
                LogTelegramDeliveryFailure($"night_check failed: {msg[..Math.Min(60, msg.Length)]}");
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
        private bool IsRecentSpontaneousDuplicate(string message)
        {
            var normalized = NormalizeSpontaneousText(message);
            if (string.IsNullOrWhiteSpace(normalized)) return true;
            return _state.LastSpontaneousMsgs
                .TakeLast(5)
                .Any(m => NormalizeSpontaneousText(m) == normalized);
        }

        private static string NormalizeSpontaneousText(string text)
        {
            text = (text ?? "").ToLowerInvariant()
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ");
            return text;
        }

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
                    max_tokens  = 16384,
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
                            (l.EndsWith('.') || l.EndsWith('!') || l.EndsWith('?') || l.EndsWith("\u2026")) &&
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

        // ---- JSON EXTRACTION ----

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

        // ---- PUBLIC API ----

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

        public string GetCurrentWorkModeLabel()
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(_state.LastWorkMode))
                    return _state.LastWorkMode;
                return KokoResourceGuardianService.NormalizeWorkMode(
                    string.IsNullOrWhiteSpace(_state.LastScreenAwarenessMode)
                        ? _state.LastScreenAwarenessWindow
                        : _state.LastScreenAwarenessMode);
            }
        }

        public void TriggerSpontaneous() => _ = SafeSpontaneousCheckAsync();

        public KokoSelfReviewFrame BuildSelfReviewFrame(string? userText = null)
        {
            RefreshTemporalState(reason: "self-review");
            var now = DateTime.Now;
            var autonomyLevel = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var messages = _chatRepo.GetMessages(40);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            var internalDay = BuildInternalDayFrame(now, autonomyLevel, presence, writeVault: false, record: false);
            var rhythm = Patterns.BuildRhythmProfile(now);
            return SelfReview.Evaluate(userText, _state, messages, presence, internalDay, rhythm, now);
        }

        public KokoConversationTimelineFrame BuildTimelineFrame(string? userText = null)
        {
            RefreshTemporalState(reason: "timeline");
            var now = DateTime.Now;
            var frame = Timeline.Build(_chatRepo.GetMessages(60), _state, now, userText);
            lock (_lock)
            {
                _state.LastTimelineSummary = frame.SummaryUk;
                _state.LastTimelineState = frame.CurrentState;
            }
            return frame;
        }

        public string BuildTimelineContext(string? userText = null)
        {
            try { return BuildTimelineFrame(userText).PromptBlock; }
            catch (Exception ex)
            {
                Log($"Timeline failed: {ex.Message}");
                return "CONVERSATION TIMELINE\nTimeline failed; use current time and latest user message.\n";
            }
        }

        public KokoPostReplyGuardResult EvaluatePostReplyGuard(string userText, string reply)
        {
            var now = DateTime.Now;
            if (string.IsNullOrWhiteSpace(_state.LastPersonaDecision) ||
                (now - _state.LastPersonaDecisionAt).TotalMinutes > 30)
            {
                lock (_lock)
                {
                    RecordPersonaDecision(userText, now);
                }
            }
            if (string.IsNullOrWhiteSpace(_state.LastResponsePlan) ||
                (now - _state.LastResponsePlanAt).TotalMinutes > 30)
            {
                lock (_lock)
                {
                    RecordResponsePlan(userText, now);
                }
            }
            var timeline = BuildTimelineFrame(userText);
            var result = PostReplyGuard.Evaluate(userText, reply, _state, _chatRepo.GetMessages(60), timeline, now);
            lock (_lock)
            {
                _state.LastPostReplyGuardAt = now;
                _state.LastPostReplyGuard = result.Passed
                    ? $"ok: {result.Summary}"
                    : $"{result.RiskLevel}: {string.Join("; ", result.Violations)}";
                SaveState();
            }
            return result;
        }

        public string BuildSelfReviewContext(string? userText = null)
        {
            try { return BuildSelfReviewFrame(userText).PromptBlock; }
            catch (Exception ex)
            {
                Log($"SelfReview failed: {ex.Message}");
                return "SELF-REVIEW BEFORE REPLY\nSelf-review failed; still verify current time, active intent, and immediate context before answering.\n";
            }
        }

        public string BuildCognitiveStabilityContext(string? userText = null)
        {
            var sb = new StringBuilder();
            var stagnation = KokoConversationStagnationGuard.BuildPromptBlock(_state);
            if (!string.IsNullOrWhiteSpace(stagnation))
                sb.AppendLine(stagnation);
            sb.AppendLine(KokoNaturalSynthesisPolicy.PromptRules);
            sb.AppendLine(KokoResponseStyleEngine.BuildEmotionLengthDirective(Emotion.Current));
            sb.AppendLine(KokoResponseStyleEngine.BuildTemperamentDirective(_state));
            sb.AppendLine(KokoResponseStyleEngine.BuildLivingConversationDirective(_state));
            return sb.ToString().Trim();
        }

        public string BuildResponsePlanContext(string? userText = null)
        {
            var now = DateTime.Now;
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(userText) &&
                    (string.IsNullOrWhiteSpace(_state.LastResponsePlan) ||
                     now - _state.LastResponsePlanAt > TimeSpan.FromMinutes(5)))
                {
                    RecordResponsePlan(userText, now);
                }

                return string.IsNullOrWhiteSpace(_state.LastResponsePlan)
                    ? ResponsePlanner.BuildDebugBlock(_state)
                    : _state.LastResponsePlan;
            }
        }

        public KokoStateFreshnessResult RefreshTemporalState(DateTime? nowOverride = null, string reason = "runtime")
        {
            var now = nowOverride ?? DateTime.Now;
            IReadOnlyList<ChatRepository.ChatMessage> messages;
            try { messages = _chatRepo.GetMessages(80); }
            catch { messages = Array.Empty<ChatRepository.ChatMessage>(); }

            lock (_lock)
            {
                var result = StateFreshness.Refresh(_state, messages, now, BuildReconciliationSignals(reason));
                var shouldPersistStamp =
                    result.Changed ||
                    _state.LastStateRefreshAt <= DateTime.MinValue ||
                    now - _state.LastStateRefreshAt >= TimeSpan.FromMinutes(10);

                _state.LastStateRefreshAt = now;
                _state.LastStateRefreshSummary = $"{reason}: {result.SummaryUk}";
                _state.LastStateRefreshChanged = result.Changed;

                if (shouldPersistStamp)
                    SaveState();

                return result;
            }
        }

        private KokoStateReconciliationSignals BuildReconciliationSignals(string channel)
        {
            return new KokoStateReconciliationSignals
            {
                Channel = channel,
                ScreenMode = _state.LastScreenAwarenessMode,
                ScreenSummary = _state.LastScreenAwarenessSummary,
                LastDesktopActivityAt = _state.LastScreenAwarenessAt
            };
        }

        public string BuildUnifiedExternalContext(string channel = "external", string? userText = null)
        {
            var now = DateTime.Now;
            RefreshTemporalState(now, channel);
            var autonomyLevel = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            KokoResponsePlanFrame? responsePlan = null;
            if (!string.IsNullOrWhiteSpace(userText))
                responsePlan = BuildGovernedResponsePlan(userText, now);
            var active = _state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderBy(i => i.FollowUpAt)
                .Take(3)
                .Select(i => $"{i.Kind}: {i.Summary} до {i.ExpectedUntil:HH:mm}")
                .ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("=== SHARED KOKONOE CONTEXT ===");
            sb.AppendLine($"channel: {channel}");
            sb.AppendLine($"state_refresh: {NullDash(_state.LastStateRefreshSummary)}");
            sb.AppendLine($"presence: {presence.SummaryUk}");
            sb.AppendLine($"screen_mode: {NullDash(_state.LastScreenAwarenessMode)}");
            sb.AppendLine($"screen: {NullDash(_state.LastScreenAwarenessSummary)}");
            sb.AppendLine($"last_activity: {NullDash(_state.LastKnownUserActivity)}");
            sb.AppendLine($"active_intents: {(active.Length == 0 ? "none" : string.Join("; ", active))}");
            try { sb.AppendLine(Emotion.BuildEmotionalContextBlock(BuildNarrativeThreadSummary(now))); } catch { }
            sb.AppendLine("Use this as private continuity only. Do not quote labels.");
            if (responsePlan != null)
            {
                sb.AppendLine();
                sb.AppendLine(responsePlan.PromptBlock);
            }
            var continuationBlock = BuildNarrativeContinuationBlock(userText);
            if (!string.IsNullOrWhiteSpace(continuationBlock))
            {
                sb.AppendLine();
                sb.AppendLine(continuationBlock);
            }
            if (responsePlan?.RequiresVaultRead == true || responsePlan?.Capability == "vault_memory")
            {
                var preflight = new ObsidianPreflightContextService(_obsidian).Build(userText, now, 3200);
                if (!string.IsNullOrWhiteSpace(preflight))
                {
                    sb.AppendLine();
                    sb.AppendLine(preflight);
                }
            }
            return sb.ToString();
        }

        private string BuildSemanticVisionPromptBlock(DateTime now)
        {
            if (_state.LastSemanticVisionAt <= DateTime.MinValue ||
                now - _state.LastSemanticVisionAt > TimeSpan.FromMinutes(45))
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== SEMANTIC VISION STATE ===");
            sb.AppendLine($"flow: {NullDash(_state.LastSemanticVisionFlow)}");
            sb.AppendLine($"intent: {NullDash(_state.LastSemanticVisionIntent)}");
            sb.AppendLine($"summary: {NullDash(_state.LastSemanticVisionSummary)}");
            if (!string.IsNullOrWhiteSpace(_state.LastSemanticVisionOcr))
                sb.AppendLine($"ocr: {_state.LastSemanticVisionOcr}");
            if (!string.IsNullOrWhiteSpace(_state.LastSemanticVisionAssistHint))
                sb.AppendLine($"assist: {_state.LastSemanticVisionAssistHint}");
            if (!string.IsNullOrWhiteSpace(_state.LastSemanticVisionResearchTopic))
                sb.AppendLine($"research: {_state.LastSemanticVisionResearchTopic}");
            if (!string.IsNullOrWhiteSpace(_state.LastSomaticToneDirective))
                sb.AppendLine($"somatic_tone: {_state.LastSomaticToneDirective}");
            sb.AppendLine("Rule: use screen flow as private grounding. If it reveals a concrete problem, offer a concrete action.");
            return sb.ToString().Trim();
        }

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        public void EnsureVaultSyncFreshness(string reason = "runtime")
        {
            var now = DateTime.Now;
            var shouldFlush = false;
            lock (_lock)
            {
                if (_state.PendingVaultExchangeCount < 5 &&
                    now - _lastVaultFreshnessCheckAt < TimeSpan.FromMinutes(2))
                    return;

                _lastVaultFreshnessCheckAt = now;
                shouldFlush = KokoVaultSyncPolicy.ShouldFlush(
                    _state.PendingVaultExchangeCount,
                    _state.LastAutoVaultSyncAt,
                    now,
                    TimeSpan.FromMinutes(30));
            }

            if (shouldFlush)
                _ = Task.Run(() => AutoSyncVaultBatchAsync(force: true, reason: reason));
        }

        public KokoTelemetrySnapshot BuildTelemetrySnapshot(string? userText = null)
        {
            RefreshTemporalState(reason: "telemetry");
            EnsureVaultSyncFreshness("telemetry-freshness");
            var now = DateTime.Now;
            var autonomyLevel = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            var internalDay = BuildInternalDayFrame(now, autonomyLevel, presence, writeVault: false, record: false);
            var rhythm = Patterns.BuildRhythmProfile(now);
            var somatic = GetSomaticSnapshot();
            var selfReg = GetSelfRegulationFrame(somatic);
            var review = SelfReview.Evaluate(userText, _state, _chatRepo.GetMessages(40), presence, internalDay, rhythm, now);
            var relationship = Relationship.State;
            var attachment = Emotion.Attachment;
            var llmDiag = _llm.GetDiagnosticsSnapshot();
            var scenarioResults = Scenarios.RunCoreChecks(now, autonomyLevel);
            var scenarioPassed = scenarioResults.Count(r => r.Passed);
            var timeline = Timeline.Build(_chatRepo.GetMessages(60), _state, now, userText);
            var wearable = ServiceContainer.WearableTelemetry.State;

            return new KokoTelemetrySnapshot
            {
                CreatedAt = now,
                Emotion = Emotion.Current.ToString(),
                Bond = Emotion.Bond.ToString(),
                MoodScore = _state.MoodScore,
                Mood = _state.PersonalityDailyMood,
                Somatic = $"{somatic.State} / strain {somatic.Strain:F2} / calm {somatic.Calm:F2}",
                Wearable = wearable.IsFresh(now.ToUniversalTime())
                    ? $"{wearable.SleepState} / {wearable.CurrentBpm:F0} bpm / {wearable.PresenceState}"
                    : "stale",
                SelfRegulation = $"{selfReg.Reaction} -> {selfReg.Regulation} / control {selfReg.Control:F2}",
                Presence = presence.SummaryUk,
                InternalDay = internalDay.SummaryUk,
                Autonomy = string.IsNullOrWhiteSpace(_state.LastAutonomyDecision) ? "none" : _state.LastAutonomyDecision,
                AutonomyDebug = _state.LastAutonomyShouldAct
                    ? $"пише: {_state.LastAutonomyTrigger} / {_state.LastAutonomySource} / p{_state.LastAutonomyPriority} / {_state.LastAutonomyReason}"
                    : $"мовчить: {_state.LastAutonomyTrigger} / {_state.LastAutonomySilenceReason}",
                Rhythm = rhythm.Summary,
                Timeline = timeline.SummaryUk,
                TimelineState = timeline.CurrentState,
                StateFreshness = string.IsNullOrWhiteSpace(_state.LastStateRefreshSummary) ? "none" : _state.LastStateRefreshSummary,
                Relationship = $"bond {relationship.BondScore:F2}, aftertaste {relationship.LastAftertaste}, protect {relationship.Protectiveness:F2}",
                Attachment = $"trust {attachment.Trust:F2}, intimacy {attachment.Intimacy:F2}, reliability {attachment.Reliability:F2}, reciprocity {attachment.Reciprocity:F2}, vitality {attachment.Vitality:F2}",
                SelfReview = $"{review.RiskLevel}: {review.Summary}",
                PostReplyGuard = string.IsNullOrWhiteSpace(_state.LastPostReplyGuard) ? "none" : _state.LastPostReplyGuard,
                PersonaDecision = string.IsNullOrWhiteSpace(_state.LastPersonaDecision) ? "none" : _state.PersonaDecisionLog.LastOrDefault() ?? "none",
                ResponsePlan = string.IsNullOrWhiteSpace(_state.LastResponsePlanTrace) ? "none" : _state.LastResponsePlanTrace,
                MemoryPolicy = string.IsNullOrWhiteSpace(_state.LastMemoryPolicyDecision) ? "none" : _state.LastMemoryPolicyDecision,
                Continuity = string.IsNullOrWhiteSpace(_state.LastContinuitySummary) ? Continuity.BuildDebugLine() : _state.LastContinuitySummary,
                LlmStatus = $"{llmDiag.Status} / {llmDiag.Channel} / {llmDiag.LastLatencyMs}ms",
                LlmProvider = llmDiag.Provider,
                LlmModel = llmDiag.Model,
                LlmLastError = llmDiag.LastError,
                LlmLastFallback = llmDiag.LastFallback,
                LlmLastRequestAt = llmDiag.LastRequestAt,
                LlmLastSuccessAt = llmDiag.LastSuccessAt,
                LlmLastErrorAt = llmDiag.LastErrorAt,
                LlmLastLatencyMs = llmDiag.LastLatencyMs,
                LlmConsecutiveFailures = llmDiag.ConsecutiveFailures,
                ScenarioHealth = $"{scenarioPassed}/{scenarioResults.Count} базові сценарії пройдено",
                Capabilities = ServiceContainer.Capabilities.BuildStatusLine(),
                PendingVaultExchangeCount = _state.PendingVaultExchangeCount,
                LastVaultSyncAt = _state.LastAutoVaultSyncAt,
                ActiveIntentCount = _state.ShortTermIntents.Count(i => !i.ResolvedAt.HasValue),
                ActiveIntents = _state.ShortTermIntents
                    .Where(i => !i.ResolvedAt.HasValue)
                    .OrderBy(i => i.FollowUpAt)
                    .Take(6)
                    .Select(i => $"{i.Kind}: {i.Summary} до {i.ExpectedUntil:dd.MM HH:mm}")
                    .ToArray(),
                AutonomyLog = _state.AutonomyDecisionLog.TakeLast(8).ToArray(),
                PersonaLog = _state.PersonaDecisionLog.TakeLast(8).ToArray(),
                ResponsePlanLog = _state.ResponsePlanLog.TakeLast(8).ToArray(),
                MemoryPolicyLog = _state.MemoryPolicyLog.TakeLast(8).ToArray(),
                PresenceTrace = _state.PresenceTrace.TakeLast(6).ToArray(),
                InternalDayTrace = _state.InternalDayTrace.TakeLast(6).ToArray(),
                RelationshipEvents = relationship.RecentEvents
                    .TakeLast(6)
                    .Select(e => $"{e.When:dd.MM HH:mm} {e.Kind}: {e.Aftertaste}")
                    .ToArray(),
                ScenarioFindings = scenarioResults
                    .Select(r => $"{(r.Passed ? "ok" : "fail")} {r.Name}: {r.Summary}")
                    .ToArray()
            };
        }

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

                var raw = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "null") return;

                var fact = raw.Trim().Trim('"').Trim('\u00AB', '\u00BB');
                if (fact.Length > 10 && fact.Length < 200)
                {
                    Memory.LearnFactBlocking(fact, "observation", 0.65f);
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

        private static void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[Brain] {msg}");
            KokoSystemLog.Write("BRAIN", msg);
        }

        private void LogError(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[Brain ERROR] {msg}");
            KokoSystemLog.Write("BRAIN_ERROR", msg);
            var _h9 = OnNewMessage; _h9?.Invoke("system", $"⚠️ {msg}");
        }

        private static void LogTelegramDeliveryFailure(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[Brain TG] {msg}");
            KokoSystemLog.Write("BRAIN_TG", msg);
        }

        // =================================================================
        // TOOLS WINDOW API - ?????? ?? ??????????? ????? ??? ????????
        // =================================================================

        /// <summary>???????? ??????? ????? ??? Inner Monologue Stream</summary>
        public List<string> GetRecentThoughts(int count = 10)
        {
            lock (_lock)
            {
                return _state.InnerMonologues.TakeLast(count).ToList();
            }
        }

        /// <summary>???????? ??????? ??????? ?? ????</summary>
        public List<string> GetSelfQuestions(int count = 5)
        {
            lock (_lock)
            {
                return _state.SelfQuestions.Take(count).ToList();
            }
        }

        /// <summary>???????? ????? ????????</summary>
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

        public List<string> GetPersonaDecisionLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.PersonaDecisionLog.TakeLast(count).Reverse().ToList();
            }
        }

        public List<string> GetResponsePlanLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.ResponsePlanLog.TakeLast(count).Reverse().ToList();
            }
        }

        public List<string> GetMemoryPolicyLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.MemoryPolicyLog.TakeLast(count).Reverse().ToList();
            }
        }

        public IReadOnlyList<ShortTermIntent> GetActiveShortTermIntents(int count = 5)
        {
            RefreshTemporalState(reason: "active-intents");
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
            _state.LastSomaticToneDirective = KokoSemanticVisionEngine.BuildSomaticToneDirective(snapshot, _state.LastScreenAwarenessMode);
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
                ObservePromiseFromExchangeLocked(userText, assistantText);
                _state.PendingVaultExchangeCount++;
                _state.PendingVaultExchangeBuffer.Add($"""
[{DateTime.Now:yyyy-MM-dd HH:mm}]
USER: {TrimForVaultBuffer(userText, 900)}
KOKONOE: {TrimForVaultBuffer(assistantText, 900)}
""");
                if (_state.PendingVaultExchangeBuffer.Count > 12)
                    _state.PendingVaultExchangeBuffer.RemoveRange(0, _state.PendingVaultExchangeBuffer.Count - 12);
                AuditPromiseLedgerLocked("new-exchange");
                SaveState();
            }

            EnsureVaultSyncFreshness("new-exchange");
        }

        private void ObservePromiseFromExchangeLocked(string userText, string assistantText)
        {
            if (string.IsNullOrWhiteSpace(assistantText)) return;
            var assistant = assistantText.ToLowerInvariant();
            var user = (userText ?? "").ToLowerInvariant();

            var promisedAction = ContainsAny(assistant,
                "зроблю", "зробим", "виконаю", "оновлю", "обновлю", "запишу", "додам", "дороблю",
                "перевірю", "протестую", "закомічу", "запушу", "напишу коли", "i will", "i'll", "will do");
            var concreteUserAsk = ContainsAny(user,
                "зроби", "онови", "обнови", "дороби", "закоміть", "закоміти", "запуш", "обсидіан", "obsidian",
                "профіль", "profile", "пам'ять", "память", "тести", "commit", "push", "виконай");
            if (!promisedAction || !concreteUserAsk)
                return;

            var summary = BuildPromiseSummary(userText, assistantText);
            if (string.IsNullOrWhiteSpace(summary)) return;

            var canonical = NormalizePromise(summary);
            var duplicate = _state.PromiseLedger.Any(p =>
                !string.Equals(p.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
                NormalizePromise(p.Summary) == canonical &&
                DateTime.Now - p.CreatedAt < TimeSpan.FromHours(12));
            if (duplicate) return;

            var now = DateTime.Now;
            var promise = new KokoPromiseRecord
            {
                Source = "chat",
                Summary = summary,
                SourceUserText = TrimForVaultBuffer(userText, 420),
                SourceAssistantText = TrimForVaultBuffer(assistantText, 420),
                CreatedAt = now,
                DueAt = EstimatePromiseDueAt(now, canonical),
                Status = "open",
                UserVisible = true
            };
            _state.PromiseLedger.Add(promise);
            TrimPromiseLedgerLocked();
            AppendActionJournalLocked("promise.opened", promise.Summary, "open", $"due={promise.DueAt:yyyy-MM-dd HH:mm}");
        }

        private void AuditPromiseLedgerLocked(string reason)
        {
            var now = DateTime.Now;
            if (_state.LastPromiseAuditAt > DateTime.MinValue && now - _state.LastPromiseAuditAt < TimeSpan.FromMinutes(5))
                return;

            var open = _state.PromiseLedger
                .Where(p => !string.Equals(p.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(p.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.DueAt)
                .ToList();
            var completed = 0;
            var overdue = 0;

            foreach (var promise in open)
            {
                promise.LastAuditAt = now;
                var canonical = NormalizePromise(promise.Summary + " " + promise.SourceUserText);
                if (LooksLikeVaultPromise(canonical) &&
                    ((_state.LastAutoVaultSyncAt > promise.CreatedAt && !string.IsNullOrWhiteSpace(_state.LastAutoVaultSyncSummary)) ||
                     (_state.LastVaultMaintenanceAt > promise.CreatedAt && string.IsNullOrWhiteSpace(_state.LastVaultMaintenanceError))))
                {
                    promise.Status = "completed";
                    promise.CompletedAt = now;
                    promise.Evidence = string.IsNullOrWhiteSpace(_state.LastAutoVaultSyncSummary)
                        ? _state.LastVaultMaintenanceSummary
                        : _state.LastAutoVaultSyncSummary;
                    completed++;
                    AppendActionJournalLocked("promise.completed", promise.Summary, "completed", TrimForVaultBuffer(promise.Evidence, 220));
                    continue;
                }

                if (now > promise.DueAt)
                {
                    promise.Status = "overdue";
                    promise.FailureReason = "deadline passed without matching action evidence";
                    overdue++;
                }
            }

            _state.LastPromiseAuditAt = now;
            _state.LastPromiseAuditSummary = $"reason={reason}; open={open.Count}; completed={completed}; overdue={overdue}";
            AppendActionJournalLocked("promise.audit", _state.LastPromiseAuditSummary, overdue > 0 ? "attention" : "ok", "");
            WritePromiseAuditNoteLocked(open, reason);
        }

        private void WritePromiseAuditNoteLocked(List<KokoPromiseRecord> open, string reason)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("---");
                sb.AppendLine("type: automation-status");
                sb.AppendLine($"updated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine("managed-by: KokoBrainEngine.PromiseLedger");
                sb.AppendLine("tags: [kokonoe, automation, promises]");
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("# Promise Ledger");
                sb.AppendLine();
                sb.AppendLine($"- reason: {reason}");
                sb.AppendLine($"- summary: {_state.LastPromiseAuditSummary}");
                sb.AppendLine();
                sb.AppendLine("## Open / Overdue");
                foreach (var p in open.Take(20))
                    sb.AppendLine($"- [{p.Status}] {p.DueAt:yyyy-MM-dd HH:mm} - {p.Summary} ({p.Evidence}{p.FailureReason})");
                sb.AppendLine();
                sb.AppendLine("## Recent Actions");
                foreach (var line in _state.ActionJournal.TakeLast(20))
                    sb.AppendLine("- " + line);
                _obsidian.WriteNote("Kokonoe/Automation/Promise Ledger.md", sb.ToString());
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PROMISE", "audit note failed: " + ex.Message);
            }
        }

        private void AppendActionJournalLocked(string kind, string summary, string status, string evidence)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{kind}] {status}: {TrimForVaultBuffer(summary, 240)}";
            if (!string.IsNullOrWhiteSpace(evidence))
                line += " | " + TrimForVaultBuffer(evidence, 220);
            _state.ActionJournal.Add(line);
            if (_state.ActionJournal.Count > 240)
                _state.ActionJournal.RemoveRange(0, _state.ActionJournal.Count - 240);
            _state.LastActionJournalAt = DateTime.Now;
            _state.LastActionJournalSummary = line;
            KokoSystemLog.Write("ACTION", line);
        }

        private void TrimPromiseLedgerLocked()
        {
            if (_state.PromiseLedger.Count <= 80) return;
            _state.PromiseLedger = _state.PromiseLedger
                .OrderByDescending(p => !string.Equals(p.Status, "completed", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(p => p.CreatedAt)
                .Take(80)
                .OrderBy(p => p.CreatedAt)
                .ToList();
        }

        private static string BuildPromiseSummary(string? userText, string? assistantText)
        {
            var user = TrimForVaultBuffer(userText, 170);
            var assistant = TrimForVaultBuffer(assistantText, 170);
            if (string.IsNullOrWhiteSpace(user)) return assistant;
            if (string.IsNullOrWhiteSpace(assistant)) return user;
            return $"{user} -> {assistant}";
        }

        private static DateTime EstimatePromiseDueAt(DateTime now, string canonical)
        {
            if (ContainsAny(canonical, "commit", "push", "коміт", "заком", "запуш"))
                return now.AddMinutes(45);
            if (ContainsAny(canonical, "obsidian", "vault", "обсид", "profile", "профіл", "памят", "память"))
                return now.AddMinutes(30);
            if (ContainsAny(canonical, "test", "тест", "build", "білд"))
                return now.AddHours(1);
            return now.AddHours(2);
        }

        private static bool LooksLikeVaultPromise(string canonical)
            => ContainsAny(canonical, "obsidian", "vault", "обсид", "profile", "профіл", "памят", "память", "нотат", "note");

        private static string NormalizePromise(string value)
            => new string((value ?? "").ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray())
                .Replace("  ", " ")
                .Trim();

        private async Task AutoSyncVaultBatchAsync(bool force = false, string reason = "batch")
        {
            if (Interlocked.CompareExchange(ref _vaultSyncInFlight, 1, 0) != 0) return;

            List<string> batch;
            try
            {
                lock (_lock)
                {
                    if ((!force && _state.PendingVaultExchangeCount < 5) || _state.PendingVaultExchangeBuffer.Count == 0)
                        return;
                    batch = _state.PendingVaultExchangeBuffer.ToList();
                }

                var prompt = $$"""
You are Kokonoe's background Obsidian archivist.
Analyze the last chat exchanges and output ONLY valid JSON.
Do not invent facts. If a section has nothing useful, use an empty array/string.
Archive reason: {{reason}}.

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

                var raw = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    WriteVaultFallback(batch, "LLM returned nothing");
                    MarkVaultBatchSynced("fallback: LLM returned nothing");
                    return;
                }

                var obj = ExtractJsonObject(raw);
                if (obj == null)
                {
                    WriteVaultFallback(batch, "JSON parse failed: " + TrimForVaultBuffer(raw, 600));
                    MarkVaultBatchSynced("fallback: JSON parse failed");
                    return;
                }

                WriteAutoVaultNotes(obj);
                RunVaultMaintenance("auto-batch-sync", TimeSpan.Zero);
                MarkVaultBatchSynced(obj["summary"]?.ToString() ?? "");
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

        private void MarkVaultBatchSynced(string summary)
        {
            lock (_lock)
            {
                _state.PendingVaultExchangeCount = 0;
                _state.PendingVaultExchangeBuffer.Clear();
                _state.LastAutoVaultSyncAt = DateTime.Now;
                _state.LastAutoVaultSyncSummary = summary;
                SaveState();
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
                var synthesisSummary = "";
                try
                {
                    var focus = string.Join(" ", new[]
                    {
                        _state.LastSemanticVisionResearchTopic,
                        _state.LastSemanticVisionSummary,
                        _state.LastKnownUserActivity,
                        _state.LastAutoVaultSyncSummary
                    }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    var synthesis = new KokoObsidianExplorationService().BuildSynthesisPlan(_obsidian, focus, 8);
                    synthesisSummary = synthesis.Summary;
                    if (synthesis.HasSignal)
                    {
                        _obsidian.WriteNote(
                            "Kokonoe/Memory/Vault Synthesis.md",
                            $"---\ntype: vault-synthesis\ntags: [kokonoe, vault, synthesis]\nupdated: {DateTime.Now:O}\n---\n\n# Vault Synthesis\n\n{synthesis.PromptBlock}\n");
                    }
                }
                catch (Exception ex)
                {
                    synthesisSummary = "synthesis failed: " + ex.Message;
                    Log("VaultSynthesis failed: " + ex.Message);
                }
                lock (_lock)
                {
                    _state.LastVaultMaintenanceAt = DateTime.Now;
                    _state.LastVaultMaintenanceReason = reason;
                    _state.LastVaultMaintenanceSummary = maintenance + (string.IsNullOrWhiteSpace(synthesisSummary) ? "" : " | " + synthesisSummary);
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
            _screenAwarenessTimer.Dispose();
            _resourceGuardianTimer.Dispose();
            _stateCheckpointTimer.Dispose();
            _dailyReviewTimer.Dispose();
            _bgLlmSemaphore.Dispose();
        }
    }
}
