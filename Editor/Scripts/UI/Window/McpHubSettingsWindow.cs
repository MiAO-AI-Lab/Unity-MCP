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
using Debug = UnityEngine.Debug;

namespace com.MiAO.Unity.MCP.Editor.UI
{
    /// <summary>
    /// MCP Hub Settings window for configuring Hub preferences and extension settings
    /// Provides configuration options for MCP server, extensions, and development settings
    /// </summary>
    public class McpHubSettingsWindow : EditorWindow
    {
        private const string MENU_TITLE = "MCP Hub Settings";
        private const int MIN_WIDTH = 500;
        private const int MIN_HEIGHT = 400;
        
        private const string STYLE_PATH = "McpHubSettingsWindow";
        
        // Setting keys for EditorPrefs
        private const string KEY_AUTO_START_SERVER = "mcp-hub:auto-start-server";
        private const string KEY_AUTO_INSTALL_DEPENDENCIES = "mcp-hub:auto-install-dependencies";
        private const string KEY_CHECK_UPDATES_ON_START = "mcp-hub:check-updates-on-start";
        private const string KEY_ENABLE_DEBUG_LOGGING = "mcp-hub:enable-debug-logging";
        private const string KEY_EXTENSION_UPDATE_CHANNEL = "mcp-hub:extension-update-channel";
        private const string KEY_MCP_SERVER_PORT = "mcp-hub:mcp-server-port";
        private const string KEY_MCP_SERVER_HOST = "mcp-hub:mcp-server-host";

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
            
            s_Instance = GetWindow<McpHubSettingsWindow>(true, MENU_TITLE, true);
            s_Instance.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            s_Instance.Show();
        }

        private void OnEnable()
        {
            s_Instance = this;
            titleContent = new GUIContent(MENU_TITLE, "Configure MCP Hub settings");
            
            CreateUIElements();
            LoadSettings();
        }

        private void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
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
            
            var title = new Label("MCP Hub Settings");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            
            var description = new Label("Configure MCP Hub behavior, server settings, and extension preferences.");
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
            CreateServerSettings();
            CreateExtensionSettings();
            
