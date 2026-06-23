using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebSystemBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly KokoSystemOverlordService _overlord;
        private readonly SemaphoreSlim _scanGate = new(1, 1);
        private bool _disposed;

        public KokoWebSystemBridgeService(KokoWebBridgeService bridge, KokoSystemOverlordService overlord)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _overlord = overlord ?? throw new ArgumentNullException(nameof(overlord));
            _bridge.Register("system.snapshot", HandleSnapshotAsync);
            _bridge.Register("system.scan", HandleScanAsync);
            _bridge.Register("system_overlord_status", HandleSnapshotAsync);
        }

        private Task<object?> HandleSnapshotAsync(JToken? payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebSystemBridgeService));
            return Task.FromResult<object?>(Project(_overlord.LastSnapshot, _overlord.RenderConsole()));
        }

        private async Task<object?> HandleScanAsync(JToken? payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebSystemBridgeService));

            await _scanGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var roots = payload?["roots"] is JArray rootArray
                    ? rootArray.Select(x => x?.ToString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                    : null;
                var maxFiles = payload?["maxFiles"]?.Value<int?>();
                var snapshot = await _overlord.ScanAsync(roots, maxFiles, ct).ConfigureAwait(false);
                var projected = Project(snapshot, _overlord.RenderConsole());
                if (!_disposed)
                    _bridge.Publish("system.snapshot", projected);
                return projected;
            }
            finally
            {
                _scanGate.Release();
            }
        }

        private static object Project(KokoOverlordSnapshot snapshot, string console) => new
        {
            takenAt = snapshot.TakenAt,
            status = snapshot.Status,
            error = snapshot.Error,
            roots = snapshot.Roots.Take(8).ToArray(),
            scannedFiles = snapshot.ScannedFiles,
            totalBytes = snapshot.TotalBytes,
            console,
            files = snapshot.Files
                .OrderByDescending(file => file.ModifiedAt)
                .Take(12)
                .Select(file => new
                {
                    file.Path,
                    file.Name,
                    file.Extension,
                    file.SizeBytes,
                    file.CreatedAt,
                    file.ModifiedAt,
                    file.Bucket,
                    file.Signal
                })
                .ToArray(),
            processes = snapshot.Processes
                .OrderByDescending(process => process.WorkingSetMb)
                .Take(8)
                .Select(process => new
                {
                    process.ProcessName,
                    process.ProcessId,
                    process.WorkingSetMb,
                    process.WindowTitle
                })
                .ToArray(),
            proposals = snapshot.Proposals
                .Take(8)
                .Select(proposal => new
                {
                    proposal.Kind,
                    proposal.Title,
                    proposal.Reason,
                    riskTier = proposal.RiskTier.ToString(),
                    decision = proposal.Decision.ToString(),
                    proposal.PendingActionId,
                    targets = proposal.Targets.Take(8).ToArray()
                })
                .ToArray()
        };

        public void Dispose()
        {
            _disposed = true;
            _scanGate.Dispose();
        }
    }
}
