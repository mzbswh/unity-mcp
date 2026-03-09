using Newtonsoft.Json.Linq;

namespace UnityMcp.Shared.Interfaces
{
    public interface IToolRegistry
    {
        int ToolCount { get; }
        int ResourceCount { get; }
        int PromptCount { get; }
        void ScanAll();
        JArray GetToolList();
        JArray GetResourceList();
        JArray GetPromptList();
        JObject GetPrompt(string name, JObject arguments);
        bool HasTool(string name);
        bool HasResource(string uri);
    }
}
