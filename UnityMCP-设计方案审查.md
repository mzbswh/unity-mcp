# Unity MCP 设计方案审查报告

> 审查日期：2026-03-06
> 审查对象：UnityMCP-设计方案.md v1.0

---

## 审查结论

设计方案整体方向正确，内嵌 C# 架构 + 属性驱动注册的核心思路是 4 个参考项目中的最优组合。但存在 **4 个严重问题**、**6 个设计缺陷**和 **若干优化建议** 需要在编码前解决。

---

## 一、严重问题（必须修复，否则无法正常工作）

### S1. Streamable HTTP 规范理解偏差

**问题**：设计方案将 Streamable HTTP 简化为普通 HTTP POST 请求-响应模式，但 MCP 规范的 Streamable HTTP 传输实际上更复杂：

- **服务端需要支持 SSE（Server-Sent Events）**：MCP 规范要求 POST 请求的响应可以是 SSE 流（`Content-Type: text/event-stream`），用于支持服务端向客户端推送通知（如 `tools/list_changed`、进度更新等）
- **需要 Session 管理**：通过 `Mcp-Session-Id` 请求头维护会话状态
- **需要支持 GET 请求用于 SSE 连接**：客户端可通过 GET 建立独立的 SSE 通道接收服务端通知

**当前设计的影响**：
- 简单的 JSON 请求-响应模式**无法支持服务端主动推送**
- 部分 MCP 客户端可能在握手阶段就因为缺少 SSE 支持而失败
- `notifications/initialized` 等通知消息没有传输通道

**修复建议**：

方案 A（推荐）：**降级为 stdio 主传输 + HTTP 辅助**
```
MCP 客户端 ←—stdio—→ 内置 C# stdio 适配器（进程内）
                            ↓
                     Unity Editor API
```
- stdio 是 MCP 最简单、兼容性最好的传输
- Unity Editor 内嵌的 Server 不适合做完整的 Streamable HTTP（缺少 ASP.NET 管线）
- 提供一个 C# 控制台包装器（thin wrapper），通过 stdin/stdout 通信，内部通过 IPC 与 Unity 通信

方案 B：**实现完整的 Streamable HTTP（含 SSE）**
```csharp
// 当客户端 Accept 头包含 text/event-stream 时，切换到 SSE 模式
if (req.Headers["Accept"]?.Contains("text/event-stream") == true)
{
    resp.ContentType = "text/event-stream";
    resp.SendChunked = true;
    // 保持连接打开，推送事件...
}
```
- 实现成本更高
- HttpListener 支持 chunked 传输，技术上可行
- 需要维护长连接的生命周期管理

方案 C（折中）：**HTTP 请求-响应 + 轮询 fallback**
- 基本保持当前设计
- 对不支持 SSE 的场景，客户端通过轮询 `/mcp` 获取通知
- 兼容性最差但实现最简单

**结论**：建议采用 **方案 A** 的思路，主传输用 stdio，HTTP 仅作为辅助健康检查和调试端点。stdio 是所有 MCP 客户端（Claude Code、Cursor、Copilot）都原生支持的传输方式。

---

### S2. HttpListener 在 macOS 上的权限和兼容性问题

**问题**：`System.Net.HttpListener` 在不同平台上行为不一致：

- **macOS**：依赖 Mono 的 `HttpListener` 实现，某些 Unity 版本中不稳定
- **权限问题**：某些系统配置下，即使监听 localhost 也需要额外权限
- **Unity 2021.2 的 .NET 版本**：使用 .NET Standard 2.1 / Mono 运行时，HttpListener 的异步 API（`GetContextAsync`）可能表现与 .NET Core 不同

**影响**：在 macOS（Unity 开发者的主要平台之一）上可能出现无法启动或间歇性崩溃。

**修复建议**：
- 如果采用 S1 的方案 A（stdio 主传输），此问题自动消除
- 如果保留 HTTP，改用更可靠的 `TcpListener` + 手动解析 HTTP（参考 UnityNaturalMCP 的实际做法）
- 添加 try-catch 启动保护和 fallback 机制

---

### S3. MainThreadDispatcher.Run<T> 存在死锁风险

**问题**：同步版本 `Run<T>` 使用 `tcs.Task.GetAwaiter().GetResult()` 阻塞调用线程等待主线程完成。

```csharp
// 当前设计
public static T Run<T>(Func<T> func)
{
    var tcs = new TaskCompletionSource<T>();
    _queue.Enqueue(...);
    return tcs.Task.GetAwaiter().GetResult(); // 阻塞 HTTP 线程
}
```

