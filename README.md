<div align="center">

# Unity MCP

[![Unity 2021.2+](https://img.shields.io/badge/Unity-2021.2%2B-000000?style=flat&logo=unity&logoColor=white)](https://unity.com/)
[![MCP Protocol](https://img.shields.io/badge/MCP-2024--11--05-4A90D9?style=flat)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**让 AI 助手直接控制你的 Unity Editor**

[English](docs/README.en.md) | 中文

</div>

**Unity MCP** 是一个嵌入 Unity Editor 的 [Model Context Protocol](https://modelcontextprotocol.io/) 服务器。安装后，Claude、Cursor、VS Code Copilot、Windsurf 等 AI 助手可以通过自然语言读取场景、创建物体、修改材质、运行测试、截图 —— 无需手动操作。

```
MCP 客户端 (Claude/Cursor/VS Code/Windsurf)
        ↕  stdio (JSON-RPC 2.0)
  C# Bridge / Python FastMCP
        ↕  TCP (自定义帧协议)
  Unity Editor (TCP 服务器 + 工具注册表)
```

---

## 快速开始

3 步完成配置，立即开始 AI 驱动的 Unity 开发。

### 1. 安装 Unity 包

**Git URL（推荐）** — 在 Unity 中：`Window > Package Manager > + > Add package from git URL`：

```
https://github.com/mzbswh/unity-mcp.git?path=unity-mcp
```

<details>
<summary>其他安装方式</summary>

**本地克隆**

```bash
git clone https://github.com/mzbswh/unity-mcp.git
```

在 Unity 中：`Window > Package Manager > + > Add package from disk`，选择 `unity-mcp/package.json`。

</details>

### 2. 一键配置客户端

打开 `Window > Unity MCP`，切换到 **Clients** 标签页，点击你使用的客户端旁的 **Configure** 按钮。

支持自动配置的客户端：

| 客户端 | 配置位置 | 说明 |
|--------|----------|------|
| **Claude Code** | `~/.claude.json` | 按项目路径写入 |
| **Cursor** | `.cursor/mcp.json` | 项目级配置 |
| **VS Code / Copilot** | `.vscode/mcp.json` | 项目级配置 |
| **Windsurf** | `~/.codeium/windsurf/mcp_config.json` | 全局配置 |

> 其他客户端可使用 **Copy Config to Clipboard** 手动粘贴。

### 3. 验证

向你的 AI 助手说：

> "列出我 Unity 场景中的所有 GameObject"

如果返回了场景层级信息，配置成功。

---

## 特性

- **60+ 编辑器工具** — GameObject、Component、Scene、Asset、Material、Animation、Prefab、Script、UI、VFX、Package、Test、Screenshot、Console
- **12 个资源端点** — 只读数据查询（场景层级、项目信息、编辑器状态、控制台日志等）
- **40+ 提示词模板** — Unity 最佳实践指南（架构、脚本、性能、Shader、XR、ECS、网络等）
- **批量执行** — 单次请求执行多个工具操作，支持原子回滚
- **运行时模式** — 可选的运行时 MCP 服务器，控制运行中的游戏
- **双服务器架构** — Mode A（C# stdio Bridge，轻量）或 Mode B（Python FastMCP，额外分析工具）
- **多实例支持** — 同时运行多个 Unity Editor 实例
- **自定义工具 API** — 用 C# 特性添加自定义工具，启动时自动发现
- **域重载安全** — Unity 脚本重编译后自动重连，Bridge 自动缓存并重放期间收到的请求，对 MCP 客户端近乎无感

---

## 工具一览

<details>
<summary><b>GameObject & Component</b></summary>

| 工具 | 说明 |
|------|------|
| `gameobject_create` | 创建 GameObject |
| `gameobject_destroy` | 删除 GameObject |
| `gameobject_find` | 按名称/路径查找 |
| `gameobject_modify` | 修改属性（名称、标签、层、激活状态） |
| `gameobject_set_parent` | 设置父子关系 |
| `gameobject_duplicate` | 复制 GameObject |
| `gameobject_get_components` | 获取组件列表 |
| `component_add` | 添加组件 |
| `component_remove` | 移除组件 |
| `component_get` | 查看组件属性 |
| `component_modify` | 修改组件属性 |

</details>

<details>
<summary><b>Scene & Asset</b></summary>

| 工具 | 说明 |
|------|------|
| `scene_create` / `scene_open` / `scene_save` | 场景管理 |
| `scene_get_hierarchy` / `scene_list_all` | 层级查看 |
| `asset_find` / `asset_get_info` | 资源搜索和信息 |
| `asset_create_folder` / `asset_delete` / `asset_move` / `asset_copy` | 资源文件操作 |
| `asset_refresh` | 刷新 AssetDatabase |

</details>

<details>
<summary><b>Material & Script</b></summary>

| 工具 | 说明 |
|------|------|
| `material_create` / `material_modify` | 材质创建和修改 |
| `shader_list` | 列出可用 Shader |
| `script_create` / `script_read` / `script_update` | C# 脚本增删改查 |

</details>

<details>
<summary><b>Prefab & Animation & UI & VFX</b></summary>

| 工具 | 说明 |
|------|------|
| `prefab_create` / `prefab_instantiate` | Prefab 工作流 |
| `prefab_open` / `prefab_save_close` / `prefab_unpack` | Prefab 编辑 |
| `animation_create_clip` / `animation_manage_controller` | 动画管理 |
| `ui_create_element` | 创建 UI 元素 |
| `vfx_create_particle` / `vfx_modify_particle` | 粒子系统 |
| `vfx_create_graph` / `vfx_get_info` | VFX Graph |

</details>

<details>
<summary><b>Editor & Utility</b></summary>

| 工具 | 说明 |
|------|------|
| `editor_get_state` / `editor_set_playmode` | 编辑器状态控制 |
| `editor_execute_menu` | 执行菜单命令 |
| `editor_selection_get` / `editor_selection_set` | 选中对象管理 |
| `screenshot_scene` / `screenshot_game` | Scene/Game 视图截图 |
| `console_get_logs` | 控制台日志 |
| `test_run` / `test_get_results` | 测试运行 |
| `package_list` / `package_add` | UPM 包管理 |
| `batch_execute` | 原子化批量执行 |
| `instance_list` / `instance_set_active` | 多实例管理 |

</details>

<details>
<summary><b>资源端点 (Resources)</b></summary>

| URI | 说明 |
|-----|------|
| `unity://scene/hierarchy` | 场景层级树 |
| `unity://scene/list` | Build Settings 中的场景列表 |
| `unity://project/info` | 项目元数据 |
| `unity://editor/state` | 编辑器状态 |
| `unity://console/logs` | 控制台日志 |
| `unity://gameobject/{id}` | GameObject 详细信息 |
| `unity://assets/search/{filter}` | 资源搜索 |
| `unity://packages/list` | 已安装的 UPM 包 |
| `unity://tests/{mode}` | 测试列表 |
| `unity://tags` / `unity://layers` | 标签和层 |
| `unity://menu/items` | 菜单项 |

</details>

---

## 自定义工具

用 C# 特性添加自定义工具 —— 启动时自动发现并注册。在 `Window > Unity MCP > Tools` 标签页中可以看到 **Built-in** 和 **Custom** 的区分。

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

通过 `Package Manager > Unity MCP > Samples > Custom Tools Example` 导入完整示例。

### 特性参考

| 特性 | 目标 | 说明 |
|------|------|------|
| `[McpToolGroup("name")]` | 类 | 标记为 MCP 工具/资源/提示词容器 |
| `[McpTool("name", "desc")]` | 方法 | 注册为 MCP 工具。选项：`Group`、`ReadOnly`、`Idempotent`、`Title`、`AutoRegister` |
| `[McpResource("uri", "name", "desc")]` | 方法 | 注册为 MCP 资源。选项：`MimeType` |
| `[McpPrompt("name", "desc")]` | 方法 | 注册为 MCP 提示词 |
| `[Desc("description")]` | 参数 | 为参数添加描述 |

---

## 架构

```
unity-mcp/
├── unity-mcp/                  # UPM 包 (com.mzbswh.unity-mcp)
│   ├── Editor/
│   │   ├── Core/               # McpServer, TcpTransport, RequestHandler, ToolRegistry
│   │   ├── Tools/              # 60+ 内置工具
│   │   ├── Resources/          # 12 个只读资源
│   │   ├── Prompts/            # 40+ 最佳实践提示词
│   │   └── Window/             # 设置界面
│   ├── Runtime/                # 运行时模式 (需定义 UNITY_MCP_RUNTIME)
│   ├── Shared/                 # Editor 与 Runtime 共享代码
│   │   ├── Attributes/         # [McpTool], [McpResource], [McpPrompt], [Desc]
│   │   ├── Models/             # ToolResult, McpConst, McpCapabilities
│   │   └── Utils/              # ParameterBinder, JsonSchemaGenerator, SecurityChecker
│   ├── Samples~/               # 自定义工具示例
│   └── Tests/                  # 测试
├── unity-bridge/               # C# stdio-to-TCP Bridge (.NET 8)
├── unity-server/               # Python FastMCP 服务器
└── scripts/                    # 构建脚本
```

### 双服务器模式

| | Mode A: Built-in (C# Bridge) | Mode B: Python (FastMCP) |
|---|---|---|
| **外部依赖** | [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | Python 3.10+, `mcp>=1.0.0` |
| **Bridge 体积** | 轻量（framework-dependent） | — |
| **额外工具** | — | `analyze_script`, `validate_assets` |
| **适合场景** | 轻量，快速启动 | 需要 Python 分析工具或自定义集成 |

---

## 运行时模式（实验性）

通过 MCP 控制运行中的游戏。在 `Player Settings > Scripting Define Symbols` 中添加 `UNITY_MCP_RUNTIME` 以启用。

运行时工具：`runtime_get_stats` / `runtime_time_scale` / `runtime_load_scene` / `runtime_invoke` / `runtime_get_logs` / `screenshot_game`

运行时服务器监听端口为 `port + 1`。

---

## Mode B: Python 服务器

<details>
<summary><b>安装和配置</b></summary>

在 Unity 中 `Window > Unity MCP`，将 Server Mode 设为 **Python**，然后使用 Clients 标签页配置客户端。

MCP 客户端配置示例（以 Cursor 为例）：

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

> `uvx` 会自动从 PyPI 下载并运行，无需手动安装。也可以用 `pip install unity-mcp-server` 安装后直接运行 `unity-mcp-server`。
> 默认连接端口 51279，多实例场景可通过 `env` 字段指定 `UNITY_MCP_PORT`。

Python 服务器额外提供：
- `analyze_script` — C# 脚本静态分析
- `validate_assets` — 资源命名和目录验证

</details>

<details>
<summary><b>Docker 部署</b></summary>

```bash
cd unity-server

# docker compose
docker compose up -d
UNITY_MCP_PORT=53000 docker compose up -d

# 手动
docker build -t unity-mcp-server .
docker run -it --rm \
  -e UNITY_MCP_HOST=host.docker.internal \
  -e UNITY_MCP_PORT=51279 \
  --add-host=host.docker.internal:host-gateway \
  unity-mcp-server
```

| 环境变量 | 默认值 | 说明 |
|----------|--------|------|
| `UNITY_MCP_HOST` | `host.docker.internal` | Unity Editor 主机地址 |
| `UNITY_MCP_PORT` | `51279` | Unity Editor TCP 端口 |
| `UNITY_MCP_TIMEOUT` | `60` | 请求超时时间（秒） |

</details>

---

## 设置

通过 `Window > Unity MCP` 访问：

| 设置 | 默认值 | 说明 |
|------|--------|------|
| Server Mode | Built-in | `Built-in`（C# Bridge）或 `Python`（FastMCP） |
| Port | Auto | TCP 端口（-1 = 根据项目路径哈希自动生成） |
| Auto Start | 开启 | 自动启动外部服务器（仅 Python 模式） |
| Request Timeout | 60s | 工具执行最大超时 |
| Log Level | Info | Debug / Info / Warning / Error / Off |
| Audit Log | 关闭 | 记录每次工具调用及耗时 |
| Max Batch Operations | 30 | 单次 `batch_execute` 调用允许的最大操作数 |

---

## 常见问题

<details>
<summary><b>服务器未启动</b></summary>

- 检查 `Window > Unity MCP` 中的状态指示灯。绿色 = 运行中。
- 点击 **Restart** 按钮重启。
- 查看 Unity Console 中的 `[MCP]` 日志。

</details>

<details>
<summary><b>MCP 客户端无法连接</b></summary>

- 确认客户端配置中的端口与 `Window > Unity MCP` 显示的端口一致。
- Mode A：确保 Bridge 二进制文件存在（通过 Git URL 安装时已自动包含）。
- Mode B：确保 `UNITY_MCP_PORT` 环境变量设置正确。
- 在 Clients 标签页检查客户端是否显示为 **Configured**。

</details>

<details>
<summary><b>自定义工具未显示</b></summary>

- 确保工具类标记了 `[McpToolGroup]`，方法标记了 `[McpTool]`。
- 检查脚本是否编译通过。
- 工具在启动时扫描；添加新工具后点击 **Restart**。
- 在 Tools 标签页中查看 **Custom** 区域。

</details>

<details>
<summary><b>域重载导致断开连接</b></summary>

这是预期行为。Unity 脚本重编译时 TCP 连接会短暂中断，但 Bridge 会自动处理：

1. Unity 在域重载前发送 `notifications/reloading` 通知
2. TCP 断开后 Bridge 自动进入指数退避重连（0s → 1s → 2s → 4s → ...）
3. 重连期间 MCP 客户端发来的请求会被 Bridge 缓存在内存队列中
4. Unity 重编译完成后 TCP 恢复，Bridge 自动重放缓存的请求

整个过程对 MCP 客户端近乎透明，通常 2-5 秒内自动恢复。

</details>

---

## 系统要求

- **Unity** 2021.2+
- **Mode A**：[.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（Bridge 已预编译打包于 `Bridge~/`，轻量）
- **Mode B**：Python 3.10+，`mcp>=1.0.0`
- **依赖**：`com.unity.nuget.newtonsoft-json` 3.2.1+（自动解析）
- **重新构建 Bridge**（可选）：[.NET 8+ SDK](https://dotnet.microsoft.com/download)，运行 `./scripts/build_bridge.sh --current-only`

## 许可证

[MIT](LICENSE)
