#!/usr/bin/env bash
# Run all unit tests
set -euo pipefail
cd "$(dirname "$0")/.."

echo "Running all tests..."
dotnet test --verbosity normal
echo "All tests passed!"
