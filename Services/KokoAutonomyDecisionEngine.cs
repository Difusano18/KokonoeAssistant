using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAutonomyDecision
    {
        public bool ShouldAct { get; set; }
        public string Source { get; set; } = "idle";
        public string Trigger { get; set; } = "autonomy_idle";
        public string StyleHint { get; set; } = "observation";
        public string Reason { get; set; } = "";
        public string ExtraContext { get; set; } = "";
        public int Priority { get; set; }
        public bool ConsumesInitiativeState { get; set; }
        public bool ConsumesPresenceInterrupt { get; set; }
        public string SilenceReason { get; set; } = "";
    }

    public sealed class KokoAutonomyDecisionEngine
    {
        private sealed record Candidate(
            string Source,
            string Trigger,
            string StyleHint,
            string Reason,
            string ExtraContext,
            int Priority,
            bool ConsumesInitiativeState,
            bool ConsumesPresenceInterrupt);

        public KokoAutonomyDecision Evaluate(
            DateTime now,
            KokoInternalState state,
            KokoPresenceFrame presence,
            KokoInternalDayFrame internalDay,
            KokoInitiativeDecision initiative,
            KokoRelationshipState relationship,
            KokoSomaticSnapshot somatic,
            KokoPatternEngine.RhythmProfile rhythm,
            int autonomyLevel)
        {
            autonomyLevel = Math.Clamp(autonomyLevel, 0, 3);
            var candidates = BuildCandidates(now, state, presence, internalDay, initiative, relationship, somatic, rhythm, autonomyLevel)
                .OrderByDescending(c => c.Priority)
                .ToList();

            if (candidates.Count == 0)
                return Wait("no_candidate", "немає достатньої причини втручатися");

            var strongest = candidates[0];
            var silenceGate = BuildSilenceGate(internalDay, strongest, rhythm, autonomyLevel);
            if (silenceGate != null)
                return Wait(strongest.Trigger, silenceGate);

            return new KokoAutonomyDecision
            {
                ShouldAct = true,
                Source = strongest.Source,
                Trigger = strongest.Trigger,
                StyleHint = strongest.StyleHint,
                Reason = strongest.Reason,
                ExtraContext = BuildContext(strongest, presence, internalDay, relationship, somatic, rhythm),
                Priority = strongest.Priority,
                ConsumesInitiativeState = strongest.ConsumesInitiativeState,
                ConsumesPresenceInterrupt = strongest.ConsumesPresenceInterrupt
            };
        }

        public void RecordDecision(KokoInternalState state, KokoAutonomyDecision decision, DateTime now)
        {
            var line = decision.ShouldAct
                ? $"{now:HH:mm} act:{decision.Trigger} src:{decision.Source} p{decision.Priority} - {decision.Reason}"
                : $"{now:HH:mm} wait:{decision.Trigger} - {decision.SilenceReason}";

            state.LastAutonomyDecision = line;
            state.LastAutonomyDecisionAt = now;
            state.LastAutonomyShouldAct = decision.ShouldAct;
            state.LastAutonomySource = decision.Source;
            state.LastAutonomyTrigger = decision.Trigger;
            state.LastAutonomyReason = decision.Reason;
            state.LastAutonomySilenceReason = decision.SilenceReason;
            state.LastAutonomyPriority = decision.Priority;
            state.AutonomyDecisionLog.Add(line);
            if (state.AutonomyDecisionLog.Count > 60)
                state.AutonomyDecisionLog.RemoveRange(0, state.AutonomyDecisionLog.Count - 60);
        }

        public string BuildDebugBlock(KokoInternalState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== AUTONOMY DECISION ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(state.LastAutonomyDecision)
                ? "last_autonomy=none"
                : $"last_autonomy={state.LastAutonomyDecision}");
            if (state.AutonomyDecisionLog.Count > 0)
            {
                sb.AppendLine("recent_autonomy:");
                foreach (var item in state.AutonomyDecisionLog.TakeLast(6))
                    sb.AppendLine($"- {item}");
            }
            sb.AppendLine("rule: this explains why Kokonoe writes or stays silent; do not recite it.");
            return sb.ToString();
        }

        private static IEnumerable<Candidate> BuildCandidates(
            DateTime now,
            KokoInternalState state,
            KokoPresenceFrame presence,
            KokoInternalDayFrame internalDay,
            KokoInitiativeDecision initiative,
            KokoRelationshipState relationship,
            KokoSomaticSnapshot somatic,
            KokoPatternEngine.RhythmProfile rhythm,
            int autonomyLevel)
        {
            if (presence.ShouldInterrupt)
            {
                yield return new Candidate(
                    "presence",
                    presence.Trigger,
                    presence.StyleHint,
                    $"{presence.SummaryUk} | {internalDay.SummaryUk}",
                    presence.ExtraContext + "\n" + internalDay.PromptBlock,
                    ClampPriority(presence.Priority + Math.Max(0, internalDay.InitiativeBias / 3) + RhythmBias(rhythm)),
                    false,
                    true);
            }

            if (initiative.ShouldAct)
            {
                yield return new Candidate(
                    "initiative",
                    initiative.Trigger,
                    initiative.StyleHint,
                    initiative.Reason,
                    initiative.ExtraContext,
                    ClampPriority(initiative.Priority + internalDay.InitiativeBias + RhythmBias(rhythm)),
                    true,
                    false);
            }

            if (autonomyLevel >= 3 &&
                relationship.Protectiveness > 0.55f &&
                presence.SilenceMinutes > 180 &&
                now - state.LastSpontaneousAt > TimeSpan.FromHours(2))
            {
                yield return new Candidate(
                    "relationship",
                    "relationship_protective_check",
                    "warm",
                    $"protectiveness={relationship.Protectiveness:F2}, silence={presence.SilenceMinutes:F0}m",
                    "Стосунок у захисному режимі, а тиша вже значуща. Напиши коротко, без шаблонного «як справи».",
                    ClampPriority(74 + internalDay.InitiativeBias / 2 + RhythmBias(rhythm)),
                    false,
                    false);
            }

            if (autonomyLevel >= 3 &&
                somatic.State == "wired" &&
                !internalDay.ShouldPreferSilence &&
                now - state.LastSpontaneousAt > TimeSpan.FromMinutes(50))
            {
                yield return new Candidate(
                    "somatic",
                    "autonomy_somatic_pressure",
                    "jab",
                    $"somatic wired, strain={somatic.Strain:F2}",
                    "Цифрове тіло напружене. Якщо пишеш, це має бути короткий контрольований укол або точне питання.",
                    ClampPriority(68 + internalDay.InitiativeBias / 2 + RhythmBias(rhythm)),
                    false,
                    false);
            }
        }

        private static string? BuildSilenceGate(KokoInternalDayFrame internalDay, Candidate strongest, KokoPatternEngine.RhythmProfile rhythm, int autonomyLevel)
        {
            if (autonomyLevel <= 0)
                return "автономність вимкнена";

            if (internalDay.ShouldPreferSilence && strongest.Priority < 86)
                return $"внутрішній день просить мовчати; найсильніший кандидат {strongest.Trigger} має лише p{strongest.Priority}";

            if (rhythm.CurrentSlotSamples >= 3 &&
                rhythm.CurrentSlotActivityRate <= 0.20f &&
                strongest.Priority < 90)
                return $"ритм показує типовий тихий слот; p{strongest.Priority} недостатньо";

            if (autonomyLevel == 1 && strongest.Priority < 90)
                return $"низька автономність; p{strongest.Priority} недостатньо";

            return null;
        }

        private static KokoAutonomyDecision Wait(string trigger, string reason) => new()
        {
            ShouldAct = false,
            Source = "silence",
            Trigger = trigger,
            StyleHint = "cold",
            Reason = reason,
            SilenceReason = reason,
            Priority = 0
        };

        private static string BuildContext(
            Candidate candidate,
            KokoPresenceFrame presence,
            KokoInternalDayFrame internalDay,
            KokoRelationshipState relationship,
            KokoSomaticSnapshot somatic,
            KokoPatternEngine.RhythmProfile rhythm)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(candidate.ExtraContext))
                sb.AppendLine(candidate.ExtraContext.Trim());
            sb.AppendLine();
            sb.AppendLine("AUTONOMY DECISION");
            sb.AppendLine($"Джерело: {candidate.Source}.");
            sb.AppendLine($"Тригер: {candidate.Trigger}.");
            sb.AppendLine($"Причина: {candidate.Reason}.");
            sb.AppendLine($"Пріоритет: {candidate.Priority}.");
            sb.AppendLine($"Presence: {presence.SummaryUk}");
            sb.AppendLine($"Внутрішній день: {internalDay.SummaryUk}");
            sb.AppendLine($"Стосунок: trust={relationship.Trust:F2}, protectiveness={relationship.Protectiveness:F2}, friction={relationship.Friction:F2}.");
            sb.AppendLine($"Соматика: {somatic.State}, strain={somatic.Strain:F2}, calm={somatic.Calm:F2}.");
            sb.AppendLine($"Ритм: {rhythm.Summary}");
            sb.AppendLine("Правило: відповідь має звучати як природний наслідок цієї причини, не як службовий звіт.");
            return sb.ToString();
        }

        private static int RhythmBias(KokoPatternEngine.RhythmProfile rhythm)
        {
            if (rhythm.CurrentSlotSamples < 3) return 0;
            if (rhythm.CurrentSlotActivityRate >= 0.70f) return 8;
            if (rhythm.CurrentSlotActivityRate <= 0.20f) return -12;
            return 0;
        }

        private static int ClampPriority(int value) => Math.Clamp(value, 0, 100);
    }
}
