"""Configuration for Unity MCP Server."""
import os

UNITY_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
UNITY_PORT = int(os.environ.get("UNITY_MCP_PORT", "51279"))
REQUEST_TIMEOUT = float(os.environ.get("UNITY_MCP_TIMEOUT", "60.0"))
