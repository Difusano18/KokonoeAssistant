# Kokonoe Wear Bridge

Minimal Wear OS bridge scaffold for Galaxy Watch.

## Desktop endpoint

KokonoeAssistant starts a local bridge:

- `GET  http://<pc-ip>:8787/api/wearable/v1/status`
- `GET  http://<pc-ip>:8787/api/wearable/v1/schema`
- `POST http://<pc-ip>:8787/api/wearable/v1/sample`

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

## Payload v1

```json
{
  "timestampUtc": "2026-06-01T20:40:00Z",
  "deviceId": "galaxy-watch-8-lte",
  "source": "wear-os-bridge",
  "heartRateBpm": 72,
  "ibiMs": null,
  "hrvRmssdMs": null,
  "spO2Percent": null,
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

## Backend plan

Phase 1 uses Wear OS Health Services for heart rate and local sensors for motion.
Phase 2 can add Samsung Health Sensor SDK for Galaxy Watch-specific data such as IBI, SpO2, ECG, PPG, and skin temperature.

