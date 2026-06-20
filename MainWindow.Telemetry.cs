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
        private void SetupHeartUI()
        {
            try
            {
                var scaleX = new DoubleAnimation(1.0, 1.45, TimeSpan.FromMilliseconds(120))
                {
                    AutoReverse = true,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(scaleX, HeartScale);
                Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));

                var scaleY = scaleX.Clone();
                Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));

                _beatStoryboard = new Storyboard();
                _beatStoryboard.Children.Add(scaleX);
                _beatStoryboard.Children.Add(scaleY);

                var heart = ServiceContainer.Heart;
                heart.BpmChanged += bpm => Dispatcher.InvokeAsync(() =>
                {
                    if (TryGetVerifiedWearablePulse(out var wearableBpm, out _))
                    {
                        HeartBpmText.Text = $"{wearableBpm:0} bpm";
                        _ecgLastBpm = wearableBpm;
                        PushHeartSample(wearableBpm);
                        DrawHeartBpmGraph();
                        UpdateHeartStats(wearableBpm);
                    }
                    else
                    {
                        HeartBpmText.Text = "-- bpm";
                    }
                    if (PulseTab.Visibility == Visibility.Visible)
                        UpdatePulseTabNumbers();
                });
                heart.Beat += bpm => Dispatcher.InvokeAsync(() =>
                {
                    try { _beatStoryboard!.Begin(this, HandoffBehavior.SnapshotAndReplace, true); }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SetupHeartUI failed near source line 635: " + ex); }
                    RecordBeatRR();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Heart UI] setup failed: {ex}");
            }
        }

        private void PushHeartSample(double bpm)
        {
            var now = DateTime.Now;
            _heartHistory.Enqueue((now, bpm));
            var cutoff = now.AddMinutes(-5);
            while (_heartHistory.Count > 0 && _heartHistory.Peek().t < cutoff)
                _heartHistory.Dequeue();
        }

        private void RecordBeatRR()
        {
            var now = DateTime.Now;
            if (_lastBeatTime != DateTime.MinValue)
            {
                var rr = (now - _lastBeatTime).TotalMilliseconds;
                if (rr > 200 && rr < 2500)
                {
                    _heartRR.Enqueue(rr);
                    while (_heartRR.Count > 40) _heartRR.Dequeue();
                }
            }
            _lastBeatTime = now;
        }

        private void DrawHeartBpmGraph()
        {
            var canvas = DashHeartBpmCanvas;
            if (canvas == null) return;
            canvas.Children.Clear();
            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            if (w < 20 || h < 20 || _heartHistory.Count < 2) return;

            var pts = _heartHistory.ToArray();
            double minBpm = 40, maxBpm = 150;
            foreach (var (_, b) in pts)
            {
                if (b < minBpm) minBpm = Math.Max(35, b - 5);
                if (b > maxBpm) maxBpm = Math.Min(160, b + 5);
            }
            double rng = Math.Max(10, maxBpm - minBpm);
            double tMin = pts[0].t.Ticks, tMax = pts[^1].t.Ticks;
            double tRng = Math.Max(1, tMax - tMin);

            // gridlines (60/80/100/120)
            foreach (var marker in new[] { 60.0, 80.0, 100.0, 120.0 })
            {
                if (marker < minBpm || marker > maxBpm) continue;
                double y = h - (marker - minBpm) / rng * h;
                var grid = new System.Windows.Shapes.Line
                {
                    X1 = 0, X2 = w, Y1 = y, Y2 = y,
                    Stroke = new SolidColorBrush(MediaColor.FromArgb(40, 255, 255, 255)),
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 2, 4 }
                };
                canvas.Children.Add(grid);
                var lbl = new TextBlock
                {
                    Text = $"{marker:0}",
                    FontSize = 8,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(MediaColor.FromArgb(90, 200, 200, 210))
                };
                Canvas.SetLeft(lbl, 2);
                Canvas.SetTop(lbl, y - 10);
                canvas.Children.Add(lbl);
            }

            var poly = new System.Windows.Shapes.Polyline
            {
                Stroke = new SolidColorBrush(UiPulse),
                StrokeThickness = 2.0,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = UiPulse,
                    ShadowDepth = 0, BlurRadius = 7, Opacity = 0.38
                }
            };
            foreach (var (t, b) in pts)
            {
                double x = (t.Ticks - tMin) / tRng * w;
                double y = h - (b - minBpm) / rng * h;
                poly.Points.Add(new System.Windows.Point(x, y));
            }
            canvas.Children.Add(poly);

            // current dot at tail
            var last = pts[^1];
            double lx = w, ly = h - (last.bpm - minBpm) / rng * h;
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(UiPulse)
            };
            Canvas.SetLeft(dot, lx - 3);
            Canvas.SetTop(dot, ly - 3);
            canvas.Children.Add(dot);
        }

        private void UpdateHeartStats(double currentBpm)
        {
            var heart = ServiceContainer.Heart;
            DashHeartCurText.Text    = $"{currentBpm:0.0}";
            DashHeartBaseText.Text   = $"{heart.BaselineBpm:0.0}";
            DashHeartTotalText.Text  = $"{heart.TotalBeats:N0}";
            DashHeartBpmLabel.Text   = $"{currentBpm:0} bpm";

            if (_heartHistory.Count > 0)
            {
                double mn = double.MaxValue, mx = double.MinValue;
                foreach (var (_, b) in _heartHistory) { if (b < mn) mn = b; if (b > mx) mx = b; }
                DashHeartMinMaxText.Text = $"{mn:0} / {mx:0}";
            }

            // RMSSD: sqrt(mean(diff(rr)^2))
            if (_heartRR.Count >= 4)
            {
                var arr = _heartRR.ToArray();
                double sumSq = 0; int n = 0;
                for (int i = 1; i < arr.Length; i++)
                {
                    var d = arr[i] - arr[i - 1];
                    sumSq += d * d; n++;
                }
                double rmssd = n > 0 ? Math.Sqrt(sumSq / n) : 0;
                DashHeartHrvText.Text = $"{rmssd:0}";
            }

            // Interpretive state text
            string state;
            if (currentBpm < 55)        state = "глибокий спокій або сон";
            else if (currentBpm < 70)   state = "спокій";
            else if (currentBpm < 85)   state = "активна увага";
            else if (currentBpm < 100)  state = "збудження / інтерес";
            else if (currentBpm < 120)  state = "стрес або сильна емоція";
            else                        state = "тахікардія — щось не так";
            DashHeartStateText.Text = state;
                try
                {
                    var somatic = ServiceContainer.BrainEngine.GetSomaticSnapshot();
                    var selfReg = ServiceContainer.BrainEngine.GetSelfRegulationFrame(somatic);
                    DashHeartStateText.Text = $"{somatic.State.ToUpper()} // {selfReg.Reaction} · {selfReg.Regulation} · strain {somatic.Strain:P0}";
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "UpdateHeartStats failed near source line 788: " + ex); }
        }

        // ------------------------------------------------------------

        // PULSE TAB
        // ------------------------------------------------------------

        private void PulseTab_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Resize: reinitialise buffer to match new canvas width
            var canvas = PulseEcgCanvas;
            if (canvas == null) return;
            int newSize = Math.Max(2, (int)(canvas.ActualWidth));
            if (newSize != _ecgBuffer.Length)
                _ecgBuffer = new double[newSize];
            DrawPulseHrGraph();
        }

        private void StartPulseLiveMonitor()
        {
            if (_pulseLiveTimer != null)
                return;

            HookWearablePulseEvents();
            _pulseLiveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pulseLiveTimer.Tick += (s, e) =>
            {
                if (PulseTab.Visibility == Visibility.Visible)
                    RefreshPulseTabLive();
            };
            _pulseLiveTimer.Start();
        }

        private void HookWearablePulseEvents()
        {
            if (_wearablePulseEventsHooked)
                return;

            try
            {
                ServiceContainer.WearableTelemetry.SampleAccepted += (result, sample) =>
                {
                    if (!sample.HeartRateBpm.HasValue)
                        return;

                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (PulseTab.Visibility != Visibility.Visible)
                            return;

                        var now = DateTime.UtcNow;
                        if (now - _lastPulseUiRefresh < TimeSpan.FromMilliseconds(250))
                            return;

                        RefreshPulseTabLive();
                    });
                };
                _wearablePulseEventsHooked = true;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PULSE_UI", "wearable event hook failed: " + ex.Message);
            }
        }

        private void RefreshPulseTabLive()
        {
            _lastPulseUiRefresh = DateTime.UtcNow;
            UpdatePulseTabNumbers();
            DrawPulseHrGraph();
        }

        private void UpdatePulseTab()
        {
            StopEcgAnimation();
            RefreshPulseTabLive();
        }

        private void UpdatePulseTabNumbers()
        {
            try
            {
                    var wearable = ServiceContainer.WearableTelemetry.State;
                    var bridge = ServiceContainer.WearableBridge;
                    var diagnostics = bridge.Diagnostics;
                    var connection = bridge.GetConnectionSnapshot(wearable);
                    var verified = IsVerifiedWearableTelemetry(connection, diagnostics, wearable);
                    var fresh = verified;
                    var lastLocal = wearable.LastSampleUtc > DateTime.MinValue
                        ? wearable.LastSampleUtc.ToLocalTime().ToString("HH:mm:ss")
                        : "--";
                    var wearableCur = verified && wearable.CurrentBpm > 0 ? wearable.CurrentBpm : 0;

                    PulseTabBpmBig.Text = wearableCur > 0 ? $"{wearableCur:0}" : "--";
                    PulseTabCurText.Text = wearableCur > 0 ? $"{wearableCur:0.0}" : "--";
                    PulseTabBaseText.Text = verified && wearable.BaselineBpm > 0 ? $"{wearable.BaselineBpm:0.0}" : "--";
                    PulseTabHrvText.Text = verified && wearable.HrvRmssdMs.HasValue ? $"{wearable.HrvRmssdMs.Value:0}" : "--";
                    PulseTabMinMaxText.Text = verified ? wearable.SleepState : "no verified sample";
                    var trustBrush = verified
                        ? new SolidColorBrush(UiOk)
                        : PulseBridgeBrush(connection.State);
                    PulseTabBpmBig.Foreground = trustBrush;
                    PulseTabCurText.Foreground = trustBrush;
                    PulseSideBpmText.Foreground = trustBrush;
                    PulseSideBpmText.Text = wearableCur > 0 ? $"{wearableCur:0}" : "--";

                    PulseTabStateLabel.Text = verified ? "LIVE VERIFIED" : FormatBridgeState(connection.State);
                    var blockedReason = VerifiedWearableBlockReason(connection, diagnostics, wearable);
                    PulseSideStateText.Text = verified ? $"last {lastLocal} / {wearable.PresenceState}" : blockedReason;
                    UpdatePulseBridgeStrip(bridge, diagnostics, connection, wearable, fresh);

                    var vitalRows = new List<object>
                    {
                        new { TimeStr = "pulse", BpmStr = wearableCur > 0 ? $"{wearableCur:0} bpm" : "--" },
                        new { TimeStr = "last measured", BpmStr = lastLocal },
                        new { TimeStr = "authority", BpmStr = verified ? "verified Galaxy Watch" : blockedReason },
                        new { TimeStr = "trust state", BpmStr = verified ? "verified" : NullDash(wearable.TrustState) },
                        new { TimeStr = "source", BpmStr = NullDash(wearable.SampleSource) },
                        new { TimeStr = "trust reason", BpmStr = verified ? "bridge pair + fresh sample" : NullDash(wearable.TrustReason) },
                        new { TimeStr = "freshness", BpmStr = verified ? "fresh" : "not trusted" },
                        new { TimeStr = "link", BpmStr = FormatBridgeState(connection.State).ToLowerInvariant() },
                        new { TimeStr = "watch app", BpmStr = connection.IsPaired ? NullDash(diagnostics.LastPairedDeviceId) : "not paired" },
                        new { TimeStr = "bridge", BpmStr = bridge.IsRunning ? $"running:{bridge.Port}" : "stopped" },
                        new { TimeStr = "authorized", BpmStr = FormatLocalTime(diagnostics.LastAuthorizedAtUtc) },
                        new { TimeStr = "command poll", BpmStr = FormatLocalTime(diagnostics.LastCommandPollAtUtc) },
                        new { TimeStr = "ack", BpmStr = $"{NullDash(diagnostics.LastAckAction)} {FormatLocalTime(diagnostics.LastCommandAckAtUtc)}" },
                        new { TimeStr = "remote", BpmStr = NullDash(diagnostics.LastRemoteEndpoint) },
                        new { TimeStr = "sample device", BpmStr = string.IsNullOrWhiteSpace(wearable.DeviceId) ? "--" : wearable.DeviceId },
                        new { TimeStr = "trusted device", BpmStr = verified ? NullDash(diagnostics.LastPairedDeviceId) : "--" },
                        new { TimeStr = "sleep", BpmStr = verified ? wearable.SleepState : "--" },
                        new { TimeStr = "confidence", BpmStr = verified ? $"{wearable.SleepConfidence:P0}" : "--" },
                        new { TimeStr = "stress", BpmStr = verified ? $"{wearable.LiveStressScore}/100" : "--" },
                        new { TimeStr = "on wrist", BpmStr = verified && wearable.OnWrist ? "yes" : "--" },
                        new { TimeStr = "location", BpmStr = verified && wearable.Latitude.HasValue && wearable.Longitude.HasValue ? "available" : "--" },
                    };
                    foreach (var line in ServiceContainer.WearableTelemetry.RecentLogLines(8).Reverse())
                    {
                        var trimmed = line.Length > 88 ? line[^88..] : line;
                        vitalRows.Add(new { TimeStr = "exe log", BpmStr = trimmed });
                    }
                    PulseVitalLog.ItemsSource = vitalRows;

                    UpdatePulseSidePanels(wearableCur);
                    UpdatePulseStatsFromSeries(GetVerifiedPulseGraphSamples(diagnostics));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PulseTab] {ex.Message}"); }
        }

        private static bool IsVerifiedWearableTelemetry(
            KokoWearableBridgeService.WearableBridgeConnectionSnapshot connection,
            KokoWearableBridgeService.WearableBridgeDiagnostics diagnostics,
            KokoWearableState wearable)
            => KokoWearableTrust.IsVerified(connection, diagnostics, wearable);

        private static bool TryGetVerifiedWearablePulse(out double bpm, out double baseline)
        {
            bpm = 0;
            baseline = 0;
            try
            {
                var wearable = ServiceContainer.WearableTelemetry.State;
                var bridge = ServiceContainer.WearableBridge;
                var diagnostics = bridge.Diagnostics;
                var connection = bridge.GetConnectionSnapshot(wearable);
                if (!IsVerifiedWearableTelemetry(connection, diagnostics, wearable) || wearable.CurrentBpm <= 0)
                    return false;

                bpm = wearable.CurrentBpm;
                baseline = wearable.BaselineBpm > 0 ? wearable.BaselineBpm : wearable.CurrentBpm;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool LooksLikeDiagnosticWearable(string? value)
            => KokoWearableTrust.LooksDiagnostic(value);

        private static string VerifiedWearableBlockReason(
            KokoWearableBridgeService.WearableBridgeConnectionSnapshot connection,
            KokoWearableBridgeService.WearableBridgeDiagnostics diagnostics,
            KokoWearableState wearable)
            => KokoWearableTrust.BlockReason(connection, diagnostics, wearable);

        private void UpdatePulseBridgeStrip(
            KokoWearableBridgeService bridge,
            KokoWearableBridgeService.WearableBridgeDiagnostics diagnostics,
            KokoWearableBridgeService.WearableBridgeConnectionSnapshot connection,
            KokoWearableState wearable,
            bool fresh)
        {
            PulseBridgeLinkText.Text = FormatBridgeState(connection.State);
            PulseBridgeLinkText.Foreground = PulseBridgeBrush(connection.State);
            PulseBridgeReasonText.Text = NullDash(connection.Reason);

            PulseBridgeWatchText.Text = connection.IsPaired
                ? $"{NullDash(diagnostics.LastPairedDeviceId)} / {(fresh ? "telemetry fresh" : "telemetry stale")}"
                : "not paired";
            PulseBridgeWatchDetailText.Text =
                $"device {NullDash(wearable.DeviceId)} / poll {FormatLocalTime(diagnostics.LastCommandPollAtUtc)} / ack {FormatLocalTime(diagnostics.LastCommandAckAtUtc)}";

            PulseBridgeDesktopText.Text = bridge.IsRunning ? $"server running :{bridge.Port}" : "server stopped";
            PulseBridgeDesktopText.Foreground = bridge.IsRunning
                ? new SolidColorBrush(UiOk)
                : new SolidColorBrush(UiWarn);
            PulseBridgeDesktopDetailText.Text =
                $"samples {diagnostics.TotalSamples} / batches {diagnostics.TotalBatchRequests} / dupes {diagnostics.TotalDuplicateSamples} / auth {diagnostics.TotalAuthFailures}";

            PulseBridgeLastSeenText.Text = connection.LastSeenAtUtc.HasValue
                ? $"{FormatLocalTime(connection.LastSeenAtUtc)} / {FormatAgeSeconds(connection.LastSeenAgeSeconds)} ago"
                : "--";
            PulseBridgeEndpointText.Text =
                $"remote {NullDash(diagnostics.LastRemoteEndpoint)} / queued {NullDash(diagnostics.LastQueuedCommandAction)} / delivered {NullDash(diagnostics.LastDeliveredCommandAction)}";

            var settings = AppSettings.Load();
            if (!PulseBridgePortBox.IsKeyboardFocusWithin)
                PulseBridgePortBox.Text = settings.WearBridgePort.ToString();
            if (!PulseBridgeUrlsInput.IsKeyboardFocusWithin)
                PulseBridgeUrlsInput.Text = BuildPulseBridgeUrlText(settings, bridge);
            PulseBridgePairLockText.Text =
                $"pc {Tail(bridge.PcId, 8)} / token ...{Tail(bridge.Token, 6)}";
            PulseBridgeSetupHintText.Text = connection.IsPaired
                ? $"paired to {NullDash(diagnostics.LastPairedDeviceId)}; watch must use one URL above"
                : "not paired; save URLs, then press Setup Once on watch";
        }

        private static string BuildPulseBridgeUrlText(AppSettings settings, KokoWearableBridgeService bridge)
        {
            var urls = bridge.LanUrls
                .Concat(bridge.ExternalUrls)
                .Concat(SplitBridgeUrls(settings.WearBridgeExternalUrls))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
            return string.Join(Environment.NewLine, urls
                .Select(BridgeUrlToHost)
                .Where(v => !string.IsNullOrWhiteSpace(v) && !IsLoopbackHost(v))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string BridgeUrlToHost(string value)
        {
            var text = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                text = "http://" + text;
            return Uri.TryCreate(text, UriKind.Absolute, out var uri) ? uri.Host : text;
        }

        private static bool IsLoopbackHost(string host)
            => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("::1", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<string> SplitBridgeUrls(string value)
            => (value ?? "")
                .Split(',', ';', '\n', '\r')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v));

        private static string NormalizeBridgeUrlInput(string value, int port)
        {
            var trimmed = (value ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(trimmed)) return "";
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                trimmed = "http://" + trimmed;
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var builder = new UriBuilder(uri);
                if (builder.Port <= 0 || uri.IsDefaultPort)
                    builder.Port = port;
                return builder.Uri.ToString().TrimEnd('/');
            }
            return trimmed;
        }

        private IReadOnlyList<string> PulseBridgeInputUrls(int port)
            => PulseBridgeUrlsInput.Text
                .Split(',', ';', '\n', '\r')
                .Select(v => NormalizeBridgeUrlInput(v, port))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static string FormatBridgeState(string state)
            => string.IsNullOrWhiteSpace(state) ? "UNKNOWN" : state.Replace('_', ' ');

        private static string FormatLocalTime(DateTime? value)
            => value.HasValue ? value.Value.ToLocalTime().ToString("HH:mm:ss") : "--";

        private static string FormatAgeSeconds(double? seconds)
        {
            if (!seconds.HasValue) return "--";
            var value = Math.Max(0, seconds.Value);
            return value < 90
                ? $"{value:0}s"
                : value < 3600
                    ? $"{value / 60.0:0}m"
                    : $"{value / 3600.0:0.0}h";
        }

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();

        private static string Tail(string value, int count)
            => string.IsNullOrEmpty(value) ? "--" : value[^Math.Min(count, value.Length)..];

        private static SolidColorBrush PulseBridgeBrush(string state)
        {
            var color = state switch
            {
                "LINKED" => UiOk,
                "WAITING_FOR_WATCH" => UiWarn,
                "WAITING_FOR_PAIR" => UiWarn,
                "ERROR" => UiPulse,
                _ => UiMuted
            };
            return new SolidColorBrush(color);
        }

        private void PulseBridgeSaveRestart_Click(object sender, RoutedEventArgs e)
        {
            var settings = AppSettings.Load();
            if (int.TryParse(PulseBridgePortBox.Text.Trim(), out var port))
                settings.WearBridgePort = Math.Clamp(port, 1024, 65535);
            settings.WearBridgeEnabled = true;
            var urls = PulseBridgeInputUrls(settings.WearBridgePort);
            settings.WearBridgeExternalUrls = string.Join(Environment.NewLine, urls);
            settings.Save();
            ServiceContainer.ReloadWearableBridge();
            UpdatePulseTabNumbers();
        }

        private void PulseBridgeCopySetup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bridge = ServiceContainer.WearableBridge;
                var settings = AppSettings.Load();
                var urls = PulseBridgeInputUrls(settings.WearBridgePort);
                var setup = new StringBuilder()
                    .AppendLine("Kokonoe WearBridge setup")
                    .AppendLine($"Port: {settings.WearBridgePort}")
                    .AppendLine($"PC ID: {bridge.PcId}")
                    .AppendLine($"Token: {bridge.Token}")
                    .AppendLine("Watch PC IPs:")
                    .AppendLine(string.Join(Environment.NewLine, (urls.Count > 0 ? urls : bridge.LanUrls)
                        .Select(BridgeUrlToHost)
                        .Where(v => !IsLoopbackHost(v))))
                    .AppendLine()
                    .AppendLine("On watch: enter Port and PC IP 1/2, then press Bind Device.")
                    .ToString();
                WClipboard.SetText(setup);
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("WEARABLE-UI", "copy bridge setup failed: " + ex);
            }
        }

        private void PulseBridgeFixFirewall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = AppSettings.Load();
                var exePath = Environment.ProcessPath
                    ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KokonoeAssistant.exe");
                var tcpRule = $"Kokonoe WearBridge TCP {settings.WearBridgePort}";
                var udpRule = $"Kokonoe WearBridge UDP {settings.WearBridgePort}";
                const string appRule = "Kokonoe Assistant App";
                static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

                var commands = new[]
                {
                    $"netsh advfirewall firewall delete rule name={Q(tcpRule)} >nul 2>nul",
                    $"netsh advfirewall firewall delete rule name={Q(udpRule)} >nul 2>nul",
                    $"netsh advfirewall firewall delete rule name={Q(appRule)} >nul 2>nul",
                    $"netsh advfirewall firewall add rule name={Q(tcpRule)} dir=in action=allow protocol=TCP localport={settings.WearBridgePort} profile=any",
                    $"netsh advfirewall firewall add rule name={Q(udpRule)} dir=in action=allow protocol=UDP localport={settings.WearBridgePort} profile=any",
                    $"netsh advfirewall firewall add rule name={Q(appRule)} dir=in action=allow program={Q(exePath)} enable=yes profile=any"
                };

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + string.Join(" & ", commands),
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                })?.WaitForExit(20000);

                PulseBridgeSetupHintText.Text = "firewall UAC requested; press Bind Device on watch again";
            }
            catch (Exception ex)
            {
                PulseBridgeSetupHintText.Text = $"firewall rule failed: {ex.Message}";
            }
        }

        private void PulseBridgeRefreshPair_Click(object sender, RoutedEventArgs e)
        {
            ServiceContainer.WearableBridge.QueueCommand("refresh_pairing");
            UpdatePulseTabNumbers();
        }

        private void PulseBridgeRestartWatch_Click(object sender, RoutedEventArgs e)
        {
            ServiceContainer.WearableBridge.QueueCommand("restart_service");
            UpdatePulseTabNumbers();
        }

        private void UpdatePulseSidePanels(double currentBpm)
        {
            try
            {
                    var wearable = ServiceContainer.WearableTelemetry.State;
                    var bridge = ServiceContainer.WearableBridge;
                    var diagnostics = bridge.Diagnostics;
                    var connection = bridge.GetConnectionSnapshot(wearable);
                    var verified = IsVerifiedWearableTelemetry(connection, diagnostics, wearable);
                    PulseMoodMainText.Text = verified ? "WATCH VERIFIED" : FormatBridgeState(connection.State);
                    PulseMoodDetailText.Text = verified ? wearable.Summary : VerifiedWearableBlockReason(connection, diagnostics, wearable);
                    PulseMoodBar.Value = verified ? Math.Clamp(wearable.SleepConfidence * 100, 0, 100) : 0;
                    PulseSystemStatusText.Text = verified
                        ? $"watch sample {wearable.LastSampleUtc.ToLocalTime():HH:mm:ss} / {wearable.SleepState}"
                        : $"bridge {FormatBridgeState(connection.State).ToLowerInvariant()} / {NullDash(diagnostics.LastRemoteEndpoint)}";

                    var wearableNow = DateTime.Now;
                    var wearableEventBrushOk = new SolidColorBrush(UiOk);
                    var wearableEventBrushWarn = new SolidColorBrush(UiWarn);
                    var events = new List<object>
                    {
                        new { Time = wearableNow.ToString("HH:mm:ss"), Text = $"link: {FormatBridgeState(connection.State).ToLowerInvariant()}", Brush = verified ? wearableEventBrushOk : wearableEventBrushWarn },
                        new { Time = FormatLocalTime(connection.LastSeenAtUtc), Text = $"trust: {(verified ? "verified" : VerifiedWearableBlockReason(connection, diagnostics, wearable))}", Brush = verified ? wearableEventBrushOk : wearableEventBrushWarn },
                        new { Time = FormatLocalTime(diagnostics.LastPairAtUtc), Text = $"watch app: {(connection.IsPaired ? NullDash(diagnostics.LastPairedDeviceId) : "not paired")}", Brush = connection.IsPaired ? wearableEventBrushOk : wearableEventBrushWarn },
                        new { Time = FormatLocalTime(diagnostics.LastCommandPollAtUtc), Text = $"command poll / ack: {NullDash(diagnostics.LastAckAction)}", Brush = diagnostics.LastCommandPollAtUtc.HasValue ? wearableEventBrushOk : wearableEventBrushWarn },
                        new { Time = wearableNow.ToString("HH:mm:ss"), Text = $"bridge: {(bridge.IsRunning ? $"running:{bridge.Port}" : "stopped")} / samples {diagnostics.TotalSamples} / auth {diagnostics.TotalAuthFailures}", Brush = bridge.IsRunning ? wearableEventBrushOk : wearableEventBrushWarn },
                    };
                    if (verified)
                    {
                        events.Add(new { Time = wearable.LastSampleUtc.ToLocalTime().ToString("HH:mm:ss"), Text = $"last pulse sample: {currentBpm:0} bpm", Brush = wearableEventBrushOk });
                        events.Add(new { Time = wearableNow.ToString("HH:mm:ss"), Text = $"sleep state: {wearable.SleepState} / confidence {wearable.SleepConfidence:P0}", Brush = wearableEventBrushOk });
                    }
                    else
                    {
                        events.Add(new { Time = "--", Text = "no verified watch sample shown", Brush = wearableEventBrushWarn });
                    }
                    PulseRecentEvents.ItemsSource = events;
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "UpdatePulseSidePanels failed near source line 1891: " + ex); }
        }

        private IReadOnlyList<(DateTime t, double bpm)> GetVerifiedPulseGraphSamples(
            KokoWearableBridgeService.WearableBridgeDiagnostics? diagnostics = null)
        {
            try
            {
                diagnostics ??= ServiceContainer.WearableBridge.Diagnostics;
                var pairedDevice = (diagnostics.LastPairedDeviceId ?? "").Trim();
                if (string.IsNullOrWhiteSpace(pairedDevice) ||
                    pairedDevice.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
                    LooksLikeDiagnosticWearable(pairedDevice))
                    return Array.Empty<(DateTime t, double bpm)>();

                var cutoff = DateTime.UtcNow.AddMinutes(-5);
                return ServiceContainer.WearableTelemetry.RecentSamples
                    .Where(s => s.TimestampUtc >= cutoff &&
                                s.HeartRateBpm is > 25 and < 230 &&
                                string.Equals((s.DeviceId ?? "").Trim(), pairedDevice, StringComparison.OrdinalIgnoreCase) &&
                                !LooksLikeDiagnosticWearable(s.DeviceId) &&
                                !LooksLikeDiagnosticWearable(s.SampleId))
                    .GroupBy(s => string.IsNullOrWhiteSpace(s.SampleId)
                        ? s.TimestampUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : s.SampleId.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(s => s.TimestampUtc).First())
                    .OrderBy(s => s.TimestampUtc)
                    .Select(s => (s.TimestampUtc.ToLocalTime(), s.HeartRateBpm!.Value))
                    .ToList();
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PULSE_UI", "graph sample read failed: " + ex.Message);
                return Array.Empty<(DateTime t, double bpm)>();
            }
        }

        private void UpdatePulseStatsFromSeries(IReadOnlyList<(DateTime t, double bpm)> pts)
        {
            if (pts.Count == 0)
            {
                PulseAvgText.Text = "-- bpm";
                PulsePeakText.Text = "-- bpm";
                PulseLowText.Text = "-- bpm";
                PulseConsistencyText.Text = "--";
                return;
            }

            var avg = pts.Average(p => p.bpm);
            var min = pts.Min(p => p.bpm);
            var max = pts.Max(p => p.bpm);
            var spread = max - min;
            var consistency = pts.Count < 3 ? 100 : Math.Clamp(100 - spread * 2.4, 0, 100);
            PulseAvgText.Text = $"{avg:0.0} bpm";
            PulsePeakText.Text = $"{max:0} bpm";
            PulseLowText.Text = $"{min:0} bpm";
            PulseConsistencyText.Text = $"{consistency:0}%";
        }

        private void DrawPulseHrGraph()
        {
            var canvas = PulseHr24Canvas;
            if (canvas == null) return;
            canvas.Children.Clear();
            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            if (w < 80 || h < 80) return;

            var diagnostics = ServiceContainer.WearableBridge.Diagnostics;
            var pts = GetVerifiedPulseGraphSamples(diagnostics);
            UpdatePulseStatsFromSeries(pts);

            var left = 34.0;
            var right = 14.0;
            var top = 16.0;
            var bottom = 24.0;
            var plotW = Math.Max(1, w - left - right);
            var plotH = Math.Max(1, h - top - bottom);
            var plotBottom = top + plotH;

            DrawPulseChartFrame(canvas, left, top, plotW, plotH);

            if (pts.Count < 2)
            {
                DrawPulseChartEmptyState(canvas, w, h, diagnostics.LastPairedDeviceId);
                return;
            }

            var now = DateTime.Now;
            var windowStart = now.AddMinutes(-5);
            var windowEnd = now;
            var minBpm = Math.Max(35, pts.Min(p => p.bpm) - 6);
            var maxBpm = Math.Min(190, pts.Max(p => p.bpm) + 6);
            var wearableBaseline = ServiceContainer.WearableTelemetry.State.BaselineBpm;
            if (wearableBaseline > 0)
            {
                minBpm = Math.Min(minBpm, wearableBaseline - 6);
                maxBpm = Math.Max(maxBpm, wearableBaseline + 6);
            }
            var range = Math.Max(12, maxBpm - minBpm);

            double MapX(DateTime t)
            {
                var ratio = (t - windowStart).TotalSeconds / Math.Max(1, (windowEnd - windowStart).TotalSeconds);
                return left + Math.Clamp(ratio, 0, 1) * plotW;
            }

            double MapY(double bpm)
            {
                var ratio = (bpm - minBpm) / range;
                return plotBottom - Math.Clamp(ratio, 0, 1) * plotH;
            }

            DrawPulseChartAxes(canvas, left, top, plotW, plotH, minBpm, maxBpm, windowStart, windowEnd);
            if (wearableBaseline > 0 && wearableBaseline >= minBpm && wearableBaseline <= maxBpm)
                DrawPulseBaseline(canvas, left, plotW, MapY(wearableBaseline), wearableBaseline);

            var points = pts
                .Select(p => new System.Windows.Point(MapX(p.t), MapY(p.bpm)))
                .Where(p => p.X >= left && p.X <= left + plotW)
                .ToList();
            if (points.Count < 2)
            {
                DrawPulseChartEmptyState(canvas, w, h, diagnostics.LastPairedDeviceId);
                return;
            }
            points = SmoothAndCompressPulsePoints(points);
            if (points.Count < 2)
            {
                DrawPulseChartEmptyState(canvas, w, h, diagnostics.LastPairedDeviceId);
                return;
            }

            var areaFigure = new PathFigure { StartPoint = new System.Windows.Point(points[0].X, plotBottom), IsClosed = true };
            areaFigure.Segments.Add(new LineSegment(points[0], true));
            areaFigure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
            areaFigure.Segments.Add(new LineSegment(new System.Windows.Point(points[^1].X, plotBottom), true));
            var areaGeometry = new PathGeometry(new[] { areaFigure });
            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = areaGeometry,
                Fill = new SolidColorBrush(MediaColor.FromArgb(28, UiPulse.R, UiPulse.G, UiPulse.B))
            });

            var lineFigure = new PathFigure { StartPoint = points[0], IsClosed = false };
            lineFigure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = new PathGeometry(new[] { lineFigure }),
                Stroke = new SolidColorBrush(UiPulse),
                StrokeThickness = 2.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = UiPulse,
                    ShadowDepth = 0,
                    BlurRadius = 9,
                    Opacity = 0.34
                }
            });

            var last = pts[^1];
            var lastPoint = points[^1];
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(UiPulse),
                Stroke = new SolidColorBrush(MediaColor.FromRgb(0xF7, 0xC8, 0xD0)),
                StrokeThickness = 1.2
            };
            Canvas.SetLeft(dot, lastPoint.X - 5);
            Canvas.SetTop(dot, lastPoint.Y - 5);
            canvas.Children.Add(dot);

            var age = Math.Max(0, (DateTime.Now - last.t).TotalSeconds);
            DrawPulseOverlayLabel(canvas, left + 8, top + 8, $"{last.bpm:0} bpm  /  {age:0}s ago", MediaColor.FromRgb(0xF7, 0xC8, 0xD0));
        }

        private static List<System.Windows.Point> SmoothAndCompressPulsePoints(IReadOnlyList<System.Windows.Point> raw)
        {
            if (raw.Count <= 3)
                return raw.ToList();

            var compressed = raw
                .GroupBy(p => (int)Math.Round(p.X / 4.0))
                .Select(g => new System.Windows.Point(g.Average(p => p.X), g.Average(p => p.Y)))
                .OrderBy(p => p.X)
                .ToList();

            if (compressed.Count <= 3)
                return compressed;

            var smoothed = new List<System.Windows.Point>(compressed.Count) { compressed[0] };
            for (var i = 1; i < compressed.Count - 1; i++)
            {
                var prev = compressed[i - 1];
                var cur = compressed[i];
                var next = compressed[i + 1];
                smoothed.Add(new System.Windows.Point(
                    cur.X,
                    prev.Y * 0.20 + cur.Y * 0.60 + next.Y * 0.20));
            }
            smoothed.Add(compressed[^1]);
            return smoothed;
        }

        private static void DrawPulseChartFrame(Canvas canvas, double left, double top, double plotW, double plotH)
        {
            var frame = new System.Windows.Shapes.Rectangle
            {
                Width = plotW,
                Height = plotH,
                Stroke = new SolidColorBrush(MediaColor.FromArgb(48, UiGrid.R, UiGrid.G, UiGrid.B)),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(MediaColor.FromArgb(18, 6, 12, 18))
            };
            canvas.Children.Add(frame);
            Canvas.SetLeft(frame, left);
            Canvas.SetTop(frame, top);
        }

        private static void DrawPulseChartAxes(
            Canvas canvas,
            double left,
            double top,
            double plotW,
            double plotH,
            double minBpm,
            double maxBpm,
            DateTime windowStart,
            DateTime windowEnd)
        {
            var gridBrush = new SolidColorBrush(MediaColor.FromArgb(28, UiGrid.R, UiGrid.G, UiGrid.B));
            var textBrush = new SolidColorBrush(MediaColor.FromArgb(132, UiMuted.R, UiMuted.G, UiMuted.B));
            var plotBottom = top + plotH;

            for (var i = 0; i <= 4; i++)
            {
                var y = top + plotH * i / 4.0;
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = left,
                    X2 = left + plotW,
                    Y1 = y,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.6,
                    StrokeDashArray = i is 0 or 4 ? null : new DoubleCollection { 3, 5 }
                });
                var bpm = maxBpm - (maxBpm - minBpm) * i / 4.0;
                var label = new TextBlock
                {
                    Text = $"{bpm:0}",
                    FontSize = 8,
                    FontFamily = new WpfFF("Consolas"),
                    Foreground = textBrush
                };
                Canvas.SetLeft(label, 2);
                Canvas.SetTop(label, y - 8);
                canvas.Children.Add(label);
            }

            for (var i = 0; i <= 5; i++)
            {
                var x = left + plotW * i / 5.0;
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = top,
                    Y2 = plotBottom,
                    Stroke = new SolidColorBrush(MediaColor.FromArgb(18, UiGrid.R, UiGrid.G, UiGrid.B)),
                    StrokeThickness = 0.5
                });

                if (i is 0 or 5)
                {
                    var time = windowStart.AddSeconds((windowEnd - windowStart).TotalSeconds * i / 5.0);
                    var label = new TextBlock
                    {
                        Text = time.ToString("HH:mm"),
                        FontSize = 8,
                        FontFamily = new WpfFF("Consolas"),
                        Foreground = textBrush
                    };
                    Canvas.SetLeft(label, Math.Clamp(x - 18, left, left + plotW - 36));
                    Canvas.SetTop(label, plotBottom + 5);
                    canvas.Children.Add(label);
                }
            }
        }

        private static void DrawPulseBaseline(Canvas canvas, double left, double plotW, double y, double baseline)
        {
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = left,
                X2 = left + plotW,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(MediaColor.FromArgb(120, UiInfo.R, UiInfo.G, UiInfo.B)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 6, 5 }
            });
            DrawPulseOverlayLabel(canvas, left + plotW - 92, y - 18, $"base {baseline:0}", UiInfo);
        }

        private static void DrawPulseChartEmptyState(Canvas canvas, double w, double h, string? pairedDevice)
        {
            var text = string.IsNullOrWhiteSpace(pairedDevice)
                ? "waiting for paired Galaxy Watch samples"
                : "waiting for verified live samples";
            DrawPulseOverlayLabel(canvas, Math.Max(20, w * 0.5 - 112), Math.Max(18, h * 0.5 - 12), text, UiMuted);
        }

        private static void DrawPulseOverlayLabel(Canvas canvas, double x, double y, string text, MediaColor color)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontFamily = new WpfFF("Consolas"),
                Foreground = new SolidColorBrush(color)
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }

        // ---- ECG REAL-TIME ANIMATION ----
        private const double EcgFps       = 40.0;  // frames per second
        private const double EcgScrollPps = 120.0; // pixels per second scroll speed

        private void StartEcgAnimation()
        {
            if (_ecgAnimTimer != null) return;

            var canvas = PulseEcgCanvas;
            if (canvas == null) return;
            int bufSize = Math.Max(2, (int)canvas.ActualWidth);
            if (_ecgBuffer.Length != bufSize)
                _ecgBuffer = new double[bufSize];

            _ecgAnimTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / EcgFps)
            };
            _ecgAnimTimer.Tick += EcgAnimTimer_Tick;
            _ecgAnimTimer.Start();
        }

        private void StopEcgAnimation()
        {
            if (_ecgAnimTimer == null) return;
            _ecgAnimTimer.Stop();
            _ecgAnimTimer.Tick -= EcgAnimTimer_Tick;
            _ecgAnimTimer = null;
        }

        private void EcgAnimTimer_Tick(object? sender, EventArgs e)
        {
            var canvas = PulseEcgCanvas;
            if (canvas == null || canvas.ActualWidth < 2) return;

            int bufSize = (int)canvas.ActualWidth;
            if (_ecgBuffer.Length != bufSize)
                _ecgBuffer = new double[bufSize];

            double bpm = Math.Max(30, _ecgLastBpm);

            // How many pixels to scroll this frame
            double pixelsThisFrame = EcgScrollPps / EcgFps;
            // How much of a beat cycle per pixel
            double phasePerPixel = (bpm / 60.0) / EcgScrollPps;

            int steps = Math.Max(1, (int)Math.Round(pixelsThisFrame));
            for (int s = 0; s < steps; s++)
            {
                // Shift buffer left by 1
                Array.Copy(_ecgBuffer, 1, _ecgBuffer, 0, _ecgBuffer.Length - 1);
                // Advance phase
                _ecgPhase = (_ecgPhase + phasePerPixel) % 1.0;
                // Generate ECG sample for this phase
                _ecgBuffer[_ecgBuffer.Length - 1] = EcgSample(_ecgPhase);
            }

            DrawEcgBuffer(canvas, bpm);
        }

        private static double EcgSample(double p)
        {
            // p: 0..1 normalised position within one heartbeat
            // Returns value in [-0.15, 1.0]
            if (p < 0.10) // P wave
                return 0.15 * Math.Sin(p / 0.10 * Math.PI);
            if (p < 0.17) // PR segment
                return 0;
            if (p < 0.20) // Q dip
                return -0.15 * Math.Sin((p - 0.17) / 0.03 * Math.PI);
            if (p < 0.25) // R spike up
                return Math.Sin((p - 0.20) / 0.05 * Math.PI);
            if (p < 0.30) // S dip
                return -0.20 * Math.Sin((p - 0.25) / 0.05 * Math.PI);
            if (p < 0.45) // ST segment
                return 0;
            if (p < 0.65) // T wave
                return 0.30 * Math.Sin((p - 0.45) / 0.20 * Math.PI);
            return 0; // rest (diastole)
        }

        private void DrawEcgBuffer(Canvas canvas, double bpm)
        {
            canvas.Children.Clear();
            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            if (w < 2 || h < 2) return;

            // Grid line at centre
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = 0, X2 = w, Y1 = h * 0.5, Y2 = h * 0.5,
                Stroke = new SolidColorBrush(MediaColor.FromArgb(25, 255, 77, 109)),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 6 }
            });

            var poly = new System.Windows.Shapes.Polyline
            {
                Stroke = new SolidColorBrush(UiPulse),
                StrokeThickness = 1.8,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = UiPulse,
                    ShadowDepth = 0, BlurRadius = 8, Opacity = 0.55
                }
            };

            // Map [-0.20, 1.0] -> [h*0.90, h*0.05]
            const double ampLo = -0.25, ampHi = 1.05;
            double ampRng = ampHi - ampLo;
            double mid = h * 0.5;

            for (int i = 0; i < _ecgBuffer.Length; i++)
            {
                double norm = (_ecgBuffer[i] - ampLo) / ampRng; // 0..1
                double y = h - norm * h * 0.85 - h * 0.075;    // padded
                poly.Points.Add(new System.Windows.Point(i, y));
            }
            canvas.Children.Add(poly);

            // Glowing head dot at the right edge
            var lastY = h - ((_ecgBuffer[^1] - ampLo) / ampRng) * h * 0.85 - h * 0.075;
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(UiPulse),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = UiPulse,
                    ShadowDepth = 0, BlurRadius = 12, Opacity = 0.9
                }
            };
            Canvas.SetLeft(dot, w - 7);
            Canvas.SetTop(dot, lastY - 3);
            canvas.Children.Add(dot);
        }

        private void DrawPulseEcg()
        {
            // Legacy: only called if animation isn't running
            if (_ecgAnimTimer?.IsEnabled == true) return;
            var canvas = PulseEcgCanvas;
            if (canvas == null || _ecgBuffer.Length < 2) return;
            DrawEcgBuffer(canvas, _ecgLastBpm);
        }
    }
}
