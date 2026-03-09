using System;

namespace UnityMcp.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class DescAttribute : Attribute
    {
        public string Text { get; }

        public DescAttribute(string text)
        {
            Text = text;
        }
    }
}
