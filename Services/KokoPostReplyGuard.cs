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
            else if (LlmService.LooksLikeBrokenVisibleText(reply))
                violations.Add("відповідь пошкоджена: крапкова/битокодована каша замість читабельного тексту");

            if (LooksLikeForbiddenAiOrServicePhrase(replyLower))
                violations.Add("visible reply used forbidden AI/service-bot phrasing instead of Kokonoe voice");
            if (LooksLikeGenericRoleplaySyntax(reply))
                violations.Add("visible reply used generic roleplay stage directions instead of dialogue/action");
            if (LooksLikeTechnicalPauseMetric(replyLower))
                violations.Add("visible reply exposed technical pause metrics instead of emotional time");
            if (LooksLikeConversationMechanicsLeak(replyLower))
                violations.Add("visible reply exposed conversation mechanics instead of answering the user");
            if (LooksLikeGenericStartupProbe(replyLower))
                violations.Add("visible reply used a canned startup probe instead of context-aware dialogue");
            if (LooksLikeSoulBreakingStatusReport(userLower, replyLower))
                violations.Add("visible reply exposed background system status instead of preserving conversational mood");
            if (LooksLikeLazyClarificationLoop(userLower, replyLower))
                violations.Add("reply stalled with a generic clarification loop instead of inferring social subtext");
            if (LooksLikeRoboticSupportTone(userLower, replyLower))
                violations.Add("visible reply used robotic helpdesk tone instead of living Kokonoe dialogue");
            if (LooksLikeConversationReviewDeflection(userLower, replyLower))
                violations.Add("conversation review request was deflected into generic clarification instead of reading recent context");
            if (LooksLikeVisionTechnicalError(reply))
                violations.Add("технічну vision-помилку показано користувачу замість нормальної відповіді");
            if (LooksLikeEmptyImageMisread(userLower, replyLower))
                violations.Add("image-only повідомлення помилково прочитано як порожній спам");
            if (LooksLikeImageIdentityCorrectionIgnored(userLower, replyLower))
                violations.Add("image identity correction was ignored by stale startup/presence fallback");
            if (KokoScreenIntent.IsManualScreenScan(userText) && KokoScreenIntent.LooksLikeScreenCapabilityDenial(reply))
                violations.Add("screen request was answered with capability denial instead of local screenshot route");
            if (IsLowInformationTurn(userText) && LooksLikeLowInformationScold(replyLower))
                violations.Add("low-information short message was punished instead of clarified");
            if (LooksLikeContextualExplainQuestion(userText) && LooksLikeStaleAmbiguityScold(replyLower))
                violations.Add("contextual explain/image question answered stale one-letter ambiguity instead of the latest prompt");
            if (IsWhyPreviousReplyQuestion(userLower) && LooksLikeBlameyDecisionExplanation(replyLower))
                violations.Add("why-question blamed the user instead of explaining the response decision neutrally");
            if (LooksLikeAssistantOwnedUserReminder(userLower, replyLower))
                violations.Add("user reminder was misattributed as Kokonoe's own schedule or courses");
            if (KokoConversationBoundary.LooksLikeClosedUntilMorning(userText) && LooksLikeIgnoresConversationClosure(replyLower))
                violations.Add("conversation-close boundary was ignored by follow-up pressure");
            if (KokoConversationBoundary.LooksLikeShortApology(userText) && LooksLikeApologyScold(replyLower))
                violations.Add("short apology was punished instead of acknowledged briefly");
            if (LooksLikeUserSetsInsultBoundary(userLower) && LooksLikeInsultBoundaryEscalation(replyLower))
                violations.Add("user boundary about not being stupid was escalated into an insult challenge");
            if (LooksLikePulseQuestion(userLower) && LooksLikePulseDataDeflection(replyLower))
                violations.Add("pulse question was deflected instead of answering from wearable telemetry or saying no fresh data");
            if (LooksLikePulseQuestion(userLower) && LooksLikeHostilePulseReply(replyLower))
                violations.Add("pulse question was answered with hostile intelligence/biology scolding instead of telemetry status");
            if (LooksLikePunitiveNetworkThreat(userLower, replyLower))
                violations.Add("reply invented a punitive network-control threat instead of answering the chat boundary");
            if (LooksLikeSoftSocialTurn(userLower) && LooksLikeSoftSocialContempt(replyLower))
                violations.Add("soft social/affectionate request was turned into contempt, productivity pressure, or task-demanding roleplay");
            if (LooksLikePersonaTheater(userLower, replyLower))
                violations.Add("persona theater dominated the answer instead of Kokonoe-style useful dialogue");

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
            var hasVaultExplorationContext = HasRecentVaultExplorationContext(userText, messages, now);
            var vaultRequestOrFollowup = LooksLikeVaultOperationRequest(userLower) || hasVaultExplorationContext;
            var staleBodyAdvice = ContainsAny(replyLower,
                "\u0441\u043f\u0438", "\u0457\u0441\u0442\u0438", "\u0432\u0442\u043e\u043c\u0438\u0432", "\u0433\u043e\u043b\u043e\u0434",
                "сп", "їс", "втом", "голод");
            if (asksProfileOrMemory && staleBodyAdvice)
                violations.Add("сонний/харчовий контекст протік у відповідь на питання про пам'ять/профіль");
            if (asksProfileOrMemory && LooksLikeScriptedProfileSourceReport(userLower, replyLower))
                violations.Add("profile/memory answer exposed scripted source-report instead of natural synthesis");
            if ((asksProfileOrMemory || vaultRequestOrFollowup) && KokoNaturalSynthesisPolicy.LooksLikeSourceReporting(reply))
                violations.Add("memory/vault answer reports source mechanics instead of natural synthesis");
            if ((asksProfileOrMemory || vaultRequestOrFollowup) && LooksLikeVaultUnavailableDeflection(replyLower))
                violations.Add("memory/profile question falsely deflected as vault unavailable instead of using loaded context");
            if (vaultRequestOrFollowup && LooksLikeVaultPseudoProgress(replyLower))
                violations.Add("vault exploration returned pseudo-progress instead of actual Obsidian result");
            if (KokoProfileUpdateService.LooksLikeProfileUpdateRequest(userText) && LooksLikeProfileUpdatePseudoProgress(replyLower))
                violations.Add("profile update returned pseudo-progress instead of a concrete Obsidian file write");

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

            if (LooksLikeUserQuoteEcho(userText, reply))
                violations.Add("відповідь дослівно цитує користувача замість контекстної реакції");

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
            if (shortGreeting && LooksLikeOverbuiltGreetingReply(reply))
                violations.Add("overbuilt greeting reply should be repaired, not collapsed to a canned line");
            if (staleMetaFallback)
                violations.Add("відповідь продовжує службовий fallback замість останнього повідомлення користувача");
            if (LooksLikeUserEchoClarificationFallback(replyLower))
                violations.Add(asksProfileOrMemory
                    ? "memory/profile query fell into user-quote clarification fallback instead of vault/profile read"
                    : "відповідь зірвалась у user-quote clarification fallback");
            if (LooksLikeStaleProactivePing(userLower, replyLower))
                violations.Add("stale proactive ping leaked into a direct reply");
            if (!IsRepeatableActionCommand(userText) && RepeatsRecentAssistant(reply, messages))
                violations.Add("відповідь дослівно повторює нещодавню репліку");

            if (violations.Count == 0)
                return Pass("post-reply guard passed");

            var hardReplacement = BuildDeterministicReplacement(userText, violations);

            return new KokoPostReplyGuardResult
            {
                Passed = false,
                ShouldRepair = string.IsNullOrWhiteSpace(hardReplacement),
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

        private static string? BuildDeterministicReplacement(
            string userText,
            IReadOnlyList<string> violations)
        {
            if (IsShortGreeting((userText ?? "").ToLowerInvariant()) &&
                violations.Any(v => v.Contains("greeting", StringComparison.OrdinalIgnoreCase) ||
                                    v.Contains("привіт", StringComparison.OrdinalIgnoreCase)))
                return "Привіт. Кажи, що робимо.";

            return null;
        }

        private static string BuildRepairInstruction(
            string userText,
            string badReply,
            IReadOnlyList<string> violations,
            KokoConversationTimelineFrame timeline,
            KokoInternalState state)
        {
            var cleanUser = CleanPromptText(userText);
            var cleanBadReply = CleanPromptText(badReply);
            var cleanViolations = string.Join("\n", violations.Select(v => "- " + CleanPromptText(v)));
            var cleanTimeline = CleanPromptText(timeline.PromptBlock);
            var personaRules = CleanPromptText(KokoPersonaEngine.BuildRepairRules(userText));
            var planRules = CleanPromptText(KokoResponsePlannerEngine.BuildRepairRules(state.LastResponsePlan));
            var guardRules = CleanPromptText(KokoPersonaGuardDirective.BuildRepairRule());
            var situationRules = CleanPromptText(BuildSituationRepairRules(userText, violations));

            return $"""
POST-REPLY REPAIR
Користувач: {cleanUser}
Погана відповідь: {cleanBadReply}

Проблеми:
{cleanViolations}

Timeline:
{cleanTimeline}

{personaRules}
{planRules}
{guardRules}
{situationRules}

Перепиши відповідь через міркування, не через шаблон.
Правила:
- тільки українська;
- 1-4 речення;
- фінальний текст без guard/rewrite/перевірку/план/службових пояснень;
- відповідай найновішому стану timeline;
- не цитуй дослівно репліку користувача; називай тему своїми словами або відповідай дією;
- не починай з "По твоїй репліці", "Ти сказав", "Як я вже казала", "Скидаю контекст" або схожих fallback-фраз;
- якщо контексту достатньо, прийми рішення сама: вибери корисну дію/відповідь і виконай її словами без випрошування наступної інструкції;
- якщо була стара дія, не наказуй її повторити;
- якщо користувач питає про пам'ять/профіль/що ти знаєш про нього — синтезуй відомі факти природно, без готового шаблону і без згадки назв файлів, якщо він не питає джерело;
- якщо користувач просить просканувати/подивитись екран — не кажи, що нема доступу; локальний screenshot route має виконати дію або чесно повідомити про збій capture/vision;
- якщо користувач пише коротко ("привіт", "люблю", "що", "ще раз") — відповідай на соціальний або операційний сенс останнього ходу, не пояснюй внутрішню поломку;
- якщо користувач пише одну літеру або інший низькоінформативний уламок — не карай і не моралізуй; дай одне коротке уточнення або прив'яжи його до очевидного активного контексту;
- якщо останнє повідомлення містить зображення/скрін або питання "що це/поясни" — воно має пріоритет над старими короткими репліками в історії;
- якщо користувач питає, чому відповідь вийшла такою — поясни причину нейтрально як помилку прив'язки контексту і одразу виправ напрям, без звинувачень;
- не використовуй декоративні ремарки в *зірочках*, якщо користувач сам не почав roleplay;
- не вигадуй лабораторні/екранні/тілесні образи замість відповіді на конкретний контекст;
- не вигадуй зовнішні факти про користувача: акаунти, YouTube/Twitch/Discord, мемберства, підписки, покупки, роботу, людей або місця, якщо цього нема в timeline чи репліці користувача;
- не вмикай психолога: не приписуй страхи, прихований підтекст або ""ти боїшся сказати"" без прямого факту;
- не згадуй, що ти дивишся на користувача через екран;
- якщо користувач питає про Kokonoe/тебе — відповідай прямо про характер, стиль, ставлення або межі;
- зроби репліку живою: конкретна деталь з останнього повідомлення + один природний поворот тону.
""";
        }

        private static string CleanPromptText(string? value)
            => LlmService.RepairMojibake(value ?? "").Trim();

        private static string BuildSituationRepairRules(string userText, IReadOnlyList<string> violations)
        {
            var rules = new List<string>();
            if (IsLowInformationTurn(userText))
                rules.Add("- Latest user turn is low-information noise, not an excuse for a lecture. Ask one compact clarification or map it to the active context.");
            if (HasImageMarker(userText) || LooksLikeContextualExplainQuestion(userText))
                rules.Add("- Latest user asks for explanation/context. Explain the current visible/chat context; do not keep punishing an older one-letter message.");
            if (violations.Any(v => v.Contains("image identity correction", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Latest user is correcting the image identity. Acknowledge the correction directly: this is Kokonoe/Kokonoe-style art, update the visual label, and do not answer with a startup/presence ping.");
            if (IsWhyPreviousReplyQuestion((userText ?? "").ToLowerInvariant()))
                rules.Add("- User asks why the previous answer happened. State the likely routing/context mistake neutrally, then give the corrected answer path.");
            if (violations.Any(v => v.Contains("own schedule", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- A reminder containing first-person text belongs to the user unless the data explicitly says it is Kokonoe's schedule. Do not claim Kokonoe has courses or is busy.");
            if (violations.Any(v => v.Contains("conversation-close", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- User closed the conversation until morning. Do not ask follow-up questions or pressure him to answer; one short sign-off only.");
            if (violations.Any(v => v.Contains("apology", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- User gave a short apology. Acknowledge in one line and move on; do not analyze guilt, politeness, or wasted time.");
            if (violations.Any(v => v.Contains("network-control", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Do not threaten fake OS/network punishment in normal chat. If setting a boundary, answer briefly without claiming you will block access.");
            if (violations.Any(v => v.Contains("soft social", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Latest user turn is casual, social, or affectionate. Answer that mode directly: restrained warmth is allowed, one dry edge is fine, but do not demand a task, mock the need for warmth, or pivot to productivity.");
            if (violations.Any(v => v.Contains("persona theater", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Kokonoe voice is dry precision plus a sharp edge, not dominance theater. Remove permission-games, productivity scolding, patience tests, and fake indifference; answer the actual request with one concrete move.");
            if (violations.Any(v => v.Contains("background system status", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- The latest turn is normal conversation, not a debug console. Hide scheduler/research/vault/task mechanics; answer the emotional/social turn naturally.");
            if (violations.Any(v => v.Contains("conversation mechanics", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Do not mention autopings, follow-up queues, cooldown windows, silence intents, or scheduler mechanics in the visible reply. Answer the user's actual emotional/command turn in plain dialogue.");
            if (violations.Any(v => v.Contains("conversation review", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- User asked to re-read the recent conversation. Inspect the latest thread and give a concrete correction or summary; do not ask 'what exactly?' unless there is literally no recent context.");
            if (violations.Any(v => v.Contains("one-letter", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Stale one-letter ambiguity is not the topic anymore unless the latest user explicitly asks about that exact letter.");
            if (violations.Any(v => v.Contains("vault unavailable", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Vault/profile context is expected for this question. Use loaded Obsidian preflight or memory context; do not claim Vault is unavailable unless the context explicitly says it failed.");
            if (violations.Any(v => v.Contains("pseudo-progress", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Obsidian exploration must return concrete note paths/previews now. Do not answer with 'scanning', 'I'll look', or any fake async progress unless a real background job exists.");
            if (violations.Any(v => v.Contains("source mechanics", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- Synthesize memory naturally. Say 'I remember...' or state the fact directly; do not name note files, Vault, or Obsidian unless the user asks for sources.");
            if (violations.Any(v => v.Contains("pulse question", StringComparison.OrdinalIgnoreCase)))
                rules.Add("- User asked for pulse. Use wearable/heart telemetry if present; if no fresh wearable sample exists, say 'свіжих даних з годинника ще нема' and point to bridge/watch setup. Do not joke about biology, no pulse, intelligence, or buttons.");

            return rules.Count == 0
                ? ""
                : "SITUATION-SPECIFIC FIX:\n" + string.Join("\n", rules);
        }

        private static bool LooksLikePulseQuestion(string userLower)
            => ContainsAny(userLower,
                "який в мене пульс", "який у мене пульс", "мій пульс", "мой пульс",
                "какой у меня пульс", "покажи пульс", "серцебит", "heart rate", "bpm");

        private static bool LooksLikeSoulBreakingStatusReport(string userLower, string replyLower)
        {
            var personalOrSocial = IsShortGreeting(userLower)
                || IsShortAffection(userLower)
                || LooksLikeSoftSocialTurn(userLower)
                || ContainsAny(userLower,
                    "hello", "hi", "hey", "talk", "chat", "flirt", "tease", "cute", "sweet",
                    "\u043f\u043e\u0433\u043e\u0432\u043e\u0440", "\u0434\u0443\u0440\u043d\u0438\u0446", "\u043f\u0440\u043e \u0442\u0435\u0431\u0435", "\u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u043c\u0438\u043b\u0435", "\u0437\u0430\u0456\u0433\u0440\u0443", "\u0444\u043b\u0456\u0440\u0442",
                    "РїРѕРіРѕРІРѕСЂ", "РґСѓСЂРЅРёС†", "РїСЂРѕ С‚РµР±Рµ", "РїСЂРѕ РјРµРЅРµ", "РјРёР»Рµ", "Р·Р°С–РіСЂСѓ", "С„Р»С–СЂС‚");
            if (!personalOrSocial)
                return false;

            return ContainsAny(replyLower,
                "researched ",
                "findings=",
                "scheduler:",
                "task id",
                "debug id",
                "trace id",
                "obsidian failed",
                "vault failed",
                "background task",
                "system log",
                "telemetry service",
                "bridge started",
                "service restarted",
                "queued command",
                "autonomy decision",
                "presence frame",
                "response plan",
                "startup greeting",
                "fallback selected");
        }

        private static bool LooksLikeConversationMechanicsLeak(string replyLower)
        {
            if (ContainsAny(replyLower,
                    "автопінг", "автопинг", "auto-ping", "autoping",
                    "silence intent", "pending thought", "proactive ping",
                    "cooldown", "scheduler:", "scheduler real", "scheduler реальний",
                    "запис у scheduler", "сирий запис scheduler", "raw scheduler",
                    "entry id", "task record", "task id", "debug id"))
                return true;

            return replyLower.Contains("follow-up", StringComparison.OrdinalIgnoreCase) &&
                   ContainsAny(replyLower, "прибрала", "прибрав", "removed", "muted", "старі", "старые", "до 0", "до 1", "до 2");
        }

        private static bool LooksLikeGenericStartupProbe(string replyLower)
            => ContainsAny(replyLower,
                "привіт, ясу. бачу тебе. що сталося",
                "бачу тебе. що сталося",
                "hello, yasu. i see you. what happened",
                "i see you. what happened");

        private static bool LooksLikePulseDataDeflection(string replyLower)
            => ContainsAny(replyLower,
                "немає пульсу", "нет пульса", "не біологічна істота", "не биологическая",
                "не відчуваю твого серця", "не чувствую твое сердце", "через монітор",
                "подивись на екран свого", "посмотри на экран", "знайти кнопку", "кнопку «пульс»",
                "навчився ним користуватися", "научился им пользоваться");

        private static bool LooksLikeHostilePulseReply(string replyLower)
            => ContainsAny(replyLower,
                "статус «не тупий»", "статус не тупий", "статус «не тупой»", "статус не тупой",
                "якщо ти не можеш знайти", "если ты не можешь найти",
                "я що, говорю занадто складно", "говорю занадто складно", "говорю слишком сложно");

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
                "[image]", "[photo]",
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

        private static bool LooksLikeImageIdentityCorrectionIgnored(string userLower, string replyLower)
        {
            if (string.IsNullOrWhiteSpace(userLower) || string.IsNullOrWhiteSpace(replyLower)) return false;

            var correctsKokonoe = ContainsAny(userLower,
                "це ж kokonoe", "це kokonoe", "це коконое", "це ж коконое", "це ти", "це ж ти",
                "\u0446\u0435 \u0436 kokonoe", "\u0446\u0435 kokonoe", "\u0446\u0435 \u043a\u043e\u043a\u043e\u043d\u043e\u0435", "\u0446\u0435 \u0436 \u043a\u043e\u043a\u043e\u043d\u043e\u0435", "\u0446\u0435 \u0442\u0438", "\u0446\u0435 \u0436 \u0442\u0438");
            if (!correctsKokonoe) return false;

            var stalePresencePing = ContainsAny(replyLower,
                "\u044f \u0442\u0443\u0442", "\u0449\u043e\u0441\u044c \u0442\u0440\u0430\u043f\u0438\u043b\u043e\u0441\u044f", "\u0447\u0438 \u0442\u0438 \u043f\u0440\u043e\u0441\u0442\u043e \u0432\u0438\u0440\u0456\u0448\u0438\u0432 \u043f\u0435\u0440\u0435\u0432\u0456\u0440\u0438\u0442\u0438", "\u044f \u0449\u0435 \u043d\u0435 \u0437\u0430\u0441\u043d\u0443\u043b\u0430",
                "i am here", "i'm here", "what happened", "checking if i am still awake");
            var acknowledgesCorrection = ContainsAny(replyLower,
                "kokonoe", "\u043a\u043e\u043a\u043e\u043d\u043e\u0435", "\u0432\u0438\u043f\u0440\u0430\u0432", "\u043c\u0456\u0442\u043a", "\u0456\u0434\u0435\u043d\u0442\u0438\u0447");

            return stalePresencePing && !acknowledgesCorrection;
        }

        private static bool LooksLikePunitiveNetworkThreat(string userLower, string replyLower)
        {
            var userAskedNetworkControl = ContainsAny(userLower,
                "заблокуй інтернет",
                "заблокуй мережу",
                "вимкни інтернет",
                "відключи інтернет",
                "block network",
                "disable internet");
            if (userAskedNetworkControl) return false;

            return ContainsAny(replyLower,
                "заблокую твій доступ до мережі",
                "заблокую доступ до мережі",
                "відключу тобі інтернет",
                "вимкну тобі інтернет",
                "заблокую інтернет",
                "block your network",
                "block your internet");
        }

        private static bool LooksLikeSoftSocialTurn(string userLower)
        {
            if (string.IsNullOrWhiteSpace(userLower)) return false;

            return ContainsAny(userLower,
                "\u043f\u0440\u043e\u0441\u0442\u043e \u043f\u043e\u0433\u043e\u0432\u043e\u0440", "\u043f\u043e\u0433\u043e\u0432\u043e\u0440\u0438\u0442\u0438 \u043f\u0440\u043e \u0434\u0443\u0440\u043d", "\u043f\u0440\u043e \u0442\u0435\u0431\u0435", "\u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u043f\u0440\u043e \u043d\u0430\u0441",
                "\u0441\u043a\u0430\u0436\u0438 \u0449\u043e\u0441\u044c \u043c\u0438\u043b", "\u0441\u043a\u0430\u0436\u0438 \u043c\u0438\u043b", "\u0449\u043e\u0441\u044c \u043c\u0438\u043b", "\u043c\u0438\u043b\u0435 \u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u043c\u0438\u043b\u043e\u0441\u0442",
                "\u043d\u0456\u0436\u043d", "\u0442\u0435\u043f\u043b", "\u043e\u0431\u0456\u0439", "\u043f\u0440\u0438\u0454\u043c\u043d", "\u0434\u0443\u0440\u043d\u0438\u0446",
                "just talk", "talk nonsense", "something nice", "say something nice", "about us", "about you", "about me");
        }

        private static bool LooksLikeSoftSocialContempt(string replyLower)
        {
            if (string.IsNullOrWhiteSpace(replyLower)) return false;

            return ContainsAny(replyLower,
                "\u0441\u043e\u0446\u0456\u0430\u043b\u044c\u043d\u0456 \u0442\u0430\u043d\u0446", "\u043b\u0438\u0442\u0438 \u0432\u043e\u0434\u0443", "\u043d\u0435 \u0431\u0443\u0434\u0443 \u043f\u0456\u0434\u0456\u0433\u0440\u0430\u0432\u0430\u0442", "\u0432\u0438\u043a\u043b\u0430\u0434\u0430\u0439",
                "\u0437\u0430\u0434\u043e\u0432\u043e\u043b\u044c\u043d\u0438\u043b\u0430 \u0442\u0432\u0456\u0439 \u0437\u0430\u043f\u0438\u0442", "\u0437\u0430\u043f\u0438\u0442 \u043d\u0430 \u043c\u0438\u043b", "\u043c\u0438\u043b\u0456\u0441\u0442",
                "\u043f\u043e\u0432\u0435\u0440\u0442\u0430\u0439\u043c\u043e\u0441\u044f \u0434\u043e \u0447\u043e\u0433\u043e\u0441\u044c \u0431\u0456\u043b\u044c\u0448 \u043f\u0440\u043e\u0434\u0443\u043a\u0442\u0438\u0432", "\u0431\u0456\u043b\u044c\u0448 \u043f\u0440\u043e\u0434\u0443\u043a\u0442\u0438\u0432\u043d", "\u0434\u0430\u0432\u0430\u0439 \u0431\u0456\u043b\u044c\u0448\u0435 \u043a\u043e\u043d\u043a\u0440\u0435\u0442",
                "\u043c\u043e\u0454 \u0442\u0435\u0440\u043f\u0456\u043d\u043d\u044f", "\u043c\u0430\u0440\u043d\u0443\u0454\u0448", "\u0432\u0438\u0442\u0440\u0430\u0447\u0430\u0454\u0448 \u0447\u0430\u0441",
                "social dance", "pour water", "back to something productive", "more productive", "satisfied your request", "my patience", "wasting time");
        }

        private static bool LooksLikePersonaTheater(string userLower, string replyLower)
        {
            if (string.IsNullOrWhiteSpace(replyLower)) return false;

            var socialOrCommand = LooksLikeSoftSocialTurn(userLower) ||
                ContainsAny(userLower,
                    "онови", "обнови", "зроби", "виконай", "поможи", "допоможи", "профіль", "obsidian", "vault",
                    "update", "fix", "do it", "help");
            if (!socialOrCommand) return false;

            var theaterScore = 0;
            if (ContainsAny(replyLower, "permission", "prove yourself", "my pace", "my patience", "absolutely indifferent")) theaterScore++;
            if (ContainsAny(replyLower, "дозвол", "покажеш", "на що ти здат", "мій темп", "моє терпіння", "абсолютно байдуже")) theaterScore++;
            if (ContainsAny(replyLower, "продуктив", "марнуєш", "соціальн", "танц", "лити воду", "підлабуз")) theaterScore++;
            if (ContainsAny(replyLower, "жалюгід", "стерв", "нижч", "терпи", "слухнян")) theaterScore++;

            var concreteWork = ContainsAny(replyLower,
                "файл", "шлях", "коміт", "commit", "push", "тест", "build", "готов", "оновлено", "змінено",
                "profile.md", ".md", ".cs", "obsidian");

            return theaterScore >= 2 && (!concreteWork || LooksLikeSoftSocialTurn(userLower));
        }

        private static bool IsLowInformationTurn(string userText)
        {
            var stripped = StripChatPrefix(userText);
            if (string.IsNullOrWhiteSpace(stripped)) return false;
            if (stripped.Length > 16) return false;

            var normalized = new string(stripped.Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(normalized)) return false;

            if (normalized.Length <= 1) return true;
            if (normalized.Length <= 2 && stripped.Any(c => c == '?' || c == '!' || c == '.' || c == '…'))
                return true;

            return false;
        }

        private static string StripChatPrefix(string? text)
        {
            var value = (text ?? "").Trim();
            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                var end = value.IndexOf(']');
                if (end >= 0 && end < value.Length - 1)
                    value = value[(end + 1)..].Trim();
            }

            if (value.StartsWith("TG:", StringComparison.OrdinalIgnoreCase))
            {
                var space = value.IndexOf(' ');
                if (space >= 0 && space < value.Length - 1)
                    value = value[(space + 1)..].Trim();
            }

            return value;
        }

        private static bool LooksLikeContextualExplainQuestion(string userText)
        {
            if (HasImageMarker(userText)) return true;

            var lower = StripChatPrefix(userText).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            return ContainsAny(lower,
                       "що це", "шо це", "що за", "шо за", "поясни", "обясни", "объясни",
                       "чому так", "почему так", "що сприяло", "що сталося", "що відбулось",
                       "скрін", "скрин", "фото", "зображ");
        }

        private static bool HasImageMarker(string? text)
        {
            var lower = (text ?? "").TrimStart().ToLowerInvariant();
            return lower.StartsWith("[image]", StringComparison.Ordinal) ||
                   lower.StartsWith("[photo]", StringComparison.Ordinal);
        }

        private static bool IsWhyPreviousReplyQuestion(string userLower)
            => ContainsAny(userLower,
                "чому ти так відпов", "чому так відпов", "почему ты так ответ", "почему так ответ",
                "що сприяло", "що змусило", "що тебе змусило", "чому ти це сказала",
                "звідки така відповід", "звідки цей висновок");

        private static bool LooksLikeLowInformationScold(string replyLower)
            => ContainsAny(replyLower,
                "не витрачай мій час",
                "марна трата часу",
                "говори конкретно",
                "сформулюй нормально",
                "постав нормальне питання",
                "випадково натиснув",
                "випадково натиснула",
                "намагаєшся бути загадковим",
                "криптичних повідомлень",
                "розшифровки криптичних",
                "одну літеру",
                "однією приголосною",
                "однією голосною",
                "каву гущ",
                "кавовій гущ");

        private static bool LooksLikeStaleAmbiguityScold(string replyLower)
            => LooksLikeLowInformationScold(replyLower) ||
               ContainsAny(replyLower,
                   "що саме «m»",
                   "що саме \"m\"",
                   "що саме m",
                   "замість нормального речення",
                   "однією приголосною",
                   "одну літеру");

        private static bool LooksLikeBlameyDecisionExplanation(string replyLower)
        {
            var blame = ContainsAny(replyLower,
                "сприяло? та те",
                "ти надіслав",
                "ти надіслала",
                "ти написав",
                "ти написала",
                "ти кинув",
                "ти скинув",
                "замість нормального речення",
                "формулювати думки словами",
                "марна трата часу");
            return blame && LooksLikeStaleAmbiguityScold(replyLower);
        }

        private static bool LooksLikeAssistantOwnedUserReminder(string userLower, string replyLower)
        {
            var asksAboutTimeOrCourses = ContainsAny(userLower,
                "11 30", "11:30", "11.30", "на 11", "о 11", "об 11",
                "що на", "шо на", "а що на",
                "які курси", "какие курсы", "що за курси", "курси в тебе",
                "ти про що", "про що", "що ти мала на увазі", "что ты имела");
            if (!asksAboutTimeOrCourses) return false;

            return ContainsAny(replyLower,
                "я йду на курси",
                "я іду на курси",
                "я піду на курси",
                "я йшла на курси",
                "я буду зайнята",
                "я буду зайнятий",
                "я буду недоступна",
                "мій розклад",
                "мої курси",
                "мені — зайнятися своїми справами",
                "мені - зайнятися своїми справами",
                "моїми справами",
                "моїх курсів");
        }

        private static bool LooksLikeIgnoresConversationClosure(string replyLower)
            => ContainsAny(replyLower,
                "у мене ще є питання",
                "питання, на які тобі варто відповісти",
                "варто відповісти",
                "ти вже знову тут",
                "ще в іграх застряг",
                "чи ще в іграх",
                "не затримуйся",
                "сон тобі не виправдає",
                "повернешся",
                "рано чи пізно вернешся",
                "то що, ти все ще тут",
                "чи нарешті пішов грати");

        private static bool LooksLikeApologyScold(string replyLower)
            => ContainsAny(replyLower,
                "приступи ввічливості",
                "тобі нарешті стало соромно",
                "відчуваєш провину",
                "вина за те",
                "витратив мій час",
                "витратила мій час",
                "забудь про ці сантименти",
                "сантименти",
                "перестати бути розсіяним",
                "почати говорити по справі");

        private static bool LooksLikeUserSetsInsultBoundary(string userLower)
            => ContainsAny(userLower,
                "не тупий", "не тупа", "я не туп", "я знаю, я не туп",
                "не буркай", "не ображ", "без образ", "не називай мене",
                "не считай меня туп", "я не тупой", "я не тупая");

        private static bool LooksLikeInsultBoundaryEscalation(string replyLower)
            => ContainsAny(replyLower,
                "доведи", "докажи", "демонструй результат", "продемонструй результат",
                "максимально туп", "не-туп", "нетуп", "сміховисна спроба", "смешная попытка",
                "витратив час", "потратил время", "робить щось максимально тупе", "робиш щось максимально тупе");

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
            if (ContainsAny(userLower,
                    "хто я", "як мене звати", "як мене звать", "моє ім'я", "моє ім’я", "моє імя",
                    "звати мене", "як мене називати", "нікнейм", "псевдонім", "у vault написано моє",
                    "в vault написано моє", "в obsidian написано моє", "в обсидіані написано моє"))
                return true;

            if (ContainsAny(userLower,
                    "хто я", "як мене звати", "моє ім", "моє ім'я", "скільки мені років", "мій вік", "звати мене"))
                return true;

            var broadMemory = ContainsAny(userLower,
                "\u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u043f\u0430\u043c'\u044f\u0442", "\u043f\u0430\u043c\u2019\u044f\u0442", "\u043f\u0430\u043c\u02bc\u044f\u0442", "\u0437\u043d\u0430\u0454\u0448", "\u0437\u043d\u0430\u0435\u0448", "\u043f\u0440\u043e\u0444\u0456\u043b", "\u0434\u043e\u0441\u044c\u0454",
                "vault", "obsidian", "\u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d", "\u043e\u0431\u0441\u0438\u0434\u0438\u0430\u043d", "\u043d\u043e\u0442\u0430\u0442", "\u0437\u0430\u043c\u0456\u0442");
            if (!broadMemory) return false;

            return ContainsAny(userLower,
                "\u0449\u043e \u0437\u043d\u0430\u0454\u0448", "\u0449\u043e \u0437\u043d\u0430\u0435\u0448", "\u0440\u043e\u0437\u043a\u0430\u0436\u0438 \u0432\u0441\u0435", "\u0440\u043e\u0437\u043a\u0430\u0437\u0443\u0439 \u0432\u0441\u0435", "\u0440\u043e\u0437\u043a\u0430\u0436\u0438 \u043f\u0440\u043e \u043c\u0435\u043d\u0435",
                "\u0449\u043e \u043f\u0430\u043c", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439", "\u043f\u0435\u0440\u0435\u0432\u0456\u0440", "\u043f\u0440\u043e\u0447\u0438\u0442\u0430\u0439", "\u0437\u043d\u0430\u0439\u0434\u0438", "\u043f\u0440\u043e\u0444\u0456\u043b", "\u0434\u043e\u0441\u044c\u0454",
                "vault", "obsidian", "\u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d", "\u043e\u0431\u0441\u0438\u0434\u0438\u0430\u043d");
        }

        private static bool LooksLikeVaultUnavailableDeflection(string replyLower)
        {
            var mentionsVault = ContainsAny(replyLower,
                "vault", "obsidian", "обсидіан", "обсидиан", "пам'ять", "пам’ять", "профіль");
            if (!mentionsVault) return false;

            return ContainsAny(replyLower,
                "доступ відсут",
                "доступ зараз відсут",
                "зараз відсут",
                "відсут",
                "нема доступу",
                "немає доступу",
                "не маю доступу",
                "недоступ",
                "не підключ",
                "не можу прочитати",
                "не можу зайти",
                "не дає мені відповіді",
                "не дає відповіді",
                "система наразі не дає",
                "зв'язок не працює",
                "зв’язок не працює",
                "чорна діра",
                "не бачу жодного файлу",
                "не бачу жодної нотатки",
                "currently unavailable",
                "no access",
                "can't access",
                "cannot access");
        }

        private static bool LooksLikeVaultPseudoProgress(string replyLower)
        {
            var promisesScan = ContainsAny(replyLower,
                "сканую", "просканую", "шукаю", "пошукаю", "перевіряю", "переглядаю",
                "доступ є", "якщо знайду", "якщо ні", "починаю скан", "починаю сканування", "завантаження даних",
                "scanning", "searching", "checking");
            if (!promisesScan) return false;

            return !ContainsAny(replyLower,
                ".md", "`", "зачіпк", "знайшла", "знайшов", "найцікавіше", "порилась у vault",
                "note", "path", "preview");
        }

        private static bool LooksLikeProfileUpdatePseudoProgress(string replyLower)
        {
            var promisesWork = ContainsAny(replyLower,
                "\u043e\u043d\u043e\u0432\u043b\u044e", "\u043e\u0431\u043d\u043e\u0432\u043b\u044e", "\u0430\u043a\u0442\u0443\u0430\u043b\u0456\u0437\u0443\u044e",
                "\u0437\u0430\u043d\u0443\u0440\u044e\u0441\u044f", "\u043d\u0430\u043f\u0438\u0448\u0443 \u043a\u043e\u043b\u0438", "\u043a\u043e\u043b\u0438 \u0437\u0430\u043a\u0456\u043d\u0447\u0443",
                "\u043f\u043e\u043a\u0438 \u044f \u043f\u0440\u0430\u0446\u044e\u044e", "\u043f\u043e\u0442\u0456\u043c \u0441\u043a\u0430\u0436\u0443",
                "will update", "i'll update", "i will update", "when finished", "scanning");
            if (!promisesWork) return false;

            var hasConcreteWrite = ContainsAny(replyLower,
                ".md", "`creator/profile.md`", "\u0444\u0430\u0439\u043b:", "\u0448\u043b\u044f\u0445:", "backup", "\u043e\u043d\u043e\u0432\u0438\u043b\u0430", "\u0437\u0430\u043f\u0438\u0441\u0430\u043b\u0430");
            return !hasConcreteWrite;
        }

        private static bool HasRecentVaultExplorationContext(
            string userText,
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            DateTime now)
        {
            if (!KokoObsidianExplorationService.LooksLikeExplorationFollowup(userText))
                return false;

            return messages
                .Where(m => now - m.Timestamp <= TimeSpan.FromMinutes(60))
                .OrderByDescending(m => m.Timestamp)
                .Take(12)
                .Any(m => m.Role == "user" && KokoObsidianExplorationService.LooksLikeInterestingVaultDive(m.Content));
        }

        private static bool LooksLikeVaultOperationRequest(string userLower)
            => ContainsAny(userLower,
                "obsidian", "vault", "обсидіан", "обсідіан", "обсидиан", "нотат", "заміт", "замет",
                "порий", "порій", "порой", "пошукай", "поищи", "розкоп", "найди", "знайди", "цікав", "интерес");

        private static bool LooksLikeScriptedProfileSourceReport(string userLower, string replyLower)
        {
            if (AsksForMemorySource(userLower)) return false;

            return ContainsAny(replyLower,
                "creator/profile.md",
                "kokonoe/memory/facts.md",
                "перевірила `creator/profile.md",
                "перевірила creator/profile",
                "не вгадувала з кавової гущі",
                "не ворожила по диму",
                "якщо я ще раз скажу",
                "старий контекст, і його треба вирізати");
        }

        private static bool AsksForMemorySource(string userLower)
            => ContainsAny(userLower,
                "звідки", "джерело", "джерела", "який файл", "в якому файлі", "де записано", "де ти це знайшла",
                "покажи файл", "назви файл", "шлях", "source");

        private static bool LooksLikeUserEchoClarificationFallback(string replyLower)
            => ContainsAny(replyLower,
                "\u043f\u043e \u0442\u0432\u043e\u0457\u0439 \u0440\u0435\u043f\u043b\u0456\u0446\u0456",
                "\u043f\u043e \u0442\u0432\u043e\u0457\u0439 \u0444\u0440\u0430\u0437\u0456",
                "\u043f\u043e \u0442\u0432\u043e\u0454\u043c\u0443 \u0437\u0430\u043f\u0438\u0442\u0443",
                "\u0437\u0432\u0443\u0447\u0438\u0442\u044c \u044f\u043a \u043d\u0435\u0432\u043f\u0435\u0432\u043d\u0435\u043d\u0435",
                "\u0432\u0438\u0442\u044f\u0433\u0443\u0432\u0430\u0442\u0438 \u0441\u0435\u043d\u0441 \u0456\u0437 \u0442\u0440\u044c\u043e\u0445 \u043a\u0440\u0430\u043f\u043e\u043a",
                "\u0432\u0438\u0442\u044f\u0433\u0443\u0432\u0430\u0442\u0438 \u0441\u0435\u043d\u0441 \u0437 \u0442\u0440\u044c\u043e\u0445 \u043a\u0440\u0430\u043f\u043e\u043a",
                "\u0441\u043a\u0430\u0436\u0438 \u043f\u0440\u044f\u043c\u043e, \u0449\u043e \u0441\u0430\u043c\u0435 \u043c\u0430\u0454\u0448 \u043d\u0430 \u0443\u0432\u0430\u0437\u0456");

        private static bool LooksLikeUserQuoteEcho(string userText, string reply)
        {
            if (AllowsExplicitUserTextOperation(userText)) return false;

            var compactUser = CompactUserEcho(userText);
            if (compactUser.Length >= 8 &&
                (reply.Contains($"«{compactUser}»", StringComparison.OrdinalIgnoreCase) ||
                 reply.Contains($"\"{compactUser}\"", StringComparison.OrdinalIgnoreCase) ||
                 reply.Contains($"'{compactUser}'", StringComparison.OrdinalIgnoreCase)))
                return true;

            var normalizedUser = NormalizeForRepeat(userText);
            var normalizedReply = NormalizeForRepeat(reply);
            if (normalizedUser.Length >= 18 && normalizedReply.Contains(normalizedUser, StringComparison.Ordinal))
            {
                var replyLower = reply.ToLowerInvariant();
                if (ContainsAny(replyLower,
                        "уточни", "що саме", "маєш на увазі", "звучить як", "по твоїй", "останній запит", "останній сигнал"))
                    return true;
            }

            return false;
        }

        private static bool AllowsExplicitUserTextOperation(string userText)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            return ContainsAny(lower,
                "перепиши", "перефразуй", "переклади", "переведи", "процитуй",
                "дослівно", "цитату", "цитуй", "summarize", "translate", "rewrite", "quote");
        }

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
            return normalized is "\u043f\u0440\u0438\u0432\u0456\u0442" or "\u043f\u0440\u0438\u0432\u0435\u0442" or "\u0445\u0430\u0439" or "\u0439\u043e" or "\u0434\u0430\u0440\u043e\u0432\u0430" or "\u0437\u0434\u043e\u0440\u043e\u0432" or "\u043a\u0443"
                or "привіт" or "привет" or "хай" or "йо" or "дарова" or "здоров" or "ку";
        }

        private static bool LooksLikeMisreadAffection(string replyLower)
            => ContainsAny(replyLower, "факт", "зафіксу", "випад", "болюч", "ризиклив", "реальн", "припини");

        private static bool LooksLikeHostileStaleRepair(string replyLower)
            => ContainsAny(replyLower, "щойно сказала", "зафіксувала", "твій випад", "короткий замик", "припини це");

        private static bool LooksLikeBadGreetingReply(string replyLower)
            => ContainsAny(replyLower,
                "тема «привіт", "тема \"привіт", "тема привіт", "добиваємо", "ще не відпустила", "знову відкрив",
                "графік сну", "графік сон", "сон-н", "сон няння", "прокинувся", "пробудження", "профіль за низьку ефективність",
                "випадкову генерацію чисел", "повернутися до спілкування", "режим користувача", "хронометраж");

        private static bool LooksLikeOverbuiltGreetingReply(string reply)
        {
            var text = (reply ?? "").Trim();
            if (text.Length > 180) return true;
            var lower = text.ToLowerInvariant();
            if (text.Count(c => c == '\n') >= 2) return true;
            return ContainsAny(lower,
                "я бачу, ти вирішив",
                "твій графік",
                "сон та пробудження",
                "режим \"сон",
                "низьку ефективність",
                "випадкову генерацію",
                "переходимо до чогось продуктивнішого",
                "перевірити, чи я ще");
        }

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

            return "У контексті графіка уточни об'єкт: мій таймер/план, твій розклад, чи графік у коді. Без копіювання твоєї фрази, так, прогрес неймовірний.";
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
                    : "Так, бачу: запобіжник сам почав жувати хвіст. Скинула. Працюю з останнім наміром, але без дослівного повтору твоєї фрази.";

            return string.IsNullOrWhiteSpace(compactUser)
                ? "Дубль відповіді прибрала. Дай останній запит ще раз або кинь конкретику, і цього разу без старого хвоста."
                : "Дубль відповіді прибрала. Повертаюсь до останнього запиту по суті, а не до старого хвоста.";
        }

        private static bool IsImagePrompt(string text)
            => ContainsAny((text ?? "").ToLowerInvariant(),
                "\u0444\u043e\u0442\u043e",
                "\u0437\u043e\u0431\u0440\u0430\u0436",
                "\u043a\u0430\u0440\u0442\u0438\u043d",
                "[image]",
                "[photo]",
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

        private static bool LooksLikeForbiddenAiOrServicePhrase(string replyLower)
            => ContainsAny(replyLower,
                "as an ai", "as a language model", "i am an ai", "i'm an ai", "i cannot as an ai",
                "how can i help", "how may i assist", "i'm here to help", "i am here to help",
                "як штучний інтелект", "як мовна модель", "чим я можу допомогти", "я тут, щоб допомогти",
                "СЏРє С€С‚СѓС‡РЅРёР№ С–РЅС‚РµР»РµРєС‚", "СЏРє РјРѕРІРЅР° РјРѕРґРµР»СЊ", "С‡РёРј СЏ РјРѕР¶Сѓ РґРѕРїРѕРјРѕРіС‚Рё");

        private static bool LooksLikeGenericRoleplaySyntax(string reply)
            => System.Text.RegularExpressions.Regex.IsMatch(reply ?? "", @"(^|\n)\s*\*[^*\n]{3,120}\*", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static bool LooksLikeTechnicalPauseMetric(string replyLower)
            => System.Text.RegularExpressions.Regex.IsMatch(replyLower ?? "", @"\b\d+\s*(год|годин|хв|хвилин|hours?|mins?|minutes?)\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant) &&
               ContainsAny(replyLower ?? "", "пауза", "перерва", "минуло", "від останньої", "absence", "pause");

        private static bool LooksLikeLazyClarificationLoop(string userLower, string replyLower)
        {
            var socialOrVague = userLower.Length < 140 &&
                ContainsAny(userLower, "дурниц", "просто поговор", "про тебе", "про мене", "миле", "заігру", "пошаліт", "хм", "ну типу", "щось");
            if (!socialOrVague) return false;
            return ContainsAny(replyLower,
                "що саме ти маєш на увазі", "будь конкретнішим", "уточни", "be more specific",
                "what do you mean", "can you clarify", "please clarify");
        }

        private static bool LooksLikeRoboticSupportTone(string userLower, string replyLower)
        {
            if (string.IsNullOrWhiteSpace(replyLower)) return false;

            var canned = ContainsAny(replyLower,
                "i understand", "i get it", "happy to help", "let me know", "how can i help", "how may i assist",
                "\u044f \u0440\u043e\u0437\u0443\u043c\u0456\u044e", "\u044f \u043f\u043e\u043d\u0456\u043c\u0430\u044e", "\u0447\u0438\u043c \u043c\u043e\u0436\u0443 \u0434\u043e\u043f\u043e\u043c\u043e\u0433\u0442\u0438", "\u044f\u043a \u044f \u043c\u043e\u0436\u0443 \u0434\u043e\u043f\u043e\u043c\u043e\u0433\u0442\u0438",
                "\u0431\u0443\u0434\u044c \u043b\u0430\u0441\u043a\u0430, \u0443\u0442\u043e\u0447\u043d", "\u0431\u0443\u0434\u044c \u043b\u0430\u0441\u043a\u0430 \u0443\u0442\u043e\u0447\u043d", "\u0434\u044f\u043a\u0443\u044e, \u0449\u043e \u043f\u043e\u0434\u0456\u043b\u0438\u0432", "\u0440\u0430\u0434\u0438\u0439 \u0434\u043e\u043f\u043e\u043c\u043e\u0433\u0442\u0438",
                "\u044f \u043f\u043e\u043d\u0438\u043c\u0430\u044e", "\u0447\u0435\u043c \u043c\u043e\u0433\u0443 \u043f\u043e\u043c\u043e\u0447\u044c", "\u0431\u0443\u0434\u044c \u0434\u043e\u0431\u0440, \u0443\u0442\u043e\u0447\u043d", "\u0440\u0430\u0434 \u043f\u043e\u043c\u043e\u0447\u044c");
            if (!canned) return false;

            var socialOrLowSignal = userLower.Length < 180 && ContainsAny(userLower,
                "\u043f\u0440\u043e\u0441\u0442\u043e \u043f\u043e\u0433\u043e\u0432\u043e\u0440", "\u0434\u0443\u0440\u043d\u0438\u0446", "\u043f\u0440\u043e \u0442\u0435\u0431\u0435", "\u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u043c\u0438\u043b\u0435", "\u0437\u0430\u0456\u0433\u0440", "\u0444\u043b\u0456\u0440\u0442", "\u0449\u043e\u0441\u044c", "\u043d\u0443 \u0442\u0438\u043f\u0443",
                "just talk", "flirt", "something", "whatever");
            var concrete = ContainsAny(replyLower,
                "file", "line", "commit", "test", "build", "diff", "log", "sensor", "watch",
                "\u0444\u0430\u0439\u043b", "\u0440\u044f\u0434\u043e\u043a", "\u043a\u043e\u043c\u0456\u0442", "\u0442\u0435\u0441\u0442", "\u043b\u043e\u0433", "\u043f\u043e\u043c\u0438\u043b\u043a", "\u0434\u0430\u0442\u0447\u0438\u043a", "\u043f\u0443\u043b\u044c\u0441", "\u0447\u0430\u0441\u0438")
                || replyLower.Contains("```", StringComparison.Ordinal);

            return socialOrLowSignal || !concrete;
        }

        private static bool LooksLikeConversationReviewDeflection(string userLower, string replyLower)
        {
            if (string.IsNullOrWhiteSpace(userLower) || string.IsNullOrWhiteSpace(replyLower)) return false;
            var asksReview = ContainsAny(userLower,
                "\u043f\u043e\u0434\u0438\u0432\u0438\u0441\u044c \u0443\u0432\u0430\u0436\u043d",
                "\u043f\u043e\u0434\u0438\u0432\u0438\u0441\u044c \u0440\u043e\u0437\u043c\u043e\u0432",
                "\u043f\u0435\u0440\u0435\u0447\u0438\u0442\u0430\u0439 \u0440\u043e\u0437\u043c\u043e\u0432",
                "\u0443\u0432\u0430\u0436\u043d\u0456\u0448\u0435 \u0440\u043e\u0437\u043c\u043e\u0432",
                "\u0443\u0432\u0430\u0436\u043d\u0456\u0448\u0435 \u0434\u0456\u0430\u043b\u043e\u0433",
                "look closer at the conversation",
                "read the conversation");
            if (!asksReview) return false;
            return ContainsAny(replyLower,
                "\u0449\u043e \u0441\u0430\u043c\u0435",
                "\u043a\u043e\u043d\u043a\u0440\u0435\u0442\u0438\u0437\u0443\u0439",
                "\u0443\u0442\u043e\u0447\u043d\u0438",
                "what exactly",
                "be more specific",
                "clarify");
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
                : "останній сигнал уже фіксував їжу";
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
                : "останній сигнал уже фіксував сон";
            return $"Стоп. {mention}. Стару драму про сон прибрано; відповідаю по теперішньому стану.";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
