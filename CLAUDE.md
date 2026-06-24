# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KokonoeAssistant is a WPF desktop application (.NET 8.0-windows) featuring Kokonoe Mercury from BlazBlue as an AI companion. It integrates with local LLMs (via LM Studio/ollama), Obsidian vaults (via MCP), Telegram bots, and includes health tracking, habit management, and a sophisticated emotion engine.

## Build Commands

```bash
# Build the project
dotnet build

# Build in release mode
dotnet build -c Release

# Run the application
dotnet run

# Clean build artifacts
dotnet clean
```

The project uses WPF with Windows Forms interop (`UseWPF` + `UseWindowsForms`). Dependencies are managed via NuGet.

`dotnet build` runs the frontend build (`npm run build` in `frontend/`) as a pre-build step and embeds `frontend/dist/` into the WebView2 shell. Run `cd frontend && npm run build` (or just `dotnet build`) after every change to `frontend/`; check both for `0 Error(s)` before moving on.

## Architecture Overview

### Mandatory Tool Gateway

All filesystem and OS side effects must go through `ServiceContainer.ToolGateway` / `IKokoToolGateway`.
Do not call `KokoFileSystemToolService.ExecuteAsync`, instantiate `PcActionExecutor`, or add another executor from production UI/LLM/agent code.
Every write/create/move/delete must return a verified `KokoToolResult`; never claim success from provider output alone.
Empty `catch { }` blocks are forbidden in new code. Log failures with `KokoSystemLog` and propagate a structured failure result where possible.
See `Architecture/TOOL_GATEWAY.md`.

### Service Container Pattern

`ServiceContainer.cs` implements a static service locator pattern for dependency injection. All services are lazily initialized and thread-safe using `lock (_lock)`.

Key services:
- `ServiceContainer.BrainEngine` - Central orchestrator (KokoBrainEngine)
- `ServiceContainer.EmotionEngine` - 12-state emotion machine with connection scoring
- `ServiceContainer.KokoPatterns` - Pattern detection and analysis
- `ServiceContainer.LlmService` - LLM communication with streaming
- `ServiceContainer.ObsidianMcp` - Obsidian vault integration via MCP
- `ServiceContainer.HealthService` - Health metrics tracking
- `ServiceContainer.Calendar` - Event management with annual reminders

**Important**: When adding new services, follow the lazy initialization pattern with double-checked locking. BrainEngine has complex wiring - it initializes other engines and injects them into LlmService.

### Kokonoe Brain Architecture

`KokoBrainEngine` (in `Services/KokoBrainEngine.cs`) is the central orchestrator managing:

1. **Emotion Engine** (`KokoEmotionEngine`) - 12 emotional states, connection scoring (0-1), bond levels (Stranger→Intimate), emotional inertia
2. **Memory Engine** (`KokoMemoryEngine`) - Episodic memory with RAG search
3. **Pattern Engine** (`KokoPatternEngine`) - Detects time patterns, mood patterns, behavioral anomalies
4. **Scheduler** (`KokoSchedulerEngine`) - Time-based task execution

BrainEngine uses two timers:
- `_thinkTimer` - every 90 minutes for internal monologue and background thinking
- `_spontaneousTimer` - every 45 minutes for proactive messaging checks

**Critical**: BrainEngine wires its sub-engines into LlmService (lines 178-182 in KokoBrainEngine.cs). If emotions/patterns aren't showing in LLM responses, check this wiring.

### Thread Safety

Most services use `private readonly object _lock = new()` for thread safety. The pattern is:

```csharp
public void SomeMethod()
{
    lock (_lock)
    {
        // Access shared state
    }
}
```

This applies to: `EnhancedMemory`, `KokoEmotionEngine`, `KokoPatternEngine`, `HealthService`, `ChatRepository`, `ServiceContainer`.

### LLM Integration

`LlmService` communicates with local LLM servers (LM Studio/ollama) via HTTP streaming. Key features:
- History management (max 30 entries, truncates 10 at a time)
- Tool support via `FunctionCall` pattern
- Personality hints from EmotionEngine injected into prompts
- Stream-based response handling

The system prompt embeds Kokonoe's character: sarcastic, intelligent, protective of her creator, Ukrainian language only.

### Obsidian MCP Integration

`ObsidianMcpService` communicates with Obsidian via Model Context Protocol. It exposes tools like:
- `read_note`, `write_note`, `search_notes`
- `rebuild_graph`, `get_graph_stats`
- `append_to_note`, `delete_note`

Kokonoe uses these tools autonomously to maintain her "brain" - the Obsidian vault is her persistent memory.

