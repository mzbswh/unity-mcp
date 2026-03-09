"""Unity TCP connection manager using the custom frame protocol."""
import asyncio
import struct
import json
import logging
from .config import UNITY_HOST, UNITY_PORT, REQUEST_TIMEOUT

logger = logging.getLogger(__name__)

# Frame protocol constants (must match C# TcpTransport)
MSG_TYPE_REQUEST = 0x01
MSG_TYPE_RESPONSE = 0x02
MSG_TYPE_NOTIFICATION = 0x03


class UnityConnection:
    """Manages TCP connection to Unity Editor's MCP server."""

    def __init__(self, host: str = None, port: int = None):
        self.host = host or UNITY_HOST
        self.port = port or UNITY_PORT
        self._reader: asyncio.StreamReader | None = None
        self._writer: asyncio.StreamWriter | None = None
        self._request_id = 0
        self._pending: dict[int, asyncio.Future] = {}
        self._lock = asyncio.Lock()
        self._connected = False

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
            asyncio.create_task(self._read_loop())
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
        if self._writer:
            self._writer.close()
            await self._writer.wait_closed()
            self._writer = None
            self._reader = None

    async def send_request(self, method: str, params: dict = None) -> dict:
        """Send a JSON-RPC request and wait for response."""
        if not self._connected:
            raise ConnectionError("Not connected to Unity")

        async with self._lock:
            self._request_id += 1
            req_id = self._request_id

        msg = json.dumps({
            "jsonrpc": "2.0",
            "id": req_id,
            "method": method,
            "params": params or {}
        }).encode("utf-8")

        # Frame: 4-byte length (BE) + 1-byte type (0x01=Request) + JSON payload
        frame_len = 1 + len(msg)
        self._writer.write(struct.pack(">IB", frame_len, MSG_TYPE_REQUEST) + msg)
        await self._writer.drain()

        future = asyncio.get_running_loop().create_future()
        self._pending[req_id] = future
        return await asyncio.wait_for(future, timeout=REQUEST_TIMEOUT)

    async def _read_loop(self):
        """Read frames from Unity and resolve pending futures."""
        try:
            while self._connected:
                # Read 4-byte length + 1-byte type
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

                req_id = msg.get("id")
                if req_id and req_id in self._pending:
                    self._pending.pop(req_id).set_result(msg)
                else:
                    logger.debug(f"Received unmatched message: {msg}")
        except asyncio.IncompleteReadError:
            logger.info("Unity connection closed")
        except Exception as e:
            logger.error(f"Read loop error: {e}")
        finally:
            self._connected = False
            self._fail_pending("Connection closed")

    def _fail_pending(self, reason: str):
        """Fail all pending requests when connection is lost."""
        for future in self._pending.values():
            if not future.done():
                future.set_exception(ConnectionError(reason))
        self._pending.clear()

    async def ensure_connected(self):
        """Reconnect to Unity if disconnected, with exponential backoff."""
        if self._connected:
            return

        delays = [0, 500, 1000, 2000, 3000, 5000, 8000, 10000]
        for attempt, delay in enumerate(delays):
            if delay > 0:
                logger.info(f"Reconnecting to Unity in {delay}ms (attempt {attempt + 1})")
                await asyncio.sleep(delay / 1000)
            try:
                await self.connect()
                return
            except (ConnectionRefusedError, OSError) as e:
                logger.warning(f"Reconnect attempt {attempt + 1} failed: {e}")

        raise ConnectionError(
            f"Cannot reconnect to Unity at {self.host}:{self.port} after {len(delays)} attempts"
        )
