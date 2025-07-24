using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;
using com.MiAO.Unity.MCP.Editor.Localization.Extensions;
using com.MiAO.Unity.MCP.Editor.Localization.Providers;

namespace com.MiAO.Unity.MCP.Editor.Localization.Resources
{
    /// <summary>
    /// 本地化系统测试工具
    /// 提供测试界面和功能验证
    /// </summary>
    public class LocalizationSystemTester : EditorWindow
    {
        [MenuItem("Window/MCP/Localization System Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalizationSystemTester>();
            window.titleContent = new GUIContent("Localization System Tester");
            window.Show();
        }
        
        private VisualElement _testContainer;
        private Label _statusLabel;
        private Button _testButton1;
        private Button _testButton2;
        private TextField _testTextField;
        private Foldout _testFoldout;
        private Toggle _testToggle;
        
        public void CreateGUI()
        {
            // 初始化本地化系统
            UILocalizationSystem.Initialize();
            
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            
            // 创建标题
            var title = new Label("本地化系统测试工具")
            {
                style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 }
            };
            root.Add(title);
            
            // 创建状态显示
            _statusLabel = new Label($"系统状态: 已初始化")
            {
                style = { marginBottom = 10, color = Color.green }
            };
            root.Add(_statusLabel);
            
            // 创建测试容器
            _testContainer = new VisualElement
            {
                style = { marginBottom = 10, paddingLeft = 10, paddingRight = 10, paddingTop = 10, paddingBottom = 10, backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f) }
            };
            root.Add(_testContainer);
            
            // 创建测试UI元素
            CreateTestElements();
            
            // 创建控制按钮
            CreateControlButtons(root);
            
            // 创建统计信息
            CreateStatsDisplay(root);
            
            // 应用初始本地化
            ApplyTestLocalization();
        }
        
        private void CreateTestElements()
        {
            // 测试按钮1 - 使用声明式本地化
            _testButton1 = new Button(() => Debug.Log("Test Button 1 Clicked"))
            {
                text = "Test Button",
                name = "test-button-1",
                style = { marginBottom = 5 }
            };
            _testButton1.SetTextKey("connector.configure");
            _testContainer.Add(_testButton1);
            
            // 测试按钮2 - 使用程序化配置
            _testButton2 = new Button(() => Debug.Log("Test Button 2 Clicked"))
            {
                text = "Another Test Button",
                name = "test-button-2", 
                style = { marginBottom = 5 }
            };
            CodeConfigProvider.RegisterTextConfig("test-button-2", "connector.reconfigure");
            _testContainer.Add(_testButton2);
            
            // 测试文本框
            _testTextField = new TextField("Test Field")
            {
                name = "test-textfield",
                style = { marginBottom = 5 }
            };
            _testTextField.SetLabelKey("connector.server_url").SetPlaceholderKey("connector.manual_placeholder");
            _testContainer.Add(_testTextField);
            
            // 测试折叠面板
            _testFoldout = new Foldout
            {
                text = "Test Foldout",
                name = "test-foldout",
                style = { marginBottom = 5 }
            };
            _testFoldout.SetTextKey("connector.information");
            _testContainer.Add(_testFoldout);
            
            // 测试复选框
            _testToggle = new Toggle("Test Toggle")
            {
                name = "test-toggle",
                style = { marginBottom = 5 }
            };
            _testToggle.SetLabelKey("settings.auto_refresh");
            _testContainer.Add(_testToggle);
            
            // 添加一些内容到折叠面板
            var foldoutContent = new Label("这是折叠面板中的内容")
            {
                style = { marginLeft = 15, marginTop = 5 }
            };
            _testFoldout.Add(foldoutContent);
        }
        
