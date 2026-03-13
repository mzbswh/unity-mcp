"""Unity MCP Server - FastMCP entry point.

This Python server acts as a dynamic bridge between MCP clients (Claude, etc.)
and the Unity Editor. It discovers tools/resources/prompts from Unity via TCP
and forwards all calls dynamically.
"""
import asyncio
import json
import logging
from contextlib import asynccontextmanager
from mcp.server.fastmcp import FastMCP, Context
from mcp.types import ImageContent, TextContent
from .unity_connection import UnityConnection, read_instance_status
from . import __version__
from .config import UNITY_HOST, UNITY_PORT, TRANSPORT, HTTP_PORT

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class UnityToolError(Exception):
    """Raised when a Unity tool reports isError=true. Propagates to FastMCP as an MCP error."""
    pass

# Track registered names to avoid duplicates on reconnect
_registered_tools: set[str] = set()
_registered_resources: set[str] = set()
_registered_prompts: set[str] = set()

# Global connection instance (initialized in lifespan)
unity: UnityConnection | None = None

# Track whether we've sent the MCP client identity to Unity
_client_identified: bool = False

def _reset_client_identity():
    global _client_identified
    _client_identified = False


@asynccontextmanager
async def lifespan(server: FastMCP):
    """Startup/shutdown lifecycle for the MCP server."""
    global unity
    unity = UnityConnection(UNITY_HOST, UNITY_PORT)
    unity.on_reconnect = _reset_client_identity
    try:
        await _discover_and_register(server)
    except ConnectionRefusedError:
        logger.warning(
            "Unity not available at startup. "
            "Tools will fail until Unity is running with MCP enabled."
        )
    except Exception as e:
        logger.error(f"Failed to discover tools during startup: {e}")
    yield
    # Shutdown
    try:
        if unity and unity.connected:
            await unity.disconnect()
    except Exception as e:
        logger.error(f"Error during shutdown: {e}")
    finally:
        unity = None


mcp = FastMCP("Unity MCP", lifespan=lifespan)


async def _ensure_connected():
    """Ensure connection to Unity, reconnecting if needed."""
    if unity is None:
        raise ConnectionError("Server not initialized")
    await unity.ensure_connected()


async def _try_rediscover(server: FastMCP):
    """Attempt to re-discover tools/resources/prompts from Unity if none are registered."""
    if _registered_tools and _registered_resources and _registered_prompts:
        return  # Already discovered
    try:
        await _discover_and_register(server)
        if _registered_tools:
            logger.info(f"Re-discovery successful: {len(_registered_tools)} tools registered")
    except Exception as e:
        logger.debug(f"Re-discovery attempt failed: {e}")


async def _try_send_client_identity(ctx: Context = None):
    """Send the MCP client's identity to Unity (once)."""
    global _client_identified
    if _client_identified or unity is None:
        return
    try:
        session = ctx.session if ctx else None
        if session is None:
            return
        client_params = getattr(session, 'client_params', None)
        if client_params is None:
            return
        client_info = getattr(client_params, 'clientInfo', None)
        if client_info is None:
            return
        name = getattr(client_info, 'name', None) or "Unknown"
        version = getattr(client_info, 'version', None) or ""
        await unity.send_request("initialize", {
            "clientInfo": {"name": name, "version": version}
        })
        _client_identified = True
        logger.info(f"Identified MCP client to Unity: {name} v{version}")
    except Exception as e:
        logger.debug(f"Failed to send client identity: {e}")


# Tools that can return a fallback response when Unity is unreachable
_OFFLINE_FALLBACK_TOOLS = {"editor_get_compile_status", "editor_get_state"}


def _offline_fallback(tool_name: str, status: dict | None = None) -> str | None:
    """Build a fallback response for select tools when Unity connection is unavailable.

    Args:
        tool_name: The tool being called.
        status: Instance status dict from status file (may be None).

    Returns a JSON string, or None if no fallback is available for this tool.
    """
    if tool_name not in _OFFLINE_FALLBACK_TOOLS:
        return None

    is_reloading = status.get("IsReloading", False) if status else True
    status_str = (status.get("Status", "unknown") if status else "unavailable")

    if tool_name == "editor_get_compile_status":
        return json.dumps({
            "isCompiling": is_reloading,
            "isUpdating": is_reloading,
            "isPlaying": False,
            "message": f"Unity is {status_str}. Wait before making scene changes."
                if is_reloading
                else "Ready. No compilation in progress.",
            "source": "status_file",
        })

    if tool_name == "editor_get_state":
        return json.dumps({
            "isPlaying": False,
            "isPaused": False,
            "isCompiling": is_reloading,
            "isUpdating": is_reloading,
            "status": status_str,
            "message": f"Unity is {status_str} (TCP unavailable).",
            "source": "status_file",
        })

    return None


