#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

VPS_HOST="${VPS_HOST:-134.209.108.82}"
VPS_USER="${VPS_USER:-root}"
SSH_TARGET="${VPS_USER}@${VPS_HOST}"
DEPLOY_DIR="${DEPLOY_DIR:-/opt/typeracer-prod}"
SERVER_PORT="${SERVER_PORT:-5000}"
SQL_PORT="${SQL_PORT:-1433}"
SQL_CONTAINER_NAME="${SQL_CONTAINER_NAME:-typeracer-sql}"
SQL_IMAGE="${SQL_IMAGE:-mcr.microsoft.com/mssql/server:2022-latest}"
SQL_TOOLS_IMAGE="${SQL_TOOLS_IMAGE:-mcr.microsoft.com/mssql-tools:latest}"
DOTNET_SDK_IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:8.0}"
AI_COACH_PROVIDER="${AI_COACH_PROVIDER:-openclaude}"
AI_COACH_MODEL="${AI_COACH_MODEL:-gpt-5.5}"
AI_COACH_BASE_URL="${AI_COACH_BASE_URL:-https://open-claude.com}"
AI_COACH_API_KEY="${AI_COACH_API_KEY:-}"
AI_COACH_TIMEOUT_MS="${AI_COACH_TIMEOUT_MS:-90000}"
AI_COACH_MAX_RETRIES="${AI_COACH_MAX_RETRIES:-5}"

if [[ -z "${SQL_SA_PASSWORD:-}" ]]; then
  echo "ERROR: Missing SQL_SA_PASSWORD environment variable."
  echo "Example: SQL_SA_PASSWORD='StrongPass!123' ./scripts/deploy_vps_sql.sh"
  exit 1
fi

if [[ ! -f "${ROOT_DIR}/TypeRacer.sln" ]]; then
  echo "ERROR: Run this script from inside DoAnMon_TypeRacer repository."
  exit 1
fi

echo "[1/7] Sync project to ${SSH_TARGET}:${DEPLOY_DIR}/current"
ssh "${SSH_TARGET}" "mkdir -p '${DEPLOY_DIR}/current'"
rsync -az --delete \
  --exclude '.git' \
  --exclude '.vs' \
  --exclude '.publish' \
  --exclude '.deploy.env' \
  --exclude 'src/**/bin' \
  --exclude 'src/**/obj' \
  --exclude 'logs' \
  --exclude '.env' \
  "${ROOT_DIR}/" "${SSH_TARGET}:${DEPLOY_DIR}/current/"

echo "[2/7] Prepare SQL Server container + schema + publish + service"
SQL_SA_PASSWORD_B64="$(printf '%s' "${SQL_SA_PASSWORD}" | base64 -w0)"
AI_COACH_API_KEY_B64="$(printf '%s' "${AI_COACH_API_KEY}" | base64 -w0)"
ssh "${SSH_TARGET}" \
  "DEPLOY_DIR='${DEPLOY_DIR}' SERVER_PORT='${SERVER_PORT}' SQL_PORT='${SQL_PORT}' SQL_CONTAINER_NAME='${SQL_CONTAINER_NAME}' SQL_IMAGE='${SQL_IMAGE}' SQL_TOOLS_IMAGE='${SQL_TOOLS_IMAGE}' DOTNET_SDK_IMAGE='${DOTNET_SDK_IMAGE}' SQL_SA_PASSWORD_B64='${SQL_SA_PASSWORD_B64}' AI_COACH_PROVIDER='${AI_COACH_PROVIDER}' AI_COACH_MODEL='${AI_COACH_MODEL}' AI_COACH_BASE_URL='${AI_COACH_BASE_URL}' AI_COACH_API_KEY_B64='${AI_COACH_API_KEY_B64}' AI_COACH_TIMEOUT_MS='${AI_COACH_TIMEOUT_MS}' AI_COACH_MAX_RETRIES='${AI_COACH_MAX_RETRIES}' bash -s" <<'REMOTE'
set -euo pipefail

SQL_SA_PASSWORD="$(printf '%s' "${SQL_SA_PASSWORD_B64}" | base64 -d)"
AI_COACH_API_KEY="$(printf '%s' "${AI_COACH_API_KEY_B64}" | base64 -d)"

if ! command -v docker >/dev/null 2>&1; then
  apt-get update
  apt-get install -y docker.io
fi
systemctl enable --now docker

mkdir -p "${DEPLOY_DIR}/sqlserver-data"
mkdir -p "${DEPLOY_DIR}/current/.publish/server"
chown -R 10001:0 "${DEPLOY_DIR}/sqlserver-data"
chmod -R g+rwX "${DEPLOY_DIR}/sqlserver-data"

if ! docker ps -a --format '{{.Names}}' | grep -qx "${SQL_CONTAINER_NAME}"; then
  docker run -d \
    --name "${SQL_CONTAINER_NAME}" \
    --restart unless-stopped \
    -e ACCEPT_EULA=Y \
    -e MSSQL_SA_PASSWORD="${SQL_SA_PASSWORD}" \
    -p "127.0.0.1:${SQL_PORT}:1433" \
    -v "${DEPLOY_DIR}/sqlserver-data:/var/opt/mssql" \
    "${SQL_IMAGE}"
else
  docker start "${SQL_CONTAINER_NAME}" >/dev/null
fi

SQLCMD_PATH="/opt/mssql-tools18/bin/sqlcmd"
if ! docker run --rm "${SQL_TOOLS_IMAGE}" sh -lc "test -x ${SQLCMD_PATH}"; then
  SQLCMD_PATH="/opt/mssql-tools/bin/sqlcmd"
