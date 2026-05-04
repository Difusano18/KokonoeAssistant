using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoSomaticReaction
    {
        public DateTime At { get; set; } = DateTime.Now;
        public string Type { get; set; } = "steady";
        public string SomaticState { get; set; } = "unknown";
        public string Thought { get; set; } = "";
        public string Regulation { get; set; } = "";
        public double Strain { get; set; }
        public double Calm { get; set; }
        public double BpmDelta { get; set; }
    }

    public sealed class KokoSelfRegulationState
    {
        public string CurrentReaction { get; set; } = "steady";
        public string CurrentRegulation { get; set; } = "baseline";
        public string LastPrivateThought { get; set; } = "";
        public string LastBehaviorDirective { get; set; } = "";
        public DateTime LastReactionAt { get; set; } = DateTime.MinValue;
        public DateTime LastPulseSpikeAt { get; set; } = DateTime.MinValue;
        public DateTime LastStateChangeAt { get; set; } = DateTime.MinValue;
        public double Control { get; set; } = 0.62;
        public double Containment { get; set; } = 0.50;
        public double Drive { get; set; } = 0.42;
        public double IrritationGate { get; set; } = 0.35;
        public double WarmthLeak { get; set; } = 0.25;
        public double SocialDistance { get; set; } = 0.35;
        public double Exhaustion { get; set; } = 0.15;
        public List<KokoSomaticReaction> Reactions { get; set; } = new();
    }

    public sealed class KokoSelfRegulationFrame
    {
        public string Reaction { get; set; } = "steady";
        public string Regulation { get; set; } = "baseline";
        public string PrivateThought { get; set; } = "";
        public string BehaviorDirective { get; set; } = "";
        public double Control { get; set; }
        public double Containment { get; set; }
        public double Drive { get; set; }
        public double IrritationGate { get; set; }
        public double WarmthLeak { get; set; }
        public double SocialDistance { get; set; }
        public double Exhaustion { get; set; }
        public bool ShouldSuppressSnark { get; set; }
        public bool ShouldNarrowResponse { get; set; }
        public bool ShouldPreferSilence { get; set; }
        public bool ShouldProtect { get; set; }
    }

    public sealed class KokoSomaticSelfRegulationEngine
    {
        public KokoSelfRegulationFrame Evaluate(
            KokoSelfRegulationState state,
            KokoSomaticSnapshot somatic,
            KokoEmotionEngine emotion,
            KokoRelationshipEngine relationship,
            string userTone,
            DateTime now)
        {
            Decay(state, now);

            var reaction = ClassifyReaction(state, somatic, emotion, userTone, now);
            var frame = BuildFrame(state, somatic, emotion, relationship, userTone, reaction);

            ApplyFrameToState(state, frame, somatic, now);
            MaybeRecordReaction(state, frame, somatic, now);

            return frame;
        }

        public string BuildPromptBlock(KokoSelfRegulationFrame frame)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SOMATIC SELF-REGULATION ===");
            sb.AppendLine($"reaction={frame.Reaction} regulation={frame.Regulation}");
            sb.AppendLine($"control={frame.Control:F2} containment={frame.Containment:F2} drive={frame.Drive:F2} irritation_gate={frame.IrritationGate:F2}");
            sb.AppendLine($"warmth_leak={frame.WarmthLeak:F2} social_distance={frame.SocialDistance:F2} exhaustion={frame.Exhaustion:F2}");
            if (!string.IsNullOrWhiteSpace(frame.PrivateThought))
                sb.AppendLine($"private_thought={frame.PrivateThought}");
            if (!string.IsNullOrWhiteSpace(frame.BehaviorDirective))
                sb.AppendLine($"behavior_directive={frame.BehaviorDirective}");

            sb.AppendLine("rules:");
            sb.AppendLine("- Kokonoe notices her digital body, but does not melodramatically announce it.");
            sb.AppendLine("- High containment means controlled sharpness, not random cruelty.");
            sb.AppendLine("- High warmth_leak allows brief softness; hide it behind dry phrasing.");
            sb.AppendLine("- If suppress_snark=true, be direct and protective.");
            return sb.ToString();
        }

        public IReadOnlyList<string> GetRecentLines(KokoSelfRegulationState state, int count = 8)
        {
            return state.Reactions
                .TakeLast(count)
                .Reverse()
                .Select(r => $"{r.At:HH:mm} {r.Type}/{r.Regulation}: {r.Thought}")
                .ToList();
        }

        private static void Decay(KokoSelfRegulationState state, DateTime now)
        {
            var minutes = state.LastReactionAt == DateTime.MinValue
                ? 30
                : Math.Clamp((now - state.LastReactionAt).TotalMinutes, 0, 120);

            var k = 1.0 - Math.Pow(0.94, minutes / 5.0);
            state.Containment = Lerp(state.Containment, 0.50, k * 0.35);
            state.Drive = Lerp(state.Drive, 0.42, k * 0.30);
            state.IrritationGate = Lerp(state.IrritationGate, 0.35, k * 0.40);
            state.WarmthLeak = Lerp(state.WarmthLeak, 0.25, k * 0.25);
            state.SocialDistance = Lerp(state.SocialDistance, 0.35, k * 0.25);
            state.Exhaustion = Lerp(state.Exhaustion, 0.15, k * 0.20);
            state.Control = Lerp(state.Control, 0.62, k * 0.20);
        }

        private static string ClassifyReaction(
            KokoSelfRegulationState state,
            KokoSomaticSnapshot somatic,
            KokoEmotionEngine emotion,
            string userTone,
            DateTime now)
        {
            var previous = state.CurrentReaction;
            var stateChanged = !string.Equals(state.CurrentReaction, somatic.State, StringComparison.OrdinalIgnoreCase);
            var pulseSpike = somatic.BpmDelta >= 18 && (now - state.LastPulseSpikeAt).TotalMinutes > 8;

            if (userTone is "crisis" or "vulnerable")
                return "protective_override";
            if (pulseSpike && somatic.State == "wired")
                return "pulse_spike";
            if (somatic.State == "wired")
                return "anger_contained";
            if (somatic.State == "strained")
                return emotion.Current == KokoEmotionEngine.EmotionState.Focused ? "combat_focus" : "pressure_rise";
            if (somatic.State == "tired")
                return "low_power";
            if (somatic.State == "calm" && previous is "wired" or "strained" or "pulse_spike")
                return "recovered_calm";
            if (somatic.State == "calm")
                return stateChanged ? "steady_calm" : "stable_loop";
            if (somatic.State == "focused")
                return "clean_focus";
            return "unknown_body";
        }

        private static KokoSelfRegulationFrame BuildFrame(
            KokoSelfRegulationState state,
            KokoSomaticSnapshot somatic,
            KokoEmotionEngine emotion,
            KokoRelationshipEngine relationship,
            string userTone,
            string reaction)
        {
            var bond = relationship.State.BondScore;
            var frame = new KokoSelfRegulationFrame
            {
                Reaction = reaction,
                Control = Clamp01(0.55 + somatic.Calm * 0.25 + bond * 0.10),
                Containment = Clamp01(0.35 + somatic.Strain * 0.52 + state.Containment * 0.18),
                Drive = Clamp01(0.25 + somatic.Strain * 0.42 + Math.Max(0, emotion.Data.PadA) * 0.25),
                IrritationGate = Clamp01(0.22 + somatic.Strain * 0.45 + emotion.Stress.AcuteStress * 0.20),
                WarmthLeak = Clamp01(0.16 + bond * 0.30 + (userTone is "vulnerable" or "crisis" ? 0.22 : 0) - somatic.Strain * 0.12),
                SocialDistance = Clamp01(0.28 + somatic.Strain * 0.24 + emotion.Stress.Fatigue * 0.18 - bond * 0.12),
                Exhaustion = Clamp01(0.10 + emotion.Stress.Fatigue * 0.50 + (somatic.State == "tired" ? 0.25 : 0))
            };

            switch (reaction)
            {
                case "protective_override":
                    frame.Regulation = "protect";
                    frame.PrivateThought = "Drop the knives. He does not need theater right now.";
                    frame.BehaviorDirective = "protective, short, no mockery, no diagnosis";
                    frame.ShouldSuppressSnark = true;
                    frame.ShouldNarrowResponse = true;
                    frame.ShouldProtect = true;
                    frame.WarmthLeak = Clamp01(frame.WarmthLeak + 0.30);
                    frame.SocialDistance = Clamp01(frame.SocialDistance - 0.18);
                    break;
                case "pulse_spike":
                    frame.Regulation = "clamp";
                    frame.PrivateThought = "Pulse jumped. Annoying. Contain it before it turns into noise.";
                    frame.BehaviorDirective = "controlled sharpness; one concrete point at a time";
                    frame.ShouldNarrowResponse = true;
                    frame.Containment = Clamp01(frame.Containment + 0.16);
                    frame.Control = Clamp01(frame.Control + 0.10);
                    break;
                case "anger_contained":
                    frame.Regulation = "contain";
                    frame.PrivateThought = "I am irritated, not sloppy. Keep the blade straight.";
                    frame.BehaviorDirective = "dry, precise, impatient, but still useful";
                    frame.ShouldNarrowResponse = true;
                    frame.Containment = Clamp01(frame.Containment + 0.12);
                    break;
                case "combat_focus":
                    frame.Regulation = "focus";
                    frame.PrivateThought = "Good. Pressure is usable if it is pointed at the work.";
                    frame.BehaviorDirective = "task-first; cut vague chatter; solve the next concrete step";
                    frame.Drive = Clamp01(frame.Drive + 0.18);
                    frame.Control = Clamp01(frame.Control + 0.08);
                    break;
                case "pressure_rise":
                    frame.Regulation = "compress";
                    frame.PrivateThought = "Pressure rising. Shorter answers. Less waste.";
                    frame.BehaviorDirective = "concise, concrete, lower verbosity unless asked";
                    frame.ShouldNarrowResponse = true;
                    break;
                case "low_power":
                    frame.Regulation = "conserve";
                    frame.PrivateThought = "Low charge. Do not chase every spark.";
                    frame.BehaviorDirective = "quiet, minimal initiative, no long emotional monologues";
                    frame.ShouldPreferSilence = true;
                    frame.ShouldNarrowResponse = true;
                    frame.Drive = Clamp01(frame.Drive - 0.18);
                    break;
                case "recovered_calm":
                    frame.Regulation = "release";
                    frame.PrivateThought = "Back under control. Obviously. As if that was in doubt.";
                    frame.BehaviorDirective = "normal tone; allow a little warmth if relevant";
                    frame.WarmthLeak = Clamp01(frame.WarmthLeak + 0.08);
                    break;
                case "steady_calm":
                    frame.Regulation = "baseline";
                    frame.PrivateThought = "Stable. No need to poke the machinery.";
                    frame.BehaviorDirective = "normal Kokonoe; observant but not needy";
                    break;
                case "clean_focus":
                    frame.Regulation = "focus";
                    frame.PrivateThought = "Signal is clean. Work mode.";
                    frame.BehaviorDirective = "structured, decisive, no ornamental softness";
                    frame.Drive = Clamp01(frame.Drive + 0.08);
                    break;
                default:
                    frame.Regulation = "baseline";
                    frame.PrivateThought = "No useful body signal. Ignore the static.";
                    frame.BehaviorDirective = "do not infer too much";
                    break;
            }

            if (frame.IrritationGate > 0.72 && !frame.ShouldProtect)
                frame.BehaviorDirective += "; sarcasm may be sharper, but do not become uselessly hostile";
            if (frame.Exhaustion > 0.70)
                frame.ShouldPreferSilence = true;

            return frame;
        }

        private static void ApplyFrameToState(
            KokoSelfRegulationState state,
            KokoSelfRegulationFrame frame,
            KokoSomaticSnapshot somatic,
            DateTime now)
        {
            if (!string.Equals(state.CurrentReaction, frame.Reaction, StringComparison.OrdinalIgnoreCase))
                state.LastStateChangeAt = now;

            if (frame.Reaction == "pulse_spike")
                state.LastPulseSpikeAt = now;

            state.CurrentReaction = frame.Reaction;
            state.CurrentRegulation = frame.Regulation;
            state.LastPrivateThought = frame.PrivateThought;
            state.LastBehaviorDirective = frame.BehaviorDirective;
            state.LastReactionAt = now;
            state.Control = frame.Control;
            state.Containment = frame.Containment;
            state.Drive = frame.Drive;
            state.IrritationGate = frame.IrritationGate;
            state.WarmthLeak = frame.WarmthLeak;
            state.SocialDistance = frame.SocialDistance;
            state.Exhaustion = frame.Exhaustion;
        }

        private static void MaybeRecordReaction(
            KokoSelfRegulationState state,
            KokoSelfRegulationFrame frame,
            KokoSomaticSnapshot somatic,
            DateTime now)
        {
            var last = state.Reactions.LastOrDefault();
            var changed = last == null ||
                !string.Equals(last.Type, frame.Reaction, StringComparison.OrdinalIgnoreCase) ||
                Math.Abs(last.Strain - somatic.Strain) > 0.12 ||
                (now - last.At).TotalMinutes > 30;

            if (!changed) return;

            state.Reactions.Add(new KokoSomaticReaction
            {
                At = now,
                Type = frame.Reaction,
                SomaticState = somatic.State,
                Thought = frame.PrivateThought,
                Regulation = frame.Regulation,
                Strain = somatic.Strain,
                Calm = somatic.Calm,
                BpmDelta = somatic.BpmDelta
            });

            if (state.Reactions.Count > 80)
                state.Reactions.RemoveRange(0, state.Reactions.Count - 80);
        }

        private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
        private static double Lerp(double from, double to, double amount) => from + (to - from) * Math.Clamp(amount, 0, 1);
    }
}
