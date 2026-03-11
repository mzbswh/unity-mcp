#!/usr/bin/env bash
set -euo pipefail

usage() { echo "Usage: $0 <unity|server> <version>"; exit 1; }
[[ $# -ne 2 ]] && usage

TARGET="$1"
VERSION="$2"

if [[ "$TARGET" == "unity" ]]; then
    sed -i.bak "s/\"version\": \".*\"/\"version\": \"$VERSION\"/" unity-mcp/package.json
    rm -f unity-mcp/package.json.bak
    sed -i.bak "s/ServerVersion = \".*\"/ServerVersion = \"$VERSION\"/" unity-mcp/Shared/Models/McpConst.cs
    rm -f unity-mcp/Shared/Models/McpConst.cs.bak
    echo "Updated Unity package version to $VERSION"
elif [[ "$TARGET" == "server" ]]; then
    sed -i.bak "s/^version = \".*\"/version = \"$VERSION\"/" unity-server/pyproject.toml
    rm -f unity-server/pyproject.toml.bak
    echo "Updated Python server version to $VERSION"
else
    usage
fi
