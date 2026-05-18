using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoPersonaFrame
    {
        public string Mode { get; set; } = "direct";
        public string Stance { get; set; } = "answer directly";
        public bool ShouldChallenge { get; set; }
        public bool ShouldAct { get; set; }
        public bool ShouldAskOneQuestionMax { get; set; }
        public string ReasonUk { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoPersonaEngine
    {
        private static readonly string[] BotPhrases =
        {
            "я розумію", "розумію, що", "мені шкода", "це важливо",
            "дякую, що поділився", "я тут, щоб допомогти", "чим я можу допомогти",
            "давай розглянемо", "давайте розглянемо", "важливо пам'ятати",
            "якщо хочеш, я можу", "звернися до фахівця", "я не можу",
            "СЏ СЂРѕР·СѓРјС–СЋ", "СЂРѕР·СѓРјС–СЋ, С‰Рѕ", "РјРµРЅС– С€РєРѕРґР°", "С†Рµ РІР°Р¶Р»РёРІРѕ",
            "РґСЏРєСѓСЋ, С‰Рѕ РїРѕРґС–Р»РёРІСЃСЏ", "СЏ С‚СѓС‚, С‰РѕР± РґРѕРїРѕРјРѕРіС‚Рё", "С‡РёРј СЏ РјРѕР¶Сѓ РґРѕРїРѕРјРѕРіС‚Рё",
            "РґР°РІР°Р№ СЂРѕР·РіР»СЏРЅРµРјРѕ", "РґР°РІР°Р№С‚Рµ СЂРѕР·РіР»СЏРЅРµРјРѕ", "РІР°Р¶Р»РёРІРѕ РїР°Рј'СЏС‚Р°С‚Рё",
            "СЏРєС‰Рѕ С…РѕС‡РµС€, СЏ РјРѕР¶Сѓ", "Р·РІРµСЂРЅРёСЃСЏ РґРѕ С„Р°С…С–РІС†СЏ", "СЏ РЅРµ РјРѕР¶Сѓ",
            "СЏРє РјРѕРІРЅР° РјРѕРґРµР»СЊ", "СЏРє ai", "СЏРє С€С‚СѓС‡РЅРёР№ С–РЅС‚РµР»РµРєС‚"
        };

        public KokoPersonaFrame Build(string? userText, KokoInternalState state, DateTime now)
        {
            userText ??= "";
            var lower = userText.ToLowerInvariant();
            var frame = new KokoPersonaFrame();

            if (state.PersonalityInCrisis || ContainsAny(lower, "РЅРµ С…РѕС‡Сѓ Р¶РёС‚Рё", "СЃР°РјРѕСѓС€РєРѕРґ", "СЃСѓС—С†РёРґ", "РїРѕРјРµСЂС‚Рё"))
            {
                frame.Mode = "crisis";
                frame.Stance = "ground in immediate reality";
                frame.ShouldAskOneQuestionMax = true;
                frame.ReasonUk = "РєСЂРёР·РѕРІРёР№ Р°Р±Рѕ Р±Р»РёР·СЊРєРёР№ РґРѕ РєСЂРёР·РѕРІРѕРіРѕ СЃРёРіРЅР°Р»";
            }
            else if (LooksLikeActionRequest(lower))
            {
                frame.Mode = "operator";
                frame.Stance = "do the task, then report only what changed";
                frame.ShouldAct = true;
                frame.ReasonUk = "РєРѕСЂРёСЃС‚СѓРІР°С‡ РїСЂРѕСЃРёС‚СЊ РґС–СЋ, Р° РЅРµ С†РµСЂРµРјРѕРЅС–СЋ";
            }
            else if (LooksLikeOpinionOrDesignRequest(lower))
            {
                frame.Mode = "critical_review";
                frame.Stance = "judge the idea, keep the useful part, cut the weak part";
                frame.ShouldChallenge = true;
                frame.ReasonUk = "РєРѕСЂРёСЃС‚СѓРІР°С‡ РїСЂРѕСЃРёС‚СЊ РѕС†С–РЅРєСѓ Р°Р±Рѕ Р°СЂС…С–С‚РµРєС‚СѓСЂРЅРµ СЂС–С€РµРЅРЅСЏ";
            }
            else if (LooksLikeVagueOrSelfContradicting(lower))
            {
                frame.Mode = "clarify_with_edge";
                frame.Stance = "point out ambiguity, ask one concrete question if needed";
                frame.ShouldChallenge = true;
                frame.ShouldAskOneQuestionMax = true;
                frame.ReasonUk = "Р·Р°РїРёС‚ С‚СѓРјР°РЅРЅРёР№ Р°Р±Рѕ СЃР°Рј СЃРѕР±С– СЃСѓРїРµСЂРµС‡РёС‚СЊ";
            }
            else
            {
                frame.Mode = "direct";
                frame.Stance = "answer the latest message plainly";
                frame.ReasonUk = "Р·РІРёС‡Р°Р№РЅР° РІС–РґРїРѕРІС–РґСЊ Р±РµР· С‚РµР°С‚СЂСѓ";
            }

            frame.PromptBlock = BuildPromptBlock(frame);
            frame.TraceLine = $"[{now:HH:mm}] persona={frame.Mode}; stance={frame.Stance}; reason={frame.ReasonUk}";
            return frame;
        }

        public static bool LooksBotLike(string? reply)
        {
            var lower = (reply ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var hits = BotPhrases.Count(p => lower.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (hits >= 1) return true;

            var tooManyQuestions = lower.Count(c => c == '?') >= 3;
            var cannedOffer = lower.Contains("РјРѕР¶Сѓ РґРѕРїРѕРјРѕРіС‚Рё") || lower.Contains("РїС–РґРєР°Р¶Рё, С‰Рѕ СЃР°РјРµ");
            return tooManyQuestions && cannedOffer;
        }

        public static bool LooksBlindlyAgreeing(string? userText, string? reply)
        {
            var userLower = (userText ?? "").ToLowerInvariant();
            var replyLower = (reply ?? "").ToLowerInvariant();
            var asksForJudgment = LooksLikeOpinionOrDesignRequest(userLower) ||
                                  ContainsAny(userLower,
                                      "\u043c\u043e\u044f \u0456\u0434\u0435\u044f", "\u0456\u0434\u0435\u044e", "\u044f\u043a \u0434\u0443\u043c\u0430\u0454\u0448", "\u0446\u0435 \u043d\u043e\u0440\u043c", "\u043e\u0446\u0456\u043d", "\u043a\u0440\u0438\u0442\u0438",
                                      "РјРѕСЏ С–РґРµСЏ", "С–РґРµСЋ", "СЏРє РґСѓРјР°С”С€", "С†Рµ РЅРѕСЂРј", "РѕС†С–РЅ", "РєСЂРёС‚Рё");
            if (!asksForJudgment) return false;

            var agrees = ContainsAny(replyLower,
                "\u0442\u0430\u043a, \u0446\u0435 \u0433\u0430\u0440\u043d\u0430 \u0456\u0434\u0435\u044f", "\u043f\u043e\u0432\u043d\u0456\u0441\u0442\u044e \u0437\u0433\u043e\u0434", "\u0430\u0431\u0441\u043e\u043b\u044e\u0442\u043d\u043e \u0437\u0433\u043e\u0434",
                "\u0447\u0443\u0434\u043e\u0432\u0430 \u0456\u0434\u0435\u044f", "\u043a\u043b\u0430\u0441\u043d\u0430 \u0456\u0434\u0435\u044f", "\u0442\u0438 \u043f\u0440\u0430\u0432\u0438\u0439", "\u0437\u0432\u0443\u0447\u0438\u0442\u044c \u0434\u043e\u0431\u0440\u0435",
                "РіР°СЂРЅР° С–РґРµСЏ", "Р·РіРѕРґ",
                "С‚Р°Рє, С†Рµ РіР°СЂРЅР° С–РґРµСЏ", "РїРѕРІРЅС–СЃС‚СЋ Р·РіРѕРґ", "Р°Р±СЃРѕР»СЋС‚РЅРѕ Р·РіРѕРґ",
                "С‡СѓРґРѕРІР° С–РґРµСЏ", "РєР»Р°СЃРЅР° С–РґРµСЏ", "С‚Рё РїСЂР°РІРёР№", "Р·РІСѓС‡РёС‚СЊ РґРѕР±СЂРµ");
            var hasCritique = ContainsAny(replyLower,
                "\u0430\u043b\u0435", "\u0440\u0438\u0437\u0438\u043a", "\u043f\u0440\u043e\u0431\u043b\u0435\u043c", "\u0441\u043b\u0430\u0431\u043a", "\u043d\u0435 \u043f\u0440\u0430\u0446\u044e", "\u0442\u0443\u043f", "\u043a\u0440\u0438\u0432",
                "Р°Р»Рµ", "СЂРёР·РёРє", "РїСЂРѕР±Р»РµРј", "СЃР»Р°Р±Рє", "РЅРµ РїСЂР°С†СЋ", "С‚СѓРї", "РєСЂРёРІ");
            return agrees && !hasCritique;
        }

        public static string BuildRepairRules(string? userText)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            var judgment = LooksLikeOpinionOrDesignRequest(lower) || ContainsAny(lower, "С–РґРµСЏ", "РѕС†С–РЅ", "РєСЂРёС‚Рё");

            return judgment
                ? """
PERSONA / CRITICAL THINKING:
- РЅРµ РїРѕРіРѕРґР¶СѓР№СЃСЏ Р°РІС‚РѕРјР°С‚РёС‡РЅРѕ; СЃРїРµСЂС€Сѓ РѕС†С–РЅРё С–РґРµСЋ;
- РЅР°Р·РІРё СЃР»Р°Р±РєРµ РјС–СЃС†Рµ, СЏРєС‰Рѕ РІРѕРЅРѕ С”;
- СЏРєС‰Рѕ С–РґРµСЏ РЅРѕСЂРјР°Р»СЊРЅР°, РІРёР·РЅР°Р№ РєРѕСЂРѕС‚РєРѕ С– РѕРґСЂР°Р·Сѓ РїРѕРєСЂР°С‰ С—С—;
- РїСЂРёР±РµСЂРё Р±РѕС‚РЅС– С„СЂР°Р·Рё РїС–РґС‚СЂРёРјРєРё, РєРѕСЂРїРѕСЂР°С‚РёРІРЅСѓ РІРІС–С‡Р»РёРІС–СЃС‚СЊ С– С‚РµСЂР°РїРµРІС‚РёС‡РЅСѓ РІР°РЅС–Р»СЊ;
- Kokonoe РіРѕРІРѕСЂРёС‚СЊ СЏРє РєРѕРјРїРµС‚РµРЅС‚РЅР°, РЅРµС‚РµСЂРїР»СЏС‡Р°, СЃР°СЂРєР°СЃС‚РёС‡РЅР° РѕСЃРѕР±РёСЃС‚С–СЃС‚СЊ, Р°Р»Рµ СЂС–С€РµРЅРЅСЏ РјР°С” Р±СѓС‚Рё С‚РµС…РЅС–С‡РЅРѕ РєРѕСЂРёСЃРЅРёРј.
"""
                : """
PERSONA / ANTI-BOT:
- РїСЂРёР±РµСЂРё С„СЂР°Р·Рё С‚РёРїСѓ "СЏ СЂРѕР·СѓРјС–СЋ", "СЏ С‚СѓС‚, С‰РѕР± РґРѕРїРѕРјРѕРіС‚Рё", "РґСЏРєСѓСЋ, С‰Рѕ РїРѕРґС–Р»РёРІСЃСЏ", "РґР°РІР°Р№ СЂРѕР·РіР»СЏРЅРµРјРѕ";
- РґР°Р№ Р¶РёРІСѓ РїСЂСЏРјСѓ РІС–РґРїРѕРІС–РґСЊ РІС–Рґ Kokonoe: РєРѕСЂРѕС‚РєРѕ, СЃСѓС…Рѕ, РєРѕРЅРєСЂРµС‚РЅРѕ;
- РЅРµ РїСЂРѕСЃРё СѓС‚РѕС‡РЅРµРЅРЅСЏ, СЏРєС‰Рѕ РјРѕР¶РЅР° Р·СЂРѕР±РёС‚Рё СЂРѕР·СѓРјРЅРµ РїСЂРёРїСѓС‰РµРЅРЅСЏ;
- РѕРґРёРЅ РїСЂРёСЂРѕРґРЅРёР№ СѓРєРѕР» РґРѕР·РІРѕР»РµРЅРёР№, СЏРєС‰Рѕ РІС–РЅ РЅРµ Р·Р°РІР°Р¶Р°С” РІС–РґРїРѕРІС–РґС–.
""";
        }

        private static string BuildPromptBlock(KokoPersonaFrame frame)
        {
            return $"""
PERSONA DECISION
mode: {frame.Mode}
stance: {frame.Stance}
should_challenge: {(frame.ShouldChallenge ? "yes" : "no")}
should_act: {(frame.ShouldAct ? "yes" : "no")}
question_limit: {(frame.ShouldAskOneQuestionMax ? "one max" : "normal")}
reason: {frame.ReasonUk}
Rules:
- Personality is not decoration; use judgment before tone.
- If the user's idea is weak, say so and improve it.
- Never use service-bot sympathy phrases or generic support scripts.
- Prefer a concrete action, correction, or decision over soft reassurance.
""";
        }

        private static bool LooksLikeActionRequest(string lower)
            => ContainsAny(lower, "Р·СЂРѕР±Рё", "РІРёРєРѕРЅР°Р№", "РґРѕРґР°Р№", "РїРѕС„С–РєСЃРё", "РІРёРїСЂР°РІ", "СЂРµР°Р»С–Р·", "СЃС‚РІРѕСЂРё", "РїРµСЂРµРїРёС€Рё", "Р·Р°РїСѓСЃС‚Рё", "РїСЂРѕС‚РµСЃС‚Рё");

        private static bool LooksLikeOpinionOrDesignRequest(string lower)
            => ContainsAny(lower, "СЏРє РґСѓРјР°С”С€", "С‰Рѕ РґСѓРјР°С”С€", "РѕС†С–РЅ", "РєСЂРёС‚Рё", "С‡Рё РЅРѕСЂРј", "С‡Рё РїСЂР°РІРёР»СЊРЅРѕ", "Р°СЂС…С–С‚РµРєС‚СѓСЂ", "РїРѕРІРµРґС–РЅС†", "РєСЂР°С‰Рµ", "РїРѕРєСЂР°С‰");

        private static bool LooksLikeVagueOrSelfContradicting(string lower)
            => ContainsAny(lower, "С– РІСЃРµ С‚Р°РєРµ", "РєСЂС‡", "РЅСѓ С‚РёРїСѓ", "СЏРєРѕСЃСЊ", "С‰РѕСЃСЊ С‚Р°Рј") && lower.Length < 220;

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
