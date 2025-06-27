using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.IvanMurzak.Unity.MCP.ComplexServices
{
    /// <summary>
    /// AI Vision Analysis Service - Implemented in Unity Runtime, can directly access Unity API
    /// </summary>
    [ComplexService("ai_vision", "AI Vision Analysis", "2.0")]
    public class AIVisionService : ComplexServiceBase
    {
        public override string ServiceId => "ai_vision";
        public override string ServiceName => "AI Vision Analysis";
        public override string Description => "Perform AI-powered image analysis using agent's vision model capabilities with Unity integration";

        public override ServiceCapabilities Capabilities => new()
        {
            RequiredModelTypes = new[] { "vision" },
            SupportedOperations = new[] { "analyze", "describe", "extract_text", "detect_objects", "capture_scene" },
            MaxImageSize = 10 * 1024 * 1024, // 10MB
            SupportedFormats = new[] { "jpg", "jpeg", "png", "gif", "bmp", "webp" },
            RequiresUnityContext = true,
            SupportsConcurrency = true
        };

        protected override async Task<Dictionary<string, ParameterDescriptor>> GenerateParametersAsync()
        {
            return await Task.FromResult(new Dictionary<string, ParameterDescriptor>
            {
                ["operation"] = new()
                {
                    Name = "operation",
                    Type = "string",
                    Description = "Type of vision analysis to perform",
                    Required = true,
                    EnumValues = Capabilities.SupportedOperations
                },
                ["imagePath"] = new()
                {
                    Name = "imagePath",
                    Type = "string",
                    Description = "Path to the image file to analyze (relative to project or absolute)",
                    Required = false
                },
                ["imageData"] = new()
                {
                    Name = "imageData",
                    Type = "string",
                    Description = "Base64 encoded image data",
                    Required = false
                },
                ["captureScene"] = new()
                {
                    Name = "captureScene",
                    Type = "boolean",
                    Description = "Whether to capture the current Unity scene view",
                    Required = false,
                    DefaultValue = false
                },
                ["prompt"] = new()
                {
                    Name = "prompt",
                    Type = "string",
                    Description = "Custom analysis prompt for the AI model",
                    Required = false,
                    DefaultValue = "Analyze this image in detail"
                },
                ["returnFormat"] = new()
                {
                    Name = "returnFormat",
                    Type = "string",
                    Description = "Format of the returned result",
                    Required = false,
                    DefaultValue = "analysis_only",
                    EnumValues = new[] { "analysis_only", "with_metadata", "structured", "json" }
                },
                ["useCache"] = new()
                {
                    Name = "useCache",
                    Type = "boolean",
                    Description = "Whether to use cached results if available",
                    Required = false,
                    DefaultValue = true
                }
            });
        }

        public override async Task<ServiceResult> ExecuteAsync(ServiceRequest request, IServiceContext context)
        {
            try
            {
                var validation = await ValidateRequestAsync(request);
                if (!validation.IsValid)
                {
                    return ServiceResult.Error(validation.ErrorMessage!);
                }

                var operation = request.GetParameter<string>("operation");
                
                await LogAsync($"Executing vision operation: {operation}");

                return operation.ToLowerInvariant() switch
                {
                    "analyze" => await AnalyzeImageAsync(request, context),
                    "describe" => await DescribeImageAsync(request, context),
                    "extract_text" => await ExtractTextAsync(request, context),
                    "detect_objects" => await DetectObjectsAsync(request, context),
                    "capture_scene" => await CaptureSceneAsync(request, context),
                    _ => ServiceResult.Error($"Unknown operation: {operation}")
                };
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "ExecuteAsync");
            }
        }

        private async Task<ServiceResult> AnalyzeImageAsync(ServiceRequest request, IServiceContext context)
        {
            try
            {
                var imageData = await GetImageDataAsync(request);
                if (imageData == null)
                {
                    return ServiceResult.Error("No valid image data provided");
                }

                var prompt = request.GetParameter<string>("prompt", "Analyze this image in detail");
                var returnFormat = request.GetParameter<string>("returnFormat", "analysis_only");
                var useCache = request.GetParameter<bool>("useCache", true);

                // Check cache
                if (useCache)
                {
                    var cacheKey = GenerateCacheKey("analyze", imageData, prompt);
                    var cachedResult = await RetrieveMemoryAsync<string>(cacheKey);
                    
                    if (!string.IsNullOrEmpty(cachedResult))
                    {
                        await LogAsync("Using cached analysis result");
                        return ServiceResult.Success(cachedResult);
                    }
                }

                // Request Agent's vision model
                var modelRequest = new ModelRequest
                {
                    ModelType = "vision",
                    Prompt = prompt,
                    ImageData = imageData,
                    Parameters = new Dictionary<string, object>
                    {
                        ["max_tokens"] = request.GetParameter<int>("maxTokens", 1000),
                        ["detail"] = request.GetParameter<string>("detail", "high"),
                        ["temperature"] = request.GetParameter<double>("temperature", 0.1)
                    }
                };

                var analysisResult = await RequestModelAsync<string>(modelRequest);

                // Format result
                var formattedResult = FormatAnalysisResult(analysisResult, returnFormat, "unity_context");

                // Cache result
                if (useCache)
                {
                    var cacheKey = GenerateCacheKey("analyze", imageData, prompt);
                    await StoreMemoryAsync(cacheKey, formattedResult, TimeSpan.FromHours(1));
                }

                return ServiceResult.Success(formattedResult);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "AnalyzeImageAsync");
            }
        }

        private async Task<ServiceResult> CaptureSceneAsync(ServiceRequest request, IServiceContext context)
        {
            try
            {
                await LogAsync("Capturing Unity scene view");

                // Get main camera
                var camera = GetMainCamera();
                if (camera == null)
                {
                    return ServiceResult.Error("No main camera found in the scene");
                }

                // Capture scene
                var sceneImage = await CaptureSceneImageAsync(camera);
                if (sceneImage == null)
                {
                    return ServiceResult.Error("Failed to capture scene image");
                }

                // If only capturing, return image info directly
                var captureOnly = request.GetParameter<bool>("captureOnly", false);
                if (captureOnly)
                {
                    return ServiceResult.Success(new
                    {
                        message = "Scene captured successfully",
                        imageSize = sceneImage.Length,
                        cameraInfo = new
                        {
                            position = camera.transform.position,
                            rotation = camera.transform.rotation,
                            fieldOfView = camera.fieldOfView
                        },
                        sceneInfo = new
                        {
                            name = GetActiveScene().name,
                            isLoaded = GetActiveScene().isLoaded,
                            gameObjectCount = GetActiveScene().GetRootGameObjects().Length
                        }
                    });
                }

                // Analyze captured scene
                var prompt = request.GetParameter<string>("prompt", "Analyze this Unity scene view");
                
                var modelRequest = new ModelRequest
                {
                    ModelType = "vision",
                    Prompt = prompt,
                    ImageData = sceneImage
                };

                var analysisResult = await RequestModelAsync<string>(modelRequest);

                return ServiceResult.Success(new
                {
                    analysis = analysisResult,
                    sceneInfo = new
                    {
                        name = GetActiveScene().name,
                        cameraPosition = camera.transform.position,
                        cameraRotation = camera.transform.rotation,
                        fieldOfView = camera.fieldOfView
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "CaptureSceneAsync");
            }
        }

        private async Task<ServiceResult> DescribeImageAsync(ServiceRequest request, IServiceContext context)
        {
            try
            {
                var imageData = await GetImageDataAsync(request);
                if (imageData == null)
                {
                    return ServiceResult.Error("No valid image data provided");
                }

                var style = request.GetParameter<string>("style", "detailed");
                var prompt = style.ToLowerInvariant() switch
                {
                    "brief" => "Provide a brief description of this image in 1-2 sentences.",
                    "detailed" => "Provide a detailed description of this image, including objects, people, setting, colors, and mood.",
                    "technical" => "Provide a technical description of this image, including composition, lighting, and visual elements.",
                    _ => "Describe this image."
                };

                var modelRequest = new ModelRequest
                {
                    ModelType = "vision",
                    Prompt = prompt,
                    ImageData = imageData,
                    Parameters = new Dictionary<string, object>
                    {
                        ["max_tokens"] = style == "brief" ? 100 : 500,
                        ["temperature"] = 0.3
                    }
                };

                var description = await RequestModelAsync<string>(modelRequest);

                return ServiceResult.Success(new
                {
                    description = description,
                    style = style,
                    unityContext = GetUnityContextInfo(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DescribeImageAsync");
            }
        }

        private async Task<ServiceResult> ExtractTextAsync(ServiceRequest request, IServiceContext context)
        {
            try
            {
                var imageData = await GetImageDataAsync(request);
                if (imageData == null)
                {
                    return ServiceResult.Error("No valid image data provided");
                }

                var language = request.GetParameter<string>("language", "auto");

                var modelRequest = new ModelRequest
                {
                    ModelType = "vision",
                    Prompt = $"Extract all text from this image. Language: {language}. Return only the extracted text, no additional commentary.",
                    ImageData = imageData,
                    Parameters = new Dictionary<string, object>
                    {
                        ["max_tokens"] = 2000,
                        ["temperature"] = 0.0
                    }
                };

                var textResult = await RequestModelAsync<string>(modelRequest);

                return ServiceResult.Success(new
                {
                    extractedText = textResult,
                    language = language,
                    unityContext = GetUnityContextInfo(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "ExtractTextAsync");
            }
        }

        private async Task<ServiceResult> DetectObjectsAsync(ServiceRequest request, IServiceContext context)
        {
            // Implement object detection logic
            return ServiceResult.Error("Object detection not yet implemented");
        }

        private async Task<byte[]?> GetImageDataAsync(ServiceRequest request)
        {
            var imagePath = request.GetParameter<string>("imagePath");
            var imageData = request.GetParameter<string>("imageData");
            var captureScene = request.GetParameter<bool>("captureScene", false);

            // Priority: captureScene > imageData > imagePath
            if (captureScene)
            {
                var camera = GetMainCamera();
                if (camera != null)
                {
                    return await CaptureSceneImageAsync(camera);
                }
            }

            if (!string.IsNullOrEmpty(imageData))
            {
                try
                {
                    return Convert.FromBase64String(imageData);
                }
                catch (Exception ex)
                {
                    await LogAsync($"Failed to decode base64 image data: {ex.Message}", LogType.Warning);
                }
            }

            if (!string.IsNullOrEmpty(imagePath))
            {
                return await LoadImageFromPathAsync(imagePath);
            }

            return null;
        }

        private async Task<byte[]?> LoadImageFromPathAsync(string imagePath)
        {
            try
            {
                // Handle relative paths
                if (!Path.IsPathRooted(imagePath))
                {
                    imagePath = Path.Combine(Application.dataPath, imagePath);
                }

                if (!File.Exists(imagePath))
                {
                    await LogAsync($"Image file not found: {imagePath}", LogType.Warning);
                    return null;
                }

                var imageData = await File.ReadAllBytesAsync(imagePath);
                
                if (imageData.Length > Capabilities.MaxImageSize)
                {
                    await LogAsync($"Image size {imageData.Length} exceeds maximum {Capabilities.MaxImageSize}", LogType.Warning);
                    return null;
                }

                return imageData;
            }
            catch (Exception ex)
            {
                await LogAsync($"Error loading image from {imagePath}: {ex.Message}", LogType.Error);
                return null;
            }
        }

        private async Task<byte[]?> CaptureSceneImageAsync(Camera camera)
        {
            try
            {
                // Create RenderTexture
                var renderTexture = new RenderTexture(1920, 1080, 24);
                var previousTarget = camera.targetTexture;
                
                camera.targetTexture = renderTexture;
                camera.Render();

                // Read pixels
                RenderTexture.active = renderTexture;
                var texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture2D.Apply();

                // Encode as PNG
                var imageData = texture2D.EncodeToPNG();

                // Cleanup
                camera.targetTexture = previousTarget;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(texture2D);
                renderTexture.Release();

                return await Task.FromResult(imageData);
            }
            catch (Exception ex)
            {
                await LogAsync($"Error capturing scene: {ex.Message}", LogType.Error);
                return null;
            }
        }

        private object FormatAnalysisResult(string analysisResult, string format, string source)
        {
            return format.ToLowerInvariant() switch
            {
                "analysis_only" => analysisResult,
                "with_metadata" => new
                {
                    analysis = analysisResult,
                    metadata = new
                    {
                        source = source,
                        timestamp = DateTime.UtcNow,
                        serviceVersion = Version,
                        unityContext = GetUnityContextInfo()
                    }
                },
                "structured" => new
                {
                    analysis = analysisResult,
                    source = source,
                    timestamp = DateTime.UtcNow,
                    serviceId = ServiceId,
                    version = Version,
                    unityContext = GetUnityContextInfo()
                },
                "json" => new
                {
                    result = analysisResult,
                    metadata = new
                    {
                        source = source,
                        timestamp = DateTime.UtcNow,
                        format = "json",
                        unityContext = GetUnityContextInfo()
                    }
                },
                _ => analysisResult
            };
        }

        private object GetUnityContextInfo()
        {
            try
            {
                var scene = GetActiveScene();
                var camera = GetMainCamera();
                
                return new
                {
                    scene = new
                    {
                        name = scene.name,
                        isLoaded = scene.isLoaded,
                        gameObjectCount = scene.GetRootGameObjects().Length
                    },
                    camera = camera != null ? new
                    {
                        position = camera.transform.position,
                        rotation = camera.transform.rotation,
                        fieldOfView = camera.fieldOfView
                    } : null,
                    isInEditor = IsInEditor(),
                    isPlaying = IsPlaying()
                };
            }
            catch
            {
                return new { error = "Failed to get Unity context" };
            }
        }

        private string GenerateCacheKey(string operation, byte[] imageData, string prompt)
        {
            using var sha256 = SHA256.Create();
            var imageHash = sha256.ComputeHash(imageData);
            var promptHash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(prompt));
            
            var combinedHash = sha256.ComputeHash(imageHash.Concat(promptHash).ToArray());
            var hashString = Convert.ToBase64String(combinedHash)[..8];
            
            return $"vision_{operation}_{hashString}";
        }
    }
}
