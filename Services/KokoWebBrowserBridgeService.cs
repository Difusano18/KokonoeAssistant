using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebBrowserBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly KokoBrowserOperatorService _browser;
        private bool _disposed;

        public KokoWebBrowserBridgeService(KokoWebBridgeService bridge, KokoBrowserOperatorService browser)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _bridge.Register("browser.status", HandleStatusAsync);
            _bridge.Register("browser.history", HandleHistoryAsync);
            _bridge.Register("browser.close", HandleCloseAsync);
            _browser.StatusChanged += OnStatusChanged;
            _browser.ScreenshotTaken += OnScreenshotTaken;
        }

        private async Task<object?> HandleStatusAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return new
            {
                isReady = _browser.IsReady,
                url = _browser.Session.CurrentUrl,
                title = _browser.Session.PageTitle,
                status = _browser.Session.Status.ToString().ToLowerInvariant(),
                lastScreenshot = _browser.Session.LastScreenshotPath,
                historyCount = _browser.Session.History.Count
            };
        }

        private async Task<object?> HandleHistoryAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return _browser.Session.History
                .TakeLast(20)
                .Select(a => new
                {
                    kind = a.Kind,
                    target = a.Target,
                    success = a.Success,
                    result = Truncate(a.Result, 200),
                    at = a.At.ToString("HH:mm:ss")
                })
                .ToList();
        }

        private async Task<object?> HandleCloseAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            await _browser.CloseAsync().ConfigureAwait(false);
            return new { ok = true };
        }

        private void OnStatusChanged(string status, string? detail)
        {
            if (_disposed) return;
            _bridge.Publish("browser.status", new
            {
                status,
                url = _browser.Session.CurrentUrl,
                title = _browser.Session.PageTitle,
                detail
            });
        }

        private void OnScreenshotTaken(string path, string url)
        {
            if (_disposed) return;
            // WebView2 has no virtual-host mapping for the screenshots folder
            // (only the frontend dist directory is mapped, in ShellWindow), so
            // ship the image inline as a data URL instead of a file path the
            // page can't actually load.
            string? dataUrl = null;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                dataUrl = "data:image/png;base64," + Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("BROWSER-CATCH", "Failed to read screenshot for preview: " + ex.Message);
            }
            _bridge.Publish("browser.screenshot", new { path, url, dataUrl });
        }

        private static string? Truncate(string? text, int max)
            => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "...";

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _browser.StatusChanged -= OnStatusChanged;
            _browser.ScreenshotTaken -= OnScreenshotTaken;
        }
    }
}
