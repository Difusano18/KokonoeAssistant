using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoHyperAutomationService : IDisposable
    {
        private readonly PcControlService _pc;
        private readonly KokoLightOcrService _ocr;
        private readonly KokoInternalBlackboardService _blackboard;
        private readonly KokoServiceHeartbeatService _heartbeat;
        private readonly Func<KokoBrainEngine?> _brainFactory;
        private readonly System.Threading.Timer _timer;
        private readonly object _lock = new();
        private string _lastWindowKey = "";
        private DateTime _lastMiniScanAt = DateTime.MinValue;
        private DateTime _lastQuickFixAt = DateTime.MinValue;
        private int _inFlight;

        public KokoHyperAutomationService(
            PcControlService pc,
            KokoLightOcrService ocr,
            KokoInternalBlackboardService blackboard,
            KokoServiceHeartbeatService heartbeat,
            Func<KokoBrainEngine?> brainFactory)
        {
            _pc = pc;
            _ocr = ocr;
            _blackboard = blackboard;
            _heartbeat = heartbeat;
            _brainFactory = brainFactory;
            _timer = new System.Threading.Timer(_ => _ = TickAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            _heartbeat.Update("VISION_MINI", "watching", "foreground watcher armed");
        }

        private async Task TickAsync()
        {
            if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
                return;

            try
            {
                var fg = _pc.GetForegroundWindow();
                var key = $"{fg.ProcessName}|{fg.Title}";
                if (string.IsNullOrWhiteSpace(key) || key == "|")
                    return;

                var changed = false;
                lock (_lock)
                {
                    changed = !string.Equals(_lastWindowKey, key, StringComparison.Ordinal);
                    if (changed)
                        _lastWindowKey = key;
                }

                if (!changed)
                    return;

                var signal = ClassifySignal(fg.ProcessName, fg.Title);
                _blackboard.Publish("screen-agent", "foreground_changed", $"{fg.ProcessName}: {Trim(fg.Title, 120)}", signal.Priority);
                _heartbeat.Update("SCREEN_AGENT", "foreground", $"{signal.Kind}: {Trim(fg.Title, 80)}");

                if (!signal.ShouldMiniScan)
                    return;

                var now = DateTime.Now;
                if (now - _lastMiniScanAt < TimeSpan.FromSeconds(20))
                    return;
                _lastMiniScanAt = now;

                await MiniScanAsync(fg, signal.Kind).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _heartbeat.Update("VISION_MINI", "error", ex.Message);
                KokoSystemLog.Write("VISION_MINI", "tick failed: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _inFlight, 0);
            }
        }

        private async Task MiniScanAsync(ForegroundWindowInfo fg, string signalKind)
        {
            _heartbeat.Update("VISION_MINI", "scanning", $"{signalKind}: {Trim(fg.Title, 70)}");
            KokoSystemLog.Write("VISION_MINI", $"High-priority mini-scan: {signalKind}; {fg.ProcessName}; {Trim(fg.Title, 120)}");

            byte[] screenshot;
            try
            {
                screenshot = await Task.Run(() => _pc.TakeScreenshot()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _heartbeat.Update("VISION_MINI", "capture_error", ex.Message);
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await _ocr.TryReadAsync(screenshot, cts.Token).ConfigureAwait(false);
            if (!result.Ok)
            {
                _blackboard.Publish("screen-agent", "mini_scan_no_ocr", $"{signalKind}: OCR unavailable ({result.Error}); using foreground metadata", 0.45);
                _heartbeat.Update("VISION_MINI", "metadata_only", result.Error);
                MaybeForceVision(signalKind, fg, "metadata_only");
                return;
            }

            var text = Trim(result.Text, 500);
            _blackboard.Publish("screen-agent", "ocr", text, _ocr.LooksInteresting(text) ? 0.85 : 0.55);
            _heartbeat.Update("VISION_MINI", "ocr_ok", Trim(text, 90));

            if (_ocr.LooksLikeQuickFix(text, out var reason))
                OfferQuickFix(reason, text, fg);
            else if (_ocr.LooksInteresting(text))
                MaybeForceVision(signalKind, fg, "interesting_ocr");
        }

        private void OfferQuickFix(string reason, string text, ForegroundWindowInfo fg)
        {
            var now = DateTime.Now;
            if (now - _lastQuickFixAt < TimeSpan.FromMinutes(6))
                return;
            _lastQuickFixAt = now;

            var summary = $"quick_fix_offer:{reason}; window={fg.ProcessName}; text={Trim(text, 140)}";
            _blackboard.Publish("screen-agent", "quick_fix", summary, 0.95);
            KokoSystemLog.Write("VISION_MINI", "Quick Fix detected: " + summary);

            var brain = _brainFactory();
            if (brain == null)
                return;

            _ = Task.Run(async () =>
            {
                await brain.ForceScreenAwarenessAsync(
                    $"quick_fix:{reason}; OCR={Trim(text, 180)}",
                    "Offer a concrete low-risk quick fix if the screen shows an error. Do not nag; be concise.");
            });
        }

        private void MaybeForceVision(string signalKind, ForegroundWindowInfo fg, string trigger)
        {
            if (signalKind == "banking")
            {
                KokoSystemLog.Write("VISION_MINI", "Sensitive high-signal window observed; full vision skipped for privacy");
                return;
            }
            if (signalKind is not ("ide" or "game"))
                return;

            var brain = _brainFactory();
            if (brain == null)
                return;

            _ = Task.Run(() => brain.ForceScreenAwarenessAsync(
                $"high_priority_window:{signalKind}; {trigger}; {fg.ProcessName}; {Trim(fg.Title, 120)}",
                signalKind == "game" ? "Short tactical observation only." : "Look for actionable errors or blocked work."));
        }

        private static (bool ShouldMiniScan, string Kind, double Priority) ClassifySignal(string process, string title)
        {
            var text = $"{process} {title}".ToLowerInvariant();
            if (ContainsAny(text, "devenv", "code", "visual studio", "rider", "jetbrains", "terminal", "powershell", "cmd", "exception", "build"))
                return (true, "ide", 0.9);
            if (ContainsAny(text, "steam", "unity", "unreal", "game", "valorant", "cs2", "dota", "minecraft"))
                return (true, "game", 0.8);
            if (ContainsAny(text, "bank", "privat", "monobank", "paypal", "stripe", "payment", "finance"))
                return (true, "banking", 0.95);
            if (ContainsAny(text, "telegram", "discord", "whatsapp"))
                return (false, "chat", 0.5);
            return (false, "ordinary", 0.3);
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            foreach (var term in terms)
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string Trim(string text, int max)
            => string.IsNullOrWhiteSpace(text) || text.Length <= max ? text ?? "" : text[..max].TrimEnd();

        public void Dispose() => _timer.Dispose();
    }
}
