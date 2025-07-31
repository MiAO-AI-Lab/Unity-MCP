using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.MCP.Editor.Common;

namespace com.MiAO.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// TextField control localization processor
    /// </summary>
    public class TextFieldLocalizationProcessor : ILocalizationProcessor
    {
        public int Priority => 100;
        
        public bool CanProcess(VisualElement element)
        {
            return element is TextField;
        }
        
        public void Process(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            var textField = (TextField)element;
            
            // Process label text
            if (!string.IsNullOrEmpty(config.LabelKey))
            {
                var labelText = GetLocalizedText(config.LabelKey, config, context);
                textField.label = labelText;
            }
            
            // Process placeholder text
            if (!string.IsNullOrEmpty(config.PlaceholderKey))
            {
                var placeholderText = GetLocalizedText(config.PlaceholderKey, config, context);
                // In Unity UI Toolkit, TextField placeholder is set through value or other means
                // Due to version differences, we comment out this part for now
                // textField.placeholder = placeholderText;
                
                // As an alternative, we can handle it in custom properties
                if (config.CustomProperties.ContainsKey("placeholder"))
                {
                    // Specific placeholder handling logic can be added here
                }
            }
            
            // Process tooltip
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                textField.tooltip = tooltipText;
            }
            
            // Process custom properties
            ProcessCustomProperties(textField, config, context);
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
        
        private void ProcessCustomProperties(TextField textField, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var kvp in config.CustomProperties)
            {
                var localizedValue = LocalizationManager.GetText(kvp.Value);
                
                switch (kvp.Key.ToLower())
                {
                    case "class":
                    case "css-class":
                        ApplyCssClass(textField, localizedValue);
                        break;
                    case "readonly":
                        ApplyReadOnlyState(textField, localizedValue);
                        break;
                    case "enabled":
                        ApplyEnabledState(textField, localizedValue);
                        break;
                    case "multiline":
                        ApplyMultilineState(textField, localizedValue);
                        break;
                    default:
                        UnityEngine.Debug.Log($"[TextFieldLocalizationProcessor] Unsupported custom property: {kvp.Key}");
                        break;
                }
            }
        }
        
        private void ApplyCssClass(TextField textField, string className)
        {
            if (!string.IsNullOrEmpty(className))
            {
                textField.AddToClassList(className);
            }
        }
        
        private void ApplyReadOnlyState(TextField textField, string readOnlyValue)
        {
            if (bool.TryParse(readOnlyValue, out var isReadOnly))
            {
                textField.isReadOnly = isReadOnly;
            }
        }
        
        private void ApplyEnabledState(TextField textField, string enabledValue)
        {
            if (bool.TryParse(enabledValue, out var isEnabled))
            {
                textField.SetEnabled(isEnabled);
            }
        }
        
        private void ApplyMultilineState(TextField textField, string multilineValue)
        {
            if (bool.TryParse(multilineValue, out var isMultiline))
            {
                textField.multiline = isMultiline;
            }
        }
    }
} 