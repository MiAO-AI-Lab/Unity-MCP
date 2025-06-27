#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.IvanMurzak.Unity.MCP.Common;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
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