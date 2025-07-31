using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.MCP.Editor.Common;

namespace com.MiAO.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// Foldout control localization processor
    /// </summary>
    public class FoldoutLocalizationProcessor : ILocalizationProcessor
    {
        public int Priority => 100;
        
        public bool CanProcess(VisualElement element)
        {
            return element is Foldout;
        }
        
        public void Process(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            var foldout = (Foldout)element;
            
            // Process title text
            if (!string.IsNullOrEmpty(config.TextKey))
            {
                var textKey = config.GetConditionalTextKey(context);
                var localizedText = GetLocalizedText(textKey, config, context);
                foldout.text = localizedText;
            }
            
            // Process tooltip
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                foldout.tooltip = tooltipText;
            }
            
            // Process custom properties
            ProcessCustomProperties(foldout, config, context);
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
        
        private void ProcessCustomProperties(Foldout foldout, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var kvp in config.CustomProperties)
            {
                var localizedValue = LocalizationManager.GetText(kvp.Value);
                
                switch (kvp.Key.ToLower())
                {
                    case "class":
                    case "css-class":
                        ApplyCssClass(foldout, localizedValue);
                        break;
                    case "value":
                    case "expanded":
                        ApplyExpandedState(foldout, localizedValue);
                        break;
                    case "enabled":
                        ApplyEnabledState(foldout, localizedValue);
                        break;
                    default:
                        UnityEngine.Debug.Log($"[FoldoutLocalizationProcessor] Unsupported custom property: {kvp.Key}");
                        break;
                }
            }
        }
        
        private void ApplyCssClass(Foldout foldout, string className)
        {
            if (!string.IsNullOrEmpty(className))
            {
                foldout.AddToClassList(className);
            }
        }
        
        private void ApplyExpandedState(Foldout foldout, string expandedValue)
        {
            if (bool.TryParse(expandedValue, out var isExpanded))
            {
                foldout.value = isExpanded;
            }
        }
        
        private void ApplyEnabledState(Foldout foldout, string enabledValue)
        {
            if (bool.TryParse(enabledValue, out var isEnabled))
            {
                foldout.SetEnabled(isEnabled);
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