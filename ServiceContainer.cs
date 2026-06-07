using System;
using System.IO;
using KokonoeAssistant;
using KokonoeAssistant.Services;

namespace KokonoeAssistant
{
    public static class ServiceContainer
    {
        private static readonly object _lock = new();
        private static string? _vault;

        private static ChatRepository?      _chatRepo;
        private static AudioRecordService?  _audio;
        private static WhisperService?      _whisper;
        private static SearchService?       _search;
        private static SummarizerService?   _summarizer;
        private static KnowledgeGraph?      _graph;
        private static EnhancedMemory?      _memory;
        private static KokonoeDataManager?  _dataManager;
        private static StateEngine?         _stateEngine;
        private static KokoEmotionEngine?   _emotion;
        private static KokoMemoryEngine?    _kokoMemory;
        private static KokoPatternEngine?   _kokoPatterns;
        private static LlmService?          _llm;
        private static HealthService?       _health;
        private static ObsidianMcpService?  _obsidian;
        private static KokoBrainEngine?     _brain;
        private static GoalService?         _goals;
        private static HabitService?        _habits;
        private static CalendarService?     _calendar;
        private static ChatLogger?          _chatLogger;
        private static TelegramUserService? _tgUser;
        private static PcControlService?      _pcControl;
        private static KokoEmbeddingService?   _embedding;
        private static KokoPredictorService?   _predictor;
        private static KokoHeartEngine?        _heart;
        private static KokoWearableTelemetryService? _wearable;
        private static KokoWearableBridgeService? _wearableBridge;
        private static OllamaKeyPoolService?   _ollamaPool;
        private static KokoAgentTaskService?   _agentTasks;
        private static KokoAgentRuntimeService? _agentRuntime;
        private static KokoFileSystemToolService? _fileTools;
        private static KokoCapabilityManifestService? _capabilities;
        private static KokoPhotoFileWatcherService? _photoWatcher;

