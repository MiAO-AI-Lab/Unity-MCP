using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using com.MiAO.MCP.Common;
using com.MiAO.MCP.Utils;
using com.MiAO.MCP.Editor.API;
using com.MiAO.MCP.Editor.Common;
using com.MiAO.MCP.Editor.Localization;
using com.MiAO.MCP.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.MiAO.MCP.Editor
{
    public partial class MainWindowEditor : EditorWindow
    {
        // Tab state
        private enum TabType
        {
            Connector,
            ModelConfig,
            UserInput,
            UndoHistory
        }

        private TabType _currentTab = TabType.Connector;
        private Button _tabConnectorButton;
        private Button _tabModelConfigButton;
        private Button _tabUserInputButton;
        private Button _tabUndoHistoryButton;
        private VisualElement _connectorContent;
        private VisualElement _modelConfigContent;
        private VisualElement _userInputContent;
        private VisualElement _undoContent;
        
        // Undo stack UI elements
        private Label _undoStackStatusText;
        private Button _btnUndoLast;
        private Button _btnRedoLast;
        private Button _btnRefreshUndoStack;
        private Button _btnClearUndoStack;
        private VisualElement _undoStackContainer;
        private Label _emptyUndoStackLabel;

        private void InitializeTabSystem(VisualElement root)
        {
            // Get tab buttons
            _tabConnectorButton = root.Query<Button>("TabConnector").First();
            _tabModelConfigButton = root.Query<Button>("TabModelConfig").First();
            _tabUserInputButton = root.Query<Button>("TabUserInput").First();
            _tabUndoHistoryButton = root.Query<Button>("TabUndoHistory").First();
            
            // Get tab content
            _connectorContent = root.Query<VisualElement>("ConnectorContent").First();
            _modelConfigContent = root.Query<VisualElement>("ModelConfigContent").First();
            _userInputContent = root.Query<VisualElement>("UserInputContent").First();
            _undoContent = root.Query<VisualElement>("UndoContent").First();
            
            // Register tab switch events
            _tabConnectorButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.Connector));
            _tabModelConfigButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.ModelConfig));
            _tabUserInputButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.UserInput));
            _tabUndoHistoryButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.UndoHistory));

            // Initialize undo stack UI
            InitializeUndoStackUI(root);

            // Initialize user input UI
            InitializeUserInputUI(root);

            // Initialize language from HubSettings
            InitializeLanguageFromHubSettings();

            // Register localization events
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            
            // Initialize localized text
            UpdateLocalizedTexts();
        }

        private void OnDestroy()
        {
            // Unregister localization events to avoid memory leaks
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            
            // Unregister EditorApplication.update event to avoid memory leaks
            EditorApplication.update -= UpdateSelectedObjectsDisplay;
        }
        
        /// <summary>
        /// Handle undo operations changes for auto-refresh
        /// </summary>
        private void OnUndoOperationsChanged()
        {
            // Only refresh if the undo tab is currently active to avoid unnecessary UI updates
            if (_currentTab == TabType.UndoHistory)
            {
                // Immediately refresh UI - reduce delay perception
                RefreshUndoStackUI();
            }
        }

        private void SwitchTab(TabType tabType)
        {
            _currentTab = tabType;
            
            // Update button states
            _tabConnectorButton.RemoveFromClassList("tab-button-active");
            _tabModelConfigButton.RemoveFromClassList("tab-button-active");
            _tabUserInputButton.RemoveFromClassList("tab-button-active");
            _tabUndoHistoryButton.RemoveFromClassList("tab-button-active");
            
            // Hide all content
            _connectorContent.style.display = DisplayStyle.None;
            _modelConfigContent.style.display = DisplayStyle.None;
            _userInputContent.style.display = DisplayStyle.None;
            _undoContent.style.display = DisplayStyle.None;
            
            // Show current tab content
            switch (tabType)
            {
                case TabType.Connector:
                    _tabConnectorButton.AddToClassList("tab-button-active");
                    _connectorContent.style.display = DisplayStyle.Flex;
                    break;
                case TabType.ModelConfig:
                    _tabModelConfigButton.AddToClassList("tab-button-active");
                    _modelConfigContent.style.display = DisplayStyle.Flex;
                    break;
                case TabType.UserInput:
                    _tabUserInputButton.AddToClassList("tab-button-active");
                    _userInputContent.style.display = DisplayStyle.Flex;
                    RefreshUserInputUI();
                    break;
                case TabType.UndoHistory:
                    _tabUndoHistoryButton.AddToClassList("tab-button-active");
                    _undoContent.style.display = DisplayStyle.Flex;
                    RefreshUndoStackUI();
                    break;
            }
        }

        private void InitializeUndoStackUI(VisualElement root)
        {
            // Get undo stack UI elements
            _undoStackStatusText = root.Query<Label>("undoStackStatusText").First();
            _btnUndoLast = root.Query<Button>("btnUndoLast").First();
            _btnRedoLast = root.Query<Button>("btnRedoLast").First();
            _btnRefreshUndoStack = root.Query<Button>("btnRefreshUndoStack").First();
            _btnClearUndoStack = root.Query<Button>("btnClearUndoStack").First();
            _undoStackContainer = root.Query<VisualElement>("undoStackContainer").First();
            _emptyUndoStackLabel = root.Query<Label>("emptyUndoStackLabel").First();
            
            // Register button events
            _btnUndoLast.RegisterCallback<ClickEvent>(evt => OnUndoLastClicked());
            _btnRedoLast.RegisterCallback<ClickEvent>(evt => OnRedoLastClicked());
            _btnRefreshUndoStack.RegisterCallback<ClickEvent>(evt => RefreshUndoStackUI());
            _btnClearUndoStack.RegisterCallback<ClickEvent>(evt => OnClearUndoStackClicked());
            
            // Initialize UI state
            RefreshUndoStackUI();
        }

        private void OnUndoLastClicked()
        {
            try
            {
                SetAllButtonsEnabled(false);
                
                // Force check for new operations before undo to prevent missing operations due to polling delay
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Use our UnityUndoMonitor system for proper stack management
                if (UnityUndoMonitor.GetUndoCount() > 0)
                {
                    UnityUndoMonitor.PerformUndo();
                    EditorApplication.delayCall += () =>
                    {
                        RefreshUndoStackUI();
                        // RefreshUndoStackUI() already called RefreshButtonStates(), no need to call SetAllButtonsEnabled(true) again
                    };
                }
                else
                {
                    RefreshButtonStates();
                    Debug.LogWarning("[!] No operations to undo");
                }
            }
            catch (Exception e)
            {
                RefreshButtonStates();
                Debug.LogError($"[X] Undo operation failed: {e.Message}");
            }
        }

        private void OnRedoLastClicked()
        {
            try
            {
                SetAllButtonsEnabled(false);
                
                // Force check for new operations before redo to prevent missing operations due to polling delay
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Use our UnityUndoMonitor system for proper stack management
                if (UnityUndoMonitor.GetRedoCount() > 0)
                {
                    UnityUndoMonitor.PerformRedo();
                    EditorApplication.delayCall += () =>
                    {
                        RefreshUndoStackUI();
                        // RefreshUndoStackUI() already called RefreshButtonStates(), no need to call SetAllButtonsEnabled(true) again
                    };
                }
                else
                {
                    RefreshButtonStates();
                    Debug.LogWarning("[!] No operations to redo");
                }
            }
            catch (Exception e)
            {
                RefreshButtonStates();
                Debug.LogError($"[X] Redo operation failed: {e.Message}");
            }
        }

        private void OnClearUndoStackClicked()
        {
            var title = LocalizationManager.GetText("dialog.clear_undo_stack_title");
            var message = LocalizationManager.GetText("dialog.clear_undo_stack_message");
            var clearButton = LocalizationManager.GetText("dialog.clear");
            var cancelButton = LocalizationManager.GetText("dialog.cancel");
                
            if (EditorUtility.DisplayDialog(title, message, clearButton, cancelButton))
            {
                // Force check for new operations before clear to prevent missing operations due to polling delay
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Use Unity's native undo clear + monitor clear
                Undo.ClearAll();
                UnityUndoMonitor.ClearHistory();
                RefreshUndoStackUI();
            }
        }

        private void RefreshUndoStackUI()
        {
            // Temporarily disable undo monitoring during UI refresh to prevent false detection of operations
            UnityUndoMonitor.SetUIRefreshState(true);
            
            try
            {
                // Force check for new operations before refreshing UI
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Update status text using UnityUndoMonitor
                var undoCount = UnityUndoMonitor.GetUndoCount();
                var redoCount = UnityUndoMonitor.GetRedoCount();
                var totalCount = undoCount + redoCount;
                _undoStackStatusText.text = LocalizationManager.GetText("operations.stack_status", totalCount);
                
                // Update operation history list
                UpdateUndoStackList();
                
                // Update button states (place at the end to ensure all UI elements are updated)
                RefreshButtonStates();
            }
            finally
            {
                // Immediately re-enable undo monitoring to reduce impact on subsequent operation detection
                UnityUndoMonitor.SetUIRefreshState(false);
            }
        }

        private void UpdateUndoStackList()
        {
            // Clear existing content
            _undoStackContainer.Clear();
            
            var undoCount = UnityUndoMonitor.GetUndoCount();
            var redoCount = UnityUndoMonitor.GetRedoCount();
            
            if (undoCount == 0 && redoCount == 0)
            {
                _undoStackContainer.Add(_emptyUndoStackLabel);
                return;
            }
            
            // Add undo stack header
            if (undoCount > 0)
            {
                var undoHeader = new Label(LocalizationManager.GetText("operations.undo_stack_header"));
                undoHeader.AddToClassList("undo-stack-section-header");
                _undoStackContainer.Add(undoHeader);
                
                // Add undo stack operations (display in order, newest at the top)
                var undoHistory = UnityUndoMonitor.GetUndoHistory();
                // Reverse the list to show newest operations first
                var reversedUndoHistory = undoHistory.AsEnumerable().Reverse().ToArray();
                for (int i = 0; i < reversedUndoHistory.Length; i++)
                {
                    var operation = reversedUndoHistory[i];
                    var operationElement = CreateUndoStackItemElement(operation, i, true);
                    _undoStackContainer.Add(operationElement);
                }
            }
            
            // Add redo stack header
            if (redoCount > 0)
            {
                var redoHeader = new Label(LocalizationManager.GetText("operations.redo_stack_header"));
                redoHeader.AddToClassList("undo-stack-section-header");
                _undoStackContainer.Add(redoHeader);
                
                // Add redo stack operations (display in order, newest at the top)
                var redoHistory = UnityUndoMonitor.GetRedoHistory();
                // GetRedoHistory() already returns reversed list (newest first), so no need to reverse again
                for (int i = 0; i < redoHistory.Count; i++)
                {
                    var operation = redoHistory[i];
                    var operationElement = CreateUndoStackItemElement(operation, i, false);
                    _undoStackContainer.Add(operationElement);
                }
            }
        }

        private VisualElement CreateUndoStackItemElement(UnityUndoMonitor.UndoOperation operation, int index, bool isUndoStack)
        {
            var itemElement = new VisualElement();
            itemElement.AddToClassList("undo-stack-item");
            
            // Header row
            var headerElement = new VisualElement();
            headerElement.AddToClassList("undo-stack-item-header");
            
            // Icon
            var iconElement = new Label();
            iconElement.AddToClassList("undo-stack-item-icon");
            iconElement.text = GetOperationIcon(operation.operationName);
            
            // Operation name with source indicator
            var titleElement = new Label();
            titleElement.AddToClassList("undo-stack-item-title");
            titleElement.text = operation.ParsedDisplayName; // Use ParsedDisplayName to show formatted [Operation] [Source] description
            
            // Timestamp
            var timeElement = new Label();
            timeElement.AddToClassList("undo-stack-item-time");
            timeElement.text = operation.timestamp.ToString("HH:mm:ss");
            
            // Add index identifier (latest operation at the top)
            var indexElement = new Label();
            indexElement.AddToClassList("undo-stack-item-index");
            if (isUndoStack)
            {
                // For undo stack, index 0 is the latest operation
                indexElement.text = index == 0 ? LocalizationManager.GetText("operations.latest") : $"#{index + 1}";
            }
            else
            {
                // For redo stack, index 0 is the latest operation
                indexElement.text = index == 0 ? LocalizationManager.GetText("operations.latest") : $"#{index + 1}";
            }
            
            // Assemble header row
            headerElement.Add(iconElement);
            headerElement.Add(titleElement);
            headerElement.Add(timeElement);
            headerElement.Add(indexElement);
            
            itemElement.Add(headerElement);
            
            return itemElement;
        }

        private string GetOperationIcon(string operationName)
        {
            // 使用解析后的操作类型来判断图标
            var (operationType, _) = UnityUndoMonitor.UndoOperation.ParseOperationName(operationName);
            var lowerOperationType = operationType.ToLower();
            
            if (lowerOperationType.Contains("create") || lowerOperationType.Contains("instantiate") || lowerOperationType.Contains("spawn"))
                return "Create";
            if (lowerOperationType.Contains("delete") || lowerOperationType.Contains("destroy"))
                return "Delete";
            if (lowerOperationType.Contains("modify") || lowerOperationType.Contains("edit") || lowerOperationType.Contains("change") || lowerOperationType.Contains("transform"))
                return "Modify";
            if (lowerOperationType.Contains("move") || lowerOperationType.Contains("parent") || lowerOperationType.Contains("position"))
                return "Move";
            if (lowerOperationType.Contains("copy") || lowerOperationType.Contains("duplicate") || lowerOperationType.Contains("clone") || lowerOperationType.Contains("drag"))
                return "Copy";
            if (lowerOperationType.Contains("rename"))
                return "Rename";
            if (lowerOperationType.Contains("select") || lowerOperationType.Contains("clear"))
                return "Select";
            
            return "Unknown";
        }

        private void SetAllButtonsEnabled(bool enabled)
        {
            if (enabled)
            {
                // When re-enabling, need to set buttons based on current state
                RefreshButtonStates();
            }
            else
            {
                // Disable all main buttons
                _btnUndoLast?.SetEnabled(false);
                _btnRedoLast?.SetEnabled(false);
                _btnRefreshUndoStack?.SetEnabled(false);
                _btnClearUndoStack?.SetEnabled(false);
            }
        }

        private void RefreshButtonStates()
        {
            var undoCount = UnityUndoMonitor.GetUndoCount();
            var redoCount = UnityUndoMonitor.GetRedoCount();
            
            // Set main buttons based on stack state
            _btnUndoLast?.SetEnabled(undoCount > 0);
            _btnRedoLast?.SetEnabled(redoCount > 0);
            _btnRefreshUndoStack?.SetEnabled(true); // Refresh button is always available
            _btnClearUndoStack?.SetEnabled(undoCount > 0 || redoCount > 0);
        }

        private void OnLanguageChanged(LocalizationManager.Language newLanguage)
        {
            UpdateLocalizedTexts();
        }

        private void UpdateLocalizedTexts()
        {
            try
            {
                // Update window title
                UpdateWindowTitle();
                
                // Update tab titles
                if (_tabConnectorButton != null)
                    _tabConnectorButton.text = LocalizationManager.GetText("tab.connector");
                if (_tabModelConfigButton != null)
                    _tabModelConfigButton.text = LocalizationManager.GetText("tab.modelconfig");
                if (_tabUserInputButton != null)
                    _tabUserInputButton.text = LocalizationManager.GetText("tab.userinput");
                if (_tabUndoHistoryButton != null)
                    _tabUndoHistoryButton.text = LocalizationManager.GetText("tab.operations");

                // Update MCP Connector tab content
                // Use new localization system instead of manual text updates
                LocalizationAdapter.LocalizeUITree(rootVisualElement);

                // All UI text updates (including Model Config) are now handled by the new localization system

                // Reload settings to update dropdown options
                // if (_languageSelector != null && _themeSelector != null) // Removed as per edit hint
                // {
                //     LoadSettings();
                // }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update localized texts: {e.Message}");
            }
        }

        /// <summary>
        /// Refresh all client configuration status to update localized text
        /// </summary>
        private void RefreshAllClientConfigurationStatus()
        {
            try
            {
                // This method ensures that all client status buttons and labels 
                // are updated with the correct localized text when language changes
                
                var root = rootVisualElement;
                
                // Update all "Configured"/"Not Configured" status labels
                var configuredLabels = root.Query<Label>().Where(l => 
                    l.text == "Configured" || 
                    l.text == "Not Configured" ||
                    l.text == "已配置" || 
                    l.text == "未配置").ToList();
                
                foreach (var label in configuredLabels)
                {
                    // Update status text based on current state
                    if (label.text == "Configured" || label.text == "已配置")
                    {
                        label.text = LocalizationManager.GetText("connector.configured");
                    }
                    else if (label.text == "Not Configured" || label.text == "未配置")
                    {
                        label.text = LocalizationManager.GetText("connector.not_configured");
                    }
                }
                
                // Update all "Configure"/"Reconfigure" buttons
                var configButtons = root.Query<Button>().Where(b => 
                    b.text == "Configure" || 
                    b.text == "Reconfigure" ||
                    b.text == "配置" || 
                    b.text == "重新配置").ToList();
                
                foreach (var button in configButtons)
                {
                    // Update button text based on current state
                    if (button.text == "Reconfigure" || button.text == "重新配置")
                    {
                        button.text = LocalizationManager.GetText("connector.reconfigure");
                    }
                    else if (button.text == "Configure" || button.text == "配置")
                    {
                        button.text = LocalizationManager.GetText("connector.configure");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RefreshClientConfigurationStatus] Error refreshing client configuration status: {ex.Message}");
            }
        }

        // ==================== User Input Tab ====================
        
        // User Input UI elements
        private VisualElement _promptMessageSection;
        private Label _promptMessageText;
        private VisualElement _userInputSection;
        private TextField _userInputField;
        private VisualElement _objectSelectionSection;
        private Label _selectedObjectsText;
        private VisualElement _buttonSection;
        private Button _btnConfirmInput;
        private Button _btnCancelInput;
        private Label _statusText;
        
        // User Input state
        private bool _userInputActive = false;
        private string _currentWindowId = "";
        private System.Action<string> _userInputCallback;
        private string _mode = "full";

        private void InitializeUserInputUI(VisualElement root)
        {
            // Get UI elements
            _promptMessageSection = root.Query<VisualElement>("promptMessageSection").First();
            _promptMessageText = root.Query<Label>("promptMessageText").First();
            _userInputSection = root.Query<VisualElement>("userInputSection").First();
            _userInputField = root.Query<TextField>("userInputField").First();
            _objectSelectionSection = root.Query<VisualElement>("objectSelectionSection").First();
            _selectedObjectsText = root.Query<Label>("selectedObjectsText").First();
            _buttonSection = root.Query<VisualElement>("buttonSection").First();
            _btnConfirmInput = root.Query<Button>("btnConfirmInput").First();
            _btnCancelInput = root.Query<Button>("btnCancelInput").First();
            _statusText = root.Query<Label>("statusText").First();
            
            // Register button events
            _btnConfirmInput.RegisterCallback<ClickEvent>(evt => OnConfirmInputClicked());
            _btnCancelInput.RegisterCallback<ClickEvent>(evt => OnCancelInputClicked());
            
            // Update selected objects display periodically
            EditorApplication.update += UpdateSelectedObjectsDisplay;
            
            // Initialize UI state
            RefreshUserInputUI();
        }

        private void RefreshUserInputUI()
        {
            if (_userInputActive)
            {
                _promptMessageSection.style.display = DisplayStyle.Flex;
                _userInputSection.style.display = DisplayStyle.Flex;
                _objectSelectionSection.style.display = DisplayStyle.Flex;
                _buttonSection.style.display = DisplayStyle.Flex;
                _statusText.text = LocalizationManager.GetText("userinput.waiting_for_input");
                
                // Update selected objects display
                UpdateSelectedObjectsDisplay();
            }
            else
            {
                _promptMessageSection.style.display = DisplayStyle.None;
                _userInputSection.style.display = DisplayStyle.None;
                _objectSelectionSection.style.display = DisplayStyle.None;
                _buttonSection.style.display = DisplayStyle.None;
                _statusText.text = LocalizationManager.GetText("userinput.waiting_for_prompt");
            }
        }

        private void OnConfirmInputClicked()
        {
            var userInputText = _userInputField.value ?? "";
            var selectedObjects = Selection.objects;

            var result = "";
            if (_mode == "full")
            {
                result = $"[Success] User input completed.";
                result += $"\nUser input text: '{userInputText}'";
                
                if (selectedObjects.Length > 0)
                {
                    result += $"\nSelected objects count: {selectedObjects.Length}";
                    result += $"\nSelected objects list:";
                    for (int i = 0; i < selectedObjects.Length; i++)
                    {
                        if (selectedObjects[i] != null)
                        {
                            var obj = selectedObjects[i];
                            var path = "";
                            if (obj is GameObject go)
                            {
                                path = GetGameObjectPath(go);
                            }
                            result += $"\n  [{i + 1}] {obj.name} ({obj.GetType().Name}) - InstanceID: {obj.GetInstanceID()}";
                            if (!string.IsNullOrEmpty(path))
                            {
                                result += $" - Path: {path}";
                            }
                        }
                    }
                }
            }
            else if (_mode == "clean")
            {
                result = userInputText;
            }
            else if (_mode == "json")
            {
                result = JsonSerializer.Serialize(new {
                    userInput = userInputText,
                    selectedObjects = selectedObjects.Select(obj => new {
                        name = obj.name,
                        type = obj.GetType().Name,
                        instanceId = obj.GetInstanceID()
                    }).ToArray()
                });
            }
            
            CompleteUserInput(result);
        }

        private void OnCancelInputClicked()
        {
            CompleteUserInput("[Cancelled] User cancelled the input.");
        }

        private void CompleteUserInput(string result)
        {
            _userInputActive = false;
            _userInputCallback?.Invoke(result);
            _userInputCallback = null;
            _currentWindowId = "";
            
            // Clear input field
            _userInputField.value = "";
            
            // Refresh UI
            RefreshUserInputUI();
        }

        private void UpdateSelectedObjectsDisplay()
        {
            if (_selectedObjectsText != null)
            {
                var selectedObjects = Selection.objects;
                if (selectedObjects.Length > 0)
                {
                    var text = $"Selected {selectedObjects.Length} objects:\n";
                    for (int i = 0; i < selectedObjects.Length && i < 10; i++)
                    {
                        if (selectedObjects[i] != null)
                        {
                            text += $"• {selectedObjects[i].name} ({selectedObjects[i].GetType().Name})\n";
                        }
                    }
                    if (selectedObjects.Length > 10)
                    {
                        text += $"... and {selectedObjects.Length - 10} more objects";
                    }
                    _selectedObjectsText.text = text;
                }
                else
                {
                    _selectedObjectsText.text = LocalizationManager.GetText("userinput.no_objects");
                }
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "";
            
            var path = go.name;
            var parent = go.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        /// <summary>
        /// Show user input UI and switch to UserInput tab
        /// </summary>
        public void ShowUserInputUI(string promptMessage, string windowId, string mode, System.Action<string> callback)
        {
            _userInputActive = true;
            _currentWindowId = windowId;
            _userInputCallback = callback;
            _promptMessageText.text = promptMessage;
            _mode = mode;
            
            // Switch to UserInput tab
            SwitchTab(TabType.UserInput);
            
            // Focus the window
            Focus();
            
            // Refresh UI
            RefreshUserInputUI();
            
            // Focus the input field
            _userInputField.Focus();
        }

        /// <summary>
        /// Hide user input UI
        /// </summary>
        public void HideUserInputUI()
        {
            _userInputActive = false;
            _userInputCallback = null;
            _currentWindowId = "";
            _userInputField.value = "";
            RefreshUserInputUI();
        }

        /// <summary>
        /// Get current user input window ID
        /// </summary>
        public string GetCurrentUserInputWindowId()
        {
            return _currentWindowId;
        }

        /// <summary>
        /// Check if user input is active
        /// </summary>
        public bool IsUserInputActive()
        {
            return _userInputActive;
        }

        private void InitializeLanguageFromHubSettings()
        {
            // Initialize localization manager's current language from HubSettings
            var savedLanguage = McpHubSettingsWindow.Settings.CurrentLanguage;
            LocalizationManager.CurrentLanguage = LocalizationManager.StringToLanguage(savedLanguage);
        }

        /// <summary>
        /// Get current language setting (now from HubSettings)
        /// </summary>
        public static string GetCurrentLanguage()
        {
            return McpHubSettingsWindow.Settings.CurrentLanguage;
        }

        /// <summary>
        /// Get current theme setting (now from HubSettings)
        /// </summary>
        public static string GetCurrentTheme()
        {
            return McpHubSettingsWindow.Settings.CurrentTheme;
        }
    }
} 