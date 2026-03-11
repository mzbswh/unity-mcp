# Unity MCP 全面优化设计规格

> 基于 Coplay unity-mcp (主参考)、mcp-unity、Unity-MCP、UnityNaturalMCP 的对比分析

## 目标

将 unity-mcp 打造为功能最完备、工程质量最高、用户体验最好的开源 Unity MCP 实现。

## 架构概览（不变）

```
AI Client (Claude/Cursor/etc.)
  ↕ stdio / streamable-http (MCP JSON-RPC 2.0)
Python Server (unity-mcp-server, FastMCP)
  ↕ Custom TCP frame protocol (localhost)
Unity Editor Plugin (C#, TcpTransport)
  ↕ MainThreadDispatcher
Unity Engine APIs
```

---

## 一、通用分页框架 (P0)

### 问题
当前除 `SceneTools.GetHierarchy` 有简陋 cursor 外，其余 Tool 返回无限大 JSON。大场景查询浪费 LLM token。

### 设计

#### 1.1 PaginationHelper 工具类

```csharp
namespace UnityMcp.Shared.Utils
{
    public static class PaginationHelper
    {
        public const int DefaultPageSize = 50;

        public static (List<T> page, string nextCursor) Paginate<T>(
            IList<T> items, int pageSize = DefaultPageSize, string cursor = null)
        {
            int start = 0;
            if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out int ci))
                start = ci;
            pageSize = Math.Clamp(pageSize, 1, 200);
            var page = items.Skip(start).Take(pageSize).ToList();
            string next = (start + pageSize < items.Count)
                ? (start + pageSize).ToString() : null;
            return (page, next);
        }
    }
}
```

#### 1.2 PagedResult 统一返回格式

```csharp
public static class ToolResult
{
    public static ToolResult Paged<T>(IList<T> items, int totalCount,
        string nextCursor, object extra = null) { ... }
}
```

返回 JSON 格式：
```json
{
  "items": [...],
  "totalCount": 150,
  "returnedCount": 50,
  "nextCursor": "50"
}
```

#### 1.3 改造目标
以下 Tool 需要添加 `pageSize`/`cursor` 参数并使用分页：
- `SceneTools.GetHierarchy` — 改用 PaginationHelper（已有 cursor，统一实现）
- `GameObjectTools.Find` — 搜索结果可能很大
- `ComponentTools.ListComponents` — 组件列表
- `AssetTools.Search` — 资产搜索
- `ConsoleResources` — 日志可能上千条
- `ScriptTools.Search` — 脚本搜索结果
- `EditorTools.GetProjectSettings` — 设置项很多

---

## 二、依赖检测与安装引导 (P0)

### 问题
用户安装 UPM 包后，如果 Python/uv 环境缺失，只能从 Console 猜原因。

### 设计

#### 2.1 DependencyChecker 静态类

```csharp
namespace UnityMcp.Editor.Core
{
    public static class DependencyChecker
    {
        public struct DependencyStatus
        {
            public bool PythonFound;
            public string PythonVersion;  // e.g. "3.12.1"
            public bool UvFound;
            public string UvVersion;
            public bool UvxFound;
            public bool AllSatisfied => PythonFound && UvxFound;
        }

        public static DependencyStatus Check() { ... }
    }
}
```

实现：通过 `Process.Start` 执行 `python3 --version` / `uv --version` / `uvx --version`，解析 stdout。跨平台：Windows 用 `python`，Mac/Linux 用 `python3`。

#### 2.2 McpSetupWindow (UI Toolkit)

新建 `Editor/Window/McpSetupWindow.cs` + `.uxml` + `.uss`：
- 在 `McpServer` 首次启动时，如果 `DependencyChecker.Check().AllSatisfied == false`，自动弹出
- 显示 Python 和 uv/uvx 的状态指示灯（绿色/红色）
- 提供 "Open Python Download" / "Install uv" 链接按钮
- 底部 Refresh 按钮 + Done 按钮（依赖全部满足后才可点击）
- 状态写入 `EditorPrefs`，不再每次启动都弹

#### 2.3 触发时机

`McpServer.Initialize()` 中：
```csharp
var deps = DependencyChecker.Check();
if (!deps.AllSatisfied)
{
    McpSetupWindow.ShowWindow(deps);
    return; // 不启动 TCP listener
}
```

---

## 三、EditorState Resource 与项目信息 (P0)

### 问题
LLM 无法在调用工具前了解 Unity 编辑器的当前状态，可能在编译中调用场景操作导致失败。

### 设计

#### 3.1 新增 Resource: `unity://editor/state`

```csharp
[McpResource("unity://editor/state", "Editor State",
    "Real-time editor state: compiling, playing, focused, selection, scene")]
public static ToolResult GetEditorState()
{
    return ToolResult.Json(new
    {
        isCompiling = EditorApplication.isCompiling,
        isPlaying = EditorApplication.isPlaying,
        isPaused = EditorApplication.isPaused,
        isFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
        activeScene = new {
            name = SceneManager.GetActiveScene().name,
            path = SceneManager.GetActiveScene().path,
            isDirty = SceneManager.GetActiveScene().isDirty
        },
        selection = new {
            count = Selection.objects.Length,
            activeObject = Selection.activeGameObject?.name,
            activeInstanceId = Selection.activeGameObject?.GetInstanceID()
        },
        platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
        unityVersion = Application.unityVersion
    });
}
```

