#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.IO;
using com.MiAO.Unity.MCP.Server.Protocol;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Server.Proxy
{
    /// <summary>
    /// Agent model proxy implementation - Direct model API calls (referencing AI.ImageRecognition.cs approach)
    /// </summary>
    public class AgentModelProxy : IAgentModelProxy
    {
        private AIConfig _config;
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
                _config = LoadConfig();

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
            var imageMessages = request.Messages.Where(m => m.Type == MessageType.Image).ToList();
            if (imageMessages.Count == 0)
            {
                return AgentModelResponse.Error("Image data is required for vision model");
            }

            // var textMessages = request.Messages.Where(m => m.Type == MessageType.Text).ToList();
            // var prompt = textMessages.Count > 0 ? string.Join(" ", textMessages.Select(m => m.Content)) : "Please analyze this image.";
            var imageData = imageMessages.Select(m => m.Content).ToList();

            try
            {
                // Validate all image data
                foreach (var image in imageData)
                {
                    if (!IsValidBase64Image(image))
                    {
                        return AgentModelResponse.Error("Invalid image data format");
                    }
                }

                // Use unified API call with multiple images
                var result = await CallUnifiedModelAPI(_config.visionModelProvider, ModelMode.Vision, request.Messages);
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
            var textMessages = request.Messages.Where(m => m.Type == MessageType.Text).ToList();
            if (textMessages.Count == 0)
            {
                return AgentModelResponse.Error("Text content is required for text model");
            }

            try
            {
                // Use unified API call
                var result = await CallUnifiedModelAPI(_config.textModelProvider, ModelMode.Text, request.Messages);
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
            var textMessages = request.Messages.Where(m => m.Type == MessageType.Text).ToList();
            var codeMessages = request.Messages.Where(m => m.Type == MessageType.Code).ToList();

            if (textMessages.Count == 0)
            {
                return AgentModelResponse.Error("Text prompt is required for code model");
            }

            var prompt = string.Join(" ", textMessages.Select(m => m.Content));
            var codeContext = codeMessages.Count > 0 ? string.Join("\n", codeMessages.Select(m => m.Content)) : null;

            try
            {
                // Add specific prompt prefix for code requests
                var codePrompt = BuildCodePrompt(prompt, codeContext);
                
                // Create message with code prompt
                var promptMessages = new List<Message> { new Message { Type = MessageType.Text, Content = codePrompt } };
                
                // Use unified API call
                var result = await CallUnifiedModelAPI(_config.codeModelProvider, ModelMode.Text, promptMessages);
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
        private async Task<string> CallUnifiedModelAPI(string provider, ModelMode mode, List<Message> messages)
        {
            provider = provider.ToLower();
            
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.Log($"[AgentModelProxy] Calling {provider} {mode} API with {messages?.Count ?? 0} messages");
#else
            Console.WriteLine($"[AgentModelProxy] Calling {provider} {mode} API with {messages?.Count ?? 0} messages");
#endif

            return provider switch
            {
                "openai" => await CallOpenAIAPI(mode, messages ?? new List<Message>()),
                "gemini" => await CallGeminiAPI(mode, messages ?? new List<Message>()),
                "claude" => await CallClaudeAPI(mode, messages ?? new List<Message>()),
                "local" => await CallLocalAPI(mode, messages ?? new List<Message>()),
                _ => throw new Exception($"Unsupported model provider: {provider}")
            };
        }

        /// <summary>
        /// OpenAI unified API call
        /// </summary>
        private async Task<string> CallOpenAIAPI(ModelMode mode, List<Message> messages)
        {
            if (string.IsNullOrEmpty(_config.openaiApiKey))
            {
                throw new Exception("OpenAI API key not configured");
            }

            object requestBody;
            
            if (mode == ModelMode.Vision)
            {
                var messageList = new List<object>();

                var currentRole = messages[0].Role;
                var preparedMessage = new
                {
                    role=currentRole.ToString().ToLower(),
                    content=new List<object>()
                };
                foreach (var message in messages)
                {
                    if (message.Role != currentRole)
                    {
                        messageList.Add(preparedMessage);
                        currentRole = message.Role;
                        preparedMessage = new
                        {
                            role=currentRole.ToString().ToLower(),
                            content=new List<object> { message.FormatContent() }
                        };
                    }
                    else
                    {
                        preparedMessage.content.Add(message.FormatContent());
                    }
                }
                messageList.Add(preparedMessage);

                requestBody = new
                {
                    model = _config.openaiModel,
                    messages = messageList,
                    max_tokens = _config.maxTokens,
                    temperature = 0.7
                };
            }
            else
            {
                var textContent = string.Join(" ", messages.Where(m => m.Type == MessageType.Text).Select(m => m.Content));
                requestBody = new
                {
                    model = _config.openaiModel,
                    messages = new[]
                    {
                        new { role = "user", content = textContent }
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
        private async Task<string> CallGeminiAPI(ModelMode mode, List<Message> messages)
        {
            if (string.IsNullOrEmpty(_config.geminiApiKey))
            {
                throw new Exception("Gemini API key not configured");
            }

            object requestBody;
            
            if (mode == ModelMode.Vision)
            {
                var contentsList = new List<object>();

                var currentRole = messages[0].Role;
                var preparedContent = new
                {
                    parts = new List<object>()
                };
                foreach (var message in messages)
                {
                    var formattedContent = message.Type == MessageType.Image 
                        ? FormatGeminiImageContent(message.Content)
                        : message.FormatGeminiContent();
                        
                    if (message.Role != currentRole)
                    {
                        contentsList.Add(preparedContent);
                        currentRole = message.Role;
                        preparedContent = new
                        {
                            parts = new List<object> { formattedContent }
                        };
                    }
                    else
                    {
                        preparedContent.parts.Add(formattedContent);
                    }
                }
                contentsList.Add(preparedContent);
                
                requestBody = new
                {
                    contents = contentsList
                };
            }
            else
            {
                var textContent = string.Join(" ", messages.Where(m => m.Type == MessageType.Text).Select(m => m.Content));
                requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = textContent } } }
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
        private async Task<string> CallClaudeAPI(ModelMode mode, List<Message> messages)
        {
            if (string.IsNullOrEmpty(_config.claudeApiKey))
            {
                throw new Exception("Claude API key not configured");
            }

            object requestBody;
            
            if (mode == ModelMode.Vision)
            {
                var messageList = new List<object>();

                var currentRole = messages[0].Role;
                var preparedMessage = new
                {
                    role = currentRole.ToString().ToLower(),
                    content = new List<object>()
                };
                foreach (var message in messages)
                {
                    var formattedContent = message.Type == MessageType.Image 
                        ? FormatClaudeImageContent(message.Content)
                        : message.FormatClaudeContent();
                        
                    if (message.Role != currentRole)
                    {
                        messageList.Add(preparedMessage);
                        currentRole = message.Role;
                        preparedMessage = new
                        {
                            role = currentRole.ToString().ToLower(),
                            content = new List<object> { formattedContent }
                        };
                    }
                    else
                    {
                        preparedMessage.content.Add(formattedContent);
                    }
                }
                messageList.Add(preparedMessage);
                
                requestBody = new
                {
                    model = _config.claudeModel,
                    max_tokens = _config.maxTokens,
                    messages = messageList
                };
            }
            else
            {
                var textContent = string.Join(" ", messages.Where(m => m.Type == MessageType.Text).Select(m => m.Content));
                requestBody = new
                {
                    model = _config.claudeModel,
                    max_tokens = _config.maxTokens,
                    messages = new[]
                    {
                        new { role = "user", content = textContent }
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
        private async Task<string> CallLocalAPI(ModelMode mode, List<Message> messages)
        {
            object requestBody;
            
            if (mode == ModelMode.Vision)
            {
                var messageList = new List<object>();

                var currentRole = messages[0].Role;
                var textContent = string.Join(" ", messages.Where(m => m.Type == MessageType.Text).Select(m => m.Content));
                var imageMessages = messages.Where(m => m.Type == MessageType.Image).Select(m => m.Content).ToArray();
                
                requestBody = new
                {
                    model = _config.localModel,
                    prompt = textContent,
                    images = imageMessages,
                    stream = false
                };
            }
            else
            {
                var textContent = string.Join(" ", messages.Where(m => m.Type == MessageType.Text).Select(m => m.Content));
                requestBody = new
                {
                    model = _config.localModel,
                    prompt = textContent,
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
        /// Format Gemini image content
        /// </summary>
        private object FormatGeminiImageContent(string imageContent)
        {
            var cleanBase64 = CleanBase64ImageData(imageContent);
            return new { 
                inline_data = new { 
                    mime_type = "image/png", 
                    data = cleanBase64 
                } 
            };
        }

        /// <summary>
        /// Format Claude image content
        /// </summary>
        private object FormatClaudeImageContent(string imageContent)
        {
            var cleanBase64 = CleanBase64ImageData(imageContent);
            return new { 
                type = "image", 
                source = new { 
                    type = "base64", 
                    media_type = "image/png", 
                    data = cleanBase64 
                } 
            };
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
        /// Check connection status for all configured model providers
        /// </summary>
        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                // Check if at least one model provider is properly configured and accessible
                var providers = new[] { _config.visionModelProvider, _config.textModelProvider, _config.codeModelProvider };
                var uniqueProviders = providers.Distinct().ToArray();

                foreach (var provider in uniqueProviders)
                {
                    if (await IsProviderConnectedAsync(provider.ToLower()))
                    {
                        return true; // At least one provider is working
                    }
                }

                return false; // No providers are working
            }
            catch (Exception ex)
            {
#if UNITY_5_3_OR_NEWER
                UnityEngine.Debug.LogError($"[AgentModelProxy] Error checking connection: {ex.Message}");
#else
                Console.WriteLine($"[AgentModelProxy] Error checking connection: {ex.Message}");
#endif
                return false;
            }
        }

        /// <summary>
        /// Check if a specific model provider is properly configured and accessible
        /// </summary>
        private async Task<bool> IsProviderConnectedAsync(string provider)
        {
            try
            {
                switch (provider)
                {
                    case "openai":
                        if (string.IsNullOrEmpty(_config.openaiApiKey) || string.IsNullOrEmpty(_config.openaiBaseUrl))
                            return false;
                        // Test with a simple request
                        return await TestOpenAIConnection();

                    case "gemini":
                        if (string.IsNullOrEmpty(_config.geminiApiKey) || string.IsNullOrEmpty(_config.geminiBaseUrl))
                            return false;
                        return await TestGeminiConnection();

                    case "claude":
                        if (string.IsNullOrEmpty(_config.claudeApiKey) || string.IsNullOrEmpty(_config.claudeBaseUrl))
                            return false;
                        return await TestClaudeConnection();

                    case "local":
                        if (string.IsNullOrEmpty(_config.localApiUrl))
                            return false;
                        return await TestLocalConnection();

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Test OpenAI connection with a simple request
        /// </summary>
        private async Task<bool> TestOpenAIConnection()
        {
            try
            {
                var requestBody = new
                {
                    model = _config.openaiModel,
                    messages = new[] { new { role = "user", content = "Hi" } },
                    max_tokens = 1
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.openaiApiKey}");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.PostAsync(_config.openaiBaseUrl, content, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Test Gemini connection
        /// </summary>
        private async Task<bool> TestGeminiConnection()
        {
            try
            {
                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = "Hi" } } } }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                var url = $"{_config.geminiBaseUrl}/{_config.geminiModel}:generateContent?key={_config.geminiApiKey}";

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.PostAsync(url, content, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Test Claude connection
        /// </summary>
        private async Task<bool> TestClaudeConnection()
        {
            try
            {
                var requestBody = new
                {
                    model = _config.claudeModel,
                    max_tokens = 1,
                    messages = new[] { new { role = "user", content = "Hi" } }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.claudeApiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.PostAsync(_config.claudeBaseUrl, content, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Test local API connection
        /// </summary>
        private async Task<bool> TestLocalConnection()
        {
            try
            {
                var requestBody = new
                {
                    model = _config.localModel,
                    prompt = "Hi",
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.PostAsync(_config.localApiUrl, content, cts.Token);
                return response.IsSuccessStatusCode;
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
        /// Load AI configuration
        /// </summary>
        private static AIConfig LoadConfig()
        {
#if UNITY_5_3_OR_NEWER
            try
            {
                // In Unity environment, load from EditorPrefs
                return LoadConfigFromEditorPrefs();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AgentModelProxy] Failed to load from EditorPrefs, using defaults: {ex.Message}");
                // Use built-in defaults
                return new AIConfig();
            }
#else
            // In Server environment, use built-in defaults
            return new AIConfig();
#endif
        }

#if UNITY_5_3_OR_NEWER
        /// <summary>
        /// Load configuration directly from EditorPrefs (Unity environment)
        /// </summary>
        private static AIConfig LoadConfigFromEditorPrefs()
        {
            const string PREFS_PREFIX = "com.miao.unity.mcp.ai.";
            
            return new AIConfig
            {
                openaiApiKey = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "openaiApiKey", ""),
                openaiModel = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "openaiModel", "gpt-4o"),
                openaiBaseUrl = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "openaiBaseUrl", "https://api.openai.com/v1/chat/completions"),
                
                geminiApiKey = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "geminiApiKey", ""),
                geminiModel = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "geminiModel", "gemini-pro"),
                geminiBaseUrl = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "geminiBaseUrl", "https://generativelanguage.googleapis.com/v1/models"),
                
                claudeApiKey = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "claudeApiKey", ""),
                claudeModel = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "claudeModel", "claude-3-sonnet-20240229"),
                claudeBaseUrl = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "claudeBaseUrl", "https://api.anthropic.com/v1/messages"),
                
                localApiUrl = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "localApiUrl", "http://localhost:11434/api/generate"),
                localModel = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "localModel", "llava"),
                
                visionModelProvider = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "visionModelProvider", "openai"),
                textModelProvider = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "textModelProvider", "openai"),
                codeModelProvider = UnityEditor.EditorPrefs.GetString(PREFS_PREFIX + "codeModelProvider", "claude"),
                
                timeoutSeconds = UnityEditor.EditorPrefs.GetInt(PREFS_PREFIX + "timeoutSeconds", 30),
                maxTokens = UnityEditor.EditorPrefs.GetInt(PREFS_PREFIX + "maxTokens", 1000)
            };
        }
#endif

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
