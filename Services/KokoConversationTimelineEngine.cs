using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoTimelineEvent
    {
        public DateTime When { get; set; }
        public string Kind { get; set; } = "";
        public string Role { get; set; } = "";
        public string Summary { get; set; } = "";
        public double MinutesAgo { get; set; }
    }

    public sealed class KokoConversationTimelineFrame
    {
        public string SummaryUk { get; set; } = "";
        public string CurrentState { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public KokoTimelineEvent[] Events { get; set; } = Array.Empty<KokoTimelineEvent>();
    }

    public sealed class KokoConversationTimelineEngine
    {
        public KokoConversationTimelineFrame Build(
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            KokoInternalState state,
            DateTime now,
            string? currentUserText = null)
        {
            var events = new List<KokoTimelineEvent>();
            foreach (var msg in messages
                .OrderBy(m => m.Timestamp)
                .TakeLast(14))
            {
                events.Add(new KokoTimelineEvent
                {
                    When = msg.Timestamp,
                    Role = msg.Role,
                    Kind = ClassifyMessage(msg.Content),
                    Summary = Trim(msg.Content, 150),
                    MinutesAgo = Math.Max(0, (now - msg.Timestamp).TotalMinutes)
                });
            }

            foreach (var intent in state.ShortTermIntents.TakeLast(8))
            {
                events.Add(new KokoTimelineEvent
                {
                    When = intent.CreatedAt,
                    Role = "intent",
                    Kind = intent.ResolvedAt.HasValue ? "intent_resolved" : "intent_active",
                    Summary = $"{intent.Kind}: {intent.Summary}; РґРѕ {intent.ExpectedUntil:dd.MM HH:mm}" +
                              (intent.ResolvedAt.HasValue ? $"; Р·Р°РєСЂРёС‚Рѕ {intent.ResolvedAt.Value:dd.MM HH:mm}" : ""),
                    MinutesAgo = Math.Max(0, (now - intent.CreatedAt).TotalMinutes)
                });
            }

            events = events
                .OrderBy(e => e.When)
                .TakeLast(18)
                .ToList();

            var currentState = InferCurrentState(events, state, now, currentUserText);
            var frame = new KokoConversationTimelineFrame
            {
                Events = events.ToArray(),
                CurrentState = currentState,
                SummaryUk = BuildSummary(events, currentState, now)
            };
            frame.PromptBlock = BuildPromptBlock(frame, now);
            return frame;
        }

        private static string InferCurrentState(
            IReadOnlyList<KokoTimelineEvent> events,
            KokoInternalState state,
            DateTime now,
            string? currentUserText)
        {
            var lower = (currentUserText ?? "").ToLowerInvariant();
            if (ContainsAny(lower,
                    "\u043f\u0440\u043e\u043a\u0438\u043d", "\u043f\u0440\u043e\u0441\u043d\u0443\u0432", "\u043f\u043e\u0441\u043f\u0430\u0432", "\u0432\u0441\u0442\u0430\u0432", "\u044f \u0442\u0443\u0442",
                    "\u0432\u0435\u0440\u043d\u0443\u0432", "\u043f\u043e\u0432\u0435\u0440\u043d\u0443\u0432"))
                return "користувач повернувся; закритий намір; не повторювати стару інструкцію";

            if (ContainsAny(lower, "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ", "СЏ С‚СѓС‚", "РІРµСЂРЅСѓРІ", "РїРѕРІРµСЂРЅСѓРІ"))
                return "РєРѕСЂРёСЃС‚СѓРІР°С‡ РїРѕРІРµСЂРЅСѓРІСЃСЏ; РЅРµ РїРѕРІС‚РѕСЂСЋРІР°С‚Рё СЃС‚Р°СЂСѓ С–РЅСЃС‚СЂСѓРєС†С–СЋ";

            var currentHasOwnTopic = !string.IsNullOrWhiteSpace(lower) && !LooksLikeTemporalFollowup(lower);

            var active = state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderByDescending(i => i.ExpectedUntil)
                .FirstOrDefault();
            if (active != null)
            {
                if (now > active.ExpectedUntil)
                    return $"РїСЂРѕСЃС‚СЂРѕС‡РµРЅРёР№ РЅР°РјС–СЂ: {active.Kind}; РїРѕС‚СЂС–Р±РµРЅ РєРѕРЅРєСЂРµС‚РЅРёР№ follow-up";
                return $"Р°РєС‚РёРІРЅРёР№ РЅР°РјС–СЂ: {active.Kind}; РЅРµ С–РіРЅРѕСЂСѓРІР°С‚Рё Р№РѕРіРѕ";
            }

            var recentResolved = state.ShortTermIntents
                .Where(i => i.ResolvedAt.HasValue && now - i.ResolvedAt.Value < TimeSpan.FromHours(6))
                .OrderByDescending(i => i.ResolvedAt)
                .FirstOrDefault();
            if (recentResolved != null && !currentHasOwnTopic)
                return $"С‰РѕР№РЅРѕ Р·Р°РєСЂРёС‚РёР№ РЅР°РјС–СЂ: {recentResolved.Kind}; РґС–СЏ РІР¶Рµ РІ РјРёРЅСѓР»РѕРјСѓ";

            var lastUser = events.LastOrDefault(e => e.Role == "user");
            if (lastUser != null && lastUser.MinutesAgo >= 60)
                return $"Р±СѓР»Р° РїР°СѓР·Р° {FormatDuration(TimeSpan.FromMinutes(lastUser.MinutesAgo))}; РІСЂР°С…СѓРІР°С‚Рё С‡Р°СЃ";

            return "РїРѕС‚РѕС‡РЅРёР№ РґС–Р°Р»РѕРі Р±РµР· РєСЂРёС‚РёС‡РЅРѕРіРѕ С‡Р°СЃРѕРІРѕРіРѕ СЂРѕР·СЂРёРІСѓ";
        }

        private static string BuildSummary(IReadOnlyList<KokoTimelineEvent> events, string currentState, DateTime now)
        {
            var lastUser = events.LastOrDefault(e => e.Role == "user");
            if (lastUser == null)
                return currentState;

            return $"РћСЃС‚Р°РЅРЅСЏ СЂРµРїР»С–РєР° РєРѕСЂРёСЃС‚СѓРІР°С‡Р° {FormatDuration(now - lastUser.When)} С‚РѕРјСѓ; СЃС‚Р°РЅ: {currentState}.";
        }

        private static string BuildPromptBlock(KokoConversationTimelineFrame frame, DateTime now)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CONVERSATION TIMELINE");
            sb.AppendLine($"Р—Р°СЂР°Р·: {now:dd.MM.yyyy HH:mm}.");
            sb.AppendLine($"РЎС‚Р°РЅ: {frame.CurrentState}");
            sb.AppendLine($"Р’РёСЃРЅРѕРІРѕРє: {frame.SummaryUk}");
            if (frame.Events.Length > 0)
            {
                sb.AppendLine("РћСЃС‚Р°РЅРЅС– РїРѕРґС–С—:");
                foreach (var e in frame.Events.TakeLast(10))
                    sb.AppendLine($"- {e.When:dd.MM HH:mm} [{e.Role}/{e.Kind}] {Trim(e.Summary, 120)} ({FormatDuration(TimeSpan.FromMinutes(e.MinutesAgo))} С‚РѕРјСѓ)");
            }
            sb.AppendLine("РџСЂР°РІРёР»Рѕ: РІС–РґРїРѕРІС–РґСЊ РјР°С” РІС–РґРїРѕРІС–РґР°С‚Рё РЅР°Р№РЅРѕРІС–С€РѕРјСѓ СЃС‚Р°РЅСѓ timeline, РЅРµ СЃС‚Р°СЂС–Р№ СЂРµРїР»С–С†С–.");
            return sb.ToString();
        }

        private static string ClassifyMessage(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "СЃРїР°С‚Рё", "СЃРїР°С‚СЊ", "СЃРѕРЅ", "Р»СЏРіР°СЋ")) return "sleep";
            if (ContainsAny(lower, "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ")) return "returned";
            if (ContainsAny(lower, "РєСѓСЂСЃ", "Р·Р°РЅСЏС‚", "РїР°СЂР°")) return "course";
            if (ContainsAny(lower, "РїС–РґСѓ", "Р№РґСѓ", "С–РґСѓ", "РІС–РґС–Р№РґСѓ", "Р±СѓРґСѓ Р·Р°Р№РЅСЏС‚РёР№")) return "leaving";
            if (ContainsAny(lower, "РїСЂРѕРµРєС‚", "РєРѕРґ", "С‚РµСЃС‚", "РєРѕРјС–С‚", "github", "obsidian")) return "project";
            return "message";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static bool LooksLikeTemporalFollowup(string lower)
            => ContainsAny(lower,
                "СЃРїР°РІ", "СЃРїР°С‚Рё", "СЃРїР°С‚СЊ", "СЃРѕРЅ", "РїРѕСЃРїР°РІ", "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РІСЃС‚Р°РІ",
                "РїРѕРІРµСЂРЅСѓРІ", "РІРµСЂРЅСѓРІ", "СЏ С‚СѓС‚", "РґРµ Р±СѓРІ", "СЃРєС–Р»СЊРєРё", "РєРѕР»Рё");

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private static string FormatDuration(TimeSpan span)
        {
            span = span.Duration();
            if (span.TotalMinutes < 1) return "РјРµРЅС€Рµ С…РІРёР»РёРЅРё";
            if (span.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} С…РІ";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} РіРѕРґ {span.Minutes} С…РІ";
            return $"{(int)span.TotalDays} РґРЅ {span.Hours} РіРѕРґ";
        }
    }
}
