"""Configuration for Unity MCP Server."""
import json
import os

UNITY_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
UNITY_PORT = int(os.environ.get("UNITY_MCP_PORT", "51279"))
REQUEST_TIMEOUT = float(os.environ.get("UNITY_MCP_TIMEOUT", "60.0"))

# Transport mode: "stdio" (default) or "streamable-http"
TRANSPORT = os.environ.get("UNITY_MCP_TRANSPORT", "stdio")
# HTTP port (only used when TRANSPORT=streamable-http)
HTTP_PORT = int(os.environ.get("UNITY_MCP_HTTP_PORT", "8080"))

# Lanhu integration
LANHU_BASE_URL = "https://lanhuapp.com"
LANHU_HTTP_TIMEOUT = float(os.environ.get("LANHU_HTTP_TIMEOUT", "30"))

# Lanhu cookie: env var first, then local file
_LANHU_CONFIG_DIR = os.path.join(os.path.expanduser("~"), ".unity-mcp")
_LANHU_CONFIG_FILE = os.path.join(_LANHU_CONFIG_DIR, "lanhu.json")


def get_lanhu_cookie() -> str:
    """Get Lanhu cookie from env var or saved config file."""
    cookie = os.environ.get("LANHU_COOKIE", "")
    if cookie:
        return cookie
    try:
        with open(_LANHU_CONFIG_FILE, "r", encoding="utf-8") as f:
            data = json.load(f)
            return data.get("cookie", "")
    except (FileNotFoundError, json.JSONDecodeError, KeyError):
        return ""


def save_lanhu_cookie(cookie: str):
    """Save Lanhu cookie to local config file for future use."""
    os.makedirs(_LANHU_CONFIG_DIR, exist_ok=True)
    with open(_LANHU_CONFIG_FILE, "w", encoding="utf-8") as f:
        json.dump({"cookie": cookie}, f)
