using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace com.MiAO.MCP.Editor.Localization.Providers
{
    /// <summary>
    /// UXML attribute-based localization configuration provider
    /// Supports declaring localization requirements through CSS class names
    /// </summary>
    public class AttributeConfigProvider : ILocalizationConfigProvider
    {
        private const string LOCALIZE_PREFIX = "mcp-localize-";
        private const string TEXT_CLASS = "mcp-localize-text-";
        private const string TOOLTIP_CLASS = "mcp-localize-tooltip-";
        private const string LABEL_CLASS = "mcp-localize-label-";
        private const string PLACEHOLDER_CLASS = "mcp-localize-placeholder-";
        
        public LocalizationConfig GetConfig(VisualElement element)
        {
            if (element == null) return null;
            
            var config = new LocalizationConfig();
            bool hasConfig = false;
            
            // Extract configuration from CSS class names
            foreach (var className in element.GetClasses())
            {
                if (TryParseLocalizationClass(className, out var propertyType, out var key))
                {
                    SetConfigProperty(config, propertyType, key);
                    hasConfig = true;
                }
            }
            
            // Check custom data attributes (if Unity supports it)
            ExtractCustomDataAttributes(element, config, ref hasConfig);
            
            return hasConfig ? config : null;
        }
        
        private bool TryParseLocalizationClass(string className, out string propertyType, out string key)
        {
            propertyType = null;
            key = null;
            
            if (!className.StartsWith(LOCALIZE_PREFIX))
                return false;
            
            var parts = className.Substring(LOCALIZE_PREFIX.Length).Split('-', 2);
            if (parts.Length != 2)
                return false;
            
            propertyType = parts[0];
            key = parts[1].Replace('-', '.');
            return true;
        }
        
        private void SetConfigProperty(LocalizationConfig config, string propertyType, string key)
        {
            switch (propertyType.ToLower())
            {
                case "text":
                    config.TextKey = key;
                    break;
                case "tooltip":
                    config.TooltipKey = key;
                    break;
                case "label":
                    config.LabelKey = key;
                    break;
                case "placeholder":
                    config.PlaceholderKey = key;
                    break;
                case "icon":
                    config.IconKey = key;
                    break;
                case "style":
                    config.StyleKey = key;
                    break;
                default:
                    // Custom properties
                    config.CustomProperties[propertyType] = key;
                    break;
            }
        }
        
        private void ExtractCustomDataAttributes(VisualElement element, LocalizationConfig config, ref bool hasConfig)
        {
            // This can be extended to support custom data attributes
            // Due to Unity UI Toolkit limitations, we currently use CSS class names
            
            // Check for conditional localization class names
            ExtractConditionalConfig(element, config, ref hasConfig);
            
            // Check for parameterized configuration class names
            ExtractParameterConfig(element, config, ref hasConfig);
        }
        
        private void ExtractConditionalConfig(VisualElement element, LocalizationConfig config, ref bool hasConfig)
        {
            foreach (var className in element.GetClasses())
            {
                // Format: mcp-condition-property-value-textkey
                if (className.StartsWith("mcp-condition-"))
                {
                    var parts = className.Substring("mcp-condition-".Length).Split('-');
                    if (parts.Length >= 3)
                    {
                        var condition = new LocalizationCondition
                        {
                            Property = parts[0],
                            Operator = ConditionOperator.Equals,
                            Value = parts[1],
                            TextKey = string.Join(".", parts.Skip(2)).Replace('-', '.')
                        };
                        
                        config.Conditions.Add(condition);
                        hasConfig = true;
                    }
                }
            }
        }
        
        private void ExtractParameterConfig(VisualElement element, LocalizationConfig config, ref bool hasConfig)
        {
            foreach (var className in element.GetClasses())
            {
                // Format: mcp-param-paramName-source
                if (className.StartsWith("mcp-param-"))
                {
                    var parts = className.Substring("mcp-param-".Length).Split('-', 2);
                    if (parts.Length == 2)
                    {
                        var parameter = new LocalizationParameter
                        {
                            Name = parts[0],
                            Source = parts[1].Replace('-', '.')
                        };
                        
                        config.Parameters.Add(parameter);
                        hasConfig = true;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Extension methods for attribute configuration provider
    /// </summary>
    public static class AttributeConfigProviderExtensions
    {
        /// <summary>
        /// Add text localization for UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <param name="textKey">Localization key</param>
        /// <returns>UI element itself (supports method chaining)</returns>
        public static T WithLocalizedText<T>(this T element, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-text-{textKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Add tooltip localization for UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <param name="tooltipKey">Tooltip localization key</param>
        /// <returns>UI element itself (supports method chaining)</returns>
        public static T WithLocalizedTooltip<T>(this T element, string tooltipKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-tooltip-{tooltipKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Add label localization for UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <param name="labelKey">Label localization key</param>
        /// <returns>UI element itself (supports method chaining)</returns>
        public static T WithLocalizedLabel<T>(this T element, string labelKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-label-{labelKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Add placeholder localization for UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <param name="placeholderKey">Placeholder localization key</param>
        /// <returns>UI element itself (supports method chaining)</returns>
        public static T WithLocalizedPlaceholder<T>(this T element, string placeholderKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-placeholder-{placeholderKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Add conditional localization for UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <param name="property">Condition property</param>
        /// <param name="value">Condition value</param>
        /// <param name="textKey">Text key when condition is met</param>
        /// <returns>UI element itself (supports method chaining)</returns>
        public static T WithConditionalText<T>(this T element, string property, string value, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-condition-{property}-{value}-{textKey.Replace('.', '-')}");
            return element;
        }
    }
} 