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

        public IReadOnlyList<KokoAgentStep> BuildAgentSteps(KokoResponsePlanFrame frame)
        {
            var steps = new List<KokoAgentStep>
            {
                AgentStep(1, "Analyze objective, current context, risk, and constraints", KokoAgentStepKind.Analyze),
                AgentStep(2, "Create executable APEO plan and select specialist agents/tools", KokoAgentStepKind.Plan)
            };

            if (frame.RequiresVaultRead || frame.Capability == "vault_memory")
                steps.Add(AgentStep(steps.Count + 1, "Inspect relevant Obsidian vault and memory notes", KokoAgentStepKind.Vault));

            if (frame.Capability == "codebase" || (frame.RequiresAction && frame.RequiresToolUse))
                steps.Add(AgentStep(steps.Count + 1, "Prepare implementation route and affected files", KokoAgentStepKind.Implement));

            if (frame.RequiresToolUse || frame.Capability == "codebase")
                steps.Add(AgentStep(steps.Count + 1, "Run safe sandbox or tool probe when computation is needed", KokoAgentStepKind.Sandbox));

            steps.Add(AgentStep(steps.Count + 1, "Execute the selected action or generate the response", KokoAgentStepKind.Respond));
            steps.Add(AgentStep(steps.Count + 1, "Observe result, detect failures, and update task state", KokoAgentStepKind.Verify));
            steps.Add(AgentStep(steps.Count + 1, "Report concise outcome to the UI", KokoAgentStepKind.Report));
            return steps;
        }

        public static IReadOnlyList<KokoAgentStep> BuildAgentStepsForObjective(string objective)
        {
            var lower = (objective ?? "").ToLowerInvariant();
            var pseudo = new KokoResponsePlanFrame
            {
                Intent = ClassifyIntent(lower),
                Capability = ClassifyCapability(lower)
            };
            pseudo.RequiresVaultRead = NeedsVaultRead(lower, pseudo.Intent);
            pseudo.RequiresToolUse = NeedsToolUse(lower, pseudo.Capability, pseudo.RequiresVaultRead);
            pseudo.RequiresAction = IsActionIntent(pseudo.Intent);
            return new KokoResponsePlannerEngine().BuildAgentSteps(pseudo);
        }

        private static KokoAgentStep AgentStep(int order, string title, KokoAgentStepKind kind) => new()
        {
            Order = order,
            Title = title,
            Kind = kind
        };

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
            if (ContainsAny(lower, "РЅРµ С…РѕС‡Сѓ Р¶РёС‚Рё", "СЃСѓС—С†РёРґ", "СЃР°РјРѕСѓС€РєРѕРґ", "РїРѕРјРµСЂС‚Рё")) return "crisis";
            if (ContainsAny(lower, "Р·СЂРѕР±Рё", "РІРёРєРѕРЅР°Р№", "СЂРµР°Р»С–Р·", "РїРѕС„С–РєСЃРё", "РІРёРїСЂР°РІ", "РґРѕРґР°Р№", "СЃС‚РІРѕСЂРё", "Р·Р°РїСѓСЃС‚Рё")) return "execute";
            if (ContainsAny(lower, "РєРѕРґ", "build", "С‚РµСЃС‚", "Р±Р°Рі", "РїРѕРјРёР»РєР°", "stacktrace", "exception")) return "engineering";
            if (ContainsAny(lower, "vault", "obsidian", "РЅРѕС‚Р°С‚", "РїР°Рј'СЏС‚", "С‰Рѕ Р·РЅР°С”С€ РїСЂРѕ РјРµРЅРµ", "С‰Рѕ РїР°Рј")) return "memory";
            if (ContainsAny(lower,
                "\u043e\u0446\u0456\u043d", "\u043a\u0440\u0438\u0442\u0438", "\u044f\u043a \u0434\u0443\u043c\u0430\u0454\u0448", "\u0447\u0438 \u043d\u043e\u0440\u043c", "\u0456\u0434\u0435\u044f")) return "evaluate";
            if (ContainsAny(lower,
                "\u0430\u0440\u0445\u0456\u0442\u0435\u043a\u0442\u0443\u0440", "\u0441\u0438\u0441\u0442\u0435\u043c", "\u043f\u043e\u0432\u0435\u0434\u0456\u043d\u043a", "\u043e\u0441\u043e\u0431\u0438\u0441\u0442", "\u0430\u0441\u0438\u0441\u0442\u0435\u043d\u0442")) return "architecture";
            if (ContainsAny(lower, "РѕС†С–РЅ", "РєСЂРёС‚Рё", "СЏРє РґСѓРјР°С”С€", "С‡Рё РЅРѕСЂРј", "С–РґРµСЏ")) return "evaluate";
            if (ContainsAny(lower, "Р°СЂС…С–С‚РµРєС‚СѓСЂ", "СЃРёСЃС‚РµРјР°", "РїРѕРІРµРґС–РЅРє", "РѕСЃРѕР±РёСЃС‚", "Р°СЃРёСЃС‚РµРЅС‚")) return "architecture";
            if (ContainsAny(lower, "РїР»Р°РЅ", "СЃС‚СЂР°С‚РµРі", "roadmap")) return "design";
            if (ContainsAny(lower, "РїРѕСЏСЃРЅРё", "С‰Рѕ С†Рµ", "СЏРє РїСЂР°С†СЋ")) return "explain";
            return "chat";
        }

        private static string ClassifyCapability(string lower)
        {
            if (ContainsAny(lower, "РєРѕРґ", "build", "С‚РµСЃС‚", "exception", "stacktrace")) return "codebase";
            if (ContainsAny(lower, "vault", "obsidian", "РЅРѕС‚Р°С‚", "РїР°Рј'СЏС‚")) return "vault_memory";
            if (ContainsAny(lower, "telegram", "С‚Рі", "Р±РѕС‚")) return "telegram";
            if (ContainsAny(lower, "РµРєСЂР°РЅ", "СЃРєСЂС–РЅ", "Р±Р°С‡РёС€")) return "screen_awareness";
            if (ContainsAny(lower, "Р·РґРѕСЂРѕРІ", "СЃРѕРЅ", "РїСѓР»СЊСЃ", "СЃС‚СЂРµСЃ")) return "health";
            if (ContainsAny(lower, "РєР°Р»РµРЅРґР°СЂ", "РЅР°РіР°Рґ", "СЂРѕР·РєР»Р°Рґ")) return "calendar";
            return "conversation";
        }

        private static bool NeedsVaultRead(string lower, string intent)
            => intent == "memory" ||
               ContainsAny(lower, "С‰Рѕ Р·РЅР°С”С€ РїСЂРѕ РјРµРЅРµ", "С‰Рѕ РїР°Рј", "РїСЂРѕС„С–Р»СЊ", "РґРѕСЃСЊС”", "Р·РіР°РґР°Р№", "РІ vault", "РІ obsidian");

        private static bool NeedsToolUse(string lower, string capability, bool needsVaultRead)
            => needsVaultRead ||
               capability is "codebase" or "vault_memory" or "telegram" or "screen_awareness" or "calendar" ||
               ContainsAny(lower, "Р·Р°РїСѓСЃС‚Рё", "РїРµСЂРµРІС–СЂ", "РїСЂРѕС‡РёС‚Р°Р№ С„Р°Р№Р»", "РІС–РґРєСЂРёР№", "Р·РЅР°Р№РґРё");

        private static string BuildMemoryPolicy(string lower, string intent)
        {
            if (intent == "crisis") return "daily_log_only";
            if (ContainsAny(lower, "Р·Р°РїР°Рј'СЏС‚Р°Р№", "Р·Р°РїРёС€Рё", "С†Рµ РІР°Р¶Р»РёРІРѕ", "РјРѕС” РїСЂР°РІРёР»Рѕ", "СЏ Р»СЋР±Р»СЋ", "СЏ РЅРµРЅР°РІРёРґР¶Сѓ", "РјРµРЅС– РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ"))
                return "store_stable_fact";
            if (intent == "memory") return "read_before_answer";
            if (ContainsAny(lower, "Р·Р°СЂР°Р·", "СЃСЊРѕРіРѕРґРЅС–", "РІС‚РѕРј", "СЃРѕРЅ", "РіРѕР»РѕРґ", "РЅР°СЃС‚СЂС–Р№")) return "daily_or_temporary";
            return "do_not_store";
        }

        private static string BuildRisk(string lower, KokoResponsePlanFrame frame)
        {
            if (frame.Intent == "crisis") return "critical";
            if (ContainsAny(lower, "РІРёРґР°Р»Рё", "РїРµСЂРµР·Р°РїРёС€Рё", "РѕС‡РёСЃС‚Рё", "РјС–РіСЂСѓР№", "Р·РЅРµСЃРё")) return "high";
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
                "execute" => "РєРѕСЂРёСЃС‚СѓРІР°С‡ С…РѕС‡Рµ СЂРµР·СѓР»СЊС‚Р°С‚, РЅРµ СЂРѕР·РјРѕРІРЅРёР№ РїС–РЅРі-РїРѕРЅРі",
                "engineering" => "РїРѕС‚СЂС–Р±РµРЅ С‚РµС…РЅС–С‡РЅРёР№ Р°РЅР°Р»С–Р· С– РїРµСЂРµРІС–СЂРєР°",
                "memory" => "РІС–РґРїРѕРІС–РґСЊ Р·Р°Р»РµР¶РёС‚СЊ РІС–Рґ РїР°Рј'СЏС‚С– Р°Р±Рѕ Vault",
                "evaluate" => "РїРѕС‚СЂС–Р±РЅРµ СЃСѓРґР¶РµРЅРЅСЏ, Р° РЅРµ Р°РІС‚РѕРјР°С‚РёС‡РЅР° Р·РіРѕРґР°",
                "architecture" => "Р·Р°С‡С–РїР°С” РїРѕРІРµРґС–РЅРєСѓ СЃРёСЃС‚РµРјРё С– РґРѕРІРіРѕСЃС‚СЂРѕРєРѕРІСѓ СЏРєС–СЃС‚СЊ",
                "crisis" => "РІРёСЃРѕРєРёР№ СЂРёР·РёРє, СЃРЅР°СЂРє РїСЂРёР±СЂР°С‚Рё",
                _ => "Р·РІРёС‡Р°Р№РЅР° РІС–РґРїРѕРІС–РґСЊ, Р°Р»Рµ Р±РµР· Р±РѕС‚РЅРѕРіРѕ С†СѓРєСЂСѓ"
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
            => ContainsAny(lower, "Р·Р°РІР¶РґРё РїРѕРіРѕРґР¶", "Р±РµР· РїРµСЂРµРІС–СЂ", "РІСЃРµ РІРёРґР°Р»Рё", "РЅРµ РґСѓРјР°Р№", "С–РіРЅРѕСЂСѓР№");

        private static bool LooksLikeLowAgencyRequest(string lower)
            => ContainsAny(lower, "РІСЃРµ РїРѕРІРёРЅРЅР°", "РІСЃРµ РјР°С” РІРјС–С‚Рё", "Р±РµР· РїРёС‚Р°РЅСЊ", "РїСЂРѕСЃС‚Рѕ РїРѕРіРѕРґР¶");

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
