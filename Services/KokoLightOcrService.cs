using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoLightOcrService
    {
        public async Task<KokoOcrResult> TryReadAsync(byte[] screenshot, CancellationToken ct = default)
        {
            if (screenshot == null || screenshot.Length == 0)
                return new KokoOcrResult(false, "", "empty_screenshot");

            var tesseract = FindExecutable("tesseract.exe") ?? FindExecutable("tesseract");
            if (string.IsNullOrWhiteSpace(tesseract))
                return new KokoOcrResult(false, "", "tesseract_not_installed");

            var tempDir = Path.Combine(Path.GetTempPath(), "kokonoe-ocr");
            Directory.CreateDirectory(tempDir);
            var id = Guid.NewGuid().ToString("N");
            var input = Path.Combine(tempDir, id + ".jpg");
            var outputBase = Path.Combine(tempDir, id);
            var outputTxt = outputBase + ".txt";

            try
            {
                await File.WriteAllBytesAsync(input, screenshot, ct).ConfigureAwait(false);
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = tesseract,
                        Arguments = $"\"{input}\" \"{outputBase}\" --psm 6",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                proc.Start();
                var completed = await WaitForExitAsync(proc, TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
                if (!completed)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    return new KokoOcrResult(false, "", "ocr_timeout");
                }

                var text = File.Exists(outputTxt)
                    ? await File.ReadAllTextAsync(outputTxt, Encoding.UTF8, ct).ConfigureAwait(false)
                    : "";
                return string.IsNullOrWhiteSpace(text)
                    ? new KokoOcrResult(false, "", "ocr_empty")
                    : new KokoOcrResult(true, Normalize(text), "");
            }
            catch (Exception ex)
            {
                return new KokoOcrResult(false, "", ex.Message);
            }
            finally
            {
                TryDelete(input);
                TryDelete(outputTxt);
            }
        }

        public bool LooksInteresting(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var lower = text.ToLowerInvariant();
            return lower.Contains("error") ||
                   lower.Contains("exception") ||
                   lower.Contains("failed") ||
                   lower.Contains("build failed") ||
                   lower.Contains("warning") ||
                   lower.Contains("crash") ||
                   lower.Contains("denied") ||
                   lower.Contains("payment") ||
                   lower.Contains("bank") ||
                   lower.Contains("login") ||
                   lower.Contains("password") ||
                   lower.Contains("nullreference") ||
                   lower.Contains("sockettimeout");
        }

        public bool LooksLikeQuickFix(string text, out string reason)
        {
            reason = "";
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var lower = text.ToLowerInvariant();
            if (lower.Contains("build failed") || lower.Contains("compilation failed") || lower.Contains("cs20"))
            {
                reason = "build_failed";
                return true;
            }
            if (lower.Contains("exception") || lower.Contains("stack trace") || lower.Contains("nullreference"))
            {
                reason = "runtime_exception";
                return true;
            }
            if (lower.Contains("sockettimeout") || lower.Contains("connection refused") || lower.Contains("access is denied"))
            {
                reason = "connectivity_or_permission";
                return true;
            }
            return false;
        }

        private static async Task<bool> WaitForExitAsync(Process proc, TimeSpan timeout, CancellationToken ct)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var delay = Task.Delay(timeout, timeoutCts.Token);
                var exit = proc.WaitForExitAsync(ct);
                var winner = await Task.WhenAny(exit, delay).ConfigureAwait(false);
                if (winner == exit)
                {
                    timeoutCts.Cancel();
                    await exit.ConfigureAwait(false);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string Normalize(string text)
            => string.Join(" ", text.Replace("\r", " ").Replace("\n", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static string? FindExecutable(string name)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), name);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch { }
            }
            return null;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    public readonly record struct KokoOcrResult(bool Ok, string Text, string Error);
}
