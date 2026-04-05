#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# build-merged.sh  —  Build Zatca.Phase2.Merged.dll (single-file distribution)
#
# Usage:
#   chmod +x build-merged.sh
#   ./build-merged.sh
#
# Output: bin/Release/net8.0/Zatca.Phase2.Merged.dll  (≈ 6 MB)
#
# Prerequisites:
#   dotnet SDK 8.0+
#   ilrepack global tool (installed automatically by this script if missing)
# ─────────────────────────────────────────────────────────────────────────────
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="$SCRIPT_DIR/bin/Release/net8.0"
MERGED="$OUT/Zatca.Phase2.Merged.dll"

echo "🔧  Checking ilrepack global tool..."
if ! command -v ilrepack &>/dev/null; then
    echo "📦  Installing dotnet-ilrepack..."
    dotnet tool install -g dotnet-ilrepack
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

echo "🏗️   Building in Release mode..."
dotnet build "$SCRIPT_DIR" --configuration Release --no-incremental

echo "🔗  Merging DLLs with ilrepack..."
ilrepack \
    --lib:"$OUT" \
    --out:"$MERGED" \
    "$OUT/Zatca.Phase2.dll" \
    "$OUT/ZATCA.EInvoice.SDK.dll" \
    "$OUT/ZATCA.EInvoice.SDK.Contracts.dll" \
    "$OUT/BouncyCastle.Crypto.dll"

SIZE=$(du -sh "$MERGED" | cut -f1)
echo ""
echo "✅  Done!"
echo "    📄  $MERGED"
echo "    📦  Size: $SIZE"
echo ""
echo "Deploy to your POS project:"
echo "  1. Copy $MERGED to your Libs/ folder"
echo "  2. Also copy the runtime dependencies alongside it:"
echo "       $OUT/IKVM.*.dll"
echo "       $OUT/Newtonsoft.Json.dll"
echo "       $OUT/saxon.dll"
echo "       $OUT/xerces.dll"
echo "  3. Add to your .csproj:"
echo '     <Reference Include="Zatca.Phase2">'
echo '       <HintPath>Libs/Zatca.Phase2.Merged.dll</HintPath>'
echo '     </Reference>'
