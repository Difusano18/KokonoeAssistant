using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant
{
    /// <summary>
    /// Enhanced Memory System —структурована память з відношеннями
    /// Взаємодіє з Knowledge Graph для автоматичного зв'язування фактів
    /// </summary>
    public class EnhancedMemory
    {
        public class Fact
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Content { get; set; } = null!;
            public DateTime Learned { get; set; } = DateTime.Now;
            public int Confidence { get; set; } = 100; // 0-100
            public string Category { get; set; } = null!; // "about_user", "preference", "habit", "goal", "observation"
            public List<string> RelatedFactIds { get; set; } = new();
            public string Source { get; set; } = null!; // де дізналась (например, "message", "deduction", "pattern")
            public int AccessCount { get; set; } = 0;
        }

        private readonly Dictionary<string, Fact> _facts = new();
        private readonly KnowledgeGraph _graph;
        private readonly string _vaultPath;
        private readonly string _factsPath;
        private readonly object _lock = new(); // Thread-safe access

        public IReadOnlyDictionary<string, Fact> Facts
        {
            get { lock (_lock) { return _facts.AsReadOnly(); } }
        }

        public EnhancedMemory(string vaultPath, KnowledgeGraph graph)
        {
            _vaultPath = vaultPath ?? throw new ArgumentNullException(nameof(vaultPath));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _factsPath = Path.Combine(vaultPath, "kokonoe-facts.json");
            Load();
        }

        public string LearnFact(string content, string category, string source = "learning", List<string>? relatedIds = null)
        {
            var fact = new Fact
            {
                Content = content,
                Category = category,
                Source = source,
                RelatedFactIds = relatedIds ?? new()
            };

            lock (_lock) { _facts[fact.Id] = fact; }

            // Add to graph
            var node = _graph.AddNode("fact", fact.Content.Substring(0, Math.Min(50, fact.Content.Length)),
                fact.Content, new() { { "category", category }, { "source", source } });

            // Link to related facts in graph
            foreach (var relatedId in fact.RelatedFactIds)
            {
                if (_facts.TryGetValue(relatedId, out var relatedFact))
                {
                    var relatedNode = _graph.Nodes.Values
                        .FirstOrDefault(n => n.Content == relatedFact.Content);
                    if (relatedNode != null)
                        _graph.AddEdge(node.Id, relatedNode.Id, "relates_to", 1.0, "memory-link");
                }
            }

            _graph.Save();
            Save();

            return fact.Id;
        }

        public void LinkFacts(string factId1, string factId2, string relation = "relates_to")
        {
            lock (_lock)
            {
                if (!_facts.TryGetValue(factId1, out var fact1) || !_facts.TryGetValue(factId2, out var fact2))
                    return;

                if (!fact1.RelatedFactIds.Contains(factId2))
                    fact1.RelatedFactIds.Add(factId2);
                if (!fact2.RelatedFactIds.Contains(factId1))
                    fact2.RelatedFactIds.Add(factId1);

                Save();
            }
        }

        public List<Fact> GetFactsByCategory(string category)
        {
            lock (_lock)
            {
                return _facts.Values
                    .Where(f => f.Category == category)
                    .OrderByDescending(f => f.Learned)
                    .ToList();
            }
        }

        public List<Fact> GetRelatedFacts(string factId, int depth = 1)
        {
            lock (_lock)
            {
                var result = new HashSet<string> { factId };
                var queue = new Queue<(string id, int d)>();
                queue.Enqueue((factId, 0));

                while (queue.Count > 0)
                {
                    var (currentId, currentDepth) = queue.Dequeue();
                    if (currentDepth >= depth || !_facts.TryGetValue(currentId, out var fact)) continue;

                    foreach (var relatedId in fact.RelatedFactIds)
                    {
                        if (result.Add(relatedId))
                            queue.Enqueue((relatedId, currentDepth + 1));
                    }
                }

                result.Remove(factId);
                return result.Select(id => _facts[id]).ToList();
            }
        }

        public string GetMemoryAsContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MEMORY ===\n");

            var categories = new[] { "about_user", "preference", "habit", "goal", "observation" };
            foreach (var cat in categories)
            {
                var facts = GetFactsByCategory(cat);
                if (facts.Any())
                {
                    sb.AppendLine($"## {cat}");
                    foreach (var fact in facts.Take(5))
                    {
                        sb.AppendLine($"- {fact.Content} (confidence: {fact.Confidence}%)");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public async Task<string> AnalyzeAndRefine()
        {
            // Check for contradictions, refine confidence, find patterns
            var sb = new StringBuilder();
            lock (_lock)
            {
                sb.AppendLine("Memory analysis:");
                sb.AppendLine($"Total facts: {_facts.Count}");

                var highConfidence = _facts.Values.Count(f => f.Confidence >= 80);
                var lowConfidence = _facts.Values.Count(f => f.Confidence < 50);

                sb.AppendLine($"High confidence: {highConfidence}");
                sb.AppendLine($"Low confidence: {lowConfidence}");
            }

            return await Task.FromResult(sb.ToString());
        }

        public string ExportAsMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Memory Export\n");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            sb.AppendLine($"Total facts: {_facts.Count}\n");

            var categories = new[] { "about_user", "preference", "habit", "goal", "observation" };
            foreach (var cat in categories)
            {
                var facts = GetFactsByCategory(cat);
                if (facts.Any())
                {
                    sb.AppendLine($"## {cat.Replace("_", " ").ToUpper()}\n");
                    foreach (var fact in facts)
                    {
                        sb.AppendLine($"- {fact.Content}");
                        sb.AppendLine($"  confidence: {fact.Confidence}% | learned: {fact.Learned:yyyy-MM-dd HH:mm} | source: {fact.Source}");
                        if (fact.RelatedFactIds.Any())
                            sb.AppendLine($"  related: {string.Join(", ", fact.RelatedFactIds.Take(3))}");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var tmp = _factsPath + ".tmp";
                    File.WriteAllText(tmp, JsonConvert.SerializeObject(_facts.Values, Formatting.Indented));
                    if (File.Exists(_factsPath)) File.Replace(tmp, _factsPath, null);
                    else                         File.Move(tmp, _factsPath);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EnhancedMemory] Save failed: {ex}"); }
            }
        }

        private void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_factsPath))
                    {
                        var facts = JsonConvert.DeserializeObject<List<Fact>>(File.ReadAllText(_factsPath));
                        if (facts != null)
                            foreach (var f in facts)
                                _facts[f.Id] = f;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EnhancedMemory] Load failed: {ex}"); }
            }
        }
    }
}
