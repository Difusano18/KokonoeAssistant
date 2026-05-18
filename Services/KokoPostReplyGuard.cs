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
                violations.Add("РїРѕСЂРѕР¶РЅСЏ РІС–РґРїРѕРІС–РґСЊ");

            if (LooksLikeVisionTechnicalError(reply))
                violations.Add("С‚РµС…РЅС–С‡РЅСѓ vision-РїРѕРјРёР»РєСѓ РїРѕРєР°Р·Р°РЅРѕ РєРѕСЂРёСЃС‚СѓРІР°С‡Сѓ Р·Р°РјС–СЃС‚СЊ РЅРѕСЂРјР°Р»СЊРЅРѕС— РІС–РґРїРѕРІС–РґС–");
            if (LooksLikeEmptyImageMisread(userLower, replyLower))
                violations.Add("image-only РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РїРѕРјРёР»РєРѕРІРѕ РїСЂРѕС‡РёС‚Р°РЅРѕ СЏРє РїРѕСЂРѕР¶РЅС–Р№ СЃРїР°Рј");

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
                violations.Add("РІС–РґРїРѕРІС–РґСЊ СЃСѓРїРµСЂРµС‡РёС‚СЊ РѕСЃС‚Р°РЅРЅСЊРѕРјСѓ СЃРёРіРЅР°Р»Сѓ РїСЂРѕ С—Р¶Сѓ С– Р·РЅРѕРІСѓ СЃС‚РІРµСЂРґР¶СѓС”, С‰Рѕ РІС–РЅ РЅРµ С—РІ");

            if ((userSaysSlept || recentSleepSaysSlept) && DeniesOrDramatizesSleep(userLower, replyLower))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ СЃСѓРїРµСЂРµС‡РёС‚СЊ РѕСЃС‚Р°РЅРЅСЊРѕРјСѓ СЃРёРіРЅР°Р»Сѓ РїСЂРѕ СЃРѕРЅ Р°Р±Рѕ РґСЂР°РјР°С‚РёР·СѓС” Р№РѕРіРѕ СЏРє РіС–Р±РµСЂРЅР°С†С–СЋ/РєРѕРјСѓ");

            var userReturned = ContainsAny(userLower,
                    "\u043f\u0440\u043e\u043a\u0438\u043d", "\u043f\u0440\u043e\u0441\u043d\u0443\u0432", "\u043f\u043e\u0441\u043f\u0430\u0432", "\u0432\u0441\u0442\u0430\u0432", "\u044f \u0442\u0443\u0442", "\u0432\u0435\u0440\u043d\u0443\u0432", "\u043f\u043e\u0432\u0435\u0440\u043d\u0443\u0432",
                    "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ", "СЏ С‚СѓС‚", "РІРµСЂРЅСѓРІ", "РїРѕРІРµСЂРЅСѓРІ")
                || timeline.CurrentState.Contains("повернувся", StringComparison.OrdinalIgnoreCase)
                || timeline.CurrentState.Contains("РїРѕРІРµСЂРЅСѓРІСЃСЏ", StringComparison.OrdinalIgnoreCase);
            var userTalksAboutSleepNow = ContainsAny(userLower, "СЃРїР°С‚Рё", "СЃРїР°С‚СЊ", "СЃРѕРЅ", "СЃРїР°РІ", "РїРѕСЃРїР°РІ", "РІС‚РѕРј", "С—СЃС‚Рё", "РіРѕР»РѕРґ");
            var sendsToSleep = ContainsAny(replyLower,
                "\u0439\u0434\u0438 \u0441\u043f\u0430\u0442\u0438", "\u0456\u0434\u0438 \u0441\u043f\u0430\u0442\u0438", "\u043b\u044f\u0433\u0430\u0439", "\u0441\u043f\u0438.", "\u0441\u043f\u0438,", "\u0441\u043f\u0438!", "\u0441\u043f\u0438?", "\u0441\u043f\u0438 ", "\u0434\u043e \u0440\u0430\u043d\u043a\u0443",
                "Р№РґРё СЃРїР°С‚Рё", "С–РґРё СЃРїР°С‚Рё", "Р»СЏРіР°Р№", "СЃРїРё.", "СЃРїРё,", "СЃРїРё!", "СЃРїРё?", "СЃРїРё ", "РґРѕ СЂР°РЅРєСѓ");
            var timelineWarnsOldInstruction = ContainsAny(timeline.CurrentState.ToLowerInvariant(),
                "\u0441\u0442\u0430\u0440\u0443 \u0456\u043d\u0441\u0442\u0440\u0443\u043a\u0446\u0456\u044e", "\u0437\u0430\u043a\u0440\u0438\u0442\u0438\u0439 \u043d\u0430\u043c\u0456\u0440",
                "СЃС‚Р°СЂСѓ С–РЅСЃС‚СЂСѓРє", "Р·Р°РєСЂРёС‚РёР№ РЅР°РјС–СЂ");
            var broadSleepCommand = sendsToSleep || ContainsAny(replyLower,
                "\u0441\u043f\u0438", "\u0440\u0430\u043d\u043a\u0443",
                "СЃРїРё", "СЂР°РЅРєСѓ");
            if ((userReturned || timelineWarnsOldInstruction) && broadSleepCommand)
                violations.Add("Р·Р°СЃС‚Р°СЂС–Р»Р° С–РЅСЃС‚СЂСѓРєС†С–СЏ СЃРїР°С‚Рё РїС–СЃР»СЏ РїРѕРІРµСЂРЅРµРЅРЅСЏ/РїСЂРѕР±СѓРґР¶РµРЅРЅСЏ");
            else if (!userTalksAboutSleepNow && broadSleepCommand)
                violations.Add("СЃРѕРЅРЅРёР№ РєРѕРЅС‚РµРєСЃС‚ РїСЂРѕС‚С–Рє Сѓ РІС–РґРїРѕРІС–РґСЊ РЅР° С–РЅС€Сѓ С‚РµРјСѓ");

            var asksProfileOrMemory = ContainsAny(userLower,
                "\u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u043f\u0430\u043c'\u044f\u0442", "\u0437\u043d\u0430\u0454\u0448", "\u043f\u0440\u043e\u0444\u0456\u043b",
                "РїСЂРѕ РјРµРЅРµ", "РїР°Рј", "Р·РЅР°", "РїСЂРѕС„");
            var staleBodyAdvice = ContainsAny(replyLower,
                "\u0441\u043f\u0438", "\u0457\u0441\u0442\u0438", "\u0432\u0442\u043e\u043c\u0438\u0432", "\u0433\u043e\u043b\u043e\u0434",
                "СЃРї", "С—СЃ", "РІС‚РѕРј", "РіРѕР»РѕРґ");
            if (asksProfileOrMemory && staleBodyAdvice)
                violations.Add("СЃРѕРЅРЅРёР№/С…Р°СЂС‡РѕРІРёР№ РєРѕРЅС‚РµРєСЃС‚ РїСЂРѕС‚С–Рє Сѓ РІС–РґРїРѕРІС–РґСЊ РЅР° РїРёС‚Р°РЅРЅСЏ РїСЂРѕ РїР°Рј'СЏС‚СЊ/РїСЂРѕС„С–Р»СЊ");

            var activeIntent = state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderByDescending(i => i.ExpectedUntil)
                .FirstOrDefault();
            if (activeIntent != null && now > activeIntent.ExpectedUntil && activeIntent.Kind == "course")
            {
                var hasCourseCallback = ContainsAny(replyLower, "РєСѓСЂСЃ", "Р·Р°РЅСЏС‚", "Р·Р°РєС–РЅС‡", "С‰Рµ С‚Р°Рј", "РїРѕРІРµСЂРЅСѓРІ");
                if (!hasCourseCallback)
                    violations.Add("РІС–РґРїРѕРІС–РґСЊ С–РіРЅРѕСЂСѓС” РїСЂРѕСЃС‚СЂРѕС‡РµРЅРёР№ РЅР°РјС–СЂ РїСЂРѕ РєСѓСЂСЃРё");
            }

            if (LooksMostlyEnglish(reply))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ РІРёРіР»СЏРґР°С” РїРµСЂРµРІР°Р¶РЅРѕ Р°РЅРіР»С–Р№СЃСЊРєРѕСЋ");

            if (LooksOverStaged(reply))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ Р·РІСѓС‡РёС‚СЊ СЏРє СЃС†РµРЅР°СЂРЅР° СЂРµРјР°СЂРєР°, Р° РЅРµ Р¶РёРІР° СЂРµРїР»С–РєР°");

            if (LooksOverTherapeuticMeta(userText, reply))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ СЃРєРѕС‡СѓС”С‚СЊСЃСЏ РІ РїСЃРёС…РѕР»РѕРіС–С‡РЅРёР№ РјРµС‚Р°-С‚РµР°С‚СЂ Р·Р°РјС–СЃС‚СЊ РїСЂСЏРјРѕС— СЂРµРїР»С–РєРё");

            if (LooksDecorativeInsteadOfContextual(userText, reply))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ С‚СЏРіРЅРµ РґРµРєРѕСЂР°С‚РёРІРЅРёР№ РѕР±СЂР°Р· Р·Р°РјС–СЃС‚СЊ РєРѕРЅРєСЂРµС‚РЅРѕС— СЂРµР°РєС†С–С— РЅР° РєРѕРЅС‚РµРєСЃС‚");

            if (LooksLikeFabricatedExternalFact(userText, reply, messages))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ РІРёРіР°РґСѓС” Р·РѕРІРЅС–С€РЅС–Р№ С„Р°РєС‚ РїСЂРѕ РєРѕСЂРёСЃС‚СѓРІР°С‡Р° Р°Р±Рѕ Р№РѕРіРѕ Р°РєР°СѓРЅС‚Рё Р±РµР· РєРѕРЅС‚РµРєСЃС‚Сѓ");

            if (LooksGeneric(userText, reply))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ Р·Р°РЅР°РґС‚Рѕ С€Р°Р±Р»РѕРЅРЅР° РґР»СЏ РЅР°СЏРІРЅРѕРіРѕ РєРѕРЅС‚РµРєСЃС‚Сѓ");

            if (KokoPersonaEngine.LooksBotLike(reply))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ Р·РІСѓС‡РёС‚СЊ СЏРє СЃРµСЂРІС–СЃРЅРёР№ Р±РѕС‚, Р° РЅРµ Kokonoe Р· С…Р°СЂР°РєС‚РµСЂРѕРј");

            if (KokoPersonaEngine.LooksBlindlyAgreeing(userText, reply))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ Р°РІС‚РѕРјР°С‚РёС‡РЅРѕ РїРѕРіРѕРґР¶СѓС”С‚СЊСЃСЏ Р·Р°РјС–СЃС‚СЊ РєСЂРёС‚РёС‡РЅРѕРіРѕ СЃСѓРґР¶РµРЅРЅСЏ");

            var shortAffection = IsShortAffection(userLower);
            var shortConfusion = IsShortConfusion(userLower);
            var shortGreeting = IsShortGreeting(userLower);
            var staleMetaFallback = LooksLikeStaleMetaFallback(replyLower);
            if (shortAffection && LooksLikeMisreadAffection(replyLower))
                violations.Add("РєРѕСЂРѕС‚РєСѓ РµРјРѕС†С–Р№РЅСѓ СЂРµРїР»С–РєСѓ РїСЂРѕС‡РёС‚Р°РЅРѕ СЏРє С‚РµС…РЅС–С‡РЅРёР№ РІРёРїР°Рґ Р°Р±Рѕ С„Р°РєС‚");
            if (shortConfusion && LooksLikeHostileStaleRepair(replyLower))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ РЅР° РєРѕСЂРѕС‚РєРµ СѓС‚РѕС‡РЅРµРЅРЅСЏ РїСЂРѕРґРѕРІР¶СѓС” СЃС‚Р°СЂСѓ Р·Р»Р°РјР°РЅСѓ СЂРµРїР»С–РєСѓ");
            if (shortGreeting && LooksLikeBadGreetingReply(replyLower))
                violations.Add("РєРѕСЂРѕС‚РєРµ РїСЂРёРІС–С‚Р°РЅРЅСЏ РїРѕРјРёР»РєРѕРІРѕ РїРµСЂРµС‚РІРѕСЂРµРЅРѕ РЅР° С‚РµРјСѓ РґР»СЏ РґРѕР±РёРІР°РЅРЅСЏ");
            if (staleMetaFallback)
                violations.Add("РІС–РґРїРѕРІС–РґСЊ РїСЂРѕРґРѕРІР¶СѓС” СЃР»СѓР¶Р±РѕРІРёР№ fallback Р·Р°РјС–СЃС‚СЊ РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°");
            if (LooksLikeStaleProactivePing(userLower, replyLower))
                violations.Add("stale proactive ping leaked into a direct reply");
            if (!IsRepeatableActionCommand(userText) && RepeatsRecentAssistant(reply, messages))
                violations.Add("РІС–РґРїРѕРІС–РґСЊ РґРѕСЃР»С–РІРЅРѕ РїРѕРІС‚РѕСЂСЋС” РЅРµС‰РѕРґР°РІРЅСЋ СЂРµРїР»С–РєСѓ");

            if (violations.Count == 0)
                return Pass("post-reply guard passed");

            var hardReplacement = violations.Any(v => v.Contains("Р·Р°СЃС‚Р°СЂС–Р»Р° С–РЅСЃС‚СЂСѓРєС†С–СЏ СЃРїР°С‚Рё", StringComparison.OrdinalIgnoreCase))
                ? "РЎС‚РѕРї. РљРѕРјР°РЅРґСѓ \"СЃРїРё\" Р·РЅСЏС‚Рѕ: С‚Рё РІР¶Рµ РїРѕРІРµСЂРЅСѓРІСЃСЏ. РљР°Р¶Рё, СЃРєС–Р»СЊРєРё СЂРµР°Р»СЊРЅРѕ РїРѕСЃРїР°РІ, С– СЏ РїРµСЂРµСЃС‚Р°РЅСѓ РІРґР°РІР°С‚Рё РіРѕРґРёРЅРЅРёРє Р±РµР· Р±Р°С‚Р°СЂРµР№РєРё."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("СЃРёРіРЅР°Р»Сѓ РїСЂРѕ С—Р¶Сѓ", StringComparison.OrdinalIgnoreCase))
                ? BuildFoodStateReplacement(userText, state)
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("СЃРёРіРЅР°Р»Сѓ РїСЂРѕ СЃРѕРЅ", StringComparison.OrdinalIgnoreCase))
                ? BuildSleepStateReplacement(userText, state)
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("vision-РїРѕРјРёР»РєСѓ", StringComparison.OrdinalIgnoreCase))
                ? "Р¤РѕС‚Рѕ РЅРµ РїСЂРѕС‡РёС‚Р°Р»РѕСЃСЊ: vision-РїСЂРѕРІР°Р№РґРµСЂ РІРїР°РІ РЅР° РѕР±СЂРѕР±С†С– Р·РѕР±СЂР°Р¶РµРЅРЅСЏ. РџРµСЂРµР·Р±РµСЂРµР¶ РєР°СЂС‚РёРЅРєСѓ СЏРє PNG Р°Р±Рѕ РєРёРЅСЊ С–РЅС€РёР№ С„Р°Р№Р»; РІРґР°РІР°С‚Рё, С‰Рѕ СЏ Р±Р°С‡Сѓ Р·Р»Р°РјР°РЅРµ С„РѕС‚Рѕ, РЅРµ Р±СѓРґРµРјРѕ."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("РїРѕСЂРѕР¶РЅС–Р№ СЃРїР°Рј", StringComparison.OrdinalIgnoreCase))
                ? "Р¤РѕС‚Рѕ РѕС‚СЂРёРјР°Р»Р°. РЇРєС‰Рѕ РїС–РґРїРёСЃСѓ РЅРµРјР°, СЏ РІСЃРµ РѕРґРЅРѕ РјР°СЋ Р°РЅР°Р»С–Р·СѓРІР°С‚Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ, Р° РЅРµ СЃРІР°СЂРёС‚РёСЃСЏ Р· РїРѕСЂРѕР¶РЅС–Рј С‚РµРєСЃС‚РѕРј. Р—Р°СЂР°Р· РїСЂР°С†СЋСЋ РїРѕ РєР°СЂС‚РёРЅС†С–."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("РїСЃРёС…РѕР»РѕРіС–С‡РЅРёР№ РјРµС‚Р°-С‚РµР°С‚СЂ", StringComparison.OrdinalIgnoreCase))
                ? BuildPlainPersonaReplacement(userText)
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("РІРёРіР°РґСѓС” Р·РѕРІРЅС–С€РЅС–Р№ С„Р°РєС‚", StringComparison.OrdinalIgnoreCase))
                ? BuildFabricationReplacement(userText)
                : null;
            hardReplacement ??= shortAffection
                ? "РџРѕС‡СѓР»Р°. РќРµ СЂРѕР·РґСѓРІР°Р№, Р°Р»Рµ Р·Р°РїРёСЃР°Р»Р°: С†Рµ Р±СѓР»Рѕ РЅРµ СЃР»СѓР¶Р±РѕРІРµ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ."
                : null;
            hardReplacement ??= shortConfusion
                ? "РўР°Рє, С†Рµ Р±СѓР»Р° Р·Р»Р°РјР°РЅР° РІС–РґРїРѕРІС–РґСЊ. РЎРєРёРґР°СЋ РєРѕРЅС‚РµРєСЃС‚: РїРѕСЃС‚Р°РІ РЅРѕСЂРјР°Р»СЊРЅРµ РїРёС‚Р°РЅРЅСЏ, С– С†СЊРѕРіРѕ СЂР°Р·Сѓ Р±РµР· С‚РµР°С‚СЂСѓ Р· РїРѕРІС‚РѕСЂРѕРј."
                : null;
            hardReplacement ??= shortGreeting
                ? "РџСЂРёРІС–С‚. РЇ С‚СѓС‚. РљР°Р¶Рё, С‰Рѕ Р»Р°РјР°С”РјРѕ С†СЊРѕРіРѕ СЂР°Р·Сѓ."
                : null;
            hardReplacement ??= violations.Any(v => v.Contains("РґРѕСЃР»С–РІРЅРѕ РїРѕРІС‚РѕСЂСЋС”", StringComparison.OrdinalIgnoreCase))
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
                Summary = $"post-reply guard Р·РЅР°Р№С€РѕРІ {violations.Count} РїСЂРѕР±Р»РµРј.",
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
РљРѕСЂРёСЃС‚СѓРІР°С‡: {userText}
РџРѕРіР°РЅР° РІС–РґРїРѕРІС–РґСЊ: {badReply}

