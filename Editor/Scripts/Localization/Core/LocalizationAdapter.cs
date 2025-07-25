using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;
using com.MiAO.Unity.MCP.Editor.Localization.Providers;
using com.MiAO.Unity.MCP.Editor.Localization.Extensions;

namespace com.MiAO.Unity.MCP.Editor.Localization
{
    /// <summary>
    /// Localization adapter
    /// 
    /// Unified management of UI element localization mapping and processing, providing:
    /// 1. Standard text to localization key mapping
    /// 2. Programmatic UI element configuration registration  
    /// 3. Complete UI tree localization processing
    /// 4. Debugging and diagnostic tools
    /// </summary>
    public static class LocalizationAdapter
         {
         private static bool _isInitialized = false;
         private static readonly Dictionary<string, string> _textMappings = new Dictionary<string, string>();
         
         // Reverse lookup cache: text -> localization key
         private static readonly Dictionary<string, string> _reverseCache = new Dictionary<string, string>();
         private static LocalizationManager.Language _lastCacheLanguage = LocalizationManager.Language.English;
        
        #region Initialization and Configuration
        
        /// <summary>
        /// Initialize localization adapter
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                // Initialize new localization system
                UILocalizationSystem.Initialize();
                
                // Setup standard text mappings
                SetupTextMappings();
                
                // Register UI element configurations
                RegisterUIConfigs();
                
                // Temporary fix: ensure config_location translation is correct
                EnsureCriticalTranslations();
                
                // Subscribe to language change events to update critical translations
                LocalizationManager.OnLanguageChanged += OnLanguageChangedHandler;
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalizationAdapter] Failed to initialize: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Language change event handler
        /// </summary>
        /// <param name="newLanguage">New language</param>
        private static void OnLanguageChangedHandler(LocalizationManager.Language newLanguage)
        {
            // Clear reverse lookup cache
            _reverseCache.Clear();
            _lastCacheLanguage = newLanguage;
            
            // Re-ensure critical translations when language switches
            EnsureCriticalTranslations();
        }
        
        /// <summary>
        /// Ensure critical translations are correctly set (temporary fix solution)
        /// </summary>
        private static void EnsureCriticalTranslations()
        {
            // Check and fix config_location translation
            if (!LocalizationManager.HasKey("connector.config_location"))
            {
                // Set translation based on current language
                var translation = LocalizationManager.CurrentLanguage == LocalizationManager.Language.ChineseSimplified 
                    ? "配置位置" 
                    : "Config Location";
                
                LocalizationManager.SetText("connector.config_location", translation);
            }
            
            // Other critical translations can be added here
            var criticalKeys = new Dictionary<string, Dictionary<LocalizationManager.Language, string>>
            {
                ["connector.config_location"] = new Dictionary<LocalizationManager.Language, string>
                {
                    [LocalizationManager.Language.English] = "Config Location",
                    [LocalizationManager.Language.ChineseSimplified] = "配置位置"
                }
            };
            
            foreach (var kvp in criticalKeys)
            {
                var key = kvp.Key;
                var translations = kvp.Value;
                
                if (!LocalizationManager.HasKey(key) && translations.TryGetValue(LocalizationManager.CurrentLanguage, out var translation))
                {
                    LocalizationManager.SetText(key, translation);
                }
            }
        }
        
