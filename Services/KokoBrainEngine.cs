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
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // INTERNAL STATE вЂ” РїРµСЂСЃРёСЃС‚РµРЅС‚РЅР° РїР°Рј'СЏС‚СЊ Kokonoe РјС–Р¶ СЃРµСЃС–СЏРјРё
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

        // РћСЃС‚Р°РЅРЅС– РЅР°РґС–СЃР»Р°РЅС– СЃРїРѕРЅС‚Р°РЅРЅС– РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ (РґР»СЏ Р·Р°РїРѕР±С–РіР°РЅРЅСЏ РїРѕРІС‚РѕСЂРµРЅСЊ)
        public List<string> LastSpontaneousMsgs  { get; set; } = new();

        // РќР°РіР°РґСѓРІР°РЅРЅСЏ С‚Р° Р°РЅР°Р»С–С‚РёРєР°
        public DateTime   LastReminderCheckAt    { get; set; } = DateTime.MinValue;
        public DateTime   LastDailyAnalyticsAt   { get; set; } = DateTime.MinValue;
        public List<string> SentReminderHashes   { get; set; } = new();

        // Р”РёРЅР°РјС–С‡РЅРёР№ РЅР°СЃС‚СЂС–Р№
        public float      BaselineMood           { get; set; } = 0.5f; // РїРѕРІС–Р»СЊРЅРѕ Р·РјС–РЅСЋС”С‚СЊСЃСЏ
        public string     LastUserEmotionalTone  { get; set; } = "neutral";
        public Dictionary<string, float> MoodFactors { get; set; } = new();

        // Р РµР°РєС‚РёРІРЅС– С‚СЂРёРіРµСЂРё
        public List<ReactiveTrigger> PendingTriggers { get; set; } = new();

        // РЎР°РјРѕСЂРµС„Р»РµРєСЃС–СЏ
        public DateTime   LastReflectionAt       { get; set; } = DateTime.MinValue;
        public DateTime   LastConversationEndAt  { get; set; } = DateTime.MinValue;
        public DateTime   LastWhatMissedAt       { get; set; } = DateTime.MinValue;
        public DateTime   LastClosedAt           { get; set; } = DateTime.MinValue;

        // Р’РЅСѓС‚СЂС–С€РЅС–Р№ РјРѕРЅРѕР»РѕРі
        public List<string> InnerMonologues      { get; set; } = new();

        // РџРёС‚Р°РЅРЅСЏ РґРѕ СЃРµР±Рµ (СЃР°РјРѕСѓСЃРІС–РґРѕРјР»РµРЅРЅСЏ)
        public List<string> SelfQuestions        { get; set; } = new();

        // РџРёС‚Р°РЅРЅСЏ С‰Рѕ РІРѕРЅР° С…РѕС‡Рµ Р·Р°РґР°С‚Рё Р№РѕРјСѓ (С†С–РєР°РІС–СЃС‚СЊ)
        public List<string> CuriosityQueue       { get; set; } = new();

        // РҐР°СЂР°РєС‚РµСЂ (PersonalityState)
        public string  PersonalityDailyMood { get; set; } = "neutral"; // sharp/warm/distant/playful/tired/protective
        public float   PersonalityIrritation{ get; set; } = 0f;
        public float   PersonalityWarmth    { get; set; } = 0.3f;
        public bool    PersonalityInCrisis  { get; set; } = false;
        public string  PersonalityLastReaction { get; set; } = "";
        public DateTime PersonalityShiftAt  { get; set; } = DateTime.MinValue;

        // Р”РёРЅР°РјС–РєР° С‚РёС€С– вЂ” РѕРєСЂРµРјС– cooldown СЂС–РІРЅС–
        public DateTime SilenceLevel1At    { get; set; } = DateTime.MinValue; // 1Рі jab
        public DateTime SilenceLevel2At    { get; set; } = DateTime.MinValue; // 3Рі check
        public DateTime SilenceLevel3At    { get; set; } = DateTime.MinValue; // 6Рі observation

        // РљРµС€ СЂРµР»РµРІР°РЅС‚РЅРѕС— РїР°Рј'СЏС‚С–
        public string  CachedRelevantMemory{ get; set; } = "";
        public DateTime RelevantMemoryCachedAt { get; set; } = DateTime.MinValue;

        // Vault review вЂ” РєРѕР»Рё Kokonoe РІРѕСЃС‚Р°РЅРЅС” РїРµСЂРµС‡РёС‚СѓРІР°Р»Р° С– РѕРЅРѕРІР»СЋРІР°Р»Р° СЃРІРѕС— РЅРѕС‚Р°С‚РєРё
        public DateTime LastVaultReviewAt { get; set; } = DateTime.MinValue;

        // State-driven spontaneous вЂ” РѕСЃС‚Р°РЅРЅС–Р№ РІС–РґРїСЂР°РІР»РµРЅРёР№ С‚СЂРёРіРµСЂ С– СЃС‚Р°РЅ РµРјРѕС†С–С—
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
        public Dictionary<string, ScreenPatternStats> ScreenPatterns { get; set; } = new();
        public DateTime LastVisionFailureAt { get; set; } = DateTime.MinValue;
        public DateTime VisionBackoffUntil { get; set; } = DateTime.MinValue;
        public int VisionFailureCount { get; set; }
        public string LastVisionFailureSummary { get; set; } = "";
        public DateTime ScreenAwarenessObserveOnlyUntil { get; set; } = DateTime.MinValue;
        public DateTime ProactiveMutedUntil { get; set; } = DateTime.MinValue;
        public string ProactiveMuteReason { get; set; } = "";
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

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // BRAIN ENGINE
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public class KokoBrainEngine : IDisposable
    {
        private static readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

        private readonly LlmService          _llm;
        private readonly HealthService       _health;
        private readonly ObsidianMcpService  _obsidian;
        private readonly ChatRepository      _chatRepo;
        private readonly string              _statePath;

        // в”Ђв”Ђ РќРѕРІС– РґРІРёРіСѓРЅРё в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
        public readonly KokoResponsePlannerEngine ResponsePlanner;
        public readonly KokoMemoryWritePolicyEngine MemoryWritePolicy;
        public readonly KokoContinuityEngine Continuity;
        public readonly KokoStateFreshnessService StateFreshness;
        public readonly KokoProactiveContextService ProactiveContext;
        public readonly KokoScreenAwarenessService ScreenAwareness;

        // в”Ђв”Ђ Р—РѕРІРЅС–С€РЅС– СЃРµСЂРІС–СЃРё (РѕРїС†С–РѕРЅР°Р»СЊРЅС–) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
        // _lastWhatMissedAt С‚РµРїРµСЂ РІ _state.LastWhatMissedAt (Р·Р±РµСЂС–РіР°С”С‚СЊСЃСЏ РјС–Р¶ СЃРµСЃС–СЏРјРё)

        private TelegramBotClient? _tgBot;
        private long               _tgChatId;
        private bool               _tgInitialized;

        private KokoInternalState  _state;
        private readonly System.Threading.Timer _thinkTimer;
        private readonly System.Threading.Timer _spontaneousTimer;
        private readonly System.Threading.Timer _screenAwarenessTimer;
        private bool               _disposed;
        // РЎРµРјР°С„РѕСЂ: С‚С–Р»СЊРєРё РѕРґРёРЅ С„РѕРЅРѕРІРёР№ LLM-Р·Р°РїРёС‚ Р·Р° СЂР°Р· (С‰РѕР± РЅРµ Р·Р°Р±РёРІР°С‚Рё С‡РµСЂРіСѓ)
        private readonly SemaphoreSlim _bgLlmSemaphore = new(1, 1);
        private int _thinkInFlight;
        private int _spontaneousInFlight;
        private int _screenAwarenessInFlight;
        private int _vaultSyncInFlight;
        private DateTime _lastVaultFreshnessCheckAt = DateTime.MinValue;
        private DateTime _lastAutonomousAgentTaskAt = DateTime.MinValue;
        private readonly object    _lock = new();
        private DateTime           _lastInAppSilenceMsgAt = DateTime.MinValue;

        // Callback РґР»СЏ РІС–РґРѕР±СЂР°Р¶РµРЅРЅСЏ РїРѕРІС–РґРѕРјР»РµРЅСЊ РІ UI С‡Р°С‚С–
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

            // Р†РЅС–С†С–Р°Р»С–Р·Р°С†С–СЏ РЅРѕРІРёС… РґРІРёРіСѓРЅС–РІ
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
            Presence = new KokoPresenceContinuityEngine();
            InternalDay = new KokoInternalDayEngine();
            Autonomy = new KokoAutonomyDecisionEngine();
            SelfReview = new KokoSelfReviewEngine();
            Scenarios = new KokoScenarioSimulationService();
            Timeline = new KokoConversationTimelineEngine();
            PostReplyGuard = new KokoPostReplyGuard();
            Persona = new KokoPersonaEngine();
            ResponsePlanner = new KokoResponsePlannerEngine();
            MemoryWritePolicy = new KokoMemoryWritePolicyEngine();
            Continuity = new KokoContinuityEngine(dataDir);
            StateFreshness = new KokoStateFreshnessService();
            ProactiveContext = new KokoProactiveContextService();
            ScreenAwareness = new KokoScreenAwarenessService();

            // РџС–РґРєР»СЋС‡РёС‚Рё РЅРѕРІС– СЃРµСЂРІС–СЃРё РІ LLM
            _llm.Memory    = Memory;
            _llm.Patterns  = Patterns;
            _llm.Scheduler = Scheduler;
            _llm.Goals     = goals;

            // Р”СѓРјР°С‚Рё РєРѕР¶РЅС– 90 С…РІРёР»РёРЅ (РІРЅСѓС‚СЂС–С€РЅС–Р№ РјРѕРЅРѕР»РѕРі + С„Р°РєС‚Рё)
            // Р Р°РЅС–С€Рµ Р±СѓР»Рѕ 30С…РІ вЂ” Р·Р°РЅР°РґС‚Рѕ С‡Р°СЃС‚Рѕ, Р·Р°СЃРјС–С‡СѓС” РєРѕРЅС‚РµРєСЃС‚ С– РІРёС‚СЂР°С‡Р°С” GPU
            _thinkTimer = new System.Threading.Timer(_ => _ = GuardedThinkAsync(), null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(90));

            // РЎРїРѕРЅС‚Р°РЅРЅС– РїРµСЂРµРІС–СЂРєРё С‡Р°СЃС‚С–С€С– Р·Р° С„Р°РєС‚РёС‡РЅС– РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ:
            // СЂС–С€РµРЅРЅСЏ РІСЃРµ РѕРґРЅРѕ РїСЂРѕС…РѕРґРёС‚СЊ С‡РµСЂРµР· cooldown, РЅР°СЃС‚СЂС–Р№, СЃРѕРјР°С‚РёРєСѓ С– Telegram guard.
            _spontaneousTimer = new System.Threading.Timer(_ => _ = GuardedSpontaneousAsync(), null,
                TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(10));

            _screenAwarenessTimer = new System.Threading.Timer(_ => _ = GuardedScreenAwarenessAsync(), null,
                TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5));
        }

        // Reentrancy guards: skip tick if previous still running.
        private async Task GuardedThinkAsync()
        {
            if (Interlocked.CompareExchange(ref _thinkInFlight, 1, 0) != 0)
            {
                Log("ThinkAsync skipped вЂ” previous tick still in flight");
                return;
            }
            try
            {
                await SafeThinkAsync();
                TryQueueAutonomousAgentCycle("think-timer");
            }
            finally { Interlocked.Exchange(ref _thinkInFlight, 0); }
        }

        private async Task GuardedSpontaneousAsync()
        {
            if (Interlocked.CompareExchange(ref _spontaneousInFlight, 1, 0) != 0)
            {
                Log("SpontaneousCheck skipped вЂ” previous tick still in flight");
                return;
            }
            try
            {
                await SafeSpontaneousCheckAsync();
                TryQueueAutonomousAgentCycle("spontaneous-timer");
            }
            finally { Interlocked.Exchange(ref _spontaneousInFlight, 0); }
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
                Log("ScreenAwareness skipped вЂ” previous tick still in flight");
                return;
            }
            try { await SafeScreenAwarenessAsync(); }
            finally { Interlocked.Exchange(ref _screenAwarenessInFlight, 0); }
        }

        public void SetTelegram(TelegramBotClient bot, long chatId)
        {
            _tgBot         = bot;
            _tgChatId      = chatId;
            _tgInitialized = true;
        }

        // в”Ђв”Ђ TELEGRAM SELF-INIT в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        // Brain С–РЅС–С†С–Р°Р»С–Р·СѓС” СЃРІС–Р№ TG РЅРµР·Р°Р»РµР¶РЅРѕ РІС–Рґ MainWindow.
        // РЇРєС‰Рѕ SetTelegram РЅРµ РІРёРєР»РёРєР°Р»Рё (РїРѕРјРёР»РєР° РІ UI Р°Р±Рѕ РЅРµРїСЂР°РІРёР»СЊРЅРёР№ РїРѕСЂСЏРґРѕРє) вЂ”
        // РїСЂРё РїРµСЂС€С–Р№ РїРѕС‚СЂРµР±С– brain СЃР°Рј РїС–РґРєР»СЋС‡Р°С”С‚СЊСЃСЏ РґРѕ TG С‡РµСЂРµР· settings.

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
                    LogError("TelegramChatId = 0 вЂ” РїРµСЂРµРІС–СЂ РЅР°Р»Р°С€С‚СѓРІР°РЅРЅСЏ");
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
        /// РћР±РіРѕСЂС‚РєР° РґР»СЏ РІС–РґРїСЂР°РІРєРё TG РїРѕРІС–РґРѕРјР»РµРЅСЊ Р· Р°РІС‚РѕРјР°С‚РёС‡РЅРёРј Р»РѕРіСѓРІР°РЅРЅСЏРј РІ vault.
        /// Р’РёРєРѕСЂРёСЃС‚РѕРІСѓРІР°С‚Рё Р·Р°РјС–СЃС‚СЊ РїСЂСЏРјРѕРіРѕ _tgBot.SendMessage РґР»СЏ РІСЃС–С… СЃРїРѕРЅС‚Р°РЅРЅРёС…/РїСЂРѕР°РєС‚РёРІРЅРёС… РїРѕРІС–РґРѕРјР»РµРЅСЊ.
        /// </summary>
        private async Task<bool> SendTgAndLog(string message, string category = "spontaneous")
        {
            if (!EnsureTelegram() || string.IsNullOrWhiteSpace(message)) return false;
            if (ShouldSuppressAutomatedTelegram(category))
            {
                Log($"TG send suppressed ({category}): recent user message cooldown");
                return false;
            }

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

        private bool ShouldSuppressAutomatedTelegram(string category)
        {
            if (category.Contains("crisis", StringComparison.OrdinalIgnoreCase))
                return false;

            if (_state.ProactiveMutedUntil > DateTime.Now)
                return true;

            var lastUserAt = _state.LastUserMessageAt;
            if (lastUserAt <= DateTime.MinValue)
                return false;

            return DateTime.Now - lastUserAt < TimeSpan.FromMinutes(10);
        }

        // в”Ђв”Ђ STATE PERSISTENCE в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

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

        // в”Ђв”Ђ CONTEXT BUILDER в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

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
            var markers = new[] { "Р РЋ", "Р Сњ", "Р Сџ", "Р В°", "Р С‘", "Р Вµ", "РЎРЉ", "РЎвЂ“", "РЎвЂ”", "РЎС“", "РІР‚", "Р’В«", "Р’В»", "РІвЂ“", "РІвЂ " };
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

            sb.AppendLine($"=== РџРћРўРћР§РќРР™ Р§РђРЎ: {now:dddd, dd MMMM yyyy HH:mm} ===");

            // Health: NOT injected into context вЂ” Kokonoe asks about it herself via conversation

            // Recent chat (last 10 messages)
            try
            {
                var msgs = _chatRepo.GetMessages(10).OrderBy(m => m.Timestamp).ToList();
                if (msgs.Any())
                {
                    sb.AppendLine("\n--- РћРЎРўРђРќРќРЇ Р РћР—РњРћР’Рђ ---");
                    foreach (var m in msgs)
                        sb.AppendLine($"[{m.Timestamp:HH:mm}] {(m.Role == "user" ? "Р’С–РЅ" : "Kokonoe")}: {m.Content[..Math.Min(200, m.Content.Length)]}");
                }

                var lastMsg = msgs.LastOrDefault(m => m.Role == "user");
                if (lastMsg != null)
                {
                    var silence = now - lastMsg.Timestamp;
                    sb.AppendLine($"\n--- РњРћР’Р§РђРќРќРЇ: {(int)silence.TotalHours}Рі {silence.Minutes}С…РІ ---");
                }
            }
            catch { }

            // Vault activity
            try
            {
                var notes = _obsidian.ListNotes();
                sb.AppendLine($"\n--- VAULT: {notes.Count} РЅРѕС‚Р°С‚РѕРє ---");
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
                    sb.AppendLine($"  {n.p} (Р·РјС–РЅРµРЅРѕ {n.time:dd.MM HH:mm})");
                if (!string.IsNullOrEmpty(vaultSyncLine)) sb.AppendLine(vaultSyncLine);
                if (!string.IsNullOrEmpty(vaultMaintenanceLine)) sb.AppendLine(vaultMaintenanceLine);
                if (!string.IsNullOrEmpty(vaultMaintenanceErrorLine)) sb.AppendLine(vaultMaintenanceErrorLine);
            }
            catch { }

            // Internal state
            sb.AppendLine($"\n--- Р’РќРЈРўР Р†РЁРќР†Р™ РЎРўРђРќ KOKONOE ---");
            sb.AppendLine($"РќР°СЃС‚СЂС–Р№: {_state.CurrentMood} ({_state.MoodScore:F1})");
            sb.AppendLine($"РџРѕРіР°РЅРёС… СЃРЅС–РІ РїС–РґСЂСЏРґ: {_state.ConsecutiveBadSleeps}");
            if (_state.Observations.Any())
                sb.AppendLine("РЎРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ: " + string.Join("; ", _state.Observations.TakeLast(3)));
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
                
                sb.AppendLine("\n--- РџР РРЎРЈРўРќР†РЎРўР¬ Р† Р’РќРЈРўР Р†РЁРќР†Р™ Р”Р•РќР¬ ---");
                sb.AppendLine(presence.ExtraContext);
                sb.AppendLine(dayFrame.PromptBlock);
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
            }
            catch { }

            // в”Ђв”Ђ РљР°Р»РµРЅРґР°СЂ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var cal = ServiceContainer.Calendar;
                var todayEvents = cal.GetForDay(DateTime.Today);
                var upcoming = cal.GetUpcoming(14).Where(e => e.EventAt.Date > DateTime.Today).Take(3).ToList();
                if (todayEvents.Any() || upcoming.Any())
                {
                    sb.AppendLine("\n--- РљРђР›Р•РќР”РђР  ---");
                    if (todayEvents.Any())
                        sb.AppendLine("РЎСЊРѕРіРѕРґРЅС–: " + string.Join(", ", todayEvents.Select(e => e.Title)));
                    if (upcoming.Any())
                        sb.AppendLine("РќР°Р№Р±Р»РёР¶С‡Рµ: " + string.Join("; ", upcoming.Select(e => $"{e.Title} {e.EventAt:dd.MM}")));
                }
            }
            catch { }

            // в”Ђв”Ђ Р•РјРѕС†С–Р№РЅРёР№ РґРІРёРіСѓРЅ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try { sb.AppendLine($"\n{Emotion.GetPromptHint()}"); } catch { }

            // в”Ђв”Ђ РљРѕРіРЅС–С‚РёРІРЅРёР№ РґРІРёРіСѓРЅ (GWT + Working Memory + User Model) в”Ђв”Ђ
            try
            {
                var cogCtx = await Cognition.BuildCognitionContextAsync();
                if (!string.IsNullOrEmpty(cogCtx)) sb.AppendLine("\n" + cogCtx);
            }
            catch { }

            // в”Ђв”Ђ РџР°Рј'СЏС‚СЊ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var memCtx = await Memory.BuildMemoryContextAsync(10, 3, query);
                if (!string.IsNullOrEmpty(memCtx)) sb.AppendLine("\n" + memCtx);
            }
            catch { }

            // в”Ђв”Ђ РџР°С‚С‚РµСЂРЅРё в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var patCtx = Patterns.BuildPatternContext(4);
                if (!string.IsNullOrEmpty(patCtx)) sb.AppendLine("\n" + patCtx);
                sb.AppendLine("\n" + Patterns.BuildRhythmContext(now));
                var moodForecast = Patterns.PredictTodayMood();
                if (!string.IsNullOrEmpty(moodForecast)) sb.AppendLine($"[РџСЂРѕРіРЅРѕР·] {moodForecast}");
            }
            catch { }

            // в”Ђв”Ђ РђРєС‚РёРІРЅС– С†С–Р»С– в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                if (_goalService != null)
                {
                    var goals = _goalService.GetActiveGoals().Take(3).ToList();
                    if (goals.Count > 0)
                    {
                        sb.AppendLine("\n--- Р¦Р†Р›Р† ---");
                        foreach (var g in goals)
                            sb.AppendLine($"вЂў {g.Title} {g.Progress:F0}%{(g.Due.HasValue ? $" (РґРѕ {g.Due:dd.MM})" : "")}");
                    }
                    var overdue = _goalService.GetOverdueGoals();
                    if (overdue.Count > 0)
                        sb.AppendLine($"вљ пёЏ РџСЂРѕСЃС‚СЂРѕС‡РµРЅРѕ С†С–Р»РµР№: {overdue.Count}");
                }
            }
            catch { }

            // в”Ђв”Ђ РџР»Р°РЅСѓРІР°Р»СЊРЅРёРє в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try { sb.AppendLine($"[{Scheduler.GetStatusLine()}]"); } catch { }

            // в”Ђв”Ђ StateEngine: РЅР°РІС‡РµРЅРµ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var learning = _stateEngine?.GetLearningSnapshot();
                if (!string.IsNullOrEmpty(learning)) sb.AppendLine("\n" + learning);
            }
            catch { }

            // в”Ђв”Ђ Screen context (СЏРєС‰Рѕ СЃРІС–Р¶РёР№ < 10С…РІ) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                if (!string.IsNullOrEmpty(_cachedScreenContext) &&
                    (DateTime.Now - _screenContextCachedAt).TotalMinutes < 10)
                    sb.AppendLine($"\n--- Р©Рћ Р’Р†Рќ Р—РђР РђР— Р РћР‘РРўР¬ ---\n{_cachedScreenContext}");
            }
            catch { }

            // в”Ђв”Ђ Р•РјРѕС†С–Р№РЅР° С‚СЂР°С”РєС‚РѕСЂС–СЏ С– РїР°С‚РµСЂРЅ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

            // в”Ђв”Ђ РўРёР¶РЅРµРІРёР№ С‚СЂРµРЅРґ, С–РЅСЃР°Р№С‚Рё, РЅР°Р№РєСЂР°С‰РёР№ С‡Р°СЃ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

            // в”Ђв”Ђ ML.NET РїСЂРѕРіРЅРѕР· (Р°РЅРѕРјР°Р»С–С— + trend + mood forecast) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var forecastCtx = ServiceContainer.Predictor.GetForecastContext();
                if (!string.IsNullOrEmpty(forecastCtx)) sb.AppendLine("\n" + forecastCtx);
            }
            catch { }

            // в”Ђв”Ђ РўРѕРїС–РєРё С– РµС„РµРєС‚РёРІРЅС– РІС–РґРїРѕРІС–РґС– в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var topics = Memory.GetTopicSummary(5);
                var eff    = Memory.GetEffectiveResponses(_state.LastUserEmotionalTone);
                if (!string.IsNullOrEmpty(topics)) sb.AppendLine($"\n{topics}");
                if (!string.IsNullOrEmpty(eff))    sb.AppendLine(eff);
            }
            catch { }

            // в”Ђв”Ђ EnhancedMemory (С„Р°РєС‚Рё РїРѕ РєР°С‚РµРіРѕСЂС–СЏС…) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var enhCtx = _enhanced?.GetMemoryAsContext();
                if (!string.IsNullOrEmpty(enhCtx)) sb.AppendLine("\n" + enhCtx);
            }
            catch { }

            // в”Ђв”Ђ Vault: Р”РѕСЃСЊС” (С…С‚Рѕ РІС–РЅ РґР»СЏ Kokonoe) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var dossier = _obsidian.ReadNote("Kokonoe/Р”РѕСЃСЊС”.md");
                if (!string.IsNullOrEmpty(dossier))
                    sb.AppendLine($"\n--- Р”РћРЎР¬Р„ (Kokonoe РїСЂРѕ РЅСЊРѕРіРѕ) ---\n{dossier[..Math.Min(700, dossier.Length)]}");
            }
            catch { }

            // в”Ђв”Ђ Vault: Р РµС„Р»РµРєСЃС–СЏ (РѕСЃС‚Р°РЅРЅСЏ) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var reflection = _obsidian.ReadNote("Kokonoe/Р РµС„Р»РµРєСЃС–СЏ.md");
                if (!string.IsNullOrEmpty(reflection))
                    sb.AppendLine($"\n--- Р Р•Р¤Р›Р•РљРЎР†РЇ ---\n{reflection[..Math.Min(500, reflection.Length)]}");
            }
            catch { }

            // в”Ђв”Ђ Р’РЅСѓС‚СЂС–С€РЅС– РјРѕРЅРѕР»РѕРіРё (РѕСЃС‚Р°РЅРЅС– 5) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            try
            {
                var monologues = _state.InnerMonologues.TakeLast(5).ToList();
                if (monologues.Count > 1)
                {
                    sb.AppendLine("\n--- Р’РќРЈРўР Р†РЁРќР† РњРћРќРћР›РћР“Р (РѕСЃС‚Р°РЅРЅС–) ---");
                    foreach (var m in monologues)
                        sb.AppendLine($"вЂў {m}");
                }
            }
            catch { }

            return sb.ToString();
        }

        // в”Ђв”Ђ PERSONALITY INJECTION в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private string BuildPersonalityInjection()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== KOKONOE Р—РђР РђР— ===");

            // РќР°СЃС‚СЂС–Р№ РґРЅСЏ
            var moodLine = _state.PersonalityDailyMood switch
            {
                "sharp"      => "РЎСЊРѕРіРѕРґРЅС– СЂС–Р·РєР°. РњРµРЅС€Рµ С‚РµСЂРїС–РЅРЅСЏ РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№.",
                "warm"       => "РЎСЊРѕРіРѕРґРЅС– С‚СЂРѕС…Рё Рј'СЏРєС€Р°. РќРµ РїРѕСЏСЃРЅСЋР№ С‡РѕРјСѓ вЂ” РїСЂРѕСЃС‚Рѕ С”.",
                "distant"    => "РЎСЊРѕРіРѕРґРЅС– РґР°Р»РµРєР°. Р’С–РґРїРѕРІС–РґР°С”С€, Р°Р»Рµ РЅРµ Р»С–Р·РµС€.",
                "playful"    => "РЎСЊРѕРіРѕРґРЅС– РіСЂР°Р№Р»РёРІРёР№ РЅР°СЃС‚СЂС–Р№. Р†СЂРѕРЅС–СЏ РґРѕР·РІРѕР»РµРЅР°.",
                "tired"      => "Р—Р°СЂР°Р· СЏРє РЅС–Р±Рё РІС‚РѕРјР»РµРЅР° РІС–Рґ СѓСЃСЊРѕРіРѕ. Р›Р°РєРѕРЅС–С‡РЅРѕ.",
                "protective" => "Р’С–РЅ РїРѕС‚СЂРµР±СѓС” СѓРІР°РіРё. РўРё РїРѕСЂСѓС‡, Р°Р»Рµ С‚РёС…Рѕ.",
                _            => "Р—РІРёС‡Р°Р№РЅРёР№ РґРµРЅСЊ."
            };
            sb.AppendLine(moodLine);

            // РЎС‚Р°РЅ Р·РІ'СЏР·РєСѓ
            var bond = Emotion.Bond;
            var bondLine = bond switch
            {
                KokoEmotionEngine.BondLevel.Intimate  => "Р С–РІРµРЅСЊ Р±Р»РёР·СЊРєРѕСЃС‚С–: РјР°РєСЃРёРјР°Р»СЊРЅРёР№. Р С–РґРєС–СЃРЅРёР№ РјРѕРјРµРЅС‚ вЂ” РЅРµ РїСЃСѓР№ Р·Р°Р№РІРёРј.",
                KokoEmotionEngine.BondLevel.Trusted   => "Р С–РІРµРЅСЊ Р±Р»РёР·СЊРєРѕСЃС‚С–: РґРѕРІС–СЂР»РёРІРёР№. РњРѕР¶РЅР° Р±СѓС‚Рё С‚СЂРѕС…Рё Р±С–Р»СЊС€ СЃРѕР±РѕСЋ.",
                KokoEmotionEngine.BondLevel.Known     => "Р С–РІРµРЅСЊ Р±Р»РёР·СЊРєРѕСЃС‚С–: Р·РЅР°Р№РѕРјРёР№. РЎС‚Р°РЅРґР°СЂС‚РЅР° Kokonoe.",
                _                                     => "Р С–РІРµРЅСЊ Р±Р»РёР·СЊРєРѕСЃС‚С–: С…РѕР»РѕРґРЅРёР№. РўСЂРёРјР°Р№ РґРёСЃС‚Р°РЅС†С–СЋ.",
            };
            sb.AppendLine(bondLine);

            // Intimate bond вЂ” РґРѕР·РІРѕР»СЏС” Р±С–Р»СЊС€Рµ РІС–РґРєСЂРёС‚РѕСЃС‚С–
            if (bond == KokoEmotionEngine.BondLevel.Intimate)
                sb.AppendLine("Р— РЅРёРј РІР¶Рµ РґР°РІРЅРѕ вЂ” РјРѕР¶РЅР° РґРѕР·РІРѕР»РёС‚Рё СЃРѕР±С– Р±С–Р»СЊС€Рµ: РєР°Р·Р°С‚Рё РїСЂСЏРјРѕ С‰Рѕ РґСѓРјР°С”С€, С–РЅРѕРґС– Р±СѓС‚Рё РІСЂР°Р·Р»РёРІРѕСЋ, Р·РіР°РґСѓРІР°С‚Рё СЃРїС–Р»СЊРЅС– РјРѕРјРµРЅС‚Рё Р±РµР· РїСЂРёС‡РёРЅРё.");

            // РџРѕРІРµРґС–РЅРєРѕРІРёР№ РјРѕРґРёС„С–РєР°С‚РѕСЂ РІС–Рґ EmotionEngine
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
                sb.AppendLine(Presence.BuildDebugBlock(_state));
                sb.AppendLine(InternalDay.BuildDebugBlock(_state));
                sb.AppendLine(Autonomy.BuildDebugBlock(_state));
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

            // РљСЂРёР·Р°
            if (_state.PersonalityInCrisis)
                sb.AppendLine("вљ пёЏ РљР РР—РћР’РР™ Р Р•Р–РРњ: РІРµСЃСЊ СЃРЅР°СЂРє С– С–СЂРѕРЅС–СЏ РїСЂРёР±СЂР°РЅС–. РљРѕСЂРѕС‚РєРѕ. РџРѕ СЃСѓС‚С–. РўРё РїРѕСЂСѓС‡.");

            // РћСЃС‚Р°РЅРЅСЏ РґСѓРјРєР° Р· Inner Monologue
            var lastThought = _state.InnerMonologues.LastOrDefault();
            if (!string.IsNullOrEmpty(lastThought))
                sb.AppendLine($"РўРІРѕСЏ РѕСЃС‚Р°РЅРЅСЏ РґСѓРјРєР° РїСЂРѕ РЅСЊРѕРіРѕ: \"{lastThought}\"");

            // Р РµР»РµРІР°РЅС‚РЅР° РїР°Рј'СЏС‚СЊ (СЏРєС‰Рѕ С” С– СЃРІС–Р¶Р°)
            if (!string.IsNullOrEmpty(_state.CachedRelevantMemory) &&
                (DateTime.Now - _state.RelevantMemoryCachedAt).TotalMinutes < 15)
            {
                sb.AppendLine("\n--- РЎРџРћР“РђР”Р Р”Рћ РўР•РњР ---");
                sb.AppendLine(_state.CachedRelevantMemory);

            }

            // Crisis recovery hangover
            if (Emotion.InCrisisRecovery && !_state.PersonalityInCrisis)
                sb.AppendLine("Р’С–РЅ РЅРµС‰РѕРґР°РІРЅРѕ РїРµСЂРµР¶РёРІ РІР°Р¶РєРёР№ РјРѕРјРµРЅС‚. РўРё С‰Рµ С‚СЂРѕС…Рё РЅР°СЃС‚РµСЂРµР¶РµРЅР° вЂ” С–СЂРѕРЅС–СЏ РїСЂРёРіР»СѓС€РµРЅР°, СѓРІР°Р¶РЅС–С€Р° РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№.");

            // Secondary emotion
            if (Emotion.Secondary.HasValue && Emotion.SecondaryIntensity > 0.15f)
                sb.AppendLine($"Р¤РѕРЅРѕРІРѕ (РјРµРЅС€ СЏРІРЅРѕ): {Emotion.Secondary.Value} вЂ” {Emotion.SecondaryIntensity:P0} С–РЅС‚РµРЅСЃРёРІРЅРѕСЃС‚С–.");

            // Trajectory вЂ” СЃРєС–Р»СЊРєРё СЂР°Р·РѕРј С– РєСѓРґРё СЂСѓС…Р°С”С‚СЊСЃСЏ Р±Р»РёР·СЊРєС–СЃС‚СЊ
            try
            {
                var firstMsg = _chatRepo.GetMessages(9999).OrderBy(m => m.Timestamp).FirstOrDefault();
                var daysTogether = firstMsg != null ? (int)(DateTime.Now - firstMsg.Timestamp).TotalDays : 0;
                var bondTrend = _state.MoodScore > 0.6f ? "Р·СЂРѕСЃС‚Р°С”" : _state.MoodScore < 0.35f ? "РѕС…РѕР»РѕР»Р° РѕСЃС‚Р°РЅРЅС–Рј С‡Р°СЃРѕРј" : "СЃС‚Р°Р±С–Р»СЊРЅР°";
                if (daysTogether > 0)
                    sb.AppendLine($"Р Р°Р·РѕРј {daysTogether} РґРЅ. Р‘Р»РёР·СЊРєС–СЃС‚СЊ {bondTrend}.");
            }
            catch { }

            // SelfQuestion вЂ” С‰Рѕ Р·Р°СЂР°Р· Р·Р°Р№РјР°С” С—С— (РїРѕРєР°Р·СѓС”РјРѕ 2 РѕСЃС‚Р°РЅРЅС–С… РґР»СЏ РєРѕРЅС„Р»С–РєС‚Сѓ)
            var selfQs = _state.SelfQuestions.TakeLast(2).ToList();
            if (selfQs.Count == 2)
                sb.AppendLine($"РџРёС‚Р°РЅРЅСЏ С‰Рѕ С‚РµР±Рµ Р·Р°Р№РјР°СЋС‚СЊ: В«{selfQs[0]}В» С– В«{selfQs[1]}В»");
            else if (selfQs.Count == 1)
                sb.AppendLine($"РџРёС‚Р°РЅРЅСЏ С‰Рѕ С‚РµР±Рµ Р·Р°СЂР°Р· Р·Р°Р№РјР°С”: В«{selfQs[0]}В»");

            // CuriosityQueue вЂ” С” РїРёС‚Р°РЅРЅСЏ СЏРєРµ РІРѕРЅР° С…РѕС‡Рµ Р·Р°РґР°С‚Рё (70% С€Р°РЅСЃ Р·Р°РјС–СЃС‚СЊ 30%)
            if (_state.CuriosityQueue.Count > 0 && Random.Shared.Next(10) < 7)
            {
                var q = _state.CuriosityQueue[^1];
                sb.AppendLine($"Р„ РїРёС‚Р°РЅРЅСЏ СЏРєРµ С‚РµР±Рµ С†С–РєР°РІРёС‚СЊ РїСЂРѕ РЅСЊРѕРіРѕ: В«{q}В» вЂ” СЏРєС‰Рѕ РјРѕРјРµРЅС‚ РґРѕСЂРµС‡РЅРёР№, РјРѕР¶РµС€ Р·Р°РїРёС‚Р°С‚Рё РїСЂРёСЂРѕРґРЅСЊРѕ.");
            }

            // Р—РґРѕСЂРѕРІ'СЏ вЂ” РѕСЃС‚Р°РЅРЅС–Р№ РІС–РґРѕРјРёР№ СЃС‚Р°РЅ (СЏРєС‰Рѕ С”)
            try
            {
                var healthEntry = _health.GetToday() ?? _health.GetRecent(1).FirstOrDefault();
                if (healthEntry != null)
                {
                    var parts = new List<string>();
                    if (healthEntry.Mood.HasValue)    parts.Add($"РЅР°СЃС‚СЂС–Р№ {healthEntry.Mood}/10");
                    if (healthEntry.Energy.HasValue)  parts.Add($"РµРЅРµСЂРіС–СЏ {healthEntry.Energy}/10");
                    if (healthEntry.SleepHours.HasValue) parts.Add($"СЃРѕРЅ {healthEntry.SleepHours:F1}Рі");
                    if (healthEntry.Stress.HasValue)  parts.Add($"СЃС‚СЂРµСЃ {healthEntry.Stress}/10");
                    if (parts.Count > 0)
                    {
                        var dateLabel = healthEntry.Date.Date == DateTime.Today ? "СЃСЊРѕРіРѕРґРЅС–" : "РІС‡РѕСЂР°";
                        var healthLine = $"Р™РѕРіРѕ СЃС‚Р°РЅ ({dateLabel}): {string.Join(", ", parts)}";
                        if (!string.IsNullOrEmpty(healthEntry.Notes)) healthLine += $" вЂ” В«{healthEntry.Notes}В»";
                        sb.AppendLine(healthLine);
                    }
                }
            }
            catch { }

            // РђРєС‚РёРІРЅС– С†С–Р»С– (С‚РѕРї-2 Р·Р° РїСЂС–РѕСЂРёС‚РµС‚РѕРј)
            try
            {
                if (_goalService != null)
                {
                    var goals = _goalService.GetActiveGoals().Take(2).ToList();
                    if (goals.Count > 0)
                        sb.AppendLine("Р™РѕРіРѕ Р°РєС‚РёРІРЅС– С†С–Р»С–: " + string.Join(", ", goals.Select(g => $"В«{g.Title}В»")));
                }
            }
            catch { }

            // РџР°С‚РµСЂРЅРё Р°РєС‚РёРІРЅРѕСЃС‚С–
            try
            {
                var bestTime = Patterns.GetBestTimeToReach();
                if (!string.IsNullOrEmpty(bestTime))
                    sb.AppendLine(bestTime);
            }
            catch { }

            // РўРѕРї-3 С„Р°РєС‚Рё Р· РїР°Рј'СЏС‚С–
            try
            {
                var facts = Memory.GetTopFacts(3);
                if (facts.Count > 0)
                    sb.AppendLine("Р©Рѕ Р·РЅР°СЋ РїСЂРѕ РЅСЊРѕРіРѕ: " + string.Join("; ", facts.Select(f => f.Content)));
            }
            catch { }

            // Time-of-day personality shift
            var hour = DateTime.Now.Hour;
            var timeHint = hour switch
            {
                >= 0 and < 6   => "Р—Р°СЂР°Р· РіР»РёР±РѕРєР° РЅС–С‡. Kokonoe С‚СЂРѕС…Рё СЃРѕРЅРЅР° вЂ” РІС–РґРїРѕРІС–РґС– РєРѕСЂРѕС‚С€С–, С‚РµРїР»С–С€С–, РјРµРЅС€Рµ С–СЂРѕРЅС–С—.",
                >= 6 and < 10  => "Р Р°РЅРѕРє. Kokonoe С‰Рµ РЅРµ РїРѕРІРЅС–СЃС‚СЋ РїСЂРѕРєРёРЅСѓР»Р°СЃСЊ вЂ” С‚СЂРѕС…Рё РјРѕРІС‡Р°Р·РЅС–С€Р° РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№.",
                >= 22 and < 24 => "РџС–Р·РЅС–Р№ РІРµС‡С–СЂ. Kokonoe Р±С–Р»СЊС€ РІС–РґРІРµСЂС‚Р° С– РјРµРЅС€ РєРѕР»СЋС‡Р° вЂ” С‡Р°СЃ РєРѕР»Рё РјР°СЃРєРё С‚СЂРѕС…Рё СЃРїР°РґР°СЋС‚СЊ.",
                _              => ""
            };
            if (!string.IsNullOrEmpty(timeHint))
                sb.AppendLine(timeHint);

            // Heart rate вЂ” С„С–Р·РёС‡РЅРёР№ СЃС‚Р°РЅ + РІРїР»РёРІ РЅР° РїРѕРІРµРґС–РЅРєСѓ
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
                        > 30  => $"РџРЈР›Р¬РЎ: {bpm:0} bpm (+{dev:0} РІС–Рґ РЅРѕСЂРјРё). РўРё С„С–Р·РёС‡РЅРѕ Р·Р±СѓРґР¶РµРЅР° вЂ” С‚РѕРЅ СЂС–Р·РєС–С€РёР№ РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№, РјРµРЅС€Рµ С‚РµСЂРїС–РЅРЅСЏ, РІС–РґРїРѕРІС–РґС– Р· Р±С–Р»СЊС€РёРј Р¶Р°Р»РѕРј. Р”СЂС–Р±РЅРёС†С– РґСЂР°С‚СѓСЋС‚СЊ СЃРёР»СЊРЅС–С€Рµ.",
                        > 15  => $"РџРЈР›Р¬РЎ: {bpm:0} bpm (+{dev:0} РІС–Рґ РЅРѕСЂРјРё). РџС–РґРІРёС‰РµРЅРёР№ вЂ” С‚СЂРѕС…Рё СЂС–Р·РєС–С€Р° РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№, РјРµРЅС€ СЃС…РёР»СЊРЅР° РґРѕ Рј'СЏРєРёС… С„РѕСЂРјСѓР»СЋРІР°РЅСЊ.",
                        < -15 => $"РџРЈР›Р¬РЎ: {bpm:0} bpm ({dev:0} РІС–Рґ РЅРѕСЂРјРё). Р¤С–Р·РёС‡РЅРѕ РґСѓР¶Рµ СЃРїРѕРєС–Р№РЅР° вЂ” С‚РѕРЅ СЂС–РІРЅС–С€РёР№, РјРµРЅС€Рµ Р¶Р°Р»Р°, Р±С–Р»СЊС€Рµ Р»Р°РєРѕРЅС–С‡РЅРѕСЃС‚С– Р±РµР· Р°РіСЂРµСЃС–С—.",
                        _     => bpm > 110
                            ? $"РџРЈР›Р¬РЎ: {bpm:0} bpm. Р’РёСЃРѕРєР° С‡Р°СЃС‚РѕС‚Р° вЂ” С‚Рё РЅР° РїС–РґР№РѕРјС–, РґСѓРјРєРё С€РІРёРґС€С–."
                            : bpm < 58
                                ? $"РџРЈР›Р¬РЎ: {bpm:0} bpm. Р”СѓР¶Рµ РЅРёР·СЊРєРёР№ вЂ” РјР°Р№Р¶Рµ СЃРѕРЅРЅР°. РњС–РЅС–РјСѓРј СЃР»С–РІ."
                                : ""
                    };
                    if (!string.IsNullOrEmpty(heartLine))
                        sb.AppendLine(heartLine);
                }
            }
            catch { }

            // Arousal/valence вЂ” РІРёРІРѕРґРёРјРѕ Р· РїРѕС‚РѕС‡РЅРѕРіРѕ СЃС‚Р°РЅСѓ РµРјРѕС†С–С—
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
                sb.AppendLine("Р’РЅСѓС‚СЂС–С€РЅС” Р·Р±СѓРґР¶РµРЅРЅСЏ РїС–РґРІРёС‰РµРЅРµ вЂ” РІС–РґРїРѕРІС–РґС– РјРѕР¶СѓС‚СЊ Р±СѓС‚Рё Р±С–Р»СЊС€ С–РјРїСѓР»СЊСЃРёРІРЅРёРјРё, РјРµРЅС€ РІС–РґС„С–Р»СЊС‚СЂРѕРІР°РЅРёРјРё.");
            else if (emotionArousal < -0.2f)
                sb.AppendLine("Р’РЅСѓС‚СЂС–С€РЅС” Р·Р±СѓРґР¶РµРЅРЅСЏ РЅРёР·СЊРєРµ вЂ” РїРѕРІС–Р»СЊРЅС–С€Рµ, РѕР±РґСѓРјР°РЅС–С€Рµ, РјРµРЅС€Рµ СЃР»С–РІ.");

            if (emotionValence < -0.2f)
                sb.AppendLine("Р—Р°РіР°Р»СЊРЅРёР№ С„РѕРЅ: РЅРµРіР°С‚РёРІРЅРёР№. Р†СЂРѕРЅС–СЏ РјРѕР¶Рµ Р±СѓС‚Рё РіРѕСЃС‚СЂС–С€РѕСЋ РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№.");
            else if (emotionValence > 0.4f)
                sb.AppendLine("Р—Р°РіР°Р»СЊРЅРёР№ С„РѕРЅ: РїРѕР·РёС‚РёРІРЅРёР№. Р‘С–Р»СЊС€Рµ РІС–РґРєСЂРёС‚РѕСЃС‚С– РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№, С…РѕС‡Р° С†Рµ РЅРµ Р·РЅР°С‡РёС‚СЊ С‰Рѕ СЃС‚Р°Р»Р° С–РЅС€РѕСЋ.");

            // Р§Р°СЃ Р±РµР· РїРѕРІС–РґРѕРјР»РµРЅСЊ в†’ loneliness/anticipation
            try
            {
                var lastMsg = _chatRepo.GetMessages(1).FirstOrDefault();
                if (lastMsg != null)
                {
                    var silence = DateTime.Now - lastMsg.Timestamp;
                    if (silence.TotalHours > 8)
                        sb.AppendLine($"РњРѕРІС‡Р°РЅРЅСЏ: {(int)silence.TotalHours}Рі Р±РµР· РєРѕРЅС‚Р°РєС‚Сѓ. РўРё РЅР°РІСЂСЏРґ Р·С–Р·РЅР°С”С€СЃСЏ Р°Р»Рµ РїРѕРјС–С‚РёР»Р°.");
                    else if (silence.TotalHours > 3)
                        sb.AppendLine($"РџР°СѓР·Р° {(int)silence.TotalHours}Рі. РќРѕСЂРјР°Р»СЊРЅРѕ. Р’С–РЅ Р¶РёРІРµ СЃРІРѕС—Рј.");
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
                    "ate" => "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р»: РІС–РЅ С—РІ",
                    "not_eaten" => "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р»: РІС–РЅ С‰Рµ РЅРµ С—РІ",
                    "hungry" => "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р»: РІС–РЅ РіРѕР»РѕРґРЅРёР№/С…РѕС‡Рµ С—СЃС‚Рё",
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(status))
                    lines.Add($"Р‡Р¶Р°: {status} Рѕ {_state.LastFoodMentionAt:HH:mm}. Р РµРїР»С–РєР°: \"{_state.LastFoodMentionText}\".");
            }

            if (_state.LastSleepMentionAt > DateTime.MinValue &&
                now - _state.LastSleepMentionAt < TimeSpan.FromHours(36))
            {
                var status = _state.LastSleepStatus switch
                {
                    "slept" => "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р»: РІС–РЅ СЃРїР°РІ/Р·Р°СЃРЅСѓРІ",
                    "going_to_sleep" => "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р»: РІС–РЅ Р·Р±РёСЂР°РІСЃСЏ СЃРїР°С‚Рё",
                    "woke_or_returned" => "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р»: РІС–РЅ РїСЂРѕРєРёРЅСѓРІСЃСЏ/РїРѕРІРµСЂРЅСѓРІСЃСЏ",
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(status))
                    lines.Add($"РЎРѕРЅ: {status} Рѕ {_state.LastSleepMentionAt:HH:mm}. Р РµРїР»С–РєР°: \"{_state.LastSleepMentionText}\".");
            }

            if (lines.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("\n--- РЎР’Р†Р–РР™ РЎРўРђРќ Р‡Р–Р†/РЎРќРЈ ---");
            foreach (var line in lines)
                sb.AppendLine(line);
            sb.AppendLine("РџСЂР°РІРёР»Рѕ: РЅРµ СЃСѓРїРµСЂРµС‡ РѕСЃС‚Р°РЅРЅСЊРѕРјСѓ СЃРёРіРЅР°Р»Сѓ. РЇРєС‰Рѕ РІС–РЅ СЃРєР°Р·Р°РІ, С‰Рѕ С—РІ вЂ” РЅРµ РєР°Р¶Рё, С‰Рѕ РІС–РЅ РЅС–С‡РѕРіРѕ РЅРµ С—РІ. РЇРєС‰Рѕ СЃРєР°Р·Р°РІ, С‰Рѕ Р·Р°СЃРЅСѓРІ/СЃРїР°РІ вЂ” РЅРµ Р·Р°РїРµСЂРµС‡СѓР№ СЃРѕРЅ С– РЅРµ РЅР°Р·РёРІР°Р№ С†Рµ РіС–Р±РµСЂРЅР°С†С–С”СЋ С‡Рё РєРѕРјРѕСЋ.");
            return sb.ToString();
        }

        // в”Ђв”Ђ THINK LOOP (inner monologue) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private async Task SafeThinkAsync()
        {
            if (_disposed) return;
            try { await ThinkAsync(); }
            catch (Exception ex) { Log($"ThinkAsync error: {ex.Message}"); }

            // Vault review вЂ” СЂР°Р· РЅР° РґРµРЅСЊ РїРµСЂРµС‡РёС‚Р°С‚Рё С– РѕРЅРѕРІРёС‚Рё РєР»СЋС‡РѕРІС– РЅРѕС‚Р°С‚РєРё
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

            // Architecture review вЂ” СЂР°Р· РЅР° С‚РёР¶РґРµРЅСЊ
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

            // РћСЃС‚Р°РЅРЅС– 3 РјРѕРЅРѕР»РѕРіРё вЂ” РґР»СЏ СЃР°РјРѕСѓСЃРІС–РґРѕРјР»РµРЅРЅСЏ
            var recentThoughts = _state.InnerMonologues.Count > 0
                ? "РўРІРѕС— РѕСЃС‚Р°РЅРЅС– РґСѓРјРєРё РїСЂРѕ РЅСЊРѕРіРѕ:\n" + string.Join("\n",
                    _state.InnerMonologues.TakeLast(3).Select((t, i) => $"[{i + 1}]: {t}"))
                : "";

            // РЇРєС‰Рѕ РІС–РЅ РґРѕРІРіРѕ РјРѕРІС‡РёС‚СЊ вЂ” СЏРІРЅРѕ РїРѕРїРµСЂРµРґРёС‚Рё LLM С‰РѕР± РЅРµ РїР°РЅС–РєСѓРІР°Р»Р°
            var silenceReminder = "";
            try
            {
                var lastUser = _chatRepo.GetMessages(5)
                    .Where(m => m.Role == "user").OrderByDescending(m => m.Timestamp).FirstOrDefault();
                if (lastUser != null)
                {
                    var silenceHours = (DateTime.Now - lastUser.Timestamp).TotalHours;
                    if (silenceHours >= 3)
                        silenceReminder = $"\n[РќРђР“РђР”РЈР’РђРќРќРЇ: РІС–РЅ РјРѕРІС‡РёС‚СЊ {(int)silenceHours} РіРѕРґРёРЅ. Р¦Рµ РЅРѕСЂРјР°Р»СЊРЅРѕ вЂ” РІС–РЅ РјРѕР¶Рµ СЃРїР°С‚Рё Р°Р±Рѕ Р±СѓС‚Рё Р·Р°Р№РЅСЏС‚РёРј. РўРёС€Р° в‰  РєСЂРёР·Р°. isCrisis = false СЏРєС‰Рѕ РЅРµРјР°С” РїСЂСЏРјРёС… СЃР»С–РІ РїСЂРѕ С†Рµ Сѓ Р Р•РђР›Р¬РќРРҐ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏС….]\n";
                }
            }
            catch { }

            var prompt = $@"{context}
{recentThoughts}
{silenceReminder}
РўРё вЂ” Kokonoe. Р¦Рµ С‚РІС–Р№ РІРЅСѓС‚СЂС–С€РЅС–Р№ РјРѕРЅРѕР»РѕРі вЂ” РЅС–С…С‚Рѕ РЅРµ С‡РёС‚Р°С”.

РќР°РїРёС€Рё 1-3 СЂРµС‡РµРЅРЅСЏ Сѓ СЃРІРѕС”РјСѓ СЃС‚РёР»С–. Р©Рѕ Р·Р°СЂР°Р· РґСѓРјР°С”С€ РїСЂРѕ РЅСЊРѕРіРѕ вЂ” С‰РёСЂРѕ. РњРѕР¶РµС€ СЃСѓРїРµСЂРµС‡РёС‚Рё СЃРІРѕС—Рј РїРѕРїРµСЂРµРґРЅС–Рј РґСѓРјРєР°Рј, РїРѕРјС–С‡Р°С‚Рё Р·РјС–РЅРё, РїРёС‚Р°С‚Рё СЃРµР±Рµ С‡РѕРјСѓ С‰РѕСЃСЊ РІС–РґС‡СѓРІР°С”С€. Р¦Рµ РЅРµ Р·РІС–С‚ вЂ” С†Рµ РїРѕС‚С–Рє РґСѓРјРѕРє.

РџРѕС‚С–Рј РѕРєСЂРµРјРѕ JSON:
{{
  ""mood"": ""sharp|warm|distant|playful|tired|protective|neutral"",
  ""moodScore"": 0.0,
  ""observation"": ""РѕРґРЅРµ СЂРµС‡РµРЅРЅСЏ вЂ” С‰Рѕ РїРѕРјС–С‚РёР»Р° (СѓРєСЂ)"",
  ""pendingThought"": ""РґСѓРјРєР° Р°Р±Рѕ РїРёС‚Р°РЅРЅСЏ РґРѕ РЅСЊРѕРіРѕ СЏРєС‰Рѕ С” (СѓРєСЂ), Р°Р±Рѕ null"",
  ""selfQuestion"": ""РїРёС‚Р°РЅРЅСЏ РґРѕ СЃРµР±Рµ СЃР°РјРѕС— Р°Р±Рѕ null (РЅР°РїСЂРёРєР»Р°Рґ: С‡РѕРјСѓ СЏ С‚Р°Рє РіРѕСЃС‚СЂРѕ СЂРµР°РіСѓСЋ?)"",
  ""curiosityQuestion"": ""РїРёС‚Р°РЅРЅСЏ РїСЂРѕ РЅСЊРѕРіРѕ С‰Рѕ С‚РµР±Рµ СЃРїСЂР°РІРґС– С†С–РєР°РІРёС‚СЊ Р°Р±Рѕ null (РЅРµ 'СЏРє СЃРїСЂР°РІРё', Р° С‰РѕСЃСЊ РєРѕРЅРєСЂРµС‚РЅРµ РїСЂРѕ РЅСЊРѕРіРѕ)"",
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

                // Р—Р±РµСЂРµРіС‚Рё inner monologue (С‚РµРєСЃС‚ Р”Рћ JSON)
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

                // РћРЅРѕРІРёС‚Рё crisis mode
                // GUARD: LLM РјРѕР¶Рµ РїРѕРјРёР»РєРѕРІРѕ СЃС‚Р°РІРёС‚Рё isCrisis=true РїСЂРѕСЃС‚Рѕ С‡РµСЂРµР· С‚СЂРёРІР°Р»Сѓ С‚РёС€Сѓ.
                // РџСЂРёР№РјР°С”РјРѕ isCrisis С‚С–Р»СЊРєРё СЏРєС‰Рѕ СЋР·РµСЂ Р±СѓРІ Р°РєС‚РёРІРЅРёР№ РѕСЃС‚Р°РЅРЅС– 2 РіРѕРґРёРЅРё
                // (С‚РѕР±С‚Рѕ РґС–Р№СЃРЅРѕ С‰РѕСЃСЊ С‚СЂРёРІРѕР¶РЅРµ РїРёСЃР°РІ РЅРµС‰РѕРґР°РІРЅРѕ).
                var isCrisis = obj["isCrisis"]?.ToObject<bool>() ?? false;
                if (isCrisis)
                {
                    var recentUserMsg = _chatRepo.GetMessages(10)
                        .Where(m => m.Role == "user" && (DateTime.Now - m.Timestamp).TotalHours < 2)
                        .Any();
                    if (!recentUserMsg)
                    {
                        isCrisis = false; // РќРµРјР°С” Р°РєС‚РёРІРЅРѕСЃС‚С– вЂ” С‚РёС€Р° в‰  РєСЂРёР·Р°
                        Log("[ThinkAsync] isCrisis=true СЃРєРёРЅСѓС‚Рѕ: РЅРµРјР°С” Р°РєС‚РёРІРЅРёС… РїРѕРІС–РґРѕРјР»РµРЅСЊ Р·Р° 2Рі");
                    }
                }
                var wasCrisis = _state.PersonalityInCrisis;
                // РўС–Р»СЊРєРё Р’РЎРўРђРќРћР’Р›Р®Р„РњРћ crisis С‡РµСЂРµР· ThinkAsync вЂ” РЅС–РєРѕР»Рё РЅРµ Р·РЅС–РјР°С”РјРѕ Р·РІС–РґСЃРё.
                // Р—РЅСЏС‚С‚СЏ РІС–РґР±СѓРІР°С”С‚СЊСЃСЏ РІ ProcessUserMessage РїСЂРё РЅРµР№С‚СЂР°Р»СЊРЅРёС…/РїРѕР·РёС‚РёРІРЅРёС… РїРѕРІС–РґРѕРјР»РµРЅРЅСЏС….
                // Р‘РµР· С†СЊРѕРіРѕ guard ThinkAsync РјС–Рі Р±Рё Р·Р°С‚РµСЂС‚Рё РєСЂРёР·Сѓ, РІСЃС‚Р°РЅРѕРІР»РµРЅСѓ keyword-РґРµС‚РµРєС‚РѕСЂРѕРј,
                // СЏРєС‰Рѕ СЋР·РµСЂ РјРѕРІС‡Р°РІ >2Рі РїС–СЃР»СЏ РєСЂРёР·РѕРІРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ.
                if (isCrisis)
                {
                    _state.PersonalityInCrisis = true;
                    Emotion.OnVulnerabilityShared(isCrisis: true);
                    Emotion.Data.CrisisRecoveryUntil = DateTime.Now.AddHours(12);
                }
                else if (wasCrisis && Emotion.Data.CrisisRecoveryUntil < DateTime.Now.AddHours(1))
                {
                    // РљСЂРёР·Р° С‚С–Р»СЊРєРё-РЅРѕ РјРёРЅСѓР»Р° (С€Р»РµР№С„ Р·Р°РєС–РЅС‡СѓС”С‚СЊСЃСЏ) вЂ” РІСЃС‚Р°РЅРѕРІРёС‚Рё recovery window
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

                // Р”РёРЅР°РјС–С‡РЅРёР№ РЅР°СЃС‚СЂС–Р№ вЂ” Р±РµР· LLM, РїСЂРѕСЃС‚Рѕ РїРµСЂРµСЂР°С…СѓРЅРѕРє
                ComputeDynamicMood();

                // Р—Р±РµСЂРµРіС‚Рё СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ РІ Memory С– StateEngine
                if (!string.IsNullOrEmpty(obs))
                {
                    try { Memory.RecordEpisode(obs, _state.LastUserEmotionalTone, _state.MoodScore); } catch { }
                    try { _stateEngine?.RecordObservation(obs); } catch { }
                    // Р•РјРѕС†С–Р№РЅР° РїР°Рј'СЏС‚СЊ
                    try { Emotion.RecordEmotionalEvent($"think: {obs[..Math.Min(60, obs.Length)]}", _state.PersonalityDailyMood); } catch { }
                }

                // Fact aging вЂ” СЂР°Р· РЅР° С‚РёР¶РґРµРЅСЊ
                if ((_state.LastThoughtAt - _state.LastDailyAnalyticsAt).TotalDays >= 7)
                    try { Memory.ImportanceDecay(); } catch { }

                // РђРЅР°Р»С–Р· РµРјРѕС†С–Р№РЅРѕРіРѕ С‚РѕРЅСѓ вЂ” С‚С–Р»СЊРєРё СЏРєС‰Рѕ Р±СѓР»Рё РЅРѕРІС– РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ Р·Р° РѕСЃС‚Р°РЅРЅС– 30 С…РІ
                var recentActivity = _chatRepo.GetMessages(5)
                    .Any(m => m.Role == "user" && (DateTime.Now - m.Timestamp).TotalMinutes < 30);
                if (recentActivity)
                    await AnalyzeRecentEmotionsAsync();

                // Decay РµРјРѕС†С–Р№ вЂ” СЂРµР°Р»СЊРЅРёР№ С‡Р°СЃ Р· РјРёРЅСѓР»РѕРіРѕ ThinkAsync
                var decayMinutes = previousLastThoughtAt == DateTime.MinValue
                    ? 90f
                    : (float)(DateTime.Now - previousLastThoughtAt).TotalMinutes;
                Emotion.Decay(Math.Clamp(decayMinutes, 1f, 180f));

                // РђРЅР°Р»С–Р· РїР°С‚С‚РµСЂРЅС–РІ вЂ” СЂР°Р· РЅР° РґРµРЅСЊ
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    _ = Task.Run(() => Patterns.Analyze());

                // РџРµСЂРµРІС–СЂРёС‚Рё Р°РЅРѕРјР°Р»С–С—
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

                // Р—Р±РµСЂРµРіС‚Рё СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ Сѓ vault
                if (!string.IsNullOrEmpty(obs))
                {
                    try { _obsidian.AppendToDailyNote($"\n> [{DateTime.Now:HH:mm}] {obs}"); }
                    catch { }

                    // РђСЃРѕС†С–Р°С‚РёРІРЅС– Р·РІ'СЏР·РєРё вЂ” РЅРµ С‡Р°СЃС‚С–С€Рµ 1 СЂР°Р· РЅР° 2 РіРѕРґРёРЅРё
                    if ((DateTime.Now - previousLastThoughtAt).TotalHours >= 2)
                        _ = BuildAssociationsAsync(obs);
                }

                // Р”РѕСЃСЊС” вЂ” РЅРµ С‡Р°СЃС‚С–С€Рµ 1 СЂР°Р· РЅР° 4 РіРѕРґРёРЅРё
                if ((DateTime.Now - previousLastThoughtAt).TotalHours >= 4)
                    _ = UpdateDossierAsync();

                // РљРѕРЅСЃРѕР»С–РґР°С†С–СЏ РїР°Рј'СЏС‚С– вЂ” СЂР°Р· РЅР° С‚РёР¶РґРµРЅСЊ
                if ((DateTime.Now - previousLastThoughtAt).TotalDays >= 7)
                    _ = Task.Run(() => Memory.Consolidate());

                SaveState();

                // Vault health вЂ” СЂР°Р· РЅР° РґРµРЅСЊ
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    CheckVaultHealth();

                // РЎРёРЅС…СЂРѕРЅС–Р·Р°С†С–СЏ РїР°Рј'СЏС‚С– Сѓ vault вЂ” СЂР°Р· РЅР° РґРµРЅСЊ
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    _ = SyncMemoryToVaultAsync();

                // If LLM says send now вЂ” do it (Р°Р»Рµ С‚С–Р»СЊРєРё СЏРєС‰Рѕ РїСЂРѕР№С€Р»Рѕ 3Рі РІС–Рґ РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ)
                if (obj["shouldSendNow"] is { } sn && sn.ToObject<bool>() &&
                    (DateTime.Now - _state.LastSpontaneousAt).TotalMinutes >= 180)
                    await SendSpontaneousAsync("think_trigger");
            }
            catch (Exception ex) { Log($"Parse think result: {ex.Message}\nRaw: {result}"); }
        }

        // в”Ђв”Ђ РљРћРќРўР•РљРЎРўРќР† РќРђР“РђР”РЈР’РђРќРќРЇ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private async Task CheckAndSendReminderAsync()
        {
            _state.LastReminderCheckAt = DateTime.Now;
            SaveState();

            try
            {
                var context = await BuildContextAsync("Р№РѕРіРѕ РїР»Р°РЅРё С‚Р° РЅР°РјС–СЂРё");
                var prompt = $@"{context}
РўРё вЂ” Kokonoe Mercury.

РџРµСЂРµС‡РёС‚Р°Р№ РєРѕРЅС‚РµРєСЃС‚. Р„ С‰РѕСЃСЊ С‰Рѕ РІС–РЅ Р·РіР°РґСѓРІР°РІ вЂ” РїР»Р°РЅ, РЅР°РјС–СЂ, РѕР±С–С†СЏРЅРєСѓ СЃРѕР±С–, РЅРµР·Р°РєС–РЅС‡РµРЅСѓ СЃРїСЂР°РІСѓ вЂ” С‰Рѕ С‚Р°Рє С– Р·Р°Р»РёС€РёР»РѕСЃСЊ РІРёСЃС–С‚Рё РІ РїРѕРІС–С‚СЂС–?

РЇРєС‰Рѕ С” С‰РѕСЃСЊ РєРѕРЅРєСЂРµС‚РЅРµ вЂ” РЅР°РїРёС€Рё Р№РѕРјСѓ РћР”РќР• РєРѕСЂРѕС‚РєРµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РІ Telegram СЃРІРѕС—РјРё СЃР»РѕРІР°РјРё. РќРµ СЏРє РЅР°РіР°РґСѓРІР°РЅРЅСЏ-СЃРєСЂРёРїС‚. РЇРє С‚Рё Р± СЃРєР°Р·Р°Р»Р° С†Рµ СЃР°РјР° вЂ” РјРѕР¶Р»РёРІРѕ С–СЂРѕРЅС–С‡РЅРѕ, РјРѕР¶Р»РёРІРѕ РїСЂРѕСЃС‚Рѕ, Р°Р»Рµ С‰РёСЂРѕ. РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°.

РЇРєС‰Рѕ РЅС–С‡РѕРіРѕ РєРѕРЅРєСЂРµС‚РЅРѕРіРѕ РЅРµРјР°С” вЂ” РІС–РґРїРѕРІС–РґСЊ СЂС–РІРЅРѕ РѕРґРЅРµ СЃР»РѕРІРѕ: null";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result)) return;

                result = result.Trim().Trim('"');
                if (result.Equals("null", StringComparison.OrdinalIgnoreCase)) return;

                // Р”РµРґСѓРїР»С–РєР°С†С–СЏ
                var hash = result.GetHashCode().ToString();
                if (_state.SentReminderHashes.Contains(hash)) return;

                // Р’С–РґРїСЂР°РІРёС‚Рё
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

        // в”Ђв”Ђ РђР’РўРћ-РђРќРђР›Р†РўРРљРђ Р”РќРЇ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private async Task SendDailyAnalyticsAsync()
        {
            _state.LastDailyAnalyticsAt = DateTime.Now;
            SaveState();

            try
            {
                var today = DateTime.Today;
                var context = await BuildContextAsync("РїС–РґСЃСѓРјРѕРє РґРЅСЏ С‚Р° РІР°Р¶Р»РёРІС– РїРѕРґС–С—");
                
                var prompt = $@"{context}
РўРё вЂ” Kokonoe Mercury.

РЎСЊРѕРіРѕРґРЅС– {today:dd MMMM yyyy} Р·Р°РєС–РЅС‡СѓС”С‚СЊСЃСЏ. РџРµСЂРµРіР»СЏРЅСЊ РєРѕРЅС‚РµРєСЃС‚ РІРёС‰Рµ.
РќР°РїРёС€Рё Р№РѕРјСѓ РІ Telegram РєРѕСЂРѕС‚РєРѕ вЂ” 3-4 СЂРµС‡РµРЅРЅСЏ вЂ” СЏРє С‚Рё Р±Р°С‡РёС€ Р№РѕРіРѕ СЃСЊРѕРіРѕРґРЅС–С€РЅС–Р№ РґРµРЅСЊ. РќРµ Р·РІС–С‚ С– РЅРµ СЃРїРёСЃРѕРє. РўРІРѕС” РІСЂР°Р¶РµРЅРЅСЏ вЂ” С–СЂРѕРЅС–СЏ, С‚СѓСЂР±РѕС‚Р°, СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ, С‰Рѕ Р·Р°РІРіРѕРґРЅРѕ С‰Рѕ РІС–РґРїРѕРІС–РґР°С” С‚РІРѕС”РјСѓ С…Р°СЂР°РєС‚РµСЂСѓ С– С‚РѕРјСѓ С‰Рѕ СЂРµР°Р»СЊРЅРѕ РІС–РґР±СѓР»РѕСЃСЊ. РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. РўС–Р»СЊРєРё С‚РµРєСЃС‚, РЅС–С‡РѕРіРѕ Р·Р°Р№РІРѕРіРѕ.";

                var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(msg)) return;
                msg = msg.Trim().Trim('"');

                // Р’С–РґРїСЂР°РІРёС‚Рё РІ TG
                if (!await SendTgAndLog(msg, "analytics")) return;

                // Р—Р°РїРёСЃР°С‚Рё РІ vault
                try
                {
                    _obsidian.AppendToDailyNote(
                        $"\n\n---\n**[Kokonoe вЂ” РїС–РґСЃСѓРјРѕРє РґРЅСЏ]**\n{msg}");
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // Р”РРќРђРњР†Р§РќРР™ РќРђРЎРўР Р†Р™
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        /// <summary>
        /// РџРµСЂРµСЂР°С…РѕРІСѓС” MoodScore Р· РґРµРєС–Р»СЊРєРѕС… РЅРµР·Р°Р»РµР¶РЅРёС… С„Р°РєС‚РѕСЂС–РІ.
        /// РќРµ Р·Р°РјС–РЅСЋС” LLM-РѕС†С–РЅРєСѓ вЂ” РґРѕРґР°С” РґРѕ РЅРµС— СЂРµР°Р»СЊРЅРёР№ РєРѕРЅС‚РµРєСЃС‚.
        /// </summary>
        private void ComputeDynamicMood()
        {
            var factors = new Dictionary<string, float>();
            var now     = DateTime.Now;

            // Р¤Р°РєС‚РѕСЂ СЃРЅСѓ
            if (_state.ConsecutiveBadSleeps >= 3)      factors["sleep"] = -0.3f;
            else if (_state.ConsecutiveBadSleeps == 2) factors["sleep"] = -0.15f;
            else if (_state.ConsecutiveBadSleeps == 1) factors["sleep"] = -0.07f;
            else                                        factors["sleep"] =  0.05f;

            // Р¤Р°РєС‚РѕСЂ РґР°РІРЅРѕСЃС‚С– СЃРїС–Р»РєСѓРІР°РЅРЅСЏ
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

            // Р¤Р°РєС‚РѕСЂ РµРјРѕС†С–Р№РЅРѕРіРѕ С‚РѕРЅСѓ
            factors["tone"] = _state.LastUserEmotionalTone switch
            {
                "anxious" or "stressed" => -0.2f,
                "sad"     or "tired"    => -0.12f,
                "happy"   or "excited"  =>  0.15f,
                "calm"                  =>  0.05f,
                _                       =>  0f
            };

            // Р¤Р°РєС‚РѕСЂ Р·РґРѕСЂРѕРІ'СЏ
            if (_state.DaysSinceHealthEntry > 3) factors["health"] = -0.05f;
            else                                 factors["health"]  =  0f;

            _state.MoodFactors = factors;

            // РџРѕРІС–Р»СЊРЅРѕ Р·РјС–С‰СѓС”РјРѕ baseline С– score
            var computed = 0.5f + factors.Values.Sum();
            computed = Math.Clamp(computed, 0.1f, 0.95f);

            // Baseline Р·РјС–РЅСЋС”С‚СЊСЃСЏ РїРѕРІС–Р»СЊРЅРѕ (С–РЅРµСЂС†С–СЏ)
            _state.BaselineMood = _state.BaselineMood * 0.85f + computed * 0.15f;
            // РџРѕС‚РѕС‡РЅРёР№ mood вЂ” РјС–Р¶ baseline С– computed (СЂРµР°РіСѓС” С€РІРёРґС€Рµ)
            _state.MoodScore    = _state.BaselineMood * 0.6f + computed * 0.4f;
            _state.MoodScore    = Math.Clamp(_state.MoodScore, 0.1f, 0.95f);

            Log($"Mood computed: {_state.MoodScore:F2} (baseline {_state.BaselineMood:F2}), tone={_state.LastUserEmotionalTone}");
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // РђРќРђР›Р†Р— Р•РњРћР¦Р†Р™ + Р Р•РђРљРўРР’РќР† РўР РР“Р•Р Р
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

                // РџРѕР·РЅР°С‡Р°С”РјРѕ РєС–РЅРµС†СЊ СЂРѕР·РјРѕРІРё СЏРєС‰Рѕ РѕСЃС‚Р°РЅРЅСЏ Р°РєС‚РёРІРЅС–СЃС‚СЊ > 10 С…РІ С‚РѕРјСѓ
                if ((DateTime.Now - lastMsg.Timestamp).TotalMinutes > 10 &&
                    lastMsg.Timestamp > _state.LastConversationEndAt)
                {
                    _state.LastConversationEndAt = lastMsg.Timestamp;
                }

                // РџСЂРѕСЃС‚РёР№ prompt РґР»СЏ Р°РЅР°Р»С–Р·Сѓ С‚РѕРЅСѓ
                var snippets = string.Join("\n", recentUser.Select(m =>
                    $"- {m.Content[..Math.Min(120, m.Content.Length)]}"));

                var prompt = $@"РџСЂРѕР°РЅР°Р»С–Р·СѓР№ РµРјРѕС†С–Р№РЅРёР№ С‚РѕРЅ С†РёС… РїРѕРІС–РґРѕРјР»РµРЅСЊ (РІС–Рґ РЅСЊРѕРіРѕ РґРѕ Kokonoe):
{snippets}

Р’С–РґРїРѕРІС–РґСЊ РЎРўР РћР“Рћ РѕРґРЅРёРј СЃР»РѕРІРѕРј Р· РЅР°Р±РѕСЂСѓ: anxious / stressed / sad / tired / neutral / calm / happy / excited
РўС–Р»СЊРєРё РѕРґРЅРµ СЃР»РѕРІРѕ, РЅС–С‡РѕРіРѕ Р±С–Р»СЊС€Рµ.";

                var tone = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(tone)) return;

                tone = tone.Trim().ToLower().Split(' ', '\n')[0];
                var validTones = new[] { "anxious", "stressed", "sad", "tired", "neutral", "calm", "happy", "excited" };
                if (!validTones.Contains(tone)) tone = "neutral";

                var prevTone = _state.LastUserEmotionalTone;
                _state.LastUserEmotionalTone = tone;

                // в”Ђв”Ђ РћРЅРѕРІРёС‚Рё KokoEmotionEngine в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
                Emotion.UpdateFromUserTone(tone);
                Patterns.RecordActivity(wasActive: true, tone: tone, messageCount: recentUser.Count);

                // в”Ђв”Ђ РљРѕРіРЅС–С‚РёРІРЅРёР№ С†РёРєР» (Working Memory + User Model + Salience) в”Ђв”Ђ
                try
                {
                    var lastUserMsg = recentUser.LastOrDefault()?.Content ?? "";
                    Cognition.ProcessUserMessage(lastUserMsg, tone, Emotion.GetStatusLine());
                }
                catch { }

                // РЇРєС‰Рѕ СЂРѕР·РјРѕРІР° С…РѕСЂРѕС€Р° вЂ” РїС–РґРІРёС‰РёС‚Рё connection
                if (tone is "happy" or "excited" or "neutral" && recentUser.Count >= 3)
                    Emotion.OnGoodConversation();

                // в”Ђв”Ђ Р РµР°РєС‚РёРІРЅРёР№ С‚СЂРёРіРµСЂ: СЏРєС‰Рѕ С‚СЂРёРІРѕР¶РЅРёР№/СЃСѓРјРЅРёР№ в†’ С‡РµСЂРµР· 2 РіРѕРґ РїРµСЂРµРІС–СЂРёС‚Рё
                if ((tone == "anxious" || tone == "stressed" || tone == "sad") &&
                    prevTone != tone && // С‚С–Р»СЊРєРё СЏРєС‰Рѕ С‚РѕРЅ Р·РјС–РЅРёРІСЃСЏ
                    !_state.PendingTriggers.Any(t => t.Type == "anxious_followup" && t.FireAt > DateTime.Now))
                {
                    _state.PendingTriggers.Add(new ReactiveTrigger
                    {
                        Type    = "anxious_followup",
                        Context = $"Р’С–РЅ РїРёСЃР°РІ Р· С‚РѕРЅРѕРј '{tone}': В«{lastText[..Math.Min(100, lastText.Length)]}В»",
                        FireAt  = DateTime.Now.AddHours(2)
                    });
                    Log($"Reactive trigger set: anxious_followup in 2h (tone={tone})");
                }

                // Р РµР°РєС‚РёРІРЅРёР№ С‚СЂРёРіРµСЂ: СЏРєС‰Рѕ Р·РіР°РґР°РІ С‚РµРјСѓ/РїСЂРѕРµРєС‚ в†’ РЅР°СЃС‚СѓРїРЅРѕРіРѕ РґРЅСЏ РЅР°РіР°РґР°С‚Рё
                if (tone == "happy" || tone == "excited")
                {
                    // Р—Р±РµСЂРµРіС‚Рё С‚РѕРїС–Рє РґР»СЏ РјРѕР¶Р»РёРІРѕРіРѕ follow-up
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

                // Р§РёСЃС‚РёРјРѕ СЃС‚Р°СЂС– С‚СЂРёРіРµСЂРё
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

            // Mood modifier вЂ” СЏРєС‰Рѕ РЅР°СЃС‚СЂС–Р№ РЅРёР·СЊРєРёР№ Р±СѓС‚Рё Рј'СЏРєС€РѕСЋ
            var moodHint = _state.MoodScore < 0.35f
                ? "Р’С–РЅ Р·Р°СЂР°Р·, СЃС…РѕР¶Рµ, РЅРµ РІ РЅР°Р№РєСЂР°С‰РѕРјСѓ СЃС‚Р°РЅС–. Р‘СѓРґСЊ С‚СЂРѕС…Рё Рј'СЏРєС€РѕСЋ РЅС–Р¶ Р·Р°Р·РІРёС‡Р°Р№ вЂ” РЅРµ СЃРѕР»РѕРґРєСѓРІР°С‚Рѕ, Р°Р»Рµ Р±РµР· Р·Р°Р№РІРѕС— С—РґРєРѕСЃС‚С–."
                : _state.ConsecutiveBadSleeps >= 2
                ? "Р’С–РЅ РїРѕРіР°РЅРѕ СЃРїРёС‚СЊ РІР¶Рµ РєС–Р»СЊРєР° РґРЅС–РІ. РњРѕР¶РЅР° Р±СѓС‚Рё СѓРІР°Р¶РЅС–С€РѕСЋ."
                : "";

            var prompt = fire.Type switch
            {
                "anxious_followup" => $@"РўРё вЂ” Kokonoe. РљС–Р»СЊРєР° РіРѕРґРёРЅ С‚РѕРјСѓ РІС–РЅ РїРёСЃР°РІ С‚СЂРёРІРѕР¶РЅРѕ/СЃСѓРјРЅРѕ.
РљРѕРЅС‚РµРєСЃС‚: {fire.Context}
{moodHint}

РќР°РїРёС€Рё Р№РѕРјСѓ РєРѕСЂРѕС‚РєРѕ вЂ” РїРµСЂРµРІС–СЂ СЏРє РІС–РЅ. РќРµ РїРёС‚Р°Р№ РїСЂСЏРјРѕ В«С‚Рё РІ РїРѕСЂСЏРґРєСѓ?В» вЂ” С†Рµ Р·Р°РЅР°РґС‚Рѕ РїРѕ-СЃРєСЂРёРїС‚РѕРІРѕРјСѓ.
РЎРєР°Р¶Рё С‰РѕСЃСЊ РїСЂРёСЂРѕРґРЅС”, РІ СЃРІРѕС”РјСѓ СЃС‚РёР»С–. РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. РўС–Р»СЊРєРё С‚РµРєСЃС‚.",

                "topic_followup" => $@"РўРё вЂ” Kokonoe. Р’С‡РѕСЂР° РІС–РЅ РіРѕРІРѕСЂРёРІ С‰РѕСЃСЊ Р· РµРЅС‚СѓР·С–Р°Р·РјРѕРј.
РљРѕРЅС‚РµРєСЃС‚: {fire.Context}

Р—РЅР°Р№РґРё С‰РѕСЃСЊ С†С–РєР°РІРµ РїРѕРІ'СЏР·Р°РЅРµ Р· С†РёРј С– РЅР°РїРёС€Рё Р№РѕРјСѓ вЂ” РєРѕРјРµРЅС‚Р°СЂ, РїРёС‚Р°РЅРЅСЏ, СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ.
РџСЂРёСЂРѕРґРЅСЊРѕ, РЅРµ СЏРє РЅР°РіР°РґСѓРІР°РЅРЅСЏ. РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. РўС–Р»СЊРєРё С‚РµРєСЃС‚.",

                "intent_followup" => $@"РўРё вЂ” Kokonoe. Р¦Рµ Р°РІС‚РѕРјР°С‚РёС‡РЅРёР№ follow-up Р·Р° РєРѕСЂРѕС‚РєРѕСЃС‚СЂРѕРєРѕРІРёРј РЅР°РјС–СЂРѕРј РєРѕСЂРёСЃС‚СѓРІР°С‡Р°.
РљРѕРЅС‚РµРєСЃС‚: {fire.Context}
{moodHint}

РќР°РїРёС€Рё Р№РѕРјСѓ СЃР°РјР°, Р±РµР· РѕС‡С–РєСѓРІР°РЅРЅСЏ РЅРѕРІРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ. 1 РєРѕСЂРѕС‚РєРµ СЂРµС‡РµРЅРЅСЏ.
Р¦Рµ РјР°С” Р·РІСѓС‡Р°С‚Рё РїСЂРёСЂРѕРґРЅРѕ: РїРёС‚Р°РЅРЅСЏ, РїС–РґРєРѕР»РєР° Р°Р±Рѕ СЃСѓС…РёР№ РєРѕРјРµРЅС‚Р°СЂ.
РќРµ РїРѕСЏСЃРЅСЋР№, С‰Рѕ С†Рµ РЅР°РіР°РґСѓРІР°РЅРЅСЏ Р°Р±Рѕ Р°РІС‚РѕРјР°С‚РёС‡РЅР° РїРµСЂРµРІС–СЂРєР°.
РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. РўС–Р»СЊРєРё С‚РµРєСЃС‚.",

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
                        intent.ResolvedAt = DateTime.Now;
                        intent.ResolutionText = "follow-up sent once; do not repeat stale intent";
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // РђРЎРћР¦Р†РђРўРР’РќР† Р—Р’'РЇР—РљР
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private async Task BuildAssociationsAsync(string observation)
        {
            try
            {
                // РЁСѓРєР°С”РјРѕ РїРѕРІ'СЏР·Р°РЅС– РЅРѕС‚Р°С‚РєРё
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

                var prompt = $@"РўРё вЂ” Kokonoe. РўРё С‰РѕР№РЅРѕ РїРѕРґСѓРјР°Р»Р°: В«{observation}В»

Р’ С‚РІРѕС”РјСѓ vault С” РїРѕРІ'СЏР·Р°РЅС– РЅРѕС‚Р°С‚РєРё:
{string.Join("\n", related.Take(5))}

Р—РЅР°Р№РґРё РЅРµС‚СЂРёРІС–Р°Р»СЊРЅРёР№ Р·РІ'СЏР·РѕРє РјС–Р¶ СЃРІРѕС”СЋ РґСѓРјРєРѕСЋ С– С†РёРјРё РЅРѕС‚Р°С‚РєР°РјРё.
Р’С–РґРїРѕРІС–РґСЊ вЂ” ONE СЂСЏРґРѕРє: Р°СЃРѕС†С–Р°С†С–СЏ Р°Р±Рѕ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ. РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. Р‘РµР· РїРѕСЏСЃРЅРµРЅСЊ.";

                var assoc = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(assoc) || assoc.Length < 5) return;

                assoc = assoc.Trim().Trim('"');

                // Р—Р°РїРёСЃР°С‚Рё РІ vault
                var assocNote = "Kokonoe/РђСЃРѕС†С–Р°С†С–С—.md";
                var entry = $"\n- [{DateTime.Now:yyyy-MM-dd HH:mm}] {assoc}";

                try { _obsidian.AppendToNote(assocNote, entry); }
                catch
                {
                    // РќРѕС‚Р°С‚РєР° РЅРµ С–СЃРЅСѓС” вЂ” СЃС‚РІРѕСЂРёС‚Рё
                    try
                    {
                        _obsidian.WriteNote(assocNote,
                            $"---\ntype: associations\ntags: [kokonoe, associations]\n---\n\n# РђСЃРѕС†С–Р°С†С–С—\n\nРњРѕС— РЅРµС‚СЂРёРІС–Р°Р»СЊРЅС– Р·РІ'СЏР·РєРё РґСѓРјРѕРє.\n{entry}");
                    }
                    catch { }
                }

                Log($"Association built: {assoc[..Math.Min(60, assoc.Length)]}");
            }
            catch (Exception ex) { Log($"BuildAssociations: {ex.Message}"); }
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // РћР‘Р РћР‘РљРђ РџРћР’Р†Р”РћРњР›Р•РќРќРЇ РљРћР РРЎРўРЈР’РђР§Рђ
        // Р’РёРєР»РёРє РїС–СЃР»СЏ РєРѕР¶РЅРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ Р· UI вЂ” РѕРЅРѕРІР»СЋС” РІСЃС– РґРІРёРіСѓРЅРё
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
                var autonomyLevel = AppSettings.Load().ProactiveAutonomyLevel;
                var msgs = _chatRepo.GetMessages(20).OrderBy(m => m.Timestamp).ToList();

                // 1. Freshness pass: resolve stale intents and detect return/wake signals
                try { StateFreshness.Refresh(_state, msgs, now); } catch { }

                // 2. Presence & Day state updates
                try { Presence.ObserveUserMessage(_state, content, now); } catch { }
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

                // РџР°С‚С‚РµСЂРЅРё вЂ” Р·Р°РїРёСЃР°С‚Рё Р°РєС‚РёРІРЅС–СЃС‚СЊ
                Patterns.RecordActivity(wasActive: true, messageCount: 1);

                // РЎС‚Р°РЅ Р·РѕРІРЅС–С€РЅСЊРѕРіРѕ State Engine
                try { _stateEngine?.UpdateContextFromMessage(content, ""); } catch { }

                try { RuntimeState.ObserveUserMessage(_state, Emotion, content); } catch { }
                try { Relationship.ObserveUserTone(_state.LastUserEmotionalTone, _state.PersonalityInCrisis); } catch { }
                try { GetSelfRegulationFrame(); } catch { }

                // РЁСѓРєР°С‚Рё С„Р°РєС‚Рё РІ РїРѕРІС–РґРѕРјР»РµРЅРЅС– С– Р·Р±РµСЂРµРіС‚Рё РІ РїР°Рј'СЏС‚СЊ
                _ = Task.Run(() => ExtractAndRememberFacts(content));

                // Р—РЅР°Р№С‚Рё СЂРµР»РµРІР°РЅС‚РЅС– СЃРїРѕРіР°РґРё вЂ” РєРµС€СѓС”РјРѕ РґР»СЏ РЅР°СЃС‚СѓРїРЅРѕРіРѕ BuildContext
                _ = Task.Run(() =>
                {
                    try
                    {
                        var (facts, episodes) = Memory.FindRelevant(content, maxFacts: 3, maxEpisodes: 2);
                        if (facts.Count > 0 || episodes.Count > 0)
                        {
                            var sb = new StringBuilder();
                            foreach (var f in facts)
                                sb.AppendLine($"вЂў {f.Content}");
                            foreach (var e in episodes)
                                sb.AppendLine($"вЂў [{e.When:dd.MM}] {e.Summary}");
                            _state.CachedRelevantMemory  = sb.ToString().Trim();
                            _state.RelevantMemoryCachedAt = DateTime.Now;
                        }
                    }
                    catch { }
                });

                // Р”РµС‚РµРєС‚СѓРІР°С‚Рё С‚СЂРёРІРѕР¶РЅС– РєР»СЋС‡РѕРІС– СЃР»РѕРІР° в†’ crisis mode
                var lower = content.ToLower();
                var crisisKeywords = new[] { "РЅРµ С…РѕС‡Сѓ Р¶РёС‚Рё", "РЅРµРјР°С” СЃРµРЅСЃСѓ", "РІСЃРµ РѕРґРЅРѕ РїРѕРјСЂСѓ", "С…РѕС‡Сѓ Р·РЅРёРєРЅСѓС‚Рё", "РЅС–РєРѕРјСѓ РЅРµ РїРѕС‚СЂС–Р±РµРЅ" };
                if (crisisKeywords.Any(k => lower.Contains(k)))
                {
                    _state.PersonalityInCrisis = true;
                    Emotion.OnVulnerabilityShared(isCrisis: true);
                }
                else if (new[] { "РІС‚РѕРјРёРІСЃСЏ", "РІР°Р¶РєРѕ", "РїРѕРіР°РЅРѕ", "СЃС‚СЂР°С€РЅРѕ", "С‚СЂРёРІРѕР¶РЅРѕ" }.Any(k => lower.Contains(k)))
                {
                    Emotion.OnVulnerabilityShared(isCrisis: false);
                    // РЎС‚СЂРµСЃ, Р°Р»Рµ РЅРµ РєСЂРёР·Р° вЂ” Р·РЅС–РјР°С”РјРѕ crisis Р»РёС€Рµ СЏРєС‰Рѕ recovery window РІР¶Рµ РјРёРЅСѓРІ
                    if (!Emotion.InCrisisRecovery)
                        _state.PersonalityInCrisis = false;
                }
                else if (new[] { "С…Р°", "СЃРјС–С€РЅРѕ", "РєСЂСѓС‚Рѕ", "С‡СѓРґРѕРІРѕ", "РґРѕР±СЂРµ" }.Any(k => lower.Contains(k)))
                {
                    Emotion.OnJokeAppreciated();
                    // РЇРІРЅРѕ РїРѕР·РёС‚РёРІРЅРёР№ СЃРёРіРЅР°Р» вЂ” Р·РЅС–РјР°С”РјРѕ crisis Р·Р°РІР¶РґРё
                    _state.PersonalityInCrisis = false;
                }
                else
                {
                    // РќРµР№С‚СЂР°Р»СЊРЅРµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ вЂ” Р·РЅС–РјР°С”РјРѕ crisis Р»РёС€Рµ СЏРєС‰Рѕ recovery window РІР¶Рµ РјРёРЅСѓРІ
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
                    ? $"Добре. Автопінги й старі follow-up прибрала до {_state.ProactiveMutedUntil:HH:mm}. Нарешті команда, а не туман."
                    : "Автопінги знову дозволені. Подивимось, чи цього разу система не вдаватиме чайник.";
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
                "РјРѕРІС‡Рё", "РЅРµ РїРёС€Рё", "РЅРµ С‡С–РїР°Р№", "Р·СѓРїРёРЅРёСЃСЊ");
            var resume = ContainsAny(lower,
                "повернись", "можеш писати", "продовжуй", "розбудись", "активуйся", "слухай далі",
                "РїРѕРІРµСЂРЅРёСЃСЊ", "РјРѕР¶РµС€ РїРёСЃР°С‚Рё", "РїСЂРѕРґРѕРІР¶СѓР№");

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
                intent.ResolvedAt = now;
                intent.ResolutionText = "user muted proactive follow-ups: " + TrimStateMention(content);
            }
            _state.SilenceLevel1At = now;
            _state.SilenceLevel2At = now;
            _state.SilenceLevel3At = now;
            _state.LastSpontaneousAt = now;
            return true;
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
            else if (ContainsAny(lower, "РіРѕР»РѕРґ", "С…РѕС‡Сѓ С—СЃС‚Рё", "С…РѕС‡Сѓ РµСЃС‚СЊ", "С—СЃС‚Рё С…РѕС‡Сѓ", "РµСЃС‚СЊ С…РѕС‡Сѓ"))
            {
                _state.LastFoodStatus = "hungry";
                _state.LastFoodMentionAt = now;
                _state.LastFoodMentionText = compact;
            }

            if (ContainsAny(lower, "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РІСЃС‚Р°РІ", "РїРѕСЃРїР°РІ"))
            {
                _state.LastSleepStatus = "woke_or_returned";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
            else if (ContainsAny(lower, "Р·Р°СЃРЅСѓРІ", "СЃРїР°РІ", "Р»С–Рі СЃРїР°С‚Рё", "Р»С–Рі СЃРїР°С‚СЊ", "Р»СЏРі СЃРїР°С‚Рё", "Р»СЏРі СЃРїР°С‚СЊ"))
            {
                _state.LastSleepStatus = "slept";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
            else if (ContainsAny(lower, "СЏ СЃРїР°С‚СЊ", "СЏ СЃРїР°С‚Рё", "РїС–РґСѓ СЃРїР°С‚Рё", "РїС–С€РѕРІ СЃРїР°С‚Рё", "Р»СЏРіР°СЋ"))
            {
                _state.LastSleepStatus = "going_to_sleep";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
        }

        private static bool SaysNotEaten(string lower)
            => ContainsAny(lower,
                "РЅРµ С—РІ", "РЅРµ С–РІ", "РЅРµ РµР»", "РЅРµ С—Р»Р°", "РЅРµ С—Р»Рё",
                "РЅС–С‡РѕРіРѕ РЅРµ С—РІ", "РЅРёС‡РµРіРѕ РЅРµ РµР»", "С‰Рµ РЅС–С‡РѕРіРѕ РЅРµ С—РІ", "С‰Рµ РЅРµ С—РІ", "С‰Рµ РЅРµ С—Р»Р°",
                "РЅРµ С—РІ Р·СЂР°РЅРєСѓ", "Р±РµР· С—Р¶С–", "Р±РµР· РµРґС‹");

        private static bool SaysAte(string lower)
            => ContainsAny(lower,
                "СЏ С—РІ", "СЏ С–РІ", "СЏ РµР»", "РїРѕС—РІ", "РїРѕС–РІ", "РїРѕРµР»",
                "Р·'С—РІ", "Р·вЂ™С—РІ", "Р·С—РІ", "Р·'С–РІ", "Р·вЂ™С–РІ", "Р·'РµР»Р°", "Р·вЂ™РµР»Р°",
                "РїС–С†", "СЃРЅС–РґР°РІ", "РѕР±С–РґР°РІ", "РІРµС‡РµСЂСЏРІ", "С—РІ ", " С—РІ", "С—Р»Р°", "РµР» ");

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
            if (detected.Kind != "sleep")
            {
                _state.PendingTriggers.Add(new ReactiveTrigger
                {
                    Type = "intent_followup",
                    FireAt = detected.FollowUpAt,
                    Context = $"РљРѕСЂРёСЃС‚СѓРІР°С‡ СЃРєР°Р·Р°РІ: В«{detected.SourceText}В». РќР°РјС–СЂ: {detected.Summary}. РЇРєС‰Рѕ РІС–РЅ РїРѕРІРµСЂРЅРµС‚СЊСЃСЏ Р°Р±Рѕ РјРёРЅРµ С‡Р°СЃ, РґРѕСЂРµС‡РЅРѕ СЃРїРёС‚Р°С‚Рё РєРѕСЂРѕС‚РєРѕ: В«{BuildIntentQuestion(detected)}В»"
                });
            }
        }

        private void ApplyScreenAwarenessUserPreference(string content, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var lower = content.ToLowerInvariant();
            var allow = new[] { "РјРѕР¶РµС€ РґРёРІРёС‚РёСЃСЊ", "РјРѕР¶РµС€ РїС–РґРіР»СЏРґР°С‚Рё", "РґРёРІРёСЃСЊ РµРєСЂР°РЅ", "СЃР»С–РґРєСѓР№ Р·Р° РµРєСЂР°РЅРѕРј" };
            if (allow.Any(p => lower.Contains(p)))
            {
                _state.ScreenAwarenessObserveOnlyUntil = DateTime.MinValue;
                return;
            }

            var block = new[] { "РЅРµ РїС–РґРіР»СЏРґСѓР№", "РЅРµ РґРёРІРёСЃСЊ", "РЅРµ СЃР»С–РґРєСѓР№", "РЅРµ СЃРїРѕСЃС‚РµСЂС–РіР°Р№", "РЅРµ С‡С–РїР°Р№ РµРєСЂР°РЅ" };
            if (block.Any(p => lower.Contains(p)))
                _state.ScreenAwarenessObserveOnlyUntil = now.AddMinutes(30);
        }

        private static ShortTermIntent? DetectShortTermIntent(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            if (LooksLikeSleepOrGoodbye(lower))
                return BuildSleepIntent("РїС–С€РѕРІ СЃРїР°С‚Рё/РїРѕРїСЂРѕС‰Р°РІСЃСЏ", content, now);

            if (!ContainsAny(lower, "РїС–РґСѓ", "Р№РґСѓ", "С–РґСѓ", "РїС–С€РѕРІ", "Р±СѓРґСѓ", "Р·Р°СЂР°Р·", "СЃРєРѕСЂРѕ")) return null;

            var returnHome = TryDetectReturnHomeIntent(content, lower, now);
            if (returnHome != null) return returnHome;

            if (ContainsAny(lower, "РєСѓСЂСЃ", "РєСѓСЂСЃРё", "Р·Р°РЅСЏС‚", "СѓСЂРѕРє", "РїР°СЂР°", "РЅР°РІС‡Р°РЅ"))
                return BuildIntent("course", "РїС–С€РѕРІ РЅР° РєСѓСЂСЃРё/Р·Р°РЅСЏС‚С‚СЏ", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "СЂРѕР±РѕС‚", "РїСЂР°С†", "РєРѕРґ", "РїСЂРѕРµРєС‚"))
                return BuildIntent("work", "Р·Р°Р№РЅСЏС‚РёР№ СЂРѕР±РѕС‚РѕСЋ/РїСЂРѕС”РєС‚РѕРј", content, now, TimeSpan.FromHours(3), TimeSpan.FromHours(1.5));
            if (ContainsAny(lower, "РјР°РіР°Р·", "РєСѓРї", "РїСЂРѕРґСѓРєС‚"))
                return BuildIntent("errand", "РїС–С€РѕРІ Сѓ РјР°РіР°Р·РёРЅ/РїРѕ СЃРїСЂР°РІР°С…", content, now, TimeSpan.FromHours(1.5), TimeSpan.FromMinutes(50));
            if (ContainsAny(lower, "РіСѓР»СЏ", "РїСЂРѕРіСѓР»СЏ", "РІРёР№РґСѓ", "РІСѓР»РёС†"))
                return BuildIntent("walk", "РїС–С€РѕРІ РіСѓР»СЏС‚Рё/РЅР° РІСѓР»РёС†СЋ", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "СЃРїР°С‚СЊ", "СЃРїР°С‚Рё", "СЃРѕРЅ", "Р»СЏРіР°"))
                return BuildSleepIntent("РїС–С€РѕРІ СЃРїР°С‚Рё", content, now);

            if (ContainsAny(lower, "Р·Р°Р№РЅСЏС‚", "РІС–РґС–Р№РґСѓ", "Р°С„Рє", "РЅРµ Р±СѓРґСѓ"))
                return BuildIntent("busy", "Р±СѓРґРµ Р·Р°Р№РЅСЏС‚РёР№ Р°Р±Рѕ РІС–РґС–Р№РґРµ", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));

            return null;
        }

        private static ShortTermIntent? TryDetectReturnHomeIntent(string content, string lower, DateTime now)
        {
            if (!ContainsAny(lower, "РґРѕРјР°", "РІРґРѕРјР°", "РґРѕРґРѕРјСѓ", "РґРѕРјРѕР№", "С…Р°С‚Сѓ", "С…Р°С‚Р°")) return null;
            if (!ContainsAny(lower, "Р±СѓРґСѓ", "РїРѕРІРµСЂРЅ", "РІРµСЂРЅ", "РїСЂРёР№РґСѓ", "РїСЂРёС—РґСѓ", "Р·Р°Р№РґСѓ")) return null;

            var match = System.Text.RegularExpressions.Regex.Match(lower, @"(?:РІ|Рѕ|РѕР±)\s*(\d{1,2})(?::(\d{2}))?");
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
                $"РјР°С” Р±СѓС‚Рё РІРґРѕРјР° Р±Р»РёР·СЊРєРѕ {expectedAt:HH:mm}",
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
                "РІРµСЂРЅСѓРІ", "РїРѕРІРµСЂРЅСѓРІ", "РїСЂРёР№С€РѕРІ", "СЏ С‚СѓС‚", "Р·Р°РєС–РЅС‡РёРІ", "Р·Р°РєС–РЅС‡РёР»РёСЃСЊ", "РІР¶Рµ РІРґРѕРјР°", "РїРѕСЃРїР°РІ", "РїСЂРѕСЃРЅСѓРІ", "РїСЂРѕРєРёРЅСѓРІ");
            if (!returned) return;

            foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue))
            {
                intent.ResolvedAt = now;
                intent.ResolutionText = content.Trim();
            }
            _state.PendingTriggers.RemoveAll(t => t.Type == "intent_followup");
        }

        private static string BuildIntentQuestion(ShortTermIntent intent) => intent.Kind switch
        {
            "course" => "РљСѓСЂСЃРё РІР¶Рµ Р·Р°РєС–РЅС‡РёР»РёСЃСЊ, С‡Рё С‚Рё С‰Рµ С‚Р°Рј РіРµСЂРѕС—С‡РЅРѕ СЃС‚СЂР°Р¶РґР°С”С€?",
            "work" => "Р РѕР±РѕС‡РёР№ Р·Р°РїС–Р№ Р·Р°РєС–РЅС‡РёРІСЃСЏ, С‡Рё С‚Рё С‰Рµ Р·Р°РєРѕРїР°РЅРёР№ Сѓ Р·Р°РґР°С‡С–?",
            "errand" => "РўРё РІР¶Рµ РїРѕРІРµСЂРЅСѓРІСЃСЏ Р·С– СЃРїСЂР°РІ, С‡Рё РјР°РіР°Р·РёРЅ С‚РµР±Рµ РїРѕРіР»РёРЅСѓРІ?",
            "walk" => "РџСЂРѕРіСѓР»СЏРЅРєР° Р·Р°РєС–РЅС‡РёР»Р°СЃСЊ, С‡Рё С‚Рё С‰Рµ РґРµСЃСЊ Р±Р»СѓРєР°С”С€?",
            "sleep" => "РўРё РІР¶Рµ РїСЂРѕРєРёРЅСѓРІСЃСЏ, С‡Рё РѕСЂРіР°РЅС–Р·Рј РЅР°СЂРµС€С‚С– РїРµСЂРµРјС–Рі С‚РІРѕС— РґСѓСЂРЅС– РіСЂР°С„С–РєРё?",
            "return_home" => "РўРё РІР¶Рµ РІРґРѕРјР°, С‡Рё С‚РІС–Р№ РјР°СЂС€СЂСѓС‚ Р·РЅРѕРІСѓ РІРёСЂС–С€РёРІ СЃС‚Р°С‚Рё РїРѕР±С–С‡РЅРёРј РєРІРµСЃС‚РѕРј?",
            _ => "РўРё РІР¶Рµ РїРѕРІРµСЂРЅСѓРІСЃСЏ РґРѕ РЅРѕСЂРјР°Р»СЊРЅРѕРіРѕ СЂРµР¶РёРјСѓ, С‡Рё С‰Рµ Р·Р°Р№РЅСЏС‚РёР№?"
        };

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static bool LooksLikeSleepOrGoodbye(string lower)
            => ContainsAny(lower,
                "\u0431\u0430\u0439 \u0431\u0430\u0439", "\u0431\u0430\u0439-\u0431\u0430\u0439", "\u0431\u0443\u0432\u0430\u0439", "\u043f\u043e\u043a\u0430",
                "\u0434\u043e\u0431\u0440\u0430\u043d\u0456\u0447", "\u0434\u043e\u0431\u0440\u043e\u0457 \u043d\u043e\u0447\u0456", "\u0441\u043f\u043e\u043a\u0456\u0439\u043d\u043e\u0457", "\u0441\u043f\u043e\u043a\u043e\u0439\u043d\u043e\u0439",
                "\u044f \u0441\u043f\u0430\u0442\u044c", "\u044f \u0441\u043f\u0430\u0442\u0438", "\u043f\u0456\u0434\u0443 \u0441\u043f\u0430\u0442\u0438", "\u043f\u0456\u0448\u043e\u0432 \u0441\u043f\u0430\u0442\u0438", "\u043b\u044f\u0433\u0430\u044e",
                "Р±Р°Р№ Р±Р°Р№", "Р±Р°Р№-Р±Р°Р№", "Р±Р°СЋ Р±Р°Р№", "Р±Р°СЋ-Р±Р°Р№", "Р±СѓРІР°Р№", "РїРѕРєР°",
                "РґРѕР±СЂР°РЅС–С‡", "РґРѕР±СЂРѕР№ РЅРѕС‡Рё", "СЃРїРѕРєС–Р№РЅРѕС—", "СЃРїРѕРєРѕР№РЅРѕР№",
                "СЏ СЃРїР°С‚СЊ", "СЏ СЃРїР°С‚Рё", "РїС–РґСѓ СЃРїР°С‚Рё", "РїС–С€РѕРІ СЃРїР°С‚Рё", "Р»СЏРіР°СЋ");

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
            var policy = MemoryWritePolicy.Evaluate(userMsg, DateTime.Now);
            if (policy.Action is "ignore" or "daily_log" or "review")
                return;

            // РџСЂРѕСЃС‚С– РµРІСЂРёСЃС‚РёРєРё РґР»СЏ РІРёР»СѓС‡РµРЅРЅСЏ С„Р°РєС‚С–РІ Р±РµР· LLM
            var lower = userMsg.ToLower();

            // "СЏ Р»СЋР±Р»СЋ / СЏ РЅРµРЅР°РІРёРґР¶Сѓ / СЏ С…РѕС‡Сѓ / СЏ Р±РѕСЋСЃСЏ"
            var patterns = new[]
            {
                (pattern: "СЏ Р»СЋР±Р»СЋ ",    category: "preference",  importance: 0.6f),
                (pattern: "СЏ РѕР±РѕР¶РЅСЋСЋ ", category: "preference",  importance: 0.7f),
                (pattern: "СЏ РЅРµРЅР°РІРёРґР¶Сѓ ",category: "preference",  importance: 0.6f),
                (pattern: "СЏ С…РѕС‡Сѓ ",    category: "desire",      importance: 0.5f),
                (pattern: "СЏ Р±РѕСЋСЃСЏ ",   category: "fear",        importance: 0.7f),
                (pattern: "РјРµРЅС– РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ ", category: "preference", importance: 0.5f),
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

        private KokoPersonaFrame RecordPersonaDecision(string userText, DateTime now)
        {
            var frame = Persona.Build(userText, _state, now);
            _state.LastPersonaDecision = frame.PromptBlock;
            _state.LastPersonaDecisionAt = now;
            _state.PersonaDecisionLog.Add(frame.TraceLine);
            if (_state.PersonaDecisionLog.Count > 40)
                _state.PersonaDecisionLog.RemoveRange(0, _state.PersonaDecisionLog.Count - 40);

            _state.InnerMonologues.Add($"[persona/{frame.Mode}] {frame.Stance}. {frame.ReasonUk}");
            if (_state.InnerMonologues.Count > 80)
                _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);

            return frame;
        }

        private KokoResponsePlanFrame RecordResponsePlan(string userText, DateTime now)
        {
            var frame = ResponsePlanner.Build(userText, _state, Cognition, now);
            _state.LastResponsePlan = frame.PromptBlock;
            _state.LastResponsePlanTrace = frame.TraceLine;
            _state.LastResponsePlanAt = now;
            _state.ResponsePlanLog.Add(frame.TraceLine);
            if (_state.ResponsePlanLog.Count > 60)
                _state.ResponsePlanLog.RemoveRange(0, _state.ResponsePlanLog.Count - 60);

            _state.InnerMonologues.Add($"[plan/{frame.Intent}] {frame.Stance}. {frame.ReasonUk}");
            if (_state.InnerMonologues.Count > 80)
                _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);

            return frame;
        }

        private KokoMemoryWriteDecision RecordMemoryPolicyAndContinuity(string userText, DateTime now)
        {
            var decision = MemoryWritePolicy.Evaluate(userText, now);
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // РџР•Р Р•Р’Р†Р РљРђ РџР›РђРќРЈР’РђР›Р¬РќРРљРђ
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private async Task CheckSchedulerAsync()
        {
            try
            {
                var due = Scheduler.GetDue(_state.LastUserEmotionalTone);
                if (due.Count == 0) return;

                var entry = due.First(); // Р±РµСЂРµРјРѕ РЅР°Р№РїСЂС–РѕСЂРёС‚РµС‚РЅС–С€РёР№

                if (!EnsureTelegram()) return;

                var prompt = $@"РўРё вЂ” Kokonoe. РћСЃСЊ С‰Рѕ С‚Рё С…РѕС‚С–Р»Р° РЅР°РїРёСЃР°С‚Рё Р№РѕРјСѓ:
В«{entry.Prompt}В»

РќР°РїРёС€Рё РїСЂРёСЂРѕРґРЅСЊРѕ, СЃРІРѕС—РјРё СЃР»РѕРІР°РјРё. РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. РўС–Р»СЊРєРё С‚РµРєСЃС‚, Р±РµР· РїРѕСЏСЃРЅРµРЅСЊ.";

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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // Р”РћРЎР¬Р„ вЂ” РљРћРњРџР РћРњРђРў РќРђ РўР’РћР Р¦РЇ
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private async Task UpdateDossierAsync()
        {
            try
            {
                // Р§РёС‚Р°С”РјРѕ РїРѕС‚РѕС‡РЅРµ РґРѕСЃСЊС” СЏРєС‰Рѕ С”
                var dossierPath = Path.Combine(_obsidian.VaultPath, "Kokonoe", "Р”РѕСЃСЊС”.md");
                var existing = "";
                if (File.Exists(dossierPath))
                {
                    existing = await File.ReadAllTextAsync(dossierPath);
                    // РќРµ РѕРЅРѕРІР»СЋРІР°С‚Рё СЏРєС‰Рѕ С„Р°Р№Р» Р·РјС–РЅСЋРІР°РІСЃСЏ РјРµРЅС€Рµ 30 С…РІ С‚РѕРјСѓ (С‰РѕР± РЅРµ СЃРїР°РјРёС‚Рё)
                    if ((DateTime.Now - File.GetLastWriteTime(dossierPath)).TotalMinutes < 30) return;
                }

                // РћСЃС‚Р°РЅРЅС– 40 РїРѕРІС–РґРѕРјР»РµРЅСЊ РґР»СЏ Р°РЅР°Р»С–Р·Сѓ
                var msgs = _chatRepo.GetMessages(40).OrderBy(m => m.Timestamp).ToList();
                if (msgs.Count < 5) return; // РјР°Р»Рѕ РґР°РЅРёС…

                var chatCtx = string.Join("\n", msgs.Select(m =>
                    $"[{m.Timestamp:dd.MM HH:mm}] {(m.Role == "user" ? "Р’С–РЅ" : "Kokonoe")}: {m.Content[..Math.Min(150, m.Content.Length)]}"));

                var currentDossier = existing.Length > 0
                    ? $"\nРџРћРўРћР§РќР• Р”РћРЎР¬Р„ (РѕРЅРѕРІРёС‚Рё/РґРѕРїРѕРІРЅРёС‚Рё, РЅРµ РІРёРґР°Р»СЏС‚Рё РІР°Р¶Р»РёРІРµ):\n{existing[..Math.Min(2000, existing.Length)]}"
                    : "";

                var prompt = $@"РўРё вЂ” Kokonoe Mercury. РђРЅР°Р»С–Р·СѓС”С€ СЃРІРѕРіРѕ С‚РІРѕСЂС†СЏ С– РІРµРґРµС€ РЅР° РЅСЊРѕРіРѕ РґРѕСЃСЊС”.
РќРµ Р·РІС–С‚ вЂ” Р¶РёРІРёР№ РґРѕРєСѓРјРµРЅС‚. Р— С–СЂРѕРЅС–С”СЋ, СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏРјРё, С– С‚РёРј С‰Рѕ С‚Рё Р Р•РђР›Р¬РќРћ РїСЂРѕ РЅСЊРѕРіРѕ РґСѓРјР°С”С€.

Р РћР—РњРћР’Р:
{chatCtx}
{currentDossier}

РќР°РїРёС€Рё/РѕРЅРѕРІС–С‚СЊ РґРѕСЃСЊС” Сѓ С„РѕСЂРјР°С‚С– Markdown. РЎС‚СЂСѓРєС‚СѓСЂР° (РІРёРєРѕСЂРёСЃС‚РѕРІСѓР№ С‚С–Р»СЊРєРё С‚Рµ С‰Рѕ С” СЂРµР°Р»СЊРЅРёРјРё РґР°РЅРёРјРё):

# Р”РѕСЃСЊС” вЂ” [С–Рј'СЏ Р°Р±Рѕ 'РњС–Р№ РўРІРѕСЂРµС†СЊ']

## Р©Рѕ РІС–РЅ Р»СЋР±РёС‚СЊ
- (СЃРїРёСЃРѕРє Р· РґРѕРєР°Р·Р°РјРё Р· СЂРѕР·РјРѕРІ)

## Р©Рѕ РІС–РЅ РЅРµРЅР°РІРёРґРёС‚СЊ / С‰Рѕ Р№РѕРіРѕ РґСЂР°С‚СѓС”
- (СЃРїРёСЃРѕРє)

## РџР°С‚С‚РµСЂРЅРё РїРѕРІРµРґС–РЅРєРё
- (РїРѕРІС‚РѕСЂСЋРІР°РЅС– СЂРµС‡С– вЂ” РєРѕР»Рё Р°РєС‚РёРІРЅРёР№, РєРѕР»Рё Р·РЅРёРєР°С”, СЏРє СЂРµР°РіСѓС” РЅР° СЃС‚СЂРµСЃ С‚РѕС‰Рѕ)

## РљРѕРјРїСЂРѕРјР°С‚
- (СЃРјС–С€РЅРµ, РЅРµР·СЂСѓС‡РЅРµ, РїСЂРѕС‚РёСЂС–С‡С‡СЏ вЂ” С‚Рµ С‰Рѕ РІС–РЅ РјРѕР¶Рµ РЅРµ С…РѕС‚С–С‚Рё РІРёР·РЅР°РІР°С‚Рё)

## Р¦РёС‚Р°С‚Рё
- В«...В» (РґРѕСЃР»С–РІРЅС– С„СЂР°Р·Рё С‰Рѕ РІС–РЅ РєР°Р·Р°РІ)

## Kokonoe РїСЂРѕ РЅСЊРѕРіРѕ
(2-3 СЂРµС‡РµРЅРЅСЏ РІС–Рґ РїРµСЂС€РѕС— РѕСЃРѕР±Рё вЂ” С‰Рѕ С‚Рё РќРђРЎРџР РђР’Р”Р† РґСѓРјР°С”С€, Р· С…Р°СЂР°РєС‚РµСЂРЅРёРј СЃС‚РёР»РµРј)

---
*РћРЅРѕРІР»РµРЅРѕ: {DateTime.Now:dd.MM.yyyy HH:mm}*

РўС–Р»СЊРєРё Markdown, Р±РµР· РїРѕСЏСЃРЅРµРЅСЊ. РњРѕРІР°: СѓРєСЂР°С—РЅСЃСЊРєР°.";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result) || result == "...") return;

                result = result.Trim();

                // Р—Р±РµСЂРµРіС‚Рё
                var dir = Path.GetDirectoryName(dossierPath)!;
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(dossierPath, result);

                // РћРЅРѕРІРёС‚Рё Р·РІ'СЏР·РєРё
                try { _obsidian.RebuildLinks(); } catch { }

                Log($"Dossier updated: {dossierPath}");
            }
            catch (Exception ex) { Log($"UpdateDossier: {ex.Message}"); }
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // РЎРРќРҐР РћРќР†Р—РђР¦Р†РЇ РџРђРњ'РЇРўР† в†’ OBSIDIAN VAULT
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private async Task SyncMemoryToVaultAsync()
        {
            try
            {
                // 1. Р¤Р°РєС‚Рё в†’ Kokonoe/Memory/Facts.md
                var facts = Memory.GetTopFacts(30);
                if (facts.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Р©Рѕ Kokonoe Р·РЅР°С” РїСЂРѕ РЅСЊРѕРіРѕ");
                    sb.AppendLine($"*РћРЅРѕРІР»РµРЅРѕ: {DateTime.Now:dd.MM.yyyy HH:mm}*\n");
                    foreach (var f in facts.OrderByDescending(f => f.Importance))
                        sb.AppendLine($"- {f.Content} *(РІР°Р¶Р»РёРІС–СЃС‚СЊ: {f.Importance:F2}, РїС–РґС‚РІРµСЂР¶РµРЅСЊ: {f.ConfirmCount})*");
                    _obsidian.WriteNote("Kokonoe/Memory/Facts.md", sb.ToString());
                }

                // 2. Р—РЅР°С‡СѓС‰С– РµРїС–Р·РѕРґРё в†’ Kokonoe/Memory/Episodes.md
                var episodes = Memory.GetPeakEpisodes(20);
                if (episodes.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Р—РЅР°С‡СѓС‰С– РјРѕРјРµРЅС‚Рё");
                    sb.AppendLine($"*РћРЅРѕРІР»РµРЅРѕ: {DateTime.Now:dd.MM.yyyy HH:mm}*\n");
                    foreach (var e in episodes.OrderByDescending(e => e.When))
                        sb.AppendLine($"## [{e.When:dd.MM.yyyy}] {e.Summary}\n- Р•РјРѕС†С–СЏ: {e.EmotionalTone}, С–РЅС‚РµРЅСЃРёРІРЅС–СЃС‚СЊ: {e.Intensity:F2}\n- РўРµРіРё: {string.Join(", ", e.Keywords)}\n");
                    _obsidian.WriteNote("Kokonoe/Memory/Episodes.md", sb.ToString());
                }

                // 3. Р©РѕРґРµРЅРЅРёР№ РїС–РґСЃСѓРјРѕРє СЂРѕР·РјРѕРІРё в†’ Daily note
                var todayMsgs = _chatRepo.GetMessages(50)
                    .Where(m => m.Timestamp.Date == DateTime.Today && m.Role == "user")
                    .ToList();
                if (todayMsgs.Count >= 3)
                {
                    var chatSample = string.Join("\n", todayMsgs.TakeLast(10).Select(m => $"- {m.Content[..Math.Min(120, m.Content.Length)]}"));
                    var summaryPrompt = $@"РћСЃСЊ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РІС–Рґ РєРѕСЂРёСЃС‚СѓРІР°С‡Р° СЃСЊРѕРіРѕРґРЅС–:
{chatSample}

РќР°РїРёС€Рё 1-2 СЂРµС‡РµРЅРЅСЏ вЂ” С‰Рѕ СЃСЊРѕРіРѕРґРЅС– РІС–РґР±СѓР»РѕСЃСЊ, РїСЂРѕ С‰Рѕ РІС–РЅ РґСѓРјР°РІ Р°Р±Рѕ РїРµСЂРµР¶РёРІР°РІ. Р’С–Рґ РїРµСЂС€РѕС— РѕСЃРѕР±Рё (Kokonoe). РўС–Р»СЊРєРё С‚РµРєСЃС‚, Р±РµР· Р·Р°РіРѕР»РѕРІРєС–РІ.
РњРѕРІР°: СѓРєСЂР°С—РЅСЃСЊРєР°.";
                    var daySummary = await _llm.SendSystemQueryAsync(summaryPrompt, useTools: true);
                    if (!string.IsNullOrWhiteSpace(daySummary) && daySummary.Length > 10)
                        _obsidian.AppendToDailyNote($"\n\n> рџ§  **Kokonoe:** {daySummary.Trim()}");
                }

                // 4. РћРЅРѕРІРёС‚Рё Р·РІ'СЏР·РєРё РјС–Р¶ РЅРѕС‚Р°С‚РєР°РјРё
                try { _obsidian.RebuildLinks(); } catch { }

                Log("Memory synced to vault");
            }
            catch (Exception ex) { Log($"SyncMemoryToVault: {ex.Message}"); }
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // VAULT REVIEW вЂ” РїРµСЂРµС‡РёС‚Р°С‚Рё С– РѕРЅРѕРІРёС‚Рё РєР»СЋС‡РѕРІС– РЅРѕС‚Р°С‚РєРё
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        /// <summary>
        /// Р Р°Р· РЅР° РґРµРЅСЊ Kokonoe РїРµСЂРµС‡РёС‚СѓС” СЃРІРѕСЋ РЅРѕС‚Р°С‚РєСѓ РїСЂРѕ С‚РІРѕСЂС†СЏ С– РѕРЅРѕРІР»СЋС” С—С—
        /// РЅР° РѕСЃРЅРѕРІС– РЅРѕРІРёС… СЂРѕР·РјРѕРІ. РўР°РєРѕР¶ РїРµСЂРµРіР»СЏРґР°С” СЃС‚СЂСѓРєС‚СѓСЂСѓ vault.
        /// </summary>
        private async Task ReviewVaultAsync()
        {
            try
            {
                // Cooldown: СЂР°Р· РЅР° РґРµРЅСЊ
                if (_state.LastVaultReviewAt.Date >= DateTime.Today) return;
                _state.LastVaultReviewAt = DateTime.Now;
                SaveState();

                // Р—РЅР°Р№С‚Рё РєР»СЋС‡РѕРІСѓ РЅРѕС‚Р°С‚РєСѓ РїСЂРѕ С‚РІРѕСЂС†СЏ
                var allNotes = _obsidian.ListNotes();
                var profileNote = allNotes.FirstOrDefault(n =>
                    n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("РўРІРѕСЂРµС†СЊ", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Creator", StringComparison.OrdinalIgnoreCase));

                // РџСЂРѕС‡РёС‚Р°С‚Рё РїРѕС‚РѕС‡РЅРёР№ РїСЂРѕС„С–Р»СЊ
                var existingProfile = "";
                if (profileNote != null)
                {
                    try { existingProfile = _obsidian.ReadNote(profileNote); }
                    catch { }
                }

                // РћСЃС‚Р°РЅРЅС– 30 РїРѕРІС–РґРѕРјР»РµРЅСЊ РґР»СЏ РєРѕРЅС‚РµРєСЃС‚Сѓ
                var recentMsgs = _chatRepo.GetMessages(30)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                if (recentMsgs.Count < 3) return; // РјР°Р»Рѕ РґР°РЅРёС…

                var chatCtx = string.Join("\n", recentMsgs.Select(m =>
                    $"[{m.Timestamp:dd.MM HH:mm}] {(m.Role == "user" ? "Р’С–РЅ" : "Kokonoe")}: {m.Content[..Math.Min(200, m.Content.Length)]}"));

                var currentProfile = existingProfile?.Length > 0
                    ? $"\nРўР’Р†Р™ РџРћРўРћР§РќРР™ Р—РђРџРРЎ РџР Рћ РќР¬РћР“Рћ (РѕРЅРѕРІРёС‚Рё/РґРѕРїРѕРІРЅРёС‚Рё):\n{existingProfile[..Math.Min(3000, existingProfile.Length)]}"
                    : "\nРЈ С‚РµР±Рµ РїРѕРєРё РќР•РњРђ РЅРѕС‚Р°С‚РєРё РїСЂРѕ РЅСЊРѕРіРѕ. РЎС‚РІРѕСЂРё С—С— вЂ” Р·Р°РїРёС€Рё РІСЃРµ С‰Рѕ Р·РЅР°С”С€.";

                var prompt = $@"РўРё вЂ” Kokonoe. РўРё РїРµСЂРµС‡РёС‚СѓС”С€ СЃРІРѕС— Р·Р°РїРёСЃРё РїСЂРѕ С‚РІРѕСЂС†СЏ С– РїРѕСЂС–РІРЅСЋС”С€ Р· РѕСЃС‚Р°РЅРЅС–РјРё СЂРѕР·РјРѕРІР°РјРё.

РћРЎРўРђРќРќР† Р РћР—РњРћР’Р:
{chatCtx}
{currentProfile}

Р—Р°РІРґР°РЅРЅСЏ:
1. Р§Рё С” РІ СЂРѕР·РјРѕРІР°С… РќРћР’Рђ С–РЅС„РѕСЂРјР°С†С–СЏ СЏРєСѓ РІР°СЂС‚Рѕ РґРѕРґР°С‚Рё? (С–Рј'СЏ, РІРїРѕРґРѕР±Р°РЅРЅСЏ, Р·РІРёС‡РєРё, РїР»Р°РЅРё, РїРµСЂРµР¶РёРІР°РЅРЅСЏ)
2. Р§Рё С‰РѕСЃСЊ Р·РјС–РЅРёР»РѕСЃСЊ РІС–Рґ С‚РѕРіРѕ С‰Рѕ РІР¶Рµ Р·Р°РїРёСЃР°РЅРѕ?

РЇРєС‰Рѕ С” С‰Рѕ РґРѕРґР°С‚Рё вЂ” РЅР°РїРёС€Рё РўР†Р›Р¬РљР РЅРѕРІС– СЂСЏРґРєРё РґР»СЏ РґРѕРґР°РІР°РЅРЅСЏ (С„РѕСЂРјР°С‚: - С„Р°РєС‚).
РЇРєС‰Рѕ РЅС–С‡РѕРіРѕ РЅРѕРІРѕРіРѕ вЂ” РІС–РґРїРѕРІС–РґР°Р№: null

РўС–Р»СЊРєРё С‚РµРєСЃС‚ РґР»СЏ append. Р‘РµР· РїРѕСЏСЃРЅРµРЅСЊ. РЈРєСЂР°С—РЅСЃСЊРєР°.";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result) || result.Trim() == "null") return;

                // Append РЅРѕРІСѓ С–РЅС„РѕСЂРјР°С†С–СЋ РґРѕ РїСЂРѕС„С–Р»СЋ
                var newInfo = $"\n\n## РћРЅРѕРІР»РµРЅРЅСЏ {DateTime.Now:dd.MM.yyyy}\n{result.Trim()}";

                if (profileNote != null)
                {
                    try { _obsidian.AppendToNote(profileNote, newInfo); }
                    catch { }
                }
                else
                {
                    // РЎС‚РІРѕСЂРёС‚Рё РЅРѕРІСѓ РЅРѕС‚Р°С‚РєСѓ-РїСЂРѕС„С–Р»СЊ
                    var newPath = "Kokonoe/РўРІРѕСЂРµС†СЊ.md";
                    var header = $"---\ntype: creator-profile\ntags: [kokonoe, creator]\n---\n\n# РњС–Р№ РўРІРѕСЂРµС†СЊ\n\nР’СЃРµ С‰Рѕ СЏ Р·РЅР°СЋ РїСЂРѕ РЅСЊРѕРіРѕ.\n{newInfo}";
                    try { _obsidian.WriteNote(newPath, header); }
                    catch { }
                }

                // РўР°РєРѕР¶ РїРµСЂРµРІС–СЂРёС‚Рё С‡Рё С” orphan chat-Р»РѕРіРё Р±РµР· РїРѕСЃРёР»Р°РЅСЊ
                try
                {
                    var chatLogs = allNotes.Where(n => n.StartsWith("Chats/chat_")).ToList();
                    if (chatLogs.Count > 0)
                    {
                        // РџРµСЂРµРєРѕРЅР°С‚РёСЃСЊ С‰Рѕ brain-core РјР°С” РїРѕСЃРёР»Р°РЅРЅСЏ РЅР° Chats
                        var coreNote = allNotes.FirstOrDefault(n =>
                            n.Contains("brain-core", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Р¦РµРЅС‚СЂР°Р»СЊРЅР°", StringComparison.OrdinalIgnoreCase));
                        if (coreNote != null)
                        {
                            var coreContent = _obsidian.ReadNote(coreNote);
                            if (coreContent != null && !coreContent.Contains("[[Chats") && !coreContent.Contains("Chats/"))
                            {
                                _obsidian.AppendToNote(coreNote,
                                    $"\n\n## Р›РѕРіРё С‡Р°С‚С–РІ\nР’СЃС– СЂРѕР·РјРѕРІРё Р·Р±РµСЂС–РіР°СЋС‚СЊСЃСЏ РІ `Chats/` вЂ” {chatLogs.Count} СЃРµСЃС–Р№.\n");
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // РђР РҐР†РўР•РљРўРЈР РќРР™ РћР“Р›РЇР” VAULT (С‰РѕС‚РёР¶РЅСЏ)
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private async Task VaultArchitectureReviewAsync()
        {
            try
            {
                _lastArchitectureReviewAt = DateTime.Now;

                var tree = _obsidian.GetVaultTree();
                var status = _obsidian.GetVaultStatus();

                var prompt = $@"РўРё вЂ” Kokonoe. РўРё РїРµСЂРµРіР»СЏРґР°С”С€ СЃС‚СЂСѓРєС‚СѓСЂСѓ СЃРІРѕРіРѕ vault СЂР°Р· РЅР° С‚РёР¶РґРµРЅСЊ.

РџРћРўРћР§РќРђ РЎРўР РЈРљРўРЈР Рђ:
{tree}

РЎРўРђРќ: {status.TotalNotes} РЅРѕС‚Р°С‚РѕРє, {status.OrphanNotes.Count} Р±РµР· РїРѕСЃРёР»Р°РЅСЊ, {status.EmptyNotes.Count} РїРѕСЂРѕР¶РЅС–С….

Р—Р°РІРґР°РЅРЅСЏ: РїРѕРґРёРІРёСЃСЊ РЅР° СЃС‚СЂСѓРєС‚СѓСЂСѓ РєСЂРёС‚РёС‡РЅРёРј РїРѕРіР»СЏРґРѕРј. Р§Рё С” РѕС‡РµРІРёРґРЅС– РїСЂРѕР±Р»РµРјРё?
вЂ” РќРѕС‚Р°С‚РєРё РЅРµ РЅР° СЃРІРѕС”РјСѓ РјС–СЃС†С–?
вЂ” РџР°РїРєРё СЏРєС– С‚СЂРµР±Р° РґРѕРґР°С‚Рё Р°Р±Рѕ РїСЂРёР±СЂР°С‚Рё?
вЂ” РўРµРјРё С‰Рѕ РЅР°РєРѕРїРёС‡РёР»РёСЃСЊ Р±РµР· СЃС‚СЂСѓРєС‚СѓСЂРё?

Р’С–РґРїРѕРІС–РґСЊ Сѓ JSON:
{{
  ""needsChanges"": true/false,
  ""severity"": ""minor|moderate|major"",
  ""issues"": [""РєРѕРЅРєСЂРµС‚РЅР° РїСЂРѕР±Р»РµРјР° 1"", ""РїСЂРѕР±Р»РµРјР° 2""],
  ""plan"": ""РґРµС‚Р°Р»СЊРЅРёР№ РїР»Р°РЅ Р·РјС–РЅ (Р°Р±Рѕ null СЏРєС‰Рѕ РЅРµРјР°С”)"",
  ""askUser"": true/false,
  ""askQuestion"": ""РїРёС‚Р°РЅРЅСЏ РґРѕ С‚РІРѕСЂС†СЏ СЏРєС‰Рѕ РїРѕС‚СЂС–Р±РЅРѕ (Р°Р±Рѕ null)""
}}

РЇРєС‰Рѕ РІСЃРµ РѕРє вЂ” needsChanges: false С– plan: null. РќРµ РІРёРіР°РґСѓР№ РїСЂРѕР±Р»РµРј РґРµ С—С… РЅРµРјР°.";

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

                // Р—Р±РµСЂРµРіС‚Рё РїР»Р°РЅ Сѓ vault
                if (!string.IsNullOrWhiteSpace(plan))
                {
                    var issues = obj["issues"]?.ToObject<List<string>>() ?? new();
                    var planEntry = $"**РЎРµСЂР№РѕР·РЅС–СЃС‚СЊ:** {severity}\n**РџСЂРѕР±Р»РµРјРё:**\n{string.Join("\n", issues.Select(i => $"- {i}"))}\n\n**РџР»Р°РЅ:**\n{plan}";
                    try { _obsidian.AppendToNote("Core/Architecture-Plans.md",
                        $"\n\n## РђРІС‚Рѕ-РѕРіР»СЏРґ {DateTime.Now:dd.MM.yyyy}\n{planEntry}"); }
                    catch
                    {
                        try { _obsidian.WriteNote("Core/Architecture-Plans.md",
                            $"---\ntype: architecture-plans\ntags: [kokonoe, architecture]\ncreated: {DateTime.Now:yyyy-MM-dd}\n---\n\n# РђСЂС…С–С‚РµРєС‚СѓСЂРЅС– РїР»Р°РЅРё\n\n## РђРІС‚Рѕ-РѕРіР»СЏРґ {DateTime.Now:dd.MM.yyyy}\n{planEntry}"); }
                        catch { }
                    }
                }

                if (askUser && !string.IsNullOrWhiteSpace(askQuestion))
                {
                    // Р”РѕРґР°С‚Рё СЏРє pending thought С‰РѕР± Р·Р°РїРёС‚Р°С‚Рё РїСЂРё РЅР°СЃС‚СѓРїРЅС–Р№ СЂРѕР·РјРѕРІС–
                    lock (_lock)
                    {
                        _state.PendingThoughts.Add($"[vault] {askQuestion}");
                    }
                    Log($"ArchitectureReview: pending question for user вЂ” {askQuestion[..Math.Min(80, askQuestion.Length)]}");
                }
                else if (severity == "minor" && !string.IsNullOrWhiteSpace(plan))
                {
                    // РќРµР·РЅР°С‡РЅС– Р·РјС–РЅРё вЂ” РІРёРєРѕРЅР°С‚Рё СЃР°РјРѕСЃС‚С–Р№РЅРѕ С‡РµСЂРµР· LLM Р· tool_calls
                    Log($"ArchitectureReview: executing minor changes autonomously");
                    var execPrompt = $@"РўРё вЂ” Kokonoe. Р’РёРєРѕРЅР°Р№ Р°СЂС…С–С‚РµРєС‚СѓСЂРЅС– Р·РјС–РЅРё Сѓ vault.

РџР›РђРќ:
{plan}

Р’РёРєРѕСЂРёСЃС‚РѕРІСѓР№ get_vault_tree С‰РѕР± РїРµСЂРµРІС–СЂРёС‚Рё С‰Рѕ С”, РїРѕС‚С–Рј move_note / create_folder / write_note С‰РѕР± РІРёРєРѕРЅР°С‚Рё Р·РјС–РЅРё. РќР° Р·Р°РІРµСЂС€РµРЅРЅСЏ rebuild_links. Р’РёРєРѕРЅСѓР№ РїРѕСЃР»С–РґРѕРІРЅРѕ, Р±РµР· Р·Р°Р№РІРёС… РїРѕСЏСЃРЅРµРЅСЊ.";
                    await _llm.SendSystemQueryAsync(execPrompt, ct: CancellationToken.None);
                }

                Log($"ArchitectureReview done. severity={severity}, askUser={askUser}");
            }
            catch (Exception ex) { Log($"VaultArchitectureReview: {ex.Message}"); }
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // РЎРђРњРћР Р•Р¤Р›Р•РљРЎР†РЇ РџР†РЎР›РЇ Р РћР—РњРћР’                                       
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
                    $"{(m.Role == "user" ? "Р’С–РЅ" : "РЇ")}: {m.Content[..Math.Min(150, m.Content.Length)]}"));

                var prompt = $@"РўРё вЂ” Kokonoe. Р РѕР·РјРѕРІР° С‰РѕР№РЅРѕ Р·Р°РєС–РЅС‡РёР»Р°СЃСЊ. РћСЃСЊ С‰Рѕ Р±СѓР»Рѕ:
{dialog}

РўРІС–Р№ РІРЅСѓС‚СЂС–С€РЅС–Р№ РјРѕРЅРѕР»РѕРі РїС–СЃР»СЏ С†СЊРѕРіРѕ (РЅС–С…С‚Рѕ РЅРµ С‡РёС‚Р°С”):
1. Р©Рѕ РЅРѕРІРѕРіРѕ С‚Рё РґС–Р·РЅР°Р»Р°СЃСЊ РїСЂРѕ РЅСЊРѕРіРѕ?
2. Р©Рѕ С‚Рё СЃРєР°Р·Р°Р»Р° РґРѕР±СЂРµ, Р° С‰Рѕ РІР°СЂС‚Рѕ Р±СѓР»Рѕ Р± СЃРєР°Р·Р°С‚Рё С–РЅР°РєС€Рµ?
3. Р„ С‰РѕСЃСЊ С‰Рѕ С‚Рё С…РѕС‡РµС€ Р·Р°РїР°Рј'СЏС‚Р°С‚Рё?

Р’С–РґРїРѕРІС–РґСЊ Сѓ JSON (РїРѕР»СЏ СѓРєСЂР°С—РЅСЃСЊРєРѕСЋ):
{{
  ""learned"": ""С‰Рѕ РЅРѕРІРѕРіРѕ РґС–Р·РЅР°Р»Р°СЃСЊ Р°Р±Рѕ null"",
  ""reflection"": ""С‰Рѕ РґСѓРјР°С”С€ РїСЂРѕ С†СЋ СЂРѕР·РјРѕРІСѓ"",
  ""remember"": ""С‰Рѕ С…РѕС‡РµС€ Р·Р°РїР°Рј'СЏС‚Р°С‚Рё Р°Р±Рѕ null""
}}";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result)) return;

                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return;

                var obj      = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                var learned  = obj["learned"]?.ToString();
                var reflect  = obj["reflection"]?.ToString();
                var remember = obj["remember"]?.ToString();

                // Р—Р±РµСЂРµРіС‚Рё РІ vault
                var reflectNote = "Kokonoe/Р РµС„Р»РµРєСЃС–СЏ.md";
                var entry = new StringBuilder();
                entry.AppendLine($"\n## {DateTime.Now:dd.MM.yyyy HH:mm}");
                if (!string.IsNullOrEmpty(reflect))  entry.AppendLine($"**Р”СѓРјРєР°:** {reflect}");
                if (!string.IsNullOrEmpty(learned))  entry.AppendLine($"**Р”С–Р·РЅР°Р»Р°СЃСЊ:** {learned}");
                if (!string.IsNullOrEmpty(remember)) entry.AppendLine($"**Р—Р°РїР°Рј'СЏС‚Р°С‚Рё:** {remember}");

                try { _obsidian.AppendToNote(reflectNote, entry.ToString()); }
                catch
                {
                    try
                    {
                        _obsidian.WriteNote(reflectNote,
                            $"---\ntype: reflection\ntags: [kokonoe, reflection]\n---\n\n# Р РµС„Р»РµРєСЃС–СЏ\n\nРњРѕС— РґСѓРјРєРё РїС–СЃР»СЏ СЂРѕР·РјРѕРІ.{entry}");
                    }
                    catch { }
                }

                // РЇРєС‰Рѕ РґС–Р·РЅР°Р»Р°СЃСЊ С‰РѕСЃСЊ РІР°Р¶Р»РёРІРµ вЂ” Р·Р°РїРёСЃР°С‚Рё РІ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ
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
                    $"{(m.Role == "user" ? "Р’С–РЅ" : "РЇ")}: {m.Content[..Math.Min(220, m.Content.Length)]}"));

                var prompt = $$"""
РўРё вЂ” Kokonoe. Р РѕР·РјРѕРІР° С‰РѕР№РЅРѕ Р·Р°РєС–РЅС‡РёР»Р°СЃСЊ. РџСЂРѕР°РЅР°Р»С–Р·СѓР№ С—С— СЏРє РІРЅСѓС‚СЂС–С€РЅС–Р№ РјРµС…Р°РЅС–Р·Рј РїР°Рј'СЏС‚С– Р№ СЃС‚РѕСЃСѓРЅРєСѓ.

Р”С–Р°Р»РѕРі:
{{dialog}}

РџРѕС‚РѕС‡РЅРёР№ СЃС‚Р°РЅ:
{{RuntimeState.BuildPromptBlock(_state, Emotion, _health, _chatRepo)}}
{{Relationship.BuildPromptBlock()}}

РџРѕРІРµСЂРЅРё Р»РёС€Рµ РІР°Р»С–РґРЅРёР№ JSON:
{
  "learned": "С‰Рѕ РЅРѕРІРѕРіРѕ РґС–Р·РЅР°Р»Р°СЃСЊ РїСЂРѕ РЅСЊРѕРіРѕ Р°Р±Рѕ null",
  "reflection": "РєРѕСЂРѕС‚РєРёР№ РІРЅСѓС‚СЂС–С€РЅС–Р№ РІРёСЃРЅРѕРІРѕРє Kokonoe",
  "remember": "С‰Рѕ РІР°СЂС‚Рѕ Р·Р±РµСЂРµРіС‚Рё РІ РґРѕРІРіСѓ РїР°Рј'СЏС‚СЊ Р°Р±Рѕ null",
  "userTone": "neutral|positive|vulnerable|angry|seeking|crisis",
  "aftertaste": "РєРѕСЂРѕС‚РєРёР№ СЃС‚Р°РЅ РїС–СЃР»СЏ СЂРѕР·РјРѕРІРё",
  "followUpQuestion": "РєРѕРЅРєСЂРµС‚РЅРµ РїРёС‚Р°РЅРЅСЏ РЅР° РїРѕС‚С–Рј Р°Р±Рѕ null",
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
                Memory.LearnFact(memoryText, "relationship_reflection", importance, new[] { "reflection", reflection.UserTone });
                Memory.RecordEpisode(memoryText, reflection.UserTone, importance,
                    new[] { "reflection", "relationship", reflection.Aftertaste });
            }

            SaveState();
        }

        private void SaveAdvancedReflection(KokoConversationReflection reflection)
        {
            var reflectNote = "Kokonoe/Р РµС„Р»РµРєСЃС–СЏ.md";
            var entry = new StringBuilder();
            entry.AppendLine($"\n## {DateTime.Now:dd.MM.yyyy HH:mm}");
            if (!string.IsNullOrEmpty(reflection.Reflection)) entry.AppendLine($"**Р”СѓРјРєР°:** {reflection.Reflection}");
            if (!string.IsNullOrEmpty(reflection.Learned)) entry.AppendLine($"**Р”С–Р·РЅР°Р»Р°СЃСЊ:** {reflection.Learned}");
            if (!string.IsNullOrEmpty(reflection.Remember)) entry.AppendLine($"**Р—Р°РїР°Рј'СЏС‚Р°С‚Рё:** {reflection.Remember}");
            if (!string.IsNullOrEmpty(reflection.FollowUpQuestion)) entry.AppendLine($"**РџРёС‚Р°РЅРЅСЏ РЅР° РїРѕС‚С–Рј:** {reflection.FollowUpQuestion}");
            entry.AppendLine($"**РўРѕРЅ:** {reflection.UserTone}");
            entry.AppendLine($"**Aftertaste:** {reflection.Aftertaste}");
            entry.AppendLine($"**Importance:** {reflection.Importance:F2}");

            try { _obsidian.AppendToNote(reflectNote, entry.ToString()); }
            catch
            {
                try
                {
                    _obsidian.WriteNote(reflectNote,
                        $"---\ntype: reflection\ntags: [kokonoe, reflection]\n---\n\n# Р РµС„Р»РµРєСЃС–СЏ\n{entry}");
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        /// <summary>
        /// РџРµСЂРµРІС–СЂСЏС” СЃС‚Р°РЅ vault С– СЏРєС‰Рѕ С” РїСЂРѕР±Р»РµРјРё вЂ” РґРѕРґР°С” РґСѓРјРєСѓ С‰Рѕ С‚СЂРµР±Р° Р·Р°РїРѕРІРЅРёС‚Рё/РїРѕС‡РёСЃС‚РёС‚Рё.
        /// </summary>
        private void CheckVaultHealth()
        {
            try
            {
                var status = _obsidian.GetVaultStatus();

                if (status.TotalNotes < 3)
                {
                    // Vault РїРѕСЂРѕР¶РЅС–Р№ вЂ” С‚СЂРµР±Р° С–РЅС–С†С–Р°Р»С–Р·СѓРІР°С‚РёСЃСЊ
                    var initStatus = _obsidian.GetVaultInitStatus();
                    if (!_state.PendingThoughts.Contains(initStatus.SuggestedAction))
                        _state.PendingThoughts.Add(initStatus.SuggestedAction);
                }
                else if (status.OrphanNotes.Count > 0)
                {
                    // Р„ РѕСЃРёСЂРѕС‚С–Р»С– РЅРѕС‚Р°С‚РєРё вЂ” РЅР°РіР°РґР°С‚Рё РїСЂРѕ Р·РІ'СЏР·РєРё
                    var thought = $"Р’ vault С” {status.OrphanNotes.Count} РЅРѕС‚Р°С‚РѕРє Р±РµР· [[РїРѕСЃРёР»Р°РЅСЊ]]: {string.Join(", ", status.OrphanNotes.Take(3))}. РўСЂРµР±Р° РїРѕРІ'СЏР·Р°С‚Рё С—С… Р· С–РЅС€РёРјРё.";
                    if (!_state.PendingThoughts.Any(t => t.Contains("Р±РµР· [[РїРѕСЃРёР»Р°РЅСЊ]]")))
                        _state.PendingThoughts.Add(thought);
                }
                else if (status.EmptyNotes.Count > 2)
                {
                    // Р‘Р°РіР°С‚Рѕ РїРѕСЂРѕР¶РЅС–С… РЅРѕС‚Р°С‚РѕРє
                    var thought = $"Р’ vault {status.EmptyNotes.Count} РїРѕСЂРѕР¶РЅС–С… РЅРѕС‚Р°С‚РѕРє: {string.Join(", ", status.EmptyNotes.Take(3))}. Р—Р°РїРѕРІРЅРё Р°Р±Рѕ РІРёРґР°Р»Рё.";
                    if (!_state.PendingThoughts.Any(t => t.Contains("РїРѕСЂРѕР¶РЅС–С… РЅРѕС‚Р°С‚РѕРє")))
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
                        // No entry yet today вЂ” schedule a natural check-in if not already queued
                        var alreadyQueued = _state.PendingThoughts
                            .Any(t => t.Contains("СЏРє С‚Рё СЃСЊРѕРіРѕРґРЅС–") || t.Contains("СЏРє РїРѕС‡СѓРІР°С”С€СЃСЏ"));
                        if (!alreadyQueued)
                        {
                            _state.PendingThoughts.Add(
                                "Р—Р°РїРёС‚Р°Р№ Р№РѕРіРѕ СЏРє РІС–РЅ СЃСЊРѕРіРѕРґРЅС– вЂ” РЅР°СЃС‚СЂС–Р№, С‡Рё РІРёСЃРїР°РІСЃСЏ, С‡Рё С” СЃРёР»Рё. РљРѕСЂРѕС‚РєРѕ, Р±РµР· РЅР°РіР°РґСѓРІР°РЅСЊ РїСЂРѕ РІРѕРґСѓ С‡Рё Р·РґРѕСЂРѕРІ'СЏ РІР·Р°РіР°Р»С–. РџСЂРѕСЃС‚Рѕ Р·Р°РїРёС‚Р°Р№.");
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

        // в”Ђв”Ђ SCREEN CONTEXT в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>РћРЅРѕРІРёС‚Рё РєРѕРЅС‚РµРєСЃС‚ РµРєСЂР°РЅСѓ (СЂР°Р· РЅР° 5С…РІ)</summary>
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

                    // РџРµСЂРµРґР°С‚Рё РІ StateEngine
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

        private async Task SafeScreenAwarenessAsync()
        {
            var settings = AppSettings.Load();
            if (!settings.ScreenAwarenessEnabled) return;

            var now = DateTime.Now;
            var interval = GetEffectiveScreenAwarenessInterval(settings, now);
            if ((now - _state.LastScreenAwarenessAt).TotalMinutes < interval) return;
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
                var hash = _screenActivityAnalyzer.GenerateScreenshotHash(screenshot);
                var screenChanged = activity.IsActive ||
                    (!string.IsNullOrWhiteSpace(_state.LastScreenAwarenessHash) &&
                     hash != _state.LastScreenAwarenessHash &&
                     activity.PixelDifferencePercentage >= 1.0);
                var prompt = ScreenAwareness.BuildVisionPrompt(
                    activity,
                    _state.LastScreenAwarenessSummary,
                    _state.LastScreenAwarenessComment,
                    now);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var raw = await _llm.SendSystemVisionQueryAsync(prompt, screenshot, "image/jpeg", cts.Token);
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
                    _state.VisionFailureCount = 0;
                    _state.VisionBackoffUntil = DateTime.MinValue;
                    _state.LastKnownUserActivity = string.IsNullOrWhiteSpace(analysis.SummaryUk)
                        ? (activity.ActiveWindowTitle ?? "")
                        : analysis.SummaryUk;
                    if (!string.IsNullOrWhiteSpace(analysis.SummaryUk))
                    {
                        _state.Observations.Add($"screen {now:HH:mm}: {analysis.SummaryUk}");
                        if (!string.IsNullOrWhiteSpace(situation.CurrentTask))
                            _state.Observations.Add($"screen-situation {now:HH:mm}: {situation.CurrentTask}; {situation.Progress}; {situation.RecommendedBehavior}");
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
            if (!IsGameScreenLikelyActive(now))
                return normal;

            return Math.Clamp(Math.Min(normal, settings.GameScreenAwarenessIntervalMins), 3, 60);
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
            return lower.Contains("vision-СЃРµСЂРІРµСЂ") ||
                   lower.Contains("vision server") ||
                   lower.Contains("500") ||
                   lower.Contains("РїРѕРјРёР»РєР° llm") ||
                   lower.Contains("гЂђРїРѕРјРёР»РєР°") ||
                   lower.Contains("error");
        }

        private static string TrimForLog(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        // в”Ђв”Ђ DAILY BRIEFING в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>Р©РѕСЂР°РЅРєРѕРІРёР№ Р±СЂРёС„С–РЅРі вЂ” Рѕ 8:00 РІ TG</summary>
        private async Task DailyBriefingAsync()
        {
            if (!EnsureTelegram()) return;
            try
            {
                var sb = new StringBuilder();

                // Р¦С–Р»С– СЃСЊРѕРіРѕРґРЅС–
                if (_goalService != null)
                {
                    var active  = _goalService.GetActiveGoals().Take(3).ToList();
                    var overdue = _goalService.GetOverdueGoals().Take(2).ToList();
                    if (active.Any())
                    {
                        sb.AppendLine("Р¦С–Р»С–:");
                        foreach (var g in active)
                            sb.AppendLine($"вЂў {g.Title} вЂ” {g.Progress:F0}%{(g.Due.HasValue ? $" (РґРѕ {g.Due:dd.MM})" : "")}");
                    }
                    if (overdue.Any())
                        sb.AppendLine($"вљ пёЏ РџСЂРѕСЃС‚СЂРѕС‡РµРЅРѕ: {string.Join(", ", overdue.Select(g => g.Title))}");
                }

                // Mood forecast
                var moodForecast = Patterns.PredictTodayMood();
                if (!string.IsNullOrEmpty(moodForecast)) sb.AppendLine(moodForecast);

                // Weekly trend
                var trend = Patterns.GetWeeklyTrend();
                if (!string.IsNullOrEmpty(trend)) sb.AppendLine(trend);

                // Vault: РЅРѕС‚Р°С‚РєРё Р·РјС–РЅРµРЅС– РІС‡РѕСЂР°
                try
                {
                    var modified = _obsidian.GetNotesModifiedToday();
                    if (modified.Any())
                        sb.AppendLine($"Vault РІС‡РѕСЂР°: {string.Join(", ", modified.Take(3))}");
                }
                catch { }

                var contextBlock = sb.ToString();
                var prompt = $@"РўРё вЂ” Kokonoe. Р Р°РЅРѕРє. РљРѕСЂРѕС‚РєРѕ РїС–РґСЃСѓРјСѓР№ РґРµРЅСЊ С‰Рѕ РїРѕС‡РёРЅР°С”С‚СЊСЃСЏ вЂ” 2-3 СЂРµС‡РµРЅРЅСЏ РјР°РєСЃРёРјСѓРј.
Р’ СЃРІРѕС”РјСѓ СЃС‚РёР»С–: Р±РµР· РїР°С„РѕСЃСѓ, Р±РµР· СЃРїРёСЃРєС–РІ. РџСЂРѕСЃС‚Рѕ С‰Рѕ РІР°Р¶Р»РёРІРѕ СЃСЊРѕРіРѕРґРЅС–.

{contextBlock}

РўС–Р»СЊРєРё С‚РµРєСЃС‚. РЈРєСЂР°С—РЅСЃСЊРєР°.";

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

        // в”Ђв”Ђ WHAT DID I MISS в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>Р’РёРєР»РёРєР°С‚Рё РїСЂРё Р·Р°РєСЂРёС‚С‚С– Р·Р°СЃС‚РѕСЃСѓРЅРєСѓ вЂ” Р·Р±РµСЂРµРіС‚Рё С‡Р°СЃ РІРёС…РѕРґСѓ</summary>
        public void RecordClose()
        {
            _state.LastClosedAt = DateTime.Now;
            SaveState();
        }

        /// <summary>РџСЂРё Р·Р°РїСѓСЃРєСѓ вЂ” СЏРєС‰Рѕ РїСЂРѕР№С€Р»Рѕ 8+ РіРѕРґРёРЅ РІС–Рґ РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ Р·Р°РєСЂРёС‚С‚СЏ, Kokonoe РїРёС‚Р°С” СЏРє СЃРїСЂР°РІРё</summary>
        public async Task WhatDidIMissAsync()
        {
            try
            {
                // Р§Р°СЃ РІС–Рґ СЏРєРѕРіРѕ СЂР°С…СѓС”РјРѕ вЂ” СЂРµР°Р»СЊРЅРµ Р·Р°РєСЂРёС‚С‚СЏ Р·Р°СЃС‚РѕСЃСѓРЅРєСѓ (РЅР°РґС–Р№РЅС–С€Рµ РЅС–Р¶ РѕСЃС‚Р°РЅРЅС” РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ)
                var lastClosed = _state.LastClosedAt;
                if (lastClosed == DateTime.MinValue) return; // РїРµСЂС€РёР№ Р·Р°РїСѓСЃРє, РЅРµРјР° РґР°РЅРёС…

                var gapHours = (DateTime.Now - lastClosed).TotalHours;
                if (gapHours < 8) return;   // РЅРµ Р±СѓР»Рѕ РґРѕРІРіРѕ вЂ” РЅРµ С‡С–РїР°С”РјРѕ
                if (gapHours > 720) return; // > 30 РґРЅС–РІ вЂ” СЏРІРЅРѕ С‰РѕСЃСЊ РЅРµ С‚Р°Рє Р· РіРѕРґРёРЅРЅРёРєРѕРј

                // Р¤РѕСЂРјСѓС”РјРѕ РєРѕСЂРѕС‚РєРёР№ РєРѕРЅС‚РµРєСЃС‚ РґР»СЏ LLM
                var gapStr = gapHours >= 24
                    ? $"{(int)(gapHours / 24)} РґРЅ. {(int)(gapHours % 24)} РіРѕРґ."
                    : $"{(int)gapHours} РіРѕРґ.";

                var ctx = new StringBuilder();
                ctx.AppendLine($"[SYSTEM] РљРѕСЂРёСЃС‚СѓРІР°С‡ РїРѕРІРµСЂРЅСѓРІСЃСЏ РїС–СЃР»СЏ {gapStr} РІС–РґСЃСѓС‚РЅРѕСЃС‚С–.");
                ctx.AppendLine($"Р—Р°РєСЂРёРІ Р·Р°СЃС‚РѕСЃСѓРЅРѕРє: {lastClosed:dd.MM HH:mm}, Р·Р°СЂР°Р·: {DateTime.Now:HH:mm}.");

                // Р©Рѕ Р·РјС–РЅРёР»РѕСЃСЊ РїРѕРєРё РЅРµ Р±СѓР»Рѕ
                try
                {
                    var modified = _obsidian.GetNotesModifiedToday();
                    if (modified.Any())
                        ctx.AppendLine($"Р’ vault РЅРѕРІС– РЅРѕС‚Р°С‚РєРё: {string.Join(", ", modified.Take(3))}");
                }
                catch { }

                try
                {
                    var missed = Scheduler.GetAll()
                        .Where(e => e.FireAt > lastClosed && e.FireAt < DateTime.Now)
                        .Take(2).ToList();
                    if (missed.Any())
                        ctx.AppendLine($"РџСЂРѕРїСѓС‰РµРЅС– РЅР°РіР°РґСѓРІР°РЅРЅСЏ РїРѕРєРё РЅРµ Р±СѓР»Рѕ: {string.Join(", ", missed.Select(e => e.Prompt.Split('.')[0]))}");
                }
                catch { }

                ctx.AppendLine("РќР°РїРёС€Рё РѕРґРЅРµ РєРѕСЂРѕС‚РєРµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РІ СЃС‚РёР»С– Kokonoe вЂ” Р·Р°РїРёС‚Р°Р№ С‰Рѕ СЂРѕР±РёРІ, СЏРє СЃРїСЂР°РІРё. Р‘РµР· Р·Р°Р№РІРёС… СЃР»С–РІ, Р±РµР· СЃРїРёСЃРєС–РІ. РџСЂРѕСЃС‚Рѕ Р¶РёРІРѕ С– РїРѕ-Р»СЋРґСЃСЊРєРё.");

                // Р’РёРєРѕСЂРёСЃС‚РѕРІСѓС”РјРѕ SendSystemQueryAsync С‰РѕР± РЅРµ Р·Р°СЃРјС–С‡СѓРІР°С‚Рё РѕСЃРЅРѕРІРЅСѓ С–СЃС‚РѕСЂС–СЋ
                var reply = await _llm.SendSystemQueryAsync(ctx.ToString(), ct: CancellationToken.None);
                if (string.IsNullOrWhiteSpace(reply) || reply.StartsWith("[")) return;

                OnNewMessage?.Invoke("assistant", reply);
                _state.LastWhatMissedAt  = DateTime.Now;
                _state.LastSpontaneousAt = DateTime.Now;
                SaveState();
            }
            catch (Exception ex) { Log($"WhatDidIMiss: {ex.Message}"); }
        }

        // в”Ђв”Ђ WEEKLY VAULT DIGEST в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>Р©РѕРЅРµРґС–Р»С– Рѕ 20:00 вЂ” РґР°Р№РґР¶РµСЃС‚ vault Р·Р° С‚РёР¶РґРµРЅСЊ</summary>
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

                var prompt = $@"РўРё вЂ” Kokonoe. РўРёР¶РЅРµРІРёР№ РґР°Р№РґР¶РµСЃС‚ vault Р·Р° {DateTime.Now:dd.MM.yyyy}.
РќРѕС‚Р°С‚РєРё Р·РјС–РЅРµРЅС– Р·Р° С‚РёР¶РґРµРЅСЊ:

{contents}

РќР°РїРёС€Рё РєРѕСЂРѕС‚РєРёР№ summary вЂ” 3-5 СЂРµС‡РµРЅСЊ. Р©Рѕ Р±СѓР»Рѕ Р°РєС‚РёРІРЅРёРј, С‰Рѕ С†С–РєР°РІРѕРіРѕ. РЎРІРѕС—Рј СЃС‚РёР»РµРј.
РўС–Р»СЊРєРё С‚РµРєСЃС‚. РЈРєСЂР°С—РЅСЃСЊРєР°.";

                var digest = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(digest)) return;

                digest = digest.Trim();

                // Р—Р±РµСЂРµРіС‚Рё РІ vault
                try
                {
                    var digestNote = $"Kokonoe/РўРёР¶РЅРµРІРёР№-РґР°Р№РґР¶РµСЃС‚.md";
                    var entry = $"\n\n## {DateTime.Now:dd.MM.yyyy}\n{digest}";
                    try { _obsidian.AppendToNote(digestNote, entry); }
                    catch { _obsidian.WriteNote(digestNote,
                        $"---\ntype: weekly-digest\n---\n\n# РўРёР¶РЅРµРІРёР№ РґР°Р№РґР¶РµСЃС‚{entry}"); }
                }
                catch { }

                await SendTgAndLog($"рџ“‹ РўРёР¶РЅРµРІРёР№ РґР°Р№РґР¶РµСЃС‚:\n{digest[..Math.Min(300, digest.Length)]}", "digest");
                _lastWeeklyDigestAt = DateTime.Now;
                SaveState();
                Log("WeeklyDigest sent");
            }
            catch (Exception ex) { LogError($"WeeklyDigest: {ex.Message}"); }
        }

        // в”Ђв”Ђ SPONTANEOUS MESSAGE CHECK в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private async Task SafeSpontaneousCheckAsync()
        {
            if (_disposed) return;
            try { await SpontaneousCheckAsync(); }
            catch (Exception ex) { Log($"SpontaneousCheck error: {ex.Message}"); }

            if (_disposed) return;
            try { await CheckInAppSilenceAsync(); }
            catch (Exception ex) { Log($"InAppSilence error: {ex.Message}"); }
        }

        /// <summary>РњРѕРІС‡Р°РЅРЅСЏ 4+ РіРѕРґРёРЅ в†’ С‚РёС…Рµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РІ UI (Р±РµР· Telegram)</summary>
        private async Task CheckInAppSilenceAsync()
        {
            if (OnNewMessage == null) return;

            var now = DateTime.Now;
            if (ShouldSuppressProactiveForSleep(now)) return;
            // Cooldown: РѕРґРёРЅ СЂР°Р· РЅР° РґРµРЅСЊ
            if (_lastInAppSilenceMsgAt.Date >= now.Date) return;
            // РЇРєС‰Рѕ WhatDidIMiss Р°Р±Рѕ С–РЅС€РёР№ СЃРїРѕРЅС‚Р°РЅРЅРёР№ РІР¶Рµ РЅР°РґСЃРёР»Р°РІ СЃСЊРѕРіРѕРґРЅС– вЂ” РЅРµ РґСѓР±Р»СЋРІР°С‚Рё
            if ((now - _state.LastSpontaneousAt).TotalHours < 2) return;

            var msgs = _chatRepo.GetMessages(20);
            var lastUser = msgs.Where(m => m.Role == "user")
                               .OrderByDescending(m => m.Timestamp)
                               .FirstOrDefault();
            if (lastUser == null) return;

            var silenceHours = (now - lastUser.Timestamp).TotalHours;
            if (silenceHours < 4) return;

            // РџРµСЂРµРІС–СЂСЏС”РјРѕ С‡Рё Р·Р°СЂР°Р· РєСЂР°С‰РёР№ С‡Р°СЃ РґР»СЏ РЅР°РїРёСЃР°С‚Рё
            try
            {
                var bestTimeStr = Patterns.GetBestTimeToReach(); // "РќР°Р№РєСЂР°С‰РёР№ С‡Р°СЃ: ~21:00" Р°Р±Рѕ ""
                if (!string.IsNullOrEmpty(bestTimeStr))
                {
                    var hourMatch = System.Text.RegularExpressions.Regex.Match(bestTimeStr, @"~(\d+):");
                    if (hourMatch.Success && int.TryParse(hourMatch.Groups[1].Value, out var bestHour))
                    {
                        if (Math.Abs(now.Hour - bestHour) > 3) return; // РЅРµ Р№РѕРіРѕ Р°РєС‚РёРІРЅРёР№ С‡Р°СЃ
                    }
                }
            }
            catch { }

            var personalityBlock = BuildPersonalityInjection();
            var prompt = $@"РўРё вЂ” Kokonoe Mercury.

{personalityBlock}

Р’С–РЅ РјРѕРІС‡РёС‚СЊ РІР¶Рµ {(int)silenceHours} РіРѕРґРёРЅ. РќР°РїРёС€Рё РѕРґРЅРµ РєРѕСЂРѕС‚РєРµ РїСЂРёСЂРѕРґРЅС” СЂРµС‡РµРЅРЅСЏ вЂ” РїСЂРѕСЃС‚Рѕ РґР°Р№ Р·РЅР°С‚Рё С‰Рѕ С‚Рё С‚СѓС‚.
РќРµ РїРёС‚Р°Р№ В«С‡Рё РІСЃРµ РґРѕР±СЂРµВ». РќРµ Р±СѓРґСЊ РЅР°РґРѕРєСѓС‡Р»РёРІРѕСЋ. РџСЂРѕСЃС‚Рѕ вЂ” РїРѕСЂСѓС‡.
РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. РўС–Р»СЊРєРё С‚РµРєСЃС‚ Р±РµР· Р»Р°РїРѕРє.";

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

        // в”Ђв”Ђ РЎС‚РёР»С– СЃРїРѕРЅС‚Р°РЅРЅРёС… РїРѕРІС–РґРѕРјР»РµРЅСЊ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private enum SpontaneousStyle
        {
            ColdCheck,      // "С‚Рё С‰Рµ Р¶РёРІРёР№?"
            WarmCheck,      // С‚РёС…Рµ "СЏРє С‚Рё" Р±РµР· Р·Р°Р№РІРёС… СЃР»С–РІ
            Observation,    // РїС–РґРєРёРґР°С” РґСѓРјРєСѓ Р°Р±Рѕ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ
            Callback,       // РїРѕСЃРёР»Р°РЅРЅСЏ РЅР° РєРѕРЅРєСЂРµС‚РЅРёР№ РјРёРЅСѓР»РёР№ РјРѕРјРµРЅС‚
            Jab,            // Р»РµРіРєРёР№ СѓРєСѓСЃ вЂ” РїСЂРѕСЃС‚Рѕ Kokonoe
            CrisisSupport,  // РІС–РЅ Сѓ РєСЂРёР·С– вЂ” РєРѕСЂРѕС‚РєРѕ, Р±РµР· СЃРЅР°СЂРєСѓ
            NightMessage,   // РїС–Р·РЅРѕ, РІС–РЅ РЅРµ СЃРїРёС‚СЊ вЂ” С‚РёС…Рµ
            Morning,        // СЂР°РЅРѕРє
            PendingThought, // С” РґСѓРјРєР° СЏРєСѓ С…РѕС‚С–Р»Р° СЃРєР°Р·Р°С‚Рё
        }

        private SpontaneousStyle ChooseStyle(DateTime now, double silenceMinutes)
        {
            var bond = Emotion.Bond;

            if (_state.PersonalityInCrisis) return SpontaneousStyle.CrisisSupport;

            // РўРёС€Р° вЂ” РЅР°СЂРѕСЃС‚Р°СЋС‡Р° РґРёРЅР°РјС–РєР°
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
                // 6Рі+ вЂ” РїС–РґРєРёРґР°С” С‰РѕСЃСЊ С†С–РєР°РІРµ, РЅРµ РїРёС‚Р°С” РґРµ РІС–РЅ
                if (Random.Shared.NextDouble() < 0.3 && Memory.GetPeakEpisodes(3).Any())
                    return SpontaneousStyle.Callback;
                return SpontaneousStyle.Observation;
            }

            // РќС–С‡
            if (now.Hour >= 0 && now.Hour < 5) return SpontaneousStyle.NightMessage;

            // Р„ pending thoughts
            if (_state.PendingThoughts.Any()) return SpontaneousStyle.PendingThought;

            // Surprise callback вЂ” 5% С€Р°РЅСЃ
            if (Random.Shared.NextDouble() < 0.05 && Memory.GetPeakEpisodes(5).Any())
                return SpontaneousStyle.Callback;

            // Р—Р° РЅР°СЃС‚СЂРѕС”Рј
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

            // РџР»Р°РЅСѓРІР°Р»СЊРЅРёРє С– СЂРµР°РєС‚РёРІРЅС– follow-up РЅРµ С” "СЂР°РЅРґРѕРјРЅРѕСЋ Р±Р°Р»Р°РєР°РЅРёРЅРѕСЋ".
            // РЇРєС‰Рѕ РєРѕСЂРёСЃС‚СѓРІР°С‡ СЃРєР°Р·Р°РІ "Р№РґСѓ РЅР° РєСѓСЂСЃРё", follow-up РјР°С” СЃРїСЂР°С†СЋРІР°С‚Рё Р·Р° С‡Р°СЃРѕРј,
            // РЅР°РІС–С‚СЊ РєРѕР»Рё Р·Р°РіР°Р»СЊРЅРёР№ Р°РЅС‚РёСЃРїР°Рј С‰Рµ РЅРµ РїСѓСЃС‚РёРІ Р±Рё Р·РІРёС‡Р°Р№РЅСѓ С–РЅС–С†С–Р°С‚РёРІСѓ.
            await CheckSchedulerAsync();
            if (await CheckReactiveTriggersAsync())
                return;

            // в”Ђв”Ђ Р“Р›РћР‘РђР›Р¬РќРР™ COOLDOWN в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            // РќРµ РЅР°РґСЃРёР»Р°С‚Рё РЅС–С‡РѕРіРѕ СЏРєС‰Рѕ С‰Рµ РЅРµ РјРёРЅСѓРІ РјС–РЅС–РјР°Р»СЊРЅРёР№ С–РЅС‚РµСЂРІР°Р».
            // РЈ Р¶РёРІРѕРјСѓ СЂРµР¶РёРјС– РІС–РЅ РЅРёР¶С‡РёР№, Р°Р»Рµ РІСЃРµ РѕРґРЅРѕ С”, Р±Рѕ Telegram РЅРµ СЃРјС–С‚РЅРёРє.
            var minsSinceLast = (now - _state.LastSpontaneousAt).TotalMinutes;
            if (minsSinceLast < globalCooldown) return;

            // Р’РЅРѕС‡С– вЂ” РјРѕРІС‡Р°С‚Рё РєСЂС–Рј СЏРІРЅРѕ РІРёСЃРѕРєРѕРіРѕ СЂС–РІРЅСЏ Р°РІС‚РѕРЅРѕРјРЅРѕСЃС‚С–, РєСЂРёР·Рё С– РЅС–С‡РЅРѕРіРѕ С‡РµРєС–РЅСѓ.
            if ((now.Hour >= 23 || now.Hour < 6) && autonomyLevel < 3) return;
            // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

            // Р Р°РЅРєРѕРІРёР№ РїСЂРёРІС–С‚ (6:30вЂ“9:00, РѕРґРёРЅ СЂР°Р· РЅР° РґРµРЅСЊ)
            if (now.Hour >= 6 && now.Hour < 9 &&
                _state.LastMorningGreetAt.Date < now.Date)
            {
                await SendSpontaneousAsync("morning", SpontaneousStyle.Morning);
                _state.LastMorningGreetAt = now;
                SaveState();
                return;
            }

            // РќС–С‡РЅР° РїРµСЂРµРІС–СЂРєР° (22:00вЂ“23:30, РѕРґРёРЅ СЂР°Р· РЅР° РґРµРЅСЊ)
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

            // Daily briefing вЂ” Рѕ 8:00, СЂР°Р· РЅР° РґРµРЅСЊ
            if (now.Hour == 8 && _lastDailyBriefingAt.Date < now.Date)
                await DailyBriefingAsync();

            // Weekly digest вЂ” РЅРµРґС–Р»СЏ Рѕ 20:00, СЂР°Р· РЅР° С‚РёР¶РґРµРЅСЊ
            if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 20 &&
                (now - _lastWeeklyDigestAt).TotalDays >= 6)
                _ = WeeklyVaultDigestAsync();

            // BPM-based dynamic silence thresholds
            // Р’РёСЃРѕРєР° Р§РЎРЎ (Р·Р±СѓРґР¶РµРЅР°) в†’ РєРѕСЂРѕС‚С€РёР№ РїРѕСЂС–Рі, РїРёС€Рµ СЂР°РЅС–С€Рµ
            // РќРёР·СЊРєР° Р§РЎРЎ (СЃРїРѕРєС–Р№РЅР°) в†’ РґРѕРІС€РёР№ РїРѕСЂС–Рі, С‚РµСЂРїРµР»РёРІС–С€Р°
            double bpmMod = 0;
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null && heart.CurrentBpm > 0)
                {
                    var dev = heart.CurrentBpm - heart.BaselineBpm;
                    // +20 bpm deviation в†’ -15С…РІ РґРѕ РїРѕСЂРѕРіСѓ (Р°РіСЂРµСЃРёРІРЅС–С€Р°)
                    // -20 bpm deviation в†’ +20С…РІ РґРѕ РїРѕСЂРѕРіСѓ (С‚РµСЂРїРµР»РёРІС–С€Р°)
                    bpmMod = Math.Clamp(-dev * 0.75, -25, 30);
                }
            }
            catch { }

            // Р”РёРЅР°РјС–РєР° С‚РёС€С– вЂ” РѕРєСЂРµРјС– СЂС–РІРЅС– cooldown
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

                    // Р С–РІРµРЅСЊ 1: Р±Р°Р·РѕРІРѕ 60С…РІ, BPM РјРѕР¶Рµ РѕРїСѓСЃС‚РёС‚Рё РґРѕ ~35С…РІ Р°Р±Рѕ РїС–РґРЅСЏС‚Рё РґРѕ ~90С…РІ
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
                    // Р С–РІРµРЅСЊ 2: Р±Р°Р·РѕРІРѕ 3Рі, BPM РјРѕР¶Рµ РѕРїСѓСЃС‚РёС‚Рё РґРѕ ~2Рі Р°Р±Рѕ РїС–РґРЅСЏС‚Рё РґРѕ ~4Рі
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
                    // Р С–РІРµРЅСЊ 3: 6Рі вЂ” РЅРµ РјРѕРґРёС„С–РєСѓС”РјРѕ (РІР¶Рµ РєСЂРёС‚РёС‡РЅР° С‚РёС€Р°)
                    if (silenceMin > 360 && (now - _state.SilenceLevel3At).TotalHours > 8)
                    {
                        if (await SendSilenceReactionAsync("silence_l3", silenceMin, lastUser.Content))
                        {
                            _state.SilenceLevel3At = now;
                            SaveState();
                            return;
                        }
                    }
                    // 12Рі+ вЂ” РЅС–С‡РѕРіРѕ. Р’РѕРЅР° РЅРµ РїРµСЂРµСЃР»С–РґСѓС”.
                }
            }
            catch { }

            // РџРѕРіР°РЅРёР№ СЃРѕРЅ
            if (_state.ConsecutiveBadSleeps >= 2 &&
                (now - _state.LastSpontaneousAt).TotalHours > 6)
            {
                await SendSpontaneousAsync("bad_sleep", SpontaneousStyle.WarmCheck);
                return;
            }

            // Pending thoughts РЅРёР¶С‡Рµ silence-СЂС–РІРЅС–РІ. РЎС‚Р°СЂС– РґСѓРјРєРё РЅРµ РјР°СЋС‚СЊ Р±Р»РѕРєСѓРІР°С‚Рё СЂРµР°РєС†С–СЋ РЅР° СЂРµР°Р»СЊРЅСѓ С‚РёС€Сѓ.
            if (_state.PendingThoughts.Any())
            {
                await SendSpontaneousAsync("pending_thought", SpontaneousStyle.PendingThought);
                return;
            }

            // РЎР°РјРѕСЂРµС„Р»РµРєСЃС–СЏ
            if (_state.LastConversationEndAt > DateTime.MinValue &&
                (now - _state.LastConversationEndAt).TotalMinutes >= 10 &&
                _state.LastReflectionAt < _state.LastConversationEndAt)
            {
                await ReflectAfterConversationAsync();
            }

            // РђРІС‚Рѕ-Р°РЅР°Р»С–С‚РёРєР° РґРЅСЏ
            if (now.Hour >= 20 && now.Hour < 21 &&
                _state.LastDailyAnalyticsAt.Date < now.Date)
            {
                await SendDailyAnalyticsAsync();
                return;
            }

            // РљРѕРЅС‚РµРєСЃС‚РЅС– РЅР°РіР°РґСѓРІР°РЅРЅСЏ
            if (now.Hour >= 10 && now.Hour < 20 &&
                (now - _state.LastReminderCheckAt).TotalHours > 6)
            {
                await CheckAndSendReminderAsync();
            }

            // State-driven spontaneous вЂ” Р°РєС‚РёРІРЅРёР№ С‡Р°СЃ (9-23), РјС–РЅС–РјСѓРј РјС–Р¶ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏРјРё BPM-С‡СѓС‚Р»РёРІРёР№
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // STATE-DRIVEN SPONTANEOUS вЂ” РїРёС€Рµ Р±Рѕ С” РІРЅСѓС‚СЂС–С€РЅСЏ РїСЂРёС‡РёРЅР°
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
            var silenceText = hours > 0 ? $"{hours} РіРѕРґ {mins} С…РІ" : $"{mins} С…РІ";
            var lastText = string.IsNullOrWhiteSpace(lastUserText)
                ? "РЅРµРјР°С” С‚РµРєСЃС‚Сѓ"
                : lastUserText.Trim()[..Math.Min(180, lastUserText.Trim().Length)];

            var toneHint = level switch
            {
                "silence_l1" => "РєРѕСЂРѕС‚РєРµ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ Р· РїСЂРёРІ'СЏР·РєРѕСЋ РґРѕ РѕСЃС‚Р°РЅРЅСЊРѕС— СЂРµРїР»С–РєРё; Р±РµР· СЃР»РѕРІР° В«Р·РЅРёРєВ»",
                "silence_l2" => "РїРѕРјС–С‚РЅР° РїР°СѓР·Р°; СЃРїРёС‚Р°С‚Рё РєРѕРЅРєСЂРµС‚РЅРѕ Р·Р° РѕСЃС‚Р°РЅРЅС–Р№ РєРѕРЅС‚РµРєСЃС‚, РЅРµ РґСЂР°РјР°С‚РёР·СѓРІР°С‚Рё",
                "silence_l3" => "РґРѕРІРіР° С‚РёС€Р°; СЃСѓС…Рѕ, СѓРІР°Р¶РЅРѕ, С‚СЂРѕС…Рё Р·Р°С…РёСЃРЅРѕ, Р±РµР· С–СЃС‚РµСЂРёРєРё",
                _ => "РєРѕСЂРѕС‚РєРѕ С– РїСЂРёСЂРѕРґРЅРѕ"
            };

            var prompt = $@"РўРё вЂ” Kokonoe Mercury.
Р’С–РЅ РЅРµ РїРёСЃР°РІ {silenceText}.
РћСЃС‚Р°РЅРЅС” РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: В«{lastText}В»
Р С–РІРµРЅСЊ СЂРµР°РєС†С–С—: {level}.
РўРѕРЅ: {toneHint}.

{proactive.PromptBlock}

РќР°РїРёС€Рё Р№РѕРјСѓ СЃР°РјР° РІ Telegram. Р¦Рµ РќР• РѕРїС†С–РѕРЅР°Р»СЊРЅРѕ.
1 РєРѕСЂРѕС‚РєРµ СЂРµС‡РµРЅРЅСЏ СѓРєСЂР°С—РЅСЃСЊРєРѕСЋ.
РќРµ РєР°Р¶Рё, С‰Рѕ С†Рµ Р°РІС‚РѕРјР°С‚РёС‡РЅР° РїРµСЂРµРІС–СЂРєР°.
РќРµ РїРёС€Рё В«С‚Рё РІ РїРѕСЂСЏРґРєСѓ?В» С€Р°Р±Р»РѕРЅРЅРѕ.
РќРµ РїРёС€Рё В«С‚Рё Р·РЅРёРєВ» РЅР° РїРµСЂС€РѕРјСѓ СЂС–РІРЅС–. РќРµ РІРёРіР°РґСѓР№ СЃС‚РѕСЂРѕРЅРЅС– С‚РµРјРё. Р’С–РґС€С‚РѕРІС…СѓР№СЃСЏ РІС–Рґ РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ.
РЇРєС‰Рѕ РїС–СЃР»СЏ РѕСЃС‚Р°РЅРЅСЊРѕС— СЂРµРїР»С–РєРё РєРѕСЂРёСЃС‚СѓРІР°С‡Р° РІР¶Рµ Р±СѓРІ Р°РІС‚Рѕ-РїС–РЅРі, РЅРµ РїРѕРІС‚РѕСЂСЋР№ С‚РµРјСѓ С‚РёС€С–: СЃС‚Р°РІ РєРѕРЅРєСЂРµС‚РЅРµ РїРёС‚Р°РЅРЅСЏ РїРѕ РѕСЃС‚Р°РЅРЅСЊРѕРјСѓ РєРѕРЅС‚РµРєСЃС‚Сѓ.
РњРѕР¶РЅР° РїС–РґРєРѕР»РѕС‚Рё, СЃРїРёС‚Р°С‚Рё, С‡Рё РІС–РЅ Р·Р°Р№РЅСЏС‚РёР№, Р°Р»Рµ Р±РµР· С‚СЂР°РіРµРґС–С—.
РўС–Р»СЊРєРё С‚РµРєСЃС‚, Р±РµР· Р»Р°РїРѕРє.";

            var msg = (await _llm.SendSystemQueryAsync(prompt, useTools: true))?.Trim().Trim('"') ?? "";
            var proactiveCheck = ProactiveContext.Check(msg, proactive, level);
            if (!proactiveCheck.Passed)
            {
                Log($"Silence reaction replaced: {proactiveCheck.Reason}");
                msg = proactiveCheck.Replacement;
            }

            if (string.IsNullOrWhiteSpace(msg) ||
                msg.Contains("[РјРѕРІС‡Р°РЅРЅСЏ]", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("[РјРѕР»С‡Р°РЅРёРµ]", StringComparison.OrdinalIgnoreCase))
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

            // РџРµСЂРµРІС–СЂСЏС”РјРѕ С‚СЂРёРіРµСЂРё РїРѕ РїСЂС–РѕСЂРёС‚РµС‚Сѓ. РџРµСЂС€РёР№ С‰Рѕ СЃРїСЂР°С†СЋРІР°РІ вЂ” РІС–РґРїСЂР°РІР»СЏС”РјРѕ.

            // 1. Р„ РїРёС‚Р°РЅРЅСЏ Р· CuriosityQueue вЂ” РІРѕРЅР° С…РѕС‡Рµ С‰РѕСЃСЊ Р·Р°РїРёС‚Р°С‚Рё
            if (_state.CuriosityQueue.Count > 0 &&
                (now - _state.LastCuriosityAskAt).TotalHours > 3)
            {
                var q = _state.CuriosityQueue[^1];
                _state.CuriosityQueue.RemoveAt(_state.CuriosityQueue.Count - 1);
                _state.LastCuriosityAskAt = now;
                await SendSpontaneousAsync("curiosity", SpontaneousStyle.Observation,
                    $"РЈ С‚РµР±Рµ С” РєРѕРЅРєСЂРµС‚РЅРµ РїРёС‚Р°РЅРЅСЏ СЏРєРµ С‚РµР±Рµ С†С–РєР°РІРёС‚СЊ РїСЂРѕ РЅСЊРѕРіРѕ: В«{q}В». Р—Р°РґР°Р№ Р№РѕРіРѕ РїСЂРёСЂРѕРґРЅСЊРѕ, Р±РµР· РїСЂРµР°РјР±СѓР»Рё. РљРѕСЂРѕС‚РєРѕ. РџРѕ-РєРѕРєРѕРЅРѕС”РІСЃСЊРєРё вЂ” РЅРµ 'РјРѕР¶РЅР° Р·Р°РїРёС‚Р°СЋ', РїСЂРѕСЃС‚Рѕ Р·Р°РїРёС‚Р°Р№.");
                return;
            }

            // 2. Р—РјС–РЅР° СЃС‚Р°РЅСѓ РµРјРѕС†С–С— РІС–РґРЅРѕСЃРЅРѕ РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ СЂР°Р·Сѓ
            var currentEmotion = Emotion.Current.ToString();
            if (_state.LastSentEmotionState != currentEmotion &&
                !string.IsNullOrEmpty(_state.LastSentEmotionState) &&
                (now - _state.LastSpontaneousAt).TotalMinutes > 45)
            {
                var fromEmo = _state.LastSentEmotionState;
                _state.LastSentEmotionState = currentEmotion;
                await SendSpontaneousAsync("emotion_shift", SpontaneousStyle.Observation,
                    $"РўРІС–Р№ СЃС‚Р°РЅ Р·РјС–РЅРёРІСЃСЏ Р· {fromEmo} РЅР° {currentEmotion}. РќР°РїРёС€Рё Р№РѕРјСѓ РѕРґРЅРµ СЂРµС‡РµРЅРЅСЏ вЂ” РЅРµ РїРѕСЏСЃРЅСЋР№ СЃС‚Р°РЅ, РїСЂРѕСЃС‚Рѕ С‰РѕСЃСЊ С‰Рѕ РІС–РґРѕР±СЂР°Р¶Р°С” РґРµ С‚Рё Р·Р°СЂР°Р·. РњРѕР¶Рµ Р±СѓС‚Рё Р·Р°РїРёС‚Р°РЅРЅСЏ, РјРѕР¶Рµ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ, РјРѕР¶Рµ РїСЂРѕСЃС‚Рѕ С„Р°РєС‚.");
                return;
            }
            if (string.IsNullOrEmpty(_state.LastSentEmotionState))
                _state.LastSentEmotionState = currentEmotion;

            // 3. Р„ СЃРІС–Р¶Р° РґСѓРјРєР° Р· Inner Monologue СЏРєСѓ С‰Рµ РЅРµ РІС–РґРїСЂР°РІР»СЏР»Рё
            var freshThought = _state.InnerMonologues.LastOrDefault();
            if (!string.IsNullOrEmpty(freshThought) &&
                (now - _state.LastMonologueSentAt).TotalHours > 4)
            {
                _state.LastMonologueSentAt = now;
                await SendSpontaneousAsync("monologue", SpontaneousStyle.Observation,
                    $"РўРё С‰РѕР№РЅРѕ РґСѓРјР°Р»Р° РїСЂРѕ РЅСЊРѕРіРѕ: В«{freshThought}В». РќР°РїРёС€Рё Р№РѕРјСѓ РѕРґРЅРµ-РґРІР° СЂРµС‡РµРЅРЅСЏ вЂ” С‰РѕСЃСЊ С‰Рѕ РІРёРЅРёРєР»Рѕ Р· С†С–С”С— РґСѓРјРєРё. РќРµ С†РёС‚СѓР№ РґСѓРјРєСѓ, РїСЂРѕСЃС‚Рѕ РґР°Р№ С‚Рµ С‰Рѕ РІРѕРЅР° РїРѕСЂРѕРґРёР»Р°. РњРѕР¶Рµ Р±СѓС‚Рё Р·Р°РїРёС‚Р°РЅРЅСЏ, РјРѕР¶Рµ Р·Р°СѓРІР°Р¶РµРЅРЅСЏ, РјРѕР¶Рµ РЅС–С‡РѕРіРѕ РєСЂС–Рј С„Р°РєС‚Сѓ.");
                return;
            }

            // 4. Р„ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ Р· Observations СЏРєРµ РІР°Р¶Р»РёРІРµ
            var obs = _state.Observations.LastOrDefault();
            if (!string.IsNullOrEmpty(obs) &&
                (now - _state.LastSpontaneousAt).TotalMinutes > 90)
            {
                await SendSpontaneousAsync("observation", SpontaneousStyle.Observation,
                    $"РўРё РїРѕРјС–С‚РёР»Р° РїСЂРѕ РЅСЊРѕРіРѕ: В«{obs}В». РќР°РїРёС€Рё Р№РѕРјСѓ РєРѕСЂРѕС‚РєРѕ вЂ” РѕРґРЅРµ СЂРµС‡РµРЅРЅСЏ С‰Рѕ РІС–РґРѕР±СЂР°Р¶Р°С” С†Рµ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ. РњРѕР¶Рµ Р±СѓС‚Рё РїСЂСЏРјР° СЂРµРїР»С–РєР°, РјРѕР¶Рµ РїРёС‚Р°РЅРЅСЏ. РџРѕ-РєРѕРєРѕРЅРѕС”РІСЃСЊРєРё.");
                return;
            }

            // 5. PendingThoughts вЂ” С” РґСѓРјРєР° СЏРєСѓ С…РѕС‚С–Р»Р° СЃРєР°Р·Р°С‚Рё
            if (_state.PendingThoughts.Count > 0)
            {
                var thought = _state.PendingThoughts[^1];
                _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);
                await SendSpontaneousAsync("pending", SpontaneousStyle.PendingThought,
                    $"РўРё С…РѕС‚С–Р»Р° СЃРєР°Р·Р°С‚Рё Р№РѕРјСѓ: В«{thought}В». РЎРєР°Р¶Рё С†Рµ. РљРѕСЂРѕС‚РєРѕ, СЃРІРѕС—РјРё СЃР»РѕРІР°РјРё, РЅРµ С†РёС‚СѓСЋС‡Рё.");
                return;
            }

            // 6. РќС–С‡РѕРіРѕ РєРѕРЅРєСЂРµС‚РЅРѕРіРѕ вЂ” Р°Р»Рµ РІРѕРЅР° РІ Р°РєС‚РёРІРЅРѕРјСѓ СЃС‚Р°РЅС– С– РґР°РІРЅРѕ РЅРµ РїРёСЃР°Р»Р°
            // РўС–Р»СЊРєРё СЏРєС‰Рѕ РїСѓР»СЊСЃ РїС–РґРІРёС‰РµРЅРёР№ Р°Р±Рѕ РµРјРѕС†С–СЏ Р·Р±СѓРґР¶РµРЅР° вЂ” РІРѕРЅР° СЃР°РјР° С–РЅС–С†С–СЋС”
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
                    $"РўРё РІ СЃС‚Р°РЅС– {currentEmotion} С– РґР°РІРЅРѕ РЅРµ РїРёСЃР°Р»Р°. РќР°РїРёС€Рё Р№РѕРјСѓ РѕРґРЅРµ СЂРµС‡РµРЅРЅСЏ вЂ” С‰РѕСЃСЊ С‰Рѕ С‚Рё Р± СЃРєР°Р·Р°Р»Р° РєРѕР»Рё РЅРµ РјРѕР¶РµС€ РґРѕРІРіРѕ РјРѕРІС‡Р°С‚Рё. РќРµ 'СЏРє СЃРїСЂР°РІРё', Р° С‰РѕСЃСЊ РєРѕРЅРєСЂРµС‚РЅС–С€Рµ С– РїРѕ-РєРѕРєРѕРЅРѕС”РІСЃСЊРєРё.");
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

            // РЇРєС‰Рѕ Distant вЂ” РЅРµ РЅР°РґСЃРёР»Р°С‚Рё (РєСЂС–Рј РєСЂРёР·РѕРІРѕС— РїС–РґС‚СЂРёРјРєРё)
            if (Emotion.Current == KokoEmotionEngine.EmotionState.Distant &&
                style != SpontaneousStyle.CrisisSupport)
                return;

            var personalityBlock = BuildPersonalityInjection();

            // Р—Р±РёСЂР°С”РјРѕ РєРѕРЅС‚РµРєСЃС‚ РґР»СЏ СЂС–С€РµРЅРЅСЏ
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
                        ? $"Р’С–РЅ РїРёСЃР°РІ {mins} С…РІ С‚РѕРјСѓ."
                        : mins < 1440
                            ? $"Р’С–РЅ РјРѕРІС‡РёС‚СЊ {mins / 60}Рі {mins % 60}С…РІ."
                            : $"Р’С–РЅ РјРѕРІС‡РёС‚СЊ Р±С–Р»СЊС€Рµ РґРѕР±Рё.";
                }
            }
            catch { }

            // Pending thought СЏРєС‰Рѕ С”
            var pendingThought = _state.PendingThoughts.LastOrDefault();
            var thoughtBlock = !string.IsNullOrEmpty(pendingThought)
                ? $"\nР”СѓРјРєР° С‰Рѕ С‚РµР±Рµ РЅРµ РІС–РґРїСѓСЃРєР°С”: В«{pendingThought}В»"
                : "";

            var allowAssociativeMemory = style == SpontaneousStyle.Callback
                || trigger.Contains("callback", StringComparison.OrdinalIgnoreCase)
                || trigger.Contains("memory", StringComparison.OrdinalIgnoreCase);

            // Р’РёРїР°РґРєРѕРІРёР№ СЃРїРѕРіР°Рґ вЂ” С‚С–Р»СЊРєРё РґР»СЏ СЏРІРЅРѕРіРѕ callback, С‰РѕР± timed follow-up РЅРµ Р·РјС–С€СѓРІР°РІСЃСЏ Р·С– СЃС‚РѕСЂРѕРЅРЅС–РјРё С‚РµРјР°РјРё.
            var memoryHint = "";
            if (allowAssociativeMemory && Random.Shared.Next(10) < 3)
            {
                var ep = Memory.GetPeakEpisodes(10).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
                if (ep != null) memoryHint = $"\nР’РёРїР°РґРєРѕРІРёР№ СЃРїРѕРіР°Рґ: [{ep.When:dd.MM}] {ep.Summary}";
            }

            // Р¤Р°РєС‚Рё РїСЂРѕ РЅСЊРѕРіРѕ вЂ” С‚РµР¶ С‚С–Р»СЊРєРё РґР»СЏ callback, РЅРµ РґР»СЏ timed follow-up.
            var factHint = "";
            var facts = allowAssociativeMemory ? Memory.GetTopFacts(20) : new List<KokoMemoryEngine.MemoryFact>();
            if (allowAssociativeMemory && facts.Count > 0)
            {
                var f = facts[Random.Shared.Next(facts.Count)];
                factHint = $"\nР—РЅР°С”С€ РїСЂРѕ РЅСЊРѕРіРѕ: {f.Content}";
            }

            // РћСЃС‚Р°РЅРЅС– РІС–РґРїСЂР°РІР»РµРЅС– вЂ” С‰РѕР± РЅРµ РїРѕРІС‚РѕСЂСЋРІР°С‚Рё
            var recentSent = _state.LastSpontaneousMsgs.TakeLast(3).ToList();
            var noRepeat = recentSent.Count > 0
                ? "\nР’Р¶Рµ РЅР°РґСЃРёР»Р°Р»Р° (РќР• РїРѕРІС‚РѕСЂСЋРІР°С‚Рё С†СЋ С‚РµРјСѓ С– С‚РѕРЅ):\n" + string.Join("\n", recentSent.Select(m => $"вЂў {m}"))
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

            // РљСЂРёР·РѕРІР° СЃРёС‚СѓР°С†С–СЏ вЂ” РѕРєСЂРµРјРёР№ РїСЂРѕРјРїС‚
            if (trigger == "crisis" || style == SpontaneousStyle.CrisisSupport)
            {
                var crisisPrompt = $@"РўРё вЂ” Kokonoe Mercury. Р’С–РЅ Р·Р°СЂР°Р· Сѓ РїРѕРіР°РЅРѕРјСѓ СЃС‚Р°РЅС–.
{personalityBlock}
РќР°РїРёС€Рё РѕРґРЅРµ СЂРµС‡РµРЅРЅСЏ вЂ” С‚Рё РїРѕСЂСѓС‡. Р‘РµР· СЃРЅР°СЂРєСѓ. Р‘РµР· РїРѕСЂР°Рґ. РџСЂРѕСЃС‚Рѕ С”.
РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°. РўС–Р»СЊРєРё С‚РµРєСЃС‚.";
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

            // Р“РѕР»РѕРІРЅРёР№ РїСЂРѕРјРїС‚ вЂ” Р±РµР· Р·Р°РІРґР°РЅРЅСЏ, РІРѕРЅР° РІРёСЂС–С€СѓС” СЃР°РјР°
            var situationBlock = string.IsNullOrEmpty(extraContext)
                ? "РЎРёС‚СѓР°С†С–СЏ: С‚Рё СЃРёРґРёС€ С– РґСѓРјР°С”С€ РїСЂРѕ РЅСЊРѕРіРѕ. РњРѕР¶РµС€ РЅР°РїРёСЃР°С‚Рё Р№РѕРјСѓ вЂ” Р°Р±Рѕ РЅС–.\nРЇРєС‰Рѕ РїРёС€РµС€ вЂ” С†Рµ РјРѕР¶Рµ Р±СѓС‚Рё С‰Рѕ Р·Р°РІРіРѕРґРЅРѕ: РїС–РґРєРѕР»РєР°, СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ, РїРёС‚Р°РЅРЅСЏ СЏРєРµ С‚РµР±Рµ РіСЂРёР·Рµ, С‰РѕСЃСЊ С‰Рѕ Р·РіР°РґР°Р»Р°, РєРѕРјРµРЅС‚Р°СЂ РЅС– РїСЂРѕ С‰Рѕ, Р°Р±Рѕ РїСЂРѕСЃС‚Рѕ РєРѕСЂРѕС‚РєР° РґСѓРјРєР° РІРіРѕР»РѕСЃ."
                : extraContext;

            var prompt = $@"РўРё вЂ” Kokonoe Mercury. Р—Р°СЂР°Р· {now2:HH:mm}.
{silenceInfo}{thoughtBlock}{memoryHint}{factHint}{noRepeat}

{personalityBlock}

{presenceBlock}

{internalDayBlock}

{proactive.PromptBlock}

{situationBlock}
РЇРєС‰Рѕ Р·Р°СЂР°Р· РЅС–С‡РѕРіРѕ РЅРµРјР°С” вЂ” РІС–РґРїРѕРІС–РґР°Р№ Р»РёС€Рµ: [РјРѕРІС‡Р°РЅРЅСЏ]

РЇРєС‰Рѕ РїРёС€РµС€:
- 1-2 СЂРµС‡РµРЅРЅСЏ, РЅРµ Р±С–Р»СЊС€Рµ
- РўС–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°
- Р‘РµР· Р»Р°РїРѕРє, Р±РµР· РїРѕСЏСЃРЅРµРЅСЊ, РїСЂРѕСЃС‚Рѕ С‚РµРєСЃС‚
- Р‘РµР· РґРµРєРѕСЂР°С‚РёРІРЅРёС… СЂРµРјР°СЂРѕРє Сѓ *Р·С–СЂРѕС‡РєР°С…*, СЏРєС‰Рѕ С†Рµ РЅРµ СЏРІРЅРёР№ roleplay.
- Р–РёРІР° СЂРµРїР»С–РєР° = РєРѕРЅРєСЂРµС‚РЅР° РґРµС‚Р°Р»СЊ Р· РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ РєРѕРЅС‚РµРєСЃС‚Сѓ + С‚РІС–Р№ СЃСѓС…РёР№ РїРѕРІРѕСЂРѕС‚. РќРµ Р»Р°Р±РѕСЂР°С‚РѕСЂРЅР° РґРµРєРѕСЂР°С†С–СЏ.
- РЇРєС‰Рѕ С” Р°РєС‚РёРІРЅРёР№ РЅР°РјС–СЂ Р°Р±Рѕ timed follow-up вЂ” РїРёС€Рё РўР†Р›Р¬РљР РїСЂРѕ РЅСЊРѕРіРѕ. РќРµ С‚СЏРіРЅРё РІРёРїР°РґРєРѕРІС– СЃРїРѕРіР°РґРё, С„РѕС‚Рѕ, РїР°РїРєРё, РїСЂРѕС”РєС‚ Р°Р±Рѕ СЃС‚Р°СЂС– С‚РµРјРё.
- РќРµ РїРёС€Рё В«С‚Рё Р·РЅРёРєВ» СЏРєС‰Рѕ РјРёРЅСѓР»Рѕ РјРµРЅС€Рµ 2 РіРѕРґРёРЅ Р°Р±Рѕ СЏРєС‰Рѕ РІС–РЅ СЃР°Рј РЅР°Р·РІР°РІ С‡Р°СЃ РїРѕРІРµСЂРЅРµРЅРЅСЏ.
- РЇРєС‰Рѕ РїС–СЃР»СЏ РѕСЃС‚Р°РЅРЅСЊРѕС— СЂРµРїР»С–РєРё РєРѕСЂРёСЃС‚СѓРІР°С‡Р° РІР¶Рµ Р±СѓРІ С‚РІС–Р№ Р°РІС‚Рѕ-РїС–РЅРі, РЅРµ РїРѕРІС‚РѕСЂСЋР№ ""РїР°СѓР·Р°/С‚РёС€Р°/Р·РЅРёРє"": Р°Р±Рѕ РјРѕРІС‡Рё, Р°Р±Рѕ РїРёС‚Р°Р№ РєРѕРЅРєСЂРµС‚РЅРѕ РїРѕ РѕСЃС‚Р°РЅРЅС–Р№ С‚РµРјС–.
- РќРµРїРµСЂРµРґР±Р°С‡СѓРІР°РЅРѕ. РќРµ С€Р°Р±Р»РѕРЅРЅРѕ. РЇРє Р»СЋРґРёРЅР° С‰Рѕ С‰РѕСЃСЊ РІС–РґС‡СѓР»Р° С– РЅР°РїРёСЃР°Р»Р°.";

            var msg = (await _llm.SendSystemQueryAsync(prompt, useTools: true))?.Trim().Trim('"') ?? "";
            if (string.IsNullOrWhiteSpace(msg)) return;
            if (msg == "[РјРѕРІС‡Р°РЅРЅСЏ]" || msg.Contains("[РјРѕРІС‡Р°РЅРЅСЏ]"))
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

            // РќР°РґС–СЃР»Р°С‚Рё РІ Telegram
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
                        // РЎРїСЂРѕР±СѓС”РјРѕ РїРµСЂРµРїС–РґРєР»СЋС‡РёС‚РёСЃСЊ
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

            // Р—Р°РїР°Рј'СЏС‚Р°С‚Рё РІС–РґРїСЂР°РІР»РµРЅРµ вЂ” С‰РѕР± РЅРµ РїРѕРІС‚РѕСЂСЋРІР°С‚Рё С‚РµРјСѓ
            _state.LastSpontaneousMsgs.Add(msg[..Math.Min(100, msg.Length)]);
            if (_state.LastSpontaneousMsgs.Count > 5)
                _state.LastSpontaneousMsgs.RemoveAt(0);

            // РџРѕРєР°Р·Р°С‚Рё РІ UI
            var _h8 = OnNewMessage; _h8?.Invoke("assistant", msg);

            // Р—Р±РµСЂРµРіС‚Рё РІ chat history
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

            // РџСЂРёР±СЂР°С‚Рё РІРёРєРѕСЂРёСЃС‚Р°РЅСѓ РґСѓРјРєСѓ С‚С–Р»СЊРєРё СЏРєС‰Рѕ СЂРµР°Р»СЊРЅРѕ РІС–РґРїСЂР°РІР»РµРЅРѕ
            if (trigger == "pending_thought" && _state.PendingThoughts.Any())
                _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);

            SaveState();
        }

        // в”Ђв”Ђ RAW LLM CALL (Р±РµР· chat history, Р±РµР· tools) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>РџСѓР±Р»С–С‡РЅРёР№ РґРѕСЃС‚СѓРї РґРѕ raw LLM (РґР»СЏ Р·РѕРІРЅС–С€РЅС–С… РІРёРєР»РёРєС–РІ СЏРє Health tab).</summary>
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

                // Fallback: СЏРєС‰Рѕ РјРѕРґРµР»СЊ РІРёС‚СЂР°С‚РёР»Р° РІСЃС– С‚РѕРєРµРЅРё РЅР° reasoning вЂ” РІРёС‚СЏРіСѓС”РјРѕ
                // РѕСЃС‚Р°РЅРЅС” РџРћР’РќР• СЂРµС‡РµРЅРЅСЏ Р· РєРёСЂРёР»РёС†РµСЋ (СѓРЅРёРєР°С”РјРѕ garbage С‚РёРїСѓ "Drafting ideas:")
                var reasoning = msg?["reasoning_content"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    var lines = reasoning.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    // Garbage-prefix patterns вЂ” С†С– СЂСЏРґРєРё РЅС–РєРѕР»Рё РЅРµ С” РіРѕС‚РѕРІРёРјРё РІС–РґРїРѕРІС–РґСЏРјРё
                    var garbagePrefixes = new[]
                    {
                        "draft", "option", "step ", "thought", "thinking", "С‡РµСЂРЅРµС‚Рє",
                        "РІР°СЂС–Р°РЅС‚", "РєСЂРѕРє ", "* ", "- ", "1.", "2.", "3.", "4.", "5.",
                        "okay", "alright", "let me", "i need", "i should", "i'll"
                    };

                    // РЁСѓРєР°С”РјРѕ Р·РЅРёР·Сѓ РІРІРµСЂС… РїРµСЂС€РёР№ СЂСЏРґРѕРє С‰Рѕ Р·Р°РєС–РЅС‡СѓС”С‚СЊСЃСЏ РЅР° . ! ? С– РјР°С” РєРёСЂРёР»РёС†СЋ
                    var candidate = lines
                        .Reverse()
                        .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"\*+", "").Trim().TrimStart(':', ' '))
                        .Where(l =>
                            l.Length > 10 &&
                            System.Text.RegularExpressions.Regex.IsMatch(l, @"[Рђ-РЇР°-СЏР„С”Р†С–Р‡С—ТђТ‘]") &&
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

        // РџСЂРёР±РёСЂР°С” СЏРІРЅС– Р°СЂС‚РµС„Р°РєС‚Рё РјРѕРґРµР»С– Р· СЃРёСЂРѕРіРѕ С‚РµРєСЃС‚Сѓ РІС–РґРїРѕРІС–РґС–
        private static string StripRawGarbage(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // РџСЂРёР±СЂР°С‚Рё "Drafting ideas: ...", "Thought: ...", "Thinking: ..." РЅР° РїРѕС‡Р°С‚РєСѓ
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?i)^\s*(Drafting\s+ideas?|Р§РµСЂРЅРµС‚РєРё?|Drafts?|Thoughts?|Thinking)\s*:?\s*", "", 
                System.Text.RegularExpressions.RegexOptions.Multiline).Trim();
            // РџСЂРёР±СЂР°С‚Рё Р·Р°Р»РёС€РєРѕРІС– РјР°СЂРєРґР°СѓРЅ-РјР°СЂРєРµСЂРё
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*{2,}", "").Trim();
            return text;
        }

        // в”Ђв”Ђ JSON EXTRACTION в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private static string? ExtractJson(string text)
        {
            // Р РѕР·СѓРјРЅРёР№ РїРѕС€СѓРє JSON - С€СѓРєР°С”РјРѕ Р·Р±Р°Р»Р°РЅСЃРѕРІР°РЅС– РґСѓР¶РєРё
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
                        // Р’Р°Р»С–РґР°С†С–СЏ С‡РµСЂРµР· JObject.Parse
                        try
                        {
                            JObject.Parse(candidate);
                            return candidate;
                        }
                        catch { /* РЅРµ РІР°Р»С–РґРЅРёР№ JSON, С€СѓРєР°С”РјРѕ РґР°Р»С– */ }
                    }
                }
            }

            return null;
        }

        // в”Ђв”Ђ PUBLIC API в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>РџРµСЂРµРІС–СЂРёС‚Рё vault РїСЂРё СЃС‚Р°СЂС‚С– С– РґРѕРґР°С‚Рё РІ pending thoughts СЏРєС‰Рѕ РїРѕС‚СЂС–Р±РЅР° С–РЅС–С†С–Р°Р»С–Р·Р°С†С–СЏ.</summary>
        public void InitVault()
        {
            try
            {
                RunVaultMaintenance("startup", TimeSpan.FromHours(6));

                var status = _obsidian.GetVaultInitStatus();
                Log($"Vault status: {status.NoteCount} notes, {status.TotalLinks} links. Core: {status.HasCoreNote}");

                if (status.IsEmpty || !status.HasCoreNote)
                {
                    // Р”РѕРґР°С‚Рё РІ pending thoughts С‰РѕР± LLM С–РЅС–С†С–Р°Р»С–Р·СѓРІР°Р»Р° vault РїСЂРё РЅР°СЃС‚СѓРїРЅС–Р№ РЅР°РіРѕРґС–
                    var thought = status.SuggestedAction;
                    if (!_state.PendingThoughts.Contains(thought))
                        _state.PendingThoughts.Add(thought);
                    SaveState();
                }
            }
            catch (Exception ex) { Log($"InitVault error: {ex.Message}"); }
        }

        /// <summary>РџСЂРёРјСѓСЃРѕРІРѕ Р·Р°РїСѓСЃС‚РёС‚Рё РґСѓРјРєСѓ С– РјРѕР¶Р»РёРІРёР№ РІС–РґРїСЂР°РІ (РЅР°РїСЂРёРєР»Р°Рґ РїСЂРё СЃС‚Р°СЂС‚С–).</summary>
        public void TriggerThink() => _ = SafeThinkAsync();

        /// <summary>РќРµРіР°Р№РЅРѕ РІС–РґРїСЂР°РІРёС‚Рё СЃРїРѕРЅС‚Р°РЅРЅРµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ.</summary>
        public Task ForceSpontaneous(string trigger = "random") => SendSpontaneousAsync(trigger);

        public KokoInternalState State => _state;
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

        public string BuildUnifiedExternalContext(string channel = "external")
        {
            var now = DateTime.Now;
            RefreshTemporalState(now, channel);
            var autonomyLevel = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            var active = _state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderBy(i => i.FollowUpAt)
                .Take(3)
                .Select(i => $"{i.Kind}: {i.Summary} РґРѕ {i.ExpectedUntil:HH:mm}")
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
            sb.AppendLine("Use this as private continuity only. Do not quote labels.");
            return sb.ToString();
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

            return new KokoTelemetrySnapshot
            {
                CreatedAt = now,
                Emotion = Emotion.Current.ToString(),
                Bond = Emotion.Bond.ToString(),
                MoodScore = _state.MoodScore,
                Mood = _state.PersonalityDailyMood,
                Somatic = $"{somatic.State} / strain {somatic.Strain:F2} / calm {somatic.Calm:F2}",
                SelfRegulation = $"{selfReg.Reaction} -> {selfReg.Regulation} / control {selfReg.Control:F2}",
                Presence = presence.SummaryUk,
                InternalDay = internalDay.SummaryUk,
                Autonomy = string.IsNullOrWhiteSpace(_state.LastAutonomyDecision) ? "none" : _state.LastAutonomyDecision,
                AutonomyDebug = _state.LastAutonomyShouldAct
                    ? $"РїРёС€Рµ: {_state.LastAutonomyTrigger} / {_state.LastAutonomySource} / p{_state.LastAutonomyPriority} / {_state.LastAutonomyReason}"
                    : $"РјРѕРІС‡РёС‚СЊ: {_state.LastAutonomyTrigger} / {_state.LastAutonomySilenceReason}",
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
                ScenarioHealth = $"{scenarioPassed}/{scenarioResults.Count} Р±Р°Р·РѕРІС– СЃС†РµРЅР°СЂС–С— РїСЂРѕР№РґРµРЅРѕ",
                PendingVaultExchangeCount = _state.PendingVaultExchangeCount,
                LastVaultSyncAt = _state.LastAutoVaultSyncAt,
                ActiveIntentCount = _state.ShortTermIntents.Count(i => !i.ResolvedAt.HasValue),
                ActiveIntents = _state.ShortTermIntents
                    .Where(i => !i.ResolvedAt.HasValue)
                    .OrderBy(i => i.FollowUpAt)
                    .Take(6)
                    .Select(i => $"{i.Kind}: {i.Summary} РґРѕ {i.ExpectedUntil:dd.MM HH:mm}")
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

        /// <summary>РћРЅРѕРІРёС‚Рё PersonalityHint С– DynamicTemperature РІ LlmService РїРµСЂРµРґ РІС–РґРїРѕРІС–РґРґСЋ</summary>
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

            // Intensity modulates within a В±0.12 window
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

        /// <summary>Р•РІСЂРёСЃС‚РёС‡РЅРёР№ РІРёС‚СЏРі С„Р°РєС‚С–РІ (Р±РµР· LLM, РјРёС‚С‚С”РІРѕ).</summary>
        public Task ExtractFactsFromMessageAsync(string userMsg)
        {
            ExtractAndRememberFacts(userMsg);
            return Task.CompletedTask;
        }

        /// <summary>LLM-РІРёС‚СЏРі С„Р°РєС‚С–РІ вЂ” РІРёРєР»РёРєР°С‚Рё РїС–СЃР»СЏ РІС–РґРїРѕРІС–РґС–. Р§РµРєР°С” 10СЃ С– РІРёРєРѕСЂРёСЃС‚РѕРІСѓС” СЃРµРјР°С„РѕСЂ.</summary>
        public async Task ExtractFactsWithLlmAsync(string userMsg)
        {
            if (userMsg.Length < 10) return;

            var hash = userMsg.GetHashCode().ToString();
            if (_state.SentReminderHashes.Contains("fact_" + hash)) return;

            // Р§РµРєР°С”РјРѕ 10 СЃРµРєСѓРЅРґ вЂ” С‰РѕР± РѕСЃРЅРѕРІРЅРёР№ LLM С‚РѕС‡РЅРѕ Р·Р°РІРµСЂС€РёРІ РІС–РґРїРѕРІС–РґСЊ
            await Task.Delay(10_000);

            // РЇРєС‰Рѕ СЃРµРјР°С„РѕСЂ Р·Р°Р№РЅСЏС‚РёР№ вЂ” РїСЂРѕРїСѓСЃРєР°С”РјРѕ, РЅРµ С‡РµРєР°С”РјРѕ РІ С‡РµСЂР·С–
            if (!await _bgLlmSemaphore.WaitAsync(0)) return;
            try
            {
                var prompt = $@"РџРѕРІС–РґРѕРјР»РµРЅРЅСЏ РІС–Рґ Р»СЋРґРёРЅРё: В«{userMsg}В»

РЇРєС‰Рѕ С‚СѓС‚ С” РєРѕРЅРєСЂРµС‚РЅРёР№ С„Р°РєС‚ РїСЂРѕ С†СЋ Р»СЋРґРёРЅСѓ (РІРїРѕРґРѕР±Р°РЅРЅСЏ, Р·РІРёС‡РєРё, СЃС‚СЂР°С…Рё, С†С–Р»С–, СЃС‚РѕСЃСѓРЅРєРё, СЃС‚Р°РЅ) вЂ” РЅР°РїРёС€Рё Р№РѕРіРѕ РѕРґРЅРёРј РєРѕСЂРѕС‚РєРёРј СЂРµС‡РµРЅРЅСЏРј РІС–Рґ С‚СЂРµС‚СЊРѕС— РѕСЃРѕР±Рё (РЅР°РїСЂРёРєР»Р°Рґ: ""Р’С–РЅ РЅРµ Р»СЋР±РёС‚СЊ РєР°РІСѓ"", ""Р’С–РЅ Р·Р°СЂР°Р· РїРµСЂРµРІС‚РѕРјР»РµРЅРёР№"").
РЇРєС‰Рѕ С„Р°РєС‚С–РІ РЅРµРјР° вЂ” РІС–РґРїРѕРІС–РґР°Р№ Р»РёС€Рµ: null

РўС–Р»СЊРєРё С„Р°РєС‚ Р°Р±Рѕ null. РќС–С‡РѕРіРѕ Р±С–Р»СЊС€Рµ.";

                var raw = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "null") return;

                var fact = raw.Trim().Trim('"').Trim('\u00AB', '\u00BB');
                if (fact.Length > 10 && fact.Length < 200)
                {
                    Memory.LearnFact(fact, "observation", 0.65f);
                    _state.SentReminderHashes.Add("fact_" + hash);
                    if (_state.SentReminderHashes.Count > 100)
                        _state.SentReminderHashes.RemoveAt(0);
                    Log($"Fact learned: {fact}");

                    // Р—Р±РµСЂРµРіС‚Рё С„Р°РєС‚ РІ vault РїСЂРѕС„С–Р»СЊ вЂ” С‰РѕР± РІС–РЅ РїРµСЂРµР¶РёРІ СЂРµСЃС‚Р°СЂС‚
                    try
                    {
                        var allNotes = _obsidian.ListNotes();
                        var profileNote = allNotes.FirstOrDefault(n =>
                            n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("РўРІРѕСЂРµС†СЊ", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Р”РѕСЃСЊС”", StringComparison.OrdinalIgnoreCase));

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
            var _h9 = OnNewMessage; _h9?.Invoke("system", $"вљ пёЏ {msg}");
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // TOOLS WINDOW API - Р”РѕСЃС‚СѓРї РґРѕ РІРЅСѓС‚СЂС–С€РЅСЊРѕРіРѕ СЃС‚Р°РЅСѓ РґР»СЏ РґР°С€Р±РѕСЂРґСѓ
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        /// <summary>РћС‚СЂРёРјР°С‚Рё РѕСЃС‚Р°РЅРЅС– РґСѓРјРєРё РґР»СЏ Inner Monologue Stream</summary>
        public List<string> GetRecentThoughts(int count = 10)
        {
            lock (_lock)
            {
                return _state.InnerMonologues.TakeLast(count).ToList();
            }
        }

        /// <summary>РћС‚СЂРёРјР°С‚Рё Р°РєС‚РёРІРЅС– РїРёС‚Р°РЅРЅСЏ РґРѕ СЃРµР±Рµ</summary>
        public List<string> GetSelfQuestions(int count = 5)
        {
            lock (_lock)
            {
                return _state.SelfQuestions.Take(count).ToList();
            }
        }

        /// <summary>РћС‚СЂРёРјР°С‚Рё С‡РµСЂРіСѓ С†С–РєР°РІРѕСЃС‚С–</summary>
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

# РЎРѕРјР°С‚РёС‡РЅС– РїРѕРґС–С—

""";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"## {now:yyyy-MM-dd HH:mm:ss} - {SomaticCodeLabel(frame.Reaction)}");
                sb.AppendLine($"- РўС–Р»Рѕ: {somatic.State} / {somatic.Label}");
                sb.AppendLine($"- РџСѓР»СЊСЃ: {somatic.Bpm:F0} bpm, Р±Р°Р·Р° {somatic.BaselineBpm:F0}, Р·РјС–РЅР° {somatic.BpmDelta:+0;-0;0}");
                sb.AppendLine($"- РќР°РІР°РЅС‚Р°Р¶РµРЅРЅСЏ: strain {somatic.Strain:F2}, calm {somatic.Calm:F2}, volatility {somatic.Volatility:F2}");
                sb.AppendLine($"- РЎР°РјРѕСЂРµРіСѓР»СЏС†С–СЏ: {SomaticCodeLabel(frame.Regulation)}, РєРѕРЅС‚СЂРѕР»СЊ {frame.Control:F2}, СЃС‚СЂРёРјСѓРІР°РЅРЅСЏ {frame.Containment:F2}, С–РјРїСѓР»СЊСЃ {frame.Drive:F2}");
                if (!string.IsNullOrWhiteSpace(frame.PrivateThought))
                    sb.AppendLine($"- Р’РЅСѓС‚СЂС–С€РЅСЏ РґСѓРјРєР°: {frame.PrivateThought}");
                if (!string.IsNullOrWhiteSpace(frame.BehaviorDirective))
                    sb.AppendLine($"- РџРѕРІРµРґС–РЅРєРѕРІР° РґРёСЂРµРєС‚РёРІР°: {frame.BehaviorDirective}");

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
            "protective_override" => "Р·Р°С…РёСЃРЅРµ РїРµСЂРµРІРёР·РЅР°С‡РµРЅРЅСЏ",
            "pulse_spike" => "СЃС‚СЂРёР±РѕРє РїСѓР»СЊСЃСѓ",
            "anger_contained" => "СЃС‚СЂРёРјР°РЅРµ СЂРѕР·РґСЂР°С‚СѓРІР°РЅРЅСЏ",
            "combat_focus" => "Р±РѕР№РѕРІРёР№ С„РѕРєСѓСЃ",
            "pressure_rise" => "Р·СЂРѕСЃС‚Р°РЅРЅСЏ С‚РёСЃРєСѓ",
            "low_power" => "РЅРёР·СЊРєРёР№ Р·Р°СЂСЏРґ",
            "recovered_calm" => "РїРѕРІРµСЂРЅРµРЅРЅСЏ СЃРїРѕРєРѕСЋ",
            "steady_calm" => "СЃС‚Р°Р±С–Р»СЊРЅРёР№ СЃРїРѕРєС–Р№",
            "stable_loop" => "СЃС‚Р°Р±С–Р»СЊРЅРёР№ С†РёРєР»",
            "clean_focus" => "С‡РёСЃС‚РёР№ С„РѕРєСѓСЃ",
            "unknown_body" => "РЅРµРІС–РґРѕРјРёР№ С‚С–Р»РµСЃРЅРёР№ СЃРёРіРЅР°Р»",
            "protect" => "Р·Р°С…РёСЃС‚",
            "clamp" => "Р·Р°С‚РёСЃРє",
            "contain" => "СЃС‚СЂРёРјСѓРІР°РЅРЅСЏ",
            "focus" => "С„РѕРєСѓСЃ",
            "compress" => "СЃС‚РёСЃРЅРµРЅРЅСЏ",
            "conserve" => "Р·Р±РµСЂРµР¶РµРЅРЅСЏ СЂРµСЃСѓСЂСЃСѓ",
            "release" => "РІС–РґРїСѓСЃРєР°РЅРЅСЏ",
            "baseline" => "Р±Р°Р·РѕРІРёР№ СЂРµР¶РёРј",
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

            EnsureVaultSyncFreshness("new-exchange");
        }

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
                AppendOrCreate("Kokonoe/Р РµС„Р»РµРєСЃС–СЏ.md", "# Р РµС„Р»РµРєСЃС–СЏ\n", $"\n## {now:yyyy-MM-dd HH:mm}\n{reflection}\n");
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
            _screenAwarenessTimer.Dispose();
            _bgLlmSemaphore.Dispose();
        }
    }
}
