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
            int autonomyLevel = 2)
        {
            autonomyLevel = Math.Clamp(autonomyLevel, 0, 3);
            var lastUser = messages
                .Where(m => m.Role == "user")
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            var frame = new KokoPresenceFrame
            {
                SituationKind = "idle",
                Trigger = "presence_idle",
                StyleHint = "observation",
                ToneHint = "dry, attentive, not generic",
                SummaryUk = "Немає свіжого сигналу, який вартий втручання.",
                Priority = 0
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

            ApplySilenceContinuity(frame, state, now, autonomyLevel);
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

        private static void ApplySilenceContinuity(KokoPresenceFrame frame, KokoInternalState state, DateTime now, int autonomyLevel)
        {
            var silence = frame.SilenceMinutes;
            if (silence < 25)
            {
                frame.SituationKind = "recent_contact";
                frame.SummaryUk = $"Він писав {FormatDuration(TimeSpan.FromMinutes(silence))} тому; не треба вдавати драму.";
                frame.Priority = 10;
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
                frame.SummaryUk = $"Довга тиша: {FormatDuration(TimeSpan.FromMinutes(silence))}. Це вже не звичайна пауза.";
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
                frame.SummaryUk = $"Середня тиша: {FormatDuration(TimeSpan.FromMinutes(silence))}. Можна підколоти, якщо немає важливішого.";
                return;
            }

            frame.SituationKind = "short_silence";
            frame.Trigger = "presence_short_silence";
            frame.StyleHint = "observation";
            frame.ToneHint = "light presence; do not overreact";
            frame.Priority = 32;
            frame.ShouldInterrupt = false;
            frame.SummaryUk = $"Коротка тиша: {FormatDuration(TimeSpan.FromMinutes(silence))}. Просто врахувати в тоні.";
        }

        private static string BuildPromptBlock(KokoPresenceFrame frame, KokoInternalState state, DateTime now, ShortTermIntent? intent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PRESENCE / CONTINUITY");
            sb.AppendLine($"Р—Р°СЂР°Р·: {now:dd.MM.yyyy HH:mm}.");
            sb.AppendLine($"Ситуація: {frame.SituationKind}.");
            sb.AppendLine($"Висновок: {frame.SummaryUk}");
            if (!string.IsNullOrWhiteSpace(frame.LastUserText))
                sb.AppendLine($"Остання репліка користувача: «{Trim(frame.LastUserText, 180)}».");
            if (frame.SilenceMinutes > 0)
                sb.AppendLine($"Минуло від останньої репліки: {FormatDuration(TimeSpan.FromMinutes(frame.SilenceMinutes))}.");
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
    }
}
