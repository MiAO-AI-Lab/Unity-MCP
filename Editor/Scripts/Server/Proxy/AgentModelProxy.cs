#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.IO;
using com.IvanMurzak.Unity.MCP.Server.Protocol;

namespace com.IvanMurzak.Unity.MCP.Server.Proxy
{
    /// <summary>
    /// Agent model proxy implementation - Direct model API calls (referencing AI.ImageRecognition.cs approach)
    /// </summary>
    public class AgentModelProxy : IAgentModelProxy
    {
        private readonly AIConfig _config;
        private readonly HttpClient _httpClient;

        public AgentModelProxy()
        {
            _config = LoadConfig();
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.timeoutSeconds);
        }

        /// <summary>
        /// Send model request - Direct model API calls
        /// </summary>
        public async Task<AgentModelResponse> SendModelRequestAsync(AgentModelRequest request)
        {
            try
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.Log($"[AgentModelProxy] Processing {request.Type} model request");
#else
                Console.WriteLine($"[AgentModelProxy] Processing {request.Type} model request");
#endif

                return request.Type.ToLower() switch
                {
                    "vision" => await ProcessVisionRequestAsync(request),
                    "text" => await ProcessTextRequestAsync(request),
                    "code" => await ProcessCodeRequestAsync(request),
                    _ => AgentModelResponse.Error($"Unsupported model type: {request.Type}")
                };
            }
            catch (Exception ex)
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogError($"[AgentModelProxy] Error processing model request: {ex.Message}");
#else
                Console.WriteLine($"[AgentModelProxy] Error processing model request: {ex.Message}");
#endif
                return AgentModelResponse.Error($"Request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Process vision model request
        /// </summary>
        private async Task<AgentModelResponse> ProcessVisionRequestAsync(AgentModelRequest request)
        {
            if (string.IsNullOrEmpty(request.ImageData))
            {
                return AgentModelResponse.Error("Image data is required for vision model");
            }

            try
            {
                // Validate image data
                if (!IsValidBase64Image(request.ImageData))
                {
                    return AgentModelResponse.Error("Invalid image data format");
                }

                // Use unified API call
                var result = await CallUnifiedModelAPI(_config.visionModelProvider, ModelMode.Vision, request.Prompt, request.ImageData);
                return AgentModelResponse.Success(result);
            }
            catch (Exception ex)
            {
                return AgentModelResponse.Error($"Vision model request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Process text model request
        /// </summary>
        private async Task<AgentModelResponse> ProcessTextRequestAsync(AgentModelRequest request)
        {
            try
            {
                // Use unified API call
                var result = await CallUnifiedModelAPI(_config.textModelProvider, ModelMode.Text, request.Prompt);
                return AgentModelResponse.Success(result);
            }
            catch (Exception ex)
            {
                return AgentModelResponse.Error($"Text model request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Process code model request
        /// </summary>
        private async Task<AgentModelResponse> ProcessCodeRequestAsync(AgentModelRequest request)
        {
            try
            {
                // Add specific prompt prefix for code requests
                var codePrompt = BuildCodePrompt(request.Prompt, request.CodeContext);
                
                // Use unified API call
                var result = await CallUnifiedModelAPI(_config.codeModelProvider, ModelMode.Text, codePrompt);
                return AgentModelResponse.Success(result);
            }
            catch (Exception ex)
            {
                return AgentModelResponse.Error($"Code model request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Model mode enumeration
        /// </summary>
        private enum ModelMode
        {
            Text,
            Vision
        }

        /// <summary>
        /// Unified model API call function
        /// </summary>
        private async Task<string> CallUnifiedModelAPI(string provider, ModelMode mode, string prompt, string? imageData = null)
        {
            provider = provider.ToLower();
            
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.Log($"[AgentModelProxy] Calling {provider} {mode} API");
#else
            Console.WriteLine($"[AgentModelProxy] Calling {provider} {mode} API");
#endif

            return provider switch
            {
                "openai" => await CallOpenAIAPI(mode, prompt, imageData),
                "gemini" => await CallGeminiAPI(mode, prompt, imageData),
                "claude" => await CallClaudeAPI(mode, prompt, imageData),
                "local" => await CallLocalAPI(mode, prompt, imageData),
                _ => throw new Exception($"Unsupported model provider: {provider}")
            };
        }

        /// <summary>
        /// OpenAI unified API call
        /// </summary>
        private async Task<string> CallOpenAIAPI(ModelMode mode, string prompt, string? imageData = null)
        {
            if (string.IsNullOrEmpty(_config.openaiApiKey))
            {
                throw new Exception("OpenAI API key not configured");
            }

            object requestBody;
            
            if (mode == ModelMode.Vision && !string.IsNullOrEmpty(imageData))
            {
                var cleanBase64 = CleanBase64ImageData(imageData);
                requestBody = new
                {
                    model = _config.openaiModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new { type = "image_url", image_url = new { url = $"data:image/png;base64,{cleanBase64}" } }
                            }
                        }
                    },
                    max_tokens = _config.maxTokens,
                    temperature = 0.7
                };
            }
            else
            {
                requestBody = new
                {
                    model = _config.openaiModel,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = _config.maxTokens,
                    temperature = 0.7
                };
            }

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = false });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.openaiApiKey}");

            var response = await _httpClient.PostAsync(_config.openaiBaseUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseText);
                return jsonResponse?.choices?[0]?.message?.content ?? "No response content";
            }
            else
            {
                throw new Exception($"OpenAI API call failed: {response.StatusCode} - {responseText}");
            }
        }

        /// <summary>
        /// Gemini unified API call
        /// </summary>
        private async Task<string> CallGeminiAPI(ModelMode mode, string prompt, string? imageData = null)
        {
            if (string.IsNullOrEmpty(_config.geminiApiKey))
            {
                throw new Exception("Gemini API key not configured");
            }

            object requestBody;
            
            if (mode == ModelMode.Vision && !string.IsNullOrEmpty(imageData))
            {
                var cleanBase64 = CleanBase64ImageData(imageData);
                requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt },
                                new { 
                                    inline_data = new { 
                                        mime_type = "image/png", 
                                        data = cleanBase64 
                                    } 
                                }
                            }
                        }
                    }
                };
            }
            else
            {
                requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };
            }

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            var url = $"{_config.geminiBaseUrl}/{_config.geminiModel}:generateContent?key={_config.geminiApiKey}";
            
            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<GeminiResponse>(responseText);
                return jsonResponse?.candidates?[0]?.content?.parts?[0]?.text ?? "No response content";
            }
            else
            {
                throw new Exception($"Gemini API call failed: {response.StatusCode} - {responseText}");
            }
        }

        /// <summary>
        /// Claude unified API call
        /// </summary>
        private async Task<string> CallClaudeAPI(ModelMode mode, string prompt, string? imageData = null)
        {
            if (string.IsNullOrEmpty(_config.claudeApiKey))
            {
                throw new Exception("Claude API key not configured");
            }

            object requestBody;
            
            if (mode == ModelMode.Vision && !string.IsNullOrEmpty(imageData))
            {
                var cleanBase64 = CleanBase64ImageData(imageData);
                requestBody = new
                {
                    model = _config.claudeModel,
                    max_tokens = _config.maxTokens,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new { 
                                    type = "image", 
                                    source = new { 
                                        type = "base64", 
                                        media_type = "image/png", 
                                        data = cleanBase64 
                                    } 
                                }
                            }
                        }
                    }
                };
            }
            else
            {
                requestBody = new
                {
                    model = _config.claudeModel,
                    max_tokens = _config.maxTokens,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };
            }

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.claudeApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            
            var response = await _httpClient.PostAsync(_config.claudeBaseUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseText);
                return jsonResponse?.content?[0]?.text ?? "No response content";
            }
            else
            {
                throw new Exception($"Claude API call failed: {response.StatusCode} - {responseText}");
            }
        }

        /// <summary>
        /// Local API unified call
        /// </summary>
        private async Task<string> CallLocalAPI(ModelMode mode, string prompt, string? imageData = null)
        {
            object requestBody;
            
            if (mode == ModelMode.Vision && !string.IsNullOrEmpty(imageData))
            {
                requestBody = new
                {
                    model = _config.localModel,
                    prompt = prompt,
                    images = new[] { imageData },
                    stream = false
                };
            }
            else
            {
                requestBody = new
                {
                    model = _config.localModel,
                    prompt = prompt,
                    stream = false
                };
            }

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_config.localApiUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<LocalResponse>(responseText);
                return jsonResponse?.response ?? "No response content";
            }
            else
            {
                throw new Exception($"Local API call failed: {response.StatusCode} - {responseText}");
            }
        }

        /// <summary>
        /// Build code analysis prompt
        /// </summary>
        private string BuildCodePrompt(string userPrompt, string? codeContext = null)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are an expert code assistant and software engineer.");
            promptBuilder.AppendLine("Please provide accurate, well-explained, and practical code solutions.");
            
            if (!string.IsNullOrEmpty(codeContext))
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Code Context:");
                promptBuilder.AppendLine(codeContext);
                promptBuilder.AppendLine();
            }
            
            promptBuilder.AppendLine("User Request:");
            promptBuilder.AppendLine(userPrompt);
            
            return promptBuilder.ToString();
        }

        /// <summary>
        /// Validate if Base64 image data is valid
        /// </summary>
        private bool IsValidBase64Image(string base64Data)
        {
            try
            {
                // Remove data URL prefix (if exists)
                if (base64Data.StartsWith("data:image/"))
                {
                    var base64Index = base64Data.IndexOf("base64,");
                    if (base64Index != -1)
                    {
                        base64Data = base64Data.Substring(base64Index + 7);
                    }
                }

                // Try to decode Base64
                var imageBytes = Convert.FromBase64String(base64Data);
                
                // Basic size check
                if (imageBytes.Length < 100) // Too small to be a valid image
                {
                    return false;
                }

                // Check magic numbers for common image formats
                return IsValidImageFormat(imageBytes);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check image format magic numbers
        /// </summary>
        private bool IsValidImageFormat(byte[] imageBytes)
        {
            if (imageBytes.Length < 4) return false;

            // PNG: 89 50 4E 47
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return true;
            
            // JPEG: FF D8 FF
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
                return true;
            
            // GIF: 47 49 46 38
            if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x38)
                return true;
            
            // BMP: 42 4D
            if (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D)
                return true;

            return false;
        }

        /// <summary>
        /// Clean Base64 image data
        /// </summary>
        private string CleanBase64ImageData(string base64Data)
        {
            // Remove data URL prefix (if exists)
            if (base64Data.StartsWith("data:image/"))
            {
                var base64Index = base64Data.IndexOf("base64,");
                if (base64Index != -1)
                {
                    base64Data = base64Data.Substring(base64Index + 7);
                }
            }

            // Remove possible whitespace characters
            return base64Data.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
        }

        /// <summary>
        /// Check connection status
        /// </summary>
        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                // Simple check if configuration is valid
                return !string.IsNullOrEmpty(_config.openaiApiKey) && !string.IsNullOrEmpty(_config.openaiBaseUrl);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get model status information
        /// </summary>
        public async Task<string> GetModelStatusAsync()
        {
            try
            {
                var isConnected = await IsConnectedAsync();
                var status = new StringBuilder();
                status.AppendLine("=== AgentModelProxy Status ===");
                status.AppendLine($"Connected: {(isConnected ? "Yes" : "No")}");
                status.AppendLine();
                
                status.AppendLine("=== Model Providers ===");
                status.AppendLine($"Vision Model: {_config.visionModelProvider}");
                status.AppendLine($"Text Model: {_config.textModelProvider}");
                status.AppendLine($"Code Model: {_config.codeModelProvider}");
                status.AppendLine();
                
                status.AppendLine("=== OpenAI Configuration ===");
                status.AppendLine($"API Key: {(!string.IsNullOrEmpty(_config.openaiApiKey) ? "Configured" : "Not configured")}");
                status.AppendLine($"Model: {_config.openaiModel}");
                status.AppendLine($"Base URL: {_config.openaiBaseUrl}");
                status.AppendLine();
                
                status.AppendLine("=== Gemini Configuration ===");
                status.AppendLine($"API Key: {(!string.IsNullOrEmpty(_config.geminiApiKey) ? "Configured" : "Not configured")}");
                status.AppendLine($"Model: {_config.geminiModel}");
                status.AppendLine($"Base URL: {_config.geminiBaseUrl}");
                status.AppendLine();
                
                status.AppendLine("=== Claude Configuration ===");
                status.AppendLine($"API Key: {(!string.IsNullOrEmpty(_config.claudeApiKey) ? "Configured" : "Not configured")}");
                status.AppendLine($"Model: {_config.claudeModel}");
                status.AppendLine($"Base URL: {_config.claudeBaseUrl}");
                status.AppendLine();
                
                status.AppendLine("=== Local Configuration ===");
                status.AppendLine($"API URL: {_config.localApiUrl}");
                status.AppendLine($"Model: {_config.localModel}");
                status.AppendLine();
                
                status.AppendLine("=== General Settings ===");
                status.AppendLine($"Max Tokens: {_config.maxTokens}");
                status.AppendLine($"Timeout: {_config.timeoutSeconds}s");
                
                return status.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting model status: {ex.Message}";
            }
        }

        /// <summary>
        /// Format file size
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Load AI configuration (referencing AI.Config.cs)
        /// </summary>
        private static AIConfig LoadConfig()
        {
#if UNITY_5_3_OR_NEWER
            // Use Unity path in Unity environment
            var configPath = Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "com.IvanMurzak.Unity.MCP", "Config", "AI_Config.json");
#else
            // Use relative path in Server environment
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AI_Config.json");
#endif

            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AIConfig>(json);
                    return config ?? new AIConfig();
                }
                else
                {
#if UNITY_5_3_OR_NEWER
                    // Create default configuration in Unity environment
                    var config = new AIConfig();
                    SaveConfig(config, configPath);
                    return config;
#else
                    // Try to copy configuration file from Unity package in Server environment
                    return LoadConfigFromUnityPackage(configPath);
#endif
                }
            }
            catch (Exception ex)
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogError($"[AgentModelProxy] Failed to load config: {ex.Message}");
#else
                Console.WriteLine($"[AgentModelProxy] Failed to load config: {ex.Message}");
