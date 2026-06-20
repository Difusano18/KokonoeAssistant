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

            try { ServiceContainer.ChatRepository.ClearAll(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "ClearChat_Click failed near source line 4849: " + ex); }
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "AppendToSessionLog failed near source line 4913: " + ex); }

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
                    CornerRadius = new CornerRadius(10, 2, 10, 10),
                    Padding = new Thickness(16, 11, 16, 11),
                    BorderBrush = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentAsstBorder"],
                    BorderThickness = new Thickness(1)
                };
                bubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = ((System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentUserShadow"]).Color,
                    ShadowDepth = 0, BlurRadius = 10, Opacity = 0.18
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
                        Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentTextLight"],
                        FontSize = 13, LineHeight = 21,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI")
                    });
                }

                sp.Children.Add(new TextBlock
                {
                    Text = vm.TimeStr, FontSize = 10,
                    Foreground = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentMuted"],
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
                var emotionBorder = "#68E6D666";
                if (!isError)
                {
                    try
                    {
                        var emo = ServiceContainer.EmotionEngine.Current.ToString();
                        emotionBorder = emo switch
                        {
                            "Warm" or "Tender"         => "#D7B46A66",
                            "Playful"                  => "#68E6D666",
                            "Irritated" or "Distant"   => "#E25A6A66",
                            "Protective" or "Concerned" => "#D7B46A88",
                            "Melancholy"               => "#6C788466",
                            _                          => "#68E6D666"
                        };
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "AddMessageBubble failed near source line 5143: " + ex); }
                }

                // Bubble
                var bubble = new Border
                {
                    Background = isError
                        ? MakeBrush("#1A0808")
                        : (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentAsstBubble"],
                    CornerRadius = new CornerRadius(2, 10, 10, 10),
                    Padding = new Thickness(16, 12, 16, 12),
                    BorderBrush = MakeBrush(isError ? "#E25A6A66" : emotionBorder),
                    BorderThickness = new Thickness(1)
                };

                // Left accent line
                var innerGrid = new Grid();
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var accent = new Border
                {
                    Width = 3,
                    Background = isError
                        ? MakeBrush("#E25A6A")
                        : (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"],
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 2, 14, 2),
                    Opacity = 0.75
                };
                Grid.SetColumn(accent, 0);

                var textBlock = new TextBlock
                {
                    Text = vm.Content,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = isError
                        ? MakeBrush("#E25A6A")
                        : (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentTextLight"],
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
                    _                                         => "#68E6D6",
                };
                var color = (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(hex);
                EmotionDot.Fill = new System.Windows.Media.SolidColorBrush(color);

                // Update glow to match color
                if (EmotionDot.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
                    glow.Color = color;
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "UpdateEmotionDot failed near source line 5446: " + ex); }
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
            if (UseVoicePipelineV2)
            {
                await HandleRecordClickAsync();
                return;
            }

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

        private async Task HandleRecordClickAsync()
        {
            try
            {
                WireVoiceDiagnostics();
                var audio = ServiceContainer.AudioRecordService;

                if (_isRecording || audio.IsRecording)
                {
                    _isRecording = false;
                    SetVoiceRecordButtons("Processing...", false);
                    SetVoiceStatus("stopping recorder...");

                    await audio.StopRecordingAsync();
                    var bytes = await audio.GetRecordingBytesAsync();

                    if (bytes?.Length > 0)
                    {
                        var whisper = ServiceContainer.WhisperService;
                        if (!whisper.IsAvailable())
                        {
                            SetVoiceStatus("whisper unavailable: model not loaded and no fallback key");
                            VoiceTranscriptText.Text = "Whisper is not available. Check model download or API key.";
                            WMsgBox.Show("Whisper is not available. Check the model file or OpenAI API key.", "Voice STT");
                            return;
                        }

                        SetVoiceStatus($"transcribing {bytes.Length:N0} bytes...");
                        var text = await whisper.TranscribeAsync(bytes, "uk");
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            InputBox.Text += (InputBox.Text.Length > 0 ? " " : "") + text;
                            VoiceTranscriptText.Text = text;
                            SetVoiceStatus($"transcribed; peak={audio.PeakInputLevel:P0}; device={audio.ActiveDevice ?? "mic"}");
                        }
                        else
                        {
                            var reason = string.IsNullOrWhiteSpace(whisper.LastTranscriptionError)
                                ? "Whisper returned empty text. Input may be unclear."
                                : whisper.LastTranscriptionError;
                            VoiceTranscriptText.Text = $"Audio was captured, but transcription failed.\n{reason}";
                            SetVoiceStatus($"transcription failed; peak={audio.PeakInputLevel:P0}; {reason}");
                            AddMessageBubble(new ChatMessageVm
                            {
                                Role = "system",
                                Content = $"Voice input captured, but transcription failed. {reason}"
                            });
                        }
                    }
                    else
                    {
                        VoiceTranscriptText.Text = "No microphone buffers were recorded.";
                        SetVoiceStatus(audio.LastError ?? "no audio data captured");
                    }

                    SetVoiceRecordButtons("Voice", true);
                    return;
                }

                SetVoiceRecordButtons("Starting...", false);
                SetVoiceStatus("opening microphone...");
                var started = await audio.StartRecordingAsync();
                if (!started)
                {
                    _isRecording = false;
                    SetVoiceRecordButtons("Voice", true);
                    var error = audio.LastError ?? "microphone failed to start";
                    VoiceTranscriptText.Text = error;
                    SetVoiceStatus(error);
                    WMsgBox.Show(error, "Voice recorder");
                    return;
                }

                _isRecording = true;
                SetVoiceRecordButtons("Stop", true);
                SetVoiceStatus($"recording; level={audio.LastInputLevel:P0}; device={audio.ActiveDevice ?? "mic"}; format={audio.ActiveFormat?.ToString() ?? "-"}");
            }
            catch (Exception ex)
            {
                SetVoiceRecordButtons("Voice", true);
                _isRecording = false;
                SetVoiceStatus($"recording error: {ex.Message}");
                VoiceTranscriptText.Text = ex.Message;
                WMsgBox.Show($"Recording error: {ex.Message}", "Voice recorder");
            }
        }

        private void WireVoiceDiagnostics()
        {
            if (_voiceDiagnosticsHooked)
                return;

            var audio = ServiceContainer.AudioRecordService;
            audio.InputLevelChanged += (_, level) =>
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    VoiceInputLevelBar.Value = Math.Clamp(level * 100.0, 0, 100);
                    if (audio.IsRecording)
                        SetVoiceStatus($"recording; level={level:P0}; device={audio.ActiveDevice ?? "mic"}");
                });
            };
            audio.RecordingStarted += (_, _) =>
            {
                _ = Dispatcher.InvokeAsync(() => SetVoiceStatus($"recording; device={audio.ActiveDevice ?? "mic"}; format={audio.ActiveFormat?.ToString() ?? "-"}"));
            };
            audio.RecordingStopped += (_, _) =>
            {
                _ = Dispatcher.InvokeAsync(() => VoiceInputLevelBar.Value = 0);
            };
            audio.RecordingError += (_, error) =>
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    VoiceTranscriptText.Text = error.Message;
                    SetVoiceStatus(error.Message);
                });
            };

            _voiceDiagnosticsHooked = true;
        }

        private async Task RunVoiceMicTestAsync()
        {
            WireVoiceDiagnostics();
            var audio = ServiceContainer.AudioRecordService;
            SetVoiceRecordButtons("Testing...", false);
            VoiceTranscriptText.Text = "Recording 3-second microphone test...";
            SetVoiceStatus("test_mic recording for 3 seconds...");

            try
            {
                var file = await audio.TestMicAsync(TimeSpan.FromSeconds(3));
                SetVoiceRecordButtons("Voice", true);

                if (string.IsNullOrWhiteSpace(file))
                {
                    var error = audio.LastError ?? "test_mic failed";
                    VoiceTranscriptText.Text = error;
                    SetVoiceStatus(error);
                    WMsgBox.Show(error, "test_mic");
                    return;
                }

                VoiceTranscriptText.Text = $"Saved microphone test WAV:\n{file}\nPeak level: {audio.PeakInputLevel:P0}";
                SetVoiceStatus($"test_mic saved; peak={audio.PeakInputLevel:P0}; file={Path.GetFileName(file)}");
                AddMessageBubble(new ChatMessageVm
                {
                    Role = "system",
                    Content = $"test_mic saved: {file}"
                });
            }
            catch (Exception ex)
            {
                SetVoiceRecordButtons("Voice", true);
                VoiceTranscriptText.Text = ex.Message;
                SetVoiceStatus($"test_mic error: {ex.Message}");
                WMsgBox.Show(ex.Message, "test_mic");
            }
        }

        private void SetVoiceRecordButtons(string text, bool enabled)
        {
            RecordBtn.Content = text;
            RecordBtn.IsEnabled = enabled;
            VoiceRecordBtn.Content = text;
            VoiceRecordBtn.IsEnabled = enabled;
        }

        private void SetVoiceStatus(string status)
        {
            VoiceStatusLabel.Text = status;
            KokoSystemLog.Write("VOICE_UI", status);
        }

        private async void VoiceTestMic_Click(object sender, RoutedEventArgs e)
        {
            await RunVoiceMicTestAsync();
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SpeakAsync failed near source line 5936: " + ex); }
                });
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SpeakAsync failed near source line 5939: " + ex); }
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
    }
}
