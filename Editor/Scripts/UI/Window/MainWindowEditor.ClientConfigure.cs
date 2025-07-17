using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor
{
    public partial class MainWindowEditor : EditorWindow
    {
        void ConfigureClientsWindows(VisualElement root)
        {
            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Claude").First(),
                configPath: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Claude",
                    "claude_desktop_config.json"
                ),
                bodyName: "mcpServers");

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-VS-Code").First(),
                configPath: Path.Combine(
                    ".vscode",
                    "mcp.json"
                ),
                bodyName: "servers");

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Cursor").First(),
                configPath: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cursor",
                    "mcp.json"
                ),
                bodyName: "mcpServers");

            ConfigureVisualStudioClient(root.Query<VisualElement>("ConfigureClient-VisualStudio").First());
        }

        void ConfigureClientsMacAndLinux(VisualElement root)
        {
            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Claude").First(),
                configPath: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    "Claude",
                    "claude_desktop_config.json"
                ),
                bodyName: "mcpServers");

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-VS-Code").First(),
                configPath: Path.Combine(
                    ".vscode",
                    "mcp.json"
                ),
                bodyName: "servers");

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Cursor").First(),
                configPath: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cursor",
                    "mcp.json"
                ),
                bodyName: "mcpServers");

            ConfigureVisualStudioClient(root.Query<VisualElement>("ConfigureClient-VisualStudio").First());
        }

        void ConfigureClient(VisualElement root, string configPath, string bodyName = "mcpServers")
        {
            var statusCircle = root.Query<VisualElement>("configureStatusCircle").First();
            var statusText = root.Query<Label>("configureStatusText").First();
            var btnConfigure = root.Query<Button>("btnConfigure").First();

            var isConfiguredResult = IsMcpClientConfigured(configPath, bodyName);

            statusCircle.RemoveFromClassList(USS_IndicatorClass_Connected);
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Connecting);
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Disconnected);

            statusCircle.AddToClassList(isConfiguredResult
                ? USS_IndicatorClass_Connected
                : USS_IndicatorClass_Disconnected);

            statusText.text = isConfiguredResult ? "Configured" : "Not Configured";
            btnConfigure.text = isConfiguredResult ? "Reconfigure" : "Configure";

            btnConfigure.RegisterCallback<ClickEvent>(evt =>
            {
                var configureResult = ConfigureMcpClient(configPath, bodyName);

                statusText.text = configureResult ? "Configured" : "Not Configured";

                statusCircle.RemoveFromClassList(USS_IndicatorClass_Connected);
                statusCircle.RemoveFromClassList(USS_IndicatorClass_Connecting);
                statusCircle.RemoveFromClassList(USS_IndicatorClass_Disconnected);

                statusCircle.AddToClassList(configureResult
                    ? USS_IndicatorClass_Connected
                    : USS_IndicatorClass_Disconnected);

                btnConfigure.text = configureResult ? "Reconfigure" : "Configure";
            });
        }

        bool IsMcpClientConfigured(string configPath, string bodyName = "mcpServers")
        {
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                return false;

            try
            {
                var json = File.ReadAllText(configPath);

                var rootObj = JsonNode.Parse(json)?.AsObject();
                if (rootObj == null)
                    return false;

                var mcpServers = rootObj[bodyName]?.AsObject();
                if (mcpServers == null)
                    return false;

                foreach (var kv in mcpServers)
                {
                    var isPortMatched = kv.Value?["args"]?.AsArray()
                        ?.Any(arg => arg?.GetValue<string>() == McpPluginUnity.Port.ToString()) ?? false;

                    var command = kv.Value?["command"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(command))
                    {
                        // Normalize both paths for comparison
                        try
                        {
                            var normalizedCommand = Path.GetFullPath(command.Replace('/', Path.DirectorySeparatorChar));
                            var normalizedTarget = Path.GetFullPath(Startup.ServerExecutableFile.Replace('/', Path.DirectorySeparatorChar));
                            if (string.Equals(normalizedCommand, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                                return isPortMatched;
                        }
                        catch
                        {
                            // If normalization fails, fallback to string comparison
                            if (string.Equals(command, Startup.ServerExecutableFile, StringComparison.OrdinalIgnoreCase))
                                return isPortMatched;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error reading config file: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }
        bool ConfigureMcpClient(string configPath, string bodyName = "mcpServers")
        {
            if (string.IsNullOrEmpty(configPath))
                return false;

            try
            {
                if (!File.Exists(configPath))
                {
                    // Create the file if it doesn't exist
                    File.WriteAllText(configPath, Startup.RawJsonConfiguration(McpPluginUnity.Port, bodyName));
                    return true;
                }

                var json = File.ReadAllText(configPath);

                // Parse the existing config as JsonObject
                var rootObj = JsonNode.Parse(json)?.AsObject();
                if (rootObj == null)
                    throw new Exception("Config file is not a valid JSON object.");

                // Parse the injected config as JsonObject
                var injectObj = JsonNode.Parse(Startup.RawJsonConfiguration(McpPluginUnity.Port, bodyName))?.AsObject();
                if (injectObj == null)
                    throw new Exception("Injected config is not a valid JSON object.");

                // Get mcpServers from both
                var mcpServers = rootObj[bodyName]?.AsObject();
                var injectMcpServers = injectObj[bodyName]?.AsObject();
                if (injectMcpServers == null)
                    throw new Exception($"Missing '{bodyName}' object in config.");

                if (mcpServers == null)
                {
                    // If mcpServers is null, create it
                    rootObj[bodyName] = JsonNode.Parse(injectMcpServers.ToJsonString())?.AsObject();
                    File.WriteAllText(configPath, rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    return IsMcpClientConfigured(configPath, bodyName);
                }

                // Find all command values in injectMcpServers
                var injectCommands = injectMcpServers
                    .Select(kv => kv.Value?["command"]?.GetValue<string>())
                    .Where(cmd => !string.IsNullOrEmpty(cmd))
                    .ToHashSet();

                // Remove any entry in mcpServers with a matching command
                var keysToRemove = mcpServers
                    .Where(kv => injectCommands.Contains(kv.Value?["command"]?.GetValue<string>()))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                    mcpServers.Remove(key);

                // Merge/overwrite entries from injectMcpServers
                foreach (var kv in injectMcpServers)
                {
                    // Clone the value to avoid parent conflict
                    mcpServers[kv.Key] = kv.Value?.ToJsonString() is string jsonStr
                        ? JsonNode.Parse(jsonStr)
                        : null;
                }

                // Write back to file
                File.WriteAllText(configPath, rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

                return IsMcpClientConfigured(configPath, bodyName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error reading config file: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        void ConfigureVisualStudioClient(VisualElement root)
        {
            var statusCircle = root.Query<VisualElement>("configureStatusCircle").First();
            var statusText = root.Query<Label>("configureStatusText").First();
            var btnConfigure = root.Query<Button>("btnConfigure").First();
            var configLocationField = root.Query<EnumField>("vsConfigLocation").First();

            // 设置枚举字段类型
            configLocationField.Init(VisualStudioConfigLocation.Global);
            configLocationField.value = VisualStudioConfigLocation.Global;

            var currentLocation = (VisualStudioConfigLocation)configLocationField.value;
            var configPath = GetVisualStudioConfigPath(currentLocation);
            var isConfiguredResult = IsMcpClientConfigured(configPath, "servers");

            UpdateVisualStudioStatus(statusCircle, statusText, btnConfigure, isConfiguredResult);

            // 监听配置位置变化
            configLocationField.RegisterValueChangedCallback(evt =>
            {
                var newLocation = (VisualStudioConfigLocation)evt.newValue;
                var newConfigPath = GetVisualStudioConfigPath(newLocation);
                var newIsConfigured = IsMcpClientConfigured(newConfigPath, "servers");
                UpdateVisualStudioStatus(statusCircle, statusText, btnConfigure, newIsConfigured);
            });

            btnConfigure.RegisterCallback<ClickEvent>(evt =>
            {
                var location = (VisualStudioConfigLocation)configLocationField.value;
                var targetConfigPath = GetVisualStudioConfigPath(location);
                var configureResult = ConfigureMcpClient(targetConfigPath, "servers");

                UpdateVisualStudioStatus(statusCircle, statusText, btnConfigure, configureResult);

                if (configureResult)
                {
                    var locationName = GetVisualStudioLocationDisplayName(location);
                    Debug.Log($"Visual Studio MCP configuration completed successfully at: {targetConfigPath} ({locationName})");
                }
            });
        }

        void UpdateVisualStudioStatus(VisualElement statusCircle, Label statusText, Button btnConfigure, bool isConfigured)
        {
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Connected);
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Connecting);
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Disconnected);

            statusCircle.AddToClassList(isConfigured
                ? USS_IndicatorClass_Connected
                : USS_IndicatorClass_Disconnected);

            statusText.text = isConfigured ? "Configured" : "Not Configured";
            btnConfigure.text = isConfigured ? "Reconfigure" : "Configure";
        }

        string GetVisualStudioConfigPath(VisualStudioConfigLocation location)
        {
            switch (location)
            {
                case VisualStudioConfigLocation.Global:
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".mcp.json"
                    );
                
                case VisualStudioConfigLocation.Solution:
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".mcp.json"));
                
                case VisualStudioConfigLocation.VisualStudioSpecific:
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".vs", "mcp.json"));
                
                default:
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".mcp.json"
                    );
            }
        }

        string GetVisualStudioLocationDisplayName(VisualStudioConfigLocation location)
        {
            switch (location)
            {
                case VisualStudioConfigLocation.Global:
                    return "Global User Configuration";
                
                case VisualStudioConfigLocation.Solution:
                    return "Solution Level Configuration";
                
                case VisualStudioConfigLocation.VisualStudioSpecific:
                    return "Visual Studio Specific Configuration";
                
                default:
                    return "Unknown";
            }
        }
    }
}