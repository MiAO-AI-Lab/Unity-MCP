using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace com.MiAO.MCP.Editor.Localization.Providers
{
    /// <summary>
    /// Code-based localization configuration provider
    /// Supports programmatic localization configuration management
    /// </summary>
    public class CodeConfigProvider : ILocalizationConfigProvider
    {
        private static readonly Dictionary<string, LocalizationConfig> _nameBasedConfigs = new Dictionary<string, LocalizationConfig>();
        private static readonly Dictionary<Type, Dictionary<string, LocalizationConfig>> _typeBasedConfigs = new Dictionary<Type, Dictionary<string, LocalizationConfig>>();
        private static readonly List<Func<VisualElement, LocalizationConfig>> _dynamicConfigProviders = new List<Func<VisualElement, LocalizationConfig>>();
        
        public LocalizationConfig GetConfig(VisualElement element)
        {
            if (element == null) return null;
            
            // Priority 1: Name-based configuration
            if (!string.IsNullOrEmpty(element.name) && _nameBasedConfigs.TryGetValue(element.name, out var nameConfig))
            {
                return nameConfig;
            }
            
            // Priority 2: Type-based configuration
            var elementType = element.GetType();
            if (_typeBasedConfigs.TryGetValue(elementType, out var typeConfigs))
            {
                // Check type+name combination configuration
                if (!string.IsNullOrEmpty(element.name) && typeConfigs.TryGetValue(element.name, out var typeNameConfig))
                {
                    return typeNameConfig;
                }
                
                // Check default configuration for type
                if (typeConfigs.TryGetValue("*", out var defaultTypeConfig))
                {
                    return defaultTypeConfig;
                }
            }
            
            // Priority 3: Dynamic configuration providers
            foreach (var provider in _dynamicConfigProviders)
            {
                var config = provider(element);
                if (config != null && config.IsValid())
                {
                    return config;
                }
            }
            
            return null;
        }
        
        #region Static Configuration Management
        
        /// <summary>
        /// Register localization configuration for element with specified name
        /// </summary>
        /// <param name="elementName">Element name</param>
        /// <param name="config">Localization configuration</param>
        public static void RegisterConfig(string elementName, LocalizationConfig config)
        {
            if (string.IsNullOrEmpty(elementName) || config == null)
                return;
                
            _nameBasedConfigs[elementName] = config;
        }
        
        /// <summary>
        /// Register simple text localization for element with specified name
        /// </summary>
        /// <param name="elementName">Element name</param>
        /// <param name="textKey">Text localization key</param>
        public static void RegisterTextConfig(string elementName, string textKey)
        {
            RegisterConfig(elementName, new LocalizationConfig { TextKey = textKey });
        }
        
        /// <summary>
        /// Register localization configuration for element of specified type
        /// </summary>
        /// <param name="elementType">Element type</param>
        /// <param name="elementName">Element name, use "*" for default type configuration</param>
        /// <param name="config">Localization configuration</param>
        public static void RegisterTypeConfig(Type elementType, string elementName, LocalizationConfig config)
        {
            if (elementType == null || config == null)
                return;
                
            if (!_typeBasedConfigs.ContainsKey(elementType))
                _typeBasedConfigs[elementType] = new Dictionary<string, LocalizationConfig>();
                
            _typeBasedConfigs[elementType][elementName ?? "*"] = config;
            UnityEngine.Debug.Log($"[CodeConfigProvider] Registered type config for {elementType.Name}:{elementName}");
        }
        
        /// <summary>
        /// Register default localization configuration for element of specified type
        /// </summary>
        /// <typeparam name="T">Element type</typeparam>
        /// <param name="config">Localization configuration</param>
        public static void RegisterDefaultConfig<T>(LocalizationConfig config) where T : VisualElement
        {
            RegisterTypeConfig(typeof(T), "*", config);
        }
        
        /// <summary>
        /// Register dynamic configuration provider
        /// </summary>
        /// <param name="provider">Dynamic configuration provider function</param>
        public static void RegisterDynamicProvider(Func<VisualElement, LocalizationConfig> provider)
        {
            if (provider == null) return;
            
            _dynamicConfigProviders.Add(provider);
            UnityEngine.Debug.Log("[CodeConfigProvider] Registered dynamic provider");
        }
        
        /// <summary>
        /// Remove configuration for element with specified name
        /// </summary>
        /// <param name="elementName">Element name</param>
        /// <returns>Whether removal was successful</returns>
        public static bool UnregisterConfig(string elementName)
        {
            return !string.IsNullOrEmpty(elementName) && _nameBasedConfigs.Remove(elementName);
        }
        
        /// <summary>
        /// Remove configuration for element of specified type
        /// </summary>
        /// <param name="elementType">Element type</param>
        /// <param name="elementName">Element name</param>
        /// <returns>Whether removal was successful</returns>
        public static bool UnregisterTypeConfig(Type elementType, string elementName)
        {
            if (elementType == null) return false;
            
            if (_typeBasedConfigs.TryGetValue(elementType, out var typeConfigs))
            {
                return typeConfigs.Remove(elementName ?? "*");
            }
            
            return false;
        }
        
        /// <summary>
        /// Clear all configurations
        /// </summary>
        public static void ClearAllConfigs()
        {
            _nameBasedConfigs.Clear();
            _typeBasedConfigs.Clear();
            _dynamicConfigProviders.Clear();
            UnityEngine.Debug.Log("[CodeConfigProvider] All configurations cleared");
        }
        
        /// <summary>
        /// Get configuration statistics
        /// </summary>
        /// <returns>Configuration statistics</returns>
        public static ConfigStats GetStats()
        {
            var totalTypeConfigs = 0;
            foreach (var typeConfig in _typeBasedConfigs.Values)
            {
                totalTypeConfigs += typeConfig.Count;
            }
            
            return new ConfigStats
            {
                NameBasedConfigs = _nameBasedConfigs.Count,
                TypeBasedConfigs = totalTypeConfigs,
                DynamicProviders = _dynamicConfigProviders.Count
            };
        }
        
        #endregion
    }
    
    /// <summary>
    /// Configuration statistics information
    /// </summary>
    public class ConfigStats
    {
        public int NameBasedConfigs { get; set; }
        public int TypeBasedConfigs { get; set; }
        public int DynamicProviders { get; set; }
        
        public override string ToString()
        {
            return $"NameBased: {NameBasedConfigs}, TypeBased: {TypeBasedConfigs}, Dynamic: {DynamicProviders}";
        }
    }
    
    /// <summary>
    /// Extension methods for code configuration provider
    /// </summary>
    public static class CodeConfigProviderExtensions
    {
        /// <summary>
        /// Quickly register text localization for element
        /// </summary>
        /// <param name="elementName">Element name</param>
        /// <param name="textKey">Text localization key</param>
        public static void RegisterText(string elementName, string textKey)
        {
            CodeConfigProvider.RegisterTextConfig(elementName, textKey);
        }
        
        /// <summary>
        /// Quickly register complete localization configuration for element
        /// </summary>
        /// <param name="elementName">Element name</param>
        /// <param name="textKey">Text key</param>
        /// <param name="tooltipKey">Tooltip key</param>
        /// <param name="labelKey">Label key</param>
        public static void RegisterFullConfig(string elementName, string textKey = null, string tooltipKey = null, string labelKey = null)
        {
            var config = new LocalizationConfig
            {
                TextKey = textKey,
                TooltipKey = tooltipKey,
                LabelKey = labelKey
            };
            
            CodeConfigProvider.RegisterConfig(elementName, config);
        }
        
        /// <summary>
        /// Register configurations in batch
        /// </summary>
        /// <param name="configs">Configuration dictionary</param>
        public static void RegisterBatch(Dictionary<string, string> configs)
        {
            foreach (var kvp in configs)
            {
                CodeConfigProvider.RegisterTextConfig(kvp.Key, kvp.Value);
            }
        }
    }
} 