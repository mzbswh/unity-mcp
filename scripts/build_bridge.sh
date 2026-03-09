#!/bin/bash
# Build unity-mcp-bridge for all supported platforms.
# Run this before publishing the UPM package.
#
# Prerequisites: .NET 8+ SDK (dotnet --version)
# Usage: ./scripts/build_bridge.sh [--current-only]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BRIDGE_DIR="$ROOT_DIR/unity-bridge"
PROJECT="$BRIDGE_DIR/unity-mcp-bridge.csproj"
# Output into the UPM package so git URL installs include the binary
PACKAGE_BRIDGE_DIR="$ROOT_DIR/unity-mcp/Bridge~"

if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK not found. Install from https://dotnet.microsoft.com/download"
    exit 1
fi

RIDS=("win-x64" "osx-x64" "osx-arm64" "linux-x64")

# --current-only: only build for the current platform
if [[ "${1:-}" == "--current-only" ]]; then
    case "$(uname -s)-$(uname -m)" in
        Darwin-arm64)  RIDS=("osx-arm64") ;;
        Darwin-x86_64) RIDS=("osx-x64") ;;
        Linux-x86_64)  RIDS=("linux-x64") ;;
        MINGW*|MSYS*)  RIDS=("win-x64") ;;
        *)             echo "Unknown platform: $(uname -s)-$(uname -m)"; exit 1 ;;
    esac
fi

echo "=== Building unity-mcp-bridge ==="
echo "Project: $PROJECT"
echo "Targets: ${RIDS[*]}"
echo ""

for rid in "${RIDS[@]}"; do
    OUT_DIR="$BRIDGE_DIR/bin/$rid"
    echo "[$rid] Building..."
    dotnet publish "$PROJECT" \
        -c Release \
        -r "$rid" \
        -o "$OUT_DIR" \
        --self-contained false \
        -p:PublishSingleFile=true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        /nologo -v quiet

    # Show result
    BINARY=$(ls "$OUT_DIR"/unity-mcp-bridge* 2>/dev/null | head -1)
    if [[ -n "$BINARY" ]]; then
        SIZE=$(du -h "$BINARY" | cut -f1)
        echo "[$rid] OK -> $BINARY ($SIZE)"

        # Copy into UPM package for git URL installs
        mkdir -p "$PACKAGE_BRIDGE_DIR/$rid"
        cp "$BINARY" "$PACKAGE_BRIDGE_DIR/$rid/"
        echo "[$rid] Copied to unity-mcp/Bridge~/$rid/"
    else
        echo "[$rid] FAILED - no binary produced"
        exit 1
    fi
done

echo ""
echo "=== Done ==="
echo "  Build output:   unity-bridge/bin/"
echo "  UPM package:    unity-mcp/Bridge~/"
