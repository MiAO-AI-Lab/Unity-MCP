#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using com.IvanMurzak.Unity.MCP.Server.ScriptableServices;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Server.ScriptableServices.Services
{
    /// <summary>
    /// AI Vision Analysis Scriptable Service - Advanced service that integrates Unity Runtime and Agent model capabilities
    /// This service masquerades as a tool call at the MCP protocol level, but internally can access Unity Runtime and ModelUse capabilities
    /// </summary>
    [ScriptableService("ai_vision", "AI Vision Analysis", "1.0")]
    public class AIVisionScriptableService : IScriptableService
    {
        private readonly ILogger<AIVisionScriptableService> _logger;
        private IScriptableServiceContext? _context;

        public string ServiceId => "ai_vision";
        public string ServiceName => "AI Vision Analysis";
        public string Description => "Advanced AI-powered image analysis service that combines Unity Runtime capabilities with Agent model processing";
        public string Version => "1.0";

        public ScriptableServiceCapabilities Capabilities => new()
        {
            RequiredModelTypes = new[] { "vision" },
            SupportedOperations = new[] { "analyze_image", "analyze_screenshot", "compare_images", "extract_text" },
            MaxImageSize = 10 * 1024 * 1024, // 10MB
            SupportedFormats = new[] { "png", "jpg", "jpeg", "bmp", "tga" },
            RequiresUnityRuntime = true,
            SupportsConcurrency = true
        };

        public AIVisionScriptableService(ILogger<AIVisionScriptableService> logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync(IScriptableServiceContext context)
        {
            _context = context;
            _logger.LogInformation($"[AIVisionScriptableService] Initialized for session: {context.SessionId}");
        }

        public async Task<ScriptableServiceResult> ExecuteAsync(ScriptableServiceRequest request, IScriptableServiceContext context)
        {
            try
            {
                _logger.LogDebug($"[AIVisionScriptableService] Executing operation: {request.Operation}");

                return request.Operation.ToLowerInvariant() switch
                {
                    "analyze_image" => await AnalyzeImageAsync(request, context),
                    "analyze_screenshot" => await AnalyzeScreenshotAsync(request, context),
                    "compare_images" => await CompareImagesAsync(request, context),
                    "extract_text" => await ExtractTextAsync(request, context),
                    _ => ScriptableServiceResult.Error($"Unsupported operation: {request.Operation}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AIVisionScriptableService] Error executing operation {request.Operation}: {ex.Message}");
                return ScriptableServiceResult.Error($"Execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyze specified image file
        /// </summary>
        private async Task<ScriptableServiceResult> AnalyzeImageAsync(ScriptableServiceRequest request, IScriptableServiceContext context)
        {
            var imagePath = request.GetParameter<string>("imagePath");
            var prompt = request.GetParameter<string>("prompt", "Please describe this image in detail");
            var includeUnityContext = request.GetParameter<bool>("includeUnityContext", false);

            if (string.IsNullOrEmpty(imagePath))
            {
                return ScriptableServiceResult.Error("imagePath parameter is required");
            }

            if (!File.Exists(imagePath))
            {
                return ScriptableServiceResult.Error($"Image file not found: {imagePath}");
            }

            try
            {
                // Read image data
                var imageData = await File.ReadAllBytesAsync(imagePath);

                // Optional: Get Unity context information
                string contextInfo = "";
                if (includeUnityContext && context.UnityRuntime != null)
                {
                    try
                    {
                        var sceneInfo = await context.UnityRuntime.CallUnityToolAsync("Scene_GetLoaded", new Dictionary<string, object>());
                        contextInfo = $"\n\nUnity Context:\n{sceneInfo}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[AIVisionScriptableService] Failed to get Unity context: {ex.Message}");
                    }
                }

                // Build complete prompt
                var fullPrompt = prompt + contextInfo;

                // Call Agent's vision model
                var analysisResult = await context.ModelUse.RequestVisionAsync<string>(fullPrompt, Convert.ToBase64String(imageData));

                var result = new
                {
                    imagePath = imagePath,
                    imageSize = imageData.Length,
                    analysis = analysisResult,
                    prompt = fullPrompt,
                    timestamp = DateTime.UtcNow,
                    unityContextIncluded = includeUnityContext
                };

                return ScriptableServiceResult.Success(result);
            }
            catch (Exception ex)
            {
                return ScriptableServiceResult.Error($"Image analysis failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyze Unity Editor screenshot
        /// </summary>
        private async Task<ScriptableServiceResult> AnalyzeScreenshotAsync(ScriptableServiceRequest request, IScriptableServiceContext context)
        {
            var prompt = request.GetParameter<string>("prompt", "Please describe this Unity Editor screenshot");
            var includeHierarchy = request.GetParameter<bool>("includeHierarchy", true);

            try
            {
                // Call Unity Runtime screenshot tool
                var screenshotResult = await context.UnityRuntime.CallUnityToolAsync("Editor_Screenshot_Take", new Dictionary<string, object>
                {
                    ["saveToFile"] = false,
                    ["returnBase64"] = true
                });

                // Parse screenshot result
                var screenshotData = JsonSerializer.Deserialize<Dictionary<string, object>>(screenshotResult);
                if (screenshotData == null || !screenshotData.ContainsKey("base64Data"))
                {
                    return ScriptableServiceResult.Error("Failed to capture screenshot");
                }

                var base64Data = screenshotData["base64Data"].ToString();
                var imageData = Convert.FromBase64String(base64Data!);

                // Optional: Get scene hierarchy information
                string contextInfo = "";
                if (includeHierarchy)
                {
                    try
                    {
                        var hierarchyInfo = await context.UnityRuntime.CallUnityToolAsync("Scene_GetHierarchy", new Dictionary<string, object>
                        {
                            ["includeChildrenDepth"] = 2
                        });
                        contextInfo = $"\n\nScene Hierarchy:\n{hierarchyInfo}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[AIVisionScriptableService] Failed to get hierarchy: {ex.Message}");
                    }
                }

                // Build complete prompt
                var fullPrompt = prompt + contextInfo;

                // Call Agent's vision model
                var analysisResult = await context.ModelUse.RequestVisionAsync<string>(fullPrompt, Convert.ToBase64String(imageData));

                var result = new
                {
                    screenshotSize = imageData.Length,
                    analysis = analysisResult,
                    prompt = fullPrompt,
                    timestamp = DateTime.UtcNow,
                    hierarchyIncluded = includeHierarchy
                };

                return ScriptableServiceResult.Success(result);
            }
            catch (Exception ex)
            {
                return ScriptableServiceResult.Error($"Screenshot analysis failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Compare two images
        /// </summary>
        private async Task<ScriptableServiceResult> CompareImagesAsync(ScriptableServiceRequest request, IScriptableServiceContext context)
        {
            var imagePath1 = request.GetParameter<string>("imagePath1");
            var imagePath2 = request.GetParameter<string>("imagePath2");
            var prompt = request.GetParameter<string>("prompt", "Compare these two images and describe the differences");

            if (string.IsNullOrEmpty(imagePath1) || string.IsNullOrEmpty(imagePath2))
            {
                return ScriptableServiceResult.Error("Both imagePath1 and imagePath2 parameters are required");
            }

            if (!File.Exists(imagePath1) || !File.Exists(imagePath2))
            {
                return ScriptableServiceResult.Error("One or both image files not found");
            }

            try
            {
                // Read both images
                var imageData1 = await File.ReadAllBytesAsync(imagePath1);
                var imageData2 = await File.ReadAllBytesAsync(imagePath2);

                // First analyze the first image
                var analysis1 = await context.ModelUse.RequestVisionAsync<string>("Describe this image in detail", Convert.ToBase64String(imageData1));

                // Then analyze the second image
                var analysis2 = await context.ModelUse.RequestVisionAsync<string>("Describe this image in detail", Convert.ToBase64String(imageData2));

                // Finally perform comparison analysis
                var comparisonPrompt = $"Based on these two image descriptions, {prompt}:\n\nImage 1: {analysis1}\n\nImage 2: {analysis2}";
                var comparisonResult = await context.ModelUse.RequestTextAsync<string>(comparisonPrompt);

                var result = new
                {
                    image1Path = imagePath1,
                    image2Path = imagePath2,
                    image1Analysis = analysis1,
                    image2Analysis = analysis2,
                    comparison = comparisonResult,
                    timestamp = DateTime.UtcNow
                };

                return ScriptableServiceResult.Success(result);
            }
            catch (Exception ex)
            {
                return ScriptableServiceResult.Error($"Image comparison failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract text from image
        /// </summary>
        private async Task<ScriptableServiceResult> ExtractTextAsync(ScriptableServiceRequest request, IScriptableServiceContext context)
        {
            var imagePath = request.GetParameter<string>("imagePath");
            var language = request.GetParameter<string>("language", "auto");

            if (string.IsNullOrEmpty(imagePath))
            {
                return ScriptableServiceResult.Error("imagePath parameter is required");
            }

            if (!File.Exists(imagePath))
            {
                return ScriptableServiceResult.Error($"Image file not found: {imagePath}");
            }

            try
            {
                var imageData = await File.ReadAllBytesAsync(imagePath);

                var prompt = language == "auto"
                    ? "Extract all text from this image. Return only the text content, preserving formatting where possible."
                    : $"Extract all text from this image in {language} language. Return only the text content, preserving formatting where possible.";

                var extractedText = await context.ModelUse.RequestVisionAsync<string>(prompt, Convert.ToBase64String(imageData));

                var result = new
                {
                    imagePath = imagePath,
                    extractedText = extractedText,
                    language = language,
                    timestamp = DateTime.UtcNow
                };

                return ScriptableServiceResult.Success(result);
            }
            catch (Exception ex)
            {
                return ScriptableServiceResult.Error($"Text extraction failed: {ex.Message}");
            }
        }

        public async Task<ScriptableServiceDescriptor> GetDescriptorAsync()
        {
            return new ScriptableServiceDescriptor
            {
                ServiceId = ServiceId,
                ServiceName = ServiceName,
                Description = Description,
                Version = Version,
                Capabilities = Capabilities,
                Parameters = new Dictionary<string, ServiceParameterDescriptor>
                {
                    ["operation"] = new()
                    {
                        Name = "operation",
                        Type = "string",
                        Description = "Operation to perform",
                        Required = true,
                        EnumValues = Capabilities.SupportedOperations
                    },
                    ["imagePath"] = new()
                    {
                        Name = "imagePath",
                        Type = "string",
                        Description = "Path to the image file",
                        Required = false
                    },
                    ["prompt"] = new()
                    {
                        Name = "prompt",
                        Type = "string",
                        Description = "Analysis prompt",
                        Required = false,
                        DefaultValue = "Please describe this image in detail"
                    },
                    ["includeUnityContext"] = new()
                    {
                        Name = "includeUnityContext",
                        Type = "boolean",
                        Description = "Include Unity scene context in analysis",
                        Required = false,
                        DefaultValue = false
                    },
                    ["includeHierarchy"] = new()
                    {
                        Name = "includeHierarchy",
                        Type = "boolean",
                        Description = "Include scene hierarchy in screenshot analysis",
                        Required = false,
                        DefaultValue = true
                    },
                    ["imagePath1"] = new()
                    {
                        Name = "imagePath1",
                        Type = "string",
                        Description = "Path to first image for comparison",
                        Required = false
                    },
                    ["imagePath2"] = new()
                    {
                        Name = "imagePath2",
                        Type = "string",
                        Description = "Path to second image for comparison",
                        Required = false
                    },
                    ["language"] = new()
                    {
                        Name = "language",
                        Type = "string",
                        Description = "Language for text extraction",
                        Required = false,
                        DefaultValue = "auto"
                    }
                }
            };
        }

        public async Task CleanupAsync()
        {
            _logger.LogInformation($"[AIVisionScriptableService] Cleanup completed");
        }
    }
}
#endif