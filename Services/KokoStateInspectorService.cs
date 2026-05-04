using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoStateInspectorSnapshot
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Mood { get; set; } = "";
        public float MoodScore { get; set; }
        public string UserTone { get; set; } = "";
        public string Emotion { get; set; } = "";
        public double EmotionIntensity { get; set; }
        public string Bond { get; set; } = "";
        public float ConnectionScore { get; set; }
        public KokoRelationshipState Relationship { get; set; } = new();
        public KokoSomaticSnapshot Somatic { get; set; } = new();
        public KokoSelfRegulationFrame SelfRegulation { get; set; } = new();
        public string LastInitiativeDecision { get; set; } = "";
        public string[] InitiativeLog { get; set; } = Array.Empty<string>();
        public string[] SelfRegulationLog { get; set; } = Array.Empty<string>();
        public string[] CuriosityQueue { get; set; } = Array.Empty<string>();
        public string[] InnerMonologues { get; set; } = Array.Empty<string>();
        public object[] TopFacts { get; set; } = Array.Empty<object>();
        public object[] RecentEpisodes { get; set; } = Array.Empty<object>();
    }

    public sealed class KokoStateInspectorService
    {
        public KokoStateInspectorSnapshot Capture(
            KokoInternalState state,
            KokoEmotionEngine emotion,
            KokoRelationshipEngine relationship,
            KokoMemoryEngine memory,
            KokoSomaticSnapshot somatic,
            KokoSelfRegulationFrame selfRegulation,
            string[] initiativeLog,
            string[] selfRegulationLog)
        {
            return new KokoStateInspectorSnapshot
            {
                CreatedAt = DateTime.Now,
                Mood = state.PersonalityDailyMood,
                MoodScore = state.MoodScore,
                UserTone = state.LastUserEmotionalTone,
                Emotion = emotion.Current.ToString(),
                EmotionIntensity = emotion.Data.Intensity,
                Bond = emotion.Bond.ToString(),
                ConnectionScore = emotion.ConnectionScore,
                Relationship = relationship.State,
                Somatic = somatic,
                SelfRegulation = selfRegulation,
                LastInitiativeDecision = state.LastInitiativeDecision,
                InitiativeLog = initiativeLog,
                SelfRegulationLog = selfRegulationLog,
                CuriosityQueue = state.CuriosityQueue.TakeLast(12).Reverse().ToArray(),
                InnerMonologues = state.InnerMonologues.TakeLast(12).Reverse().ToArray(),
                TopFacts = memory.GetTopFacts(12)
                    .Select(f => new
                    {
                        f.Id,
                        f.Content,
                        f.Category,
                        f.Importance,
                        f.ConfirmCount,
                        f.LastSeen
                    })
                    .Cast<object>()
                    .ToArray(),
                RecentEpisodes = memory.GetRecentEpisodes(8)
                    .Select(e => new
                    {
                        e.Id,
                        e.When,
                        e.Summary,
                        e.EmotionalTone,
                        e.Intensity
                    })
                    .Cast<object>()
                    .ToArray()
            };
        }

        public string ToJson(KokoStateInspectorSnapshot snapshot)
        {
            return JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        }

        public string ToMarkdown(KokoStateInspectorSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"updated: {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("tags: [kokonoe, inspector, state]");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Kokonoe State Inspector");
            sb.AppendLine();
            sb.AppendLine("## Core");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| mood | {snapshot.Mood} ({snapshot.MoodScore:F2}) |");
            sb.AppendLine($"| user tone | {snapshot.UserTone} |");
            sb.AppendLine($"| emotion | {snapshot.Emotion} ({snapshot.EmotionIntensity:F2}) |");
            sb.AppendLine($"| bond | {snapshot.Bond} / connection {snapshot.ConnectionScore:P0} |");
            sb.AppendLine($"| initiative | {Escape(snapshot.LastInitiativeDecision)} |");
            sb.AppendLine();

            sb.AppendLine("## Relationship");
            sb.AppendLine();
            sb.AppendLine("| Trust | Intimacy | Friction | Protect | Curiosity | Stability | Bond Score | Aftertaste |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---|");
            sb.AppendLine($"| {snapshot.Relationship.Trust:F2} | {snapshot.Relationship.Intimacy:F2} | {snapshot.Relationship.Friction:F2} | {snapshot.Relationship.Protectiveness:F2} | {snapshot.Relationship.Curiosity:F2} | {snapshot.Relationship.Stability:F2} | {snapshot.Relationship.BondScore:F2} | {Escape(snapshot.Relationship.LastAftertaste)} |");
            sb.AppendLine();

            sb.AppendLine("## Somatic");
            sb.AppendLine();
            sb.AppendLine("| State | BPM | Baseline | Delta | Strain | Calm | Volatility |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
            sb.AppendLine($"| {snapshot.Somatic.State} | {snapshot.Somatic.Bpm:F0} | {snapshot.Somatic.BaselineBpm:F0} | {snapshot.Somatic.BpmDelta:+0;-0;0} | {snapshot.Somatic.Strain:F2} | {snapshot.Somatic.Calm:F2} | {snapshot.Somatic.Volatility:F2} |");
            sb.AppendLine();
            sb.AppendLine($"Directive: `{Escape(snapshot.Somatic.BehaviorHint)}`");
            sb.AppendLine();

            sb.AppendLine("## Self Regulation");
            sb.AppendLine();
            sb.AppendLine("| Reaction | Regulation | Control | Containment | Drive | Irritation | Warmth | Distance | Exhaustion |");
            sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
            sb.AppendLine($"| {snapshot.SelfRegulation.Reaction} | {snapshot.SelfRegulation.Regulation} | {snapshot.SelfRegulation.Control:F2} | {snapshot.SelfRegulation.Containment:F2} | {snapshot.SelfRegulation.Drive:F2} | {snapshot.SelfRegulation.IrritationGate:F2} | {snapshot.SelfRegulation.WarmthLeak:F2} | {snapshot.SelfRegulation.SocialDistance:F2} | {snapshot.SelfRegulation.Exhaustion:F2} |");
            sb.AppendLine();
            sb.AppendLine($"Private thought: `{Escape(snapshot.SelfRegulation.PrivateThought)}`");
            sb.AppendLine();
            sb.AppendLine($"Directive: `{Escape(snapshot.SelfRegulation.BehaviorDirective)}`");
            sb.AppendLine();

            AppendList(sb, "Initiative Log", snapshot.InitiativeLog);
            AppendList(sb, "Self-Regulation Log", snapshot.SelfRegulationLog);
            AppendList(sb, "Curiosity Queue", snapshot.CuriosityQueue);
            AppendList(sb, "Inner Monologues", snapshot.InnerMonologues);

            sb.AppendLine("## Top Facts");
            sb.AppendLine();
            AppendJsonObjects(sb, snapshot.TopFacts);
            sb.AppendLine();

            sb.AppendLine("## Recent Episodes");
            sb.AppendLine();
            AppendJsonObjects(sb, snapshot.RecentEpisodes);

            return sb.ToString();
        }

        private static void AppendList(StringBuilder sb, string title, string[] items)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            if (items.Length == 0)
            {
                sb.AppendLine("- none");
                sb.AppendLine();
                return;
            }

            foreach (var item in items)
                sb.AppendLine($"- {Escape(item)}");
            sb.AppendLine();
        }

        private static void AppendJsonObjects(StringBuilder sb, object[] items)
        {
            if (items.Length == 0)
            {
                sb.AppendLine("- none");
                return;
            }

            foreach (var item in items)
            {
                var json = JsonConvert.SerializeObject(item, Formatting.None);
                sb.AppendLine($"- `{Escape(json)}`");
            }
        }

        private static string Escape(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? ""
                : text.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();
        }
    }
}
