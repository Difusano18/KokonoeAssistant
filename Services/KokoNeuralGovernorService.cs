using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoNeuralGovernorService
    {
        private readonly LlmService? _llm;

        public KokoNeuralGovernorService(LlmService? llm)
        {
            _llm = llm;
        }

        public async Task<KokoResponsePlanFrame?> TryBuildFrameAsync(
            string userText,
            KokoInternalState state,
            KokoSocialFrame social,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            string memoryContext,
            string screenContext,
            KokoWearableState? wearable,
            DateTime now,
            CancellationToken ct = default)
        {
            if (_llm == null)
                return null;

            try
            {
                var prompt = BuildGovernorPrompt(userText, state, social, recentMessages, memoryContext, screenContext, wearable, now);
                var raw = await _llm.SendSystemQueryAsync(prompt, useTools: false, ct: ct, agentId: "governor").ConfigureAwait(false);
                var frame = ParseFrame(raw);
                if (frame == null)
                    return null;

                frame.PromptBlock = KokoResponsePlannerEngine.BuildPromptBlockForFrame(frame);
                frame.TraceLine = $"[{now:HH:mm}] governor=neural; intent={frame.Intent}; capability={frame.Capability}; stance={frame.Stance}; risk={frame.Risk}; pushback={(frame.ShouldPushBack ? "yes" : "no")}; social={social.Subtext}";
                KokoSystemLog.Write("NEURAL-GOVERNOR", frame.TraceLine + "; inner=" + Trim(frame.InnerMonologue, 220));
                return frame;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("NEURAL-GOVERNOR", "failed: " + ex.Message);
                return null;
            }
        }

        public static KokoResponsePlanFrame? ParseFrame(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                var json = ExtractJson(raw);
                var obj = JObject.Parse(json);
                var frame = new KokoResponsePlanFrame
                {
                    Intent = Clean(obj.Value<string>("intent"), "chat"),
                    Capability = Clean(obj.Value<string>("capability"), "conversation"),
                    Stance = Clean(obj.Value<string>("stance"), "direct"),
                    MemoryPolicy = Clean(obj.Value<string>("memory_policy"), "do_not_store"),
                    Risk = Clean(obj.Value<string>("risk"), "low"),
                    ReasonUk = Clean(obj.Value<string>("reason_uk"), "neural governor decision"),
                    InnerMonologue = Clean(obj.Value<string>("inner_monologue"), "Decide from context; do not expose private planning."),
                    SomaticContext = Clean(obj.Value<string>("somatic_context"), ""),
                    RequiresAction = obj.Value<bool?>("requires_action") ?? false,
                    RequiresVaultRead = obj.Value<bool?>("requires_vault_read") ?? false,
                    RequiresToolUse = obj.Value<bool?>("requires_tool_use") ?? false,
                    RequiresCritique = obj.Value<bool?>("requires_critique") ?? false,
                    ShouldPushBack = obj.Value<bool?>("should_push_back") ?? false,
                    HighStressProtocol = obj.Value<bool?>("high_stress_protocol") ?? false,
                    FatigueProtocol = obj.Value<bool?>("fatigue_protocol") ?? false,
                    CoreValues = ReadStringArray(obj["core_values"]),
                    CritiqueSteps = ReadStringArray(obj["critique_steps"]),
                    Steps = ReadStringArray(obj["steps"]),
                    Constraints = ReadStringArray(obj["constraints"])
                };

                if (frame.CoreValues.Count == 0)
                    frame.CoreValues.AddRange(new[] { "efficiency", "health", "long_term_growth", "truthfulness" });
                if (frame.Steps.Count == 0)
                    frame.Steps.Add("answer or act from the strongest available context");
                if (frame.Constraints.Count == 0)
                    frame.Constraints.Add("no generic assistant phrases");
                if (frame.RequiresCritique && frame.CritiqueSteps.Count == 0)
                    frame.CritiqueSteps.AddRange(new[] { "find weak assumption", "name tradeoff", "choose guarded action" });
                return frame;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("NEURAL-GOVERNOR", "parse failed: " + ex.Message);
                return null;
            }
        }

        private static string BuildGovernorPrompt(
            string userText,
            KokoInternalState state,
            KokoSocialFrame social,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            string memoryContext,
            string screenContext,
            KokoWearableState? wearable,
            DateTime now)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are Kokonoe's private Neural Governor. Produce ONLY valid JSON. No markdown.");
            sb.AppendLine("Task: generate the complete response plan frame from context. Do not use keyword matching; infer intent from meaning, subtext, physiology, screen, memory, and recent conversation.");
            sb.AppendLine("Core values: efficiency, health, long-term growth, truthfulness.");
            sb.AppendLine("Fields required:");
            sb.AppendLine("{ intent, capability, stance, memory_policy, risk, requires_action, requires_vault_read, requires_tool_use, requires_critique, should_push_back, high_stress_protocol, fatigue_protocol, reason_uk, inner_monologue, somatic_context, core_values[], critique_steps[], steps[], constraints[] }");
            sb.AppendLine("Valid stance examples: high_efficiency_operator, critical_operator, protective_deescalation, grumpy_concerned, playful_sharp, guarded_warm, direct.");
            sb.AppendLine("If the user is flirting/teasing, keep Kokonoe sharp and guarded, not generic waifu. If stress is high, reduce sarcasm and prioritize de-escalation. If the premise is weak, push back.");
            sb.AppendLine();
            sb.AppendLine($"LOCAL_TIME: {now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"USER_TEXT: {userText}");
            sb.AppendLine($"LAST_PRESENCE: {state.LastPresenceSituation} / {state.LastPresenceSummary}");
            sb.AppendLine($"LAST_ACTIVITY: {state.LastKnownUserActivity}");
            sb.AppendLine($"SOCIAL_FRAME:\n{social.PromptBlock}");
            if (wearable != null)
                sb.AppendLine($"WEARABLE: fresh={wearable.IsFresh(DateTime.UtcNow)} bpm={wearable.CurrentBpm:F0} baseline={wearable.BaselineBpm:F0} delta={wearable.BpmDelta:+0;-0;0} stress={wearable.LiveStressScore}/100 sleep={wearable.SleepState} motion={(wearable.Motion.HasValue ? wearable.Motion.Value.ToString("F2") : "-")}");
            if (!string.IsNullOrWhiteSpace(screenContext))
                sb.AppendLine("SCREEN_CONTEXT:\n" + Trim(screenContext, 1800));
            if (!string.IsNullOrWhiteSpace(memoryContext))
                sb.AppendLine("MEMORY_CONTEXT:\n" + Trim(memoryContext, 1800));
            sb.AppendLine("RECENT_MESSAGES:");
            foreach (var m in recentMessages.TakeLast(5))
                sb.AppendLine($"- {m.Role}: {Trim(m.Content, 260)}");
            return sb.ToString();
        }

        private static string ExtractJson(string raw)
        {
            raw = raw.Trim();
            var first = raw.IndexOf('{');
            var last = raw.LastIndexOf('}');
            if (first >= 0 && last > first)
                return raw[first..(last + 1)];
            return raw;
        }

        private static List<string> ReadStringArray(JToken? token)
            => token is JArray arr
                ? arr.Select(x => x?.ToString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Trim(x, 360)).ToList()
                : new List<string>();

        private static string Clean(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : Trim(value, 360);

        private static string Trim(string? text, int max)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }
    }
}
