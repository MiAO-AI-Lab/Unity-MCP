#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Server.ScriptableServices
{
    /// <summary>
    /// Scriptable service registry - Manages registration and discovery of all ScriptableServices
    /// </summary>
    public class ScriptableServiceRegistry : IScriptableServiceRegistry
    {
        private readonly ILogger<ScriptableServiceRegistry> _logger;
        private readonly Dictionary<string, IScriptableService> _services = new();
        private readonly Dictionary<string, ScriptableServiceDescriptor> _descriptors = new();
        private bool _initialized = false;

        public ScriptableServiceRegistry(ILogger<ScriptableServiceRegistry> logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            _logger.LogInformation("[ScriptableServiceRegistry] Initializing scriptable services...");

            // Automatically discover all services marked with ScriptableServiceAttribute
            await DiscoverServicesAsync();

            _initialized = true;
            _logger.LogInformation($"[ScriptableServiceRegistry] Initialized {_services.Count} scriptable services");
        }

        public async Task<IScriptableService?> GetServiceAsync(string serviceId)
        {
            await InitializeAsync();
            _services.TryGetValue(serviceId, out var service);
            return service;
        }

        public async Task<List<ScriptableServiceDescriptor>> GetAllServiceDescriptorsAsync()
        {
            await InitializeAsync();
            return _descriptors.Values.ToList();
        }

        public async Task<bool> RegisterServiceAsync(IScriptableService service)
        {
            try
            {
                if (_services.ContainsKey(service.ServiceId))
                {
                    _logger.LogWarning($"[ScriptableServiceRegistry] Service already registered: {service.ServiceId}");
                    return false;
                }

                _services[service.ServiceId] = service;
                _descriptors[service.ServiceId] = await service.GetDescriptorAsync();

                _logger.LogInformation($"[ScriptableServiceRegistry] Registered service: {service.ServiceId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ScriptableServiceRegistry] Failed to register service {service.ServiceId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnregisterServiceAsync(string serviceId)
        {
            if (!_services.ContainsKey(serviceId))
            {
                return false;
            }

            try
            {
                var service = _services[serviceId];
                await service.CleanupAsync();

                _services.Remove(serviceId);
                _descriptors.Remove(serviceId);

                _logger.LogInformation($"[ScriptableServiceRegistry] Unregistered service: {serviceId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ScriptableServiceRegistry] Failed to unregister service {serviceId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Automatically discover all ScriptableServices
        /// </summary>
        private async Task DiscoverServicesAsync()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var serviceTypes = assembly.GetTypes()
                            .Where(type => type.IsClass && !type.IsAbstract)
                            .Where(type => typeof(IScriptableService).IsAssignableFrom(type))
                            .Where(type => type.GetCustomAttribute<ScriptableServiceAttribute>() != null);

                        foreach (var serviceType in serviceTypes)
                        {
                            try
                            {
                                var service = CreateServiceInstance(serviceType);
                                if (service != null)
                                {
                                    await RegisterServiceAsync(service);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"[ScriptableServiceRegistry] Failed to create service instance {serviceType.Name}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[ScriptableServiceRegistry] Failed to scan assembly {assembly.FullName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ScriptableServiceRegistry] Failed to discover services: {ex.Message}");
            }
        }

        /// <summary>
        /// Create service instance
        /// </summary>
        private IScriptableService? CreateServiceInstance(Type serviceType)
        {
            try
            {
                // Try to use dependency injection constructor
                var constructors = serviceType.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length);

                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    var args = new object[parameters.Length];
                    bool canCreate = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;

                        // Simple dependency injection - can be extended to more complex DI container
                        if (paramType == typeof(ILogger<>) || paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(ILogger<>))
                        {
                            // Create appropriate Logger
                            var loggerType = typeof(ILogger<>).MakeGenericType(serviceType);
                            args[i] = CreateLogger(loggerType);
                        }
                        else if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue!;
                        }
                        else
                        {
                            canCreate = false;
                            break;
                        }
                    }

                    if (canCreate)
                    {
                        return (IScriptableService)Activator.CreateInstance(serviceType, args)!;
                    }
                }

                // If no suitable constructor, try parameterless constructor
                return (IScriptableService)Activator.CreateInstance(serviceType)!;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ScriptableServiceRegistry] Failed to create instance of {serviceType.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create Logger instance
        /// </summary>
        private object CreateLogger(Type loggerType)
        {
            // Create generic ConsoleLogger instance
            var genericLoggerType = typeof(ConsoleLogger<>).MakeGenericType(loggerType.GetGenericArguments()[0]);
            return Activator.CreateInstance(genericLoggerType)!;
        }
    }

    /// <summary>
    /// Simple Console Logger implementation
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly string _categoryName;

        public ConsoleLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            Console.WriteLine($"[{logLevel}] [{_categoryName}] {message}");
            if (exception != null)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Generic Logger wrapper
    /// </summary>
    public class ConsoleLogger<T> : ILogger<T>
    {
        private readonly ILogger _logger;

        public ConsoleLogger() : this(typeof(T).Name) { }

        public ConsoleLogger(string categoryName)
        {
            _logger = new ConsoleLogger(categoryName);
        }

        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
#endif