**死锁场景**：
1. HTTP 线程调用 `Run<T>` → 阻塞等待主线程
2. 主线程的 Tool 实现内部又发起了 HTTP 请求（如调用 Package Manager API）
3. HTTP 线程池耗尽 → 新请求无法处理 → 死锁

**更严重的问题**：Unity Editor 的 `EditorApplication.update` 在某些情况下（如编译中、域重载中）不会被调用，此时 `Run<T>` 会永远阻塞。

**修复建议**：
```csharp
// 1. 移除同步版本 Run<T>，只保留异步版本 RunAsync<T>
// 2. 所有调用点改为 async/await
// 3. 添加超时机制
public static async Task<T> RunAsync<T>(Func<T> func, int timeoutMs = 30000)
{
    if (IsMainThread)
        return func();

    var tcs = new TaskCompletionSource<T>();
    var cts = new CancellationTokenSource(timeoutMs);
    cts.Token.Register(() => tcs.TrySetException(
        new TimeoutException($"Main thread execution timed out after {timeoutMs}ms")));

    _queue.Enqueue(new WorkItem(() =>
    {
        try { tcs.TrySetResult(func()); }
        catch (Exception ex) { tcs.TrySetException(ex); }
    }));

    return await tcs.Task;
}
```

---

### S4. ToolResult 方法重载歧义

**问题**：`ToolResult.Success(object)` 和 `ToolResult.Success(string)` 两个重载存在隐式转换问题。

```csharp
string msg = "hello";
ToolResult.Success(msg);    // 调用 Success(string) ✓
ToolResult.Success((object)msg);  // 调用 Success(object) → JToken.FromObject → 不同序列化行为

object result = GetSomeString();
ToolResult.Success(result);  // 调用 Success(object)，即使 result 实际是 string
```

**影响**：返回值序列化行为不可预测，可能导致 AI 客户端解析失败。

**修复建议**：
```csharp
public class ToolResult
{
    // 移除 Success(string) 重载，统一用 Success(object)
    // 或者改名避免歧义：
    public static ToolResult Text(string message) { ... }
    public static ToolResult Json(object data) { ... }
    public static ToolResult Error(string message) { ... }
    public static ToolResult Paginated(object items, int total, string nextCursor = null) { ... }
}
```

---

## 二、设计缺陷（应该修复，影响可靠性和功能完整性）

### D1. 缺少请求超时机制

**问题**：如果某个 Tool 执行卡住（如等待用户交互、触发长时间编译），HTTP 请求会无限期挂起。

**修复建议**：
- 在 `HttpTransport.HandleRequest` 中添加全局超时（默认 60 秒）
- 在 `ToolRegistry.InvokeTool` 中支持 per-tool 超时配置
- 超时后返回标准 JSON-RPC 超时错误

---

### D2. 程序集扫描逻辑有遗漏

**问题**：

```csharp
// 当前逻辑跳过所有 "Unity" 开头的程序集
if (name.StartsWith("System") || name.StartsWith("Unity") || name.StartsWith("mscorlib"))
    continue;
```

- 框架自身的程序集（如 `UnityMcp.Editor`）可能被跳过
- 用户程序集如果命名为 `UnityGame.Tools` 也会被跳过
- 某些第三方包的程序集可能包含有用的 MCP 工具也被跳过

**修复建议**：
```csharp
// 改用精确的排除列表
private static readonly HashSet<string> _skipPrefixes = new()
{
    "System.", "mscorlib", "netstandard",
    "Unity.Core", "UnityEngine", "UnityEditor",
    "Mono.", "nunit.", "Newtonsoft."
};

// 或更好的方案：只扫描包含 [assembly: ContainsMcpTools] 标记的程序集
[assembly: ContainsMcpTools]  // 在自己的 asmdef 和用户的 asmdef 中标记
```

---

### D3. Resource URI 模板匹配缺失

**问题**：设计了 `unity://gameobject/{id}` 和 `unity://assets/search/{filter}` 等带路径参数的 URI，但 ToolRegistry 使用精确匹配的 `Dictionary<string, ResourceEntry>`，无法处理模板。

```csharp
// 当前：精确匹配
_resources[resAttr.Uri] = new ResourceEntry { ... };

// 客户端请求 "unity://gameobject/12345" 无法匹配到 "unity://gameobject/{id}"
```

**修复建议**：
- 区分静态 URI 和模板 URI
- 对模板 URI 使用正则匹配或前缀树
- 将路径参数提取后传给 Resource 方法

```csharp
[McpResource("unity://gameobject/{id}", ...)]
public static object GetGameObject([Desc("Instance ID")] int id) { ... }
// 框架自动从 URI 中提取 {id} 绑定到参数
```

---

### D4. 域重载时请求丢失

