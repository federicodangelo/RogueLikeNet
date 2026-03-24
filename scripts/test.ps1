#!/usr/bin/env pwsh
# Run all unit tests
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location "$PSScriptRoot/.."
try {
    Write-Host "Running all tests..." -ForegroundColor Cyan
    dotnet test --verbosity normal
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    Write-Host "All tests passed!" -ForegroundColor Green
}
finally {
    Pop-Location
}