        /// <summary>
        /// Setup standard UI text mappings
        /// Contains English and Chinese text to localization key mappings to ensure language switching works properly
        /// </summary>
        private static void SetupTextMappings()
        {
            // Maintain bidirectional mapping of English and Chinese text to localization keys
            _textMappings.Clear();
              
              var englishMappings = new Dictionary<string, string>
              {
                  // === Tab text ===
                  ["MCP Connector"] = "tab.connector",
                  ["Model Config"] = "tab.modelconfig",
                  ["User Input"] = "tab.userinput", 
                  ["Operations"] = "tab.operations",
                  ["Settings"] = "tab.settings",
                  
                  // === Connector page ===
                  ["AI Connector (MCP)"] = "connector.title",
                  ["Log Level"] = "connector.log_level",
                  ["Server URL"] = "connector.server_url",
                  ["Connect to MCP server"] = "connector.connect_server",
                  ["Information"] = "connector.information",
                  ["Configure MCP Client"] = "connector.configure_client",
                  ["Configure"] = "connector.configure",
                  ["Reconfigure"] = "connector.reconfigure",
                  ["More Clients"] = "connector.more_clients",
                  ["Pinned Clients"] = "connector.pinned_clients",
                  ["Drag any client to the pinned area above"] = "connector.drag_instruction",
                  ["Manual configuration"] = "connector.manual_config",
                  ["Copy paste the json into your MCP Client to configure it."] = "connector.manual_desc",
                  ["Rebuild MCP Server"] = "connector.rebuild_server",
                  ["Please check the logs to see the operation result."] = "connector.check_logs",
                  ["Config Location"] = "connector.config_location",
                  
                  // Client names
                  ["Cursor"] = "connector.client.cursor",
                  ["Claude Desktop"] = "connector.client.claude_desktop",
                  ["VS Code"] = "connector.client.vscode",
                  ["Visual Studio"] = "connector.client.visual_studio",
                  ["Augment"] = "connector.client.augment",
                  ["Windsurf"] = "connector.client.windsurf",
                  ["Cline"] = "connector.client.cline",
                  
                  // Descriptive text
                  ["Usually the server is hosted locally at"] = "connector.info_desc",
                  ["At least one client should be configured"] = "connector.client_desc",
                  
                  // Connection status
                  ["Connecting..."] = "connector.connecting",
                  ["Stop"] = "connector.stop",
                  ["Connect"] = "connector.connect",
                  ["Disconnect"] = "connector.disconnect",
                  ["Connected"] = "connector.connected",
                  ["Disconnected"] = "connector.disconnected",
                  ["Configured"] = "connector.configured",
                  ["Not Configured"] = "connector.not_configured",
                  
                  // === Model configuration page ===
                  ["AI Model Configuration"] = "model.title",
                  ["AI Provider Settings"] = "model.provider_settings",
                  ["OpenAI Settings"] = "model.openai_settings",
                  ["Gemini Settings"] = "model.gemini_settings",
                  ["Claude Settings"] = "model.claude_settings",
                  ["Local Settings"] = "model.local_settings",
                  ["Model Provider Selection"] = "model.provider_selection",
                  ["General Settings"] = "model.general_settings",
                  ["API Key"] = "model.api_key",
                  ["Model"] = "model.model",
                  ["Base URL"] = "model.base_url",
                  ["API URL"] = "model.api_url",
                  ["Vision Provider"] = "model.vision_provider",
                  ["Text Provider"] = "model.text_provider",
                  ["Code Provider"] = "model.code_provider",
                  ["Timeout"] = "model.timeout",
                  ["Max Tokens"] = "model.max_tokens",
                  
                  // === 操作监控页面 ===
                  ["Operations Monitor"] = "operations.title",
                  ["Undo Stack"] = "operations.undo_stack",
                  ["History"] = "operations.history",
                  ["No history available"] = "operations.no_history",
                  ["< Undo"] = "operations.undo",
                  ["Redo >"] = "operations.redo",
                  ["Refresh"] = "operations.refresh",
                  ["Clear Stack"] = "operations.clear_stack",
                  ["Total operations: {0}"] = "operations.stack_status",
                  
                  // === 用户输入页面 ===
                  ["User Input Panel"] = "userinput.title",
                  ["Prompt Message:"] = "userinput.prompt_label",
                  ["User Input:"] = "userinput.input_label",
                  ["Currently Selected Objects:"] = "userinput.selected_objects_label",
                  ["No objects selected"] = "userinput.no_objects",
                  ["Please enter..."] = "userinput.placeholder",
                  ["Confirm"] = "userinput.confirm",
                  ["Cancel"] = "userinput.cancel",
                  ["Waiting for user input request..."] = "userinput.waiting",
                  
                  // === 设置页面 ===
                  ["User Preferences"] = "settings.title",
                  ["Language Settings"] = "settings.language_settings",
                  ["Theme Settings"] = "settings.theme_settings",
                  ["Select the language for the user interface"] = "settings.language_desc",
                  ["Choose the visual theme for the interface"] = "settings.theme_desc",
                  ["Auto-refresh UI"] = "settings.auto_refresh",
                  ["Interface Language"] = "settings.interface_language",
                  ["UI Theme"] = "settings.ui_theme",
                  ["Language"] = "settings.language",
                  ["Theme"] = "settings.theme",
                  ["Save"] = "settings.save",
                  ["Reset"] = "settings.reset",
                  
                  // === 通用按钮 ===
                  ["Save Configuration"] = "common.save_config",
                  ["Not configured"] = "common.not_configured",
                  
                  // === 变体和额外映射 ===
                  ["More Clients (Click to expand, drag items up to pin)"] = "connector.more_clients",
                  ["Pinned Clients (Drag to reorder)"] = "connector.pinned_clients",
                  ["Timeout (seconds)"] = "model.timeout",
                  ["Save Settings"] = "settings.save",
                  ["Reset to Defaults"] = "settings.reset",
                  ["&lt; Undo"] = "operations.undo",
                  ["Redo &gt;"] = "operations.redo",
                  ["Operations Panel"] = "operations.title",
                  ["Operation History"] = "operations.history",
                  ["No operation history"] = "operations.no_history",
                  ["Stack Status: 0 operations"] = "operations.stack_status"
              };
              
              // 添加英文映射
              foreach (var mapping in englishMappings)
              {
                  _textMappings[mapping.Key] = mapping.Value;
              }
              
              // 添加中文映射 - 确保中文到英文的切换正常工作
              var chineseMappings = new Dictionary<string, string>
              {
                  // === 标签页文本 ===
                  ["MCP连接"] = "tab.connector",
                  ["模型配置"] = "tab.modelconfig", 
                  ["用户输入"] = "tab.userinput",
                  ["操作"] = "tab.operations",
                  ["设置"] = "tab.settings",
                  
                  // === 连接器页面 ===
                  ["MCP连接"] = "connector.title",
                  ["日志级别"] = "connector.log_level",
                  ["服务器地址"] = "connector.server_url",
                  ["连接到 MCP 服务器"] = "connector.connect_server",
                  ["信息"] = "connector.information",
                  ["配置 MCP 客户端"] = "connector.configure_client",
                  ["配置"] = "connector.configure",
                  ["重新配置"] = "connector.reconfigure",
                  ["更多客户端"] = "connector.more_clients",
                  ["置顶客户端"] = "connector.pinned_clients",
                  ["拖拽任意客户端到上方置顶区域"] = "connector.drag_instruction",
                  ["手动配置"] = "connector.manual_config",
                  ["复制此 JSON 配置到您的 MCP 客户端中进行配置。"] = "connector.manual_desc",
                  ["重新构建 MCP 服务器"] = "connector.rebuild_server",
                  ["请查看日志以了解操作结果。"] = "connector.check_logs",
                  ["配置位置"] = "connector.config_location",
                  
                  // 客户端名称
                  ["Cursor"] = "connector.client.cursor",
                  ["Claude 桌面版"] = "connector.client.claude_desktop",
                  ["VS Code"] = "connector.client.vscode",
                  ["Visual Studio"] = "connector.client.visual_studio",
                  ["Augment"] = "connector.client.augment",
                  ["Windsurf"] = "connector.client.windsurf",
                  ["Cline"] = "connector.client.cline",
                  
                  // 描述性文本
                  ["通常服务器运行在本地地址：\nhttp://localhost:60606\n\n如果需要，您也可以连接到远程 MCP 服务器。底层连接使用 SignalR 建立，支持丰富的功能特性。"] = "connector.info_desc",
                  ["至少需要配置一个客户端。\n某些客户端配置后需要重启。"] = "connector.client_desc",
                  
                  // 连接状态
                  ["连接中..."] = "connector.connecting",
                  ["停止"] = "connector.stop",
                  ["连接"] = "connector.connect",
                  ["断开连接"] = "connector.disconnect",
                  ["已连接"] = "connector.connected",
                  ["已断开连接"] = "connector.disconnected",
                  ["已配置"] = "connector.configured",
                  ["未配置"] = "connector.not_configured",
                  
                  // === 模型配置页面 ===
                  ["AI 模型配置"] = "model.title",
                  ["AI 提供商设置"] = "model.provider_settings",
                  ["OpenAI 设置"] = "model.openai_settings",
                  ["Gemini 设置"] = "model.gemini_settings",
                  ["Claude 设置"] = "model.claude_settings",
                  ["本地设置"] = "model.local_settings",
                  ["模型提供商选择"] = "model.provider_selection",
                  ["通用设置"] = "model.general_settings",
                  ["API 密钥"] = "model.api_key",
                  ["模型"] = "model.model",
                  ["基础 URL"] = "model.base_url",
                  ["API 地址"] = "model.api_url",
                  ["视觉模型提供商"] = "model.vision_provider",
                  ["文本模型提供商"] = "model.text_provider",
                  ["代码提供商"] = "model.code_provider",
                  ["超时时间"] = "model.timeout",
                  ["最大令牌数"] = "model.max_tokens",
                  
                  // === 设置页面 ===
                  ["用户偏好"] = "settings.title",
                  ["语言设置"] = "settings.language_settings",
                  ["界面语言"] = "settings.language",
                  ["选择用户界面的语言"] = "settings.language_desc",
                  ["主题设置"] = "settings.theme_settings",
                  ["UI主题"] = "settings.theme",
                  ["自动刷新界面"] = "settings.auto_refresh",
                  ["选择界面的视觉主题"] = "settings.theme_desc",
                  ["语言"] = "settings.language",
                  ["主题"] = "settings.theme",
                  
                  // === 用户输入页面 ===
                  ["用户输入面板"] = "userinput.title",
                  ["提示消息："] = "userinput.prompt_label",
                  ["用户输入："] = "userinput.input_label",
                  ["当前选定对象："] = "userinput.selected_objects_label",
                  
                  // === 操作监控页面 ===
                  ["操作监控"] = "operations.title",
                  ["撤销堆栈"] = "operations.undo_stack",
                  ["历史记录"] = "operations.history",
                  
                  // === 按钮文本 ===
                  ["保存"] = "common.save",
                  ["重置"] = "common.reset"
              };
              
              // 添加中文映射
              foreach (var mapping in chineseMappings)
              {
                  _textMappings[mapping.Key] = mapping.Value;
              }
          }
        