async def _wait_for_unity_ready(max_wait: float = 30.0, poll_interval: float = 0.5) -> bool:
    """Poll the instance status file until Unity is no longer reloading.

    Returns True if Unity became ready, False if timed out.
    """
    import time
    deadline = time.monotonic() + max_wait
    while time.monotonic() < deadline:
        status = read_instance_status()
        if status is None or not status.get("IsReloading", False):
            return True
        await asyncio.sleep(poll_interval)
    return False


async def _wait_for_connection(max_wait: float = 20.0, poll_interval: float = 0.5) -> bool:
    """Wait for Unity TCP connection to be established after reload.

    The status file may flip to 'ready' before the TCP server is actually
    listening. This polls ensure_connected() until it succeeds or times out.

    Returns True if connected, False if timed out.
    """
    import time
    deadline = time.monotonic() + max_wait
    while time.monotonic() < deadline:
        try:
            await _ensure_connected()
            return True
        except (ConnectionError, OSError):
            await asyncio.sleep(poll_interval)
    return False


async def _wait_for_reload_and_reconnect(label: str) -> None:
    """Wait for Unity to finish reloading and re-establish TCP connection.

    Two-phase wait inspired by mcpprojects/unity-mcp:
    1. Poll status file until Unity is no longer reloading
    2. Poll TCP connection until Unity is accepting connections

    Args:
        label: Human-readable label for log messages (e.g. tool name, resource URI).

    Raises:
        UnityToolError: If Unity doesn't become ready within the timeout.
    """
    logger.info(f"Unity is reloading, waiting before calling {label}...")
    ready = await _wait_for_unity_ready()
    if not ready:
        raise UnityToolError(
            "Unity is still reloading after timeout. Please retry later."
        )
    # Status file says ready, but TCP may not be listening yet
    connected = await _wait_for_connection()
    if not connected:
        raise UnityToolError(
            "Unity finished reloading but TCP connection could not be established. "
            "Please retry later."
        )


async def _with_reload_retry(send_fn, label: str, fallback_fn=None):
    """Execute send_fn with automatic reload detection and retry.

    Handles three scenarios:
    1. Preflight: status file shows reloading → wait for reload + reconnect, then send
    2. Happy path: send directly
    3. Connection error: always wait for reconnection (bounded), then retry.
       Unity may have just finished reloading (status=ready) but TCP isn't up yet,
       or a domain reload may have started after the preflight check.

    Args:
        send_fn: Async callable that sends the request and returns the result.
        label: Human-readable label for log messages.
        fallback_fn: Optional callable(status) that returns a fallback response
                     when Unity is unreachable, or None to skip.
    """
    try:
        # Preflight: check if Unity is reloading via status file
        status = read_instance_status()
        if status and status.get("IsReloading", False):
            if fallback_fn:
                fallback = fallback_fn(status)
                if fallback:
                    return fallback
            await _wait_for_reload_and_reconnect(label)
            return await send_fn()

        return await send_fn()
    except UnityToolError:
        raise
    except asyncio.TimeoutError:
        if fallback_fn:
            fallback = fallback_fn(read_instance_status())
            if fallback:
                return fallback
        logger.error(f"Timeout: {label}")
        raise UnityToolError(f"Execution timeout: {label}")
    except (ConnectionError, OSError) as e:
        # Connection failed. This can happen when:
        # - Unity is mid-reload (status file may or may not reflect this yet)
        # - Unity just finished reloading but TCP server hasn't started yet
        # - Unity is genuinely not running
        #
        # Following mcpprojects/unity-mcp's approach: always attempt a bounded
        # wait for reconnection rather than failing immediately.
        # _wait_for_reload_and_reconnect handles both cases:
        # - If status file shows reloading: waits up to 30s for reload + 20s for TCP
        # - If status file shows ready: _wait_for_unity_ready returns immediately,
        #   then waits up to 20s for TCP connection
        status = read_instance_status()
        if fallback_fn:
            fallback = fallback_fn(status)
            if fallback:
                return fallback
        # If no status file at all, Unity is likely not running — fail fast.
        if status is None:
            logger.error(f"Connection error [{label}]: {e} (no Unity instance found)")
            raise UnityToolError(f"Unity connection error: {e}")
        try:
            logger.info(f"Connection error, waiting for Unity to recover for {label}...")
            await _wait_for_reload_and_reconnect(label)
            return await send_fn()
        except UnityToolError:
            raise
        except Exception as retry_err:
            logger.error(f"Retry failed [{label}]: {retry_err}")
            raise UnityToolError(f"Unity connection error after retry: {retry_err}")
    except Exception as e:
        logger.error(f"Forwarding error [{label}]: {e}")
        raise UnityToolError(f"Forwarding error: {e}")


