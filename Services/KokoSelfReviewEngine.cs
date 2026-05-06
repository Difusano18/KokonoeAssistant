using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoSelfReviewFrame
    {
        public string Summary { get; set; } = "";
        public string RiskLevel { get; set; } = "low";
        public bool RequiresObsidianContext { get; set; }
        public bool HasTemporalRisk { get; set; }
        public string[] Warnings { get; set; } = Array.Empty<string>();
        public string Directive { get; set; } = "";
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoSelfReviewEngine
    {
        public KokoSelfReviewFrame Evaluate(
            string? userText,
            KokoInternalState state,
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            KokoPresenceFrame presence,
            KokoInternalDayFrame internalDay,
            KokoPatternEngine.RhythmProfile rhythm,
            DateTime now)
        {
            userText ??= "";
            var lower = userText.ToLowerInvariant();
            var warnings = new List<string>();

            var lastUser = messages
                .Where(m => m.Role == "user")
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();
            var silence = lastUser == null ? TimeSpan.Zero : now - lastUser.Timestamp;

            if (silence.TotalMinutes > 45)
                warnings.Add($"Врахуй часовий розрив: від останньої репліки минуло {FormatDuration(silence)}.");

            var activeIntent = state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderByDescending(i => i.FollowUpAt)
                .FirstOrDefault();

            var recentResolvedIntent = state.ShortTermIntents
                .Where(i => i.ResolvedAt.HasValue && now - i.ResolvedAt.Value < TimeSpan.FromHours(6))
                .OrderByDescending(i => i.ResolvedAt)
                .FirstOrDefault();

            if (activeIntent != null)
            {
                var overdue = now - activeIntent.ExpectedUntil;
                warnings.Add(overdue.TotalMinutes > 0
                    ? $"Активний намір прострочений: {activeIntent.Summary}; очікуване вікно минуло {FormatDuration(overdue)} тому."
                    : $"Активний намір ще триває: {activeIntent.Summary}; не відповідай так, ніби його не було.");
            }

            if (recentResolvedIntent != null)
                warnings.Add($"Нещодавно закритий намір: {recentResolvedIntent.Summary}; не давай інструкцію в минуле.");

            if (ContainsAny(lower, "прокин", "проснув", "поспав", "я встав", "я тут") ||
                (recentResolvedIntent?.Kind == "sleep"))
            {
                warnings.Add("Заборонено казати йому «спи» або поводитись так, ніби він ще не прокинувся.");
            }

            if (ContainsAny(lower, "що далі", "план", "покращ", "реаліз", "роби", "давай"))
                warnings.Add("Користувач просить наступний крок: дай конкретний план або дію, не загальну філософію.");

            var needsVault = ContainsAny(lower,
                "obsidian", "обсидіан", "vault", "пам'ять", "память", "нотат", "архітектур", "проект", "проєкт", "код", "коміт", "github");

            var highRisk = warnings.Any(w =>
                w.Contains("Заборонено", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("прострочений", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("часовий розрив", StringComparison.OrdinalIgnoreCase));

            var frame = new KokoSelfReviewFrame
            {
                RequiresObsidianContext = needsVault,
                HasTemporalRisk = warnings.Count > 0,
                Warnings = warnings.ToArray(),
                RiskLevel = highRisk ? "high" : warnings.Count > 0 ? "medium" : "low",
                Summary = warnings.Count == 0
                    ? "Ризиків перед відповіддю не видно; відповідати з поточного контексту."
                    : $"Перед відповіддю є {warnings.Count} контрольних пунктів.",
                Directive = BuildDirective(needsVault, warnings.Count > 0, presence, internalDay, rhythm)
            };

            frame.PromptBlock = BuildPromptBlock(frame, presence, internalDay, rhythm, now);
            return frame;
        }

        private static string BuildDirective(
            bool needsVault,
            bool hasWarnings,
            KokoPresenceFrame presence,
            KokoInternalDayFrame internalDay,
            KokoPatternEngine.RhythmProfile rhythm)
        {
            var parts = new List<string>
            {
                "перед відповіддю звір час, останній намір і presence"
            };
            if (needsVault) parts.Add("використай Obsidian/vault context, якщо він є в системному контексті");
            if (hasWarnings) parts.Add("не ігноруй контрольні попередження");
            if (internalDay.ShouldPreferSilence) parts.Add("не роздувай відповідь без потреби");
            if (rhythm.CurrentSlotSamples >= 3 && rhythm.CurrentSlotActivityRate <= 0.20f)
                parts.Add("типово тихий час: не провокуй зайву розмову без причини");
            if (presence.SituationKind.Contains("intent", StringComparison.OrdinalIgnoreCase))
                parts.Add("відповідь має бути прив'язана до попередньої дії користувача");
            return string.Join("; ", parts) + ".";
        }

        private static string BuildPromptBlock(
            KokoSelfReviewFrame frame,
            KokoPresenceFrame presence,
            KokoInternalDayFrame internalDay,
            KokoPatternEngine.RhythmProfile rhythm,
            DateTime now)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SELF-REVIEW BEFORE REPLY");
            sb.AppendLine($"Час перевірки: {now:dd.MM.yyyy HH:mm}.");
            sb.AppendLine($"Ризик: {frame.RiskLevel}.");
            sb.AppendLine($"Висновок: {frame.Summary}");
            sb.AppendLine($"Presence: {presence.SummaryUk}");
            sb.AppendLine($"Внутрішній день: {internalDay.SummaryUk}");
            sb.AppendLine($"Ритм: {rhythm.Summary}");
            if (frame.Warnings.Length > 0)
            {
                sb.AppendLine("Контрольні попередження:");
                foreach (var warning in frame.Warnings)
                    sb.AppendLine($"- {warning}");
            }
            if (frame.RequiresObsidianContext)
                sb.AppendLine("Потрібен Obsidian-контекст: так.");
            sb.AppendLine($"Директива: {frame.Directive}");
            sb.AppendLine("Правило: не показуй цей review користувачу; просто виправ відповідь відповідно до нього.");
            return sb.ToString();
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string FormatDuration(TimeSpan span)
        {
            span = span.Duration();
            if (span.TotalMinutes < 1) return "менше хвилини";
            if (span.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} хв";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} год {span.Minutes} хв";
            return $"{(int)span.TotalDays} дн {span.Hours} год";
        }
    }
}
