using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.MiAO.Unity.MCP.Editor.API;
using com.MiAO.Unity.MCP.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.MiAO.Unity.MCP.Editor
{
    public partial class MainWindowEditor : EditorWindow
    {
        // Tab state
        private enum TabType
        {
            Connector,
            ModelConfig,
            UserInput,
            Settings,
            UndoHistory
        }

        private TabType _currentTab = TabType.Connector;
        private Button _tabConnectorButton;
        private Button _tabModelConfigButton;
        private Button _tabUserInputButton;
        private Button _tabSettingsButton;
        private Button _tabUndoHistoryButton;
        private VisualElement _connectorContent;
        private VisualElement _modelConfigContent;
        private VisualElement _userInputContent;
        private VisualElement _settingsContent;
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
            _tabSettingsButton = root.Query<Button>("TabSettings").First();
            _tabUndoHistoryButton = root.Query<Button>("TabUndoHistory").First();
            
            // Get tab content
            _connectorContent = root.Query<VisualElement>("ConnectorContent").First();
            _modelConfigContent = root.Query<VisualElement>("ModelConfigContent").First();
            _userInputContent = root.Query<VisualElement>("UserInputContent").First();
            _settingsContent = root.Query<VisualElement>("SettingsContent").First();
            _undoContent = root.Query<VisualElement>("UndoContent").First();
            
            // Register tab switch events
            _tabConnectorButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.Connector));
            _tabModelConfigButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.ModelConfig));
            _tabUserInputButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.UserInput));
            _tabSettingsButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.Settings));
            _tabUndoHistoryButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.UndoHistory));
            
            // Initialize undo stack UI
            InitializeUndoStackUI(root);
            
            // Initialize settings page UI
            InitializeSettingsUI(root);
            
            // Initialize user input UI
            InitializeUserInputUI(root);
            
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

        private void SwitchTab(TabType tabType)
        {
            _currentTab = tabType;
            
            // Update button states
            _tabConnectorButton.RemoveFromClassList("tab-button-active");
            _tabModelConfigButton.RemoveFromClassList("tab-button-active");
            _tabUserInputButton.RemoveFromClassList("tab-button-active");
            _tabSettingsButton.RemoveFromClassList("tab-button-active");
            _tabUndoHistoryButton.RemoveFromClassList("tab-button-active");
            
            // Hide all content
            _connectorContent.style.display = DisplayStyle.None;
            _modelConfigContent.style.display = DisplayStyle.None;
            _userInputContent.style.display = DisplayStyle.None;
            _settingsContent.style.display = DisplayStyle.None;
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
                case TabType.Settings:
                    _tabSettingsButton.AddToClassList("tab-button-active");
                    _settingsContent.style.display = DisplayStyle.Flex;
                    // Ensure settings page shows latest values when opened
                    if (_languageSelector != null) LoadSettings();
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
                
                var success = SimpleUndoStack.Undo();
                if (success)
                {
                    EditorApplication.delayCall += () =>
                    {
                        RefreshUndoStackUI();
                        // RefreshUndoStackUI() already called RefreshButtonStates(), no need to call SetAllButtonsEnabled(true) again
                    };
                    Debug.Log("[√] Undo operation successful");
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
                
                var success = SimpleUndoStack.Redo();
                if (success)
                {
                    EditorApplication.delayCall += () =>
                    {
                        RefreshUndoStackUI();
                        // RefreshUndoStackUI() already called RefreshButtonStates(), no need to call SetAllButtonsEnabled(true) again
                    };
                    Debug.Log("[√] Redo operation successful");
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
                SimpleUndoStack.Clear();
                RefreshUndoStackUI();
                Debug.Log("[√] Undo stack cleared");
            }
        }

        private void RefreshUndoStackUI()
        {
            // Update status text
            var undoCount = SimpleUndoStack.GetUndoCount();
            var redoCount = SimpleUndoStack.GetRedoCount();
            var totalCount = undoCount + redoCount;
            _undoStackStatusText.text = LocalizationManager.GetText("operations.stack_status", totalCount);
            
            // Update operation history list
            UpdateUndoStackList();
            
            // Update button states (place at the end to ensure all UI elements are updated)
            RefreshButtonStates();
        }

        private void UpdateUndoStackList()
        {
            // Clear existing content
            _undoStackContainer.Clear();
            
            var undoCount = SimpleUndoStack.GetUndoCount();
            var redoCount = SimpleUndoStack.GetRedoCount();
            
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
                var undoHistory = SimpleUndoStack.GetUndoHistory();
                for (int i = 0; i < undoHistory.Count; i++)
                {
                    var operation = undoHistory[i];
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
                var redoHistory = SimpleUndoStack.GetRedoHistory();
                for (int i = 0; i < redoHistory.Count; i++)
                {
                    var operation = redoHistory[i];
                    var operationElement = CreateUndoStackItemElement(operation, i, false);
                    _undoStackContainer.Add(operationElement);
                }
            }
        }

        private VisualElement CreateUndoStackItemElement(SimpleUndoItem operation, int index, bool isUndoStack)
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
            
            // Operation name
            var titleElement = new Label();
            titleElement.AddToClassList("undo-stack-item-title");
            titleElement.text = operation.operationName;
            
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



        private string GetOperationIcon(string description)
        {
            if (description.Contains("创建") || description.Contains("Create") || description.Contains("创建GameObject"))
                return LocalizationManager.GetText("operations.icon.create");
            if (description.Contains("删除") || description.Contains("Delete") || description.Contains("Destroy"))
                return LocalizationManager.GetText("operations.icon.delete");
            if (description.Contains("修改") || description.Contains("Modify") || description.Contains("修改GameObject"))
                return LocalizationManager.GetText("operations.icon.modify");
            if (description.Contains("移动") || description.Contains("Move") || description.Contains("Parent"))
                return LocalizationManager.GetText("operations.icon.move");
            if (description.Contains("复制") || description.Contains("Copy") || description.Contains("Duplicate"))
                return LocalizationManager.GetText("operations.icon.copy");
            if (description.Contains("重命名") || description.Contains("Rename"))
                return LocalizationManager.GetText("operations.icon.rename");
            
            return LocalizationManager.GetText("operations.icon.unknown");
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
            var undoCount = SimpleUndoStack.GetUndoCount();
            var redoCount = SimpleUndoStack.GetRedoCount();
            
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
                if (_tabSettingsButton != null)
                    _tabSettingsButton.text = LocalizationManager.GetText("tab.settings");
                
                // Update MCP Connector tab content
                UpdateConnectorTabTexts();
                
                // Update Model Config tab content
                UpdateModelConfigTabTexts();
                
                // Update User Input tab content
                UpdateUserInputTabTexts();
                
                // Update Settings tab content
                UpdateSettingsTabTexts();
                
                // Update Operations tab content
                UpdateOperationsTabTexts();
                
                // Reload settings to update dropdown options
                if (_languageSelector != null && _themeSelector != null)
                {
                    LoadSettings();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update localized texts: {e.Message}");
            }
        }

        private void UpdateConnectorTabTexts()
        {
            var root = rootVisualElement;
            
            // Elements located by name
            var labelSettings = root.Query<Label>("labelSettings").First();
            if (labelSettings != null)
                labelSettings.text = LocalizationManager.GetText("connector.title");
                
            var dropdownLogLevel = root.Query<EnumField>("dropdownLogLevel").First();
            if (dropdownLogLevel != null)
                dropdownLogLevel.label = LocalizationManager.GetText("connector.loglevel");
                
            var inputServerURL = root.Query<TextField>("InputServerURL").First();
            if (inputServerURL != null)
                inputServerURL.label = LocalizationManager.GetText("connector.server_url");
                
            var rebuildButton = root.Query<Button>("btnRebuildServer").First();
            if (rebuildButton != null)
                rebuildButton.text = LocalizationManager.GetText("connector.rebuild_server");
            
            // Use dual check (English and Chinese) to find elements
            var connectServerLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Connect to MCP server") || 
                l.text.Contains("连接到 MCP 服务器")).ToList();
            foreach (var label in connectServerLabels)
            {
                label.text = LocalizationManager.GetText("connector.connect_server");
            }
            
            var infoFoldouts = root.Query<Foldout>().Where(f => 
                f.text.Contains("Information") || 
                f.text.Contains("信息")).ToList();
            foreach (var foldout in infoFoldouts)
            {
                foldout.text = LocalizationManager.GetText("connector.information");
            }
            
            var infoDescs = root.Query<Label>().Where(l => 
                l.text.Contains("Usually the server is hosted locally") || 
                l.text.Contains("通常服务器运行在本地地址")).ToList();
            foreach (var label in infoDescs)
            {
                label.text = LocalizationManager.GetText("connector.info_desc");
            }
            
            var configureClientLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Configure MCP Client") || 
                l.text.Contains("配置 MCP 客户端")).ToList();
            foreach (var label in configureClientLabels)
            {
                label.text = LocalizationManager.GetText("connector.configure_client");
            }
            
            var clientDescLabels = root.Query<Label>().Where(l => 
                l.text.Contains("At least one client should be configured") || 
                l.text.Contains("至少需要配置一个客户端")).ToList();
            foreach (var label in clientDescLabels)
            {
                label.text = LocalizationManager.GetText("connector.client_desc");
            }
            
            var manualConfigLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Manual configuration") || 
                l.text.Contains("手动配置")).ToList();
            foreach (var label in manualConfigLabels)
            {
                label.text = LocalizationManager.GetText("connector.manual_config");
            }
            
            var manualDescLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Copy paste the json") || 
                l.text.Contains("复制此 JSON 配置")).ToList();
            foreach (var label in manualDescLabels)
            {
                label.text = LocalizationManager.GetText("connector.manual_desc");
            }
            
            var checkLogsLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Please check the logs") || 
                l.text.Contains("请查看日志")).ToList();
            foreach (var label in checkLogsLabels)
            {
                label.text = LocalizationManager.GetText("connector.check_logs");
            }
        }

        private void UpdateModelConfigTabTexts()
        {
            var root = rootVisualElement;
            
            // Use dual check (English and Chinese) to find elements
            var modelTitles = root.Query<Label>().Where(l => 
                l.text.Contains("AI Model Configuration") || 
                l.text.Contains("AI 模型配置")).ToList();
            foreach (var label in modelTitles)
            {
                label.text = LocalizationManager.GetText("model.title");
            }
            
            // Elements located by name
            var configFoldout = root.Query<Foldout>("configFoldout").First();
            if (configFoldout != null)
                configFoldout.text = LocalizationManager.GetText("model.provider_settings");
                
            var saveConfigButton = root.Query<Button>("btnSaveConfig").First();
            if (saveConfigButton != null)
                saveConfigButton.text = LocalizationManager.GetText("model.save_config");
            
            // Use dual check to find settings foldouts
            var openaiSettings = root.Query<Foldout>().Where(f => 
                f.text.Contains("OpenAI Settings") || 
                f.text.Contains("OpenAI 设置")).ToList();
            foreach (var foldout in openaiSettings)
            {
                foldout.text = LocalizationManager.GetText("model.openai_settings");
            }
            
            var geminiSettings = root.Query<Foldout>().Where(f => 
                f.text.Contains("Gemini Settings") || 
                f.text.Contains("Gemini 设置")).ToList();
            foreach (var foldout in geminiSettings)
            {
                foldout.text = LocalizationManager.GetText("model.gemini_settings");
            }
            
            var claudeSettings = root.Query<Foldout>().Where(f => 
                f.text.Contains("Claude Settings") || 
                f.text.Contains("Claude 设置")).ToList();
            foreach (var foldout in claudeSettings)
            {
                foldout.text = LocalizationManager.GetText("model.claude_settings");
            }
            
            var localSettings = root.Query<Foldout>().Where(f => 
                f.text.Contains("Local Settings") || 
                f.text.Contains("本地设置")).ToList();
            foreach (var foldout in localSettings)
            {
                foldout.text = LocalizationManager.GetText("model.local_settings");
            }
            
            UpdateModelConfigFieldLabels(root);
        }
        
        private void UpdateModelConfigFieldLabels(VisualElement root)
        {
            // Update API Key labels
            var apiKeyFields = root.Query<TextField>().Where(f => f.name.Contains("ApiKey")).ToList();
            foreach (var field in apiKeyFields)
            {
                field.label = LocalizationManager.GetText("model.api_key");
            }
            
            // Update Model labels
            var modelFields = root.Query<TextField>().Where(f => f.name.Contains("Model") && !f.name.Contains("Provider")).ToList();
            foreach (var field in modelFields)
            {
                if (field.name == "localModel")
                    field.label = LocalizationManager.GetText("model.model");
                else if (field.name.EndsWith("Model"))
                    field.label = LocalizationManager.GetText("model.model");
            }
            
            // Update Base URL labels
            var baseUrlFields = root.Query<TextField>().Where(f => f.name.Contains("BaseUrl")).ToList();
            foreach (var field in baseUrlFields)
            {
                field.label = LocalizationManager.GetText("model.base_url");
            }
            
            // Update API URL labels
            var apiUrlFields = root.Query<TextField>().Where(f => f.name.Contains("localApiUrl")).ToList();
            foreach (var field in apiUrlFields)
            {
                field.label = LocalizationManager.GetText("model.api_url");
            }
            
            // Update Provider Selection
            var providerFoldouts = root.Query<Foldout>().Where(f => 
                f.text.Contains("Model Provider Selection") || 
                f.text.Contains("模型提供商选择")).ToList();
            foreach (var foldout in providerFoldouts)
            {
                foldout.text = LocalizationManager.GetText("model.provider_selection");
            }
            
            // Update General Settings
            var generalFoldouts = root.Query<Foldout>().Where(f => 
                f.text.Contains("General Settings") || 
                f.text.Contains("通用设置")).ToList();
            foreach (var foldout in generalFoldouts)
            {
                foldout.text = LocalizationManager.GetText("model.general_settings");
            }
        }

        private void UpdateUserInputTabTexts()
        {
            var root = rootVisualElement;
            
            // Update User Input tab content
            var userInputTitles = root.Query<Label>().Where(l => 
                l.text.Contains("User Input Panel") || 
                l.text.Contains("用户输入面板")).ToList();
            foreach (var label in userInputTitles)
            {
                label.text = LocalizationManager.GetText("userinput.title");
            }
            
            var promptLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Prompt Message:") || 
                l.text.Contains("提示信息:")).ToList();
            foreach (var label in promptLabels)
            {
                label.text = LocalizationManager.GetText("userinput.prompt_message");
            }
            
            var inputLabels = root.Query<Label>().Where(l => 
                l.text.Contains("User Input:") || 
                l.text.Contains("用户输入:")).ToList();
            foreach (var label in inputLabels)
            {
                label.text = LocalizationManager.GetText("userinput.user_input");
            }
            
            var objectLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Currently Selected Objects:") || 
                l.text.Contains("当前选中的对象:")).ToList();
            foreach (var label in objectLabels)
            {
                label.text = LocalizationManager.GetText("userinput.selected_objects");
            }
            
            var confirmButton = root.Query<Button>("btnConfirmInput").First();
            if (confirmButton != null)
                confirmButton.text = LocalizationManager.GetText("userinput.confirm");
                
            var cancelButton = root.Query<Button>("btnCancelInput").First();
            if (cancelButton != null)
                cancelButton.text = LocalizationManager.GetText("userinput.cancel");
        }

        private void UpdateSettingsTabTexts()
        {
            var root = rootVisualElement;
            
            // Use dual check (English and Chinese) to find elements
            var settingsTitles = root.Query<Label>().Where(l => 
                l.text.Contains("User Preferences") || 
                l.text.Contains("用户偏好")).ToList();
            foreach (var label in settingsTitles)
            {
                label.text = LocalizationManager.GetText("settings.title");
            }
            
            var languageFoldouts = root.Query<Foldout>().Where(f => 
                f.text.Contains("Language Settings") || 
                f.text.Contains("语言设置")).ToList();
            foreach (var foldout in languageFoldouts)
            {
                foldout.text = LocalizationManager.GetText("settings.language_settings");
            }
            
            // Elements located by reference
            if (_languageSelector != null)
                _languageSelector.label = LocalizationManager.GetText("settings.interface_language");
                
            var languageDescs = root.Query<Label>().Where(l => 
                l.text.Contains("Select your preferred language") || 
                l.text.Contains("选择您的首选语言")).ToList();
            foreach (var label in languageDescs)
            {
                label.text = LocalizationManager.GetText("settings.language_desc");
            }
            
            var themeFoldouts = root.Query<Foldout>().Where(f => 
                f.text.Contains("Theme Settings") || 
                f.text.Contains("主题设置")).ToList();
            foreach (var foldout in themeFoldouts)
            {
                foldout.text = LocalizationManager.GetText("settings.theme_settings");
            }
            
            if (_themeSelector != null)
                _themeSelector.label = LocalizationManager.GetText("settings.ui_theme");
                
            if (_autoRefreshToggle != null)
                _autoRefreshToggle.label = LocalizationManager.GetText("settings.auto_refresh");
                
            var themeDescs = root.Query<Label>().Where(l => 
                l.text.Contains("Configure the appearance") || 
                l.text.Contains("配置界面外观")).ToList();
            foreach (var label in themeDescs)
            {
                label.text = LocalizationManager.GetText("settings.theme_desc");
            }
            
            // Buttons located by name
            var saveButton = root.Query<Button>("btnSaveSettings").First();
            if (saveButton != null)
                saveButton.text = LocalizationManager.GetText("settings.save");
            
            var resetButton = root.Query<Button>("btnResetSettings").First();
            if (resetButton != null)
                resetButton.text = LocalizationManager.GetText("settings.reset");
        }

        private void UpdateOperationsTabTexts()
        {
            var root = rootVisualElement;
            
            // Use dual check (English and Chinese) to find elements
            var operationsTitles = root.Query<Label>().Where(l => 
                l.text.Contains("Operations Panel") || 
                l.text.Contains("操作面板")).ToList();
            foreach (var label in operationsTitles)
            {
                label.text = LocalizationManager.GetText("operations.title");
            }
            
            var undoStackFoldouts = root.Query<Foldout>().Where(f => 
                f.text.Contains("Undo Stack") || 
                f.text.Contains("撤销栈")).ToList();
            foreach (var foldout in undoStackFoldouts)
            {
                foldout.text = LocalizationManager.GetText("operations.undo_stack");
            }
            
            var historyLabels = root.Query<Label>().Where(l => 
                l.text.Contains("Operation History") || 
                l.text.Contains("操作历史")).ToList();
            foreach (var label in historyLabels)
            {
                label.text = LocalizationManager.GetText("operations.history");
            }
            
            // Elements located by reference
            if (_btnRefreshUndoStack != null)
                _btnRefreshUndoStack.text = LocalizationManager.GetText("operations.refresh");
                
            if (_btnUndoLast != null)
                _btnUndoLast.text = LocalizationManager.GetText("operations.undo");
                
            if (_btnRedoLast != null)
                _btnRedoLast.text = LocalizationManager.GetText("operations.redo");
                
            if (_emptyUndoStackLabel != null)
                _emptyUndoStackLabel.text = LocalizationManager.GetText("operations.no_history");
                
            if (_btnClearUndoStack != null)
                _btnClearUndoStack.text = LocalizationManager.GetText("operations.clear_stack");
                
            // Update status text
            if (_undoStackStatusText != null)
            {
                var count = SimpleUndoStack.GetUndoCount() + SimpleUndoStack.GetRedoCount();
                _undoStackStatusText.text = LocalizationManager.GetText("operations.stack_status", count);
            }
        }

        // Settings page UI element references
        private DropdownField _languageSelector;
        private DropdownField _themeSelector;
        private Toggle _autoRefreshToggle;

        // EditorPrefs key name constants
        private static readonly string PREF_LANGUAGE = "MCP.Settings.Language";
        private static readonly string PREF_THEME = "MCP.Settings.Theme";
        private static readonly string PREF_AUTO_REFRESH = "MCP.Settings.AutoRefresh";

        private void InitializeSettingsUI(VisualElement root)
        {
            // Get settings page UI elements
            _languageSelector = root.Query<DropdownField>("languageSelector").First();
            _themeSelector = root.Query<DropdownField>("themeSelector").First();
            _autoRefreshToggle = root.Query<Toggle>("autoRefreshToggle").First();
            var btnSaveSettings = root.Query<Button>("btnSaveSettings").First();
            var btnResetSettings = root.Query<Button>("btnResetSettings").First();
            
            // Initialize localization manager's current language
            var savedLanguage = EditorPrefs.GetString(PREF_LANGUAGE, "English");
            LocalizationManager.CurrentLanguage = LocalizationManager.StringToLanguage(savedLanguage);
            
            // Initialize selector options (using localization)
            UpdateSelectorChoices();
            
            // Load saved settings
            LoadSettings();
            
            // Register events
            btnSaveSettings.RegisterCallback<ClickEvent>(evt => OnSaveSettingsClicked());
            btnResetSettings.RegisterCallback<ClickEvent>(evt => OnResetSettingsClicked());
        }

        private void UpdateSelectorChoices()
        {
            if (_languageSelector != null)
            {
                _languageSelector.choices = new List<string> 
                { 
                    LocalizationManager.GetText("language.english"), 
                    LocalizationManager.GetText("language.chinese") 
                };
            }
            
            if (_themeSelector != null)
            {
                _themeSelector.choices = new List<string> 
                { 
                    LocalizationManager.GetText("theme.dark"), 
                    LocalizationManager.GetText("theme.light"), 
                    LocalizationManager.GetText("theme.auto") 
                };
            }
        }

        private void LoadSettings()
        {
            try
            {
                // Update selector options (ensure localization)
                UpdateSelectorChoices();
                
                // Load language settings
                var savedLanguage = EditorPrefs.GetString(PREF_LANGUAGE, "English");
                var localizedLanguage = ConvertLanguageToDisplay(savedLanguage);
                if (_languageSelector.choices.Contains(localizedLanguage))
                {
                    _languageSelector.value = localizedLanguage;
                }
                else
                {
                    _languageSelector.value = LocalizationManager.GetText("language.english");
                }

                // Load theme settings
                var savedTheme = EditorPrefs.GetString(PREF_THEME, "Dark");
                var localizedTheme = ConvertThemeToDisplay(savedTheme);
                if (_themeSelector.choices.Contains(localizedTheme))
                {
                    _themeSelector.value = localizedTheme;
                }
                else
                {
                    _themeSelector.value = LocalizationManager.GetText("theme.dark");
                }

                // Load auto-refresh settings
                _autoRefreshToggle.value = EditorPrefs.GetBool(PREF_AUTO_REFRESH, true);

                Debug.Log("[Settings] Settings loaded successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Settings] Failed to load settings: {e.Message}");
                // Use default values if loading fails
                ResetSettingsToDefaults();
            }
        }

        private string ConvertLanguageToDisplay(string savedLanguage)
        {
            return savedLanguage switch
            {
                "简体中文" => LocalizationManager.GetText("language.chinese"),
                "ChineseSimplified" => LocalizationManager.GetText("language.chinese"),
                _ => LocalizationManager.GetText("language.english")
            };
        }

        private string ConvertThemeToDisplay(string savedTheme)
        {
            return savedTheme switch
            {
                "Light" => LocalizationManager.GetText("theme.light"),
                "Auto" => LocalizationManager.GetText("theme.auto"),
                _ => LocalizationManager.GetText("theme.dark")
            };
        }

        private string ConvertDisplayToLanguage(string displayLanguage)
        {
            if (displayLanguage == LocalizationManager.GetText("language.chinese"))
                return "简体中文";
            return "English";
        }

        private string ConvertDisplayToTheme(string displayTheme)
        {
            if (displayTheme == LocalizationManager.GetText("theme.light"))
                return "Light";
            if (displayTheme == LocalizationManager.GetText("theme.auto"))
                return "Auto";
            return "Dark";
        }
        
        private void OnSaveSettingsClicked()
        {
            try
            {
                // Validate setting values
                if (!ValidateSettings())
                {
                    var errorMessage = LocalizationManager.GetText("dialog.invalid_settings");
                    EditorUtility.DisplayDialog(LocalizationManager.GetText("dialog.settings_error"), errorMessage, "OK");
                    return;
                }

                // Convert display values to storage values
                var languageToSave = ConvertDisplayToLanguage(_languageSelector.value);
                var themeToSave = ConvertDisplayToTheme(_themeSelector.value);
                
                // Check if language has changed
                var previousLanguage = EditorPrefs.GetString(PREF_LANGUAGE, "English");
                var languageChanged = previousLanguage != languageToSave;

                // Save language settings
                EditorPrefs.SetString(PREF_LANGUAGE, languageToSave);
                
                // Save theme settings
                EditorPrefs.SetString(PREF_THEME, themeToSave);
                
                // Save auto-refresh settings
                EditorPrefs.SetBool(PREF_AUTO_REFRESH, _autoRefreshToggle.value);
                
                // Update localization manager if language has changed
                if (languageChanged)
                {
                    LocalizationManager.CurrentLanguage = LocalizationManager.StringToLanguage(languageToSave);
                }
                
                Debug.Log($"[Settings] Settings saved successfully - Language: {languageToSave}, Theme: {themeToSave}, AutoRefresh: {_autoRefreshToggle.value}");
                
                // Show detailed save confirmation
                var settingsSummary = GetSettingsSummary();
                var successTitle = LocalizationManager.GetText("dialog.settings");
                var successMessage = LocalizationManager.GetText("dialog.save_success", settingsSummary);
                EditorUtility.DisplayDialog(successTitle, successMessage, "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Settings] Failed to save settings: {e.Message}");
                var errorTitle = LocalizationManager.GetText("dialog.settings_error");
                var errorMessage = LocalizationManager.GetText("dialog.save_failed", e.Message);
                EditorUtility.DisplayDialog(errorTitle, errorMessage, "OK");
            }
        }
        
        private void OnResetSettingsClicked()
        {
            var title = LocalizationManager.GetText("dialog.reset_settings_title");
            var message = LocalizationManager.GetText("dialog.reset_settings_message");
            var resetButton = LocalizationManager.GetText("dialog.reset");
            var cancelButton = LocalizationManager.GetText("dialog.cancel");
                
            if (EditorUtility.DisplayDialog(title, message, resetButton, cancelButton))
            {
                try
                {
                    ResetSettingsToDefaults();
                    
                    Debug.Log("[Settings] Settings reset to defaults successfully");
                    var successTitle = LocalizationManager.GetText("dialog.settings");
                    var successMessage = LocalizationManager.GetText("dialog.reset_success");
                    EditorUtility.DisplayDialog(successTitle, successMessage, "OK");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Settings] Failed to reset settings: {e.Message}");
                    var errorTitle = LocalizationManager.GetText("dialog.settings_error");
                    var errorMessage = LocalizationManager.GetText("dialog.reset_failed", e.Message);
                    EditorUtility.DisplayDialog(errorTitle, errorMessage, "OK");
                }
            }
        }

        private void ResetSettingsToDefaults()
        {
            // Reset localization manager to default language
            LocalizationManager.CurrentLanguage = LocalizationManager.Language.English;
            
            // Update selector options
            UpdateSelectorChoices();
            
            // Set default values (using localized text)
            _languageSelector.value = LocalizationManager.GetText("language.english");
            _themeSelector.value = LocalizationManager.GetText("theme.dark");
            _autoRefreshToggle.value = true;
            
            // Save default values to EditorPrefs
            EditorPrefs.SetString(PREF_LANGUAGE, "English");
            EditorPrefs.SetString(PREF_THEME, "Dark");
            EditorPrefs.SetBool(PREF_AUTO_REFRESH, true);
        }

        /// <summary>
        /// Get current language setting
        /// </summary>
        public static string GetCurrentLanguage()
        {
            return EditorPrefs.GetString(PREF_LANGUAGE, "English");
        }

        /// <summary>
        /// Get current theme setting
        /// </summary>
        public static string GetCurrentTheme()
        {
            return EditorPrefs.GetString(PREF_THEME, "Dark");
        }

        /// <summary>
        /// Get auto-refresh setting
        /// </summary>
        public static bool GetAutoRefreshEnabled()
        {
            return EditorPrefs.GetBool(PREF_AUTO_REFRESH, true);
        }

        /// <summary>
        /// Clear all saved settings
        /// </summary>
        public static void ClearAllSettings()
        {
            EditorPrefs.DeleteKey(PREF_LANGUAGE);
            EditorPrefs.DeleteKey(PREF_THEME);
            EditorPrefs.DeleteKey(PREF_AUTO_REFRESH);
            Debug.Log("[Settings] All settings cleared");
        }

        /// <summary>
        /// Validate setting value validity
        /// </summary>
        private bool ValidateSettings()
        {
            // Validate language settings
            if (!_languageSelector.choices.Contains(_languageSelector.value))
            {
                Debug.LogWarning($"[Settings] Invalid language setting: {_languageSelector.value}");
                return false;
            }

            // Validate theme settings
            if (!_themeSelector.choices.Contains(_themeSelector.value))
            {
                Debug.LogWarning($"[Settings] Invalid theme setting: {_themeSelector.value}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get summary information of all current settings
        /// </summary>
        public static string GetSettingsSummary()
        {
            var summaryTitle = LocalizationManager.GetText("summary.title");
            var languageLabel = LocalizationManager.GetText("summary.language");
            var themeLabel = LocalizationManager.GetText("summary.theme");
            var autoRefreshLabel = LocalizationManager.GetText("summary.auto_refresh");
            var enabledText = LocalizationManager.GetText("text.enabled");
            var disabledText = LocalizationManager.GetText("text.disabled");
            
            var currentLang = GetCurrentLanguage();
            var displayLang = currentLang == "简体中文" ? LocalizationManager.GetText("language.chinese") : LocalizationManager.GetText("language.english");
            
            var currentTheme = GetCurrentTheme();
            var displayTheme = currentTheme switch
            {
                "Light" => LocalizationManager.GetText("text.light"),
                "Auto" => LocalizationManager.GetText("text.auto"),
                _ => LocalizationManager.GetText("text.dark")
            };
            
            var summary = $"{summaryTitle}\n" +
                         $"{languageLabel}{displayLang}\n" +
                         $"{themeLabel}{displayTheme}\n" +
                         $"{autoRefreshLabel}{(GetAutoRefreshEnabled() ? enabledText : disabledText)}";
            return summary;
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
                _statusText.text = "Waiting for user input...";
                
                // Update selected objects display
                UpdateSelectedObjectsDisplay();
            }
            else
            {
                _promptMessageSection.style.display = DisplayStyle.None;
                _userInputSection.style.display = DisplayStyle.None;
                _objectSelectionSection.style.display = DisplayStyle.None;
                _buttonSection.style.display = DisplayStyle.None;
                _statusText.text = "Waiting for prompt...";
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
                    _selectedObjectsText.text = "No objects selected";
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
    }
} 