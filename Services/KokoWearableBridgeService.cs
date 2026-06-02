using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWearableBridgeService : IDisposable
    {
        public const int DefaultPort = 8787;

        private readonly KokoWearableTelemetryService _telemetry;
        private readonly string _dataDir;
        private readonly string _tokenPath;
        private readonly string _pcIdPath;
        private readonly object _lock = new();
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        public KokoWearableBridgeService(KokoWearableTelemetryService telemetry, string dataDir, int port = DefaultPort)
        {
            _telemetry = telemetry;
            _dataDir = dataDir;
            Port = port;
            Directory.CreateDirectory(_dataDir);
            _tokenPath = Path.Combine(_dataDir, "wearable-bridge-token.txt");
            _pcIdPath = Path.Combine(_dataDir, "wearable-bridge-pc-id.txt");
            Token = LoadOrCreateToken();
            PcId = LoadOrCreatePcId();
        }

        public int Port { get; }
        public string Token { get; }
        public string PcId { get; }
        public bool IsRunning { get; private set; }
        public string BaseUrl => $"http://localhost:{Port}";
        public string LastError { get; private set; } = "";

        public void Start()
        {
            lock (_lock)
            {
                if (IsRunning) return;

                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{Port}/api/wearable/v1/");

                try
                {
                    _listener.Start();
                    IsRunning = true;
                    LastError = "";
                    _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    Debug.WriteLine($"[WearableBridge] public bind failed: {ex.Message}");
                    TryStartLocalhostFallback();
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                try { _cts?.Cancel(); } catch { }
                try { _listener?.Stop(); } catch { }
                try { _listener?.Close(); } catch { }
                _listener = null;
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
                IsRunning = false;
            }
        }

        public void Dispose() => Stop();

        private void TryStartLocalhostFallback()
        {
            try
            {
                _listener?.Close();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/api/wearable/v1/");
                _listener.Start();
                IsRunning = true;
                _loopTask = Task.Run(() => RunLoopAsync(_cts?.Token ?? CancellationToken.None));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.WriteLine($"[WearableBridge] localhost bind failed: {ex.Message}");
                IsRunning = false;
            }
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener?.IsListening == true)
            {
                HttpListenerContext? ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleAsync(ctx), ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    Debug.WriteLine($"[WearableBridge] loop failed: {ex.Message}");
                    if (ctx != null) await WriteJsonAsync(ctx.Response, 500, new { ok = false, error = "bridge_loop_failed" }).ConfigureAwait(false);
                }
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Koko-Bridge-Token";
            res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";

            if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(res, 204, new { }).ConfigureAwait(false);
                return;
            }

            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";
            try
            {
                if (req.HttpMethod == "GET" && path.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonAsync(res, 200, new
                    {
                        ok = true,
                        bridge = "kokonoe-wearable-v1",
                        pcId = PcId,
                        pcName = Environment.MachineName,
                        version = "wearable-bridge-pairing-v2",
                        running = IsRunning,
                        port = Port,
                        urls = GetLanUrls(),
                        tokenRequired = true,
                        pairingAvailable = true,
                        wearable = _telemetry.State
                    }).ConfigureAwait(false);
                    return;
                }

                if (req.HttpMethod == "GET" && path.EndsWith("/schema", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonAsync(res, 200, BuildSchema()).ConfigureAwait(false);
                    return;
                }

                if (req.HttpMethod == "POST" && path.EndsWith("/pair", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                    var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var request = string.IsNullOrWhiteSpace(body)
                        ? new WearablePairRequest()
                        : JsonConvert.DeserializeObject<WearablePairRequest>(body) ?? new WearablePairRequest();

                    await WriteJsonAsync(res, 200, new
                    {
                        ok = true,
                        bridge = "kokonoe-wearable-v1",
                        pcId = PcId,
                        pcName = Environment.MachineName,
                        version = "wearable-bridge-pairing-v2",
                        urls = GetLanUrls(),
                        token = Token,
                        pairedDeviceId = string.IsNullOrWhiteSpace(request.DeviceId) ? "unknown" : request.DeviceId.Trim(),
                        message = "paired"
                    }).ConfigureAwait(false);
                    return;
                }

                if (req.HttpMethod == "POST" && path.EndsWith("/sample", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(req))
                    {
                        await WriteJsonAsync(res, 401, new { ok = false, error = "missing_or_invalid_bridge_token" }).ConfigureAwait(false);
                        return;
                    }

                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                    var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var sample = JsonConvert.DeserializeObject<KokoWearableSample>(body) ?? new KokoWearableSample();
                    var state = _telemetry.Ingest(sample);
                    await WriteJsonAsync(res, 200, new { ok = true, state }).ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(res, 404, new { ok = false, error = "unknown_wearable_endpoint" }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                await WriteJsonAsync(res, 500, new { ok = false, error = "wearable_bridge_failed", detail = ex.Message }).ConfigureAwait(false);
            }
        }

        private bool IsAuthorized(HttpListenerRequest req)
        {
            var header = req.Headers["X-Koko-Bridge-Token"] ?? "";
            if (string.IsNullOrWhiteSpace(header)) return false;
            var provided = Encoding.UTF8.GetBytes(header.Trim());
            var expected = Encoding.UTF8.GetBytes(Token);
            if (provided.Length != expected.Length) return false;
            return CryptographicOperations.FixedTimeEquals(provided, expected);
        }

        private object BuildSchema() => new
        {
            endpoint = "/api/wearable/v1/sample",
            method = "POST",
            header = "X-Koko-Bridge-Token",
            fields = new[]
            {
                "timestampUtc", "deviceId", "source", "heartRateBpm", "ibiMs", "hrvRmssdMs",
                "spO2Percent", "latitude", "longitude", "locationAccuracyM", "semanticLocation",
                "motion", "onWrist", "activity", "batteryPercent", "charging", "note"
            }
        };

        private IReadOnlyList<string> GetLanUrls()
        {
            var urls = new List<string> { BaseUrl };
            try
            {
                foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    var ip = address.ToString();
                    if (ip.StartsWith("127.") || ip.StartsWith("169.254.")) continue;
                    urls.Add($"http://{ip}:{Port}");
                }
            }
            catch { }
            return urls;
        }

        private async Task WriteJsonAsync(HttpListenerResponse res, int status, object payload)
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);
            res.StatusCode = status;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            await res.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            try { res.OutputStream.Close(); } catch { }
        }

        private string LoadOrCreateToken()
        {
            try
            {
                if (File.Exists(_tokenPath))
                {
                    var existing = File.ReadAllText(_tokenPath).Trim();
                    if (existing.Length >= 24) return existing;
                }

                Span<byte> bytes = stackalloc byte[24];
                RandomNumberGenerator.Fill(bytes);
                var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                File.WriteAllText(_tokenPath, token);
                return token;
            }
            catch
            {
                return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            }
        }

        private string LoadOrCreatePcId()
        {
            try
            {
                if (File.Exists(_pcIdPath))
                {
                    var existing = File.ReadAllText(_pcIdPath).Trim();
                    if (existing.Length >= 12) return existing;
                }

                var raw = $"{Environment.MachineName}-{Guid.NewGuid():N}";
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var pcId = "pc-" + Convert.ToHexString(hash).ToLowerInvariant()[..16];
                File.WriteAllText(_pcIdPath, pcId);
                return pcId;
            }
            catch
            {
                return "pc-" + Guid.NewGuid().ToString("N")[..16];
            }
        }

        private sealed class WearablePairRequest
        {
            public string DeviceId { get; set; } = "";
            public string AppVersion { get; set; } = "";
        }
    }
}
