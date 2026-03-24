#!/usr/bin/env pwsh
# Build the entire RogueLikeNet solution
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location "$PSScriptRoot/.."
try {
    Write-Host "Building RogueLikeNet solution..." -ForegroundColor Cyan
    dotnet build
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "Build succeeded!" -ForegroundColor Green
}
finally {
    Pop-Location
}
