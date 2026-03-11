# Unity MCP 渐进式优化实施计划

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 去除 Bridge 层、统一输出格式、重写设置窗口（UI Toolkit）、添加 CI/CD 和 LLM 文档、修复零散问题。

**Architecture:** Python Server 作为唯一 MCP 入口（stdio/streamable-http），通过 TCP 帧协议与 Unity Editor 通信。域重载缓冲从 Bridge 迁移到 Python 的 asyncio.Queue。设置窗口从 IMGUI 迁移到 UI Toolkit 模块化 Section 架构。

**Tech Stack:** C# (Unity 2021.2+, UI Toolkit), Python 3.10+ (FastMCP, asyncio), GitHub Actions

**Spec:** `docs/superpowers/specs/2026-03-11-progressive-optimization-design.md`

---

## File Structure

### Phase 1: Bridge 移除 + Python 缓冲（P0）

| 操作 | 文件 | 职责 |
|------|------|------|
| 删除 | `unity-bridge/` (整个目录) | C# stdio-to-TCP bridge |
| 删除 | `unity-mcp/Bridge~/` (整个目录) | 预编译二进制 |
| 删除 | `unity-mcp/Editor/Core/ServerProcessManager.cs` | Bridge 进程管理 |
| 删除 | `scripts/build_bridge.sh` | Bridge 构建脚本 |
| 修改 | `unity-mcp/Editor/Core/McpServer.cs` | 移除 s_processManager |
| 修改 | `unity-mcp/Editor/Core/McpSettings.cs` | 移除 ServerMode/BridgePath |
| 重写 | `unity-server/unity_mcp_server/unity_connection.py` | 添加域重载缓冲 |
| 新建 | `unity-server/tests/test_unity_connection.py` | 连接+缓冲测试 |

### Phase 2: 统一输出格式（P0）

| 操作 | 文件 | 职责 |
|------|------|------|
| 修改 | `unity-mcp/Editor/Tools/*.cs` (26 文件) | 移除 success 字段 |
| 修改 | `unity-mcp/Runtime/Tools/*.cs` (5 文件) | 移除 success 字段 |

### Phase 3: 设置窗口 UI Toolkit 重写（P1）

