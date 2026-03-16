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
        ↕  stdio / streamable-http (JSON-RPC 2.0)
  Python FastMCP 服务器 (动态工具发现)
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

### 3. 验证

向你的 AI 助手说：

> "列出我 Unity 场景中的所有 GameObject"

如果返回了场景层级信息，配置成功。

---

## 特性

- **146 个编辑器工具** — GameObject、Scene、Asset、Material、Animation、Prefab、UI Toolkit、VFX、Graphics、Lighting、NavMesh、Physics、Terrain、Shader、Texture、Build、Package、Test、Screenshot、Console、ProBuilder 等
- **11 个 Python 端工具** — PSD 解析/导出/合成/转 UI、蓝湖设计稿获取/切图下载（无需 Unity 连接即可运行）
- **13 个资源端点** — 只读数据查询（场景层级、项目信息、编辑器状态、控制台日志、当前选中等）
- **48 个提示词模板** — Unity 最佳实践指南（架构、脚本、性能、Shader、XR、ECS、网络等）
- **PSD → UI 工作流** — 解析 PSD/PSB 文件结构，导出图层图片，自动生成 Unity UI 层级
- **蓝湖集成** — 获取蓝湖设计稿列表、下载设计图进行 AI 分析、批量下载切图到项目
- **批量执行** — 单次请求执行多个工具操作，支持原子回滚
- **运行时模式** — 可选的运行时 MCP 服务器，控制运行中的游戏（8 个运行时工具）
- **动态工具发现** — Python 服务器启动时从 Unity 自动发现并注册所有工具/资源/提示词
- **多实例支持** — 同时运行多个 Unity Editor 实例
- **自定义工具 API** — 用 C# 特性添加自定义工具，启动时自动发现
- **域重载安全** — Unity 脚本重编译后自动重连；Python 服务器自动缓存并重放期间收到的请求，对 MCP 客户端近乎无感
- **依赖检测** — 首次启动自动检查 Python/uv/uvx 环境，引导安装
- **版本更新检查** — 每日自动检查新版本，在设置窗口提示更新
- **工具调用诊断** — 记录最近工具调用的名称、耗时和成功/失败状态

---

## 工具一览

