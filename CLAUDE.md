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
│   ├── Core/                 # McpServer, McpSettings, TcpTransport, ToolRegistry, RequestHandler, McpServices, ToolCallLogger, PackageUpdateChecker, DependencyChecker
│   ├── Tools/                # Built-in MCP tool implementations (32 files, includes ShaderTools, UIToolkitTools, GraphicsTools, ProBuilderTools)
│   └── Window/               # Settings window (UI Toolkit) + McpSetupWindow (dependency wizard)
├── Runtime/Tools/            # Runtime MCP tools (play mode)
├── Shared/
│   ├── Attributes/           # [McpTool], [McpResource], [McpToolGroup], [Desc] etc.
│   ├── Models/               # McpConst, ToolResult, JSON-RPC models
│   ├── Interfaces/           # ITransport etc.
│   └── Utils/                # PortResolver, PaginationHelper etc.
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

## Available Tools by Category

### Scene
`scene_create`, `scene_open`, `scene_save`, `scene_get_hierarchy`

### GameObject
`gameobject_create`, `gameobject_destroy`, `gameobject_find` (paginated), `gameobject_get_info`, `gameobject_modify`, `gameobject_duplicate`, `gameobject_set_parent`, `gameobject_add_component`, `gameobject_remove_component`, `gameobject_get_component`, `gameobject_modify_component`, `gameobject_copy_component`

### Asset
`asset_find` (paginated), `asset_create_folder`, `asset_move`, `asset_delete`, `asset_get_info`

### Editor
`editor_get_state`, `editor_set_playmode`, `editor_execute_menu`, `editor_selection_get`, `editor_selection_set`, `editor_refresh`, `editor_get_compile_status`, `editor_undo`, `editor_redo`, `editor_open_window`

### Camera
`camera_create`, `camera_configure`, `camera_get_info`, `camera_look_at`

### Material
`material_create`, `material_modify`, `material_get_info`

### Prefab
`prefab_create`, `prefab_instantiate`, `prefab_unpack`, `prefab_apply`

### ScriptableObject
`so_create`, `so_read`, `so_modify`, `so_list`

### Texture
`texture_get_info`, `texture_search` (paginated)

### Screenshot
`screenshot_scene`, `screenshot_game` — supports `maxResolution` for AI-friendly downsampling (recommended: 640-1024)

### Animation
`animation_create_clip`, `animation_add_keyframe`

### Lighting
`lighting_bake`, `lighting_cancel_bake`

### Physics
`physics_create_material`, `physics_get_settings`, `physics_set_gravity`, `physics_raycast`

### Build
`build_player`, `build_get_settings`

### Shader
`shader_info`, `shader_list`

### UI Toolkit
`uitoolkit_create`, `uitoolkit_list`, `uitoolkit_attach`, `uitoolkit_get_visual_tree`

### Graphics
`graphics_get_skybox`, `graphics_set_skybox`, `graphics_get_fog`, `graphics_set_fog`, `graphics_get_ambient`, `graphics_set_ambient`, `graphics_get_render_pipeline`, `graphics_get_quality`, `graphics_set_quality`, `graphics_get_stats`, `graphics_get_lightmap_settings`

### ProBuilder (conditional: PROBUILDER_ENABLED)
`probuilder_create_shape`, `probuilder_get_mesh_info`, `probuilder_extrude_faces`, `probuilder_set_material`

### Code Execution
`execute_code`

## Available Resources

- `unity://editor/state` — Editor state (compiling, playing, scene, selection, platform)
- `unity://editor/selection` — Detailed info about currently selected objects
- `unity://project/info` — Project metadata, render pipeline, packages
- `unity://scene/hierarchy` — Scene hierarchy tree
- `unity://scene/list` — Build scenes and loaded scenes
- `unity://console/logs` — Recent console logs
- `unity://server/status` — Python server connection status

## Build & Test

```bash
# Python server tests
cd unity-server && pip install -e . && pip install pytest pytest-asyncio
python -m pytest tests/ -v

# Version bump
scripts/bump-version.sh unity 1.1.0
scripts/bump-version.sh server 1.1.0
```
