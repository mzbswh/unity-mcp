"""Tests for UnityConnection with domain-reload buffering."""
import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock
from unity_mcp_server.unity_connection import UnityConnection


@pytest.fixture
def conn():
    return UnityConnection("127.0.0.1", 51279)


@pytest.mark.asyncio
async def test_send_request_writes_frame_when_connected(conn):
    """Connected state: send_request should write a frame to the TCP stream."""
    mock_writer = MagicMock()
    mock_writer.write = MagicMock()
    mock_writer.drain = AsyncMock()

    conn._writer = mock_writer
    conn._connected = True
    conn._buffering = False
    conn._drain_task = asyncio.create_task(conn._drain_buffer())

    async def resolve_soon():
        await asyncio.sleep(0.01)
        if 1 in conn._pending:
            conn._pending[1].set_result({"jsonrpc": "2.0", "id": 1, "result": {}})

    asyncio.create_task(resolve_soon())

    result = await conn.send_request("tools/call", {"name": "test"})
    assert result["id"] == 1
    mock_writer.write.assert_called_once()
    conn._drain_task.cancel()


@pytest.mark.asyncio
async def test_send_request_buffers_when_disconnected(conn):
    """Disconnected/buffering state: frame should be queued, not sent."""
    conn._connected = False
    conn._buffering = True

    with pytest.raises(asyncio.TimeoutError):
        await asyncio.wait_for(
            conn.send_request("tools/call", {"name": "test"}),
            timeout=0.1
        )
    assert conn._buffer.qsize() == 1


@pytest.mark.asyncio
async def test_enter_buffer_mode_sets_state(conn):
    """_enter_buffer_mode should set _buffering=True, _connected=False."""
    conn._connected = True
    conn._buffering = False
    conn._reconnecting = True  # prevent auto-reconnect in test

    conn._enter_buffer_mode()

    assert conn._buffering is True
    assert conn._connected is False


@pytest.mark.asyncio
async def test_enter_buffer_mode_idempotent(conn):
    """Calling _enter_buffer_mode twice should not start two reconnect loops."""
    conn._connected = True
    conn._buffering = False
    conn._reconnecting = True

    conn._enter_buffer_mode()
    conn._enter_buffer_mode()  # second call should be no-op

    assert conn._buffering is True


@pytest.mark.asyncio
async def test_fail_pending_clears_futures(conn):
    """_fail_pending should reject all pending futures with ConnectionError."""
    loop = asyncio.get_running_loop()
    f1 = loop.create_future()
    f2 = loop.create_future()
    conn._pending = {1: f1, 2: f2}

    conn._fail_pending("test disconnect")

    assert f1.done()
    assert f2.done()
    with pytest.raises(ConnectionError):
        f1.result()
    assert len(conn._pending) == 0


@pytest.mark.asyncio
async def test_send_request_buffers_on_write_failure(conn):
    """If write fails mid-send, frame should be buffered."""
    mock_writer = MagicMock()
    mock_writer.write = MagicMock(side_effect=ConnectionError("broken pipe"))
    mock_writer.drain = AsyncMock()

    conn._writer = mock_writer
    conn._connected = True
    conn._buffering = False
    conn._reconnecting = True  # prevent auto-reconnect

    with pytest.raises(asyncio.TimeoutError):
        await asyncio.wait_for(
            conn.send_request("tools/call", {"name": "test"}),
            timeout=0.1
        )

    assert conn._buffering is True
    assert conn._buffer.qsize() == 1