async def _forward_tool(tool_name: str, ctx: Context = None, **kwargs):
    """Forward a tool call to Unity and return the result."""
    async def send():
        await _ensure_connected()
        await _try_send_client_identity(ctx)
        if not _registered_tools:
            await _try_rediscover(mcp)
        filtered_args = {k: v for k, v in kwargs.items() if v is not None}
        result = await unity.send_request("tools/call", {
            "name": tool_name,
            "arguments": filtered_args
        })
        if "error" in result:
            error = result["error"]
            msg = error.get("message", str(error)) if isinstance(error, dict) else str(error)
            raise UnityToolError(msg)
        mcp_result = result.get("result", {})
        is_error = mcp_result.get("isError", False)
        content_list = mcp_result.get("content", [])
        if is_error:
            for item in content_list:
                if isinstance(item, dict) and item.get("type") == "text":
                    raise UnityToolError(item.get("text", "Unknown error"))
            raise UnityToolError("Unknown error from Unity")
        blocks = []
        for item in content_list:
            if not isinstance(item, dict):
                continue
            content_type = item.get("type")
            if content_type == "image":
                blocks.append(ImageContent(
                    type="image",
                    data=item.get("data", ""),
                    mimeType=item.get("mimeType", "image/png")
                ))
            elif content_type == "text":
                blocks.append(TextContent(type="text", text=item.get("text", "")))
        if len(blocks) == 1 and isinstance(blocks[0], TextContent):
            return blocks[0].text
        if blocks:
            return blocks
        return json.dumps(mcp_result, indent=2)

    fallback_fn = None
    if tool_name in _OFFLINE_FALLBACK_TOOLS:
        fallback_fn = lambda status: _offline_fallback(tool_name, status)

    return await _with_reload_retry(send, tool_name, fallback_fn)


async def _forward_resource(uri: str) -> str:
    """Forward a resource read to Unity and return the result as JSON string."""
    async def send():
        await _ensure_connected()
        if not _registered_resources:
            await _try_rediscover(mcp)
        result = await unity.send_request("resources/read", {"uri": uri})
        if "error" in result:
            error = result["error"]
            msg = error.get("message", str(error)) if isinstance(error, dict) else str(error)
            raise UnityToolError(f"Resource error: {msg}")
        mcp_result = result.get("result", {})
        contents = mcp_result.get("contents", [])
        if contents and isinstance(contents, list):
            first = contents[0]
            if isinstance(first, dict) and "text" in first:
                return first["text"]
        return json.dumps(mcp_result, indent=2)

    return await _with_reload_retry(send, f"resource:{uri}")


async def _discover_and_register(server: FastMCP):
    """Discover tools and resources from Unity and register them with FastMCP."""
    try:
        await _ensure_connected()
    except Exception as e:
        logger.warning(f"Cannot connect to Unity for discovery: {e}")
        return

    # Discover tools
    try:
        tools_response = await unity.send_request("tools/list", {})
        tools = tools_response.get("result", {}).get("tools", [])
        skipped = []
        for tool_def in tools:
            name = tool_def.get("name", "")
            if not name or name in _registered_tools:
                if name:
                    skipped.append(name)
                continue
            try:
                _register_dynamic_tool(server, name, tool_def)
                _registered_tools.add(name)
            except Exception as e:
                logger.error(f"Failed to register tool '{name}': {e}")
        logger.warning(f"Unity returned {len(tools)} tools, registered {len(_registered_tools)}, skipped {len(skipped)}")
        if skipped:
            logger.debug(f"Skipped tools (already registered): {skipped}")
    except Exception as e:
        logger.error(f"Failed to discover tools: {e}")

    # Discover resources
    try:
        res_response = await unity.send_request("resources/list", {})
        resources = res_response.get("result", {}).get("resources", [])
        for res_def in resources:
            uri = res_def.get("uri", "")
            if not uri or uri in _registered_resources:
                continue
            _register_dynamic_resource(server, uri, res_def)
            _registered_resources.add(uri)
        logger.info(f"Registered {len(_registered_resources)} resources from Unity")
    except Exception as e:
        logger.error(f"Failed to discover resources: {e}")

    # Discover prompts
    try:
        prompts_response = await unity.send_request("prompts/list", {})
        prompts = prompts_response.get("result", {}).get("prompts", [])
        for prompt_def in prompts:
            name = prompt_def.get("name", "")
            if not name or name in _registered_prompts:
                continue
            _register_dynamic_prompt(server, name, prompt_def)
            _registered_prompts.add(name)
        logger.info(f"Registered {len(_registered_prompts)} prompts from Unity")
    except Exception as e:
        logger.error(f"Failed to discover prompts: {e}")


