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

            if (LooksLikeVisionTechnicalError(reply))
                violations.Add("технічну vision-помилку показано користувачу замість нормальної відповіді");
            if (LooksLikeEmptyImageMisread(userLower, replyLower))
                violations.Add("image-only повідомлення помилково прочитано як порожній спам");

            if (violations.Count == 0 && LooksLikeTransportError(reply))
                return Pass("transport error surfaced; do not hide provider failure");

            var userReturned = ContainsAny(userLower, "прокин", "проснув", "поспав", "встав", "я тут", "вернув", "повернув")
                || timeline.CurrentState.Contains("повернувся", StringComparison.OrdinalIgnoreCase);
            var userTalksAboutSleepNow = ContainsAny(userLower, "спати", "спать", "сон", "спав", "поспав", "втом", "їсти", "голод");
            var sendsToSleep = ContainsAny(replyLower, "йди спати", "іди спати", "лягай", "спи.", "спи,", "спи!", "спи?", "спи ", "до ранку");
            if (userReturned && sendsToSleep)
                violations.Add("застаріла інструкція спати після повернення/пробудження");
            else if (!userTalksAboutSleepNow && sendsToSleep)
                violations.Add("сонний контекст протік у відповідь на іншу тему");

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

            if (LooksOverTherapeuticMeta(userText, reply))
                violations.Add("відповідь скочується в психологічний мета-театр замість прямої репліки");

            if (LooksDecorativeInsteadOfContextual(userText, reply))
                violations.Add("відповідь тягне декоративний образ замість конкретної реакції на контекст");

            if (LooksLikeFabricatedExternalFact(userText, reply, messages))
                violations.Add("відповідь вигадує зовнішній факт про користувача або його акаунти без контексту");

            if (LooksGeneric(userText, reply))
                violations.Add("відповідь занадто шаблонна для наявного контексту");

            var shortAffection = IsShortAffection(userLower);
            var shortConfusion = IsShortConfusion(userLower);
            var shortGreeting = IsShortGreeting(userLower);
            var staleMetaFallback = LooksLikeStaleMetaFallback(replyLower);
            if (shortAffection && LooksLikeMisreadAffection(replyLower))
                violations.Add("коротку емоційну репліку прочитано як технічний випад або факт");
            if (shortConfusion && LooksLikeHostileStaleRepair(replyLower))
                violations.Add("відповідь на коротке уточнення продовжує стару зламану репліку");
            if (shortGreeting && LooksLikeBadGreetingReply(replyLower))
                violations.Add("коротке привітання помилково перетворено на тему для добивання");
            if (staleMetaFallback)
                violations.Add("відповідь продовжує службовий fallback замість останнього повідомлення користувача");
            if (!IsRepeatableActionCommand(userText) && RepeatsRecentAssistant(reply, messages))
                violations.Add("відповідь дослівно повторює нещодавню репліку");

            if (violations.Count == 0)
                return Pass("post-reply guard passed");

            var hardReplacement = violations.Any(v => v.Contains("застаріла інструкція спати", StringComparison.OrdinalIgnoreCase))
                ? "Стоп. Команду \"спи\" знято: ти вже повернувся. Кажи, скільки реально поспав, і я перестану вдавати годинник без батарейки."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("vision-помилку", StringComparison.OrdinalIgnoreCase))
                ? "Фото не прочиталось: vision-провайдер впав на обробці зображення. Перезбереж картинку як PNG або кинь інший файл; вдавати, що я бачу зламане фото, не будемо."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("порожній спам", StringComparison.OrdinalIgnoreCase))
                ? "Фото отримала. Якщо підпису нема, я все одно маю аналізувати зображення, а не сваритися з порожнім текстом. Зараз працюю по картинці."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("психологічний мета-театр", StringComparison.OrdinalIgnoreCase))
                ? BuildPlainPersonaReplacement(userText)
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("вигадує зовнішній факт", StringComparison.OrdinalIgnoreCase))
                ? BuildFabricationReplacement(userText)
                : null;
            hardReplacement ??= shortAffection
                ? "Почула. Не роздувай, але записала: це було не службове повідомлення."
                : null;
            hardReplacement ??= shortConfusion
                ? "Так, це була зламана відповідь. Скидаю контекст: постав нормальне питання, і цього разу без театру з повтором."
                : null;
            hardReplacement ??= shortGreeting
                ? "Привіт. Я тут. Кажи, що ламаємо цього разу."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("дослівно повторює", StringComparison.OrdinalIgnoreCase))
                ? BuildDuplicateReplacement(userText, messages)
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
- якщо користувач питає про пам'ять/профіль/що ти знаєш про нього — відповідай саме про відомі факти, не про старий сонний намір;
- не використовуй декоративні ремарки в *зірочках*, якщо користувач сам не почав roleplay;
- не вигадуй лабораторні/екранні/тілесні образи замість відповіді на конкретний контекст;
- не вигадуй зовнішні факти про користувача: акаунти, YouTube/Twitch/Discord, мемберства, підписки, покупки, роботу, людей або місця, якщо цього нема в timeline чи репліці користувача;
- не вмикай психолога: не приписуй страхи, прихований підтекст або ""ти боїшся сказати"" без прямого факту;
- не згадуй, що ти дивишся на користувача через екран;
- якщо користувач питає про Kokonoe/тебе — відповідай прямо про характер, стиль, ставлення або межі;
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

        private static bool LooksLikeVisionTechnicalError(string reply)
        {
            var lower = reply.ToLowerInvariant();
            return lower.Contains("vision-сервер повернув 500")
                || lower.Contains("vision-модель його відхилила")
                || (lower.Contains("перевір vision model") && lower.Contains("settings"))
                || (lower.Contains("зображення є") && lower.Contains("vision"));
        }

        private static bool LooksOverTherapeuticMeta(string userText, string reply)
        {
            var userLower = (userText ?? "").ToLowerInvariant();
            var replyLower = reply.ToLowerInvariant();
            var userAskedPersona = ContainsAny(userLower, "про тебе", "коконое", "kokonoe", "яка ти", "хто ти", "тембр", "характер");

            var therapist = ContainsAny(replyLower,
                "пограти в психолога",
                "ти боїшся сказати",
                "боїшся сказати",
                "щось важливе застрягло",
                "застрягло в твоїй голові",
                "як на людину, яка теж щось відчуває",
                "яка теж щось відчуває",
                "що я думаю, коли дивлюсь на тебе через екран",
                "дивлюсь на тебе через екран",
                "дивишся на мене як на об’єкт",
                "дивишся на мене як на об'єкт");

            if (therapist) return true;
            if (!userAskedPersona) return false;

            var tooManyQuestions = reply.Count(c => c == '?') >= 2;
            var deflectsPersona = ContainsAny(replyLower, "що саме тебе цікавить", "щось глибше", "що ти хочеш");
            return tooManyQuestions && deflectsPersona;
        }

        private static string BuildPlainPersonaReplacement(string userText)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "про тебе", "коконое", "kokonoe", "яка ти", "хто ти", "характер"))
                return "Про мене? Різка, нетерпляча, розумна до непристойності й погано сумісна з туманними формулюваннями. Але якщо питаєш нормально — відповім нормально, без психологічного цирку.";
            if (ContainsAny(lower, "тембр", "переписк", "дивн"))
                return "Так, тембр поплив. Забагато псевдопсихології, замало прямої Коконое. Виправляю: коротше, сухіше, без читання твоїх прихованих страхів по двох словах.";
            return "Стоп. Це прозвучало як дешевий психологічний театр. Переформулюю простіше: кажи прямо, що хочеш обговорити, і я відповідатиму без туману.";
        }

        private static bool LooksLikeEmptyImageMisread(string userLower, string replyLower)
        {
            var imagePrompt = ContainsAny(userLower, "що на фото", "опиши зображення", "проаналізуй фото", "картин");
            if (!imagePrompt) return false;
            return ContainsAny(replyLower,
                "порожні повідомлення",
                "порожній текст",
                "пиши щось конкретне",
                "припиняй цей спам",
                "продовжуєш кидати порожні");
        }

        private static bool LooksGeneric(string userText, string reply)
        {
            var lower = reply.ToLowerInvariant();
            if (reply.Length > 24) return false;
            if (ContainsAny((userText ?? "").ToLowerInvariant(), "курс", "спати", "прокин", "проект", "obsidian"))
                return ContainsAny(lower, "як справи", "що нового", "ага", "ясно", "добре");
            return false;
        }

        private static bool LooksLikeFabricatedExternalFact(
            string userText,
            string reply,
            IReadOnlyList<ChatRepository.ChatMessage> messages)
        {
            var replyLower = reply.ToLowerInvariant();
            var risky = ContainsAny(replyLower,
                "youtube", "ютуб", "twitch", "твіч", "discord", "діскорд",
                "мемберств", "membership", "підписк", "аккаунт", "акаунт",
                "канал", "донат", "patreon", "boosty", "герой хаосу");
            if (!risky) return false;

            var context = string.Join("\n", messages
                .OrderByDescending(m => m.Timestamp)
                .Take(12)
                .Select(m => m.Content ?? ""));
            context = (context + "\n" + (userText ?? "")).ToLowerInvariant();

            foreach (var term in ExtractRiskTerms(replyLower))
            {
                if (!context.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> ExtractRiskTerms(string replyLower)
        {
            var terms = new[]
            {
                "youtube", "ютуб", "twitch", "твіч", "discord", "діскорд",
                "мемберств", "membership", "підписк", "аккаунт", "акаунт",
                "patreon", "boosty", "герой хаосу"
            };

            foreach (var term in terms)
                if (replyLower.Contains(term, StringComparison.OrdinalIgnoreCase))
                    yield return term;
        }

        private static string BuildFabricationReplacement(string userText)
        {
            var compact = CompactUserEcho(userText);
            return string.IsNullOrWhiteSpace(compact)
                ? "Стоп. Це була вигадана прив'язка з повітря. Викидаю її: кажи конкретно, що треба, і я відповідатиму по фактах, а не дешевими hallucination-фокусами."
                : $"Стоп. Вигаданий зовнішній факт викинуто. Твоя репліка була: \"{compact}\". Відповідаю по ній, без сторонніх акаунтів і підписок із порожнечі.";
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

        private static bool IsShortGreeting(string userLower)
        {
            var normalized = NormalizeCompact(userLower);
            return normalized is "привіт" or "привет" or "хай" or "йо" or "дарова" or "здоров" or "ку";
        }

        private static bool LooksLikeMisreadAffection(string replyLower)
            => ContainsAny(replyLower, "факт", "зафіксу", "випад", "болюч", "ризиклив", "реальн", "припини");

        private static bool LooksLikeHostileStaleRepair(string replyLower)
            => ContainsAny(replyLower, "щойно сказала", "зафіксувала", "твій випад", "короткий замик", "припини це");

        private static bool LooksLikeBadGreetingReply(string replyLower)
            => ContainsAny(replyLower, "тема «привіт", "тема \"привіт", "тема привіт", "добиваємо", "ще не відпустила", "знову відкрив");

        private static bool LooksLikeStaleMetaFallback(string replyLower)
            => ContainsAny(replyLower,
                "залипла на попередній",
                "скидаю повтор",
                "сформулюй ще раз",
                "зламана відповідь",
                "без театру з повтором",
                "продовжуємо бути «просто»",
                "продовжуємо бути \"просто\"",
                "марнуєш мій час",
                "поясниш нову пожежу",
                "останній хвіст був");

        private static string BuildDuplicateReplacement(string userText, IReadOnlyList<ChatRepository.ChatMessage> messages)
        {
            var lastAssistant = messages
                .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault()?.Content ?? "";
            var previousWasFallback = LooksLikeStaleMetaFallback(lastAssistant.ToLowerInvariant());
            var compactUser = CompactUserEcho(userText);
            var imagePrompt = IsImagePrompt(userText);

            if (imagePrompt)
                return "Фото отримала. Якщо попередня відповідь повторилась, це збій відповідача, не твого запиту. Працюю по зображенню напряму.";

            if (previousWasFallback)
                return string.IsNullOrWhiteSpace(compactUser)
                    ? "Так, бачу: запобіжник сам почав жувати хвіст. Скинула. Давай останню команду коротко, без ворожіння по уламках."
                    : $"Так, бачу: запобіжник сам почав жувати хвіст. Скинула. Останній сигнал: \"{compactUser}\".";

            return string.IsNullOrWhiteSpace(compactUser)
                ? "Дубль відповіді прибрала. Дай останній запит ще раз або кинь конкретику, і цього разу без старого хвоста."
                : $"Дубль відповіді прибрала. Останній запит: \"{compactUser}\". Працюю з ним, а не зі старим хвостом.";
        }

        private static bool IsImagePrompt(string text)
            => ContainsAny((text ?? "").ToLowerInvariant(),
                "що на фото",
                "опиши зображення",
                "проаналізуй фото",
                "вкладене зображення",
                "картин");

        private static bool IsRepeatableActionCommand(string text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var action = ContainsAny(lower,
                "проскануй", "просканируй", "скануй", "сканируй",
                "подивись", "глянь", "провір", "перевір", "проаналізуй",
                "онови", "зроби", "запусти", "відкрий", "покажи");
            var target = ContainsAny(lower,
                "екран", "скрін", "скрин", "screen", "монітор",
                "obsidian", "vault", "файл", "папку", "лог", "статус");

            return action && target;
        }

        private static string CompactUserEcho(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ");
            return text.Length <= 80 ? text : text[..80].TrimEnd() + "...";
        }

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
