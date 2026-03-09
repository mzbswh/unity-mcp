FROM python:3.12-slim

WORKDIR /app

# Install dependencies first (cache layer)
COPY pyproject.toml .
RUN pip install --no-cache-dir .

# Copy source code
COPY unity_mcp_server/ unity_mcp_server/

# Default environment
ENV UNITY_MCP_HOST=host.docker.internal
ENV UNITY_MCP_PORT=52345
ENV UNITY_MCP_TIMEOUT=60

ENTRYPOINT ["unity-mcp-server"]
