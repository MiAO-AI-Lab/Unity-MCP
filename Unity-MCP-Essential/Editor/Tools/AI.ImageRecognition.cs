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
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Essential.Tools
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
            [Description("Path to the image file to analyze. Supports PNG, JPG, JPEG, GIF, BMP, WebP formats. If need to analyze multiple images, please write in format: image1.png|image2.png|image3.png")]
            string imagePath,
            
            [Description("Custom prompt for image analysis. Describe what you want to know about the image. Default is 'Please describe the content of the image in detail, including objects, colors, shapes, and any visible text. Respond in English.'")]
            string prompt = "Please describe the content of the image in detail, including objects, colors, shapes, and any visible text. Respond in English.",
            
            [Description("Analysis focus: 'general', 'objects', 'text', 'colors', 'scene', 'technical'. Default is 'general'.")]
            string focus = "general",
            
            [Description("Maximum response length in characters. Default is 1000.")]
            int maxLength = 1000
        ) 
        {
            try
            {
                // Validate input parameters
                if (string.IsNullOrEmpty(imagePath))
                    return Error.ImagePathIsEmpty();

                // Split multiple image paths
                string[] imagePaths = imagePath.Split('|', StringSplitOptions.RemoveEmptyEntries);
                
                // Validate all image paths
                var validatedImages = new List<(string path, byte[] data, string base64)>();
                
                foreach (string path in imagePaths)
                {
                    string trimmedPath = path.Trim();
                    
                    // Check if file exists
                    if (!File.Exists(trimmedPath))
                        return Error.ImageFileNotFound(trimmedPath);

                    // Check file format
                    string extension = Path.GetExtension(trimmedPath).ToLowerInvariant();
                    if (!SupportedImageFormats.Contains(extension))
                        return Error.UnsupportedImageFormat(trimmedPath);

                    // Read and encode image
                    try
                    {
                        byte[] imageBytes = File.ReadAllBytes(trimmedPath);
                        string base64Image = Convert.ToBase64String(imageBytes);
                        
                        validatedImages.Add((trimmedPath, imageBytes, base64Image));
                        Debug.Log($"[AI_ImageRecognition] Image loaded: {trimmedPath}, {FormatFileSize(imageBytes.Length)}");
                    }
                    catch (Exception ex)
                    {
                        return Error.FailedToReadImageFile(trimmedPath, ex);
                    }
                }

                // Build complete analysis prompt
                string fullPrompt = BuildAnalysisPrompt(prompt, focus, maxLength, validatedImages.Count);

                // Perform AI analysis
                return await PerformMultipleImagesAnalysisAsync(validatedImages, fullPrompt);
            }
            catch (Exception ex)
            {
                return Error.AIRequestFailed(ex.Message);
            }
        }

        private static string BuildAnalysisPrompt(string userPrompt, string focus, int maxLength, int imageCount = 1)
        {
            var promptBuilder = new StringBuilder();
            
            // Add language instruction
            promptBuilder.AppendLine("Please respond in English.");

            // Add multiple images instruction if applicable
            if (imageCount > 1)
            {
                promptBuilder.AppendLine($"You are analyzing {imageCount} images. Please analyze each image and provide a comprehensive comparison or combined analysis as appropriate.");
            }

            // Add focus instruction
            switch (focus.ToLowerInvariant())
            {
                case "objects":
                    string objectInstruction = imageCount > 1 
                        ? "Focus on identifying and describing objects in each image, and compare objects across images."
                        : "Focus on identifying and describing objects in the image.";
                    promptBuilder.AppendLine(objectInstruction);
                    break;
                case "text":
                    string textInstruction = imageCount > 1 
                        ? "Focus on reading and transcribing any text visible in each image."
                        : "Focus on reading and transcribing any text visible in the image.";
                    promptBuilder.AppendLine(textInstruction);
                    break;
                case "colors":
                    string colorInstruction = imageCount > 1 
                        ? "Focus on describing colors, color schemes, and visual aesthetics in each image and compare them."
                        : "Focus on describing colors, color schemes, and visual aesthetics.";
                    promptBuilder.AppendLine(colorInstruction);
                    break;
                case "scene":
                    string sceneInstruction = imageCount > 1 
                        ? "Focus on describing the overall scene, setting, and context of each image."
                        : "Focus on describing the overall scene, setting, and context.";
                    promptBuilder.AppendLine(sceneInstruction);
                    break;
                case "technical":
                    string technicalInstruction = imageCount > 1 
                        ? "Focus on technical aspects like composition, lighting, and image quality for each image."
                        : "Focus on technical aspects like composition, lighting, and image quality.";
                    promptBuilder.AppendLine(technicalInstruction);
                    break;
                case "general":
                    string generalInstruction = imageCount > 1 
                        ? "Provide a comprehensive analysis of each image and their relationships."
                        : "Provide a comprehensive analysis of the image.";
                    promptBuilder.AppendLine(generalInstruction);
                    break;
                case "none": // Do not add any focus instruction
                default:
                    promptBuilder.AppendLine("");
                    break;
            }

            // Add user prompt
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(userPrompt);

            // Add length limitation
            if (maxLength > 0)
            {
                string lengthInstruction = imageCount > 1
                    ? $"\nPlease limit your response to approximately {maxLength} characters for the combined analysis."
                    : $"\nPlease limit your response to approximately {maxLength} characters.";
                promptBuilder.AppendLine(lengthInstruction);
            }

            return promptBuilder.ToString();
        }

        private static async Task<string> PerformMultipleImagesAnalysisAsync(List<(string path, byte[] data, string base64)> images, string prompt)
        {
            try
            {
                Debug.Log($"[AI_ImageRecognition] Starting multiple images AI analysis via RpcRouter");
                Debug.Log($"[AI_ImageRecognition] Analyzing {images.Count} images");
                Debug.Log($"[AI_ImageRecognition] Prompt: {prompt}");

                var rpcRouter = McpServiceLocator.GetRequiredService<IRpcRouter>();
                
                // For multiple images, we'll create a list of base64 data
                // and include image information in the prompt
                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine(prompt);
                // promptBuilder.AppendLine();
                // promptBuilder.AppendLine("Images being analyzed:");
                
                var base64ImagesList = new List<string>();
                
                for (int i = 0; i < images.Count; i++)
                {
                    var image = images[i];
                    promptBuilder.AppendLine($"Image {i + 1}: {Path.GetFileName(image.path)} ({FormatFileSize(image.data.Length)})");
                    base64ImagesList.Add(image.base64);
                }

                var messages = new List<Message> { Message.Text(promptBuilder.ToString()) };
                foreach (var base64Image in base64ImagesList)
                {
                    messages.Add(Message.Image(base64Image));
                }
                var request = new RequestModelUse
                {
                    RequestID = Guid.NewGuid().ToString(),
                    ModelType = "vision",
                    Messages = messages,
                };

                Debug.Log($"[AI_ImageRecognition] Sending multiple images request with ID: {request.RequestID}");
                
                var response = await rpcRouter.RequestModelUse(request);
                
                // Access Value property in ResponseData<ModelUseResponse> to get ModelUseResponse
                if (response?.Value?.IsSuccess == true)
                {
                    var result = response.Value.Content?.ToString() ?? "No content received";
                    Debug.Log($"[AI_ImageRecognition] Multiple images analysis result: {result}");
                    return result;
                }
                else
                {
                    var errorMessage = response?.Value?.ErrorMessage ?? "Unknown error";
                    Debug.LogError($"[AI_ImageRecognition] Multiple images analysis failed: {errorMessage}");
                    return Error.AIRequestFailed(errorMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI_ImageRecognition] Multiple images analysis failed: {ex.Message}");
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