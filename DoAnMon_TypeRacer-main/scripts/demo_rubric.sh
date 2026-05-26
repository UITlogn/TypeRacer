#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PYTHON_BIN="${PYTHON_BIN:-python3}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

HOST="134.209.108.82"
PORT="5000"
TIMEOUT="75"
LOOPS="100"
RUN_FULL=1
RUN_CONCURRENCY=1
RUN_LOAD_BALANCER=1
INCLUDE_TIMEOUT_CASE=0

usage() {
  cat <<'USAGE'
Usage: bash scripts/demo_rubric.sh [options]

Options:
  --host <host>                 Target host (default: 134.209.108.82)
  --port <port>                 Target port (default: 5000)
  --timeout <seconds>           Script timeout for python tests (default: 75)
  --loops <n>                   Loops for full pipeline test (default: 100)
  --quick                       Faster run: loops=30 and skip concurrency test
  --skip-full                   Skip full_pipeline_test.py
  --skip-concurrency            Skip race_concurrency_test.py
  --skip-load-balancer          Skip local load_balancer_probe_test.py
  --include-timeout-case        Include timeout case in race_concurrency_test.py
  --python <bin>                Python executable (default: python3)
  --dotnet <bin>                dotnet executable (default: dotnet, fallback: ~/.dotnet/dotnet)
  -h, --help                    Show this help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --host)
      HOST="$2"
      shift 2
      ;;
    --port)
      PORT="$2"
      shift 2
      ;;
    --timeout)
      TIMEOUT="$2"
      shift 2
      ;;
    --loops)
      LOOPS="$2"
      shift 2
      ;;
    --quick)
      LOOPS="30"
      RUN_CONCURRENCY=0
      shift
      ;;
    --skip-full)
      RUN_FULL=0
      shift
      ;;
    --skip-concurrency)
      RUN_CONCURRENCY=0
      shift
      ;;
    --skip-load-balancer)
      RUN_LOAD_BALANCER=0
      shift
      ;;
    --include-timeout-case)
      INCLUDE_TIMEOUT_CASE=1
      shift
      ;;
    --python)
      PYTHON_BIN="$2"
      shift 2
      ;;
    --dotnet)
      DOTNET_BIN="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      usage
      exit 2
      ;;
  esac
done

if ! "${DOTNET_BIN}" --info >/dev/null 2>&1; then
  if [[ -x "${HOME}/.dotnet/dotnet" ]]; then
    DOTNET_BIN="${HOME}/.dotnet/dotnet"
  else
    echo "Cannot run dotnet. Set --dotnet or DOTNET_BIN to a working SDK path." >&2
    exit 1
  fi
fi

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
LOG_DIR="${ROOT_DIR}/logs/rubric_demo_${TIMESTAMP}"
mkdir -p "${LOG_DIR}"

PASS_COUNT=0
FAIL_COUNT=0

declare -a SUMMARY_LINES=()

run_case() {
  local case_name="$1"
  shift

  local log_file="${LOG_DIR}/${case_name}.log"
  echo "[RUN] ${case_name}"
  echo "      $*"

  if "$@" >"${log_file}" 2>&1; then
    echo "[PASS] ${case_name}"
    SUMMARY_LINES+=("PASS | ${case_name} | ${log_file}")
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    local code=$?
    echo "[FAIL] ${case_name} (exit=${code})"
    SUMMARY_LINES+=("FAIL | ${case_name} | ${log_file}")
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
}

run_case "smoke_test_tcp" \
  "${PYTHON_BIN}" "${ROOT_DIR}/scripts/smoke_test_tcp.py" \
  --host "${HOST}" --port "${PORT}"

run_case "unit_tests" \
  "${DOTNET_BIN}" test "${ROOT_DIR}/src/TypeRacer.Tests/TypeRacer.Tests.csproj" \
  -v:minimal

run_case "rubric_evidence_audit" \
  "${PYTHON_BIN}" "${ROOT_DIR}/scripts/rubric_evidence_audit.py"

run_case "encrypted_protocol_test" \
  "${PYTHON_BIN}" "${ROOT_DIR}/scripts/encrypted_protocol_test.py" \
  --host "${HOST}" --port "${PORT}" --timeout "${TIMEOUT}"

run_case "ui_layout_static_audit" \
  "${PYTHON_BIN}" "${ROOT_DIR}/scripts/ui_layout_static_audit.py"

if [[ "${RUN_LOAD_BALANCER}" -eq 1 ]]; then
  run_case "load_balancer_probe_test" \
    "${PYTHON_BIN}" "${ROOT_DIR}/scripts/load_balancer_probe_test.py"
fi

run_case "edge_matrix_test" \
  "${PYTHON_BIN}" "${ROOT_DIR}/scripts/edge_matrix_test.py" \
  --host "${HOST}" --port "${PORT}" --timeout "${TIMEOUT}"

run_case "game_mode_protocol_test" \
  "${PYTHON_BIN}" "${ROOT_DIR}/scripts/game_mode_protocol_test.py" \
  --host "${HOST}" --port "${PORT}" --timeout "${TIMEOUT}"

run_case "custom_text_ai_protocol_test" \
  "${PYTHON_BIN}" "${ROOT_DIR}/scripts/custom_text_ai_protocol_test.py" \
  --host "${HOST}" --port "${PORT}" --timeout "${TIMEOUT}"

if [[ "${RUN_FULL}" -eq 1 ]]; then
  run_case "full_pipeline_test" \
    "${PYTHON_BIN}" "${ROOT_DIR}/scripts/full_pipeline_test.py" \
    --host "${HOST}" --port "${PORT}" --timeout "${TIMEOUT}" --loops "${LOOPS}"
fi

if [[ "${RUN_CONCURRENCY}" -eq 1 ]]; then
  concurrency_cmd=(
    "${PYTHON_BIN}" "${ROOT_DIR}/scripts/race_concurrency_test.py"
    --host "${HOST}" --port "${PORT}" --timeout "${TIMEOUT}"
  )

  if [[ "${INCLUDE_TIMEOUT_CASE}" -eq 1 ]]; then
    concurrency_cmd+=(--include-timeout-case)
  fi

  run_case "race_concurrency_test" "${concurrency_cmd[@]}"
fi

SUMMARY_FILE="${LOG_DIR}/summary.txt"
{
  echo "Rubric demo summary"
  echo "Generated at: $(date -Iseconds)"
  echo "Target: ${HOST}:${PORT}"
  echo "Timeout: ${TIMEOUT}s"
  echo "Loops (full pipeline): ${LOOPS}"
  echo ""
  for line in "${SUMMARY_LINES[@]}"; do
    echo "${line}"
  done
  echo ""
  echo "Total PASS: ${PASS_COUNT}"
  echo "Total FAIL: ${FAIL_COUNT}"
} >"${SUMMARY_FILE}"

echo ""
echo "Summary file: ${SUMMARY_FILE}"
if [[ "${FAIL_COUNT}" -gt 0 ]]; then
  echo "Kết quả: FAIL (${FAIL_COUNT} case lỗi)"
  exit 1
fi

echo "Kết quả: PASS (all ${PASS_COUNT} cases)"
exit 0
