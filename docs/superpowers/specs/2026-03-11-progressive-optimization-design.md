# Unity MCP 渐进式优化设计

> 日期：2026-03-11
> 状态：已批准
> 方案：方案 A — 渐进优化，不做大规模架构重写

---

## 1. 概述

基于对当前代码库的审计及行业同类项目的调研，本 spec 覆盖六个优化方向。

### 目标
- 去除 C# Bridge 层，Python Server 作为唯一 MCP 入口
- 统一 MCP 工具输出格式，提升 AI 理解一致性
- 升级设置窗口，改善开发者体验
- 添加 CI/CD 及 PyPI 自动发布
- 添加 LLM 引导文档（CLAUDE.md、AGENTS.md）
- 修复零散问题（Undo 覆盖、错误吞没、Python 私有 API 访问）

### 非目标
- 迁移到 SignalR/gRPC（保留现有 TCP 帧协议）
- 将 MCP Server 内嵌到 Unity 进程（会失去 Python 分析工具和域重载恢复能力）

---

## 2. 去除 Bridge，Python Server 作为唯一入口

### 当前架构
```
MCP 客户端 -> stdio -> unity-mcp-bridge (C# .NET 8) -> TCP -> Unity TcpTransport
MCP 客户端 -> stdio -> unity-mcp-server (Python FastMCP) -> TCP -> Unity TcpTransport
```

### 目标架构
```
MCP 客户端 -> stdio -> unity-mcp-server (Python FastMCP) -> TCP -> Unity TcpTransport
              （同时支持 Streamable HTTP 模式）
```

### 变更内容

#### 删除
- `unity-bridge/` — 整个 Bridge 项目
- `unity-mcp/Bridge~/` — 预编译二进制（4 个平台）
- `unity-mcp/Editor/Core/ServerProcessManager.cs` — 进程管理器
- `scripts/build_bridge.sh`（如存在）

#### 修改
- **`McpServer.cs`**：移除 `s_processManager` 字段及所有引用
- **`McpSettings.cs`**：移除 `ServerMode` 枚举、`BridgePath` 属性。保留 `PythonTransportMode`，将传输设置提升为主设置（不再是"Python 特定"的）
- **`McpSettingsWindow.cs`**：移除模式切换 UI、`DrawBuiltInSettings()`、Bridge 路径配置。简化 Server 标签页为单一模式

#### 将 Bridge 的域重载缓冲逻辑迁移到 Python

Bridge 的核心价值是域重载期间的请求缓冲（Program.cs 中的 `ConcurrentQueue<byte[]> s_pendingFrames`）。需要在 `unity_connection.py` 中复现：

- TCP 连接断开时切换到缓冲模式
- 将 `send_request()` 调用排入 `asyncio.Queue`
- 重连后排空队列（重放缓冲的请求）
- 收到 Unity 的 `notifications/reloading` 时主动准备断连
- 重连使用指数退避：0s, 300ms, 500ms, 1s, 1s, 2s, 2s, 3s, 3s, 5s（与 Bridge 相同）

#### 统一客户端配置
所有客户端配置方式统一：
```json
{
  "mcpServers": {
    "unity": {
      "type": "stdio",
      "command": "uvx",
      "args": ["unity-mcp-server"],
      "env": { "UNITY_MCP_PORT": "51279" }
    }
  }
}
```

`UNITY_MCP_PORT` 仅在端口非默认值（51279）时需要设置。

---

## 3. 统一 MCP 工具输出格式

### 问题
工具当前存在四种返回模式：`Json(new { success=true, ... })`、`Json(new { data })`、`Text("message")`、`Paginated(...)`。其中 `success` 字段是冗余的（MCP 协议已有 `isError`）。

### 规则

1. **读操作**（`ReadOnly=true`）：直接通过 `ToolResult.Json(data)` 返回数据，不含 `success` 字段。
   ```csharp
   // 之前
   return ToolResult.Json(new { success = true, name = go.name, components = ... });
   // 之后
   return ToolResult.Json(new { name = go.name, components = ... });
   ```

