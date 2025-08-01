using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;

namespace com.MiAO.MCP.Editor.Common
{
    /// <summary>
    /// Lightweight localization manager with no external dependencies
    /// Uses Unity's built-in JSON utilities for clean, simple multi-language support
    /// </summary>
    public static class LocalizationManager
    {
        #region Public Types

        public enum Language
        {
            English,
            ChineseSimplified
        }

        #endregion

        #region Private Fields

        private static Language _currentLanguage = Language.English;
        private static bool _isInitialized = false;
        private static Dictionary<string, string> _currentTranslations = new Dictionary<string, string>();
        
        private static readonly Dictionary<Language, string> _languageCodes = new Dictionary<Language, string>
        {
            [Language.English] = "en",
            [Language.ChineseSimplified] = "zh-CN"
        };

        private static readonly Dictionary<Language, CultureInfo> _cultureMappings = new Dictionary<Language, CultureInfo>
        {
            [Language.English] = new CultureInfo("en-US"),
            [Language.ChineseSimplified] = new CultureInfo("zh-CN")
        };

        private static readonly Dictionary<Language, string> _resourceFilePaths = new Dictionary<Language, string>
        {
            [Language.English] = "Assets/MiAO-MCP-for-Unity/Editor/Scripts/Common/Localization/Language/en.json",
            [Language.ChineseSimplified] = "Assets/MiAO-MCP-for-Unity/Editor/Scripts/Common/Localization/Language/zh-CN.json"
        };

        #endregion

        #region Public Properties
        
        /// <summary>
        /// Current language setting
        /// </summary>
        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LoadResourceFile();
                    OnLanguageChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// Current culture info
        /// </summary>
        public static CultureInfo CurrentCulture => _cultureMappings[_currentLanguage];

        /// <summary>
        /// Check if the localization system is initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Get count of loaded translations
        /// </summary>
        public static int TranslationCount => _currentTranslations.Count;

        #endregion

        #region Public Events

        /// <summary>
        /// Language change event
        /// </summary>
        public static event Action<Language> OnLanguageChanged;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize the localization system
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // Load saved language preference
                LoadLanguagePreference();

                // Load resource file
                LoadResourceFile();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                // Fallback to English if initialization fails
                _currentLanguage = Language.English;
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Get localized text by key
        /// </summary>
        /// <param name="key">Localization key (supports dot notation like "window.title")</param>
        /// <returns>Localized text or key if not found</returns>
        public static string GetText(string key)
        {
            if (!_isInitialized) Initialize();

            if (string.IsNullOrEmpty(key))
                return key;

            try
            {
                if (_currentTranslations.TryGetValue(key, out var text))
                {
                    return text;
                }

                // Track missing keys for debugging purposes
                if (!_missingKeys.Contains(key))
                {
                    _missingKeys.Add(key);
                    Debug.LogWarning($"[LocalizationManager] Missing translation for key: '{key}' in language: {_currentLanguage}");
                }
                
                return key;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalizationManager] Failed to get text for key '{key}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// Get localized text by key with formatting arguments
        /// </summary>
        /// <param name="key">Localization key</param>
        /// <param name="args">Format arguments</param>
        /// <returns>Formatted localized text</returns>
        public static string GetText(string key, params object[] args)
        {
            var text = GetText(key);
            
            if (args == null || args.Length == 0)
                return text;

            try
            {
                return string.Format(_cultureMappings[_currentLanguage], text, args);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalizationManager] Failed to format text for key '{key}': {ex.Message}");
                return text;
            }
        }

        /// <summary>
        /// Get localized text by nested key path
        /// </summary>
        /// <param name="category">Category (e.g., "window", "connector")</param>
        /// <param name="key">Specific key</param>
        /// <returns>Localized text</returns>
        public static string GetText(string category, string key)
        {
            return GetText($"{category}.{key}");
        }

        /// <summary>
        /// Get localized text by nested key path with formatting
        /// </summary>
        /// <param name="category">Category</param>
        /// <param name="key">Specific key</param>
        /// <param name="args">Format arguments</param>
        /// <returns>Formatted localized text</returns>
        public static string GetText(string category, string key, params object[] args)
        {
            return GetText($"{category}.{key}", args);
        }

