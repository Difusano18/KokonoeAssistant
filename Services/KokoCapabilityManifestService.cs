using System;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoCapabilityManifestService
    {
        public string BuildPromptBlock()
        {
            var lines = new[]
            {
                "RUNTIME CAPABILITY MANIFEST",
                "- Identity: Kokonoe operator runtime; answer from actual host capabilities, not helpless stock phrases.",
                "- Chat: local desktop chat plus Telegram routes when configured.",
                "- Memory: Obsidian/Vault read/search/write/append/daily/backlinks/graph/maintenance routes are available through host tools/context.",
                "- Screen/Vision: local screenshot, foreground window, browser/window titles, visible windows, CPU/RAM/top processes, and image/vision routes are available when host context provides them.",
                "- PC control: safe local OS actions, app open, volume, process/system context, screenshot, shell-chain policy and confirmations for risky actions.",
                "- Agent runtime: autonomous task board, background tasks, Obsidian backlog import, system-control execution through policy.",
                "- CodeAct: restricted Python calculations and data transformations run through IKokoToolGateway; host filesystem, process, network, unsafe imports, and reflection are blocked, while code/output artifacts are retained for recovery.",
                "- Wearable telemetry: Galaxy Watch bridge endpoint exists; pulse/location/body state must come from wearable samples when fresh, otherwise say data is stale/offline.",
                "- Audio/voice: recording and Whisper transcription routes exist when configured.",
                "- Calendar/goals/habits: local reminder, event, goal, and habit services exist.",
                "- Rule: never claim a capability is absent until the relevant route/context was checked or the host explicitly reports failure.",
                "- Rule: if data source is stale, say stale and name the bridge/source needed; do not invent metaphors, biology jokes, or user-blame."
            };

            return string.Join(Environment.NewLine, lines);
        }

        public string BuildStatusLine()
        {
            var parts = new[]
            {
                Safe("vault", () => ServiceContainer.ObsidianMcp != null ? "on" : "off"),
                Safe("pc", () => ServiceContainer.PcControl != null ? "on" : "off"),
                Safe("brain", () => ServiceContainer.BrainEngine != null ? "on" : "off"),
                Safe("wearable_bridge", () => ServiceContainer.WearableBridge.IsRunning ? $"on:{ServiceContainer.WearableBridge.Port}" : $"off:{ServiceContainer.WearableBridge.LastError}"),
                Safe("wearable", () =>
                {
                    var state = ServiceContainer.WearableTelemetry.State;
                    return state.IsFresh(DateTime.UtcNow) ? $"fresh:{state.CurrentBpm:F0}bpm" : "stale";
                })
            };

            return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private static string Safe(string name, Func<string> read)
        {
            try { return $"{name}={read()}"; }
            catch (Exception ex) { return $"{name}=error:{ex.GetType().Name}"; }
        }
    }
}
