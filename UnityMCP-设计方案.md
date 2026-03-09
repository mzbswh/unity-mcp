# Unity MCP 自研设计方案

> 版本：v2.4
> 日期：2026-03-06
> 基于 4 个开源项目的最佳实践融合 + v1.0 审查修订 + v2.1 源码交叉验证优化 + v2.2 Runtime 模式 + v2.3 Roslyn / 多实例 + v2.4 VFX / Docker / MPPM

---

## 1. 架构总览

### 1.1 核心设计理念

**双 Server 架构 + 统一 Unity 插件 + 可选 Runtime 模式**：Unity 侧始终是同一个插件（TCP 监听 + 工具注册 + 主线程调度），用户可在 Project Settings 中一键切换前端 Server 模式。v2.2 新增 Runtime 程序集，支持构建后的独立游戏也能连接 MCP Server，实现运行时 AI 交互。

### 1.2 系统架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                    MCP 客户端 (AI 端)                            │
│         Claude Code / Cursor / Copilot / Gemini 等              │
└──────────────┬──────────────────────────┬───────────────────────┘
               │                          │
     ┌─────────▼──────────┐     ┌─────────▼──────────┐
     │ 模式 A: 内置 Server │     │ 模式 B: Python Server│
     │   (C# 桥接进程)     │     │   (FastMCP)         │
     │                    │     │                     │
     │ MCP 客户端         │     │ MCP 客户端          │
     │    ↕ stdio         │     │    ↕ stdio / HTTP   │
     │ C# Bridge (~150行) │     │ Python Server       │
     │    ↕ TCP            │     │    ↕ TCP             │
     └─────────┬──────────┘     └─────────┬──────────┘
               │ TCP localhost:{port}      │
               └──────────┬───────────────┘
                          │
     ┌────────────────────┼────────────────────┐
     │                    │                    │
┌────▼────────────────────▼───────────────┐ ┌──▼──────────────────────────────┐
│          Unity Editor 进程              │ │     Runtime 进程 (构建后游戏)    │
│                                         │ │     (可选, 条件编译启用)        │
│  ┌───────────────────────────────────┐  │ │  ┌────────────────────────────┐ │
│  │     TCP 监听层 (TcpListener)      │  │ │  │  TCP 监听层 (TcpListener)  │ │
│  │  • 监听 localhost:{port}          │  │ │  │  • 监听 localhost:{port+1} │ │
│  │  • 消息帧: 4B长度+1B类型+JSON     │  │ │  │  • 同一消息帧协议           │ │
│  │  • 多客户端并发                    │  │ │  └──────────┬─────────────────┘ │
│  └────────────┬──────────────────────┘  │ │             │ JSON-RPC 2.0      │
│               │ JSON-RPC 2.0            │ │  ┌──────────▼─────────────────┐ │
│  ┌────────────▼──────────────────────┐  │ │  │   请求处理器 (共享)        │ │
│  │     请求处理器 (RequestHandler)    │  │ │  │   RequestHandler           │ │
│  │  • 路由 / 能力协商 / 超时          │  │ │  └──────────┬─────────────────┘ │
│  └────────────┬──────────────────────┘  │ │             │                    │
│               │                          │ │  ┌──────────▼─────────────────┐ │
│  ┌────────────▼──────────────────────┐  │ │  │ 工具注册中心 (共享)        │ │
│  │     工具注册中心 (ToolRegistry)    │  │ │  │ Editor工具 + Runtime工具   │ │
│  │  • TypeCache 扫描 [McpToolGroup]  │  │ │  └──────────┬─────────────────┘ │
│  │  • 反射发现 + JSON Schema         │  │ │             │                    │
│  │  • 动态注册 + URI 模板匹配        │  │ │  ┌──────────▼─────────────────┐ │
│  └────┬─────────┬──────────┬─────────┘  │ │  │ RuntimeMainThreadDispatcher│ │
│       │         │          │             │ │  │ • ConcurrentQueue          │ │
│ ┌─────▼──┐ ┌───▼───┐ ┌───▼─────────┐   │ │  │ • MonoBehaviour.Update()   │ │
│ │内置Tool│ │Resourc│ │自定义 Tools  │   │ │  │ • DontDestroyOnLoad        │ │
│ │ (50+)  │ │ (12+) │ │ (项目扩展)  │   │ │  └──────────┬─────────────────┘ │
│ └────┬───┘ └───┬───┘ └───┬─────────┘   │ │             │                    │
│      │         │         │               │ │      Unity Runtime API           │
│  ┌───▼─────────▼─────────▼───────────┐  │ │  (GameObject, Camera, Input etc.)│
│  │  主线程调度器 (MainThreadDispatcher)│  │ └─────────────────────────────────┘
│  │  • ConcurrentQueue                 │  │
│  │  • EditorApplication.update        │  │
│  │  • 仅 RunAsync + 超时 + Undo       │  │
│  └────────────┬──────────────────────┘  │
│               │                          │
│        Unity Editor API                  │
│  (AssetDatabase, EditorSceneManager...) │
└─────────────────────────────────────────┘
```

### 1.3 两种 Server 模式对比

| 维度 | 模式 A: 内置 C# 桥接 | 模式 B: 外部 Python Server |
|------|---------------------|--------------------------|
| **外部依赖** | 无（预编译二进制随包分发） | 需要 Python 3.10+ 和 uv |
| **安装难度** | 零配置 | 需 pip install |
| **MCP 传输** | stdio | stdio + Streamable HTTP (SSE) |
| **MCP 规范合规** | 基础（stdio 足够） | 完整（FastMCP 原生支持） |
| **功能扩展** | 仅限 Unity 侧工具 | 可添加 Python 侧工具（文件分析、AI 增强等） |
| **性能** | 最优（进程内 TCP） | 略有开销（跨语言序列化） |
| **适用场景** | 个人开发、快速上手 | 团队协作、需要高级功能 |

### 1.4 关键技术选型（v2.1 修订）

| 决策点 | v1.0 选择 | v2.1 修订 | 修订理由 |
|--------|----------|----------|---------|
| **传输协议** | HttpListener | **TcpListener** | 跨平台更可靠，规避 macOS 兼容性问题 |
| **MCP 传输** | Streamable HTTP | **stdio（主）** | 所有 MCP 客户端原生支持，规避 SSE/Session 复杂性 |
| **同步 API** | Run\<T\> 阻塞等待 | **仅 RunAsync\<T\>** | 消除死锁风险，强制异步 |
| **程序集扫描** | 跳过 Unity/System 前缀 | **TypeCache + [McpToolGroup] 标记** | 高性能类型发现，降低扩展门槛 |
| **ToolResult** | Success(object) / Success(string) | **Text() / Json() / Error()** | 消除重载歧义 |
| **batch_execute** | P2 | **P0** | 核心性能优化，减少 Token 消耗 |
| **请求超时** | 无 | **默认 60s** | 防止 Tool 卡死导致请求挂起 |
| **TCP 消息帧** | 4 字节长度 + JSON | **4 字节长度 + 1 字节类型 + JSON** | 区分 Request/Response/Notification |
| **工具元数据** | Name + Description | **+ Idempotent/ReadOnly/Group/AutoRegister** | 丰富工具注解，支持过滤和权限控制 |
| **Runtime 模式** | 无 | **v2.2: 可选 Runtime 程序集** | 支持构建后游戏连接 MCP (参考 Unity-MCP) |
| **Roslyn 动态执行** | 无 | **v2.3: code_execute + code_validate** | 动态编译执行 C# + 沙箱安全 (参考 Unity-MCP) |
| **多实例管理** | 无 | **v2.3: 文件注册 + 实例路由** | 支持多 Unity Editor 并行 (参考 unity-mcp-beta) |

---

## 2. 通信层设计

### 2.1 Unity 侧 TCP 监听

```
端口: 基于项目路径 SHA256 哈希映射到 50000-59999
地址: localhost:{port}
消息帧: [4字节长度 BE] + [1字节类型] + [UTF-8 JSON 负载]
```

**消息帧协议**（比 HTTP 更轻量，比裸 TCP 更可靠）：
```
┌──────────┬──────────┬──────────────────────────────┐
│ 4 bytes  │ 1 byte   │ N bytes                      │
│ 长度 (BE)│ 消息类型  │ JSON-RPC 2.0 消息体          │
└──────────┴──────────┴──────────────────────────────┘

长度字段 = 1 (类型字节) + N (JSON 负载长度)

消息类型:
  0x01 = Request      (有 id, 需要响应)
  0x02 = Response     (有 id, 对应请求的响应)
  0x03 = Notification (无 id, 单向消息, 如域重载通知)
```

> **来源**: MCP 规范要求区分 request/response/notification 三种消息。参考 unity-mcp-beta `WebSocketTransportClient.cs` 通过 `type` 字段区分消息类型。

### 2.2 JSON-RPC 2.0 消息格式

**请求：**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "gameobject_create",
    "arguments": {
      "name": "Player",
      "primitiveType": "Cube",
      "position": {"x": 0, "y": 1, "z": 0}
    }
  }
}
```

**成功响应：**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"success\":true,\"instanceId\":12345,\"message\":\"Created GameObject 'Player'\"}"
      }
    ]
  }
}
```

**错误响应：**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32602,
    "message": "Required parameter 'name' not provided"
  }
}
```

### 2.3 支持的 MCP 方法

| 方法 | 描述 |
|------|------|
| `initialize` | 能力协商，返回支持的 Tools/Resources/Prompts |
| `notifications/initialized` | 客户端确认初始化完成 |
| `tools/list` | 返回所有可用工具列表（支持 group 过滤） |
| `tools/call` | 调用指定工具 |
| `resources/list` | 返回所有可用资源列表 |
| `resources/read` | 读取指定资源（支持 URI 模板） |
| `prompts/list` | 返回所有可用提示词列表 |
| `prompts/get` | 获取指定提示词 |
| `ping` | 心跳检测 |

#### initialize 握手规范（v2.1 补充）

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

> **来源**: 参考 mcp-unity `Server~/src/index.ts` McpServer 初始化及 MCP 协议规范 `protocolVersion: "2024-11-05"`。

### 2.4 主线程调度方案（v2.1 修订）

```
TCP 后台线程收到请求
    ↓
解析 JSON-RPC，找到目标 Tool
    ↓
创建 TaskCompletionSource<string> + CancellationTokenSource(timeout)
    ↓
将 (Tool + Params + TCS) 入队 ConcurrentQueue
    ↓
EditorApplication.update 回调出队执行
    ↓
Tool 在主线程执行 Unity API
    ↓
TCS.TrySetResult(jsonResult) 或超时 TCS.TrySetCanceled()
    ↓
TCP 后台线程取回结果，返回响应
```

**改进**：
- 移除同步 `Run<T>`，仅保留异步 `RunAsync<T>`，消除死锁
- 所有请求携带超时 `CancellationToken`（默认 30s）
- 域重载时：发送 Notification 消息类型 (0x03)，桥接进程自动等待重连
- 编译完成后通过 `CompilationPipeline.compilationFinished` 重新捕获主线程 ID

### 2.5 域重载处理

```
Assembly Reload 触发
    ↓
beforeAssemblyReload → 向已连接客户端发送 {"status":"reloading"}
    ↓
TCP 监听器停止，端口号持久化到 EditorPrefs
    ↓
域重载完成 → [InitializeOnLoad] 重新初始化
    ↓
用 EditorPrefs 中的端口号重启 TCP 监听
    ↓
桥接进程/Python Server 自动重连（指数退避 + 抖动）
```

### 2.6 连接候选地址（v2.1 新增）

对于 localhost 连接，生成多个候选地址以处理 DNS 解析差异：

```csharp
private static List<IPEndPoint> GetConnectionCandidates(int port)
{
    return new List<IPEndPoint>
    {
        new IPEndPoint(IPAddress.Loopback, port),      // 127.0.0.1
        new IPEndPoint(IPAddress.IPv6Loopback, port),   // ::1
    };
}
```

> **来源**: unity-mcp-beta `WebSocketTransportClient.cs` 中 `BuildConnectionCandidateUris()` 为 localhost 生成 IPv4 和 IPv6 候选。

---

## 3. Server 模式 A: 内置 C# 桥接进程

### 3.1 工作原理

```
MCP 客户端 ←— stdin/stdout (JSON-RPC) —→ unity-mcp-bridge.exe
                                              ↕
                                     TCP localhost:{port}
                                              ↕
                                     Unity Editor Plugin
```

桥接进程的职责极简：
1. 从 stdin 逐行读取 JSON-RPC 消息
2. 通过 TCP 转发到 Unity
3. 从 TCP 接收响应写回 stdout

### 3.2 桥接进程核心代码（v2.1 修订 — 指数退避 + 域重载感知）

```csharp
// unity-mcp-bridge/Program.cs
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

class Program
{
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

                // 并行: stdin → TCP 和 TCP → stdout
                var stdinToTcp = Task.Run(() => PipeStdinToTcp(stream, cts.Token));
                var tcpToStdout = Task.Run(() => PipeTcpToStdout(stream, cts.Token));

                await Task.WhenAny(stdinToTcp, tcpToStdout);
            }
            catch (Exception)
            {
                // 指数退避 + 抖动防止惊群 (参考 mcp-unity calculateBackoffDelay)
                int delay = attempt < delays.Length ? delays[attempt] : delays[^1];
                attempt++;
                int jitter = (int)(delay * 0.2 * Random.Shared.NextDouble());
                await Task.Delay(delay + jitter, cts.Token);
            }
        }
    }

    static async Task PipeStdinToTcp(NetworkStream tcp, CancellationToken ct)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string line;
        while ((line = await reader.ReadLineAsync()) != null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var payload = Encoding.UTF8.GetBytes(line);
            // v2.1: 4字节长度 + 1字节类型(Request=0x01) + JSON
            int frameLen = 1 + payload.Length;
            var header = new byte[5];
            header[0] = (byte)(frameLen >> 24);
            header[1] = (byte)(frameLen >> 16);
            header[2] = (byte)(frameLen >> 8);
            header[3] = (byte)(frameLen);
            header[4] = 0x01; // Request
            await tcp.WriteAsync(header, 0, 5, ct);
            await tcp.WriteAsync(payload, 0, payload.Length, ct);
            await tcp.FlushAsync(ct);
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

            // Notification 类型：域重载通知不写入 stdout
            if (msgType == 0x03)
            {
                var obj = JObject.Parse(json);
                if (obj["method"]?.ToString() == "notifications/reloading")
                {
                    Console.Error.WriteLine("[unity-mcp-bridge] Unity reloading, waiting...");
                    continue;
                }
            }

            Console.WriteLine(json);
            Console.Out.Flush();
        }
    }

    static async Task ReadExactAsync(NetworkStream s, byte[] buf, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await s.ReadAsync(buf, offset, count - offset, ct);
            if (read == 0) throw new IOException("Connection closed");
            offset += read;
        }
    }

    static int DetectPort()
    {
        var envPort = Environment.GetEnvironmentVariable("UNITY_MCP_PORT");
        if (envPort != null) return int.Parse(envPort);
        return 52345; // fallback
    }
}
```