| 操作 | 文件 | 职责 |
|------|------|------|
| 删除 | `unity-mcp/Editor/Window/McpSettingsWindow.cs` | 旧 IMGUI 窗口 |
| 新建 | `unity-mcp/Editor/Window/McpSettingsWindow.cs` | 新主窗口（CreateGUI + 标签页） |
| 新建 | `unity-mcp/Editor/Window/McpSettingsWindow.uxml` | 主窗口布局 |
| 新建 | `unity-mcp/Editor/Window/McpSettingsWindow.uss` | 主窗口样式 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpConnectionSection.cs` | 连接/服务器设置 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpConnectionSection.uxml` | 连接区布局 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpClientConfigSection.cs` | 客户端配置器列表 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpClientConfigSection.uxml` | 客户端区布局 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpToolsSection.cs` | 工具列表 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpToolsSection.uxml` | 工具区布局 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpAdvancedSection.cs` | 高级设置+诊断 |
| 新建 | `unity-mcp/Editor/Window/Sections/McpAdvancedSection.uxml` | 高级区布局 |
| 新建 | `unity-mcp/Editor/Window/ClientConfig/ClientProfile.cs` | 客户端描述数据 |
| 新建 | `unity-mcp/Editor/Window/ClientConfig/ClientRegistry.cs` | 客户端注册表 |
| 新建 | `unity-mcp/Editor/Window/ClientConfig/IConfigWriter.cs` | 写入策略接口 |
| 新建 | `unity-mcp/Editor/Window/ClientConfig/JsonFileConfigWriter.cs` | JSON 文件写入 |
| 新建 | `unity-mcp/Editor/Window/ClientConfig/ClaudeCliConfigWriter.cs` | Claude CLI 写入 |

### Phase 4: CI/CD + 版本管理（P1）

| 操作 | 文件 | 职责 |
|------|------|------|
| 新建 | `.github/workflows/ci.yml` | PR/Push 验证 |
| 新建 | `.github/workflows/release-server.yml` | PyPI 自动发布 |
| 新建 | `scripts/bump-version.sh` | 版本号更新脚本 |
| 删除 | `scripts/publish_pypi.sh` | 旧发布脚本（被 CI 替代） |

### Phase 5: LLM 文档（P1）

| 操作 | 文件 | 职责 |
|------|------|------|
| 新建 | `CLAUDE.md` | AI 助手项目引导 |
| 新建 | `AGENTS.md` | 通用 Agent 引导 |

### Phase 6: 零散修复（P2）

| 操作 | 文件 | 职责 |
|------|------|------|
| 修改 | `unity-mcp/Editor/Core/TcpTransport.cs` | 错误吞没修复 |
| 修改 | `unity-server/unity_mcp_server/server.py:262-283` | 私有 API 降级处理 |
| 修改 | `unity-mcp/Editor/Tools/*.cs` (部分) | Undo 补全 |

---

## Chunk 1: Bridge 移除 + Python 域重载缓冲

### Task 1: Python 域重载缓冲 — 测试

**Files:**
- Create: `unity-server/tests/__init__.py`
- Create: `unity-server/tests/test_unity_connection.py`

- [ ] **Step 1: 创建测试目录和文件**

```python
# unity-server/tests/__init__.py
# (empty)
```

```python
# unity-server/tests/test_unity_connection.py
"""Tests for UnityConnection with domain-reload buffering."""
import asyncio
import struct
import json
import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from unity_mcp_server.unity_connection import UnityConnection, MSG_TYPE_REQUEST, MSG_TYPE_RESPONSE


@pytest.fixture
def connection():
    return UnityConnection("127.0.0.1", 51279)


@pytest.mark.asyncio
async def test_send_request_when_connected(connection):
    """Connected state: send_request should write frame and return response."""
    # We'll mock the internals after implementation
    pass


@pytest.mark.asyncio
async def test_send_request_buffers_when_disconnected(connection):
    """Disconnected state: send_request should queue and not raise."""
    pass


@pytest.mark.asyncio
async def test_buffered_requests_replayed_on_reconnect(connection):
    """After reconnect, buffered requests should be sent in order."""
    pass


@pytest.mark.asyncio
async def test_reloading_notification_triggers_buffer_mode(connection):
    """notifications/reloading should switch to buffer mode preemptively."""
    pass


@pytest.mark.asyncio
async def test_reconnect_exponential_backoff(connection):
    """Reconnection should follow the defined backoff schedule."""
    pass
```

- [ ] **Step 2: 验证测试文件创建成功**

Run: `cd unity-server && python -m pytest tests/test_unity_connection.py --collect-only`
Expected: 5 tests collected

- [ ] **Step 3: Commit**

```bash
git add unity-server/tests/
git commit -m "test: add skeleton tests for UnityConnection domain-reload buffering"
```

---

### Task 2: 实现 Python 域重载缓冲

**Files:**
- Modify: `unity-server/unity_mcp_server/unity_connection.py`

- [ ] **Step 1: 重写 UnityConnection 添加缓冲逻辑**

```python
"""Unity TCP connection manager with domain-reload buffering."""
import asyncio
import struct
import json
import logging
from .config import UNITY_HOST, UNITY_PORT, REQUEST_TIMEOUT

logger = logging.getLogger(__name__)

MSG_TYPE_REQUEST = 0x01
MSG_TYPE_RESPONSE = 0x02
MSG_TYPE_NOTIFICATION = 0x03

# Backoff schedule for reconnection (milliseconds)
_RECONNECT_DELAYS_MS = [0, 300, 500, 1000, 1000, 2000, 2000, 3000, 3000, 5000]


class UnityConnection:
    """Manages TCP connection to Unity Editor with domain-reload buffering.

    When the connection drops (e.g. during Unity domain reload), incoming
    send_request() calls are queued in an asyncio.Queue. Once reconnected,
    the queue is drained and all buffered requests are replayed in order.
    """

    def __init__(self, host: str = None, port: int = None):
        self.host = host or UNITY_HOST
        self.port = port or UNITY_PORT
        self._reader: asyncio.StreamReader | None = None
        self._writer: asyncio.StreamWriter | None = None
        self._request_id = 0
        self._pending: dict[int, asyncio.Future] = {}
        self._lock = asyncio.Lock()
        self._connected = False
        self._buffering = False
        self._buffer: asyncio.Queue = asyncio.Queue()
        self._read_task: asyncio.Task | None = None
        self._drain_task: asyncio.Task | None = None
        self._reconnecting = False

    @property
    def connected(self) -> bool:
        return self._connected

    async def connect(self):
        """Connect to Unity Editor TCP server."""
        try:
            self._reader, self._writer = await asyncio.open_connection(
                self.host, self.port
            )
            self._connected = True
            self._buffering = False
            self._read_task = asyncio.create_task(self._read_loop())
            self._drain_task = asyncio.create_task(self._drain_buffer())
            logger.info(f"Connected to Unity at {self.host}:{self.port}")
        except ConnectionRefusedError:
            logger.error(
                f"Cannot connect to Unity at {self.host}:{self.port}. "
                "Is Unity running with MCP enabled?"
            )
            raise

    async def disconnect(self):
        """Close the connection."""
        self._connected = False
        if self._read_task and not self._read_task.done():
            self._read_task.cancel()
        if self._drain_task and not self._drain_task.done():
            self._drain_task.cancel()
        if self._writer:
            self._writer.close()
            try:
                await self._writer.wait_closed()
            except Exception:
                pass
            self._writer = None
            self._reader = None

    async def send_request(self, method: str, params: dict = None) -> dict:
        """Send a JSON-RPC request. Buffers if disconnected."""
        async with self._lock:
            self._request_id += 1
            req_id = self._request_id

        msg = json.dumps({
            "jsonrpc": "2.0",
            "id": req_id,
            "method": method,
            "params": params or {}
        }).encode("utf-8")

        frame_len = 1 + len(msg)
        frame = struct.pack(">IB", frame_len, MSG_TYPE_REQUEST) + msg

        future = asyncio.get_running_loop().create_future()
        self._pending[req_id] = future

        if self._connected and not self._buffering:
            try:
                self._writer.write(frame)
                await self._writer.drain()
            except (ConnectionError, OSError):
                # Connection died mid-write, buffer the frame
                await self._buffer.put(frame)
                self._enter_buffer_mode()
        else:
            await self._buffer.put(frame)

        return await asyncio.wait_for(future, timeout=REQUEST_TIMEOUT)

    def _enter_buffer_mode(self):
        """Switch to buffering mode and start reconnection."""
        if self._buffering:
            return
        self._buffering = True
        self._connected = False
        logger.info("Entered buffer mode (Unity disconnected or reloading)")
        if not self._reconnecting:
            asyncio.create_task(self._reconnect_loop())

    async def _drain_buffer(self):
        """Drain buffered frames when connected."""
        try:
            while True:
                frame = await self._buffer.get()
                # Wait until we're connected and not buffering
                while not self._connected or self._buffering:
                    await asyncio.sleep(0.05)
                try:
                    self._writer.write(frame)
                    await self._writer.drain()
                except (ConnectionError, OSError):
                    # Put it back and wait for reconnection
                    await self._buffer.put(frame)
                    self._enter_buffer_mode()
        except asyncio.CancelledError:
            pass

    async def _read_loop(self):
        """Read frames from Unity and resolve pending futures."""
        try:
            while self._connected:
                header = await self._reader.readexactly(5)
                frame_len = struct.unpack(">I", header[:4])[0]
                msg_type = header[4]
                payload_len = frame_len - 1

                if payload_len <= 0 or payload_len > 10 * 1024 * 1024:
                    logger.error(f"Invalid payload length: {payload_len}")
                    break

                data = await self._reader.readexactly(payload_len)

                try:
                    msg = json.loads(data.decode("utf-8"))
                except (UnicodeDecodeError, json.JSONDecodeError) as e:
                    logger.error(f"Failed to decode message: {e}")
                    continue

                # Handle notifications
                if msg_type == MSG_TYPE_NOTIFICATION:
                    method = msg.get("method", "")
                    if method == "notifications/reloading":
                        logger.info("Unity is reloading, entering buffer mode")
                        self._enter_buffer_mode()
                    continue

                req_id = msg.get("id")
                if req_id and req_id in self._pending:
                    self._pending.pop(req_id).set_result(msg)
                else:
                    logger.debug(f"Received unmatched message: {msg}")
        except asyncio.IncompleteReadError:
            logger.info("Unity connection closed")
        except asyncio.CancelledError:
            return
        except Exception as e:
            logger.error(f"Read loop error: {e}")
        finally:
            self._connected = False
            if not self._buffering:
                self._enter_buffer_mode()

    def _fail_pending(self, reason: str):
        """Fail all pending requests when giving up."""
        for future in self._pending.values():
            if not future.done():
                future.set_exception(ConnectionError(reason))
        self._pending.clear()

    async def _reconnect_loop(self):
        """Reconnect with exponential backoff, then drain buffer."""
        self._reconnecting = True
        try:
            for attempt, delay_ms in enumerate(_RECONNECT_DELAYS_MS):
                if delay_ms > 0:
                    logger.info(f"Reconnecting in {delay_ms}ms (attempt {attempt + 1})")
                    await asyncio.sleep(delay_ms / 1000)
                try:
                    # Clean up old connection
                    if self._writer:
                        self._writer.close()
                        try:
                            await self._writer.wait_closed()
                        except Exception:
                            pass

                    self._reader, self._writer = await asyncio.open_connection(
                        self.host, self.port
                    )
                    self._connected = True
                    self._buffering = False
                    self._read_task = asyncio.create_task(self._read_loop())
                    pending_count = self._buffer.qsize()
                    logger.info(
                        f"Reconnected to Unity, replaying {pending_count} buffered request(s)"
                    )
                    return
                except (ConnectionRefusedError, OSError) as e:
                    logger.warning(f"Reconnect attempt {attempt + 1} failed: {e}")

            # All attempts exhausted — fail buffered requests
            self._fail_pending(
                f"Cannot reconnect to Unity at {self.host}:{self.port}"
            )
            # Clear the buffer (futures already failed)
            while not self._buffer.empty():
                try:
                    self._buffer.get_nowait()
                except asyncio.QueueEmpty:
                    break
        finally:
            self._reconnecting = False

    async def ensure_connected(self):
        """Connect or reconnect to Unity."""
        if self._connected:
            return
        await self.connect()
```

- [ ] **Step 2: 运行测试**

Run: `cd unity-server && python -m pytest tests/test_unity_connection.py -v`
Expected: 5 tests pass (skeleton tests)

- [ ] **Step 3: 补全测试用例**

用 mock 替换 TCP 连接，测试真实缓冲和重放行为：

```python
# 更新 tests/test_unity_connection.py 中的测试

@pytest.mark.asyncio
async def test_send_request_when_connected(connection):
    """Connected state: send_request should write frame and return response."""
    mock_writer = MagicMock()
    mock_writer.write = MagicMock()
    mock_writer.drain = AsyncMock()
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()

    connection._writer = mock_writer
    connection._connected = True
    connection._buffering = False
    connection._drain_task = asyncio.create_task(connection._drain_buffer())

    async def fake_response():
        await asyncio.sleep(0.01)
        # Simulate response arriving
        connection._pending[1].set_result({
            "jsonrpc": "2.0", "id": 1,
            "result": {"content": [{"type": "text", "text": "ok"}]}
        })
    asyncio.create_task(fake_response())

    result = await connection.send_request("tools/call", {"name": "test"})
    assert result["id"] == 1
    mock_writer.write.assert_called_once()
    connection._drain_task.cancel()


@pytest.mark.asyncio
async def test_send_request_buffers_when_disconnected(connection):
    """Disconnected state: send_request should queue without raising immediately."""
    connection._connected = False
    connection._buffering = True

    # send_request will buffer, but the future will timeout since no response
    with pytest.raises(asyncio.TimeoutError):
        await asyncio.wait_for(
            connection.send_request("tools/call", {"name": "test"}),
            timeout=0.1
        )
    assert connection._buffer.qsize() == 1


@pytest.mark.asyncio
async def test_reloading_notification_triggers_buffer_mode(connection):
    """notifications/reloading should switch to buffer mode."""
    connection._connected = True
    connection._buffering = False
    connection._reconnecting = True  # prevent auto-reconnect in test

    connection._enter_buffer_mode()

    assert connection._buffering is True
    assert connection._connected is False
```

- [ ] **Step 4: 运行补全的测试**

Run: `cd unity-server && python -m pytest tests/test_unity_connection.py -v`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add unity-server/unity_mcp_server/unity_connection.py unity-server/tests/
git commit -m "feat: add domain-reload buffering to Python UnityConnection"
```

---

### Task 3: 清理 C# 侧 Bridge 引用

**Files:**
- Modify: `unity-mcp/Editor/Core/McpServer.cs:15,83-84,94-95`
- Modify: `unity-mcp/Editor/Core/McpSettings.cs:12,16,18,27-31,39-43`
- Delete: `unity-mcp/Editor/Core/ServerProcessManager.cs`

- [ ] **Step 1: 修改 McpServer.cs — 移除 s_processManager**

从 `McpServer.cs` 中：
- 删除第 15 行: `private static ServerProcessManager s_processManager;`
- 删除第 83 行: `s_processManager?.StopServer();`
- 删除第 94-95 行: `s_processManager?.StopServer();` 和 `s_processManager = null;`
- 删除第 61 行中的 `Mode: {settings.Mode}` 引用（Mode 字段将被移除）

修改后的 `McpServer.cs`:
```csharp
// Shutdown() 方法:
public static void Shutdown()
{
    int port = EditorPrefs.GetInt("UnityMcp_Port", 0);
    if (port > 0)
        InstanceDiscovery.Unregister(port);

    Transport?.Stop();
    s_initialized = false;
}

// Restart() 方法:
public static void Restart()
{
    int oldPort = EditorPrefs.GetInt("UnityMcp_Port", 0);
    if (oldPort > 0) InstanceDiscovery.Unregister(oldPort);

    Transport?.Stop();
    s_initialized = false;
    Initialize();
}
```

日志改为:
```csharp
McpLogger.Info($"Server started on TCP:{port} " +
               $"({Registry.ToolCount} tools, {Registry.ResourceCount} resources, " +
               $"{Registry.PromptCount} prompts)");
```

- [ ] **Step 2: 修改 McpSettings.cs — 移除 ServerMode 和 BridgePath**

删除:
- `ServerMode` 枚举定义 (第 12 行)
- `serverMode` 字段 (第 16 行)
- `bridgePath` 字段 (第 18 行)
- `Mode` 属性 (第 27-31 行)
- `BridgePath` 属性 (第 39-43 行)

将 `PythonTransportMode` 重命名为 `TransportMode`，将 `pythonTransport` 重命名为 `transport`，`pythonHttpPort` 重命名为 `httpPort`:

```csharp
public class McpSettings : ScriptableSingleton<McpSettings>
{
    public static new McpSettings Instance => instance;
    public enum McpLogLevel { Debug, Info, Warning, Error, Off }
    public enum TransportMode { Stdio, StreamableHttp }

    [SerializeField] private int port = PortResolver.DefaultPort;
    [SerializeField] private TransportMode transport = TransportMode.Stdio;
    [SerializeField] private int httpPort = 8080;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int requestTimeoutSeconds = 60;
    [SerializeField] private McpLogLevel logLevel = McpLogLevel.Info;
    [SerializeField] private bool enableAuditLog;
    [SerializeField] private int maxBatchOperations = 50;

    public int Port { get => port > 0 ? port : PortResolver.DefaultPort; set { port = value; Save(true); } }
    public TransportMode Transport { get => transport; set { transport = value; Save(true); } }
    public int HttpPort { get => httpPort; set { httpPort = value; Save(true); } }
    public bool AutoStart { get => autoStart; set { autoStart = value; Save(true); } }
    public int RequestTimeoutMs => requestTimeoutSeconds * 1000;
    public int RequestTimeoutSeconds { get => requestTimeoutSeconds; set { requestTimeoutSeconds = value; Save(true); } }
    public McpLogLevel LogLevel { get => logLevel; set { logLevel = value; Save(true); } }
    public bool EnableAuditLog { get => enableAuditLog; set { enableAuditLog = value; Save(true); } }
    public int MaxBatchOperations { get => maxBatchOperations; set { maxBatchOperations = value; Save(true); } }
    public string Version => McpConst.ServerVersion;
}
```

- [ ] **Step 3: 删除 ServerProcessManager.cs**

```bash
rm unity-mcp/Editor/Core/ServerProcessManager.cs
rm unity-mcp/Editor/Core/ServerProcessManager.cs.meta
```

- [ ] **Step 4: 全局搜索并修复残余引用**

搜索: `ServerMode`、`BridgePath`、`s_processManager`、`ServerProcessManager`、`PythonTransport`（旧名）在 C# 文件中的引用，逐一修复。

Run: `grep -rn "ServerMode\|BridgePath\|s_processManager\|ServerProcessManager\|PythonTransport\|PythonHttpPort" unity-mcp/ --include="*.cs" | grep -v ".meta"`

- [ ] **Step 5: 确认 Unity 编译通过**

在 Unity 中打开项目，确认无编译错误。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: remove Bridge layer, simplify McpSettings"
```

---

### Task 4: 删除 Bridge 相关文件

**Files:**
- Delete: `unity-bridge/` (整个目录)
- Delete: `unity-mcp/Bridge~/` (整个目录)
- Delete: `scripts/build_bridge.sh`

- [ ] **Step 1: 删除文件**

```bash
rm -rf unity-bridge/
rm -rf unity-mcp/Bridge~/
rm scripts/build_bridge.sh
```

- [ ] **Step 2: 更新 .gitignore 如有 Bridge 相关条目**

搜索并移除 `.gitignore` 中任何 Bridge 相关忽略规则。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore: delete Bridge binary, source, and build script"
```

---

## Chunk 2: 统一 MCP 工具输出格式

### Task 5: 统一 Editor/Tools 输出格式

**Files:**
- Modify: `unity-mcp/Editor/Tools/*.cs` (26 文件)

规则回顾:
1. 读操作 → `ToolResult.Json(data)` 不含 `success`
2. 写操作 → `ToolResult.Json(关键信息)` 不含 `success`/`message`
3. 纯副作用 → `ToolResult.Text(描述)`
4. 错误/分页/图片 → 不变

- [ ] **Step 1: 处理 GameObjectTools.cs**

3 处 `success = true` (第 41、183、296 行):
- 第 41 行（创建 GO）: 移除 `success = true`, 保留 `instanceId`, `name`, `path`
- 第 183 行（查找 GO）: 移除 `success = true`, 保留数据字段
- 第 296 行（修改 GO）: 移除 `success = true`, 保留修改后的状态

- [ ] **Step 2: 处理 ComponentTools.cs**

1 处 (第 32 行): 移除 `success = true`

- [ ] **Step 3: 处理 SceneTools.cs**

4 处 (第 38, 58, 152, 178 行):
- 第 38 行（创建场景）: `ToolResult.Json(new { path, name = scene.name })` — 仅去除 `success = true`
- 第 58 行（打开场景）: 同上
- 第 152/178 行: 同上

- [ ] **Step 4: 处理 MaterialTools.cs、ScriptTools.cs、PrefabTools.cs**

MaterialTools.cs 第 47 行, ScriptTools.cs 第 65 行, PrefabTools.cs 第 42/83/316 行: 移除 `success = true`

- [ ] **Step 5: 处理 AnimationTools.cs**

8 处 (第 46, 95, 133, 165, 306, 350, 438, 486 行): 每处移除 `success = true`

- [ ] **Step 6: 处理 VfxTools.cs**

5 处 (第 41, 113, 143, 333, 452 行): 每处移除 `success = true`

- [ ] **Step 7: 处理剩余工具文件**

LightingTools.cs (第 52, 293 行), AudioTools.cs (第 54 行), NavMeshTools.cs (第 42 行), TerrainTools.cs (第 58, 220 行), PackageTools.cs (第 51 行), AssetTools.cs (第 139 行), InstanceTools.cs (第 45 行), ScriptableObjectTools.cs (第 58 行), TestTools.cs (第 37 行), CodeExecutionTools.cs (第 100 行), BatchExecuteTool.cs (第 75 行 — 注意此处 `success` 是批量结果的一部分，需保留作为每个操作的成功标志)

- [ ] **Step 8: 处理 AssetTools.cs 特殊情况**

第 139 行 `message = $"Copied '{source}' -> '{destination}'"` — 改为:
```csharp
ToolResult.Json(new { source, destination })
```

- [ ] **Step 9: 确认 Unity 编译通过**

在 Unity 中确认无编译错误。

- [ ] **Step 10: Commit**

```bash
git add unity-mcp/Editor/Tools/
git commit -m "refactor: unify Editor tool output format, remove success field"
```

---

### Task 6: 统一 Runtime/Tools 输出格式

**Files:**
- Modify: `unity-mcp/Runtime/Tools/RuntimeControlTools.cs` (第 22, 56, 67 行)
- Modify: `unity-mcp/Runtime/Tools/RuntimeInvokeTools.cs` (第 56 行)

- [ ] **Step 1: 逐一处理 4 处 success 字段**

同样规则: 移除 `success = true`，保留有意义的数据字段。

- [ ] **Step 2: 确认编译通过**

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Runtime/Tools/
git commit -m "refactor: unify Runtime tool output format, remove success field"
```

---

## Chunk 3: 设置窗口 UI Toolkit 重写

### Task 7: 客户端配置器数据层

**Files:**
- Create: `unity-mcp/Editor/Window/ClientConfig/ClientProfile.cs`
- Create: `unity-mcp/Editor/Window/ClientConfig/ClientRegistry.cs`
- Create: `unity-mcp/Editor/Window/ClientConfig/IConfigWriter.cs`
- Create: `unity-mcp/Editor/Window/ClientConfig/JsonFileConfigWriter.cs`
- Create: `unity-mcp/Editor/Window/ClientConfig/ClaudeCliConfigWriter.cs`

- [ ] **Step 1: 创建 ClientProfile.cs**

```csharp
using System;

namespace UnityMcp.Editor.Window.ClientConfig
{
    public enum ConfigStrategy { JsonFile, CliCommand }

    [Serializable]
    public class PlatformPaths
    {
        public string Windows;
        public string Mac;
        public string Linux;

        public string Current =>
#if UNITY_EDITOR_WIN
            Windows;
#elif UNITY_EDITOR_OSX
            Mac;
#else
            Linux;
#endif
    }

    [Serializable]
    public class ClientProfile
    {
        public string Id;
        public string DisplayName;
        public ConfigStrategy Strategy;
        public PlatformPaths Paths;
        public string[] InstallSteps;
        public bool IsProjectLevel; // true = .cursor/mcp.json, false = global config
    }
}
```

- [ ] **Step 2: 创建 IConfigWriter.cs**

```csharp
namespace UnityMcp.Editor.Window.ClientConfig
{
    public enum McpStatus { NotConfigured, Configured, NeedsUpdate }

    public interface IConfigWriter
    {
        McpStatus CheckStatus(ClientProfile profile, int port, string transport);
        void Configure(ClientProfile profile, int port, string transport, int httpPort);
        string GetManualSnippet(ClientProfile profile, int port, string transport, int httpPort);
    }
}
```

- [ ] **Step 3: 创建 JsonFileConfigWriter.cs**

实现读写 JSON 配置文件的通用逻辑：读取已有配置 → 合并 mcpServers.unity 节点 → 写回。

- [ ] **Step 4: 创建 ClaudeCliConfigWriter.cs**

实现调用 `claude mcp add/remove` 的逻辑。

- [ ] **Step 5: 创建 ClientRegistry.cs — 注册 12 个客户端**

```csharp
using System.Collections.Generic;
using System.IO;
using System;

namespace UnityMcp.Editor.Window.ClientConfig
{
    public static class ClientRegistry
    {
        private static readonly Dictionary<ConfigStrategy, IConfigWriter> s_writers = new()
        {
            { ConfigStrategy.JsonFile, new JsonFileConfigWriter() },
            { ConfigStrategy.CliCommand, new ClaudeCliConfigWriter() },
        };

        public static IConfigWriter GetWriter(ConfigStrategy strategy) => s_writers[strategy];

        public static readonly ClientProfile[] All = BuildProfiles();

        private static ClientProfile[] BuildProfiles()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[]
            {
                new ClientProfile
                {
                    Id = "claude-code", DisplayName = "Claude Code",
                    Strategy = ConfigStrategy.CliCommand,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".claude.json"),
                        Mac = Path.Combine(home, ".claude.json"),
                        Linux = Path.Combine(home, ".claude.json"),
                    },
                    InstallSteps = new[] { "Ensure Claude CLI is installed", "Click Configure to register via 'claude mcp add'" },
                },
                new ClientProfile
                {
                    Id = "cursor", DisplayName = "Cursor",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = ".cursor/mcp.json", Mac = ".cursor/mcp.json", Linux = ".cursor/mcp.json",
                    },
                    InstallSteps = new[] { "Click Configure to write .cursor/mcp.json" },
                },
                // ... 其余 10 个客户端同样模式
            };
        }
    }
}
```

- [ ] **Step 6: Commit**

```bash
git add unity-mcp/Editor/Window/ClientConfig/
git commit -m "feat: add client configurator data layer (ClientProfile + IConfigWriter)"
```

---

### Task 8: 主窗口 + UXML/USS 骨架

**Files:**
- Delete: `unity-mcp/Editor/Window/McpSettingsWindow.cs` (旧 IMGUI)
- Create: `unity-mcp/Editor/Window/McpSettingsWindow.cs` (新 UI Toolkit)
- Create: `unity-mcp/Editor/Window/McpSettingsWindow.uxml`
- Create: `unity-mcp/Editor/Window/McpSettingsWindow.uss`

- [ ] **Step 1: 删除旧窗口**

```bash
rm unity-mcp/Editor/Window/McpSettingsWindow.cs
# 保留 .meta 文件的 GUID 不变，或者同时删除让 Unity 重新生成
```

- [ ] **Step 2: 创建 McpSettingsWindow.uxml — 主布局**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <!-- 顶部状态栏 -->
    <ui:VisualElement name="header" class="header">
        <ui:VisualElement name="status-indicator" class="status-dot status-gray" />
        <ui:Label name="status-text" text="Stopped" class="status-label" />
        <ui:VisualElement class="spacer" />
        <ui:Label name="version-label" class="version-label" />
        <ui:Button name="docs-btn" text="Docs" class="link-btn" />
        <ui:Button name="issues-btn" text="Issues" class="link-btn" />
    </ui:VisualElement>

    <!-- 标签页工具栏 -->
    <uie:Toolbar name="tab-bar">
        <uie:ToolbarToggle name="tab-connection" text="Connection" value="true" class="tab-toggle" />
        <uie:ToolbarToggle name="tab-clients" text="Clients" class="tab-toggle" />
        <uie:ToolbarToggle name="tab-tools" text="Tools" class="tab-toggle" />
        <uie:ToolbarToggle name="tab-advanced" text="Advanced" class="tab-toggle" />
    </uie:Toolbar>

    <!-- 内容区 -->
    <ui:ScrollView name="content-area" class="content-area">
        <ui:VisualElement name="panel-connection" />
        <ui:VisualElement name="panel-clients" class="hidden" />
        <ui:VisualElement name="panel-tools" class="hidden" />
        <ui:VisualElement name="panel-advanced" class="hidden" />
    </ui:ScrollView>
</ui:UXML>
```

- [ ] **Step 3: 创建 McpSettingsWindow.uss — 基础样式**

使用 Unity CSS 变量适配 Light/Dark 主题，4px/8px 间距网格。

```css
.header {
    flex-direction: row;
    align-items: center;
    padding: 4px 8px;
    background-color: var(--unity-colors-toolbar-background);
    border-bottom-width: 1px;
    border-bottom-color: var(--unity-colors-toolbar-border);
}

.status-dot {
    width: 8px; height: 8px;
    border-radius: 4px;
    margin-right: 4px;
}

.status-green { background-color: #4CAF50; }
.status-yellow { background-color: #FF9800; }
.status-red { background-color: #F44336; }
.status-gray { background-color: #9E9E9E; }

.status-label { -unity-font-style: bold; margin-right: 8px; }
.version-label { color: var(--unity-colors-default-text-hover); }
.spacer { flex-grow: 1; }

.link-btn {
    background-color: rgba(0,0,0,0);
    border-width: 0;
    color: var(--unity-colors-link-text);
    cursor: link;
    padding: 2px 4px;
}

.tab-toggle { -unity-font-style: bold; }

.content-area {
    flex-grow: 1;
    padding: 8px;
}

.hidden { display: none; }

/* Section 通用样式 */
.section-title {
    -unity-font-style: bold;
    font-size: 14px;
    margin-bottom: 8px;
}