#endif
                return new AIConfig();
            }
        }

#if !UNITY_5_3_OR_NEWER
        /// <summary>
        /// Load configuration file from Unity package to Server environment
        /// </summary>
        private static AIConfig LoadConfigFromUnityPackage(string serverConfigPath)
        {
            try
            {
                // Build configuration file path in Unity package
                var unityConfigPath = GetUnityPackageConfigPath();
                
                if (File.Exists(unityConfigPath))
                {
                    Console.WriteLine($"[AgentModelProxy] Copying config from Unity package: {unityConfigPath}");
                    
                    // Create Server configuration directory
                    string serverConfigDir = Path.GetDirectoryName(serverConfigPath);
                    if (!Directory.Exists(serverConfigDir))
                        Directory.CreateDirectory(serverConfigDir);
                    
                    // Copy configuration file
                    File.Copy(unityConfigPath, serverConfigPath, true);
                    
                    // Read and return configuration
                    string json = File.ReadAllText(serverConfigPath);
                    var config = JsonSerializer.Deserialize<AIConfig>(json);
                    
                    Console.WriteLine($"[AgentModelProxy] Config copied and loaded successfully");
                    return config ?? new AIConfig();
                }
                else
                {
                    Console.WriteLine($"[AgentModelProxy] Unity package config not found: {unityConfigPath}");
                    // Create default configuration
                    var config = new AIConfig();
                    SaveConfig(config, serverConfigPath);
                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AgentModelProxy] Failed to load config from Unity package: {ex.Message}");
                // Create default configuration
                var config = new AIConfig();
                SaveConfig(config, serverConfigPath);
                return config;
            }
        }

        /// <summary>
        /// Get configuration file path in Unity package
        /// </summary>
        private static string GetUnityPackageConfigPath()
        {
            // Search upward from Server's bin directory to find Unity project root
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectRoot = FindUnityProjectRoot(currentDir);
            
            if (!string.IsNullOrEmpty(projectRoot))
            {
                return Path.Combine(projectRoot, "Packages", "com.IvanMurzak.Unity.MCP", "Config", "AI_Config.json");
            }
            
            // If project root not found, try using relative path
            return Path.Combine(currentDir, "..", "..", "..", "..", "..", "Packages", "com.IvanMurzak.Unity.MCP", "Config", "AI_Config.json");
        }

        /// <summary>
        /// Find Unity project root directory
        /// </summary>
        private static string FindUnityProjectRoot(string startPath)
        {
            var currentDir = new DirectoryInfo(startPath);
            
            while (currentDir != null)
            {
                // Check if Unity project identifier files exist
                if (File.Exists(Path.Combine(currentDir.FullName, "ProjectSettings", "ProjectVersion.txt")) ||
                    Directory.Exists(Path.Combine(currentDir.FullName, "Assets")) ||
                    Directory.Exists(Path.Combine(currentDir.FullName, "Packages")))
                {
                    return currentDir.FullName;
                }
                
                currentDir = currentDir.Parent;
            }
            
            return string.Empty;
        }
