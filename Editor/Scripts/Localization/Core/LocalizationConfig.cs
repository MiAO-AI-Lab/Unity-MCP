using System;
using System.Collections.Generic;
using System.Linq;

namespace com.MiAO.Unity.MCP.Editor.Localization
{
    /// <summary>
    /// 本地化配置数据结构
    /// 支持多维度本地化和条件控制
    /// </summary>
    [Serializable]
    public class LocalizationConfig
    {
        /// <summary>
        /// 文本本地化键
        /// </summary>
        public string TextKey { get; set; }
        
        /// <summary>
        /// 工具提示本地化键
        /// </summary>
        public string TooltipKey { get; set; }
        
        /// <summary>
        /// 图标本地化键
        /// </summary>
        public string IconKey { get; set; }
        
        /// <summary>
        /// 样式本地化键
        /// </summary>
        public string StyleKey { get; set; }
        
        /// <summary>
        /// 占位符文本本地化键
        /// </summary>
        public string PlaceholderKey { get; set; }
        
        /// <summary>
        /// 标签文本本地化键
        /// </summary>
        public string LabelKey { get; set; }
        
        /// <summary>
        /// 自定义属性本地化
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// 条件本地化规则
        /// </summary>
        public List<LocalizationCondition> Conditions { get; set; } = new List<LocalizationCondition>();
        
        /// <summary>
        /// 格式化参数
        /// </summary>
        public List<LocalizationParameter> Parameters { get; set; } = new List<LocalizationParameter>();
        
        /// <summary>
        /// 是否启用HTML支持
        /// </summary>
        public bool EnableHtml { get; set; } = false;
        
        /// <summary>
        /// 是否启用富文本标签
        /// </summary>
        public bool EnableRichText { get; set; } = false;
        
        /// <summary>
        /// 更新策略
        /// </summary>
        public LocalizationUpdateStrategy UpdateStrategy { get; set; } = LocalizationUpdateStrategy.Immediate;
        
        /// <summary>
        /// 缓存策略
        /// </summary>
        public LocalizationCacheStrategy CacheStrategy { get; set; } = LocalizationCacheStrategy.Default;
        
        /// <summary>
        /// 获取所有本地化键
        /// </summary>
        /// <returns>所有相关的本地化键</returns>
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
        /// 检查是否有效的配置
        /// </summary>
        /// <returns>如果配置有效返回true</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(TextKey) || 
                   !string.IsNullOrEmpty(TooltipKey) || 
                   !string.IsNullOrEmpty(LabelKey) ||
                   CustomProperties.Any() ||
                   Conditions.Any();
        }
        
        /// <summary>
        /// 获取基于条件的文本键
        /// </summary>
        /// <param name="context">本地化上下文</param>
        /// <returns>匹配条件的文本键，如果没有匹配返回默认TextKey</returns>
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
    /// 本地化条件
    /// </summary>
    [Serializable]
    public class LocalizationCondition
    {
        /// <summary>
        /// 条件属性名
        /// </summary>
        public string Property { get; set; }
        
        /// <summary>
        /// 条件操作符
        /// </summary>
        public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;
        
        /// <summary>
        /// 条件值
        /// </summary>
        public string Value { get; set; }
        
        /// <summary>
        /// 条件匹配时使用的文本键
        /// </summary>
        public string TextKey { get; set; }
        
        /// <summary>
        /// 条件优先级
        /// </summary>
        public int Priority { get; set; } = 0;
        
        /// <summary>
        /// 评估条件是否满足
        /// </summary>
        /// <param name="context">本地化上下文</param>
        /// <returns>条件满足返回true</returns>
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
    /// 本地化参数
    /// </summary>
    [Serializable]
    public class LocalizationParameter
    {
        /// <summary>
        /// 参数名
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 参数值来源
        /// </summary>
        public string Source { get; set; }
        
        /// <summary>
        /// 默认值
        /// </summary>
        public string DefaultValue { get; set; }
        
        /// <summary>
        /// 格式化字符串
        /// </summary>
        public string Format { get; set; }
        
        /// <summary>
        /// 获取参数值
        /// </summary>
        /// <param name="context">本地化上下文</param>
        /// <returns>参数值</returns>
        public object GetValue(LocalizationContext context)
        {
            if (string.IsNullOrEmpty(Source))
                return DefaultValue;
                
            // 支持从上下文获取值
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
    /// 条件操作符
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
    /// 本地化更新策略
    /// </summary>
    public enum LocalizationUpdateStrategy
    {
        /// <summary>
        /// 立即更新
        /// </summary>
        Immediate,
        
        /// <summary>
        /// 延迟更新
        /// </summary>
        Deferred,
        
        /// <summary>
        /// 批量更新
        /// </summary>
        Batched,
        
        /// <summary>
        /// 手动更新
        /// </summary>
        Manual
    }
    
    /// <summary>
    /// 本地化缓存策略
    /// </summary>
    public enum LocalizationCacheStrategy
    {
        /// <summary>
        /// 默认缓存
        /// </summary>
        Default,
        
        /// <summary>
        /// 无缓存
        /// </summary>
        NoCache,
        
        /// <summary>
        /// 强缓存
        /// </summary>
        Aggressive,
        
        /// <summary>
        /// 会话缓存
        /// </summary>
        Session
    }
} 