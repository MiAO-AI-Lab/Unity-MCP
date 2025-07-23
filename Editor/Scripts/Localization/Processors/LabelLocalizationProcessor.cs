using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// Label控件本地化处理器
    /// </summary>
    public class LabelLocalizationProcessor : ILocalizationProcessor
    {
        public int Priority => 100;
        
        public bool CanProcess(VisualElement element)
        {
            return element is Label;
        }
        
        public void Process(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            var label = (Label)element;
            
            // 处理主文本
            if (!string.IsNullOrEmpty(config.TextKey))
            {
                var textKey = config.GetConditionalTextKey(context);
                var localizedText = GetLocalizedText(textKey, config, context);
                label.text = localizedText;
            }
            
            // 处理工具提示
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                label.tooltip = tooltipText;
            }
            
            // 处理自定义属性
            ProcessCustomProperties(label, config, context);
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
        
        private void ProcessCustomProperties(Label label, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var kvp in config.CustomProperties)
            {
                var localizedValue = LocalizationManager.GetText(kvp.Value);
                
                // 根据属性名处理不同的自定义属性
                switch (kvp.Key.ToLower())
                {
                    case "class":
                    case "css-class":
                        ApplyCssClass(label, localizedValue);
                        break;
                    case "style":
                        ApplyStyle(label, localizedValue);
                        break;
                    default:
                        // 其他自定义属性可以在这里扩展
                        UnityEngine.Debug.Log($"[LabelLocalizationProcessor] Unsupported custom property: {kvp.Key}");
                        break;
                }
            }
        }
        
        private void ApplyCssClass(Label label, string className)
        {
            if (!string.IsNullOrEmpty(className))
            {
                label.AddToClassList(className);
            }
        }
        
        private void ApplyStyle(Label label, string styleValue)
        {
            // 这里可以根据需要解析和应用内联样式
            // 暂时留空，可以根据具体需求扩展
        }
        
        private string ProcessRichText(string text, LocalizationContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // 处理简单的富文本标签
            // 例如：{b}bold{/b} -> <b>bold</b>
            text = text.Replace("{b}", "<b>").Replace("{/b}", "</b>");
            text = text.Replace("{i}", "<i>").Replace("{/i}", "</i>");
            text = text.Replace("{u}", "<u>").Replace("{/u}", "</u>");
            
            // 处理颜色标签
            // 例如：{color=red}text{/color} -> <color=red>text</color>
            text = System.Text.RegularExpressions.Regex.Replace(text, 
                @"\{color=([^}]+)\}([^{]*)\{/color\}", 
                "<color=$1>$2</color>");
            
            return text;
        }
    }
} 