2. **写操作**：通过 `ToolResult.Json(data)` 返回被操作对象的关键信息，不含 `success` 或 `message` 字段。
   ```csharp
   // 之前
   return ToolResult.Json(new { success = true, instanceId = id, name, message = "Created..." });
   // 之后
   return ToolResult.Json(new { instanceId = id, name });
   ```

3. **纯副作用操作**（删除、刷新等）：通过 `ToolResult.Text(message)` 返回简洁描述。
   ```csharp
   return ToolResult.Text($"Destroyed GameObject '{goName}'");
   ```

4. **错误**：不变 — `ToolResult.Error(message)`。

5. **分页结果**：不变 — `ToolResult.Paginated(items, total, nextCursor)`。

6. **图片**：不变 — `ToolResult.Image(base64, mimeType, description)`。

### 影响范围
- `Editor/Tools/` 下约 25 个文件
- `Runtime/Tools/` 下约 6 个文件
- 机械性改动：移除 JSON 返回中的 `success` 字段和 `message` 字段

---

## 4. 设置窗口升级（迁移到 UI Toolkit）

将现有 935 行 IMGUI 窗口重写为 UI Toolkit，拆分为独立 Section 组件，提升可维护性和视觉品质。

### 4.0 架构设计

```
McpSettingsWindow.cs          — EditorWindow 主体，CreateGUI() + 标签页切换
├── UXML/McpSettingsWindow.uxml   — 主窗口布局
├── USS/McpSettingsWindow.uss     — 主窗口样式
└── Sections/
    ├── McpConnectionSection.cs   — 服务器状态、启动/停止按钮
    │   └── McpConnectionSection.uxml
    ├── McpClientConfigSection.cs — 客户端配置器列表
    │   └── McpClientConfigSection.uxml
    ├── McpToolsSection.cs        — 工具列表、搜索、启用/禁用
    │   └── McpToolsSection.uxml
    └── McpAdvancedSection.cs     — 高级设置 + 诊断信息
        └── McpAdvancedSection.uxml
```

每个 Section 是独立的类，接收 `VisualElement root` 参数，通过事件（`Action`/`event`）与其他 Section 通信。Section 间不直接引用。

### 4.1 Server/Connection 标签页
```
服务器状态指示灯 + 启动/停止/重启按钮
服务器设置
  端口（带 tooltip 说明默认值和多实例场景）
  自动启动（toggle）
  传输模式：Stdio / Streamable HTTP
  HTTP 端口（仅 Streamable HTTP 时显示）
高级设置（折叠区域）
  请求超时
  日志级别
  审计日志
  批处理最大操作数
```

### 4.2 客户端配置器架构重构

当前 4 个客户端的配置逻辑硬编码在 `McpSettingsWindow.cs` 中，扩展新客户端需要修改窗口代码。重构为声明式注册 + 策略模式：

**核心设计**：用数据驱动代替继承层次。每个客户端用一个 `ClientProfile` 数据对象描述，配合两种写入策略（JSON 文件 / CLI 命令），避免为每个客户端写一个子类。

```csharp
// 客户端描述（纯数据，无逻辑）
public class ClientProfile
{
    public string Id;              // "cursor", "claude-code"
    public string DisplayName;     // "Cursor"
    public string IconName;        // USS 图标类名（可选）
    public ConfigStrategy Strategy; // JsonFile / CliCommand
    public PlatformPaths Paths;    // Windows/Mac/Linux 配置路径
    public string[] InstallSteps;  // 安装引导文字
}

// 两种写入策略
public interface IConfigWriter
{
    McpStatus CheckStatus(ClientProfile profile);
    void WriteMcpConfig(ClientProfile profile);
    string GetManualSnippet(ClientProfile profile);
}

public class JsonFileConfigWriter : IConfigWriter { ... }   // 读写 JSON 配置文件
public class ClaudeCliConfigWriter : IConfigWriter { ... }  // 调用 claude mcp add/remove
```

