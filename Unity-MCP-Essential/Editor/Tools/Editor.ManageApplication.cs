#pragma warning disable CS8632
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Editor
    {
        [McpPluginTool
        (
            "Editor_ManageApplication",
            Title = "Manage Unity Editor Application"
        )]
        [Description(@"Manage Unity Editor application information and state control operations:
- getInformation: Retrieve current Unity Editor application information and state
- setState: Control Unity Editor application state (playmode, pause)")]
        public string Application
        (
            [Description("Operation type: 'getInformation', 'setState'")]
            string operation,
            [Description("For setState: If true, the 'playmode' will be started. If false, the 'playmode' will be stopped.")]
            bool? isPlaying = null,
            [Description("For setState: If true, the 'playmode' will be paused. If false, the 'playmode' will be resumed.")]
            bool? isPaused = null
        )
        {
            return operation.ToLower() switch
            {
                "getinformation" => GetApplicationInformation(),
                "setstate" => SetApplicationState(isPlaying, isPaused),
                _ => "[Error] Invalid operation. Valid operations: 'getInformation', 'setState'"
            };
        }

        private string GetApplicationInformation()
        {
            return MainThread.Instance.Run(() => "[Success] " + EditorStats);
        }

        private string SetApplicationState(bool? isPlaying, bool? isPaused)
        {
            return MainThread.Instance.Run(() =>
            {
                if (isPlaying.HasValue)
                    EditorApplication.isPlaying = isPlaying.Value;
                if (isPaused.HasValue)
                    EditorApplication.isPaused = isPaused.Value;
                return $"[Success] {EditorStats}";
            });
        }
    }
} 