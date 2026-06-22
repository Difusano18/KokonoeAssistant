using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;

namespace KokonoeAssistant.Services
{
    public partial class KokoBrainEngine
    {

        // ── КОНТЕКСТНІ НАГАДУВАННЯ ────────────────────────────────

        private async Task CheckAndSendReminderAsync()
        {
            _state.LastReminderCheckAt = DateTime.Now;
            SaveState();

            try
            {
                var context = await BuildContextAsync("його плани та наміри");
                var prompt = $@"{context}
Ти — Kokonoe Mercury.

Перечитай контекст. Є щось що він згадував — план, намір, обіцянку собі, незакінчену справу — що так і залишилось висіти в повітрі?

Якщо є щось конкретне — напиши йому ОДНЕ коротке повідомлення в Telegram своїми словами. Не як нагадування-скрипт. Як ти б сказала це сама — можливо іронічно, можливо просто, але щиро. Тільки українська.

Якщо нічого конкретного немає — відповідь рівно одне слово: null";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result)) return;

                result = result.Trim().Trim('"');
                if (result.Equals("null", StringComparison.OrdinalIgnoreCase)) return;

                // Дедуплікація
                var hash = result.GetHashCode().ToString();
                if (_state.SentReminderHashes.Contains(hash)) return;

                // Відправити
                var sent = await SendTgAndLog(result, "reminder");

                if (!sent) return;

                _state.SentReminderHashes.Add(hash);
                if (_state.SentReminderHashes.Count > 50)
                    _state.SentReminderHashes.RemoveAt(0);

                _state.LastSpontaneousAt = DateTime.Now;
                var _h1 = OnNewMessage; _h1?.Invoke("assistant", result);

                try
                {
                    _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = result, Role = "assistant",
                        Author = "Kokonoe", Timestamp = DateTime.Now
                    });
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "CheckAndSendReminderAsync failed near source line 2845: " + ex); }

