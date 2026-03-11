"""Unity TCP connection manager with domain-reload buffering.

When the TCP connection drops (e.g. during Unity domain reload), incoming
send_request() calls are queued. Once reconnected, the queue is drained
and all buffered requests are replayed in order, making script recompilation
nearly transparent to MCP clients.
"""
import asyncio
import struct
import json
import logging
from .config import UNITY_HOST, UNITY_PORT, REQUEST_TIMEOUT

logger = logging.getLogger(__name__)

MSG_TYPE_REQUEST = 0x01
MSG_TYPE_RESPONSE = 0x02
MSG_TYPE_NOTIFICATION = 0x03

# Backoff schedule for reconnection (milliseconds).
# Aggressive early retries for domain reload (typically 2-5s),
# then back off for longer outages.
_RECONNECT_DELAYS_MS = [0, 300, 500, 1000, 1000, 2000, 2000, 3000, 3000, 5000]


class UnityConnection:
    """Manages TCP connection to Unity Editor with domain-reload buffering."""

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
        self.on_reconnect: callable | None = None

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
            self._start_drain()
            logger.info(f"Connected to Unity at {self.host}:{self.port}")
        except ConnectionRefusedError:
            logger.error(
                f"Cannot connect to Unity at {self.host}:{self.port}. "
                "Is Unity running with MCP enabled?"
            )
            raise

    def _start_drain(self):
        """Start the buffer drain task if not already running."""
        if self._drain_task is None or self._drain_task.done():
            self._drain_task = asyncio.create_task(self._drain_buffer())

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
        """Send a JSON-RPC request and wait for response.

        If the connection is down (buffering mode), the frame is queued
        and will be sent once reconnected. The returned future will still
        timeout per REQUEST_TIMEOUT if the response never arrives.
        """
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
        """Continuously drain buffered frames when connected."""
        try:
            while True:
                frame = await self._buffer.get()
                while not self._connected or self._buffering:
                    await asyncio.sleep(0.05)
                try:
                    self._writer.write(frame)
                    await self._writer.drain()
                except (ConnectionError, OSError):
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
            if self._connected:
                self._connected = False
                self._enter_buffer_mode()

    def _fail_pending(self, reason: str):
        """Fail all pending requests when giving up on reconnection."""
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
                    self._start_drain()
                    pending_count = self._buffer.qsize()
                    logger.info(
                        f"Reconnected to Unity, replaying {pending_count} buffered request(s)"
                    )
                    if self.on_reconnect:
                        try:
                            self.on_reconnect()
                        except Exception:
                            pass
                    return
                except (ConnectionRefusedError, OSError) as e:
                    logger.warning(f"Reconnect attempt {attempt + 1} failed: {e}")

            self._fail_pending(
                f"Cannot reconnect to Unity at {self.host}:{self.port}"
            )
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
