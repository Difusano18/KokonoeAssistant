param(
    [string]$BaseUrl = "http://127.0.0.1:8787",
    [string]$ExePath = "",
    [int]$TimeoutSeconds = 5,
    [switch]$StartExe
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host "== $Text =="
}

function Invoke-JsonPost([string]$Uri, [object]$Body, [hashtable]$Headers = @{}) {
    $json = $Body | ConvertTo-Json -Compress -Depth 8
    Invoke-RestMethod -Method Post -Uri $Uri -Headers $Headers -ContentType "application/json" -Body $json -TimeoutSec $TimeoutSeconds
}

function Ensure-Bridge {
    try {
        return Invoke-RestMethod -Uri "$BaseUrl/api/wearable/v1/status" -TimeoutSec $TimeoutSeconds
    }
    catch {
        if (-not $StartExe) { throw }
        $path = if ($ExePath) { $ExePath } else { Join-Path (Resolve-Path ".").Path "bin\Debug\net8.0-windows\KokonoeAssistant.exe" }
        if (-not (Test-Path $path)) { throw "KokonoeAssistant.exe not found: $path" }
        Start-Process -FilePath $path -WorkingDirectory (Split-Path $path) -WindowStyle Hidden
        Start-Sleep -Seconds 4
        return Invoke-RestMethod -Uri "$BaseUrl/api/wearable/v1/status" -TimeoutSec $TimeoutSeconds
    }
}

Write-Step "Bridge status"
$status = Ensure-Bridge
if ($status.bridge -ne "kokonoe-wearable-v1") {
    throw "Unexpected bridge marker: $($status.bridge)"
}
Write-Host "bridge=$($status.bridge) pcId=$($status.pcId) port=$($status.port)"

Write-Step "Pair emulated watch client"
$pair = Invoke-JsonPost "$BaseUrl/api/wearable/v1/pair" @{ deviceId = "live-smoke-watch-a"; appVersion = "live-smoke" }
if (-not $pair.ok -or [string]::IsNullOrWhiteSpace($pair.token)) {
    throw "Pair failed"
}
$headers = @{ "X-Koko-Bridge-Token" = $pair.token }

Write-Step "Push dual pulse trains"
$sent = 0
$devices = @(
    @{ id = "live-smoke-watch-a"; bpms = @(72, 75, 88, 101, 109) },
    @{ id = "live-smoke-watch-b"; bpms = @(69, 73, 84, 95, 103) }
)

foreach ($device in $devices) {
    $index = 0
    foreach ($bpm in $device.bpms) {
        $sample = @{
            sampleId = "$($device.id)-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())-$index"
            timestampUtc = ([DateTime]::UtcNow.ToString("o"))
            deviceId = $device.id
            source = "live-smoke-watch-client"
            heartRateBpm = $bpm
            motion = 0.20 + ($index * 0.04)
            onWrist = $true
            activity = "heart_realtime:live_smoke"
            semanticLocation = "live-smoke"
            batteryPercent = 90 - $index
        }
        $result = Invoke-JsonPost "$BaseUrl/api/wearable/v1/sample" $sample $headers
        if (-not $result.ok -or -not $result.accepted) {
            throw "Sample rejected for $($device.id) bpm=$bpm"
        }
        $sent++
        $index++
    }
}

Write-Step "Verify status and logs"
$after = Invoke-RestMethod -Uri "$BaseUrl/api/wearable/v1/status" -TimeoutSec $TimeoutSeconds
if ($after.connection.state -ne "LINKED") {
    throw "Expected LINKED connection, got $($after.connection.state)"
}
if (($after.diagnostics.totalSamples -as [long]) -lt $sent) {
    throw "Bridge sample counter too low: $($after.diagnostics.totalSamples), sent $sent"
}
if (($after.wearable.currentBpm -as [double]) -le 0) {
    throw "Wearable currentBpm was not updated"
}

Write-Host "sent=$sent"
Write-Host "connection=$($after.connection.state)"
Write-Host "currentBpm=$($after.wearable.currentBpm)"
Write-Host "device=$($after.wearable.deviceId)"
Write-Host "liveStressScore=$($after.wearable.liveStressScore)"
Write-Host "totalSamples=$($after.diagnostics.totalSamples)"
Write-Host ""
Write-Host "PASS wearable bridge live smoke"
