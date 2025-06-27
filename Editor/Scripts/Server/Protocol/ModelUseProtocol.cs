#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.Unity.MCP.Server.Proxy;

namespace com.IvanMurzak.Unity.MCP.Server.Protocol
{
    /// <summary>
    /// Model use protocol handler - Handles Unity Runtime reverse calls to Agent's ModelUse requests
    /// </summary>
    public class ModelUseProtocol
    {
        private readonly IAgentModelProxy _agentProxy;

        public ModelUseProtocol(IAgentModelProxy agentProxy)
        {
            _agentProxy = agentProxy;
        }

        // Default constructor, uses AgentModelProxyFactory
        public ModelUseProtocol()
        {
            _agentProxy = AgentModelProxyFactory.GetInstance();
        }

        /// <summary>
        /// Handle ModelUse request
        /// </summary>
        public async Task<string> HandleModelUseRequestAsync(string requestJson)
        {
            try
            {
                var request = JsonSerializer.Deserialize<ModelUseRequest>(requestJson);
                if (request == null)
                {
                    return CreateErrorResponse("Invalid request format");
                }

                // Validate request
                var validation = ValidateRequest(request);
                if (!validation.IsValid)
                {
                    return CreateErrorResponse(validation.ErrorMessage ?? "Request validation failed");
                }

                // Process request
                var response = await ProcessModelRequestAsync(request);
                return JsonSerializer.Serialize(response);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse($"Error processing model request: {ex.Message}");
            }
        }

        /// <summary>
        /// Process model request
        /// </summary>
        private async Task<ModelUseResponse> ProcessModelRequestAsync(ModelUseRequest request)
        {
            try
            {
                // Build AgentModelRequest
                var agentRequest = new AgentModelRequest
                {
                    Type = request.ModelType,
                    Prompt = request.Prompt,
                    ImageData = request.ImageData,
                    CodeContext = request.CodeContext,
                    Parameters = request.Parameters ?? new Dictionary<string, object>()
                };

                // Call AgentModelProxy
                var result = await _agentProxy.SendModelRequestAsync(agentRequest);

                if (result.IsSuccess)
                {
                    return ModelUseResponse.Success(result.Content);
                }
                else
                {
                    return ModelUseResponse.Error(result.ErrorMessage ?? "Model request failed");
                }
            }
            catch (Exception ex)
            {
                return ModelUseResponse.Error($"Error processing model request: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate request
        /// </summary>
        private ValidationResult ValidateRequest(ModelUseRequest request)
        {
            if (string.IsNullOrEmpty(request.ModelType))
                return ValidationResult.Invalid("ModelType is required");

            if (string.IsNullOrEmpty(request.Prompt))
                return ValidationResult.Invalid("Prompt is required");

            // Validate model type
            var supportedTypes = new[] { "vision", "text", "code" };
            if (!Array.Exists(supportedTypes, t => t.Equals(request.ModelType, StringComparison.OrdinalIgnoreCase)))
            {
                return ValidationResult.Invalid($"Unsupported model type: {request.ModelType}");
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Create error response
        /// </summary>
        private string CreateErrorResponse(string errorMessage)
        {
            var response = ModelUseResponse.Error(errorMessage);
            return JsonSerializer.Serialize(response);
        }
    }

    /// <summary>
    /// Agent model proxy interface (Server internal definition)
    /// </summary>
    public interface IAgentModelProxy : IDisposable
    {
        Task<AgentModelResponse> SendModelRequestAsync(AgentModelRequest request);
        Task<bool> IsConnectedAsync();
    }

    /// <summary>
    /// Agent model request (Server internal definition)
    /// </summary>
    public class AgentModelRequest
    {
        public string Type { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? ImageData { get; set; }
        public string? CodeContext { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Agent model response (Server internal definition)
    /// </summary>
    public class AgentModelResponse
    {
        public bool IsSuccess { get; set; }
        public object? Content { get; set; }
        public string? ErrorMessage { get; set; }

        public static AgentModelResponse Success(object? content) => new() { IsSuccess = true, Content = content };
        public static AgentModelResponse Error(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    /// Validation result (Server internal definition)
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        public static ValidationResult Valid() => new() { IsValid = true };
        public static ValidationResult Invalid(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
    }
}
