#!/usr/bin/env bash
# Locates Unity's bundled Roslyn compiler and Mono runtime. No system dotnet or mono needed.
set -euo pipefail

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
MONO="$UNITY_DATA/MonoBleedingEdge/bin/mono"
NETFX_REF="$UNITY_DATA/UnityReferenceAssemblies/unity-4.8-api"
NETSTANDARD_REF="$UNITY_DATA/NetStandard/ref/2.1.0/netstandard.dll"

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CORE_SRC="$PROJECT_ROOT/Assets/_Project/Scripts/Core"
TEST_SRC="$PROJECT_ROOT/Assets/_Project/Tests/EditMode"
OUT_DIR="$PROJECT_ROOT/Tools/battle-sim/build"

NUNIT_DLL="$(find "$PROJECT_ROOT/Library/PackageCache" -name nunit.framework.dll -path '*net40*' 2>/dev/null | head -1 || true)"
if [[ -z "$NUNIT_DLL" ]]; then
    NUNIT_DLL="$(find "$UNITY_DATA/Resources/PackageManager/BuiltInPackages" -name nunit.framework.dll -path '*net40*' 2>/dev/null | head -1 || true)"
fi

mkdir -p "$OUT_DIR"

# csc invoked against the .NET Framework 4.8 reference set, which is what Unity's
# nunit.framework.dll targets. Core sources are compiled in directly rather than referenced,
# so there is no netstandard-versus-netfx mismatch to work around.
compile_netfx() {
    local out="$1"; shift
    "$DOTNET" "$CSC" -nologo -noconfig -nostdlib+ -langversion:7.3 -warn:4 -target:exe -out:"$out" \
        -r:"$NETFX_REF/mscorlib.dll" -r:"$NETFX_REF/System.dll" -r:"$NETFX_REF/System.Core.dll" \
        ${NUNIT_DLL:+-r:"$NUNIT_DLL"} "$@"
}