<details>
<summary><b>GameObject & Component (14 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `gameobject_create` | 创建 GameObject |
| `gameobject_destroy` | 删除 GameObject |
| `gameobject_find` | 按名称/路径查找 |
| `gameobject_modify` | 修改属性（名称、标签、层、激活状态） |
| `gameobject_set_parent` | 设置父子关系 |
| `gameobject_duplicate` | 复制 GameObject |
| `gameobject_get_components` | 获取组件列表 |
| `gameobject_look_at` | 朝向目标 |
| `gameobject_set_sibling_index` | 设置同级排序 |
| `gameobject_add_component` | 添加组件 |
| `gameobject_remove_component` | 移除组件 |
| `gameobject_get_component` | 查看组件属性 |
| `gameobject_modify_component` | 修改组件属性 |
| `gameobject_copy_component` | 复制组件值 |

</details>

<details>
<summary><b>Scene (15 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `scene_create` / `scene_open` / `scene_save` | 场景管理 |
| `scene_get_hierarchy` / `scene_list_all` | 层级查看 |
| `scene_align_with_view` / `scene_move_to_view` | 视图对齐 |
| `scene_frame_selected` | 聚焦选中对象 |
| `scene_view_get` / `scene_view_set` | Scene 视图位置控制 |
| `scene_view_get_settings` / `scene_view_set_settings` | Scene 视图设置 |
| `game_view_get_settings` / `game_view_set_settings` | Game 视图设置 |
| `scene_view_snap_angle` | 吸附到预设角度 |

</details>

<details>
<summary><b>Asset (10 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `asset_find` / `asset_get_info` | 资源搜索和信息 |
| `asset_create_folder` / `asset_delete` / `asset_move` / `asset_copy` | 资源文件操作 |
| `asset_refresh` | 刷新资源数据库 |
| `asset_set_import_settings` / `asset_set_model_import` | 导入设置 |
| `asset_find_references` | 查找引用 |

</details>

<details>
<summary><b>Material & Shader (8 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `material_create` / `material_modify` | 材质创建和修改 |
| `material_set_render_mode` | 设置渲染模式 |
| `material_get_keywords` / `material_set_keywords` | 材质关键字管理 |
| `shader_list` / `shader_get_properties` | Shader 查询 |
| `shader_info` | Shader 详细信息 |

</details>

<details>
<summary><b>Prefab (10 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `prefab_create` / `prefab_instantiate` | Prefab 工作流 |
| `prefab_open` / `prefab_close` | Prefab 编辑模式 |
| `prefab_get_hierarchy` / `prefab_get_stage_objects` | Prefab 层级查看 |
| `prefab_modify_contents` | 修改 Prefab 内容 |
| `prefab_apply_overrides` / `prefab_revert_overrides` | 覆盖管理 |
| `prefab_unpack` | 解包 Prefab |

</details>

<details>
<summary><b>Animation (6 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `animation_create_clip` | 创建动画剪辑 |
| `animation_manage_controller` | 管理 Animator Controller |
| `animation_add_transition` | 添加状态转换 |
| `animation_add_layer` | 添加动画层 |
| `animation_create_blend_tree` | 创建混合树 |
| `animation_set_clip_curve` | 设置动画曲线 |

</details>

<details>
<summary><b>UI Toolkit (4 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `uitoolkit_create` | UXML/USS 创建 |
| `uitoolkit_list` | 列出 UI 资源 |
| `uitoolkit_attach` | 绑定 UIDocument |
| `uitoolkit_get_visual_tree` | 获取运行时视觉树 |

</details>

<details>
<summary><b>VFX (1 tool)</b></summary>

| 工具 | 说明 |
|------|------|
| `vfx_create_graph` | VFX Graph 创建 |

</details>

<details>
<summary><b>Graphics (11 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `graphics_get_skybox` / `graphics_set_skybox` | 天空盒 |
| `graphics_get_fog` / `graphics_set_fog` | 雾效 |
| `graphics_get_ambient` / `graphics_set_ambient` | 环境光 |
| `graphics_get_render_pipeline` | 渲染管线信息 |
| `graphics_get_quality` / `graphics_set_quality` | 画质设置 |
| `graphics_get_stats` | 图形统计 |
| `graphics_get_lightmap_settings` | 光照贴图设置 |

</details>

<details>
<summary><b>Lighting (2 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `lighting_bake` | 光照烘焙 |
| `lighting_cancel_bake` | 取消烘焙 |

</details>

<details>
<summary><b>Physics & NavMesh (6 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `physics_create_material` | 物理材质 |
| `physics_raycast` | 射线检测 |
| `physics_get_settings` / `physics_set_gravity` | 物理设置 |
| `navmesh_bake` / `navmesh_clear` | 导航网格烘焙 |

</details>

<details>
<summary><b>Terrain (6 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `terrain_create` | 创建地形 |
| `terrain_get_info` | 获取地形信息 |
| `terrain_set_height` / `terrain_flatten` | 高度编辑 |
| `terrain_add_layer` | 添加地形层 |
| `terrain_add_tree` | 添加树木 |

</details>

<details>
<summary><b>Texture (2 tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `texture_get_info` | 获取纹理信息 |
| `texture_search` | 搜索纹理资源 |

</details>

<details>
<summary><b>PSD & 蓝湖 (11 tools, Python 端)</b></summary>

| 工具 | 说明 |
|------|------|
| `psd_summary` | 获取 PSD/PSB 文件摘要（尺寸、图层数、颜色模式等） |
| `psd_layer_detail` | 获取详细图层信息（层级、混合模式、可见性等） |
| `psd_parse` | 解析 PSD/PSB 并返回完整图层树（可选导出图片） |
| `psd_export_images` | 导出 PSD 所有可见图层为 PNG 图片（仅返回导出列表） |
| `psd_to_image` | 将 PSD/PSB 合成为单张 PNG/JPG 图片（支持缩放） |
| `psd_to_ui` | PSD 转 Unity UI 完整流程（解析 + 导出 + 生成 UI） |
| `lanhu_set_cookie` | 设置蓝湖认证 Cookie |
| `lanhu_get_designs` | 获取蓝湖项目设计稿列表 |
| `lanhu_analyze_design` | 下载蓝湖设计图进行 AI 分析 |
| `lanhu_get_slices` | 获取蓝湖设计稿切图列表 |
| `lanhu_download_slices` | 批量下载蓝湖切图到 Unity 项目 |

</details>

<details>
<summary><b>Editor & Utility (50+ tools)</b></summary>

| 工具 | 说明 |
|------|------|
| `editor_get_state` / `editor_set_playmode` | 编辑器状态控制 |
| `editor_execute_menu` | 执行菜单命令 |
| `editor_selection_get` / `editor_selection_set` | 选中对象管理 |
| `editor_undo` / `editor_redo` | 撤销/重做 |
| `editor_open_window` | 打开编辑器窗口 |
| `editor_get_compile_status` | 脚本编译状态 |
| `screenshot_scene` / `screenshot_game` | Scene/Game 视图截图（返回 MCP 图片） |
| `console_get_logs` | 控制台日志 |
| `test_run` / `test_get_results` | 测试运行 |
| `package_list` / `package_add` / `package_remove` / `package_search` / `package_get_info` | UPM 包管理 |
| `build_player` / `build_get_settings` / `build_set_scenes` / `build_switch_platform` | 构建管理 |
| `build_get_player_settings` / `build_set_player_settings` | Player Settings |
| `settings_get_tags` / `settings_add_tag` / `settings_get_layers` / `settings_add_layer` | 标签和层管理 |
| `settings_get_sorting_layers` / `settings_add_sorting_layer` | 排序层 |
| `settings_get_quality` / `settings_set_quality` | 画质等级 |
| `settings_get_time` / `settings_set_time` | 时间设置 |
| `so_create` / `so_get` / `so_modify` / `so_list_types` | ScriptableObject 管理 |
| `code_execute` / `code_validate` | C# 代码执行 |
| `batch_execute` | 原子化批量执行 |
| `instance_list` / `instance_set_active` | 多实例管理 |
| `editor_is_clone` / `editor_get_mppm_tags` | MPPM 支持 |
| `probuilder_create_shape` / `probuilder_get_mesh_info` / `probuilder_extrude_faces` / `probuilder_set_material` | ProBuilder 建模 |

</details>

<details>
<summary><b>资源端点 (13 Resources)</b></summary>

| URI | 说明 |
|-----|------|
| `unity://scene/hierarchy` | 场景层级树 |
| `unity://scene/list` | Build Settings 中的场景列表 |
| `unity://project/info` | 项目元数据 |
| `unity://editor/state` | 编辑器状态 |
| `unity://editor/selection` | 当前选中对象 |
| `unity://console/logs` | 控制台日志 |
| `unity://gameobject/{id}` | GameObject 详细信息 |
| `unity://assets/search/{filter}` | 资源搜索 |
| `unity://packages/list` | 已安装的 UPM 包 |
| `unity://tests/{mode}` | 测试列表 |
| `unity://tags` | 标签列表 |
| `unity://layers` | 层列表 |
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
│   │   │                         ToolCallLogger, DependencyChecker, PackageUpdateChecker
│   │   ├── Tools/              # 146 个内置工具
│   │   ├── Resources/          # 13 个只读资源
│   │   ├── Prompts/            # 48 个最佳实践提示词
│   │   ├── Utils/              # 编辑器辅助工具
│   │   └── Window/             # 设置界面、客户端配置
│   ├── Runtime/                # 运行时模式 (需定义 UNITY_MCP_RUNTIME)
│   ├── Shared/                 # Editor 与 Runtime 共享代码
│   │   ├── Attributes/         # [McpTool], [McpResource], [McpPrompt], [Desc]
│   │   ├── Models/             # ToolResult, McpConst, McpCapabilities, Pagination
│   │   ├── Interfaces/         # IToolRegistry, ITcpTransport, IMainThreadDispatcher
│   │   ├── Instance/           # 多实例发现 (InstanceDiscovery)
│   │   └── Utils/              # PaginationHelper, ParameterBinder, JsonSchemaGenerator
│   ├── Samples~/               # 自定义工具示例
│   └── Tests/                  # EditMode 测试（9 个测试文件）
├── unity-server/               # Python FastMCP 服务器（PyPI: unity-mcp-server）
│   ├── unity_mcp_server/
│   │   ├── server.py           # FastMCP 入口 + 动态工具发现注册
│   │   ├── unity_connection.py # TCP 连接管理 + 自动重连
│   │   ├── config.py           # 环境变量配置
│   │   └── tools/              # Python 端工具（PSD 解析、蓝湖集成等）
│   ├── pyproject.toml          # PyPI 包配置
│   ├── Dockerfile              # Docker 部署
│   └── docker-compose.yml
└── scripts/                    # bump-version.sh 版本管理
```

### 工作原理

Unity 插件在 Editor 启动时启动 TCP 服务器，扫描所有带 `[McpTool]`/`[McpResource]`/`[McpPrompt]` 特性的方法并注册。MCP 客户端通过 stdio 启动 Python FastMCP 服务器，Python 服务器连接到 Unity 的 TCP 端口，通过 `discover` 命令获取所有工具/资源/提示词定义，动态注册到 FastMCP。之后所有 MCP 调用都通过 Python → TCP → Unity 主线程执行。

---

## 运行时模式（实验性）

通过 MCP 控制运行中的游戏。在 `Player Settings > Scripting Define Symbols` 中添加 `UNITY_MCP_RUNTIME` 以启用。

运行时工具（8 个）：`runtime_get_stats` / `runtime_time_scale` / `runtime_load_scene` / `runtime_invoke` / `runtime_get_logs` / `runtime_profiler_snapshot` / `screenshot_game` / `screenshot_camera`

运行时服务器监听端口为 `port + 1`。

---

## Docker 部署

<details>
<summary><b>Docker 配置</b></summary>

```bash
cd unity-server

# stdio 模式（默认）
docker compose up -d

# Streamable HTTP 模式
UNITY_MCP_TRANSPORT=streamable-http docker compose up -d

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
| `UNITY_MCP_HOST` | `127.0.0.1` | Unity Editor 主机地址 |
| `UNITY_MCP_PORT` | `51279` | Unity Editor TCP 端口 |
| `UNITY_MCP_TIMEOUT` | `60` | 请求超时时间（秒） |
| `UNITY_MCP_TRANSPORT` | `stdio` | 传输模式：`stdio` 或 `streamable-http` |
| `UNITY_MCP_HTTP_PORT` | `8080` | HTTP 端口（仅 `streamable-http` 模式） |

</details>

---

## 设置

通过 `Window > Unity MCP` 访问：

| 设置 | 默认值 | 说明 |
|------|--------|------|
| Port | 51279 | TCP 端口，多实例场景可修改 |
| Auto Start | 开启 | Unity 启动时自动启动 MCP 服务 |
| Request Timeout | 60s | 工具执行最大超时 |
| Log Level | Info | Debug / Info / Warning / Error / Off |
| Audit Log | 关闭 | 记录每次工具调用及耗时 |
| Max Batch Operations | 50 | 单次 `batch_execute` 调用允许的最大操作数 |

---

## 常见问题

<details>
<summary><b>服务器未启动</b></summary>

- 检查 `Window > Unity MCP` 中的状态指示灯。绿色 = 运行中。
- 点击 **Restart** 按钮重启。
- 查看 Unity Console 中的 `[MCP]` 日志。
- 首次启动如果提示缺少 Python/uv，按照引导安装依赖。

</details>

<details>
<summary><b>MCP 客户端无法连接</b></summary>

- 确认客户端配置中的端口与 `Window > Unity MCP` 显示的端口一致。
- 确保 Python 3.10+ 和 `uvx` 已安装（运行 `uvx --version` 检查）。
- 如使用多实例，确保 `UNITY_MCP_PORT` 环境变量设置正确。
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

这是预期行为。Unity 脚本重编译时 TCP 连接会短暂中断，但会自动处理：

1. Unity 在域重载前发送 `notifications/reloading` 通知
2. TCP 断开后 Python 服务器自动进入指数退避重连（0s → 1s → 2s → 4s → ...）
3. 重连期间 MCP 客户端发来的请求会被缓存
4. Unity 重编译完成后 TCP 恢复，自动重放缓存的请求

整个过程对 MCP 客户端近乎透明，通常 2-5 秒内自动恢复。

</details>

---

## 系统要求

- **Unity** 2021.2+
- **Python** 3.10+（推荐使用 `uvx`，自动安装依赖）
- **Unity 依赖**：`com.unity.nuget.newtonsoft-json` 3.2.1+（自动解析）
- **Python 依赖**（自动安装）：`mcp`、`psd-tools`（PSD 解析）、`httpx`（蓝湖集成）

## 许可证

[MIT](LICENSE)
