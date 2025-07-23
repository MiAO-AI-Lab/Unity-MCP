using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace com.MiAO.Unity.MCP.Editor.Localization.Providers
{
    /// <summary>
    /// 基于UXML属性的本地化配置提供者
    /// 支持通过CSS类名声明本地化需求
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
            
            // 从CSS类名中提取配置
            foreach (var className in element.GetClasses())
            {
                if (TryParseLocalizationClass(className, out var propertyType, out var key))
                {
                    SetConfigProperty(config, propertyType, key);
                    hasConfig = true;
                }
            }
            
            // 检查自定义数据属性（如果Unity支持）
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
                    // 自定义属性
                    config.CustomProperties[propertyType] = key;
                    break;
            }
        }
        
        private void ExtractCustomDataAttributes(VisualElement element, LocalizationConfig config, ref bool hasConfig)
        {
            // 这里可以扩展支持自定义数据属性
            // 由于Unity UI Toolkit的限制，暂时使用CSS类名方式
            
            // 检查是否有条件本地化的类名
            ExtractConditionalConfig(element, config, ref hasConfig);
            
            // 检查是否有参数化配置的类名
            ExtractParameterConfig(element, config, ref hasConfig);
        }
        
        private void ExtractConditionalConfig(VisualElement element, LocalizationConfig config, ref bool hasConfig)
        {
            foreach (var className in element.GetClasses())
            {
                // 格式：mcp-condition-property-value-textkey
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
                // 格式：mcp-param-paramName-source
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
    /// 属性配置提供者的扩展方法
    /// </summary>
    public static class AttributeConfigProviderExtensions
    {
        /// <summary>
        /// 为UI元素添加文本本地化
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <param name="textKey">本地化键</param>
        /// <returns>UI元素自身（支持链式调用）</returns>
        public static T WithLocalizedText<T>(this T element, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-text-{textKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为UI元素添加工具提示本地化
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <param name="tooltipKey">工具提示本地化键</param>
        /// <returns>UI元素自身（支持链式调用）</returns>
        public static T WithLocalizedTooltip<T>(this T element, string tooltipKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-tooltip-{tooltipKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为UI元素添加标签本地化
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <param name="labelKey">标签本地化键</param>
        /// <returns>UI元素自身（支持链式调用）</returns>
        public static T WithLocalizedLabel<T>(this T element, string labelKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-label-{labelKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为UI元素添加占位符本地化
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <param name="placeholderKey">占位符本地化键</param>
        /// <returns>UI元素自身（支持链式调用）</returns>
        public static T WithLocalizedPlaceholder<T>(this T element, string placeholderKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-placeholder-{placeholderKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为UI元素添加条件本地化
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <param name="property">条件属性</param>
        /// <param name="value">条件值</param>
        /// <param name="textKey">条件满足时的文本键</param>
        /// <returns>UI元素自身（支持链式调用）</returns>
        public static T WithConditionalText<T>(this T element, string property, string value, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-condition-{property}-{value}-{textKey.Replace('.', '-')}");
            return element;
        }
    }
} 