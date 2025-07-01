#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using com.MiAO.Unity.MCP.Server.RpcGateway.Core;
using com.MiAO.Unity.MCP.Server.RpcGateway.Unity;
using com.MiAO.Unity.MCP.Server.RpcGateway.External;
using com.MiAO.Unity.MCP.Server.WorkflowOrchestration.Core;
using com.MiAO.Unity.MCP.Server.McpToolAdapter;
using ModelContextProtocol.Protocol;

namespace com.MiAO.Unity.MCP.Server.Handlers
{
    /// <summary>
    /// Workflow Handler - Replaces ComplexServiceHandler to implement true Middleware architecture
    /// This class serves as the primary workflow orchestration controller, managing the execution
    /// of complex multi-step workflows that can interact with Unity Runtime, external services,
    /// and AI models through a unified interface.
    /// </summary>
    public class WorkflowHandler
    {
        private readonly ILogger<WorkflowHandler> _logger;
        private readonly SimpleWorkflowEngine _workflowEngine;
        private readonly WorkflowToolAdapter _toolAdapter;
        private readonly Dictionary<string, IRpcGateway> _gateways = new();
        private bool _isInitialized = false;

        public WorkflowHandler(ILogger<WorkflowHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Create the workflow engine instance with proper logging configuration
            var engineLogger = logger as ILogger<SimpleWorkflowEngine> ??
                              Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                                  .CreateLogger<SimpleWorkflowEngine>();
            _workflowEngine = new SimpleWorkflowEngine(engineLogger);

            // Create the tool adapter instance that bridges workflows to MCP tools
            var adapterLogger = logger as ILogger<WorkflowToolAdapter> ??
                               Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                                   .CreateLogger<WorkflowToolAdapter>();
            _toolAdapter = new WorkflowToolAdapter(adapterLogger, _workflowEngine);
        }

        /// <summary>
        /// Initialize the workflow handler by setting up RPC gateways and loading workflow definitions
        /// This method establishes connections to Unity Runtime and external services, then loads
        /// all available workflow definitions from the configuration directory.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                _logger.LogInformation("[WorkflowHandler] Initializing...");

                // Initialize all RPC gateways for communication with external systems
                await InitializeGatewaysAsync();

                // Initialize the tool adapter which loads workflow definitions from disk
                await _toolAdapter.InitializeAsync();

                _isInitialized = true;
                _logger.LogInformation("[WorkflowHandler] Initialization complete");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorkflowHandler] Initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get all available workflow tools for MCP tool listing
        /// This method returns workflow definitions converted to MCP Tool format,
        /// making them discoverable and callable through the MCP protocol.
        /// </summary>
        public async Task<List<Tool>> GetAvailableWorkflowToolsAsync()
        {
            await EnsureInitializedAsync();
            return await _toolAdapter.GetAvailableWorkflowToolsAsync();
        }

        /// <summary>
        /// Execute a workflow as an MCP tool call
        /// This method handles the execution of complex workflows triggered through MCP,
        /// managing parameter validation, session handling, and result formatting.
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow to execute</param>
        /// <param name="arguments">Input arguments passed from the MCP client</param>
        /// <param name="sessionId">Optional session identifier for maintaining context</param>
        public async Task<CallToolResponse> ExecuteWorkflowAsync(string workflowId, Dictionary<string, JsonElement> arguments, string? sessionId = null)
        {
            await EnsureInitializedAsync();
            return await _toolAdapter.ExecuteWorkflowAsync(workflowId, arguments, sessionId);
        }

        /// <summary>
        /// Check if a specific workflow exists in the system
        /// Useful for validation before attempting workflow execution
        /// </summary>
        public async Task<bool> HasWorkflowAsync(string workflowId)
        {
            await EnsureInitializedAsync();
            var workflow = await _workflowEngine.GetWorkflowAsync(workflowId);
            return workflow != null;
        }

