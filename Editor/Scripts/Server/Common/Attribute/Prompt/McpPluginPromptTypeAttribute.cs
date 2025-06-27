#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;

namespace com.IvanMurzak.Unity.MCP.Common
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class McpPluginPromptTypeAttribute : Attribute
    {
        public string? Path { get; set; }

        public McpPluginPromptTypeAttribute() { }
    }
}