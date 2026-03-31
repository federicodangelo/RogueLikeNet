#!/usr/bin/env pwsh
# Launch the desktop client in standalone mode (embedded server)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location "$PSScriptRoot/.."
try {
    Write-Host "Launching RogueLikeNet Desktop Client (standalone mode)..." -ForegroundColor Cyan
    Write-Host ""
    dotnet publish -c Release src/RogueLikeNet.Client.Desktop
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    src\RogueLikeNet.Client.Desktop\bin\Release\net10.0\win-x64\publish\RogueLikeNet.Client.Desktop.exe
    
}
finally {
    Pop-Location
}
