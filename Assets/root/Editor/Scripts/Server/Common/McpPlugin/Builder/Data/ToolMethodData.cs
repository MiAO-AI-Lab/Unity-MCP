#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Reflection;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class ToolMethodData
    {
        public string Name => Attribute.Name;
        public Type ClassType { get; set; }
        public MethodInfo MethodInfo { get; set; }
        public McpPluginToolAttribute Attribute { get; set; }

        public ToolMethodData(Type classType, MethodInfo methodInfo, McpPluginToolAttribute attribute)
        {
            ClassType = classType;
            MethodInfo = methodInfo;
            Attribute = attribute;
        }
    }
}