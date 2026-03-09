using System;

namespace UnityMcp.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public string Title { get; set; }
        public string Group { get; set; }
        public bool Idempotent { get; set; }
        public bool ReadOnly { get; set; }
        public bool AutoRegister { get; set; } = true;

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}
