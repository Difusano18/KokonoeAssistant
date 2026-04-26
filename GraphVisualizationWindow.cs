using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;
using IOPath = System.IO.Path;

namespace KokonoeAssistant
{
    public partial class GraphVisualizationWindow : Window
    {
        private readonly KnowledgeGraph _graph;
        private readonly string _vaultPath;

        public GraphVisualizationWindow(KnowledgeGraph graph, string vaultPath)
        {
            _graph = graph;
            _vaultPath = vaultPath;

            Title = "Knowledge Graph";
            Width = 1000;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0B, 0x18));
            ResizeMode = ResizeMode.CanResize;
            WindowStyle = WindowStyle.SingleBorderWindow;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var mainPanel = new DockPanel { LastChildFill = true };

            // Top panel
            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8), Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x1C)) };

            var stats = new TextBlock
            {
                Text = $"Nodes: {_graph.Nodes.Count} | Edges: {_graph.Edges.Count}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xE8)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var exportBtn = new Button
            {
                Content = "Export DOT",
                Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x08, 0x08, 0x10)),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(5, 0, 5, 0),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            exportBtn.Click += (_, _) => ExportGraph();

            var refreshBtn = new Button
            {
                Content = "Refresh",
                Background = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0xBB)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(12, 5, 12, 5),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            refreshBtn.Click += (_, _) => RefreshVisualization();

            topPanel.Children.Add(stats);
            topPanel.Children.Add(exportBtn);
            topPanel.Children.Add(refreshBtn);

            DockPanel.SetDock(topPanel, Dock.Top);
            mainPanel.Children.Add(topPanel);

            // Main canvas
            var canvas = new Canvas
            {
                Background = new SolidColorBrush(Color.FromRgb(0x08, 0x08, 0x14))
            };

            // Simple visualization: display nodes and edges as text
            var textPanel = new StackPanel { Margin = new Thickness(20) };
            var scroll = new ScrollViewer { Content = textPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            var title = new TextBlock
            {
                Text = "Knowledge Graph Structure",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            textPanel.Children.Add(title);

            // Show node types summary
            foreach (var nodeType in new[] { "fact", "note", "concept", "goal", "person", "project" })
            {
                var nodes = _graph.Nodes.Values
                    .Where(n => n.Type == nodeType)
                    .ToList();
                if (nodes.Count > 0)
                {
                    var typeTitle = new TextBlock
                    {
                        Text = $"\n## {nodeType}s ({nodes.Count})",
                        Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0xBB)),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 5, 0, 3),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas")
                    };
                    textPanel.Children.Add(typeTitle);

                    foreach (var node in nodes)
                    {
                        var nodeText = new TextBlock
                        {
                            Text = $"• {node.Label} (accessed: {node.AccessCount}x, confidence: {node.ConfidenceScore:P0})",
                            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xE8)),
                            FontSize = 10,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(15, 2, 0, 2),
                            FontFamily = new System.Windows.Media.FontFamily("Consolas")
                        };
                        textPanel.Children.Add(nodeText);
                    }
                }
            }

            mainPanel.Children.Add(scroll);
            Content = mainPanel;
        }

        private void ExportGraph()
        {
            try
            {
                var dotContent = _graph.GetGraphVizDot(100);
                var outputPath = IOPath.Combine(_vaultPath, $"graph_export_{DateTime.Now:yyyy-MM-dd_HH-mm}.dot");
                File.WriteAllText(outputPath, dotContent);

                MessageBox.Show($"Graph exported to:\n{outputPath}\n\nUse Graphviz to visualize.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Try to open with default app
                try { Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true }); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshVisualization()
        {
            // Reload and show updated graph
            MessageBox.Show($"Graph updated:\nNodes: {_graph.Nodes.Count}\nEdges: {_graph.Edges.Count}", "Graph Info");
        }
    }
}
