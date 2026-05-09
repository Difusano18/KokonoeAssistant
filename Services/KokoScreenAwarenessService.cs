using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoScreenAwarenessAnalysis
    {
        public string SummaryUk { get; set; } = "";
        public string ActivityUk { get; set; } = "";
        public bool ShouldComment { get; set; }
        public string CommentUk { get; set; } = "";
        public double Importance { get; set; }
        public string Raw { get; set; } = "";
    }

    public sealed class KokoScreenAwarenessDecision
    {
        public bool ShouldSend { get; set; }
        public string Reason { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public sealed class KokoScreenAwarenessService
    {
        public string BuildVisionPrompt(
            ActivityAnalyzer.ActivityState activity,
            string lastSummary,
            string lastComment,
            DateTime now)
        {
            var window = string.IsNullOrWhiteSpace(activity.ActiveWindowTitle)
                ? "невідоме вікно"
                : activity.ActiveWindowTitle;

            return $$"""
Ти Kokonoe. Подивись на скрін екрана користувача і зроби короткий screen-awareness аналіз.

Поточний час: {{now:dd.MM.yyyy HH:mm}}
Активне вікно Windows: {{window}}
Активність за пікселями: {{(activity.IsActive ? "активний екран" : "майже без змін")}}
Зміна пікселів: {{activity.PixelDifferencePercentage:F1}}%
Минуло від помітної зміни: {{(int)activity.TimeSinceLastChange.TotalMinutes}} хв
Попередній screen-summary: {{NullDash(lastSummary)}}
Останній screen-comment: {{NullDash(lastComment)}}

Правила:
- Відповідай ТІЛЬКИ valid JSON без markdown.
- Пиши українською.
- Не переписуй приватні рядки, ключі, паролі, токени або довгі повідомлення з екрана.
- Не коментуй просто заради коментаря. Коментар потрібен тільки якщо на екрані є помітна дія, помилка, довга застійна ситуація, зміна заняття, навчання, код, гра, Telegram/чат або щось реально варте реакції.
- Якщо це майже той самий екран і немає нової думки, постав should_comment=false.
- comment_uk має бути 1 коротке речення в її стилі: живо, сухо, без технічних слів типу "скріншот проаналізовано".

JSON schema:
{
  "summary_uk": "що видно/що він робить, до 140 символів",
  "activity_uk": "active|idle|same|changed + коротко",
  "should_comment": true,
  "comment_uk": "короткий коментар або порожньо",
  "importance": 0.0
}
""";
        }

        public KokoScreenAwarenessAnalysis Parse(string? raw)
        {
            raw = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return new KokoScreenAwarenessAnalysis { Raw = "" };

            try
            {
                var json = ExtractJsonObject(raw);
                if (json == null)
                    return new KokoScreenAwarenessAnalysis { SummaryUk = Trim(raw, 180), Raw = raw };

                var obj = JObject.Parse(json);
                return new KokoScreenAwarenessAnalysis
                {
                    SummaryUk = Trim(obj["summary_uk"]?.ToString(), 180),
                    ActivityUk = Trim(obj["activity_uk"]?.ToString(), 120),
                    ShouldComment = obj["should_comment"]?.Value<bool>() == true,
                    CommentUk = CleanComment(obj["comment_uk"]?.ToString()),
                    Importance = Math.Clamp(obj["importance"]?.Value<double>() ?? 0, 0, 1),
                    Raw = raw
                };
            }
            catch
            {
                return new KokoScreenAwarenessAnalysis { SummaryUk = Trim(raw, 180), Raw = raw };
            }
        }

        public KokoScreenAwarenessDecision DecideComment(
            KokoScreenAwarenessAnalysis analysis,
            DateTime now,
            DateTime lastCommentAt,
            string lastComment,
            int cooldownMinutes,
            bool commentsEnabled)
        {
            if (!commentsEnabled)
                return No("comments disabled");

            if (!analysis.ShouldComment)
                return No("vision chose observation-only");

            var comment = CleanComment(analysis.CommentUk);
            if (string.IsNullOrWhiteSpace(comment))
                return No("empty comment");

            var cooldown = Math.Clamp(cooldownMinutes, 1, 180);
            if ((now - lastCommentAt).TotalMinutes < cooldown)
                return No("comment cooldown");

            if (LooksTechnical(comment))
                return No("technical wording");

            if (TooSimilar(comment, lastComment))
                return No("same comment");

            return new KokoScreenAwarenessDecision
            {
                ShouldSend = true,
                Message = comment
            };
        }

        public string BuildCompactContext(KokoScreenAwarenessAnalysis analysis, ActivityAnalyzer.ActivityState activity)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SCREEN AWARENESS]");
            sb.AppendLine($"Window: {NullDash(activity.ActiveWindowTitle)}");
            sb.AppendLine($"Activity: {(activity.IsActive ? "active" : "idle/same")} ({activity.PixelDifferencePercentage:F1}% change)");
            if (!string.IsNullOrWhiteSpace(analysis.SummaryUk))
                sb.AppendLine($"Summary: {analysis.SummaryUk}");
            if (!string.IsNullOrWhiteSpace(analysis.ActivityUk))
                sb.AppendLine($"State: {analysis.ActivityUk}");
            return sb.ToString().Trim();
        }

        private static KokoScreenAwarenessDecision No(string reason)
            => new() { ShouldSend = false, Reason = reason };

        private static string CleanComment(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim().Trim('"', '\'', '`');
            text = text.Replace("\r", " ").Replace("\n", " ");
            while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ");
            if (text.StartsWith("[") && text.EndsWith("]")) return "";
            return Trim(text, 260);
        }

        private static bool LooksTechnical(string text)
        {
            var lower = text.ToLowerInvariant();
            return lower.Contains("скріншот проаналізовано")
                || lower.Contains("screen-awareness")
                || lower.Contains("json")
                || lower.Contains("should_comment")
                || lower.Contains("аналіз екрана заверш");
        }

        private static bool TooSimilar(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            var aa = TokenSet(a);
            var bb = TokenSet(b);
            if (aa.Length == 0 || bb.Length == 0) return false;
            var overlap = aa.Count(t => bb.Contains(t));
            return overlap >= Math.Min(aa.Length, bb.Length) * 0.75;
        }

        private static string[] TokenSet(string text)
            => text.ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ':', ';', '!', '?', '"', '«', '»', '(', ')' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 4)
                .Distinct()
                .Take(24)
                .ToArray();

        private static string? ExtractJsonObject(string text)
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            return text[start..(end + 1)];
        }

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}
