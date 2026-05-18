using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoMemoryCandidate
    {
        public string Text { get; set; } = "";
        public string Kind { get; set; } = "noise";
        public string Target { get; set; } = "none";
        public double Confidence { get; set; }
        public string ReasonUk { get; set; } = "";
    }

    public sealed class KokoMemoryWriteDecision
    {
        public string Action { get; set; } = "ignore";
        public string ReasonUk { get; set; } = "";
        public string Risk { get; set; } = "low";
        public List<KokoMemoryCandidate> Candidates { get; set; } = new();
        public string TraceLine { get; set; } = "";
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoMemoryWritePolicyEngine
    {
        public KokoMemoryWriteDecision Evaluate(string? userText, DateTime now)
        {
            userText ??= "";
            var lower = userText.ToLowerInvariant().Trim();
            var decision = new KokoMemoryWriteDecision();

            if (string.IsNullOrWhiteSpace(lower) || IsNoise(lower))
            {
                decision.Action = "ignore";
                decision.ReasonUk = "С€СѓРј Р°Р±Рѕ РєРѕСЂРѕС‚РєР° СЃР»СѓР¶Р±РѕРІР° СЂРµРїР»С–РєР°";
            }
            else if (LooksTemporary(lower))
            {
                decision.Action = "daily_log";
                decision.ReasonUk = "С‚РёРјС‡Р°СЃРѕРІРёР№ СЃС‚Р°РЅ; РЅРµ РїСЃСѓРІР°С‚Рё РґРѕРІРіРѕСЃС‚СЂРѕРєРѕРІРёР№ РїСЂРѕС„С–Р»СЊ";
                decision.Candidates.Add(BuildCandidate(userText, "temporary_state", "Daily/Logs", 0.72, "СЃС‚Р°РЅ Р°РєС‚СѓР°Р»СЊРЅРёР№ Р·Р°СЂР°Р·, РЅРµ СЃС‚Р°Р±С–Р»СЊРЅРёР№ С„Р°РєС‚"));
            }
            else if (LooksTask(lower))
            {
                decision.Action = "task_queue";
                decision.ReasonUk = "СЃС…РѕР¶Рµ РЅР° Р·Р°РґР°С‡Сѓ Р°Р±Рѕ РЅР°РјС–СЂ";
                decision.Candidates.Add(BuildCandidate(userText, "task", "Kokonoe/Tasks Queue.md", 0.70, "РєРѕСЂРёСЃРЅРѕ С‚СЂРёРјР°С‚Рё СЏРє Р°РєС‚РёРІРЅРёР№ РЅР°РјС–СЂ"));
            }
            else if (LooksExplicitMemory(lower))
            {
                decision.Action = "store_stable";
                decision.ReasonUk = "РєРѕСЂРёСЃС‚СѓРІР°С‡ СЏРІРЅРѕ РїСЂРѕСЃРёС‚СЊ Р·Р°РїР°Рј'СЏС‚Р°С‚Рё";
                decision.Candidates.Add(BuildCandidate(CleanClaim(userText), "stable_fact", "Kokonoe/Memory/Facts.md", 0.92, "СЏРІРЅРµ РїСЂРѕС…Р°РЅРЅСЏ Р·Р±РµСЂРµРіС‚Рё"));
            }
            else if (TryExtractPreference(userText, out var preference))
            {
                decision.Action = "store_stable";
                decision.ReasonUk = "СЃС‚Р°Р±С–Р»СЊРЅРµ РІРїРѕРґРѕР±Р°РЅРЅСЏ Р°Р±Рѕ СЃС‚Р°РІР»РµРЅРЅСЏ";
                decision.Candidates.Add(BuildCandidate(preference, "preference", "Kokonoe/Preferences.md", 0.82, "С„РѕСЂРјСѓР»Р° РІРїРѕРґРѕР±Р°РЅРЅСЏ/Р°РЅС‚РёРїР°С‚С–С—"));
            }
            else if (TryExtractGoal(userText, out var goal))
            {
                decision.Action = "store_stable";
                decision.ReasonUk = "РґРѕРІС€РёР№ РЅР°РјС–СЂ Р°Р±Рѕ С†С–Р»СЊ";
                decision.Candidates.Add(BuildCandidate(goal, "goal", "Kokonoe/Tasks.md", 0.74, "РєРѕСЂРёСЃС‚СѓРІР°С‡ С„РѕСЂРјСѓР»СЋС” Р±Р°Р¶Р°РЅРёР№ РЅР°РїСЂСЏРј"));
            }
            else if (LooksUncertain(lower))
            {
                decision.Action = "review";
                decision.ReasonUk = "РјРѕР¶Рµ Р±СѓС‚Рё РІР°Р¶Р»РёРІРёРј, Р°Р»Рµ Р·РІСѓС‡РёС‚СЊ РЅРµРІРїРµРІРЅРµРЅРѕ";
                decision.Candidates.Add(BuildCandidate(userText, "uncertain", "Kokonoe/Memory/Review.md", 0.52, "РїРѕС‚СЂС–Р±РЅРµ РїС–РґС‚РІРµСЂРґР¶РµРЅРЅСЏ"));
            }
            else
            {
                decision.Action = "ignore";
                decision.ReasonUk = "РЅРµРјР°С” РґРѕСЃС‚Р°С‚РЅСЊРѕ СЃС‚Р°Р±С–Р»СЊРЅРѕС— С–РЅС„РѕСЂРјР°С†С–С— РґР»СЏ РїР°Рј'СЏС‚С–";
            }

            decision.Risk = decision.Action is "store_stable" ? "medium" :
                decision.Action is "review" or "task_queue" ? "low" : "none";
            decision.TraceLine = $"[{now:HH:mm}] memory={decision.Action}; candidates={decision.Candidates.Count}; reason={decision.ReasonUk}";
            decision.PromptBlock = BuildPromptBlock(decision);
            return decision;
        }

        public string BuildDebugBlock(KokoInternalState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MEMORY WRITE POLICY ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(state.LastMemoryPolicyDecision)
                ? "last_memory_policy=none"
                : $"last_memory_policy={state.LastMemoryPolicyDecision}");
            if (state.MemoryPolicyLog.Count > 0)
            {
                sb.AppendLine("recent_memory_policy:");
                foreach (var item in state.MemoryPolicyLog.TakeLast(6))
                    sb.AppendLine($"- {item}");
            }
            sb.AppendLine("rule: stable memory is filtered; temporary state is not a permanent belief.");
            return sb.ToString();
        }

        private static string BuildPromptBlock(KokoMemoryWriteDecision decision)
        {
            var sb = new StringBuilder();
            sb.AppendLine("MEMORY WRITE POLICY");
            sb.AppendLine($"action: {decision.Action}");
            sb.AppendLine($"risk: {decision.Risk}");
            sb.AppendLine($"reason: {decision.ReasonUk}");
            if (decision.Candidates.Count > 0)
            {
                sb.AppendLine("candidates:");
                foreach (var c in decision.Candidates)
                    sb.AppendLine($"- kind={c.Kind}; target={c.Target}; confidence={c.Confidence:F2}; text={c.Text}");
            }
            sb.AppendLine("rules:");
            sb.AppendLine("- stable facts/preferences/goals may become beliefs;");
            sb.AppendLine("- temporary state belongs in Daily/Logs, not profile;");
            sb.AppendLine("- uncertain claims go to review; do not present them as fact.");
            return sb.ToString();
        }

        private static KokoMemoryCandidate BuildCandidate(string text, string kind, string target, double confidence, string reason)
            => new()
            {
                Text = text.Trim(),
                Kind = kind,
                Target = target,
                Confidence = confidence,
                ReasonUk = reason
            };

        private static bool IsNoise(string lower)
            => lower.Length < 4 ||
               lower is "РѕРє" or "РѕРєРµР№" or "Р°РіР°" or "СЏСЃРЅРѕ" or "РїСЂРёРІС–С‚" or "РґСЏРєСѓСЋ" or "СЃРїСЃ" or "Р»РѕР»";

        private static bool LooksTemporary(string lower)
            => ContainsAny(lower, "Р·Р°СЂР°Р·", "СЃСЊРѕРіРѕРґРЅС–", "РІС‚РѕРјРёРІ", "СЃРѕРЅРЅРёР№", "С…РѕС‡Сѓ СЃРїР°С‚Рё", "РіРѕР»РѕРґ", "Р±РѕР»РёС‚СЊ", "РЅР°СЃС‚СЂС–Р№", "СЃС‚СЂРµСЃ", "РјРµРЅС– РїРѕРіР°РЅРѕ");

        private static bool LooksTask(string lower)
            => ContainsAny(lower, "С‚СЂРµР±Р° Р·СЂРѕР±РёС‚Рё", "РЅРµ Р·Р°Р±СѓРґСЊ", "РЅР°РіР°РґР°Р№", "РїР»Р°РЅСѓСЋ", "Р·Р°РІС‚СЂР° С‚СЂРµР±Р°", "РґРµРґР»Р°Р№РЅ", "С‚Р°СЃРє", "Р·Р°РґР°С‡Р°");

        private static bool LooksExplicitMemory(string lower)
            => ContainsAny(lower, "Р·Р°РїР°Рј'СЏС‚Р°Р№", "Р·Р°РїР°РјСЏС‚Р°Р№", "Р·Р°РїРёС€Рё С‰Рѕ", "РІР°Р¶Р»РёРІРѕ: ", "С†Рµ РІР°Р¶Р»РёРІРѕ", "РјС–Р№ РїСЂРёРЅС†РёРї", "РјРѕС” РїСЂР°РІРёР»Рѕ");

        private static bool LooksUncertain(string lower)
            => ContainsAny(lower, "РјРѕР¶Р»РёРІРѕ", "РЅР°РїРµРІРЅРѕ", "Р·РґР°С”С‚СЊСЃСЏ", "РЅРµ РІРїРµРІРЅРµРЅРёР№", "РЅРµ СѓРІРµСЂРµРЅ", "СЃРєРѕСЂС–С€ Р·Р° РІСЃРµ");

        private static bool TryExtractPreference(string text, out string preference)
        {
            var lower = text.ToLowerInvariant();
            if (ContainsAny(lower,
                "\u043b\u044e\u0431\u043b\u044e", "\u043e\u0431\u043e\u0436\u043d\u044e\u044e", "\u043d\u0435\u043d\u0430\u0432\u0438\u0434\u0436\u0443", "\u043f\u043e\u0434\u043e\u0431\u0430\u0454\u0442\u044c\u0441\u044f",
                "СЋР±", "РѕР±РѕР¶", "РЅРµРЅР°РІ", "РїРѕРґРѕР±"))
            {
                preference = text.Trim();
                return preference.Length is >= 3 and <= 220;
            }

            var patterns = new[]
            {
                "\u044f \u043b\u044e\u0431\u043b\u044e ", "\u044f \u043e\u0431\u043e\u0436\u043d\u044e\u044e ", "\u044f \u043d\u0435\u043d\u0430\u0432\u0438\u0434\u0436\u0443 ", "\u043c\u0435\u043d\u0456 \u043f\u043e\u0434\u043e\u0431\u0430\u0454\u0442\u044c\u0441\u044f ",
                "\u043c\u0435\u043d\u0456 \u043d\u0435 \u043f\u043e\u0434\u043e\u0431\u0430\u0454\u0442\u044c\u0441\u044f ", "\u044f \u0432\u0456\u0434\u0434\u0430\u044e \u043f\u0435\u0440\u0435\u0432\u0430\u0433\u0443 ",
                "СЏ Р»СЋР±Р»СЋ ", "СЏ РѕР±РѕР¶РЅСЋСЋ ", "СЏ РЅРµРЅР°РІРёРґР¶Сѓ ", "РјРµРЅС– РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ ",
                "РјРµРЅС– РЅРµ РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ ", "СЏ РІС–РґРґР°СЋ РїРµСЂРµРІР°РіСѓ ", "i love ", "i hate ", "i prefer "
            };
            preference = ExtractAfterPattern(text, patterns);
            return !string.IsNullOrWhiteSpace(preference);
        }

        private static bool TryExtractGoal(string text, out string goal)
        {
            var patterns = new[] { "СЏ С…РѕС‡Сѓ ", "СЏ РїР»Р°РЅСѓСЋ ", "РјРѕСЏ С†С–Р»СЊ ", "РјРѕСЏ РјРµС‚Р° ", "i want ", "my goal " };
            goal = ExtractAfterPattern(text, patterns);
            if (goal.Length < 8) goal = "";
            return !string.IsNullOrWhiteSpace(goal);
        }

        private static string ExtractAfterPattern(string text, IEnumerable<string> patterns)
        {
            var lower = text.ToLowerInvariant();
            foreach (var pattern in patterns)
            {
                var idx = lower.IndexOf(pattern, StringComparison.Ordinal);
                if (idx < 0) continue;
                var raw = text[(idx + pattern.Length)..].Trim();
                raw = raw.Split('.', '!', '?', '\n').FirstOrDefault()?.Trim() ?? "";
                if (raw.Length is >= 3 and <= 220)
                    return $"{pattern.Trim()} {raw}".Trim();
            }
            return "";
        }

        private static string CleanClaim(string text)
        {
            var cleaned = text.Trim();
            foreach (var prefix in new[] { "Р·Р°РїР°Рј'СЏС‚Р°Р№", "Р·Р°РїР°РјСЏС‚Р°Р№", "Р·Р°РїРёС€Рё С‰Рѕ", "С†Рµ РІР°Р¶Р»РёРІРѕ", "РІР°Р¶Р»РёРІРѕ:" })
            {
                if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    cleaned = cleaned[prefix.Length..].Trim(' ', ':', '-', '\u2014');
            }
            return cleaned;
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
