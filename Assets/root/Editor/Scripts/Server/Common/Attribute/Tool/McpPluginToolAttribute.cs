#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;

namespace com.IvanMurzak.Unity.MCP.Common
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class McpPluginToolAttribute : Attribute
    {
        public string Name { get; set; }
        public string? Title { get; set; }

        public McpPluginToolAttribute(string name, string? title = null)
        {
            Name = name;
            Title = title;
        }
    }
}