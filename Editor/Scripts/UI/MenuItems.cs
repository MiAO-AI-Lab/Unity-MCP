#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using System.Threading.Tasks;
using com.MiAO.Unity.MCP.Editor.UI;

namespace com.MiAO.Unity.MCP.Editor
{
    public static class MenuItems
    {
        [MenuItem("Window/MCP Hub", priority = 1006)]
        public static void ShowWindow() => MainWindowEditor.ShowWindow();

        [MenuItem("Window/MCP Hub Manager", priority = 1007)]
        public static void ShowHubManager() => McpHubWindow.ShowWindow();

        [MenuItem("Window/MCP Hub Welcome", priority = 1008)]
        public static void ShowWelcome() => McpHubWelcomeWindow.ShowWindow();

        [MenuItem("Tools/MCP Hub/DotNet/Get Version", priority = 1010)]
        public static async void GetDotNetVersion() => await Startup.IsDotNetInstalled();

        [MenuItem("Tools/MCP Hub/DotNet/Install", priority = 1011)]
        public static async void InstallDotNet() => await Startup.InstallDotNetIfNeeded(force: true);

        [MenuItem("Tools/MCP Hub/MCP Plugin/Build and Start", priority = 1012)]
        public static void BuildAndStart() => McpPluginUnity.BuildAndStart();

        [MenuItem("Tools/MCP Hub/MCP Server/Build", priority = 1013)]
        public static Task BuildMcpServer() => Startup.BuildServer();

        [MenuItem("Tools/MCP Hub/MCP Server/Logs/Open Logs", priority = 1014)]
        public static void OpenLogs() => OpenFile(Startup.ServerLogsPath);

        [MenuItem("Tools/MCP Hub/MCP Server/Open Error Logs", priority = 1015)]
        public static void OpenErrorLogs() => OpenFile(Startup.ServerErrorLogsPath);
        static void OpenFile(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true // Ensures the file opens with the default application
                });
            }
            else
            {
                Debug.LogError($"{Consts.Log.Tag} Log file not found at: {filePath}");
            }
        }
    }
}
#endif