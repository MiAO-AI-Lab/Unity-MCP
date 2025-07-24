using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor.Localization
{
    /// <summary>
    /// UI本地化系统核心管理器
    /// 提供可扩展的本地化架构
    /// </summary>
    public static class UILocalizationSystem
    {
        #region Private Fields
        
        private static readonly List<ILocalizationProcessor> _processors = new List<ILocalizationProcessor>();
        private static readonly List<ILocalizationConfigProvider> _configProviders = new List<ILocalizationConfigProvider>();
        private static readonly Dictionary<string, string> _textCache = new Dictionary<string, string>();
        private static readonly Dictionary<VisualElement, LocalizationConfig> _elementConfigCache = new Dictionary<VisualElement, LocalizationConfig>();
        
        private static bool _isInitialized = false;
        private static LocalizationPerformanceStats _globalStats = new LocalizationPerformanceStats();
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// 全局性能统计
        /// </summary>
        public static LocalizationPerformanceStats GlobalStats => _globalStats;
        
        /// <summary>
        /// 注册的处理器数量
        /// </summary>
        public static int ProcessorCount => _processors.Count;
        
        /// <summary>
        /// 注册的配置提供者数量
        /// </summary>
        public static int ConfigProviderCount => _configProviders.Count;
        
        #endregion
        
        #region Public Events
        
        /// <summary>
        /// 本地化处理开始事件
        /// </summary>
        public static event Action<VisualElement, LocalizationContext> OnLocalizationStarted;
        
        /// <summary>
        /// 本地化处理完成事件
        /// </summary>
        public static event Action<VisualElement, LocalizationContext> OnLocalizationCompleted;
        
        /// <summary>
        /// 本地化错误事件
        /// </summary>
        public static event Action<Exception, VisualElement> OnLocalizationError;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// 初始化UI本地化系统
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                UnityEngine.Debug.Log("[UILocalizationSystem] Initializing...");
                
                // 清理旧数据
                _processors.Clear();
                _configProviders.Clear();
                _textCache.Clear();
                _elementConfigCache.Clear();
                
                // 注册默认处理器
                RegisterDefaultProcessors();
                
                // 注册默认配置提供者
                RegisterDefaultConfigProviders();
                
                // 订阅语言变化事件
                LocalizationManager.OnLanguageChanged += OnLanguageChanged;
                
                _isInitialized = true;
                UnityEngine.Debug.Log($"[UILocalizationSystem] Initialized with {_processors.Count} processors and {_configProviders.Count} config providers");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UILocalizationSystem] Failed to initialize: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 关闭UI本地化系统
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized) return;
            
            try
            {
                // 取消订阅事件
                LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
                
                // 清理缓存
                ClearAllCaches();
                
                // 清理处理器和提供者
                _processors.Clear();
                _configProviders.Clear();
                
                _isInitialized = false;
                UnityEngine.Debug.Log("[UILocalizationSystem] Shutdown completed");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UILocalizationSystem] Error during shutdown: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Processor Management
        
        /// <summary>
        /// 注册本地化处理器
        /// </summary>
        /// <typeparam name="T">处理器类型</typeparam>
        public static void RegisterProcessor<T>() where T : ILocalizationProcessor, new()
        {
            RegisterProcessor(new T());
        }
        
        /// <summary>
        /// 注册本地化处理器
        /// </summary>
        /// <param name="processor">处理器实例</param>
        public static void RegisterProcessor(ILocalizationProcessor processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            
            // 避免重复注册
            if (_processors.Any(p => p.GetType() == processor.GetType()))
            {
                UnityEngine.Debug.LogWarning($"[UILocalizationSystem] Processor {processor.GetType().Name} already registered");
                return;
            }
            
            _processors.Add(processor);
            
            // 按优先级排序
            _processors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            
            UnityEngine.Debug.Log($"[UILocalizationSystem] Registered processor: {processor.GetType().Name} (Priority: {processor.Priority})");
        }
        
        /// <summary>
        /// 移除本地化处理器
        /// </summary>
        /// <typeparam name="T">处理器类型</typeparam>
        /// <returns>是否成功移除</returns>
        public static bool UnregisterProcessor<T>() where T : ILocalizationProcessor
        {
            var processor = _processors.FirstOrDefault(p => p is T);
            if (processor != null)
            {
                _processors.Remove(processor);
                UnityEngine.Debug.Log($"[UILocalizationSystem] Unregistered processor: {typeof(T).Name}");
                return true;
            }
            return false;
        }
        
        #endregion
        
        #region Config Provider Management
        
        /// <summary>
        /// 注册配置提供者
        /// </summary>
        /// <typeparam name="T">配置提供者类型</typeparam>
        public static void RegisterConfigProvider<T>() where T : ILocalizationConfigProvider, new()
        {
            RegisterConfigProvider(new T());
        }
        
        /// <summary>
        /// 注册配置提供者
        /// </summary>
        /// <param name="provider">配置提供者实例</param>
        public static void RegisterConfigProvider(ILocalizationConfigProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            
            if (_configProviders.Any(p => p.GetType() == provider.GetType()))
            {
                UnityEngine.Debug.LogWarning($"[UILocalizationSystem] Config provider {provider.GetType().Name} already registered");
                return;
            }
            
            _configProviders.Add(provider);
            UnityEngine.Debug.Log($"[UILocalizationSystem] Registered config provider: {provider.GetType().Name}");
        }
        
        #endregion
        
        #region Localization Processing
        
        /// <summary>
        /// 本地化单个UI元素
        /// </summary>
        /// <param name="element">要本地化的UI元素</param>
        /// <param name="context">本地化上下文</param>
        public static void LocalizeElement(VisualElement element, LocalizationContext context = null)
        {
            if (!_isInitialized) Initialize();
            if (element == null) return;
            
            context ??= CreateDefaultContext(element);
            
            try
            {
                OnLocalizationStarted?.Invoke(element, context);
                
                var config = GetLocalizationConfig(element);
                if (config == null || !config.IsValid())
                    return;
                
                ApplyLocalization(element, config, context);
                context.Stats.ProcessedElementsCount++;
                
                OnLocalizationCompleted?.Invoke(element, context);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UILocalizationSystem] Error localizing element {element.name}: {ex.Message}");
                OnLocalizationError?.Invoke(ex, element);
            }
        }
        
        /// <summary>
        /// 本地化整个UI树
        /// </summary>
        /// <param name="root">根UI元素</param>
        /// <param name="context">本地化上下文</param>
        public static void LocalizeElementTree(VisualElement root, LocalizationContext context = null)
        {
            if (!_isInitialized) Initialize();
            if (root == null) return;
            
            context ??= CreateDefaultContext(root);
            context.IsBatchMode = true;
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                OnLocalizationStarted?.Invoke(root, context);
                
                // 收集所有需要本地化的元素
                var elementsToLocalize = new List<(VisualElement element, LocalizationConfig config)>();
                
                root.Query().ForEach(element =>
                {
                    var config = GetLocalizationConfig(element);
                    if (config != null && config.IsValid())
                    {
                        elementsToLocalize.Add((element, config));
                    }
                });
                
                // 批量获取本地化文本
                var allKeys = elementsToLocalize.SelectMany(x => x.config.GetAllKeys()).Distinct().ToList();
                var textCache = GetTextBatch(allKeys);
                
                // 应用本地化
                foreach (var (element, config) in elementsToLocalize)
                {
                    ApplyLocalizationFromCache(element, config, context, textCache);
                    context.Stats.ProcessedElementsCount++;
                }
                
                stopwatch.Stop();
                context.Stats.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                OnLocalizationCompleted?.Invoke(root, context);
                
                UnityEngine.Debug.Log($"[UILocalizationSystem] Localized {context.Stats.ProcessedElementsCount} elements in {context.Stats.ProcessingTimeMs}ms");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UILocalizationSystem] Error localizing element tree: {ex.Message}");
                OnLocalizationError?.Invoke(ex, root);
            }
        }
        
        #endregion
        
        #region Cache Management
        
        /// <summary>
        /// 清理所有缓存
        /// </summary>
        public static void ClearAllCaches()
        {
            _textCache.Clear();
            _elementConfigCache.Clear();
            UnityEngine.Debug.Log("[UILocalizationSystem] All caches cleared");
        }
        
        /// <summary>
        /// 清理文本缓存
        /// </summary>
        public static void ClearTextCache()
        {
            _textCache.Clear();
            UnityEngine.Debug.Log("[UILocalizationSystem] Text cache cleared");
        }
        
        /// <summary>
        /// 清理配置缓存
        /// </summary>
        public static void ClearConfigCache()
        {
            _elementConfigCache.Clear();
            UnityEngine.Debug.Log("[UILocalizationSystem] Config cache cleared");
        }
        
        #endregion
        
        #region Private Methods
        
        private static void RegisterDefaultProcessors()
        {
            // 注册所有默认UI元素处理器
            RegisterProcessor<Processors.LabelLocalizationProcessor>();
            RegisterProcessor<Processors.ButtonLocalizationProcessor>();
            RegisterProcessor<Processors.TextFieldLocalizationProcessor>();
            RegisterProcessor<Processors.FoldoutLocalizationProcessor>();
            RegisterProcessor<Processors.DropdownFieldLocalizationProcessor>();
            RegisterProcessor<Processors.ToggleLocalizationProcessor>();
            
            UnityEngine.Debug.Log("[UILocalizationSystem] Registered default processors");
        }
        
        private static void RegisterDefaultConfigProviders()
        {
            // 注册默认配置提供者（按优先级顺序）
            RegisterConfigProvider<Providers.AttributeConfigProvider>();
            RegisterConfigProvider<Providers.CodeConfigProvider>();
            
            UnityEngine.Debug.Log("[UILocalizationSystem] Registered default config providers");
        }
        
        private static void OnLanguageChanged(LocalizationManager.Language newLanguage)
        {
            UnityEngine.Debug.Log($"[UILocalizationSystem] Language changed to {newLanguage}, clearing all caches");
            // 清理所有缓存，确保语言切换后重新处理所有元素
            ClearAllCaches();
        }
        
        private static LocalizationContext CreateDefaultContext(VisualElement rootElement)
        {
            return new LocalizationContext
            {
                CurrentLanguage = LocalizationManager.CurrentLanguage.ToString(),
                RootElement = rootElement,
                Stats = new LocalizationPerformanceStats()
            };
        }
        
        private static LocalizationConfig GetLocalizationConfig(VisualElement element)
        {
            // 尝试从缓存获取
            if (_elementConfigCache.TryGetValue(element, out var cachedConfig))
            {
                return cachedConfig;
            }
            
            // 从配置提供者获取
            foreach (var provider in _configProviders)
            {
                var config = provider.GetConfig(element);
                if (config != null && config.IsValid())
                {
                    _elementConfigCache[element] = config;
                    return config;
                }
            }
            
            return null;
        }
        
        private static void ApplyLocalization(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var processor in _processors)
            {
                if (processor.CanProcess(element))
                {
                    processor.Process(element, config, context);
                    return;
                }
            }
            
            UnityEngine.Debug.LogWarning($"[UILocalizationSystem] No processor found for element type: {element.GetType().Name}");
        }
        
        private static void ApplyLocalizationFromCache(VisualElement element, LocalizationConfig config, LocalizationContext context, Dictionary<string, string> textCache)
        {
            foreach (var processor in _processors)
            {
                if (processor.CanProcess(element))
                {
                    processor.Process(element, config, context);
                    return;
                }
            }
        }
        
        private static Dictionary<string, string> GetTextBatch(List<string> keys)
        {
            var result = new Dictionary<string, string>();
            
            foreach (var key in keys)
            {
                if (_textCache.TryGetValue(key, out var cachedText))
                {
                    result[key] = cachedText;
                    _globalStats.CacheHitCount++;
                }
                else
                {
                    var text = LocalizationManager.GetText(key);
                    _textCache[key] = text;
                    result[key] = text;
                    _globalStats.CacheMissCount++;
                }
            }
            
            return result;
        }
        
        #endregion
    }
    
    /// <summary>
    /// 本地化配置提供者接口
    /// </summary>
    public interface ILocalizationConfigProvider
    {
        /// <summary>
        /// 获取UI元素的本地化配置
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <returns>本地化配置，如果没有返回null</returns>
        LocalizationConfig GetConfig(VisualElement element);
    }
} 