using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KokonoeAssistant.Models;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebArtifactBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly KokoArtifactService _artifacts;
        private bool _disposed;

        public KokoWebArtifactBridgeService(KokoWebBridgeService bridge, KokoArtifactService artifacts)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
            _bridge.Register("artifacts.list", HandleListAsync);
            _bridge.Register("artifacts.open", HandleOpenAsync);
            _artifacts.ArtifactAdded += OnArtifactAdded;
        }

        private async Task<object?> HandleListAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return _artifacts.GetAll()
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .Select(Project)
                .ToList();
        }

        private async Task<object?> HandleOpenAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            var id = payload?["id"]?.ToString() ?? "";
            _artifacts.OpenInExplorer(id);
            return new { ok = true };
        }

        private void OnArtifactAdded(KokoArtifact artifact)
        {
            if (_disposed) return;
            _bridge.Publish("artifact.new", Project(artifact));
        }

        private static object Project(KokoArtifact a) => new
        {
            id = a.Id,
            title = a.Title,
            kind = a.Kind.ToString().ToLowerInvariant(),
            previewText = a.PreviewText,
            sourceUrl = a.SourceUrl,
            sizeLabel = a.SizeLabel,
            createdAt = a.CreatedAt.ToString("HH:mm dd MMM")
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _artifacts.ArtifactAdded -= OnArtifactAdded;
        }
    }
}
