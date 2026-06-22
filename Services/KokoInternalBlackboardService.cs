using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoInternalBlackboardService : IKokoInternalBlackboardService
    {
        private readonly object _lock = new();
        private readonly string _path;
        private readonly List<BlackboardEvent> _events = new();

        public KokoInternalBlackboardService(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "kokonoe-blackboard.jsonl");
            LoadTail();
        }

        public void Publish(string agent, string kind, string summary, double priority = 0.5, object? payload = null, string status = "published")
        {
            if (string.IsNullOrWhiteSpace(summary))
                return;

            var ev = new BlackboardEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                At = DateTime.Now,
                Agent = string.IsNullOrWhiteSpace(agent) ? "unknown" : agent.Trim(),
                Kind = string.IsNullOrWhiteSpace(kind) ? "event" : kind.Trim(),
                Summary = summary.Trim(),
                Priority = Math.Clamp(priority, 0, 1),
                Status = string.IsNullOrWhiteSpace(status) ? "published" : status.Trim(),
                PayloadJson = payload == null ? "" : JsonConvert.SerializeObject(payload)
            };

            lock (_lock)
            {
                _events.Add(ev);
                if (_events.Count > 300)
                    _events.RemoveRange(0, _events.Count - 300);
                try
                {
                    File.AppendAllText(_path, JsonConvert.SerializeObject(ev) + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    KokoSystemLog.Write("BLACKBOARD", "append failed: " + ex.Message);
                }
            }

            KokoSystemLog.Write("BLACKBOARD", $"{ev.Agent}/{ev.Kind}: {ev.Summary}");
        }

        public IReadOnlyList<BlackboardEvent> Recent(int count = 30)
        {
            lock (_lock)
                return _events.TakeLast(Math.Clamp(count, 1, 300)).Select(e => e.Clone()).ToList();
        }

        public IReadOnlyList<BlackboardEvent> Recent(int count, string? agent = null, string? kind = null)
        {
            lock (_lock)
            {
                IEnumerable<BlackboardEvent> query = _events;
                if (!string.IsNullOrWhiteSpace(agent))
                    query = query.Where(e => e.Agent.Equals(agent.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(kind))
                    query = query.Where(e => e.Kind.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase));
                return query.TakeLast(Math.Clamp(count, 1, 300)).Select(e => e.Clone()).ToList();
            }
        }

        public string BuildPromptBlock(int count = 8)
        {
            var events = Recent(Math.Clamp(count, 1, 20))
                .Where(e => !string.IsNullOrWhiteSpace(e.Summary))
                .ToList();
            if (events.Count == 0)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== INTERNAL BLACKBOARD RECENT ===");
            foreach (var ev in events)
            {
                var summary = Trim(ev.Summary, 220);
                sb.AppendLine($"- {ev.At:HH:mm} {ev.Agent}/{ev.Kind} p={ev.Priority:F2} status={NullDash(ev.Status)} :: {summary}");
            }
            sb.AppendLine("Rule: use this only as private continuity. Never expose blackboard labels, agent names, ids, or status fields.");
            return sb.ToString().Trim();
        }

        private void LoadTail()
        {
            try
            {
                if (!File.Exists(_path))
                    return;

                var loaded = File.ReadLines(_path, Encoding.UTF8)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .TakeLast(120)
                    .Select(line =>
                    {
                        try { return JsonConvert.DeserializeObject<BlackboardEvent>(line); }
                        catch { return null; }
                    })
                    .Where(e => e != null)
                    .Select(e => e!)
                    .ToList();

                lock (_lock)
                {
                    _events.Clear();
                    _events.AddRange(loaded);
                    if (_events.Count > 300)
                        _events.RemoveRange(0, _events.Count - 300);
                }
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("BLACKBOARD", "load failed: " + ex.Message);
            }
        }

        private static string Trim(string value, int max)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (value.Length <= max) return value;
            return value[..Math.Max(0, max - 1)].TrimEnd() + "...";
        }

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    public sealed class BlackboardEvent
    {
        public string Id { get; set; } = "";
        public DateTime At { get; set; }
        public string Agent { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Summary { get; set; } = "";
        public double Priority { get; set; }
        public string Status { get; set; } = "published";
        public string PayloadJson { get; set; } = "";

        public BlackboardEvent Clone() => new()
        {
            Id = Id,
            At = At,
            Agent = Agent,
            Kind = Kind,
            Summary = Summary,
            Priority = Priority,
            Status = Status,
            PayloadJson = PayloadJson
        };
    }
}
