#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Editor.Extensions;

namespace com.MiAO.Unity.MCP.Editor.UI
{
    /// <summary>
    /// Main MCP Hub window for managing extension packages and Hub settings
    /// Similar to GameCreator's hub system but tailored for MCP
    /// </summary>
    public class McpHubWindow : EditorWindow
    {
        private const string MENU_ITEM = "Window/MCP Hub Manager";
        private const string MENU_TITLE = "MCP Hub Manager";
        
        private const int MIN_WIDTH = 900;
        private const int MIN_HEIGHT = 600;
        
        private const string STYLE_PATH = "McpHubWindow";
        
        private const string NAME_SIDEBAR = "mcp-hub-sidebar";
        private const string NAME_MAIN_CONTENT = "mcp-hub-main-content";
        private const string NAME_TOOLBAR = "mcp-hub-toolbar";
        private const string NAME_STATUS_BAR = "mcp-hub-status-bar";

        // Cache keys for persistent data
        private const string KEY_SELECTED_TAB = "mcp-hub:selected-tab";
        private const string KEY_LAST_REFRESH = "mcp-hub:last-refresh";

        private static McpHubWindow s_Instance;
        
        // UI Elements
        private VisualElement m_Root;
        private VisualElement m_Sidebar;
        private VisualElement m_MainContent;
        private VisualElement m_Toolbar;
        private VisualElement m_StatusBar;
        
        private ListView m_ExtensionList;
        private TextField m_SearchField;
        private Button m_RefreshButton;
        private Button m_SettingsButton;
        private Label m_StatusLabel;
        
        // Data
        private List<ExtensionPackageInfo> m_AvailableExtensions = new List<ExtensionPackageInfo>();
        private int m_SelectedTabIndex = 0;
        
        // Properties
        public static McpHubWindow Instance => s_Instance;
        
        public int SelectedTabIndex
        {
            get => EditorPrefs.GetInt(KEY_SELECTED_TAB, 0);
            set => EditorPrefs.SetInt(KEY_SELECTED_TAB, value);
        }

        // Menu Items
        [MenuItem(MENU_ITEM)]
        public static void ShowWindow()
        {
            s_Instance = GetWindow<McpHubWindow>(false, MENU_TITLE, true);
            s_Instance.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            s_Instance.Show();
        }

        private void OnEnable()
        {
            s_Instance = this;
            titleContent = new GUIContent(MENU_TITLE, "Manage MCP extension packages");
            
            CreateUIElements();
            LoadData();
            RefreshExtensionList();
        }

        private void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
        }

        /// <summary>
        /// Creates the main UI layout structure
        /// </summary>
        private void CreateUIElements()
        {
            m_Root = rootVisualElement;
            m_Root.Clear();
            
            // Load custom styles if available
            LoadStyles();
            
            // Create main layout
            CreateToolbar();
            CreateMainLayout();
            CreateStatusBar();
            
            // Setup event handlers
            SetupEventHandlers();
        }

