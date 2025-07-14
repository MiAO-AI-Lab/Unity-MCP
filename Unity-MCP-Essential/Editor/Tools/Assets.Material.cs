#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.MiAO.Unity.MCP.Common;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Assets_Material
    {
        public static class Error
        {
            static string MaterialsPrinted => string.Join("\n", AssetDatabase.FindAssets("t:Material"));
        }
    }
}