> **来源**: 指数退避参考 mcp-unity `unityConnection.ts` 第 344-356 行；延迟序列参考 unity-mcp-beta `WebSocketTransportClient.cs` `ReconnectSchedule`。

### 3.3 构建和分发

```xml
<!-- unity-mcp-bridge.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
  </PropertyGroup>
</Project>
```

预编译目标平台（随 UPM 包分发）：
- `win-x64/unity-mcp-bridge.exe`
- `osx-x64/unity-mcp-bridge`
- `osx-arm64/unity-mcp-bridge`
- `linux-x64/unity-mcp-bridge`

---

## 4. Server 模式 B: 外部 Python Server

### 4.1 工作原理

```
MCP 客户端 ←— stdio / Streamable HTTP —→ Python Server (FastMCP)
                                              ↕
                                     TCP localhost:{port}
                                              ↕
                                     Unity Editor Plugin
```

Python Server 的优势：
- **完整的 MCP 规范支持**：FastMCP 原生支持 SSE、Session 管理
- **可扩展 Python 侧工具**：如代码分析、AI 增强、文件处理
- **热重载**：修改 Python 代码无需重启 Unity

### 4.2 Python Server 核心结构

```python
# server/unity_mcp_server.py
import asyncio
import struct
import json
from mcp.server.fastmcp import FastMCP

mcp = FastMCP("Unity MCP", version="1.0.0")

# ─── Unity TCP 连接管理 ───

class UnityConnection:
    """管理与 Unity Editor 的 TCP 连接"""

    def __init__(self, host: str = "127.0.0.1", port: int = 52345):
        self.host = host
        self.port = port
        self._reader: asyncio.StreamReader | None = None
        self._writer: asyncio.StreamWriter | None = None
        self._request_id = 0
        self._pending: dict[int, asyncio.Future] = {}
        self._lock = asyncio.Lock()

    async def connect(self):
        self._reader, self._writer = await asyncio.open_connection(self.host, self.port)
        asyncio.create_task(self._read_loop())

    async def send_request(self, method: str, params: dict = None) -> dict:
        """发送 JSON-RPC 请求并等待响应"""
        async with self._lock:
            self._request_id += 1
            req_id = self._request_id

        msg = json.dumps({
            "jsonrpc": "2.0",
            "id": req_id,
            "method": method,
            "params": params or {}
        }).encode("utf-8")

        # v2.1: 4字节长度(BE) + 1字节类型(0x01=Request) + JSON
        frame_len = 1 + len(msg)
        self._writer.write(struct.pack(">IB", frame_len, 0x01) + msg)
        await self._writer.drain()

        future = asyncio.get_event_loop().create_future()
        self._pending[req_id] = future
        return await asyncio.wait_for(future, timeout=60.0)

    async def _read_loop(self):
        try:
            while True:
                # v2.1: 读取 4字节长度 + 1字节类型
                header = await self._reader.readexactly(5)
                frame_len = struct.unpack(">I", header[:4])[0]
                msg_type = header[4]
                payload_len = frame_len - 1
                data = await self._reader.readexactly(payload_len)
                msg = json.loads(data.decode("utf-8"))

                req_id = msg.get("id")
                if req_id and req_id in self._pending:
                    self._pending.pop(req_id).set_result(msg)
        except asyncio.IncompleteReadError:
            pass  # 连接关闭

unity = UnityConnection()

# ─── MCP Tools (转发到 Unity) ───

@mcp.tool()
async def gameobject_create(name: str, primitive_type: str = None,
                            position: dict = None) -> str:
    """Create a new GameObject in the scene"""
    result = await unity.send_request("tools/call", {
        "name": "gameobject_create",
        "arguments": {"name": name, "primitiveType": primitive_type, "position": position}
    })
    return json.dumps(result.get("result", result.get("error")))

@mcp.tool()
async def console_get_logs(log_types: list[str] = None,
                           max_count: int = 50) -> str:
    """Get Unity console logs"""
    result = await unity.send_request("tools/call", {
        "name": "console_get_logs",
        "arguments": {"logTypes": log_types, "maxCount": max_count}
    })
    return json.dumps(result.get("result", result.get("error")))

# ─── Python 侧增强工具 (不依赖 Unity) ───

@mcp.tool()
async def analyze_script(file_path: str) -> str:
    """Analyze a C# script for potential issues (Python-side, no Unity needed)"""
    with open(file_path, "r") as f:
        content = f.read()
    # Python 侧分析逻辑...
    return json.dumps({"analysis": "...", "suggestions": []})

# ─── MCP Resources ───

@mcp.resource("unity://project/info")
async def get_project_info() -> str:
    """Get Unity project information"""
    result = await unity.send_request("resources/read", {"uri": "unity://project/info"})
    return json.dumps(result.get("result", {}))

# ─── 启动 ───

async def main():
    await unity.connect()
    # FastMCP 自动处理 stdio/HTTP 传输
    await mcp.run()

if __name__ == "__main__":
    asyncio.run(main())
```

### 4.3 Python 项目结构

```
server/
├── pyproject.toml              # uv/pip 项目配置
├── unity_mcp_server/
│   ├── __init__.py
│   ├── server.py              # FastMCP 入口
│   ├── unity_connection.py    # Unity TCP 连接管理
│   ├── tools/                 # Python 侧增强工具
│   │   ├── __init__.py
│   │   ├── script_analyzer.py # C# 脚本分析
│   │   └── asset_validator.py # 资产规范检查
│   └── config.py              # 配置管理
└── README.md
```

### 4.4 Python Server 的独有优势

Python Server 可提供 Unity 侧无法实现的增强功能：

| 工具 | 描述 | 为什么需要 Python |
|------|------|------------------|
| `analyze_script` | C# 脚本静态分析 | 使用 tree-sitter 等 Python 解析库 |
| `validate_assets` | 资产命名/结构规范检查 | 复杂的文件系统遍历 + 规则引擎 |
| `generate_docs` | 从代码生成文档 | 使用 Python 文本处理库 |
| `search_unity_docs` | 搜索 Unity 官方文档 | HTTP 请求 + 解析 |

---

## 5. 配置窗口设计

### 5.1 Project Settings 面板

路径：`Edit > Project Settings > Unity MCP`

```
┌─────────────────────────────────────────────────────────────┐
│  Unity MCP Settings                                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Server Mode                                                │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ ○ Built-in (C# Bridge)     ● Python Server           │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                             │
│  ── Connection ──────────────────────────────────────────── │
│  Port:          [ 52345    ]  (auto-detected from project)  │
│  Status:        ● Connected (3 clients)                     │
│                                                             │
│  ── Built-in Server ─────────────────────────────────────── │
│  Bridge Path:   [/path/to/unity-mcp-bridge    ] [Browse]    │
│  Auto Start:    [✓]                                         │
│                                                             │
│  ── Python Server ───────────────────────────────────────── │
│  Python Path:   [/usr/bin/python3             ] [Browse]    │
│  Server Script: [server/unity_mcp_server.py   ] [Browse]    │
│  Auto Start:    [✓]                                         │
│  Use uv:        [✓]                                         │
│                                                             │
│  ── Advanced ────────────────────────────────────────────── │
│  Request Timeout:     [ 60   ] seconds                      │
│  Log Level:           [ Info           ▼]                   │
│  Enable Audit Log:    [✓]                                   │
│                                                             │
│  ── Registered Tools ────────────────────────────────────── │
│  Tools: 52    Resources: 12    Prompts: 12                  │
│  [View All Tools...]                                        │
│                                                             │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ [✓] gameobject_create     - Create GameObject         │ │
│  │ [✓] gameobject_destroy    - Delete GameObject         │ │
│  │ [✓] component_add        - Add Component              │ │
│  │ [ ] code_execute          - Execute C# code (高权限)  │ │
│  │ ...                                                   │ │
│  │ [Enable All] [Disable All] [Reset to Default]         │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  ── Quick Setup ─────────────────────────────────────────── │
│  [ Copy Claude Code Config ]  [ Copy Cursor Config ]        │
│  [ Copy VS Code Config     ]  [ Copy Windsurf Config ]      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 McpSettings 配置模型

```csharp
public class McpSettings : ScriptableSingleton<McpSettings>
{
    public enum ServerMode { BuiltIn, Python }
    public enum LogLevel { Off, Error, Warning, Info, Debug }

    [SerializeField] private ServerMode serverMode = ServerMode.BuiltIn;
    [SerializeField] private int port = -1; // -1 = auto-detect
    [SerializeField] private string bridgePath = "";
    [SerializeField] private string pythonPath = "python3";
    [SerializeField] private string pythonServerScript = "";
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool useUv = true;
    [SerializeField] private int requestTimeoutSeconds = 60;
    [SerializeField] private LogLevel logLevel = LogLevel.Info;
    [SerializeField] private bool enableAuditLog = false;

    // Properties
    public ServerMode Mode => serverMode;
    public int Port => port > 0 ? port : PortResolver.GetPort(Application.dataPath);
    public int RequestTimeoutMs => requestTimeoutSeconds * 1000;
    // ...
}
```

### 5.3 Quick Setup 配置生成

点击按钮自动生成对应客户端的 MCP 配置：

**Claude Code (claude_desktop_config.json)：**
```json
{
  "mcpServers": {
    "unity": {
      "command": "/path/to/unity-mcp-bridge",
      "args": ["52345"]
    }
  }
}
```

**Cursor (.cursor/mcp.json)：**
```json
{
  "mcpServers": {
    "unity": {
      "command": "/path/to/unity-mcp-bridge",
      "args": ["52345"]
    }
  }
}
```

Python 模式时自动切换为：
```json
{
  "mcpServers": {
    "unity": {
      "command": "uv",
      "args": ["run", "/path/to/server/unity_mcp_server.py", "--port", "52345"]
    }
  }
}
```

---

## 6. 工具注册机制（v2.1 修订）

### 6.1 属性定义

```csharp
/// 标记一个类包含 MCP 工具方法
/// v2.1: 使用 TypeCache 自动发现，无需 [assembly: ContainsMcpTools]
[AttributeUsage(AttributeTargets.Class)]
public class McpToolGroupAttribute : Attribute
{
    public string GroupName { get; }
    public McpToolGroupAttribute(string groupName = null) { GroupName = groupName; }
}

/// 标记一个方法为 MCP Tool（v2.1 增强 — 参考 Unity-MCP + unity-mcp-beta）
[AttributeUsage(AttributeTargets.Method)]
public class McpToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    /// <summary>工具显示标题 (如 "GameObject / Create")</summary>
    public string Title { get; set; }

    /// <summary>标记为幂等操作 (重复调用不产生副作用)</summary>
    public bool Idempotent { get; set; } = false;

    /// <summary>标记为只读操作 (不修改场景/资产状态)</summary>
    public bool ReadOnly { get; set; } = false;

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

/// 标记一个方法为 MCP Resource（支持 URI 模板）
[AttributeUsage(AttributeTargets.Method)]
public class McpResourceAttribute : Attribute
{
    public string UriTemplate { get; }  // 如 "unity://gameobject/{id}"
    public string Name { get; }
    public string Description { get; }
    public McpResourceAttribute(string uriTemplate, string name, string description)
    {
        UriTemplate = uriTemplate;
        Name = name;
        Description = description;
    }
}

/// 标记一个方法为 MCP Prompt
[AttributeUsage(AttributeTargets.Method)]
public class McpPromptAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public McpPromptAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

/// 参数描述
[AttributeUsage(AttributeTargets.Parameter)]
public class DescAttribute : Attribute
{
    public string Text { get; }
    public DescAttribute(string text) { Text = text; }
}
```

### 6.2 工具发现机制（v2.1 修订 — TypeCache）

v2.1 使用 Unity `TypeCache` API 替代手动程序集遍历，无需 `[assembly: ContainsMcpTools]` 标记：

```csharp
// ToolRegistry 中的扫描逻辑
public void ScanTools()
{
    _tools.Clear();
    _resources.Clear();
    _prompts.Clear();

    // TypeCache: Unity 2019.2+ 高性能类型查找，比手动遍历程序集快 10-100x
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

// Per-tool 启用/禁用控制
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

> **来源**: unity-mcp-beta `ToolDiscoveryService.cs` 中 `TypeCache.GetTypesWithAttribute` 用法。扩展工具只需添加 `[McpToolGroup]` 标记类即可被发现，降低扩展门槛。

### 6.3 Resource URI 模板匹配

```csharp
// 注册时
[McpResource("unity://gameobject/{id}", "GameObject Detail", "...")]
public static object GetGameObject([Desc("Instance ID")] int id) { ... }

// ToolRegistry 内部匹配逻辑
public ResourceEntry MatchResource(string uri)
{
    foreach (var entry in _resources.Values)
    {
        var match = entry.UriRegex.Match(uri);
        if (match.Success)
        {
            entry.ExtractedParams = ExtractParams(entry.UriTemplate, match);
            return entry;
        }
    }
    return null;
}

// "unity://gameobject/{id}" → Regex: ^unity://gameobject/(?<id>[^/]+)$
```

### 6.4 JSON Schema 自动生成规则

| C# 类型 | JSON Schema | 备注 |
|---------|-------------|------|
| `string` | `"type": "string"` | |
| `int`, `long` | `"type": "integer"` | |
| `float`, `double` | `"type": "number"` | |
| `bool` | `"type": "boolean"` | |
| `string[]`, `List<string>` | `"type": "array", "items": {"type": "string"}` | |
| `Vector2` | `"type": "object", "properties": {"x","y"}` | v2.1: 带 required + additionalProperties:false |
| `Vector3` | `"type": "object", "properties": {"x","y","z"}` | 同上 |
| `Vector4` | `"type": "object", "properties": {"x","y","z","w"}` | 同上 |
| `Color` | `"type": "object", "properties": {"r","g","b","a"}` | v2.1: 改为 RGBA object, 各分量 min:0 max:1 |
| `Quaternion` | `"type": "object", "properties": {"x","y","z","w"}` | w 默认为 1 (恒等四元数) |
| `Bounds` | `{"center": Vector3, "size": Vector3}` | 嵌套 Schema |
| `Rect` | `"type": "object", "properties": {"x","y","width","height"}` | |
| `JObject` | `"type": "object"` | 透传原始 JSON |
| `JArray` | `"type": "array"` | 透传原始 JSON 数组 |
| `enum` | `"type": "string", "enum": [...]` | 自动提取枚举值 |
| 无默认值参数 | → `required` | |
| 有默认值/nullable | → `optional` | |

### 6.5 UnityTypeConverters — 完整类型转换器（v2.1 新增）

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp.Editor.Utils
{
    /// <summary>注册所有 Unity 类型转换器 + JSON Schema 生成</summary>
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

        private static JObject ObjectSchema(params (string name, string type)[] fields) { /* ... */ }
        private static JObject ObjectSchemaWithConstraints(
            params (string name, string type, double min, double max)[] fields) { /* ... */ }
    }

    // --- 转换器 ---
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
        // RGBA [0,1] 范围约束 (参考 Unity-MCP-Plugin ColorConverter)
        public override Color ReadJson(JsonReader reader, Type objectType,
            Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Color(
                obj["r"]?.Value<float>() ?? 0f, obj["g"]?.Value<float>() ?? 0f,
                obj["b"]?.Value<float>() ?? 0f, obj["a"]?.Value<float>() ?? 1f);
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
        // w 默认为 1 (恒等四元数) — 参考 Unity-MCP-Plugin QuaternionConverter
        public override Quaternion ReadJson(JsonReader reader, Type objectType,
            Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Quaternion(
                obj["x"]?.Value<float>() ?? 0f, obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f, obj["w"]?.Value<float>() ?? 1f);
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

> **来源**: Unity-MCP-Plugin `Converter/` 目录。Color `[0,1]` 范围约束来自 Unity-MCP 的 `minimum`/`maximum` schema 属性。

### 6.6 ParameterBinder — 完整参数绑定策略（v2.1 新增）

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
                    $"Invalid enum value '{str}' for '{paramName}'. " +
                    $"Valid: {string.Join(", ", Enum.GetNames(targetType))}");
            }

            // 3. Nullable<T> 解包
            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying != null)
                return ConvertParameter(token, underlying, paramName);

            // 4. JObject / JArray 透传
            if (targetType == typeof(JObject)) return token as JObject ?? token.ToObject<JObject>();
            if (targetType == typeof(JArray)) return token as JArray ?? token.ToObject<JArray>();

            // 5. 标准类型 (string, int, float, bool, 数组, 对象)
            return token.ToObject(targetType);
        }

        private static bool IsNullable(Type type) =>
            !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}
