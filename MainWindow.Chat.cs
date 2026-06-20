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

            if (text.Equals("test_mic", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("/test_mic", StringComparison.OrdinalIgnoreCase))
            {
                InputBox.Clear();
                await RunVoiceMicTestAsync();
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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 2938: " + ex); }

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
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(sendText); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 2960: " + ex); }
                }, _llmCts?.Token ?? default);

                var overlordDirective = await TryHandleSystemOverlordDirectiveAsync(sendText, _llmCts?.Token ?? default);
                if (overlordDirective.Handled)
                {
                    RemoveThinkingBubble();
                    var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
                    var replyTb = AddMessageBubble(replyVm);
                    if (replyTb != null)
                        await TypeIntoAsync(replyTb, overlordDirective.Reply, _llmCts?.Token ?? default);
                    else
                        replyVm.Content = overlordDirective.Reply;
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 2983: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, overlordDirective.Reply));
                    ObserveAutonomousProfile("app", sendText, overlordDirective.Reply);
                    return;
                }

                var profileUpdate = await TryHandleProfileUpdateAsync(sendText, _llmCts?.Token ?? default);
                if (profileUpdate.Handled)
                {
                    RemoveThinkingBubble();
                    var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
                    var replyTb = AddMessageBubble(replyVm);
                    if (replyTb != null)
                        await TypeIntoAsync(replyTb, profileUpdate.Reply, _llmCts?.Token ?? default);
                    else
                        replyVm.Content = profileUpdate.Reply;
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3009: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, profileUpdate.Reply));
                    ObserveAutonomousProfile("app", sendText, profileUpdate.Reply);
                    return;
                }

                if (TryStartObservationAgentTask(sendText, out var observationReply))
                {
                    RemoveThinkingBubble();
                    var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
                    var replyTb = AddMessageBubble(replyVm);
                    if (replyTb != null)
                        await TypeIntoAsync(replyTb, observationReply, _llmCts?.Token ?? default);
                    else
                        replyVm.Content = observationReply;
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                        {
                            Content = observationReply,
                            Role = "assistant",
                            Author = "Kokonoe",
                            Timestamp = DateTime.Now
                        });
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3034: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, observationReply));
                    ObserveAutonomousProfile("app", sendText, observationReply);
                    return;
                }

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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3086: " + ex); }

                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, obsidianCommand.replyText));
                    ObserveAutonomousProfile("app", sendText, obsidianCommand.replyText);
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3113: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, controlCommand.Reply));
                    ObserveAutonomousProfile("app", sendText, controlCommand.Reply);
                    return;
                }

                var agentDirective = await TryHandleSystemOverlordDirectiveAsync(sendText, _llmCts?.Token ?? default, allowAgentTask: true);
                if (agentDirective.Handled)
                {
                    RemoveThinkingBubble();
                    var replyVm = new ChatMessageVm { Role = "assistant", Content = "" };
                    var replyTb = AddMessageBubble(replyVm);
                    if (replyTb != null)
                        await TypeIntoAsync(replyTb, agentDirective.Reply, _llmCts?.Token ?? default);
                    else
                        replyVm.Content = agentDirective.Reply;
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3139: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, agentDirective.Reply));
                    ObserveAutonomousProfile("app", sendText, agentDirective.Reply);
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
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3182: " + ex); }

                // Auto-log this exchange to Obsidian vault for archival memory
                _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("app", sendText, reply));
                _ = Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine?.ObserveExchangeForVaultSync(sendText, reply); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3188: " + ex); }
                });
                ObserveAutonomousProfile("app", sendText, reply);

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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "SendMessage failed near source line 3203: " + ex); }
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
            var foreground = ServiceContainer.PcControl.GetForegroundWindow();
            var pcContext = ServiceContainer.PcControl.GetAllContext();
            var prompt = BuildScreenScanPrompt(userText, foreground, pcContext);
            string? reply;
            using (var visionCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                visionCts.CancelAfter(TimeSpan.FromSeconds(90));
                reply = await _llm.SendSystemVisionQueryAsync(prompt, screenshot, "image/jpeg", visionCts.Token);
            }

            if (VisionResponseQuality.LooksUnusable(reply) || VisionResponseQuality.LooksGeneric(reply))
            {
                try
                {
                    var enhanced = await Task.Run(() => ImageProcessingService.EnhanceForVision(screenshot), ct);
                    var retryPrompt = VisionResponseQuality.BuildRetryPrompt(prompt, foreground.ToString());
                    using var repairCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    repairCts.CancelAfter(TimeSpan.FromSeconds(120));
                    var repaired = await _llm.SendSystemVisionQueryAsync(
                        retryPrompt,
                        enhanced.Length > 0 ? enhanced : screenshot,
                        "image/jpeg",
                        repairCts.Token,
                        maxTokensOverride: 2048);
                    if (!VisionResponseQuality.LooksUnusable(repaired) && !VisionResponseQuality.LooksGeneric(repaired))
                        reply = repaired;
                }
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildScreenScanReplyAsync failed near source line 3422: " + ex); }
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                _lastManualScreenScanFailures++;
                reply = await BuildScreenScanFallbackReplyAsync(screenshot, _lastManualScreenScanFailures, userText, ct, pcContext);
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
            CancellationToken ct,
            PcContextSnapshot? pcContext = null)
        {
            var diag = _llm.GetDiagnosticsSnapshot();
            var activity = new ActivityAnalyzer().AnalyzeScreenshot(screenshot);
            var window = string.IsNullOrWhiteSpace(activity.ActiveWindowTitle)
                ? "активне вікно не визначилось"
                : activity.ActiveWindowTitle.Trim();
            var contextBlock = pcContext == null
                ? ""
                : "\n- Повний PC-контекст:\n" + PcIntentRouter.FormatAllContext(pcContext);
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
{contextBlock}

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

            var contextTail = pcContext == null
                ? ""
                : " " + TrimOpsLine(PcIntentRouter.FormatAllContext(pcContext), 420);
            return $"Скріншот зроблено, але опис зображення не повернувся. З локальних даних бачу активне вікно: {window}.{contextTail} {repeat}";
        }

        private static string BuildScreenScanPrompt(
            string userText,
            ForegroundWindowInfo? foreground = null,
            PcContextSnapshot? pcContext = null) => $"""
Foreground window: {foreground?.ToString() ?? "-"}
Full PC context:
{(pcContext == null ? "-" : PcIntentRouter.FormatAllContext(pcContext))}
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
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "StoreAssistantReply failed near source line 3549: " + ex); }
        }
    }
}