        /// <summary>
        /// Check if a localization key exists
        /// </summary>
        /// <param name="key">Localization key</param>
        /// <returns>True if key exists</returns>
        public static bool HasKey(string key)
        {
            if (!_isInitialized) Initialize();
            return !string.IsNullOrEmpty(key) && _currentTranslations.ContainsKey(key);
        }

        /// <summary>
        /// Add or update a translation at runtime
        /// </summary>
        /// <param name="key">Translation key</param>
        /// <param name="value">Translation value</param>
        public static void SetText(string key, string value)
        {
            if (!_isInitialized) Initialize();
            
            if (!string.IsNullOrEmpty(key))
            {
                _currentTranslations[key] = value ?? "";
            }
        }

        /// <summary>
        /// Remove a translation
        /// </summary>
        /// <param name="key">Translation key to remove</param>
        /// <returns>True if key was removed</returns>
        public static bool RemoveText(string key)
        {
            if (!_isInitialized) Initialize();
            return !string.IsNullOrEmpty(key) && _currentTranslations.Remove(key);
        }

        /// <summary>
        /// Get all translation keys for current language
        /// </summary>
        /// <returns>Array of all keys</returns>
        public static string[] GetAllKeys()
        {
            if (!_isInitialized) Initialize();
            
            var keys = new string[_currentTranslations.Count];
            _currentTranslations.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>
        /// Convert language enum to string
        /// </summary>
        /// <param name="language">Language enum</param>
        /// <returns>Language string</returns>
        public static string LanguageToString(Language language)
        {
            return language.ToString();
        }

        /// <summary>
        /// Convert string to language enum
        /// </summary>
        /// <param name="languageString">Language string</param>
        /// <returns>Language enum</returns>
        public static Language StringToLanguage(string languageString)
        {
            if (string.IsNullOrEmpty(languageString))
                return Language.English;

            // Handle display names first
            return languageString switch
            {
                "简体中文" => Language.ChineseSimplified,
                "English" => Language.English,
                "ChineseSimplified" => Language.ChineseSimplified,
                _ => Enum.TryParse<Language>(languageString, true, out var language) ? language : Language.English
            };
        }

        /// <summary>
        /// Get display name for language
        /// </summary>
        /// <param name="language">Language enum</param>
        /// <returns>Display name</returns>
        public static string LanguageToDisplayString(Language language)
        {
            return language switch
            {
                Language.English => "English",
                Language.ChineseSimplified => "简体中文",
                _ => language.ToString()
            };
        }

        /// <summary>
        /// Get all available languages
        /// </summary>
        /// <returns>Array of available languages</returns>
        public static Language[] GetAvailableLanguages()
        {
            return (Language[])Enum.GetValues(typeof(Language));
        }

        /// <summary>
        /// Reload localization resources
        /// </summary>
        public static void Reload()
        {
            LoadResourceFile();
        }

        /// <summary>
        /// Clear all cached translations
        /// </summary>
        public static void Clear()
        {
            _currentTranslations.Clear();
            _missingKeys.Clear();
        }



        #endregion

        #region Private Fields for Optimization

        private static readonly HashSet<string> _missingKeys = new HashSet<string>();

        #endregion

        #region Private Methods

        private static void LoadResourceFile()
        {
            try
            {
                if (!_resourceFilePaths.TryGetValue(_currentLanguage, out var resourcePath))
                {
                    Debug.LogError($"[LocalizationManager] Resource path not found for language: {_currentLanguage}");
                    return;
                }

                // Use Unity's package path resolution instead of Path.GetFullPath
                var fullPath = resourcePath;
                if (!File.Exists(fullPath))
                {
                    // Try alternative path resolution methods
                    var altPath = Path.Combine(Application.dataPath, "..", resourcePath);
                    if (File.Exists(altPath))
                    {
                        fullPath = altPath;
                    }
                    else
                    {
                        Debug.LogError($"[LocalizationManager] Resource file not found: {resourcePath}");
                        Debug.LogError($"[LocalizationManager] Also tried: {altPath}");
                        return;
                    }
                }

                // Load and parse JSON file
                var jsonContent = File.ReadAllText(fullPath);
                
                // Clear current translations and missing keys
                _currentTranslations.Clear();
                _missingKeys.Clear();
                
                // Parse using Unity's built-in JSON support
                ParseJsonRecursive(jsonContent, "", _currentTranslations);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationManager] Failed to load resource file: {ex.Message}");
            }
        }