.field-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: 4px;
}
```

- [ ] **Step 4: 创建新 McpSettingsWindow.cs — UI Toolkit 主窗口**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityMcp.Editor.Core;
using UnityMcp.Editor.Window.Sections;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Window
{
    public class McpSettingsWindow : EditorWindow
    {
        private VisualElement[] _panels;
        private ToolbarToggle[] _tabs;
        private VisualElement _statusDot;
        private Label _statusText;

        private McpConnectionSection _connectionSection;
        private McpClientConfigSection _clientSection;
        private McpToolsSection _toolsSection;
        private McpAdvancedSection _advancedSection;

        [MenuItem("Window/Unity MCP")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<McpSettingsWindow>("Unity MCP");
            wnd.minSize = new Vector2(500, 400);
        }

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                GetUxmlPath("McpSettingsWindow"));
            tree.CloneTree(rootVisualElement);

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                GetUssPath("McpSettingsWindow"));
            rootVisualElement.styleSheets.Add(style);

            // Header
            _statusDot = rootVisualElement.Q("status-indicator");
            _statusText = rootVisualElement.Q<Label>("status-text");
            rootVisualElement.Q<Label>("version-label").text = $"v{McpConst.ServerVersion}";
            rootVisualElement.Q<Button>("docs-btn").clicked += () =>
                Application.OpenURL("https://github.com/mzbswh/unity-mcp#readme");
            rootVisualElement.Q<Button>("issues-btn").clicked += () =>
                Application.OpenURL("https://github.com/mzbswh/unity-mcp/issues");

            // Tabs
            _tabs = new[]
            {
                rootVisualElement.Q<ToolbarToggle>("tab-connection"),
                rootVisualElement.Q<ToolbarToggle>("tab-clients"),
                rootVisualElement.Q<ToolbarToggle>("tab-tools"),
                rootVisualElement.Q<ToolbarToggle>("tab-advanced"),
            };
            _panels = new[]
            {
                rootVisualElement.Q("panel-connection"),
                rootVisualElement.Q("panel-clients"),
                rootVisualElement.Q("panel-tools"),
                rootVisualElement.Q("panel-advanced"),
            };

            for (int i = 0; i < _tabs.Length; i++)
            {
                int idx = i;
                _tabs[i].RegisterValueChangedCallback(_ => SwitchTab(idx));
            }

            // Initialize sections
            _connectionSection = new McpConnectionSection(_panels[0]);
            _clientSection = new McpClientConfigSection(_panels[1]);
            _toolsSection = new McpToolsSection(_panels[2]);
            _advancedSection = new McpAdvancedSection(_panels[3]);

            // Cross-section events
            _connectionSection.OnStatusChanged += UpdateHeaderStatus;

            UpdateHeaderStatus();
            SwitchTab(0);
        }

        private void SwitchTab(int idx)
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].SetValueWithoutNotify(i == idx);
                _panels[i].style.display = i == idx ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateHeaderStatus()
        {
            bool running = McpServer.Transport?.IsRunning ?? false;
            _statusDot.RemoveFromClassList("status-green");
            _statusDot.RemoveFromClassList("status-red");
            _statusDot.RemoveFromClassList("status-gray");
            _statusDot.AddToClassList(running ? "status-green" : "status-red");
            _statusText.text = running ? "Running" : "Stopped";
        }

        private static string GetUxmlPath(string name) =>
            $"Packages/com.mzbswh.unity-mcp/Editor/Window/{name}.uxml";

        private static string GetUssPath(string name) =>
            $"Packages/com.mzbswh.unity-mcp/Editor/Window/{name}.uss";
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add unity-mcp/Editor/Window/
git commit -m "feat: scaffold UI Toolkit settings window with tabs and header"
```

