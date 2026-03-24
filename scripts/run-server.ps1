#!/usr/bin/env pwsh
# Start the dedicated multiplayer server
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location "$PSScriptRoot/.."
try {
    Write-Host "Starting RogueLikeNet Server..." -ForegroundColor Cyan
    Write-Host "  HTTP:      http://localhost:5090" -ForegroundColor Yellow
    Write-Host "  WebSocket: ws://localhost:5090/ws" -ForegroundColor Yellow
    Write-Host "  Press Ctrl+C to stop" -ForegroundColor DarkGray
    Write-Host ""
    dotnet run --project src/RogueLikeNet.Server
}
finally {
    Pop-Location
}
