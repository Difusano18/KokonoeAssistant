using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KokonoeAssistant
{
    /// <summary>
    /// Tool/Plugin System — виконує операції у Obsidian, будує графи, аналізує дані
    /// LLM може запросити виконання операції через структурований format
    /// </summary>
    public abstract class KokonooTool
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public abstract Task<string> Execute(Dictionary<string, string> parameters);

        public virtual string GetUsageExample() => $"Tool: {Name}\n{Description}";
    }

    public class ToolExecutor
    {
        private readonly Dictionary<string, KokonooTool> _tools = new();
        private readonly string _vaultPath;
        private readonly KnowledgeGraph _graph;
        private readonly StateEngine _state;

        public ToolExecutor(string vaultPath, KnowledgeGraph graph, StateEngine state)
        {
            _vaultPath = vaultPath;
            _graph = graph;
            _state = state;
            RegisterDefaultTools();
        }

        private void RegisterDefaultTools()
        {
            RegisterTool(new SearchVaultTool(_vaultPath));
            RegisterTool(new CreateNoteTool(_vaultPath, _graph));
            RegisterTool(new LinkNotesTool(_vaultPath, _graph));
            RegisterTool(new BuildGraphTool(_graph));
            RegisterTool(new AnalyzeNotesTool(_vaultPath, _graph));
            RegisterTool(new FindRelationshipsTool(_vaultPath, _graph));
            RegisterTool(new GenerateGraphVisualizationTool(_vaultPath, _graph));
        }

        public void RegisterTool(KokonooTool tool)
        {
            _tools[tool.Name.ToLower()] = tool;
        }

        public async Task<string> ExecuteFromPrompt(string toolCall)
        {
            // Parse format: TOOL: <name> | params=<json>
            var match = Regex.Match(toolCall, @"TOOL:\s*(\w+)\s*\|\s*params=(.+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid tool format";

            var toolName = match.Groups[1].Value.ToLower();
            var paramsJson = match.Groups[2].Value;

            if (!_tools.TryGetValue(toolName, out var tool))
                return $"Tool '{toolName}' not found";

            try
            {
                var parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(paramsJson) 
                    ?? new Dictionary<string, string>();
                return await tool.Execute(parameters);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public List<string> GetAvailableTools() => _tools.Keys.ToList();

        public string GetToolsList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available tools:");
            foreach (var (name, tool) in _tools)
            {
                sb.AppendLine($"  - {name}: {tool.Description}");
            }
            return sb.ToString();
        }
    }

    // ═══════════════════════════════════════
    // BUILT-IN TOOLS
    // ═══════════════════════════════════════

    public class SearchVaultTool : KokonooTool
    {
        private readonly string _vaultPath;

        public SearchVaultTool(string vaultPath)
        {
            Name = "search_vault";
            Description = "Search vault notes by keyword. Returns top matches.";
            _vaultPath = vaultPath;
        }

        public override async Task<string> Execute(Dictionary<string, string> parameters)
        {
            return await Task.Run(() =>
            {
                if (!parameters.TryGetValue("query", out var query) || string.IsNullOrEmpty(query))
                    return "Parameter 'query' required";

                var results = new List<(string file, int matches)>();
                var queryLower = query.ToLower();

                try
                {
                    foreach (var file in Directory.GetFiles(_vaultPath, "*.md", SearchOption.AllDirectories))
                    {
                        var content = File.ReadAllText(file).ToLower();
                        var count = (content.Length - content.Replace(queryLower, "").Length) / queryLower.Length;
                        if (count > 0)
                            results.Add((Path.GetFileNameWithoutExtension(file), count));
                    }
                }
                catch { }

                if (results.Count == 0)
                    return "No matches found";

                var sb = new StringBuilder();
                foreach (var (file, count) in results.OrderByDescending(x => x.matches).Take(5))
                    sb.AppendLine($"- {file} ({count} matches)");
                return sb.ToString();
            });
        }
    }

    public class CreateNoteTool : KokonooTool
    {
        private readonly string _vaultPath;
        private readonly KnowledgeGraph _graph;

        public CreateNoteTool(string vaultPath, KnowledgeGraph graph)
        {
            Name = "create_note";
            Description = "Create a new note in vault. Returns path.";
            _vaultPath = vaultPath;
            _graph = graph;
        }

        public override async Task<string> Execute(Dictionary<string, string> parameters)
        {
            return await Task.Run(() =>
            {
                if (!parameters.TryGetValue("title", out var title) || string.IsNullOrEmpty(title))
                    return "Parameter 'title' required";

                parameters.TryGetValue("content", out var content);
                parameters.TryGetValue("tags", out var tags);

                var safe = string.Concat(title.Split(Path.GetInvalidFileNameChars())).Trim();
                var path = Path.Combine(_vaultPath, $"{safe}.md");

                var tagsStr = string.IsNullOrEmpty(tags) ? "" : string.Join(",", tags.Split(','));
                var body = $"---\ndate: {DateTime.Now:yyyy-MM-dd}\ntags: [{tagsStr}]\ncreated-by: kokonoe-tools\n---\n\n# {title}\n\n{content ?? ""}\n";

                try
                {
                    File.WriteAllText(path, body);
                    
                    // Add to graph
                    var node = _graph.AddNode("note", title, content ?? "", new() { { "path", path } });
                    _graph.Save();

                    return $"Note created: {path}";
                }
                catch (Exception ex) { return $"Error: {ex.Message}"; }
            });
        }
    }

    public class LinkNotesTool : KokonooTool
    {
        private readonly string _vaultPath;
        private readonly KnowledgeGraph _graph;

        public LinkNotesTool(string vaultPath, KnowledgeGraph graph)
        {
            Name = "link_notes";
            Description = "Link two notes together in the graph. Creates bidirectional references.";
            _vaultPath = vaultPath;
            _graph = graph;
        }

        public override async Task<string> Execute(Dictionary<string, string> parameters)
        {
            return await Task.Run(() =>
            {
                if (!parameters.TryGetValue("note1", out var note1) || !parameters.TryGetValue("note2", out var note2))
                    return "Parameters 'note1' and 'note2' required";

                parameters.TryGetValue("relation", out var relation);
                relation ??= "relates_to";

                // Find notes and link them
                var nodes1 = _graph.Nodes.Values.Where(n => n.Label.Contains(note1, StringComparison.OrdinalIgnoreCase)).ToList();
                var nodes2 = _graph.Nodes.Values.Where(n => n.Label.Contains(note2, StringComparison.OrdinalIgnoreCase)).ToList();

                if (nodes1.Count == 0 || nodes2.Count == 0)
                    return "One or both notes not found in graph";

                try
                {
                    _graph.AddEdge(nodes1[0].Id, nodes2[0].Id, relation, 1.0, "user-linked");
                    _graph.AddEdge(nodes2[0].Id, nodes1[0].Id, relation, 1.0, "user-linked");
                    _graph.Save();

                    return $"Linked: {note1} <-[{relation}]-> {note2}";
                }
                catch (Exception ex) { return $"Error: {ex.Message}"; }
            });
        }
    }

    public class BuildGraphTool : KokonooTool
    {
        private readonly KnowledgeGraph _graph;

        public BuildGraphTool(KnowledgeGraph graph)
        {
            Name = "build_graph";
            Description = "Analyze vault and build/rebuild knowledge graph from notes.";
            _graph = graph;
        }

        public override async Task<string> Execute(Dictionary<string, string> parameters)
        {
            return await Task.Run(() =>
            {
                // Simulated graph building
                var nodeCount = _graph.Nodes.Count;
                var edgeCount = _graph.Edges.Count;

                _graph.Save();
                return $"Graph saved: {nodeCount} nodes, {edgeCount} edges";
            });
        }
    }

    public class AnalyzeNotesTool : KokonooTool
    {
        private readonly string _vaultPath;
        private readonly KnowledgeGraph _graph;

        public AnalyzeNotesTool(string vaultPath, KnowledgeGraph graph)
        {
            Name = "analyze_notes";
            Description = "Analyze notes in vault. Returns statistics.";
            _vaultPath = vaultPath;
            _graph = graph;
        }

        public override async Task<string> Execute(Dictionary<string, string> parameters)
        {
            return await Task.Run(() =>
            {
                var files = Directory.GetFiles(_vaultPath, "*.md").Length;
                var nodes = _graph.Nodes.Count;
                var edges = _graph.Edges.Count;

                var sb = new StringBuilder();
                sb.AppendLine($"Vault analysis:");
                sb.AppendLine($"  Files: {files}");
                sb.AppendLine($"  Graph nodes: {nodes}");
                sb.AppendLine($"  Graph edges: {edges}");
                sb.AppendLine($"  Density: {(nodes > 0 ? (double)edges / (nodes * (nodes - 1)) : 0):P2}");
                return sb.ToString();
            });
        }
    }

    public class FindRelationshipsTool : KokonooTool
    {
        private readonly string _vaultPath;
        private readonly KnowledgeGraph _graph;

        public FindRelationshipsTool(string vaultPath, KnowledgeGraph graph)
        {
            Name = "find_relationships";
            Description = "Find all relationships for a concept in the graph.";
            _vaultPath = vaultPath;
            _graph = graph;
        }

        public override async Task<string> Execute(Dictionary<string, string> parameters)
        {
            return await Task.Run(() =>
            {
                if (!parameters.TryGetValue("concept", out var concept) || string.IsNullOrEmpty(concept))
                    return "Parameter 'concept' required";

                var node = _graph.Nodes.Values.FirstOrDefault(n => n.Label.Contains(concept, StringComparison.OrdinalIgnoreCase));
                if (node == null)
                    return $"Concept '{concept}' not found";

                var related = _graph.FindRelated(node.Id, 2);

                var sb = new StringBuilder();
                sb.AppendLine($"Relationships for '{node.Label}':");
                foreach (var r in related.Take(10))
                    sb.AppendLine($"  - {r.Label} ({r.Type})");
                return sb.ToString();
            });
        }
    }

    public class GenerateGraphVisualizationTool : KokonooTool
    {
        private readonly string _vaultPath;
        private readonly KnowledgeGraph _graph;

        public GenerateGraphVisualizationTool(string vaultPath, KnowledgeGraph graph)
        {
            Name = "generate_visualization";
            Description = "Generate graph visualization as DOT (Graphviz) format.";
            _vaultPath = vaultPath;
            _graph = graph;
        }

        public override async Task<string> Execute(Dictionary<string, string> parameters)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var dot = _graph.GetGraphVizDot();
                    var outputPath = Path.Combine(_vaultPath, "graph_visualization.dot");
                    File.WriteAllText(outputPath, dot);
                    return $"Graph visualization saved to: {outputPath}";
                }
                catch (Exception ex) { return $"Error: {ex.Message}"; }
            });
        }
    }
}
