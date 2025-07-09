using System.Collections.Generic;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Editor.Common
{
    /// <summary>
    /// Localization manager for managing multi-language text in UI interface
    /// </summary>
    public static class LocalizationManager
    {
        public enum Language
        {
            English,
            ChineseSimplified
        }

        private static Language _currentLanguage = Language.English;
        
        /// <summary>
        /// Current language setting
        /// </summary>
        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnLanguageChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// Language change event
        /// </summary>
        public static System.Action<Language> OnLanguageChanged;

        /// <summary>
        /// Localized text dictionary
        /// </summary>
        private static readonly Dictionary<string, Dictionary<Language, string>> LocalizedTexts = new Dictionary<string, Dictionary<Language, string>>
        {
            // Window title
            ["window.title"] = new Dictionary<Language, string>
            {
                [Language.English] = "MCP Hub",
                [Language.ChineseSimplified] = "MCP Hub"
            },

            // Tab titles
            ["tab.connector"] = new Dictionary<Language, string>
            {
                [Language.English] = "MCP Connector",
                [Language.ChineseSimplified] = "MCP连接"
            },
            ["tab.modelconfig"] = new Dictionary<Language, string>
            {
                [Language.English] = "Model Config",
                [Language.ChineseSimplified] = "模型配置"
            },
            ["tab.operations"] = new Dictionary<Language, string>
            {
                [Language.English] = "Operations",
                [Language.ChineseSimplified] = "操作管理"
            },
            ["tab.settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Settings",
                [Language.ChineseSimplified] = "设置"
            },

            // MCP Connector tab
            ["connector.title"] = new Dictionary<Language, string>
            {
                [Language.English] = "MCP Connector",
                [Language.ChineseSimplified] = "MCP连接"
            },
            ["connector.loglevel"] = new Dictionary<Language, string>
            {
                [Language.English] = "Log Level",
                [Language.ChineseSimplified] = "日志级别"
            },
            ["connector.connect_server"] = new Dictionary<Language, string>
            {
                [Language.English] = "Connect to MCP server",
                [Language.ChineseSimplified] = "连接到 MCP 服务器"
            },
            ["connector.server_url"] = new Dictionary<Language, string>
            {
                [Language.English] = "Server URL",
                [Language.ChineseSimplified] = "服务器地址"
            },
            ["connector.information"] = new Dictionary<Language, string>
            {
                [Language.English] = "Information",
                [Language.ChineseSimplified] = "信息"
            },
            ["connector.info_desc"] = new Dictionary<Language, string>
            {
                [Language.English] = "Usually the server is hosted locally at:\nhttp://localhost:60606\n\nBut feel free to connect to remote MCP server if needed. The connection under the hood is established using SignalR and supports wide range of features.",
    [Language.ChineseSimplified] = "通常服务器运行在本地地址：\nhttp://localhost:60606\n\n如果需要，您也可以连接到远程 MCP 服务器。底层连接使用 SignalR 建立，支持丰富的功能特性。"
            },
            ["connector.disconnected"] = new Dictionary<Language, string>
            {
                [Language.English] = "Disconnected",
                [Language.ChineseSimplified] = "已断开连接"
            },
            ["connector.connected"] = new Dictionary<Language, string>
            {
                [Language.English] = "Connected",
                [Language.ChineseSimplified] = "已连接"
            },
            ["connector.connect"] = new Dictionary<Language, string>
            {
                [Language.English] = "Connect",
                [Language.ChineseSimplified] = "连接"
            },
            ["connector.disconnect"] = new Dictionary<Language, string>
            {
                [Language.English] = "Disconnect",
                [Language.ChineseSimplified] = "断开连接"
            },
            ["connector.connecting"] = new Dictionary<Language, string>
            {
                [Language.English] = "Connecting...",
                [Language.ChineseSimplified] = "连接中..."
            },
            ["connector.stop"] = new Dictionary<Language, string>
            {
                [Language.English] = "Stop",
                [Language.ChineseSimplified] = "停止"
            },
            ["connector.configure_client"] = new Dictionary<Language, string>
            {
                [Language.English] = "Configure MCP Client",
                [Language.ChineseSimplified] = "配置 MCP 客户端"
            },
            ["connector.client_desc"] = new Dictionary<Language, string>
            {
                [Language.English] = "At least one client should be configured.\nSome clients need restart after configuration.",
                [Language.ChineseSimplified] = "至少需要配置一个客户端。\n某些客户端配置后需要重启。"
            },
            ["connector.not_configured"] = new Dictionary<Language, string>
            {
                [Language.English] = "Not configured",
                [Language.ChineseSimplified] = "未配置"
            },
            ["connector.configured"] = new Dictionary<Language, string>
            {
                [Language.English] = "Configured",
                [Language.ChineseSimplified] = "已配置"
            },
            ["connector.configure"] = new Dictionary<Language, string>
            {
                [Language.English] = "Configure",
                [Language.ChineseSimplified] = "配置"
            },
            ["connector.manual_config"] = new Dictionary<Language, string>
            {
                [Language.English] = "Manual configuration",
                [Language.ChineseSimplified] = "手动配置"
            },
            ["connector.manual_desc"] = new Dictionary<Language, string>
            {
                [Language.English] = "Copy paste the json into your MCP Client to configure it.",
                [Language.ChineseSimplified] = "复制此 JSON 配置到您的 MCP 客户端中进行配置。"
            },
            ["connector.manual_placeholder"] = new Dictionary<Language, string>
            {
                [Language.English] = "This is a multi-line, read-only, selectable text box.\nYou can copy from here.",
                [Language.ChineseSimplified] = "这是一个多行、只读、可选择的文本框。\n您可以从这里复制。"
            },
            ["connector.rebuild_server"] = new Dictionary<Language, string>
            {
                [Language.English] = "Rebuild MCP Server",
                [Language.ChineseSimplified] = "重新构建 MCP 服务器"
            },
            ["connector.check_logs"] = new Dictionary<Language, string>
            {
                [Language.English] = "Please check the logs to see the operation result.",
                [Language.ChineseSimplified] = "请查看日志以了解操作结果。"
            },

            // Model Configuration tab
            ["model.title"] = new Dictionary<Language, string>
            {
                [Language.English] = "AI Model Configuration",
                [Language.ChineseSimplified] = "AI 模型配置"
            },
            ["model.provider_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "AI Provider Settings",
                [Language.ChineseSimplified] = "AI 提供商设置"
            },
            ["model.openai_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "OpenAI Settings",
                [Language.ChineseSimplified] = "OpenAI 设置"
            },
            ["model.gemini_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Gemini Settings",
                [Language.ChineseSimplified] = "Gemini 设置"
            },
            ["model.claude_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Claude Settings",
                [Language.ChineseSimplified] = "Claude 设置"
            },
            ["model.local_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Local Settings",
                [Language.ChineseSimplified] = "本地设置"
            },
            ["model.api_key"] = new Dictionary<Language, string>
            {
                [Language.English] = "API Key",
                [Language.ChineseSimplified] = "API 密钥"
            },
            ["model.model"] = new Dictionary<Language, string>
            {
                [Language.English] = "Model",
                [Language.ChineseSimplified] = "模型"
            },
            ["model.base_url"] = new Dictionary<Language, string>
            {
                [Language.English] = "Base URL",
                [Language.ChineseSimplified] = "基础 URL"
            },
            ["model.api_url"] = new Dictionary<Language, string>
            {
                [Language.English] = "API URL",
                [Language.ChineseSimplified] = "API 地址"
            },
            ["model.provider_selection"] = new Dictionary<Language, string>
            {
                [Language.English] = "Model Provider Selection",
                [Language.ChineseSimplified] = "模型提供商选择"
            },
            ["model.vision_provider"] = new Dictionary<Language, string>
            {
                [Language.English] = "Vision Provider",
                [Language.ChineseSimplified] = "视觉模型提供商"
            },
            ["model.text_provider"] = new Dictionary<Language, string>
            {
                [Language.English] = "Text Provider",
                [Language.ChineseSimplified] = "文本模型提供商"
            },
            ["model.code_provider"] = new Dictionary<Language, string>
            {
                [Language.English] = "Code Provider",
                [Language.ChineseSimplified] = "代码模型提供商"
            },
            ["model.general_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "General Settings",
                [Language.ChineseSimplified] = "通用设置"
            },
            ["model.timeout"] = new Dictionary<Language, string>
            {
                [Language.English] = "Timeout (seconds)",
                [Language.ChineseSimplified] = "超时时间（秒）"
            },
            ["model.max_tokens"] = new Dictionary<Language, string>
            {
                [Language.English] = "Max Tokens",
                [Language.ChineseSimplified] = "最大令牌数"
            },
            ["model.save_config"] = new Dictionary<Language, string>
            {
                [Language.English] = "Save Configuration",
                [Language.ChineseSimplified] = "保存配置"
            },

            // Settings tab
            ["settings.title"] = new Dictionary<Language, string>
            {
                [Language.English] = "User Preferences",
                [Language.ChineseSimplified] = "用户偏好设置"
            },
            ["settings.language_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Language Settings",
                [Language.ChineseSimplified] = "语言设置"
            },
            ["settings.interface_language"] = new Dictionary<Language, string>
            {
                [Language.English] = "Interface Language",
                [Language.ChineseSimplified] = "界面语言"
            },
            ["settings.language_desc"] = new Dictionary<Language, string>
            {
                [Language.English] = "Select your preferred language for the user interface.",
                [Language.ChineseSimplified] = "选择您偏好的用户界面语言。"
            },
            ["settings.theme_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Theme Settings",
                [Language.ChineseSimplified] = "主题设置"
            },
            ["settings.ui_theme"] = new Dictionary<Language, string>
            {
                [Language.English] = "UI Theme",
                [Language.ChineseSimplified] = "界面主题"
            },
            ["settings.auto_refresh"] = new Dictionary<Language, string>
            {
                [Language.English] = "Auto-refresh UI",
                [Language.ChineseSimplified] = "自动刷新界面"
            },
            ["settings.theme_desc"] = new Dictionary<Language, string>
            {
                [Language.English] = "Configure the appearance and behavior of the user interface.",
                [Language.ChineseSimplified] = "配置用户界面的外观和行为。"
            },
            ["settings.save"] = new Dictionary<Language, string>
            {
                [Language.English] = "Save Settings",
                [Language.ChineseSimplified] = "保存设置"
            },
            ["settings.reset"] = new Dictionary<Language, string>
            {
                [Language.English] = "Reset to Defaults",
                [Language.ChineseSimplified] = "重置为默认值"
            },

            // Operations tab
            ["operations.title"] = new Dictionary<Language, string>
            {
                [Language.English] = "Operations Panel",
                [Language.ChineseSimplified] = "操作面板"
            },
            ["operations.undo_stack"] = new Dictionary<Language, string>
            {
                [Language.English] = "Undo Stack",
                [Language.ChineseSimplified] = "撤销栈"
            },
            ["operations.refresh"] = new Dictionary<Language, string>
            {
                [Language.English] = "Refresh",
                [Language.ChineseSimplified] = "刷新"
            },
            ["operations.stack_status"] = new Dictionary<Language, string>
            {
                [Language.English] = "Stack Status: {0} operations",
                [Language.ChineseSimplified] = "栈状态：{0} 个操作"
            },
            ["operations.undo"] = new Dictionary<Language, string>
            {
                [Language.English] = "< Undo",
                [Language.ChineseSimplified] = "< 撤销"
            },
            ["operations.redo"] = new Dictionary<Language, string>
            {
                [Language.English] = "Redo >",
                [Language.ChineseSimplified] = "重做 >"
            },
            ["operations.history"] = new Dictionary<Language, string>
            {
                [Language.English] = "Operation History",
                [Language.ChineseSimplified] = "操作历史"
            },
            ["operations.no_history"] = new Dictionary<Language, string>
            {
                [Language.English] = "No operation history",
                [Language.ChineseSimplified] = "无操作历史"
            },
            ["operations.clear_stack"] = new Dictionary<Language, string>
            {
                [Language.English] = "Clear Stack",
                [Language.ChineseSimplified] = "清空栈"
            },
            ["operations.undo_stack_header"] = new Dictionary<Language, string>
            {
                [Language.English] = "[<] Undo Stack (ordered by time, newest first)",
                [Language.ChineseSimplified] = "[<] 撤销栈（按时间排序，最新的在前）"
            },
            ["operations.redo_stack_header"] = new Dictionary<Language, string>
            {
                [Language.English] = "[>] Redo Stack (ordered by time, newest first)",
                [Language.ChineseSimplified] = "[>] 重做栈（按时间排序，最新的在前）"
            },
            ["operations.latest"] = new Dictionary<Language, string>
            {
                [Language.English] = "< Latest",
                [Language.ChineseSimplified] = "< 最新"
            },
            ["operations.icon.create"] = new Dictionary<Language, string>
            {
                [Language.English] = "[Create]",
                [Language.ChineseSimplified] = "[创建]"
            },
            ["operations.icon.delete"] = new Dictionary<Language, string>
            {
                [Language.English] = "[Delete]",
                [Language.ChineseSimplified] = "[删除]"
            },
            ["operations.icon.modify"] = new Dictionary<Language, string>
            {
                [Language.English] = "[Modify]",
                [Language.ChineseSimplified] = "[修改]"
            },
            ["operations.icon.move"] = new Dictionary<Language, string>
            {
                [Language.English] = "[Move]",
                [Language.ChineseSimplified] = "[移动]"
            },
            ["operations.icon.copy"] = new Dictionary<Language, string>
            {
                [Language.English] = "[Copy]",
                [Language.ChineseSimplified] = "[复制]"
            },
            ["operations.icon.rename"] = new Dictionary<Language, string>
            {
                [Language.English] = "[Rename]",
                [Language.ChineseSimplified] = "[重命名]"
            },
            ["operations.icon.unknown"] = new Dictionary<Language, string>
            {
                [Language.English] = "[Unknown]",
                [Language.ChineseSimplified] = "[未知]"
            },

            // User Input tab
            ["tab.userinput"] = new Dictionary<Language, string>
            {
                [Language.English] = "User Input",
                [Language.ChineseSimplified] = "用户输入"
            },
            ["userinput.title"] = new Dictionary<Language, string>
            {
                [Language.English] = "User Input Panel",
                [Language.ChineseSimplified] = "用户输入面板"
            },
            ["userinput.prompt_message"] = new Dictionary<Language, string>
            {
                [Language.English] = "Prompt Message:",
                [Language.ChineseSimplified] = "提示消息："
            },
            ["userinput.user_input"] = new Dictionary<Language, string>
            {
                [Language.English] = "User Input:",
                [Language.ChineseSimplified] = "用户输入："
            },
            ["userinput.selected_objects"] = new Dictionary<Language, string>
            {
                [Language.English] = "Currently Selected Objects:",
                [Language.ChineseSimplified] = "当前选中的对象："
            },
            ["userinput.no_objects"] = new Dictionary<Language, string>
            {
                [Language.English] = "No objects selected",
                [Language.ChineseSimplified] = "没有选中对象"
            },
            ["userinput.confirm"] = new Dictionary<Language, string>
            {
                [Language.English] = "Confirm",
                [Language.ChineseSimplified] = "确认"
            },
            ["userinput.cancel"] = new Dictionary<Language, string>
            {
                [Language.English] = "Cancel",
                [Language.ChineseSimplified] = "取消"
            },
            ["userinput.waiting"] = new Dictionary<Language, string>
            {
                [Language.English] = "Waiting for user input request...",
                [Language.ChineseSimplified] = "等待用户输入请求..."
            },

            // Common text
            ["language.english"] = new Dictionary<Language, string>
            {
                [Language.English] = "English",
                [Language.ChineseSimplified] = "英文"
            },
            ["language.chinese"] = new Dictionary<Language, string>
            {
                [Language.English] = "Chinese (Simplified)",
                [Language.ChineseSimplified] = "简体中文"
            },
            ["theme.dark"] = new Dictionary<Language, string>
            {
                [Language.English] = "Dark",
                [Language.ChineseSimplified] = "深色"
            },
            ["theme.light"] = new Dictionary<Language, string>
            {
                [Language.English] = "Light",
                [Language.ChineseSimplified] = "浅色"
            },
            ["theme.auto"] = new Dictionary<Language, string>
            {
                [Language.English] = "Auto",
                [Language.ChineseSimplified] = "自动"
            },

            // Dialog text
            ["dialog.clear_undo_stack_title"] = new Dictionary<Language, string>
            {
                [Language.English] = "Clear Undo Stack",
                [Language.ChineseSimplified] = "清空撤销栈"
            },
            ["dialog.clear_undo_stack_message"] = new Dictionary<Language, string>
            {
                [Language.English] = "Are you sure you want to clear all undo history? This action cannot be undone.",
                [Language.ChineseSimplified] = "您确定要清空所有撤销历史吗？此操作无法撤销。"
            },
            ["dialog.clear"] = new Dictionary<Language, string>
            {
                [Language.English] = "Clear",
                [Language.ChineseSimplified] = "清空"
            },
            ["dialog.cancel"] = new Dictionary<Language, string>
            {
                [Language.English] = "Cancel",
                [Language.ChineseSimplified] = "取消"
            },
            ["dialog.reset"] = new Dictionary<Language, string>
            {
                [Language.English] = "Reset",
                [Language.ChineseSimplified] = "重置"
            },
            ["dialog.settings_error"] = new Dictionary<Language, string>
            {
                [Language.English] = "Settings Error",
                [Language.ChineseSimplified] = "设置错误"
            },
            ["dialog.settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Settings",
                [Language.ChineseSimplified] = "设置"
            },
            ["dialog.invalid_settings"] = new Dictionary<Language, string>
            {
                [Language.English] = "Invalid settings detected. Please check your selections.",
                [Language.ChineseSimplified] = "检测到无效设置。请检查您的选择。"
            },
            ["dialog.save_success"] = new Dictionary<Language, string>
            {
                [Language.English] = "Settings saved successfully!\n\n{0}",
                [Language.ChineseSimplified] = "设置保存成功！\n\n{0}"
            },
            ["dialog.save_failed"] = new Dictionary<Language, string>
            {
                [Language.English] = "Failed to save settings:\n{0}",
                [Language.ChineseSimplified] = "保存设置失败：\n{0}"
            },
            ["dialog.reset_settings_title"] = new Dictionary<Language, string>
            {
                [Language.English] = "Reset Settings",
                [Language.ChineseSimplified] = "重置设置"
            },
            ["dialog.reset_settings_message"] = new Dictionary<Language, string>
            {
                [Language.English] = "Are you sure you want to reset all settings to default values?\n\nThis will reset:\n• Language to English\n• Theme to Dark\n• Auto-refresh to enabled",
                [Language.ChineseSimplified] = "您确定要将所有设置重置为默认值吗？\n\n这将重置：\n• 语言为英文\n• 主题为深色\n• 自动刷新为启用"
            },
            ["dialog.reset_success"] = new Dictionary<Language, string>
            {
                [Language.English] = "Settings have been reset to default values.",
                [Language.ChineseSimplified] = "设置已重置为默认值。"
            },
            ["dialog.reset_failed"] = new Dictionary<Language, string>
            {
                [Language.English] = "Failed to reset settings:\n{0}",
                [Language.ChineseSimplified] = "重置设置失败：\n{0}"
            },

            // Settings summary text
            ["summary.title"] = new Dictionary<Language, string>
            {
                [Language.English] = "Settings Summary:",
                [Language.ChineseSimplified] = "设置摘要："
            },
            ["summary.language"] = new Dictionary<Language, string>
            {
                [Language.English] = "• Language: ",
                [Language.ChineseSimplified] = "• 语言："
            },
            ["summary.theme"] = new Dictionary<Language, string>
            {
                [Language.English] = "• Theme: ",
                [Language.ChineseSimplified] = "• 主题："
            },
            ["summary.auto_refresh"] = new Dictionary<Language, string>
            {
                [Language.English] = "• Auto Refresh: ",
                [Language.ChineseSimplified] = "• 自动刷新："
            },
            ["text.enabled"] = new Dictionary<Language, string>
            {
                [Language.English] = "Enabled",
                [Language.ChineseSimplified] = "启用"
            },
            ["text.disabled"] = new Dictionary<Language, string>
            {
                [Language.English] = "Disabled",
                [Language.ChineseSimplified] = "禁用"
            },
            ["text.light"] = new Dictionary<Language, string>
            {
                [Language.English] = "Light",
                [Language.ChineseSimplified] = "浅色"
            },
            ["text.dark"] = new Dictionary<Language, string>
            {
                [Language.English] = "Dark",
                [Language.ChineseSimplified] = "深色"
            },
            ["text.auto"] = new Dictionary<Language, string>
            {
                [Language.English] = "Auto",
                [Language.ChineseSimplified] = "自动"
            }
        };

        /// <summary>
        /// Get localized text
        /// </summary>
        /// <param name="key">Text key</param>
        /// <param name="args">Format parameters</param>
        /// <returns>Localized text</returns>
        public static string GetText(string key, params object[] args)
        {
            if (LocalizedTexts.TryGetValue(key, out var languageDict) && 
                languageDict.TryGetValue(CurrentLanguage, out var text))
            {
                if (args != null && args.Length > 0)
                {
                    return string.Format(text, args);
                }
                return text;
            }

            // Return key as default value if localized text is not found
            Debug.LogWarning($"Missing localization for key: {key}");
            return key;
        }

        /// <summary>
        /// Convert string to language enum
        /// </summary>
        public static Language StringToLanguage(string languageString)
        {
            return languageString switch
            {
                "简体中文" => Language.ChineseSimplified,
                "ChineseSimplified" => Language.ChineseSimplified,
                _ => Language.English
            };
        }

        /// <summary>
        /// Convert language enum to display string
        /// </summary>
        public static string LanguageToDisplayString(Language language)
        {
            return language switch
            {
                Language.ChineseSimplified => GetText("language.chinese"),
                _ => GetText("language.english")
            };
        }
    }
} 