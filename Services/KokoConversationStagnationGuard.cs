using System;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public static class KokoConversationStagnationGuard
    {
        public static bool Observe(KokoInternalState state, string? userText, DateTime now)
        {
            if (state == null) return false;
            var normalized = Normalize(userText);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            if (normalized.Length <= 18 &&
                string.Equals(normalized, state.LastShortUserPhraseNormalized, StringComparison.OrdinalIgnoreCase) &&
                now - state.LastShortUserPhraseAt < TimeSpan.FromMinutes(20))
            {
                state.ConversationLoopCount = Math.Min(10, state.ConversationLoopCount + 1);
            }
            else if (normalized.Length <= 18)
            {
                state.ConversationLoopCount = 1;
                state.LastShortUserPhraseNormalized = normalized;
            }
            else
            {
                state.ConversationLoopCount = 0;
                state.LastShortUserPhraseNormalized = "";
            }

            state.LastShortUserPhraseAt = now;

            if (state.ConversationLoopCount < 3 || now - state.LastForcedTopicAt < TimeSpan.FromMinutes(10))
                return false;

            var thought = state.PendingThoughts
                .LastOrDefault(t => !string.IsNullOrWhiteSpace(t) && t.Length > 16);
            if (string.IsNullOrWhiteSpace(thought))
                return false;

            state.LastForcedTopicAt = now;
            state.LastForcedTopic = thought.Trim();
            return true;
        }

        public static string BuildPromptBlock(KokoInternalState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.LastForcedTopic))
                return "";
            if (DateTime.Now - state.LastForcedTopicAt > TimeSpan.FromMinutes(12))
                return "";

            return "CONVERSATION STAGNATION GUARD\n" +
                   $"Loop count: {state.ConversationLoopCount}\n" +
                   $"Forced topic: {state.LastForcedTopic}\n" +
                   "Instruction: stop reacting to the repeated short phrase. Pivot to the forced topic naturally and make it useful.";
        }

        private static string Normalize(string? text)
        {
            var value = (text ?? "").Trim().ToLowerInvariant();
            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                var end = value.IndexOf(']');
                if (end >= 0 && end < value.Length - 1)
                    value = value[(end + 1)..].Trim();
            }

            var semantic = new string(value.Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)).ToArray())
                .Trim();
            if (!string.IsNullOrWhiteSpace(semantic))
                return semantic;

            var punctuation = new string(value.Where(ch => !char.IsWhiteSpace(ch)).Take(8).ToArray());
            return punctuation.Length <= 8 ? punctuation : "";
        }
    }
}
