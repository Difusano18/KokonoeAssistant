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
        // TELEGRAM BOT API
        private void InitTelegram()
        {
            try
            {
                var s = AppSettings.Load();
                ServiceContainer.TelegramStatus.RefreshConfiguration(s);

                if (!s.TelegramEnabled)
                {
                    ServiceContainer.TelegramStatus.MarkBotState("disabled");
                    TgStatusLabel.Text = " · вимкнено";
                    TgStatusDot.Tag = "offline";
                    return;
                }

                if (string.IsNullOrWhiteSpace(s.TelegramToken) || s.TelegramChatId <= 0)
                {
                    ServiceContainer.TelegramStatus.MarkBotState("not_configured");
                    TgStatusLabel.Text = " · не налаштовано";
                    TgStatusDot.Tag = "offline";
                    System.Diagnostics.Debug.WriteLine("[Telegram] skipped: token or allowed chat id is missing");
                    return;
                }

                ServiceContainer.TelegramStatus.MarkBotState("connecting");
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
                ServiceContainer.TelegramStatus.MarkBotState("listening");
            }
            catch (Exception ex)
            {
                ServiceContainer.TelegramStatus.RecordBotError(ex.Message);
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
                    ServiceContainer.TelegramStatus.RecordBotActivity("callback");
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
                ServiceContainer.TelegramStatus.RecordBotActivity("incoming");

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
                        try { await bot.SendMessage(chatId, imgReply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8688: " + ex); }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TG] Photo error: {ex.Message}");
                        try { await bot.SendMessage(chatId, "Не змогла прочитати фото.", cancellationToken: ct); } catch (Exception logEx) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8693: " + logEx); }
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
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8711: " + ex); }

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
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(text); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8735: " + ex); }
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
                    var plan = PcActionPlan.Single("Telegram shell command", "shell", text, PcActionRiskTier.RiskyLocal);
                    plan.Actions[0].Arguments["command"] = text;
                    var replyText = await ExecuteLivePcActionPlanAsync(plan, ct);
                    await bot.SendMessage(chatId, replyText, cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Open)
                {
                    var plan = PcActionPlan.Single("Telegram open app", "openApp", text, PcActionRiskTier.SafeLocal);
                    var replyText = await ExecuteLivePcActionPlanAsync(plan, ct);
                    var openOk = !replyText.Contains("blocked", StringComparison.OrdinalIgnoreCase);
                    var openMsg = replyText;
                    await bot.SendMessage(chatId, openOk ? $"✅ {openMsg}" : $"❌ {openMsg}", cancellationToken: ct);
                    return;
                }

                if (awaiting == TgAwaitingMode.Kill)
                {
                    var plan = PcActionPlan.Single("Telegram kill process", "killProcess", text, PcActionRiskTier.RiskyLocal);
                    plan.AffectedProcesses.Add(text);
                    var replyText = await ExecuteLivePcActionPlanAsync(plan, ct);
                    var killOk = false;
                    var killMsg = replyText;
                    await bot.SendMessage(chatId, killOk ? $"✅ {killMsg}" : $"❌ {killMsg}", cancellationToken: ct);
                    return;
                }

                // ---- Regular chat - LLM ----
                if (TryStartObservationAgentTask(text, out var observationReply))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe]: {observationReply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = observationReply });
                    });
                    try { await bot.SendMessage(chatId, observationReply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8785: " + ex); }
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage { Content = observationReply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now });
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8790: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, observationReply));
                    ObserveAutonomousProfile("tg", text, observationReply);
                    return;
                }

                if (await TryHandleTelegramScreenScanAsync(bot, chatId, from, text, ct))
                    return;

                var overlordDirective = await TryHandleSystemOverlordDirectiveAsync(text, ct);
                if (overlordDirective.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe]: {overlordDirective.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = overlordDirective.Reply });
                    });
                    try { await bot.SendMessage(chatId, overlordDirective.Reply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8809: " + ex); }
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage { Content = overlordDirective.Reply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now });
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8814: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, overlordDirective.Reply));
                    ObserveAutonomousProfile("tg", text, overlordDirective.Reply);
                    return;
                }

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
                    try { await bot.SendMessage(chatId, controlCommand.Reply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8830: " + ex); }
                    try
                    {
                        ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage { Content = controlCommand.Reply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now });
                    }
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8835: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, controlCommand.Reply));
                    ObserveAutonomousProfile("tg", text, controlCommand.Reply);
                    return;
                }

                var profileUpdate = await TryHandleProfileUpdateAsync(text, ct);
                if (profileUpdate.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe]: {profileUpdate.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = profileUpdate.Reply });
                    });
                    try { await bot.SendMessage(chatId, profileUpdate.Reply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8851: " + ex); }
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8862: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, profileUpdate.Reply));
                    ObserveAutonomousProfile("tg", text, profileUpdate.Reply);
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
                    try { await bot.SendMessage(chatId, obsidianReply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8877: " + ex); }
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8888: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, obsidianReply));
                    ObserveAutonomousProfile("tg", text, obsidianReply);
                    return;
                }

                var agentDirective = await TryHandleSystemOverlordDirectiveAsync(text, ct, allowAgentTask: true);
                if (agentDirective.Handled)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _tgMessages.Add($"[Kokonoe]: {agentDirective.Reply}");
                        TgScroll.ScrollToBottom();
                        AddMessageBubble(new ChatMessageVm { Role = "user", Content = $"[TG: {from}] {text}" });
                        AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = agentDirective.Reply });
                    });
                    try { await bot.SendMessage(chatId, agentDirective.Reply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8904: " + ex); }
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
                    catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8915: " + ex); }
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, agentDirective.Reply));
                    ObserveAutonomousProfile("tg", text, agentDirective.Reply);
                    return;
                }

                await Task.Run(() =>
                {
                    try { ServiceContainer.BrainEngine?.ProcessUserMessage(text); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8923: " + ex); }
                }, ct);
                var tgTotalWatch = Stopwatch.StartNew();
                var tgContextResult = await Task.Run(() =>
                {
                    var context = BuildTelegramReplyContext("telegram-bot", text, out var fastRoute, out var contextMs);
                    return (Context: context, FastRoute: fastRoute, ContextMs: contextMs);
                }, ct);
                var tgContext = tgContextResult.Context;
                var tgFastRoute = tgContextResult.FastRoute;
                var tgContextMs = tgContextResult.ContextMs;
                KokoSystemLog.Write("TG_LATENCY", $"bot context route={(tgFastRoute ? "fast" : "full")} ms={tgContextMs} chars={tgContext.Length}");
                var tgPrompt =
                    $"Telegram direct message from {from}:\n" +
                    $"{text}\n\n" +
                    "Answer only to this latest message. Do not continue old proactive pings, food reminders, work reminders, or \"are you there\" checks unless this latest message asks about them. " +
                    "If the user asks what you meant, briefly reset the context and answer the current question. Ukrainian only, concise, natural.";
                var tgLlmWatch = Stopwatch.StartNew();
                var reply = await _llm.SendAsync(tgPrompt, null, "image/jpeg", tgContext, ct, agentId: "chat");
                var tgLlmMs = tgLlmWatch.ElapsedMilliseconds;
                var tgGuardWatch = Stopwatch.StartNew();
                reply = await GuardAndRepairReplyAsync(text, reply, tgContext, ct, allowLlmRepair: !tgFastRoute);
                KokoSystemLog.Write("TG_LATENCY", $"bot total route={(tgFastRoute ? "fast" : "full")} total_ms={tgTotalWatch.ElapsedMilliseconds} llm_ms={tgLlmMs} guard_ms={tgGuardWatch.ElapsedMilliseconds}");
                await Dispatcher.InvokeAsync(() =>
                {
                    _tgMessages.Add($"[Kokonoe]: {reply}");
                    TgScroll.ScrollToBottom();
                    AddMessageBubble(new ChatMessageVm { Role = "user",      Content = $"[TG: {from}] {text}" });
                    AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = reply });
                });
                try { await bot.SendMessage(chatId, reply, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8953: " + ex); }
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
                catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgUpdate failed near source line 8964: " + ex); }

                // Log TG exchange to vault archive
                _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, reply));
                ObserveAutonomousProfile("tg", text, reply);
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
            try { await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HandleTgCallback failed near source line 9009: " + ex); }

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
                case "pc_lock":         await bot.SendMessage(chatId, await ExecuteLivePcActionPlanAsync(PcActionPlan.Single("Telegram lock screen", "lockScreen", "", PcActionRiskTier.RiskyLocal), ct), cancellationToken: ct); break;
                case "pc_sleep":        await bot.SendMessage(chatId, await ExecuteLivePcActionPlanAsync(PcActionPlan.Single("Telegram sleep", "sleep", "", PcActionRiskTier.RiskyLocal), ct), cancellationToken: ct); break;
                case "pc_mon_off":      await bot.SendMessage(chatId, await ExecuteLivePcActionPlanAsync(PcActionPlan.Single("Telegram monitor off", "monitorOff", "", PcActionRiskTier.RiskyLocal), ct), cancellationToken: ct); break;
                case "pc_shutdown_ask": await TgConfirmAction(bot, chatId, "Справді вимкнути ПК?", "pc_shutdown_ok", ct); break;
                case "pc_restart_ask":  await TgConfirmAction(bot, chatId, "Справді перезавантажити?", "pc_restart_ok", ct); break;
                case "pc_shutdown_ok":  await bot.SendMessage(chatId, await ExecuteLivePcActionPlanAsync(PcActionPlan.Single("Telegram shutdown", "shutdown", "", PcActionRiskTier.ExternalOrIrreversible), ct), cancellationToken: ct); break;
                case "pc_restart_ok":   await bot.SendMessage(chatId, await ExecuteLivePcActionPlanAsync(PcActionPlan.Single("Telegram restart", "restart", "", PcActionRiskTier.ExternalOrIrreversible), ct), cancellationToken: ct); break;
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
            sb.AppendLine($"*CPU:* {info.CpuPercent:F1}%");
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
            try { ServiceContainer.BrainEngine?.Memory?.RecordEpisodeBlocking($"настрій: {m.Label}", m.Tone, m.Score); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "TgHandleMood failed near source line 9383: " + ex); }
            try { ServiceContainer.EmotionEngine.UpdateFromUserTone(m.Tone, m.Score); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "TgHandleMood failed near source line 9384: " + ex); }
            try { _obsidian.AppendToDailyNote($"\n> ❤️ [{DateTime.Now:HH:mm}] Настрій: {m.Label}"); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "TgHandleMood failed near source line 9385: " + ex); }

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
            ServiceContainer.TelegramStatus.RecordBotError(ex.Message);
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
                ServiceContainer.TelegramStatus.RecordBotActivity("sent");
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
    }
}
