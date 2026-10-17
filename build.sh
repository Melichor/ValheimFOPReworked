#!/usr/bin/env bash
# Build the mod. The work is done by tools/build.py; ./build.sh --help lists the options.
set -euo pipefail
command -v python3 >/dev/null || { echo "error: python3 is needed but not installed" >&2; exit 1; }
exec python3 "$(dirname "${BASH_SOURCE[0]}")/tools/build.py" "$@"
