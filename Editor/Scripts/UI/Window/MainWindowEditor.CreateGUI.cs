using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
                // Connection status will update automatically through Observable
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
            var logoElement = root.Query<VisualElement>("imgLogo").First();

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

                    // Update logo based on connection state
                    var isConnected = connectionState == HubConnectionState.Connected && keepConnected;
                    if (logoElement != null)
                    {
                        var logoPath = isConnected 
                            ? "Packages/com.MiAO.Unity.MCP/Editor/Gizmos/512_logo_conneted.png"
                            : "Packages/com.MiAO.Unity.MCP/Editor/Gizmos/logo_512.png";
                        var logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
                        if (logoTexture != null)
                        {
                            logoElement.style.backgroundImage = new StyleBackground(logoTexture);
                        }
                    }

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

            // Configure MCP Client - Draggable Configuration
            // -----------------------------------------------------------------
            SetupDraggableClientConfiguration(root);

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
            var element = root.Query<T>(elementName).First();
            if (element != null)
            {
                element.text = LocalizationManager.GetText(localizationKey);
            }
        }

        /// <summary>
        /// Set element label using localization key
        /// </summary>
        private void SetElementLabel(VisualElement root, string elementName, string localizationKey)
        {
            var element = root.Query(elementName).First();
            if (element == null) return;
            
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
        /// Apply localized text to UXML elements
        /// </summary>
        private void ApplyUILocalization(VisualElement root)
        {
            // Apply tab page text
            SetElementText<Button>(root, "TabConnector", "tab.connector");
            SetElementText<Button>(root, "TabModelConfig", "tab.modelconfig");
            SetElementText<Button>(root, "TabUserInput", "tab.userinput");
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
            SetElementTextByContent<Button>(root, "Configure", "connector.configure");
            SetElementTextByContent<Label>(root, "Not configured", "connector.not_configured");
            
            // Drag and drop related elements
            SetElementTextByContentContains<Label>(root, "Pinned Clients", "connector.pinned_clients");
            SetElementTextByContentContains<Foldout>(root, "More Clients", "connector.more_clients");
            SetElementTextByContentContains<Label>(root, "Drag any client", "connector.drag_instruction");
            
            // Manual configuration and server management
            SetElementTextByContent<Label>(root, "Manual configuration", "connector.manual_config");
            SetElementTextByContentContains<Label>(root, "Copy paste the json", "connector.manual_desc");
            SetElementText<Button>(root, "btnRebuildServer", "connector.rebuild_server");
            SetElementTextByContentContains<Label>(root, "Please check the logs", "connector.check_logs");
            
            // Update manual configuration placeholder
            var rawJsonField = root.Query<TextField>("rawJsonConfiguration").First();
            if (rawJsonField != null && rawJsonField.value.Contains("This is a multi-line"))
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

        /// <summary>
        /// Setup draggable client configuration interface
        /// </summary>
        private void SetupDraggableClientConfiguration(VisualElement root)
        {
            // Get main areas
            var pinnedArea = root.Query<VisualElement>("PinnedClientsArea").First();
            var moreFoldout = root.Query<Foldout>("MoreClientsFoldout").First();
            var moreArea = root.Query<VisualElement>("MoreClientsArea").First();

            if (pinnedArea == null || moreFoldout == null || moreArea == null)
            {
                Debug.LogWarning("[MCP] Draggable client configuration UI elements are missing.");
                return;
            }

            // Define client configuration information
            var clientConfigs = new Dictionary<string, ClientConfig>
            {
                { "Cursor", new ClientConfig("Cursor", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json"), "mcpServers") },
                { "Claude", new ClientConfig("Claude Desktop", GetClaudeDesktopPath(), "mcpServers") },
                { "VSCode", new ClientConfig("VS Code", Path.Combine(".vscode", "mcp.json"), "servers") },
                { "VisualStudio", new ClientConfig("Visual Studio", GetVisualStudioConfigPath(VisualStudioConfigLocation.Global), "servers") },
                { "Augment", new ClientConfig("Augment (VS Code)", GetVSCodeSettingsPath(), "augment.advanced.mcpServers") },
                { "Windsurf", new ClientConfig("Windsurf", GetWindsurfSettingsPath(), "mcpServers") },
                { "Cline", new ClientConfig("Cline", GetClineSettingsPath(), "mcpServers") }
            };

            // Setup each client item
            SetupClientItem(root, "ClientItem-Cursor", clientConfigs["Cursor"]);
            SetupClientItem(root, "ClientItem-Claude", clientConfigs["Claude"]);
            SetupClientItem(root, "ClientItem-VSCode", clientConfigs["VSCode"]);
            SetupClientItem(root, "ClientItem-VisualStudio", clientConfigs["VisualStudio"]);
            SetupClientItem(root, "ClientItem-Augment", clientConfigs["Augment"]);
            SetupClientItem(root, "ClientItem-Windsurf", clientConfigs["Windsurf"]);
            SetupClientItem(root, "ClientItem-Cline", clientConfigs["Cline"]);

            // Setup drag and drop functionality
            SetupDragAndDrop(pinnedArea, moreArea, moreFoldout);
        }

        /// <summary>
        /// Client configuration information
        /// </summary>
        private class ClientConfig
        {
            public string DisplayName { get; }
            public string ConfigPath { get; }
            public string BodyName { get; }

            public ClientConfig(string displayName, string configPath, string bodyName)
            {
                DisplayName = displayName;
                ConfigPath = configPath;
                BodyName = bodyName;
            }
        }

        /// <summary>
        /// Setup individual client item
        /// </summary>
        private void SetupClientItem(VisualElement root, string itemName, ClientConfig config)
        {
            var clientItem = root.Query<VisualElement>(itemName).First();
            if (clientItem == null) return;

            var statusCircle = clientItem.Query<VisualElement>("configureStatusCircle").First();
            var statusText = clientItem.Query<Label>("configureStatusText").First();
            var configureBtn = clientItem.Query<Button>("btnConfigure").First();

            if (statusCircle == null || statusText == null || configureBtn == null) return;

            // Check configuration status
            var isConfigured = IsMcpClientConfigured(config.ConfigPath, config.BodyName);
            UpdateClientStatus(statusCircle, statusText, configureBtn, isConfigured);

            // Configure button click event
            configureBtn.clicked += () =>
            {
                var success = ConfigureMcpClient(config.ConfigPath, config.BodyName);
                UpdateClientStatus(statusCircle, statusText, configureBtn, success);
                
                if (success)
                {
                    Debug.Log($"[MCP] {config.DisplayName} configuration completed successfully.");
                }
                else
                {
                    Debug.LogError($"[MCP] Failed to configure {config.DisplayName}.");
                }
            };

            // Special handling for Visual Studio
            if (itemName == "ClientItem-VisualStudio")
            {
                var vsConfigLocation = clientItem.Query<EnumField>("vsConfigLocation").First();
                if (vsConfigLocation != null)
                {
                    vsConfigLocation.Init(VisualStudioConfigLocation.Global);
                    vsConfigLocation.value = VisualStudioConfigLocation.Global;
                    
                    vsConfigLocation.RegisterValueChangedCallback(evt =>
                    {
                        var newPath = GetVisualStudioConfigPath((VisualStudioConfigLocation)evt.newValue);
                        var newIsConfigured = IsMcpClientConfigured(newPath, config.BodyName);
                        UpdateClientStatus(statusCircle, statusText, configureBtn, newIsConfigured);
                    });
                }
            }
        }

        #region Drag-related constants
        private static readonly Color DefaultBorderColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        private static readonly Color HoverBorderColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        private static readonly Color HoverBackgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.1f);
        private static readonly Color DragBackgroundColor = new Color(0.4f, 0.6f, 0.9f, 0.35f);
        private static readonly Color DragBorderColor = new Color(0.2f, 0.4f, 1f, 0.8f);
        private static readonly Color DefaultDotColor = new Color(0.47f, 0.47f, 0.47f, 0.8f);
        private static readonly Color HoverDotColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color DragDotColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color DropZoneHighlightColor = new Color(0.7f, 0.7f, 0.7f, 0.25f);
        private static readonly Color HighlightBorderColor = new Color(0.8f, 0.8f, 0.8f, 0.7f);
        private static readonly Color DropZoneNormalHighlight = new Color(0.6f, 0.6f, 0.6f, 0.15f);
        private static readonly Color DropZoneNormalBorder = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        private static readonly Color DropZoneDefaultBorder = new Color(0.4f, 0.4f, 0.4f, 0.3f);
        private static readonly Color DropZoneResetColor = new Color(0.6f, 0.6f, 0.6f, 0.08f);
        #endregion

        /// <summary>
        /// Setup drag and drop functionality
        /// </summary>
        private void SetupDragAndDrop(VisualElement pinnedArea, VisualElement moreArea, Foldout moreFoldout)
        {
            
            // Get all draggable client items
            var allDraggableItems = new List<VisualElement>();
            
            // Get from pinned area
            allDraggableItems.AddRange(pinnedArea.Query<VisualElement>(className: "draggable-client-item").ToList());
            
            // Get from More area
            allDraggableItems.AddRange(moreArea.Query<VisualElement>(className: "draggable-client-item").ToList());

            foreach (var item in allDraggableItems)
            {
                // Ensure all items have consistent initial state
                ResetItemAppearance(item);
                
                // Set drag handle tooltip localization
                var dragHandle = item.Query<VisualElement>(className: "drag-handle").First();
                if (dragHandle != null)
                {
                    dragHandle.tooltip = LocalizationManager.GetText("connector.drag_tooltip");
                }
                
                SetupItemDragHandlers(item, pinnedArea, moreArea, moreFoldout);
            }
        }

        /// <summary>
        /// Setup drag handlers for individual item
        /// </summary>
        private void SetupItemDragHandlers(VisualElement item, VisualElement pinnedArea, VisualElement moreArea, Foldout moreFoldout)
        {
            var dragHandle = item.Query<VisualElement>(className: "drag-handle").First();
            if (dragHandle == null) return;

            var isDragging = false;
            var dragStartPosition = Vector2.zero;
            var originalParent = item.parent;

            // Add hover effects
            dragHandle.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!isDragging)
                {
                    // Hover effect
                    UpdateDragHandleDots(dragHandle, new StyleColor(HoverDotColor), new Scale(new Vector2(1.15f, 1.15f)));
                    dragHandle.style.translate = new Translate(0, -1, 0);
                    // Set visual state when hovering
                    SetItemVisualState(item, HoverBackgroundColor, HoverBorderColor);
                }
            });

            dragHandle.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (!isDragging)
                {
                    // Restore default state
                    UpdateDragHandleDots(dragHandle, new StyleColor(DefaultDotColor), new Scale(Vector2.one));
                    dragHandle.style.translate = new Translate(0, 0, 0);
                    item.style.backgroundColor = StyleKeyword.Initial;
                    SetAllBorderColors(item, new StyleColor(DefaultBorderColor));
                }
            });

            // Mouse down
            dragHandle.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // Left mouse button
                {
                    isDragging = true;
                    dragStartPosition = evt.localMousePosition;
                    originalParent = item.parent;
                    dragHandle.CaptureMouse();
                    
                    // Drag start effect
                    UpdateDragHandleDots(dragHandle, new StyleColor(DragDotColor), new Scale(new Vector2(1.3f, 1.3f)));
                    
                    // Drag visual effect
                    item.style.opacity = 0.8f;
                    SetItemVisualState(item, DragBackgroundColor, DragBorderColor, 2);
                    
                    // Highlight all possible drop zones
                    HighlightDropZones(pinnedArea, moreArea, moreFoldout, true);
                    
                    evt.StopPropagation();
                }
            });

            // Mouse drag - Lower threshold for better sensitivity
            dragHandle.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (isDragging)
                {
                    var delta = evt.localMousePosition - dragStartPosition;
                    if (delta.magnitude > 3) // Lower drag threshold from 10 to 3
                    {
                        // Enhanced drag visual effect
                        item.style.opacity = 0.7f;
                        item.style.translate = new Translate(delta.x * 0.8f, delta.y * 0.8f, 0);
                        
                        // Dynamically highlight target areas
                        UpdateDropZoneHighlight(evt.mousePosition, pinnedArea, moreArea, moreFoldout, item);
                    }
                }
            });

            // Mouse release
            dragHandle.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    dragHandle.ReleaseMouse();
                    
                    // Restore appearance
                    ResetItemAppearance(item);
                    
                    // Clear area highlighting
                    HighlightDropZones(pinnedArea, moreArea, moreFoldout, false);

                    // Check if need to move to other areas
                    var mouseGlobalPos = evt.mousePosition;
                    var moved = false;
                    
                    if (IsPointInElement(mouseGlobalPos, pinnedArea) && originalParent != pinnedArea)
                    {
                        MoveItemToArea(item, pinnedArea);
                        moved = true;
                        Debug.Log($"[MCP] {LocalizationManager.GetText("connector.notification_pinned")}");
                    }
                    else if (IsDropTargetForMoreArea(mouseGlobalPos, moreArea, moreFoldout) && originalParent != moreArea)
                    {
                        // If fold area is collapsed, expand it first
                        if (!moreFoldout.value)
                        {
                            moreFoldout.value = true;
                        }
                        MoveItemToArea(item, moreArea);
                        moved = true;
                        Debug.Log($"[MCP] {LocalizationManager.GetText("connector.notification_moved")}");
                    }
                    
                    if (!moved && originalParent != item.parent)
                    {
                        // If accidentally detached, restore to original position
                        item.RemoveFromHierarchy();
                        originalParent.Add(item);
                    }
                    
                    evt.StopPropagation();
                }
            });
        }

        /// <summary>
        /// Reset item appearance
        /// </summary>
        private void ResetItemAppearance(VisualElement item)
        {
            item.style.opacity = 1f;
            item.style.backgroundColor = StyleKeyword.Initial;
            item.style.translate = new Translate(0, 0, 0);
            SetAllBorderColors(item, new StyleColor(DefaultBorderColor));
            SetAllBorderWidths(item, 1);
            
            // Reset drag handle dot state
            var dragHandle = item.Query<VisualElement>(className: "drag-handle").First();
            if (dragHandle != null)
            {
                UpdateDragHandleDots(dragHandle, new StyleColor(DefaultDotColor), new Scale(Vector2.one));
            }
        }

        /// <summary>
        /// Check if it can be used as a drop target for More area
        /// </summary>
        private bool IsDropTargetForMoreArea(Vector2 mousePosition, VisualElement moreArea, Foldout moreFoldout)
        {
            // If fold area is expanded, check if mouse is in moreArea
            if (moreFoldout.value)
            {
                return IsPointInElement(mousePosition, moreArea);
            }
            else
            {
                // If fold area is collapsed, check if mouse is in fold area title bar
                return IsPointInElement(mousePosition, moreFoldout);
            }
        }

        /// <summary>
        /// Highlight drop zones
        /// </summary>
        private void HighlightDropZones(VisualElement pinnedArea, VisualElement moreArea, Foldout moreFoldout, bool highlight)
        {
            var highlightColor = highlight ? new StyleColor(DropZoneNormalHighlight) : StyleKeyword.Initial;
            var borderColor = highlight ? new StyleColor(DropZoneNormalBorder) : new StyleColor(DropZoneDefaultBorder);

            // Set pinned area
            SetDropZoneStyle(pinnedArea, highlightColor, borderColor);
            
            // Set more area based on fold state
            var targetElement = moreFoldout.value ? moreArea : moreFoldout;
            SetDropZoneStyle(targetElement, highlightColor, borderColor);
        }

        /// <summary>
        /// Set drop zone style
        /// </summary>
        private void SetDropZoneStyle(VisualElement element, StyleColor backgroundColor, StyleColor borderColor)
        {
            element.style.backgroundColor = backgroundColor;
            SetAllBorderColors(element, borderColor);
        }

        /// <summary>
        /// Dynamically update drop zone highlighting
        /// </summary>
        private void UpdateDropZoneHighlight(Vector2 mousePosition, VisualElement pinnedArea, VisualElement moreArea, Foldout moreFoldout, VisualElement draggedItem)
        {
            // Reset all areas
            pinnedArea.style.backgroundColor = new StyleColor(DropZoneResetColor);
            
            // Reset More area based on fold state
            var targetResetElement = moreFoldout.value ? moreArea : moreFoldout;
            targetResetElement.style.backgroundColor = new StyleColor(DropZoneResetColor);
            
            // Highlight currently hovered area
            if (IsPointInElement(mousePosition, pinnedArea) && draggedItem.parent != pinnedArea)
            {
                pinnedArea.style.backgroundColor = new StyleColor(DropZoneHighlightColor);
                SetAllBorderColors(pinnedArea, new StyleColor(HighlightBorderColor));
            }
            else if (IsDropTargetForMoreArea(mousePosition, moreArea, moreFoldout) && draggedItem.parent != moreArea)
            {
                if (moreFoldout.value)
                {
                    // Expanded state: highlight moreArea
                    moreArea.style.backgroundColor = new StyleColor(DropZoneHighlightColor);
                    SetAllBorderColors(moreArea, new StyleColor(HighlightBorderColor));
                }
                else
                {
                    // Collapsed state: highlight fold area title bar
                    moreFoldout.style.backgroundColor = new StyleColor(DropZoneHighlightColor);
                    SetAllBorderColors(moreFoldout, new StyleColor(HighlightBorderColor));
                }
            }
        }



        /// <summary>
        /// Set all border colors of element
        /// </summary>
        private void SetAllBorderColors(VisualElement element, StyleColor color)
        {
            element.style.borderBottomColor = color;
            element.style.borderTopColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        /// <summary>
        /// Set visual state of item (background color, border color, etc.)
        /// </summary>
        private void SetItemVisualState(VisualElement item, Color backgroundColor, Color borderColor, int borderWidth = 1)
        {
            item.style.backgroundColor = new StyleColor(backgroundColor);
            SetAllBorderColors(item, new StyleColor(borderColor));
            SetAllBorderWidths(item, borderWidth);
        }

        /// <summary>
        /// Set all border widths of element
        /// </summary>
        private void SetAllBorderWidths(VisualElement element, int width)
        {
            element.style.borderBottomWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
        }

        /// <summary>
        /// Recursively update styles of all dots in drag handle (handling new 3x2 vertical layout)
        /// </summary>
        private void UpdateDragHandleDots(VisualElement dragHandle, StyleColor color, Scale scale)
        {
            // Recursively find all deepest dots (elements with no children)
            foreach (var child in dragHandle.Children())
            {
                if (child.childCount > 0)
                {
                    UpdateDragHandleDots(child, color, scale);
                }
                else
                {
                    child.style.backgroundColor = color;
                    child.style.scale = scale;
                }
            }
        }

        /// <summary>
        /// Check if point is inside element
        /// </summary>
        private bool IsPointInElement(Vector2 point, VisualElement element)
        {
            var worldBound = element.worldBound;
            return worldBound.Contains(point);
        }

        /// <summary>
        /// Move item to specified area
        /// </summary>
        private void MoveItemToArea(VisualElement item, VisualElement targetArea)
        {
            item.RemoveFromHierarchy();
            targetArea.Add(item);
        }

        /// <summary>
        /// Get Claude Desktop configuration path
        /// </summary>
        private string GetClaudeDesktopPath()
        {
#if UNITY_EDITOR_WIN
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Claude", "claude_desktop_config.json");
#endif
        }
    }
}