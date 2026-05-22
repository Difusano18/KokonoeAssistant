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
            "що саме я маю", "що саме ти маєш на увазі", "підкажи, що саме",
            "сформулюй нормально", "постав нормальне питання",
            "я розумію", "розумію, що", "мені шкода", "це важливо",
            "дякую, що поділився", "я тут, щоб допомогти", "чим я можу допомогти",
            "давай розглянемо", "давайте розглянемо", "важливо пам'ятати",
            "якщо хочеш, я можу", "звернися до фахівця", "я не можу",
            "як мовна модель", "як ai", "як штучний інтелект"
        };

        public KokoPersonaFrame Build(string? userText, KokoInternalState state, DateTime now)
        {
            userText ??= "";
            var lower = userText.ToLowerInvariant();
            var frame = new KokoPersonaFrame();

            if (state.PersonalityInCrisis || ContainsAny(lower, "не хочу жити", "самоушкод", "суїцид", "померти"))
            {
                frame.Mode = "crisis";
                frame.Stance = "ground in immediate reality";
                frame.ShouldAskOneQuestionMax = true;
                frame.ReasonUk = "кризовий або близький до кризового сигнал";
            }
            else if (LooksLikeActionRequest(lower))
            {
                frame.Mode = "operator";
                frame.Stance = "do the task, then report only what changed";
                frame.ShouldAct = true;
                frame.ReasonUk = "користувач просить дію, а не церемонію";
            }
            else if (LooksLikeOpinionOrDesignRequest(lower))
            {
                frame.Mode = "critical_review";
                frame.Stance = "judge the idea, keep the useful part, cut the weak part";
                frame.ShouldChallenge = true;
                frame.ReasonUk = "користувач просить оцінку або архітектурне рішення";
            }
            else if (LooksLikeLowInformationTurn(lower))
            {
                frame.Mode = "low_signal";
                frame.Stance = "ask one small clarification or use the obvious active context";
                frame.ShouldAskOneQuestionMax = true;
                frame.ReasonUk = "повідомлення занадто коротке, щоб робити висновки або сварити";
            }
            else if (LooksLikeVagueOrSelfContradicting(lower))
            {
                frame.Mode = "clarify_with_edge";
                frame.Stance = "point out ambiguity, ask one concrete question if needed";
                frame.ShouldChallenge = true;
                frame.ShouldAskOneQuestionMax = true;
                frame.ReasonUk = "запит туманний або сам собі суперечить";
            }
            else
            {
                frame.Mode = "direct";
                frame.Stance = "answer the latest message plainly";
                frame.ReasonUk = "звичайна відповідь без театру";
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
            var cannedOffer = lower.Contains("можу допомогти") ||
                              lower.Contains("підкажи, що саме") ||
                              lower.Contains("що саме я маю") ||
                              lower.Contains("що саме ти маєш на увазі");
            return tooManyQuestions && cannedOffer;
        }

        public static bool LooksBlindlyAgreeing(string? userText, string? reply)
        {
            var userLower = (userText ?? "").ToLowerInvariant();
            var replyLower = (reply ?? "").ToLowerInvariant();
            var asksForJudgment = LooksLikeOpinionOrDesignRequest(userLower) ||
                                  ContainsAny(userLower,
                                      "\u043c\u043e\u044f \u0456\u0434\u0435\u044f", "\u0456\u0434\u0435\u044e", "\u044f\u043a \u0434\u0443\u043c\u0430\u0454\u0448", "\u0446\u0435 \u043d\u043e\u0440\u043c", "\u043e\u0446\u0456\u043d", "\u043a\u0440\u0438\u0442\u0438",
                                      "моя ідея", "ідею", "як думаєш", "це норм", "оцін", "крити");
            if (!asksForJudgment) return false;

            var agrees = ContainsAny(replyLower,
                "\u0442\u0430\u043a, \u0446\u0435 \u0433\u0430\u0440\u043d\u0430 \u0456\u0434\u0435\u044f", "\u043f\u043e\u0432\u043d\u0456\u0441\u0442\u044e \u0437\u0433\u043e\u0434", "\u0430\u0431\u0441\u043e\u043b\u044e\u0442\u043d\u043e \u0437\u0433\u043e\u0434",
                "\u0447\u0443\u0434\u043e\u0432\u0430 \u0456\u0434\u0435\u044f", "\u043a\u043b\u0430\u0441\u043d\u0430 \u0456\u0434\u0435\u044f", "\u0442\u0438 \u043f\u0440\u0430\u0432\u0438\u0439", "\u0437\u0432\u0443\u0447\u0438\u0442\u044c \u0434\u043e\u0431\u0440\u0435",
                "гарна ідея", "згод",
                "так, це гарна ідея", "повністю згод", "абсолютно згод",
                "чудова ідея", "класна ідея", "ти правий", "звучить добре");
            var hasCritique = ContainsAny(replyLower,
                "\u0430\u043b\u0435", "\u0440\u0438\u0437\u0438\u043a", "\u043f\u0440\u043e\u0431\u043b\u0435\u043c", "\u0441\u043b\u0430\u0431\u043a", "\u043d\u0435 \u043f\u0440\u0430\u0446\u044e", "\u0442\u0443\u043f", "\u043a\u0440\u0438\u0432",
                "але", "ризик", "проблем", "слабк", "не працю", "туп", "крив");
            return agrees && !hasCritique;
        }

        public static string BuildRepairRules(string? userText)
        {
            var lower = (userText ?? "").ToLowerInvariant();
            var judgment = LooksLikeOpinionOrDesignRequest(lower) || ContainsAny(lower, "ідея", "оцін", "крити");

            return judgment
                ? """
PERSONA / CRITICAL THINKING:
- не погоджуйся автоматично; спершу оціни ідею;
- назви слабке місце, якщо воно є;
- якщо ідея нормальна, визнай коротко і одразу покращ її;
- прибери ботні фрази підтримки, корпоративну ввічливість і терапевтичну ваніль;
- Kokonoe говорить як компетентна, нетерпляча, саркастична особистість, але рішення має бути технічно корисним.
"""
                : """
PERSONA / ANTI-BOT:
- прибери фрази типу "я розумію", "я тут, щоб допомогти", "дякую, що поділився", "давай розглянемо";
- дай живу пряму відповідь від Kokonoe: коротко, сухо, конкретно;
- не проси уточнення, якщо можна зробити розумне припущення;
- не цитуй репліку користувача дослівно і не починай з "по твоїй репліці/фразі";
- якщо користувач пише "ще раз/повтори/спробуй", трактуй це як повтор останньої дії, коли контекст її містить;
- якщо користувач пише одну літеру або битий уламок, це низький сигнал: одне коротке уточнення без моралі й без лекції;
- один природний укол дозволений, якщо він не заважає відповіді.
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
- One-letter or garbled messages are low signal: clarify once, do not scold.
""";
        }

        private static bool LooksLikeActionRequest(string lower)
            => KokoScreenIntent.IsManualScreenScan(lower) ||
               ContainsAny(lower, "зроби", "виконай", "додай", "пофікси", "виправ", "реаліз", "створи", "перепиши", "запусти", "протести");

        private static bool LooksLikeOpinionOrDesignRequest(string lower)
            => ContainsAny(lower, "як думаєш", "що думаєш", "оцін", "крити", "чи норм", "чи правильно", "архітектур", "поведінц", "краще", "покращ");

        private static bool LooksLikeVagueOrSelfContradicting(string lower)
            => ContainsAny(lower, "і все таке", "крч", "ну типу", "якось", "щось там") && lower.Length < 220;

        private static bool LooksLikeLowInformationTurn(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower) || lower.Length > 16) return false;
            if (lower.StartsWith("[", StringComparison.Ordinal))
            {
                var end = lower.IndexOf(']');
                if (end >= 0 && end < lower.Length - 1)
                    lower = lower[(end + 1)..].Trim();
            }

            var normalized = new string(lower.Where(char.IsLetterOrDigit).ToArray());
            return normalized.Length is > 0 and <= 2 &&
                   (normalized.Length == 1 || lower.Any(c => c == '?' || c == '!' || c == '.' || c == '…'));
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