---

### Task 9: Connection Section

**Files:**
- Create: `unity-mcp/Editor/Window/Sections/McpConnectionSection.cs`
- Create: `unity-mcp/Editor/Window/Sections/McpConnectionSection.uxml`

- [ ] **Step 1: 创建 UXML 布局**

包含: 启动/停止/重启按钮、端口设置、自动启动开关、传输模式选择、HTTP 端口（条件显示）。

- [ ] **Step 2: 创建 Section 控制器类**

绑定 McpSettings 属性，监听变化并调用 `McpServer.Restart()`。暴露 `event Action OnStatusChanged`。

- [ ] **Step 3: 确认 Unity 编译通过并测试标签页切换**

- [ ] **Step 4: Commit**

```bash
git add unity-mcp/Editor/Window/Sections/McpConnection*
git commit -m "feat: implement Connection section (port, transport, start/stop)"
```

---

### Task 10: Clients Section

**Files:**
- Create: `unity-mcp/Editor/Window/Sections/McpClientConfigSection.cs`
- Create: `unity-mcp/Editor/Window/Sections/McpClientConfigSection.uxml`

- [ ] **Step 1: 创建 UXML — 客户端卡片列表容器**

- [ ] **Step 2: 创建 Section 控制器**

遍历 `ClientRegistry.All`，为每个客户端生成卡片 UI：
- 名称 + 状态指示灯（绿/黄/灰）
- 配置路径预览
- 展开区: Configure 按钮 + 打开配置文件 + 复制手动配置 + 安装步骤

