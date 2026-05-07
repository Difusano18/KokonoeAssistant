using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoStartupGreetingFrame
    {
        public double? GapMinutes { get; set; }
        public string GapTextUk { get; set; } = "";
        public string LastConcreteTopic { get; set; } = "";
        public string RecentSessionBlock { get; set; } = "";
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoStartupGreetingService
    {
        public KokoStartupGreetingFrame BuildFrame(IReadOnlyList<ChatRepository.ChatMessage> messages, DateTime now)
        {
            var recent = messages.OrderBy(m => m.Timestamp).TakeLast(8).ToList();
            var last = recent.LastOrDefault();
            var gap = last == null ? (double?)null : Math.Max(0, (now - last.Timestamp).TotalMinutes);

            var frame = new KokoStartupGreetingFrame
            {
                GapMinutes = gap,
                GapTextUk = FormatGap(gap),
                LastConcreteTopic = InferTopic(recent),
                RecentSessionBlock = BuildRecentSessionBlock(recent)
            };
            frame.PromptBlock = BuildPromptBlock(frame, now);
            return frame;
        }

        public string BuildFallback(KokoStartupGreetingFrame frame)
        {
            if (string.IsNullOrWhiteSpace(frame.LastConcreteTopic))
                return "Я на місці. Давай, показуй що сьогодні добиваємо, поки воно знову не вирішило розвалитись.";

            if (frame.GapMinutes.HasValue && frame.GapMinutes.Value < 10)
                return $"Знову відкрив. Значить, тема «{frame.LastConcreteTopic}» ще не відпустила; добре, добиваємо її без цирку.";

            if (frame.GapMinutes.HasValue && frame.GapMinutes.Value < 120)
                return $"Повернувся через {frame.GapTextUk}. Я пам'ятаю хвіст: «{frame.LastConcreteTopic}». Продовжуй, що там ще не померло?";

            return $"Повернувся. Останній хвіст був «{frame.LastConcreteTopic}»; або продовжуємо його, або ти зараз урочисто поясниш нову пожежу.";
        }

        public string Sanitize(string? reply, KokoStartupGreetingFrame frame)
        {
            var text = (reply ?? "").Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("[", StringComparison.Ordinal))
                return BuildFallback(frame);

            var lower = text.ToLowerInvariant();
            if (lower.Contains("знову тут") ||
                lower.Contains("повернувся. де тебе носило") ||
                lower.Contains("щось недороблено"))
                return BuildFallback(frame);

            if (text.Length < 35 && !string.IsNullOrWhiteSpace(frame.LastConcreteTopic))
                return BuildFallback(frame);

            return text;
        }

        private static string BuildPromptBlock(KokoStartupGreetingFrame frame, DateTime now)
        {
            return $"""
STARTUP GREETING CONTEXT
Зараз: {now:dd.MM.yyyy HH:mm}
Перерва від останнього повідомлення: {frame.GapTextUk}
Остання конкретна тема: {NullDash(frame.LastConcreteTopic)}
Остання сесія:
{frame.RecentSessionBlock}
Правила:
- Напиши ОДНЕ повідомлення при вході в застосунок.
- Не дублюй окремий "what did I miss".
- Не пиши сухі canned фрази: "Знову тут", "Повернувся", "де тебе носило", "щось недороблено".
- Має бути жива репліка: згадати останній хвіст розмови або стан проекту і дати короткий саркастичний поштовх.
- 1-2 речення українською, без лапок, без *сценічних ремарок*.
""";
        }

        private static string BuildRecentSessionBlock(IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            if (recent.Count == 0) return "-";

            var sb = new StringBuilder();
            foreach (var msg in recent.TakeLast(6))
            {
                var who = msg.Role == "user" ? "Він" : "Коконое";
                sb.AppendLine($"- [{msg.Timestamp:HH:mm}] {who}: {Trim(msg.Content, 150)}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string InferTopic(IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            foreach (var msg in recent.AsEnumerable().Reverse())
            {
                var text = Trim(msg.Content, 180);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var lower = text.ToLowerInvariant();
                if (ContainsAny(lower, "авто-пінг", "автовідпов", "пауза", "тиша", "зник"))
                    return "авто-пінги мають бути живими, а не таймером із хамством";
                if (ContainsAny(lower, "obsidian", "ваульт", "vault"))
                    return "vault/Obsidian має оновлювати стан без симуляції амнезії";
                if (ContainsAny(lower, "іспан", "фраз", "слово", "вимов"))
                    return "іспанська фраза";
                if (ContainsAny(lower, "тест", "build", "коміт", "github", "пуш"))
                    return "тести, коміт і робочий стан проекту";
                if (ContainsAny(lower, "gui", "гуї", "дашборд", "інтерфейс"))
                    return "інтерфейс і dashboard";

                if (msg.Role == "user")
                    return text;
            }
            return "";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string NullDash(string text) => string.IsNullOrWhiteSpace(text) ? "-" : text;

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private static string FormatGap(double? minutes)
        {
            if (!minutes.HasValue) return "невідомо";
            if (minutes.Value < 1) return "щойно";
            if (minutes.Value < 60) return $"{(int)minutes.Value} хв";
            if (minutes.Value < 1440) return $"{(int)(minutes.Value / 60)} год {(int)(minutes.Value % 60)} хв";
            return $"{(int)(minutes.Value / 1440)} дн {(int)((minutes.Value % 1440) / 60)} год";
        }
    }
}
