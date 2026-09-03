#!/usr/bin/env bash
# Runs the headless combat slice. Pass --seed N, --turns N, --quiet or --log through.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/toolchain.sh"

compile_netfx "$OUT_DIR/BattleDemo.exe" \
    "$CORE_SRC"/Grid/*.cs \
    "$CORE_SRC"/Combat/*.cs \
    "$PROJECT_ROOT/Tools/battle-sim/BattleDemo.cs"

exec "$MONO" "$OUT_DIR/BattleDemo.exe" "$@"
