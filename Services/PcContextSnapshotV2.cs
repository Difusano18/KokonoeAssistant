using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KokonoeAssistant.Services
{
    public enum PcObservationMode
    {
        Light = 0,
        Normal = 1,
        Deep = 2
    }

    public sealed class PcContextSnapshotV2
    {
        public DateTime TakenAt { get; set; } = DateTime.Now;
        public PcObservationMode ObservationMode { get; set; } = PcObservationMode.Light;
        public PcIdentityContext Identity { get; set; } = new();
        public PcPresenceContext Presence { get; set; } = new();
        public ForegroundWindowInfo Foreground { get; set; } = new();
        public PcWorkspaceContext Workspace { get; set; } = new();
        public PcResourceContext Resources { get; set; } = new();
        public PcPrivacyContext Privacy { get; set; } = new();
        public List<WindowSummary> VisibleWindows { get; set; } = new();
        public List<WindowSummary> BrowserWindows { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PC context v2: {TakenAt:yyyy-MM-dd HH:mm:ss} mode={ObservationMode}");
            sb.AppendLine($"Identity: {Identity.MachineName} | {Identity.OsVersion} | uptime {Identity.Uptime.TotalHours:F1}h");
            sb.AppendLine($"Presence: idle {Presence.IdleTime.TotalMinutes:F1}m | recent_input={Presence.HasRecentInput}");
            sb.AppendLine("Foreground: " + Foreground);
            sb.AppendLine($"Workspace: {Workspace.Mode} confidence={Workspace.Confidence:F2} reason={Workspace.Reason}");
            sb.AppendLine($"Resources: CPU {Resources.CpuPercent:F1}% | RAM {Resources.RamUsedGb:F1}/{Resources.RamTotalGb:F1} GB");
            sb.AppendLine($"Privacy: sensitive={Privacy.IsSensitive} screen_allowed={Privacy.ScreenObservationAllowed} reason={Privacy.SensitivityReason}");

            if (BrowserWindows.Count > 0)
                sb.AppendLine("Browser windows: " + string.Join(" | ", BrowserWindows.Take(6).Select(w => w.ToString())));
            if (VisibleWindows.Count > 0)
                sb.AppendLine("Visible windows: " + string.Join(" | ", VisibleWindows.Take(8).Select(w => w.ToString())));
            if (Errors.Count > 0)
                sb.AppendLine("Errors: " + string.Join("; ", Errors));

            return sb.ToString().Trim();
        }
    }

    public sealed class PcIdentityContext
    {
        public string MachineName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string OsVersion { get; set; } = "";
        public TimeSpan Uptime { get; set; }
    }

    public sealed class PcPresenceContext
    {
        public TimeSpan IdleTime { get; set; }
        public bool HasRecentInput => IdleTime < TimeSpan.FromMinutes(2);
        public bool LooksAway => IdleTime >= TimeSpan.FromMinutes(15);
    }

    public sealed class PcWorkspaceContext
    {
        public string Mode { get; set; } = "unknown";
        public string Reason { get; set; } = "";
        public double Confidence { get; set; }
    }

    public sealed class PcPrivacyContext
    {
        public bool IsSensitive { get; set; }
        public string SensitivityReason { get; set; } = "";
        public bool ScreenObservationAllowed { get; set; } = true;
        public bool Redacted { get; set; } = true;
        public List<string> Redactions { get; set; } = new();
    }

    public sealed class PcResourceContext
    {
        public double CpuPercent { get; set; }
        public double RamTotalGb { get; set; }
        public double RamUsedGb { get; set; }
        public int VolumePercent { get; set; }
        public List<DriveInfo_> Drives { get; set; } = new();
        public List<ProcessResourceInfo> TopProcesses { get; set; } = new();
    }

    public static class PcContextRedactor
    {
        private static readonly Regex EmailRegex = new(
            @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SecretAssignmentRegex = new(
            @"(?i)\b(api[_\-\s]?key|token|secret|password|passwd|pwd|bearer)\b\s*[:=]\s*[""']?[^""'\s]{6,}",
            RegexOptions.Compiled);

        private static readonly Regex LongTokenRegex = new(
            @"\b[A-Za-z0-9_\-]{28,}\b",
            RegexOptions.Compiled);

        private static readonly Regex UserPathRegex = new(
            @"(?i)\b([A-Z]:\\Users\\)[^\\/:*?""<>|\r\n]+",
            RegexOptions.Compiled);

        private static readonly string[] SensitiveTitleMarkers =
        {
            "password", "passwd", "пароль", "bitwarden", "1password", "lastpass", "keepass",
            "login", "sign in", "signin", "log in", "вхід", "авторизац", "2fa", "otp",
            "bank", "банкінг", "monobank", "privat24", "paypal", "payment", "checkout",
            "api key", "token", "secret", "seed phrase", "recovery phrase", "private key",
            "private chat", "direct message", "dm "
        };

        public static string RedactText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var result = text.Trim();
            result = SecretAssignmentRegex.Replace(result, "$1=[redacted]");
            result = EmailRegex.Replace(result, "[email]");
            result = UserPathRegex.Replace(result, "$1[user]");
            result = LongTokenRegex.Replace(result, "[id]");
            return result;
        }

        public static ForegroundWindowInfo RedactForeground(ForegroundWindowInfo? source)
        {
            source ??= new ForegroundWindowInfo();
            return new ForegroundWindowInfo
            {
                Handle = source.Handle,
                Title = RedactText(source.Title),
                ClassName = RedactText(source.ClassName),
                ProcessName = RedactText(source.ProcessName),
                ProcessId = source.ProcessId,
                Error = RedactText(source.Error)
            };
        }

        public static WindowSummary RedactWindow(WindowSummary source)
            => new()
            {
                Handle = source.Handle,
                ProcessId = source.ProcessId,
                ProcessName = RedactText(source.ProcessName),
                Title = RedactText(source.Title),
                ClassName = RedactText(source.ClassName)
            };

        public static PcPrivacyContext AssessPrivacy(ForegroundWindowInfo? foreground, IEnumerable<WindowSummary>? windows = null)
        {
            var privacy = new PcPrivacyContext();
            var candidates = new List<string>();

            if (foreground != null)
                candidates.Add($"{foreground.ProcessName} {foreground.Title} {foreground.ClassName}");

            if (windows != null)
                candidates.AddRange(windows.Take(20).Select(w => $"{w.ProcessName} {w.Title} {w.ClassName}"));

            foreach (var candidate in candidates)
            {
                var lower = (candidate ?? "").ToLowerInvariant();
                var marker = SensitiveTitleMarkers.FirstOrDefault(m => lower.Contains(m, StringComparison.OrdinalIgnoreCase));
                if (marker == null)
                    continue;

                privacy.IsSensitive = true;
                privacy.ScreenObservationAllowed = false;
                privacy.SensitivityReason = $"matched sensitive marker '{marker.Trim()}'";
                privacy.Redactions.Add("sensitive-window");
                break;
            }

            return privacy;
        }

        public static PcWorkspaceContext ClassifyWorkspace(ForegroundWindowInfo foreground, IEnumerable<WindowSummary> windows)
        {
            var text = string.Join(" ", new[]
            {
                foreground.ProcessName,
                foreground.Title,
                foreground.ClassName,
                string.Join(" ", windows.Take(12).Select(w => $"{w.ProcessName} {w.Title}"))
            }).ToLowerInvariant();

            if (ContainsAny(text, "devenv", "visual studio", "code", "rider", "program.cs", ".cs", "terminal", "powershell", "git"))
                return new PcWorkspaceContext { Mode = "coding", Confidence = 0.82, Reason = "developer tools or code files are visible" };
            if (ContainsAny(text, "obsidian", "vault", ".md"))
                return new PcWorkspaceContext { Mode = "vault", Confidence = 0.78, Reason = "Obsidian or markdown workspace is visible" };
            if (ContainsAny(text, "spotify", "youtube", "netflix", "media player", "vlc"))
                return new PcWorkspaceContext { Mode = "media", Confidence = 0.76, Reason = "media app or media site is visible" };
            if (ContainsAny(text, "telegram", "discord", "slack"))
                return new PcWorkspaceContext { Mode = "chat", Confidence = 0.74, Reason = "chat app is visible" };
            if (ContainsAny(text, "steam", "game", "unity", "unreal", "valorant", "minecraft", "genshin"))
                return new PcWorkspaceContext { Mode = "gaming", Confidence = 0.72, Reason = "game or launcher is visible" };
            if (ContainsAny(text, "chrome", "msedge", "firefox", "brave", "browser"))
                return new PcWorkspaceContext { Mode = "browser", Confidence = 0.68, Reason = "browser window is visible" };

            return new PcWorkspaceContext { Mode = "unknown", Confidence = 0.2, Reason = "no strong workspace signal" };
        }

        private static bool ContainsAny(string text, params string[] markers)
            => markers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
    }
}
