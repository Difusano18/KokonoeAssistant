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
    }
}
