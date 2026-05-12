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
        public string Kind { get; set; } = "observe";
        public bool CountsAsJab { get; set; }
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
- Не згадуй email, username, API keys, телефони, адреси, chat id, токени, назви акаунтів або приватні ідентифікатори. Узагальнюй: "сторінка налаштувань", "панель акаунта", "чат", "редактор".
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
                    SummaryUk = RedactSensitive(Trim(obj["summary_uk"]?.ToString(), 180)),
                    ActivityUk = Trim(obj["activity_uk"]?.ToString(), 120),
                    ShouldComment = obj["should_comment"]?.Value<bool>() == true,
                    CommentUk = RedactSensitive(CleanComment(obj["comment_uk"]?.ToString())),
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
            bool commentsEnabled,
            bool screenChanged = true,
            bool isActive = true,
            string activeWindowTitle = "")
        {
            if (!commentsEnabled)
                return No("comments disabled", "silence");

            if (!analysis.ShouldComment)
                return No("vision chose observation-only");

            var comment = CleanComment(analysis.CommentUk);
            if (string.IsNullOrWhiteSpace(comment))
                return No("empty comment");

            if (analysis.Importance < 0.60)
                return No("low importance");

            var passiveChatWindow = IsPassiveChatWindow(activeWindowTitle, analysis);
            if (LooksSensitive(activeWindowTitle, analysis))
                return No("sensitive screen", "silence");

            var useful = LooksUseful(activeWindowTitle, analysis, screenChanged, isActive);
            var jabCandidate = LooksJabCandidate(activeWindowTitle, analysis, screenChanged, isActive, passiveChatWindow);

            if (!screenChanged && !isActive && !jabCandidate)
                return No("unchanged idle screen");

            var kind = useful && !passiveChatWindow ? "assist" : jabCandidate ? "jab" : "observe";
            if (kind == "observe")
                return No(passiveChatWindow ? "passive chat/profile screen" : "observation only");

            var cooldown = Math.Clamp(passiveChatWindow ? Math.Max(cooldownMinutes, 30) : cooldownMinutes, 1, 180);
            if ((now - lastCommentAt).TotalMinutes < cooldown)
                return No("comment cooldown");

            if (LooksTechnical(comment))
                return No("technical wording");

            if (TooSimilar(comment, lastComment))
                return No("same comment");

            return new KokoScreenAwarenessDecision
            {
                ShouldSend = true,
                Message = comment,
                Kind = kind,
                CountsAsJab = kind == "jab"
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

        private static KokoScreenAwarenessDecision No(string reason, string kind = "observe")
            => new() { ShouldSend = false, Reason = reason, Kind = kind };

        private static string CleanComment(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim().Trim('"', '\'', '`');
            text = text.Replace("\r", " ").Replace("\n", " ");
            while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ");
            if (text.StartsWith("[") && text.EndsWith("]")) return "";
            return Trim(text, 260);
        }

        private static string RedactSensitive(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
                "[email]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\b[A-Za-z0-9_\-]{24,}\b",
                "[private]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\+?\d[\d\s().\-]{8,}\d",
                "[phone/id]");
            return text.Trim();
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

        private static bool IsPassiveChatWindow(string activeWindowTitle, KokoScreenAwarenessAnalysis analysis)
        {
            var text = $"{activeWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk} {analysis.CommentUk}".ToLowerInvariant();
            var looksChat = text.Contains("telegram")
                || text.Contains("чат")
                || text.Contains("chat")
                || text.Contains("бот")
                || text.Contains("bot")
                || text.Contains("profile")
                || text.Contains("проф");

            if (!looksChat) return false;

            var looksPassive = text.Contains("same")
                || text.Contains("idle")
                || text.Contains("profile")
                || text.Contains("проф")
                || text.Contains("дивиш")
                || text.Contains("список")
                || text.Contains("сторін");

            return looksPassive || analysis.Importance < 0.85;
        }

        private static bool LooksSensitive(string activeWindowTitle, KokoScreenAwarenessAnalysis analysis)
        {
            var text = $"{activeWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk} {analysis.CommentUk}".ToLowerInvariant();
            return ContainsAny(text,
                "password", "passwd", "парол", "api key", "apikey", "token", "токен",
                "secret", "2fa", "otp", "authenticator", "bank", "банк", "банкінг",
                "credit card", "card number", "ключ доступ", "private key", "seed phrase");
        }

        private static bool LooksUseful(string activeWindowTitle, KokoScreenAwarenessAnalysis analysis, bool screenChanged, bool isActive)
        {
            var text = $"{activeWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk} {analysis.CommentUk}".ToLowerInvariant();
            if (analysis.Importance >= 0.88 && (screenChanged || isActive))
                return true;

            return analysis.Importance >= 0.65 && ContainsAny(text,
                "error", "exception", "failed", "crash", "bug", "помил", "злам",
                "code", "код", "visual studio", "vscode", "rider", "terminal", "build",
                "завдання", "homework", "курс", "навчан", "obsidian", "editor", "редактор");
        }

        private static bool LooksJabCandidate(string activeWindowTitle, KokoScreenAwarenessAnalysis analysis, bool screenChanged, bool isActive, bool passiveChatWindow)
        {
            var text = $"{activeWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk} {analysis.CommentUk}".ToLowerInvariant();
            if (passiveChatWindow && analysis.Importance >= 0.70)
                return true;

            if (!screenChanged && !isActive && analysis.Importance >= 0.70)
                return true;

            return analysis.Importance >= 0.65 && ContainsAny(text,
                "youtube", "tiktok", "reddit", "steam", "telegram", "chat", "чат",
                "game", "гра", "dota", "дота", "мем", "картин", "scroll", "горта",
                "idle", "same", "завис", "дивиш", "профіль", "profile");
        }

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

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
