"""Unity MCP Server - FastMCP entry point.

This Python server acts as a dynamic bridge between MCP clients (Claude, etc.)
and the Unity Editor. It discovers tools/resources/prompts from Unity via TCP
and forwards all calls dynamically.
"""
import asyncio
import json
import logging
from contextlib import asynccontextmanager
from mcp.server.fastmcp import FastMCP
from .unity_connection import UnityConnection
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


@asynccontextmanager
async def lifespan(server: FastMCP):
    """Startup/shutdown lifecycle for the MCP server."""
    global unity
    unity = UnityConnection(UNITY_HOST, UNITY_PORT)
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


mcp = FastMCP("Unity MCP", version=__version__, lifespan=lifespan)


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


async def _forward_tool(tool_name: str, **kwargs) -> str:
    """Forward a tool call to Unity and return the result as JSON string."""
    try:
        await _ensure_connected()
        # If no tools have been registered yet (Unity was offline at startup),
        # attempt re-discovery now that we have a connection
        if not _registered_tools:
            await _try_rediscover(mcp)
        # Filter out None values (optional params not provided by client)
        filtered_args = {k: v for k, v in kwargs.items() if v is not None}
        result = await unity.send_request("tools/call", {
            "name": tool_name,
            "arguments": filtered_args
        })
        if "error" in result:
            error = result["error"]
            msg = error.get("message", str(error)) if isinstance(error, dict) else str(error)
            raise UnityToolError(msg)
        # Unity returns MCP content structure: {"content":[{"type":"text","text":"..."}], "isError":...}
        # Extract the inner text to avoid double-wrapping by FastMCP
        mcp_result = result.get("result", {})
        is_error = mcp_result.get("isError", False)
        content_list = mcp_result.get("content", [])
        if content_list and isinstance(content_list, list):
            first = content_list[0]
            if isinstance(first, dict) and first.get("type") == "text":
                text = first.get("text", "")
                if is_error:
                    raise UnityToolError(text)
                return text
        return json.dumps(mcp_result, indent=2)
    except UnityToolError:
        raise  # Let FastMCP handle this as an MCP error response
    except asyncio.TimeoutError:
        logger.error(f"Tool call timeout: {tool_name}")
        raise UnityToolError(f"Tool execution timeout: {tool_name}")
    except ConnectionError as e:
        logger.error(f"Tool connection error [{tool_name}]: {e}")
        raise UnityToolError(f"Unity connection error: {e}")
    except Exception as e:
        logger.error(f"Tool forwarding error [{tool_name}]: {e}")
        raise UnityToolError(f"Tool forwarding error: {e}")


