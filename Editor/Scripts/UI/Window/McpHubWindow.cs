#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
        
        // Special package IDs that should be treated differently
        private const string HUB_CORE_FRAMEWORK_ID = "com.miao.unity.mcp";

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
        private ProgressBar m_ProgressBar;
        
        // Data
        private List<ExtensionPackageInfo> m_AvailableExtensions = new List<ExtensionPackageInfo>();
        private List<ExtensionPackageInfo> m_FilteredExtensions = new List<ExtensionPackageInfo>();
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
            
            // Register search field callback
            m_SearchField.RegisterCallback<ChangeEvent<string>>(evt => FilterExtensions(evt.newValue));
            
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
            m_MainContent.style.paddingTop = 10;
            m_MainContent.style.paddingTop = 10;
            
            // Extension list
            CreateExtensionList();
            
            m_MainContent.Add(m_ExtensionList);
        }

        /// <summary>
        /// Creates the extension list view
        /// </summary>
        private void CreateExtensionList()
        {
            m_FilteredExtensions = new List<ExtensionPackageInfo>();
            m_ExtensionList = new ListView
            {
                itemsSource = m_FilteredExtensions,
                itemHeight = 70,
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
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            
            // Content area
            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.marginRight = 10;
            
            var title = new Label();
            title.name = "title";
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            
            var description = new Label();
            description.name = "description";
            description.style.fontSize = 12;
            description.style.color = new Color(0.7f, 0.7f, 0.7f);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 4;
            
            var status = new Label();
            status.name = "status";
            status.style.fontSize = 10;
            status.style.color = new Color(0.6f, 0.6f, 0.6f);
            
            content.Add(title);
            content.Add(description);
            content.Add(status);
            
            // Action buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Column;
            buttonContainer.style.justifyContent = Justify.Center;
            buttonContainer.style.minWidth = 100;
            buttonContainer.style.alignSelf = Align.FlexEnd;
            
            var actionButton = new Button();
            actionButton.style.minWidth = 90;
            actionButton.style.height = 30;
            actionButton.name = "action-button";
            
            // Loading indicator (hidden by default)
            var loadingIndicator = new VisualElement();
            loadingIndicator.name = "loading-indicator";
            loadingIndicator.style.width = 16;
            loadingIndicator.style.height = 16;
            loadingIndicator.style.borderLeftWidth = 2;
            loadingIndicator.style.borderLeftColor = Color.white;
            loadingIndicator.style.display = DisplayStyle.None;
            loadingIndicator.style.marginLeft = 5;
            loadingIndicator.style.marginRight = 5;
            loadingIndicator.style.alignSelf = Align.Center;
            
            buttonContainer.Add(actionButton);
            buttonContainer.Add(loadingIndicator);
            
            container.Add(content);
            container.Add(buttonContainer);
            
            return container;
        }

        /// <summary>
        /// Binds data to an extension item in the list
        /// </summary>
        private void BindExtensionItem(VisualElement element, int index)
        {
            if (index >= m_FilteredExtensions.Count) return;
            
            var extension = m_FilteredExtensions[index];
            var container = element;
            
            // Get UI elements
            var title = container.Q<Label>("title");
            var description = container.Q<Label>("description");
            var status = container.Q<Label>("status");
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
            
            // Special handling for Hub Core Framework
            if (extension.Id == HUB_CORE_FRAMEWORK_ID)
            {
                if (extension.IsInstalled)
                {
                    if (extension.HasUpdate)
                    {
                        button.text = "Update";
                        button.clicked += () => UpdateExtension(extension);
                    }
                    else
                    {
                        button.text = "Up to Date";
                        button.SetEnabled(false);
                        button.style.opacity = 0.5f;
                    }
                }
                else
                {
                    button.text = "Core Package";
                    button.SetEnabled(false);
                    button.style.opacity = 0.5f;
                }
                return;
            }
            
            // Normal extension handling
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
            
            // Reset button state
            button.SetEnabled(true);
            button.style.opacity = 1f;
        }

        /// <summary>
        /// Creates the bottom status bar
        /// </summary>
        private void CreateStatusBar()
        {
            m_StatusBar = new VisualElement { name = NAME_STATUS_BAR };
            m_StatusBar.style.flexDirection = FlexDirection.Column;
            m_StatusBar.style.paddingTop = 5;
            m_StatusBar.style.paddingBottom = 5;
            m_StatusBar.style.paddingLeft = 10;
            m_StatusBar.style.paddingRight = 10;
            m_StatusBar.style.borderTopWidth = 1;
            m_StatusBar.style.borderTopColor = Color.gray;
            
            // Status label
            m_StatusLabel = new Label("Ready");
            m_StatusLabel.style.fontSize = 12;
            m_StatusLabel.style.marginBottom = 2;
            
            // Progress bar (hidden by default)
            m_ProgressBar = new ProgressBar();
            m_ProgressBar.style.height = 4;
            m_ProgressBar.style.display = DisplayStyle.None;
            m_ProgressBar.value = 0f;
            m_ProgressBar.lowValue = 0f;
            m_ProgressBar.highValue = 100f;
            
            m_StatusBar.Add(m_StatusLabel);
            m_StatusBar.Add(m_ProgressBar);
            m_Root.Add(m_StatusBar);
        }

        /// <summary>
        /// Sets up event handlers for UI interactions
        /// </summary>
        private void SetupEventHandlers()
        {
            // The search field callback is now registered in CreateToolbar
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
            Debug.Log($"{Consts.Log.Tag} Loaded {m_AvailableExtensions.Count} extensions");
            
            // Debug: 输出extension详情
            foreach(var ext in m_AvailableExtensions)
            {
                Debug.Log($"{Consts.Log.Tag} Extension: {ext.DisplayName} - Installed: {ext.IsInstalled} - Category: {ext.ExtensionCategory}");
            }
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
            ApplySearchFilter(query);
            FilterExtensionsByCategory(SelectedTabIndex);
        }

        /// <summary>
        /// Applies search filter to current filtered extensions
        /// </summary>
        private void ApplySearchFilter(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            query = query.ToLowerInvariant();
            var searchFiltered = m_FilteredExtensions.Where(ext => 
                ext.DisplayName.ToLowerInvariant().Contains(query) ||
                ext.Description.ToLowerInvariant().Contains(query) ||
                ext.Author.ToLowerInvariant().Contains(query) ||
                ext.Id.ToLowerInvariant().Contains(query)
            ).ToList();
            
            m_FilteredExtensions.Clear();
            m_FilteredExtensions.AddRange(searchFiltered);
        }

        /// <summary>
        /// Filters extensions by category
        /// </summary>
        private void FilterExtensionsByCategory(int categoryIndex)
        {
            m_FilteredExtensions.Clear();
            
            // Apply category filtering
            switch (categoryIndex)
            {
                case 0: // All Extensions
                    m_FilteredExtensions.AddRange(m_AvailableExtensions);
                    break;
                case 1: // Installed
                    m_FilteredExtensions.AddRange(m_AvailableExtensions.Where(ext => ext.IsInstalled));
                    break;
                case 2: // Available
                    m_FilteredExtensions.AddRange(m_AvailableExtensions.Where(ext => !ext.IsInstalled));
                    break;
                case 3: // Updates
                    m_FilteredExtensions.AddRange(m_AvailableExtensions.Where(ext => ext.HasUpdate));
                    break;
            }
            
            // Apply search filter if search field has content
            if (!string.IsNullOrEmpty(m_SearchField?.value))
            {
                ApplySearchFilter(m_SearchField.value);
            }
            
            // Rebuild the list view
            if (m_ExtensionList != null)
            {
                m_ExtensionList.itemsSource = m_FilteredExtensions;
                m_ExtensionList.Rebuild();
            }
            
            UpdateStatus($"Showing {m_FilteredExtensions.Count} extensions");
        }

        /// <summary>
        /// Gets the status text for an extension
        /// </summary>
        private string GetExtensionStatusText(ExtensionPackageInfo extension)
        {
            // Special status for Hub Core Framework
            if (extension.Id == HUB_CORE_FRAMEWORK_ID)
            {
                if (extension.IsInstalled)
                {
                    if (extension.HasUpdate)
                        return $"Core Framework v{extension.InstalledVersion} (Update available: v{extension.LatestVersion})";
                    return $"Core Framework v{extension.InstalledVersion} (Required)";
                }
                return $"Core Framework v{extension.LatestVersion} (Required)";
            }
            
            // Normal extension status
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
            // Prevent installation of Hub Core Framework
            if (extension.Id == HUB_CORE_FRAMEWORK_ID)
            {
                EditorUtility.DisplayDialog("Core Package", 
                    "Hub Core Framework is a required core package and cannot be installed separately.", 
                    "OK");
                return;
            }
            
            // Find the button for this extension
            var button = FindExtensionButton(extension);
            if (button == null) return;
            
            var originalText = button.text;
            SetButtonLoadingState(button, true, originalText);
            ShowProgressBar();
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
            finally
            {
                SetButtonLoadingState(button, false, originalText);
                HideProgressBar();
            }
        }

        /// <summary>
        /// Uninstalls an extension package
        /// </summary>
        private async void UninstallExtension(ExtensionPackageInfo extension)
        {
            // Prevent uninstallation of Hub Core Framework
            if (extension.Id == HUB_CORE_FRAMEWORK_ID)
            {
                EditorUtility.DisplayDialog("Core Package", 
                    "Hub Core Framework is a required core package and cannot be uninstalled.", 
                    "OK");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Uninstall Extension", 
                $"Are you sure you want to uninstall {extension.DisplayName}?", 
                "Uninstall", "Cancel"))
            {
                return;
            }
            
            // Find the button for this extension
            var button = FindExtensionButton(extension);
            if (button == null) return;
            
            var originalText = button.text;
            SetButtonLoadingState(button, true, originalText);
            ShowProgressBar();
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
            finally
            {
                SetButtonLoadingState(button, false, originalText);
                HideProgressBar();
            }
        }

        /// <summary>
        /// Updates an extension package
        /// </summary>
        private async void UpdateExtension(ExtensionPackageInfo extension)
        {
            // Find the button for this extension
            var button = FindExtensionButton(extension);
            if (button == null) return;
            
            var originalText = button.text;
            SetButtonLoadingState(button, true, originalText);
            ShowProgressBar();
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
            finally
            {
                SetButtonLoadingState(button, false, originalText);
                HideProgressBar();
            }
        }

        /// <summary>
        /// Finds the button for a specific extension in the list
        /// </summary>
        private Button FindExtensionButton(ExtensionPackageInfo extension)
        {
            if (m_ExtensionList == null) return null;
            
            // Find the index of the extension in the filtered list
            var index = m_FilteredExtensions.IndexOf(extension);
            if (index < 0 || index >= m_ExtensionList.itemsSource.Count) return null;
            
            // Get the visual element for this item
            var itemElement = m_ExtensionList.GetRootElementForIndex(index);
            if (itemElement == null) return null;
            
            // Find the button in the item
            return itemElement.Q<Button>();
        }

        /// <summary>
        /// Sets the loading state of a button
        /// </summary>
        private void SetButtonLoadingState(Button button, bool isLoading, string originalText = "")
        {
            if (isLoading)
            {
                button.SetEnabled(false);
                button.style.opacity = 0.6f;
                button.text = "Loading...";
                
                // Show loading indicator
                var loadingIndicator = button.parent?.Q<VisualElement>("loading-indicator");
                if (loadingIndicator != null)
                {
                    loadingIndicator.style.display = DisplayStyle.Flex;
                    StartLoadingAnimation(loadingIndicator);
                }
            }
            else
            {
                button.SetEnabled(true);
                button.style.opacity = 1f;
                button.text = originalText;
                
                // Hide loading indicator
                var loadingIndicator = button.parent?.Q<VisualElement>("loading-indicator");
                if (loadingIndicator != null)
                {
                    loadingIndicator.style.display = DisplayStyle.None;
                    StopLoadingAnimation(loadingIndicator);
                }
            }
        }

        /// <summary>
        /// Starts the loading animation for a visual element
        /// </summary>
        private void StartLoadingAnimation(VisualElement element)
        {
            if (element == null) return;
            
            // Create a simple pulsing animation instead of rotation
            var alpha = 0.3f;
            var increasing = true;
            element.schedule.Execute(() =>
            {
                if (increasing)
                {
                    alpha += 0.1f;
                    if (alpha >= 1.0f)
                    {
                        alpha = 1.0f;
                        increasing = false;
                    }
                }
                else
                {
                    alpha -= 0.1f;
                    if (alpha <= 0.3f)
                    {
                        alpha = 0.3f;
                        increasing = true;
                    }
                }
                element.style.opacity = alpha;
            }).Every(100);
        }

        /// <summary>
        /// Stops the loading animation for a visual element
        /// </summary>
        private void StopLoadingAnimation(VisualElement element)
        {
            if (element == null) return;
            
            // Reset opacity
            element.style.opacity = 1f;
        }

        /// <summary>
        /// Shows the progress bar with indeterminate progress
        /// </summary>
        private void ShowProgressBar()
        {
            if (m_ProgressBar != null)
            {
                m_ProgressBar.style.display = DisplayStyle.Flex;
                m_ProgressBar.value = 0f;
                
                // Start indeterminate progress animation
                m_ProgressBar.schedule.Execute(() =>
                {
                    m_ProgressBar.value = (m_ProgressBar.value + 5f) % 100f;
                }).Every(100);
            }
        }

        /// <summary>
        /// Hides the progress bar
        /// </summary>
        private void HideProgressBar()
        {
            if (m_ProgressBar != null)
            {
                m_ProgressBar.style.display = DisplayStyle.None;
                m_ProgressBar.value = 0f;
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