        /// <summary>
        /// Loads USS styles for the window
        /// </summary>
        private void LoadStyles()
        {
            try
            {
                // Try to load custom styles
                var styleSheet = Resources.Load<StyleSheet>(STYLE_PATH);
                if (styleSheet != null)
                {
                    m_Root.styleSheets.Add(styleSheet);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Consts.Log.Tag} Could not load MCP Hub styles: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the top toolbar with search and action buttons
        /// </summary>
        private void CreateToolbar()
        {
            m_Toolbar = new VisualElement { name = NAME_TOOLBAR };
            m_Toolbar.style.flexDirection = FlexDirection.Row;
            m_Toolbar.style.paddingTop = 5;
            m_Toolbar.style.paddingBottom = 5;
            m_Toolbar.style.paddingLeft = 10;
            m_Toolbar.style.paddingRight = 10;
            m_Toolbar.style.borderBottomWidth = 1;
            m_Toolbar.style.borderBottomColor = Color.gray;
            
            // Search field
            m_SearchField = new TextField("Search Extensions");
            m_SearchField.style.flexGrow = 1;
            m_SearchField.style.marginRight = 10;
            
            // Refresh button
            m_RefreshButton = new Button(() => RefreshExtensionList())
            {
                text = "Refresh"
            };
            m_RefreshButton.style.minWidth = 80;
            m_RefreshButton.style.marginRight = 5;
            
            // Settings button
            m_SettingsButton = new Button(() => McpHubSettingsWindow.ShowWindow())
            {
                text = "Settings"
            };
            m_SettingsButton.style.minWidth = 80;
            
            m_Toolbar.Add(m_SearchField);
            m_Toolbar.Add(m_RefreshButton);
            m_Toolbar.Add(m_SettingsButton);
            
            m_Root.Add(m_Toolbar);
        }

        /// <summary>
        /// Creates the main content area with sidebar and content panels
        /// </summary>
        private void CreateMainLayout()
        {
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.flexGrow = 1;
            
            CreateSidebar();
            CreateMainContent();
            
            mainContainer.Add(m_Sidebar);
            mainContainer.Add(m_MainContent);
            
            m_Root.Add(mainContainer);
        }

        /// <summary>
        /// Creates the left sidebar with extension categories
        /// </summary>
        private void CreateSidebar()
        {
            m_Sidebar = new VisualElement { name = NAME_SIDEBAR };
            m_Sidebar.style.width = 250;
            m_Sidebar.style.borderRightWidth = 1;
            m_Sidebar.style.borderRightColor = Color.gray;
            m_Sidebar.style.paddingTop = 10;
            m_Sidebar.style.paddingLeft = 10;
            m_Sidebar.style.paddingRight = 10;
            
            // Category buttons
            CreateCategoryButton("All Extensions", 0);
            CreateCategoryButton("Installed", 1);
            CreateCategoryButton("Available", 2);
            CreateCategoryButton("Updates", 3);
            
            // Separator
            // var separator = new VisualElement();
            // separator.style.height = 1;
            // separator.style.backgroundColor = Color.gray;
            // separator.style.marginTop = 10;
            // separator.style.marginBottom = 10;
            // m_Sidebar.Add(separator);
            
            // Extension categories
            // CreateCategoryButton("Essential Tools", 4);
            // CreateCategoryButton("Vision Packs", 5);
            // CreateCategoryButton("Programmer Packs", 6);
        }

        /// <summary>
        /// Creates a category button in the sidebar
        /// </summary>
        private void CreateCategoryButton(string text, int index)
        {
            var button = new Button(() => SelectCategory(index))
            {
                text = text
            };
            button.style.marginBottom = 2;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 10;
            
            if (index == SelectedTabIndex)
            {
                button.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
            }
            
            m_Sidebar.Add(button);
        }

        /// <summary>
        /// Creates the main content area showing extensions
        /// </summary>
        private void CreateMainContent()
        {
            m_MainContent = new VisualElement { name = NAME_MAIN_CONTENT };
            m_MainContent.style.flexGrow = 1;
            m_MainContent.style.padding = 10;
            
            // Extension list
            CreateExtensionList();
            
            m_MainContent.Add(m_ExtensionList);
        }

        /// <summary>
        /// Creates the extension list view
        /// </summary>
        private void CreateExtensionList()
        {
            m_ExtensionList = new ListView
            {
                itemsSource = m_AvailableExtensions,
                itemHeight = 80,
                makeItem = MakeExtensionItem,
                bindItem = BindExtensionItem
            };
            m_ExtensionList.style.flexGrow = 1;
        }

        /// <summary>
        /// Creates a UI item for an extension in the list
        /// </summary>
        private VisualElement MakeExtensionItem()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.paddingTop = 5;
            container.style.paddingBottom = 5;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            
            // Icon placeholder
            var icon = new VisualElement();
            icon.style.width = 60;
            icon.style.height = 60;
            icon.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            icon.style.marginRight = 10;
            
            // Content area
            var content = new VisualElement();
            content.style.flexGrow = 1;
            
            var title = new Label();
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            var description = new Label();
            description.style.fontSize = 12;
            description.style.color = new Color(0.7f, 0.7f, 0.7f);
            description.style.whiteSpace = WhiteSpace.Normal;
            
            var status = new Label();
            status.style.fontSize = 10;
            status.style.marginTop = 5;
            
            content.Add(title);
            content.Add(description);
            content.Add(status);
            
            // Action buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Column;
            buttonContainer.style.justifyContent = Justify.Center;
            buttonContainer.style.minWidth = 100;
            
            var actionButton = new Button();
            actionButton.style.minWidth = 90;
            
            buttonContainer.Add(actionButton);
            
            container.Add(icon);
            container.Add(content);
            container.Add(buttonContainer);
            
            return container;
        }

        /// <summary>
        /// Binds data to an extension item in the list
        /// </summary>
        private void BindExtensionItem(VisualElement element, int index)
        {
            if (index >= m_AvailableExtensions.Count) return;
            
            var extension = m_AvailableExtensions[index];
            var container = element;
            
            // Get UI elements
            var title = container.Q<Label>();
            var description = container.Q<Label>("", className: null);
            var status = container.Q<Label>("", className: null);
            var actionButton = container.Q<Button>();
            
            if (title != null) title.text = extension.DisplayName;
            if (description != null) description.text = extension.Description;
            if (status != null) status.text = GetExtensionStatusText(extension);
            
            if (actionButton != null)
            {
                ConfigureActionButton(actionButton, extension);
            }
        }

        /// <summary>
        /// Configures the action button for an extension
        /// </summary>
        private void ConfigureActionButton(Button button, ExtensionPackageInfo extension)
        {
            button.clicked -= null; // Clear existing handlers
            
            if (extension.IsInstalled)
            {
                if (extension.HasUpdate)
                {
                    button.text = "Update";
                    button.clicked += () => UpdateExtension(extension);
                }
                else
                {
                    button.text = "Uninstall";
                    button.clicked += () => UninstallExtension(extension);
                }
            }
            else
            {
                button.text = "Install";
                button.clicked += () => InstallExtension(extension);
            }
        }

        /// <summary>
        /// Creates the bottom status bar
        /// </summary>
        private void CreateStatusBar()
        {
            m_StatusBar = new VisualElement { name = NAME_STATUS_BAR };
            m_StatusBar.style.flexDirection = FlexDirection.Row;
            m_StatusBar.style.paddingTop = 5;
            m_StatusBar.style.paddingBottom = 5;
            m_StatusBar.style.paddingLeft = 10;
            m_StatusBar.style.paddingRight = 10;
            m_StatusBar.style.borderTopWidth = 1;
            m_StatusBar.style.borderTopColor = Color.gray;
            
            m_StatusLabel = new Label("Ready");
            m_StatusLabel.style.flexGrow = 1;
            m_StatusLabel.style.fontSize = 12;
            
            m_StatusBar.Add(m_StatusLabel);
            m_Root.Add(m_StatusBar);
        }

        /// <summary>
        /// Sets up event handlers for UI interactions
        /// </summary>
        private void SetupEventHandlers()
        {
            if (m_SearchField != null)
            {
                m_SearchField.RegisterValueChangedCallback(OnSearchChanged);
            }
        }

        /// <summary>
        /// Handles search field value changes
        /// </summary>
        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            FilterExtensions(evt.newValue);
        }

