#!/usr/bin/env bash
# Build the entire RogueLikeNet solution
set -euo pipefail
cd "$(dirname "$0")/.."

echo "Building RogueLikeNet solution..."
dotnet build
echo "Build succeeded!"
