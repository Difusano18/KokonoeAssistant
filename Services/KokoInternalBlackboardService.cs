using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoInternalBlackboardService
    {
        private readonly object _lock = new();
        private readonly string _path;
        private readonly List<BlackboardEvent> _events = new();

        public KokoInternalBlackboardService(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "kokonoe-blackboard.jsonl");
        }

        public void Publish(string agent, string kind, string summary, double priority = 0.5, object? payload = null)
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
    }

    public sealed class BlackboardEvent
    {
        public string Id { get; set; } = "";
        public DateTime At { get; set; }
        public string Agent { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Summary { get; set; } = "";
        public double Priority { get; set; }
        public string PayloadJson { get; set; } = "";

        public BlackboardEvent Clone() => new()
        {
            Id = Id,
            At = At,
            Agent = Agent,
            Kind = Kind,
            Summary = Summary,
            Priority = Priority,
            PayloadJson = PayloadJson
        };
    }
}
