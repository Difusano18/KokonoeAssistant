using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════
    // TUNNEL MANAGER
    // Авто-запуск cloudflared quick tunnel, парсинг публічного URL.
    // ══════════════════════════════════════════════════════════════════
    //
    // Очікує cloudflared.exe в одному з місць (по порядку):
    //   1. <baseDir>/cloudflared.exe
    //   2. <baseDir>/tools/cloudflared.exe
    //   3. PATH
    //
    // Якщо не знайдено — Start() повертає false, PublicUrl лишається null.
    //
    // Користувач має сам один раз скачати:
    //   https://github.com/cloudflare/cloudflared/releases/latest
    //   (cloudflared-windows-amd64.exe → перейменувати в cloudflared.exe)

    public class TunnelManager
    {
        private const string DOWNLOAD_URL =
            "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";

        private Process? _proc;
        private readonly int _port;
        private string? _exePath;

        public string? PublicUrl { get; private set; }
        public bool IsRunning => _proc != null && !_proc.HasExited;

        public event Action<string>? UrlChanged;
        public event Action<string>? Log;

        public TunnelManager(int port)
        {
            _port = port;
            _exePath = ResolveExe();
        }

        private static string? ResolveExe()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "cloudflared.exe"),
                Path.Combine(baseDir, "tools", "cloudflared.exe"),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            // PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var p = Path.Combine(dir.Trim(), "cloudflared.exe");
                    if (File.Exists(p)) return p;
                }
                catch { }
            }
            return null;
        }

        public async Task<bool> StartAsync()
        {
            if (IsRunning) return true;
            if (_exePath == null)
            {
                Log?.Invoke("[Tunnel] cloudflared.exe не знайдено — качаю з GitHub...");
                _exePath = await DownloadCloudflaredAsync();
                if (_exePath == null)
                {
                    Log?.Invoke("[Tunnel] завантаження провалилося, тунель недоступний");
                    return false;
                }
                Log?.Invoke($"[Tunnel] скачано в {_exePath}");
            }
            return StartInternal();
        }

        public bool Start() => StartAsync().GetAwaiter().GetResult();

        private bool StartInternal()
        {
            if (_exePath == null) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName  = _exePath,
                    Arguments = $"tunnel --no-autoupdate --url http://localhost:{_port}",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _proc.OutputDataReceived += (_, e) => HandleLine(e.Data);
                _proc.ErrorDataReceived  += (_, e) => HandleLine(e.Data);
                _proc.Exited += (_, _) => Log?.Invoke($"[Tunnel] cloudflared exited (code {_proc?.ExitCode})");

                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
                Log?.Invoke($"[Tunnel] cloudflared started (pid {_proc.Id})");
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[Tunnel] start failed: {ex.Message}");
                return false;
            }
        }

        private async Task<string?> DownloadCloudflaredAsync()
        {
            var dest = Path.Combine(AppContext.BaseDirectory, "cloudflared.exe");
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("KokonoeAssistant/1.0");
                using var resp = await http.GetAsync(DOWNLOAD_URL, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                var tmp = dest + ".part";
                await using (var fs = File.Create(tmp))
                    await resp.Content.CopyToAsync(fs);
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
                return dest;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[Tunnel] download failed: {ex.Message}");
                return null;
            }
        }

        public void Stop()
        {
            try
            {
                if (_proc != null && !_proc.HasExited) _proc.Kill(entireProcessTree: true);
            }
            catch { }
            _proc = null;
        }

        private static readonly Regex _urlRx = new(
            @"https://[a-z0-9-]+\.trycloudflare\.com",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private void HandleLine(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Debug.WriteLine($"[cloudflared] {line}");
            var m = _urlRx.Match(line);
            if (m.Success && PublicUrl != m.Value)
            {
                PublicUrl = m.Value;
                Log?.Invoke($"[Tunnel] Public URL: {PublicUrl}");
                UrlChanged?.Invoke(PublicUrl);
            }
        }

        // Чекає поки URL зʼявиться (з таймаутом)
        public async Task<string?> WaitForUrlAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
            {
                if (PublicUrl != null) return PublicUrl;
                await Task.Delay(250, ct);
            }
            return PublicUrl;
        }
    }
}
