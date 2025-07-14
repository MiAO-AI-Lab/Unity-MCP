#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.MiAO.Unity.MCP.Common;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Editor
    {
        public static string EditorStats => @$"Editor Application information:
EditorApplication.isPlaying: {EditorApplication.isPlaying}
EditorApplication.isPaused: {EditorApplication.isPaused}
EditorApplication.isCompiling: {EditorApplication.isCompiling}
EditorApplication.isPlayingOrWillChangePlaymode: {EditorApplication.isPlayingOrWillChangePlaymode}
EditorApplication.isUpdating: {EditorApplication.isUpdating}
EditorApplication.applicationContentsPath : {EditorApplication.applicationContentsPath}
EditorApplication.applicationPath : {EditorApplication.applicationPath}
EditorApplication.timeSinceStartup : {EditorApplication.timeSinceStartup}";

        public static class Error
        {
            public static string ScriptPathIsEmpty()
                => "[Error] Script path is empty. Please provide a valid path. Sample: \"Assets/Scripts/MyScript.cs\".";
        }
    }
}