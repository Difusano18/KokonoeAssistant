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
        private readonly Action _resetHistory;
        private bool _disposed;

        public KokoWebChatBridgeService(
            KokoWebBridgeService bridge,
            LlmService llm,
            Func<string, string?>? contextBuilder = null)
            : this(
                bridge,
                (text, context, onChunk, ct) => llm.SendStreamingAsync(text, context, onChunk, ct),
                (text, context, onChunk, ct) => llm.SendAsync(text, extraContext: context, ct: ct, onChunk: onChunk),
                contextBuilder,
                llm.ClearHistory)
        {
        }

        public KokoWebChatBridgeService(
            KokoWebBridgeService bridge,
            Func<string, string?, Action<string>, CancellationToken, Task<string?>> stream,
            Func<string, string?, CancellationToken, Task<string>> fallback,
            Func<string, string?>? contextBuilder = null,
            Action? resetHistory = null)
            : this(
                bridge,
                stream,
                (text, context, _, ct) => fallback(text, context, ct),
                contextBuilder,
                resetHistory)
        {
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));
        }

        public KokoWebChatBridgeService(
            KokoWebBridgeService bridge,
            Func<string, string?, Action<string>, CancellationToken, Task<string?>> stream,
            Func<string, string?, Action<string>, CancellationToken, Task<string>> fallback,
            Func<string, string?>? contextBuilder = null,
            Action? resetHistory = null)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _contextBuilder = contextBuilder ?? (_ => null);
            _resetHistory = resetHistory ?? (() => { });
            _bridge.Register("chat.send", HandleSendAsync);
            _bridge.Register("send_message", HandleSendAsync);
            _bridge.Register("chat.clear_history", HandleClearHistoryAsync);
        }

        private async Task<object?> HandleSendAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();

            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebChatBridgeService));

            var text = payload?["text"]?.ToString()?.Trim() ?? "";
            var streamId = payload?["streamId"]?.ToString()?.Trim() ?? Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Chat message is empty.");
            if (text.Length > 32_000)
                throw new InvalidOperationException("Chat message exceeds 32000 characters.");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var effectiveCt = linkedCts.Token;

            var sequence = 0;
            var emittedChunks = 0;
            try
            {
                // Task.Yield() alone doesn't move this off the UI thread: the WebView2 message
                // handler captures the Dispatcher SynchronizationContext, so a bare await would
                // just re-post the continuation back onto the same UI thread's queue. The context
                // builder does vault scans / embedding lookups that take real seconds, so it needs
                // an actual ThreadPool hop (same pattern as MainWindow's BuildContext callers).
                var context = await Task.Run(() => _contextBuilder(text), effectiveCt).ConfigureAwait(false);
                _bridge.Publish("chat.started", new { streamId });

                var streamed = await _stream(text, context, chunk =>
                {
                    if (string.IsNullOrEmpty(chunk))
                        return;
                    emittedChunks++;
                    _bridge.Publish("chat.chunk", new { streamId, sequence = sequence++, chunk });
                }, effectiveCt).ConfigureAwait(false);

                var usedStreaming = streamed != null;
                var reply = streamed;
                if (reply == null)
                {
                    // This is the only point KokoWebChatBridgeService can observe that the
                    // model needed tool execution the streaming path couldn't handle —
                    // it has no visibility into individual tool calls mid-stream (that
                    // lives inside LlmService). Used as the proxy for "a real task is
                    // starting" rather than a precise multi-step-task detector.
                    _bridge.Publish("mission.started", new
                    {
                        streamId,
                        goal = Trim(text, 100),
                        startedAt = DateTimeOffset.UtcNow
                    });
                    if (emittedChunks > 0)
                        _bridge.Publish("chat.reset", new { streamId, reason = "tool_fallback" });
                    var fallbackChunks = 0;
                    reply = await _fallback(text, context, chunk =>
                    {
                        if (string.IsNullOrEmpty(chunk))
                            return;
                        fallbackChunks++;
                        _bridge.Publish("chat.chunk", new { streamId, sequence = sequence++, chunk });
                    }, effectiveCt).ConfigureAwait(false);
                    usedStreaming = fallbackChunks > 0;
                }

                reply ??= "";
                if (IsProviderError(reply))
                {
                    _bridge.Publish("chat.error", new { streamId, error = reply, errorType = "provider" });
                    return new { streamId, reply, streamed = false };
                }

                _bridge.Publish("chat.completed", new { streamId, reply, streamed = usedStreaming });
                return new { streamId, reply, streamed = usedStreaming };
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _bridge.Publish("chat.error", new
                {
                    streamId,
                    error = "Немає відповіді від провайдера за 120 секунд. Перевір API key і URL у Settings.",
                    errorType = "timeout"
                });
                throw;
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

        private async Task<object?> HandleClearHistoryAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            _resetHistory();
            KokoSystemLog.Write("LLM", "conversation history reset by user");
            return new { ok = true, clearedAt = DateTimeOffset.UtcNow };
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

        // LlmService.SendAsync can't change its return type to a richer result without
        // breaking every other caller, so on provider/connection failure it returns the
        // failure as plain reply text instead of throwing. These are the prefixes/markers
        // it uses for that (see LlmService.BuildFriendlyLlmError and SendAsync's catch
        // blocks) — kept in sync by hand since there's no shared type between the two files.
        private static bool IsProviderError(string reply) =>
            reply.StartsWith("[Provider]", StringComparison.Ordinal) ||
            reply.StartsWith("[Pool]", StringComparison.Ordinal) ||
            reply.Contains("LLM-запит відхилено", StringComparison.Ordinal) ||
            reply.Contains("відхилив LLM-запит", StringComparison.Ordinal) ||
            reply.Contains("Сервер моделі впав", StringComparison.Ordinal) ||
            reply.Contains("Ліміт запитів з'їдений", StringComparison.Ordinal);

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}
