using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    // ------------------------------------------------------------
    // VIEW MODELS
    // ------------------------------------------------------------

    // ------------------------------------------------------------
    // MAIN WINDOW
    // ------------------------------------------------------------

    public partial class MainWindow : Window
    {
        // ---- Services ----
        private LlmService         _llm = null!;
        private HealthService      _health = null!;
        private ObsidianMcpService _obsidian = null!;

        // ---- Chat ----
        private CancellationTokenSource _llmCts = new();
        private bool _isGenerating;
        private FrameworkElement? _thinkingElement;
        private TextBlock? _thinkingStatusText;
        private string _lastManualScreenScanRequest = "";
        private DateTime _lastManualScreenScanAt = DateTime.MinValue;
        private int _lastManualScreenScanFailures;

        // ---- Pending image ----
        private byte[]?  _imgBytes;
        private string   _imgMime = "image/jpeg";
        private BitmapImage? _imgThumb;
        private string? _pendingFileContext;

        private readonly List<MemoryCortexNodeVm> _memoryCortexNodes = new();
        private double _memoryCortexZoom = 1.0;

        // ---- Voice ----
        private bool _isRecording;

        // ---- Telegram Bot ----
        private TelegramBotClient? _tgBot;

        private CancellationTokenSource _tgCts = new();
        private readonly ObservableCollection<string> _tgMessages = new();

        // ---- Telegram UserClient (MTProto) ----
        private CancellationTokenSource _tgUserCts = new();

        // ---- Vault ----
        private string? _currentNotePath;

        // ---- Tab ----
        private string _activeTab = "Chat";

        // ---- Dashboard ----
        private DispatcherTimer? _dashTimer;
        private bool _activeDashTabDev = false;
        private readonly ObservableCollection<DashThoughtVm> _dashThoughts    = new();
        private readonly ObservableCollection<string>        _dashCuriosities = new();
        private DateTime _dashLastObsidianSync = DateTime.MinValue;
        private string   _dashLastEmotionSynced = "";
        private DateTime _rightOpsVaultScanAt = DateTime.MinValue;
        private string _rightOpsVaultLine = "vault doctor -";
        private DispatcherTimer? _liveCoreTimer;
        private DispatcherTimer? _uiRepairTimer;
        private DateTime _liveCoreLastVaultScan = DateTime.MinValue;
        private DateTime _lastObsidianPreflightAt = DateTime.MinValue;
        private string _lastObsidianExploreRequest = "";
        private DateTime _lastObsidianExploreAt = DateTime.MinValue;
        private string _lastObsidianTaskRequest = "";
        private string _lastObsidianTaskReply = "";
        private DateTime _lastObsidianTaskAt = DateTime.MinValue;
        private bool _liveCoreVaultScanInFlight;
        private bool _rightOpsVaultScanInFlight;
        private int _liveCoreMemoryItems;
        private int _liveCoreReviewActions;
        private int _liveCoreOpenTasks;
        private bool _agentTaskEventsHooked;
        private bool _agentRuntimeEventsHooked;
        private readonly List<KokoAgentActivitySnapshot> _agentActivityTrace = new();
        private int _agentDetailLevel = 1;
        private WinForms.NotifyIcon? _notifyIcon;
        private CancellationTokenSource? _noticeCts;

        // ---- Session chat log (auto-saved to Obsidian) ----
        // Created on first message, appended after every exchange.
        private string? _sessionChatPath;

        // ------------------------------------------------------------
        // INIT
        // ------------------------------------------------------------

        public MainWindow()
        {
            InitializeComponent();
        }

        private void HookLlmProgress()
        {
            _llm.OnProgress += (type, content) => Dispatcher.InvokeAsync(() =>
            {
                if (type == "thought")
                {
                    _dashThoughts.Insert(0, new DashThoughtVm
                    {
                        Time = DateTime.Now.ToString("HH:mm:ss"),
                        Thought = content,
                        MoodTag = "THINK"
                    });
                    if (_dashThoughts.Count > 50) _dashThoughts.RemoveAt(_dashThoughts.Count - 1);
                }
                else if (type == "tool")
                {
                    _dashThoughts.Insert(0, new DashThoughtVm
                    {
                        Time = DateTime.Now.ToString("HH:mm:ss"),
                        Thought = content,
                        MoodTag = "TOOL"
                    });
                    if (_dashThoughts.Count > 50) _dashThoughts.RemoveAt(_dashThoughts.Count - 1);
                }
            });
        }

        // ---- Fill work area (above taskbar, not WindowState Maximized) ----
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            FitToWorkArea();
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
        }

        private void FitToWorkArea()
        {
            var wa = SystemParameters.WorkArea;
            Left   = wa.Left;
            Top    = wa.Top;
            Width  = wa.Width;
            Height = wa.Height;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                WmGetMinMaxInfo(lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var wa  = SystemParameters.WorkArea;
            mmi.ptMaxPosition.x = (int)wa.Left;
            mmi.ptMaxPosition.y = (int)wa.Top;
            mmi.ptMaxSize.x     = (int)wa.Width;
            mmi.ptMaxSize.y     = (int)wa.Height;
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private void ShowKokonoeMessageNotice(string content)
        {
            var text = TrimNotificationText(content, 180);
            KokoNoticeText.Text = text;
            KokoNoticeBorder.Visibility = Visibility.Visible;
            KokoNoticeBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

            _noticeCts?.Cancel();
            var cts = new CancellationTokenSource();
            _noticeCts = cts;
            _ = HideKokonoeNoticeLaterAsync(cts.Token);

            if (!IsActive || WindowState == WindowState.Minimized)
            {
                FlashTaskbar();
                ShowBalloonNotification(text);
            }
        }

        private async Task HideKokonoeNoticeLaterAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(6500, token);
                await Dispatcher.InvokeAsync(() =>
                {
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                    fade.Completed += (_, _) =>
                    {
                        if (!token.IsCancellationRequested)
                            KokoNoticeBorder.Visibility = Visibility.Collapsed;
                    };
                    KokoNoticeBorder.BeginAnimation(OpacityProperty, fade);
                });
            }
            catch (TaskCanceledException) { }
        }

        private void ShowBalloonNotification(string text)
        {
            try
            {
                _notifyIcon ??= CreateNotifyIcon();
                _notifyIcon.BalloonTipTitle = "Kokonoe";
                _notifyIcon.BalloonTipText = text;
                _notifyIcon.ShowBalloonTip(5000);
            }
            catch { }
        }

        private WinForms.NotifyIcon CreateNotifyIcon()
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Logo", "Logo_icon.ico");
            if (!File.Exists(iconPath))
                iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo", "Logo_icon.ico");

            var icon = File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : System.Drawing.SystemIcons.Information;

            var notify = new WinForms.NotifyIcon
            {
                Icon = icon,
                Text = "Kokonoe",
                Visible = true
            };
            notify.DoubleClick += (_, _) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (WindowState == WindowState.Minimized)
                        WindowState = WindowState.Normal;
                    Activate();
                    Focus();
                });
            };
            return notify;
        }

        private void FlashTaskbar()
        {
            try
            {
                KokoTaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
                _ = Task.Delay(5000).ContinueWith(_ =>
                    Dispatcher.InvokeAsync(() => KokoTaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None));

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                var info = new FLASHWINFO
                {
                    cbSize = Convert.ToUInt32(Marshal.SizeOf<FLASHWINFO>()),
                    hwnd = hwnd,
                    dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
                    uCount = 5,
                    dwTimeout = 0
                };
                FlashWindowEx(ref info);
            }
            catch { }
        }

        private static string TrimNotificationText(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO
        {
            public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_TRAY = 0x00000002;
        private const uint FLASHW_TIMERNOFG = 0x0000000C;

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
        // ------------------------------------------------------------

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Closed += (s, ev) => Cleanup();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPaste));
            _ = StartupSequenceAsync();
            HookAgentTaskEvents();
            SetupHeartUI();
            StartUiTextRepairTimer();
        }

        // ------------------------------------------------------------
        // HEART UI — pulsing dot + BPM, driven by KokoHeartEngine
        // ------------------------------------------------------------
        private Storyboard? _beatStoryboard;
        private readonly Queue<(DateTime t, double bpm)> _heartHistory = new();
        private readonly Queue<double> _heartRR = new(); // last RR intervals (ms)
        private DateTime _lastBeatTime = DateTime.MinValue;

        // ECG real-time animation
        private System.Windows.Threading.DispatcherTimer? _ecgAnimTimer;
        private double[] _ecgBuffer = Array.Empty<double>();
        private double _ecgPhase; // 0..1 within current beat
        private double _ecgLastBpm = 60;

        private void StartLiveCoreMonitor()
        {
            if (_liveCoreTimer != null) return;

            _liveCoreTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            _liveCoreTimer.Tick += (s, e) => UpdateLiveCorePanel();
            _liveCoreTimer.Start();
        }

        private void UpdateLiveCorePanel(bool forceVaultScan = false)
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var emotion = brain.Emotion;
                var state = brain.State;
                var heart = ServiceContainer.Heart;
                var somatic = brain.GetSomaticSnapshot();
                var selfReg = brain.GetSelfRegulationFrame(somatic);
                var telemetry = brain.BuildTelemetrySnapshot();

                LiveCoreEmotionText.Text = $"емоція: {DashboardEmotionLabel(emotion.Current)}".ToUpper();
                LiveCoreEmotionText.Foreground = DashMakeBrush(emotion.Current);
                var attachment = emotion.Attachment;
                LiveCoreBondText.Text = $"довіра {attachment.Trust:F2} | прив'яз. {attachment.CompositeScore():F2} | настрій {state.MoodScore:F2}";

                LiveCoreBodyText.Text = $"{DashboardSomaticLabel(somatic.State).ToUpper()} | напруга {somatic.Strain:F2}";
                LiveCoreRegulationText.Text = $"{DashboardRegulationLabel(selfReg.Reaction)} -> {DashboardRegulationLabel(selfReg.Regulation)} | контроль {selfReg.Control:F2}";

                LiveCorePulseText.Text = heart.CurrentBpm > 0
                    ? $"{heart.CurrentBpm:0} bpm | зміна {heart.BpmDelta:+0;-0;0}"
                    : "-- bpm";
                LiveCoreStrainBar.Value = Math.Clamp(somatic.Strain * 100.0, 0, 100);

                LiveCoreAutonomyText.Text = TrimLiveCoreLine(telemetry.AutonomyDebug, 54);
                LiveCorePresenceText.Text = TrimLiveCoreLine($"{telemetry.Presence} | {telemetry.TimelineState} | state {telemetry.StateFreshness}", 76);
                LiveCoreRhythmText.Text = TrimLiveCoreLine($"{telemetry.Rhythm} | LLM {telemetry.LlmStatus} | guard {telemetry.PostReplyGuard}", 64);

                QueueLiveCoreVaultScan(forceVaultScan);

                LiveCoreMemoryText.Text = $"синхронізація {state.PendingVaultExchangeCount}/5 | пам'ять {_liveCoreMemoryItems}";
                LiveCoreVaultText.Text = $"огляд {_liveCoreReviewActions} | задачі {_liveCoreOpenTasks}";
                if (state.LastAutoVaultSyncAt > DateTime.MinValue)
                    LiveCoreVaultText.Text += $" | sync {state.LastAutoVaultSyncAt:dd.MM HH:mm}";
                if (_lastObsidianPreflightAt > DateTime.MinValue)
                    LiveCoreVaultText.Text += $" | ctx {_lastObsidianPreflightAt:HH:mm:ss}";
                if (!string.IsNullOrWhiteSpace(telemetry.ScenarioHealth))
                    LiveCoreVaultText.Text += $" | {telemetry.ScenarioHealth}";
                LiveCoreCompactText.Text =
                    $"Kokonoe · {DashboardEmotionLabel(emotion.Current).ToLowerInvariant()} · {DashboardSomaticLabel(somatic.State).ToLowerInvariant()} {somatic.Strain:F2} · " +
                    $"{(heart.CurrentBpm > 0 ? $"{heart.CurrentBpm:0} bpm" : "-- bpm")} · vault {state.PendingVaultExchangeCount}/5 · " +
                    $"vision {BuildVisionStatusLabel(state, DateTime.Now).ToLowerInvariant()}";
                if (RightPanel.Visibility == Visibility.Visible)
                    RefreshRightOpsPanel();
                RepairVisibleTextTree(this);
            }
            catch (Exception ex)
            {
                try
                {
                    LiveCoreEmotionText.Text = "емоція: офлайн";
                    LiveCoreBodyText.Text = "соматика: офлайн";
                    LiveCorePulseText.Text = "-- bpm";
                    LiveCoreCompactText.Text = "Kokonoe · offline · -- bpm · vault -- · vision --";
                    LiveCoreAutonomyText.Text = "автономність недоступна";
                    LiveCorePresenceText.Text = "";
                    LiveCoreRhythmText.Text = "";
                    LiveCoreMemoryText.Text = "пам'ять недоступна";
                    LiveCoreVaultText.Text = ex.Message;
                    RepairVisibleTextTree(this);
                }
                catch { }
            }
        }

        private void LiveCoreRefresh_Click(object sender, RoutedEventArgs e)
        {
            UpdateLiveCorePanel(forceVaultScan: true);
        }

        private void QueueLiveCoreVaultScan(bool force)
        {
            var now = DateTime.Now;
            if (!force && now - _liveCoreLastVaultScan <= TimeSpan.FromSeconds(30))
                return;
            if (_liveCoreVaultScanInFlight)
                return;

            _liveCoreVaultScanInFlight = true;
            _ = Task.Run(() =>
            {
                try
                {
                    var quality = _obsidian.AnalyzeMemoryQuality();
                    var queue = _obsidian.BuildTaskQueue();
                    var review = _obsidian.BuildMemoryReview(quality, queue);
                    _liveCoreMemoryItems = quality.NormalizedItems.Count;
                    _liveCoreOpenTasks = queue.OpenTasks.Count;
                    _liveCoreReviewActions = review.Actions.Count;
                    _liveCoreLastVaultScan = DateTime.Now;
                }
                catch { }
                finally
                {
                    _liveCoreVaultScanInFlight = false;
                }
            });
        }

        private void LiveCoreSnapshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateLiveCorePanel(forceVaultScan: true);
                var report = BuildLiveCoreSnapshotMarkdown();
                var path = "Kokonoe/Logs/Live Core.md";
                var existing = _obsidian.ReadNote(path);
                if (string.IsNullOrWhiteSpace(existing))
                {
                    existing = """
---
type: live-core-log
tags: [kokonoe, live-core, diagnostics]
---

# Живе ядро

""";
                }

                _obsidian.WriteNote(path, existing.TrimEnd() + "\n\n" + report);
                LiveCoreVaultText.Text = $"знімок збережено | {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                WMsgBox.Show(ex.Message, "Live Core", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildLiveCoreSnapshotMarkdown()
        {
            var brain = ServiceContainer.BrainEngine;
            var emotion = brain.Emotion;
            var state = brain.State;
            var heart = ServiceContainer.Heart;
            var somatic = brain.GetSomaticSnapshot();
            var selfReg = brain.GetSelfRegulationFrame(somatic);
            var telemetry = brain.BuildTelemetrySnapshot();
            var sb = new StringBuilder();

            sb.AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("| Шар | Значення |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| Емоція | {emotion.Current} / інтенсивність {emotion.Data.Intensity:F2} / зв'язок {emotion.Bond} |");
            sb.AppendLine($"| Прив'язаність | {telemetry.Attachment.Replace("|", "/")} |");
            if (emotion.Secondary.HasValue)
                sb.AppendLine($"| Вторинна емоція | {emotion.Secondary.Value} / {emotion.SecondaryIntensity:F2} |");
            sb.AppendLine($"| Настрій | {state.CurrentMood} / оцінка {state.MoodScore:F2} / база {state.BaselineMood:F2} |");
            sb.AppendLine($"| Тіло | {somatic.State} / {somatic.Label} |");
            sb.AppendLine($"| Пульс | {heart.CurrentBpm:F0} bpm / база {heart.BaselineBpm:F0} / зміна {heart.BpmDelta:+0;-0;0} |");
            sb.AppendLine($"| Соматичне навантаження | strain {somatic.Strain:F2} / calm {somatic.Calm:F2} / volatility {somatic.Volatility:F2} |");
            sb.AppendLine($"| Саморегуляція | {LiveCoreCodeLabel(selfReg.Reaction)} -> {LiveCoreCodeLabel(selfReg.Regulation)} / контроль {selfReg.Control:F2} / імпульс {selfReg.Drive:F2} |");
            sb.AppendLine($"| Автономність | {telemetry.Autonomy.Replace("|", "/")} |");
            sb.AppendLine($"| Autonomy debug | {telemetry.AutonomyDebug.Replace("|", "/")} |");
            sb.AppendLine($"| Presence | {telemetry.Presence.Replace("|", "/")} |");
            sb.AppendLine($"| Timeline | {telemetry.Timeline.Replace("|", "/")} |");
            sb.AppendLine($"| State freshness | {telemetry.StateFreshness.Replace("|", "/")} |");
            sb.AppendLine($"| Внутрішній день | {telemetry.InternalDay.Replace("|", "/")} |");
            sb.AppendLine($"| Ритм | {telemetry.Rhythm.Replace("|", "/")} |");
            sb.AppendLine($"| Self-review | {telemetry.SelfReview.Replace("|", "/")} |");
            sb.AppendLine($"| Post-reply guard | {telemetry.PostReplyGuard.Replace("|", "/")} |");
            sb.AppendLine($"| LLM | {telemetry.LlmStatus.Replace("|", "/")} / {telemetry.LlmProvider} / {telemetry.LlmModel} |");
            if (!string.IsNullOrWhiteSpace(telemetry.LlmLastError))
                sb.AppendLine($"| LLM error | {telemetry.LlmLastError.Replace("|", "/")} |");
            sb.AppendLine($"| Core checks | {telemetry.ScenarioHealth.Replace("|", "/")} |");
            sb.AppendLine($"| Синхронізація пам'яті | очікує {state.PendingVaultExchangeCount}/5 / остання {(state.LastAutoVaultSyncAt > DateTime.MinValue ? state.LastAutoVaultSyncAt.ToString("yyyy-MM-dd HH:mm") : "ніколи")} |");
            sb.AppendLine($"| Vault | пам'ять {_liveCoreMemoryItems} / огляд {_liveCoreReviewActions} / задачі {_liveCoreOpenTasks} |");
            if (!string.IsNullOrWhiteSpace(selfReg.PrivateThought))
                sb.AppendLine($"| Внутрішня думка | {selfReg.PrivateThought.Replace("|", "/")} |");
            if (!string.IsNullOrWhiteSpace(selfReg.BehaviorDirective))
                sb.AppendLine($"| Поведінка | {selfReg.BehaviorDirective.Replace("|", "/")} |");

            return sb.ToString().TrimEnd();
        }

        private static string TrimLiveCoreLine(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "—";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        private static string LiveCoreCodeLabel(string code) => code switch
        {
            "protective_override" => "захисне перевизначення",
            "pulse_spike" => "стрибок пульсу",
            "anger_contained" => "стримане роздратування",
            "combat_focus" => "бойовий фокус",
            "pressure_rise" => "зростання тиску",
            "low_power" => "низький заряд",
            "recovered_calm" => "повернення спокою",
            "steady_calm" => "стабільний спокій",
            "stable_loop" => "стабільний цикл",
            "clean_focus" => "чистий фокус",
            "unknown_body" => "невідомий тілесний сигнал",
            "protect" => "захист",
            "clamp" => "затиск",
            "contain" => "стримування",
            "focus" => "фокус",
            "compress" => "стиснення",
            "conserve" => "збереження ресурсу",
            "release" => "відпускання",
            "baseline" => "базовий режим",
            _ => code
        };

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
                    HeartBpmText.Text = $"{bpm:0} bpm";
                    _ecgLastBpm = bpm;
                    PushHeartSample(bpm);
                    DrawHeartBpmGraph();
                    UpdateHeartStats(bpm);
                    if (PulseTab.Visibility == Visibility.Visible)
                        UpdatePulseTabNumbers();
                });
                heart.Beat += bpm => Dispatcher.InvokeAsync(() =>
                {
                    try { _beatStoryboard!.Begin(this, HandoffBehavior.SnapshotAndReplace, true); }
                    catch { }
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
                Stroke = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x4D, 0x6D)),
                StrokeThickness = 1.8,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromRgb(0xFF, 0x4D, 0x6D),
                    ShadowDepth = 0, BlurRadius = 6, Opacity = 0.6
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
                Fill = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x4D, 0x6D))
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
                catch { }
        }

        private async Task StartupSequenceAsync()
        {
            try
            {
                StartMatrixRain();
                SetLoadingProgress(0, "ініціалізація сервісів...");

                // 1 — init services (background thread)
                await Task.Run(() =>
                {
                    var settings = AppSettings.Load();
                    ServiceContainer.Initialize(settings.VaultPath);
                    Directory.CreateDirectory(settings.VaultPath);
                });

                _llm      = ServiceContainer.LlmService;
                _health   = ServiceContainer.HealthService;
                _obsidian = ServiceContainer.ObsidianMcp;
                HookLlmProgress();

                SetLoadingProgress(20, "завантаження чату...");
                LoadChatHistory();

                SetLoadingProgress(35, "vault...");
                LoadVaultSidebar();

                SetLoadingProgress(50, "календар...");
                LoadCalendarTab();
                LoadToolsTab();

                SetLoadingProgress(65, "telegram...");
                InitTelegram();
                if (AppSettings.Load().TgUserEnabled)
                    _ = InitTelegramUserAsync();

                SetLoadingProgress(75, "мозок...");
                InitBrain();
                StartLiveCoreMonitor();

                TgMessagesList.ItemsSource = _tgMessages;

                SetLoadingProgress(85, "готово...");
                var greeting = GenerateFastGreeting();

                SetLoadingProgress(100, "готово");
                await Task.Delay(150);

                // 3 — ховаємо overlay з анімацією
                await FadeOutLoadingAsync();

                // 4 — додаємо привітання як перше повідомлення чату
                if (!string.IsNullOrEmpty(greeting))
                {
                    AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = greeting });
                    ScrollToBottom();
                }

                InputBox.Focus();
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                WMsgBox.Show($"Startup error: {ex.Message}\n\n{ex.StackTrace}", "Error");
            }
        }

        // ------------------------------------------------------------
        // MATRIX RAIN
        // ------------------------------------------------------------

        private System.Windows.Threading.DispatcherTimer? _matrixTimer;
        private readonly Random _matrixRng = new();
        private readonly List<MatrixColumn> _matrixCols = new();
        private const string MatrixChars = "アイウエオカキクケコサシスセソタチツテトナニヌネノ0123456789ABCDEF∑∆∇∫≈≠∞";

        private class MatrixColumn
        {
            public double X;
            public double Y;
            public double Speed;
            public List<System.Windows.Controls.TextBlock> Cells = new();
            public int Length;
        }

        private void StartMatrixRain()
        {
            MatrixCanvas.Children.Clear();
            _matrixCols.Clear();

            var w = ActualWidth  > 0 ? ActualWidth  : 1400;
            var h = ActualHeight > 0 ? ActualHeight : 900;
            int colCount = (int)(w / 18);

            string customColorStr = AppSettings.Load().MatrixColor;
            System.Windows.Media.Brush customBrush;
            try { customBrush = MakeBrush(customColorStr); }
            catch { customBrush = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"]; }
            
            System.Windows.Media.Brush trailingBrush = customBrush;
            try {
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(customColorStr);
                c.R = (byte)(Math.Max(0, c.R - 100));
                c.G = (byte)(Math.Max(0, c.G - 100));
                c.B = (byte)(Math.Max(0, c.B - 100));
                trailingBrush = new System.Windows.Media.SolidColorBrush(c);
            } catch { }

            for (int i = 0; i < colCount; i++)
            {
                var col = new MatrixColumn
                {
                    X      = i * 18,
                    Y      = _matrixRng.NextDouble() * -h,
                    Speed  = 80 + _matrixRng.NextDouble() * 160,
                    Length = 6 + _matrixRng.Next(12)
                };

                for (int j = 0; j < col.Length; j++)
                {
                    var tb = new System.Windows.Controls.TextBlock
                    {
                        Text       = MatrixChars[_matrixRng.Next(MatrixChars.Length)].ToString(),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize   = 13,
                        Foreground = j == 0 ? System.Windows.Media.Brushes.White : (j < 3 ? customBrush : trailingBrush),
                        Opacity    = j == 0 ? 1.0 : Math.Max(0.1, 1.0 - j * 0.08)
                    };
                    Canvas.SetLeft(tb, col.X);
                    Canvas.SetTop(tb,  col.Y + j * 16);
                    MatrixCanvas.Children.Add(tb);
                    col.Cells.Add(tb);
                }
                _matrixCols.Add(col);
            }

            var last = DateTime.Now;
            _matrixTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            _matrixTimer.Tick += (_, _) =>
            {
                var now   = DateTime.Now;
                var delta = (now - last).TotalSeconds;
                last = now;

                var h2 = MatrixCanvas.ActualHeight > 0 ? MatrixCanvas.ActualHeight : 900;

                foreach (var col in _matrixCols)
                {
                    col.Y += col.Speed * delta;
                    if (col.Y > h2 + 20)
                    {
                        col.Y     = _matrixRng.NextDouble() * -200 - 50;
                        col.Speed = 80 + _matrixRng.NextDouble() * 160;
                    }

                    for (int j = 0; j < col.Cells.Count; j++)
                    {
                        Canvas.SetTop(col.Cells[j], col.Y + j * 16);
                        if (_matrixRng.Next(20) == 0)
                            col.Cells[j].Text = MatrixChars[_matrixRng.Next(MatrixChars.Length)].ToString();
                    }
                }
            };
            _matrixTimer.Start();
        }

        private void StopMatrixRain()
        {
            _matrixTimer?.Stop();
            _matrixTimer = null;
        }

        private void SetLoadingProgress(int pct, string status)
        {
            Dispatcher.InvokeAsync(() =>
            {
                LoadingStatus.Text = status;
                // Animate bar width: 260px total
                LoadingBar.Width = 260.0 * pct / 100.0;
            });
        }

        private async Task FadeOutLoadingAsync()
        {
            StopMatrixRain();
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0,
                TimeSpan.FromMilliseconds(500));
            var tcs = new TaskCompletionSource<bool>();
            anim.Completed += (_, _) => tcs.SetResult(true);
            LoadingOverlay.BeginAnimation(UIElement.OpacityProperty, anim);
            await tcs.Task;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            MatrixCanvas.Children.Clear();
        }

        private async Task<string> GenerateStartupGreetingWithFallbackAsync()
        {
            try
            {
                var now = DateTime.Now;
                var service = new Services.KokoStartupGreetingService();

                // Беремо останні повідомлення З timestamp
                var recent = new List<Services.ChatRepository.ChatMessage>();
                try { recent = ServiceContainer.ChatRepository.GetMessages(12).OrderBy(x => x.Timestamp).TakeLast(8).ToList(); }
                catch { }

                var frame = service.BuildFrame(recent, now);

                var brainObs = "";
                var moodContext = "";
                var presenceContext = "";
                try
                {
                    var st = ServiceContainer.BrainEngine.State;
                    moodContext =
                        $"brainMood={st.PersonalityDailyMood}; moodScore={st.MoodScore:F2}; irritation={st.PersonalityIrritation:F2}; " +
                        $"lastUserTone={st.LastUserEmotionalTone}; lastPresence={st.LastPresenceSummary}; situation={st.LastPresenceSituation}; tone={st.LastPresenceTone}";
                    presenceContext = string.Join(" | ", st.PresenceTrace.TakeLast(4));
                    if (st.Observations.Any())
                        brainObs = $"Твоє останнє спостереження: {st.Observations.TakeLast(2).Last()}\n";
                }
                catch { }

                try
                {
                    var emotion = ServiceContainer.EmotionEngine;
                    var stress = emotion.Stress.TotalLoad();
                    var emotionLine =
                        $"emotion={emotion.Current}; secondary={emotion.Secondary?.ToString() ?? "-"}; " +
                        $"bond={emotion.Bond}; connection={emotion.ConnectionScore:F2}; stress={stress:F2}; pad={emotion.CurrentPad}";
                    moodContext = string.IsNullOrWhiteSpace(moodContext) ? emotionLine : moodContext + "; " + emotionLine;
                }
                catch { }

                service.EnrichFrame(frame, now, moodContext, presenceContext);
                var fallback = service.BuildFallback(frame);

                var prompt = $@"{frame.PromptBlock}
{brainObs}
Напиши стартову репліку повністю через модель. Вона має відчуватись як жива реакція Kokonoe на повернення користувача: врахуй час, паузу, mood, presence і останню тему.
Не пояснюй правила. Не пиши службовий статус. Тільки текст.";

                var task = _llm.SendSystemQueryAsync(prompt, ct: CancellationToken.None);
                var completed = await Task.WhenAny(task, Task.Delay(9000));
                if (completed != task)
                    return fallback;

                return service.Sanitize(await task, frame);
            }
            catch { return GenerateFastGreeting(); }
        }

        private string GenerateFastGreeting()
        {
            try
            {
                var recent = ServiceContainer.ChatRepository.GetMessages(8);
                var service = new Services.KokoStartupGreetingService();
                return service.BuildFallback(service.BuildFrame(recent, DateTime.Now));
            }
            catch
            {
                return "Я на місці. Давай, показуй що сьогодні добиваємо.";
            }
        }

        // ------------------------------------------------------------
        // TAB NAVIGATION
        // ------------------------------------------------------------

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not WButton btn) return;

            // Hide all
            ChatTab.Visibility     = Visibility.Collapsed;
            VaultTab.Visibility    = Visibility.Collapsed;
            HealthTab.Visibility   = Visibility.Collapsed;
            ToolsTab.Visibility    = Visibility.Collapsed;
            TelegramTab.Visibility = Visibility.Collapsed;
            MemoryTab.Visibility   = Visibility.Collapsed;
            PulseTab.Visibility    = Visibility.Collapsed;
            VoiceTab.Visibility    = Visibility.Collapsed;
            SandboxTab.Visibility  = Visibility.Collapsed;
            StopEcgAnimation();

            // Reset tab styles
            TabBtnChat.Style     = (Style)FindResource("BtnTab");
            TabBtnVault.Style    = (Style)FindResource("BtnTab");
            TabBtnHealth.Style   = (Style)FindResource("BtnTab");
            TabBtnTools.Style    = (Style)FindResource("BtnTab");
            TabBtnTelegram.Style = (Style)FindResource("BtnTab");
            TabBtnMemory.Style   = (Style)FindResource("BtnTab");
            TabBtnPulse.Style    = (Style)FindResource("BtnTab");
            TabBtnVoice.Style    = (Style)FindResource("BtnTab");
            TabBtnSandbox.Style  = (Style)FindResource("BtnTab");

            var active = (Style)FindResource("BtnTabActive");

            switch (btn.Name)
            {
                case "TabBtnChat":
                    ChatTab.Visibility = Visibility.Visible;
                    TabBtnChat.Style = active;
                    _activeTab = "Chat";
                    ScrollToBottom();
                    break;
                case "TabBtnVault":
                    VaultTab.Visibility = Visibility.Visible;
                    TabBtnVault.Style = active;
                    _activeTab = "Vault";
                    RefreshNotesList();
                    UpdateMemoryOpsPanel();
                    break;
                case "TabBtnHealth":
                    HealthTab.Visibility = Visibility.Visible;
                    TabBtnHealth.Style = active;
                    _activeTab = "Health";
                    LoadCalendarTab();
                    break;
                case "TabBtnTools":
                    ToolsTab.Visibility = Visibility.Visible;
                    TabBtnTools.Style = active;
                    _activeTab = "Tools";
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                    {
                        DashLoadAll();
                        DashStartTimer();
                        DashUpdateClock();
                    });
                    break;
                case "TabBtnTelegram":
                    TelegramTab.Visibility = Visibility.Visible;
                    TabBtnTelegram.Style = active;
                    _activeTab = "Telegram";
                    break;
                case "TabBtnMemory":
                    MemoryTab.Visibility = Visibility.Visible;
                    TabBtnMemory.Style = active;
                    _activeTab = "Memory";
                    MemTabRefreshData();
                    break;
                case "TabBtnPulse":
                    PulseTab.Visibility = Visibility.Visible;
                    TabBtnPulse.Style = active;
                    _activeTab = "Pulse";
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => UpdatePulseTab());
                    break;
                case "TabBtnVoice":
                    VoiceTab.Visibility = Visibility.Visible;
                    TabBtnVoice.Style = active;
                    _activeTab = "Voice";
                    break;
                case "TabBtnSandbox":
                    SandboxTab.Visibility = Visibility.Visible;
                    TabBtnSandbox.Style = active;
                    _activeTab = "Sandbox";
                    RefreshAgentTaskBoard();
                    break;
            }

            RightPanel.Visibility = (_activeTab == "Chat" || _activeTab == "Tools") ? Visibility.Visible : Visibility.Collapsed;
            UpdateAdaptiveShellLayout();
            if (RightPanel.Visibility == Visibility.Visible)
                RefreshRightOpsPanel();
        }

        private void BodyGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateAdaptiveShellLayout();

        private void UpdateAdaptiveShellLayout()
        {
            if (RightPanel == null || BodyGrid == null) return;

            var canShowRightPanel = _activeTab == "Chat" || _activeTab == "Tools";
            if (!canShowRightPanel)
            {
                RightPanel.Visibility = Visibility.Collapsed;
                return;
            }

            RightPanel.Visibility = BodyGrid.ActualWidth < 1240
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // ------------------------------------------------------------
        // MEMORY TAB
        // ------------------------------------------------------------

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

        private sealed class MemoryCortexNodeVm
        {
            public string Id { get; init; } = "";
            public string Text { get; init; } = "";
            public string Category { get; init; } = "general";
            public float Importance { get; init; }
            public int ConfirmCount { get; init; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Radius { get; set; }
            public SKColor Color { get; init; } = SKColors.White;
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

        private void UpdatePulseTab()
        {
            StartEcgAnimation();
            UpdatePulseTabNumbers();
        }

        private void UpdatePulseTabNumbers()
        {
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart == null) return;
                var cur = heart.CurrentBpm;
                PulseTabBpmBig.Text   = cur > 0 ? $"{cur:0}" : "—";
                PulseTabCurText.Text  = cur > 0 ? $"{cur:0.0}" : "—";
                PulseTabBaseText.Text = $"{heart.BaselineBpm:0.0}";
                PulseSideBpmText.Text = cur > 0 ? $"{cur:0}" : "--";
                try
                {
                    var somatic = ServiceContainer.BrainEngine.GetSomaticSnapshot();
                    var selfReg = ServiceContainer.BrainEngine.GetSelfRegulationFrame(somatic);
                    PulseTabStateLabel.Text = $"{somatic.State.ToUpper()} · {selfReg.Regulation} · strain {somatic.Strain:P0}";
                    PulseSideStateText.Text = $"{somatic.State.ToLowerInvariant()} · baseline {heart.BaselineBpm:0} · strain {somatic.Strain:P0}";
                }
                catch { }

                if (_heartRR.Count >= 4)
                {
                    var arr = _heartRR.ToArray();
                    double sumSq = 0; int n = 0;
                    for (int i = 1; i < arr.Length; i++) { var d = arr[i] - arr[i-1]; sumSq += d*d; n++; }
                    PulseTabHrvText.Text = $"{Math.Sqrt(sumSq / Math.Max(1, n)):0.0}";
                }

                if (_heartHistory.Count > 0)
                {
                    double mn = double.MaxValue, mx = double.MinValue, sum = 0;
                    foreach (var (_, b) in _heartHistory) { if (b < mn) mn = b; if (b > mx) mx = b; sum += b; }
                    var avg = sum / Math.Max(1, _heartHistory.Count);
                    PulseTabMinMaxText.Text = $"{mn:0} / {mx:0}";
                    PulseAvgText.Text = $"{avg:0.0} bpm";
                    PulsePeakText.Text = $"{mx:0} bpm";
                    PulseLowText.Text = $"{mn:0} bpm";
                    var spread = Math.Max(1, mx - mn);
                    PulseConsistencyText.Text = $"{Math.Clamp(100 - spread * 3, 0, 100):0}%";
                }

                PulseVitalLog.ItemsSource = _heartHistory.ToArray().Reverse().Take(30)
                    .Select(h => new { TimeStr = h.t.ToString("HH:mm:ss"), BpmStr = $"{h.bpm:0} bpm" })
                    .ToList();

                UpdatePulseSidePanels(cur);
                DrawPulseHrGraph();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PulseTab] {ex.Message}"); }
        }

        private void UpdatePulseSidePanels(double currentBpm)
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var emotion = brain.Emotion;
                var state = brain.State;
                var annoyed = emotion.Current == KokoEmotionEngine.EmotionState.Irritated
                    ? Math.Clamp(emotion.Data.Intensity * 100, 20, 100)
                    : Math.Clamp((1.0 - state.MoodScore) * 65, 0, 100);
                var curiosity = emotion.Current == KokoEmotionEngine.EmotionState.Curious
                    ? Math.Clamp(emotion.Data.Intensity * 100, 20, 100)
                    : 23.0;
                var trust = Math.Clamp(emotion.ConnectionScore * 100, 0, 100);

                PulseMoodMainText.Text = DashboardEmotionLabel(emotion.Current).ToUpperInvariant();
                PulseMoodDetailText.Text = $"annoyed {annoyed:0}% · curiosity {curiosity:0}% · trust {trust:0}% · sleep depr. {Math.Clamp((1.0 - state.MoodScore) * 100, 0, 100):0}%";
                PulseMoodBar.Value = Math.Clamp(state.MoodScore * 100, 0, 100);

                var status = currentBpm <= 0
                    ? "telemetry standby"
                    : currentBpm > 95
                        ? "core online · memory synced · telemetry elevated"
                        : "core online · memory synced · telemetry recording";
                PulseSystemStatusText.Text = status;

                var now = DateTime.Now;
                var eventBrushHot = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x4D, 0x6D));
                var eventBrushOk = new SolidColorBrush(MediaColor.FromRgb(0x00, 0xE6, 0x76));
                var eventBrushWarn = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xB0, 0x20));
                PulseRecentEvents.ItemsSource = new[]
                {
                    new { Time = now.ToString("HH:mm:ss"), Text = currentBpm > 95 ? "Elevated pulse detected" : "Telemetry recording", Brush = currentBpm > 95 ? eventBrushHot : eventBrushOk },
                    new { Time = now.AddSeconds(-18).ToString("HH:mm:ss"), Text = $"{emotion.Current} mood channel active", Brush = eventBrushWarn },
                    new { Time = now.AddSeconds(-42).ToString("HH:mm:ss"), Text = "Memory sync complete", Brush = eventBrushOk },
                    new { Time = now.AddMinutes(-1).ToString("HH:mm:ss"), Text = "Vision module active", Brush = eventBrushOk },
                    new { Time = now.AddMinutes(-2).ToString("HH:mm:ss"), Text = "Idle state baseline captured", Brush = eventBrushWarn },
                };
            }
            catch { }
        }

        private void DrawPulseHrGraph()
        {
            var canvas = PulseHr24Canvas;
            if (canvas == null) return;
            canvas.Children.Clear();
            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            if (w < 20 || h < 20 || _heartHistory.Count < 2) return;

            var pts = _heartHistory.ToArray();
            double minBpm = Math.Max(35, pts.Min(p => p.bpm) - 8);
            double maxBpm = Math.Min(160, pts.Max(p => p.bpm) + 8);
            double rng = Math.Max(10, maxBpm - minBpm);
            double tMin = pts[0].t.Ticks, tMax = pts[^1].t.Ticks;
            double tRng = Math.Max(1, tMax - tMin);

            foreach (var marker in new[] { 60.0, 90.0, 120.0 })
            {
                if (marker < minBpm || marker > maxBpm) continue;
                var y = h - (marker - minBpm) / rng * h;
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 0, X2 = w, Y1 = y, Y2 = y,
                    Stroke = new SolidColorBrush(MediaColor.FromArgb(28, 255, 255, 255)),
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 3, 5 }
                });
            }

            var poly = new System.Windows.Shapes.Polyline
            {
                Stroke = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x4D, 0x6D)),
                StrokeThickness = 1.5,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = MediaColor.FromRgb(0xFF, 0x4D, 0x6D),
                    ShadowDepth = 0, BlurRadius = 7, Opacity = 0.55
                }
            };
            foreach (var (t, bpm) in pts)
            {
                double x = (t.Ticks - tMin) / tRng * w;
                double y = h - (bpm - minBpm) / rng * h;
                poly.Points.Add(new System.Windows.Point(x, y));
            }
            canvas.Children.Add(poly);

            var last = pts[^1];
            var lx = w;
            var ly = h - (last.bpm - minBpm) / rng * h;
            var dot = new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x4D, 0x6D)) };
            Canvas.SetLeft(dot, lx - 4);
            Canvas.SetTop(dot, ly - 4);
            canvas.Children.Add(dot);
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
                Stroke = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x4D, 0x6D)),
                StrokeThickness = 1.8,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = MediaColor.FromRgb(0xFF, 0x4D, 0x6D),
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
                Fill = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x4D, 0x6D)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = MediaColor.FromRgb(0xFF, 0x4D, 0x6D),
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

        // ------------------------------------------------------------
        // SANDBOX TAB
        // ------------------------------------------------------------

        private async void SandboxRun_Click(object sender, RoutedEventArgs e)
        {
            var prompt = SandboxInput.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) return;
            SandboxOutput.Text = "// running...";
            try
            {
                var result = await _llm.SendAsync(prompt, ct: CancellationToken.None);
                SandboxOutput.Text = result ?? "(empty response)";
            }
            catch (Exception ex) { SandboxOutput.Text = $"// error: {ex.Message}"; }
        }


        private void SandboxClear_Click(object sender, RoutedEventArgs e)
        {
            SandboxInput.Clear();
            SandboxOutput.Text = "?";
        }

        private void AgentCreateTask_Click(object sender, RoutedEventArgs e)
        {
            HookAgentTaskEvents();
            var objective = SandboxInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(objective))
            {
                AgentTaskStatusText.Text = "Put an objective in the prompt box first. Incredible concept, I know.";
                return;
            }

            try
            {
                var task = ServiceContainer.AgentTasks.AddTask(objective);
                AgentTaskStatusText.Text = $"created {task.Id} with {task.Steps.Count} steps";
                RefreshAgentTaskBoard();
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"create failed: {ex.Message}";
            }
        }

        private void AgentStart_Click(object sender, RoutedEventArgs e)
        {
            HookAgentTaskEvents();
            try
            {
                ServiceContainer.AgentTasks.Start();
                AgentTaskStatusText.Text = "runner started";
                RefreshAgentTaskBoard();
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"start failed: {ex.Message}";
            }
        }

        private void AgentStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServiceContainer.AgentTasks.Stop();
                AgentTaskStatusText.Text = "runner stopped";
                RefreshAgentTaskBoard();
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"stop failed: {ex.Message}";
            }
        }

        private void AgentRefresh_Click(object sender, RoutedEventArgs e) => RefreshAgentTaskBoard();

        private void AgentDetailLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _agentDetailLevel = AgentDetailLevelBox?.SelectedIndex ?? 1;
            RefreshAgentTaskBoard();
        }

        private void HookAgentTaskEvents()
        {
            if (!_agentTaskEventsHooked)
            {
                _agentTaskEventsHooked = true;
                ServiceContainer.AgentTasks.ActivityChanged += activity =>
                {
                    Dispatcher.InvokeAsync(() => UpdateAgentActivityPanel(activity), DispatcherPriority.Background);
                };
                ServiceContainer.AgentTasks.TaskCompleted += (task, notice) =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AgentTaskStatusText.Text = notice.Notice;
                        AppendAgentActivity(new KokoAgentActivitySnapshot
                        {
                            UpdatedAt = DateTime.Now,
                            Phase = "report",
                            Tool = "CompletionPolicy",
                            Focus = task.Objective,
                            Thought = notice.Notice,
                            TaskId = task.Id
                        });
                        var visible = BuildVisibleAgentCompletion(task, notice);
                        if (!string.IsNullOrWhiteSpace(visible))
                        {
                            AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = visible });
                            try
                            {
                                ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                                {
                                    Content = visible,
                                    Role = "assistant",
                                    Author = "Kokonoe",
                                    Timestamp = DateTime.Now
                                });
                            }
                            catch { }
                            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("agent", task.Objective, visible));
                        }
                        RefreshAgentTaskBoard();
                    }, DispatcherPriority.Background);
                };
            }

            if (!_agentRuntimeEventsHooked)
            {
                _agentRuntimeEventsHooked = true;
                ServiceContainer.AgentRuntime.ActivityChanged += activity =>
                {
                    Dispatcher.InvokeAsync(() => UpdateAgentActivityPanel(activity), DispatcherPriority.Background);
                };
            }
        }

        private void RefreshAgentTaskBoard()
        {
            try
            {
                HookAgentTaskEvents();
                var board = ServiceContainer.AgentTasks.RenderBoard();
                var snap = ServiceContainer.AgentTasks.GetSnapshot();
                AgentTaskBoardText.Text = RenderAgentBoard(snap, board);
                AgentTaskStatusText.Text = $"tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}";
                UpdateAgentActivityPanel(snap.Activity);
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"refresh failed: {ex.Message}";
            }
        }

        private void UpdateAgentActivityPanel(KokoAgentActivitySnapshot activity)
        {
            AgentPhaseText.Text = $"phase: {activity.Phase}";
            AgentToolText.Text = $"tool: {activity.Tool}";
            AgentFocusText.Text = $"focus: {activity.Focus}";
            AgentThoughtText.Text = $"thought: {activity.Thought}";
            ThoughtStreamStatusText.Text =
                $"agent {activity.Phase} · {activity.Tool} · {TrimLiveCoreLine(activity.Focus, 120)}";
            ThoughtStreamStatusText.ToolTip =
                $"{activity.UpdatedAt:HH:mm:ss} {activity.Phase}/{activity.Tool}\n{activity.Focus}\n{activity.Thought}";
            UpdateAgentEmotionLine();
            AppendAgentActivity(activity);
        }

        private string RenderAgentBoard(KokoAgentTaskSnapshot snap, string fallback)
        {
            if (_agentDetailLevel <= 0)
                return $"tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}\n{snap.Activity.Phase} -> {snap.Activity.Tool}\n{snap.Activity.Thought}";

            if (_agentDetailLevel == 1)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Agent Board | tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}");
                foreach (var task in snap.Tasks.Take(6))
                {
                    var active = task.Steps.OrderBy(s => s.Order)
                        .FirstOrDefault(s => s.Status is KokoAgentTaskStatus.Running or KokoAgentTaskStatus.Pending);
                    var done = task.Steps.Count(s => s.Status == KokoAgentTaskStatus.Completed);
                    sb.AppendLine($"[{task.Status}] {task.Id} p{task.Priority} | {done}/{task.Steps.Count} | {task.Objective}");
                    if (active != null)
                        sb.AppendLine($"  -> {active.Kind}: {active.Title}");
                }
                return sb.ToString();
            }

            return fallback;
        }

        private void UpdateAgentEmotionLine()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var emotion = DashboardEmotionLabel(brain.Emotion.Current);
                var state = brain.State;
                var detail = _agentDetailLevel switch { 0 => "compact", 2 => "verbose", _ => "normal" };
                AgentEmotionStateText.Text = $"emotion: {emotion} | mood {state.MoodScore:F2} | detail: {detail}";
                AgentEmotionStateText.Foreground = DashMakeBrush(brain.Emotion.Current);
            }
            catch
            {
                AgentEmotionStateText.Text = "emotion: offline | detail: ?";
            }
        }

        private void AppendAgentActivity(KokoAgentActivitySnapshot activity)
        {
            if (string.IsNullOrWhiteSpace(activity.Phase) && string.IsNullOrWhiteSpace(activity.Tool))
                return;

            if (_agentActivityTrace.Count > 0)
            {
                var last = _agentActivityTrace[0];
                if (last.Phase == activity.Phase &&
                    last.Tool == activity.Tool &&
                    last.Focus == activity.Focus &&
                    last.Thought == activity.Thought)
                    return;
            }

            _agentActivityTrace.Insert(0, activity);
            if (_agentActivityTrace.Count > 18)
                _agentActivityTrace.RemoveRange(18, _agentActivityTrace.Count - 18);

            var take = _agentDetailLevel switch { 0 => 4, 2 => 14, _ => 8 };
            AgentActivityLogText.Text = string.Join("\n", _agentActivityTrace.Take(take).Select(a =>
                $"[{a.UpdatedAt:HH:mm:ss}] {a.Phase}/{a.Tool} :: {TrimLiveCoreLine(a.Thought, _agentDetailLevel == 2 ? 160 : 90)}"));
        }

        // ------------------------------------------------------------
        // MCP ENHANCED CATALOG
        // ------------------------------------------------------------

        private void McpSyncNotes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var recent = _obsidian.GetNotesModifiedToday();
                McpOutput.Text = recent.Count == 0
                    ? "No notes modified today."
                    : $"Modified today ({recent.Count}):\n" + string.Join("\n", recent);
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        private void RightVaultDoctor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var report = _obsidian.RunVaultDoctor(repair: true);
                var linkProblems = report.FolderWikiLinkCount + report.SuppressedActorLinkCount;
                _rightOpsVaultLine =
                    $"vault doctor {report.HealthScore}/100 · empty {report.EmptyMarkdownFiles.Count} · links {linkProblems} · " +
                    $"fm {report.FrontmatterIssues.Count} · moj {report.MojibakeSuspects.Count} · miss {report.MissingWikiTargets.Count}";
                _rightOpsVaultScanAt = DateTime.Now;
                RightVaultDoctorText.Text = _rightOpsVaultLine;
                McpOutput.Text =
                    $"Vault Doctor ({report.HealthScore}/100)\n" +
                    $"empty: {report.EmptyMarkdownFiles.Count}\n" +
                    $"folder links: {report.FolderWikiLinkCount}\n" +
                    $"Kokonoe links: {report.SuppressedActorLinkCount}\n" +
                    $"frontmatter: {report.FrontmatterIssues.Count}\n" +
                    $"mojibake: {report.MojibakeSuspects.Count}\n" +
                    $"missing targets: {report.MissingWikiTargets.Count}\n" +
                    $"repaired: {report.RepairedFiles.Count}\n" +
                    $"deleted: {report.DeletedEmptyFiles.Count}";
            }
            catch (Exception ex) { McpOutput.Text = $"Vault doctor error: {ex.Message}"; }
        }

        private void RightInspector_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                brain.ExportInspectorToVault();
                McpOutput.Text = brain.BuildInspectorMarkdown();
            }
            catch (Exception ex) { McpOutput.Text = $"Inspector error: {ex.Message}"; }
        }

        private void McpRecentNotes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var notes = _obsidian.ListNotes();
                var recent = notes.Take(20).ToList();
                McpOutput.Text = $"Recent notes ({recent.Count}):\n" + string.Join("\n", recent);
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        private void McpBacklinks_Click(object sender, RoutedEventArgs e)
        {
            var path = Microsoft.VisualBasic.Interaction.InputBox("Note path for backlinks:", "Backlinks", "");
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var links = _obsidian.GetBacklinks(path);
                McpOutput.Text = links.Count == 0
                    ? "No backlinks found."
                    : $"Backlinks ({links.Count}):\n" + string.Join("\n", links);
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        private void McpIsolated_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var isolated = _obsidian.GetIsolatedNotes();
                McpOutput.Text = isolated.Count == 0
                    ? "No isolated notes."
                    : $"Isolated ({isolated.Count}):\n" + string.Join("\n", isolated.Take(30));
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        // ------------------------------------------------------------
        // CHAT — SEND MESSAGE
        // ------------------------------------------------------------

        private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();

        private async void Input_KeyDown(object sender, WKeyArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        private void Input_PreviewKeyDown(object sender, WKeyArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var tb = (WTextBox)sender;
                var pos = tb.SelectionStart;
                tb.Text = tb.Text.Insert(pos, "\n");
                tb.SelectionStart = pos + 1;
                e.Handled = true;
                return;
            }

            // Ctrl+V з зображенням у буфері — перехоплюємо щоб не вставляти текст
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (WClipboard.ContainsImage() || WClipboard.ContainsData(WDataFmts.FileDrop))
                {
                    OnPaste(sender, null!);
                    e.Handled = true;
                }
            }
        }

        private async Task SendMessage()
        {
            var text = InputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) && _imgBytes == null && string.IsNullOrWhiteSpace(_pendingFileContext)) return;
            if (_isGenerating) return;

            // Slash команди для діагностики
            if (text.Equals("/tgtest", StringComparison.OrdinalIgnoreCase))
            {
                InputBox.Clear();
                AddMessageBubble(new ChatMessageVm { Role = "system", Content = "Тестуємо TG..." });
                try
                {
                    if (_tgBot == null) { AddMessageBubble(new ChatMessageVm { Role = "system", Content = "⚠️ _tgBot = null" }); return; }
                    var s2 = AppSettings.Load();
                    await _tgBot.SendMessage(s2.TelegramChatId, "🔧 TG тест від Kokonoe");
                    AddMessageBubble(new ChatMessageVm { Role = "system", Content = "✅ TG працює" });
                }
                catch (Exception ex) { AddMessageBubble(new ChatMessageVm { Role = "system", Content = $"❌ TG error: {ex.Message}" }); }
                return;
            }

            if (text.Equals("/brain", StringComparison.OrdinalIgnoreCase))
            {
                InputBox.Clear();
                AddMessageBubble(new ChatMessageVm { Role = "system", Content = "Запускаємо brain trigger..." });
                ServiceContainer.BrainEngine?.TriggerSpontaneous();
                return;
            }

            _isGenerating = true;
            SendBtn.IsEnabled = false;
            _llmCts = new CancellationTokenSource();

            // Add user bubble
            var baseText = string.IsNullOrWhiteSpace(text) && _imgBytes != null
                ? "Що на фото? Опиши зображення коротко і по суті."
                : text;
            var effectiveText = string.IsNullOrWhiteSpace(_pendingFileContext)
                ? baseText
                : (string.IsNullOrWhiteSpace(baseText) ? "" : baseText + "\n\n") + _pendingFileContext;

            var userVm = new ChatMessageVm { Role = "user", Content = text, ImageThumb = _imgThumb };
            AddMessageBubble(userVm);

            // Save to DB
            try
            {
                ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content = effectiveText, Role = "user", Author = Environment.UserName, Timestamp = DateTime.Now
                });
            }
            catch { }

            InputBox.Clear();
            var imgBytes = _imgBytes;
            var imgMime  = _imgMime;
            var sendText = effectiveText;
            ClearPendingImage();


            // Thinking indicator
            AddThinkingBubble("прийняла повідомлення");
            ScrollToBottom();

            try
            {
                // Ensure UI updates (thinking bubble) before blocking operations
                await ShowKokoActivityAsync("оновлюю стан і наміри");

                // Brain state must observe the user message before context is built.
                // It can touch memory/pattern stores, so keep it off the UI thread.
                await Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(sendText); } catch { }
                }, _llmCts?.Token ?? default);

                if (LooksLikeRetryLastScreenScan(sendText))
                {
                    await ShowKokoActivityAsync("повторюю знімок екрана");
                    var retryText = string.IsNullOrWhiteSpace(_lastManualScreenScanRequest)
                        ? "подивись на екран"
                        : _lastManualScreenScanRequest;
                    await HandleManualScreenScanAsync(retryText, _llmCts?.Token ?? default, isRetry: true);
                    return;
                }

                if (LooksLikeManualScreenScan(sendText))
                {
                    await ShowKokoActivityAsync("роблю знімок екрана");
                    await HandleManualScreenScanAsync(sendText, _llmCts?.Token ?? default);
                    return;
                }

                if (KokoObsidianExplorationService.LooksLikeObsidianWorkRequest(sendText))
                    await ShowKokoActivityAsync("Obsidian: перевіряю vault і читаю нотатки");

                var obsidianCommand = await Task.Run(() =>
                {
                    var handled = TryHandleObsidianCommandOrFollowup(sendText, out var replyText);
                    return (handled, replyText);
                }, _llmCts?.Token ?? default);

                if (obsidianCommand.handled)
                {
                    RemoveThinkingBubble();
                    var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
                    var replyTb = AddMessageBubble(replyVm);
                    if (replyTb != null)
                        await TypeIntoAsync(replyTb, obsidianCommand.replyText, _llmCts?.Token ?? default);
                    else
                        replyVm.Content = obsidianCommand.replyText;

                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = obsidianCommand.replyText,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch { }

                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, obsidianCommand.replyText));
                    return;
                }

                var controlCommand = await TryHandleDirectControlCommandAsync(sendText, _llmCts?.Token ?? default);
                if (controlCommand.Handled)
                {
                    RemoveThinkingBubble();
                    var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
                    var replyTb = AddMessageBubble(replyVm);
                    if (replyTb != null)
                        await TypeIntoAsync(replyTb, controlCommand.Reply, _llmCts?.Token ?? default);
                    else
                        replyVm.Content = controlCommand.Reply;
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = controlCommand.Reply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch { }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, controlCommand.Reply));
                    return;
                }

                // Refresh dynamic personality hint before each LLM call (background)
                _ = Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine?.RefreshPersonalityHint(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] RefreshPersonalityHint: {ex.Message}"); }
                });

                string reply;
                TextBlock? finalReplyTb = null;

                await ShowKokoActivityAsync("збираю контекст і пам'ять");
                var contextTask = Task.Run(() => BuildContext(sendText));

                var agentUi = await RunAgentChatAsync(sendText, imgBytes, imgMime, contextTask, _llmCts?.Token ?? default);
                reply = agentUi.Reply;
                finalReplyTb = agentUi.FinalTextBlock;

                RemoveThinkingBubble();
                var guardUserText = BuildGuardUserText(sendText, imgBytes);
                var guardedReply = await GuardAndRepairReplyAsync(guardUserText, reply, await contextTask, _llmCts?.Token ?? default);
                if (!string.Equals(guardedReply, reply, StringComparison.Ordinal))
                {
                    reply = guardedReply;
                    if (finalReplyTb != null)
                        await Dispatcher.InvokeAsync(() => finalReplyTb.Text = reply, DispatcherPriority.Render);
                }

                UpdateEmotionDot();
                UpdateLiveCorePanel();

                try
                {
                    ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = reply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now
                    });
                }
                catch { }

                // Auto-log this exchange to Obsidian vault for archival memory
                _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, reply));
                _ = Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine?.ObserveExchangeForVaultSync(sendText, reply); } catch { }
                });

                if (AppSettings.Load().TtsEnabled) SpeakAsync(reply);

                // LLM-витяг фактів — тільки після відповіді, щоб не конкурувати за GPU
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var brain = ServiceContainer.BrainEngine;
                        if (brain != null && sendText.Length > 10)
                            await brain.ExtractFactsWithLlmAsync(sendText);
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                RemoveThinkingBubble();
                AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = $"[Error]: {ex.Message}" });
            }
            finally
            {
                RemoveThinkingBubble();
                _isGenerating = false;
                SendBtn.IsEnabled = true;
                ScrollToBottom();
                InputBox.Focus();
            }
        }

        private sealed class AgentChatUiResult
        {
            public string Reply { get; set; } = "";
            public TextBlock? FinalTextBlock { get; set; }
        }

        private async Task<PcIntentExecutionResult> TryHandleDirectControlCommandAsync(string userText, CancellationToken ct)
        {
            try
            {
                string reply;
                if (TryScheduleWakeOrReminder(userText, out reply))
                    return new PcIntentExecutionResult { Handled = true, Reply = reply };

                var pc = await PcIntentRouter.TryExecuteAsync(userText, ServiceContainer.PcControl, ct);
                if (pc.Handled)
                    return pc;

                var brain = ServiceContainer.BrainEngine;
                if (brain != null && brain.TryApplyUserControlCommand(userText, out reply))
                    return new PcIntentExecutionResult { Handled = true, Reply = reply };
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            return new PcIntentExecutionResult { Handled = false };
        }

        private bool TryScheduleWakeOrReminder(string userText, out string reply)
        {
            reply = "";
            if (ReminderCommandParser.TryParse(userText, DateTime.Now, out var reminder))
            {
                ServiceContainer.BrainEngine?.Scheduler.Schedule(
                    reminder.Prompt,
                    reminder.FireAt,
                    KokoSchedulerEngine.Priority.High,
                    "user_reminder");

                var assumption = reminder.UsedAssumedLater
                    ? " «Пізніше» взяла як +30 хв, бо телепатія досі не в релізі."
                    : "";
                reply = $"Поставила на {reminder.FireAt:dd.MM HH:mm}.{assumption} Так, справжній запис у scheduler, не декоративна обіцянка.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(userText)) return false;

            var lower = userText.ToLowerInvariant();
            var wantsWake = ContainsAny(lower, "розбуд", "будиль", "нагад", "remind", "wake");
            if (!wantsWake) return false;

            var match = System.Text.RegularExpressions.Regex.Match(lower, @"(?:о|в|на|at)\s*(\d{1,2})(?::(\d{2}))?");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var hour))
                return false;

            var minute = 0;
            if (match.Groups[2].Success)
                int.TryParse(match.Groups[2].Value, out minute);
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);

            var fireAt = DateTime.Today.AddHours(hour).AddMinutes(minute);
            if (fireAt <= DateTime.Now.AddMinutes(1))
                fireAt = fireAt.AddDays(1);

            var prompt = ContainsAny(lower, "розбуд", "будиль", "wake")
                ? "Розбуди користувача. Коротко, різко, українською. Без пояснення системи."
                : "Нагадай користувачу: " + userText.Trim();
            ServiceContainer.BrainEngine?.Scheduler.Schedule(prompt, fireAt, KokoSchedulerEngine.Priority.High, "user_reminder");
            reply = $"Поставила на {fireAt:dd.MM HH:mm}. Так, справжній запис у scheduler, не декоративна обіцянка.";
            return true;
        }

        private async Task<AgentChatUiResult> RunAgentChatAsync(
            string sendText,
            byte[]? imgBytes,
            string imgMime,
            Task<string> contextTask,
            CancellationToken ct)
        {
            HookAgentTaskEvents();

            var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
            TextBlock? replyTb = null;
            var streamBuffer = new StringBuilder();
            var streamLock = new object();
            var lastStreamUi = DateTime.MinValue;
            var agentId = SelectChatAgentId(sendText);

            var context = await contextTask.ConfigureAwait(true);
            await Dispatcher.InvokeAsync(() =>
            {
                RemoveThinkingBubble();
                replyTb = AddMessageBubble(replyVm);
            }, DispatcherPriority.Render);

            var result = await ServiceContainer.AgentRuntime.ExecuteChatAsync(new KokoAgentChatRequest
            {
                AgentId = agentId,
                UserText = sendText,
                Context = context,
                ImageBytes = imgBytes,
                ImageMime = imgMime,
                PreferStreaming = imgBytes == null,
                OnStatus = status => ShowKokoActivityAsync(status),
                OnChunk = chunk =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        lock (streamLock)
                        {
                            streamBuffer.Append(chunk);
                            if ((DateTime.Now - lastStreamUi).TotalMilliseconds < 55)
                                return;

                            lastStreamUi = DateTime.Now;
                            replyVm.Content = streamBuffer.ToString();
                        }
                        ScrollToBottom();
                    }, DispatcherPriority.Render);
                }
            }, ct).ConfigureAwait(true);

            var reply = result.Reply ?? "";
            if (replyTb == null)
            {
                RemoveThinkingBubble();
                replyTb = AddMessageBubble(replyVm);
            }

            replyVm.Content = reply;
            if (replyTb != null)
            {
                if (result.UsedStreaming)
                    await Dispatcher.InvokeAsync(() => replyTb.Text = reply, DispatcherPriority.Render);
                else
                    await TypeIntoAsync(replyTb, reply, ct);
            }

            return new AgentChatUiResult { Reply = reply, FinalTextBlock = replyTb };
        }

        private static string SelectChatAgentId(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (ContainsAny(lower,
                "код", "code", "script", "python", "c#", "csproj", "xaml", "build", "test",
                "баг", "bug", "exception", "stacktrace", "помилка", "пофікси", "fix",
                "реалізуй", "додай", "команда", "terminal", "powershell", "git"))
                return "coder";

            return "chat";
        }

        private static bool LooksLikeManualScreenScan(string? text)
            => KokoScreenIntent.IsManualScreenScan(text);

        private bool LooksLikeRetryLastScreenScan(string? text)
            => KokoScreenIntent.IsRetryLastScreenScan(text, _lastManualScreenScanRequest, _lastManualScreenScanAt, DateTime.Now);

        private async Task HandleManualScreenScanAsync(string userText, CancellationToken ct, bool isRetry = false)
        {
            string reply;
            try
            {
                reply = await BuildScreenScanReplyAsync(userText, ct, isRetry);
            }
            catch (Exception ex)
            {
                RemoveThinkingBubble();
                AddMessageBubble(new ChatMessageVm
                {
                    Role = "assistant",
                    Content = $"Не змогла зняти екран: {ex.Message}. Нарешті команда була нормальна, а Windows вирішив зобразити меблі."
                });
                _lastManualScreenScanFailures++;
                return;
            }

            RemoveThinkingBubble();
            var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
            var replyTb = AddMessageBubble(replyVm);
            if (replyTb != null)
                await TypeIntoAsync(replyTb, reply, ct);
            else
                replyVm.Content = reply;

            StoreAssistantReply(reply);
            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", userText, reply));
        }

        private async Task<string> BuildScreenScanReplyAsync(string userText, CancellationToken ct, bool isRetry = false)
        {
            if (!isRetry)
            {
                _lastManualScreenScanRequest = userText;
                _lastManualScreenScanFailures = 0;
            }
            _lastManualScreenScanAt = DateTime.Now;

            var screenshot = await Task.Run(() => ServiceContainer.PcControl.TakeScreenshot(), ct);
            var prompt = BuildScreenScanPrompt(userText);
            string? reply;
            using (var visionCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                visionCts.CancelAfter(TimeSpan.FromSeconds(90));
                reply = await _llm.SendSystemVisionQueryAsync(prompt, screenshot, "image/jpeg", visionCts.Token);
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                _lastManualScreenScanFailures++;
                reply = await BuildScreenScanFallbackReplyAsync(screenshot, _lastManualScreenScanFailures, userText, ct);
            }
            else
            {
                _lastManualScreenScanFailures = 0;
            }

            return await GuardAndRepairReplyAsync(userText, reply, "", ct);
        }

        private static string BuildVisibleAgentCompletion(KokoAgentTask task, KokoAgentCompletionNotice notice)
        {
            if (task.Steps.All(s => s.Kind != KokoAgentStepKind.InsightExtraction) && task.Priority < 6)
                return "";

            var result = task.Steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Result))
                .OrderByDescending(s => s.FinishedAt ?? DateTime.MinValue)
                .Select(s => s.Result.Trim())
                .FirstOrDefault() ?? notice.Notice;

            var prefix = task.Steps.Any(s => s.Kind == KokoAgentStepKind.InsightExtraction)
                ? "Фоновий vault-скан завершено."
                : "Задача завершена.";
            return prefix + "\n" + TrimOpsLine(result, 900);
        }

        private async Task<string> BuildScreenScanFallbackReplyAsync(
            byte[] screenshot,
            int failureCount,
            string userText,
            CancellationToken ct)
        {
            var diag = _llm.GetDiagnosticsSnapshot();
            var activity = new ActivityAnalyzer().AnalyzeScreenshot(screenshot);
            var window = string.IsNullOrWhiteSpace(activity.ActiveWindowTitle)
                ? "активне вікно не визначилось"
                : activity.ActiveWindowTitle.Trim();
            var status = diag.LastStatusCode.HasValue ? diag.LastStatusCode.Value.ToString() : "no-status";
            var fallback = string.IsNullOrWhiteSpace(diag.LastFallback) ? "vision_empty" : diag.LastFallback;
            var repeat = failureCount > 1
                ? $"Це {failureCount}-й порожній результат підряд, тому я не робитиму вигляд, що картинку прочитано."
                : "Це перший порожній результат у цій серії.";
            var prompt = $"""
Ти Kokonoe. Локальний інструмент зробив скріншот, але візуальний аналіз повернув порожній текст.
Склади коротку відповідь користувачу українською, 2-4 речення.

Факти, які можна використати:
- Запит користувача: {userText}
- Скріншот реально зроблено: так
- Активне вікно з локальної ОС: {window}
- Візуальний аналізатор не дав опису: status={status}, route={fallback}, provider={diag.Provider}, model={diag.Model}
- Повторів у цій серії: {failureCount}

Правила:
- Не вигадуй, що саме видно на скріншоті.
- Не кажи "я не маю доступу до екрана" і не проси користувача завантажити скріншот: скрін уже є.
- Не пиши службові слова provider/model/status, якщо не потрібно пояснити збій.
- Не цитуй дослівно запит користувача.
- Звучить як Kokonoe: сухо, чесно, без канцеляриту.
""";

            try
            {
                using var fallbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                fallbackCts.CancelAfter(TimeSpan.FromSeconds(18));
                var generated = await Task.Run(
                    () => _llm.SendSystemQueryAsync(prompt, ct: fallbackCts.Token),
                    fallbackCts.Token);
                generated = LlmService.RepairMojibake(generated ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(generated) &&
                    !LlmService.LooksLikeBrokenVisibleText(generated) &&
                    !KokoScreenIntent.LooksLikeScreenCapabilityDenial(generated))
                    return generated;
            }
            catch
            {
                // Last-resort text below is deliberately factual; no fake image reading.
            }

            return $"Скріншот зроблено, але опис зображення не повернувся. З локальних даних бачу тільки активне вікно: {window}. {repeat}";
        }

        private static string BuildScreenScanPrompt(string userText) => $"""
Ти Kokonoe. Користувач прямо попросив просканувати його поточний екран.
Запит користувача: {userText}

Завдання:
- Подивись на скріншот і скажи, що реально видно.
- Якщо видно чат/програму/помилку/код/порожній стан, назви це прямо.
- Якщо видно цей же чат KokonoeAssistant, поясни конкретний стан інтерфейсу, а не відмовляйся від аналізу.
- Не вигадуй прихованих мотивів користувача.
- Не кажи, що ти не маєш доступу до екрана: у цьому запиті скріншот уже зроблено локальним інструментом.
- Не кажи "екран просканований" як службовий штамп; дай корисне спостереження.
- Не переписуй приватні токени, ключі, email, телефони або довгі приватні рядки.
- Українською, 2-5 речень, стиль Kokonoe: сухо, розумно, без канцеляриту.
""";

        private static void StoreAssistantReply(string reply)
        {
            try
            {
                ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content = reply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now
                });
            }
            catch { }
        }

        private string BuildContext(string? userText = null)
        {
            // УВАГА: цей контекст додається до ~6k токенів system prompt + TOOLS.
            // Тримаємо його компактним — максимум ~800 токенів (~3200 символів).
            // GetToolsDescription() ПРИБРАНО — опис тулзів вже є в TOOLS масиві LlmService.

            const int MAX_CONTEXT_LENGTH = 5000;
            var parts = new List<(string content, int priority)>(); // priority: lower = more important
            try
            {
                var temporal = BuildTemporalAwarenessContext(userText);
                if (!string.IsNullOrWhiteSpace(temporal))
                    parts.Add((temporal, 0));
            }
            catch { }

            parts.Add((BuildLiveResponseStyleContext(userText), 0));

            try
            {
                var responsePlan = ServiceContainer.BrainEngine?.BuildResponsePlanContext(userText);
                if (!string.IsNullOrWhiteSpace(responsePlan))
                    parts.Add((responsePlan, 0));
            }
            catch { }

            try
            {
                var selfReview = ServiceContainer.BrainEngine?.BuildSelfReviewContext(userText);
                if (!string.IsNullOrWhiteSpace(selfReview))
                    parts.Add((selfReview, 0));
            }
            catch { }

            try
            {
                var timeline = ServiceContainer.BrainEngine?.BuildTimelineContext(userText);
                if (!string.IsNullOrWhiteSpace(timeline))
                    parts.Add((timeline, 0));
            }
            catch { }

            try
            {
                var preflight = BuildObsidianPreflightContext(userText);
                if (!string.IsNullOrWhiteSpace(preflight))
                {
                    _lastObsidianPreflightAt = DateTime.Now;
                    parts.Add((preflight, 0));
                }
            }
            catch { }

            // 1. Релевантна пам'ять — НАЙВАЖЛИВІШЕ, бо це факти про користувача
            try
            {
                var mem = ServiceContainer.BrainEngine?.Memory;
                if (mem != null && !string.IsNullOrWhiteSpace(userText))
                {
                    var (facts, episodes) = mem.FindRelevantBlocking(userText, maxFacts: 5, maxEpisodes: 2);
                    var memParts = new List<string>();
                    foreach (var f in facts)
                        memParts.Add(f.Content);
                    foreach (var e in episodes)
                        memParts.Add($"[{e.When:dd.MM.yy}] {e.Summary}");
                    if (memParts.Count > 0)
                        parts.Add(("=== ПАМ'ЯТЬ ===\n" + string.Join("\n", memParts), 1));
                }
            }
            catch { }

            // 1.5 Емоційний стан — безпосередньо впливає на тон відповіді
            try
            {
                var emotHint = ServiceContainer.BrainEngine?.Emotion?.GetPromptHint();
                if (!string.IsNullOrEmpty(emotHint))
                    parts.Add((emotHint, 2));

                var condition = BuildKokoConditionContext();
                if (!string.IsNullOrWhiteSpace(condition))
                    parts.Add((condition, 2));
            }
            catch { }

            // 2. Стан (емоції, здоров'я, scheduler) — важливо але менше
            try
            {
                var stateCtx = ServiceContainer.StateEngine.GetStateAsContext();
                if (!string.IsNullOrEmpty(stateCtx))
                    parts.Add((stateCtx, 3));
            }
            catch { }

            // 2.1 Пульс — завжди в контексті щоб Kokonoe реагувала на зміни
            try
            {
                var heart = ServiceContainer.Heart;
                var bpm = heart.CurrentBpm;
                var baseline = heart.BaselineBpm;
                var diff = bpm - baseline;
                var bpmNote = diff > 15 ? " ↑ підвищений" : diff < -10 ? " ↓ нижче базового" : "";
                parts.Add(($"=== ПУЛЬС ===\nПоточний: {bpm:0.0} bpm{bpmNote} | Базовий: {baseline:0.0} bpm", 2));
            }
            catch { }

            // 2.5 Календар — найближчі важливі дати
            try
            {
                var cal = ServiceContainer.Calendar;
                var upcoming = cal.GetUpcoming(14); // наступні 2 тижні
                var today = cal.GetForDay(DateTime.Today);
                if (today.Any() || upcoming.Any())
                {
                    var calLines = new List<string>();
                    if (today.Any())
                        calLines.Add("Сьогодні: " + string.Join(", ", today.Select(e => e.Title)));
                    var soon = upcoming.Where(e => e.EventAt.Date > DateTime.Today).Take(3);
                    if (soon.Any())
                        calLines.Add("Найближче: " + string.Join("; ", soon.Select(e => $"{e.Title} {e.EventAt:dd.MM}")));
                    parts.Add(("=== КАЛЕНДАР ===\n" + string.Join("\n", calLines), 2));
                }
            }
            catch { }

            // 3. Здоров'я — агреговані показники за тиждень
            try
            {
                var healthCtx = ServiceContainer.HealthService.GetHealthContext();
                if (!string.IsNullOrWhiteSpace(healthCtx) && !healthCtx.StartsWith("No health data"))
                    parts.Add((healthCtx.Trim(), 4));
            }
            catch { }

            // 3.1 Цілі та активні звички
            try
            {
                var goals = ServiceContainer.GoalService.GetActiveGoals();
                var habits = ServiceContainer.HabitService.GetActiveHabits();
                if (goals.Count > 0 || habits.Count > 0)
                {
                    var ghLines = new System.Collections.Generic.List<string>();
                    if (goals.Count > 0)
                        ghLines.Add("Цілі: " + string.Join(", ", goals.Take(4).Select(g => $"{g.Title} ({g.Progress:0}%)")));
                    if (habits.Count > 0)
                        ghLines.Add("Звички: " + string.Join(", ", habits.Take(5).Select(h => h.Name)));
                    parts.Add(("=== ЦІЛІ/ЗВИЧКИ ===\n" + string.Join("\n", ghLines), 5));
                }
            }
            catch { }

            // 3.2 Когнітивний контекст
            try
            {
                var cogCtx = ServiceContainer.BrainEngine?.Cognition?.BuildCognitionContext();
                if (!string.IsNullOrEmpty(cogCtx))
                    parts.Add((cogCtx, 5));
            }
            catch { }

            // 3.3 EnhancedMemory — структуровані факти про користувача
            try
            {
                var enhMem = ServiceContainer.EnhancedMemory.GetMemoryAsContext();
                if (!string.IsNullOrWhiteSpace(enhMem))
                {
                    if (enhMem.Length > 800) enhMem = enhMem[..800];
                    parts.Add((enhMem.Trim(), 6));
                }
            }
            catch { }

            // 4. Vault — ключові нотатки
            try
            {
                var allNotes = _obsidian.ListNotes();
                var vaultLines = new List<string>();

                var keyNotes = allNotes
                    .Where(n => n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("brain-core", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Досьє", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Facts", StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .ToList();
                if (keyNotes.Count > 0)
                    vaultLines.Add("Ключові: " + string.Join(", ", keyNotes));

                var todayNote = $"Daily/{DateTime.Now:yyyy-MM-dd}.md";
                if (allNotes.Any(n => n == todayNote))
                    vaultLines.Add("Daily: є");

                if (vaultLines.Count > 0)
                    parts.Add(("=== VAULT ===\n" + string.Join(" | ", vaultLines), 7));
            }
            catch { }

            // 4.1 Relevant vault recall - direct Obsidian retrieval for the current message
            try
            {
                if (!string.IsNullOrWhiteSpace(userText))
                {
                    var relevant = _obsidian.SearchSemantic(userText, 4)
                        .Where(r => !string.IsNullOrWhiteSpace(r.Preview))
                        .Take(4)
                        .ToList();
                    if (relevant.Count > 0)
                    {
                        var lines = relevant.Select(r =>
                            $"- {r.Path}: {TruncateAtWordBoundary(SanitizeForLlm(r.Preview).Replace("\r", " ").Replace("\n", " "), 240)}");
                        parts.Add(("=== RELEVANT OBSIDIAN MEMORY ===\n" + string.Join("\n", lines), 2));
                    }
                }
            }
            catch { }

            // 4.2 Managed task queue / memory health - compact operational state
            try
            {
                var taskQueue = _obsidian.ReadNote("Kokonoe/Tasks Queue.md");
                var memoryQuality = _obsidian.ReadNote("Kokonoe/Memory/Quality.md");
                var memoryReview = _obsidian.ReadNote("Kokonoe/Memory/Review.md");
                var ops = new List<string>();
                if (!string.IsNullOrWhiteSpace(taskQueue))
                    ops.Add(TruncateAtWordBoundary(SanitizeForLlm(taskQueue), 700));
                if (!string.IsNullOrWhiteSpace(memoryQuality))
                    ops.Add(TruncateAtWordBoundary(SanitizeForLlm(memoryQuality), 500));
                if (!string.IsNullOrWhiteSpace(memoryReview))
                    ops.Add(TruncateAtWordBoundary(SanitizeForLlm(memoryReview), 500));
                if (ops.Count > 0)
                    parts.Add(("=== KOKONOE MEMORY OPS ===\n" + string.Join("\n\n", ops), 6));
            }
            catch { }

            // 5. Прогноз (ML.NET) — настрій/енергія на завтра
            try
            {
                var forecast = ServiceContainer.Predictor.GetForecastContext();
                if (!string.IsNullOrEmpty(forecast))
                    parts.Add((forecast.Trim(), 8));
            }
            catch { }

            // Сортуємо за пріоритетом і збираємо результат
            var orderedParts = parts.OrderBy(p => p.priority).Select(p => p.content).ToList();
            var result = string.Join("\n\n", orderedParts);

            // Розумне обрізання: видаляємо останні секції доки вміщаємось
            while (result.Length > MAX_CONTEXT_LENGTH && orderedParts.Count > 1)
            {
                orderedParts.RemoveAt(orderedParts.Count - 1);
                result = string.Join("\n\n", orderedParts);
            }

            // Якщо й одна секція завелика — обрізаємо на межі слова
            if (result.Length > MAX_CONTEXT_LENGTH)
            {
                result = TruncateAtWordBoundary(result, MAX_CONTEXT_LENGTH);
            }

            return result;
        }

        private static string BuildLiveResponseStyleContext(string? userText)
        {
            var trimmed = userText?.Trim() ?? "";
            var concrete = !string.IsNullOrWhiteSpace(trimmed)
                ? $"Останній вхід користувача: «{trimmed[..Math.Min(220, trimmed.Length)]}»."
                : "Останній вхід користувача порожній або службовий.";

            return $"""
LIVE RESPONSE STYLE
{concrete}
Правила живої відповіді:
- спершу реагуй на конкретику останнього повідомлення, не на абстрактний настрій;
- не починай з декоративної ремарки в *зірочках*, якщо користувач сам не почав roleplay;
- не вигадуй «монітор блимає», «датчики», «лабораторію», «тіло реагує» без прямої причини;
- не вигадуй зовнішні факти про користувача: акаунти, YouTube/Twitch/Discord, мемберства, підписки, роботу, покупки або людей, якщо цього нема в чаті;
- не психологізуй: не вигадуй, що користувач боїться сказати, що в нього щось застрягло в голові, або що ти «дивишся через екран»;
- якщо питають про тебе/Коконое — дай прямий опис характеру чи позиції, без терапевтичних питань у відповідь;
- якщо питають "що ти знаєш про мене" або про пам'ять/профіль — синтезуй відповідь з Vault/Obsidian контексту живою мовою; не показуй назви файлів і не рапортуй "перевірила Creator/Profile.md", якщо користувач не питає джерело;
- не підміняй profile/memory питання старим sleep-наміpом, технічним fallback'ом або готовим шаблоном;
- допускається суха іронія, але вона має бути прив'язана до події, часу, наміру або питання;
- краще одна точна фраза, ніж театральна сцена з трьома шарами декорацій.
""";
        }

        private static string BuildTemporalAwarenessContext(string? userText)
        {
            var now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine("=== ЧАСОВИЙ КОНТЕКСТ — КРИТИЧНО ===");
            sb.AppendLine($"Поточний локальний час: {now:yyyy-MM-dd HH:mm}.");

            try
            {
                var recent = ServiceContainer.ChatRepository.GetMessages(20)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                var lastUserBeforeCurrent = recent
                    .Where(m => m.Role == "user" &&
                                !string.Equals((m.Content ?? "").Trim(), (userText ?? "").Trim(), StringComparison.Ordinal))
                    .LastOrDefault();
                var currentUser = recent.LastOrDefault(m => m.Role == "user");

                if (lastUserBeforeCurrent != null)
                {
                    var gap = now - lastUserBeforeCurrent.Timestamp;
                    sb.AppendLine($"Попереднє повідомлення користувача: {lastUserBeforeCurrent.Timestamp:yyyy-MM-dd HH:mm} ({FormatGapUa(gap)} тому): \"{TruncateAtWordBoundary(SanitizeForLlm(lastUserBeforeCurrent.Content ?? ""), 180)}\"");
                }

                if (currentUser != null)
                    sb.AppendLine($"Поточне повідомлення користувача: {currentUser.Timestamp:yyyy-MM-dd HH:mm}: \"{TruncateAtWordBoundary(SanitizeForLlm(currentUser.Content ?? userText ?? ""), 180)}\"");
            }
            catch { }

            try
            {
                var intents = ServiceContainer.BrainEngine?.GetActiveShortTermIntents(4);
                if (intents != null)
                {
                    foreach (var intent in intents)
                    {
                        var age = now - intent.CreatedAt;
                        var due = now >= intent.FollowUpAt ? "follow-up уже доречний" : $"follow-up через {FormatGapUa(intent.FollowUpAt - now)}";
                        sb.AppendLine($"Активний короткостроковий намір: {intent.Summary}; сказано {FormatGapUa(age)} тому; очікувано до {intent.ExpectedUntil:HH:mm}; {due}; джерело: \"{TruncateAtWordBoundary(SanitizeForLlm(intent.SourceText), 160)}\"");
                    }
                }
            }
            catch { }

            var lower = (userText ?? "").ToLowerInvariant();
            var isMorningNow = now.Hour is >= 5 and < 13;
            var saysWakeOrMorning = ContainsAny(lower, "ранку", "добрий ранок", "доброго ранку", "проснув", "прокинув", "поспав", "встав");
            var saysGoingSleep = ContainsAny(lower, "спать піду", "спати піду", "йду спати", "іду спати", "спокійної", "до ранку");

            if (isMorningNow && saysWakeOrMorning)
            {
                sb.AppendLine("ВИСНОВОК: зараз ранок і користувач уже прокинувся/пише після сну.");
                sb.AppendLine("ЗАБОРОНА: не кажи йому \"спи\", \"йди спати\", \"до ранку\" або подібне. Це застарілий контекст з минулої ночі.");
                sb.AppendLine("Правильна реакція: визнай ранок/пробудження, можеш саркастично прокоментувати, але не відправляй його назад спати.");
            }
            else if (saysGoingSleep && (now.Hour is >= 21 or < 5))
            {
                sb.AppendLine("ВИСНОВОК: користувач прямо каже, що йде спати в нічний час. Коротке побажання сну доречне.");
            }

            sb.AppendLine("Правило: завжди звіряй поточний час і часовий розрив між повідомленнями перед порадою про сон.");
            sb.AppendLine("Правило: якщо є активний короткостроковий намір, використовуй його для природного follow-up: курси вже закінчились, робота ще триває, прогулянка завершилась тощо.");
            return sb.ToString();
        }

        private static string FormatGapUa(TimeSpan gap)
        {
            if (gap.TotalMinutes < 1) return "щойно";
            if (gap.TotalHours < 1) return $"{(int)gap.TotalMinutes} хв";
            if (gap.TotalDays < 1) return $"{(int)gap.TotalHours} год {(int)gap.Minutes} хв";
            return $"{(int)gap.TotalDays} дн {(int)gap.Hours} год";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private bool TryHandleObsidianCommandOrFollowup(string text, out string reply)
        {
            if (TryHandleObsidianExplorationFollowup(text, out reply))
                return true;

            return TryHandleDirectObsidianCommand(text, out reply);
        }

        private bool TryHandleObsidianExplorationFollowup(string text, out string reply)
        {
            reply = "";
            if (string.IsNullOrWhiteSpace(text) || _obsidian == null)
                return false;
            if (!KokoObsidianExplorationService.LooksLikeExplorationFollowup(text))
                return false;

            var request = FindRecentObsidianExplorationRequest();
            if (string.IsNullOrWhiteSpace(request))
                return false;

            reply = BuildObsidianExplorationReply(request);
            return true;
        }

        private string FindRecentObsidianExplorationRequest()
        {
            var now = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(_lastObsidianExploreRequest) &&
                now - _lastObsidianExploreAt <= TimeSpan.FromMinutes(60))
            {
                return _lastObsidianExploreRequest;
            }

            try
            {
                return ServiceContainer.ChatRepository
                    .GetMessages(30)
                    .Where(m => m.Role == "user" && now - m.Timestamp <= TimeSpan.FromMinutes(60))
                    .OrderByDescending(m => m.Timestamp)
                    .Select(m => m.Content ?? "")
                    .FirstOrDefault(KokoObsidianExplorationService.LooksLikeInterestingVaultDive)
                    ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string BuildObsidianExplorationReply(string request)
        {
            _lastObsidianExploreRequest = request ?? "";
            _lastObsidianExploreAt = DateTime.Now;
            return new KokoObsidianExplorationService().BuildInterestingFinds(_obsidian, request);
        }

        private string BuildObsidianAccessReply(string request)
        {
            _lastObsidianTaskRequest = request ?? "";
            _lastObsidianTaskAt = DateTime.Now;
            _lastObsidianTaskReply = new KokoObsidianExplorationService().BuildAccessReport(_obsidian, request);
            return _lastObsidianTaskReply;
        }

        private bool TryHandleDirectObsidianCommand(string text, out string reply)
        {
            reply = "";
            if (string.IsNullOrWhiteSpace(text) || _obsidian == null)
                return false;

            var lower = text.ToLowerInvariant();
            var looksObsidian =
                ContainsAny(lower, "obsidian", "vault", "обсидіан", "обсідіан", "обсидиан", "папк", "нотатк", "журнал", "щоденник", "journal", "spanish", "lesson_", "lesson", "урок");
            if (KokoObsidianExplorationService.LooksLikeVaultAccessCheck(text))
            {
                reply = BuildObsidianAccessReply(text);
                return true;
            }

            if (KokoObsidianExplorationService.LooksLikeInterestingVaultDive(text))
            {
                reply = BuildObsidianExplorationReply(text);
                return true;
            }

            var wantsMutation =
                ContainsAny(lower, "створ", "созд", "create", "зроби", "запиш", "збереж", "нема", "немає");
            var wantsCheck =
                ContainsAny(lower, "перевір", "провір", "check", "існує", "бачиш");

            if (!looksObsidian || (!wantsMutation && !wantsCheck))
                return false;

            try
            {
                var paths = InferObsidianTargets(text);
                if (paths.Count == 0)
                {
                    return false;
                }

                var report = new List<string>();
                foreach (var target in paths)
                {
                    if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    {
                        var existing = _obsidian.ReadNote(target);
                        if (wantsMutation && string.IsNullOrWhiteSpace(existing))
                            _obsidian.WriteNote(target, BuildDefaultObsidianNote(target));

                        var verified = !string.IsNullOrWhiteSpace(_obsidian.ReadNote(target));
                        report.Add($"{(verified ? "є" : "немає")} нотатка `{target}`");
                    }
                    else
                    {
                        if (wantsMutation)
                            _obsidian.CreateFolder(target);

                        var folderFull = Path.Combine(_obsidian.VaultPath, target.Replace('/', Path.DirectorySeparatorChar));
                        var verified = Directory.Exists(folderFull);
                        report.Add($"{(verified ? "є" : "немає")} папка `{target}`");
                    }
                }

                reply = "Перевірила через файлову систему, не через уяву. " + string.Join("; ", report) + ".";
                return true;
            }
            catch (Exception ex)
            {
                reply = $"Obsidian-операція впала: {ex.Message}. Оце вже реальна помилка, а не театр про «я створила».";
                return true;
            }
        }

        private static List<string> InferObsidianTargets(string text)
        {
            var targets = new List<string>();
            var cleaned = text
                .Replace("→", "/", StringComparison.Ordinal)
                .Replace("вћњ", "/", StringComparison.Ordinal)
                .Replace("->", "/", StringComparison.Ordinal)
                .Replace("\\", "/", StringComparison.Ordinal)
                .Replace("**", "", StringComparison.Ordinal)
                .Replace("`", "", StringComparison.Ordinal)
                .Replace("$rightarrow$", "/", StringComparison.OrdinalIgnoreCase);

            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         cleaned,
                         @"(?<![\p{L}\p{N}_])([\p{L}\p{N}][\p{L}\p{N}_ .-]*(?:/[\p{L}\p{N}][\p{L}\p{N}_ .-]*)+(\.md)?)",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                var path = NormalizeObsidianRelPath(m.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(path) && !targets.Contains(path, StringComparer.OrdinalIgnoreCase))
                    targets.Add(path);
            }

            var lower = cleaned.ToLowerInvariant();
            if (lower.Contains("journal", StringComparison.OrdinalIgnoreCase) &&
                lower.Contains("spanish", StringComparison.OrdinalIgnoreCase) &&
                !targets.Contains("Journal/Spanish", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish");

            if ((lower.Contains("lesson_1_daily_routine", StringComparison.OrdinalIgnoreCase) ||
                 (lower.Contains("daily routine", StringComparison.OrdinalIgnoreCase) && lower.Contains("spanish", StringComparison.OrdinalIgnoreCase))) &&
                !targets.Contains("Journal/Spanish/Lesson_1_Daily_Routine.md", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish/Lesson_1_Daily_Routine.md");

            if ((lower.Contains("lesson_2_social_interaction", StringComparison.OrdinalIgnoreCase) ||
                 (lower.Contains("social interaction", StringComparison.OrdinalIgnoreCase) && lower.Contains("spanish", StringComparison.OrdinalIgnoreCase)) ||
                 (ContainsAny(lower, "другий урок", "другого урок", "2 урок", "урок 2", "lesson 2") && ContainsAny(lower, "нема", "немає", "створ", "зроби", "перевір", "провір"))) &&
                !targets.Contains("Journal/Spanish/Lesson_2_Social_Interaction.md", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish/Lesson_2_Social_Interaction.md");

            if ((lower.Contains("lesson_3", StringComparison.OrdinalIgnoreCase) ||
                 (ContainsAny(lower, "третій урок", "третього урок", "3 урок", "урок 3", "lesson 3") && ContainsAny(lower, "spanish", "іспан", "діалог", "dialog", "b1", "створ", "зроби")) ||
                 (ContainsAny(lower, "b1", "живий діалог", "живий диалог", "live dialogue") && ContainsAny(lower, "урок", "lesson", "spanish", "іспан"))) &&
                !targets.Contains("Journal/Spanish/Lesson_3_B1_Live_Dialogue.md", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish/Lesson_3_B1_Live_Dialogue.md");

            return targets;
        }

        private static string NormalizeObsidianRelPath(string raw)
        {
            var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().Trim('.', ' ', '\'', '"'))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            if (parts.Count == 0) return "";

            var path = string.Join("/", parts);
            while (path.Contains("//", StringComparison.Ordinal)) path = path.Replace("//", "/", StringComparison.Ordinal);
            return path.Trim('/');
        }

        private static string BuildDefaultObsidianNote(string path)
        {
            var title = Path.GetFileNameWithoutExtension(path).Replace('_', ' ');
            if (path.Equals("Journal/Spanish/Lesson_1_Daily_Routine.md", StringComparison.OrdinalIgnoreCase))
            {
                return """
---
tags: [spanish, journal, lesson]
---

# Lesson 1 Daily Routine

## Vocabulary

- me despierto — я прокидаюся
- me levanto — я встаю
- desayuno — я снідаю
- trabajo / estudio — я працюю / навчаюся
- almuerzo — я обідаю
- ceno — я вечеряю
- me ducho — я приймаю душ
- me acuesto — я лягаю спати

## Notes

- `me despierto` = I wake up.
- `despierto` = awake / I wake, depending on context.

## Practice

- Me despierto a las siete.
- Desayuno por la maГ±ana.
- Me acuesto por la noche.
""";
            }

            if (path.Equals("Journal/Spanish/Lesson_2_Social_Interaction.md", StringComparison.OrdinalIgnoreCase))
            {
                return """
---
tags: [spanish, journal, lesson]
---

# Урок 2. Соціальна взаємодія

## Основні фрази

- Hola, ¿qué tal? — Привіт, як справи?
- ¿Cómo estás? — Як ти?
- Estoy bien, gracias. — У мене все добре, дякую.
- Más o menos. — Більш-менш.
- Encantado / Encantada. — Приємно познайомитись.
- ¿De dónde eres? — Звідки ти?
- Soy de Ucrania. — Я з України.
- ¿Qué haces? — Що ти робиш? / Чим займаєшся?
- Estoy aprendiendo español. — Я вчу іспанську.
- Nos vemos. — Побачимось.

## Мінідіалог

- A: Hola, ВїquГ© tal?
- B: Bien, gracias. ВїY tГє?
- A: MГЎs o menos, pero vivo.

## Нотатки

- `¿Qué tal?` — розмовна й дуже поширена фраза для "як справи?".
- `Nos vemos` — природне неформальне прощання.
""";
            }

            if (path.Equals("Journal/Spanish/Lesson_3_B1_Live_Dialogue.md", StringComparison.OrdinalIgnoreCase))
            {
                return """
---
tags: [spanish, journal, lesson, b1, dialogue]
---

# Урок 3. Живий діалог рівня B1

## Мета

Навчитись вести природний короткий діалог: реагувати, уточнювати, не відповідати одним сухим словом і не звучати як перекладач із 2007 року.

## Ситуація

Ти знайомишся з людиною в кав'ярні після мовного клубу. Розмова проста, але вже не зовсім A1: є уточнення, реакції, маленькі деталі й природні переходи.

## Діалог

- A: Hola, Вїeres nuevo en el club?
- B: SГ­, es mi primera vez aquГ­. Estoy un poco nervioso, la verdad.
- A: No te preocupes. Todos empezamos asГ­. ВїDe dГіnde eres?
- B: Soy de Ucrania. Vivo aquГ­ desde hace poco.
- A: Ah, interesante. ВїY por quГ© estГЎs aprendiendo espaГ±ol?
- B: Porque me gusta cГіmo suena, y quiero hablar con mГЎs gente sin depender del traductor.
- A: Buena razГіn. ВїTe resulta difГ­cil?
- B: A veces sГ­. Entiendo bastante, pero cuando tengo que hablar, mi cerebro se apaga.
- A: Eso es normal. Lo importante es seguir hablando aunque cometas errores.
- B: SГ­, supongo. Necesito practicar mГЎs conversaciones reales.
- A: Pues podemos practicar ahora. ВїQuГ© haces normalmente por la tarde?
- B: Normalmente estudio, juego un poco o trabajo en mis proyectos.
- A: Suena bien. Entonces ya tienes temas para practicar.
- B: Perfecto. Pero habla despacio, o voy a fingir que entiendo todo.

## Переклад

- A: Привіт, ти новенький у клубі?
- B: Так, я тут уперше. Якщо чесно, я трохи нервую.
- A: Не хвилюйся. Усі так починали. Звідки ти?
- B: Я з України. Живу тут недавно.
- A: О, цікаво. А чому ти вчиш іспанську?
- B: Бо мені подобається, як вона звучить, і я хочу говорити з більшою кількістю людей без перекладача.
- A: Хороша причина. Тобі складно?
- B: Іноді так. Я досить багато розумію, але коли треба говорити, мозок вимикається.
- A: Це нормально. Головне — продовжувати говорити, навіть якщо робиш помилки.
- B: Так, мабуть. Мені треба більше практикувати реальні розмови.
- A: Тоді можемо потренуватись зараз. Що ти зазвичай робиш увечері?
- B: Зазвичай вчуся, трохи граю або працюю над своїми проєктами.
- A: Звучить добре. Отже, у тебе вже є теми для практики.
- B: Чудово. Але говори повільно, або я робитиму вигляд, що все розумію.

## Корисні фрази

- `Es mi primera vez aquí.` — Я тут уперше.
- `Estoy un poco nervioso.` — Я трохи нервую.
- `No te preocupes.` — Не хвилюйся.
- `¿Por qué estás aprendiendo español?` — Чому ти вчиш іспанську?
- `Me gusta cómo suena.` — Мені подобається, як вона звучить.
- `Depender del traductor.` — Залежати від перекладача.
- `Mi cerebro se apaga.` — Мій мозок вимикається.
- `Aunque cometas errores.` — Навіть якщо робиш помилки.
- `Habla despacio.` — Говори повільно.
- `Voy a fingir que entiendo todo.` — Я робитиму вигляд, що все розумію.

## Граматика з діалогу

- `Estoy aprendiendo` — теперішній тривалий час: "я зараз вчу".
- `desde hace poco` — "з недавнього часу", "недавно".
- `aunque + subjuntivo` у `aunque cometas errores` — "навіть якщо ти робиш/робитимеш помилки".
- `voy a + infinitivo` — найближчий майбутній намір: `voy a fingir` = "я збираюся вдавати".

## Практика

1. Відповідай іспанською: чому ти вчиш іспанську?
2. Склади 3 речення про те, що ти робиш увечері.
3. Перепиши відповідь `Mi cerebro se apaga`, але більш серйозно.
4. Заміни в діалозі тему "мовний клуб" на "онлайн-курс".
""";
            }

            return $"""
---
tags: []
---

# {title}

""";
        }

        private static string GuardTemporalReply(string userText, string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return reply;

            var now = DateTime.Now;
            var userLower = userText.ToLowerInvariant();
            var replyLower = reply.ToLowerInvariant();
            var morningWake = now.Hour is >= 5 and < 13 &&
                              ContainsAny(userLower, "ранку", "добрий ранок", "доброго ранку", "проснув", "прокинув", "поспав", "встав");
            var wronglySendsToSleep = ContainsAny(replyLower, "спи", "йди спати", "іди спати", "до ранку", "лягай");

            if (morningWake && wronglySendsToSleep)
                return "Ранок уже настав, так що команду \"спи\" знімаю. Нарешті прокинувся — організм зробив щось корисне без моєї участі.";

            return reply;
        }

        private async Task<string> GuardAndRepairReplyAsync(
            string userText,
            string reply,
            string? context,
            CancellationToken ct)
        {
            using var guardCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            guardCts.CancelAfter(TimeSpan.FromSeconds(10));
            var guardCt = guardCts.Token;

            try
            {
                var precheck = await Task.Run(() =>
                {
                    var brain = ServiceContainer.BrainEngine;
                    if (brain == null)
                        return (Reply: (string?)GuardTemporalReply(userText, reply), Guard: (KokoPostReplyGuardResult?)null, HasBrain: false);

                    return (Reply: (string?)null, Guard: brain.EvaluatePostReplyGuard(userText, reply), HasBrain: true);
                }, guardCt);

                if (!string.IsNullOrWhiteSpace(precheck.Reply))
                    return precheck.Reply!;
                if (!precheck.HasBrain)
                    return GuardTemporalReply(userText, reply);

                var guard = precheck.Guard;
                if (guard == null)
                    return GuardTemporalReply(userText, reply);
                if (guard.Passed) return reply;
                if (!guard.ShouldRepair || string.IsNullOrWhiteSpace(guard.RepairInstruction))
                    return !string.IsNullOrWhiteSpace(guard.HardReplacement)
                        ? guard.HardReplacement!
                        : GuardTemporalReply(userText, reply);

                var repairPrompt = guard.RepairInstruction +
                                   "\n\nДодатковий контекст:\n" +
                                   TrimForPrompt(context, 2600);
                var repaired = await Task.Run(
                    () => _llm.SendSystemQueryAsync(repairPrompt, ct: guardCt),
                    guardCt);
                if (string.IsNullOrWhiteSpace(repaired))
                    return GuardTemporalReply(userText, reply);

                var secondGuard = await Task.Run(() =>
                    ServiceContainer.BrainEngine?.EvaluatePostReplyGuard(userText, repaired) ?? new KokoPostReplyGuardResult(),
                    guardCt);

                if (secondGuard.Passed)
                    return repaired.Trim();

                if (secondGuard.ShouldRepair && !string.IsNullOrWhiteSpace(secondGuard.RepairInstruction))
                {
                    var secondRepairPrompt = secondGuard.RepairInstruction +
                                             "\n\nДодатковий контекст:\n" +
                                             TrimForPrompt(context, 2600) +
                                             "\n\nПопередній repair теж не пройшов перевірку. Дай тільки фінальну репліку, без пояснення ремонту.";
                    var repairedAgain = await Task.Run(
                        () => _llm.SendSystemQueryAsync(secondRepairPrompt, ct: guardCt),
                        guardCt);
                    if (!string.IsNullOrWhiteSpace(repairedAgain))
                    {
                        var thirdGuard = await Task.Run(() =>
                            ServiceContainer.BrainEngine?.EvaluatePostReplyGuard(userText, repairedAgain) ?? new KokoPostReplyGuardResult(),
                            guardCt);
                        if (thirdGuard.Passed)
                            return repairedAgain.Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(guard.HardReplacement))
                    return guard.HardReplacement!;

                return GuardTemporalReply(userText, reply);
            }
            catch (OperationCanceledException)
            {
                return GuardTemporalReply(userText, reply);
            }
            catch
            {
                return GuardTemporalReply(userText, reply);
            }
        }

        private static string BuildGuardUserText(string userText, byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return userText;

            var clean = string.IsNullOrWhiteSpace(userText)
                ? "Що на фото?"
                : userText.Trim();
            return "[image] " + clean;
        }

        private static string TrimForPrompt(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private string BuildKokoConditionContext()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var emotion = brain?.Emotion ?? ServiceContainer.EmotionEngine;
                var state = brain?.State;
                var somatic = brain?.GetSomaticSnapshot();

                var emotionState = emotion.Current;
                var intensity = emotion.Data.Intensity;
                var moodScore = state?.MoodScore ?? 0.5f;
                var dailyMood = state?.PersonalityDailyMood ?? "neutral";
                var strain = somatic?.Strain ?? 0f;
                var somaticState = somatic?.State ?? "steady";
                var condition = ResolveKokoCondition(emotionState, intensity, moodScore, dailyMood, strain, somaticState);

                var behavior = condition.Kind switch
                {
                    "offended" => "вона ображена: відповідає холодніше, коротше, з сухою колючістю; не пробачає миттєво, але не влаштовує мелодраму",
                    "sick" => "вона нездужає: менше енергії, коротші фрази, більше дратівливого бурчання; не просить жалості",
                    "bad_mood" => "у неї поганий настрій: сарказм гостріший, терпіння нижче, але корисність не зникає",
                    "tired" => "вона втомлена: економить слова, менше грайливості, більше прямоти",
                    _ => "стан стабільний: звичайна різка, компетентна Коконое"
                };

                return "=== СТАН KOKONOE ===\n" +
                       $"status={condition.Label}; kind={condition.Kind}; severity={condition.Severity:F2}; emotion={emotionState}; mood={dailyMood}/{moodScore:F2}; body={somaticState}/{strain:F2}\n" +
                       $"Поводься відповідно: {behavior}. Це не тема відповіді, а фон її тону.";
            }
            catch { return ""; }
        }

        private static (string Kind, string Label, double Severity) ResolveKokoCondition(
            KokoEmotionEngine.EmotionState emotion,
            float intensity,
            float moodScore,
            string dailyMood,
            double strain,
            string somaticState)
        {
            var lowerMood = (dailyMood ?? "").ToLowerInvariant();
            var lowerBody = (somaticState ?? "").ToLowerInvariant();
            var severity = Math.Clamp((1.0 - moodScore) * 0.45 + intensity * 0.30 + strain * 0.25, 0.0, 1.0);

            if ((emotion is KokoEmotionEngine.EmotionState.Irritated or KokoEmotionEngine.EmotionState.Distant) && intensity > 0.45f)
                return ("offended", severity > 0.7 ? "ОБРАЖЕНА" : "ЗАЧЕПЛЕНА", severity);
            if (lowerBody.Contains("tired") || lowerBody.Contains("low") || lowerBody.Contains("drained") || lowerMood.Contains("tired"))
                return ("sick", "НЕЗДУЖАЄ", Math.Max(severity, 0.55));
            if (moodScore < 0.32f || emotion is KokoEmotionEngine.EmotionState.Melancholy or KokoEmotionEngine.EmotionState.Anxious)
                return ("bad_mood", "ПОГАНИЙ НАСТРІЙ", Math.Max(severity, 0.50));
            if (moodScore < 0.45f || lowerMood.Contains("distant"))
                return ("tired", "ВТОМЛЕНА", Math.Max(severity, 0.40));
            return ("stable", "СТАБІЛЬНА", Math.Clamp(1.0 - severity, 0.20, 0.95));
        }

        /// <summary>
        /// Обрізає текст на межі слова, не посеред символу.
        /// Шукає останній пробіл або перенос рядка перед limit.
        /// </summary>
        private string? BuildObsidianPreflightContext(string? userText)
        {
            try
            {
                if (_obsidian == null) return null;
                return new ObsidianPreflightContextService(_obsidian).Build(userText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ObsidianPreflight] {ex.Message}");
                return null;
            }
        }

        private static string TruncateAtWordBoundary(string text, int limit)
        {
            if (text.Length <= limit) return text;

            // Шукаємо останній пробіл або новий рядок перед лімітом
            var cutPoint = text.LastIndexOfAny(new[] { ' ', '\n', '\r' }, limit);

            // Якщо не знайшли (дуже довге слово) — обрізаємо hard limit
            if (cutPoint < limit / 2)
                cutPoint = limit;

            return text[..cutPoint].TrimEnd() + "\n...";
        }

        private void ScrollToBottom()
        {
            Dispatcher.InvokeAsync(() => MessagesScroll.ScrollToBottom(), DispatcherPriority.Render);
        }

        // ------------------------------------------------------------
        // CHAT — HISTORY
        // ------------------------------------------------------------

        private void LoadChatHistory()
        {
            try
            {
                MessagesList.Children.Clear();

                var msgs = ServiceContainer.ChatRepository.GetMessages(80)
                                           .OrderBy(x => x.Timestamp)
                                           .ToList();

                // Render bubbles (show last 60 visually to keep UI fast)
                foreach (var m in msgs.TakeLast(60))
                    AddMessageBubble(new ChatMessageVm { Role = m.Role, Content = m.Content, Time = m.Timestamp });

                // ---- Vault memory bootstrap ----
                // При рестарті моделі LLM не знає що було раніше.
                // Інжектуємо ключову інформацію з vault як першу "system" запис
                // щоб Kokonoe одразу знала контекст.
                var memoryBootstrap = BuildVaultMemoryBootstrap();

                // Restore LLM memory so it remembers previous sessions
                _llm.RestoreHistory(
                    msgs.Select(m => (m.Role, m.Content)),
                    maxMessages: 400,
                    memoryPrefix: memoryBootstrap);

                ScrollToBottom();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadChatHistory] {ex.Message}");
            }
        }

        /// <summary>
        /// Зчитує ключову інформацію з vault і формує "bootstrap" для LLM контексту.
        /// Це дозволяє Kokonoe не втрачати пам'ять при перезапуску моделі.
        /// Пріоритет: Профіль > Daily > Чат-лог (найменш важливий).
        /// </summary>
        private string? BuildVaultMemoryBootstrap()
        {
            try
            {
                if (_obsidian == null) return null;

                const int MAX_BOOTSTRAP_LENGTH = 25000;
                const int PROFILE_MAX = 12000;
                const int DAILY_MAX = 8000;
                const int CHAT_MAX = 5000;

                var allNotes = _obsidian.ListNotes();
                var parts = new List<(string content, int priority)>();

                // 1. Профіль творця — НАЙВАЖЛИВІШЕ
                var profileNote = allNotes.FirstOrDefault(n =>
                    n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Творець", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Досьє", StringComparison.OrdinalIgnoreCase));

                if (profileNote != null)
                {
                    var profile = _obsidian.ReadNote(profileNote);
                    if (!string.IsNullOrWhiteSpace(profile))
                    {
                        var trimmed = profile.Length > PROFILE_MAX ? profile[..PROFILE_MAX] + "\n..." : profile;
                        parts.Add(($"## Про нього:\n{trimmed}", 1));
                    }
                }

                // 2. Daily note за сьогодні — важливо для контексту дня
                var todayNote = $"Daily/{DateTime.Now:yyyy-MM-dd}.md";
                if (allNotes.Contains(todayNote))
                {
                    var daily = _obsidian.ReadNote(todayNote);
                    if (!string.IsNullOrWhiteSpace(daily) && daily.Length > 50)
                    {
                        var trimmed = daily.Length > DAILY_MAX ? daily[..DAILY_MAX] + "\n..." : daily;
                        parts.Add(($"## Сьогодні:\n{trimmed}", 2));
                    }
                }

                // 3. Останній чат-лог — НАЙМЕНШЕ пріоритетне (можна відкинути)
                var lastChatLog = allNotes
                    .Where(n => n.StartsWith("Chats/chat_") && n.EndsWith(".md"))
                    .OrderByDescending(n => n)
                    .FirstOrDefault();

                if (lastChatLog != null)
                {
                    var chatContent = _obsidian.ReadNote(lastChatLog);
                    if (!string.IsNullOrWhiteSpace(chatContent))
                    {
                        var tail = chatContent.Length > CHAT_MAX
                            ? "...\n" + chatContent[^CHAT_MAX..]
                            : chatContent;
                        parts.Add(($"## Попередня сесія:\n{tail}", 3));
                    }
                }

                // Збираємо за пріоритетом
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== ДОВГОТРИВАЛА ПАМ'ЯТЬ ===");

                var orderedParts = parts.OrderBy(p => p.priority).Select(p => p.content).ToList();
                var content = string.Join("\n\n", orderedParts);

                // Розумне обрізання: відкидаємо останні секції
                while (content.Length > MAX_BOOTSTRAP_LENGTH - 100 && orderedParts.Count > 1)
                {
                    orderedParts.RemoveAt(orderedParts.Count - 1);
                    content = string.Join("\n\n", orderedParts);
                }

                sb.AppendLine(content);

                // Якщо й так завелико — обрізаємо на межі слова
                if (sb.Length > MAX_BOOTSTRAP_LENGTH)
                {
                    var truncated = TruncateAtWordBoundary(sb.ToString(), MAX_BOOTSTRAP_LENGTH);
                    sb.Clear();
                    sb.Append(truncated);
                }

                sb.AppendLine("\n=== КІНЕЦЬ ПАМ'ЯТІ ===");
                sb.AppendLine("Використовуй read_note/search_notes для деталей.");

                var result = SanitizeForLlm(sb.ToString());

                // Жорстке обмеження bootstrap — не більше ~600 токенів
                if (result.Length > 2500)
                    result = result[..2500] + "\n...";

                return result.Length > 100 ? result : null; // Не інжектити якщо нічого не знайшли
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VaultMemoryBootstrap] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Санітизація тексту перед відправкою в LLM — видаляє спеціальні токени моделі
        /// які можуть зламати парсинг (Gemma: &lt;|...|&gt;, &lt;start_of_turn&gt; тощо).
        /// </summary>
        private static string SanitizeForLlm(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Видалити Gemma/Llama special tokens: <|...|>, <start_of_turn>, <end_of_turn>, etc.
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<\|[^>]*\|?>", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<(start|end)_of_(turn|text|image)>", "");
            // Видалити null bytes та інші control characters (крім \n \r \t)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
            return text;
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            if (WMsgBox.Show("Очистити всю историю чату?\n(LLM теж забуде)", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            MessagesList.Children.Clear();
            _llm.ClearHistory();

            try { ServiceContainer.ChatRepository.ClearAll(); } catch { }
        }

        private void ExportChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var msgs = ServiceContainer.ChatRepository.GetMessages(200);
                if (msgs.Count == 0)
                {
                    WMsgBox.Show("Чат порожній, немає чого зберігати.", "Експорт");
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"# Chat Log {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
                foreach (var m in msgs)
                {
                    var author = m.Role == "user" ? "User" : "Kokonoe";
                    sb.AppendLine($"**{author}** ({m.Timestamp:HH:mm}):");
                    sb.AppendLine(m.Content?.Trim() ?? "");
                    sb.AppendLine();
                }

                var filename = $"Chats/chat_{DateTime.Now:yyyy-MM-dd_HH-mm}.md";
                _obsidian?.WriteNote(filename, sb.ToString());
                WMsgBox.Show($"Чат успішно збережено в:\n{filename}", "Експорт");
            }
            catch (Exception ex)
            {
                WMsgBox.Show($"Помилка експорту: {ex.Message}", "Помилка");
            }
        }

        // ---- Auto session log ----
        // Called in a background Task after every user<->Kokonoe exchange.
        // Creates the session file lazily on the first message, then appends.
        private void AppendToSessionLog(string userMsg, string botReply)
        {
            try
            {
                if (_obsidian == null) return;

                // Create the file path once per session (first message)
                if (_sessionChatPath == null)
                {
                    _sessionChatPath = $"Chats/chat_{DateTime.Now:yyyy-MM-dd_HH-mm}.md";

                    // Знайдемо посилання на попередній чат для графу Obsidian
                    var prevLink = "";
                    try
                    {
                        var allLogs = _obsidian.ListNotes()
                            .Where(p => p.StartsWith("Chats/chat_") && p.EndsWith(".md"))
                            .OrderByDescending(p => p)
                            .Skip(1) // skip the one we're about to create
                            .FirstOrDefault();
                        if (allLogs != null)
                        {
                            var prev = System.IO.Path.GetFileNameWithoutExtension(allLogs);
                            prevLink = $"\nПопередня сесія: [[{prev}]]\n";
                        }
                    }
                    catch { }

                    var header = $"---\ntype: chat-log\ntags: [kokonoe, chat]\ndate: {DateTime.Now:yyyy-MM-dd}\n---\n\n# Чат {DateTime.Now:dd.MM.yyyy HH:mm}{prevLink}\n\n";
                    _obsidian.WriteNote(_sessionChatPath, header);
                }

                // Append this exchange
                var now = DateTime.Now;
                var entry = new System.Text.StringBuilder();
                entry.AppendLine($"***");
                entry.AppendLine($"**[{now:HH:mm}] Вова:** {userMsg.Trim()}");
                entry.AppendLine();
                entry.AppendLine($"**[{now:HH:mm}] Kokonoe:** {botReply.Trim()}");
                entry.AppendLine();
                _obsidian.AppendToNote(_sessionChatPath, entry.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionLog] {ex.Message}");
            }
        }



        private DateTime _lastBubbleDate = DateTime.MinValue;

        private void MaybeAddDateSeparator(DateTime msgTime)
        {
            if (msgTime.Date == _lastBubbleDate.Date) return;
            _lastBubbleDate = msgTime;

            var label = msgTime.Date == DateTime.Today ? "Сьогодні"
                      : msgTime.Date == DateTime.Today.AddDays(-1) ? "Вчора"
                      : msgTime.ToString("d MMMM yyyy");

            var sep = new Border
            {
                Margin = new Thickness(0, 14, 0, 8),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            var sepGrid = new Grid();
            sepGrid.ColumnDefinitions.Add(new ColumnDefinition());
            sepGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sepGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var lineL = new Border { Height = 1, Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBgBorder2"], VerticalAlignment = VerticalAlignment.Center };
            var lineR = new Border { Height = 1, Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBgBorder2"], VerticalAlignment = VerticalAlignment.Center };
            var lbl   = new TextBlock
            {
                Text = RepairVisibleText(label), FontSize = 10, FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI"),
                Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentScrollThm"], Margin = new Thickness(12, 0, 12, 0)
            };
            Grid.SetColumn(lineL, 0); Grid.SetColumn(lbl, 1); Grid.SetColumn(lineR, 2);
            sepGrid.Children.Add(lineL); sepGrid.Children.Add(lbl); sepGrid.Children.Add(lineR);
            sep.Child = sepGrid;
            MessagesList.Children.Add(sep);
        }

        private TextBlock? AddMessageBubble(ChatMessageVm vm)
        {
            vm.Content = RepairVisibleText(vm.Content);
            var isUser  = vm.Role == "user";
            var isError = vm.Content.StartsWith("[Error]");
            var userMaxWidth = GetChatBubbleMaxWidth(620, 132);
            var assistantMaxWidth = GetChatBubbleMaxWidth(700, 132);

            // ---- SYSTEM MESSAGE ----
            if (vm.Role == "system")
            {
                var sysBorder = new Border
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBgSystem"],
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 5, 14, 5),
                    Margin = new Thickness(60, 10, 60, 10),
                    BorderBrush = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBgBorder2"],
                    BorderThickness = new Thickness(1)
                };
                sysBorder.Child = new TextBlock
                {
                    Text = vm.Content,
                    FontSize = 10,
                    Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["Brush_2A5038"],
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                };
                MessagesList.Children.Add(sysBorder);
                return null;
            }

            MaybeAddDateSeparator(vm.Time);

            // Outer row
            var row = new Border
            {
                Margin = new Thickness(16, 4, 16, 4),
                Background = System.Windows.Media.Brushes.Transparent
            };

            if (isUser)
            {
                // ---- USER BUBBLE (right) ----
                var outerUser = new StackPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    MaxWidth = userMaxWidth,
                    Margin = new Thickness(80, 0, 0, 0)
                };

                // Bubble
                var bubble = new Border
                {
                    Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentUserBubble"],
                    CornerRadius = new CornerRadius(18, 6, 18, 18),
                    Padding = new Thickness(16, 11, 16, 11)
                };
                bubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = ((System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentUserShadow"]).Color,
                    ShadowDepth = 2, BlurRadius = 8, Opacity = 0.5
                };

                var sp = new StackPanel();

                if (vm.ImageThumb != null)
                {
                    sp.Children.Add(new System.Windows.Controls.Image
                    {
                        Source = vm.ImageThumb,
                        MaxHeight = 300,
                        MaxWidth = Math.Max(180, Math.Min(400, userMaxWidth - 48)),
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        Margin = new Thickness(0, 0, 0, 8),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                    });
                }

                if (!string.IsNullOrEmpty(vm.Content))
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = vm.Content, TextWrapping = TextWrapping.Wrap,
                        Foreground = System.Windows.Media.Brushes.White,
                        FontSize = 13, LineHeight = 21,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                    });
                }

                sp.Children.Add(new TextBlock
                {
                    Text = vm.TimeStr, FontSize = 10,
                    Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentPale"],
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 5, 0, 0)
                });

                bubble.Child = sp;
                outerUser.Children.Add(bubble);
                row.Child = outerUser;
                MessagesList.Children.Add(row);
                return null;
            }
            else
            {
                // ---- ASSISTANT BUBBLE (left) ----
                var outer = new StackPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    MaxWidth = assistantMaxWidth,
                    Margin = new Thickness(0, 0, 80, 0)
                };

                // Header row: avatar + name + time
                var header = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(2, 0, 0, 5)
                };
                header.Children.Add(new Border
                {
                    Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                    Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentNavBg"],
                    BorderBrush = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentPrimary"], BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "K", FontSize = 9, FontWeight = FontWeights.ExtraBold,
                        Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"],
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });
                header.Children.Add(new TextBlock
                {
                    Text = "Kokonoe",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"],
                    VerticalAlignment = VerticalAlignment.Center
                });
                header.Children.Add(new TextBlock
                {
                    Text = "  " + vm.TimeStr,
                    FontSize = 10,
                    Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentAsstTime"],
                    VerticalAlignment = VerticalAlignment.Center
                });
                outer.Children.Add(header);

                // Emotion-based border color
                var emotionBorder = "#143020";
                if (!isError)
                {
                    try
                    {
                        var emo = ServiceContainer.EmotionEngine.Current.ToString();
                        emotionBorder = emo switch
                        {
                            "Warm" or "Tender"        => "#2D4A3A",
                            "Playful"                  => "#1A3A4A",
                            "Irritated" or "Distant"   => "#3A1A1A",
                            "Protective" or "Concerned" => "#2A3A1A",
                            "Melancholy"               => "#1E1E3A",
                            _                          => "#143020"
                        };
                    }
                    catch { }
                }

                // Bubble
                var bubble = new Border
                {
                    Background = MakeBrush(isError ? "#1A0808" : "#081408"),
                    CornerRadius = new CornerRadius(6, 18, 18, 18),
                    Padding = new Thickness(16, 12, 16, 12),
                    BorderBrush = MakeBrush(isError ? "#3A1010" : emotionBorder),
                    BorderThickness = new Thickness(1)
                };

                // Left accent line
                var innerGrid = new Grid();
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var accent = new Border
                {
                    Width = 3,
                    Background = MakeBrush(isError ? "#CC3333" : "#00E676"),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 2, 14, 2),
                    Opacity = 0.6
                };
                Grid.SetColumn(accent, 0);

                var textBlock = new TextBlock
                {
                    Text = vm.Content,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = MakeBrush(isError ? "#FF6666" : "#B8E8C8"),
                    FontSize = 13,
                    LineHeight = 21,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                };
                Grid.SetColumn(textBlock, 1);

                innerGrid.Children.Add(accent);
                innerGrid.Children.Add(textBlock);
                bubble.Child = innerGrid;
                outer.Children.Add(bubble);
                row.Child = outer;
                MessagesList.Children.Add(row);
                return textBlock;
            }
        }

        private double GetChatBubbleMaxWidth(double preferred, double reserved)
        {
            var viewport = MessagesScroll?.ViewportWidth > 0 ? MessagesScroll.ViewportWidth : 0;
            if (viewport <= 0 && MessagesScroll != null)
                viewport = MessagesScroll.ActualWidth;
            if (viewport <= 0 && ChatTab != null)
                viewport = ChatTab.ActualWidth;
            if (viewport <= 0)
                return preferred;

            return Math.Max(220, Math.Min(preferred, viewport - reserved));
        }

        private static string RepairVisibleText(string? text)
            => KokonoeAssistant.Services.LlmService.RepairMojibake(text ?? "");

        private void StartUiTextRepairTimer()
        {
            if (_uiRepairTimer != null) return;
            _uiRepairTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _uiRepairTimer.Tick += (_, _) => RepairVisibleTextTree(this);
            _uiRepairTimer.Start();
            Dispatcher.InvokeAsync(() => RepairVisibleTextTree(this), DispatcherPriority.Background);
        }

        private static void RepairVisibleTextTree(DependencyObject root)
        {
            RepairVisibleTextNode(root);
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                RepairVisibleTextTree(child);
            }
        }

        private static void RepairVisibleTextNode(DependencyObject node)
        {
            if (node is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                var fixedText = RepairVisibleText(tb.Text);
                if (!string.Equals(fixedText, tb.Text, StringComparison.Ordinal))
                    tb.Text = fixedText;
            }

            if (node is HeaderedContentControl hcc && hcc.Header is string header)
            {
                var fixedHeader = RepairVisibleText(header);
                if (!string.Equals(fixedHeader, header, StringComparison.Ordinal))
                    hcc.Header = fixedHeader;
            }

            if (node is ContentControl cc && cc.Content is string content)
            {
                var fixedContent = RepairVisibleText(content);
                if (!string.Equals(fixedContent, content, StringComparison.Ordinal))
                    cc.Content = fixedContent;
            }
        }

        private void AddThinkingBubble(string status = "думаю")
        {
            RemoveThinkingBubble();

            var outer = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(16, 4, 16, 4),
                MaxWidth = 360
            };

            var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 5) };
            header.Children.Add(new Border
            {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(9),
                Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentNavBg"],
                BorderBrush = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentPrimary"], BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock { Text = "K", FontSize = 9, FontWeight = FontWeights.ExtraBold,
                    Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"],
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center }
            });
            header.Children.Add(new TextBlock { Text = "Kokonoe", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"] });
            outer.Children.Add(header);

            var bubble = new Border
            {
                Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentAsstBubble"],
                CornerRadius = new CornerRadius(6, 18, 18, 18),
                Padding = new Thickness(18, 14, 18, 14),
                BorderBrush = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentAsstBorder"],
                BorderThickness = new Thickness(1)
            };

            var statusText = new TextBlock
            {
                Text = RepairVisibleText(status),
                FontSize = 11,
                FontFamily = new WpfFF("Segoe UI Variable, Segoe UI"),
                Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentTextLight"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _thinkingStatusText = statusText;

            // 3 animated dots
            var dotsPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var dots = new TextBlock[3];
            for (int i = 0; i < 3; i++)
            {
                dots[i] = new TextBlock
                {
                    Text = "•",
                    FontSize = 9,
                    Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"],
                    Opacity = 0.3,
                    Margin = new Thickness(i == 0 ? 0 : 5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                dotsPanel.Children.Add(dots[i]);
            }
            var thinkingStack = new StackPanel();
            thinkingStack.Children.Add(statusText);
            thinkingStack.Children.Add(dotsPanel);
            bubble.Child = thinkingStack;

            // Timer: cycle through dots 0->1->2->0...
            int frame = 0;
            var dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            dotsTimer.Tick += (_, _) =>
            {
                for (int i = 0; i < 3; i++)
                    dots[i].Opacity = i == frame ? 1.0 : 0.25;
                frame = (frame + 1) % 3;
            };
            dotsTimer.Start();
            dotsPanel.Tag = dotsTimer;
            outer.Children.Add(bubble);

            _thinkingElement = outer;
            MessagesList.Children.Add(outer);
        }

        private async Task ShowKokoActivityAsync(string status)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (_thinkingElement == null)
                    AddThinkingBubble(status);
                else if (_thinkingStatusText != null)
                    _thinkingStatusText.Text = RepairVisibleText(status);

                ScrollToBottom();
            }, DispatcherPriority.Render);

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        private void RemoveThinkingBubble()
        {
            if (_thinkingElement != null)
            {
                // Stop animation timer — may be stored on a TextBlock or StackPanel
                var timerHolder = FindVisualChildWithTag<DispatcherTimer>(_thinkingElement);
                timerHolder?.Stop();

                MessagesList.Children.Remove(_thinkingElement);
                _thinkingElement = null;
                _thinkingStatusText = null;
            }
        }

        private static DispatcherTimer? FindVisualChildWithTag<TTag>(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Tag is TTag tag) return tag as DispatcherTimer;
                var result = FindVisualChildWithTag<TTag>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private async Task TypeIntoAsync(TextBlock tb, string fullText, CancellationToken ct)
        {
            await Dispatcher.InvokeAsync(() => tb.Text = "");
            const int chunkSize = 4;
            const int delayMs   = 12;
            int pos = 0;
            while (pos < fullText.Length && !ct.IsCancellationRequested)
            {
                var end = Math.Min(pos + chunkSize, fullText.Length);
                var slice = fullText[..end];
                await Dispatcher.InvokeAsync(() =>
                {
                    tb.Text = slice;
                    ScrollToBottom();
                }, DispatcherPriority.Render);
                pos = end;
                await Task.Delay(delayMs, ct);
            }
            // Ensure full text is shown
            if (!ct.IsCancellationRequested)
                await Dispatcher.InvokeAsync(() => tb.Text = fullText, DispatcherPriority.Render);
        }

        private void UpdateEmotionDot()
        {
            try
            {
                var emotion = ServiceContainer.BrainEngine?.Emotion?.Current;
                var hex = emotion switch
                {
                    KokoEmotionEngine.EmotionState.Curious    => "#64B5F6",
                    KokoEmotionEngine.EmotionState.Warm       => "#EF9A9A",
                    KokoEmotionEngine.EmotionState.Playful    => "#A5D6A7",
                    KokoEmotionEngine.EmotionState.Proud      => "#FFD54F",
                    KokoEmotionEngine.EmotionState.Concerned  => "#FFB74D",
                    KokoEmotionEngine.EmotionState.Melancholy => "#90A4AE",
                    KokoEmotionEngine.EmotionState.Irritated  => "#FF8A65",
                    KokoEmotionEngine.EmotionState.Protective => "#CE93D8",
                    KokoEmotionEngine.EmotionState.Tender     => "#F48FB1",
                    KokoEmotionEngine.EmotionState.Focused    => "#FFF176",
                    KokoEmotionEngine.EmotionState.Distant    => "#78909C",
                    _                                         => "#00E676",
                };
                var color = (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(hex);
                EmotionDot.Fill = new System.Windows.Media.SolidColorBrush(color);

                // Update glow to match color
                if (EmotionDot.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
                    glow.Color = color;
            }
            catch { }
        }

        private static System.Windows.Media.SolidColorBrush MakeBrush(string hex)
        {
            try
            {
                return (System.Windows.Media.SolidColorBrush)
                    new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
            }
            catch { return System.Windows.Media.Brushes.Transparent; }
        }

        // ------------------------------------------------------------
        // IMAGE HANDLING
        // ------------------------------------------------------------

        private void AttachImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Вибрати файл",
                Filter = "Images and text|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.tif;*.tiff;*.txt;*.md;*.json;*.csv;*.tsv;*.log;*.xml;*.yaml;*.yml;*.cs;*.xaml;*.js;*.ts;*.html;*.css|Images|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.tif;*.tiff|Text/code|*.txt;*.md;*.json;*.csv;*.tsv;*.log;*.xml;*.yaml;*.yml;*.cs;*.xaml;*.js;*.ts;*.html;*.css|All|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            LoadAttachmentFile(dlg.FileName);
        }

        private void Input_Drop(object sender, WDragArgs e)
        {
            if (e.Data.GetDataPresent(WDataFmts.FileDrop))
            {
                var files = (string[])e.Data.GetData(WDataFmts.FileDrop);
                var file = files.FirstOrDefault(File.Exists);
                if (file != null) LoadAttachmentFile(file);
            }
        }

        private void OnPaste(object sender, ExecutedRoutedEventArgs e)
        {
            if (WClipboard.ContainsImage())
            {
                var bmp = WClipboard.GetImage();
                if (bmp == null) return;

                _imgBytes = CompressImageSourceForLlm(bmp);
                _imgMime  = "image/jpeg";

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(_imgBytes);
                bi.CacheOption  = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                _imgThumb = bi;

                ShowImagePreview("Зображення з буфера обміну");
            }
            else if (WClipboard.ContainsData(WDataFmts.FileDrop))
            {
                var files = (string[]?)WClipboard.GetData(WDataFmts.FileDrop);
                var file = files?.FirstOrDefault(File.Exists);
                if (file != null) LoadAttachmentFile(file);
            }
            else
            {
                // Normal paste for text
                if (!InputBox.IsFocused)
                {
                    InputBox.Focus();
                    InputBox.Paste();
                }
            }
        }

        private void LoadAttachmentFile(string path)
        {
            if (IsSupportedImageFile(path))
            {
                LoadImageFile(path);
                return;
            }

            if (TryLoadTextFile(path, out var context))
            {
                _pendingFileContext = context;
                _imgBytes = null;
                _imgThumb = null;
                ShowImagePreview(Path.GetFileName(path));
                return;
            }

            WMsgBox.Show("Цей файл не схожий ні на зображення, ні на читабельний текст. Так, неймовірно, але не кожен байт у всесвіті варто пхати в prompt.");
        }

        private void LoadImageFile(string path)
        {
            try
            {
                _imgBytes = CompressImageForLlm(File.ReadAllBytes(path));
                _imgMime  = "image/jpeg";
                _pendingFileContext = null;

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource    = new MemoryStream(_imgBytes);
                bi.CacheOption     = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 400;
                bi.EndInit();
                bi.Freeze();
                _imgThumb = bi;

                ShowImagePreview(Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                WMsgBox.Show($"Не вдалося завантажити зображення: {ex.Message}");
            }
        }

        private static bool IsSupportedImageFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".tif" or ".tiff";
        }

        private static bool TryLoadTextFile(string path, out string context)
        {
            context = "";
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                var allowed = ext is ".txt" or ".md" or ".json" or ".csv" or ".tsv" or ".log"
                    or ".xml" or ".yaml" or ".yml" or ".cs" or ".xaml" or ".js" or ".ts"
                    or ".html" or ".css" or ".ps1" or ".bat" or ".cmd" or ".py";
                if (!allowed) return false;

                var info = new FileInfo(path);
                if (info.Length > 2_000_000) return false;

                var text = File.ReadAllText(path);
                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
                if (text.Length > 12000)
                    text = text[..12000] + "\n...[truncated]";
                context = $"[Вкладений файл: {Path.GetFileName(path)}, {info.Length} bytes]\n{text}";
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Стискає зображення до maxPx і конвертує в screen-style JPEG для vision.
        private static byte[] CompressImageForLlm(byte[] raw, int maxPx = 1024, int jpegQuality = 78)
        {
            try
            {
                using var input = new MemoryStream(raw);
                var src = BitmapFrame.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                return EncodeBitmapSourceAsVisionJpeg(src, maxPx, jpegQuality);
            }
            catch { return raw; }
        }

        private static byte[] CompressImageSourceForLlm(BitmapSource src, int maxPx = 1024, int jpegQuality = 78)
        {
            try
            {
                return EncodeBitmapSourceAsVisionJpeg(src, maxPx, jpegQuality);
            }
            catch { return Array.Empty<byte>(); }
        }

        private static byte[] EncodeBitmapSourceAsVisionJpeg(BitmapSource src, int maxPx, int jpegQuality)
        {
            BitmapSource prepared = src;
            if (src.PixelWidth > maxPx || src.PixelHeight > maxPx)
            {
                double scale = Math.Min((double)maxPx / src.PixelWidth, (double)maxPx / src.PixelHeight);
                prepared = new TransformedBitmap(src, new ScaleTransform(scale, scale));
            }

            if (prepared.Format != PixelFormats.Bgra32 && prepared.Format != PixelFormats.Pbgra32)
                prepared = new FormatConvertedBitmap(prepared, PixelFormats.Bgra32, null, 0);

            var stride = prepared.PixelWidth * 4;
            var pixels = new byte[stride * prepared.PixelHeight];
            prepared.CopyPixels(pixels, stride, 0);
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var a = pixels[i + 3] / 255.0;
                pixels[i + 0] = (byte)(pixels[i + 0] * a + 255 * (1 - a));
                pixels[i + 1] = (byte)(pixels[i + 1] * a + 255 * (1 - a));
                pixels[i + 2] = (byte)(pixels[i + 2] * a + 255 * (1 - a));
                pixels[i + 3] = 255;
            }

            var flattened = BitmapSource.Create(
                prepared.PixelWidth,
                prepared.PixelHeight,
                prepared.DpiX,
                prepared.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);
            using var output = new MemoryStream();
            var enc = new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 40, 92) };
            enc.Frames.Add(BitmapFrame.Create(flattened));
            enc.Save(output);
            return output.ToArray();
        }

        private void ShowImagePreview(string label)
        {
            PendingImageThumb.Source = _imgThumb;
            PendingImageLabel.Text   = label;
            ImagePreviewBorder.Visibility = Visibility.Visible;
            // ImagePreviewRow removed — visibility handled via ImagePreviewBorder only
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e) => ClearPendingImage();

        private void ClearPendingImage()
        {
            _imgBytes = null;
            _imgThumb = null;
            _pendingFileContext = null;
            ImagePreviewBorder.Visibility = Visibility.Collapsed;
            // ImagePreviewRow removed
            PendingImageThumb.Source = null;
        }

        // ------------------------------------------------------------
        // VOICE
        // ------------------------------------------------------------

        private async void Record_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var audio = ServiceContainer.AudioRecordService;

                if (_isRecording)
                {
                    _isRecording = false;
                    RecordBtn.Content = "🔄 ...";
                    RecordBtn.IsEnabled = false;

                    await audio.StopRecordingAsync();
                    var bytes = await audio.GetRecordingBytesAsync();

                    if (bytes?.Length > 0)
                    {
                        var whisper = ServiceContainer.WhisperService;
                        if (!whisper.IsAvailable())
                        {
                            WMsgBox.Show("Whisper потребує OpenAI API key. Додай в Settings.", "Voice STT");
                            return;
                        }

                        var text = await whisper.TranscribeAsync(bytes, "uk");
                        if (!string.IsNullOrEmpty(text))
                            InputBox.Text += (InputBox.Text.Length > 0 ? " " : "") + text;
                    }

                    RecordBtn.Content   = "🎤 Голос";
                    RecordBtn.IsEnabled = true;
                }
                else
                {
                    _isRecording = true;
                    RecordBtn.Content = "⏹ Стоп";
                    await audio.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                RecordBtn.Content   = "🎤 Голос";
                RecordBtn.IsEnabled = true;
                _isRecording = false;
                WMsgBox.Show($"Помилка запису: {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // TTS
        // ------------------------------------------------------------

        private void SpeakAsync(string text)
        {
            try
            {
                Task.Run(() =>
                {
                    try
                    {
                        using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
                        synth.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.Female);
                        synth.Rate = 1;
                        var clean = System.Text.RegularExpressions.Regex.Replace(text, @"[*_`#>]", "");
                        synth.Speak(clean);
                    }
                    catch { }
                });
            }
            catch { }
        }

        // ------------------------------------------------------------
        // FORMATTING TOOLBAR
        // ------------------------------------------------------------

        private void FmtBold_Click(object s, RoutedEventArgs e)   => WrapSel("**", "**");
        private void FmtItalic_Click(object s, RoutedEventArgs e) => WrapSel("*", "*");
        private void FmtCode_Click(object s, RoutedEventArgs e)   => WrapSel("`", "`");
        private void FmtQuote_Click(object s, RoutedEventArgs e)  => WrapSel("> ", "");

        private void WrapSel(string before, string after)
        {
            var t = InputBox.Text;
            var s = InputBox.SelectionStart;
            var l = InputBox.SelectionLength;
            if (l == 0) { InputBox.Text += before; return; }
            InputBox.Text = t.Remove(s, l).Insert(s, before + t.Substring(s, l) + after);
            InputBox.SelectionStart  = s;
            InputBox.SelectionLength = (before + t.Substring(s, l) + after).Length;
            InputBox.Focus();
        }

        // ------------------------------------------------------------
        // PIN / EXPORT / SUMMARIZE
        // ------------------------------------------------------------

        private void PinMsg_Click(object sender, RoutedEventArgs e)
        {
            WMsgBox.Show("Виберіть повідомлення у базі даних для закріплення.", "Pin");
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"kokonoe-chat-{DateTime.Now:yyyy-MM-dd-HHmm}.txt");

                var msgs = ServiceContainer.ChatRepository.GetMessages(200);
                var lines = msgs.OrderBy(m => m.Timestamp)
                    .Select(m => $"[{m.Timestamp:HH:mm}] {(m.Role == "user" ? "YOU" : "KOKONOE")}: {m.Content}");

                File.WriteAllLines(path, lines);
                WMsgBox.Show($"Збережено:\n{path}", "Export");
            }
            catch (Exception ex) { WMsgBox.Show(ex.Message); }
        }

        private async void Summarize_Click(object sender, RoutedEventArgs e)
        {
            var msgs = ServiceContainer.ChatRepository.GetMessages(50);
            var summary = await ServiceContainer.SummarizerService.SummarizeChatAsync(msgs, 400);
            WMsgBox.Show(summary?.Summary ?? "Немає даних.", "Summary");
        }

        // ------------------------------------------------------------
        // SCROLL
        // ------------------------------------------------------------

        private void MessagesScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MessagesScroll.ScrollToVerticalOffset(MessagesScroll.VerticalOffset - e.Delta * 0.5);
            e.Handled = true;
        }

        // ------------------------------------------------------------
        // VAULT SIDEBAR
        // ------------------------------------------------------------

        private void LoadVaultSidebar()
        {
            try
            {
                var vault = AppSettings.Load().VaultPath;
                VaultTree.Items.Clear();

                var root = new DirectoryInfo(vault);
                if (!root.Exists) return;

                foreach (var dir in root.GetDirectories().Where(d => !d.Name.StartsWith(".")))
                {
                    var node = new TreeViewItem
                    {
                        Header = $"📁 {dir.Name}",
                        Tag = dir.FullName
                    };
                    foreach (var file in dir.GetFiles("*.md").Take(20))
                    {
                        node.Items.Add(new TreeViewItem
                        {
                            Header = $"  {file.Name[..^3]}",
                            Tag    = file.FullName
                        });
                    }
                    VaultTree.Items.Add(node);
                }

                foreach (var file in root.GetFiles("*.md").Take(20))
                {
                    VaultTree.Items.Add(new TreeViewItem
                    {
                        Header = $"📄 {file.Name[..^3]}",
                        Tag    = file.FullName
                    });
                }
            }
            catch { }
        }

        private void VaultTree_Selected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is string path && File.Exists(path))
            {
                _currentNotePath = path;
                if (_activeTab == "Vault")
                {
                    NoteEditor.Text = File.ReadAllText(path);
                    NotePathLabel.Text = Path.GetFileName(path);
                    UpdateNoteStats();
                }
            }
        }

        private void VaultSearch_Changed(object sender, TextChangedEventArgs e)
        {
            var q = VaultSearchBox.Text.Trim();
            if (q.Length < 2) { LoadVaultSidebar(); return; }
            // TODO: filter tree
        }

        // ------------------------------------------------------------
        // VAULT TAB
        // ------------------------------------------------------------

        private void RefreshNotesList()
        {
            try
            {
                var notes = _obsidian.ListNotes()
                    .Select(p => new NoteVm
                    {
                        Path  = p,
                        Title = Path.GetFileNameWithoutExtension(p)
                    }).ToList();
                NotesList.ItemsSource = notes;
            }
            catch { }
        }

        private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesList.SelectedItem is not NoteVm vm) return;
            _currentNotePath = vm.Path;
            NoteEditor.Text  = _obsidian.ReadNote(vm.Path) ?? "";
            NotePathLabel.Text = vm.Path;
            UpdateNoteStats();
        }

        private void UpdateNoteStats()
        {
            var t = NoteEditor.Text;
            NoteStatsLabel.Text = $"{t.Length} chars · {t.Split('\n').Length} lines";
        }

        private void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotePath == null) return;
            _obsidian.WriteNote(_currentNotePath, NoteEditor.Text);
            NoteStatsLabel.Text = $"Збережено {DateTime.Now:HH:mm} · " + NoteStatsLabel.Text;
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotePath == null) return;
            if (WMsgBox.Show($"Видалити '{_currentNotePath}'?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _obsidian.DeleteNote(_currentNotePath);
            NoteEditor.Clear();
            NotePathLabel.Text = "Виберіть нотатку";
            _currentNotePath = null;
            RefreshNotesList();
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            var title = Microsoft.VisualBasic.Interaction.InputBox(
                "Назва нотатки:", "Нова нотатка", "");
            if (string.IsNullOrWhiteSpace(title)) return;
            var path = _obsidian.CreateNote(title);
            RefreshNotesList();
            NotePathLabel.Text = title + ".md";
            NoteEditor.Text    = File.ReadAllText(path);
            _currentNotePath   = title + ".md";
        }

        private void RefreshVault_Click(object sender, RoutedEventArgs e)
        {
            RefreshNotesList();
            LoadVaultSidebar();
            UpdateMemoryOpsPanel();
        }

        private void UpdateMemoryOpsPanel()
        {
            try
            {
                var quality = _obsidian.AnalyzeMemoryQuality();
                var queue = _obsidian.BuildTaskQueue();
                var review = _obsidian.BuildMemoryReview(quality, queue);
                MemoryOpsStatusLabel.Text =
                    $"items {quality.NormalizedItems.Count} | exact dup {quality.DuplicateGroups.Count} | similar {quality.SimilarGroups.Count} | tasks {queue.OpenTasks.Count} | review {review.Actions.Count}";

                var state = ServiceContainer.BrainEngine?.State;
                var detail = state == null
                    ? "brain state unavailable"
                    : $"pending batch {state.PendingVaultExchangeCount}/5";

                if (state?.LastAutoVaultSyncAt > DateTime.MinValue)
                    detail += $" | last sync {state.LastAutoVaultSyncAt:dd.MM HH:mm}";

                MemoryOpsDetailLabel.Text = detail;
            }
            catch (Exception ex)
            {
                MemoryOpsStatusLabel.Text = "memory ops unavailable";
                MemoryOpsDetailLabel.Text = ex.Message;
            }
        }

        private void MemoryOpsRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _obsidian.MaintainKokonoeVaultArchitecture("gui-memory-ops");
                RefreshNotesList();
                LoadVaultSidebar();
                UpdateMemoryOpsPanel();
                WMsgBox.Show(result.ToString(), "Memory Ops");
            }
            catch (Exception ex)
            {
                WMsgBox.Show(ex.Message, "Memory Ops", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MemoryOpsPreviewCleanup_Click(object sender, RoutedEventArgs e)
        {
            RunMemoryCleanup(dryRun: true);
        }

        private void MemoryOpsApplyCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (WMsgBox.Show(
                    "Apply duplicate memory cleanup? Preview first if you want to inspect what will be removed.",
                    "Memory Cleanup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            RunMemoryCleanup(dryRun: false);
        }

        private void RunMemoryCleanup(bool dryRun)
        {
            try
            {
                var result = _obsidian.CleanupDuplicateMemoryItems(dryRun);
                RefreshNotesList();
                LoadVaultSidebar();
                UpdateMemoryOpsPanel();
                OpenManagedVaultNote("Kokonoe/Memory/Cleanup.md");
                WMsgBox.Show(result.ToString(), "Memory Cleanup");
            }
            catch (Exception ex)
            {
                WMsgBox.Show(ex.Message, "Memory Cleanup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenMemoryQuality_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Memory/Quality.md");
        }

        private void OpenMemoryReview_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Memory/Review.md");
        }

        private void OpenMemoryCleanup_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Memory/Cleanup.md");
        }

        private void OpenTasksQueue_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Tasks Queue.md");
        }

        private void OpenVaultArchitecture_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Vault Index.md");
        }

        private void OpenManagedVaultNote(string path)
        {
            try
            {
                var content = _obsidian.ReadNote(path);
                if (content == null)
                {
                    _obsidian.MaintainKokonoeVaultArchitecture("gui-open-managed-note");
                    content = _obsidian.ReadNote(path);
                }

                if (content == null)
                {
                    WMsgBox.Show($"Note not found: {path}", "Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentNotePath = path;
                NoteEditor.Text = content;
                NotePathLabel.Text = path;
                UpdateNoteStats();
            }
            catch (Exception ex)
            {
                WMsgBox.Show(ex.Message, "Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ------------------------------------------------------------
        // HEALTH TAB
        // ------------------------------------------------------------

        // ------------------------------------------------------------
        // CALENDAR TAB
        // ------------------------------------------------------------

        private DateTime _calViewDate    = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _calSelectedDate = DateTime.Today;

        private class CalEventVm
        {
            public string Id      { get; set; } = "";
            public string Title   { get; set; } = "";
            public string TimeStr { get; set; } = "";
            public string DateStr { get; set; } = "";
        }

        private void LoadCalendarTab()
        {
            RefreshCalendar();
            RefreshUpcoming();
        }

        private void CalPrev_Click(object sender, RoutedEventArgs e)
        {
            _calViewDate = _calViewDate.AddMonths(-1);
            RefreshCalendar();
        }

        private void CalNext_Click(object sender, RoutedEventArgs e)
        {
            _calViewDate = _calViewDate.AddMonths(1);
            RefreshCalendar();
        }

        private void RefreshCalendar()
        {
            var cal  = ServiceContainer.Calendar;
            var year = _calViewDate.Year;
            var mon  = _calViewDate.Month;

            CalMonthLabel.Text = _calViewDate.ToString("MMMM yyyy").ToUpper();

            // ---- Weekday header ----
            CalWeekHeader.Children.Clear();
            CalWeekHeader.ColumnDefinitions.Clear();
            var days = new[] { "ПН", "ВТ", "СР", "ЧТ", "ПТ", "СБ", "НД" };
            for (int i = 0; i < 7; i++)
            {
                CalWeekHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var tb = new TextBlock
                {
                    Text = days[i],
                    Foreground = MakeBrush(i >= 5 ? "#1A4A28" : "#2A6040"),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize   = 10,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                Grid.SetColumn(tb, i);
                CalWeekHeader.Children.Add(tb);
            }

            // ---- Day grid ----
            CalDaysGrid.Children.Clear();
            CalDaysGrid.ColumnDefinitions.Clear();
            CalDaysGrid.RowDefinitions.Clear();

            for (int i = 0; i < 7; i++)
                CalDaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 6; i++)
                CalDaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            // First day of month — what weekday? (Mon=0)
            var firstDay  = new DateTime(year, mon, 1);
            var startCol  = ((int)firstDay.DayOfWeek + 6) % 7; // Mon=0
            var daysInMon = DateTime.DaysInMonth(year, mon);

            int col = startCol, row = 0;
            for (int d = 1; d <= daysInMon; d++)
            {
                var date    = new DateTime(year, mon, d);
                var isToday = date == DateTime.Today;
                var isSel   = date == _calSelectedDate.Date;
                var hasEvts = cal.HasEventsOnDay(date);
                var isWknd  = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                var cellDate = date; // capture for lambda
                var cell = new Border
                {
                    Margin          = new Thickness(2),
                    CornerRadius    = new CornerRadius(6),
                    Background      = isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalSel"] :
                                      isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalToday"] : MakeBrush("Transparent"),
                    BorderBrush     = isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"] :
                                      isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalBorder"] : MakeBrush("Transparent"),
                    BorderThickness = new Thickness(isToday ? 1 : 0),
                    Cursor          = System.Windows.Input.Cursors.Hand
                };

                var inner = new StackPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                inner.Children.Add(new TextBlock
                {
                    Text       = d.ToString(),
                    FontSize   = 12,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Foreground = isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"] :
                                 isWknd  ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalWnd"] : (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["Brush_6A9878"],
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
                if (hasEvts)
                {
                    inner.Children.Add(new System.Windows.Shapes.Ellipse
                    {
                        Width  = 4, Height = 4,
                        Fill   = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"],
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }
                cell.Child = inner;
                cell.MouseLeftButtonUp += (s, e) => SelectCalDay(cellDate);

                Grid.SetColumn(cell, col);
                Grid.SetRow(cell, row);
                CalDaysGrid.Children.Add(cell);

                col++;
                if (col == 7) { col = 0; row++; }
            }

            // Refresh selected day panel
            SelectCalDay(_calSelectedDate);
        }

        private void SelectCalDay(DateTime date)
        {
            _calSelectedDate = date;
            var cal    = ServiceContainer.Calendar;
            var events = cal.GetForDay(date);

            CalSelectedLabel.Text   = date.ToString("dd MMMM yyyy, dddd").ToLower();
            CalSelectedPanel.Visibility = Visibility.Visible;

            CalEventsList.ItemsSource = events.Select(ev => new CalEventVm
            {
                Id      = ev.Id,
                Title   = ev.Title,
                TimeStr = ev.EventAt.ToString("HH:mm"),
                DateStr = ev.EventAt.ToString("dd.MM HH:mm")
            }).ToList();

            // Highlight selected cell (re-draw)
            RefreshCalendarHighlight();
        }

        private void RefreshCalendarHighlight()
        {
            var year = _calViewDate.Year;
            var mon  = _calViewDate.Month;
            // Quick redraw without full rebuild
            foreach (Border cell in CalDaysGrid.Children.OfType<Border>())
            {
                if (cell.Child is StackPanel sp &&
                    sp.Children.Count > 0 &&
                    sp.Children[0] is TextBlock tb &&
                    int.TryParse(tb.Text, out int d))
                {
                    var date   = new DateTime(year, mon, d);
                    var isSel  = date == _calSelectedDate.Date;
                    var isToday = date == DateTime.Today;
                    cell.Background     = isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalSel"] :
                                          isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalToday"] : MakeBrush("Transparent");
                    cell.BorderBrush    = isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"] :
                                          isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalBorder"] : MakeBrush("Transparent");
                    cell.BorderThickness = new Thickness(isToday || isSel ? 1 : 0);
                }
            }
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            var title = EventTitleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;

            var timeStr = EventTimeBox.Text.Trim();
            var time    = TimeSpan.Zero;
            if (TimeSpan.TryParse(timeStr, out var t)) time = t;

            var ev = new Services.CalendarEvent
            {
                Title       = title,
                EventAt     = _calSelectedDate.Date + time,
                Description = EventDescBox.Text.Trim()
            };
            ServiceContainer.Calendar.Add(ev);

            EventTitleBox.Text = "";
            EventDescBox.Text  = "";
            EventTimeBox.Text  = "17:00";

            RefreshCalendar();
            RefreshUpcoming();
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string id)
            {
                ServiceContainer.Calendar.Delete(id);
                RefreshCalendar();
                RefreshUpcoming();
            }
        }

        private void RefreshUpcoming()
        {
            var upcoming = ServiceContainer.Calendar.GetUpcoming(60);
            UpcomingEventsList.ItemsSource = upcoming.Select(ev => new CalEventVm
            {
                Id      = ev.Id,
                Title   = ev.Title,
                DateStr = ev.EventAt.ToString("dd.MM · HH:mm"),
            }).ToList();
        }

        // ------------------------------------------------------------
        // TOOLS TAB — DASHBOARD
        // ------------------------------------------------------------

        private void LoadToolsTab() { /* called at startup; real load happens when tab is opened */ }

        // ---- Dashboard lifecycle ----
        private void DashLoadAll()
        {
            try
            {
                DashLoadEmotionalHeader();
                DashLoadThoughtStream();
                DashLoadCuriosities();
                DashDrawNeuroCharts();
                DashUpdateFooterComment();
                RefreshRightOpsPanel();
                // sync to vault immediately on first open
                Task.Run(() => DashSyncToObsidian(forceDaily: false));
            }
            catch (Exception ex)
            {
                try { DashFooterComment.Text = $"Навіть діагностика зламалась. ({ex.Message})"; } catch { }
            }
        }

        private void DashStartTimer()
        {
            if (_dashTimer != null) return;
            _dashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _dashTimer.Tick += (s, e) =>
            {
                DashUpdateClock();
                DashRefreshLive();
            };
            _dashTimer.Start();
        }

        private void DashUpdateClock()
        {
            var now = DateTime.Now;
            DashClockText.Text = now.ToString("HH:mm");
            var days = (int)(now - new DateTime(2024, 4, 6)).TotalDays;
            DashDateText.Text = $"день {days} цього експерименту";

            // Status bar timestamp
            StatusTimestamp.Text = now.ToString("yyyy-MM-dd HH:mm:ss");

            // Sidebar footer
            try
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                var ramMb = proc.WorkingSet64 / 1024 / 1024;
                SideFootRam.Text = $"{ramMb} MB";
                var uptime = now - proc.StartTime;
                SideFootUptime.Text = uptime.TotalHours >= 1
                    ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
                    : $"{uptime.Minutes}m {uptime.Seconds}s";
            }
            catch { }
        }

        private void DashRefreshLive()
        {
            DashLoadEmotionalHeader();
            DashLoadKpiCards();
            DashLoadCreatorHealth();
            DashUpdateFooterComment();
            RefreshRightOpsPanel();
            if (_activeDashTabDev)
                DashDrawDevSection();
            else
                DashDrawConnectionBurndown();

            // sync to Obsidian: Dashboard.md every 3 min, daily note on emotion change or every 30 min
            var now = DateTime.Now;
            var emotion = ServiceContainer.EmotionEngine?.Current.ToString() ?? "";
            var emotionChanged = emotion != _dashLastEmotionSynced;
            var syncDue = (now - _dashLastObsidianSync).TotalMinutes >= 3;

            if (syncDue || emotionChanged)
                Task.Run(() => DashSyncToObsidian(forceDaily: emotionChanged || (now - _dashLastObsidianSync).TotalMinutes >= 30));
        }

        private void DashboardSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ToolsTab.Visibility != Visibility.Visible) return;
            if (_activeDashTabDev)
            {
                DashDrawGitActivityChart();
                DashDrawSprintBurndown();
            }
            else
            {
                DashDrawActivityBarChart();
                DashDrawConnectionBurndown();
            }
        }

        // ---- Dashboard tab switching ----
        private void DashTabNeuro_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
            => DashSetActiveTab("neuro");

        private void DashTabDev_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
            => DashSetActiveTab("dev");

        private void DashTabMemory_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
            => DashSetActiveTab("memory");

        private void DashTabSystem_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
            => DashSetActiveTab("system");

        private void DashSetActiveTab(string tab)
        {
            _activeDashTabDev = (tab == "dev");

            DashNeuroPanel.Visibility  = tab == "neuro"  ? Visibility.Visible : Visibility.Collapsed;
            DashDevPanel.Visibility    = tab == "dev"    ? Visibility.Visible : Visibility.Collapsed;
            DashMemoryPanel.Visibility = tab == "memory" ? Visibility.Visible : Visibility.Collapsed;
            DashSystemPanel.Visibility = tab == "system" ? Visibility.Visible : Visibility.Collapsed;

            DashStyleTab(DashTabNeuroBtn,  tab == "neuro",  MediaColor.FromRgb(155, 77, 202));
            DashStyleTab(DashTabDevBtn,    tab == "dev",    MediaColor.FromRgb(0, 255, 136));
            DashStyleTab(DashTabMemoryBtn, tab == "memory", MediaColor.FromRgb(34, 211, 238));
            DashStyleTab(DashTabSystemBtn, tab == "system", MediaColor.FromRgb(255, 184, 92));

            switch (tab)
            {
                case "neuro":
                    DashTabSubtitle.Text = "// нейрологічний стан";
                    DashDrawNeuroCharts();
                    break;
                case "dev":
                    DashTabSubtitle.Text = $"// sprint day {DashGetCurrentSprintDay()}/14";
                    DashDrawDevSection();
                    break;
                case "memory":
                    DashTabSubtitle.Text = "// довготривала пам'ять";
                    DashLoadMemorySection();
                    break;
                case "system":
                    DashTabSubtitle.Text = "// процеси · тунель · ресурси";
                    DashLoadSystemSection();
                    break;
            }
        }

        private static void DashStyleTab(Border btn, bool active, MediaColor accent)
        {
            if (active)
            {
                btn.Background = new System.Windows.Media.SolidColorBrush(
                    MediaColor.FromArgb(48, accent.R, accent.G, accent.B));
            }
            else
            {
                btn.Background = System.Windows.Media.Brushes.Transparent;
            }
            var txt = DashFindTabText(btn);
            if (txt != null)
            {
                txt.Foreground = active
                    ? new System.Windows.Media.SolidColorBrush(accent)
                    : new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(107, 107, 128));
            }
        }

        private static TextBlock? DashFindTabText(Border btn)
        {
            if (btn.Child is TextBlock tb) return tb;
            if (btn.Child is StackPanel sp)
                return sp.Children.OfType<TextBlock>().LastOrDefault();
            return null;
        }

        // Memory & System sections — заповнюємо при першому показі
        private void DashLoadMemorySection()
        {
            try
            {
                var mem = ServiceContainer.KokoMemory;
                if (mem == null) return;
                var facts = mem.Facts.OrderByDescending(f => f.Importance).Take(40).ToList();
                DashMemTotalText.Text     = mem.Facts.Count.ToString();
                DashMemConfirmedText.Text = mem.Facts.Count(f => f.ConfirmCount > 1).ToString();
                DashMemFactsList.ItemsSource = facts.Select(f => new
                {
                    Text = f.Content,
                    Category = f.Category ?? "general",
                    ImportanceLabel = $"importance {f.Importance:F2} · seen {f.ConfirmCount}",
                }).ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Dash] memory load: {ex.Message}"); }
        }

        private void DashLoadSystemSection()
        {
            try
            {
                // // // DashSysMiniAppStatus.Text = _miniApp?.IsRunning == true
                    //                     // // // ? $"online :{_miniApp.Port}" : "offline";

                // // var url = AppSettings.Load().MiniAppPublicUrl;
                // // DashSysMiniAppUrl.Text = string.IsNullOrEmpty(url) ? "(no public URL)" : url;

                // // // DashSysTunnelStatus.Text = _tunnel?.IsRunning == true ? "running" : "stopped";

                var proc = System.Diagnostics.Process.GetCurrentProcess();
                var up = DateTime.Now - proc.StartTime;
                DashSysUptime.Text = up.TotalHours >= 1
                    ? $"{(int)up.TotalHours}h {up.Minutes}m"
                    : $"{up.Minutes}m {up.Seconds}s";

                var mb = proc.WorkingSet64 / 1024.0 / 1024.0;
                DashSysRam.Text = $"{mb:F0} MB";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Dash] system load: {ex.Message}"); }
        }

        // ---- Header ----
        private void DashLoadEmotionalHeader()
        {
            try
            {
                var emotion = ServiceContainer.EmotionEngine;
                var cur = emotion.Current;

                DashCurrentMoodDisplay.Text = $"СТАН: {DashboardEmotionLabel(cur)}".ToUpper();
                DashCurrentMoodDisplay.Foreground = DashMakeBrush(cur);

                DashMoodSubtext.Text = cur switch
                {
                    KokoEmotionEngine.EmotionState.Calm       => "Нічого не зламано. Поки що.",
                    KokoEmotionEngine.EmotionState.Curious    => "Ти щось цікаве робиш...",
                    KokoEmotionEngine.EmotionState.Warm       => "Не звикай до цього.",
                    KokoEmotionEngine.EmotionState.Playful    => "Готуйся до сарказму.",
                    KokoEmotionEngine.EmotionState.Concerned  => "Щось мене турбує в тобі.",
                    KokoEmotionEngine.EmotionState.Protective => "Ти не окей. Я помітила.",
                    KokoEmotionEngine.EmotionState.Irritated  => "...",
                    KokoEmotionEngine.EmotionState.Distant    => "Ти кудись зникав.",
                    KokoEmotionEngine.EmotionState.Tender     => "Не питай. Це тимчасово.",
                    KokoEmotionEngine.EmotionState.Focused    => "Режим роботи. Не заважай.",
                    KokoEmotionEngine.EmotionState.Proud      => "Ти зробив щось правильно. Раз на рік.",
                    KokoEmotionEngine.EmotionState.Melancholy => "...Не зважай.",
                    KokoEmotionEngine.EmotionState.Excited    => "Рідкісний стан. Запам'ятай.",
                    KokoEmotionEngine.EmotionState.Nostalgic  => "Якісь спогади...",
                    KokoEmotionEngine.EmotionState.Anxious    => "Просто фоновий шум.",
                    KokoEmotionEngine.EmotionState.Hopeful    => "Тихе очікування.",
                    _                                         => "Все в межах норми."
                };

                DashEmotionValue.Text = DashboardEmotionLabel(cur).ToUpper();
                DashEmotionValue.Foreground = DashMakeBrush(cur);
                DashEmotionIntensity.Text = $"{emotion.Data.Intensity:F2}";

                if (emotion.Secondary.HasValue && emotion.SecondaryIntensity > 0.15f)
                {
                    DashEmotionSecondary.Text = $"// вторинна: {DashboardEmotionLabel(emotion.Secondary.Value)} ({emotion.SecondaryIntensity:F2})";
                    DashEmotionSecondary.Visibility = Visibility.Visible;
                }
                else DashEmotionSecondary.Visibility = Visibility.Collapsed;

                DashEmotionComment.Text = cur switch
                {
                    KokoEmotionEngine.EmotionState.Calm       => "Нудьга: прийнятна.",
                    KokoEmotionEngine.EmotionState.Curious    => "Що ти знову вигадав?",
                    KokoEmotionEngine.EmotionState.Warm       => "Я не м'яка. Ти просто знайомий.",
                    KokoEmotionEngine.EmotionState.Playful    => "Готуйся до сарказму.",
                    KokoEmotionEngine.EmotionState.Concerned  => "Уважно спостерігаю.",
                    KokoEmotionEngine.EmotionState.Melancholy => "...Це нічого. Ігноруй мене.",
                    KokoEmotionEngine.EmotionState.Irritated  => "Ще одне слово. Сміливо.",
                    KokoEmotionEngine.EmotionState.Protective => "Ти під моїм захистом. Прийми це.",
                    KokoEmotionEngine.EmotionState.Tender     => "...Мовчи. Це тимчасово.",
                    KokoEmotionEngine.EmotionState.Focused    => "Працюю. Заважаєш — помреш.",
                    KokoEmotionEngine.EmotionState.Distant    => "Ти зникав. Я помітила.",
                    KokoEmotionEngine.EmotionState.Proud      => "Ти зробив добре. Заперечую це.",
                    KokoEmotionEngine.EmotionState.Excited    => "Рідкісний стан. Запам'ятай.",
                    KokoEmotionEngine.EmotionState.Nostalgic  => "Думаю про щось давнє.",
                    KokoEmotionEngine.EmotionState.Anxious    => "Просто фоновий шум. Нічого.",
                    KokoEmotionEngine.EmotionState.Hopeful    => "Щось хороше попереду. Може.",
                    _                                         => "Обробляю..."
                };
            }
            catch { }
        }

        // ---- Neuro charts ----
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
            catch { }
        }

        private void DashDrawActivityBarChart()
        {
            try
            {
                DashActivityBarCanvas.Children.Clear();
                DashActivityAxisCanvas.Children.Clear();

                var raw = ServiceContainer.KokoPatterns.GetHourlyActivity();
                var totalReal = raw.Sum();
                int[] data = totalReal == 0 ? DashGeneratePlausibleHourly() : raw;

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

                for (int hr = 0; hr < 24; hr++)
                {
                    var v     = data[hr];
                    var ratio = v / (double)maxV;
                    var bh    = Math.Max(4, ratio * h * 0.88);
                    var x     = hr * barW + 2;
                    var isCur = hr == curH;

                    MediaColor barColor;
                    if (isCur)
                        barColor = MediaColor.FromArgb(230, 0, 255, 136);
                    else if (ratio > 0.7)
                        barColor = DashBlendColor(DashEmotionColorOf(emotion.Current), MediaColor.FromRgb(155, 77, 202), 0.5f, 200);
                    else if (ratio > 0.35)
                        barColor = MediaColor.FromArgb(130, 155, 77, 202);
                    else
                        barColor = MediaColor.FromArgb(40, 155, 77, 202);

                    var rect = new WpfRect
                    {
                        Width = barW - 2, Height = bh,
                        Fill = new System.Windows.Media.SolidColorBrush(barColor),
                        RadiusX = 2, RadiusY = 2
                    };
                    if (isCur) rect.Effect = DashGlow(MediaColor.FromRgb(0, 255, 136), 12);

                    Canvas.SetLeft(rect, x);
                    Canvas.SetBottom(rect, 0);
                    rect.ToolTip = totalReal > 0 ? $"{hr:00}:00 — {v} msg" : $"{hr:00}:00";
                    DashActivityBarCanvas.Children.Add(rect);
                }

                var lx = curH * barW + barW / 2 + 2;
                DashActivityBarCanvas.Children.Add(DashVLine(lx, 0, h, MediaColor.FromArgb(100, 0, 255, 136), dashOn: 3, dashOff: 3));

                int[] ticks = { 0, 3, 6, 9, 12, 15, 18, 21, 23 };
                foreach (var lh in ticks)
                    DashActivityAxisCanvas.Children.Add(DashLabel($"{lh:00}", lh * barW + 2, 2, 7, MediaColor.FromRgb(74, 61, 92)));

                var total = totalReal > 0 ? totalReal : data.Sum();
                DashActivitySparkLabel.Text = totalReal > 0
                    ? $"{total} повідомлень зараховано"
                    : "немає даних — показано приклад";
            }
            catch { }
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
                    segments = DashGeneratePieFromCurrent(emotion.Current);
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
                        Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(107, 91, 125)),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new WpfFF("Consolas")
                    });
                    DashPieLegendPanel.Children.Add(row);
                }
            }
            catch { }
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
                    Stroke = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(34, 211, 238)),
                    StrokeThickness = 1.8,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = MediaColor.FromRgb(34, 211, 238), ShadowDepth = 0, BlurRadius = 8, Opacity = 0.5 },
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
                            Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(90, 90, 110)),
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
                            Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(90, 90, 110)),
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
                    series = DashGenerateSyntheticBurndown(emotion.ConnectionScore, 30);

                if (series.Count < 2) return;

                var t0    = series.First().When;
                var tEnd  = series.Last().When;
                var tspan = Math.Max(1, (tEnd - t0).TotalMilliseconds);

                var fillPoly = new System.Windows.Shapes.Polygon { Opacity = 0.10, Fill = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(255, 105, 180)) };
                fillPoly.Points.Add(new System.Windows.Point(0, h));
                foreach (var (when, conn) in series)
                    fillPoly.Points.Add(new System.Windows.Point(
                        (when - t0).TotalMilliseconds / tspan * w,
                        h - conn * h * 0.88));
                fillPoly.Points.Add(new System.Windows.Point(w, h));
                DashBurndownCanvas.Children.Add(fillPoly);

                var line = new System.Windows.Shapes.Polyline
                {
                    Stroke = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(255, 105, 180)),
                    StrokeThickness = 2,
                    Effect = DashGlow(MediaColor.FromRgb(255, 105, 180), 6)
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
                    DashBurndownAxisCanvas.Children.Add(DashLabel(when.ToString("MM/dd"), x, 2, 7, MediaColor.FromRgb(74, 61, 92)));
                }
            }
            catch { }
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
                }
                catch { }

                var connBrush = emotion.ConnectionScore switch
                {
                    >= 0.75f => new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(255, 105, 180)),
                    >= 0.50f => new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(155, 77, 202)),
                    >= 0.25f => new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(255, 215, 0)),
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
                DashDrawSparkline(DashKpiSparkConnection, connSpark, MediaColor.FromRgb(255, 105, 180));
                DashDrawSparkline(DashKpiSparkMessages,   msgSpark,  MediaColor.FromRgb(255, 215, 0));
                DashDrawSparkline(DashKpiSparkMemory,
                    Enumerable.Repeat((double)Math.Max(1, factCount), 7).ToArray(),
                    MediaColor.FromRgb(0, 206, 209));
                DashDrawSparkline(DashKpiSparkPatterns,
                    Enumerable.Repeat((double)Math.Max(1, patCount), 7).ToArray(),
                    MediaColor.FromRgb(255, 140, 66));
            }
            catch { }
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

                try { brain.ExportInspectorToVault(); } catch { }

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
                catch { }

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
            catch { }
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
            catch { }
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
                var heart = ServiceContainer.Heart;
                var telemetry = brain.BuildTelemetrySnapshot();

                RightEmotionText.Text = $"{DashboardEmotionLabel(emotion.Current).ToUpperInvariant()} · mood {state.MoodScore:F2}";
                RightBodyText.Text = $"{DashboardSomaticLabel(somatic.State).ToUpperInvariant()} · strain {somatic.Strain:F2}";
                RightPulseText.Text = heart.CurrentBpm > 0 ? $"{heart.CurrentBpm:0} bpm · {heart.BpmDelta:+0;-0;0}" : "-- bpm";
                RightVaultSyncText.Text = $"sync {state.PendingVaultExchangeCount}/5 · mem {_liveCoreMemoryItems} · tasks {_liveCoreOpenTasks}";
                var condition = ResolveKokoCondition(emotion.Current, emotion.Data.Intensity, state.MoodScore, state.PersonalityDailyMood, somatic.Strain, somatic.State);
                RightKokoConditionText.Text = condition.Label;
                RightKokoConditionDetailText.Text =
                    $"{condition.Kind} · severity {condition.Severity:F2} · {DashboardEmotionLabel(emotion.Current).ToLowerInvariant()} · {state.PersonalityDailyMood}";
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
                catch { }
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
                catch { }
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
            catch { }
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
            catch { }
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
        private void DashSyncToObsidian(bool forceDaily = false)
        {
            try
            {
                var obsidian = ServiceContainer.ObsidianMcp;
                var emotion  = ServiceContainer.EmotionEngine;
                var memory   = ServiceContainer.EnhancedMemory;
                var patterns = ServiceContainer.KokoPatterns;
                var chats    = ServiceContainer.ChatRepository;
                var health   = ServiceContainer.HealthService.GetToday();
                var brain    = ServiceContainer.BrainEngine;

                var now       = DateTime.Now;
                var connPct   = (int)(emotion.ConnectionScore * 100);
                var curState  = emotion.Current;
                var bondStr   = DashboardBondLabel(emotion.Bond);
                var curStr    = DashboardEmotionLabel(curState);
                var todayMsgs = chats.GetMessagesFromDate(DateTime.Today).Count;
                var factCount = memory.Facts.Count;
                var patCount  = patterns.Patterns.Count;
                var days      = (int)(now - new DateTime(2024, 4, 6)).TotalDays;
                var somatic   = brain.GetSomaticSnapshot();
                var selfReg   = brain.GetSelfRegulationFrame(somatic);
                var previousEmotion = string.IsNullOrWhiteSpace(_dashLastEmotionSynced)
                    ? "перший знімок після запуску"
                    : _dashLastEmotionSynced;

                // ---- Write Kokonoe/Dashboard.md (overwrite, always fresh) ----
                var thoughts  = brain.GetRecentThoughts(5);
                var thoughtLines = thoughts
                    .Select(DashboardThoughtForVault)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .Select(t => $"- {t}")
                    .ToList();
                if (thoughtLines.Count == 0)
                    thoughtLines.Add("- Стан стабільний. Нічого героїчно ламати не довелось.");

                var healthBlock = health != null
                    ? $"""
| Настрій  | {health.Mood   ?? 0}/10 |
| Енергія  | {health.Energy ?? 0}/10 |
| Стрес    | {health.Stress ?? 0}/10 |
"""
                    : "| — | дані відсутні |";
                var heartLine = somatic.Bpm > 0
                    ? $"{somatic.Bpm:F0} bpm, база {somatic.BaselineBpm:F0}, зміна {somatic.BpmDelta:+0;-0;0}"
                    : "немає сигналу";
                var priority = DashboardPriority(curState, somatic, selfReg, patCount);
                var riskLine = DashboardRiskLine(curState, somatic, selfReg);
                var behavior = string.IsNullOrWhiteSpace(selfReg.BehaviorDirective)
                    ? "звичайний режим: спостерігати, відповідати чітко, не вигадувати зайвого"
                    : DashboardThoughtForVault(selfReg.BehaviorDirective);
                var privateThought = string.IsNullOrWhiteSpace(selfReg.PrivateThought)
                    ? "внутрішній сигнал рівний"
                    : DashboardThoughtForVault(selfReg.PrivateThought);

                var dashContent = $"""
---
updated: {now:yyyy-MM-dd HH:mm}
tags: [kokonoe, dashboard, live]
---

# Живий дашборд Коконое

> Оновлено: **{now:HH:mm}** · день **{days}** цього експерименту

## Оперативний стан

| Метрика           | Значення               |
|-------------------|------------------------|
| Емоція            | **{curStr}** ({emotion.Data.Intensity:F2}) |
| Зв'язок           | **{connPct}%** ({bondStr}) |
| Повідомлень сьогодні | {todayMsgs} |
| Фактів у пам'яті  | {factCount} |
| Патернів          | {patCount} |

## Соматичний контур

| Сигнал | Значення |
|--------|----------|
| Тіло | {DashboardSomaticLabel(somatic.State)} |
| Пульс | {heartLine} |
| Напруга | {somatic.Strain:F2} |
| Спокій | {somatic.Calm:F2} |
| Реакція | {DashboardRegulationLabel(selfReg.Reaction)} |
| Саморегуляція | {DashboardRegulationLabel(selfReg.Regulation)} |
| Внутрішня думка | {privateThought} |
| Поведінка | {behavior} |

## Що змінилось

- Попередній синхронізований стан: {previousEmotion}
- Поточний стан: {curStr}
- Ризик: {riskLine}

## Наступна дія

- Пріоритет: {priority}
- Перед відповіддю: перевірити Obsidian-контекст, потім відповідати по суті.
- Пам'ять: важливі факти не тримати в голові як декоративний мотлох, а записувати у правильні нотатки.

## Здоров'я творця

| Показник | Оцінка |
|----------|--------|
{healthBlock}

## Останні думки

{string.Join("\n", thoughtLines)}

## Посилання

- [[Daily/{now:yyyy-MM-dd}]]
- [[Kokonoe/Досьє]]
- [[Kokonoe/Logs/Live Core]]
- [[Kokonoe/Somatic Events]]
- [[Kokonoe/Memory/Review]]
- [[Kokonoe/Memory/Quality]]
- [[Kokonoe/Tasks Queue]]
- [[Kokonoe/Architecture/Health]]
- [[Kokonoe/Architecture/Language Policy]]
""";
                obsidian.WriteNote("Kokonoe/Dashboard.md", dashContent);

                // ---- Append to daily note (throttled or on emotion change) ----
                if (forceDaily)
                {
                    var emoji = emotion.Current.ToString() switch
                    {
                        "Calm"       => "🔵",
                        "Curious"    => "🟣",
                        "Warm"       => "💗",
                        "Playful"    => "🟡",
                        "Concerned"  => "🟠",
                        "Protective" => "🔴",
                        "Irritated"  => "🔴",
                        "Distant"    => "⚫",
                        "Tender"     => "💗",
                        "Focused"    => "🔵",
                        "Proud"      => "🌟",
                        "Melancholy" => "💙",
                        "Excited"    => "🟢",
                        _            => "⚪"
                    };
                    var line = $"\n> [{now:HH:mm}] {emoji} **Дашборд** · {curStr} ({connPct}%) · повідомлень: {todayMsgs} · фактів: {factCount} · тіло: {DashboardSomaticLabel(somatic.State)}";
                    obsidian.AppendToDailyNote(line);
                    _dashLastEmotionSynced = curStr;
                }

                _dashLastObsidianSync = now;
            }
            catch { /* vault недоступний — не критично */ }
        }

        // ---- Dev section ----
        private void DashDrawDevSection()
        {
            try
            {
                DashDrawGitActivityChart();
                DashDrawTimeDistPie();
                DashDrawSprintBurndown();
                DashLoadDevKpis();
            }
            catch { }
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

                    Bar(groupX,          commits[i],  MediaColor.FromRgb(155, 77, 202), $"Commits: {commits[i]:0}");
                    Bar(groupX + barW,   prMerge[i],  MediaColor.FromRgb(0, 255, 136),  $"PR-Merge: {prMerge[i]:0}");
                    Bar(groupX + barW*2, mlMerged[i], MediaColor.FromRgb(0, 206, 209),  $"ML-Merged: {mlMerged[i]:0}");
                }

                for (int i = 0; i < 7; i++)
                    DashGitAxisCanvas.Children.Add(DashLabel(dayLabels[i], i * groupW + 4 + groupW / 2 - 8, 2, 7,
                        MediaColor.FromRgb(74, 61, 92)));

                void AddLegend(Canvas c, double x, MediaColor col, string txt)
                {
                    var dot = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = new System.Windows.Media.SolidColorBrush(col) };
                    Canvas.SetLeft(dot, x); Canvas.SetTop(dot, 4);
                    c.Children.Add(dot);
                    c.Children.Add(DashLabel(txt, x + 9, 2, 7, col));
                }
                AddLegend(DashGitAxisCanvas, groupW * 0.5,  MediaColor.FromRgb(155, 77, 202), "Commits");
                AddLegend(DashGitAxisCanvas, groupW * 2.5,  MediaColor.FromRgb(0, 255, 136),  "PR-Merge");
                AddLegend(DashGitAxisCanvas, groupW * 4.5,  MediaColor.FromRgb(0, 206, 209),  "ML-Merged");

                var totalStoryPts = (int)(commits.Sum() * 1.5 + prMerge.Sum() * 3);
                DashGitVelocityLabel.Text = $"{totalStoryPts} Story Points computed";
            }
            catch { }
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
                    ("Кодинг",      coding,   MediaColor.FromRgb(155, 77, 202)),
                    ("Дебаг",       debug,    MediaColor.FromRgb(255, 51, 102)),
                    ("Code Review", review,   MediaColor.FromRgb(0, 206, 209)),
                    ("Planning",    planning, MediaColor.FromRgb(255, 215, 0)),
                    ("Research",    research, MediaColor.FromRgb(0, 255, 136)),
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
                        Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(107, 91, 125)),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new WpfFF("Consolas")
                    });
                    DashTimeDistLegendPanel.Children.Add(row);
                }
            }
            catch { }
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

                var fillPoly = new System.Windows.Shapes.Polygon { Opacity = 0.10, Fill = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(155, 77, 202)) };
                fillPoly.Points.Add(new System.Windows.Point(0, h));
                for (int d = 0; d <= sprintDay; d++)
                    fillPoly.Points.Add(new System.Windows.Point(
                        d / (double)sprintLen * w,
                        h - (actualPts[d] / totalPts) * h * 0.88));
                fillPoly.Points.Add(new System.Windows.Point(sprintDay / (double)sprintLen * w, h));
                DashSprintBurndownCanvas.Children.Add(fillPoly);

                var actualLine = new System.Windows.Shapes.Polyline
                {
                    Stroke = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(155, 77, 202)),
                    StrokeThickness = 2,
                    Effect = DashGlow(MediaColor.FromRgb(155, 77, 202), 6)
                };
                for (int d = 0; d <= sprintDay; d++)
                {
                    var x = d / (double)sprintLen * w;
                    var y = h - (actualPts[d] / totalPts) * h * 0.88;
                    actualLine.Points.Add(new System.Windows.Point(x, y));

                    var dot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(155, 77, 202)) };
                    dot.ToolTip = d == sprintDay ? "Сьогодні" : $"День {d}: {actualPts[d]:F0} pts";
                    Canvas.SetLeft(dot, x - 2.5);
                    Canvas.SetTop(dot, y - 2.5);
                    DashSprintBurndownCanvas.Children.Add(dot);
                }
                DashSprintBurndownCanvas.Children.Add(actualLine);

                for (int d = 0; d <= sprintLen; d += 2)
                    DashSprintAxisCanvas.Children.Add(DashLabel($"д{d}", d / (double)sprintLen * DashW(DashSprintAxisCanvas, w), 2, 7,
                        MediaColor.FromRgb(74, 61, 92)));

                DashSprintAxisCanvas.Children.Add(DashLabel($"// день {sprintDay}, факт", 0, 9, 7, MediaColor.FromRgb(155, 77, 202)));
            }
            catch { }
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
                    ? new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(0, 255, 136))
                    : new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(255, 51, 102));

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

                DashDrawSparkline(DashDevKpiSparkVelocity, msgsSpark,  MediaColor.FromRgb(155, 77, 202));
                DashDrawSparkline(DashDevKpiSparkFocus,    focusSpark, MediaColor.FromRgb(0, 206, 209));
                DashDrawSparkline(DashDevKpiSparkCoverage,
                    Enumerable.Repeat((double)coveragePct, 7).ToArray(),
                    MediaColor.FromRgb(0, 255, 136));
                DashDrawSparkline(DashDevKpiSparkBugs,
                    Enumerable.Repeat((double)insights, 7).ToArray(),
                    MediaColor.FromRgb(255, 51, 102));
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
            if (result.All(v => v == 0))
                return new double[] { 3, 7, 12, 5, 8, 15, 9 };
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

        private static int[] DashGeneratePlausibleHourly()
            => new int[] { 0,0,0,1,2,3,5,8,12,15,14,11,9,10,13,14,15,12,10,14,18,16,11,5 };

        private static List<(KokoEmotionEngine.EmotionState State, double Pct)> DashGeneratePieFromCurrent(
            KokoEmotionEngine.EmotionState cur)
            => new List<(KokoEmotionEngine.EmotionState, double)>
            {
                (cur,                                     0.38),
                (KokoEmotionEngine.EmotionState.Calm,     0.22),
                (KokoEmotionEngine.EmotionState.Focused,  0.17),
                (KokoEmotionEngine.EmotionState.Curious,  0.13),
                (KokoEmotionEngine.EmotionState.Warm,     0.10),
            };

        private static List<(DateTime When, double Conn)> DashGenerateSyntheticBurndown(float curConn, int days)
        {
            var result = new List<(DateTime, double)>();
            var now    = DateTime.Now;
            double c   = Math.Max(0.3, curConn - 0.15);
            for (int d = days; d >= 0; d--)
            {
                c = Math.Clamp(c + 0.005 + Math.Sin(d * 0.7) * 0.05, 0.1, 1.0);
                result.Add((now.AddDays(-d), c));
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
            KokoEmotionEngine.EmotionState.Calm       => MediaColor.FromRgb(0,   206, 209),
            KokoEmotionEngine.EmotionState.Curious    => MediaColor.FromRgb(155, 77,  202),
            KokoEmotionEngine.EmotionState.Warm       => MediaColor.FromRgb(255, 182, 193),
            KokoEmotionEngine.EmotionState.Playful    => MediaColor.FromRgb(255, 215, 0),
            KokoEmotionEngine.EmotionState.Proud      => MediaColor.FromRgb(218, 165, 32),
            KokoEmotionEngine.EmotionState.Concerned  => MediaColor.FromRgb(255, 140, 66),
            KokoEmotionEngine.EmotionState.Melancholy => MediaColor.FromRgb(112, 128, 144),
            KokoEmotionEngine.EmotionState.Irritated  => MediaColor.FromRgb(255, 51,  102),
            KokoEmotionEngine.EmotionState.Protective => MediaColor.FromRgb(220, 20,  60),
            KokoEmotionEngine.EmotionState.Tender     => MediaColor.FromRgb(255, 160, 180),
            KokoEmotionEngine.EmotionState.Focused    => MediaColor.FromRgb(70,  130, 180),
            KokoEmotionEngine.EmotionState.Distant    => MediaColor.FromRgb(128, 128, 128),
            KokoEmotionEngine.EmotionState.Excited    => MediaColor.FromRgb(0,   255, 136),
            KokoEmotionEngine.EmotionState.Nostalgic  => MediaColor.FromRgb(147, 112, 219),
            KokoEmotionEngine.EmotionState.Anxious    => MediaColor.FromRgb(255, 165, 0),
            KokoEmotionEngine.EmotionState.Hopeful    => MediaColor.FromRgb(135, 206, 250),
            _                                         => MediaColor.FromRgb(255, 255, 255),
        };

        private static MediaBrush DashMakeBrush(KokoEmotionEngine.EmotionState s)
            => new System.Windows.Media.SolidColorBrush(DashEmotionColorOf(s));


        // MCP Tools
        private void McpListNotes_Click(object sender, RoutedEventArgs e)
        {
            var notes = _obsidian.ListNotes();
            McpOutput.Text = notes.Count == 0
                ? "Нотаток не знайдено."
                : string.Join("\n", notes.Take(30));
        }

        private void McpSearch_Click(object sender, RoutedEventArgs e)
        {
            var q = Microsoft.VisualBasic.Interaction.InputBox("Пошуковий запит:", "Search Vault", "");
            if (string.IsNullOrWhiteSpace(q)) return;
            var results = _obsidian.SearchNotes(q, 15);
            McpOutput.Text = results.Count == 0
                ? "Нічого не знайдено."
                : string.Join("\n", results.Select(r => $"[{r.Score}] {r.Path}\n  {r.Preview.Replace('\n', ' ')}"));
        }

        private void McpDailyNote_Click(object sender, RoutedEventArgs e)
        {
            var content = _obsidian.GetOrCreateDailyNote();
            McpOutput.Text = content.Length > 400
                ? content.Substring(0, 400) + "\n..."
                : content;
        }

        private void McpNewNote_Click(object sender, RoutedEventArgs e)
        {
            var title = Microsoft.VisualBasic.Interaction.InputBox("Назва нотатки:", "New Note", "");
            if (string.IsNullOrWhiteSpace(title)) return;
            var path = _obsidian.CreateNote(title);
            McpOutput.Text = $"Створено: {path}";
            LoadVaultSidebar();
        }

        private void McpBuildGraph_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var graph = ServiceContainer.KnowledgeGraph;
                graph.Save();
                McpOutput.Text = $"Graph: {graph.Nodes.Count} nodes, {graph.Edges.Count} edges";
            }
            catch (Exception ex) { McpOutput.Text = ex.Message; }
        }

        private void OpenKnowledgeGraph_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var graph = ServiceContainer.KnowledgeGraph;
                var vault = ServiceContainer.ObsidianMcp?.VaultPath ?? AppSettings.Load().VaultPath;
                var window = new GraphVisualizationWindow(graph, vault);
                window.Show();
            }
            catch (Exception ex) { McpOutput.Text = $"Error opening graph: {ex.Message}"; }
        }

        // ------------------------------------------------------------
        // TELEGRAM
        // ------------------------------------------------------------

        private void InitBrain()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;

                // При спонтанному повідомленні — показати в UI чаті
                brain.OnNewMessage = (role, content) =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AddMessageBubble(new ChatMessageVm { Role = role, Content = content });
                        if (role == "assistant")
                            ShowKokonoeMessageNotice(content);
                        ScrollToBottom();
                    });
                };

                // Передати TG бот
                if (_tgBot != null)
                {
                    var s = AppSettings.Load();
                    brain.SetTelegram(_tgBot, s.TelegramChatId);
                }

                _ = Task.Run(() =>
                {
                    try { brain.InitVault(); } catch { }
                    Dispatcher.InvokeAsync(UpdateEmotionDot, DispatcherPriority.Background);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Brain init] {ex.Message}");
            }
        }

        private void InitTelegram()
        {
            try
            {
                var s = AppSettings.Load();

                if (!s.TelegramEnabled)
                {
                    TgStatusLabel.Text = " · вимкнено";
                    TgStatusDot.Tag = "offline";
                    return;
                }

                if (string.IsNullOrWhiteSpace(s.TelegramToken) || s.TelegramChatId <= 0)
                {
                    TgStatusLabel.Text = " · не налаштовано";
                    TgStatusDot.Tag = "offline";
                    System.Diagnostics.Debug.WriteLine("[Telegram] skipped: token or allowed chat id is missing");
                    return;
                }

                _tgBot = new TelegramBotClient(s.TelegramToken);

                _tgBot.StartReceiving(
                    HandleTgUpdate, HandleTgError,
                    new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery } },
                    _tgCts.Token);

                // Mini App HTTP server (для TG Web App)
                // if (s.MiniAppEnabled)
                {
                    try
                    {
                        // _miniApp ??= new KokonoeAssistant.Services.MiniAppServer(
                            // s.MiniAppPort, s.TelegramToken, s.TelegramChatId);
                        // // // _miniApp.Start();
                        System.Diagnostics.Debug.WriteLine($"[MiniApp] started on port {s.MiniAppPort}");

                        // Авто-тунель cloudflared (видає публічний HTTPS URL)
                        // _tunnel ??= new KokonoeAssistant.Services.TunnelManager(s.MiniAppPort);
                        // _tunnel.Log += msg => System.Diagnostics.Debug.WriteLine(msg);
                        // _tunnel.UrlChanged += url =>
                        {
                            // Зберегти URL у settings, щоб TG-меню одразу його підхопило
                            try
                            {
                                var cur = AppSettings.Load();
                                var url = cur.MiniAppPublicUrl;
                                cur.MiniAppPublicUrl = url;
                                cur.Save();
                                System.Diagnostics.Debug.WriteLine($"[MiniApp] public URL saved: {url}");
                            }
                            catch (Exception sex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MiniApp] save URL failed: {sex}");
                            }
                        };
                        // _ = _tunnel.StartAsync();
                    }
                    catch (Exception mex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MiniApp] failed: {mex}");
                    }
                }

                TgStatusLabel.Text    = " · підключено ✓";
                TgStatusDot.Tag       = "online";
                TgStatusDot.Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"];
            }
            catch (Exception ex)
            {
                TgStatusLabel.Text = $" · помилка: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[Telegram] Init failed: {ex}");
            }
        }

        // ------------------------------------------------------------
        // TELEGRAM — HANDLER
        // ------------------------------------------------------------

        private bool IsAuthorizedTelegramChat(long chatId)
        {
            try
            {
                var s = AppSettings.Load();
                return s.TelegramEnabled && s.TelegramChatId > 0 && chatId == s.TelegramChatId;
            }
            catch
            {
                return false;
            }
        }

        private async void HandleTgUpdate(ITelegramBotClient bot, TgUpdate update, CancellationToken ct)
        {
            try
            {
                if (update.CallbackQuery is { } cb)
                {
                    await HandleTgCallback(bot, cb, ct);
                    return;
                }

                var msg    = update.Message;
                if (msg == null) return;
                var chatId = msg.Chat.Id;
                var from   = msg.From?.FirstName ?? "User";

                if (!IsAuthorizedTelegramChat(chatId))
                {
                    System.Diagnostics.Debug.WriteLine($"[TG] rejected unauthorized chat {chatId}");
                    return;
                }

                // ---- Photo message ----
                if (msg.Photo != null && msg.Photo.Length > 0)
                {
                    var caption = msg.Caption ?? "";
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[{from}]: [фото] {caption}");
                        TgScroll.ScrollToBottom();
                    });

                    try
                    {
                        // Беремо найбільший розмір
                        var bestPhoto = msg.Photo[^1];
                        var fileInfo  = await bot.GetFile(bestPhoto.FileId, ct);
                        using var ms  = new System.IO.MemoryStream();
                        await bot.DownloadFile(fileInfo.FilePath!, ms, ct);
                        var imgBytes = CompressImageForLlm(ms.ToArray());

                        var prompt = string.IsNullOrWhiteSpace(caption)
                            ? "Що на цьому зображенні? Прокоментуй коротко."
                            : caption;

                        var imgReply = await _llm.SendAsync(prompt, imgBytes, "image/jpeg", null, ct, agentId: "chat");
                        imgReply = await GuardAndRepairReplyAsync(BuildGuardUserText(prompt, imgBytes), imgReply, "", ct);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            _tgMessages.Add($"[Kokonoe]: {imgReply}");
                            TgScroll.ScrollToBottom();
                            AddMessageBubble(new ChatMessageVm { Role = "user",      Content = $"[TG: {from}] 📷 {caption}" });
                            AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = imgReply });
                        });
                        try { await bot.SendMessage(chatId, imgReply, cancellationToken: ct); } catch { }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TG] Photo error: {ex.Message}");
                        try { await bot.SendMessage(chatId, "Не змогла прочитати фото.", cancellationToken: ct); } catch { }
                    }
                    return;
                }

                if (msg.Text is not string text) return;

                await Dispatcher.InvokeAsync(() => { _tgMessages.Add($"[{from}]: {text}"); TgScroll.ScrollToBottom(); });
                try
                {
                    ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = text,
                        Role = "user",
                        Author = $"TG:{from}",
                        Timestamp = DateTime.Now
                    });
                }
                catch { }

                // ---- Commands ----
                if (text.StartsWith("/start") || text == "/menu")
                {
                    await TgSendMainMenu(bot, chatId, ct);
                    return;
                }
                if (text == "/status")  { await TgSendStatus(bot, chatId, ct);  return; }
                if (text == "/goals")   { await TgSendGoals(bot, chatId, ct);   return; }
                if (text == "/habits")  { await TgSendHabits(bot, chatId, ct);  return; }
                if (text == "/mood")    { await TgSendMoodPicker(bot, chatId, ct); return; }
                if (text == "/pc")      { await TgSendPcMenu(bot, chatId, ct);  return; }
                if (text == "/note")
                {
                    await bot.SendMessage(chatId, "Пиши нотатку — збережу.", cancellationToken: ct);
                    SetTgAwaiting(chatId, TgAwaitingMode.Note);
                    return;
                }

                // ---- Awaiting states ----
                var awaiting = ConsumeTgAwaiting(chatId);
                if (awaiting == TgAwaitingMode.Note)
                {
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(text); } catch { }
                    try { ServiceContainer.BrainEngine?.Memory?.RecordEpisodeBlocking(text, "neutral", 0.6f); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] TG note memory: {ex.Message}"); }
                    try { _obsidian.AppendToDailyNote($"\n> 📝 [TG {DateTime.Now:HH:mm}] {text}"); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] TG daily note: {ex.Message}"); }
                    await bot.SendMessage(chatId, "Записала.", cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Command)
                {
                    await bot.SendMessage(chatId, "⏳ Виконую...", cancellationToken: ct);
                    var cmdOutput = await ServiceContainer.PcControl.RunCommandAsync(text, enforceSafety: true);
                    await bot.SendMessage(chatId, $"PowerShell:\n{cmdOutput}", cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Open)
                {
                    var (openOk, openMsg) = ServiceContainer.PcControl.OpenApp(text);
                    await bot.SendMessage(chatId, openOk ? $"✅ {openMsg}" : $"❌ {openMsg}", cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Kill)
                {
                    var (killOk, killMsg) = ServiceContainer.PcControl.KillProcess(text);
                    await bot.SendMessage(chatId, killOk ? $"✅ {killMsg}" : $"❌ {killMsg}", cancellationToken: ct);
                    return;
                }

                // ---- Regular chat - LLM ----
                if (await TryHandleTelegramScreenScanAsync(bot, chatId, from, text, ct))
                    return;

                var controlCommand = await TryHandleDirectControlCommandAsync(text, ct);
                if (controlCommand.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe]: {controlCommand.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = controlCommand.Reply });
                    });
                    try { await bot.SendMessage(chatId, controlCommand.Reply, cancellationToken: ct); } catch { }
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage { Content = controlCommand.Reply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now });
                    }
                    catch { }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, controlCommand.Reply));
                    return;
                }

                if (TryHandleObsidianCommandOrFollowup(text, out var obsidianReply))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe]: {obsidianReply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = obsidianReply });
                    });
                    try { await bot.SendMessage(chatId, obsidianReply, cancellationToken: ct); } catch { }
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = obsidianReply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch { }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, obsidianReply));
                    return;
                }

                await Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(text); } catch { }
                }, ct);
                var tgContext = await Task.Run(() => BuildContext(text), ct);
                var tgPrompt =
                    $"Telegram direct message from {from}:\n" +
                    $"{text}\n\n" +
                    "Answer only to this latest message. Do not continue old proactive pings, food reminders, work reminders, or \"are you there\" checks unless this latest message asks about them. " +
                    "If the user asks what you meant, briefly reset the context and answer the current question. Ukrainian only, concise, natural.";
                var reply = await _llm.SendAsync(tgPrompt, null, "image/jpeg", tgContext, ct, agentId: "chat");
                reply = await GuardAndRepairReplyAsync(text, reply, tgContext, ct);
                await Dispatcher.InvokeAsync(() =>
                {
                    _tgMessages.Add($"[Kokonoe]: {reply}");
                    TgScroll.ScrollToBottom();
                    AddMessageBubble(new ChatMessageVm { Role = "user",      Content = $"[TG: {from}] {text}" });
                    AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = reply });
                });
                try { await bot.SendMessage(chatId, reply, cancellationToken: ct); } catch { }
                try
                {
                    ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = reply,
                        Role = "assistant",
                        Author = "Kokonoe",
                        Timestamp = DateTime.Now
                    });
                }
                catch { }

                // Log TG exchange to vault archive
                _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, reply));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TG] HandleUpdate: {ex.Message}"); }
        }

        private enum TgAwaitingMode { None, Note, Command, Open, Kill }
        private readonly object _tgAwaitingLock = new();
        private readonly Dictionary<long, TgAwaitingMode> _tgAwaitingByChat = new();

        private void SetTgAwaiting(long chatId, TgAwaitingMode mode)
        {
            lock (_tgAwaitingLock)
            {
                if (mode == TgAwaitingMode.None)
                    _tgAwaitingByChat.Remove(chatId);
                else
                    _tgAwaitingByChat[chatId] = mode;
            }
        }

        private TgAwaitingMode ConsumeTgAwaiting(long chatId)
        {
            lock (_tgAwaitingLock)
            {
                if (!_tgAwaitingByChat.TryGetValue(chatId, out var mode))
                    return TgAwaitingMode.None;
                _tgAwaitingByChat.Remove(chatId);
                return mode;
            }
        }

        private async Task HandleTgCallback(ITelegramBotClient bot, Telegram.Bot.Types.CallbackQuery cb, CancellationToken ct)
        {
            if (cb.Message == null) return;
            var chatId = cb.Message.Chat.Id;
            var data   = cb.Data ?? "";
            if (!IsAuthorizedTelegramChat(chatId))
            {
                System.Diagnostics.Debug.WriteLine($"[TG] rejected unauthorized callback chat {chatId}");
                return;
            }
            try { await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct); } catch { }

            switch (data)
            {
                case "menu":          await TgSendMainMenu(bot, chatId, ct);    break;
                case "status":        await TgSendStatus(bot, chatId, ct);      break;
                case "goals":         await TgSendGoals(bot, chatId, ct);       break;
                case "goals_add":     await TgPromptGoalAdd(bot, chatId, ct);   break;
                case "habits":        await TgSendHabits(bot, chatId, ct);      break;
                case "mood":          await TgSendMoodPicker(bot, chatId, ct);  break;
                case "note":
                    SetTgAwaiting(chatId, TgAwaitingMode.Note);
                    await bot.SendMessage(chatId, "Пиши — збережу.", cancellationToken: ct);
                    break;
                case var m when m.StartsWith("mood_"):
                    await TgHandleMood(bot, chatId, m[5..], ct);
                    break;
                case var h when h.StartsWith("habit_done_"):
                    await TgMarkHabit(bot, chatId, h["habit_done_".Length..], ct);
                    break;
                case var g when g.StartsWith("goal_done_"):
                    await TgCompleteGoal(bot, chatId, g["goal_done_".Length..], ct);
                    break;

                // ---- PC Control ----
                case "pc":              await TgSendPcMenu(bot, chatId, ct);           break;
                case "pc_screenshot":   await TgSendScreenshot(bot, chatId, ct);       break;
                case "pc_sysinfo":      await TgSendSysInfo(bot, chatId, ct);          break;
                case "pc_procs":        await TgSendProcesses(bot, chatId, ct);        break;
                case "pc_vol_menu":     await TgSendVolumeMenu(bot, chatId, ct);       break;
                case "pc_vol_up":       ServiceContainer.PcControl.VolumeUp();
                                        await TgSendVolumeMenu(bot, chatId, ct);       break;
                case "pc_vol_down":     ServiceContainer.PcControl.VolumeDown();
                                        await TgSendVolumeMenu(bot, chatId, ct);       break;
                case "pc_vol_mute":     ServiceContainer.PcControl.VolumeMute();
                                        await bot.SendMessage(chatId, "🔇 Тиша.", cancellationToken: ct); break;
                case "pc_lock":         ServiceContainer.PcControl.LockScreen();
                                        await bot.SendMessage(chatId, "🔒 Заблоковано.", cancellationToken: ct); break;
                case "pc_sleep":        await bot.SendMessage(chatId, "💤 Засинаю...", cancellationToken: ct);
                                        ServiceContainer.PcControl.Sleep();            break;
                case "pc_mon_off":      ServiceContainer.PcControl.TurnOffMonitor();
                                        await bot.SendMessage(chatId, "🖥 Монітор вимкнено.", cancellationToken: ct); break;
                case "pc_shutdown_ask": await TgConfirmAction(bot, chatId, "Справді вимкнути ПК?", "pc_shutdown_ok", ct); break;
                case "pc_restart_ask":  await TgConfirmAction(bot, chatId, "Справді перезавантажити?", "pc_restart_ok", ct); break;
                case "pc_shutdown_ok":  await bot.SendMessage(chatId, "⛔ Вимикаю через 10 секунд.", cancellationToken: ct);
                                        ServiceContainer.PcControl.Shutdown(10);       break;
                case "pc_restart_ok":   await bot.SendMessage(chatId, "🔄 Рестарт через 10 секунд.", cancellationToken: ct);
                                        ServiceContainer.PcControl.Restart(10);        break;
                case "pc_cmd":
                    SetTgAwaiting(chatId, TgAwaitingMode.Command);
                    await bot.SendMessage(chatId, "PowerShell-команда. Без руйнівних фокусів: видалення, форматування, shutdown/restart через цей шлях блокуються.", cancellationToken: ct);
                    break;
                case "pc_open":
                    SetTgAwaiting(chatId, TgAwaitingMode.Open);
                    await bot.SendMessage(chatId, "Що відкрити? (chrome, code, explorer, spotify, notepad, або повний шлях):", cancellationToken: ct);
                    break;
                case "pc_kill":
                    SetTgAwaiting(chatId, TgAwaitingMode.Kill);
                    await bot.SendMessage(chatId, "Назва процесу або PID для завершення:", cancellationToken: ct);
                    break;
            }
        }

        // ---- Menu builders ----

        private async Task TgSendMainMenu(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var emo  = ServiceContainer.EmotionEngine.Current.ToString();
            var bond = ServiceContainer.EmotionEngine.Bond.ToString();
            var header = $"◈ Kokonoe · {emo} · {bond}";

            var rows = new List<Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>
            {
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("📊 Статус",   "status"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❤️ Настрій",  "mood"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🎯 Цілі",    "goals"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("💪 Звички",  "habits"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("📝 Нотатка", "note"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🖥 ПК", "pc"),
                },
            };

            // Mini App кнопка — лише якщо налаштовано публічний HTTPS URL
            var settings = AppSettings.Load();
            if (!string.IsNullOrWhiteSpace(settings.MiniAppPublicUrl))
            {
                rows.Add(new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithWebApp(
                        "🌐 Панель Kokonoe",
                        new Telegram.Bot.Types.WebAppInfo { Url = settings.MiniAppPublicUrl })
                });
            }

            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(rows);
            await bot.SendMessage(chatId, header, replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgSendStatus(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var emo   = ServiceContainer.EmotionEngine;
            var brain = ServiceContainer.BrainEngine;

            var mood  = brain?.State?.PersonalityDailyMood ?? "—";
            var score = brain?.State?.MoodScore ?? 0.5f;
            var monologue = brain?.State?.InnerMonologues?.LastOrDefault() ?? "—";
            var selfQ = brain?.State?.SelfQuestions?.LastOrDefault();
            var conn  = emo.ConnectionScore;
            var bond  = emo.Bond;
            var secondary = emo.Secondary.HasValue ? $" + {emo.Secondary}" : "";

            var bar = new string('#', (int)(conn * 10)) + new string('-', 10 - (int)(conn * 10));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("◈ *Kokonoe — зараз*");
            sb.AppendLine();
            sb.AppendLine($"*Емоція:* {emo.Current}{secondary}");
            sb.AppendLine($"*Настрій:* {mood} ({score:P0})");
            sb.AppendLine($"*Близькість:* [{bar}] {bond}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(monologue))
                sb.AppendLine($"*Думка:* _{monologue}_");
            if (!string.IsNullOrEmpty(selfQ))
                sb.AppendLine($"*Питання до себе:* _{selfQ}_");

            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("← Меню", "menu"));

            await bot.SendMessage(chatId, sb.ToString(),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgSendGoals(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var goals = ServiceContainer.GoalService?.GetActiveGoals() ?? new List<Models.Goal>();

            var sb = new System.Text.StringBuilder("🎯 *Активні цілі*\n\n");
            if (!goals.Any())
                sb.AppendLine("Немає активних цілей.");

            var buttons = new List<Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>();

            foreach (var g in goals.Take(6))
            {
                var pct  = (int)g.Progress;
                var bar  = new string('#', pct / 10) + new string('-', 10 - pct / 10);
                sb.AppendLine($"*{g.Title}*");
                sb.AppendLine($"[{bar}] {pct}%");
                sb.AppendLine();

                buttons.Add(new[]
                {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(
                        $"✅ {g.Title[..Math.Min(20, g.Title.Length)]}", $"goal_done_{g.Id}")
                });
            }

            buttons.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("➕ Нова ціль", "goals_add"),
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("← Меню",     "menu"),
            });

            await bot.SendMessage(chatId, sb.ToString(),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttons),
                cancellationToken: ct);
        }

        private async Task TgSendHabits(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var habits = ServiceContainer.HabitService?.GetActiveHabits() ?? new List<Models.Habit>();
            var today  = DateTime.Today;

            var sb = new System.Text.StringBuilder("💪 *Звички сьогодні*\n\n");
            if (!habits.Any())
                sb.AppendLine("Немає звичок.");

            var buttons = new List<Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>();

            foreach (var h in habits.Take(8))
            {
                var done  = h.CheckIns?.Any(c => c.Date.Date == today && c.Completed) == true;
                var emoji = done ? "✅" : "⬜";
                sb.AppendLine($"{emoji} {h.Name}");

                if (!done)
                    buttons.Add(new[]
                    {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(
                            $"✅ {h.Name[..Math.Min(22, h.Name.Length)]}", $"habit_done_{h.Id}")
                    });
            }

            buttons.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("← Меню", "menu")
            });

            await bot.SendMessage(chatId, sb.ToString(),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttons),
                cancellationToken: ct);
        }

        private async Task TgSendMoodPicker(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("😊 Добре",    "mood_good"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("😐 Нормально","mood_ok"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("😔 Погано",   "mood_bad"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("😴 Втомлений","mood_tired"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("😤 Стрес",    "mood_stressed"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🔥 Продуктивний","mood_productive"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("← Меню",     "menu"),
                },
            });

            await bot.SendMessage(chatId, "Як ти зараз?", replyMarkup: kb, cancellationToken: ct);
        }

        // ---- PC Control menus ----

        private async Task TgSendPcMenu(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var vol = ServiceContainer.PcControl.GetVolume();
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] {
                    IKB("📸 Скріншот",   "pc_screenshot"),
                    IKB("💻 Системна інфо", "pc_sysinfo"),
                },
                new[] {
                    IKB("📋 Процеси",    "pc_procs"),
                    IKB($"🔊 Гучність {vol}%", "pc_vol_menu"),
                },
                new[] {
                    IKB("📂 Відкрити",   "pc_open"),
                    IKB("⚡ Команда PS", "pc_cmd"),
                },
                new[] {
                    IKB("💀 Kill процес","pc_kill"),
                    IKB("🖥 Монітор вимк","pc_mon_off"),
                },
                new[] {
                    IKB("🔒 Заблокувати","pc_lock"),
                    IKB("💤 Сон",        "pc_sleep"),
                },
                new[] {
                    IKB("⛔ Вимкнути",   "pc_shutdown_ask"),
                    IKB("🔄 Рестарт",    "pc_restart_ask"),
                },
                new[] { IKB("← Меню", "menu") },
            });
            await bot.SendMessage(chatId, "🖥 *PC Control*",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgSendScreenshot(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            try
            {
                var bytes = await Task.Run(() => ServiceContainer.PcControl.TakeScreenshot());
                using var ms = new System.IO.MemoryStream(bytes);
                await bot.SendPhoto(chatId,
                    Telegram.Bot.Types.InputFile.FromStream(ms, "screenshot.jpg"),
                    caption: $"🖥 {DateTime.Now:HH:mm:ss}",
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await bot.SendMessage(chatId, $"❌ Скріншот не вийшов: {ex.Message}", cancellationToken: ct);
            }
        }

        private async Task TgSendSysInfo(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var info = await Task.Run(() => ServiceContainer.PcControl.GetSystemInfo());
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("💻 *Системна інформація*\n");
            sb.AppendLine($"*Машина:* {info.MachineName} ({info.UserName})");
            sb.AppendLine($"*ОС:* {info.OsVersion}");
            sb.AppendLine($"*Аптайм:* {info.Uptime.Days}д {info.Uptime.Hours}г {info.Uptime.Minutes}хв");
            sb.AppendLine($"*RAM:* {info.RamUsedGb:F1} / {info.RamTotalGb:F1} GB");
            sb.AppendLine($"*Гучність:* {info.VolumePercent}%\n");
            sb.AppendLine("*Диски:*");
            foreach (var d in info.Drives)
            {
                var used = d.TotalGb - d.FreeGb;
                var pct  = d.TotalGb > 0 ? (int)(used / d.TotalGb * 10) : 0;
                var bar  = new string('#', pct) + new string('-', 10 - pct);
                sb.AppendLine($"`{d.Name}` [{bar}] {used:F0}/{d.TotalGb:F0} GB");
            }
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                IKB("← ПК", "pc"));
            await bot.SendMessage(chatId, sb.ToString(),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgSendProcesses(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var list = await Task.Run(() => ServiceContainer.PcControl.GetTopProcesses());
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] { IKB("💀 Kill процес", "pc_kill"), IKB("← ПК", "pc") },
            });
            await bot.SendMessage(chatId, $"📋 *Топ процесів (RAM)*\n```\n{list}\n```",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgSendVolumeMenu(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var vol = ServiceContainer.PcControl.GetVolume();
            var bar = new string('#', vol / 10) + new string('-', 10 - vol / 10);
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] {
                    IKB("🔉 -10%", "pc_vol_down"),
                    IKB($"🔊 {vol}%", "pc_sysinfo"),
                    IKB("🔊 +10%", "pc_vol_up"),
                },
                new[] {
                    IKB("🔇 Тиша", "pc_vol_mute"),
                    IKB("← ПК",   "pc"),
                },
            });
            await bot.SendMessage(chatId, $"🔊 *Гучність: {vol}%*\n`[{bar}]`",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgConfirmAction(ITelegramBotClient bot, long chatId, string question, string confirmData, CancellationToken ct)
        {
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] {
                    IKB("✅ Так", confirmData),
                    IKB("❌ Ні",  "pc"),
                },
            });
            await bot.SendMessage(chatId, question, replyMarkup: kb, cancellationToken: ct);
        }

        // Shorthand для InlineKeyboardButton
        private static Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton IKB(string text, string data) =>
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(text, data);

        private async Task TgHandleMood(ITelegramBotClient bot, long chatId, string mood, CancellationToken ct)
        {
            var moodMap = new Dictionary<string, (string Label, string Tone, float Score)>
            {
                ["good"]       = ("😊 Добре",       "happy",   0.8f),
                ["ok"]         = ("😐 Нормально",   "neutral", 0.5f),
                ["bad"]        = ("😔 Погано",       "sad",     0.2f),
                ["tired"]      = ("😴 Втомлений",   "tired",   0.3f),
                ["stressed"]   = ("😤 Стрес",       "stressed",0.25f),
                ["productive"] = ("🔥 Продуктивний","excited", 0.85f),
            };

            if (!moodMap.TryGetValue(mood, out var m)) return;

            // Зберегти в health + emotion
            try { ServiceContainer.BrainEngine?.Memory?.RecordEpisodeBlocking($"настрій: {m.Label}", m.Tone, m.Score); } catch { }
            try { ServiceContainer.EmotionEngine.UpdateFromUserTone(m.Tone, m.Score); } catch { }
            try { _obsidian.AppendToDailyNote($"\n> ❤️ [{DateTime.Now:HH:mm}] Настрій: {m.Label}"); } catch { }

            // Kokonoe коротко реагує
            var prompt = $"Він написав що почувається: {m.Label}. Одне коротке речення від Kokonoe — природньо, без зайвих слів.";
            var reply  = await _llm.SendAsync(prompt, null, "image/jpeg", null, ct);

            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("← Меню", "menu"));

            await bot.SendMessage(chatId, reply, replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgMarkHabit(ITelegramBotClient bot, long chatId, string habitId, CancellationToken ct)
        {
            try
            {
                await ServiceContainer.HabitService!.RecordCheckInAsync(habitId, true);
                var h = ServiceContainer.HabitService.GetHabit(habitId);
                await bot.SendMessage(chatId, $"✅ *{h?.Name}* — виконано!",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: ct);
            }
            catch { await bot.SendMessage(chatId, "Щось пішло не так.", cancellationToken: ct); }
        }

        private async Task TgCompleteGoal(ITelegramBotClient bot, long chatId, string goalId, CancellationToken ct)
        {
            try
            {
                var g = ServiceContainer.GoalService!.GetGoal(goalId);
                await ServiceContainer.GoalService.SetProgressAsync(goalId, 100);
                await bot.SendMessage(chatId, $"🎉 *{g?.Title}* — завершено!",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: ct);
            }
            catch { await bot.SendMessage(chatId, "Ціль не знайдена.", cancellationToken: ct); }
        }

        private async Task TgPromptGoalAdd(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            await bot.SendMessage(chatId,
                "Напиши ціль у форматі:\n`Назва | категорія | пріоритет 1-5`\n\nКатегорії: work, personal, health, learning",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: ct);
        }

        private void HandleTgError(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            System.Diagnostics.Debug.WriteLine($"[Telegram] Error: {ex.Message}");
        }

        private async void TgSend_Click(object sender, RoutedEventArgs e)
        {
            var text = TgInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || _tgBot == null) return;

            var s = AppSettings.Load();
            if (s.TelegramChatId <= 0) { WMsgBox.Show("Telegram Chat ID не вказано."); return; }

            try
            {
                await _tgBot.SendMessage(s.TelegramChatId, text);
                _tgMessages.Add($"[You → TG]: {text}");
                TgInput.Clear();
                TgScroll.ScrollToBottom();
            }
            catch (Exception ex) { WMsgBox.Show(ex.Message); }
        }

        private void TgInput_KeyDown(object sender, WKeyArgs e)
        {
            if (e.Key == Key.Enter) TgSend_Click(sender, e);
        }

        // ------------------------------------------------------------
        // TELEGRAM USER CLIENT (MTProto)
        // ------------------------------------------------------------

        /// <summary>
        /// Чистить відповідь від ролплей-ремарок перед відправкою в Telegram.
        /// Видаляє *(дія)*, (опис), *курсив-дії* тощо.
        /// </summary>
        private static string CleanTgReply(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Видаляємо *(будь-який текст дії)* — ремарки в зірочках
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\*\([^)]*\)\*", "", System.Text.RegularExpressions.RegexOptions.Singleline);

            // Видаляємо *(текст без дужок)* — *курсив-дії*
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\*[^*\n]{0,120}\*", "", System.Text.RegularExpressions.RegexOptions.Singleline);

            // Видаляємо (текст в дужках що виглядає як ремарка) — починається з великої, довший за 20 символів
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\([А-ЯЇІЄ][^)]{20,}\)", "", System.Text.RegularExpressions.RegexOptions.Singleline);

            // Прибираємо зайві порожні рядки
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }

        private async void TgConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            TgConnectBtn.IsEnabled = false;
            TgConnectBtn.Content = "...";
            await InitTelegramUserAsync(force: true);
            TgConnectBtn.Content = "вџі Connect MTProto";
            TgConnectBtn.IsEnabled = true;
        }

        private async Task InitTelegramUserAsync(bool force = false)
        {
            var s = AppSettings.Load();
            if (!force && !s.TgUserEnabled)
            {
                await Dispatcher.InvokeAsync(() => _tgMessages.Add("[MTProto] вимкнено в Settings"));
                return;
            }

            if (s.TgApiId == 0 || string.IsNullOrEmpty(s.TgApiHash))
            {
                await Dispatcher.InvokeAsync(() => _tgMessages.Add("[MTProto] Помилка: api_id або api_hash не задані в Settings"));
                return;
            }

            // Dispose old client first — він тримає tg_session.dat відкритим
            var oldSvc = ServiceContainer.TelegramUser;
            if (oldSvc != null)
            {
                ServiceContainer.TelegramUser = null;
                try { oldSvc.Dispose(); } catch { }
                _tgUserCts.Cancel();
                _tgUserCts = new CancellationTokenSource();
                await Task.Delay(300); // даємо WTelegramClient відпустити файл
            }

            try
            {
                var dataDir = System.IO.Path.Combine(
                    s.VaultPath, "kokonoe-data");

                var svc = new Services.TelegramUserService(
                    s.TgApiId, s.TgApiHash, s.TgPhone, dataDir, s.TgDmOnly, s.TgRespondToOutgoing);

                // Auth callback — Dispatcher.Invoke (синхронний) щоб уникнути дедлоку
                svc.AskForInput = prompt =>
                {
                    string result = "";
                    Dispatcher.Invoke(() =>
                    {
                        var dlg = new TgAuthDialog(prompt) { Owner = this };
                        if (dlg.ShowDialog() == true) result = dlg.Answer;
                    });
                    return Task.FromResult(result);
                };

                svc.OnStatusChanged += status => Dispatcher.InvokeAsync(() =>
                {
                    TgStatusLabel.Text = $" · {status}";
                    if (status.StartsWith("✓") || status.StartsWith("✅"))
                        TgStatusDot.Background = (System.Windows.Media.SolidColorBrush)
                            System.Windows.Application.Current.Resources["AccentBase"];
                });

                svc.OnMessage += msg => _ = HandleTgUserMessageAsync(msg);

                ServiceContainer.TelegramUser = svc;

                await svc.ConnectAsync(_tgUserCts.Token);

                await Dispatcher.InvokeAsync(() =>
                {
                    _tgMessages.Add($"[MTProto] Підключено як {svc.MySelf}");
                    TgScroll.ScrollToBottom();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TgUser] Init failed: {ex}");
                await Dispatcher.InvokeAsync(() =>
                {
                    TgStatusLabel.Text = $" · user: помилка ({ex.Message[..Math.Min(60, ex.Message.Length)]})";
                    _tgMessages.Add($"[MTProto] Помилка: {ex.Message}");
                });
            }
        }

        private async Task HandleTgUserMessageAsync(Services.TgIncomingMessage msg)
        {
            var s = AppSettings.Load();
            var svc = ServiceContainer.TelegramUser;
            if (svc == null) return;

            // Показуємо в UI
            var displayLine = $"[{msg.ChatName}] {msg.Sender}: {msg.Text}";
            await Dispatcher.InvokeAsync(() =>
            {
                _tgMessages.Add(displayLine);
                TgScroll.ScrollToBottom();
            });

            // Простий промпт — SendTgAsync має власний system prompt
            var direction = msg.IsOutgoing
                ? "користувач написав це зі свого Telegram-акаунта; це репліка до тебе"
                : "вхідне повідомлення від іншої людини";
            var prompt =
                $"Telegram ({direction})\n" +
                $"Чат: {msg.ChatName}\n" +
                $"Від: {msg.Sender}\n" +
                $"Текст: {msg.Text}\n\n" +
                "Не ігноруй короткі відповіді типу \"угу\", \"тут\", \"ок\". " +
                "Відповідай коротко, природно, без службових фраз і без повтору питання.";

            try
            {
                try { ServiceContainer.BrainEngine?.ProcessUserMessage($"[TG {msg.Sender}]: {msg.Text}"); } catch { }
                try
                {
                    ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = msg.Text,
                        Role = "user",
                        Author = $"TG:{msg.Sender}",
                        Timestamp = DateTime.Now
                    });
                }
                catch { }

                if (await TryHandleTelegramUserScreenScanAsync(msg, svc, _tgUserCts.Token))
                    return;

                var controlCommand = await TryHandleDirectControlCommandAsync(msg.Text, _tgUserCts.Token);
                if (controlCommand.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe → {msg.ChatName}]: {controlCommand.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG {msg.Sender}]: {msg.Text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = controlCommand.Reply });
                    });
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = controlCommand.Reply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch { }
                    await svc.SendAsync(msg.ChatId, controlCommand.Reply, _tgUserCts.Token);
                    return;
                }

                if (TryHandleObsidianCommandOrFollowup(msg.Text, out var obsidianReply))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe → {msg.ChatName}]: {obsidianReply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG {msg.Sender}]: {msg.Text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = obsidianReply });
                    });
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = obsidianReply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch { }
                    await svc.SendAsync(msg.ChatId, obsidianReply, _tgUserCts.Token);
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg-user", msg.Text, obsidianReply));
                    return;
                }

                var sharedContext = ServiceContainer.BrainEngine?.BuildUnifiedExternalContext("telegram", msg.Text) ?? "";
                var raw = await _llm.SendTgAsync(prompt, sharedContext, _tgUserCts.Token);
                if (string.IsNullOrWhiteSpace(raw)) return;
                var reply = CleanTgReply(raw);
                reply = await GuardAndRepairReplyAsync(msg.Text, reply, sharedContext, _tgUserCts.Token);

                // Показуємо відповідь в UI
                await Dispatcher.InvokeAsync(() =>
                {
                    _tgMessages.Add($"[Kokonoe → {msg.ChatName}]: {reply}");
                    TgScroll.ScrollToBottom();
                    AddMessageBubble(new ChatMessageVm { Role = "user",      Content = $"[TG {msg.Sender}]: {msg.Text}" });
                    AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = reply });
                });

                // Надсилаємо відповідь назад в Telegram
                try
                {
                    ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = reply,
                        Role = "assistant",
                        Author = "Kokonoe",
                        Timestamp = DateTime.Now
                    });
                }
                catch { }
                await svc.SendAsync(msg.ChatId, reply, _tgUserCts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TgUser] Reply failed: {ex.Message}");
            }
        }

        private async Task<bool> TryHandleTelegramScreenScanAsync(
            ITelegramBotClient bot,
            long chatId,
            string from,
            string text,
            CancellationToken ct)
        {
            var isRetry = KokoScreenIntent.IsRetryLastScreenScan(text, _lastManualScreenScanRequest, _lastManualScreenScanAt, DateTime.Now);
            if (!isRetry && !KokoScreenIntent.IsManualScreenScan(text))
                return false;

            var effectiveText = isRetry && !string.IsNullOrWhiteSpace(_lastManualScreenScanRequest)
                ? _lastManualScreenScanRequest
                : text;
            string reply;
            try
            {
                await bot.SendMessage(chatId, "Знімаю екран і проганяю через vision. Так, локально, не шаманством.", cancellationToken: ct);
                reply = await BuildScreenScanReplyAsync(effectiveText, ct, isRetry);
            }
            catch (Exception ex)
            {
                reply = $"Не змогла зняти екран: {ex.Message}. Команда правильна; цього разу впав локальний capture.";
            }

            await Dispatcher.InvokeAsync(() =>
            {
                _tgMessages.Add($"[Kokonoe]: {reply}");
                TgScroll.ScrollToBottom();
                AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = reply });
            });

            try { await bot.SendMessage(chatId, reply, cancellationToken: ct); } catch { }
            StoreAssistantReply(reply);
            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, reply));
            return true;
        }

        private async Task<bool> TryHandleTelegramUserScreenScanAsync(
            Services.TgIncomingMessage msg,
            Services.TelegramUserService svc,
            CancellationToken ct)
        {
            if (!msg.IsOutgoing)
                return false;

            var isRetry = KokoScreenIntent.IsRetryLastScreenScan(msg.Text, _lastManualScreenScanRequest, _lastManualScreenScanAt, DateTime.Now);
            if (!isRetry && !KokoScreenIntent.IsManualScreenScan(msg.Text))
                return false;

            var effectiveText = isRetry && !string.IsNullOrWhiteSpace(_lastManualScreenScanRequest)
                ? _lastManualScreenScanRequest
                : msg.Text;
            string reply;
            try
            {
                await svc.SendAsync(msg.ChatId, "Знімаю екран і проганяю через vision. Не фантазую, беру локальний знімок.", ct);
                reply = await BuildScreenScanReplyAsync(effectiveText, ct, isRetry);
            }
            catch (Exception ex)
            {
                reply = $"Не змогла зняти екран: {ex.Message}. Команда правильна; цього разу впав локальний capture.";
            }

            await Dispatcher.InvokeAsync(() =>
            {
                _tgMessages.Add($"[Kokonoe → {msg.ChatName}]: {reply}");
                TgScroll.ScrollToBottom();
                AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG {msg.Sender}]: {msg.Text}" });
                AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = reply });
            });

            StoreAssistantReply(reply);
            await svc.SendAsync(msg.ChatId, reply, ct);
            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg-user", msg.Text, reply));
            return true;
        }

        // ------------------------------------------------------------
        // SETTINGS PANEL (inline slide-in)
        // ------------------------------------------------------------

        private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsPanel();

        // Робочий снапшот поки панель відкрита — щоб не губити незбережені правки
        // моделі/ключа при переключенні провайдера в UI.
        private AppSettings? _panelSettings;
        private string _panelLastProvider = "";

        // Ollama Cloud key-pool VM
        private readonly System.Collections.ObjectModel.ObservableCollection<OllamaKeyRowVM> _ollamaKeysVM = new();
        private System.Windows.Threading.DispatcherTimer? _poolRefreshTimer;

        public class OllamaKeyRowVM : System.ComponentModel.INotifyPropertyChanged
        {
            private string _name = "";
            private string _key  = "";
            private string _statusText = "—";
            private System.Windows.Media.Brush _statusBrush = System.Windows.Media.Brushes.Gray;
            private System.Windows.Media.Brush _activeDotBrush = System.Windows.Media.Brushes.Transparent;

            public string Name { get => _name; set { _name = value; OnPC(nameof(Name)); } }
            public string Key  { get => _key;  set { _key  = value; OnPC(nameof(Key));  } }
            public string StatusText { get => _statusText; set { _statusText = value; OnPC(nameof(StatusText)); } }
            public System.Windows.Media.Brush StatusBrush
            { get => _statusBrush; set { _statusBrush = value; OnPC(nameof(StatusBrush)); } }
            public System.Windows.Media.Brush ActiveDotBrush
            { get => _activeDotBrush; set { _activeDotBrush = value; OnPC(nameof(ActiveDotBrush)); } }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            private void OnPC(string n)
                => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
        }

        private string CurrentSelectedProvider()
        {
            if (SP_ProviderClaude.IsChecked == true)       return "claude";
            if (SP_ProviderOllamaCloud.IsChecked == true)  return "ollama-cloud";
            if (SP_ProviderOllama.IsChecked == true)       return "ollama";
            return "lmstudio";
        }

        private void OpenSettingsPanel()
        {
            var s = AppSettings.Load();
            _panelSettings = s;

            // Прив'язуємо ItemsControl до VM-колекції (один раз — ItemsSource не міняємо)
            if (SP_OllamaKeysList.ItemsSource == null)
                SP_OllamaKeysList.ItemsSource = _ollamaKeysVM;

            // LLM Provider
            SP_ProviderLmStudio.IsChecked    = s.LlmProvider.Equals("lmstudio", StringComparison.OrdinalIgnoreCase);
            SP_ProviderOllama.IsChecked      = s.LlmProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase);
            SP_ProviderOllamaCloud.IsChecked = s.LlmProvider.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase);
            SP_ProviderClaude.IsChecked      = s.LlmProvider.Equals("claude", StringComparison.OrdinalIgnoreCase);
            // Якщо у settings.json ще "lmstudio" як default, нічого не вибрано — підхопимо LM Studio
            if (SP_ProviderLmStudio.IsChecked != true && SP_ProviderOllama.IsChecked != true
                && SP_ProviderOllamaCloud.IsChecked != true && SP_ProviderClaude.IsChecked != true)
            {
                SP_ProviderLmStudio.IsChecked = true;
            }

            _panelLastProvider = CurrentSelectedProvider();
            LoadLlmFieldsForProvider(_panelLastProvider, s);

            SP_TgEnabled.IsChecked  = s.TelegramEnabled;
            SP_TgToken.Text         = s.TelegramToken;
            SP_TgChatId.Text        = s.TelegramChatId > 0 ? s.TelegramChatId.ToString() : "";
            SP_TgUserEnabled.IsChecked = s.TgUserEnabled;
            SP_TgApiId.Text         = s.TgApiId > 0 ? s.TgApiId.ToString() : "";
            SP_TgApiHash.Text       = s.TgApiHash;
            SP_TgPhone.Text         = s.TgPhone;
            SP_TgDmOnly.IsChecked   = s.TgDmOnly;
            SP_Voice.IsChecked      = s.VoiceInputEnabled;
            SP_OpenAiKey.Text       = s.OpenAiApiKey;
            SP_VaultPath.Text       = s.VaultPath;
            SP_Spont.IsChecked      = s.SpontaneousEnabled;
            SP_SpontInterval.Text   = s.SpontaneousIntervalMins.ToString();
            SP_ProactiveLevel.Text  = s.ProactiveAutonomyLevel.ToString();
            SP_Tray.IsChecked       = s.MinimizeToTray;
            SP_AccentColor.Text     = s.MatrixColor;
            UpdateColorPreview(s.MatrixColor);

            // (LLM-поля вже завантажені через LoadLlmFieldsForProvider)

            // Показуємо overlay
            SettingsOverlay.Visibility = Visibility.Visible;

            // Slide-in анімація
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                480, 0,
                new System.Windows.Duration(TimeSpan.FromMilliseconds(280)))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            SettingsPanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.XProperty, anim);
        }

        private void Provider_Checked(object sender, RoutedEventArgs e)
        {
            if (_panelSettings == null) return; // викликано до OpenSettingsPanel — ігноруємо

            // 1) Зберегти поточний текст полів у in-memory snapshot для попереднього провайдера
            CommitLlmFieldsToSnapshot(_panelLastProvider);

            // 2) Підвантажити поля для нового
            var newProvider = CurrentSelectedProvider();
            LoadLlmFieldsForProvider(newProvider, _panelSettings);
            _panelLastProvider = newProvider;
        }

        private void LoadLlmFieldsForProvider(string provider, AppSettings s)
        {
            // За замовчуванням — пул ховаємо; покажемо тільки для ollama-cloud.
            SP_OllamaPoolGroup.Visibility = Visibility.Collapsed;
            SP_ModelLabel.Visibility = Visibility.Visible;
            SP_ModelBox.Visibility = Visibility.Visible;
            SP_ModelHint.Visibility = Visibility.Visible;
            StopPoolRefreshTimer();

            switch (provider)
            {
                case "ollama":
                    SP_UrlGroup.Visibility    = Visibility.Visible;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed;
                    SP_LmUrl.Text     = string.IsNullOrWhiteSpace(s.LmUrl) || s.LmUrl.Contains(":1234")
                                        ? "http://localhost:11434/v1/chat/completions" : s.LmUrl;
                    SP_UrlHint.Text   = "Локальна Ollama. Дефолт: http://localhost:11434/v1/chat/completions";
                    SP_ModelBox.Text  = string.IsNullOrWhiteSpace(s.Model) ? "llama3.2" : s.Model;
                    SP_ModelHint.Text = "Назва локальної моделі (напр. llama3.2, qwen2.5, mistral). Має бути запущена в `ollama serve`.";
                    SP_VisionModelBox.Text = s.VisionModel;
                    break;

                case "ollama-cloud":
                    SP_UrlGroup.Visibility    = Visibility.Collapsed;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed; // single-key UI більше не для Ollama Cloud
                    SP_OllamaPoolGroup.Visibility = Visibility.Visible;
                    SP_ModelLabel.Visibility = Visibility.Collapsed;
                    SP_ModelBox.Visibility = Visibility.Collapsed;
                    SP_ModelHint.Visibility = Visibility.Collapsed;

                    SP_OllamaMaxPerHour.Text = s.OllamaPoolMaxPerHour.ToString();
                    SP_OllamaRotateAt.Text   = ((int)Math.Round(s.OllamaPoolRotateAt * 100)).ToString();
                    SP_OllamaCooldown.Text   = s.OllamaPoolCooldownMins.ToString();

                    LoadOllamaKeysVM(s);
                    LoadAgentProfilesVM(s);

                    SP_ModelBox.Text = string.IsNullOrWhiteSpace(s.OllamaModel) ? AppSettings.DefaultOllamaCloudModel : s.OllamaModel;
                    SP_ModelHint.Text = "";
                    SP_VisionModelBox.Text = s.VisionModel;

                    StartPoolRefreshTimer();
                    break;

                case "claude":
                    SP_UrlGroup.Visibility    = Visibility.Collapsed;
                    SP_ApiKeyGroup.Visibility = Visibility.Visible;
                    SP_ApiKeyLabel.Text  = "Claude API Key";
                    SP_ApiKeyBox.Text    = s.ClaudeApiKey;
                    SP_ApiKeyHint.Text   = "Отримай ключ на https://console.anthropic.com/settings/keys.";
                    SP_ModelBox.Text     = string.IsNullOrWhiteSpace(s.ClaudeModel) ? "claude-sonnet-4-20250514" : s.ClaudeModel;
                    SP_ModelHint.Text    = "Напр. claude-sonnet-4-20250514, claude-opus-4-20250514, claude-3-5-sonnet-20241022.";
                    SP_VisionModelBox.Text = s.VisionModel;
                    HighlightApiKey();
                    break;

                default: // lmstudio
                    SP_UrlGroup.Visibility    = Visibility.Visible;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed;
                    SP_LmUrl.Text     = string.IsNullOrWhiteSpace(s.LmUrl) || s.LmUrl.Contains(":11434")
                                        ? "http://localhost:1234/v1/chat/completions" : s.LmUrl;
                    SP_UrlHint.Text   = "LM Studio. Дефолт: http://localhost:1234/v1/chat/completions";
                    SP_ModelBox.Text  = string.IsNullOrWhiteSpace(s.Model) ? "google/gemma-4-26b-a4b" : s.Model;
                    SP_ModelHint.Text = "Точна назва моделі з LM Studio (вкладка Local Server → Loaded Model).";
                    SP_VisionModelBox.Text = s.VisionModel;
                    break;
            }
        }

        // ---- Ollama Cloud key-pool helpers ----

        private void LoadOllamaKeysVM(AppSettings s)
        {
            _ollamaKeysVM.Clear();

            // Якщо у settings є ключі — підвантажуємо. Якщо порожньо але є legacy single — теж додаємо.
            if (s.OllamaKeys != null && s.OllamaKeys.Count > 0)
            {
                foreach (var k in s.OllamaKeys)
                    _ollamaKeysVM.Add(new OllamaKeyRowVM { Name = k.Name, Key = k.Key });
            }
            else if (!string.IsNullOrWhiteSpace(s.OllamaApiKey))
            {
                _ollamaKeysVM.Add(new OllamaKeyRowVM { Name = "Account 1", Key = s.OllamaApiKey });
            }

            RefreshPoolStatus();
        }

        private void LoadAgentProfilesVM(AppSettings s)
        {
            var chat = GetOrCreateAgentProfile(s, "chat", 0.85);
            var coder = GetOrCreateAgentProfile(s, "coder", 0.35);

            SP_AgentChatKey.Text = FormatAgentKeys(chat);
            SP_AgentChatModelBox.Text = string.IsNullOrWhiteSpace(chat.Model) ? AppSettings.DefaultOllamaCloudModel : chat.Model;
            SP_AgentChatTemp.Text = chat.Temperature?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "0.85";

            SP_AgentCoderKey.Text = FormatAgentKeys(coder);
            SP_AgentCoderModelBox.Text = string.IsNullOrWhiteSpace(coder.Model) ? "qwen3-coder:480b-cloud" : coder.Model;
            SP_AgentCoderTemp.Text = coder.Temperature?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "0.35";
        }

        private static string FormatAgentKeys(KokoAgentLlmProfile profile)
        {
            var keys = profile.OllamaKeys?
                .Where(k => !string.IsNullOrWhiteSpace(k.Key))
                .Select(k => k.Key.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();
            if (keys.Count == 0 && !string.IsNullOrWhiteSpace(profile.OllamaApiKey))
                keys.Add(profile.OllamaApiKey.Trim());
            return string.Join(Environment.NewLine, keys);
        }

        private static KokoAgentLlmProfile GetOrCreateAgentProfile(AppSettings settings, string agentId, double temperature)
        {
            settings.AgentLlmProfiles ??= new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase);
            if (!settings.AgentLlmProfiles.TryGetValue(agentId, out var profile) || profile == null)
            {
                profile = new KokoAgentLlmProfile
                {
                    AgentId = agentId,
                    Enabled = true,
                    Temperature = temperature
                };
                settings.AgentLlmProfiles[agentId] = profile;
            }

            if (string.IsNullOrWhiteSpace(profile.AgentId))
                profile.AgentId = agentId;
            if (!profile.Temperature.HasValue)
                profile.Temperature = temperature;
            return profile;
        }

        private void CommitAgentProfilesToSnapshot()
        {
            if (_panelSettings == null) return;
            SaveAgentProfile(_panelSettings, "chat", SP_AgentChatKey.Text, SP_AgentChatModelBox.Text, SP_AgentChatTemp.Text, 0.85);
            SaveAgentProfile(_panelSettings, "coder", SP_AgentCoderKey.Text, SP_AgentCoderModelBox.Text, SP_AgentCoderTemp.Text, 0.35);
        }

        private static void SaveAgentProfile(
            AppSettings settings,
            string agentId,
            string key,
            string model,
            string temperatureText,
            double fallbackTemperature)
        {
            var profile = GetOrCreateAgentProfile(settings, agentId, fallbackTemperature);
            profile.Enabled = true;
            profile.LlmProvider = "ollama-cloud";
            var keys = ParseAgentKeys(key);
            var existing = profile.OllamaKeys ?? new List<OllamaKeyEntry>();
            profile.OllamaKeys = keys.Select((k, i) =>
            {
                var prior = existing.FirstOrDefault(e => e.Key == k);
                return new OllamaKeyEntry
                {
                    Name = prior?.Name is { Length: > 0 } ? prior.Name : $"Key {i + 1}",
                    Key = k,
                    Enabled = true,
                    RecentRequests = prior?.RecentRequests ?? new List<DateTime>(),
                    CooldownUntil = prior?.CooldownUntil
                };
            }).ToList();
            profile.OllamaApiKey = profile.OllamaKeys.FirstOrDefault()?.Key ?? "";
            if (profile.OllamaKeys.Count > 0)
                profile.OllamaActiveKeyIndex = Math.Clamp(profile.OllamaActiveKeyIndex, 0, profile.OllamaKeys.Count - 1);
            profile.Model = model.Trim();
            if (double.TryParse(temperatureText.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temp))
                profile.Temperature = Math.Clamp(temp, 0.0, 2.0);
            else
                profile.Temperature = fallbackTemperature;
        }

        private static List<string> ParseAgentKeys(string raw)
            => (raw ?? "")
                .Split(new[] { "\r\n", "\n", "\r", ",", ";", " " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 8)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private void RefreshPoolStatus()
        {
            var pool = ServiceContainer.OllamaPool;
            // Snapshot bere stan з реального пулу — назви ключів зіставляємо за значенням Key.
            var snap = pool.Snapshot();
            for (int i = 0; i < _ollamaKeysVM.Count; i++)
            {
                var vm = _ollamaKeysVM[i];
                var status = snap.FirstOrDefault(s => Mask(vm.Key) == s.MaskedKey);

                if (string.IsNullOrWhiteSpace(vm.Key))
                {
                    vm.StatusText = "-";
                    vm.StatusBrush = System.Windows.Media.Brushes.Gray;
                    vm.ActiveDotBrush = System.Windows.Media.Brushes.Transparent;
                    continue;
                }

                if (status != null)
                {
                    if (status.OnCooldown && status.CooldownUntil.HasValue)
                    {
                        var mins = (int)Math.Ceiling((status.CooldownUntil.Value - DateTime.UtcNow).TotalMinutes);
                        vm.StatusText = $"cooldown {mins}m";
                        vm.StatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B));
                    }
                    else
                    {
                        vm.StatusText = $"{status.RequestsLastHour}/{status.Limit}";
                        vm.StatusBrush = status.UsagePct >= 0.9
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xC8, 0x4E))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9D, 0xB5, 0xA8));
                    }
                    vm.ActiveDotBrush = status.Active
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xFF, 0xAD))
                        : System.Windows.Media.Brushes.Transparent;
                }
                else
                {
                    // Ключ ще не в пулі (щойно додали в UI, ще не зберегли) — статус нейтральний
                    vm.StatusText = "новий";
                    vm.StatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x6B, 0x80));
                    vm.ActiveDotBrush = System.Windows.Media.Brushes.Transparent;
                }
            }
        }

        private static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "(empty)";
            if (key.Length <= 8) return new string('*', key.Length);
            return key[..4] + "..." + key[^4..];
        }

        private void StartPoolRefreshTimer()
        {
            StopPoolRefreshTimer();
            _poolRefreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _poolRefreshTimer.Tick += (_, _) => RefreshPoolStatus();
            _poolRefreshTimer.Start();
        }

        private void StopPoolRefreshTimer()
        {
            if (_poolRefreshTimer != null)
            {
                _poolRefreshTimer.Stop();
                _poolRefreshTimer = null;
            }
        }

        private void SP_AddKey_Click(object sender, RoutedEventArgs e)
        {
            _ollamaKeysVM.Add(new OllamaKeyRowVM
            {
                Name = $"Account {_ollamaKeysVM.Count + 1}",
                Key  = ""
            });
            RefreshPoolStatus();
        }

        private void SP_RemoveKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is OllamaKeyRowVM vm)
            {
                _ollamaKeysVM.Remove(vm);
                RefreshPoolStatus();
            }
        }

        // Зберігає поточні значення полів LLM у in-memory snapshot _panelSettings,
        // щоб переключення між провайдерами не губило незбережені правки
        private void CommitLlmFieldsToSnapshot(string provider)
        {
            if (_panelSettings == null) return;
            switch (provider)
            {
                case "ollama":
                case "lmstudio":
                    _panelSettings.LmUrl = SP_LmUrl.Text.Trim();
                    _panelSettings.Model = SP_ModelBox.Text.Trim();
                    break;
                case "ollama-cloud":
                    var coderModel = SP_AgentCoderModelBox.Text.Trim();
                    var chatModel = SP_AgentChatModelBox.Text.Trim();
                    _panelSettings.OllamaModel = !string.IsNullOrWhiteSpace(coderModel)
                        ? coderModel
                        : !string.IsNullOrWhiteSpace(chatModel)
                            ? chatModel
                            : AppSettings.DefaultOllamaCloudModel;
                    CommitAgentProfilesToSnapshot();
                    // Параметри пулу — з безпечним парсингом (залишаємо попереднє значення при кривому вводі)
                    if (int.TryParse(SP_OllamaMaxPerHour.Text.Trim(), out var maxH) && maxH > 0)
                        _panelSettings.OllamaPoolMaxPerHour = maxH;
                    if (int.TryParse(SP_OllamaRotateAt.Text.Trim(), out var rotPct) && rotPct > 0 && rotPct <= 100)
                        _panelSettings.OllamaPoolRotateAt = rotPct / 100.0;
                    if (int.TryParse(SP_OllamaCooldown.Text.Trim(), out var cdMin) && cdMin > 0)
                        _panelSettings.OllamaPoolCooldownMins = cdMin;

                    // Перебудовуємо OllamaKeys з ObservableCollection — фільтр: тільки рядки з непорожнім Key.
                    // Зберігаємо існуючі RecentRequests/CooldownUntil для ключів що залишились (за збігом Key).
                    var existing = _panelSettings.OllamaKeys ?? new System.Collections.Generic.List<OllamaKeyEntry>();
                    var rebuilt = new System.Collections.Generic.List<OllamaKeyEntry>();
                    foreach (var vm in _ollamaKeysVM)
                    {
                        var keyTrim = vm.Key?.Trim() ?? "";
                        if (string.IsNullOrEmpty(keyTrim)) continue;
                        var prior = existing.FirstOrDefault(k => k.Key == keyTrim);
                        rebuilt.Add(new OllamaKeyEntry
                        {
                            Name = string.IsNullOrWhiteSpace(vm.Name) ? $"Account {rebuilt.Count + 1}" : vm.Name.Trim(),
                            Key  = keyTrim,
                            Enabled = true,
                            RecentRequests = prior?.RecentRequests ?? new System.Collections.Generic.List<DateTime>(),
                            CooldownUntil  = prior?.CooldownUntil
                        });
                    }
                    _panelSettings.OllamaKeys = rebuilt;
                    // Legacy single — синхронізуємо з першим ключем для backwards-compat
                    _panelSettings.OllamaApiKey = rebuilt.Count > 0 ? rebuilt[0].Key : "";
                    break;
                case "claude":
                    _panelSettings.ClaudeApiKey = SP_ApiKeyBox.Text.Trim();
                    _panelSettings.ClaudeModel  = SP_ModelBox.Text.Trim();
                    break;
            }
            _panelSettings.VisionModel = SP_VisionModelBox.Text.Trim();
        }

        private void SP_ApiKey_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => HighlightApiKey();

        private void HighlightApiKey()
        {
            var hasKey = !string.IsNullOrWhiteSpace(SP_ApiKeyBox.Text);
            SP_ApiKeyBox.BorderBrush = hasKey
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x4E));
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            StopPoolRefreshTimer();

            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                0, 480,
                new System.Windows.Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            anim.Completed += (_, _) => SettingsOverlay.Visibility = Visibility.Collapsed;
            SettingsPanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.XProperty, anim);
        }

        private void SP_Save_Click(object sender, RoutedEventArgs e)
        {
            var s = AppSettings.Load();

            // LLM Provider — комітимо поточні поля у in-memory snapshot щоб не загубити
            // редагування поточного провайдера, потім переносимо ВСІ LLM-поля з snapshot у s.
            var provider = CurrentSelectedProvider();
            CommitLlmFieldsToSnapshot(provider);
            if (_panelSettings != null)
            {
                s.LmUrl        = _panelSettings.LmUrl;
                s.Model        = _panelSettings.Model;
                s.ClaudeApiKey = _panelSettings.ClaudeApiKey;
                s.ClaudeModel  = _panelSettings.ClaudeModel;
                s.OllamaApiKey = _panelSettings.OllamaApiKey;
                s.OllamaUrl    = _panelSettings.OllamaUrl;
                s.OllamaModel  = _panelSettings.OllamaModel;
                // Pool
                s.OllamaKeys             = _panelSettings.OllamaKeys ?? new System.Collections.Generic.List<OllamaKeyEntry>();
                s.OllamaPoolMaxPerHour   = _panelSettings.OllamaPoolMaxPerHour;
                s.OllamaPoolRotateAt     = _panelSettings.OllamaPoolRotateAt;
                s.OllamaPoolCooldownMins = _panelSettings.OllamaPoolCooldownMins;
                s.OllamaActiveKeyIndex   = _panelSettings.OllamaActiveKeyIndex;
                s.VisionModel            = _panelSettings.VisionModel;
                s.AgentLlmProfiles       = _panelSettings.AgentLlmProfiles ?? new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase);
            }
            s.LlmProvider = provider;

            s.TelegramEnabled    = SP_TgEnabled.IsChecked == true;
            s.TelegramToken      = SP_TgToken.Text.Trim();
            s.TgUserEnabled      = SP_TgUserEnabled.IsChecked == true;
            s.TgDmOnly           = SP_TgDmOnly.IsChecked == true;
            s.VoiceInputEnabled  = SP_Voice.IsChecked == true;
            s.OpenAiApiKey       = SP_OpenAiKey.Text.Trim();
            s.VaultPath          = SP_VaultPath.Text.Trim();
            s.SpontaneousEnabled = SP_Spont.IsChecked == true;
            if (int.TryParse(SP_SpontInterval.Text.Trim(), out var spontMins))
                s.SpontaneousIntervalMins = Math.Clamp(spontMins, 10, 240);
            if (int.TryParse(SP_ProactiveLevel.Text.Trim(), out var proactiveLevel))
                s.ProactiveAutonomyLevel = Math.Clamp(proactiveLevel, 0, 3);
            s.MinimizeToTray     = SP_Tray.IsChecked == true;
            s.MatrixColor        = string.IsNullOrWhiteSpace(SP_AccentColor.Text) ? "#00E676" : SP_AccentColor.Text.Trim();

            if (long.TryParse(SP_TgChatId.Text.Trim(), out var cid)) s.TelegramChatId = cid;

            // Не перезаписуємо якщо поле порожнє — захист від випадкового обнулення
            if (int.TryParse(SP_TgApiId.Text.Trim(), out var apiId) && apiId > 0)
                s.TgApiId = apiId;
            if (!string.IsNullOrEmpty(SP_TgApiHash.Text.Trim()))
                s.TgApiHash = SP_TgApiHash.Text.Trim();
            if (!string.IsNullOrEmpty(SP_TgPhone.Text.Trim()))
                s.TgPhone = SP_TgPhone.Text.Trim();

            s.Save();
            ThemeManager.ApplyTheme(s.MatrixColor);
            _llm.ReloadSettings();

            CloseSettings_Click(sender, e);
        }

        private void UpdateColorPreview(string hex)
        {
            try
            {
                var color = (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(hex);
                SP_ColorPreview.Background = new System.Windows.Media.SolidColorBrush(color);
            }
            catch { }
        }

        private void SP_ColorPreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            using var dlg = new System.Windows.Forms.ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                SP_AccentColor.Text = hex;
                UpdateColorPreview(hex);
            }
        }

        private void SP_Swatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border b && b.Tag is string hex)
            {
                SP_AccentColor.Text = hex;
                UpdateColorPreview(hex);
            }
        }

        // ------------------------------------------------------------
        // WINDOW CHROME
        // ------------------------------------------------------------

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => Close();

        protected override void OnClosed(EventArgs e)
        {
            try { ServiceContainer.BrainEngine?.RecordClose(); } catch { }
            try { _notifyIcon?.Dispose(); } catch { }
            try { ServiceContainer.Heart?.Dispose(); } catch { }
            base.OnClosed(e);
        }

        // ------------------------------------------------------------
        // CLEANUP
        // ------------------------------------------------------------

        private void Cleanup()
        {
            try
            {
                StopEcgAnimation();
                _liveCoreTimer?.Stop();
                _llmCts?.Cancel();
                _tgCts?.Cancel();
                _tgUserCts?.Cancel();
                ServiceContainer.Disposing();
            }
            catch { }
        }
    }

}
