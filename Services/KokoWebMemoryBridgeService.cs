using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebMemoryBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly Func<object> _snapshotFactory;
        private bool _disposed;

        public KokoWebMemoryBridgeService(KokoWebBridgeService bridge, KokoMemoryEngine memory)
            : this(bridge, () => BuildSnapshot(memory))
        {
        }

        public KokoWebMemoryBridgeService(KokoWebBridgeService bridge, Func<object> snapshotFactory)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _snapshotFactory = snapshotFactory ?? throw new ArgumentNullException(nameof(snapshotFactory));
            _bridge.Register("memory.snapshot", HandleSnapshotAsync);
            _bridge.Register("memory.refresh", HandleRefreshAsync);
        }

        private Task<object?> HandleSnapshotAsync(Newtonsoft.Json.Linq.JToken? payload, CancellationToken ct)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebMemoryBridgeService));
            return Task.FromResult<object?>(_snapshotFactory());
        }

        private Task<object?> HandleRefreshAsync(Newtonsoft.Json.Linq.JToken? payload, CancellationToken ct)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebMemoryBridgeService));
            var snapshot = _snapshotFactory();
            _bridge.Publish("memory.snapshot", snapshot);
            return Task.FromResult<object?>(snapshot);
        }

        private static object BuildSnapshot(KokoMemoryEngine memory)
        {
            var facts = memory.GetTopFacts(8)
                .Select(f => new
                {
                    id = f.Id,
                    content = f.Content,
                    category = f.Category,
                    importance = f.Importance,
                    confirmCount = f.ConfirmCount,
                    lastSeen = f.LastSeen,
                    tags = f.Tags.Take(5).ToArray()
                })
                .ToArray();

            var episodes = memory.GetRecentEpisodes(6)
                .Select(e => new
                {
                    id = e.Id,
                    summary = e.Summary,
                    emotionalTone = e.EmotionalTone,
                    intensity = e.Intensity,
                    when = e.When,
                    keywords = e.Keywords.Take(5).ToArray()
                })
                .ToArray();

            var sessionFacts = memory.Working.CurrentSessionFacts
                .TakeLast(8)
                .ToArray();

            return new
            {
                takenAt = DateTime.UtcNow,
                factCount = memory.Facts.Count,
                episodeCount = memory.Episodes.Count,
                sessionFactCount = memory.Working.CurrentSessionFacts.Count,
                sessionStartedAt = memory.Working.SessionStart,
                currentContext = memory.Working.CurrentContext,
                lastUserMessage = memory.Working.LastUserMessage,
                facts,
                episodes,
                sessionFacts
            };
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