```

> **来源**: 综合 Unity-MCP-Plugin 反射绑定 + unity-mcp-beta `ToolDiscoveryService.cs` 参数类型映射。

---

## 7. Tool / Resource / Prompt 完整清单（v2.1 修订）

### 7.1 Tools（68 个，含 8 个 Runtime 工具）

#### P0 — 核心功能（首版实现，23 个）

**GameObject 操作（7 个）**
| 工具名 | 描述 |
|--------|------|
| `gameobject_create` | 创建 GameObject（空对象或原始体） |
| `gameobject_destroy` | 删除 GameObject |
| `gameobject_find` | 查找 GameObject（按名称/标签/路径/组件类型） |
| `gameobject_modify` | 修改属性（name/tag/layer/active/static） |
| `gameobject_set_parent` | 设置父级对象 |
| `gameobject_duplicate` | 复制 GameObject |
| `gameobject_get_components` | 列出 GameObject 上所有组件 |

**组件操作（4 个）**
| 工具名 | 描述 |
|--------|------|
| `component_add` | 添加组件 |
| `component_remove` | 移除组件 |
| `component_get` | 获取组件序列化字段 |
| `component_modify` | 修改组件字段 |

**场景管理（5 个）**
| 工具名 | 描述 |
|--------|------|
| `scene_create` | 创建新场景 |
| `scene_open` | 打开场景（Single/Additive） |
| `scene_save` | 保存当前场景 |
| `scene_get_hierarchy` | 获取场景层级（支持分页） |
| `scene_list_all` | 列出 Build Settings 中所有场景 |

**资产管理（5 个）**
| 工具名 | 描述 |
|--------|------|
| `asset_find` | 搜索资产（支持 `t:Texture` 过滤） |
| `asset_create_folder` | 创建文件夹 |
| `asset_delete` | 删除资产 |
| `asset_move` | 移动/重命名资产 |
| `asset_refresh` | 刷新 AssetDatabase |

**批处理（1 个）**
| 工具名 | 描述 |
|--------|------|
| `batch_execute` | 批量执行多个工具（支持 `id` 结果关联、`stopOnError`、`atomic` 原子回滚） |

batch_execute 消息格式（v2.1 修订 — 增加 `id` 字段用于结果关联）：
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
// 响应:
{
  "results": [
    { "id": "op1", "index": 0, "success": true, "result": {...} },
    { "id": "op2", "index": 1, "success": true, "result": {...} }
  ],
  "summary": { "total": 2, "succeeded": 2, "failed": 0 }
}
```
> **来源**: mcp-unity `BatchExecuteTool.cs` 中 `operationId = operation["id"]?.ToString()` 设计。

**控制台（1 个）**
| 工具名 | 描述 |
|--------|------|
| `console_get_logs` | 获取日志（类型过滤/数量限制/正则） |

#### P1 — 扩展功能（第二版，22 个）

**脚本管理（3 个）**
| 工具名 | 描述 |
|--------|------|
| `script_create` | 创建 C# 脚本 |
| `script_read` | 读取脚本内容 |
| `script_update` | 更新脚本内容 |

**材质（3 个）**
| 工具名 | 描述 |
|--------|------|
| `material_create` | 创建材质 |
| `material_modify` | 修改材质属性 |
| `shader_list` | 列出可用 Shader |

**Prefab（5 个）**
| 工具名 | 描述 |
|--------|------|
| `prefab_create` | 创建 Prefab |
| `prefab_instantiate` | 实例化 Prefab |
| `prefab_open` | 打开编辑模式 |
| `prefab_save_close` | 保存并关闭 |
| `prefab_unpack` | 解包 Prefab |

**编辑器（5 个）**
| 工具名 | 描述 |
|--------|------|
| `editor_get_state` | 获取编辑器状态 |
| `editor_set_playmode` | 控制 Play 模式 |
| `editor_execute_menu` | 执行菜单项 |
| `editor_selection_get` | 获取当前选中对象 |
| `editor_selection_set` | 设置选中对象 |

**包管理（2 个）**
| 工具名 | 描述 |
|--------|------|
| `package_list` | 列出已安装包 |
| `package_add` | 安装包 |

**Roslyn 动态代码执行（v2.3 新增，2 个）**
| 工具名 | 描述 |
|--------|------|
| `code_execute` | 编译并执行任意 C# 代码片段（Roslyn），返回编译诊断 + 执行结果 + 控制台输出 |
| `code_validate` | 仅编译验证（不执行），返回编译诊断（用于代码检查） |

> **来源**: Unity-MCP 是唯一支持 Roslyn 动态执行的参考项目。本设计增加了**沙箱安全策略**（禁止文件系统/网络/进程操作）和**超时保护**（默认 10s）。

**多实例管理（v2.3 新增，2 个）**
| 工具名 | 描述 |
|--------|------|
| `instance_list` | 列出所有已连接的 Unity 实例（项目名、PID、端口、状态） |
| `instance_set_active` | 设置当前活跃实例（后续工具调用路由到该实例） |

> **来源**: unity-mcp-beta 是唯一支持多 Unity 实例的参考项目（通过 `set_active_instance` 路由）。

#### P2 — 高级功能（后续迭代，15 个）

| 工具名 | 描述 |
|--------|------|
| `test_run` | 运行测试 |
| `test_get_results` | 获取测试结果 |
| `screenshot_scene` | Scene View 截图 |
| `screenshot_game` | Game View 截图 |
| `animation_create_clip` | 创建 AnimationClip |
| `animation_manage_controller` | 管理 AnimatorController |
| `ui_create_element` | 创建 UI 元素 |
| `asset_copy` | 复制资产 |
| `asset_get_info` | 获取资产元数据 |

**VFX/粒子系统（v2.4 新增，4 个）**
| 工具名 | 描述 |
|--------|------|
| `vfx_create_particle` | 创建 Particle System（支持预设模板：火焰/烟雾/爆炸/水花等） |
| `vfx_modify_particle` | 修改 Particle System 模块参数（Main/Emission/Shape/Renderer 等） |
| `vfx_create_graph` | 创建 VFX Graph 资产（需 VFX Graph 包，自动检测可用性） |
| `vfx_get_info` | 获取粒子/VFX 资产信息（模块列表、粒子数量、性能指标） |

> **来源**: unity-mcp-beta 是唯一覆盖 VFX/粒子的参考项目。本设计同时支持旧版 Particle System 和新版 VFX Graph。

**Multiplayer Play Mode 感知（v2.4 新增，2 个）**
| 工具名 | 描述 |
|--------|------|
| `editor_is_clone` | 检测当前 Editor 是否为 MPPM 克隆实例（返回 bool + 角色信息） |
| `editor_get_mppm_tags` | 获取 MPPM 的 Player Tags 配置（多玩家角色标识） |

> **来源**: mcp-unity 是唯一支持 Multiplayer Play Mode 感知的参考项目。克隆实例应跳过 MCP Server 启动，避免端口冲突。

#### P2-Runtime — Runtime 专用工具（v2.2 新增，8 个）

> 以下工具仅在 Runtime 模式下可用（`UNITY_MCP_RUNTIME` 条件编译），Editor Play Mode 下也可使用。

**截图工具（2 个）**
| 工具名 | 描述 |
|--------|------|
| `screenshot_game` | Game View / 游戏画面截图（Camera.Render + RenderTexture，返回 Base64 PNG） |
| `screenshot_camera` | 指定 Camera 截图（支持自定义分辨率和 Camera 名称参数） |

**性能监控（3 个）**
| 工具名 | 描述 |
|--------|------|
| `runtime_get_stats` | 获取运行时统计（FPS、帧时间、内存、GC 分配、对象数量） |
| `runtime_profiler_snapshot` | 获取 Profiler 快照（CPU/GPU 时间 Top N、内存分类统计） |
| `runtime_get_logs` | 获取 Runtime 日志（Application.logMessageReceived 捕获，支持过滤） |

**运行时控制（3 个）**
| 工具名 | 描述 |
|--------|------|
| `runtime_time_scale` | 设置/获取 Time.timeScale（暂停、慢动作、加速） |
| `runtime_load_scene` | 运行时加载场景（SceneManager.LoadScene，支持 Additive） |
| `runtime_invoke` | 反射调用任意 MonoBehaviour 上的方法（指定 GameObject 路径 + 组件类型 + 方法名） |

> **来源**: Unity-MCP 是唯一支持 Runtime 的参考项目，其核心特性包括截图、动态代码执行和反射调用。本设计采用更安全的**白名单反射**替代 Unity-MCP 的全开放反射（需标记 `[McpInvokable]`）。

### 7.2 Resources（15 个，含 3 个 Runtime 资源）

| URI | 名称 | 优先级 |
|-----|------|--------|
| `unity://project/info` | 项目信息 | P0 |
| `unity://editor/state` | 编辑器状态 | P0 |
| `unity://scene/hierarchy` | 场景层级树 | P0 |
| `unity://scene/list` | 场景列表 | P0 |
| `unity://gameobject/{id}` | GameObject 详情 | P0 |
| `unity://console/logs` | 控制台日志 | P0 |
| `unity://assets/search/{filter}` | 资产搜索 | P1 |
| `unity://packages/list` | 包列表 | P1 |
| `unity://menu/items` | 菜单项列表 | P1 |
| `unity://tags` | Tag 列表 | P1 |
| `unity://layers` | Layer 列表 | P1 |
| `unity://tests/{mode}` | 测试列表 | P2 |
| `unity://runtime/stats` | 运行时性能统计 | P2-RT |
| `unity://runtime/scene` | 运行时场景状态（已加载场景列表 + 活跃场景） | P2-RT |
| `unity://runtime/objects/{query}` | 运行时 GameObject 查询（FindObjectsOfType） | P2-RT |

### 7.3 Prompts（48 个，v2.4 扩充）

#### P0 — 核心编码指南（8 个）

| 名称 | 描述 |
|------|------|
| `unity_script_conventions` | C# 编码规范（命名规则、访问修饰符、序列化字段、Unity 特有模式） |
| `gameobject_architecture` | 组件架构指南（组合优于继承、ScriptableObject 数据驱动、单一职责） |
| `monobehaviour_lifecycle` | MonoBehaviour 生命周期最佳实践（Awake/Start/Update 顺序、协程使用） |
| `error_handling` | Unity C# 错误处理模式（Try-Catch 边界、Debug.LogException、null 检查策略） |
| `serialization_guide` | 序列化指南（[SerializeField]、ScriptableObject、JsonUtility、自定义 Inspector） |
| `code_review_checklist` | 代码审查清单（常见 Unity 反模式、性能陷阱、内存泄漏检查项） |
| `async_programming` | v2.4: 异步编程指南（async/await 在 Unity 中的使用、UniTask、协程对比、线程安全） |
| `scriptableobject_patterns` | v2.4: ScriptableObject 设计模式（数据容器、事件通道、运行时集合、单例替代） |

#### P1 — 系统设计指南（16 个）

| 名称 | 描述 |
|------|------|
| `scene_organization` | 场景组织最佳实践（层级命名、空 GameObject 分组、场景加载策略） |
| `asset_naming` | 资产命名规范（文件夹结构、前缀/后缀约定、大小写规则） |
| `performance_optimization` | 性能优化指南（对象池、LOD、Draw Call 合批、GC 优化） |
| `physics_setup` | 物理配置指南（碰撞矩阵、Rigidbody 设置、Raycast 最佳实践） |
| `input_system` | 新版 Input System 使用指南（Action Map 设计、PlayerInput 组件、回调模式） |
| `audio_architecture` | 音频架构指南（AudioMixer 层级、对象池化 AudioSource、空间音频） |
| `ai_navigation` | AI 导航指南（NavMesh 烘焙、Agent 配置、动态障碍物、路径查询） |
| `networking_patterns` | 多人游戏网络模式（状态同步 vs 帧同步、网络对象生命周期、延迟补偿） |
| `save_system` | 存档系统设计（序列化策略、加密、版本迁移、云存储接口） |
| `localization` | 本地化设计指南（Localization Package 使用、字符串表管理、字体处理） |
| `dependency_injection` | 依赖注入模式（Service Locator、Zenject/VContainer 集成建议） |
| `event_architecture` | 事件系统架构（C# event、UnityEvent、ScriptableObject 事件、消息总线对比） |
| `object_pooling` | v2.4: 对象池设计指南（通用池、预热策略、自动回收、与 Addressables 结合） |
| `state_machine` | v2.4: 状态机设计（FSM/HFSM 模式、Animator 状态机、自定义状态机框架） |
| `camera_system` | v2.4: 摄像机系统设计（Cinemachine 配置、多摄像机切换、后处理栈、分屏） |
| `lighting_setup` | v2.4: 光照配置指南（烘焙光照 vs 实时、Light Probe、Reflection Probe、URP/HDRP 差异） |

