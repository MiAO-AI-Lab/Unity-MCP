#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Editor.Extensions;
using com.MiAO.Unity.MCP.Editor.Common;
using Consts = com.MiAO.Unity.MCP.Common.Consts;
using Debug = UnityEngine.Debug;

namespace com.MiAO.Unity.MCP.Editor.UI
{
    /// <summary>
    /// MCP Hub Settings window for configuring Hub preferences and extension settings
    /// Provides configuration options for MCP server, extensions, and development settings
    /// </summary>
    public class McpHubSettingsWindow : EditorWindow
    {
        private const int MIN_WIDTH = 500;
        private const int MIN_HEIGHT = 600;
        
        private const string STYLE_PATH = "McpHubSettingsWindow";
        
        // Setting keys for EditorPrefs
        private const string KEY_AUTO_START_SERVER = "mcp-hub:auto-start-server";
        private const string KEY_AUTO_INSTALL_DEPENDENCIES = "mcp-hub:auto-install-dependencies";
        private const string KEY_CHECK_UPDATES_ON_START = "mcp-hub:check-updates-on-start";
        private const string KEY_ENABLE_DEBUG_LOGGING = "mcp-hub:enable-debug-logging";
        private const string KEY_EXTENSION_UPDATE_CHANNEL = "mcp-hub:extension-update-channel";
        private const string KEY_MCP_SERVER_PORT = "mcp-hub:mcp-server-port";
        private const string KEY_MCP_SERVER_HOST = "mcp-hub:mcp-server-host";
        
        // Language and theme setting keys (moved from MainWindow)
        private const string KEY_LANGUAGE = "MCP.Settings.Language";
        private const string KEY_THEME = "MCP.Settings.Theme";

        // Default values
        private const bool DEFAULT_AUTO_START_SERVER = true;
        private const bool DEFAULT_AUTO_INSTALL_DEPENDENCIES = true;
        private const bool DEFAULT_CHECK_UPDATES_ON_START = true;
        private const bool DEFAULT_ENABLE_DEBUG_LOGGING = false;
        private const string DEFAULT_UPDATE_CHANNEL = "stable";
        private const int DEFAULT_SERVER_PORT = 8080;
        private const string DEFAULT_SERVER_HOST = "localhost";

        private static McpHubSettingsWindow s_Instance;

        // UI Elements
        private VisualElement m_Root;
        private VisualElement m_Header;
        private VisualElement m_Content;
        private VisualElement m_Footer;

        // General Settings
        private Toggle m_AutoStartServerToggle;
        private Toggle m_AutoInstallDependenciesToggle;
        private Toggle m_CheckUpdatesOnStartToggle;
        private Toggle m_EnableDebugLoggingToggle;

        // Server Settings
        private TextField m_ServerHostField;
        private IntegerField m_ServerPortField;

        // Extension Settings
        private PopupField<string> m_UpdateChannelDropdown;
        private Button m_RefreshExtensionsButton;
        private Button m_ResetExtensionCacheButton;

        // Language and Theme Settings (moved from MainWindow)
        private DropdownField m_LanguageSelector;
        private DropdownField m_ThemeSelector;

        // Action buttons
        private Button m_ApplyButton;
        private Button m_ResetButton;
        private Button m_CloseButton;

        // Status
        private Label m_StatusLabel;

        public static McpHubSettingsWindow Instance => s_Instance;

        /// <summary>
        /// Opens the settings window
        /// </summary>
        public static void ShowWindow()
        {
            if (s_Instance != null) 
            {
                s_Instance.Focus();
                return;
            }
            
            s_Instance = GetWindow<McpHubSettingsWindow>(true, LocalizationManager.GetText("hubsettings.title"), true);
            s_Instance.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            s_Instance.Show();
        }

        private void OnEnable()
        {
            s_Instance = this;
            titleContent = new GUIContent(LocalizationManager.GetText("hubsettings.title"), LocalizationManager.GetText("hubsettings.window_desc"));
            
            // Subscribe to language change events
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            
            CreateUIElements();
            LoadSettings();
            UpdateLocalizedTexts();
        }

        private void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
            
            // Unsubscribe from language change events
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        /// <summary>
        /// Handle language change events
        /// </summary>
        private void OnLanguageChanged(LocalizationManager.Language newLanguage)
        {
            UpdateLocalizedTexts();
        }

