# Current State

Updated: 2026-05-22 16:45 Europe/Kiev  
Latest code commit: `15349a9 Route local PC commands before chat`

## Project Shape

KokonoeAssistant is a Windows WPF app with desktop chat, Telegram bot/MTProto integration, Obsidian vault tooling, screen capture + vision, proactive state, memory, scheduler, and tests under `Tests/KokonoeAssistant.Tests`.

Main entry points:

- `MainWindow.xaml.cs` - desktop chat, Telegram flows, UI handlers, direct pre-LLM routes.
- `Services/LlmService.cs` - model calls, tool definitions, output repair, capability prompt.
- `Services/KokoBrainEngine.cs` - proactive state, screen awareness loop, memory/state logic.
- `Services/ObsidianMcpService.cs` - Vault operations and maintenance tools.
- `Services/PcControlService.cs` - actual OS/PC actions.
- `Services/PcIntentRouter.cs` - natural-language PC command router before chat/LLM.

## Latest Behavior Baseline

Natural screen requests are routed through local screenshot + vision:

- examples: `що в мене на екрані`, `проскануй екран`, `сфоткай екран`;
- screen route must not fall back to "I cannot see your screen" text;
- screenshot capture is verified by test.

Natural OS/PC commands are routed before LLM:

- examples: `відкрий chrome`, `що жере RAM`, `постав гучність на 37`, `ps: Write-Output koko-ok`;
- reversible commands execute through `PcControlService`;
- shutdown/restart require explicit confirmation through `/pc`;
- destructive shell fragments are blocked by `PcCommandSafety`.

Telegram:

- `/pc` menu supports screenshot, sysinfo, processes, volume, open app/path, kill process, lock/sleep/monitor, shutdown/restart confirmation.
- `pc_cmd` now accepts PowerShell text through awaiting state, with safety blocking.
- natural Telegram/MTProto PC requests go through the same router before normal LLM chat.

Output quality:

- post-reply guard rejects dotted garbage and mojibake-like visible output.
- broken decorative comments were converted to ASCII separators.
- test fixture mojibake `Р‘Р°Р№ Р±Р°Р№` was replaced with normal `Бай бай`.

## Verified

Last full checks:

- `dotnet build KokonoeAssistant.csproj -p:UseSharedCompilation=false` - passed.
- `dotnet run --project Tests\KokonoeAssistant.Tests\KokonoeAssistant.Tests.csproj -p:UseSharedCompilation=false` - passed, `110 tests`.
- New app started from `bin\Debug\net8.0-windows\KokonoeAssistant.exe`, PID `22112`.

Known recurring warning:

- Some tests use obsolete `KokoMemoryEngine` methods. Current build of the main app is clean; test run may show warnings unless those tests are refactored.

## Active Risks

- Do not run WPF build and test project in parallel. They can lock `obj\Debug\net8.0-windows\KokonoeAssistant.dll` or the exe.
- If the app is running, `dotnet build` may fail while copying `KokonoeAssistant.exe`; stop the running process first.
- Do not route screen requests through `PcIntentRouter`; they must stay on screenshot + vision route.
- Do not allow arbitrary destructive shell commands from Telegram or desktop chat.

## Next Update Checklist

- Read this file first.
- Make the code change.
- Run build and relevant tests.
- Add an update note in `ProjectMemory/UPDATES/`.
- Refresh this file.
- Commit.
