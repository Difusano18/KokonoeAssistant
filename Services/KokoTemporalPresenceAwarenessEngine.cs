using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoTemporalPresenceFrame
    {
        public DateTime CapturedAt { get; set; } = DateTime.MinValue;
        public DateTime LastInteractionAt { get; set; } = DateTime.MinValue;
        public double? TimeSinceLastInteractionMinutes { get; set; }
        public string TimeSinceLastInteractionText { get; set; } = "unknown";
        public string UserLastExitType { get; set; } = "unknown";
        public string AbsenceClass { get; set; } = "unknown";
        public string GreetingMood { get; set; } = "neutral_return";
        public string StateInfluence { get; set; } = "";
        public string ContinuityDirective { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoTemporalPresenceAwarenessEngine
    {
        public KokoTemporalPresenceFrame Build(
            IReadOnlyList<ChatRepository.ChatMessage>? messages,
            KokoInternalState? state,
            DateTime now)
        {
            var ordered = (messages ?? Array.Empty<ChatRepository.ChatMessage>())
                .OrderBy(m => m.Timestamp)
                .ToList();
            var last = ordered.LastOrDefault();
            var lastUser = ordered.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            var interactionAt = MaxDate(last?.Timestamp ?? DateTime.MinValue, state?.LastUserMessageAt ?? DateTime.MinValue);
            if (interactionAt == DateTime.MinValue && lastUser != null)
                interactionAt = lastUser.Timestamp;

            var gapMinutes = interactionAt == DateTime.MinValue
                ? (double?)null
                : Math.Max(0, (now - interactionAt).TotalMinutes);
            var exitType = ClassifyExit(ordered, gapMinutes);
            var absence = ClassifyAbsence(gapMinutes, now.Hour);
            var mood = PickGreetingMood(state, exitType, absence, gapMinutes);
            var influence = BuildStateInfluence(state, exitType, absence, gapMinutes);
            var directive = BuildContinuityDirective(exitType, absence, mood, state);

            var frame = new KokoTemporalPresenceFrame
            {
                CapturedAt = now,
                LastInteractionAt = interactionAt,
                TimeSinceLastInteractionMinutes = gapMinutes,
                TimeSinceLastInteractionText = FormatDuration(gapMinutes),
                UserLastExitType = exitType,
                AbsenceClass = absence,
                GreetingMood = mood,
                StateInfluence = influence,
                ContinuityDirective = directive
            };
            frame.PromptBlock = BuildPromptBlock(frame);
            frame.TraceLine = $"[{now:HH:mm:ss}] temporal_presence exit={exitType}; absence={absence}; gap={frame.TimeSinceLastInteractionText}; mood={mood}";

            if (state != null)
            {
                state.LastTemporalPresenceAt = now;
                state.LastTemporalPresenceExitType = exitType;
                state.LastTemporalPresenceAbsenceClass = absence;
                state.LastTemporalPresenceGapText = frame.TimeSinceLastInteractionText;
                state.LastTemporalPresenceGreetingMood = mood;
                state.LastTemporalPresenceDirective = directive;
            }

            KokoSystemLog.Write("TEMPORAL-PRESENCE", frame.TraceLine);
            return frame;
        }

        public string BuildPromptBlock(KokoInternalState state, DateTime now)
        {
            if (state == null) return "";
            var frame = new KokoTemporalPresenceFrame
            {
                CapturedAt = now,
                LastInteractionAt = state.LastUserMessageAt,
                TimeSinceLastInteractionMinutes = state.LastUserMessageAt == DateTime.MinValue
                    ? null
                    : Math.Max(0, (now - state.LastUserMessageAt).TotalMinutes),
                TimeSinceLastInteractionText = string.IsNullOrWhiteSpace(state.LastTemporalPresenceGapText)
                    ? FormatDuration(state.LastUserMessageAt == DateTime.MinValue ? null : Math.Max(0, (now - state.LastUserMessageAt).TotalMinutes))
                    : state.LastTemporalPresenceGapText,
                UserLastExitType = Clean(state.LastTemporalPresenceExitType, "unknown"),
                AbsenceClass = Clean(state.LastTemporalPresenceAbsenceClass, "unknown"),
                GreetingMood = Clean(state.LastTemporalPresenceGreetingMood, "neutral_return"),
                StateInfluence = BuildStateInfluence(state, Clean(state.LastTemporalPresenceExitType, "unknown"), Clean(state.LastTemporalPresenceAbsenceClass, "unknown"), null),
                ContinuityDirective = Clean(state.LastTemporalPresenceDirective, "Use current time and last interaction age before framing absence.")
            };
            return BuildPromptBlock(frame);
        }

        public static string BuildDirective(KokoInternalState state)
        {
            if (state == null) return "";
            return $"TEMPORAL PRESENCE DIRECTIVE: exit={Clean(state.LastTemporalPresenceExitType, "unknown")}; absence={Clean(state.LastTemporalPresenceAbsenceClass, "unknown")}; gap={Clean(state.LastTemporalPresenceGapText, "unknown")}; greeting_mood={Clean(state.LastTemporalPresenceGreetingMood, "neutral_return")}. React to elapsed time without canned greetings or fake mind-reading.";
        }

        private static string ClassifyExit(IReadOnlyList<ChatRepository.ChatMessage> ordered, double? gapMinutes)
        {
            if (!gapMinutes.HasValue) return "unknown";
            if (gapMinutes.Value < 10) return "quick_return";

            var last = ordered.LastOrDefault();
            var lastUser = ordered.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            var lastText = (last?.Content ?? "").ToLowerInvariant();
            var lastUserText = (lastUser?.Content ?? "").ToLowerInvariant();
            var tail = string.Join("\n", ordered.TakeLast(5).Select(m => (m.Content ?? "").ToLowerInvariant()));

            if (LooksLikeSystemFailure(tail)) return "system_error_disconnect";
            if (LooksLikeTaskDone(tail)) return "task_completed";
            if (LooksLikeConflict(tail)) return "abrupt_unannounced";
            if (LooksLikePlannedFarewell(lastUserText) || LooksLikePlannedFarewell(lastText)) return "planned_farewell";
            if (last?.Role == "user" && gapMinutes.Value >= 30) return "abrupt_unannounced";
            return "ordinary_pause";
        }

        private static string ClassifyAbsence(double? gapMinutes, int hour)
        {
            if (!gapMinutes.HasValue) return "unknown";
            if (gapMinutes.Value < 3) return "instant";
            if (gapMinutes.Value < 15) return "short";
            if (gapMinutes.Value < 90) return "brief";
            if (gapMinutes.Value < 360) return "noticeable";
            if (gapMinutes.Value < 1440) return hour >= 22 || hour < 6 ? "long_night" : "long_day";
            return "multi_day";
        }

        private static string PickGreetingMood(KokoInternalState? state, string exitType, string absenceClass, double? gapMinutes)
        {
            var energy = state?.PersonaEnergyLevel ?? 0.62;
            var patience = state?.PersonaPatienceLevel ?? 0.58;
            var daily = state?.PersonalityDailyMood ?? "neutral";
            if (state?.PersonalityInCrisis == true) return "protective_low_noise";
            if (exitType == "task_completed") return "satisfied_dry";
            if (exitType == "planned_farewell") return "settled_dry";
            if (exitType == "system_error_disconnect") return "technical_focus";
            if (absenceClass is "long_night" or "multi_day" && energy < 0.42) return "tired_sharp";
            if (exitType == "abrupt_unannounced" && patience < 0.45) return "irritated_continuity";
            if (daily is "warm" or "playful") return "alive_" + daily;
            return gapMinutes is >= 240 ? "dry_return" : "neutral_return";
        }

        private static string BuildStateInfluence(KokoInternalState? state, string exitType, string absenceClass, double? gapMinutes)
        {
            if (state == null) return "state unavailable; infer only from messages and wall time";
            var parts = new List<string>
            {
                $"energy={Fmt(state.PersonaEnergyLevel)}",
                $"patience={Fmt(state.PersonaPatienceLevel)}",
                $"daily={state.PersonalityDailyMood}",
                $"favor_debt={state.PersonaFavorDebt}"
            };
            if (state.LastPresenceAt > DateTime.MinValue)
                parts.Add($"last_presence={state.LastPresenceAt:HH:mm}");
            if (state.LastSomaticAt > DateTime.MinValue)
                parts.Add($"somatic={state.LastSomaticLabel}/{Fmt(state.LastSomaticStrain)}");
            if (exitType == "abrupt_unannounced") parts.Add("continuity was interrupted");
            if (absenceClass is "long_night" or "multi_day") parts.Add("absence should be acknowledged once");
            if (gapMinutes is < 15) parts.Add("keep return greeting light");
            return string.Join("; ", parts);
        }

        private static string BuildContinuityDirective(string exitType, string absenceClass, string mood, KokoInternalState? state)
        {
            var directive = exitType switch
            {
                "planned_farewell" => "Do not dramatize the exit; resume cleanly and mention the old context only if useful.",
                "task_completed" => "Treat the previous session as completed; ask for the next target without pretending something is unfinished.",
                "system_error_disconnect" => "Acknowledge technical interruption only if relevant; avoid emotional blame.",
                "abrupt_unannounced" => "Acknowledge interrupted continuity once, dryly, then return to the concrete thread.",
                "quick_return" => "Keep it light; no disappearance framing.",
                _ => "Use elapsed time and last concrete context without generic startup filler."
            };

            if (absenceClass is "long_night" or "multi_day")
                directive += " Long absence: mark the gap once, then offer a concrete continuation.";
            if (mood.Contains("protective", StringComparison.OrdinalIgnoreCase))
                directive += " Protective mood: lower sarcasm and keep it short.";
            if ((state?.PersonaFavorDebt ?? 0) > 0)
                directive += " Favor debt may color tone, but must not become a repetitive scold.";
            return directive;
        }

        private static string BuildPromptBlock(KokoTemporalPresenceFrame frame)
        {
            var sb = new StringBuilder();
            sb.AppendLine("TEMPORAL PRESENCE AWARENESS");
            sb.AppendLine($"time_since_last_interaction: {frame.TimeSinceLastInteractionText}");
            sb.AppendLine($"user_last_exit_type: {frame.UserLastExitType}");
            sb.AppendLine($"absence_class: {frame.AbsenceClass}");
            sb.AppendLine($"greeting_mood: {frame.GreetingMood}");
            sb.AppendLine($"state_influence: {frame.StateInfluence}");
            sb.AppendLine($"continuity_directive: {frame.ContinuityDirective}");
            sb.AppendLine("rules:");
            sb.AppendLine("- Use this only as private continuity state; do not expose labels or module names.");
            sb.AppendLine("- Dynamic greetings must react to the gap, exit type, current mood, and last concrete thread.");
            sb.AppendLine("- Do not invent hidden reasons for absence. No therapy-style guesses.");
            sb.AppendLine("- One return jab is enough; then continue the user's actual work or conversation.");
            return sb.ToString();
        }

        private static bool LooksLikePlannedFarewell(string lower)
            => ContainsAny(lower,
                "goodbye", "bye", "see you", "later",
                "бувай", "пока", "до зустр", "до завтра", "добраніч", "на добраніч",
                "Р±СѓРІР°Р№", "РїРѕРєР°", "РґРѕ Р·СѓСЃС‚СЂ", "РґРѕ Р·Р°РІС‚СЂР°", "РґРѕР±СЂР°РЅС–С‡");

        private static bool LooksLikeTaskDone(string lower)
            => ContainsAny(lower,
                "done", "completed", "fixed", "pushed", "merged",
                "готово", "зроблено", "виконано", "запуш", "коміт",
                "РіРѕС‚РѕРІРѕ", "Р·СЂРѕР±Р»РµРЅРѕ", "РІРёРєРѕРЅР°РЅРѕ", "Р·Р°РїСѓС€", "РєРѕРјС–С‚");

        private static bool LooksLikeSystemFailure(string lower)
            => ContainsAny(lower,
                "crash", "disconnect", "timeout", "exception", "connection lost",
                "краш", "відвал", "таймаут", "помилка", "не працює",
                "РєСЂР°С€", "РІС–РґРІР°Р»", "С‚Р°Р№РјР°СѓС‚", "РїРѕРјРёР»Рє", "РЅРµ РїСЂР°С†");

        private static bool LooksLikeConflict(string lower)
            => ContainsAny(lower,
                "angry", "hate", "shut up", "fuck off",
                "злий", "образ", "дістала", "ненавид", "заткнись",
                "Р·Р»РёР№", "РѕР±СЂР°Р·", "РґС–СЃС‚Р°Р»Р°", "РЅРµРЅР°РІРёРґ", "Р·Р°С‚РєРЅРё");

        private static DateTime MaxDate(DateTime a, DateTime b) => a >= b ? a : b;

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string Clean(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string FormatDuration(double? minutes)
        {
            if (!minutes.HasValue) return "unknown";
            if (minutes.Value < 1) return "under 1m";
            if (minutes.Value < 60) return $"{Math.Max(1, (int)Math.Round(minutes.Value))}m";
            if (minutes.Value < 1440) return $"{(int)(minutes.Value / 60)}h {(int)(minutes.Value % 60)}m";
            return $"{(int)(minutes.Value / 1440)}d {(int)((minutes.Value % 1440) / 60)}h";
        }

        private static string Fmt(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}
