using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Developer-only raw diagnostic tab: live KokoDevLogBus entries (full LLM request/
    /// response bodies, raw tool call args/results) plus a snapshot pull so the tab has
    /// something to show immediately on open instead of only what fires afterward.
    /// </summary>
    public sealed class KokoWebDevBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private bool _disposed;

        public KokoWebDevBridgeService(KokoWebBridgeService bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _bridge.Register("dev.snapshot", HandleSnapshotAsync);
            KokoDevLogBus.OnEntry += OnEntry;
        }

        private async Task<object?> HandleSnapshotAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed) throw new ObjectDisposedException(nameof(KokoWebDevBridgeService));
            return new { entries = KokoDevLogBus.Snapshot().Select(Project) };
        }

        private void OnEntry(KokoDevLogEntry entry)
        {
            if (!_disposed)
                _bridge.Publish("dev.entry", Project(entry));
        }

        private static object Project(KokoDevLogEntry entry) => new
        {
            kind = entry.Kind,
            label = entry.Label,
            content = entry.Content,
            at = entry.At.ToString("HH:mm:ss")
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            KokoDevLogBus.OnEntry -= OnEntry;
        }
    }
}
