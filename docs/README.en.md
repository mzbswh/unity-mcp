<div align="center">

# Unity MCP

[![Unity 2021.2+](https://img.shields.io/badge/Unity-2021.2%2B-000000?style=flat&logo=unity&logoColor=white)](https://unity.com/)
[![MCP Protocol](https://img.shields.io/badge/MCP-2024--11--05-4A90D9?style=flat)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](../LICENSE)

**Let AI assistants control your Unity Editor**

English | [中文](../README.md)

</div>

**Unity MCP** is a [Model Context Protocol](https://modelcontextprotocol.io/) server embedded in the Unity Editor. Once installed, AI assistants like Claude, Cursor, VS Code Copilot, and Windsurf can read scenes, create GameObjects, modify materials, run tests, and capture screenshots — all through natural language.

```
MCP Client (Claude/Cursor/VS Code/Windsurf)
        ↕  stdio / streamable-http (JSON-RPC 2.0)
  Python FastMCP Server (dynamic tool discovery)
        ↕  TCP (custom frame protocol)
  Unity Editor (TCP server + tool registry)
```

---

## Quick Start

Get up and running in 3 steps.

### 1. Install the Unity Package

**Git URL (recommended)** — In Unity: `Window > Package Manager > + > Add package from git URL`:

```
https://github.com/mzbswh/unity-mcp.git?path=unity-mcp
```

<details>
<summary>Other install methods</summary>

**Local clone**

```bash
git clone https://github.com/mzbswh/unity-mcp.git
```

In Unity: `Window > Package Manager > + > Add package from disk`, select `unity-mcp/package.json`.

</details>

### 2. One-Click Client Configuration

Open `Window > Unity MCP`, switch to the **Clients** tab, and click **Configure** next to your client.

Supported clients:

| Client | Config Location | Scope |
|--------|----------------|-------|
| **Claude Code** | `~/.claude.json` | Per-project |
| **Cursor** | `.cursor/mcp.json` | Project-level |
| **VS Code / Copilot** | `.vscode/mcp.json` | Project-level |
| **Windsurf** | `~/.codeium/windsurf/mcp_config.json` | Global |

> For other clients, use **Copy Config to Clipboard** and paste manually.

MCP client config example (Cursor):

```json
{
  "mcpServers": {
    "unity": {
      "command": "uvx",
      "args": ["unity-mcp-server"]
    }
  }
}
```

> `uvx` automatically downloads and runs from PyPI — no manual install needed. You can also `pip install unity-mcp-server` and run `unity-mcp-server` directly.
> Default port is 51279. For multi-instance setups, specify `UNITY_MCP_PORT` via the `env` field.

### 3. Verify

Ask your AI assistant:

> "List all GameObjects in my Unity scene"

If it returns the scene hierarchy, you're all set.

---

## Features

- **190 Editor Tools** — GameObject, Component, Scene, Asset, Material, Animation, Prefab, Script, UI Toolkit, VFX, Audio, Camera, Graphics, Lighting, NavMesh, Physics, Terrain, Shader, Texture, Build, Package, Test, Screenshot, Console, ProBuilder, and more
- **12 Python-Side Tools** — Script analysis, asset validation, PSD parsing/export/flatten/UI generation, Lanhu design fetching/slice downloading (runs without Unity connection)
- **13 Resource Endpoints** — Read-only data queries (scene hierarchy, project info, editor state, console logs, current selection, etc.)
- **48 Prompt Templates** — Unity best-practice guides (architecture, scripting, performance, shaders, XR, ECS, networking, etc.)
- **PSD → UI Workflow** — Parse PSD/PSB file structure, export layer images, auto-generate Unity UI hierarchy
- **Lanhu Integration** — Fetch Lanhu design lists, download design images for AI analysis, batch-download slices into project
- **Batch Execute** — Run multiple tool operations in a single request with atomic rollback
- **Runtime Mode** — Optional runtime MCP server for controlling the running game (8 runtime tools)
- **Dynamic Tool Discovery** — Python server discovers and registers all tools/resources/prompts from Unity at startup
- **Multi-Instance** — Supports multiple Unity Editor instances simultaneously
- **Custom Tool API** — Add your own tools with C# attributes, auto-discovered at startup
- **Domain Reload Safe** — Automatically reconnects after Unity script recompilation; Python server buffers and replays in-flight requests, making reloads nearly transparent to MCP clients
- **Dependency Detection** — First-run check for Python/uv/uvx environment with guided installation
- **Update Checker** — Daily automatic check for new versions, shown in settings window
- **Call Diagnostics** — Logs recent tool calls with name, duration, and success/failure status

---

## Tools Overview

<details>
<summary><b>GameObject & Component (15 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `gameobject_create` | Create GameObject |
| `gameobject_destroy` | Delete GameObject |
| `gameobject_find` | Find by name/path |
| `gameobject_modify` | Modify properties (name, tag, layer, active state) |
| `gameobject_set_parent` | Set parent-child relationship |
| `gameobject_duplicate` | Duplicate GameObject |
| `gameobject_get_components` | Get component list |
| `gameobject_look_at` | Look at target |
| `gameobject_move_relative` | Move relative |
| `gameobject_set_sibling_index` | Set sibling order |
| `component_add` | Add component |
| `component_remove` | Remove component |
| `component_get` | View component properties |
| `component_modify` | Modify component properties |
| `component_copy_values` | Copy component values |

</details>

<details>
<summary><b>Scene (15 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `scene_create` / `scene_open` / `scene_save` | Scene management |
| `scene_get_hierarchy` / `scene_list_all` | Hierarchy viewing |
| `scene_align_with_view` / `scene_move_to_view` | View alignment |
| `scene_frame_selected` | Frame selected object |
| `scene_view_get` / `scene_view_set` | Scene view position control |
| `scene_view_get_settings` / `scene_view_set_settings` | Scene view settings |
| `game_view_get_settings` / `game_view_set_settings` | Game view settings |
| `scene_view_snap_angle` | Snap to preset angle |

</details>

<details>
<summary><b>Asset (10 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `asset_find` / `asset_get_info` | Asset search and info |
| `asset_create_folder` / `asset_delete` / `asset_move` / `asset_copy` | Asset file operations |
| `asset_refresh` | Refresh AssetDatabase |
| `asset_set_import_settings` / `asset_set_model_import` | Import settings |
| `asset_find_references` | Find references |

</details>

<details>
<summary><b>Material & Shader (13 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `material_create` / `material_modify` | Material creation and modification |
| `material_set_render_mode` | Set render mode |
| `material_get_keywords` / `material_set_keywords` | Material keyword management |
| `shader_list` / `shader_get_properties` | Shader queries |
| `shader_create` / `shader_read` / `shader_update` / `shader_delete` | Shader CRUD |
| `shader_info` | Shader detailed info |

</details>

<details>
<summary><b>Script (3 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `script_create` / `script_read` / `script_update` | C# script CRUD |

</details>

<details>
<summary><b>Prefab (10 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `prefab_create` / `prefab_instantiate` | Prefab workflow |
| `prefab_open` / `prefab_save_close` | Prefab editing mode |
| `prefab_get_hierarchy` / `prefab_get_stage_objects` | Prefab hierarchy viewing |
| `prefab_modify_contents` | Modify prefab contents |
| `prefab_apply_overrides` / `prefab_revert_overrides` | Override management |
| `prefab_unpack` | Unpack prefab |

</details>

<details>
<summary><b>Animation (6 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `animation_create_clip` | Create animation clip |
| `animation_manage_controller` | Manage Animator Controller |
| `animation_add_transition` | Add state transition |
| `animation_add_layer` | Add animation layer |
| `animation_create_blend_tree` | Create blend tree |
| `animation_set_clip_curve` | Set animation curve |

</details>

<details>
<summary><b>UI Toolkit (7 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `uitoolkit_create` / `uitoolkit_read` / `uitoolkit_update` / `uitoolkit_delete` | UXML/USS CRUD |
| `uitoolkit_list` | List UI assets |
| `uitoolkit_attach` | Attach UIDocument |
| `uitoolkit_get_visual_tree` | Get runtime visual tree |

</details>

<details>
<summary><b>VFX & Audio (12 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `vfx_create_particle` / `vfx_modify_particle` | Particle systems |
| `vfx_create_graph` / `vfx_get_info` | VFX Graph |
| `vfx_create_line` / `vfx_modify_line` / `vfx_create_trail` | Lines and trails |
| `audio_create_source` / `audio_modify_source` / `audio_get_source_info` | Audio source management |
| `audio_set_clip_import` | Audio import settings |
| `audio_create_listener` | Audio listener |

</details>

<details>
<summary><b>Graphics & Lighting (21 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `graphics_get_skybox` / `graphics_set_skybox` | Skybox |
| `graphics_get_fog` / `graphics_set_fog` | Fog |
| `graphics_get_ambient` / `graphics_set_ambient` | Ambient lighting |
| `graphics_get_render_pipeline` | Render pipeline info |
| `graphics_get_quality` / `graphics_set_quality` | Quality settings |
| `graphics_get_stats` | Graphics stats |
| `graphics_bake_lighting` / `graphics_get_lightmap_settings` | Lightmap baking |
| `light_create` / `light_modify` / `light_get_info` | Light management |
| `lighting_get_environment` / `lighting_set_environment` | Environment lighting |
| `lighting_bake` / `lighting_get_bake_status` / `lighting_cancel_bake` | Bake control |
| `light_create_probe` | Light probe |

</details>

<details>
<summary><b>Camera (4 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `camera_create` | Create camera |
| `camera_configure` | Configure camera parameters |
| `camera_get_info` | Get camera info |
| `camera_look_at` | Point camera at target |

</details>

<details>
<summary><b>Physics & NavMesh (13 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `physics_add_rigidbody` / `physics_add_collider` | Physics components |
| `physics_create_material` | Physics material |
| `physics_raycast` | Raycast |
| `physics_get_settings` / `physics_set_gravity` | Physics settings |
| `navmesh_add_agent` / `navmesh_modify_agent` | Navigation agents |
| `navmesh_add_obstacle` / `navmesh_add_surface` | NavMesh obstacles and surfaces |
| `navmesh_bake` / `navmesh_clear` | NavMesh baking |
| `navmesh_get_info` | NavMesh info |

</details>

<details>
<summary><b>Terrain (6 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `terrain_create` | Create terrain |
| `terrain_get_info` | Get terrain info |
| `terrain_set_height` / `terrain_flatten` | Height editing |
| `terrain_add_layer` | Add terrain layer |
| `terrain_add_tree` | Add trees |

</details>

<details>
<summary><b>Texture (3 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `texture_get_info` | Get texture info |
| `texture_set_import` | Modify texture import settings |
| `texture_search` | Search texture assets |

</details>

<details>
<summary><b>PSD & Lanhu (11 tools, Python-side)</b></summary>

| Tool | Description |
|------|-------------|
| `psd_summary` | Get PSD/PSB file summary (dimensions, layer count, color mode, etc.) |
| `psd_layer_detail` | Get detailed layer info (hierarchy, blend modes, visibility, etc.) |
| `psd_parse` | Parse PSD/PSB and return full layer tree (optionally export images) |
| `psd_export_images` | Export all visible image layers as PNG (returns export list only) |
| `psd_to_image` | Flatten PSD/PSB into a single PNG/JPG image (supports resizing) |
| `psd_to_ui` | Full PSD to Unity UI workflow (parse + export + generate UI) |
| `lanhu_set_cookie` | Set Lanhu authentication cookie |
| `lanhu_get_designs` | Get Lanhu project design list |
| `lanhu_analyze_design` | Download Lanhu design image for AI analysis |
| `lanhu_get_slices` | Get Lanhu design slice list |
| `lanhu_download_slices` | Batch-download Lanhu slices to Unity project |

</details>

<details>
<summary><b>Editor & Utility (50+ tools)</b></summary>

| Tool | Description |
|------|-------------|
| `editor_get_state` / `editor_set_playmode` | Editor state control |
| `editor_execute_menu` | Execute menu commands |
| `editor_selection_get` / `editor_selection_set` | Selection management |
| `editor_undo` / `editor_redo` | Undo/Redo |
| `editor_open_window` | Open editor window |
| `editor_refresh` | Refresh AssetDatabase |
| `editor_get_compile_status` | Script compile status |
| `screenshot_scene` / `screenshot_game` | Scene/Game view screenshots (returns MCP images) |
| `console_get_logs` | Console logs |
| `test_run` / `test_get_results` | Test runner |
| `package_list` / `package_add` / `package_remove` / `package_search` / `package_get_info` | UPM package management |
| `build_player` / `build_get_settings` / `build_set_scenes` / `build_switch_platform` | Build management |
| `build_get_player_settings` / `build_set_player_settings` | Player Settings |
| `settings_get_tags` / `settings_add_tag` / `settings_get_layers` / `settings_add_layer` | Tag and layer management |
| `settings_get_sorting_layers` / `settings_add_sorting_layer` | Sorting layers |
| `settings_get_quality` / `settings_set_quality` | Quality levels |
| `settings_get_time` / `settings_set_time` | Time settings |
| `so_create` / `so_get` / `so_modify` / `so_list_types` | ScriptableObject management |
| `code_execute` / `code_validate` | C# code execution |
| `batch_execute` | Atomic batch execution |
| `instance_list` / `instance_set_active` | Multi-instance management |
| `editor_is_clone` / `editor_get_mppm_tags` | MPPM support |
| `probuilder_create_shape` / `probuilder_get_mesh_info` / `probuilder_extrude_faces` / `probuilder_set_material` | ProBuilder modeling |

</details>

<details>
<summary><b>Resources (13 endpoints)</b></summary>

| URI | Description |
|-----|-------------|
| `unity://scene/hierarchy` | Scene hierarchy tree |
| `unity://scene/list` | Scenes in Build Settings |
| `unity://project/info` | Project metadata |
| `unity://editor/state` | Editor state |
| `unity://editor/selection` | Current selection |
| `unity://console/logs` | Console logs |
| `unity://gameobject/{id}` | GameObject details |
| `unity://assets/search/{filter}` | Asset search |
| `unity://packages/list` | Installed UPM packages |
| `unity://tests/{mode}` | Test list |
| `unity://tags` | Tag list |
| `unity://layers` | Layer list |
| `unity://menu/items` | Menu items |

</details>

---

## Custom Tools

Add custom tools with C# attributes — auto-discovered and registered at startup. The `Window > Unity MCP > Tools` tab shows **Built-in** and **Custom** tools separately.

```csharp
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

[McpToolGroup("MyProject.Tools")]
public static class MyTools
{
    [McpTool("my_hello", "A simple greeting tool", ReadOnly = true)]
    public static ToolResult Hello(
        [Desc("Your name")] string name = "World")
    {
        return ToolResult.Json(new { message = $"Hello, {name}!" });
    }

    [McpResource("unity://custom/status", "Custom Status",
        "Project-specific status resource")]
    public static ToolResult GetStatus()
    {
        return ToolResult.Json(new
        {
            projectName = Application.productName,
            objectCount = Object.FindObjectsByType<GameObject>(
                FindObjectsSortMode.None).Length
        });
    }
}
```

Import the full example via `Package Manager > Unity MCP > Samples > Custom Tools Example`.

### Attribute Reference

| Attribute | Target | Description |
|-----------|--------|-------------|
| `[McpToolGroup("name")]` | Class | Marks a class as an MCP tool/resource/prompt container |
| `[McpTool("name", "desc")]` | Method | Registers as MCP tool. Options: `Group`, `ReadOnly`, `Idempotent`, `Title`, `AutoRegister` |
| `[McpResource("uri", "name", "desc")]` | Method | Registers as MCP resource. Options: `MimeType` |
| `[McpPrompt("name", "desc")]` | Method | Registers as MCP prompt |
| `[Desc("description")]` | Parameter | Adds description to parameter |

---

## Architecture

```
unity-mcp/
├── unity-mcp/                  # UPM Package (com.mzbswh.unity-mcp)
│   ├── Editor/
│   │   ├── Core/               # McpServer, TcpTransport, RequestHandler, ToolRegistry
│   │   │                         ToolCallLogger, DependencyChecker, PackageUpdateChecker
│   │   ├── Tools/              # 190 built-in tools (33 tool files)
│   │   ├── Resources/          # 13 read-only resources
│   │   ├── Prompts/            # 48 best-practice prompts
│   │   ├── Utils/              # Editor utilities
│   │   └── Window/             # Settings UI, client configuration
│   ├── Runtime/                # Runtime mode (requires UNITY_MCP_RUNTIME define)
│   ├── Shared/                 # Code shared between Editor and Runtime
│   │   ├── Attributes/         # [McpTool], [McpResource], [McpPrompt], [Desc]
│   │   ├── Models/             # ToolResult, McpConst, McpCapabilities, Pagination
│   │   ├── Interfaces/         # IToolRegistry, ITcpTransport, IMainThreadDispatcher
│   │   ├── Instance/           # Multi-instance discovery (InstanceDiscovery)
│   │   └── Utils/              # PaginationHelper, ParameterBinder, JsonSchemaGenerator
│   ├── Samples~/               # Custom tool examples
│   └── Tests/                  # EditMode tests (9 test files)
├── unity-server/               # Python FastMCP server (PyPI: unity-mcp-server)
│   ├── unity_mcp_server/
│   │   ├── server.py           # FastMCP entry point + dynamic tool discovery
│   │   ├── unity_connection.py # TCP connection management + auto-reconnect
│   │   ├── config.py           # Environment variable configuration
│   │   └── tools/              # Python-side tools (PSD parser, Lanhu integration, script analyzer, etc.)
│   ├── pyproject.toml          # PyPI package configuration
│   ├── Dockerfile              # Docker deployment
│   └── docker-compose.yml
└── scripts/                    # bump-version.sh version management
```

### How It Works

The Unity plugin starts a TCP server when the Editor launches, scanning all methods with `[McpTool]`/`[McpResource]`/`[McpPrompt]` attributes and registering them. The MCP client launches the Python FastMCP server via stdio, which connects to Unity's TCP port and uses a `discover` command to fetch all tool/resource/prompt definitions, dynamically registering them with FastMCP. All subsequent MCP calls flow through Python → TCP → Unity main thread.

---

## Runtime Mode (Experimental)

Control a running game via MCP. Add `UNITY_MCP_RUNTIME` to `Player Settings > Scripting Define Symbols` to enable.

Runtime tools (8): `runtime_get_stats` / `runtime_time_scale` / `runtime_load_scene` / `runtime_invoke` / `runtime_get_logs` / `runtime_profiler_snapshot` / `screenshot_game` / `screenshot_camera`

Runtime server listens on `port + 1`.

---

## Docker Deployment

<details>
<summary><b>Docker Configuration</b></summary>

```bash
cd unity-server

# stdio mode (default)
docker compose up -d

# Streamable HTTP mode
UNITY_MCP_TRANSPORT=streamable-http docker compose up -d

# manual
docker build -t unity-mcp-server .
docker run -it --rm \
  -e UNITY_MCP_HOST=host.docker.internal \
  -e UNITY_MCP_PORT=51279 \
  --add-host=host.docker.internal:host-gateway \
  unity-mcp-server
```

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_MCP_HOST` | `127.0.0.1` | Unity Editor host address |
| `UNITY_MCP_PORT` | `51279` | Unity Editor TCP port |
| `UNITY_MCP_TIMEOUT` | `60` | Request timeout (seconds) |
| `UNITY_MCP_TRANSPORT` | `stdio` | Transport mode: `stdio` or `streamable-http` |
| `UNITY_MCP_HTTP_PORT` | `8080` | HTTP port (only for `streamable-http` mode) |

</details>

---

## Settings

Access via `Window > Unity MCP`:

| Setting | Default | Description |
|---------|---------|-------------|
| Port | 51279 | TCP port, change for multi-instance setups |
| Auto Start | On | Auto-start MCP server when Unity opens |
| Request Timeout | 60s | Max tool execution timeout |
| Log Level | Info | Debug / Info / Warning / Error / Off |
| Audit Log | Off | Log each tool call with timing |
| Max Batch Operations | 50 | Maximum operations allowed per `batch_execute` call |

---

## Troubleshooting

<details>
<summary><b>Server not starting</b></summary>

- Check the status indicator in `Window > Unity MCP`. Green = running.
- Click **Restart**.
- Check Unity Console for `[MCP]` log messages.
- On first run, if prompted about missing Python/uv, follow the guided installation.

</details>

<details>
<summary><b>MCP client can't connect</b></summary>

- Verify the port in client config matches `Window > Unity MCP`.
- Ensure Python 3.10+ and `uvx` are installed (run `uvx --version` to check).
- For multi-instance setups, ensure `UNITY_MCP_PORT` environment variable is correct.
- Check the Clients tab for **Configured** status.

</details>

<details>
<summary><b>Custom tools not showing</b></summary>

- Ensure your class has `[McpToolGroup]` and methods have `[McpTool]`.
- Check that scripts compile without errors.
- Tools are scanned at startup; click **Restart** after adding new ones.
- Check the **Custom** section in the Tools tab.

</details>

<details>
<summary><b>Domain reload causes disconnection</b></summary>

This is expected behavior. The TCP connection briefly drops during Unity script recompilation, but it's handled automatically:

1. Unity sends a `notifications/reloading` notification before domain reload
2. After TCP disconnects, Python server enters exponential backoff reconnection (0s → 1s → 2s → 4s → ...)
3. Requests from the MCP client during reconnection are buffered
4. Once Unity finishes recompilation and TCP recovers, buffered requests are replayed

The entire process is nearly transparent to MCP clients, typically recovering within 2-5 seconds.

</details>

---

## Requirements

- **Unity** 2021.2+
- **Python** 3.10+ (recommend `uvx` for automatic dependency management)
- **Unity Dependency**: `com.unity.nuget.newtonsoft-json` 3.2.1+ (auto-resolved)
- **Python Dependencies** (auto-installed): `mcp`, `psd-tools` (PSD parsing), `httpx` (Lanhu integration)

## License

[MIT](../LICENSE)
