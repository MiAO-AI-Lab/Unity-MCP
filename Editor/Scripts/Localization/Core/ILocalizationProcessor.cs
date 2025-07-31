using UnityEngine.UIElements;

namespace com.MiAO.MCP.Editor.Localization
{
    /// <summary>
    /// Localization processor interface
    /// Defines how to handle localization of different types of UI elements
    /// </summary>
    public interface ILocalizationProcessor
    {
        /// <summary>
        /// Processor priority, higher values have higher priority
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Check if the specified UI element can be processed
        /// </summary>
        /// <param name="element">UI element to check</param>
        /// <returns>Returns true if it can be processed</returns>
        bool CanProcess(VisualElement element);
        
        /// <summary>
        /// Process UI element localization
        /// </summary>
        /// <param name="element">UI element to process</param>
        /// <param name="config">Localization configuration</param>
        /// <param name="context">Localization context</param>
        void Process(VisualElement element, LocalizationConfig config, LocalizationContext context);
    }
    
    /// <summary>
    /// Localization context, provides additional information needed during processing
    /// </summary>
    public class LocalizationContext
    {
        /// <summary>
        /// Current language
        /// </summary>
        public string CurrentLanguage { get; set; }
        
        /// <summary>
        /// Root element reference
        /// </summary>
        public VisualElement RootElement { get; set; }
        
        /// <summary>
        /// Custom properties
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> Properties { get; set; } 
            = new System.Collections.Generic.Dictionary<string, object>();
        
        /// <summary>
        /// Whether in batch processing mode
        /// </summary>
        public bool IsBatchMode { get; set; }
        
        /// <summary>
        /// Performance statistics information
        /// </summary>
        public LocalizationPerformanceStats Stats { get; set; }
    }
    
    /// <summary>
    /// Localization performance statistics
    /// </summary>
    public class LocalizationPerformanceStats
    {
        public int ProcessedElementsCount { get; set; }
        public long ProcessingTimeMs { get; set; }
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
    }
} 