def _register_dynamic_tool(server: FastMCP, name: str, tool_def: dict):
    """Register a single tool as a FastMCP tool that forwards to Unity."""
    import inspect
    import keyword
    from typing import Optional

    description = tool_def.get("description", f"Unity tool: {name}")
    schema = tool_def.get("inputSchema", {})
    properties = schema.get("properties", {})
    required_set = set(schema.get("required", []))

    type_map = {
        "string": str, "integer": int, "number": float,
        "boolean": bool, "object": dict, "array": list,
    }

    # Build inspect.Parameter list so FastMCP sees a proper signature.
    # We also build a param_descriptions dict to preserve Unity's original
    # parameter descriptions, which inspect.Signature cannot carry.
    # If a parameter name is a Python keyword (e.g. "from"), we suffix it
    # with "_" for the Python signature and map it back when forwarding.
    params = []
    param_descriptions = {}
    py_to_unity_names = {}  # Maps Python param name -> Unity param name
    for param_name, param_def in properties.items():
        py_name = param_name
        if keyword.iskeyword(param_name):
            py_name = param_name + "_"
            py_to_unity_names[py_name] = param_name
        py_type = type_map.get(param_def.get("type", "string"), str)
        if param_name in required_set:
            params.append(inspect.Parameter(
                py_name,
                kind=inspect.Parameter.KEYWORD_ONLY,
                annotation=py_type,
            ))
        else:
            params.append(inspect.Parameter(
                py_name,
                kind=inspect.Parameter.KEYWORD_ONLY,
                default=None,
                annotation=Optional[py_type],
            ))
        # Preserve original description from Unity's schema
        if "description" in param_def:
            param_descriptions[param_name] = param_def["description"]

    # Create forwarding function with captured tool name
    # ctx: Context is injected by FastMCP automatically (not part of tool schema)
    # Remap any Python-safe param names back to Unity's original names
    _name_map = py_to_unity_names  # capture for closure

    async def tool_handler(ctx: Context, **kwargs):
        if _name_map:
            kwargs = {_name_map.get(k, k): v for k, v in kwargs.items()}
        return await _forward_tool(name, ctx=ctx, **kwargs)

    tool_handler.__name__ = name
    tool_handler.__qualname__ = name
    tool_handler.__doc__ = description
    tool_handler.__signature__ = inspect.Signature(params)

    # Register with FastMCP
    server.tool(name=name, description=description)(tool_handler)

    # After registration, patch the tool's inputSchema to restore Unity's
    # original parameter descriptions, enums, and constraints that
    # inspect.Signature cannot carry.
    # NOTE: We intentionally keep Python-safe names (e.g. "from_") in the
    # schema so Pydantic validation matches. The mapping back to Unity's
    # original names happens at call time in tool_handler.
    try:
        tool_manager = server._tool_manager
        if hasattr(tool_manager, '_tools') and name in tool_manager._tools:
            tool_obj = tool_manager._tools[name]
            if hasattr(tool_obj, 'parameters') and tool_obj.parameters:
                tool_schema = tool_obj.parameters
                schema_props = tool_schema.get("properties", {})

                for pname, pdef in properties.items():
                    # Use the Python-safe name for schema lookup
                    schema_key = pname
                    for py_name, unity_name in py_to_unity_names.items():
                        if unity_name == pname:
                            schema_key = py_name
                            break
                    if schema_key in schema_props:
                        # Restore description
                        if "description" in pdef:
                            schema_props[schema_key]["description"] = pdef["description"]
                        # Restore enum values
                        if "enum" in pdef:
                            schema_props[schema_key]["enum"] = pdef["enum"]
                        # Restore numeric constraints
                        for constraint in ("minimum", "maximum", "default"):
                            if constraint in pdef:
                                schema_props[schema_key][constraint] = pdef[constraint]
    except Exception as e:
        logger.warning(f"Could not patch schema for tool '{name}': {e}. "
                       f"Parameter descriptions may be missing.")


