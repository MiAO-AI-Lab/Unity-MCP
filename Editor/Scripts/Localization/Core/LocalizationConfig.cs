using System;
using System.Collections.Generic;
using System.Linq;

namespace com.MiAO.Unity.MCP.Editor.Localization
{
    /// <summary>
    /// Localization configuration data structure
    /// Supports multi-dimensional localization and conditional control
    /// </summary>
    [Serializable]
    public class LocalizationConfig
    {
        /// <summary>
        /// Text localization key
        /// </summary>
        public string TextKey { get; set; }
        
        /// <summary>
        /// Tooltip localization key
        /// </summary>
        public string TooltipKey { get; set; }
        
        /// <summary>
        /// Icon localization key
        /// </summary>
        public string IconKey { get; set; }
        
        /// <summary>
        /// Style localization key
        /// </summary>
        public string StyleKey { get; set; }
        
        /// <summary>
        /// Placeholder text localization key
        /// </summary>
        public string PlaceholderKey { get; set; }
        
        /// <summary>
        /// Label text localization key
        /// </summary>
        public string LabelKey { get; set; }
        
        /// <summary>
        /// Custom property localization
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Conditional localization rules
        /// </summary>
        public List<LocalizationCondition> Conditions { get; set; } = new List<LocalizationCondition>();
        
        /// <summary>
        /// Formatting parameters
        /// </summary>
        public List<LocalizationParameter> Parameters { get; set; } = new List<LocalizationParameter>();
        
        /// <summary>
        /// Whether to enable HTML support
        /// </summary>
        public bool EnableHtml { get; set; } = false;
        
        /// <summary>
        /// Whether to enable rich text tags
        /// </summary>
        public bool EnableRichText { get; set; } = false;
        
        /// <summary>
        /// Update strategy
        /// </summary>
        public LocalizationUpdateStrategy UpdateStrategy { get; set; } = LocalizationUpdateStrategy.Immediate;
        
        /// <summary>
        /// Cache strategy
        /// </summary>
        public LocalizationCacheStrategy CacheStrategy { get; set; } = LocalizationCacheStrategy.Default;
        
        /// <summary>
        /// Get all localization keys
        /// </summary>
        /// <returns>All related localization keys</returns>
        public IEnumerable<string> GetAllKeys()
        {
            var keys = new List<string>();
            
            if (!string.IsNullOrEmpty(TextKey)) keys.Add(TextKey);
            if (!string.IsNullOrEmpty(TooltipKey)) keys.Add(TooltipKey);
            if (!string.IsNullOrEmpty(IconKey)) keys.Add(IconKey);
            if (!string.IsNullOrEmpty(StyleKey)) keys.Add(StyleKey);
            if (!string.IsNullOrEmpty(PlaceholderKey)) keys.Add(PlaceholderKey);
            if (!string.IsNullOrEmpty(LabelKey)) keys.Add(LabelKey);
            
            keys.AddRange(CustomProperties.Values.Where(v => !string.IsNullOrEmpty(v)));
            keys.AddRange(Conditions.Select(c => c.TextKey).Where(k => !string.IsNullOrEmpty(k)));
            
            return keys.Distinct();
        }
        
        /// <summary>
        /// Check if the configuration is valid
        /// </summary>
        /// <returns>Returns true if the configuration is valid</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(TextKey) || 
                   !string.IsNullOrEmpty(TooltipKey) || 
                   !string.IsNullOrEmpty(LabelKey) ||
                   CustomProperties.Any() ||
                   Conditions.Any();
        }
        
        /// <summary>
        /// Get conditional text key
        /// </summary>
        /// <param name="context">Localization context</param>
        /// <returns>Text key matching the condition, returns default TextKey if no match</returns>
        public string GetConditionalTextKey(LocalizationContext context)
        {
            if (Conditions == null || !Conditions.Any())
                return TextKey;
                
            foreach (var condition in Conditions)
            {
                if (condition.EvaluateCondition(context))
                    return condition.TextKey;
            }
            
            return TextKey;
        }
    }
    
    /// <summary>
    /// Localization condition
    /// </summary>
    [Serializable]
    public class LocalizationCondition
    {
        /// <summary>
        /// Condition property name
        /// </summary>
        public string Property { get; set; }
        
        /// <summary>
        /// Condition operator
        /// </summary>
        public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;
        
        /// <summary>
        /// Condition value
        /// </summary>
        public string Value { get; set; }
        
        /// <summary>
        /// Text key to use when condition matches
        /// </summary>
        public string TextKey { get; set; }
        
        /// <summary>
        /// Condition priority
        /// </summary>
        public int Priority { get; set; } = 0;
        
        /// <summary>
        /// Evaluate whether the condition is satisfied
        /// </summary>
        /// <param name="context">Localization context</param>
        /// <returns>Returns true if condition is satisfied</returns>
        public bool EvaluateCondition(LocalizationContext context)
        {
            if (context?.Properties == null || !context.Properties.ContainsKey(Property))
                return false;
                
            var contextValue = context.Properties[Property]?.ToString() ?? "";
            
            return Operator switch
            {
                ConditionOperator.Equals => contextValue.Equals(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.NotEquals => !contextValue.Equals(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.Contains => contextValue.Contains(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.StartsWith => contextValue.StartsWith(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.EndsWith => contextValue.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
    
    /// <summary>
    /// Localization parameter
    /// </summary>
    [Serializable]
    public class LocalizationParameter
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Parameter value source
        /// </summary>
        public string Source { get; set; }
        
        /// <summary>
        /// Default value
        /// </summary>
        public string DefaultValue { get; set; }
        
        /// <summary>
        /// Format string
        /// </summary>
        public string Format { get; set; }
        
        /// <summary>
        /// Get parameter value
        /// </summary>
        /// <param name="context">Localization context</param>
        /// <returns>Parameter value</returns>
        public object GetValue(LocalizationContext context)
        {
            if (string.IsNullOrEmpty(Source))
                return DefaultValue;
                
            // Support getting values from context
            if (Source.StartsWith("context.") && context?.Properties != null)
            {
                var propertyName = Source.Substring("context.".Length);
                if (context.Properties.TryGetValue(propertyName, out var value))
                    return value;
            }
            
            return DefaultValue;
        }
    }
    
    /// <summary>
    /// Condition operator
    /// </summary>
    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith
    }
    
    /// <summary>
    /// Localization update strategy
    /// </summary>
    public enum LocalizationUpdateStrategy
    {
        /// <summary>
        /// Immediate update
        /// </summary>
        Immediate,
        
        /// <summary>
        /// Deferred update
        /// </summary>
        Deferred,
        
        /// <summary>
        /// Batch update
        /// </summary>
        Batched,
        
        /// <summary>
        /// Manual update
        /// </summary>
        Manual
    }
    
    /// <summary>
    /// Localization cache strategy
    /// </summary>
    public enum LocalizationCacheStrategy
    {
        /// <summary>
        /// Default cache
        /// </summary>
        Default,
        
        /// <summary>
        /// No cache
        /// </summary>
        NoCache,
        
        /// <summary>
        /// Aggressive cache
        /// </summary>
        Aggressive,
        
        /// <summary>
        /// Session cache
        /// </summary>
        Session
    }
} 