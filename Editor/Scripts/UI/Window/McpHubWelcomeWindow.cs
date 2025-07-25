#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Editor.Extensions;
using com.MiAO.Unity.MCP.Editor;

namespace com.MiAO.Unity.MCP.Editor.UI
{
    /// <summary>
    /// MCP Hub Welcome window - provides an overview and quick access to Hub features
    /// Shows system status, recent extensions, and quick actions
    /// </summary>
    public class McpHubWelcomeWindow : EditorWindow
    {
        private const string MENU_TITLE = "Welcome to MCP Hub";
        private const int MIN_WIDTH = 600;
        private const int MIN_HEIGHT = 500;
        
        private const string STYLE_PATH = "McpHubWelcomeWindow";
        
        // Setting keys
        private const string KEY_SHOW_ON_STARTUP = "mcp-hub:show-welcome-on-startup";
        private const string KEY_LAST_SHOWN_VERSION = "mcp-hub:last-shown-version";
        
        private static McpHubWelcomeWindow s_Instance;
        
        // UI Elements
        private VisualElement m_Root;
        private VisualElement m_Header;
        private VisualElement m_Content;
        private VisualElement m_Footer;
        
        private Label m_VersionLabel;
        private Label m_StatusLabel;
        private VisualElement m_QuickActionsPanel;
        private VisualElement m_RecentExtensionsPanel;
        
        private Toggle m_ShowOnStartupToggle;

        public static McpHubWelcomeWindow Instance => s_Instance;

        /// <summary>
        /// Shows the welcome window
        /// </summary>
        public static void ShowWindow()
        {
            if (s_Instance != null)
            {
                s_Instance.Focus();
                return;
            }
            
            s_Instance = GetWindow<McpHubWelcomeWindow>(false, MENU_TITLE, true);
            s_Instance.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            s_Instance.Show();
        }

        /// <summary>
        /// Shows welcome window on startup if enabled
        /// </summary>
        [InitializeOnLoadMethod]
        public static void ShowOnStartup()
        {
            if (EditorPrefs.GetBool(KEY_SHOW_ON_STARTUP, true))
            {
                EditorApplication.delayCall += () =>
                {
                    // Only show if this is a new version or first time
                    var currentVersion = GetCurrentVersion();
                    var lastShownVersion = EditorPrefs.GetString(KEY_LAST_SHOWN_VERSION, "");
                    
                    if (currentVersion != lastShownVersion)
                    {
                        ShowWindow();
                        EditorPrefs.SetString(KEY_LAST_SHOWN_VERSION, currentVersion);
                    }
                };
            }
        }

        private void OnEnable()
        {
            s_Instance = this;
            titleContent = new GUIContent(MENU_TITLE, "Welcome to MCP Hub");
            
            CreateUIElements();
            UpdateContent();
        }

        private void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
        }

