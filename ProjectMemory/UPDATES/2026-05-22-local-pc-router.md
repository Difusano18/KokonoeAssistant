# 2026-05-22 Local PC Router

Commit: `15349a9 Route local PC commands before chat`

## Goal

Kokonoe was answering some OS/PC requests as chat/roleplay because real PC actions existed only in scattered handlers and Telegram menu callbacks. The fix was to route clear local commands before the LLM.

## Changed

- `Services/PcIntentRouter.cs` - new natural-language router for safe PC actions.
- `Services/PcControlService.cs` - added aliases and shell safety integration.
- `MainWindow.xaml.cs` - desktop chat, Telegram bot, and MTProto now call the router before LLM chat.
- `Services/LlmService.cs` - capability prompt now states OS actions are host-routed.
- `Services/KokoResponsePlannerEngine.cs` - classifies OS control as tool/action work, not normal chat.
- `Tests/KokonoeAssistant.Tests/Program.cs` - added router, shell, and screenshot tests.
- `MainWindow.xaml.cs`, `Services/KokoBrainEngine.cs`, `Services/ObsidianMcpService.cs`, `Services/LlmService.cs` - cleaned corrupted decorative comments.

## Behavior

Supported deterministic PC commands now include:

- system info;
- process list;
- volume up/down/mute/set;
- open app/path;
- kill process;
- lock screen;
- sleep;
- monitor off;
- explicit `ps:` / `powershell:` command.

Shutdown/restart are recognized but require confirmation through `/pc`. Destructive shell fragments are blocked.

Screen requests are deliberately excluded from `PcIntentRouter`; they still go through screenshot + vision.

## Tests

- `dotnet build KokonoeAssistant.csproj -p:UseSharedCompilation=false` - passed.
- `dotnet run --project Tests\KokonoeAssistant.Tests\KokonoeAssistant.Tests.csproj -p:UseSharedCompilation=false` - passed, `110 tests`.
- Real PowerShell test: `Write-Output koko-ok`.
- Real screenshot capture test checks JPEG bytes.

## Risks / Next

- Avoid parallel build/test for this WPF project.
- Consider adding UI-level smoke tests later for Telegram callback states.
- Consider migrating obsolete memory API usage in tests.

