using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoProactiveContextFrame
    {
        public string LastUserText { get; set; } = "";
        public DateTime LastUserAt { get; set; } = DateTime.MinValue;
        public double SilenceMinutes { get; set; }
        public string SilenceTextUk { get; set; } = "";
        public string AnchorUk { get; set; } = "";
        public string ActiveIntentUk { get; set; } = "";
        public bool ShouldStaySilentForSleep { get; set; }
        public int AssistantPingsAfterLastUser { get; set; }
        public string[] RecentAssistantPings { get; set; } = Array.Empty<string>();
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoProactiveReplyCheck
    {
        public bool Passed { get; set; }
        public string Reason { get; set; } = "";
        public string Replacement { get; set; } = "";
    }

    public sealed class KokoProactiveContextService
    {
        public KokoProactiveContextFrame Build(
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            KokoInternalState state,
            DateTime now)
        {
            var ordered = messages.OrderBy(m => m.Timestamp).ToList();
            var lastUser = ordered.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            var afterLastUser = lastUser == null
                ? ordered.Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)).ToList()
                : ordered.Where(m => m.Timestamp > lastUser.Timestamp &&
                                      string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)).ToList();
            var activeIntent = state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderBy(i => i.FollowUpAt)
                .FirstOrDefault();

            var frame = new KokoProactiveContextFrame
            {
                LastUserText = Trim(lastUser?.Content, 220),
                LastUserAt = lastUser?.Timestamp ?? DateTime.MinValue,
                SilenceMinutes = lastUser == null ? 0 : Math.Max(0, (now - lastUser.Timestamp).TotalMinutes),
                AssistantPingsAfterLastUser = afterLastUser.Count,
                RecentAssistantPings = afterLastUser.TakeLast(3).Select(m => Trim(m.Content, 160)).ToArray(),
                ActiveIntentUk = activeIntent == null
                    ? ""
                    : $"{activeIntent.Kind}: {activeIntent.Summary}; очікуване вікно до {activeIntent.ExpectedUntil:HH:mm}"
            };
            frame.SilenceTextUk = FormatDuration(TimeSpan.FromMinutes(frame.SilenceMinutes));
            frame.AnchorUk = BuildAnchor(frame.LastUserText, activeIntent);
            frame.ShouldStaySilentForSleep =
                activeIntent?.Kind == "sleep" ||
                (LooksLikeSleepOrGoodbye(frame.LastUserText.ToLowerInvariant()) && frame.SilenceMinutes < 12 * 60);
            frame.PromptBlock = BuildPromptBlock(frame);
            return frame;
        }

        public KokoProactiveReplyCheck Check(string? reply, KokoProactiveContextFrame frame, string level)
        {
            var text = (reply ?? "").Trim();
            if (frame.ShouldStaySilentForSleep)
                return Fail("sleep/goodbye context should stay silent", "[мовчання]");
            if (string.IsNullOrWhiteSpace(text))
                return Fail("empty", BuildFallback(frame, level));

            var lower = text.ToLowerInvariant();
            if (LooksStaged(lower))
                return Fail("staged decorative action", BuildFallback(frame, level));

            if (frame.AssistantPingsAfterLastUser > 0 && LooksLikeRepeatedSilencePing(lower))
                return Fail("repeated generic silence ping", BuildFallback(frame, level));

            if (frame.SilenceMinutes < 120 && lower.Contains("зник"))
                return Fail("too early disappearance framing", BuildFallback(frame, level));

            if (LooksLikeGenericSilencePing(lower) && !TouchesAnchor(lower, frame))
                return Fail("generic silence without anchor", BuildFallback(frame, level));

            return new KokoProactiveReplyCheck { Passed = true };
        }

        public string BuildFallback(KokoProactiveContextFrame frame, string level)
        {
            var last = string.IsNullOrWhiteSpace(frame.LastUserText) ? "останнього сигналу" : $"«{frame.LastUserText}»";

            if (frame.AssistantPingsAfterLastUser > 0)
                return $"Добре, без другого кола про тишу. {last} ще актуально, чи ти вже переключився і, звісно, вирішив не витрачати зайві символи?";

            if (!string.IsNullOrWhiteSpace(frame.ActiveIntentUk))
                return $"Ти казав {last}. Минуло {frame.SilenceTextUk}; {frame.ActiveIntentUk}. Це ще в силі, чи план уже мутував?";

            if (!string.IsNullOrWhiteSpace(frame.LastUserText))
                return level switch
                {
                    "silence_l1" => $"Після {last} минуло {frame.SilenceTextUk}. Продовжуєш це, чи вже кинув напризволяще?",
                    "silence_l2" => $"Контекст був {last}. Минуло {frame.SilenceTextUk}; мені здогадуватись, ти ще там чи вже змінив курс?",
                    _ => $"Після {last} тиша вже {frame.SilenceTextUk}. Подай сигнал, якщо цей план ще живий."
                };

            return "Тиша є, контексту немає. Дуже інформативно. Подай хоча б один нормальний сигнал.";
        }

        private static string BuildPromptBlock(KokoProactiveContextFrame frame)
        {
            var recent = frame.RecentAssistantPings.Length == 0
                ? "немає"
                : string.Join("\n", frame.RecentAssistantPings.Select(p => $"- {p}"));

            return $"""
PROACTIVE CONTEXT
Остання репліка користувача: {NullDash(frame.LastUserText)}
Минуло: {frame.SilenceTextUk}
Якір контексту: {NullDash(frame.AnchorUk)}
Активний намір: {NullDash(frame.ActiveIntentUk)}
Сон / goodbye режим: {(frame.ShouldStaySilentForSleep ? "так, мовчати" : "ні")}
Авто-пінгів після останньої репліки користувача: {frame.AssistantPingsAfterLastUser}
Останні авто-пінги:
{recent}
Правила:
- Не пиши загальне "ти зник", "пауза помітна", "тиша затягнулась", якщо вже був авто-пінг після останньої репліки.
- Кожен proactive ping має прив'язуватись до останньої конкретної репліки або активного наміру.
- Якщо контекст слабкий, краще мовчати або написати одне конкретне питання.
""";
        }

        private static string BuildAnchor(string lastUserText, ShortTermIntent? activeIntent)
        {
            if (activeIntent != null)
                return $"намір: {activeIntent.Summary}";
            if (string.IsNullOrWhiteSpace(lastUserText))
                return "";

            var lower = lastUserText.ToLowerInvariant();
            if (ContainsAny(lower, "іспан", "испан", "фраз", "слово", "вимов"))
                return "навчання/фраза/мова";
            if (ContainsAny(lower, "курс", "занят", "урок", "пара"))
                return "курси або заняття";
            if (ContainsAny(lower, "дома", "вдома", "додому", "домой"))
                return "повернення додому";
            if (ContainsAny(lower, "сон", "спат", "сплю", "ляга"))
                return "сон";
            if (LooksLikeSleepOrGoodbye(lower))
                return "прощання/сон";
            if (ContainsAny(lower, "код", "проект", "тест", "коміт", "github", "obsidian"))
                return "проект";
            return Trim(lastUserText, 90);
        }

        private static bool LooksStaged(string lower)
            => lower.StartsWith("*") || lower.Contains("**") || lower.Contains("*див") || lower.Contains("*під");

        private static bool LooksLikeRepeatedSilencePing(string lower)
            => ContainsAny(lower, "пауза вже", "тиша затяг", "ти зник", "мовчиш", "економити слова");

        private static bool LooksLikeGenericSilencePing(string lower)
            => ContainsAny(lower, "пауза", "тиша", "зник", "мовчиш");

        private static bool TouchesAnchor(string lower, KokoProactiveContextFrame frame)
        {
            var anchor = (frame.AnchorUk + " " + frame.LastUserText).ToLowerInvariant();
            var tokens = anchor
                .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ':', ';', '!', '?', '"', '«', '»' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 4)
                .Distinct()
                .Take(12)
                .ToArray();
            return tokens.Any(t => lower.Contains(t));
        }

        private static KokoProactiveReplyCheck Fail(string reason, string replacement)
            => new() { Passed = false, Reason = reason, Replacement = replacement };

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static bool LooksLikeSleepOrGoodbye(string lower)
            => ContainsAny(lower,
                "бай бай", "бай-бай", "баю бай", "баю-бай", "бувай", "пока",
                "добраніч", "доброй ночи", "спокійної", "спокойной",
                "я спать", "я спати", "піду спати", "пішов спати", "лягаю");

        private static string NullDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return "менше хвилини";
            if (span.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} хв";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} год {span.Minutes} хв";
            return $"{(int)span.TotalDays} дн {span.Hours} год";
        }
    }
}
