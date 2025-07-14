#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.IO;
using com.MiAO.Unity.MCP.Common;


namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_AI
    {
        private static readonly string[] SupportedImageFormats = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

        public static class Error
        {
            public static string ImagePathIsEmpty() => "[Error] Image path is empty";
            public static string ImageFileNotFound(string path) => $"[Error] Image file not found: {path}";
            public static string UnsupportedImageFormat(string path) => $"[Error] Unsupported image format: {Path.GetExtension(path)}";
            public static string PromptIsEmpty() => "[Error] Prompt is empty";
            public static string InvalidProvider(string provider) => $"[Error] Invalid provider: {provider}";
            public static string InvalidReturnFormat(string format) => $"[Error] Invalid return format: {format}";
            public static string InvalidLanguage(string language) => $"[Error] Invalid language: {language}";
            public static string InvalidFocus(string focus) => $"[Error] Invalid focus: {focus}";
            public static string APIError(string message) => $"[Error] API error: {message}";
            public static string UnexpectedError(Exception ex) => $"[Error] Unexpected error: {ex.Message}";
            public static string FailedToReadImageFile(string path, Exception ex) => $"[Error] Failed to read image file {path}: {ex.Message}";
            public static string AIRequestFailed(string message) => $"[Error] AI request failed: {message}";
        }
    }
} 