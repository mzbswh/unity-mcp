"""Configuration for Unity MCP Server.

The UNITY_MCP_PORT environment variable is set automatically by the Unity Editor
when launching this server (see ServerProcessManager.CreatePythonStartInfo).
If not set, falls back to 0 which will cause an explicit connection error rather
than silently connecting to the wrong port.
"""
import os
import logging

UNITY_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")

_port_str = os.environ.get("UNITY_MCP_PORT", "")
if _port_str:
    UNITY_PORT = int(_port_str)
else:
    logging.warning(
        "UNITY_MCP_PORT not set. The Unity Editor should set this automatically. "
        "Set it manually or check your MCP client configuration (env field)."
    )
    UNITY_PORT = 0

REQUEST_TIMEOUT = float(os.environ.get("UNITY_MCP_TIMEOUT", "60.0"))
