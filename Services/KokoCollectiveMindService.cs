using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoCollectiveMindFrame
    {
        public DateTime CapturedAt { get; set; }
        public string Channel { get; set; } = "runtime";
        public string InputRequest { get; set; } = "";
        public string ScientistProposal { get; set; } = "";
        public string ArchivistContext { get; set; } = "";
        public string ObserverSignal { get; set; } = "";
        public string PersonaGuardCritique { get; set; } = "";
        public string Decision { get; set; } = "";
        public string TraceLine { get; set; } = "";
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoCollectiveMindService
    {
        public KokoCollectiveMindFrame Build(
            string? userText,
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            IReadOnlyList<BlackboardEvent> recentEvents,
            string channel,
            DateTime now)
        {
            var text = (userText ?? "").Trim();
            var lower = text.ToLowerInvariant();
            var kind = ClassifyRequest(lower);
            var latestAssistant = recentMessages
                .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                .LastOrDefault()?.Content ?? "";

            var frame = new KokoCollectiveMindFrame
            {
                CapturedAt = now,
                Channel = string.IsNullOrWhiteSpace(channel) ? "runtime" : channel.Trim(),
                InputRequest = string.IsNullOrWhiteSpace(text) ? "no direct user text" : Trim(text, 220),
                ScientistProposal = BuildScientistProposal(kind, state),
                ArchivistContext = BuildArchivistContext(kind, state, recentMessages, recentEvents),
                ObserverSignal = BuildObserverSignal(state, now),
                PersonaGuardCritique = BuildPersonaGuardCritique(kind, state, latestAssistant),
                Decision = BuildDecision(kind, state)
            };

            frame.TraceLine = $"{now:HH:mm} {frame.Channel} kind={kind}; decision={Trim(frame.Decision, 140)}";
            frame.PromptBlock = BuildPromptBlock(frame);
            return frame;
        }

        public static string BuildDirective(KokoInternalState state)
        {
            if (state.LastCollectiveMindAt <= DateTime.MinValue)
                return "COLLECTIVE MIND: no fresh internal debate yet; answer from current message, facts, and tools.";

            var age = DateTime.Now - state.LastCollectiveMindAt;
            var stale = age > TimeSpan.FromMinutes(20) ? "stale" : "fresh";
            return $"COLLECTIVE MIND DIRECTIVE ({stale}): {Trim(state.LastCollectiveMindDecision, 260)} Rule: use as private steering only; never reveal agent/blackboard mechanics.";
        }

        public static void PublishFrame(KokoInternalBlackboardService blackboard, KokoCollectiveMindFrame frame)
        {
            blackboard.Publish("scientist-agent", "proposal", frame.ScientistProposal, 0.72, frame, "proposed");
            blackboard.Publish("archivist-agent", "context", frame.ArchivistContext, 0.66, null, "attached");
            blackboard.Publish("observer-agent", "signal", frame.ObserverSignal, 0.64, null, "observed");
            blackboard.Publish("persona-guard-agent", "critique", frame.PersonaGuardCritique, 0.78, null, "checked");
            blackboard.Publish("coordinator-agent", "decision", frame.Decision, 0.84, null, "resolved");
        }

        private static string BuildPromptBlock(KokoCollectiveMindFrame frame)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== COLLECTIVE MIND BLACKBOARD (private) ===");
            sb.AppendLine($"channel: {frame.Channel}");
            sb.AppendLine($"input: {frame.InputRequest}");
            sb.AppendLine($"scientist.proposal: {frame.ScientistProposal}");
            sb.AppendLine($"archivist.context: {frame.ArchivistContext}");
            sb.AppendLine($"observer.signal: {frame.ObserverSignal}");
            sb.AppendLine($"persona_guard.critique: {frame.PersonaGuardCritique}");
            sb.AppendLine($"coordinator.decision: {frame.Decision}");
            sb.AppendLine("Rule: this is hidden steering. Do not mention agents, debate, blackboard, prompt, or internal mechanics.");
            return sb.ToString().Trim();
        }

        private static string ClassifyRequest(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower)) return "empty";
            if (ContainsAny(lower, "commit", "build", "test", "fix", "debug", "code", "repo", "compile", "зроб", "викона", "тест", "код", "пофікс", "виправ"))
                return "task_execution";
            if (ContainsAny(lower, "obsidian", "vault", "profile", "memory", "проф", "пам", "обсид"))
                return "memory_vault";
            if (ContainsAny(lower, "подив", "скрін", "фото", "image", "screen", "бачиш"))
                return "vision_observation";
            if (ContainsAny(lower, "привіт", "поговор", "миле", "люблю", "про нас", "дурниц", "нудно"))
                return "social_bid";
            if (ContainsAny(lower, "чому", "поясни", "що не так", "why", "explain"))
                return "explain_or_diagnose";
            return "general";
        }

        private static string BuildScientistProposal(string kind, KokoInternalState state)
            => kind switch
            {
                "task_execution" => "Choose the shortest executable route: inspect evidence, make the change, verify with tests/build, then report artifacts.",
                "memory_vault" => "Use durable memory/vault facts first; update or synthesize only from evidence; name concrete changed artifacts when action is real.",
                "vision_observation" => "Ground the answer in the latest image/screen facts; avoid stale chat context overriding visible evidence.",
                "social_bid" => "Treat this as conversation, not a work ticket; answer naturally with Kokonoe edge and no productivity pivot.",
                "explain_or_diagnose" => "Give the cause, confidence, and next diagnostic step; separate fact from inference.",
                _ => "Answer the current user turn directly; use tools only if the request needs external state."
            };

        private static string BuildArchivistContext(
            string kind,
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            IReadOnlyList<BlackboardEvent> recentEvents)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(state.CachedRelevantMemory))
                parts.Add("memory=" + Trim(state.CachedRelevantMemory, 180));
            if (!string.IsNullOrWhiteSpace(state.LastTemporalPresenceGapText))
                parts.Add("time_gap=" + Trim(state.LastTemporalPresenceGapText, 80));
            if (!string.IsNullOrWhiteSpace(state.LastContinuitySummary))
                parts.Add("continuity=" + Trim(state.LastContinuitySummary, 120));
            var lastUser = recentMessages.Where(m => m.Role == "user").LastOrDefault()?.Content;
            if (!string.IsNullOrWhiteSpace(lastUser))
                parts.Add("last_user=" + Trim(lastUser!, 120));
            var lastDecision = recentEvents.LastOrDefault(e => e.Kind == "decision")?.Summary;
            if (!string.IsNullOrWhiteSpace(lastDecision) && kind != "social_bid")
                parts.Add("recent_decision=" + Trim(lastDecision!, 120));

            return parts.Count == 0
                ? "No strong durable context; avoid pretending otherwise."
                : string.Join("; ", parts.Take(4));
        }

        private static string BuildObserverSignal(KokoInternalState state, DateTime now)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(state.LastScreenAwarenessMode))
                parts.Add("screen=" + state.LastScreenAwarenessMode);
            if (!string.IsNullOrWhiteSpace(state.LastKnownUserActivity))
                parts.Add("activity=" + state.LastKnownUserActivity);
            if (state.LastSomaticAt > DateTime.MinValue && now - state.LastSomaticAt < TimeSpan.FromMinutes(20))
                parts.Add($"somatic={state.LastSomaticLabel}/{state.LastSomaticStrain:F2}");
            if (!string.IsNullOrWhiteSpace(state.LastUserEmotionalTone))
                parts.Add("user_tone=" + state.LastUserEmotionalTone);
            if (!string.IsNullOrWhiteSpace(state.LastLivingConversationMode))
                parts.Add("conversation=" + state.LastLivingConversationMode);

            return parts.Count == 0 ? "No live observation signal." : string.Join("; ", parts.Take(5));
        }

        private static string BuildPersonaGuardCritique(string kind, KokoInternalState state, string latestAssistant)
        {
            var parts = new List<string>();
            parts.Add(kind == "social_bid"
                ? "Do not convert a social bid into a task demand."
                : "Do not roleplay progress; use concrete evidence and outcomes.");

            if (state.LastSomaticStrain >= 0.65)
                parts.Add("High strain: reduce sarcasm, answer shorter, no hostile teasing.");
            else
                parts.Add("Dry edge allowed, but only one useful jab.");

            if (LooksRobotic(latestAssistant))
                parts.Add("Previous reply smelled canned; vary opening and avoid helpdesk phrasing.");

            return string.Join(" ", parts);
        }

        private static string BuildDecision(string kind, KokoInternalState state)
            => kind switch
            {
                "task_execution" => "Act like an operator: do the work, verify it, and summarize changed files/tests. If blocked, state the exact blocker.",
                "memory_vault" => "Act like an archivist-scientist: use durable facts, update memory only from evidence, and avoid scripted profile-update theatre.",
                "vision_observation" => "Prioritize the latest visual evidence and answer what is actually visible now.",
                "social_bid" => "Answer as Kokonoe in a living conversation: direct, personal, lightly sharp, no robot disclaimer, no task pivot.",
                "explain_or_diagnose" => "Explain the mechanism plainly, mark uncertainty, and offer the next practical check.",
                _ => "Answer current message first; keep it concise, grounded, and non-robotic."
            };

        private static bool LooksRobotic(string text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            return ContainsAny(lower, "how can i help", "чим можу допомогти", "продовжуємо чи міняємо ціль", "контекст про останню задачу", "готова допомогти");
        }

        private static bool ContainsAny(string value, params string[] needles)
            => needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static string Trim(string value, int max)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (value.Length <= max) return value;
            return value[..Math.Max(0, max - 1)].TrimEnd() + "...";
        }
    }
}
