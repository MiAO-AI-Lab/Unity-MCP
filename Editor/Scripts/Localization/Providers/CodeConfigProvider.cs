using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace com.MiAO.Unity.MCP.Editor.Localization.Providers
{
    /// <summary>
    /// 基于代码的本地化配置提供者
    /// 支持程序化的本地化配置管理
    /// </summary>
    public class CodeConfigProvider : ILocalizationConfigProvider
    {
        private static readonly Dictionary<string, LocalizationConfig> _nameBasedConfigs = new Dictionary<string, LocalizationConfig>();
        private static readonly Dictionary<Type, Dictionary<string, LocalizationConfig>> _typeBasedConfigs = new Dictionary<Type, Dictionary<string, LocalizationConfig>>();
        private static readonly List<Func<VisualElement, LocalizationConfig>> _dynamicConfigProviders = new List<Func<VisualElement, LocalizationConfig>>();
        
        public LocalizationConfig GetConfig(VisualElement element)
        {
            if (element == null) return null;
            
            // 优先级1: 基于元素名称的配置
            if (!string.IsNullOrEmpty(element.name) && _nameBasedConfigs.TryGetValue(element.name, out var nameConfig))
            {
                return nameConfig;
            }
            
            // 优先级2: 基于元素类型的配置
            var elementType = element.GetType();
            if (_typeBasedConfigs.TryGetValue(elementType, out var typeConfigs))
            {
                // 检查类型+名称的组合配置
                if (!string.IsNullOrEmpty(element.name) && typeConfigs.TryGetValue(element.name, out var typeNameConfig))
                {
                    return typeNameConfig;
                }
                
                // 检查类型的默认配置
                if (typeConfigs.TryGetValue("*", out var defaultTypeConfig))
                {
                    return defaultTypeConfig;
                }
            }
            
            // 优先级3: 动态配置提供者
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
        /// 为指定名称的元素注册本地化配置
        /// </summary>
        /// <param name="elementName">元素名称</param>
        /// <param name="config">本地化配置</param>
        public static void RegisterConfig(string elementName, LocalizationConfig config)
        {
            if (string.IsNullOrEmpty(elementName) || config == null)
                return;
                
            _nameBasedConfigs[elementName] = config;
            UnityEngine.Debug.Log($"[CodeConfigProvider] Registered config for element: {elementName}");
        }
        
        /// <summary>
        /// 为指定名称的元素注册简单文本本地化
        /// </summary>
        /// <param name="elementName">元素名称</param>
        /// <param name="textKey">文本本地化键</param>
        public static void RegisterTextConfig(string elementName, string textKey)
        {
            RegisterConfig(elementName, new LocalizationConfig { TextKey = textKey });
        }
        
        /// <summary>
        /// 为指定类型的元素注册本地化配置
        /// </summary>
        /// <param name="elementType">元素类型</param>
        /// <param name="elementName">元素名称，使用"*"表示类型默认配置</param>
        /// <param name="config">本地化配置</param>
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
        /// 为指定类型的元素注册默认本地化配置
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="config">本地化配置</param>
        public static void RegisterDefaultConfig<T>(LocalizationConfig config) where T : VisualElement
        {
            RegisterTypeConfig(typeof(T), "*", config);
        }
        
        /// <summary>
        /// 注册动态配置提供者
        /// </summary>
        /// <param name="provider">动态配置提供者函数</param>
        public static void RegisterDynamicProvider(Func<VisualElement, LocalizationConfig> provider)
        {
            if (provider == null) return;
            
            _dynamicConfigProviders.Add(provider);
            UnityEngine.Debug.Log("[CodeConfigProvider] Registered dynamic provider");
        }
        
        /// <summary>
        /// 移除指定名称的元素配置
        /// </summary>
        /// <param name="elementName">元素名称</param>
        /// <returns>是否成功移除</returns>
        public static bool UnregisterConfig(string elementName)
        {
            return !string.IsNullOrEmpty(elementName) && _nameBasedConfigs.Remove(elementName);
        }
        
        /// <summary>
        /// 移除指定类型的元素配置
        /// </summary>
        /// <param name="elementType">元素类型</param>
        /// <param name="elementName">元素名称</param>
        /// <returns>是否成功移除</returns>
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
        /// 清除所有配置
        /// </summary>
        public static void ClearAllConfigs()
        {
            _nameBasedConfigs.Clear();
            _typeBasedConfigs.Clear();
            _dynamicConfigProviders.Clear();
            UnityEngine.Debug.Log("[CodeConfigProvider] All configurations cleared");
        }
        
        /// <summary>
        /// 获取配置统计信息
        /// </summary>
        /// <returns>配置统计</returns>
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
    /// 配置统计信息
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
    /// 代码配置提供者的扩展方法
    /// </summary>
    public static class CodeConfigProviderExtensions
    {
        /// <summary>
        /// 快速注册元素的文本本地化
        /// </summary>
        /// <param name="elementName">元素名称</param>
        /// <param name="textKey">文本本地化键</param>
        public static void RegisterText(string elementName, string textKey)
        {
            CodeConfigProvider.RegisterTextConfig(elementName, textKey);
        }
        
        /// <summary>
        /// 快速注册元素的完整本地化配置
        /// </summary>
        /// <param name="elementName">元素名称</param>
        /// <param name="textKey">文本键</param>
        /// <param name="tooltipKey">工具提示键</param>
        /// <param name="labelKey">标签键</param>
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
        /// 批量注册配置
        /// </summary>
        /// <param name="configs">配置字典</param>
        public static void RegisterBatch(Dictionary<string, string> configs)
        {
            foreach (var kvp in configs)
            {
                CodeConfigProvider.RegisterTextConfig(kvp.Key, kvp.Value);
            }
        }
    }
} 