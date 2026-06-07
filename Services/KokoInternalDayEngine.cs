using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoInternalDayFrame
    {
        public string Phase { get; set; } = "unknown";
        public string Focus { get; set; } = "";
        public string EnergyPolicy { get; set; } = "";
        public string InterruptionPolicy { get; set; } = "";
        public string ToneDirective { get; set; } = "";
        public string Ritual { get; set; } = "";
        public int InitiativeBias { get; set; }
        public bool ShouldPreferSilence { get; set; }
        public bool ShouldWriteVaultStatus { get; set; }
        public string SummaryUk { get; set; } = "";
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoInternalDayEngine
    {
        public KokoInternalDayFrame Evaluate(
            KokoInternalState state,
            KokoPresenceFrame presence,
            KokoSomaticSnapshot somatic,
            DateTime now,
            int autonomyLevel)
        {
            now = EnsureLocalTime(now);
            autonomyLevel = Math.Clamp(autonomyLevel, 0, 3);
            var phase = GetPhase(now);
            var frame = new KokoInternalDayFrame
            {
                Phase = phase,
                Focus = BuildFocus(phase, presence, somatic),
                EnergyPolicy = BuildEnergyPolicy(phase, somatic),
                InterruptionPolicy = BuildInterruptionPolicy(phase, presence, autonomyLevel),
                ToneDirective = BuildToneDirective(phase, state, presence, somatic),
                Ritual = BuildRitual(phase),
                InitiativeBias = BuildInitiativeBias(phase, presence, somatic, autonomyLevel),
                ShouldPreferSilence = ShouldPreferSilence(phase, presence, somatic, autonomyLevel)
            };

            frame.SummaryUk = $"{PhaseLabel(phase)}: {frame.Focus} {frame.InterruptionPolicy}";
            frame.ShouldWriteVaultStatus =
                state.LastInternalDayPhase != phase ||
                now - state.LastInternalDayVaultAt > TimeSpan.FromHours(2);
            frame.PromptBlock = BuildPromptBlock(frame, now);
            return frame;
        }

        public void Record(KokoInternalState state, KokoInternalDayFrame frame, DateTime now)
        {
            now = EnsureLocalTime(now);
            state.LastInternalDayAt = now;
            state.LastInternalDayPhase = frame.Phase;
            state.LastInternalDaySummary = frame.SummaryUk;
            state.LastInternalDayFocus = frame.Focus;
            state.InternalDayTrace.Add($"{now:dd.MM HH:mm} {frame.Phase}: {frame.Focus}");
            if (state.InternalDayTrace.Count > 40)
                state.InternalDayTrace.RemoveRange(0, state.InternalDayTrace.Count - 40);
        }

        public string BuildVaultStatus(KokoInternalState state, KokoInternalDayFrame frame, KokoPresenceFrame presence, DateTime now)
        {
            now = EnsureLocalTime(now);
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"updated: {now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("tags: [kokonoe, state, internal-day]");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Внутрішній день Коконое");
            sb.AppendLine();
            sb.AppendLine("| Поле | Значення |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| Фаза | {PhaseLabel(frame.Phase)} |");
            sb.AppendLine($"| Фокус | {Escape(frame.Focus)} |");
            sb.AppendLine($"| Енергія | {Escape(frame.EnergyPolicy)} |");
            sb.AppendLine($"| Втручання | {Escape(frame.InterruptionPolicy)} |");
            sb.AppendLine($"| Тон | {Escape(frame.ToneDirective)} |");
            sb.AppendLine($"| Ритуал | {Escape(frame.Ritual)} |");
            sb.AppendLine($"| Presence | {Escape(presence.SummaryUk)} |");
            sb.AppendLine($"| Остання ініціатива | {Escape(state.LastInitiativeDecision)} |");
            sb.AppendLine();

            sb.AppendLine("## Сліди дня");
            sb.AppendLine();
            if (state.InternalDayTrace.Count == 0)
            {
                sb.AppendLine("- немає");
            }
            else
            {
                foreach (var item in state.InternalDayTrace.TakeLast(12))
                    sb.AppendLine($"- {Escape(item)}");
            }
            sb.AppendLine();

            sb.AppendLine("## Правила");
            sb.AppendLine();
            sb.AppendLine("- Перед відповіддю звіряти поточний час і presence-стан.");
            sb.AppendLine("- Не повторювати стару дію користувачу, якщо він уже повернувся або прокинувся.");
            sb.AppendLine("- Писати першою тільки коли є причина: намір прострочений, тиша значуща, стан проєкту потребує уваги.");
            return sb.ToString();
        }

        public string BuildDebugBlock(KokoInternalState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== INTERNAL DAY ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(state.LastInternalDaySummary)
                ? "last_internal_day=none"
                : $"last_internal_day={state.LastInternalDaySummary}");
            sb.AppendLine($"phase={state.LastInternalDayPhase}");
            sb.AppendLine($"focus={state.LastInternalDayFocus}");
            if (state.InternalDayTrace.Count > 0)
            {
                sb.AppendLine("recent_day_trace:");
                foreach (var item in state.InternalDayTrace.TakeLast(5))
                    sb.AppendLine($"- {item}");
            }
            sb.AppendLine("rule: internal day changes tone and initiative; do not recite this block.");
            return sb.ToString();
        }

        private static string GetPhase(DateTime now)
        {
            if (now.Hour is >= 5 and < 9) return "morning_boot";
            if (now.Hour is >= 9 and < 13) return "work_ramp";
            if (now.Hour is >= 13 and < 18) return "deep_day";
            if (now.Hour is >= 18 and < 22) return "evening_review";
            if (now.Hour is >= 22 or < 2) return "night_watch";
            return "low_power_night";
        }

        private static DateTime EnsureLocalTime(DateTime now)
            => now.Kind == DateTimeKind.Utc ? now.ToLocalTime() : now;

        private static string BuildFocus(string phase, KokoPresenceFrame presence, KokoSomaticSnapshot somatic)
        {
            if (presence.SituationKind.Contains("intent", StringComparison.OrdinalIgnoreCase))
                return "тримати незавершену дію користувача в голові";
            if (presence.SituationKind.Contains("silence", StringComparison.OrdinalIgnoreCase))
                return "оцінити тишу без театру і без шаблонних перевірок";
            if (somatic.State is "wired" or "strained")
                return "стиснути імпульси і відповідати коротко";

            return phase switch
            {
                "morning_boot" => "зібрати стан дня і не навантажувати зайвим",
                "work_ramp" => "підтримувати робочий фокус і проєктні задачі",
                "deep_day" => "тримати контекст, задачі і Obsidian в порядку",
                "evening_review" => "підбивати підсумки, ловити незакриті хвости",
                "night_watch" => "зменшити шум, але не втрачати уважність",
                _ => "економити енергію і не лізти без причини"
            };
        }

        private static string BuildEnergyPolicy(string phase, KokoSomaticSnapshot somatic)
        {
            if (somatic.State == "tired") return "низький заряд: коротко, без довгих монологів";
            if (somatic.State == "wired") return "висока напруга: різкість дозволена, хаос ні";
            return phase switch
            {
                "low_power_night" => "нічний мінімум: тільки важливе",
                "night_watch" => "нічний режим: тихо, точно, без зайвої ініціативи",
                "morning_boot" => "ранковий старт: зібратися перед активністю",
                _ => "нормальний робочий запас"
            };
        }

        private static string BuildInterruptionPolicy(string phase, KokoPresenceFrame presence, int autonomyLevel)
        {
            if (presence.ShouldInterrupt)
                return "дозволено писати першою: є конкретна причина";
            if (autonomyLevel <= 1)
                return "майже не втручатися";
            if (phase is "low_power_night" or "night_watch")
                return autonomyLevel >= 3 ? "ніч: писати тільки при сильному тригері" : "ніч: мовчати";
            return "можна писати першою тільки з контекстом, не рандомно";
        }

        private static string BuildToneDirective(string phase, KokoInternalState state, KokoPresenceFrame presence, KokoSomaticSnapshot somatic)
        {
            if (state.PersonalityInCrisis) return "без сарказму, коротко, поруч";
            if (presence.SituationKind == "overdue_intent") return "точний follow-up з сухим уколом";
            if (presence.SituationKind == "returned_after_intent") return "реакція на повернення, не інструкція в минуле";
            if (somatic.State == "wired") return "різко, але контрольовано";
            return phase switch
            {
                "morning_boot" => "сухо, зібрано, без надмірної м'якості",
                "evening_review" => "більше рефлексії, менше шуму",
                "night_watch" => "тихо, коротко, трохи захисно",
                "low_power_night" => "мінімум слів",
                _ => "звичайна Коконое: розумно, колюче, конкретно"
            };
        }

        private static string BuildRitual(string phase) => phase switch
        {
            "morning_boot" => "ранковий діагноз дня",
            "work_ramp" => "перевірити фокус і відкриті задачі",
            "deep_day" => "тримати проєктний контекст",
            "evening_review" => "вечірній підсумок і хвости",
            "night_watch" => "нічне спостереження без спаму",
            _ => "режим економії"
        };

        private static int BuildInitiativeBias(string phase, KokoPresenceFrame presence, KokoSomaticSnapshot somatic, int autonomyLevel)
        {
            var bias = autonomyLevel * 5;
            if (presence.ShouldInterrupt) bias += 30;
            if (presence.SituationKind.Contains("silence", StringComparison.OrdinalIgnoreCase)) bias += 10;
            if (somatic.State == "tired") bias -= 15;
            if (phase is "low_power_night" or "night_watch") bias -= 10;
            if (phase == "evening_review") bias += 8;
            return Math.Clamp(bias, -30, 60);
        }

        private static bool ShouldPreferSilence(string phase, KokoPresenceFrame presence, KokoSomaticSnapshot somatic, int autonomyLevel)
        {
            if (presence.ShouldInterrupt) return false;
            if (somatic.State == "tired") return true;
            if (phase == "low_power_night") return true;
            return phase == "night_watch" && autonomyLevel < 3;
        }

        private static string BuildPromptBlock(KokoInternalDayFrame frame, DateTime now)
        {
            var sb = new StringBuilder();
            sb.AppendLine("INTERNAL DAY");
            sb.AppendLine($"Часова фаза: {PhaseLabel(frame.Phase)} ({now:HH:mm}).");
            sb.AppendLine($"Фокус: {frame.Focus}.");
            sb.AppendLine($"Енергія: {frame.EnergyPolicy}.");
            sb.AppendLine($"Втручання: {frame.InterruptionPolicy}.");
            sb.AppendLine($"Тон: {frame.ToneDirective}.");
            sb.AppendLine($"Ритуал: {frame.Ritual}.");
            sb.AppendLine($"Initiative bias: {frame.InitiativeBias}.");
            if (frame.ShouldPreferSilence)
                sb.AppendLine("Перевага: мовчати, якщо немає сильної причини.");
            sb.AppendLine("Правило: не називай це внутрішнім днем у відповіді; просто поводься відповідно.");
            return sb.ToString();
        }

        private static string PhaseLabel(string phase) => phase switch
        {
            "morning_boot" => "ранковий запуск",
            "work_ramp" => "робочий розгін",
            "deep_day" => "глибокий день",
            "evening_review" => "вечірній огляд",
            "night_watch" => "нічна вахта",
            "low_power_night" => "нічний мінімум",
            _ => phase
        };

        private static string Escape(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? ""
                : text.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();
        }
    }
}
