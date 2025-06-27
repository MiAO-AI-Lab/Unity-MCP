#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Server.ScriptableServices
{
    /// <summary>
    /// Scriptable service interface - Replaces ComplexService, providing more powerful scripting service capabilities
    /// </summary>
    public interface IScriptableService
    {
        /// <summary>
        /// Service unique identifier
        /// </summary>
        string ServiceId { get; }

        /// <summary>
        /// Service display name
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Service description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Service version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Service capability description
        /// </summary>
        ScriptableServiceCapabilities Capabilities { get; }

        /// <summary>
        /// Initialize service
        /// </summary>
        Task InitializeAsync(IScriptableServiceContext context);

        /// <summary>
        /// Execute service request
        /// </summary>
        Task<ScriptableServiceResult> ExecuteAsync(ScriptableServiceRequest request, IScriptableServiceContext context);

        /// <summary>
        /// Get service descriptor (used to generate MCP tool description)
        /// </summary>
        Task<ScriptableServiceDescriptor> GetDescriptorAsync();

        /// <summary>
        /// Clean up service resources
        /// </summary>
        Task CleanupAsync();
    }

    /// <summary>
    /// Scriptable service context - Provides access to Unity Runtime and ModelUse
    /// </summary>
    public interface IScriptableServiceContext
    {
        /// <summary>
        /// Session ID
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Agent ID
        /// </summary>
        string AgentId { get; }

        /// <summary>
        /// Unity Runtime connector (used to call Unity API)
        /// </summary>
        IUnityRuntimeConnector UnityRuntime { get; }

        /// <summary>
        /// Model use service (reverse call Agent's model capabilities)
        /// </summary>
        IModelUseService ModelUse { get; }

        /// <summary>
        /// Memory manager
        /// </summary>
        IScriptableServiceMemoryManager Memory { get; }

        /// <summary>
        /// Get property
        /// </summary>
        Task<T?> GetPropertyAsync<T>(string key);

        /// <summary>
        /// Set property
        /// </summary>
        Task SetPropertyAsync<T>(string key, T value);

        /// <summary>
        /// Create child context
        /// </summary>
        IScriptableServiceContext CreateChildContext(string childSessionId);
    }

    /// <summary>
    /// Unity Runtime connector interface
    /// </summary>
    public interface IUnityRuntimeConnector
    {
        /// <summary>
        /// Call Unity tool
        /// </summary>
        Task<string> CallUnityToolAsync(string toolName, Dictionary<string, object> parameters);

        /// <summary>
        /// Get Unity tools list
        /// </summary>
        Task<string[]> GetUnityToolsAsync();

        /// <summary>
        /// Check connection status
        /// </summary>
        Task<bool> IsConnectedAsync();
    }

    /// <summary>
    /// Model use service interface (reverse call Agent)
    /// </summary>
    public interface IModelUseService
    {
        /// <summary>
        /// Request text model processing
        /// </summary>
        Task<T> RequestTextAsync<T>(string prompt, Dictionary<string, object>? parameters = null);

        /// <summary>
        /// Request vision model processing
        /// </summary>
        Task<T> RequestVisionAsync<T>(string prompt, string imageData, Dictionary<string, object>? parameters = null);

        /// <summary>
        /// Request code model processing
        /// </summary>
        Task<T> RequestCodeAsync<T>(string prompt, string? codeContext = null, Dictionary<string, object>? parameters = null);

        /// <summary>
        /// Generic model request
        /// </summary>
        Task<T> RequestAsync<T>(ModelUseRequest request);
    }

    /// <summary>
    /// ScriptableService memory manager interface
    /// </summary>
    public interface IScriptableServiceMemoryManager
    {
        Task StoreAsync<T>(string sessionId, string key, T value, TimeSpan? expiry = null);
        Task<T?> RetrieveAsync<T>(string sessionId, string key);
        Task<bool> ExistsAsync(string sessionId, string key);
        Task RemoveAsync(string sessionId, string key);
        Task ClearSessionAsync(string sessionId);
    }

    /// <summary>
    /// Scriptable service registry interface
    /// </summary>
    public interface IScriptableServiceRegistry
    {
        Task InitializeAsync();
        Task<IScriptableService?> GetServiceAsync(string serviceId);
        Task<List<ScriptableServiceDescriptor>> GetAllServiceDescriptorsAsync();
        Task<bool> RegisterServiceAsync(IScriptableService service);
        Task<bool> UnregisterServiceAsync(string serviceId);
    }

    /// <summary>
    /// Scriptable service request
    /// </summary>
    public class ScriptableServiceRequest
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public IScriptableServiceContext? Context { get; set; }

        /// <summary>
        /// Get parameter value
        /// </summary>
        public T GetParameter<T>(string key, T defaultValue = default!)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T directValue)
                        return directValue;

                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
                    }

                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// Scriptable service result
    /// </summary>
    public class ScriptableServiceResult
    {
        public bool IsSuccess { get; set; }
        public object? Content { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public static ScriptableServiceResult Success(object? content = null, Dictionary<string, object>? metadata = null)
        {
            return new ScriptableServiceResult
            {
                IsSuccess = true,
                Content = content,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }

        public static ScriptableServiceResult Error(string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new ScriptableServiceResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// Scriptable service capability description
    /// </summary>
    public class ScriptableServiceCapabilities
    {
        /// <summary>
        /// Required model types
        /// </summary>
        public string[] RequiredModelTypes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Supported operations
        /// </summary>
        public string[] SupportedOperations { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Maximum image size (bytes)
        /// </summary>
        public long MaxImageSize { get; set; } = 0;

        /// <summary>
        /// Supported formats
        /// </summary>
        public string[] SupportedFormats { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether Unity Runtime connection is required
        /// </summary>
        public bool RequiresUnityRuntime { get; set; } = true;

        /// <summary>
        /// Whether concurrency is supported
        /// </summary>
        public bool SupportsConcurrency { get; set; } = true;
    }

    /// <summary>
    /// Scriptable service descriptor (used to generate MCP tool description)
    /// </summary>
    public class ScriptableServiceDescriptor
    {
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public ScriptableServiceCapabilities Capabilities { get; set; } = new();
        public Dictionary<string, ServiceParameterDescriptor> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Service parameter descriptor
    /// </summary>
    public class ServiceParameterDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Required { get; set; } = false;
        public object? DefaultValue { get; set; }
        public string[]? EnumValues { get; set; }
    }

    /// <summary>
    /// Scriptable service attribute marker
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ScriptableServiceAttribute : Attribute
    {
        public string ServiceId { get; }
        public string ServiceName { get; }
        public string Version { get; }

        public ScriptableServiceAttribute(string serviceId, string serviceName = "", string version = "1.0")
        {
            ServiceId = serviceId;
            ServiceName = string.IsNullOrEmpty(serviceName) ? serviceId : serviceName;
            Version = version;
        }
    }
}