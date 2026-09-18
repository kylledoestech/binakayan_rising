#!/usr/bin/env bash
# Rebuilds every encampment building and prop from the Blender scripts, then checks them.
# Pass --only <Name> to rebuild one piece.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CAMP_DIR="${CAMP_DIR:-$HERE/../../Assets/_Project/Art/Encampment}"
BLENDER="${BLENDER:-blender}"

"$BLENDER" -b --factory-startup --python "$HERE/render_camp.py" -- --out "$CAMP_DIR" "$@" \
    | grep -E "^SPRITE|Error|Traceback|^  File" || true

python3 "$HERE/check_camp.py" "$CAMP_DIR" --sheet "$HERE/out/camp_sheet.png"
