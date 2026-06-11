using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoEmotionalMemoryFrame
    {
        public string SessionMood { get; set; } = "neutral";
        public string ExitStyle { get; set; } = "unknown";
        public string MannersState { get; set; } = "neutral";
        public double GrudgeScore { get; set; }
        public string VibeSummary { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoEmotionalMemoryService
    {
        public void ObserveUserMessage(
            KokoInternalState state,
            string? userText,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            DateTime now)
        {
            var frame = BuildFrame(state, recentMessages, now, userText);
            state.EmotionalSessionMood = frame.SessionMood;
            state.EmotionalMannersState = frame.MannersState;
            state.EmotionalGrudgeScore = frame.GrudgeScore;
            state.LastEmotionalMemoryAt = now;
            Remember(state, frame.TraceLine);
            KokoSystemLog.Write("EMOTIONAL-MEMORY", frame.TraceLine);
        }

        public void ObserveAssistantReply(
            KokoInternalState state,
            string? reply,
            DateTime now)
        {
            if (string.IsNullOrWhiteSpace(reply))
                return;

            var text = reply.ToLowerInvariant();
            if (ContainsAny(text, "sorry", "вибач", "пробач"))
                state.EmotionalGrudgeScore = Math.Max(0, state.EmotionalGrudgeScore - 0.08);
            if (ContainsAny(text, "as an ai", "how can i help", "чим я можу допомогти"))
                state.EmotionalGrudgeScore = Math.Min(1, state.EmotionalGrudgeScore + 0.18);

            state.LastEmotionalMemoryAt = now;
        }

        public KokoEmotionalMemoryFrame BuildFrame(
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            DateTime now,
            string? currentUserText = null)
        {
            var recent = recentMessages.OrderBy(m => m.Timestamp).TakeLast(30).ToList();
            var tail = string.Join("\n", recent.TakeLast(8).Select(m => $"{m.Role}: {m.Content}"));
            var lowerTail = tail.ToLowerInvariant();
            var current = (currentUserText ?? "").ToLowerInvariant();
            var last = recent.LastOrDefault();
            var lastUser = recent.LastOrDefault(m => m.Role == "user");
            var gapMinutes = last == null ? 0 : Math.Max(0, (now - last.Timestamp).TotalMinutes);

            var mood = InferSessionMood(lowerTail, current, state);
            var exitStyle = InferExitStyle(recent, gapMinutes);
            var manners = InferMannersState(exitStyle, lowerTail, current, state);
            var grudge = UpdateGrudge(state.EmotionalGrudgeScore, exitStyle, manners, current);

            var frame = new KokoEmotionalMemoryFrame
            {
                SessionMood = mood,
                ExitStyle = exitStyle,
                MannersState = manners,
                GrudgeScore = grudge,
                VibeSummary = BuildVibeSummary(recent, mood, exitStyle, manners, gapMinutes),
            };

            frame.PromptBlock = BuildPromptBlock(frame, state, recent, now, lastUser);
            frame.TraceLine = $"[{now:HH:mm}] mood={mood}; exit={exitStyle}; manners={manners}; grudge={grudge:F2}; vibe={Trim(frame.VibeSummary, 160)}";
            return frame;
        }

        public string BuildPromptBlock(
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            DateTime now,
            string? currentUserText = null)
            => BuildFrame(state, recentMessages, now, currentUserText).PromptBlock;

        public string BuildRawHydrationBlock(
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> recentMessages,
            DateTime now,
            int take = 30)
        {
            var recent = recentMessages.OrderBy(m => m.Timestamp).TakeLast(Math.Clamp(take, 8, 40)).ToList();
            if (recent.Count == 0)
                return "RAW CHAT HYDRATION\nnone";

            var sb = new StringBuilder();
            sb.AppendLine("RAW CHAT HYDRATION");
            sb.AppendLine("Use this as short-term conversational memory. It has priority over vague Obsidian summaries for the last 24 hours.");
            sb.AppendLine("Do not invent generic summaries like 'we discussed work' if the raw turns do not support it.");
            sb.AppendLine("If visual/image context appears here or in LastVisualMemoryAnchor, keep it alive instead of saying there was no photo.");
            if (!string.IsNullOrWhiteSpace(state.LastVisualMemoryAnchor) &&
                now - state.LastVisualMemoryAnchorAt < TimeSpan.FromHours(24))
                sb.AppendLine($"visual_anchor: {Trim(state.LastVisualMemoryAnchor, 280)}");
            sb.AppendLine("recent_raw_turns:");
            foreach (var msg in recent)
                sb.AppendLine($"- [{msg.Timestamp:MM-dd HH:mm}] {msg.Role}: {Trim(msg.Content, 260)}");

            var block = sb.ToString().TrimEnd();
            state.LastRawContextHydration = Trim(block, 1800);
            state.LastRawContextHydrationAt = now;
            return block;
        }

        public void RememberVisualAnchor(KokoInternalState state, string? summary, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return;

            state.LastVisualMemoryAnchor = Trim(summary, 500);
            state.LastVisualMemoryAnchorAt = now;
            Remember(state, $"{now:MM-dd HH:mm} visual-anchor: {Trim(summary, 160)}");
        }

        private static string InferSessionMood(string tail, string current, KokoInternalState state)
        {
            if (ContainsAny(current, "терміново", "срочно", "panic", "не працює", "error") ||
                ContainsAny(tail, "panic", "терміново", "срочно", "не працює"))
                return "high_efficiency";
            if (ContainsAny(current, "миле", "милий", "люблю", "про тебе", "про мене", "заігру", "флірт") ||
                ContainsAny(tail, "миле", "милий", "люблю", "про тебе", "про мене", "заігру", "флірт"))
                return "charged_playful";
            if (ContainsAny(current, "ок", "ясно", "угу", "неважно", "байдуже", "відвали") ||
                LooksShortCold(current))
                return "cold_or_dismissive";
            if (state.PersonalityInCrisis) return "protective";
            return state.EmotionalSessionMood == "unknown" ? "neutral" : string.IsNullOrWhiteSpace(state.EmotionalSessionMood) ? "neutral" : state.EmotionalSessionMood;
        }

        private static string InferExitStyle(IReadOnlyList<ChatRepository.ChatMessage> recent, double gapMinutes)
        {
            if (recent.Count == 0) return "unknown";
            if (gapMinutes < 30) return "still_present";

            var last = recent.Last();
            var lastText = (last.Content ?? "").ToLowerInvariant();
            var lastUser = recent.LastOrDefault(m => m.Role == "user");
            var lastUserText = (lastUser?.Content ?? "").ToLowerInvariant();

            if (ContainsAny(lastText, "бувай", "пока", "до завтра", "добраніч", "спати", "sleep", "bye") ||
                ContainsAny(lastUserText, "бувай", "пока", "до завтра", "добраніч", "спати", "sleep", "bye"))
                return "clean_exit";
            if (ContainsAny(lastText, "відвали", "заткнись", "нах", "іди", "не хочу з тобою") ||
                ContainsAny(lastUserText, "відвали", "заткнись", "нах", "іди", "не хочу з тобою"))
                return "conflict_exit";
            if (last.Role == "user" && gapMinutes >= 45)
                return "abrupt_user_disappearance";
            if (last.Role == "assistant" && gapMinutes >= 90)
                return "left_on_kokonoe_reply";
            return "ordinary_pause";
        }

        private static string InferMannersState(string exitStyle, string tail, string current, KokoInternalState state)
        {
            if (ContainsAny(current, "вибач", "пробач", "сорі", "sorry"))
                return "repaired";
            if (exitStyle is "abrupt_user_disappearance" or "conflict_exit")
                return "rude_exit_remembered";
            if (exitStyle == "clean_exit")
                return "clean";
            if (ContainsAny(tail, "дякую", "спасибі", "норм", "добре"))
                return "warm_trace";
            return state.EmotionalMannersState == "unknown" ? "neutral" : string.IsNullOrWhiteSpace(state.EmotionalMannersState) ? "neutral" : state.EmotionalMannersState;
        }

        private static double UpdateGrudge(double previous, string exitStyle, string manners, string current)
        {
            var value = Math.Clamp(previous * 0.96, 0, 1);
            if (exitStyle == "abrupt_user_disappearance") value += 0.16;
            if (exitStyle == "conflict_exit") value += 0.28;
            if (manners == "repaired") value -= 0.22;
            if (ContainsAny(current, "дякую", "вибач", "пробач", "sorry")) value -= 0.08;
            return Math.Clamp(value, 0, 1);
        }

        private static string BuildVibeSummary(
            IReadOnlyList<ChatRepository.ChatMessage> recent,
            string mood,
            string exitStyle,
            string manners,
            double gapMinutes)
        {
            var lastConcrete = recent
                .Where(m => !string.IsNullOrWhiteSpace(m.Content) && m.Content.Trim().Length > 3)
                .TakeLast(5)
                .Select(m => Trim(m.Content, 80))
                .ToArray();
            var gap = gapMinutes < 30 ? "current" : gapMinutes < 240 ? "noticeable absence" : "long absence";
            return $"{gap}; mood={mood}; exit={exitStyle}; manners={manners}; tail={string.Join(" / ", lastConcrete)}";
        }

        private static string BuildPromptBlock(
            KokoEmotionalMemoryFrame frame,
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> recent,
            DateTime now,
            ChatRepository.ChatMessage? lastUser)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EMOTIONAL MEMORY / MANNERS");
            sb.AppendLine($"session_mood: {frame.SessionMood}");
            sb.AppendLine($"exit_style: {frame.ExitStyle}");
            sb.AppendLine($"manners_state: {frame.MannersState}");
            sb.AppendLine($"grudge_score: {frame.GrudgeScore:F2}");
            sb.AppendLine($"vibe_snapshot: {frame.VibeSummary}");
            if (lastUser != null)
                sb.AppendLine($"last_user_raw: [{lastUser.Timestamp:MM-dd HH:mm}] {Trim(lastUser.Content, 180)}");
            if (!string.IsNullOrWhiteSpace(state.LastVisualMemoryAnchor) &&
                now - state.LastVisualMemoryAnchorAt < TimeSpan.FromHours(24))
                sb.AppendLine($"visual_memory_anchor: {Trim(state.LastVisualMemoryAnchor, 260)}");
            sb.AppendLine("rules:");
            sb.AppendLine("- Carry this mood across sessions; do not reset to a cheerful welcome if the last exit was rude or unresolved.");
            sb.AppendLine("- If the user vanished without closing, Kokonoe may be colder or teasing, but do not overact.");
            sb.AppendLine("- Use emotional time in visible dialogue: 'gone for ages', 'barely gone', 'finally back'. Do not say exact pause metrics.");
            sb.AppendLine("- No stage directions, no asterisk roleplay, no generic companion cliches.");
            sb.AppendLine("- For the last 24 hours, raw chat history beats Obsidian summaries for conversational flow.");
            return sb.ToString();
        }

        private static bool LooksShortCold(string lower)
        {
            lower = (lower ?? "").Trim();
            if (lower.Length == 0 || lower.Length > 24) return false;
            return lower is "ок" or "окей" or "ясно" or "угу" or "ага" or "ні" or "нет" or "bye" or "k";
        }

        private static void Remember(KokoInternalState state, string trace)
        {
            if (string.IsNullOrWhiteSpace(trace)) return;
            state.EmotionalMemoryTrace.Add(trace);
            if (state.EmotionalMemoryTrace.Count > 40)
                state.EmotionalMemoryTrace.RemoveRange(0, state.EmotionalMemoryTrace.Count - 40);
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }
    }
}