        /// <summary>
        /// Get comprehensive workflow information for debugging and monitoring
        /// Returns detailed information about loaded workflows, gateway states,
        /// and system status in JSON format for diagnostic purposes.
        /// </summary>
        public async Task<string> GetWorkflowInfoAsync()
        {
            await EnsureInitializedAsync();

            var workflows = await _workflowEngine.GetAvailableWorkflowsAsync();
            var gatewayInfo = new Dictionary<string, object>();

            // Collect status information from all registered gateways
            foreach (var kvp in _gateways)
            {
                var gateway = kvp.Value;
                gatewayInfo[kvp.Key] = new
                {
                    gatewayId = gateway.GatewayId,
                    isConnected = await gateway.IsConnectedAsync(),
                    toolCount = (await gateway.DiscoverToolsAsync()).Length
                };
            }

            var info = new
            {
                workflowCount = workflows.Count,
                workflows = workflows.Select(w => new
                {
                    id = w.Id,
                    name = w.Name,
                    description = w.Description,
                    stepCount = w.Steps.Count,
                    author = w.Author,
                    version = w.Version
                }).ToArray(),
                gateways = gatewayInfo,
                initialized = _isInitialized
            };

            return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Direct Unity tool invocation for backward compatibility
        /// Provides a simplified interface for calling Unity tools directly
        /// without going through the workflow orchestration system.
        /// </summary>
        public async Task<string> CallUnityToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            await EnsureInitializedAsync();

            if (_gateways.TryGetValue("unity", out var unityGateway))
            {
                try
                {
                    var result = await unityGateway.CallAsync<string>(toolName, parameters);
                    return result ?? "";
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[WorkflowHandler] Error calling Unity tool {toolName}: {ex.Message}");
                    return $"Error: {ex.Message}";
                }
            }

            return "Error: Unity gateway not available";
        }

        /// <summary>
        /// Direct ModelUse invocation for backward compatibility
        /// Provides a simplified interface for calling AI model services directly
        /// without going through the workflow orchestration system.
        /// </summary>
        public async Task<string> CallModelUseAsync(string operation, Dictionary<string, object> parameters)
        {
            await EnsureInitializedAsync();

            if (_gateways.TryGetValue("model_use", out var modelUseGateway))
            {
                try
                {
                    var result = await modelUseGateway.CallAsync<string>(operation, parameters);
                    return result ?? "";
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[WorkflowHandler] Error calling ModelUse {operation}: {ex.Message}");
                    return $"Error: {ex.Message}";
                }
            }

            return "Error: ModelUse gateway not available";
        }

        /// <summary>
        /// Initialize all RPC gateways for external system communication
        /// Sets up connections to Unity Runtime, AI model services, and other external systems
        /// that workflows may need to interact with during execution.
        /// </summary>
        private async Task InitializeGatewaysAsync()
        {
            try
            {
                // Create Unity RPC gateway for Unity Runtime communication
                var unityLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<UnityRpcGateway>();
                var unityGateway = new UnityRpcGateway(unityLogger);
                _gateways["unity"] = unityGateway;
                _workflowEngine.RegisterGateway(unityGateway);

                // Create ModelUse RPC gateway for AI model service communication
                var modelUseLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<ModelUseRpcGateway>();
                var modelUseGateway = new ModelUseRpcGateway(modelUseLogger);
                _gateways["model_use"] = modelUseGateway;
                _workflowEngine.RegisterGateway(modelUseGateway);

                _logger.LogInformation("[WorkflowHandler] RPC gateways initialized");

                // Test connectivity status of all gateways
                foreach (var kvp in _gateways)
                {
                    var isConnected = await kvp.Value.IsConnectedAsync();
                    _logger.LogInformation($"[WorkflowHandler] Gateway '{kvp.Key}' connection status: {isConnected}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorkflowHandler] Error initializing gateways: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensure the handler is properly initialized before any operations
        /// Lazy initialization pattern to avoid blocking constructor calls
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        /// <summary>
        /// Clean up resources and dispose of the handler
        /// Properly releases all gateway connections and clears internal state
        /// </summary>
        public async Task DisposeAsync()
        {
            _logger.LogInformation("[WorkflowHandler] Disposing...");
            _gateways.Clear();
            _isInitialized = false;
            await Task.CompletedTask;
        }
    }
}
#endif