        /// <summary>
        /// Selects a category in the sidebar
        /// </summary>
        private void SelectCategory(int index)
        {
            SelectedTabIndex = index;
            RefreshSidebar();
            FilterExtensionsByCategory(index);
        }

        /// <summary>
        /// Refreshes the sidebar to reflect current selection
        /// </summary>
        private void RefreshSidebar()
        {
            // Recreate sidebar to update button states
            m_Sidebar.Clear();
            
            CreateCategoryButton("All Extensions", 0);
            CreateCategoryButton("Installed", 1);
            CreateCategoryButton("Available", 2);
            CreateCategoryButton("Updates", 3);
            
            // var separator = new VisualElement();
            // separator.style.height = 1;
            // separator.style.backgroundColor = Color.gray;
            // separator.style.marginTop = 10;
            // separator.style.marginBottom = 10;
            // m_Sidebar.Add(separator);
            
            // CreateCategoryButton("Essential Tools", 4);
            // CreateCategoryButton("Vision Packs", 5);
            // CreateCategoryButton("Programmer Packs", 6);
        }

        /// <summary>
        /// Loads extension data
        /// </summary>
        private void LoadData()
        {
            m_AvailableExtensions = ExtensionManager.GetAvailableExtensions();
        }

        /// <summary>
        /// Refreshes the extension list
        /// </summary>
        private void RefreshExtensionList()
        {
            UpdateStatus("Refreshing extension list...");
            
            try
            {
                LoadData();
                FilterExtensionsByCategory(SelectedTabIndex);
                
                if (m_ExtensionList != null)
                {
                    m_ExtensionList.Rebuild();
                }
                
                UpdateStatus($"Found {m_AvailableExtensions.Count} extensions");
                EditorPrefs.SetString(KEY_LAST_REFRESH, DateTime.Now.ToString());
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing extensions: {ex.Message}");
                Debug.LogError($"{Consts.Log.Tag} Error refreshing extension list: {ex}");
            }
        }

