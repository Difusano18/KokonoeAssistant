using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebChatBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly Func<string, string?, Action<string>, CancellationToken, Task<string?>> _stream;
        private readonly Func<string, string?, Action<string>, CancellationToken, Task<string>> _fallback;
        private readonly Func<string, string?> _contextBuilder;
        private bool _disposed;

        public KokoWebChatBridgeService(
            KokoWebBridgeService bridge,
            LlmService llm,
            Func<string, string?>? contextBuilder = null)
            : this(
                bridge,
                (text, context, onChunk, ct) => llm.SendStreamingAsync(text, context, onChunk, ct),
                (text, context, onChunk, ct) => llm.SendAsync(text, extraContext: context, ct: ct, onChunk: onChunk),
                contextBuilder)
        {
        }

        public KokoWebChatBridgeService(
            KokoWebBridgeService bridge,
            Func<string, string?, Action<string>, CancellationToken, Task<string?>> stream,
            Func<string, string?, CancellationToken, Task<string>> fallback,
            Func<string, string?>? contextBuilder = null)
            : this(
                bridge,
                stream,
                (text, context, _, ct) => fallback(text, context, ct),
                contextBuilder)
        {
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));
        }

        public KokoWebChatBridgeService(
            KokoWebBridgeService bridge,
            Func<string, string?, Action<string>, CancellationToken, Task<string?>> stream,
            Func<string, string?, Action<string>, CancellationToken, Task<string>> fallback,
            Func<string, string?>? contextBuilder = null)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _contextBuilder = contextBuilder ?? (_ => null);
            _bridge.Register("chat.send", HandleSendAsync);
            _bridge.Register("send_message", HandleSendAsync);
        }

        private async Task<object?> HandleSendAsync(JToken? payload, CancellationToken ct)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebChatBridgeService));

            var text = payload?["text"]?.ToString()?.Trim() ?? "";
            var streamId = payload?["streamId"]?.ToString()?.Trim() ?? Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Chat message is empty.");
            if (text.Length > 32_000)
                throw new InvalidOperationException("Chat message exceeds 32000 characters.");

            var context = _contextBuilder(text);
            var sequence = 0;
            var emittedChunks = 0;
            _bridge.Publish("chat.started", new { streamId });
            try
            {
                var streamed = await _stream(text, context, chunk =>
                {
                    if (string.IsNullOrEmpty(chunk))
                        return;
                    emittedChunks++;
                    _bridge.Publish("chat.chunk", new { streamId, sequence = sequence++, chunk });
                }, ct).ConfigureAwait(false);

                var usedStreaming = streamed != null;
                var reply = streamed;
                if (reply == null)
                {
                    if (emittedChunks > 0)
                        _bridge.Publish("chat.reset", new { streamId, reason = "tool_fallback" });
                    var fallbackChunks = 0;
                    reply = await _fallback(text, context, chunk =>
                    {
                        if (string.IsNullOrEmpty(chunk))
                            return;
                        fallbackChunks++;
                        _bridge.Publish("chat.chunk", new { streamId, sequence = sequence++, chunk });
                    }, ct).ConfigureAwait(false);
                    usedStreaming = fallbackChunks > 0;
                }

                reply ??= "";
                _bridge.Publish("chat.completed", new { streamId, reply, streamed = usedStreaming });
                return new { streamId, reply, streamed = usedStreaming };
            }
            catch (OperationCanceledException)
            {
                _bridge.Publish("chat.canceled", new { streamId });
                throw;
            }
            catch (Exception ex)
            {
                _bridge.Publish("chat.error", new { streamId, error = ex.Message });
                throw;
            }
        }

        public void PublishExternalMessage(string role, string content)
        {
            if (_disposed || string.IsNullOrWhiteSpace(content))
                return;

            var safeRole = role.Equals("system", StringComparison.OrdinalIgnoreCase)
                ? "system"
                : "assistant";
            _bridge.Publish("chat.external", new
            {
                role = safeRole,
                content = content.Trim(),
                receivedAt = DateTimeOffset.UtcNow
            });
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
