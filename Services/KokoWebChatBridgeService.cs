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
        private readonly Func<bool> _wasToolCallFallback;
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
                llm.ClearHistory,
                () => llm.GetDiagnosticsSnapshot().LastFallback == "tool_call_fallback")
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
            Action? resetHistory = null,
            Func<bool>? wasToolCallFallback = null)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _contextBuilder = contextBuilder ?? (_ => null);
            _resetHistory = resetHistory ?? (() => { });
            // Default true: delegate-injected (test) construction has no diagnostics signal
            // to distinguish a real tool-call fallback from an HTTP-error fallback, so it
            // falls back to the pre-L3 behavior of always treating a null stream as a mission.
            _wasToolCallFallback = wasToolCallFallback ?? (() => true);
            _bridge.Register("chat.send", HandleSendAsync);
            _bridge.Register("send_message", HandleSendAsync);
            _bridge.Register("chat.clear_history", HandleClearHistoryAsync);
            KokoActivityBus.OnActivity += OnActivity;
        }

        private void OnActivity(KokoActivity a)
        {
            if (_disposed) return;
            _bridge.Publish("koko.activity", new
            {
                kind = a.Kind,
                label = a.Label,
                detail = a.Detail,
                status = a.Status,
                at = a.At.ToString("HH:mm:ss")
            });
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

            // chat.send used to block the whole RPC until the reply (and a possible fallback
            // call) finished — a slow first token or a multi-round tool loop just past the
            // bridge's client-side timeout looked identical to a dead bridge, even though
            // chat.chunk events were arriving fine the whole time. Acknowledge immediately
            // and run the actual turn detached; chat.started/chunk/completed/error/canceled
            // already carry everything the UI needs.
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunChatPipelineAsync(text, streamId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    KokoSystemLog.Write("WEB-CHAT", $"chat pipeline failed for stream {streamId}: {ex}");
                    _bridge.Publish("chat.error", new { streamId, error = ex.Message, errorType = "pipeline" });
                }
            });

            return new { accepted = true, streamId };
        }

        private async Task RunChatPipelineAsync(string text, string streamId, CancellationToken ct)
        {
            // Two separate budgets instead of one 120s deadline for the whole turn: a real
            // multi-round tool loop (browser/web_search/delegate, several round trips) can
            // legitimately run past 120s while still making progress, and the old single
            // deadline killed it mid-flight with a misleading "no response from provider"
            // error. The frontend (Chat.ts) has no client-side watchdog of its own for the
            // turn duration — it just waits on chat.started/chunk/completed/error/canceled —
            // so widening the generation budget is the whole fix; no heartbeat event needed.
            using var contextTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var contextLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, contextTimeoutCts.Token);
            using var genTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            using var genLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, genTimeoutCts.Token);

            var sequence = 0;
            var emittedChunks = 0;
            try
            {
                // Task.Yield() alone doesn't move this off the UI thread: the WebView2 message
                // handler captures the Dispatcher SynchronizationContext, so a bare await would
                // just re-post the continuation back onto the same UI thread's queue. The context
                // builder does vault scans / embedding lookups that take real seconds, so it needs
                // an actual ThreadPool hop (same pattern as MainWindow's BuildContext callers).
                KokoActivityBus.Emit(new KokoActivity { Kind = "context", Label = "Збираю контекст", Status = "running" });
                var context = await Task.Run(() => _contextBuilder(text), contextLinkedCts.Token).ConfigureAwait(false);
                KokoActivityBus.Emit(new KokoActivity { Kind = "context", Label = "Збираю контекст", Status = "done" });
                _bridge.Publish("chat.started", new { streamId });

                var effectiveCt = genLinkedCts.Token;
                KokoActivityBus.Emit(new KokoActivity { Kind = "thinking", Label = "Генерую відповідь", Status = "running" });
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
                    // A null stream result means either a genuine tool-call (a real mission)
                    // or a streaming HTTP/connection failure (not one) — both used to be
                    // treated as a mission here. _wasToolCallFallback checks the diagnostics
                    // tag LlmService.SendStreamingAsync now records at each return-null point
                    // to tell them apart.
                    if (_wasToolCallFallback())
                    {
                        _bridge.Publish("mission.started", new
                        {
                            streamId,
                            goal = Trim(text, 100),
                            startedAt = DateTimeOffset.UtcNow
                        });
                    }
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
                    KokoActivityBus.Emit(new KokoActivity { Kind = "thinking", Label = "Генерую відповідь", Status = "failed" });
                    _bridge.Publish("chat.error", new { streamId, error = reply, errorType = "provider" });
                    return;
                }

                KokoActivityBus.Emit(new KokoActivity { Kind = "thinking", Label = "Генерую відповідь", Status = "done" });
                _bridge.Publish("chat.completed", new { streamId, reply, streamed = usedStreaming });
            }
            catch (OperationCanceledException) when (contextTimeoutCts.IsCancellationRequested)
            {
                KokoActivityBus.Emit(new KokoActivity { Kind = "context", Label = "Збираю контекст", Status = "failed" });
                _bridge.Publish("chat.error", new
                {
                    streamId,
                    error = "Збір контексту зайняв надто довго (20с).",
                    errorType = "timeout"
                });
            }
            catch (OperationCanceledException) when (genTimeoutCts.IsCancellationRequested)
            {
                KokoActivityBus.Emit(new KokoActivity { Kind = "thinking", Label = "Генерую відповідь", Status = "failed" });
                _bridge.Publish("chat.error", new
                {
                    streamId,
                    error = "Немає відповіді від провайдера за 300 секунд. Перевір API key і URL у Settings.",
                    errorType = "timeout"
                });
            }
            catch (OperationCanceledException)
            {
                KokoActivityBus.Emit(new KokoActivity { Kind = "thinking", Label = "Генерую відповідь", Status = "failed" });
                _bridge.Publish("chat.canceled", new { streamId });
            }
            catch (Exception ex)
            {
                KokoActivityBus.Emit(new KokoActivity { Kind = "thinking", Label = "Генерую відповідь", Status = "failed" });
                _bridge.Publish("chat.error", new { streamId, error = ex.Message });
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
            KokoActivityBus.OnActivity -= OnActivity;
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