        /// <summary>
        /// Update all localized texts
        /// </summary>
        private void UpdateLocalizedTexts()
        {
            try
            {
                // Update window title
                titleContent = new GUIContent(LocalizationManager.GetText("hubsettings.title"), LocalizationManager.GetText("hubsettings.window_desc"));
                
                // Update selector choices
                UpdateSelectorChoices();
                
                // Recreate UI elements to update all text
                CreateUIElements();
                LoadSettings();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to update localized texts: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the UI elements for the settings window
        /// </summary>
        private void CreateUIElements()
        {
            m_Root = rootVisualElement;
            m_Root.Clear();
            
            // Load styles
            LoadStyles();
            
            // Create layout
            CreateHeader();
            CreateContent();
            CreateFooter();
        }

        /// <summary>
        /// Loads custom styles for the window
        /// </summary>
        private void LoadStyles()
        {
            try
            {
                var styleSheet = Resources.Load<StyleSheet>(STYLE_PATH);
                if (styleSheet != null)
                {
                    m_Root.styleSheets.Add(styleSheet);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Consts.Log.Tag} Could not load settings window styles: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the header section with title and description
        /// </summary>
        private void CreateHeader()
        {
            m_Header = new VisualElement();
            m_Header.style.paddingTop = 15;
            m_Header.style.paddingBottom = 15;
            m_Header.style.paddingLeft = 20;
            m_Header.style.paddingRight = 20;
            m_Header.style.borderBottomWidth = 1;
            m_Header.style.borderBottomColor = Color.gray;
            
            var title = new Label(LocalizationManager.GetText("hubsettings.title"));
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            
            var description = new Label(LocalizationManager.GetText("hubsettings.window_desc"));
            description.style.fontSize = 12;
            description.style.color = new Color(0.7f, 0.7f, 0.7f);
            description.style.whiteSpace = WhiteSpace.Normal;
            
            m_Header.Add(title);
            m_Header.Add(description);
            m_Root.Add(m_Header);
        }

        /// <summary>
        /// Creates the main content area with settings sections
        /// </summary>
        private void CreateContent()
        {
            m_Content = new VisualElement();
            m_Content.style.flexGrow = 1;
            m_Content.style.paddingTop = 20;
            m_Content.style.paddingLeft = 20;
            m_Content.style.paddingRight = 20;
            
            CreateGeneralSettings();
            CreateLanguageAndThemeSettings();
            CreateServerSettings();
            CreateExtensionSettings();
            
            m_Root.Add(m_Content);
        }

        /// <summary>
        /// Creates the general settings section
        /// </summary>
        private void CreateGeneralSettings()
        {
            var section = CreateSection(LocalizationManager.GetText("hubsettings.general_settings"));
            
            m_AutoStartServerToggle = new Toggle(LocalizationManager.GetText("hubsettings.auto_start_server"));
            m_AutoStartServerToggle.tooltip = LocalizationManager.GetText("hubsettings.auto_start_server_tooltip");
            section.Add(m_AutoStartServerToggle);
            
            m_AutoInstallDependenciesToggle = new Toggle(LocalizationManager.GetText("hubsettings.auto_install_deps"));
            m_AutoInstallDependenciesToggle.tooltip = LocalizationManager.GetText("hubsettings.auto_install_deps_tooltip");
            section.Add(m_AutoInstallDependenciesToggle);
            
            m_CheckUpdatesOnStartToggle = new Toggle(LocalizationManager.GetText("hubsettings.check_updates_startup"));
            m_CheckUpdatesOnStartToggle.tooltip = LocalizationManager.GetText("hubsettings.check_updates_startup_tooltip");
            section.Add(m_CheckUpdatesOnStartToggle);
            
            m_EnableDebugLoggingToggle = new Toggle(LocalizationManager.GetText("hubsettings.enable_debug_logging"));
            m_EnableDebugLoggingToggle.tooltip = LocalizationManager.GetText("hubsettings.enable_debug_logging_tooltip");
            section.Add(m_EnableDebugLoggingToggle);
            
            m_Content.Add(section);
        }

        /// <summary>
        /// Creates the language and theme settings section (moved from MainWindow)
        /// </summary>
        private void CreateLanguageAndThemeSettings()
        {
            var section = CreateSection(LocalizationManager.GetText("hubsettings.language_settings"));
            
            // Language selector
            m_LanguageSelector = new DropdownField(LocalizationManager.GetText("hubsettings.interface_language"));
            UpdateSelectorChoices();
            var langDesc = new Label(LocalizationManager.GetText("hubsettings.language_desc"));
            langDesc.style.fontSize = 11;
            langDesc.style.color = new Color(0.7f, 0.7f, 0.7f);
            langDesc.style.marginBottom = 10;
            section.Add(m_LanguageSelector);
            section.Add(langDesc);
            
            // Theme selector
            m_ThemeSelector = new DropdownField(LocalizationManager.GetText("hubsettings.ui_theme"));
            UpdateSelectorChoices();
            var themeDesc = new Label(LocalizationManager.GetText("hubsettings.theme_desc"));
            themeDesc.style.fontSize = 11;
            themeDesc.style.color = new Color(0.7f, 0.7f, 0.7f);
            themeDesc.style.marginBottom = 10;
            section.Add(m_ThemeSelector);
            section.Add(themeDesc);
            
            m_Content.Add(section);
        }

        /// <summary>
        /// Creates the server settings section
        /// </summary>
        private void CreateServerSettings()
        {
            var section = CreateSection(LocalizationManager.GetText("hubsettings.server_settings"));
            
            m_ServerHostField = new TextField(LocalizationManager.GetText("hubsettings.server_host"));
            m_ServerHostField.tooltip = LocalizationManager.GetText("hubsettings.server_host_tooltip");
            section.Add(m_ServerHostField);
            
            m_ServerPortField = new IntegerField(LocalizationManager.GetText("hubsettings.server_port"));
            m_ServerPortField.tooltip = LocalizationManager.GetText("hubsettings.server_port_tooltip");
            section.Add(m_ServerPortField);
            
            // Server control buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 10;
            
            var startServerButton = new Button(() => StartMcpServer())
            {
                text = LocalizationManager.GetText("hubsettings.start_server")
            };
            startServerButton.style.marginRight = 5;
            
            var stopServerButton = new Button(() => StopMcpServer())
            {
                text = LocalizationManager.GetText("hubsettings.stop_server")
            };
            stopServerButton.style.marginRight = 5;
            
            var serverStatusButton = new Button(() => CheckServerStatus())
            {
                text = LocalizationManager.GetText("hubsettings.check_status")
            };
            
            buttonContainer.Add(startServerButton);
            buttonContainer.Add(stopServerButton);
            buttonContainer.Add(serverStatusButton);
            section.Add(buttonContainer);
            
            m_Content.Add(section);
        }

        /// <summary>
        /// Creates the extension settings section
        /// </summary>
        private void CreateExtensionSettings()
        {
            var section = CreateSection(LocalizationManager.GetText("hubsettings.extension_settings"));
            
            // Update channel dropdown
            var updateChannels = new List<string> { 
                LocalizationManager.GetText("hubsettings.update_channel_stable"), 
                LocalizationManager.GetText("hubsettings.update_channel_beta"), 
                LocalizationManager.GetText("hubsettings.update_channel_development") 
            };
            m_UpdateChannelDropdown = new PopupField<string>(LocalizationManager.GetText("hubsettings.update_channel"), updateChannels, 0);
            m_UpdateChannelDropdown.tooltip = LocalizationManager.GetText("hubsettings.update_channel_tooltip");
            section.Add(m_UpdateChannelDropdown);
            
            // Extension management buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 10;
            
            m_RefreshExtensionsButton = new Button(() => RefreshExtensions())
            {
                text = LocalizationManager.GetText("hubsettings.refresh_extensions")
            };
            m_RefreshExtensionsButton.style.marginRight = 5;
            
            m_ResetExtensionCacheButton = new Button(() => ResetExtensionCache())
            {
                text = LocalizationManager.GetText("hubsettings.reset_cache")
            };
            m_ResetExtensionCacheButton.style.marginRight = 5;
            
            var openExtensionFolderButton = new Button(() => OpenExtensionFolder())
            {
                text = LocalizationManager.GetText("hubsettings.open_extensions_folder")
            };
            
            buttonContainer.Add(m_RefreshExtensionsButton);
            buttonContainer.Add(m_ResetExtensionCacheButton);
            buttonContainer.Add(openExtensionFolderButton);
            section.Add(buttonContainer);
            
            m_Content.Add(section);
        }

        /// <summary>
        /// Creates a settings section with title
        /// </summary>
        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 20;
            
            var sectionTitle = new Label(title);
            sectionTitle.style.fontSize = 14;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.marginBottom = 10;
            sectionTitle.style.color = new Color(0.9f, 0.9f, 0.9f);
            
            section.Add(sectionTitle);
            
            return section;
        }

        /// <summary>
        /// Creates the footer with action buttons and status
        /// </summary>
        private void CreateFooter()
        {
            m_Footer = new VisualElement();
            m_Footer.style.flexDirection = FlexDirection.Row;
            m_Footer.style.paddingTop = 15;
            m_Footer.style.paddingBottom = 15;
            m_Footer.style.paddingLeft = 20;
            m_Footer.style.paddingRight = 20;
            m_Footer.style.borderTopWidth = 1;
            m_Footer.style.borderTopColor = Color.gray;
            m_Footer.style.justifyContent = Justify.SpaceBetween;
            
            // Status label
            m_StatusLabel = new Label(LocalizationManager.GetText("hubsettings.ready"));
            m_StatusLabel.style.fontSize = 12;
            m_StatusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            
            // Button container
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            
            m_ApplyButton = new Button(() => ApplySettings())
            {
                text = LocalizationManager.GetText("hubsettings.apply")
            };
            m_ApplyButton.style.minWidth = 80;
            m_ApplyButton.style.marginRight = 5;
            
            m_ResetButton = new Button(() => ResetSettings())
            {
                text = LocalizationManager.GetText("hubsettings.reset")
            };
            m_ResetButton.style.minWidth = 80;
            m_ResetButton.style.marginRight = 5;
            
            m_CloseButton = new Button(() => Close())
            {
                text = LocalizationManager.GetText("hubsettings.close")
            };
            m_CloseButton.style.minWidth = 80;
            
            buttonContainer.Add(m_ApplyButton);
            buttonContainer.Add(m_ResetButton);
            buttonContainer.Add(m_CloseButton);
            
            m_Footer.Add(m_StatusLabel);
            m_Footer.Add(buttonContainer);
            m_Root.Add(m_Footer);
        }

        /// <summary>
        /// Updates selector dropdown choices with localized text
        /// </summary>
        private void UpdateSelectorChoices()
        {
            if (m_LanguageSelector != null)
            {
                m_LanguageSelector.choices = new List<string> 
                { 
                    LocalizationManager.GetText("language.english"), 
                    LocalizationManager.GetText("language.chinese") 
                };
            }
            
            if (m_ThemeSelector != null)
            {
                m_ThemeSelector.choices = new List<string> 
                { 
                    LocalizationManager.GetText("theme.dark"), 
                    LocalizationManager.GetText("theme.light"), 
                    LocalizationManager.GetText("theme.auto") 
                };
            }
        }

        /// <summary>
        /// Loads settings from EditorPrefs
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // General settings
                if (m_AutoStartServerToggle != null)
                    m_AutoStartServerToggle.value = EditorPrefs.GetBool(KEY_AUTO_START_SERVER, DEFAULT_AUTO_START_SERVER);
                if (m_AutoInstallDependenciesToggle != null)
                    m_AutoInstallDependenciesToggle.value = EditorPrefs.GetBool(KEY_AUTO_INSTALL_DEPENDENCIES, DEFAULT_AUTO_INSTALL_DEPENDENCIES);
                if (m_CheckUpdatesOnStartToggle != null)
                    m_CheckUpdatesOnStartToggle.value = EditorPrefs.GetBool(KEY_CHECK_UPDATES_ON_START, DEFAULT_CHECK_UPDATES_ON_START);
                if (m_EnableDebugLoggingToggle != null)
                    m_EnableDebugLoggingToggle.value = EditorPrefs.GetBool(KEY_ENABLE_DEBUG_LOGGING, DEFAULT_ENABLE_DEBUG_LOGGING);
                
                // Server settings
                if (m_ServerHostField != null)
                    m_ServerHostField.value = EditorPrefs.GetString(KEY_MCP_SERVER_HOST, DEFAULT_SERVER_HOST);
                if (m_ServerPortField != null)
                    m_ServerPortField.value = EditorPrefs.GetInt(KEY_MCP_SERVER_PORT, DEFAULT_SERVER_PORT);
                
                // Extension settings
                var updateChannel = EditorPrefs.GetString(KEY_EXTENSION_UPDATE_CHANNEL, DEFAULT_UPDATE_CHANNEL);
                var channelIndex = updateChannel switch
                {
                    "stable" => 0,
                    "beta" => 1,
                    "development" => 2,
                    _ => 0
                };
                if (m_UpdateChannelDropdown != null)
                    m_UpdateChannelDropdown.index = channelIndex;
                
                // Language and theme settings (moved from MainWindow)
                LoadLanguageAndThemeSettings();
                
                UpdateStatus(LocalizationManager.GetText("hubsettings.settings_loaded"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error loading settings: {ex.Message}");
                UpdateStatus(LocalizationManager.GetText("hubsettings.error_loading"));
            }
        }

        /// <summary>
        /// Loads language and theme settings (moved from MainWindow)
        /// </summary>
        private void LoadLanguageAndThemeSettings()
        {
            try
            {
                // Update selector options (ensure localization)
                UpdateSelectorChoices();
                
                // Load language settings
                var savedLanguage = EditorPrefs.GetString(KEY_LANGUAGE, "English");
                var localizedLanguage = ConvertLanguageToDisplay(savedLanguage);
                if (m_LanguageSelector != null && m_LanguageSelector.choices.Contains(localizedLanguage))
                {
                    m_LanguageSelector.value = localizedLanguage;
                }
                else if (m_LanguageSelector != null)
                {
                    m_LanguageSelector.value = LocalizationManager.GetText("language.english");
                }

                // Load theme settings
                var savedTheme = EditorPrefs.GetString(KEY_THEME, "Dark");
                var localizedTheme = ConvertThemeToDisplay(savedTheme);
                if (m_ThemeSelector != null && m_ThemeSelector.choices.Contains(localizedTheme))
                {
                    m_ThemeSelector.value = localizedTheme;
                }
                else if (m_ThemeSelector != null)
                {
                    m_ThemeSelector.value = LocalizationManager.GetText("theme.dark");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HubSettings] Failed to load language and theme settings: {e.Message}");
                // Use default values if loading fails
                if (m_LanguageSelector != null)
                    m_LanguageSelector.value = LocalizationManager.GetText("language.english");
                if (m_ThemeSelector != null)
                    m_ThemeSelector.value = LocalizationManager.GetText("theme.dark");
            }
        }

        /// <summary>
        /// Converts saved language to display language
        /// </summary>
        private string ConvertLanguageToDisplay(string savedLanguage)
        {
            return savedLanguage switch
            {
                "简体中文" => LocalizationManager.GetText("language.chinese"),
                "ChineseSimplified" => LocalizationManager.GetText("language.chinese"),
                _ => LocalizationManager.GetText("language.english")
            };
        }

        /// <summary>
        /// Converts saved theme to display theme
        /// </summary>
        private string ConvertThemeToDisplay(string savedTheme)
        {
            return savedTheme switch
            {
                "Light" => LocalizationManager.GetText("theme.light"),
                "Auto" => LocalizationManager.GetText("theme.auto"),
                _ => LocalizationManager.GetText("theme.dark")
            };
        }

        /// <summary>
        /// Converts display language to storage language
        /// </summary>
        private string ConvertDisplayToLanguage(string displayLanguage)
        {
            if (displayLanguage == LocalizationManager.GetText("language.chinese"))
                return "简体中文";
            return "English";
        }

        /// <summary>
        /// Converts display theme to storage theme
        /// </summary>
        private string ConvertDisplayToTheme(string displayTheme)
        {
            if (displayTheme == LocalizationManager.GetText("theme.light"))
                return "Light";
            if (displayTheme == LocalizationManager.GetText("theme.auto"))
                return "Auto";
            return "Dark";
        }

        /// <summary>
        /// Applies and saves settings to EditorPrefs
        /// </summary>
        private void ApplySettings()
        {
            try
            {
                // General settings
                EditorPrefs.SetBool(KEY_AUTO_START_SERVER, m_AutoStartServerToggle.value);
                EditorPrefs.SetBool(KEY_AUTO_INSTALL_DEPENDENCIES, m_AutoInstallDependenciesToggle.value);
                EditorPrefs.SetBool(KEY_CHECK_UPDATES_ON_START, m_CheckUpdatesOnStartToggle.value);
                EditorPrefs.SetBool(KEY_ENABLE_DEBUG_LOGGING, m_EnableDebugLoggingToggle.value);
                
                // Server settings
                EditorPrefs.SetString(KEY_MCP_SERVER_HOST, m_ServerHostField.value);
                EditorPrefs.SetInt(KEY_MCP_SERVER_PORT, m_ServerPortField.value);
                
                // Extension settings
                var updateChannelIndex = m_UpdateChannelDropdown.index;
                var updateChannel = updateChannelIndex switch
                {
                    0 => "stable",
                    1 => "beta",
                    2 => "development",
                    _ => "stable"
                };
                EditorPrefs.SetString(KEY_EXTENSION_UPDATE_CHANNEL, updateChannel);
                
                // Language and theme settings (moved from MainWindow)
                ApplyLanguageAndThemeSettings();
                
                UpdateStatus(LocalizationManager.GetText("hubsettings.settings_applied"));
                Debug.Log($"{Consts.Log.Tag} MCP Hub settings applied");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error applying settings: {ex.Message}");
                UpdateStatus(LocalizationManager.GetText("hubsettings.error_applying"));
            }
        }

        /// <summary>
        /// Applies language and theme settings (moved from MainWindow)
        /// </summary>
        private void ApplyLanguageAndThemeSettings()
        {
            if (m_LanguageSelector != null && m_ThemeSelector != null)
            {
                // Convert display values to storage values
                var languageToSave = ConvertDisplayToLanguage(m_LanguageSelector.value);
                var themeToSave = ConvertDisplayToTheme(m_ThemeSelector.value);
                
                // Check if language has changed
                var previousLanguage = EditorPrefs.GetString(KEY_LANGUAGE, "English");
                var languageChanged = previousLanguage != languageToSave;

                // Save language settings
                EditorPrefs.SetString(KEY_LANGUAGE, languageToSave);
                
                // Save theme settings
                EditorPrefs.SetString(KEY_THEME, themeToSave);
                
                // Update localization manager if language has changed
                if (languageChanged)
                {
                    LocalizationManager.CurrentLanguage = LocalizationManager.StringToLanguage(languageToSave);
                }
            }
        }

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        private void ResetSettings()
        {
            if (EditorUtility.DisplayDialog(LocalizationManager.GetText("hubsettings.reset_confirm_title"), 
                LocalizationManager.GetText("hubsettings.reset_confirm_message"), 
                LocalizationManager.GetText("hubsettings.reset"), LocalizationManager.GetText("common.cancel")))
            {
                try
                {
                    // Reset UI elements to defaults
                    m_AutoStartServerToggle.value = DEFAULT_AUTO_START_SERVER;
                    m_AutoInstallDependenciesToggle.value = DEFAULT_AUTO_INSTALL_DEPENDENCIES;
                    m_CheckUpdatesOnStartToggle.value = DEFAULT_CHECK_UPDATES_ON_START;
                    m_EnableDebugLoggingToggle.value = DEFAULT_ENABLE_DEBUG_LOGGING;
                    m_ServerHostField.value = DEFAULT_SERVER_HOST;
                    m_ServerPortField.value = DEFAULT_SERVER_PORT;
                    m_UpdateChannelDropdown.index = 0; // stable
                    
                    // Reset language and theme settings
                    LocalizationManager.CurrentLanguage = LocalizationManager.Language.English;
                    UpdateSelectorChoices();
                    if (m_LanguageSelector != null)
                        m_LanguageSelector.value = LocalizationManager.GetText("language.english");
                    if (m_ThemeSelector != null)
                        m_ThemeSelector.value = LocalizationManager.GetText("theme.dark");
                    
                    // Save reset values to EditorPrefs
                    EditorPrefs.SetString(KEY_LANGUAGE, "English");
                    EditorPrefs.SetString(KEY_THEME, "Dark");
                    
                    UpdateStatus(LocalizationManager.GetText("hubsettings.settings_reset"));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{Consts.Log.Tag} Error resetting settings: {ex.Message}");
                    UpdateStatus(LocalizationManager.GetText("hubsettings.error_resetting"));
                }
            }
        }

        /// <summary>
        /// Starts the MCP server
        /// </summary>
        private void StartMcpServer()
        {
            try
            {
                UpdateStatus(LocalizationManager.GetText("hubsettings.starting_server"));
                McpPluginUnity.BuildAndStart();
                UpdateStatus(LocalizationManager.GetText("hubsettings.server_started"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error starting MCP server: {ex.Message}");
                UpdateStatus(LocalizationManager.GetText("hubsettings.error_starting_server"));
            }
        }

        /// <summary>
        /// Stops the MCP server by force closing all related processes
        /// </summary>
        private void StopMcpServer()
        {
            try
            {
                UpdateStatus(LocalizationManager.GetText("hubsettings.stopping_server"));
                
                // Find and kill all MCP server processes
                var mcpProcesses = FindMcpServerProcesses();
                
                if (mcpProcesses.Count == 0)
                {
                    UpdateStatus(LocalizationManager.GetText("hubsettings.no_processes_found"));
                    return;
                }
                
                int killedCount = 0;
                foreach (var process in mcpProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            killedCount++;
                            Debug.Log($"{Consts.Log.Tag} Killed MCP server process: {process.ProcessName} (PID: {process.Id})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"{Consts.Log.Tag} Failed to kill process {process.ProcessName} (PID: {process.Id}): {ex.Message}");
                    }
                }
                
                UpdateStatus(LocalizationManager.GetText("hubsettings.force_closed_processes", killedCount));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error stopping MCP server: {ex.Message}");
                UpdateStatus(LocalizationManager.GetText("hubsettings.error_stopping_server"));
            }
        }

        /// <summary>
        /// Checks MCP server status by detecting running processes
        /// </summary>
        private void CheckServerStatus()
        {
            try
            {
                UpdateStatus(LocalizationManager.GetText("hubsettings.checking_status"));
                
                var mcpProcesses = FindMcpServerProcesses();
                
                if (mcpProcesses.Count == 0)
                {
                    UpdateStatus(LocalizationManager.GetText("hubsettings.server_not_running"));
                    return;
                }
                
                var processInfo = mcpProcesses.Select(p => $"{p.ProcessName} (PID: {p.Id})").ToArray();
                var statusMessage = LocalizationManager.GetText("hubsettings.server_running", mcpProcesses.Count);
                
                UpdateStatus(statusMessage);
                Debug.Log($"{Consts.Log.Tag} MCP Server Status: {statusMessage}\nProcesses: {string.Join(", ", processInfo)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error checking server status: {ex.Message}");
                UpdateStatus(LocalizationManager.GetText("hubsettings.error_checking_status"));
            }
        }

        /// <summary>
        /// Finds all MCP server related processes
        /// </summary>
        private List<Process> FindMcpServerProcesses()
        {
            var mcpProcesses = new List<Process>();
            
            try
            {
                // Get all running processes
                var allProcesses = Process.GetProcesses();
                
                // Look for MCP server related processes
                foreach (var process in allProcesses)
                {
                    try
                    {
                        var processName = process.ProcessName.ToLowerInvariant();
                        
                        // Check for common MCP server process names
                        if (processName.Contains("mcp") || 
                            processName.Contains("unity-mcp") ||
                            processName.Contains("dotnet") && IsMcpServerProcess(process))
                        {
                            mcpProcesses.Add(process);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip processes that can't be accessed
                        Debug.LogWarning($"{Consts.Log.Tag} Could not access process: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error finding MCP server processes: {ex.Message}");
            }
            
            return mcpProcesses;
        }

        /// <summary>
        /// Determines if a dotnet process is likely an MCP server
        /// </summary>
        private bool IsMcpServerProcess(Process process)
        {
            try
            {
                // Check if the process has command line arguments that suggest it's an MCP server
                var startInfo = process.StartInfo;
                if (startInfo != null && !string.IsNullOrEmpty(startInfo.Arguments))
                {
                    var args = startInfo.Arguments.ToLowerInvariant();
                    return args.Contains("mcp") || args.Contains("unity-mcp");
                }
                
                // Check process title
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    var title = process.MainWindowTitle.ToLowerInvariant();
                    return title.Contains("mcp") || title.Contains("unity-mcp");
                }
                
                return false;
            }
            catch
            {
                // If we can't access process details, assume it's not an MCP server
                return false;
            }
        }

        /// <summary>
        /// Refreshes the extension list
        /// </summary>
        private void RefreshExtensions()
        {
            try
            {
                UpdateStatus(LocalizationManager.GetText("hubsettings.refreshing_extensions"));
                ExtensionManager.RefreshExtensionCache();
                UpdateStatus(LocalizationManager.GetText("hubsettings.extensions_refreshed"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error refreshing extensions: {ex.Message}");
                UpdateStatus(LocalizationManager.GetText("hubsettings.error_refreshing_extensions"));
            }
        }

        /// <summary>
        /// Resets the extension cache
        /// </summary>
        private void ResetExtensionCache()
        {
            if (EditorUtility.DisplayDialog(LocalizationManager.GetText("hubsettings.reset_cache_confirm_title"), 
                LocalizationManager.GetText("hubsettings.reset_cache_confirm_message"), 
                LocalizationManager.GetText("hubsettings.reset"), LocalizationManager.GetText("common.cancel")))
            {
                try
                {
                    UpdateStatus(LocalizationManager.GetText("hubsettings.resetting_cache"));
                    // Clear extension cache
                    ExtensionManager.RefreshExtensionCache();
                    UpdateStatus(LocalizationManager.GetText("hubsettings.cache_reset"));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{Consts.Log.Tag} Error resetting extension cache: {ex.Message}");
                    UpdateStatus(LocalizationManager.GetText("hubsettings.error_resetting_cache"));
                }
            }
        }

        /// <summary>
        /// Opens the extensions folder in file explorer
        /// </summary>
        private void OpenExtensionFolder()
        {
            try
            {
                var packagesPath = System.IO.Path.Combine(Application.dataPath, "..", "Packages");
                EditorUtility.RevealInFinder(packagesPath);
                UpdateStatus(LocalizationManager.GetText("hubsettings.opened_extensions_folder"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error opening extensions folder: {ex.Message}");
                UpdateStatus(LocalizationManager.GetText("hubsettings.error_opening_folder"));
            }
        }

        /// <summary>
        /// Updates the status message
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = message;
            }
        }

        /// <summary>
        /// Gets setting values for external access
        /// </summary>
        public static class Settings
        {
            public static bool AutoStartServer => EditorPrefs.GetBool(KEY_AUTO_START_SERVER, DEFAULT_AUTO_START_SERVER);
            public static bool AutoInstallDependencies => EditorPrefs.GetBool(KEY_AUTO_INSTALL_DEPENDENCIES, DEFAULT_AUTO_INSTALL_DEPENDENCIES);
            public static bool CheckUpdatesOnStart => EditorPrefs.GetBool(KEY_CHECK_UPDATES_ON_START, DEFAULT_CHECK_UPDATES_ON_START);
            public static bool EnableDebugLogging => EditorPrefs.GetBool(KEY_ENABLE_DEBUG_LOGGING, DEFAULT_ENABLE_DEBUG_LOGGING);
            public static string ServerHost => EditorPrefs.GetString(KEY_MCP_SERVER_HOST, DEFAULT_SERVER_HOST);
            public static int ServerPort => EditorPrefs.GetInt(KEY_MCP_SERVER_PORT, DEFAULT_SERVER_PORT);
            public static string UpdateChannel => EditorPrefs.GetString(KEY_EXTENSION_UPDATE_CHANNEL, DEFAULT_UPDATE_CHANNEL);
            
            // Language and theme settings (moved from MainWindow)
            public static string CurrentLanguage => EditorPrefs.GetString(KEY_LANGUAGE, "English");
            public static string CurrentTheme => EditorPrefs.GetString(KEY_THEME, "Dark");
        }
    }
}
#endif