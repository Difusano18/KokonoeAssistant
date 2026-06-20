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
        // TELEGRAM USER CLIENT
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
                try { oldSvc.Dispose(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "InitTelegramUserAsync failed near source line 9518: " + ex); }
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
                try { ServiceContainer.BrainEngine?.ProcessUserMessage($"[TG {msg.Sender}]: {msg.Text}"); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9603: " + ex); }
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
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9614: " + ex); }

                if (await TryHandleTelegramUserScreenScanAsync(msg, svc, _tgUserCts.Token))
                    return;

                var overlordDirective = await TryHandleSystemOverlordDirectiveAsync(msg.Text, _tgUserCts.Token);
                if (overlordDirective.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe → {msg.ChatName}]: {overlordDirective.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG {msg.Sender}]: {msg.Text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = overlordDirective.Reply });
                    });
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = overlordDirective.Reply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9639: " + ex); }
                    await svc.SendAsync(msg.ChatId, overlordDirective.Reply, _tgUserCts.Token);
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg-user", msg.Text, overlordDirective.Reply));
                    ObserveAutonomousProfile("tg-user", msg.Text, overlordDirective.Reply);
                    return;
                }

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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9666: " + ex); }
                    await svc.SendAsync(msg.ChatId, controlCommand.Reply, _tgUserCts.Token);
                    ObserveAutonomousProfile("tg-user", msg.Text, controlCommand.Reply);
                    return;
                }

                var profileUpdate = await TryHandleProfileUpdateAsync(msg.Text, _tgUserCts.Token);
                if (profileUpdate.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe в†’ {msg.ChatName}]: {profileUpdate.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG {msg.Sender}]: {msg.Text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = profileUpdate.Reply });
                    });
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = profileUpdate.Reply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9692: " + ex); }
                    await svc.SendAsync(msg.ChatId, profileUpdate.Reply, _tgUserCts.Token);
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg-user", msg.Text, profileUpdate.Reply));
                    ObserveAutonomousProfile("tg-user", msg.Text, profileUpdate.Reply);
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9718: " + ex); }
                    await svc.SendAsync(msg.ChatId, obsidianReply, _tgUserCts.Token);
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg-user", msg.Text, obsidianReply));
                    ObserveAutonomousProfile("tg-user", msg.Text, obsidianReply);
                    return;
                }

                var agentDirective = await TryHandleSystemOverlordDirectiveAsync(msg.Text, _tgUserCts.Token, allowAgentTask: true);
                if (agentDirective.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe -> {msg.ChatName}]: {agentDirective.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG {msg.Sender}]: {msg.Text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = agentDirective.Reply });
                    });
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = agentDirective.Reply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9745: " + ex); }
                    await svc.SendAsync(msg.ChatId, agentDirective.Reply, _tgUserCts.Token);
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg-user", msg.Text, agentDirective.Reply));
                    ObserveAutonomousProfile("tg-user", msg.Text, agentDirective.Reply);
                    return;
                }

                var tgTotalWatch = Stopwatch.StartNew();
                var tgContextResult = await Task.Run(() =>
                {
                    var context = BuildTelegramReplyContext("telegram", msg.Text, out var fastRoute, out var contextMs);
                    return (Context: context, FastRoute: fastRoute, ContextMs: contextMs);
                }, _tgUserCts.Token);
                var sharedContext = tgContextResult.Context;
                var tgFastRoute = tgContextResult.FastRoute;
                var tgContextMs = tgContextResult.ContextMs;
                KokoSystemLog.Write("TG_LATENCY", $"user context route={(tgFastRoute ? "fast" : "full")} ms={tgContextMs} chars={sharedContext.Length}");
                var tgLlmWatch = Stopwatch.StartNew();
                var raw = await _llm.SendTgAsync(prompt, sharedContext, _tgUserCts.Token);
                if (string.IsNullOrWhiteSpace(raw)) return;
                var tgLlmMs = tgLlmWatch.ElapsedMilliseconds;
                var reply = CleanTgReply(raw);
                var tgGuardWatch = Stopwatch.StartNew();
                reply = await GuardAndRepairReplyAsync(msg.Text, reply, sharedContext, _tgUserCts.Token, allowLlmRepair: !tgFastRoute);
                KokoSystemLog.Write("TG_LATENCY", $"user total route={(tgFastRoute ? "fast" : "full")} total_ms={tgTotalWatch.ElapsedMilliseconds} llm_ms={tgLlmMs} guard_ms={tgGuardWatch.ElapsedMilliseconds}");

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
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUserMessageAsync failed near source line 9791: " + ex); }
                await svc.SendAsync(msg.ChatId, reply, _tgUserCts.Token);
                _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg-user", msg.Text, reply));
                ObserveAutonomousProfile("tg-user", msg.Text, reply);
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

            try { await bot.SendMessage(chatId, reply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "TryHandleTelegramScreenScanAsync failed near source line 9835: " + ex); }
            StoreAssistantReply(reply);
            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, reply));
            ObserveAutonomousProfile("tg", text, reply);
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
            ObserveAutonomousProfile("tg-user", msg.Text, reply);
            return true;
        }
    }
}
