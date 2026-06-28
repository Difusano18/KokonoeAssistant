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

        public Task<string?> CallLlmPublicAsync(string prompt) => CallLlmRawAsync(prompt);

        private async Task<string?> CallLlmRawAsync(string prompt)
        {
            try
            {
                var s = AppSettings.Load();
                var body = new
                {
                    model       = s.Model,
                    messages    = new[] { new { role = "user", content = prompt } },
                    max_tokens  = 16384,
                    temperature = 0.9,
                    stream      = false
                };

                var json    = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp    = await _http.PostAsync(s.LmUrl, content);

                if (!resp.IsSuccessStatusCode) return null;

                var text = await resp.Content.ReadAsStringAsync();
                var obj  = Newtonsoft.Json.Linq.JObject.Parse(text);
                var msg  = obj["choices"]?[0]?["message"];

                var msgContent = msg?["content"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(msgContent))
                {
                    var cleanMsg = StripRawGarbage(msgContent);
                    if (!string.IsNullOrEmpty(cleanMsg)) return cleanMsg;
                }

                // Fallback: якщо модель витратила всі токени на reasoning — витягуємо
                // останнє ПОВНЕ речення з кирилицею (уникаємо garbage типу "Drafting ideas:")
                var reasoning = msg?["reasoning_content"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    var lines = reasoning.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    // Garbage-prefix patterns — ці рядки ніколи не є готовими відповідями
                    var garbagePrefixes = new[]
                    {
                        "draft", "option", "step ", "thought", "thinking", "чернетк",
                        "варіант", "крок ", "* ", "- ", "1.", "2.", "3.", "4.", "5.",
                        "okay", "alright", "let me", "i need", "i should", "i'll"
                    };

                    // Шукаємо знизу вверх перший рядок що закінчується на . ! ? і має кирилицю
                    var candidate = lines
                        .Reverse()
                        .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"\*+", "").Trim().TrimStart(':', ' '))
                        .Where(l =>
                            l.Length > 10 &&
                            System.Text.RegularExpressions.Regex.IsMatch(l, @"[А-Яа-яЄєІіЇїҐґ]") &&
                            (l.EndsWith('.') || l.EndsWith('!') || l.EndsWith('?') || l.EndsWith("\u2026")) &&
                            !garbagePrefixes.Any(p => l.ToLower().StartsWith(p)))
                        .FirstOrDefault();

                    if (candidate != null && candidate.Length > 10)
                        return StripRawGarbage(candidate);
                }
                return null;
            }
            catch { return null; }
        }

        // Прибирає явні артефакти моделі з сирого тексту відповіді
        private static string StripRawGarbage(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Прибрати "Drafting ideas: ...", "Thought: ...", "Thinking: ..." на початку
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?i)^\s*(Drafting\s+ideas?|Чернетки?|Drafts?|Thoughts?|Thinking)\s*:?\s*", "",
                System.Text.RegularExpressions.RegexOptions.Multiline).Trim();
            // Прибрати залишкові маркдаун-маркери
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*{2,}", "").Trim();
            return text;
        }

        // ---- JSON EXTRACTION ----

        private static string? ExtractJson(string text)
        {
            // Розумний пошук JSON - шукаємо збалансовані дужки
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    int depth = 1;
                    int j = i + 1;
                    bool inString = false;
                    bool escape = false;

                    while (j < text.Length && depth > 0)
                    {
                        char c = text[j];

                        if (inString)
                        {
                            if (escape)
                            {
                                escape = false;
                            }
                            else if (c == '\\')
                            {
                                escape = true;
                            }
                            else if (c == '"')
                            {
                                inString = false;
                            }
                        }
                        else
                        {
                            if (c == '"')
                            {
                                inString = true;
                            }
                            else if (c == '{')
                            {
                                depth++;
                            }
                            else if (c == '}')
                            {
                                depth--;
                            }
                        }

                        j++;
                    }

                    if (depth == 0)
                    {
                        var candidate = text[i..j];
                        // Валідація через JObject.Parse
                        try
                        {
                            JObject.Parse(candidate);
                            return candidate;
                        }
                        catch { /* не валідний JSON, шукаємо далі */ }
                    }
                }
            }

            return null;
        }

        /// <summary>Примусово запустити думку і можливий відправ (наприклад при старті).</summary>
        public void TriggerThink() => _ = SafeThinkAsync();

        public string GetCurrentWorkModeLabel()
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(_state.LastWorkMode))
                    return _state.LastWorkMode;
                return KokoResourceGuardianService.NormalizeWorkMode(
                    string.IsNullOrWhiteSpace(_state.LastScreenAwarenessMode)
                        ? _state.LastScreenAwarenessWindow
                        : _state.LastScreenAwarenessMode);
            }
        }

        public KokoSelfReviewFrame BuildSelfReviewFrame(string? userText = null)
        {
            RefreshTemporalState(reason: "self-review");
            var now = DateTime.Now;
            var autonomyLevel = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var messages = _chatRepo.GetMessages(40);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            var internalDay = BuildInternalDayFrame(now, autonomyLevel, presence, writeVault: false, record: false);
            var rhythm = Patterns.BuildRhythmProfile(now);
            return SelfReview.Evaluate(userText, _state, messages, presence, internalDay, rhythm, now);
        }

        public KokoConversationTimelineFrame BuildTimelineFrame(string? userText = null)
        {
            RefreshTemporalState(reason: "timeline");
            var now = DateTime.Now;
            var frame = Timeline.Build(_chatRepo.GetMessages(60), _state, now, userText);
            lock (_lock)
            {
                _state.LastTimelineSummary = frame.SummaryUk;
                _state.LastTimelineState = frame.CurrentState;
            }
            return frame;
        }

        public string BuildTimelineContext(string? userText = null)
        {
            try { return BuildTimelineFrame(userText).PromptBlock; }
            catch (Exception ex)
            {
                Log($"Timeline failed: {ex.Message}");
                return "CONVERSATION TIMELINE\nTimeline failed; use current time and latest user message.\n";
            }
        }

        public KokoPostReplyGuardResult EvaluatePostReplyGuard(string userText, string reply)
        {
            var now = DateTime.Now;
            if (string.IsNullOrWhiteSpace(_state.LastPersonaDecision) ||
                (now - _state.LastPersonaDecisionAt).TotalMinutes > 30)
            {
                lock (_lock)
                {
                    RecordPersonaDecision(userText, now);
                }
            }
            if (string.IsNullOrWhiteSpace(_state.LastResponsePlan) ||
                (now - _state.LastResponsePlanAt).TotalMinutes > 30)
            {
                lock (_lock)
                {
                    RecordResponsePlan(userText, now);
                }
            }
            var timeline = BuildTimelineFrame(userText);
            var result = PostReplyGuard.Evaluate(userText, reply, _state, _chatRepo.GetMessages(60), timeline, now);
            lock (_lock)
            {
                _state.LastPostReplyGuardAt = now;
                _state.LastPostReplyGuard = result.Passed
                    ? $"ok: {result.Summary}"
                    : $"{result.RiskLevel}: {string.Join("; ", result.Violations)}";
                SaveState();
            }
            return result;
        }

        public string BuildSelfReviewContext(string? userText = null)
        {
            try { return BuildSelfReviewFrame(userText).PromptBlock; }
            catch (Exception ex)
            {
                Log($"SelfReview failed: {ex.Message}");
                return "SELF-REVIEW BEFORE REPLY\nSelf-review failed; still verify current time, active intent, and immediate context before answering.\n";
            }
        }

        public string BuildCognitiveStabilityContext(string? userText = null)
        {
            var sb = new StringBuilder();
            var stagnation = KokoConversationStagnationGuard.BuildPromptBlock(_state);
            if (!string.IsNullOrWhiteSpace(stagnation))
                sb.AppendLine(stagnation);
            sb.AppendLine(KokoPersonaGuardDirective.Compact);
            sb.AppendLine(KokoNaturalSynthesisPolicy.PromptRules);
            sb.AppendLine(KokoResponseStyleEngine.BuildEmotionLengthDirective(Emotion.Current));
            sb.AppendLine(KokoResponseStyleEngine.BuildTemperamentDirective(_state));
            sb.AppendLine(KokoResponseStyleEngine.BuildLivingConversationDirective(_state));
            sb.AppendLine(KokoSubconsciousMonologueEngine.BuildDirective(_state));
            sb.AppendLine(KokoAsyncPersonalityEngine.BuildDirective(_state));
            sb.AppendLine(KokoTemporalPresenceAwarenessEngine.BuildDirective(_state));
            sb.AppendLine(KokoCollectiveMindService.BuildDirective(_state));
            return sb.ToString().Trim();
        }

        public string BuildCollectiveMindDirective(string? userText = null)
            => KokoCollectiveMindService.BuildDirective(_state);

        public string BuildCollectiveMindContext(string? userText = null, string channel = "runtime", bool publish = false)
        {
            try
            {
                var now = DateTime.Now;
                IReadOnlyList<ChatRepository.ChatMessage> recentMessages;
                try { recentMessages = _chatRepo.GetMessages(12).OrderBy(m => m.Timestamp).ToList(); }
                catch { recentMessages = Array.Empty<ChatRepository.ChatMessage>(); }

                var frame = RecordCollectiveMind(userText ?? "", channel, now, publish, recentMessages);
                var sb = new StringBuilder();
                sb.AppendLine(frame.PromptBlock);
                var recentBlackboard = _blackboard.BuildPromptBlock(6);
                if (!string.IsNullOrWhiteSpace(recentBlackboard))
                {
                    sb.AppendLine();
                    sb.AppendLine(recentBlackboard);
                }
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                Log($"BuildCollectiveMindContext failed: {ex.Message}");
                return "COLLECTIVE MIND BLACKBOARD (private): unavailable; answer current request from verified context and avoid scripted filler.";
            }
        }

        private KokoCollectiveMindFrame RecordCollectiveMind(
            string userText,
            string channel,
            DateTime now,
            bool publish,
            IReadOnlyList<ChatRepository.ChatMessage>? recentMessages = null)
        {
            recentMessages ??= Array.Empty<ChatRepository.ChatMessage>();
            var recentEvents = _blackboard.Recent(12);
            var frame = CollectiveMind.Build(userText, _state, recentMessages, recentEvents, channel, now);
            var shouldPublish = publish && ShouldPublishCollectiveFrame(now, frame.Decision);
            _state.LastCollectiveMindAt = now;
            _state.LastCollectiveMindDecision = frame.Decision;
            _state.LastCollectiveMindTrace = frame.TraceLine;
            if (shouldPublish)
                KokoCollectiveMindService.PublishFrame(_blackboard, frame);
            return frame;
        }

        private bool ShouldPublishCollectiveFrame(DateTime now, string decision)
        {
            if (_state.LastCollectiveMindAt <= DateTime.MinValue)
                return true;
            if (now - _state.LastCollectiveMindAt > TimeSpan.FromSeconds(45))
                return true;
            return !string.Equals(_state.LastCollectiveMindDecision, decision, StringComparison.Ordinal);
        }

        public string BuildResponsePlanContext(string? userText = null)
        {
            var now = DateTime.Now;
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(userText) &&
                    (string.IsNullOrWhiteSpace(_state.LastResponsePlan) ||
                     now - _state.LastResponsePlanAt > TimeSpan.FromMinutes(5)))
                {
                    RecordResponsePlan(userText, now);
                }

                return string.IsNullOrWhiteSpace(_state.LastResponsePlan)
                    ? ResponsePlanner.BuildDebugBlock(_state)
                    : _state.LastResponsePlan;
            }
        }

        public KokoStateFreshnessResult RefreshTemporalState(DateTime? nowOverride = null, string reason = "runtime")
        {
            var now = nowOverride ?? DateTime.Now;
            IReadOnlyList<ChatRepository.ChatMessage> messages;
            try { messages = _chatRepo.GetMessages(80); }
            catch { messages = Array.Empty<ChatRepository.ChatMessage>(); }

            lock (_lock)
            {
                var result = StateFreshness.Refresh(_state, messages, now, BuildReconciliationSignals(reason));
                var shouldPersistStamp =
                    result.Changed ||
                    _state.LastStateRefreshAt <= DateTime.MinValue ||
                    now - _state.LastStateRefreshAt >= TimeSpan.FromMinutes(10);

                _state.LastStateRefreshAt = now;
                _state.LastStateRefreshSummary = $"{reason}: {result.SummaryUk}";
                _state.LastStateRefreshChanged = result.Changed;

                if (shouldPersistStamp)
                    SaveState();

                return result;
            }
        }

        private KokoStateReconciliationSignals BuildReconciliationSignals(string channel)
        {
            return new KokoStateReconciliationSignals
            {
                Channel = channel,
                ScreenMode = _state.LastScreenAwarenessMode,
                ScreenSummary = _state.LastScreenAwarenessSummary,
                LastDesktopActivityAt = _state.LastScreenAwarenessAt
            };
        }

        public string BuildUnifiedExternalContext(string channel = "external", string? userText = null)
        {
            // MainWindow.Chat.cs refreshes this separately (fire-and-forget, before its own
            // context build) for the desktop path. Web chat's context builder is exactly this
            // method and had no equivalent call, so LlmService.PersonalityHint stayed at its
            // "" default for any session that only ever used the web shell — character core
            // (KokoCharacterCore) never reached the prompt, answers came out generic-assistant.
            RefreshPersonalityHint();
            var now = DateTime.Now;
            RefreshTemporalState(now, channel);
            var autonomyLevel = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            KokoResponsePlanFrame? responsePlan = null;
            if (!string.IsNullOrWhiteSpace(userText))
                responsePlan = BuildGovernedResponsePlan(userText, now);
            var active = _state.ShortTermIntents
                .Where(i => !i.ResolvedAt.HasValue)
                .OrderBy(i => i.FollowUpAt)
                .Take(3)
                .Select(i => $"{i.Kind}: {i.Summary} до {i.ExpectedUntil:HH:mm}")
                .ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("=== SHARED KOKONOE CONTEXT ===");
            sb.AppendLine($"channel: {channel}");
            sb.AppendLine($"state_refresh: {NullDash(_state.LastStateRefreshSummary)}");
            sb.AppendLine($"presence: {presence.SummaryUk}");
            sb.AppendLine($"screen_mode: {NullDash(_state.LastScreenAwarenessMode)}");
            sb.AppendLine($"screen: {NullDash(_state.LastScreenAwarenessSummary)}");
            if (!string.IsNullOrWhiteSpace(_state.LastScreenAwarenessSummary))
            {
                var screenDetail = _state.LastScreenAwarenessSummary.Trim();
                if (screenDetail.Length > 80) screenDetail = screenDetail[..80] + "...";
                KokoActivityBus.Emit(new KokoActivity
                {
                    Kind = "source",
                    Label = "Дивлюся на твій екран",
                    Detail = screenDetail,
                    Status = "done"
                });
            }
            sb.AppendLine($"last_activity: {NullDash(_state.LastKnownUserActivity)}");
            sb.AppendLine($"active_intents: {(active.Length == 0 ? "none" : string.Join("; ", active))}");
            try { sb.AppendLine(Emotion.BuildEmotionalContextBlock(BuildNarrativeThreadSummary(now))); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildUnifiedExternalContext failed near source line 6778: " + ex); }
            sb.AppendLine("Use this as private continuity only. Do not quote labels.");
            var collective = BuildCollectiveMindContext(userText, channel, publish: false);
            if (!string.IsNullOrWhiteSpace(collective))
            {
                sb.AppendLine();
                sb.AppendLine(collective);
            }
            if (responsePlan != null)
            {
                sb.AppendLine();
                sb.AppendLine(responsePlan.PromptBlock);
            }
            var continuationBlock = BuildNarrativeContinuationBlock(userText);
            if (!string.IsNullOrWhiteSpace(continuationBlock))
            {
                sb.AppendLine();
                sb.AppendLine(continuationBlock);
            }
            if (responsePlan?.RequiresVaultRead == true || responsePlan?.Capability == "vault_memory")
            {
                KokoActivityBus.Emit(new KokoActivity { Kind = "source", Label = "Читаю vault", Detail = "Obsidian", Status = "running" });
                var preflight = new ObsidianPreflightContextService(_obsidian).Build(userText, now, 3200);
                KokoActivityBus.Emit(new KokoActivity { Kind = "source", Label = "Читаю vault", Detail = "Obsidian", Status = "done" });
                if (!string.IsNullOrWhiteSpace(preflight))
                {
                    sb.AppendLine();
                    sb.AppendLine(preflight);
                }
            }
            return sb.ToString();
        }

        public KokoTelemetrySnapshot BuildTelemetrySnapshot(string? userText = null)
        {
            RefreshTemporalState(reason: "telemetry");
            EnsureVaultSyncFreshness("telemetry-freshness");
            var now = DateTime.Now;
            var autonomyLevel = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var presence = BuildPresenceFrame(now, autonomyLevel);
            var internalDay = BuildInternalDayFrame(now, autonomyLevel, presence, writeVault: false, record: false);
            var rhythm = Patterns.BuildRhythmProfile(now);
            var somatic = GetSomaticSnapshot();
            var selfReg = GetSelfRegulationFrame(somatic);
            var review = SelfReview.Evaluate(userText, _state, _chatRepo.GetMessages(40), presence, internalDay, rhythm, now);
            var relationship = Relationship.State;
            var attachment = Emotion.Attachment;
            var llmDiag = _llm.GetDiagnosticsSnapshot();
            var scenarioResults = Scenarios.RunCoreChecks(now, autonomyLevel);
            var scenarioPassed = scenarioResults.Count(r => r.Passed);
            var timeline = Timeline.Build(_chatRepo.GetMessages(60), _state, now, userText);
            var wearable = ServiceContainer.WearableTelemetry.State;
            string wearableStatus;
            try
            {
                var bridge = ServiceContainer.WearableBridge;
                var connection = bridge.GetConnectionSnapshot(wearable, now.ToUniversalTime());
                var diagnostics = bridge.Diagnostics;
                wearableStatus = KokoWearableTrust.IsVerified(connection, diagnostics, wearable)
                    ? $"{wearable.SleepState} / {wearable.CurrentBpm:F0} bpm / {wearable.PresenceState}"
                    : $"{connection.State.ToLowerInvariant()} / {KokoWearableTrust.BlockReason(connection, diagnostics, wearable)}";
            }
            catch (Exception ex)
            {
                wearableStatus = "unavailable / " + ex.Message;
            }

            return new KokoTelemetrySnapshot
            {
                CreatedAt = now,
                Emotion = Emotion.Current.ToString(),
                Bond = Emotion.Bond.ToString(),
                MoodScore = _state.MoodScore,
                Mood = _state.PersonalityDailyMood,
                Somatic = $"{somatic.State} / strain {somatic.Strain:F2} / calm {somatic.Calm:F2}",
                Wearable = wearableStatus,
                SelfRegulation = $"{selfReg.Reaction} -> {selfReg.Regulation} / control {selfReg.Control:F2}",
                Presence = presence.SummaryUk,
                InternalDay = internalDay.SummaryUk,
                Autonomy = string.IsNullOrWhiteSpace(_state.LastAutonomyDecision) ? "none" : _state.LastAutonomyDecision,
                AutonomyDebug = _state.LastAutonomyShouldAct
                    ? $"пише: {_state.LastAutonomyTrigger} / {_state.LastAutonomySource} / p{_state.LastAutonomyPriority} / {_state.LastAutonomyReason}"
                    : $"мовчить: {_state.LastAutonomyTrigger} / {_state.LastAutonomySilenceReason}",
                Rhythm = rhythm.Summary,
                Timeline = timeline.SummaryUk,
                TimelineState = timeline.CurrentState,
                StateFreshness = string.IsNullOrWhiteSpace(_state.LastStateRefreshSummary) ? "none" : _state.LastStateRefreshSummary,
                Relationship = $"bond {relationship.BondScore:F2}, aftertaste {relationship.LastAftertaste}, protect {relationship.Protectiveness:F2}",
                Attachment = $"trust {attachment.Trust:F2}, intimacy {attachment.Intimacy:F2}, reliability {attachment.Reliability:F2}, reciprocity {attachment.Reciprocity:F2}, vitality {attachment.Vitality:F2}",
                SelfReview = $"{review.RiskLevel}: {review.Summary}",
                PostReplyGuard = string.IsNullOrWhiteSpace(_state.LastPostReplyGuard) ? "none" : _state.LastPostReplyGuard,
                PersonaDecision = string.IsNullOrWhiteSpace(_state.LastPersonaDecision) ? "none" : _state.PersonaDecisionLog.LastOrDefault() ?? "none",
                ResponsePlan = string.IsNullOrWhiteSpace(_state.LastResponsePlanTrace) ? "none" : _state.LastResponsePlanTrace,
                MemoryPolicy = string.IsNullOrWhiteSpace(_state.LastMemoryPolicyDecision) ? "none" : _state.LastMemoryPolicyDecision,
                Continuity = string.IsNullOrWhiteSpace(_state.LastContinuitySummary) ? Continuity.BuildDebugLine() : _state.LastContinuitySummary,
                LlmStatus = $"{llmDiag.Status} / {llmDiag.Channel} / {llmDiag.LastLatencyMs}ms",
                LlmProvider = llmDiag.Provider,
                LlmModel = llmDiag.Model,
                LlmLastError = llmDiag.LastError,
                LlmLastFallback = llmDiag.LastFallback,
                LlmLastRequestAt = llmDiag.LastRequestAt,
                LlmLastSuccessAt = llmDiag.LastSuccessAt,
                LlmLastErrorAt = llmDiag.LastErrorAt,
                LlmLastLatencyMs = llmDiag.LastLatencyMs,
                LlmConsecutiveFailures = llmDiag.ConsecutiveFailures,
                ScenarioHealth = $"{scenarioPassed}/{scenarioResults.Count} базові сценарії пройдено",
                Capabilities = ServiceContainer.Capabilities.BuildStatusLine(),
                PendingVaultExchangeCount = _state.PendingVaultExchangeCount,
                LastVaultSyncAt = _state.LastAutoVaultSyncAt,
                ActiveIntentCount = _state.ShortTermIntents.Count(i => !i.ResolvedAt.HasValue),
                ActiveIntents = _state.ShortTermIntents
                    .Where(i => !i.ResolvedAt.HasValue)
                    .OrderBy(i => i.FollowUpAt)
                    .Take(6)
                    .Select(i => $"{i.Kind}: {i.Summary} до {i.ExpectedUntil:dd.MM HH:mm}")
                    .ToArray(),
                AutonomyLog = _state.AutonomyDecisionLog.TakeLast(8).ToArray(),
                PersonaLog = _state.PersonaDecisionLog.TakeLast(8).ToArray(),
                ResponsePlanLog = _state.ResponsePlanLog.TakeLast(8).ToArray(),
                MemoryPolicyLog = _state.MemoryPolicyLog.TakeLast(8).ToArray(),
                PresenceTrace = _state.PresenceTrace.TakeLast(6).ToArray(),
                InternalDayTrace = _state.InternalDayTrace.TakeLast(6).ToArray(),
                RelationshipEvents = relationship.RecentEvents
                    .TakeLast(6)
                    .Select(e => $"{e.When:dd.MM HH:mm} {e.Kind}: {e.Aftertaste}")
                    .ToArray(),
                ScenarioFindings = scenarioResults
                    .Select(r => $"{(r.Passed ? "ok" : "fail")} {r.Name}: {r.Summary}")
                    .ToArray()
            };
        }

        /// <summary>Оновити PersonalityHint і DynamicTemperature в LlmService перед відповіддю</summary>
        public void RefreshPersonalityHint()
        {
            try
            {
                _llm.PersonalityHint    = BuildPersonalityInjection();
                _llm.DynamicTemperature = ComputeTemperature();
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "RefreshPersonalityHint failed near source line 6964: " + ex); }
        }

        private double ComputeTemperature()
        {
            var state     = Emotion.Current;
            var intensity = Emotion.Data.Intensity; // 0..1

            // Base temperature per arousal level of emotion
            double baseTemp = state switch
            {
                KokoEmotionEngine.EmotionState.Calm       => 0.68,
                KokoEmotionEngine.EmotionState.Focused    => 0.70,
                KokoEmotionEngine.EmotionState.Distant    => 0.72,
                KokoEmotionEngine.EmotionState.Melancholy => 0.72,
                KokoEmotionEngine.EmotionState.Nostalgic  => 0.74,
                KokoEmotionEngine.EmotionState.Tender     => 0.76,
                KokoEmotionEngine.EmotionState.Warm       => 0.80,
                KokoEmotionEngine.EmotionState.Concerned  => 0.80,
                KokoEmotionEngine.EmotionState.Hopeful    => 0.83,
                KokoEmotionEngine.EmotionState.Protective => 0.84,
                KokoEmotionEngine.EmotionState.Curious    => 0.86,
                KokoEmotionEngine.EmotionState.Proud      => 0.88,
                KokoEmotionEngine.EmotionState.Playful    => 0.91,
                KokoEmotionEngine.EmotionState.Anxious    => 0.93,
                KokoEmotionEngine.EmotionState.Irritated  => 0.95,
                KokoEmotionEngine.EmotionState.Excited    => 0.97,
                _                                         => 0.85,
            };

            // Intensity modulates within a ±0.12 window
            double temp = baseTemp + (intensity - 0.5f) * 0.24;

            // Heart rate deviation from baseline bumps temperature
            try
            {
                var heart = ServiceContainer.Heart;
                if (heart != null)
                {
                    var deviation = heart.CurrentBpm - heart.BaselineBpm;
                    if (deviation > 10) temp += Math.Min(0.08, deviation / 200.0);
                    else if (deviation < -10) temp -= Math.Min(0.06, Math.Abs(deviation) / 250.0);
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ComputeTemperature failed near source line 7008: " + ex); }

            // Daily mood nudge
            if (_state.PersonalityDailyMood == "tired") temp -= 0.08;
            if (_state.PersonalityDailyMood == "playful") temp += 0.05;

            return Math.Clamp(temp, 0.60, 1.05);
        }

        /// <summary>Евристичний витяг фактів (без LLM, миттєво).</summary>
        public Task ExtractFactsFromMessageAsync(string userMsg)
        {
            ExtractAndRememberFacts(userMsg);
            return Task.CompletedTask;
        }

        /// <summary>LLM-витяг фактів — викликати після відповіді. Чекає 10с і використовує семафор.</summary>
        public async Task ExtractFactsWithLlmAsync(string userMsg)
        {
            if (userMsg.Length < 10) return;

            var hash = userMsg.GetHashCode().ToString();
            if (_state.SentReminderHashes.Contains("fact_" + hash)) return;

            // Чекаємо 10 секунд — щоб основний LLM точно завершив відповідь
            await Task.Delay(10_000);

            // Якщо семафор зайнятий — пропускаємо, не чекаємо в черзі
            if (!await _bgLlmSemaphore.WaitAsync(0)) return;
            try
            {
                var prompt = $@"Повідомлення від людини: «{userMsg}»

Якщо тут є конкретний факт про цю людину (вподобання, звички, страхи, цілі, стосунки, стан) — напиши його одним коротким реченням від третьої особи (наприклад: ""Він не любить каву"", ""Він зараз перевтомлений"").
Якщо фактів нема — відповідай лише: null

Тільки факт або null. Нічого більше.";

                var raw = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "null") return;

                var fact = raw.Trim().Trim('"').Trim('\u00AB', '\u00BB');
                if (fact.Length > 10 && fact.Length < 200)
                {
                    Memory.LearnFactBlocking(fact, "observation", 0.65f);
                    _state.SentReminderHashes.Add("fact_" + hash);
                    if (_state.SentReminderHashes.Count > 100)
                        _state.SentReminderHashes.RemoveAt(0);
                    Log($"Fact learned: {fact}");

                    // Зберегти факт в vault профіль — щоб він пережив рестарт
                    try
                    {
                        var allNotes = _obsidian.ListNotes();
                        var profileNote = allNotes.FirstOrDefault(n =>
                            n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Творець", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Досьє", StringComparison.OrdinalIgnoreCase));

                        if (profileNote != null)
                        {
                            _obsidian.AppendToNote(profileNote,
                                $"\n- [{DateTime.Now:yyyy-MM-dd}] {fact}");
                            Log($"Fact saved to vault: {profileNote}");
                        }
                    }
                    catch (Exception ex2) { Log($"Fact vault save error: {ex2.Message}"); }
                }
            }
            catch (Exception ex) { Log($"ExtractFacts error: {ex.Message}"); }
            finally
            {
                try { _bgLlmSemaphore.Release(); }
                catch (ObjectDisposedException ex) { KokoSystemLog.Write("BRAIN-CATCH", "ExtractFactsWithLlmAsync failed near source line 7082: " + ex); }
            }
        }

        private static void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[Brain] {msg}");
            KokoSystemLog.Write("BRAIN", msg);
        }

        private void LogError(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[Brain ERROR] {msg}");
            KokoSystemLog.Write("BRAIN_ERROR", msg);
            var _h9 = OnNewMessage; _h9?.Invoke("system", $"⚠️ {msg}");
        }

        private static void LogTelegramDeliveryFailure(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[Brain TG] {msg}");
            KokoSystemLog.Write("BRAIN_TG", msg);
        }

        // =================================================================
        // TOOLS WINDOW API - ?????? ?? ??????????? ????? ??? ????????
        // =================================================================

        /// <summary>???????? ??????? ????? ??? Inner Monologue Stream</summary>
        public List<string> GetRecentThoughts(int count = 10)
        {
            lock (_lock)
            {
                return _state.InnerMonologues.TakeLast(count).ToList();
            }
        }

        /// <summary>???????? ??????? ??????? ?? ????</summary>
        public List<string> GetSelfQuestions(int count = 5)
        {
            lock (_lock)
            {
                return _state.SelfQuestions.Take(count).ToList();
            }
        }

        /// <summary>???????? ????? ????????</summary>
        public List<string> GetCuriosityQueue(int count = 4)
        {
            lock (_lock)
            {
                return _state.CuriosityQueue.Take(count).ToList();
            }
        }

        public List<string> GetInitiativeReasonLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.InitiativeReasonLog.TakeLast(count).Reverse().ToList();
            }
        }

        public List<string> GetPersonaDecisionLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.PersonaDecisionLog.TakeLast(count).Reverse().ToList();
            }
        }

        public List<string> GetResponsePlanLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.ResponsePlanLog.TakeLast(count).Reverse().ToList();
            }
        }

        public List<string> GetMemoryPolicyLog(int count = 8)
        {
            lock (_lock)
            {
                return _state.MemoryPolicyLog.TakeLast(count).Reverse().ToList();
            }
        }

        public IReadOnlyList<ShortTermIntent> GetActiveShortTermIntents(int count = 5)
        {
            RefreshTemporalState(reason: "active-intents");
            lock (_lock)
            {
                var now = DateTime.Now;
                return _state.ShortTermIntents
                    .Where(i => !i.ResolvedAt.HasValue && i.ExpectedUntil >= now.AddMinutes(-30))
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(count)
                    .ToList();
            }
        }

        public string GetDebugStateSnapshot()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine(RuntimeState.BuildPromptBlock(_state, Emotion, _health, _chatRepo));
                sb.AppendLine(Relationship.BuildPromptBlock());
                var somatic = GetSomaticSnapshot();
                sb.AppendLine(Somatic.BuildPromptBlock(somatic));
                sb.AppendLine(SelfRegulator.BuildPromptBlock(GetSelfRegulationFrame(somatic)));
                sb.AppendLine(Initiative.BuildDebugBlock(_state));
                return sb.ToString();
            }
        }

        public KokoSomaticSnapshot GetSomaticSnapshot()
        {
            KokoHeartEngine? heart = null;
            try { heart = ServiceContainer.Heart; } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "GetSomaticSnapshot failed near source line 7200: " + ex); }

            var snapshot = Somatic.Evaluate(heart, Emotion, _health, DateTime.Now);
            _state.LastSomaticState = snapshot.State;
            _state.LastSomaticLabel = snapshot.Label;
            _state.LastSomaticStrain = snapshot.Strain;
            _state.LastSomaticCalm = snapshot.Calm;
            _state.LastSomaticAt = DateTime.Now;
            _state.LastSomaticToneDirective = KokoSemanticVisionEngine.BuildSomaticToneDirective(snapshot, _state.LastScreenAwarenessMode);
            return snapshot;
        }

        public KokoSelfRegulationFrame GetSelfRegulationFrame(KokoSomaticSnapshot? snapshot = null)
        {
            snapshot ??= GetSomaticSnapshot();
            var before = _state.SelfRegulation.Reactions.Count;
            var frame = SelfRegulator.Evaluate(
                _state.SelfRegulation,
                snapshot,
                Emotion,
                Relationship,
                _state.LastUserEmotionalTone,
                DateTime.Now);

            if (_state.SelfRegulation.Reactions.Count > before &&
                !string.IsNullOrWhiteSpace(frame.PrivateThought))
            {
                _state.InnerMonologues.Add($"[somatic/{frame.Regulation}] {frame.PrivateThought}");
                if (_state.InnerMonologues.Count > 30)
                    _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 30);
                TryRecordSomaticVaultEvent(snapshot, frame);
            }

            return frame;
        }

        private void TryRecordSomaticVaultEvent(KokoSomaticSnapshot somatic, KokoSelfRegulationFrame frame)
        {
            try
            {
                if (!ShouldPersistSomaticEvent(somatic, frame))
                    return;

                var key = $"{frame.Reaction}:{frame.Regulation}:{somatic.State}:{Math.Round(somatic.Strain, 1)}";
                var now = DateTime.Now;
                if (key == _state.LastSomaticVaultEventKey &&
                    now - _state.LastSomaticVaultEventAt < TimeSpan.FromMinutes(20))
                    return;

                var path = "Kokonoe/Logs/Somatic Events.md";
                var existing = _obsidian.ReadNote(path);
                if (string.IsNullOrWhiteSpace(existing))
                {
                    existing = """
---
type: somatic-events
tags: [kokonoe, somatic, pulse, self-regulation]
---

# Соматичні події

""";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"## {now:yyyy-MM-dd HH:mm:ss} - {SomaticCodeLabel(frame.Reaction)}");
                sb.AppendLine($"- Тіло: {somatic.State} / {somatic.Label}");
                sb.AppendLine($"- Пульс: {somatic.Bpm:F0} bpm, база {somatic.BaselineBpm:F0}, зміна {somatic.BpmDelta:+0;-0;0}");
                sb.AppendLine($"- Навантаження: strain {somatic.Strain:F2}, calm {somatic.Calm:F2}, volatility {somatic.Volatility:F2}");
                sb.AppendLine($"- Саморегуляція: {SomaticCodeLabel(frame.Regulation)}, контроль {frame.Control:F2}, стримування {frame.Containment:F2}, імпульс {frame.Drive:F2}");
                if (!string.IsNullOrWhiteSpace(frame.PrivateThought))
                    sb.AppendLine($"- Внутрішня думка: {frame.PrivateThought}");
                if (!string.IsNullOrWhiteSpace(frame.BehaviorDirective))
                    sb.AppendLine($"- Поведінкова директива: {frame.BehaviorDirective}");

                _obsidian.WriteNote(path, existing.TrimEnd() + "\n\n" + sb.ToString().TrimEnd() + "\n");
                _state.LastSomaticVaultEventKey = key;
                _state.LastSomaticVaultEventAt = now;
                SaveState();
            }
            catch (Exception ex)
            {
                Log($"Somatic vault event: {ex.Message}");
            }
        }

        private static bool ShouldPersistSomaticEvent(KokoSomaticSnapshot somatic, KokoSelfRegulationFrame frame)
        {
            if (frame.Reaction is "pulse_spike" or "protective_override" or "combat_focus" or "pressure_rise" or "low_power" or "recovered_calm")
                return true;
            if (somatic.Strain >= 0.65 || Math.Abs(somatic.BpmDelta) >= 18)
                return true;
            return false;
        }

        private static string SomaticCodeLabel(string code) => code switch
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

        public List<string> GetSelfRegulationLog(int count = 8)
        {
            lock (_lock)
            {
                return SelfRegulator.GetRecentLines(_state.SelfRegulation, count).ToList();
            }
        }

        public KokoStateInspectorSnapshot CaptureInspectorSnapshot()
        {
            lock (_lock)
            {
                var somatic = GetSomaticSnapshot();
                var selfRegulation = GetSelfRegulationFrame(somatic);
                return Inspector.Capture(
                    _state,
                    Emotion,
                    Relationship,
                    Memory,
                    somatic,
                    selfRegulation,
                    GetInitiativeReasonLog(10).ToArray(),
                    GetSelfRegulationLog(10).ToArray());
            }
        }

        public string BuildInspectorMarkdown() => Inspector.ToMarkdown(CaptureInspectorSnapshot());
        public string BuildInspectorJson() => Inspector.ToJson(CaptureInspectorSnapshot());

        public void ExportInspectorToVault()
        {
            try
            {
                var snapshot = CaptureInspectorSnapshot();
                _obsidian.WriteNote("Kokonoe/Inspector.md", Inspector.ToMarkdown(snapshot));
                _obsidian.WriteNote("Kokonoe/Inspector.json", Inspector.ToJson(snapshot));
            }
            catch (Exception ex) { Log($"ExportInspectorToVault: {ex.Message}"); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _thinkTimer.Dispose();
            _spontaneousTimer.Dispose();
            _screenAwarenessTimer.Dispose();
            _resourceGuardianTimer.Dispose();
            _stateCheckpointTimer.Dispose();
            _dailyReviewTimer.Dispose();
            _bgLlmSemaphore.Dispose();
            _staticContextCacheGate.Dispose();
        }
    }
}
