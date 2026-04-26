using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════
    // MINI APP SERVER
    // Локальний HTTP сервер для Telegram Mini App.
    // Виставляється назовні через ngrok/cloudflared.
    // ══════════════════════════════════════════════════════════════════
    //
    // Endpoints:
    //   GET  /              -> webapp/index.html
    //   GET  /<file>        -> webapp/<file> (статика)
    //   GET  /api/state     -> поточний стан Kokonoe (mood/bond/conn)
    //   POST /api/chat      -> відправити повідомлення (return reply)
    //   POST /api/os/<cmd>  -> OS-команди (lock/sleep/volume/...)
    //
    // Security:
    //   Кожен /api/* запит має містити header X-Tg-InitData з підписаним
    //   initData від Telegram. HMAC валідуємо по bot token.
    //   Дозволяємо тільки одного юзера (TelegramChatId з settings).
    //

    public class MiniAppServer
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private readonly string _webRoot;
        private readonly string _botToken;
        private readonly long _allowedUserId;
        private readonly int _port;

        public bool IsRunning => _listener?.IsListening == true;
        public int  Port      => _port;

        public MiniAppServer(int port, string botToken, long allowedUserId)
        {
            _port = port;
            _botToken = botToken;
            _allowedUserId = allowedUserId;
            _webRoot = Path.Combine(AppContext.BaseDirectory, "webapp");
        }

        public void Start()
        {
            if (_listener != null) return;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_port}/");
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            try { _listener.Start(); }
            catch (HttpListenerException ex)
            {
                // 5 = Access denied — треба netsh urlacl, fallback на localhost only
                Debug.WriteLine($"[MiniApp] Failed to bind on +:{_port} ({ex.Message}), trying localhost only");
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
            }
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => AcceptLoop(_cts.Token));
            Debug.WriteLine($"[MiniApp] Listening on port {_port}, webRoot={_webRoot}");
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { break; }
                _ = Task.Run(() => HandleSafe(ctx));
            }
        }

        private async Task HandleSafe(HttpListenerContext ctx)
        {
            try { await Handle(ctx); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniApp] handler crash: {ex}");
                try
                {
                    ctx.Response.StatusCode = 500;
                    await WriteJson(ctx, new { error = "internal" });
                }
                catch { }
            }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        private async Task Handle(HttpListenerContext ctx)
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-Tg-InitData");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            if (ctx.Request.HttpMethod == "OPTIONS") { ctx.Response.StatusCode = 204; return; }

            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                if (!ValidateAuth(ctx, out var reason))
                {
                    ctx.Response.StatusCode = 401;
                    await WriteJson(ctx, new { error = "auth", reason });
                    return;
                }
                await HandleApi(ctx, path);
                return;
            }

            await ServeStatic(ctx, path);
        }

        // ── AUTH (TG initData HMAC) ───────────────────────────────────

        private bool ValidateAuth(HttpListenerContext ctx, out string reason)
        {
            reason = "";
            var initData = ctx.Request.Headers["X-Tg-InitData"];
            if (string.IsNullOrEmpty(initData)) { reason = "no-initdata"; return false; }

            if (!VerifyTelegramInitData(initData, _botToken, out var fields))
            {
                reason = "bad-hmac";
                return false;
            }

            // Дозволяємо тільки нашого юзера
            if (fields.TryGetValue("user", out var userJson))
            {
                try
                {
                    var u = JsonConvert.DeserializeObject<Dictionary<string, object>>(userJson);
                    if (u != null && u.TryGetValue("id", out var idObj))
                    {
                        var idStr = idObj?.ToString() ?? "";
                        if (long.TryParse(idStr, out var uid) && uid == _allowedUserId) return true;
                        reason = $"forbidden-user:{idStr}";
                        return false;
                    }
                }
                catch { }
            }
            reason = "no-user";
            return false;
        }

        // Telegram Mini App initData verification:
        // https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app
        private static bool VerifyTelegramInitData(string initData, string botToken,
            out Dictionary<string, string> fields)
        {
            fields = new Dictionary<string, string>();
            var pairs = HttpUtility.ParseQueryString(initData);
            string? hash = null;
            var parts = new List<string>();
            foreach (string? key in pairs.AllKeys)
            {
                if (key == null) continue;
                var val = pairs[key] ?? "";
                fields[key] = val;
                if (key == "hash") { hash = val; continue; }
                parts.Add($"{key}={val}");
            }
            if (hash == null) return false;

            parts.Sort(StringComparer.Ordinal);
            var dataCheckString = string.Join("\n", parts);

            using var hmacSecret = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
            var secretKey = hmacSecret.ComputeHash(Encoding.UTF8.GetBytes(botToken));

            using var hmac = new HMACSHA256(secretKey);
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
            var sigHex = Convert.ToHexString(sig).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(sigHex),
                Encoding.ASCII.GetBytes(hash.ToLowerInvariant()));
        }

        // ── API ───────────────────────────────────────────────────────

        private async Task HandleApi(HttpListenerContext ctx, string path)
        {
            switch (path.ToLowerInvariant())
            {
                case "/api/state":
                    await ApiState(ctx); return;
                case "/api/ping":
                    await WriteJson(ctx, new { ok = true, ts = DateTime.UtcNow }); return;
                default:
                    ctx.Response.StatusCode = 404;
                    await WriteJson(ctx, new { error = "not-found", path });
                    return;
            }
        }

        private async Task ApiState(HttpListenerContext ctx)
        {
            var emo   = ServiceContainer.EmotionEngine;
            var brain = ServiceContainer.BrainEngine;
            var sched = brain?.Scheduler;
            var resp = new
            {
                ok      = true,
                emotion = emo?.Current.ToString(),
                bond    = emo?.Bond.ToString(),
                conn    = emo?.ConnectionScore,
                mood    = brain?.State?.PersonalityDailyMood,
                score   = brain?.State?.MoodScore,
                monolog = brain?.State?.InnerMonologues?.LastOrDefault(),
                pending = sched?.GetAll().Count,
                ts      = DateTime.Now,
            };
            await WriteJson(ctx, resp);
        }

        // ── STATIC ────────────────────────────────────────────────────

        private async Task ServeStatic(HttpListenerContext ctx, string path)
        {
            var rel = path.TrimStart('/');
            if (string.IsNullOrEmpty(rel)) rel = "index.html";

            // Path traversal protection
            var fullPath = Path.GetFullPath(Path.Combine(_webRoot, rel));
            if (!fullPath.StartsWith(_webRoot, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 403;
                return;
            }

            if (!File.Exists(fullPath))
            {
                ctx.Response.StatusCode = 404;
                var bytes = Encoding.UTF8.GetBytes($"not found: {rel}");
                await ctx.Response.OutputStream.WriteAsync(bytes);
                return;
            }

            ctx.Response.ContentType = GetMime(fullPath);
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            using var fs = File.OpenRead(fullPath);
            await fs.CopyToAsync(ctx.Response.OutputStream);
        }

        private static string GetMime(string path) =>
            Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js"   => "application/javascript; charset=utf-8",
                ".css"  => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".svg"  => "image/svg+xml",
                ".png"  => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".ico"  => "image/x-icon",
                _       => "application/octet-stream",
            };

        private static async Task WriteJson(HttpListenerContext ctx, object obj)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var json = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            await ctx.Response.OutputStream.WriteAsync(bytes);
        }
    }
}
