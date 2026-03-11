# Unity MCP — AI Assistant Guide

## Project Overview

Unity MCP enables AI assistants (Claude, Cursor, etc.) to control Unity Editor via the Model Context Protocol (MCP).

**Architecture:**

```
AI Client (Claude/Cursor)
  ↕ stdio or streamable-http (MCP JSON-RPC 2.0)
Python Server (unity-mcp-server, FastMCP)
  ↕ Custom TCP frame protocol (localhost)
Unity Editor Plugin (C#, TcpTransport)
  ↕ MainThreadDispatcher
Unity Engine APIs
```

**TCP Frame Format:** `[4-byte big-endian length][1-byte type][JSON payload]`
- `0x01` = Request, `0x02` = Response, `0x03` = Notification

## Directory Structure

```
unity-mcp/                    # Unity package (com.mzbswh.unity-mcp)
├── Editor/
│   ├── Core/                 # McpServer, McpSettings, TcpTransport, ToolRegistry, RequestHandler
│   ├── Tools/                # Built-in MCP tool implementations (26 files)
│   └── Window/               # Settings window (UI Toolkit)
├── Runtime/Tools/            # Runtime MCP tools (play mode)
├── Shared/
│   ├── Attributes/           # [McpTool], [McpResource], [McpToolGroup], [Desc] etc.
│   ├── Models/               # McpConst, ToolResult, JSON-RPC models
│   ├── Interfaces/           # ITransport etc.
│   └── Utils/                # PortResolver etc.
├── Samples~/CustomTools/     # Example custom tool
└── package.json              # Unity package manifest

unity-server/                 # Python MCP server (PyPI: unity-mcp-server)
├── unity_mcp_server/
│   ├── server.py             # FastMCP server entry, tool/resource registration
│   ├── unity_connection.py   # TCP connection to Unity + domain-reload buffering
│   ├── config.py             # Configuration
│   └── tools/                # Python-side tool wrappers
├── tests/                    # pytest tests
├── pyproject.toml            # Package metadata
└── Dockerfile                # Docker support
```

## Key Invariants

1. **Main thread only**: All Unity API calls MUST go through `MainThreadDispatcher`. Never call Unity APIs from TCP read threads.
2. **Domain reload**: When Unity recompiles, the TCP connection drops. Python server buffers requests in `asyncio.Queue` and drains them after reconnection.
3. **ToolResult return**: All MCP tool methods return `ToolResult` (via `ToolResult.Json(...)` or `ToolResult.Text(...)`). Do NOT include a `success` field in the anonymous object — the framework handles error signaling.
4. **Undo registration**: All write operations (create/modify/destroy GameObjects) MUST register with `Undo.RegisterCreatedObjectUndo`, `Undo.RecordObject`, or `Undo.DestroyObjectImmediate`.
5. **Version sync**: Unity package version (`package.json`) and `McpConst.ServerVersion` must match. Use `scripts/bump-version.sh unity <ver>`.

## Adding a New MCP Tool

1. Create or open a `*Tools.cs` file in `unity-mcp/Editor/Tools/` (or `Runtime/Tools/` for play-mode tools)
2. Add the `[McpToolGroup("YourGroup")]` attribute to the static class
3. Add a `public static ToolResult` method with `[McpTool("tool_name", "description")]`
4. Use `[Desc("...")]` on parameters for documentation
5. Return `ToolResult.Json(new { ... })` — no `success` field
6. The tool is auto-discovered by `ToolRegistry` via reflection

See `Samples~/CustomTools/MyCustomToolExample.cs` for a complete example.

## Common Pitfalls

- **Empty catch blocks**: Always log exceptions, at minimum with `McpLogger.Debug(...)`. Never silently swallow.
- **Forgetting Undo**: Creates/destroys without Undo registration break Ctrl+Z for users.
- **Blocking the main thread**: Long operations should yield or use async patterns through MainThreadDispatcher.
- **TCP port conflicts**: Default port is resolved by `PortResolver`. Don't hardcode ports.
- **Python version mismatch**: pyproject.toml `version` must match the git tag for PyPI release (`server-v*`).

## Code Conventions

- **C#**: Follow existing patterns — static classes for tools, attributes for metadata, namespace `UnityMcp.Editor.Tools` / `UnityMcp.Runtime.Tools`
- **Python**: Standard Python conventions, `asyncio` for async, `pytest` for tests
- **Commits**: Conventional commits (`feat:`, `fix:`, `refactor:`, `ci:`, `docs:`)
- **Unity version**: Minimum 2021.2 (UI Toolkit support)

## Build & Test

```bash
# Python server tests
cd unity-server && pip install -e . && pip install pytest pytest-asyncio
python -m pytest tests/ -v

# Version bump
scripts/bump-version.sh unity 1.1.0
scripts/bump-version.sh server 1.1.0
```
