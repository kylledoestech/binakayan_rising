#!/usr/bin/env bash
# Rebuilds every unit sprite from the Blender scripts, then checks them.
# Pass --only <ArchetypeId> to rebuild one unit.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITS_DIR="${UNITS_DIR:-$HERE/../../Assets/_Project/Art/Units}"
BLENDER="${BLENDER:-blender}"

"$BLENDER" -b --factory-startup --python "$HERE/build_and_render.py" -- --out "$UNITS_DIR" "$@" \
    | grep -E "^SPRITE|Error|Traceback|^  File" || true

python3 "$HERE/check_sprites.py" "$UNITS_DIR" --sheet "$HERE/out/sheet.png"
