param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

function Require-Path([string]$Path, [string]$Name) {
    if (-not (Test-Path $Path)) {
        throw "$Name not found: $Path"
    }
}

$wearRoot = Join-Path $RepoRoot "WearBridge"
$sdkRoot = Join-Path $wearRoot ".android-sdk"
$buildTools = Join-Path $sdkRoot "build-tools\35.0.0"
$apksigner = Join-Path $buildTools "apksigner.bat"
$distDir = Join-Path $wearRoot "dist"
$finalApk = Join-Path $distDir "KokonoeWearBridge-debug.apk"
$gradle = Join-Path $env:USERPROFILE ".gradle\wrapper\dists\gradle-8.14.3-all\10utluxaxniiv4wxiphsi49nj\gradle-8.14.3\bin\gradle.bat"

Require-Path $wearRoot "WearBridge"
Require-Path $sdkRoot "Android SDK"
Require-Path $apksigner "apksigner"
Require-Path $gradle "Gradle"

$jdk = Get-ChildItem (Join-Path $wearRoot ".tools") -Directory |
    Where-Object { $_.Name -like "jdk-*" } |
    Select-Object -First 1
if (-not $jdk) {
    throw "JDK not found under $wearRoot\.tools"
}

$env:JAVA_HOME = $jdk.FullName
$env:ANDROID_SDK_ROOT = (Resolve-Path $sdkRoot).Path
$env:ANDROID_HOME = $env:ANDROID_SDK_ROOT

Write-Host "== Desktop tests =="
Push-Location $RepoRoot
try {
    dotnet run --project Tests\KokonoeAssistant.Tests\KokonoeAssistant.Tests.csproj -p:OutputPath=bin\CodexTest\
}
finally {
    Pop-Location
}

Write-Host "== Android assemble =="
Push-Location $wearRoot
try {
    & $gradle :app:assembleDebug
    New-Item -ItemType Directory -Force $distDir | Out-Null
    Copy-Item -Force (Join-Path $wearRoot "app\build\outputs\apk\debug\app-debug.apk") $finalApk
    & $apksigner verify --verbose $finalApk
    Get-Item $finalApk | Select-Object FullName, Length, LastWriteTime
}
finally {
    Pop-Location
}

Write-Host "OK: release verification complete"
