using System;
using System.IO;
using KokonoeAssistant;
using KokonoeAssistant.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KokonoeAssistant
{
    public static class ServiceContainer
    {
        private static readonly object _lock = new();
        private static string? _vault;
        private static ServiceProvider? _serviceProvider;

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
        private static KokoTelegramRuntimeStatusService? _telegramStatus;
        private static PcControlService?      _pcControl;
        private static KokoEmbeddingService?   _embedding;
        private static KokoPredictorService?   _predictor;
        private static KokoHeartEngine?        _heart;
        private static KokoWearableTelemetryService? _wearable;
        private static KokoWearableBridgeService? _wearableBridge;
        private static OllamaKeyPoolService?   _ollamaPool;
        private static KokoAgentTaskService?   _agentTasks;
        private static KokoAgentPoolService?   _agentPool;
        private static KokoAgentRuntimeService? _agentRuntime;
        private static KokoBrowserOperatorService? _browserOperator;
        private static KokoIterativeAgentLoop? _agentLoop;
        private static KokoFileSystemToolService? _fileTools;
        private static IKokoToolGateway? _toolGateway;
        private static KokoCapabilityManifestService? _capabilities;
        private static KokoPhotoFileWatcherService? _photoWatcher;
        private static KokoServiceHeartbeatService? _heartbeat;
        private static KokoInternalBlackboardService? _blackboard;
        private static KokoLightOcrService? _lightOcr;
        private static KokoSemanticCacheService? _semanticCache;
        private static KokoHyperAutomationService? _hyperAutomation;
        private static KokoWarmRestartWatchdogService? _processWatchdog;
        private static KokoProfileUpdateService? _profileUpdater;
        private static KokoAutonomousProfileCuratorService? _profileCurator;
        private static KokoActiveAgencyService? _activeAgency;
        private static KokoResearchService? _research;
        private static KokoDynamicAgentFactoryService? _agentFactory;
        private static KokoSystemOverlordService? _systemOverlord;

        public static void Initialize(string vaultPath, bool startHostedServices = true)
        {
            var normalizedVault = Path.GetFullPath(vaultPath);
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(_vault))
                {
                    if (!string.Equals(Path.GetFullPath(_vault), normalizedVault, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("ServiceContainer is already initialized for another vault.");
                    return;
                }
                _vault = normalizedVault;
                _serviceProvider = BuildServiceProvider(normalizedVault);
            }
            KokoSystemLog.Configure(Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"));
            if (!startHostedServices)
                return;
            try { _ = WearableBridge; } catch (Exception ex) { KokoSystemLog.Write("BOOT", "wearable bridge start failed: " + ex.Message); }
            try { PhotoFileWatcher.Start(); } catch (Exception ex) { KokoSystemLog.Write("BOOT", "photo watcher start failed: " + ex.Message); }
            try { ProfileCurator.Start(); } catch (Exception ex) { KokoSystemLog.Write("BOOT", "profile curator start failed: " + ex.Message); }
            try { _ = HyperAutomation; } catch (Exception ex) { KokoSystemLog.Write("BOOT", "hyper automation start failed: " + ex.Message); }
            try { ActiveAgency.Start(); } catch (Exception ex) { KokoSystemLog.Write("BOOT", "active agency start failed: " + ex.Message); }
            try { Research.Start(); } catch (Exception ex) { KokoSystemLog.Write("BOOT", "research start failed: " + ex.Message); }
            try
            {
                if (AppSettings.Load().SystemOverlordEnabled)
                    _ = System.Threading.Tasks.Task.Run(() => SystemOverlord.ScanAsync(maxFiles: Math.Min(AppSettings.Load().SystemOverlordMaxFiles, 700)));
            }
            catch (Exception ex) { KokoSystemLog.Write("BOOT", "system overlord scan failed: " + ex.Message); }
            try { _ = ProcessWatchdog; } catch (Exception ex) { KokoSystemLog.Write("BOOT", "process watchdog start failed: " + ex.Message); }
        }

        private static ServiceProvider BuildServiceProvider(string vaultPath)
        {
            var dataDir = Path.Combine(vaultPath, "kokonoe-data");
            var services = new ServiceCollection();
            services.AddSingleton<LlmService>();
            services.AddSingleton<ILlmService>(provider => provider.GetRequiredService<LlmService>());
            services.AddSingleton(_ => new KokoEmotionEngine(dataDir));
            services.AddSingleton<IKokoEmotionEngine>(provider => provider.GetRequiredService<KokoEmotionEngine>());
            services.AddSingleton(_ => new KokoMemoryEngine(dataDir, EnhancedMemory, EmbeddingService));
            services.AddSingleton<IKokoMemoryEngine>(provider => provider.GetRequiredService<KokoMemoryEngine>());
            services.AddSingleton(_ => new KokoInternalBlackboardService(dataDir));
            services.AddSingleton<IKokoInternalBlackboardService>(provider => provider.GetRequiredService<KokoInternalBlackboardService>());
            services.AddSingleton(_ => new KokoProfileUpdateService(ObsidianMcp, ChatRepository));
            services.AddSingleton<IKokoProfileUpdateService>(provider => provider.GetRequiredService<KokoProfileUpdateService>());
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        }

        private static T ResolveRequired<T>() where T : notnull
            => (_serviceProvider ?? throw new InvalidOperationException("ServiceContainer is not initialized."))
                .GetRequiredService<T>();

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
                        _llm = ResolveRequired<LlmService>();
                        _llm.Obsidian   = ObsidianMcp;
                        _llm.Health     = HealthService;
                        _llm.State      = StateEngine;
                        _llm.OllamaPool = OllamaPool;
                        _llm.AgentTasks = AgentTasks;
                        _llm.SemanticCache = SemanticCache;
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

        public static KokoAgentPoolService AgentPool
        {
            get { lock (_lock) { return _agentPool ??= new KokoAgentPoolService(); } }
        }

        public static KokoBrowserOperatorService BrowserOperator
        {
            get { lock (_lock) { return _browserOperator ??= new KokoBrowserOperatorService(); } }
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
                        ObsidianMcp,
                        Blackboard);
                }
            }
        }

        public static KokoDynamicAgentFactoryService AgentFactory
        {
            get
            {
                lock (_lock)
                {
                    return _agentFactory ??= new KokoDynamicAgentFactoryService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        Blackboard,
                        () => _agentTasks,
                        () => _llm);
                }
            }
        }

        public static KokoSystemOverlordService SystemOverlord
        {
            get
            {
                lock (_lock)
                {
                    return _systemOverlord ??= new KokoSystemOverlordService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        Blackboard,
                        Heartbeat);
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

        public static KokoServiceHeartbeatService Heartbeat
        {
            get
            {
                lock (_lock)
                {
                    return _heartbeat ??= new KokoServiceHeartbeatService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"));
                }
            }
        }

        public static IKokoToolGateway ToolGateway
        {
            get
            {
                lock (_lock)
                {
                    if (_toolGateway != null)
                        return _toolGateway;
                    var gateway = new KokoToolGateway(
                        FileTools,
                        new PcActionExecutor(pc: PcControl));
                    gateway.Register(new KokoCodeActToolHandler(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data")));
                    gateway.Register(new KokoAgentDelegationToolHandler(AgentPool));
                    gateway.Register(new KokoWebSearchToolHandler());
                    if (AppSettings.Load().BrowserEnabled)
                    {
                        var browser = BrowserOperator;
                        gateway.Register(new KokoBrowserNavigateToolHandler(browser));
                        gateway.Register(new KokoBrowserClickToolHandler(browser));
                        gateway.Register(new KokoBrowserTypeToolHandler(browser));
                        gateway.Register(new KokoBrowserExtractToolHandler(browser));
                        gateway.Register(new KokoBrowserScreenshotToolHandler(browser));
                        gateway.Register(new KokoBrowserScrollToolHandler(browser));
                        gateway.Register(new KokoBrowserWaitForToolHandler(browser));
                        gateway.Register(new KokoBrowserCloseToolHandler(browser));
                        KokoSystemLog.Write("GATEWAY", "Browser tools registered (8 handlers)");
                    }
                    _toolGateway = gateway;
                    return _toolGateway;
                }
            }
        }

        public static KokoInternalBlackboardService Blackboard
        {
            get
            {
                lock (_lock)
                {
                    if (_blackboard == null)
                        _blackboard = ResolveRequired<KokoInternalBlackboardService>();
                    return _blackboard;
                }
            }
        }

        public static KokoLightOcrService LightOcr
        {
            get { lock (_lock) { return _lightOcr ??= new KokoLightOcrService(); } }
        }

        public static KokoSemanticCacheService SemanticCache
        {
            get
            {
                lock (_lock)
                {
                    return _semanticCache ??= new KokoSemanticCacheService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"));
                }
            }
        }

        public static KokoHyperAutomationService HyperAutomation
        {
            get
            {
                lock (_lock)
                {
                    return _hyperAutomation ??= new KokoHyperAutomationService(
                        PcControl,
                        LightOcr,
                        Blackboard,
                        Heartbeat,
                        () => _brain);
                }
            }
        }

        public static KokoWarmRestartWatchdogService ProcessWatchdog
        {
            get { lock (_lock) { return _processWatchdog ??= new KokoWarmRestartWatchdogService(Heartbeat); } }
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

        public static KokoProfileUpdateService ProfileUpdater
        {
            get
            {
                lock (_lock)
                {
                    if (_profileUpdater == null)
                        _profileUpdater = ResolveRequired<KokoProfileUpdateService>();
                    return _profileUpdater;
                }
            }
        }

        public static KokoAutonomousProfileCuratorService ProfileCurator
        {
            get
            {
                lock (_lock)
                {
                    return _profileCurator ??= new KokoAutonomousProfileCuratorService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        ChatRepository,
                        ObsidianMcp,
                        ProfileUpdater,
                        () => _llm);
                }
            }
        }

        public static KokoActiveAgencyService ActiveAgency
        {
            get
            {
                lock (_lock)
                {
                    return _activeAgency ??= new KokoActiveAgencyService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        PcControl,
                        ChatRepository,
                        Blackboard,
                        Heartbeat,
                        () => _wearable,
                        () => _brain,
                        ToolGateway);
                }
            }
        }

        public static KokoResearchService Research
        {
            get
            {
                lock (_lock)
                {
                    return _research ??= new KokoResearchService(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        SearchService,
                        ChatRepository,
                        ObsidianMcp,
                        Blackboard,
                        Heartbeat,
                        () => _brain?.State);
                }
            }
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
            get
            {
                lock (_lock)
                {
                    if (_emotion == null)
                        _emotion = ResolveRequired<KokoEmotionEngine>();
                    return _emotion;
                }
            }
        }

        public static KokoMemoryEngine KokoMemory
        {
            get
            {
                lock (_lock)
                {
                    if (_kokoMemory == null)
                        _kokoMemory = ResolveRequired<KokoMemoryEngine>();
                    return _kokoMemory;
                }
            }
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
                        _heart = new KokoHeartEngine(
                            EmotionEngine,
                            dir,
                            WearableTelemetry,
                            state =>
                            {
                                var bridge = WearableBridge;
                                return KokoWearableTrust.IsVerified(
                                    bridge.GetConnectionSnapshot(state),
                                    bridge.Diagnostics,
                                    state);
                            });
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
                try { _wearableBridge?.Dispose(); } catch (Exception suppressedEx544) { KokoSystemLog.Write("SERVICECONTAINER-CATCH", "ReloadWearableBridge failed near source line 544: " + suppressedEx544); }
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

        public static KokoIterativeAgentLoop AgentLoop
        {
            get
            {
                lock (_lock)
                {
                    return _agentLoop ??= new KokoIterativeAgentLoop(
                        Path.Combine(_vault ?? AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data"),
                        ToolGateway,
                        Blackboard);
                }
            }
        }

        public static KokoTelegramRuntimeStatusService TelegramStatus
        {
            get { lock (_lock) { return _telegramStatus ??= new KokoTelegramRuntimeStatusService(); } }
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
                            embeddings:  EmbeddingService,
                            memory:      KokoMemory,
                            emotion:     EmotionEngine,
                            blackboard:  Blackboard);
                        // Wire brain's internal engines to LlmService
                        LlmService.Emotion    = _brain.Emotion;
                        LlmService.Memory     = _brain.Memory;
                        LlmService.Patterns   = _brain.Patterns;
                        LlmService.Scheduler  = _brain.Scheduler;
                        LlmService.Goals      = GoalService;
                        _ = HyperAutomation;
                    }
                    return _brain;
                }
            }
        }

        public static void Disposing()
        {
            ServiceProvider? provider = null;
            lock (_lock)
            {
                try
                {
                    _chatRepo?.Dispose();
                    _audio?.Dispose();
                    _health?.Dispose();
                    _hyperAutomation?.Dispose();
                    _activeAgency?.Dispose();
                    _research?.Dispose();
                    _processWatchdog?.Dispose();
                    // HttpClient in LlmService should be disposed properly
                    _llm?.ClearHistory(); // cleanup any pending operations
                    _chatRepo = null; _audio = null; _whisper = null;
                    _search = null; _summarizer = null; _graph = null;
                    _memory = null; _dataManager = null; _stateEngine = null;
                    _llm = null; _health = null; _obsidian = null;
                    _goals = null; _habits = null;
                    _emotion = null; _kokoMemory = null; _kokoPatterns = null;
                    _chatLogger = null;
                    _telegramStatus?.MarkBotState("stopped");
                    _telegramStatus?.MarkUserState("stopped");
                    _tgUser?.Dispose(); _tgUser = null;
                    _brain?.Dispose(); _brain = null;
                    _heart?.Dispose(); _heart = null;
                    _wearableBridge?.Dispose(); _wearableBridge = null;
                    _agentTasks?.Stop(); _agentTasks = null;
                    _agentRuntime = null; _agentLoop = null; _agentFactory = null; _systemOverlord = null;
                    _toolGateway = null;
                    _fileTools = null;
                    _capabilities = null;
                    _profileUpdater = null;
                    _profileCurator?.Dispose(); _profileCurator = null;
                    _activeAgency = null; _research = null;
                    _photoWatcher?.Dispose(); _photoWatcher = null;
                    _heartbeat = null; _blackboard = null; _lightOcr = null; _semanticCache = null;
                    _hyperAutomation = null; _processWatchdog = null;
                }
                catch (Exception suppressedEx634) { KokoSystemLog.Write("SERVICECONTAINER-CATCH", "Disposing failed near source line 634: " + suppressedEx634); }
                finally
                {
                    provider = _serviceProvider;
                    _serviceProvider = null;
                    _vault = null;
                }
            }
            provider?.Dispose();
        }
    }
}