#### P2 — 专业领域指南（24 个）

| 名称 | 描述 |
|------|------|
| `animation_workflow` | 动画工作流（Animator Controller 设计、Animation Clip 创建、状态机拆分） |
| `ui_toolkit_guide` | UI Toolkit 指南（USS 样式、VisualElement 层级、数据绑定、与 uGUI 对比） |
| `shader_basics` | Shader 基础（ShaderGraph 节点、URP/HDRP 适配、自定义 Shader 模式） |
| `testing_strategy` | 测试策略（EditMode/PlayMode 测试区分、Mock 策略、CI 集成） |
| `debug_workflow` | 调试工作流（Profiler 使用、Frame Debugger、Memory Profiler、日志策略） |
| `project_setup` | 项目初始化指南（UPM 包结构、asmdef 划分、Git LFS 配置、EditorSettings） |
| `2d_game_guide` | 2D 游戏开发指南（Sprite 管理、Tilemap、2D 物理、像素完美） |
| `3d_modeling_import` | 3D 模型导入指南（FBX 设置、材质映射、LOD 配置、动画拆分） |
| `vfx_particle_guide` | VFX/粒子系统指南（Particle System vs VFX Graph、GPU 粒子、性能预算） |
| `addressables_guide` | Addressables 资产管理指南（Group 策略、远程加载、内存管理、版本更新） |
| `cicd_unity` | Unity CI/CD 指南（命令行构建、GitHub Actions、测试自动化、多平台发布） |
| `mobile_optimization` | 移动端优化指南（热管理、内存预算、纹理压缩、Shader 变体裁剪） |
| `xr_development` | XR 开发指南（XR Plugin Management、空间交互、性能目标、手势输入） |
| `ecs_dots_guide` | ECS/DOTS 指南（Entity 设计、System 组织、Burst 编译、Job 调度模式） |
| `terrain_guide` | v2.4: 地形系统指南（Terrain Layers、树木/草地、地形 LOD、程序化生成） |
| `custom_editor` | v2.4: 自定义 Editor 工具开发（PropertyDrawer、EditorWindow、IMGUI vs UI Toolkit） |
| `render_pipeline` | v2.4: 渲染管线指南（URP vs HDRP 选型、Custom Render Pass、Render Feature） |
| `multiplayer_setup` | v2.4: 多人游戏初始化指南（Netcode for GameObjects、Lobby、Relay、Transport 选择） |
| `procedural_generation` | v2.4: 程序化生成指南（噪声算法、地图生成、房间布局、种子系统） |
| `inventory_system` | v2.4: 背包/物品系统设计（数据模型、UI 绑定、拖拽操作、持久化） |
| `dialogue_system` | v2.4: 对话系统设计（节点图结构、本地化集成、条件分支、Timeline 联动） |
| `version_control_unity` | v2.4: Unity 版本控制最佳实践（.meta 文件、合并策略、LFS 规则、场景合并工具） |
| `asset_bundle_guide` | v2.4: AssetBundle 指南（构建流水线、依赖管理、增量更新、与 Addressables 对比） |
| `editor_automation` | v2.4: 编辑器自动化指南（MenuItem、BuildPipeline、AssetPostprocessor、脚本化导入） |

---

## 8. 目录结构（v2.4）

```
com.yourcompany.unity-mcp/
├── package.json                          # UPM 包定义
├── CHANGELOG.md
├── LICENSE
├── README.md
├── CLAUDE.md                             # AI 引导文档
├── AGENTS.md                             # AI Agent 操作手册
│
├── Shared/
│   ├── UnityMcp.Shared.asmdef            # Editor + Runtime 共享程序集
│   │
│   ├── Interfaces/
│   │   ├── IMainThreadDispatcher.cs      # 主线程调度抽象接口
│   │   ├── ITcpTransport.cs              # TCP 传输层抽象接口
│   │   └── IToolRegistry.cs              # 工具注册抽象接口
│   │
│   ├── Attributes/
│   │   ├── McpToolGroupAttribute.cs      # 从 Editor 上移到共享层
│   │   ├── McpToolAttribute.cs           # v2.1: +Idempotent/ReadOnly/Group/AutoRegister
│   │   ├── McpResourceAttribute.cs
│   │   ├── McpPromptAttribute.cs
│   │   ├── McpInvokableAttribute.cs      # v2.2: 标记可被 runtime_invoke 调用的方法
│   │   └── DescAttribute.cs
│   │
│   ├── Models/
│   │   ├── ToolResult.cs                 # Text() / Json() / Error() / Paginated()
│   │   ├── McpCapabilities.cs
│   │   └── Pagination.cs
│   │
│   ├── Utils/
│   │   ├── JsonSchemaGenerator.cs
│   │   ├── ParameterBinder.cs            # v2.1: 完整参数绑定
│   │   ├── UnityTypeConverters.cs        # v2.1: 完整转换器
│   │   ├── PortResolver.cs
│   │   ├── SecurityChecker.cs            # v2.3: Roslyn 沙箱安全检查
│   │   └── McpLogger.cs                  # 分级日志 + 审计日志
│   │
│   └── Instance/
│       ├── InstanceDiscovery.cs           # v2.3: 实例注册/注销/发现
│       └── InstanceInfo.cs                # v2.3: 实例信息模型
│
├── Editor/
│   ├── UnityMcp.Editor.asmdef            # 引用 UnityMcp.Shared
│   │
│   ├── Core/
│   │   ├── McpServer.cs                  # 入口 [InitializeOnLoad]
│   │   ├── TcpTransport.cs              # TCP 监听 + 消息帧 (含类型字节)
│   │   ├── RequestHandler.cs             # JSON-RPC 路由 + 超时 + initialize 握手
│   │   ├── ToolRegistry.cs              # 工具注册 (TypeCache + URI 模板 + per-tool 启用)
│   │   ├── MainThreadDispatcher.cs       # 仅异步 API + 超时 + 编译回调 (实现 IMainThreadDispatcher)
│   │   ├── McpSettings.cs               # 配置 (ScriptableSingleton)
│   │   ├── McpSettingsProvider.cs        # Project Settings UI (含工具启用/禁用面板)
│   │   └── ServerProcessManager.cs       # 进程生命周期 (崩溃检测+恢复+孤立清理)
│   │
│   ├── Utils/
│   │   └── UndoHelper.cs                # Editor 专用 (Undo 仅 Editor 可用)
│   │
│   ├── Tools/
│   │   ├── GameObjectTools.cs
│   │   ├── ComponentTools.cs
│   │   ├── SceneTools.cs
│   │   ├── AssetTools.cs
│   │   ├── ScriptTools.cs
│   │   ├── MaterialTools.cs
│   │   ├── PrefabTools.cs
│   │   ├── EditorTools.cs
│   │   ├── ConsoleTools.cs
│   │   ├── TestTools.cs
│   │   ├── PackageTools.cs
│   │   ├── BatchExecuteTool.cs
│   │   ├── CodeExecutionTools.cs          # v2.3: Roslyn 动态执行 + 验证
│   │   ├── InstanceTools.cs               # v2.3: 多实例管理
│   │   ├── VfxTools.cs                    # v2.4: VFX/粒子系统操作
│   │   └── MppmTools.cs                   # v2.4: Multiplayer Play Mode 感知
│   │
│   ├── Resources/
│   │   ├── ProjectResources.cs
│   │   ├── EditorResources.cs
│   │   ├── SceneResources.cs
│   │   ├── GameObjectResources.cs
│   │   └── ConsoleResources.cs
│   │
│   ├── Prompts/
│   │   ├── ScriptPrompts.cs              # 编码规范、生命周期、错误处理、序列化
│   │   ├── ArchitecturePrompts.cs        # 组件架构、事件系统、依赖注入
│   │   ├── SystemDesignPrompts.cs        # v2.3: 物理/音频/AI导航/网络/存档/本地化
│   │   ├── SpecialtyPrompts.cs           # v2.3: 动画/UI/Shader/VFX/2D/3D/XR/ECS
│   │   └── WorkflowPrompts.cs            # 测试/调试/CI/CD/项目初始化
│   │
│   └── Window/
│       ├── McpSettingsWindow.cs          # Project Settings 面板
│       └── McpStatusBar.cs              # 状态栏指示器 (可选)
│
├── Runtime/                              # v2.2 新增: Runtime 程序集
│   ├── UnityMcp.Runtime.asmdef           # 引用 UnityMcp.Shared, 条件编译 UNITY_MCP_RUNTIME
│   │
│   ├── Core/
│   │   ├── McpRuntimeBehaviour.cs        # MonoBehaviour 入口 (DontDestroyOnLoad)
│   │   ├── RuntimeTcpTransport.cs        # Runtime TCP 监听 (复用 Shared 协议)
│   │   ├── RuntimeMainThreadDispatcher.cs# MonoBehaviour.Update() 驱动队列
│   │   └── RuntimeToolRegistry.cs        # Runtime 工具注册 (反射扫描, 无 TypeCache)
│   │
│   ├── Tools/
│   │   ├── ScreenshotTools.cs            # screenshot_game, screenshot_camera
│   │   ├── RuntimeStatsTools.cs          # runtime_get_stats, runtime_profiler_snapshot
│   │   ├── RuntimeControlTools.cs        # runtime_time_scale, runtime_load_scene
│   │   ├── RuntimeLogTools.cs            # runtime_get_logs
│   │   └── RuntimeInvokeTools.cs         # runtime_invoke (白名单反射)
│   │
│   └── Resources/
│       ├── RuntimeStatsResources.cs      # unity://runtime/stats
│       ├── RuntimeSceneResources.cs      # unity://runtime/scene
│       └── RuntimeObjectResources.cs     # unity://runtime/objects/{query}
│
├── Bridge~/                              # 不被 Unity 导入
│   ├── unity-mcp-bridge.csproj
│   ├── Program.cs                        # C# 桥接进程源码
│   └── bin/                              # 预编译二进制
│       ├── win-x64/unity-mcp-bridge.exe
│       ├── osx-x64/unity-mcp-bridge
│       ├── osx-arm64/unity-mcp-bridge
│       └── linux-x64/unity-mcp-bridge
│
├── Server~/                              # 不被 Unity 导入
│   ├── pyproject.toml
│   ├── Dockerfile                         # v2.4: Docker 部署
│   ├── docker-compose.yml                 # v2.4: Docker Compose 配置
│   ├── unity_mcp_server/
│   │   ├── __init__.py
│   │   ├── server.py
│   │   ├── unity_connection.py
│   │   ├── instance_manager.py            # v2.3: 多实例管理
│   │   ├── tools/
│   │   └── config.py
│   └── README.md
│
├── Tests/
│   ├── Editor/
│   │   ├── UnityMcp.Tests.Editor.asmdef
│   │   ├── ToolRegistryTests.cs
│   │   ├── JsonSchemaGeneratorTests.cs
│   │   ├── ParameterBinderTests.cs
│   │   └── GameObjectToolsTests.cs
│   └── Runtime/
│       └── UnityMcp.Tests.Runtime.asmdef
│
└── Samples~/
    └── CustomTools/
        ├── MyCustomToolExample.cs        # 仅需 [McpToolGroup] 标记即可被发现
        └── README.md
```

---

## 9. 关键代码设计（v2.1 修订）

### 9.1 McpServer — 入口

```csharp
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor.Core
{
    [InitializeOnLoad]
    public static class McpServer
    {
        public static ToolRegistry Registry { get; private set; }

        private static TcpTransport _transport;
        private static RequestHandler _handler;
        private static ServerProcessManager _processManager;
        private static bool _initialized;

        static McpServer()
        {
            EditorApplication.delayCall += Initialize;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
            EditorApplication.quitting += Shutdown;
        }

        private static void Initialize()
        {
            if (_initialized) return;

            var settings = McpSettings.Instance;

            // 1. 扫描注册所有工具 (v2.1: TypeCache 自动发现)
            Registry = new ToolRegistry();
            Registry.ScanTools();

            // 2. 初始化请求处理器
            _handler = new RequestHandler(Registry, settings.RequestTimeoutMs);

            // 3. 启动 TCP 监听
            int port = settings.Port;
            _transport = new TcpTransport(port, _handler);
            _transport.Start();

            // 4. 根据配置启动外部 Server 进程
            if (settings.AutoStart)
            {
                _processManager = new ServerProcessManager(settings);
                _processManager.StartServer();
            }

            // 5. 持久化端口号（域重载恢复用）
            EditorPrefs.SetInt("UnityMcp_Port", port);

            _initialized = true;
            McpLogger.Info($"Server started on TCP:{port} " +
                          $"({Registry.ToolCount} tools, {Registry.ResourceCount} resources, " +
                          $"{Registry.PromptCount} prompts) Mode: {settings.Mode}");
        }

        private static void OnBeforeReload()
        {
            // 通知已连接客户端即将重载
            _transport?.BroadcastReloading();
            _transport?.Stop();
        }

        private static void OnAfterReload()
        {
            _initialized = false;
            EditorApplication.delayCall += Initialize;
        }

        private static void Shutdown()
        {
            _processManager?.StopServer();
            _transport?.Stop();
            _initialized = false;
        }
    }
}
```

