using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebBridgeService : IDisposable
    {
        private readonly ConcurrentDictionary<string, Func<JToken?, CancellationToken, Task<object?>>> _handlers =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Action<string> _postJson;
        private readonly CoreWebView2? _webView;
        private readonly CancellationTokenSource _lifetime = new();
        private SynchronizationContext? _postContext;
        private bool _disposed;

        public KokoWebBridgeService(CoreWebView2 webView)
            : this(json => webView.PostWebMessageAsJson(json))
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _postContext = SynchronizationContext.Current;
            _webView.WebMessageReceived += OnWebMessageReceived;
        }

        public KokoWebBridgeService(Action<string> postJson)
        {
            _postJson = postJson ?? throw new ArgumentNullException(nameof(postJson));
            Register("ping", (_, _) => Task.FromResult<object?>("pong"));
        }

        public void Register(string method, Func<JToken?, CancellationToken, Task<object?>> handler)
        {
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException("Bridge method is empty.", nameof(method));
            _handlers[method.Trim()] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public IReadOnlyCollection<string> RegisteredMethods
            => _handlers.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

        public async Task HandleMessageAsync(string json, CancellationToken ct = default)
        {
            if (_disposed)
                return;

            string id = "";
            try
            {
                var request = JObject.Parse(json);
                if (!string.Equals(request["type"]?.ToString(), "request", StringComparison.OrdinalIgnoreCase))
                    return;

                id = request["id"]?.ToString()?.Trim() ?? "";
                var method = request["method"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException("Request id is required.");
                if (string.IsNullOrWhiteSpace(method))
                    throw new InvalidOperationException("Request method is required.");
                if (!_handlers.TryGetValue(method, out var handler))
                    throw new InvalidOperationException($"Unknown bridge method: {method}");

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
                var result = await handler(request["payload"], linked.Token);
                Post(new JObject
                {
                    ["type"] = "response",
                    ["id"] = id,
                    ["result"] = result == null ? JValue.CreateNull() : JToken.FromObject(result)
                });
                KokoSystemLog.Write("WEB-BRIDGE", $"request id={id} method={method} ok");
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Post(new JObject
                {
                    ["type"] = "response",
                    ["id"] = id,
                    ["error"] = ex.Message
                });
                KokoSystemLog.Write("WEB-BRIDGE", $"request id={id} failed: {ex.Message}");
            }
        }

        public void Publish(string channel, object? payload)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Bridge event channel is empty.", nameof(channel));
            Post(new JObject
            {
                ["type"] = "event",
                ["channel"] = channel.Trim(),
                ["payload"] = payload == null ? JValue.CreateNull() : JToken.FromObject(payload)
            });
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                await HandleMessageAsync(e.WebMessageAsJson, _lifetime.Token);
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("WEB-BRIDGE", "message dispatch failed: " + ex);
            }
        }

        private void Post(JObject envelope)
        {
            if (_disposed)
                return;
            var json = envelope.ToString(Formatting.None);
            if (_postContext != null && SynchronizationContext.Current != _postContext)
            {
                _postContext.Post(static state =>
                {
                    var post = (PendingPost)state!;
                    post.Owner.PostNow(post.Json);
                }, new PendingPost(this, json));
                return;
            }
            PostNow(json);
        }

        private void PostNow(string json)
        {
            if (!_disposed)
                _postJson(json);
        }

        private sealed record PendingPost(KokoWebBridgeService Owner, string Json);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lifetime.Cancel();
            if (_webView != null)
                _webView.WebMessageReceived -= OnWebMessageReceived;
            _lifetime.Dispose();
            _postContext = null;
        }
    }
}
