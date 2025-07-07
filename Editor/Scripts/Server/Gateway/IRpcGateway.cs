#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace com.MiAO.Unity.MCP.Server.RpcGateway.Core
{
    /// <summary>
    /// RPC Gateway Core Interface - Unified RPC Call Abstraction
    /// This interface provides a standardized way to communicate with external systems
    /// through RPC calls, abstracting away the specific communication protocols and
    /// providing a consistent interface for workflow orchestration.
    /// </summary>
    public interface IRpcGateway
    {
        /// <summary>
        /// Call a remote tool with specified parameters
        /// Executes a remote procedure call to the target system with the provided
        /// tool name and parameters, returning the result as the specified type.
        /// </summary>
        Task<T> CallAsync<T>(string toolName, Dictionary<string, object> parameters);

        /// <summary>
        /// Dynamically discover available tools from the remote system
        /// Queries the remote system to retrieve a list of available tools,
        /// their parameters, and metadata for dynamic tool discovery.
        /// </summary>
        Task<RpcToolDescriptor[]> DiscoverToolsAsync();

        /// <summary>
        /// Create a tool proxy for easier tool invocation
        /// Creates a proxy object that encapsulates the tool information and
        /// provides a simplified interface for repeated tool invocations.
        /// </summary>
        Task<IRpcToolProxy> CreateToolProxyAsync(string toolName);

        /// <summary>
        /// Check the connection status to the remote system
        /// Verifies if the gateway is properly connected and able to
        /// communicate with the target remote system.
        /// </summary>
        Task<bool> IsConnectedAsync();

        /// <summary>
        /// Gateway identifier for registration and routing purposes
        /// Unique identifier used by the workflow engine to route
        /// calls to the appropriate gateway implementation.
        /// </summary>
        string GatewayId { get; }
    }

    /// <summary>
    /// RPC Tool Descriptor - Metadata about a remote tool
    /// Contains comprehensive information about a remote tool including
    /// its parameters, return type, and additional metadata for proper
    /// invocation and documentation purposes.
    /// </summary>
    public class RpcToolDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RpcParameterDescriptor[] Parameters { get; set; } = Array.Empty<RpcParameterDescriptor>();
        public string ReturnType { get; set; } = "string";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// RPC Parameter Descriptor - Detailed parameter information
    /// Describes the structure and requirements of a parameter for
    /// proper validation and invocation of remote tools.
    /// </summary>
    public class RpcParameterDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Required { get; set; } = false;
        public object? DefaultValue { get; set; }
    }

    /// <summary>
    /// RPC Tool Proxy Interface - Simplified tool invocation
    /// Provides a higher-level interface for invoking specific tools
    /// without needing to repeatedly specify tool metadata.
    /// </summary>
    public interface IRpcToolProxy
    {
        string ToolName { get; }
        RpcToolDescriptor Descriptor { get; }
        Task<T> InvokeAsync<T>(Dictionary<string, object> parameters);
    }
}
#endif