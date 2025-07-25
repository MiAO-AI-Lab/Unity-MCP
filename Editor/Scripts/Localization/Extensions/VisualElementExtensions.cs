using System;
using UnityEngine.UIElements;

namespace com.MiAO.Unity.MCP.Editor.Localization.Extensions
{
    /// <summary>
    /// VisualElement本地化扩展方法
    /// 提供便捷的本地化API
    /// </summary>
    public static class VisualElementExtensions
    {
        #region 立即本地化方法
        
        /// <summary>
        /// 立即本地化当前元素
        /// </summary>
        /// <param name="element">要本地化的元素</param>
        /// <param name="context">本地化上下文（可选）</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T Localize<T>(this T element, LocalizationContext context = null) where T : VisualElement
        {
            UILocalizationSystem.LocalizeElement(element, context);
            return element;
        }
        
        /// <summary>
        /// 立即本地化当前元素及其所有子元素
        /// </summary>
        /// <param name="element">要本地化的根元素</param>
        /// <param name="context">本地化上下文（可选）</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T LocalizeTree<T>(this T element, LocalizationContext context = null) where T : VisualElement
        {
            UILocalizationSystem.LocalizeElementTree(element, context);
            return element;
        }
        
        #endregion
        
        #region 声明式本地化方法
        
        /// <summary>
        /// 为元素设置文本本地化键
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="textKey">文本本地化键</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T SetTextKey<T>(this T element, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-text-{textKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为元素设置工具提示本地化键
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="tooltipKey">工具提示本地化键</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T SetTooltipKey<T>(this T element, string tooltipKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-tooltip-{tooltipKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为元素设置标签本地化键
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="labelKey">标签本地化键</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T SetLabelKey<T>(this T element, string labelKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-label-{labelKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为元素设置占位符本地化键
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="placeholderKey">占位符本地化键</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T SetPlaceholderKey<T>(this T element, string placeholderKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-localize-placeholder-{placeholderKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为元素设置完整的本地化配置
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="textKey">文本键</param>
        /// <param name="tooltipKey">工具提示键</param>
        /// <param name="labelKey">标签键</param>
        /// <param name="placeholderKey">占位符键</param>
        /// <returns>元素自身（支持链式调用）</returns>
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
        
        #region 条件本地化方法
        
        /// <summary>
        /// 为元素添加条件本地化
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="property">条件属性名</param>
        /// <param name="value">条件值</param>
        /// <param name="textKey">条件满足时的文本键</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T AddConditionalText<T>(this T element, string property, string value, string textKey) where T : VisualElement
        {
            element.AddToClassList($"mcp-condition-{property}-{value}-{textKey.Replace('.', '-')}");
            return element;
        }
        
        /// <summary>
        /// 为元素添加参数化本地化
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="paramName">参数名</param>
        /// <param name="source">参数来源</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T AddParameter<T>(this T element, string paramName, string source) where T : VisualElement
        {
            element.AddToClassList($"mcp-param-{paramName}-{source.Replace('.', '-')}");
            return element;
        }
        
        #endregion
        
        #region 程序化配置方法
        
        /// <summary>
        /// 为元素注册程序化本地化配置
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="config">本地化配置</param>
        /// <returns>元素自身（支持链式调用）</returns>
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
        /// 为元素注册简单文本本地化
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="textKey">文本本地化键</param>
        /// <returns>元素自身（支持链式调用）</returns>
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
        
        #region 查询和检测方法
        
        /// <summary>
        /// 检查元素是否有本地化配置
        /// </summary>
        /// <param name="element">要检查的元素</param>
        /// <returns>如果有本地化配置返回true</returns>
        public static bool HasLocalizationConfig(this VisualElement element)
        {
            // 检查CSS类名
            foreach (var className in element.GetClasses())
            {
                if (className.StartsWith("mcp-localize-") || className.StartsWith("mcp-condition-") || className.StartsWith("mcp-param-"))
                {
                    return true;
                }
            }
            
            // 检查程序化配置
            if (!string.IsNullOrEmpty(element.name))
            {
                // 简单检查：这里可以扩展为调用ConfigProvider
                return true; // 暂时返回true，实际实现需要查询配置提供者
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取元素的所有本地化键
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <returns>本地化键集合</returns>
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
        
        #region 性能优化方法
        
        /// <summary>
        /// 延迟本地化元素（在下一帧执行）
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="context">本地化上下文</param>
        /// <returns>元素自身（支持链式调用）</returns>
        public static T LocalizeDelayed<T>(this T element, LocalizationContext context = null) where T : VisualElement
        {
            element.schedule.Execute(() => UILocalizationSystem.LocalizeElement(element, context));
            return element;
        }
        
        /// <summary>
        /// 批量本地化多个元素
        /// </summary>
        /// <param name="elements">要本地化的元素集合</param>
        /// <param name="context">本地化上下文</param>
        public static void LocalizeBatch(this System.Collections.Generic.IEnumerable<VisualElement> elements, LocalizationContext context = null)
        {
            context ??= new LocalizationContext
            {
                CurrentLanguage = com.MiAO.Unity.MCP.Editor.Common.LocalizationManager.CurrentLanguage.ToString(),
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