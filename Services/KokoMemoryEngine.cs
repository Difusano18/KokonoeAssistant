using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════
    // KOKO MEMORY ENGINE
    // Довгострокова пам'ять Kokonoe про творця.
    // Три рівні: факти (persistent), епізоди (події), робоча пам'ять (сесія).
    // Підключається до EnhancedMemory для збереження і KnowledgeGraph для зв'язків.
    // ══════════════════════════════════════════════════════════════════

    public class KokoMemoryEngine
    {
        // ── Типи записів ──────────────────────────────────────────────

        public class MemoryFact
        {
            public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..12];
            public string  Content     { get; set; } = "";
            public string  Category    { get; set; } = "general"; // about_user / preference / fear / desire / habit / value
            public float   Importance  { get; set; } = 0.5f;      // 0..1
            public int     ConfirmCount { get; set; } = 1;         // скільки разів підтверджено
            public DateTime FirstSeen  { get; set; } = DateTime.Now;
            public DateTime LastSeen   { get; set; } = DateTime.Now;
            public List<string> Tags   { get; set; } = new();
        }

        public class MemoryEpisode
        {
            public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..12];
            public DateTime When       { get; set; } = DateTime.Now;
            public string  Summary     { get; set; } = "";
            public string  EmotionalTone { get; set; } = "neutral";
            public float   Intensity   { get; set; } = 0.5f;       // 0..1 — наскільки значима подія
            public List<string> Keywords { get; set; } = new();
        }

        public class WorkingMemory
        {
            public List<string> CurrentSessionFacts { get; set; } = new();  // що дізналась сьогодні
            public string       LastUserMessage     { get; set; } = "";
            public string       CurrentContext      { get; set; } = "";
            public DateTime     SessionStart        { get; set; } = DateTime.Now;
        }

        // ── Стан ──────────────────────────────────────────────────────

        private readonly string              _factsPath;
        private readonly string              _episodesPath;
        private readonly EnhancedMemory?     _enhanced;  // може бути null якщо не ініц
        private readonly object              _lock = new();

        private List<MemoryFact>    _facts    = new();
        private List<MemoryEpisode> _episodes = new();
        public  WorkingMemory       Working   { get; } = new();

        public IReadOnlyList<MemoryFact>    Facts    => _facts.AsReadOnly();
        public IReadOnlyList<MemoryEpisode> Episodes => _episodes.AsReadOnly();

        // ── Ініціалізація ─────────────────────────────────────────────

        public KokoMemoryEngine(string dataDir, EnhancedMemory? enhanced = null)
        {
            _factsPath    = Path.Combine(dataDir, "koko-facts.json");
            _episodesPath = Path.Combine(dataDir, "koko-episodes.json");
            _chainsPath   = Path.Combine(dataDir, "koko-chains.json");
            _enhanced     = enhanced;
            Load();
        }

        // ── ФАКТИ ─────────────────────────────────────────────────────

        /// <summary>Додати або оновити факт про творця</summary>
        public MemoryFact LearnFact(string content, string category = "general", float importance = 0.5f, string[]? tags = null)
        {
            lock (_lock)
            {
                // Шукаємо схожий факт (щоб не дублювати)
                var existing = _facts.FirstOrDefault(f =>
                    f.Content.Equals(content, StringComparison.OrdinalIgnoreCase) ||
                    (f.Category == category && Similarity(f.Content, content) > 0.8f));

                if (existing != null)
                {
                    existing.ConfirmCount++;
                    existing.LastSeen  = DateTime.Now;
                    existing.Importance = Math.Min(1f, existing.Importance + 0.05f); // підтверджений факт стає важливішим
                    if (tags != null) existing.Tags = existing.Tags.Union(tags).Distinct().ToList();
                    Save();
                    return existing;
                }

                var fact = new MemoryFact
                {
                    Content    = content,
                    Category   = category,
                    Importance = importance,
                    Tags       = tags?.ToList() ?? new()
                };

                _facts.Add(fact);

                // Синхронізація з EnhancedMemory якщо є
                try { _enhanced?.LearnFact(content, category, "brain"); }
                catch (Exception ex) { Debug.WriteLine($"[KokoMemory] EnhancedMemory.LearnFact failed: {ex.Message}"); }

                // Додати в робочу пам'ять сесії
                Working.CurrentSessionFacts.Add($"[{category}] {content}");
                if (Working.CurrentSessionFacts.Count > 30)
                    Working.CurrentSessionFacts.RemoveAt(0);

                Save();
                return fact;
            }
        }

        /// <summary>Пошук фактів за запитом</summary>
        public List<MemoryFact> Recall(string query, int max = 10)
        {
            lock (_lock)
            {
                var q = query.ToLower();
                return _facts
                    .Where(f => f.Content.ToLower().Contains(q) ||
                                f.Tags.Any(t => t.ToLower().Contains(q)) ||
                                f.Category.ToLower().Contains(q))
                    .OrderByDescending(f => f.Importance)
                    .ThenByDescending(f => f.ConfirmCount)
                    .Take(max)
                    .ToList();
            }
        }

        /// <summary>Найважливіші факти для контексту</summary>
        public List<MemoryFact> GetTopFacts(int max = 15)
        {
            lock (_lock)
            {
                return _facts
                    .OrderByDescending(f => f.Importance * f.ConfirmCount)
                    .Take(max)
                    .ToList();
            }
        }

        /// <summary>Факти за категорією</summary>
        public List<MemoryFact> GetByCategory(string category, int max = 10)
        {
            lock (_lock)
            {
                return _facts
                    .Where(f => f.Category == category)
                    .OrderByDescending(f => f.Importance)
                    .Take(max)
                    .ToList();
            }
        }

        /// <summary>Видалити факт</summary>
        public bool ForgetFact(string factId)
        {
            lock (_lock)
            {
                var f = _facts.FirstOrDefault(x => x.Id == factId);
                if (f == null) return false;
                _facts.Remove(f);
                Save();
                return true;
            }
        }

        // ── ЕПІЗОДИ ───────────────────────────────────────────────────

        /// <summary>Зафіксувати значущу подію</summary>
        public MemoryEpisode RecordEpisode(string summary, string emotionalTone = "neutral", float intensity = 0.5f, string[]? keywords = null)
        {
            lock (_lock)
            {
                var ep = new MemoryEpisode
                {
                    Summary       = summary,
                    EmotionalTone = emotionalTone,
                    Intensity     = intensity,
                    Keywords      = keywords?.ToList() ?? ExtractKeywords(summary)
                };

                _episodes.Add(ep);

                // Зберігаємо не більше 200 епізодів (видаляємо старі малозначущі)
                if (_episodes.Count > 200)
                {
                    _episodes = _episodes
                        .OrderByDescending(e => e.Intensity * 0.7f + (float)(e.When - DateTime.MinValue).TotalDays * 0.0001f)
                        .Take(150)
                        .OrderBy(e => e.When)
                        .ToList();
                }

                Save();
                return ep;
            }
        }

        /// <summary>Останні N епізодів</summary>
        public List<MemoryEpisode> GetRecentEpisodes(int max = 10)
        {
            lock (_lock)
            {
                return _episodes.OrderByDescending(e => e.When).Take(max).ToList();
            }
        }

        /// <summary>Найінтенсивніші епізоди (емоційні піки)</summary>
        public List<MemoryEpisode> GetPeakEpisodes(int max = 5)
        {
            lock (_lock)
            {
                return _episodes.OrderByDescending(e => e.Intensity).Take(max).ToList();
            }
        }

        /// <summary>Епізоди з певним тоном</summary>
        public List<MemoryEpisode> GetEpisodesByTone(string tone, int max = 10)
        {
            lock (_lock)
            {
                return _episodes
                    .Where(e => e.EmotionalTone == tone)
                    .OrderByDescending(e => e.When)
                    .Take(max)
                    .ToList();
            }
        }

        public MemoryEpisode? GetRandomSignificantEpisode()
        {
            lock (_lock)
            {
                var candidates = _episodes.Where(e => e.Intensity >= 0.5f).ToList();
                if (candidates.Count == 0) return null;
                return candidates[Random.Shared.Next(candidates.Count)];
            }
        }

        // ── КОНСОЛІДАЦІЯ ──────────────────────────────────────────────

        /// <summary>Консолідувати пам'ять: видалити дублікати, підвищити важливість підтверджених фактів</summary>
        public int Consolidate()
        {
            lock (_lock)
            {
                var before = _facts.Count;

                // Видалити факти з низькою важливістю і 1 підтвердженням старші 30 днів
                _facts.RemoveAll(f =>
                    f.Importance < 0.2f &&
                    f.ConfirmCount == 1 &&
                    (DateTime.Now - f.FirstSeen).TotalDays > 30);

                // Обмежити до 500 фактів — залишити найважливіші
                if (_facts.Count > 500)
                {
                    _facts = _facts
                        .OrderByDescending(f => f.Importance * f.ConfirmCount)
                        .Take(500)
                        .ToList();
                }

                Save();
                return before - _facts.Count; // скільки видалено
            }
        }

        // ── КОНТЕКСТ ДЛЯ LLM ─────────────────────────────────────────

        /// <summary>Сформувати блок пам'яті для системного промпту</summary>
        public string BuildMemoryContext(int maxFacts = 12, int maxEpisodes = 5)
        {
            lock (_lock)
            {
                var sb = new StringBuilder();

                var topFacts = _facts
                    .OrderByDescending(f => f.Importance * f.ConfirmCount)
                    .Take(maxFacts)
                    .ToList();

                if (topFacts.Count > 0)
                {
                    sb.AppendLine("=== ПАМ'ЯТЬ — ФАКТИ ПРО НЬОГО ===");
                    foreach (var f in topFacts)
                        sb.AppendLine($"• [{f.Category}] {f.Content} (підтверджено {f.ConfirmCount}×)");
                }

                var recentEps = _episodes
                    .OrderByDescending(e => e.When)
                    .Take(maxEpisodes)
                    .ToList();

                if (recentEps.Count > 0)
                {
                    sb.AppendLine("\n=== ПАМ'ЯТЬ — ОСТАННІ ПОДІЇ ===");
                    foreach (var e in recentEps)
                        sb.AppendLine($"• [{e.When:dd.MM HH:mm}] ({e.EmotionalTone}, {e.Intensity:P0}) {e.Summary}");
                }

                if (Working.CurrentSessionFacts.Count > 0)
                {
                    sb.AppendLine("\n=== ЦЯ СЕСІЯ — НОВЕ ===");
                    foreach (var f in Working.CurrentSessionFacts.TakeLast(5))
                        sb.AppendLine($"• {f}");
                }

                return sb.ToString();
            }
        }

        /// <summary>Знайти факти і епізоди релевантні до поточного тексту</summary>
        public (List<MemoryFact> Facts, List<MemoryEpisode> Episodes) FindRelevant(
            string query, int maxFacts = 3, int maxEpisodes = 2)
        {
            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return (new(), new());

                var words = query.ToLower()
                    .Split(' ', '.', ',', '?', '!', '\n')
                    .Where(w => w.Length > 3)
                    .ToHashSet();

                var scoredFacts = _facts
                    .Select(f => {
                        var fWords = f.Content.ToLower().Split(' ').ToHashSet();
                        var overlap = words.Count(w => fWords.Any(fw => fw.Contains(w) || w.Contains(fw)));
                        return (f, score: overlap + f.Importance * f.ConfirmCount * 0.5f);
                    })
                    .Where(x => x.score > 0.5f)
                    .OrderByDescending(x => x.score)
                    .Take(maxFacts)
                    .Select(x => x.f)
                    .ToList();

                var scoredEps = _episodes
                    .Select(e => {
                        var eWords = (e.Summary + " " + string.Join(" ", e.Keywords)).ToLower().Split(' ').ToHashSet();
                        var overlap = words.Count(w => eWords.Any(ew => ew.Contains(w) || w.Contains(ew)));
                        return (e, score: overlap + e.Intensity * 0.3f);
                    })
                    .Where(x => x.score > 0.5f)
                    .OrderByDescending(x => x.score)
                    .Take(maxEpisodes)
                    .Select(x => x.e)
                    .ToList();

                return (scoredFacts, scoredEps);
            }
        }

        // ── КАУЗАЛЬНІ ЛАНЦЮГИ ────────────────────────────────────────

        public class CausalChain
        {
            public string Id           { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string Trigger      { get; set; } = ""; // "він сказав: втомився"
            public string TriggerTone  { get; set; } = "neutral";
            public string Response     { get; set; } = ""; // "я відповіла коротко"
            public string Outcome      { get; set; } = ""; // "він заспокоївся"
            public bool   WasPositive  { get; set; } = true;
            public int    SuccessCount { get; set; } = 0;
            public int    TotalCount   { get; set; } = 1;
            public float  SuccessRate  => TotalCount > 0 ? (float)SuccessCount / TotalCount : 0f;
            public DateTime LastSeen   { get; set; } = DateTime.Now;
        }

        private List<CausalChain> _chains = new();
        private readonly string   _chainsPath;

        /// <summary>Записати причинно-наслідковий зв'язок</summary>
        public void RecordCausalChain(string trigger, string triggerTone, string response, string outcome, bool wasPositive)
        {
            lock (_lock)
            {
                // Знайти схожий chain
                var existing = _chains.FirstOrDefault(c =>
                    c.TriggerTone == triggerTone &&
                    Similarity(c.Trigger, trigger) > 0.5f);

                if (existing != null)
                {
                    existing.TotalCount++;
                    if (wasPositive) existing.SuccessCount++;
                    existing.LastSeen = DateTime.Now;
                }
                else
                {
                    _chains.Add(new CausalChain
                    {
                        Trigger      = trigger,
                        TriggerTone  = triggerTone,
                        Response     = response,
                        Outcome      = outcome,
                        WasPositive  = wasPositive,
                        SuccessCount = wasPositive ? 1 : 0,
                    });
                    if (_chains.Count > 300) _chains.RemoveAt(0);
                }
                SaveChains();
            }
        }

        /// <summary>Що ефективно спрацьовує при певному тоні</summary>
        public string GetEffectiveResponses(string tone)
        {
            lock (_lock)
            {
                var relevant = _chains
                    .Where(c => c.TriggerTone == tone && c.TotalCount >= 2)
                    .OrderByDescending(c => c.SuccessRate)
                    .Take(3)
                    .ToList();
                if (!relevant.Any()) return "";
                var best = relevant.First();
                return $"Коли він {tone}: «{best.Response}» спрацьовує в {best.SuccessRate:P0} ({best.TotalCount} разів)";
            }
        }

        // ── ТОПІКИ ──────────────────────────────────────────────────

        /// <summary>Топ N тем за частотою в епізодах</summary>
        public string GetTopicSummary(int topN = 5)
        {
            lock (_lock)
            {
                var allKeywords = _episodes
                    .SelectMany(e => e.Keywords)
                    .GroupBy(k => k.ToLower())
                    .Where(g => g.Count() >= 2)
                    .OrderByDescending(g => g.Count())
                    .Take(topN)
                    .Select(g => $"{g.Key}({g.Count()})");
                var result = string.Join(", ", allKeywords);
                return string.IsNullOrEmpty(result) ? "" : $"Часті теми: {result}";
            }
        }

        // ── СТАРІННЯ ФАКТІВ ──────────────────────────────────────────

        /// <summary>Знизити важливість застарілих непідтверджених фактів</summary>
        public int ImportanceDecay()
        {
            lock (_lock)
            {
                var decayed = 0;
                var cutoff  = DateTime.Now.AddDays(-60);
                foreach (var f in _facts.Where(f => f.LastSeen < cutoff && f.ConfirmCount <= 1))
                {
                    f.Importance = Math.Max(0.05f, f.Importance * 0.9f);
                    decayed++;
                }
                if (decayed > 0) Save();
                return decayed;
            }
        }

        // ── PERSISTENCE ───────────────────────────────────────────────

        private void Load()
        {
            try
            {
                if (File.Exists(_factsPath))
                    _facts = JsonConvert.DeserializeObject<List<MemoryFact>>(File.ReadAllText(_factsPath)) ?? new();
                if (File.Exists(_episodesPath))
                    _episodes = JsonConvert.DeserializeObject<List<MemoryEpisode>>(File.ReadAllText(_episodesPath)) ?? new();
                if (File.Exists(_chainsPath))
                    _chains = JsonConvert.DeserializeObject<List<CausalChain>>(File.ReadAllText(_chainsPath)) ?? new();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KokoMemory] Load failed, resetting in-memory state: {ex}");
                _facts = new(); _episodes = new(); _chains = new();
            }
        }

        private void Save()
        {
            try
            {
                AtomicWrite(_factsPath,    JsonConvert.SerializeObject(_facts,    Formatting.Indented));
                AtomicWrite(_episodesPath, JsonConvert.SerializeObject(_episodes, Formatting.Indented));
            }
            catch (Exception ex) { Debug.WriteLine($"[KokoMemory] Save failed: {ex}"); }
        }

        private void SaveChains()
        {
            try { AtomicWrite(_chainsPath, JsonConvert.SerializeObject(_chains, Formatting.Indented)); }
            catch (Exception ex) { Debug.WriteLine($"[KokoMemory] SaveChains failed: {ex}"); }
        }

        private static void AtomicWrite(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else                   File.Move(tmp, path);
        }

        // ── УТИЛІТИ ───────────────────────────────────────────────────

        private static float Similarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;
            var wordsA = new HashSet<string>(a.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var wordsB = new HashSet<string>(b.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var intersection = wordsA.Intersect(wordsB).Count();
            var union = wordsA.Union(wordsB).Count();
            return union == 0 ? 0f : (float)intersection / union;
        }

        private static List<string> ExtractKeywords(string text)
        {
            var stopWords = new HashSet<string> { "і", "в", "на", "що", "як", "це", "він", "вона", "the", "a", "is", "in", "of" };
            return text.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !stopWords.Contains(w))
                .Distinct()
                .Take(6)
                .ToList();
        }
    }
}
