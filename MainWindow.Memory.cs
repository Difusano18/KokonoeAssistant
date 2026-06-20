using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KokonoeAssistant.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TgUpdate   = Telegram.Bot.Types.Update;
using WMsgBox    = System.Windows.MessageBox;
using WButton    = System.Windows.Controls.Button;
using WKeyArgs   = System.Windows.Input.KeyEventArgs;
using WDragArgs  = System.Windows.DragEventArgs;
using WClipboard = System.Windows.Clipboard;
using WDataFmts  = System.Windows.DataFormats;
using WTextBox   = System.Windows.Controls.TextBox;
using MediaBrush   = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor   = System.Windows.Media.Color;
using WpfRect      = System.Windows.Shapes.Rectangle;
using WpfFF        = System.Windows.Media.FontFamily;
using WpfSz        = System.Windows.Size;
using WpfOri       = System.Windows.Controls.Orientation;
using WinForms     = System.Windows.Forms;

namespace KokonoeAssistant
{
    public partial class MainWindow
    {
        private void MemTabRefresh_Click(object sender, RoutedEventArgs e) => MemTabRefreshData();

        private void MemTabRefreshData()
        {
            try
            {
                var mem = ServiceContainer.KokoMemory;
                if (mem == null) return;
                var all = mem.Facts;
                var query = MemoryCortexSearchBox?.Text?.Trim() ?? "";
                var filtered = string.IsNullOrWhiteSpace(query)
                    ? all
                    : all.Where(f =>
                        (f.Content ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (f.Category ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                var facts = filtered.OrderByDescending(f => f.Importance).Take(60).ToList();

                MemTabTotalText.Text     = all.Count.ToString();
                MemTabConfirmedText.Text = all.Count(f => f.ConfirmCount > 1).ToString();
                MemTabHighImpText.Text   = all.Count(f => f.Importance >= 0.7f).ToString();
                MemTabCatCountText.Text  = all.Select(f => f.Category).Distinct().Count().ToString();

                MemoryActiveQueryText.Text = string.IsNullOrWhiteSpace(query) ? "\"memory cortex\"" : $"\"{query}\"";
                MemoryCortexSourcesText.Text = $"Vault online\nIndex cache synced\nGraph engine active\nSemantic links {Math.Max(0, facts.Count * 3)}";
                MemoryCortexDensityText.Text = $"density {(all.Count == 0 ? 0 : Math.Min(99, (facts.Count * 100) / Math.Max(1, all.Count)))}%";

                BuildMemoryCortexNodes(facts.Take(26).ToList());
                DrawMemoryCortexGraph();
                SelectMemoryCortexNode(_memoryCortexNodes.OrderByDescending(n => n.Importance).FirstOrDefault());
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MemTab] {ex.Message}"); }
        }

        private void BuildMemoryCortexNodes(IReadOnlyList<KokoMemoryEngine.MemoryFact> facts)
        {
            _memoryCortexNodes.Clear();
            if (facts.Count == 0) return;

            double width = Math.Max(760, MemoryCortexSkia?.ActualWidth ?? 0);
            double height = Math.Max(460, MemoryCortexSkia?.ActualHeight ?? 0);
            double centerX = width / 2;
            double centerY = height / 2;
            double spreadX = width * 0.38 * _memoryCortexZoom;
            double spreadY = height * 0.34 * _memoryCortexZoom;

            var clusters = facts
                .GroupBy(f => string.IsNullOrWhiteSpace(f.Category) ? "general" : f.Category.Trim())
                .OrderByDescending(g => g.Max(f => f.Importance))
                .ToList();

            int index = 0;
            for (int clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                var cluster = clusters[clusterIndex].OrderByDescending(f => f.Importance).ToList();
                double clusterAngle = (Math.PI * 2 * clusterIndex / Math.Max(1, clusters.Count)) - Math.PI / 2;
                double clusterX = centerX + Math.Cos(clusterAngle) * spreadX * 0.55;
                double clusterY = centerY + Math.Sin(clusterAngle) * spreadY * 0.55;

                for (int itemIndex = 0; itemIndex < cluster.Count; itemIndex++)
                {
                    var fact = cluster[itemIndex];
                    double angle = clusterAngle + (itemIndex - (cluster.Count - 1) / 2.0) * 0.42;
                    double orbit = 32 + itemIndex * 18;
                    _memoryCortexNodes.Add(new MemoryCortexNodeVm
                    {
                        Id = $"mem-{index++}",
                        Text = fact.Content ?? "",
                        Category = string.IsNullOrWhiteSpace(fact.Category) ? "general" : fact.Category,
                        Importance = fact.Importance,
                        ConfirmCount = fact.ConfirmCount,
                        Radius = 6 + Math.Clamp(fact.Importance, 0.1f, 1f) * 15 + Math.Min(8, fact.ConfirmCount),
                        X = Math.Clamp(clusterX + Math.Cos(angle) * orbit, 34, width - 34),
                        Y = Math.Clamp(clusterY + Math.Sin(angle) * orbit, 34, height - 34),
                        Color = MemoryCortexColorForCategory(fact.Category)
                    });
                }
            }
        }

        private SKColor MemoryCortexColorForCategory(string? category)
        {
            var key = (category ?? "").ToLowerInvariant();
            if (key.Contains("person")) return new SKColor(255, 45, 117);
            if (key.Contains("project")) return new SKColor(41, 182, 246);
            if (key.Contains("emotion")) return new SKColor(186, 104, 255);
            if (key.Contains("goal")) return new SKColor(0, 230, 118);
            if (key.Contains("world")) return new SKColor(0, 191, 165);
            return new SKColor(126, 87, 194);
        }

        private void DrawMemoryCortexGraph()
        {
            MemoryCortexSkia?.InvalidateVisual();
        }

        private void MemoryCortexSkia_PaintSurface(object? sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var info = e.Info;
            canvas.Clear(SKColors.Transparent);

            using var gridPaint = new SKPaint
            {
                Color = new SKColor(255, 45, 117, 18),
                StrokeWidth = 1,
                IsAntialias = true
            };
            for (int x = 0; x < info.Width; x += 56)
                canvas.DrawLine(x, 0, x, info.Height, gridPaint);
            for (int y = 0; y < info.Height; y += 56)
                canvas.DrawLine(0, y, info.Width, y, gridPaint);

            if (_memoryCortexNodes.Count == 0)
            {
                using var emptyPaint = new SKPaint
                {
                    Color = new SKColor(160, 160, 180),
                    IsAntialias = true
                };
                using var emptyFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
                canvas.DrawText("no memory nodes. tragic, but fixable.", 24, 42, SKTextAlign.Left, emptyFont, emptyPaint);
                return;
            }

            using var edgePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
            using var glowPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10) };
            using var nodePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            using var ringPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f };
            using var labelPaint = new SKPaint
            {
                Color = new SKColor(230, 230, 245),
                IsAntialias = true
            };
            using var subLabelPaint = new SKPaint
            {
                Color = new SKColor(186, 104, 255),
                IsAntialias = true
            };
            using var labelFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 13);
            using var subLabelFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 10);