#### 3.2 新增 Resource: `unity://project/info`

```csharp
[McpResource("unity://project/info", "Project Info",
    "Project metadata: name, packages, render pipeline, scripting backend")]
public static ToolResult GetProjectInfo()
{
    return ToolResult.Json(new
    {
        productName = Application.productName,
        companyName = Application.companyName,
        version = Application.version,
        dataPath = Application.dataPath,
        renderPipeline = GetRenderPipelineName(),
        scriptingBackend = PlayerSettings.GetScriptingBackend(
            EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
        apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(
            EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
        packages = GetInstalledPackages()  // name + version 列表
    });
}
```

#### 3.3 新增 Resource: `unity://editor/selection`

```csharp
[McpResource("unity://editor/selection", "Current Selection",
    "Detailed info about currently selected objects in the editor")]
```

返回所有选中对象的 instanceId、name、type、path。

---

## 四、C# 端测试覆盖提升 (P0)

### 问题
仅 4 个测试文件，核心工具无测试。

### 设计

#### 4.1 测试策略

按优先级逐步添加 EditMode 测试：

**Phase 1 — 核心框架测试**（现有基础扩展）：
- `ToolRegistryTests` — 补充 Resource/Prompt 注册测试
- `RequestHandlerTests` — JSON-RPC 请求解析、错误响应
- `PaginationHelperTests` — 分页边界条件
- `DependencyCheckerTests` — mock 进程输出的解析

**Phase 2 — 工具测试**（需要 Unity Editor 环境）：
- `GameObjectToolsTests` — 扩展现有测试，覆盖 Find/Modify/Duplicate
- `SceneToolsTests` — GetHierarchy 分页、LoadScene
- `ComponentToolsTests` — Add/Remove/Modify
- `PrefabToolsTests` — Create/Instantiate/Unpack
- `MaterialToolsTests` — Create/Modify
- `AssetToolsTests` — Search/Import/Move

**Phase 3 — 集成测试**：
- `ClientConfigTests` — JsonFileConfigWriter/ClaudeCliConfigWriter 的读写逻辑
- `McpSettingsTests` — 设置持久化

#### 4.2 测试工具

创建 `Tests/Editor/TestUtilities.cs`：
- `CreateTempGameObject()` — 带自动清理
- `CreateTempScene()` — 测试场景管理
- `AssertJsonContains()` — ToolResult JSON 断言

---

## 五、Tool 领域补充 (P1)

### 5.1 CameraTools（新建）

```
Editor/Tools/CameraTools.cs
```
- `camera_create` — 创建指定类型相机（Perspective/Orthographic）
- `camera_configure` — 设置 FOV、near/far clip、clear flags、culling mask
- `camera_set_main` — 设置为 MainCamera tag
- `camera_look_at` — 朝向目标位置/对象
- `camera_get_info` — 获取相机详细参数

### 5.2 ScriptableObjectTools（新建）

```
Editor/Tools/ScriptableObjectTools.cs
```
- `so_create` — 创建指定类型的 SO 资产
- `so_read` — 读取 SO 所有序列化字段
- `so_modify` — 修改 SO 字段值
- `so_list` — 按类型列出所有 SO 资产

### 5.3 TextureTools（新建）

```
Editor/Tools/TextureTools.cs
```
- `texture_get_info` — 读取纹理信息（尺寸、格式、压缩、mipmap）
- `texture_set_import` — 修改导入设置（maxSize、compression、filterMode）
- `texture_search` — 按条件搜索纹理资产

### 5.4 MenuItemTools（新建）

```
Editor/Tools/MenuItemTools.cs
```
- `menu_execute` — 通过名称执行任意 Unity 菜单项
- `menu_list` — 列出所有可用菜单项（分页）

### 5.5 现有工具增强

- `EditorTools` 增加 `editor_refresh` — 强制 AssetDatabase.Refresh
- `EditorTools` 增加 `editor_set_selection` — 通过 instanceId/name 设置编辑器选中对象
- `ScriptTools` 增加脚本编译状态检查

---

## 六、版本更新检查 (P1)

### 设计

#### 6.1 PackageUpdateChecker

```csharp
namespace UnityMcp.Editor.Core
{
    public static class PackageUpdateChecker
    {
        // 每天检查一次 GitHub raw package.json
        private const string PackageJsonUrl =
            "https://raw.githubusercontent.com/mzbswh/unity-mcp/main/unity-mcp/package.json";

        public static void CheckOncePerDay() { ... }
    }
}
```

- 通过 `UnityWebRequest` 拉取远程 `package.json` 的 `version` 字段
- 与本地 `McpConst.ServerVersion` 比较
- 缓存到 `EditorPrefs["UnityMcp.LastUpdateCheck"]` / `EditorPrefs["UnityMcp.LatestVersion"]`
- 如有新版本，在 Settings Window header 显示更新提示（黄色徽章）

