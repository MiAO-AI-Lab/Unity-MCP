using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.IvanMurzak.Unity.MCP.ComplexServices
{
    /// <summary>
    /// Service context implementation
    /// </summary>
    public class ServiceContext : IServiceContext
    {
        private readonly Dictionary<string, object> _properties = new();

        public string SessionId { get; }
        public string AgentId { get; }
        public UnityContext Unity { get; }
        public IUnityMemoryManager Memory { get; }
        public IModelUseService ModelUse { get; }

        public ServiceContext(
            string sessionId,
            string agentId,
            UnityContext unityContext,
            IUnityMemoryManager memoryManager,
            IModelUseService modelUseService)
        {
            SessionId = sessionId;
            AgentId = agentId;
            Unity = unityContext;
            Memory = memoryManager;
            ModelUse = modelUseService;
        }

        public async Task<T?> GetPropertyAsync<T>(string key)
        {
            if (_properties.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T directValue)
                        return directValue;
                    
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default(T);
                }
            }
            
            return await Task.FromResult(default(T));
        }

        public async Task SetPropertyAsync<T>(string key, T value)
        {
            _properties[key] = value!;
            await Task.CompletedTask;
        }

        public IServiceContext CreateChildContext(string childSessionId)
        {
            return new ServiceContext(
                childSessionId,
                AgentId,
                Unity,
                Memory,
                ModelUse);
        }
    }

    /// <summary>
    /// Service context factory
    /// </summary>
    public static class ServiceContextFactory
    {
        /// <summary>
        /// Create service context
        /// </summary>
        public static IServiceContext Create(
            string sessionId,
            string agentId,
            IUnityMemoryManager? memoryManager = null,
            IModelUseService? modelUseService = null)
        {
            // Create Unity context
            var unityContext = CreateUnityContext();

            // Use default implementation if not provided
            memoryManager ??= new UnityMemoryManager();
            modelUseService ??= new ModelUseService();

            return new ServiceContext(
                sessionId,
                agentId,
                unityContext,
                memoryManager,
                modelUseService);
        }

        /// <summary>
        /// Create Unity context
        /// </summary>
        private static UnityContext CreateUnityContext()
        {
            var context = new UnityContext();

            try
            {
                // Get current active scene
                context.ActiveScene = SceneManager.GetActiveScene();

                // Get main camera
                context.MainCamera = Camera.main;

                // Check if in editor mode
#if UNITY_EDITOR
                context.IsInEditor = true;
                context.IsPlaying = UnityEditor.EditorApplication.isPlaying;
                
                // Get selected game objects
                var selectedObjects = UnityEditor.Selection.gameObjects;
                context.SelectedGameObjects = selectedObjects ?? Array.Empty<GameObject>();
#else
                context.IsInEditor = false;
                context.IsPlaying = Application.isPlaying;
                context.SelectedGameObjects = Array.Empty<GameObject>();
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServiceContext] Failed to create Unity context: {ex.Message}");
            }

            return context;
        }
    }

    /// <summary>
    /// Unity memory manager implementation
    /// </summary>
    public class UnityMemoryManager : IUnityMemoryManager
    {
        private readonly Dictionary<string, Dictionary<string, CacheEntry>> _cache = new();
        private readonly object _lock = new();

        public async Task StoreAsync<T>(string sessionId, string key, T value, TimeSpan? expiry = null)
        {
            lock (_lock)
            {
                if (!_cache.ContainsKey(sessionId))
                {
                    _cache[sessionId] = new Dictionary<string, CacheEntry>();
                }

                var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : DateTime.MaxValue;
                _cache[sessionId][key] = new CacheEntry
                {
                    Value = value!,
                    ExpiryTime = expiryTime
                };
            }

            await Task.CompletedTask;
        }

        public async Task<T?> RetrieveAsync<T>(string sessionId, string key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(sessionId, out var sessionCache) &&
                    sessionCache.TryGetValue(key, out var entry))
                {
                    if (DateTime.UtcNow <= entry.ExpiryTime)
                    {
                        try
                        {
                            if (entry.Value is T directValue)
                                return directValue;
                            
                            return (T)Convert.ChangeType(entry.Value, typeof(T));
                        }
                        catch
                        {
                            return default(T);
                        }
                    }
                    else
                    {
                        // Expired, remove
                        sessionCache.Remove(key);
                    }
                }
            }

            return await Task.FromResult(default(T));
        }

        public async Task<bool> ExistsAsync(string sessionId, string key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(sessionId, out var sessionCache) &&
                    sessionCache.TryGetValue(key, out var entry))
                {
                    if (DateTime.UtcNow <= entry.ExpiryTime)
                    {
                        return true;
                    }
                    else
                    {
                        // Expired, remove
                        sessionCache.Remove(key);
                    }
                }
            }

            return await Task.FromResult(false);
        }

        public async Task RemoveAsync(string sessionId, string key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(sessionId, out var sessionCache))
                {
                    sessionCache.Remove(key);
                }
            }

            await Task.CompletedTask;
        }

        public async Task ClearSessionAsync(string sessionId)
        {
            lock (_lock)
            {
                _cache.Remove(sessionId);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Cleanup expired entries
        /// </summary>
        public void CleanupExpiredEntries()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var sessionsToRemove = new List<string>();

                foreach (var sessionKv in _cache)
                {
                    var keysToRemove = new List<string>();
                    
                    foreach (var entryKv in sessionKv.Value)
                    {
                        if (now > entryKv.Value.ExpiryTime)
                        {
                            keysToRemove.Add(entryKv.Key);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        sessionKv.Value.Remove(key);
                    }

                    if (sessionKv.Value.Count == 0)
                    {
                        sessionsToRemove.Add(sessionKv.Key);
                    }
                }

                foreach (var sessionId in sessionsToRemove)
                {
                    _cache.Remove(sessionId);
                }
            }
        }

        private class CacheEntry
        {
            public object Value { get; set; } = null!;
            public DateTime ExpiryTime { get; set; }
        }
    }

    /// <summary>
    /// Model use service implementation (temporary implementation, will be improved in McpServer)
    /// </summary>
    public class ModelUseService : IModelUseService
    {
        public async Task<T> RequestAsync<T>(ModelRequest request)
        {
            // Temporary implementation - will be improved in McpServer protocol extension
            await Task.Delay(100);

            // Mock response
            return (T)(object)"Mock model response";
        }
    }
}
