#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.Extensions.Logging;

namespace com.MiAO.Unity.MCP.Runtime.ModelUse
{
    /// <summary>
    /// ModelUse RPC Client - Unity Runtime side for sending model requests to MCP Server
    /// </summary>
    public class ModelUseRpcClient : IModelUseService
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<ModelUseRpcClient> _logger;

        public ModelUseRpcClient(IConnectionManager connectionManager, ILogger<ModelUseRpcClient> logger)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Request text model processing
        /// </summary>
        public async Task<T> RequestTextAsync<T>(string prompt, Dictionary<string, object>? parameters = null)
        {
            var request = new ModelUseRequest("text", prompt)
            {
                Parameters = parameters
            };

            return await RequestAsync<T>(request);
        }

        /// <summary>
        /// Request vision model processing
        /// </summary>
        public async Task<T> RequestVisionAsync<T>(string prompt, List<string> imageData, Dictionary<string, object>? parameters = null)
        {
            var messages = new List<Message> { Message.Text(prompt) };
            foreach (var image in imageData)
            {
                messages.Add(Message.Image(image));
            }

            var request = new ModelUseRequest("vision", messages)
            {
                Parameters = parameters
            };

            return await RequestAsync<T>(request);
        }

        /// <summary>
        /// Request code model processing
        /// </summary>
        public async Task<T> RequestCodeAsync<T>(string prompt, string? codeContext = null, Dictionary<string, object>? parameters = null)
        {
            var requestParams = parameters ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(codeContext))
            {
                requestParams["codeContext"] = codeContext;
            }

            var messages = new List<Message> { Message.Text(prompt) };
            if (!string.IsNullOrEmpty(codeContext))
            {
                messages.Add(Message.Code(codeContext));
            }

            var request = new ModelUseRequest("code", messages)
            {
                Parameters = requestParams
            };

            return await RequestAsync<T>(request);
        }

        /// <summary>
        /// Generic model request
        /// </summary>
        public async Task<T> RequestAsync<T>(ModelUseRequest request)
        {
            try
            {
                _logger.LogDebug($"[ModelUseRpcClient] Sending model request: {request.ModelType}");

                var requestData = new RequestModelUse
                {
                    RequestID = Guid.NewGuid().ToString(),
                    ModelType = request.ModelType,
                    Messages = request.Messages,
                    Parameters = request.Parameters
                };

                var response = await _connectionManager.InvokeAsync<RequestModelUse, ResponseData<ModelUseResponse>>(
                    Consts.RPC.Client.RequestModelUse,
                    requestData
                );

                if (response.IsError)
                {
                    throw new Exception(response.Message ?? "Model use request failed");
                }

                if (response.Value == null)
                {
                    throw new Exception("Model use response is null");
                }

                if (!response.Value.IsSuccess)
                {
                    throw new Exception(response.Value.ErrorMessage ?? "Model use request failed");
                }

                // Try to convert response content to target type
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)(response.Value.Content?.ToString() ?? "");
                }
                else if (typeof(T) == typeof(ModelUseResponse))
                {
                    return (T)(object)response.Value;
                }
                else
                {
                    // Try JSON deserialization
                    try
                    {
                        var jsonString = response.Value.Content?.ToString() ?? "";
                        return System.Text.Json.JsonSerializer.Deserialize<T>(jsonString)!;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[ModelUseRpcClient] Failed to deserialize response to {typeof(T).Name}: {ex.Message}");
                        return (T)(object)(response.Value.Content?.ToString() ?? "");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ModelUseRpcClient] Model request failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// ModelUse service interface
    /// </summary>
    public interface IModelUseService
    {
        Task<T> RequestTextAsync<T>(string prompt, Dictionary<string, object>? parameters = null);
        Task<T> RequestVisionAsync<T>(string prompt, List<string> imageData, Dictionary<string, object>? parameters = null);
        Task<T> RequestCodeAsync<T>(string prompt, string? codeContext = null, Dictionary<string, object>? parameters = null);
        Task<T> RequestAsync<T>(ModelUseRequest request);
    }
}