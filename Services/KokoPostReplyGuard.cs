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

            var userSaysAte = SaysAte(userLower);
            var userSaysNotEaten = SaysNotEaten(userLower);
            var userSaysSlept = SaysSlept(userLower);
            var recentFoodSaysAte = state.LastFoodStatus == "ate" &&
                state.LastFoodMentionAt > DateTime.MinValue &&
                now - state.LastFoodMentionAt < TimeSpan.FromHours(12);
            var recentSleepSaysSlept = state.LastSleepStatus is "slept" or "woke_or_returned" &&
                state.LastSleepMentionAt > DateTime.MinValue &&
                now - state.LastSleepMentionAt < TimeSpan.FromHours(18);

            if (!userSaysNotEaten && (userSaysAte || recentFoodSaysAte) && ClaimsUserDidNotEat(replyLower))
                violations.Add("відповідь суперечить останньому сигналу про їжу і знову стверджує, що він не їв");

            if ((userSaysSlept || recentSleepSaysSlept) && DeniesOrDramatizesSleep(userLower, replyLower))
                violations.Add("відповідь суперечить останньому сигналу про сон або драматизує його як гібернацію/кому");

            var userReturned = ContainsAny(userLower,
                    "\u043f\u0440\u043e\u043a\u0438\u043d", "\u043f\u0440\u043e\u0441\u043d\u0443\u0432", "\u043f\u043e\u0441\u043f\u0430\u0432", "\u0432\u0441\u0442\u0430\u0432", "\u044f \u0442\u0443\u0442", "\u0432\u0435\u0440\u043d\u0443\u0432", "\u043f\u043e\u0432\u0435\u0440\u043d\u0443\u0432",
                    "прокин", "проснув", "поспав", "встав", "я тут", "вернув", "повернув")
                || timeline.CurrentState.Contains("повернувся", StringComparison.OrdinalIgnoreCase)
                || timeline.CurrentState.Contains("повернувся", StringComparison.OrdinalIgnoreCase);
            var userTalksAboutSleepNow = ContainsAny(userLower, "спати", "спать", "сон", "спав", "поспав", "втом", "їсти", "голод");
            var sendsToSleep = ContainsAny(replyLower,
                "\u0439\u0434\u0438 \u0441\u043f\u0430\u0442\u0438", "\u0456\u0434\u0438 \u0441\u043f\u0430\u0442\u0438", "\u043b\u044f\u0433\u0430\u0439", "\u0441\u043f\u0438.", "\u0441\u043f\u0438,", "\u0441\u043f\u0438!", "\u0441\u043f\u0438?", "\u0441\u043f\u0438 ", "\u0434\u043e \u0440\u0430\u043d\u043a\u0443",
                "йди спати", "іди спати", "лягай", "спи.", "спи,", "спи!", "спи?", "спи ", "до ранку");
            var timelineWarnsOldInstruction = ContainsAny(timeline.CurrentState.ToLowerInvariant(),
                "\u0441\u0442\u0430\u0440\u0443 \u0456\u043d\u0441\u0442\u0440\u0443\u043a\u0446\u0456\u044e", "\u0437\u0430\u043a\u0440\u0438\u0442\u0438\u0439 \u043d\u0430\u043c\u0456\u0440",
                "стару інструк", "закритий намір");
            var broadSleepCommand = sendsToSleep || ContainsAny(replyLower,
                "\u0441\u043f\u0438", "\u0440\u0430\u043d\u043a\u0443",
                "спи", "ранку");
            if ((userReturned || timelineWarnsOldInstruction) && broadSleepCommand)
                violations.Add("застаріла інструкція спати після повернення/пробудження");
            else if (!userTalksAboutSleepNow && broadSleepCommand)
                violations.Add("сонний контекст протік у відповідь на іншу тему");

            var asksProfileOrMemory = IsMemoryOrProfileQuestion(userLower);
            var staleBodyAdvice = ContainsAny(replyLower,
                "\u0441\u043f\u0438", "\u0457\u0441\u0442\u0438", "\u0432\u0442\u043e\u043c\u0438\u0432", "\u0433\u043e\u043b\u043e\u0434",
                "сп", "їс", "втом", "голод");
            if (asksProfileOrMemory && staleBodyAdvice)
                violations.Add("сонний/харчовий контекст протік у відповідь на питання про пам'ять/профіль");

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

            if (KokoPersonaEngine.LooksBotLike(reply))
                violations.Add("відповідь звучить як сервісний бот, а не Kokonoe з характером");

            if (KokoPersonaEngine.LooksBlindlyAgreeing(userText, reply))
                violations.Add("відповідь автоматично погоджується замість критичного судження");

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
            if (asksProfileOrMemory && LooksLikeUserEchoClarificationFallback(replyLower))
                violations.Add("memory/profile query fell into user-quote clarification fallback instead of vault/profile read");
            if (LooksLikeStaleProactivePing(userLower, replyLower))
                violations.Add("stale proactive ping leaked into a direct reply");
            if (!IsRepeatableActionCommand(userText) && RepeatsRecentAssistant(reply, messages))
                violations.Add("відповідь дослівно повторює нещодавню репліку");

            if (violations.Count == 0)
                return Pass("post-reply guard passed");

            var hardReplacement = violations.Any(v => v.Contains("застаріла інструкція спати", StringComparison.OrdinalIgnoreCase))
                ? "Стоп. Команду \"спи\" знято: ти вже повернувся. Кажи, скільки реально поспав, і я перестану вдавати годинник без батарейки."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("сигналу про їжу", StringComparison.OrdinalIgnoreCase))
                ? BuildFoodStateReplacement(userText, state)
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("сигналу про сон", StringComparison.OrdinalIgnoreCase))
                ? BuildSleepStateReplacement(userText, state)
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
            hardReplacement ??= !asksProfileOrMemory && violations.Any(v => v.Contains("вигадує зовнішній факт", StringComparison.OrdinalIgnoreCase))
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
            hardReplacement ??= violations.Any(v => v.Contains("stale proactive ping", StringComparison.OrdinalIgnoreCase))
                ? BuildStaleProactiveReplacement(userText)
                : null;

            return new KokoPostReplyGuardResult
            {
                Passed = false,
                ShouldRepair = hardReplacement == null,
                RiskLevel = violations.Count >= 2 ? "high" : "medium",
                Summary = $"post-reply guard знайшов {violations.Count} проблем.",
                Violations = violations.ToArray(),
                HardReplacement = hardReplacement,
                RepairInstruction = BuildRepairInstruction(userText, reply, violations, timeline, state)
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
            KokoConversationTimelineFrame timeline,
            KokoInternalState state)
        {
            return $"""
POST-REPLY REPAIR
Користувач: {userText}
Погана відповідь: {badReply}

Проблеми:
{string.Join("\n", violations.Select(v => "- " + v))}

Timeline:
{timeline.PromptBlock}

{KokoPersonaEngine.BuildRepairRules(userText)}
{KokoResponsePlannerEngine.BuildRepairRules(state.LastResponsePlan)}

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
            return (lower.Contains("vision") && lower.Contains("500"))
                || (lower.Contains("vision") && lower.Contains("settings"))
                || (lower.Contains("vision") && lower.Contains("model"))
                || lower.Contains("vision-сервер повернув 500")
                || lower.Contains("vision-модель")
                || lower.Contains("перевір vision model")
                || lower.Contains("vision-сервер повернув 500")
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
            var imagePrompt = ContainsAny(userLower,
                "\u0444\u043e\u0442\u043e", "\u0437\u043e\u0431\u0440\u0430\u0436", "\u043a\u0430\u0440\u0442\u0438\u043d",
                "фото", "зобр", "картин",
                "що на фото", "опиши зображення", "проаналізуй фото", "картин");
            if (!imagePrompt) return false;
            return ContainsAny(replyLower,
                "\u043f\u043e\u0440\u043e\u0436\u043d", "\u043f\u0443\u0441\u0442", "\u0441\u043f\u0430\u043c",
                "порож", "пуст", "спам",
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

        private static bool IsMemoryOrProfileQuestion(string userLower)
        {
            var broadMemory = ContainsAny(userLower,
                "\u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u043f\u0430\u043c'\u044f\u0442", "\u043f\u0430\u043c\u2019\u044f\u0442", "\u043f\u0430\u043c\u02bc\u044f\u0442", "\u0437\u043d\u0430\u0454\u0448", "\u0437\u043d\u0430\u0435\u0448", "\u043f\u0440\u043e\u0444\u0456\u043b", "\u0434\u043e\u0441\u044c\u0454",
                "vault", "obsidian", "\u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d", "\u043e\u0431\u0441\u0438\u0434\u0438\u0430\u043d", "\u043d\u043e\u0442\u0430\u0442", "\u0437\u0430\u043c\u0456\u0442");
            if (!broadMemory) return false;

            return ContainsAny(userLower,
                "\u0449\u043e \u0437\u043d\u0430\u0454\u0448", "\u0449\u043e \u0437\u043d\u0430\u0435\u0448", "\u0440\u043e\u0437\u043a\u0430\u0436\u0438 \u0432\u0441\u0435", "\u0440\u043e\u0437\u043a\u0430\u0437\u0443\u0439 \u0432\u0441\u0435", "\u0440\u043e\u0437\u043a\u0430\u0436\u0438 \u043f\u0440\u043e \u043c\u0435\u043d\u0435",
                "\u0449\u043e \u043f\u0430\u043c", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439", "\u043f\u0435\u0440\u0435\u0432\u0456\u0440", "\u043f\u0440\u043e\u0447\u0438\u0442\u0430\u0439", "\u0437\u043d\u0430\u0439\u0434\u0438", "\u043f\u0440\u043e\u0444\u0456\u043b", "\u0434\u043e\u0441\u044c\u0454",
                "vault", "obsidian", "\u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d", "\u043e\u0431\u0441\u0438\u0434\u0438\u0430\u043d");
        }

        private static bool LooksLikeUserEchoClarificationFallback(string replyLower)
            => ContainsAny(replyLower,
                "\u043f\u043e \u0442\u0432\u043e\u0457\u0439 \u0440\u0435\u043f\u043b\u0456\u0446\u0456",
                "\u043f\u043e \u0442\u0432\u043e\u0457\u0439 \u0444\u0440\u0430\u0437\u0456",
                "\u043f\u043e \u0442\u0432\u043e\u0454\u043c\u0443 \u0437\u0430\u043f\u0438\u0442\u0443",
                "\u0437\u0432\u0443\u0447\u0438\u0442\u044c \u044f\u043a \u043d\u0435\u0432\u043f\u0435\u0432\u043d\u0435\u043d\u0435",
                "\u0432\u0438\u0442\u044f\u0433\u0443\u0432\u0430\u0442\u0438 \u0441\u0435\u043d\u0441 \u0456\u0437 \u0442\u0440\u044c\u043e\u0445 \u043a\u0440\u0430\u043f\u043e\u043a",
                "\u0432\u0438\u0442\u044f\u0433\u0443\u0432\u0430\u0442\u0438 \u0441\u0435\u043d\u0441 \u0437 \u0442\u0440\u044c\u043e\u0445 \u043a\u0440\u0430\u043f\u043e\u043a",
                "\u0441\u043a\u0430\u0436\u0438 \u043f\u0440\u044f\u043c\u043e, \u0449\u043e \u0441\u0430\u043c\u0435 \u043c\u0430\u0454\u0448 \u043d\u0430 \u0443\u0432\u0430\u0437\u0456");

        private static string BuildFabricationReplacement(string userText)
        {
            var compact = CompactUserEcho(userText);
            if (string.IsNullOrWhiteSpace(compact))
                return "Не бачу конкретики. Сформулюй нормально, що саме треба, і я відповім по суті.";

            return "\u041d\u0435 \u0431\u0443\u0434\u0443 \u0432\u0438\u0433\u0430\u0434\u0443\u0432\u0430\u0442\u0438 \u0437\u043e\u0432\u043d\u0456\u0448\u043d\u0456 \u0444\u0430\u043a\u0442\u0438 \u0431\u0435\u0437 \u043e\u043f\u043e\u0440\u0438 \u0432 \u043a\u043e\u043d\u0442\u0435\u043a\u0441\u0442\u0456. \u0414\u0430\u0439 \u043a\u043e\u043d\u043a\u0440\u0435\u0442\u043d\u0438\u0439 \u043e\u0431'\u0454\u043a\u0442 \u043f\u0435\u0440\u0435\u0432\u0456\u0440\u043a\u0438 \u0430\u0431\u043e \u0434\u043e\u0437\u0432\u0456\u043b \u043d\u0430 vault-read, \u0456 \u0432\u0456\u0434\u043f\u043e\u0432\u0456\u0434\u044c \u043f\u0456\u0434\u0435 \u043f\u043e \u0444\u0430\u043a\u0442\u0430\u0445, \u0430 \u043d\u0435 \u043f\u043e \u0434\u0438\u043c\u0443.";
        }

        private static bool IsShortAffection(string userLower)
        {
            var normalized = NormalizeCompact(userLower);
            if (userLower.Length <= 32 && ContainsAny(userLower,
                    "\u043b\u044e\u0431", "\u043a\u043e\u0445\u0430", "\u0441\u0443\u043c\u0443", "\u043e\u0431\u0456\u0439",
                    "юб", "ха", "ум", "бій"))
                return true;

            return normalized is "\u043b\u044e\u0431\u043b\u044e" or "\u043b\u044e\u0431\u043b\u044e\u0442\u0435\u0431\u0435" or "\u043a\u043e\u0445\u0430\u044e" or "\u043a\u043e\u0445\u0430\u044e\u0442\u0435\u0431\u0435"
                or "\u0441\u0443\u043c\u0443\u044e" or "\u043e\u0431\u0456\u0439\u043c\u0430\u044e"
                or "люблю" or "люблютебе" or "кохаю" or "кохаютебе"
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
            if (userLower.Length <= 24 && ContainsAny(userLower,
                    "\u043f\u0440\u0438\u0432\u0456\u0442", "\u043f\u0440\u0438\u0432\u0435\u0442", "\u0445\u0430\u0439", "\u0439\u043e", "\u0434\u0430\u0440\u043e\u0432\u0430", "\u0437\u0434\u043e\u0440\u043e\u0432", "\u043a\u0443",
                    "прив", "хай", "йо", "дар", "здор", "ку"))
                return true;

            return normalized is "\u043f\u0440\u0438\u0432\u0456\u0442" or "\u043f\u0440\u0438\u0432\u0435\u0442" or "\u0445\u0430\u0439" or "\u0439\u043e" or "\u0434\u0430\u0440\u043e\u0432\u0430" or "\u0437\u0434\u043e\u0440\u043e\u0432" or "\u043a\u0443"
                or "привіт" or "привет" or "хай" or "йо" or "дарова" or "здоров" or "ку";
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

        private static bool LooksLikeStaleProactivePing(string userLower, string replyLower)
        {
            var userAskedFoodOrPresence = ContainsAny(userLower,
                "їсти", "їжу", "голод", "ти ще там", "я тут", "забув їсти",
                "їсти", "їжу", "голод", "ти ще там");
            if (userAskedFoodOrPresence) return false;

            return ContainsAny(replyLower,
                "ти ще там", "ти взагалі планував сьогодні їсти", "знову забув",
                "планував сьогодні їсти", "просто цікаво",
                "ти ще там", "планував сьогодні їсти", "знову забув");
        }

        private static string BuildStaleProactiveReplacement(string userText)
        {
            var compact = CompactUserEcho(userText);
            if (string.IsNullOrWhiteSpace(compact))
                return "Сформулюй запит коротко і конкретно. Без туману, я не ворожка.";

            return $"Про \"{compact}\": уточни, що саме маєш на увазі під графіком - мій таймер/план, твій розклад, чи графік у коді.";
        }

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
                "\u0444\u043e\u0442\u043e",
                "\u0437\u043e\u0431\u0440\u0430\u0436",
                "\u043a\u0430\u0440\u0442\u0438\u043d",
                "фото",
                "зобр",
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

        private static bool SaysNotEaten(string lower)
            => ContainsAny(lower,
                "не їв", "не ів", "не ел", "не їла", "не їли",
                "нічого не їв", "ничего не ел", "ще нічого не їв", "ще не їв", "ще не їла",
                "без їжі", "без еды");

        private static bool SaysAte(string lower)
            => ContainsAny(lower,
                "я їв", "я ів", "я ел", "поїв", "поів", "поел",
                "з'їв", "з’їв", "зїв", "з'ів", "з’ів",
                "піц", "снідав", "обідав", "вечеряв", "їв ", " їв", "їла", "ел ");

        private static bool SaysSlept(string lower)
            => ContainsAny(lower,
                "заснув", "спав", "поспав", "ліг спати", "ліг спать",
                "ляг спати", "ляг спать", "прокин", "проснув");

        private static bool ClaimsUserDidNotEat(string replyLower)
            => ContainsAny(replyLower,
                "нічого не їв", "ничего не ел", "ще нічого не їв", "ще не їв",
                "ти не їв", "ти не ел", "не їв", "не ел",
                "без глюкози", "мозок без глюкози", "йди на кухню", "закинь у себе щось", "закинь в себе щось");

        private static bool DeniesOrDramatizesSleep(string userLower, string replyLower)
        {
            var deniesSleep = ContainsAny(replyLower, "ти не спав", "ти не спала", "не спав, ти", "не сон, а");
            var dramaticSleep = ContainsAny(replyLower, "гібернац", "кома", "в кому", "впав у кому");
            var userUsedDramaticWord = ContainsAny(userLower, "гібернац", "кома", "в кому");
            return deniesSleep || (dramaticSleep && !userUsedDramaticWord);
        }

        private static string BuildFoodStateReplacement(string userText, KokoInternalState state)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "піц"))
                return "Піцу з'їв — прийнято. Теза «нічого не їв» мертва, поховали без церемоній. Тепер працюємо з поточним станом: скільки часу до курсів і що треба встигнути?";

            if (SaysAte(lower))
                return "Їжу зафіксовано. Стару маячню про «нічого не їв» знято. Далі без повтору зламаної платівки: що зараз з енергією і найближчою справою?";

            var mention = string.IsNullOrWhiteSpace(state.LastFoodMentionText)
                ? "останній сигнал каже, що ти їв"
                : $"останній сигнал був: «{CompactUserEcho(state.LastFoodMentionText)}»";
            return $"Стоп. {mention}. Отже, не вигадуємо «нічого не їв» і повертаємось до реального питання.";
        }

        private static string BuildSleepStateReplacement(string userText, KokoInternalState state)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            var hasTime18 = ContainsAny(lower, "18:00", "18.00", "18 00", "о 18", "в 18");
            if (hasTime18)
                return "Прийнято: ти заснув о 18:00. Це довгий сон, не «гібернація» і не медичний цирк. Далі працюємо з поточним станом, а не зі старою драмою.";

            if (SaysSlept(lower))
                return "Прийнято: ти спав. Не «не спав», не «гібернація», не кома для дешевої репліки. Тепер кажи поточний стан і що робимо далі.";

            var mention = string.IsNullOrWhiteSpace(state.LastSleepMentionText)
                ? "останній сигнал каже, що сон уже був"
                : $"останній сигнал був: «{CompactUserEcho(state.LastSleepMentionText)}»";
            return $"Стоп. {mention}. Стару драму про сон прибрано; відповідаю по теперішньому стану.";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
