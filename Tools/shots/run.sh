#!/usr/bin/env bash
# Runs the Linux player through a screenshot route and prints the layout audit.
#
#   Tools/shots/run.sh [route] [out dir]
#
# Build the player first (Tools > Binakayan Rising > Build Linux Player, or batchmode:
# Unity -batchmode -quit -projectPath . -executeMethod BinakayanRising.EditorTools.PlayerBuild.BuildLinux).
# The route starts and deletes campaigns, so it always runs against a throwaway save folder.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$HERE/../.." && pwd)"
ROUTE="${1:-phase1}"
OUT="$(realpath -m "${2:-$HERE/out/$ROUTE}")"
PLAYER="$PROJECT/Builds/Linux/BinakayanRising.x86_64"
SAVES="$(mktemp -d)"
trap 'rm -rf "$SAVES"' EXIT

[[ -x "$PLAYER" ]] || { echo "error: no player at $PLAYER; build it first" >&2; exit 1; }
rm -rf "$OUT" && mkdir -p "$OUT"

status=0
"$PLAYER" -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 \
    -brShot "$ROUTE" -brShotDir "$OUT" -brSaveDir "$SAVES" \
    -logFile "$OUT/player.log" || status=$?

cat "$OUT/audit.txt" 2>/dev/null || echo "no audit written; see $OUT/player.log"
grep -E "Exception|Error:|\[Shot\] refusing" "$OUT/player.log" | grep -v "^\s*$" | head -20 || true
echo "exit $status, shots in $OUT"
exit $status
