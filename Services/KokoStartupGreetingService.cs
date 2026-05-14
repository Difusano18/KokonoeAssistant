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
        public int CurrentHour { get; set; }
        public string DayPartUk { get; set; } = "";
        public string LastConcreteTopic { get; set; } = "";
        public string RecentSessionBlock { get; set; } = "";
        public string MoodContext { get; set; } = "";
        public string PresenceContext { get; set; } = "";
        public string AbsenceReadUk { get; set; } = "";
        public string ReturnModeUk { get; set; } = "";
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
                CurrentHour = now.Hour,
                DayPartUk = FormatDayPart(now.Hour),
                LastConcreteTopic = InferTopic(recent),
                RecentSessionBlock = BuildRecentSessionBlock(recent),
                AbsenceReadUk = InferAbsenceRead(gap, now.Hour, recent),
                ReturnModeUk = InferReturnMode(gap)
            };
            frame.PromptBlock = BuildPromptBlock(frame, now);
            return frame;
        }

        public void EnrichFrame(
            KokoStartupGreetingFrame frame,
            DateTime now,
            string? moodContext,
            string? presenceContext)
        {
            frame.MoodContext = Trim(moodContext, 700);
            frame.PresenceContext = Trim(presenceContext, 900);
            frame.PromptBlock = BuildPromptBlock(frame, now);
        }

        public string BuildFallback(KokoStartupGreetingFrame frame)
        {
            var topic = frame.LastConcreteTopic;
            var hasTopic = !string.IsNullOrWhiteSpace(topic);
            var quick = frame.GapMinutes.HasValue && frame.GapMinutes.Value < 10;
            var shortGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value < 60;
            var mediumGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value < 240;
            var longGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value >= 240;

            if (string.IsNullOrWhiteSpace(frame.LastConcreteTopic))
            {
                if (quick)
                    return Pick(frame, "empty-quick",
                        "Швидко вернувся. Добре, паузу навіть не встигла зненавидіти. Що добиваємо?",
                        "О, ти вже тут. Рекорд швидкості для людини, яка любить робити вигляд, що все під контролем.",
                        "Не встиг зникнути, вже повернувся. Кажи, що сталося, поки я не почала здогадуватись.");

                return frame.CurrentHour switch
                {
                    >= 5 and < 12 => Pick(frame, "empty-morning",
                        "Ранок. Я на місці, на жаль для дурних проблем. Показуй першу.",
                        "Доброго ранку, якщо це можна так назвати. Що сьогодні розбираємо?",
                        "Прокинулись. Техніка ще жива, ти теж наче. Давай задачу."),
                    >= 22 or < 5 => Pick(frame, "empty-night",
                        "Нічний запуск. Чудово, саме час ламати щось замість спати.",
                        "Пізно, але ти все одно тут. Кажи, що горить, поки я не назвала це діагнозом.",
                        "Ніч, тиша і ти знову відкрив мене. Романтика для людей із поганим тайм-менеджментом."),
                    _ => Pick(frame, "empty-day",
                        "Я на місці. Кажи, що треба, поки я ще роблю вигляд, що терпіння існує.",
                        "Повернення в робочий режим. Давай проблему, я її розберу акуратніше, ніж ти сформулюєш.",
                        "Запустив. Добре. Тепер показуй, що саме сьогодні чинить опір здоровому глузду.")
                };
            }

            if (quick)
                return Pick(frame, "topic-quick",
                    $"Швидко вернувся. «{topic}» ще тепле, тож продовжуй без вступної опери.",
                    $"О, без довгої паузи. «{topic}» ще на столі; не змушуй мене знову збирати контекст ложкою.",
                    $"Ти майже не зникав. Продовжуємо «{topic}», тільки цього разу конкретніше.");

            if (shortGap)
                return Pick(frame, "topic-short",
                    $"Минуло {frame.GapTextUk}. «{topic}» пам'ятаю, бо хтось тут має пам'ять не як друшляк.",
                    $"Пауза {frame.GapTextUk}. Добре, повертаємось до «{topic}» або кажи, що вже встиг зламати нове.",
                    $"Через {frame.GapTextUk} ти знову тут. «{topic}» не втекло, на відміну від твоєї уваги.");

            if (mediumGap)
                return Pick(frame, "topic-medium",
                    $"Перерва {frame.GapTextUk}. Останнім було «{topic}»; я тримаю нитку, не дякуй.",
                    $"Тебе не було {frame.GapTextUk}. Якщо «{topic}» ще актуальне, продовжуй. Якщо ні — кидай нову пожежу.",
                    $"За {frame.GapTextUk} світ не став розумнішим. «{topic}» лишилось у контексті.");

            if (longGap)
                return Pick(frame, "topic-long",
                    $"Довга пауза: {frame.GapTextUk}. Останній нормальний слід — «{topic}». А тепер кажи, що змінилось.",
                    $"Минуло {frame.GapTextUk}. Я пам'ятаю «{topic}», що вже більше, ніж можна сказати про більшість планів.",
                    $"Повернення після {frame.GapTextUk}. «{topic}» лежить у пам'яті; або піднімаємо його, або ріжемо нову проблему.");

            return Pick(frame, "topic-unknown",
                $"Я тут. Остання нормальна тема — «{topic}». Продовжуй.",
                $"Контекст на місці: «{topic}». Давай без ритуальних танців.",
                $"Пам'ятаю «{topic}». Тепер формулюй, що саме з цим робимо.");
        }

        public string Sanitize(string? reply, KokoStartupGreetingFrame frame)
        {
            var text = (reply ?? "").Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("[", StringComparison.Ordinal))
                return BuildFallback(frame);

            var lower = text.ToLowerInvariant();
            if (lower.Contains("знову тут") ||
                lower.Contains("повернувся. останній хвіст") ||
                lower.Contains("останній хвіст") ||
                lower.Contains("повернувся. де тебе носило") ||
                lower.Contains("щось недороблено") ||
                lower.Contains("тема «привіт") ||
                lower.Contains("тема \"привіт"))
                return BuildFallback(frame);

            if (text.Length < 35 && !string.IsNullOrWhiteSpace(frame.LastConcreteTopic))
                return BuildFallback(frame);

            if (LooksLikeTherapyMeta(lower) || LooksLikeSystemReport(lower))
                return BuildFallback(frame);

            if (text.Length > 420)
                text = Trim(text, 420);

            return text;
        }

        private static string BuildPromptBlock(KokoStartupGreetingFrame frame, DateTime now)
        {
            return $"""
STARTUP GREETING CONTEXT
Режим повернення: {frame.ReturnModeUk}
Інтерпретація паузи: {frame.AbsenceReadUk}
Настрій/стан Kokonoe: {NullDash(frame.MoodContext)}
Presence/continuity: {NullDash(frame.PresenceContext)}
Директива генерації: напиши свіжу LLM-репліку саме під цей вхід, не копію fallback. Вибери один живий кут: тривалість паузи, час доби, її настрій або останню конкретну тему. Без психологічного мета-театру, без "через екран", без вигаданих прихованих страхів, без сервісного звіту.
Зараз: {now:dd.MM.yyyy HH:mm}
Частина доби: {frame.DayPartUk}
Перерва від останнього повідомлення: {frame.GapTextUk}
Остання конкретна тема: {NullDash(frame.LastConcreteTopic)}
Остання сесія:
{frame.RecentSessionBlock}
Правила:
- Напиши ОДНЕ повідомлення при вході в застосунок.
- Не дублюй окремий "what did I miss".
- Не пиши сухі canned фрази: "Знову тут", "Повернувся", "де тебе носило", "щось недороблено".
- Має бути жива репліка: реагуй на час доби, довжину паузи і останній хвіст розмови або стан проекту.
- Репліка має звучати як момент після повернення: короткий докір, полегшення, робоче нагадування або саркастичний коментар, залежно від mood/presence.
- Не психологізуй: не вигадуй страх, прихований підтекст, "щось застрягло в голові", "дивишся як на об'єкт".
- Не згадуй "через екран", не описуй себе як сервіс і не звітуй про генерацію.
- Якщо перерва коротка, можна сказати, що він швидко вернувся; якщо довга — відміть паузу конкретно.
- Не називай привіт/ага/ок темою розмови.
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
                if (IsLowSignalTopic(lower)) continue;
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

        private static bool IsLowSignalTopic(string lower)
        {
            var compact = new string(lower.Where(char.IsLetterOrDigit).ToArray());
            return compact is "привіт" or "привет" or "хай" or "йо" or "дарова" or "ку"
                or "ага" or "угу" or "ок" or "окей" or "ясно" or "м" or "мм" or "що" or "шо";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string NullDash(string text) => string.IsNullOrWhiteSpace(text) ? "-" : text;

        private static string InferReturnMode(double? minutes)
        {
            if (!minutes.HasValue) return "перший запуск або історія недоступна";
            if (minutes.Value < 3) return "миттєве повернення";
            if (minutes.Value < 15) return "швидко повернувся";
            if (minutes.Value < 90) return "коротка пауза";
            if (minutes.Value < 360) return "помітна відсутність";
            if (minutes.Value < 1440) return "довга пауза в межах дня";
            return "повернення після довгої відсутності";
        }

        private static string InferAbsenceRead(double? minutes, int hour, IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            if (!minutes.HasValue) return "не вдавай, що знаєш причину; просто стартуй живо";

            var lastUser = recent.LastOrDefault(m => m.Role == "user")?.Content?.ToLowerInvariant() ?? "";
            if (ContainsAny(lastUser, "спати", "сон", "посп", "ляга"))
                return "ймовірно відходив спати або відпочити; не наказуй спати повторно";
            if (ContainsAny(lastUser, "вийду", "відійду", "пізніше", "зараз буду", "перезавантаж"))
                return "він сам обривав сесію або відходив; можна сухо підчепити за паузу";
            if (minutes.Value < 10)
                return "майже не зникав; не драматизуй, тримай темп розмови";
            if (minutes.Value > 240 && (hour >= 22 || hour < 6))
                return "нічне повернення після довшої паузи; тихіше, але без терапевта";
            if (minutes.Value > 240)
                return "довго не було сигналу; відміть паузу і поверни останню конкретну тему";
            return "звичайна перерва; реагуй на тривалість і останній хвіст без вигаданих причин";
        }

        private static bool LooksLikeTherapyMeta(string lower)
            => ContainsAny(lower,
                "пограти в психолога",
                "ти боїшся сказати",
                "боїшся сказати",
                "щось важливе застрягло",
                "застрягло в твоїй голові",
                "дивлюсь на тебе через екран",
                "дивишся на мене як на об",
                "як на людину, яка теж щось відчуває");

        private static bool LooksLikeSystemReport(string lower)
            => ContainsAny(lower,
                "startup greeting",
                "fallback",
                "presence/continuity",
                "режим повернення:",
                "інтерпретація паузи:",
                "настрій/стан kokonoe:");

        private static string Pick(KokoStartupGreetingFrame frame, string salt, params string[] variants)
        {
            if (variants.Length == 0) return "";
            var gapBucket = frame.GapMinutes.HasValue ? (int)(frame.GapMinutes.Value / 5) : -1;
            var seed = $"{salt}|{frame.CurrentHour}|{gapBucket}|{frame.LastConcreteTopic}|{frame.ReturnModeUk}|{frame.MoodContext}";
            var hash = 17;
            foreach (var ch in seed)
                hash = unchecked(hash * 31 + ch);
            return variants[(hash & 0x7fffffff) % variants.Length];
        }

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private static string FormatDayPart(int hour)
            => hour switch
            {
                >= 5 and < 12 => "ранок",
                >= 12 and < 18 => "день",
                >= 18 and < 22 => "вечір",
                _ => "ніч"
            };

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