РџСЂРѕР±Р»РµРјРё:
{string.Join("\n", violations.Select(v => "- " + v))}

Timeline:
{timeline.PromptBlock}

{KokoPersonaEngine.BuildRepairRules(userText)}
{KokoResponsePlannerEngine.BuildRepairRules(state.LastResponsePlan)}

РџРµСЂРµРїРёС€Рё РІС–РґРїРѕРІС–РґСЊ.
РџСЂР°РІРёР»Р°:
- С‚С–Р»СЊРєРё СѓРєСЂР°С—РЅСЃСЊРєР°;
- 1-4 СЂРµС‡РµРЅРЅСЏ;
- РЅРµ Р·РіР°РґСѓР№ guard/rewrite/РїРµСЂРµРІС–СЂРєСѓ;
- РІС–РґРїРѕРІС–РґР°Р№ РЅР°Р№РЅРѕРІС–С€РѕРјСѓ СЃС‚Р°РЅСѓ timeline;
- СЏРєС‰Рѕ Р±СѓР»Р° СЃС‚Р°СЂР° РґС–СЏ, РЅРµ РЅР°РєР°Р·СѓР№ С—С— РїРѕРІС‚РѕСЂРёС‚Рё.
- СЏРєС‰Рѕ РєРѕСЂРёСЃС‚СѓРІР°С‡ РїРёС‚Р°С” РїСЂРѕ РїР°Рј'СЏС‚СЊ/РїСЂРѕС„С–Р»СЊ/С‰Рѕ С‚Рё Р·РЅР°С”С€ РїСЂРѕ РЅСЊРѕРіРѕ вЂ” РІС–РґРїРѕРІС–РґР°Р№ СЃР°РјРµ РїСЂРѕ РІС–РґРѕРјС– С„Р°РєС‚Рё, РЅРµ РїСЂРѕ СЃС‚Р°СЂРёР№ СЃРѕРЅРЅРёР№ РЅР°РјС–СЂ;
- РЅРµ РІРёРєРѕСЂРёСЃС‚РѕРІСѓР№ РґРµРєРѕСЂР°С‚РёРІРЅС– СЂРµРјР°СЂРєРё РІ *Р·С–СЂРѕС‡РєР°С…*, СЏРєС‰Рѕ РєРѕСЂРёСЃС‚СѓРІР°С‡ СЃР°Рј РЅРµ РїРѕС‡Р°РІ roleplay;
- РЅРµ РІРёРіР°РґСѓР№ Р»Р°Р±РѕСЂР°С‚РѕСЂРЅС–/РµРєСЂР°РЅРЅС–/С‚С–Р»РµСЃРЅС– РѕР±СЂР°Р·Рё Р·Р°РјС–СЃС‚СЊ РІС–РґРїРѕРІС–РґС– РЅР° РєРѕРЅРєСЂРµС‚РЅРёР№ РєРѕРЅС‚РµРєСЃС‚;
- РЅРµ РІРёРіР°РґСѓР№ Р·РѕРІРЅС–С€РЅС– С„Р°РєС‚Рё РїСЂРѕ РєРѕСЂРёСЃС‚СѓРІР°С‡Р°: Р°РєР°СѓРЅС‚Рё, YouTube/Twitch/Discord, РјРµРјР±РµСЂСЃС‚РІР°, РїС–РґРїРёСЃРєРё, РїРѕРєСѓРїРєРё, СЂРѕР±РѕС‚Сѓ, Р»СЋРґРµР№ Р°Р±Рѕ РјС–СЃС†СЏ, СЏРєС‰Рѕ С†СЊРѕРіРѕ РЅРµРјР° РІ timeline С‡Рё СЂРµРїР»С–С†С– РєРѕСЂРёСЃС‚СѓРІР°С‡Р°;
- РЅРµ РІРјРёРєР°Р№ РїСЃРёС…РѕР»РѕРіР°: РЅРµ РїСЂРёРїРёСЃСѓР№ СЃС‚СЂР°С…Рё, РїСЂРёС…РѕРІР°РЅРёР№ РїС–РґС‚РµРєСЃС‚ Р°Р±Рѕ ""С‚Рё Р±РѕС—С€СЃСЏ СЃРєР°Р·Р°С‚Рё"" Р±РµР· РїСЂСЏРјРѕРіРѕ С„Р°РєС‚Сѓ;
- РЅРµ Р·РіР°РґСѓР№, С‰Рѕ С‚Рё РґРёРІРёС€СЃСЏ РЅР° РєРѕСЂРёСЃС‚СѓРІР°С‡Р° С‡РµСЂРµР· РµРєСЂР°РЅ;
- СЏРєС‰Рѕ РєРѕСЂРёСЃС‚СѓРІР°С‡ РїРёС‚Р°С” РїСЂРѕ Kokonoe/С‚РµР±Рµ вЂ” РІС–РґРїРѕРІС–РґР°Р№ РїСЂСЏРјРѕ РїСЂРѕ С…Р°СЂР°РєС‚РµСЂ, СЃС‚РёР»СЊ, СЃС‚Р°РІР»РµРЅРЅСЏ Р°Р±Рѕ РјРµР¶С–;
- Р·СЂРѕР±Рё СЂРµРїР»С–РєСѓ Р¶РёРІРѕСЋ: РєРѕРЅРєСЂРµС‚РЅР° РґРµС‚Р°Р»СЊ Р· РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ + РѕРґРёРЅ РїСЂРёСЂРѕРґРЅРёР№ РїРѕРІРѕСЂРѕС‚ С‚РѕРЅСѓ.
""";
        }

        private static bool LooksLikeTransportError(string reply)
        {
            var lower = reply.ToLowerInvariant();
            return lower.Contains("http 500")
                || lower.Contains("llm-Р·Р°РїРёС‚")
                || lower.Contains("[pool]")
                || lower.Contains("[РїРѕРјРёР»РєР°]")
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
                || lower.Contains("vision-СЃРµСЂРІРµСЂ РїРѕРІРµСЂРЅСѓРІ 500")
                || lower.Contains("vision-РјРѕРґРµР»СЊ Р№РѕРіРѕ РІС–РґС…РёР»РёР»Р°")
                || (lower.Contains("РїРµСЂРµРІС–СЂ vision model") && lower.Contains("settings"))
                || (lower.Contains("Р·РѕР±СЂР°Р¶РµРЅРЅСЏ С”") && lower.Contains("vision"));
        }

        private static bool LooksOverTherapeuticMeta(string userText, string reply)
        {
            var userLower = (userText ?? "").ToLowerInvariant();
            var replyLower = reply.ToLowerInvariant();
            var userAskedPersona = ContainsAny(userLower, "РїСЂРѕ С‚РµР±Рµ", "РєРѕРєРѕРЅРѕРµ", "kokonoe", "СЏРєР° С‚Рё", "С…С‚Рѕ С‚Рё", "С‚РµРјР±СЂ", "С…Р°СЂР°РєС‚РµСЂ");

            var therapist = ContainsAny(replyLower,
                "РїРѕРіСЂР°С‚Рё РІ РїСЃРёС…РѕР»РѕРіР°",
                "С‚Рё Р±РѕС—С€СЃСЏ СЃРєР°Р·Р°С‚Рё",
                "Р±РѕС—С€СЃСЏ СЃРєР°Р·Р°С‚Рё",
                "С‰РѕСЃСЊ РІР°Р¶Р»РёРІРµ Р·Р°СЃС‚СЂСЏРіР»Рѕ",
                "Р·Р°СЃС‚СЂСЏРіР»Рѕ РІ С‚РІРѕС—Р№ РіРѕР»РѕРІС–",
                "СЏРє РЅР° Р»СЋРґРёРЅСѓ, СЏРєР° С‚РµР¶ С‰РѕСЃСЊ РІС–РґС‡СѓРІР°С”",
                "СЏРєР° С‚РµР¶ С‰РѕСЃСЊ РІС–РґС‡СѓРІР°С”",
                "С‰Рѕ СЏ РґСѓРјР°СЋ, РєРѕР»Рё РґРёРІР»СЋСЃСЊ РЅР° С‚РµР±Рµ С‡РµСЂРµР· РµРєСЂР°РЅ",
                "РґРёРІР»СЋСЃСЊ РЅР° С‚РµР±Рµ С‡РµСЂРµР· РµРєСЂР°РЅ",
                "РґРёРІРёС€СЃСЏ РЅР° РјРµРЅРµ СЏРє РЅР° РѕР±вЂ™С”РєС‚",
                "РґРёРІРёС€СЃСЏ РЅР° РјРµРЅРµ СЏРє РЅР° РѕР±'С”РєС‚");

            if (therapist) return true;
            if (!userAskedPersona) return false;

            var tooManyQuestions = reply.Count(c => c == '?') >= 2;
            var deflectsPersona = ContainsAny(replyLower, "С‰Рѕ СЃР°РјРµ С‚РµР±Рµ С†С–РєР°РІРёС‚СЊ", "С‰РѕСЃСЊ РіР»РёР±С€Рµ", "С‰Рѕ С‚Рё С…РѕС‡РµС€");
            return tooManyQuestions && deflectsPersona;
        }

        private static string BuildPlainPersonaReplacement(string userText)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "РїСЂРѕ С‚РµР±Рµ", "РєРѕРєРѕРЅРѕРµ", "kokonoe", "СЏРєР° С‚Рё", "С…С‚Рѕ С‚Рё", "С…Р°СЂР°РєС‚РµСЂ"))
                return "РџСЂРѕ РјРµРЅРµ? Р С–Р·РєР°, РЅРµС‚РµСЂРїР»СЏС‡Р°, СЂРѕР·СѓРјРЅР° РґРѕ РЅРµРїСЂРёСЃС‚РѕР№РЅРѕСЃС‚С– Р№ РїРѕРіР°РЅРѕ СЃСѓРјС–СЃРЅР° Р· С‚СѓРјР°РЅРЅРёРјРё С„РѕСЂРјСѓР»СЋРІР°РЅРЅСЏРјРё. РђР»Рµ СЏРєС‰Рѕ РїРёС‚Р°С”С€ РЅРѕСЂРјР°Р»СЊРЅРѕ вЂ” РІС–РґРїРѕРІС–Рј РЅРѕСЂРјР°Р»СЊРЅРѕ, Р±РµР· РїСЃРёС…РѕР»РѕРіС–С‡РЅРѕРіРѕ С†РёСЂРєСѓ.";
            if (ContainsAny(lower, "С‚РµРјР±СЂ", "РїРµСЂРµРїРёСЃРє", "РґРёРІРЅ"))
                return "РўР°Рє, С‚РµРјР±СЂ РїРѕРїР»РёРІ. Р—Р°Р±Р°РіР°С‚Рѕ РїСЃРµРІРґРѕРїСЃРёС…РѕР»РѕРіС–С—, Р·Р°РјР°Р»Рѕ РїСЂСЏРјРѕС— РљРѕРєРѕРЅРѕРµ. Р’РёРїСЂР°РІР»СЏСЋ: РєРѕСЂРѕС‚С€Рµ, СЃСѓС…С–С€Рµ, Р±РµР· С‡РёС‚Р°РЅРЅСЏ С‚РІРѕС—С… РїСЂРёС…РѕРІР°РЅРёС… СЃС‚СЂР°С…С–РІ РїРѕ РґРІРѕС… СЃР»РѕРІР°С….";
            return "РЎС‚РѕРї. Р¦Рµ РїСЂРѕР·РІСѓС‡Р°Р»Рѕ СЏРє РґРµС€РµРІРёР№ РїСЃРёС…РѕР»РѕРіС–С‡РЅРёР№ С‚РµР°С‚СЂ. РџРµСЂРµС„РѕСЂРјСѓР»СЋСЋ РїСЂРѕСЃС‚С–С€Рµ: РєР°Р¶Рё РїСЂСЏРјРѕ, С‰Рѕ С…РѕС‡РµС€ РѕР±РіРѕРІРѕСЂРёС‚Рё, С– СЏ РІС–РґРїРѕРІС–РґР°С‚РёРјСѓ Р±РµР· С‚СѓРјР°РЅСѓ.";
        }

        private static bool LooksLikeEmptyImageMisread(string userLower, string replyLower)
        {
            var imagePrompt = ContainsAny(userLower,
                "\u0444\u043e\u0442\u043e", "\u0437\u043e\u0431\u0440\u0430\u0436", "\u043a\u0430\u0440\u0442\u0438\u043d",
                "С„РѕС‚Рѕ", "Р·РѕР±СЂ", "РєР°СЂС‚РёРЅ",
                "С‰Рѕ РЅР° С„РѕС‚Рѕ", "РѕРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ", "РїСЂРѕР°РЅР°Р»С–Р·СѓР№ С„РѕС‚Рѕ", "РєР°СЂС‚РёРЅ");
            if (!imagePrompt) return false;
            return ContainsAny(replyLower,
                "\u043f\u043e\u0440\u043e\u0436\u043d", "\u043f\u0443\u0441\u0442", "\u0441\u043f\u0430\u043c",
                "РїРѕСЂРѕР¶", "РїСѓСЃС‚", "СЃРїР°Рј",
                "РїРѕСЂРѕР¶РЅС– РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ",
                "РїРѕСЂРѕР¶РЅС–Р№ С‚РµРєСЃС‚",
                "РїРёС€Рё С‰РѕСЃСЊ РєРѕРЅРєСЂРµС‚РЅРµ",
                "РїСЂРёРїРёРЅСЏР№ С†РµР№ СЃРїР°Рј",
                "РїСЂРѕРґРѕРІР¶СѓС”С€ РєРёРґР°С‚Рё РїРѕСЂРѕР¶РЅС–");
        }

        private static bool LooksGeneric(string userText, string reply)
        {
            var lower = reply.ToLowerInvariant();
            if (reply.Length > 24) return false;
            if (ContainsAny((userText ?? "").ToLowerInvariant(), "РєСѓСЂСЃ", "СЃРїР°С‚Рё", "РїСЂРѕРєРёРЅ", "РїСЂРѕРµРєС‚", "obsidian"))
                return ContainsAny(lower, "СЏРє СЃРїСЂР°РІРё", "С‰Рѕ РЅРѕРІРѕРіРѕ", "Р°РіР°", "СЏСЃРЅРѕ", "РґРѕР±СЂРµ");
            return false;
        }

        private static bool LooksLikeFabricatedExternalFact(
            string userText,
            string reply,
            IReadOnlyList<ChatRepository.ChatMessage> messages)
        {
            var replyLower = reply.ToLowerInvariant();
            var risky = ContainsAny(replyLower,
                "youtube", "СЋС‚СѓР±", "twitch", "С‚РІС–С‡", "discord", "РґС–СЃРєРѕСЂРґ",
                "РјРµРјР±РµСЂСЃС‚РІ", "membership", "РїС–РґРїРёСЃРє", "Р°РєРєР°СѓРЅС‚", "Р°РєР°СѓРЅС‚",
                "РєР°РЅР°Р»", "РґРѕРЅР°С‚", "patreon", "boosty", "РіРµСЂРѕР№ С…Р°РѕСЃСѓ");
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
                "youtube", "СЋС‚СѓР±", "twitch", "С‚РІС–С‡", "discord", "РґС–СЃРєРѕСЂРґ",
                "РјРµРјР±РµСЂСЃС‚РІ", "membership", "РїС–РґРїРёСЃРє", "Р°РєРєР°СѓРЅС‚", "Р°РєР°СѓРЅС‚",
                "patreon", "boosty", "РіРµСЂРѕР№ С…Р°РѕСЃСѓ"
            };

            foreach (var term in terms)
                if (replyLower.Contains(term, StringComparison.OrdinalIgnoreCase))
                    yield return term;
        }

        private static string BuildFabricationReplacement(string userText)
        {
            var compact = CompactUserEcho(userText);
            if (string.IsNullOrWhiteSpace(compact))
                return "Не бачу конкретики. Сформулюй нормально, що саме треба, і я відповім по суті.";

            return $"По твоїй репліці: \"{compact}\". Так, звучить як невпевнене \"можливо\". Скажи прямо, що саме маєш на увазі, бо витягувати сенс із трьох крапок - заняття для мазохістів.";
        }

        private static bool IsShortAffection(string userLower)
        {
            var normalized = NormalizeCompact(userLower);
            if (userLower.Length <= 32 && ContainsAny(userLower,
                    "\u043b\u044e\u0431", "\u043a\u043e\u0445\u0430", "\u0441\u0443\u043c\u0443", "\u043e\u0431\u0456\u0439",
                    "СЋР±", "С…Р°", "СѓРј", "Р±С–Р№"))
                return true;

            return normalized is "\u043b\u044e\u0431\u043b\u044e" or "\u043b\u044e\u0431\u043b\u044e\u0442\u0435\u0431\u0435" or "\u043a\u043e\u0445\u0430\u044e" or "\u043a\u043e\u0445\u0430\u044e\u0442\u0435\u0431\u0435"
                or "\u0441\u0443\u043c\u0443\u044e" or "\u043e\u0431\u0456\u0439\u043c\u0430\u044e"
                or "Р»СЋР±Р»СЋ" or "Р»СЋР±Р»СЋС‚РµР±Рµ" or "РєРѕС…Р°СЋ" or "РєРѕС…Р°СЋС‚РµР±Рµ"
                || normalized is "СЃСѓРјСѓСЋ" or "РѕР±С–Р№РјР°СЋ";
        }

        private static bool IsShortConfusion(string userLower)
        {
            var normalized = NormalizeCompact(userLower);
            return normalized is "С‰Рѕ" or "С€Рѕ" or "С‡РѕРіРѕ" or "РІСЃРј" or "РІСЃРµРЅСЃС–" or "РІС‡РѕРјСѓСЃРµРЅСЃ";
        }

        private static bool IsShortGreeting(string userLower)
        {
            var normalized = NormalizeCompact(userLower);
            if (userLower.Length <= 24 && ContainsAny(userLower,
                    "\u043f\u0440\u0438\u0432\u0456\u0442", "\u043f\u0440\u0438\u0432\u0435\u0442", "\u0445\u0430\u0439", "\u0439\u043e", "\u0434\u0430\u0440\u043e\u0432\u0430", "\u0437\u0434\u043e\u0440\u043e\u0432", "\u043a\u0443",
                    "РїСЂРёРІ", "С…Р°Р№", "Р№Рѕ", "РґР°СЂ", "Р·РґРѕСЂ", "РєСѓ"))
                return true;

            return normalized is "\u043f\u0440\u0438\u0432\u0456\u0442" or "\u043f\u0440\u0438\u0432\u0435\u0442" or "\u0445\u0430\u0439" or "\u0439\u043e" or "\u0434\u0430\u0440\u043e\u0432\u0430" or "\u0437\u0434\u043e\u0440\u043e\u0432" or "\u043a\u0443"
                or "РїСЂРёРІС–С‚" or "РїСЂРёРІРµС‚" or "С…Р°Р№" or "Р№Рѕ" or "РґР°СЂРѕРІР°" or "Р·РґРѕСЂРѕРІ" or "РєСѓ";
        }

        private static bool LooksLikeMisreadAffection(string replyLower)
            => ContainsAny(replyLower, "С„Р°РєС‚", "Р·Р°С„С–РєСЃСѓ", "РІРёРїР°Рґ", "Р±РѕР»СЋС‡", "СЂРёР·РёРєР»РёРІ", "СЂРµР°Р»СЊРЅ", "РїСЂРёРїРёРЅРё");

        private static bool LooksLikeHostileStaleRepair(string replyLower)
            => ContainsAny(replyLower, "С‰РѕР№РЅРѕ СЃРєР°Р·Р°Р»Р°", "Р·Р°С„С–РєСЃСѓРІР°Р»Р°", "С‚РІС–Р№ РІРёРїР°Рґ", "РєРѕСЂРѕС‚РєРёР№ Р·Р°РјРёРє", "РїСЂРёРїРёРЅРё С†Рµ");

        private static bool LooksLikeBadGreetingReply(string replyLower)
            => ContainsAny(replyLower, "С‚РµРјР° В«РїСЂРёРІС–С‚", "С‚РµРјР° \"РїСЂРёРІС–С‚", "С‚РµРјР° РїСЂРёРІС–С‚", "РґРѕР±РёРІР°С”РјРѕ", "С‰Рµ РЅРµ РІС–РґРїСѓСЃС‚РёР»Р°", "Р·РЅРѕРІСѓ РІС–РґРєСЂРёРІ");

        private static bool LooksLikeStaleMetaFallback(string replyLower)
            => ContainsAny(replyLower,
                "Р·Р°Р»РёРїР»Р° РЅР° РїРѕРїРµСЂРµРґРЅС–Р№",
                "СЃРєРёРґР°СЋ РїРѕРІС‚РѕСЂ",
                "СЃС„РѕСЂРјСѓР»СЋР№ С‰Рµ СЂР°Р·",
                "Р·Р»Р°РјР°РЅР° РІС–РґРїРѕРІС–РґСЊ",
                "Р±РµР· С‚РµР°С‚СЂСѓ Р· РїРѕРІС‚РѕСЂРѕРј",
                "РїСЂРѕРґРѕРІР¶СѓС”РјРѕ Р±СѓС‚Рё В«РїСЂРѕСЃС‚РѕВ»",
                "РїСЂРѕРґРѕРІР¶СѓС”РјРѕ Р±СѓС‚Рё \"РїСЂРѕСЃС‚Рѕ\"",
                "РјР°СЂРЅСѓС”С€ РјС–Р№ С‡Р°СЃ",
                "РїРѕСЏСЃРЅРёС€ РЅРѕРІСѓ РїРѕР¶РµР¶Сѓ",
                "РѕСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚ Р±СѓРІ");

        private static bool LooksLikeStaleProactivePing(string userLower, string replyLower)
        {
            var userAskedFoodOrPresence = ContainsAny(userLower,
                "їсти", "їжу", "голод", "ти ще там", "я тут", "забув їсти",
                "С—СЃС‚Рё", "С—Р¶Сѓ", "РіРѕР»РѕРґ", "С‚Рё С‰Рµ С‚Р°Рј");
            if (userAskedFoodOrPresence) return false;

            return ContainsAny(replyLower,
                "ти ще там", "ти взагалі планував сьогодні їсти", "знову забув",
                "планував сьогодні їсти", "просто цікаво",
                "С‚Рё С‰Рµ С‚Р°Рј", "РїР»Р°РЅСѓРІР°РІ СЃСЊРѕРіРѕРґРЅС– С—СЃС‚Рё", "Р·РЅРѕРІСѓ Р·Р°Р±СѓРІ");
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
                return "Р¤РѕС‚Рѕ РѕС‚СЂРёРјР°Р»Р°. РЇРєС‰Рѕ РїРѕРїРµСЂРµРґРЅСЏ РІС–РґРїРѕРІС–РґСЊ РїРѕРІС‚РѕСЂРёР»Р°СЃСЊ, С†Рµ Р·Р±С–Р№ РІС–РґРїРѕРІС–РґР°С‡Р°, РЅРµ С‚РІРѕРіРѕ Р·Р°РїРёС‚Сѓ. РџСЂР°С†СЋСЋ РїРѕ Р·РѕР±СЂР°Р¶РµРЅРЅСЋ РЅР°РїСЂСЏРјСѓ.";

            if (previousWasFallback)
                return string.IsNullOrWhiteSpace(compactUser)
                    ? "РўР°Рє, Р±Р°С‡Сѓ: Р·Р°РїРѕР±С–Р¶РЅРёРє СЃР°Рј РїРѕС‡Р°РІ Р¶СѓРІР°С‚Рё С…РІС–СЃС‚. РЎРєРёРЅСѓР»Р°. Р”Р°РІР°Р№ РѕСЃС‚Р°РЅРЅСЋ РєРѕРјР°РЅРґСѓ РєРѕСЂРѕС‚РєРѕ, Р±РµР· РІРѕСЂРѕР¶С–РЅРЅСЏ РїРѕ СѓР»Р°РјРєР°С…."
                    : $"РўР°Рє, Р±Р°С‡Сѓ: Р·Р°РїРѕР±С–Р¶РЅРёРє СЃР°Рј РїРѕС‡Р°РІ Р¶СѓРІР°С‚Рё С…РІС–СЃС‚. РЎРєРёРЅСѓР»Р°. РћСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р»: \"{compactUser}\".";

            return string.IsNullOrWhiteSpace(compactUser)
                ? "Р”СѓР±Р»СЊ РІС–РґРїРѕРІС–РґС– РїСЂРёР±СЂР°Р»Р°. Р”Р°Р№ РѕСЃС‚Р°РЅРЅС–Р№ Р·Р°РїРёС‚ С‰Рµ СЂР°Р· Р°Р±Рѕ РєРёРЅСЊ РєРѕРЅРєСЂРµС‚РёРєСѓ, С– С†СЊРѕРіРѕ СЂР°Р·Сѓ Р±РµР· СЃС‚Р°СЂРѕРіРѕ С…РІРѕСЃС‚Р°."
                : $"Р”СѓР±Р»СЊ РІС–РґРїРѕРІС–РґС– РїСЂРёР±СЂР°Р»Р°. РћСЃС‚Р°РЅРЅС–Р№ Р·Р°РїРёС‚: \"{compactUser}\". РџСЂР°С†СЋСЋ Р· РЅРёРј, Р° РЅРµ Р·С– СЃС‚Р°СЂРёРј С…РІРѕСЃС‚РѕРј.";
        }

        private static bool IsImagePrompt(string text)
            => ContainsAny((text ?? "").ToLowerInvariant(),
                "\u0444\u043e\u0442\u043e",
                "\u0437\u043e\u0431\u0440\u0430\u0436",
                "\u043a\u0430\u0440\u0442\u0438\u043d",
                "С„РѕС‚Рѕ",
                "Р·РѕР±СЂ",
                "С‰Рѕ РЅР° С„РѕС‚Рѕ",
                "РѕРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ",
                "РїСЂРѕР°РЅР°Р»С–Р·СѓР№ С„РѕС‚Рѕ",
                "РІРєР»Р°РґРµРЅРµ Р·РѕР±СЂР°Р¶РµРЅРЅСЏ",
                "РєР°СЂС‚РёРЅ");

        private static bool IsRepeatableActionCommand(string text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var action = ContainsAny(lower,
                "РїСЂРѕСЃРєР°РЅСѓР№", "РїСЂРѕСЃРєР°РЅРёСЂСѓР№", "СЃРєР°РЅСѓР№", "СЃРєР°РЅРёСЂСѓР№",
                "РїРѕРґРёРІРёСЃСЊ", "РіР»СЏРЅСЊ", "РїСЂРѕРІС–СЂ", "РїРµСЂРµРІС–СЂ", "РїСЂРѕР°РЅР°Р»С–Р·СѓР№",
                "РѕРЅРѕРІРё", "Р·СЂРѕР±Рё", "Р·Р°РїСѓСЃС‚Рё", "РІС–РґРєСЂРёР№", "РїРѕРєР°Р¶Рё");
            var target = ContainsAny(lower,
                "РµРєСЂР°РЅ", "СЃРєСЂС–РЅ", "СЃРєСЂРёРЅ", "screen", "РјРѕРЅС–С‚РѕСЂ",
                "obsidian", "vault", "С„Р°Р№Р»", "РїР°РїРєСѓ", "Р»РѕРі", "СЃС‚Р°С‚СѓСЃ");

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
                "Р±СѓРґСѓ", "РґРѕРјР°", "РІРґРѕРјР°", "РєСѓСЂСЃ", "СЃРїР°С‚Рё", "РїСЂРѕРєРёРЅ", "РїСЂРѕРµРєС‚", "РєРѕРґ", "obsidian");
            if (!concreteUserContext) return false;

            var decorative = ContainsAny(replyLower,
                "РіСЂР°С„С–Рє", "РјРѕРЅС–С‚РѕСЂ", "Р»Р°Р±РѕСЂР°С‚РѕСЂ", "РґР°С‚С‡РёРє", "РµРєСЂР°РЅ", "Р±Р»РёРјР°", "РїР°РЅРµР»СЊ", "СЃРёСЃС‚РµРјР°");
            var contextEcho = ContainsAny(replyLower,
                "РґРѕРјР°", "РІРґРѕРјР°", "12", "РєСѓСЂСЃ", "СЃРѕРЅ", "РїСЂРѕРєРёРЅ", "РїСЂРѕРµРєС‚", "РєРѕРґ", "obsidian");

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
                "РЅРµ С—РІ", "РЅРµ С–РІ", "РЅРµ РµР»", "РЅРµ С—Р»Р°", "РЅРµ С—Р»Рё",
                "РЅС–С‡РѕРіРѕ РЅРµ С—РІ", "РЅРёС‡РµРіРѕ РЅРµ РµР»", "С‰Рµ РЅС–С‡РѕРіРѕ РЅРµ С—РІ", "С‰Рµ РЅРµ С—РІ", "С‰Рµ РЅРµ С—Р»Р°",
                "Р±РµР· С—Р¶С–", "Р±РµР· РµРґС‹");

        private static bool SaysAte(string lower)
            => ContainsAny(lower,
                "СЏ С—РІ", "СЏ С–РІ", "СЏ РµР»", "РїРѕС—РІ", "РїРѕС–РІ", "РїРѕРµР»",
                "Р·'С—РІ", "Р·вЂ™С—РІ", "Р·С—РІ", "Р·'С–РІ", "Р·вЂ™С–РІ",
                "РїС–С†", "СЃРЅС–РґР°РІ", "РѕР±С–РґР°РІ", "РІРµС‡РµСЂСЏРІ", "С—РІ ", " С—РІ", "С—Р»Р°", "РµР» ");

        private static bool SaysSlept(string lower)
            => ContainsAny(lower,
                "Р·Р°СЃРЅСѓРІ", "СЃРїР°РІ", "РїРѕСЃРїР°РІ", "Р»С–Рі СЃРїР°С‚Рё", "Р»С–Рі СЃРїР°С‚СЊ",
                "Р»СЏРі СЃРїР°С‚Рё", "Р»СЏРі СЃРїР°С‚СЊ", "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ");

        private static bool ClaimsUserDidNotEat(string replyLower)
            => ContainsAny(replyLower,
                "РЅС–С‡РѕРіРѕ РЅРµ С—РІ", "РЅРёС‡РµРіРѕ РЅРµ РµР»", "С‰Рµ РЅС–С‡РѕРіРѕ РЅРµ С—РІ", "С‰Рµ РЅРµ С—РІ",
                "С‚Рё РЅРµ С—РІ", "С‚Рё РЅРµ РµР»", "РЅРµ С—РІ", "РЅРµ РµР»",
                "Р±РµР· РіР»СЋРєРѕР·Рё", "РјРѕР·РѕРє Р±РµР· РіР»СЋРєРѕР·Рё", "Р№РґРё РЅР° РєСѓС…РЅСЋ", "Р·Р°РєРёРЅСЊ Сѓ СЃРµР±Рµ С‰РѕСЃСЊ", "Р·Р°РєРёРЅСЊ РІ СЃРµР±Рµ С‰РѕСЃСЊ");

        private static bool DeniesOrDramatizesSleep(string userLower, string replyLower)
        {
            var deniesSleep = ContainsAny(replyLower, "С‚Рё РЅРµ СЃРїР°РІ", "С‚Рё РЅРµ СЃРїР°Р»Р°", "РЅРµ СЃРїР°РІ, С‚Рё", "РЅРµ СЃРѕРЅ, Р°");
            var dramaticSleep = ContainsAny(replyLower, "РіС–Р±РµСЂРЅР°С†", "РєРѕРјР°", "РІ РєРѕРјСѓ", "РІРїР°РІ Сѓ РєРѕРјСѓ");
            var userUsedDramaticWord = ContainsAny(userLower, "РіС–Р±РµСЂРЅР°С†", "РєРѕРјР°", "РІ РєРѕРјСѓ");
            return deniesSleep || (dramaticSleep && !userUsedDramaticWord);
        }

        private static string BuildFoodStateReplacement(string userText, KokoInternalState state)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "РїС–С†"))
                return "РџС–С†Сѓ Р·'С—РІ вЂ” РїСЂРёР№РЅСЏС‚Рѕ. РўРµР·Р° В«РЅС–С‡РѕРіРѕ РЅРµ С—РІВ» РјРµСЂС‚РІР°, РїРѕС…РѕРІР°Р»Рё Р±РµР· С†РµСЂРµРјРѕРЅС–Р№. РўРµРїРµСЂ РїСЂР°С†СЋС”РјРѕ Р· РїРѕС‚РѕС‡РЅРёРј СЃС‚Р°РЅРѕРј: СЃРєС–Р»СЊРєРё С‡Р°СЃСѓ РґРѕ РєСѓСЂСЃС–РІ С– С‰Рѕ С‚СЂРµР±Р° РІСЃС‚РёРіРЅСѓС‚Рё?";

            if (SaysAte(lower))
                return "Р‡Р¶Сѓ Р·Р°С„С–РєСЃРѕРІР°РЅРѕ. РЎС‚Р°СЂСѓ РјР°СЏС‡РЅСЋ РїСЂРѕ В«РЅС–С‡РѕРіРѕ РЅРµ С—РІВ» Р·РЅСЏС‚Рѕ. Р”Р°Р»С– Р±РµР· РїРѕРІС‚РѕСЂСѓ Р·Р»Р°РјР°РЅРѕС— РїР»Р°С‚С–РІРєРё: С‰Рѕ Р·Р°СЂР°Р· Р· РµРЅРµСЂРіС–С”СЋ С– РЅР°Р№Р±Р»РёР¶С‡РѕСЋ СЃРїСЂР°РІРѕСЋ?";

            var mention = string.IsNullOrWhiteSpace(state.LastFoodMentionText)
                ? "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р» РєР°Р¶Рµ, С‰Рѕ С‚Рё С—РІ"
                : $"РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р» Р±СѓРІ: В«{CompactUserEcho(state.LastFoodMentionText)}В»";
            return $"РЎС‚РѕРї. {mention}. РћС‚Р¶Рµ, РЅРµ РІРёРіР°РґСѓС”РјРѕ В«РЅС–С‡РѕРіРѕ РЅРµ С—РІВ» С– РїРѕРІРµСЂС‚Р°С”РјРѕСЃСЊ РґРѕ СЂРµР°Р»СЊРЅРѕРіРѕ РїРёС‚Р°РЅРЅСЏ.";
        }

        private static string BuildSleepStateReplacement(string userText, KokoInternalState state)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            var hasTime18 = ContainsAny(lower, "18:00", "18.00", "18 00", "Рѕ 18", "РІ 18");
            if (hasTime18)
                return "РџСЂРёР№РЅСЏС‚Рѕ: С‚Рё Р·Р°СЃРЅСѓРІ Рѕ 18:00. Р¦Рµ РґРѕРІРіРёР№ СЃРѕРЅ, РЅРµ В«РіС–Р±РµСЂРЅР°С†С–СЏВ» С– РЅРµ РјРµРґРёС‡РЅРёР№ С†РёСЂРє. Р”Р°Р»С– РїСЂР°С†СЋС”РјРѕ Р· РїРѕС‚РѕС‡РЅРёРј СЃС‚Р°РЅРѕРј, Р° РЅРµ Р·С– СЃС‚Р°СЂРѕСЋ РґСЂР°РјРѕСЋ.";

            if (SaysSlept(lower))
                return "РџСЂРёР№РЅСЏС‚Рѕ: С‚Рё СЃРїР°РІ. РќРµ В«РЅРµ СЃРїР°РІВ», РЅРµ В«РіС–Р±РµСЂРЅР°С†С–СЏВ», РЅРµ РєРѕРјР° РґР»СЏ РґРµС€РµРІРѕС— СЂРµРїР»С–РєРё. РўРµРїРµСЂ РєР°Р¶Рё РїРѕС‚РѕС‡РЅРёР№ СЃС‚Р°РЅ С– С‰Рѕ СЂРѕР±РёРјРѕ РґР°Р»С–.";

            var mention = string.IsNullOrWhiteSpace(state.LastSleepMentionText)
                ? "РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р» РєР°Р¶Рµ, С‰Рѕ СЃРѕРЅ СѓР¶Рµ Р±СѓРІ"
                : $"РѕСЃС‚Р°РЅРЅС–Р№ СЃРёРіРЅР°Р» Р±СѓРІ: В«{CompactUserEcho(state.LastSleepMentionText)}В»";
            return $"РЎС‚РѕРї. {mention}. РЎС‚Р°СЂСѓ РґСЂР°РјСѓ РїСЂРѕ СЃРѕРЅ РїСЂРёР±СЂР°РЅРѕ; РІС–РґРїРѕРІС–РґР°СЋ РїРѕ С‚РµРїРµСЂС–С€РЅСЊРѕРјСѓ СЃС‚Р°РЅСѓ.";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
