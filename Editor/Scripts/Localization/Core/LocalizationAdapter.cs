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
    /// 本地化适配器
    /// 
    /// 统一管理UI元素的本地化映射和处理，提供：
    /// 1. 标准文本到本地化键的映射
    /// 2. 程序化UI元素配置注册  
    /// 3. 整体UI树本地化处理
    /// 4. 调试和诊断工具
    /// </summary>
    public static class LocalizationAdapter
    {
        private static bool _isInitialized = false;
        private static readonly Dictionary<string, string> _textMappings = new Dictionary<string, string>();
        
        #region 初始化和配置
        
        /// <summary>
        /// 初始化本地化适配器
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                // 初始化新的本地化系统
                UILocalizationSystem.Initialize();
                
                // 设置标准文本映射
                SetupTextMappings();
                
                // 注册UI元素的配置
                RegisterUIConfigs();
                
                _isInitialized = true;
                UnityEngine.Debug.Log("[LocalizationAdapter] Initialized successfully");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalizationAdapter] Failed to initialize: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 设置标准UI文本映射
        /// 仅处理当前UXML中实际存在的文本，简化映射逻辑
        /// </summary>
        private static void SetupTextMappings()
        {
            // 创建标准的文本到本地化键的映射表
            var textMappings = new Dictionary<string, string>
            {
                // === 标签页文本 ===
                ["MCP Connector"] = "tab.connector",
                ["Model Config"] = "tab.modelconfig", 
                ["User Input"] = "tab.userinput",
                ["Operations"] = "tab.operations",
                ["Settings"] = "tab.settings",
                
                // === 连接器页面 ===
                ["AI Connector (MCP)"] = "connector.title",
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
                
                // 客户端名称
                ["Cursor"] = "connector.client.cursor",
                ["Claude Desktop"] = "connector.client.claude_desktop",
                ["VS Code"] = "connector.client.vscode",
                ["Visual Studio"] = "connector.client.visual_studio",
                ["Augment (VS Code)"] = "connector.client.augment",
                ["Windsurf"] = "connector.client.windsurf",
                ["Cline"] = "connector.client.cline",
                
                // 描述性文本
                ["Usually the server is hosted locally at"] = "connector.info_desc",
                ["At least one client should be configured"] = "connector.client_desc",
                
                // 连接状态
                ["Connecting..."] = "connector.connecting",
                ["Stop"] = "connector.stop", 
                ["Connect"] = "connector.connect",
                ["Disconnect"] = "connector.disconnect",
                ["Connected"] = "connector.connected",
                ["Disconnected"] = "connector.disconnected",
                ["Configured"] = "connector.configured",
                ["Not Configured"] = "connector.not_configured",
                
                // === 模型配置页面 ===
                ["AI Model Configuration"] = "model.title",
                ["AI Provider Settings"] = "model.provider_settings",
                ["OpenAI Settings"] = "model.openai_settings",
                ["Gemini Settings"] = "model.gemini_settings",
                ["Claude Settings"] = "model.claude_settings",
                ["Local Settings"] = "model.local_settings",
                ["Model Provider Selection"] = "model.provider_selection",
                ["General Settings"] = "model.general_settings",
                
                // === 操作监控页面 ===
                ["Operations Monitor"] = "operations.title",
                ["Operations Panel"] = "operations.title", // UXML中的旧标题
                ["Undo Stack"] = "operations.undo_stack",
                ["History"] = "operations.history",
                ["Operation History"] = "operations.history", // 兼容旧版本
                ["No history available"] = "operations.no_history",
                ["No operation history"] = "operations.no_history", // UXML中的旧文本
                ["< Undo"] = "operations.undo",
                ["&lt; Undo"] = "operations.undo", // HTML编码版本
                ["Redo >"] = "operations.redo",
                ["Redo &gt;"] = "operations.redo", // HTML编码版本
                ["Refresh"] = "operations.refresh",
                ["Clear Stack"] = "operations.clear_stack",
                
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
                
                // === 操作监控页面（补充） ===
                ["Operations Monitor"] = "operations.title",
                ["Stack Status: 0 operations"] = "operations.stack_status",
                
                // === 设置页面 ===
                ["User Preferences"] = "settings.title",
                ["Language Settings"] = "settings.language_settings",
                ["Theme Settings"] = "settings.theme_settings",
                ["Select the language for the user interface"] = "settings.language_desc",
                ["Choose the visual theme for the interface"] = "settings.theme_desc",
                ["Auto-refresh UI"] = "settings.auto_refresh",
                ["Language"] = "settings.language",
                ["Theme"] = "settings.theme",
                ["Save Settings"] = "settings.save",
                ["Reset to Defaults"] = "settings.reset",
                
                // === 通用按钮 ===
                ["Save Configuration"] = "common.save_config",
                ["Not configured"] = "common.not_configured"
            };
            
            // 将映射添加到字典中
            foreach (var mapping in textMappings)
            {
                _textMappings[mapping.Key] = mapping.Value;
            }
            
            UnityEngine.Debug.Log($"[LocalizationAdapter] Loaded {_textMappings.Count} text mappings");
        }
        
        /// <summary>
        /// 注册UI元素的程序化配置
        /// </summary>
        private static void RegisterUIConfigs()
        {
            // 为关键UI元素注册程序化本地化配置
            
            // 标签页相关（根据CreateGUI.cs中的元素名称）
            CodeConfigProvider.RegisterTextConfig("TabConnector", "tab.connector");
            CodeConfigProvider.RegisterTextConfig("TabModelConfig", "tab.modelconfig");
            CodeConfigProvider.RegisterTextConfig("TabUserInput", "tab.userinput");
            CodeConfigProvider.RegisterTextConfig("TabUndoHistory", "tab.operations");
            CodeConfigProvider.RegisterTextConfig("TabSettings", "tab.settings");
            
            // 连接器相关
            CodeConfigProvider.RegisterTextConfig("labelSettings", "connector.title");
            CodeConfigProvider.RegisterTextConfig("btnRebuildServer", "connector.rebuild_server");
            CodeConfigProvider.RegisterTextConfig("btnConnectOrDisconnect", "connector.connect");
            CodeConfigProvider.RegisterTextConfig("connectionStatusText", "connector.disconnected");
            
            // 客户端配置按钮
            CodeConfigProvider.RegisterTextConfig("btnConfigure", "connector.configure");
            CodeConfigProvider.RegisterTextConfig("configureStatusText", "common.not_configured");
            
            // 模型配置相关
            CodeConfigProvider.RegisterTextConfig("btnSaveConfig", "common.save_config");
            
            // 用户输入相关
            CodeConfigProvider.RegisterTextConfig("userInputField", "userinput.placeholder");
            CodeConfigProvider.RegisterTextConfig("selectedObjectsText", "userinput.no_objects");
            CodeConfigProvider.RegisterTextConfig("btnConfirmInput", "userinput.confirm");
            CodeConfigProvider.RegisterTextConfig("btnCancelInput", "userinput.cancel");
            CodeConfigProvider.RegisterTextConfig("statusText", "userinput.waiting");
            
            // 操作监控相关
            CodeConfigProvider.RegisterTextConfig("btnRefreshUndoStack", "operations.refresh");
            CodeConfigProvider.RegisterTextConfig("undoStackStatusText", "operations.stack_status");
            CodeConfigProvider.RegisterTextConfig("btnUndoLast", "operations.undo");
            CodeConfigProvider.RegisterTextConfig("btnRedoLast", "operations.redo");
            CodeConfigProvider.RegisterTextConfig("emptyUndoStackLabel", "operations.no_history");
            CodeConfigProvider.RegisterTextConfig("btnClearUndoStack", "operations.clear_stack");
            
            // 设置相关
            CodeConfigProvider.RegisterTextConfig("btnSaveSettings", "settings.save");
            CodeConfigProvider.RegisterTextConfig("btnResetSettings", "settings.reset");
        }
        
        #endregion
        
        #region 文本处理方法
        
        /// <summary>
        /// UI树本地化方法
        /// 使用本地化系统进行整体本地化
        /// </summary>
        /// <param name="root">根UI元素</param>
        public static void LocalizeUITree(VisualElement root)
        {
            if (!_isInitialized) Initialize();
            
            if (root == null) return;
            
            try
            {
                // 首先应用文本映射
                ApplyMappingsToTree(root);
                
                // 然后使用本地化系统进行完整本地化
                UILocalizationSystem.LocalizeElementTree(root);
                
                UnityEngine.Debug.Log($"[LocalizationAdapter] Localized UI tree: {root.name}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalizationAdapter] Error localizing UI tree: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 对整个UI树应用文本映射
        /// </summary>
        /// <param name="root">根元素</param>
        private static void ApplyMappingsToTree(VisualElement root)
        {
            // 遍历所有文本元素，查找需要映射的内容
            root.Query<Label>().ForEach(label =>
            {
                ProcessTextElement(label, label.text, (l, text) => l.text = text);
            });
            
            // 处理按钮
            root.Query<Button>().ForEach(button =>
            {
                ProcessTextElement(button, button.text, (b, text) => b.text = text);
            });
            
            // 处理Foldout
            root.Query<Foldout>().ForEach(foldout =>
            {
                ProcessTextElement(foldout, foldout.text, (f, text) => f.text = text);
            });
            
            // 处理TextField标签
            root.Query<TextField>().ForEach(textField =>
            {
                if (!string.IsNullOrEmpty(textField.label))
                {
                    ProcessTextElement(textField, textField.label, (tf, text) => tf.label = text);
                }
            });
            
            // 处理DropdownField标签
            root.Query<DropdownField>().ForEach(dropdown =>
            {
                if (!string.IsNullOrEmpty(dropdown.label))
                {
                    ProcessTextElement(dropdown, dropdown.label, (dd, text) => dd.label = text);
                }
            });
            
            // 处理Toggle
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
            
            // 处理EnumField标签
            root.Query<EnumField>().ForEach(enumField =>
            {
                if (!string.IsNullOrEmpty(enumField.label))
                {
                    ProcessTextElement(enumField, enumField.label, (ef, text) => ef.label = text);
                }
            });
        }
        
        /// <summary>
        /// 处理单个文本元素
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="element">UI元素</param>
        /// <param name="currentText">当前文本</param>
        /// <param name="setter">设置文本的方法</param>
        private static void ProcessTextElement<T>(T element, string currentText, System.Action<T, string> setter) where T : VisualElement
        {
            if (string.IsNullOrEmpty(currentText)) return;
            
            // 尝试精确匹配
            if (_textMappings.TryGetValue(currentText, out var localizationKey))
            {
                var newText = LocalizationManager.GetText(localizationKey);
                setter(element, newText);
                
                // 为元素注册配置以便后续自动更新
                if (!string.IsNullOrEmpty(element.name))
                {
                    CodeConfigProvider.RegisterTextConfig(element.name, localizationKey);
                }
                return;
            }
            
            // 尝试部分匹配（针对包含特定文本的情况）
            foreach (var mapping in _textMappings)
            {
                if (currentText.Contains(mapping.Key))
                {
                    var newText = LocalizationManager.GetText(mapping.Value);
                    setter(element, newText);
                    
                    if (!string.IsNullOrEmpty(element.name))
                    {
                        CodeConfigProvider.RegisterTextConfig(element.name, mapping.Value);
                    }
                    return;
                }
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
                if (!string.IsNullOrEmpty(label.text) && !_textMappings.ContainsKey(label.text))
                {
                    unlocalizedTexts.Add($"Label: '{label.text}' (name: '{label.name}')");
                }
            });
            
            root.Query<Button>().ForEach(button =>
            {
                if (!string.IsNullOrEmpty(button.text) && !_textMappings.ContainsKey(button.text))
                {
                    unlocalizedTexts.Add($"Button: '{button.text}' (name: '{button.name}')");
                }
            });
            
            root.Query<Foldout>().ForEach(foldout =>
            {
                if (!string.IsNullOrEmpty(foldout.text) && !_textMappings.ContainsKey(foldout.text))
                {
                    unlocalizedTexts.Add($"Foldout: '{foldout.text}' (name: '{foldout.name}')");
                }
            });
            
            if (unlocalizedTexts.Count > 0)
            {
                UnityEngine.Debug.LogWarning($"[LocalizationAdapter] Found {unlocalizedTexts.Count} unlocalized texts:\n" + 
                    string.Join("\n", unlocalizedTexts));
            }
            else
            {
                UnityEngine.Debug.Log("[LocalizationAdapter] All texts appear to be localized!");
            }
        }
        
        #endregion
    }
} 