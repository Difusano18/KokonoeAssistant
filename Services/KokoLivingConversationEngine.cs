using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoLivingConversationFrame
    {
        public string Mode { get; set; } = "alive_direct";
        public string CurrentMove { get; set; } = "answer_first";
        public string EmotionalColor { get; set; } = "dry";
        public double Variability { get; set; } = 0.35;
        public string AvoidMoves { get; set; } = "";
        public string Reason { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoLivingConversationEngine
    {
        private const int MaxRecentMoves = 10;

        public KokoLivingConversationFrame Update(
            KokoInternalState state,
            string? userText,
            string? personaMode,
            KokoEmotionEngine emotion,
            KokoSocialFrame? social,
            DateTime now)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (emotion == null) throw new ArgumentNullException(nameof(emotion));

            var lower = (userText ?? "").ToLowerInvariant();
            var stress = Math.Max(emotion.Stress.TotalLoad(), state.LastSomaticStrain);
            var calm = Math.Max(0, state.LastSomaticCalm);
            var socialSubtext = social?.Subtext ?? "neutral";

            var mode = PickMode(state, lower, personaMode, emotion, socialSubtext, stress, calm, now);
            var move = PickMove(mode, state, lower);
            var color = PickColor(mode, emotion, stress);
            var variability = UpdateVariability(state, mode, socialSubtext, stress, now);
            var avoid = string.Join(", ", state.RecentConversationMoves.TakeLast(5));
            var reason = BuildReason(mode, move, socialSubtext, stress, calm, emotion);

            PushMove(state, move);
            state.LastLivingConversationMode = mode;
            state.LastLivingConversationTrace = reason;
            state.LastLivingConversationAt = now;
            state.LivingConversationVariability = variability;

            var frame = new KokoLivingConversationFrame
            {
                Mode = mode,
                CurrentMove = move,
                EmotionalColor = color,
                Variability = variability,
                AvoidMoves = avoid,
                Reason = reason,
                PromptBlock = BuildPromptBlock(mode, move, color, variability, avoid, reason),
                TraceLine = $"[{now:HH:mm}] living={mode}; move={move}; color={color}; variability={Fmt(variability)}; {reason}"
            };

            KokoSystemLog.Write("LIVING", frame.TraceLine);
            return frame;
        }

        public string BuildPromptBlock(KokoInternalState state, KokoEmotionEngine emotion, DateTime now)
        {
            if (state == null || emotion == null) return "";

            var mode = string.IsNullOrWhiteSpace(state.LastLivingConversationMode)
                ? "alive_direct"
                : state.LastLivingConversationMode;
            var move = state.RecentConversationMoves.LastOrDefault() ?? "answer_first";
            var color = PickColor(mode, emotion, Math.Max(emotion.Stress.TotalLoad(), state.LastSomaticStrain));
            var avoid = string.Join(", ", state.RecentConversationMoves.TakeLast(5));
            var reason = string.IsNullOrWhiteSpace(state.LastLivingConversationTrace)
                ? "passive prompt reuse; no fresh user turn"
                : state.LastLivingConversationTrace;

            return BuildPromptBlock(mode, move, color, NormalizeExisting(state.LivingConversationVariability, 0.35), avoid, reason);
        }

        public static string BuildDirective(KokoInternalState state)
        {
            var mode = string.IsNullOrWhiteSpace(state.LastLivingConversationMode)
                ? "alive_direct"
                : state.LastLivingConversationMode;
            var variability = NormalizeExisting(state.LivingConversationVariability, 0.35);
            var recent = string.Join(",", state.RecentConversationMoves.TakeLast(3));
            return $"LIVING CONVERSATION: mode={mode}; variability={Fmt(variability)}; recent_moves={recent}. Avoid helpdesk openings, repeated moves, and productivity pivots on social bids.";
        }

        private static string PickMode(
            KokoInternalState state,
            string lower,
            string? personaMode,
            KokoEmotionEngine emotion,
            string socialSubtext,
            double stress,
            double calm,
            DateTime now)
        {
            if (state.PersonalityInCrisis || stress >= 0.72 || emotion.Current == KokoEmotionEngine.EmotionState.Protective)
                return "quiet_operator";

            if (socialSubtext is "soft_affection" or "flirting_or_affectionate_teasing")
                return "warm_guarded";

            if (socialSubtext is "playful_teasing" or "boredom_time_killing" || LooksBanter(lower))
                return calm >= 0.45 || stress < 0.45 ? "playful_edge" : "alive_direct";

            if (LooksLowSignal(lower) || state.PersonaPatienceLevel < 0.38)
                return "impatient_precision";

            if (ContainsAny(personaMode ?? "", "operator", "architecture", "coding", "critical") ||
                ContainsAny(lower, "build", "test", "commit", "fix", "error", "stack", "лог", "помилка", "тест", "коміт"))
                return "alive_direct";

            if (now.Hour is >= 0 and < 5 && stress < 0.55)
                return "playful_edge";

            return "alive_direct";
        }

        private static string PickMove(string mode, KokoInternalState state, string lower)
        {
            var preferred = mode switch
            {
                "quiet_operator" => "ground_then_fix",
                "warm_guarded" => "answer_social_bid",
                "playful_edge" => "dry_banter_then_signal",
                "impatient_precision" => "extract_missing_signal",
                _ => LooksQuestion(lower) ? "direct_answer" : "answer_first"
            };

            if (!state.RecentConversationMoves.TakeLast(3).Contains(preferred, StringComparer.OrdinalIgnoreCase))
                return preferred;

            return preferred switch
            {
                "dry_banter_then_signal" => "sharp_observation",
                "answer_social_bid" => "guarded_warmth",
                "extract_missing_signal" => "one_concrete_question",
                "ground_then_fix" => "short_stabilize",
                _ => "concrete_next_step"
            };
        }

        private static string PickColor(string mode, KokoEmotionEngine emotion, double stress)
        {
            if (mode == "quiet_operator") return "low_noise_protective";
            if (mode == "warm_guarded") return "guarded_warmth";
            if (mode == "playful_edge") return "sharp_playful";
            if (mode == "impatient_precision") return "dry_impatient";
            if (stress >= 0.55) return "focused_serious";
            if (emotion.Current is KokoEmotionEngine.EmotionState.Playful or KokoEmotionEngine.EmotionState.Curious) return "curious_dry";
            return "dry_competent";
        }

        private static double UpdateVariability(KokoInternalState state, string mode, string socialSubtext, double stress, DateTime now)
        {
            var current = NormalizeExisting(state.LivingConversationVariability, 0.35);
            var target = mode switch
            {
                "quiet_operator" => 0.18,
                "warm_guarded" => 0.55,
                "playful_edge" => 0.70,
                "impatient_precision" => 0.32,
                _ => 0.42
            };

            if (stress >= 0.70) target = Math.Min(target, 0.22);
            if (socialSubtext is "soft_affection" or "flirting_or_affectionate_teasing") target = Math.Max(target, 0.52);

            var idleMinutes = state.LastLivingConversationAt == DateTime.MinValue ? 0 : Math.Max(0, (now - state.LastLivingConversationAt).TotalMinutes);
            var blend = idleMinutes > 30 ? 0.45 : 0.25;
            return Math.Clamp((current * (1 - blend)) + (target * blend), 0.10, 0.85);
        }

        private static string BuildPromptBlock(string mode, string move, string color, double variability, string avoid, string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LIVING CONVERSATION CONTROL");
            sb.AppendLine($"mode: {mode}");
            sb.AppendLine($"current_move: {move}");
            sb.AppendLine($"emotional_color: {color}");
            sb.AppendLine($"variability: {Fmt(variability)}");
            sb.AppendLine($"avoid_recent_moves: {(string.IsNullOrWhiteSpace(avoid) ? "none" : avoid)}");
            sb.AppendLine($"reason: {reason}");
            sb.AppendLine("rules:");
            sb.AppendLine("- Answer like a person with a live mind, not a ticket system. No 'I understand', 'how can I help', 'let me know', or generic support openings.");
            sb.AppendLine("- Kokonoe Mercury base: brilliant, dry, impatient, guarded; useful first, sharp second.");
            sb.AppendLine("- Social bids get a real social reply first. Do not pivot affection, boredom, or banter into productivity unless the user asks.");
            sb.AppendLine("- Work requests get action first. Style follows the work; do not perform lore, stage directions, or domination theater.");
            sb.AppendLine("- Change emotional color only when context changes: stress, affection, urgency, boredom, success, or fatigue. No random mood whiplash.");
            sb.AppendLine("- Do not repeat the same opening, jab, clarification loop, or productivity scold from recent moves.");
            sb.AppendLine("- If data is missing, ask one concrete question or state the missing sensor/file/log plainly.");
            sb.AppendLine("- Keep Ukrainian/Russian conversation natural when the user writes that way; English only for code terms or logs.");
            return sb.ToString();
        }

        private static string BuildReason(string mode, string move, string socialSubtext, double stress, double calm, KokoEmotionEngine emotion)
            => $"subtext={socialSubtext}; stress={Fmt(stress)}; calm={Fmt(calm)}; emotion={emotion.Current}; move={move}; mode={mode}";

        private static void PushMove(KokoInternalState state, string move)
        {
            if (string.IsNullOrWhiteSpace(move)) return;
            state.RecentConversationMoves.Add(move);
            if (state.RecentConversationMoves.Count > MaxRecentMoves)
                state.RecentConversationMoves.RemoveRange(0, state.RecentConversationMoves.Count - MaxRecentMoves);
        }

        private static bool LooksQuestion(string lower)
            => lower.Contains('?') || ContainsAny(lower, "чому", "що", "як", "де", "коли", "можеш", "поясни");

        private static bool LooksBanter(string lower)
            => ContainsAny(lower, "дурниц", "поговор", "про тебе", "про мене", "миле", "заіг", "флірт", "хех", "лол", "жарт");

        private static bool LooksLowSignal(string lower)
            => string.IsNullOrWhiteSpace(lower) ||
               lower.Trim().Length <= 4 ||
               ContainsAny(lower, "ну типу", "і тд", "щось", "якось", "короче");

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static double NormalizeExisting(double value, double fallback)
            => value <= 0 || double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Clamp(value, 0.10, 0.85);

        private static string Fmt(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}
