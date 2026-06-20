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
        private void DashDrawDevSection()
        {
            try
            {
                DashDrawGitActivityChart();
                DashDrawTimeDistPie();
                DashDrawSprintBurndown();
                DashLoadDevKpis();
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawDevSection failed near source line 7925: " + ex); }
        }

        private void DashDrawGitActivityChart()
        {
            try
            {
                DashGitActivityCanvas.Children.Clear();
                DashGitAxisCanvas.Children.Clear();

                var chats   = ServiceContainer.ChatRepository;
                var emotion = ServiceContainer.EmotionEngine;
                var w = DashW(DashGitActivityCanvas, 800);
                var h = DashH(DashGitActivityCanvas, 130);

                string[] dayLabels = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
                var commits  = new double[7];
                var prMerge  = new double[7];
                var mlMerged = new double[7];

                for (int d = 6; d >= 0; d--)
                {
                    var day = DateTime.Today.AddDays(-d);
                    var msgCount = chats.GetMessagesFromDate(day, 500)
                                        .Where(m => m.Timestamp.Date == day).Count();
                    var idx = 6 - d;
                    commits[idx]  = Math.Max(0, msgCount / 3);
                    prMerge[idx]  = Math.Max(0, msgCount / 8);
                    mlMerged[idx] = Math.Max(0, msgCount / 12);
                }

                if (commits.All(v => v == 0))
                {
                    commits  = new double[] { 4, 7, 5, 9, 6, 3, 2 };
                    prMerge  = new double[] { 1, 2, 1, 3, 2, 1, 0 };
                    mlMerged = new double[] { 0, 1, 0, 1, 1, 0, 0 };
                }

                var maxV   = Math.Max(1, commits.Max());
                var groupW = (w - 8) / 7.0;
                var barW   = groupW / 3.5;

                for (int g = 1; g <= 4; g++)
                {
                    var gy = h - (g / 4.0) * h * 0.88;
                    DashGitActivityCanvas.Children.Add(DashHLine(0, w, gy, MediaColor.FromArgb(14, 255, 255, 255)));
                }

                for (int i = 0; i < 7; i++)
                {
                    var groupX = i * groupW + 4;
                    var today  = (int)(DateTime.Today.DayOfWeek + 6) % 7 == i;

                    void Bar(double x, double val, MediaColor color, string tip)
                    {
                        var bh   = Math.Max(3, val / maxV * h * 0.85);
                        var rect = new WpfRect
                        {
                            Width   = barW - 1, Height = bh,
                            Fill    = new System.Windows.Media.SolidColorBrush(today
                                ? MediaColor.FromArgb(220, color.R, color.G, color.B)
                                : MediaColor.FromArgb(160, color.R, color.G, color.B)),
                            RadiusX = 2, RadiusY = 2,
                            ToolTip = tip
                        };
                        if (today) rect.Effect = DashGlow(color, 10);
                        Canvas.SetLeft(rect, x);
                        Canvas.SetBottom(rect, 0);
                        DashGitActivityCanvas.Children.Add(rect);
                    }

                    Bar(groupX,          commits[i],  UiInfo, $"Commits: {commits[i]:0}");
                    Bar(groupX + barW,   prMerge[i],  UiOk,  $"PR-Merge: {prMerge[i]:0}");
                    Bar(groupX + barW*2, mlMerged[i], UiMercury,  $"ML-Merged: {mlMerged[i]:0}");
                }

                for (int i = 0; i < 7; i++)
                    DashGitAxisCanvas.Children.Add(DashLabel(dayLabels[i], i * groupW + 4 + groupW / 2 - 8, 2, 7,
                        UiMuted));

                void AddLegend(Canvas c, double x, MediaColor col, string txt)
                {
                    var dot = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = new System.Windows.Media.SolidColorBrush(col) };
                    Canvas.SetLeft(dot, x); Canvas.SetTop(dot, 4);
                    c.Children.Add(dot);
                    c.Children.Add(DashLabel(txt, x + 9, 2, 7, col));
                }
                AddLegend(DashGitAxisCanvas, groupW * 0.5,  UiInfo, "Commits");
                AddLegend(DashGitAxisCanvas, groupW * 2.5,  UiOk,  "PR-Merge");
                AddLegend(DashGitAxisCanvas, groupW * 4.5,  UiMercury,  "ML-Merged");

                var totalStoryPts = (int)(commits.Sum() * 1.5 + prMerge.Sum() * 3);
                DashGitVelocityLabel.Text = $"{totalStoryPts} Story Points computed";
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawGitActivityChart failed near source line 8019: " + ex); }
        }

        private void DashDrawTimeDistPie()
        {
            try
            {
                DashTimeDistCanvas.Children.Clear();
                DashTimeDistLegendPanel.Children.Clear();

                var health = ServiceContainer.HealthService.GetToday();

                double coding   = 0.29;
                double debug    = 0.12;
                double review   = 0.20;
                double planning = 0.10;
                double research = 0.29;

                if (health != null)
                {
                    var energy = (health.Energy ?? 5) / 10.0;
                    var stress = (health.Stress ?? 3) / 10.0;
                    coding   = Math.Clamp(0.25 + energy * 0.10 - stress * 0.05, 0.15, 0.45);
                    debug    = Math.Clamp(0.10 + stress * 0.08, 0.05, 0.30);
                    review   = 0.18;
                    planning = Math.Clamp(0.10 - energy * 0.03 + stress * 0.02, 0.05, 0.20);
                    research = Math.Max(0.05, 1.0 - coding - debug - review - planning);
                }

                var segments = new (string Name, double Pct, MediaColor Color)[]
                {
                    ("Кодинг",      coding,   UiInfo),
                    ("Дебаг",       debug,    UiPulse),
                    ("Code Review", review,   UiMercury),
                    ("Planning",    planning, UiWarn),
                    ("Research",    research, UiOk),
                };

                double cx = 65, cy = 65, r = 57, ir = 28, angle = -90;
                foreach (var (name, pct, color) in segments)
                {
                    var sweep = pct * 360;
                    if (sweep >= 360) sweep = 359.9;
                    DashTimeDistCanvas.Children.Add(
                        DashPieSlice(cx, cy, r, ir, angle, sweep, new System.Windows.Media.SolidColorBrush(color)));
                    angle += sweep;
                }

                var dom = segments.OrderByDescending(s => s.Pct).First();
                DashTimeDistCenterLabel.Text = $"{dom.Pct:P0}";

                foreach (var (name, pct, color) in segments)
                {
                    var row = new StackPanel { Orientation = WpfOri.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
                    row.Children.Add(new System.Windows.Shapes.Ellipse
                    {
                        Width = 7, Height = 7, Fill = new System.Windows.Media.SolidColorBrush(color),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0)
                    });
                    row.Children.Add(new TextBlock
                    {
                        Text = $"{name} {pct:P0}", FontSize = 8,
                        Foreground = new System.Windows.Media.SolidColorBrush(UiMuted),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new WpfFF("Consolas")
                    });
                    DashTimeDistLegendPanel.Children.Add(row);
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawTimeDistPie failed near source line 8088: " + ex); }
        }

        private void DashDrawSprintBurndown()
        {
            try
            {
                DashSprintBurndownCanvas.Children.Clear();
                DashSprintAxisCanvas.Children.Clear();

                var w = DashW(DashSprintBurndownCanvas, 1200);
                var h = DashH(DashSprintBurndownCanvas, 90);

                int sprintLen = 14;
                int sprintDay = DashGetCurrentSprintDay();
                int totalPts  = 42;

                for (int g = 1; g <= 4; g++)
                {
                    var gy = h - (g / 4.0) * h * 0.88;
                    DashSprintBurndownCanvas.Children.Add(DashHLine(0, w, gy, MediaColor.FromArgb(12, 255, 255, 255)));
                }

                var idealLine = new System.Windows.Shapes.Polyline
                {
                    Stroke = new System.Windows.Media.SolidColorBrush(MediaColor.FromArgb(50, 107, 91, 125)),
                    StrokeThickness = 1.2,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 5, 4 }
                };
                for (int d = 0; d <= sprintLen; d++)
                    idealLine.Points.Add(new System.Windows.Point(
                        d / (double)sprintLen * w,
                        h - (1.0 - d / (double)sprintLen) * h * 0.88));
                DashSprintBurndownCanvas.Children.Add(idealLine);

                var chats     = ServiceContainer.ChatRepository;
                var actualPts = new double[sprintDay + 1];
                actualPts[0]  = totalPts;
                double remaining = totalPts;

                for (int d = 1; d <= sprintDay; d++)
                {
                    var day  = DateTime.Today.AddDays(-(sprintDay - d));
                    var msgs = chats.GetMessagesFromDate(day, 500)
                                    .Where(m => m.Timestamp.Date == day).Count();
                    remaining   = Math.Max(0, remaining - Math.Max(0.5, msgs / 5.0 + 0.5));
                    actualPts[d] = remaining;
                }

                var fillPoly = new System.Windows.Shapes.Polygon { Opacity = 0.10, Fill = new System.Windows.Media.SolidColorBrush(UiInfo) };
                fillPoly.Points.Add(new System.Windows.Point(0, h));
                for (int d = 0; d <= sprintDay; d++)
                    fillPoly.Points.Add(new System.Windows.Point(
                        d / (double)sprintLen * w,
                        h - (actualPts[d] / totalPts) * h * 0.88));
                fillPoly.Points.Add(new System.Windows.Point(sprintDay / (double)sprintLen * w, h));
                DashSprintBurndownCanvas.Children.Add(fillPoly);

                var actualLine = new System.Windows.Shapes.Polyline
                {
                    Stroke = new System.Windows.Media.SolidColorBrush(UiInfo),
                    StrokeThickness = 2,
                    Effect = DashGlow(UiInfo, 6)
                };
                for (int d = 0; d <= sprintDay; d++)
                {
                    var x = d / (double)sprintLen * w;
                    var y = h - (actualPts[d] / totalPts) * h * 0.88;
                    actualLine.Points.Add(new System.Windows.Point(x, y));

                    var dot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = new System.Windows.Media.SolidColorBrush(UiInfo) };
                    dot.ToolTip = d == sprintDay ? "Сьогодні" : $"День {d}: {actualPts[d]:F0} pts";
                    Canvas.SetLeft(dot, x - 2.5);
                    Canvas.SetTop(dot, y - 2.5);
                    DashSprintBurndownCanvas.Children.Add(dot);
                }
                DashSprintBurndownCanvas.Children.Add(actualLine);

                for (int d = 0; d <= sprintLen; d += 2)
                    DashSprintAxisCanvas.Children.Add(DashLabel($"д{d}", d / (double)sprintLen * DashW(DashSprintAxisCanvas, w), 2, 7,
                        UiMuted));

                DashSprintAxisCanvas.Children.Add(DashLabel($"// день {sprintDay}, факт", 0, 9, 7, UiInfo));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashDrawSprintBurndown failed near source line 8172: " + ex); }
        }

        private void DashLoadDevKpis()
        {
            try
            {
                var chats    = ServiceContainer.ChatRepository;
                var memory   = ServiceContainer.KokoMemory;
                var patterns = ServiceContainer.KokoPatterns;

                // — KPI 1: Повідомлень/тиждень (real chat throughput) —
                var weekMsgs   = 0;
                var msgsSpark  = new double[7];
                for (int d = 6; d >= 0; d--)
                {
                    var day = DateTime.Today.AddDays(-d);
                    var cnt = chats.GetMessagesFromDate(day, 500)
                                   .Where(m => m.Timestamp.Date == day).Count();
                    weekMsgs       += cnt;
                    msgsSpark[6-d]  = cnt;
                }
                var prevWeekMsgs = 0;
                for (int d = 13; d >= 7; d--)
                {
                    var day = DateTime.Today.AddDays(-d);
                    prevWeekMsgs += chats.GetMessagesFromDate(day, 500)
                                         .Where(m => m.Timestamp.Date == day).Count();
                }
                var msgDelta = prevWeekMsgs == 0 ? 0 : (int)((weekMsgs - prevWeekMsgs) * 100.0 / prevWeekMsgs);
                DashDevKpiVelocity.Text      = weekMsgs.ToString();
                DashDevKpiVelocityDelta.Text = msgDelta == 0 ? "—" : (msgDelta > 0 ? $"+{msgDelta}%" : $"{msgDelta}%");
                DashDevKpiVelocityDelta.Foreground = msgDelta >= 0
                    ? new System.Windows.Media.SolidColorBrush(UiOk)
                    : new System.Windows.Media.SolidColorBrush(UiPulse);

                // — KPI 2: Пік активності (real peak hour from pattern engine) —
                var hourly  = patterns.GetHourlyActivity();
                var peakHr  = 0; var peakV = 0;
                for (int h = 0; h < 24; h++) if (hourly[h] > peakV) { peakV = hourly[h]; peakHr = h; }
                DashDevKpiFocus.Text      = peakV == 0 ? "—" : $"{peakHr:00}";
                DashDevKpiFocusDelta.Text = peakV == 0 ? "немає даних" : $":00 ({peakV})";
                var focusSpark = hourly.Skip(Math.Max(0, peakHr - 3)).Take(7)
                                       .Select(v => (double)v).ToArray();
                if (focusSpark.Length < 7) focusSpark = Enumerable.Repeat(0.0, 7).ToArray();

                // — KPI 3: Confirmed facts % (real, from KokoMemory) —
                var allFacts   = memory.Facts;
                var confirmed  = allFacts.Count(f => f.ConfirmCount > 1);
                var coveragePct = allFacts.Count == 0 ? 0 : (int)(confirmed * 100.0 / allFacts.Count);
                DashDevKpiCoverage.Text      = $"{coveragePct}";
                DashDevKpiCoverageDelta.Text = $"% ({confirmed}/{allFacts.Count})";

                // — KPI 4: Insights count (real, from pattern engine) —
                var insights = patterns.Patterns.Count;
                DashDevKpiBugs.Text      = insights.ToString();
                DashDevKpiBugsDelta.Text = "патернів";

                DashDrawSparkline(DashDevKpiSparkVelocity, msgsSpark,  UiInfo);
                DashDrawSparkline(DashDevKpiSparkFocus,    focusSpark, UiMercury);
                DashDrawSparkline(DashDevKpiSparkCoverage,
                    Enumerable.Repeat((double)coveragePct, 7).ToArray(),
                    UiOk);
                DashDrawSparkline(DashDevKpiSparkBugs,
                    Enumerable.Repeat((double)insights, 7).ToArray(),
                    UiPulse);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Dash] DevKpis failed: {ex.Message}"); }
        }

        private static int DashGetCurrentSprintDay()
            => (DateTime.Today.DayOfYear - 1) % 14 + 1;

        // ---- Sparkline ----
        private void DashDrawSparkline(Canvas canvas, double[] values, MediaColor color)
        {
            canvas.Children.Clear();
            if (values == null || values.Length < 2) return;
            var w    = DashW(canvas, 120);
            var h    = DashH(canvas, 24);
            if (values.All(v => Math.Abs(v) < 0.001))
            {
                canvas.Children.Add(DashHLine(0, w, h * 0.68, MediaColor.FromArgb(54, UiGrid.R, UiGrid.G, UiGrid.B)));
                return;
            }
            var max  = values.Max() > 0 ? values.Max() : 1;
            var step = w / (values.Length - 1.0);
            var line = new System.Windows.Shapes.Polyline
            {
                Stroke = new System.Windows.Media.SolidColorBrush(color),
                StrokeThickness = 1.5,
                Opacity = 0.9
            };
            for (int i = 0; i < values.Length; i++)
                line.Points.Add(new System.Windows.Point(i * step, h - (values[i] / max) * h * 0.85));
            canvas.Children.Add(line);

            var last = values.Length - 1;
            var endX = last * step;
            var endY = h - (values[last] / max) * h * 0.85;
            var dot  = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = new System.Windows.Media.SolidColorBrush(color) };
            Canvas.SetLeft(dot, endX - 2.5);
            Canvas.SetTop(dot, endY - 2.5);
            canvas.Children.Add(dot);
        }

        // ---- Data helpers ----
        private double[] DashBuildConnSparkValues(KokoEmotionEngine emotion)
        {
            var result  = new double[7];
            var history = emotion.Data.History;
            for (int d = 6; d >= 0; d--)
            {
                var day     = DateTime.Today.AddDays(-d);
                var entries = history.Where(e => e.When.Date == day).ToList();
                result[6-d] = entries.Any()
                    ? entries.Average(e => e.Intensity) * 100
                    : emotion.ConnectionScore * 100;
            }
            return result;
        }

        private double[] DashBuildDailyMsgSpark(ChatRepository chats)
        {
            var result = new double[7];
            for (int d = 6; d >= 0; d--)
            {
                var day = DateTime.Today.AddDays(-d);
                result[6-d] = chats.GetMessagesFromDate(day, 500).Where(m => m.Timestamp.Date == day).Count();
            }
            return result;
        }

        private List<(DateTime When, double Conn)> DashBuildConnSeries(
            List<KokoEmotionEngine.EmotionEntry> history, float currentConn)
        {
            var result   = new List<(DateTime, double)>();
            var positive = new[]
            {
                KokoEmotionEngine.EmotionState.Warm, KokoEmotionEngine.EmotionState.Playful,
                KokoEmotionEngine.EmotionState.Tender, KokoEmotionEngine.EmotionState.Proud,
                KokoEmotionEngine.EmotionState.Curious, KokoEmotionEngine.EmotionState.Excited,
                KokoEmotionEngine.EmotionState.Hopeful
            };
            var recent = history.Where(e => e.When >= DateTime.Now.AddDays(-30)).ToList();
            if (recent.Count == 0) recent = history.TakeLast(20).ToList();
            if (recent.Count < 2)  return result;

            double conn = Math.Max(0.2, currentConn - recent.Count * 0.005);
            foreach (var e in recent)
            {
                if (positive.Contains(e.State))
                    conn = Math.Min(1.0, conn + 0.025);
                else if (e.State is KokoEmotionEngine.EmotionState.Distant
                              or KokoEmotionEngine.EmotionState.Irritated)
                    conn = Math.Max(0.1, conn - 0.02);
                result.Add((e.When, Math.Clamp(conn, 0.05, 1.0)));
            }
            return result;
        }

        // ---- Pie slice ----
        private static System.Windows.Shapes.Path DashPieSlice(double cx, double cy, double r, double ir,
                              double startDeg, double sweepDeg, MediaBrush fill)
        {
            static (double x, double y) P(double cx, double cy, double rad, double deg)
            {
                var a = deg * Math.PI / 180;
                return (cx + rad * Math.Cos(a), cy + rad * Math.Sin(a));
            }

            var end = startDeg + sweepDeg;
            var (ox1, oy1) = P(cx, cy, r,  startDeg);
            var (ox2, oy2) = P(cx, cy, r,  end);
            var (ix1, iy1) = P(cx, cy, ir, end);
            var (ix2, iy2) = P(cx, cy, ir, startDeg);
            var large = sweepDeg > 180 ? 1 : 0;

            var geom = new System.Windows.Media.PathGeometry();
            var fig  = new System.Windows.Media.PathFigure { StartPoint = new System.Windows.Point(ox1, oy1), IsClosed = true, IsFilled = true };
            fig.Segments.Add(new System.Windows.Media.ArcSegment(new System.Windows.Point(ox2, oy2), new WpfSz(r, r),   0, large == 1, System.Windows.Media.SweepDirection.Clockwise,        true));
            fig.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(ix1, iy1), true));
            fig.Segments.Add(new System.Windows.Media.ArcSegment(new System.Windows.Point(ix2, iy2), new WpfSz(ir, ir), 0, large == 1, System.Windows.Media.SweepDirection.Counterclockwise, true));
            geom.Figures.Add(fig);
            return new System.Windows.Shapes.Path { Data = geom, Fill = fill, Opacity = 0.88 };
        }

        // ---- Drawing primitives ----
        private static System.Windows.Shapes.Line DashHLine(double x1, double x2, double y, MediaColor color, double sw = 1)
            => new() { X1 = x1, X2 = x2, Y1 = y, Y2 = y,
                Stroke = new System.Windows.Media.SolidColorBrush(color), StrokeThickness = sw };

        private static System.Windows.Shapes.Line DashVLine(double x, double y1, double y2, MediaColor color,
                                  double sw = 1.5, double dashOn = 0, double dashOff = 0)
        {
            var l = new System.Windows.Shapes.Line { X1 = x, X2 = x, Y1 = y1, Y2 = y2,
                Stroke = new System.Windows.Media.SolidColorBrush(color), StrokeThickness = sw };
            if (dashOn > 0) l.StrokeDashArray = new System.Windows.Media.DoubleCollection { dashOn, dashOff };
            return l;
        }

        private static TextBlock DashLabel(string text, double x, double y, double fs, MediaColor color)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = fs,
                Foreground = new System.Windows.Media.SolidColorBrush(color),
                FontFamily = new WpfFF("Consolas")
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            return tb;
        }

        private static void DashDrawEmptyState(Canvas canvas, string text, double w, double h)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontFamily = new WpfFF("Segoe UI Variable, Segoe UI"),
                Foreground = new SolidColorBrush(MediaColor.FromArgb(150, UiMuted.R, UiMuted.G, UiMuted.B))
            };
            label.Measure(new WpfSz(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Max(8, (w - label.DesiredSize.Width) * 0.5));
            Canvas.SetTop(label, Math.Max(8, (h - label.DesiredSize.Height) * 0.5));
            canvas.Children.Add(label);
        }

        private static System.Windows.Media.Effects.DropShadowEffect DashGlow(MediaColor c, double blur)
            => new() { Color = c, ShadowDepth = 0, BlurRadius = blur, Opacity = 0.65 };

        private static double DashW(Canvas canvas, double fallback)
            => canvas.ActualWidth  > 10 ? canvas.ActualWidth  : fallback;

        private static double DashH(Canvas canvas, double fallback)
            => canvas.ActualHeight > 10 ? canvas.ActualHeight : fallback;

        private static MediaColor DashBlendColor(MediaColor a, MediaColor b, float t, byte alpha)
        {
            byte lerp(byte x, byte y) => (byte)(x + (y - x) * t);
            return MediaColor.FromArgb(alpha, lerp(a.R, b.R), lerp(a.G, b.G), lerp(a.B, b.B));
        }

        // ---- Emotion color map ----
        private static MediaColor DashEmotionColorOf(KokoEmotionEngine.EmotionState s) => s switch
        {
            KokoEmotionEngine.EmotionState.Calm       => UiMercury,
            KokoEmotionEngine.EmotionState.Curious    => UiInfo,
            KokoEmotionEngine.EmotionState.Warm       => MediaColor.FromRgb(0xD8, 0x9A, 0xA8),
            KokoEmotionEngine.EmotionState.Playful    => UiWarn,
            KokoEmotionEngine.EmotionState.Proud      => UiWarn,
            KokoEmotionEngine.EmotionState.Concerned  => MediaColor.FromRgb(0xC9, 0x82, 0x4A),
            KokoEmotionEngine.EmotionState.Melancholy => UiMuted,
            KokoEmotionEngine.EmotionState.Irritated  => UiPulse,
            KokoEmotionEngine.EmotionState.Protective => UiPulse,
            KokoEmotionEngine.EmotionState.Tender     => MediaColor.FromRgb(0xD8, 0x9A, 0xA8),
            KokoEmotionEngine.EmotionState.Focused    => UiInfo,
            KokoEmotionEngine.EmotionState.Distant    => UiMuted,
            KokoEmotionEngine.EmotionState.Excited    => UiOk,
            KokoEmotionEngine.EmotionState.Nostalgic  => MediaColor.FromRgb(0x8F, 0xA3, 0xBF),
            KokoEmotionEngine.EmotionState.Anxious    => MediaColor.FromRgb(0xC9, 0x82, 0x4A),
            KokoEmotionEngine.EmotionState.Hopeful    => UiInfo,
            _                                         => MediaColor.FromRgb(255, 255, 255),
        };

        private static MediaBrush DashMakeBrush(KokoEmotionEngine.EmotionState s)
            => new System.Windows.Media.SolidColorBrush(DashEmotionColorOf(s));
    }
}