**问题**：Unity 的 Assembly Reload 会触发 `Shutdown()` 销毁 HttpListener，然后 `[InitializeOnLoad]` 重新创建。在这个间隙（通常 1-5 秒）内：
- HttpListener 不存在，客户端请求会超时
- `ConcurrentQueue` 中的待处理项会丢失（static 字段在域重载时重置）
- 客户端收不到任何响应

**修复建议**：
- 将端口号持久化到 `EditorPrefs`，重启后复用同一端口
- 在 `Shutdown()` 中向客户端发送 503 Service Unavailable
- 考虑在域重载期间保持最小化的 HTTP 响应能力（返回 "reloading" 状态）
- 客户端侧（stdio 代理）添加重试机制

---

### D5. 缺少 CORS 头支持

**问题**：如果 MCP 客户端通过浏览器或 Electron 应用（如 VS Code）访问 HTTP 端点，跨域请求会被阻止。

**修复建议**：
```csharp
resp.Headers.Add("Access-Control-Allow-Origin", "*");
resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Mcp-Session-Id");

// 处理 OPTIONS 预检请求
if (req.HttpMethod == "OPTIONS")
{
    await WriteResponse(resp, 204, "");
    return;
}
```

---

### D6. batch_execute 优先级错误

**问题**：`batch_execute` 被归类为 P2（后续迭代），但它是**减少 Token 消耗和通信开销的核心机制**。

参考 unity-mcp-beta 的实际经验：批处理可将性能提升 10-100 倍。AI 在创建复杂场景时，经常需要一次创建十几个 GameObject 并添加组件。

**修复建议**：将 `batch_execute` 提升到 **P0**。

---

## 三、优化建议（提升体验和健壮性）

### O1. Tool 清单补充

当前清单遗漏了一些高频操作：

| 遗漏工具 | 描述 | 建议优先级 |
|----------|------|-----------|
| `asset_copy` | 复制资产 | P0 |
| `asset_move` | 移动/重命名资产 | P0 |
| `asset_get_info` | 获取资产元数据（类型、大小、依赖） | P1 |
| `editor_selection_get` | 获取当前选中的对象 | P1 |
| `editor_selection_set` | 设置选中对象 | P1 |
| `scene_list_all` | 列出 Build Settings 中的所有场景 | P0 |
| `gameobject_get_components` | 列出 GameObject 上的所有组件 | P0 |
| `component_list_all_types` | 列出所有可用组件类型 | P1 |
| `package_remove` | 移除包 | P1 |
| `prefab_unpack` | 解包 Prefab | P1 |

---

### O2. 错误响应标准化

当前 `ToolResult.Error(message)` 只有消息，缺少错误分类。

**建议**：
```csharp
public static ToolResult Error(string message, string code = "tool_error")
{
    return new ToolResult
    {
        IsSuccess = false,
        ErrorMessage = message,
        ErrorCode = code  // "validation_error", "not_found", "permission_denied", "timeout"
    };
}
```

---

### O3. 日志分级

当前所有日志都用 `Debug.Log` / `Debug.LogError`，缺少分级控制。

**建议**：在 `McpSettings` 中添加日志级别配置：
```csharp
public enum McpLogLevel { Off, Error, Warning, Info, Debug }
```

---

### O4. 工具执行审计日志

建议记录每次工具调用的审计日志，便于调试和回溯：

```
[UnityMCP] Tool: gameobject_create | Args: {name:"Player"} | 32ms | Success
[UnityMCP] Tool: component_add | Args: {target:"Player",type:"Rigidbody"} | 5ms | Success
[UnityMCP] Tool: script_create | Args: {name:"PlayerController"} | 128ms | Error: Path exists
```

---

### O5. Unity 类型 JSON 转换器覆盖不全

设计方案提到了 `UnityTypeConverters.cs` 但未详细设计。需要覆盖的 Unity 类型：

| 类型 | JSON 表示 |
|------|----------|
| `Vector2/3/4` | `{x, y, z, w}` |
| `Quaternion` | `{x, y, z, w}` 或 `{euler: {x, y, z}}` |
| `Color` | `{r, g, b, a}` 或 `"#RRGGBBAA"` |
| `Bounds` | `{center: {x,y,z}, size: {x,y,z}}` |
| `Rect` | `{x, y, width, height}` |
| `LayerMask` | `int` 或 `string[]` |
| `AnimationCurve` | `[{time, value, inTangent, outTangent}]` |

**建议**：为常见类型提供双向转换器，并支持用户注册自定义转换器。

---

### O6. 考虑添加 Tool 分组过滤

当工具数量达到 40+ 时，AI 的 tools/list 响应会很大（占用 Token）。

