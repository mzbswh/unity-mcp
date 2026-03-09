# Unity MCP 设计方案 v2.1 增量优化

> 基于对 4 个参考项目实际源码的交叉验证
> 每个修改标注参考来源

---

## [修改] 2.1 TCP 消息帧协议 — 增加消息类型标识

**原文**: `消息帧: [4字节长度前缀] + [UTF-8 JSON 负载]`

**问题**: 当前协议无法区分请求（需要响应）、响应、通知（无需响应）三种消息类型。参考 mcp-unity 的 WebSocket 实现，请求有 `id` 字段而通知没有，但 TCP 帧层面没有区分机制，可能导致桥接进程无法正确路由。

**修改为**:

```
消息帧: [4字节长度 BE] + [1字节类型] + [UTF-8 JSON 负载]

类型字节:
  0x01 = Request    (有 id, 需要响应)
  0x02 = Response   (有 id, 对应请求的响应)
  0x03 = Notification (无 id, 单向消息)
```

**来源**: MCP 规范要求区分 request/response/notification 三种消息。参考 unity-mcp-beta 的 WebSocket 传输中通过 `type` 字段区分 `register`/`command_result`/`pong` 等消息类型 (`WebSocketTransportClient.cs` 第 28-37 行)。

```csharp
// TcpTransport 写消息
private async Task WriteMessage(NetworkStream stream, byte msgType, string json, CancellationToken ct)
{
    var payload = Encoding.UTF8.GetBytes(json);
    var header = new byte[5];
    // 4 字节长度（不含 header 本身）= 1 + payload.Length
    int frameLen = 1 + payload.Length;
    header[0] = (byte)(frameLen >> 24);
    header[1] = (byte)(frameLen >> 16);
    header[2] = (byte)(frameLen >> 8);
    header[3] = (byte)(frameLen);
    header[4] = msgType;
    await stream.WriteAsync(header, 0, 5, ct);
    await stream.WriteAsync(payload, 0, payload.Length, ct);
    await stream.FlushAsync(ct);
}
```

---

## [修改] 2.3 MCP 方法 — 补充完整 initialize 响应结构

**原文**: 仅列出方法名和简要描述。

**问题**: initialize 是最关键的握手步骤，v2.0 没有定义 capabilities 的具体 JSON 结构。

**修改为** — 增加完整的 initialize 请求/响应规范：

```json
// 客户端 → 服务器: initialize 请求
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "capabilities": {
      "sampling": {},
      "roots": { "listChanged": true }
    },
    "clientInfo": {
      "name": "Claude Code",
      "version": "1.0.0"
    }
  }
}

// 服务器 → 客户端: initialize 响应
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2024-11-05",
    "capabilities": {
      "tools": { "listChanged": false },
      "resources": { "listChanged": false },
      "prompts": { "listChanged": false }
    },
    "serverInfo": {
      "name": "Unity MCP",
      "version": "1.0.0"
    }
  }
}

// 客户端 → 服务器: initialized 通知（无 id, 不需要响应）
{
  "jsonrpc": "2.0",
  "method": "notifications/initialized",
  "params": {}
}
```

**来源**: 参考 mcp-unity-main `Server~/src/index.ts` 第 44-56 行的 McpServer 初始化，以及 MCP 协议规范 `protocolVersion: "2024-11-05"`。所有 3 个参考项目都使用相同的 capabilities 结构。

**RequestHandler 中的实现**:

```csharp
private JObject HandleInitialize(JObject request)
{
    var clientInfo = request["params"]?["clientInfo"];
    McpLogger.Info($"MCP client connected: {clientInfo?["name"]} v{clientInfo?["version"]}");

    return new JObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = request["id"],
        ["result"] = new JObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JObject
            {
                ["tools"] = new JObject { ["listChanged"] = false },
                ["resources"] = new JObject { ["listChanged"] = false },
                ["prompts"] = new JObject { ["listChanged"] = false }
            },
            ["serverInfo"] = new JObject
            {
                ["name"] = "Unity MCP",
                ["version"] = McpSettings.Instance.Version
            }
        }
    };
}
```

