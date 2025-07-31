using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.MCP.Editor.Common;

namespace com.MiAO.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// Label control localization processor
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
            
            // Process main text
            if (!string.IsNullOrEmpty(config.TextKey))
            {
                var textKey = config.GetConditionalTextKey(context);
                var localizedText = GetLocalizedText(textKey, config, context);
                label.text = localizedText;
            }
            
            // Process tooltip
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                label.tooltip = tooltipText;
            }
            
            // Process custom properties
            ProcessCustomProperties(label, config, context);
        }
        
        private string GetLocalizedText(string key, LocalizationConfig config, LocalizationContext context)
        {
            var text = LocalizationManager.GetText(key);
            
            // Process formatting parameters
            if (config.Parameters != null && config.Parameters.Count > 0)
            {
                var args = config.Parameters.Select(p => p.GetValue(context)).ToArray();
                text = string.Format(text, args);
            }
            
            // Process rich text
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
                
                // Process different custom properties based on property name
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
                        // Other custom properties can be extended here
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
            // Style parsing and application can be implemented here as needed
            // Currently left empty, can be extended based on specific requirements
        }
        
        private string ProcessRichText(string text, LocalizationContext context)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Process simple rich text tags
            // Example: {b}bold{/b} -> <b>bold</b>
            text = text.Replace("{b}", "<b>").Replace("{/b}", "</b>");
            text = text.Replace("{i}", "<i>").Replace("{/i}", "</i>");
            text = text.Replace("{u}", "<u>").Replace("{/u}", "</u>");
            
            // Process color tags
            // Example: {color=red}text{/color} -> <color=red>text</color>
            text = System.Text.RegularExpressions.Regex.Replace(text, 
                @"\{color=([^}]+)\}([^{]*)\{/color\}", 
                "<color=$1>$2</color>");
            
            return text;
        }
    }
} 