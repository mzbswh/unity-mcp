# Unity MCP

[![Unity 2021.2+](https://img.shields.io/badge/Unity-2021.2%2B-blue.svg)](https://unity.com/)
[![MCP Protocol](https://img.shields.io/badge/MCP-2024--11--05-green.svg)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[English](docs/README.en.md)

**Unity MCP** 是一个嵌入 Unity Editor 的 [Model Context Protocol](https://modelcontextprotocol.io/) 服务器，让 AI 助手（Claude、Cursor、VS Code Copilot、Windsurf 等）能够直接控制和查询你的 Unity 项目。

> AI 可以读取场景层级、创建 GameObject、修改材质、运行测试、截取屏幕截图 —— 全部通过自然语言实现。

## 特性

- **60+ 编辑器工具** — GameObject、Component、Scene、Asset、Material、Animation、Prefab、Script、UI、VFX、Package、Test、Screenshot、Console 等
- **12 个资源端点** — 只读数据端点，用于查询场景层级、项目信息、编辑器状态、控制台日志等
- **40+ 提示词模板** — Unity 最佳实践指南，覆盖架构、脚本、性能、Shader、XR、ECS、网络等
- **批量执行** — 在单次请求中执行多个工具操作，支持原子回滚
- **运行时模式** — 可选的运行时 MCP 服务器，用于控制运行中的游戏（性能统计、时间缩放、场景加载）
- **双服务器架构** — Mode A（C# stdio Bridge）或 Mode B（Python FastMCP），按需选择
- **多实例支持** — 同时支持多个 Unity Editor 实例
- **自定义工具 API** — 用简单的 C# 特性添加自定义工具
- **域重载安全** — Unity 脚本重编译后自动恢复连接

## 架构

```
MCP 客户端 (Claude/Cursor/...)
    |
    |  stdio (JSON-RPC 2.0)
    |
 [Mode A: C# Bridge]          [Mode B: Python FastMCP 服务器]
    |                               |
    |  TCP (自定义帧协议)            |  TCP (自定义帧协议)
    |                               |
Unity Editor (TCP 服务器 + 工具注册表)
```

- **Mode A（内置）**：轻量级 C# Bridge 二进制文件，实现 stdio 与 TCP 的双向转换。无需 Python。
- **Mode B（Python）**：Python FastMCP 服务器，动态发现工具。额外提供本地分析工具（`analyze_script`、`validate_assets`）。

## 快速开始

### 1. 安装 Unity 包

**方式 A — Git URL（推荐）**

在 Unity 中：`Window > Package Manager > + > Add package from git URL`：

```
https://github.com/mzbswh/unity-mcp.git?path=unity-mcp
```

**方式 B — 本地克隆**

```bash
git clone https://github.com/mzbswh/unity-mcp.git
```

然后在 Unity 中：`Window > Package Manager > + > Add package from disk`，选择 `unity-mcp/package.json`。

### 2. 构建 Bridge（Mode A）

需要 [.NET 8+ SDK](https://dotnet.microsoft.com/download)：

```bash
./scripts/build_bridge.sh --current-only
```

### 3. 配置 MCP 客户端

在 Unity 中：`Window > Unity MCP > Quick Setup`，点击你的客户端（Claude Code / Cursor / VS Code / Windsurf）将配置复制到剪贴板。

**Claude Desktop** — 粘贴到 `~/Library/Application Support/Claude/claude_desktop_config.json`（macOS）或 `%APPDATA%\Claude\claude_desktop_config.json`（Windows）：

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

端口根据项目路径自动生成。在 `Window > Unity MCP` 中查看实际端口。

### 4. 验证

向你的 AI 助手提问：

> "列出我 Unity 场景中的所有 GameObject"

如果返回了场景层级信息，说明配置成功。

## 可用工具

### 编辑器工具

| 分类 | 工具 | 说明 |
|------|------|------|
| **GameObject** | `gameobject_create`、`gameobject_destroy`、`gameobject_find`、`gameobject_modify`、`gameobject_set_parent`、`gameobject_duplicate`、`gameobject_get_components` | 创建、查找、修改和管理 GameObject |
| **Component** | `component_add`、`component_remove`、`component_get`、`component_modify` | 添加/移除/查看/修改组件及其属性 |
| **Scene** | `scene_create`、`scene_open`、`scene_save`、`scene_get_hierarchy`、`scene_list_all` | 场景管理和层级查看 |
| **Asset** | `asset_find`、`asset_create_folder`、`asset_delete`、`asset_move`、`asset_copy`、`asset_refresh`、`asset_get_info` | AssetDatabase 操作 |
| **Material** | `material_create`、`material_modify`、`shader_list` | 创建/修改材质和列出 Shader |
| **Script** | `script_create`、`script_read`、`script_update` | C# 脚本的增删改查 |
| **Prefab** | `prefab_create`、`prefab_instantiate`、`prefab_open`、`prefab_save_close`、`prefab_unpack` | Prefab 工作流 |
| **Animation** | `animation_create_clip`、`animation_manage_controller` | AnimationClip 和 AnimatorController 管理 |
| **UI** | `ui_create_element` | 创建 UI 元素（Button、Text、Image 等） |
| **VFX** | `vfx_create_particle`、`vfx_modify_particle`、`vfx_create_graph`、`vfx_get_info` | 粒子系统和 VFX Graph |
| **Editor** | `editor_get_state`、`editor_set_playmode`、`editor_execute_menu`、`editor_selection_get`、`editor_selection_set` | 编辑器状态和播放模式控制 |
| **Screenshot** | `screenshot_scene`、`screenshot_game` | 将 Scene/Game 视图截图为 Base64 PNG |
| **Console** | `console_get_logs` | 读取和过滤 Unity 控制台日志 |
| **Test** | `test_run`、`test_get_results` | 运行 EditMode/PlayMode 测试 |
| **Package** | `package_list`、`package_add` | UPM 包管理 |
| **Batch** | `batch_execute` | 原子化批量执行多个工具 |
| **Instance** | `instance_list`、`instance_set_active` | 多实例管理 |
| **MPPM** | `editor_is_clone`、`editor_get_mppm_tags` | Multiplayer Play Mode 支持 |

### 资源

| URI | 说明 |
|-----|------|
| `unity://scene/hierarchy` | 场景层级树 |
| `unity://scene/list` | Build Settings 中的场景列表 |
| `unity://project/info` | 项目元数据 |
| `unity://editor/state` | 编辑器状态（播放模式、平台等） |
| `unity://console/logs` | 控制台日志条目 |
| `unity://gameobject/{id}` | 按实例 ID 获取 GameObject 详细信息 |
| `unity://assets/search/{filter}` | 资源搜索结果 |
| `unity://packages/list` | 已安装的 UPM 包 |
| `unity://tests/{mode}` | 测试列表（EditMode/PlayMode） |
| `unity://tags` | 可用标签 |
| `unity://layers` | 可用层 |
| `unity://menu/items` | 菜单项 |

### 提示词模板

40+ Unity 最佳实践提示词，覆盖：脚本规范、MonoBehaviour 生命周期、错误处理、序列化、架构模式、ScriptableObject、异步编程、场景组织、资源命名、性能优化、物理、输入系统、音频、AI 导航、网络、动画、UI Toolkit、Shader、测试、调试、项目搭建、2D/3D 工作流、VFX、Addressables、CI/CD、移动端优化、XR、ECS/DOTS、地形、自定义编辑器、渲染管线、多人游戏、程序化生成、背包/对话系统、版本控制、AssetBundle、编辑器自动化、存档系统、本地化、依赖注入、事件架构、对象池、状态机、相机系统和光照。

## 自定义工具

用简单的 C# 特性添加自定义工具。创建带有 `[McpToolGroup]` 的类和带有 `[McpTool]` 的方法 —— 启动时自动发现并注册。

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
| `[McpToolGroup("name")]` | 类 | 标记一个类包含 MCP 工具/资源/提示词 |
| `[McpTool("name", "desc")]` | 方法 | 注册方法为 MCP 工具。选项：`Group`、`ReadOnly`、`Idempotent`、`Title`、`AutoRegister` |
| `[McpResource("uri", "name", "desc")]` | 方法 | 注册方法为 MCP 资源。选项：`MimeType` |
| `[McpPrompt("name", "desc")]` | 方法 | 注册方法为 MCP 提示词 |
| `[Desc("description")]` | 参数 | 为工具/资源/提示词的参数添加描述 |

## 运行时模式（实验性）

通过 MCP 控制运行中的游戏。在 `Player Settings > Scripting Define Symbols` 中添加 `UNITY_MCP_RUNTIME` 以启用。

运行时工具包括：
- `runtime_get_stats` / `runtime_profiler_snapshot` — 性能监控
- `runtime_time_scale` — 暂停、慢动作、快进
- `runtime_load_scene` — 运行时加载场景
- `runtime_invoke` — 调用运行时对象的方法
- `runtime_get_logs` — 读取运行时日志
- `screenshot_game` / `screenshot_camera` — 运行时截图

运行时服务器监听端口为 `port + 1`（根据项目路径自动检测）。

## Mode B：Python 服务器

用于额外的本地分析工具或自定义 Python 集成：

### 安装

```bash
cd unity-server
pip install -e .
# 或使用 uv：
uv pip install -e .
```

### 配置

在 Unity 中：`Window > Unity MCP`，将 Server Mode 设为 **Python**，配置 Python 路径和服务器脚本，然后使用 Quick Setup 复制 MCP 客户端配置。

Python 服务器额外提供两个本地工具：
- `analyze_script` — C# 脚本静态分析，检查常见问题
- `validate_assets` — 资源命名规范和目录结构验证

所有 Unity 工具/资源/提示词会被动态发现并转发。

## 设置

通过 `Window > Unity MCP` 访问：

| 设置 | 默认值 | 说明 |
|------|--------|------|
| Server Mode | Built-in | `Built-in`（C# Bridge）或 `Python`（FastMCP） |
| Port | Auto | TCP 端口（-1 = 根据项目路径哈希自动生成） |
| Auto Start | 开启 | 自动启动外部服务器进程（仅 Python 模式） |
| Request Timeout | 60s | 工具执行最大超时时间 |
| Log Level | Info | Debug / Info / Warning / Error / Off |
| Audit Log | 关闭 | 记录每次工具调用及耗时 |

## 系统要求

- **Unity** 2021.2 或更高版本
- **Mode A**：[.NET 8+ SDK](https://dotnet.microsoft.com/download)（用于构建 Bridge 二进制文件）
- **Mode B**：Python 3.10+，需要 `mcp>=1.0.0`
- **依赖**：`com.unity.nuget.newtonsoft-json` 3.2.1+（自动解析）

## 项目结构

```
unity-mcp/
├── unity-mcp/                  # UPM 包
│   ├── Editor/                 # 仅编辑器代码
│   │   ├── Core/               # McpServer、TcpTransport、RequestHandler、ToolRegistry
│   │   ├── Tools/              # 60+ 内置工具
│   │   ├── Resources/          # 12 个只读资源
│   │   ├── Prompts/            # 40+ 最佳实践提示词
│   │   ├── Window/             # 设置界面
│   │   └── Utils/              # UndoHelper
│   ├── Runtime/                # 运行时模式 (UNITY_MCP_RUNTIME)
│   │   ├── Core/               # RuntimeTcpTransport、RuntimeToolRegistry
│   │   ├── Tools/              # 运行时工具（性能统计、控制、调用）
│   │   └── Resources/          # 运行时资源
│   ├── Shared/                 # Editor 与 Runtime 共享
│   │   ├── Attributes/         # [McpTool]、[McpResource]、[McpPrompt]、[Desc]
│   │   ├── Models/             # ToolResult、McpConst、McpCapabilities
│   │   ├── Utils/              # ParameterBinder、JsonSchemaGenerator、SecurityChecker
│   │   └── Instance/           # 多实例发现
│   ├── Samples~/               # 自定义工具示例
│   ├── Tests/                  # Editor 与 Runtime 测试
│   └── package.json            # UPM 清单
├── unity-bridge/               # C# stdio-to-TCP Bridge (.NET 8)
├── unity-server/               # Python FastMCP 服务器
└── scripts/                    # 构建脚本
```

## 常见问题

**服务器未启动**
- 检查 `Window > Unity MCP` 中的状态。如需重启，点击 Restart 按钮。
- 查看 Unity Console 中的 `[MCP]` 日志消息。

**MCP 客户端无法连接**
- 确认 MCP 客户端配置中的端口与 `Window > Unity MCP` 显示的端口一致。
- Mode A：确保 Bridge 二进制文件已构建（`./scripts/build_bridge.sh --current-only`）。
- Mode B：确保 `UNITY_MCP_PORT` 环境变量设置正确。

**工具未显示**
- 确保你的工具类标记了 `[McpToolGroup]`，方法标记了 `[McpTool]`。
- 检查脚本是否编译通过。
- 工具在启动时扫描注册；添加新工具后点击 Restart。

**域重载导致断开连接**
- 这是预期行为。Bridge 会在 Unity 重编译完成后自动重连。

## 许可证

MIT
