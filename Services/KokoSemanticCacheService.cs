using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoSemanticCacheService
    {
        private readonly object _lock = new();
        private readonly string _path;
        private readonly List<SemanticCacheEntry> _entries = new();

        public KokoSemanticCacheService(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "semantic-cache.json");
            Load();
        }

        public bool TryGet(string query, out string answer)
        {
            answer = "";
            var q = Normalize(query);
            if (q.Length < 12)
                return false;

            lock (_lock)
            {
                var best = _entries
                    .Where(e => DateTime.Now - e.CreatedAt < TimeSpan.FromDays(7))
                    .Select(e => new { Entry = e, Score = Similarity(q, e.NormalizedQuery) })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();
                if (best == null || best.Score < 0.68)
                    return false;

                best.Entry.Hits++;
                best.Entry.LastHitAt = DateTime.Now;
                answer = best.Entry.Answer;
                SaveLocked();
                KokoSystemLog.Write("SEMANTIC_CACHE", $"hit score={best.Score:F2}; query={Trim(query, 90)}");
                return !string.IsNullOrWhiteSpace(answer);
            }
        }

        public void Put(string query, string answer)
        {
            var q = Normalize(query);
            if (q.Length < 12 || string.IsNullOrWhiteSpace(answer) || answer.Length < 20)
                return;
            if (answer.Contains("error", StringComparison.OrdinalIgnoreCase) && answer.Length < 80)
                return;

            lock (_lock)
            {
                var existing = _entries.FirstOrDefault(e => e.NormalizedQuery == q);
                if (existing != null)
                {
                    existing.Answer = answer.Trim();
                    existing.CreatedAt = DateTime.Now;
                }
                else
                {
                    _entries.Add(new SemanticCacheEntry
                    {
                        Query = query.Trim(),
                        NormalizedQuery = q,
                        Answer = answer.Trim(),
                        CreatedAt = DateTime.Now
                    });
                }
                if (_entries.Count > 250)
                    _entries.RemoveRange(0, _entries.Count - 250);
                SaveLocked();
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return;
                var loaded = JsonConvert.DeserializeObject<List<SemanticCacheEntry>>(File.ReadAllText(_path)) ?? new();
                _entries.Clear();
                _entries.AddRange(loaded.Where(e => !string.IsNullOrWhiteSpace(e.NormalizedQuery)));
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("SEMANTIC_CACHE", "load failed: " + ex.Message);
            }
        }

        private void SaveLocked()
        {
            try { File.WriteAllText(_path, JsonConvert.SerializeObject(_entries, Formatting.Indented)); }
            catch (Exception ex) { KokoSystemLog.Write("SEMANTIC_CACHE", "save failed: " + ex.Message); }
        }

        private static string Normalize(string value)
        {
            var lower = (value ?? "").ToLowerInvariant();
            lower = Regex.Replace(lower, @"https?://\S+", " ");
            lower = Regex.Replace(lower, @"[^\p{L}\p{Nd}\s]+", " ");
            var terms = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2)
                .Take(80);
            return string.Join(" ", terms);
        }

        private static double Similarity(string a, string b)
        {
            var aa = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var bb = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (aa.Count == 0 || bb.Count == 0)
                return 0;
            var intersection = aa.Intersect(bb, StringComparer.OrdinalIgnoreCase).Count();
            var union = aa.Union(bb, StringComparer.OrdinalIgnoreCase).Count();
            return union == 0 ? 0 : (double)intersection / union;
        }

        private static string Trim(string text, int max)
            => string.IsNullOrWhiteSpace(text) || text.Length <= max ? text : text[..max];
    }

    public sealed class SemanticCacheEntry
    {
        public string Query { get; set; } = "";
        public string NormalizedQuery { get; set; } = "";
        public string Answer { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime LastHitAt { get; set; }
        public int Hits { get; set; }
    }
}
