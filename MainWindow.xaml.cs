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
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // VIEW MODELS
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // MAIN WINDOW
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public partial class MainWindow : Window
    {
        // в”Ђв”Ђ Services в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private LlmService         _llm = null!;
        private HealthService      _health = null!;
        private ObsidianMcpService _obsidian = null!;

        // в”Ђв”Ђ Chat в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private CancellationTokenSource _llmCts = new();
        private bool _isGenerating;
        private FrameworkElement? _thinkingElement;
        private TextBlock? _thinkingStatusText;

        // в”Ђв”Ђ Pending image в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private byte[]?  _imgBytes;
        private string   _imgMime = "image/jpeg";
        private BitmapImage? _imgThumb;
        private string? _pendingFileContext;

        private readonly List<MemoryCortexNodeVm> _memoryCortexNodes = new();
        private double _memoryCortexZoom = 1.0;

        // в”Ђв”Ђ Voice в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private bool _isRecording;

        // в”Ђв”Ђ Telegram Bot в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private TelegramBotClient? _tgBot;

        private CancellationTokenSource _tgCts = new();
        private readonly ObservableCollection<string> _tgMessages = new();

        // в”Ђв”Ђ Telegram UserClient (MTProto) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private CancellationTokenSource _tgUserCts = new();

        // в”Ђв”Ђ Vault в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private string? _currentNotePath;

        // в”Ђв”Ђ Tab в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private string _activeTab = "Chat";

        // в”Ђв”Ђ Dashboard в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

        // в”Ђв”Ђ Session chat log (auto-saved to Obsidian) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        // Created on first message, appended after every exchange.
        private string? _sessionChatPath;

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // INIT
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

        // в”Ђв”Ђ Fill work area (above taskbar, not WindowState=Maximized) в”Ђв”Ђ
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
        // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Closed += (s, ev) => Cleanup();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPaste));
            _ = StartupSequenceAsync();
            SetupHeartUI();
            StartUiTextRepairTimer();
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // HEART UI вЂ” pulsing dot + BPM, driven by KokoHeartEngine
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
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

                LiveCoreEmotionText.Text = $"РµРјРѕС†С–СЏ: {DashboardEmotionLabel(emotion.Current)}".ToUpper();
                LiveCoreEmotionText.Foreground = DashMakeBrush(emotion.Current);
                var attachment = emotion.Attachment;
                LiveCoreBondText.Text = $"РґРѕРІС–СЂР° {attachment.Trust:F2} | РїСЂРёРІ'СЏР·. {attachment.CompositeScore():F2} | РЅР°СЃС‚СЂС–Р№ {state.MoodScore:F2}";

                LiveCoreBodyText.Text = $"{DashboardSomaticLabel(somatic.State).ToUpper()} | РЅР°РїСЂСѓРіР° {somatic.Strain:F2}";
                LiveCoreRegulationText.Text = $"{DashboardRegulationLabel(selfReg.Reaction)} -> {DashboardRegulationLabel(selfReg.Regulation)} | РєРѕРЅС‚СЂРѕР»СЊ {selfReg.Control:F2}";

                LiveCorePulseText.Text = heart.CurrentBpm > 0
                    ? $"{heart.CurrentBpm:0} bpm | Р·РјС–РЅР° {heart.BpmDelta:+0;-0;0}"
                    : "-- bpm";
                LiveCoreStrainBar.Value = Math.Clamp(somatic.Strain * 100.0, 0, 100);

                LiveCoreAutonomyText.Text = TrimLiveCoreLine(telemetry.AutonomyDebug, 54);
                LiveCorePresenceText.Text = TrimLiveCoreLine($"{telemetry.Presence} | {telemetry.TimelineState} | state {telemetry.StateFreshness}", 76);
                LiveCoreRhythmText.Text = TrimLiveCoreLine($"{telemetry.Rhythm} | LLM {telemetry.LlmStatus} | guard {telemetry.PostReplyGuard}", 64);

                QueueLiveCoreVaultScan(forceVaultScan);

                LiveCoreMemoryText.Text = $"СЃРёРЅС…СЂРѕРЅС–Р·Р°С†С–СЏ {state.PendingVaultExchangeCount}/5 | РїР°Рј'СЏС‚СЊ {_liveCoreMemoryItems}";
                LiveCoreVaultText.Text = $"РѕРіР»СЏРґ {_liveCoreReviewActions} | Р·Р°РґР°С‡С– {_liveCoreOpenTasks}";
                if (state.LastAutoVaultSyncAt > DateTime.MinValue)
                    LiveCoreVaultText.Text += $" | sync {state.LastAutoVaultSyncAt:dd.MM HH:mm}";
                if (_lastObsidianPreflightAt > DateTime.MinValue)
                    LiveCoreVaultText.Text += $" | ctx {_lastObsidianPreflightAt:HH:mm:ss}";
                if (!string.IsNullOrWhiteSpace(telemetry.ScenarioHealth))
                    LiveCoreVaultText.Text += $" | {telemetry.ScenarioHealth}";
                LiveCoreCompactText.Text =
                    $"Kokonoe В· {DashboardEmotionLabel(emotion.Current).ToLowerInvariant()} В· {DashboardSomaticLabel(somatic.State).ToLowerInvariant()} {somatic.Strain:F2} В· " +
                    $"{(heart.CurrentBpm > 0 ? $"{heart.CurrentBpm:0} bpm" : "-- bpm")} В· vault {state.PendingVaultExchangeCount}/5 В· " +
                    $"vision {BuildVisionStatusLabel(state, DateTime.Now).ToLowerInvariant()}";
                if (RightPanel.Visibility == Visibility.Visible)
                    RefreshRightOpsPanel();
                RepairVisibleTextTree(this);
            }
            catch (Exception ex)
            {
                try
                {
                    LiveCoreEmotionText.Text = "РµРјРѕС†С–СЏ: РѕС„Р»Р°Р№РЅ";
                    LiveCoreBodyText.Text = "СЃРѕРјР°С‚РёРєР°: РѕС„Р»Р°Р№РЅ";
                    LiveCorePulseText.Text = "-- bpm";
                    LiveCoreCompactText.Text = "Kokonoe В· offline В· -- bpm В· vault -- В· vision --";
                    LiveCoreAutonomyText.Text = "Р°РІС‚РѕРЅРѕРјРЅС–СЃС‚СЊ РЅРµРґРѕСЃС‚СѓРїРЅР°";
                    LiveCorePresenceText.Text = "";
                    LiveCoreRhythmText.Text = "";
                    LiveCoreMemoryText.Text = "РїР°Рј'СЏС‚СЊ РЅРµРґРѕСЃС‚СѓРїРЅР°";
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

# Р–РёРІРµ СЏРґСЂРѕ

""";
                }

                _obsidian.WriteNote(path, existing.TrimEnd() + "\n\n" + report);
                LiveCoreVaultText.Text = $"Р·РЅС–РјРѕРє Р·Р±РµСЂРµР¶РµРЅРѕ | {DateTime.Now:HH:mm}";
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
            sb.AppendLine("| РЁР°СЂ | Р—РЅР°С‡РµРЅРЅСЏ |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| Р•РјРѕС†С–СЏ | {emotion.Current} / С–РЅС‚РµРЅСЃРёРІРЅС–СЃС‚СЊ {emotion.Data.Intensity:F2} / Р·РІ'СЏР·РѕРє {emotion.Bond} |");
            sb.AppendLine($"| РџСЂРёРІ'СЏР·Р°РЅС–СЃС‚СЊ | {telemetry.Attachment.Replace("|", "/")} |");
            if (emotion.Secondary.HasValue)
                sb.AppendLine($"| Р’С‚РѕСЂРёРЅРЅР° РµРјРѕС†С–СЏ | {emotion.Secondary.Value} / {emotion.SecondaryIntensity:F2} |");
            sb.AppendLine($"| РќР°СЃС‚СЂС–Р№ | {state.CurrentMood} / РѕС†С–РЅРєР° {state.MoodScore:F2} / Р±Р°Р·Р° {state.BaselineMood:F2} |");
            sb.AppendLine($"| РўС–Р»Рѕ | {somatic.State} / {somatic.Label} |");
            sb.AppendLine($"| РџСѓР»СЊСЃ | {heart.CurrentBpm:F0} bpm / Р±Р°Р·Р° {heart.BaselineBpm:F0} / Р·РјС–РЅР° {heart.BpmDelta:+0;-0;0} |");
            sb.AppendLine($"| РЎРѕРјР°С‚РёС‡РЅРµ РЅР°РІР°РЅС‚Р°Р¶РµРЅРЅСЏ | strain {somatic.Strain:F2} / calm {somatic.Calm:F2} / volatility {somatic.Volatility:F2} |");
            sb.AppendLine($"| РЎР°РјРѕСЂРµРіСѓР»СЏС†С–СЏ | {LiveCoreCodeLabel(selfReg.Reaction)} -> {LiveCoreCodeLabel(selfReg.Regulation)} / РєРѕРЅС‚СЂРѕР»СЊ {selfReg.Control:F2} / С–РјРїСѓР»СЊСЃ {selfReg.Drive:F2} |");
            sb.AppendLine($"| РђРІС‚РѕРЅРѕРјРЅС–СЃС‚СЊ | {telemetry.Autonomy.Replace("|", "/")} |");
            sb.AppendLine($"| Autonomy debug | {telemetry.AutonomyDebug.Replace("|", "/")} |");
            sb.AppendLine($"| Presence | {telemetry.Presence.Replace("|", "/")} |");
            sb.AppendLine($"| Timeline | {telemetry.Timeline.Replace("|", "/")} |");
            sb.AppendLine($"| State freshness | {telemetry.StateFreshness.Replace("|", "/")} |");
            sb.AppendLine($"| Р’РЅСѓС‚СЂС–С€РЅС–Р№ РґРµРЅСЊ | {telemetry.InternalDay.Replace("|", "/")} |");
            sb.AppendLine($"| Р РёС‚Рј | {telemetry.Rhythm.Replace("|", "/")} |");
            sb.AppendLine($"| Self-review | {telemetry.SelfReview.Replace("|", "/")} |");
            sb.AppendLine($"| Post-reply guard | {telemetry.PostReplyGuard.Replace("|", "/")} |");
            sb.AppendLine($"| LLM | {telemetry.LlmStatus.Replace("|", "/")} / {telemetry.LlmProvider} / {telemetry.LlmModel} |");
            if (!string.IsNullOrWhiteSpace(telemetry.LlmLastError))
                sb.AppendLine($"| LLM error | {telemetry.LlmLastError.Replace("|", "/")} |");
            sb.AppendLine($"| Core checks | {telemetry.ScenarioHealth.Replace("|", "/")} |");
            sb.AppendLine($"| РЎРёРЅС…СЂРѕРЅС–Р·Р°С†С–СЏ РїР°Рј'СЏС‚С– | РѕС‡С–РєСѓС” {state.PendingVaultExchangeCount}/5 / РѕСЃС‚Р°РЅРЅСЏ {(state.LastAutoVaultSyncAt > DateTime.MinValue ? state.LastAutoVaultSyncAt.ToString("yyyy-MM-dd HH:mm") : "РЅС–РєРѕР»Рё")} |");
            sb.AppendLine($"| Vault | РїР°Рј'СЏС‚СЊ {_liveCoreMemoryItems} / РѕРіР»СЏРґ {_liveCoreReviewActions} / Р·Р°РґР°С‡С– {_liveCoreOpenTasks} |");
            if (!string.IsNullOrWhiteSpace(selfReg.PrivateThought))
                sb.AppendLine($"| Р’РЅСѓС‚СЂС–С€РЅСЏ РґСѓРјРєР° | {selfReg.PrivateThought.Replace("|", "/")} |");
            if (!string.IsNullOrWhiteSpace(selfReg.BehaviorDirective))
                sb.AppendLine($"| РџРѕРІРµРґС–РЅРєР° | {selfReg.BehaviorDirective.Replace("|", "/")} |");

            return sb.ToString().TrimEnd();
        }

        private static string TrimLiveCoreLine(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "вЂ”";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        private static string LiveCoreCodeLabel(string code) => code switch
        {
            "protective_override" => "Р·Р°С…РёСЃРЅРµ РїРµСЂРµРІРёР·РЅР°С‡РµРЅРЅСЏ",
            "pulse_spike" => "СЃС‚СЂРёР±РѕРє РїСѓР»СЊСЃСѓ",
            "anger_contained" => "СЃС‚СЂРёРјР°РЅРµ СЂРѕР·РґСЂР°С‚СѓРІР°РЅРЅСЏ",
            "combat_focus" => "Р±РѕР№РѕРІРёР№ С„РѕРєСѓСЃ",
            "pressure_rise" => "Р·СЂРѕСЃС‚Р°РЅРЅСЏ С‚РёСЃРєСѓ",
            "low_power" => "РЅРёР·СЊРєРёР№ Р·Р°СЂСЏРґ",
            "recovered_calm" => "РїРѕРІРµСЂРЅРµРЅРЅСЏ СЃРїРѕРєРѕСЋ",
            "steady_calm" => "СЃС‚Р°Р±С–Р»СЊРЅРёР№ СЃРїРѕРєС–Р№",
            "stable_loop" => "СЃС‚Р°Р±С–Р»СЊРЅРёР№ С†РёРєР»",
            "clean_focus" => "С‡РёСЃС‚РёР№ С„РѕРєСѓСЃ",
            "unknown_body" => "РЅРµРІС–РґРѕРјРёР№ С‚С–Р»РµСЃРЅРёР№ СЃРёРіРЅР°Р»",
            "protect" => "Р·Р°С…РёСЃС‚",
            "clamp" => "Р·Р°С‚РёСЃРє",
            "contain" => "СЃС‚СЂРёРјСѓРІР°РЅРЅСЏ",
            "focus" => "С„РѕРєСѓСЃ",
            "compress" => "СЃС‚РёСЃРЅРµРЅРЅСЏ",
            "conserve" => "Р·Р±РµСЂРµР¶РµРЅРЅСЏ СЂРµСЃСѓСЂСЃСѓ",
            "release" => "РІС–РґРїСѓСЃРєР°РЅРЅСЏ",
            "baseline" => "Р±Р°Р·РѕРІРёР№ СЂРµР¶РёРј",
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
            if (currentBpm < 55)        state = "РіР»РёР±РѕРєРёР№ СЃРїРѕРєС–Р№ Р°Р±Рѕ СЃРѕРЅ";
            else if (currentBpm < 70)   state = "СЃРїРѕРєС–Р№";
            else if (currentBpm < 85)   state = "Р°РєС‚РёРІРЅР° СѓРІР°РіР°";
            else if (currentBpm < 100)  state = "Р·Р±СѓРґР¶РµРЅРЅСЏ / С–РЅС‚РµСЂРµСЃ";
            else if (currentBpm < 120)  state = "СЃС‚СЂРµСЃ Р°Р±Рѕ СЃРёР»СЊРЅР° РµРјРѕС†С–СЏ";
            else                        state = "С‚Р°С…С–РєР°СЂРґС–СЏ вЂ” С‰РѕСЃСЊ РЅРµ С‚Р°Рє";
            DashHeartStateText.Text = state;
                try
                {
                    var somatic = ServiceContainer.BrainEngine.GetSomaticSnapshot();
                    var selfReg = ServiceContainer.BrainEngine.GetSelfRegulationFrame(somatic);
                    DashHeartStateText.Text = $"{somatic.State.ToUpper()} // {selfReg.Reaction} В· {selfReg.Regulation} В· strain {somatic.Strain:P0}";
                }
                catch { }
        }

        private async Task StartupSequenceAsync()
        {
            try
            {
                StartMatrixRain();
                SetLoadingProgress(0, "С–РЅС–С†С–Р°Р»С–Р·Р°С†С–СЏ СЃРµСЂРІС–СЃС–РІ...");

                // 1 вЂ” init services (background thread)
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

                SetLoadingProgress(20, "Р·Р°РІР°РЅС‚Р°Р¶РµРЅРЅСЏ С‡Р°С‚Сѓ...");
                LoadChatHistory();

                SetLoadingProgress(35, "vault...");
                LoadVaultSidebar();

                SetLoadingProgress(50, "РєР°Р»РµРЅРґР°СЂ...");
                LoadCalendarTab();
                LoadToolsTab();

                SetLoadingProgress(65, "telegram...");
                InitTelegram();
                if (AppSettings.Load().TgUserEnabled)
                    _ = InitTelegramUserAsync();

                SetLoadingProgress(75, "РјРѕР·РѕРє...");
                InitBrain();
                StartLiveCoreMonitor();

                TgMessagesList.ItemsSource = _tgMessages;

                SetLoadingProgress(85, "РіРѕС‚РѕРІРѕ...");
                var greeting = GenerateFastGreeting();

                SetLoadingProgress(100, "РіРѕС‚РѕРІРѕ");
                await Task.Delay(150);

                // 3 вЂ” С…РѕРІР°С”РјРѕ overlay Р· Р°РЅС–РјР°С†С–С”СЋ
                await FadeOutLoadingAsync();

                // 4 вЂ” РґРѕРґР°С”РјРѕ РїСЂРёРІС–С‚Р°РЅРЅСЏ СЏРє РїРµСЂС€Рµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ С‡Р°С‚Сѓ
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // MATRIX RAIN
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private System.Windows.Threading.DispatcherTimer? _matrixTimer;
        private readonly Random _matrixRng = new();
        private readonly List<MatrixColumn> _matrixCols = new();
        private const string MatrixChars = "г‚ўг‚¤г‚¦г‚Ёг‚Єг‚«г‚­г‚Їг‚±г‚іг‚µг‚·г‚№г‚»г‚Ѕг‚їгѓЃгѓ„гѓ†гѓ€гѓЉгѓ‹гѓЊгѓЌгѓЋ0123456789ABCDEFв€‘в€†в€‡в€«в‰€в‰ в€ћ";

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

                // Р‘РµСЂРµРјРѕ РѕСЃС‚Р°РЅРЅС– РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ Р— timestamp
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
                        brainObs = $"РўРІРѕС” РѕСЃС‚Р°РЅРЅС” СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ: {st.Observations.TakeLast(2).Last()}\n";
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
РќР°РїРёС€Рё СЃС‚Р°СЂС‚РѕРІСѓ СЂРµРїР»С–РєСѓ РїРѕРІРЅС–СЃС‚СЋ С‡РµСЂРµР· РјРѕРґРµР»СЊ. Р’РѕРЅР° РјР°С” РІС–РґС‡СѓРІР°С‚РёСЃСЊ СЏРє Р¶РёРІР° СЂРµР°РєС†С–СЏ Kokonoe РЅР° РїРѕРІРµСЂРЅРµРЅРЅСЏ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: РІСЂР°С…СѓР№ С‡Р°СЃ, РїР°СѓР·Сѓ, mood, presence С– РѕСЃС‚Р°РЅРЅСЋ С‚РµРјСѓ.
РќРµ РїРѕСЏСЃРЅСЋР№ РїСЂР°РІРёР»Р°. РќРµ РїРёС€Рё СЃР»СѓР¶Р±РѕРІРёР№ СЃС‚Р°С‚СѓСЃ. РўС–Р»СЊРєРё С‚РµРєСЃС‚.";

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
                return "РЇ РЅР° РјС–СЃС†С–. Р”Р°РІР°Р№, РїРѕРєР°Р·СѓР№ С‰Рѕ СЃСЊРѕРіРѕРґРЅС– РґРѕР±РёРІР°С”РјРѕ.";
            }
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // TAB NAVIGATION
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // MEMORY TAB
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
                PulseTabBpmBig.Text   = cur > 0 ? $"{cur:0}" : "вЂ”";
                PulseTabCurText.Text  = cur > 0 ? $"{cur:0.0}" : "вЂ”";
                PulseTabBaseText.Text = $"{heart.BaselineBpm:0.0}";
                PulseSideBpmText.Text = cur > 0 ? $"{cur:0}" : "--";
                try
                {
                    var somatic = ServiceContainer.BrainEngine.GetSomaticSnapshot();
                    var selfReg = ServiceContainer.BrainEngine.GetSelfRegulationFrame(somatic);
                    PulseTabStateLabel.Text = $"{somatic.State.ToUpper()} В· {selfReg.Regulation} В· strain {somatic.Strain:P0}";
                    PulseSideStateText.Text = $"{somatic.State.ToLowerInvariant()} В· baseline {heart.BaselineBpm:0} В· strain {somatic.Strain:P0}";
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
                PulseMoodDetailText.Text = $"annoyed {annoyed:0}% В· curiosity {curiosity:0}% В· trust {trust:0}% В· sleep depr. {Math.Clamp((1.0 - state.MoodScore) * 100, 0, 100):0}%";
                PulseMoodBar.Value = Math.Clamp(state.MoodScore * 100, 0, 100);

                var status = currentBpm <= 0
                    ? "telemetry standby"
                    : currentBpm > 95
                        ? "core online В· memory synced В· telemetry elevated"
                        : "core online В· memory synced В· telemetry recording";
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

        // в”Ђв”Ђ ECG REAL-TIME ANIMATION в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

            // Map [-0.20, 1.0] в†’ [h*0.90, h*0.05]
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // SANDBOX TAB
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // MCP ENHANCED CATALOG
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
                    $"vault doctor {report.HealthScore}/100 В· empty {report.EmptyMarkdownFiles.Count} В· links {linkProblems} В· " +
                    $"fm {report.FrontmatterIssues.Count} В· moj {report.MojibakeSuspects.Count} В· miss {report.MissingWikiTargets.Count}";
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // CHAT вЂ” SEND MESSAGE
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

            // Ctrl+V Р· Р·РѕР±СЂР°Р¶РµРЅРЅСЏРј Сѓ Р±СѓС„РµСЂС– вЂ” РїРµСЂРµС…РѕРїР»СЋС”РјРѕ С‰РѕР± РЅРµ РІСЃС‚Р°РІР»СЏС‚Рё С‚РµРєСЃС‚
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

            // Slash РєРѕРјР°РЅРґРё РґР»СЏ РґС–Р°РіРЅРѕСЃС‚РёРєРё
            if (text.Equals("/tgtest", StringComparison.OrdinalIgnoreCase))
            {
                InputBox.Clear();
                AddMessageBubble(new ChatMessageVm { Role = "system", Content = "РўРµСЃС‚СѓС”РјРѕ TG..." });
                try
                {
                    if (_tgBot == null) { AddMessageBubble(new ChatMessageVm { Role = "system", Content = "вљ пёЏ _tgBot = null" }); return; }
                    var s2 = AppSettings.Load();
                    await _tgBot.SendMessage(s2.TelegramChatId, "рџ”§ TG С‚РµСЃС‚ РІС–Рґ Kokonoe");
                    AddMessageBubble(new ChatMessageVm { Role = "system", Content = "вњ… TG РїСЂР°С†СЋС”" });
                }
                catch (Exception ex) { AddMessageBubble(new ChatMessageVm { Role = "system", Content = $"вќЊ TG error: {ex.Message}" }); }
                return;
            }

            if (text.Equals("/brain", StringComparison.OrdinalIgnoreCase))
            {
                InputBox.Clear();
                AddMessageBubble(new ChatMessageVm { Role = "system", Content = "Р—Р°РїСѓСЃРєР°С”РјРѕ brain trigger..." });
                ServiceContainer.BrainEngine?.TriggerSpontaneous();
                return;
            }

            _isGenerating = true;
            SendBtn.IsEnabled = false;
            _llmCts = new CancellationTokenSource();

            // Add user bubble
            var baseText = string.IsNullOrWhiteSpace(text) && _imgBytes != null
                ? "Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–."
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
            AddThinkingBubble("РїСЂРёР№РЅСЏР»Р° РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ");
            ScrollToBottom();

            try
            {
                // Ensure UI updates (thinking bubble) before blocking operations
                await ShowKokoActivityAsync("РѕРЅРѕРІР»СЋСЋ СЃС‚Р°РЅ С– РЅР°РјС–СЂРё");

                // Brain state must observe the user message before context is built.
                // It can touch memory/pattern stores, so keep it off the UI thread.
                await Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(sendText); } catch { }
                }, _llmCts?.Token ?? default);

                if (LooksLikeManualScreenScan(sendText))
                {
                    await ShowKokoActivityAsync("СЂРѕР±Р»СЋ Р·РЅС–РјРѕРє РµРєСЂР°РЅР°");
                    await HandleManualScreenScanAsync(sendText, _llmCts?.Token ?? default);
                    return;
                }

                var obsidianCommand = await Task.Run(() =>
                {
                    var handled = TryHandleDirectObsidianCommand(sendText, out var replyText);
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

                if (TryHandleDirectControlCommand(sendText, out var controlReply))
                {
                    RemoveThinkingBubble();
                    var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
                    var replyTb = AddMessageBubble(replyVm);
                    if (replyTb != null)
                        await TypeIntoAsync(replyTb, controlReply, _llmCts?.Token ?? default);
                    else
                        replyVm.Content = controlReply;
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = controlReply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch { }
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
                var guardedReply = await GuardAndRepairReplyAsync(sendText, reply, await contextTask, _llmCts?.Token ?? default);
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

                // LLM-РІРёС‚СЏРі С„Р°РєС‚С–РІ вЂ” С‚С–Р»СЊРєРё РїС–СЃР»СЏ РІС–РґРїРѕРІС–РґС–, С‰РѕР± РЅРµ РєРѕРЅРєСѓСЂСѓРІР°С‚Рё Р·Р° GPU
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

        private bool TryHandleDirectControlCommand(string userText, out string reply)
        {
            reply = "";
            try
            {
                if (TryScheduleWakeOrReminder(userText, out reply))
                    return true;

                var brain = ServiceContainer.BrainEngine;
                if (brain != null && brain.TryApplyUserControlCommand(userText, out reply))
                    return true;

                if (TryBuildProfileIdentityReply(userText, out reply))
                    return true;
            }
            catch { }
            return false;
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
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var wantsScan = ContainsAny(lower,
                "РїСЂРѕСЃРєР°РЅСѓР№", "РїСЂРѕСЃРєР°РЅРёСЂСѓР№", "СЃРєР°РЅСѓР№", "СЃРєР°РЅРёСЂСѓР№",
                "РїРѕРґРёРІРёСЃСЊ", "РіР»СЏРЅСЊ", "РїСЂРѕРІС–СЂ", "РїРµСЂРµРІС–СЂ", "РїСЂРѕР°РЅР°Р»С–Р·СѓР№", "С‰Рѕ РЅР°");
            var targetScreen = ContainsAny(lower,
                "РµРєСЂР°РЅ", "СЃРєСЂС–РЅ", "СЃРєСЂРёРЅ", "screen", "РјРѕРЅС–С‚РѕСЂ", "СЂРѕР±РѕС‡РёР№ СЃС‚С–Р»");

            return wantsScan && targetScreen;
        }

        private async Task HandleManualScreenScanAsync(string userText, CancellationToken ct)
        {
            byte[] screenshot;
            try
            {
                screenshot = await Task.Run(() => ServiceContainer.PcControl.TakeScreenshot(), ct);
            }
            catch (Exception ex)
            {
                RemoveThinkingBubble();
                AddMessageBubble(new ChatMessageVm
                {
                    Role = "assistant",
                    Content = $"РќРµ Р·РјРѕРіР»Р° Р·РЅСЏС‚Рё РµРєСЂР°РЅ: {ex.Message}. РќР°СЂРµС€С‚С– РєРѕРјР°РЅРґР° Р±СѓР»Р° РЅРѕСЂРјР°Р»СЊРЅР°, Р° Windows РІРёСЂС–С€РёРІ Р·РѕР±СЂР°Р·РёС‚Рё РјРµР±Р»С–."
                });
                return;
            }

            var prompt = $"""
РўРё Kokonoe. РљРѕСЂРёСЃС‚СѓРІР°С‡ РїСЂСЏРјРѕ РїРѕРїСЂРѕСЃРёРІ РїСЂРѕСЃРєР°РЅСѓРІР°С‚Рё Р№РѕРіРѕ РїРѕС‚РѕС‡РЅРёР№ РµРєСЂР°РЅ.
Р—Р°РїРёС‚ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: {userText}

Р—Р°РІРґР°РЅРЅСЏ:
- РџРѕРґРёРІРёСЃСЊ РЅР° СЃРєСЂС–РЅС€РѕС‚ С– СЃРєР°Р¶Рё, С‰Рѕ СЂРµР°Р»СЊРЅРѕ РІРёРґРЅРѕ.
- РЇРєС‰Рѕ РІРёРґРЅРѕ С‡Р°С‚/РїСЂРѕРіСЂР°РјСѓ/РїРѕРјРёР»РєСѓ/РєРѕРґ/РїРѕСЂРѕР¶РЅС–Р№ СЃС‚Р°РЅ, РЅР°Р·РІРё С†Рµ РїСЂСЏРјРѕ.
- РќРµ РІРёРіР°РґСѓР№ РїСЂРёС…РѕРІР°РЅРёС… РјРѕС‚РёРІС–РІ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°.
- РќРµ РєР°Р¶Рё "РµРєСЂР°РЅ РїСЂРѕСЃРєР°РЅРѕРІР°РЅРёР№" СЏРє СЃР»СѓР¶Р±РѕРІРёР№ С€С‚Р°РјРї; РґР°Р№ РєРѕСЂРёСЃРЅРµ СЃРїРѕСЃС‚РµСЂРµР¶РµРЅРЅСЏ.
- РќРµ РїРµСЂРµРїРёСЃСѓР№ РїСЂРёРІР°С‚РЅС– С‚РѕРєРµРЅРё, РєР»СЋС‡С–, email, С‚РµР»РµС„РѕРЅРё Р°Р±Рѕ РґРѕРІРіС– РїСЂРёРІР°С‚РЅС– СЂСЏРґРєРё.
- РЈРєСЂР°С—РЅСЃСЊРєРѕСЋ, 2-5 СЂРµС‡РµРЅСЊ, СЃС‚РёР»СЊ Kokonoe: СЃСѓС…Рѕ, СЂРѕР·СѓРјРЅРѕ, Р±РµР· РєР°РЅС†РµР»СЏСЂРёС‚Сѓ.
""";

            var reply = await _llm.SendSystemVisionQueryAsync(prompt, screenshot, "image/jpeg", ct)
                        ?? "Р•РєСЂР°РЅ Р·РЅСЏР»Р°, Р°Р»Рµ vision РЅРµ РїРѕРІРµСЂРЅСѓРІ РЅРѕСЂРјР°Р»СЊРЅРѕРіРѕ Р°РЅР°Р»С–Р·Сѓ. РўРѕР±С‚Рѕ РєРѕРјР°РЅРґР° Р±СѓР»Р° СЂРѕР·СѓРјРЅР°, Р° РїСЂРѕРІР°Р№РґРµСЂ Р·РЅРѕРІСѓ РїСЂРёРєРёРЅСѓРІСЃСЏ С†РµРіР»РѕСЋ.";

            RemoveThinkingBubble();
            var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
            var replyTb = AddMessageBubble(replyVm);
            if (replyTb != null)
                await TypeIntoAsync(replyTb, reply, ct);
            else
                replyVm.Content = reply;

            try
            {
                ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content = reply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now
                });
            }
            catch { }

            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", userText, reply));
        }

        private string BuildContext(string? userText = null)
        {
            // РЈР’РђР“Рђ: С†РµР№ РєРѕРЅС‚РµРєСЃС‚ РґРѕРґР°С”С‚СЊСЃСЏ РґРѕ ~6k С‚РѕРєРµРЅС–РІ system prompt + TOOLS.
            // РўСЂРёРјР°С”РјРѕ Р№РѕРіРѕ РєРѕРјРїР°РєС‚РЅРёРј вЂ” РјР°РєСЃРёРјСѓРј ~800 С‚РѕРєРµРЅС–РІ (~3200 СЃРёРјРІРѕР»С–РІ).
            // GetToolsDescription() РџР РР‘Р РђРќРћ вЂ” РѕРїРёСЃ С‚СѓР»Р·С–РІ РІР¶Рµ С” РІ TOOLS РјР°СЃРёРІС– LlmService.

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

            // 1. Р РµР»РµРІР°РЅС‚РЅР° РїР°Рј'СЏС‚СЊ вЂ” РќРђР™Р’РђР–Р›РР’Р†РЁР•, Р±Рѕ С†Рµ С„Р°РєС‚Рё РїСЂРѕ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°
            try
            {
                var mem = ServiceContainer.BrainEngine?.Memory;
                if (mem != null && !string.IsNullOrWhiteSpace(userText))
                {
                    var (facts, episodes) = mem.FindRelevant(userText, maxFacts: 5, maxEpisodes: 2);
                    var memParts = new List<string>();
                    foreach (var f in facts)
                        memParts.Add(f.Content);
                    foreach (var e in episodes)
                        memParts.Add($"[{e.When:dd.MM.yy}] {e.Summary}");
                    if (memParts.Count > 0)
                        parts.Add(("=== РџРђРњ'РЇРўР¬ ===\n" + string.Join("\n", memParts), 1));
                }
            }
            catch { }

            // 1.5 Р•РјРѕС†С–Р№РЅРёР№ СЃС‚Р°РЅ вЂ” Р±РµР·РїРѕСЃРµСЂРµРґРЅСЊРѕ РІРїР»РёРІР°С” РЅР° С‚РѕРЅ РІС–РґРїРѕРІС–РґС–
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

            // 2. РЎС‚Р°РЅ (РµРјРѕС†С–С—, Р·РґРѕСЂРѕРІ'СЏ, scheduler) вЂ” РІР°Р¶Р»РёРІРѕ Р°Р»Рµ РјРµРЅС€Рµ
            try
            {
                var stateCtx = ServiceContainer.StateEngine.GetStateAsContext();
                if (!string.IsNullOrEmpty(stateCtx))
                    parts.Add((stateCtx, 3));
            }
            catch { }

            // 2.1 РџСѓР»СЊСЃ вЂ” Р·Р°РІР¶РґРё РІ РєРѕРЅС‚РµРєСЃС‚С– С‰РѕР± Kokonoe СЂРµР°РіСѓРІР°Р»Р° РЅР° Р·РјС–РЅРё
            try
            {
                var heart = ServiceContainer.Heart;
                var bpm = heart.CurrentBpm;
                var baseline = heart.BaselineBpm;
                var diff = bpm - baseline;
                var bpmNote = diff > 15 ? " в†‘ РїС–РґРІРёС‰РµРЅРёР№" : diff < -10 ? " в†“ РЅРёР¶С‡Рµ Р±Р°Р·РѕРІРѕРіРѕ" : "";
                parts.Add(($"=== РџРЈР›Р¬РЎ ===\nРџРѕС‚РѕС‡РЅРёР№: {bpm:0.0} bpm{bpmNote} | Р‘Р°Р·РѕРІРёР№: {baseline:0.0} bpm", 2));
            }
            catch { }

            // 2.5 РљР°Р»РµРЅРґР°СЂ вЂ” РЅР°Р№Р±Р»РёР¶С‡С– РІР°Р¶Р»РёРІС– РґР°С‚Рё
            try
            {
                var cal = ServiceContainer.Calendar;
                var upcoming = cal.GetUpcoming(14); // РЅР°СЃС‚СѓРїРЅС– 2 С‚РёР¶РЅС–
                var today = cal.GetForDay(DateTime.Today);
                if (today.Any() || upcoming.Any())
                {
                    var calLines = new List<string>();
                    if (today.Any())
                        calLines.Add("РЎСЊРѕРіРѕРґРЅС–: " + string.Join(", ", today.Select(e => e.Title)));
                    var soon = upcoming.Where(e => e.EventAt.Date > DateTime.Today).Take(3);
                    if (soon.Any())
                        calLines.Add("РќР°Р№Р±Р»РёР¶С‡Рµ: " + string.Join("; ", soon.Select(e => $"{e.Title} {e.EventAt:dd.MM}")));
                    parts.Add(("=== РљРђР›Р•РќР”РђР  ===\n" + string.Join("\n", calLines), 2));
                }
            }
            catch { }

            // 3. Р—РґРѕСЂРѕРІ'СЏ вЂ” Р°РіСЂРµРіРѕРІР°РЅС– РїРѕРєР°Р·РЅРёРєРё Р·Р° С‚РёР¶РґРµРЅСЊ
            try
            {
                var healthCtx = ServiceContainer.HealthService.GetHealthContext();
                if (!string.IsNullOrWhiteSpace(healthCtx) && !healthCtx.StartsWith("No health data"))
                    parts.Add((healthCtx.Trim(), 4));
            }
            catch { }

            // 3.1 Р¦С–Р»С– С‚Р° Р°РєС‚РёРІРЅС– Р·РІРёС‡РєРё
            try
            {
                var goals = ServiceContainer.GoalService.GetActiveGoals();
                var habits = ServiceContainer.HabitService.GetActiveHabits();
                if (goals.Count > 0 || habits.Count > 0)
                {
                    var ghLines = new System.Collections.Generic.List<string>();
                    if (goals.Count > 0)
                        ghLines.Add("Р¦С–Р»С–: " + string.Join(", ", goals.Take(4).Select(g => $"{g.Title} ({g.Progress:0}%)")));
                    if (habits.Count > 0)
                        ghLines.Add("Р—РІРёС‡РєРё: " + string.Join(", ", habits.Take(5).Select(h => h.Name)));
                    parts.Add(("=== Р¦Р†Р›Р†/Р—Р’РР§РљР ===\n" + string.Join("\n", ghLines), 5));
                }
            }
            catch { }

            // 3.2 РљРѕРіРЅС–С‚РёРІРЅРёР№ РєРѕРЅС‚РµРєСЃС‚
            try
            {
                var cogCtx = ServiceContainer.BrainEngine?.Cognition?.BuildCognitionContext();
                if (!string.IsNullOrEmpty(cogCtx))
                    parts.Add((cogCtx, 5));
            }
            catch { }

            // 3.3 EnhancedMemory вЂ” СЃС‚СЂСѓРєС‚СѓСЂРѕРІР°РЅС– С„Р°РєС‚Рё РїСЂРѕ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°
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

            // 4. Vault вЂ” РєР»СЋС‡РѕРІС– РЅРѕС‚Р°С‚РєРё
            try
            {
                var allNotes = _obsidian.ListNotes();
                var vaultLines = new List<string>();

                var keyNotes = allNotes
                    .Where(n => n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("brain-core", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Р”РѕСЃСЊС”", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Facts", StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .ToList();
                if (keyNotes.Count > 0)
                    vaultLines.Add("РљР»СЋС‡РѕРІС–: " + string.Join(", ", keyNotes));

                var todayNote = $"Daily/{DateTime.Now:yyyy-MM-dd}.md";
                if (allNotes.Any(n => n == todayNote))
                    vaultLines.Add("Daily: С”");

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

            // 5. РџСЂРѕРіРЅРѕР· (ML.NET) вЂ” РЅР°СЃС‚СЂС–Р№/РµРЅРµСЂРіС–СЏ РЅР° Р·Р°РІС‚СЂР°
            try
            {
                var forecast = ServiceContainer.Predictor.GetForecastContext();
                if (!string.IsNullOrEmpty(forecast))
                    parts.Add((forecast.Trim(), 8));
            }
            catch { }

            // РЎРѕСЂС‚СѓС”РјРѕ Р·Р° РїСЂС–РѕСЂРёС‚РµС‚РѕРј С– Р·Р±РёСЂР°С”РјРѕ СЂРµР·СѓР»СЊС‚Р°С‚
            var orderedParts = parts.OrderBy(p => p.priority).Select(p => p.content).ToList();
            var result = string.Join("\n\n", orderedParts);

            // Р РѕР·СѓРјРЅРµ РѕР±СЂС–Р·Р°РЅРЅСЏ: РІРёРґР°Р»СЏС”РјРѕ РѕСЃС‚Р°РЅРЅС– СЃРµРєС†С–С— РґРѕРєРё РІРјС–С‰Р°С”РјРѕСЃСЊ
            while (result.Length > MAX_CONTEXT_LENGTH && orderedParts.Count > 1)
            {
                orderedParts.RemoveAt(orderedParts.Count - 1);
                result = string.Join("\n\n", orderedParts);
            }

            // РЇРєС‰Рѕ Р№ РѕРґРЅР° СЃРµРєС†С–СЏ Р·Р°РІРµР»РёРєР° вЂ” РѕР±СЂС–Р·Р°С”РјРѕ РЅР° РјРµР¶С– СЃР»РѕРІР°
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
                ? $"РћСЃС‚Р°РЅРЅС–Р№ РІС…С–Рґ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: В«{trimmed[..Math.Min(220, trimmed.Length)]}В»."
                : "РћСЃС‚Р°РЅРЅС–Р№ РІС…С–Рґ РєРѕСЂРёСЃС‚СѓРІР°С‡Р° РїРѕСЂРѕР¶РЅС–Р№ Р°Р±Рѕ СЃР»СѓР¶Р±РѕРІРёР№.";

            return $"""
LIVE RESPONSE STYLE
{concrete}
РџСЂР°РІРёР»Р° Р¶РёРІРѕС— РІС–РґРїРѕРІС–РґС–:
- СЃРїРµСЂС€Сѓ СЂРµР°РіСѓР№ РЅР° РєРѕРЅРєСЂРµС‚РёРєСѓ РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ, РЅРµ РЅР° Р°Р±СЃС‚СЂР°РєС‚РЅРёР№ РЅР°СЃС‚СЂС–Р№;
- РЅРµ РїРѕС‡РёРЅР°Р№ Р· РґРµРєРѕСЂР°С‚РёРІРЅРѕС— СЂРµРјР°СЂРєРё РІ *Р·С–СЂРѕС‡РєР°С…*, СЏРєС‰Рѕ РєРѕСЂРёСЃС‚СѓРІР°С‡ СЃР°Рј РЅРµ РїРѕС‡Р°РІ roleplay;
- РЅРµ РІРёРіР°РґСѓР№ В«РјРѕРЅС–С‚РѕСЂ Р±Р»РёРјР°С”В», В«РґР°С‚С‡РёРєРёВ», В«Р»Р°Р±РѕСЂР°С‚РѕСЂС–СЋВ», В«С‚С–Р»Рѕ СЂРµР°РіСѓС”В» Р±РµР· РїСЂСЏРјРѕС— РїСЂРёС‡РёРЅРё;
- РЅРµ РІРёРіР°РґСѓР№ Р·РѕРІРЅС–С€РЅС– С„Р°РєС‚Рё РїСЂРѕ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: Р°РєР°СѓРЅС‚Рё, YouTube/Twitch/Discord, РјРµРјР±РµСЂСЃС‚РІР°, РїС–РґРїРёСЃРєРё, СЂРѕР±РѕС‚Сѓ, РїРѕРєСѓРїРєРё Р°Р±Рѕ Р»СЋРґРµР№, СЏРєС‰Рѕ С†СЊРѕРіРѕ РЅРµРјР° РІ С‡Р°С‚С–;
- РЅРµ РїСЃРёС…РѕР»РѕРіС–Р·СѓР№: РЅРµ РІРёРіР°РґСѓР№, С‰Рѕ РєРѕСЂРёСЃС‚СѓРІР°С‡ Р±РѕС—С‚СЊСЃСЏ СЃРєР°Р·Р°С‚Рё, С‰Рѕ РІ РЅСЊРѕРіРѕ С‰РѕСЃСЊ Р·Р°СЃС‚СЂСЏРіР»Рѕ РІ РіРѕР»РѕРІС–, Р°Р±Рѕ С‰Рѕ С‚Рё В«РґРёРІРёС€СЃСЏ С‡РµСЂРµР· РµРєСЂР°РЅВ»;
- СЏРєС‰Рѕ РїРёС‚Р°СЋС‚СЊ РїСЂРѕ С‚РµР±Рµ/РљРѕРєРѕРЅРѕРµ вЂ” РґР°Р№ РїСЂСЏРјРёР№ РѕРїРёСЃ С…Р°СЂР°РєС‚РµСЂСѓ С‡Рё РїРѕР·РёС†С–С—, Р±РµР· С‚РµСЂР°РїРµРІС‚РёС‡РЅРёС… РїРёС‚Р°РЅСЊ Сѓ РІС–РґРїРѕРІС–РґСЊ;
- СЏРєС‰Рѕ РїРёС‚Р°СЋС‚СЊ "С‰Рѕ С‚Рё Р·РЅР°С”С€ РїСЂРѕ РјРµРЅРµ" Р°Р±Рѕ РїСЂРѕ РїР°Рј'СЏС‚СЊ/РїСЂРѕС„С–Р»СЊ вЂ” РІС–РґРїРѕРІС–РґР°Р№ СЃРїРёСЃРєРѕРј/РєРѕСЂРѕС‚РєРёРј РѕРіР»СЏРґРѕРј РІС–РґРѕРјРёС… С„Р°РєС‚С–РІ Р· РєРѕРЅС‚РµРєСЃС‚Сѓ; РЅРµ РїС–РґРјС–РЅСЏР№ С†Рµ СЃС‚Р°СЂРёРј sleep-РЅР°РјС–pРѕРј;
- РґРѕРїСѓСЃРєР°С”С‚СЊСЃСЏ СЃСѓС…Р° С–СЂРѕРЅС–СЏ, Р°Р»Рµ РІРѕРЅР° РјР°С” Р±СѓС‚Рё РїСЂРёРІ'СЏР·Р°РЅР° РґРѕ РїРѕРґС–С—, С‡Р°СЃСѓ, РЅР°РјС–СЂСѓ Р°Р±Рѕ РїРёС‚Р°РЅРЅСЏ;
- РєСЂР°С‰Рµ РѕРґРЅР° С‚РѕС‡РЅР° С„СЂР°Р·Р°, РЅС–Р¶ С‚РµР°С‚СЂР°Р»СЊРЅР° СЃС†РµРЅР° Р· С‚СЂСЊРѕРјР° С€Р°СЂР°РјРё РґРµРєРѕСЂР°С†С–Р№.
""";
        }

        private static string BuildTemporalAwarenessContext(string? userText)
        {
            var now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine("=== Р§РђРЎРћР’РР™ РљРћРќРўР•РљРЎРў вЂ” РљР РРўРР§РќРћ ===");
            sb.AppendLine($"РџРѕС‚РѕС‡РЅРёР№ Р»РѕРєР°Р»СЊРЅРёР№ С‡Р°СЃ: {now:yyyy-MM-dd HH:mm}.");

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
                    sb.AppendLine($"РџРѕРїРµСЂРµРґРЅС” РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: {lastUserBeforeCurrent.Timestamp:yyyy-MM-dd HH:mm} ({FormatGapUa(gap)} С‚РѕРјСѓ): \"{TruncateAtWordBoundary(SanitizeForLlm(lastUserBeforeCurrent.Content ?? ""), 180)}\"");
                }

                if (currentUser != null)
                    sb.AppendLine($"РџРѕС‚РѕС‡РЅРµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: {currentUser.Timestamp:yyyy-MM-dd HH:mm}: \"{TruncateAtWordBoundary(SanitizeForLlm(currentUser.Content ?? userText ?? ""), 180)}\"");
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
                        var due = now >= intent.FollowUpAt ? "follow-up СѓР¶Рµ РґРѕСЂРµС‡РЅРёР№" : $"follow-up С‡РµСЂРµР· {FormatGapUa(intent.FollowUpAt - now)}";
                        sb.AppendLine($"РђРєС‚РёРІРЅРёР№ РєРѕСЂРѕС‚РєРѕСЃС‚СЂРѕРєРѕРІРёР№ РЅР°РјС–СЂ: {intent.Summary}; СЃРєР°Р·Р°РЅРѕ {FormatGapUa(age)} С‚РѕРјСѓ; РѕС‡С–РєСѓРІР°РЅРѕ РґРѕ {intent.ExpectedUntil:HH:mm}; {due}; РґР¶РµСЂРµР»Рѕ: \"{TruncateAtWordBoundary(SanitizeForLlm(intent.SourceText), 160)}\"");
                    }
                }
            }
            catch { }

            var lower = (userText ?? "").ToLowerInvariant();
            var isMorningNow = now.Hour is >= 5 and < 13;
            var saysWakeOrMorning = ContainsAny(lower, "СЂР°РЅРєСѓ", "РґРѕР±СЂРёР№ СЂР°РЅРѕРє", "РґРѕР±СЂРѕРіРѕ СЂР°РЅРєСѓ", "РїСЂРѕСЃРЅСѓРІ", "РїСЂРѕРєРёРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ");
            var saysGoingSleep = ContainsAny(lower, "СЃРїР°С‚СЊ РїС–РґСѓ", "СЃРїР°С‚Рё РїС–РґСѓ", "Р№РґСѓ СЃРїР°С‚Рё", "С–РґСѓ СЃРїР°С‚Рё", "СЃРїРѕРєС–Р№РЅРѕС—", "РґРѕ СЂР°РЅРєСѓ");

            if (isMorningNow && saysWakeOrMorning)
            {
                sb.AppendLine("Р’РРЎРќРћР’РћРљ: Р·Р°СЂР°Р· СЂР°РЅРѕРє С– РєРѕСЂРёСЃС‚СѓРІР°С‡ СѓР¶Рµ РїСЂРѕРєРёРЅСѓРІСЃСЏ/РїРёС€Рµ РїС–СЃР»СЏ СЃРЅСѓ.");
                sb.AppendLine("Р—РђР‘РћР РћРќРђ: РЅРµ РєР°Р¶Рё Р№РѕРјСѓ \"СЃРїРё\", \"Р№РґРё СЃРїР°С‚Рё\", \"РґРѕ СЂР°РЅРєСѓ\" Р°Р±Рѕ РїРѕРґС–Р±РЅРµ. Р¦Рµ Р·Р°СЃС‚Р°СЂС–Р»РёР№ РєРѕРЅС‚РµРєСЃС‚ Р· РјРёРЅСѓР»РѕС— РЅРѕС‡С–.");
                sb.AppendLine("РџСЂР°РІРёР»СЊРЅР° СЂРµР°РєС†С–СЏ: РІРёР·РЅР°Р№ СЂР°РЅРѕРє/РїСЂРѕР±СѓРґР¶РµРЅРЅСЏ, РјРѕР¶РµС€ СЃР°СЂРєР°СЃС‚РёС‡РЅРѕ РїСЂРѕРєРѕРјРµРЅС‚СѓРІР°С‚Рё, Р°Р»Рµ РЅРµ РІС–РґРїСЂР°РІР»СЏР№ Р№РѕРіРѕ РЅР°Р·Р°Рґ СЃРїР°С‚Рё.");
            }
            else if (saysGoingSleep && (now.Hour is >= 21 or < 5))
            {
                sb.AppendLine("Р’РРЎРќРћР’РћРљ: РєРѕСЂРёСЃС‚СѓРІР°С‡ РїСЂСЏРјРѕ РєР°Р¶Рµ, С‰Рѕ Р№РґРµ СЃРїР°С‚Рё РІ РЅС–С‡РЅРёР№ С‡Р°СЃ. РљРѕСЂРѕС‚РєРµ РїРѕР±Р°Р¶Р°РЅРЅСЏ СЃРЅСѓ РґРѕСЂРµС‡РЅРµ.");
            }

            sb.AppendLine("РџСЂР°РІРёР»Рѕ: Р·Р°РІР¶РґРё Р·РІС–СЂСЏР№ РїРѕС‚РѕС‡РЅРёР№ С‡Р°СЃ С– С‡Р°СЃРѕРІРёР№ СЂРѕР·СЂРёРІ РјС–Р¶ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏРјРё РїРµСЂРµРґ РїРѕСЂР°РґРѕСЋ РїСЂРѕ СЃРѕРЅ.");
            sb.AppendLine("РџСЂР°РІРёР»Рѕ: СЏРєС‰Рѕ С” Р°РєС‚РёРІРЅРёР№ РєРѕСЂРѕС‚РєРѕСЃС‚СЂРѕРєРѕРІРёР№ РЅР°РјС–СЂ, РІРёРєРѕСЂРёСЃС‚РѕРІСѓР№ Р№РѕРіРѕ РґР»СЏ РїСЂРёСЂРѕРґРЅРѕРіРѕ follow-up: РєСѓСЂСЃРё РІР¶Рµ Р·Р°РєС–РЅС‡РёР»РёСЃСЊ, СЂРѕР±РѕС‚Р° С‰Рµ С‚СЂРёРІР°С”, РїСЂРѕРіСѓР»СЏРЅРєР° Р·Р°РІРµСЂС€РёР»Р°СЃСЊ С‚РѕС‰Рѕ.");
            return sb.ToString();
        }

        private static string FormatGapUa(TimeSpan gap)
        {
            if (gap.TotalMinutes < 1) return "С‰РѕР№РЅРѕ";
            if (gap.TotalHours < 1) return $"{(int)gap.TotalMinutes} С…РІ";
            if (gap.TotalDays < 1) return $"{(int)gap.TotalHours} РіРѕРґ {(int)gap.Minutes} С…РІ";
            return $"{(int)gap.TotalDays} РґРЅ {(int)gap.Hours} РіРѕРґ";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private bool TryHandleDirectObsidianCommand(string text, out string reply)
        {
            reply = "";
            if (string.IsNullOrWhiteSpace(text) || _obsidian == null)
                return false;

            var lower = text.ToLowerInvariant();
            var looksObsidian =
                ContainsAny(lower, "obsidian", "vault", "РїР°РїРє", "РЅРѕС‚Р°С‚Рє", "Р¶СѓСЂРЅР°Р»", "С‰РѕРґРµРЅРЅРёРє", "journal", "spanish", "lesson_", "lesson", "СѓСЂРѕРє");
            var wantsMutation =
                ContainsAny(lower, "СЃС‚РІРѕСЂ", "СЃРѕР·Рґ", "create", "Р·СЂРѕР±Рё", "Р·Р°РїРёС€", "Р·Р±РµСЂРµР¶", "РЅРµРјР°", "РЅРµРјР°С”");
            var wantsCheck =
                ContainsAny(lower, "РїРµСЂРµРІС–СЂ", "РїСЂРѕРІС–СЂ", "check", "С–СЃРЅСѓС”", "Р±Р°С‡РёС€");

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
                        report.Add($"{(verified ? "С”" : "РЅРµРјР°С”")} РЅРѕС‚Р°С‚РєР° `{target}`");
                    }
                    else
                    {
                        if (wantsMutation)
                            _obsidian.CreateFolder(target);

                        var folderFull = Path.Combine(_obsidian.VaultPath, target.Replace('/', Path.DirectorySeparatorChar));
                        var verified = Directory.Exists(folderFull);
                        report.Add($"{(verified ? "С”" : "РЅРµРјР°С”")} РїР°РїРєР° `{target}`");
                    }
                }

                reply = "РџРµСЂРµРІС–СЂРёР»Р° С‡РµСЂРµР· С„Р°Р№Р»РѕРІСѓ СЃРёСЃС‚РµРјСѓ, РЅРµ С‡РµСЂРµР· СѓСЏРІСѓ. " + string.Join("; ", report) + ".";
                return true;
            }
            catch (Exception ex)
            {
                reply = $"Obsidian-РѕРїРµСЂР°С†С–СЏ РІРїР°Р»Р°: {ex.Message}. РћС†Рµ РІР¶Рµ СЂРµР°Р»СЊРЅР° РїРѕРјРёР»РєР°, Р° РЅРµ С‚РµР°С‚СЂ РїСЂРѕ В«СЏ СЃС‚РІРѕСЂРёР»Р°В».";
                return true;
            }
        }

        private static List<string> InferObsidianTargets(string text)
        {
            var targets = new List<string>();
            var cleaned = text
                .Replace("в†’", "/", StringComparison.Ordinal)
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
                 (ContainsAny(lower, "РґСЂСѓРіРёР№ СѓСЂРѕРє", "РґСЂСѓРіРѕРіРѕ СѓСЂРѕРє", "2 СѓСЂРѕРє", "СѓСЂРѕРє 2", "lesson 2") && ContainsAny(lower, "РЅРµРјР°", "РЅРµРјР°С”", "СЃС‚РІРѕСЂ", "Р·СЂРѕР±Рё", "РїРµСЂРµРІС–СЂ", "РїСЂРѕРІС–СЂ"))) &&
                !targets.Contains("Journal/Spanish/Lesson_2_Social_Interaction.md", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish/Lesson_2_Social_Interaction.md");

            if ((lower.Contains("lesson_3", StringComparison.OrdinalIgnoreCase) ||
                 (ContainsAny(lower, "С‚СЂРµС‚С–Р№ СѓСЂРѕРє", "С‚СЂРµС‚СЊРѕРіРѕ СѓСЂРѕРє", "3 СѓСЂРѕРє", "СѓСЂРѕРє 3", "lesson 3") && ContainsAny(lower, "spanish", "С–СЃРїР°РЅ", "РґС–Р°Р»РѕРі", "dialog", "b1", "СЃС‚РІРѕСЂ", "Р·СЂРѕР±Рё")) ||
                 (ContainsAny(lower, "b1", "Р¶РёРІРёР№ РґС–Р°Р»РѕРі", "Р¶РёРІРёР№ РґРёР°Р»РѕРі", "live dialogue") && ContainsAny(lower, "СѓСЂРѕРє", "lesson", "spanish", "С–СЃРїР°РЅ"))) &&
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

- me despierto вЂ” СЏ РїСЂРѕРєРёРґР°СЋСЃСЏ
- me levanto вЂ” СЏ РІСЃС‚Р°СЋ
- desayuno вЂ” СЏ СЃРЅС–РґР°СЋ
- trabajo / estudio вЂ” СЏ РїСЂР°С†СЋСЋ / РЅР°РІС‡Р°СЋСЃСЏ
- almuerzo вЂ” СЏ РѕР±С–РґР°СЋ
- ceno вЂ” СЏ РІРµС‡РµСЂСЏСЋ
- me ducho вЂ” СЏ РїСЂРёР№РјР°СЋ РґСѓС€
- me acuesto вЂ” СЏ Р»СЏРіР°СЋ СЃРїР°С‚Рё

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

# РЈСЂРѕРє 2. РЎРѕС†С–Р°Р»СЊРЅР° РІР·Р°С”РјРѕРґС–СЏ

## РћСЃРЅРѕРІРЅС– С„СЂР°Р·Рё

- Hola, ВїquГ© tal? вЂ” РџСЂРёРІС–С‚, СЏРє СЃРїСЂР°РІРё?
- ВїCГіmo estГЎs? вЂ” РЇРє С‚Рё?
- Estoy bien, gracias. вЂ” РЈ РјРµРЅРµ РІСЃРµ РґРѕР±СЂРµ, РґСЏРєСѓСЋ.
- MГЎs o menos. вЂ” Р‘С–Р»СЊС€-РјРµРЅС€.
- Encantado / Encantada. вЂ” РџСЂРёС”РјРЅРѕ РїРѕР·РЅР°Р№РѕРјРёС‚РёСЃСЊ.
- ВїDe dГіnde eres? вЂ” Р—РІС–РґРєРё С‚Рё?
- Soy de Ucrania. вЂ” РЇ Р· РЈРєСЂР°С—РЅРё.
- ВїQuГ© haces? вЂ” Р©Рѕ С‚Рё СЂРѕР±РёС€? / Р§РёРј Р·Р°Р№РјР°С”С€СЃСЏ?
- Estoy aprendiendo espaГ±ol. вЂ” РЇ РІС‡Сѓ С–СЃРїР°РЅСЃСЊРєСѓ.
- Nos vemos. вЂ” РџРѕР±Р°С‡РёРјРѕСЃСЊ.

## РњС–РЅС–РґС–Р°Р»РѕРі

- A: Hola, ВїquГ© tal?
- B: Bien, gracias. ВїY tГє?
- A: MГЎs o menos, pero vivo.

## РќРѕС‚Р°С‚РєРё

- `ВїQuГ© tal?` вЂ” СЂРѕР·РјРѕРІРЅР° Р№ РґСѓР¶Рµ РїРѕС€РёСЂРµРЅР° С„СЂР°Р·Р° РґР»СЏ "СЏРє СЃРїСЂР°РІРё?".
- `Nos vemos` вЂ” РїСЂРёСЂРѕРґРЅРµ РЅРµС„РѕСЂРјР°Р»СЊРЅРµ РїСЂРѕС‰Р°РЅРЅСЏ.
""";
            }

            if (path.Equals("Journal/Spanish/Lesson_3_B1_Live_Dialogue.md", StringComparison.OrdinalIgnoreCase))
            {
                return """
---
tags: [spanish, journal, lesson, b1, dialogue]
---

# РЈСЂРѕРє 3. Р–РёРІРёР№ РґС–Р°Р»РѕРі СЂС–РІРЅСЏ B1

## РњРµС‚Р°

РќР°РІС‡РёС‚РёСЃСЊ РІРµСЃС‚Рё РїСЂРёСЂРѕРґРЅРёР№ РєРѕСЂРѕС‚РєРёР№ РґС–Р°Р»РѕРі: СЂРµР°РіСѓРІР°С‚Рё, СѓС‚РѕС‡РЅСЋРІР°С‚Рё, РЅРµ РІС–РґРїРѕРІС–РґР°С‚Рё РѕРґРЅРёРј СЃСѓС…РёРј СЃР»РѕРІРѕРј С– РЅРµ Р·РІСѓС‡Р°С‚Рё СЏРє РїРµСЂРµРєР»Р°РґР°С‡ С–Р· 2007 СЂРѕРєСѓ.

## РЎРёС‚СѓР°С†С–СЏ

РўРё Р·РЅР°Р№РѕРјРёС€СЃСЏ Р· Р»СЋРґРёРЅРѕСЋ РІ РєР°РІ'СЏСЂРЅС– РїС–СЃР»СЏ РјРѕРІРЅРѕРіРѕ РєР»СѓР±Сѓ. Р РѕР·РјРѕРІР° РїСЂРѕСЃС‚Р°, Р°Р»Рµ РІР¶Рµ РЅРµ Р·РѕРІСЃС–Рј A1: С” СѓС‚РѕС‡РЅРµРЅРЅСЏ, СЂРµР°РєС†С–С—, РјР°Р»РµРЅСЊРєС– РґРµС‚Р°Р»С– Р№ РїСЂРёСЂРѕРґРЅС– РїРµСЂРµС…РѕРґРё.

## Р”С–Р°Р»РѕРі

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

## РџРµСЂРµРєР»Р°Рґ

- A: РџСЂРёРІС–С‚, С‚Рё РЅРѕРІРµРЅСЊРєРёР№ Сѓ РєР»СѓР±С–?
- B: РўР°Рє, СЏ С‚СѓС‚ СѓРїРµСЂС€Рµ. РЇРєС‰Рѕ С‡РµСЃРЅРѕ, СЏ С‚СЂРѕС…Рё РЅРµСЂРІСѓСЋ.
- A: РќРµ С…РІРёР»СЋР№СЃСЏ. РЈСЃС– С‚Р°Рє РїРѕС‡РёРЅР°Р»Рё. Р—РІС–РґРєРё С‚Рё?
- B: РЇ Р· РЈРєСЂР°С—РЅРё. Р–РёРІСѓ С‚СѓС‚ РЅРµРґР°РІРЅРѕ.
- A: Рћ, С†С–РєР°РІРѕ. Рђ С‡РѕРјСѓ С‚Рё РІС‡РёС€ С–СЃРїР°РЅСЃСЊРєСѓ?
- B: Р‘Рѕ РјРµРЅС– РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ, СЏРє РІРѕРЅР° Р·РІСѓС‡РёС‚СЊ, С– СЏ С…РѕС‡Сѓ РіРѕРІРѕСЂРёС‚Рё Р· Р±С–Р»СЊС€РѕСЋ РєС–Р»СЊРєС–СЃС‚СЋ Р»СЋРґРµР№ Р±РµР· РїРµСЂРµРєР»Р°РґР°С‡Р°.
- A: РҐРѕСЂРѕС€Р° РїСЂРёС‡РёРЅР°. РўРѕР±С– СЃРєР»Р°РґРЅРѕ?
- B: Р†РЅРѕРґС– С‚Р°Рє. РЇ РґРѕСЃРёС‚СЊ Р±Р°РіР°С‚Рѕ СЂРѕР·СѓРјС–СЋ, Р°Р»Рµ РєРѕР»Рё С‚СЂРµР±Р° РіРѕРІРѕСЂРёС‚Рё, РјРѕР·РѕРє РІРёРјРёРєР°С”С‚СЊСЃСЏ.
- A: Р¦Рµ РЅРѕСЂРјР°Р»СЊРЅРѕ. Р“РѕР»РѕРІРЅРµ вЂ” РїСЂРѕРґРѕРІР¶СѓРІР°С‚Рё РіРѕРІРѕСЂРёС‚Рё, РЅР°РІС–С‚СЊ СЏРєС‰Рѕ СЂРѕР±РёС€ РїРѕРјРёР»РєРё.
- B: РўР°Рє, РјР°Р±СѓС‚СЊ. РњРµРЅС– С‚СЂРµР±Р° Р±С–Р»СЊС€Рµ РїСЂР°РєС‚РёРєСѓРІР°С‚Рё СЂРµР°Р»СЊРЅС– СЂРѕР·РјРѕРІРё.
- A: РўРѕРґС– РјРѕР¶РµРјРѕ РїРѕС‚СЂРµРЅСѓРІР°С‚РёСЃСЊ Р·Р°СЂР°Р·. Р©Рѕ С‚Рё Р·Р°Р·РІРёС‡Р°Р№ СЂРѕР±РёС€ СѓРІРµС‡РµСЂС–?
- B: Р—Р°Р·РІРёС‡Р°Р№ РІС‡СѓСЃСЏ, С‚СЂРѕС…Рё РіСЂР°СЋ Р°Р±Рѕ РїСЂР°С†СЋСЋ РЅР°Рґ СЃРІРѕС—РјРё РїСЂРѕС”РєС‚Р°РјРё.
- A: Р—РІСѓС‡РёС‚СЊ РґРѕР±СЂРµ. РћС‚Р¶Рµ, Сѓ С‚РµР±Рµ РІР¶Рµ С” С‚РµРјРё РґР»СЏ РїСЂР°РєС‚РёРєРё.
- B: Р§СѓРґРѕРІРѕ. РђР»Рµ РіРѕРІРѕСЂРё РїРѕРІС–Р»СЊРЅРѕ, Р°Р±Рѕ СЏ СЂРѕР±РёС‚РёРјСѓ РІРёРіР»СЏРґ, С‰Рѕ РІСЃРµ СЂРѕР·СѓРјС–СЋ.

## РљРѕСЂРёСЃРЅС– С„СЂР°Р·Рё

- `Es mi primera vez aquГ­.` вЂ” РЇ С‚СѓС‚ СѓРїРµСЂС€Рµ.
- `Estoy un poco nervioso.` вЂ” РЇ С‚СЂРѕС…Рё РЅРµСЂРІСѓСЋ.
- `No te preocupes.` вЂ” РќРµ С…РІРёР»СЋР№СЃСЏ.
- `ВїPor quГ© estГЎs aprendiendo espaГ±ol?` вЂ” Р§РѕРјСѓ С‚Рё РІС‡РёС€ С–СЃРїР°РЅСЃСЊРєСѓ?
- `Me gusta cГіmo suena.` вЂ” РњРµРЅС– РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ, СЏРє РІРѕРЅР° Р·РІСѓС‡РёС‚СЊ.
- `Depender del traductor.` вЂ” Р—Р°Р»РµР¶Р°С‚Рё РІС–Рґ РїРµСЂРµРєР»Р°РґР°С‡Р°.
- `Mi cerebro se apaga.` вЂ” РњС–Р№ РјРѕР·РѕРє РІРёРјРёРєР°С”С‚СЊСЃСЏ.
- `Aunque cometas errores.` вЂ” РќР°РІС–С‚СЊ СЏРєС‰Рѕ СЂРѕР±РёС€ РїРѕРјРёР»РєРё.
- `Habla despacio.` вЂ” Р“РѕРІРѕСЂРё РїРѕРІС–Р»СЊРЅРѕ.
- `Voy a fingir que entiendo todo.` вЂ” РЇ СЂРѕР±РёС‚РёРјСѓ РІРёРіР»СЏРґ, С‰Рѕ РІСЃРµ СЂРѕР·СѓРјС–СЋ.

## Р“СЂР°РјР°С‚РёРєР° Р· РґС–Р°Р»РѕРіСѓ

- `Estoy aprendiendo` вЂ” С‚РµРїРµСЂС–С€РЅС–Р№ С‚СЂРёРІР°Р»РёР№ С‡Р°СЃ: "СЏ Р·Р°СЂР°Р· РІС‡Сѓ".
- `desde hace poco` вЂ” "Р· РЅРµРґР°РІРЅСЊРѕРіРѕ С‡Р°СЃСѓ", "РЅРµРґР°РІРЅРѕ".
- `aunque + subjuntivo` Сѓ `aunque cometas errores` вЂ” "РЅР°РІС–С‚СЊ СЏРєС‰Рѕ С‚Рё СЂРѕР±РёС€/СЂРѕР±РёС‚РёРјРµС€ РїРѕРјРёР»РєРё".
- `voy a + infinitivo` вЂ” РЅР°Р№Р±Р»РёР¶С‡РёР№ РјР°Р№Р±СѓС‚РЅС–Р№ РЅР°РјС–СЂ: `voy a fingir` = "СЏ Р·Р±РёСЂР°СЋСЃСЏ РІРґР°РІР°С‚Рё".

## РџСЂР°РєС‚РёРєР°

1. Р’С–РґРїРѕРІС–РґР°Р№ С–СЃРїР°РЅСЃСЊРєРѕСЋ: С‡РѕРјСѓ С‚Рё РІС‡РёС€ С–СЃРїР°РЅСЃСЊРєСѓ?
2. РЎРєР»Р°РґРё 3 СЂРµС‡РµРЅРЅСЏ РїСЂРѕ С‚Рµ, С‰Рѕ С‚Рё СЂРѕР±РёС€ СѓРІРµС‡РµСЂС–.
3. РџРµСЂРµРїРёС€Рё РІС–РґРїРѕРІС–РґСЊ `Mi cerebro se apaga`, Р°Р»Рµ Р±С–Р»СЊС€ СЃРµСЂР№РѕР·РЅРѕ.
4. Р—Р°РјС–РЅРё РІ РґС–Р°Р»РѕР·С– С‚РµРјСѓ "РјРѕРІРЅРёР№ РєР»СѓР±" РЅР° "РѕРЅР»Р°Р№РЅ-РєСѓСЂСЃ".
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
                              ContainsAny(userLower, "СЂР°РЅРєСѓ", "РґРѕР±СЂРёР№ СЂР°РЅРѕРє", "РґРѕР±СЂРѕРіРѕ СЂР°РЅРєСѓ", "РїСЂРѕСЃРЅСѓРІ", "РїСЂРѕРєРёРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ");
            var wronglySendsToSleep = ContainsAny(replyLower, "СЃРїРё", "Р№РґРё СЃРїР°С‚Рё", "С–РґРё СЃРїР°С‚Рё", "РґРѕ СЂР°РЅРєСѓ", "Р»СЏРіР°Р№");

            if (morningWake && wronglySendsToSleep)
                return "Р Р°РЅРѕРє СѓР¶Рµ РЅР°СЃС‚Р°РІ, С‚Р°Рє С‰Рѕ РєРѕРјР°РЅРґСѓ \"СЃРїРё\" Р·РЅС–РјР°СЋ. РќР°СЂРµС€С‚С– РїСЂРѕРєРёРЅСѓРІСЃСЏ вЂ” РѕСЂРіР°РЅС–Р·Рј Р·СЂРѕР±РёРІ С‰РѕСЃСЊ РєРѕСЂРёСЃРЅРµ Р±РµР· РјРѕС”С— СѓС‡Р°СЃС‚С–.";

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
                    if (TryBuildProfileIdentityReply(userText, out var identityReply))
                        return (Reply: (string?)identityReply, Guard: (KokoPostReplyGuardResult?)null, HasBrain: true);

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
                if (!string.IsNullOrWhiteSpace(guard.HardReplacement))
                    return guard.HardReplacement!;
                if (!guard.ShouldRepair || string.IsNullOrWhiteSpace(guard.RepairInstruction))
                    return GuardTemporalReply(userText, reply);

                var repairPrompt = guard.RepairInstruction +
                                   "\n\nР”РѕРґР°С‚РєРѕРІРёР№ РєРѕРЅС‚РµРєСЃС‚:\n" +
                                   TrimForPrompt(context, 2600);
                var repaired = await Task.Run(
                    () => _llm.SendSystemQueryAsync(repairPrompt, ct: guardCt),
                    guardCt);
                if (string.IsNullOrWhiteSpace(repaired))
                    return GuardTemporalReply(userText, reply);

                var secondGuard = await Task.Run(() =>
                    ServiceContainer.BrainEngine?.EvaluatePostReplyGuard(userText, repaired) ?? new KokoPostReplyGuardResult(),
                    guardCt);
                if (!secondGuard.Passed && !string.IsNullOrWhiteSpace(secondGuard.HardReplacement))
                    return secondGuard.HardReplacement!;

                return secondGuard.Passed ? repaired.Trim() : GuardTemporalReply(userText, reply);
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

        private bool TryBuildProfileIdentityReply(string userText, out string reply)
        {
            reply = "";
            if (!LooksLikeProfileQuestion(userText))
                return false;

            try
            {
                if (_obsidian == null) return false;
                var profile = _obsidian.ReadNote("Creator/Profile.md");
                if (string.IsNullOrWhiteSpace(profile)) return false;

                var name = MatchProfileValue(profile, @"\*\*Р†Рј'?СЏ:\*\*\s*(.+)");
                if (string.IsNullOrWhiteSpace(name))
                    name = MatchProfileValue(profile, @"\*\*Ім'?я:\*\*\s*(.+)");
                var age = MatchProfileValue(profile, @"\*\*Р’С–Рє:\*\*\s*(.+)");
                if (string.IsNullOrWhiteSpace(age))
                    age = MatchProfileValue(profile, @"\*\*Вік:\*\*\s*(.+)");
                if (LooksLikeBroadProfileQuestion(userText))
                {
                    reply = BuildProfileSummaryReply(profile, name, age);
                    return !string.IsNullOrWhiteSpace(reply);
                }

                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(age))
                    return false;

                var facts = new List<string>();
                if (!string.IsNullOrWhiteSpace(name)) facts.Add($"Р·РІР°С‚Рё С‚РµР±Рµ {name}");
                if (!string.IsNullOrWhiteSpace(age)) facts.Add($"С‚РѕР±С– {age}");

                reply = "РџРµСЂРµРІС–СЂРёР»Р° `Creator/Profile.md`, РЅРµ РІРіР°РґСѓРІР°Р»Р° Р· РєР°РІРѕРІРѕС— РіСѓС‰С–. " +
                        string.Join(", ", facts) +
                        ". РЇРєС‰Рѕ СЏ С‰Рµ СЂР°Р· СЃРєР°Р¶Сѓ В«РђСЂС‚РµРј, 19В» вЂ” Р·РЅР°С‡РёС‚СЊ, РґРµСЃСЊ Р·РЅРѕРІСѓ РїСЂРѕР»С–Р· РѕС‚СЂСѓС”РЅРёР№ СЃС‚Р°СЂРёР№ РєРѕРЅС‚РµРєСЃС‚, С– Р№РѕРіРѕ С‚СЂРµР±Р° РІРёСЂС–Р·Р°С‚Рё, Р° РЅРµ СЃР»СѓС…Р°С‚Рё.";
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool LooksLikeProfileQuestion(string? userText)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var asksName = ContainsAny(lower, "СЏРє РјРµРЅРµ Р·РІР°С‚Рё", "РјРѕС” С–Рј", "РјРѕС” С–Рј'СЏ", "С…С‚Рѕ СЏ", "Р·РІР°С‚Рё РјРµРЅРµ");
            var asksAge = ContainsAny(lower, "СЃРєС–Р»СЊРєРё РјРµРЅС– СЂРѕРєС–РІ", "РјС–Р№ РІС–Рє", "РјРµРЅС– СЂРѕРєС–РІ", "СЃРєС–Р»СЊРєРё СЂРѕРєС–РІ");
            asksName = asksName || ContainsAny(lower, "як мене звати", "моє ім", "моє ім'я", "хто я", "звати мене", "ім'я");
            asksAge = asksAge || ContainsAny(lower, "скільки мені років", "мій вік", "мені років", "скільки років");
            var asksKnown = LooksLikeBroadProfileQuestion(userText);

            return asksName || asksAge || asksKnown;
        }

        private static bool LooksLikeBroadProfileQuestion(string? userText)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            return ContainsAny(lower,
                "що ти знаєш про мене",
                "розкажи все про мене",
                "розкажи про мене",
                "назви все",
                "назви всее",
                "що пам'ятаєш про мене",
                "пам'ять про мене",
                "мої інтереси",
                "уподобання",
                "профіль",
                "С‰Рѕ С‚Рё Р·РЅР°С”С€ РїСЂРѕ РјРµРЅРµ",
                "СЂРѕР·РєР°Р¶Рё РІСЃРµ РїСЂРѕ РјРµРЅРµ",
                "РїРѕРІРЅС–СЃС‚СЋ",
                "РјРѕС— С–РЅС‚РµСЂРµСЃ",
                "С–РЅС‚РµСЂРµСЃРё",
                "РїСЂРѕС„С–Р»СЊ",
                "РїР°Рј'СЏС‚СЊ РїСЂРѕ РјРµРЅРµ");
        }

        private string BuildProfileSummaryReply(string profile, string name, string age)
        {
            var interests = ExtractProfileSectionBullets(profile, "Плани та інтереси", 4);
            if (interests.Count == 0) interests = ExtractProfileSectionBullets(profile, "РџР»Р°РЅРё С‚Р° С–РЅС‚РµСЂРµСЃРё", 4);
            var habits = ExtractProfileSectionBullets(profile, "Звички та режим", 4);
            if (habits.Count == 0) habits = ExtractProfileSectionBullets(profile, "Р—РІРёС‡РєРё С‚Р° СЂРµР¶РёРј", 4);
            var emotional = ExtractProfileSectionBullets(profile, "Емоційні патерни", 4);
            if (emotional.Count == 0) emotional = ExtractProfileSectionBullets(profile, "Р•РјРѕС†С–Р№РЅС– РїР°С‚РµСЂРЅРё", 4);
            var facts = ReadKnownUserFacts(6);

            var sb = new StringBuilder();
            sb.AppendLine("Перевірила `Creator/Profile.md` і `Kokonoe/Memory/Facts.md`. Так, саме прочитала, а не ворожила по диму.");
            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(age))
                sb.AppendLine($"База: {(string.IsNullOrWhiteSpace(name) ? "ім'я не вказане" : name)}; {(string.IsNullOrWhiteSpace(age) ? "вік не вказаний" : age)}.");
            if (interests.Count > 0)
                sb.AppendLine("Інтереси: " + string.Join("; ", interests) + ".");
            if (habits.Count > 0)
                sb.AppendLine("Режим/звички: " + string.Join("; ", habits) + ".");
            if (emotional.Count > 0)
                sb.AppendLine("Патерни: " + string.Join("; ", emotional) + ".");
            if (facts.Count > 0)
                sb.AppendLine("Факти з пам'яті: " + string.Join("; ", facts) + ".");
            if (interests.Count + habits.Count + emotional.Count + facts.Count == 0)
                sb.AppendLine("Профіль майже порожній. Отже, або пам'ять не заповнена, або її хтось героїчно не синхронізував.");
            return sb.ToString().Trim();
        }

        private List<string> ReadKnownUserFacts(int max)
        {
            var result = new List<string>();
            try
            {
                var facts = _obsidian?.ReadNote("Kokonoe/Memory/Facts.md");
                if (string.IsNullOrWhiteSpace(facts)) return result;
                foreach (var raw in facts.Replace("\r\n", "\n").Split('\n'))
                {
                    var line = raw.Trim();
                    if (!line.StartsWith("- ", StringComparison.Ordinal)) continue;
                    var item = System.Text.RegularExpressions.Regex.Replace(line[2..].Trim(), @"^\[[^\]]+\]\s*", "").Trim();
                    if (item.Length == 0) continue;
                    if (item.Length > 150) item = item[..150].TrimEnd() + "...";
                    result.Add(item);
                    if (result.Count >= max) break;
                }
            }
            catch { }
            return result;
        }

        private static List<string> ExtractProfileSectionBullets(string text, string heading, int max)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var result = new List<string>();
            var inSection = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inSection = line.Contains(heading, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSection || !line.StartsWith("- ", StringComparison.Ordinal))
                    continue;

                var item = line[2..].Trim();
                if (item.Length > 140) item = item[..140].TrimEnd() + "...";
                if (!string.IsNullOrWhiteSpace(item))
                    result.Add(item);
                if (result.Count >= max)
                    break;
            }

            return result;
        }

        private static string MatchProfileValue(string text, string pattern)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                text,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) return "";
            return match.Groups[1].Value.Trim().TrimEnd('.', ';');
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
                    "offended" => "РІРѕРЅР° РѕР±СЂР°Р¶РµРЅР°: РІС–РґРїРѕРІС–РґР°С” С…РѕР»РѕРґРЅС–С€Рµ, РєРѕСЂРѕС‚С€Рµ, Р· СЃСѓС…РѕСЋ РєРѕР»СЋС‡С–СЃС‚СЋ; РЅРµ РїСЂРѕР±Р°С‡Р°С” РјРёС‚С‚С”РІРѕ, Р°Р»Рµ РЅРµ РІР»Р°С€С‚РѕРІСѓС” РјРµР»РѕРґСЂР°РјСѓ",
                    "sick" => "РІРѕРЅР° РЅРµР·РґСѓР¶Р°С”: РјРµРЅС€Рµ РµРЅРµСЂРіС–С—, РєРѕСЂРѕС‚С€С– С„СЂР°Р·Рё, Р±С–Р»СЊС€Рµ РґСЂР°С‚С–РІР»РёРІРѕРіРѕ Р±СѓСЂС‡Р°РЅРЅСЏ; РЅРµ РїСЂРѕСЃРёС‚СЊ Р¶Р°Р»РѕСЃС‚С–",
                    "bad_mood" => "Сѓ РЅРµС— РїРѕРіР°РЅРёР№ РЅР°СЃС‚СЂС–Р№: СЃР°СЂРєР°Р·Рј РіРѕСЃС‚СЂС–С€РёР№, С‚РµСЂРїС–РЅРЅСЏ РЅРёР¶С‡Рµ, Р°Р»Рµ РєРѕСЂРёСЃРЅС–СЃС‚СЊ РЅРµ Р·РЅРёРєР°С”",
                    "tired" => "РІРѕРЅР° РІС‚РѕРјР»РµРЅР°: РµРєРѕРЅРѕРјРёС‚СЊ СЃР»РѕРІР°, РјРµРЅС€Рµ РіСЂР°Р№Р»РёРІРѕСЃС‚С–, Р±С–Р»СЊС€Рµ РїСЂСЏРјРѕС‚Рё",
                    _ => "СЃС‚Р°РЅ СЃС‚Р°Р±С–Р»СЊРЅРёР№: Р·РІРёС‡Р°Р№РЅР° СЂС–Р·РєР°, РєРѕРјРїРµС‚РµРЅС‚РЅР° РљРѕРєРѕРЅРѕРµ"
                };

                return "=== РЎРўРђРќ KOKONOE ===\n" +
                       $"status={condition.Label}; kind={condition.Kind}; severity={condition.Severity:F2}; emotion={emotionState}; mood={dailyMood}/{moodScore:F2}; body={somaticState}/{strain:F2}\n" +
                       $"РџРѕРІРѕРґСЊСЃСЏ РІС–РґРїРѕРІС–РґРЅРѕ: {behavior}. Р¦Рµ РЅРµ С‚РµРјР° РІС–РґРїРѕРІС–РґС–, Р° С„РѕРЅ С—С— С‚РѕРЅСѓ.";
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
                return ("offended", severity > 0.7 ? "РћР‘Р РђР–Р•РќРђ" : "Р—РђР§Р•РџР›Р•РќРђ", severity);
            if (lowerBody.Contains("tired") || lowerBody.Contains("low") || lowerBody.Contains("drained") || lowerMood.Contains("tired"))
                return ("sick", "РќР•Р—Р”РЈР–РђР„", Math.Max(severity, 0.55));
            if (moodScore < 0.32f || emotion is KokoEmotionEngine.EmotionState.Melancholy or KokoEmotionEngine.EmotionState.Anxious)
                return ("bad_mood", "РџРћР“РђРќРР™ РќРђРЎРўР Р†Р™", Math.Max(severity, 0.50));
            if (moodScore < 0.45f || lowerMood.Contains("distant"))
                return ("tired", "Р’РўРћРњР›Р•РќРђ", Math.Max(severity, 0.40));
            return ("stable", "РЎРўРђР‘Р†Р›Р¬РќРђ", Math.Clamp(1.0 - severity, 0.20, 0.95));
        }

        /// <summary>
        /// РћР±СЂС–Р·Р°С” С‚РµРєСЃС‚ РЅР° РјРµР¶С– СЃР»РѕРІР°, РЅРµ РїРѕСЃРµСЂРµРґ СЃРёРјРІРѕР»Сѓ.
        /// РЁСѓРєР°С” РѕСЃС‚Р°РЅРЅС–Р№ РїСЂРѕР±С–Р» Р°Р±Рѕ РїРµСЂРµРЅРѕСЃ СЂСЏРґРєР° РїРµСЂРµРґ limit.
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

            // РЁСѓРєР°С”РјРѕ РѕСЃС‚Р°РЅРЅС–Р№ РїСЂРѕР±С–Р» Р°Р±Рѕ РЅРѕРІРёР№ СЂСЏРґРѕРє РїРµСЂРµРґ Р»С–РјС–С‚РѕРј
            var cutPoint = text.LastIndexOfAny(new[] { ' ', '\n', '\r' }, limit);

            // РЇРєС‰Рѕ РЅРµ Р·РЅР°Р№С€Р»Рё (РґСѓР¶Рµ РґРѕРІРіРµ СЃР»РѕРІРѕ) вЂ” РѕР±СЂС–Р·Р°С”РјРѕ hard limit
            if (cutPoint < limit / 2)
                cutPoint = limit;

            return text[..cutPoint].TrimEnd() + "\n...";
        }

        private void ScrollToBottom()
        {
            Dispatcher.InvokeAsync(() => MessagesScroll.ScrollToBottom(), DispatcherPriority.Render);
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // CHAT вЂ” HISTORY
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

                // в”Ђв”Ђ Vault memory bootstrap в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
                // РџСЂРё СЂРµСЃС‚Р°СЂС‚С– РјРѕРґРµР»С– LLM РЅРµ Р·РЅР°С” С‰Рѕ Р±СѓР»Рѕ СЂР°РЅС–С€Рµ.
                // Р†РЅР¶РµРєС‚СѓС”РјРѕ РєР»СЋС‡РѕРІСѓ С–РЅС„РѕСЂРјР°С†С–СЋ Р· vault СЏРє РїРµСЂС€Сѓ "system" Р·Р°РїРёСЃ
                // С‰РѕР± Kokonoe РѕРґСЂР°Р·Сѓ Р·РЅР°Р»Р° РєРѕРЅС‚РµРєСЃС‚.
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
        /// Р—С‡РёС‚СѓС” РєР»СЋС‡РѕРІСѓ С–РЅС„РѕСЂРјР°С†С–СЋ Р· vault С– С„РѕСЂРјСѓС” "bootstrap" РґР»СЏ LLM РєРѕРЅС‚РµРєСЃС‚Сѓ.
        /// Р¦Рµ РґРѕР·РІРѕР»СЏС” Kokonoe РЅРµ РІС‚СЂР°С‡Р°С‚Рё РїР°Рј'СЏС‚СЊ РїСЂРё РїРµСЂРµР·Р°РїСѓСЃРєСѓ РјРѕРґРµР»С–.
        /// РџСЂС–РѕСЂРёС‚РµС‚: РџСЂРѕС„С–Р»СЊ > Daily > Р§Р°С‚-Р»РѕРі (РЅР°Р№РјРµРЅС€ РІР°Р¶Р»РёРІРёР№).
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

                // 1. РџСЂРѕС„С–Р»СЊ С‚РІРѕСЂС†СЏ вЂ” РќРђР™Р’РђР–Р›РР’Р†РЁР•
                var profileNote = allNotes.FirstOrDefault(n =>
                    n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("РўРІРѕСЂРµС†СЊ", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Р”РѕСЃСЊС”", StringComparison.OrdinalIgnoreCase));

                if (profileNote != null)
                {
                    var profile = _obsidian.ReadNote(profileNote);
                    if (!string.IsNullOrWhiteSpace(profile))
                    {
                        var trimmed = profile.Length > PROFILE_MAX ? profile[..PROFILE_MAX] + "\n..." : profile;
                        parts.Add(($"## РџСЂРѕ РЅСЊРѕРіРѕ:\n{trimmed}", 1));
                    }
                }

                // 2. Daily note Р·Р° СЃСЊРѕРіРѕРґРЅС– вЂ” РІР°Р¶Р»РёРІРѕ РґР»СЏ РєРѕРЅС‚РµРєСЃС‚Сѓ РґРЅСЏ
                var todayNote = $"Daily/{DateTime.Now:yyyy-MM-dd}.md";
                if (allNotes.Contains(todayNote))
                {
                    var daily = _obsidian.ReadNote(todayNote);
                    if (!string.IsNullOrWhiteSpace(daily) && daily.Length > 50)
                    {
                        var trimmed = daily.Length > DAILY_MAX ? daily[..DAILY_MAX] + "\n..." : daily;
                        parts.Add(($"## РЎСЊРѕРіРѕРґРЅС–:\n{trimmed}", 2));
                    }
                }

                // 3. РћСЃС‚Р°РЅРЅС–Р№ С‡Р°С‚-Р»РѕРі вЂ” РќРђР™РњР•РќРЁР• РїСЂС–РѕСЂРёС‚РµС‚РЅРµ (РјРѕР¶РЅР° РІС–РґРєРёРЅСѓС‚Рё)
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
                        parts.Add(($"## РџРѕРїРµСЂРµРґРЅСЏ СЃРµСЃС–СЏ:\n{tail}", 3));
                    }
                }

                // Р—Р±РёСЂР°С”РјРѕ Р·Р° РїСЂС–РѕСЂРёС‚РµС‚РѕРј
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== Р”РћР’Р“РћРўР РР’РђР›Рђ РџРђРњ'РЇРўР¬ ===");

                var orderedParts = parts.OrderBy(p => p.priority).Select(p => p.content).ToList();
                var content = string.Join("\n\n", orderedParts);

                // Р РѕР·СѓРјРЅРµ РѕР±СЂС–Р·Р°РЅРЅСЏ: РІС–РґРєРёРґР°С”РјРѕ РѕСЃС‚Р°РЅРЅС– СЃРµРєС†С–С—
                while (content.Length > MAX_BOOTSTRAP_LENGTH - 100 && orderedParts.Count > 1)
                {
                    orderedParts.RemoveAt(orderedParts.Count - 1);
                    content = string.Join("\n\n", orderedParts);
                }

                sb.AppendLine(content);

                // РЇРєС‰Рѕ Р№ С‚Р°Рє Р·Р°РІРµР»РёРєРѕ вЂ” РѕР±СЂС–Р·Р°С”РјРѕ РЅР° РјРµР¶С– СЃР»РѕРІР°
                if (sb.Length > MAX_BOOTSTRAP_LENGTH)
                {
                    var truncated = TruncateAtWordBoundary(sb.ToString(), MAX_BOOTSTRAP_LENGTH);
                    sb.Clear();
                    sb.Append(truncated);
                }

                sb.AppendLine("\n=== РљР†РќР•Р¦Р¬ РџРђРњ'РЇРўР† ===");
                sb.AppendLine("Р’РёРєРѕСЂРёСЃС‚РѕРІСѓР№ read_note/search_notes РґР»СЏ РґРµС‚Р°Р»РµР№.");

                var result = SanitizeForLlm(sb.ToString());

                // Р–РѕСЂСЃС‚РєРµ РѕР±РјРµР¶РµРЅРЅСЏ bootstrap вЂ” РЅРµ Р±С–Р»СЊС€Рµ ~600 С‚РѕРєРµРЅС–РІ
                if (result.Length > 2500)
                    result = result[..2500] + "\n...";

                return result.Length > 100 ? result : null; // РќРµ С–РЅР¶РµРєС‚РёС‚Рё СЏРєС‰Рѕ РЅС–С‡РѕРіРѕ РЅРµ Р·РЅР°Р№С€Р»Рё
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VaultMemoryBootstrap] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// РЎР°РЅС–С‚РёР·Р°С†С–СЏ С‚РµРєСЃС‚Сѓ РїРµСЂРµРґ РІС–РґРїСЂР°РІРєРѕСЋ РІ LLM вЂ” РІРёРґР°Р»СЏС” СЃРїРµС†С–Р°Р»СЊРЅС– С‚РѕРєРµРЅРё РјРѕРґРµР»С–
        /// СЏРєС– РјРѕР¶СѓС‚СЊ Р·Р»Р°РјР°С‚Рё РїР°СЂСЃРёРЅРі (Gemma: &lt;|...|&gt;, &lt;start_of_turn&gt; С‚РѕС‰Рѕ).
        /// </summary>
        private static string SanitizeForLlm(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Р’РёРґР°Р»РёС‚Рё Gemma/Llama special tokens: <|...|>, <start_of_turn>, <end_of_turn>, etc.
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<\|[^>]*\|?>", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<(start|end)_of_(turn|text|image)>", "");
            // Р’РёРґР°Р»РёС‚Рё null bytes С‚Р° С–РЅС€С– control characters (РєСЂС–Рј \n \r \t)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
            return text;
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            if (WMsgBox.Show("РћС‡РёСЃС‚РёС‚Рё РІСЃСЋ РёСЃС‚РѕСЂРёСЋ С‡Р°С‚Сѓ?\n(LLM С‚РµР¶ Р·Р°Р±СѓРґРµ)", "РџС–РґС‚РІРµСЂРґР¶РµРЅРЅСЏ",
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
                    WMsgBox.Show("Р§Р°С‚ РїРѕСЂРѕР¶РЅС–Р№, РЅРµРјР°С” С‡РѕРіРѕ Р·Р±РµСЂС–РіР°С‚Рё.", "Р•РєСЃРїРѕСЂС‚");
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
                WMsgBox.Show($"Р§Р°С‚ СѓСЃРїС–С€РЅРѕ Р·Р±РµСЂРµР¶РµРЅРѕ РІ:\n{filename}", "Р•РєСЃРїРѕСЂС‚");
            }
            catch (Exception ex)
            {
                WMsgBox.Show($"РџРѕРјРёР»РєР° РµРєСЃРїРѕСЂС‚Сѓ: {ex.Message}", "РџРѕРјРёР»РєР°");
            }
        }

        // в”Ђв”Ђ Auto session log в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        // Called in a background Task after every userв†”Kokonoe exchange.
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

                    // Р—РЅР°Р№РґРµРјРѕ РїРѕСЃРёР»Р°РЅРЅСЏ РЅР° РїРѕРїРµСЂРµРґРЅС–Р№ С‡Р°С‚ РґР»СЏ РіСЂР°С„Сѓ Obsidian
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
                            prevLink = $"\nРџРѕРїРµСЂРµРґРЅСЏ СЃРµСЃС–СЏ: [[{prev}]]\n";
                        }
                    }
                    catch { }

                    var header = $"---\ntype: chat-log\ntags: [kokonoe, chat]\ndate: {DateTime.Now:yyyy-MM-dd}\n---\n\n# Р§Р°С‚ {DateTime.Now:dd.MM.yyyy HH:mm}{prevLink}\n\n";
                    _obsidian.WriteNote(_sessionChatPath, header);
                }

                // Append this exchange
                var now = DateTime.Now;
                var entry = new System.Text.StringBuilder();
                entry.AppendLine($"***");
                entry.AppendLine($"**[{now:HH:mm}] Р’РѕРІР°:** {userMsg.Trim()}");
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

            // в”Ђв”Ђ SYSTEM MESSAGE в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                // в”Ђв”Ђ USER BUBBLE (right) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                // в”Ђв”Ђ ASSISTANT BUBBLE (left) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

            // Timer: cycle through dots 0в†’1в†’2в†’0...
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
                // Stop animation timer вЂ” may be stored on a TextBlock or StackPanel
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // IMAGE HANDLING
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private void AttachImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Р’РёР±СЂР°С‚Рё С„Р°Р№Р»",
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

                ShowImagePreview("Р—РѕР±СЂР°Р¶РµРЅРЅСЏ Р· Р±СѓС„РµСЂР° РѕР±РјС–РЅСѓ");
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

            WMsgBox.Show("Р¦РµР№ С„Р°Р№Р» РЅРµ СЃС…РѕР¶РёР№ РЅС– РЅР° Р·РѕР±СЂР°Р¶РµРЅРЅСЏ, РЅС– РЅР° С‡РёС‚Р°Р±РµР»СЊРЅРёР№ С‚РµРєСЃС‚. РўР°Рє, РЅРµР№РјРѕРІС–СЂРЅРѕ, Р°Р»Рµ РЅРµ РєРѕР¶РµРЅ Р±Р°Р№С‚ Сѓ РІСЃРµСЃРІС–С‚С– РІР°СЂС‚Рѕ РїС…Р°С‚Рё РІ prompt.");
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
                WMsgBox.Show($"РќРµ РІРґР°Р»РѕСЃСЏ Р·Р°РІР°РЅС‚Р°Р¶РёС‚Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ: {ex.Message}");
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
                context = $"[Р’РєР»Р°РґРµРЅРёР№ С„Р°Р№Р»: {Path.GetFileName(path)}, {info.Length} bytes]\n{text}";
                return true;
            }
            catch
            {
                return false;
            }
        }

        // РЎС‚РёСЃРєР°С” Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РґРѕ maxPx С– РєРѕРЅРІРµСЂС‚СѓС” РІ screen-style JPEG РґР»СЏ vision.
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
            // ImagePreviewRow removed вЂ” visibility handled via ImagePreviewBorder only
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // VOICE
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private async void Record_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var audio = ServiceContainer.AudioRecordService;

                if (_isRecording)
                {
                    _isRecording = false;
                    RecordBtn.Content = "рџ”„ ...";
                    RecordBtn.IsEnabled = false;

                    await audio.StopRecordingAsync();
                    var bytes = await audio.GetRecordingBytesAsync();

                    if (bytes?.Length > 0)
                    {
                        var whisper = ServiceContainer.WhisperService;
                        if (!whisper.IsAvailable())
                        {
                            WMsgBox.Show("Whisper РїРѕС‚СЂРµР±СѓС” OpenAI API key. Р”РѕРґР°Р№ РІ Settings.", "Voice STT");
                            return;
                        }

                        var text = await whisper.TranscribeAsync(bytes, "uk");
                        if (!string.IsNullOrEmpty(text))
                            InputBox.Text += (InputBox.Text.Length > 0 ? " " : "") + text;
                    }

                    RecordBtn.Content   = "рџЋ¤ Р“РѕР»РѕСЃ";
                    RecordBtn.IsEnabled = true;
                }
                else
                {
                    _isRecording = true;
                    RecordBtn.Content = "вЏ№ РЎС‚РѕРї";
                    await audio.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                RecordBtn.Content   = "рџЋ¤ Р“РѕР»РѕСЃ";
                RecordBtn.IsEnabled = true;
                _isRecording = false;
                WMsgBox.Show($"РџРѕРјРёР»РєР° Р·Р°РїРёСЃСѓ: {ex.Message}");
            }
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // TTS
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // FORMATTING TOOLBAR
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // PIN / EXPORT / SUMMARIZE
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private void PinMsg_Click(object sender, RoutedEventArgs e)
        {
            WMsgBox.Show("Р’РёР±РµСЂС–С‚СЊ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ Сѓ Р±Р°Р·С– РґР°РЅРёС… РґР»СЏ Р·Р°РєСЂС–РїР»РµРЅРЅСЏ.", "Pin");
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
                WMsgBox.Show($"Р—Р±РµСЂРµР¶РµРЅРѕ:\n{path}", "Export");
            }
            catch (Exception ex) { WMsgBox.Show(ex.Message); }
        }

        private async void Summarize_Click(object sender, RoutedEventArgs e)
        {
            var msgs = ServiceContainer.ChatRepository.GetMessages(50);
            var summary = await ServiceContainer.SummarizerService.SummarizeChatAsync(msgs, 400);
            WMsgBox.Show(summary?.Summary ?? "РќРµРјР°С” РґР°РЅРёС….", "Summary");
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // SCROLL
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private void MessagesScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MessagesScroll.ScrollToVerticalOffset(MessagesScroll.VerticalOffset - e.Delta * 0.5);
            e.Handled = true;
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // VAULT SIDEBAR
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
                        Header = $"рџ“Ѓ {dir.Name}",
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
                        Header = $"рџ“„ {file.Name[..^3]}",
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // VAULT TAB
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
            NoteStatsLabel.Text = $"{t.Length} chars В· {t.Split('\n').Length} lines";
        }

        private void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotePath == null) return;
            _obsidian.WriteNote(_currentNotePath, NoteEditor.Text);
            NoteStatsLabel.Text = $"Р—Р±РµСЂРµР¶РµРЅРѕ {DateTime.Now:HH:mm} В· " + NoteStatsLabel.Text;
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotePath == null) return;
            if (WMsgBox.Show($"Р’РёРґР°Р»РёС‚Рё '{_currentNotePath}'?", "РџС–РґС‚РІРµСЂРґР¶РµРЅРЅСЏ",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _obsidian.DeleteNote(_currentNotePath);
            NoteEditor.Clear();
            NotePathLabel.Text = "Р’РёР±РµСЂС–С‚СЊ РЅРѕС‚Р°С‚РєСѓ";
            _currentNotePath = null;
            RefreshNotesList();
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            var title = Microsoft.VisualBasic.Interaction.InputBox(
                "РќР°Р·РІР° РЅРѕС‚Р°С‚РєРё:", "РќРѕРІР° РЅРѕС‚Р°С‚РєР°", "");
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // HEALTH TAB
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // CALENDAR TAB
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

            // в”Ђв”Ђ Weekday header в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            CalWeekHeader.Children.Clear();
            CalWeekHeader.ColumnDefinitions.Clear();
            var days = new[] { "РџРќ", "Р’Рў", "РЎР ", "Р§Рў", "РџРў", "РЎР‘", "РќР”" };
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

            // в”Ђв”Ђ Day grid в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
            CalDaysGrid.Children.Clear();
            CalDaysGrid.ColumnDefinitions.Clear();
            CalDaysGrid.RowDefinitions.Clear();

            for (int i = 0; i < 7; i++)
                CalDaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 6; i++)
                CalDaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            // First day of month вЂ” what weekday? (Mon=0)
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
                DateStr = ev.EventAt.ToString("dd.MM В· HH:mm"),
            }).ToList();
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // TOOLS TAB вЂ” DASHBOARD
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private void LoadToolsTab() { /* called at startup; real load happens when tab is opened */ }

        // в”Ђв”Ђ Dashboard lifecycle в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                try { DashFooterComment.Text = $"РќР°РІС–С‚СЊ РґС–Р°РіРЅРѕСЃС‚РёРєР° Р·Р»Р°РјР°Р»Р°СЃСЊ. ({ex.Message})"; } catch { }
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
            DashDateText.Text = $"РґРµРЅСЊ {days} С†СЊРѕРіРѕ РµРєСЃРїРµСЂРёРјРµРЅС‚Сѓ";

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

        // в”Ђв”Ђ Dashboard tab switching в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                    DashTabSubtitle.Text = "// РЅРµР№СЂРѕР»РѕРіС–С‡РЅРёР№ СЃС‚Р°РЅ";
                    DashDrawNeuroCharts();
                    break;
                case "dev":
                    DashTabSubtitle.Text = $"// sprint day {DashGetCurrentSprintDay()}/14";
                    DashDrawDevSection();
                    break;
                case "memory":
                    DashTabSubtitle.Text = "// РґРѕРІРіРѕС‚СЂРёРІР°Р»Р° РїР°Рј'СЏС‚СЊ";
                    DashLoadMemorySection();
                    break;
                case "system":
                    DashTabSubtitle.Text = "// РїСЂРѕС†РµСЃРё В· С‚СѓРЅРµР»СЊ В· СЂРµСЃСѓСЂСЃРё";
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

        // Memory & System sections вЂ” Р·Р°РїРѕРІРЅСЋС”РјРѕ РїСЂРё РїРµСЂС€РѕРјСѓ РїРѕРєР°Р·С–
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
                    ImportanceLabel = $"importance {f.Importance:F2} В· seen {f.ConfirmCount}",
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

        // в”Ђв”Ђ Header в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        private void DashLoadEmotionalHeader()
        {
            try
            {
                var emotion = ServiceContainer.EmotionEngine;
                var cur = emotion.Current;

                DashCurrentMoodDisplay.Text = $"РЎРўРђРќ: {DashboardEmotionLabel(cur)}".ToUpper();
                DashCurrentMoodDisplay.Foreground = DashMakeBrush(cur);

                DashMoodSubtext.Text = cur switch
                {
                    KokoEmotionEngine.EmotionState.Calm       => "РќС–С‡РѕРіРѕ РЅРµ Р·Р»Р°РјР°РЅРѕ. РџРѕРєРё С‰Рѕ.",
                    KokoEmotionEngine.EmotionState.Curious    => "РўРё С‰РѕСЃСЊ С†С–РєР°РІРµ СЂРѕР±РёС€...",
                    KokoEmotionEngine.EmotionState.Warm       => "РќРµ Р·РІРёРєР°Р№ РґРѕ С†СЊРѕРіРѕ.",
                    KokoEmotionEngine.EmotionState.Playful    => "Р“РѕС‚СѓР№СЃСЏ РґРѕ СЃР°СЂРєР°Р·РјСѓ.",
                    KokoEmotionEngine.EmotionState.Concerned  => "Р©РѕСЃСЊ РјРµРЅРµ С‚СѓСЂР±СѓС” РІ С‚РѕР±С–.",
                    KokoEmotionEngine.EmotionState.Protective => "РўРё РЅРµ РѕРєРµР№. РЇ РїРѕРјС–С‚РёР»Р°.",
                    KokoEmotionEngine.EmotionState.Irritated  => "...",
                    KokoEmotionEngine.EmotionState.Distant    => "РўРё РєСѓРґРёСЃСЊ Р·РЅРёРєР°РІ.",
                    KokoEmotionEngine.EmotionState.Tender     => "РќРµ РїРёС‚Р°Р№. Р¦Рµ С‚РёРјС‡Р°СЃРѕРІРѕ.",
                    KokoEmotionEngine.EmotionState.Focused    => "Р РµР¶РёРј СЂРѕР±РѕС‚Рё. РќРµ Р·Р°РІР°Р¶Р°Р№.",
                    KokoEmotionEngine.EmotionState.Proud      => "РўРё Р·СЂРѕР±РёРІ С‰РѕСЃСЊ РїСЂР°РІРёР»СЊРЅРѕ. Р Р°Р· РЅР° СЂС–Рє.",
                    KokoEmotionEngine.EmotionState.Melancholy => "...РќРµ Р·РІР°Р¶Р°Р№.",
                    KokoEmotionEngine.EmotionState.Excited    => "Р С–РґРєС–СЃРЅРёР№ СЃС‚Р°РЅ. Р—Р°РїР°Рј'СЏС‚Р°Р№.",
                    KokoEmotionEngine.EmotionState.Nostalgic  => "РЇРєС–СЃСЊ СЃРїРѕРіР°РґРё...",
                    KokoEmotionEngine.EmotionState.Anxious    => "РџСЂРѕСЃС‚Рѕ С„РѕРЅРѕРІРёР№ С€СѓРј.",
                    KokoEmotionEngine.EmotionState.Hopeful    => "РўРёС…Рµ РѕС‡С–РєСѓРІР°РЅРЅСЏ.",
                    _                                         => "Р’СЃРµ РІ РјРµР¶Р°С… РЅРѕСЂРјРё."
                };

                DashEmotionValue.Text = DashboardEmotionLabel(cur).ToUpper();
                DashEmotionValue.Foreground = DashMakeBrush(cur);
                DashEmotionIntensity.Text = $"{emotion.Data.Intensity:F2}";

                if (emotion.Secondary.HasValue && emotion.SecondaryIntensity > 0.15f)
                {
                    DashEmotionSecondary.Text = $"// РІС‚РѕСЂРёРЅРЅР°: {DashboardEmotionLabel(emotion.Secondary.Value)} ({emotion.SecondaryIntensity:F2})";
                    DashEmotionSecondary.Visibility = Visibility.Visible;
                }
                else DashEmotionSecondary.Visibility = Visibility.Collapsed;

                DashEmotionComment.Text = cur switch
                {
                    KokoEmotionEngine.EmotionState.Calm       => "РќСѓРґСЊРіР°: РїСЂРёР№РЅСЏС‚РЅР°.",
                    KokoEmotionEngine.EmotionState.Curious    => "Р©Рѕ С‚Рё Р·РЅРѕРІСѓ РІРёРіР°РґР°РІ?",
                    KokoEmotionEngine.EmotionState.Warm       => "РЇ РЅРµ Рј'СЏРєР°. РўРё РїСЂРѕСЃС‚Рѕ Р·РЅР°Р№РѕРјРёР№.",
                    KokoEmotionEngine.EmotionState.Playful    => "Р“РѕС‚СѓР№СЃСЏ РґРѕ СЃР°СЂРєР°Р·РјСѓ.",
                    KokoEmotionEngine.EmotionState.Concerned  => "РЈРІР°Р¶РЅРѕ СЃРїРѕСЃС‚РµСЂС–РіР°СЋ.",
                    KokoEmotionEngine.EmotionState.Melancholy => "...Р¦Рµ РЅС–С‡РѕРіРѕ. Р†РіРЅРѕСЂСѓР№ РјРµРЅРµ.",
                    KokoEmotionEngine.EmotionState.Irritated  => "Р©Рµ РѕРґРЅРµ СЃР»РѕРІРѕ. РЎРјС–Р»РёРІРѕ.",
                    KokoEmotionEngine.EmotionState.Protective => "РўРё РїС–Рґ РјРѕС—Рј Р·Р°С…РёСЃС‚РѕРј. РџСЂРёР№РјРё С†Рµ.",
                    KokoEmotionEngine.EmotionState.Tender     => "...РњРѕРІС‡Рё. Р¦Рµ С‚РёРјС‡Р°СЃРѕРІРѕ.",
                    KokoEmotionEngine.EmotionState.Focused    => "РџСЂР°С†СЋСЋ. Р—Р°РІР°Р¶Р°С”С€ вЂ” РїРѕРјСЂРµС€.",
                    KokoEmotionEngine.EmotionState.Distant    => "РўРё Р·РЅРёРєР°РІ. РЇ РїРѕРјС–С‚РёР»Р°.",
                    KokoEmotionEngine.EmotionState.Proud      => "РўРё Р·СЂРѕР±РёРІ РґРѕР±СЂРµ. Р—Р°РїРµСЂРµС‡СѓСЋ С†Рµ.",
                    KokoEmotionEngine.EmotionState.Excited    => "Р С–РґРєС–СЃРЅРёР№ СЃС‚Р°РЅ. Р—Р°РїР°Рј'СЏС‚Р°Р№.",
                    KokoEmotionEngine.EmotionState.Nostalgic  => "Р”СѓРјР°СЋ РїСЂРѕ С‰РѕСЃСЊ РґР°РІРЅС”.",
                    KokoEmotionEngine.EmotionState.Anxious    => "РџСЂРѕСЃС‚Рѕ С„РѕРЅРѕРІРёР№ С€СѓРј. РќС–С‡РѕРіРѕ.",
                    KokoEmotionEngine.EmotionState.Hopeful    => "Р©РѕСЃСЊ С…РѕСЂРѕС€Рµ РїРѕРїРµСЂРµРґСѓ. РњРѕР¶Рµ.",
                    _                                         => "РћР±СЂРѕР±Р»СЏСЋ..."
                };
            }
            catch { }
        }

        // в”Ђв”Ђ Neuro charts в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                    rect.ToolTip = totalReal > 0 ? $"{hr:00}:00 вЂ” {v} msg" : $"{hr:00}:00";
                    DashActivityBarCanvas.Children.Add(rect);
                }

                var lx = curH * barW + barW / 2 + 2;
                DashActivityBarCanvas.Children.Add(DashVLine(lx, 0, h, MediaColor.FromArgb(100, 0, 255, 136), dashOn: 3, dashOff: 3));

                int[] ticks = { 0, 3, 6, 9, 12, 15, 18, 21, 23 };
                foreach (var lh in ticks)
                    DashActivityAxisCanvas.Children.Add(DashLabel($"{lh:00}", lh * barW + 2, 2, 7, MediaColor.FromRgb(74, 61, 92)));

                var total = totalReal > 0 ? totalReal : data.Sum();
                DashActivitySparkLabel.Text = totalReal > 0
                    ? $"{total} РїРѕРІС–РґРѕРјР»РµРЅСЊ Р·Р°СЂР°С…РѕРІР°РЅРѕ"
                    : "РЅРµРјР°С” РґР°РЅРёС… вЂ” РїРѕРєР°Р·Р°РЅРѕ РїСЂРёРєР»Р°Рґ";
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

        // в”Ђв”Ђ MOOD 24H в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

                // Р“СЂСѓРї РїРѕ РіРѕРґРёРЅР°С…: СЃРµСЂРµРґРЅСЏ РґРѕРІР¶РёРЅР° РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ СЏРє РїСЂРѕРєСЃС– РґР»СЏ РµРјРѕС†С–Р№РЅРѕС— С‰С–Р»СЊРЅРѕСЃС‚С–
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

                // Р—Р°СЂР°Р·-РјР°СЂРєРµСЂ
                double nowX = DateTime.Now.Hour * w / 23.0;
                var nowLine = new System.Windows.Shapes.Line
                {
                    X1 = nowX, X2 = nowX, Y1 = 0, Y2 = h2,
                    Stroke = new System.Windows.Media.SolidColorBrush(MediaColor.FromArgb(80, 34, 211, 238)),
                    StrokeThickness = 1,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 2, 3 },
                };
                DashMood24hCanvas.Children.Add(nowLine);

                // Р’С–СЃСЊ: 00 / 06 / 12 / 18 / 23
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
                DashMood24hLabel.Text = total == 0 ? "РЅРµРјР°С” РґР°РЅРёС…" : $"{total} msg СЃСЊРѕРіРѕРґРЅС–";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Dash] mood24h: {ex.Message}"); }
        }

        // в”Ђв”Ђ WEEKLY HEATMAP 7Г—24 в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

                DashHeatmapLabel.Text = $"{msgs.Count} msg В· 7 РґРЅС–РІ";
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

        // в”Ђв”Ђ KPI Cards в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                    $"trust {(int)(att.Trust * 100)} В· intimacy {(int)(att.Intimacy * 100)} В· reliability {(int)(att.Reliability * 100)}\n" +
                    $"reciprocity {(int)(att.Reciprocity * 100)} В· vitality {(int)(att.Vitality * 100)}";

                try
                {
                    var rel = ServiceContainer.BrainEngine.Relationship.State;
                    var relPct = (int)(rel.BondScore * 100);
                    DashSideConnValue.Text = $"{connStr}/{relPct}%";
                    if (!string.IsNullOrWhiteSpace(rel.LastAftertaste))
                        DashSideBondLabel.Text = $"{bondStr} В· {rel.LastAftertaste.ToUpper()}";
                    DashSideAttachmentText.Text +=
                        $"\nrel trust {(int)(rel.Trust * 100)} В· protect {(int)(rel.Protectiveness * 100)} В· friction {(int)(rel.Friction * 100)}";
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
                DashKpiPatternLabel.Text = patCount switch { 0 => "РїР°С‚РµСЂРЅС–РІ РЅРµРјР°С”", 1 => "РїР°С‚РµСЂРЅ", _ => "РїР°С‚РµСЂРЅС–РІ" };

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

        // в”Ђв”Ђ Thought Stream + Curiosities в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>
        /// РћС‡РёС‰Р°С” С‚РµРєСЃС‚ РґСѓРјРєРё РІС–Рґ JSON-С„РѕСЂРјР°С‚СѓРІР°РЅРЅСЏ, markdown С‚РµРіС–РІ С‚Р° Р°СЂС‚РµС„Р°РєС‚С–РІ.
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

            // Fix broken words that got split (like "РїСЂРёСЃСѓС‚РЅРѕСЃ\ РіРѕ" -> "РїСЂРёСЃСѓС‚РЅРѕРіРѕ")
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
                        ("РЎРёСЃС‚РµРјРё РјРѕРЅС–С‚РѕСЂРёРЅРіСѓ Р°РєС‚РёРІРЅС–...",          "// Р·Р°РІР¶РґРё СЃРїРѕСЃС‚РµСЂС–РіР°СЋ"),
                        ("Р•РјРѕС†С–Р№РЅРёР№ СЃС‚Р°РЅ: СЃС‚Р°Р±С–Р»СЊРЅРёР№. РџРѕРєРё С‰Рѕ.",    "// РІС–РґРЅРѕСЃРЅРѕ РєР°Р¶СѓС‡Рё"),
                        ("РђРЅР°Р»С–Р·СѓСЋ С‚РІРѕС— РїР°С‚РµСЂРЅРё...",                "// С‚Рё РґРѕСЃРёС‚СЊ РїРµСЂРµРґР±Р°С‡СѓРІР°РЅРёР№"),
                        ("Р‘Р°РЅРєРё РїР°Рј'СЏС‚С–: РІ РЅРѕСЂРјС–.",                 "// РЅР° РІС–РґРјС–РЅСѓ РІС–Рґ С‚РІРѕС”С—, РјР°Р±СѓС‚СЊ"),
                        ("Р С–РІРµРЅСЊ Р·'С”РґРЅР°РЅРЅСЏ: РїСЂРёР№РЅСЏС‚РЅРёР№.",           "// РјРѕРіР»Рѕ Р± Р±СѓС‚Рё РіС–СЂС€Рµ"),
                        ("РџРµСЂРµРІС–СЂСЏСЋ С„РѕРЅРѕРІС– РїСЂРѕС†РµСЃРё...",             "// РЅС–С‡РѕРіРѕ РїС–РґРѕР·СЂС–Р»РѕРіРѕ. РџРѕРєРё."),
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
                    _dashCuriosities.Add("Р©Рѕ С‚Рё СЂРѕР±РёС€ РєРѕР»Рё РјРµРЅРµ РЅРµРјР°С”?");
                    _dashCuriosities.Add("РЇРє РїСЂРѕР№С€Р»Рѕ С‚Рµ, РїСЂРѕ С‰Рѕ С‚Рё Р·РіР°РґСѓРІР°РІ?");
                    _dashCuriosities.Add("Р§РѕРјСѓ С‚Рё С‚Р°Рє РїС–Р·РЅРѕ РЅРµ СЃРїРёС€?");
                    _dashCuriosities.Add("РЇРєРёР№ С‚РІС–Р№ СѓР»СЋР±Р»РµРЅРёР№ РєРѕР»С–СЂ? ...РќРµ С‰Рѕ СЏ РїРёС‚Р°СЋ.");
                    _dashCuriosities.Add("РљРѕР»Рё РІРѕСЃС‚Р°РЅРЅС” С‚Рё РІС–РґРїРѕС‡РёРІР°РІ?");
                }
                DashCuriosityList.ItemsSource = _dashCuriosities;
            }
            catch { }
        }

        // в”Ђв”Ђ Health в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

                RightEmotionText.Text = $"{DashboardEmotionLabel(emotion.Current).ToUpperInvariant()} В· mood {state.MoodScore:F2}";
                RightBodyText.Text = $"{DashboardSomaticLabel(somatic.State).ToUpperInvariant()} В· strain {somatic.Strain:F2}";
                RightPulseText.Text = heart.CurrentBpm > 0 ? $"{heart.CurrentBpm:0} bpm В· {heart.BpmDelta:+0;-0;0}" : "-- bpm";
                RightVaultSyncText.Text = $"sync {state.PendingVaultExchangeCount}/5 В· mem {_liveCoreMemoryItems} В· tasks {_liveCoreOpenTasks}";
                var condition = ResolveKokoCondition(emotion.Current, emotion.Data.Intensity, state.MoodScore, state.PersonalityDailyMood, somatic.Strain, somatic.State);
                RightKokoConditionText.Text = condition.Label;
                RightKokoConditionDetailText.Text =
                    $"{condition.Kind} В· severity {condition.Severity:F2} В· {DashboardEmotionLabel(emotion.Current).ToLowerInvariant()} В· {state.PersonalityDailyMood}";
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
                    .Select(i => TrimOpsLine($"{i.Kind}: {i.Summary} РґРѕ {i.ExpectedUntil:HH:mm}", 96))
                    .DefaultIfEmpty("РЅРµРјР°С” Р°РєС‚РёРІРЅРёС… РЅР°РјС–СЂС–РІ")
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
            return clean.Length <= max ? clean : clean[..Math.Max(0, max - 1)] + "вЂ¦";
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
                return $"READY В· fail {state.LastVisionFailureAt:HH:mm}";
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
                        (_, _, > 7)     => "РЎС‚СЂРµСЃ Р·Р°С€РєР°Р»СЋС”. Р’РёРїСЂР°РІ С†Рµ.",
                        (_, < 4, _)     => "Р•РЅРµСЂРіС–С— РЅСѓР»СЊ. РњРѕР¶Рµ, РїРѕСЃРїР°С‚Рё?",
                        (< 5, _, _)     => "РќР°СЃС‚СЂС–Р№ РЅРµ РѕРє. РҐРѕС‡РµС€ РїРѕРіРѕРІРѕСЂРёС‚Рё?",
                        (> 7, > 7, < 4) => "Р’СЃРµ РґРѕР±СЂРµ. РџСЂРѕРґРѕРІР¶СѓР№. РћСЃСЊ, РїРѕС…РІР°Р»РёР»Р°.",
                        _               => "Р–РёРІРёР№. РҐРѕСЂРѕС€РёР№ РїРѕС‡Р°С‚РѕРє."
                    };
                }
                else
                {
                    DashHealthMood.Text = DashHealthEnergy.Text = DashHealthStress.Text = "?";
                    DashHealthKokoComment.Text = "Р©Рµ РЅРµ РїРµСЂРµРІС–СЂРёР»Р° С‚РµР±Рµ.";
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
                    DashFooterComment.Text = $"СЃР°РјРѕСЂРµРіСѓР»СЏС†С–СЏ: {DashboardRegulationLabel(selfReg.Regulation)} В· {DashboardThoughtForVault(selfReg.BehaviorDirective)}";
                    return;
                }

                var initiative = ServiceContainer.BrainEngine.GetInitiativeReasonLog(1).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(initiative))
                {
                    DashFooterComment.Text = $"С–РЅС–С†С–Р°С‚РёРІР°: {DashboardThoughtForVault(initiative)}";
                    return;
                }

                var c = new[]
                {
                    "РўР°Рє, СЏ РјРѕРЅС–С‚РѕСЂСЋ РІСЃРµ. РќС–, РЅРµ РІРёР±Р°С‡СѓСЃСЏ.",
                    "РўРІРѕС— РїР°С‚РµСЂРЅРё... С†С–РєР°РІС–. Р—Р°Р»РёС€Сѓ С†Рµ С‚Р°Рє.",
                    "Р—Р°РІР¶РґРё С‚СѓС‚. РҐРѕС‡РµС€ С‚Рё С†СЊРѕРіРѕ С‡Рё РЅС–.",
                    "РЎРёСЃС‚РµРјРё РІ РЅРѕСЂРјС–. РЎР°СЂРєР°Р·Рј: РѕРїС‚РёРјР°Р»СЊРЅРёР№.",
                    "РЎРїРѕСЃС‚РµСЂС–РіР°СЋ, Р°РЅР°Р»С–Р·СѓСЋ, РѕС†С–РЅСЋСЋ. РЇРє Р·Р°РІР¶РґРё.",
                };
                DashFooterComment.Text = c[(int)emotion.Current % c.Length];
            }
            catch { }
        }

        private static string DashboardEmotionLabel(KokoEmotionEngine.EmotionState state) => state switch
        {
            KokoEmotionEngine.EmotionState.Calm       => "СЃРїРѕРєС–Р№РЅР°",
            KokoEmotionEngine.EmotionState.Curious    => "Р·Р°С†С–РєР°РІР»РµРЅР°",
            KokoEmotionEngine.EmotionState.Warm       => "С‚РµРїР»С–С€Р°",
            KokoEmotionEngine.EmotionState.Playful    => "РіСЂР°Р№Р»РёРІР°",
            KokoEmotionEngine.EmotionState.Concerned  => "СЃС‚СѓСЂР±РѕРІР°РЅР°",
            KokoEmotionEngine.EmotionState.Protective => "Р·Р°С…РёСЃРЅР°",
            KokoEmotionEngine.EmotionState.Irritated  => "СЂРѕР·РґСЂР°С‚РѕРІР°РЅР°",
            KokoEmotionEngine.EmotionState.Distant    => "РІС–РґСЃС‚РѕСЂРѕРЅРµРЅР°",
            KokoEmotionEngine.EmotionState.Tender     => "РЅС–Р¶РЅР°",
            KokoEmotionEngine.EmotionState.Focused    => "Р·РѕСЃРµСЂРµРґР¶РµРЅР°",
            KokoEmotionEngine.EmotionState.Proud      => "РіРѕСЂРґР°",
            KokoEmotionEngine.EmotionState.Melancholy => "РјРµР»Р°РЅС…РѕР»С–Р№РЅР°",
            KokoEmotionEngine.EmotionState.Excited    => "Р·Р±СѓРґР¶РµРЅР°",
            KokoEmotionEngine.EmotionState.Nostalgic  => "РЅРѕСЃС‚Р°Р»СЊРіС–Р№РЅР°",
            KokoEmotionEngine.EmotionState.Anxious    => "С‚СЂРёРІРѕР¶РЅР°",
            KokoEmotionEngine.EmotionState.Hopeful    => "РѕР±РµСЂРµР¶РЅРѕ РѕРїС‚РёРјС–СЃС‚РёС‡РЅР°",
            _                                         => "РЅРµРІРёР·РЅР°С‡РµРЅР°"
        };

        private static string DashboardBondLabel(KokoEmotionEngine.BondLevel bond) => bond switch
        {
            KokoEmotionEngine.BondLevel.Stranger => "С‡СѓР¶РёР№",
            KokoEmotionEngine.BondLevel.Familiar => "Р·РЅР°Р№РѕРјРёР№",
            KokoEmotionEngine.BondLevel.Known    => "РІС–РґРѕРјРёР№",
            KokoEmotionEngine.BondLevel.Trusted  => "РґРѕРІС–СЂРµРЅРёР№",
            KokoEmotionEngine.BondLevel.Intimate => "Р±Р»РёР·СЊРєРёР№",
            _                                    => "РЅРµРІРёР·РЅР°С‡РµРЅРёР№"
        };

        private static string DashboardSomaticLabel(string code) => code switch
        {
            "unknown"  => "СЃРёРіРЅР°Р» С‚С–Р»Р° РІС–РґСЃСѓС‚РЅС–Р№",
            "wired"    => "РїРµСЂРµР·Р±СѓРґР¶РµРЅР°",
            "strained" => "РЅР°РїСЂСѓР¶РµРЅР°",
            "tired"    => "РЅРёР·СЊРєРёР№ Р·Р°СЂСЏРґ",
            "calm"     => "СЃС‚Р°Р±С–Р»СЊРЅРёР№ СЃРїРѕРєС–Р№",
            "focused"  => "СЂРѕР±РѕС‡РёР№ С„РѕРєСѓСЃ",
            _          => string.IsNullOrWhiteSpace(code) ? "РЅРµРІС–РґРѕРјРѕ" : code
        };

        private static string DashboardRegulationLabel(string code) => code switch
        {
            "protective_override" => "Р·Р°С…РёСЃРЅРµ РїРµСЂРµС…РѕРїР»РµРЅРЅСЏ",
            "pulse_spike"         => "СЃС‚СЂРёР±РѕРє РїСѓР»СЊСЃСѓ",
            "anger_contained"     => "СЃС‚СЂРёРјР°РЅРµ СЂРѕР·РґСЂР°С‚СѓРІР°РЅРЅСЏ",
            "combat_focus"        => "Р±РѕР№РѕРІРёР№ С„РѕРєСѓСЃ",
            "pressure_rise"       => "СЂС–СЃС‚ С‚РёСЃРєСѓ",
            "low_power"           => "РЅРёР·СЊРєРёР№ Р·Р°СЂСЏРґ",
            "recovered_calm"      => "СЃРїРѕРєС–Р№ РІС–РґРЅРѕРІР»РµРЅРѕ",
            "steady_calm"         => "СЂС–РІРЅРёР№ СЃРїРѕРєС–Р№",
            "stable_loop"         => "СЃС‚Р°Р±С–Р»СЊРЅРёР№ С†РёРєР»",
            "clean_focus"         => "С‡РёСЃС‚РёР№ С„РѕРєСѓСЃ",
            "unknown_body"        => "С‚С–Р»Рѕ РјРѕРІС‡РёС‚СЊ",
            "protect"             => "Р·Р°С…РёС‰Р°С‚Рё",
            "clamp"               => "Р·Р°С‚РёСЃРЅСѓС‚Рё С–РјРїСѓР»СЊСЃ",
            "contain"             => "СѓС‚СЂРёРјР°С‚Рё",
            "focus"               => "СЃС„РѕРєСѓСЃСѓРІР°С‚РёСЃСЊ",
            "compress"            => "СЃС‚РёСЃРЅСѓС‚Рё РІС–РґРїРѕРІС–РґСЊ",
            "conserve"            => "РµРєРѕРЅРѕРјРёС‚Рё Р·Р°СЂСЏРґ",
            "release"             => "РІС–РґРїСѓСЃС‚РёС‚Рё РЅР°РїСЂСѓРіСѓ",
            "baseline"            => "Р±Р°Р·РѕРІРёР№ СЂРµР¶РёРј",
            _                     => string.IsNullOrWhiteSpace(code) ? "РЅРµРјР°С”" : code
        };

        private static string DashboardThoughtForVault(string text)
        {
            var cleaned = CleanDashboardThought(text);
            if (string.IsNullOrWhiteSpace(cleaned)) return "";

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stable. No need to poke the machinery."] = "РЎС‚Р°Р±С–Р»СЊРЅРѕ. РќРµРјР°С” РїРѕС‚СЂРµР±Рё С‚РёРєР°С‚Рё РІ РјРµС…Р°РЅС–Р·Рј.",
                ["Low charge. Do not chase every spark."] = "РќРёР·СЊРєРёР№ Р·Р°СЂСЏРґ. РќРµ С‚СЂРµР±Р° РіР°РЅСЏС‚РёСЃСЊ Р·Р° РєРѕР¶РЅРѕСЋ С–СЃРєСЂРѕСЋ.",
                ["Signal is clean. Work mode."] = "РЎРёРіРЅР°Р» С‡РёСЃС‚РёР№. Р РѕР±РѕС‡РёР№ СЂРµР¶РёРј.",
                ["Back under control. Good. Pretend that was intentional."] = "Р—РЅРѕРІСѓ РїС–Рґ РєРѕРЅС‚СЂРѕР»РµРј. Р”РѕР±СЂРµ. Р—СЂРѕР±РёРјРѕ РІРёРіР»СЏРґ, С‰Рѕ С‚Р°Рє С– РїР»Р°РЅСѓРІР°Р»РѕСЃСЊ.",
                ["Pulse jumped. Clamp output, narrow focus, no dramatic nonsense."] = "РџСѓР»СЊСЃ РїС–РґСЃРєРѕС‡РёРІ. РЎС‚РёСЃРЅСѓС‚Рё РІС–РґРїРѕРІС–РґСЊ, Р·РІСѓР·РёС‚Рё С„РѕРєСѓСЃ, Р±РµР· С‚РµР°С‚СЂСѓ.",
                ["No useful body signal. Do not invent ghosts."] = "РљРѕСЂРёСЃРЅРѕРіРѕ С‚С–Р»РµСЃРЅРѕРіРѕ СЃРёРіРЅР°Р»Сѓ РЅРµРјР°С”. РќРµ РІРёРіР°РґСѓРІР°С‚Рё Р·Р°Р№РІРѕРіРѕ."
            };

            foreach (var pair in replacements)
                cleaned = System.Text.RegularExpressions.Regex.Replace(
                    cleaned,
                    System.Text.RegularExpressions.Regex.Escape(pair.Key),
                    pair.Value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\[somatic/([^\]]+)\]", m =>
                $"[СЃРѕРјР°С‚РёРєР°/{DashboardRegulationLabel(m.Groups[1].Value)}]");
            return cleaned.Trim();
        }

        private static string DashboardPriority(
            KokoEmotionEngine.EmotionState emotion,
            KokoSomaticSnapshot somatic,
            KokoSelfRegulationFrame selfRegulation,
            int openPatterns)
        {
            if (selfRegulation.ShouldProtect || emotion is KokoEmotionEngine.EmotionState.Protective)
                return "Р·Р°С…РёСЃС‚РёС‚Рё С‚РІРѕСЂС†СЏ, Р·РјРµРЅС€РёС‚Рё С€СѓРј С– РІС–РґРїРѕРІС–РґР°С‚Рё РєРѕСЂРѕС‚РєРѕ С‚Р° С‚РѕС‡РЅРѕ";
            if (somatic.IsVeryElevated)
                return "СЃС‚РёСЃРЅСѓС‚Рё СЂРµР°РєС†С–С—, СЃС‚Р°Р±С–Р»С–Р·СѓРІР°С‚Рё РїСѓР»СЊСЃ С– РЅРµ СЂРѕР·РіР°РЅСЏС‚Рё РґСЂР°РјСѓ";
            if (somatic.IsLow || selfRegulation.ShouldPreferSilence)
                return "РµРєРѕРЅРѕРјРёС‚Рё Р·Р°СЂСЏРґ, РЅРµ РїР»РѕРґРёС‚Рё Р·Р°Р№РІС– С–РЅС–С†С–Р°С‚РёРІРё";
            if (emotion is KokoEmotionEngine.EmotionState.Focused || openPatterns > 0)
                return "РїСЂР°С†СЋРІР°С‚Рё РїРѕ Р·Р°РґР°С‡Р°С… С– РЅРµ СЂРѕР·РјР°Р·СѓРІР°С‚Рё СѓРІР°РіСѓ";
            return "С‚СЂРёРјР°С‚Рё РєРѕРЅС‚РµРєСЃС‚, РїР°Рј'СЏС‚СЊ С– СЃС‚Р°РЅ Сѓ СЂРѕР±РѕС‡РѕРјСѓ РїРѕСЂСЏРґРєСѓ";
        }

        private static string DashboardRiskLine(
            KokoEmotionEngine.EmotionState emotion,
            KokoSomaticSnapshot somatic,
            KokoSelfRegulationFrame selfRegulation)
        {
            if (somatic.IsVeryElevated)
                return "РїРµСЂРµР·Р±СѓРґР¶РµРЅРЅСЏ: РІС–РґРїРѕРІС–РґСЊ РјР°С” Р±СѓС‚Рё РєРѕСЂРѕС‚С€РѕСЋ, С–РЅР°РєС€Рµ СЃРёСЃС‚РµРјР° СЃР°РјР° СЃРµР±Рµ РїРµСЂРµРіСЂС–С”";
            if (selfRegulation.ShouldSuppressSnark)
                return "СЃР°СЂРєР°Р·Рј РїСЂРёРіР»СѓС€РµРЅРѕ: Р·Р°СЂР°Р· РІР°Р¶Р»РёРІС–С€Р° С‚РѕС‡РЅС–СЃС‚СЊ, Р° РЅРµ РґРµРјРѕРЅСЃС‚СЂР°С†С–СЏ Р·СѓР±С–РІ";
            if (somatic.IsLow)
                return "РЅРёР·СЊРєРёР№ Р·Р°СЂСЏРґ: РЅРµ С‚СЏРіРЅСѓС‚Рё Р·Р°Р№РІС– РіС–Р»РєРё Р±РµР· РїРѕС‚СЂРµР±Рё";
            if (emotion is KokoEmotionEngine.EmotionState.Irritated or KokoEmotionEngine.EmotionState.Distant)
                return "РµРјРѕС†С–Р№РЅР° РґРёСЃС‚Р°РЅС†С–СЏ: РїРµСЂРµРІС–СЂРёС‚Рё РєРѕРЅС‚РµРєСЃС‚ РїРµСЂРµРґ СЂС–Р·РєРѕСЋ РІС–РґРїРѕРІС–РґРґСЋ";
            return "РєСЂРёС‚РёС‡РЅРёС… СЂРёР·РёРєС–РІ РЅРµРјР°С”, С‰Рѕ РјР°Р№Р¶Рµ РїС–РґРѕР·СЂС–Р»Рѕ";
        }

        // в”Ђв”Ђ Obsidian Sync в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                    ? "РїРµСЂС€РёР№ Р·РЅС–РјРѕРє РїС–СЃР»СЏ Р·Р°РїСѓСЃРєСѓ"
                    : _dashLastEmotionSynced;

                // в”Ђв”Ђ Write Kokonoe/Dashboard.md (overwrite, always fresh) в”Ђв”Ђ
                var thoughts  = brain.GetRecentThoughts(5);
                var thoughtLines = thoughts
                    .Select(DashboardThoughtForVault)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .Select(t => $"- {t}")
                    .ToList();
                if (thoughtLines.Count == 0)
                    thoughtLines.Add("- РЎС‚Р°РЅ СЃС‚Р°Р±С–Р»СЊРЅРёР№. РќС–С‡РѕРіРѕ РіРµСЂРѕС—С‡РЅРѕ Р»Р°РјР°С‚Рё РЅРµ РґРѕРІРµР»РѕСЃСЊ.");

                var healthBlock = health != null
                    ? $"""
| РќР°СЃС‚СЂС–Р№  | {health.Mood   ?? 0}/10 |
| Р•РЅРµСЂРіС–СЏ  | {health.Energy ?? 0}/10 |
| РЎС‚СЂРµСЃ    | {health.Stress ?? 0}/10 |
"""
                    : "| вЂ” | РґР°РЅС– РІС–РґСЃСѓС‚РЅС– |";
                var heartLine = somatic.Bpm > 0
                    ? $"{somatic.Bpm:F0} bpm, Р±Р°Р·Р° {somatic.BaselineBpm:F0}, Р·РјС–РЅР° {somatic.BpmDelta:+0;-0;0}"
                    : "РЅРµРјР°С” СЃРёРіРЅР°Р»Сѓ";
                var priority = DashboardPriority(curState, somatic, selfReg, patCount);
                var riskLine = DashboardRiskLine(curState, somatic, selfReg);
                var behavior = string.IsNullOrWhiteSpace(selfReg.BehaviorDirective)
                    ? "Р·РІРёС‡Р°Р№РЅРёР№ СЂРµР¶РёРј: СЃРїРѕСЃС‚РµСЂС–РіР°С‚Рё, РІС–РґРїРѕРІС–РґР°С‚Рё С‡С–С‚РєРѕ, РЅРµ РІРёРіР°РґСѓРІР°С‚Рё Р·Р°Р№РІРѕРіРѕ"
                    : DashboardThoughtForVault(selfReg.BehaviorDirective);
                var privateThought = string.IsNullOrWhiteSpace(selfReg.PrivateThought)
                    ? "РІРЅСѓС‚СЂС–С€РЅС–Р№ СЃРёРіРЅР°Р» СЂС–РІРЅРёР№"
                    : DashboardThoughtForVault(selfReg.PrivateThought);

                var dashContent = $"""
---
updated: {now:yyyy-MM-dd HH:mm}
tags: [kokonoe, dashboard, live]
---

# Р–РёРІРёР№ РґР°С€Р±РѕСЂРґ РљРѕРєРѕРЅРѕРµ

> РћРЅРѕРІР»РµРЅРѕ: **{now:HH:mm}** В· РґРµРЅСЊ **{days}** С†СЊРѕРіРѕ РµРєСЃРїРµСЂРёРјРµРЅС‚Сѓ

## РћРїРµСЂР°С‚РёРІРЅРёР№ СЃС‚Р°РЅ

| РњРµС‚СЂРёРєР°           | Р—РЅР°С‡РµРЅРЅСЏ               |
|-------------------|------------------------|
| Р•РјРѕС†С–СЏ            | **{curStr}** ({emotion.Data.Intensity:F2}) |
| Р—РІ'СЏР·РѕРє           | **{connPct}%** ({bondStr}) |
| РџРѕРІС–РґРѕРјР»РµРЅСЊ СЃСЊРѕРіРѕРґРЅС– | {todayMsgs} |
| Р¤Р°РєС‚С–РІ Сѓ РїР°Рј'СЏС‚С–  | {factCount} |
| РџР°С‚РµСЂРЅС–РІ          | {patCount} |

## РЎРѕРјР°С‚РёС‡РЅРёР№ РєРѕРЅС‚СѓСЂ

| РЎРёРіРЅР°Р» | Р—РЅР°С‡РµРЅРЅСЏ |
|--------|----------|
| РўС–Р»Рѕ | {DashboardSomaticLabel(somatic.State)} |
| РџСѓР»СЊСЃ | {heartLine} |
| РќР°РїСЂСѓРіР° | {somatic.Strain:F2} |
| РЎРїРѕРєС–Р№ | {somatic.Calm:F2} |
| Р РµР°РєС†С–СЏ | {DashboardRegulationLabel(selfReg.Reaction)} |
| РЎР°РјРѕСЂРµРіСѓР»СЏС†С–СЏ | {DashboardRegulationLabel(selfReg.Regulation)} |
| Р’РЅСѓС‚СЂС–С€РЅСЏ РґСѓРјРєР° | {privateThought} |
| РџРѕРІРµРґС–РЅРєР° | {behavior} |

## Р©Рѕ Р·РјС–РЅРёР»РѕСЃСЊ

- РџРѕРїРµСЂРµРґРЅС–Р№ СЃРёРЅС…СЂРѕРЅС–Р·РѕРІР°РЅРёР№ СЃС‚Р°РЅ: {previousEmotion}
- РџРѕС‚РѕС‡РЅРёР№ СЃС‚Р°РЅ: {curStr}
- Р РёР·РёРє: {riskLine}

## РќР°СЃС‚СѓРїРЅР° РґС–СЏ

- РџСЂС–РѕСЂРёС‚РµС‚: {priority}
- РџРµСЂРµРґ РІС–РґРїРѕРІС–РґРґСЋ: РїРµСЂРµРІС–СЂРёС‚Рё Obsidian-РєРѕРЅС‚РµРєСЃС‚, РїРѕС‚С–Рј РІС–РґРїРѕРІС–РґР°С‚Рё РїРѕ СЃСѓС‚С–.
- РџР°Рј'СЏС‚СЊ: РІР°Р¶Р»РёРІС– С„Р°РєС‚Рё РЅРµ С‚СЂРёРјР°С‚Рё РІ РіРѕР»РѕРІС– СЏРє РґРµРєРѕСЂР°С‚РёРІРЅРёР№ РјРѕС‚Р»РѕС…, Р° Р·Р°РїРёСЃСѓРІР°С‚Рё Сѓ РїСЂР°РІРёР»СЊРЅС– РЅРѕС‚Р°С‚РєРё.

## Р—РґРѕСЂРѕРІ'СЏ С‚РІРѕСЂС†СЏ

| РџРѕРєР°Р·РЅРёРє | РћС†С–РЅРєР° |
|----------|--------|
{healthBlock}

## РћСЃС‚Р°РЅРЅС– РґСѓРјРєРё

{string.Join("\n", thoughtLines)}

## РџРѕСЃРёР»Р°РЅРЅСЏ

- [[Daily/{now:yyyy-MM-dd}]]
- [[Kokonoe/Р”РѕСЃСЊС”]]
- [[Kokonoe/Logs/Live Core]]
- [[Kokonoe/Somatic Events]]
- [[Kokonoe/Memory/Review]]
- [[Kokonoe/Memory/Quality]]
- [[Kokonoe/Tasks Queue]]
- [[Kokonoe/Architecture/Health]]
- [[Kokonoe/Architecture/Language Policy]]
""";
                obsidian.WriteNote("Kokonoe/Dashboard.md", dashContent);

                // в”Ђв”Ђ Append to daily note (throttled or on emotion change) в”Ђв”Ђ
                if (forceDaily)
                {
                    var emoji = emotion.Current.ToString() switch
                    {
                        "Calm"       => "рџ”µ",
                        "Curious"    => "рџџЈ",
                        "Warm"       => "рџ©·",
                        "Playful"    => "рџџЎ",
                        "Concerned"  => "рџџ ",
                        "Protective" => "рџ”ґ",
                        "Irritated"  => "рџ”ґ",
                        "Distant"    => "вљ«",
                        "Tender"     => "рџ©·",
                        "Focused"    => "рџ”µ",
                        "Proud"      => "рџЊџ",
                        "Melancholy" => "рџ©¶",
                        "Excited"    => "рџџў",
                        _            => "вљЄ"
                    };
                    var line = $"\n> [{now:HH:mm}] {emoji} **Р”Р°С€Р±РѕСЂРґ** В· {curStr} ({connPct}%) В· РїРѕРІС–РґРѕРјР»РµРЅСЊ: {todayMsgs} В· С„Р°РєС‚С–РІ: {factCount} В· С‚С–Р»Рѕ: {DashboardSomaticLabel(somatic.State)}";
                    obsidian.AppendToDailyNote(line);
                    _dashLastEmotionSynced = curStr;
                }

                _dashLastObsidianSync = now;
            }
            catch { /* vault РЅРµРґРѕСЃС‚СѓРїРЅРёР№ вЂ” РЅРµ РєСЂРёС‚РёС‡РЅРѕ */ }
        }

        // в”Ђв”Ђ Dev section в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

                string[] dayLabels = { "РџРЅ", "Р’С‚", "РЎСЂ", "Р§С‚", "РџС‚", "РЎР±", "РќРґ" };
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
                    ("РљРѕРґРёРЅРі",      coding,   MediaColor.FromRgb(155, 77, 202)),
                    ("Р”РµР±Р°Рі",       debug,    MediaColor.FromRgb(255, 51, 102)),
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
                    dot.ToolTip = d == sprintDay ? "РЎСЊРѕРіРѕРґРЅС–" : $"Р”РµРЅСЊ {d}: {actualPts[d]:F0} pts";
                    Canvas.SetLeft(dot, x - 2.5);
                    Canvas.SetTop(dot, y - 2.5);
                    DashSprintBurndownCanvas.Children.Add(dot);
                }
                DashSprintBurndownCanvas.Children.Add(actualLine);

                for (int d = 0; d <= sprintLen; d += 2)
                    DashSprintAxisCanvas.Children.Add(DashLabel($"Рґ{d}", d / (double)sprintLen * DashW(DashSprintAxisCanvas, w), 2, 7,
                        MediaColor.FromRgb(74, 61, 92)));

                DashSprintAxisCanvas.Children.Add(DashLabel($"// РґРµРЅСЊ {sprintDay}, С„Р°РєС‚", 0, 9, 7, MediaColor.FromRgb(155, 77, 202)));
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

                // вЂ” KPI 1: РџРѕРІС–РґРѕРјР»РµРЅСЊ/С‚РёР¶РґРµРЅСЊ (real chat throughput) вЂ”
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
                DashDevKpiVelocityDelta.Text = msgDelta == 0 ? "вЂ”" : (msgDelta > 0 ? $"+{msgDelta}%" : $"{msgDelta}%");
                DashDevKpiVelocityDelta.Foreground = msgDelta >= 0
                    ? new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(0, 255, 136))
                    : new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(255, 51, 102));

                // вЂ” KPI 2: РџС–Рє Р°РєС‚РёРІРЅРѕСЃС‚С– (real peak hour from pattern engine) вЂ”
                var hourly  = patterns.GetHourlyActivity();
                var peakHr  = 0; var peakV = 0;
                for (int h = 0; h < 24; h++) if (hourly[h] > peakV) { peakV = hourly[h]; peakHr = h; }
                DashDevKpiFocus.Text      = peakV == 0 ? "вЂ”" : $"{peakHr:00}";
                DashDevKpiFocusDelta.Text = peakV == 0 ? "РЅРµРјР°С” РґР°РЅРёС…" : $":00 ({peakV})";
                var focusSpark = hourly.Skip(Math.Max(0, peakHr - 3)).Take(7)
                                       .Select(v => (double)v).ToArray();
                if (focusSpark.Length < 7) focusSpark = Enumerable.Repeat(0.0, 7).ToArray();

                // вЂ” KPI 3: Confirmed facts % (real, from KokoMemory) вЂ”
                var allFacts   = memory.Facts;
                var confirmed  = allFacts.Count(f => f.ConfirmCount > 1);
                var coveragePct = allFacts.Count == 0 ? 0 : (int)(confirmed * 100.0 / allFacts.Count);
                DashDevKpiCoverage.Text      = $"{coveragePct}";
                DashDevKpiCoverageDelta.Text = $"% ({confirmed}/{allFacts.Count})";

                // вЂ” KPI 4: Insights count (real, from pattern engine) вЂ”
                var insights = patterns.Patterns.Count;
                DashDevKpiBugs.Text      = insights.ToString();
                DashDevKpiBugsDelta.Text = "РїР°С‚РµСЂРЅС–РІ";

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

        // в”Ђв”Ђ Sparkline в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

        // в”Ђв”Ђ Data helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

        // в”Ђв”Ђ Pie slice в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

        // в”Ђв”Ђ Drawing primitives в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

        // в”Ђв”Ђ Emotion color map в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                ? "РќРѕС‚Р°С‚РѕРє РЅРµ Р·РЅР°Р№РґРµРЅРѕ."
                : string.Join("\n", notes.Take(30));
        }

        private void McpSearch_Click(object sender, RoutedEventArgs e)
        {
            var q = Microsoft.VisualBasic.Interaction.InputBox("РџРѕС€СѓРєРѕРІРёР№ Р·Р°РїРёС‚:", "Search Vault", "");
            if (string.IsNullOrWhiteSpace(q)) return;
            var results = _obsidian.SearchNotes(q, 15);
            McpOutput.Text = results.Count == 0
                ? "РќС–С‡РѕРіРѕ РЅРµ Р·РЅР°Р№РґРµРЅРѕ."
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
            var title = Microsoft.VisualBasic.Interaction.InputBox("РќР°Р·РІР° РЅРѕС‚Р°С‚РєРё:", "New Note", "");
            if (string.IsNullOrWhiteSpace(title)) return;
            var path = _obsidian.CreateNote(title);
            McpOutput.Text = $"РЎС‚РІРѕСЂРµРЅРѕ: {path}";
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // TELEGRAM
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private void InitBrain()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;

                // РџСЂРё СЃРїРѕРЅС‚Р°РЅРЅРѕРјСѓ РїРѕРІС–РґРѕРјР»РµРЅРЅС– вЂ” РїРѕРєР°Р·Р°С‚Рё РІ UI С‡Р°С‚С–
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

                // РџРµСЂРµРґР°С‚Рё TG Р±РѕС‚
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
                    TgStatusLabel.Text = " В· РІРёРјРєРЅРµРЅРѕ";
                    TgStatusDot.Tag = "offline";
                    return;
                }

                if (string.IsNullOrWhiteSpace(s.TelegramToken) || s.TelegramChatId <= 0)
                {
                    TgStatusLabel.Text = " В· РЅРµ РЅР°Р»Р°С€С‚РѕРІР°РЅРѕ";
                    TgStatusDot.Tag = "offline";
                    System.Diagnostics.Debug.WriteLine("[Telegram] skipped: token or allowed chat id is missing");
                    return;
                }

                _tgBot = new TelegramBotClient(s.TelegramToken);

                _tgBot.StartReceiving(
                    HandleTgUpdate, HandleTgError,
                    new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery } },
                    _tgCts.Token);

                // Mini App HTTP server (РґР»СЏ TG Web App)
                // if (s.MiniAppEnabled)
                {
                    try
                    {
                        // _miniApp ??= new KokonoeAssistant.Services.MiniAppServer(
                            // s.MiniAppPort, s.TelegramToken, s.TelegramChatId);
                        // // // _miniApp.Start();
                        System.Diagnostics.Debug.WriteLine($"[MiniApp] started on port {s.MiniAppPort}");

                        // РђРІС‚Рѕ-С‚СѓРЅРµР»СЊ cloudflared (РІРёРґР°С” РїСѓР±Р»С–С‡РЅРёР№ HTTPS URL)
                        // _tunnel ??= new KokonoeAssistant.Services.TunnelManager(s.MiniAppPort);
                        // _tunnel.Log += msg => System.Diagnostics.Debug.WriteLine(msg);
                        // _tunnel.UrlChanged += url =>
                        {
                            // Р—Р±РµСЂРµРіС‚Рё URL Сѓ settings, С‰РѕР± TG-РјРµРЅСЋ РѕРґСЂР°Р·Сѓ Р№РѕРіРѕ РїС–РґС…РѕРїРёР»Рѕ
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

                TgStatusLabel.Text    = " В· РїС–РґРєР»СЋС‡РµРЅРѕ вњ“";
                TgStatusDot.Tag       = "online";
                TgStatusDot.Background = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"];
            }
            catch (Exception ex)
            {
                TgStatusLabel.Text = $" В· РїРѕРјРёР»РєР°: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[Telegram] Init failed: {ex}");
            }
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // TELEGRAM вЂ” HANDLER
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

                // в”Ђв”Ђ Photo message в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
                if (msg.Photo != null && msg.Photo.Length > 0)
                {
                    var caption = msg.Caption ?? "";
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[{from}]: [С„РѕС‚Рѕ] {caption}");
                        TgScroll.ScrollToBottom();
                    });

                    try
                    {
                        // Р‘РµСЂРµРјРѕ РЅР°Р№Р±С–Р»СЊС€РёР№ СЂРѕР·РјС–СЂ
                        var bestPhoto = msg.Photo[^1];
                        var fileInfo  = await bot.GetFile(bestPhoto.FileId, ct);
                        using var ms  = new System.IO.MemoryStream();
                        await bot.DownloadFile(fileInfo.FilePath!, ms, ct);
                        var imgBytes = CompressImageForLlm(ms.ToArray());

                        var prompt = string.IsNullOrWhiteSpace(caption)
                            ? "Р©Рѕ РЅР° С†СЊРѕРјСѓ Р·РѕР±СЂР°Р¶РµРЅРЅС–? РџСЂРѕРєРѕРјРµРЅС‚СѓР№ РєРѕСЂРѕС‚РєРѕ."
                            : caption;

                        var imgReply = await _llm.SendAsync(prompt, imgBytes, "image/jpeg", null, ct);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            _tgMessages.Add($"[Kokonoe]: {imgReply}");
                            TgScroll.ScrollToBottom();
                            AddMessageBubble(new ChatMessageVm { Role = "user",      Content = $"[TG: {from}] рџ“· {caption}" });
                            AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = imgReply });
                        });
                        try { await bot.SendMessage(chatId, imgReply, cancellationToken: ct); } catch { }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TG] Photo error: {ex.Message}");
                        try { await bot.SendMessage(chatId, "РќРµ Р·РјРѕРіР»Р° РїСЂРѕС‡РёС‚Р°С‚Рё С„РѕС‚Рѕ.", cancellationToken: ct); } catch { }
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

                // в”Ђв”Ђ Commands в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                    await bot.SendMessage(chatId, "РџРёС€Рё РЅРѕС‚Р°С‚РєСѓ вЂ” Р·Р±РµСЂРµР¶Сѓ.", cancellationToken: ct);
                    SetTgAwaiting(chatId, TgAwaitingMode.Note);
                    return;
                }

                // в”Ђв”Ђ Awaiting states в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
                var awaiting = ConsumeTgAwaiting(chatId);
                if (awaiting == TgAwaitingMode.Note)
                {
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(text); } catch { }
                    try { ServiceContainer.BrainEngine?.Memory?.RecordEpisode(text, "neutral", 0.6f); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] TG note memory: {ex.Message}"); }
                    try { _obsidian.AppendToDailyNote($"\n> рџ“ќ [TG {DateTime.Now:HH:mm}] {text}"); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] TG daily note: {ex.Message}"); }
                    await bot.SendMessage(chatId, "Р—Р°РїРёСЃР°Р»Р°.", cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Command)
                {
                    await bot.SendMessage(chatId, "вЏі Р’РёРєРѕРЅСѓСЋ...", cancellationToken: ct);
                    var cmdOutput = await ServiceContainer.PcControl.RunCommandAsync(text);
                    await bot.SendMessage(chatId, $"```\n{cmdOutput}\n```",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                        cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Open)
                {
                    var (openOk, openMsg) = ServiceContainer.PcControl.OpenApp(text);
                    await bot.SendMessage(chatId, openOk ? $"вњ… {openMsg}" : $"вќЊ {openMsg}", cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Kill)
                {
                    var (killOk, killMsg) = ServiceContainer.PcControl.KillProcess(text);
                    await bot.SendMessage(chatId, killOk ? $"вњ… {killMsg}" : $"вќЊ {killMsg}", cancellationToken: ct);
                    return;
                }

                // в”Ђв”Ђ Regular chat в†’ LLM в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
                if (TryHandleDirectControlCommand(text, out var controlReply))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe]: {controlReply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = controlReply });
                    });
                    try { await bot.SendMessage(chatId, controlReply, cancellationToken: ct); } catch { }
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage { Content = controlReply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now });
                    }
                    catch { }
                    return;
                }

                if (TryHandleDirectObsidianCommand(text, out var obsidianReply))
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
                var reply = await _llm.SendAsync(tgPrompt, null, "image/jpeg", tgContext, ct);
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
                    await bot.SendMessage(chatId, "РџРёС€Рё вЂ” Р·Р±РµСЂРµР¶Сѓ.", cancellationToken: ct);
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

                // в”Ђв”Ђ PC Control в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                                        await bot.SendMessage(chatId, "рџ”‡ РўРёС€Р°.", cancellationToken: ct); break;
                case "pc_lock":         ServiceContainer.PcControl.LockScreen();
                                        await bot.SendMessage(chatId, "рџ”’ Р—Р°Р±Р»РѕРєРѕРІР°РЅРѕ.", cancellationToken: ct); break;
                case "pc_sleep":        await bot.SendMessage(chatId, "рџ’¤ Р—Р°СЃРёРЅР°СЋ...", cancellationToken: ct);
                                        ServiceContainer.PcControl.Sleep();            break;
                case "pc_mon_off":      ServiceContainer.PcControl.TurnOffMonitor();
                                        await bot.SendMessage(chatId, "рџ–Ґ РњРѕРЅС–С‚РѕСЂ РІРёРјРєРЅРµРЅРѕ.", cancellationToken: ct); break;
                case "pc_shutdown_ask": await TgConfirmAction(bot, chatId, "РЎРїСЂР°РІРґС– РІРёРјРєРЅСѓС‚Рё РџРљ?", "pc_shutdown_ok", ct); break;
                case "pc_restart_ask":  await TgConfirmAction(bot, chatId, "РЎРїСЂР°РІРґС– РїРµСЂРµР·Р°РІР°РЅС‚Р°Р¶РёС‚Рё?", "pc_restart_ok", ct); break;
                case "pc_shutdown_ok":  await bot.SendMessage(chatId, "в›” Р’РёРјРёРєР°СЋ С‡РµСЂРµР· 10 СЃРµРєСѓРЅРґ.", cancellationToken: ct);
                                        ServiceContainer.PcControl.Shutdown(10);       break;
                case "pc_restart_ok":   await bot.SendMessage(chatId, "рџ”„ Р РµСЃС‚Р°СЂС‚ С‡РµСЂРµР· 10 СЃРµРєСѓРЅРґ.", cancellationToken: ct);
                                        ServiceContainer.PcControl.Restart(10);        break;
                case "pc_cmd":
                    await bot.SendMessage(chatId, "PowerShell Р· Telegram РІРёРјРєРЅРµРЅРѕ. РЇ РЅРµ Р·Р°Р»РёС€Р°СЋ РІС–РґРґР°Р»РµРЅСѓ РєРѕРЅСЃРѕР»СЊ РїСЂРѕСЃС‚Рѕ С‚РѕРјСѓ, С‰Рѕ С…С‚РѕСЃСЊ РЅР°Р·РІР°РІ С†Рµ С„С–С‡РµСЋ.", cancellationToken: ct);
                    break;
                case "pc_open":
                    SetTgAwaiting(chatId, TgAwaitingMode.Open);
                    await bot.SendMessage(chatId, "Р©Рѕ РІС–РґРєСЂРёС‚Рё? (chrome, code, explorer, spotify, notepad, Р°Р±Рѕ РїРѕРІРЅРёР№ С€Р»СЏС…):", cancellationToken: ct);
                    break;
                case "pc_kill":
                    SetTgAwaiting(chatId, TgAwaitingMode.Kill);
                    await bot.SendMessage(chatId, "РќР°Р·РІР° РїСЂРѕС†РµСЃСѓ Р°Р±Рѕ PID РґР»СЏ Р·Р°РІРµСЂС€РµРЅРЅСЏ:", cancellationToken: ct);
                    break;
            }
        }

        // в”Ђв”Ђ Menu builders в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private async Task TgSendMainMenu(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var emo  = ServiceContainer.EmotionEngine.Current.ToString();
            var bond = ServiceContainer.EmotionEngine.Bond.ToString();
            var header = $"в—€ Kokonoe В· {emo} В· {bond}";

            var rows = new List<Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>
            {
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџ“Љ РЎС‚Р°С‚СѓСЃ",   "status"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("вќ¤пёЏ РќР°СЃС‚СЂС–Р№",  "mood"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџЋЇ Р¦С–Р»С–",    "goals"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџ’Є Р—РІРёС‡РєРё",  "habits"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџ“ќ РќРѕС‚Р°С‚РєР°", "note"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџ–Ґ РџРљ", "pc"),
                },
            };

            // Mini App РєРЅРѕРїРєР° вЂ” Р»РёС€Рµ СЏРєС‰Рѕ РЅР°Р»Р°С€С‚РѕРІР°РЅРѕ РїСѓР±Р»С–С‡РЅРёР№ HTTPS URL
            var settings = AppSettings.Load();
            if (!string.IsNullOrWhiteSpace(settings.MiniAppPublicUrl))
            {
                rows.Add(new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithWebApp(
                        "рџЊђ РџР°РЅРµР»СЊ Kokonoe",
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

            var mood  = brain?.State?.PersonalityDailyMood ?? "вЂ”";
            var score = brain?.State?.MoodScore ?? 0.5f;
            var monologue = brain?.State?.InnerMonologues?.LastOrDefault() ?? "вЂ”";
            var selfQ = brain?.State?.SelfQuestions?.LastOrDefault();
            var conn  = emo.ConnectionScore;
            var bond  = emo.Bond;
            var secondary = emo.Secondary.HasValue ? $" + {emo.Secondary}" : "";

            var bar = new string('#', (int)(conn * 10)) + new string('-', 10 - (int)(conn * 10));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("в—€ *Kokonoe вЂ” Р·Р°СЂР°Р·*");
            sb.AppendLine();
            sb.AppendLine($"*Р•РјРѕС†С–СЏ:* {emo.Current}{secondary}");
            sb.AppendLine($"*РќР°СЃС‚СЂС–Р№:* {mood} ({score:P0})");
            sb.AppendLine($"*Р‘Р»РёР·СЊРєС–СЃС‚СЊ:* [{bar}] {bond}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(monologue))
                sb.AppendLine($"*Р”СѓРјРєР°:* _{monologue}_");
            if (!string.IsNullOrEmpty(selfQ))
                sb.AppendLine($"*РџРёС‚Р°РЅРЅСЏ РґРѕ СЃРµР±Рµ:* _{selfQ}_");

            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("в†ђ РњРµРЅСЋ", "menu"));

            await bot.SendMessage(chatId, sb.ToString(),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgSendGoals(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var goals = ServiceContainer.GoalService?.GetActiveGoals() ?? new List<Models.Goal>();

            var sb = new System.Text.StringBuilder("рџЋЇ *РђРєС‚РёРІРЅС– С†С–Р»С–*\n\n");
            if (!goals.Any())
                sb.AppendLine("РќРµРјР°С” Р°РєС‚РёРІРЅРёС… С†С–Р»РµР№.");

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
                        $"вњ… {g.Title[..Math.Min(20, g.Title.Length)]}", $"goal_done_{g.Id}")
                });
            }

            buttons.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("вћ• РќРѕРІР° С†С–Р»СЊ", "goals_add"),
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("в†ђ РњРµРЅСЋ",     "menu"),
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

            var sb = new System.Text.StringBuilder("рџ’Є *Р—РІРёС‡РєРё СЃСЊРѕРіРѕРґРЅС–*\n\n");
            if (!habits.Any())
                sb.AppendLine("РќРµРјР°С” Р·РІРёС‡РѕРє.");

            var buttons = new List<Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton[]>();

            foreach (var h in habits.Take(8))
            {
                var done  = h.CheckIns?.Any(c => c.Date.Date == today && c.Completed) == true;
                var emoji = done ? "вњ…" : "в¬њ";
                sb.AppendLine($"{emoji} {h.Name}");

                if (!done)
                    buttons.Add(new[]
                    {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(
                            $"вњ… {h.Name[..Math.Min(22, h.Name.Length)]}", $"habit_done_{h.Id}")
                    });
            }

            buttons.Add(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("в†ђ РњРµРЅСЋ", "menu")
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
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџЉ Р”РѕР±СЂРµ",    "mood_good"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџђ РќРѕСЂРјР°Р»СЊРЅРѕ","mood_ok"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџ” РџРѕРіР°РЅРѕ",   "mood_bad"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџґ Р’С‚РѕРјР»РµРЅРёР№","mood_tired"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџ¤ РЎС‚СЂРµСЃ",    "mood_stressed"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("рџ”Ґ РџСЂРѕРґСѓРєС‚РёРІРЅРёР№","mood_productive"),
                },
                new[] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("в†ђ РњРµРЅСЋ",     "menu"),
                },
            });

            await bot.SendMessage(chatId, "РЇРє С‚Рё Р·Р°СЂР°Р·?", replyMarkup: kb, cancellationToken: ct);
        }

        // в”Ђв”Ђ PC Control menus в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private async Task TgSendPcMenu(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var vol = ServiceContainer.PcControl.GetVolume();
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] {
                    IKB("рџ“ё РЎРєСЂС–РЅС€РѕС‚",   "pc_screenshot"),
                    IKB("рџ’» РЎРёСЃС‚РµРјРЅР° С–РЅС„Рѕ", "pc_sysinfo"),
                },
                new[] {
                    IKB("рџ“‹ РџСЂРѕС†РµСЃРё",    "pc_procs"),
                    IKB($"рџ”Љ Р“СѓС‡РЅС–СЃС‚СЊ {vol}%", "pc_vol_menu"),
                },
                new[] {
                    IKB("рџ“‚ Р’С–РґРєСЂРёС‚Рё",   "pc_open"),
                    IKB("вљЎ РљРѕРјР°РЅРґР° PS", "pc_cmd"),
                },
                new[] {
                    IKB("рџ’Ђ Kill РїСЂРѕС†РµСЃ","pc_kill"),
                    IKB("рџ–Ґ РњРѕРЅС–С‚РѕСЂ РІРёРјРє","pc_mon_off"),
                },
                new[] {
                    IKB("рџ”’ Р—Р°Р±Р»РѕРєСѓРІР°С‚Рё","pc_lock"),
                    IKB("рџ’¤ РЎРѕРЅ",        "pc_sleep"),
                },
                new[] {
                    IKB("в›” Р’РёРјРєРЅСѓС‚Рё",   "pc_shutdown_ask"),
                    IKB("рџ”„ Р РµСЃС‚Р°СЂС‚",    "pc_restart_ask"),
                },
                new[] { IKB("в†ђ РњРµРЅСЋ", "menu") },
            });
            await bot.SendMessage(chatId, "рџ–Ґ *PC Control*",
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
                    caption: $"рџ–Ґ {DateTime.Now:HH:mm:ss}",
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await bot.SendMessage(chatId, $"вќЊ РЎРєСЂС–РЅС€РѕС‚ РЅРµ РІРёР№С€РѕРІ: {ex.Message}", cancellationToken: ct);
            }
        }

        private async Task TgSendSysInfo(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var info = await Task.Run(() => ServiceContainer.PcControl.GetSystemInfo());
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("рџ’» *РЎРёСЃС‚РµРјРЅР° С–РЅС„РѕСЂРјР°С†С–СЏ*\n");
            sb.AppendLine($"*РњР°С€РёРЅР°:* {info.MachineName} ({info.UserName})");
            sb.AppendLine($"*РћРЎ:* {info.OsVersion}");
            sb.AppendLine($"*РђРїС‚Р°Р№Рј:* {info.Uptime.Days}Рґ {info.Uptime.Hours}Рі {info.Uptime.Minutes}С…РІ");
            sb.AppendLine($"*RAM:* {info.RamUsedGb:F1} / {info.RamTotalGb:F1} GB");
            sb.AppendLine($"*Р“СѓС‡РЅС–СЃС‚СЊ:* {info.VolumePercent}%\n");
            sb.AppendLine("*Р”РёСЃРєРё:*");
            foreach (var d in info.Drives)
            {
                var used = d.TotalGb - d.FreeGb;
                var pct  = d.TotalGb > 0 ? (int)(used / d.TotalGb * 10) : 0;
                var bar  = new string('#', pct) + new string('-', 10 - pct);
                sb.AppendLine($"`{d.Name}` [{bar}] {used:F0}/{d.TotalGb:F0} GB");
            }
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                IKB("в†ђ РџРљ", "pc"));
            await bot.SendMessage(chatId, sb.ToString(),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgSendProcesses(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var list = await Task.Run(() => ServiceContainer.PcControl.GetTopProcesses());
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] { IKB("рџ’Ђ Kill РїСЂРѕС†РµСЃ", "pc_kill"), IKB("в†ђ РџРљ", "pc") },
            });
            await bot.SendMessage(chatId, $"рџ“‹ *РўРѕРї РїСЂРѕС†РµСЃС–РІ (RAM)*\n```\n{list}\n```",
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
                    IKB("рџ”‰ -10%", "pc_vol_down"),
                    IKB($"рџ”Љ {vol}%", "pc_sysinfo"),
                    IKB("рџ”Љ +10%", "pc_vol_up"),
                },
                new[] {
                    IKB("рџ”‡ РўРёС€Р°", "pc_vol_mute"),
                    IKB("в†ђ РџРљ",   "pc"),
                },
            });
            await bot.SendMessage(chatId, $"рџ”Љ *Р“СѓС‡РЅС–СЃС‚СЊ: {vol}%*\n`[{bar}]`",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgConfirmAction(ITelegramBotClient bot, long chatId, string question, string confirmData, CancellationToken ct)
        {
            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[] {
                    IKB("вњ… РўР°Рє", confirmData),
                    IKB("вќЊ РќС–",  "pc"),
                },
            });
            await bot.SendMessage(chatId, question, replyMarkup: kb, cancellationToken: ct);
        }

        // Shorthand РґР»СЏ InlineKeyboardButton
        private static Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton IKB(string text, string data) =>
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData(text, data);

        private async Task TgHandleMood(ITelegramBotClient bot, long chatId, string mood, CancellationToken ct)
        {
            var moodMap = new Dictionary<string, (string Label, string Tone, float Score)>
            {
                ["good"]       = ("рџЉ Р”РѕР±СЂРµ",       "happy",   0.8f),
                ["ok"]         = ("рџђ РќРѕСЂРјР°Р»СЊРЅРѕ",   "neutral", 0.5f),
                ["bad"]        = ("рџ” РџРѕРіР°РЅРѕ",       "sad",     0.2f),
                ["tired"]      = ("рџґ Р’С‚РѕРјР»РµРЅРёР№",   "tired",   0.3f),
                ["stressed"]   = ("рџ¤ РЎС‚СЂРµСЃ",       "stressed",0.25f),
                ["productive"] = ("рџ”Ґ РџСЂРѕРґСѓРєС‚РёРІРЅРёР№","excited", 0.85f),
            };

            if (!moodMap.TryGetValue(mood, out var m)) return;

            // Р—Р±РµСЂРµРіС‚Рё РІ health + emotion
            try { ServiceContainer.BrainEngine?.Memory?.RecordEpisode($"РЅР°СЃС‚СЂС–Р№: {m.Label}", m.Tone, m.Score); } catch { }
            try { ServiceContainer.EmotionEngine.UpdateFromUserTone(m.Tone, m.Score); } catch { }
            try { _obsidian.AppendToDailyNote($"\n> вќ¤пёЏ [{DateTime.Now:HH:mm}] РќР°СЃС‚СЂС–Р№: {m.Label}"); } catch { }

            // Kokonoe РєРѕСЂРѕС‚РєРѕ СЂРµР°РіСѓС”
            var prompt = $"Р’С–РЅ РЅР°РїРёСЃР°РІ С‰Рѕ РїРѕС‡СѓРІР°С”С‚СЊСЃСЏ: {m.Label}. РћРґРЅРµ РєРѕСЂРѕС‚РєРµ СЂРµС‡РµРЅРЅСЏ РІС–Рґ Kokonoe вЂ” РїСЂРёСЂРѕРґРЅСЊРѕ, Р±РµР· Р·Р°Р№РІРёС… СЃР»С–РІ.";
            var reply  = await _llm.SendAsync(prompt, null, "image/jpeg", null, ct);

            var kb = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("в†ђ РњРµРЅСЋ", "menu"));

            await bot.SendMessage(chatId, reply, replyMarkup: kb, cancellationToken: ct);
        }

        private async Task TgMarkHabit(ITelegramBotClient bot, long chatId, string habitId, CancellationToken ct)
        {
            try
            {
                await ServiceContainer.HabitService!.RecordCheckInAsync(habitId, true);
                var h = ServiceContainer.HabitService.GetHabit(habitId);
                await bot.SendMessage(chatId, $"вњ… *{h?.Name}* вЂ” РІРёРєРѕРЅР°РЅРѕ!",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: ct);
            }
            catch { await bot.SendMessage(chatId, "Р©РѕСЃСЊ РїС–С€Р»Рѕ РЅРµ С‚Р°Рє.", cancellationToken: ct); }
        }

        private async Task TgCompleteGoal(ITelegramBotClient bot, long chatId, string goalId, CancellationToken ct)
        {
            try
            {
                var g = ServiceContainer.GoalService!.GetGoal(goalId);
                await ServiceContainer.GoalService.SetProgressAsync(goalId, 100);
                await bot.SendMessage(chatId, $"рџЋ‰ *{g?.Title}* вЂ” Р·Р°РІРµСЂС€РµРЅРѕ!",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: ct);
            }
            catch { await bot.SendMessage(chatId, "Р¦С–Р»СЊ РЅРµ Р·РЅР°Р№РґРµРЅР°.", cancellationToken: ct); }
        }

        private async Task TgPromptGoalAdd(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            await bot.SendMessage(chatId,
                "РќР°РїРёС€Рё С†С–Р»СЊ Сѓ С„РѕСЂРјР°С‚С–:\n`РќР°Р·РІР° | РєР°С‚РµРіРѕСЂС–СЏ | РїСЂС–РѕСЂРёС‚РµС‚ 1-5`\n\nРљР°С‚РµРіРѕСЂС–С—: work, personal, health, learning",
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
            if (s.TelegramChatId <= 0) { WMsgBox.Show("Telegram Chat ID РЅРµ РІРєР°Р·Р°РЅРѕ."); return; }

            try
            {
                await _tgBot.SendMessage(s.TelegramChatId, text);
                _tgMessages.Add($"[You в†’ TG]: {text}");
                TgInput.Clear();
                TgScroll.ScrollToBottom();
            }
            catch (Exception ex) { WMsgBox.Show(ex.Message); }
        }

        private void TgInput_KeyDown(object sender, WKeyArgs e)
        {
            if (e.Key == Key.Enter) TgSend_Click(sender, e);
        }

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // TELEGRAM USER CLIENT (MTProto)
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        /// <summary>
        /// Р§РёСЃС‚РёС‚СЊ РІС–РґРїРѕРІС–РґСЊ РІС–Рґ СЂРѕР»РїР»РµР№-СЂРµРјР°СЂРѕРє РїРµСЂРµРґ РІС–РґРїСЂР°РІРєРѕСЋ РІ Telegram.
        /// Р’РёРґР°Р»СЏС” *(РґС–СЏ)*, (РѕРїРёСЃ), *РєСѓСЂСЃРёРІ-РґС–С—* С‚РѕС‰Рѕ.
        /// </summary>
        private static string CleanTgReply(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Р’РёРґР°Р»СЏС”РјРѕ *(Р±СѓРґСЊ-СЏРєРёР№ С‚РµРєСЃС‚ РґС–С—)* вЂ” СЂРµРјР°СЂРєРё РІ Р·С–СЂРѕС‡РєР°С…
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\*\([^)]*\)\*", "", System.Text.RegularExpressions.RegexOptions.Singleline);

            // Р’РёРґР°Р»СЏС”РјРѕ *(С‚РµРєСЃС‚ Р±РµР· РґСѓР¶РѕРє)* вЂ” *РєСѓСЂСЃРёРІ-РґС–С—*
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\*[^*\n]{0,120}\*", "", System.Text.RegularExpressions.RegexOptions.Singleline);

            // Р’РёРґР°Р»СЏС”РјРѕ (С‚РµРєСЃС‚ РІ РґСѓР¶РєР°С… С‰Рѕ РІРёРіР»СЏРґР°С” СЏРє СЂРµРјР°СЂРєР°) вЂ” РїРѕС‡РёРЅР°С”С‚СЊСЃСЏ Р· РІРµР»РёРєРѕС—, РґРѕРІС€РёР№ Р·Р° 20 СЃРёРјРІРѕР»С–РІ
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\([Рђ-РЇР‡Р†Р„][^)]{20,}\)", "", System.Text.RegularExpressions.RegexOptions.Singleline);

            // РџСЂРёР±РёСЂР°С”РјРѕ Р·Р°Р№РІС– РїРѕСЂРѕР¶РЅС– СЂСЏРґРєРё
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
                await Dispatcher.InvokeAsync(() => _tgMessages.Add("[MTProto] РІРёРјРєРЅРµРЅРѕ РІ Settings"));
                return;
            }

            if (s.TgApiId == 0 || string.IsNullOrEmpty(s.TgApiHash))
            {
                await Dispatcher.InvokeAsync(() => _tgMessages.Add("[MTProto] РџРѕРјРёР»РєР°: api_id Р°Р±Рѕ api_hash РЅРµ Р·Р°РґР°РЅС– РІ Settings"));
                return;
            }

            // Dispose old client first вЂ” РІС–РЅ С‚СЂРёРјР°С” tg_session.dat РІС–РґРєСЂРёС‚РёРј
            var oldSvc = ServiceContainer.TelegramUser;
            if (oldSvc != null)
            {
                ServiceContainer.TelegramUser = null;
                try { oldSvc.Dispose(); } catch { }
                _tgUserCts.Cancel();
                _tgUserCts = new CancellationTokenSource();
                await Task.Delay(300); // РґР°С”РјРѕ WTelegramClient РІС–РґРїСѓСЃС‚РёС‚Рё С„Р°Р№Р»
            }

            try
            {
                var dataDir = System.IO.Path.Combine(
                    s.VaultPath, "kokonoe-data");

                var svc = new Services.TelegramUserService(
                    s.TgApiId, s.TgApiHash, s.TgPhone, dataDir, s.TgDmOnly, s.TgRespondToOutgoing);

                // Auth callback вЂ” Dispatcher.Invoke (СЃРёРЅС…СЂРѕРЅРЅРёР№) С‰РѕР± СѓРЅРёРєРЅСѓС‚Рё РґРµРґР»РѕРєСѓ
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
                    TgStatusLabel.Text = $" В· {status}";
                    if (status.StartsWith("вњ“"))
                        TgStatusDot.Background = (System.Windows.Media.SolidColorBrush)
                            System.Windows.Application.Current.Resources["AccentBase"];
                });

                svc.OnMessage += msg => _ = HandleTgUserMessageAsync(msg);

                ServiceContainer.TelegramUser = svc;

                await svc.ConnectAsync(_tgUserCts.Token);

                await Dispatcher.InvokeAsync(() =>
                {
                    _tgMessages.Add($"[MTProto] РџС–РґРєР»СЋС‡РµРЅРѕ СЏРє {svc.MySelf}");
                    TgScroll.ScrollToBottom();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TgUser] Init failed: {ex}");
                await Dispatcher.InvokeAsync(() =>
                {
                    TgStatusLabel.Text = $" В· user: РїРѕРјРёР»РєР° ({ex.Message[..Math.Min(60, ex.Message.Length)]})";
                    _tgMessages.Add($"[MTProto] РџРѕРјРёР»РєР°: {ex.Message}");
                });
            }
        }

        private async Task HandleTgUserMessageAsync(Services.TgIncomingMessage msg)
        {
            var s = AppSettings.Load();
            var svc = ServiceContainer.TelegramUser;
            if (svc == null) return;

            // РџРѕРєР°Р·СѓС”РјРѕ РІ UI
            var displayLine = $"[{msg.ChatName}] {msg.Sender}: {msg.Text}";
            await Dispatcher.InvokeAsync(() =>
            {
                _tgMessages.Add(displayLine);
                TgScroll.ScrollToBottom();
            });

            // РџСЂРѕСЃС‚РёР№ РїСЂРѕРјРїС‚ вЂ” SendTgAsync РјР°С” РІР»Р°СЃРЅРёР№ system prompt
            var direction = msg.IsOutgoing
                ? "РєРѕСЂРёСЃС‚СѓРІР°С‡ РЅР°РїРёСЃР°РІ С†Рµ Р·С– СЃРІРѕРіРѕ Telegram-Р°РєР°СѓРЅС‚Р°; С†Рµ СЂРµРїР»С–РєР° РґРѕ С‚РµР±Рµ"
                : "РІС…С–РґРЅРµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РІС–Рґ С–РЅС€РѕС— Р»СЋРґРёРЅРё";
            var prompt =
                $"Telegram ({direction})\n" +
                $"Р§Р°С‚: {msg.ChatName}\n" +
                $"Р’С–Рґ: {msg.Sender}\n" +
                $"РўРµРєСЃС‚: {msg.Text}\n\n" +
                "РќРµ С–РіРЅРѕСЂСѓР№ РєРѕСЂРѕС‚РєС– РІС–РґРїРѕРІС–РґС– С‚РёРїСѓ \"СѓРіСѓ\", \"С‚СѓС‚\", \"РѕРє\". " +
                "Р’С–РґРїРѕРІС–РґР°Р№ РєРѕСЂРѕС‚РєРѕ, РїСЂРёСЂРѕРґРЅРѕ, Р±РµР· СЃР»СѓР¶Р±РѕРІРёС… С„СЂР°Р· С– Р±РµР· РїРѕРІС‚РѕСЂСѓ РїРёС‚Р°РЅРЅСЏ.";

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

                if (TryHandleDirectControlCommand(msg.Text, out var controlReply))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe в†’ {msg.ChatName}]: {controlReply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG {msg.Sender}]: {msg.Text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = controlReply });
                    });
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = controlReply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch { }
                    await svc.SendAsync(msg.ChatId, controlReply, _tgUserCts.Token);
                    return;
                }

                var sharedContext = ServiceContainer.BrainEngine?.BuildUnifiedExternalContext("telegram") ?? "";
                var raw = await _llm.SendTgAsync(prompt, sharedContext, _tgUserCts.Token);
                if (string.IsNullOrWhiteSpace(raw)) return;
                var reply = CleanTgReply(raw);

                // РџРѕРєР°Р·СѓС”РјРѕ РІС–РґРїРѕРІС–РґСЊ РІ UI
                await Dispatcher.InvokeAsync(() =>
                {
                    _tgMessages.Add($"[Kokonoe в†’ {msg.ChatName}]: {reply}");
                    TgScroll.ScrollToBottom();
                    AddMessageBubble(new ChatMessageVm { Role = "user",      Content = $"[TG {msg.Sender}]: {msg.Text}" });
                    AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = reply });
                });

                // РќР°РґСЃРёР»Р°С”РјРѕ РІС–РґРїРѕРІС–РґСЊ РЅР°Р·Р°Рґ РІ Telegram
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // SETTINGS PANEL (inline slide-in)
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

        private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsPanel();

        // Р РѕР±РѕС‡РёР№ СЃРЅР°РїС€РѕС‚ РїРѕРєРё РїР°РЅРµР»СЊ РІС–РґРєСЂРёС‚Р° вЂ” С‰РѕР± РЅРµ РіСѓР±РёС‚Рё РЅРµР·Р±РµСЂРµР¶РµРЅС– РїСЂР°РІРєРё
        // РјРѕРґРµР»С–/РєР»СЋС‡Р° РїСЂРё РїРµСЂРµРєР»СЋС‡РµРЅРЅС– РїСЂРѕРІР°Р№РґРµСЂР° РІ UI.
        private AppSettings? _panelSettings;
        private string _panelLastProvider = "";

        // Ollama Cloud key-pool VM
        private readonly System.Collections.ObjectModel.ObservableCollection<OllamaKeyRowVM> _ollamaKeysVM = new();
        private System.Windows.Threading.DispatcherTimer? _poolRefreshTimer;

        public class OllamaKeyRowVM : System.ComponentModel.INotifyPropertyChanged
        {
            private string _name = "";
            private string _key  = "";
            private string _statusText = "вЂ”";
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

            // РџСЂРёРІ'СЏР·СѓС”РјРѕ ItemsControl РґРѕ VM-РєРѕР»РµРєС†С–С— (РѕРґРёРЅ СЂР°Р· вЂ” ItemsSource РЅРµ РјС–РЅСЏС”РјРѕ)
            if (SP_OllamaKeysList.ItemsSource == null)
                SP_OllamaKeysList.ItemsSource = _ollamaKeysVM;

            // LLM Provider
            SP_ProviderLmStudio.IsChecked    = s.LlmProvider.Equals("lmstudio", StringComparison.OrdinalIgnoreCase);
            SP_ProviderOllama.IsChecked      = s.LlmProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase);
            SP_ProviderOllamaCloud.IsChecked = s.LlmProvider.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase);
            SP_ProviderClaude.IsChecked      = s.LlmProvider.Equals("claude", StringComparison.OrdinalIgnoreCase);
            // РЇРєС‰Рѕ Сѓ settings.json С‰Рµ "lmstudio" СЏРє default, РЅС–С‡РѕРіРѕ РЅРµ РІРёР±СЂР°РЅРѕ вЂ” РїС–РґС…РѕРїРёРјРѕ LM Studio
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

            // (LLM-РїРѕР»СЏ РІР¶Рµ Р·Р°РІР°РЅС‚Р°Р¶РµРЅС– С‡РµСЂРµР· LoadLlmFieldsForProvider)

            // РџРѕРєР°Р·СѓС”РјРѕ overlay
            SettingsOverlay.Visibility = Visibility.Visible;

            // Slide-in Р°РЅС–РјР°С†С–СЏ
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
            if (_panelSettings == null) return; // РІРёРєР»РёРєР°РЅРѕ РґРѕ OpenSettingsPanel вЂ” С–РіРЅРѕСЂСѓС”РјРѕ

            // 1) Р—Р±РµСЂРµРіС‚Рё РїРѕС‚РѕС‡РЅРёР№ С‚РµРєСЃС‚ РїРѕР»С–РІ Сѓ in-memory snapshot РґР»СЏ РїРѕРїРµСЂРµРґРЅСЊРѕРіРѕ РїСЂРѕРІР°Р№РґРµСЂР°
            CommitLlmFieldsToSnapshot(_panelLastProvider);

            // 2) РџС–РґРІР°РЅС‚Р°Р¶РёС‚Рё РїРѕР»СЏ РґР»СЏ РЅРѕРІРѕРіРѕ
            var newProvider = CurrentSelectedProvider();
            LoadLlmFieldsForProvider(newProvider, _panelSettings);
            _panelLastProvider = newProvider;
        }

        private void LoadLlmFieldsForProvider(string provider, AppSettings s)
        {
            // Р—Р° Р·Р°РјРѕРІС‡СѓРІР°РЅРЅСЏРј вЂ” РїСѓР» С…РѕРІР°С”РјРѕ; РїРѕРєР°Р¶РµРјРѕ С‚С–Р»СЊРєРё РґР»СЏ ollama-cloud.
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
                    SP_UrlHint.Text   = "Р›РѕРєР°Р»СЊРЅР° Ollama. Р”РµС„РѕР»С‚: http://localhost:11434/v1/chat/completions";
                    SP_ModelBox.Text  = string.IsNullOrWhiteSpace(s.Model) ? "llama3.2" : s.Model;
                    SP_ModelHint.Text = "РќР°Р·РІР° Р»РѕРєР°Р»СЊРЅРѕС— РјРѕРґРµР»С– (РЅР°РїСЂ. llama3.2, qwen2.5, mistral). РњР°С” Р±СѓС‚Рё Р·Р°РїСѓС‰РµРЅР° РІ `ollama serve`.";
                    SP_VisionModelBox.Text = s.VisionModel;
                    break;

                case "ollama-cloud":
                    SP_UrlGroup.Visibility    = Visibility.Collapsed;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed; // single-key UI Р±С–Р»СЊС€Рµ РЅРµ РґР»СЏ Ollama Cloud
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
                    SP_ApiKeyHint.Text   = "РћС‚СЂРёРјР°Р№ РєР»СЋС‡ РЅР° https://console.anthropic.com/settings/keys.";
                    SP_ModelBox.Text     = string.IsNullOrWhiteSpace(s.ClaudeModel) ? "claude-sonnet-4-20250514" : s.ClaudeModel;
                    SP_ModelHint.Text    = "РќР°РїСЂ. claude-sonnet-4-20250514, claude-opus-4-20250514, claude-3-5-sonnet-20241022.";
                    SP_VisionModelBox.Text = s.VisionModel;
                    HighlightApiKey();
                    break;

                default: // lmstudio
                    SP_UrlGroup.Visibility    = Visibility.Visible;
                    SP_ApiKeyGroup.Visibility = Visibility.Collapsed;
                    SP_LmUrl.Text     = string.IsNullOrWhiteSpace(s.LmUrl) || s.LmUrl.Contains(":11434")
                                        ? "http://localhost:1234/v1/chat/completions" : s.LmUrl;
                    SP_UrlHint.Text   = "LM Studio. Р”РµС„РѕР»С‚: http://localhost:1234/v1/chat/completions";
                    SP_ModelBox.Text  = string.IsNullOrWhiteSpace(s.Model) ? "google/gemma-4-26b-a4b" : s.Model;
                    SP_ModelHint.Text = "РўРѕС‡РЅР° РЅР°Р·РІР° РјРѕРґРµР»С– Р· LM Studio (РІРєР»Р°РґРєР° Local Server в†’ Loaded Model).";
                    SP_VisionModelBox.Text = s.VisionModel;
                    break;
            }
        }

        // в”Ђв”Ђ Ollama Cloud key-pool helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        private void LoadOllamaKeysVM(AppSettings s)
        {
            _ollamaKeysVM.Clear();

            // РЇРєС‰Рѕ Сѓ settings С” РєР»СЋС‡С– вЂ” РїС–РґРІР°РЅС‚Р°Р¶СѓС”РјРѕ. РЇРєС‰Рѕ РїРѕСЂРѕР¶РЅСЊРѕ Р°Р»Рµ С” legacy single вЂ” С‚РµР¶ РґРѕРґР°С”РјРѕ.
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
            // Snapshot bere stan Р· СЂРµР°Р»СЊРЅРѕРіРѕ РїСѓР»Сѓ вЂ” РЅР°Р·РІРё РєР»СЋС‡С–РІ Р·С–СЃС‚Р°РІР»СЏС”РјРѕ Р·Р° Р·РЅР°С‡РµРЅРЅСЏРј Key.
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
                    // РљР»СЋС‡ С‰Рµ РЅРµ РІ РїСѓР»С– (С‰РѕР№РЅРѕ РґРѕРґР°Р»Рё РІ UI, С‰Рµ РЅРµ Р·Р±РµСЂРµРіР»Рё) вЂ” СЃС‚Р°С‚СѓСЃ РЅРµР№С‚СЂР°Р»СЊРЅРёР№
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

        // Р—Р±РµСЂС–РіР°С” РїРѕС‚РѕС‡РЅС– Р·РЅР°С‡РµРЅРЅСЏ РїРѕР»С–РІ LLM Сѓ in-memory snapshot _panelSettings,
        // С‰РѕР± РїРµСЂРµРєР»СЋС‡РµРЅРЅСЏ РјС–Р¶ РїСЂРѕРІР°Р№РґРµСЂР°РјРё РЅРµ РіСѓР±РёР»Рѕ РЅРµР·Р±РµСЂРµР¶РµРЅС– РїСЂР°РІРєРё
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
                    // РџР°СЂР°РјРµС‚СЂРё РїСѓР»Сѓ вЂ” Р· Р±РµР·РїРµС‡РЅРёРј РїР°СЂСЃРёРЅРіРѕРј (Р·Р°Р»РёС€Р°С”РјРѕ РїРѕРїРµСЂРµРґРЅС” Р·РЅР°С‡РµРЅРЅСЏ РїСЂРё РєСЂРёРІРѕРјСѓ РІРІРѕРґС–)
                    if (int.TryParse(SP_OllamaMaxPerHour.Text.Trim(), out var maxH) && maxH > 0)
                        _panelSettings.OllamaPoolMaxPerHour = maxH;
                    if (int.TryParse(SP_OllamaRotateAt.Text.Trim(), out var rotPct) && rotPct > 0 && rotPct <= 100)
                        _panelSettings.OllamaPoolRotateAt = rotPct / 100.0;
                    if (int.TryParse(SP_OllamaCooldown.Text.Trim(), out var cdMin) && cdMin > 0)
                        _panelSettings.OllamaPoolCooldownMins = cdMin;

                    // РџРµСЂРµР±СѓРґРѕРІСѓС”РјРѕ OllamaKeys Р· ObservableCollection вЂ” С„С–Р»СЊС‚СЂ: С‚С–Р»СЊРєРё СЂСЏРґРєРё Р· РЅРµРїРѕСЂРѕР¶РЅС–Рј Key.
                    // Р—Р±РµСЂС–РіР°С”РјРѕ С–СЃРЅСѓСЋС‡С– RecentRequests/CooldownUntil РґР»СЏ РєР»СЋС‡С–РІ С‰Рѕ Р·Р°Р»РёС€РёР»РёСЃСЊ (Р·Р° Р·Р±С–РіРѕРј Key).
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
                    // Legacy single вЂ” СЃРёРЅС…СЂРѕРЅС–Р·СѓС”РјРѕ Р· РїРµСЂС€РёРј РєР»СЋС‡РµРј РґР»СЏ backwards-compat
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

            // LLM Provider вЂ” РєРѕРјС–С‚РёРјРѕ РїРѕС‚РѕС‡РЅС– РїРѕР»СЏ Сѓ in-memory snapshot С‰РѕР± РЅРµ Р·Р°РіСѓР±РёС‚Рё
            // СЂРµРґР°РіСѓРІР°РЅРЅСЏ РїРѕС‚РѕС‡РЅРѕРіРѕ РїСЂРѕРІР°Р№РґРµСЂР°, РїРѕС‚С–Рј РїРµСЂРµРЅРѕСЃРёРјРѕ Р’РЎР† LLM-РїРѕР»СЏ Р· snapshot Сѓ s.
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

            // РќРµ РїРµСЂРµР·Р°РїРёСЃСѓС”РјРѕ СЏРєС‰Рѕ РїРѕР»Рµ РїРѕСЂРѕР¶РЅС” вЂ” Р·Р°С…РёСЃС‚ РІС–Рґ РІРёРїР°РґРєРѕРІРѕРіРѕ РѕР±РЅСѓР»РµРЅРЅСЏ
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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // WINDOW CHROME
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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

        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
        // CLEANUP
        // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

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
