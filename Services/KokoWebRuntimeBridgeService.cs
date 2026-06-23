using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebRuntimeBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly Func<object> _snapshotFactory;
        private bool _disposed;

        public KokoWebRuntimeBridgeService(KokoWebBridgeService bridge, Func<object>? snapshotFactory = null)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _snapshotFactory = snapshotFactory ?? BuildFromServiceContainer;
            _bridge.Register("runtime.snapshot", HandleSnapshotAsync);
            _bridge.Register("runtime.refresh", HandleRefreshAsync);
        }

        private Task<object?> HandleSnapshotAsync(JToken? payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebRuntimeBridgeService));
            return Task.FromResult<object?>(_snapshotFactory());
        }

        private Task<object?> HandleRefreshAsync(JToken? payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebRuntimeBridgeService));
            var snapshot = _snapshotFactory();
            _bridge.Publish("runtime.snapshot", snapshot);
            return Task.FromResult<object?>(snapshot);
        }

        public void Dispose()
            => _disposed = true;

        public static object BuildFromServiceContainer()
        {
            var process = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var llm = Safe(() => ServiceContainer.LlmService.GetDiagnosticsSnapshot());
            var wearable = Safe(() => ServiceContainer.WearableTelemetry.State);
            var bridge = Safe(() => ServiceContainer.WearableBridge);
            var bridgeDiagnostics = Safe(() => bridge?.Diagnostics);
            var connection = Safe(() => bridge?.GetConnectionSnapshot(wearable, now));
            var heartbeat = Safe(() => ServiceContainer.Heartbeat);

            return new
            {
                takenAt = now,
                process = new
                {
                    pid = process.Id,
                    workingSetMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
                    privateMemoryMb = Math.Round(process.PrivateMemorySize64 / 1024.0 / 1024.0, 1),
                    uptimeSeconds = Math.Max(0, (DateTime.Now - process.StartTime).TotalSeconds),
                    responding = process.Responding
                },
                llm = llm == null ? null : new
                {
                    llm.Status,
                    llm.Provider,
                    llm.Model,
                    llm.Channel,
                    llm.InFlight,
                    llm.TotalRequests,
                    llm.TotalFailures,
                    llm.ConsecutiveFailures,
                    llm.LastLatencyMs,
                    llm.LastError,
                    llm.LastFallback,
                    llm.LastStatusCode,
                    llm.LastRequestAt,
                    llm.LastSuccessAt,
                    llm.LastErrorAt
                },
                wearable = wearable == null ? null : new
                {
                    wearable.CurrentBpm,
                    wearable.BaselineBpm,
                    wearable.DeviceId,
                    wearable.TrustState,
                    wearable.TrustReason,
                    wearable.SleepState,
                    wearable.PresenceState,
                    wearable.LiveStressScore,
                    wearable.Summary,
                    wearable.LastSampleUtc,
                    fresh = wearable.IsFresh(now),
                    bridgeState = connection?.State ?? (bridgeDiagnostics?.IsRunning == true ? "RUNNING" : "OFFLINE"),
                    bridgeReason = connection?.Reason ?? bridgeDiagnostics?.LastError ?? "",
                    bridgePort = bridgeDiagnostics?.Port ?? 0,
                    bridgeSamples = bridgeDiagnostics?.TotalSamples ?? 0,
                    bridgeAuthFailures = bridgeDiagnostics?.TotalAuthFailures ?? 0,
                    bridgePendingCommands = bridgeDiagnostics?.PendingCommands ?? 0
                },
                heartbeat = heartbeat == null ? null : new
                {
                    markdownPath = heartbeat.MarkdownPath,
                    htmlPath = heartbeat.HtmlPath,
                    entries = heartbeat.Snapshot().Select(entry => new
                    {
                        entry.Service,
                        entry.Status,
                        entry.Detail,
                        entry.UpdatedAt,
                        ageSeconds = Math.Max(0, (DateTime.Now - entry.UpdatedAt).TotalSeconds)
                    }).ToArray()
                },
                logs = new
                {
                    system = KokoSystemLog.LogPath,
                    wearable = Safe(() => ServiceContainer.WearableTelemetry.LogPath) ?? ""
                }
            };
        }

        private static T? Safe<T>(Func<T> factory)
        {
            try { return factory(); }
            catch (Exception ex)
            {
                KokoSystemLog.Write("WEB-RUNTIME", "snapshot field failed: " + ex.Message);
                return default;
            }
        }
    }
}
