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

            if (LooksOverStaged(reply))
                violations.Add("відповідь звучить як сценарна ремарка, а не жива репліка");

            if (LooksDecorativeInsteadOfContextual(userText, reply))
                violations.Add("відповідь тягне декоративний образ замість конкретної реакції на контекст");

            if (LooksGeneric(userText, reply))
                violations.Add("відповідь занадто шаблонна для наявного контексту");

            var shortAffection = IsShortAffection(userLower);
            var shortConfusion = IsShortConfusion(userLower);
            if (shortAffection && LooksLikeMisreadAffection(replyLower))
                violations.Add("коротку емоційну репліку прочитано як технічний випад або факт");
            if (shortConfusion && LooksLikeHostileStaleRepair(replyLower))
                violations.Add("відповідь на коротке уточнення продовжує стару зламану репліку");
            if (RepeatsRecentAssistant(reply, messages))
                violations.Add("відповідь дослівно повторює нещодавню репліку");

            if (violations.Count == 0)
                return Pass("post-reply guard passed");

            var hardReplacement = violations.Any(v => v.Contains("спати", StringComparison.OrdinalIgnoreCase))
                ? "Стоп. Команду \"спи\" знято: ти вже повернувся. Кажи, скільки реально поспав, і я перестану вдавати годинник без батарейки."
                : null;
            hardReplacement ??= shortAffection
                ? "Почула. Не роздувай, але записала: це було не службове повідомлення."
                : null;
            hardReplacement ??= shortConfusion
                ? "Так, це була зламана відповідь. Скидаю контекст: постав нормальне питання, і цього разу без театру з повтором."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("дослівно повторює", StringComparison.OrdinalIgnoreCase))
                ? "Залипла на попередній репліці. Скидаю повтор: сформулюй ще раз, що саме треба, і я відповім по суті."
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
- не використовуй декоративні ремарки в *зірочках*, якщо користувач сам не почав roleplay;
- не вигадуй лабораторні/екранні/тілесні образи замість відповіді на конкретний контекст;
- зроби репліку живою: конкретна деталь з останнього повідомлення + один природний поворот тону.
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

        private static bool IsShortAffection(string userLower)
        {
            var normalized = NormalizeCompact(userLower);
            return normalized is "люблю" or "люблютебе" or "кохаю" or "кохаютебе"
                || normalized is "сумую" or "обіймаю";
        }

        private static bool IsShortConfusion(string userLower)
        {
            var normalized = NormalizeCompact(userLower);
            return normalized is "що" or "шо" or "чого" or "всм" or "всенсі" or "вчомусенс";
        }

        private static bool LooksLikeMisreadAffection(string replyLower)
            => ContainsAny(replyLower, "факт", "зафіксу", "випад", "болюч", "ризиклив", "реальн", "припини");

        private static bool LooksLikeHostileStaleRepair(string replyLower)
            => ContainsAny(replyLower, "щойно сказала", "зафіксувала", "твій випад", "короткий замик", "припини це");

        private static bool RepeatsRecentAssistant(string reply, IReadOnlyList<ChatRepository.ChatMessage> messages)
        {
            var normalizedReply = NormalizeForRepeat(reply);
            if (normalizedReply.Length < 24) return false;

            return messages
                .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .Take(5)
                .Any(m =>
                {
                    var previous = NormalizeForRepeat(m.Content);
                    if (previous.Length < 24) return false;
                    if (previous == normalizedReply) return true;
                    var min = Math.Min(previous.Length, normalizedReply.Length);
                    var max = Math.Max(previous.Length, normalizedReply.Length);
                    return min >= 60 && max - min <= 20 &&
                           previous[..min].Equals(normalizedReply[..min], StringComparison.Ordinal);
                });
        }

        private static string NormalizeCompact(string text)
            => new(text.Where(char.IsLetterOrDigit).ToArray());

        private static string NormalizeForRepeat(string text)
        {
            var chars = text
                .ToLowerInvariant()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray();
            return new string(chars);
        }

        private static bool LooksOverStaged(string reply)
        {
            var starPairs = reply.Count(c => c == '*') / 2;
            if (starPairs <= 0) return false;

            var stageChars = 0;
            var inside = false;
            foreach (var ch in reply)
            {
                if (ch == '*')
                {
                    inside = !inside;
                    continue;
                }
                if (inside) stageChars++;
            }

            return starPairs >= 1 && (stageChars > 22 || stageChars > reply.Length / 4);
        }

        private static bool LooksDecorativeInsteadOfContextual(string userText, string reply)
        {
            var userLower = (userText ?? "").ToLowerInvariant();
            var replyLower = reply.ToLowerInvariant();
            var concreteUserContext = ContainsAny(userLower,
                "буду", "дома", "вдома", "курс", "спати", "прокин", "проект", "код", "obsidian");
            if (!concreteUserContext) return false;

            var decorative = ContainsAny(replyLower,
                "графік", "монітор", "лаборатор", "датчик", "екран", "блима", "панель", "система");
            var contextEcho = ContainsAny(replyLower,
                "дома", "вдома", "12", "курс", "сон", "прокин", "проект", "код", "obsidian");

            return decorative && !contextEcho;
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
