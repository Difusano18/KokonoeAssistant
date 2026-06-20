using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KokonoeAssistant.Services;
using Newtonsoft.Json;

namespace KokonoeAssistant
{
    /// <summary>
    /// Knowledge Graph Engine — будує зв'язки між фактами, нотатками та концепціями.
    /// Кожна нова інформація автоматично зв'язується з існуючим графом.
    /// </summary>
    public class KnowledgeGraph
    {
        public class GraphNode
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Type { get; set; } = null!; // "fact", "note", "concept", "goal", "person", "project"
            public string Label { get; set; } = null!;
            public string Content { get; set; } = null!;
            public DateTime Created { get; set; } = DateTime.Now;
            public DateTime LastAccessed { get; set; } = DateTime.Now;
            public int AccessCount { get; set; } = 0;
            public Dictionary<string, string> Metadata { get; set; } = new();
            public double ConfidenceScore { get; set; } = 1.0; // 0-1
        }

        public class GraphEdge
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string SourceId { get; set; } = null!;
            public string TargetId { get; set; } = null!;
            public string Relation { get; set; } = null!; // "mentions", "relates_to", "depends_on", "contradicts", "supports", "occurs_during"
            public double Weight { get; set; } = 1.0;
            public DateTime Created { get; set; } = DateTime.Now;
            public string Evidence { get; set; } = null!; // де цей зв'язок знайдешся
        }

        private readonly Dictionary<string, GraphNode> _nodes = new();
        private readonly List<GraphEdge> _edges = new();
        private readonly string _graphPath;
        private readonly string _nodesPath;
        private readonly string _edgesPath;
        private readonly object _graphLock = new(); // Thread-safe access to graph

        public IReadOnlyDictionary<string, GraphNode> Nodes => _nodes.AsReadOnly();
        public IReadOnlyList<GraphEdge> Edges => _edges.AsReadOnly();

        public KnowledgeGraph(string vaultPath)
        {
            _graphPath = Path.Combine(vaultPath, "kokonoe-graph");
            _nodesPath = Path.Combine(_graphPath, "nodes.json");
            _edgesPath = Path.Combine(_graphPath, "edges.json");
            
            Directory.CreateDirectory(_graphPath);
            Load();
        }

        public GraphNode AddNode(string type, string label, string content, Dictionary<string, string>? metadata = null)
        {
            lock (_graphLock)
            {
                var node = new GraphNode
                {
                    Type = type,
                    Label = label,
                    Content = content,
                    Metadata = metadata ?? new()
                };

                _nodes[node.Id] = node;
                return node;
            }
        }

        public GraphEdge? AddEdge(string sourceId, string targetId, string relation, double weight = 1.0, string evidence = "")
        {
            lock (_graphLock)
            {
                if (!_nodes.ContainsKey(sourceId) || !_nodes.ContainsKey(targetId)) return null;

                var existingEdge = _edges.FirstOrDefault(e => e.SourceId == sourceId && e.TargetId == targetId && e.Relation == relation);
                if (existingEdge != null)
                {
                    existingEdge.Weight += weight;
                    existingEdge.Evidence = evidence;
                    return existingEdge;
                }

                var edge = new GraphEdge
                {
                    SourceId = sourceId,
                    TargetId = targetId,
                    Relation = relation,
                    Weight = weight,
                    Evidence = evidence
                };

                _edges.Add(edge);
                return edge;
            }
        }

        public void AccessNode(string nodeId)
        {
            lock (_graphLock)
            {
                if (_nodes.TryGetValue(nodeId, out var node))
                {
                    node.AccessCount++;
                    node.LastAccessed = DateTime.Now;
                }
            }
        }

        public List<GraphNode> FindRelated(string nodeId, int depth = 2, string? filterRelation = null)
        {
            lock (_graphLock)
            {
                var result = new HashSet<string> { nodeId };
                var queue = new Queue<(string id, int d)>();
                queue.Enqueue((nodeId, 0));

                while (queue.Count > 0)
                {
                    var (currentId, currentDepth) = queue.Dequeue();
                    if (currentDepth >= depth) continue;

                    var relatedEdges = _edges
                        .Where(e => (e.SourceId == currentId || e.TargetId == currentId) &&
                                    (filterRelation == null || e.Relation == filterRelation))
                        .ToList();

                    foreach (var edge in relatedEdges)
                    {
                        var nextId = edge.SourceId == currentId ? edge.TargetId : edge.SourceId;
                        if (result.Add(nextId))
                            queue.Enqueue((nextId, currentDepth + 1));
                    }
                }

                result.Remove(nodeId);
                return result.Select(id => _nodes[id]).ToList();
            }
        }

        public string GetGraphVizDot(int maxNodes = 50)
        {
            lock (_graphLock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("digraph KnowledgeGraph {");
                sb.AppendLine("    rankdir=LR;");
                sb.AppendLine("    node [shape=box, style=rounded];");

                var topNodes = _nodes.Values
                    .OrderByDescending(n => n.AccessCount)
                    .ThenByDescending(n => n.Created)
                    .Take(maxNodes)
                    .ToList();

                var nodeIds = topNodes.Select(n => n.Id).ToHashSet();

                foreach (var node in topNodes)
                {
                    var color = node.Type switch
                    {
                        "fact" => "lightblue",
                        "goal" => "lightgreen",
                        "project" => "lightyellow",
                        "person" => "lightcoral",
                        "concept" => "lavender",
                        _ => "lightgray"
                    };
                    sb.AppendLine($"    \"{node.Id}\" [label=\"{EscapeDot(node.Label)}\", fillcolor={color}, style=\"filled,rounded\"];");
                }

                foreach (var edge in _edges.Where(e => nodeIds.Contains(e.SourceId) && nodeIds.Contains(e.TargetId)))
                {
                    sb.AppendLine($"    \"{edge.SourceId}\" -> \"{edge.TargetId}\" [label=\"{EscapeDot(edge.Relation)}\", weight={edge.Weight}];");
                }

                sb.AppendLine("}");
                return sb.ToString();
            }
        }

        private string EscapeDot(string text)
        {
            return text.Replace("\"", "\\\"").Replace("\n", "\\n").Substring(0, Math.Min(50, text.Length));
        }

        public void Save()
        {
            lock (_graphLock)
            {
                try
                {
                    File.WriteAllText(_nodesPath, JsonConvert.SerializeObject(_nodes.Values, Formatting.Indented));
                    File.WriteAllText(_edgesPath, JsonConvert.SerializeObject(_edges, Formatting.Indented));
                }
                catch (Exception suppressedEx203) { KokoSystemLog.Write("KNOWLEDGEGRAPH-CATCH", "Save failed near source line 203: " + suppressedEx203); }
            }
        }

        private void Load()
        {
            lock (_graphLock)
            {
                try
                {
                    if (File.Exists(_nodesPath))
                    {
                        var nodes = JsonConvert.DeserializeObject<List<GraphNode>>(File.ReadAllText(_nodesPath));
                        if (nodes != null)
                            foreach (var n in nodes)
                                _nodes[n.Id] = n;
                    }

                    if (File.Exists(_edgesPath))
                    {
                        var edges = JsonConvert.DeserializeObject<List<GraphEdge>>(File.ReadAllText(_edgesPath));
                        if (edges != null)
                            _edges.AddRange(edges);
                    }
                }
                catch (Exception suppressedEx228) { KokoSystemLog.Write("KNOWLEDGEGRAPH-CATCH", "Load failed near source line 228: " + suppressedEx228); }
            }
        }

        public string ExportAsMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Knowledge Graph Export\n");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            sb.AppendLine($"Total Nodes: {_nodes.Count} | Total Edges: {_edges.Count}\n");

            var nodesByType = _nodes.Values.GroupBy(n => n.Type);
            foreach (var group in nodesByType)
            {
                sb.AppendLine($"## {group.Key}s ({group.Count()})\n");
                foreach (var node in group.OrderByDescending(n => n.AccessCount))
                {
                    sb.AppendLine($"- **{node.Label}** (created: {node.Created:yyyy-MM-dd}, accessed: {node.AccessCount}x)");
                    if (!string.IsNullOrEmpty(node.Content))
                        sb.AppendLine($"  > {node.Content.Substring(0, Math.Min(100, node.Content.Length))}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
