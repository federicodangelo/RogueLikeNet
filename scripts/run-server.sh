#!/usr/bin/env bash
# Start the dedicated multiplayer server
set -euo pipefail
cd "$(dirname "$0")/.."

echo "Starting RogueLikeNet Server..."
echo "  HTTP:      http://localhost:5090"
echo "  WebSocket: ws://localhost:5090/ws"
echo "  Press Ctrl+C to stop"
echo ""
dotnet run --project src/RogueLikeNet.Server
