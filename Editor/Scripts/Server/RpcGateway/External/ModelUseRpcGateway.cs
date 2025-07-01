#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using com.MiAO.Unity.MCP.Server.RpcGateway.Core;
using com.MiAO.Unity.MCP.Server.Protocol;
using com.MiAO.Unity.MCP.Server.Proxy;

namespace com.MiAO.Unity.MCP.Server.RpcGateway.External
{
    /// <summary>
    /// ModelUse RPC Gateway - Refactored from ComplexServiceHandler ModelUse logic
    /// This gateway provides a standardized interface for communicating with AI model services,
    /// enabling workflows to invoke various AI capabilities (text, vision, code generation)
    /// through a unified RPC abstraction layer.
    /// </summary>
    public class ModelUseRpcGateway : IRpcGateway
    {
        private readonly ILogger<ModelUseRpcGateway> _logger;
        private readonly ModelUseProtocol _modelUseProtocol;
        private readonly Dictionary<string, IRpcToolProxy> _toolProxies = new();

        public string GatewayId => "model_use";

        public ModelUseRpcGateway(ILogger<ModelUseRpcGateway> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var agentProxy = AgentModelProxyFactory.GetInstance();
            _modelUseProtocol = new ModelUseProtocol(agentProxy);
        }

        /// <summary>
        /// Call ModelUse functionality through the established protocol
        /// Executes AI model requests by leveraging the existing ModelUseProtocol,
        /// providing proper parameter serialization and response handling.
        /// </summary>
        public async Task<T> CallAsync<T>(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogDebug($"[ModelUseRpcGateway] Calling ModelUse tool: {toolName}");

                var requestJson = JsonSerializer.Serialize(new
                {
                    operation = toolName,
                    parameters = parameters
                });

                var responseJson = await _modelUseProtocol.HandleModelUseRequestAsync(requestJson);

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)responseJson;
                }
                else
                {
                    var result = JsonSerializer.Deserialize<T>(responseJson);
                    return result ?? throw new InvalidOperationException("ModelUse response deserialization returned null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ModelUseRpcGateway] Error calling ModelUse tool {toolName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Discover available ModelUse tools with predefined capabilities
        /// Returns a comprehensive list of AI model capabilities including text generation,
        /// vision analysis, and code generation with proper parameter schemas.
        /// </summary>
        public async Task<RpcToolDescriptor[]> DiscoverToolsAsync()
        {
            // ModelUse tools are predefined based on available AI model capabilities
            var tools = new[]
            {
                new RpcToolDescriptor
                {
                    Name = "text",
                    Description = "Request text completion from AI model",
                    Parameters = new[]
                    {
                        new RpcParameterDescriptor { Name = "prompt", Type = "string", Required = true, Description = "Text prompt" },
                        new RpcParameterDescriptor { Name = "parameters", Type = "object", Required = false, Description = "Additional parameters" }
                    },
                    ReturnType = "string",
                    Metadata = new Dictionary<string, object> { ["source"] = "model_use", ["category"] = "text" }
                },
                new RpcToolDescriptor
                {
                    Name = "vision",
                    Description = "Request vision analysis from AI model",
                    Parameters = new[]
                    {
                        new RpcParameterDescriptor { Name = "prompt", Type = "string", Required = true, Description = "Analysis prompt" },
                        new RpcParameterDescriptor { Name = "imageData", Type = "string", Required = true, Description = "Base64 image data" },
                        new RpcParameterDescriptor { Name = "parameters", Type = "object", Required = false, Description = "Additional parameters" }
                    },
                    ReturnType = "string",
                    Metadata = new Dictionary<string, object> { ["source"] = "model_use", ["category"] = "vision" }
                },
                new RpcToolDescriptor
                {
                    Name = "code",
                    Description = "Request code generation/analysis from AI model",
                    Parameters = new[]
                    {
                        new RpcParameterDescriptor { Name = "prompt", Type = "string", Required = true, Description = "Code prompt" },
                        new RpcParameterDescriptor { Name = "codeContext", Type = "string", Required = false, Description = "Existing code context" },
                        new RpcParameterDescriptor { Name = "parameters", Type = "object", Required = false, Description = "Additional parameters" }
                    },
                    ReturnType = "string",
                    Metadata = new Dictionary<string, object> { ["source"] = "model_use", ["category"] = "code" }
                }
            };

            return await Task.FromResult(tools);
        }

        /// <summary>
        /// Create a ModelUse tool proxy for streamlined repeated invocations
        /// Creates a proxy object that encapsulates tool metadata and provides
        /// a simplified interface for multiple calls to the same AI model capability.
        /// </summary>
        public async Task<IRpcToolProxy> CreateToolProxyAsync(string toolName)
        {
            if (_toolProxies.TryGetValue(toolName, out var existingProxy))
            {
                return existingProxy;
            }

            var tools = await DiscoverToolsAsync();
            var toolDescriptor = Array.Find(tools, t => t.Name == toolName);

            if (toolDescriptor == null)
            {
                throw new ArgumentException($"ModelUse tool not found: {toolName}");
            }

            var proxy = new ModelUseToolProxy(toolName, toolDescriptor, this);
            _toolProxies[toolName] = proxy;

            return proxy;
        }

        /// <summary>
        /// Check ModelUse connection status - always available through existing proxy
        /// ModelUse is always considered available because it operates through the
        /// existing AgentModelProxy infrastructure which is always initialized.
        /// </summary>
        public async Task<bool> IsConnectedAsync()
        {
            // ModelUse is always available because it works through the existing proxy mechanism
            return await Task.FromResult(true);
        }
    }

    /// <summary>
    /// ModelUse Tool Proxy Implementation - Encapsulates AI model invocation logic
    /// Provides a simplified interface for invoking specific AI model capabilities
    /// without requiring repeated tool discovery and metadata handling.
    /// </summary>
    public class ModelUseToolProxy : IRpcToolProxy
    {
        private readonly ModelUseRpcGateway _gateway;

        public string ToolName { get; }
        public RpcToolDescriptor Descriptor { get; }

        public ModelUseToolProxy(string toolName, RpcToolDescriptor descriptor, ModelUseRpcGateway gateway)
        {
            ToolName = toolName;
            Descriptor = descriptor;
            _gateway = gateway;
        }

        public async Task<T> InvokeAsync<T>(Dictionary<string, object> parameters)
        {
            return await _gateway.CallAsync<T>(ToolName, parameters);
        }
    }

    /// <summary>
    /// ModelUse Service Interface - Extracted from ComplexServiceHandler
    /// Defines the contract for AI model service operations including text generation,
    /// vision analysis, and code generation capabilities.
    /// </summary>
    public interface IModelUseService
    {
        Task<T> RequestTextAsync<T>(string prompt, Dictionary<string, object>? parameters = null);
        Task<T> RequestVisionAsync<T>(string prompt, string imageData, Dictionary<string, object>? parameters = null);
        Task<T> RequestCodeAsync<T>(string prompt, string? codeContext = null, Dictionary<string, object>? parameters = null);
    }

    /// <summary>
    /// ModelUse Service Implementation - Adapts existing ModelUseProtocol
    /// Provides a high-level service interface that adapts the existing ModelUseProtocol
    /// for easier integration with workflow orchestration and other system components.
    /// </summary>
    public class ModelUseService : IModelUseService
    {
        private readonly ModelUseRpcGateway _gateway;

        public ModelUseService(ModelUseRpcGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public async Task<T> RequestTextAsync<T>(string prompt, Dictionary<string, object>? parameters = null)
        {
            var requestParams = new Dictionary<string, object>
            {
                ["prompt"] = prompt
            };
            if (parameters != null)
                requestParams["parameters"] = parameters;

            return await _gateway.CallAsync<T>("text", requestParams);
        }

        public async Task<T> RequestVisionAsync<T>(string prompt, string imageData, Dictionary<string, object>? parameters = null)
        {
            var requestParams = new Dictionary<string, object>
            {
                ["prompt"] = prompt,
                ["imageData"] = imageData
            };
            if (parameters != null)
                requestParams["parameters"] = parameters;

            return await _gateway.CallAsync<T>("vision", requestParams);
        }

        public async Task<T> RequestCodeAsync<T>(string prompt, string? codeContext = null, Dictionary<string, object>? parameters = null)
        {
            var requestParams = new Dictionary<string, object>
            {
                ["prompt"] = prompt
            };
            if (codeContext != null)
                requestParams["codeContext"] = codeContext;
            if (parameters != null)
                requestParams["parameters"] = parameters;

            return await _gateway.CallAsync<T>("code", requestParams);
        }
    }
}
#endif