### 9.2 MainThreadDispatcher — 仅异步，带超时（v2.1 修订 — 编译回调）

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor.Core
{
    [InitializeOnLoad]
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<WorkItem> _queue = new();
        private static int _mainThreadId; // v2.1: 非 readonly, 域重载后需重新捕获

        static MainThreadDispatcher()
        {
            // 域重载后重新捕获主线程 ID
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += ProcessQueue;

            // v2.1: 编译完成后重新初始化主线程 ID
            // 参考 Unity-MCP CompilationPipeline.compilationFinished
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += _ =>
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            };
        }

        public static bool IsMainThread =>
            Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// 异步在主线程执行。从后台线程安全调用，不会死锁。
        /// </summary>
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

        /// <summary>异步执行无返回值操作</summary>
        public static Task RunAsync(Action action, int timeoutMs = 30000)
        {
            return RunAsync(() => { action(); return (object)null; }, timeoutMs);
        }

        private static void ProcessQueue()
        {
            int budget = 10; // 每帧最多处理 10 项，避免编辑器卡顿
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
}
```

### 9.3 TcpTransport — TCP 传输层（v2.1 修订 — 消息类型字节）

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityMcp.Editor.Core
{
    public class TcpTransport
    {
        private readonly int _port;
        private readonly RequestHandler _handler;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly ConcurrentBag<TcpClient> _clients = new();

        public TcpTransport(int port, RequestHandler handler)
        {
            _port = port;
            _handler = handler;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            foreach (var client in _clients)
            {
                try { client.Close(); } catch { }
            }
        }

        public void BroadcastReloading()
        {
            var payload = Encoding.UTF8.GetBytes(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/reloading\"}");
            foreach (var client in _clients)
            {
                try
                {
                    if (!client.Connected) continue;
                    var stream = client.GetStream();
                    // v2.1: 4字节长度 + 1字节类型(Notification=0x03) + JSON
                    int frameLen = 1 + payload.Length;
                    var header = new byte[5];
                    header[0] = (byte)(frameLen >> 24);
                    header[1] = (byte)(frameLen >> 16);
                    header[2] = (byte)(frameLen >> 8);
                    header[3] = (byte)(frameLen);
                    header[4] = 0x03; // Notification
                    stream.Write(header, 0, 5);
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush();
                }
                catch { }
            }
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _clients.Add(client);
                    _ = HandleClient(client, ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken ct)
        {
            var stream = client.GetStream();
            var headerBuf = new byte[5]; // v2.1: 4 bytes length + 1 byte type

            try
            {
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    // v2.1: 读取 4 字节长度 + 1 字节类型
                    await ReadExactAsync(stream, headerBuf, 5, ct);
                    int frameLen = IPAddress.NetworkToHostOrder(
                        BitConverter.ToInt32(headerBuf, 0));
                    byte msgType = headerBuf[4];
                    int payloadLen = frameLen - 1;

                    if (payloadLen <= 0 || payloadLen > 10 * 1024 * 1024)
                        throw new InvalidDataException($"Invalid payload length: {payloadLen}");

                    // 读取 JSON 消息体
                    var msgBuf = new byte[payloadLen];
                    await ReadExactAsync(stream, msgBuf, payloadLen, ct);
                    var json = Encoding.UTF8.GetString(msgBuf);

                    // 处理请求
                    McpLogger.Debug($"← [{msgType:X2}] {json}");
                    var response = await _handler.HandleRequest(json);
                    McpLogger.Debug($"→ {response}");

                    // 写回响应 (类型 0x02 = Response)
                    var respBytes = Encoding.UTF8.GetBytes(response);
                    int respFrameLen = 1 + respBytes.Length;
                    var respHeader = new byte[5];
                    respHeader[0] = (byte)(respFrameLen >> 24);
                    respHeader[1] = (byte)(respFrameLen >> 16);
                    respHeader[2] = (byte)(respFrameLen >> 8);
                    respHeader[3] = (byte)(respFrameLen);
                    respHeader[4] = 0x02; // Response
                    await stream.WriteAsync(respHeader, 0, 5, ct);
                    await stream.WriteAsync(respBytes, 0, respBytes.Length, ct);
                    await stream.FlushAsync(ct);
                }
            }
            catch (IOException) { } // 连接关闭
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                McpLogger.Error($"Client error: {ex.Message}");
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static async Task ReadExactAsync(
            NetworkStream s, byte[] buf, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await s.ReadAsync(buf, offset, count - offset, ct);
                if (read == 0) throw new IOException("Connection closed by remote");
                offset += read;
            }
        }
    }
}
```

### 9.4 ToolResult — 无歧义返回模型

```csharp
using Newtonsoft.Json.Linq;

namespace UnityMcp.Editor.Models
{
    public class ToolResult
    {
        public bool IsSuccess { get; private set; }
        public JToken Content { get; private set; }
        public string ErrorMessage { get; private set; }
        public string ErrorCode { get; private set; }

        /// <summary>返回纯文本消息</summary>
        public static ToolResult Text(string message)
        {
            return new ToolResult
            {
                IsSuccess = true,
                Content = new JValue(message)
            };
        }

        /// <summary>返回 JSON 序列化的数据对象</summary>
        public static ToolResult Json(object data)
        {
            return new ToolResult
            {
                IsSuccess = true,
                Content = data == null ? JValue.CreateNull() : JToken.FromObject(data)
            };
        }

        /// <summary>返回错误</summary>
        public static ToolResult Error(string message, string code = "tool_error")
        {
            return new ToolResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                ErrorCode = code
            };
        }

        /// <summary>返回分页结果</summary>
        public static ToolResult Paginated(object items, int total, string nextCursor = null)
        {
            var obj = new JObject
            {
                ["items"] = JToken.FromObject(items),
                ["total"] = total,
            };
            if (nextCursor != null) obj["nextCursor"] = nextCursor;
            return new ToolResult { IsSuccess = true, Content = obj };
        }
    }
}
```

### 9.5 RequestHandler — initialize 握手实现（v2.1 新增）

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

### 9.6 ServerProcessManager — 外部进程管理（v2.1 修订 — 崩溃检测 + 自动恢复）

```csharp
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor.Core
{
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
            // 参考 Unity-MCP McpServerManager.CheckExistingProcess()
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
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }

            EditorPrefs.DeleteKey(PidEditorPrefKey);
            return false;
        }

        /// <summary>清理可能遗留的孤立进程</summary>
        private void CleanupOrphanedProcess()
        {
            // 参考 Unity-MCP McpServerManager.KillOrphanedServerProcesses()
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
            // 参考 Unity-MCP McpServerManager.ScheduleStartupVerification()
            int pid = _serverProcess?.Id ?? -1;
            double checkTime = EditorApplication.timeSinceStartup + 5.0;

            void Check()
            {
                if (EditorApplication.timeSinceStartup < checkTime) return;
                EditorApplication.update -= Check;

                if (_serverProcess == null || _serverProcess.HasExited)
                {
                    McpLogger.Error($"Server process (PID: {pid}) exited within 5s of startup.");
                    Cleanup();
                }
            }

            EditorApplication.update += Check;
        }

        /// <summary>进程意外退出回调</summary>
        private void OnProcessExited(object sender, EventArgs e)
        {
            McpLogger.Warning("Server process exited unexpectedly");
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

        private ProcessStartInfo CreateBridgeStartInfo()
        {
            string bridgePath = _settings.BridgePath;
            if (string.IsNullOrEmpty(bridgePath))
                bridgePath = GetDefaultBridgePath();

            return new ProcessStartInfo
            {
                FileName = bridgePath,
                Arguments = _settings.Port.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
        }

        private ProcessStartInfo CreatePythonStartInfo()
        {
            string serverScript = _settings.PythonServerScript;
            if (string.IsNullOrEmpty(serverScript))
                serverScript = GetDefaultPythonServerPath();

            if (_settings.UseUv)
            {
                return new ProcessStartInfo
                {
                    FileName = "uv",
                    Arguments = $"run {serverScript} --port {_settings.Port}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            return new ProcessStartInfo
            {
                FileName = _settings.PythonPath,
                Arguments = $"{serverScript} --port {_settings.Port}",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        private string GetDefaultBridgePath()
        {
            string platform = Application.platform switch
            {
                RuntimePlatform.WindowsEditor => "win-x64/unity-mcp-bridge.exe",
                RuntimePlatform.OSXEditor =>
                    System.Runtime.InteropServices.RuntimeInformation.OSArchitecture ==
                    System.Runtime.InteropServices.Architecture.Arm64
                        ? "osx-arm64/unity-mcp-bridge" : "osx-x64/unity-mcp-bridge",
                _ => "linux-x64/unity-mcp-bridge"
            };
            return Path.Combine(GetPackagePath(), "Bridge~", "bin", platform);
        }

        private static string GetPackagePath()
        {
            var guids = AssetDatabase.FindAssets("package t:TextAsset",
                new[] { "Packages/com.yourcompany.unity-mcp" });
            return guids.Length > 0
                ? Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guids[0]))
                : "";
        }
    }
}
```

### 9.7 McpLogger — 分级日志

```csharp
using UnityEngine;

namespace UnityMcp.Editor.Utils
{
    public static class McpLogger
    {
        private const string Tag = "[UnityMCP]";

        public static void Debug(string message)
        {
            if (McpSettings.Instance.LogLevel <= McpSettings.LogLevel.Debug)
                UnityEngine.Debug.Log($"{Tag} {message}");
        }

        public static void Info(string message)
        {
            if (McpSettings.Instance.LogLevel <= McpSettings.LogLevel.Info)
                UnityEngine.Debug.Log($"{Tag} {message}");
        }

        public static void Warning(string message)
        {
            if (McpSettings.Instance.LogLevel <= McpSettings.LogLevel.Warning)
                UnityEngine.Debug.LogWarning($"{Tag} {message}");
        }

        public static void Error(string message)
        {
            if (McpSettings.Instance.LogLevel <= McpSettings.LogLevel.Error)
                UnityEngine.Debug.LogError($"{Tag} {message}");
        }

        /// <summary>审计日志: 记录每次工具调用</summary>
        public static void Audit(string toolName, string args, long durationMs,
                                  bool success, string error = null)
        {
            if (!McpSettings.Instance.EnableAuditLog) return;
            var status = success ? "OK" : $"ERR: {error}";
            UnityEngine.Debug.Log(
                $"{Tag} AUDIT | {toolName} | {durationMs}ms | {status} | {args}");
        }
    }
}
```

---

## 10. 自定义工具扩展

### 10.1 最简示例

```csharp
// v2.1: 只需添加 [McpToolGroup] 标记类，TypeCache 自动发现，无需 [assembly: ContainsMcpTools]
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Attributes;
using UnityMcp.Editor.Models;
using UnityMcp.Editor.Utils;

[McpToolGroup("MyProject")]
public static class MyCustomTools
{
    [McpTool("my_spawn_enemy", "Spawn enemy prefabs at the given position",
             Group = "MyProject", Idempotent = false)]
    public static ToolResult SpawnEnemy(
        [Desc("Prefab path in Assets folder")] string prefabPath,
        [Desc("Spawn position")] Vector3 position,
        [Desc("Number of enemies to spawn")] int count = 1)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return ToolResult.Error($"Prefab not found: '{prefabPath}'", "not_found");

        var undoGroup = UndoHelper.BeginGroup("Spawn Enemies");
        var names = new List<string>();

        for (int i = 0; i < count; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = position + Vector3.right * i * 2;
            UndoHelper.RegisterCreatedObject(go, "Spawn Enemy");
            names.Add(go.name);
        }

        UndoHelper.EndGroup(undoGroup);
        return ToolResult.Text($"Spawned {count} enemies: {string.Join(", ", names)}");
    }
}
```

**框架自动完成**：发现 → Schema 生成 → 参数绑定 → 主线程执行 → 序列化返回

### 10.2 自定义 Resource（带 URI 模板）

```csharp
[McpToolGroup("MyProject")]
public static class MyResources
{
    [McpResource("myproject://enemy/{type}/stats", "Enemy Stats",
        "Get stats for a specific enemy type")]
    public static object GetEnemyStats([Desc("Enemy type name")] string type)
    {
        // 框架自动从 URI 提取 {type} 绑定到参数
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Enemies/{type}.prefab");
        if (prefab == null) return new { error = "Not found" };

        return new
        {
            type,
            hasRigidbody = prefab.GetComponent<Rigidbody>() != null,
            childCount = prefab.transform.childCount,
            components = prefab.GetComponents<Component>().Length
        };
    }
}
```

---

## 11. Runtime 模式设计（v2.2 新增）

> **来源**: Unity-MCP 是 4 个参考项目中**唯一支持 Runtime 模式**的项目。本设计参考其架构，但采用更安全的白名单反射策略。

### 11.1 Runtime 与 Editor 的核心差异

| 维度 | Editor 模式 | Runtime 模式 |
|------|------------|-------------|
| **进程** | Unity Editor | 构建后的 Player (Standalone/Mobile/WebGL) |
| **主线程驱动** | `EditorApplication.update` | `MonoBehaviour.Update()` |
| **工具发现** | `TypeCache.GetTypesWithAttribute` | `AppDomain.CurrentDomain.GetAssemblies()` 反射 |
| **Undo 支持** | `Undo.RecordObject` | ❌ 不可用 |
| **AssetDatabase** | ✅ 完整 | ❌ 不存在 |
| **端口** | `SHA256(projectPath) → 50000-59999` | Editor端口 + 1 (或配置指定) |
| **启用方式** | `[InitializeOnLoad]` 自动启动 | `McpRuntimeBehaviour` 手动挂载或 Prefab |
| **条件编译** | 无特殊要求 | `UNITY_MCP_RUNTIME` 宏定义 |

### 11.2 共享层设计 (Shared Assembly)

Editor 和 Runtime 共享以下核心组件，避免代码重复：

```
UnityMcp.Shared.asmdef
├── Interfaces/          ← 抽象接口
│   ├── IMainThreadDispatcher   RunAsync<T> 签名
│   ├── ITcpTransport           Start/Stop/Send 签名
│   └── IToolRegistry           Register/Discover/Execute 签名
├── Attributes/          ← 属性定义 (Editor + Runtime 通用)
├── Models/              ← ToolResult, Pagination 等数据模型
└── Utils/               ← 类型转换器, 参数绑定, JSON Schema, 日志
```

**asmdef 配置**：
```json
{
    "name": "UnityMcp.Shared",
    "rootNamespace": "UnityMcp.Shared",
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "defineConstraints": [],
    "references": []
}
```

Editor 和 Runtime 的 asmdef 都引用 `UnityMcp.Shared`：
```json
// UnityMcp.Editor.asmdef
{
    "name": "UnityMcp.Editor",
    "includePlatforms": ["Editor"],
    "references": ["UnityMcp.Shared"]
}

// UnityMcp.Runtime.asmdef
{
    "name": "UnityMcp.Runtime",
    "includePlatforms": [],
    "defineConstraints": ["UNITY_MCP_RUNTIME"],
    "references": ["UnityMcp.Shared"]
}
```

### 11.3 IMainThreadDispatcher — 共享接口

```csharp
namespace UnityMcp.Shared
{
    public interface IMainThreadDispatcher
    {
        /// <summary>
        /// 在主线程执行操作，返回异步结果。
        /// </summary>
        Task<T> RunAsync<T>(Func<T> action, CancellationToken ct = default);

        /// <summary>
        /// 在主线程执行操作（无返回值）。
        /// </summary>
        Task RunAsync(Action action, CancellationToken ct = default);
    }
}
```

**Editor 实现**: 基于 `EditorApplication.update` + `ConcurrentQueue`（已有设计，§9.2）
**Runtime 实现**: 基于 `MonoBehaviour.Update()` + `ConcurrentQueue`（下文 §11.4）

### 11.4 McpRuntimeBehaviour — Runtime 入口