        private static void ParseJsonRecursive(string jsonContent, string prefix, Dictionary<string, string> result)
        {
            try
            {
                // Use a simple JSON parser that works with Unity's JsonUtility concepts
                var jsonDict = SimpleJsonParser.Parse(jsonContent);
                FlattenDictionary(jsonDict, prefix, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationManager] Failed to parse JSON: {ex.Message}");
            }
        }

        private static void FlattenDictionary(Dictionary<string, object> dict, string prefix, Dictionary<string, string> result)
        {
            foreach (var kvp in dict)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                
                if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    // Recursively flatten nested objects
                    FlattenDictionary(nestedDict, key, result);
                }
                else
                {
                    // Store the value as string
                    result[key] = kvp.Value?.ToString() ?? "";
                }
            }
        }

        private static void LoadLanguagePreference()
        {
            // Use the same preference key as UI settings to ensure consistency
            var savedLanguage = UnityEditor.EditorPrefs.GetString("MCP.Settings.Language", Language.English.ToString());
            _currentLanguage = StringToLanguage(savedLanguage);
        }

        private static void SaveLanguagePreference()
        {
            // Use the same preference key as UI settings to ensure consistency
            UnityEditor.EditorPrefs.SetString("MCP.Settings.Language", _currentLanguage.ToString());
        }

        #endregion

        #region Static Constructor

        static LocalizationManager()
        {
            // Auto-initialize when class is first accessed
            Initialize();
            
            // Save language preference when changed
            OnLanguageChanged += (language) => SaveLanguagePreference();
        }

        #endregion

        
    }

    #region Simple JSON Parser

    /// <summary>
    /// Lightweight JSON parser for localization needs
    /// No external dependencies, works with Unity's string processing
    /// </summary>
    internal static class SimpleJsonParser
    {
        public static Dictionary<string, object> Parse(string json)
        {
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                throw new ArgumentException("Invalid JSON format");

            var result = new Dictionary<string, object>();
            var content = json.Substring(1, json.Length - 2).Trim();
            
            ParseObject(content, result);
            return result;
        }

        private static void ParseObject(string content, Dictionary<string, object> result)
        {
            var i = 0;
            while (i < content.Length)
            {
                // Skip whitespace
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
                if (i >= content.Length) break;

                // Parse key
                if (content[i] != '"') throw new ArgumentException("Expected quoted key");
                i++; // Skip opening quote
                var keyStart = i;
                while (i < content.Length && content[i] != '"') i++;
                if (i >= content.Length) throw new ArgumentException("Unterminated key");
                var key = content.Substring(keyStart, i - keyStart);
                i++; // Skip closing quote

                // Skip whitespace and colon
                while (i < content.Length && (char.IsWhiteSpace(content[i]) || content[i] == ':')) i++;
                if (i >= content.Length) break;

                // Parse value
                object value;
                if (content[i] == '"')
                {
                    // String value
                    i++; // Skip opening quote
                    var valueStart = i;
                    while (i < content.Length && content[i] != '"')
                    {
                        if (content[i] == '\\') i++; // Skip escaped character
                        i++;
                    }
                    if (i >= content.Length) throw new ArgumentException("Unterminated string value");
                    value = content.Substring(valueStart, i - valueStart).Replace("\\n", "\n").Replace("\\\"", "\"");
                    i++; // Skip closing quote
                }
                else if (content[i] == '{')
                {
                    // Nested object
                    var braceCount = 1;
                    var objStart = i + 1;
                    i++; // Skip opening brace
                    while (i < content.Length && braceCount > 0)
                    {
                        if (content[i] == '{') braceCount++;
                        else if (content[i] == '}') braceCount--;
                        i++;
                    }
                    if (braceCount > 0) throw new ArgumentException("Unterminated object");
                    var objContent = content.Substring(objStart, i - objStart - 1);
                    var nestedResult = new Dictionary<string, object>();
                    ParseObject(objContent, nestedResult);
                    value = nestedResult;
                }
                else
                {
                    throw new ArgumentException($"Unexpected character: {content[i]}");
                }

                result[key] = value;

                // Skip whitespace and comma
                while (i < content.Length && (char.IsWhiteSpace(content[i]) || content[i] == ',')) i++;
            }
        }
    }

    #endregion
} 