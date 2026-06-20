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
        private bool _voiceDiagnosticsHooked;
        private static bool UseVoicePipelineV2 => true;

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
        private DispatcherTimer? _pulseLiveTimer;
        private DateTime _lastPulseUiRefresh = DateTime.MinValue;
        private bool _wearablePulseEventsHooked;
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

        private static readonly MediaColor UiMercury = MediaColor.FromRgb(0x68, 0xE6, 0xD6);
        private static readonly MediaColor UiPulse   = MediaColor.FromRgb(0xE2, 0x5A, 0x6A);
        private static readonly MediaColor UiInfo    = MediaColor.FromRgb(0x76, 0xB7, 0xE8);
        private static readonly MediaColor UiWarn    = MediaColor.FromRgb(0xD7, 0xB4, 0x6A);
        private static readonly MediaColor UiOk      = MediaColor.FromRgb(0x6F, 0xE3, 0xA1);
        private static readonly MediaColor UiMuted   = MediaColor.FromRgb(0x76, 0x84, 0x93);
        private static readonly MediaColor UiGrid    = MediaColor.FromRgb(0x50, 0x69, 0x84);

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
            catch (TaskCanceledException ex) { KokoSystemLog.Write("UI-CATCH", "HideKokonoeNoticeLaterAsync failed near source line 254: " + ex); }
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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "ShowBalloonNotification failed near source line 266: " + ex); }
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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "FlashTaskbar failed near source line 319: " + ex); }
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
                var somatic = brain.GetSomaticSnapshot();
                var selfReg = brain.GetSelfRegulationFrame(somatic);
                var telemetry = brain.BuildTelemetrySnapshot();
                var hasWearablePulse = TryGetVerifiedWearablePulse(out var wearableBpm, out var wearableBaseline);
                var uiState = ResolveCurrentRuntimeUiState();

                LiveCoreEmotionText.Text = $"емоція: {DashboardEmotionLabel(emotion.Current)}".ToUpper();
                LiveCoreEmotionText.Foreground = DashMakeBrush(emotion.Current);
                var attachment = emotion.Attachment;
                LiveCoreBondText.Text = $"довіра {attachment.Trust:F2} | прив'яз. {attachment.CompositeScore():F2} | настрій {state.MoodScore:F2}";

                LiveCoreBodyText.Text = $"{DashboardSomaticLabel(somatic.State).ToUpper()} | напруга {somatic.Strain:F2}";
                LiveCoreRegulationText.Text = $"{DashboardRegulationLabel(selfReg.Reaction)} -> {DashboardRegulationLabel(selfReg.Regulation)} | контроль {selfReg.Control:F2}";

                LiveCorePulseText.Text = hasWearablePulse
                    ? $"{wearableBpm:0} bpm | watch {wearableBpm - wearableBaseline:+0;-0;0}"
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
                    $"Kokonoe · {uiState.Primary.ToLowerInvariant()} · {uiState.Emotion.ToLowerInvariant()} · {uiState.Body.ToLowerInvariant()} {somatic.Strain:F2} · " +
                    $"{(hasWearablePulse ? $"watch {wearableBpm:0}" : "watch --")} · vault {state.PendingVaultExchangeCount}/5 · " +
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
                    LiveCoreCompactText.Text = "Kokonoe · offline · watch -- · vault -- · vision --";
                    LiveCoreAutonomyText.Text = "автономність недоступна";
                    LiveCorePresenceText.Text = "";
                    LiveCoreRhythmText.Text = "";
                    LiveCoreMemoryText.Text = "пам'ять недоступна";
                    LiveCoreVaultText.Text = ex.Message;
                    RepairVisibleTextTree(this);
                }
                catch (Exception logEx) { KokoSystemLog.Write("UI-CATCH", "UpdateLiveCorePanel failed near source line 447: " + logEx); }
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
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "QueueLiveCoreVaultScan failed near source line 477: " + ex); }
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
            var somatic = brain.GetSomaticSnapshot();
            var selfReg = brain.GetSelfRegulationFrame(somatic);
            var telemetry = brain.BuildTelemetrySnapshot();
            var hasWearablePulse = TryGetVerifiedWearablePulse(out var wearableBpm, out var wearableBaseline);
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
            sb.AppendLine(hasWearablePulse
                ? $"| Пульс | {wearableBpm:F0} bpm / wearable baseline {wearableBaseline:F0} / watch {wearableBpm - wearableBaseline:+0;-0;0} |"
                : "| Пульс | -- / wearable telemetry not verified |");
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

                SetLoadingProgress(35, "vault ui skipped...");

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
                StartPulseLiveMonitor();
                HookAgentTaskEvents();
                RefreshAgentTaskBoard();
                RefreshGenesisFabric();

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
            } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "StartMatrixRain failed near source line 890: " + ex); }

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
                try { recent = ServiceContainer.ChatRepository.GetMessages(40).OrderBy(x => x.Timestamp).TakeLast(30).ToList(); }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "GenerateStartupGreetingWithFallbackAsync failed near source line 992: " + ex); }

                Services.KokoInternalState? startupState = null;
                try { startupState = ServiceContainer.BrainEngine?.State; } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "GenerateStartupGreetingWithFallbackAsync failed near source line 995: " + ex); }
                var frame = service.BuildFrame(recent, now, startupState);

                var brainObs = "";
                var moodContext = "";
                var presenceContext = "";
                try
                {
                    var brain = ServiceContainer.BrainEngine;
                    var st = startupState ?? brain?.State;
                    if (st == null)
                        throw new InvalidOperationException("Brain state unavailable for startup greeting.");
                    moodContext =
                        $"brainMood={st.PersonalityDailyMood}; moodScore={st.MoodScore:F2}; irritation={st.PersonalityIrritation:F2}; " +
                        $"lastUserTone={st.LastUserEmotionalTone}; lastPresence={st.LastPresenceSummary}; situation={st.LastPresenceSituation}; tone={st.LastPresenceTone}; " +
                        $"emotionalMood={st.EmotionalSessionMood}; exit={st.EmotionalExitStyle}; manners={st.EmotionalMannersState}; grudge={st.EmotionalGrudgeScore:F2}";
                    try
                    {
                        var emotionalBlock = brain?.EmotionalMemory.BuildPromptBlock(st, recent, now);
                        if (!string.IsNullOrWhiteSpace(emotionalBlock))
                            moodContext += "\n" + emotionalBlock;
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "GenerateStartupGreetingWithFallbackAsync failed near source line 1017: " + ex); }
                    presenceContext = string.Join(" | ", st.PresenceTrace.TakeLast(4));
                    if (st.Observations.Any())
                        brainObs = $"Твоє останнє спостереження: {st.Observations.TakeLast(2).Last()}\n";
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "GenerateStartupGreetingWithFallbackAsync failed near source line 1022: " + ex); }

                try
                {
                    var emotion = ServiceContainer.EmotionEngine;
                    var stress = emotion.Stress.TotalLoad();
                    var emotionLine =
                        $"emotion={emotion.Current}; secondary={emotion.Secondary?.ToString() ?? "-"}; " +
                        $"bond={emotion.Bond}; connection={emotion.ConnectionScore:F2}; stress={stress:F2}; pad={emotion.CurrentPad}";
                    moodContext = string.IsNullOrWhiteSpace(moodContext) ? emotionLine : moodContext + "; " + emotionLine;
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "GenerateStartupGreetingWithFallbackAsync failed near source line 1033: " + ex); }

                service.EnrichFrame(frame, now, moodContext, presenceContext);
                var fallback = service.BuildFallback(frame);

                var prompt = $@"{frame.PromptBlock}
{brainObs}
Напиши стартову репліку повністю через модель. Вона має відчуватись як жива реакція Kokonoe на повернення користувача: врахуй час, паузу, mood, presence і останню тему.
Не пояснюй правила. Не пиши службовий статус. Тільки текст.";

                prompt = $@"{frame.PromptBlock}
{brainObs}
Напиши одну стартову репліку повністю через модель.
Вона має звучати як жива реакція Kokonoe на повернення користувача: врахуй час, довжину паузи, настрій, presence і останню реальну тему.
Не пояснюй правила. Не пиши службовий статус. Не використовуй фрази на кшталт ""Привіт. Контекст про останню задачу..."", ""продовжуємо чи міняємо ціль"", ""я тут якщо щось зламаєш"".
Не називай модулі, prompt, контекст, кеш, state або аналіз. Тільки природний текст.
1-2 речення українською, сухо, живо, без театру.";

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
                Services.KokoInternalState? startupState = null;
                try { startupState = ServiceContainer.BrainEngine?.State; } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "GenerateFastGreeting failed near source line 1068: " + ex); }
                return service.BuildFallback(service.BuildFrame(recent, DateTime.Now, startupState));
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

            RightPanel.Visibility = _activeTab == "Tools" ? Visibility.Visible : Visibility.Collapsed;
            UpdateAdaptiveShellLayout();
            if (RightPanel.Visibility == Visibility.Visible)
                RefreshRightOpsPanel();
        }

        private void BodyGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateAdaptiveShellLayout();

        private void UpdateAdaptiveShellLayout()
        {
            if (RightPanel == null || BodyGrid == null) return;

            var canShowRightPanel = _activeTab == "Tools";
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

        private void GenesisRefresh_Click(object sender, RoutedEventArgs e) => RefreshGenesisFabric();

        private void GenesisRegister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var role = GenesisRoleBox.SelectedItem as KokoAgentRoleDefinition;
                var roleId = role?.RoleId ?? "analyst";
                var provider = GenesisProviderBox.Text?.Trim();
                var model = GenesisModelBox.Text?.Trim();
                var agent = ServiceContainer.AgentFactory.CreateOrUpdateAgent(
                    GenesisAgentIdBox.Text,
                    roleId,
                    displayName: role?.DisplayName,
                    provider: string.IsNullOrWhiteSpace(provider) ? null : provider,
                    model: string.IsNullOrWhiteSpace(model) ? null : model);

                GenesisStatusText.Text = $"registered {agent.AgentId} as {agent.RoleId}";
                RefreshGenesisFabric(selectAgentId: agent.AgentId);
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "register failed: " + ex.Message;
            }
        }

        private void GenesisDisable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var agentId = GenesisAgentIdBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(agentId))
                {
                    GenesisStatusText.Text = "agent id is empty";
                    return;
                }

                var ok = ServiceContainer.AgentFactory.SetAgentEnabled(agentId, false);
                GenesisStatusText.Text = ok ? $"disabled {agentId}" : $"agent not found: {agentId}";
                RefreshGenesisFabric(selectAgentId: agentId);
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "disable failed: " + ex.Message;
            }
        }

        private void GenesisRunTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var objective = SandboxInput.Text.Trim();
                if (string.IsNullOrWhiteSpace(objective))
                {
                    GenesisStatusText.Text = "prompt workspace is empty";
                    return;
                }

                HookAgentTaskEvents();
                var agentId = GenesisAgentIdBox.Text.Trim();
                var role = (GenesisRoleBox.SelectedItem as KokoAgentRoleDefinition)?.RoleId ?? "analyst";
                var task = ServiceContainer.AgentTasks.AddTask($"[{agentId}/{role}] {objective}", priority: 7);
                ServiceContainer.Blackboard.Publish(agentId, "task", $"queued task {task.Id}: {objective}", 0.68,
                    new { task.Id, role, objective }, "queued");
                GenesisStatusText.Text = $"queued {task.Id}";
                RefreshAgentTaskBoard();
                RefreshGenesisFabric(selectAgentId: agentId);
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "queue failed: " + ex.Message;
            }
        }

        private void RefreshGenesisFabric(string? selectAgentId = null)
        {
            try
            {
                if (!ServiceContainer.IsInitialized)
                    return;

                var factory = ServiceContainer.AgentFactory;
                var roles = factory.Roles.ToList();
                if (GenesisRoleBox.Items.Count == 0)
                {
                    GenesisRoleBox.ItemsSource = roles;
                    GenesisRoleBox.SelectedItem = roles.FirstOrDefault(r => r.RoleId == "analyst") ?? roles.FirstOrDefault();
                }

                var snap = factory.GetSnapshot();
                GenesisConsoleText.Text = factory.RenderConsole();
                GenesisStatusText.Text = $"agents {snap.Agents.Count(a => a.Enabled)}/{snap.Agents.Count} | blackboard {snap.BlackboardRecent.Count}";

                var selected = !string.IsNullOrWhiteSpace(selectAgentId)
                    ? snap.Agents.FirstOrDefault(a => a.AgentId.Equals(selectAgentId.Trim(), StringComparison.OrdinalIgnoreCase))
                    : snap.Agents.FirstOrDefault(a => a.AgentId.Equals(GenesisAgentIdBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (selected != null)
                {
                    GenesisAgentIdBox.Text = selected.AgentId;
                    GenesisProviderBox.Text = selected.Provider;
                    GenesisModelBox.Text = selected.Model;
                    GenesisRoleBox.SelectedItem = roles.FirstOrDefault(r => r.RoleId.Equals(selected.RoleId, StringComparison.OrdinalIgnoreCase))
                        ?? GenesisRoleBox.SelectedItem;
                }
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "fabric offline: " + ex.Message;
            }
        }

        private bool HookAgentTaskEvents()
        {
            if (!ServiceContainer.IsInitialized)
                return false;

            if (!_agentTaskEventsHooked)
            {
                var agentTasks = ServiceContainer.AgentTasks;
                agentTasks.ActivityChanged += activity =>
                {
                    Dispatcher.InvokeAsync(() => UpdateAgentActivityPanel(activity), DispatcherPriority.Background);
                };
                agentTasks.TaskCompleted += (task, notice) =>
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
                            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HookAgentTaskEvents failed near source line 2603: " + ex); }
                            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("agent", task.Objective, visible));
                        }
                        RefreshAgentTaskBoard();
                    }, DispatcherPriority.Background);
                };
                _agentTaskEventsHooked = true;
            }

            if (!_agentRuntimeEventsHooked)
            {
                var agentRuntime = ServiceContainer.AgentRuntime;
                agentRuntime.ActivityChanged += activity =>
                {
                    Dispatcher.InvokeAsync(() => UpdateAgentActivityPanel(activity), DispatcherPriority.Background);
                };
                _agentRuntimeEventsHooked = true;
            }

            return true;
        }

        private void RefreshAgentTaskBoard()
        {
            try
            {
                if (!HookAgentTaskEvents())
                {
                    AgentTaskStatusText.Text = "agent services initializing";
                    return;
                }
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
            var workMode = "Unknown";
            try { workMode = ServiceContainer.BrainEngine.GetCurrentWorkModeLabel(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "UpdateAgentActivityPanel failed near source line 2653: " + ex); }
            ThoughtStreamStatusText.Text =
                $"mode {workMode} | agent {activity.Phase} | {activity.Tool} | {TrimLiveCoreLine(activity.Focus, 120)}";
            ThoughtStreamStatusText.ToolTip =
                $"{activity.UpdatedAt:HH:mm:ss} mode={workMode} {activity.Phase}/{activity.Tool}\n{activity.Focus}\n{activity.Thought}";
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

        private void RightGenesis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshGenesisFabric();
                McpOutput.Text = ServiceContainer.AgentFactory.RenderConsole();
            }
            catch (Exception ex) { McpOutput.Text = $"Genesis error: {ex.Message}"; }
        }

        private async void RightOverlord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                McpOutput.Text = "System Overlord scanning configured roots...";
                var snap = await ServiceContainer.SystemOverlord.ScanAsync(maxFiles: AppSettings.Load().SystemOverlordMaxFiles);
                McpOutput.Text = ServiceContainer.SystemOverlord.RenderConsole();
                if (snap.Proposals.Count == 0)
                    McpOutput.Text += "\n\nNo maintenance proposals. Either clean system, or insufficient evidence. Shocking restraint.";
            }
            catch (Exception ex) { McpOutput.Text = $"Overlord error: {ex.Message}"; }
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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "LoadVaultSidebar failed near source line 6048: " + ex); }
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
            catch (Exception ex)
            {
                KokoSystemLog.Write("VAULT-UI", "refresh notes failed: " + ex);
            }
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
                try { DashFooterComment.Text = $"Навіть діагностика зламалась. ({ex.Message})"; } catch (Exception logEx) { KokoSystemLog.Write("UI-CATCH", "DashLoadAll failed near source line 6523: " + logEx); }
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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashUpdateClock failed near source line 6560: " + ex); }
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

            DashStyleTab(DashTabNeuroBtn,  tab == "neuro",  UiInfo);
            DashStyleTab(DashTabDevBtn,    tab == "dev",    UiOk);
            DashStyleTab(DashTabMemoryBtn, tab == "memory", UiMercury);
            DashStyleTab(DashTabSystemBtn, tab == "system", UiWarn);

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
                    : new System.Windows.Media.SolidColorBrush(UiMuted);
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
                var brain = ServiceContainer.BrainEngine;
                var emotion = brain.Emotion;
                var cur = emotion.Current;
                var uiState = ResolveCurrentRuntimeUiState();

                DashCurrentMoodDisplay.Text = $"СТАН: {uiState.Primary}".ToUpperInvariant();
                DashCurrentMoodDisplay.Foreground = uiState.Kind == "stable"
                    ? DashMakeBrush(cur)
                    : new SolidColorBrush(UiWarn);

                var emotionSubtext = cur switch
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
                DashMoodSubtext.Text = uiState.Kind == "stable"
                    ? emotionSubtext
                    : $"{uiState.Emotion} · {uiState.Body} · {uiState.Detail}";

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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "DashLoadEmotionalHeader failed near source line 6791: " + ex); }
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
                    try { brain.InitVault(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "InitBrain failed near source line 8532: " + ex); }
                    Dispatcher.InvokeAsync(UpdateEmotionDot, DispatcherPriority.Background);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Brain init] {ex.Message}");
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
            try { ServiceContainer.BrainEngine?.RecordClose(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "OnClosed failed near source line 10473: " + ex); }
            try { _notifyIcon?.Dispose(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "OnClosed failed near source line 10474: " + ex); }
            try { ServiceContainer.Heart?.Dispose(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "OnClosed failed near source line 10475: " + ex); }
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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "Cleanup failed near source line 10494: " + ex); }
        }
    }

}