**建议**：支持按组过滤查询：
```json
{"method": "tools/list", "params": {"group": "gameobject"}}
```

---

## 四、设计方案中已做对的部分

以下设计决策经审查确认为最优，无需修改：

1. **内嵌架构**：零外部依赖，安装即用，最优部署方案
2. **属性驱动注册**：`[McpToolGroup]` + `[McpTool]` + `[Desc]`，扩展成本业界最低
3. **JSON Schema 自动生成**：从方法签名自动推导，消除手动维护负担
4. **ToolResult 统一返回模型**（修复 S4 后）：Success/Error/Paginated 三态覆盖所有场景
5. **UndoHelper 封装**：统一的 Undo 组管理，支持批处理原子回滚
6. **确定性端口**：SHA256 哈希避免冲突
7. **目录结构**：Core/Attributes/Models/Utils/Tools 分层清晰
8. **Newtonsoft.Json 选择**：Unity 内置，零额外依赖
9. **ConcurrentQueue + EditorApplication.update 调度**：简单高效，正确的线程安全方案
10. **批处理 + Undo 组原子回滚**：融合了 unity-mcp-beta 和 mcp-unity 的最佳实践

---

## 五、修改优先级总结

| 编号 | 类型 | 描述 | 优先级 | 影响范围 |
|------|------|------|--------|---------|
| S1 | 严重 | Streamable HTTP 规范偏差，建议改 stdio 主传输 | **最高** | 架构层 |
| S2 | 严重 | HttpListener macOS 兼容性 | **最高** | 传输层 |
| S3 | 严重 | MainThreadDispatcher 死锁和超时 | **最高** | 核心框架 |
| S4 | 严重 | ToolResult 重载歧义 | **高** | 数据模型 |
| D1 | 缺陷 | 请求超时机制缺失 | **高** | 传输层 |
| D2 | 缺陷 | 程序集扫描逻辑遗漏 | **高** | 注册中心 |
| D3 | 缺陷 | Resource URI 模板匹配 | **中** | 注册中心 |
| D4 | 缺陷 | 域重载请求丢失 | **中** | 传输层 |
| D5 | 缺陷 | 缺少 CORS 头 | **低** | 传输层 |
| D6 | 缺陷 | batch_execute 优先级错误 | **高** | 功能规划 |
| O1-O6 | 优化 | 工具补充/错误分类/日志/类型转换 | **低-中** | 各层 |

---

## 六、S1 传输层重新设计建议

鉴于 S1 是最关键的架构问题，这里给出详细的替代方案：

### 推荐方案：stdio 主传输 + HTTP 调试端点

```
┌─────────────────────────────────────────────────────────────────┐
│                    MCP 客户端 (AI 端)                            │
│         Claude Code / Cursor / Copilot / Gemini 等              │
└────────────────────────┬────────────────────────────────────────┘
                         │ stdio (stdin/stdout)
                         │ JSON-RPC 2.0
┌────────────────────────▼────────────────────────────────────────┐
│            stdio 桥接进程（轻量 C# 控制台应用）                   │
│  • 从 stdin 读取 JSON-RPC 请求                                  │
│  • 通过 TCP/NamedPipe 转发到 Unity 内嵌 Server                  │
│  • 从 Unity 接收响应写回 stdout                                  │
│  • 约 100 行 C#，编译为单文件可执行程序                          │
└────────────────────────┬────────────────────────────────────────┘
                         │ TCP localhost:{port}
                         │ 或 Named Pipe
┌────────────────────────▼────────────────────────────────────────┐
│                  Unity Editor 进程                               │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              TCP/Pipe 监听层                                │  │
│  │  • TcpListener（比 HttpListener 更可靠跨平台）             │  │
│  │  • 简单的消息帧协议（长度前缀 + JSON）                     │  │
│  │  • 辅助 HTTP 端点：GET /health（仅调试用）                 │  │
│  └────────────────────────┬──────────────────────────────────┘  │
│                           │                                     │
│               （以下与原设计相同）                                │
│          JsonRpcHandler → ToolRegistry → MainThreadDispatcher   │
└─────────────────────────────────────────────────────────────────┘
```

**优势**：
- stdio 是所有 MCP 客户端都支持的标准传输
- TCP 在所有平台上都稳定可靠（不依赖 HttpListener）
- 桥接进程极轻量（100 行 C#），可随 Unity 包分发预编译二进制
- 完全规避了 SSE/Session/CORS 等 HTTP 复杂性
- 桥接进程可自动检测 Unity 端口并连接

**客户端配置示例**：
```json
{
  "mcpServers": {
    "unity": {
      "command": "/path/to/unity-mcp-bridge",
      "args": ["--port", "52345"]
    }
  }
}
```
