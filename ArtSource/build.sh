#!/bin/bash
# Usage: ArtSource/build.sh ArtSource/assets/foo.py [more scripts...]  (one Blender run each, headless)
HERE="$(cd "$(dirname "$0")" && pwd)"
BLENDER=/mnt/Apps/blender-5.2.3-linux-x64/blender
rc=0
for s in "$@"; do
  PYTHONPATH="$HERE/lib" "$BLENDER" -b --factory-startup --python-use-system-env --python-exit-code 1 -P "$s" 2>&1 | grep -E 'ASSET_|Traceback|Error|error:|File "' | grep -v 'Fra:'
  [ "${PIPESTATUS[0]}" -eq 0 ] || rc=1
done
exit $rc