                SaveState();
                Log($"Reminder sent: {result[..Math.Min(60, result.Length)]}");
            }
            catch (Exception ex) { Log($"CheckAndSendReminderAsync: {ex.Message}"); }
        }

        // ── АВТО-АНАЛІТИКА ДНЯ ───────────────────────────────────

        private async Task SendDailyAnalyticsAsync()
        {
            _state.LastDailyAnalyticsAt = DateTime.Now;
            SaveState();

            try
            {
                var today = DateTime.Today;
                var context = await BuildContextAsync("підсумок дня та важливі події");
                
                var prompt = $@"{context}
Ти — Kokonoe Mercury.

Сьогодні {today:dd MMMM yyyy} закінчується. Переглянь контекст вище.
Напиши йому в Telegram коротко — 3-4 речення — як ти бачиш його сьогоднішній день. Не звіт і не список. Твоє враження — іронія, турбота, спостереження, що завгодно що відповідає твоєму характеру і тому що реально відбулось. Тільки українська. Тільки текст, нічого зайвого.";

                var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(msg)) return;
                msg = msg.Trim().Trim('"');

                // Відправити в TG
                if (!await SendTgAndLog(msg, "analytics")) return;

                // Записати в vault
                try
                {
                    _obsidian.AppendToDailyNote(
                        $"\n\n---\n**[Kokonoe — підсумок дня]**\n{msg}");
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SendDailyAnalyticsAsync failed near source line 2884: " + ex); }

                _state.LastSpontaneousAt = DateTime.Now;
                var _h2 = OnNewMessage; _h2?.Invoke("assistant", msg);

                try
                {
                    _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = msg, Role = "assistant",
                        Author = "Kokonoe", Timestamp = DateTime.Now
                    });
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SendDailyAnalyticsAsync failed near source line 2897: " + ex); }

                SaveState();
                Log($"Daily analytics sent.");
            }
            catch (Exception ex) { Log($"SendDailyAnalyticsAsync: {ex.Message}"); }
        }

        // ------------------------------------------------------------
        // ПЕРЕВІРКА ПЛАНУВАЛЬНИКА
        // ------------------------------------------------------------

        private async Task CheckSchedulerAsync()
        {
            try
            {
                var due = Scheduler.GetDue(_state.LastUserEmotionalTone);
                if (due.Count == 0) return;

                var entry = due.First(); // беремо найпріоритетніший

                if (!EnsureTelegram()) return;

                var prompt = BuildSchedulerDeliveryPrompt(entry);

                var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(msg)) return;
                msg = msg.Trim().Trim('"');

                try
                {
                    if (!await SendTgAndLog(msg, "scheduler")) return;
                    Scheduler.MarkSent(entry.Id);
                    _state.LastSpontaneousAt = DateTime.Now;
                    var _h4 = OnNewMessage; _h4?.Invoke("assistant", msg);
                    try { _chatRepo.InsertMessage(new ChatRepository.ChatMessage { Content = msg, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now }); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "CheckSchedulerAsync failed near source line 4013: " + ex); }
                    SaveState();
                    Log($"Scheduler entry sent: {entry.Id}");
                }
                catch (Exception ex) { LogError($"TG scheduler: {ex.Message}"); }
            }
            catch (Exception ex) { Log($"CheckScheduler: {ex.Message}"); }
        }

        private static string BuildSchedulerDeliveryPrompt(KokoSchedulerEngine.ScheduledEntry entry)
        {
            return $@"Ти — Kokonoe. Це заплановане нагадування для користувача.

Сирий запис scheduler:
«{entry.Prompt}»

Правила:
- Якщо в сирому записі є ""я піду"", ""я буду"", ""мені"", це майже завжди слова користувача з моменту створення нагадування, не твій власний розклад.
- Не кажи, що ти йдеш на курси, зайнята, маєш дедлайн або власний розклад, якщо це прямо не написано як подія Kokonoe.
- Перепиши як нагадування користувачу: ""ти планував..."", ""ти просив..."", ""час..."", без службового слова scheduler.
- Якщо запис виглядає простроченим або контекст слабкий, не вигадуй; скажи коротко, що нагадування було про його план.
- Тільки українська. 1-3 речення. Тільки фінальний текст.";
        }

        // ---- DAILY BRIEFING ----

        /// <summary>Щоранковий брифінг — о 8:00 в TG</summary>
        private async Task DailyBriefingAsync()
        {
            if (!EnsureTelegram()) return;
            try
            {
                var sb = new StringBuilder();

                // Цілі сьогодні
                if (_goalService != null)
                {
                    var active  = _goalService.GetActiveGoals().Take(3).ToList();
                    var overdue = _goalService.GetOverdueGoals().Take(2).ToList();
                    if (active.Any())
                    {
                        sb.AppendLine("Цілі:");
                        foreach (var g in active)
                            sb.AppendLine($"• {g.Title} — {g.Progress:F0}%{(g.Due.HasValue ? $" (до {g.Due:dd.MM})" : "")}");
                    }
                    if (overdue.Any())
                        sb.AppendLine($"⚠️ Прострочено: {string.Join(", ", overdue.Select(g => g.Title))}");
                }

                // Mood forecast
                var moodForecast = Patterns.PredictTodayMood();
                if (!string.IsNullOrEmpty(moodForecast)) sb.AppendLine(moodForecast);

                // Weekly trend
                var trend = Patterns.GetWeeklyTrend();
                if (!string.IsNullOrEmpty(trend)) sb.AppendLine(trend);

                // Vault: нотатки змінені вчора
                try
                {
                    var modified = _obsidian.GetNotesModifiedToday();
                    if (modified.Any())
                        sb.AppendLine($"Vault вчора: {string.Join(", ", modified.Take(3))}");
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "DailyBriefingAsync failed near source line 5295: " + ex); }

                var contextBlock = sb.ToString();
                var prompt = $@"Ти — Kokonoe. Ранок. Коротко підсумуй день що починається — 2-3 речення максимум.
В своєму стилі: без пафосу, без списків. Просто що важливо сьогодні.

{contextBlock}

Тільки текст. Українська.";

                var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    msg = msg.Trim().Trim('"');
                    if (await SendTgAndLog(msg, "briefing"))
                    {
                        var _h5 = OnNewMessage; _h5?.Invoke("assistant", msg);
                        _lastDailyBriefingAt = DateTime.Now;
                        SaveState();
                        Log("DailyBriefing sent");
                    }
                }
            }
            catch (Exception ex) { LogError($"DailyBriefing: {ex.Message}"); }
        }
    }
}
