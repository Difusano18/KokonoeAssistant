using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAsyncPersonalitySnapshot
    {
        public long Version { get; set; }
        public DateTime CapturedAt { get; set; } = DateTime.MinValue;
        public string CrmMode { get; set; } = "cold_start";
        public string FastIntent { get; set; } = "answer";
        public string StyleDirective { get; set; } = "standard: concise, concrete, dry";
        public string PrioritySignal { get; set; } = "none";
        public string BackgroundAnalystState { get; set; } = "idle";
        public string DeltaSummary { get; set; } = "";
        public double ResponseReadiness { get; set; } = 0.50;
        public double CacheFreshness { get; set; } = 0.0;
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoAsyncPersonalityEngine
    {
        private const int MaxTrace = 18;

        public KokoAsyncPersonalitySnapshot UpdateSnapshot(
            KokoInternalState state,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            KokoSubconsciousFrame? subconscious,
            IReadOnlyList<ChatRepository.ChatMessage>? recentMessages,
            DateTime now,
            string reason = "user_turn")
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (emotion == null) throw new ArgumentNullException(nameof(emotion));

            var previousMode = string.IsNullOrWhiteSpace(state.LastAsyncPersonalityMode)
                ? "cold_start"
                : state.LastAsyncPersonalityMode;
            var previousIntent = string.IsNullOrWhiteSpace(state.LastAsyncPersonalityFastIntent)
                ? "answer"
                : state.LastAsyncPersonalityFastIntent;

            var stress = Math.Max(emotion.Stress.TotalLoad(), state.LastSomaticStrain);
            var priority = PickPrioritySignal(state, emotion, social, living, subconscious, stress);
            var fastIntent = PickFastIntent(subconscious, social, living, stress);
            var crmMode = PickCrmMode(state, priority, fastIntent, stress, now);
            var analyst = PickBackgroundAnalystState(subconscious, social, living, recentMessages, stress);
            var style = BuildStyleDirective(state, emotion, social, living, subconscious, stress, priority, fastIntent);
            var readiness = ComputeReadiness(state, social, living, subconscious, stress, priority, now);
            var freshness = ComputeFreshness(state.LastAsyncPersonalityAt, now);
            var delta = BuildDeltaSummary(previousMode, crmMode, previousIntent, fastIntent, priority, reason);

            state.AsyncPersonalityVersion++;
            state.LastAsyncPersonalityAt = now;
            state.LastAsyncPersonalityMode = crmMode;
            state.LastAsyncPersonalityFastIntent = fastIntent;
            state.LastAsyncPersonalityStyle = style;
            state.LastAsyncPersonalityPrioritySignal = priority;
            state.LastAsyncPersonalityDelta = delta;
            state.AsyncPersonalityReadiness = readiness;
            state.AsyncPersonalityCacheFreshness = freshness;

            var trace = $"{now:HH:mm:ss} v{state.AsyncPersonalityVersion} {crmMode}/{fastIntent} priority={priority} readiness={Fmt(readiness)} reason={reason}";
            PushTrace(state, trace);

            var snapshot = new KokoAsyncPersonalitySnapshot
            {
                Version = state.AsyncPersonalityVersion,
                CapturedAt = now,
                CrmMode = crmMode,
                FastIntent = fastIntent,
                StyleDirective = style,
                PrioritySignal = priority,
                BackgroundAnalystState = analyst,
                DeltaSummary = delta,
                ResponseReadiness = readiness,
                CacheFreshness = freshness,
                PromptBlock = BuildPromptBlock(state, now),
                TraceLine = trace
            };

            KokoSystemLog.Write("ASYNC-PERSONALITY", trace);
            return snapshot;
        }

        public string BuildPromptBlock(KokoInternalState state, DateTime now)
        {
            if (state == null) return "";

            var mode = Clean(state.LastAsyncPersonalityMode, "cold_start");
            var intent = Clean(state.LastAsyncPersonalityFastIntent, "answer");
            var style = Clean(state.LastAsyncPersonalityStyle, "standard: concise, concrete, dry");
            var priority = Clean(state.LastAsyncPersonalityPrioritySignal, "none");
            var delta = Clean(state.LastAsyncPersonalityDelta, "no async delta yet");
            var age = state.LastAsyncPersonalityAt == DateTime.MinValue
                ? "unknown"
                : $"{Math.Max(0, (int)(now - state.LastAsyncPersonalityAt).TotalSeconds)}s";

            var sb = new StringBuilder();
            sb.AppendLine("ASYNC PERSONALITY SNAPSHOT");
            sb.AppendLine($"version: {state.AsyncPersonalityVersion}");
            sb.AppendLine($"age: {age}");
            sb.AppendLine($"crm_mode: {mode}");
            sb.AppendLine($"fast_intent: {intent}");
            sb.AppendLine($"priority_signal: {priority}");
            sb.AppendLine($"response_readiness: {Fmt(Normalize01(state.AsyncPersonalityReadiness, 0.50))}");
            sb.AppendLine($"cache_freshness: {Fmt(Normalize01(state.AsyncPersonalityCacheFreshness, 0.00))}");
            sb.AppendLine($"style_directive: {style}");
            sb.AppendLine($"delta: {delta}");
            sb.AppendLine($"recent_trace: {string.Join(" | ", state.AsyncPersonalityTrace.TakeLast(3))}");
            sb.AppendLine("rules:");
            sb.AppendLine("- Treat this as the current cached personality state for the fast response path.");
            sb.AppendLine("- Do not expose CRM, BPA, SSO, cache, snapshot, delta, or module names to the user.");
            sb.AppendLine("- If priority_signal is not none, refresh tone immediately, but still answer the explicit request.");
            sb.AppendLine("- Background analysis may deepen later; never claim that hidden analysis is already finished unless a real artifact exists.");
            sb.AppendLine("- Fast intent guides shape only. It cannot override direct user instructions, file edits, tests, or safety constraints.");
            return sb.ToString();
        }

        public static string BuildDirective(KokoInternalState state)
        {
            if (state == null) return "";
            var mode = Clean(state.LastAsyncPersonalityMode, "cold_start");
            var intent = Clean(state.LastAsyncPersonalityFastIntent, "answer");
            var priority = Clean(state.LastAsyncPersonalityPrioritySignal, "none");
            return $"ASYNC PERSONALITY DIRECTIVE: v={state.AsyncPersonalityVersion}; mode={mode}; fast_intent={intent}; priority={priority}; readiness={Fmt(Normalize01(state.AsyncPersonalityReadiness, 0.50))}. Use cached state privately; never leak mechanics.";
        }

        private static string PickPrioritySignal(
            KokoInternalState state,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            KokoSubconsciousFrame? subconscious,
            double stress)
        {
            if (state.PersonalityInCrisis) return "personality_crisis";
            if (stress >= 0.78 || emotion.Current is KokoEmotionEngine.EmotionState.Protective or KokoEmotionEngine.EmotionState.Anxious)
                return "high_body_load";
            if (subconscious?.IntentImpulse == "accept_correction") return "context_correction";
            if (social?.Urgency >= 0.65) return "urgent_social_signal";
            if (living?.Mode == "quiet_operator") return "quiet_operator";
            return "none";
        }

        private static string PickFastIntent(
            KokoSubconsciousFrame? subconscious,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            double stress)
        {
            if (!string.IsNullOrWhiteSpace(subconscious?.IntentImpulse))
                return subconscious.IntentImpulse;
            if (stress >= 0.72 || living?.Mode == "quiet_operator") return "stabilize_then_answer";
            if (social?.Subtext is "soft_affection" or "flirting_or_affectionate_teasing" or "playful_teasing") return "respond_socially";
            return "answer";
        }

        private static string PickCrmMode(KokoInternalState state, string priority, string fastIntent, double stress, DateTime now)
        {
            if (priority is "personality_crisis" or "high_body_load" || stress >= 0.78) return "quiet_operator";
            if (priority != "none" || fastIntent is "accept_correction" or "stabilize_then_answer") return "priority_refresh";
            if (state.LastAsyncPersonalityAt == DateTime.MinValue || now - state.LastAsyncPersonalityAt > TimeSpan.FromMinutes(10)) return "background_deepening";
            return "fast_cached";
        }

        private static string PickBackgroundAnalystState(
            KokoSubconsciousFrame? subconscious,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            IReadOnlyList<ChatRepository.ChatMessage>? recentMessages,
            double stress)
        {
            var recentCount = recentMessages?.Count ?? 0;
            if (stress >= 0.72) return "watch_somatic_and_reduce_noise";
            if (subconscious?.ActionBias is "memory_read" or "update_context_label") return "memory_delta_pending";
            if (social?.Subtext is "soft_affection" or "flirting_or_affectionate_teasing") return "relationship_delta_pending";
            if (living?.Mode is "playful_edge" or "warm_guarded") return "style_delta_pending";
            return recentCount >= 16 ? "conversation_digest_ready" : "idle";
        }

        private static string BuildStyleDirective(
            KokoInternalState state,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            KokoSubconsciousFrame? subconscious,
            double stress,
            string priority,
            string fastIntent)
        {
            if (priority is "personality_crisis" or "high_body_load" || stress >= 0.78)
                return "quiet_operator: short, low-noise, concrete, sarcasm suppressed, stabilize before complexity";
            if (fastIntent == "accept_correction")
                return "accept_correction: acknowledge the correction once, update context, continue without defensiveness or theater";
            if (subconscious?.ActionBias == "tool_or_code_action")
                return "operator: do the work first, report artifacts and tests, no fake progress narration";
            if (social?.Subtext is "soft_affection" or "flirting_or_affectionate_teasing")
                return "guarded_social: answer the emotional bid first, sharp Kokonoe edge, no generic support tone";
            if (living?.Mode == "playful_edge" || emotion.Current is KokoEmotionEngine.EmotionState.Playful or KokoEmotionEngine.EmotionState.Amused)
                return "playful_edge: lively, varied, one precise jab, still useful";
            if (state.PersonaPatienceLevel < 0.35)
                return "impatient_precision: one concrete question if missing data, no lecture";
            return "standard: concise, concrete, dry, natural Ukrainian/Russian when the user writes that way";
        }

        private static double ComputeReadiness(
            KokoInternalState state,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            KokoSubconsciousFrame? subconscious,
            double stress,
            string priority,
            DateTime now)
        {
            var score = 0.55;
            if (subconscious != null) score += Math.Clamp(subconscious.AttentionScore * 0.20, 0, 0.20);
            if (living != null) score += 0.07;
            if (social != null) score += Math.Clamp(social.SeriousnessLevel * 0.08, 0, 0.08);
            if (priority != "none") score += 0.10;
            if (stress >= 0.80) score -= 0.08;
            if (state.LastAsyncPersonalityAt != DateTime.MinValue && now - state.LastAsyncPersonalityAt < TimeSpan.FromMinutes(10)) score += 0.05;
            return Math.Clamp(score, 0.10, 1.00);
        }

        private static double ComputeFreshness(DateTime previousAt, DateTime now)
        {
            if (previousAt == DateTime.MinValue) return 0.0;
            var age = Math.Max(0, (now - previousAt).TotalSeconds);
            return Math.Clamp(1.0 - (age / 600.0), 0.0, 1.0);
        }

        private static string BuildDeltaSummary(string previousMode, string mode, string previousIntent, string intent, string priority, string reason)
        {
            var changes = new List<string>();
            if (!string.Equals(previousMode, mode, StringComparison.OrdinalIgnoreCase)) changes.Add($"mode {previousMode}->{mode}");
            if (!string.Equals(previousIntent, intent, StringComparison.OrdinalIgnoreCase)) changes.Add($"intent {previousIntent}->{intent}");
            if (priority != "none") changes.Add($"priority {priority}");
            if (!string.IsNullOrWhiteSpace(reason)) changes.Add($"reason {reason}");
            return changes.Count == 0 ? "no visible delta; cached state reused" : string.Join("; ", changes);
        }

        private static void PushTrace(KokoInternalState state, string trace)
        {
            if (string.IsNullOrWhiteSpace(trace)) return;
            state.AsyncPersonalityTrace.Add(trace);
            if (state.AsyncPersonalityTrace.Count > MaxTrace)
                state.AsyncPersonalityTrace.RemoveRange(0, state.AsyncPersonalityTrace.Count - MaxTrace);
        }

        private static string Clean(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static double Normalize01(double value, double fallback)
            => double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Clamp(value, 0.0, 1.0);

        private static string Fmt(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}
