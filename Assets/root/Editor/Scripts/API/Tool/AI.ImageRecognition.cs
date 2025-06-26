#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    static partial class Tool_AI_CertBypass
    {
        static Tool_AI_CertBypass()
        {
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
            // Set security protocol
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Can return true in development environment, but should perform proper certificate validation in production environment
            Debug.Log($"[AI_ImageRecognition] ValidateServerCertificate: {certificate.Subject}");
            return true;
        }
    }

    // Added: Custom certificate handler
    public class BypassCertificateHandler : UnityEngine.Networking.CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            Debug.Log("[AI_ImageRecognition] BypassCertificateHandler: ValidateCertificate called!");
            return true;
        }
    }

    public partial class Tool_AI
    {
        [McpPluginTool
        (
            "AI_ImageRecognition",
            Title = "AI Image Recognition and Analysis"
        )]
        [Description(@"Analyze images using AI vision models with custom prompts. 
Supports multiple AI service providers and flexible output formats.
Can return analysis results, Base64 data, or both for direct AI communication.")]
        public async Task<string> ImageRecognition
        (
            [Description("Path to the image file to analyze. Supports PNG, JPG, JPEG, GIF, BMP, WebP formats.")]
            string imagePath,
            
            [Description("Custom prompt for image analysis. Describe what you want to know about the image. Default is 'Please describe the content of the image in detail, including objects, colors, shapes, and any visible text. Respond in English.'")]
            string prompt = "Please describe the content of the image in detail, including objects, colors, shapes, and any visible text. Respond in English.",
            
            [Description("Analysis focus: 'general', 'objects', 'text', 'colors', 'scene', 'technical'. Default is 'general'.")]
            string focus = "general",
            
            [Description("Maximum response length in characters. Default is 1000.")]
            int maxLength = 1000
        ) 
        {
            var language = "en-US";
            var returnFormat = "analysis_only";
            
            try
            {
                // Validate input parameters
                if (string.IsNullOrEmpty(imagePath))
                    return Error.ImagePathIsEmpty();

                // Check if file exists
                if (!File.Exists(imagePath))
                    return Error.ImageFileNotFound(imagePath);

                // Check file format
                string extension = Path.GetExtension(imagePath).ToLowerInvariant();
                if (!SupportedImageFormats.Contains(extension))
                    return Error.UnsupportedImageFormat(imagePath);

                // Read and encode image
                string base64Image;
                byte[] imageBytes;
                try
                {
                    // Read image file directly to avoid Unity texture processing thread issues
                    imageBytes = File.ReadAllBytes(imagePath);
                    base64Image = Convert.ToBase64String(imageBytes);
                    
                    Debug.Log($"[AI_ImageRecognition] Original image size: {FormatFileSize(imageBytes.Length)}");
                }
                catch (Exception ex)
                {
                    return Error.FailedToReadImageFile(imagePath, ex);
                }

                Debug.Log($"[AI_ImageRecognition] Image loaded: {imagePath}");
                Debug.Log($"[AI_ImageRecognition] Image size: {FormatFileSize(imageBytes.Length)}");
                Debug.Log($"[AI_ImageRecognition] Base64 length: {base64Image.Length}");

                // Build complete analysis prompt
                string fullPrompt = BuildAnalysisPrompt(prompt, focus, language, maxLength);

                // Process based on return format
                switch (returnFormat.ToLowerInvariant())
                {
                    case "analysis only":
                        return await PerformAIAnalysisAsync(base64Image, fullPrompt, imagePath);
                    default:
                        return await PerformAIAnalysisAsync(base64Image, fullPrompt, imagePath);
                }
            }
            catch (Exception ex)
            {
                return Error.AIRequestFailed(ex.Message);
            }
        }

        private static string BuildAnalysisPrompt(string userPrompt, string focus, string language, int maxLength)
        {
            var promptBuilder = new StringBuilder();
            
            // Add language instruction
            if (language == "zh-CN")
            {
                promptBuilder.AppendLine("请用中文回答。");
            }
            else
            {
                promptBuilder.AppendLine("Please respond in English.");
            }

            // Add focus instruction
            switch (focus.ToLowerInvariant())
            {
                case "objects":
                    promptBuilder.AppendLine(language == "zh-CN" ? 
                        "重点识别和描述图像中的物体。" : 
                        "Focus on identifying and describing objects in the image.");
                    break;
                case "text":
                    promptBuilder.AppendLine(language == "zh-CN" ? 
                        "重点读取和转录图像中可见的任何文字。" : 
                        "Focus on reading and transcribing any text visible in the image.");
                    break;
                case "colors":
                    promptBuilder.AppendLine(language == "zh-CN" ? 
                        "重点描述颜色、配色方案和视觉美学。" : 
                        "Focus on describing colors, color schemes, and visual aesthetics.");
                    break;
                case "scene":
                    promptBuilder.AppendLine(language == "zh-CN" ? 
                        "重点描述整体场景、设置和背景。" : 
                        "Focus on describing the overall scene, setting, and context.");
                    break;
                case "technical":
                    promptBuilder.AppendLine(language == "zh-CN" ? 
                        "重点分析技术方面，如构图、光线和图像质量。" : 
                        "Focus on technical aspects like composition, lighting, and image quality.");
                    break;
                default:
                    promptBuilder.AppendLine(language == "zh-CN" ? 
                        "提供图像的全面分析。" : 
                        "Provide a comprehensive analysis of the image.");
                    break;
            }

            // Add user prompt
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(userPrompt);

            // Add length limitation
            if (maxLength > 0)
            {
                string lengthInstruction = language == "zh-CN" ? 
                    $"\n请将回答限制在大约{maxLength}个字符内。请使用中文回答。" : 
                    $"\nPlease limit your response to approximately {maxLength} characters.";
                promptBuilder.AppendLine(lengthInstruction);
            }

            return promptBuilder.ToString();
        }

        private static string FormatBase64Response(string base64Image, string imagePath)
        {
            var mimeType = "image/png";
            return $"data:{mimeType};base64,{base64Image}";
        }

        private static async Task<string> PerformAIAnalysisAsync(string base64Image, string prompt, string imagePath)
        {
            try
            {
                Debug.Log($"[AI_ImageRecognition] Starting AI analysis via RpcRouter");
                Debug.Log($"[AI_ImageRecognition] Prompt: {prompt}");

                var rpcRouter = McpServiceLocator.GetRequiredService<IRpcRouter>();
                
                var request = new RequestModelUse
                {
                    RequestID = Guid.NewGuid().ToString(),
                    ModelType = "vision",
                    Prompt = prompt,
                    ImageData = base64Image,
                };

                Debug.Log($"[AI_ImageRecognition] Sending request with ID: {request.RequestID}");
                
                var response = await rpcRouter.RequestModelUse(request);
                
                // Access Value property in ResponseData<ModelUseResponse> to get ModelUseResponse
                if (response?.Value?.IsSuccess == true)
                {
                    var result = response.Value.Content?.ToString() ?? "No content received";
                    Debug.Log($"[AI_ImageRecognition] Analysis result: {result}");
                    return result;
                }
                else
                {
                    var errorMessage = response?.Value?.ErrorMessage ?? "Unknown error";
                    Debug.LogError($"[AI_ImageRecognition] Analysis failed: {errorMessage}");
                    return Error.AIRequestFailed(errorMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI_ImageRecognition] Analysis failed: {ex.Message}");
                return Error.AIRequestFailed(ex.Message);
            }
        }

        private static string FormatFileSize(long bytes)
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
    }
} 