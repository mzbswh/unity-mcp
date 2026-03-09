#if UNITY_MCP_RUNTIME
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Core
{
    public class RuntimeRequestHandler
    {
        private readonly RuntimeToolRegistry _registry;
        private readonly McpRuntimeBehaviour _dispatcher;

        public RuntimeRequestHandler(RuntimeToolRegistry registry, McpRuntimeBehaviour dispatcher)
        {
            _registry = registry;
            _dispatcher = dispatcher;
        }

        public async Task<string> HandleRequest(string json)
        {
            JObject request;
            try { request = JObject.Parse(json); }
            catch { return ErrorResponse(null, McpConst.ParseError, "Parse error").ToString(Newtonsoft.Json.Formatting.None); }

            var method = request["method"]?.ToString();
            var id = request["id"];

            try
            {
                var result = method switch
                {
                    "initialize" => HandleInitialize(request),
                    "ping" => new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = new JObject() },
                    "tools/list" => HandleToolsList(id),
                    "tools/call" => await HandleToolsCall(request),
                    "resources/list" => HandleResourcesList(id),
                    "resources/read" => await HandleResourcesRead(request),
                    "prompts/list" => HandlePromptsList(id),
                    "prompts/get" => await HandlePromptsGet(request),
                    _ => ErrorResponse(id, McpConst.MethodNotFound, $"Method not found: {method}")
                };
                return result.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Runtime] Error handling {method}: {ex.Message}");
                return ErrorResponse(id, McpConst.InternalError, ex.Message).ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        private JObject HandleInitialize(JObject request)
        {
            var clientInfo = request["params"]?["clientInfo"];
            Debug.Log($"[MCP Runtime] Client connected: {clientInfo?["name"]} v{clientInfo?["version"]}");

            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = request["id"],
                ["result"] = new JObject
                {
                    ["protocolVersion"] = McpConst.ProtocolVersion,
                    ["capabilities"] = new JObject
                    {
                        ["tools"] = new JObject { ["listChanged"] = false },
                        ["resources"] = new JObject { ["listChanged"] = false },
                        ["prompts"] = new JObject { ["listChanged"] = false }
                    },
                    ["serverInfo"] = new JObject
                    {
                        ["name"] = McpConst.ServerName + " Runtime",
                        ["version"] = McpConst.ServerVersion
                    },
                    ["instructions"] = "Unity MCP Runtime Server provides tools for controlling Unity Player at runtime. Use tools/list to discover available operations."
                }
            };
        }

        private JObject HandleToolsList(JToken id)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = new JObject { ["tools"] = _registry.GetToolList() }
            };
        }

        private async Task<JObject> HandleToolsCall(JObject request)
        {
            var id = request["id"];
            var toolName = request["params"]?["name"]?.ToString();
            var arguments = request["params"]?["arguments"] as JObject ?? new JObject();

            if (string.IsNullOrEmpty(toolName))
                return ErrorResponse(id, McpConst.InvalidParams, "Missing tool name");

            if (!_registry.HasTool(toolName))
                return ErrorResponse(id, McpConst.InvalidParams, $"Unknown tool: {toolName}");

            var result = await _dispatcher.RunOnMainThread(() =>
                _registry.ExecuteTool(toolName, arguments));

            var mcpResult = new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = result.IsSuccess
                            ? result.Content?.ToString() ?? ""
                            : result.ErrorMessage
                    }
                }
            };
            if (!result.IsSuccess)
                mcpResult["isError"] = true;

            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = mcpResult
            };
        }

        private JObject HandleResourcesList(JToken id)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = new JObject { ["resources"] = _registry.GetResourceList() }
            };
        }

        private async Task<JObject> HandleResourcesRead(JObject request)
        {
            var id = request["id"];
            var uri = request["params"]?["uri"]?.ToString();

            if (string.IsNullOrEmpty(uri))
                return ErrorResponse(id, McpConst.InvalidParams, "Missing resource URI");

            var result = await _dispatcher.RunOnMainThread(() =>
                _registry.ReadResource(uri));

            if (!result.IsSuccess)
                return ErrorResponse(id, McpConst.InternalError, result.ErrorMessage ?? "Resource execution failed");

            string mimeType = _registry.GetResourceMimeType(uri);
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = new JObject
                {
                    ["contents"] = new JArray
                    {
                        new JObject
                        {
                            ["uri"] = uri,
                            ["mimeType"] = mimeType,
                            ["text"] = result.Content?.ToString() ?? ""
                        }
                    }
                }
            };
        }

        private JObject HandlePromptsList(JToken id)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = new JObject { ["prompts"] = _registry.GetPromptList() }
            };
        }

        private async Task<JObject> HandlePromptsGet(JObject request)
        {
            var id = request["id"];
            var name = request["params"]?["name"]?.ToString();
            var arguments = request["params"]?["arguments"] as JObject;

            var prompt = await _dispatcher.RunOnMainThread(() =>
                _registry.GetPrompt(name, arguments));
            if (prompt == null)
                return ErrorResponse(id, McpConst.InvalidParams, $"Prompt not found: {name}");

            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = prompt
            };
        }

        private static JObject ErrorResponse(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject { ["code"] = code, ["message"] = message }
            };
        }
    }
}
#endif
