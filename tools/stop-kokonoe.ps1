$ErrorActionPreference = 'SilentlyContinue'

Get-Process KokonoeAssistant | Stop-Process -Force

Write-Host 'Stopped KokonoeAssistant processes if any were running.'