fi

echo "Waiting for SQL Server to accept connections..."
SQL_READY=0
for _ in $(seq 1 60); do
  if docker run --rm --network host "${SQL_TOOLS_IMAGE}" \
    "${SQLCMD_PATH}" \
      -S "127.0.0.1,${SQL_PORT}" \
      -U sa \
      -P "${SQL_SA_PASSWORD}" \
      -C \
      -Q "SELECT 1" >/dev/null 2>&1; then
    SQL_READY=1
    break
  fi
  sleep 2
done
if [[ "${SQL_READY}" -ne 1 ]]; then
  echo "ERROR: SQL Server did not become ready in time." >&2
  exit 1
fi

docker run --rm --network host -v "${DEPLOY_DIR}/current/database:/sql:ro" "${SQL_TOOLS_IMAGE}" \
  "${SQLCMD_PATH}" \
    -S "127.0.0.1,${SQL_PORT}" -U sa -P "${SQL_SA_PASSWORD}" -C -i /sql/001_create_tables.sql
docker run --rm --network host -v "${DEPLOY_DIR}/current/database:/sql:ro" "${SQL_TOOLS_IMAGE}" \
  "${SQLCMD_PATH}" \
    -S "127.0.0.1,${SQL_PORT}" -U sa -P "${SQL_SA_PASSWORD}" -C -i /sql/002_create_indexes.sql

PASSAGES_COUNT="$(docker run --rm --network host "${SQL_TOOLS_IMAGE}" \
  "${SQLCMD_PATH}" \
    -S "127.0.0.1,${SQL_PORT}" -U sa -P "${SQL_SA_PASSWORD}" -C \
    -Q "SET NOCOUNT ON; USE TypeRacerDB; SELECT COUNT(*) FROM passages;" -W -h-1 \
  | tr -d '\r' | tail -n 1)"

if [[ "${PASSAGES_COUNT}" == "0" ]]; then
  echo "Seeding passages..."
  docker run --rm --network host -v "${DEPLOY_DIR}/current/database:/sql:ro" "${SQL_TOOLS_IMAGE}" \
    "${SQLCMD_PATH}" \
      -S "127.0.0.1,${SQL_PORT}" -U sa -P "${SQL_SA_PASSWORD}" -C -i /sql/003_seed_passages.sql
else
  echo "Skip seeding passages (current count: ${PASSAGES_COUNT})"
fi

docker run --rm --network host -v "${DEPLOY_DIR}/current/database:/sql:ro" "${SQL_TOOLS_IMAGE}" \
  "${SQLCMD_PATH}" \
    -S "127.0.0.1,${SQL_PORT}" -U sa -P "${SQL_SA_PASSWORD}" -C -i /sql/004_passage_language_migration.sql

docker run --rm \
  -v "${DEPLOY_DIR}/current:/src" \
  -w /src/src/TypeRacer.Server \
  "${DOTNET_SDK_IMAGE}" \
  dotnet publish -c Release -r linux-x64 --self-contained true -o /src/.publish/server

cat > "${DEPLOY_DIR}/current/.deploy.env" <<ENVVARS
DATA__PROVIDER=SqlServer
SERVER__PORT=${SERVER_PORT}
CONNECTIONSTRINGS__DEFAULTCONNECTION=Server=127.0.0.1,${SQL_PORT};Database=TypeRacerDB;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=true;Encrypt=false;
AICOACH__ENABLED=true
AICOACH__PROVIDER=${AI_COACH_PROVIDER}
AICOACH__OPENAIMODEL=${AI_COACH_MODEL}
AICOACH__OPENAIBASEURL=${AI_COACH_BASE_URL}
AICOACH__OPENAIAPIKEY=${AI_COACH_API_KEY}
AICOACH__TIMEOUTMS=${AI_COACH_TIMEOUT_MS}
AICOACH__MAXRETRIES=${AI_COACH_MAX_RETRIES}
ENVVARS

cat > /etc/systemd/system/typeracer-server.service <<SERVICE
[Unit]
Description=TypeRacer TCP Server
After=network-online.target docker.service
Wants=network-online.target
Requires=docker.service

[Service]
Type=simple
WorkingDirectory=${DEPLOY_DIR}/current/.publish/server
EnvironmentFile=${DEPLOY_DIR}/current/.deploy.env
ExecStart=${DEPLOY_DIR}/current/.publish/server/TypeRacer.Server
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
SERVICE

systemctl daemon-reload
systemctl enable typeracer-server
systemctl restart typeracer-server

if command -v ufw >/dev/null 2>&1; then
  ufw allow "${SERVER_PORT}/tcp" >/dev/null 2>&1 || true
fi
REMOTE

echo "[3/7] Service status"
ssh "${SSH_TARGET}" "systemctl --no-pager --full status typeracer-server | sed -n '1,40p'"

echo "[4/7] Verify listening socket"
ssh "${SSH_TARGET}" "ss -ltnp | grep -E ':${SERVER_PORT}[[:space:]]' || true"

echo "[5/7] Tail latest server logs"
ssh "${SSH_TARGET}" "journalctl -u typeracer-server -n 40 --no-pager"

echo "[6/7] Done"
echo "Server endpoint: ${VPS_HOST}:${SERVER_PORT}"
echo "Run smoke test:"
echo "  python3 ${ROOT_DIR}/scripts/smoke_test_tcp.py --host ${VPS_HOST} --port ${SERVER_PORT}"
