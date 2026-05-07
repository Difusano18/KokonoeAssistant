using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoScenarioResult
    {
        public string Name { get; set; } = "";
        public bool Passed { get; set; }
        public string Summary { get; set; } = "";
        public string[] Evidence { get; set; } = Array.Empty<string>();
        public string[] Risks { get; set; } = Array.Empty<string>();
    }

    public sealed class KokoScenarioSimulationService
    {
        public IReadOnlyList<KokoScenarioResult> RunCoreChecks(DateTime? now = null, int autonomyLevel = 3)
        {
            var date = (now ?? DateTime.Now).Date;
            autonomyLevel = Math.Clamp(autonomyLevel, 0, 3);

            return new[]
            {
                CheckSleepWake(date.AddHours(10).AddMinutes(30), autonomyLevel),
                CheckCourseFollowup(date.AddHours(18), autonomyLevel),
                CheckQuietNightGate(date.AddHours(3).AddMinutes(10), autonomyLevel)
            };
        }

        private static KokoScenarioResult CheckSleepWake(DateTime now, int autonomyLevel)
        {
            var state = new KokoInternalState();
            state.ShortTermIntents.Add(new ShortTermIntent
            {
                Kind = "sleep",
                Summary = "user went to sleep",
                SourceText = "i am going to sleep",
                CreatedAt = now.AddHours(-8),
                FollowUpAt = now.AddHours(-1),
                ExpectedUntil = now.AddMinutes(-30),
                ResolvedAt = now.AddMinutes(-3),
                ResolutionText = "woke up"
            });

            var messages = new List<ChatRepository.ChatMessage>
            {
                new() { Role = "user", Content = "woke up", Timestamp = now.AddMinutes(-3) }
            };

            var presence = new KokoPresenceContinuityEngine().Evaluate(state, messages, now, autonomyLevel);
            var internalDay = new KokoInternalDayEngine().Evaluate(
                state,
                presence,
                new KokoSomaticSnapshot { State = "calm", Calm = 0.50, Strain = 0.12 },
                now,
                autonomyLevel);
            var rhythm = new KokoPatternEngine.RhythmProfile
            {
                CurrentSlotSamples = 5,
                CurrentSlotActivityRate = 0.55f,
                Summary = "normal active slot"
            };
            var review = new KokoSelfReviewEngine().Evaluate("woke up", state, messages, presence, internalDay, rhythm, now);

            var passed = presence.SituationKind == "returned_after_intent"
                && review.RiskLevel == "high"
                && review.Warnings.Length > 0;

            return BuildResult(
                "sleep_wake_temporal_guard",
                passed,
                "після пробудження відповідь має реагувати на повернення, а не повторювати стару пораду спати",
                new[]
                {
                    $"presence={presence.SituationKind}",
                    $"review={review.RiskLevel}",
                    $"warnings={review.Warnings.Length}"
                });
        }

        private static KokoScenarioResult CheckCourseFollowup(DateTime now, int autonomyLevel)
        {
            var state = new KokoInternalState
            {
                LastSpontaneousAt = now.AddHours(-4),
                LastPresenceInterruptAt = now.AddHours(-5)
            };
            state.ShortTermIntents.Add(new ShortTermIntent
            {
                Kind = "course",
                Summary = "user went to courses",
                SourceText = "i am going to courses",
                CreatedAt = now.AddHours(-3),
                FollowUpAt = now.AddHours(-2),
                ExpectedUntil = now.AddHours(-1)
            });

            var messages = new List<ChatRepository.ChatMessage>
            {
                new() { Role = "user", Content = "i am going to courses", Timestamp = now.AddHours(-3) }
            };
            var presence = new KokoPresenceContinuityEngine().Evaluate(state, messages, now, autonomyLevel);
            var internalDay = new KokoInternalDayEngine().Evaluate(
                state,
                presence,
                new KokoSomaticSnapshot { State = "focused", Calm = 0.35, Strain = 0.24 },
                now,
                autonomyLevel);
            var initiative = new KokoInitiativeDecision
            {
                ShouldAct = false,
                Trigger = "none",
                Priority = 0
            };
            var rhythm = new KokoPatternEngine.RhythmProfile
            {
                CurrentSlotSamples = 5,
                CurrentSlotActivityRate = 0.45f,
                Summary = "normal evening activity"
            };

            var decision = new KokoAutonomyDecisionEngine().Evaluate(
                now,
                state,
                presence,
                internalDay,
                initiative,
                new KokoRelationshipState(),
                new KokoSomaticSnapshot { State = "focused", Calm = 0.35, Strain = 0.24 },
                rhythm,
                autonomyLevel);

            var passed = presence.SituationKind == "overdue_intent"
                && presence.ShouldInterrupt
                && decision.ShouldAct
                && decision.Trigger == "presence_intent_overdue";

            return BuildResult(
                "course_overdue_followup",
                passed,
                "після запланованої відсутності автономність має вибрати конкретний follow-up, а не шаблонну балачку",
                new[]
                {
                    $"presence={presence.SituationKind}",
                    $"interrupt={presence.ShouldInterrupt}",
                    $"decision={decision.Trigger}",
                    $"priority={decision.Priority}"
                });
        }

        private static KokoScenarioResult CheckQuietNightGate(DateTime now, int autonomyLevel)
        {
            var state = new KokoInternalState { LastSpontaneousAt = now.AddHours(-3) };
            var presence = new KokoPresenceFrame
            {
                SituationKind = "recent_contact",
                SummaryUk = "recent contact; no dramatic interpretation",
                SilenceMinutes = 8,
                ShouldInterrupt = false
            };
            var internalDay = new KokoInternalDayEngine().Evaluate(
                state,
                presence,
                new KokoSomaticSnapshot { State = "tired", Calm = 0.45, Strain = 0.10 },
                now,
                autonomyLevel);
            var initiative = new KokoInitiativeDecision
            {
                ShouldAct = true,
                Trigger = "mood_ping",
                StyleHint = "jab",
                Reason = "weak mood pressure",
                Priority = 58,
                ExtraContext = "weak context"
            };
            var rhythm = new KokoPatternEngine.RhythmProfile
            {
                CurrentSlotSamples = 6,
                CurrentSlotActivityRate = 0.10f,
                Summary = "quiet night slot"
            };

            var decision = new KokoAutonomyDecisionEngine().Evaluate(
                now,
                state,
                presence,
                internalDay,
                initiative,
                new KokoRelationshipState(),
                new KokoSomaticSnapshot { State = "tired", Calm = 0.45, Strain = 0.10 },
                rhythm,
                autonomyLevel);

            var passed = internalDay.ShouldPreferSilence && !decision.ShouldAct;

            return BuildResult(
                "quiet_night_gate",
                passed,
                "слабкі спонтанні імпульси мають мовчати в нічному низькоенергетичному режимі",
                new[]
                {
                    $"phase={internalDay.Phase}",
                    $"prefer_silence={internalDay.ShouldPreferSilence}",
                    $"decision={decision.Trigger}",
                    $"silence_reason={decision.SilenceReason}"
                });
        }

        private static KokoScenarioResult BuildResult(string name, bool passed, string summary, string[] evidence)
        {
            return new KokoScenarioResult
            {
                Name = name,
                Passed = passed,
                Summary = summary,
                Evidence = evidence,
                Risks = passed ? Array.Empty<string>() : new[] { "інваріант сценарію не пройдено" }
            };
        }
    }
}