- [ ] **Step 3: 测试 Configure 和 CheckStatus 流程**

- [ ] **Step 4: Commit**

```bash
git add unity-mcp/Editor/Window/Sections/McpClientConfig*
git commit -m "feat: implement Clients section with 12 configurators"
```

---

### Task 11: Tools Section

**Files:**
- Create: `unity-mcp/Editor/Window/Sections/McpToolsSection.cs`
- Create: `unity-mcp/Editor/Window/Sections/McpToolsSection.uxml`

- [ ] **Step 1: 创建 UXML — 搜索栏 + ListView**

- [ ] **Step 2: 创建 Section 控制器**

使用 `ToolbarSearchField` 实现实时搜索过滤，`ListView` 虚拟化渲染工具列表。从 `McpServer.Registry` 获取工具/资源/提示词列表。

- [ ] **Step 3: 测试搜索过滤和启用/禁用 Toggle**

- [ ] **Step 4: Commit**

```bash
git add unity-mcp/Editor/Window/Sections/McpTools*
git commit -m "feat: implement Tools section with search and ListView"
```

---

### Task 12: Advanced Section + 诊断

**Files:**
- Create: `unity-mcp/Editor/Window/Sections/McpAdvancedSection.cs`
- Create: `unity-mcp/Editor/Window/Sections/McpAdvancedSection.uxml`

