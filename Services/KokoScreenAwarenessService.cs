using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoScreenAwarenessAnalysis
    {
        public string SummaryUk { get; set; } = "";
        public string ActivityUk { get; set; } = "";
        public string ScreenMode { get; set; } = "unknown";
        public string CurrentTask { get; set; } = "";
        public string Progress { get; set; } = "";
        public string Blocker { get; set; } = "";
        public string RecommendedBehavior { get; set; } = "";
        public bool ShouldComment { get; set; }
        public string CommentUk { get; set; } = "";
        public double Importance { get; set; }
        public string Raw { get; set; } = "";
    }

    public sealed class KokoScreenSituation
    {
        public string CurrentTask { get; set; } = "";
        public string Progress { get; set; } = "unknown";
        public string Blocker { get; set; } = "";
        public string RecommendedBehavior { get; set; } = "observe";
        public string Reason { get; set; } = "";
        public bool ShouldAssist => RecommendedBehavior is "assist" or "interrupt";
    }

    public sealed class KokoScreenPatternCandidate
    {
        public bool ShouldRecord { get; set; }
        public string Key { get; set; } = "";
        public string Text { get; set; } = "";
        public string Mode { get; set; } = "";
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
            DateTime now,
            ForegroundWindowInfo? foreground = null,
            TimeSpan idleTime = default,
            string multimodalContext = "")
        {
            var window = string.IsNullOrWhiteSpace(activity.ActiveWindowTitle)
                ? "невідоме вікно"
                : activity.ActiveWindowTitle;
            var foregroundLine = foreground?.ToString() ?? "unavailable";
            var idleMinutes = idleTime.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture);

            return $$"""
Foreground window metadata: {{foregroundLine}}
Goal: find subtle patterns and help the user improve efficiency, focus, debugging, or gameplay. Do not hide behind "not important" when there is a useful pattern.
Input idle time: {{idleMinutes}} minutes
{{BuildMultimodalPromptBlock(multimodalContext)}}
Ти Kokonoe. Подивись на скрін екрана користувача і зроби короткий screen-awareness аналіз.

Поточний час: {{now:dd.MM.yyyy HH:mm}}
Активне вікно Windows: {{window}}
Активність за пікселями: {{(activity.IsActive ? "активний екран" : "майже без змін")}}
Зміна пікселів: {{activity.PixelDifferencePercentage:F1}}%
Минуло від помітної зміни: {{(int)activity.TimeSinceLastChange.TotalMinutes}} хв
Час бездіяльності (вводу): {{(int)idleTime.TotalMinutes}} хв
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

- Для живого режиму: якщо видно зависання, довге вдивляння, скрол, чат/профіль, гру або очевидне відволікання — можна дати короткий підкол навіть без "важливої" події.
- Якщо видно активну гру: будь уважнішою, але не коментуй кожен кадр. Коментар доречний при матчі/раунді, смерті, перемозі/поразці, AFK, довгій паузі, явному фейлі або якщо він застряг у меню.
- Якщо це майже той самий екран і останній коментар уже сказав ту саму думку, постав should_comment=false. Якщо сама пауза/зависання стала новою думкою — можна should_comment=true.

JSON schema:
{
  "summary_uk": "що видно/що він робить, до 140 символів",
  "activity_uk": "active|idle|same|changed + коротко",
  "screen_mode": "coding|obsidian|telegram|browser|game|idle|private|media|desktop",
  "current_task": "ймовірна задача користувача, до 90 символів",
  "progress": "moving|stuck|idle|switching|unknown",
  "blocker": "видима перешкода або порожньо",
  "recommended_behavior": "observe|assist|interrupt|jab",
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
                    ScreenMode = NormalizeMode(obj["screen_mode"]?.ToString(), raw),
                    CurrentTask = RedactSensitive(Trim(obj["current_task"]?.ToString(), 120)),
                    Progress = NormalizeProgress(obj["progress"]?.ToString()),
                    Blocker = RedactSensitive(Trim(obj["blocker"]?.ToString(), 160)),
                    RecommendedBehavior = NormalizeBehavior(obj["recommended_behavior"]?.ToString()),
                    ShouldComment = obj["should_comment"]?.Value<bool>() == true,
                    CommentUk = RedactSensitive(CleanComment(obj["comment_uk"]?.ToString())),
                    Importance = Math.Clamp(obj["importance"]?.Value<double>() ?? 0, 0, 1),
                    Raw = raw
                };
            }
            catch
            {
                return new KokoScreenAwarenessAnalysis { SummaryUk = Trim(raw, 180), ScreenMode = NormalizeMode("", raw), Raw = raw };
            }
        }

        private static string BuildMultimodalPromptBlock(string context)
        {
            if (string.IsNullOrWhiteSpace(context))
                return "";
            return $"Multimodal context:\n{context.Trim()}\n";
        }

        public KokoScreenSituation BuildSituation(
            KokoScreenAwarenessAnalysis analysis,
            ActivityAnalyzer.ActivityState activity,
            KokoScreenSituation? previous = null)
        {
            var text = $"{activity.ActiveWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk} {analysis.CurrentTask} {analysis.Blocker}".ToLowerInvariant();
            var mode = NormalizeMode(analysis.ScreenMode, text);
            var task = !string.IsNullOrWhiteSpace(analysis.CurrentTask) ? analysis.CurrentTask : InferTask(mode, text);
            var progress = !string.IsNullOrWhiteSpace(analysis.Progress) && analysis.Progress != "unknown"
                ? analysis.Progress
                : InferProgress(analysis, activity, previous, text);
            var blocker = !string.IsNullOrWhiteSpace(analysis.Blocker) ? analysis.Blocker : InferBlocker(text);
            var behavior = !string.IsNullOrWhiteSpace(analysis.RecommendedBehavior) && analysis.RecommendedBehavior != "observe"
                ? analysis.RecommendedBehavior
                : InferBehavior(mode, progress, blocker, analysis, text);

            if (mode == "private")
                behavior = "observe";

            return new KokoScreenSituation
            {
                CurrentTask = Trim(task, 120),
                Progress = progress,
                Blocker = Trim(blocker, 160),
                RecommendedBehavior = behavior,
                Reason = BuildSituationReason(mode, progress, blocker, analysis.Importance)
            };
        }

        public KokoScreenPatternCandidate BuildPatternCandidate(
            KokoScreenAwarenessAnalysis analysis,
            KokoScreenSituation situation,
            DateTime now)
        {
            var mode = NormalizeMode(analysis.ScreenMode, $"{analysis.SummaryUk} {analysis.ActivityUk} {situation.CurrentTask}");
            if (mode is "private" or "idle" or "desktop")
                return new KokoScreenPatternCandidate { ShouldRecord = false, Mode = mode };

            var text = $"{analysis.SummaryUk} {analysis.ActivityUk} {situation.CurrentTask}".ToLowerInvariant();
            var category = mode switch
            {
                "game" => ContainsAny(text, "dota", "дота") ? "Dota 2 / \u0456\u0433\u0440\u0438" : "\u0456\u0433\u0440\u0438",
                "browser" => ContainsAny(text, "youtube", "ютуб") ? "YouTube/\u0431\u0440\u0430\u0443\u0437\u0435\u0440" : "\u0431\u0440\u0430\u0443\u0437\u0435\u0440/\u043f\u043e\u0448\u0443\u043a",
                "coding" => "\u043a\u043e\u0434\u0438\u043d\u0433/\u0434\u0435\u0431\u0430\u0433",
                "obsidian" => "\u0440\u043e\u0431\u043e\u0442\u0430 \u0437 Obsidian/vault",
                "telegram" => "\u0447\u0430\u0442/\u0442\u0435\u0441\u0442\u0443\u0432\u0430\u043d\u043d\u044f Kokonoe",
                "media" => "\u043c\u0435\u0434\u0456\u0430",
                _ => mode
            };

            var slot = now.Hour switch
            {
                >= 5 and < 12 => "\u0440\u0430\u043d\u043e\u043a",
                >= 12 and < 18 => "\u0434\u0435\u043d\u044c",
                >= 18 and < 24 => "\u0432\u0435\u0447\u0456\u0440",
                _ => "\u043d\u0456\u0447"
            };

            return new KokoScreenPatternCandidate
            {
                ShouldRecord = true,
                Mode = mode,
                Key = NormalizePatternKey($"{category}|{slot}"),
                Text = $"{slot}: \u0447\u0430\u0441\u0442\u043e \u043f\u043e\u043c\u0456\u0447\u0435\u043d\u043e {category}; \u0439\u043c\u043e\u0432\u0456\u0440\u043d\u0430 \u043f\u043e\u0442\u043e\u0447\u043d\u0430 \u0437\u0430\u0434\u0430\u0447\u0430: {NullDash(situation.CurrentTask)}"
            };
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
            string activeWindowTitle = "",
            KokoScreenSituation? situation = null)
        {
            if (!commentsEnabled)
                return No("comments disabled", "silence");

            if (!analysis.ShouldComment)
                return No("vision chose observation-only");

            var comment = CleanComment(analysis.CommentUk);
            if (string.IsNullOrWhiteSpace(comment))
                return No("empty comment");

            var passiveChatWindow = IsPassiveChatWindow(activeWindowTitle, analysis);
            if (analysis.ScreenMode == "private" || LooksSensitive(activeWindowTitle, analysis))
                return No("sensitive screen", "silence");
            analysis.ScreenMode = NormalizeMode(analysis.ScreenMode, $"{activeWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk}");

            var gameMode = analysis.ScreenMode == "game";
            var importanceFloor = gameMode ? 0.40 : 0.45;
            if (analysis.Importance < importanceFloor)
                return No($"Importance {analysis.Importance:0.00} < {importanceFloor:0.00}");

            var useful = LooksUseful(activeWindowTitle, analysis, screenChanged, isActive);
            var jabCandidate = LooksJabCandidate(activeWindowTitle, analysis, screenChanged, isActive, passiveChatWindow);
            if (situation?.RecommendedBehavior is "assist" or "interrupt")
                useful = true;
            if (situation?.RecommendedBehavior == "jab")
                jabCandidate = true;
            if (gameMode && analysis.Importance >= 0.40 && (screenChanged || isActive))
                jabCandidate = true;

            if (!screenChanged && !isActive && !jabCandidate)
                return No("unchanged idle screen");

            var kind = useful ? "assist" : jabCandidate ? "jab" : "observe";
            if (kind == "observe")
                return No("observation only");

            var cooldown = Math.Clamp(cooldownMinutes, 1, 180);
            if (kind != "jab" && (now - lastCommentAt).TotalMinutes < cooldown)
                return No("comment cooldown");
            if (gameMode && kind == "jab" && (now - lastCommentAt).TotalMinutes < cooldown)
                return No("game comment cooldown", "jab");

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

        public string BuildCompactContext(KokoScreenAwarenessAnalysis analysis, ActivityAnalyzer.ActivityState activity, KokoScreenSituation? situation = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SCREEN AWARENESS]");
            sb.AppendLine($"Window: {NullDash(activity.ActiveWindowTitle)}");
            sb.AppendLine($"Mode: {NullDash(analysis.ScreenMode)}");
            sb.AppendLine($"Activity: {(activity.IsActive ? "active" : "idle/same")} ({activity.PixelDifferencePercentage:F1}% change)");
            if (!string.IsNullOrWhiteSpace(analysis.SummaryUk))
                sb.AppendLine($"Summary: {analysis.SummaryUk}");
            if (!string.IsNullOrWhiteSpace(analysis.ActivityUk))
                sb.AppendLine($"State: {analysis.ActivityUk}");
            situation ??= BuildSituation(analysis, activity);
            sb.AppendLine("[SCREEN SITUATION]");
            sb.AppendLine($"Task: {NullDash(situation.CurrentTask)}");
            sb.AppendLine($"Progress: {NullDash(situation.Progress)}");
            sb.AppendLine($"Blocker: {NullDash(situation.Blocker)}");
            sb.AppendLine($"Behavior: {NullDash(situation.RecommendedBehavior)}");
            if (!string.IsNullOrWhiteSpace(situation.Reason))
                sb.AppendLine($"Reason: {situation.Reason}");
            return sb.ToString().Trim();
        }

        private static KokoScreenAwarenessDecision No(string reason, string kind = "observe")
            => new() { ShouldSend = false, Reason = reason, Kind = kind };

        public static string NormalizeMode(string? declared, string text)
        {
            var mode = (declared ?? "").Trim().ToLowerInvariant();
            if (mode is "coding" or "obsidian" or "telegram" or "browser" or "game" or "idle" or "private" or "media" or "desktop")
                return mode;

            var lower = (text ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "password", "token", "api key", "bank", "seed phrase", "authenticator")) return "private";
            if (ContainsAny(lower, "visual studio", "vscode", "rider", "terminal", "build", "exception", "code", "код")) return "coding";
            if (ContainsAny(lower, "obsidian", "vault", "graph", "нотат", "заміт")) return "obsidian";
            if (ContainsAny(lower, "telegram", "чат", "chat", "bot")) return "telegram";
            if (ContainsAny(lower, "chrome", "browser", "youtube", "браузер", "сайт")) return "browser";
            if (ContainsAny(lower, "dota", "steam", "game", "гра")) return "game";
            if (ContainsAny(lower, "idle", "same", "без змін", "завис")) return "idle";
            return "desktop";
        }

        private static string NormalizeProgress(string? value)
        {
            var v = (value ?? "").Trim().ToLowerInvariant();
            return v is "moving" or "stuck" or "idle" or "switching" ? v : "unknown";
        }

        private static string NormalizeBehavior(string? value)
        {
            var v = (value ?? "").Trim().ToLowerInvariant();
            return v is "observe" or "assist" or "interrupt" or "jab" ? v : "observe";
        }

        private static string InferTask(string mode, string text)
        {
            if (mode == "coding" || ContainsAny(text, "build", "exception", "error", "visual studio", "vscode", "code"))
                return "debugging or editing code";
            if (mode == "obsidian" || ContainsAny(text, "obsidian", "vault"))
                return "working with Obsidian vault";
            if (mode == "telegram" || ContainsAny(text, "telegram", "chat", "bot"))
                return "chatting or testing Kokonoe behavior";
            if (mode == "browser" || ContainsAny(text, "browser", "chrome", "youtube"))
                return "browsing or researching";
            if (mode == "game")
                return "playing a game";
            return "using the desktop";
        }

        private static string InferProgress(
            KokoScreenAwarenessAnalysis analysis,
            ActivityAnalyzer.ActivityState activity,
            KokoScreenSituation? previous,
            string text)
        {
            if (ContainsAny(text, "error", "exception", "failed", "crash", "помил", "злам", "stuck"))
                return "stuck";
            if (!activity.IsActive || ContainsAny(text, "idle", "same", "без змін", "завис"))
                return previous != null && TextSimilarity(previous.CurrentTask, analysis.CurrentTask) > 0.55 ? "stuck" : "idle";
            if (ContainsAny(text, "changed", "active", "typing", "editing", "build", "код", "редактор"))
                return "moving";
            return "unknown";
        }

        private static string InferBlocker(string text)
        {
            if (ContainsAny(text, "locked", "file is locked", "cannot access", "used by another process"))
                return "file locked by running process";
            if (ContainsAny(text, "error", "exception", "failed", "crash", "помил", "злам"))
                return "visible error or failed operation";
            if (ContainsAny(text, "same", "idle", "без змін", "завис"))
                return "no visible progress";
            return "";
        }

        private static string InferBehavior(string mode, string progress, string blocker, KokoScreenAwarenessAnalysis analysis, string text)
        {
            if (!string.IsNullOrWhiteSpace(blocker))
                return progress == "stuck" ? "interrupt" : "assist";
            if (mode is "coding" or "obsidian" && analysis.Importance >= 0.65)
                return "assist";
            if (progress == "stuck" && analysis.Importance >= 0.65)
                return "interrupt";
            if (ContainsAny(text, "youtube", "game", "telegram", "profile", "idle") && analysis.Importance >= 0.70)
                return "jab";
            return "observe";
        }

        private static string BuildSituationReason(string mode, string progress, string blocker, double importance)
        {
            var parts = new[]
            {
                $"mode={mode}",
                $"progress={progress}",
                string.IsNullOrWhiteSpace(blocker) ? "" : $"blocker={blocker}",
                $"importance={importance:0.00}"
            }.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join("; ", parts);
        }

        private static string NormalizePatternKey(string text)
        {
            var chars = text.ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            var normalized = new string(chars);
            while (normalized.Contains("--", StringComparison.Ordinal))
                normalized = normalized.Replace("--", "-");
            return normalized.Trim('-');
        }

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
            if (analysis.Importance >= 0.78 && (screenChanged || isActive))
                return true;

            return analysis.Importance >= 0.55 && ContainsAny(text,
                "error", "exception", "failed", "crash", "bug", "помил", "злам",
                "code", "код", "visual studio", "vscode", "rider", "terminal", "build",
                "завдання", "homework", "курс", "навчан", "obsidian", "editor", "редактор");
        }

        private static bool LooksJabCandidate(string activeWindowTitle, KokoScreenAwarenessAnalysis analysis, bool screenChanged, bool isActive, bool passiveChatWindow)
        {
            var text = $"{activeWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk} {analysis.CommentUk}".ToLowerInvariant();
            if (passiveChatWindow && analysis.Importance >= 0.50)
                return true;

            if (!screenChanged && !isActive && analysis.Importance >= 0.60)
                return true;

            return analysis.Importance >= 0.50 && ContainsAny(text,
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
                .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ':', ';', '!', '?', '"', '(', ')' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 4)
                .Distinct()
                .Take(24)
                .ToArray();

        private static double TextSimilarity(string? a, string? b)
        {
            var aa = TokenSet(a ?? "");
            var bb = TokenSet(b ?? "");
            if (aa.Length == 0 || bb.Length == 0) return 0;
            var overlap = aa.Count(t => bb.Contains(t));
            return (double)overlap / Math.Max(aa.Length, bb.Length);
        }

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
