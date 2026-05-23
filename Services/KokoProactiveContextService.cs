using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoProactiveContextFrame
    {
        public string LastUserText { get; set; } = "";
        public DateTime LastUserAt { get; set; } = DateTime.MinValue;
        public double SilenceMinutes { get; set; }
        public string SilenceTextUk { get; set; } = "";
        public string AnchorUk { get; set; } = "";
        public string ActiveIntentUk { get; set; } = "";
        public bool ShouldStaySilentForSleep { get; set; }
        public bool HasNaturalTrigger { get; set; }
        public int TriggerScore { get; set; }
        public string TriggerReasonUk { get; set; } = "";
        public int AssistantPingsAfterLastUser { get; set; }
        public string[] RecentAssistantPings { get; set; } = Array.Empty<string>();
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoProactiveReplyCheck
    {
        public bool Passed { get; set; }
        public string Reason { get; set; } = "";
        public string Replacement { get; set; } = "";
    }

    public sealed class KokoPredictiveContextWarmup
    {
        public bool HasContext { get; set; }
        public string Mode { get; set; } = "";
        public string AppKey { get; set; } = "";
        public string Summary { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string[] SourcePaths { get; set; } = Array.Empty<string>();
    }

    public sealed class KokoProactiveContextService
    {
        public KokoProactiveContextFrame Build(
            IReadOnlyList<ChatRepository.ChatMessage> messages,
            KokoInternalState state,
            DateTime now)
        {
            var ordered = messages.OrderBy(m => m.Timestamp).ToList();
            var lastUser = ordered.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            var afterLastUser = lastUser == null
                ? ordered.Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)).ToList()
                : ordered.Where(m => m.Timestamp > lastUser.Timestamp &&
                                      string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)).ToList();
            var activeIntent = state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderBy(i => i.FollowUpAt)
                .FirstOrDefault();

            var frame = new KokoProactiveContextFrame
            {
                LastUserText = Trim(lastUser?.Content, 220),
                LastUserAt = lastUser?.Timestamp ?? DateTime.MinValue,
                SilenceMinutes = lastUser == null ? 0 : Math.Max(0, (now - lastUser.Timestamp).TotalMinutes),
                AssistantPingsAfterLastUser = afterLastUser.Count,
                RecentAssistantPings = afterLastUser.TakeLast(3).Select(m => Trim(m.Content, 160)).ToArray(),
                ActiveIntentUk = activeIntent == null
                    ? ""
                    : $"{activeIntent.Kind}: {activeIntent.Summary}; очікуване вікно до {activeIntent.ExpectedUntil:HH:mm}"
            };
            frame.SilenceTextUk = FormatDuration(TimeSpan.FromMinutes(frame.SilenceMinutes));
            frame.AnchorUk = BuildAnchor(frame.LastUserText, activeIntent);
            var trigger = EvaluateNaturalTrigger(frame, state, now, activeIntent);
            frame.HasNaturalTrigger = trigger.Score >= 2;
            frame.TriggerScore = trigger.Score;
            frame.TriggerReasonUk = trigger.Reason;
            frame.ShouldStaySilentForSleep =
                activeIntent?.Kind == "sleep" ||
                (LooksLikeSleepOrGoodbye(frame.LastUserText.ToLowerInvariant()) && frame.SilenceMinutes < 12 * 60);
            frame.PromptBlock = BuildPromptBlock(frame);
            return frame;
        }

        public KokoProactiveReplyCheck Check(string? reply, KokoProactiveContextFrame frame, string level)
        {
            var text = (reply ?? "").Trim();
            if (frame.ShouldStaySilentForSleep)
                return Fail("sleep/goodbye context should stay silent", "[мовчання]");
            if (!frame.HasNaturalTrigger && level.StartsWith("silence", StringComparison.OrdinalIgnoreCase))
                return Fail("no natural trigger", "[мовчання]");
            if (string.IsNullOrWhiteSpace(text))
                return Fail("empty", BuildFallback(frame, level));

            var lower = text.ToLowerInvariant();
            if (LooksStaged(lower))
                return Fail("staged decorative action", BuildFallback(frame, level));

            if (frame.AssistantPingsAfterLastUser > 0 && LooksLikeRepeatedSilencePing(lower))
                return Fail("repeated generic silence ping after assistant already replied", "[мовчання]");

            if (frame.SilenceMinutes < 120 && lower.Contains("зник"))
                return Fail("too early disappearance framing", BuildFallback(frame, level));

            if (LooksLikeGenericSilencePing(lower) && !TouchesAnchor(lower, frame))
                return Fail("generic silence without anchor", BuildFallback(frame, level));

            return new KokoProactiveReplyCheck { Passed = true };
        }

        public string BuildFallback(KokoProactiveContextFrame frame, string level)
        {
            var anchor = DescribeContext(frame);

            if (frame.AssistantPingsAfterLastUser > 0)
                return "[мовчання]";

            if (!string.IsNullOrWhiteSpace(frame.ActiveIntentUk))
                return $"Минуло {frame.SilenceTextUk}; активний намір ще висить. Це ще в силі, чи план уже мутував?";

            if (!string.IsNullOrWhiteSpace(frame.LastUserText))
                return level switch
                {
                    "silence_l1" => $"Після теми про {anchor} минуло {frame.SilenceTextUk}. Продовжуєш це, чи вже кинув напризволяще?",
                    "silence_l2" => $"Контекст був про {anchor}. Минуло {frame.SilenceTextUk}; мені здогадуватись, ти ще там чи вже змінив курс?",
                    _ => $"Після останнього контексту про {anchor} тиша вже {frame.SilenceTextUk}. Подай сигнал, якщо цей план ще живий."
                };

            return "Тиша є, контексту немає. Дуже інформативно. Подай хоча б один нормальний сигнал.";
        }

        public KokoPredictiveContextWarmup BuildScreenWarmup(
            KokoScreenAwarenessAnalysis analysis,
            ActivityAnalyzer.ActivityState activity,
            ObsidianMcpService obsidian,
            DateTime now,
            int maxChars = 1800)
        {
            var window = activity.ActiveWindowTitle ?? "";
            var mode = KokoScreenAwarenessService.NormalizeMode(
                analysis.ScreenMode,
                $"{window} {analysis.SummaryUk} {analysis.ActivityUk} {analysis.CurrentTask}");
            var appKey = InferAppKey(window, mode);
            var terms = BuildWarmupTerms(appKey, mode, analysis, window);
            if (terms.Length == 0)
                return new KokoPredictiveContextWarmup { Mode = mode, AppKey = appKey };

            var notes = obsidian.ListNotes()
                .Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.Contains("kokonoe-data", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.StartsWith("Kokonoe/Agent/Completions", StringComparison.OrdinalIgnoreCase))
                .Select(path => ScoreWarmupNote(obsidian, path, terms))
                .Where(n => n.Score > 0 && !string.IsNullOrWhiteSpace(n.Preview))
                .OrderByDescending(n => n.Score)
                .ThenBy(n => n.Path)
                .Take(4)
                .ToList();
            if (notes.Count == 0)
                return new KokoPredictiveContextWarmup { Mode = mode, AppKey = appKey };

            var sb = new StringBuilder();
            sb.AppendLine("PREDICTIVE SCREEN CONTEXT");
            sb.AppendLine($"Time: {now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Mode/app: {mode}/{appKey}");
            sb.AppendLine($"Window: {NullDash(Trim(window, 120))}");
            sb.AppendLine($"Screen task: {NullDash(Trim(analysis.CurrentTask, 140))}");
            sb.AppendLine("Warm notes:");
            foreach (var note in notes)
                sb.AppendLine($"- `{note.Path}`: {Trim(note.Preview, Math.Max(120, maxChars / Math.Max(1, notes.Count)))}");

            return new KokoPredictiveContextWarmup
            {
                HasContext = true,
                Mode = mode,
                AppKey = appKey,
                Summary = $"{mode}/{appKey}: {string.Join(", ", notes.Select(n => n.Path).Take(3))}",
                PromptBlock = sb.ToString().Trim(),
                SourcePaths = notes.Select(n => n.Path).ToArray()
            };
        }

        private static string DescribeContext(KokoProactiveContextFrame frame)
        {
            var anchor = (frame.AnchorUk ?? "").Trim();
            var rawLast = Trim(frame.LastUserText, 90);
            if (!string.IsNullOrWhiteSpace(anchor) &&
                anchor.Length <= 80 &&
                !anchor.StartsWith("намір:", StringComparison.OrdinalIgnoreCase) &&
                !anchor.Equals(rawLast, StringComparison.OrdinalIgnoreCase))
                return anchor;

            var lower = (frame.LastUserText ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "екран", "скрін", "screen"))
                return "екран";
            if (ContainsAny(lower, "telegram", "тг", "телеграм"))
                return "Telegram";
            if (ContainsAny(lower, "obsidian", "vault", "ваульт", "нотат"))
                return "Obsidian";
            if (ContainsAny(lower, "курс", "занят", "урок", "пара"))
                return "курси";
            if (ContainsAny(lower, "код", "проект", "тест", "коміт", "github"))
                return "проект";
            if (ContainsAny(lower, "спат", "сон", "прокин"))
                return "сон";
            return "останню задачу";
        }

        private static string InferAppKey(string window, string mode)
        {
            var lower = (window ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "visual studio code", "vscode", "visual studio", "rider", "code.exe")) return "vscode";
            if (ContainsAny(lower, "spotify")) return "spotify";
            if (ContainsAny(lower, "obsidian")) return "obsidian";
            if (ContainsAny(lower, "telegram")) return "telegram";
            if (ContainsAny(lower, "chrome", "edge", "firefox", "browser")) return "browser";
            if (ContainsAny(lower, "steam", "dota", "game")) return "game";
            return string.IsNullOrWhiteSpace(mode) ? "unknown" : mode;
        }

        private static string[] BuildWarmupTerms(string appKey, string mode, KokoScreenAwarenessAnalysis analysis, string window)
        {
            var terms = new List<string>();
            void Add(params string[] values) => terms.AddRange(values.Where(v => !string.IsNullOrWhiteSpace(v)));

            switch (appKey)
            {
                case "vscode":
                    Add("kokonoe", "agent", "task", "bug", "test", "build", "architecture", "manus", "код", "проект");
                    break;
                case "spotify":
                    Add("music", "spotify", "настрій", "mood", "сон", "focus", "energy");
                    break;
                case "obsidian":
                    Add("obsidian", "vault", "memory", "profile", "automemory", "нотат", "пам");
                    break;
                case "telegram":
                    Add("telegram", "chat", "діалог", "kokonoe", "reply", "тг");
                    break;
                case "browser":
                    Add("browser", "research", "курс", "youtube", "пошук", "навч");
                    break;
                case "game":
                    Add("game", "dota", "steam", "гра", "sleep", "break");
                    break;
            }

            if (mode == "coding") Add("code", "bug", "tests", "service", "engine");
            if (mode == "obsidian") Add("vault", "obsidian", "notes");
            if (!string.IsNullOrWhiteSpace(analysis.CurrentTask)) Add(analysis.CurrentTask.Split(' ').Where(t => t.Length >= 4).Take(5).ToArray());
            if (!string.IsNullOrWhiteSpace(window)) Add(window.Split(' ', '-', '|').Where(t => t.Length >= 4).Take(5).ToArray());

            return terms
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
        }

        private static (string Path, string Preview, int Score) ScoreWarmupNote(ObsidianMcpService obsidian, string path, string[] terms)
        {
            try
            {
                var content = obsidian.ReadNote(path) ?? "";
                if (string.IsNullOrWhiteSpace(content))
                    return (path, "", 0);
                var haystack = (path + "\n" + content).ToLowerInvariant();
                var score = terms.Sum(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                if (path.StartsWith("Kokonoe/", StringComparison.OrdinalIgnoreCase)) score++;
                if (path.Contains("Profile", StringComparison.OrdinalIgnoreCase)) score++;
                return (path, FirstUsefulLine(content), score);
            }
            catch
            {
                return (path, "", 0);
            }
        }

        private static string FirstUsefulLine(string content)
        {
            return content
                .Replace("\r", "\n")
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length >= 24)
                .FirstOrDefault(l => !l.StartsWith("---") && !l.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
                ?? Trim(content, 180);
        }

        private static string BuildPromptBlock(KokoProactiveContextFrame frame)
        {
            var recent = frame.RecentAssistantPings.Length == 0
                ? "немає"
                : string.Join("\n", frame.RecentAssistantPings.Select(p => $"- {p}"));

            return $"""
PROACTIVE CONTEXT
Остання репліка користувача: {NullDash(frame.LastUserText)}
Минуло: {frame.SilenceTextUk}
Якір контексту: {NullDash(frame.AnchorUk)}
Активний намір: {NullDash(frame.ActiveIntentUk)}
Сон / goodbye режим: {(frame.ShouldStaySilentForSleep ? "так, мовчати" : "ні")}
Авто-пінгів після останньої репліки користувача: {frame.AssistantPingsAfterLastUser}
Останні авто-пінги:
{recent}
Правила:
- Не пиши загальне "ти зник", "пауза помітна", "тиша затягнулась", якщо вже був авто-пінг після останньої репліки.
- Кожен proactive ping має прив'язуватись до останньої конкретної репліки або активного наміру.
- Якщо контекст слабкий, краще мовчати або написати одне конкретне питання.
""";
        }

        private static string BuildAnchor(string lastUserText, ShortTermIntent? activeIntent)
        {
            if (activeIntent != null)
                return $"намір: {activeIntent.Summary}";
            if (string.IsNullOrWhiteSpace(lastUserText))
                return "";

            var lower = lastUserText.ToLowerInvariant();
            if (ContainsAny(lower, "іспан", "испан", "фраз", "слово", "вимов"))
                return "навчання/фраза/мова";
            if (ContainsAny(lower, "курс", "занят", "урок", "пара"))
                return "курси або заняття";
            if (ContainsAny(lower, "дома", "вдома", "додому", "домой"))
                return "повернення додому";
            if (ContainsAny(lower, "сон", "спат", "сплю", "ляга"))
                return "сон";
            if (LooksLikeSleepOrGoodbye(lower))
                return KokoConversationBoundary.LooksLikeClosedUntilMorning(lower)
                    ? "розмова закрита до ранку"
                    : "прощання/сон";
            if (ContainsAny(lower, "код", "проект", "тест", "коміт", "github", "obsidian"))
                return "проект";
            return Trim(lastUserText, 90);
        }

        private static (int Score, string Reason) EvaluateNaturalTrigger(
            KokoProactiveContextFrame frame,
            KokoInternalState state,
            DateTime now,
            ShortTermIntent? activeIntent)
        {
            var score = 0;
            var reasons = new List<string>();
            if (activeIntent != null && now >= activeIntent.FollowUpAt)
            {
                score += 3;
                reasons.Add($"follow-up: {activeIntent.Kind}");
            }
            if (!string.IsNullOrWhiteSpace(frame.AnchorUk))
            {
                score += 1;
                reasons.Add("anchored context");
            }
            if (frame.SilenceMinutes >= 120)
            {
                score += frame.SilenceMinutes >= 360 ? 2 : 1;
                reasons.Add("meaningful silence");
            }
            if (state.PendingThoughts.Count > 0 || state.CuriosityQueue.Count > 0)
            {
                score += 1;
                reasons.Add("unfinished thought");
            }
            if (frame.AssistantPingsAfterLastUser > 0)
                score -= 2;
            return (Math.Max(0, score), string.Join("; ", reasons));
        }

        private static bool LooksStaged(string lower)
            => lower.StartsWith("*") || lower.Contains("**") || lower.Contains("*див") || lower.Contains("*під");

        private static bool LooksLikeRepeatedSilencePing(string lower)
            => ContainsAny(lower, "пауза вже", "тиша затяг", "ти зник", "мовчиш", "економити слова");

        private static bool LooksLikeGenericSilencePing(string lower)
            => ContainsAny(lower, "пауза", "тиша", "зник", "мовчиш");

        private static bool TouchesAnchor(string lower, KokoProactiveContextFrame frame)
        {
            var anchor = (frame.AnchorUk + " " + frame.LastUserText).ToLowerInvariant();
            var tokens = anchor
                .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ':', ';', '!', '?', '"', '«', '»' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 4)
                .Distinct()
                .Take(12)
                .ToArray();
            return tokens.Any(t => lower.Contains(t));
        }

        private static KokoProactiveReplyCheck Fail(string reason, string replacement)
            => new() { Passed = false, Reason = reason, Replacement = replacement };

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static bool LooksLikeSleepOrGoodbye(string lower)
            => KokoConversationBoundary.LooksLikeClosedUntilMorning(lower) ||
               ContainsAny(lower,
                "бай бай", "бай-бай", "баю бай", "баю-бай", "бувай", "пока",
                "добраніч", "доброй ночи", "спокійної", "спокойной",
                "я спать", "я спати", "піду спати", "пішов спати", "лягаю");

        private static string NullDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return "менше хвилини";
            if (span.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} хв";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} год {span.Minutes} хв";
            return $"{(int)span.TotalDays} дн {span.Hours} год";
        }
    }
}