        private void CreateControlButtons(VisualElement root)
        {
            var buttonContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 10 }
            };
            root.Add(buttonContainer);
            
            // 本地化按钮
            var localizeButton = new Button(() => ApplyTestLocalization())
            {
                text = "重新本地化",
                style = { marginRight = 5 }
            };
            buttonContainer.Add(localizeButton);
            
            // 切换语言按钮
            var toggleLangButton = new Button(() => ToggleLanguage())
            {
                text = "切换语言",
                style = { marginRight = 5 }
            };
            buttonContainer.Add(toggleLangButton);
            
            // 清理缓存按钮
            var clearCacheButton = new Button(() => ClearCaches())
            {
                text = "清理缓存",
                style = { marginRight = 5 }
            };
            buttonContainer.Add(clearCacheButton);
            
            // 显示统计按钮
            var showStatsButton = new Button(() => ShowStats())
            {
                text = "显示统计",
                style = { marginRight = 5 }
            };
            buttonContainer.Add(showStatsButton);
            
            // 调试按钮
            var debugButton = new Button(() => DebugUnlocalized())
            {
                text = "调试未本地化"
            };
            buttonContainer.Add(debugButton);
        }
        
        private void CreateStatsDisplay(VisualElement root)
        {
            var statsContainer = new VisualElement
            {
                style = { 
                    marginTop = 10, 
                    paddingLeft = 10, 
                    paddingRight = 10, 
                    paddingTop = 10, 
                    paddingBottom = 10, 
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f) 
                }
            };
            root.Add(statsContainer);
            
            var statsTitle = new Label("系统统计信息")
            {
                style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 }
            };
            statsContainer.Add(statsTitle);
            
            var statsContent = new Label("点击 '显示统计' 按钮查看详细信息")
            {
                name = "stats-content"
            };
            statsContainer.Add(statsContent);
        }
        
        private void ApplyTestLocalization()
        {
            try
            {
                // 使用新的本地化系统
                UILocalizationSystem.LocalizeElementTree(_testContainer);
                
                _statusLabel.text = $"本地化完成 - 语言: {LocalizationManager.CurrentLanguage}";
                _statusLabel.style.color = Color.green;
                
                Debug.Log("[LocalizationSystemTester] 本地化测试完成");
            }
            catch (Exception ex)
            {
                _statusLabel.text = $"本地化失败: {ex.Message}";
                _statusLabel.style.color = Color.red;
                Debug.LogError($"[LocalizationSystemTester] 本地化测试失败: {ex.Message}");
            }
        }
        
        private void ToggleLanguage()
        {
            var currentLang = LocalizationManager.CurrentLanguage;
            var newLang = currentLang == LocalizationManager.Language.English 
                ? LocalizationManager.Language.ChineseSimplified 
                : LocalizationManager.Language.English;
                
            LocalizationManager.CurrentLanguage = newLang;
            
            // 语言变更后会自动触发本地化更新
            _statusLabel.text = $"语言已切换到: {newLang}";
            _statusLabel.style.color = Color.yellow;
        }
        
        private void ClearCaches()
        {
            UILocalizationSystem.ClearAllCaches();
            CodeConfigProvider.ClearAllConfigs();
            
            _statusLabel.text = "缓存已清理";
            _statusLabel.style.color = Color.cyan;
            
            Debug.Log("[LocalizationSystemTester] 所有缓存已清理");
        }
        
        private void ShowStats()
        {
            var stats = UILocalizationSystem.GlobalStats;
            var configStats = CodeConfigProvider.GetStats();
            
            var statsInfo = $"系统统计:\n" +
                           $"• 处理器数量: {UILocalizationSystem.ProcessorCount}\n" +
                           $"• 配置提供者数量: {UILocalizationSystem.ConfigProviderCount}\n" +
                           $"• 已处理元素: {stats.ProcessedElementsCount}\n" +
                           $"• 处理时间: {stats.ProcessingTimeMs}ms\n" +
                           $"• 缓存命中: {stats.CacheHitCount}\n" +
                           $"• 缓存未命中: {stats.CacheMissCount}\n" +
                           $"• 配置统计: {configStats}";
            
            var statsLabel = rootVisualElement.Query<Label>("stats-content").First();
            if (statsLabel != null)
            {
                statsLabel.text = statsInfo;
            }
            
            _statusLabel.text = "统计信息已更新";
            _statusLabel.style.color = Color.magenta;
        }
        
        private void DebugUnlocalized()
        {
            LocalizationAdapter.DebugUnlocalizedTexts(rootVisualElement);
            
            _statusLabel.text = "调试完成 - 检查控制台输出";
            _statusLabel.style.color = Color.white;
        }
    }
} 