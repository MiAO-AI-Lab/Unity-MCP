using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// Toggle控件本地化处理器
    /// </summary>
    public class ToggleLocalizationProcessor : ILocalizationProcessor
    {
        public int Priority => 100;
        
        public bool CanProcess(VisualElement element)
        {
            return element is Toggle;
        }
        
        public void Process(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            var toggle = (Toggle)element;
            
            // 处理标签文本
            if (!string.IsNullOrEmpty(config.LabelKey))
            {
                var labelText = GetLocalizedText(config.LabelKey, config, context);
                toggle.label = labelText;
            }
            
            // 处理文本（显示在复选框旁边的文本）
            if (!string.IsNullOrEmpty(config.TextKey))
            {
                var textKey = config.GetConditionalTextKey(context);
                var localizedText = GetLocalizedText(textKey, config, context);
                toggle.text = localizedText;
            }
            
            // 处理工具提示
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                toggle.tooltip = tooltipText;
            }
            
            // 处理自定义属性
            ProcessCustomProperties(toggle, config, context);
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
        
        private void ProcessCustomProperties(Toggle toggle, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var kvp in config.CustomProperties)
            {
                var localizedValue = LocalizationManager.GetText(kvp.Value);
                
                switch (kvp.Key.ToLower())
                {
                    case "class":
                    case "css-class":
                        ApplyCssClass(toggle, localizedValue);
                        break;
                    case "enabled":
                        ApplyEnabledState(toggle, localizedValue);
                        break;
                    case "value":
                        ApplyToggleValue(toggle, localizedValue);
                        break;
                    default:
                        UnityEngine.Debug.Log($"[ToggleLocalizationProcessor] Unsupported custom property: {kvp.Key}");
                        break;
                }
            }
        }
        
        private void ApplyCssClass(Toggle toggle, string className)
        {
            if (!string.IsNullOrEmpty(className))
            {
                toggle.AddToClassList(className);
            }
        }
        
        private void ApplyEnabledState(Toggle toggle, string enabledValue)
        {
            if (bool.TryParse(enabledValue, out var isEnabled))
            {
                toggle.SetEnabled(isEnabled);
            }
        }
        
        private void ApplyToggleValue(Toggle toggle, string toggleValue)
        {
            if (bool.TryParse(toggleValue, out var value))
            {
                toggle.value = value;
            }
        }
        
        private string ProcessRichText(string text, LocalizationContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // 基本富文本处理
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