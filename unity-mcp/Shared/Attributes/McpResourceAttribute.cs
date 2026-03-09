using System;

namespace UnityMcp.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class McpResourceAttribute : Attribute
    {
        public string UriTemplate { get; }
        public string Name { get; }
        public string Description { get; }
        public string MimeType { get; set; } = "application/json";

        public McpResourceAttribute(string uriTemplate, string name, string description)
        {
            UriTemplate = uriTemplate;
            Name = name;
            Description = description;
        }
    }
}
