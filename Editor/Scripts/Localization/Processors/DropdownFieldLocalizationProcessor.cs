using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// DropdownField control localization processor
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
            
            // Process label text
            if (!string.IsNullOrEmpty(config.LabelKey))
            {
                var labelText = GetLocalizedText(config.LabelKey, config, context);
                dropdown.label = labelText;
            }
            
            // Process tooltip
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                dropdown.tooltip = tooltipText;
            }
            
            // Process custom properties
            ProcessCustomProperties(dropdown, config, context);
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
            // Support for localized choice list
            // Format: choice1.key,choice2.key,choice3.key
            if (string.IsNullOrEmpty(choicesConfig)) return;
            
            var choiceKeys = choicesConfig.Split(',');
            var localizedChoices = choiceKeys.Select(key => LocalizationManager.GetText(key.Trim())).ToList();
            
            // Save current selected value
            var currentValue = dropdown.value;
            var currentIndex = dropdown.choices?.IndexOf(currentValue) ?? -1;
            
            // Update choice list
            dropdown.choices = localizedChoices;
            
            // Try to maintain selection state
            if (currentIndex >= 0 && currentIndex < localizedChoices.Count)
            {
                dropdown.value = localizedChoices[currentIndex];
            }
        }
    }
} 