### UI Thread Considerations

WPF requires UI updates on the dispatcher thread. When updating UI from async callbacks:

```csharp
await Dispatcher.InvokeAsync(() =>
{
    // UI updates here
});
```

For long operations (file I/O, LLM calls), use `Task.Run()` to avoid freezing the UI:

```csharp
var context = await Task.Run(() => BuildContext(userInput));
```

### WebView Frontend (TypeScript + Vite)

The chat/agent/settings UI is `frontend/` (vanilla TypeScript + Vite), rendered inside a WebView2 control hosted in the same process as the WPF app — not a separate server. `Services/KokoWebBridgeService` is the JSON-over-`postMessage` transport; each `KokoWeb*BridgeService` (Chat, Agent, Settings, Vault, Telegram, Memory, Runtime, System, Persona) registers request handlers and publishes events on it. `frontend/src/bridge.ts` is the client side (`window.koko.call(...)` / `window.koko.on(...)`).

`OnWebMessageReceived` (in `KokoWebBridgeService`) fires on the UI thread, which has a captured `DispatcherSynchronizationContext`. That means **`await Task.Yield()` inside a bridge handler does not move work to the ThreadPool** — it just re-posts the continuation onto the same dispatcher queue, letting one pending Windows message run first. It's still required as the first line of every new handler (matches the existing convention across all `KokoWeb*BridgeService` files), but if a handler does real work (vault scans, embedding lookups, HTTP calls), that needs an actual `Task.Run(...).ConfigureAwait(false)` hop, same pattern as `KokoWebChatBridgeService.HandleSendAsync`'s context builder.

