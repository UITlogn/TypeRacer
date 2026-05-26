#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_ZIP="${SOURCE_ZIP:-${1:-${ROOT_DIR}/TypeRacer.Client.source-for-vs.zip}}"
PLAYER_ZIP="${PLAYER_ZIP:-${2:-${ROOT_DIR}/TypeRacer.Player.All.zip}}"
PUBLISH_DIR="${ROOT_DIR}/.publish/client-win-x64"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

if ! "${DOTNET_BIN}" --info >/dev/null 2>&1; then
  if [[ -x "${HOME}/.dotnet/dotnet" ]]; then
    DOTNET_BIN="${HOME}/.dotnet/dotnet"
  else
    echo "Cannot run dotnet. Set DOTNET_BIN to a working SDK path." >&2
    exit 1
  fi
fi

cd "${ROOT_DIR}"

rm -f "${SOURCE_ZIP}" "${PLAYER_ZIP}"
rm -rf "${PUBLISH_DIR}"

zip -r "${SOURCE_ZIP}" \
  TypeRacer.ClientOnly.sln \
  CLIENT_QUICKSTART.md \
  src/TypeRacer.Client \
  src/TypeRacer.Shared \
  -x '*/bin/*' '*/obj/*' '*.user' '*.suo' '*.cache'

"${DOTNET_BIN}" publish src/TypeRacer.Client/TypeRacer.Client.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:EnableWindowsTargeting=true \
  -o "${PUBLISH_DIR}"

cp CLIENT_QUICKSTART.md "${PUBLISH_DIR}/CLIENT_QUICKSTART.md"

(
  cd "${PUBLISH_DIR}"
  zip -r "${PLAYER_ZIP}" .
)

echo "Created source package: ${SOURCE_ZIP}"
echo "Created player package: ${PLAYER_ZIP}"
