using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoPostReplyGuardResult
    {
        public bool Passed { get; set; } = true;
        public bool ShouldRepair { get; set; }
        public string RiskLevel { get; set; } = "low";
        public string Summary { get; set; } = "";
        public string[] Violations { get; set; } = Array.Empty<string>();
        public string RepairInstruction { get; set; } = "";
        public string? HardReplacement { get; set; }
    }

    public sealed class KokoPostReplyGuard
    {
        public KokoPostReplyGuardResult Evaluate(
            string userText,
            string reply,
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            KokoConversationTimelineFrame timeline,
            DateTime now)
        {
            var violations = new List<string>();
            userText ??= "";
            reply ??= "";
            var userLower = userText.ToLowerInvariant();
            var replyLower = reply.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(reply))
                violations.Add("порожня відповідь");

            if (LooksLikeTransportError(reply))
                return Pass("transport error surfaced; do not hide provider failure");

            var userReturned = ContainsAny(userLower, "прокин", "проснув", "поспав", "встав", "я тут", "вернув", "повернув")
                || timeline.CurrentState.Contains("повернувся", StringComparison.OrdinalIgnoreCase)
                || timeline.CurrentState.Contains("закритий намір", StringComparison.OrdinalIgnoreCase);
            var sendsToSleep = ContainsAny(replyLower, "йди спати", "іди спати", "лягай", "спи.", "спи ", "до ранку");
            if (userReturned && sendsToSleep)
                violations.Add("застаріла інструкція спати після повернення/пробудження");

            var activeIntent = state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderByDescending(i => i.ExpectedUntil)
                .FirstOrDefault();
            if (activeIntent != null && now > activeIntent.ExpectedUntil && activeIntent.Kind == "course")
            {
                var hasCourseCallback = ContainsAny(replyLower, "курс", "занят", "закінч", "ще там", "повернув");
                if (!hasCourseCallback)
                    violations.Add("відповідь ігнорує прострочений намір про курси");
            }

            if (LooksMostlyEnglish(reply))
                violations.Add("відповідь виглядає переважно англійською");

            if (LooksGeneric(userText, reply))
                violations.Add("відповідь занадто шаблонна для наявного контексту");

            if (violations.Count == 0)
                return Pass("post-reply guard passed");

            var hardReplacement = violations.Any(v => v.Contains("спати", StringComparison.OrdinalIgnoreCase))
                ? "Стоп. Команду \"спи\" знято: ти вже повернувся. Кажи, скільки реально поспав, і я перестану вдавати годинник без батарейки."
                : null;

            return new KokoPostReplyGuardResult
            {
                Passed = false,
                ShouldRepair = hardReplacement == null,
                RiskLevel = violations.Count >= 2 ? "high" : "medium",
                Summary = $"post-reply guard знайшов {violations.Count} проблем.",
                Violations = violations.ToArray(),
                HardReplacement = hardReplacement,
                RepairInstruction = BuildRepairInstruction(userText, reply, violations, timeline)
            };
        }

        private static KokoPostReplyGuardResult Pass(string summary) => new()
        {
            Passed = true,
            ShouldRepair = false,
            RiskLevel = "low",
            Summary = summary
        };

        private static string BuildRepairInstruction(
            string userText,
            string badReply,
            IReadOnlyList<string> violations,
            KokoConversationTimelineFrame timeline)
        {
            return $"""
POST-REPLY REPAIR
Користувач: {userText}
Погана відповідь: {badReply}

Проблеми:
{string.Join("\n", violations.Select(v => "- " + v))}

Timeline:
{timeline.PromptBlock}

Перепиши відповідь.
Правила:
- тільки українська;
- 1-4 речення;
- не згадуй guard/rewrite/перевірку;
- відповідай найновішому стану timeline;
- якщо була стара дія, не наказуй її повторити.
""";
        }

        private static bool LooksLikeTransportError(string reply)
        {
            var lower = reply.ToLowerInvariant();
            return lower.Contains("http 500")
                || lower.Contains("llm-запит")
                || lower.Contains("[pool]")
                || lower.Contains("[помилка]")
                || lower.Contains("internal server error");
        }

        private static bool LooksGeneric(string userText, string reply)
        {
            var lower = reply.ToLowerInvariant();
            if (reply.Length > 24) return false;
            if (ContainsAny((userText ?? "").ToLowerInvariant(), "курс", "спати", "прокин", "проект", "obsidian"))
                return ContainsAny(lower, "як справи", "що нового", "ага", "ясно", "добре");
            return false;
        }

        private static bool LooksMostlyEnglish(string text)
        {
            var letters = text.Where(char.IsLetter).ToArray();
            if (letters.Length < 12) return false;
            var latin = letters.Count(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z');
            var cyrillic = letters.Count(c => c >= '\u0400' && c <= '\u04FF');
            return latin > cyrillic * 2 && latin > 20;
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