- [ ] **Step 1: 创建 UXML — 高级设置 + 诊断信息**

高级设置: 请求超时、日志级别、审计日志、批处理上限。
诊断区: 端口状态、最近调用、版本信息、复制诊断按钮。

- [ ] **Step 2: 创建 Section 控制器**

诊断信息通过 `McpServer.Transport` 和 `McpLogger` 获取。"复制诊断信息"按钮将所有信息格式化后放入剪贴板。

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Editor/Window/Sections/McpAdvanced*
git commit -m "feat: implement Advanced section with diagnostics"
```

---

## Chunk 4: CI/CD、LLM 文档与零散修复

### Task 13: GitHub Actions — CI

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: 创建 CI 工作流**

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  python-lint-test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: unity-server
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with:
          python-version: "3.12"
      - run: pip install -e ".[dev]" 2>/dev/null || pip install -e .
      - run: python -m pytest tests/ -v --tb=short
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add Python lint and test workflow"
```

---

### Task 14: GitHub Actions — PyPI Release

**Files:**
- Create: `.github/workflows/release-server.yml`
- Delete: `scripts/publish_pypi.sh`

- [ ] **Step 1: 创建 Release 工作流**

```yaml
name: Release Python Server

on:
  push:
    tags: ["server-v*"]

permissions:
  id-token: write

jobs:
  publish:
    runs-on: ubuntu-latest
    environment: pypi
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-python@v5
        with:
          python-version: "3.12"

      - name: Verify version matches tag
        run: |
          TAG_VERSION="${GITHUB_REF_NAME#server-v}"
          PKG_VERSION=$(python -c "import tomllib; print(tomllib.load(open('unity-server/pyproject.toml','rb'))['project']['version'])")
          if [ "$TAG_VERSION" != "$PKG_VERSION" ]; then
            echo "Tag version ($TAG_VERSION) != pyproject.toml version ($PKG_VERSION)"
            exit 1
          fi

      - name: Build
        run: |
          pip install build
          cd unity-server && python -m build

      - name: Publish to PyPI
        uses: pypa/gh-action-pypi-publish@release/v1
        with:
          packages-dir: unity-server/dist/
```

