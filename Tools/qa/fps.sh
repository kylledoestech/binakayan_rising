#!/usr/bin/env bash
# Proposal Table 5, "30-60 FPS in combat": plays one full battle through the "bench" shot route
# with the frame-rate probe on, then prints the report (average, 1% low, worst frame).
#
#   Tools/qa/fps.sh [width=1920] [height=1080]
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$HERE/../.." && pwd)"
OUT="$HERE/out/fps"
PLAYER="$PROJECT/Builds/Linux/BinakayanRising.x86_64"
SAVES="$(mktemp -d)"
trap 'rm -rf "$SAVES"' EXIT
[[ -x "$PLAYER" ]] || { echo "error: no player at $PLAYER; build it first" >&2; exit 1; }
rm -rf "$OUT" && mkdir -p "$OUT"
"$PLAYER" -screen-fullscreen 0 -screen-width "${1:-1920}" -screen-height "${2:-1080}" \
    -brShot bench -brShotDir "$OUT" -brSaveDir "$SAVES" -brFps "$OUT/fps.txt" -logFile "$OUT/player.log" || true
cat "$OUT/fps.txt"
