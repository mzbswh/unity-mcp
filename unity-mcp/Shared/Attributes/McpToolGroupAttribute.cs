using System;

namespace UnityMcp.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class McpToolGroupAttribute : Attribute
    {
        public string Name { get; }

        public McpToolGroupAttribute(string name)
        {
            Name = name;
        }
    }
}
