using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;
using com.MiAO.MCP.Editor.Common;

namespace com.MiAO.MCP.Editor.Localization
{
    /// <summary>
    /// UI localization system core manager
    /// Provides extensible localization architecture
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
        /// Whether the system is initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Global performance statistics
        /// </summary>
        public static LocalizationPerformanceStats GlobalStats => _globalStats;
        
        /// <summary>
        /// Number of registered processors
        /// </summary>
        public static int ProcessorCount => _processors.Count;
        
        /// <summary>
        /// Number of registered configuration providers
        /// </summary>
        public static int ConfigProviderCount => _configProviders.Count;
        
        #endregion
        
        #region Public Events
        
        /// <summary>
        /// Localization processing started event
        /// </summary>
        public static event Action<VisualElement, LocalizationContext> OnLocalizationStarted;
        
        /// <summary>
        /// Localization processing completed event
        /// </summary>
        public static event Action<VisualElement, LocalizationContext> OnLocalizationCompleted;
        
        /// <summary>
        /// Localization error event
        /// </summary>
        public static event Action<Exception, VisualElement> OnLocalizationError;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize UI localization system
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                UnityEngine.Debug.Log("[UILocalizationSystem] Initializing...");
                
                // Clear old data
                _processors.Clear();
                _configProviders.Clear();
                _textCache.Clear();
                _elementConfigCache.Clear();
                
                // Register default processors
                RegisterDefaultProcessors();
                
                // Register default configuration providers
                RegisterDefaultConfigProviders();
                
                // Subscribe to language change events
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
        /// Shutdown UI localization system
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized) return;
            
            try
            {
                // Unsubscribe from events
                LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
                
                // Clear caches
                ClearAllCaches();
                
                // Clear processors and providers
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
        /// Register localization processor
        /// </summary>
        /// <typeparam name="T">Processor type</typeparam>
        public static void RegisterProcessor<T>() where T : ILocalizationProcessor, new()
        {
            RegisterProcessor(new T());
        }
        
        /// <summary>
        /// Register localization processor
        /// </summary>
        /// <param name="processor">Processor instance</param>
        public static void RegisterProcessor(ILocalizationProcessor processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            
            // Avoid duplicate registration
            if (_processors.Any(p => p.GetType() == processor.GetType()))
            {
                UnityEngine.Debug.LogWarning($"[UILocalizationSystem] Processor {processor.GetType().Name} already registered");
                return;
            }
            
            _processors.Add(processor);
            
            // Sort by priority
            _processors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            
            // UnityEngine.Debug.Log($"[UILocalizationSystem] Registered processor: {processor.GetType().Name} (Priority: {processor.Priority})");
        }
        
        /// <summary>
        /// Remove localization processor
        /// </summary>
        /// <typeparam name="T">Processor type</typeparam>
        /// <returns>Whether removal was successful</returns>
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
        /// Register configuration provider
        /// </summary>
        /// <typeparam name="T">Configuration provider type</typeparam>
        public static void RegisterConfigProvider<T>() where T : ILocalizationConfigProvider, new()
        {
            RegisterConfigProvider(new T());
        }
        
        /// <summary>
        /// Register configuration provider
        /// </summary>
        /// <param name="provider">Configuration provider instance</param>
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
        /// Localize single UI element
        /// </summary>
        /// <param name="element">UI element to localize</param>
        /// <param name="context">Localization context</param>
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
        /// Localize entire UI tree
        /// </summary>
        /// <param name="root">Root UI element</param>
        /// <param name="context">Localization context</param>
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
                
                // Collect all elements that need localization
                var elementsToLocalize = new List<(VisualElement element, LocalizationConfig config)>();
                
                root.Query().ForEach(element =>
                {
                    var config = GetLocalizationConfig(element);
                    if (config != null && config.IsValid())
                    {
                        elementsToLocalize.Add((element, config));
                    }
                });
                
                // Batch get localized text
                var allKeys = elementsToLocalize.SelectMany(x => x.config.GetAllKeys()).Distinct().ToList();
                var textCache = GetTextBatch(allKeys);
                
                // Apply localization
                foreach (var (element, config) in elementsToLocalize)
                {
                    ApplyLocalizationFromCache(element, config, context, textCache);
                    context.Stats.ProcessedElementsCount++;
                }
                
                stopwatch.Stop();
                context.Stats.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                OnLocalizationCompleted?.Invoke(root, context);
                
                // UnityEngine.Debug.Log($"[UILocalizationSystem] Localized {context.Stats.ProcessedElementsCount} elements in {context.Stats.ProcessingTimeMs}ms");
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
        /// Clear all caches
        /// </summary>
        public static void ClearAllCaches()
        {
            _textCache.Clear();
            _elementConfigCache.Clear();
        }
        
        /// <summary>
        /// Clear text cache
        /// </summary>
        public static void ClearTextCache()
        {
            _textCache.Clear();
            UnityEngine.Debug.Log("[UILocalizationSystem] Text cache cleared");
        }
        
        /// <summary>
        /// Clear configuration cache
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
            // Register all default UI element processors
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
            // Register default configuration providers (in priority order)
            RegisterConfigProvider<Providers.AttributeConfigProvider>();
            RegisterConfigProvider<Providers.CodeConfigProvider>();
            
            UnityEngine.Debug.Log("[UILocalizationSystem] Registered default config providers");
        }
        
        private static void OnLanguageChanged(LocalizationManager.Language newLanguage)
        {
            UnityEngine.Debug.Log($"[UILocalizationSystem] Language changed to {newLanguage}, clearing all caches");
            // Clear all caches to ensure all elements are reprocessed after language switch
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
            // Try to get from cache
            if (_elementConfigCache.TryGetValue(element, out var cachedConfig))
            {
                return cachedConfig;
            }
            
            // Get from configuration providers
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
    /// Localization configuration provider interface
    /// </summary>
    public interface ILocalizationConfigProvider
    {
        /// <summary>
        /// Get localization configuration for UI element
        /// </summary>
        /// <param name="element">UI element</param>
        /// <returns>Localization configuration, return null if none exists</returns>
        LocalizationConfig GetConfig(VisualElement element);
    }
} 