#!/usr/bin/env bash
# Type-checks the Gameplay, UI and Editor assemblies with the Unity editor closed.
#
# Why this exists
# ---------------
# test.sh already proves BinakayanRising.Core compiles and passes its tests without Unity, but
# Core is the one assembly with `noEngineReferences: true`. Everything that touches uGUI,
# TextMeshPro or URP could previously only be compiled by opening the editor and waiting for a
# domain reload — a slow loop for what are almost always trivial mistakes.
#
# The trick is to not hand-maintain a reference list. Unity's build backend already writes a
# complete Roslyn response file per assembly under Library/Bee/artifacts/*.dag/, carrying all
# ~275 references and ~130 defines. This script borrows that file, retargets its output, swaps
# in the sources we actually want, and adds the few references our new asmdefs pull in that the
# cached one predates.
#
# Consequence worth knowing: the borrowed .rsp is a snapshot. If a package is added or removed,
# open Unity once so the backend regenerates it, then this script picks the new one up.
#
# Usage:  ./compile-ui.sh            type-check everything
#         ./compile-ui.sh --verbose  also print the generated response files
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUT_DIR="$SCRIPT_DIR/build"
VERBOSE=0
[[ "${1:-}" == "--verbose" ]] && VERBOSE=1

# ---------------------------------------------------------------- toolchain

UNITY_ROOT="${UNITY_ROOT:-$HOME/Unity/Hub/Editor}"
if [[ -n "${UNITY_VERSION:-}" ]]; then
    UNITY_DATA="$UNITY_ROOT/$UNITY_VERSION/Editor/Data"
else
    UNITY_DATA=""
    for candidate in "$UNITY_ROOT"/*/Editor/Data; do
        [[ -f "$candidate/DotNetSdkRoslyn/csc.dll" ]] && UNITY_DATA="$candidate"
    done
fi

if [[ -z "$UNITY_DATA" || ! -f "$UNITY_DATA/DotNetSdkRoslyn/csc.dll" ]]; then
    echo "error: no Unity editor found under $UNITY_ROOT" >&2
    echo "       set UNITY_ROOT or UNITY_VERSION, e.g. UNITY_VERSION=6000.3.22f1" >&2
    exit 1
fi

DOTNET="$UNITY_DATA/NetCoreRuntime/dotnet"
CSC="$UNITY_DATA/DotNetSdkRoslyn/csc.dll"
SCRIPT_ASSEMBLIES="$PROJECT_ROOT/Library/ScriptAssemblies"
EDITOR_MANAGED="$UNITY_DATA/Managed"

mkdir -p "$OUT_DIR"

# ---------------------------------------------------------------- template

TEMPLATE="$(ls -t "$PROJECT_ROOT"/Library/Bee/artifacts/*.dag/BinakayanRising.Gameplay.rsp 2>/dev/null | head -1 || true)"
if [[ -z "$TEMPLATE" ]]; then
    echo "error: no cached compiler response file found." >&2
    echo "       Open the project in Unity once so it generates" >&2
    echo "       Library/Bee/artifacts/*.dag/BinakayanRising.Gameplay.rsp, then re-run." >&2
    exit 1
fi

# Additional references our asmdefs declare that the cached response file predates.
EXTRA_REFS=(
    "$SCRIPT_ASSEMBLIES/Unity.TextMeshPro.dll"
    "$SCRIPT_ASSEMBLIES/UnityEngine.UI.dll"
    "$SCRIPT_ASSEMBLIES/Unity.RenderPipelines.Universal.Runtime.dll"
    "$SCRIPT_ASSEMBLIES/Unity.RenderPipelines.Universal.2D.Runtime.dll"
    "$SCRIPT_ASSEMBLIES/Unity.RenderPipelines.Core.Runtime.dll"
)

# Builds a response file: the template's flags and references, our sources, our output.
#   $1 assembly name   $2 output dll   $3.. source directories
write_rsp() {
    local name="$1"; shift
    local out="$1"; shift
    local rsp="$OUT_DIR/$name.rsp"

    {
        # Everything except the output paths, the analyzers and the old source list.
        # Analyzers are Unity's source generators; they need the editor's build context and
        # only emit code we do not depend on for a type check.
        grep -E '^-(r:|define:|langversion:|nullable:|unsafe|nostdlib|noconfig|deterministic|optimize|target:|warn|nowarn|debug)' "$TEMPLATE" \
            | grep -v '^-refout:' || true

        echo "-target:library"
        echo "-out:\"$out\""

        for ref in "${EXTRA_REFS[@]}"; do
            [[ -f "$ref" ]] && echo "-r:\"$ref\""
        done

        # Editor assemblies need TMP's editor half. They do NOT need Managed/UnityEditor.dll:
        # the template already carries the modular UnityEditor.*Module.dll set, and adding the
        # monolithic assembly on top makes every editor type ambiguous (CS0433).
        if [[ "$name" == *Editor* ]]; then
            for extra in "$SCRIPT_ASSEMBLIES"/Unity.TextMeshPro.Editor.dll; do
                [[ -f "$extra" ]] && echo "-r:\"$extra\""
            done
        fi

        # Sibling project assemblies, so UI can see Gameplay and Editor can see both.
        for sibling in "${SIBLINGS[@]:-}"; do
            [[ -n "$sibling" && -f "$sibling" ]] && echo "-r:\"$sibling\""
        done

        for dir in "$@"; do
            find "$dir" -name '*.cs' -type f | sort | sed 's|^|"|;s|$|"|'
        done
    } > "$rsp"

    [[ $VERBOSE -eq 1 ]] && { echo "--- $rsp ---"; cat "$rsp"; }
    echo "$rsp"
}

compile() {
    local label="$1" rsp="$2"
    printf '%-28s ' "$label"
    local log
    if log=$(cd "$PROJECT_ROOT" && "$DOTNET" "$CSC" -nologo -noconfig "@$rsp" 2>&1); then
        # Warnings are worth surfacing but are not a failure.
        local warnings
        warnings=$(grep -c 'warning CS' <<<"$log" || true)
        echo "ok${warnings:+  ($warnings warnings)}"
        [[ $VERBOSE -eq 1 && -n "$log" ]] && echo "$log"
        return 0
    fi
    echo "FAILED"
    grep -E 'error CS' <<<"$log" | sort -u | head -40
    return 1
}

# ---------------------------------------------------------------- run

SRC="$PROJECT_ROOT/Assets/_Project/Scripts"
status=0

SIBLINGS=()
rsp=$(write_rsp BinakayanRising.Gameplay "$OUT_DIR/BinakayanRising.Gameplay.dll" "$SRC/Gameplay")
compile "Gameplay" "$rsp" || status=1

SIBLINGS=("$OUT_DIR/BinakayanRising.Gameplay.dll")
rsp=$(write_rsp BinakayanRising.UI "$OUT_DIR/BinakayanRising.UI.dll" "$SRC/UI")
compile "UI" "$rsp" || status=1

SIBLINGS=("$OUT_DIR/BinakayanRising.Gameplay.dll" "$OUT_DIR/BinakayanRising.UI.dll")
rsp=$(write_rsp BinakayanRising.Editor "$OUT_DIR/BinakayanRising.Editor.dll" "$SRC/Editor")
compile "Editor" "$rsp" || status=1

exit $status
