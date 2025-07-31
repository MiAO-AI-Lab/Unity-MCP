using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.MCP.Editor.Common;

namespace com.MiAO.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// Button control localization processor
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
            
            // Process button text
            if (!string.IsNullOrEmpty(config.TextKey))
            {
                var textKey = config.GetConditionalTextKey(context);
                var localizedText = GetLocalizedText(textKey, config, context);
                button.text = localizedText;
            }
            
            // Process tooltip
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                button.tooltip = tooltipText;
            }
            
            // Process custom properties
            ProcessCustomProperties(button, config, context);
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
            // Style logic can be extended as needed
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
            
            // Basic rich text processing (same as Label processor)
            text = text.Replace("{b}", "<b>").Replace("{/b}", "</b>");
            text = text.Replace("{i}", "<i>").Replace("{/i}", "</i>");
            text = text.Replace("{u}", "<u>").Replace("{/u}", "</u>");
            
            // Process color tags
            text = System.Text.RegularExpressions.Regex.Replace(text, 
                @"\{color=([^}]+)\}([^{]*)\{/color\}", 
                "<color=$1>$2</color>");
            
            return text;
        }
    }
} 