Critical DOM IDs (don't rename without updating the TS that looks them up):
`#chat-form` `#chat-input` `#chat-send` `#chat-scroll` `#messages` `#chat-status` `#bridge-status`
`#new-chat-btn` `#onboarding-banner` `#settings-backdrop` `#settings-drawer` `#settings-open` `#settings-close`
`#panel-tasks` `#panel-memory` `#panel-telemetry` `#agent-tasks` `#agent-activity`
`.runtime-dot` `.message.user` `.message.assistant` `.message-body` `.agent-status` `.agent-step`

Default LLM provider is `ollama-cloud` (see `AppSettings.LlmProvider`); `lmstudio` needs a local server running and was the cause of past "no reply" failures, so don't change the default without a reason. The `OllamaApiKey`/`OllamaKeys` pool and `OllamaUrl` belong to the `ollama-cloud` provider specifically — there's no `OllamaCloudApiKey`/`OllamaCloudBaseUrl` property, despite the naming you'd expect.

## Key File Locations

- `MainWindow.xaml.cs` - Main chat interface and UI logic
- `Windows/ToolsWindow.xaml.cs` - JARVIS-style neurological dashboard
- `ServiceContainer.cs` - Service registration and lifecycle
- `Services/KokoBrainEngine.cs` - Core AI orchestration (largest file, ~137KB)
- `Services/LlmService.cs` - LLM communication (~56KB)
- `Services/KokoEmotionEngine.cs` - 12-state emotion machine
- `EnhancedMemory.cs` - Structured fact storage with Knowledge Graph integration
- `AppSettings.cs` - Configuration persistence

## Data Storage

All persistent data is stored in `kokonoe-data/` within the vault path:
- `kokonoe-emotions.json` - Emotion state
- `koko-patterns.json` - Pattern data
- `kokonoe-facts.json` - Enhanced memory facts
- `kokonoe-health.db` - SQLite health tracking
- `calendar-events.json` - Calendar events
- `chats/` - Chat logs

+## Service Reachability Audit (2026-06-20)

Scope: all 76 `Services/Koko*.cs` files, excluding `Tests/`, `bin/`, `obj/`, and each declaration file itself.
Result: every type has at least one external production reference. No file is safe to delete in this phase.
Types without an explicit constructor call are static utilities or target-typed DTOs; they are not dead code.

| Type | Runtime evidence | Production callers |
|---|---:|---|
| `KokoActionDirectiveRouter` | static; refs=2 | MainWindow.Agent.cs, Services/KokoSystemOverlordService.cs |
| `KokoActiveAgencyService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoAgentCompletionPolicy` | static; refs=2 | Services/KokoAgentRuntimeService.cs, Services/KokoAgentTaskService.cs |
| `KokoAgentRuntimeService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoAgentTaskService` | constructed=1; refs=3 | ServiceContainer.cs, Services/KokoDynamicAgentFactoryService.cs, Services/LlmService.cs |
| `KokoAsyncPersonalityEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoAutonomousProfileCuratorService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoAutonomyDecisionEngine` | constructed=2; refs=2 | Services/KokoBrainEngine.cs, Services/KokoScenarioSimulationService.cs |
| `KokoBrainEngine` | constructed=1; refs=6 | ServiceContainer.cs, Services/KokoActiveAgencyService.cs, Services/KokoAgentTaskService.cs (+3) |
| `KokoCapabilityManifestService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoCognitionEngine` | constructed=1; refs=2 | Services/KokoBrainEngine.cs, Services/KokoResponsePlannerEngine.cs |
| `KokoCollectiveMindService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoContinuityEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoConversationBoundary` | static; refs=3 | Services/KokoBrainEngine.cs, Services/KokoPostReplyGuard.cs, Services/KokoProactiveContextService.cs |
| `KokoConversationStagnationGuard` | static; refs=1 | Services/KokoBrainEngine.cs |
| `KokoConversationTimelineEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoDynamicAgentFactoryService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoEmbeddingService` | constructed=1; refs=3 | ServiceContainer.cs, Services/KokoBrainEngine.cs, Services/KokoMemoryEngine.cs |
| `KokoEmotionalMemoryService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoEmotionEngine` | constructed=2; refs=20 | MainWindow.ChatUi.cs, MainWindow.Context.cs, MainWindow.DashboardDev.cs (+17) |
| `KokoFileSystemToolService` | constructed=3; refs=3 | ServiceContainer.cs, ToolExecutor.cs, Services/KokoActiveAgencyService.cs |
| `KokoHeartEngine` | constructed=1; refs=4 | MainWindow.xaml.cs, ServiceContainer.cs, Services/KokoBrainEngine.cs (+1) |
| `KokoHyperAutomationService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoInitiativeEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoInternalBlackboardService` | constructed=1; refs=8 | ServiceContainer.cs, Services/KokoActiveAgencyService.cs, Services/KokoAgentRuntimeService.cs (+5) |
| `KokoInternalDayEngine` | constructed=2; refs=2 | Services/KokoBrainEngine.cs, Services/KokoScenarioSimulationService.cs |
| `KokoLightOcrService` | constructed=1; refs=2 | ServiceContainer.cs, Services/KokoHyperAutomationService.cs |
| `KokoLivingConversationEngine` | constructed=1; refs=2 | Services/KokoBrainEngine.cs, Services/KokoResponseStyleEngine.cs |
| `KokoMemoryEngine` | constructed=2; refs=8 | MainWindow.Memory.cs, ServiceContainer.cs, Services/KokoBrainEngine.cs (+5) |
| `KokoMemoryWritePolicyEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoNaturalSynthesisPolicy` | static; refs=2 | Services/KokoBrainEngine.cs, Services/KokoPostReplyGuard.cs |
| `KokoNeuralGovernorService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoObservationService` | constructed=1; refs=2 | MainWindow.Agent.cs, Services/KokoAgentTaskService.cs |
| `KokoObsidianExplorationService` | constructed=2; refs=4 | MainWindow.Chat.cs, MainWindow.Context.cs, Services/KokoBrainEngine.cs (+1) |
| `KokoPatternEngine` | constructed=2; refs=7 | ServiceContainer.cs, Services/KokoAutonomyDecisionEngine.cs, Services/KokoBrainEngine.cs (+4) |
| `KokoPersonaEngine` | constructed=1; refs=3 | Services/KokoBrainEngine.cs, Services/KokoPostReplyGuard.cs, Services/KokoSubconsciousMonologueEngine.cs |
| `KokoPersonaGuardDirective` | static; refs=4 | Services/KokoBrainEngine.cs, Services/KokoPostReplyGuard.cs, Services/KokoStartupGreetingService.cs (+1) |
| `KokoPhotoFileWatcherService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoPostReplyGuard` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoPredictorService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoPresenceContinuityEngine` | constructed=2; refs=2 | Services/KokoBrainEngine.cs, Services/KokoScenarioSimulationService.cs |
| `KokoProactiveContextService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoProfileUpdateService` | constructed=1; refs=7 | MainWindow.Context.cs, ServiceContainer.cs, Services/KokoAutonomousProfileCuratorService.cs (+4) |
| `KokoRelationshipEngine` | constructed=1; refs=4 | Services/KokoBrainEngine.cs, Services/KokoInitiativeEngine.cs, Services/KokoSomaticSelfRegulationEngine.cs (+1) |
| `KokoResearchService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoResourceGuardianService` | static; refs=1 | Services/KokoBrainEngine.cs |
| `KokoResponsePlannerEngine` | constructed=1; refs=6 | MainWindow.Agent.cs, Services/KokoAgentRuntimeService.cs, Services/KokoBrainEngine.cs (+3) |
| `KokoResponseStyleEngine` | static; refs=1 | Services/KokoBrainEngine.cs |
| `KokoRuntimeStateService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoSandboxExecutor` | constructed=3; refs=3 | Services/KokoAgentRuntimeService.cs, Services/KokoAgentTaskService.cs, Services/LlmService.cs |
| `KokoScenarioSimulationService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoSchedulerEngine` | constructed=1; refs=3 | MainWindow.Chat.cs, Services/KokoBrainEngine.cs, Services/LlmService.cs |
| `KokoScreenAwarenessService` | constructed=1; refs=4 | Services/KokoBrainEngine.cs, Services/KokoPresenceContinuityEngine.cs, Services/KokoProactiveContextService.cs (+1) |
| `KokoScreenIntent` | static; refs=7 | MainWindow.Chat.cs, MainWindow.TelegramUser.cs, Services/KokoAgentTaskService.cs (+4) |
| `KokoSelfReviewEngine` | constructed=2; refs=2 | Services/KokoBrainEngine.cs, Services/KokoScenarioSimulationService.cs |
| `KokoSemanticCacheService` | constructed=1; refs=2 | ServiceContainer.cs, Services/LlmService.cs |
| `KokoSemanticVisionEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoServiceHeartbeatService` | constructed=1; refs=6 | ServiceContainer.cs, Services/KokoActiveAgencyService.cs, Services/KokoHyperAutomationService.cs (+3) |
| `KokoSocialEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoSomaticEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoSomaticSelfRegulationEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoStartupGreetingService` | constructed=1; refs=1 | MainWindow.xaml.cs |
| `KokoStateFreshnessService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoStateInspectorService` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoSubconsciousMonologueEngine` | constructed=1; refs=1 | Services/KokoBrainEngine.cs |
| `KokoSystemLog` | static; refs=77 | KnowledgeGraph.cs, KokonoeDataManager.cs, App.xaml.cs (+74) |
| `KokoSystemOverlordService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoTelemetrySnapshot` | target-typed/referenced; refs=1 | Services/KokoBrainEngine.cs |
| `KokoTemperamentEngine` | constructed=1; refs=2 | Services/KokoBrainEngine.cs, Services/KokoResponseStyleEngine.cs |
| `KokoTemporalPresenceAwarenessEngine` | constructed=2; refs=2 | Services/KokoBrainEngine.cs, Services/KokoStartupGreetingService.cs |
| `KokoToolGateway` | constructed=3; refs=3 | ServiceContainer.cs, ToolExecutor.cs, Services/KokoActiveAgencyService.cs |
| `KokoVaultSyncPolicy` | static; refs=1 | Services/KokoBrainEngine.cs |
| `KokoWarmRestartWatchdogService` | constructed=1; refs=1 | ServiceContainer.cs |
| `KokoWearableBridgeService` | constructed=1; refs=5 | MainWindow.Telemetry.cs, ServiceContainer.cs, Services/KokoBrainEngine.cs (+2) |
| `KokoWearableTelemetryService` | constructed=1; refs=4 | ServiceContainer.cs, Services/KokoActiveAgencyService.cs, Services/KokoHeartEngine.cs (+1) |
| `KokoWearableTrust` | static; refs=4 | MainWindow.Telemetry.cs, ServiceContainer.cs, Services/KokoBrainEngine.cs (+1) |

## Adding New Features

When adding new functionality:

1. **Services**: Create in `Services/` folder, use `lock (_lock)` for thread safety
2. **UI**: Add XAML in appropriate folder, bind to existing service container
3. **Registration**: Add to `ServiceContainer.cs` following the lazy init pattern
4. **Brain Integration**: If AI-related, wire into `KokoBrainEngine` constructor

## Common Patterns

**Service with persistence**:
```csharp
private readonly string _path;
private readonly object _lock = new();
private DataType _data;

public MyService(string dataDir)
{
    _path = Path.Combine(dataDir, "my-data.json");
    _data = Load();
}

private DataType Load() { /* JSON deserialize with try/catch */ }
private void Save() { /* JSON serialize with try/catch */ }
```

**UI Canvas drawing**:
Canvas elements require manual drawing in code-behind. See `ToolsWindow.xaml.cs` for heatmap/timeline examples using `Polyline`, `Rectangle`, `Ellipse`.
