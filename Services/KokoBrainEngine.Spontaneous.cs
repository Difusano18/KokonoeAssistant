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

        private async Task GuardedSpontaneousAsync()
        {
            if (Interlocked.CompareExchange(ref _spontaneousInFlight, 1, 0) != 0)
            {
                Log("SpontaneousCheck skipped — previous tick still in flight");
                return;
            }
            try
            {
                await SafeSpontaneousCheckAsync();
                CheckForAutonomousObjectives("spontaneous-timer");
                TryQueueAutonomousAgentCycle("spontaneous-timer");
            }
            finally { Interlocked.Exchange(ref _spontaneousInFlight, 0); }
        }

        public void SetTelegram(TelegramBotClient bot, long chatId)
        {
            _tgBot         = bot;
            _tgChatId      = chatId;
            _tgInitialized = true;
        }

        // ---- TELEGRAM SELF-INIT ----
        // Brain ініціалізує свій TG незалежно від MainWindow.
        // Якщо SetTelegram не викликали (помилка в UI або неправильний порядок) —
        // при першій потребі brain сам підключається до TG через settings.

        private bool EnsureTelegram()
        {
            if (_tgInitialized && _tgBot != null) return true;

            try
            {
                var s = AppSettings.Load();
                if (!s.TelegramEnabled || string.IsNullOrEmpty(s.TelegramToken)) return false;

                _tgBot         = new TelegramBotClient(s.TelegramToken);
                _tgChatId      = s.TelegramChatId;
                if (_tgChatId <= 0)
                {
                    LogError("TelegramChatId = 0 — перевір налаштування");
                    return false;
                }
                _tgInitialized = true;
                Log("TG self-initialized from settings");
                return true;
            }
            catch (Exception ex)
            {
                Log($"TG self-init failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Обгортка для відправки TG повідомлень з автоматичним логуванням в vault.
        /// Використовувати замість прямого _tgBot.SendMessage для всіх спонтанних/проактивних повідомлень.
        /// </summary>
        private async Task<bool> SendTgAndLog(string message, string category = "spontaneous")
        {
            if (!EnsureTelegram() || string.IsNullOrWhiteSpace(message)) return false;
            if (ShouldSuppressAutomatedTelegram(category, out var suppressionReason))
            {
                Log($"TG send suppressed ({category}): {suppressionReason}");
                return false;
            }
            lock (_lock)
            {
                if (ShouldSuppressRecentThought(message, category, out var duplicateReason))
                {
                    Log($"TG send suppressed duplicate ({category}): {duplicateReason}");
                    return false;
                }
                // Recorded before the send (not after) so a concurrent call from another
                // timer/path sees this thought as already-claimed instead of racing past
                // the same suppression check while this one is still awaiting the network call.
                RecordRecentThought(message, category, DateTime.Now);
            }

            try
            {
                await _tgBot!.SendMessage(_tgChatId, message);
                // Log to vault archive
                try { ServiceContainer.ChatLogger.LogOutgoing("tg", message, category); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SendTgAndLog failed near source line 1493: " + ex); }
                return true;
            }
            catch (Exception ex)
            {
                LogTelegramDeliveryFailure($"send ({category}): {ex.Message}");
                return false;
            }
        }

        private bool ShouldSuppressAutomatedTelegram(string category)
            => ShouldSuppressAutomatedTelegram(category, out _);

        private bool ShouldSuppressAutomatedTelegram(string category, out string reason)
        {
            reason = "";
            if (category.Contains("crisis", StringComparison.OrdinalIgnoreCase))
                return false;

            if (_state.ProactiveMutedUntil > DateTime.Now)
            {
                reason = $"proactive muted until {_state.ProactiveMutedUntil:HH:mm}";
                return true;
            }

            if (HasActiveSleepIntent(DateTime.Now) && IsSleepInterruptCategory(category))
            {
                reason = "active sleep intent; suppress automated Telegram until explicit wake/safe biometric wake";
                return true;
            }

            if (IsLowActivityState(DateTime.Now) &&
                DateTime.Now - _state.LastSpontaneousAt < TimeSpan.FromMinutes(60) &&
                IsSpontaneousLikeCategory(category))
            {
                reason = "low activity cooldown 60m";
                return true;
            }

            var lastUserAt = _state.LastUserMessageAt;
            if (lastUserAt <= DateTime.MinValue)
                return false;

            var cooldownMinutes = IsFastProactiveCategory(category) ? 5 : 10;
            var elapsed = DateTime.Now - lastUserAt;
            if (elapsed >= TimeSpan.FromMinutes(cooldownMinutes))
                return false;

            reason = $"recent user message cooldown {elapsed.TotalMinutes:0.0}m < {cooldownMinutes}m";
            return true;
        }

        private bool ShouldSuppressRecentThought(string message, string category, out string reason)
        {
            reason = "";
            if (category.Contains("crisis", StringComparison.OrdinalIgnoreCase))
                return false;

            var now = DateTime.Now;
            _state.RecentThoughtBuffer.RemoveAll(t => now - t.At > TimeSpan.FromHours(6));
            var canonical = NormalizeThoughtForBuffer(message);
            if (canonical.Length < 8)
                return false;

            var isIdle = LooksLikeIdleOrStuckThought(canonical, category);
            var similar = _state.RecentThoughtBuffer
                .Where(t => now - t.At <= TimeSpan.FromHours(6))
                .Select(t => new { Thought = t, Score = ThoughtSimilarity(canonical, t.Canonical) })
                .Where(x => x.Score >= 0.72 || x.Thought.Hash == canonical)
                .OrderByDescending(x => x.Score)
                .ToList();

            if (similar.Any())
            {
                var hit = similar[0].Thought;
                reason = $"similar thought already sent at {hit.At:HH:mm}; score={similar[0].Score:F2}; preview={hit.Preview}";
                return true;
            }

            if (isIdle)
            {
                var idleCount = _state.RecentThoughtBuffer.Count(t =>
                    now - t.At <= TimeSpan.FromHours(6) &&
                    LooksLikeIdleOrStuckThought(t.Canonical, t.Category));
                if (idleCount >= 2)
                {
                    reason = $"idle/stuck observation already sent {idleCount} times in 6h";
                    return true;
                }
            }

            if (WouldRepeatPresenceTrace(canonical, now))
            {
                reason = "presence trace already contains this idle/away/sleep observation";
                return true;
            }

            return false;
        }

        private void RecordRecentThought(string message, string category, DateTime now)
        {
            var canonical = NormalizeThoughtForBuffer(message);
            if (canonical.Length < 8)
                return;

            _state.RecentThoughtBuffer.RemoveAll(t => now - t.At > TimeSpan.FromHours(6));
            _state.RecentThoughtBuffer.Add(new RecentThoughtFingerprint
            {
                At = now,
                Category = category,
                Hash = canonical,
                Canonical = canonical,
                Preview = TrimStateMention(message)
            });
            if (_state.RecentThoughtBuffer.Count > 80)
                _state.RecentThoughtBuffer.RemoveRange(0, _state.RecentThoughtBuffer.Count - 80);
        }

        private static string NormalizeThoughtForBuffer(string text)
        {
            var chars = (text ?? "")
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ')
                .ToArray();
            var normalized = new string(chars);
            while (normalized.Contains("  ", StringComparison.Ordinal))
                normalized = normalized.Replace("  ", " ");
            return normalized.Trim();
        }

        private static double ThoughtSimilarity(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return 0;
            if (left == right)
                return 1;

            var a = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var b = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (a.Count == 0 || b.Count == 0)
                return 0;
            var intersection = a.Count(t => b.Contains(t));
            var union = a.Count + b.Count - intersection;
            return union <= 0 ? 0 : intersection / (double)union;
        }

        private static bool LooksLikeIdleOrStuckThought(string canonical, string category)
        {
            var text = $"{canonical} {category}".ToLowerInvariant();
            return ContainsAny(text,
                "idle", "stuck", "silence", "away", "ghost", "zombie", "завис", "пропав", "мовч", "тиша", "idle_staring");
        }

        private bool WouldRepeatPresenceTrace(string canonical, DateTime now)
        {
            if (!LooksLikeIdleOrStuckThought(canonical, ""))
                return false;

            return _state.PresenceTrace
                .TakeLast(8)
                .Any(t => ContainsAny(t.ToLowerInvariant(), "idle", "away", "ghost", "sleep", "stuck", "завис", "відійшов"));
        }

        private bool HasActiveSleepIntent(DateTime now)
            => _state.ShortTermIntents.Any(i => !i.ResolvedAt.HasValue && i.Kind == "sleep" && now - i.CreatedAt < TimeSpan.FromHours(24));

        private bool IsLowActivityState(DateTime now)
        {
            if (HasActiveSleepIntent(now))
                return true;
            if (_state.LastPresenceSituation is "away" or "ghost_mode" or "watch_sleeping" or "sleep_locked" or "sleeping")
                return true;

            try
            {
                if (ServiceContainer.PcControl.GetSystemInfo().IdleTime >= TimeSpan.FromMinutes(30))
                    return true;
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "IsLowActivityState failed near source line 1672: " + ex); }

            try
            {
                var wearable = ServiceContainer.WearableTelemetry?.State;
                if (wearable != null && wearable.IsFresh(now.ToUniversalTime()) &&
                    wearable.OnWrist &&
                    ((wearable.Motion ?? 0) <= 0.01 || wearable.SleepState is "probably_asleep" or "drowsy_or_resting" or "quiet_night"))
                    return true;
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "IsLowActivityState failed near source line 1682: " + ex); }

            return false;
        }

        private static bool IsSpontaneousLikeCategory(string category)
            => category.Contains("spontaneous", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("jab", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("observation", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("reactive", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("night", StringComparison.OrdinalIgnoreCase);

        private static bool IsSleepInterruptCategory(string category)
            => IsSpontaneousLikeCategory(category) ||
               category.Contains("briefing", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("digest", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("analytics", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("resource_guardian", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("predictive", StringComparison.OrdinalIgnoreCase);

        private static bool IsFastProactiveCategory(string category)
            => category.Contains("jab", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("observation", StringComparison.OrdinalIgnoreCase) ||
               category.Contains("observe", StringComparison.OrdinalIgnoreCase);

        // ---- SPONTANEOUS MESSAGE CHECK ----

        private async Task SafeSpontaneousCheckAsync()
        {
            if (_disposed) return;
            try { await SpontaneousCheckAsync(); }
            catch (Exception ex) { Log($"SpontaneousCheck error: {ex.Message}"); }

            if (_disposed) return;
            try { await CheckInAppSilenceAsync(); }
            catch (Exception ex) { Log($"InAppSilence error: {ex.Message}"); }
        }

        /// <summary>Мовчання 4+ годин → тихе повідомлення в UI (без Telegram)</summary>
        private async Task CheckInAppSilenceAsync()
        {
            if (OnNewMessage == null) return;

            var now = DateTime.Now;
            if (ShouldSuppressProactiveForSleep(now)) return;
            // Cooldown: один раз на день
            if (_lastInAppSilenceMsgAt.Date >= now.Date) return;
            // Якщо WhatDidIMiss або інший спонтанний вже надсилав сьогодні — не дублювати
            if ((now - _state.LastSpontaneousAt).TotalHours < 2) return;

            var msgs = _chatRepo.GetMessages(20);
            var lastUser = msgs.Where(m => m.Role == "user")
                               .OrderByDescending(m => m.Timestamp)
                               .FirstOrDefault();
            if (lastUser == null) return;

            var silenceHours = (now - lastUser.Timestamp).TotalHours;
            if (silenceHours < 4) return;

            // Перевіряємо чи зараз кращий час для написати
            try
            {
                var bestTimeStr = Patterns.GetBestTimeToReach(); // "Найкращий час: ~21:00" або ""
                if (!string.IsNullOrEmpty(bestTimeStr))
                {
                    var hourMatch = System.Text.RegularExpressions.Regex.Match(bestTimeStr, @"~(\d+):");
                    if (hourMatch.Success && int.TryParse(hourMatch.Groups[1].Value, out var bestHour))
                    {
                        if (Math.Abs(now.Hour - bestHour) > 3) return; // не його активний час
                    }
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "CheckInAppSilenceAsync failed near source line 5496: " + ex); }

            var personalityBlock = BuildPersonalityInjection();
            var prompt = $@"Ти — Kokonoe Mercury.

{personalityBlock}

Він мовчить вже {(int)silenceHours} годин. Напиши одне коротке природнє речення — просто дай знати що ти тут.
Не питай «чи все добре». Не будь надокучливою. Просто — поруч.
Тільки українська. Тільки текст без лапок.";

            var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
            if (string.IsNullOrWhiteSpace(msg)) return;

            msg = msg.Trim().Trim('"');
            _lastInAppSilenceMsgAt = now;

            var _h7 = OnNewMessage; _h7?.Invoke("assistant", msg);
            try
            {
                _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content = msg, Role = "assistant", Author = "Kokonoe", Timestamp = now
                });
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "CheckInAppSilenceAsync failed near source line 5521: " + ex); }
        }

        private SpontaneousStyle ChooseStyle(DateTime now, double silenceMinutes)
        {
            var bond = Emotion.Bond;

            if (_state.PersonalityInCrisis) return SpontaneousStyle.CrisisSupport;

            // Тиша — наростаюча динаміка
            if (silenceMinutes > 60 && silenceMinutes < 180)
            {
                return bond >= KokoEmotionEngine.BondLevel.Trusted
                    ? SpontaneousStyle.WarmCheck
                    : SpontaneousStyle.Jab;
            }
            if (silenceMinutes >= 180 && silenceMinutes < 360)
            {
                return bond >= KokoEmotionEngine.BondLevel.Trusted
                    ? SpontaneousStyle.WarmCheck
                    : SpontaneousStyle.ColdCheck;
            }
            if (silenceMinutes >= 360)
            {
                // 6г+ — підкидає щось цікаве, не питає де він
                if (Random.Shared.NextDouble() < 0.3 && Memory.GetPeakEpisodes(3).Any())
                    return SpontaneousStyle.Callback;
                return SpontaneousStyle.Observation;
            }

            // Ніч
            if (now.Hour >= 0 && now.Hour < 5) return SpontaneousStyle.NightMessage;

            // pending thoughts
            if (_state.PendingThoughts.Any()) return SpontaneousStyle.PendingThought;

            // Surprise callback — 5% шанс
            if (Random.Shared.NextDouble() < 0.05 && Memory.GetPeakEpisodes(5).Any())
                return SpontaneousStyle.Callback;

            // За настроєм
            return _state.PersonalityDailyMood switch
            {
                "playful" => SpontaneousStyle.Jab,
                "warm"    => SpontaneousStyle.WarmCheck,
                _         => SpontaneousStyle.Observation,
            };
        }

        private async Task SpontaneousCheckAsync()
        {
            var s = AppSettings.Load();
            if (!s.TelegramEnabled || !s.SpontaneousEnabled) return;
            if (!EnsureTelegram()) return;

            var now = DateTime.Now;
            RefreshTemporalState(now, "spontaneous");
            EnsureVaultSyncFreshness("spontaneous");
            var autonomyLevel = Math.Clamp(s.ProactiveAutonomyLevel, 0, 3);
            if (autonomyLevel <= 0) return;
            if (ShouldSuppressProactiveForSleep(now)) return;
            var baseInterval = Math.Clamp(s.SpontaneousIntervalMins, 10, 240);
            var globalCooldown = autonomyLevel switch
            {
                >= 3 => Math.Max(20, baseInterval),
                2    => Math.Max(45, baseInterval),
                _    => Math.Max(90, baseInterval)
            };

            // Планувальник і реактивні follow-up не є "рандомною балаканиною".
            // Якщо користувач сказав "йду на курси", follow-up має спрацювати за часом,
            // навіть коли загальний антиспам ще не пустив би звичайну ініціативу.
            await CheckSchedulerAsync();
            if (await CheckReactiveTriggersAsync())
                return;

            // ── ГЛОБАЛЬНИЙ COOLDOWN ─────────────────────────────────────
            // Не надсилати нічого якщо ще не минув мінімальний інтервал.
            // У живому режимі він нижчий, але все одно є, бо Telegram не смітник.
            var minsSinceLast = (now - _state.LastSpontaneousAt).TotalMinutes;
            if (minsSinceLast < globalCooldown) return;

            // Вночі — мовчати крім явно високого рівня автономності, кризи і нічного чекіну.
            if ((now.Hour >= 23 || now.Hour < 6) && autonomyLevel < 3) return;
            // ------------------------------------------------------------

            // Ранковий привіт (6:30–9:00, один раз на день)
            if (now.Hour >= 6 && now.Hour < 9 &&
                _state.LastMorningGreetAt.Date < now.Date)
            {
                await SendSpontaneousAsync("morning", SpontaneousStyle.Morning);
                _state.LastMorningGreetAt = now;
                SaveState();
                return;
            }

            // Нічна перевірка (22:00–23:30, один раз на день)
            if (now.Hour >= 22 && now.Hour < 24 &&
                _state.LastNightCheckAt.Date < now.Date)
            {
                await SendSpontaneousAsync("night", SpontaneousStyle.NightMessage);
                _state.LastNightCheckAt = now;
                SaveState();
                return;
            }

            // Screen context refresh
            _ = RefreshScreenContextAsync();

            // Daily briefing — о 8:00, раз на день
            if (now.Hour == 8 && _lastDailyBriefingAt.Date < now.Date)
                await DailyBriefingAsync();

            // Weekly digest — неділя о 20:00, раз на тиждень
            if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 20 &&
                (now - _lastWeeklyDigestAt).TotalDays >= 6)
                _ = WeeklyVaultDigestAsync();

            // BPM-based dynamic silence thresholds
            // Висока ЧСС (збуджена) → коротший поріг, пише раніше
            // Низька ЧСС (спокійна) → довший поріг, терпеливіша
            double bpmMod = 0;
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null && heart.CurrentBpm > 0)
                {
                    var dev = heart.CurrentBpm - heart.BaselineBpm;
                    // +20 bpm deviation → -15хв до порогу (агресивніша)
                    // -20 bpm deviation → +20хв до порогу (терпеливіша)
                    bpmMod = Math.Clamp(-dev * 0.75, -25, 30);
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SpontaneousCheckAsync failed near source line 5668: " + ex); }

            // Динаміка тиші — окремі рівні cooldown
            try
            {
                var msgs = _chatRepo.GetMessages(20);
                var lastUser = msgs.Where(m => m.Role == "user")
                                   .OrderByDescending(m => m.Timestamp)
                                   .FirstOrDefault();
                if (lastUser != null)
                {
                    var silenceMin = (now - lastUser.Timestamp).TotalMinutes;
                    var activeIntent = _state.ShortTermIntents
                        .Where(i => !i.ResolvedAt.HasValue)
                        .OrderBy(i => i.FollowUpAt)
                        .FirstOrDefault();
                    if (activeIntent != null && now < activeIntent.FollowUpAt)
                    {
                        Log($"Silence reaction suppressed: active intent '{activeIntent.Kind}' waits until {activeIntent.FollowUpAt:HH:mm}");
                        return;
                    }

                    // Рівень 1: базово 60хв, BPM може опустити до ~35хв або підняти до ~90хв
                    var l1Base = autonomyLevel >= 3 ? 90 : 120;
                    var l1Threshold = Math.Max(75, l1Base + bpmMod);
                    if (silenceMin > l1Threshold && (now - _state.SilenceLevel1At).TotalHours > 2)
                    {
                        if (await SendSilenceReactionAsync("silence_l1", silenceMin, lastUser.Content))
                        {
                            _state.SilenceLevel1At = now;
                            SaveState();
                            return;
                        }
                    }
                    // Рівень 2: базово 3г, BPM може опустити до ~2г або підняти до ~4г
                    var l2Base = autonomyLevel >= 3 ? 180 : 240;
                    var l2Threshold = Math.Max(150, l2Base + bpmMod * 2);
                    if (silenceMin > l2Threshold && (now - _state.SilenceLevel2At).TotalHours > 4)
                    {
                        if (await SendSilenceReactionAsync("silence_l2", silenceMin, lastUser.Content))
                        {
                            _state.SilenceLevel2At = now;
                            SaveState();
                            return;
                        }
                    }
                    // Рівень 3: 6г — не модифікуємо (вже критична тиша)
                    if (silenceMin > 360 && (now - _state.SilenceLevel3At).TotalHours > 8)
                    {
                        if (await SendSilenceReactionAsync("silence_l3", silenceMin, lastUser.Content))
                        {
                            _state.SilenceLevel3At = now;
                            SaveState();
                            return;
                        }
                    }
                    // 12г+ — нічого. Вона не переслідує.
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SpontaneousCheckAsync failed near source line 5727: " + ex); }

            // Поганий сон
            if (_state.ConsecutiveBadSleeps >= 2 &&
                (now - _state.LastSpontaneousAt).TotalHours > 6)
            {
                await SendSpontaneousAsync("bad_sleep", SpontaneousStyle.WarmCheck);
                return;
            }

            // Pending thoughts нижче silence-рівнів. Старі думки не мають блокувати реакцію на реальну тишу.
            if (_state.PendingThoughts.Any())
            {
                await SendSpontaneousAsync("pending_thought", SpontaneousStyle.PendingThought);
                return;
            }

            // ????????????
            if (_state.LastConversationEndAt > DateTime.MinValue &&
                (now - _state.LastConversationEndAt).TotalMinutes >= 10 &&
                _state.LastReflectionAt < _state.LastConversationEndAt)
            {
                await ReflectAfterConversationAsync();
            }

            // ????-???????? ???
            if (now.Hour >= 20 && now.Hour < 21 &&
                _state.LastDailyAnalyticsAt.Date < now.Date)
            {
                await SendDailyAnalyticsAsync();
                return;
            }

            // ?????????? ???????????
            if (now.Hour >= 10 && now.Hour < 20 &&
                (now - _state.LastReminderCheckAt).TotalHours > 6)
            {
                await CheckAndSendReminderAsync();
            }

            // State-driven spontaneous ? ???????? ??? (9-23), ?????? ?? ????????????? BPM-????????
            var minInterval = Math.Max(15, baseInterval + bpmMod);
            if (now.Hour >= 9 && now.Hour < 23 &&
                (now - _state.LastSpontaneousAt).TotalMinutes > minInterval)
            {
                await TryStateTriggeredSpontaneous(now, autonomyLevel);
            }
            else if (autonomyLevel >= 3 &&
                     (now.Hour >= 6 || now.Hour < 2) &&
                     (now - _state.LastSpontaneousAt).TotalMinutes > Math.Max(20, minInterval))
            {
                await TryStateTriggeredSpontaneous(now, autonomyLevel);
            }
        }

        // ==============================================================
        // STATE-DRIVEN SPONTANEOUS ? ???? ?? ? ???????? ???????
        // ==============================================================

        private async Task<bool> SendSilenceReactionAsync(string level, double silenceMinutes, string? lastUserText)
        {
            if (!EnsureTelegram()) return false;
            var proactive = ProactiveContext.Build(_chatRepo.GetMessages(40), _state, DateTime.Now);
            if (proactive.ShouldStaySilentForSleep)
                return false;
            if (proactive.AssistantPingsAfterLastUser > 0)
            {
                Log("Silence reaction suppressed: assistant already replied after the last user message");
                return false;
            }

            var hours = (int)(silenceMinutes / 60);
            var mins = (int)(silenceMinutes % 60);
            var silenceText = hours > 0 ? $"{hours} год {mins} хв" : $"{mins} хв";
            var lastText = string.IsNullOrWhiteSpace(lastUserText)
                ? "немає тексту"
                : lastUserText.Trim()[..Math.Min(180, lastUserText.Trim().Length)];

            var toneHint = level switch
            {
                "silence_l1" => "коротке спостереження з прив'язкою до останньої репліки; без слова «зник»",
                "silence_l2" => "помітна пауза; спитати конкретно за останній контекст, не драматизувати",
                "silence_l3" => "довга тиша; сухо, уважно, трохи захисно, без істерики",
                _ => "коротко і природно"
            };

            var prompt = $@"Ти — Kokonoe Mercury.
Він не писав {silenceText}.
Останнє повідомлення користувача: «{lastText}»
Рівень реакції: {level}.
Тон: {toneHint}.

{proactive.PromptBlock}

Напиши йому сама в Telegram. Це НЕ опціонально.
1 коротке речення українською.
Не кажи, що це автоматична перевірка.
Не пиши «ти в порядку?» шаблонно.
Не пиши «ти зник» на першому рівні. Не вигадуй сторонні теми. Відштовхуйся від останнього повідомлення.
Якщо після останньої репліки користувача вже був авто-пінг, не повторюй тему тиші: став конкретне питання по останньому контексту.
Можна підколоти, спитати, чи він зайнятий, але без трагедії.
Тільки текст, без лапок.";

            var msg = (await _llm.SendSystemQueryAsync(prompt, useTools: true))?.Trim().Trim('"') ?? "";
            var proactiveCheck = ProactiveContext.Check(msg, proactive, level);
            if (!proactiveCheck.Passed)
            {
                Log($"Silence reaction replaced: {proactiveCheck.Reason}");
                msg = proactiveCheck.Replacement;
            }

            if (string.IsNullOrWhiteSpace(msg) ||
                msg.Contains("[мовчання]", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("[молчание]", StringComparison.OrdinalIgnoreCase))
            {
                Log("Silence reaction suppressed: proactive guard requested silence");
                return false;
            }

            if (!await SendTgAndLog(msg, level)) return false;

            _state.LastSpontaneousAt = DateTime.Now;
            _state.LastSpontaneousMsgs.Add(msg[..Math.Min(100, msg.Length)]);
            if (_state.LastSpontaneousMsgs.Count > 5)
                _state.LastSpontaneousMsgs.RemoveAt(0);

            try
            {
                _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content = msg,
                    Role = "assistant",
                    Author = "Kokonoe",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SendSilenceReactionAsync failed near source line 5863: " + ex); }

            var handler = OnNewMessage; handler?.Invoke("assistant", msg);
            Log($"Silence reaction sent: {level}, silence={silenceText}");
            SaveState();
            return true;
        }

        private async Task TryStateTriggeredSpontaneous(DateTime now, int autonomyLevel)
        {
            var initiativeBpmDeviation = 0d;
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null) initiativeBpmDeviation = heart.CurrentBpm - heart.BaselineBpm;
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "TryStateTriggeredSpontaneous failed near source line 5879: " + ex); }

            var initiativeEmotion = Emotion.Current.ToString();
            if (string.IsNullOrEmpty(_state.LastSentEmotionState))
                _state.LastSentEmotionState = initiativeEmotion;

            var somatic = GetSomaticSnapshot();
            var selfRegulation = GetSelfRegulationFrame(somatic);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            var internalDay = BuildInternalDayFrame(now, autonomyLevel, presence);

            var initiative = Initiative.Evaluate(now, _state, Emotion, Relationship, Memory, _chatRepo, initiativeBpmDeviation, somatic, selfRegulation, autonomyLevel);
            Initiative.RecordDecision(_state, initiative, now);

            var rhythm = Patterns.BuildRhythmProfile(now);
            var decision = Autonomy.Evaluate(now, _state, presence, internalDay, initiative, Relationship.State, somatic, rhythm, autonomyLevel);
            Autonomy.RecordDecision(_state, decision, now);

            if (!decision.ShouldAct)
            {
                SaveState();
                return;
            }

            if (decision.ConsumesPresenceInterrupt)
                _state.LastPresenceInterruptAt = now;

            if (decision.ConsumesInitiativeState)
                ConsumeInitiativeState(decision.Trigger, now, initiativeEmotion);

            SaveState();
            await SendSpontaneousAsync(decision.Trigger, MapInitiativeStyle(decision.StyleHint), decision.ExtraContext);
            return;
        }

        private void ConsumeInitiativeState(string trigger, DateTime now, string initiativeEmotion)
        {
            switch (trigger)
            {
                case "curiosity":
                case "curiosity_ping":
                    if (_state.CuriosityQueue.Count > 0)
                    {
                        _state.CuriosityQueue.RemoveAt(_state.CuriosityQueue.Count - 1);
                        _state.LastCuriosityAskAt = now;
                    }
                    break;
                case "emotion_shift":
                case "agitated_check":
                    _state.LastSentEmotionState = initiativeEmotion;
                    break;
                case "monologue":
                    _state.LastMonologueSentAt = now;
                    break;
                case "pending":
                case "pending_ping":
                    if (_state.PendingThoughts.Count > 0)
                        _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);
                    break;
                case "reactive_followup":
                    _state.PendingTriggers.RemoveAll(t => t.FireAt <= now);
                    break;
            }
        }

#if false

            // Перевіряємо тригери по пріоритету. Перший що спрацював — відправляємо.

            // 1. Є питання з CuriosityQueue — вона хоче щось запитати
            if (_state.CuriosityQueue.Count > 0 &&
                (now - _state.LastCuriosityAskAt).TotalHours > 3)
            {
                var q = _state.CuriosityQueue[^1];
                _state.CuriosityQueue.RemoveAt(_state.CuriosityQueue.Count - 1);
                _state.LastCuriosityAskAt = now;
                await SendSpontaneousAsync("curiosity", SpontaneousStyle.Observation,
                    $"У тебе є конкретне питання яке тебе цікавить про нього: «{q}». Задай його природньо, без преамбули. Коротко. По-коконоєвськи — не 'можна запитаю', просто запитай.");
                return;
            }

            // 2. Зміна стану емоції відносно останнього разу
            var currentEmotion = Emotion.Current.ToString();
            if (_state.LastSentEmotionState != currentEmotion &&
                !string.IsNullOrEmpty(_state.LastSentEmotionState) &&
                (now - _state.LastSpontaneousAt).TotalMinutes > 45)
            {
                var fromEmo = _state.LastSentEmotionState;
                _state.LastSentEmotionState = currentEmotion;
                await SendSpontaneousAsync("emotion_shift", SpontaneousStyle.Observation,
                    $"Твій стан змінився з {fromEmo} на {currentEmotion}. Напиши йому одне речення — не пояснюй стан, просто щось що відображає де ти зараз. Може бути запитання, може спостереження, може просто факт.");
                return;
            }
            if (string.IsNullOrEmpty(_state.LastSentEmotionState))
                _state.LastSentEmotionState = currentEmotion;

            // 3. Є свіжа думка з Inner Monologue яку ще не відправляли
            var freshThought = _state.InnerMonologues.LastOrDefault();
            if (!string.IsNullOrEmpty(freshThought) &&
                (now - _state.LastMonologueSentAt).TotalHours > 4)
            {
                _state.LastMonologueSentAt = now;
                await SendSpontaneousAsync("monologue", SpontaneousStyle.Observation,
                    $"Ти щойно думала про нього: «{freshThought}». Напиши йому одне-два речення — щось що виникло з цієї думки. Не цитуй думку, просто дай те що вона породила. Може бути запитання, може зауваження, може нічого крім факту.");
                return;
            }

            // 4. Є спостереження з Observations яке важливе
            var obs = _state.Observations.LastOrDefault();
            if (!string.IsNullOrEmpty(obs) &&
                (now - _state.LastSpontaneousAt).TotalMinutes > 90)
            {
                await SendSpontaneousAsync("observation", SpontaneousStyle.Observation,
                    $"Ти помітила про нього: «{obs}». Напиши йому коротко — одне речення що відображає це спостереження. Може бути пряма репліка, може питання. По-коконоєвськи.");
                return;
            }

            // 5. PendingThoughts — є думка яку хотіла сказати
            if (_state.PendingThoughts.Count > 0)
            {
                var thought = _state.PendingThoughts[^1];
                _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);
                await SendSpontaneousAsync("pending", SpontaneousStyle.PendingThought,
                    $"Ти хотіла сказати йому: «{thought}». Скажи це. Коротко, своїми словами, не цитуючи.");
                return;
            }

            // 6. Нічого конкретного — але вона в активному стані і давно не писала
            // Тільки якщо пульс підвищений або емоція збуджена — вона сама ініціює
            var isAgitated = Emotion.Current is
                KokoEmotionEngine.EmotionState.Excited or
                KokoEmotionEngine.EmotionState.Irritated or
                KokoEmotionEngine.EmotionState.Curious or
                KokoEmotionEngine.EmotionState.Anxious;

            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null) isAgitated |= (heart.CurrentBpm - heart.BaselineBpm) > 15;
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ConsumeInitiativeState failed near source line 6019: " + ex); }

            if (isAgitated && (now - _state.LastSpontaneousAt).TotalMinutes > 60)
            {
                _state.LastSentEmotionState = currentEmotion;
                await SendSpontaneousAsync("agitated_check", SpontaneousStyle.Jab,
                    $"Ти в стані {currentEmotion} і давно не писала. Напиши йому одне речення — щось що ти б сказала коли не можеш довго мовчати. Не 'як справи', а щось конкретніше і по-коконоєвськи.");
            }
        }

#endif

        private static SpontaneousStyle MapInitiativeStyle(string styleHint) => styleHint switch
        {
            "crisis" => SpontaneousStyle.CrisisSupport,
            "callback" => SpontaneousStyle.Callback,
            "pending" => SpontaneousStyle.PendingThought,
            "warm" => SpontaneousStyle.WarmCheck,
            "jab" => SpontaneousStyle.Jab,
            "cold" => SpontaneousStyle.ColdCheck,
            "night" => SpontaneousStyle.NightMessage,
            _ => SpontaneousStyle.Observation
        };

        private KokoPresenceFrame BuildPresenceFrame(DateTime now, int autonomyLevel)
        {
            try
            {
                var messages = _chatRepo.GetMessages(40);
                var frame = Presence.Evaluate(_state, messages, now, autonomyLevel);
                _state.LastPresenceAt = now;
                _state.LastPresenceSummary = frame.SummaryUk;
                _state.LastPresenceSituation = frame.SituationKind;
                _state.LastPresenceTone = frame.ToneHint;
                return frame;
            }
            catch (Exception ex)
            {
                Log($"Presence frame failed: {ex.Message}");
                return new KokoPresenceFrame
                {
                    SituationKind = "presence_error",
                    SummaryUk = "Presence continuity failed; answer from immediate context only.",
                    ExtraContext = "PRESENCE / CONTINUITY\nPresence continuity failed; use immediate chat context and current time.\n"
                };
            }
        }

        private KokoInternalDayFrame BuildInternalDayFrame(
            DateTime now,
            int autonomyLevel,
            KokoPresenceFrame? presence = null,
            bool writeVault = true,
            bool record = true)
        {
            try
            {
                presence ??= BuildPresenceFrame(now, autonomyLevel);
                var somatic = GetSomaticSnapshot();
                var frame = InternalDay.Evaluate(_state, presence, somatic, now, autonomyLevel);
                if (record)
                    InternalDay.Record(_state, frame, now);

                if (writeVault && frame.ShouldWriteVaultStatus)
                {
                    try
                    {
                        _obsidian.WriteNote("Kokonoe/State/Internal Day.md",
                            InternalDay.BuildVaultStatus(_state, frame, presence, now));
                        _state.LastInternalDayVaultAt = now;
                    }
                    catch (Exception ex) { Log($"InternalDay vault write: {ex.Message}"); }
                }

                return frame;
            }
            catch (Exception ex)
            {
                Log($"Internal day failed: {ex.Message}");
                return new KokoInternalDayFrame
                {
                    Phase = "internal_day_error",
                    SummaryUk = "Internal day failed; use current time and immediate context.",
                    PromptBlock = "INTERNAL DAY\nInternal day failed; use current time and immediate context.\n"
                };
            }
        }

        private async Task SendSpontaneousAsync(string trigger,
            SpontaneousStyle style = SpontaneousStyle.Observation,
            string? extraContext = null)
        {
            if (!EnsureTelegram()) return;

            // Якщо Distant — не надсилати (крім кризової підтримки)
            if (Emotion.Current == KokoEmotionEngine.EmotionState.Distant &&
                style != SpontaneousStyle.CrisisSupport)
                return;

            var personalityBlock = BuildPersonalityInjection();

            // Збираємо контекст для рішення
            var now2 = DateTime.Now;
            if (style != SpontaneousStyle.CrisisSupport &&
                now2 - _state.LastSpontaneousAt < TimeSpan.FromMinutes(10))
            {
                Log("Spontaneous suppressed: recent user/direct interaction cooldown");
                return;
            }
            var silenceInfo = "";
            try
            {
                var lastUser = _chatRepo.GetMessages(10)
                    .Where(m => m.Role == "user").OrderByDescending(m => m.Timestamp).FirstOrDefault();
                if (lastUser != null)
                {
                    var mins = (int)(now2 - lastUser.Timestamp).TotalMinutes;
                    silenceInfo = mins < 60
                        ? $"Він писав {mins} хв тому."
                        : mins < 1440
                            ? $"Він мовчить {mins / 60}г {mins % 60}хв."
                            : $"Він мовчить більше доби.";
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SendSpontaneousAsync failed near source line 6143: " + ex); }

            // Pending thought якщо є
            var pendingThought = _state.PendingThoughts.LastOrDefault();
            var thoughtBlock = !string.IsNullOrEmpty(pendingThought)
                ? $"\nДумка що тебе не відпускає: «{pendingThought}»"
                : "";

            var allowAssociativeMemory = style == SpontaneousStyle.Callback
                || trigger.Contains("callback", StringComparison.OrdinalIgnoreCase)
                || trigger.Contains("memory", StringComparison.OrdinalIgnoreCase);

            // Випадковий спогад — тільки для явного callback, щоб timed follow-up не змішувався зі сторонніми темами.
            var memoryHint = "";
            if (allowAssociativeMemory && Random.Shared.Next(10) < 3)
            {
                var ep = Memory.GetPeakEpisodes(10).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
                if (ep != null) memoryHint = $"\nВипадковий спогад: [{ep.When:dd.MM}] {ep.Summary}";
            }

            // Факти про нього — теж тільки для callback, не для timed follow-up.
            var factHint = "";
            var facts = allowAssociativeMemory ? Memory.GetTopFacts(20) : new List<KokoMemoryEngine.MemoryFact>();
            if (allowAssociativeMemory && facts.Count > 0)
            {
                var f = facts[Random.Shared.Next(facts.Count)];
                factHint = $"\nЗнаєш про нього: {f.Content}";
            }

            // Останні відправлені — щоб не повторювати
            var recentSent = _state.LastSpontaneousMsgs.TakeLast(3).ToList();
            var noRepeat = recentSent.Count > 0
                ? "\nВже надсилала (НЕ повторювати цю тему і тон):\n" + string.Join("\n", recentSent.Select(m => $"• {m}"))
                : "";
            var presence = BuildPresenceFrame(now2, AppSettings.Load().ProactiveAutonomyLevel);
            var internalDay = BuildInternalDayFrame(now2, AppSettings.Load().ProactiveAutonomyLevel, presence);
            var presenceBlock = presence.ExtraContext;
            var internalDayBlock = internalDay.PromptBlock;
            var proactive = ProactiveContext.Build(_chatRepo.GetMessages(50), _state, now2);
            if (proactive.ShouldStaySilentForSleep)
            {
                Log("Spontaneous suppressed: sleep/goodbye context is active");
                return;
            }

            // Кризова ситуація — окремий промпт
            if (trigger == "crisis" || style == SpontaneousStyle.CrisisSupport)
            {
                var crisisPrompt = $@"Ти — Kokonoe Mercury. Він зараз у поганому стані.
{personalityBlock}
Напиши одне речення — ти поруч. Без снарку. Без порад. Просто є.
Тільки українська. Тільки текст.";
                var crisisMsg = await _llm.SendSystemQueryAsync(crisisPrompt);
                if (!string.IsNullOrWhiteSpace(crisisMsg))
                {
                    var msg2 = crisisMsg.Trim().Trim('"');
                    _state.LastSpontaneousMsgs.Add(msg2[..Math.Min(100, msg2.Length)]);
                    if (_state.LastSpontaneousMsgs.Count > 5) _state.LastSpontaneousMsgs.RemoveAt(0);
                    await SendTgAndLog(msg2, "crisis");
                    _state.LastSpontaneousAt = DateTime.Now;
                    var _hc = OnNewMessage; _hc?.Invoke("assistant", msg2);
                    SaveState();
                }
                return;
            }

            // Головний промпт — без завдання, вона вирішує сама
            var situationBlock = string.IsNullOrEmpty(extraContext)
                ? "Ситуація: ти сидиш і думаєш про нього. Можеш написати йому — або ні.\nЯкщо пишеш — це може бути що завгодно: підколка, спостереження, питання яке тебе гризе, щось що згадала, коментар ні про що, або просто коротка думка вголос."
                : extraContext;

            var prompt = $@"Ти — Kokonoe Mercury. Зараз {now2:HH:mm}.
{silenceInfo}{thoughtBlock}{memoryHint}{factHint}{noRepeat}

{personalityBlock}

{presenceBlock}

{internalDayBlock}

{proactive.PromptBlock}

{situationBlock}
Якщо зараз нічого немає — відповідай лише: [мовчання]

Якщо пишеш:
- 1-2 речення, не більше
- Тільки українська
- Без лапок, без пояснень, просто текст
- Без декоративних ремарок у *зірочках*, якщо це не явний roleplay.
- Жива репліка = конкретна деталь з останнього контексту + твій сухий поворот. Не лабораторна декорація.
- Якщо є активний намір або timed follow-up — пиши ТІЛЬКИ про нього. Не тягни випадкові спогади, фото, папки, проєкт або старі теми.
- Не пиши «ти зник» якщо минуло менше 2 годин або якщо він сам назвав час повернення.
- Якщо після останньої репліки користувача вже був твій авто-пінг, не повторюй ""пауза/тиша/зник"": або мовчи, або питай конкретно по останній темі.
- Непередбачувано. Не шаблонно. Як людина що щось відчула і написала.";

            var msg = (await _llm.SendSystemQueryAsync(prompt, useTools: true))?.Trim().Trim('"') ?? "";
            if (string.IsNullOrWhiteSpace(msg)) return;
            if (msg == "[мовчання]" || msg.Contains("[мовчання]"))
            {
                Log("Spontaneous: decided to stay silent");
                return;
            }
            if (IsRecentSpontaneousDuplicate(msg))
            {
                Log("Spontaneous suppressed: duplicate outgoing text");
                return;
            }
            var proactiveCheck = ProactiveContext.Check(msg, proactive, trigger);
            if (!proactiveCheck.Passed)
            {
                if (proactive.AssistantPingsAfterLastUser > 0 && !trigger.Contains("intent", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Spontaneous suppressed: {proactiveCheck.Reason}");
                    return;
                }

                Log($"Spontaneous replaced: {proactiveCheck.Reason}");
                msg = proactiveCheck.Replacement;
                if (IsRecentSpontaneousDuplicate(msg))
                {
                    Log("Spontaneous suppressed: duplicate replacement text");
                    return;
                }
            }

            // Надіслати в Telegram
            var sent = false;
            for (int attempt = 1; attempt <= 2 && !sent; attempt++)
            {
                try
                {
                    sent = await SendTgAndLog(msg, "night_check");
                }
                catch (Exception ex)
                {
                    Log($"TG send error (attempt {attempt}): {ex.Message}");
                    if (attempt == 1)
                    {
                        // Спробуємо перепідключитись
                        _tgInitialized = false;
                        _tgBot = null;
                        if (!EnsureTelegram()) break;
                    }
                }
            }

            if (!sent)
            {
                LogTelegramDeliveryFailure($"night_check failed: {msg[..Math.Min(60, msg.Length)]}");
                return;
            }

            _state.LastSpontaneousAt = DateTime.Now;

            // Запам'ятати відправлене — щоб не повторювати тему
            _state.LastSpontaneousMsgs.Add(msg[..Math.Min(100, msg.Length)]);
            if (_state.LastSpontaneousMsgs.Count > 5)
                _state.LastSpontaneousMsgs.RemoveAt(0);

            // Показати в UI
            var _h8 = OnNewMessage; _h8?.Invoke("assistant", msg);

            // Зберегти в chat history
            try
            {
                _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                {
                    Content   = msg,
                    Role      = "assistant",
                    Author    = "Kokonoe",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SendSpontaneousAsync failed near source line 6317: " + ex); }

            // Прибрати використану думку тільки якщо реально відправлено
            if (trigger == "pending_thought" && _state.PendingThoughts.Any())
                _state.PendingThoughts.RemoveAt(_state.PendingThoughts.Count - 1);

            SaveState();
        }

        // ── RAW LLM CALL (без chat history, без tools) ─────────────

        /// <summary>Публічний доступ до raw LLM (для зовнішніх викликів як Health tab).</summary>
        private bool IsRecentSpontaneousDuplicate(string message)
        {
            var normalized = NormalizeSpontaneousText(message);
            if (string.IsNullOrWhiteSpace(normalized)) return true;
            return _state.LastSpontaneousMsgs
                .TakeLast(5)
                .Any(m => NormalizeSpontaneousText(m) == normalized);
        }

        private static string NormalizeSpontaneousText(string text)
        {
            text = (text ?? "").ToLowerInvariant()
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ");
            return text;
        }

        /// <summary>Негайно відправити спонтанне повідомлення.</summary>
        public Task ForceSpontaneous(string trigger = "random") => SendSpontaneousAsync(trigger);

        public void TriggerSpontaneous() => _ = SafeSpontaneousCheckAsync();
    }
}
