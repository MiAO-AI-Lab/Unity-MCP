using System;
using UnityEngine.UIElements;

namespace com.MiAO.MCP.Editor.Localization.Extensions
{
    /// <summary>
    /// VisualElement localization extension methods
    /// Provides convenient localization APIs
    /// </summary>
    public static class VisualElementExtensions
    {
        #region Immediate Localization Methods
        
        /// <summary>
        /// Immediately localize the current element
        /// </summary>
        /// <param name="element">Element to localize</param>
        /// <param name="context">Localization context (optional)</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T Localize<T>(this T element, LocalizationContext context = null) where T : VisualElement
        {
            UILocalizationSystem.LocalizeElement(element, context);
            return element;
        }
        
        /// <summary>
        /// Immediately localize the current element and all its child elements
        /// </summary>
        /// <param name="element">Root element to localize</param>
        /// <param name="context">Localization context (optional)</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T LocalizeTree<T>(this T element, LocalizationContext context = null) where T : VisualElement
        {
            UILocalizationSystem.LocalizeElementTree(element, context);
            return element;
        }
        
        #endregion
        
        #region Declarative Localization Methods
        
        /// <summary>
        /// Set text localization key for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="textKey">Text localization key</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T SetTextKey<T>(this T element, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-text-{textKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Set tooltip localization key for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="tooltipKey">Tooltip localization key</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T SetTooltipKey<T>(this T element, string tooltipKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-tooltip-{tooltipKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Set label localization key for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="labelKey">Label localization key</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T SetLabelKey<T>(this T element, string labelKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-label-{labelKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Set placeholder localization key for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="placeholderKey">Placeholder localization key</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T SetPlaceholderKey<T>(this T element, string placeholderKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-placeholder-{placeholderKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Set complete localization configuration for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="textKey">Text key</param>
        /// <param name="tooltipKey">Tooltip key</param>
        /// <param name="labelKey">Label key</param>
        /// <param name="placeholderKey">Placeholder key</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T SetLocalizationKeys<T>(this T element, 
            string textKey = null, 
            string tooltipKey = null, 
            string labelKey = null, 
            string placeholderKey = null) where T : VisualElement
        {
            if (!string.IsNullOrEmpty(textKey)) element.SetTextKey(textKey);
            if (!string.IsNullOrEmpty(tooltipKey)) element.SetTooltipKey(tooltipKey);
            if (!string.IsNullOrEmpty(labelKey)) element.SetLabelKey(labelKey);
            if (!string.IsNullOrEmpty(placeholderKey)) element.SetPlaceholderKey(placeholderKey);
            return element;
        }
        
        #endregion
        
        #region Conditional Localization Methods
        
        /// <summary>
        /// Add conditional localization for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="property">Condition property name</param>
        /// <param name="value">Condition value</param>
        /// <param name="textKey">Text key when condition is met</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T AddConditionalText<T>(this T element, string property, string value, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-condition-{property}-{value}-{textKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// Add parameterized localization for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="paramName">Parameter name</param>
        /// <param name="source">Parameter source</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T AddParameter<T>(this T element, string paramName, string source) where T : VisualElement
        {
            element.AddToClassList($"mcp-param-{paramName}-{source.Replace('.', '-')}");
            return element;
        }
        
        #endregion
        
        #region Programmatic Configuration Methods
        
        /// <summary>
        /// Register programmatic localization configuration for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="config">Localization configuration</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T RegisterConfig<T>(this T element, LocalizationConfig config) where T : VisualElement
        {
            if (!string.IsNullOrEmpty(element.name))
            {
                Providers.CodeConfigProvider.RegisterConfig(element.name, config);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[VisualElementExtensions] Cannot register config for element without name. Element type: {element.GetType().Name}");
            }
            return element;
        }
        
        /// <summary>
        /// Register simple text localization for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="textKey">Text localization key</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T RegisterText<T>(this T element, string textKey) where T : VisualElement
        {
            if (!string.IsNullOrEmpty(element.name))
            {
                Providers.CodeConfigProvider.RegisterTextConfig(element.name, textKey);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[VisualElementExtensions] Cannot register text config for element without name. Element type: {element.GetType().Name}");
            }
            return element;
        }
        
        #endregion
        
        #region Query and Detection Methods
        
        /// <summary>
        /// Check if element has localization configuration
        /// </summary>
        /// <param name="element">Element to check</param>
        /// <returns>True if element has localization configuration</returns>
        public static bool HasLocalizationConfig(this VisualElement element)
        {
            // Check CSS class names
            foreach (var className in element.GetClasses())
            {
                if (className.StartsWith("mcp-localize-") || className.StartsWith("mcp-condition-") || className.StartsWith("mcp-param-"))
                {
                    return true;
                }
            }
            
            // Check programmatic configuration
            if (!string.IsNullOrEmpty(element.name))
            {
                // Simple check: this can be extended to call ConfigProvider
                return true; // Temporarily return true, actual implementation needs to query configuration providers
            }
            
            return false;
        }
        
        /// <summary>
        /// Get all localization keys for element
        /// </summary>
        /// <param name="element">Target element</param>
        /// <returns>Collection of localization keys</returns>
        public static System.Collections.Generic.List<string> GetLocalizationKeys(this VisualElement element)
        {
            var keys = new System.Collections.Generic.List<string>();
            
            foreach (var className in element.GetClasses())
            {
                if (className.StartsWith("mcp-localize-"))
                {
                    var parts = className.Substring("mcp-localize-".Length).Split('-', 2);
                    if (parts.Length == 2)
                    {
                        keys.Add(parts[1].Replace('-', '.'));
                    }
                }
            }
            
            return keys;
        }
        
        #endregion
        
        #region Performance Optimization Methods
        
        /// <summary>
        /// Localize element with delay (execute in next frame)
        /// </summary>
        /// <param name="element">Target element</param>
        /// <param name="context">Localization context</param>
        /// <returns>Element itself (supports method chaining)</returns>
        public static T LocalizeDelayed<T>(this T element, LocalizationContext context = null) where T : VisualElement
        {
            element.schedule.Execute(() => UILocalizationSystem.LocalizeElement(element, context));
            return element;
        }
        
        /// <summary>
        /// Batch localize multiple elements
        /// </summary>
        /// <param name="elements">Collection of elements to localize</param>
        /// <param name="context">Localization context</param>
        public static void LocalizeBatch(this System.Collections.Generic.IEnumerable<VisualElement> elements, LocalizationContext context = null)
        {
            context ??= new LocalizationContext
            {
                CurrentLanguage = com.MiAO.MCP.Editor.Common.LocalizationManager.CurrentLanguage.ToString(),
                IsBatchMode = true,
                Stats = new LocalizationPerformanceStats()
            };
            
            foreach (var element in elements)
            {
                UILocalizationSystem.LocalizeElement(element, context);
            }
        }
        
        #endregion
    }
} 