**客户端注册**：通过静态列表声明，新增客户端只需加一条 `ClientProfile`：
```csharp
public static class ClientRegistry
{
    public static readonly ClientProfile[] All = new[]
    {
        new ClientProfile { Id = "claude-code", DisplayName = "Claude Code", Strategy = ConfigStrategy.CliCommand, ... },
        new ClientProfile { Id = "cursor", DisplayName = "Cursor", Strategy = ConfigStrategy.JsonFile, ... },
        // ... 更多客户端
    };
}
```

**支持的客户端**（从 4 个扩展到 12 个）：
| 客户端 | 写入策略 | 配置位置 |
|--------|---------|---------|
| Claude Code | CLI 命令 | `~/.claude.json` |
| Claude Desktop | JSON 文件 | 平台特定 |
| Cursor | JSON 文件 | `.cursor/mcp.json` |
| VS Code / Copilot | JSON 文件 | `.vscode/mcp.json` |
| Windsurf | JSON 文件 | `~/.codeium/windsurf/mcp_config.json` |
| Gemini CLI | JSON 文件 | `~/.gemini/settings.json` |
| Cline | JSON 文件 | VS Code 扩展设置 |
| Rider | JSON 文件 | 项目级 `.idea/mcp.json` |
| Kiro | JSON 文件 | `.kiro/mcp.json` |
| Codex | JSON 文件 | `~/.codex/config.json` |
| Copilot CLI | JSON 文件 | GitHub Copilot 配置 |
| Augment | JSON 文件 | 待确认 |

**UI 改进**：
- 客户端列表使用卡片式布局，每张卡片展示：名称、状态指示灯、配置路径
- 选中卡片展开操作区：Configure/Update 按钮、打开配置文件、复制手动配置、安装步骤
- 未安装的客户端灰显但仍可配置

### 4.3 Tools 标签页改进
- 搜索/过滤栏（`ToolbarSearchField`），支持按名称和描述过滤
- 工具列表使用 `ListView` + 虚拟化，支持大量工具高效渲染
- 工具分组显示：Built-in / Custom，使用 `Foldout`
- 每个工具行：名称 | 描述（截断） | 启用/禁用 Toggle

### 4.4 新增诊断区域（Advanced 标签页）
```
诊断信息
  TCP 端口状态：Listening / 被 PID xxx 占用
  最近工具调用（最近 5 条）：工具名 | 耗时 | 成功/失败
  Unity 版本 / 包版本 / Python Server 版本
  [复制诊断信息] 按钮 -> 复制到剪贴板用于提交 issue
```

### 4.5 帮助/文档区域
- 在版本号旁增加"文档"和"反馈问题"链接按钮

### 4.6 视觉设计与交互体验

设置窗口是用户接触本插件的第一印象，需要在 Unity 编辑器风格内做到**专业、清晰、高效**。

**布局原则**：
- 顶部固定区域：连接状态栏（状态指示灯 + 版本号 + 文档/反馈链接），始终可见
- 中部标签页内容区：Connection | Clients | Tools | Advanced，使用 `ToolbarToggle` 切换
- 标签页切换时内容平滑过渡，避免闪烁
- 最小窗口尺寸 500×400，内容区自适应窗口大小

**状态指示**：
- 连接状态使用颜色圆点：绿色（运行中）、黄色（重连中）、红色（已断开）、灰色（未启动）
- 客户端配置状态：绿色对勾（已配置且最新）、黄色感叹号（配置过期需更新）、灰色横线（未配置）
- 所有状态变化带 tooltip 说明具体原因

**交互细节**：
- 端口冲突时内联显示错误提示（红色文字 + 占用进程信息），而非弹窗
- 客户端 Configure 操作成功后即时更新状态指示，无需手动刷新
- Tools 列表的搜索栏支持实时过滤（输入即筛选，无需回车）
- 长操作（如 Configure）期间按钮显示加载状态，防止重复点击

