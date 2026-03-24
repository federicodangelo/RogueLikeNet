#!/usr/bin/env bash
# Publish the web client to WASM and serve it locally
set -euo pipefail
cd "$(dirname "$0")/.."

PORT="${1:-8080}"
echo "Publishing RogueLikeNet Web Client (WASM)..."
dotnet publish src/RogueLikeNet.Client.Web -c Release
echo "Publish succeeded!"
echo ""

# Avalonia.Browser generates the servable bundle in AppBundle
WWWROOT="src/RogueLikeNet.Client.Web/bin/Release/net10.0/browser-wasm/AppBundle"
if [ ! -d "$WWWROOT" ]; then
    echo "ERROR: AppBundle directory not found at $WWWROOT"
    exit 1
fi

# Try dotnet-serve first, fall back to Python
if command -v dotnet-serve &>/dev/null; then
    echo "Serving with dotnet-serve at http://localhost:$PORT"
    echo "Press Ctrl+C to stop"
    cd "$WWWROOT"
    dotnet-serve -p "$PORT" --mime .wasm=application/wasm --mime .js=application/javascript
elif command -v python3 &>/dev/null; then
    echo "Serving with Python HTTP server at http://localhost:$PORT"
    echo "Press Ctrl+C to stop"
    cd "$WWWROOT"
    python3 -m http.server "$PORT"
elif command -v python &>/dev/null; then
    echo "Serving with Python HTTP server at http://localhost:$PORT"
    echo "Press Ctrl+C to stop"
    cd "$WWWROOT"
    python -m http.server "$PORT"
else
    echo "ERROR: No static file server found."
    echo "Install one of the following:"
    echo "  dotnet tool install -g dotnet-serve"
    echo "  or install Python (python3 -m http.server)"
    echo ""
    echo "Published files are at: $(realpath "$WWWROOT")"
    echo "Serve that directory with any static file server."
    exit 1
fi
