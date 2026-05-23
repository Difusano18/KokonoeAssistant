using System;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoResourceGuardianDecision
    {
        public bool ShouldPrompt { get; init; }
        public string WorkMode { get; init; } = "Unknown";
        public string Message { get; init; } = "";
        public string ProcessName { get; init; } = "";
        public double CpuPercent { get; init; }
        public double MemoryMb { get; init; }
        public string Reason { get; init; } = "";
    }

    public static class KokoResourceGuardianService
    {
        private static readonly string[] BrowserProcesses =
        {
            "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "browser"
        };

        public static KokoResourceGuardianDecision Evaluate(
            SystemInfo info,
            string? currentMode,
            DateTime now,
            DateTime lastPromptAt)
        {
            var mode = NormalizeWorkMode(currentMode);
            var heavy = info.TopProcesses
                .Where(p => p.MemoryMb >= 700 || p.CpuPercent >= 18)
                .OrderByDescending(p => p.CpuPercent)
                .ThenByDescending(p => p.MemoryMb)
                .FirstOrDefault();

            if (heavy == null)
            {
                return new KokoResourceGuardianDecision
                {
                    WorkMode = mode,
                    Reason = "no heavy process"
                };
            }

            var browser = BrowserProcesses.Any(b =>
                heavy.ProcessName.Contains(b, StringComparison.OrdinalIgnoreCase));
            var cooldownReady = lastPromptAt <= DateTime.MinValue ||
                                now - lastPromptAt >= TimeSpan.FromMinutes(20);
            var gamingPressure = mode == "Gaming" && browser && cooldownReady;

            if (!gamingPressure)
            {
                return new KokoResourceGuardianDecision
                {
                    WorkMode = mode,
                    ProcessName = heavy.ProcessName,
                    CpuPercent = heavy.CpuPercent,
                    MemoryMb = heavy.MemoryMb,
                    Reason = cooldownReady ? "pressure not relevant to current mode" : "cooldown"
                };
            }

            return new KokoResourceGuardianDecision
            {
                ShouldPrompt = true,
                WorkMode = mode,
                ProcessName = heavy.ProcessName,
                CpuPercent = heavy.CpuPercent,
                MemoryMb = heavy.MemoryMb,
                Reason = "browser resource pressure during game mode",
                Message =
                    $"Я бачу, що {heavy.ProcessName} вантажить систему під час гри: {heavy.MemoryMb:F0} MB, CPU {heavy.CpuPercent:F1}%. Нічого сама не закриваю. Хочеш, я призупиню або закрию його після твого прямого підтвердження?"
            };
        }

        public static string NormalizeWorkMode(string? screenModeOrWindow)
        {
            var value = (screenModeOrWindow ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value)) return "Unknown";
            if (value.Contains("game") || value.Contains("steam") || value.Contains("unity") || value.Contains("unreal"))
                return "Gaming";
            if (value.Contains("code") || value.Contains("visual studio") || value.Contains("rider") || value.Contains("terminal"))
                return "Coding";
            if (value.Contains("obsidian") || value.Contains("vault") || value.Contains("note"))
                return "Vault";
            if (value.Contains("browser") || value.Contains("chrome") || value.Contains("edge") || value.Contains("firefox"))
                return "Browsing";
            if (value.Contains("telegram") || value.Contains("discord"))
                return "Messaging";
            return char.ToUpperInvariant(value[0]) + value[1..];
        }
    }
}