- [ ] **Step 2: 删除旧脚本**

```bash
rm scripts/publish_pypi.sh
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release-server.yml
git rm scripts/publish_pypi.sh
git commit -m "ci: add PyPI release workflow, remove manual publish script"
```

---

### Task 15: 版本管理脚本

**Files:**
- Create: `scripts/bump-version.sh`

- [ ] **Step 1: 创建 bump-version.sh**

```bash
#!/usr/bin/env bash
set -euo pipefail

usage() { echo "Usage: $0 <unity|server> <version>"; exit 1; }
[[ $# -ne 2 ]] && usage

TARGET="$1"
VERSION="$2"

if [[ "$TARGET" == "unity" ]]; then
    # Update package.json
    sed -i.bak "s/\"version\": \".*\"/\"version\": \"$VERSION\"/" unity-mcp/package.json
    rm -f unity-mcp/package.json.bak
    # Update McpConst.cs
    sed -i.bak "s/ServerVersion = \".*\"/ServerVersion = \"$VERSION\"/" unity-mcp/Shared/Models/McpConst.cs
    rm -f unity-mcp/Shared/Models/McpConst.cs.bak
    echo "Updated Unity package version to $VERSION"
elif [[ "$TARGET" == "server" ]]; then
    sed -i.bak "s/^version = \".*\"/version = \"$VERSION\"/" unity-server/pyproject.toml
    rm -f unity-server/pyproject.toml.bak
    echo "Updated Python server version to $VERSION"
else
    usage
fi
```

