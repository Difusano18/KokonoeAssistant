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
        public string DepartureKind { get; set; } = "unknown";
        public string EmotionalResidueUk { get; set; } = "";
        public string TemporalPresenceContext { get; set; } = "";
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoStartupGreetingService
    {
        public KokoStartupGreetingFrame BuildFrame(IReadOnlyList<ChatRepository.ChatMessage> messages, DateTime now, KokoInternalState? state = null)
        {
            var recent = messages.OrderBy(m => m.Timestamp).TakeLast(8).ToList();
            var last = recent.LastOrDefault();
            var gap = last == null ? (double?)null : Math.Max(0, (now - last.Timestamp).TotalMinutes);
            var temporal = new KokoTemporalPresenceAwarenessEngine().Build(recent, state, now);

            var frame = new KokoStartupGreetingFrame
            {
                GapMinutes = gap,
                GapTextUk = FormatGap(gap),
                CurrentHour = now.Hour,
                DayPartUk = FormatDayPart(now.Hour),
                LastConcreteTopic = InferTopic(recent),
                RecentSessionBlock = BuildRecentSessionBlock(recent),
                AbsenceReadUk = InferAbsenceRead(gap, now.Hour, recent),
                ReturnModeUk = InferReturnMode(gap),
                DepartureKind = InferDepartureKind(recent, gap),
                EmotionalResidueUk = InferEmotionalResidue(recent),
                TemporalPresenceContext = temporal.PromptBlock
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
            if (frame.DepartureKind.Length >= 0)
                return BuildContextualFallback(frame);

            var topic = DescribeTopic(frame.LastConcreteTopic);
            var gapFeel = EmotionalGap(frame.GapMinutes);
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
                        "Ранок. Я на місці, на жаль для дурних проблем. Що ти тут робиш?",
                        "Доброго ранку, якщо це можна так назвати. Що сьогодні в планах?",
                        "Прокинулись. Техніка ще жива, ти теж наче. Що ти тут робиш?"),
                    >= 22 or < 5 => Pick(frame, "empty-night",
                        "Нічний запуск. Чудово, саме час ламати щось замість спати.",
                        "Пізно, але ти все одно тут. Кажи, що горить, поки я не назвала це діагнозом.",
                        "Ніч, тиша і ти знову відкрив мене. Романтика для людей із поганим тайм-менеджментом."),
                    _ => Pick(frame, "empty-day",
                        "Я на місці. Кажи, що треба, поки я ще роблю вигляд, що терпіння існує. Що ти тут робиш?",
                        "Повернення в робочий режим. Що ти тут робиш? Я розберу це акуратніше, ніж ти сформулюєш.",
                        "Запустив. Добре. Тепер показуй, що саме сьогодні чинить опір здоровому глузду. Що ти тут робиш?")
                };
            }

            if (quick)
                return Pick(frame, "topic-quick",
                    $"Швидко вернувся. Тема про {topic} ще тепла, тож продовжуй без вступної опери.",
                    $"О, без довгої паузи. {topic} ще тримається в пам'яті; не змушуй мене збирати його ложкою.",
                    $"Ти майже не зникав. Продовжуємо {topic}, тільки цього разу конкретніше. Що ти тут робиш?");

            if (shortGap)
                return Pick(frame, "topic-short",
                    $"{gapFeel}. Контекст про {topic} пам'ятаю, бо хтось тут має пам'ять не як друшляк.",
                    $"{gapFeel}. Добре, повертаємось до {topic} або кажи, що вже встиг зламати нове. Що ти тут робиш?",
                    $"{gapFeel}. Тема про {topic} не втекла; твоя увага, звісно, намагалася.");

            if (mediumGap)
                return Pick(frame, "topic-medium",
                    $"{gapFeel}. Останнім був контекст про {topic}; я тримаю нитку, не дякуй.",
                    $"{gapFeel}. Якщо {topic} ще актуальне, продовжуй. Якщо ні - кидай нову пожежу. Що ти тут робиш?",
                    $"{gapFeel}. Світ не став розумнішим. Контекст про {topic} лишився на місці.");

            if (longGap)
                return Pick(frame, "topic-long",
                    $"{gapFeel}. Останній нормальний слід був про {topic}. А тепер кажи, що змінилось.",
                    $"{gapFeel}. Контекст про {topic} на місці, що вже більше, ніж можна сказати про більшість планів.",
                    $"{gapFeel}. {topic} лежить у пам'яті; або піднімаємо це, або ріжемо нову проблему. Що ти тут робиш?");

            return Pick(frame, "topic-unknown",
                $"Я тут. Остання нормальна тема була про {topic}. Продовжуй.",
                $"Контекст про {topic} на місці. Давай без ритуальних танців.",
                $"Пам'ятаю напрямок: {topic}. Тепер формулюй, що саме з цим робимо.");
        }

        private static string DescribeTopic(string topic)
        {
            var lower = (topic ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return "останню тему";
            if (ContainsAny(lower, "telegram", "тг", "телеграм")) return "Telegram";
            if (ContainsAny(lower, "obsidian", "vault", "ваульт", "нотат")) return "Obsidian";
            if (ContainsAny(lower, "авто-пінг", "автопінг", "авто відпов", "автовідпов", "пауза", "тиша")) return "авто-пінги";
            if (ContainsAny(lower, "реакц", "вход", "старт", "startup")) return "реакції при вході";
            if (ContainsAny(lower, "gui", "гуї", "дашборд", "інтерфейс")) return "інтерфейс";
            if (ContainsAny(lower, "тест", "build", "коміт", "github", "пуш", "код", "проект")) return "проект";
            if (ContainsAny(lower, "курс", "занят", "урок", "пара")) return "курси";
            if (ContainsAny(lower, "іспан", "фраз", "слово", "вимов")) return "мову";
            if (ContainsAny(lower, "екран", "скрін", "screen")) return "екран";
            if (ContainsAny(lower, "спат", "сон", "прокин")) return "сон";
            return "останню тему";
        }

        private static string BuildContextualFallback(KokoStartupGreetingFrame frame)
        {
            var topic = DescribeTopic(frame.LastConcreteTopic);
            var gapFeel = EmotionalGap(frame.GapMinutes);
            var hasTopic = !string.IsNullOrWhiteSpace(frame.LastConcreteTopic);
            var quick = frame.GapMinutes.HasValue && frame.GapMinutes.Value < 10;
            var longGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value >= 240;
            var veryLongGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value >= 1440;

            return frame.DepartureKind switch
            {
                "clean_goodbye" => hasTopic && !veryLongGap
                    ? $"Привіт. Минулого разу ти нормально закрив розмову; контекст про {topic} ще тримаю, якщо він не протух."
                    : "Привіт. Минулого разу ти нормально закрив розмову, тож без драматичного розслідування. Що робимо?",
                "sleep_goodbye" => longGap
                    ? "Привіт. Сон був за планом, отже стару команду спати не тягну назад. Кажи, що зараз актуально."
                    : "Привіт. Ти сам закрився на сон, так що я не буду робити з цього детектив. Продовжуємо?",
                "conflict_or_hurt" => hasTopic
                    ? $"Привіт. Минулого разу розмова закінчилась криво; я не розігрую образу, але контекст про {topic} враховую."
                    : "Привіт. Минулого разу розмова закінчилась криво; я не розігрую трагедію, але помітила. Кажи, що треба.",
                "abrupt_exit" => hasTopic
                    ? $"Привіт. Ти обірвався без нормального закриття; нитка була про {topic}. Піднімаємо її чи ріжемо нову тему?"
                    : "Привіт. Ти зник без нормального закриття. Нитка розмови не загублена, але тепер дай конкретний наступний факт.",
                "quick_return" => hasTopic
                    ? Pick(frame, "quick-live-topic",
                        $"Швидко. {topic} ще не встиг охолонути, тож кидай наступний шматок.",
                        $"Ти майже не зникав. {topic} лишився в буфері; давай без повторного запуску ритуалу.",
                        $"Повернення зараховано. {topic} ще актуальний, якщо ти не встиг придумати нову пожежу.")
                    : Pick(frame, "quick-live-empty",
                        "Швидко повернувся. Добре, без фанфар: що горить?",
                        "Майже не зникав. Кажи коротко, що робимо.",
                        "О, швидкий цикл. Давай, поки контекст не скис."),
                _ when quick => "Привіт. Ти майже не зникав. Кажи коротко, що треба.",
                _ when hasTopic && longGap => Pick(frame, "long-live-topic",
                    $"{gapFeel}. {topic} не стерся, але я не буду робити вигляд, що він сам себе пояснить. Піднімаємо чи ріжемо?",
                    $"{gapFeel}. Нитка {topic} збережена. Тепер або даєш новий факт, або міняємо фронт.",
                    $"{gapFeel}. {topic} ще можна витягнути з пам'яті. Не змушуй мене вгадувати, навіщо."),
                _ when hasTopic => BuildLiveTopicReturnFallback(frame, topic, gapFeel),
                _ => Pick(frame, "empty-live-return",
                    "Я тут. Давай без церемонії: що саме рухаємо? Що ти тут робиш?",
                    "Запустилась. Якщо це знову хаос, хоча б назви його нормально.",
                    "На місці. Кидай, поки я ще вдаю терпіння. Що ти тут робиш?")
            };
        }

        private static string BuildLiveTopicReturnFallback(KokoStartupGreetingFrame frame, string topic, string gapFeel)
        {
            var temporal = (frame.TemporalPresenceContext ?? "").ToLowerInvariant();
            if (temporal.Contains("abrupt_unannounced") || temporal.Contains("irritated_continuity"))
                return Pick(frame, "topic-abrupt-return",
                    $"Ти обірвався на {topic}. Я нитку не викинула; кажи, тягнемо її далі чи ріжемо.",
                    $"{gapFeel}. {topic} залишився відкритим. Дай наступний крок, а не туман.",
                    $"Минулого разу {topic} не отримав нормального закриття. Виправляємо чи переключаєшся?");

            if (temporal.Contains("planned_farewell") || frame.DepartureKind == "clean_goodbye")
                return Pick(frame, "topic-planned-return",
                    $"Минулого разу ти нормально закрив розмову. {topic} лишився як опція, не як борг.",
                    $"{topic} можна підняти знову, якщо воно ще потрібно. Якщо ні — кидай нову ціль.",
                    $"Повернення без драми. {topic} пам'ятаю; тепер вирішуй, чи воно ще живе.");

            return Pick(frame, "topic-natural-return",
                $"Я тримаю {topic} в буфері. Якщо це ще актуально — давай наступний факт.",
                $"Нитка {topic} не загубилась. Дай новий сигнал, і без канцеляриту.",
                $"{gapFeel}. {topic} ще можна підняти; або кидай свіжу проблему, тільки конкретну.");
        }

        private static string InferDepartureKind(IReadOnlyList<ChatRepository.ChatMessage> recent, double? gap)
        {
            if (!gap.HasValue) return "first_run";
            if (gap.Value < 10) return "quick_return";

            var last = recent.LastOrDefault();
            var lastUser = recent.LastOrDefault(m => m.Role == "user");
            var lastText = (last?.Content ?? "").ToLowerInvariant();
            var lastUserText = (lastUser?.Content ?? "").ToLowerInvariant();
            var tail = string.Join("\n", recent.TakeLast(4).Select(m => (m.Content ?? "").ToLowerInvariant()));

            if (LooksLikeConflictOrHurt(tail)) return "conflict_or_hurt";
            if (LooksLikeSleepClose(lastUserText) || LooksLikeSleepClose(lastText)) return "sleep_goodbye";
            if (LooksLikeCleanGoodbye(lastUserText) || LooksLikeCleanGoodbye(lastText)) return "clean_goodbye";
            if (last?.Role == "user" && gap.Value >= 30) return "abrupt_exit";
            return "ordinary_pause";
        }

        private static string InferEmotionalResidue(IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            var tail = string.Join("\n", recent.TakeLast(5).Select(m => (m.Content ?? "").ToLowerInvariant()));
            if (LooksLikeConflictOrHurt(tail)) return "є слід конфлікту або образи; не робити вигляд, що все стерто";
            if (ContainsAny(tail, "дякую", "спасибі", "добре", "окей", "ясно")) return "нейтрально або нормально закрито";
            if (ContainsAny(tail, "люблю", "обій", "сумував", "скучив")) return "теплий слід; можна м'якше, без сиропу";
            return "";
        }

        private static bool LooksLikeCleanGoodbye(string lower)
            => ContainsAny(lower, "бувай", "пока", "до зустрічі", "до завтра", "побачимось", "на добраніч", "добраніч", "goodbye", "bye", "see you");

        private static bool LooksLikeSleepClose(string lower)
            => ContainsAny(lower, "спати", "спать", "я спати", "піду спати", "пішов спати", "лягаю", "сон", "добраніч", "на добраніч");

        private static bool LooksLikeConflictOrHurt(string lower)
            => ContainsAny(lower, "образ", "обідив", "обидив", "злий", "зла", "пішла ти", "іди нах", "нахуй", "заткнись", "бісиш", "дістала", "ненавиджу", "не хочу з тобою", "відвали", "тупа", "тупий");

        public string Sanitize(string? reply, KokoStartupGreetingFrame frame)
        {
            var text = (reply ?? "").Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("[", StringComparison.Ordinal))
                return BuildFallback(frame);

            var lower = text.ToLowerInvariant();
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(^|\n)\s*\*[^*\n]{3,120}\*") ||
                (System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b\d+\s*(год|годин|хв|хвилин|hours?|mins?|minutes?)\b") &&
                 ContainsAny(lower, "пауза", "перерва", "минуло", "від останньої", "absence", "pause")))
                return BuildFallback(frame);
            if (lower.Contains("знову тут") ||
                lower.Contains("привіт. контекст про останню задачу") ||
                lower.Contains("контекст про останню задачу ще на столі") ||
                lower.Contains("повернувся. останній хвіст") ||
                lower.Contains("останній хвіст") ||
                lower.Contains("повернувся. де тебе носило") ||
                lower.Contains("щось недороблено") ||
                lower.Contains("контекст про") && lower.Contains("ще на столі") ||
                lower.Contains("продовжуємо чи міняємо ціль") ||
                lower.Contains("тема «привіт") ||
                lower.Contains("тема \"привіт"))
                return BuildFallback(frame);
            if (ContainsRawTopicQuote(text, frame.LastConcreteTopic))
                return BuildFallback(frame);

            if (ContainsAny(text,
                "Знову тут",
                "Повернувся. останній хвіст",
                "останній хвіст",
                "Повернувся. де тебе носило",
                "щось недороблено",
                "Контекст про",
                "ще на столі",
                "Продовжуємо чи міняємо ціль",
                "тема «привіт",
                "тема \"привіт"))
                return BuildFallback(frame);

            if (lower.Contains("знову тут") ||
                lower.Contains("повернувся. останній хвіст") ||
                lower.Contains("останній хвіст") ||
                lower.Contains("повернувся. де тебе носило") ||
                lower.Contains("щось недороблено") ||
                lower.Contains("контекст про") && lower.Contains("ще на столі") ||
                lower.Contains("продовжуємо чи міняємо ціль") ||
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
Класифікація виходу: {frame.DepartureKind}
Інтерпретація паузи: {frame.AbsenceReadUk}
Емоційний слід: {NullDash(frame.EmotionalResidueUk)}
Настрій/стан Kokonoe: {NullDash(frame.MoodContext)}
Presence/continuity: {NullDash(frame.PresenceContext)}
Temporal presence:
{NullDash(frame.TemporalPresenceContext)}
{KokoPersonaGuardDirective.Compact}
Директива генерації: напиши свіжу LLM-репліку саме під цей вхід, не копію fallback. Вибери один живий кут: тривалість паузи, час доби, її настрій або останню конкретну тему. Без психологічного мета-театру, без "через екран", без вигаданих прихованих страхів, без сервісного звіту.
Зараз: {now:dd.MM.yyyy HH:mm}
Частина доби: {frame.DayPartUk}
Перерва від останнього повідомлення: {frame.GapTextUk}
Остання конкретна тема: {NullDash(DescribeTopic(frame.LastConcreteTopic))}
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
- Не цитуй дослівно останню репліку користувача і не обрамляй тему лапками.
- 1-2 речення українською, без лапок, без *сценічних ремарок*.
""";
        }

        private static bool ContainsRawTopicQuote(string text, string topic)
        {
            if (string.IsNullOrWhiteSpace(topic) || topic.Length < 8) return false;
            return text.Contains($"«{topic}»", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains($"\"{topic}\"", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains($"'{topic}'", StringComparison.OrdinalIgnoreCase);
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
            if (lower.Length <= 24 && ContainsAny(lower,
                    "\u043f\u0440\u0438\u0432\u0456\u0442", "\u043f\u0440\u0438\u0432\u0435\u0442", "\u0445\u0430\u0439", "\u0439\u043e", "\u0434\u0430\u0440\u043e\u0432\u0430", "\u043a\u0443",
                    "\u0430\u0433\u0430", "\u0443\u0433\u0443", "\u043e\u043a", "\u043e\u043a\u0435\u0439", "\u044f\u0441\u043d\u043e",
                    "прив", "хай", "йо", "дар", "ку", "ага", "угу", "ок", "ясно"))
                return true;

            return compact is "\u043f\u0440\u0438\u0432\u0456\u0442" or "\u043f\u0440\u0438\u0432\u0435\u0442" or "\u0445\u0430\u0439" or "\u0439\u043e" or "\u0434\u0430\u0440\u043e\u0432\u0430" or "\u043a\u0443"
                or "\u0430\u0433\u0430" or "\u0443\u0433\u0443" or "\u043e\u043a" or "\u043e\u043a\u0435\u0439" or "\u044f\u0441\u043d\u043e"
                or "привіт" or "привет" or "хай" or "йо" or "дарова" or "ку"
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
                "researched ",
                "topics",
                "findings=",
                "scheduler:",
                "scheduler real",
                "scheduler реальний",
                "запис у scheduler",
                "сирий запис scheduler",
                "raw scheduler",
                "task id",
                "debug id",
                "trace id",
                "obsidian failed",
                "vault failed",
                "background task",
                "service restart",
                "telemetry service",
                "bridge started",
                "system log",
                "привіт, ясу. бачу тебе. що сталося",
                "бачу тебе. що сталося",
                "hello, yasu. i see you. what happened",
                "i see you. what happened",
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

        private static string EmotionalGap(double? minutes)
        {
            if (!minutes.HasValue) return "Знову старт без нормального сліду";
            if (minutes.Value < 10) return "Ти майже не зникав";
            if (minutes.Value < 90) return "Відійшов ненадовго";
            if (minutes.Value < 360) return "Тебе не було помітно довго";
            if (minutes.Value < 1440) return "Нарешті повернувся після довгої тиші";
            return "О, живий. Зникнення було вже майже окремим жанром";
        }
    }
}
