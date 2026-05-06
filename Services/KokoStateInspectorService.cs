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
        public string LastPresenceSummary { get; set; } = "";
        public string LastPresenceSituation { get; set; } = "";
        public string LastInternalDaySummary { get; set; } = "";
        public string LastInternalDayPhase { get; set; } = "";
        public string LastInternalDayFocus { get; set; } = "";
        public string LastAutonomyDecision { get; set; } = "";
        public string[] InitiativeLog { get; set; } = Array.Empty<string>();
        public string[] SelfRegulationLog { get; set; } = Array.Empty<string>();
        public string[] PresenceTrace { get; set; } = Array.Empty<string>();
        public string[] InternalDayTrace { get; set; } = Array.Empty<string>();
        public string[] AutonomyDecisionLog { get; set; } = Array.Empty<string>();
        public object[] RelationshipEvents { get; set; } = Array.Empty<object>();
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
                LastPresenceSummary = state.LastPresenceSummary,
                LastPresenceSituation = state.LastPresenceSituation,
                LastInternalDaySummary = state.LastInternalDaySummary,
                LastInternalDayPhase = state.LastInternalDayPhase,
                LastInternalDayFocus = state.LastInternalDayFocus,
                LastAutonomyDecision = state.LastAutonomyDecision,
                InitiativeLog = initiativeLog,
                SelfRegulationLog = selfRegulationLog,
                PresenceTrace = state.PresenceTrace.TakeLast(10).Reverse().ToArray(),
                InternalDayTrace = state.InternalDayTrace.TakeLast(10).Reverse().ToArray(),
                AutonomyDecisionLog = state.AutonomyDecisionLog.TakeLast(10).Reverse().ToArray(),
                RelationshipEvents = relationship.State.RecentEvents
                    .TakeLast(8)
                    .Reverse()
                    .Select(e => new
                    {
                        e.When,
                        e.Kind,
                        e.Reason,
                        e.Aftertaste,
                        e.Trust,
                        e.Intimacy,
                        e.Friction,
                        e.Protectiveness
                    })
                    .Cast<object>()
                    .ToArray(),
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
            sb.AppendLine("# Інспектор стану Коконое");
            sb.AppendLine();
            sb.AppendLine("## Ядро");
            sb.AppendLine();
            sb.AppendLine("| Поле | Значення |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| настрій | {snapshot.Mood} ({snapshot.MoodScore:F2}) |");
            sb.AppendLine($"| тон користувача | {snapshot.UserTone} |");
            sb.AppendLine($"| емоція | {snapshot.Emotion} ({snapshot.EmotionIntensity:F2}) |");
            sb.AppendLine($"| зв'язок | {snapshot.Bond} / близькість {snapshot.ConnectionScore:P0} |");
            sb.AppendLine($"| ініціатива | {Escape(snapshot.LastInitiativeDecision)} |");
            sb.AppendLine($"| автономність | {Escape(snapshot.LastAutonomyDecision)} |");
            sb.AppendLine($"| presence | {Escape(snapshot.LastPresenceSummary)} |");
            sb.AppendLine($"| внутрішній день | {Escape(snapshot.LastInternalDaySummary)} |");
            sb.AppendLine();

            sb.AppendLine("## Присутність і день");
            sb.AppendLine();
            sb.AppendLine("| Presence | Фаза дня | Фокус |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| {Escape(snapshot.LastPresenceSituation)} | {Escape(snapshot.LastInternalDayPhase)} | {Escape(snapshot.LastInternalDayFocus)} |");
            sb.AppendLine();
            AppendList(sb, "Сліди присутності", snapshot.PresenceTrace);
            AppendList(sb, "Сліди внутрішнього дня", snapshot.InternalDayTrace);
            AppendList(sb, "Журнал автономності", snapshot.AutonomyDecisionLog);

            sb.AppendLine("## Стосунок");
            sb.AppendLine();
            sb.AppendLine("| Довіра | Близькість | Тертя | Захист | Цікавість | Стабільність | Оцінка зв'язку | Післясмак |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---|");
            sb.AppendLine($"| {snapshot.Relationship.Trust:F2} | {snapshot.Relationship.Intimacy:F2} | {snapshot.Relationship.Friction:F2} | {snapshot.Relationship.Protectiveness:F2} | {snapshot.Relationship.Curiosity:F2} | {snapshot.Relationship.Stability:F2} | {snapshot.Relationship.BondScore:F2} | {Escape(snapshot.Relationship.LastAftertaste)} |");
            sb.AppendLine();
            sb.AppendLine("### Події стосунку");
            sb.AppendLine();
            AppendJsonObjects(sb, snapshot.RelationshipEvents);
            sb.AppendLine();

            sb.AppendLine("## Соматика");
            sb.AppendLine();
            sb.AppendLine("| Стан | BPM | База | Зміна | Strain | Calm | Volatility |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
            sb.AppendLine($"| {snapshot.Somatic.State} | {snapshot.Somatic.Bpm:F0} | {snapshot.Somatic.BaselineBpm:F0} | {snapshot.Somatic.BpmDelta:+0;-0;0} | {snapshot.Somatic.Strain:F2} | {snapshot.Somatic.Calm:F2} | {snapshot.Somatic.Volatility:F2} |");
            sb.AppendLine();
            sb.AppendLine($"Директива: `{Escape(snapshot.Somatic.BehaviorHint)}`");
            sb.AppendLine();

            sb.AppendLine("## Саморегуляція");
            sb.AppendLine();
            sb.AppendLine("| Реакція | Регуляція | Контроль | Стримування | Імпульс | Роздратування | Тепло | Дистанція | Виснаження |");
            sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
            sb.AppendLine($"| {CodeLabel(snapshot.SelfRegulation.Reaction)} | {CodeLabel(snapshot.SelfRegulation.Regulation)} | {snapshot.SelfRegulation.Control:F2} | {snapshot.SelfRegulation.Containment:F2} | {snapshot.SelfRegulation.Drive:F2} | {snapshot.SelfRegulation.IrritationGate:F2} | {snapshot.SelfRegulation.WarmthLeak:F2} | {snapshot.SelfRegulation.SocialDistance:F2} | {snapshot.SelfRegulation.Exhaustion:F2} |");
            sb.AppendLine();
            sb.AppendLine($"Внутрішня думка: `{Escape(snapshot.SelfRegulation.PrivateThought)}`");
            sb.AppendLine();
            sb.AppendLine($"Директива: `{Escape(snapshot.SelfRegulation.BehaviorDirective)}`");
            sb.AppendLine();

            AppendList(sb, "Журнал ініціативи", snapshot.InitiativeLog);
            AppendList(sb, "Журнал саморегуляції", snapshot.SelfRegulationLog);
            AppendList(sb, "Черга цікавості", snapshot.CuriosityQueue);
            AppendList(sb, "Внутрішні монологи", snapshot.InnerMonologues);

            sb.AppendLine("## Головні факти");
            sb.AppendLine();
            AppendJsonObjects(sb, snapshot.TopFacts);
            sb.AppendLine();

            sb.AppendLine("## Останні епізоди");
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
                sb.AppendLine("- немає");
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
                sb.AppendLine("- немає");
                return;
            }

            foreach (var item in items)
            {
                var json = JsonConvert.SerializeObject(item, Formatting.None);
                sb.AppendLine($"- `{Escape(json)}`");
            }
        }

        private static string CodeLabel(string code) => code switch
        {
            "protective_override" => "захисне перевизначення",
            "pulse_spike" => "стрибок пульсу",
            "anger_contained" => "стримане роздратування",
            "combat_focus" => "бойовий фокус",
            "pressure_rise" => "зростання тиску",
            "low_power" => "низький заряд",
            "recovered_calm" => "повернення спокою",
            "steady_calm" => "стабільний спокій",
            "stable_loop" => "стабільний цикл",
            "clean_focus" => "чистий фокус",
            "unknown_body" => "невідомий тілесний сигнал",
            "protect" => "захист",
            "clamp" => "затиск",
            "contain" => "стримування",
            "focus" => "фокус",
            "compress" => "стиснення",
            "conserve" => "збереження ресурсу",
            "release" => "відпускання",
            "baseline" => "базовий режим",
            _ => code
        };

        private static string Escape(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? ""
                : text.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();
        }
    }
}
