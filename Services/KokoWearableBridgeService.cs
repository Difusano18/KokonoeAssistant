using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWearableBridgeService : IDisposable
    {
        public const int DefaultPort = 8787;

        private readonly KokoWearableTelemetryService _telemetry;
        private readonly string _dataDir;
        private readonly string _tokenPath;
        private readonly string _pcIdPath;
        private readonly IReadOnlyList<string> _externalUrls;
        private readonly object _lock = new();
        private TcpListener? _listener;
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private Task? _udpTask;
        private readonly Queue<WearableCommandEnvelope> _pendingCommands = new();
        private long _totalStatusRequests;
        private long _totalSamples;
        private long _totalBatchRequests;
        private long _totalDuplicateSamples;
        private long _totalPairRequests;
        private long _totalCommandPolls;
        private long _totalCommandAcks;
        private long _totalAuthFailures;
        private DateTime? _lastQueuedCommandAtUtc;
        private DateTime? _lastDeliveredCommandAtUtc;
        private DateTime? _lastAuthorizedAtUtc;
        private DateTime? _lastStatusAtUtc;
        private DateTime? _lastSampleAtUtc;
        private DateTime? _lastPairAtUtc;
        private DateTime? _lastCommandPollAtUtc;
        private DateTime? _lastCommandAckAtUtc;
        private string _lastDeviceId = "";
        private string _lastPairedDeviceId = "";
        private string _lastAcceptedSampleId = "";
        private string _lastDuplicateSampleId = "";
        private string _lastQueuedCommandId = "";
        private string _lastQueuedCommandAction = "";
        private string _lastDeliveredCommandId = "";
        private string _lastDeliveredCommandAction = "";
        private string _lastRemoteEndpoint = "";
        private string _lastUserAgent = "";
        private string _lastAckCommandId = "";
        private string _lastAckAction = "";
        private bool _lastAckOk;
        private string _lastAckDetail = "";

        public KokoWearableBridgeService(
            KokoWearableTelemetryService telemetry,
            string dataDir,
            int port = DefaultPort,
            IEnumerable<string>? externalUrls = null)
        {
            _telemetry = telemetry;
            _dataDir = dataDir;
            Port = port;
            _externalUrls = NormalizeUrls(externalUrls ?? Array.Empty<string>());
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
        public IReadOnlyList<string> LanUrls => GetLanUrls();
        public IReadOnlyList<string> ExternalUrls => _externalUrls;
        public string LastError { get; private set; } = "";
        public int PendingCommandCount { get { lock (_lock) { PurgeCommandQueueLocked(DateTime.UtcNow); return _pendingCommands.Count; } } }
        public WearableBridgeDiagnostics Diagnostics => BuildDiagnostics();

        public WearableCommandEnvelope QueueCommand(string action, string payload = "")
        {
            var command = new WearableCommandEnvelope
            {
                CommandId = Guid.NewGuid().ToString("N"),
                Action = string.IsNullOrWhiteSpace(action) ? "noop" : action.Trim(),
                Payload = payload ?? "",
                CreatedAtUtc = DateTime.UtcNow,
                NotBeforeUtc = DateTime.UtcNow
            };
            lock (_lock)
            {
                _pendingCommands.Enqueue(command);
                while (_pendingCommands.Count > 20)
                    _pendingCommands.Dequeue();
                _lastQueuedCommandAtUtc = command.CreatedAtUtc;
                _lastQueuedCommandId = command.CommandId;
                _lastQueuedCommandAction = command.Action;
            }
            LogBridge($"command queued action={command.Action} id={command.CommandId}");
            return command;
        }

        public WearableBridgeConnectionSnapshot GetConnectionSnapshot(KokoWearableState? wearable = null, DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            var diagnostics = Diagnostics;
            wearable ??= _telemetry.State;

            var telemetryFresh = wearable.IsFresh(now);
            var authorizedRecent = IsRecent(diagnostics.LastAuthorizedAtUtc, now, TimeSpan.FromSeconds(90));
            var sampleRecent = IsRecent(diagnostics.LastSampleAtUtc, now, TimeSpan.FromMinutes(3));
            var commandPollRecent = IsRecent(diagnostics.LastCommandPollAtUtc, now, TimeSpan.FromSeconds(45));
            var ackRecent = IsRecent(diagnostics.LastCommandAckAtUtc, now, TimeSpan.FromMinutes(2));
            var paired = !string.IsNullOrWhiteSpace(diagnostics.LastPairedDeviceId);
            var lastSeen = LatestUtc(
                diagnostics.LastAuthorizedAtUtc,
                diagnostics.LastSampleAtUtc,
                diagnostics.LastCommandPollAtUtc,
                diagnostics.LastCommandAckAtUtc);
            var linked = diagnostics.IsRunning && (telemetryFresh || authorizedRecent || sampleRecent || commandPollRecent);

            var state = !diagnostics.IsRunning
                ? "STOPPED"
                : !string.IsNullOrWhiteSpace(diagnostics.LastError) && !linked
                    ? "ERROR"
                    : linked
                        ? "LINKED"
                        : paired
                            ? "WAITING_FOR_WATCH"
                            : "WAITING_FOR_PAIR";

            var reason = state switch
            {
                "STOPPED" => "desktop bridge is stopped",
                "ERROR" => diagnostics.LastError,
                "LINKED" when telemetryFresh => "fresh wearable telemetry",
                "LINKED" when authorizedRecent => "recent authorized watch request",
                "LINKED" when sampleRecent => "recent accepted sample",
                "LINKED" when commandPollRecent => "recent command poll",
                "WAITING_FOR_WATCH" => "paired, but no recent authorized watch traffic",
                _ => "no watch pairing seen yet"
            };

            return new WearableBridgeConnectionSnapshot
            {
                State = state,
                Reason = reason,
                IsLinked = linked,
                IsPaired = paired,
                TelemetryFresh = telemetryFresh,
                AuthorizedRecent = authorizedRecent,
                SampleRecent = sampleRecent,
                CommandPollRecent = commandPollRecent,
                AckRecent = ackRecent,
                LastSeenAtUtc = lastSeen,
                LastSeenAgeSeconds = lastSeen.HasValue ? Math.Max(0, (now - lastSeen.Value.ToUniversalTime()).TotalSeconds) : null
            };
        }

        public void Start()
        {
            lock (_lock)
            {
                if (IsRunning) return;

                _cts = new CancellationTokenSource();
                try
                {
                    _listener = new TcpListener(IPAddress.Any, Port);
                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _listener.Start();
                    IsRunning = true;
                    LastError = "";
                    _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
                    TryStartUdpDiscoveryLocked(_cts.Token);
                    LogBridge($"started tcp on 0.0.0.0:{Port}; udp discovery={(_udp != null ? "on" : "off")}");
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    LogBridge($"tcp bind failed: {ex.Message}");
                    IsRunning = false;
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                try { _cts?.Cancel(); } catch { }
                try { _listener?.Stop(); } catch { }
                try { _udp?.Close(); } catch { }
                _listener = null;
                _udp = null;
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
                _udpTask = null;
                IsRunning = false;
                LogBridge("stopped");
            }
        }

        public void Dispose() => Stop();

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                    _ = Task.Run(() => HandleTcpClientAsync(client, ct), ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }
                catch (SocketException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    LogBridge($"loop failed: {ex.Message}");
                }
            }
        }

        private void TryStartUdpDiscoveryLocked(CancellationToken ct)
        {
            try
            {
                _udp = new UdpClient(AddressFamily.InterNetwork);
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                _udpTask = Task.Run(() => RunUdpDiscoveryLoopAsync(ct), ct);
            }
            catch (Exception ex)
            {
                LogBridge($"udp bind failed: {ex.Message}");
                try { _udp?.Close(); } catch { }
                _udp = null;
            }
        }

        private async Task RunUdpDiscoveryLoopAsync(CancellationToken ct)
        {
            var probe = Encoding.UTF8.GetBytes("KOKONOE_WEARABLE_DISCOVER_V1");
            while (!ct.IsCancellationRequested && _udp != null)
            {
                try
                {
                    var packet = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
                    if (!packet.Buffer.AsSpan().SequenceEqual(probe)) continue;

                    var remotePrefix = Ipv4Prefix(packet.RemoteEndPoint.Address.ToString());
                    var url = GetLanUrls().FirstOrDefault(u => !string.IsNullOrWhiteSpace(remotePrefix) && u.Contains(remotePrefix, StringComparison.OrdinalIgnoreCase))
                        ?? GetLanUrls().FirstOrDefault()
                        ?? BaseUrl;
                    var payload = JsonConvert.SerializeObject(new
                    {
                        bridge = "kokonoe-wearable-v1",
                        pcId = PcId,
                        pcName = Environment.MachineName,
                        port = Port,
                        baseUrl = url,
                        urls = GetLanUrls().Concat(_externalUrls).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    });
                    var bytes = Encoding.UTF8.GetBytes(payload);
                    await _udp.SendAsync(bytes, bytes.Length, packet.RemoteEndPoint).ConfigureAwait(false);
                    LogBridge($"udp discovery answered {packet.RemoteEndPoint}");
                }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }
                catch (SocketException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    LogBridge($"udp discovery failed: {ex.Message}");
                }
            }
        }

        private static string Ipv4Prefix(string value)
        {
            var parts = value.Split('.');
            return parts.Length == 4 ? string.Join(".", parts.Take(3)) : "";
        }

        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var req = await ReadRequestAsync(stream, client, ct).ConfigureAwait(false);
                    if (req == null) return;
                    var response = await HandleAsync(req).ConfigureAwait(false);
                    await WriteJsonAsync(stream, response.Status, response.Payload).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    LogBridge($"tcp client failed: {ex.Message}");
                    try
                    {
                        await WriteJsonAsync(client.GetStream(), 500, new { ok = false, error = "bridge_loop_failed", detail = ex.Message }).ConfigureAwait(false);
                    }
                    catch { }
                }
            }
        }

        private async Task<BridgeHttpResponse> HandleAsync(BridgeHttpRequest req)
        {
            if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                return new BridgeHttpResponse(204, new { });
            }

            var path = req.Path.TrimEnd('/');
            try
            {
                if (req.HttpMethod == "GET" && path.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
                {
                    RecordStatus(req);
                    var wearable = _telemetry.State;
                    var diagnostics = BuildDiagnosticsPayload();
                    var connection = BuildConnectionPayload(GetConnectionSnapshot(wearable));
                    return new BridgeHttpResponse(200, new
                    {
                        ok = true,
                        bridge = "kokonoe-wearable-v1",
                        pcId = PcId,
                        pcName = Environment.MachineName,
                        version = "wearable-bridge-pairing-v2",
                        running = IsRunning,
                        port = Port,
                        urls = GetLanUrls().Concat(_externalUrls).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        externalUrls = _externalUrls,
                        tokenRequired = true,
                        pairingAvailable = true,
                        connection,
                        diagnostics,
                        wearable
                    });
                }

                if (req.HttpMethod == "GET" && path.EndsWith("/schema", StringComparison.OrdinalIgnoreCase))
                {
                    return new BridgeHttpResponse(200, BuildSchema());
                }

                if (req.HttpMethod == "GET" && path.EndsWith("/command", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(req))
                    {
                        RecordAuthFailure(req);
                        return new BridgeHttpResponse(401, new { ok = false, error = "missing_or_invalid_bridge_token" });
                    }

                    RecordAuthorized(req);
                    RecordCommandPoll(req);
                    WearableCommandEnvelope? command = null;
                    lock (_lock)
                    {
                        var now = DateTime.UtcNow;
                        PurgeCommandQueueLocked(now);
                        command = _pendingCommands.FirstOrDefault(c => c.NotBeforeUtc <= now);
                        if (command != null)
                        {
                            command.DeliveryAttempts++;
                            command.LastDeliveredAtUtc = now;
                            command.NotBeforeUtc = now + CommandRetryDelay(command.DeliveryAttempts);
                        }
                    }
                    if (command != null)
                        RecordCommandDelivered(req, command);

                    return new BridgeHttpResponse(200, new
                    {
                        ok = true,
                        commandId = command?.CommandId ?? "",
                        action = command?.Action ?? "",
                        payload = command?.Payload ?? "",
                        pending = PendingCommandCount
                    });
                }

                if (req.HttpMethod == "POST" && path.EndsWith("/command", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(req))
                    {
                        RecordAuthFailure(req);
                        return new BridgeHttpResponse(401, new { ok = false, error = "missing_or_invalid_bridge_token" });
                    }

                    RecordAuthorized(req);
                    var body = req.Body;
                    var commandRequest = string.IsNullOrWhiteSpace(body)
                        ? new WearableCommandRequest()
                        : JsonConvert.DeserializeObject<WearableCommandRequest>(body) ?? new WearableCommandRequest();
                    var queued = QueueCommand(commandRequest.Action, commandRequest.Payload);
                    return new BridgeHttpResponse(200, new { ok = true, queued });
                }

                if (req.HttpMethod == "POST" && path.EndsWith("/command/ack", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(req))
                    {
                        RecordAuthFailure(req);
                        return new BridgeHttpResponse(401, new { ok = false, error = "missing_or_invalid_bridge_token" });
                    }

                    RecordAuthorized(req);
                    var body = req.Body;
                    var ack = string.IsNullOrWhiteSpace(body)
                        ? new WearableCommandAckRequest()
                        : JsonConvert.DeserializeObject<WearableCommandAckRequest>(body) ?? new WearableCommandAckRequest();
                    RecordCommandAck(req, ack);
                    RemoveCommandLocked(ack.CommandId);
                    return new BridgeHttpResponse(200, new { ok = true, diagnostics = BuildDiagnosticsPayload() });
                }

                if (req.HttpMethod == "POST" && path.EndsWith("/pair", StringComparison.OrdinalIgnoreCase))
                {
                    var body = req.Body;
                    var request = string.IsNullOrWhiteSpace(body)
                        ? new WearablePairRequest()
                        : JsonConvert.DeserializeObject<WearablePairRequest>(body) ?? new WearablePairRequest();
                    RecordPair(req, request.DeviceId);

                    return new BridgeHttpResponse(200, new
                    {
                        ok = true,
                        bridge = "kokonoe-wearable-v1",
                        pcId = PcId,
                        pcName = Environment.MachineName,
                        version = "wearable-bridge-pairing-v2",
                        urls = GetLanUrls().Concat(_externalUrls).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        externalUrls = _externalUrls,
                        diagnostics = BuildDiagnosticsPayload(),
                        token = Token,
                        pairedDeviceId = string.IsNullOrWhiteSpace(request.DeviceId) ? "unknown" : request.DeviceId.Trim(),
                        message = "paired"
                    });
                }

                if (req.HttpMethod == "POST" && path.EndsWith("/sample", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(req))
                    {
                        RecordAuthFailure(req);
                        return new BridgeHttpResponse(401, new { ok = false, error = "missing_or_invalid_bridge_token" });
                    }

                    RecordAuthorized(req);
                    var body = req.Body;
                    var sample = JsonConvert.DeserializeObject<KokoWearableSample>(body) ?? new KokoWearableSample();
                    var ingest = _telemetry.IngestDetailed(sample);
                    if (ingest.Accepted) RecordSample(req, sample);
                    if (ingest.Duplicate) RecordDuplicateSample(req, sample);
                    return new BridgeHttpResponse(200, new { ok = true, accepted = ingest.Accepted, duplicate = ingest.Duplicate, state = ingest.State });
                }

                if (req.HttpMethod == "POST" && path.EndsWith("/samples", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(req))
                    {
                        RecordAuthFailure(req);
                        return new BridgeHttpResponse(401, new { ok = false, error = "missing_or_invalid_bridge_token" });
                    }

                    RecordAuthorized(req);
                    var body = req.Body;
                    var samples = ParseSampleBatch(body);
                    if (samples.Count == 0)
                    {
                        return new BridgeHttpResponse(400, new { ok = false, error = "empty_sample_batch" });
                    }

                    KokoWearableState? state = null;
                    var accepted = 0;
                    foreach (var sample in samples.Take(256))
                    {
                        var ingest = _telemetry.IngestDetailed(sample);
                        state = ingest.State;
                        if (ingest.Accepted)
                        {
                            RecordSample(req, sample);
                            accepted++;
                        }
                        if (ingest.Duplicate)
                            RecordDuplicateSample(req, sample);
                    }
                    RecordBatch(req);
                    return new BridgeHttpResponse(200, new { ok = true, count = accepted, state });
                }

                return new BridgeHttpResponse(404, new { ok = false, error = "unknown_wearable_endpoint" });
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return new BridgeHttpResponse(500, new { ok = false, error = "wearable_bridge_failed", detail = ex.Message });
            }
        }

        private void RecordStatus(BridgeHttpRequest req)
        {
            lock (_lock)
            {
                _totalStatusRequests++;
                _lastStatusAtUtc = DateTime.UtcNow;
                RecordRemoteLocked(req);
            }
        }

        private void RecordPair(BridgeHttpRequest req, string deviceId)
        {
            lock (_lock)
            {
                _totalPairRequests++;
                _lastPairAtUtc = DateTime.UtcNow;
                _lastPairedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "unknown" : deviceId.Trim();
                RecordRemoteLocked(req);
            }
            LogBridge($"pair device={deviceId} remote={req.RemoteEndpoint}");
        }

        private void RecordAuthorized(BridgeHttpRequest req)
        {
            lock (_lock)
            {
                _lastAuthorizedAtUtc = DateTime.UtcNow;
                RecordRemoteLocked(req);
            }
        }

        private void RecordCommandPoll(BridgeHttpRequest req)
        {
            lock (_lock)
            {
                _totalCommandPolls++;
                _lastCommandPollAtUtc = DateTime.UtcNow;
                RecordRemoteLocked(req);
            }
        }

        private void RecordCommandDelivered(BridgeHttpRequest req, WearableCommandEnvelope command)
        {
            lock (_lock)
            {
                _lastDeliveredCommandAtUtc = DateTime.UtcNow;
                _lastDeliveredCommandId = command.CommandId;
                _lastDeliveredCommandAction = command.Action;
                RecordRemoteLocked(req);
            }
            LogBridge($"command delivered action={command.Action} id={command.CommandId} attempts={command.DeliveryAttempts}");
        }

        private void RecordCommandAck(BridgeHttpRequest req, WearableCommandAckRequest ack)
        {
            lock (_lock)
            {
                _totalCommandAcks++;
                _lastCommandAckAtUtc = DateTime.UtcNow;
                _lastAckCommandId = (ack.CommandId ?? "").Trim();
                _lastAckAction = (ack.Action ?? "").Trim();
                _lastAckOk = ack.Ok;
                _lastAckDetail = (ack.Detail ?? "").Trim();
                RecordRemoteLocked(req);
            }
            LogBridge($"command ack action={ack.Action} id={ack.CommandId} ok={ack.Ok}");
        }

        private static TimeSpan CommandRetryDelay(int attempts)
        {
            var seconds = attempts switch
            {
                <= 1 => 5,
                2 => 12,
                3 => 30,
                4 => 75,
                _ => 180
            };
            return TimeSpan.FromSeconds(seconds);
        }

        private void PurgeCommandQueueLocked(DateTime nowUtc)
        {
            if (_pendingCommands.Count == 0) return;
            var keep = _pendingCommands
                .Where(c => nowUtc - c.CreatedAtUtc <= TimeSpan.FromMinutes(30))
                .TakeLast(20)
                .ToList();
            _pendingCommands.Clear();
            foreach (var item in keep)
                _pendingCommands.Enqueue(item);
        }

        private void RemoveCommandLocked(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId)) return;
            lock (_lock)
            {
                if (_pendingCommands.Count == 0) return;
                var keep = _pendingCommands
                    .Where(c => !string.Equals(c.CommandId, commandId.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _pendingCommands.Clear();
                foreach (var item in keep)
                    _pendingCommands.Enqueue(item);
            }
        }

        private void RecordSample(BridgeHttpRequest req, KokoWearableSample sample)
        {
            lock (_lock)
            {
                _totalSamples++;
                _lastSampleAtUtc = DateTime.UtcNow;
                _lastDeviceId = string.IsNullOrWhiteSpace(sample.DeviceId) ? "unknown" : sample.DeviceId.Trim();
                _lastAcceptedSampleId = (sample.SampleId ?? "").Trim();
                RecordRemoteLocked(req);
            }
            LogBridge($"sample accepted device={sample.DeviceId} bpm={sample.HeartRateBpm:0.#}");
        }

        private void RecordBatch(BridgeHttpRequest req)
        {
            lock (_lock)
            {
                _totalBatchRequests++;
                RecordRemoteLocked(req);
            }
        }

        private void RecordDuplicateSample(BridgeHttpRequest req, KokoWearableSample sample)
        {
            lock (_lock)
            {
                _totalDuplicateSamples++;
                _lastDuplicateSampleId = (sample.SampleId ?? "").Trim();
                RecordRemoteLocked(req);
            }
        }

        private void RecordAuthFailure(BridgeHttpRequest req)
        {
            lock (_lock)
            {
                _totalAuthFailures++;
                RecordRemoteLocked(req);
            }
            LogBridge($"auth failed remote={req.RemoteEndpoint}");
        }

        private void LogBridge(string message)
        {
            _telemetry.WriteLog($"[WEARABLE-BRIDGE] {message}");
        }

        private void RecordRemoteLocked(BridgeHttpRequest req)
        {
            _lastRemoteEndpoint = req.RemoteEndpoint;
            _lastUserAgent = req.UserAgent;
        }

        private WearableBridgeDiagnostics BuildDiagnostics()
        {
            lock (_lock)
            {
                PurgeCommandQueueLocked(DateTime.UtcNow);
                return new WearableBridgeDiagnostics
                {
                    IsRunning = IsRunning,
                    Port = Port,
                    PendingCommands = _pendingCommands.Count,
                    TotalStatusRequests = _totalStatusRequests,
                    TotalSamples = _totalSamples,
                    TotalBatchRequests = _totalBatchRequests,
                    TotalDuplicateSamples = _totalDuplicateSamples,
                    TotalPairRequests = _totalPairRequests,
                    TotalCommandPolls = _totalCommandPolls,
                    TotalCommandAcks = _totalCommandAcks,
                    TotalAuthFailures = _totalAuthFailures,
                    LastQueuedCommandAtUtc = _lastQueuedCommandAtUtc,
                    LastDeliveredCommandAtUtc = _lastDeliveredCommandAtUtc,
                    LastAuthorizedAtUtc = _lastAuthorizedAtUtc,
                    LastStatusAtUtc = _lastStatusAtUtc,
                    LastSampleAtUtc = _lastSampleAtUtc,
                    LastPairAtUtc = _lastPairAtUtc,
                    LastCommandPollAtUtc = _lastCommandPollAtUtc,
                    LastCommandAckAtUtc = _lastCommandAckAtUtc,
                    LastDeviceId = _lastDeviceId,
                    LastPairedDeviceId = _lastPairedDeviceId,
                    LastAcceptedSampleId = _lastAcceptedSampleId,
                    LastDuplicateSampleId = _lastDuplicateSampleId,
                    LastQueuedCommandId = _lastQueuedCommandId,
                    LastQueuedCommandAction = _lastQueuedCommandAction,
                    LastDeliveredCommandId = _lastDeliveredCommandId,
                    LastDeliveredCommandAction = _lastDeliveredCommandAction,
                    LastRemoteEndpoint = _lastRemoteEndpoint,
                    LastUserAgent = _lastUserAgent,
                    LastAckCommandId = _lastAckCommandId,
                    LastAckAction = _lastAckAction,
                    LastAckOk = _lastAckOk,
                    LastAckDetail = _lastAckDetail,
                    LastError = LastError
                };
            }
        }

        private object BuildDiagnosticsPayload()
        {
            var d = BuildDiagnostics();
            return new
            {
                isRunning = d.IsRunning,
                port = d.Port,
                pendingCommands = d.PendingCommands,
                totalStatusRequests = d.TotalStatusRequests,
                totalSamples = d.TotalSamples,
                totalBatchRequests = d.TotalBatchRequests,
                totalDuplicateSamples = d.TotalDuplicateSamples,
                totalPairRequests = d.TotalPairRequests,
                totalCommandPolls = d.TotalCommandPolls,
                totalCommandAcks = d.TotalCommandAcks,
                totalAuthFailures = d.TotalAuthFailures,
                lastQueuedCommandAtUtc = d.LastQueuedCommandAtUtc,
                lastDeliveredCommandAtUtc = d.LastDeliveredCommandAtUtc,
                lastAuthorizedAtUtc = d.LastAuthorizedAtUtc,
                lastStatusAtUtc = d.LastStatusAtUtc,
                lastSampleAtUtc = d.LastSampleAtUtc,
                lastPairAtUtc = d.LastPairAtUtc,
                lastCommandPollAtUtc = d.LastCommandPollAtUtc,
                lastCommandAckAtUtc = d.LastCommandAckAtUtc,
                lastDeviceId = d.LastDeviceId,
                lastPairedDeviceId = d.LastPairedDeviceId,
                lastAcceptedSampleId = d.LastAcceptedSampleId,
                lastDuplicateSampleId = d.LastDuplicateSampleId,
                lastQueuedCommandId = d.LastQueuedCommandId,
                lastQueuedCommandAction = d.LastQueuedCommandAction,
                lastDeliveredCommandId = d.LastDeliveredCommandId,
                lastDeliveredCommandAction = d.LastDeliveredCommandAction,
                lastRemoteEndpoint = d.LastRemoteEndpoint,
                lastUserAgent = d.LastUserAgent,
                lastAckCommandId = d.LastAckCommandId,
                lastAckAction = d.LastAckAction,
                lastAckOk = d.LastAckOk,
                lastAckDetail = d.LastAckDetail,
                lastError = d.LastError
            };
        }

        private static object BuildConnectionPayload(WearableBridgeConnectionSnapshot c)
            => new
            {
                state = c.State,
                reason = c.Reason,
                isLinked = c.IsLinked,
                isPaired = c.IsPaired,
                telemetryFresh = c.TelemetryFresh,
                authorizedRecent = c.AuthorizedRecent,
                sampleRecent = c.SampleRecent,
                commandPollRecent = c.CommandPollRecent,
                ackRecent = c.AckRecent,
                lastSeenAtUtc = c.LastSeenAtUtc,
                lastSeenAgeSeconds = c.LastSeenAgeSeconds
            };

        private static bool IsRecent(DateTime? value, DateTime nowUtc, TimeSpan maxAge)
            => value.HasValue && nowUtc - value.Value.ToUniversalTime() <= maxAge;

        private static DateTime? LatestUtc(params DateTime?[] values)
        {
            DateTime? latest = null;
            foreach (var value in values)
            {
                if (!value.HasValue) continue;
                var utc = value.Value.ToUniversalTime();
                if (!latest.HasValue || utc > latest.Value)
                    latest = utc;
            }
            return latest;
        }

        private bool IsAuthorized(BridgeHttpRequest req)
        {
            var header = req.Headers.TryGetValue("X-Koko-Bridge-Token", out var value) ? value : "";
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
                "sampleId", "timestampUtc", "deviceId", "source", "heartRateBpm", "ibiMs", "hrvRmssdMs",
                "spO2Percent", "skinTemperatureC", "ppgSignalQuality", "ecgAvailable",
                "latitude", "longitude", "locationAccuracyM", "semanticLocation",
                "motion", "onWrist", "activity", "batteryPercent", "charging", "note"
            }
        };

        private IReadOnlyList<KokoWearableSample> ParseSampleBatch(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return Array.Empty<KokoWearableSample>();
            try
            {
                var token = JToken.Parse(body);
                var array = token.Type == JTokenType.Array
                    ? (JArray)token
                    : token["samples"] as JArray;
                if (array == null) return Array.Empty<KokoWearableSample>();

                return array
                    .Select(item => item.ToObject<KokoWearableSample>())
                    .Where(sample => sample != null)
                    .Cast<KokoWearableSample>()
                    .ToList();
            }
            catch
            {
                return Array.Empty<KokoWearableSample>();
            }
        }

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

        private IReadOnlyList<string> NormalizeUrls(IEnumerable<string> urls)
        {
            return urls
                .SelectMany(u => (u ?? "").Split(',', ';', '\n', '\r'))
                .Select(u => u.Trim().TrimEnd('/'))
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? u
                    : $"http://{u}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }

        private async Task<BridgeHttpRequest?> ReadRequestAsync(NetworkStream stream, TcpClient client, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[4096];
            var headerEnd = -1;
            while (headerEnd < 0 && ms.Length < 64 * 1024)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                if (read <= 0) return null;
                ms.Write(buffer, 0, read);
                headerEnd = FindHeaderEnd(ms.GetBuffer(), (int)ms.Length);
            }
            if (headerEnd < 0) return null;

            var raw = ms.ToArray();
            var headerText = Encoding.ASCII.GetString(raw, 0, headerEnd);
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;
            var first = lines[0].Split(' ');
            if (first.Length < 2) return null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }

            var contentLength = headers.TryGetValue("Content-Length", out var lenText) && int.TryParse(lenText, out var len)
                ? Math.Clamp(len, 0, 1024 * 1024)
                : 0;
            var bodyStart = headerEnd + 4;
            var bodyBytes = raw.Skip(bodyStart).ToArray();
            while (bodyBytes.Length < contentLength)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, contentLength - bodyBytes.Length)), ct).ConfigureAwait(false);
                if (read <= 0) break;
                bodyBytes = bodyBytes.Concat(buffer.Take(read)).ToArray();
            }

            var path = first[1];
            var q = path.IndexOf('?');
            if (q >= 0) path = path[..q];
            return new BridgeHttpRequest
            {
                HttpMethod = first[0].ToUpperInvariant(),
                Path = path,
                Body = contentLength > 0 ? Encoding.UTF8.GetString(bodyBytes, 0, Math.Min(contentLength, bodyBytes.Length)) : "",
                Headers = headers,
                RemoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "",
                UserAgent = headers.TryGetValue("User-Agent", out var ua) ? ua : ""
            };
        }

        private static int FindHeaderEnd(byte[] bytes, int length)
        {
            for (var i = 3; i < length; i++)
            {
                if (bytes[i - 3] == '\r' && bytes[i - 2] == '\n' && bytes[i - 1] == '\r' && bytes[i] == '\n')
                    return i - 3;
            }
            return -1;
        }

        private async Task WriteJsonAsync(Stream stream, int status, object payload)
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);
            var statusText = status switch
            {
                200 => "OK",
                204 => "No Content",
                400 => "Bad Request",
                401 => "Unauthorized",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => "OK"
            };
            var header =
                $"HTTP/1.1 {status} {statusText}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {bytes.Length}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Headers: Content-Type, X-Koko-Bridge-Token\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            if (bytes.Length > 0)
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
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

        public sealed class WearableCommandEnvelope
        {
            public string CommandId { get; set; } = "";
            public string Action { get; set; } = "";
            public string Payload { get; set; } = "";
            public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
            public DateTime NotBeforeUtc { get; set; } = DateTime.UtcNow;
            public DateTime? LastDeliveredAtUtc { get; set; }
            public int DeliveryAttempts { get; set; }
        }

        private sealed class WearableCommandRequest
        {
            public string Action { get; set; } = "";
            public string Payload { get; set; } = "";
        }

        private sealed class WearableCommandAckRequest
        {
            public string CommandId { get; set; } = "";
            public string Action { get; set; } = "";
            public bool Ok { get; set; }
            public string Detail { get; set; } = "";
        }

        private sealed class BridgeHttpRequest
        {
            public string HttpMethod { get; set; } = "";
            public string Path { get; set; } = "";
            public string Body { get; set; } = "";
            public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string RemoteEndpoint { get; set; } = "";
            public string UserAgent { get; set; } = "";
        }

        private readonly record struct BridgeHttpResponse(int Status, object Payload);

        public sealed class WearableBridgeDiagnostics
        {
            public bool IsRunning { get; set; }
            public int Port { get; set; }
            public int PendingCommands { get; set; }
            public long TotalStatusRequests { get; set; }
            public long TotalSamples { get; set; }
            public long TotalBatchRequests { get; set; }
            public long TotalDuplicateSamples { get; set; }
            public long TotalPairRequests { get; set; }
            public long TotalCommandPolls { get; set; }
            public long TotalCommandAcks { get; set; }
            public long TotalAuthFailures { get; set; }
            public DateTime? LastQueuedCommandAtUtc { get; set; }
            public DateTime? LastDeliveredCommandAtUtc { get; set; }
            public DateTime? LastAuthorizedAtUtc { get; set; }
            public DateTime? LastStatusAtUtc { get; set; }
            public DateTime? LastSampleAtUtc { get; set; }
            public DateTime? LastPairAtUtc { get; set; }
            public DateTime? LastCommandPollAtUtc { get; set; }
            public DateTime? LastCommandAckAtUtc { get; set; }
            public string LastDeviceId { get; set; } = "";
            public string LastPairedDeviceId { get; set; } = "";
            public string LastAcceptedSampleId { get; set; } = "";
            public string LastDuplicateSampleId { get; set; } = "";
            public string LastQueuedCommandId { get; set; } = "";
            public string LastQueuedCommandAction { get; set; } = "";
            public string LastDeliveredCommandId { get; set; } = "";
            public string LastDeliveredCommandAction { get; set; } = "";
            public string LastRemoteEndpoint { get; set; } = "";
            public string LastUserAgent { get; set; } = "";
            public string LastAckCommandId { get; set; } = "";
            public string LastAckAction { get; set; } = "";
            public bool LastAckOk { get; set; }
            public string LastAckDetail { get; set; } = "";
            public string LastError { get; set; } = "";
        }

        public sealed class WearableBridgeConnectionSnapshot
        {
            public string State { get; set; } = "STOPPED";
            public string Reason { get; set; } = "";
            public bool IsLinked { get; set; }
            public bool IsPaired { get; set; }
            public bool TelemetryFresh { get; set; }
            public bool AuthorizedRecent { get; set; }
            public bool SampleRecent { get; set; }
            public bool CommandPollRecent { get; set; }
            public bool AckRecent { get; set; }
            public DateTime? LastSeenAtUtc { get; set; }
            public double? LastSeenAgeSeconds { get; set; }
        }
    }
}
