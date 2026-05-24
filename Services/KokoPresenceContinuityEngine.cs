using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoPresenceFrame
    {
        public string SituationKind { get; set; } = "idle";
        public string Trigger { get; set; } = "presence_idle";
        public string StyleHint { get; set; } = "observation";
        public string ToneHint { get; set; } = "dry, attentive, not generic";
        public string SummaryUk { get; set; } = "";
        public string LastUserText { get; set; } = "";
        public double SilenceMinutes { get; set; }
        public int Priority { get; set; }
        public bool ShouldInterrupt { get; set; }
        public DateTime NextUsefulAt { get; set; } = DateTime.MinValue;
        public string ExtraContext { get; set; } = "";
    }

    public sealed class KokoPresenceContinuityEngine
    {
        public void ObserveUserMessage(KokoInternalState state, string content, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var lower = content.ToLowerInvariant();
            state.LastPresenceAt = now;
            state.LastPresenceSummary = BuildObservation(content, lower, now);
            state.LastPresenceSituation = InferSituation(lower);
            state.LastPresenceTone = InferTone(lower);
            RememberTrace(state, $"{now:dd.MM HH:mm} user:{state.LastPresenceSituation} - {Trim(content, 120)}");
        }

        public KokoPresenceFrame Evaluate(
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            DateTime now,
            int autonomyLevel = 2,
            SystemInfo? systemInfoOverride = null,
            KokoScreenAwarenessAnalysis? screenAnalysisOverride = null)
        {
            autonomyLevel = Math.Clamp(autonomyLevel, 0, 3);
            var lastUser = messages
                .Where(m => m.Role == "user")
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            var sysInfo = systemInfoOverride ?? ServiceContainer.PcControl.GetSystemInfo();
            var idleMinutes = sysInfo.IdleTime.TotalMinutes;

            var frame = new KokoPresenceFrame
            {
                SituationKind = "idle",
                Trigger = "presence_idle",
                StyleHint = "observation",
                ToneHint = "dry, attentive, not generic",
                SummaryUk = "Немає свіжого сигналу, який вартий втручання.",
                Priority = 0,
                ExtraContext = $"[PC_IDLE: {idleMinutes:F1}m]"
            };

            if (lastUser == null)
            {
                frame.SummaryUk = "Історія порожня; не вигадуй близькість з повітря.";
                frame.ExtraContext = BuildPromptBlock(frame, state, now, null);
                return frame;
            }

            frame.LastUserText = lastUser.Content;
            frame.SilenceMinutes = Math.Max(0, (now - lastUser.Timestamp).TotalMinutes);

            var activeIntent = state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderByDescending(i => i.FollowUpAt)
                .FirstOrDefault();

            if (activeIntent != null)
            {
                ApplyIntentContinuity(frame, activeIntent, now, autonomyLevel);
                frame.ExtraContext = BuildPromptBlock(frame, state, now, activeIntent);
                return frame;
            }

            var resolvedIntent = state.ShortTermIntents
                .Where(i => i.ResolvedAt.HasValue && now - i.ResolvedAt.Value < TimeSpan.FromHours(3))
                .OrderByDescending(i => i.ResolvedAt)
                .FirstOrDefault();

            if (resolvedIntent != null)
            {
                ApplyResolvedIntent(frame, resolvedIntent, now);
                frame.ExtraContext = BuildPromptBlock(frame, state, now, resolvedIntent);
                return frame;
            }

            ApplySilenceContinuity(frame, state, now, autonomyLevel, sysInfo, screenAnalysisOverride);
            frame.ExtraContext = BuildPromptBlock(frame, state, now, null);
            return frame;
        }

        public string BuildDebugBlock(KokoInternalState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PRESENCE CONTINUITY ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(state.LastPresenceSummary)
                ? "last_presence=none"
                : $"last_presence={state.LastPresenceSummary}");
            sb.AppendLine($"situation={state.LastPresenceSituation}");
            sb.AppendLine($"tone={state.LastPresenceTone}");
            if (state.PresenceTrace.Count > 0)
            {
                sb.AppendLine("recent_presence_trace:");
                foreach (var item in state.PresenceTrace.TakeLast(5))
                    sb.AppendLine($"- {item}");
            }
            sb.AppendLine("rule: account for elapsed time before answering. Never tell him to sleep if he has already returned or woken up.");
            return sb.ToString();
        }

        private static void ApplyIntentContinuity(KokoPresenceFrame frame, ShortTermIntent intent, DateTime now, int autonomyLevel)
        {
            var until = intent.ExpectedUntil - now;
            var followDue = now >= intent.FollowUpAt;
            var overdue = now >= intent.ExpectedUntil;

            frame.SituationKind = overdue ? "overdue_intent" : followDue ? "due_intent_followup" : "active_absence";
            frame.Trigger = overdue ? "presence_intent_overdue" : "presence_intent_followup";
            frame.StyleHint = intent.Kind == "sleep" ? "warm" : "callback";
            frame.Priority = overdue ? 94 : followDue ? 88 : 42;
            frame.ShouldInterrupt = intent.Kind == "sleep"
                ? false
                : autonomyLevel >= 3
                    ? followDue
                    : overdue;
            frame.NextUsefulAt = followDue ? now : intent.FollowUpAt;

            var timeText = until.TotalMinutes >= 0
                ? $"очікуване вікно ще триває приблизно {FormatDuration(until)}"
                : $"очікуване вікно минуло {FormatDuration(-until)} тому";

            frame.ToneHint = intent.Kind switch
            {
                "sleep" when overdue => "he may have woken up; do not tell him to sleep, ask like you noticed the time",
                "sleep" => "let him sleep unless he writes first; no nagging",
                "course" when overdue => "ask whether the courses ended; dry, specific, mildly mocking",
                "course" => "remember he is at courses; do not ask random nonsense",
                "return_home" when overdue => "ask if he got home; specific, no generic disappearance complaint",
                "return_home" => "he said when he will be home; wait until that time, do not nag early",
                "work" => "work-aware, concise, do not break focus unless due",
                _ => "specific follow-up, not generic checking"
            };

            frame.SummaryUk = $"Активний намір: {intent.Summary}; {timeText}.";
        }

        private static void ApplyResolvedIntent(KokoPresenceFrame frame, ShortTermIntent intent, DateTime now)
        {
            frame.SituationKind = "returned_after_intent";
            frame.Trigger = "presence_returned";
            frame.StyleHint = intent.Kind == "sleep" ? "jab" : "callback";
            frame.ToneHint = intent.Kind == "sleep"
                ? "he is back after sleep; do not tell him to sleep, react to the return"
                : "he returned after an earlier plan; acknowledge elapsed time";
            frame.Priority = 50;
            frame.ShouldInterrupt = false;
            frame.SummaryUk = $"Намір уже закритий: {intent.Summary}. Він повернувся/оновив стан {FormatDuration(now - intent.ResolvedAt!.Value)} тому.";
        }

        private static void ApplySilenceContinuity(
            KokoPresenceFrame frame,
            KokoInternalState state,
            DateTime now,
            int autonomyLevel,
            SystemInfo sysInfo,
            KokoScreenAwarenessAnalysis? screenAnalysisOverride)
        {
            var silence = frame.SilenceMinutes;
            var idleMinutes = sysInfo.IdleTime.TotalMinutes;
            var screen = BuildScreenPresenceSignal(state, now, screenAnalysisOverride);

            if (idleMinutes < 1.0 && silence > 5 && silence < 360)
            {
                frame.SituationKind = "physically_present";
                frame.Trigger = "presence_pc_active_chat_silent";
                frame.StyleHint = "observation";
                frame.ToneHint = "user is physically active at PC but silent in chat; do not treat this as absence";
                frame.Priority = 28;
                frame.ShouldInterrupt = false;
                frame.SummaryUk = $"ПК активний: ввід був {FormatDuration(sysInfo.IdleTime)} тому, але в чаті тиша {FormatDuration(TimeSpan.FromMinutes(silence))}.";
                return;
            }

            if (idleMinutes >= 5.0 && screen.HasRecentContext)
            {
                if (screen.HasActiveContent)
                {
                    frame.SituationKind = "physically_present";
                    frame.Trigger = "presence_screen_active_idle_input";
                    frame.StyleHint = "observation";
                    frame.ToneHint = "input is idle, but screen content is active; assume watching/reading/playing, not away";
                    frame.Priority = 48;
                    frame.ShouldInterrupt = false;
                    frame.SummaryUk = $"Ввід неактивний {FormatDuration(sysInfo.IdleTime)}, але екран активний ({screen.Mode}/{screen.Activity}). Ймовірно, він дивиться або читає, а не відійшов.";
                    return;
                }

                if (screen.HasInactiveScreen)
                {
                    frame.SituationKind = "away";
                    frame.Trigger = "presence_screen_idle_away";
                    frame.StyleHint = "distant";
                    frame.ToneHint = "user is likely away; do background work, do not ping with where-are-you messages";
                    frame.Priority = 18;
                    frame.ShouldInterrupt = false;
                    frame.SummaryUk = $"Ввід неактивний {FormatDuration(sysInfo.IdleTime)} і екран без активності ({screen.Mode}/{screen.Activity}). Схоже, він відійшов від ПК.";
                    return;
                }
            }

            // AWAY detection (Idle > 15m)
            if (idleMinutes >= 15.0)
            {
                frame.SituationKind = "away";
                frame.Trigger = "presence_away";
                frame.StyleHint = "distant";
                frame.ToneHint = "user is away from PC; do not disturb with small talk, use for background tasks";
                frame.Priority = 20;
                frame.ShouldInterrupt = false;
                frame.SummaryUk = $"Він відійшов від ПК ({FormatDuration(sysInfo.IdleTime)} без вводу).";
                return;
            }

            // IDLE detection (Idle 2-15m)
            if (idleMinutes >= 2.0)
            {
                frame.SituationKind = "idle_staring";
                frame.Trigger = "presence_idle_staring";
                frame.StyleHint = "observation";
                frame.ToneHint = "user is at PC but idle; maybe staring at something? check screen if possible";
                frame.Priority = 55;
                frame.ShouldInterrupt = autonomyLevel >= 2 && now - state.LastPresenceInterruptAt > TimeSpan.FromMinutes(30);
                frame.SummaryUk = $"Він за комп'ютером, але не активний {FormatDuration(sysInfo.IdleTime)}.";
                return;
            }

            if (silence >= 360)
            {
                frame.SituationKind = "long_silence";
                frame.Trigger = "presence_long_silence";
                frame.StyleHint = "warm";
                frame.ToneHint = "long absence; dry concern, no melodrama";
                frame.Priority = 72;
                frame.ShouldInterrupt = autonomyLevel >= 3 && now - state.LastPresenceInterruptAt > TimeSpan.FromHours(8);
                frame.NextUsefulAt = now;
                frame.SummaryUk = $"Довга тиша в чаті: {FormatDuration(TimeSpan.FromMinutes(silence))}.";
                return;
            }

            if (silence < 25)
            {
                frame.SituationKind = "recent_contact";
                frame.SummaryUk = $"Він писав {FormatDuration(TimeSpan.FromMinutes(silence))} тому; не треба вдавати драму.";
                frame.Priority = 10;
                return;
            }


            if (silence >= 90)
            {
                frame.SituationKind = "medium_silence";
                frame.Trigger = "presence_medium_silence";
                frame.StyleHint = "jab";
                frame.ToneHint = "notice the absence; one specific jab or question";
                frame.Priority = 62;
                frame.ShouldInterrupt = autonomyLevel >= 3 && now - state.LastPresenceInterruptAt > TimeSpan.FromHours(3);
                frame.NextUsefulAt = now;
                frame.SummaryUk = $"Середня тиша в чаті: {FormatDuration(TimeSpan.FromMinutes(silence))}.";
                return;
            }

            frame.SituationKind = "short_silence";
            frame.Trigger = "presence_short_silence";
            frame.StyleHint = "observation";
            frame.ToneHint = "light presence; do not overreact";
            frame.Priority = 32;
            frame.ShouldInterrupt = false;
            frame.SummaryUk = $"Коротка тиша в чаті: {FormatDuration(TimeSpan.FromMinutes(silence))}.";
        }

        private static ScreenPresenceSignal BuildScreenPresenceSignal(
            KokoInternalState state,
            DateTime now,
            KokoScreenAwarenessAnalysis? overrideAnalysis)
        {
            var signal = new ScreenPresenceSignal();
            if (overrideAnalysis != null)
            {
                signal.HasRecentContext = true;
                signal.Mode = KokoScreenAwarenessService.NormalizeMode(
                    overrideAnalysis.ScreenMode,
                    $"{overrideAnalysis.SummaryUk} {overrideAnalysis.ActivityUk} {overrideAnalysis.CurrentTask}");
                signal.Activity = overrideAnalysis.ActivityUk ?? "";
                signal.Progress = overrideAnalysis.Progress ?? "";
                signal.Text = $"{overrideAnalysis.SummaryUk} {overrideAnalysis.ActivityUk} {overrideAnalysis.CurrentTask} {overrideAnalysis.Progress}".ToLowerInvariant();
                return EnrichScreenPresenceSignal(signal);
            }

            if (state.LastScreenAwarenessAt <= DateTime.MinValue ||
                now - state.LastScreenAwarenessAt > TimeSpan.FromMinutes(45))
                return signal;

            signal.HasRecentContext = true;
            signal.Mode = KokoScreenAwarenessService.NormalizeMode(
                state.LastScreenAwarenessMode,
                $"{state.LastScreenAwarenessWindow} {state.LastScreenAwarenessSummary} {state.LastScreenAwarenessActivity}");
            signal.Activity = state.LastScreenAwarenessActivity ?? "";
            signal.Progress = state.LastScreenSituationProgress ?? "";
            signal.Text = $"{state.LastScreenAwarenessWindow} {state.LastScreenAwarenessSummary} {state.LastScreenAwarenessActivity} {state.LastScreenSituationTask} {state.LastScreenSituationProgress}".ToLowerInvariant();
            return EnrichScreenPresenceSignal(signal);
        }

        private static ScreenPresenceSignal EnrichScreenPresenceSignal(ScreenPresenceSignal signal)
        {
            var contentMode = signal.Mode is not ("idle" or "desktop" or "private" or "unknown" or "");
            var active = ContainsAny(signal.Text, "active", "changed", "moving", "switching", "актив", "змін", "рух", "гра", "video", "youtube") ||
                         signal.Progress is "moving" or "switching";
            var stuckContent = contentMode &&
                               (ContainsAny(signal.Text, "stuck", "завис", "exception", "error", "debug") ||
                                signal.Progress == "stuck");
            var inactiveDesktop = ContainsAny(signal.Text, "idle", "same", "без змін", "desktop") ||
                                  signal.Progress == "idle";

            signal.HasActiveContent = contentMode && (active || stuckContent);
            signal.HasInactiveScreen = (inactiveDesktop && !contentMode) || signal.Mode is "idle" or "desktop";
            return signal;
        }

        private sealed class ScreenPresenceSignal
        {
            public bool HasRecentContext { get; set; }
            public bool HasActiveContent { get; set; }
            public bool HasInactiveScreen { get; set; }
            public string Mode { get; set; } = "";
            public string Activity { get; set; } = "";
            public string Progress { get; set; } = "";
            public string Text { get; set; } = "";
        }

        private static string BuildPromptBlock(KokoPresenceFrame frame, KokoInternalState state, DateTime now, ShortTermIntent? intent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PRESENCE / CONTINUITY");
            sb.AppendLine($"Зараз: {now:dd.MM.yyyy HH:mm}.");
            sb.AppendLine($"Ситуація: {frame.SituationKind}.");
            sb.AppendLine($"Висновок: {frame.SummaryUk}");
            if (!string.IsNullOrWhiteSpace(frame.LastUserText))
                sb.AppendLine($"Остання репліка користувача: «{Trim(frame.LastUserText, 180)}».");
            if (frame.SilenceMinutes > 0)
                sb.AppendLine($"Минуло від останньої репліки: {FormatDuration(TimeSpan.FromMinutes(frame.SilenceMinutes))}.");
            if (state.LastScreenAwarenessAt > DateTime.MinValue && now - state.LastScreenAwarenessAt < TimeSpan.FromMinutes(45))
                sb.AppendLine($"Screen presence: mode={NullDash(state.LastScreenAwarenessMode)}, activity={NullDash(state.LastScreenAwarenessActivity)}, summary={Trim(state.LastScreenAwarenessSummary, 140)}.");
            if (intent != null)
            {
                sb.AppendLine($"Намір: {intent.Summary}.");
                sb.AppendLine($"Створено: {intent.CreatedAt:dd.MM HH:mm}; follow-up: {intent.FollowUpAt:dd.MM HH:mm}; очікувано до: {intent.ExpectedUntil:dd.MM HH:mm}.");
                if (intent.ResolvedAt.HasValue)
                    sb.AppendLine($"Намір закрито: {intent.ResolvedAt.Value:dd.MM HH:mm}; причина: {Trim(intent.ResolutionText, 160)}.");
            }
            sb.AppendLine($"Тон presence: {frame.ToneHint}.");
            sb.AppendLine("Rule: if he already woke up or returned, do not tell him to repeat an action that is already in the past.");
            sb.AppendLine("Правило: перед відповіддю перевір час. Якщо він уже прокинувся/повернувся, не кажи йому робити те, що вже в минулому.");
            sb.AppendLine("Правило: follow-up має бути конкретним до попередньої події, не шаблонним «як справи».");
            if (state.PresenceTrace.Count > 0)
                sb.AppendLine("Останні presence-сліди: " + string.Join(" | ", state.PresenceTrace.TakeLast(3)));
            return sb.ToString();
        }

        private static string BuildObservation(string content, string lower, DateTime now)
        {
            var situation = InferSituation(lower);
            return situation switch
            {
                "leaving" => $"{now:HH:mm}: він повідомив, що кудись іде або буде зайнятий.",
                "returned" => $"{now:HH:mm}: він повернувся/прокинувся/закрив попередню дію.",
                "sleep" => $"{now:HH:mm}: він говорить про сон; наступні відповіді мають враховувати час.",
                "project" => $"{now:HH:mm}: він фокусується на проєкті KokonoeAssistant.",
                _ => $"{now:HH:mm}: свіже повідомлення без окремої події: {Trim(content, 80)}"
            };
        }

        private static string InferSituation(string lower)
        {
            if (ContainsAny(lower, "прокин", "проснув", "поспав", "повернув", "вернув", "я тут", "закінчив", "закінчились"))
                return "returned";
            if (ContainsAny(lower, "спать", "спати", "сон", "лягаю"))
                return "sleep";
            if (ContainsAny(lower, "піду", "йду", "іду", "пішов", "відійду", "афк", "буду зайнятий"))
                return "leaving";
            if (ContainsAny(lower, "проект", "код", "тести", "коміт", "гіт", "github", "obsidian"))
                return "project";
            return "message";
        }

        private static string InferTone(string lower)
        {
            if (ContainsAny(lower, "втом", "погано", "стрес", "тривож", "болить"))
                return "careful";
            if (ContainsAny(lower, "злий", "бісить", "дратує", "ненавид"))
                return "sharp";
            if (ContainsAny(lower, "хм", "що далі", "думаєш"))
                return "planning";
            return "default";
        }

        private static void RememberTrace(KokoInternalState state, string trace)
        {
            state.PresenceTrace.Add(trace);
            if (state.PresenceTrace.Count > 30)
                state.PresenceTrace.RemoveRange(0, state.PresenceTrace.Count - 30);
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string FormatDuration(TimeSpan span)
        {
            span = span.Duration();
            if (span.TotalMinutes < 1) return "менше хвилини";
            if (span.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} хв";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} год {span.Minutes} хв";
            return $"{(int)span.TotalDays} дн {span.Hours} год";
        }

        private static string Trim(string text, int max)
        {
            text = (text ?? "").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        private static string NullDash(string? text)
            => string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
    }
}
