using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.MCP.Editor.Common;

namespace com.MiAO.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// Toggle control localization processor
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
            
            // Process label text
            if (!string.IsNullOrEmpty(config.LabelKey))
            {
                var labelText = GetLocalizedText(config.LabelKey, config, context);
                toggle.label = labelText;
            }
            
            // Process text (text displayed next to checkbox)
            if (!string.IsNullOrEmpty(config.TextKey))
            {
                var textKey = config.GetConditionalTextKey(context);
                var localizedText = GetLocalizedText(textKey, config, context);
                toggle.text = localizedText;
            }
            
            // Process tooltip
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                toggle.tooltip = tooltipText;
            }
            
            // Process custom properties
            ProcessCustomProperties(toggle, config, context);
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
            
            // Basic rich text processing
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