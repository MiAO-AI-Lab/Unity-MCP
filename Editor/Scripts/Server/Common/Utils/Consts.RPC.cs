#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public static partial class Consts
    {
        public static class Env
        {
            public const string Port = "UNITY_MCP_PORT";
        }
        public static class Hub
        {
            public const int DefaultPort = 60606;
            public const int MaxPort = 65535;
            public const string DefaultEndpoint = "http://localhost:60606";
            public const string RemoteApp = "/mcp/remote-app";
            public const float TimeoutSeconds = 60f;
        }

        public static partial class RPC
        {
            public static class Client
            {
                public const string RunCallTool = "/mcp/run-call-tool";
                public const string RunListTool = "/mcp/run-list-tool";
                public const string RunResourceContent = "/mcp/run-resource-content";
                public const string RunListResources = "/mcp/run-list-resources";
                public const string RunListResourceTemplates = "/mcp/run-list-resource-templates";
                public const string RequestModelUse = "RequestModelUse";
                public const string ForceDisconnect = "force-disconnect";
            }

            public static class Server
            {
                public const string OnListToolsUpdated = "OnListToolsUpdated";
                public const string OnListResourcesUpdated = "OnListResourcesUpdated";
                public const string OnModelUseResponse = "OnModelUseResponse";
            }
        }
    }

    /// <summary>
    /// ModelUse request data structure - Used for Unity Runtime to request model services from MCP Server
    /// </summary>
    public class RequestModelUse
    {
        public string RequestID { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? ImageData { get; set; }
        public string? CodeContext { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }

        public RequestModelUse() { }
        public RequestModelUse(string requestId, string modelType, string prompt)
        {
            RequestID = requestId;
            ModelType = modelType;
            Prompt = prompt;
        }
    }

    /// <summary>
    /// ModelUse request structure - Used for Runtime-side API calls
    /// </summary>
    public class ModelUseRequest
    {
        public string ModelType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? ImageData { get; set; }
        public string? CodeContext { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }

        public ModelUseRequest() { }
        public ModelUseRequest(string modelType, string prompt)
        {
            ModelType = modelType;
            Prompt = prompt;
        }
    }

    /// <summary>
    /// ModelUse response data structure
    /// </summary>
    public class ModelUseResponse
    {
        public bool IsSuccess { get; set; }
        public object? Content { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public ModelUseResponse() { }
        public ModelUseResponse(bool isSuccess, object? content, string? errorMessage = null)
        {
            IsSuccess = isSuccess;
            Content = content;
            ErrorMessage = errorMessage;
        }

        public static ModelUseResponse Success(object? content = null, Dictionary<string, object>? metadata = null)
        {
            return new ModelUseResponse(true, content) { Metadata = metadata };
        }

        public static ModelUseResponse Error(string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new ModelUseResponse(false, null, errorMessage) { Metadata = metadata };
        }
    }
}