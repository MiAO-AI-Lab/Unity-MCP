#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor
{
    [InitializeOnLoad]
    public static partial class Startup
    {
        static Startup()
        {
            McpPluginUnity.RegisterJsonConverters();
            McpPluginUnity.BuildAndStart();
            BuildServerIfNeeded(force: true);
        }
    }
}