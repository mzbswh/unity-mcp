using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Shared.Models
{
    public class ToolResult
    {
        private static readonly JsonSerializer s_serializer = CreateSerializer();

        private static JsonSerializer CreateSerializer()
        {
            var s = JsonSerializer.CreateDefault();
            UnityTypeConverters.EnsureRegistered(s);
            return s;
        }

        public bool IsSuccess { get; private set; }
        public JToken Content { get; private set; }
        public string ErrorMessage { get; private set; }
        public string ErrorCode { get; private set; }

        public static ToolResult Text(string message)
        {
            return new ToolResult
            {
                IsSuccess = true,
                Content = new JValue(message)
            };
        }

        public static ToolResult Json(object data)
        {
            return new ToolResult
            {
                IsSuccess = true,
                Content = data == null ? JValue.CreateNull() : JToken.FromObject(data, s_serializer)
            };
        }

        public static ToolResult Error(string message, string code = "tool_error")
        {
            return new ToolResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                ErrorCode = code
            };
        }

        public static ToolResult Paginated(object items, int total, string nextCursor = null)
        {
            var obj = new JObject
            {
                ["items"] = JToken.FromObject(items, s_serializer),
                ["total"] = total,
            };
            if (nextCursor != null) obj["nextCursor"] = nextCursor;
            return new ToolResult { IsSuccess = true, Content = obj };
        }

        public JObject ToMcpResponse()
        {
            if (IsSuccess)
            {
                string text = Content is JValue jv ? jv.ToString() : Content.ToString();
                return new JObject
                {
                    ["content"] = new JArray
                    {
                        new JObject { ["type"] = "text", ["text"] = text }
                    }
                };
            }

            return new JObject
            {
                ["isError"] = true,
                ["content"] = new JArray
                {
                    new JObject { ["type"] = "text", ["text"] = ErrorMessage }
                }
            };
        }

        /// <summary>
        /// MCP resources/read response format: {"contents":[{"uri":"...","mimeType":"...","text":"..."}]}
        /// </summary>
        public JObject ToResourceResponse(string uri, string mimeType = "application/json")
        {
            string text = Content is JValue jv ? jv.ToString() : Content?.ToString() ?? "";

            return new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = mimeType,
                        ["text"] = text
                    }
                }
            };
        }

        /// <summary>
        /// MCP prompts/get response format: {"description":"...","messages":[{"role":"user","content":{"type":"text","text":"..."}}]}
        /// </summary>
        public JObject ToPromptResponse(string description)
        {
            string text;
            if (!IsSuccess)
                text = ErrorMessage;
            else
                text = Content is JValue jv ? jv.ToString() : Content.ToString();

            return new JObject
            {
                ["description"] = description,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JObject
                        {
                            ["type"] = "text",
                            ["text"] = text
                        }
                    }
                }
            };
        }
    }
}
