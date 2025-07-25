using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// Button控件本地化处理器
    /// </summary>
    public class ButtonLocalizationProcessor : ILocalizationProcessor
    {
        public int Priority => 100;
        
        public bool CanProcess(VisualElement element)
        {
            return element is Button;
        }
        
        public void Process(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            var button = (Button)element;
            
            // 处理按钮文本
            if (!string.IsNullOrEmpty(config.TextKey))
            {
                var textKey = config.GetConditionalTextKey(context);
                var localizedText = GetLocalizedText(textKey, config, context);
                button.text = localizedText;
            }
            
            // 处理工具提示
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                button.tooltip = tooltipText;
            }
            
            // 处理自定义属性
            ProcessCustomProperties(button, config, context);
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
            
            // 处理富文本
            if (config.EnableRichText)
            {
                text = ProcessRichText(text, context);
            }
            
            return text;
        }
        
        private void ProcessCustomProperties(Button button, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var kvp in config.CustomProperties)
            {
                var localizedValue = LocalizationManager.GetText(kvp.Value);
                
                switch (kvp.Key.ToLower())
                {
                    case "class":
                    case "css-class":
                        ApplyCssClass(button, localizedValue);
                        break;
                    case "style":
                        ApplyStyle(button, localizedValue);
                        break;
                    case "enabled":
                        ApplyEnabledState(button, localizedValue);
                        break;
                    default:
                        UnityEngine.Debug.Log($"[ButtonLocalizationProcessor] Unsupported custom property: {kvp.Key}");
                        break;
                }
            }
        }
        
        private void ApplyCssClass(Button button, string className)
        {
            if (!string.IsNullOrEmpty(className))
            {
                button.AddToClassList(className);
            }
        }
        
        private void ApplyStyle(Button button, string styleValue)
        {
            // 可以根据需要扩展样式应用逻辑
        }
        
        private void ApplyEnabledState(Button button, string enabledValue)
        {
            if (bool.TryParse(enabledValue, out var isEnabled))
            {
                button.SetEnabled(isEnabled);
            }
        }
        
        private string ProcessRichText(string text, LocalizationContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // 基本富文本处理（与Label处理器相同）
            text = text.Replace("{b}", "<b>").Replace("{/b}", "</b>");
            text = text.Replace("{i}", "<i>").Replace("{/i}", "</i>");
            text = text.Replace("{u}", "<u>").Replace("{/u}", "</u>");
            
            // 处理颜色标签
            text = System.Text.RegularExpressions.Regex.Replace(text, 
                @"\{color=([^}]+)\}([^{]*)\{/color\}", 
                "<color=$1>$2</color>");
            
            return text;
        }
    }
} 