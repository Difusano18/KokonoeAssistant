using System;
using System.Threading;
using System.Threading.Tasks;
using static KokonoeAssistant.Services.KokoBrowserToolHandlerSupport;

namespace KokonoeAssistant.Services
{
    public sealed class KokoBrowserNavigateToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserNavigateToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.navigate";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var url = Arg(call, "url");
            if (string.IsNullOrWhiteSpace(url))
                return Failure(call, "url is required.");
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            var action = await _browser.NavigateAsync(url, ct).ConfigureAwait(false);
            return ToResult(call, Name, action);
        }
    }

    public sealed class KokoBrowserClickToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserClickToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.click";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var selector = Arg(call, "selector");
            if (string.IsNullOrWhiteSpace(selector))
                return Failure(call, "selector is required.");
            var action = await _browser.ClickAsync(selector, ct).ConfigureAwait(false);
            return ToResult(call, Name, action);
        }
    }

    public sealed class KokoBrowserTypeToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserTypeToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.type";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var selector = Arg(call, "selector");
            if (string.IsNullOrWhiteSpace(selector))
                return Failure(call, "selector is required.");
            var text = Arg(call, "text");
            var action = await _browser.TypeAsync(selector, text, ct).ConfigureAwait(false);
            return ToResult(call, Name, action);
        }
    }

    public sealed class KokoBrowserExtractToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserExtractToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.extract";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var selector = call.Arguments.TryGetValue("selector", out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
            var action = await _browser.ExtractAsync(selector, ct).ConfigureAwait(false);
            return ToResult(call, Name, action);
        }
    }

    public sealed class KokoBrowserScreenshotToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserScreenshotToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.screenshot";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var action = await _browser.ScreenshotAsync(ct).ConfigureAwait(false);
            return ToResult(call, Name, action);
        }
    }

    public sealed class KokoBrowserScrollToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserScrollToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.scroll";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var direction = Arg(call, "direction");
            if (string.IsNullOrWhiteSpace(direction))
                direction = "down";
            var pixels = int.TryParse(Arg(call, "pixels"), out var parsedPixels) ? parsedPixels : 500;
            var action = await _browser.ScrollAsync(direction, pixels, ct).ConfigureAwait(false);
            return ToResult(call, Name, action);
        }
    }

    public sealed class KokoBrowserWaitForToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserWaitForToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.wait_for";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var selector = Arg(call, "selector");
            if (string.IsNullOrWhiteSpace(selector))
                return Failure(call, "selector is required.");
            var timeoutMs = int.TryParse(Arg(call, "timeoutMs"), out var parsedTimeout) ? parsedTimeout : 10_000;
            var action = await _browser.WaitForAsync(selector, timeoutMs, ct).ConfigureAwait(false);
            return ToResult(call, Name, action);
        }
    }

    public sealed class KokoBrowserCloseToolHandler : IKokoToolHandler
    {
        private readonly KokoBrowserOperatorService _browser;
        public KokoBrowserCloseToolHandler(KokoBrowserOperatorService browser) => _browser = browser;
        public string Name => "browser.close";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            return new KokoToolResult
            {
                CallId = call.Id,
                ToolName = Name,
                Success = true,
                Verified = true,
                Reason = "Browser closed.",
                Output = "Browser closed."
            };
        }
    }

    internal static class KokoBrowserToolHandlerSupport
    {
        public static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";

        public static KokoToolResult ToResult(KokoToolCall call, string toolName, KokonoeAssistant.Models.BrowserAction action) => new()
        {
            CallId = call.Id,
            ToolName = toolName,
            Success = action.Success,
            Verified = action.Success,
            Reason = action.Success ? "ok" : (action.Result ?? "failed"),
            Output = action.Result ?? ""
        };

        public static KokoToolResult Failure(KokoToolCall call, string reason) => new()
        {
            CallId = call.Id,
            ToolName = call.Name,
            Success = false,
            Reason = reason
        };
    }
}
