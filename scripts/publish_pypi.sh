#!/bin/bash
# Publish unity-mcp-server to PyPI.
#
# Prerequisites:
#   pip install build twine
#   PyPI API token configured (see below)
#
# Usage:
#   ./scripts/publish_pypi.sh              # publish to PyPI
#   ./scripts/publish_pypi.sh --test       # publish to TestPyPI first
#   ./scripts/publish_pypi.sh --dry-run    # build only, no upload
#
# PyPI token setup (one-time):
#   1. Create token at https://pypi.org/manage/account/token/
#   2. Save to ~/.pypirc:
#        [pypi]
#        username = __token__
#        password = pypi-xxxx...
#
#   For TestPyPI:
#        [testpypi]
#        username = __token__
#        password = pypi-xxxx...

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SERVER_DIR="$(cd "$SCRIPT_DIR/../unity-server" && pwd)"

# Parse args
REPO="pypi"
DRY_RUN=false
for arg in "$@"; do
    case "$arg" in
        --test)    REPO="testpypi" ;;
        --dry-run) DRY_RUN=true ;;
        *)         echo "Unknown option: $arg"; exit 1 ;;
    esac
done

# Check dependencies
for cmd in python3 pip; do
    if ! command -v "$cmd" &>/dev/null; then
        echo "Error: $cmd not found"
        exit 1
    fi
done

# Ensure build and twine are installed
python3 -m pip install --quiet build twine

cd "$SERVER_DIR"

# Read version from pyproject.toml
VERSION=$(python3 -c "
import tomllib
with open('pyproject.toml', 'rb') as f:
    print(tomllib.load(f)['project']['version'])
")

echo "=== Publishing unity-mcp-server v${VERSION} ==="
echo "  Source:  $SERVER_DIR"
echo "  Target:  $REPO"
echo ""

# Clean previous builds
rm -rf dist/ build/ *.egg-info

# Build
echo "[1/2] Building..."
python3 -m build --sdist --wheel 2>&1 | tail -3
echo ""

# List artifacts
echo "Artifacts:"
ls -lh dist/
echo ""

if $DRY_RUN; then
    echo "[dry-run] Skipping upload."
    exit 0
fi

# Upload
echo "[2/2] Uploading to $REPO..."
if [ "$REPO" = "testpypi" ]; then
    python3 -m twine upload --repository testpypi dist/*
    echo ""
    echo "=== Done ==="
    echo "  Test install: pip install -i https://test.pypi.org/simple/ unity-mcp-server"
    echo "  Test run:     uvx --index-url https://test.pypi.org/simple/ unity-mcp-server"
else
    python3 -m twine upload dist/*
    echo ""
    echo "=== Done ==="
    echo "  Install: pip install unity-mcp-server"
    echo "  Run:     uvx unity-mcp-server"
fi
