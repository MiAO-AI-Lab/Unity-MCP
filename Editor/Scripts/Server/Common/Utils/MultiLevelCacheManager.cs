#if !UNITY_5_3_OR_NEWER
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace com.MiAO.MCP.Server.Utils
{
    /// <summary>
    /// Multi-Level Cache Manager - Universal request/operation caching strategy
    /// Provides multi-level caching strategies based on time intervals, call counts, and custom change detection,
    /// reducing the performance impact of high-frequency operations (file I/O, network requests, etc.).
    /// </summary>
    /// <typeparam name="T">Type of cached data</typeparam>
    public class MultiLevelCacheManager<T>
    {
        private readonly ILogger _logger;
        private readonly string _cacheId;

        // Cache data and state
        private T? _cachedData;
        private DateTime _lastLoadTime = DateTime.MinValue;
        private int _callsSinceLastLoad = 0;
        private DateTime _lastChangeCheckTime = DateTime.MinValue;

        // Cache strategy configuration
        private readonly MultiLevelCacheConfig _config;

        // Change detection delegate
        private readonly Func<Task<bool>>? _changeDetector;

        /// <summary>
        /// Data loading delegate
        /// </summary>
        private readonly Func<Task<T>> _dataLoader;

        public MultiLevelCacheManager(
            ILogger logger,
            string cacheId,
            Func<Task<T>> dataLoader,
            MultiLevelCacheConfig? config = null,
            Func<Task<bool>>? changeDetector = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheId = cacheId ?? throw new ArgumentNullException(nameof(cacheId));
            _dataLoader = dataLoader ?? throw new ArgumentNullException(nameof(dataLoader));
            _config = config ?? MultiLevelCacheConfig.Default;
            _changeDetector = changeDetector;
        }

        /// <summary>
        /// Get data using intelligent caching strategy
        /// </summary>
        public async Task<T> GetDataAsync()
        {
            // Increment call counter
            _callsSinceLastLoad++;

            _logger.LogTrace($"[{_cacheId}] GetDataAsync called (calls since last load: {_callsSinceLastLoad})");

            // Check if we need to reload
            if (await ShouldReloadAsync())
            {
                await ReloadDataAsync();
            }

            return _cachedData!;
        }

        /// <summary>
        /// Force reload data, bypassing all caching strategies
        /// </summary>
        public async Task ForceReloadAsync()
        {
            _logger.LogTrace($"[{_cacheId}] Force reloading data");
            await ReloadDataAsync();
        }

        /// <summary>
        /// Notify cache manager that external changes have occurred
        /// This will trigger a reload on the next data access
        /// </summary>
        public void NotifyDataChanged()
        {
            _logger.LogTrace($"[{_cacheId}] External data change notification received");
            // Reset the last load time to force reload on next access
            _lastLoadTime = DateTime.MinValue;
            _callsSinceLastLoad = 0;
        }

        /// <summary>
        /// Check if there is cached data available
        /// </summary>
        public bool HasCachedData => _cachedData != null;

        /// <summary>
        /// Get cache statistics for monitoring and debugging
        /// </summary>
        public CacheStats GetStats()
        {
            var now = DateTime.Now;
            var effectiveConfig = GetEffectiveConfig();

            return new CacheStats
            {
                CacheId = _cacheId,
                LastLoadTime = _lastLoadTime == DateTime.MinValue ? null : _lastLoadTime,
                CallsSinceLastLoad = _callsSinceLastLoad,
                TimeSinceLastLoad = _lastLoadTime == DateTime.MinValue ? null : now - _lastLoadTime,
                LastChangeCheckTime = _lastChangeCheckTime == DateTime.MinValue ? null : _lastChangeCheckTime,
                HasCachedData = HasCachedData,
                Configuration = effectiveConfig,
                NextReloadTriggers = new CacheReloadTriggers
                {
                    CallsRemaining = Math.Max(0, effectiveConfig.MaxCallsBeforeReload - _callsSinceLastLoad),
                    TimeUntilNextCheck = _lastLoadTime == DateTime.MinValue ? "On next call" :
                        Math.Max(0, (effectiveConfig.MinReloadInterval - (now - _lastLoadTime)).TotalSeconds).ToString("F1") + " seconds"
                }
            };
        }

        /// <summary>
        /// Determine if data should be reloaded based on multiple criteria
        /// </summary>
        private async Task<bool> ShouldReloadAsync()
        {
            var now = DateTime.Now;
            var effectiveConfig = GetEffectiveConfig();

            // First load - always reload
            if (_lastLoadTime == DateTime.MinValue || _cachedData == null)
            {
                _logger.LogTrace($"[{_cacheId}] First load - reloading data");
                return true;
            }

            // Force reload if call count exceeds limit
            if (_callsSinceLastLoad >= effectiveConfig.MaxCallsBeforeReload)
            {
                _logger.LogTrace($"[{_cacheId}] Max calls ({effectiveConfig.MaxCallsBeforeReload}) reached - forcing reload");
                return true;
            }

            // Don't reload if minimum time interval hasn't passed
            if (now - _lastLoadTime < effectiveConfig.MinReloadInterval)
            {
                _logger.LogTrace($"[{_cacheId}] Min reload interval ({effectiveConfig.MinReloadInterval}) not reached - skipping reload");
                return false;
            }

            // Check for changes (if change detector is provided)
            if (_changeDetector != null &&
                now - _lastChangeCheckTime >= effectiveConfig.ChangeCheckInterval)
            {
                _lastChangeCheckTime = now;

                try
                {
                    var hasChanges = await _changeDetector();
                    if (hasChanges)
                    {
                        _logger.LogTrace($"[{_cacheId}] Changes detected - reloading data");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[{_cacheId}] Error in change detection: {ex.Message}");
                    // If change detection fails, reload for safety
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reload data from the data loader
        /// </summary>
        private async Task ReloadDataAsync()
        {
            try
            {
                _logger.LogTrace($"[{_cacheId}] Reloading data...");

                _cachedData = await _dataLoader();
                _lastLoadTime = DateTime.Now;
                _callsSinceLastLoad = 0;

                _logger.LogTrace($"[{_cacheId}] Data reloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{_cacheId}] Error reloading data: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get effective configuration (supports runtime overrides)
        /// </summary>
        private MultiLevelCacheConfig GetEffectiveConfig()
        {
            return new MultiLevelCacheConfig
            {
                MinReloadInterval = _config.MinReloadIntervalOverride > TimeSpan.Zero ?
                    _config.MinReloadIntervalOverride : _config.MinReloadInterval,
                MaxCallsBeforeReload = _config.MaxCallsBeforeReloadOverride > 0 ?
                    _config.MaxCallsBeforeReloadOverride : _config.MaxCallsBeforeReload,
                ChangeCheckInterval = _config.ChangeCheckIntervalOverride > TimeSpan.Zero ?
                    _config.ChangeCheckIntervalOverride : _config.ChangeCheckInterval
            };
        }

        /// <summary>
        /// Update configuration at runtime
        /// </summary>
        public void UpdateConfig(Action<MultiLevelCacheConfig> configUpdater)
        {
            configUpdater(_config);
            _logger.LogTrace($"[{_cacheId}] Configuration updated");
        }
    }

    /// <summary>
    /// Multi-level cache configuration with various caching strategies
    /// </summary>
    public class MultiLevelCacheConfig
    {
        /// <summary>
        /// Minimum reload interval (default: 5 minutes)
        /// </summary>
        public TimeSpan MinReloadInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum calls before forcing reload (default: 100 calls)
        /// </summary>
        public int MaxCallsBeforeReload { get; set; } = 100;

        /// <summary>
        /// Change detection check interval (default: 1 minute)
        /// </summary>
        public TimeSpan ChangeCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

        // Runtime override configuration
        public TimeSpan MinReloadIntervalOverride { get; set; } = TimeSpan.Zero;
        public int MaxCallsBeforeReloadOverride { get; set; } = 0;
        public TimeSpan ChangeCheckIntervalOverride { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Default configuration with balanced caching strategy
        /// </summary>
        public static MultiLevelCacheConfig Default => new MultiLevelCacheConfig();

        /// <summary>
        /// High frequency access configuration (more aggressive caching strategy)
        /// </summary>
        public static MultiLevelCacheConfig HighFrequency => new MultiLevelCacheConfig
        {
            MinReloadInterval = TimeSpan.FromMinutes(10),
            MaxCallsBeforeReload = 200,
            ChangeCheckInterval = TimeSpan.FromMinutes(2)
        };

        /// <summary>
        /// Development mode configuration (more frequent checks for debugging)
        /// </summary>
        public static MultiLevelCacheConfig Development => new MultiLevelCacheConfig
        {
            MinReloadInterval = TimeSpan.FromMinutes(1),
            MaxCallsBeforeReload = 20,
            ChangeCheckInterval = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Cache statistics information for monitoring and debugging
    /// </summary>
    public class CacheStats
    {
        public string CacheId { get; set; } = string.Empty;
        public DateTime? LastLoadTime { get; set; }
        public int CallsSinceLastLoad { get; set; }
        public TimeSpan? TimeSinceLastLoad { get; set; }
        public DateTime? LastChangeCheckTime { get; set; }
        public bool HasCachedData { get; set; }
        public MultiLevelCacheConfig Configuration { get; set; } = new();
        public CacheReloadTriggers NextReloadTriggers { get; set; } = new();

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Information about next reload triggers
    /// </summary>
    public class CacheReloadTriggers
    {
        public int CallsRemaining { get; set; }
        public string TimeUntilNextCheck { get; set; } = string.Empty;
    }
}
#endif