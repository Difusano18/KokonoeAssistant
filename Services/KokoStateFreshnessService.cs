using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoStateFreshnessResult
    {
        public bool Changed { get; set; }
        public int ExpiredIntentCount { get; set; }
        public int ResolvedIntentCount { get; set; }
        public int RemovedIntentCount { get; set; }
        public int ActiveIntentCount { get; set; }
        public string SummaryUk { get; set; } = "";
    }

    public sealed class KokoStateReconciliationSignals
    {
        public string Channel { get; set; } = "";
        public string ScreenMode { get; set; } = "";
        public string ScreenSummary { get; set; } = "";
        public DateTime LastDesktopActivityAt { get; set; } = DateTime.MinValue;
    }

    public sealed class KokoStateFreshnessService
    {
        public KokoStateFreshnessResult Refresh(
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            DateTime now,
            KokoStateReconciliationSignals? signals = null)
        {
            var result = new KokoStateFreshnessResult();

            var lastUser = messages
                .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            if (lastUser != null)
            {
                result.ResolvedIntentCount += ResolveFromLatestUserSignal(state, lastUser, now);
                result.ResolvedIntentCount += ResolveStaleIntentFromLaterUserActivity(state, lastUser, now);
            }

            if (signals != null)
                result.ResolvedIntentCount += ResolveFromExternalSignals(state, signals, now);

            result.ExpiredIntentCount += ExpireOverdueIntents(state, now);

            var removed = state.ShortTermIntents.RemoveAll(i =>
                i.ResolvedAt.HasValue && now - i.ResolvedAt.Value > TimeSpan.FromDays(2));
            result.RemovedIntentCount += removed;

            if (state.ShortTermIntents.Count > 16)
            {
                var extra = state.ShortTermIntents.Count - 16;
                state.ShortTermIntents.RemoveRange(0, extra);
                result.RemovedIntentCount += extra;
            }

            result.ActiveIntentCount = state.ShortTermIntents.Count(i => !i.ResolvedAt.HasValue);
            result.Changed = result.ResolvedIntentCount > 0 || result.ExpiredIntentCount > 0 || result.RemovedIntentCount > 0;
            result.SummaryUk = BuildSummary(result, lastUser, now);
            return result;
        }

        private static int ResolveFromExternalSignals(KokoInternalState state, KokoStateReconciliationSignals signals, DateTime now)
        {
            var mode = (signals.ScreenMode ?? "").ToLowerInvariant();
            var summary = (signals.ScreenSummary ?? "").ToLowerInvariant();
            var channel = (signals.Channel ?? "").ToLowerInvariant();
            var desktopActive = signals.LastDesktopActivityAt > DateTime.MinValue &&
                                now - signals.LastDesktopActivityAt < TimeSpan.FromMinutes(20);
            var activeNonAwayScreen = desktopActive || ContainsAny(mode, "coding", "obsidian", "telegram", "browser", "game", "desktop") ||
                                      ContainsAny(summary, "код", "obsidian", "telegram", "браузер", "гра", "вікно", "редактор");

            if (!activeNonAwayScreen && channel != "telegram" && channel != "desktop")
                return 0;

            var count = 0;
            foreach (var intent in state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue).ToList())
            {
                if (intent.Kind == "sleep")
                {
                    if (!CanResolveSleepFromExternalSignals(intent, mode, summary, channel, desktopActive, now, out var sleepReason))
                    {
                        LogSleep($"protected active sleep intent from passive signal; source={channel}/{mode}; reason={sleepReason}");
                        continue;
                    }

                    MarkResolved(intent, now,
                        $"sleep deactivated by high-importance screen activity after expected window: {channel}/{mode}; {Trim(signals.ScreenSummary, 120)}");
                    count++;
                    continue;
                }

                if (now < intent.ExpectedUntil.AddMinutes(10) && !desktopActive)
                    continue;

                if (intent.Kind is "course" or "return_home" or "errand" or "walk" or "busy" or "work" or "sleep")
                {
                    MarkResolved(intent, now, $"auto-state-reconcile: {channel}/{mode} activity superseded stale {intent.Kind} intent");
                    count++;
                }
            }
            return count;
        }

        private static int ResolveFromLatestUserSignal(KokoInternalState state, ChatRepository.ChatMessage lastUser, DateTime now)
        {
            var lower = (lastUser.Content ?? "").ToLowerInvariant();
            if (!LooksLikeReturnOrWakeSignal(lower))
                return 0;

            var count = 0;
            foreach (var intent in state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue))
            {
                if (intent.Kind == "sleep" && !LooksLikeExplicitWakeSignal(lower))
                    continue;

                MarkResolved(intent, now, $"auto-state-refresh: latest user signal closed it: {Trim(lastUser.Content, 160)}");
                count++;
            }
            return count;
        }

        private static int ResolveStaleIntentFromLaterUserActivity(KokoInternalState state, ChatRepository.ChatMessage lastUser, DateTime now)
        {
            var content = lastUser.Content ?? "";
            var lower = content.ToLowerInvariant();
            var count = 0;

            foreach (var intent in state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue).ToList())
            {
                if (intent.Kind == "sleep")
                    continue;

                if (lastUser.Timestamp <= intent.CreatedAt.AddMinutes(1))
                    continue;

                if (lastUser.Timestamp < intent.ExpectedUntil.AddMinutes(10))
                    continue;

                if (LooksLikeSameIntentContinuation(lower, intent.Kind))
                    continue;

                MarkResolved(intent, now, $"auto-state-refresh: later user activity superseded stale {intent.Kind} intent: {Trim(content, 160)}");
                count++;
            }

            return count;
        }

        private static int ExpireOverdueIntents(KokoInternalState state, DateTime now)
        {
            var count = 0;
            foreach (var intent in state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue).ToList())
            {
                if (intent.Kind == "sleep")
                    continue;

                var grace = GraceFor(intent.Kind);
                var maxAge = MaxAgeFor(intent.Kind);
                var expiredByWindow = now > intent.ExpectedUntil + grace;
                var expiredByAge = now - intent.CreatedAt > maxAge;

                if (!expiredByWindow && !expiredByAge)
                    continue;

                MarkResolved(intent, now, "auto-state-refresh: intent window expired");
                count++;
            }
            return count;
        }

        private static bool CanResolveSleepFromExternalSignals(
            ShortTermIntent intent,
            string mode,
            string summary,
            string channel,
            bool desktopActive,
            DateTime now,
            out string reason)
        {
            var minLockUntil = intent.CreatedAt.AddHours(4);
            if (now < minLockUntil)
            {
                reason = $"minimum sleep lock until {minLockUntil:HH:mm}";
                return false;
            }

            if (now < intent.ExpectedUntil)
            {
                reason = $"expected sleep window still active until {intent.ExpectedUntil:HH:mm}";
                return false;
            }

            if (!desktopActive)
            {
                reason = "no fresh desktop activity";
                return false;
            }

            if (!LooksLikeHighImportanceWakeScreen(mode, summary, channel))
            {
                reason = "screen signal is passive or low-importance";
                return false;
            }

            reason = "high-importance screen activity after expected sleep window";
            return true;
        }

        private static bool LooksLikeHighImportanceWakeScreen(string mode, string summary, string channel)
        {
            var text = $"{mode} {summary} {channel}".ToLowerInvariant();
            if (ContainsAny(text, "idle", "same", "static", "video", "youtube", "music", "background", "lockscreen"))
                return false;

            return ContainsAny(text,
                "coding", "ide", "visual studio", "vscode", "build", "debug", "error", "exception",
                "obsidian", "editing", "typing", "active", "changed", "commit", "game", "telegram active",
                "РєРѕРґ", "Р°РєС‚РёРІ", "Р·РјС–РЅ", "РїРёС€Рµ", "СЂРµРґР°РєС‚РѕСЂ");
        }

        private static void MarkResolved(ShortTermIntent intent, DateTime now, string reason)
        {
            intent.ResolvedAt = now;
            intent.ResolutionText = reason;
            if (intent.Kind == "sleep")
                LogSleep("deactivated: " + reason);
        }

        private static void LogSleep(string message)
        {
            try { KokoSystemLog.Write("SLEEP_INTENT", message); }
            catch { }
        }

        private static TimeSpan GraceFor(string kind) => kind switch
        {
            "return_home" => TimeSpan.FromMinutes(75),
            "course" => TimeSpan.FromMinutes(45),
            "errand" => TimeSpan.FromMinutes(90),
            "walk" => TimeSpan.FromHours(2),
            "work" => TimeSpan.FromHours(4),
            "busy" => TimeSpan.FromHours(2),
            "sleep" => TimeSpan.FromHours(2),
            _ => TimeSpan.FromHours(2)
        };

        private static TimeSpan MaxAgeFor(string kind) => kind switch
        {
            "sleep" => TimeSpan.FromHours(14),
            "work" => TimeSpan.FromHours(12),
            "course" => TimeSpan.FromHours(4),
            "return_home" => TimeSpan.FromHours(6),
            _ => TimeSpan.FromHours(6)
        };

        private static bool LooksLikeSameIntentContinuation(string lower, string kind)
        {
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var stillAway = ContainsAny(lower, "ще", "досі", "поки", "буду", "йду", "іду", "піду", "пішов", "зараз");
            return kind switch
            {
                "course" => stillAway && ContainsAny(lower, "курс", "занят", "урок", "пара", "навчан"),
                "work" => stillAway && ContainsAny(lower, "робот", "прац", "код", "проєкт", "проект"),
                "errand" => stillAway && ContainsAny(lower, "магаз", "куп", "продукт", "справ"),
                "walk" => stillAway && ContainsAny(lower, "гуля", "прогуля", "вулиц"),
                "return_home" => stillAway && ContainsAny(lower, "додому", "дом", "дороз", "їду", "йду", "іду"),
                "busy" => stillAway && ContainsAny(lower, "зайнят", "потім", "не можу"),
                _ => false
            };
        }

        private static bool LooksLikeReturnOrWakeSignal(string lower)
            => ContainsAny(lower,
                "\u043f\u0440\u043e\u043a\u0438\u043d", "\u043f\u0440\u043e\u0441\u043d\u0443\u0432", "\u043f\u043e\u0441\u043f\u0430\u0432", "\u0432\u0441\u0442\u0430\u0432", "\u044f \u0442\u0443\u0442", "\u0440\u0430\u043d\u043a\u0443",
                "\u0432\u0435\u0440\u043d\u0443\u0432", "\u043f\u043e\u0432\u0435\u0440\u043d\u0443\u0432", "\u043f\u0440\u0438\u0439\u0448\u043e\u0432", "\u043f\u0440\u0438\u0439\u0448\u043b\u0430", "\u0432\u0436\u0435 \u0432\u0434\u043e\u043c\u0430",
                "\u0437\u0430\u043a\u0456\u043d\u0447\u0438\u0432", "\u0437\u0430\u043a\u0456\u043d\u0447\u0438\u043b\u0438\u0441\u044c", "\u0437\u0430\u043a\u0456\u043d\u0447\u0438\u043b\u043e\u0441\u044f", "\u043f\u0440\u0438\u0457\u0445\u0430\u0432", "\u0434\u043e\u0457\u0445\u0430\u0432",
                "прокин", "проснув", "поспав", "встав", "я тут","ранку",
                "вернув", "повернув", "прийшов", "прийшла", "вже вдома",
                "закінчив", "закінчились", "закінчилося", "приїхав", "доїхав");

        private static bool LooksLikeExplicitWakeSignal(string lower)
            => ContainsAny(lower,
                "прокин", "проснув", "поспав", "встав", "я встав", "я прокинув", "я проснув",
                "woke", "wake", "awake", "i am up", "im up",
                "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ");

        private static string BuildSummary(KokoStateFreshnessResult result, ChatRepository.ChatMessage? lastUser, DateTime now)
        {
            if (!result.Changed)
                return $"стан свіжий; активних намірів {result.ActiveIntentCount}; перевірено {now:dd.MM HH:mm}";

            var parts = new List<string>();
            if (result.ResolvedIntentCount > 0) parts.Add($"закрито сигналом користувача {result.ResolvedIntentCount}");
            if (result.ExpiredIntentCount > 0) parts.Add($"прострочено {result.ExpiredIntentCount}");
            if (result.RemovedIntentCount > 0) parts.Add($"прибрано старих {result.RemovedIntentCount}");
            if (lastUser != null) parts.Add($"остання репліка {Math.Max(0, (int)(now - lastUser.Timestamp).TotalMinutes)} хв тому");
            return string.Join("; ", parts);
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}
