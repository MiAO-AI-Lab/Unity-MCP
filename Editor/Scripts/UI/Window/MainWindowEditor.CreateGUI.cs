using System;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.MiAO.Unity.MCP.Editor
{
    public partial class MainWindowEditor : EditorWindow
    {
        const string USS_IndicatorClass_Connected = "status-indicator-circle-online";
        const string USS_IndicatorClass_Connecting = "status-indicator-circle-connecting";
        const string USS_IndicatorClass_Disconnected = "status-indicator-circle-disconnected";

        const string ServerButtonText_Connect = "Connect";
        const string ServerButtonText_Disconnect = "Disconnect";
        const string ServerButtonText_Stop = "Stop";

        public void CreateGUI()
        {
            _disposables.Clear();
            rootVisualElement.Clear();
            
            var templateControlPanel = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.MiAO.Unity.MCP/Editor/UI/uxml/AiConnectorWindow.uxml");
            if (templateControlPanel == null)
            {
                Debug.LogError("'templateControlPanel' could not be loaded from path: Packages/com.MiAO.Unity.MCP/Editor/UI/uxml/AiConnectorWindow.uxml");
                return;
            }

            // Load and apply the stylesheet
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.MiAO.Unity.MCP/Editor/UI/uss/AiConnectorWindow.uss");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning("Could not load stylesheet from: Packages/com.MiAO.Unity.MCP/Editor/UI/uss/AiConnectorWindow.uss");
            }

            var root = templateControlPanel.Instantiate();
            rootVisualElement.Add(root);

            // Settings
            // -----------------------------------------------------------------

            var dropdownLogLevel = root.Query<EnumField>("dropdownLogLevel").First();
            dropdownLogLevel.value = McpPluginUnity.LogLevel;
            dropdownLogLevel.RegisterValueChangedCallback(evt =>
            {
                McpPluginUnity.LogLevel = evt.newValue as LogLevel? ?? LogLevel.Warning;
                SaveChanges($"[AI Connector] LogLevel Changed: {evt.newValue}");
                McpPluginUnity.BuildAndStart();
            });

            // Connection status
            // -----------------------------------------------------------------

            var inputFieldHost = root.Query<TextField>("InputServerURL").First();
            inputFieldHost.value = McpPluginUnity.Host;
            inputFieldHost.RegisterCallback<FocusOutEvent>(evt =>
            {
                var newValue = inputFieldHost.value;
                if (McpPluginUnity.Host == newValue)
                    return;

                McpPluginUnity.Host = newValue;
                SaveChanges($"[{nameof(MainWindowEditor)}] Host Changed: {newValue}");
                Invalidate();
            });

            var btnConnectOrDisconnect = root.Query<Button>("btnConnectOrDisconnect").First();
            var connectionStatusCircle = root
                .Query<VisualElement>("ServerConnectionInfo").First()
                .Query<VisualElement>("connectionStatusCircle").First();
            var connectionStatusText = root
                .Query<VisualElement>("ServerConnectionInfo").First()
                .Query<Label>("connectionStatusText").First();

            McpPlugin.DoAlways(plugin =>
            {
                Observable.CombineLatest(
                    McpPluginUnity.ConnectionState,
                    plugin.KeepConnected,
                    (connectionState, keepConnected) => (connectionState, keepConnected)
                )
                .ThrottleLast(TimeSpan.FromMilliseconds(10))
                .ObserveOnCurrentSynchronizationContext()
                .SubscribeOnCurrentSynchronizationContext()
                .Subscribe(tuple =>
                {
                    var (connectionState, keepConnected) = tuple;

                    inputFieldHost.isReadOnly = keepConnected || connectionState switch
                    {
                        HubConnectionState.Connected => true,
                        HubConnectionState.Disconnected => false,
                        HubConnectionState.Reconnecting => true,
                        HubConnectionState.Connecting => true,
                        _ => false
                    };
                    inputFieldHost.tooltip = plugin.KeepConnected.CurrentValue
                        ? "Editable only when disconnected from the MCP Server."
                        : "The server URL. https://localhost:60606";

                    // Update the style class
                    if (inputFieldHost.isReadOnly)
                    {
                        inputFieldHost.AddToClassList("disabled-text-field");
                        inputFieldHost.RemoveFromClassList("enabled-text-field");
                    }
                    else
                    {
                        inputFieldHost.AddToClassList("enabled-text-field");
                        inputFieldHost.RemoveFromClassList("disabled-text-field");
                    }

                    connectionStatusText.text = connectionState switch
                    {
                        HubConnectionState.Connected => keepConnected
                            ? "Connected"
                            : "Disconnected",
                        HubConnectionState.Disconnected => keepConnected
                            ? "Connecting..."
                            : "Disconnected",
                        HubConnectionState.Reconnecting => keepConnected
                            ? "Connecting..."
                            : "Disconnected",
                        HubConnectionState.Connecting => keepConnected
                            ? "Connecting..."
                            : "Disconnected",
                        _ => McpPluginUnity.IsConnected.CurrentValue.ToString() ?? "Unknown"
                    };

                    btnConnectOrDisconnect.text = connectionState switch
                    {
                        HubConnectionState.Connected => keepConnected
                            ? ServerButtonText_Disconnect
                            : ServerButtonText_Connect,
                        HubConnectionState.Disconnected => keepConnected
                            ? ServerButtonText_Stop
                            : ServerButtonText_Connect,
                        HubConnectionState.Reconnecting => keepConnected
                            ? ServerButtonText_Stop
                            : ServerButtonText_Connect,
                        HubConnectionState.Connecting => keepConnected
                            ? ServerButtonText_Stop
                            : ServerButtonText_Connect,
                        _ => McpPluginUnity.IsConnected.CurrentValue.ToString() ?? "Unknown"
                    };

                    connectionStatusCircle.RemoveFromClassList(USS_IndicatorClass_Connected);
                    connectionStatusCircle.RemoveFromClassList(USS_IndicatorClass_Connecting);
                    connectionStatusCircle.RemoveFromClassList(USS_IndicatorClass_Disconnected);

                    connectionStatusCircle.AddToClassList(connectionState switch
                    {
                        HubConnectionState.Connected => keepConnected
                            ? USS_IndicatorClass_Connected
                            : USS_IndicatorClass_Disconnected,
                        HubConnectionState.Disconnected => keepConnected
                            ? USS_IndicatorClass_Connecting
                            : USS_IndicatorClass_Disconnected,
                        HubConnectionState.Reconnecting => keepConnected
                            ? USS_IndicatorClass_Connecting
                            : USS_IndicatorClass_Disconnected,
                        HubConnectionState.Connecting => keepConnected
                            ? USS_IndicatorClass_Connecting
                            : USS_IndicatorClass_Disconnected,
                        _ => throw new ArgumentOutOfRangeException(nameof(connectionState), connectionState, null)
                    });
                })
                .AddTo(_disposables);
            }).AddTo(_disposables);


            btnConnectOrDisconnect.RegisterCallback<ClickEvent>(evt =>
            {
                if (btnConnectOrDisconnect.text == ServerButtonText_Connect)
                {
                    // btnConnectOrDisconnect.text = ServerButtonText_Stop;
                    McpPluginUnity.KeepConnected = true;
                    McpPluginUnity.Save();
                    if (McpPlugin.HasInstance)
                    {
                        McpPlugin.Instance.Connect();
                    }
                    else
                    {
                        McpPluginUnity.BuildAndStart();
                    }
                }
                else if (btnConnectOrDisconnect.text == ServerButtonText_Disconnect)
                {
                    // btnConnectOrDisconnect.text = ServerButtonText_Connect;
                    McpPluginUnity.KeepConnected = false;
                    McpPluginUnity.Save();
                    if (McpPlugin.HasInstance)
                        McpPlugin.Instance.Disconnect();
                }
                else if (btnConnectOrDisconnect.text == ServerButtonText_Stop)
                {
                    // btnConnectOrDisconnect.text = ServerButtonText_Connect;
                    McpPluginUnity.KeepConnected = false;
                    McpPluginUnity.Save();
                    if (McpPlugin.HasInstance)
                        McpPlugin.Instance.Disconnect();
                }
            });

            // Configure MCP Client
            // -----------------------------------------------------------------

#if UNITY_EDITOR_WIN
            ConfigureClientsWindows(root);
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            ConfigureClientsMacAndLinux(root);
#endif

            // Provide raw json configuration
            // -----------------------------------------------------------------

            var rawJsonField = root.Query<TextField>("rawJsonConfiguration").First();
            rawJsonField.value = Startup.RawJsonConfiguration(McpPluginUnity.Port);

            // AI Configuration
            // -----------------------------------------------------------------
            ConfigureAISettings(root);

            // Rebuild MCP Server
            // -----------------------------------------------------------------
            root.Query<Button>("btnRebuildServer").First().RegisterCallback<ClickEvent>(async evt =>
            {
                await Startup.BuildServer(force: true);
            });
        }

        private void ConfigureAISettings(VisualElement root)
        {
            var btnSaveConfig = root.Query<Button>("btnSaveConfig").First();

            // Load current configuration
            LoadAIConfiguration(root);

            btnSaveConfig.RegisterCallback<ClickEvent>(evt =>
            {
                SaveAIConfiguration(root);
            });
        }

        private void LoadAIConfiguration(VisualElement root)
        {
            try
            {
                var configPath = "Packages/com.miao.unity.mcp/Config/AI_Config.json";
                var exampleConfigPath = "Packages/com.miao.unity.mcp/Config/AI_Config.json.example";
                
                // If AI_Config.json doesn't exist, copy from AI_Config.json.example
                if (!System.IO.File.Exists(configPath) && System.IO.File.Exists(exampleConfigPath))
                {
                    System.IO.File.Copy(exampleConfigPath, configPath);
                    Debug.Log($"Created AI_Config.json from example file: {configPath}");
                }
                
                var configText = System.IO.File.ReadAllText(configPath);
                var config = JsonUtility.FromJson<AIConfigData>(configText);

                // OpenAI settings
                root.Query<TextField>("openaiApiKey").First().value = config.openaiApiKey ?? "";
                root.Query<TextField>("openaiModel").First().value = config.openaiModel ?? "gpt-4o";
                root.Query<TextField>("openaiBaseUrl").First().value = config.openaiBaseUrl ?? "";

                // Gemini settings
                root.Query<TextField>("geminiApiKey").First().value = config.geminiApiKey ?? "";
                root.Query<TextField>("geminiModel").First().value = config.geminiModel ?? "gemini-pro";
                root.Query<TextField>("geminiBaseUrl").First().value = config.geminiBaseUrl ?? "";

                // Claude settings
                root.Query<TextField>("claudeApiKey").First().value = config.claudeApiKey ?? "";
                root.Query<TextField>("claudeModel").First().value = config.claudeModel ?? "claude-3-sonnet-20240229";
                root.Query<TextField>("claudeBaseUrl").First().value = config.claudeBaseUrl ?? "";

                // Local settings
                root.Query<TextField>("localApiUrl").First().value = config.localApiUrl ?? "";
                root.Query<TextField>("localModel").First().value = config.localModel ?? "llava";

                // Model provider settings
                root.Query<TextField>("visionModelProvider").First().value = config.visionModelProvider ?? "openai";
                root.Query<TextField>("textModelProvider").First().value = config.textModelProvider ?? "openai";
                root.Query<TextField>("codeModelProvider").First().value = config.codeModelProvider ?? "claude";

                // Other settings
                root.Query<IntegerField>("timeoutSeconds").First().value = config.timeoutSeconds;
                root.Query<IntegerField>("maxTokens").First().value = config.maxTokens;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load AI configuration: {ex.Message}");
            }
        }

        private void SaveAIConfiguration(VisualElement root)
        {
            try
            {
                var config = new AIConfigData
                {
                    openaiApiKey = root.Query<TextField>("openaiApiKey").First().value,
                    openaiModel = root.Query<TextField>("openaiModel").First().value,
                    openaiBaseUrl = root.Query<TextField>("openaiBaseUrl").First().value,
                    
                    geminiApiKey = root.Query<TextField>("geminiApiKey").First().value,
                    geminiModel = root.Query<TextField>("geminiModel").First().value,
                    geminiBaseUrl = root.Query<TextField>("geminiBaseUrl").First().value,
                    
                    claudeApiKey = root.Query<TextField>("claudeApiKey").First().value,
                    claudeModel = root.Query<TextField>("claudeModel").First().value,
                    claudeBaseUrl = root.Query<TextField>("claudeBaseUrl").First().value,
                    
                    localApiUrl = root.Query<TextField>("localApiUrl").First().value,
                    localModel = root.Query<TextField>("localModel").First().value,
                    
                    visionModelProvider = root.Query<TextField>("visionModelProvider").First().value,
                    textModelProvider = root.Query<TextField>("textModelProvider").First().value,
                    codeModelProvider = root.Query<TextField>("codeModelProvider").First().value,
                    
                    timeoutSeconds = root.Query<IntegerField>("timeoutSeconds").First().value,
                    maxTokens = root.Query<IntegerField>("maxTokens").First().value
                };

                // 1. Save configuration file in Unity package
                var unityConfigPath = "Packages/com.MiAO.Unity.MCP/Config/AI_Config.json";
                var jsonText = JsonUtility.ToJson(config, true);
                System.IO.File.WriteAllText(unityConfigPath, jsonText);
                
                // 2. Also update server environment configuration file
                UpdateServerConfiguration(config);
                
                // 3. Reload AgentModelProxy configuration
                ReloadAgentModelProxyConfig();
                
                Debug.Log("AI configuration saved successfully to both Unity and Server environments!");
                SaveChanges("[AI Connector] AI configuration updated");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save AI configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Update server environment configuration file
        /// </summary>
        private void UpdateServerConfiguration(AIConfigData config)
        {
            try
            {
                // Build server configuration file path (based on the actual path you provided)
                var projectRoot = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..");
                var serverConfigPath = System.IO.Path.Combine(
                    projectRoot,
                    "Library", "com.MiAO.unity.mcp.server", "bin~", "Release", "net9.0", 
                    "Config", "AI_Config.json"
                );
                
                // Ensure directory exists
                var serverConfigDir = System.IO.Path.GetDirectoryName(serverConfigPath);
                if (!System.IO.Directory.Exists(serverConfigDir))
                {
                    System.IO.Directory.CreateDirectory(serverConfigDir);
                }
                
                // Save configuration file to server environment
                System.IO.File.WriteAllText(serverConfigPath, JsonUtility.ToJson(config, true));
                
                Debug.Log($"Server configuration updated: {serverConfigPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to update server configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Reload AgentModelProxy configuration
        /// </summary>
        private void ReloadAgentModelProxyConfig()
        {
            try
            {
                // Use reflection to call AgentModelProxy.ReloadConfig()
                var agentModelProxyType = System.Type.GetType("com.MiAO.Unity.MCP.Editor.Server.AgentModelProxy, com.MiAO.Unity.MCP.Editor");
                if (agentModelProxyType != null)
                {
                    var reloadConfigMethod = agentModelProxyType.GetMethod("ReloadConfig", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (reloadConfigMethod != null)
                    {
                        reloadConfigMethod.Invoke(null, null);
                        Debug.Log("AgentModelProxy configuration reloaded successfully!");
                    }
                    else
                    {
                        Debug.LogWarning("ReloadConfig method not found in AgentModelProxy");
                    }
                }
                else
                {
                    Debug.LogWarning("AgentModelProxy type not found");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to reload AgentModelProxy configuration: {ex.Message}");
            }
        }

        [System.Serializable]
        private class AIConfigData
        {
            public string openaiApiKey;
            public string openaiModel;
            public string openaiBaseUrl;
            public string geminiApiKey;
            public string geminiModel;
            public string geminiBaseUrl;
            public string claudeApiKey;
            public string claudeModel;
            public string claudeBaseUrl;
            public string localApiUrl;
            public string localModel;
            public string visionModelProvider;
            public string textModelProvider;
            public string codeModelProvider;
            public int timeoutSeconds;
            public int maxTokens;
        }
    }
}