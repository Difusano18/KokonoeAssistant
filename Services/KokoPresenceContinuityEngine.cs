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
                SummaryUk = "РќРµРјР°С” СЃРІС–Р¶РѕРіРѕ СЃРёРіРЅР°Р»Сѓ, СЏРєРёР№ РІР°СЂС‚РёР№ РІС‚СЂСѓС‡Р°РЅРЅСЏ.",
                Priority = 0
            };

            if (lastUser == null)
            {
                frame.SummaryUk = "Р†СЃС‚РѕСЂС–СЏ РїРѕСЂРѕР¶РЅСЏ; РЅРµ РІРёРіР°РґСѓР№ Р±Р»РёР·СЊРєС–СЃС‚СЊ Р· РїРѕРІС–С‚СЂСЏ.";
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
                ? $"РѕС‡С–РєСѓРІР°РЅРµ РІС–РєРЅРѕ С‰Рµ С‚СЂРёРІР°С” РїСЂРёР±Р»РёР·РЅРѕ {FormatDuration(until)}"
                : $"РѕС‡С–РєСѓРІР°РЅРµ РІС–РєРЅРѕ РјРёРЅСѓР»Рѕ {FormatDuration(-until)} С‚РѕРјСѓ";

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

            frame.SummaryUk = $"РђРєС‚РёРІРЅРёР№ РЅР°РјС–СЂ: {intent.Summary}; {timeText}.";
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
            frame.SummaryUk = $"РќР°РјС–СЂ СѓР¶Рµ Р·Р°РєСЂРёС‚РёР№: {intent.Summary}. Р’С–РЅ РїРѕРІРµСЂРЅСѓРІСЃСЏ/РѕРЅРѕРІРёРІ СЃС‚Р°РЅ {FormatDuration(now - intent.ResolvedAt!.Value)} С‚РѕРјСѓ.";
        }

        private static void ApplySilenceContinuity(KokoPresenceFrame frame, KokoInternalState state, DateTime now, int autonomyLevel)
        {
            var silence = frame.SilenceMinutes;
            if (silence < 25)
            {
                frame.SituationKind = "recent_contact";
                frame.SummaryUk = $"Р’С–РЅ РїРёСЃР°РІ {FormatDuration(TimeSpan.FromMinutes(silence))} С‚РѕРјСѓ; РЅРµ С‚СЂРµР±Р° РІРґР°РІР°С‚Рё РґСЂР°РјСѓ.";
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
                frame.SummaryUk = $"Р”РѕРІРіР° С‚РёС€Р°: {FormatDuration(TimeSpan.FromMinutes(silence))}. Р¦Рµ РІР¶Рµ РЅРµ Р·РІРёС‡Р°Р№РЅР° РїР°СѓР·Р°.";
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
                frame.SummaryUk = $"РЎРµСЂРµРґРЅСЏ С‚РёС€Р°: {FormatDuration(TimeSpan.FromMinutes(silence))}. РњРѕР¶РЅР° РїС–РґРєРѕР»РѕС‚Рё, СЏРєС‰Рѕ РЅРµРјР°С” РІР°Р¶Р»РёРІС–С€РѕРіРѕ.";
                return;
            }

            frame.SituationKind = "short_silence";
            frame.Trigger = "presence_short_silence";
            frame.StyleHint = "observation";
            frame.ToneHint = "light presence; do not overreact";
            frame.Priority = 32;
            frame.ShouldInterrupt = false;
            frame.SummaryUk = $"РљРѕСЂРѕС‚РєР° С‚РёС€Р°: {FormatDuration(TimeSpan.FromMinutes(silence))}. РџСЂРѕСЃС‚Рѕ РІСЂР°С…СѓРІР°С‚Рё РІ С‚РѕРЅС–.";
        }

        private static string BuildPromptBlock(KokoPresenceFrame frame, KokoInternalState state, DateTime now, ShortTermIntent? intent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PRESENCE / CONTINUITY");
            sb.AppendLine($"Р—Р°СЂР°Р·: {now:dd.MM.yyyy HH:mm}.");
            sb.AppendLine($"РЎРёС‚СѓР°С†С–СЏ: {frame.SituationKind}.");
            sb.AppendLine($"Р’РёСЃРЅРѕРІРѕРє: {frame.SummaryUk}");
            if (!string.IsNullOrWhiteSpace(frame.LastUserText))
                sb.AppendLine($"РћСЃС‚Р°РЅРЅСЏ СЂРµРїР»С–РєР° РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: В«{Trim(frame.LastUserText, 180)}В».");
            if (frame.SilenceMinutes > 0)
                sb.AppendLine($"РњРёРЅСѓР»Рѕ РІС–Рґ РѕСЃС‚Р°РЅРЅСЊРѕС— СЂРµРїР»С–РєРё: {FormatDuration(TimeSpan.FromMinutes(frame.SilenceMinutes))}.");
            if (intent != null)
            {
                sb.AppendLine($"РќР°РјС–СЂ: {intent.Summary}.");
                sb.AppendLine($"РЎС‚РІРѕСЂРµРЅРѕ: {intent.CreatedAt:dd.MM HH:mm}; follow-up: {intent.FollowUpAt:dd.MM HH:mm}; РѕС‡С–РєСѓРІР°РЅРѕ РґРѕ: {intent.ExpectedUntil:dd.MM HH:mm}.");
                if (intent.ResolvedAt.HasValue)
                    sb.AppendLine($"РќР°РјС–СЂ Р·Р°РєСЂРёС‚Рѕ: {intent.ResolvedAt.Value:dd.MM HH:mm}; РїСЂРёС‡РёРЅР°: {Trim(intent.ResolutionText, 160)}.");
            }
            sb.AppendLine($"РўРѕРЅ presence: {frame.ToneHint}.");
            sb.AppendLine("Rule: if he already woke up or returned, do not tell him to repeat an action that is already in the past.");
            sb.AppendLine("РџСЂР°РІРёР»Рѕ: РїРµСЂРµРґ РІС–РґРїРѕРІС–РґРґСЋ РїРµСЂРµРІС–СЂ С‡Р°СЃ. РЇРєС‰Рѕ РІС–РЅ СѓР¶Рµ РїСЂРѕРєРёРЅСѓРІСЃСЏ/РїРѕРІРµСЂРЅСѓРІСЃСЏ, РЅРµ РєР°Р¶Рё Р№РѕРјСѓ СЂРѕР±РёС‚Рё С‚Рµ, С‰Рѕ РІР¶Рµ РІ РјРёРЅСѓР»РѕРјСѓ.");
            sb.AppendLine("РџСЂР°РІРёР»Рѕ: follow-up РјР°С” Р±СѓС‚Рё РєРѕРЅРєСЂРµС‚РЅРёРј РґРѕ РїРѕРїРµСЂРµРґРЅСЊРѕС— РїРѕРґС–С—, РЅРµ С€Р°Р±Р»РѕРЅРЅРёРј В«СЏРє СЃРїСЂР°РІРёВ».");
            if (state.PresenceTrace.Count > 0)
                sb.AppendLine("РћСЃС‚Р°РЅРЅС– presence-СЃР»С–РґРё: " + string.Join(" | ", state.PresenceTrace.TakeLast(3)));
            return sb.ToString();
        }

        private static string BuildObservation(string content, string lower, DateTime now)
        {
            var situation = InferSituation(lower);
            return situation switch
            {
                "leaving" => $"{now:HH:mm}: РІС–РЅ РїРѕРІС–РґРѕРјРёРІ, С‰Рѕ РєСѓРґРёСЃСЊ С–РґРµ Р°Р±Рѕ Р±СѓРґРµ Р·Р°Р№РЅСЏС‚РёР№.",
                "returned" => $"{now:HH:mm}: РІС–РЅ РїРѕРІРµСЂРЅСѓРІСЃСЏ/РїСЂРѕРєРёРЅСѓРІСЃСЏ/Р·Р°РєСЂРёРІ РїРѕРїРµСЂРµРґРЅСЋ РґС–СЋ.",
                "sleep" => $"{now:HH:mm}: РІС–РЅ РіРѕРІРѕСЂРёС‚СЊ РїСЂРѕ СЃРѕРЅ; РЅР°СЃС‚СѓРїРЅС– РІС–РґРїРѕРІС–РґС– РјР°СЋС‚СЊ РІСЂР°С…РѕРІСѓРІР°С‚Рё С‡Р°СЃ.",
                "project" => $"{now:HH:mm}: РІС–РЅ С„РѕРєСѓСЃСѓС”С‚СЊСЃСЏ РЅР° РїСЂРѕС”РєС‚С– KokonoeAssistant.",
                _ => $"{now:HH:mm}: СЃРІС–Р¶Рµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ Р±РµР· РѕРєСЂРµРјРѕС— РїРѕРґС–С—: {Trim(content, 80)}"
            };
        }

        private static string InferSituation(string lower)
        {
            if (ContainsAny(lower, "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РїРѕСЃРїР°РІ", "РїРѕРІРµСЂРЅСѓРІ", "РІРµСЂРЅСѓРІ", "СЏ С‚СѓС‚", "Р·Р°РєС–РЅС‡РёРІ", "Р·Р°РєС–РЅС‡РёР»РёСЃСЊ"))
                return "returned";
            if (ContainsAny(lower, "СЃРїР°С‚СЊ", "СЃРїР°С‚Рё", "СЃРѕРЅ", "Р»СЏРіР°СЋ"))
                return "sleep";
            if (ContainsAny(lower, "РїС–РґСѓ", "Р№РґСѓ", "С–РґСѓ", "РїС–С€РѕРІ", "РІС–РґС–Р№РґСѓ", "Р°С„Рє", "Р±СѓРґСѓ Р·Р°Р№РЅСЏС‚РёР№"))
                return "leaving";
            if (ContainsAny(lower, "РїСЂРѕРµРєС‚", "РєРѕРґ", "С‚РµСЃС‚Рё", "РєРѕРјС–С‚", "РіС–С‚", "github", "obsidian"))
                return "project";
            return "message";
        }

        private static string InferTone(string lower)
        {
            if (ContainsAny(lower, "РІС‚РѕРј", "РїРѕРіР°РЅРѕ", "СЃС‚СЂРµСЃ", "С‚СЂРёРІРѕР¶", "Р±РѕР»РёС‚СЊ"))
                return "careful";
            if (ContainsAny(lower, "Р·Р»РёР№", "Р±С–СЃРёС‚СЊ", "РґСЂР°С‚СѓС”", "РЅРµРЅР°РІРёРґ"))
                return "sharp";
            if (ContainsAny(lower, "С…Рј", "С‰Рѕ РґР°Р»С–", "РґСѓРјР°С”С€"))
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
            if (span.TotalMinutes < 1) return "РјРµРЅС€Рµ С…РІРёР»РёРЅРё";
            if (span.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} С…РІ";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} РіРѕРґ {span.Minutes} С…РІ";
            return $"{(int)span.TotalDays} РґРЅ {span.Hours} РіРѕРґ";
        }

        private static string Trim(string text, int max)
        {
            text = (text ?? "").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }
    }
}
