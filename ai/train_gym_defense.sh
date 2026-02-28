#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
CONFIG_PATH="${SCRIPT_DIR}/hockey_defense_ppo.yaml"
RESULTS_DIR="${SCRIPT_DIR}/results"
COMPAT_PYTHONPATH="${SCRIPT_DIR}/compat"

RUN_ID="gym_defense_v1"
if [ "${1:-}" != "" ] && [[ "${1}" != -* ]]; then
  RUN_ID="${1}"
  shift
fi

resolve_python() {
  local candidate="$1"
  if [ -x "${candidate}" ]; then
    printf "%s\n" "${candidate}"
    return 0
  fi

  if command -v "${candidate}" >/dev/null 2>&1; then
    command -v "${candidate}"
    return 0
  fi

  return 1
}

is_compatible_mlagents_stack() {
  local python_bin="$1"
  "${python_bin}" - <<'PY' >/dev/null 2>&1
from importlib import metadata
import sys

def version(dist: str):
    try:
        return metadata.version(dist)
    except metadata.PackageNotFoundError:
        return None

mlagents = version("mlagents")
mlagents_envs = version("mlagents-envs")
protobuf = version("protobuf")
onnx = version("onnx")
torch = version("torch")

if not all([mlagents, mlagents_envs, protobuf, onnx, torch]):
    sys.exit(1)

if mlagents != "1.1.0" or mlagents_envs != "1.1.0":
    sys.exit(1)

parts = protobuf.split(".")
if len(parts) < 2:
    sys.exit(1)
if int(parts[0]) != 3 or int(parts[1]) > 20:
    sys.exit(1)

if onnx != "1.15.0":
    sys.exit(1)
PY
}

PYTHON_CANDIDATES=()
if [ -n "${MLAGENTS_PYTHON:-}" ]; then
  PYTHON_CANDIDATES+=("${MLAGENTS_PYTHON}")
fi
PYTHON_CANDIDATES+=(
  "${SCRIPT_DIR}/venv/bin/python"
  "${HOME}/.pyenv/versions/3.10.12/bin/python"
  "python3"
  "python"
)

PYTHON_BIN=""
for candidate in "${PYTHON_CANDIDATES[@]}"; do
  if ! resolved="$(resolve_python "${candidate}")"; then
    continue
  fi

  if is_compatible_mlagents_stack "${resolved}"; then
    PYTHON_BIN="${resolved}"
    break
  fi
done

if [ -z "${PYTHON_BIN}" ]; then
  echo "No compatible ML-Agents Python environment found." >&2
  echo "Expected: mlagents==1.1.0, mlagents-envs==1.1.0, protobuf<3.21, onnx==1.15.0." >&2
  echo "Tip: use ~/.pyenv/versions/3.10.12 or recreate ai/venv with pinned requirements." >&2
  exit 1
fi

export PYTHONPATH="${COMPAT_PYTHONPATH}${PYTHONPATH:+:${PYTHONPATH}}"

"${PYTHON_BIN}" -m mlagents.trainers.learn "${CONFIG_PATH}" \
  --run-id "${RUN_ID}" \
  --results-dir "${RESULTS_DIR}" \
  --time-scale 20 \
  --timeout-wait 300 \
  "$@"
