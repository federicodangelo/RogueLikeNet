#!/usr/bin/env bash
# Launch the desktop client in standalone mode (embedded server)
set -euo pipefail
cd "$(dirname "$0")/.."

echo "Launching RogueLikeNet Desktop Client (standalone mode)..."
echo ""
dotnet run --project src/RogueLikeNet.Client.Desktop
