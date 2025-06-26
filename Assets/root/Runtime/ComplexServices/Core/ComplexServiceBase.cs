using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.ComplexServices
{
    /// <summary>
    /// Complex service base class - Provides common functionality implementation
    /// </summary>
    public abstract class ComplexServiceBase : IComplexService
    {
        private IServiceContext? _currentContext;
        private bool _isInitialized = false;

        #region Abstract Properties - Must be implemented by subclasses

        public abstract string ServiceId { get; }
        public abstract string ServiceName { get; }
        public abstract string Description { get; }
        public virtual string Version => "1.0";
        public abstract ServiceCapabilities Capabilities { get; }

        #endregion

        #region Abstract Methods - Must be implemented by subclasses

        /// <summary>
        /// Execute service request - Subclasses implement specific logic
        /// </summary>
        public abstract Task<ServiceResult> ExecuteAsync(ServiceRequest request, IServiceContext context);

        /// <summary>
        /// Generate parameter descriptions - Subclasses implement parameter definitions
        /// </summary>
        protected abstract Task<Dictionary<string, ParameterDescriptor>> GenerateParametersAsync();

        #endregion

        #region Virtual Methods - Subclasses can override

        /// <summary>
        /// Initialize service - Subclasses can override
        /// </summary>
        public virtual async Task InitializeAsync(IServiceContext context)
        {
            _currentContext = context;
            _isInitialized = true;
            
            await LogAsync($"Service {ServiceId} initialized successfully");
        }

        /// <summary>
        /// Cleanup service resources - Subclasses can override
        /// </summary>
        public virtual async Task CleanupAsync()
        {
            _isInitialized = false;
            _currentContext = null;
            
            await LogAsync($"Service {ServiceId} cleaned up");
        }

        /// <summary>
        /// Validate request - Subclasses can override
        /// </summary>
        protected virtual async Task<ValidationResult> ValidateRequestAsync(ServiceRequest request)
        {
            if (request == null)
                return ValidationResult.Invalid("Request cannot be null");

            if (string.IsNullOrEmpty(request.ServiceId))
                return ValidationResult.Invalid("ServiceId is required");

            if (request.ServiceId != ServiceId)
                return ValidationResult.Invalid($"ServiceId mismatch: expected {ServiceId}, got {request.ServiceId}");

            return await Task.FromResult(ValidationResult.Valid());
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get service descriptor
        /// </summary>
        public async Task<ServiceDescriptor> GetDescriptorAsync()
        {
            var parameters = await GenerateParametersAsync();
            
            return new ServiceDescriptor
            {
                ServiceId = ServiceId,
                ServiceName = ServiceName,
                Description = Description,
                Version = Version,
                Capabilities = Capabilities,
                Parameters = parameters
            };
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// Request model processing
        /// </summary>
        protected async Task<T> RequestModelAsync<T>(ModelRequest request)
        {
            if (_currentContext?.ModelUse == null)
                throw new InvalidOperationException("ModelUse service not available");

            return await _currentContext.ModelUse.RequestAsync<T>(request);
        }

        /// <summary>
        /// Store memory data
        /// </summary>
        protected async Task StoreMemoryAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            if (_currentContext?.Memory == null)
                throw new InvalidOperationException("Memory manager not available");

            await _currentContext.Memory.StoreAsync(_currentContext.SessionId, key, value, expiry);
        }

        /// <summary>
        /// Retrieve memory data
        /// </summary>
        protected async Task<T?> RetrieveMemoryAsync<T>(string key)
        {
            if (_currentContext?.Memory == null)
                return default(T);

            return await _currentContext.Memory.RetrieveAsync<T>(_currentContext.SessionId, key);
        }

        /// <summary>
        /// Check if memory data exists
        /// </summary>
        protected async Task<bool> ExistsInMemoryAsync(string key)
        {
            if (_currentContext?.Memory == null)
                return false;

            return await _currentContext.Memory.ExistsAsync(_currentContext.SessionId, key);
        }

        /// <summary>
        /// Remove memory data
        /// </summary>
        protected async Task RemoveFromMemoryAsync(string key)
        {
            if (_currentContext?.Memory == null)
                return;

            await _currentContext.Memory.RemoveAsync(_currentContext.SessionId, key);
        }

        /// <summary>
        /// Log message
        /// </summary>
        protected async Task LogAsync(string message, LogType logType = LogType.Log)
        {
            var logMessage = $"[{ServiceId}] {message}";
            
            switch (logType)
            {
                case LogType.Error:
                    Debug.LogError(logMessage);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(logMessage);
                    break;
                case LogType.Log:
                default:
                    Debug.Log(logMessage);
                    break;
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Handle exception
        /// </summary>
        protected async Task<ServiceResult> HandleExceptionAsync(Exception ex, string operation)
        {
            var errorMessage = $"Error in {operation}: {ex.Message}";
            await LogAsync(errorMessage, LogType.Error);
            
            return ServiceResult.Error(errorMessage, new Dictionary<string, object>
            {
                ["operation"] = operation,
                ["exceptionType"] = ex.GetType().Name,
                ["stackTrace"] = ex.StackTrace ?? string.Empty
            });
        }

        /// <summary>
        /// Get Unity context
        /// </summary>
        protected UnityContext GetUnityContext()
        {
            if (_currentContext?.Unity == null)
                throw new InvalidOperationException("Unity context not available");

            return _currentContext.Unity;
        }

        /// <summary>
        /// Get current active scene
        /// </summary>
        protected UnityEngine.SceneManagement.Scene GetActiveScene()
        {
            return GetUnityContext().ActiveScene;
        }

        /// <summary>
        /// Get main camera
        /// </summary>
        protected Camera GetMainCamera()
        {
            var camera = GetUnityContext().MainCamera;
            if (camera == null)
                throw new InvalidOperationException("Main camera not available");
            
            return camera;
        }

        /// <summary>
        /// Get selected game objects
        /// </summary>
        protected GameObject[] GetSelectedGameObjects()
        {
            return GetUnityContext().SelectedGameObjects;
        }

        /// <summary>
        /// Check if in editor mode
        /// </summary>
        protected bool IsInEditor()
        {
            return GetUnityContext().IsInEditor;
        }

        /// <summary>
        /// Check if in play mode
        /// </summary>
        protected bool IsPlaying()
        {
            return GetUnityContext().IsPlaying;
        }

        /// <summary>
        /// Validate result
        /// </summary>
        protected async Task<ValidationResult> ValidateResultAsync(object result)
        {
            return await Task.FromResult(ValidationResult.Valid());
        }

        #endregion
    }

    /// <summary>
    /// Validate result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        public static ValidationResult Valid() => new() { IsValid = true };
        public static ValidationResult Invalid(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
    }
}