**风格约束**：
- 使用 Unity 内置 CSS 变量（`--unity-colors-default-background` 等）适配 Light/Dark 主题
- 不使用自定义字体和花哨配色，保持与 Unity 原生窗口视觉一致
- 间距和对齐遵循 Unity Editor 的 4px/8px 网格系统
- 图标优先使用 Unity 内置 EditorIcons（`d_console.infoicon` 等），不引入外部图标资源

### 4.7 迁移说明
- **删除**现有 `McpSettingsWindow.cs`（935 行 IMGUI）
- **新建** `Window/` 目录结构：主窗口 + 4 个 Section（各含 .cs + .uxml）
- USS 样式通过 Unity 内置 CSS 变量适配 Light/Dark 主题
- 最低兼容 Unity 2021.2（UI Toolkit 在此版本稳定可用）

---

## 5. CI/CD 与 PyPI 自动发布

### 工作流

**`.github/workflows/ci.yml`** — PR/Push 验证
- 触发：push 到 main、PR 到 main
- Python：lint + 基础测试（`unity-server/`）
- Unity：通过 GameCI 运行编辑器测试（2021.3 + 2022.3 矩阵）

**`.github/workflows/release-server.yml`** — PyPI 发布
- 触发：push tag `server-v*`
- 步骤：校验 pyproject.toml 版本号与 tag 一致、构建、发布到 PyPI
- 认证：PyPI Trusted Publisher（无需 token）

### 版本管理
两个独立的版本轨道：
- **Unity 包**：`package.json` + `McpConst.cs`（保持同步）
- **Python Server**：`pyproject.toml`（独立版本）

**`scripts/bump-version.sh`**：
```bash
./scripts/bump-version.sh unity 1.2.0   # 更新 package.json + McpConst.cs
./scripts/bump-version.sh server 1.0.3  # 更新 pyproject.toml
```

---

## 6. LLM 引导文档

### CLAUDE.md（项目根目录）
内容：
- 项目概述（一句话 + 架构图）
- 数据流：MCP 客户端 <-> stdio <-> Python Server <-> TCP <-> Unity Editor
- 关键目录说明
- 关键不变量（端口、协议、工具命名规则、主线程约束）
- 如何添加新工具（C# 侧 + Python 自动发现）
- 如何添加资源/提示词
- 常见陷阱（域重载、端口冲突）
- 代码规约（命名、返回格式、Undo）

### AGENTS.md（项目根目录）
面向通用 AI Agent 的精简版：
- 如何构建/运行
- 可用工具/资源/提示词清单
- 何时更新此文件

---

## 7. 其他修复

### 7.1 Undo 覆盖补全
审查所有写操作工具，补充缺失的 Undo 调用：
- 创建对象：`Undo.RegisterCreatedObjectUndo`
- 修改属性前：`Undo.RecordObject`
- 删除对象：`Undo.DestroyObjectImmediate`
- 复用现有 `UndoHelper` 封装

### 7.2 错误吞没修复
将静默的 `catch { }` 改为至少记录日志：
- `TcpTransport.cs:186` — HandleClient 中的 IOException
- 实施阶段逐一审查其他位置

### 7.3 Python Server 私有 API 修复
`server.py:264` 访问 `server._tool_manager._tools`（FastMCP 内部 API）。用 try/except 包裹并在失败时优雅降级（跳过 schema 修补，功能不受影响）。

---

## 8. 实施优先级

| 优先级 | 任务 | 预估复杂度 |
|--------|------|-----------|
| P0 | 去除 Bridge + 将缓冲逻辑迁移到 Python | 高 |
| P0 | 统一输出格式 | 中（机械性改动） |
| P1 | 设置窗口 UI Toolkit 重写 + 客户端配置器架构 | 高 |
| P1 | CLAUDE.md + AGENTS.md | 低 |
| P1 | CI/CD + PyPI 发布 | 中 |
| P2 | Undo 覆盖补全 | 低 |
| P2 | 错误吞没修复 | 低 |
| P2 | Python 私有 API 修复 | 低 |

建议执行顺序：先完成 P0（有依赖关系 — Bridge 移除影响设置窗口），然后 P1 并行推进，最后 P2 作为收尾。
