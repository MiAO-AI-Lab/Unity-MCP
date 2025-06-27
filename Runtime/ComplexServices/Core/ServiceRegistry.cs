using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.ComplexServices
{
    /// <summary>
    /// Service Registry - Manages all complex services
    /// </summary>
    public static class ServiceRegistry
    {
        private static readonly Dictionary<string, IComplexService> _services = new();
        private static readonly Dictionary<string, Type> _serviceTypes = new();
        private static bool _isInitialized = false;
        private static readonly object _lock = new();

        /// <summary>
        /// Initialize service registry
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    Debug.Log("[ServiceRegistry] Initializing service registry...");

                    // Discover all complex services
                    DiscoverServices();
                    
                    _isInitialized = true;
                    Debug.Log($"[ServiceRegistry] Initialized with {_serviceTypes.Count} service types");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServiceRegistry] Failed to initialize: {ex.Message}");
                    throw;
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Get service instance
        /// </summary>
        public static async Task<IComplexService?> GetServiceAsync(string serviceId)
        {
            await EnsureInitializedAsync();

            // If instance already exists, return directly
            if (_services.TryGetValue(serviceId, out var existingService))
            {
                return existingService;
            }

            // Try to create new instance
            if (_serviceTypes.TryGetValue(serviceId, out var serviceType))
            {
                try
                {
                    var service = await CreateServiceInstanceAsync(serviceType);
                    if (service != null)
                    {
                        _services[serviceId] = service;

                        // Initialize service
                        var context = ServiceContextFactory.Create(
                            Guid.NewGuid().ToString(),
                            "system");
                        
                        await service.InitializeAsync(context);
                        
                        Debug.Log($"[ServiceRegistry] Created and initialized service: {serviceId}");
                        return service;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServiceRegistry] Failed to create service {serviceId}: {ex.Message}");
                }
            }

            Debug.LogWarning($"[ServiceRegistry] Service not found: {serviceId}");
            return null;
        }

        /// <summary>
        /// Get all available services
        /// </summary>
        public static async Task<IEnumerable<IComplexService>> GetAllServicesAsync()
        {
            await EnsureInitializedAsync();

            var services = new List<IComplexService>();

            foreach (var serviceId in _serviceTypes.Keys)
            {
                var service = await GetServiceAsync(serviceId);
                if (service != null)
                {
                    services.Add(service);
                }
            }

            return services;
        }

        /// <summary>
        /// Get all service descriptors
        /// </summary>
        public static async Task<IEnumerable<ServiceDescriptor>> GetAllServiceDescriptorsAsync()
        {
            var services = await GetAllServicesAsync();
            var descriptors = new List<ServiceDescriptor>();

            foreach (var service in services)
            {
                try
                {
                    var descriptor = await service.GetDescriptorAsync();
                    descriptors.Add(descriptor);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServiceRegistry] Failed to get descriptor for {service.ServiceId}: {ex.Message}");
                }
            }

            return descriptors;
        }

        /// <summary>
        /// Register service type
        /// </summary>
        public static void RegisterServiceType<T>(string serviceId) where T : IComplexService
        {
            RegisterServiceType(serviceId, typeof(T));
        }

        /// <summary>
        /// Register service type
        /// </summary>
        public static void RegisterServiceType(string serviceId, Type serviceType)
        {
            if (!typeof(IComplexService).IsAssignableFrom(serviceType))
            {
                throw new ArgumentException($"Type {serviceType.Name} does not implement IComplexService");
            }

            _serviceTypes[serviceId] = serviceType;
            Debug.Log($"[ServiceRegistry] Registered service type: {serviceId} -> {serviceType.Name}");
        }

        /// <summary>
        /// Unregister service
        /// </summary>
        public static async Task UnregisterServiceAsync(string serviceId)
        {
            if (_services.TryGetValue(serviceId, out var service))
            {
                try
                {
                    await service.CleanupAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServiceRegistry] Error cleaning up service {serviceId}: {ex.Message}");
                }

                _services.Remove(serviceId);
            }

            _serviceTypes.Remove(serviceId);
            Debug.Log($"[ServiceRegistry] Unregistered service: {serviceId}");
        }

        /// <summary>
        /// Cleanup all services
        /// </summary>
        public static async Task CleanupAllAsync()
        {
            var cleanupTasks = _services.Values.Select(async service =>
            {
                try
                {
                    await service.CleanupAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServiceRegistry] Error cleaning up service {service.ServiceId}: {ex.Message}");
                }
            });

            await Task.WhenAll(cleanupTasks);

            _services.Clear();
            _serviceTypes.Clear();
            _isInitialized = false;

            Debug.Log("[ServiceRegistry] All services cleaned up");
        }

        private static async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        private static void DiscoverServices()
        {
            var assemblies = GetRelevantAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var serviceTypes = assembly.GetTypes()
                        .Where(t => t.GetCustomAttribute<ComplexServiceAttribute>() != null)
                        .Where(t => typeof(IComplexService).IsAssignableFrom(t))
                        .Where(t => !t.IsAbstract && !t.IsInterface);

                    foreach (var serviceType in serviceTypes)
                    {
                        var attribute = serviceType.GetCustomAttribute<ComplexServiceAttribute>();
                        if (attribute != null)
                        {
                            RegisterServiceType(attribute.ServiceId, serviceType);
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"[ServiceRegistry] Failed to load types from assembly {assembly.FullName}: {ex.Message}");

                    // Try to load successful types
                    var loadedTypes = ex.Types.Where(t => t != null);
                    foreach (var type in loadedTypes)
                    {
                        try
                        {
                            var attribute = type!.GetCustomAttribute<ComplexServiceAttribute>();
                            if (attribute != null && typeof(IComplexService).IsAssignableFrom(type))
                            {
                                RegisterServiceType(attribute.ServiceId, type);
                            }
                        }
                        catch (Exception typeEx)
                        {
                            Debug.LogWarning($"[ServiceRegistry] Failed to process type {type?.Name}: {typeEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ServiceRegistry] Error processing assembly {assembly.FullName}: {ex.Message}");
                }
            }
        }

        private static async Task<IComplexService?> CreateServiceInstanceAsync(Type serviceType)
        {
            try
            {
                // Try to use default constructor
                var service = Activator.CreateInstance(serviceType) as IComplexService;
                return await Task.FromResult(service);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServiceRegistry] Failed to create instance of {serviceType.Name}: {ex.Message}");
                return null;
            }
        }

        private static IEnumerable<Assembly> GetRelevantAssemblies()
        {
            var assemblies = new List<Assembly>();

            // Current assembly
            assemblies.Add(Assembly.GetExecutingAssembly());

            // Calling assembly
            var callingAssembly = Assembly.GetCallingAssembly();
            if (!assemblies.Contains(callingAssembly))
            {
                assemblies.Add(callingAssembly);
            }

            // Loaded related assemblies
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Where(a => a.FullName?.Contains("Unity.MCP") == true || 
                           a.FullName?.Contains("ComplexServices") == true)
                .Where(a => !assemblies.Contains(a));

            assemblies.AddRange(loadedAssemblies);

            return assemblies;
        }
    }

    /// <summary>
    /// Service Registry Initializer
    /// </summary>
    public static class ServiceRegistryInitializer
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // Automatically initialize service registry at runtime
            _ = Task.Run(async () =>
            {
                try
                {
                    await ServiceRegistry.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServiceRegistryInitializer] Failed to initialize service registry: {ex.Message}");
                }
            });
        }
    }
}