        public static void Initialize(string vaultPath)
        {
            lock (_lock) { _vault = vaultPath; }
            KokoSystemLog.Configure(Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"));
            try { _ = WearableBridge; } catch { }
            try { PhotoFileWatcher.Start(); } catch { }
        }

        public static bool IsInitialized
        {
            get { lock (_lock) { return !string.IsNullOrWhiteSpace(_vault); } }
        }

        public static ChatRepository ChatRepository
        {
            get { lock (_lock) { return _chatRepo ??= new ChatRepository(_vault); } }
        }

        public static AudioRecordService AudioRecordService
        {
            get { lock (_lock) { return _audio ??= new AudioRecordService(); } }
        }

        public static WhisperService WhisperService
        {
            get
            {
                lock (_lock)
                {
                    if (_whisper == null)
                    {
                        var modelDir = Path.Combine(
                            _vault ?? AppDomain.CurrentDomain.BaseDirectory, "models");
                        _whisper = new WhisperService(modelDir,
                            AppSettings.Load().OpenAiApiKey);
                    }
                    return _whisper;
                }
            }
        }

        public static KokoEmbeddingService EmbeddingService
        {
            get
            {
                lock (_lock)
                {
                    if (_embedding == null)
                    {
                        var s = AppSettings.Load();
                        // Той самий хост що й основний LLM (Ollama/LM Studio зазвичай на 11434)
                        var host = s.LmUrl?.Replace("/v1/chat/completions", "").TrimEnd('/') ?? "http://localhost:11434";
                        _embedding = new KokoEmbeddingService(host, "nomic-embed-text");
                        _ = _embedding.PingAsync(); // background check
                    }
                    return _embedding;
                }
            }
        }

        public static KokoPredictorService Predictor
        {
            get
            {
                lock (_lock)
                {
                    return _predictor ??= new KokoPredictorService(
                        HealthService,
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"));
                }
            }
        }

        public static SearchService SearchService
        {
            get { lock (_lock) { return _search ??= new SearchService(ChatRepository, _vault); } }
        }

        public static SummarizerService SummarizerService
        {
            get { lock (_lock) { return _summarizer ??= new SummarizerService(); } }
        }

        public static KnowledgeGraph KnowledgeGraph
        {
            get { lock (_lock) { return _graph ??= new KnowledgeGraph(_vault ?? throw new InvalidOperationException("Not initialized")); } }
        }

        public static EnhancedMemory EnhancedMemory
        {
            get { lock (_lock) { return _memory ??= new EnhancedMemory(_vault ?? throw new InvalidOperationException("Not initialized"), KnowledgeGraph); } }
        }

        public static KokonoeDataManager DataManager
        {
            get { lock (_lock) { return _dataManager ??= new KokonoeDataManager(_vault ?? throw new InvalidOperationException("Not initialized")); } }
        }

        public static StateEngine StateEngine
        {
            get { lock (_lock) { return _stateEngine ??= new StateEngine(_vault ?? throw new InvalidOperationException("Not initialized"), KnowledgeGraph, DataManager); } }
        }

        public static LlmService LlmService
        {
            get
            {
                lock (_lock)
                {
                    if (_llm == null)
                    {
                        _llm = new LlmService();
                        _llm.Obsidian   = ObsidianMcp;
                        _llm.Health     = HealthService;
                        _llm.State      = StateEngine;
                        _llm.OllamaPool = OllamaPool;
                        _llm.AgentTasks = AgentTasks;
                        // Ensure BrainEngine is initialized to wire up Emotion, Memory, Patterns
                        // This ensures all LlmService dependencies are properly set
                        var brain = BrainEngine;
                    }
                    return _llm;
                }
            }
        }

        public static OllamaKeyPoolService OllamaPool
        {
            get { lock (_lock) { return _ollamaPool ??= new OllamaKeyPoolService(); } }
        }

        public static KokoAgentTaskService AgentTasks
        {
            get
            {
                lock (_lock)
                {
                    if (_agentTasks == null)
                    {
                        _agentTasks = new KokoAgentTaskService(
                            Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                            LlmService,
                            ObsidianMcp)
                        {
                            MaxParallel = 4,
                            AutoStartOnAdd = true
                        };
                        _agentTasks.Start();
                    }
                    return _agentTasks;
                }
            }
        }

        public static KokoAgentRuntimeService AgentRuntime
        {
            get
            {
                lock (_lock)
                {
                    return _agentRuntime ??= new KokoAgentRuntimeService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        LlmService,
                        ObsidianMcp);
                }
            }
        }

        public static KokoFileSystemToolService FileTools
        {
            get
            {
                lock (_lock)
                {
                    return _fileTools ??= new KokoFileSystemToolService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data", "agent-files"));
                }
            }
        }

        public static KokoCapabilityManifestService Capabilities
        {
            get { lock (_lock) { return _capabilities ??= new KokoCapabilityManifestService(); } }
        }

        public static KokoPhotoFileWatcherService PhotoFileWatcher
        {
            get
            {
                lock (_lock)
                {
                    return _photoWatcher ??= new KokoPhotoFileWatcherService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        () => LlmService,
                        ChatRepository,
                        () => ChatLogger,
                        () => KokoMemory,
                        () => ObsidianMcp);
                }
            }
        }

        public static HealthService HealthService
        {
            get { lock (_lock) { return _health ??= new HealthService(_vault ?? throw new InvalidOperationException("Not initialized")); } }
        }

        public static ObsidianMcpService ObsidianMcp
        {
            get { lock (_lock) { return _obsidian ??= new ObsidianMcpService(_vault ?? throw new InvalidOperationException("Not initialized")); } }
        }

        public static GoalService GoalService
        {
            get { lock (_lock) { return _goals ??= new GoalService(_vault ?? AppDomain.CurrentDomain.BaseDirectory, DataManager); } }
        }

        public static HabitService HabitService
        {
            get { lock (_lock) { return _habits ??= new HabitService(_vault ?? AppDomain.CurrentDomain.BaseDirectory, DataManager); } }
        }

        public static PcControlService PcControl
        {
            get { lock (_lock) { return _pcControl ??= new PcControlService(); } }
        }

