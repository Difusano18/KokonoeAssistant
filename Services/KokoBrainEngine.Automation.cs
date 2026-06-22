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

        // Reentrancy guards: skip tick if previous still running.
        private async Task GuardedThinkAsync()
        {
            if (Interlocked.CompareExchange(ref _thinkInFlight, 1, 0) != 0)
            {
                Log("ThinkAsync skipped — previous tick still in flight");
                return;
            }
            try
            {
                await SafeThinkAsync();
                CheckForAutonomousObjectives("think-timer");
                TryQueueAutonomousAgentCycle("think-timer");
            }
            finally { Interlocked.Exchange(ref _thinkInFlight, 0); }
        }

        private void HookAgentCompletionEvents()
        {
            if (_agentCompletionEventsHooked) return;
            _agentCompletionEventsHooked = true;
            try
            {
                ServiceContainer.AgentTasks.TaskCompleted += (task, notice) =>
                {
                    try { ObserveAgentTaskCompletion(task, notice); }
                    catch (Exception ex) { Log($"ObserveAgentTaskCompletion: {ex.Message}"); }
                };
            }
            catch (Exception ex)
            {
                Log($"HookAgentCompletionEvents: {ex.Message}");
            }
        }

        private void HookWearableTelemetryEvents()
        {
            if (_wearableEventsHooked) return;
            _wearableEventsHooked = true;
            try
            {
                ServiceContainer.WearableTelemetry.SampleAccepted += (result, sample) =>
                {
                    if (result.Accepted)
                        _ = Task.Run(() => HandleWearableSomaticEventAsync(result, sample));
                };
                Log("Wearable telemetry events hooked");
            }
            catch (Exception ex)
            {
                Log($"HookWearableTelemetryEvents: {ex.Message}");
            }
        }

        private void HookWearableActionEvents()
        {
            if (_wearableActionEventsHooked) return;
            _wearableActionEventsHooked = true;
            try
            {
                ServiceContainer.WearableBridge.ActionReceived += action =>
                {
                    _ = Task.Run(() => HandleWatchActionAsync(action));
                };
                Log("Wearable action events hooked");
            }
            catch (Exception ex)
            {
                Log($"HookWearableActionEvents: {ex.Message}");
            }
        }

        private async Task HandleWatchActionAsync(KokoWearableBridgeService.WearableActionRequest action)
        {
            try
            {
                var name = (action.Action ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(name))
                    return;

                lock (_lock)
                {
                    _state.LastWatchActionAt = DateTime.Now;
                    _state.LastWatchAction = name;
                    _state.AutonomyDecisionLog.Add($"[{DateTime.Now:HH:mm}] watch action: {name}");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }

                ServiceContainer.Blackboard.Publish("heart-agent", "watch_action", $"{name}: {action.Payload}", 0.9, action);
                ServiceContainer.Heartbeat.Update("WATCH_ACTION", "received", name);
                Log($"Watch action received: {name}; payload={TrimForLog(action.Payload, 120)}");

                switch (name)
                {
                    case "look_screen_now":
                    case "screen_now":
                    case "look":
                        await ForceScreenAwarenessAsync("watch_action:look_screen_now", "The user explicitly asked from the watch. Give a concise useful observation.");
                        break;
                    case "note_this":
                    case "note":
                        TryWriteWatchNote(action);
                        break;
                    case "im_stressed":
                    case "stress":
                        await ForceScreenAwarenessAsync("watch_action:im_stressed", "Protective tone. Look for what might be stressing the user and suggest one low-risk next step.");
                        ServiceContainer.WearableBridge.QueueCommand("vibrate", "Kokonoe: бачу сигнал стресу. Зроби коротку паузу.");
                        break;
                    default:
                        Log($"Watch action ignored: unknown action {name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"HandleWatchActionAsync: {ex.Message}");
            }
        }

        private void TryWriteWatchNote(KokoWearableBridgeService.WearableActionRequest action)
        {
            try
            {
                var now = DateTime.Now;
                var foreground = ServiceContainer.PcControl.GetForegroundWindow();
                var text = string.IsNullOrWhiteSpace(action.Payload)
                    ? $"Watch note requested. Foreground: {foreground.ProcessName} / {foreground.Title}"
                    : action.Payload.Trim();
                var path = $"Kokonoe/Watch Notes/{now:yyyy-MM-dd HHmmss} - quick note.md";
                _obsidian.WriteNote(path, $"---\ntype: watch-note\ntags: [kokonoe, wearable, quick-note]\ncreated: {now:O}\n---\n\n# Watch note\n\n{text}\n\nForeground: `{foreground.ProcessName}` / {foreground.Title}\n");
                Memory.RecordEpisodeBlocking(text, "watch_note", 0.72f, new[] { "wearable", "watch-action" });
                ServiceContainer.Blackboard.Publish("vault-agent", "watch_note", path, 0.78);
                Log($"Watch note wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"TryWriteWatchNote: {ex.Message}");
            }
        }

        private async Task HandleWearableSomaticEventAsync(KokoWearableIngestResult result, KokoWearableSample sample)
        {
            try
            {
                var now = DateTime.Now;
                TryApplySomaticAutomation(result, sample, now);

                if (string.IsNullOrWhiteSpace(result.EventKind))
                    return;

                if (now - _lastSomaticVisionTriggerAt < TimeSpan.FromMinutes(4))
                {
                    Log($"Wearable somatic event suppressed: cooldown {result.EventKind} {result.EventReason}");
                    return;
                }

                _lastSomaticVisionTriggerAt = now;
                var protective = result.EventKind is "stress_spike" or "high_strain";
                var tone = protective
                    ? "Protective/concerned: be concrete, low-pressure, and reduce teasing."
                    : "Fatigue-aware: shorter, quieter, avoid pushing.";
                var reason = $"wearable_{result.EventKind}: {result.EventReason}";

                Log($"Triggering Scan: {reason}");
                KokoSystemLog.Write("BRAIN", $"Triggering Scan: {reason}");

                if (protective)
                {
                    try
                    {
                        ServiceContainer.WearableBridge.QueueCommand(
                            "vibrate",
                            "Kokonoe: коротка пауза. Пульс підскочив, генію.");
                    }
                    catch (Exception ex)
                    {
                        Log($"Wearable vibrate command failed: {ex.Message}");
                    }
                }

                await ForceScreenAwarenessAsync(reason, tone).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"HandleWearableSomaticEventAsync: {ex.Message}");
            }
        }

        private void TryApplySomaticAutomation(KokoWearableIngestResult result, KokoWearableSample sample, DateTime now)
        {
            try
            {
                var state = result.State;
                if (state == null)
                    return;

                ServiceContainer.Heartbeat.Update("HEART_AGENT", "sample", state.Summary);
                ServiceContainer.Blackboard.Publish("heart-agent", "sample", state.Summary, state.LiveStressScore / 100.0);

                if (state.ContextSignal == "woke_up" &&
                    (_state.LastMorningBriefingAt <= DateTime.MinValue || now - _state.LastMorningBriefingAt > TimeSpan.FromHours(8)))
                {
                    TryWriteMorningBriefing(now, state);
                }

                var activity = $"{state.Activity} {sample.Activity}".ToLowerInvariant();
                var workout = activity.Contains("running") || activity.Contains("workout") ||
                              activity.Contains("exercise") || ((state.Motion ?? 0) >= 0.70 && state.CurrentBpm >= 95);
                if (workout)
                {
                    lock (_lock)
                    {
                        _state.CurrentSomaticAutomationMode = "coach";
                        _state.ProactiveMutedUntil = now.AddMinutes(45);
                        _state.ProactiveMuteReason = "workout_nonessential_notifications_muted";
                        _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] somatic mode coach; workout detected; notifications muted");
                        if (_state.AutonomyDecisionLog.Count > 80)
                            _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                        SaveState();
                    }
                    ServiceContainer.Heartbeat.Update("SOMATIC_MODE", "coach", "workout/running detected");
                    ServiceContainer.Blackboard.Publish("heart-agent", "somatic_mode", "coach mode; nonessential proactive muted", 0.82);
                }

                var highStrain = state.LiveStressScore >= 78 ||
                    (state.CurrentBpm > 0 && state.BaselineBpm > 0 && state.CurrentBpm >= state.BaselineBpm + 24);
                if (highStrain && now - _state.LastSomaticBreakPromptAt > TimeSpan.FromMinutes(25))
                {
                    lock (_lock)
                    {
                        _state.CurrentSomaticAutomationMode = "quiet_operator";
                        _state.LastSomaticBreakPromptAt = now;
                        _state.LastSomaticToneDirective = "quiet_operator: stress rising; concrete help, low noise";
                        _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] somatic quiet_operator; stress={state.LiveStressScore}/100 bpm={state.CurrentBpm:F0}");
                        if (_state.AutonomyDecisionLog.Count > 80)
                            _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                        SaveState();
                    }

                    try
                    {
                        ServiceContainer.WearableBridge.QueueCommand(
                            "vibrate",
                            $"Kokonoe: breathe. {state.CurrentBpm:F0} bpm is not a trophy.");
                    }
                    catch (Exception ex)
                    {
                        Log($"Somatic high-strain vibrate failed: {ex.Message}");
                    }

                    ServiceContainer.Heartbeat.Update("SOMATIC_MODE", "quiet_operator", $"stress {state.LiveStressScore}/100");
                    ServiceContainer.Blackboard.Publish("heart-agent", "quiet_operator", state.Summary, 0.88);
                }
            }
            catch (Exception ex)
            {
                Log($"TryApplySomaticAutomation: {ex.Message}");
            }
        }

        private void TryWriteMorningBriefing(DateTime now, KokoWearableState state)
        {
            try
            {
                var recent = string.Join("\n", _state.Observations.TakeLast(12).Select(o => "- " + o));
                var insight = string.IsNullOrWhiteSpace(_state.LastReflectiveInsightSummary)
                    ? "No fresh reflective insight yet."
                    : _state.LastReflectiveInsightSummary;
                var path = $"Kokonoe/Morning Briefings/{now:yyyy-MM-dd} - wearable wake briefing.md";
                var body = $"""
---
type: morning-briefing
tags: [kokonoe, wearable, briefing]
created: {now:O}
---

# Morning Briefing

Wake signal: {state.ContextSignal}
Wearable: {state.Summary}

## Planned While You Were Away

{insight}

## Recent Observations

{recent}
""";
                _obsidian.WriteNote(path, body);
                lock (_lock)
                {
                    _state.LastMorningBriefingAt = now;
                    _state.LastMorningBriefingSummary = path;
                    SaveState();
                }
                ServiceContainer.Blackboard.Publish("vault-agent", "morning_briefing", path, 0.86);
                Log($"Morning briefing wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"TryWriteMorningBriefing: {ex.Message}");
            }
        }

        private void CheckpointStateAndHeartbeat()
        {
            try
            {
                lock (_lock)
                {
                    AuditPromiseLedgerLocked("heartbeat");
                    SaveState();
                }
                TrySelfHealWearableBridge("heartbeat");
                ServiceContainer.Heartbeat.Update("BRAIN", "online", $"mood={_state.CurrentMood}; thoughts={_state.PendingThoughts.Count}; autonomy={TrimForLog(_state.LastAutonomyDecision, 80)}");
                ServiceContainer.Heartbeat.Update("VISION", _screenAwarenessInFlight == 1 ? "scanning" : "idle", $"last={FormatAge(_state.LastScreenAwarenessAt)}; failures={_state.VisionFailureCount}");
                ServiceContainer.Heartbeat.Update("WATCH", ServiceContainer.WearableBridge.GetConnectionSnapshot().State, ServiceContainer.WearableTelemetry.State.Summary);
                ServiceContainer.Heartbeat.Update("BLACKBOARD", "online", $"{ServiceContainer.Blackboard.Recent(10).Count} recent events");
                KokoSystemLog.Write("BRAIN", "state checkpoint saved");
            }
            catch (Exception ex)
            {
                Log($"CheckpointStateAndHeartbeat: {ex.Message}");
            }
        }

        private void TrySelfHealWearableBridge(string reason)
        {
            try
            {
                var now = DateTime.Now;
                var bridge = ServiceContainer.WearableBridge;
                var diagnostics = bridge.Diagnostics;
                var connection = bridge.GetConnectionSnapshot();
                if (bridge.IsRunning)
                {
                    if (!string.IsNullOrWhiteSpace(diagnostics.LastError) &&
                        !connection.IsLinked &&
                        now - _state.LastBridgeSelfHealAt > TimeSpan.FromMinutes(10))
                    {
                        lock (_lock)
                        {
                            _state.LastBridgeSelfHealAt = now;
                            AppendActionJournalLocked("self-heal.bridge.observe", diagnostics.LastError, "attention", reason);
                            SaveState();
                        }
                    }
                    return;
                }

                lock (_lock)
                {
                    if (now - _state.LastBridgeSelfHealAt < TimeSpan.FromMinutes(5))
                        return;
                    _state.LastBridgeSelfHealAt = now;
                    AppendActionJournalLocked("self-heal.bridge.restart", "wearable bridge was stopped; attempting Start()", "started", reason);
                    SaveState();
                }
                bridge.Start();
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("SELF_HEAL", "wearable bridge heal failed: " + ex.Message);
            }
        }

        private static string FormatAge(DateTime at)
        {
            if (at <= DateTime.MinValue)
                return "never";
            var age = DateTime.Now - at;
            if (age.TotalSeconds < 90)
                return $"{age.TotalSeconds:0}s ago";
            if (age.TotalMinutes < 90)
                return $"{age.TotalMinutes:0}m ago";
            return $"{age.TotalHours:0.0}h ago";
        }

        private void ObserveAgentTaskCompletion(KokoAgentTask task, KokoAgentCompletionNotice notice)
        {
            if (task.Steps.All(s => s.Kind != KokoAgentStepKind.InsightExtraction))
                return;

            var result = task.Steps
                .Where(s => s.Kind == KokoAgentStepKind.InsightExtraction && !string.IsNullOrWhiteSpace(s.Result))
                .OrderByDescending(s => s.FinishedAt ?? DateTime.MinValue)
                .Select(s => s.Result.Trim())
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
                result = notice.Notice;

            lock (_lock)
            {
                var thought = "[agent-insight] " + TrimStateMention(result);
                if (!_state.PendingThoughts.Any(t => string.Equals(t, thought, StringComparison.OrdinalIgnoreCase)))
                    _state.PendingThoughts.Add(thought);
                if (_state.PendingThoughts.Count > 20)
                    _state.PendingThoughts.RemoveRange(0, _state.PendingThoughts.Count - 20);

                _state.LastBackgroundVaultScanAt = DateTime.Now;
                _state.LastBackgroundVaultScanSummary = TrimStateMention(result);
                _state.LastAutonomyDecision = $"agent_completed:{task.Id}";
                _state.LastAutonomyDecisionAt = DateTime.Now;
                _state.AutonomyDecisionLog.Add($"[{DateTime.Now:HH:mm}] insight task {task.Id} completed");
                if (_state.AutonomyDecisionLog.Count > 80)
                    _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                SaveState();
            }
        }

        private void CheckForAutonomousObjectives(string source)
        {
            try
            {
                var now = DateTime.Now;
                var level = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
                if (level <= 0)
                    return;

                TryPredictiveIdlePrompt(source, now, level);
                TrySelfHealWearableBridge(source, now);

                var lastUser = _chatRepo.GetMessages(20)
                    .Where(m => m.Role == "user")
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();
                if (lastUser == null)
                    return;

                var silence = now - lastUser.Timestamp;
                if (silence < TimeSpan.FromMinutes(30))
                    return;

                TryRunMemorySelfHealing("autonomous-objectives");
                TryWriteReflectiveInsight(source, now, silence);

                DateTime lastScan;
                lock (_lock) { lastScan = _state.LastBackgroundVaultScanAt; }
                if (now - lastScan < TimeSpan.FromHours(6))
                    return;

                var existing = ServiceContainer.AgentTasks.GetSnapshot().Tasks.Any(t =>
                    t.Status is KokoAgentTaskStatus.Pending or KokoAgentTaskStatus.Running &&
                    t.Objective.Contains("Background Vault Scanner", StringComparison.OrdinalIgnoreCase));
                if (existing)
                    return;

                var objective = "Background Vault Scanner: Проаналізуй останні 10 змінених нотаток в Obsidian, запусти vault_cluster.py для тематичного групування, знайди цікаві факти, суперечності або задачі. Запиши результат як insight, без очікування команди користувача.";
                var task = ServiceContainer.AgentTasks.AddTask(objective, priority: 3);
                ServiceContainer.AgentTasks.Start();

                lock (_lock)
                {
                    _state.LastBackgroundVaultScanAt = now;
                    _state.LastAutonomyDecision = $"queued_background_vault_scan:{task.Id}";
                    _state.LastAutonomyDecisionAt = now;
                    _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] {source} queued background vault scan {task.Id}");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                Log($"CheckForAutonomousObjectives: {ex.Message}");
            }
        }

        private void TryRunMemorySelfHealing(string source)
        {
            var settings = AppSettings.Load();
            if (Math.Clamp(settings.ProactiveAutonomyLevel, 0, 3) < 2)
                return;

            lock (_lock)
            {
                if (_state.LastMemorySelfHealAt > DateTime.MinValue &&
                    DateTime.Now - _state.LastMemorySelfHealAt < TimeSpan.FromHours(12))
                    return;
            }

            if (Interlocked.CompareExchange(ref _memorySelfHealInFlight, 1, 0) != 0)
                return;

            _ = Task.Run(() =>
            {
                try
                {
                    var result = _obsidian.SelfHealMemoryConflicts();
                    lock (_lock)
                    {
                        _state.LastMemorySelfHealAt = DateTime.Now;
                        _state.LastMemorySelfHealSummary = result;
                        _state.LastMemorySelfHealError = "";
                        _state.AutonomyDecisionLog.Add($"[{DateTime.Now:HH:mm}] {source} memory self-heal: {TrimStateMention(result)}");
                        if (_state.AutonomyDecisionLog.Count > 80)
                            _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                        SaveState();
                    }
                    Log("MemorySelfHeal: " + result);
                }
                catch (Exception ex)
                {
                    lock (_lock)
                    {
                        _state.LastMemorySelfHealAt = DateTime.Now;
                        _state.LastMemorySelfHealError = ex.Message;
                        SaveState();
                    }
                    Log($"MemorySelfHeal failed: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _memorySelfHealInFlight, 0);
                }
            });
        }

        private void TryPredictiveIdlePrompt(string source, DateTime now, int autonomyLevel)
        {
            if (autonomyLevel < 2)
                return;
            if (now.Hour != 9 || now.Minute < 10 || now.Minute > 45)
                return;
            if (now - _lastPredictiveIdlePromptAt < TimeSpan.FromHours(6))
                return;

            TimeSpan idle;
            try { idle = ServiceContainer.PcControl.GetSystemInfo().IdleTime; }
            catch (Exception ex)
            {
                Log($"PredictiveIdlePrompt skipped: idle read failed: {ex.Message}");
                return;
            }

            if (idle < TimeSpan.FromMinutes(10))
                return;

            string forecast;
            try { forecast = ServiceContainer.Predictor.GetForecastContext(); }
            catch (Exception ex) { forecast = $"predictor unavailable: {ex.Message}"; }

            _lastPredictiveIdlePromptAt = now;
            Log($"PredictiveIdlePrompt firing: source={source}; idle={idle.TotalMinutes:0.0}m; forecast={TrimStateMention(forecast)}");
            _ = Task.Run(async () =>
            {
                var message = "09:10, а комп'ютер досі в ступорі. Ти живий, чи мені вмикати ранковий запуск мозку примусово?";
                await SendTgAndLog(message, "predictive_observation").ConfigureAwait(false);
            });
        }

        private void TryWriteReflectiveInsight(string source, DateTime now, TimeSpan silence)
        {
            if (silence < TimeSpan.FromHours(4))
                return;

            DateTime lastInsight;
            lock (_lock) lastInsight = _state.LastReflectiveInsightAt;
            if (lastInsight > DateTime.MinValue && now - lastInsight < TimeSpan.FromHours(4))
                return;
            if (now - _lastReflectiveInsightAt < TimeSpan.FromMinutes(30))
                return;
            _lastReflectiveInsightAt = now;

            try
            {
                List<string> observations;
                List<string> thoughts;
                List<string> initiatives;
                string screenSummary;
                lock (_lock)
                {
                    observations = _state.Observations.TakeLast(12).ToList();
                    thoughts = _state.PendingThoughts.TakeLast(8).ToList();
                    initiatives = _state.InitiativeReasonLog.TakeLast(8).ToList();
                    screenSummary = $"{_state.LastScreenAwarenessMode}: {_state.LastScreenAwarenessSummary}; {_state.LastScreenSituationProgress}; {_state.LastScreenSituationBehavior}";
                }

                var insight = BuildReflectiveInsightText(now, source, silence, observations, thoughts, initiatives, screenSummary);
                var path = $"Kokonoe/Reflective Insights/{now:yyyy-MM-dd HHmm} - autonomous insight.md";
                _obsidian.WriteNote(path, insight);
                Memory.RecordEpisodeBlocking(
                    $"Autonomous reflective insight: {TrimStateMention(string.Join("; ", observations.TakeLast(3)))}",
                    "reflective",
                    0.62f,
                    new[] { "autonomy", "reflection", "screen", "patterns" });

                lock (_lock)
                {
                    _state.LastReflectiveInsightAt = now;
                    _state.LastReflectiveInsightSummary = TrimStateMention(insight);
                    _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] {source} wrote reflective insight after {silence.TotalHours:0.0}h inactivity");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }
                Log($"ReflectiveInsight wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"ReflectiveInsight failed: {ex.Message}");
            }
        }

        private static string BuildReflectiveInsightText(
            DateTime now,
            string source,
            TimeSpan silence,
            IReadOnlyList<string> observations,
            IReadOnlyList<string> thoughts,
            IReadOnlyList<string> initiatives,
            string screenSummary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"created: {now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("tags: [kokonoe, reflective-insight, autonomous]");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Autonomous Reflective Insight");
            sb.AppendLine();
            sb.AppendLine($"Source: {source}");
            sb.AppendLine($"User inactivity window: {silence.TotalHours:0.0}h");
            sb.AppendLine($"Last screen: {screenSummary}");
            sb.AppendLine();
            AppendInsightList(sb, "Recent observations", observations);
            AppendInsightList(sb, "Pending thoughts", thoughts);
            AppendInsightList(sb, "Initiative trace", initiatives);
            sb.AppendLine("## Pattern");
            sb.AppendLine("- If the same screen state, silence, or task residue repeats, prefer one concrete next action over generic checking.");
            sb.AppendLine("- Treat wearable/screen/file signals as context, not certainty. Useful suspicion, not prophecy.");
            return sb.ToString().Trim() + Environment.NewLine;
        }

        private static void AppendInsightList(StringBuilder sb, string title, IReadOnlyList<string> items)
        {
            sb.AppendLine($"## {title}");
            if (items.Count == 0)
            {
                sb.AppendLine("- none");
                sb.AppendLine();
                return;
            }
            foreach (var item in items)
                sb.AppendLine("- " + TrimStateMention(item));
            sb.AppendLine();
        }

        private void TrySelfHealWearableBridge(string source, DateTime now)
        {
            try
            {
                var bridge = ServiceContainer.WearableBridge;
                var diagnostics = bridge.Diagnostics;
                var shouldRestart = !diagnostics.IsRunning || !string.IsNullOrWhiteSpace(diagnostics.LastError);
                if (!shouldRestart)
                    return;

                DateTime lastHeal;
                lock (_lock) lastHeal = _state.LastBridgeSelfHealAt;
                if (lastHeal > DateTime.MinValue && now - lastHeal < TimeSpan.FromMinutes(5))
                    return;

                Log($"Bridge self-heal: source={source}; running={diagnostics.IsRunning}; error={diagnostics.LastError}; port={diagnostics.Port}");
                ServiceContainer.ReloadWearableBridge();
                lock (_lock)
                {
                    _state.LastBridgeSelfHealAt = now;
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                Log($"Bridge self-heal failed: {ex.Message}");
            }
        }

        private void TryQueueAutonomousAgentCycle(string source)
        {
            try
            {
                var now = DateTime.Now;
                if (now - _lastAutonomousAgentTaskAt < TimeSpan.FromMinutes(20))
                    return;

                string? objective = null;
                lock (_lock)
                {
                    objective = _state.PendingThoughts
                        .LastOrDefault(t => !string.IsNullOrWhiteSpace(t) && t.Length > 18);
                    if (string.IsNullOrWhiteSpace(objective) && _state.LastAutonomyShouldAct && !string.IsNullOrWhiteSpace(_state.LastAutonomyReason))
                        objective = _state.LastAutonomyReason;
                }

                if (string.IsNullOrWhiteSpace(objective))
                    return;

                var task = ServiceContainer.AgentTasks.AddTask($"APEO/{source}: {objective}", priority: 3);
                ServiceContainer.AgentTasks.Start();
                _lastAutonomousAgentTaskAt = now;

                lock (_lock)
                {
                    _state.LastAutonomyDecision = $"queued_agent_task:{task.Id}";
                    _state.LastAutonomyDecisionAt = now;
                    _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] {source} queued agent task {task.Id}");
                    if (_state.AutonomyDecisionLog.Count > 80)
                        _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                Log($"TryQueueAutonomousAgentCycle: {ex.Message}");
            }
        }

        private async Task GuardedResourceGuardianAsync()
        {
            if (Interlocked.CompareExchange(ref _resourceGuardianInFlight, 1, 0) != 0)
            {
                Log("ResourceGuardian skipped - previous tick still in flight");
                return;
            }

            try { await SafeResourceGuardianAsync(); }
            finally { Interlocked.Exchange(ref _resourceGuardianInFlight, 0); }
        }

        private async Task SafeResourceGuardianAsync()
        {
            if (_disposed) return;

            var now = DateTime.Now;
            DateTime lastAt;
            DateTime lastPromptAt;
            string modeSeed;
            lock (_lock)
            {
                lastAt = _state.LastResourceGuardianAt;
                lastPromptAt = _state.LastResourceGuardianPromptAt;
                modeSeed = string.IsNullOrWhiteSpace(_state.LastScreenAwarenessMode)
                    ? _state.LastScreenAwarenessWindow
                    : _state.LastScreenAwarenessMode;
            }

            if (lastAt > DateTime.MinValue && now - lastAt < TimeSpan.FromMinutes(5))
                return;

            SystemInfo info;
            try
            {
                info = await Task.Run(() => ServiceContainer.PcControl.GetSystemInfo()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"ResourceGuardian system info failed: {ex.Message}");
                return;
            }

            var decision = KokoResourceGuardianService.Evaluate(info, modeSeed, now, lastPromptAt);
            lock (_lock)
            {
                _state.LastResourceGuardianAt = now;
                _state.LastWorkMode = decision.WorkMode;
                _state.LastResourceGuardianSummary =
                    $"{decision.WorkMode}; cpu {info.CpuPercent:F1}%; ram {info.RamUsedGb:F1}/{info.RamTotalGb:F1} GB; {decision.Reason}";
                _state.AutonomyDecisionLog.Add($"[{now:HH:mm}] resource guardian: {_state.LastResourceGuardianSummary}");
                if (_state.AutonomyDecisionLog.Count > 80)
                    _state.AutonomyDecisionLog.RemoveRange(0, _state.AutonomyDecisionLog.Count - 80);
                SaveState();
            }

            if (!decision.ShouldPrompt || string.IsNullOrWhiteSpace(decision.Message))
                return;

            var sent = await SendTgAndLog(decision.Message, "resource_guardian");
            lock (_lock)
            {
                _state.LastResourceGuardianPromptAt = now;
                if (sent)
                    _state.LastSpontaneousAt = now;
                SaveState();
            }
            if (sent)
            {
                try { OnNewMessage?.Invoke("assistant", decision.Message); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SafeResourceGuardianAsync failed near source line 1427: " + ex); }
            }
        }
    }
}
