using Newtonsoft.Json.Linq;

namespace UnityMcp.Shared.Models
{
    /// <summary>
    /// MCP protocol server capabilities declaration.
    /// Used in the initialize response to inform clients what features the server supports.
    /// </summary>
    public class McpCapabilities
    {
        public bool ToolsSupported { get; set; } = true;
        public bool ToolsListChanged { get; set; } = false;
        public bool ResourcesSupported { get; set; } = true;
        public bool ResourcesListChanged { get; set; } = false;
        public bool PromptsSupported { get; set; } = true;
        public bool PromptsListChanged { get; set; } = false;

        public static McpCapabilities Default => new McpCapabilities();

        public JObject ToJson()
        {
            var caps = new JObject();

            if (ToolsSupported)
                caps["tools"] = new JObject { ["listChanged"] = ToolsListChanged };

            if (ResourcesSupported)
                caps["resources"] = new JObject { ["listChanged"] = ResourcesListChanged };

            if (PromptsSupported)
                caps["prompts"] = new JObject { ["listChanged"] = PromptsListChanged };

            return caps;
        }
    }
}
