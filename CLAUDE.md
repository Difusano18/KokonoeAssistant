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

## Architecture Overview

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
