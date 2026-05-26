#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-$ROOT/.dotnet/dotnet}"
if [[ ! -x "$DOTNET_BIN" ]]; then
  DOTNET_BIN="$(command -v dotnet || true)"
fi
if [[ -z "$DOTNET_BIN" && -x /root/.dotnet/dotnet ]]; then
  DOTNET_BIN="/root/.dotnet/dotnet"
fi
if [[ -z "$DOTNET_BIN" ]]; then
  echo "dotnet was not found. Set DOTNET_BIN=/path/to/dotnet." >&2
  exit 127
fi

cd "$ROOT"
"$DOTNET_BIN" publish tools/TypeRacer.UiProbe/TypeRacer.UiProbe.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o .publish/ui-probe \
  -v:minimal \
  /p:UseSharedCompilation=false

xvfb-run -a wine .publish/ui-probe/TypeRacer.UiProbe.exe
