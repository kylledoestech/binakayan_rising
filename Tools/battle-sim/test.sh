#!/usr/bin/env bash
# Compiles and runs every EditMode test with the Unity Editor closed.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/toolchain.sh"

if [[ -z "$NUNIT_DLL" ]]; then
    echo "error: nunit.framework.dll not found; open the project in Unity once to populate Library/PackageCache" >&2
    exit 1
fi

echo "== strict Core build (netstandard 2.1, no engine references, warnings as errors) =="
"$DOTNET" "$CSC" -nologo -noconfig -nostdlib+ -langversion:7.3 -warnaserror+ -warn:4 \
    -target:library -out:"$OUT_DIR/BinakayanRising.Core.dll" -r:"$NETSTANDARD_REF" \
    "$CORE_SRC"/Grid/*.cs "$CORE_SRC"/Combat/*.cs "$CORE_SRC"/Localization/*.cs \
    "$CORE_SRC"/Meta/*.cs "$CORE_SRC"/Content/*.cs
echo "   OK - Core compiles with zero warnings and zero UnityEngine dependencies"
echo

echo "== EditMode tests =="
# nunit.framework.dll must sit beside the exe for the runtime to resolve it.
cp -f "$NUNIT_DLL" "$OUT_DIR/"
compile_netfx "$OUT_DIR/EditModeTests.exe" \
    "$CORE_SRC"/Grid/*.cs \
    "$CORE_SRC"/Combat/*.cs \
    "$CORE_SRC"/Localization/*.cs \
    "$CORE_SRC"/Meta/*.cs \
    "$CORE_SRC"/Content/*.cs \
    "$TEST_SRC"/Grid/*.cs \
    "$TEST_SRC"/Combat/*.cs \
    "$TEST_SRC"/Localization/*.cs \
    "$TEST_SRC"/Meta/*.cs \
    "$TEST_SRC"/Content/*.cs \
    "$PROJECT_ROOT/Tools/battle-sim/OfflineNUnitRunner.cs"

exec "$MONO" "$OUT_DIR/EditModeTests.exe"
