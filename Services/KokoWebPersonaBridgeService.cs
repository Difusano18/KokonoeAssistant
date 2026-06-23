using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebPersonaBridgeService : IDisposable
    {
        private readonly KokoEmotionEngine _emotion;
        private bool _disposed;

        public KokoWebPersonaBridgeService(KokoWebBridgeService bridge, KokoEmotionEngine emotion)
        {
            _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            bridge.Register("persona.status", HandleStatusAsync);
        }

        private async Task<object?> HandleStatusAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebPersonaBridgeService));

            return new
            {
                mood = _emotion.Current.ToString(),
                bond = _emotion.Bond.ToString(),
                connection = _emotion.ConnectionScore
            };
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
