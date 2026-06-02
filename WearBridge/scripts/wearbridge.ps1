param(
    [ValidateSet("all", "verify", "install", "open", "status", "logs", "devices")]
    [string]$Action = "all",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [int]$WaitSeconds = 90,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

function Require-Path([string]$Path, [string]$Name) {
    if (-not (Test-Path $Path)) {
        throw "$Name not found: $Path"
    }
}

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host "== $Text =="
}

$wearRoot = Join-Path $RepoRoot "WearBridge"
$sdkRoot = Join-Path $wearRoot ".android-sdk"
$adb = Join-Path $sdkRoot "platform-tools\adb.exe"
$apk = Join-Path $wearRoot "dist\KokonoeWearBridge-debug.apk"
$verifyScript = Join-Path $wearRoot "scripts\verify-release.ps1"
$packageName = "dev.kokonoe.wearbridge"
$mainActivity = "$packageName/.MainActivity"

Require-Path $wearRoot "WearBridge"
Require-Path $adb "adb"
Require-Path $verifyScript "verify-release.ps1"

function Get-AdbDevices {
    $raw = & $adb devices -l
    $raw | Where-Object { $_ -match "\bdevice\b" -and $_ -notmatch "^List of devices" }
}

function Wait-Device {
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    do {
        $devices = @(Get-AdbDevices)
        if ($devices.Count -gt 0) {
            return $devices[0]
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw @"
No Wear OS device found through adb.
Do this once:
1. Enable Developer options on the watch.
2. Enable ADB debugging and Wireless debugging.
3. Pair/connect it from Android Studio or adb.
4. Re-run: WearBridge\scripts\wearbridge.ps1 all
"@
}

function Invoke-Verify {
    if ($SkipTests) {
        Write-Step "Android APK only"
        Push-Location $wearRoot
        try {
            $gradle = Join-Path $env:USERPROFILE ".gradle\wrapper\dists\gradle-8.14.3-all\10utluxaxniiv4wxiphsi49nj\gradle-8.14.3\bin\gradle.bat"
            Require-Path $gradle "Gradle"
            $jdk = Get-ChildItem (Join-Path $wearRoot ".tools") -Directory |
                Where-Object { $_.Name -like "jdk-*" } |
                Select-Object -First 1
            if (-not $jdk) { throw "JDK not found under $wearRoot\.tools" }
            $env:JAVA_HOME = $jdk.FullName
            $env:ANDROID_SDK_ROOT = (Resolve-Path $sdkRoot).Path
            $env:ANDROID_HOME = $env:ANDROID_SDK_ROOT
            & $gradle :app:assembleDebug
            New-Item -ItemType Directory -Force (Join-Path $wearRoot "dist") | Out-Null
            Copy-Item -Force (Join-Path $wearRoot "app\build\outputs\apk\debug\app-debug.apk") $apk
        }
        finally {
            Pop-Location
        }
    } else {
        Write-Step "Release verification"
        & powershell -ExecutionPolicy Bypass -File $verifyScript -RepoRoot $RepoRoot
    }
}

function Invoke-Install {
    Require-Path $apk "APK"
    Write-Step "Waiting for Wear OS device"
    $device = Wait-Device
    Write-Host $device

    Write-Step "Installing APK"
    & $adb install -r $apk
    if ($LASTEXITCODE -ne 0) {
        throw "adb install failed"
    }
}

function Invoke-Open {
    Write-Step "Opening Kokonoe Bridge on watch"
    Wait-Device | Out-Null
    & $adb shell am start -n $mainActivity
    if ($LASTEXITCODE -ne 0) {
        throw "adb launch failed"
    }
}

function Invoke-Status {
    Write-Step "Device status"
    $devices = @(Get-AdbDevices)
    if ($devices.Count -eq 0) {
        Write-Host "No adb device connected."
        return
    }
    $devices | ForEach-Object { Write-Host $_ }
    & $adb shell dumpsys package $packageName | Select-String -Pattern "versionName|versionCode|firstInstallTime|lastUpdateTime" -Context 0,0
}

function Invoke-Logs {
    Write-Step "Recent app logs"
    Wait-Device | Out-Null
    & $adb logcat -d -t 250 | Select-String -Pattern "kokonoe|WearBridge|HealthServices|AndroidRuntime|FATAL EXCEPTION"
}

switch ($Action) {
    "devices" { & $adb devices -l }
    "verify" { Invoke-Verify }
    "install" { Invoke-Install }
    "open" { Invoke-Open }
    "status" { Invoke-Status }
    "logs" { Invoke-Logs }
    "all" {
        Invoke-Verify
        Invoke-Install
        Invoke-Open
        Invoke-Status
        Invoke-Logs
        Write-Step "Done"
        Write-Host "On the watch: tap Setup Once. After that it should reconnect automatically."
    }
}
