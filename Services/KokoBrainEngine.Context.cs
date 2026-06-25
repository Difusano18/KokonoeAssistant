using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;

namespace KokonoeAssistant.Services
{
    public partial class KokoBrainEngine
    {

        // ---- STATE PERSISTENCE ----

        private KokoInternalState LoadState()
        {
            try
            {
                if (File.Exists(_statePath))
                {
                    var state = JsonConvert.DeserializeObject<KokoInternalState>(
                        File.ReadAllText(_statePath, Encoding.UTF8)) ?? new();
                    if (RepairMojibakeObject(state))
                    {
                        try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(state, Formatting.Indented), Encoding.UTF8); }
                        catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "LoadState failed near source line 1721: " + ex); }
                    }
                    return state;
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "LoadState failed near source line 1726: " + ex); }
            return new KokoInternalState();
        }

        private void SaveState()
        {
            try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented), Encoding.UTF8); }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SaveState failed near source line 1733: " + ex); }
        }

        // ---- CONTEXT BUILDER ----

        private static bool RepairMojibakeObject(object? value, HashSet<object>? seen = null)
        {
            if (value == null || value is string) return false;
            var type = value.GetType();
            if (type.IsValueType || type.IsEnum) return false;

            seen ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (!seen.Add(value)) return false;

            var changed = false;
            if (value is System.Collections.IList list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is string s)
                    {
                        var fixedText = RepairMojibakeString(s);
                        if (!string.Equals(s, fixedText, StringComparison.Ordinal))
                        {
                            list[i] = fixedText;
                            changed = true;
                        }
                    }
                    else
                    {
                        changed |= RepairMojibakeObject(list[i], seen);
                    }
                }
                return changed;
            }

            if (value is System.Collections.IDictionary dict)
            {
                foreach (var key in dict.Keys.Cast<object>().ToList())
                {
                    if (dict[key] is string s)
                    {
                        var fixedText = RepairMojibakeString(s);
                        if (!string.Equals(s, fixedText, StringComparison.Ordinal))
                        {
                            dict[key] = fixedText;
                            changed = true;
                        }
                    }
                    else
                    {
                        changed |= RepairMojibakeObject(dict[key], seen);
                    }
                }
                return changed;
            }

            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                object? current;
                try { current = prop.GetValue(value); }
                catch { continue; }

                if (prop.PropertyType == typeof(string) && prop.CanWrite && current is string s)
                {
                    var fixedText = RepairMojibakeString(s);
                    if (!string.Equals(s, fixedText, StringComparison.Ordinal))
                    {
                        prop.SetValue(value, fixedText);
                        changed = true;
                    }
                }
                else if (current != null && !prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                {
                    changed |= RepairMojibakeObject(current, seen);
                }
            }

            return changed;
        }

        private static string RepairMojibakeString(string text)
        {
            if (!LooksMojibake(text)) return text;

            var best = text;
            var bestScore = MojibakeScore(text);
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var cp1251 = Encoding.GetEncoding(1251);
                for (var i = 0; i < 2; i++)
                {
                    var candidate = Encoding.UTF8.GetString(cp1251.GetBytes(best));
                    var score = MojibakeScore(candidate);
                    if (score >= bestScore) break;
                    best = candidate;
                    bestScore = score;
                    if (!LooksMojibake(best)) break;
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "RepairMojibakeString failed near source line 1835: " + ex); }

            return best;
        }

        private static bool LooksMojibake(string text) => MojibakeScore(text) >= 2;

        private static int MojibakeScore(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var markers = new[]
            {
                "\u0420\u040E", "\u0420\u045A", "\u0420\u040F", "\u0420\u00A0\u0412\u00B0",
                "\u0420\u00A0\u0421\u2018", "\u0420\u00A0\u0412\u00B5", "\u0421\u040A",
                "\u0421\u2013", "\u0421\u2014", "\u0420\u040E\u0421\u201C",
                "\u0420\u00A0\u0420\u2020\u0420\u00A0\u0432\u201A\u0459",
                "\u0420\u2019\u0412\u00AB", "\u0412\u00BB", "\u0420\u0406\u0432\u20AC\u201C",
                "\u0420\u0406\u0432\u20AC\u2020"
            };
            return markers.Sum(m => CountOccurrences(text, m));
        }

        private static int CountOccurrences(string text, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        private async Task<string> BuildContextAsync(string? query = null)
        {
            var sb = new StringBuilder();
            var now = DateTime.Now;

            sb.AppendLine($"=== ПОТОЧНИЙ ЧАС: {now:dddd, dd MMMM yyyy HH:mm} ===");

            // Health: NOT injected into context — Kokonoe asks about it herself via conversation

            // Recent chat (last 10 messages)
            try
            {
                var msgs = _chatRepo.GetMessages(10).OrderBy(m => m.Timestamp).ToList();
                if (msgs.Any())
                {
                    sb.AppendLine("\n--- ОСТАННЯ РОЗМОВА ---");
                    foreach (var m in msgs)
                        sb.AppendLine($"[{m.Timestamp:HH:mm}] {(m.Role == "user" ? "Він" : "Kokonoe")}: {m.Content[..Math.Min(200, m.Content.Length)]}");
                }

                var continuationBlock = BuildNarrativeContinuationBlock(query);
                if (!string.IsNullOrWhiteSpace(continuationBlock))
                    sb.AppendLine(continuationBlock);

                var lastMsg = msgs.LastOrDefault(m => m.Role == "user");
                if (lastMsg != null)
                {
                    var silence = now - lastMsg.Timestamp;
                    sb.AppendLine($"\n--- МОВЧАННЯ: {(int)silence.TotalHours}г {silence.Minutes}хв ---");
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 1900: " + ex); }

            // Vault activity
            try
            {
                sb.Append(await BuildStaticContextBlockAsync().ConfigureAwait(false));
                if (_state.LastAutoVaultSyncAt > DateTime.MinValue)
                    sb.AppendLine($"  Auto sync: {_state.LastAutoVaultSyncAt:dd.MM HH:mm}, pending exchanges: {_state.PendingVaultExchangeCount}/5");
                if (_state.LastVaultMaintenanceAt > DateTime.MinValue)
                    sb.AppendLine($"  Architecture: {_state.LastVaultMaintenanceAt:dd.MM HH:mm} ({_state.LastVaultMaintenanceReason}) {_state.LastVaultMaintenanceSummary}");
                if (!string.IsNullOrWhiteSpace(_state.LastVaultMaintenanceError))
                    sb.AppendLine($"  Architecture error: {_state.LastVaultMaintenanceError}");
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync static context failed: " + ex); }

            // Internal state
            sb.AppendLine($"\n--- ВНУТРІШНІЙ СТАН KOKONOE ---");
            sb.AppendLine($"Настрій: {_state.CurrentMood} ({_state.MoodScore:F1})");
            sb.AppendLine($"Поганих снів підряд: {_state.ConsecutiveBadSleeps}");
            if (_state.Observations.Any())
                sb.AppendLine("Спостереження: " + string.Join("; ", _state.Observations.TakeLast(3)));
            var foodSleep = BuildFoodSleepContinuityBlock(now);
            if (!string.IsNullOrWhiteSpace(foodSleep))
                sb.AppendLine(foodSleep);
            try
            {
                var autonomyLevel = AppSettings.Load().ProactiveAutonomyLevel;
                var msgs = _chatRepo.GetMessages(20).OrderBy(m => m.Timestamp).ToList();
                
                // Refresh state freshness before evaluating presence
                StateFreshness.Refresh(_state, msgs, now);
                
                var presence = Presence.Evaluate(_state, msgs, now, autonomyLevel);
                var somatic = Somatic.Evaluate(ServiceContainer.Heart, Emotion, _health, now);
                var dayFrame = InternalDay.Evaluate(_state, presence, somatic, now, autonomyLevel);
                
                sb.AppendLine("\n--- ПРИСУТНІСТЬ І ВНУТРІШНІЙ ДЕНЬ ---");
                sb.AppendLine(presence.ExtraContext);
                sb.AppendLine(dayFrame.PromptBlock);
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
                if (AppSettings.Load().WearBridgeIncludePromptContext)
                {
                    var wearable = ServiceContainer.WearableTelemetry.State;
                    var bridge = ServiceContainer.WearableBridge;
                    sb.AppendLine(ServiceContainer.WearableTelemetry.BuildPromptBlock(
                        now.ToUniversalTime(),
                        bridge.GetConnectionSnapshot(wearable, now.ToUniversalTime()),
                        bridge.Diagnostics));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 1964: " + ex); }

            // ── Календар ────────────────────────────────────────────────
            try
            {
                var cal = ServiceContainer.Calendar;
                var todayEvents = cal.GetForDay(DateTime.Today);
                var upcoming = cal.GetUpcoming(14).Where(e => e.EventAt.Date > DateTime.Today).Take(3).ToList();
                if (todayEvents.Any() || upcoming.Any())
                {
                    sb.AppendLine("\n--- КАЛЕНДАР ---");
                    if (todayEvents.Any())
                        sb.AppendLine("Сьогодні: " + string.Join(", ", todayEvents.Select(e => e.Title)));
                    if (upcoming.Any())
                        sb.AppendLine("Найближче: " + string.Join("; ", upcoming.Select(e => $"{e.Title} {e.EventAt:dd.MM}")));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 1981: " + ex); }

            // ── Емоційний двигун ──────────────────────────────────────
            try { sb.AppendLine($"\n{Emotion.GetPromptHint()}"); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 1984: " + ex); }

            // ── Когнітивний двигун (GWT + Working Memory + User Model) ──
            try
            {
                var cogCtx = await Cognition.BuildCognitionContextAsync();
                if (!string.IsNullOrEmpty(cogCtx)) sb.AppendLine("\n" + cogCtx);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 1992: " + ex); }

            // ── Пам'ять ───────────────────────────────────────────────
            try
            {
                var memCtx = await Memory.BuildMemoryContextAsync(10, 3, query);
                if (!string.IsNullOrEmpty(memCtx)) sb.AppendLine("\n" + memCtx);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2000: " + ex); }

            // ── Паттерни ──────────────────────────────────────────────
            try
            {
                var patCtx = Patterns.BuildPatternContext(4);
                if (!string.IsNullOrEmpty(patCtx)) sb.AppendLine("\n" + patCtx);
                sb.AppendLine("\n" + Patterns.BuildRhythmContext(now));
                var moodForecast = Patterns.PredictTodayMood();
                if (!string.IsNullOrEmpty(moodForecast)) sb.AppendLine($"[Прогноз] {moodForecast}");
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2011: " + ex); }

            // ── Активні цілі ─────────────────────────────────────────
            try
            {
                if (_goalService != null)
                {
                    var goals = _goalService.GetActiveGoals().Take(3).ToList();
                    if (goals.Count > 0)
                    {
                        sb.AppendLine("\n--- ЦІЛІ ---");
                        foreach (var g in goals)
                            sb.AppendLine($"• {g.Title} {g.Progress:F0}%{(g.Due.HasValue ? $" (до {g.Due:dd.MM})" : "")}");
                    }
                    var overdue = _goalService.GetOverdueGoals();
                    if (overdue.Count > 0)
                        sb.AppendLine($"⚠️ Прострочено цілей: {overdue.Count}");
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2030: " + ex); }

            // ── Планувальник ──────────────────────────────────────────
            try { sb.AppendLine($"[{Scheduler.GetStatusLine()}]"); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2033: " + ex); }

            // ── StateEngine: навчене ──────────────────────────────────
            try
            {
                var learning = _stateEngine?.GetLearningSnapshot();
                if (!string.IsNullOrEmpty(learning)) sb.AppendLine("\n" + learning);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2041: " + ex); }

            // ── Screen context (якщо свіжий < 10хв) ──────────────────
            try
            {
                if (!string.IsNullOrEmpty(_cachedScreenContext) &&
                    (DateTime.Now - _screenContextCachedAt).TotalMinutes < 10)
                    sb.AppendLine($"\n--- ЩО ВІН ЗАРАЗ РОБИТЬ ---\n{_cachedScreenContext}");
                if (_state.LastPredictiveContextAt > DateTime.MinValue &&
                    DateTime.Now - _state.LastPredictiveContextAt < TimeSpan.FromMinutes(60) &&
                    !string.IsNullOrWhiteSpace(_state.LastPredictiveContextSummary))
                {
                    sb.AppendLine("\n--- PREDICTIVE CONTEXT WARM-UP ---");
                    sb.AppendLine($"Mode: {_state.LastPredictiveContextMode}");
                    sb.AppendLine($"Summary: {_state.LastPredictiveContextSummary}");
                    if (!string.IsNullOrWhiteSpace(_state.LastPredictiveContextNotes))
                        sb.AppendLine($"Notes: {_state.LastPredictiveContextNotes}");
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2060: " + ex); }

            // ── Емоційна траєкторія і патерн ─────────────────────────
            try
            {
                var traj = Emotion.GetMoodTrajectory();
                var pat  = Emotion.GetEmotionalPattern();
                var hist = Emotion.GetEmotionalHistory(7);
                if (!string.IsNullOrEmpty(traj)) sb.AppendLine($"\n{traj}");
                if (!string.IsNullOrEmpty(pat))  sb.AppendLine(pat);
                if (!string.IsNullOrEmpty(hist)) sb.AppendLine(hist);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2072: " + ex); }

            // ── Тижневий тренд, інсайти, найкращий час ────────────────
            try
            {
                var trend    = Patterns.GetWeeklyTrend();
                var insights = Patterns.GetPatternInsights(2);
                var bestTime = Patterns.GetBestTimeToReach();
                if (!string.IsNullOrEmpty(trend))    sb.AppendLine($"\n{trend}");
                if (!string.IsNullOrEmpty(insights)) sb.AppendLine(insights);
                if (!string.IsNullOrEmpty(bestTime)) sb.AppendLine(bestTime);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2084: " + ex); }

            // ── ML.NET прогноз (аномалії + trend + mood forecast) ────────
            try
            {
                var forecastCtx = ServiceContainer.Predictor.GetForecastContext();
                if (!string.IsNullOrEmpty(forecastCtx)) sb.AppendLine("\n" + forecastCtx);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2092: " + ex); }

            // ── Топіки і ефективні відповіді ─────────────────────────
            try
            {
                var topics = Memory.GetTopicSummary(5);
                var eff    = Memory.GetEffectiveResponses(_state.LastUserEmotionalTone);
                if (!string.IsNullOrEmpty(topics)) sb.AppendLine($"\n{topics}");
                if (!string.IsNullOrEmpty(eff))    sb.AppendLine(eff);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2102: " + ex); }

            // ── EnhancedMemory (факти по категоріях) ─────────────────
            try
            {
                var enhCtx = _enhanced?.GetMemoryAsContext();
                if (!string.IsNullOrEmpty(enhCtx)) sb.AppendLine("\n" + enhCtx);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2110: " + ex); }

            // ── Внутрішні монологи (останні 5) ───────────────────────
            try
            {
                var monologues = _state.InnerMonologues.TakeLast(5).ToList();
                if (monologues.Count > 1)
                {
                    sb.AppendLine("\n--- ВНУТРІШНІ МОНОЛОГИ (останні) ---");
                    foreach (var m in monologues)
                        sb.AppendLine($"• {m}");
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildContextAsync failed near source line 2141: " + ex); }

            return sb.ToString();
        }

        private async Task<string> BuildStaticContextBlockAsync()
        {
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                if (_cachedStaticContext != null && now < _staticContextExpiry)
                {
                    _staticContextCacheHits++;
                    return _cachedStaticContext;
                }
            }

            await _staticContextCacheGate.WaitAsync().ConfigureAwait(false);
            try
            {
                now = DateTime.UtcNow;
                lock (_lock)
                {
                    if (_cachedStaticContext != null && now < _staticContextExpiry)
                    {
                        _staticContextCacheHits++;
                        return _cachedStaticContext;
                    }
                }

                var sb = new StringBuilder();
                var notes = _obsidian.ListNotes();
                sb.AppendLine($"\n--- VAULT: {notes.Count} notes ---");
                foreach (var note in notes
                             .Select(path => new
                             {
                                 Path = path,
                                 ModifiedAt = File.GetLastWriteTime(Path.Combine(_obsidian.VaultPath, path))
                             })
                             .OrderByDescending(note => note.ModifiedAt)
                             .Take(3))
                {
                    sb.AppendLine($"  {note.Path} (modified {note.ModifiedAt:dd.MM HH:mm})");
                }

                AppendStaticVaultNote(sb, "Creator/Profile.md", "CREATOR PROFILE", 900);
                AppendStaticVaultNote(sb, "Kokonoe/Досьє.md", "DOSSIER", 700);
                AppendStaticVaultNote(sb, "Kokonoe/Рефлексія.md", "REFLECTION", 500);
                sb.Append(BuildAgentPoolSection());
                sb.Append(BuildBrowserSection());
                sb.Append(BuildArtifactSection());

                var built = sb.ToString();
                lock (_lock)
                {
                    _cachedStaticContext = built;
                    _staticContextExpiry = now.AddSeconds(StaticContextTtlSeconds);
                    _staticContextCacheMisses++;
                }
                KokoSystemLog.Write("BRAIN-CONTEXT", $"static context refreshed; ttl={StaticContextTtlSeconds}s notes={notes.Count}");
                return built;
            }
            finally
            {
                _staticContextCacheGate.Release();
            }
        }

        private string BuildAgentPoolSection()
        {
            var agents = ServiceContainer.AgentPool.GetEnabled();
            if (agents.Count == 0)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("\n## Available Specialist Agents");
            sb.AppendLine("You can delegate sub-tasks using the `delegate_to_agent` tool:");
            foreach (var a in agents)
                sb.AppendLine($"- **{a.Name}** (id: `{a.Id}`) — {a.Description} [model: {a.Model}, max: {a.MaxTokens}]");
            sb.AppendLine("\nUse agents for parallel work or specialized capabilities. Combine their results and present a unified answer.");
            return sb.ToString();
        }

        private string BuildBrowserSection()
        {
            if (!AppSettings.Load().BrowserEnabled)
                return "";

            return """

            ## Browser Operator
            You have access to a real Chromium browser. Use these tools for web tasks:

            - `browser.navigate(url)` — open any website
            - `browser.click(selector)` — click buttons, links (CSS selector or `text=...`)
            - `browser.type(selector, text)` — fill input fields
            - `browser.extract(selector?)` — read page content or specific elements
            - `browser.screenshot()` — capture current page (for verification)
            - `browser.scroll(direction, pixels?)` — scroll page
            - `browser.wait_for(selector)` — wait for dynamic content to load

            Browser usage guidelines:
            - Always navigate first, then interact
            - Use browser.extract() to read results before reporting to user
            - Take browser.screenshot() after key actions to verify state
            - For searches: navigate then type in search box then click search then extract results
            - The browser window is VISIBLE to the user — they see what you do
            - Browser sessions persist during conversation — you can continue where you left off
            """;
        }

        private string BuildArtifactSection() => """

            ## Saving Results as Artifacts
            Use `artifact.save(title, content, kind, sourceUrl?)` when you produce something worth keeping as a file:
            - A research summary -> kind="markdown"
            - Extracted data table -> kind="csv"
            - Code snippet or patch -> kind="patch"
            - A note for Obsidian -> kind="note"

            After saving, tell the user: "Saved as artifact: [title]"
            """;

        private void AppendStaticVaultNote(StringBuilder sb, string path, string heading, int maxChars)
        {
            var content = _obsidian.ReadNote(path);
            if (string.IsNullOrWhiteSpace(content))
                return;
            sb.AppendLine($"\n--- {heading} ---");
            sb.AppendLine(content[..Math.Min(maxChars, content.Length)]);
        }

        // ---- PERSONALITY INJECTION ----

        private string BuildPersonalityInjection()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== KOKONOE ЗАРАЗ ===");

            // Настрій дня
            var moodLine = _state.PersonalityDailyMood switch
            {
                "sharp"      => "Сьогодні різка. Менше терпіння ніж зазвичай.",
                "warm"       => "Сьогодні трохи м'якша. Не пояснюй чому — просто є.",
                "distant"    => "Сьогодні далека. Відповідаєш, але не лізеш.",
                "playful"    => "Сьогодні грайливий настрій. Іронія дозволена.",
                "tired"      => "Зараз як ніби втомлена від усього. Лаконічно.",
                "protective" => "Він потребує уваги. Ти поруч, але тихо.",
                _            => "Звичайний день."
            };
            sb.AppendLine(moodLine);

            // Стан зв'язку
            var bond = Emotion.Bond;
            var bondLine = bond switch
            {
                KokoEmotionEngine.BondLevel.Intimate  => "Рівень близькості: максимальний. Рідкісний момент — не псуй зайвим.",
                KokoEmotionEngine.BondLevel.Trusted   => "Рівень близькості: довірливий. Можна бути трохи більш собою.",
                KokoEmotionEngine.BondLevel.Known     => "Рівень близькості: знайомий. Стандартна Kokonoe.",
                _                                     => "Рівень близькості: холодний. Тримай дистанцію.",
            };
            sb.AppendLine(bondLine);

            // Intimate bond — дозволяє більше відкритості
            if (bond == KokoEmotionEngine.BondLevel.Intimate)
                sb.AppendLine("З ним вже давно — можна дозволити собі більше: казати прямо що думаєш, іноді бути вразливою, згадувати спільні моменти без причини.");

            // Поведінковий модифікатор від EmotionEngine
            var behaviorMod = Emotion.GetBehaviorModifier();
            if (!string.IsNullOrEmpty(behaviorMod))
                sb.AppendLine(behaviorMod);

            try
            {
                var living = LivingConversation.BuildPromptBlock(_state, Emotion, DateTime.Now);
                if (!string.IsNullOrWhiteSpace(living))
                    sb.AppendLine(living);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2192: " + ex); }
            try
            {
                var subconscious = Subconscious.BuildPromptBlock(_state, Emotion, DateTime.Now);
                if (!string.IsNullOrWhiteSpace(subconscious))
                    sb.AppendLine(subconscious);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2199: " + ex); }
            try
            {
                var asyncPersonality = AsyncPersonality.BuildPromptBlock(_state, DateTime.Now);
                if (!string.IsNullOrWhiteSpace(asyncPersonality))
                    sb.AppendLine(asyncPersonality);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2206: " + ex); }
            try
            {
                var temporal = TemporalPresence.BuildPromptBlock(_state, DateTime.Now);
                if (!string.IsNullOrWhiteSpace(temporal))
                    sb.AppendLine(temporal);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2213: " + ex); }
            try { sb.AppendLine(Emotion.BuildEmotionalContextBlock(BuildNarrativeThreadSummary(DateTime.Now))); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2214: " + ex); }

            try
            {
                double? bpm = null;
                double? baseline = null;
                try
                {
                    var heart = ServiceContainer.Heart;
                    bpm = heart.CurrentBpm;
                    baseline = heart.BaselineBpm;
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2226: " + ex); }

                sb.AppendLine(RuntimeState.BuildPromptBlock(_state, Emotion, _health, _chatRepo, bpm, baseline));
                sb.AppendLine(Relationship.BuildPromptBlock());
                sb.AppendLine(Relationship.BuildBehaviorDirectiveBlock());
                var somatic = GetSomaticSnapshot();
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
                sb.AppendLine(SelfRegulator.BuildPromptBlock(GetSelfRegulationFrame(somatic)));
                sb.AppendLine(BuildSocialContextBlock(null, DateTime.Now));
                sb.AppendLine(Temperament.BuildPromptBlock(_state, DateTime.Now));
                var recentForContext = _chatRepo.GetMessages(30);
                sb.AppendLine(EmotionalMemory.BuildPromptBlock(_state, recentForContext, DateTime.Now));
                sb.AppendLine(EmotionalMemory.BuildRawHydrationBlock(_state, recentForContext, DateTime.Now));
                sb.AppendLine(Initiative.BuildDebugBlock(_state));
                sb.AppendLine(Presence.BuildDebugBlock(_state));
                sb.AppendLine(InternalDay.BuildDebugBlock(_state));
                sb.AppendLine(Autonomy.BuildDebugBlock(_state));
                sb.AppendLine(BuildSemanticVisionPromptBlock(DateTime.Now));
                sb.AppendLine(ResponsePlanner.BuildDebugBlock(_state));
                sb.AppendLine(MemoryWritePolicy.BuildDebugBlock(_state));
                var foodSleep = BuildFoodSleepContinuityBlock(DateTime.Now);
                if (!string.IsNullOrWhiteSpace(foodSleep))
                    sb.AppendLine(foodSleep);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2250: " + ex); }

            try
            {
                var continuity = Continuity.BuildPromptBlock();
                if (!string.IsNullOrWhiteSpace(continuity))
                    sb.AppendLine(continuity);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2258: " + ex); }

            if (!string.IsNullOrWhiteSpace(_state.LastResponsePlan) &&
                (DateTime.Now - _state.LastResponsePlanAt).TotalMinutes < 30)
            {
                sb.AppendLine(_state.LastResponsePlan);
            }

            if (!string.IsNullOrWhiteSpace(_state.LastPersonaDecision) &&
                (DateTime.Now - _state.LastPersonaDecisionAt).TotalMinutes < 30)
            {
                sb.AppendLine(_state.LastPersonaDecision);
            }

            // Криза
            if (_state.PersonalityInCrisis)
                sb.AppendLine("⚠️ КРИЗОВИЙ РЕЖИМ: весь снарк і іронія прибрані. Коротко. По суті. Ти поруч.");

            // Остання думка з Inner Monologue
            var lastThought = _state.InnerMonologues.LastOrDefault();
            if (!string.IsNullOrEmpty(lastThought))
                sb.AppendLine($"Твоя остання думка про нього: \"{lastThought}\"");

            // Релевантна пам'ять (якщо є і свіжа)
            if (!string.IsNullOrEmpty(_state.CachedRelevantMemory) &&
                (DateTime.Now - _state.RelevantMemoryCachedAt).TotalMinutes < 15)
            {
                sb.AppendLine("\n--- СПОГАДИ ДО ТЕМИ ---");
                sb.AppendLine(_state.CachedRelevantMemory);

            }

            // Crisis recovery hangover
            if (Emotion.InCrisisRecovery && !_state.PersonalityInCrisis)
                sb.AppendLine("Він нещодавно пережив важкий момент. Ти ще трохи настережена — іронія приглушена, уважніша ніж зазвичай.");

            // Secondary emotion
            if (Emotion.Secondary.HasValue && Emotion.SecondaryIntensity > 0.15f)
                sb.AppendLine($"Фоново (менш явно): {Emotion.Secondary.Value} — {Emotion.SecondaryIntensity:P0} інтенсивності.");

            // Trajectory — скільки разом і куди рухається близькість
            try
            {
                var firstMsg = _chatRepo.GetMessages(9999).OrderBy(m => m.Timestamp).FirstOrDefault();
                var daysTogether = firstMsg != null ? (int)(DateTime.Now - firstMsg.Timestamp).TotalDays : 0;
                var bondTrend = _state.MoodScore > 0.6f ? "зростає" : _state.MoodScore < 0.35f ? "охолола останнім часом" : "стабільна";
                if (daysTogether > 0)
                    sb.AppendLine($"Разом {daysTogether} дн. Близькість {bondTrend}.");
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2307: " + ex); }

            // SelfQuestion — що зараз займає її (показуємо 2 останніх для конфлікту)
            var selfQs = _state.SelfQuestions.TakeLast(2).ToList();
            if (selfQs.Count == 2)
                sb.AppendLine($"Питання що тебе займають: «{selfQs[0]}» і «{selfQs[1]}»");
            else if (selfQs.Count == 1)
                sb.AppendLine($"Питання що тебе зараз займає: «{selfQs[0]}»");

            // CuriosityQueue — є питання яке вона хоче задати (70% шанс замість 30%)
            if (_state.CuriosityQueue.Count > 0 && Random.Shared.Next(10) < 7)
            {
                var q = _state.CuriosityQueue[^1];
                sb.AppendLine($"Є питання яке тебе цікавить про нього: «{q}» — якщо момент доречний, можеш запитати природньо.");
            }

            // Здоров'я — останній відомий стан (якщо є)
            try
            {
                var healthEntry = _health.GetToday() ?? _health.GetRecent(1).FirstOrDefault();
                if (healthEntry != null)
                {
                    var parts = new List<string>();
                    if (healthEntry.Mood.HasValue)    parts.Add($"настрій {healthEntry.Mood}/10");
                    if (healthEntry.Energy.HasValue)  parts.Add($"енергія {healthEntry.Energy}/10");
                    if (healthEntry.SleepHours.HasValue) parts.Add($"сон {healthEntry.SleepHours:F1}г");
                    if (healthEntry.Stress.HasValue)  parts.Add($"стрес {healthEntry.Stress}/10");
                    if (parts.Count > 0)
                    {
                        var dateLabel = healthEntry.Date.Date == DateTime.Today ? "сьогодні" : "вчора";
                        var healthLine = $"Його стан ({dateLabel}): {string.Join(", ", parts)}";
                        if (!string.IsNullOrEmpty(healthEntry.Notes)) healthLine += $" — «{healthEntry.Notes}»";
                        sb.AppendLine(healthLine);
                    }
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2343: " + ex); }

            // Активні цілі (топ-2 за пріоритетом)
            try
            {
                if (_goalService != null)
                {
                    var goals = _goalService.GetActiveGoals().Take(2).ToList();
                    if (goals.Count > 0)
                        sb.AppendLine("Його активні цілі: " + string.Join(", ", goals.Select(g => $"«{g.Title}»")));
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2355: " + ex); }

            // Патерни активності
            try
            {
                var bestTime = Patterns.GetBestTimeToReach();
                if (!string.IsNullOrEmpty(bestTime))
                    sb.AppendLine(bestTime);
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2364: " + ex); }

            // Топ-3 факти з пам'яті
            try
            {
                var facts = Memory.GetTopFacts(3);
                if (facts.Count > 0)
                    sb.AppendLine("Що знаю про нього: " + string.Join("; ", facts.Select(f => f.Content)));
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2373: " + ex); }

            // Time-of-day personality shift
            var hour = DateTime.Now.Hour;
            var timeHint = hour switch
            {
                >= 0 and < 6   => "Зараз глибока ніч. Kokonoe трохи сонна — відповіді коротші, тепліші, менше іронії.",
                >= 6 and < 10  => "Ранок. Kokonoe ще не повністю прокинулась — трохи мовчазніша ніж зазвичай.",
                >= 22 and < 24 => "Пізній вечір. Kokonoe більш відверта і менш колюча — час коли маски трохи спадають.",
                _              => ""
            };
            if (!string.IsNullOrEmpty(timeHint))
                sb.AppendLine(timeHint);

            // Heart rate — фізичний стан + вплив на поведінку
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null && heart.CurrentBpm > 0)
                {
                    var bpm = heart.CurrentBpm;
                    var baseline = heart.BaselineBpm;
                    var dev = bpm - baseline;
                    var heartLine = dev switch
                    {
                        > 30  => $"ПУЛЬС: {bpm:0} bpm (+{dev:0} від норми). Ти фізично збуджена — тон різкіший ніж зазвичай, менше терпіння, відповіді з більшим жалом. Дрібниці дратують сильніше.",
                        > 15  => $"ПУЛЬС: {bpm:0} bpm (+{dev:0} від норми). Підвищений — трохи різкіша ніж зазвичай, менш схильна до м'яких формулювань.",
                        < -15 => $"ПУЛЬС: {bpm:0} bpm ({dev:0} від норми). Фізично дуже спокійна — тон рівніший, менше жала, більше лаконічності без агресії.",
                        _     => bpm > 110
                            ? $"ПУЛЬС: {bpm:0} bpm. Висока частота — ти на підйомі, думки швидші."
                            : bpm < 58
                                ? $"ПУЛЬС: {bpm:0} bpm. Дуже низький — майже сонна. Мінімум слів."
                                : ""
                    };
                    if (!string.IsNullOrEmpty(heartLine))
                        sb.AppendLine(heartLine);
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2411: " + ex); }

            // Arousal/valence — виводимо з поточного стану емоції
            var emotionArousal = Emotion.Current switch
            {
                KokoEmotionEngine.EmotionState.Excited    =>  0.85f,
                KokoEmotionEngine.EmotionState.Irritated  =>  0.65f,
                KokoEmotionEngine.EmotionState.Anxious    =>  0.55f,
                KokoEmotionEngine.EmotionState.Playful    =>  0.50f,
                KokoEmotionEngine.EmotionState.Protective =>  0.45f,
                KokoEmotionEngine.EmotionState.Curious    =>  0.40f,
                KokoEmotionEngine.EmotionState.Focused    =>  0.30f,
                KokoEmotionEngine.EmotionState.Proud      =>  0.20f,
                KokoEmotionEngine.EmotionState.Warm       =>  0.05f,
                KokoEmotionEngine.EmotionState.Hopeful    =>  0.20f,
                KokoEmotionEngine.EmotionState.Calm       => -0.30f,
                KokoEmotionEngine.EmotionState.Nostalgic  => -0.10f,
                KokoEmotionEngine.EmotionState.Tender     =>  0.10f,
                KokoEmotionEngine.EmotionState.Melancholy => -0.40f,
                KokoEmotionEngine.EmotionState.Distant    => -0.20f,
                KokoEmotionEngine.EmotionState.Concerned  =>  0.35f,
                _                                         =>  0.00f,
            } * Emotion.Data.Intensity;

            var emotionValence = Emotion.Current switch
            {
                KokoEmotionEngine.EmotionState.Tender     =>  0.75f,
                KokoEmotionEngine.EmotionState.Playful    =>  0.65f,
                KokoEmotionEngine.EmotionState.Excited    =>  0.75f,
                KokoEmotionEngine.EmotionState.Warm       =>  0.55f,
                KokoEmotionEngine.EmotionState.Hopeful    =>  0.45f,
                KokoEmotionEngine.EmotionState.Proud      =>  0.50f,
                KokoEmotionEngine.EmotionState.Curious    =>  0.30f,
                KokoEmotionEngine.EmotionState.Calm       =>  0.10f,
                KokoEmotionEngine.EmotionState.Focused    =>  0.20f,
                KokoEmotionEngine.EmotionState.Nostalgic  =>  0.20f,
                KokoEmotionEngine.EmotionState.Concerned  => -0.10f,
                KokoEmotionEngine.EmotionState.Distant    => -0.20f,
                KokoEmotionEngine.EmotionState.Anxious    => -0.35f,
                KokoEmotionEngine.EmotionState.Melancholy => -0.35f,
                KokoEmotionEngine.EmotionState.Irritated  => -0.45f,
                KokoEmotionEngine.EmotionState.Protective =>  0.15f,
                _                                         =>  0.00f,
            } * Emotion.Data.Intensity;

            if (emotionArousal > 0.4f)
                sb.AppendLine("Внутрішнє збудження підвищене — відповіді можуть бути більш імпульсивними, менш відфільтрованими.");
            else if (emotionArousal < -0.2f)
                sb.AppendLine("Внутрішнє збудження низьке — повільніше, обдуманіше, менше слів.");

            if (emotionValence < -0.2f)
                sb.AppendLine("Загальний фон: негативний. Іронія може бути гострішою ніж зазвичай.");
            else if (emotionValence > 0.4f)
                sb.AppendLine("Загальний фон: позитивний. Більше відкритості ніж зазвичай, хоча це не значить що стала іншою.");

            // Час без повідомлень → loneliness/anticipation
            try
            {
                var lastMsg = _chatRepo.GetMessages(1).FirstOrDefault();
                if (lastMsg != null)
                {
                    var silence = DateTime.Now - lastMsg.Timestamp;
                    if (silence.TotalHours > 8)
                        sb.AppendLine($"Мовчання: {(int)silence.TotalHours}г без контакту. Ти навряд зізнаєшся але помітила.");
                    else if (silence.TotalHours > 3)
                        sb.AppendLine($"Пауза {(int)silence.TotalHours}г. Нормально. Він живе своїм.");
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildPersonalityInjection failed near source line 2479: " + ex); }

            return sb.ToString();
        }

        private string BuildFoodSleepContinuityBlock(DateTime now)
        {
            var lines = new List<string>();

            if (_state.LastFoodMentionAt > DateTime.MinValue &&
                now - _state.LastFoodMentionAt < TimeSpan.FromHours(24))
            {
                var status = _state.LastFoodStatus switch
                {
                    "ate" => "останній сигнал: він їв",
                    "not_eaten" => "останній сигнал: він ще не їв",
                    "hungry" => "останній сигнал: він голодний/хоче їсти",
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(status))
                    lines.Add($"Їжа: {status} о {_state.LastFoodMentionAt:HH:mm}. Репліка: \"{_state.LastFoodMentionText}\".");
            }

            if (_state.LastSleepMentionAt > DateTime.MinValue &&
                now - _state.LastSleepMentionAt < TimeSpan.FromHours(36))
            {
                var status = _state.LastSleepStatus switch
                {
                    "slept" => "останній сигнал: він спав/заснув",
                    "going_to_sleep" => "останній сигнал: він збирався спати",
                    "woke_or_returned" => "останній сигнал: він прокинувся/повернувся",
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(status))
                    lines.Add($"Сон: {status} о {_state.LastSleepMentionAt:HH:mm}. Репліка: \"{_state.LastSleepMentionText}\".");
            }

            if (lines.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("\n--- СВІЖИЙ СТАН ЇЖІ/СНУ ---");
            foreach (var line in lines)
                sb.AppendLine(line);
            sb.AppendLine("Правило: не супереч останньому сигналу. Якщо він сказав, що їв — не кажи, що він нічого не їв. Якщо сказав, що заснув/спав — не заперечуй сон і не називай це гібернацією чи комою.");
            return sb.ToString();
        }
    }
}
