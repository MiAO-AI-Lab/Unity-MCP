#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using com.MiAO.Unity.MCP.Server.RpcGateway.Core;
using com.MiAO.Unity.MCP.Server;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using System.Threading;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;

namespace com.MiAO.Unity.MCP.Server.RpcGateway.Unity
{
    /// <summary>
    /// Unity RPC Gateway Implementation - Refactored from ComplexServiceHandler Unity connection logic
    /// This gateway provides a standardized interface for communicating with Unity Runtime,
    /// enabling workflows to invoke Unity tools through the existing ToolRouter infrastructure
    /// while maintaining proper abstraction and error handling.
    /// </summary>
    public class UnityRpcGateway : IRpcGateway
    {
        private readonly ILogger<UnityRpcGateway> _logger;
        private readonly Dictionary<string, IRpcToolProxy> _toolProxies = new();
        private RpcToolDescriptor[]? _cachedTools;
        private DateTime? _lastToolsUpdate;
        private readonly TimeSpan _toolsCacheExpiry = TimeSpan.FromMinutes(5);

        public string GatewayId => "unity";

        public UnityRpcGateway(ILogger<UnityRpcGateway> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Call Unity tools through the existing ToolRouter infrastructure
        /// Executes Unity tools by leveraging the established ToolRouter.Call method,
        /// providing proper parameter mapping and result type conversion.
        /// </summary>
        public async Task<T> CallAsync<T>(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogDebug($"[UnityRpcGateway] Calling Unity tool: {toolName}");

                // Use the existing ToolRouter.Call method for Unity tool execution
                var response = await ToolRouter.Call(toolName, args =>
                {
                    foreach (var kvp in parameters)
                        args[kvp.Key] = kvp.Value;
                });

                if (response.IsError)
                {
                    throw new InvalidOperationException($"Unity tool call failed: {response.Content?.FirstOrDefault()?.Text}");
                }

                var resultText = response.Content?.FirstOrDefault()?.Text ?? "";

                // Attempt to parse result as the target type
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)resultText;
                }
                else if (typeof(T).IsClass && typeof(T) != typeof(string))
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<T>(resultText, JsonUtils.JsonSerializerOptions);
                        return result ?? throw new InvalidOperationException("Deserialization returned null");
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning($"[UnityRpcGateway] Failed to deserialize result as {typeof(T).Name}: {ex.Message}");
                        return (T)(object)resultText;
                    }
                }
                else
                {
                    return (T)Convert.ChangeType(resultText, typeof(T));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UnityRpcGateway] Error calling Unity tool {toolName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Dynamically discover Unity tools using the existing ToolRouter.ListAll method
        /// Retrieves the current list of available Unity tools and converts them to
        /// RPC tool descriptors with proper caching for performance optimization.
        /// </summary>
        public async Task<RpcToolDescriptor[]> DiscoverToolsAsync()
        {
            try
            {
                // Check cache validity to avoid unnecessary tool discovery calls
                if (_cachedTools != null && _lastToolsUpdate.HasValue &&
                    DateTime.UtcNow - _lastToolsUpdate.Value < _toolsCacheExpiry)
                {
                    return _cachedTools;
                }

                _logger.LogDebug("[UnityRpcGateway] Discovering Unity tools...");

                // Use ToolRouter.ListAll to get Unity tool list
                var toolsResult = await ToolRouter.ListAll(CancellationToken.None);

                if (toolsResult.Tools == null)
                {
                    _logger.LogWarning($"[UnityRpcGateway] Failed to get Unity tools");
                    return Array.Empty<RpcToolDescriptor>();
                }

                // Convert MCP tools to RPC tool descriptors
                var rpcToolDescriptors = new List<RpcToolDescriptor>();

                foreach (var tool in toolsResult.Tools)
                {
                    var rpcDescriptor = new RpcToolDescriptor
                    {
                        Name = tool.Name,
                        Description = tool.Description ?? "",
                        Parameters = ConvertParameters(tool.InputSchema),
                        ReturnType = "string",
                        Metadata = new Dictionary<string, object>
                        {
                            ["source"] = "Unity",
                            ["originalTool"] = tool
                        }
                    };

                    rpcToolDescriptors.Add(rpcDescriptor);
                }

                // Update cache with discovered tools
                _cachedTools = rpcToolDescriptors.ToArray();
                _lastToolsUpdate = DateTime.UtcNow;

                _logger.LogTrace($"[UnityRpcGateway] Discovered {_cachedTools.Length} Unity tools");
                return _cachedTools;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UnityRpcGateway] Error discovering Unity tools: {ex.Message}");
                return Array.Empty<RpcToolDescriptor>();
            }
        }

        /// <summary>
        /// Create a Unity tool proxy for simplified repeated invocations
        /// Creates a proxy object that encapsulates tool metadata and provides
        /// a streamlined interface for multiple calls to the same Unity tool.
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
                throw new ArgumentException($"Unity tool not found: {toolName}");
            }

            var proxy = new UnityToolProxy(toolName, toolDescriptor, this);
            _toolProxies[toolName] = proxy;

            return proxy;
        }

        /// <summary>
        /// Check Unity connection status by verifying ToolRunner availability
        /// Determines if the gateway can communicate with Unity Runtime
        /// by checking the availability of the McpServerService and its ToolRunner.
        /// </summary>
        public Task<bool> IsConnectedAsync()
        {
            try
            {
                var mcpServerService = McpServerService.Instance;
                return Task.FromResult(mcpServerService?.ToolRunner != null);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Convert parameter descriptions from MCP format to RPC format
        /// Transforms MCP tool input schema into RPC parameter descriptors
        /// for consistent parameter handling across different gateway types.
        /// </summary>
        private RpcParameterDescriptor[] ConvertParameters(object? inputSchema)
        {
            // Simplified implementation - should parse JSON Schema in production
            return Array.Empty<RpcParameterDescriptor>();
        }
    }

    /// <summary>
    /// Unity Tool Proxy Implementation - Encapsulates Unity tool invocation logic
    /// Provides a simplified interface for invoking specific Unity tools without
    /// requiring repeated tool discovery and metadata handling.
    /// </summary>
    public class UnityToolProxy : IRpcToolProxy
    {
        private readonly UnityRpcGateway _gateway;

        public string ToolName { get; }
        public RpcToolDescriptor Descriptor { get; }

        public UnityToolProxy(string toolName, RpcToolDescriptor descriptor, UnityRpcGateway gateway)
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
}
#endif