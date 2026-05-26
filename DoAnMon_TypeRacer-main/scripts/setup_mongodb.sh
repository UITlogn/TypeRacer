#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTAINER_NAME="${CONTAINER_NAME:-typeracer-mongo}"
IMAGE="${IMAGE:-docker.io/library/mongo:7.0}"
HOST_PORT="${HOST_PORT:-27017}"
DB_NAME="${DB_NAME:-TypeRacer}"
COLLECTION_NAME="${COLLECTION_NAME:-passages}"
TMP_JSON="/tmp/typeracer_passages.json"

echo "[1/4] Pulling MongoDB image: $IMAGE"
podman pull "$IMAGE" >/dev/null

echo "[2/4] Ensuring container '$CONTAINER_NAME' is running on port $HOST_PORT"
if podman container exists "$CONTAINER_NAME"; then
  if ! podman ps --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
    podman start "$CONTAINER_NAME" >/dev/null
  fi
else
  podman run -d \
    --name "$CONTAINER_NAME" \
    -p "${HOST_PORT}:27017" \
    "$IMAGE" >/dev/null
fi

echo "[3/4] Exporting passages from SQL seed file"
python3 "$ROOT_DIR/scripts/export_seed_passages_json.py" > "$TMP_JSON"

echo "[4/4] Seeding MongoDB collection $DB_NAME.$COLLECTION_NAME"
podman exec -i "$CONTAINER_NAME" mongosh --quiet <<EOF
use $DB_NAME
db.$COLLECTION_NAME.deleteMany({})
db.$COLLECTION_NAME.insertMany($(cat "$TMP_JSON"))
printjson({
  database: "$DB_NAME",
  collection: "$COLLECTION_NAME",
  count: db.$COLLECTION_NAME.countDocuments()
})
EOF

echo "MongoDB is ready at mongodb://localhost:${HOST_PORT}"