async def _forward_resource(uri: str) -> str:
    """Forward a resource read to Unity and return the result as JSON string."""
    try:
        await _ensure_connected()
        if not _registered_resources:
            await _try_rediscover(mcp)
        result = await unity.send_request("resources/read", {"uri": uri})
        if "error" in result:
            error = result["error"]
            msg = error.get("message", str(error)) if isinstance(error, dict) else str(error)
            raise UnityToolError(f"Resource error: {msg}")
        # Unity returns MCP contents structure: {"contents":[{"uri":"...","mimeType":"...","text":"..."}]}
        # Extract the inner text to avoid double-wrapping by FastMCP
        mcp_result = result.get("result", {})
        contents = mcp_result.get("contents", [])
        if contents and isinstance(contents, list):
            first = contents[0]
            if isinstance(first, dict) and "text" in first:
                return first["text"]
        return json.dumps(mcp_result, indent=2)
    except UnityToolError:
        raise
    except asyncio.TimeoutError:
        logger.error(f"Resource read timeout: {uri}")
        raise UnityToolError(f"Resource read timeout: {uri}")
    except ConnectionError as e:
        logger.error(f"Resource connection error [{uri}]: {e}")
        raise UnityToolError(f"Unity connection error: {e}")
    except Exception as e:
        logger.error(f"Resource forwarding error [{uri}]: {e}")
        raise UnityToolError(f"Resource forwarding error: {e}")


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
        for tool_def in tools:
            name = tool_def.get("name", "")
            if not name or name in _registered_tools:
                continue
            _register_dynamic_tool(server, name, tool_def)
            _registered_tools.add(name)
        logger.info(f"Registered {len(_registered_tools)} tools from Unity")
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
    params = []
    param_descriptions = {}
    for param_name, param_def in properties.items():
        py_type = type_map.get(param_def.get("type", "string"), str)
        if param_name in required_set:
            params.append(inspect.Parameter(
                param_name,
                kind=inspect.Parameter.KEYWORD_ONLY,
                annotation=py_type,
            ))
        else:
            params.append(inspect.Parameter(
                param_name,
                kind=inspect.Parameter.KEYWORD_ONLY,
                default=None,
                annotation=Optional[py_type],
            ))
        # Preserve original description from Unity's schema
        if "description" in param_def:
            param_descriptions[param_name] = param_def["description"]

    # Create forwarding function with captured tool name
    async def tool_handler(**kwargs) -> str:
        return await _forward_tool(name, **kwargs)

    tool_handler.__name__ = name
    tool_handler.__qualname__ = name
    tool_handler.__doc__ = description
    tool_handler.__signature__ = inspect.Signature(params, return_annotation=str)

    # Register with FastMCP
    server.tool(name=name, description=description)(tool_handler)

    # After registration, patch the tool's inputSchema to restore Unity's
    # original parameter descriptions, enums, and constraints that
    # inspect.Signature cannot carry.
    try:
        tool_manager = server._tool_manager
        if hasattr(tool_manager, '_tools') and name in tool_manager._tools:
            tool_obj = tool_manager._tools[name]
            if hasattr(tool_obj, 'parameters') and tool_obj.parameters:
                tool_schema = tool_obj.parameters
                schema_props = tool_schema.get("properties", {})
                for pname, pdef in properties.items():
                    if pname in schema_props:
                        # Restore description
                        if "description" in pdef:
                            schema_props[pname]["description"] = pdef["description"]
                        # Restore enum values
                        if "enum" in pdef:
                            schema_props[pname]["enum"] = pdef["enum"]
                        # Restore numeric constraints
                        for constraint in ("minimum", "maximum", "default"):
                            if constraint in pdef:
                                schema_props[pname][constraint] = pdef[constraint]
    except Exception as e:
        logger.warning(f"Could not patch schema for tool '{name}': {e}. "
                       f"Parameter descriptions may be missing.")


def _register_dynamic_resource(server: FastMCP, uri: str, res_def: dict):
    """Register a single resource as a FastMCP resource that forwards to Unity."""
    description = res_def.get("description", f"Unity resource: {uri}")
    res_name = res_def.get("name", uri)

    # Handle parameterized URI templates (e.g. "unity://gameobject/{id}")
    # FastMCP extracts template params and passes them as kwargs
    async def resource_handler(_uri_template=uri, **kwargs) -> str:
        actual_uri = _uri_template
        for k, v in kwargs.items():
            actual_uri = actual_uri.replace(f"{{{k}}}", str(v))
        return await _forward_resource(actual_uri)

    resource_handler.__name__ = res_name.replace("/", "_").replace(":", "_")
    resource_handler.__doc__ = description

    server.resource(uri)(resource_handler)


async def _forward_prompt(name: str, arguments: dict | None = None) -> str:
    """Forward a prompt get to Unity and return the result."""
    try:
        await _ensure_connected()
        result = await unity.send_request("prompts/get", {
            "name": name,
            "arguments": arguments or {}
        })
        if "error" in result:
            error = result["error"]
            msg = error.get("message", str(error)) if isinstance(error, dict) else str(error)
            raise UnityToolError(f"Prompt error: {msg}")
        # Unity returns: {"description":"...","messages":[{"role":"user","content":{"type":"text","text":"..."}}]}
        mcp_result = result.get("result", {})
        messages = mcp_result.get("messages", [])
        if messages and isinstance(messages, list):
            first = messages[0]
            content = first.get("content", {})
            if isinstance(content, dict) and content.get("type") == "text":
                return content.get("text", "")
        return json.dumps(mcp_result, indent=2)
    except UnityToolError:
        raise
    except asyncio.TimeoutError:
        logger.error(f"Prompt get timeout: {name}")
        raise UnityToolError(f"Prompt execution timeout: {name}")
    except ConnectionError as e:
        logger.error(f"Prompt connection error [{name}]: {e}")
        raise UnityToolError(f"Unity connection error: {e}")
    except Exception as e:
        logger.error(f"Prompt forwarding error [{name}]: {e}")
        raise UnityToolError(f"Prompt forwarding error: {e}")


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