---

## [修改] 6.1 McpToolAttribute — 补充高级参数

**原文**: McpToolAttribute 仅有 Name 和 Description。

**问题**: 参考项目提供了更丰富的工具元数据。

**修改为**:

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class McpToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    // v2.1 新增 — 参考 Unity-MCP McpPluginTool 属性
    /// <summary>工具显示标题 (如 "GameObject / Create")</summary>
    public string Title { get; set; }

    /// <summary>标记为幂等操作 (重复调用不产生副作用)</summary>
    public bool Idempotent { get; set; } = false;

    /// <summary>标记为只读操作 (不修改场景/资产状态)</summary>
    public bool ReadOnly { get; set; } = false;

    // v2.1 新增 — 参考 unity-mcp-beta McpForUnityToolAttribute
    /// <summary>工具分组 (用于 tools/list 过滤)</summary>
    public string Group { get; set; } = "core";

    /// <summary>是否默认启用 (false 则需在设置中手动启用)</summary>
    public bool AutoRegister { get; set; } = true;

    public McpToolAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
```

**来源**:
- `IdempotentHint` / `Title`: Unity-MCP-Plugin `Assets.Refresh.cs` 中 `[McpPluginTool(AssetsRefreshToolId, Title = "Assets / Refresh", IdempotentHint = true)]`
- `Group` / `AutoRegister`: unity-mcp-beta `McpForUnityToolAttribute.cs` 中的 `Group` 和 `AutoRegister` 属性
- `ReadOnly`: MCP 规范中 tool annotations 的 `readOnlyHint`

---

## [修改] 9.2 MainThreadDispatcher — 对齐参考项目的边界情况处理

**原文**: 使用 ConcurrentQueue + EditorApplication.update。

**问题**: 未处理域重载时的 MainThreadId 重置和编译期间 update 暂停。

**修改为** — 增加以下处理（参考 Unity-MCP-Plugin `MainThreadDispatcher.cs`）:

```csharp
[InitializeOnLoad]
public static class MainThreadDispatcher
{
    private static readonly ConcurrentQueue<WorkItem> _queue = new();
    private static int _mainThreadId;

