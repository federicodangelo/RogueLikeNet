#!/usr/bin/env pwsh
# Launch the desktop client in standalone mode (embedded server)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location "$PSScriptRoot/.."
try {
    Write-Host "Launching RogueLikeNet Desktop Client (standalone mode)..." -ForegroundColor Cyan
    Write-Host "  Controls: WASD or Arrow keys to move, Space to wait" -ForegroundColor Yellow
    Write-Host ""
    dotnet run --project src/RogueLikeNet.Client.Desktop
}
finally {
    Pop-Location
}