- [ ] **Step 2: Commit**

```bash
chmod +x scripts/bump-version.sh
git add scripts/bump-version.sh
git commit -m "chore: add bump-version.sh for Unity and Python version management"
```

---

### Task 16: CLAUDE.md 和 AGENTS.md

**Files:**
- Create: `CLAUDE.md`
- Create: `AGENTS.md`

- [ ] **Step 1: 创建 CLAUDE.md**

内容按 spec 第 6 节:
- 项目概述 + 架构图
- 数据流
- 目录结构
- 关键不变量
- 添加新工具步骤
- 常见陷阱
- 代码规约

- [ ] **Step 2: 创建 AGENTS.md**

精简版: 构建/运行、工具清单、更新指引。

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md AGENTS.md
git commit -m "docs: add CLAUDE.md and AGENTS.md for AI assistant guidance"
```

---

### Task 17: 零散修复 — 错误吞没

**Files:**
- Modify: `unity-mcp/Editor/Core/TcpTransport.cs`

- [ ] **Step 1: 修复空 catch 块**

6 处空 `catch {}`:
- 第 81 行 `try { client.Close(); } catch { }` → `catch (Exception ex) { McpLogger.Debug($"Close error: {ex.Message}"); }`
- 第 186 行 `catch (IOException) { }` → `catch (IOException ex) { McpLogger.Debug($"Client IO: {ex.Message}"); }`
- 第 195 行 `try { client.Close(); } catch { }` → 同上
- 第 243 行 `catch { return false; }` → `catch (Exception ex) { McpLogger.Debug($"Heartbeat check: {ex.Message}"); return false; }`
- 第 299 行 `try { client.Close(); } catch { }` → 同上
- 第 319 行 `catch { /* ignore parse errors */ }` → `catch (Exception ex) { McpLogger.Debug($"Notification parse: {ex.Message}"); }`

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Editor/Core/TcpTransport.cs
git commit -m "fix: replace silent catch blocks with debug logging in TcpTransport"
```

---

### Task 18: 零散修复 — Python 私有 API

**Files:**
- Modify: `unity-server/unity_mcp_server/server.py:262-283`

- [ ] **Step 1: 当前代码已有 try/except，检查降级是否充分**

当前第 262-283 行已经用 `try/except` 包裹了 `_tool_manager._tools` 访问，并在失败时 `logger.warning`。这已经满足 spec 要求。

标记为已完成，无需改动。

- [ ] **Step 2: Commit (skip if no change)**

---

### Task 19: 零散修复 — Undo 补全

**Files:**
- Modify: `unity-mcp/Editor/Tools/*.cs` (审查所有写操作)

- [ ] **Step 1: 搜索所有创建 GameObject 但未注册 Undo 的位置**

```bash
grep -n "new GameObject\|Instantiate\|CreatePrimitive" unity-mcp/Editor/Tools/*.cs | grep -v "Undo"
```

- [ ] **Step 2: 搜索所有 Destroy 但未用 Undo.DestroyObjectImmediate 的位置**

```bash
grep -n "DestroyImmediate\|Object.Destroy" unity-mcp/Editor/Tools/*.cs | grep -v "Undo"
```

- [ ] **Step 3: 逐一补全 Undo 调用**

对每处缺失，按 spec 规则添加:
- 创建: `Undo.RegisterCreatedObjectUndo(obj, "MCP: Create ...")`
- 修改前: `Undo.RecordObject(obj, "MCP: Modify ...")`
- 删除: `Undo.DestroyObjectImmediate(obj)`

- [ ] **Step 4: 确认 Unity 编译通过**

- [ ] **Step 5: Commit**

```bash
git add unity-mcp/Editor/Tools/
git commit -m "fix: add missing Undo registration for write operations"
```

---

## 执行顺序总结

```
Chunk 1 (P0): Task 1-4  → Bridge 移除 + Python 缓冲
Chunk 2 (P0): Task 5-6  → 输出格式统一
Chunk 3 (P1): Task 7-12 → 设置窗口 UI Toolkit 重写
Chunk 4 (P1+P2): Task 13-19 → CI/CD + 文档 + 修复
```

**Chunk 1 必须先完成**（McpSettings 变更会影响 Chunk 3 的设置窗口）。Chunk 2 可与 Chunk 1 并行。Chunk 3 依赖 Chunk 1。Chunk 4 中的 Task 13-16 可与 Chunk 3 并行。
