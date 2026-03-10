# unity-mcp-server

Python MCP server for [Unity MCP](https://github.com/mzbswh/unity-mcp) — enables AI assistants (Claude, Cursor, VS Code Copilot, Windsurf) to control the Unity Editor via the [Model Context Protocol](https://modelcontextprotocol.io/).

## Quick Start

### 1. Install the Unity package

In Unity: `Window > Package Manager > + > Add package from git URL`:

```
https://github.com/mzbswh/unity-mcp.git?path=unity-mcp
```

### 2. Configure your MCP client

Add to your MCP client config (e.g. `.cursor/mcp.json`, `.vscode/mcp.json`):

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

Or use **Window > Unity MCP > Clients** tab in Unity for one-click configuration.

### 3. Verify

Ask your AI assistant: *"List all GameObjects in my Unity scene"*

## Streamable HTTP Mode

By default the server runs in **stdio** mode (MCP client launches it automatically). To run as a standalone HTTP server:

```bash
UNITY_MCP_TRANSPORT=streamable-http uvx unity-mcp-server
```

Then configure your MCP client with `http://127.0.0.1:8080/mcp`.

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_MCP_HOST` | `127.0.0.1` | Unity Editor host address |
| `UNITY_MCP_PORT` | `51279` | Unity Editor TCP port |
| `UNITY_MCP_TIMEOUT` | `60` | Request timeout (seconds) |
| `UNITY_MCP_TRANSPORT` | `stdio` | Transport mode: `stdio` or `streamable-http` |
| `UNITY_MCP_HTTP_PORT` | `8080` | HTTP port (only for `streamable-http` mode) |

## Docker

```bash
cd unity-server

# stdio mode (default)
docker compose up -d

# Streamable HTTP mode
UNITY_MCP_TRANSPORT=streamable-http docker compose up -d
```

## Extra Tools

In addition to all Unity Editor tools (60+), this server provides:

- `analyze_script` — C# script static analysis
- `validate_assets` — Asset naming and directory validation

## License

[MIT](https://github.com/mzbswh/unity-mcp/blob/main/LICENSE)