        /// <summary>
        /// Register programmatic configurations for UI elements
        /// </summary>
        private static void RegisterUIConfigs()
        {
            // Register programmatic localization configurations for key UI elements
            
            // Tab-related (based on element names in CreateGUI.cs)
            CodeConfigProvider.RegisterTextConfig("TabConnector", "tab.connector");
            CodeConfigProvider.RegisterTextConfig("TabModelConfig", "tab.modelconfig");
            CodeConfigProvider.RegisterTextConfig("TabUserInput", "tab.userinput");
            CodeConfigProvider.RegisterTextConfig("TabUndoHistory", "tab.operations");
            CodeConfigProvider.RegisterTextConfig("TabSettings", "tab.settings");
            
            // Connector-related
            CodeConfigProvider.RegisterTextConfig("labelSettings", "connector.title");
            CodeConfigProvider.RegisterTextConfig("btnRebuildServer", "connector.rebuild_server");
            CodeConfigProvider.RegisterTextConfig("btnConnectOrDisconnect", "connector.connect");
            CodeConfigProvider.RegisterTextConfig("connectionStatusText", "connector.disconnected");
            
            // Client configuration buttons
            CodeConfigProvider.RegisterTextConfig("btnConfigure", "connector.configure");
            CodeConfigProvider.RegisterTextConfig("configureStatusText", "common.not_configured");
            
            // Model configuration related
            CodeConfigProvider.RegisterTextConfig("btnSaveConfig", "common.save_config");
            
            // User input related
            CodeConfigProvider.RegisterTextConfig("userInputField", "userinput.placeholder");
            CodeConfigProvider.RegisterTextConfig("selectedObjectsText", "userinput.no_objects");
            CodeConfigProvider.RegisterTextConfig("btnConfirmInput", "userinput.confirm");
            CodeConfigProvider.RegisterTextConfig("btnCancelInput", "userinput.cancel");
            CodeConfigProvider.RegisterTextConfig("statusText", "userinput.waiting");
            
            // Operations monitoring related
            CodeConfigProvider.RegisterTextConfig("btnRefreshUndoStack", "operations.refresh");
            CodeConfigProvider.RegisterTextConfig("undoStackStatusText", "operations.stack_status");
            CodeConfigProvider.RegisterTextConfig("btnUndoLast", "operations.undo");
            CodeConfigProvider.RegisterTextConfig("btnRedoLast", "operations.redo");
            CodeConfigProvider.RegisterTextConfig("emptyUndoStackLabel", "operations.no_history");
            CodeConfigProvider.RegisterTextConfig("btnClearUndoStack", "operations.clear_stack");
            
            // Settings related
            CodeConfigProvider.RegisterTextConfig("btnSaveSettings", "settings.save");
            CodeConfigProvider.RegisterTextConfig("btnResetSettings", "settings.reset");
        }
        