#### 6.2 触发时机

`McpServer.Initialize()` 中调用 `PackageUpdateChecker.CheckOncePerDay()`。

---

## 七、Settings Window 增强 (P1)

### 7.1 Sections 独立 UXML 化

当前 4 个 Section 的 UI 布局在 C# 中用代码构建。改为每个 Section 有独立的 `.uxml` 模板文件，C# 只负责事件绑定和逻辑。

文件结构：
```
Editor/Window/Sections/
  McpConnectionSection.cs + .uxml
  McpClientConfigSection.cs + .uxml
  McpToolsSection.cs + .uxml
  McpAdvancedSection.cs + .uxml
  McpResourcesSection.cs + .uxml     ← 新增
  McpValidationSection.cs + .uxml    ← 新增（如果加脚本验证）
```

### 7.2 新增 Resources Section

显示所有已注册的 Resource：URI、名称、描述。和 Tools Section 类似的列表+搜索。

### 7.3 Advanced Section 增强

- **调用日志**: 最近 20 次工具调用的 name + 耗时 + 成功/失败，环形缓冲区存储
- **连接统计**: 重连次数、平均延迟
- **Server Source Override**: 可选字段，指定本地 Python 源码路径用于开发调试
- **更新提示**: 如果 PackageUpdateChecker 检测到新版本，显示黄色提示栏

---

## 八、连接诊断增强 (P1)

### 8.1 ToolCallLogger

```csharp
namespace UnityMcp.Editor.Core
{
    public static class ToolCallLogger
    {
        public struct CallRecord
        {
            public string ToolName;
            public long DurationMs;
            public bool Success;
            public DateTime Timestamp;
        }

        private static readonly CircularBuffer<CallRecord> _history = new(20);

        public static void Log(string tool, long ms, bool success) { ... }
        public static IReadOnlyList<CallRecord> GetHistory() => _history.ToList();
    }
}
```

在 `RequestHandler.HandleRequest()` 中，计时并记录每次工具调用。

### 8.2 ConnectionStats

在 `TcpTransport` 中增加：
- `ReconnectCount` 属性
- `LastConnectedAt` 属性
- `AverageResponseMs` 属性（滑动窗口计算）

---

## 九、ServiceLocator 解耦 (P2)

### 设计

创建轻量的 ServiceLocator（不引入完整 DI 框架）：

```csharp
namespace UnityMcp.Editor.Core
{
    public static class McpServices
    {
        public static IToolRegistry ToolRegistry { get; set; }
        public static ITcpTransport Transport { get; set; }
        public static IRequestHandler RequestHandler { get; set; }

        public static void Reset() { ... } // 用于测试
    }
}
```

提取 `IToolRegistry`、`ITcpTransport`、`IRequestHandler` 接口。`McpServer` 初始化时注册实现。测试时可注册 mock。

---

## 十、Python Server 端增强 (P2)

### 10.1 集成现有 Python 工具

`tools/asset_validator.py` 和 `tools/script_analyzer.py` 已存在但未注册到 FastMCP。将它们作为纯 Python 端工具注册：
- `asset_validate` — 不经 Unity，直接分析项目目录下的资产文件
- `script_analyze` — 不经 Unity，直接用 AST 分析 C# 脚本结构

### 10.2 Python 端独立 Resource

添加 `unity://server/status` Resource：返回 Python Server 自身状态（连接状态、已注册工具数、缓冲区队列长度、运行时间）。

---

## 十一、LLM 生态文件 (P2)

### 11.1 llms.txt

创建项目根目录 `llms.txt`，遵循 llms.txt 标准格式：
```
# Unity MCP
> Model Context Protocol server for Unity Editor

## Tools
- gameobject_create: Create a new GameObject
- gameobject_destroy: Delete a GameObject
...（列出所有工具的 name: description）

## Resources
- unity://scene/hierarchy: Scene hierarchy tree
...
```

### 11.2 server.json

创建 MCP 服务器发现文件 `server.json`：
```json
{
  "name": "unity-mcp-server",
  "version": "1.0.0",
  "description": "MCP server for Unity Editor",
  "transport": ["stdio", "streamable-http"]
}
```

---

## 约束与原则

1. **不抄袭**: 仅参考 Coplay 等项目的架构模式和功能范围，所有代码原创
2. **YAGNI**: 每个新增功能必须有明确的用户场景
3. **向后兼容**: 新增分页参数都有默认值，不破坏现有 Tool 调用
4. **测试先行**: 新功能需附带测试
5. **最小侵入**: 优先扩展而非重写现有代码

---

## 执行顺序

```
Phase 1 (P0): 分页框架 → 依赖检测 → EditorState Resource → 核心测试
Phase 2 (P1): Tool 领域补充 → 版本更新 → Settings 增强 → 诊断增强
Phase 3 (P2): ServiceLocator → Python 增强 → LLM 生态文件
```
