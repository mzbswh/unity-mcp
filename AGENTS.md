# Unity MCP — Agent Quick Reference

## Build & Run

```bash
# Python server
cd unity-server && pip install -e .
unity-mcp-server                    # runs via stdio by default

# Tests
pip install pytest pytest-asyncio
python -m pytest tests/ -v

# Version bump
scripts/bump-version.sh unity 1.1.0   # updates package.json + McpConst.cs
scripts/bump-version.sh server 1.1.0  # updates pyproject.toml
```

## Tool List (C# → Python)

All tools live in `unity-mcp/Editor/Tools/*.cs` and `unity-mcp/Runtime/Tools/*.cs`.
Each static class uses `[McpToolGroup]`, each method uses `[McpTool]`.

Key tool files: SceneTools, GameObjectTools, ComponentTools, MaterialTools, PrefabTools, AssetTools, ScriptTools, BuildTools, CodeExecutionTools, BatchExecuteTool, EditorTools, ConsoleTools, ScreenshotTools.

## Adding / Modifying Tools

1. Edit or create `*Tools.cs` in `Editor/Tools/` or `Runtime/Tools/`
2. Decorate: `[McpToolGroup("Group")]` on class, `[McpTool("name", "desc")]` on method
3. Return `ToolResult.Json(new { ... })` — do NOT include `success` field
4. Register Undo for all write operations
5. All Unity API calls must go through `MainThreadDispatcher`

## Key Files

| File | Purpose |
|------|---------|
| `unity-mcp/Editor/Core/McpServer.cs` | Server lifecycle |
| `unity-mcp/Editor/Core/TcpTransport.cs` | TCP connection to Python |
| `unity-mcp/Editor/Core/ToolRegistry.cs` | Reflection-based tool discovery |
| `unity-mcp/Editor/Core/McpSettings.cs` | Settings (ScriptableSingleton) |
| `unity-server/unity_mcp_server/server.py` | Python FastMCP entry |
| `unity-server/unity_mcp_server/unity_connection.py` | TCP client + domain-reload buffering |

## CI/CD

- Push/PR to `main` → `.github/workflows/ci.yml` runs Python tests
- Tag `server-v*` → `.github/workflows/release-server.yml` publishes to PyPI