def _register_dynamic_resource(server: FastMCP, uri: str, res_def: dict):
    """Register a single resource as a FastMCP resource that forwards to Unity."""
    import re
    import inspect

    description = res_def.get("description", f"Unity resource: {uri}")
    res_name = res_def.get("name", uri)

    # Extract template parameter names from URI (e.g. "unity://gameobject/{id}" -> ["id"])
    template_params = re.findall(r"\{(\w+)\}", uri)

    if template_params:
        # FastMCP + Pydantic validate_call requires real named parameters
        # (not **kwargs with custom __signature__), so we dynamically create
        # a function with the correct parameter names via exec.
        param_list = ", ".join(f"{p}: str" for p in template_params)
        func_name = re.sub(r"[^a-zA-Z0-9_]", "_", res_name)
        lines = [f"async def {func_name}({param_list}) -> str:"]
        lines.append("    actual_uri = _uri")
        for p in template_params:
            lines.append(f'    actual_uri = actual_uri.replace("{{{p}}}", str({p}))')
        lines.append("    return await _fwd(actual_uri)")
        code = "\n".join(lines)

        local_ns: dict = {}
        exec(code, {"_uri": uri, "_fwd": _forward_resource, "str": str}, local_ns)
        resource_handler = local_ns[func_name]
    else:
        # No template params — FastMCP expects a no-arg function
        async def resource_handler() -> str:
            return await _forward_resource(uri)

    resource_handler.__name__ = re.sub(r"[^a-zA-Z0-9_]", "_", res_name)
    resource_handler.__doc__ = description

    server.resource(uri)(resource_handler)


async def _forward_prompt(name: str, arguments: dict | None = None) -> str:
    """Forward a prompt get to Unity and return the result."""
    async def send():
        await _ensure_connected()
        result = await unity.send_request("prompts/get", {
            "name": name,
            "arguments": arguments or {}
        })
        if "error" in result:
            error = result["error"]
            msg = error.get("message", str(error)) if isinstance(error, dict) else str(error)
            raise UnityToolError(f"Prompt error: {msg}")
        mcp_result = result.get("result", {})
        messages = mcp_result.get("messages", [])
        if messages and isinstance(messages, list):
            first = messages[0]
            content = first.get("content", {})
            if isinstance(content, dict) and content.get("type") == "text":
                return content.get("text", "")
        return json.dumps(mcp_result, indent=2)

    return await _with_reload_retry(send, f"prompt:{name}")


def _register_dynamic_prompt(server: FastMCP, name: str, prompt_def: dict):
    """Register a single prompt as a FastMCP prompt that forwards to Unity."""
    import inspect
    from typing import Optional

    description = prompt_def.get("description", f"Unity prompt: {name}")
    arguments = prompt_def.get("arguments", [])

    # Build inspect.Parameter list from prompt arguments
    params = []
    for arg_def in arguments:
        arg_name = arg_def.get("name", "")
        if not arg_name:
            continue
        required = arg_def.get("required", False)
        if required:
            params.append(inspect.Parameter(
                arg_name,
                kind=inspect.Parameter.KEYWORD_ONLY,
                annotation=str,
            ))
        else:
            params.append(inspect.Parameter(
                arg_name,
                kind=inspect.Parameter.KEYWORD_ONLY,
                default=None,
                annotation=Optional[str],
            ))

    async def prompt_handler(**kwargs) -> str:
        filtered = {k: v for k, v in kwargs.items() if v is not None}
        return await _forward_prompt(name, filtered)

    prompt_handler.__name__ = name
    prompt_handler.__qualname__ = name
    prompt_handler.__doc__ = description
    prompt_handler.__signature__ = inspect.Signature(params, return_annotation=str)

    server.prompt(name=name, description=description)(prompt_handler)


# --- Python-side tools (no Unity connection needed) ---

@mcp.tool(name="analyze_script", description="Analyze a C# script for common issues and patterns (runs locally, no Unity needed)")
def analyze_script(file_path: str) -> str:
    from .tools.script_analyzer import analyze_script as _analyze
    return json.dumps(_analyze(file_path), indent=2)


@mcp.tool(name="validate_assets", description="Validate asset naming conventions and folder structure (runs locally, no Unity needed)")
def validate_assets(project_path: str) -> str:
    from .tools.asset_validator import validate_assets as _validate
    return json.dumps(_validate(project_path), indent=2)


@mcp.tool(
    name="psd_summary",
    description="Get a quick overview of a PSD/PSB file without exporting anything. "
    "Returns canvas dimensions, layer counts by type (group/text/image/fillcolor), "
    "and a compact layer tree with names and types. Text layer content is included (up to 50 chars). "
    "Use this to understand PSD structure before deciding how to process it. "
    "Runs locally using psd-tools (no Unity needed).",
)
def psd_summary(
    psd_path: str,
) -> str:
    from .tools.psd_parser import get_psd_summary

    return json.dumps(get_psd_summary(psd_path), indent=2, ensure_ascii=False)


