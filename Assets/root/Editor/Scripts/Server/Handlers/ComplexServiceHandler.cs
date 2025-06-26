using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.Unity.MCP.Server.Protocol;
using com.IvanMurzak.Unity.MCP.Server.Proxy;
#if !UNITY_5_3_OR_NEWER
using com.IvanMurzak.Unity.MCP.Server.ScriptableServices;
#endif
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Server.Handlers
{
    /// <summary>
    /// Complex service handler - Refactored to ScriptableService framework
    /// Now supports reverse ModelUse RPC and Unity Runtime connections
    /// </summary>
    public class ComplexServiceHandler
    {
        private readonly ModelUseProtocol _modelUseProtocol;
#if !UNITY_5_3_OR_NEWER
        private readonly Dictionary<string, IScriptableServiceContext> _sessionContexts = new();
        private readonly IScriptableServiceRegistry _serviceRegistry;
        private readonly IUnityRuntimeConnector _unityRuntimeConnector;
#endif
        private readonly ILogger<ComplexServiceHandler> _logger;

        public ComplexServiceHandler(ILogger<ComplexServiceHandler> logger)
        {
            _logger = logger;
            var agentProxy = AgentModelProxyFactory.GetInstance();
            _modelUseProtocol = new ModelUseProtocol(agentProxy);

#if !UNITY_5_3_OR_NEWER
            // Create appropriate Logger instances
            var serviceRegistryLogger = new com.IvanMurzak.Unity.MCP.Server.ScriptableServices.ConsoleLogger<ScriptableServiceRegistry>();
            var unityRuntimeLogger = new com.IvanMurzak.Unity.MCP.Server.ScriptableServices.ConsoleLogger<UnityRuntimeConnector>();

            _serviceRegistry = new ScriptableServiceRegistry(serviceRegistryLogger);
            _unityRuntimeConnector = new UnityRuntimeConnector(unityRuntimeLogger);
#endif
        }

#if !UNITY_5_3_OR_NEWER
        /// <summary>
        /// Handle ScriptableService calls (replacing original ComplexService)
        /// </summary>
        public async Task<string> HandleScriptableServiceCallAsync(string serviceName, Dictionary<string, object> parameters, string sessionId = null, string agentId = null)
        {
            try
            {
                _logger.LogDebug($"[ComplexServiceHandler] Handling scriptable service call: {serviceName}");

                // Ensure service registry is initialized
                await _serviceRegistry.InitializeAsync();

                // Get service instance
                var service = await _serviceRegistry.GetServiceAsync(serviceName);
                if (service == null)
                {
                    return CreateErrorResponse($"Scriptable service not found: {serviceName}");
                }

                // Create or get service context
                var context = await GetOrCreateServiceContextAsync(sessionId ?? Guid.NewGuid().ToString(), agentId ?? "unknown");

                // Create service request
                var request = new ScriptableServiceRequest
                {
                    ServiceId = serviceName,
                    Operation = parameters.GetValueOrDefault("operation", "").ToString() ?? "",
                    Parameters = parameters,
                    Context = context
                };

                // Execute service
                var result = await service.ExecuteAsync(request, context);

                // Return result
                if (result.IsSuccess)
                {
                    return CreateSuccessResponse(result.Content, result.Metadata);
                }
                else
                {
                    return CreateErrorResponse(result.ErrorMessage ?? "Service execution failed", result.Metadata);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ComplexServiceHandler] Error handling scriptable service call: {ex.Message}");
                return CreateErrorResponse($"Internal error: {ex.Message}");
            }
        }
#else
        /// <summary>
        /// ScriptableService call handling in Unity environment (simplified version)
        /// </summary>
        public async Task<string> HandleScriptableServiceCallAsync(string serviceName, Dictionary<string, object> parameters, string sessionId = null, string agentId = null)
        {
            _logger.LogWarning($"[ComplexServiceHandler] ScriptableService calls not supported in Unity environment: {serviceName}");
            return CreateErrorResponse("ScriptableService calls are not supported in Unity environment. Use Unity Runtime tools instead.");
        }
#endif

        /// <summary>
        /// Handle model use requests (reverse call to Agent)
        /// </summary>
        public async Task<string> HandleModelUseRequestAsync(string requestJson)
        {
            return await _modelUseProtocol.HandleModelUseRequestAsync(requestJson);
        }

#if !UNITY_5_3_OR_NEWER
        /// <summary>
        /// Get all available ScriptableServices
        /// </summary>
        public async Task<string> GetAvailableServicesAsync()
        {
            try
            {
                await _serviceRegistry.InitializeAsync();
                var descriptors = await _serviceRegistry.GetAllServiceDescriptorsAsync();

                var services = new List<object>();
                foreach (var descriptor in descriptors)
                {
                    services.Add(new
                    {
                        serviceId = descriptor.ServiceId,
                        serviceName = descriptor.ServiceName,
                        description = descriptor.Description,
                        version = descriptor.Version,
                        capabilities = descriptor.Capabilities,
                        parameters = descriptor.Parameters
                    });
                }

                return CreateSuccessResponse(new
                {
                    services = services,
                    count = services.Count,
                    framework = "ScriptableService"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ComplexServiceHandler] Error getting available services: {ex.Message}");
                return CreateErrorResponse($"Failed to get services: {ex.Message}");
            }
        }

        /// <summary>
        /// Get service descriptor
        /// </summary>
        public async Task<string> GetServiceDescriptorAsync(string serviceName)
        {
            try
            {
                await _serviceRegistry.InitializeAsync();
                var service = await _serviceRegistry.GetServiceAsync(serviceName);

                if (service == null)
                {
                    return CreateErrorResponse($"Service not found: {serviceName}");
                }

                var descriptor = await service.GetDescriptorAsync();
                return CreateSuccessResponse(descriptor);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ComplexServiceHandler] Error getting service descriptor: {ex.Message}");
                return CreateErrorResponse($"Failed to get descriptor: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up session context
        /// </summary>
        public async Task CleanupSessionAsync(string sessionId)
        {
            if (_sessionContexts.TryGetValue(sessionId, out var context))
            {
                try
                {
                    await context.Memory.ClearSessionAsync(sessionId);
                    _sessionContexts.Remove(sessionId);
                    _logger.LogInformation($"[ComplexServiceHandler] Cleaned up session: {sessionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[ComplexServiceHandler] Error cleaning up session {sessionId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get or create ScriptableService context
        /// </summary>
        private async Task<IScriptableServiceContext> GetOrCreateServiceContextAsync(string sessionId, string agentId)
        {
            if (_sessionContexts.TryGetValue(sessionId, out var existingContext))
            {
                return existingContext;
            }

            // Create new service context
            var memoryManager = new ScriptableServiceMemoryManager();
            var modelUseService = new ScriptableServiceModelUseService(_modelUseProtocol);

            var context = new ScriptableServiceContext
            {
                SessionId = sessionId,
                AgentId = agentId,
                UnityRuntime = _unityRuntimeConnector,
                ModelUse = modelUseService,
                Memory = memoryManager,
                Properties = new Dictionary<string, object>()
            };

            _sessionContexts[sessionId] = context;
            return context;
        }
#else
        /// <summary>
        /// Service retrieval in Unity environment (simplified version)
        /// </summary>
        public async Task<string> GetAvailableServicesAsync()
        {
            return CreateSuccessResponse(new
            {
                services = new List<object>(),
                count = 0,
                framework = "Unity Runtime Tools",
                message = "Use Unity Runtime tools instead of ScriptableServices in Unity environment"
            });
        }

        /// <summary>
        /// Service descriptor retrieval in Unity environment (simplified version)
        /// </summary>
        public async Task<string> GetServiceDescriptorAsync(string serviceName)
        {
            return CreateErrorResponse("Service descriptors are not supported in Unity environment. Use Unity Runtime tools instead.");
        }

        /// <summary>
        /// Session cleanup in Unity environment (simplified version)
        /// </summary>
        public async Task CleanupSessionAsync(string sessionId)
        {
            _logger.LogInformation($"[ComplexServiceHandler] Session cleanup not needed in Unity environment: {sessionId}");
            await Task.CompletedTask;
        }
#endif

        /// <summary>
        /// Create success response
        /// </summary>
        private string CreateSuccessResponse(object? content = null, Dictionary<string, object>? metadata = null)
        {
            var response = new
            {
                success = true,
                content = content,
                metadata = metadata ?? new Dictionary<string, object>(),
                timestamp = DateTime.UtcNow.ToString("O")
            };

            return JsonSerializer.Serialize(response);
        }

        /// <summary>
        /// Create error response
        /// </summary>
        private string CreateErrorResponse(string errorMessage, Dictionary<string, object>? metadata = null)
        {
            var response = new
            {
                success = false,
                error = errorMessage,
                metadata = metadata ?? new Dictionary<string, object>(),
                timestamp = DateTime.UtcNow.ToString("O")
            };

            return JsonSerializer.Serialize(response);
        }
    }

#if !UNITY_5_3_OR_NEWER
    public class ScriptableServiceModelUseService : IModelUseService
    {
        private readonly ModelUseProtocol _modelUseProtocol;

        public ScriptableServiceModelUseService(ModelUseProtocol modelUseProtocol)
        {
            _modelUseProtocol = modelUseProtocol;
        }

        public async Task<T> RequestTextAsync<T>(string prompt, Dictionary<string, object>? parameters = null)
        {
            var request = new ModelUseRequest
            {
                ModelType = "text",
                Prompt = prompt,
                Parameters = parameters ?? new Dictionary<string, object>()
            };

            return await RequestAsync<T>(request);
        }

        public async Task<T> RequestVisionAsync<T>(string prompt, string imageData, Dictionary<string, object>? parameters = null)
        {
            var request = new ModelUseRequest
            {
                ModelType = "vision",
                Prompt = prompt,
                ImageData = imageData,
                Parameters = parameters ?? new Dictionary<string, object>()
            };

            return await RequestAsync<T>(request);
        }

        public async Task<T> RequestCodeAsync<T>(string prompt, string? codeContext = null, Dictionary<string, object>? parameters = null)
        {
            var requestParams = parameters ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(codeContext))
            {
                requestParams["codeContext"] = codeContext;
            }

            var request = new ModelUseRequest
            {
                ModelType = "code",
                Prompt = prompt,
                Parameters = requestParams
            };

            return await RequestAsync<T>(request);
        }

        public async Task<T> RequestAsync<T>(ModelUseRequest request)
        {
            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                var responseJson = await _modelUseProtocol.HandleModelUseRequestAsync(requestJson);

                var response = JsonSerializer.Deserialize<ModelUseResponse>(responseJson);
                if (response?.IsSuccess == true && response.Content != null)
                {
                    if (response.Content is T directResult)
                    {
                        return directResult;
                    }

                    // Try to deserialize
                    var contentJson = JsonSerializer.Serialize(response.Content);
                    return JsonSerializer.Deserialize<T>(contentJson) ?? default(T)!;
                }

                throw new Exception(response?.ErrorMessage ?? "Unknown error in model request");
            }
            catch (Exception ex)
            {
                throw new Exception($"ModelUse request failed: {ex.Message}", ex);
            }
        }
    }

    public class UnityRuntimeConnector : IUnityRuntimeConnector
    {
        private readonly ILogger<UnityRuntimeConnector> _logger;

        public UnityRuntimeConnector(ILogger<UnityRuntimeConnector> logger)
        {
            _logger = logger;
        }

        public async Task<string> CallUnityToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogDebug($"[UnityRuntimeConnector] Calling Unity tool: {toolName}");

                // Actual communication with Unity Runtime should be implemented here
                // Return simulated response for now
                var response = new
                {
                    success = true,
                    toolName = toolName,
                    result = "Tool execution simulated in ScriptableService environment",
                    parameters = parameters
                };

                return JsonSerializer.Serialize(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UnityRuntimeConnector] Error calling Unity tool {toolName}: {ex.Message}");
                throw;
            }
        }

        public async Task<string[]> GetUnityToolsAsync()
        {
            // Return list of available Unity tools
            return new[]
            {
                "Unity_GameObject_Create",
                "Unity_Scene_Load",
                "Unity_Asset_Import",
                // ... other Unity tools
            };
        }

        public async Task<bool> IsConnectedAsync()
        {
            // Simulate connection status in ScriptableService environment
            return await Task.FromResult(false); // Connect to Unity Runtime in actual environment
        }
    }

    public class ScriptableServiceMemoryManager : IScriptableServiceMemoryManager
    {
        private readonly Dictionary<string, Dictionary<string, (object Value, DateTime? Expiry)>> _sessionData = new();

        public async Task StoreAsync<T>(string sessionId, string key, T value, TimeSpan? expiry = null)
        {
            if (!_sessionData.ContainsKey(sessionId))
            {
                _sessionData[sessionId] = new Dictionary<string, (object, DateTime?)>();
            }

            var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
            _sessionData[sessionId][key] = (value!, expiryTime);

            await Task.CompletedTask;
        }

        public async Task<T?> RetrieveAsync<T>(string sessionId, string key)
        {
            if (!_sessionData.TryGetValue(sessionId, out var sessionDict) ||
                !sessionDict.TryGetValue(key, out var data))
            {
                return default(T);
            }

            // Check expiry time
            if (data.Expiry.HasValue && DateTime.UtcNow > data.Expiry.Value)
            {
                sessionDict.Remove(key);
                return default(T);
            }

            try
            {
                if (data.Value is T directValue)
                {
                    return directValue;
                }

                // Try to convert
                if (data.Value != null)
                {
                    var json = JsonSerializer.Serialize(data.Value);
                    return JsonSerializer.Deserialize<T>(json);
                }
            }
            catch
            {
                // Conversion failed, return default value
            }

            return default(T);
        }

        public async Task<bool> ExistsAsync(string sessionId, string key)
        {
            if (!_sessionData.TryGetValue(sessionId, out var sessionDict) ||
                !sessionDict.TryGetValue(key, out var data))
            {
                return false;
            }

            // Check expiry time
            if (data.Expiry.HasValue && DateTime.UtcNow > data.Expiry.Value)
            {
                sessionDict.Remove(key);
                return false;
            }

            return await Task.FromResult(true);
        }

        public async Task RemoveAsync(string sessionId, string key)
        {
            if (_sessionData.TryGetValue(sessionId, out var sessionDict))
            {
                sessionDict.Remove(key);
            }

            await Task.CompletedTask;
        }

        public async Task ClearSessionAsync(string sessionId)
        {
            _sessionData.Remove(sessionId);
            await Task.CompletedTask;
        }
    }

    public class ScriptableServiceContext : IScriptableServiceContext
    {
        public string SessionId { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
        public IUnityRuntimeConnector UnityRuntime { get; set; } = null!;
        public IModelUseService ModelUse { get; set; } = null!;
        public IScriptableServiceMemoryManager Memory { get; set; } = null!;
        public Dictionary<string, object> Properties { get; set; } = new();

        public async Task<T?> GetPropertyAsync<T>(string key)
        {
            if (Properties.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T directValue)
                        return directValue;

                    var json = JsonSerializer.Serialize(value);
                    return JsonSerializer.Deserialize<T>(json);
                }
                catch
                {
                    return default(T);
                }
            }

            return default(T);
        }

        public async Task SetPropertyAsync<T>(string key, T value)
        {
            Properties[key] = value!;
            await Task.CompletedTask;
        }

        public IScriptableServiceContext CreateChildContext(string childSessionId)
        {
            return new ScriptableServiceContext
            {
                SessionId = childSessionId,
                AgentId = AgentId,
                UnityRuntime = UnityRuntime,
                ModelUse = ModelUse,
                Memory = Memory,
                Properties = new Dictionary<string, object>(Properties)
            };
        }
    }
#endif
}