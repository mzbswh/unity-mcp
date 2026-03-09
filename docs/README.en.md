# Unity MCP

[![Unity 2021.2+](https://img.shields.io/badge/Unity-2021.2%2B-blue.svg)](https://unity.com/)
[![MCP Protocol](https://img.shields.io/badge/MCP-2024--11--05-green.svg)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Unity MCP** is a [Model Context Protocol](https://modelcontextprotocol.io/) server embedded in the Unity Editor, enabling AI assistants (Claude, Cursor, VS Code Copilot, Windsurf, etc.) to directly control and query your Unity project.

> AI reads your scene hierarchy, creates GameObjects, modifies materials, runs tests, captures screenshots — all through natural language.

## Features

- **60+ Editor Tools** — GameObject, Component, Scene, Asset, Material, Animation, Prefab, Script, UI, VFX, Package, Test, Screenshot, Console, and more
- **12 Resources** — Read-only data endpoints for scene hierarchy, project info, editor state, console logs, etc.
- **40+ Prompts** — Unity best-practice guides for architecture, scripting, performance, shaders, XR, ECS, networking, etc.
- **Batch Execute** — Run multiple tool operations in a single request with atomic rollback support
- **Runtime Mode** — Optional runtime MCP server for controlling the running game (performance stats, time scale, scene loading)
- **Dual Server Architecture** — Mode A (C# stdio Bridge) or Mode B (Python FastMCP) to suit your workflow
- **Multi-Instance** — Supports multiple Unity Editor instances simultaneously
- **Custom Tool API** — Add your own tools with simple C# attributes
- **Domain Reload Safe** — Survives Unity script recompilation without dropping connections

## Architecture

```
MCP Client (Claude/Cursor/...)
    |
    |  stdio (JSON-RPC 2.0)
    |
 [Mode A: C# Bridge]          [Mode B: Python FastMCP Server]
    |                               |
    |  TCP (custom frame)           |  TCP (custom frame)
    |                               |
Unity Editor (TCP Server + Tool Registry)
```

- **Mode A (Built-in)**: Lightweight C# bridge binary translates stdio <-> TCP. No Python needed.
- **Mode B (Python)**: Python FastMCP server with dynamic tool discovery. Adds local analysis tools (`analyze_script`, `validate_assets`).

## Quick Start

### 1. Install the Unity Package

**Option A — Git URL (recommended)**

In Unity: `Window > Package Manager > + > Add package from git URL`:

```
https://github.com/mzbswh/unity-mcp.git?path=unity-mcp
```

**Option B — Local clone**

```bash
git clone https://github.com/mzbswh/unity-mcp.git
```

Then in Unity: `Window > Package Manager > + > Add package from disk`, select `unity-mcp/package.json`.

### 2. Bridge Binary (Mode A)

The bridge binary is pre-built and bundled inside the UPM package (`Bridge~/` directory). **No extra steps needed when installing via Git URL.**

If you're developing locally or need to rebuild, install [.NET 8+ SDK](https://dotnet.microsoft.com/download):

```bash
./scripts/build_bridge.sh --current-only
```

Build output is automatically copied to `unity-mcp/Bridge~/`.

### 3. Configure Your MCP Client

In Unity: `Window > Unity MCP > Quick Setup`, click your client (Claude Code / Cursor / VS Code / Windsurf) to copy the config to clipboard.

**Claude Desktop** — paste into `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "unity": {
      "command": "/path/to/unity-mcp-bridge",
      "args": ["52345"],
      "env": {
        "UNITY_MCP_PORT": "52345"
      }
    }
  }
}
```

The port is auto-generated per project. Check `Window > Unity MCP` for the actual port.

### 4. Verify

Ask your AI assistant:

> "List all GameObjects in my Unity scene"

If it returns the scene hierarchy, you're all set.

## Available Tools

### Editor Tools

| Category | Tools | Description |
|----------|-------|-------------|
| **GameObject** | `gameobject_create`, `gameobject_destroy`, `gameobject_find`, `gameobject_modify`, `gameobject_set_parent`, `gameobject_duplicate`, `gameobject_get_components` | Create, find, modify, and manage GameObjects |
| **Component** | `component_add`, `component_remove`, `component_get`, `component_modify` | Add/remove/inspect/modify components and their properties |
| **Scene** | `scene_create`, `scene_open`, `scene_save`, `scene_get_hierarchy`, `scene_list_all` | Scene management and hierarchy inspection |
| **Asset** | `asset_find`, `asset_create_folder`, `asset_delete`, `asset_move`, `asset_copy`, `asset_refresh`, `asset_get_info` | AssetDatabase operations |
| **Material** | `material_create`, `material_modify`, `shader_list` | Create/modify materials and list shaders |
| **Script** | `script_create`, `script_read`, `script_update` | C# script CRUD operations |
| **Prefab** | `prefab_create`, `prefab_instantiate`, `prefab_open`, `prefab_save_close`, `prefab_unpack` | Prefab workflow |
| **Animation** | `animation_create_clip`, `animation_manage_controller` | AnimationClip and AnimatorController management |
| **UI** | `ui_create_element` | Create UI elements (Button, Text, Image, etc.) |
| **VFX** | `vfx_create_particle`, `vfx_modify_particle`, `vfx_create_graph`, `vfx_get_info` | Particle systems and VFX Graph |
| **Editor** | `editor_get_state`, `editor_set_playmode`, `editor_execute_menu`, `editor_selection_get`, `editor_selection_set` | Editor state and Play mode control |
| **Screenshot** | `screenshot_scene`, `screenshot_game` | Capture Scene/Game view as Base64 PNG |
| **Console** | `console_get_logs` | Read and filter Unity console logs |
| **Test** | `test_run`, `test_get_results` | Run EditMode/PlayMode tests |
| **Package** | `package_list`, `package_add` | UPM package management |
| **Batch** | `batch_execute` | Execute multiple tools atomically |
| **Instance** | `instance_list`, `instance_set_active` | Multi-instance management |
| **MPPM** | `editor_is_clone`, `editor_get_mppm_tags` | Multiplayer Play Mode support |

### Resources

| URI | Description |
|-----|-------------|
| `unity://scene/hierarchy` | Scene hierarchy tree |
| `unity://scene/list` | Scenes in Build Settings |
| `unity://project/info` | Project metadata |
| `unity://editor/state` | Editor state (play mode, platform, etc.) |
| `unity://console/logs` | Console log entries |
| `unity://gameobject/{id}` | Detailed GameObject info by instance ID |
| `unity://assets/search/{filter}` | Asset search results |
| `unity://packages/list` | Installed UPM packages |
| `unity://tests/{mode}` | Test list (EditMode/PlayMode) |
| `unity://tags` | Available tags |
| `unity://layers` | Available layers |
| `unity://menu/items` | Menu items |

### Prompts

40+ Unity best-practice prompts covering: script conventions, MonoBehaviour lifecycle, error handling, serialization, architecture patterns, ScriptableObjects, async programming, scene organization, asset naming, performance optimization, physics, input system, audio, AI navigation, networking, animation, UI Toolkit, shaders, testing, debugging, project setup, 2D/3D workflows, VFX, Addressables, CI/CD, mobile optimization, XR, ECS/DOTS, terrain, custom editors, render pipelines, multiplayer, procedural generation, inventory/dialogue systems, version control, Asset Bundles, editor automation, save systems, localization, dependency injection, event architecture, object pooling, state machines, camera systems, and lighting.

## Custom Tools

Add your own tools with simple C# attributes. Create a class with `[McpToolGroup]` and methods with `[McpTool]` — they're auto-discovered at startup.

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

Import the sample via `Package Manager > Unity MCP > Samples > Custom Tools Example` for a complete example.

### Attribute Reference

| Attribute | Target | Description |
|-----------|--------|-------------|
| `[McpToolGroup("name")]` | Class | Marks a class as containing MCP tools/resources/prompts |
| `[McpTool("name", "desc")]` | Method | Registers a method as an MCP tool. Options: `Group`, `ReadOnly`, `Idempotent`, `Title`, `AutoRegister` |
| `[McpResource("uri", "name", "desc")]` | Method | Registers a method as an MCP resource. Options: `MimeType` |
| `[McpPrompt("name", "desc")]` | Method | Registers a method as an MCP prompt |
| `[Desc("description")]` | Parameter | Adds a description to a tool/resource/prompt parameter |

## Runtime Mode (Experimental)

Control the running game via MCP. Add the scripting define `UNITY_MCP_RUNTIME` in `Player Settings > Scripting Define Symbols` to enable.

Runtime tools include:
- `runtime_get_stats` / `runtime_profiler_snapshot` — Performance monitoring
- `runtime_time_scale` — Pause, slow motion, fast forward
- `runtime_load_scene` — Load scenes at runtime
- `runtime_invoke` — Invoke methods on runtime objects
- `runtime_get_logs` — Read runtime logs
- `screenshot_game` / `screenshot_camera` — Capture runtime screenshots

The runtime server listens on `port + 1` (auto-detected from project path).

## Mode B: Python Server

For additional local analysis tools or custom Python integrations:

### Setup

```bash
cd unity-server
pip install -e .
# or with uv:
uv pip install -e .
```

### Configure

In Unity: `Window > Unity MCP`, set Server Mode to **Python**, configure the Python path and server script, then use Quick Setup to copy the MCP client config.

Python server adds two local tools:
- `analyze_script` — Static analysis of C# scripts for common issues
- `validate_assets` — Asset naming convention and folder structure validation

All Unity tools/resources/prompts are dynamically discovered and forwarded.

### Docker Deployment

Run the Python server in a container (useful for CI/CD or isolated environments):

**Using docker compose:**

```bash
cd unity-server

# Use default port 52345
docker compose up -d

# Specify a custom port
UNITY_MCP_PORT=53000 docker compose up -d
```

**Manual build and run:**

```bash
cd unity-server
docker build -t unity-mcp-server .
docker run -it --rm \
  -e UNITY_MCP_HOST=host.docker.internal \
  -e UNITY_MCP_PORT=52345 \
  --add-host=host.docker.internal:host-gateway \
  unity-mcp-server
```

**MCP client config (Docker mode):**

```json
{
  "mcpServers": {
    "unity": {
      "command": "docker",
      "args": ["run", "-i", "--rm",
        "-e", "UNITY_MCP_HOST=host.docker.internal",
        "-e", "UNITY_MCP_PORT=52345",
        "--add-host=host.docker.internal:host-gateway",
        "unity-mcp-server"
      ]
    }
  }
}
```

**Environment variables:**

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_MCP_HOST` | `host.docker.internal` | Unity Editor host address. Use `host.docker.internal` to reach the host from inside the container |
| `UNITY_MCP_PORT` | `52345` | Unity Editor TCP port. Must match the port shown in `Window > Unity MCP` |
| `UNITY_MCP_TIMEOUT` | `60` | Request timeout in seconds |

> **Note:** `host.docker.internal` works out of the box on Docker Desktop (macOS/Windows). On Linux, use `--add-host=host.docker.internal:host-gateway` (Docker 20.10+) or `--network=host`.

## Settings

Access via `Window > Unity MCP`:

| Setting | Default | Description |
|---------|---------|-------------|
| Server Mode | Built-in | `Built-in` (C# Bridge) or `Python` (FastMCP) |
| Port | Auto | TCP port (-1 = auto from project path hash) |
| Auto Start | On | Auto-start external server process (Python mode only) |
| Request Timeout | 60s | Max time for tool execution |
| Log Level | Info | Debug / Info / Warning / Error / Off |
| Audit Log | Off | Log every tool invocation with timing |

## Requirements

- **Unity** 2021.2 or later
- **Mode A**: [.NET 8+ SDK](https://dotnet.microsoft.com/download) (to build the bridge binary)
- **Mode B**: Python 3.10+ with `mcp>=1.0.0`
- **Dependency**: `com.unity.nuget.newtonsoft-json` 3.2.1+ (auto-resolved)

## Project Structure

```
unity-mcp/
├── unity-mcp/                  # UPM Package
│   ├── Editor/                 # Editor-only code
│   │   ├── Core/               # McpServer, TcpTransport, RequestHandler, ToolRegistry
│   │   ├── Tools/              # 60+ built-in tools
│   │   ├── Resources/          # 12 read-only resources
│   │   ├── Prompts/            # 40+ best-practice prompts
│   │   ├── Window/             # Settings UI
│   │   └── Utils/              # UndoHelper
│   ├── Runtime/                # Runtime mode (UNITY_MCP_RUNTIME)
│   │   ├── Core/               # RuntimeTcpTransport, RuntimeToolRegistry
│   │   ├── Tools/              # Runtime tools (stats, control, invoke)
│   │   └── Resources/          # Runtime resources
│   ├── Shared/                 # Shared between Editor & Runtime
│   │   ├── Attributes/         # [McpTool], [McpResource], [McpPrompt], [Desc]
│   │   ├── Models/             # ToolResult, McpConst, McpCapabilities
│   │   ├── Utils/              # ParameterBinder, JsonSchemaGenerator, SecurityChecker
│   │   └── Instance/           # Multi-instance discovery
│   ├── Samples~/               # Custom tool example
│   ├── Tests/                  # Editor & Runtime tests
│   └── package.json            # UPM manifest
├── unity-bridge/               # C# stdio-to-TCP bridge (.NET 8)
├── unity-server/               # Python FastMCP server
└── scripts/                    # Build scripts
```

## Troubleshooting

**Server not starting**
- Check `Window > Unity MCP` for status. Click Restart if needed.
- Check the Unity Console for `[MCP]` log messages.

**MCP client can't connect**
- Verify the port in your MCP client config matches `Window > Unity MCP`.
- For Mode A: ensure the bridge binary exists (`./scripts/build_bridge.sh --current-only`).
- For Mode B: ensure `UNITY_MCP_PORT` environment variable is set correctly.

**Tools not appearing**
- Ensure your tool class has `[McpToolGroup]` and methods have `[McpTool]`.
- Check that your script compiles without errors.
- Tools are scanned at startup; click Restart after adding new tools.

**Domain Reload disconnects**
- This is expected. The bridge auto-reconnects after Unity finishes recompiling.

## License

MIT
