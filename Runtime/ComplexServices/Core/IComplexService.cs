using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace com.MiAO.Unity.MCP.ComplexServices
{
    /// <summary>
    /// Complex service interface - Implemented in Unity Runtime
    /// </summary>
    public interface IComplexService
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
        ServiceCapabilities Capabilities { get; }

        /// <summary>
        /// Initialize service
        /// </summary>
        Task InitializeAsync(IServiceContext context);

        /// <summary>
        /// Execute service request
        /// </summary>
        Task<ServiceResult> ExecuteAsync(ServiceRequest request, IServiceContext context);

        /// <summary>
        /// Get service descriptor
        /// </summary>
        Task<ServiceDescriptor> GetDescriptorAsync();

        /// <summary>
        /// Cleanup service resources
        /// </summary>
        Task CleanupAsync();
    }

    /// <summary>
    /// Service context interface
    /// </summary>
    public interface IServiceContext
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
        /// Unity context (Scene, Camera, etc.)
        /// </summary>
        UnityContext Unity { get; }

        /// <summary>
        /// Memory manager
        /// </summary>
        IUnityMemoryManager Memory { get; }

        /// <summary>
        /// Model use service
        /// </summary>
        IModelUseService ModelUse { get; }

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
        IServiceContext CreateChildContext(string childSessionId);
    }

    /// <summary>
    /// Unity context information
    /// </summary>
    public class UnityContext
    {
        /// <summary>
        /// Current active scene
        /// </summary>
        public UnityEngine.SceneManagement.Scene ActiveScene { get; set; }

        /// <summary>
        /// Main camera
        /// </summary>
        public Camera MainCamera { get; set; }

        /// <summary>
        /// Selected game objects
        /// </summary>
        public GameObject[] SelectedGameObjects { get; set; } = Array.Empty<GameObject>();

        /// <summary>
        /// Whether in editor mode
        /// </summary>
        public bool IsInEditor { get; set; }

        /// <summary>
        /// Whether in play mode
        /// </summary>
        public bool IsPlaying { get; set; }
    }

    /// <summary>
    /// Service request
    /// </summary>
    public class ServiceRequest
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public IServiceContext? Context { get; set; }

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
    /// Service result
    /// </summary>
    public class ServiceResult
    {
        public bool IsSuccess { get; set; }
        public object? Content { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        public static ServiceResult Success(object? content = null, Dictionary<string, object>? metadata = null)
        {
            return new ServiceResult
            {
                IsSuccess = true,
                Content = content,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }
        
        public static ServiceResult Error(string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new ServiceResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// Service capability description
    /// </summary>
    public class ServiceCapabilities
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
        /// Whether Unity context is required
        /// </summary>
        public bool RequiresUnityContext { get; set; } = true;

        /// <summary>
        /// Whether supports concurrent execution
        /// </summary>
        public bool SupportsConcurrency { get; set; } = true;
    }

    /// <summary>
    /// Service descriptor
    /// </summary>
    public class ServiceDescriptor
    {
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public ServiceCapabilities Capabilities { get; set; } = new();
        public Dictionary<string, ParameterDescriptor> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Parameter descriptor
    /// </summary>
    public class ParameterDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Required { get; set; } = false;
        public object? DefaultValue { get; set; }
        public string[]? EnumValues { get; set; }
    }

    /// <summary>
    /// Complex service attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ComplexServiceAttribute : Attribute
    {
        public string ServiceId { get; }
        public string ServiceName { get; }
        public string Version { get; }

        public ComplexServiceAttribute(string serviceId, string serviceName = "", string version = "1.0")
        {
            ServiceId = serviceId;
            ServiceName = string.IsNullOrEmpty(serviceName) ? serviceId : serviceName;
            Version = version;
        }
    }

    /// <summary>
    /// Model use service interface
    /// </summary>
    public interface IModelUseService
    {
        /// <summary>
        /// Request model processing
        /// </summary>
        Task<T> RequestAsync<T>(ModelRequest request);
    }

    /// <summary>
    /// Model request
    /// </summary>
    public class ModelRequest
    {
        public string ModelType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Unity memory manager interface
    /// </summary>
    public interface IUnityMemoryManager
    {
        Task StoreAsync<T>(string sessionId, string key, T value, TimeSpan? expiry = null);
        Task<T?> RetrieveAsync<T>(string sessionId, string key);
        Task<bool> ExistsAsync(string sessionId, string key);
        Task RemoveAsync(string sessionId, string key);
        Task ClearSessionAsync(string sessionId);
    }
}