@mcp.tool(
    name="psd_layer_detail",
    description="Get detailed info about a specific layer in a PSD/PSB file by path. "
    "Path format: 'groupName/layerName' (e.g. '组 110/活动'). "
    "Returns full properties: position, size, opacity, blend mode, type, "
    "text content/font/color (for text layers), fill color (for fill layers), "
    "child list (for groups). Runs locally (no Unity needed).",
)
def psd_layer_detail(
    psd_path: str,
    layer_path: str,
) -> str:
    from .tools.psd_parser import get_psd_layer_detail

    return json.dumps(get_psd_layer_detail(psd_path, layer_path), indent=2, ensure_ascii=False)


@mcp.tool(
    name="psd_parse",
    description="Parse a PSD/PSB file and return the full layer tree structure. "
    "Optionally export layer images as PNG (set export_images=false to skip). "
    "Runs locally using psd-tools (no Unity needed). The returned layer tree can be passed to "
    "psd_create_ui to generate Unity UI. Each layer includes: name, type (image/text/group/fillcolor), "
    "position, size, uiType, textProperties (for text), fillColor (for fillcolor), "
    "imagePath (for images, only when export_images=true), children (for groups). "
    "Smart object layers wrapping text are automatically detected as text layers.",
)
def psd_parse(
    psd_path: str,
    image_output_dir: str = None,
    unity_project_path: str = None,
    export_images: bool = True,
) -> str:
    from .tools.psd_parser import parse_psd

    return json.dumps(
        parse_psd(psd_path, image_output_dir, unity_project_path, export_images),
        indent=2, ensure_ascii=False,
    )


@mcp.tool(
    name="psd_export_images",
    description="Export all visible image layers from a PSD/PSB file as PNG files. "
    "Returns only the list of exported images (file name, asset path, original layer name) "
    "without the full layer tree. Duplicate images are deduplicated by content hash. "
    "Use this when you only need the images, not the full PSD structure. "
    "Runs locally using psd-tools (no Unity needed).",
)
def psd_export_images(
    psd_path: str,
    image_output_dir: str,
    unity_project_path: str = None,
) -> str:
    from .tools.psd_parser import parse_psd

    result = parse_psd(psd_path, image_output_dir, unity_project_path, export_images=True)
    if "error" in result:
        return json.dumps(result, indent=2, ensure_ascii=False)
    return json.dumps({
        "psdName": result.get("psdName"),
        "canvasWidth": result.get("canvasWidth"),
        "canvasHeight": result.get("canvasHeight"),
        "exportedImages": result.get("exportedImages", []),
        "totalExported": len(result.get("exportedImages", [])),
    }, indent=2, ensure_ascii=False)


@mcp.tool(
    name="psd_to_image",
    description="Flatten and export a PSD/PSB file as a single PNG or JPG image. "
    "Composites all visible layers into one image. Optionally resize with max_resolution. "
    "Runs locally using psd-tools (no Unity needed).",
)
def psd_to_image(
    psd_path: str,
    output_path: str,
    max_resolution: int = 0,
    format: str = "png",
) -> str:
    """Flatten a PSD/PSB to a single image file.

    Args:
        psd_path: Absolute path to the PSD/PSB file.
        output_path: Absolute path for the output image file.
        max_resolution: If > 0, resize so the longest side does not exceed this value.
        format: Output format, "png" or "jpg" (default "png").
    """
    try:
        from psd_tools import PSDImage
    except ImportError:
        return json.dumps({"error": "psd-tools is not installed. Run: pip install psd-tools"})

    psd_path = os.path.realpath(psd_path)
    if not os.path.isfile(psd_path):
        return json.dumps({"error": f"File not found: {psd_path}"})

    ext = os.path.splitext(psd_path)[1].lower()
    if ext not in (".psd", ".psb"):
        return json.dumps({"error": f"Not a valid PSD/PSB file: {psd_path}"})

    psd = PSDImage.open(psd_path)
    img = psd.composite()
    if img is None:
        return json.dumps({"error": "Failed to composite PSD layers"})

    # Resize if needed
    if max_resolution > 0:
        w, h = img.size
        longest = max(w, h)
        if longest > max_resolution:
            scale = max_resolution / longest
            new_w = int(w * scale)
            new_h = int(h * scale)
            img = img.resize((new_w, new_h), resample=3)  # LANCZOS

    # Determine format
    fmt = format.lower()
    if fmt in ("jpg", "jpeg"):
        save_fmt = "JPEG"
        if img.mode == "RGBA":
            img = img.convert("RGB")
    else:
        save_fmt = "PNG"

    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    img.save(output_path, format=save_fmt)

    return json.dumps({
        "outputPath": os.path.abspath(output_path),
        "width": img.size[0],
        "height": img.size[1],
        "format": save_fmt.lower(),
        "originalWidth": psd.width,
        "originalHeight": psd.height,
    }, indent=2, ensure_ascii=False)


