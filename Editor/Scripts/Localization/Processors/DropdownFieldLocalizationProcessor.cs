using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// DropdownField控件本地化处理器
    /// </summary>
    public class DropdownFieldLocalizationProcessor : ILocalizationProcessor
    {
        public int Priority => 100;
        
        public bool CanProcess(VisualElement element)
        {
            return element is DropdownField;
        }
        
        public void Process(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            var dropdown = (DropdownField)element;
            
            // 处理标签文本
            if (!string.IsNullOrEmpty(config.LabelKey))
            {
                var labelText = GetLocalizedText(config.LabelKey, config, context);
                dropdown.label = labelText;
            }
            
            // 处理工具提示
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                dropdown.tooltip = tooltipText;
            }
            
            // 处理自定义属性
            ProcessCustomProperties(dropdown, config, context);
        }
        
        private string GetLocalizedText(string key, LocalizationConfig config, LocalizationContext context)
        {
            var text = LocalizationManager.GetText(key);
            
            // 处理格式化参数
            if (config.Parameters != null && config.Parameters.Count > 0)
            {
                var args = config.Parameters.Select(p => p.GetValue(context)).ToArray();
                text = string.Format(text, args);
            }
            
            return text;
        }
        
        private void ProcessCustomProperties(DropdownField dropdown, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var kvp in config.CustomProperties)
            {
                var localizedValue = LocalizationManager.GetText(kvp.Value);
                
                switch (kvp.Key.ToLower())
                {
                    case "class":
                    case "css-class":
                        ApplyCssClass(dropdown, localizedValue);
                        break;
                    case "enabled":
                        ApplyEnabledState(dropdown, localizedValue);
                        break;
                    case "choices":
                        ApplyLocalizedChoices(dropdown, localizedValue, context);
                        break;
                    default:
                        UnityEngine.Debug.Log($"[DropdownFieldLocalizationProcessor] Unsupported custom property: {kvp.Key}");
                        break;
                }
            }
        }
        
        private void ApplyCssClass(DropdownField dropdown, string className)
        {
            if (!string.IsNullOrEmpty(className))
            {
                dropdown.AddToClassList(className);
            }
        }
        
        private void ApplyEnabledState(DropdownField dropdown, string enabledValue)
        {
            if (bool.TryParse(enabledValue, out var isEnabled))
            {
                dropdown.SetEnabled(isEnabled);
            }
        }
        
        private void ApplyLocalizedChoices(DropdownField dropdown, string choicesConfig, LocalizationContext context)
        {
            // 支持本地化选项列表
            // 格式：choice1.key,choice2.key,choice3.key
            if (string.IsNullOrEmpty(choicesConfig)) return;
            
            var choiceKeys = choicesConfig.Split(',');
            var localizedChoices = choiceKeys.Select(key => LocalizationManager.GetText(key.Trim())).ToList();
            
            // 保存当前选中的值
            var currentValue = dropdown.value;
            var currentIndex = dropdown.choices?.IndexOf(currentValue) ?? -1;
            
            // 更新选项列表
            dropdown.choices = localizedChoices;
            
            // 尝试保持选中状态
            if (currentIndex >= 0 && currentIndex < localizedChoices.Count)
            {
                dropdown.value = localizedChoices[currentIndex];
            }
        }
    }
} 