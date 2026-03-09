using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Routes incoming JSON-RPC 2.0 requests to the appropriate handler.
    /// Handles: initialize, tools/list, tools/call, resources/list, resources/read,
    ///          prompts/list, prompts/get, ping.
    /// </summary>
    public class RequestHandler
    {
        private readonly ToolRegistry _registry;
        private readonly int _timeoutMs;

        public RequestHandler(ToolRegistry registry, int timeoutMs)
        {
            _registry = registry;
            _timeoutMs = timeoutMs;
        }

        public async Task<string> HandleRequest(string json)
        {
            JObject request;
            try
            {
                request = JObject.Parse(json);
            }
            catch
            {
                return JsonRpcError(null, McpConst.ParseError, "Invalid JSON");
            }

            var id = request["id"];
            var method = request["method"]?.ToString();
            var @params = request["params"] as JObject ?? new JObject();

            try
            {
                var result = method switch
                {
                    "initialize" => HandleInitialize(request),
                    "ping" => new JObject(),
                    "tools/list" => HandleToolsList(),
                    "tools/call" => await HandleToolsCall(@params),
                    "resources/list" => HandleResourcesList(),
                    "resources/read" => await HandleResourcesRead(@params),
                    "prompts/list" => HandlePromptsList(),
                    "prompts/get" => await HandlePromptsGet(@params),
                    _ => throw new MethodNotFoundException(method)
                };

                return JsonRpcSuccess(id, result);
            }
            catch (MethodNotFoundException ex)
            {
                return JsonRpcError(id, McpConst.MethodNotFound, $"Unknown method: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                return JsonRpcError(id, McpConst.InvalidParams, ex.Message);
            }
            catch (TimeoutException ex)
            {
                return JsonRpcError(id, McpConst.InternalError, ex.Message);
            }
            catch (Exception ex)
            {
                McpLogger.Error($"Request error [{method}]: {ex}");
                return JsonRpcError(id, McpConst.InternalError, ex.Message);
            }
        }

        public Task HandleNotification(string json)
        {
            // No response needed for notifications
            try
            {
                var obj = JObject.Parse(json);
                var method = obj["method"]?.ToString() ?? "unknown";
                McpLogger.Debug($"Received notification: {method}");
            }
            catch
            {
                McpLogger.Debug("Received unparseable notification");
            }
            return Task.CompletedTask;
        }

        // --- MCP method handlers ---

        private JObject HandleInitialize(JObject request)
        {
            var clientInfo = request["params"]?["clientInfo"];
            McpLogger.Info($"MCP client connected: {clientInfo?["name"]} v{clientInfo?["version"]}");

            return new JObject
            {
                ["protocolVersion"] = McpConst.ProtocolVersion,
                ["capabilities"] = McpCapabilities.Default.ToJson(),
                ["serverInfo"] = new JObject
                {
                    ["name"] = McpConst.ServerName,
                    ["version"] = McpSettings.Instance.Version
                },
                ["instructions"] = "Unity MCP Server provides tools, resources and prompts for controlling Unity Editor. Use tools/list to discover available operations."
            };
        }

        private JObject HandleToolsList()
        {
            return new JObject { ["tools"] = _registry.GetToolList() };
        }

        private async Task<JObject> HandleToolsCall(JObject @params)
        {
            var toolName = @params["name"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
                throw new ArgumentException("Missing required parameter 'name'");

            var entry = _registry.GetTool(toolName);
            if (entry == null)
                throw new ArgumentException($"Unknown tool: '{toolName}'");

            var arguments = @params["arguments"] as JObject;
            var sw = Stopwatch.StartNew();

            var result = await MainThreadDispatcher.RunAsync(() =>
            {
                var args = ParameterBinder.Bind(entry.Method, arguments);
                var ret = entry.Method.Invoke(entry.Instance, args);
                return ret as ToolResult ?? ToolResult.Json(ret);
            }, _timeoutMs);

            sw.Stop();
            McpLogger.Audit(toolName, arguments?.ToString(Newtonsoft.Json.Formatting.None),
                sw.ElapsedMilliseconds, result.IsSuccess, result.ErrorMessage);

            return result.ToMcpResponse();
        }

        private JObject HandleResourcesList()
        {
            return new JObject { ["resources"] = _registry.GetResourceList() };
        }

        private async Task<JObject> HandleResourcesRead(JObject @params)
        {
            var uri = @params["uri"]?.ToString();
            if (string.IsNullOrEmpty(uri))
                throw new ArgumentException("Missing required parameter 'uri'");

            var entry = _registry.MatchResource(uri, out var extractedParams);
            if (entry == null)
                throw new ArgumentException($"Unknown resource: '{uri}'");

            var result = await MainThreadDispatcher.RunAsync(() =>
            {
                // Build arguments from URI template params
                var uriArgs = new JObject();
                if (extractedParams != null)
                    foreach (var kv in extractedParams)
                        uriArgs[kv.Key] = kv.Value;

                var args = ParameterBinder.Bind(entry.Method, uriArgs);
                var ret = entry.Method.Invoke(entry.Instance, args);
                return ret as ToolResult ?? ToolResult.Json(ret);
            }, _timeoutMs);

            if (!result.IsSuccess)
                throw new Exception(result.ErrorMessage ?? "Resource execution failed");

            // MCP resources/read format: {"contents":[{"uri":"...","text":"...","mimeType":"..."}]}
            return result.ToResourceResponse(uri, entry.Attribute.MimeType);
        }

        private JObject HandlePromptsList()
        {
            return new JObject { ["prompts"] = _registry.GetPromptList() };
        }

        private async Task<JObject> HandlePromptsGet(JObject @params)
        {
            var name = @params["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Missing required parameter 'name'");

            var arguments = @params["arguments"] as JObject;
            try
            {
                var result = await MainThreadDispatcher.RunAsync(() =>
                {
                    var r = _registry.GetPrompt(name, arguments);
                    if (r == null)
                        throw new ArgumentException($"Unknown prompt: '{name}'");
                    return r;
                }, _timeoutMs);
                return result;
            }
            catch (Exception ex) when (ex is not ArgumentException
                                       && ex is not TimeoutException
                                       && ex.InnerException is ArgumentException argEx)
            {
                throw argEx;
            }
        }

        // --- JSON-RPC helpers ---

        private static string JsonRpcSuccess(JToken id, JToken result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string JsonRpcError(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message
                }
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private class MethodNotFoundException : Exception
        {
            public MethodNotFoundException(string method) : base(method) { }
        }
    }
}
