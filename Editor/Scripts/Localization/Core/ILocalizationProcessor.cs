using UnityEngine.UIElements;

namespace com.MiAO.MCP.Editor.Localization
{
    /// <summary>
    /// 本地化处理器接口
    /// 定义了如何处理不同类型UI元素的本地化
    /// </summary>
    public interface ILocalizationProcessor
    {
        /// <summary>
        /// 处理器优先级，数值越高优先级越高
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 检查是否可以处理指定的UI元素
        /// </summary>
        /// <param name="element">要检查的UI元素</param>
        /// <returns>如果可以处理返回true</returns>
        bool CanProcess(VisualElement element);
        
        /// <summary>
        /// 处理UI元素的本地化
        /// </summary>
        /// <param name="element">要处理的UI元素</param>
        /// <param name="config">本地化配置</param>
        /// <param name="context">本地化上下文</param>
        void Process(VisualElement element, LocalizationConfig config, LocalizationContext context);
    }
    
    /// <summary>
    /// 本地化上下文，提供处理时需要的额外信息
    /// </summary>
    public class LocalizationContext
    {
        /// <summary>
        /// 当前语言
        /// </summary>
        public string CurrentLanguage { get; set; }
        
        /// <summary>
        /// 根元素引用
        /// </summary>
        public VisualElement RootElement { get; set; }
        
        /// <summary>
        /// 自定义属性
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> Properties { get; set; } 
            = new System.Collections.Generic.Dictionary<string, object>();
        
        /// <summary>
        /// 是否为批量处理模式
        /// </summary>
        public bool IsBatchMode { get; set; }
        
        /// <summary>
        /// 性能统计信息
        /// </summary>
        public LocalizationPerformanceStats Stats { get; set; }
    }
    
    /// <summary>
    /// 本地化性能统计
    /// </summary>
    public class LocalizationPerformanceStats
    {
        public int ProcessedElementsCount { get; set; }
        public long ProcessingTimeMs { get; set; }
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
    }
} 