        #endregion
        
        #region Text Processing Methods
        
        /// <summary>
        /// UI tree localization method
        /// Uses localization system for complete localization
        /// </summary>
        /// <param name="root">Root UI element</param>
        public static void LocalizeUITree(VisualElement root)
        {
            if (!_isInitialized) Initialize();
            
            if (root == null) return;
            
            try
            {
                // Force clear all caches to ensure reprocessing during language switch
                UILocalizationSystem.ClearAllCaches();
                
                // First apply text mappings
                ApplyMappingsToTree(root);
                
                // Then use localization system for complete localization
                UILocalizationSystem.LocalizeElementTree(root);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalizationAdapter] Error localizing UI tree: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply text mappings to entire UI tree
        /// </summary>
        /// <param name="root">Root element</param>
        private static void ApplyMappingsToTree(VisualElement root)
        {
            // Traverse all text elements, look for content that needs mapping
            root.Query<Label>().ForEach(label =>
            {
                ProcessTextElement(label, label.text, (l, text) => l.text = text);
            });
            
            // Process buttons
            root.Query<Button>().ForEach(button =>
            {
                ProcessTextElement(button, button.text, (b, text) => b.text = text);
            });
            
            // Process Foldout
            root.Query<Foldout>().ForEach(foldout =>
            {
                ProcessTextElement(foldout, foldout.text, (f, text) => f.text = text);
            });
            
            // Process TextField labels
            root.Query<TextField>().ForEach(textField =>
            {
                if (!string.IsNullOrEmpty(textField.label))
                {
                    ProcessTextElement(textField, textField.label, (tf, text) => tf.label = text);
                }
            });
            
            // Process DropdownField labels
            root.Query<DropdownField>().ForEach(dropdown =>
            {
                if (!string.IsNullOrEmpty(dropdown.label))
                {
                    ProcessTextElement(dropdown, dropdown.label, (dd, text) => dd.label = text);
                }
            });
            
            // Process Toggle
            root.Query<Toggle>().ForEach(toggle =>
            {
                if (!string.IsNullOrEmpty(toggle.text))
                {
                    ProcessTextElement(toggle, toggle.text, (t, text) => t.text = text);
                }
                if (!string.IsNullOrEmpty(toggle.label))
                {
                    ProcessTextElement(toggle, toggle.label, (t, text) => t.label = text);
                }
            });
            
            // Process EnumField labels
            root.Query<EnumField>().ForEach(enumField =>
            {
                if (!string.IsNullOrEmpty(enumField.label))
                {
                    ProcessTextElement(enumField, enumField.label, (ef, text) => ef.label = text);
                }
            });
        }
        
        /// <summary>
        /// Process single text element
        /// Intelligently match text in any language to localization key without needing separate mappings for each language
        /// </summary>
        /// <typeparam name="T">Element type</typeparam>
        /// <param name="element">UI element</param>
        /// <param name="currentText">Current text</param>
        /// <param name="setter">Method to set text</param>
        private static void ProcessTextElement<T>(T element, string currentText, System.Action<T, string> setter) where T : VisualElement
        {
            if (string.IsNullOrEmpty(currentText)) return;
            
            // 1. Try direct mapping match (mainly for English)
            if (_textMappings.TryGetValue(currentText, out var localizationKey))
            {
                var newText = LocalizationManager.GetText(localizationKey);
                setter(element, newText);
                RegisterElementConfig(element, localizationKey);
                return;
            }
            
            // 2. Try reverse lookup: current text might be a translation result of some localization key
            var foundKey = FindLocalizationKeyByReverse(currentText);
            if (!string.IsNullOrEmpty(foundKey))
            {
                var newText = LocalizationManager.GetText(foundKey);
                setter(element, newText);
                RegisterElementConfig(element, foundKey);
                return;
            }
            
            // 3. Try partial matching (for cases containing specific text)
            foreach (var mapping in _textMappings)
            {
                if (currentText.Contains(mapping.Key))
                {
                    var newText = LocalizationManager.GetText(mapping.Value);
                    setter(element, newText);
                    RegisterElementConfig(element, mapping.Value);
                    return;
                }
            }
        }
        
        /// <summary>
        /// 通过反向查找找到文本对应的localization key
        /// 检查当前文本是否是某个localization key的翻译结果
        /// 使用缓存来提高性能
        /// </summary>
        /// <param name="currentText">当前显示的文本</param>
        /// <returns>找到的localization key，如果没找到返回null</returns>
        private static string FindLocalizationKeyByReverse(string currentText)
        {
            if (string.IsNullOrEmpty(currentText)) return null;
            
            // 检查语言是否变化，如果变化则清空缓存
            if (LocalizationManager.CurrentLanguage != _lastCacheLanguage)
            {
                _reverseCache.Clear();
                _lastCacheLanguage = LocalizationManager.CurrentLanguage;
            }
            
            // 1. 检查缓存
            if (_reverseCache.TryGetValue(currentText, out var cachedKey))
            {
                return cachedKey;
            }
            
            // 2. 遍历所有已知的localization key，检查当前文本是否是其翻译结果
            foreach (var mapping in _textMappings)
            {
                var key = mapping.Value;
                
                // 检查当前文本是否是这个key在当前语言下的翻译
                var translatedText = LocalizationManager.GetText(key);
                if (string.Equals(currentText, translatedText, StringComparison.Ordinal))
                {
                    _reverseCache[currentText] = key; // 缓存结果
                    return key;
                }
            }
            
            // 3. 如果还没找到，尝试在所有available keys中查找
            var allKeys = LocalizationManager.GetAllKeys();
            foreach (var key in allKeys)
            {
                // 检查当前语言的翻译
                var translatedText = LocalizationManager.GetText(key);
                if (string.Equals(currentText, translatedText, StringComparison.Ordinal))
                {
                    _reverseCache[currentText] = key; // 缓存结果
                    return key;
                }
            }
            
            // 4. 没找到，缓存null结果以避免重复搜索
            _reverseCache[currentText] = null;
            return null;
        }
        
        /// <summary>
        /// 为元素注册本地化配置
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <param name="localizationKey">本地化键</param>
        private static void RegisterElementConfig(VisualElement element, string localizationKey)
        {
            if (!string.IsNullOrEmpty(element.name))
            {
                CodeConfigProvider.RegisterTextConfig(element.name, localizationKey);
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 为UI元素添加本地化标记
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="currentText">当前显示的文本</param>
        /// <returns>是否成功添加标记</returns>
        public static bool MarkElementForLocalization(VisualElement element, string currentText)
        {
            if (element == null || string.IsNullOrEmpty(currentText))
                return false;
            
            if (_textMappings.TryGetValue(currentText, out var localizationKey))
            {
                element.SetTextKey(localizationKey);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取建议的本地化键
        /// </summary>
        /// <param name="currentText">当前文本</param>
        /// <returns>建议的本地化键，如果没有返回null</returns>
        public static string GetSuggestedLocalizationKey(string currentText)
        {
            return _textMappings.TryGetValue(currentText, out var key) ? key : null;
        }
        
        /// <summary>
        /// 获取所有文本映射
        /// </summary>
        /// <returns>文本映射字典</returns>
        public static Dictionary<string, string> GetTextMappings()
        {
            return new Dictionary<string, string>(_textMappings);
        }
        
        /// <summary>
        /// 调试方法：列出UI树中所有未本地化的文本
        /// </summary>
        /// <param name="root">根UI元素</param>
        public static void DebugUnlocalizedTexts(VisualElement root)
        {
            if (!_isInitialized) Initialize();
            
            var unlocalizedTexts = new List<string>();
            
            // 收集所有文本元素的文本内容
            root.Query<Label>().ForEach(label =>
            {
                if (!string.IsNullOrEmpty(label.text) && !IsTextLocalized(label.text))
                {
                    unlocalizedTexts.Add($"Label: '{label.text}' (name: '{label.name}')");
                }
            });
            
            root.Query<Button>().ForEach(button =>
            {
                if (!string.IsNullOrEmpty(button.text) && !IsTextLocalized(button.text))
                {
                    unlocalizedTexts.Add($"Button: '{button.text}' (name: '{button.name}')");
                }
            });
            
            root.Query<Foldout>().ForEach(foldout =>
            {
                if (!string.IsNullOrEmpty(foldout.text) && !IsTextLocalized(foldout.text))
                {
                    unlocalizedTexts.Add($"Foldout: '{foldout.text}' (name: '{foldout.name}')");
                }
            });
            
            if (unlocalizedTexts.Count > 0)
            {
                UnityEngine.Debug.LogWarning($"[LocalizationAdapter] Found {unlocalizedTexts.Count} potentially unlocalized texts:\n" + 
                    string.Join("\n", unlocalizedTexts));
            }
        }
        
        /// <summary>
        /// 检查文本是否需要本地化处理
        /// 这个方法判断文本是否应该被本地化系统处理，而不是判断文本是否已经是正确的语言
        /// </summary>
        /// <param name="text">要检查的文本</param>
        /// <returns>true如果文本不需要本地化处理</returns>
        private static bool IsTextLocalized(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            
            // 检查是否为动态格式化文本（包含占位符）
            if (text.Contains("{0}") || text.Contains("{1}")) return true;
            
            // 检查是否为特殊的UI文本（通常不需要本地化）
            var specialTexts = new[] { "API", "URL", "HTTP", "JSON", "MCP", "AI" };
            if (specialTexts.Any(special => text.Equals(special, StringComparison.OrdinalIgnoreCase))) return true;
            
            // 检查是否在直接映射字典中或者可以通过反向查找找到
            if (_textMappings.ContainsKey(text)) return true;
            
            // 尝试反向查找，如果能找到对应的localization key，说明这个文本是可以本地化的
            var foundKey = FindLocalizationKeyByReverse(text);
            if (!string.IsNullOrEmpty(foundKey)) return true;
            
            return false;
        }
        
        #endregion
    }
} 