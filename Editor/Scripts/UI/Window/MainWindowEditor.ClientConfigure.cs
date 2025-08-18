using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using com.MiAO.MCP.Editor.Common;

namespace com.MiAO.MCP.Editor
{
    public partial class MainWindowEditor : EditorWindow
    {
        private static readonly JsonDocumentOptions JsonOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true 
        };
        void ConfigureClientsWindows(VisualElement root)
        {
            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Claude").First(),
                configPath: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Claude",
                    "claude_desktop_config.json"
                ),
                bodyName: "mcpServers");

            ConfigureCommonClients(root);
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

            ConfigureCommonClients(root);
        }

        void ConfigureCommonClients(VisualElement root)
        {
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

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-VisualStudio").First(),
                configPath: GetVisualStudioConfigPath(VisualStudioConfigLocation.Global),
                bodyName: "servers");

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Augment").First(),
                configPath: GetVSCodeSettingsPath(),
                bodyName: "augment.advanced.mcpServers");

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Windsurf").First(),
                configPath: GetWindsurfSettingsPath(),
                bodyName: "mcpServers");

            ConfigureClient(root.Query<VisualElement>("ConfigureClient-Cline").First(),
                configPath: GetClineSettingsPath(),
                bodyName: "mcpServers");
        }

        void ConfigureClient(VisualElement root, string configPath, string bodyName = "mcpServers")
        {
            var statusCircle = root.Query<VisualElement>("configureStatusCircle").First();
            var statusText = root.Query<Label>("configureStatusText").First();
            var btnConfigure = root.Query<Button>("btnConfigure").First();

            // Detect if this is Visual Studio configuration (has vsConfigLocation enum field)
            EnumField configLocationField = null;
            try
            {
                configLocationField = root.Query<EnumField>("vsConfigLocation").First();
            }
            catch
            {
                // Element not found - not a Visual Studio configuration
            }
            
            if (configLocationField != null)
            {
                ConfigureVisualStudioClient(statusCircle, statusText, btnConfigure, configLocationField, bodyName);
            }
            else
            {
                ConfigureStandardClient(statusCircle, statusText, btnConfigure, configPath, bodyName);
            }
        }

        void ConfigureVisualStudioClient(VisualElement statusCircle, Label statusText, Button btnConfigure, 
            EnumField configLocationField, string bodyName)
        {
            // Setup Visual Studio enum field
            configLocationField.Init(VisualStudioConfigLocation.Global);
            configLocationField.value = VisualStudioConfigLocation.Global;

            // Visual Studio dynamic path logic
            System.Func<string> getCurrentPath = () => GetVisualStudioConfigPath((VisualStudioConfigLocation)configLocationField.value);

            // Initialize status
            UpdateClientStatus(statusCircle, statusText, btnConfigure, IsMcpClientConfigured(getCurrentPath(), bodyName));

            // Configure button click event
            btnConfigure.RegisterCallback<ClickEvent>(evt =>
            {
                var pathToUse = getCurrentPath();
                var configureResult = ConfigureMcpClient(pathToUse, bodyName);
                UpdateClientStatus(statusCircle, statusText, btnConfigure, configureResult);
                
                if (configureResult)
                {
                    var location = (VisualStudioConfigLocation)configLocationField.value;
                    var locationName = MenuItems.GetVisualStudioLocationDisplayName(location);
                    Debug.Log($"Visual Studio MCP configuration completed successfully at: {pathToUse} ({locationName})");
                }
            });

            // Listen for configuration location changes
            configLocationField.RegisterValueChangedCallback(evt =>
            {
                var newPath = getCurrentPath();
                UpdateClientStatus(statusCircle, statusText, btnConfigure, IsMcpClientConfigured(newPath, bodyName));
            });
        }

        void ConfigureStandardClient(VisualElement statusCircle, Label statusText, Button btnConfigure, 
            string configPath, string bodyName)
        {
            // Initialize status
            UpdateClientStatus(statusCircle, statusText, btnConfigure, IsMcpClientConfigured(configPath, bodyName));

            // Configure button click event
            btnConfigure.RegisterCallback<ClickEvent>(evt =>
            {
                var configureResult = ConfigureMcpClient(configPath, bodyName);
                UpdateClientStatus(statusCircle, statusText, btnConfigure, configureResult);
            });
        }

        bool IsMcpClientConfigured(string configPath, string bodyName = "mcpServers")
        {
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                return false;

            // Check if this is Augment configuration - using inline logic
            if (bodyName.StartsWith("augment"))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var rootObj = JsonNode.Parse(json, documentOptions: JsonOptions)?.AsObject();
                    if (rootObj == null)
                        return false;

                    var augmentAdvanced = rootObj["augment.advanced"]?.AsObject();
                    if (augmentAdvanced == null)
                        return false;

                    var mcpServers = augmentAdvanced["mcpServers"]?.AsArray();
                    if (mcpServers == null)
                        return false;

                    // Check if Unity-MCP server is configured
                    foreach (var server in mcpServers)
                    {
                        var serverObj = server?.AsObject();
                        if (serverObj == null) continue;

                        var name = serverObj["name"]?.GetValue<string>();
                        var command = serverObj["command"]?.GetValue<string>();

                        if (name == "Unity-MCP" || (!string.IsNullOrEmpty(command) && 
                            string.Equals(Path.GetFullPath(command), Path.GetFullPath(Startup.ServerExecutableFile), StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error reading VS Code settings file: {ex.Message}");
                    Debug.LogException(ex);
                    return false;
                }
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var rootObj = JsonNode.Parse(json, documentOptions: JsonOptions)?.AsObject();
                if (rootObj == null)
                    return false;

                var mcpServers = rootObj[bodyName]?.AsObject();
                if (mcpServers == null)
                    return false;

                foreach (var kv in mcpServers)
                {
                    var serverConfig = kv.Value?.AsObject();
                    if (serverConfig == null) continue;
                    
                    var command = serverConfig["command"]?.GetValue<string>();
                    var argsArray = serverConfig["args"]?.AsArray();
                    
                    string[] args = null;
                    if (argsArray != null)
                    {
                        args = argsArray.Select(arg => arg?.GetValue<string>()).ToArray();
                    }
                    
                    var isPortMatched = argsArray?.Any(arg => arg?.GetValue<string>() == McpPluginUnity.Port.ToString()) ?? false;

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

        public bool ConfigureMcpClient(string configPath, string bodyName = "mcpServers")
        {
            if (string.IsNullOrEmpty(configPath))
                return false;

            // Check if this is Augment configuration - using inline logic
            if (bodyName.StartsWith("augment"))
            {
                try
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    JsonObject rootObj;
                    if (File.Exists(configPath))
                    {
                        var json = File.ReadAllText(configPath);
                        rootObj = JsonNode.Parse(json, documentOptions: JsonOptions)?.AsObject() ?? new JsonObject();
                    }
                    else
                    {
                        rootObj = new JsonObject();
                    }

                    // Ensure augment.advanced exists
                    if (!rootObj.ContainsKey("augment.advanced"))
                    {
                        rootObj["augment.advanced"] = new JsonObject();
                    }
                    var augmentAdvanced = rootObj["augment.advanced"]!.AsObject();

                    // Ensure mcpServers array exists
                    if (!augmentAdvanced.ContainsKey("mcpServers"))
                    {
                        augmentAdvanced["mcpServers"] = new JsonArray();
                    }
                    var mcpServers = augmentAdvanced["mcpServers"]!.AsArray();

                    // Remove existing Unity-MCP configurations
                    for (int i = mcpServers.Count - 1; i >= 0; i--)
                    {
                        var server = mcpServers[i]?.AsObject();
                        if (server == null) continue;

                        var name = server["name"]?.GetValue<string>();
                        var command = server["command"]?.GetValue<string>();

                        if (name == "Unity-MCP" || (!string.IsNullOrEmpty(command) && 
                            string.Equals(Path.GetFullPath(command), Path.GetFullPath(Startup.ServerExecutableFile), StringComparison.OrdinalIgnoreCase)))
                        {
                            mcpServers.RemoveAt(i);
                        }
                    }

                    // Add new Unity-MCP configuration
                    var unityMcpServer = new JsonObject
                    {
                        ["name"] = "Unity-MCP",
                        ["command"] = Startup.ServerExecutableFile.Replace('\\', '/'),
                        ["args"] = new JsonArray(
                            JsonValue.Create(McpPluginUnity.Port.ToString())
                        )
                    };

                    mcpServers.Add(unityMcpServer);

                    // Write back to file
                    File.WriteAllText(configPath, rootObj.ToJsonString(JsonSerializerOptions));

                    return IsMcpClientConfigured(configPath, bodyName);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error configuring Augment MCP: {ex.Message}");
                    Debug.LogException(ex);
                    return false;
                }
            }

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
                var rootObj = JsonNode.Parse(json, documentOptions: JsonOptions)?.AsObject();
                if (rootObj == null)
                    throw new Exception("Config file is not a valid JSON object.");

                // Parse the injected config as JsonObject
                var injectObj = JsonNode.Parse(Startup.RawJsonConfiguration(McpPluginUnity.Port, bodyName), documentOptions: JsonOptions)?.AsObject();
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
                    File.WriteAllText(configPath, rootObj.ToJsonString(JsonSerializerOptions));
                    return IsMcpClientConfigured(configPath, bodyName);
                }

                foreach (var server in mcpServers)
                {
                    var command = server.Value?["command"]?.GetValue<string>();
                    var args = server.Value?["args"]?.AsArray()?.Select(a => a?.GetValue<string>()).ToArray();
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

        public string GetVisualStudioConfigPath(VisualStudioConfigLocation location)
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

        // Unified status update function
        void UpdateClientStatus(VisualElement statusCircle, Label statusText, Button btnConfigure, bool isConfigured)
        {
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Connected);
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Connecting);
            statusCircle.RemoveFromClassList(USS_IndicatorClass_Disconnected);

            statusCircle.AddToClassList(isConfigured
                ? USS_IndicatorClass_Connected
                : USS_IndicatorClass_Disconnected);

            statusText.text = isConfigured 
                ? LocalizationManager.GetText("connector.configured")
                : LocalizationManager.GetText("connector.not_configured");
            btnConfigure.text = isConfigured 
                ? LocalizationManager.GetText("connector.reconfigure") 
                : LocalizationManager.GetText("connector.configure");
        }

        string GetVSCodeSettingsPath()
        {
            return GetVSCodeBasePath("settings.json");
        }

        string GetVSCodeBasePath(string fileName)
        {
#if UNITY_EDITOR_WIN
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Code",
                "User",
                fileName
            );
#elif UNITY_EDITOR_OSX
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "Code",
                "User",
                fileName
            );
#elif UNITY_EDITOR_LINUX
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "Code",
                "User",
                fileName
            );
#else
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Code",
                "User",
                fileName
            );
#endif
        }

        string GetWindsurfSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codeium",
                "windsurf",
                "mcp_config.json"
            );
        }

        List<string> GetAllClineSettingsPaths()
        {
            var paths = new List<string>
            {
                GetVSCodeBasePath(Path.Combine(
                    "globalStorage",
                    "saoudrizwan.claude-dev",
                    "settings",
                    "cline_mcp_settings.json"
                )),
                
                GetCursorBasePath(Path.Combine(
                    "globalStorage",
                    "saoudrizwan.claude-dev",
                    "settings",
                    "cline_mcp_settings.json"
                )),
                
                GetWindsurfBasePath(Path.Combine(
                    "globalStorage",
                    "saoudrizwan.claude-dev",
                    "settings",
                    "cline_mcp_settings.json"
                ))
            };
            
            return paths;
        }

        string GetClineSettingsPath()
        {
            var possiblePaths = GetAllClineSettingsPaths();

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return possiblePaths[0];
        }

        string GetCursorBasePath(string fileName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Cursor",
                "User",
                fileName
            );
        }

        string GetWindsurfBasePath(string fileName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".codeium",
                "windsurf",
                "User",
                fileName
            );
        }
    }
}