        /// <summary>
        /// Creates the UI structure for the welcome window
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
                Debug.LogWarning($"{Consts.Log.Tag} Could not load welcome window styles: {ex.Message}");
            }
            
            // Apply default styling
            m_Root.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
        }

        /// <summary>
        /// Creates the header with logo and version info
        /// </summary>
        private void CreateHeader()
        {
            m_Header = new VisualElement();
            m_Header.style.paddingTop = 30;
            m_Header.style.paddingBottom = 20;
            m_Header.style.paddingLeft = 30;
            m_Header.style.paddingRight = 30;
            m_Header.style.alignItems = Align.Center;
            m_Header.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            
            // Logo/Title area
            var titleContainer = new VisualElement();
            titleContainer.style.alignItems = Align.Center;
            
            var title = new Label("MCP Hub");
            title.style.fontSize = 32;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.85f, 0.85f, 1f);
            title.style.marginBottom = 5;
            
            var subtitle = new Label("Unity Model Context Protocol Hub");
            subtitle.style.fontSize = 16;
            subtitle.style.color = new Color(0.7f, 0.7f, 0.7f);
            subtitle.style.marginBottom = 10;
            
            m_VersionLabel = new Label($"Version {GetCurrentVersion()}");
            m_VersionLabel.style.fontSize = 12;
            m_VersionLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            
            titleContainer.Add(title);
            titleContainer.Add(subtitle);
            titleContainer.Add(m_VersionLabel);
            
            m_Header.Add(titleContainer);
            m_Root.Add(m_Header);
        }

        /// <summary>
        /// Creates the main content area
        /// </summary>
        private void CreateContent()
        {
            m_Content = new VisualElement();
            m_Content.style.flexGrow = 1;
            m_Content.style.paddingTop = 20;
            m_Content.style.paddingLeft = 30;
            m_Content.style.paddingRight = 30;
            
            // Create two-column layout
            var columnsContainer = new VisualElement();
            columnsContainer.style.flexDirection = FlexDirection.Row;
            columnsContainer.style.flexGrow = 1;
            
            CreateQuickActionsPanel();
            CreateRecentExtensionsPanel();
            
            columnsContainer.Add(m_QuickActionsPanel);
            columnsContainer.Add(m_RecentExtensionsPanel);
            
            m_Content.Add(columnsContainer);
            m_Root.Add(m_Content);
        }

        /// <summary>
        /// Creates the quick actions panel
        /// </summary>
        private void CreateQuickActionsPanel()
        {
            m_QuickActionsPanel = CreatePanel("Quick Actions");
            
            var actions = new (string, string, System.Action)[]
            {
                ("Open Hub Manager", "Manage extensions and settings", () => McpHubWindow.ShowWindow()),
                ("Open MCP Main Window", "Open MCP main interface", () => OpenMcpMainWindow()),
                ("Tutorial", "View MCP Hub tutorial", () => OpenTutorial()),
                ("Community", "Join the MCP Hub community", () => OpenCommunity()),
                ("Hub Settings", "Configure MCP Hub preferences", () => McpHubSettingsWindow.ShowWindow()),
            };

            foreach (var (title, description, action) in actions)
            {
                var actionButton = CreateActionButton(title, description, action);
                m_QuickActionsPanel.Add(actionButton);
            }
        }

        /// <summary>
        /// Creates the recent extensions panel
        /// </summary>
        private void CreateRecentExtensionsPanel()
        {
            m_RecentExtensionsPanel = CreatePanel("Extensions Status");
            
            // Status info
            var statusContainer = new VisualElement();
            statusContainer.style.marginBottom = 15;
            
            m_StatusLabel = new Label("Loading extension status...");
            m_StatusLabel.style.fontSize = 12;
            m_StatusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            statusContainer.Add(m_StatusLabel);
            
            m_RecentExtensionsPanel.Add(statusContainer);
            
            // Extension list will be populated by UpdateContent()
        }



        /// <summary>
        /// Creates a panel with title
        /// </summary>
        private VisualElement CreatePanel(string title)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.marginRight = 15;
            panel.style.paddingTop = 15;
            panel.style.paddingBottom = 15;
            panel.style.paddingLeft = 15;
            panel.style.paddingRight = 15;
            panel.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            panel.style.borderTopLeftRadius = 5;
            panel.style.borderTopRightRadius = 5;
            panel.style.borderBottomLeftRadius = 5;
            panel.style.borderBottomRightRadius = 5;
            
            var panelTitle = new Label(title);
            panelTitle.style.fontSize = 16;
            panelTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            panelTitle.style.marginBottom = 15;
            panelTitle.style.color = new Color(0.9f, 0.9f, 0.9f);
            
            panel.Add(panelTitle);
            return panel;
        }

        /// <summary>
        /// Creates an action button with title and description
        /// </summary>
        private VisualElement CreateActionButton(string title, string description, Action action)
        {
            var buttonContainer = new VisualElement();
            buttonContainer.style.marginBottom = 10;
            buttonContainer.style.paddingTop = 10;
            buttonContainer.style.paddingBottom = 10;
            buttonContainer.style.paddingLeft = 10;
            buttonContainer.style.paddingRight = 10;
            buttonContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            buttonContainer.style.borderTopLeftRadius = 3;
            buttonContainer.style.borderTopRightRadius = 3;
            buttonContainer.style.borderBottomLeftRadius = 3;
            buttonContainer.style.borderBottomRightRadius = 3;
            
            var button = new Button(action);
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            
            var content = new VisualElement();
            
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            titleLabel.style.marginBottom = 2;
            
            var descLabel = new Label(description);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            
            content.Add(titleLabel);
            content.Add(descLabel);
            button.Add(content);
            buttonContainer.Add(button);
            
            // Hover effects
            buttonContainer.RegisterCallback<MouseEnterEvent>(evt =>
            {
                buttonContainer.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
            });
            
            buttonContainer.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                buttonContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            });
            
            return buttonContainer;
        }

        /// <summary>
        /// Creates the footer with preferences
        /// </summary>
        private void CreateFooter()
        {
            m_Footer = new VisualElement();
            m_Footer.style.flexDirection = FlexDirection.Row;
            m_Footer.style.justifyContent = Justify.SpaceBetween;
            m_Footer.style.paddingTop = 20;
            m_Footer.style.paddingBottom = 20;
            m_Footer.style.paddingLeft = 30;
            m_Footer.style.paddingRight = 30;
            m_Footer.style.borderTopWidth = 1;
            m_Footer.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            
            m_ShowOnStartupToggle = new Toggle("Show this window on startup");
            m_ShowOnStartupToggle.value = EditorPrefs.GetBool(KEY_SHOW_ON_STARTUP, true);
            m_ShowOnStartupToggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(KEY_SHOW_ON_STARTUP, evt.newValue);
            });
            
            var closeButton = new Button(() => Close())
            {
                text = "Get Started"
            };
            closeButton.style.minWidth = 100;
            closeButton.style.height = 30;
            
            m_Footer.Add(m_ShowOnStartupToggle);
            m_Footer.Add(closeButton);
            m_Root.Add(m_Footer);
        }

        /// <summary>
        /// Updates the content with current information
        /// </summary>
        private void UpdateContent()
        {
            try
            {
                // Update extension status
                var installedExtensions = ExtensionManager.GetInstalledExtensions();
                var availableExtensions = ExtensionManager.GetAvailableExtensions();
                var updatesAvailable = ExtensionManager.GetExtensionsWithUpdates();
                
                m_StatusLabel.text = $"Extensions: {installedExtensions.Count} installed, " +
                                   $"{availableExtensions.Count - installedExtensions.Count} available, " +
                                   $"{updatesAvailable.Count} updates";

                // Add extension quick info
                if (installedExtensions.Count > 0)
                {
                    var extensionsList = new VisualElement();
                    extensionsList.style.marginTop = 10;
                    
                    var listTitle = new Label("Installed Extensions:");
                    listTitle.style.fontSize = 12;
                    listTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                    listTitle.style.marginBottom = 5;
                    extensionsList.Add(listTitle);
                    
                    foreach (var ext in installedExtensions.Take(5)) // Show first 5
                    {
                        var extItem = new Label($"• {ext.DisplayName} v{ext.InstalledVersion}");
                        extItem.style.fontSize = 11;
                        extItem.style.color = new Color(0.8f, 0.8f, 0.8f);
                        extensionsList.Add(extItem);
                    }
                    
                    if (installedExtensions.Count > 5)
                    {
                        var moreLabel = new Label($"... and {installedExtensions.Count - 5} more");
                        moreLabel.style.fontSize = 11;
                        moreLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                        moreLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                        extensionsList.Add(moreLabel);
                    }
                    
                    m_RecentExtensionsPanel.Add(extensionsList);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Consts.Log.Tag} Error updating welcome content: {ex.Message}");
                m_StatusLabel.text = "Error loading extension status";
            }
        }

        /// <summary>
        /// Gets the current MCP Hub version
        /// </summary>
        private static string GetCurrentVersion()
        {
            try
            {
                // Try to read version from package.json
                var packagePath = System.IO.Path.Combine(Application.dataPath, "..", "Packages", "Unity-MCP", "package.json");
                if (System.IO.File.Exists(packagePath))
                {
                    var packageJson = System.IO.File.ReadAllText(packagePath);
                    // Simple version extraction - in production, use JSON parser
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(packageJson, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                    if (versionMatch.Success)
                    {
                        return versionMatch.Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Consts.Log.Tag} Could not read version: {ex.Message}");
            }
            
            return "1.0.0";
        }

        // Action methods
        private void OpenMcpMainWindow()
        {
            // Open the main MCP window
            var window = EditorWindow.GetWindow<MainWindowEditor>();
            window.Show();
        }

        private void OpenTutorial()
        {
            Application.OpenURL("https://github.com/MiAO-AI-Lab/Unity-MCP");
        }

        private void OpenCommunity()
        {
            Application.OpenURL("https://discord.gg/JC6xvWAh3F");
        }
    }
}
#endif