@mcp.tool(
    name="psd_to_ui",
    description="One-step PSD to Unity UI conversion. Parses the PSD file (Python-side), "
    "exports layer images, then calls Unity to create the UI hierarchy and save as a prefab. "
    "Supports custom component mapping via component_map to use project-specific UI components. "
    "Returns the list of created objects and exported images with hierarchy paths for AI renaming. "
    "After calling this tool, use asset_move to rename exported images and gameobject_modify to rename objects.",
)
async def psd_to_ui(
    psd_path: str,
    image_output_dir: str,
    prefab_path: str,
    unity_project_path: str = None,
    component_map: dict = None,
) -> str:
    """One-step PSD to Unity UI conversion.

    Args:
        psd_path: Absolute path to the PSD/PSB file.
        image_output_dir: Unity Assets-relative path for exported images.
        prefab_path: Prefab save path (e.g. Assets/Prefab/MyUI.prefab).
        unity_project_path: Unity project root. If None, image_output_dir is absolute.
        component_map: Component mapping table. Maps uiType names to full C# type names.
            If the type exists in the project, it will be used; otherwise falls back to defaults.
            Example: {"Text": "MyGame.UI.CustomText, Assembly-CSharp",
                      "Image": "MyGame.UI.CustomImage, Assembly-CSharp"}
            Supported keys: Text, TMPText, Image, RawImage, FillColor, Button, TMPButton.
    """
    from .tools.psd_parser import parse_psd

    # Step 1: Parse PSD and export images (Python-side)
    parsed = parse_psd(psd_path, image_output_dir, unity_project_path)
    if "error" in parsed:
        return json.dumps(parsed, indent=2, ensure_ascii=False)

    # Step 2: Call Unity to create UI from parsed data
    arguments = {
        "layers": parsed["layers"],
        "prefabPath": prefab_path,
        "imageDir": image_output_dir,
        "canvasWidth": parsed["canvasWidth"],
        "canvasHeight": parsed["canvasHeight"],
    }
    if component_map:
        arguments["componentMap"] = component_map

    await _ensure_connected()
    result = await unity.send_request("tools/call", {
        "name": "psd_create_ui",
        "arguments": arguments,
    })

    if "error" in result:
        error = result["error"]
        msg = error.get("message", str(error)) if isinstance(error, dict) else str(error)
        return json.dumps({"error": msg, "parsedLayers": parsed}, indent=2, ensure_ascii=False)

    mcp_result = result.get("result", {})
    is_error = mcp_result.get("isError", False)
    if is_error:
        for item in mcp_result.get("content", []):
            if isinstance(item, dict) and item.get("type") == "text":
                return json.dumps(
                    {"error": item.get("text"), "parsedLayers": parsed},
                    indent=2, ensure_ascii=False,
                )

    # Extract text content from Unity response
    for item in mcp_result.get("content", []):
        if isinstance(item, dict) and item.get("type") == "text":
            return item.get("text", "{}")

    return json.dumps(mcp_result, indent=2, ensure_ascii=False)


# --- Lanhu design platform tools ---

_LANHU_NO_COOKIE_MSG = json.dumps({
    "error": "Lanhu cookie not configured",
    "action": "Please call lanhu_set_cookie with your Lanhu session cookie first. "
    "You can get the cookie from browser DevTools (F12 → Network → any lanhuapp.com request → Cookie header).",
}, ensure_ascii=False)


@mcp.tool(
    name="lanhu_set_cookie",
    description="Set and save the Lanhu session cookie for authentication. "
    "Cookie is saved to ~/.unity-mcp/lanhu.json for future use. "
    "Optionally provide a test_url (Lanhu project URL) to verify the cookie works. "
    "Get the cookie from browser DevTools: F12 → Network → any lanhuapp.com request → Cookie header.",
)
async def lanhu_set_cookie(cookie: str, test_url: str = None) -> str:
    from .tools.lanhu import LanhuClient
    from .config import save_lanhu_cookie

    client = LanhuClient(cookie=cookie)
    try:
        result = await client.verify_cookie(test_url=test_url)
        if result["valid"]:
            save_lanhu_cookie(cookie)
        return json.dumps(result, indent=2, ensure_ascii=False)
    finally:
        await client.close()


