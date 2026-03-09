using System;

namespace UnityMcp.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class McpPromptAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }

        public McpPromptAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}
