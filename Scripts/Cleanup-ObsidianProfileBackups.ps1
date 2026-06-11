param(
    [string]$BackupDir = "O:\App\Obsidian\MyBrain\Yasus\Kokonoe\Profile Backups",
    [int]$KeepDays = 7,
    [int]$WindowMinutes = 60,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$resolved = [System.IO.Path]::GetFullPath($BackupDir)
$expected = [System.IO.Path]::GetFullPath("O:\App\Obsidian\MyBrain\Yasus\Kokonoe\Profile Backups")
if ($resolved -ne $expected) {
    throw "Refusing to clean unexpected directory: $resolved"
}

if (-not (Test-Path -LiteralPath $resolved -PathType Container)) {
    Write-Output "Backup directory does not exist: $resolved"
    exit 0
}

$now = Get-Date
$cutoff = $now.AddDays(-[math]::Abs($KeepDays))
$files = Get-ChildItem -LiteralPath $resolved -File -Filter "Profile-*.md" |
    Sort-Object LastWriteTime, Name

$delete = New-Object System.Collections.Generic.List[System.IO.FileInfo]

foreach ($file in $files) {
    if ($file.LastWriteTime -lt $cutoff) {
        $delete.Add($file)
    }
}

$kept = New-Object System.Collections.Generic.List[System.IO.FileInfo]
$recent = $files |
    Where-Object { $_.LastWriteTime -ge $cutoff } |
    Sort-Object LastWriteTime, Name -Descending

foreach ($file in $recent) {
    $tooCloseToKept = $false
    foreach ($keeper in $kept) {
        $span = [math]::Abs((New-TimeSpan -Start $file.LastWriteTime -End $keeper.LastWriteTime).TotalMinutes)
        if ($span -lt $WindowMinutes) {
            $tooCloseToKept = $true
            break
        }
    }

    if ($tooCloseToKept) {
        $delete.Add($file)
    } else {
        $kept.Add($file)
    }
}

$unique = $delete |
    Sort-Object FullName -Unique

$beforeCount = $files.Count
$beforeBytes = ($files | Measure-Object Length -Sum).Sum
$deleteBytes = ($unique | Measure-Object Length -Sum).Sum

if ($Apply) {
    foreach ($file in $unique) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
}

$afterCount = if ($Apply) {
    (Get-ChildItem -LiteralPath $resolved -File -Filter "Profile-*.md").Count
} else {
    $beforeCount - $unique.Count
}

[pscustomobject]@{
    BackupDir = $resolved
    Mode = if ($Apply) { "applied" } else { "dry-run" }
    BeforeCount = $beforeCount
    PlannedDeleteCount = $unique.Count
    AfterCount = $afterCount
    BeforeBytes = $beforeBytes
    PlannedDeleteBytes = $deleteBytes
    KeepDays = $KeepDays
    WindowMinutes = $WindowMinutes
} | Format-List