```csharp
#if UNITY_MCP_RUNTIME
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityMcp.Shared;

namespace UnityMcp.Runtime
{
    /// <summary>
    /// Runtime MCP 入口。挂载到场景中的 GameObject 或通过 RuntimeInitializeOnLoadMethod 自动创建。
    /// </summary>
    public class McpRuntimeBehaviour : MonoBehaviour, IMainThreadDispatcher
    {
        private static McpRuntimeBehaviour _instance;

        [Header("Runtime MCP Settings")]
        [SerializeField] private int _port = 0; // 0 = 自动 (Editor端口+1)
        [SerializeField] private bool _autoStart = true;

        private RuntimeTcpTransport _transport;
        private RuntimeToolRegistry _registry;
        private RequestHandler _requestHandler;
        private readonly ConcurrentQueue<WorkItem> _workQueue = new();

        // 自动创建（无需手动挂载）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance != null) return;

            var go = new GameObject("[MCP Runtime]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<McpRuntimeBehaviour>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_autoStart) StartMcp();
        }

        public void StartMcp()
        {
            int port = _port > 0 ? _port : PortResolver.GetPort() + 1;

            _registry = new RuntimeToolRegistry();
            _registry.ScanTools(); // 反射扫描所有带 [McpToolGroup] 的类型

            _requestHandler = new RequestHandler(_registry, this);

            _transport = new RuntimeTcpTransport(port, _requestHandler);
            _transport.Start();

            McpLogger.Info($"[MCP Runtime] Listening on port {port}");
        }

        private void Update()
        {
            // 每帧处理最多 10 个任务，避免卡顿
            int processed = 0;
            while (processed < 10 && _workQueue.TryDequeue(out var item))
            {
                try
                {
                    item.Execute();
                }
                catch (Exception ex)
                {
                    item.SetException(ex);
                }
                processed++;
            }
        }

        private void OnDestroy()
        {
            _transport?.Stop();
            if (_instance == this) _instance = null;
        }

        // IMainThreadDispatcher 实现
        public Task<T> RunAsync<T>(Func<T> action, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<T>();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            cts.Token.Register(() => tcs.TrySetCanceled(), false);

            _workQueue.Enqueue(new WorkItem(() =>
            {
                if (cts.Token.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                    return;
                }
                tcs.TrySetResult(action());
            }, ex => tcs.TrySetException(ex)));

            return tcs.Task;
        }

        public Task RunAsync(Action action, CancellationToken ct = default)
        {
            return RunAsync<object>(() => { action(); return null; }, ct);
        }

        // 内部工作项
        private readonly struct WorkItem
        {
            private readonly Action _action;
            private readonly Action<Exception> _onError;

            public WorkItem(Action action, Action<Exception> onError)
            {
                _action = action;
                _onError = onError;
            }

            public void Execute() => _action();
            public void SetException(Exception ex) => _onError(ex);
        }
    }
}
#endif
```

### 11.5 RuntimeToolRegistry — 无 TypeCache 的反射扫描

```csharp
#if UNITY_MCP_RUNTIME
using System;
using System.Linq;
using System.Reflection;
using UnityMcp.Shared;

namespace UnityMcp.Runtime
{
    /// <summary>
    /// Runtime 工具注册。不依赖 TypeCache (Editor-only API)，
    /// 改用 AppDomain 反射扫描。
    /// </summary>
    public class RuntimeToolRegistry : IToolRegistry
    {
        public void ScanTools()
        {
            var toolGroupTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !IsSystemAssembly(a))
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.GetCustomAttribute<McpToolGroupAttribute>() != null);

            foreach (var type in toolGroupTypes)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var toolAttr = method.GetCustomAttribute<McpToolAttribute>();
                    if (toolAttr != null)
                    {
                        RegisterTool(method, toolAttr);
                    }

                    var resourceAttr = method.GetCustomAttribute<McpResourceAttribute>();
                    if (resourceAttr != null)
                    {
                        RegisterResource(method, resourceAttr);
                    }
                }
            }
        }

        private static bool IsSystemAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            return name.StartsWith("System") || name.StartsWith("Unity.")
                || name.StartsWith("UnityEngine.") || name.StartsWith("UnityEditor")
                || name == "mscorlib" || name == "netstandard";
        }

        // RegisterTool / RegisterResource / Execute 等实现与 Editor ToolRegistry 共享逻辑
        // 差异点: 无 per-tool 启用/禁用 (Runtime 无 EditorPrefs), 无 TypeCache
    }
}
#endif
```

### 11.6 截图工具实现

```csharp
#if UNITY_MCP_RUNTIME
using System;
using UnityEngine;
using UnityMcp.Shared;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Screenshot")]
    public static class ScreenshotTools
    {
        [McpTool("screenshot_game", "Capture current game view as Base64 PNG",
            ReadOnly = true, Group = "runtime")]
        public static ToolResult CaptureGameView(
            [Desc("Width in pixels (default: Screen.width)")] int width = 0,
            [Desc("Height in pixels (default: Screen.height)")] int height = 0)
        {
            width = width > 0 ? width : Screen.width;
            height = height > 0 ? height : Screen.height;

            var rt = RenderTexture.GetTemporary(width, height, 24);
            var camera = Camera.main;
            if (camera == null)
                return ToolResult.Error("No main camera found");

            var prevRT = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = prevRT;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);

            return ToolResult.Json(new
            {
                format = "png",
                width,
                height,
                base64 = Convert.ToBase64String(bytes),
                sizeBytes = bytes.Length
            });
        }

        [McpTool("screenshot_camera", "Capture specific camera view as Base64 PNG",
            ReadOnly = true, Group = "runtime")]
        public static ToolResult CaptureCamera(
            [Desc("Camera name (default: Main Camera)")] string cameraName = "Main Camera",
            [Desc("Width in pixels")] int width = 1920,
            [Desc("Height in pixels")] int height = 1080)
        {
            var camera = GameObject.Find(cameraName)?.GetComponent<Camera>();
            if (camera == null)
                return ToolResult.Error($"Camera '{cameraName}' not found");

            var rt = RenderTexture.GetTemporary(width, height, 24);
            var prevRT = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = prevRT;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);

            return ToolResult.Json(new
            {
                format = "png",
                cameraName,
                width,
                height,
                base64 = Convert.ToBase64String(bytes),
                sizeBytes = bytes.Length
            });
        }
    }
}
#endif
```

### 11.7 性能监控工具实现

```csharp
#if UNITY_MCP_RUNTIME
using UnityEngine;
using UnityEngine.Profiling;
using UnityMcp.Shared;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Stats")]
    public static class RuntimeStatsTools
    {
        private static float _deltaTimeAccumulator;
        private static int _frameCount;
        private static float _currentFps;

        // 在 McpRuntimeBehaviour.Update 中调用以更新 FPS
        internal static void UpdateStats()
        {
            _frameCount++;
            _deltaTimeAccumulator += Time.unscaledDeltaTime;
            if (_deltaTimeAccumulator >= 1f)
            {
                _currentFps = _frameCount / _deltaTimeAccumulator;
                _frameCount = 0;
                _deltaTimeAccumulator = 0f;
            }
        }

        [McpTool("runtime_get_stats", "Get runtime performance statistics",
            ReadOnly = true, Idempotent = true, Group = "runtime")]
        public static ToolResult GetStats()
        {
            return ToolResult.Json(new
            {
                fps = Mathf.RoundToInt(_currentFps),
                frameTime = new
                {
                    current = Time.unscaledDeltaTime * 1000f,
                    smoothed = Time.smoothDeltaTime * 1000f
                },
                memory = new
                {
                    totalAllocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                    totalReservedMB = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f),
                    monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f),
                    monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024f * 1024f),
                    gfxDriverMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f)
                },
                objects = new
                {
                    gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length,
                    totalUnityObjects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Length
                },
                time = new
                {
                    timeScale = Time.timeScale,
                    realtimeSinceStartup = Time.realtimeSinceStartup,
                    frameCount = Time.frameCount
                }
            });
        }
    }
}
#endif
```

### 11.8 Runtime 反射调用（白名单策略）

```csharp
#if UNITY_MCP_RUNTIME
using System;
using System.Reflection;
using UnityEngine;
using UnityMcp.Shared;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Invoke")]
    public static class RuntimeInvokeTools
    {
        [McpTool("runtime_invoke",
            "Invoke a method on a MonoBehaviour. Only methods marked with [McpInvokable] are allowed.",
            Group = "runtime")]
        public static ToolResult Invoke(
            [Desc("GameObject path in hierarchy (e.g. 'Player' or 'UI/Canvas/Panel')")] string gameObjectPath,
            [Desc("Component type name (e.g. 'PlayerController')")] string componentType,
            [Desc("Method name to invoke")] string methodName,
            [Desc("Method arguments as JSON array (optional)")] object[] args = null)
        {
            var go = GameObject.Find(gameObjectPath);
            if (go == null)
                return ToolResult.Error($"GameObject '{gameObjectPath}' not found");

            // 查找组件（按类型名匹配）
            Component target = null;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp.GetType().Name == componentType)
                {
                    target = comp;
                    break;
                }
            }
            if (target == null)
                return ToolResult.Error($"Component '{componentType}' not found on '{gameObjectPath}'");

            // 查找方法
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                return ToolResult.Error($"Method '{methodName}' not found on '{componentType}'");

            // 白名单检查: 必须标记 [McpInvokable]
            if (method.GetCustomAttribute<McpInvokableAttribute>() == null)
                return ToolResult.Error(
                    $"Method '{methodName}' is not marked with [McpInvokable]. " +
                    "Only methods explicitly marked as invokable can be called for safety.");

            try
            {
                var result = method.Invoke(target, args);
                return ToolResult.Json(new
                {
                    success = true,
                    gameObject = gameObjectPath,
                    component = componentType,
                    method = methodName,
                    returnValue = result
                });
            }
            catch (TargetInvocationException ex)
            {
                return ToolResult.Error($"Invocation error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
    }
}
#endif
```

**McpInvokableAttribute 定义**（位于 `Shared/Attributes/`）：

```csharp
namespace UnityMcp.Shared
{
    /// <summary>
    /// 标记一个 MonoBehaviour 方法可以被 runtime_invoke 工具调用。
    /// 未标记此属性的方法将被拒绝，确保安全性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class McpInvokableAttribute : Attribute { }
}
```

**使用示例**：

```csharp
public class PlayerController : MonoBehaviour
{
    [McpInvokable] // 允许 AI 通过 MCP 调用
    public void TakeDamage(float amount)
    {
        health -= amount;
    }

    // 未标记 [McpInvokable] 的方法不可被调用
    private void Die() { /* ... */ }
}
```

### 11.9 Runtime 启用配置

Runtime 模式通过 **条件编译宏** + **Settings UI** 控制：

**启用步骤**：
1. 在 Project Settings → Player → Scripting Define Symbols 中添加 `UNITY_MCP_RUNTIME`
2. 或在 McpSettings 面板中勾选 "Enable Runtime Mode"（自动管理宏定义）

```csharp
// McpSettingsProvider.cs 中的 Runtime 配置 UI 追加
private void DrawRuntimeSection()
{
    EditorGUILayout.Space(10);
    EditorGUILayout.LabelField("Runtime Mode (v2.2)", EditorStyles.boldLabel);

    var enableRuntime = EditorGUILayout.Toggle(
        new GUIContent("Enable Runtime MCP",
            "Allow MCP connections in built games. Adds UNITY_MCP_RUNTIME define."),
        McpSettings.instance.enableRuntime);

    if (enableRuntime != McpSettings.instance.enableRuntime)
    {
        McpSettings.instance.enableRuntime = enableRuntime;
        UpdateScriptingDefines(enableRuntime);
    }

    if (enableRuntime)
    {
        EditorGUI.indentLevel++;
        McpSettings.instance.runtimePort = EditorGUILayout.IntField(
            new GUIContent("Runtime Port", "0 = auto (Editor port + 1)"),
            McpSettings.instance.runtimePort);
        McpSettings.instance.runtimeAutoStart = EditorGUILayout.Toggle(
            "Auto Start on Launch", McpSettings.instance.runtimeAutoStart);
        EditorGUI.indentLevel--;
    }

    EditorGUILayout.HelpBox(
        "Runtime mode allows AI to interact with built games.\n" +
        "⚠️ Do NOT enable in production builds — this is for development/testing only.",
        MessageType.Warning);
}

private static void UpdateScriptingDefines(bool enable)
{
    var target = EditorUserBuildSettings.selectedBuildTargetGroup;
    var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
    var list = defines.Split(';').ToList();

    if (enable && !list.Contains("UNITY_MCP_RUNTIME"))
        list.Add("UNITY_MCP_RUNTIME");
    else if (!enable)
        list.Remove("UNITY_MCP_RUNTIME");

    PlayerSettings.SetScriptingDefineSymbolsForGroup(target, string.Join(";", list));
}
```

### 11.10 Runtime 安全考量

| 风险 | 缓解措施 |
|------|---------|
| **生产环境暴露** | `UNITY_MCP_RUNTIME` 默认不启用，条件编译确保代码不编进 Release 包 |
| **任意反射调用** | 白名单策略：仅允许 `[McpInvokable]` 标记的方法 |
| **端口暴露** | 仅监听 `localhost`（`IPAddress.Loopback`），外部网络无法访问 |
| **性能开销** | `Update()` 每帧最多处理 10 项，FPS 统计零分配（无 new） |
| **内存泄漏** | 截图工具使用 `RenderTexture.GetTemporary` + 及时 `Destroy` |
| **移动端兼容** | TCP 监听在 iOS/Android 需要特殊权限（文档说明） |

---

## 12. Roslyn 动态代码执行（v2.3 新增）

> **来源**: Unity-MCP 是唯一支持 Roslyn 动态执行的参考项目（通过 `code_execute` 工具）。本设计在其基础上增加了沙箱安全策略和超时保护。

### 12.1 功能概述

| 维度 | 说明 |
|------|------|
| **编译器** | Microsoft.CodeAnalysis.CSharp (Roslyn) |
| **执行环境** | Editor (主线程) + Runtime (可选) |
| **返回内容** | 编译诊断 + 执行返回值 + Console.Write 捕获 |
| **安全策略** | API 白名单 + 超时 (默认 10s) + 禁止危险命名空间 |
| **UPM 依赖** | 无 — Unity 2021.3+ 已内置 Roslyn (com.unity.roslyn) |

### 12.2 code_execute 工具设计

