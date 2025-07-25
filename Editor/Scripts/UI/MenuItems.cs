#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Editor.UI;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace com.MiAO.Unity.MCP.Editor
{
    public static class MenuItems
    {
        [MenuItem("Window/MCP Hub/MCP Main Window", priority = 1006)]
        public static void ShowWindow() => MainWindowEditor.ShowWindow();

        [MenuItem("Window/MCP Hub/MCP Hub Manager", priority = 1007)]
        public static void ShowHubManager() => McpHubWindow.ShowWindow();

        [MenuItem("Window/MCP Hub/Welcome", priority = 1008)]
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
		
        static void ConfigureVisualStudio(com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation location)
        {
            var configPath = GetVisualStudioConfigPath(location);
            var configResult = ConfigureMcpClient(configPath, "servers");
            var locationName = GetVisualStudioLocationDisplayName(location);

            if (configResult)
            {
                Debug.Log($"{Consts.Log.Tag} Visual Studio MCP configuration completed successfully at: {configPath} ({locationName})");
            }
            else
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to configure Visual Studio MCP at: {configPath} ({locationName})");
            }
        }

        static string GetVisualStudioConfigPath(com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation location)
        {
            switch (location)
            {
                case com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation.Global:
                    return System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                        ".mcp.json"
                    );
                
                case com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation.Solution:
                    return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", ".mcp.json"));
                
                case com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation.VisualStudioSpecific:
                    return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", ".vs", "mcp.json"));
                
                default:
                    return System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                        ".mcp.json"
                    );
            }
        }

        static string GetVisualStudioLocationDisplayName(com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation location)
        {
            switch (location)
            {
                case com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation.Global:
                    return "Global User Configuration";
                
                case com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation.Solution:
                    return "Solution Level Configuration";
                
                case com.MiAO.Unity.MCP.Editor.Common.VisualStudioConfigLocation.VisualStudioSpecific:
                    return "Visual Studio Specific Configuration";
                
                default:
                    return "Unknown";
            }
        }

        static bool ConfigureMcpClient(string configPath, string bodyName = "mcpServers")
        {
            if (string.IsNullOrEmpty(configPath))
                return false;

            try
            {
                // 确保目录存在
                var directory = System.IO.Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                if (!System.IO.File.Exists(configPath))
                {
                    // Create the file if it doesn't exist
                    System.IO.File.WriteAllText(configPath, Startup.RawJsonConfiguration(McpPluginUnity.Port, bodyName));
                    return true;
                }

                var json = System.IO.File.ReadAllText(configPath);

                // Parse the existing config as JsonObject
                var rootObj = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject();
                if (rootObj == null)
                    throw new System.Exception("Config file is not a valid JSON object.");

                // Parse the injected config as JsonObject
                var injectObj = System.Text.Json.Nodes.JsonNode.Parse(Startup.RawJsonConfiguration(McpPluginUnity.Port, bodyName))?.AsObject();
                if (injectObj == null)
                    throw new System.Exception("Injected config is not a valid JSON object.");

                // Get servers from both
                var servers = rootObj[bodyName]?.AsObject();
                var injectServers = injectObj[bodyName]?.AsObject();
                if (injectServers == null)
                    throw new System.Exception($"Missing '{bodyName}' object in config.");

                if (servers == null)
                {
                    // If servers is null, create it
                    rootObj[bodyName] = System.Text.Json.Nodes.JsonNode.Parse(injectServers.ToJsonString())?.AsObject();
                    System.IO.File.WriteAllText(configPath, rootObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    return true;
                }

                // Find all command values in injectServers
                var injectCommands = injectServers
                    .Select(kv => kv.Value?["command"]?.GetValue<string>())
                    .Where(cmd => !string.IsNullOrEmpty(cmd))
                    .ToHashSet();

                // Remove any entry in servers with a matching command
                var keysToRemove = servers
                    .Where(kv => injectCommands.Contains(kv.Value?["command"]?.GetValue<string>()))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                    servers.Remove(key);

                // Merge/overwrite entries from injectServers
                foreach (var kv in injectServers)
                {
                    // Clone the value to avoid parent conflict
                    servers[kv.Key] = kv.Value?.ToJsonString() is string jsonStr
                        ? System.Text.Json.Nodes.JsonNode.Parse(jsonStr)
                        : null;
                }

                // Write back to file
                System.IO.File.WriteAllText(configPath, rootObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error configuring MCP client: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

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