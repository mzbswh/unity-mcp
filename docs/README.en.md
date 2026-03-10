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
        ↕  stdio (JSON-RPC 2.0)
  C# Bridge / Python FastMCP
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

### 3. Verify

Ask your AI assistant:

> "List all GameObjects in my Unity scene"

If it returns the scene hierarchy, you're all set.

---

## Features

- **60+ Editor Tools** — GameObject, Component, Scene, Asset, Material, Animation, Prefab, Script, UI, VFX, Package, Test, Screenshot, Console
- **12 Resource Endpoints** — Read-only data queries (scene hierarchy, project info, editor state, console logs, etc.)
- **40+ Prompt Templates** — Unity best-practice guides (architecture, scripting, performance, shaders, XR, ECS, networking, etc.)
- **Batch Execute** — Run multiple tool operations in a single request with atomic rollback
- **Runtime Mode** — Optional runtime MCP server for controlling the running game
- **Dual Server Architecture** — Mode A (C# stdio Bridge, lightweight) or Mode B (Python FastMCP, extra analysis tools)
- **Multi-Instance** — Supports multiple Unity Editor instances simultaneously
- **Custom Tool API** — Add your own tools with C# attributes, auto-discovered at startup
- **Domain Reload Safe** — Automatically reconnects after Unity script recompilation; Bridge buffers and replays in-flight requests, making reloads nearly transparent to MCP clients

---

## Tools Overview

<details>
<summary><b>GameObject & Component</b></summary>

| Tool | Description |
|------|-------------|
| `gameobject_create` | Create GameObject |
| `gameobject_destroy` | Delete GameObject |
| `gameobject_find` | Find by name/path |
| `gameobject_modify` | Modify properties (name, tag, layer, active state) |
| `gameobject_set_parent` | Set parent-child relationship |
| `gameobject_duplicate` | Duplicate GameObject |
| `gameobject_get_components` | Get component list |
| `component_add` | Add component |
| `component_remove` | Remove component |
| `component_get` | View component properties |
| `component_modify` | Modify component properties |

</details>

<details>
<summary><b>Scene & Asset</b></summary>

| Tool | Description |
|------|-------------|
| `scene_create` / `scene_open` / `scene_save` | Scene management |
| `scene_get_hierarchy` / `scene_list_all` | Hierarchy viewing |
| `asset_find` / `asset_get_info` | Asset search and info |
| `asset_create_folder` / `asset_delete` / `asset_move` / `asset_copy` | Asset file operations |
| `asset_refresh` | Refresh AssetDatabase |

</details>

<details>
<summary><b>Material & Script</b></summary>

| Tool | Description |
|------|-------------|
| `material_create` / `material_modify` | Material creation and modification |
| `shader_list` | List available shaders |
| `script_create` / `script_read` / `script_update` | C# script CRUD |

</details>

<details>
<summary><b>Prefab & Animation & UI & VFX</b></summary>

| Tool | Description |
|------|-------------|
| `prefab_create` / `prefab_instantiate` | Prefab workflow |
| `prefab_open` / `prefab_save_close` / `prefab_unpack` | Prefab editing |
| `animation_create_clip` / `animation_manage_controller` | Animation management |
| `vfx_create_particle` / `vfx_modify_particle` | Particle systems |
| `vfx_create_graph` / `vfx_get_info` | VFX Graph |

</details>

<details>
<summary><b>Editor & Utility</b></summary>

| Tool | Description |
|------|-------------|
| `editor_get_state` / `editor_set_playmode` | Editor state control |
| `editor_execute_menu` | Execute menu commands |
| `editor_selection_get` / `editor_selection_set` | Selection management |
| `screenshot_scene` / `screenshot_game` | Scene/Game view screenshots |
| `console_get_logs` | Console logs |
| `test_run` / `test_get_results` | Test runner |
| `package_list` / `package_add` | UPM package management |
| `batch_execute` | Atomic batch execution |
| `instance_list` / `instance_set_active` | Multi-instance management |

</details>

<details>
<summary><b>Resources</b></summary>

| URI | Description |
|-----|-------------|
| `unity://scene/hierarchy` | Scene hierarchy tree |
| `unity://scene/list` | Scenes in Build Settings |
| `unity://project/info` | Project metadata |
| `unity://editor/state` | Editor state |
| `unity://console/logs` | Console logs |
| `unity://gameobject/{id}` | GameObject details |
| `unity://assets/search/{filter}` | Asset search |
| `unity://packages/list` | Installed UPM packages |
| `unity://tests/{mode}` | Test list |
| `unity://tags` / `unity://layers` | Tags and layers |
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
│   │   ├── Tools/              # 60+ built-in tools
│   │   ├── Resources/          # 12 read-only resources
│   │   ├── Prompts/            # 40+ best-practice prompts
│   │   └── Window/             # Settings UI
│   ├── Runtime/                # Runtime mode (requires UNITY_MCP_RUNTIME define)
│   ├── Shared/                 # Code shared between Editor and Runtime
│   │   ├── Attributes/         # [McpTool], [McpResource], [McpPrompt], [Desc]
│   │   ├── Models/             # ToolResult, McpConst, McpCapabilities
│   │   └── Utils/              # ParameterBinder, JsonSchemaGenerator, SecurityChecker
│   ├── Samples~/               # Custom tool examples
│   └── Tests/                  # Tests
├── unity-bridge/               # C# stdio-to-TCP Bridge (.NET 8)
├── unity-server/               # Python FastMCP server
└── scripts/                    # Build scripts
```

### Dual Server Modes

| | Mode A: Built-in (C# Bridge) | Mode B: Python (FastMCP) |
|---|---|---|
| **Dependencies** | None (self-contained executable) | Python 3.10+ (`uvx` auto-installs) |
| **Transport** | stdio | stdio / Streamable HTTP |
| **Extra Tools** | — | `analyze_script`, `validate_assets` |
| **Best For** | Out-of-the-box, zero config | Python analysis tools, HTTP deployment, or Docker |

---

## Runtime Mode (Experimental)

Control a running game via MCP. Add `UNITY_MCP_RUNTIME` to `Player Settings > Scripting Define Symbols` to enable.

Runtime tools: `runtime_get_stats` / `runtime_time_scale` / `runtime_load_scene` / `runtime_invoke` / `runtime_get_logs` / `screenshot_game`

Runtime server listens on `port + 1`.

---

## Mode B: Python Server

<details>
<summary><b>Installation & Configuration</b></summary>

In Unity, open `Window > Unity MCP`, set Server Mode to **Python**, then use the Clients tab to configure your client.

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

Extra Python tools:
- `analyze_script` — C# script static analysis
- `validate_assets` — Asset naming and directory validation

</details>

<details>
<summary><b>Docker Deployment</b></summary>

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
| `UNITY_MCP_HOST` | `host.docker.internal` | Unity Editor host address |
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
| Server Mode | Built-in | `Built-in` (C# Bridge) or `Python` (FastMCP) |
| Port | 51279 | TCP port, change for multi-instance setups |
| Auto Start | On | Auto-start MCP server when Unity opens |
| Transport | Stdio | Python transport mode: `Stdio` or `Streamable HTTP` |
| HTTP Port | 8080 | Streamable HTTP port (Python mode only) |
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

</details>

<details>
<summary><b>MCP client can't connect</b></summary>

- Verify the port in client config matches `Window > Unity MCP`.
- Mode A: Ensure the Bridge binary exists (included automatically via Git URL install).
- Mode B: Ensure `UNITY_MCP_PORT` environment variable is correct.
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

This is expected behavior. The TCP connection briefly drops during Unity script recompilation, but the Bridge handles it automatically:

1. Unity sends a `notifications/reloading` notification before domain reload
2. After TCP disconnects, Bridge enters exponential backoff reconnection (0s → 1s → 2s → 4s → ...)
3. Requests from the MCP client during reconnection are buffered in an in-memory queue
4. Once Unity finishes recompilation and TCP recovers, Bridge replays all buffered requests

The entire process is nearly transparent to MCP clients, typically recovering within 2-5 seconds.

</details>

---

## Requirements

- **Unity** 2021.2+
- **Mode A**: No extra dependencies (Bridge is a self-contained executable, pre-built in `Bridge~/`)
- **Mode B**: Python 3.10+ (recommend `uvx` for automatic dependency management)
- **Unity Dependency**: `com.unity.nuget.newtonsoft-json` 3.2.1+ (auto-resolved)
- **Rebuild Bridge** (optional): [.NET 8+ SDK](https://dotnet.microsoft.com/download), run `./scripts/build_bridge.sh --current-only`

## License

[MIT](../LICENSE)