            m_Root.Add(m_Content);
        }

        /// <summary>
        /// Creates the general settings section
        /// </summary>
        private void CreateGeneralSettings()
        {
            var section = CreateSection("General Settings");
            
            m_AutoStartServerToggle = new Toggle("Auto-start MCP server on Unity startup");
            m_AutoStartServerToggle.tooltip = "Automatically starts the MCP server when Unity Editor starts";
            section.Add(m_AutoStartServerToggle);
            
            m_AutoInstallDependenciesToggle = new Toggle("Auto-install extension dependencies");
            m_AutoInstallDependenciesToggle.tooltip = "Automatically install required dependencies when installing extensions";
            section.Add(m_AutoInstallDependenciesToggle);
            
            m_CheckUpdatesOnStartToggle = new Toggle("Check for extension updates on startup");
            m_CheckUpdatesOnStartToggle.tooltip = "Check for available extension updates when Unity Editor starts";
            section.Add(m_CheckUpdatesOnStartToggle);
            
            m_EnableDebugLoggingToggle = new Toggle("Enable debug logging");
            m_EnableDebugLoggingToggle.tooltip = "Enable detailed debug logs for MCP Hub operations";
            section.Add(m_EnableDebugLoggingToggle);
            
            m_Content.Add(section);
        }

        /// <summary>
        /// Creates the server settings section
        /// </summary>
        private void CreateServerSettings()
        {
            var section = CreateSection("MCP Server Settings");
            
            m_ServerHostField = new TextField("Server Host");
            m_ServerHostField.tooltip = "Host address for the MCP server";
            section.Add(m_ServerHostField);
            
            m_ServerPortField = new IntegerField("Server Port");
            m_ServerPortField.tooltip = "Port number for the MCP server";
            section.Add(m_ServerPortField);
            
            // Server control buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 10;
            
            var startServerButton = new Button(() => StartMcpServer())
            {
                text = "Start Server"
            };
            startServerButton.style.marginRight = 5;
            
            var stopServerButton = new Button(() => StopMcpServer())
            {
                text = "Stop Server"
            };
            stopServerButton.style.marginRight = 5;
            
            var serverStatusButton = new Button(() => CheckServerStatus())
            {
                text = "Check Status"
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
            var section = CreateSection("Extension Settings");
            
            // Update channel dropdown
            var updateChannels = new List<string> { "stable", "beta", "development" };
            m_UpdateChannelDropdown = new PopupField<string>("Update Channel", updateChannels, 0);
            m_UpdateChannelDropdown.tooltip = "Choose the update channel for extension packages";
            section.Add(m_UpdateChannelDropdown);
            
            // Extension management buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 10;
            
            m_RefreshExtensionsButton = new Button(() => RefreshExtensions())
            {
                text = "Refresh Extensions"
            };
            m_RefreshExtensionsButton.style.marginRight = 5;
            
            m_ResetExtensionCacheButton = new Button(() => ResetExtensionCache())
            {
                text = "Reset Cache"
            };
            m_ResetExtensionCacheButton.style.marginRight = 5;
            
            var openExtensionFolderButton = new Button(() => OpenExtensionFolder())
            {
                text = "Open Extensions Folder"
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
            m_StatusLabel = new Label("Ready");
            m_StatusLabel.style.fontSize = 12;
            m_StatusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            
            // Button container
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            
            m_ApplyButton = new Button(() => ApplySettings())
            {
                text = "Apply"
            };
            m_ApplyButton.style.minWidth = 80;
            m_ApplyButton.style.marginRight = 5;
            
            m_ResetButton = new Button(() => ResetSettings())
            {
                text = "Reset"
            };
            m_ResetButton.style.minWidth = 80;
            m_ResetButton.style.marginRight = 5;
            
            m_CloseButton = new Button(() => Close())
            {
                text = "Close"
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
        /// Loads settings from EditorPrefs
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // General settings
                m_AutoStartServerToggle.value = EditorPrefs.GetBool(KEY_AUTO_START_SERVER, DEFAULT_AUTO_START_SERVER);
                m_AutoInstallDependenciesToggle.value = EditorPrefs.GetBool(KEY_AUTO_INSTALL_DEPENDENCIES, DEFAULT_AUTO_INSTALL_DEPENDENCIES);
                m_CheckUpdatesOnStartToggle.value = EditorPrefs.GetBool(KEY_CHECK_UPDATES_ON_START, DEFAULT_CHECK_UPDATES_ON_START);
                m_EnableDebugLoggingToggle.value = EditorPrefs.GetBool(KEY_ENABLE_DEBUG_LOGGING, DEFAULT_ENABLE_DEBUG_LOGGING);
                
                // Server settings
                m_ServerHostField.value = EditorPrefs.GetString(KEY_MCP_SERVER_HOST, DEFAULT_SERVER_HOST);
                m_ServerPortField.value = EditorPrefs.GetInt(KEY_MCP_SERVER_PORT, DEFAULT_SERVER_PORT);
                
                // Extension settings
                var updateChannel = EditorPrefs.GetString(KEY_EXTENSION_UPDATE_CHANNEL, DEFAULT_UPDATE_CHANNEL);
                var channelIndex = Array.IndexOf(new[] { "stable", "beta", "development" }, updateChannel);
                m_UpdateChannelDropdown.index = channelIndex >= 0 ? channelIndex : 0;
                
                UpdateStatus("Settings loaded");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error loading settings: {ex.Message}");
                UpdateStatus("Error loading settings");
            }
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
                var updateChannels = new List<string> { "stable", "beta", "development" };
                EditorPrefs.SetString(KEY_EXTENSION_UPDATE_CHANNEL, updateChannels[m_UpdateChannelDropdown.index]);
                
                UpdateStatus("Settings applied successfully");
                Debug.Log($"{Consts.Log.Tag} MCP Hub settings applied");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error applying settings: {ex.Message}");
                UpdateStatus("Error applying settings");
            }
        }

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        private void ResetSettings()
        {
            if (EditorUtility.DisplayDialog("Reset Settings", 
                "Are you sure you want to reset all settings to default values?", 
                "Reset", "Cancel"))
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
                    
                    UpdateStatus("Settings reset to defaults");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{Consts.Log.Tag} Error resetting settings: {ex.Message}");
                    UpdateStatus("Error resetting settings");
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
                UpdateStatus("Starting MCP server...");
                McpPluginUnity.BuildAndStart();
                UpdateStatus("MCP server started");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error starting MCP server: {ex.Message}");
                UpdateStatus("Error starting MCP server");
            }
        }

        /// <summary>
        /// Stops the MCP server by force closing all related processes
        /// </summary>
        private void StopMcpServer()
        {
            try
            {
                UpdateStatus("Stopping MCP server...");
                
                // Find and kill all MCP server processes
                var mcpProcesses = FindMcpServerProcesses();
                
                if (mcpProcesses.Count == 0)
                {
                    UpdateStatus("No MCP server processes found");
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
                
                UpdateStatus($"Force closed {killedCount} MCP server processes");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error stopping MCP server: {ex.Message}");
                UpdateStatus("Error stopping MCP server");
            }
        }

        /// <summary>
        /// Checks MCP server status by detecting running processes
        /// </summary>
        private void CheckServerStatus()
        {
            try
            {
                UpdateStatus("Checking server status...");
                
                var mcpProcesses = FindMcpServerProcesses();
                
                if (mcpProcesses.Count == 0)
                {
                    UpdateStatus("Server status: Not running");
                    return;
                }
                
                var processInfo = mcpProcesses.Select(p => $"{p.ProcessName} (PID: {p.Id})").ToArray();
                var statusMessage = $"Server status: Running - {mcpProcesses.Count} process(es) detected\n" +
                                  $"Processes: {string.Join(", ", processInfo)}";
                
                UpdateStatus(statusMessage);
                Debug.Log($"{Consts.Log.Tag} MCP Server Status: {statusMessage}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error checking server status: {ex.Message}");
                UpdateStatus("Error checking server status");
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
                UpdateStatus("Refreshing extensions...");
                ExtensionManager.RefreshExtensionCache();
                UpdateStatus("Extensions refreshed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error refreshing extensions: {ex.Message}");
                UpdateStatus("Error refreshing extensions");
            }
        }

        /// <summary>
        /// Resets the extension cache
        /// </summary>
        private void ResetExtensionCache()
        {
            if (EditorUtility.DisplayDialog("Reset Extension Cache", 
                "Are you sure you want to reset the extension cache? This will clear all cached extension data.", 
                "Reset", "Cancel"))
            {
                try
                {
                    UpdateStatus("Resetting extension cache...");
                    // Clear extension cache
                    ExtensionManager.RefreshExtensionCache();
                    UpdateStatus("Extension cache reset");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{Consts.Log.Tag} Error resetting extension cache: {ex.Message}");
                    UpdateStatus("Error resetting extension cache");
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
                UpdateStatus("Opened extensions folder");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error opening extensions folder: {ex.Message}");
                UpdateStatus("Error opening extensions folder");
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
        }
    }
}
#endif