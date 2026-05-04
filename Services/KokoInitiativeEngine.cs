using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoInitiativeDecision
    {
        public bool ShouldAct { get; set; }
        public string Trigger { get; set; } = "idle";
        public string StyleHint { get; set; } = "observation";
        public string Reason { get; set; } = "";
        public string ExtraContext { get; set; } = "";
        public int Priority { get; set; }
        public DateTime NextAllowedAt { get; set; } = DateTime.MinValue;
    }

    public sealed class KokoInitiativeEngine
    {
        private sealed record Candidate(
            string Trigger,
            string StyleHint,
            string Reason,
            string ExtraContext,
            int Priority,
            TimeSpan Cooldown);

        public KokoInitiativeDecision Evaluate(
            DateTime now,
            KokoInternalState state,
            KokoEmotionEngine emotion,
            KokoRelationshipEngine relationship,
            KokoMemoryEngine memory,
            ChatRepository chatRepo,
            double bpmDeviation = 0,
            KokoSomaticSnapshot? somatic = null,
            KokoSelfRegulationFrame? selfRegulation = null)
        {
            var candidates = BuildCandidates(now, state, emotion, relationship, memory, chatRepo, bpmDeviation, somatic, selfRegulation)
                .OrderByDescending(c => c.Priority)
                .ToList();

            if (selfRegulation?.ShouldPreferSilence == true &&
                candidates.All(c => c.Priority < 80))
            {
                return new KokoInitiativeDecision
                {
                    ShouldAct = false,
                    Trigger = "self_regulation_silence",
                    StyleHint = "cold",
                    Reason = $"self-regulation prefers silence: {selfRegulation.Reaction}",
                    Priority = 25
                };
            }

            if (candidates.Count == 0)
            {
                return new KokoInitiativeDecision
                {
                    ShouldAct = false,
                    Trigger = "idle",
                    Reason = "no internal pressure worth interrupting for"
                };
            }

            foreach (var candidate in candidates)
            {
                var nextAllowed = GetNextAllowedAt(state, candidate.Trigger, candidate.Cooldown);
                if (now >= nextAllowed)
                {
                    return new KokoInitiativeDecision
                    {
                        ShouldAct = true,
                        Trigger = candidate.Trigger,
                        StyleHint = candidate.StyleHint,
                        Reason = candidate.Reason,
                        ExtraContext = candidate.ExtraContext,
                        Priority = candidate.Priority,
                        NextAllowedAt = now + candidate.Cooldown
                    };
                }
            }

            var blocked = candidates[0];
            return new KokoInitiativeDecision
            {
                ShouldAct = false,
                Trigger = blocked.Trigger,
                StyleHint = blocked.StyleHint,
                Reason = $"blocked by cooldown: {blocked.Reason}",
                Priority = blocked.Priority,
                NextAllowedAt = GetNextAllowedAt(state, blocked.Trigger, blocked.Cooldown)
            };
        }

        public void RecordDecision(KokoInternalState state, KokoInitiativeDecision decision, DateTime now)
        {
            var summary = decision.ShouldAct
                ? $"{now:HH:mm} act:{decision.Trigger} p{decision.Priority} - {decision.Reason}"
                : $"{now:HH:mm} wait:{decision.Trigger} - {decision.Reason}";

            state.LastInitiativeDecision = summary;
            state.LastInitiativeDecisionAt = now;
            state.InitiativeReasonLog.Add(summary);
            if (state.InitiativeReasonLog.Count > 40)
                state.InitiativeReasonLog.RemoveRange(0, state.InitiativeReasonLog.Count - 40);

            if (decision.ShouldAct && !string.IsNullOrWhiteSpace(decision.Trigger))
                state.InitiativeCooldowns[decision.Trigger] = now;
        }

        public string BuildDebugBlock(KokoInternalState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== INITIATIVE STATE ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(state.LastInitiativeDecision)
                ? "last_decision=none"
                : $"last_decision={state.LastInitiativeDecision}");

            if (state.InitiativeReasonLog.Count > 0)
            {
                sb.AppendLine("recent_reasons:");
                foreach (var item in state.InitiativeReasonLog.TakeLast(5))
                    sb.AppendLine($"- {item}");
            }

            sb.AppendLine("rule: use initiative reasons as quiet context. Do not recite this block to the user.");
            return sb.ToString();
        }

        private static IEnumerable<Candidate> BuildCandidates(
            DateTime now,
            KokoInternalState state,
            KokoEmotionEngine emotion,
            KokoRelationshipEngine relationship,
            KokoMemoryEngine memory,
            ChatRepository chatRepo,
            double bpmDeviation,
            KokoSomaticSnapshot? somatic,
            KokoSelfRegulationFrame? selfRegulation)
        {
            var currentEmotion = emotion.Current.ToString();
            var relationshipState = relationship.State;

            if (state.PersonalityInCrisis)
            {
                yield return new Candidate(
                    "crisis",
                    "crisis",
                    "crisis flag is active",
                    "Crisis mode is active. Be short, direct, and protective.",
                    100,
                    TimeSpan.FromMinutes(20));
            }

            if (state.PendingTriggers.Any(t => t.FireAt <= now))
            {
                var trigger = state.PendingTriggers.OrderBy(t => t.FireAt).First(t => t.FireAt <= now);
                yield return new Candidate(
                    "reactive_followup",
                    "callback",
                    $"reactive follow-up is due: {trigger.Type}",
                    $"A pending follow-up is due. Context: {trigger.Context}",
                    92,
                    TimeSpan.FromMinutes(60));
            }

            if (somatic?.State == "wired" &&
                (now - state.LastSpontaneousAt).TotalMinutes > 45)
            {
                yield return new Candidate(
                    "somatic_wired",
                    "warm",
                    $"somatic state is wired: strain {somatic.Strain:F2}, delta {somatic.BpmDelta:F0} bpm",
                    "Somatic state is wired. If you write, make it short, grounded, and protective. Do not diagnose.",
                    88,
                    TimeSpan.FromHours(2));
            }

            if (selfRegulation?.ShouldProtect == true &&
                (now - state.LastSpontaneousAt).TotalMinutes > 35)
            {
                yield return new Candidate(
                    "self_regulation_protect",
                    "warm",
                    $"self-regulation protective override: {selfRegulation.Reaction}",
                    $"Self-regulation is in protect mode. Directive: {selfRegulation.BehaviorDirective}",
                    90,
                    TimeSpan.FromHours(2));
            }

            if (selfRegulation?.Reaction == "pulse_spike" &&
                (now - state.LastSpontaneousAt).TotalMinutes > 50)
            {
                yield return new Candidate(
                    "pulse_spike_reaction",
                    "observation",
                    "digital body noticed a pulse spike and clamped down",
                    $"Internal reaction: {selfRegulation.PrivateThought}. Write only if it becomes useful, short, and controlled.",
                    73,
                    TimeSpan.FromHours(3));
            }

            if (somatic?.State == "tired" &&
                state.CuriosityQueue.Count == 0 &&
                state.PendingThoughts.Count == 0)
            {
                yield return new Candidate(
                    "somatic_tired_silence",
                    "cold",
                    "somatic state is tired; initiative should stay quiet unless something else is important",
                    "Somatic state is tired. Prefer silence unless the message is truly useful.",
                    20,
                    TimeSpan.FromHours(4));
            }

            if (state.CuriosityQueue.Count > 0 && (now - state.LastCuriosityAskAt).TotalHours > 3)
            {
                var q = state.CuriosityQueue[^1];
                yield return new Candidate(
                    "curiosity",
                    "observation",
                    "curiosity queue has an unanswered question",
                    $"Ask this naturally, in one short Kokonoe-style message: \"{q}\"",
                    82 + (int)(relationshipState.Curiosity * 10),
                    TimeSpan.FromHours(3));
            }

            if (!string.IsNullOrEmpty(state.LastSentEmotionState) &&
                state.LastSentEmotionState != currentEmotion &&
                (now - state.LastSpontaneousAt).TotalMinutes > 45)
            {
                yield return new Candidate(
                    "emotion_shift",
                    "observation",
                    $"emotion shifted from {state.LastSentEmotionState} to {currentEmotion}",
                    $"The emotional state shifted from {state.LastSentEmotionState} to {currentEmotion}. Send one short line that reflects the shift without explaining the mechanism.",
                    76,
                    TimeSpan.FromMinutes(45));
            }

            var freshThought = state.InnerMonologues.LastOrDefault();
            if (!string.IsNullOrWhiteSpace(freshThought) &&
                (now - state.LastMonologueSentAt).TotalHours > 4)
            {
                yield return new Candidate(
                    "monologue",
                    "observation",
                    "fresh inner monologue has not been externalized",
                    $"A thought is still active: \"{freshThought}\". Let it produce a short message, not a quote.",
                    70,
                    TimeSpan.FromHours(4));
            }

            var observation = state.Observations.LastOrDefault();
            if (!string.IsNullOrWhiteSpace(observation) &&
                (now - state.LastSpontaneousAt).TotalMinutes > 90)
            {
                yield return new Candidate(
                    "observation",
                    "observation",
                    "latest observation is strong enough to surface",
                    $"Observation about him: \"{observation}\". Turn it into one concise line or a question.",
                    64,
                    TimeSpan.FromMinutes(90));
            }

            if (state.PendingThoughts.Count > 0)
            {
                var thought = state.PendingThoughts[^1];
                yield return new Candidate(
                    "pending",
                    "pending",
                    "pending thought is waiting to be said",
                    $"Pending thought: \"{thought}\". Say it briefly in your own words.",
                    60,
                    TimeSpan.FromHours(2));
            }

            var agitated = emotion.Current is
                KokoEmotionEngine.EmotionState.Excited or
                KokoEmotionEngine.EmotionState.Irritated or
                KokoEmotionEngine.EmotionState.Curious or
                KokoEmotionEngine.EmotionState.Anxious;

            if ((agitated || bpmDeviation > 15) &&
                (now - state.LastSpontaneousAt).TotalMinutes > 60)
            {
                yield return new Candidate(
                    "agitated_check",
                    "jab",
                    bpmDeviation > 15
                        ? $"physiology is elevated by {bpmDeviation:F0} bpm"
                        : $"emotion is agitated: {currentEmotion}",
                    $"Current emotion is {currentEmotion}. Send one concrete, not-generic line.",
                    55,
                    TimeSpan.FromHours(1));
            }

            var lastUser = chatRepo.GetMessages(30)
                .Where(m => m.Role == "user")
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();
            if (lastUser != null)
            {
                var silence = now - lastUser.Timestamp;
                if (silence.TotalHours > 6 && relationshipState.Protectiveness > 0.45f)
                {
                    yield return new Candidate(
                        "long_silence",
                        "warm",
                        $"long silence: {silence.TotalHours:F1}h with protective state",
                        "He has been silent for a while. If you write, make it specific and short.",
                        48,
                        TimeSpan.FromHours(6));
                }
            }

            var peakMemory = memory.GetPeakEpisodes(1).FirstOrDefault();
            if (peakMemory != null &&
                relationshipState.Intimacy > 0.55f &&
                (now - state.LastSpontaneousAt).TotalHours > 8)
            {
                yield return new Candidate(
                    "memory_echo",
                    "callback",
                    "relationship intimacy allows a memory callback",
                    $"Memory callback candidate: [{peakMemory.When:dd.MM}] {peakMemory.Summary}",
                    44,
                    TimeSpan.FromHours(10));
            }
        }

        private static DateTime GetNextAllowedAt(KokoInternalState state, string trigger, TimeSpan cooldown)
        {
            return state.InitiativeCooldowns.TryGetValue(trigger, out var last)
                ? last + cooldown
                : DateTime.MinValue;
        }
    }
}
