using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoServiceHeartbeatService
    {
        private readonly object _lock = new();
        private readonly string _jsonPath;
        private readonly string _mdPath;
        private readonly string _htmlPath;
        private readonly Dictionary<string, HeartbeatEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

        public KokoServiceHeartbeatService(string dataDir)
        {
            var logDir = Path.Combine(dataDir, "logs");
            Directory.CreateDirectory(logDir);
            _jsonPath = Path.Combine(logDir, "Kokonoe_Heartbeat.json");
            _mdPath = Path.Combine(logDir, "Kokonoe_Heartbeat.md");
            _htmlPath = Path.Combine(logDir, "Kokonoe_Heartbeat.html");
            Update("SYSTEM", "starting", "heartbeat service initialized");
        }

        public string MarkdownPath => _mdPath;
        public string HtmlPath => _htmlPath;

        public IReadOnlyList<HeartbeatEntry> Snapshot()
        {
            lock (_lock)
                return _entries.Values.OrderBy(e => e.Service).Select(e => e.Clone()).ToList();
        }

        public void Update(string service, string status, string detail = "")
        {
            if (string.IsNullOrWhiteSpace(service))
                service = "SYSTEM";
            if (string.IsNullOrWhiteSpace(status))
                status = "unknown";

            lock (_lock)
            {
                _entries[service.Trim()] = new HeartbeatEntry
                {
                    Service = service.Trim(),
                    Status = status.Trim(),
                    Detail = detail?.Trim() ?? "",
                    UpdatedAt = DateTime.Now
                };
                WriteFilesLocked();
            }
        }

        private void WriteFilesLocked()
        {
            try
            {
                var entries = _entries.Values.OrderBy(e => e.Service).ToList();
                File.WriteAllText(_jsonPath, JsonConvert.SerializeObject(entries, Formatting.Indented), Encoding.UTF8);
                File.WriteAllText(_mdPath, BuildMarkdown(entries), Encoding.UTF8);
                File.WriteAllText(_htmlPath, BuildHtml(entries), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("HEARTBEAT", "write failed: " + ex.Message);
            }
        }

        private static string BuildMarkdown(IReadOnlyList<HeartbeatEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Kokonoe Service Heartbeat");
            sb.AppendLine();
            sb.AppendLine($"Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("| Service | Status | Detail | Age |");
            sb.AppendLine("|---|---:|---|---:|");
            foreach (var e in entries)
            {
                var age = DateTime.Now - e.UpdatedAt;
                sb.AppendLine($"| {Esc(e.Service)} | {Esc(e.Status)} | {Esc(e.Detail)} | {age.TotalSeconds:0}s |");
            }
            return sb.ToString();
        }

        private static string BuildHtml(IReadOnlyList<HeartbeatEntry> entries)
        {
            var rows = string.Join(Environment.NewLine, entries.Select(e =>
            {
                var age = DateTime.Now - e.UpdatedAt;
                var cls = e.Status.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                          e.Status.Contains("fail", StringComparison.OrdinalIgnoreCase)
                    ? "bad"
                    : e.Status.Contains("scan", StringComparison.OrdinalIgnoreCase) ||
                      e.Status.Contains("think", StringComparison.OrdinalIgnoreCase)
                        ? "busy"
                        : "ok";
                return $"<tr class=\"{cls}\"><td>{Html(e.Service)}</td><td>{Html(e.Status)}</td><td>{Html(e.Detail)}</td><td>{age.TotalSeconds:0}s</td></tr>";
            }));

            return $$"""
<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <meta http-equiv="refresh" content="5">
  <title>Kokonoe Heartbeat</title>
  <style>
    body{margin:0;background:#050812;color:#dbe7ff;font-family:Consolas,monospace}
    main{padding:20px;max-width:1100px;margin:auto}
    h1{font-size:18px;color:#ff4d6d}
    table{width:100%;border-collapse:collapse;background:#101a2e}
    th,td{padding:10px;border-bottom:1px solid #263552;text-align:left}
    th{color:#57f7d4;font-size:11px;text-transform:uppercase}
    .ok td:nth-child(2){color:#58ffbf}.busy td:nth-child(2){color:#ffd66e}.bad td:nth-child(2){color:#ff775f}
  </style>
</head>
<body><main><h1>// KOKONOE SERVICE HEARTBEAT</h1><p>{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}</p>
<table><thead><tr><th>Service</th><th>Status</th><th>Detail</th><th>Age</th></tr></thead><tbody>
{{rows}}
</tbody></table></main></body></html>
""";
        }

        private static string Esc(string value) => (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value ?? "");
    }

    public sealed class HeartbeatEntry
    {
        public string Service { get; set; } = "";
        public string Status { get; set; } = "";
        public string Detail { get; set; } = "";
        public DateTime UpdatedAt { get; set; }

        public HeartbeatEntry Clone() => new()
        {
            Service = Service,
            Status = Status,
            Detail = Detail,
            UpdatedAt = UpdatedAt
        };
    }
}