        public static CalendarService Calendar
        {
            get { lock (_lock) { return _calendar ??= new CalendarService(Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data")); } }
        }

        public static KokoEmotionEngine EmotionEngine
        {
            get { lock (_lock) { return _emotion ??= new KokoEmotionEngine(Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data")); } }
        }

        public static KokoMemoryEngine KokoMemory
        {
            get { lock (_lock) { return _kokoMemory ??= new KokoMemoryEngine(Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"), EnhancedMemory, EmbeddingService); } }
        }

        public static KokoHeartEngine Heart
        {
            get
            {
                lock (_lock)
                {
                    if (_heart == null)
                    {
                        var dir = Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data");
                        _heart = new KokoHeartEngine(EmotionEngine, dir, WearableTelemetry);
                        _heart.Start();
                    }
                    return _heart;
                }
            }
        }

        public static KokoWearableTelemetryService WearableTelemetry
        {
            get
            {
                lock (_lock)
                {
                    var dir = Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data");
                    return _wearable ??= new KokoWearableTelemetryService(dir);
                }
            }
        }

        public static KokoWearableBridgeService WearableBridge
        {
            get
            {
                lock (_lock)
                {
                    if (_wearableBridge == null)
                    {
                        var dir = Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data");
                        var settings = AppSettings.Load();
                        _wearableBridge = new KokoWearableBridgeService(
                            WearableTelemetry,
                            dir,
                            settings.WearBridgePort,
                            settings.WearBridgeExternalUrls.Split(',', ';', '\n', '\r'));
                        if (settings.WearBridgeEnabled)
                            _wearableBridge.Start();
                    }
                    return _wearableBridge;
                }
            }
        }

        public static void ReloadWearableBridge()
        {
            lock (_lock)
            {
                try { _wearableBridge?.Dispose(); } catch { }
                _wearableBridge = null;
                if (AppSettings.Load().WearBridgeEnabled)
                    _ = WearableBridge;
            }
        }

        public static KokoPatternEngine KokoPatterns
        {
            get { lock (_lock) { return _kokoPatterns ??= new KokoPatternEngine(Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data")); } }
        }

        public static ChatLogger ChatLogger
        {
            get { lock (_lock) { return _chatLogger ??= new ChatLogger(ObsidianMcp); } }
        }

        public static TelegramUserService? TelegramUser
        {
            get { lock (_lock) { return _tgUser; } }
            set { lock (_lock) { _tgUser = value; } }
        }

        public static KokoBrainEngine BrainEngine
        {
            get
            {
                lock (_lock)
                {
                    if (_brain == null)
                    {
                        var dataDir = Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data");
                        _brain = new KokoBrainEngine(
                            LlmService, HealthService, ObsidianMcp, ChatRepository, dataDir,
                            enhanced:    EnhancedMemory,
                            stateEngine: StateEngine,
                            goals:       GoalService,
                            habits:      HabitService,
                            embeddings:  EmbeddingService);
                        // Wire brain's internal engines to LlmService
                        LlmService.Emotion    = _brain.Emotion;
                        LlmService.Memory     = _brain.Memory;
                        LlmService.Patterns   = _brain.Patterns;
                        LlmService.Scheduler  = _brain.Scheduler;
                        LlmService.Goals      = GoalService;
                    }
                    return _brain;
                }
            }
        }

        public static void Disposing()
        {
            lock (_lock)
            {
                try
                {
                    _chatRepo?.Dispose();
                    _audio?.Dispose();
                    _health?.Dispose();
                    // HttpClient in LlmService should be disposed properly
                    _llm?.ClearHistory(); // cleanup any pending operations
                    _chatRepo = null; _audio = null; _whisper = null;
                    _search = null; _summarizer = null; _graph = null;
                    _memory = null; _dataManager = null; _stateEngine = null;
                    _llm = null; _health = null; _obsidian = null;
                    _goals = null; _habits = null;
                    _emotion = null; _kokoMemory = null; _kokoPatterns = null;
                    _chatLogger = null;
                    _tgUser?.Dispose(); _tgUser = null;
                    _brain?.Dispose(); _brain = null;
                    _heart?.Dispose(); _heart = null;
                    _wearableBridge?.Dispose(); _wearableBridge = null;
                    _agentTasks?.Stop(); _agentTasks = null;
                    _agentRuntime = null;
                    _fileTools = null;
                    _capabilities = null;
                    _photoWatcher?.Dispose(); _photoWatcher = null;
                }
                catch { }
            }
        }
    }
}
