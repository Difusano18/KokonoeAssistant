using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoActiveAgencyService : IDisposable
    {
        private readonly string _dataDir;
        private readonly PcControlService _pc;
        private readonly ChatRepository _chat;
        private readonly KokoInternalBlackboardService _blackboard;
        private readonly KokoServiceHeartbeatService _heartbeat;
        private readonly Func<KokoWearableTelemetryService?> _wearableFactory;
        private readonly Func<KokoBrainEngine?> _brainFactory;
        private readonly PcActionExecutor _executor;
        private readonly System.Threading.Timer _timer;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _statePath;
        private AgencyState _state;
        private bool _started;

        public KokoActiveAgencyService(
            string dataDir,
            PcControlService pc,
            ChatRepository chat,
            KokoInternalBlackboardService blackboard,
            KokoServiceHeartbeatService heartbeat,
            Func<KokoWearableTelemetryService?> wearableFactory,
            Func<KokoBrainEngine?> brainFactory)
        {
            _dataDir = dataDir;
            Directory.CreateDirectory(_dataDir);
            _pc = pc;
            _chat = chat;
            _blackboard = blackboard;
            _heartbeat = heartbeat;
            _wearableFactory = wearableFactory;
            _brainFactory = brainFactory;
            _executor = new PcActionExecutor(pc: _pc);
            _statePath = Path.Combine(_dataDir, "active-agency-state.json");
            _state = LoadState();
            _timer = new System.Threading.Timer(_ => _ = TickAsync("timer"), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            _timer.Change(TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(45));
            _heartbeat.Update("ACTIVE_AGENCY", "armed", "safe local automation online");
            KokoSystemLog.Write("ACTIVE_AGENCY", "started");
        }

        private async Task TickAsync(string reason)
        {
            if (!await _gate.WaitAsync(0).ConfigureAwait(false))
                return;

            try
            {
                var now = DateTime.Now;
                await EvaluateWorkBootstrapAsync(now).ConfigureAwait(false);
                await EvaluateSomaticAudioAsync(now).ConfigureAwait(false);
                await EvaluateSleepLockAsync(now).ConfigureAwait(false);
                EvaluateSelfThrottle(now);
                SaveState();
            }
            catch (Exception ex)
            {
                _heartbeat.Update("ACTIVE_AGENCY", "error", ex.Message);
                KokoSystemLog.Write("ACTIVE_AGENCY", "tick failed: " + ex.Message);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task EvaluateWorkBootstrapAsync(DateTime now)
        {
            if (now - _state.LastWorkBootstrapAt < TimeSpan.FromHours(6))
                return;

            var fg = _pc.GetForegroundWindow();
            var text = $"{fg.ProcessName} {fg.Title}".ToLowerInvariant();
            var coding = ContainsAny(text, "devenv", "visual studio", "code", "rider", "jetbrains", "terminal", "powershell", "cmd");
            if (!coding)
                return;

            var idle = _pc.GetSystemInfo().IdleTime;
            if (idle > TimeSpan.FromMinutes(5))
                return;

            if (Process.GetProcesses().Any(p => p.ProcessName.Contains("obsidian", StringComparison.OrdinalIgnoreCase)))
                return;

            var plan = PcActionPlan.Single(
                "agency_work_bootstrap_open_obsidian",
                "openApp",
                "obsidian",
                PcActionRiskTier.SafeLocal);
            plan.UserFacingSummaryUk = "Work bootstrap: open Obsidian beside coding context.";
            var result = await _executor.ExecuteAsync(plan).ConfigureAwait(false);
            _state.LastWorkBootstrapAt = now;
            LogAction("work_bootstrap", result, "Detected coding context; opened Obsidian as working memory.");
        }

        private async Task EvaluateSomaticAudioAsync(DateTime now)
        {
            if (now - _state.LastAudioDampenAt < TimeSpan.FromMinutes(20))
                return;

            var wearable = _wearableFactory()?.State;
            if (wearable == null || !wearable.IsFresh(DateTime.UtcNow))
                return;

            var highStress = wearable.LiveStressScore >= 75 ||
                             (wearable.BpmDelta >= 22 && (wearable.Motion ?? 0) <= 0.20);
            if (!highStress)
                return;

            var volume = _pc.GetVolume();
            if (volume <= 30)
                return;

            var plan = PcActionPlan.Single(
                "agency_somatic_audio_dampen",
                "volumeSet",
                "25",
                PcActionRiskTier.SafeLocal);
            plan.UserFacingSummaryUk = "Somatic automation: reduce audio volume during high strain.";
            var result = await _executor.ExecuteAsync(plan).ConfigureAwait(false);
            _state.LastAudioDampenAt = now;
            LogAction("somatic_audio", result, $"Wearable strain {wearable.LiveStressScore}/100; volume {volume}% -> 25%.");
        }

        private async Task EvaluateSleepLockAsync(DateTime now)
        {
            if (now - _state.LastSleepLockProposalAt < TimeSpan.FromHours(2))
                return;

            var info = _pc.GetSystemInfo();
            if (info.IdleTime < TimeSpan.FromMinutes(30))
                return;

            var wearable = _wearableFactory()?.State;
            if (wearable == null || !wearable.IsFresh(DateTime.UtcNow))
                return;

            var sleepLike = wearable.SleepState == "probably_asleep" &&
                            wearable.OnWrist &&
                            (wearable.Motion ?? 0) <= 0.03 &&
                            wearable.CurrentBpm > 0 &&
                            (wearable.BaselineBpm <= 0 || wearable.CurrentBpm <= wearable.BaselineBpm + 8);
            if (!sleepLike)
                return;

            var plan = PcActionPlan.Single(
                "agency_sleep_lock_pc",
                "lockScreen",
                "",
                PcActionRiskTier.RiskyLocal);
            plan.UserFacingSummaryUk = "Sleep protection: lock PC after 30m idle and watch sleep telemetry.";
            var result = await _executor.ExecuteAsync(plan).ConfigureAwait(false);
            _state.LastSleepLockProposalAt = now;
            LogAction("sleep_lock", result, $"Idle={info.IdleTime.TotalMinutes:F0}m; wearable={wearable.SleepState}; motion={wearable.Motion:F2}; bpm={wearable.CurrentBpm:F0}.");
        }

        private void EvaluateSelfThrottle(DateTime now)
        {
            if (now - _state.LastSelfThrottleAt < TimeSpan.FromMinutes(30))
                return;

            var proc = Process.GetCurrentProcess();
            var workingSetMb = proc.WorkingSet64 / 1024.0 / 1024.0;
            if (workingSetMb < 1800)
                return;

            var settings = AppSettings.Load();
            var changed = false;
            if (settings.ScreenAwarenessIntervalMins < 20)
            {
                settings.ScreenAwarenessIntervalMins = 20;
                changed = true;
            }
            if (settings.ScreenAwarenessCommentCooldownMins < 30)
            {
                settings.ScreenAwarenessCommentCooldownMins = 30;
                changed = true;
            }

            if (!changed)
                return;

            settings.Save();
            _state.LastSelfThrottleAt = now;
            var message = $"[ACTION:resource_self_throttle] Kokonoe process memory {workingSetMb:F0} MB; screen scan interval raised to {settings.ScreenAwarenessIntervalMins}m and comment cooldown to {settings.ScreenAwarenessCommentCooldownMins}m.";
            AppendActionMessage(message);
            _blackboard.Publish("resource-agent", "self_throttle", message, 0.82);
            _heartbeat.Update("ACTIVE_AGENCY", "self_throttle", $"{workingSetMb:F0} MB");
            KokoSystemLog.Write("ACTIVE_AGENCY", message);
        }

        private void LogAction(string kind, PcActionExecutionResult result, string reason)
        {
            var status = result.Succeeded ? "executed" :
                result.RequiresConfirmation ? "pending_confirmation" :
                result.Blocked ? "blocked" : "failed";
            var actionId = string.IsNullOrWhiteSpace(result.PendingActionId) ? result.ActionId : "pc:" + result.PendingActionId;
            var message = $"[ACTION:{kind}] {status}; id={actionId}; reason={reason}; result={Trim(result.Message, 260)}";
            AppendActionMessage(message);
            _blackboard.Publish("pc-agent", kind, message, result.Succeeded ? 0.85 : result.RequiresConfirmation ? 0.65 : 0.35);
            _heartbeat.Update("ACTIVE_AGENCY", status, kind);
            KokoSystemLog.Write("ACTIVE_AGENCY", message);
        }

        private void AppendActionMessage(string message)
        {
            try
            {
                _chat.InsertMessage(new ChatRepository.ChatMessage
                {
                    Role = "assistant",
                    Author = "Kokonoe",
                    Content = message,
                    Timestamp = DateTime.Now
                });
                EventBus.PublishChatMessage(message, "assistant");
            }
            catch { }
        }

        private AgencyState LoadState()
        {
            try
            {
                return File.Exists(_statePath)
                    ? JsonConvert.DeserializeObject<AgencyState>(File.ReadAllText(_statePath)) ?? new AgencyState()
                    : new AgencyState();
            }
            catch { return new AgencyState(); }
        }

        private void SaveState()
        {
            try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented)); }
            catch { }
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string Trim(string? text, int max)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        public void Dispose()
        {
            _timer.Dispose();
            _gate.Dispose();
        }

        private sealed class AgencyState
        {
            public DateTime LastWorkBootstrapAt { get; set; } = DateTime.MinValue;
            public DateTime LastAudioDampenAt { get; set; } = DateTime.MinValue;
            public DateTime LastSleepLockProposalAt { get; set; } = DateTime.MinValue;
            public DateTime LastSelfThrottleAt { get; set; } = DateTime.MinValue;
        }
    }
}
