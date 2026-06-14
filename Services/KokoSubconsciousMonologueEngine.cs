using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoSubconsciousFrame
    {
        public string Mode { get; set; } = "steady_processing";
        public string IntentImpulse { get; set; } = "answer";
        public string ActionBias { get; set; } = "reply";
        public double AttentionScore { get; set; } = 0.45;
        public string InnerVoice { get; set; } = "";
        public string EmotionalInertia { get; set; } = "";
        public string AssociativeAnchor { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoSubconsciousMonologueEngine
    {
        private const int MaxSignals = 12;

        public KokoSubconsciousFrame Update(
            KokoInternalState state,
            string? userText,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            IReadOnlyList<ChatRepository.ChatMessage>? recentMessages,
            DateTime now)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (emotion == null) throw new ArgumentNullException(nameof(emotion));

            var lower = (userText ?? "").ToLowerInvariant();
            var recent = recentMessages?.TakeLast(12).ToList() ?? new List<ChatRepository.ChatMessage>();
            var signals = BuildSignals(state, lower, emotion, social, living, recent, now);
            var attention = ComputeAttention(state, lower, emotion, social, living, signals);
            var mode = PickMode(state, lower, emotion, social, living, attention);
            var impulse = PickIntentImpulse(mode, lower, attention, social, living);
            var actionBias = PickActionBias(impulse, lower, state);
            var inertia = BuildEmotionalInertia(state, emotion, social, living);
            var anchor = BuildAssociativeAnchor(state, recent, now);
            var innerVoice = BuildInnerVoice(mode, impulse, actionBias, attention, inertia, anchor);

            PushSignal(state, $"{now:HH:mm} {mode}/{impulse} attention={Fmt(attention)} {string.Join("; ", signals.Take(3))}");
            state.LastSubconsciousMode = mode;
            state.LastSubconsciousIntent = impulse;
            state.LastSubconsciousActionBias = actionBias;
            state.LastSubconsciousTrace = innerVoice;
            state.LastSubconsciousAt = now;
            state.SubconsciousAttentionScore = attention;

            var frame = new KokoSubconsciousFrame
            {
                Mode = mode,
                IntentImpulse = impulse,
                ActionBias = actionBias,
                AttentionScore = attention,
                InnerVoice = innerVoice,
                EmotionalInertia = inertia,
                AssociativeAnchor = anchor,
                PromptBlock = BuildPromptBlock(mode, impulse, actionBias, attention, innerVoice, inertia, anchor, signals),
                TraceLine = $"[{now:HH:mm}] subconscious={mode}; impulse={impulse}; bias={actionBias}; attention={Fmt(attention)}; {innerVoice}"
            };

            KokoSystemLog.Write("SUBCONSCIOUS", frame.TraceLine);
            return frame;
        }

        public string BuildPromptBlock(KokoInternalState state, KokoEmotionEngine emotion, DateTime now)
        {
            if (state == null || emotion == null) return "";

            var mode = string.IsNullOrWhiteSpace(state.LastSubconsciousMode)
                ? "steady_processing"
                : state.LastSubconsciousMode;
            var impulse = string.IsNullOrWhiteSpace(state.LastSubconsciousIntent)
                ? "answer"
                : state.LastSubconsciousIntent;
            var bias = string.IsNullOrWhiteSpace(state.LastSubconsciousActionBias)
                ? "reply"
                : state.LastSubconsciousActionBias;
            var attention = NormalizeAttention(state.SubconsciousAttentionScore);
            var inertia = $"emotion={emotion.Current}; stress={Fmt(emotion.Stress.TotalLoad())}; temperament={state.PersonaTemperamentState}; living={state.LastLivingConversationMode}";
            var anchor = string.IsNullOrWhiteSpace(state.LastVisualMemoryAnchor)
                ? state.EmotionalMemoryTrace.TakeLast(1).FirstOrDefault() ?? "none"
                : $"visual={Trim(state.LastVisualMemoryAnchor, 180)}";
            var voice = string.IsNullOrWhiteSpace(state.LastSubconsciousTrace)
                ? "No fresh subconscious frame; keep response grounded in current prompt blocks."
                : state.LastSubconsciousTrace;

            return BuildPromptBlock(mode, impulse, bias, attention, voice, inertia, anchor, state.SubconsciousSignals.TakeLast(5));
        }

        public static string BuildDirective(KokoInternalState state)
        {
            var mode = string.IsNullOrWhiteSpace(state.LastSubconsciousMode)
                ? "steady_processing"
                : state.LastSubconsciousMode;
            var impulse = string.IsNullOrWhiteSpace(state.LastSubconsciousIntent)
                ? "answer"
                : state.LastSubconsciousIntent;
            var bias = string.IsNullOrWhiteSpace(state.LastSubconsciousActionBias)
                ? "reply"
                : state.LastSubconsciousActionBias;
            return $"SUBCONSCIOUS DIRECTIVE: mode={mode}; impulse={impulse}; bias={bias}; attention={Fmt(NormalizeAttention(state.SubconsciousAttentionScore))}. Use as private steering, never expose as mechanics.";
        }

        private static List<string> BuildSignals(
            KokoInternalState state,
            string lower,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            IReadOnlyList<ChatRepository.ChatMessage> recent,
            DateTime now)
        {
            var signals = new List<string>();
            if (LooksTechnical(lower)) signals.Add("technical_problem");
            if (LooksActionRequest(lower)) signals.Add("action_request");
            if (LooksMemoryRequest(lower)) signals.Add("memory_or_vault_request");
            if (LooksSocial(lower) || social?.Subtext is "soft_affection" or "flirting_or_affectionate_teasing") signals.Add("social_bid");
            if (LooksCorrection(lower)) signals.Add("correction");
            if (LooksLowSignal(lower)) signals.Add("low_signal");
            if (emotion.Stress.TotalLoad() >= 0.60 || state.LastSomaticStrain >= 0.65) signals.Add("high_body_load");
            if (state.PersonaPatienceLevel < 0.40) signals.Add("low_patience");
            if (!string.IsNullOrWhiteSpace(state.LastVisualMemoryAnchor) && now - state.LastVisualMemoryAnchorAt < TimeSpan.FromHours(8)) signals.Add("visual_anchor_active");
            if (recent.Any(m => m.Role == "assistant" && KokoPersonaEngine.LooksBotLike(m.Content))) signals.Add("recent_bot_tone_risk");
            if (living?.Mode == "quiet_operator") signals.Add("living_quiet_operator");
            if (signals.Count == 0) signals.Add("ordinary_turn");
            return signals.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static double ComputeAttention(
            KokoInternalState state,
            string lower,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            IReadOnlyList<string> signals)
        {
            var score = 0.35;
            if (signals.Contains("technical_problem")) score += 0.20;
            if (signals.Contains("action_request")) score += 0.18;
            if (signals.Contains("memory_or_vault_request")) score += 0.16;
            if (signals.Contains("correction")) score += 0.16;
            if (signals.Contains("social_bid")) score += 0.10;
            if (signals.Contains("high_body_load")) score += 0.14;
            if (signals.Contains("visual_anchor_active")) score += 0.07;
            if (LooksLowSignal(lower)) score -= 0.08;
            if (social?.Urgency >= 0.50) score += 0.12;
            if (living?.Mode == "quiet_operator") score += 0.08;
            if (state.PersonalityInCrisis) score = Math.Max(score, 0.92);
            score += Math.Clamp(emotion.Stress.TotalLoad() * 0.12, 0, 0.12);
            return Math.Clamp(score, 0.10, 1.00);
        }

        private static string PickMode(
            KokoInternalState state,
            string lower,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            KokoLivingConversationFrame? living,
            double attention)
        {
            if (state.PersonalityInCrisis || emotion.Stress.TotalLoad() >= 0.72 || living?.Mode == "quiet_operator")
                return "survival_focus";
            if (LooksCorrection(lower))
                return "context_correction";
            if (LooksMemoryRequest(lower))
                return "associative_recall";
            if (LooksActionRequest(lower) || LooksTechnical(lower))
                return "operator_planning";
            if (social?.Subtext is "soft_affection" or "flirting_or_affectionate_teasing" || LooksSocial(lower))
                return "social_resonance";
            if (attention < 0.32)
                return "background_observe";
            return "steady_processing";
        }

        private static string PickIntentImpulse(string mode, string lower, double attention, KokoSocialFrame? social, KokoLivingConversationFrame? living)
            => mode switch
            {
                "survival_focus" => "stabilize_then_answer",
                "context_correction" => "accept_correction",
                "associative_recall" => "retrieve_then_synthesize",
                "operator_planning" => "act_or_plan",
                "social_resonance" => "respond_socially",
                "background_observe" => attention < 0.25 ? "stay_silent" : "one_small_question",
                _ => LooksQuestion(lower) ? "answer_precisely" : "answer"
            };

        private static string PickActionBias(string impulse, string lower, KokoInternalState state)
        {
            if (impulse == "stay_silent") return "observe_only";
            if (impulse == "act_or_plan" && LooksActionRequest(lower)) return "tool_or_code_action";
            if (impulse == "retrieve_then_synthesize") return "memory_read";
            if (impulse == "accept_correction") return "update_context_label";
            if (state.LastAutonomyShouldAct && state.LastAutonomyPriority >= 80) return "proactive_action";
            return "reply";
        }

        private static string BuildEmotionalInertia(KokoInternalState state, KokoEmotionEngine emotion, KokoSocialFrame? social, KokoLivingConversationFrame? living)
            => $"emotion={emotion.Current}; stress={Fmt(emotion.Stress.TotalLoad())}; patience={Fmt(state.PersonaPatienceLevel)}; warmth={state.PersonalityWarmth:F2}; social={social?.Subtext ?? "neutral"}; living={living?.Mode ?? state.LastLivingConversationMode}";

        private static string BuildAssociativeAnchor(KokoInternalState state, IReadOnlyList<ChatRepository.ChatMessage> recent, DateTime now)
        {
            if (!string.IsNullOrWhiteSpace(state.LastVisualMemoryAnchor) &&
                now - state.LastVisualMemoryAnchorAt < TimeSpan.FromHours(8))
                return $"visual: {Trim(state.LastVisualMemoryAnchor, 220)}";

            var emotional = state.EmotionalMemoryTrace.TakeLast(2).ToArray();
            if (emotional.Length > 0)
                return "emotional: " + string.Join(" / ", emotional.Select(x => Trim(x, 120)));

            var lastUser = recent.LastOrDefault(m => m.Role == "user");
            return lastUser == null ? "none" : $"recent_user: {Trim(lastUser.Content, 160)}";
        }

        private static string BuildInnerVoice(string mode, string impulse, string actionBias, double attention, string inertia, string anchor)
            => $"Mode {mode}; impulse {impulse}; bias {actionBias}; attention {Fmt(attention)}. Hold {inertia}. Anchor: {anchor}.";

        private static string BuildPromptBlock(
            string mode,
            string impulse,
            string actionBias,
            double attention,
            string innerVoice,
            string inertia,
            string anchor,
            IEnumerable<string> signals)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SUBCONSCIOUS MONOLOGUE FRAME");
            sb.AppendLine($"mode: {mode}");
            sb.AppendLine($"intent_impulse: {impulse}");
            sb.AppendLine($"action_bias: {actionBias}");
            sb.AppendLine($"attention_score: {Fmt(attention)}");
            sb.AppendLine($"inner_voice_summary: {innerVoice}");
            sb.AppendLine($"emotional_inertia: {inertia}");
            sb.AppendLine($"associative_anchor: {anchor}");
            sb.AppendLine($"signals: {string.Join(", ", signals)}");
            sb.AppendLine("rules:");
            sb.AppendLine("- This is private steering, not visible text. Do not mention subconscious frames, scores, modules, or mechanics.");
            sb.AppendLine("- If impulse=accept_correction, acknowledge the correction and update the active context instead of defending the previous answer.");
            sb.AppendLine("- If impulse=respond_socially, answer the social bid before tasks, with Kokonoe edge but no helpdesk tone.");
            sb.AppendLine("- If impulse=act_or_plan, produce concrete action or a compact plan; no fake progress reports.");
            sb.AppendLine("- If attention is low, keep it short; one useful inference beats a lecture.");
            return sb.ToString();
        }

        private static void PushSignal(KokoInternalState state, string signal)
        {
            if (string.IsNullOrWhiteSpace(signal)) return;
            state.SubconsciousSignals.Add(signal);
            if (state.SubconsciousSignals.Count > MaxSignals)
                state.SubconsciousSignals.RemoveRange(0, state.SubconsciousSignals.Count - MaxSignals);
        }

        private static bool LooksTechnical(string lower)
            => ContainsAny(lower, "build", "test", "error", "exception", "stack", "commit", "push", "fix", "debug", "лог", "помилка", "тест", "коміт", "білд");

        private static bool LooksActionRequest(string lower)
            => ContainsAny(lower, "зроби", "виконай", "онови", "пофікси", "додай", "прибери", "створи", "реалізуй", "run", "do it", "implement", "fix");

        private static bool LooksMemoryRequest(string lower)
            => ContainsAny(lower, "obsidian", "обсидіан", "пам", "профіль", "згадай", "vault", "memory");

        private static bool LooksSocial(string lower)
            => ContainsAny(lower, "поговор", "про тебе", "про мене", "миле", "люблю", "дурниц", "флірт", "заіг", "хех", "lol");

        private static bool LooksCorrection(string lower)
            => ContainsAny(lower, "це ж", "це не", "ти не", "не так", "виправ", "wrong", "actually", "це kokonoe", "це коконое");

        private static bool LooksQuestion(string lower)
            => lower.Contains('?') || ContainsAny(lower, "чому", "що", "як", "де", "коли", "поясни");

        private static bool LooksLowSignal(string lower)
            => string.IsNullOrWhiteSpace(lower) || lower.Trim().Length <= 4 || ContainsAny(lower, "ну типу", "і тд", "щось", "якось");

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static double NormalizeAttention(double value)
            => value <= 0 || double.IsNaN(value) || double.IsInfinity(value) ? 0.45 : Math.Clamp(value, 0.10, 1.00);

        private static string Fmt(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }
    }
}
