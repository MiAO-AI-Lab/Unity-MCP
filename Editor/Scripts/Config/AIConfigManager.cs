using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.MiAO.MCP.Editor
{
    /// <summary>
    /// AI Configuration Manager using EditorPrefs for persistent storage.
    /// This follows Unity's best practices for editor tool configuration.
    /// </summary>
    public static class AIConfigManager
    {
        // EditorPrefs key prefix for AI configuration
        private const string PREFS_PREFIX = "com.miao.unity.mcp.ai.";
        
        // Configuration keys
        private const string OPENAI_API_KEY = PREFS_PREFIX + "openaiApiKey";
        private const string OPENAI_MODEL = PREFS_PREFIX + "openaiModel";
        private const string OPENAI_BASE_URL = PREFS_PREFIX + "openaiBaseUrl";
        
        private const string GEMINI_API_KEY = PREFS_PREFIX + "geminiApiKey";
        private const string GEMINI_MODEL = PREFS_PREFIX + "geminiModel";
        private const string GEMINI_BASE_URL = PREFS_PREFIX + "geminiBaseUrl";
        
        private const string CLAUDE_API_KEY = PREFS_PREFIX + "claudeApiKey";
        private const string CLAUDE_MODEL = PREFS_PREFIX + "claudeModel";
        private const string CLAUDE_BASE_URL = PREFS_PREFIX + "claudeBaseUrl";
        
        private const string LOCAL_API_URL = PREFS_PREFIX + "localApiUrl";
        private const string LOCAL_MODEL = PREFS_PREFIX + "localModel";
        
        private const string VISION_MODEL_PROVIDER = PREFS_PREFIX + "visionModelProvider";
        private const string TEXT_MODEL_PROVIDER = PREFS_PREFIX + "textModelProvider";
        private const string CODE_MODEL_PROVIDER = PREFS_PREFIX + "codeModelProvider";
        
        private const string TIMEOUT_SECONDS = PREFS_PREFIX + "timeoutSeconds";
        private const string MAX_TOKENS = PREFS_PREFIX + "maxTokens";
        
        // No longer needed - removed migration/initialization logic
        
        /// <summary>
        /// Load AI configuration from EditorPrefs with default values
        /// </summary>
        public static AIConfigData LoadConfig()
        {
            return new AIConfigData
            {
                openaiApiKey = EditorPrefs.GetString(OPENAI_API_KEY, ""),
                openaiModel = EditorPrefs.GetString(OPENAI_MODEL, "gpt-4o"),
                openaiBaseUrl = EditorPrefs.GetString(OPENAI_BASE_URL, "https://api.openai.com/v1/chat/completions"),
                
                geminiApiKey = EditorPrefs.GetString(GEMINI_API_KEY, ""),
                geminiModel = EditorPrefs.GetString(GEMINI_MODEL, "gemini-pro"),
                geminiBaseUrl = EditorPrefs.GetString(GEMINI_BASE_URL, "https://generativelanguage.googleapis.com/v1/models"),
                
                claudeApiKey = EditorPrefs.GetString(CLAUDE_API_KEY, ""),
                claudeModel = EditorPrefs.GetString(CLAUDE_MODEL, "claude-3-sonnet-20240229"),
                claudeBaseUrl = EditorPrefs.GetString(CLAUDE_BASE_URL, "https://api.anthropic.com/v1/messages"),
                
                localApiUrl = EditorPrefs.GetString(LOCAL_API_URL, "http://localhost:11434/api/generate"),
                localModel = EditorPrefs.GetString(LOCAL_MODEL, "llava"),
                
                visionModelProvider = EditorPrefs.GetString(VISION_MODEL_PROVIDER, "openai"),
                textModelProvider = EditorPrefs.GetString(TEXT_MODEL_PROVIDER, "openai"),
                codeModelProvider = EditorPrefs.GetString(CODE_MODEL_PROVIDER, "claude"),
                
                timeoutSeconds = EditorPrefs.GetInt(TIMEOUT_SECONDS, 30),
                maxTokens = EditorPrefs.GetInt(MAX_TOKENS, 1000)
            };
        }
        
        /// <summary>
        /// Save AI configuration to EditorPrefs
        /// </summary>
        public static void SaveConfig(AIConfigData config)
        {
            EditorPrefs.SetString(OPENAI_API_KEY, config.openaiApiKey ?? "");
            EditorPrefs.SetString(OPENAI_MODEL, config.openaiModel ?? "gpt-4o");
            EditorPrefs.SetString(OPENAI_BASE_URL, config.openaiBaseUrl ?? "https://api.openai.com/v1/chat/completions");
            
            EditorPrefs.SetString(GEMINI_API_KEY, config.geminiApiKey ?? "");
            EditorPrefs.SetString(GEMINI_MODEL, config.geminiModel ?? "gemini-pro");
            EditorPrefs.SetString(GEMINI_BASE_URL, config.geminiBaseUrl ?? "https://generativelanguage.googleapis.com/v1/models");
            
            EditorPrefs.SetString(CLAUDE_API_KEY, config.claudeApiKey ?? "");
            EditorPrefs.SetString(CLAUDE_MODEL, config.claudeModel ?? "claude-3-sonnet-20240229");
            EditorPrefs.SetString(CLAUDE_BASE_URL, config.claudeBaseUrl ?? "https://api.anthropic.com/v1/messages");
            
            EditorPrefs.SetString(LOCAL_API_URL, config.localApiUrl ?? "http://localhost:11434/api/generate");
            EditorPrefs.SetString(LOCAL_MODEL, config.localModel ?? "llava");
            
            EditorPrefs.SetString(VISION_MODEL_PROVIDER, config.visionModelProvider ?? "openai");
            EditorPrefs.SetString(TEXT_MODEL_PROVIDER, config.textModelProvider ?? "openai");
            EditorPrefs.SetString(CODE_MODEL_PROVIDER, config.codeModelProvider ?? "claude");
            
            EditorPrefs.SetInt(TIMEOUT_SECONDS, config.timeoutSeconds);
            EditorPrefs.SetInt(MAX_TOKENS, config.maxTokens);
        }
        
        /// <summary>
        /// Reset all AI configuration settings to defaults
        /// </summary>
        public static void ResetToDefaults()
        {
            var defaultConfig = new AIConfigData
            {
                openaiApiKey = "",
                openaiModel = "gpt-4o",
                openaiBaseUrl = "https://api.openai.com/v1/chat/completions",
                
                geminiApiKey = "",
                geminiModel = "gemini-pro",
                geminiBaseUrl = "https://generativelanguage.googleapis.com/v1/models",
                
                claudeApiKey = "",
                claudeModel = "claude-3-sonnet-20240229",
                claudeBaseUrl = "https://api.anthropic.com/v1/messages",
                
                localApiUrl = "http://localhost:11434/api/generate",
                localModel = "llava",
                
                visionModelProvider = "openai",
                textModelProvider = "openai",
                codeModelProvider = "claude",
                
                timeoutSeconds = 30,
                maxTokens = 1000
            };
            
            SaveConfig(defaultConfig);
            Debug.Log("[AI Config Manager] Configuration reset to defaults.");
        }
        
        /// <summary>
        /// Clear all AI configuration settings from EditorPrefs
        /// </summary>
        public static void ClearAllSettings()
        {
            EditorPrefs.DeleteKey(OPENAI_API_KEY);
            EditorPrefs.DeleteKey(OPENAI_MODEL);
            EditorPrefs.DeleteKey(OPENAI_BASE_URL);
            
            EditorPrefs.DeleteKey(GEMINI_API_KEY);
            EditorPrefs.DeleteKey(GEMINI_MODEL);
            EditorPrefs.DeleteKey(GEMINI_BASE_URL);
            
            EditorPrefs.DeleteKey(CLAUDE_API_KEY);
            EditorPrefs.DeleteKey(CLAUDE_MODEL);
            EditorPrefs.DeleteKey(CLAUDE_BASE_URL);
            
            EditorPrefs.DeleteKey(LOCAL_API_URL);
            EditorPrefs.DeleteKey(LOCAL_MODEL);
            
            EditorPrefs.DeleteKey(VISION_MODEL_PROVIDER);
            EditorPrefs.DeleteKey(TEXT_MODEL_PROVIDER);
            EditorPrefs.DeleteKey(CODE_MODEL_PROVIDER);
            
            EditorPrefs.DeleteKey(TIMEOUT_SECONDS);
            EditorPrefs.DeleteKey(MAX_TOKENS);
            
            Debug.Log("[AI Config Manager] All configuration settings cleared.");
        }
        
        /// <summary>
        /// Export current configuration to JSON file for backup or sharing
        /// </summary>
        public static void ExportToJson(string filePath)
        {
            var config = LoadConfig();
            var jsonText = JsonUtility.ToJson(config, true);
            File.WriteAllText(filePath, jsonText);
            Debug.Log($"[AI Config Manager] Configuration exported to: {filePath}");
        }
        
        /// <summary>
        /// Import configuration from JSON file
        /// </summary>
        public static void ImportFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[AI Config Manager] Import file not found: {filePath}");
                return;
            }
            
            try
            {
                var configText = File.ReadAllText(filePath);
                var config = JsonUtility.FromJson<AIConfigData>(configText);
                
                if (config != null)
                {
                    SaveConfig(config);
                    Debug.Log($"[AI Config Manager] Configuration imported from: {filePath}");
                }
                else
                {
                    Debug.LogError($"[AI Config Manager] Failed to parse configuration file: {filePath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AI Config Manager] Failed to import configuration: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// AI Configuration data structure - moved here from MainWindowEditor
    /// </summary>
    [System.Serializable]
    public class AIConfigData
    {
        public string openaiApiKey;
        public string openaiModel;
        public string openaiBaseUrl;
        public string geminiApiKey;
        public string geminiModel;
        public string geminiBaseUrl;
        public string claudeApiKey;
        public string claudeModel;
        public string claudeBaseUrl;
        public string localApiUrl;
        public string localModel;
        public string visionModelProvider;
        public string textModelProvider;
        public string codeModelProvider;
        public int timeoutSeconds;
        public int maxTokens;
    }
}