#!/bin/bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
if command -v dotnet >/dev/null 2>&1; then
  terrain_dotnet="$(command -v dotnet)"
elif [[ -x /Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet && "$(uname -m)" == arm64 ]]; then
  terrain_dotnet=/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet
elif [[ -x /Applications/Rider.app/Contents/lib/ReSharperHost/macos-x64/dotnet/dotnet ]]; then
  terrain_dotnet=/Applications/Rider.app/Contents/lib/ReSharperHost/macos-x64/dotnet/dotnet
else
  echo 'Нужен .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
  exit 1
fi
if [[ "${1:-}" == "--smoke-test" ]]; then
  shift
  exec "$terrain_dotnet" run --project tests/Terrain.App.SmokeTests -c Release -- "$@"
fi
exec "$terrain_dotnet" run --project src/Terrain.App -c Release -- "$@"