```csharp
[McpToolGroup("CodeExecution")]
public static class CodeExecutionTools
{
    [McpTool("code_execute",
        "Compile and execute arbitrary C# code snippet using Roslyn. " +
        "The code should define a static method 'object Run()' which will be invoked. " +
        "Has access to UnityEngine and UnityEditor namespaces. " +
        "Dangerous APIs (File IO, Network, Process) are blocked.",
        Group = "advanced")]
    public static ToolResult Execute(
        [Desc("C# code snippet. Must contain a static method: object Run()")] string code,
        [Desc("Execution timeout in seconds (default: 10, max: 60)")] int timeoutSeconds = 10)
    {
        if (timeoutSeconds > 60) timeoutSeconds = 60;

        // 1. 安全检查 — 禁止危险 API
        var securityResult = SecurityChecker.Validate(code);
        if (!securityResult.IsValid)
            return ToolResult.Error($"Security violation: {securityResult.Reason}");

        // 2. 编译
        var compilation = CreateCompilation(code);
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new
                {
                    id = d.Id,
                    message = d.GetMessage(),
                    line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                });

            return ToolResult.Json(new
            {
                success = false,
                phase = "compilation",
                errors,
                warnings = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Warning)
                    .Select(d => d.GetMessage())
            });
        }

        // 3. 加载并执行
        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        var entryType = assembly.GetTypes()
            .FirstOrDefault(t => t.GetMethod("Run",
                BindingFlags.Public | BindingFlags.Static) != null);

        if (entryType == null)
            return ToolResult.Error(
                "Code must contain a public static method: object Run()");

        var method = entryType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

        // 4. 捕获 Console 输出
        var consoleOutput = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(consoleOutput);

        try
        {
            object result = null;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var task = Task.Run(() => method.Invoke(null, null), cts.Token);

            if (!task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
                return ToolResult.Error($"Execution timed out after {timeoutSeconds}s");

            result = task.Result;

            return ToolResult.Json(new
            {
                success = true,
                returnValue = result,
                returnType = result?.GetType().Name ?? "null",
                consoleOutput = consoleOutput.ToString(),
                executionTimeMs = (int)task.Result // placeholder
            });
        }
        catch (TargetInvocationException ex)
        {
            return ToolResult.Json(new
            {
                success = false,
                phase = "execution",
                error = ex.InnerException?.Message ?? ex.Message,
                stackTrace = ex.InnerException?.StackTrace,
                consoleOutput = consoleOutput.ToString()
            });
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [McpTool("code_validate",
        "Compile C# code without executing — returns diagnostics only. " +
        "Useful for checking if generated code compiles before writing to file.",
        ReadOnly = true, Idempotent = true, Group = "advanced")]
    public static ToolResult Validate(
        [Desc("C# code snippet to validate")] string code)
    {
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        return ToolResult.Json(new
        {
            isValid = !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
            errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new { d.Id, message = d.GetMessage(),
                    line = d.Location.GetLineSpan().StartLinePosition.Line + 1 }),
            warnings = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(d => new { d.Id, message = d.GetMessage(),
                    line = d.Location.GetLineSpan().StartLinePosition.Line + 1 })
        });
    }

    private static CSharpCompilation CreateCompilation(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // 引用 Unity 程序集 + 标准库
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a =>
            {
                var name = a.GetName().Name;
                return name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor")
                    || name == "mscorlib" || name == "System" || name == "System.Core"
                    || name == "netstandard" || name.StartsWith("System.");
            })
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        return CSharpCompilation.Create(
            $"McpDynamic_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
```

### 12.3 安全检查器

```csharp
public static class SecurityChecker
{
    // 禁止的命名空间/类型
    private static readonly string[] ForbiddenPatterns = new[]
    {
        "System.IO.File",           // 文件读写
        "System.IO.Directory",      // 目录操作
        "System.IO.Path",           // 路径操作（允许 Path.Combine 等无害操作时可放宽）
        "System.Net",               // 网络操作
        "System.Diagnostics.Process",// 进程启动
        "System.Environment.Exit",  // 强制退出
        "System.Reflection.Emit",   // 动态 IL 生成
        "System.Runtime.InteropServices.DllImport", // P/Invoke
        "UnityEditor.AssetDatabase.DeleteAsset",    // 危险资产操作
        "UnityEditor.FileUtil.DeleteFileOrDirectory",
        "Application.Quit",         // 退出应用
    };

    // 禁止的 using 声明
    private static readonly string[] ForbiddenUsings = new[]
    {
        "System.Net",
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Diagnostics",
    };

    public static (bool IsValid, string Reason) Validate(string code)
    {
        foreach (var pattern in ForbiddenPatterns)
        {
            if (code.Contains(pattern))
                return (false, $"Forbidden API: {pattern}");
        }

        foreach (var ns in ForbiddenUsings)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(code, $@"using\s+{ns.Replace(".", @"\.")}"))
                return (false, $"Forbidden namespace: {ns}");
        }

        return (true, null);
    }
}
```

### 12.4 Runtime 版本差异

| 维度 | Editor 版本 | Runtime 版本 |
|------|------------|-------------|
| **可用程序集** | UnityEngine + UnityEditor | 仅 UnityEngine |
| **额外限制** | 禁止危险 AssetDatabase 操作 | 禁止所有 UnityEditor 引用 |
| **Roslyn 可用性** | Unity 内置 | 需随构建包含 Roslyn DLL（~5MB） |
| **使用场景** | 代码验证、快速原型、批量操作 | 运行时调试、动态行为注入 |

> **注意**: Runtime 版本需要在构建时将 `Microsoft.CodeAnalysis.CSharp.dll` 和 `Microsoft.CodeAnalysis.dll` 包含在 Plugins/ 中。通过 `UNITY_MCP_RUNTIME_ROSLYN` 额外条件编译控制。

### 12.5 code_execute 使用示例

```json
{
  "name": "code_execute",
  "arguments": {
    "code": "using UnityEngine;\npublic class Snippet {\n    public static object Run() {\n        var count = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;\n        return new { cameraCount = count, mainCamera = Camera.main?.name };\n    }\n}",
    "timeoutSeconds": 5
  }
}
```

响应：
```json
{
  "success": true,
  "returnValue": { "cameraCount": 2, "mainCamera": "Main Camera" },
  "returnType": "AnonymousType",
  "consoleOutput": "",
  "executionTimeMs": 42
}
```

---

## 13. 多 Unity 实例管理（v2.3 新增）

> **来源**: unity-mcp-beta 是唯一支持多 Unity 实例的参考项目（通过 `set_active_instance` 路由，WebSocket 多连接管理）。

### 13.1 设计概述

支持同时连接多个 Unity Editor 实例，适用于：
- 同一项目的多个 Unity 版本测试
- 多项目并行开发
- Multiplayer 多实例调试

```
MCP 客户端 (AI)
    ↓ stdio
Bridge / Python Server
    ↓ 实例路由 (activeInstance)
    ├── TCP → Unity Instance A (port 50123)
    ├── TCP → Unity Instance B (port 50456)
    └── TCP → Unity Instance C (port 50789)
```

### 13.2 实例发现机制

每个 Unity 实例启动时，在**确定性端口**（SHA256(projectPath)）上监听。Bridge/Python Server 通过以下方式发现实例：

```csharp
public class InstanceDiscovery
{
    // 实例注册文件目录
    private static readonly string RegistryDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityMCP", "instances");

    /// <summary>
    /// Unity 实例启动时注册自身
    /// </summary>
    public static void Register(int port, string projectPath)
    {
        Directory.CreateDirectory(RegistryDir);

        var info = new InstanceInfo
        {
            Port = port,
            ProjectPath = projectPath,
            ProjectName = Path.GetFileName(projectPath),
            Pid = System.Diagnostics.Process.GetCurrentProcess().Id,
            UnityVersion = Application.unityVersion,
            StartTime = DateTime.UtcNow.ToString("o"),
        };

        var filePath = Path.Combine(RegistryDir, $"{port}.json");
        File.WriteAllText(filePath, JsonUtility.ToJson(info, true));
    }

    /// <summary>
    /// Unity 实例关闭时注销
    /// </summary>
    public static void Unregister(int port)
    {
        var filePath = Path.Combine(RegistryDir, $"{port}.json");
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    /// <summary>
    /// 发现所有存活实例（清理僵尸注册）
    /// </summary>
    public static List<InstanceInfo> DiscoverAll()
    {
        if (!Directory.Exists(RegistryDir))
            return new List<InstanceInfo>();

        var instances = new List<InstanceInfo>();
        foreach (var file in Directory.GetFiles(RegistryDir, "*.json"))
        {
            try
            {
                var info = JsonUtility.FromJson<InstanceInfo>(File.ReadAllText(file));

                // 检查进程是否存活
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(info.Pid);
                    if (!process.HasExited)
                    {
                        instances.Add(info);
                        continue;
                    }
                }
                catch { /* 进程不存在 */ }

                // 僵尸注册，清理
                File.Delete(file);
            }
            catch { File.Delete(file); }
        }

        return instances;
    }
}

[Serializable]
public class InstanceInfo
{
    public int Port;
    public string ProjectPath;
    public string ProjectName;
    public int Pid;
    public string UnityVersion;
    public string StartTime;
}
```

### 13.3 Bridge 侧多实例路由

```csharp
// Bridge Program.cs 中的实例管理
public class InstanceRouter
{
    private readonly Dictionary<int, TcpClient> _connections = new();
    private int _activePort;

    /// <summary>
    /// 连接到所有已发现的实例
    /// </summary>
    public async Task ConnectAll()
    {
        var instances = InstanceDiscovery.DiscoverAll();
        foreach (var inst in instances)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, inst.Port);
                _connections[inst.Port] = client;

                if (_activePort == 0)
                    _activePort = inst.Port; // 默认第一个为活跃实例
            }
            catch { /* 连接失败，跳过 */ }
        }
    }

    /// <summary>
    /// 设置活跃实例
    /// </summary>
    public bool SetActive(int port)
    {
        if (!_connections.ContainsKey(port)) return false;
        _activePort = port;
        return true;
    }

    /// <summary>
    /// 获取活跃实例的连接
    /// </summary>
    public TcpClient GetActiveConnection() =>
        _connections.TryGetValue(_activePort, out var client) ? client : null;
}
```

### 13.4 Python Server 侧多实例路由

```python
# unity_mcp_server/instance_manager.py
import json
import asyncio
from pathlib import Path

REGISTRY_DIR = Path.home() / ".local" / "share" / "UnityMCP" / "instances"
if not REGISTRY_DIR.exists():
    REGISTRY_DIR = Path.home() / "AppData" / "Local" / "UnityMCP" / "instances"  # Windows

class InstanceManager:
    def __init__(self):
        self._connections: dict[int, asyncio.StreamWriter] = {}
        self._instances: dict[int, dict] = {}
        self._active_port: int = 0

    async def discover_and_connect(self):
        """发现并连接所有 Unity 实例"""
        if not REGISTRY_DIR.exists():
            return

        for file in REGISTRY_DIR.glob("*.json"):
            try:
                info = json.loads(file.read_text())
                port = info["Port"]
                reader, writer = await asyncio.open_connection("127.0.0.1", port)
                self._connections[port] = writer
                self._instances[port] = info

                if self._active_port == 0:
                    self._active_port = port
            except Exception:
                file.unlink(missing_ok=True)  # 清理僵尸

    def set_active(self, port: int) -> bool:
        if port not in self._connections:
            return False
        self._active_port = port
        return True

    def get_active(self) -> asyncio.StreamWriter | None:
        return self._connections.get(self._active_port)

    def list_instances(self) -> list[dict]:
        return [
            {**info, "isActive": port == self._active_port}
            for port, info in self._instances.items()
        ]
```

### 13.5 instance_list / instance_set_active 工具实现

```csharp
[McpToolGroup("InstanceManagement")]
public static class InstanceTools
{
    [McpTool("instance_list",
        "List all connected Unity Editor instances with project name, port, PID, and Unity version.",
        ReadOnly = true, Idempotent = true, Group = "instance")]
    public static ToolResult ListInstances()
    {
        var instances = InstanceDiscovery.DiscoverAll();
        return ToolResult.Json(new
        {
            count = instances.Count,
            instances = instances.Select(i => new
            {
                port = i.Port,
                projectName = i.ProjectName,
                projectPath = i.ProjectPath,
                pid = i.Pid,
                unityVersion = i.UnityVersion,
                startTime = i.StartTime
            })
        });
    }

    [McpTool("instance_set_active",
        "Set the active Unity instance. All subsequent tool calls will be routed to this instance.",
        Group = "instance")]
    public static ToolResult SetActive(
        [Desc("Port number of the target Unity instance")] int port)
    {
        // 注意: 实际路由切换在 Bridge/Python Server 侧完成
        // 此工具通过特殊返回值通知 Server 层切换
        var instances = InstanceDiscovery.DiscoverAll();
        var target = instances.FirstOrDefault(i => i.Port == port);
        if (target == null)
            return ToolResult.Error($"No Unity instance found on port {port}");

        return ToolResult.Json(new
        {
            success = true,
            activeInstance = new
            {
                port = target.Port,
                projectName = target.ProjectName,
                unityVersion = target.UnityVersion
            },
            _meta = new { routeSwitch = port } // Bridge/Server 识别此标记进行路由切换
        });
    }
}
```

### 13.6 McpServer 启动时注册

```csharp
// McpServer.cs 中增加实例注册
[InitializeOnLoad]
public static class McpServer
{
    static McpServer()
    {
        var port = PortResolver.GetPort();
        // ... 原有初始化逻辑 ...

        // v2.3: 注册实例供多实例管理发现
        InstanceDiscovery.Register(port, Application.dataPath.Replace("/Assets", ""));

        // 域重载前注销
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            InstanceDiscovery.Unregister(port);
        };

        // 退出时注销
        EditorApplication.quitting += () =>
        {
            InstanceDiscovery.Unregister(port);
        };
    }
}
```

---

## 14. Docker 部署（v2.4 新增）

> **来源**: unity-mcp-beta 和 Unity-MCP 都支持 Docker 部署。本设计基于 Python Server 模式（模式B）天然适配容器化。

### 14.1 适用场景

- **团队共享 Server**：多个开发者的 AI 客户端连接同一个 Python MCP Server
- **CI/CD 集成**：在 CI 环境中运行 MCP Server 执行自动化 Unity 操作
- **隔离部署**：Server 在容器中运行，与宿主机环境隔离

### 14.2 Dockerfile

```dockerfile
FROM python:3.12-slim

WORKDIR /app

# 安装依赖
COPY Server~/pyproject.toml Server~/README.md ./
RUN pip install --no-cache-dir -e .

# 复制 Server 代码
COPY Server~/unity_mcp_server/ ./unity_mcp_server/

# 暴露 HTTP 端口 (Streamable HTTP 模式)
EXPOSE 8080

# 环境变量
ENV UNITY_MCP_HOST=0.0.0.0
ENV UNITY_MCP_PORT=8080
ENV UNITY_MCP_TRANSPORT=http

# 启动
CMD ["python", "-m", "unity_mcp_server.server"]
```

### 14.3 docker-compose.yml

```yaml
version: '3.8'

services:
  unity-mcp-server:
    build:
      context: .
      dockerfile: Server~/Dockerfile
    ports:
      - "8080:8080"            # MCP HTTP 端口 (AI 客户端连接)
    environment:
      - UNITY_MCP_TRANSPORT=http
      - UNITY_MCP_UNITY_HOST=host.docker.internal  # 容器内访问宿主机 Unity
      - UNITY_MCP_UNITY_PORT=50123                  # Unity TCP 端口
    extra_hosts:
      - "host.docker.internal:host-gateway"  # Linux 支持
    restart: unless-stopped
```

