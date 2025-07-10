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
                }
                
                var configText = System.IO.File.ReadAllText(configPath);
                var config = JsonUtility.FromJson<AIConfigData>(configText);

                // Load provider settings
                SetFieldValue<TextField>(root, "openaiApiKey", config.openaiApiKey, "");
                SetFieldValue<TextField>(root, "openaiModel", config.openaiModel, "gpt-4o");
                SetFieldValue<TextField>(root, "openaiBaseUrl", config.openaiBaseUrl, "");

                SetFieldValue<TextField>(root, "geminiApiKey", config.geminiApiKey, "");
                SetFieldValue<TextField>(root, "geminiModel", config.geminiModel, "gemini-pro");
                SetFieldValue<TextField>(root, "geminiBaseUrl", config.geminiBaseUrl, "");

                SetFieldValue<TextField>(root, "claudeApiKey", config.claudeApiKey, "");
                SetFieldValue<TextField>(root, "claudeModel", config.claudeModel, "claude-3-sonnet-20240229");
                SetFieldValue<TextField>(root, "claudeBaseUrl", config.claudeBaseUrl, "");

                SetFieldValue<TextField>(root, "localApiUrl", config.localApiUrl, "");
                SetFieldValue<TextField>(root, "localModel", config.localModel, "llava");

                // Model provider settings
                var providerOptions = new System.Collections.Generic.List<string> { "openai", "claude", "gemini", "local" };
                
                SetupProviderDropdown(root, "visionModelProvider", providerOptions, config.visionModelProvider ?? "openai");
                SetupProviderDropdown(root, "textModelProvider", providerOptions, config.textModelProvider ?? "openai");
                SetupProviderDropdown(root, "codeModelProvider", providerOptions, config.codeModelProvider ?? "claude");

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
            SaveAIConfigurationInternal(root, "[AI Connector] AI configuration updated");
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
            SaveAIConfigurationInternal(root, "[AI Connector] Model provider changed");
        }

        /// <summary>
        /// Internal method to handle AI configuration saving with unified logic
        /// </summary>
        private void SaveAIConfigurationInternal(VisualElement root, string changeMessage)
        {
            try
            {
                var config = CollectAIConfigurationFromUI(root);

                // 1. Save configuration file in Unity package
                var unityConfigPath = "Packages/com.miao.unity.mcp/Config/AI_Config.json";
                var jsonText = JsonUtility.ToJson(config, true);
                System.IO.File.WriteAllText(unityConfigPath, jsonText);
                
                // 2. Update server environment
                UpdateServerConfiguration(config);
                
                SaveChanges(changeMessage);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save AI configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Collect AI configuration data from UI elements
        /// </summary>
        private AIConfigData CollectAIConfigurationFromUI(VisualElement root)
        {
            return new AIConfigData
            {
                openaiApiKey = GetFieldValue<TextField>(root, "openaiApiKey"),
                openaiModel = GetFieldValue<TextField>(root, "openaiModel"),
                openaiBaseUrl = GetFieldValue<TextField>(root, "openaiBaseUrl"),
                
                geminiApiKey = GetFieldValue<TextField>(root, "geminiApiKey"),
                geminiModel = GetFieldValue<TextField>(root, "geminiModel"),
                geminiBaseUrl = GetFieldValue<TextField>(root, "geminiBaseUrl"),
                
                claudeApiKey = GetFieldValue<TextField>(root, "claudeApiKey"),
                claudeModel = GetFieldValue<TextField>(root, "claudeModel"),
                claudeBaseUrl = GetFieldValue<TextField>(root, "claudeBaseUrl"),
                
                localApiUrl = GetFieldValue<TextField>(root, "localApiUrl"),
                localModel = GetFieldValue<TextField>(root, "localModel"),
                
                visionModelProvider = GetFieldValue<DropdownField>(root, "visionModelProvider"),
                textModelProvider = GetFieldValue<DropdownField>(root, "textModelProvider"),
                codeModelProvider = GetFieldValue<DropdownField>(root, "codeModelProvider"),
                
                timeoutSeconds = root.Query<IntegerField>("timeoutSeconds").First().value,
                maxTokens = root.Query<IntegerField>("maxTokens").First().value
            };
        }

        /// <summary>
        /// Generic helper method to get field values
        /// </summary>
        private string GetFieldValue<T>(VisualElement root, string fieldName) where T : BaseField<string>
        {
            return root.Query<T>(fieldName).First().value;
        }

        /// <summary>
        /// Generic helper method to set field values with default fallback
        /// </summary>
        private void SetFieldValue<T>(VisualElement root, string fieldName, string value, string defaultValue) where T : BaseField<string>
        {
            root.Query<T>(fieldName).First().value = value ?? defaultValue;
        }

        /// <summary>
        /// Setup provider dropdown with choices, value and callback
        /// </summary>
        private void SetupProviderDropdown(VisualElement root, string fieldName, List<string> choices, string value)
        {
            var dropdown = root.Query<DropdownField>(fieldName).First();
            dropdown.choices = choices;
            dropdown.value = value;
            dropdown.RegisterValueChangedCallback(evt => SaveAIConfigurationImmediate(root));
        }

        /// <summary>
        /// Set element text using localization key
        /// </summary>
        private void SetElementText<T>(VisualElement root, string elementName, string localizationKey) where T : TextElement
        {
            root.Query<T>(elementName).First().text = LocalizationManager.GetText(localizationKey);
        }

        /// <summary>
        /// Set element label using localization key
        /// </summary>
        private void SetElementLabel(VisualElement root, string elementName, string localizationKey)
        {
            var element = root.Query(elementName).First();
            
            // Use reflection to set label property since different field types have different generic parameters
            var labelProperty = element.GetType().GetProperty("label");
            if (labelProperty != null && labelProperty.CanWrite)
            {
                labelProperty.SetValue(element, LocalizationManager.GetText(localizationKey));
            }
        }

        /// <summary>
        /// Set element text by finding elements with specific content
        /// </summary>
        private void SetElementTextByContent<T>(VisualElement root, string currentText, string localizationKey) where T : VisualElement
        {
            var elements = root.Query<T>().ToList();
            foreach (var element in elements)
            {
                var currentElementText = GetElementText(element);
                if (currentElementText == currentText)
                {
                    SetElementText(element, LocalizationManager.GetText(localizationKey));
                }
            }
        }

        /// <summary>
        /// Set element text by finding elements containing specific text
        /// </summary>
        private void SetElementTextByContentContains<T>(VisualElement root, string containsText, string localizationKey) where T : VisualElement
        {
            var elements = root.Query<T>().ToList();
            foreach (var element in elements)
            {
                var currentElementText = GetElementText(element);
                if (currentElementText != null && currentElementText.Contains(containsText))
                {
                    SetElementText(element, LocalizationManager.GetText(localizationKey));
                }
            }
        }

        /// <summary>
        /// Get text from element using reflection for different element types
        /// </summary>
        private string GetElementText(VisualElement element)
        {
            var textProperty = element.GetType().GetProperty("text");
            return textProperty?.GetValue(element) as string;
        }

        /// <summary>
        /// Set text on element using reflection for different element types
        /// </summary>
        private void SetElementText(VisualElement element, string text)
        {
            var textProperty = element.GetType().GetProperty("text");
            if (textProperty != null && textProperty.CanWrite)
            {
                textProperty.SetValue(element, text);
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
            SetElementText<Button>(root, "TabConnector", "tab.connector");
            SetElementText<Button>(root, "TabModelConfig", "tab.modelconfig");
            SetElementText<Button>(root, "TabUndoHistory", "tab.operations");
            SetElementText<Button>(root, "TabSettings", "tab.settings");

            // Connector page tab content
            SetElementText<Label>(root, "labelSettings", "connector.title");
            SetElementLabel(root, "dropdownLogLevel", "connector.loglevel");
            SetElementTextByContent<Label>(root, "Connect to MCP server", "connector.connect_server");
            SetElementLabel(root, "InputServerURL", "connector.server_url");
            SetElementTextByContent<Foldout>(root, "Information", "connector.information");
            
            // Update server information and client configuration
            SetElementTextByContentContains<Label>(root, "Usually the server is hosted locally", "connector.info_desc");
            SetElementTextByContent<Label>(root, "Configure MCP Client", "connector.configure_client");
            SetElementTextByContentContains<Label>(root, "At least one client", "connector.client_desc");
            SetElementText<Button>(root, "btnConfigure", "connector.configure");
            SetElementTextByContent<Label>(root, "Not configured", "connector.not_configured");
            
            // Manual configuration and server management
            SetElementTextByContent<Label>(root, "Manual configuration", "connector.manual_config");
            SetElementTextByContentContains<Label>(root, "Copy paste the json", "connector.manual_desc");
            SetElementText<Button>(root, "btnRebuildServer", "connector.rebuild_server");
            SetElementTextByContentContains<Label>(root, "Please check the logs", "connector.check_logs");
            
            // Update manual configuration placeholder
            var rawJsonField = root.Query<TextField>("rawJsonConfiguration").First();
            if (rawJsonField.value.Contains("This is a multi-line"))
            {
                rawJsonField.value = LocalizationManager.GetText("connector.manual_placeholder");
            }

            // Model Configuration page tab content
            SetElementTextByContent<Label>(root, "AI Model Configuration", "model.title");
            SetElementTextByContent<Foldout>(root, "AI Provider Settings", "model.provider_settings");
            
            // Various settings label
            ApplyModelConfigLabels(root);
            
            // Settings page tab content  
            ApplySettingsLabels(root);
            
            // Operations page tab content
            ApplyOperationsLabels(root);
        }

        private void ApplyModelConfigLabels(VisualElement root)
        {
            // Provider settings foldouts
            SetElementTextByContent<Foldout>(root, "OpenAI Settings", "model.openai_settings");
            SetElementTextByContent<Foldout>(root, "Gemini Settings", "model.gemini_settings");
            SetElementTextByContent<Foldout>(root, "Claude Settings", "model.claude_settings");
            SetElementTextByContent<Foldout>(root, "Local Settings", "model.local_settings");
            SetElementTextByContent<Foldout>(root, "Model Provider Selection", "model.provider_selection");
            SetElementTextByContent<Foldout>(root, "General Settings", "model.general_settings");

            // Provider field labels
            SetProviderFieldLabels(root, "openai", "model.api_key", "model.model", "model.base_url");
            SetProviderFieldLabels(root, "gemini", "model.api_key", "model.model", "model.base_url");
            SetProviderFieldLabels(root, "claude", "model.api_key", "model.model", "model.base_url");
            
            SetElementLabel(root, "localApiUrl", "model.api_url");
            SetElementLabel(root, "localModel", "model.model");
            
            // Model provider dropdowns
            SetElementLabel(root, "visionModelProvider", "model.vision_provider");
            SetElementLabel(root, "textModelProvider", "model.text_provider");
            SetElementLabel(root, "codeModelProvider", "model.code_provider");
            
            // General settings
            SetElementLabel(root, "timeoutSeconds", "model.timeout");
            SetElementLabel(root, "maxTokens", "model.max_tokens");
            SetElementText<Button>(root, "btnSaveConfig", "model.save_config");
        }

        /// <summary>
        /// Set labels for provider field triplet (API key, model, base URL)
        /// </summary>
        private void SetProviderFieldLabels(VisualElement root, string provider, string apiKeyLoc, string modelLoc, string baseUrlLoc)
        {
            SetElementLabel(root, $"{provider}ApiKey", apiKeyLoc);
            SetElementLabel(root, $"{provider}Model", modelLoc);
            SetElementLabel(root, $"{provider}BaseUrl", baseUrlLoc);
        }

        private void ApplySettingsLabels(VisualElement root)
        {
            // Settings sections
            SetElementTextByContent<Label>(root, "User Preferences", "settings.title");
            SetElementTextByContent<Foldout>(root, "Language Settings", "settings.language_settings");
            SetElementTextByContent<Foldout>(root, "Theme Settings", "settings.theme_settings");
            
            // Settings controls
            SetElementLabel(root, "languageSelector", "settings.interface_language");
            SetElementLabel(root, "themeSelector", "settings.ui_theme");
            SetElementLabel(root, "autoRefreshToggle", "settings.auto_refresh");
            
            // Description texts
            SetElementTextByContentContains<Label>(root, "Select your preferred language", "settings.language_desc");
            SetElementTextByContentContains<Label>(root, "Configure the appearance", "settings.theme_desc");
            
            // Action buttons
            SetElementText<Button>(root, "btnSaveSettings", "settings.save");
            SetElementText<Button>(root, "btnResetSettings", "settings.reset");
        }

        private void ApplyOperationsLabels(VisualElement root)
        {
            // Operations sections
            SetElementTextByContent<Label>(root, "Operations Panel", "operations.title");
            SetElementTextByContent<Foldout>(root, "Undo Stack", "operations.undo_stack");
            SetElementTextByContent<Label>(root, "Operation History", "operations.history");
            SetElementTextByContent<Label>(root, "No operation history", "operations.no_history");
            
            // Operation buttons
            SetElementText<Button>(root, "btnRefreshUndoStack", "operations.refresh");
            SetElementText<Button>(root, "btnUndoLast", "operations.undo");
            SetElementText<Button>(root, "btnRedoLast", "operations.redo");
            SetElementText<Button>(root, "btnClearUndoStack", "operations.clear_stack");
        }
    }
}