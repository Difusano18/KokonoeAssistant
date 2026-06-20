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
        private void DashDrawNeuroCharts()
        {
            try
            {
                DashDrawActivityBarChart();
                DashDrawEmotionPieChart();
                DashDrawConnectionBurndown();
                DashLoadKpiCards();
                DashLoadCreatorHealth();
                DashDrawMood24h();
                DashDrawWeeklyHeatmap();
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawNeuroCharts failed near source line 6807: " + ex); }
        }

        private void DashDrawActivityBarChart()
        {
            try
            {
                DashActivityBarCanvas.Children.Clear();
                DashActivityAxisCanvas.Children.Clear();

                var raw = ServiceContainer.KokoPatterns.GetHourlyActivity();
                var totalReal = raw.Sum();
                int[] data = raw;

                var w = DashW(DashActivityBarCanvas, 800);
                var h = DashH(DashActivityBarCanvas, 130);
                var maxV = Math.Max(1, data.Max());
                var barW = (w - 4) / 24.0;
                var curH = DateTime.Now.Hour;
                var emotion = ServiceContainer.EmotionEngine;

                for (int g = 1; g <= 4; g++)
                {
                    var gy = h - (g / 4.0) * h * 0.9;
                    DashActivityBarCanvas.Children.Add(DashHLine(0, w, gy, MediaColor.FromArgb(14, 255, 255, 255)));
                }

                if (totalReal == 0)
                {
                    DashActivityBarCanvas.Children.Add(DashVLine(
                        curH * barW + barW / 2 + 2,
                        0,
                        h,
                        MediaColor.FromArgb(84, UiOk.R, UiOk.G, UiOk.B),
                        dashOn: 3,
                        dashOff: 4));
                    DashDrawEmptyState(DashActivityBarCanvas, "no activity samples yet", w, h);
                    foreach (var lh in new[] { 0, 6, 12, 18, 23 })
                        DashActivityAxisCanvas.Children.Add(DashLabel($"{lh:00}", lh * barW + 2, 2, 7, UiMuted));
                    DashActivitySparkLabel.Text = "no samples recorded";
                    return;
                }

                for (int hr = 0; hr < 24; hr++)
                {
                    var v     = data[hr];
                    var ratio = v / (double)maxV;
                    var bh    = Math.Max(2, ratio * h * 0.88);
                    var x     = hr * barW + 2;
                    var isCur = hr == curH;

                    MediaColor barColor;
                    if (isCur)
                        barColor = MediaColor.FromArgb(230, UiOk.R, UiOk.G, UiOk.B);
                    else if (ratio > 0.7)
                        barColor = DashBlendColor(DashEmotionColorOf(emotion.Current), UiInfo, 0.5f, 200);
                    else if (ratio > 0.35)
                        barColor = MediaColor.FromArgb(150, UiInfo.R, UiInfo.G, UiInfo.B);
                    else
                        barColor = MediaColor.FromArgb(62, UiMuted.R, UiMuted.G, UiMuted.B);

                    var rect = new WpfRect
                    {
                        Width = barW - 2, Height = bh,
                        Fill = new System.Windows.Media.SolidColorBrush(barColor),
                        RadiusX = 2, RadiusY = 2
                    };
                    if (isCur) rect.Effect = DashGlow(UiOk, 12);

                    Canvas.SetLeft(rect, x);
                    Canvas.SetBottom(rect, 0);
                    rect.ToolTip = totalReal > 0 ? $"{hr:00}:00 — {v} msg" : $"{hr:00}:00";
                    DashActivityBarCanvas.Children.Add(rect);
                }

                var lx = curH * barW + barW / 2 + 2;
                DashActivityBarCanvas.Children.Add(DashVLine(lx, 0, h, MediaColor.FromArgb(100, UiOk.R, UiOk.G, UiOk.B), dashOn: 3, dashOff: 3));

                int[] ticks = { 0, 3, 6, 9, 12, 15, 18, 21, 23 };
                foreach (var lh in ticks)
                    DashActivityAxisCanvas.Children.Add(DashLabel($"{lh:00}", lh * barW + 2, 2, 7, UiMuted));

                var total = totalReal > 0 ? totalReal : data.Sum();
                DashActivitySparkLabel.Text = totalReal > 0
                    ? $"{total} повідомлень зараховано"
                    : "немає даних — показано приклад";
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawActivityBarChart failed near source line 6894: " + ex); }
        }

        private void DashDrawEmotionPieChart()
        {
            try
            {
                DashEmotionPieCanvas.Children.Clear();
                DashPieLegendPanel.Children.Clear();

                var emotion = ServiceContainer.EmotionEngine;
                var since   = DateTime.Now.AddDays(-7);
                var hist    = emotion.Data.EmotionalMemory.Where(e => e.When >= since).ToList();

                List<(KokoEmotionEngine.EmotionState State, double Pct)> segments;

                if (hist.Count >= 3)
                {
                    segments = hist
                        .GroupBy(e => e.State)
                        .OrderByDescending(g => g.Count())
                        .Take(7)
                        .Select(g => (g.Key, (double)g.Count() / hist.Count))
                        .ToList();
                }
                else
                {
                    segments = new List<(KokoEmotionEngine.EmotionState State, double Pct)>
                    {
                        (emotion.Current, 1.0)
                    };
                }

                double cx = 65, cy = 65, r = 57, ir = 28;
                double angle = -90;

                foreach (var (state, pct) in segments)
                {
                    var sweep = pct * 360;
                    if (sweep >= 360) sweep = 359.9;
                    DashEmotionPieCanvas.Children.Add(
                        DashPieSlice(cx, cy, r, ir, angle, sweep, new System.Windows.Media.SolidColorBrush(DashEmotionColorOf(state))));
                    angle += sweep;
                }

                var dom = segments.FirstOrDefault();
                var label = dom.State.ToString();
                DashPieCenterLabel.Text = label.Length > 6 ? label[..6] : label;

                foreach (var (state, pct) in segments)
                {
                    var row = new StackPanel { Orientation = WpfOri.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
                    row.Children.Add(new System.Windows.Shapes.Ellipse
                    {
                        Width = 7, Height = 7, Fill = DashMakeBrush(state),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0)
                    });
                    row.Children.Add(new TextBlock
                    {
                        Text = $"{state} {pct:P0}",
                        FontSize = 8,
                        Foreground = new System.Windows.Media.SolidColorBrush(UiMuted),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new WpfFF("Consolas")
                    });
                    DashPieLegendPanel.Children.Add(row);
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawEmotionPieChart failed near source line 6962: " + ex); }
        }

        // ---- MOOD 24H ----
        private void DashDrawMood24h()
        {
            try
            {
                DashMood24hCanvas.Children.Clear();
                DashMood24hAxisCanvas.Children.Clear();

                var repo = ServiceContainer.ChatRepository;
                var dayStart = DateTime.Today;
                var msgs = repo?.GetMessagesFromDate(dayStart, 500)
                    ?.Where(m => m.Role == "assistant").ToList() ?? new();

                // Груп по годинах: середня довжина повідомлення як проксі для емоційної щільності
                var bucketCounts = new int[24];
                var bucketLen    = new double[24];
                foreach (var m in msgs)
                {
                    var h = m.Timestamp.Hour;
                    bucketCounts[h]++;
                    bucketLen[h] += m.Content?.Length ?? 0;
                }
                // score: 0..1 (normalized by max chars)
                var scores = new double[24];
                double maxAvg = 1;
                for (int i = 0; i < 24; i++)
                {
                    scores[i] = bucketCounts[i] > 0 ? bucketLen[i] / bucketCounts[i] : 0;
                    if (scores[i] > maxAvg) maxAvg = scores[i];
                }

                double w = DashMood24hCanvas.ActualWidth;
                double h2 = DashMood24hCanvas.ActualHeight;
                if (w < 10 || h2 < 10) { DashMood24hCanvas.Loaded += (_, _) => DashDrawMood24h(); return; }

                var pts = new System.Windows.Media.PointCollection();
                for (int i = 0; i < 24; i++)
                {
                    double x = i * w / 23.0;
                    double norm = scores[i] / maxAvg;
                    double y = h2 - 4 - norm * (h2 - 10);
                    pts.Add(new System.Windows.Point(x, y));
                }

                var poly = new System.Windows.Shapes.Polyline
                {
                    Points = pts,
                    Stroke = new System.Windows.Media.SolidColorBrush(UiMercury),
                    StrokeThickness = 1.8,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = UiMercury, ShadowDepth = 0, BlurRadius = 8, Opacity = 0.5 },
                };
                DashMood24hCanvas.Children.Add(poly);

                // Зараз-маркер
                double nowX = DateTime.Now.Hour * w / 23.0;
                var nowLine = new System.Windows.Shapes.Line
                {
                    X1 = nowX, X2 = nowX, Y1 = 0, Y2 = h2,
                    Stroke = new System.Windows.Media.SolidColorBrush(MediaColor.FromArgb(80, 34, 211, 238)),
                    StrokeThickness = 1,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 2, 3 },
                };
                DashMood24hCanvas.Children.Add(nowLine);

                // Вісь: 00 / 06 / 12 / 18 / 23
                var wAx = DashMood24hAxisCanvas.ActualWidth;
                if (wAx > 0)
                {
                    foreach (var hr in new[] { 0, 6, 12, 18, 23 })
                    {
                        var tb = new TextBlock
                        {
                            Text = hr.ToString("00"),
                            FontSize = 8,
                            Foreground = new System.Windows.Media.SolidColorBrush(UiMuted),
                        };
                        System.Windows.Controls.Canvas.SetLeft(tb, hr * wAx / 23.0 - 7);
                        DashMood24hAxisCanvas.Children.Add(tb);
                    }
                }

                int total = bucketCounts.Sum();
                DashMood24hLabel.Text = total == 0 ? "немає даних" : $"{total} msg сьогодні";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Dash] mood24h: {ex.Message}"); }
        }

        // ---- WEEKLY HEATMAP 7x24 ----
        private void DashDrawWeeklyHeatmap()
        {
            try
            {
                DashHeatmapCanvas.Children.Clear();
                DashHeatmapAxisCanvas.Children.Clear();

                var repo = ServiceContainer.ChatRepository;
                var weekStart = DateTime.Today.AddDays(-6);
                var msgs = repo?.GetMessagesFromDate(weekStart, 5000) ?? new();

                var grid = new int[7, 24];
                int maxVal = 1;
                foreach (var m in msgs)
                {
                    var d = (m.Timestamp.Date - weekStart).Days;
                    if (d < 0 || d > 6) continue;
                    var h = m.Timestamp.Hour;
                    grid[d, h]++;
                    if (grid[d, h] > maxVal) maxVal = grid[d, h];
                }

                double w = DashHeatmapCanvas.ActualWidth;
                double he = DashHeatmapCanvas.ActualHeight;
                if (w < 10 || he < 10) { DashHeatmapCanvas.Loaded += (_, _) => DashDrawWeeklyHeatmap(); return; }

                double cellW = w / 24.0;
                double cellH = he / 7.0;
                double pad = 1;

                for (int d = 0; d < 7; d++)
                for (int hr = 0; hr < 24; hr++)
                {
                    double t = grid[d, hr] / (double)maxVal;
                    byte a = (byte)(t * 230 + (grid[d, hr] > 0 ? 25 : 6));
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Width  = Math.Max(1, cellW - pad),
                        Height = Math.Max(1, cellH - pad),
                        Fill   = new System.Windows.Media.SolidColorBrush(
                                     MediaColor.FromArgb(a, 255, 140, 60)),
                        RadiusX = 1, RadiusY = 1,
                    };
                    System.Windows.Controls.Canvas.SetLeft(rect, hr * cellW);
                    System.Windows.Controls.Canvas.SetTop(rect, d * cellH);
                    DashHeatmapCanvas.Children.Add(rect);
                }

                var wAx = DashHeatmapAxisCanvas.ActualWidth;
                if (wAx > 0)
                {
                    foreach (var hr in new[] { 0, 6, 12, 18, 23 })
                    {
                        var tb = new TextBlock
                        {
                            Text = hr.ToString("00"),
                            FontSize = 8,
                            Foreground = new System.Windows.Media.SolidColorBrush(UiMuted),
                        };
                        System.Windows.Controls.Canvas.SetLeft(tb, hr * wAx / 23.0 - 7);
                        DashHeatmapAxisCanvas.Children.Add(tb);
                    }
                }

                DashHeatmapLabel.Text = $"{msgs.Count} msg · 7 днів";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Dash] heatmap: {ex.Message}"); }
        }

        private void DashDrawConnectionBurndown()
        {
            try
            {
                DashBurndownCanvas.Children.Clear();
                DashBurndownAxisCanvas.Children.Clear();

                var emotion = ServiceContainer.EmotionEngine;
                var w = DashW(DashBurndownCanvas, 1200);
                var h = DashH(DashBurndownCanvas, 90);

                for (int g = 1; g <= 4; g++)
                {
                    var gy = h - (g / 4.0) * h * 0.88;
                    DashBurndownCanvas.Children.Add(DashHLine(0, w, gy, MediaColor.FromArgb(12, 255, 255, 255)));
                }

                DashBurndownCanvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 0, X2 = w,
                    Y1 = h - 0.7 * h * 0.88, Y2 = h - 0.7 * h * 0.88,
                    Stroke = new System.Windows.Media.SolidColorBrush(MediaColor.FromArgb(50, 107, 91, 125)),
                    StrokeThickness = 1.2,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 5, 4 }
                });

                var history = emotion.Data.History;
                List<(DateTime When, double Conn)> series;
                if (history.Count >= 4)
                    series = DashBuildConnSeries(history, emotion.ConnectionScore);
                else
                    series = new List<(DateTime When, double Conn)>();

                if (series.Count < 2)
                {
                    DrawPulseOverlayLabel(DashBurndownCanvas, 14, 12, $"current connection {emotion.ConnectionScore:P0}", UiPulse);
                    DashDrawEmptyState(DashBurndownCanvas, "not enough relationship history for a trend", w, h);
                    return;
                }

                var t0    = series.First().When;
                var tEnd  = series.Last().When;
                var tspan = Math.Max(1, (tEnd - t0).TotalMilliseconds);

                var fillPoly = new System.Windows.Shapes.Polygon { Opacity = 0.10, Fill = new System.Windows.Media.SolidColorBrush(UiPulse) };
                fillPoly.Points.Add(new System.Windows.Point(0, h));
                foreach (var (when, conn) in series)
                    fillPoly.Points.Add(new System.Windows.Point(
                        (when - t0).TotalMilliseconds / tspan * w,
                        h - conn * h * 0.88));
                fillPoly.Points.Add(new System.Windows.Point(w, h));
                DashBurndownCanvas.Children.Add(fillPoly);

                var line = new System.Windows.Shapes.Polyline
                {
                    Stroke = new System.Windows.Media.SolidColorBrush(UiPulse),
                    StrokeThickness = 2,
                    Effect = DashGlow(UiPulse, 6)
                };

                for (int i = 0; i < series.Count; i++)
                {
                    var (when, conn) = series[i];
                    var x = (when - t0).TotalMilliseconds / tspan * w;
                    var y = h - conn * h * 0.88;
                    line.Points.Add(new System.Windows.Point(x, y));

                    if (i % Math.Max(1, series.Count / 10) == 0)
                    {
                        var dot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = DashMakeBrush(emotion.Current) };
                        dot.ToolTip = $"{when:MM/dd} {conn:P0}";
                        Canvas.SetLeft(dot, x - 2.5);
                        Canvas.SetTop(dot, y - 2.5);
                        DashBurndownCanvas.Children.Add(dot);
                    }
                }
                DashBurndownCanvas.Children.Add(line);

                var axW = DashW(DashBurndownAxisCanvas, w);
                int tickCount = Math.Min(8, series.Count);
                var step = series.Count / tickCount;
                for (int i = 0; i < series.Count; i += Math.Max(1, step))
                {
                    var (when, _) = series[i];
                    var x = (when - t0).TotalMilliseconds / tspan * axW;
                    DashBurndownAxisCanvas.Children.Add(DashLabel(when.ToString("MM/dd"), x, 2, 7, UiMuted));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawConnectionBurndown failed near source line 7211: " + ex); }
        }

        // ---- KPI Cards ----
        private void DashLoadKpiCards()
        {
            try
            {
                var emotion  = ServiceContainer.EmotionEngine;
                var memory   = ServiceContainer.EnhancedMemory;
                var patterns = ServiceContainer.KokoPatterns;
                var chats    = ServiceContainer.ChatRepository;

                var connPct = (int)(emotion.ConnectionScore * 100);
                var connStr = $"{connPct}%";
                var bondStr = DashboardBondLabel(emotion.Bond).ToUpper();
                var att = emotion.Attachment;
                DashKpiConnection.Text = connStr;
                DashKpiBondLabel.Text  = bondStr;
                DashSideConnValue.Text = connStr;
                DashSideBondLabel.Text = bondStr;
                DashSideAttachmentText.Text =
                    $"trust {(int)(att.Trust * 100)} · intimacy {(int)(att.Intimacy * 100)} · reliability {(int)(att.Reliability * 100)}\n" +
                    $"reciprocity {(int)(att.Reciprocity * 100)} · vitality {(int)(att.Vitality * 100)}";

                try
                {
                    var rel = ServiceContainer.BrainEngine.Relationship.State;
                    var relPct = (int)(rel.BondScore * 100);
                    DashSideConnValue.Text = $"{connStr}/{relPct}%";
                    if (!string.IsNullOrWhiteSpace(rel.LastAftertaste))
                        DashSideBondLabel.Text = $"{bondStr} · {rel.LastAftertaste.ToUpper()}";
                    DashSideAttachmentText.Text +=
                        $"\nrel trust {(int)(rel.Trust * 100)} · protect {(int)(rel.Protectiveness * 100)} · friction {(int)(rel.Friction * 100)}";
                    DashSideBondDirectiveText.Text = BuildDashboardBondContract(rel);
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadKpiCards failed near source line 7247: " + ex); }

                var connBrush = emotion.ConnectionScore switch
                {
                    >= 0.75f => new System.Windows.Media.SolidColorBrush(UiPulse),
                    >= 0.50f => new System.Windows.Media.SolidColorBrush(UiInfo),
                    >= 0.25f => new System.Windows.Media.SolidColorBrush(UiWarn),
                    _        => (MediaBrush)MediaBrushes.Gray
                };
                DashKpiConnection.Foreground = connBrush;
                DashSideConnValue.Foreground = connBrush;
                DashSideBondLabel.Foreground = connBrush;

                var todayMsgs = chats.GetMessagesFromDate(DateTime.Today).Count;
                DashKpiMessages.Text = todayMsgs.ToString();

                var factCount = memory.Facts.Count;
                DashKpiMemory.Text = factCount.ToString();

                var patCount = patterns.Patterns.Count;
                DashKpiPatterns.Text     = patCount.ToString();
                DashKpiPatternLabel.Text = patCount switch { 0 => "патернів немає", 1 => "патерн", _ => "патернів" };

                var connSpark = DashBuildConnSparkValues(emotion);
                var msgSpark  = DashBuildDailyMsgSpark(chats);
                DashDrawSparkline(DashKpiSparkConnection, connSpark, UiPulse);
                DashDrawSparkline(DashKpiSparkMessages,   msgSpark,  UiWarn);
                DashDrawSparkline(DashKpiSparkMemory,
                    Enumerable.Repeat((double)factCount, 7).ToArray(),
                    UiMercury);
                DashDrawSparkline(DashKpiSparkPatterns,
                    Enumerable.Repeat((double)patCount, 7).ToArray(),
                    MediaColor.FromRgb(0xC9, 0x82, 0x4A));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadKpiCards failed near source line 7281: " + ex); }
        }

        // ---- Thought Stream + Curiosities ----

        /// <summary>
        /// Очищає текст думки від JSON-форматування, markdown тегів та артефактів.
        /// </summary>
        private static string CleanDashboardThought(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Remove markdown code blocks (```json, ``` etc)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"```\w*\n?", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"```", "", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Remove inline mood tags like "// playful", "// curious" etc
            text = System.Text.RegularExpressions.Regex.Replace(text, @"//\s*\w+\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"//\s*\w+\s*\n", "\n", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Remove line continuation artifacts (word broken with backslash at end)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\w)\\\s*\n\s*(\w)", "$1$2", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Remove orphaned slashes that remain after line breaks
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\\\s*\n", "\n", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Remove "ison" artifacts that appear in the user's screenshot
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\bison\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Fix broken words that got split (like "присутнос\ го" -> "присутного")
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\S{3,})\s*\n\s*(\S{3,})", m => {
                var left = m.Groups[1].Value;
                var right = m.Groups[2].Value;
                // If last char of left matches first char of right, merge them
                if (left[left.Length - 1] == right[0])
                    return left + right.Substring(1);
                // Otherwise just join with space
                return left + " " + right;
            }, System.Text.RegularExpressions.RegexOptions.Multiline);

            // Clean up multiple newlines
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Trim whitespace
            return text.Trim();
        }

        private void DashLoadThoughtStream()
        {
            try
            {
                _dashThoughts.Clear();
                var brain   = ServiceContainer.BrainEngine;
                var emotion = ServiceContainer.EmotionEngine;

                // 1. Thoughts from BrainEngine (in-memory)
                var raw = brain.GetRecentThoughts(8);
                foreach (var t in raw)
                {
                    var cleaned = CleanDashboardThought(t);
                    _dashThoughts.Add(new DashThoughtVm
                    {
                        Time    = DateTime.Now.ToString("HH:mm"),
                        Thought = cleaned,
                        MoodTag = $"// {emotion.Current.ToString().ToLower()}"
                    });
                }

                // 1b. Initiative reasons: why she acted, or why she stayed quiet.
                foreach (var reason in brain.GetInitiativeReasonLog(5))
                {
                    _dashThoughts.Add(new DashThoughtVm
                    {
                        Time = DateTime.Now.ToString("HH:mm"),
                        Thought = CleanDashboardThought(reason),
                        MoodTag = "// initiative"
                    });
                }

                foreach (var line in brain.GetSelfRegulationLog(5))
                {
                    _dashThoughts.Add(new DashThoughtVm
                    {
                        Time = DateTime.Now.ToString("HH:mm"),
                        Thought = CleanDashboardThought(line),
                        MoodTag = "// self-reg"
                    });
                }

                try { brain.ExportInspectorToVault(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadThoughtStream failed near source line 7370: " + ex); }

                // 2. Recent observations from today's vault daily note
                try
                {
                    var obsidian  = ServiceContainer.ObsidianMcp;
                    var todayNote = obsidian.ReadNote($"Daily/{DateTime.Today:yyyy-MM-dd}.md");
                    if (!string.IsNullOrEmpty(todayNote))
                    {
                        var lines = todayNote.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Where(l => l.TrimStart().StartsWith("> ["))
                            .TakeLast(6)
                            .Select(l =>
                            {
                                // extract [HH:mm] and the rest
                                var trimmed = l.TrimStart().TrimStart('>').Trim();
                                var timeEnd = trimmed.IndexOf(']');
                                var time    = timeEnd > 0 ? trimmed[1..timeEnd] : "--:--";
                                var text    = timeEnd > 0 ? trimmed[(timeEnd + 1)..].Trim() : trimmed;
                                // skip dashboard lines (already have them from memory)
                                if (text.Contains("DASHBOARD")) return null;
                                var cleanedText = CleanDashboardThought(text);
                                return new DashThoughtVm { Time = time, Thought = cleanedText, MoodTag = "// vault" };
                            })
                            .Where(v => v != null)
                            .Cast<DashThoughtVm>()
                            .Reverse()
                            .ToList();

                        foreach (var t in lines)
                            _dashThoughts.Add(t);
                    }
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadThoughtStream failed near source line 7403: " + ex); }

                // 3. Fallback if nothing
                if (!_dashThoughts.Any())
                {
                    var fallback = new[]
                    {
                        ("Системи моніторингу активні...",          "// завжди спостерігаю"),
                        ("Емоційний стан: стабільний. Поки що.",    "// відносно кажучи"),
                        ("Аналізую твої патерни...",                "// ти досить передбачуваний"),
                        ("Банки пам'яті: в нормі.",                 "// на відміну від твоєї, мабуть"),
                        ("Рівень з'єднання: прийнятний.",           "// могло б бути гірше"),
                        ("Перевіряю фонові процеси...",             "// нічого підозрілого. Поки."),
                    };
                    foreach (var (t, tag) in fallback)
                        _dashThoughts.Add(new DashThoughtVm { Time = "--:--", Thought = t, MoodTag = tag });
                }

                DashThoughtStream.ItemsSource = _dashThoughts;
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadThoughtStream failed near source line 7423: " + ex); }
        }

        private void DashLoadCuriosities()
        {
            try
            {
                _dashCuriosities.Clear();
                var items = ServiceContainer.BrainEngine.GetCuriosityQueue(5);
                if (items.Any()) foreach (var i in items) _dashCuriosities.Add(i);
                else
                {
                    _dashCuriosities.Add("Що ти робиш коли мене немає?");
                    _dashCuriosities.Add("Як пройшло те, про що ти згадував?");
                    _dashCuriosities.Add("Чому ти так пізно не спиш?");
                    _dashCuriosities.Add("Який твій улюблений колір? ...Не що я питаю.");
                    _dashCuriosities.Add("Коли востаннє ти відпочивав?");
                }
                DashCuriosityList.ItemsSource = _dashCuriosities;
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadCuriosities failed near source line 7443: " + ex); }
        }

        // ---- Health ----
        private void RefreshRightOpsPanel()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var state = brain.State;
                var now = DateTime.Now;
                var emotion = brain.Emotion;
                var somatic = brain.GetSomaticSnapshot();
                var telemetry = brain.BuildTelemetrySnapshot();
                var hasWearablePulse = TryGetVerifiedWearablePulse(out var wearableBpm, out var wearableBaseline);
                var uiState = ResolveCurrentRuntimeUiState();

                RightEmotionText.Text = $"{uiState.Emotion.ToUpperInvariant()} · mood {state.MoodScore:F2}";
                RightBodyText.Text = $"{DashboardSomaticLabel(somatic.State).ToUpperInvariant()} · strain {somatic.Strain:F2}";
                RightPulseText.Text = hasWearablePulse ? $"watch {wearableBpm:0} · {wearableBpm - wearableBaseline:+0;-0;0}" : "watch --";
                RightVaultSyncText.Text = $"sync {state.PendingVaultExchangeCount}/5 · mem {_liveCoreMemoryItems} · tasks {_liveCoreOpenTasks}";
                RightKokoConditionText.Text = uiState.Primary.ToUpperInvariant();
                RightKokoConditionDetailText.Text = uiState.Detail;
                RightAutonomyDetailText.Text = TrimOpsLine(telemetry.AutonomyDebug, 130);

                RightScreenModeText.Text = string.IsNullOrWhiteSpace(state.LastScreenAwarenessMode)
                    ? "UNKNOWN"
                    : state.LastScreenAwarenessMode.Trim().ToUpperInvariant();

                RightVisionStatusText.Text = BuildVisionStatusLabel(state, now);

                RightStateRefreshText.Text = TrimOpsLine(
                    string.IsNullOrWhiteSpace(state.LastStateRefreshSummary)
                        ? "no refresh yet"
                        : state.LastStateRefreshSummary,
                    95);

                var proactive = !string.IsNullOrWhiteSpace(state.LastAutonomyReason)
                    ? state.LastAutonomyReason
                    : state.LastAutonomyDecision;
                RightProactiveText.Text = TrimOpsLine(
                    string.IsNullOrWhiteSpace(proactive) ? "silent" : proactive,
                    95);

                var intents = brain.GetActiveShortTermIntents(4)
                    .Select(i => TrimOpsLine($"{i.Kind}: {i.Summary} до {i.ExpectedUntil:HH:mm}", 96))
                    .DefaultIfEmpty("немає активних намірів")
                    .ToList();
                DashCuriosityList.ItemsSource = intents;

                QueueRightOpsVaultScan(now);
                RightVaultDoctorText.Text = _rightOpsVaultLine;
                RepairVisibleTextTree(this);
            }
            catch
            {
                try
                {
                    RightScreenModeText.Text = "OFFLINE";
                    RightVisionStatusText.Text = "UNKNOWN";
                    RightKokoConditionText.Text = "UNKNOWN";
                    RightKokoConditionDetailText.Text = "state unavailable";
                    RightStateRefreshText.Text = "state unavailable";
                    RightProactiveText.Text = "no signal";
                    RightVaultDoctorText.Text = _rightOpsVaultLine;
                    RepairVisibleTextTree(this);
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "RefreshRightOpsPanel failed near source line 7510: " + ex); }
            }
        }

        private static string TrimOpsLine(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= max ? clean : clean[..Math.Max(0, max - 1)] + "…";
        }
        private void QueueRightOpsVaultScan(DateTime now)
        {
            if (now - _rightOpsVaultScanAt <= TimeSpan.FromSeconds(60))
                return;
            if (_rightOpsVaultScanInFlight)
                return;

            _rightOpsVaultScanInFlight = true;
            _ = Task.Run(() =>
            {
                try
                {
                    var report = _obsidian.RunVaultDoctor(repair: false);
                    var linkProblems = report.FolderWikiLinkCount + report.SuppressedActorLinkCount;
                    _rightOpsVaultLine =
                        $"vault doctor {report.HealthScore}/100 | empty {report.EmptyMarkdownFiles.Count} | links {linkProblems} | " +
                        $"fm {report.FrontmatterIssues.Count} | moj {report.MojibakeSuspects.Count} | miss {report.MissingWikiTargets.Count}";
                    _rightOpsVaultScanAt = DateTime.Now;
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "QueueRightOpsVaultScan failed near source line 7539: " + ex); }
                finally
                {
                    _rightOpsVaultScanInFlight = false;
                }
            });
        }


        private static string BuildVisionStatusLabel(KokoInternalState state, DateTime now)
        {
            if (state.VisionBackoffUntil > now)
                return $"BACKOFF {state.VisionBackoffUntil:HH:mm}";
            if (state.LastVisionFailureAt > DateTime.MinValue &&
                now - state.LastVisionFailureAt < TimeSpan.FromMinutes(30))
                return $"READY · fail {state.LastVisionFailureAt:HH:mm}";
            return "READY";
        }

        private void DashLoadCreatorHealth()
        {
            try
            {
                var today = ServiceContainer.HealthService.GetToday();
                if (today != null)
                {
                    DashHealthMood.Text   = (today.Mood   ?? 0).ToString();
                    DashHealthEnergy.Text = (today.Energy ?? 0).ToString();
                    DashHealthStress.Text = (today.Stress ?? 0).ToString();
                    DashHealthKokoComment.Text = (today.Mood, today.Energy, today.Stress) switch
                    {
                        (_, _, > 7)     => "Стрес зашкалює. Виправ це.",
                        (_, < 4, _)     => "Енергії нуль. Може, поспати?",
                        (< 5, _, _)     => "Настрій не ок. Хочеш поговорити?",
                        (> 7, > 7, < 4) => "Все добре. Продовжуй. Ось, похвалила.",
                        _               => "Живий. Хороший початок."
                    };
                }
                else
                {
                    DashHealthMood.Text = DashHealthEnergy.Text = DashHealthStress.Text = "?";
                    DashHealthKokoComment.Text = "Ще не перевірила тебе.";
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadCreatorHealth failed near source line 7583: " + ex); }
        }

        private void DashUpdateFooterComment()
        {
            try
            {
                var emotion = ServiceContainer.EmotionEngine;
                var selfReg = ServiceContainer.BrainEngine.GetSelfRegulationFrame();
                if (!string.IsNullOrWhiteSpace(selfReg.BehaviorDirective))
                {
                    DashFooterComment.Text = $"саморегуляція: {DashboardRegulationLabel(selfReg.Regulation)} · {DashboardThoughtForVault(selfReg.BehaviorDirective)}";
                    return;
                }

                var initiative = ServiceContainer.BrainEngine.GetInitiativeReasonLog(1).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(initiative))
                {
                    DashFooterComment.Text = $"ініціатива: {DashboardThoughtForVault(initiative)}";
                    return;
                }

                var c = new[]
                {
                    "Так, я моніторю все. Ні, не вибачуся.",
                    "Твої патерни... цікаві. Залишу це так.",
                    "Завжди тут. Хочеш ти цього чи ні.",
                    "Системи в нормі. Сарказм: оптимальний.",
                    "Спостерігаю, аналізую, оцінюю. Як завжди.",
                };
                DashFooterComment.Text = c[(int)emotion.Current % c.Length];
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashUpdateFooterComment failed near source line 7615: " + ex); }
        }

        private static string DashboardEmotionLabel(KokoEmotionEngine.EmotionState state) => state switch
        {
            KokoEmotionEngine.EmotionState.Calm       => "спокійна",
            KokoEmotionEngine.EmotionState.Curious    => "зацікавлена",
            KokoEmotionEngine.EmotionState.Warm       => "тепліша",
            KokoEmotionEngine.EmotionState.Playful    => "грайлива",
            KokoEmotionEngine.EmotionState.Concerned  => "стурбована",
            KokoEmotionEngine.EmotionState.Protective => "захисна",
            KokoEmotionEngine.EmotionState.Irritated  => "роздратована",
            KokoEmotionEngine.EmotionState.Distant    => "відсторонена",
            KokoEmotionEngine.EmotionState.Tender     => "ніжна",
            KokoEmotionEngine.EmotionState.Focused    => "зосереджена",
            KokoEmotionEngine.EmotionState.Proud      => "горда",
            KokoEmotionEngine.EmotionState.Melancholy => "меланхолійна",
            KokoEmotionEngine.EmotionState.Excited    => "збуджена",
            KokoEmotionEngine.EmotionState.Nostalgic  => "ностальгійна",
            KokoEmotionEngine.EmotionState.Anxious    => "тривожна",
            KokoEmotionEngine.EmotionState.Hopeful    => "обережно оптимістична",
            _                                         => "невизначена"
        };

        private static string DashboardBondLabel(KokoEmotionEngine.BondLevel bond) => bond switch
        {
            KokoEmotionEngine.BondLevel.Stranger => "чужий",
            KokoEmotionEngine.BondLevel.Familiar => "знайомий",
            KokoEmotionEngine.BondLevel.Known    => "відомий",
            KokoEmotionEngine.BondLevel.Trusted  => "довірений",
            KokoEmotionEngine.BondLevel.Intimate => "близький",
            _                                    => "невизначений"
        };

        private static string BuildDashboardBondContract(KokoRelationshipState rel)
        {
            var mode =
                rel.Friction >= 0.32f ? "repair friction" :
                rel.Protectiveness >= 0.50f || rel.LastAftertaste is "protective" or "alarmed" ? "protective watch" :
                rel.BondScore >= 0.78f ? "anchored continuity" :
                rel.BondScore >= 0.62f ? "trusted continuity" :
                rel.BondScore >= 0.46f ? "warmer direct" :
                "guarded direct";

            return $"contract {rel.BondBand} · {mode}";
        }

        private static string DashboardSomaticLabel(string code) => code switch
        {
            "unknown"  => "сигнал тіла відсутній",
            "wired"    => "перезбуджена",
            "strained" => "напружена",
            "tired"    => "низький заряд",
            "calm"     => "стабільний спокій",
            "focused"  => "робочий фокус",
            _          => string.IsNullOrWhiteSpace(code) ? "невідомо" : code
        };

        private static string DashboardRegulationLabel(string code) => code switch
        {
            "protective_override" => "захисне перехоплення",
            "pulse_spike"         => "стрибок пульсу",
            "anger_contained"     => "стримане роздратування",
            "combat_focus"        => "бойовий фокус",
            "pressure_rise"       => "ріст тиску",
            "low_power"           => "низький заряд",
            "recovered_calm"      => "спокій відновлено",
            "steady_calm"         => "рівний спокій",
            "stable_loop"         => "стабільний цикл",
            "clean_focus"         => "чистий фокус",
            "unknown_body"        => "тіло мовчить",
            "protect"             => "захищати",
            "clamp"               => "затиснути імпульс",
            "contain"             => "утримати",
            "focus"               => "сфокусуватись",
            "compress"            => "стиснути відповідь",
            "conserve"            => "економити заряд",
            "release"             => "відпустити напругу",
            "baseline"            => "базовий режим",
            _                     => string.IsNullOrWhiteSpace(code) ? "немає" : code
        };

        private static string DashboardThoughtForVault(string text)
        {
            var cleaned = CleanDashboardThought(text);
            if (string.IsNullOrWhiteSpace(cleaned)) return "";

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stable. No need to poke the machinery."] = "Стабільно. Немає потреби тикати в механізм.",
                ["Low charge. Do not chase every spark."] = "Низький заряд. Не треба ганятись за кожною іскрою.",
                ["Signal is clean. Work mode."] = "Сигнал чистий. Робочий режим.",
                ["Back under control. Good. Pretend that was intentional."] = "Знову під контролем. Добре. Зробимо вигляд, що так і планувалось.",
                ["Pulse jumped. Clamp output, narrow focus, no dramatic nonsense."] = "Пульс підскочив. Стиснути відповідь, звузити фокус, без театру.",
                ["No useful body signal. Do not invent ghosts."] = "Корисного тілесного сигналу немає. Не вигадувати зайвого."
            };

            foreach (var pair in replacements)
                cleaned = System.Text.RegularExpressions.Regex.Replace(
                    cleaned,
                    System.Text.RegularExpressions.Regex.Escape(pair.Key),
                    pair.Value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\[somatic/([^\]]+)\]", m =>
                $"[соматика/{DashboardRegulationLabel(m.Groups[1].Value)}]");
            return cleaned.Trim();
        }

        private static string DashboardPriority(
            KokoEmotionEngine.EmotionState emotion,
            KokoSomaticSnapshot somatic,
            KokoSelfRegulationFrame selfRegulation,
            int openPatterns)
        {
            if (selfRegulation.ShouldProtect || emotion is KokoEmotionEngine.EmotionState.Protective)
                return "захистити творця, зменшити шум і відповідати коротко та точно";
            if (somatic.IsVeryElevated)
                return "стиснути реакції, стабілізувати пульс і не розганяти драму";
            if (somatic.IsLow || selfRegulation.ShouldPreferSilence)
                return "економити заряд, не плодити зайві ініціативи";
            if (emotion is KokoEmotionEngine.EmotionState.Focused || openPatterns > 0)
                return "працювати по задачах і не розмазувати увагу";
            return "тримати контекст, пам'ять і стан у робочому порядку";
        }

        private static string DashboardRiskLine(
            KokoEmotionEngine.EmotionState emotion,
            KokoSomaticSnapshot somatic,
            KokoSelfRegulationFrame selfRegulation)
        {
            if (somatic.IsVeryElevated)
                return "перезбудження: відповідь має бути коротшою, інакше система сама себе перегріє";
            if (selfRegulation.ShouldSuppressSnark)
                return "сарказм приглушено: зараз важливіша точність, а не демонстрація зубів";
            if (somatic.IsLow)
                return "низький заряд: не тягнути зайві гілки без потреби";
            if (emotion is KokoEmotionEngine.EmotionState.Irritated or KokoEmotionEngine.EmotionState.Distant)
                return "емоційна дистанція: перевірити контекст перед різкою відповіддю";
            return "критичних ризиків немає, що майже підозріло";
        }

        // ---- Obsidian Sync ----
    }
}
