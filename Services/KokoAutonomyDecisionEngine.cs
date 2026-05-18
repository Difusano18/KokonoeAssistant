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
                return Wait("no_candidate", "РЅРµРјР°С” РґРѕСЃС‚Р°С‚РЅСЊРѕС— РїСЂРёС‡РёРЅРё РІС‚СЂСѓС‡Р°С‚РёСЃСЏ");

            var strongest = candidates[0];
            var silenceGate = BuildSilenceGate(presence, internalDay, strongest, rhythm, autonomyLevel);
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
                    "РЎС‚РѕСЃСѓРЅРѕРє Сѓ Р·Р°С…РёСЃРЅРѕРјСѓ СЂРµР¶РёРјС–, Р° С‚РёС€Р° РІР¶Рµ Р·РЅР°С‡СѓС‰Р°. РќР°РїРёС€Рё РєРѕСЂРѕС‚РєРѕ, Р±РµР· С€Р°Р±Р»РѕРЅРЅРѕРіРѕ В«СЏРє СЃРїСЂР°РІРёВ».",
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
                    "Р¦РёС„СЂРѕРІРµ С‚С–Р»Рѕ РЅР°РїСЂСѓР¶РµРЅРµ. РЇРєС‰Рѕ РїРёС€РµС€, С†Рµ РјР°С” Р±СѓС‚Рё РєРѕСЂРѕС‚РєРёР№ РєРѕРЅС‚СЂРѕР»СЊРѕРІР°РЅРёР№ СѓРєРѕР» Р°Р±Рѕ С‚РѕС‡РЅРµ РїРёС‚Р°РЅРЅСЏ.",
                    ClampPriority(68 + internalDay.InitiativeBias / 2 + RhythmBias(rhythm)),
                    false,
                    false);
            }
        }

        private static string? BuildSilenceGate(KokoPresenceFrame presence, KokoInternalDayFrame internalDay, Candidate strongest, KokoPatternEngine.RhythmProfile rhythm, int autonomyLevel)
        {
            if (autonomyLevel <= 0)
                return "Р°РІС‚РѕРЅРѕРјРЅС–СЃС‚СЊ РІРёРјРєРЅРµРЅР°";

            if (presence.SituationKind == "active_absence" && strongest.Source != "presence")
                return $"С” Р°РєС‚РёРІРЅРёР№ РЅР°РјС–СЂ РєРѕСЂРёСЃС‚СѓРІР°С‡Р° (active intent); С‡РµРєР°С‚Рё РґРѕ {presence.NextUsefulAt:HH:mm}, РЅРµ РІРёРіР°РґСѓРІР°С‚Рё generic ping";

            if (internalDay.ShouldPreferSilence && strongest.Priority < 86)
                return $"РІРЅСѓС‚СЂС–С€РЅС–Р№ РґРµРЅСЊ РїСЂРѕСЃРёС‚СЊ РјРѕРІС‡Р°С‚Рё; РЅР°Р№СЃРёР»СЊРЅС–С€РёР№ РєР°РЅРґРёРґР°С‚ {strongest.Trigger} РјР°С” Р»РёС€Рµ p{strongest.Priority}";

            if (rhythm.CurrentSlotSamples >= 3 &&
                rhythm.CurrentSlotActivityRate <= 0.20f &&
                strongest.Priority < 90)
                return $"СЂРёС‚Рј РїРѕРєР°Р·СѓС” С‚РёРїРѕРІРёР№ С‚РёС…РёР№ СЃР»РѕС‚; p{strongest.Priority} РЅРµРґРѕСЃС‚Р°С‚РЅСЊРѕ";

            if (autonomyLevel == 1 && strongest.Priority < 90)
                return $"РЅРёР·СЊРєР° Р°РІС‚РѕРЅРѕРјРЅС–СЃС‚СЊ; p{strongest.Priority} РЅРµРґРѕСЃС‚Р°С‚РЅСЊРѕ";

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
            sb.AppendLine($"Р”Р¶РµСЂРµР»Рѕ: {candidate.Source}.");
            sb.AppendLine($"РўСЂРёРіРµСЂ: {candidate.Trigger}.");
            sb.AppendLine($"РџСЂРёС‡РёРЅР°: {candidate.Reason}.");
            sb.AppendLine($"РџСЂС–РѕСЂРёС‚РµС‚: {candidate.Priority}.");
            sb.AppendLine($"Presence: {presence.SummaryUk}");
            sb.AppendLine($"Р’РЅСѓС‚СЂС–С€РЅС–Р№ РґРµРЅСЊ: {internalDay.SummaryUk}");
            sb.AppendLine($"РЎС‚РѕСЃСѓРЅРѕРє: trust={relationship.Trust:F2}, protectiveness={relationship.Protectiveness:F2}, friction={relationship.Friction:F2}.");
            sb.AppendLine($"РЎРѕРјР°С‚РёРєР°: {somatic.State}, strain={somatic.Strain:F2}, calm={somatic.Calm:F2}.");
            sb.AppendLine($"Р РёС‚Рј: {rhythm.Summary}");
            sb.AppendLine("РџСЂР°РІРёР»Рѕ: РІС–РґРїРѕРІС–РґСЊ РјР°С” Р·РІСѓС‡Р°С‚Рё СЏРє РїСЂРёСЂРѕРґРЅРёР№ РЅР°СЃР»С–РґРѕРє С†С–С”С— РїСЂРёС‡РёРЅРё, РЅРµ СЏРє СЃР»СѓР¶Р±РѕРІРёР№ Р·РІС–С‚.");
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
