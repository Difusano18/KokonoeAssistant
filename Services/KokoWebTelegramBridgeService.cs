using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebTelegramBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly KokoTelegramRuntimeStatusService _status;
        private readonly Func<AppSettings> _settings;
        private bool _disposed;

        public KokoWebTelegramBridgeService(
            KokoWebBridgeService bridge,
            KokoTelegramRuntimeStatusService status,
            Func<AppSettings>? settings = null)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _settings = settings ?? AppSettings.Load;
            _bridge.Register("telegram.status", HandleStatusAsync);
            _bridge.Register("telegram_status", HandleStatusAsync);
            _status.Changed += OnChanged;
        }

        private async Task<object?> HandleStatusAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed) throw new ObjectDisposedException(nameof(KokoWebTelegramBridgeService));
            _status.RefreshConfiguration(_settings());
            return Project(_status.GetSnapshot());
        }

        private void OnChanged(KokoTelegramRuntimeSnapshot snapshot)
        {
            if (!_disposed)
                _bridge.Publish("telegram.status", Project(snapshot));
        }

        private static object Project(KokoTelegramRuntimeSnapshot snapshot) => new
        {
            updatedAt = snapshot.UpdatedAt,
            bot = ProjectChannel(snapshot.Bot),
            user = ProjectChannel(snapshot.User)
        };

        private static object ProjectChannel(KokoTelegramChannelStatus channel) => new
        {
            enabled = channel.Enabled,
            configured = channel.Configured,
            state = channel.State,
            account = channel.Account,
            lastActivity = channel.LastActivity,
            lastActivityAt = channel.LastActivityAt,
            lastError = channel.LastError,
            lastErrorAt = channel.LastErrorAt
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _status.Changed -= OnChanged;
        }
    }
}