### 14.4 架构说明

```
┌─────────────────────┐     ┌─────────────────────────────┐
│  AI 客户端 (Claude)  │     │  Docker Container            │
│                     │     │  ┌────────────────────────┐  │
│  MCP Client         ├────►│  │ Python MCP Server      │  │
│  (HTTP 连接)        │:8080│  │ FastMCP + Streamable   │  │
│                     │     │  └──────────┬─────────────┘  │
└─────────────────────┘     │             │ TCP             │
                            │  host.docker.internal:{port} │
                            └──────────┬──────────────────┘
                                       │
                            ┌──────────▼──────────────────┐
                            │  宿主机 Unity Editor         │
                            │  TcpListener localhost:50123 │
                            └─────────────────────────────┘
```

> **注意**: Docker 模式仅适用于 **模式B (Python Server)**。模式A (C# Bridge) 使用 stdio 传输，不需要也不适合容器化。
> Unity Editor 的 TCP 监听需要对 Docker 容器可达（通过 `host.docker.internal`）。

---

## 15. Multiplayer Play Mode 感知（v2.4 新增）

> **来源**: mcp-unity 是唯一支持 Multiplayer Play Mode (MPPM) 感知的参考项目。MPPM 克隆实例如果也启动 MCP Server 会导致端口冲突和重复操作。

### 15.1 问题描述

Unity 2023.1+ 的 **Multiplayer Play Mode** 功能可以在同一台机器上启动多个 Editor 虚拟玩家实例（克隆）。克隆实例与主 Editor 共享同一项目路径，导致：

1. **端口冲突**：SHA256(projectPath) 计算出相同端口号
2. **重复操作**：AI 的工具调用会同时影响主 Editor 和克隆
3. **资源竞争**：多个实例同时修改 AssetDatabase 导致冲突

### 15.2 检测与跳过逻辑

```csharp
// McpServer.cs 启动时检测
[InitializeOnLoad]
public static class McpServer
{
    static McpServer()
    {
        // v2.4: MPPM 克隆实例检测 — 跳过 MCP Server 启动
        if (IsMppmClone())
        {
            McpLogger.Info("[MCP] Skipping MCP Server on MPPM clone instance");
            return;
        }

        // ... 正常初始化流程 ...
    }

    /// <summary>
    /// 检测当前 Editor 是否为 MPPM 克隆实例。
    /// Unity 2023.1+ 中，克隆实例通过命令行参数 -intanceId 标识。
    /// </summary>
    private static bool IsMppmClone()
    {
#if UNITY_2023_1_OR_NEWER
        // 方法1: 检查 MPPM API (推荐)
        try
        {
            var mppmType = Type.GetType(
                "Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode");
            if (mppmType != null)
            {
                var readOnlyTagsProp = mppmType.GetProperty("ReadOnlyTags",
                    BindingFlags.Public | BindingFlags.Static);
                if (readOnlyTagsProp != null)
                {
                    // 有 MPPM API 且有 Tags → 是虚拟玩家（克隆或主）
                    // 进一步检查是否是克隆
                    var typeProp = mppmType.GetProperty("Type",
                        BindingFlags.Public | BindingFlags.Static);
                    if (typeProp != null)
                    {
                        var typeValue = typeProp.GetValue(null)?.ToString();
                        return typeValue == "Clone";
                    }
                }
            }
        }
        catch { /* MPPM 包未安装，忽略 */ }

        // 方法2: 回退检查命令行参数
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-intanceId" || args[i] == "-instanceId")
            {
                // 有 instanceId 参数 → 克隆实例
                return true;
            }
        }
#endif
        return false;
    }
}
```

### 15.3 editor_is_clone / editor_get_mppm_tags 工具实现

```csharp
[McpToolGroup("Editor")]
public static class MppmTools
{
    [McpTool("editor_is_clone",
        "Check if current editor is a Multiplayer Play Mode clone instance",
        ReadOnly = true, Idempotent = true, Group = "editor")]
    public static ToolResult IsClone()
    {
        bool isClone = false;
        string playerType = "Main";

#if UNITY_2023_1_OR_NEWER
        try
        {
            var mppmType = Type.GetType(
                "Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode");
            if (mppmType != null)
            {
                var typeProp = mppmType.GetProperty("Type",
                    BindingFlags.Public | BindingFlags.Static);
                if (typeProp != null)
                {
                    playerType = typeProp.GetValue(null)?.ToString() ?? "Main";
                    isClone = playerType == "Clone";
                }
            }
        }
        catch { }
#endif

        return ToolResult.Json(new
        {
            isClone,
            playerType,
            mppmAvailable = playerType != "Main" || isClone
        });
    }

    [McpTool("editor_get_mppm_tags",
        "Get Multiplayer Play Mode player tags for the current instance",
        ReadOnly = true, Idempotent = true, Group = "editor")]
    public static ToolResult GetMppmTags()
    {
#if UNITY_2023_1_OR_NEWER
        try
        {
            var mppmType = Type.GetType(
                "Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode");
            if (mppmType != null)
            {
                var tagsProp = mppmType.GetProperty("ReadOnlyTags",
                    BindingFlags.Public | BindingFlags.Static);
                if (tagsProp != null)
                {
                    var tags = tagsProp.GetValue(null) as IEnumerable<string>;
                    return ToolResult.Json(new
                    {
                        available = true,
                        tags = tags?.ToArray() ?? Array.Empty<string>()
                    });
                }
            }
        }
        catch { }
#endif
        return ToolResult.Json(new { available = false, tags = Array.Empty<string>() });
    }
}
```

---

## 16. 变更总结

### 16.1 v1.0 → v2.0 核心变更

| 变更项 | v1.0 | v2.0 | 原因 |
|--------|------|------|------|
| 传输层 | HttpListener (Streamable HTTP) | **TcpListener + 消息帧** | macOS 兼容 + 规避 SSE/Session 复杂性 |
| MCP 传输 | Streamable HTTP 直连 | **stdio 桥接 / Python Server** | 标准合规 + 所有客户端兼容 |
| Server 模式 | 单一内嵌 | **双模式可切换** | 满足不同场景需求 |
| 同步 API | Run\<T\> (阻塞) | **移除，仅 RunAsync\<T\>** | 消除死锁 |
| 请求超时 | 无 | **默认 60s/30s** | 防止卡死 |
| ToolResult | Success(object) / Success(string) | **Text() / Json() / Error()** | 消除重载歧义 |
| Resource URI | 精确匹配 | **正则模板匹配** | 支持 `{id}` 路径参数 |
| batch_execute | P2 | **P0** | 核心性能特性 |
| 日志 | Debug.Log 直出 | **分级 + 审计** | 可控 + 可追溯 |
| 域重载 | 简单重启 | **通知 + 端口持久化 + 自动重连** | 无缝恢复 |
| 每帧处理量 | 无限制 | **每帧最多 10 项** | 避免编辑器卡顿 |
| 配置 UI | 无 | **Project Settings 完整面板** | 双模式切换 + 快捷配置 |
| 进程管理 | 无 | **ServerProcessManager** | 桥接/Python 进程生命周期 |
| Python Server | 无 | **FastMCP + Python 侧增强工具** | 完整 MCP 合规 + 扩展能力 |

### 16.2 v2.0 → v2.1 增量优化（基于参考项目源码交叉验证）

| 编号 | 类型 | 变更点 | 来源 |
|------|------|--------|------|
| 1 | 修改 | TCP 帧增加 1 字节类型标识 (Request/Response/Notification) | MCP 规范 + unity-mcp-beta |
| 2 | 修改 | initialize 完整 capabilities JSON 结构 | mcp-unity + Unity-MCP + MCP 规范 |
| 3 | 修改 | McpToolAttribute 增加 Idempotent/ReadOnly/Group/AutoRegister/Title | Unity-MCP + unity-mcp-beta |
| 4 | 修改 | MainThreadDispatcher 增加 CompilationPipeline.compilationFinished 回调 | Unity-MCP |
| 5 | 修改 | ServerProcessManager 增加崩溃检测 + 孤立进程清理 + 域重载恢复 | Unity-MCP McpServerManager |
| 6 | 修改 | ToolRegistry 改用 TypeCache 扫描，移除 [assembly: ContainsMcpTools] | unity-mcp-beta ToolDiscoveryService |
| 7 | 新增 | UnityTypeConverters 完整实现 (7种类型 + JSON Schema 生成) | Unity-MCP Converter 目录 |
| 8 | 新增 | ParameterBinder 完整参数绑定 (Unity类型+enum+nullable+JObject) | Unity-MCP + unity-mcp-beta |
| 9 | 修改 | 配置窗口增加 per-tool 启用/禁用管理 | unity-mcp-beta ToolDiscoveryService |
| 10 | 修改 | Bridge 增加指数退避 + 抖动 + 域重载感知 | mcp-unity + unity-mcp-beta |
| 11 | 修改 | batch_execute 增加 `id` 字段用于结果关联 | mcp-unity BatchExecuteTool |
| 12 | 新增 | 连接候选地址 IPv4 + IPv6 | unity-mcp-beta BuildConnectionCandidateUris |

### 16.3 v2.1 → v2.2 增量优化（Runtime 模式支持）

| 编号 | 类型 | 变更点 | 来源 |
|------|------|--------|------|
| 1 | 新增 | **Shared 程序集**：Attributes/Models/Utils 从 Editor 提取到共享层 | 架构重构 |
| 2 | 新增 | **IMainThreadDispatcher 接口**：Editor 和 Runtime 共享调度抽象 | 架构重构 |
| 3 | 新增 | **McpRuntimeBehaviour**：MonoBehaviour 入口 + DontDestroyOnLoad + RuntimeInitializeOnLoadMethod | Unity-MCP Runtime 架构 |
| 4 | 新增 | **RuntimeMainThreadDispatcher**：基于 MonoBehaviour.Update() 的主线程调度 | Unity-MCP |
| 5 | 新增 | **RuntimeToolRegistry**：AppDomain 反射扫描（替代 Editor 的 TypeCache） | Unity-MCP |
| 6 | 新增 | **截图工具** (2个)：screenshot_game / screenshot_camera (Camera.Render + RenderTexture) | Unity-MCP Screenshot 功能 |
| 7 | 新增 | **性能监控工具** (3个)：runtime_get_stats / runtime_profiler_snapshot / runtime_get_logs | Unity-MCP + 自研 |
| 8 | 新增 | **运行时控制工具** (3个)：runtime_time_scale / runtime_load_scene / runtime_invoke | Unity-MCP 反射调用 |
| 9 | 新增 | **McpInvokableAttribute**：白名单反射策略（比 Unity-MCP 全开放反射更安全） | 安全性改进 |
| 10 | 新增 | **Runtime 资源** (3个)：runtime/stats / runtime/scene / runtime/objects | Unity-MCP |
| 11 | 修改 | **架构图**：新增 Runtime 进程路径 | 文档更新 |
| 12 | 修改 | **目录结构**：新增 Shared/ + Runtime/ 目录，Editor 引用 Shared | 架构重构 |
| 13 | 修改 | **条件编译**：`UNITY_MCP_RUNTIME` 宏控制 Runtime 代码编译 | 安全性 |
| 14 | 新增 | **Runtime 安全考量**：localhost-only、白名单反射、条件编译、性能限制 | 安全性 |

### 16.4 v2.2 → v2.3 增量优化（Roslyn 动态执行 / Prompts 扩充 / 多实例管理）

| 编号 | 类型 | 变更点 | 来源 |
|------|------|--------|------|
| 1 | 新增 | **code_execute 工具**：Roslyn 编译+执行 C# 代码片段，返回编译诊断+执行结果+Console 输出 | Unity-MCP Roslyn |
| 2 | 新增 | **code_validate 工具**：仅编译验证不执行，用于代码检查 | Unity-MCP + 自研 |
| 3 | 新增 | **SecurityChecker 沙箱**：禁止 File IO/Network/Process/Reflection.Emit 等危险 API | 安全性改进（Unity-MCP 无此限制） |
| 4 | 新增 | **执行超时保护**：默认 10s，最大 60s，防止死循环 | 安全性改进 |
| 5 | 新增 | **instance_list 工具**：列出所有已连接 Unity 实例 | unity-mcp-beta set_active_instance |
| 6 | 新增 | **instance_set_active 工具**：切换活跃实例路由 | unity-mcp-beta set_active_instance |
| 7 | 新增 | **InstanceDiscovery 机制**：文件注册 + PID 存活检查 + 僵尸清理 | 自研（参考 unity-mcp-beta 思路） |
| 8 | 新增 | **Bridge/Python 实例路由**：InstanceRouter / InstanceManager 多连接管理 | unity-mcp-beta |
| 9 | 修改 | **Prompts 扩充**：12 → 32 个，分 P0/P1/P2 三级，覆盖编码/架构/系统设计/专业领域 | Unity-MCP (48个) + 自研 |
| 10 | 修改 | **工具总数**：59 → 62 个（+2 Roslyn +2 多实例，-1 code_execute 从 P2 移至 P1） | 功能补全 |
| 11 | 修改 | **目录结构**：新增 Shared/Instance/ + Editor/Tools/CodeExecutionTools + InstanceTools + Prompts 文件 | 架构调整 |
| 12 | 修改 | **McpServer 启动流程**：增加 InstanceDiscovery.Register 实例注册 | 多实例支持 |

### 16.5 v2.3 → v2.4 增量优化（VFX / Prompts / Docker / MPPM）

| 编号 | 类型 | 变更点 | 来源 |
|------|------|--------|------|
| 1 | 新增 | **VFX/粒子工具** (4个)：vfx_create_particle / vfx_modify_particle / vfx_create_graph / vfx_get_info | unity-mcp-beta |
| 2 | 新增 | **MPPM 感知工具** (2个)：editor_is_clone / editor_get_mppm_tags | mcp-unity |
| 3 | 新增 | **MPPM 克隆检测**：McpServer 启动时检测克隆实例并跳过初始化 | mcp-unity |
| 4 | 新增 | **Docker 部署**：Dockerfile + docker-compose.yml + 架构说明 | unity-mcp-beta + Unity-MCP |
| 5 | 修改 | **Prompts 扩充**：32 → 48 个（+2 P0 + 4 P1 + 10 P2），追平 Unity-MCP | Unity-MCP (48个) |
| 6 | 修改 | **工具总数**：62 → 68 个（+4 VFX + 2 MPPM） | 功能补全 |
| 7 | 修改 | **目录结构**：新增 VfxTools.cs / MppmTools.cs / Prompts 文件 + Docker 配置 | 架构调整 |
