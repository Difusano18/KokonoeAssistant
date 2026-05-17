using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoResponsePlanFrame
    {
        public string Intent { get; set; } = "chat";
        public string Capability { get; set; } = "conversation";
        public string Stance { get; set; } = "direct";
        public string MemoryPolicy { get; set; } = "do_not_store";
        public bool RequiresAction { get; set; }
        public bool RequiresVaultRead { get; set; }
        public bool RequiresToolUse { get; set; }
        public bool RequiresCritique { get; set; }
        public bool ShouldPushBack { get; set; }
        public string Risk { get; set; } = "low";
        public string ReasonUk { get; set; } = "";
        public List<string> Steps { get; set; } = new();
        public List<string> Constraints { get; set; } = new();
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoResponsePlannerEngine
    {
        public KokoResponsePlanFrame Build(
            string? userText,
            KokoInternalState state,
            KokoCognitionEngine cognition,
            DateTime now)
        {
            userText ??= "";
            var lower = userText.ToLowerInvariant();
            var frame = new KokoResponsePlanFrame();

            frame.Intent = ClassifyIntent(lower);
            frame.Capability = ClassifyCapability(lower);
            frame.RequiresAction = IsActionIntent(frame.Intent);
            frame.RequiresCritique = frame.Intent is "evaluate" or "design" or "architecture";
            frame.ShouldPushBack = frame.RequiresCritique || LooksUnsafeOrContradictory(lower) || LooksLikeLowAgencyRequest(lower);
            frame.RequiresVaultRead = NeedsVaultRead(lower, frame.Intent);
            frame.RequiresToolUse = NeedsToolUse(lower, frame.Capability, frame.RequiresVaultRead);
            frame.MemoryPolicy = BuildMemoryPolicy(lower, frame.Intent);
            frame.Risk = BuildRisk(lower, frame);
            frame.Stance = BuildStance(frame, state);
            frame.ReasonUk = BuildReason(frame);
            frame.Steps = BuildSteps(frame);
            frame.Constraints = BuildConstraints(frame, state);
            frame.PromptBlock = BuildPromptBlock(frame, cognition);
            frame.TraceLine = $"[{now:HH:mm}] plan={frame.Intent}/{frame.Capability}; stance={frame.Stance}; memory={frame.MemoryPolicy}; risk={frame.Risk}";

            return frame;
        }

        public string BuildDebugBlock(KokoInternalState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RESPONSE PLANNER ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(state.LastResponsePlanTrace)
                ? "last_plan=none"
                : $"last_plan={state.LastResponsePlanTrace}");
            if (state.ResponsePlanLog.Count > 0)
            {
                sb.AppendLine("recent_plans:");
                foreach (var item in state.ResponsePlanLog.TakeLast(6))
                    sb.AppendLine($"- {item}");
            }
            sb.AppendLine("rule: use this as private execution policy; do not recite it.");
            return sb.ToString();
        }

        public static string BuildRepairRules(KokoResponsePlanFrame? frame)
        {
            if (frame == null) return "";
            var sb = new StringBuilder();
            sb.AppendLine("RESPONSE PLAN REPAIR:");
            sb.AppendLine($"- intent: {frame.Intent}");
            sb.AppendLine($"- capability: {frame.Capability}");
            sb.AppendLine($"- stance: {frame.Stance}");
            sb.AppendLine($"- memory_policy: {frame.MemoryPolicy}");
            if (frame.RequiresCritique)
                sb.AppendLine("- include one real critique or tradeoff before agreeing.");
            if (frame.RequiresAction)
                sb.AppendLine("- prefer doing/solving over explaining that you can help.");
            if (frame.RequiresVaultRead)
                sb.AppendLine("- if answering about remembered facts, use vault/memory context instead of guessing.");
            sb.AppendLine("- keep Kokonoe competent, specific, and unsentimental.");
            return sb.ToString();
        }

        public static string BuildRepairRules(string? responsePlan)
        {
            if (string.IsNullOrWhiteSpace(responsePlan)) return "";
            return $"""
RESPONSE PLAN REPAIR:
{responsePlan.Trim()}
- rewrite must follow this private plan;
- do not mention the plan;
- if the plan says critique/action/vault-read, do that instead of generic reassurance.
""";
        }

        private static string ClassifyIntent(string lower)
        {
            if (ContainsAny(lower, "не хочу жити", "суїцид", "самоушкод", "померти")) return "crisis";
            if (ContainsAny(lower, "зроби", "виконай", "реаліз", "пофікси", "виправ", "додай", "створи", "запусти")) return "execute";
            if (ContainsAny(lower, "код", "build", "тест", "баг", "помилка", "stacktrace", "exception")) return "engineering";
            if (ContainsAny(lower, "vault", "obsidian", "нотат", "пам'ят", "що знаєш про мене", "що пам")) return "memory";
            if (ContainsAny(lower, "оцін", "крити", "як думаєш", "чи норм", "ідея")) return "evaluate";
            if (ContainsAny(lower, "архітектур", "система", "поведінк", "особист", "асистент")) return "architecture";
            if (ContainsAny(lower, "план", "стратег", "roadmap")) return "design";
            if (ContainsAny(lower, "поясни", "що це", "як працю")) return "explain";
            return "chat";
        }

        private static string ClassifyCapability(string lower)
        {
            if (ContainsAny(lower, "код", "build", "тест", "exception", "stacktrace")) return "codebase";
            if (ContainsAny(lower, "vault", "obsidian", "нотат", "пам'ят")) return "vault_memory";
            if (ContainsAny(lower, "telegram", "тг", "бот")) return "telegram";
            if (ContainsAny(lower, "екран", "скрін", "бачиш")) return "screen_awareness";
            if (ContainsAny(lower, "здоров", "сон", "пульс", "стрес")) return "health";
            if (ContainsAny(lower, "календар", "нагад", "розклад")) return "calendar";
            return "conversation";
        }

        private static bool NeedsVaultRead(string lower, string intent)
            => intent == "memory" ||
               ContainsAny(lower, "що знаєш про мене", "що пам", "профіль", "досьє", "згадай", "в vault", "в obsidian");

        private static bool NeedsToolUse(string lower, string capability, bool needsVaultRead)
            => needsVaultRead ||
               capability is "codebase" or "vault_memory" or "telegram" or "screen_awareness" or "calendar" ||
               ContainsAny(lower, "запусти", "перевір", "прочитай файл", "відкрий", "знайди");

        private static string BuildMemoryPolicy(string lower, string intent)
        {
            if (intent == "crisis") return "daily_log_only";
            if (ContainsAny(lower, "запам'ятай", "запиши", "це важливо", "моє правило", "я люблю", "я ненавиджу", "мені подобається"))
                return "store_stable_fact";
            if (intent == "memory") return "read_before_answer";
            if (ContainsAny(lower, "зараз", "сьогодні", "втом", "сон", "голод", "настрій")) return "daily_or_temporary";
            return "do_not_store";
        }

        private static string BuildRisk(string lower, KokoResponsePlanFrame frame)
        {
            if (frame.Intent == "crisis") return "critical";
            if (ContainsAny(lower, "видали", "перезапиши", "очисти", "мігруй", "знеси")) return "high";
            if (frame.RequiresToolUse || frame.MemoryPolicy == "store_stable_fact") return "medium";
            return "low";
        }

        private static string BuildStance(KokoResponsePlanFrame frame, KokoInternalState state)
        {
            if (frame.Intent == "crisis") return "protective_direct";
            if (frame.Risk == "high") return "verify_before_action";
            if (frame.RequiresCritique) return "critical_operator";
            if (frame.RequiresAction) return "operator";
            if (state.PersonalityDailyMood == "sharp") return "dry_direct";
            return "direct";
        }

        private static string BuildReason(KokoResponsePlanFrame frame)
            => frame.Intent switch
            {
                "execute" => "користувач хоче результат, не розмовний пінг-понг",
                "engineering" => "потрібен технічний аналіз і перевірка",
                "memory" => "відповідь залежить від пам'яті або Vault",
                "evaluate" => "потрібне судження, а не автоматична згода",
                "architecture" => "зачіпає поведінку системи і довгострокову якість",
                "crisis" => "високий ризик, снарк прибрати",
                _ => "звичайна відповідь, але без ботного цукру"
            };

        private static List<string> BuildSteps(KokoResponsePlanFrame frame)
        {
            var steps = new List<string>();
            if (frame.RequiresVaultRead) steps.Add("read relevant memory/vault context before making claims");
            if (frame.RequiresToolUse) steps.Add("use available tools when they materially reduce guessing");
            if (frame.RequiresCritique) steps.Add("identify flaw/tradeoff before proposing improved version");
            if (frame.RequiresAction) steps.Add("execute or give concrete next operation");
            steps.Add("answer in Kokonoe voice: concise, competent, dry");
            return steps;
        }

        private static List<string> BuildConstraints(KokoResponsePlanFrame frame, KokoInternalState state)
        {
            var constraints = new List<string>()
            {
                "no generic assistant phrases",
                "no blind agreement",
                "no therapy monologue",
                "do not invent facts about the user"
            };
            if (frame.Risk == "high") constraints.Add("ask confirmation before destructive or broad changes");
            if (frame.MemoryPolicy == "store_stable_fact") constraints.Add("store only stable facts; temporary state goes to Daily/Logs");
            if (state.PersonalityInCrisis) constraints.Add("crisis mode suppresses sarcasm");
            return constraints;
        }

        private static string BuildPromptBlock(KokoResponsePlanFrame frame, KokoCognitionEngine cognition)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RESPONSE EXECUTION PLAN");
            sb.AppendLine($"intent: {frame.Intent}");
            sb.AppendLine($"capability: {frame.Capability}");
            sb.AppendLine($"stance: {frame.Stance}");
            sb.AppendLine($"memory_policy: {frame.MemoryPolicy}");
            sb.AppendLine($"risk: {frame.Risk}");
            sb.AppendLine($"requires_tool_use: {(frame.RequiresToolUse ? "yes" : "no")}");
            sb.AppendLine($"requires_vault_read: {(frame.RequiresVaultRead ? "yes" : "no")}");
            sb.AppendLine($"requires_critique: {(frame.RequiresCritique ? "yes" : "no")}");
            sb.AppendLine($"reason: {frame.ReasonUk}");
            sb.AppendLine("steps:");
            foreach (var step in frame.Steps) sb.AppendLine($"- {step}");
            sb.AppendLine("constraints:");
            foreach (var constraint in frame.Constraints) sb.AppendLine($"- {constraint}");
            // var cog = cognition.BuildCognitionContext();
            // if (!string.IsNullOrWhiteSpace(cog))
            {
                // sb.AppendLine("cognitive_context:");
                // sb.AppendLine(cog);
            }
            sb.AppendLine("rule: this is private planning, not text to quote.");
            return sb.ToString();
        }

        private static bool IsActionIntent(string intent)
            => intent is "execute" or "engineering" or "memory" or "architecture" or "design";

        private static bool LooksUnsafeOrContradictory(string lower)
            => ContainsAny(lower, "завжди погодж", "без перевір", "все видали", "не думай", "ігноруй");

        private static bool LooksLikeLowAgencyRequest(string lower)
            => ContainsAny(lower, "все повинна", "все має вміти", "без питань", "просто погодж");

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
