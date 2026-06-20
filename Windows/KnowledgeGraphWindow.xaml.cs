using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KokonoeAssistant.Services;
using Color = System.Windows.Media.Color;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;
using IOPath = System.IO.Path;

namespace KokonoeAssistant.Windows
{
    public partial class KnowledgeGraphWindow : Window
    {
        private readonly KnowledgeGraph? _graph;
        private readonly string _vaultPath;

        public KnowledgeGraphWindow()
        {
            InitializeComponent();

            // Get graph from ServiceContainer
            try
            {
                _graph = ServiceContainer.KnowledgeGraph;
                _vaultPath = ServiceContainer.ObsidianMcp?.VaultPath ??
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ObsidianVault");
            }
            catch
            {
                _graph = null;
                _vaultPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ObsidianVault");
            }

            Loaded += KnowledgeGraphWindow_Loaded;
        }

        public KnowledgeGraphWindow(KnowledgeGraph graph, string vaultPath)
        {
            InitializeComponent();
            _graph = graph;
            _vaultPath = vaultPath;

            Loaded += KnowledgeGraphWindow_Loaded;
        }

        private void KnowledgeGraphWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_graph == null)
            {
                MessageBox.Show("Knowledge Graph is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            LoadGraphData();
        }

        private void LoadGraphData()
        {
            if (_graph == null) return;

            // Update stats
            var statsText = $"Nodes: {_graph.Nodes.Count} | Edges: {_graph.Edges.Count}";
            Title = $"🧠 Knowledge Graph - {statsText}";

            // Clear canvas
            GraphCanvas.Children.Clear();

            // Simple force-directed layout
            var nodePositions = CalculateNodePositions();

            // Draw edges first (so they appear behind nodes)
            foreach (var edge in _graph.Edges)
            {
                if (nodePositions.ContainsKey(edge.SourceId) && nodePositions.ContainsKey(edge.TargetId))
                {
                    DrawEdge(nodePositions[edge.SourceId], nodePositions[edge.TargetId], edge);
                }
            }

            // Draw nodes
            foreach (var node in _graph.Nodes.Values)
            {
                if (nodePositions.ContainsKey(node.Id))
                {
                    DrawNode(nodePositions[node.Id], node);
                }
            }
        }

        private Dictionary<string, System.Windows.Point> CalculateNodePositions()
        {
            if (_graph == null) return new Dictionary<string, System.Windows.Point>();

            var positions = new Dictionary<string, System.Windows.Point>();
            var random = new Random(42); // Fixed seed for consistency
            var canvasWidth = GraphCanvas.ActualWidth > 100 ? GraphCanvas.ActualWidth : 800;
            var canvasHeight = GraphCanvas.ActualHeight > 100 ? GraphCanvas.ActualHeight : 500;

            // Center point
            var cx = canvasWidth / 2;
            var cy = canvasHeight / 2;

            // Group nodes by type
            var nodesByType = _graph.Nodes.Values.GroupBy(n => n.Type).ToList();
            var angleStep = 2 * Math.PI / Math.Max(1, nodesByType.Count);

            for (int i = 0; i < nodesByType.Count; i++)
            {
                var group = nodesByType[i];
                var groupAngle = i * angleStep;
                var radius = 150 + i * 50;

                var nodesInGroup = group.ToList();
                var nodeAngleStep = 0.5 / Math.Max(1, nodesInGroup.Count);

                for (int j = 0; j < nodesInGroup.Count; j++)
                {
                    var node = nodesInGroup[j];
                    var angle = groupAngle + j * nodeAngleStep;

                    // Add some randomness
                    var r = radius + random.Next(-30, 30);

                    var x = cx + r * Math.Cos(angle);
                    var y = cy + r * Math.Sin(angle);

                    positions[node.Id] = new System.Windows.Point(x, y);
                }
            }

            return positions;
        }

        private void DrawNode(System.Windows.Point pos, KnowledgeGraph.GraphNode node)
        {
            var color = GetNodeColor(node.Type);
            var size = 15 + Math.Min(20, node.AccessCount * 2);

            // Node circle
            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1
            };

            Canvas.SetLeft(ellipse, pos.X - size / 2);
            Canvas.SetTop(ellipse, pos.Y - size / 2);
            GraphCanvas.Children.Add(ellipse);

            // Node label
            var label = new TextBlock
            {
                Text = node.Label,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xE8)),
                FontSize = 9,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextAlignment = TextAlignment.Center
            };

            Canvas.SetLeft(label, pos.X - 40);
            Canvas.SetTop(label, pos.Y + size / 2 + 2);
            System.Windows.Controls.Panel.SetZIndex(label, 10);
            GraphCanvas.Children.Add(label);

            // Tooltip
            var tooltip = new System.Windows.Controls.ToolTip
            {
                Content = $"{node.Type}: {node.Label}\nConfidence: {node.ConfidenceScore:P0}\nAccessed: {node.AccessCount}x"
            };
            ellipse.ToolTip = tooltip;
        }

        private void DrawEdge(System.Windows.Point from, System.Windows.Point to, KnowledgeGraph.GraphEdge edge)
        {
            var line = new Line
            {
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)),
                StrokeThickness = 1 + edge.Weight
            };

            GraphCanvas.Children.Add(line);
        }

        private Color GetNodeColor(string type)
        {
            return type?.ToLower() switch
            {
                "fact" => Color.FromRgb(0xFF, 0x6B, 0x35),      // Orange
                "note" => Color.FromRgb(0x00, 0xCC, 0xFF),      // Cyan
                "concept" => Color.FromRgb(0x00, 0xCC, 0x99),  // Teal
                "goal" => Color.FromRgb(0xFF, 0xCC, 0x00),     // Gold
                "person" => Color.FromRgb(0xCC, 0x66, 0xFF),   // Purple
                "project" => Color.FromRgb(0x66, 0x99, 0xFF),   // Blue
                _ => Color.FromRgb(0x99, 0x99, 0x99)           // Gray
            };
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadGraphData();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null) return;

            try
            {
                var dotContent = _graph.GetGraphVizDot(100);
                var outputPath = IOPath.Combine(_vaultPath, $"graph_export_{DateTime.Now:yyyy-MM-dd_HH-mm}.dot");
                File.WriteAllText(outputPath, dotContent);

                MessageBox.Show($"Graph exported to:\n{outputPath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                try { Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true }); } catch (Exception suppressedEx228) { KokoSystemLog.Write("KNOWLEDGEGRAPHWINDOW.XAML-CATCH", "ExportButton_Click failed near source line 228: " + suppressedEx228); }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
