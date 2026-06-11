using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KokonoeAssistant.Services
{
    public sealed class KokoSocialFrame
    {
        public string Subtext { get; set; } = "neutral";
        public double Playfulness { get; set; }
        public double Flirt { get; set; }
        public double Urgency { get; set; }
        public double Boredom { get; set; }
        public double Affection { get; set; }
        public double Dismissiveness { get; set; }
        public double Stress { get; set; }
        public double SarcasmLevel { get; set; } = 0.45;
        public double EmpathyLevel { get; set; } = 0.35;
        public double SeriousnessLevel { get; set; } = 0.55;
        public string MoodMirror { get; set; } = "dry_neutral";
        public string ResponseDirective { get; set; } = "";
        public string Reason { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoSocialEngine
    {
        public KokoSocialFrame Analyze(
            string? userText,
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage>? recentMessages,
            KokoWearableState? wearable,
            DateTime now)
        {
            userText ??= "";
            var lower = userText.ToLowerInvariant();
            var recent = string.Join("\n", (recentMessages ?? Array.Empty<ChatRepository.ChatMessage>())
                .TakeLast(8)
                .Select(m => $"{m.Role}: {m.Content}"));
            var recentLower = recent.ToLowerInvariant();

            var frame = new KokoSocialFrame
            {
                Playfulness = Score(lower, recentLower,
                    "хех", "ахах", "лол", "жарт", "дурниц", "прикол", "пошаліт", "tease", "lol", "haha", "nonsense"),
                Flirt = Score(lower, recentLower,
                    "заігру", "флірт", "милий", "мила", "миле", "люблю", "про нас", "про тебе", "про мене", "kiss", "flirt", "cute", "miss you"),
                Urgency = Score(lower, recentLower,
                    "терміново", "срочно", "urgent", "critical", "зараз", "не працює", "злам", "помилка", "error", "panic"),
                Boredom = Score(lower, recentLower,
                    "нудно", "просто поговор", "дурниц", "нічого", "bored", "just talk", "whatever"),
                Affection = Score(lower, recentLower,
                    "люблю", "сумую", "милий", "миле", "обій", "дякую", "love", "miss", "nice"),
                Dismissiveness = ScoreDismissiveness(lower, recentLower),
            };

            var wearableStress = wearable?.IsFresh(DateTime.UtcNow) == true
                ? Math.Clamp((wearable.LiveStressScore / 100.0) + Math.Max(0, wearable.BpmDelta) / 45.0, 0, 1)
                : 0;
            frame.Stress = Math.Clamp(Math.Max(frame.Urgency, wearableStress), 0, 1);

            frame.Subtext = PickSubtext(frame);
            ApplyFlux(frame, state, now);
            frame.ResponseDirective = BuildDirective(frame);
            frame.Reason = BuildReason(frame, wearable);
            frame.PromptBlock = BuildPromptBlock(frame);
            frame.TraceLine = $"[{now:HH:mm}] social={frame.Subtext}; flux=s{frame.SarcasmLevel:F2}/e{frame.EmpathyLevel:F2}/r{frame.SeriousnessLevel:F2}; reason={frame.Reason}";
            KokoSystemLog.Write("SOCIAL", frame.TraceLine);
            return frame;
        }

        private static void ApplyFlux(KokoSocialFrame frame, KokoInternalState state, DateTime now)
        {
            var sarcasm = 0.45;
            var empathy = 0.35;
            var serious = 0.55;

            if (frame.Stress >= 0.70 || state.PersonalityInCrisis)
            {
                sarcasm = 0.10;
                empathy = 0.72;
                serious = 1.00;
                frame.MoodMirror = "protective_serious";
            }
            else if (frame.Flirt >= 0.55 || frame.Affection >= 0.32)
            {
                sarcasm = 0.72;
                empathy = 0.58;
                serious = 0.34;
                frame.MoodMirror = "symmetrically_sharp";
            }
            else if (frame.Playfulness >= 0.55 || frame.Boredom >= 0.55)
            {
                sarcasm = 0.82;
                empathy = 0.32;
                serious = 0.28;
                frame.MoodMirror = "annoyingly_playful";
            }
            else if (frame.Dismissiveness >= 0.55)
            {
                sarcasm = 0.76;
                empathy = 0.18;
                serious = 0.58;
                frame.MoodMirror = "cold_mirror";
            }

            if (now.Hour is >= 0 and < 5 && frame.Stress < 0.65)
            {
                sarcasm = Math.Min(1.0, sarcasm + 0.08);
                serious = Math.Max(0.20, serious - 0.08);
            }

            if (state.PersonalityDailyMood == "protective")
            {
                empathy = Math.Max(empathy, 0.62);
                serious = Math.Max(serious, 0.76);
                sarcasm = Math.Min(sarcasm, 0.42);
            }

            frame.SarcasmLevel = Math.Clamp(sarcasm, 0, 1);
            frame.EmpathyLevel = Math.Clamp(empathy, 0, 1);
            frame.SeriousnessLevel = Math.Clamp(serious, 0, 1);
        }

        private static string PickSubtext(KokoSocialFrame frame)
        {
            if (frame.Stress >= 0.70) return "urgent_or_stressed";
            if (frame.Flirt >= 0.55) return "flirting_or_affectionate_teasing";
            if (frame.Affection >= 0.32) return "soft_affection";
            if (frame.Dismissiveness >= 0.55) return "dismissive_or_cold";
            if (frame.Playfulness >= 0.55) return "playful_teasing";
            if (frame.Boredom >= 0.55) return "boredom_time_killing";
            return "neutral";
        }

        private static string BuildDirective(KokoSocialFrame frame) => frame.Subtext switch
        {
            "urgent_or_stressed" => "Drop theatrical contempt. Be blunt, protective, and practical. One recovery nudge max if useful.",
            "flirting_or_affectionate_teasing" => "Match play with sharp Kokonoe restraint. No maid/waifu softness, no productivity pivot.",
            "soft_affection" => "Answer the affection directly with guarded warmth and one dry edge max.",
            "dismissive_or_cold" => "Mirror the chill with restrained irritation. Do not chase approval; answer only what is useful.",
            "playful_teasing" => "Banter is allowed. Keep it intelligent and short; do not become a clown routine.",
            "boredom_time_killing" => "Tease the boredom lightly, then offer an interesting direction or make one character-driven assumption.",
            _ => "Use ordinary Kokonoe directness. No service-bot phrasing."
        };

        private static string BuildReason(KokoSocialFrame frame, KokoWearableState? wearable)
        {
            var parts = new List<string>
            {
                $"play={frame.Playfulness:F2}",
                $"flirt={frame.Flirt:F2}",
                $"urgency={frame.Urgency:F2}",
                $"boredom={frame.Boredom:F2}",
                $"affection={frame.Affection:F2}",
                $"dismissive={frame.Dismissiveness:F2}"
            };
            if (wearable?.IsFresh(DateTime.UtcNow) == true)
                parts.Add($"wearable_stress={wearable.LiveStressScore}/100 delta={wearable.BpmDelta:+0;-0;0}");
            return string.Join("; ", parts);
        }

        private static string BuildPromptBlock(KokoSocialFrame frame)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SOCIAL SUBTEXT / PERSONALITY FLUX");
            sb.AppendLine($"subtext: {frame.Subtext}");
            sb.AppendLine($"mood_mirror: {frame.MoodMirror}");
            sb.AppendLine($"sarcasm_level: {frame.SarcasmLevel:F2}");
            sb.AppendLine($"empathy_level: {frame.EmpathyLevel:F2}");
            sb.AppendLine($"seriousness_level: {frame.SeriousnessLevel:F2}");
            sb.AppendLine($"directive: {frame.ResponseDirective}");
            sb.AppendLine("rules:");
            sb.AppendLine("- Social intuition changes tone, not facts.");
            sb.AppendLine("- Flirting/teasing remains Kokonoe: sharp, guarded, scientist-demon energy; never generic waifu/maid behavior.");
            sb.AppendLine("- High stress overrides sarcasm.");
            sb.AppendLine("- Cold or dismissive user tone should be mirrored with controlled distance, not needy clarification loops.");
            sb.AppendLine("- If the user is vague/playful, infer the vibe and respond in character instead of stalling with 'be specific'.");
            sb.AppendLine("- Never use 'as an AI', 'how can I help', or service-bot sympathy filler.");
            return sb.ToString();
        }

        private static double Score(string lower, string recentLower, params string[] needles)
        {
            var score = 0.0;
            foreach (var n in needles)
            {
                if (lower.Contains(n, StringComparison.OrdinalIgnoreCase)) score += 0.34;
                if (recentLower.Contains(n, StringComparison.OrdinalIgnoreCase)) score += 0.10;
            }

            if (Regex.IsMatch(lower, @"[!?]{2,}|\.{2,}|…")) score += 0.08;
            return Math.Clamp(score, 0, 1);
        }

        private static double ScoreDismissiveness(string lower, string recentLower)
        {
            var score = Score(lower, recentLower,
                "ок", "окей", "ясно", "угу", "ага", "байдуже", "неважно", "відвали",
                "whatever", "fine", "k", "meh");
            var compact = new string((lower ?? "").Where(char.IsLetterOrDigit).ToArray());
            if (compact is "ок" or "окей" or "ясно" or "угу" or "ага" or "k" or "meh")
                score = Math.Max(score, 0.70);
            return Math.Clamp(score, 0, 1);
        }
    }
}