            for (int i = 0; i < _memoryCortexNodes.Count; i++)
            {
                for (int j = i + 1; j < _memoryCortexNodes.Count; j++)
                {
                    var a = _memoryCortexNodes[i];
                    var b = _memoryCortexNodes[j];
                    bool linked = a.Category == b.Category || (i < 5 && j % 3 == 0);
                    if (!linked) continue;

                    edgePaint.Color = linked && a.Category == b.Category
                        ? a.Color.WithAlpha(72)
                        : new SKColor(120, 90, 170, 32);
                    edgePaint.StrokeWidth = a.Category == b.Category ? 1.25f : 0.75f;
                    canvas.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, edgePaint);
                }
            }

            var active = _memoryCortexNodes.OrderByDescending(n => n.Importance).Take(6).ToList();
            edgePaint.Color = new SKColor(255, 45, 170, 190);
            edgePaint.StrokeWidth = 2.4f;
            for (int i = 0; i < active.Count - 1; i++)
                canvas.DrawLine((float)active[i].X, (float)active[i].Y, (float)active[i + 1].X, (float)active[i + 1].Y, edgePaint);

            foreach (var node in _memoryCortexNodes.OrderBy(n => n.Radius))
            {
                glowPaint.Color = node.Color.WithAlpha((byte)(node.Importance >= 0.8f ? 150 : 80));
                canvas.DrawCircle((float)node.X, (float)node.Y, (float)(node.Radius * 2.15), glowPaint);

                nodePaint.Color = node.Color.WithAlpha(230);
                canvas.DrawCircle((float)node.X, (float)node.Y, (float)node.Radius, nodePaint);

                ringPaint.Color = SKColors.White.WithAlpha((byte)(node.Importance >= 0.8f ? 190 : 90));
                ringPaint.StrokeWidth = node.Importance >= 0.8f ? 1.7f : 0.9f;
                canvas.DrawCircle((float)node.X, (float)node.Y, (float)(node.Radius + 2), ringPaint);

                if (node.Importance >= 0.55f || node.Radius > 18)
                {
                    canvas.DrawText(ShortMemoryLabel(node.Text), (float)(node.X + node.Radius + 8), (float)(node.Y - 2), SKTextAlign.Left, labelFont, labelPaint);
                    canvas.DrawText($"importance {node.Importance:0.00}", (float)(node.X + node.Radius + 8), (float)(node.Y + 12), SKTextAlign.Left, subLabelFont, subLabelPaint);
                }
            }
        }

        private static string ShortMemoryLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "memory.md";
            var trimmed = text.Trim().Replace('\n', ' ');
            return trimmed.Length <= 24 ? trimmed : trimmed[..24] + "...";
        }

        private void SelectMemoryCortexNode(MemoryCortexNodeVm? node)
        {
            if (node == null)
            {
                MemoryInspectorTitle.Text = "select node";
                MemoryInspectorMeta.Text = "type -- / importance -- / confirmations --";
                MemoryInspectorImportanceBar.Value = 0;
                MemoryLinksList.ItemsSource = Array.Empty<string>();
                MemoryActivityFeed.ItemsSource = Array.Empty<string>();
                MemoryThoughtFlow.ItemsSource = Array.Empty<string>();
                return;
            }

            var related = _memoryCortexNodes
                .Where(n => n != node)
                .OrderByDescending(n => n.Category == node.Category)
                .ThenByDescending(n => n.Importance)
                .Take(6)
                .ToList();

            MemoryInspectorTitle.Text = ShortMemoryLabel(node.Text);
            MemoryInspectorMeta.Text = $"type {node.Category} / importance {node.Importance:0.00} / confirmations {node.ConfirmCount}";
            MemoryInspectorImportanceBar.Value = Math.Clamp(node.Importance * 100, 0, 100);
            MemoryLinksList.ItemsSource = related.Select(n => $"- {ShortMemoryLabel(n.Text)}   {n.Importance:0.00}").ToList();
            MemoryActivityFeed.ItemsSource = new[]
            {
                $"{DateTime.Now:HH:mm} {ShortMemoryLabel(node.Text)} accessed",
                $"{DateTime.Now.AddMinutes(-1):HH:mm} linked to {related.FirstOrDefault()?.Category ?? "none"}",
                $"{DateTime.Now.AddMinutes(-2):HH:mm} relation strength recalculated",
                $"{DateTime.Now.AddMinutes(-3):HH:mm} vault sync completed"
            };
            MemoryThoughtFlow.ItemsSource = related.Take(4).Select(n => $"- {ShortMemoryLabel(n.Text)}").Prepend($"- {ShortMemoryLabel(node.Text)}").Append("- response generated").ToList();
        }

        private void MemoryTab_SizeChanged(object sender, SizeChangedEventArgs e) => DrawMemoryCortexGraph();

        private void MemoryCortexSkia_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(MemoryCortexSkia);
            var nearest = _memoryCortexNodes
                .OrderBy(n => Math.Pow(n.X - point.X, 2) + Math.Pow(n.Y - point.Y, 2))
                .FirstOrDefault();
            if (nearest != null) SelectMemoryCortexNode(nearest);
        }

        private void MemoryCortexSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MemoryTab?.Visibility == Visibility.Visible)
                MemTabRefreshData();
        }

        private void MemoryCortexZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _memoryCortexZoom = e.NewValue <= 0 ? 1.0 : e.NewValue;
            DrawMemoryCortexGraph();
        }
    }
}
