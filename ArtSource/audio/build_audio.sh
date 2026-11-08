#!/usr/bin/env bash
# Regenerates all game audio, then checks it. Blender's bundled Python supplies numpy.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
PY="${SS_PY:-$(ls /mnt/Apps/blender-*/[0-9]*/python/bin/python3* | head -n1)}"
"$PY" -I "$HERE/gen_audio.py" "$@"
"$PY" -I "$HERE/check_audio.py"
