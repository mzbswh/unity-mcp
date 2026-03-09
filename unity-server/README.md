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
      "args": ["unity-mcp-server"],
      "env": {
        "UNITY_MCP_PORT": "52345"
      }
    }
  }
}
```

Or use **Window > Unity MCP > Clients** tab in Unity for one-click configuration.

### 3. Verify

Ask your AI assistant: *"List all GameObjects in my Unity scene"*

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_MCP_HOST` | `127.0.0.1` | Unity Editor host address |
| `UNITY_MCP_PORT` | `52345` | Unity Editor TCP port |
| `UNITY_MCP_TIMEOUT` | `60` | Request timeout (seconds) |

## Extra Tools

In addition to all Unity Editor tools (60+), this server provides:

- `analyze_script` — C# script static analysis
- `validate_assets` — Asset naming and directory validation

## License

[MIT](https://github.com/mzbswh/unity-mcp/blob/main/LICENSE)
