using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebWearBridgeService : IDisposable
    {
        private readonly KokoWearableTelemetryService _telemetry;
        private readonly KokoWearableBridgeService? _wearBridge;
        private bool _disposed;

        public KokoWebWearBridgeService(
            KokoWebBridgeService bridge,
            KokoWearableTelemetryService telemetry,
            KokoWearableBridgeService? wearBridge)
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _wearBridge = wearBridge;
            bridge.Register("wear.status", HandleStatusAsync);
        }

        private async Task<object?> HandleStatusAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebWearBridgeService));

            var state = _telemetry.State;
            return new
            {
                connected = state.IsFresh(DateTime.UtcNow),
                bridgeEnabled = AppSettings.Load().WearBridgeEnabled,
                bridgeRunning = _wearBridge?.IsRunning ?? false,
                deviceId = state.DeviceId,
                bpm = state.CurrentBpm,
                battery = state.BatteryPercent,
                charging = state.Charging,
                lastSampleUtc = state.LastSampleUtc,
                summary = state.Summary
            };
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
