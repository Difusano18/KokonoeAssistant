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
                decision.ReasonUk = "шум або коротка службова репліка";
            }
            else if (LooksTemporary(lower))
            {
                decision.Action = "daily_log";
                decision.ReasonUk = "тимчасовий стан; не псувати довгостроковий профіль";
                decision.Candidates.Add(BuildCandidate(userText, "temporary_state", "Daily/Logs", 0.72, "стан актуальний зараз, не стабільний факт"));
            }
            else if (LooksTask(lower))
            {
                decision.Action = "task_queue";
                decision.ReasonUk = "схоже на задачу або намір";
                decision.Candidates.Add(BuildCandidate(userText, "task", "Kokonoe/Tasks Queue.md", 0.70, "корисно тримати як активний намір"));
            }
            else if (LooksExplicitMemory(lower))
            {
                decision.Action = "store_stable";
                decision.ReasonUk = "користувач явно просить запам'ятати";
                decision.Candidates.Add(BuildCandidate(CleanClaim(userText), "stable_fact", "Kokonoe/Memory/Facts.md", 0.92, "явне прохання зберегти"));
            }
            else if (TryExtractPreference(userText, out var preference))
            {
                decision.Action = "store_stable";
                decision.ReasonUk = "стабільне вподобання або ставлення";
                decision.Candidates.Add(BuildCandidate(preference, "preference", "Kokonoe/Preferences.md", 0.82, "формула вподобання/антипатії"));
            }
            else if (TryExtractGoal(userText, out var goal))
            {
                decision.Action = "store_stable";
                decision.ReasonUk = "довший намір або ціль";
                decision.Candidates.Add(BuildCandidate(goal, "goal", "Kokonoe/Tasks.md", 0.74, "користувач формулює бажаний напрям"));
            }
            else if (LooksUncertain(lower))
            {
                decision.Action = "review";
                decision.ReasonUk = "може бути важливим, але звучить невпевнено";
                decision.Candidates.Add(BuildCandidate(userText, "uncertain", "Kokonoe/Memory/Review.md", 0.52, "потрібне підтвердження"));
            }
            else
            {
                decision.Action = "ignore";
                decision.ReasonUk = "немає достатньо стабільної інформації для пам'яті";
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
               lower is "ок" or "окей" or "ага" or "ясно" or "привіт" or "дякую" or "спс" or "лол";

        private static bool LooksTemporary(string lower)
            => ContainsAny(lower, "зараз", "сьогодні", "втомив", "сонний", "хочу спати", "голод", "болить", "настрій", "стрес", "мені погано");

        private static bool LooksTask(string lower)
            => ContainsAny(lower, "треба зробити", "не забудь", "нагадай", "планую", "завтра треба", "дедлайн", "таск", "задача");

        private static bool LooksExplicitMemory(string lower)
            => ContainsAny(lower, "запам'ятай", "запамятай", "запиши що", "важливо: ", "це важливо", "мій принцип", "моє правило");

        private static bool LooksUncertain(string lower)
            => ContainsAny(lower, "можливо", "напевно", "здається", "не впевнений", "не уверен", "скоріш за все");

        private static bool TryExtractPreference(string text, out string preference)
        {
            var patterns = new[]
            {
                "я люблю ", "я обожнюю ", "я ненавиджу ", "мені подобається ",
                "мені не подобається ", "я віддаю перевагу ", "i love ", "i hate ", "i prefer "
            };
            preference = ExtractAfterPattern(text, patterns);
            return !string.IsNullOrWhiteSpace(preference);
        }

        private static bool TryExtractGoal(string text, out string goal)
        {
            var patterns = new[] { "я хочу ", "я планую ", "моя ціль ", "моя мета ", "i want ", "my goal " };
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
            foreach (var prefix in new[] { "запам'ятай", "запамятай", "запиши що", "це важливо", "важливо:" })
            {
                if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    cleaned = cleaned[prefix.Length..].Trim(' ', ':', '-', '—');
            }
            return cleaned;
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
