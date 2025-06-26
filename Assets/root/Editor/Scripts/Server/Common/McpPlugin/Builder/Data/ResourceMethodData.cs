#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Reflection;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class ResourceMethodData
    {
        public Type ClassType { get; set; }
        public MethodInfo GetContentMethod { get; set; }
        public MethodInfo ListResourcesMethod { get; set; }
        public McpPluginResourceAttribute Attribute { get; set; }

        public ResourceMethodData(Type classType, MethodInfo getContentMethod, MethodInfo listResourcesMethod, McpPluginResourceAttribute attribute)
        {
            ClassType = classType;
            GetContentMethod = getContentMethod;
            ListResourcesMethod = listResourcesMethod;
            Attribute = attribute;
        }
    }
}