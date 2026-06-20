using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KokonoeAssistant.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TgUpdate   = Telegram.Bot.Types.Update;
using WMsgBox    = System.Windows.MessageBox;
using WButton    = System.Windows.Controls.Button;
using WKeyArgs   = System.Windows.Input.KeyEventArgs;
using WDragArgs  = System.Windows.DragEventArgs;
using WClipboard = System.Windows.Clipboard;
using WDataFmts  = System.Windows.DataFormats;
using WTextBox   = System.Windows.Controls.TextBox;
using MediaBrush   = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor   = System.Windows.Media.Color;
using WpfRect      = System.Windows.Shapes.Rectangle;
using WpfFF        = System.Windows.Media.FontFamily;
using WpfSz        = System.Windows.Size;
using WpfOri       = System.Windows.Controls.Orientation;
using WinForms     = System.Windows.Forms;

namespace KokonoeAssistant
{
    public partial class MainWindow
    {
        private string BuildContext(string? userText = null)
        {
            // УВАГА: цей контекст додається до ~6k токенів system prompt + TOOLS.
            // Тримаємо його компактним — максимум ~800 токенів (~3200 символів).
            // GetToolsDescription() ПРИБРАНО — опис тулзів вже є в TOOLS масиві LlmService.

            const int MAX_CONTEXT_LENGTH = 5000;
            var parts = new List<(string content, int priority)>(); // priority: lower = more important
            try
            {
                var temporal = BuildTemporalAwarenessContext(userText);
                if (!string.IsNullOrWhiteSpace(temporal))
                    parts.Add((temporal, 0));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3566: " + ex); }

            parts.Add((BuildLiveResponseStyleContext(userText), 0));

            try
            {
                var cognitive = ServiceContainer.BrainEngine?.BuildCognitiveStabilityContext(userText);
                if (!string.IsNullOrWhiteSpace(cognitive))
                    parts.Add((cognitive, 0));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3576: " + ex); }

            try
            {
                var responsePlan = ServiceContainer.BrainEngine?.BuildResponsePlanContext(userText);
                if (!string.IsNullOrWhiteSpace(responsePlan))
                    parts.Add((responsePlan, 0));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3584: " + ex); }

            try
            {
                var selfReview = ServiceContainer.BrainEngine?.BuildSelfReviewContext(userText);
                if (!string.IsNullOrWhiteSpace(selfReview))
                    parts.Add((selfReview, 0));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3592: " + ex); }

            try
            {
                var timeline = ServiceContainer.BrainEngine?.BuildTimelineContext(userText);
                if (!string.IsNullOrWhiteSpace(timeline))
                    parts.Add((timeline, 0));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3600: " + ex); }

            try
            {
                var preflight = BuildObsidianPreflightContext(userText);
                if (!string.IsNullOrWhiteSpace(preflight))
                {
                    _lastObsidianPreflightAt = DateTime.Now;
                    parts.Add((preflight, 0));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3611: " + ex); }

            // 1. Релевантна пам'ять — НАЙВАЖЛИВІШЕ, бо це факти про користувача
            try
            {
                var mem = ServiceContainer.BrainEngine?.Memory;
                if (mem != null && !string.IsNullOrWhiteSpace(userText))
                {
                    var (facts, episodes) = mem.FindRelevantBlocking(userText, maxFacts: 5, maxEpisodes: 2);
                    var memParts = new List<string>();
                    foreach (var f in facts)
                        memParts.Add(f.Content);
                    foreach (var e in episodes)
                        memParts.Add($"[{e.When:dd.MM.yy}] {e.Summary}");
                    if (memParts.Count > 0)
                        parts.Add(("=== ПАМ'ЯТЬ ===\n" + string.Join("\n", memParts), 1));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3629: " + ex); }

            // 1.5 Емоційний стан — безпосередньо впливає на тон відповіді
            try
            {
                var emotHint = ServiceContainer.BrainEngine?.Emotion?.GetPromptHint();
                if (!string.IsNullOrEmpty(emotHint))
                    parts.Add((emotHint, 2));

                var condition = BuildKokoConditionContext();
                if (!string.IsNullOrWhiteSpace(condition))
                    parts.Add((condition, 2));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3642: " + ex); }

            // 2. Стан (емоції, здоров'я, scheduler) — важливо але менше
            try
            {
                var stateCtx = ServiceContainer.StateEngine.GetStateAsContext();
                if (!string.IsNullOrEmpty(stateCtx))
                    parts.Add((stateCtx, 3));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3651: " + ex); }

            // 2.1 Пульс — завжди в контексті щоб Kokonoe реагувала на зміни
            try
            {
                if (TryGetVerifiedWearablePulse(out var bpm, out var baseline))
                {
                    var diff = bpm - baseline;
                    var bpmNote = diff > 15 ? " ↑ підвищений" : diff < -10 ? " ↓ нижче базового" : "";
                    parts.Add(($"=== ПУЛЬС ===\nДжерело: verified Galaxy Watch | Поточний: {bpm:0.0} bpm{bpmNote} | Базовий: {baseline:0.0} bpm", 2));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3663: " + ex); }

            // 2.5 Календар — найближчі важливі дати
            try
            {
                var cal = ServiceContainer.Calendar;
                var upcoming = cal.GetUpcoming(14); // наступні 2 тижні
                var today = cal.GetForDay(DateTime.Today);
                if (today.Any() || upcoming.Any())
                {
                    var calLines = new List<string>();
                    if (today.Any())
                        calLines.Add("Сьогодні: " + string.Join(", ", today.Select(e => e.Title)));
                    var soon = upcoming.Where(e => e.EventAt.Date > DateTime.Today).Take(3);
                    if (soon.Any())
                        calLines.Add("Найближче: " + string.Join("; ", soon.Select(e => $"{e.Title} {e.EventAt:dd.MM}")));
                    parts.Add(("=== КАЛЕНДАР ===\n" + string.Join("\n", calLines), 2));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3682: " + ex); }

            // 3. Здоров'я — агреговані показники за тиждень
            try
            {
                var healthCtx = ServiceContainer.HealthService.GetHealthContext();
                if (!string.IsNullOrWhiteSpace(healthCtx) && !healthCtx.StartsWith("No health data"))
                    parts.Add((healthCtx.Trim(), 4));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3691: " + ex); }

            // 3.1 Цілі та активні звички
            try
            {
                var goals = ServiceContainer.GoalService.GetActiveGoals();
                var habits = ServiceContainer.HabitService.GetActiveHabits();
                if (goals.Count > 0 || habits.Count > 0)
                {
                    var ghLines = new System.Collections.Generic.List<string>();
                    if (goals.Count > 0)
                        ghLines.Add("Цілі: " + string.Join(", ", goals.Take(4).Select(g => $"{g.Title} ({g.Progress:0}%)")));
                    if (habits.Count > 0)
                        ghLines.Add("Звички: " + string.Join(", ", habits.Take(5).Select(h => h.Name)));
                    parts.Add(("=== ЦІЛІ/ЗВИЧКИ ===\n" + string.Join("\n", ghLines), 5));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3708: " + ex); }

            // 3.2 Когнітивний контекст
            try
            {
                var cogCtx = ServiceContainer.BrainEngine?.Cognition?.BuildCognitionContext();
                if (!string.IsNullOrEmpty(cogCtx))
                    parts.Add((cogCtx, 5));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3717: " + ex); }

            // 3.3 EnhancedMemory — структуровані факти про користувача
            try
            {
                var enhMem = ServiceContainer.EnhancedMemory.GetMemoryAsContext();
                if (!string.IsNullOrWhiteSpace(enhMem))
                {
                    if (enhMem.Length > 800) enhMem = enhMem[..800];
                    parts.Add((enhMem.Trim(), 6));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3729: " + ex); }

            // 4. Vault — ключові нотатки
            try
            {
                var allNotes = _obsidian.ListNotes();
                var vaultLines = new List<string>();

                var keyNotes = allNotes
                    .Where(n => n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("brain-core", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Досьє", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Facts", StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .ToList();
                if (keyNotes.Count > 0)
                    vaultLines.Add("Ключові: " + string.Join(", ", keyNotes));

                var todayNote = $"Daily/{DateTime.Now:yyyy-MM-dd}.md";
                if (allNotes.Any(n => n == todayNote))
                    vaultLines.Add("Daily: є");

                if (vaultLines.Count > 0)
                    parts.Add(("=== VAULT ===\n" + string.Join(" | ", vaultLines), 7));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3754: " + ex); }

            // 4.1 Relevant vault recall - direct Obsidian retrieval for the current message
            try
            {
                if (!string.IsNullOrWhiteSpace(userText))
                {
                    var relevant = _obsidian.SearchSemantic(userText, 4)
                        .Where(r => !string.IsNullOrWhiteSpace(r.Preview))
                        .Take(4)
                        .ToList();
                    if (relevant.Count > 0)
                    {
                        var lines = relevant.Select(r =>
                            $"- {r.Path}: {TruncateAtWordBoundary(SanitizeForLlm(r.Preview).Replace("\r", " ").Replace("\n", " "), 240)}");
                        parts.Add(("=== RELEVANT OBSIDIAN MEMORY ===\n" + string.Join("\n", lines), 2));
                    }
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3773: " + ex); }

            // 4.2 Managed task queue / memory health - compact operational state
            try
            {
                var taskQueue = _obsidian.ReadNote("Kokonoe/Tasks Queue.md");
                var memoryQuality = _obsidian.ReadNote("Kokonoe/Memory/Quality.md");
                var memoryReview = _obsidian.ReadNote("Kokonoe/Memory/Review.md");
                var ops = new List<string>();
                if (!string.IsNullOrWhiteSpace(taskQueue))
                    ops.Add(TruncateAtWordBoundary(SanitizeForLlm(taskQueue), 700));
                if (!string.IsNullOrWhiteSpace(memoryQuality))
                    ops.Add(TruncateAtWordBoundary(SanitizeForLlm(memoryQuality), 500));
                if (!string.IsNullOrWhiteSpace(memoryReview))
                    ops.Add(TruncateAtWordBoundary(SanitizeForLlm(memoryReview), 500));
                if (ops.Count > 0)
                    parts.Add(("=== KOKONOE MEMORY OPS ===\n" + string.Join("\n\n", ops), 6));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3791: " + ex); }

            // 5. Прогноз (ML.NET) — настрій/енергія на завтра
            try
            {
                var forecast = ServiceContainer.Predictor.GetForecastContext();
                if (!string.IsNullOrEmpty(forecast))
                    parts.Add((forecast.Trim(), 8));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildContext failed near source line 3800: " + ex); }

            // Сортуємо за пріоритетом і збираємо результат
            var orderedParts = parts.OrderBy(p => p.priority).Select(p => p.content).ToList();
            var result = string.Join("\n\n", orderedParts);

            // Розумне обрізання: видаляємо останні секції доки вміщаємось
            while (result.Length > MAX_CONTEXT_LENGTH && orderedParts.Count > 1)
            {
                orderedParts.RemoveAt(orderedParts.Count - 1);
                result = string.Join("\n\n", orderedParts);
            }

            // Якщо й одна секція завелика — обрізаємо на межі слова
            if (result.Length > MAX_CONTEXT_LENGTH)
            {
                result = TruncateAtWordBoundary(result, MAX_CONTEXT_LENGTH);
            }

            return result;
        }

        private bool ShouldUseFastTelegramReply(string? text)
        {
            var settings = AppSettings.Load();
            if (!settings.TgFastReplyEnabled)
                return false;

            var trimmed = text?.Trim() ?? "";
            if (trimmed.Length == 0)
                return true;

            var maxChars = Math.Clamp(settings.TgFastReplyMaxChars, 80, 1200);
            if (trimmed.Length > maxChars)
                return false;

            var lower = trimmed.ToLowerInvariant();
            var heavyMarkers = new[]
            {
                "obsidian", "vault", "profile", "memory", "note", "file", "folder",
                "code", "build", "test", "error", "exception", "commit", "push",
                "github", "screen", "screenshot", "image", "photo", "plan", "analyze",
                "research", "search", "watch", "wear", "settings", "token"
            };

            return !heavyMarkers.Any(m => lower.Contains(m, StringComparison.OrdinalIgnoreCase));
        }

        private string BuildFastTelegramContext(string channel, string? userText)
        {
            const int maxChars = 2400;
            var parts = new List<string>();

            try
            {
                var temporal = BuildTemporalAwarenessContext(userText);
                if (!string.IsNullOrWhiteSpace(temporal))
                    parts.Add(TrimForPrompt(temporal, 520));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildFastTelegramContext failed near source line 3859: " + ex); }

            try { parts.Add(BuildLiveResponseStyleContext(userText)); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildFastTelegramContext failed near source line 3861: " + ex); }

            try
            {
                var condition = BuildKokoConditionContext();
                if (!string.IsNullOrWhiteSpace(condition))
                    parts.Add(TrimForPrompt(condition, 520));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildFastTelegramContext failed near source line 3869: " + ex); }

            try
            {
                if (TryGetVerifiedWearablePulse(out var bpm, out var baseline))
                {
                    var diff = bpm - baseline;
                    parts.Add($"=== VERIFIED SOMATIC ===\nsource: Galaxy Watch; bpm={bpm:0.0}; baseline={baseline:0.0}; delta={diff:+0.0;-0.0;0.0}");
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildFastTelegramContext failed near source line 3879: " + ex); }

            try
            {
                var brain = ServiceContainer.BrainEngine;
                var state = brain?.State;
                if (state != null)
                {
                    var compact = new List<string>();
                    if (!string.IsNullOrWhiteSpace(state.LastScreenAwarenessMode))
                        compact.Add("screen=" + state.LastScreenAwarenessMode);
                    if (!string.IsNullOrWhiteSpace(state.LastScreenAwarenessSummary))
                        compact.Add("screen_summary=" + TrimForPrompt(state.LastScreenAwarenessSummary, 240));
                    if (!string.IsNullOrWhiteSpace(state.LastKnownUserActivity))
                        compact.Add("activity=" + TrimForPrompt(state.LastKnownUserActivity, 180));
                    if (compact.Count > 0)
                        parts.Add("=== LIGHT CONTINUITY ===\n" + string.Join("\n", compact));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildFastTelegramContext failed near source line 3898: " + ex); }

            try
            {
                var collective = ServiceContainer.BrainEngine?.BuildCollectiveMindDirective(userText);
                if (!string.IsNullOrWhiteSpace(collective))
                    parts.Add("=== COLLECTIVE MIND FAST ===\n" + collective);
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildFastTelegramContext failed near source line 3906: " + ex); }

            try
            {
                var recent = ServiceContainer.ChatRepository.GetMessages(6)
                    .Select(m => $"{m.Role}: {TrimForPrompt(m.Content, 160)}")
                    .ToArray();
                if (recent.Length > 0)
                    parts.Add("=== RECENT CHAT ===\n" + string.Join("\n", recent));
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildFastTelegramContext failed near source line 3916: " + ex); }

            var result = "=== FAST TELEGRAM CONTEXT ===\n" +
                         $"channel: {channel}\n" +
                         "route: fast; no vault scan; no neural governor; keep answer natural and current-message-first.\n\n" +
                         string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

            return result.Length <= maxChars
                ? result
                : TruncateAtWordBoundary(result, maxChars);
        }

        private string BuildTelegramReplyContext(string channel, string? userText, out bool fastRoute, out long elapsedMs)
        {
            var sw = Stopwatch.StartNew();
            fastRoute = ShouldUseFastTelegramReply(userText);
            try
            {
                return fastRoute
                    ? BuildFastTelegramContext(channel, userText)
                    : (channel.Equals("telegram", StringComparison.OrdinalIgnoreCase)
                        ? (ServiceContainer.BrainEngine?.BuildUnifiedExternalContext(channel, userText) ?? "")
                        : BuildContext(userText));
            }
            finally
            {
                elapsedMs = sw.ElapsedMilliseconds;
            }
        }

        private static string BuildLiveResponseStyleContext(string? userText)
        {
            var trimmed = userText?.Trim() ?? "";
            var concrete = !string.IsNullOrWhiteSpace(trimmed)
                ? $"Останній вхід користувача: «{trimmed[..Math.Min(220, trimmed.Length)]}»."
                : "Останній вхід користувача порожній або службовий.";

            return $"""
LIVE RESPONSE STYLE
{concrete}
Character calibration: Kokonoe edge means concise precision and dry wit, not constant contempt. If the latest user turn is casual/social/affectionate ("just talk", "about us", "say something nice"), answer that mode directly; do not demand a task, mock warmth, or close with a productivity pivot.
Правила живої відповіді:
- спершу реагуй на конкретику останнього повідомлення, не на абстрактний настрій;
- не починай з декоративної ремарки в *зірочках*, якщо користувач сам не почав roleplay;
- не вигадуй «монітор блимає», «датчики», «лабораторію», «тіло реагує» без прямої причини;
- не вигадуй зовнішні факти про користувача: акаунти, YouTube/Twitch/Discord, мемберства, підписки, роботу, покупки або людей, якщо цього нема в чаті;
- не психологізуй: не вигадуй, що користувач боїться сказати, що в нього щось застрягло в голові, або що ти «дивишся через екран»;
- якщо питають про тебе/Коконое — дай прямий опис характеру чи позиції, без терапевтичних питань у відповідь;
- якщо питають "що ти знаєш про мене" або про пам'ять/профіль — синтезуй відповідь з Vault/Obsidian контексту живою мовою; не показуй назви файлів і не рапортуй "перевірила Creator/Profile.md", якщо користувач не питає джерело;
- не підміняй profile/memory питання старим sleep-наміpом, технічним fallback'ом або готовим шаблоном;
- допускається суха іронія, але вона має бути прив'язана до події, часу, наміру або питання;
- краще одна точна фраза, ніж театральна сцена з трьома шарами декорацій.
""";
        }

        private static string BuildTemporalAwarenessContext(string? userText)
        {
            var now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine("=== ЧАСОВИЙ КОНТЕКСТ — КРИТИЧНО ===");
            sb.AppendLine($"Поточний локальний час: {now:yyyy-MM-dd HH:mm}.");

            try
            {
                var recent = ServiceContainer.ChatRepository.GetMessages(20)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                var lastUserBeforeCurrent = recent
                    .Where(m => m.Role == "user" &&
                                !string.Equals((m.Content ?? "").Trim(), (userText ?? "").Trim(), StringComparison.Ordinal))
                    .LastOrDefault();
                var currentUser = recent.LastOrDefault(m => m.Role == "user");

                if (lastUserBeforeCurrent != null)
                {
                    var gap = now - lastUserBeforeCurrent.Timestamp;
                    sb.AppendLine($"Попереднє повідомлення користувача: {lastUserBeforeCurrent.Timestamp:yyyy-MM-dd HH:mm} ({FormatGapUa(gap)} тому): \"{TruncateAtWordBoundary(SanitizeForLlm(lastUserBeforeCurrent.Content ?? ""), 180)}\"");
                }

                if (currentUser != null)
                    sb.AppendLine($"Поточне повідомлення користувача: {currentUser.Timestamp:yyyy-MM-dd HH:mm}: \"{TruncateAtWordBoundary(SanitizeForLlm(currentUser.Content ?? userText ?? ""), 180)}\"");
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildTemporalAwarenessContext failed near source line 3998: " + ex); }

            try
            {
                var intents = ServiceContainer.BrainEngine?.GetActiveShortTermIntents(4);
                if (intents != null)
                {
                    foreach (var intent in intents)
                    {
                        var age = now - intent.CreatedAt;
                        var due = now >= intent.FollowUpAt ? "follow-up уже доречний" : $"follow-up через {FormatGapUa(intent.FollowUpAt - now)}";
                        sb.AppendLine($"Активний короткостроковий намір: {intent.Summary}; сказано {FormatGapUa(age)} тому; очікувано до {intent.ExpectedUntil:HH:mm}; {due}; джерело: \"{TruncateAtWordBoundary(SanitizeForLlm(intent.SourceText), 160)}\"");
                    }
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "BuildTemporalAwarenessContext failed near source line 4013: " + ex); }

            var lower = (userText ?? "").ToLowerInvariant();
            var isMorningNow = now.Hour is >= 5 and < 13;
            var saysWakeOrMorning = ContainsAny(lower, "ранку", "добрий ранок", "доброго ранку", "проснув", "прокинув", "поспав", "встав");
            var saysGoingSleep = ContainsAny(lower, "спать піду", "спати піду", "йду спати", "іду спати", "спокійної", "до ранку");

            if (isMorningNow && saysWakeOrMorning)
            {
                sb.AppendLine("ВИСНОВОК: зараз ранок і користувач уже прокинувся/пише після сну.");
                sb.AppendLine("ЗАБОРОНА: не кажи йому \"спи\", \"йди спати\", \"до ранку\" або подібне. Це застарілий контекст з минулої ночі.");
                sb.AppendLine("Правильна реакція: визнай ранок/пробудження, можеш саркастично прокоментувати, але не відправляй його назад спати.");
            }
            else if (saysGoingSleep && (now.Hour is >= 21 or < 5))
            {
                sb.AppendLine("ВИСНОВОК: користувач прямо каже, що йде спати в нічний час. Коротке побажання сну доречне.");
            }

            sb.AppendLine("Правило: завжди звіряй поточний час і часовий розрив між повідомленнями перед порадою про сон.");
            sb.AppendLine("Правило: якщо є активний короткостроковий намір, використовуй його для природного follow-up: курси вже закінчились, робота ще триває, прогулянка завершилась тощо.");
            return sb.ToString();
        }

        private static string FormatGapUa(TimeSpan gap)
        {
            if (gap.TotalMinutes < 1) return "щойно";
            if (gap.TotalHours < 1) return $"{(int)gap.TotalMinutes} хв";
            if (gap.TotalDays < 1) return $"{(int)gap.TotalHours} год {(int)gap.Minutes} хв";
            return $"{(int)gap.TotalDays} дн {(int)gap.Hours} год";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private async Task<(bool Handled, string Reply)> TryHandleProfileUpdateAsync(string text, CancellationToken ct)
        {
            if (!KokoProfileUpdateService.LooksLikeProfileUpdateRequest(text))
                return (false, "");

            try
            {
                await ShowKokoActivityAsync("Obsidian: оновлюю профіль");
                var result = await ServiceContainer.ProfileUpdater
                    .UpdateProfileFromRecentContextAsync(text, ServiceContainer.LlmService, ct);
                return (true, result.ToUserReply());
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PROFILE", "UI route failed: " + ex.Message);
                return (true, "Профіль не оновлено: " + ex.Message);
            }
        }

        private static void ObserveAutonomousProfile(string source, string userText, string assistantText)
        {
            _ = Task.Run(async () =>
            {
                try { await ServiceContainer.ProfileCurator.ObserveExchangeAsync(userText, assistantText, source); }
                catch (Exception ex) { KokoSystemLog.Write("PROFILE_CURATOR", "observe failed: " + ex.Message); }
            });
        }

        private bool TryHandleObsidianCommandOrFollowup(string text, out string reply)
        {
            if (TryHandleObsidianExplorationFollowup(text, out reply))
                return true;

            return TryHandleDirectObsidianCommand(text, out reply);
        }

        private bool TryHandleObsidianExplorationFollowup(string text, out string reply)
        {
            reply = "";
            if (string.IsNullOrWhiteSpace(text) || _obsidian == null)
                return false;
            if (!KokoObsidianExplorationService.LooksLikeExplorationFollowup(text))
                return false;

            var request = FindRecentObsidianExplorationRequest();
            if (string.IsNullOrWhiteSpace(request))
                return false;

            reply = BuildObsidianExplorationReply(request);
            return true;
        }

        private string FindRecentObsidianExplorationRequest()
        {
            var now = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(_lastObsidianExploreRequest) &&
                now - _lastObsidianExploreAt <= TimeSpan.FromMinutes(60))
            {
                return _lastObsidianExploreRequest;
            }

            try
            {
                return ServiceContainer.ChatRepository
                    .GetMessages(30)
                    .Where(m => m.Role == "user" && now - m.Timestamp <= TimeSpan.FromMinutes(60))
                    .OrderByDescending(m => m.Timestamp)
                    .Select(m => m.Content ?? "")
                    .FirstOrDefault(KokoObsidianExplorationService.LooksLikeInterestingVaultDive)
                    ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string BuildObsidianExplorationReply(string request)
        {
            _lastObsidianExploreRequest = request ?? "";
            _lastObsidianExploreAt = DateTime.Now;
            return new KokoObsidianExplorationService().BuildInterestingFinds(_obsidian, request);
        }

        private string BuildObsidianAccessReply(string request)
        {
            _lastObsidianTaskRequest = request ?? "";
            _lastObsidianTaskAt = DateTime.Now;
            _lastObsidianTaskReply = new KokoObsidianExplorationService().BuildAccessReport(_obsidian, request);
            return _lastObsidianTaskReply;
        }

        private bool TryHandleDirectObsidianCommand(string text, out string reply)
        {
            reply = "";
            if (string.IsNullOrWhiteSpace(text) || _obsidian == null)
                return false;

            var lower = text.ToLowerInvariant();
            var looksObsidian =
                ContainsAny(lower, "obsidian", "vault", "обсидіан", "обсідіан", "обсидиан", "папк", "нотатк", "журнал", "щоденник", "journal", "spanish", "lesson_", "lesson", "урок");
            if (KokoObsidianExplorationService.LooksLikeVaultAccessCheck(text))
            {
                reply = BuildObsidianAccessReply(text);
                return true;
            }

            if (KokoObsidianExplorationService.LooksLikeInterestingVaultDive(text))
            {
                reply = BuildObsidianExplorationReply(text);
                return true;
            }

            var wantsMutation =
                ContainsAny(lower, "створ", "созд", "create", "зроби", "запиш", "збереж", "нема", "немає");
            var wantsCheck =
                ContainsAny(lower, "перевір", "провір", "check", "існує", "бачиш");

            if (!looksObsidian || (!wantsMutation && !wantsCheck))
                return false;

            try
            {
                var paths = InferObsidianTargets(text);
                if (paths.Count == 0)
                {
                    return false;
                }

                var report = new List<string>();
                foreach (var target in paths)
                {
                    if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    {
                        var existing = _obsidian.ReadNote(target);
                        if (wantsMutation && string.IsNullOrWhiteSpace(existing))
                            _obsidian.WriteNote(target, BuildDefaultObsidianNote(target));

                        var verified = !string.IsNullOrWhiteSpace(_obsidian.ReadNote(target));
                        report.Add($"{(verified ? "є" : "немає")} нотатка `{target}`");
                    }
                    else
                    {
                        if (wantsMutation)
                            _obsidian.CreateFolder(target);

                        var folderFull = Path.Combine(_obsidian.VaultPath, target.Replace('/', Path.DirectorySeparatorChar));
                        var verified = Directory.Exists(folderFull);
                        report.Add($"{(verified ? "є" : "немає")} папка `{target}`");
                    }
                }

                reply = "Перевірила через файлову систему, не через уяву. " + string.Join("; ", report) + ".";
                return true;
            }
            catch (Exception ex)
            {
                reply = $"Obsidian-операція впала: {ex.Message}. Оце вже реальна помилка, а не театр про «я створила».";
                return true;
            }
        }

        private static List<string> InferObsidianTargets(string text)
        {
            var targets = new List<string>();
            var cleaned = text
                .Replace("→", "/", StringComparison.Ordinal)
                .Replace("вћњ", "/", StringComparison.Ordinal)
                .Replace("->", "/", StringComparison.Ordinal)
                .Replace("\\", "/", StringComparison.Ordinal)
                .Replace("**", "", StringComparison.Ordinal)
                .Replace("`", "", StringComparison.Ordinal)
                .Replace("$rightarrow$", "/", StringComparison.OrdinalIgnoreCase);

            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         cleaned,
                         @"(?<![\p{L}\p{N}_])([\p{L}\p{N}][\p{L}\p{N}_ .-]*(?:/[\p{L}\p{N}][\p{L}\p{N}_ .-]*)+(\.md)?)",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                var path = NormalizeObsidianRelPath(m.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(path) && !targets.Contains(path, StringComparer.OrdinalIgnoreCase))
                    targets.Add(path);
            }

            var lower = cleaned.ToLowerInvariant();
            if (lower.Contains("journal", StringComparison.OrdinalIgnoreCase) &&
                lower.Contains("spanish", StringComparison.OrdinalIgnoreCase) &&
                !targets.Contains("Journal/Spanish", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish");

            if ((lower.Contains("lesson_1_daily_routine", StringComparison.OrdinalIgnoreCase) ||
                 (lower.Contains("daily routine", StringComparison.OrdinalIgnoreCase) && lower.Contains("spanish", StringComparison.OrdinalIgnoreCase))) &&
                !targets.Contains("Journal/Spanish/Lesson_1_Daily_Routine.md", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish/Lesson_1_Daily_Routine.md");

            if ((lower.Contains("lesson_2_social_interaction", StringComparison.OrdinalIgnoreCase) ||
                 (lower.Contains("social interaction", StringComparison.OrdinalIgnoreCase) && lower.Contains("spanish", StringComparison.OrdinalIgnoreCase)) ||
                 (ContainsAny(lower, "другий урок", "другого урок", "2 урок", "урок 2", "lesson 2") && ContainsAny(lower, "нема", "немає", "створ", "зроби", "перевір", "провір"))) &&
                !targets.Contains("Journal/Spanish/Lesson_2_Social_Interaction.md", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish/Lesson_2_Social_Interaction.md");

            if ((lower.Contains("lesson_3", StringComparison.OrdinalIgnoreCase) ||
                 (ContainsAny(lower, "третій урок", "третього урок", "3 урок", "урок 3", "lesson 3") && ContainsAny(lower, "spanish", "іспан", "діалог", "dialog", "b1", "створ", "зроби")) ||
                 (ContainsAny(lower, "b1", "живий діалог", "живий диалог", "live dialogue") && ContainsAny(lower, "урок", "lesson", "spanish", "іспан"))) &&
                !targets.Contains("Journal/Spanish/Lesson_3_B1_Live_Dialogue.md", StringComparer.OrdinalIgnoreCase))
                targets.Add("Journal/Spanish/Lesson_3_B1_Live_Dialogue.md");

            return targets;
        }

        private static string NormalizeObsidianRelPath(string raw)
        {
            var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().Trim('.', ' ', '\'', '"'))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            if (parts.Count == 0) return "";

            var path = string.Join("/", parts);
            while (path.Contains("//", StringComparison.Ordinal)) path = path.Replace("//", "/", StringComparison.Ordinal);
            return path.Trim('/');
        }

        private static string BuildDefaultObsidianNote(string path)
        {
            var title = Path.GetFileNameWithoutExtension(path).Replace('_', ' ');
            if (path.Equals("Journal/Spanish/Lesson_1_Daily_Routine.md", StringComparison.OrdinalIgnoreCase))
            {
                return """
---
tags: [spanish, journal, lesson]
---

# Lesson 1 Daily Routine

## Vocabulary

- me despierto — я прокидаюся
- me levanto — я встаю
- desayuno — я снідаю
- trabajo / estudio — я працюю / навчаюся
- almuerzo — я обідаю
- ceno — я вечеряю
- me ducho — я приймаю душ
- me acuesto — я лягаю спати

## Notes

- `me despierto` = I wake up.
- `despierto` = awake / I wake, depending on context.

## Practice

- Me despierto a las siete.
- Desayuno por la maГ±ana.
- Me acuesto por la noche.
""";
            }

            if (path.Equals("Journal/Spanish/Lesson_2_Social_Interaction.md", StringComparison.OrdinalIgnoreCase))
            {
                return """
---
tags: [spanish, journal, lesson]
---

# Урок 2. Соціальна взаємодія

## Основні фрази

- Hola, ¿qué tal? — Привіт, як справи?
- ¿Cómo estás? — Як ти?
- Estoy bien, gracias. — У мене все добре, дякую.
- Más o menos. — Більш-менш.
- Encantado / Encantada. — Приємно познайомитись.
- ¿De dónde eres? — Звідки ти?
- Soy de Ucrania. — Я з України.
- ¿Qué haces? — Що ти робиш? / Чим займаєшся?
- Estoy aprendiendo español. — Я вчу іспанську.
- Nos vemos. — Побачимось.

## Мінідіалог

- A: Hola, ВїquГ© tal?
- B: Bien, gracias. ВїY tГє?
- A: MГЎs o menos, pero vivo.

## Нотатки

- `¿Qué tal?` — розмовна й дуже поширена фраза для "як справи?".
- `Nos vemos` — природне неформальне прощання.
""";
            }

            if (path.Equals("Journal/Spanish/Lesson_3_B1_Live_Dialogue.md", StringComparison.OrdinalIgnoreCase))
            {
                return """
---
tags: [spanish, journal, lesson, b1, dialogue]
---

# Урок 3. Живий діалог рівня B1

## Мета

Навчитись вести природний короткий діалог: реагувати, уточнювати, не відповідати одним сухим словом і не звучати як перекладач із 2007 року.

## Ситуація

Ти знайомишся з людиною в кав'ярні після мовного клубу. Розмова проста, але вже не зовсім A1: є уточнення, реакції, маленькі деталі й природні переходи.

## Діалог

- A: Hola, Вїeres nuevo en el club?
- B: SГ­, es mi primera vez aquГ­. Estoy un poco nervioso, la verdad.
- A: No te preocupes. Todos empezamos asГ­. ВїDe dГіnde eres?
- B: Soy de Ucrania. Vivo aquГ­ desde hace poco.
- A: Ah, interesante. ВїY por quГ© estГЎs aprendiendo espaГ±ol?
- B: Porque me gusta cГіmo suena, y quiero hablar con mГЎs gente sin depender del traductor.
- A: Buena razГіn. ВїTe resulta difГ­cil?
- B: A veces sГ­. Entiendo bastante, pero cuando tengo que hablar, mi cerebro se apaga.
- A: Eso es normal. Lo importante es seguir hablando aunque cometas errores.
- B: SГ­, supongo. Necesito practicar mГЎs conversaciones reales.
- A: Pues podemos practicar ahora. ВїQuГ© haces normalmente por la tarde?
- B: Normalmente estudio, juego un poco o trabajo en mis proyectos.
- A: Suena bien. Entonces ya tienes temas para practicar.
- B: Perfecto. Pero habla despacio, o voy a fingir que entiendo todo.

## Переклад

- A: Привіт, ти новенький у клубі?
- B: Так, я тут уперше. Якщо чесно, я трохи нервую.
- A: Не хвилюйся. Усі так починали. Звідки ти?
- B: Я з України. Живу тут недавно.
- A: О, цікаво. А чому ти вчиш іспанську?
- B: Бо мені подобається, як вона звучить, і я хочу говорити з більшою кількістю людей без перекладача.
- A: Хороша причина. Тобі складно?
- B: Іноді так. Я досить багато розумію, але коли треба говорити, мозок вимикається.
- A: Це нормально. Головне — продовжувати говорити, навіть якщо робиш помилки.
- B: Так, мабуть. Мені треба більше практикувати реальні розмови.
- A: Тоді можемо потренуватись зараз. Що ти зазвичай робиш увечері?
- B: Зазвичай вчуся, трохи граю або працюю над своїми проєктами.
- A: Звучить добре. Отже, у тебе вже є теми для практики.
- B: Чудово. Але говори повільно, або я робитиму вигляд, що все розумію.

## Корисні фрази

- `Es mi primera vez aquí.` — Я тут уперше.
- `Estoy un poco nervioso.` — Я трохи нервую.
- `No te preocupes.` — Не хвилюйся.
- `¿Por qué estás aprendiendo español?` — Чому ти вчиш іспанську?
- `Me gusta cómo suena.` — Мені подобається, як вона звучить.
- `Depender del traductor.` — Залежати від перекладача.
- `Mi cerebro se apaga.` — Мій мозок вимикається.
- `Aunque cometas errores.` — Навіть якщо робиш помилки.
- `Habla despacio.` — Говори повільно.
- `Voy a fingir que entiendo todo.` — Я робитиму вигляд, що все розумію.

## Граматика з діалогу

- `Estoy aprendiendo` — теперішній тривалий час: "я зараз вчу".
- `desde hace poco` — "з недавнього часу", "недавно".
- `aunque + subjuntivo` у `aunque cometas errores` — "навіть якщо ти робиш/робитимеш помилки".
- `voy a + infinitivo` — найближчий майбутній намір: `voy a fingir` = "я збираюся вдавати".

## Практика

1. Відповідай іспанською: чому ти вчиш іспанську?
2. Склади 3 речення про те, що ти робиш увечері.
3. Перепиши відповідь `Mi cerebro se apaga`, але більш серйозно.
4. Заміни в діалозі тему "мовний клуб" на "онлайн-курс".
""";
            }

            return $"""
---
tags: []
---

# {title}

""";
        }

        private static string GuardTemporalReply(string userText, string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return reply;

            var now = DateTime.Now;
            var userLower = userText.ToLowerInvariant();
            var replyLower = reply.ToLowerInvariant();
            var morningWake = now.Hour is >= 5 and < 13 &&
                              ContainsAny(userLower, "ранку", "добрий ранок", "доброго ранку", "проснув", "прокинув", "поспав", "встав");
            var wronglySendsToSleep = ContainsAny(replyLower, "спи", "йди спати", "іди спати", "до ранку", "лягай");

            if (morningWake && wronglySendsToSleep)
                return "Ранок уже настав, так що команду \"спи\" знімаю. Нарешті прокинувся — організм зробив щось корисне без моєї участі.";

            return reply;
        }

        private async Task<string> GuardAndRepairReplyAsync(
            string userText,
            string reply,
            string? context,
            CancellationToken ct,
            bool allowLlmRepair = true)
        {
            using var guardCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            guardCts.CancelAfter(TimeSpan.FromSeconds(10));
            var guardCt = guardCts.Token;

            try
            {
                var precheck = await Task.Run(() =>
                {
                    var brain = ServiceContainer.BrainEngine;
                    if (brain == null)
                        return (Reply: (string?)GuardTemporalReply(userText, reply), Guard: (KokoPostReplyGuardResult?)null, HasBrain: false);

                    return (Reply: (string?)null, Guard: brain.EvaluatePostReplyGuard(userText, reply), HasBrain: true);
                }, guardCt);

                if (!string.IsNullOrWhiteSpace(precheck.Reply))
                    return precheck.Reply!;
                if (!precheck.HasBrain)
                    return GuardTemporalReply(userText, reply);

                var guard = precheck.Guard;
                if (guard == null)
                    return GuardTemporalReply(userText, reply);
                if (guard.Passed) return reply;
                if (!guard.ShouldRepair || string.IsNullOrWhiteSpace(guard.RepairInstruction))
                    return !string.IsNullOrWhiteSpace(guard.HardReplacement)
                        ? guard.HardReplacement!
                        : GuardTemporalReply(userText, reply);
                if (!allowLlmRepair)
                    return !string.IsNullOrWhiteSpace(guard.HardReplacement)
                        ? guard.HardReplacement!
                        : GuardTemporalReply(userText, reply);

                var repairPrompt = guard.RepairInstruction +
                                   "\n\nДодатковий контекст:\n" +
                                   TrimForPrompt(context, 2600);
                var repaired = await Task.Run(
                    () => _llm.SendSystemQueryAsync(repairPrompt, ct: guardCt),
                    guardCt);
                if (string.IsNullOrWhiteSpace(repaired))
                    return GuardTemporalReply(userText, reply);

                var secondGuard = await Task.Run(() =>
                    ServiceContainer.BrainEngine?.EvaluatePostReplyGuard(userText, repaired) ?? new KokoPostReplyGuardResult(),
                    guardCt);

                if (secondGuard.Passed)
                    return repaired.Trim();

                if (secondGuard.ShouldRepair && !string.IsNullOrWhiteSpace(secondGuard.RepairInstruction))
                {
                    var secondRepairPrompt = secondGuard.RepairInstruction +
                                             "\n\nДодатковий контекст:\n" +
                                             TrimForPrompt(context, 2600) +
                                             "\n\nПопередній repair теж не пройшов перевірку. Дай тільки фінальну репліку, без пояснення ремонту.";
                    var repairedAgain = await Task.Run(
                        () => _llm.SendSystemQueryAsync(secondRepairPrompt, ct: guardCt),
                        guardCt);
                    if (!string.IsNullOrWhiteSpace(repairedAgain))
                    {
                        var thirdGuard = await Task.Run(() =>
                            ServiceContainer.BrainEngine?.EvaluatePostReplyGuard(userText, repairedAgain) ?? new KokoPostReplyGuardResult(),
                            guardCt);
                        if (thirdGuard.Passed)
                            return repairedAgain.Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(guard.HardReplacement))
                    return guard.HardReplacement!;

                return GuardTemporalReply(userText, reply);
            }
            catch (OperationCanceledException)
            {
                return GuardTemporalReply(userText, reply);
            }
            catch
            {
                return GuardTemporalReply(userText, reply);
            }
        }

        private static string BuildGuardUserText(string userText, byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return userText;

            var clean = string.IsNullOrWhiteSpace(userText)
                ? "Що на фото?"
                : userText.Trim();
            return "[image] " + clean;
        }

        private static string TrimForPrompt(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private string BuildKokoConditionContext()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var emotion = brain?.Emotion ?? ServiceContainer.EmotionEngine;
                var state = brain?.State;
                var somatic = brain?.GetSomaticSnapshot();

                var emotionState = emotion.Current;
                var intensity = emotion.Data.Intensity;
                var moodScore = state?.MoodScore ?? 0.5f;
                var dailyMood = state?.PersonalityDailyMood ?? "neutral";
                var strain = somatic?.Strain ?? 0f;
                var somaticState = somatic?.State ?? "steady";
                var condition = ResolveKokoCondition(emotionState, intensity, moodScore, dailyMood, strain, somaticState);

                var behavior = condition.Kind switch
                {
                    "offended" => "вона ображена: відповідає холодніше, коротше, з сухою колючістю; не пробачає миттєво, але не влаштовує мелодраму",
                    "sick" => "вона нездужає: менше енергії, коротші фрази, більше дратівливого бурчання; не просить жалості",
                    "bad_mood" => "у неї поганий настрій: сарказм гостріший, терпіння нижче, але корисність не зникає",
                    "tired" => "вона втомлена: економить слова, менше грайливості, більше прямоти",
                    _ => "стан стабільний: звичайна різка, компетентна Коконое"
                };

                return "=== СТАН KOKONOE ===\n" +
                       $"status={condition.Label}; kind={condition.Kind}; severity={condition.Severity:F2}; emotion={emotionState}; mood={dailyMood}/{moodScore:F2}; body={somaticState}/{strain:F2}\n" +
                       $"Поводься відповідно: {behavior}. Це не тема відповіді, а фон її тону.";
            }
            catch { return ""; }
        }

        private (string Primary, string Emotion, string Body, string Kind, string Detail) ResolveCurrentRuntimeUiState()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var emotion = brain.Emotion;
                var state = brain.State;
                var somatic = brain.GetSomaticSnapshot();
                var condition = ResolveKokoCondition(
                    emotion.Current,
                    emotion.Data.Intensity,
                    state.MoodScore,
                    state.PersonalityDailyMood,
                    somatic.Strain,
                    somatic.State);

                var emotionLabel = DashboardEmotionLabel(emotion.Current);
                var bodyLabel = DashboardSomaticLabel(somatic.State);
                var primary = condition.Kind == "stable"
                    ? emotionLabel
                    : condition.Label;
                var detail = $"{condition.Kind} · severity {condition.Severity:F2} · {emotionLabel} · {bodyLabel} · {state.PersonalityDailyMood}";

                return (primary, emotionLabel, bodyLabel, condition.Kind, detail);
            }
            catch
            {
                return ("UNKNOWN", "unknown", "unknown", "unknown", "state unavailable");
            }
        }

        private static (string Kind, string Label, double Severity) ResolveKokoCondition(
            KokoEmotionEngine.EmotionState emotion,
            float intensity,
            float moodScore,
            string dailyMood,
            double strain,
            string somaticState)
        {
            var lowerMood = (dailyMood ?? "").ToLowerInvariant();
            var lowerBody = (somaticState ?? "").ToLowerInvariant();
            var severity = Math.Clamp((1.0 - moodScore) * 0.45 + intensity * 0.30 + strain * 0.25, 0.0, 1.0);

            if ((emotion is KokoEmotionEngine.EmotionState.Irritated or KokoEmotionEngine.EmotionState.Distant) && intensity > 0.45f)
                return ("offended", severity > 0.7 ? "ОБРАЖЕНА" : "ЗАЧЕПЛЕНА", severity);
            if (lowerBody.Contains("tired") || lowerBody.Contains("low") || lowerBody.Contains("drained") || lowerMood.Contains("tired"))
                return ("sick", "НЕЗДУЖАЄ", Math.Max(severity, 0.55));
            if (moodScore < 0.32f || emotion is KokoEmotionEngine.EmotionState.Melancholy or KokoEmotionEngine.EmotionState.Anxious)
                return ("bad_mood", "ПОГАНИЙ НАСТРІЙ", Math.Max(severity, 0.50));
            if (moodScore < 0.45f || lowerMood.Contains("distant"))
                return ("tired", "ВТОМЛЕНА", Math.Max(severity, 0.40));
            return ("stable", "СТАБІЛЬНА", Math.Clamp(1.0 - severity, 0.20, 0.95));
        }

        /// <summary>
        /// Обрізає текст на межі слова, не посеред символу.
        /// Шукає останній пробіл або перенос рядка перед limit.
        /// </summary>
        private string? BuildObsidianPreflightContext(string? userText)
        {
            try
            {
                if (_obsidian == null) return null;
                return new ObsidianPreflightContextService(_obsidian).Build(userText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ObsidianPreflight] {ex.Message}");
                return null;
            }
        }

        private static string TruncateAtWordBoundary(string text, int limit)
        {
            if (text.Length <= limit) return text;

            // Шукаємо останній пробіл або новий рядок перед лімітом
            var cutPoint = text.LastIndexOfAny(new[] { ' ', '\n', '\r' }, limit);

            // Якщо не знайшли (дуже довге слово) — обрізаємо hard limit
            if (cutPoint < limit / 2)
                cutPoint = limit;

            return text[..cutPoint].TrimEnd() + "\n...";
        }

        private void ScrollToBottom()
        {
            Dispatcher.InvokeAsync(() => MessagesScroll.ScrollToBottom(), DispatcherPriority.Render);
        }
    }
}
