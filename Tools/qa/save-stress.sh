#!/usr/bin/env bash
# Proposal Table 5, "zero corrupted saves": kills the player with SIGKILL while it is rewriting
# its save as fast as it can, relaunches, and checks the save still loads. Repeats N times.
#
#   Tools/qa/save-stress.sh [rounds=100] [out dir]
#
# Every load after the first reads a save whose writer died at a random point: mid temp-file
# write, mid File.Replace, or between the two. A round fails if the save exists but neither it
# nor its backup loads ("load=FAIL"), or if the write counter goes backwards by more than the
# one save the backup can cost.
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$HERE/../.." && pwd)"
ROUNDS="${1:-100}"
OUT="$(realpath -m "${2:-$HERE/out/save-stress}")"
PLAYER="$PROJECT/Builds/Linux/BinakayanRising.x86_64"
[[ -x "$PLAYER" ]] || { echo "error: no player at $PLAYER; build it first" >&2; exit 1; }
rm -rf "$OUT" && mkdir -p "$OUT/saves"

fails=0; backups=0; last=0
for ((i = 1; i <= ROUNDS; i++)); do
    log="$OUT/round-$i.log"
    "$PLAYER" -batchmode -nographics -brSaveStress -brSaveDir "$OUT/saves" -logFile "$log" &
    pid=$!
    for _ in $(seq 200); do grep -q "\[SaveStress\] saving" "$log" 2>/dev/null && break; sleep 0.05; done
    sleep "0.$((RANDOM % 900 + 50))"
    kill -9 "$pid" 2>/dev/null; wait "$pid" 2>/dev/null
    line=$(grep -o "\[SaveStress\] boot.*" "$log" | head -1)
    writes=$(sed -n 's/.*writes=\([0-9]*\).*/\1/p' <<<"$line")
    wrote=$(grep -o "\[SaveStress\] wrote [0-9]*" "$log" | tail -1 | grep -o "[0-9]*$")
    [[ "$line" == *load=FAIL* ]] && { echo "round $i: CORRUPT  $line"; fails=$((fails + 1)); }
    [[ "$line" == *backup=True* ]] && backups=$((backups + 1))
    if (( i > 1 )) && [[ -n "$writes" ]] && (( writes + 1 < last )); then
        echo "round $i: counter went back from $last to $writes"; fails=$((fails + 1))
    fi
    [[ -n "$wrote" ]] && last=$wrote
    echo "round $i: ${line#*boot } (last logged write $wrote)" >> "$OUT/summary.txt"
done
ls "$OUT/saves" >> "$OUT/summary.txt"
echo "rounds $ROUNDS, corrupt $fails, restored from backup $backups" | tee -a "$OUT/summary.txt"
(( fails == 0 ))