#endif

        /// <summary>
        /// Save configuration
        /// </summary>
        private static void SaveConfig(AIConfig config, string configPath)
        {
            try
            {
                string configDir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.Log($"[AgentModelProxy] Config saved to: {configPath}");
#else
                Console.WriteLine($"[AgentModelProxy] Config saved to: {configPath}");
#endif
            }
            catch (Exception ex)
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogError($"[AgentModelProxy] Failed to save config: {ex.Message}");
#else
                Console.WriteLine($"[AgentModelProxy] Failed to save config: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// Reload configuration file
        /// </summary>
        public static void ReloadConfig()
        {
            // Reset factory instance to force configuration reload
            AgentModelProxyFactory.ResetInstance();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// AI configuration class (referencing AI.Config.cs)
    /// </summary>
    public class AIConfig
    {
        public string openaiApiKey { get; set; } = "";
        public string openaiModel { get; set; } = "gpt-4o";
        public string openaiBaseUrl { get; set; } = "https://api.openai.com/v1/chat/completions";

        public string geminiApiKey { get; set; } = "";
        public string geminiModel { get; set; } = "gemini-pro";
        public string geminiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1/models";

        public string claudeApiKey { get; set; } = "";
        public string claudeModel { get; set; } = "claude-3-sonnet-20240229";
        public string claudeBaseUrl { get; set; } = "https://api.anthropic.com/v1/messages";

        public string localApiUrl { get; set; } = "http://localhost:11434/api/generate";
        public string localModel { get; set; } = "llava";
        
        // Model selection configuration
        public string visionModelProvider { get; set; } = "openai"; // openai, gemini, claude, local
        public string textModelProvider { get; set; } = "openai";   // openai, gemini, claude, local
        public string codeModelProvider { get; set; } = "claude";   // openai, gemini, claude, local
        
        public int timeoutSeconds { get; set; } = 30;
        public int maxTokens { get; set; } = 1000;
    }

    /// <summary>
    /// OpenAI response data structure (referencing AI.ImageRecognition.cs)
    /// </summary>
    public class OpenAIResponse
    {
        public OpenAIChoice[] choices { get; set; } = Array.Empty<OpenAIChoice>();
    }

    public class OpenAIChoice
    {
        public OpenAIMessage message { get; set; } = new();
    }

    public class OpenAIMessage
    {
        public string content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gemini response data structure
    /// </summary>
    public class GeminiResponse
    {
        public GeminiCandidate[] candidates { get; set; } = Array.Empty<GeminiCandidate>();
    }

    public class GeminiCandidate
    {
        public GeminiContent content { get; set; } = new();
    }

    public class GeminiContent
    {
        public GeminiPart[] parts { get; set; } = Array.Empty<GeminiPart>();
    }

    public class GeminiPart
    {
        public string text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Claude response data structure
    /// </summary>
    public class ClaudeResponse
    {
        public ClaudeContent[] content { get; set; } = Array.Empty<ClaudeContent>();
    }

    public class ClaudeContent
    {
        public string text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Local API response data structure
    /// </summary>
    public class LocalResponse
    {
        public string response { get; set; } = string.Empty;
    }

    /// <summary>
    /// Agent model proxy factory (Server internal implementation)
    /// </summary>
    public static class AgentModelProxyFactory
    {
        private static IAgentModelProxy? _instance;

        /// <summary>
        /// Get Agent model proxy instance
        /// </summary>
        public static IAgentModelProxy GetInstance()
        {
            if (_instance == null)
            {
                _instance = new AgentModelProxy();
            }

            return _instance;
        }

        /// <summary>
        /// Set custom instance (for testing)
        /// </summary>
        public static void SetInstance(IAgentModelProxy proxy)
        {
            _instance = proxy;
        }

        /// <summary>
        /// Reset instance (for testing or reconfiguration)
        /// </summary>
        public static void ResetInstance()
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}
