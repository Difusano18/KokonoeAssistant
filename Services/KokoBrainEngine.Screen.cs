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

        private async Task GuardedScreenAwarenessAsync()
        {
            if (Interlocked.CompareExchange(ref _screenAwarenessInFlight, 1, 0) != 0)
            {
                Log("ScreenAwareness skipped — previous tick still in flight");
                return;
            }
            try { await SafeScreenAwarenessAsync(); }
            finally { Interlocked.Exchange(ref _screenAwarenessInFlight, 0); }
        }

        public async Task ForceScreenAwarenessAsync(string reason, string toneDirective = "")
        {
            if (Interlocked.CompareExchange(ref _screenAwarenessInFlight, 1, 0) != 0)
            {
                Log($"Forced ScreenAwareness skipped: previous tick still in flight; reason={reason}");
                return;
            }

            try { await SafeScreenAwarenessAsync(reason, toneDirective); }
            finally { Interlocked.Exchange(ref _screenAwarenessInFlight, 0); }
        }

        // ---- SCREEN CONTEXT ----

        /// <summary>Оновити контекст екрану (раз на 5хв)</summary>
        private async Task RefreshScreenContextAsync()
        {
            if (_contextAnalyzer == null) return;
            if ((DateTime.Now - _lastScreenRefreshAt).TotalMinutes < 5) return;
            try
            {
                _lastScreenRefreshAt = DateTime.Now;
                var frame = _contextAnalyzer.AnalyzeCurrentFrame();
                var obs   = frame.SummaryForLLM ?? "";
                if (!string.IsNullOrEmpty(obs))
                {
                    _cachedScreenContext   = obs;
                    _screenContextCachedAt = DateTime.Now;
                    _llm.ScreenCtx = obs;

                    // Передати в StateEngine
                    var screenState    = frame.ScreenActivity?.IsActive == true ? "ACTIVE" : "IDLE";
                    var dominantState  = frame.DominantState ?? "";
                    var activityPat    = frame.ActivityPattern ?? "";
                    var faceDetected   = frame.WebcamAnalysis?.FaceDetected ?? false;
                    var expression     = frame.WebcamAnalysis?.ExpressionLevel ?? "";
                    var brightness     = frame.WebcamAnalysis?.Brightness ?? 0.0;
                    _stateEngine?.UpdateVisualMonitoringState(
                        screenState, "", 0.0,
                        faceDetected, expression, brightness,
                        activityPat, dominantState);

                    Log($"[Screen] {obs[..Math.Min(80, obs.Length)]}");
                }
            }
            catch (Exception ex) { Log($"RefreshScreenContext: {ex.Message}"); }
        }

        private async Task SafeScreenAwarenessAsync(string forceReason = "", string toneDirective = "")
        {
            var settings = AppSettings.Load();
            if (!settings.ScreenAwarenessEnabled) return;

            var now = DateTime.Now;
            var interval = GetEffectiveScreenAwarenessInterval(settings, now);
            var forceBySignal = !string.IsNullOrWhiteSpace(forceReason);
            var forceByInitiative = !forceBySignal && ShouldForceScreenAwarenessFromAutonomy(now);
            var sinceLastScreen = (now - _state.LastScreenAwarenessAt).TotalMinutes;
            if (!forceBySignal && !forceByInitiative && sinceLastScreen < interval)
            {
                Log($"ScreenAwareness suppressed: interval {sinceLastScreen:0.0}m < {interval}m");
                return;
            }
            if (forceBySignal)
                Log($"ScreenAwareness forced: {forceReason}");
            if (forceByInitiative)
                Log("ScreenAwareness forced: autonomy curiosity/observation trigger");
            if (now < _state.VisionBackoffUntil)
            {
                Log($"ScreenAwareness skipped: vision backoff until {_state.VisionBackoffUntil:HH:mm}");
                return;
            }

            if (ShouldSuppressProactiveForSleep(now))
            {
                Log("ScreenAwareness suppressed: sleep/goodbye context is active");
                return;
            }

            try
            {
                var diag = _llm.GetDiagnosticsSnapshot();
                if (diag.InFlight > 0)
                {
                    Log("ScreenAwareness skipped: foreground LLM is busy");
                    return;
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SafeScreenAwarenessAsync failed near source line 4764: " + ex); }

            if (!await _bgLlmSemaphore.WaitAsync(0))
            {
                Log("ScreenAwareness skipped: background LLM is busy");
                return;
            }

            try
            {
                byte[] screenshot;
                try
                {
                    screenshot = await Task.Run(() => ServiceContainer.PcControl.TakeScreenshot());
                }
                catch (Exception ex)
                {
                    Log($"ScreenAwareness capture failed: {ex.Message}");
                    return;
                }

                if (screenshot.Length == 0) return;

                var activity = _screenActivityAnalyzer.AnalyzeScreenshot(screenshot);
                var foreground = ServiceContainer.PcControl.GetForegroundWindow();
                if (string.IsNullOrWhiteSpace(activity.ActiveWindowTitle) && !string.IsNullOrWhiteSpace(foreground.Title))
                    activity.ActiveWindowTitle = foreground.Title;
                var idleTime = TimeSpan.Zero;
                try { idleTime = ServiceContainer.PcControl.GetSystemInfo().IdleTime; }
                catch (Exception ex) { Log($"ScreenAwareness idle-time read failed: {ex.Message}"); }
                var hash = _screenActivityAnalyzer.GenerateScreenshotHash(screenshot);
                var screenChanged = activity.IsActive ||
                    (!string.IsNullOrWhiteSpace(_state.LastScreenAwarenessHash) &&
                     hash != _state.LastScreenAwarenessHash &&
                     activity.PixelDifferencePercentage >= 1.0);
                var prompt = ScreenAwareness.BuildVisionPrompt(
                    activity,
                    _state.LastScreenAwarenessSummary,
                    _state.LastScreenAwarenessComment,
                    now,
                    foreground,
                    idleTime,
                    BuildVisionMultimodalContext(now, forceReason, toneDirective));

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var raw = await _llm.SendSystemVisionQueryAsync(prompt, screenshot, "image/jpeg", cts.Token);
                if (LooksLikeVisionFailure(raw))
                {
                    try
                    {
                        var enhanced = ImageProcessingService.EnhanceForVision(screenshot);
                        var retryPrompt = VisionResponseQuality.BuildRetryPrompt(prompt, foreground.ToString());
                        using var retryCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                        var retryRaw = await _llm.SendSystemVisionQueryAsync(
                            retryPrompt,
                            enhanced.Length > 0 ? enhanced : screenshot,
                            "image/jpeg",
                            retryCts.Token,
                            maxTokensOverride: 2048);
                        if (!LooksLikeVisionFailure(retryRaw))
                            raw = retryRaw;
                    }
                    catch (Exception ex)
                    {
                        Log($"ScreenAwareness vision repair failed: {ex.Message}");
                    }
                }

                if (LooksLikeVisionFailure(raw))
                {
                    RegisterVisionFailure(raw, now, activity);
                    SaveState();
                    return;
                }
                var analysis = ScreenAwareness.Parse(raw);
                var previousSituation = new KokoScreenSituation
                {
                    CurrentTask = _state.LastScreenSituationTask,
                    Progress = _state.LastScreenSituationProgress,
                    Blocker = _state.LastScreenSituationBlocker,
                    RecommendedBehavior = string.IsNullOrWhiteSpace(_state.LastScreenSituationBehavior)
                        ? "observe"
                        : _state.LastScreenSituationBehavior,
                    Reason = _state.LastScreenSituationReason
                };
                var situation = ScreenAwareness.BuildSituation(analysis, activity, previousSituation);
                var context = ScreenAwareness.BuildCompactContext(analysis, activity, situation);
                var patternCandidate = ScreenAwareness.BuildPatternCandidate(analysis, situation, now);
                var predictiveWarmup = ProactiveContext.BuildScreenWarmup(analysis, activity, _obsidian, now);
                if (predictiveWarmup.HasContext)
                    context += "\n\n" + predictiveWarmup.PromptBlock;
                var semanticFrame = SemanticVision.BuildFrame(analysis, activity, situation, _state, now);
                context += "\n\n" + semanticFrame.PromptBlock;

                lock (_lock)
                {
                    _state.LastScreenAwarenessAt = now;
                    _state.LastScreenAwarenessHash = hash;
                    _state.LastScreenAwarenessSummary = analysis.SummaryUk;
                    _state.LastScreenAwarenessActivity = analysis.ActivityUk;
                    _state.LastScreenAwarenessMode = analysis.ScreenMode;
                    _state.LastScreenAwarenessWindow = activity.ActiveWindowTitle ?? "";
                    _state.LastScreenSituationTask = situation.CurrentTask;
                    _state.LastScreenSituationProgress = situation.Progress;
                    _state.LastScreenSituationBlocker = situation.Blocker;
                    _state.LastScreenSituationBehavior = situation.RecommendedBehavior;
                    _state.LastScreenSituationReason = situation.Reason;
                    SemanticVision.ApplyToState(_state, semanticFrame);
                    if (semanticFrame.ShouldResearch && !string.IsNullOrWhiteSpace(semanticFrame.ResearchTopic))
                    {
                        var curiosity = "[semantic-vision] Research or inspect: " + semanticFrame.ResearchTopic;
                        if (!_state.CuriosityQueue.Any(q => q.Contains(semanticFrame.ResearchTopic, StringComparison.OrdinalIgnoreCase)))
                        {
                            _state.CuriosityQueue.Add(curiosity);
                            if (_state.CuriosityQueue.Count > 20)
                                _state.CuriosityQueue.RemoveRange(0, _state.CuriosityQueue.Count - 20);
                        }
                    }
                    _state.VisionFailureCount = 0;
                    _state.VisionBackoffUntil = DateTime.MinValue;
                    _state.LastKnownUserActivity = string.IsNullOrWhiteSpace(analysis.SummaryUk)
                        ? (activity.ActiveWindowTitle ?? "")
                        : analysis.SummaryUk;
                    if (predictiveWarmup.HasContext)
                    {
                        var isNewWarmup = !string.Equals(_state.LastPredictiveContextMode, predictiveWarmup.Mode, StringComparison.OrdinalIgnoreCase) ||
                                          now - _state.LastPredictiveContextAt > TimeSpan.FromMinutes(30);
                        _state.LastPredictiveContextAt = now;
                        _state.LastPredictiveContextMode = predictiveWarmup.Mode;
                        _state.LastPredictiveContextSummary = predictiveWarmup.Summary;
                        _state.LastPredictiveContextNotes = string.Join("; ", predictiveWarmup.SourcePaths.Take(6));
                        if (string.Equals(predictiveWarmup.AppKey, "browser", StringComparison.OrdinalIgnoreCase) &&
                            activity.TimeSinceLastChange >= TimeSpan.FromMinutes(10))
                        {
                            _state.CachedRelevantMemory = predictiveWarmup.PromptBlock;
                            _state.RelevantMemoryCachedAt = now;
                        }
                        if (isNewWarmup)
                        {
                            _state.PendingThoughts.Add("[screen-warmup] " + TrimStateMention(predictiveWarmup.Summary));
                            if (_state.PendingThoughts.Count > 20)
                                _state.PendingThoughts.RemoveRange(0, _state.PendingThoughts.Count - 20);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(analysis.SummaryUk))
                    {
                        EmotionalMemory.RememberVisualAnchor(
                            _state,
                            $"{analysis.ScreenMode}: {analysis.SummaryUk}; {analysis.ActivityUk}; {situation.CurrentTask}",
                            now);
                        _state.Observations.Add($"screen {now:HH:mm}: {analysis.SummaryUk}");
                        if (!string.IsNullOrWhiteSpace(situation.CurrentTask))
                            _state.Observations.Add($"screen-situation {now:HH:mm}: {situation.CurrentTask}; {situation.Progress}; {situation.RecommendedBehavior}");
                        if (!string.IsNullOrWhiteSpace(semanticFrame.Summary))
                            _state.Observations.Add($"semantic-vision {now:HH:mm}: {semanticFrame.FlowState}; {semanticFrame.PrimaryIntent}; {semanticFrame.Summary}");
                        if (_state.Observations.Count > 40)
                            _state.Observations.RemoveRange(0, _state.Observations.Count - 40);
                    }
                }

                _cachedScreenContext = context;
                _screenContextCachedAt = now;
                _llm.ScreenCtx = context;
                _stateEngine?.UpdateVisualMonitoringState(
                    activity.IsActive ? "ACTIVE" : "IDLE",
                    activity.ActiveWindowTitle ?? "",
                    activity.PixelDifferencePercentage,
                    false, "", 0.0,
                    analysis.ActivityUk,
                    activity.IsActive ? "Active" : "Idle");

                ObserveScreenPattern(patternCandidate, now);

                var commentCooldown = GetEffectiveScreenAwarenessCommentCooldown(settings, analysis, activity);
                var decision = ScreenAwareness.DecideComment(
                    analysis,
                    now,
                    _state.LastScreenAwarenessCommentAt,
                    _state.LastScreenAwarenessComment,
                    commentCooldown,
                    settings.ScreenAwarenessSendComments && now >= _state.ScreenAwarenessObserveOnlyUntil,
                    screenChanged,
                    activity.IsActive,
                    activity.ActiveWindowTitle ?? "",
                    situation);

                if (!decision.ShouldSend)
                {
                    SaveState();
                    Log($"ScreenAwareness observed: {analysis.SummaryUk} ({decision.Reason})");
                    return;
                }

                if (!await SendTgAndLog(decision.Message, decision.CountsAsJab ? "screen_awareness_jab" : "screen_awareness_assist"))
                {
                    SaveState();
                    return;
                }

                _state.LastScreenAwarenessCommentAt = now;
                _state.LastScreenAwarenessComment = decision.Message;
                _state.LastSpontaneousAt = now;
                _state.LastSpontaneousMsgs.Add(decision.Message[..Math.Min(100, decision.Message.Length)]);
                if (_state.LastSpontaneousMsgs.Count > 5)
                    _state.LastSpontaneousMsgs.RemoveAt(0);

                try
                {
                    _chatRepo.InsertMessage(new ChatRepository.ChatMessage
                    {
                        Content = decision.Message,
                        Role = "assistant",
                        Author = "Kokonoe",
                        Timestamp = now
                    });
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SafeScreenAwarenessAsync failed near source line 4980: " + ex); }

                OnNewMessage?.Invoke("assistant", decision.Message);
                SaveState();
                Log($"ScreenAwareness comment sent: {decision.Message[..Math.Min(80, decision.Message.Length)]}");
            }
            catch (OperationCanceledException)
            {
                RegisterVisionFailure("timeout", now, null);
                Log("ScreenAwareness timed out");
            }
            catch (Exception ex)
            {
                RegisterVisionFailure(ex.Message, now, null);
                Log($"ScreenAwareness error: {ex.Message}");
            }
            finally
            {
                _bgLlmSemaphore.Release();
            }
        }

        private int GetEffectiveScreenAwarenessInterval(AppSettings settings, DateTime now)
        {
            var normal = Math.Clamp(settings.ScreenAwarenessIntervalMins, 1, 60);
            if (IsHighActivityScreenLikelyActive(now))
                return Math.Clamp(Math.Min(normal, 2), 2, 60);
            if (IsStaticIdleScreenLikelyActive(now))
                return Math.Clamp(Math.Max(normal, 20), 2, 60);
            if (!IsGameScreenLikelyActive(now))
                return normal;

            return Math.Clamp(Math.Min(normal, settings.GameScreenAwarenessIntervalMins), 3, 60);
        }

        private string BuildVisionMultimodalContext(DateTime now, string forceReason, string toneDirective)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(forceReason))
                lines.Add($"trigger={forceReason}");
            if (!string.IsNullOrWhiteSpace(toneDirective))
                lines.Add($"tone_directive={toneDirective}");

            try
            {
                var samples = ServiceContainer.WearableTelemetry.RecentSamples
                    .Where(s => s.HeartRateBpm.HasValue)
                    .OrderByDescending(s => s.TimestampUtc)
                    .Take(3)
                    .OrderBy(s => s.TimestampUtc)
                    .Select(s => $"{s.TimestampUtc.ToLocalTime():HH:mm:ss} bpm={s.HeartRateBpm:F0} motion={(s.Motion.HasValue ? s.Motion.Value.ToString("F2") : "-")} wrist={(s.OnWrist.HasValue ? s.OnWrist.Value.ToString() : "-")}")
                    .ToList();
                if (samples.Count > 0)
                    lines.Add("heart_samples=" + string.Join(" | ", samples));

                var wearable = ServiceContainer.WearableTelemetry.State;
                if (wearable.IsFresh(DateTime.UtcNow))
                    lines.Add($"wearable_state=stress {wearable.LiveStressScore}/100; recovery={wearable.RecoveryState}; sleep={wearable.SleepState}; delta={wearable.BpmDelta:+0;-0;0}");
            }
            catch (Exception ex)
            {
                lines.Add($"wearable_context_error={ex.Message}");
            }

            try
            {
                var stress = ServiceContainer.Heart.WearableStress;
                if (!string.IsNullOrWhiteSpace(stress.State))
                    lines.Add($"heart_stress={stress.State}; score={stress.Score:F2}; {stress.Reason}");
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildVisionMultimodalContext failed near source line 5050: " + ex); }

            try
            {
                lines.Add($"work_mode={GetCurrentWorkModeLabel()}");
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildVisionMultimodalContext failed near source line 5056: " + ex); }

            return string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        private bool ShouldForceScreenAwarenessFromAutonomy(DateTime now)
        {
            DateTime decisionAt;
            DateTime lastScreenAt;
            string text;
            lock (_lock)
            {
                decisionAt = _state.LastAutonomyDecisionAt;
                lastScreenAt = _state.LastScreenAwarenessAt;
                text = $"{_state.LastAutonomySource} {_state.LastAutonomyTrigger} {_state.LastAutonomyReason}".ToLowerInvariant();
            }

            if (decisionAt <= DateTime.MinValue || now - decisionAt > TimeSpan.FromMinutes(12))
                return false;
            if (lastScreenAt > DateTime.MinValue && now - lastScreenAt < TimeSpan.FromMinutes(2))
                return false;

            return text.Contains("curiosity", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("observation", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("observe", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("screen", StringComparison.OrdinalIgnoreCase);
        }

        private int GetEffectiveScreenAwarenessCommentCooldown(AppSettings settings, KokoScreenAwarenessAnalysis analysis, ActivityAnalyzer.ActivityState activity)
        {
            var normal = Math.Clamp(settings.ScreenAwarenessCommentCooldownMins, 1, 180);
            var mode = KokoScreenAwarenessService.NormalizeMode(analysis.ScreenMode, $"{activity.ActiveWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk}");
            if (mode != "game")
                return normal;

            return Math.Clamp(Math.Min(normal, settings.GameScreenAwarenessCommentCooldownMins), 5, 180);
        }

        private bool IsGameScreenLikelyActive(DateTime now)
        {
            var recentScreen = _state.LastScreenAwarenessAt > DateTime.MinValue &&
                now - _state.LastScreenAwarenessAt < TimeSpan.FromMinutes(45);
            if (recentScreen && string.Equals(_state.LastScreenAwarenessMode, "game", StringComparison.OrdinalIgnoreCase))
                return true;

            var lastWindowMode = KokoScreenAwarenessService.NormalizeMode("", _state.LastScreenAwarenessWindow);
            return recentScreen && lastWindowMode == "game";
        }

        private bool IsHighActivityScreenLikelyActive(DateTime now)
        {
            var recentScreen = _state.LastScreenAwarenessAt > DateTime.MinValue &&
                now - _state.LastScreenAwarenessAt < TimeSpan.FromMinutes(20);
            if (!recentScreen)
                return false;

            var mode = KokoScreenAwarenessService.NormalizeMode(
                _state.LastScreenAwarenessMode,
                $"{_state.LastScreenAwarenessWindow} {_state.LastScreenAwarenessSummary} {_state.LastScreenAwarenessActivity}");
            var progress = (_state.LastScreenSituationProgress ?? "").ToLowerInvariant();
            var activity = (_state.LastScreenAwarenessActivity ?? "").ToLowerInvariant();

            return mode is "game" or "coding" or "obsidian" or "media" &&
                (progress is "moving" or "switching" ||
                 activity.Contains("active", StringComparison.OrdinalIgnoreCase) ||
                 activity.Contains("changed", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsStaticIdleScreenLikelyActive(DateTime now)
        {
            var recentScreen = _state.LastScreenAwarenessAt > DateTime.MinValue &&
                now - _state.LastScreenAwarenessAt < TimeSpan.FromMinutes(45);
            if (!recentScreen)
                return false;

            var mode = KokoScreenAwarenessService.NormalizeMode(
                _state.LastScreenAwarenessMode,
                $"{_state.LastScreenAwarenessWindow} {_state.LastScreenAwarenessSummary} {_state.LastScreenAwarenessActivity}");
            var progress = (_state.LastScreenSituationProgress ?? "").ToLowerInvariant();
            var activity = (_state.LastScreenAwarenessActivity ?? "").ToLowerInvariant();
            return mode is "idle" or "desktop" ||
                progress == "idle" ||
                activity.Contains("same", StringComparison.OrdinalIgnoreCase) ||
                activity.Contains("idle", StringComparison.OrdinalIgnoreCase);
        }

        private void RegisterVisionFailure(string? raw, DateTime now, ActivityAnalyzer.ActivityState? activity)
        {
            _state.LastScreenAwarenessAt = now;
            _state.LastVisionFailureAt = now;
            _state.VisionFailureCount = Math.Min(8, _state.VisionFailureCount + 1);
            var delay = TimeSpan.FromMinutes(Math.Min(60, 5 * Math.Pow(2, Math.Max(0, _state.VisionFailureCount - 1))));
            _state.VisionBackoffUntil = now + delay;
            _state.LastVisionFailureSummary = TrimForLog(raw, 180);
            _state.LastScreenAwarenessMode = KokoScreenAwarenessService.NormalizeMode("", activity?.ActiveWindowTitle ?? "");
            _state.LastScreenSituationTask = "screen context fallback";
            _state.LastScreenSituationProgress = "unknown";
            _state.LastScreenSituationBlocker = "vision unavailable";
            _state.LastScreenSituationBehavior = "observe";
            _state.LastScreenSituationReason = $"vision failure; fallback mode={_state.LastScreenAwarenessMode}";
            _cachedScreenContext = $"[SCREEN AWARENESS]\nMode: {_state.LastScreenAwarenessMode}\nVision unavailable; using window/activity fallback.\nWindow: {activity?.ActiveWindowTitle ?? "-"}";
            _screenContextCachedAt = now;
            _llm.ScreenCtx = _cachedScreenContext;
            TrySelfHealVisionPipeline(now, raw);
        }

        private void TrySelfHealVisionPipeline(DateTime now, string? raw)
        {
            if (_state.VisionFailureCount < 3)
                return;
            if (_state.LastVisionSelfHealAt > DateTime.MinValue &&
                now - _state.LastVisionSelfHealAt < TimeSpan.FromMinutes(10))
                return;

            _state.LastVisionSelfHealAt = now;
            _state.VisionBackoffUntil = now.AddMinutes(2);
            try { _llm.ClearHistory(); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "TrySelfHealVisionPipeline failed near source line 5172: " + ex); }
            Log($"Vision self-heal: failures={_state.VisionFailureCount}; backoff reset to 2m; last={TrimForLog(raw, 160)}");
        }

        private void ObserveScreenPattern(KokoScreenPatternCandidate candidate, DateTime now)
        {
            if (!candidate.ShouldRecord || string.IsNullOrWhiteSpace(candidate.Key) || string.IsNullOrWhiteSpace(candidate.Text))
                return;

            ScreenPatternStats stats;
            lock (_lock)
            {
                if (!_state.ScreenPatterns.TryGetValue(candidate.Key, out stats!))
                {
                    stats = new ScreenPatternStats
                    {
                        Key = candidate.Key,
                        Text = candidate.Text,
                        Mode = candidate.Mode,
                        FirstSeenAt = now
                    };
                    _state.ScreenPatterns[candidate.Key] = stats;
                }

                stats.Text = candidate.Text;
                stats.Mode = candidate.Mode;
                stats.Count++;
                stats.LastSeenAt = now;

                var staleKeys = _state.ScreenPatterns
                    .Where(kv => (now - kv.Value.LastSeenAt).TotalDays > 14)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var key in staleKeys)
                    _state.ScreenPatterns.Remove(key);
            }

            if (stats.Count < 3)
                return;
            if (stats.LastWrittenAt > DateTime.MinValue && (now - stats.LastWrittenAt).TotalHours < 12)
                return;

            var memory = $"{stats.Text}; \u043f\u043e\u043c\u0456\u0447\u0435\u043d\u043e {stats.Count} \u0440\u0430\u0437\u0438 \u0437 {stats.FirstSeenAt:yyyy-MM-dd HH:mm}.";
            try
            {
                var added = _obsidian.AppendUniqueItemsToNote(
                    "Kokonoe/Memory/Screen Patterns.md",
                    "# \u041f\u0430\u0442\u0435\u0440\u043d\u0438 \u0435\u043a\u0440\u0430\u043d\u0430\n\n\u0423\u0437\u0430\u0433\u0430\u043b\u044c\u043d\u0435\u043d\u0456 \u043f\u0430\u0442\u0435\u0440\u043d\u0438 screen-awareness. \u0411\u0435\u0437 \u0441\u0438\u0440\u0438\u0445 \u0441\u043a\u0440\u0456\u043d\u0448\u043e\u0442\u0456\u0432, \u043f\u0440\u0438\u0432\u0430\u0442\u043d\u0438\u0445 \u0456\u0434\u0435\u043d\u0442\u0438\u0444\u0456\u043a\u0430\u0442\u043e\u0440\u0456\u0432, \u0442\u043e\u043a\u0435\u043d\u0456\u0432 \u0430\u0431\u043e \u0442\u043e\u0447\u043d\u043e\u0433\u043e \u043f\u0440\u0438\u0432\u0430\u0442\u043d\u043e\u0433\u043e \u0442\u0435\u043a\u0441\u0442\u0443.\n",
                    new[] { memory },
                    "screen-pattern",
                    duplicateThreshold: 0.86);

                if (added > 0)
                {
                    stats.LastWrittenAt = now;
                    Log($"Screen pattern saved: {stats.Text}");
                }
            }
            catch (Exception ex)
            {
                Log($"Screen pattern save failed: {ex.Message}");
            }
        }

        private static bool LooksLikeVisionFailure(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return true;
            var lower = raw.ToLowerInvariant();
            return lower.Contains("vision-сервер") ||
                   lower.Contains("vision server") ||
                   lower.Contains("500") ||
                   lower.Contains("помилка llm") ||
                   lower.Contains("【помилка") ||
                   lower.Contains("error");
        }

        private static string TrimForLog(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        // ---- WHAT DID I MISS ----

        /// <summary>Викликати при закритті застосунку — зберегти час виходу</summary>
        public void RecordClose()
        {
            _state.LastClosedAt = DateTime.Now;
            SaveState();
        }

        /// <summary>При запуску — якщо пройшло 8+ годин від останнього закриття, Kokonoe питає як справи</summary>
        public async Task WhatDidIMissAsync()
        {
            try
            {
                // Час від якого рахуємо — реальне закриття застосунку (надійніше ніж останнє повідомлення)
                var lastClosed = _state.LastClosedAt;
                if (lastClosed == DateTime.MinValue) return; // перший запуск, нема даних

                var gapHours = (DateTime.Now - lastClosed).TotalHours;
                if (gapHours < 8) return;   // не було довго — не чіпаємо
                if (gapHours > 720) return; // > 30 днів — явно щось не так з годинником

                // Формуємо короткий контекст для LLM
                var gapStr = gapHours >= 24
                    ? $"{(int)(gapHours / 24)} дн. {(int)(gapHours % 24)} год."
                    : $"{(int)gapHours} год.";

                var ctx = new StringBuilder();
                ctx.AppendLine($"[SYSTEM] Користувач повернувся після {gapStr} відсутності.");
                ctx.AppendLine($"Закрив застосунок: {lastClosed:dd.MM HH:mm}, зараз: {DateTime.Now:HH:mm}.");

                // Що змінилось поки не було
                try
                {
                    var modified = _obsidian.GetNotesModifiedToday();
                    if (modified.Any())
                        ctx.AppendLine($"В vault нові нотатки: {string.Join(", ", modified.Take(3))}");
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "WhatDidIMissAsync failed near source line 5359: " + ex); }

                try
                {
                    var missed = Scheduler.GetAll()
                        .Where(e => e.FireAt > lastClosed && e.FireAt < DateTime.Now)
                        .Take(2).ToList();
                    if (missed.Any())
                        ctx.AppendLine($"Пропущені нагадування поки не було: {string.Join(", ", missed.Select(e => e.Prompt.Split('.')[0]))}");
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "WhatDidIMissAsync failed near source line 5369: " + ex); }

                ctx.AppendLine("Напиши одне коротке повідомлення в стилі Kokonoe — запитай що робив, як справи. Без зайвих слів, без списків. Просто живо і по-людськи.");

                // Використовуємо SendSystemQueryAsync щоб не засмічувати основну історію
                var reply = await _llm.SendSystemQueryAsync(ctx.ToString(), ct: CancellationToken.None);
                if (string.IsNullOrWhiteSpace(reply) || reply.StartsWith("[")) return;

                OnNewMessage?.Invoke("assistant", reply);
                _state.LastWhatMissedAt  = DateTime.Now;
                _state.LastSpontaneousAt = DateTime.Now;
                SaveState();
            }
            catch (Exception ex) { Log($"WhatDidIMiss: {ex.Message}"); }
        }

        private string BuildSemanticVisionPromptBlock(DateTime now)
        {
            if (_state.LastSemanticVisionAt <= DateTime.MinValue ||
                now - _state.LastSemanticVisionAt > TimeSpan.FromMinutes(45))
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("=== SEMANTIC VISION STATE ===");
            sb.AppendLine($"flow: {NullDash(_state.LastSemanticVisionFlow)}");
            sb.AppendLine($"intent: {NullDash(_state.LastSemanticVisionIntent)}");
            sb.AppendLine($"summary: {NullDash(_state.LastSemanticVisionSummary)}");
            if (!string.IsNullOrWhiteSpace(_state.LastSemanticVisionOcr))
                sb.AppendLine($"ocr: {_state.LastSemanticVisionOcr}");
            if (!string.IsNullOrWhiteSpace(_state.LastSemanticVisionAssistHint))
                sb.AppendLine($"assist: {_state.LastSemanticVisionAssistHint}");
            if (!string.IsNullOrWhiteSpace(_state.LastSemanticVisionResearchTopic))
                sb.AppendLine($"research: {_state.LastSemanticVisionResearchTopic}");
            if (!string.IsNullOrWhiteSpace(_state.LastSomaticToneDirective))
                sb.AppendLine($"somatic_tone: {_state.LastSomaticToneDirective}");
            sb.AppendLine("Rule: use screen flow as private grounding. If it reveals a concrete problem, offer a concrete action.");
            return sb.ToString().Trim();
        }

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