    static MainThreadDispatcher()
    {
        // 域重载后重新捕获主线程 ID
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        EditorApplication.update += ProcessQueue;

        // 编译完成后重新初始化（参考 Unity-MCP CompilationPipeline.compilationFinished）
        UnityEditor.Compilation.CompilationPipeline.compilationFinished += _ =>
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        };
    }

    public static bool IsMainThread =>
        Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    public static Task<T> RunAsync<T>(Func<T> func, int timeoutMs = 30000)
    {
        if (IsMainThread)
        {
            try { return Task.FromResult(func()); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }

        var tcs = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => tcs.TrySetException(
            new TimeoutException(
                $"Main thread execution timed out after {timeoutMs}ms. " +
                "Unity may be compiling or in a modal dialog.")),
            useSynchronizationContext: false);

        _queue.Enqueue(new WorkItem(() =>
        {
            if (cts.IsCancellationRequested) return;
            try { tcs.TrySetResult(func()); }
            catch (Exception ex) { tcs.TrySetException(ex); }
            finally { cts.Dispose(); }
        }));

        return tcs.Task;
    }

    public static Task RunAsync(Action action, int timeoutMs = 30000)
    {
        return RunAsync(() => { action(); return (object)null; }, timeoutMs);
    }

    private static void ProcessQueue()
    {
        // 每帧预算：避免卡顿
        int budget = 10;
        while (budget-- > 0 && _queue.TryDequeue(out var item))
        {
            try { item.Execute(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    private readonly struct WorkItem
    {
        private readonly Action _action;
        public WorkItem(Action action) => _action = action;
        public void Execute() => _action();
    }
}
```

**来源**: Unity-MCP-Plugin `MainThreadDispatcher.cs` 中 `CompilationPipeline.compilationFinished += OnCompilationFinished` 回调确保编译后主线程 ID 正确。

---

## [修改] 9.5 ServerProcessManager — 增加崩溃检测和自动恢复

**原文**: 仅有启动和停止逻辑。

**问题**: 缺少 Unity-MCP 中的关键特性：启动验证、进程崩溃事件、孤立进程清理、域重载恢复。

**修改为**:

```csharp
public class ServerProcessManager
{
    private readonly McpSettings _settings;
    private Process _serverProcess;
    private const string PidEditorPrefKey = "UnityMcp_ServerPID";

    public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;

    public ServerProcessManager(McpSettings settings)
    {
        _settings = settings;
    }

    /// <summary>启动 Server 进程（或恢复域重载前的进程）</summary>
    public void StartServer()
    {
        // 1. 尝试恢复现有进程（域重载场景）
        if (TryRecoverExistingProcess())
            return;

        // 2. 清理孤立进程
        CleanupOrphanedProcess();

        // 3. 启动新进程
        LaunchNewProcess();

        // 4. 延迟验证启动成功
        ScheduleStartupVerification();
    }

    /// <summary>域重载后尝试恢复已有进程</summary>
    private bool TryRecoverExistingProcess()
    {
        // 参考 Unity-MCP McpServerManager.CheckExistingProcess() 第 528-566 行
        int savedPid = EditorPrefs.GetInt(PidEditorPrefKey, -1);
        if (savedPid <= 0) return false;

        try
        {
            var process = Process.GetProcessById(savedPid);
            if (process != null && !process.HasExited)
            {
                _serverProcess = process;
                _serverProcess.EnableRaisingEvents = true;
                _serverProcess.Exited += OnProcessExited;
                McpLogger.Info($"Recovered existing server process (PID: {savedPid})");
                return true;
            }
        }
        catch (ArgumentException) { } // 进程不存在
        catch (InvalidOperationException) { }

        EditorPrefs.DeleteKey(PidEditorPrefKey);
        return false;
    }

    /// <summary>清理可能遗留的孤立进程</summary>
    private void CleanupOrphanedProcess()
    {
        // 参考 Unity-MCP McpServerManager.KillOrphanedServerProcesses() 第 827-892 行
        // 使用 netstat (Windows) 或 lsof (macOS/Linux) 检测端口占用
        int port = _settings.Port;
        int? listeningPid = GetPidListeningOnPort(port);
        if (listeningPid == null) return;

        try
        {
            var process = Process.GetProcessById(listeningPid.Value);
            if (process.HasExited) return;

            McpLogger.Warning($"Port {port} occupied by PID {listeningPid}. Terminating...");
            process.Kill();
            process.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            McpLogger.Warning($"Failed to clean orphaned process: {ex.Message}");
        }
    }

    private void LaunchNewProcess()
    {
        var startInfo = _settings.Mode switch
        {
            McpSettings.ServerMode.BuiltIn => CreateBridgeStartInfo(),
            McpSettings.ServerMode.Python => CreatePythonStartInfo(),
            _ => null
        };
        if (startInfo == null) return;

        _serverProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        _serverProcess.Exited += OnProcessExited;
        _serverProcess.Start();

        EditorPrefs.SetInt(PidEditorPrefKey, _serverProcess.Id);
        McpLogger.Info($"Started {_settings.Mode} server (PID: {_serverProcess.Id})");
    }

    /// <summary>延迟 5 秒验证进程是否存活（捕获早期崩溃）</summary>
    private void ScheduleStartupVerification()
    {
        // 参考 Unity-MCP McpServerManager.ScheduleStartupVerification() 第 990-1036 行
        int pid = _serverProcess?.Id ?? -1;
        double checkTime = EditorApplication.timeSinceStartup + 5.0;

        void Check()
        {
            if (EditorApplication.timeSinceStartup < checkTime) return;
            EditorApplication.update -= Check;

            if (_serverProcess == null || _serverProcess.HasExited)
            {
                McpLogger.Error($"Server process (PID: {pid}) exited within 5s of startup. " +
                               "Check port availability or server logs.");
                Cleanup();
            }
        }

        EditorApplication.update += Check;
    }

    /// <summary>进程意外退出回调</summary>
    private void OnProcessExited(object sender, EventArgs e)
    {
        McpLogger.Warning("Server process exited unexpectedly");
        // 回到主线程执行清理
        MainThreadDispatcher.RunAsync(() => { Cleanup(); });
    }

    public void StopServer()
    {
        if (!IsRunning) return;

        try
        {
            // 参考 Unity-MCP: Unix 先 SIGTERM, Windows 直接 Kill
            #if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            using var kill = Process.Start(new ProcessStartInfo
            {
                FileName = "kill",
                Arguments = $"-TERM {_serverProcess.Id}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            kill?.WaitForExit(1000);
            if (!_serverProcess.HasExited)
                _serverProcess.Kill();
            #else
            _serverProcess.Kill();
            #endif

            _serverProcess.WaitForExit(5000);
        }
        catch { }
        finally
        {
            Cleanup();
        }
    }

    private void Cleanup()
    {
        _serverProcess = null;
        EditorPrefs.DeleteKey(PidEditorPrefKey);
    }

    private static int? GetPidListeningOnPort(int port)
    {
        #if UNITY_EDITOR_WIN
        string cmd = "netstat"; string args = $"-ano -p tcp";
        #else
        string cmd = "lsof"; string args = $"-ti tcp:{port} -sTCP:LISTEN";
        #endif

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cmd, Arguments = args,
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            #if UNITY_EDITOR_WIN
            // 解析 netstat 输出找到监听指定端口的 PID
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains($":{port}") && line.Contains("LISTENING"))
                {
                    var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (int.TryParse(parts[^1], out int pid)) return pid;
                }
            }
            #else
            if (int.TryParse(output.Trim(), out int pid)) return pid;
            #endif
        }
        catch { }
        return null;
    }

    // CreateBridgeStartInfo() 和 CreatePythonStartInfo() 保持 v2.0 不变
}
```

**来源**: Unity-MCP-Plugin `McpServerManager.cs`:
- 域重载恢复: `CheckExistingProcess()` 第 528-566 行
- 孤立进程清理: `KillOrphanedServerProcesses()` 第 827-892 行 + `GetPidListeningOnPort()` 第 898-962 行
- 启动验证: `ScheduleStartupVerification()` 第 990-1036 行
- 平台信号: `SendTerminateSignal()` 第 731-757 行

---

## [修改] 6.3 ToolRegistry — 使用 TypeCache 优化扫描

**原文**: 遍历 `AppDomain.CurrentDomain.GetAssemblies()` + `[assembly: ContainsMcpTools]` 过滤。

**问题**: Unity 提供了 `TypeCache` API（2019.2+），专为高性能类型查找设计，比手动遍历程序集快 10-100x。

**修改为**:

```csharp
public void ScanMarkedAssemblies()
{
    _tools.Clear();
    _resources.Clear();
    _prompts.Clear();

    // 使用 TypeCache 替代手动程序集遍历
    // 参考 unity-mcp-beta ToolDiscoveryService.cs 的 TypeCache.GetTypesWithAttribute
    var toolGroupTypes = UnityEditor.TypeCache.GetTypesWithAttribute<McpToolGroupAttribute>();

    foreach (var type in toolGroupTypes)
    {
        object instance = null;
        if (!type.IsAbstract && !type.IsStatic() && type.GetConstructor(Type.EmptyTypes) != null)
            instance = Activator.CreateInstance(type);

        ScanType(type, instance);
    }

    McpLogger.Info($"Discovered {_tools.Count} tools, {_resources.Count} resources, " +
                   $"{_prompts.Count} prompts from {toolGroupTypes.Count} tool groups");
}
```

**来源**: unity-mcp-beta `ToolDiscoveryService.cs` 中 `TypeCache.GetTypesWithAttribute<McpForUnityToolAttribute>()` 的用法。TypeCache 是 Unity 内部索引，避免了加载所有类型元数据的开销。

**附带修改**: 移除 `[assembly: ContainsMcpTools]` 属性要求。改用 TypeCache 后，只需 `[McpToolGroup]` 标记类即可被发现，降低扩展门槛。

---

## [新增] 6.7 UnityTypeConverters — 完整类型转换器

v2.0 提到了 `UnityTypeConverters.cs` 但未定义具体实现。基于 Unity-MCP-Plugin 的 Converter 目录补充完整设计。

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp.Editor.Utils
{
    /// <summary>注册所有 Unity 类型转换器</summary>
    public static class UnityTypeConverters
    {
        private static bool _registered;

        public static void Register(JsonSerializer serializer)
        {
            if (_registered) return;
            serializer.Converters.Add(new Vector2Converter());
            serializer.Converters.Add(new Vector3Converter());
            serializer.Converters.Add(new Vector4Converter());
            serializer.Converters.Add(new QuaternionConverter());
            serializer.Converters.Add(new ColorConverter());
            serializer.Converters.Add(new BoundsConverter());
            serializer.Converters.Add(new RectConverter());
            _registered = true;
        }

        /// <summary>生成 Unity 类型对应的 JSON Schema</summary>
        public static JObject GetSchema(Type type)
        {
            if (type == typeof(Vector2))
                return ObjectSchema(("x", "number"), ("y", "number"));
            if (type == typeof(Vector3))
                return ObjectSchema(("x", "number"), ("y", "number"), ("z", "number"));
            if (type == typeof(Vector4) || type == typeof(Quaternion))
                return ObjectSchema(("x", "number"), ("y", "number"),
                                    ("z", "number"), ("w", "number"));
            if (type == typeof(Color))
                return ObjectSchemaWithConstraints(
                    ("r", "number", 0, 1), ("g", "number", 0, 1),
                    ("b", "number", 0, 1), ("a", "number", 0, 1));
            if (type == typeof(Bounds))
                return new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["center"] = GetSchema(typeof(Vector3)),
                        ["size"] = GetSchema(typeof(Vector3))
                    }
                };
            if (type == typeof(Rect))
                return ObjectSchema(("x", "number"), ("y", "number"),
                                    ("width", "number"), ("height", "number"));
            if (type.IsEnum)
                return new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray(Enum.GetNames(type))
                };

            return null; // 非 Unity 类型，走默认处理
        }

        private static JObject ObjectSchema(params (string name, string type)[] fields)
        {
            var props = new JObject();
            var required = new JArray();
            foreach (var (name, type) in fields)
            {
                props[name] = new JObject { ["type"] = type };
                required.Add(name);
            }
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = required,
                ["additionalProperties"] = false
            };
        }

        private static JObject ObjectSchemaWithConstraints(
            params (string name, string type, double min, double max)[] fields)
        {
            var props = new JObject();
            var required = new JArray();
            foreach (var (name, type, min, max) in fields)
            {
                props[name] = new JObject
                {
                    ["type"] = type,
                    ["minimum"] = min,
                    ["maximum"] = max
                };
                required.Add(name);
            }
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = required,
                ["additionalProperties"] = false
            };
        }
    }

    // --- 转换器示例 ---

    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType,
            Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Vector3(
                obj["x"]?.Value<float>() ?? 0f,
                obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f);
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WritePropertyName("z"); writer.WriteValue(value.z);
            writer.WriteEndObject();
        }
    }

    public class ColorConverter : JsonConverter<Color>
    {
        // 参考 Unity-MCP-Plugin ColorConverter: RGBA [0,1] 范围约束
        public override Color ReadJson(JsonReader reader, Type objectType,
            Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Color(
                obj["r"]?.Value<float>() ?? 0f,
                obj["g"]?.Value<float>() ?? 0f,
                obj["b"]?.Value<float>() ?? 0f,
                obj["a"]?.Value<float>() ?? 1f);
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r"); writer.WriteValue(value.r);
            writer.WritePropertyName("g"); writer.WriteValue(value.g);
            writer.WritePropertyName("b"); writer.WriteValue(value.b);
            writer.WritePropertyName("a"); writer.WriteValue(value.a);
            writer.WriteEndObject();
        }
    }

    public class QuaternionConverter : JsonConverter<Quaternion>
    {
        // 参考 Unity-MCP-Plugin QuaternionConverter: w 默认为 1 (恒等四元数)
        public override Quaternion ReadJson(JsonReader reader, Type objectType,
            Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Quaternion(
                obj["x"]?.Value<float>() ?? 0f,
                obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f,
                obj["w"]?.Value<float>() ?? 1f);
        }

        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WritePropertyName("z"); writer.WriteValue(value.z);
            writer.WritePropertyName("w"); writer.WriteValue(value.w);
            writer.WriteEndObject();
        }
    }
}
```

**来源**: Unity-MCP-Plugin `Converter/` 目录下的 `Vector3Converter.cs`, `ColorConverter.cs`, `QuaternionConverter.cs`。Color 的 `[0,1]` 范围约束来自 Unity-MCP 的 `minimum`/`maximum` schema 属性。

---

## [新增] 6.8 ParameterBinder — 完整参数绑定策略

```csharp
using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp.Editor.Utils
{
    public static class ParameterBinder
    {
        /// <summary>将 JObject 参数绑定到方法签名</summary>
        public static object[] Bind(MethodInfo method, JObject arguments)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var token = arguments?[param.Name];

                if (token == null || token.Type == JTokenType.Null)
                {
                    if (param.HasDefaultValue)
                        args[i] = param.DefaultValue;
                    else if (IsNullable(param.ParameterType))
                        args[i] = null;
                    else
                        throw new ArgumentException(
                            $"Required parameter '{param.Name}' not provided");
                }
                else
                {
                    args[i] = ConvertParameter(token, param.ParameterType, param.Name);
                }
            }

            return args;
        }

        private static object ConvertParameter(JToken token, Type targetType, string paramName)
        {
            // 1. Unity 特殊类型
            if (targetType == typeof(Vector2))
                return new Vector2(token["x"]?.Value<float>() ?? 0, token["y"]?.Value<float>() ?? 0);
            if (targetType == typeof(Vector3))
                return new Vector3(token["x"]?.Value<float>() ?? 0, token["y"]?.Value<float>() ?? 0,
                                   token["z"]?.Value<float>() ?? 0);
            if (targetType == typeof(Vector4))
                return new Vector4(token["x"]?.Value<float>() ?? 0, token["y"]?.Value<float>() ?? 0,
                                   token["z"]?.Value<float>() ?? 0, token["w"]?.Value<float>() ?? 0);
            if (targetType == typeof(Quaternion))
                return new Quaternion(token["x"]?.Value<float>() ?? 0, token["y"]?.Value<float>() ?? 0,
                                      token["z"]?.Value<float>() ?? 0, token["w"]?.Value<float>() ?? 1);
            if (targetType == typeof(Color))
                return new Color(token["r"]?.Value<float>() ?? 0, token["g"]?.Value<float>() ?? 0,
                                 token["b"]?.Value<float>() ?? 0, token["a"]?.Value<float>() ?? 1);
            if (targetType == typeof(Bounds))
            {
                var center = ConvertParameter(token["center"], typeof(Vector3), "center");
                var size = ConvertParameter(token["size"], typeof(Vector3), "size");
                return new Bounds((Vector3)center, (Vector3)size);
            }
            if (targetType == typeof(Rect))
                return new Rect(token["x"]?.Value<float>() ?? 0, token["y"]?.Value<float>() ?? 0,
                                token["width"]?.Value<float>() ?? 0, token["height"]?.Value<float>() ?? 0);

            // 2. 枚举类型 (字符串名 → enum 值)
            if (targetType.IsEnum)
            {
                var str = token.Value<string>();
                if (Enum.TryParse(targetType, str, ignoreCase: true, out var enumVal))
                    return enumVal;
                throw new ArgumentException(
                    $"Invalid enum value '{str}' for parameter '{paramName}'. " +
                    $"Valid values: {string.Join(", ", Enum.GetNames(targetType))}");
            }

            // 3. Nullable<T> 解包
            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying != null)
                return ConvertParameter(token, underlying, paramName);

            // 4. JObject / JArray 透传
            if (targetType == typeof(JObject)) return token as JObject ?? token.ToObject<JObject>();
            if (targetType == typeof(JArray)) return token as JArray ?? token.ToObject<JArray>();

            // 5. 标准类型 (string, int, float, bool, 数组, 对象)
            try
            {
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Cannot convert parameter '{paramName}' to {targetType.Name}: {ex.Message}");
            }
        }

        private static bool IsNullable(Type type) =>
            !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}
```

**来源**: 综合 Unity-MCP-Plugin 的反射绑定机制和 unity-mcp-beta `ToolDiscoveryService.cs` 的参数类型映射。

---

## [修改] 5.1 配置窗口 — 增加工具启用/禁用管理

**原文**: 配置窗口仅展示 "Tools: 52 Resources: 12 Prompts: 12"。

**修改为** — 增加 per-tool 启用/禁用控制：

```
── Registered Tools ────────────────────────────────
  [View All Tools...]

  ┌────────────────────────────────────────────────┐
  │ [✓] gameobject_create     - Create GameObject  │
  │ [✓] gameobject_destroy    - Delete GameObject  │
  │ [✓] component_add        - Add Component       │
  │ [ ] code_execute          - Execute C# code    │  ← 默认禁用（高权限）
  │ [ ] reflection_call       - Reflection call     │  ← 默认禁用
  │ ...                                            │
  │ [Enable All] [Disable All] [Reset to Default]  │
  └────────────────────────────────────────────────┘
```

```csharp
// ToolRegistry 中
public bool IsToolEnabled(string toolName)
{
    string key = $"UnityMcp_Tool_{toolName}";
    if (EditorPrefs.HasKey(key))
        return EditorPrefs.GetBool(key);

    // 默认值来自属性的 AutoRegister
    return _tools.TryGetValue(toolName, out var entry)
        && entry.Attribute.AutoRegister;
}
```

**来源**: unity-mcp-beta `ToolDiscoveryService.cs` 中 `IsToolEnabled()` / `SetToolEnabled()` + EditorPrefs 持久化机制。

---

## [修改] 3.2 C# Bridge — 增加重连和域重载等待

**原文**: Bridge 仅有简单的 `SocketException` catch 重试。

**修改为** — 增加指数退避和域重载感知：

```csharp
// unity-mcp-bridge/Program.cs 连接循环
static async Task Main(string[] args)
{
    int port = args.Length > 0 ? int.Parse(args[0]) : DetectPort();

    // 参考 mcp-unity unityConnection.ts 的重连参数
    int attempt = 0;
    int[] delays = { 0, 1000, 2000, 4000, 8000, 15000, 30000 };

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            attempt = 0; // 连接成功，重置计数

            var stream = client.GetStream();

            // 处理 "reloading" 通知
            var stdinToTcp = Task.Run(() => PipeStdinToTcp(stream, cts.Token));
            var tcpToStdout = Task.Run(() => PipeTcpToStdout(stream, cts.Token));

            await Task.WhenAny(stdinToTcp, tcpToStdout);
        }
        catch (Exception)
        {
            // 指数退避
            int delay = attempt < delays.Length ? delays[attempt] : delays[^1];
            attempt++;

            // 添加抖动防止惊群 (参考 mcp-unity calculateBackoffDelay)
            int jitter = (int)(delay * 0.2 * Random.Shared.NextDouble());
            await Task.Delay(delay + jitter, cts.Token);
        }
    }
}

static async Task PipeTcpToStdout(NetworkStream tcp, CancellationToken ct)
{
    var headerBuf = new byte[5]; // 4 bytes length + 1 byte type
    while (!ct.IsCancellationRequested)
    {
        await ReadExactAsync(tcp, headerBuf, 5, ct);
        int frameLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headerBuf, 0));
        byte msgType = headerBuf[4];
        int payloadLen = frameLen - 1;

        var msgBuf = new byte[payloadLen];
        await ReadExactAsync(tcp, msgBuf, payloadLen, ct);
        var json = Encoding.UTF8.GetString(msgBuf);

        // 通知类型消息：域重载通知不写入 stdout
        if (msgType == 0x03) // Notification
        {
            var obj = JObject.Parse(json);
            if (obj["method"]?.ToString() == "notifications/reloading")
            {
                // Unity 正在重载，等待重连
                Console.Error.WriteLine("[unity-mcp-bridge] Unity reloading, waiting...");
                continue;
            }
        }

        Console.WriteLine(json);
        Console.Out.Flush();
    }
}
```

**来源**:
- 指数退避: mcp-unity `unityConnection.ts` 第 344-356 行 `calculateBackoffDelay()`
- 抖动: mcp-unity 第 353 行 `delay * 0.2 * Math.random()`
- 延迟序列: unity-mcp-beta `WebSocketTransportClient.cs` 第 28-37 行 `ReconnectSchedule`

---

## [修改] 7.1 Tools — 批处理消息格式对齐

**原文**: batch_execute 的 operations 数组格式为 `[{"tool":"name","arguments":{...}}]`。

**修改为** — 增加 `id` 字段用于结果关联（参考 mcp-unity BatchExecuteTool）：

```json
{
  "name": "batch_execute",
  "arguments": {
    "operations": [
      { "id": "op1", "tool": "gameobject_create", "arguments": {"name": "Player"} },
      { "id": "op2", "tool": "component_add", "arguments": {"target": "Player", "type": "Rigidbody"} }
    ],
    "stopOnError": true,
    "atomic": true
  }
}
```

响应中每个结果通过 `id` 关联：
```json
{
  "results": [
    { "id": "op1", "index": 0, "success": true, "result": {...} },
    { "id": "op2", "index": 1, "success": true, "result": {...} }
  ],
  "summary": { "total": 2, "succeeded": 2, "failed": 0 }
}
```

**来源**: mcp-unity `BatchExecuteTool.cs` 中 `operationId = operation["id"]?.ToString() ?? i.ToString()` 的设计。

---

## [新增] 2.6 连接候选地址

对于 localhost 连接，应生成多个候选地址以处理 DNS 解析差异。

```csharp
// TcpTransport 或 Bridge 中
private static List<IPEndPoint> GetConnectionCandidates(int port)
{
    return new List<IPEndPoint>
    {
        new IPEndPoint(IPAddress.Loopback, port),      // 127.0.0.1
        new IPEndPoint(IPAddress.IPv6Loopback, port),   // ::1
    };
}
```

**来源**: unity-mcp-beta `WebSocketTransportClient.cs` 中 `BuildConnectionCandidateUris()` 为 localhost 生成 IPv4 和 IPv6 候选。

---

## 变更总结

| 编号 | 类型 | 变更点 | 来源 |
|------|------|--------|------|
| 1 | 修改 | TCP 帧增加 1 字节类型标识 | MCP 规范 + unity-mcp-beta |
| 2 | 修改 | initialize 完整 capabilities JSON | mcp-unity + Unity-MCP + MCP 规范 |
| 3 | 修改 | McpToolAttribute 增加 Idempotent/ReadOnly/Group | Unity-MCP + unity-mcp-beta |
| 4 | 修改 | MainThreadDispatcher 增加编译回调 | Unity-MCP CompilationPipeline |
| 5 | 修改 | ServerProcessManager 增加崩溃检测+恢复 | Unity-MCP McpServerManager |
| 6 | 修改 | ToolRegistry 改用 TypeCache 扫描 | unity-mcp-beta ToolDiscoveryService |
| 7 | 新增 | UnityTypeConverters 完整实现 | Unity-MCP Converter 目录 |
| 8 | 新增 | ParameterBinder 完整实现 | Unity-MCP + unity-mcp-beta |
| 9 | 修改 | 配置窗口增加 per-tool 启用/禁用 | unity-mcp-beta ToolDiscoveryService |
| 10 | 修改 | Bridge 增加指数退避+域重载感知 | mcp-unity + unity-mcp-beta |
| 11 | 修改 | batch_execute 增加 id 字段 | mcp-unity BatchExecuteTool |
| 12 | 新增 | 连接候选地址 IPv4+IPv6 | unity-mcp-beta BuildConnectionCandidateUris |
