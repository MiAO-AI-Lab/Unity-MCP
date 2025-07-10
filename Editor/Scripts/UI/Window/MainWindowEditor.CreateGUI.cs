using System;
using System.Collections.Generic;
using System.Linq;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Editor.Common;
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



        public void CreateGUI()
        {
            _disposables.Clear();
            rootVisualElement.Clear();
            
            var templateControlPanel = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.miao.unity.mcp/Editor/UI/uxml/AiConnectorWindow.uxml");
            if (templateControlPanel == null)
            {
                Debug.LogError("'templateControlPanel' could not be loaded from path: Packages/com.miao.unity.mcp/Editor/UI/uxml/AiConnectorWindow.uxml");
                return;
            }

            // Load and apply the stylesheet
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.miao.unity.mcp/Editor/UI/uss/AiConnectorWindow.uss");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning("Could not load stylesheet from: Packages/com.miao.unity.mcp/Editor/UI/uss/AiConnectorWindow.uss");
            }

            var root = templateControlPanel.Instantiate();
            rootVisualElement.Add(root);

            // Apply initial localization
            // -----------------------------------------------------------------
            ApplyUILocalization(root);

            // Subscribe to language change events
            // -----------------------------------------------------------------
            LocalizationManager.OnLanguageChanged += (newLanguage) =>
            {
                ApplyUILocalization(root);
                // Update dynamic connection status text
                UpdateConnectionStatusText(root);
            };

            // Initialize tab system
            // -----------------------------------------------------------------
            InitializeTabSystem(root);

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
                            ? LocalizationManager.GetText("connector.connected")
                            : LocalizationManager.GetText("connector.disconnected"),
                        HubConnectionState.Disconnected => keepConnected
                            ? LocalizationManager.GetText("connector.connecting")
                            : LocalizationManager.GetText("connector.disconnected"),
                        HubConnectionState.Reconnecting => keepConnected
                            ? LocalizationManager.GetText("connector.connecting")
                            : LocalizationManager.GetText("connector.disconnected"),
                        HubConnectionState.Connecting => keepConnected
                            ? LocalizationManager.GetText("connector.connecting")
                            : LocalizationManager.GetText("connector.disconnected"),
                        _ => McpPluginUnity.IsConnected.CurrentValue.ToString() ?? "Unknown"
                    };

                    btnConnectOrDisconnect.text = connectionState switch
                    {
                        HubConnectionState.Connected => keepConnected
                            ? LocalizationManager.GetText("connector.disconnect")
                            : LocalizationManager.GetText("connector.connect"),
                        HubConnectionState.Disconnected => keepConnected
                            ? LocalizationManager.GetText("connector.stop")
                            : LocalizationManager.GetText("connector.connect"),
                        HubConnectionState.Reconnecting => keepConnected
                            ? LocalizationManager.GetText("connector.stop")
                            : LocalizationManager.GetText("connector.connect"),
                        HubConnectionState.Connecting => keepConnected
                            ? LocalizationManager.GetText("connector.stop")
                            : LocalizationManager.GetText("connector.connect"),
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
                if (btnConnectOrDisconnect.text == LocalizationManager.GetText("connector.connect"))
                {
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
                else if (btnConnectOrDisconnect.text == LocalizationManager.GetText("connector.disconnect"))
                {
                    McpPluginUnity.KeepConnected = false;
                    McpPluginUnity.Save();
                    if (McpPlugin.HasInstance)
                        McpPlugin.Instance.Disconnect();
                }
                else if (btnConnectOrDisconnect.text == LocalizationManager.GetText("connector.stop"))
                {
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

                // Model provider settings - using DropdownField
                var providerOptions = new System.Collections.Generic.List<string> { "openai", "claude", "gemini", "local" };
                
                var visionProviderDropdown = root.Query<DropdownField>("visionModelProvider").First();
                visionProviderDropdown.choices = providerOptions;
                visionProviderDropdown.value = config.visionModelProvider ?? "openai";
                
                var textProviderDropdown = root.Query<DropdownField>("textModelProvider").First();
                textProviderDropdown.choices = providerOptions;
                textProviderDropdown.value = config.textModelProvider ?? "openai";
                
                var codeProviderDropdown = root.Query<DropdownField>("codeModelProvider").First();
                codeProviderDropdown.choices = providerOptions;
                codeProviderDropdown.value = config.codeModelProvider ?? "claude";

                // Register change callbacks for immediate config update
                visionProviderDropdown.RegisterValueChangedCallback(evt => SaveAIConfigurationImmediate(root));
                textProviderDropdown.RegisterValueChangedCallback(evt => SaveAIConfigurationImmediate(root));
                codeProviderDropdown.RegisterValueChangedCallback(evt => SaveAIConfigurationImmediate(root));

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
                    
                    visionModelProvider = root.Query<DropdownField>("visionModelProvider").First().value,
                    textModelProvider = root.Query<DropdownField>("textModelProvider").First().value,
                    codeModelProvider = root.Query<DropdownField>("codeModelProvider").First().value,
                    
                    timeoutSeconds = root.Query<IntegerField>("timeoutSeconds").First().value,
                    maxTokens = root.Query<IntegerField>("maxTokens").First().value
                };

                // 1. Save configuration file in Unity package
                var unityConfigPath = "Packages/com.miao.unity.mcp/Config/AI_Config.json";
                var jsonText = JsonUtility.ToJson(config, true);
                System.IO.File.WriteAllText(unityConfigPath, jsonText);
                
                // 2. Also update server environment configuration file
                UpdateServerConfiguration(config);

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
                    "Library", "com.miao.unity.mcp.server", "bin~", "Release", "net9.0", 
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
        /// Save AI configuration immediately when dropdown values change
        /// </summary>
        private void SaveAIConfigurationImmediate(VisualElement root)
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
                    
                    visionModelProvider = root.Query<DropdownField>("visionModelProvider").First().value,
                    textModelProvider = root.Query<DropdownField>("textModelProvider").First().value,
                    codeModelProvider = root.Query<DropdownField>("codeModelProvider").First().value,
                    
                    timeoutSeconds = root.Query<IntegerField>("timeoutSeconds").First().value,
                    maxTokens = root.Query<IntegerField>("maxTokens").First().value
                };

                // 1. Save configuration file in Unity package
                var unityConfigPath = "Packages/com.miao.unity.mcp/Config/AI_Config.json";
                var jsonText = JsonUtility.ToJson(config, true);
                System.IO.File.WriteAllText(unityConfigPath, jsonText);
                
                // 2. Also update server environment configuration file
                UpdateServerConfiguration(config);
                
                
                Debug.Log($"Model provider configuration updated automatically!");
                SaveChanges("[AI Connector] Model provider changed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save AI configuration immediately: {ex.Message}");
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

        /// <summary>
        /// Update dynamic text related to connection status
        /// </summary>
        private void UpdateConnectionStatusText(VisualElement root)
        {
            // This method will automatically update connection status text when language changes
            // Since connection status is updated in real-time through Observable, it will automatically use new localized text after language change
        }

        /// <summary>
        /// Apply localized text to UXML elements
        /// </summary>
        private void ApplyUILocalization(VisualElement root)
        {
            // Apply tab page text
            root.Query<Button>("TabConnector").First().text = LocalizationManager.GetText("tab.connector");
            root.Query<Button>("TabModelConfig").First().text = LocalizationManager.GetText("tab.modelconfig");
            root.Query<Button>("TabUndoHistory").First().text = LocalizationManager.GetText("tab.operations");
            root.Query<Button>("TabSettings").First().text = LocalizationManager.GetText("tab.settings");

            // Connector page tab content
            root.Query<Label>("labelSettings").First().text = LocalizationManager.GetText("connector.title");
            root.Query<EnumField>("dropdownLogLevel").First().label = LocalizationManager.GetText("connector.loglevel");
            
            var connectServerLabels = root.Query<Label>().Where(l => l.text == "Connect to MCP server").ToList();
            foreach (var label in connectServerLabels)
            {
                label.text = LocalizationManager.GetText("connector.connect_server");
            }
            
            root.Query<TextField>("InputServerURL").First().label = LocalizationManager.GetText("connector.server_url");
            
            var informationFoldouts = root.Query<Foldout>().Where(f => f.text == "Information").ToList();
            foreach (var foldout in informationFoldouts)
            {
                foldout.text = LocalizationManager.GetText("connector.information");
            }
            
            // Update server information description
            var serverInfoLabels = root.Query<Label>().Where(l => l.text.Contains("Usually the server is hosted locally")).ToList();
            foreach (var label in serverInfoLabels)
            {
                label.text = LocalizationManager.GetText("connector.info_desc");
            }
            
            // Configure client part
            var configureClientLabels = root.Query<Label>().Where(l => l.text == "Configure MCP Client").ToList();
            foreach (var label in configureClientLabels)
            {
                label.text = LocalizationManager.GetText("connector.configure_client");
            }
            
            var configureClientDescLabels = root.Query<Label>().Where(l => l.text.Contains("At least one client")).ToList();
            foreach (var label in configureClientDescLabels)
            {
                label.text = LocalizationManager.GetText("connector.client_desc");
            }
            
            // Configure button
            var configureButtons = root.Query<Button>("btnConfigure").ToList();
            foreach (var button in configureButtons)
            {
                button.text = LocalizationManager.GetText("connector.configure");
            }
            
            // Configure status text
            var notConfiguredLabels = root.Query<Label>("configureStatusText").ToList();
            foreach (var label in notConfiguredLabels)
            {
                if (label.text == "Not configured")
                    label.text = LocalizationManager.GetText("connector.not_configured");
            }
            
            // Manual configuration part
            var manualConfigLabels = root.Query<Label>().Where(l => l.text == "Manual configuration").ToList();
            foreach (var label in manualConfigLabels)
            {
                label.text = LocalizationManager.GetText("connector.manual_config");
            }
            
            var manualConfigDescLabels = root.Query<Label>().Where(l => l.text.Contains("Copy paste the json")).ToList();
            foreach (var label in manualConfigDescLabels)
            {
                label.text = LocalizationManager.GetText("connector.manual_desc");
            }
            
            // Manual configuration text field
            var rawJsonField = root.Query<TextField>("rawJsonConfiguration").First();
            if (rawJsonField.value.Contains("This is a multi-line"))
            {
                rawJsonField.value = LocalizationManager.GetText("connector.manual_placeholder");
            }
            
            // Rebuild server button
            root.Query<Button>("btnRebuildServer").First().text = LocalizationManager.GetText("connector.rebuild_server");
            
            // Check log label
            var checkLogsLabels = root.Query<Label>().Where(l => l.text.Contains("Please check the logs")).ToList();
            foreach (var label in checkLogsLabels)
            {
                label.text = LocalizationManager.GetText("connector.check_logs");
            }

            // Model Configuration page tab content
            var modelConfigLabels = root.Query<Label>().Where(l => l.text == "AI Model Configuration").ToList();
            foreach (var label in modelConfigLabels)
            {
                label.text = LocalizationManager.GetText("model.title");
            }
            
            var aiProviderFoldouts = root.Query<Foldout>().Where(f => f.text == "AI Provider Settings").ToList();
            foreach (var foldout in aiProviderFoldouts)
            {
                foldout.text = LocalizationManager.GetText("model.provider_settings");
            }
            
            // Various settings label
            ApplyModelConfigLabels(root);
            
            // Settings page tab content  
            ApplySettingsLabels(root);
            
            // Operations page tab content
            ApplyOperationsLabels(root);
        }

        private void ApplyModelConfigLabels(VisualElement root)
        {
            // OpenAI, Gemini, Claude, Local settings
            var openaiLabels = root.Query<Foldout>().Where(f => f.text == "OpenAI Settings").ToList();
            foreach (var label in openaiLabels) label.text = LocalizationManager.GetText("model.openai_settings");
            
            var geminiLabels = root.Query<Foldout>().Where(f => f.text == "Gemini Settings").ToList();
            foreach (var label in geminiLabels) label.text = LocalizationManager.GetText("model.gemini_settings");
            
            var claudeLabels = root.Query<Foldout>().Where(f => f.text == "Claude Settings").ToList();
            foreach (var label in claudeLabels) label.text = LocalizationManager.GetText("model.claude_settings");
            
            var localLabels = root.Query<Foldout>().Where(f => f.text == "Local Settings").ToList();
            foreach (var label in localLabels) label.text = LocalizationManager.GetText("model.local_settings");
            
            var providerSelectionLabels = root.Query<Foldout>().Where(f => f.text == "Model Provider Selection").ToList();
            foreach (var label in providerSelectionLabels) label.text = LocalizationManager.GetText("model.provider_selection");
            
            var generalSettingsLabels = root.Query<Foldout>().Where(f => f.text == "General Settings").ToList();
            foreach (var label in generalSettingsLabels) label.text = LocalizationManager.GetText("model.general_settings");

            // Field label
            root.Query<TextField>("openaiApiKey").First().label = LocalizationManager.GetText("model.api_key");
            root.Query<TextField>("openaiModel").First().label = LocalizationManager.GetText("model.model");
            root.Query<TextField>("openaiBaseUrl").First().label = LocalizationManager.GetText("model.base_url");
            
            root.Query<TextField>("geminiApiKey").First().label = LocalizationManager.GetText("model.api_key");
            root.Query<TextField>("geminiModel").First().label = LocalizationManager.GetText("model.model");
            root.Query<TextField>("geminiBaseUrl").First().label = LocalizationManager.GetText("model.base_url");
            
            root.Query<TextField>("claudeApiKey").First().label = LocalizationManager.GetText("model.api_key");
            root.Query<TextField>("claudeModel").First().label = LocalizationManager.GetText("model.model");
            root.Query<TextField>("claudeBaseUrl").First().label = LocalizationManager.GetText("model.base_url");
            
            root.Query<TextField>("localApiUrl").First().label = LocalizationManager.GetText("model.api_url");
            root.Query<TextField>("localModel").First().label = LocalizationManager.GetText("model.model");
            
            root.Query<DropdownField>("visionModelProvider").First().label = LocalizationManager.GetText("model.vision_provider");
            root.Query<DropdownField>("textModelProvider").First().label = LocalizationManager.GetText("model.text_provider");
            root.Query<DropdownField>("codeModelProvider").First().label = LocalizationManager.GetText("model.code_provider");
            
            root.Query<IntegerField>("timeoutSeconds").First().label = LocalizationManager.GetText("model.timeout");
            root.Query<IntegerField>("maxTokens").First().label = LocalizationManager.GetText("model.max_tokens");
            
            root.Query<Button>("btnSaveConfig").First().text = LocalizationManager.GetText("model.save_config");
        }

        private void ApplySettingsLabels(VisualElement root)
        {
            var userPreferencesLabels = root.Query<Label>().Where(l => l.text == "User Preferences").ToList();
            foreach (var label in userPreferencesLabels)
            {
                label.text = LocalizationManager.GetText("settings.title");
            }
            
            var languageSettingsLabels = root.Query<Foldout>().Where(f => f.text == "Language Settings").ToList();
            foreach (var label in languageSettingsLabels) label.text = LocalizationManager.GetText("settings.language_settings");
            
            var themeSettingsLabels = root.Query<Foldout>().Where(f => f.text == "Theme Settings").ToList();
            foreach (var label in themeSettingsLabels) label.text = LocalizationManager.GetText("settings.theme_settings");
            
            root.Query<DropdownField>("languageSelector").First().label = LocalizationManager.GetText("settings.interface_language");
            root.Query<DropdownField>("themeSelector").First().label = LocalizationManager.GetText("settings.ui_theme");
            root.Query<Toggle>("autoRefreshToggle").First().label = LocalizationManager.GetText("settings.auto_refresh");
            
            var languageDescLabels = root.Query<Label>().Where(l => l.text.Contains("Select your preferred language")).ToList();
            foreach (var label in languageDescLabels)
            {
                label.text = LocalizationManager.GetText("settings.language_desc");
            }
            
            var themeDescLabels = root.Query<Label>().Where(l => l.text.Contains("Configure the appearance")).ToList();
            foreach (var label in themeDescLabels)
            {
                label.text = LocalizationManager.GetText("settings.theme_desc");
            }
            
            root.Query<Button>("btnSaveSettings").First().text = LocalizationManager.GetText("settings.save");
            root.Query<Button>("btnResetSettings").First().text = LocalizationManager.GetText("settings.reset");
        }

        private void ApplyOperationsLabels(VisualElement root)
        {
            var operationsPanelLabels = root.Query<Label>().Where(l => l.text == "Operations Panel").ToList();
            foreach (var label in operationsPanelLabels)
            {
                label.text = LocalizationManager.GetText("operations.title");
            }
            
            var undoStackLabels = root.Query<Foldout>().Where(f => f.text == "Undo Stack").ToList();
            foreach (var label in undoStackLabels) label.text = LocalizationManager.GetText("operations.undo_stack");
            
            root.Query<Button>("btnRefreshUndoStack").First().text = LocalizationManager.GetText("operations.refresh");
            root.Query<Button>("btnUndoLast").First().text = LocalizationManager.GetText("operations.undo");
            root.Query<Button>("btnRedoLast").First().text = LocalizationManager.GetText("operations.redo");
            root.Query<Button>("btnClearUndoStack").First().text = LocalizationManager.GetText("operations.clear_stack");
            
            var operationHistoryLabels = root.Query<Label>().Where(l => l.text == "Operation History").ToList();
            foreach (var label in operationHistoryLabels)
            {
                label.text = LocalizationManager.GetText("operations.history");
            }
            
            var noHistoryLabels = root.Query<Label>().Where(l => l.text == "No operation history").ToList();
            foreach (var label in noHistoryLabels)
            {
                label.text = LocalizationManager.GetText("operations.no_history");
            }
        }
    }
}