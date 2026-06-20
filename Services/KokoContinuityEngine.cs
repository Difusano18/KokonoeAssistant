using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoBelief
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public string Kind { get; set; } = "stable_fact";
        public string Claim { get; set; } = "";
        public double Confidence { get; set; } = 0.5;
        public string Status { get; set; } = "active"; // active / uncertain / stale / contradicted
        public string Source { get; set; } = "";
        public DateTime FirstSeen { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int EvidenceCount { get; set; } = 1;
        public List<string> Contradictions { get; set; } = new();
    }

    public sealed class KokoContinuitySnapshot
    {
        public List<KokoBelief> Beliefs { get; set; } = new();
        public List<string> AuditLog { get; set; } = new();
        public DateTime LastUpdatedAt { get; set; } = DateTime.MinValue;
    }

    public sealed class KokoContinuityEngine
    {
        private readonly string _path;
        private readonly object _lock = new();
        private KokoContinuitySnapshot _data;

        public KokoContinuityEngine(string dataDir)
        {
            _path = Path.Combine(dataDir, "koko-continuity-beliefs.json");
            _data = Load();
        }

        public IReadOnlyList<KokoBelief> Beliefs
        {
            get { lock (_lock) return _data.Beliefs.ToList(); }
        }

        public KokoBelief? ApplyMemoryDecision(KokoMemoryWriteDecision decision, DateTime now)
        {
            if (decision.Candidates.Count == 0) return null;
            var candidate = decision.Candidates
                .OrderByDescending(c => c.Confidence)
                .First();

            if (candidate.Kind == "temporary_state" || decision.Action == "daily_log")
            {
                AppendAudit(now, $"temporary ignored as belief: {Trim(candidate.Text, 80)}");
                return null;
            }

            if (decision.Action is not ("store_stable" or "review" or "task_queue"))
                return null;

            lock (_lock)
            {
                var normalized = Normalize(candidate.Text);
                if (string.IsNullOrWhiteSpace(normalized)) return null;

                var existing = _data.Beliefs.FirstOrDefault(b => Normalize(b.Claim) == normalized);
                if (existing != null)
                {
                    existing.LastSeen = now;
                    existing.EvidenceCount++;
                    existing.Confidence = Math.Min(0.98, existing.Confidence + 0.05);
                    if (existing.Status == "uncertain" && existing.Confidence >= 0.70)
                        existing.Status = "active";
                    AppendAuditLocked(now, $"reinforced {existing.Kind}: {Trim(existing.Claim, 80)}");
                    Save();
                    return existing;
                }

                var contradiction = FindPossibleContradiction(candidate);
                var belief = new KokoBelief
                {
                    Kind = candidate.Kind,
                    Claim = candidate.Text,
                    Confidence = Math.Clamp(candidate.Confidence, 0.1, 0.98),
                    Status = decision.Action == "review" || candidate.Confidence < 0.65 ? "uncertain" : "active",
                    Source = candidate.Target,
                    FirstSeen = now,
                    LastSeen = now
                };

                if (contradiction != null)
                {
                    belief.Status = "uncertain";
                    belief.Contradictions.Add(contradiction.Claim);
                    contradiction.Status = "contradicted";
                    contradiction.Contradictions.Add(belief.Claim);
                    AppendAuditLocked(now, $"possible contradiction: {Trim(belief.Claim, 60)} <> {Trim(contradiction.Claim, 60)}");
                }

                _data.Beliefs.Add(belief);
                PruneLocked(now);
                AppendAuditLocked(now, $"stored {belief.Kind}/{belief.Status}: {Trim(belief.Claim, 80)}");
                Save();
                return belief;
            }
        }

        public string BuildPromptBlock(int maxBeliefs = 8)
        {
            lock (_lock)
            {
                var active = _data.Beliefs
                    .Where(b => b.Status == "active")
                    .OrderByDescending(b => b.Confidence * Math.Max(1, b.EvidenceCount))
                    .Take(maxBeliefs)
                    .ToList();
                var uncertain = _data.Beliefs
                    .Where(b => b.Status == "uncertain")
                    .OrderByDescending(b => b.LastSeen)
                    .Take(4)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("CONTINUITY / BELIEF MODEL");
                if (active.Count == 0) sb.AppendLine("active_beliefs: none");
                else
                {
                    sb.AppendLine("active_beliefs:");
                    foreach (var b in active)
                        sb.AppendLine($"- [{b.Kind} c={b.Confidence:F2} e={b.EvidenceCount}] {b.Claim}");
                }

                if (uncertain.Count > 0)
                {
                    sb.AppendLine("uncertain_beliefs:");
                    foreach (var b in uncertain)
                        sb.AppendLine($"- [{b.Kind} c={b.Confidence:F2}] {b.Claim}");
                }

                sb.AppendLine("rules:");
                sb.AppendLine("- active beliefs may guide answers;");
                sb.AppendLine("- uncertain beliefs require cautious wording or confirmation;");
                sb.AppendLine("- contradicted/stale beliefs must not be stated as current fact.");
                return sb.ToString();
            }
        }

        public string BuildDebugLine()
        {
            lock (_lock)
            {
                var active = _data.Beliefs.Count(b => b.Status == "active");
                var uncertain = _data.Beliefs.Count(b => b.Status == "uncertain");
                var contradicted = _data.Beliefs.Count(b => b.Status == "contradicted");
                return $"beliefs active={active}, uncertain={uncertain}, contradicted={contradicted}";
            }
        }

        private KokoBelief? FindPossibleContradiction(KokoMemoryCandidate candidate)
        {
            var claim = Normalize(candidate.Text);
            var negated = IsNegated(claim);
            var tokens = claim.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 4 && !StopWords.Contains(t))
                .Take(8)
                .ToArray();

            if (tokens.Length == 0) return null;

            return _data.Beliefs
                .Where(b => b.Kind == candidate.Kind && b.Status != "stale")
                .FirstOrDefault(b =>
                {
                    var other = Normalize(b.Claim);
                    var overlap = tokens.Count(t => other.Contains(t, StringComparison.OrdinalIgnoreCase));
                    return overlap >= Math.Min(2, tokens.Length) && IsNegated(other) != negated;
                });
        }

        private void PruneLocked(DateTime now)
        {
            foreach (var b in _data.Beliefs)
            {
                if (b.Status == "active" &&
                    b.Confidence < 0.45 &&
                    now - b.LastSeen > TimeSpan.FromDays(90))
                    b.Status = "stale";
            }

            if (_data.Beliefs.Count > 300)
            {
                _data.Beliefs = _data.Beliefs
                    .OrderByDescending(b => b.Status == "active")
                    .ThenByDescending(b => b.Confidence * Math.Max(1, b.EvidenceCount))
                    .ThenByDescending(b => b.LastSeen)
                    .Take(240)
                    .ToList();
            }
        }

        private void AppendAudit(DateTime now, string line)
        {
            lock (_lock)
            {
                AppendAuditLocked(now, line);
                Save();
            }
        }

        private void AppendAuditLocked(DateTime now, string line)
        {
            _data.AuditLog.Add($"[{now:yyyy-MM-dd HH:mm}] {line}");
            if (_data.AuditLog.Count > 120)
                _data.AuditLog.RemoveRange(0, _data.AuditLog.Count - 120);
            _data.LastUpdatedAt = now;
        }

        private KokoContinuitySnapshot Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonConvert.DeserializeObject<KokoContinuitySnapshot>(File.ReadAllText(_path)) ?? new();
            }
            catch (Exception suppressedEx228) { KokoSystemLog.Write("CONTINUITYENGINE-CATCH", "Load failed near source line 228: " + suppressedEx228); }
            return new KokoContinuitySnapshot();
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented));
            }
            catch (Exception suppressedEx239) { KokoSystemLog.Write("CONTINUITYENGINE-CATCH", "Save failed near source line 239: " + suppressedEx239); }
        }

        private static string Normalize(string text)
        {
            text = text.ToLowerInvariant();
            foreach (var prefix in new[] { "я люблю ", "я обожнюю ", "я ненавиджу ", "мені подобається ", "мені не подобається ", "я хочу " })
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    text = text[prefix.Length..];
            text = new string(text.Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());
            return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool IsNegated(string text)
            => text.Contains(" не ", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ненавид", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("більше не", StringComparison.OrdinalIgnoreCase);

        private static string Trim(string text, int max)
            => text.Length <= max ? text : text[..max] + "...";

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "мені", "тебе", "щоб", "вона", "вони", "дуже", "буде", "треба", "типу", "коли", "якщо", "мене"
        };
    }
}
