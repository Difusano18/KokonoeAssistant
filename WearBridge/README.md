# Kokonoe Wear Bridge

Minimal Wear OS bridge scaffold for Galaxy Watch.

## Desktop endpoint

KokonoeAssistant starts a local bridge:

- `GET  http://<pc-ip>:8787/api/wearable/v1/status`
- `GET  http://<pc-ip>:8787/api/wearable/v1/schema`
- `POST http://<pc-ip>:8787/api/wearable/v1/sample`
- `POST http://<pc-ip>:8787/api/wearable/v1/samples`
- `GET  http://<pc-ip>:8787/api/wearable/v1/command`
- `POST http://<pc-ip>:8787/api/wearable/v1/command/ack`
- `POST http://<pc-ip>:8787/api/wearable/v1/action`

The POST request must include:

```http
X-Koko-Bridge-Token: <token>
Content-Type: application/json
```

The token is stored on the PC at:

```text
<vault-or-app-data>/kokonoe-data/wearable-bridge-token.txt
```

If Windows blocks LAN binding for `http://+:8787/`, run PowerShell as Administrator:

```powershell
netsh http add urlacl url=http://+:8787/api/wearable/v1/ user=Everyone
```

Then restart KokonoeAssistant. Yes, Windows made this uglier than it needed to be.

## Notebook with two IP addresses

If the notebook moves between two predictable LAN addresses, add both URLs in
KokonoeAssistant settings under **Wear Bridge -> Fallback notebook URLs**:

```text
http://192.168.1.10:8787
http://192.168.0.10:8787
```

During pairing, the desktop returns those URLs to the watch. The watch then tries:

1. the last successful URL
2. the main configured URL
3. all known fallback URLs
4. local subnet discovery

If the watch and notebook are not on a reachable network path, use a VPN or a
real public/relay URL. NAT is not impressed by optimism.

## Diagnostics

The desktop bridge exposes runtime diagnostics in `GET /status`:

- `connection.state`: `LINKED`, `WAITING_FOR_WATCH`, `WAITING_FOR_PAIR`, `STOPPED`, or `ERROR`
- `connection.reason`: short explanation of the current state
- recent link flags for telemetry, authorized requests, sample uploads, command polls, and acks
- total status requests, samples, duplicate samples, batch uploads, pair requests, command polls
- command acknowledgements
- auth failure count
- last remote endpoint
- last paired device id
- last authorized request, sample/status/pair/command/ack timestamps
- last accepted and duplicate `sampleId`
- last queued and delivered desktop command

Desktop commands use a simple mailbox:

1. desktop queues a command
2. watch polls `/command`
3. watch executes the command locally
4. watch posts `/command/ack` with `commandId`, `action`, `ok`, and `detail`

Watch actions go the other way with `POST /action`. The watch can send compact
commands such as `look_screen_now`, `note_this`, and `im_stressed`; the desktop
routes them into the autonomous brain/vision loop and records the action in the
central system log.

The KokonoeAssistant settings window shows the same bridge diagnostics under
**Wear Bridge**. The large link state is intentionally stricter than "server is
running": it reports `LINKED` only when recent telemetry, an authorized request,
or command polling proves that the watch is actually talking to this PC. If it
says `WAITING FOR PAIR` or `WAITING FOR WATCH`, the server may be fine while the
watch is still missing the route, token, or pairing state. Use **Copy
Diagnostics** to copy the current bridge, connection, URL, command, and wearable
state as JSON.

## Payload v1

```json
{
  "timestampUtc": "2026-06-01T20:40:00Z",
  "sampleId": "galaxy-watch-8-lte-1780346400000",
  "deviceId": "galaxy-watch-8-lte",
  "source": "wear-os-bridge",
  "heartRateBpm": 72,
  "ibiMs": null,
  "hrvRmssdMs": null,
  "spO2Percent": null,
  "skinTemperatureC": null,
  "ppgSignalQuality": null,
  "ecgAvailable": null,
  "latitude": null,
  "longitude": null,
  "locationAccuracyM": null,
  "semanticLocation": "home",
  "motion": 0.12,
  "onWrist": true,
  "activity": "resting",
  "batteryPercent": 84,
  "charging": false,
  "note": ""
}
```

Queued offline samples are flushed with `POST /samples` as a JSON array:

```json
[
  { "sampleId": "galaxy-watch-8-lte-1780346400000", "timestampUtc": "2026-06-01T20:40:00Z", "deviceId": "galaxy-watch-8-lte", "heartRateBpm": 72 },
  { "sampleId": "galaxy-watch-8-lte-1780346410000", "timestampUtc": "2026-06-01T20:40:10Z", "deviceId": "galaxy-watch-8-lte", "heartRateBpm": 73 }
]
```

`sampleId` is optional for legacy senders, but WearBridge sends it so retry and
batch replays do not duplicate telemetry.

## Backend plan

Phase 1 uses Wear OS Health Services for heart rate and local sensors for motion.
Phase 2 can add Samsung Health Sensor SDK for Galaxy Watch-specific data such as IBI, SpO2, ECG, PPG, and skin temperature.