@mcp.tool(
    name="lanhu_get_designs",
    description="Get the list of UI design images from a Lanhu project. "
    "Provide a Lanhu project URL (with tid and pid params). "
    "Returns design names, IDs, dimensions, and preview URLs. "
    "Call this first before using lanhu_analyze_design or lanhu_get_slices. "
    "Requires lanhu_set_cookie to be called first if cookie is not configured.",
)
async def lanhu_get_designs(url: str) -> str:
    from .tools.lanhu import LanhuClient, NoCookieError

    try:
        client = LanhuClient()
    except NoCookieError:
        return _LANHU_NO_COOKIE_MSG
    try:
        result = await client.get_designs(url)
        return json.dumps(result, indent=2, ensure_ascii=False)
    finally:
        await client.close()


@mcp.tool(
    name="lanhu_analyze_design",
    description="Download Lanhu design images for AI visual analysis. "
    "Provide a Lanhu project URL and design_names ('all', a name, an index number, "
    "or a list of names/indexes from lanhu_get_designs). "
    "Images are saved to output_dir as PNG files. "
    "Returns file paths for each downloaded design image. "
    "Requires lanhu_set_cookie to be called first if cookie is not configured.",
)
async def lanhu_analyze_design(
    url: str,
    design_names: str,
    output_dir: str,
) -> str:
    from .tools.lanhu import LanhuClient, NoCookieError

    try:
        client = LanhuClient()
    except NoCookieError:
        return _LANHU_NO_COOKIE_MSG

    names = design_names
    if "," in design_names:
        names = [n.strip() for n in design_names.split(",")]

    try:
        result = await client.download_design_images(url, names, output_dir)
        return json.dumps(result, indent=2, ensure_ascii=False)
    finally:
        await client.close()


@mcp.tool(
    name="lanhu_get_slices",
    description="Get slice/asset info (download URLs, text layers) from a Lanhu design. "
    "One-step: internally fetches design list, finds the target by name, then extracts slices. "
    "No need to call lanhu_get_designs first. "
    "Returns slice download URLs, text content, positions, fonts, and optional metadata. "
    "scale controls image resolution: 1=@1x, 2=@2x (default, iOS standard), 3=@3x, 4=@4x (original max). "
    "Requires lanhu_set_cookie to be called first if cookie is not configured.",
)
async def lanhu_get_slices(
    url: str,
    design_name: str,
    include_metadata: bool = True,
    scale: int = 2,
) -> str:
    from .tools.lanhu import LanhuClient, NoCookieError

    try:
        client = LanhuClient()
    except NoCookieError:
        return _LANHU_NO_COOKIE_MSG

    try:
        result = await client.get_design_slices(url, design_name, include_metadata, scale=scale)
        return json.dumps(result, indent=2, ensure_ascii=False)
    finally:
        await client.close()


@mcp.tool(
    name="lanhu_download_slices",
    description="Batch download all slices/assets from a Lanhu design to a local directory. "
    "One-step: fetches design list, finds the target, extracts slice URLs, downloads all images. "
    "No need to call lanhu_get_designs or lanhu_get_slices first. "
    "Files are named based on layer_path by default (e.g. TopBar_Icon.png). "
    "scale controls image resolution: 1=@1x, 2=@2x (default, iOS standard), 3=@3x, 4=@4x (original max). "
    "Requires lanhu_set_cookie to be called first if cookie is not configured.",
)
async def lanhu_download_slices(
    url: str,
    design_name: str,
    output_dir: str,
    name_pattern: str = "layer_path",
    scale: int = 2,
) -> str:
    from .tools.lanhu import LanhuClient, NoCookieError

    try:
        client = LanhuClient()
    except NoCookieError:
        return _LANHU_NO_COOKIE_MSG

    try:
        result = await client.download_slices(url, design_name, output_dir, name_pattern, scale=scale)
        return json.dumps(result, indent=2, ensure_ascii=False)
    finally:
        await client.close()


@mcp.resource("unity://server/status")
def server_status() -> str:
    """Python MCP server status and connection info."""
    status = {
        "serverName": "Unity MCP Python Bridge",
        "version": __version__,
        "connected": unity is not None and unity.connected if unity else False,
        "registeredTools": len(_registered_tools),
        "registeredResources": len(_registered_resources),
        "registeredPrompts": len(_registered_prompts),
        "transport": TRANSPORT,
        "unityHost": UNITY_HOST,
        "unityPort": UNITY_PORT,
    }
    return json.dumps(status, indent=2)


def main():
    """Entry point for the Unity MCP Python server."""
    if TRANSPORT == "streamable-http":
        mcp.run(transport="streamable-http", host="0.0.0.0", port=HTTP_PORT)
    else:
        mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
