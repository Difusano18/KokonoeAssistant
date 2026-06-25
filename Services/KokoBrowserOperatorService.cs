using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KokonoeAssistant.Models;
using Microsoft.Playwright;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Playwright-based browser operator. Headed Chromium window, visible to
    /// the user, one session per app run.
    /// </summary>
    public sealed class KokoBrowserOperatorService : IAsyncDisposable
    {
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IPage? _page;
        private readonly string _screenshotsDir;
        private bool _initialized;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public event Action<string, string?>? StatusChanged;
        public event Action<string, string>? ScreenshotTaken;

        public BrowserSession Session { get; } = new();
        public bool IsReady => _page != null && !_page.IsClosed;

        public KokoBrowserOperatorService()
        {
            _screenshotsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kokonoe", "BrowserScreenshots");
            Directory.CreateDirectory(_screenshotsDir);
        }

        private async Task EnsureInitializedAsync(CancellationToken ct = default)
        {
            if (_initialized && IsReady) return;
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_initialized && IsReady) return;

                var headless = AppSettings.Load().BrowserHeadless;
                _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = headless,
                    SlowMo = 80,
                    Args = new[] { "--start-maximized" }
                }).ConfigureAwait(false);

                _page = await _browser.NewPageAsync(new BrowserNewPageOptions
                {
                    ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
                }).ConfigureAwait(false);

                _page.FrameNavigated += async (_, frame) =>
                {
                    if (frame != _page.MainFrame) return;
                    Session.CurrentUrl = _page.Url;
                    try { Session.PageTitle = await _page.TitleAsync().ConfigureAwait(false); }
                    catch (Exception ex) { KokoSystemLog.Write("BROWSER-CATCH", "TitleAsync failed in FrameNavigated: " + ex.Message); }
                    PushStatus("navigated");
                };

                _initialized = true;
                KokoSystemLog.Write("BROWSER", $"Chromium launched (headed={!headless})");
                PushStatus("ready");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<BrowserAction> NavigateAsync(string url, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            Session.Status = BrowserSessionStatus.Navigating;
            PushStatus("navigating", url);

            var action = new BrowserAction { Kind = "navigate", Target = url };
            try
            {
                await _page!.GotoAsync(url, new PageGotoOptions
                {
                    Timeout = 30_000,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                }).ConfigureAwait(false);
                Session.CurrentUrl = _page.Url;
                Session.PageTitle = await _page.TitleAsync().ConfigureAwait(false);
                action.Result = $"Navigated to: {Session.PageTitle}";
                action.Success = true;
            }
            catch (Exception ex)
            {
                action.Success = false;
                action.Result = $"Navigation failed: {ex.Message}";
                Session.LastError = ex.Message;
            }

            Session.Status = BrowserSessionStatus.Idle;
            Session.History.Add(action);
            Session.LastActionAt = DateTime.UtcNow;
            PushStatus("idle");
            return action;
        }

        public async Task<BrowserAction> ClickAsync(string selector, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            Session.Status = BrowserSessionStatus.Acting;
            PushStatus("clicking", selector);

            var action = new BrowserAction { Kind = "click", Target = selector };
            try
            {
                await _page!.ClickAsync(selector, new PageClickOptions { Timeout = 8_000 }).ConfigureAwait(false);
                action.Success = true;
                action.Result = $"Clicked: {selector}";
            }
            catch (Exception ex)
            {
                try
                {
                    // Parameterized — never string-interpolate a selector into
                    // JS text. The selector can originate from page content the
                    // model just read, so treat it as untrusted input.
                    await _page!.EvaluateAsync("(sel) => document.querySelector(sel)?.click()", selector).ConfigureAwait(false);
                    action.Success = true;
                    action.Result = $"Clicked via JS: {selector}";
                }
                catch
                {
                    action.Success = false;
                    action.Result = $"Click failed: {ex.Message}";
                    Session.LastError = ex.Message;
                }
            }

            Session.Status = BrowserSessionStatus.Idle;
            Session.History.Add(action);
            PushStatus("idle");
            return action;
        }

        public async Task<BrowserAction> TypeAsync(string selector, string text, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            var action = new BrowserAction { Kind = "type", Target = selector, Value = text };
            try
            {
                await _page!.FillAsync(selector, text, new PageFillOptions { Timeout = 8_000 }).ConfigureAwait(false);
                action.Success = true;
                action.Result = $"Typed in {selector}";
            }
            catch (Exception ex)
            {
                action.Success = false;
                action.Result = $"Type failed: {ex.Message}";
            }
            Session.History.Add(action);
            return action;
        }

        public async Task<BrowserAction> ExtractAsync(string? selector = null, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            Session.Status = BrowserSessionStatus.Extracting;
            PushStatus("extracting");

            var action = new BrowserAction { Kind = "extract", Target = selector ?? "body" };
            try
            {
                string content;
                if (string.IsNullOrWhiteSpace(selector))
                {
                    content = await _page!.EvaluateAsync<string>("document.body.innerText").ConfigureAwait(false) ?? "";
                    if (content.Length > 8000)
                        content = content[..8000] + "\n...[truncated]";
                }
                else
                {
                    var elements = await _page!.QuerySelectorAllAsync(selector).ConfigureAwait(false);
                    var texts = await Task.WhenAll(elements.Select(e => e.InnerTextAsync())).ConfigureAwait(false);
                    content = string.Join("\n", texts);
                }
                action.Result = content;
                action.Success = true;
            }
            catch (Exception ex)
            {
                action.Success = false;
                action.Result = $"Extract failed: {ex.Message}";
            }

            Session.Status = BrowserSessionStatus.Idle;
            Session.History.Add(action);
            PushStatus("idle");
            return action;
        }

        public async Task<BrowserAction> ScreenshotAsync(CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            var action = new BrowserAction { Kind = "screenshot", Target = Session.CurrentUrl };
            try
            {
                var path = Path.Combine(_screenshotsDir, $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                await _page!.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = path,
                    FullPage = false
                }).ConfigureAwait(false);
                action.Result = path;
                action.Success = true;
                Session.LastScreenshotPath = path;

                try { ScreenshotTaken?.Invoke(path, Session.CurrentUrl); }
                catch (Exception subscriberEx) { KokoSystemLog.Write("BROWSER-CATCH", "ScreenshotTaken subscriber failed: " + subscriberEx.Message); }
            }
            catch (Exception ex)
            {
                action.Success = false;
                action.Result = $"Screenshot failed: {ex.Message}";
            }
            Session.History.Add(action);
            return action;
        }

        public async Task<BrowserAction> ScrollAsync(string direction = "down", int pixels = 500, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            var dy = direction == "up" ? -pixels : pixels;
            await _page!.EvaluateAsync("(d) => window.scrollBy(0, d)", dy).ConfigureAwait(false);
            var action = new BrowserAction
            {
                Kind = "scroll",
                Target = direction,
                Value = pixels.ToString(),
                Success = true,
                Result = $"Scrolled {direction} {pixels}px"
            };
            Session.History.Add(action);
            return action;
        }

        public async Task<BrowserAction> WaitForAsync(string selector, int timeoutMs = 10_000, CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            var action = new BrowserAction { Kind = "wait_for", Target = selector };
            try
            {
                await _page!.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = timeoutMs }).ConfigureAwait(false);
                action.Success = true;
                action.Result = $"Element appeared: {selector}";
            }
            catch (Exception ex)
            {
                action.Success = false;
                action.Result = $"Timeout waiting for {selector}: {ex.Message}";
            }
            Session.History.Add(action);
            return action;
        }

        public async Task CloseAsync()
        {
            if (_browser != null)
                await _browser.CloseAsync().ConfigureAwait(false);
            _playwright?.Dispose();
            _initialized = false;
            _page = null;
            Session.Status = BrowserSessionStatus.Closed;
            PushStatus("closed");
        }

        private void PushStatus(string status, string? detail = null)
        {
            try { StatusChanged?.Invoke(status, detail); }
            catch (Exception ex) { KokoSystemLog.Write("BROWSER-CATCH", "StatusChanged subscriber failed: " + ex.Message); }
        }

        public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
    }
}