        /// <summary>
        /// Filters extensions by search query
        /// </summary>
        private void FilterExtensions(string query)
        {
            // Implementation for filtering extensions based on search
            // This would filter m_AvailableExtensions and rebuild the list
            UpdateStatus($"Filtering extensions: {query}");
        }

        /// <summary>
        /// Filters extensions by category
        /// </summary>
        private void FilterExtensionsByCategory(int categoryIndex)
        {
            // Implementation for filtering by category
            switch (categoryIndex)
            {
                case 0: // All Extensions
                    // Show all
                    break;
                case 1: // Installed
                    // Show only installed
                    break;
                case 2: // Available
                    // Show only available for install
                    break;
                case 3: // Updates
                    // Show only extensions with updates
                    break;
                case 4: // Essential Tools
                case 5: // Vision Packs
                case 6: // Programmer Packs
                    // Filter by specific pack type
                    break;
            }
            
            if (m_ExtensionList != null)
            {
                m_ExtensionList.Rebuild();
            }
        }

        /// <summary>
        /// Gets the status text for an extension
        /// </summary>
        private string GetExtensionStatusText(ExtensionPackageInfo extension)
        {
            if (extension.IsInstalled)
            {
                if (extension.HasUpdate)
                    return $"Installed v{extension.InstalledVersion} (Update available: v{extension.LatestVersion})";
                return $"Installed v{extension.InstalledVersion}";
            }
            return $"Available v{extension.LatestVersion}";
        }

        /// <summary>
        /// Installs an extension package
        /// </summary>
        private async void InstallExtension(ExtensionPackageInfo extension)
        {
            UpdateStatus($"Installing {extension.DisplayName}...");
            
            try
            {
                await ExtensionManager.InstallExtensionAsync(extension);
                UpdateStatus($"Successfully installed {extension.DisplayName}");
                RefreshExtensionList();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to install {extension.DisplayName}: {ex.Message}");
                Debug.LogError($"{Consts.Log.Tag} Installation failed: {ex}");
            }
        }

        /// <summary>
        /// Uninstalls an extension package
        /// </summary>
        private async void UninstallExtension(ExtensionPackageInfo extension)
        {
            if (!EditorUtility.DisplayDialog("Uninstall Extension", 
                $"Are you sure you want to uninstall {extension.DisplayName}?", 
                "Uninstall", "Cancel"))
            {
                return;
            }
            
            UpdateStatus($"Uninstalling {extension.DisplayName}...");
            
            try
            {
                await ExtensionManager.UninstallExtensionAsync(extension);
                UpdateStatus($"Successfully uninstalled {extension.DisplayName}");
                RefreshExtensionList();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to uninstall {extension.DisplayName}: {ex.Message}");
                Debug.LogError($"{Consts.Log.Tag} Uninstallation failed: {ex}");
            }
        }

        /// <summary>
        /// Updates an extension package
        /// </summary>
        private async void UpdateExtension(ExtensionPackageInfo extension)
        {
            UpdateStatus($"Updating {extension.DisplayName}...");
            
            try
            {
                await ExtensionManager.UpdateExtensionAsync(extension);
                UpdateStatus($"Successfully updated {extension.DisplayName} to v{extension.LatestVersion}");
                RefreshExtensionList();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to update {extension.DisplayName}: {ex.Message}");
                Debug.LogError($"{Consts.Log.Tag} Update failed: {ex}");
            }
        }

        /// <summary>
        /// Updates the status bar message
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = message;
            }
            
            Debug.Log($"{Consts.Log.Tag} {message}");
        }
    }
}
#endif