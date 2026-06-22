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
    // BRAIN ENGINE
    // ------------------------------------------------------------
    public partial class KokoBrainEngine : IDisposable
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        private readonly LlmService _llm;
        private readonly HealthService _health;
        private readonly ObsidianMcpService _obsidian;
        private readonly ChatRepository _chatRepo;
        private readonly string _statePath;
        // ── Нові двигуни ─────────────────────────────────────────────
        public readonly KokoMemoryEngine Memory;
        public readonly KokoEmotionEngine Emotion;
        public readonly KokoPatternEngine Patterns;
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
        public readonly KokoSubconsciousMonologueEngine Subconscious;
        public readonly KokoAsyncPersonalityEngine AsyncPersonality;
        public readonly KokoTemporalPresenceAwarenessEngine TemporalPresence;
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
        public readonly KokoCollectiveMindService CollectiveMind;
        // ── Зовнішні сервіси (опціональні) ───────────────────────────
        private EnhancedMemory? _enhanced;
        private StateEngine? _stateEngine;
        private GoalService? _goalService;
        private HabitService? _habitService;
        private ContextAnalyzer? _contextAnalyzer;
        private readonly ActivityAnalyzer _screenActivityAnalyzer = new();
        private const int StaticContextTtlSeconds = 45;
        private readonly SemaphoreSlim _staticContextCacheGate = new(1, 1);
        private string? _cachedStaticContext;
        private DateTime _staticContextExpiry = DateTime.MinValue;
        private long _staticContextCacheHits;
        private long _staticContextCacheMisses;
        // Screen context cache
        private string _cachedScreenContext = "";
        private DateTime _screenContextCachedAt = DateTime.MinValue;
        private DateTime _lastScreenRefreshAt = DateTime.MinValue;
        private DateTime _lastDailyBriefingAt = DateTime.MinValue;
        private DateTime _lastWeeklyDigestAt = DateTime.MinValue;
        private DateTime _lastArchitectureReviewAt = DateTime.MinValue;
        // _lastWhatMissedAt тепер в _state.LastWhatMissedAt (зберігається між сесіями)
        private TelegramBotClient? _tgBot;
        private long _tgChatId;
        private bool _tgInitialized;
        private KokoInternalState _state;
        private readonly System.Threading.Timer _thinkTimer;
        private readonly System.Threading.Timer _spontaneousTimer;
        private readonly System.Threading.Timer _screenAwarenessTimer;
        private readonly System.Threading.Timer _resourceGuardianTimer;
        private readonly System.Threading.Timer _stateCheckpointTimer;
        private readonly System.Threading.Timer _dailyReviewTimer;
        private bool _disposed;
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
        private readonly object _lock = new();
        private DateTime _lastInAppSilenceMsgAt = DateTime.MinValue;
        // Callback для відображення повідомлень в UI чаті
        public Action<string, string>? OnNewMessage; // (role, content)
        public KokoBrainEngine(LlmService llm, HealthService health, ObsidianMcpService obsidian, ChatRepository chatRepo, string dataDir, EnhancedMemory? enhanced = null, StateEngine? stateEngine = null, GoalService? goals = null, HabitService? habits = null, ContextAnalyzer? contextAnalyzer = null, KokoEmbeddingService? embeddings = null)
        {
            _llm = llm;
            _health = health;
            _obsidian = obsidian;
            _chatRepo = chatRepo;
            _enhanced = enhanced;
            _stateEngine = stateEngine;
            _goalService = goals;
            _habitService = habits;
            _contextAnalyzer = contextAnalyzer;
            Directory.CreateDirectory(dataDir);
            _statePath = Path.Combine(dataDir, "kokonoe-brain.json");
            _state = LoadState();
            // Ініціалізація нових двигунів
            Memory = new KokoMemoryEngine(dataDir, enhanced, embeddings);
            Emotion = new KokoEmotionEngine(dataDir);
            Patterns = new KokoPatternEngine(dataDir);
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
            Subconscious = new KokoSubconsciousMonologueEngine();
            AsyncPersonality = new KokoAsyncPersonalityEngine();
            TemporalPresence = new KokoTemporalPresenceAwarenessEngine();
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
            CollectiveMind = new KokoCollectiveMindService();
            // Підключити нові сервіси в LLM
            _llm.Memory = Memory;
            _llm.Patterns = Patterns;
            _llm.Scheduler = Scheduler;
            _llm.Goals = goals;
            // Думати кожні 90 хвилин (внутрішній монолог + факти)
            // Раніше було 30хв — занадто часто, засмічує контекст і витрачає GPU
            _thinkTimer = new System.Threading.Timer(_ => _ = GuardedThinkAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(90));
            // Спонтанні перевірки частіші за фактичні повідомлення:
            // рішення все одно проходить через cooldown, настрій, соматику і Telegram guard.
            _spontaneousTimer = new System.Threading.Timer(_ => _ = GuardedSpontaneousAsync(), null, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(10));
            _screenAwarenessTimer = new System.Threading.Timer(_ => _ = GuardedScreenAwarenessAsync(), null, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5));
            _resourceGuardianTimer = new System.Threading.Timer(_ => _ = GuardedResourceGuardianAsync(), null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));
            _stateCheckpointTimer = new System.Threading.Timer(_ => CheckpointStateAndHeartbeat(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            _dailyReviewTimer = new System.Threading.Timer(_ => _ = TryDailySelfReviewAsync("daily-review-timer"), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));
            HookAgentCompletionEvents();
            HookWearableTelemetryEvents();
            HookWearableActionEvents();
            CheckpointStateAndHeartbeat();
        }

        // ── Стилі спонтанних повідомлень ──────────────────────────────
        private enum SpontaneousStyle
        {
            ColdCheck, // "ти ще живий?"
            WarmCheck, // тихе "як ти" без зайвих слів
            Observation, // підкидає думку або спостереження
            Callback, // посилання на конкретний минулий момент
            Jab, // легкий укус — просто Kokonoe
            CrisisSupport, // він у кризі — коротко, без снарку
            NightMessage, // пізно, він не спить — тихе
            Morning, // ранок
            PendingThought, // є думка яку хотіла сказати
        }

        public KokoInternalState State => _state;
    }
}
