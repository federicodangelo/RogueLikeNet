#!/usr/bin/env pwsh
# Publish the web client to WASM and serve it locally
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Port = if ($args.Count -gt 0) { $args[0] } else { 8080 }

Push-Location "$PSScriptRoot/.."
try {
    Write-Host "Publishing RogueLikeNet Web Client (WASM)..." -ForegroundColor Cyan
    dotnet publish src/RogueLikeNet.Client.Web -c Release
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    Write-Host "Publish succeeded!" -ForegroundColor Green
    Write-Host ""

    # Avalonia.Browser generates the servable bundle in AppBundle
    $wwwroot = "src/RogueLikeNet.Client.Web/bin/Release/net10.0/browser-wasm/AppBundle"
    if (-not (Test-Path $wwwroot)) {
        throw "AppBundle directory not found at $wwwroot"
    }

    # Try dotnet-serve first, fall back to Python, then to a minimal .NET server
    $dotnetServe = Get-Command dotnet-serve -ErrorAction SilentlyContinue
    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) { $python = Get-Command python3 -ErrorAction SilentlyContinue }

    if ($dotnetServe) {
        Write-Host "Serving with dotnet-serve at http://localhost:$Port" -ForegroundColor Yellow
        Write-Host "Press Ctrl+C to stop" -ForegroundColor DarkGray
        Push-Location $wwwroot
        dotnet-serve -p $Port --mime .wasm=application/wasm --mime .js=application/javascript
        Pop-Location
    }
    elseif ($python) {
        Write-Host "Serving with Python HTTP server at http://localhost:$Port" -ForegroundColor Yellow
        Write-Host "Press Ctrl+C to stop" -ForegroundColor DarkGray
        Push-Location $wwwroot
        & $python.Source -m http.server $Port
        Pop-Location
    }
    else {
        Write-Host "No static file server found." -ForegroundColor Red
        Write-Host "Install one of the following:" -ForegroundColor Yellow
        Write-Host "  dotnet tool install -g dotnet-serve" -ForegroundColor White
        Write-Host "  or install Python (python -m http.server)" -ForegroundColor White
        Write-Host ""
        Write-Host "Published files are at: $((Resolve-Path $wwwroot).Path)" -ForegroundColor Cyan
        Write-Host "Serve that directory with any static file server." -ForegroundColor Cyan
        exit 1
    }
}
finally {
    Pop-Location
}
