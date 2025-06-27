using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Common
{
    /// <summary>
    /// MCP service locator - Provides global access to dependency injection container, directly caches service instances to avoid repeated creation
    /// </summary>
    public static class McpServiceLocator
    {
        private static IServiceProvider? _serviceProvider;
        private static readonly object _lock = new object();

        // Cached service instances
        private static IRpcRouter? _cachedRpcRouter;
        private static IConnectionManager? _cachedConnectionManager;
        private static IMcpPlugin? _cachedMcpPlugin;
        private static ILogger? _cachedLogger;

        /// <summary>
        /// Initialize service locator and cache core service instances
        /// </summary>
        /// <param name="serviceProvider">Service provider instance</param>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            lock (_lock)
            {
                _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

                // Immediately get and cache core service instances
                try
                {
                    _cachedRpcRouter = _serviceProvider.GetRequiredService<IRpcRouter>();
                    _cachedConnectionManager = _serviceProvider.GetRequiredService<IConnectionManager>();
                    _cachedMcpPlugin = _serviceProvider.GetRequiredService<IMcpPlugin>();
                }
                catch (Exception ex)
                {
                    // If unable to get core services, log warning but don't throw exception
                    _cachedLogger?.LogWarning($"Failed to cache core services during initialization: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Clear service locator
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _serviceProvider = null;
                _cachedRpcRouter = null;
                _cachedConnectionManager = null;
                _cachedMcpPlugin = null;
                _cachedLogger = null;
            }
        }

        /// <summary>
        /// Get service instance - Prioritize returning cached instances
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance</returns>
        /// <exception cref="InvalidOperationException">Service locator not initialized or service not registered</exception>
        public static T GetService<T>() where T : notnull
        {
            lock (_lock)
            {
                if (_serviceProvider == null)
                {
                    throw new InvalidOperationException("McpServiceLocator is not initialized. Make sure to call Initialize() first.");
                }

                // Check if it's a cached core service type
                if (typeof(T) == typeof(IRpcRouter) && _cachedRpcRouter != null)
                {
                    return (T)_cachedRpcRouter;
                }
                if (typeof(T) == typeof(IConnectionManager) && _cachedConnectionManager != null)
                {
                    return (T)_cachedConnectionManager;
                }
                if (typeof(T) == typeof(IMcpPlugin) && _cachedMcpPlugin != null)
                {
                    return (T)_cachedMcpPlugin;
                }

                var service = _serviceProvider.GetService<T>();
                if (service == null)
                {
                    throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
                }

                return service;
            }
        }

        /// <summary>
        /// Try to get service instance - Prioritize returning cached instances
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance, returns null if not found</returns>
        public static T? TryGetService<T>() where T : class
        {
            lock (_lock)
            {
                if (_serviceProvider == null)
                {
                    return null;
                }

                // Check if it's a cached core service type
                if (typeof(T) == typeof(IRpcRouter) && _cachedRpcRouter != null)
                {
                    return _cachedRpcRouter as T;
                }
                if (typeof(T) == typeof(IConnectionManager) && _cachedConnectionManager != null)
                {
                    return _cachedConnectionManager as T;
                }
                if (typeof(T) == typeof(IMcpPlugin) && _cachedMcpPlugin != null)
                {
                    return _cachedMcpPlugin as T;
                }

                return _serviceProvider.GetService<T>();
            }
        }

        /// <summary>
        /// Get required service instance - Prioritize returning cached instances
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance</returns>
        /// <exception cref="InvalidOperationException">Service locator not initialized or service not registered</exception>
        public static T GetRequiredService<T>() where T : notnull
        {
            lock (_lock)
            {
                if (_serviceProvider == null)
                {
                    throw new InvalidOperationException("McpServiceLocator is not initialized. Make sure to call Initialize() first.");
                }

                // Check if it's a cached core service type
                if (typeof(T) == typeof(IRpcRouter) && _cachedRpcRouter != null)
                {
                    return (T)_cachedRpcRouter;
                }
                if (typeof(T) == typeof(IConnectionManager) && _cachedConnectionManager != null)
                {
                    return (T)_cachedConnectionManager;
                }
                if (typeof(T) == typeof(IMcpPlugin) && _cachedMcpPlugin != null)
                {
                    return (T)_cachedMcpPlugin;
                }

                return _serviceProvider.GetRequiredService<T>();
            }
        }

        /// <summary>
        /// Check if service locator is initialized
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (_lock)
                {
                    return _serviceProvider != null;
                }
            }
        }

        /// <summary>
        /// Create scoped service
        /// </summary>
        /// <returns>Service scope</returns>
        public static IServiceScope CreateScope()
        {
            lock (_lock)
            {
                if (_serviceProvider == null)
                {
                    throw new InvalidOperationException("McpServiceLocator is not initialized. Make sure to call Initialize() first.");
                }

                return _serviceProvider.CreateScope();
            }
        }
    }

    /// <summary>
    /// Service locator extension methods
    /// </summary>
    public static class McpServiceLocatorExtensions
    {
        /// <summary>
        /// Get Logger instance
        /// </summary>
        /// <typeparam name="T">Logger type parameter</typeparam>
        /// <returns>Logger instance</returns>
        public static ILogger<T> GetLogger<T>()
        {
            return McpServiceLocator.GetRequiredService<ILogger<T>>();
        }

        /// <summary>
        /// Get RpcRouter instance - Returns cached instance
        /// </summary>
        /// <returns>RpcRouter instance</returns>
        public static IRpcRouter GetRpcRouter()
        {
            return McpServiceLocator.GetRequiredService<IRpcRouter>();
        }

        /// <summary>
        /// Get ConnectionManager instance - Returns cached instance
        /// </summary>
        /// <returns>ConnectionManager instance</returns>
        public static IConnectionManager GetConnectionManager()
        {
            return McpServiceLocator.GetRequiredService<IConnectionManager>();
        }
    }
}