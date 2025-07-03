#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using com.MiAO.Unity.MCP.Server.WorkflowOrchestration.Core;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Server.McpToolAdapter
{
    /// <summary>
    /// Workflow Tool Adapter - Converts workflow definitions into MCP tools
    /// This adapter bridges the gap between the workflow orchestration system and the MCP protocol,
    /// enabling complex workflows to be exposed as discoverable and callable MCP tools.
    /// It handles parameter validation, execution coordination, and result formatting.
    /// </summary>
    public class WorkflowToolAdapter
    {
        private readonly ILogger<WorkflowToolAdapter> _logger;
        private readonly IWorkflowEngine _workflowEngine;
        private readonly Dictionary<string, Guid> _sessionMapping = new();

        // Cache management for workflow loading optimization
        private DateTime _lastLoadTime = DateTime.MinValue;
        private int _callsSinceLastLoad = 0;
        private readonly Dictionary<string, DateTime> _fileModificationTimes = new();

        // Configuration constants for cache management
        private static readonly TimeSpan MinReloadInterval = TimeSpan.FromMinutes(5);
        private static readonly int MaxCallsBeforeReload = 100;
        private static readonly TimeSpan FileCheckInterval = TimeSpan.FromMinutes(1);
        private DateTime _lastFileCheckTime = DateTime.MinValue;

        // Configuration properties for runtime adjustment
        public TimeSpan MinReloadIntervalOverride { get; set; } = TimeSpan.Zero;
        public int MaxCallsBeforeReloadOverride { get; set; } = 0;
        public TimeSpan FileCheckIntervalOverride { get; set; } = TimeSpan.Zero;

        public WorkflowToolAdapter(ILogger<WorkflowToolAdapter> logger, IWorkflowEngine workflowEngine)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        }

        /// <summary>
        /// Initialize the adapter by loading built-in workflow definitions
        /// Scans the configuration directory for workflow JSON files and registers them
        /// with the workflow engine for execution.
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadBuiltinWorkflowsAsync();
            _logger.Trace("[WorkflowToolAdapter] Initialized");
        }

        /// <summary>
        /// Get all available workflows as MCP tools
        /// Converts workflow definitions into MCP Tool objects that can be discovered
        /// and invoked through the MCP protocol. Each workflow becomes a callable tool
        /// with appropriate parameter schemas and descriptions.
        /// Uses intelligent caching to avoid excessive file I/O operations.
        /// </summary>
        public async Task<List<Tool>> GetAvailableWorkflowToolsAsync()
        {
            try
            {
                // Increment call counter
                _callsSinceLastLoad++;

                // Check if we need to reload workflows based on various criteria
                if (await ShouldReloadWorkflowsAsync())
                {
                    await LoadBuiltinWorkflowsAsync();
                    _lastLoadTime = DateTime.Now;
                    _callsSinceLastLoad = 0;
                    _logger.LogTrace($"[WorkflowToolAdapter] Reloaded workflows from disk");
                }

                var workflows = await _workflowEngine.GetAvailableWorkflowsAsync();
                var tools = new List<Tool>();

                foreach (var workflow in workflows)
                {
                    var tool = CreateWorkflowTool(workflow);
                    tools.Add(tool);
                }

                _logger.LogDebug($"[WorkflowToolAdapter] Generated {tools.Count} workflow tools (calls since last load: {_callsSinceLastLoad})");
                return tools;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorkflowToolAdapter] Error getting workflow tools: {ex.Message}");
                return new List<Tool>();
            }
        }

        /// <summary>
        /// Execute a workflow as an MCP tool call
        /// Handles the complete lifecycle of workflow execution including parameter conversion,
        /// validation, session management, and result formatting for MCP protocol compliance.
        /// </summary>
        public async Task<CallToolResponse> ExecuteWorkflowAsync(string workflowId, Dictionary<string, JsonElement> arguments, string? sessionId = null)
        {
            try
            {
                _logger.LogTrace($"[WorkflowToolAdapter] Executing workflow: {workflowId}");

                // Retrieve the workflow definition from the engine
                var workflow = await _workflowEngine.GetWorkflowAsync(workflowId);
                if (workflow == null)
                {
                    return CreateErrorResponse($"Workflow not found: {workflowId}");
                }

                // Convert JSON parameters to strongly-typed objects
                var parameters = ConvertJsonElementsToObjects(arguments);

                // Validate parameters against workflow schema
                var validationResult = ValidateParameters(workflow, parameters);
                if (!validationResult.IsValid)
                {
                    return CreateErrorResponse($"Parameter validation failed: {validationResult.ErrorMessage}");
                }

                // Create or use existing session identifier
                var actualSessionId = sessionId ?? Guid.NewGuid().ToString();

                // Create data flow context for workflow execution
                var context = new SimpleDataFlowContext(actualSessionId, parameters);

                // Execute the workflow through the engine
                var result = await _workflowEngine.ExecuteWorkflowAsync(workflow, context);

                // Convert workflow result to MCP response format
                return CreateSuccessResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorkflowToolAdapter] Error executing workflow {workflowId}: {ex.Message}");
                return CreateErrorResponse($"Workflow execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert workflow definition to MCP tool representation
        /// Creates a proper MCP Tool object with JSON schema for parameters,
        /// making the workflow discoverable and callable through MCP clients.
        /// </summary>
        private Tool CreateWorkflowTool(WorkflowDefinition workflow)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            // Add parameters from workflow definition
            if (workflow.Parameters != null)
            {
                foreach (var param in workflow.Parameters)
                {
                    properties[param.Name] = new
                    {
                        type = ConvertParameterType(param.Type),
                        description = param.Description
                    };
                    if (param.Required)
                    {
                        required.Add(param.Name);
                    }
                }
            }

            // Create schema as JsonElement properly
            var schemaObject = new
            {
                type = "object",
                properties = properties,
                required = required.ToArray()
            };

            var schemaJson = JsonSerializer.Serialize(schemaObject);
            using var document = JsonDocument.Parse(schemaJson);
            var inputSchema = document.RootElement.Clone();

            return new Tool
            {
                Name = $"workflow_{workflow.Id}",
                Description = workflow.Description ?? $"Execute workflow: {workflow.Id}",
                InputSchema = inputSchema
            };
        }

        /// <summary>
        /// Convert workflow parameter types to JSON schema types
        /// Maps workflow-specific type names to standard JSON schema type definitions
        /// for proper MCP protocol compliance.
        /// </summary>
        private string ConvertParameterType(string workflowType)
        {
            return workflowType.ToLower() switch
            {
                "string" => "string",
                "int" or "integer" => "number",
                "float" or "double" => "number",
                "bool" or "boolean" => "boolean",
                "array" => "array",
                "object" => "object",
                _ => "string"
            };
        }

        /// <summary>
        /// Convert JsonElement arguments to plain objects
        /// Transforms MCP protocol JsonElement parameters into strongly-typed objects
        /// that can be processed by the workflow engine.
        /// </summary>
        private Dictionary<string, object> ConvertJsonElementsToObjects(Dictionary<string, JsonElement> arguments)
        {
            var result = new Dictionary<string, object>();

            foreach (var kvp in arguments)
            {
                result[kvp.Key] = ConvertJsonElement(kvp.Value);
            }

            return result;
        }

        private object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(prop => prop.Name, prop => ConvertJsonElement(prop.Value)),
                JsonValueKind.Null => null!,
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Validate parameters against workflow schema
        /// Checks if all required parameters are present and their types are valid
        /// against the workflow definition.
        /// </summary>
        private (bool IsValid, string? ErrorMessage) ValidateParameters(WorkflowDefinition workflow, Dictionary<string, object> parameters)
        {
            foreach (var param in workflow.Parameters)
            {
                if (param.Required && !parameters.ContainsKey(param.Name))
                {
                    return (false, $"Required parameter missing: {param.Name}");
                }

                if (parameters.TryGetValue(param.Name, out var value))
                {
                    // Simple type validation against the expected parameter type
                    if (!ValidateParameterType(value, param.Type))
                    {
                        return (false, $"Parameter '{param.Name}' has invalid type. Expected: {param.Type}");
                    }
                }
            }

            return (true, null);
        }

        private bool ValidateParameterType(object value, string expectedType)
        {
            if (value == null) return true;

            return expectedType.ToLower() switch
            {
                "string" => value is string,
                "int" or "integer" => value is int or long,
                "float" or "double" => value is float or double or int,
                "bool" or "boolean" => value is bool,
                "array" => value is Array or System.Collections.IEnumerable,
                "object" => value is Dictionary<string, object> or object,
                _ => true // Default allow any type for forward compatibility
            };
        }

        /// <summary>
        /// Create a successful workflow execution response
        /// Formats the result of a successful workflow execution into a MCP response format
        /// with detailed execution information and step results.
        /// </summary>
        private CallToolResponse CreateSuccessResponse(WorkflowResult result)
        {
            var content = new List<Content>();

            // Main result containing execution summary and outputs
            var mainResult = new
            {
                success = result.IsSuccess,
                executionTime = result.ExecutionTime.ToString(),
                outputs = result.Outputs,
                metadata = result.Metadata
            };

            content.Add(new Content
            {
                Type = "text",
                Text = JsonSerializer.Serialize(mainResult, JsonUtils.JsonSerializerOptions)
            });

            // If there's an error, add error information to the response
            if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                content.Add(new Content
                {
                    Type = "text",
                    Text = $"Error: {result.ErrorMessage}"
                });
            }

            // Step details for debugging and monitoring purposes
            if (result.StepResults.Any())
            {
                var stepDetails = result.StepResults.Select(s => new
                {
                    stepId = s.StepId,
                    success = s.IsSuccess,
                    executionTime = s.ExecutionTime.ToString(),
                    error = s.ErrorMessage
                }).ToArray();

                content.Add(new Content
                {
                    Type = "text",
                    Text = $"Step Details:\n{JsonSerializer.Serialize(stepDetails, JsonUtils.JsonSerializerOptions)}"
                });
            }

            return new CallToolResponse
            {
                Content = content,
                IsError = false
            };
        }

        /// <summary>
        /// Create an error response for workflow execution failures
        /// Formats an error message into a standardized MCP response format.
        /// </summary>
        private CallToolResponse CreateErrorResponse(string errorMessage)
        {
            return new CallToolResponse
            {
                Content = new List<Content>
                {
                    new Content
                    {
                        Type = "text",
                        Text = errorMessage
                    }
                },
                IsError = true
            };
        }

        /// <summary>
        /// Determine if workflows should be reloaded based on multiple criteria
        /// Checks time elapsed, call count, and file modification times to make an intelligent decision
        /// about whether to perform potentially expensive file I/O operations.
        /// </summary>
        private async Task<bool> ShouldReloadWorkflowsAsync()
        {
            var now = DateTime.Now;

            // Get effective configuration values (allow runtime overrides)
            var minReloadInterval = MinReloadIntervalOverride > TimeSpan.Zero ? MinReloadIntervalOverride : MinReloadInterval;
            var maxCallsBeforeReload = MaxCallsBeforeReloadOverride > 0 ? MaxCallsBeforeReloadOverride : MaxCallsBeforeReload;
            var fileCheckInterval = FileCheckIntervalOverride > TimeSpan.Zero ? FileCheckIntervalOverride : FileCheckInterval;

            // First load - always reload
            if (_lastLoadTime == DateTime.MinValue)
            {
                _logger.LogDebug($"[WorkflowToolAdapter] First load - reloading workflows");
                return true;
            }

            // Force reload if too many calls have occurred
            if (_callsSinceLastLoad >= maxCallsBeforeReload)
            {
                _logger.LogDebug($"[WorkflowToolAdapter] Max calls ({maxCallsBeforeReload}) reached - forcing reload");
                return true;
            }

            // Don't reload if minimum time interval hasn't passed
            if (now - _lastLoadTime < minReloadInterval)
            {
                _logger.LogDebug($"[WorkflowToolAdapter] Min reload interval ({minReloadInterval}) not reached - skipping reload");
                return false;
            }

            // Check file modifications only if enough time has passed since last check
            if (now - _lastFileCheckTime >= fileCheckInterval)
            {
                _lastFileCheckTime = now;

                var hasFileChanges = await HasWorkflowFilesChangedAsync();
                if (hasFileChanges)
                {
                    _logger.LogDebug($"[WorkflowToolAdapter] File changes detected - reloading workflows");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if workflow definition files have been modified since last load
        /// Compares file modification times to detect changes without reading file contents.
        /// </summary>
        private async Task<bool> HasWorkflowFilesChangedAsync()
        {
            try
            {
                var configDir = Path.Combine(AppContext.BaseDirectory, "Config", "WorkflowDefinitions");

                if (!Directory.Exists(configDir))
                {
                    return false;
                }

                var jsonFiles = Directory.GetFiles(configDir, "*.json");
                var currentFileSet = new HashSet<string>(jsonFiles);
                var cachedFileSet = new HashSet<string>(_fileModificationTimes.Keys);

                // Check if files were added or removed
                if (!currentFileSet.SetEquals(cachedFileSet))
                {
                    _logger.LogDebug($"[WorkflowToolAdapter] Workflow file list changed");
                    return true;
                }

                // Check if existing files were modified
                foreach (var jsonFile in jsonFiles)
                {
                    var lastWriteTime = File.GetLastWriteTime(jsonFile);

                    if (_fileModificationTimes.TryGetValue(jsonFile, out var cachedTime))
                    {
                        if (lastWriteTime > cachedTime)
                        {
                            _logger.LogDebug($"[WorkflowToolAdapter] File modified: {Path.GetFileName(jsonFile)}");
                            return true;
                        }
                    }
                    else
                    {
                        // New file
                        _logger.LogDebug($"[WorkflowToolAdapter] New file detected: {Path.GetFileName(jsonFile)}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorkflowToolAdapter] Error checking file changes: {ex.Message}");
                // If we can't check file changes, err on the side of caution and reload
                return true;
            }
        }

        /// <summary>
        /// Force reload of workflows - useful for development or when files are known to have changed
        /// Bypasses all caching mechanisms and immediately reloads workflow definitions from disk.
        /// </summary>
        public async Task ForceReloadWorkflowsAsync()
        {
            _logger.LogTrace($"[WorkflowToolAdapter] Force reloading workflows");
            await LoadBuiltinWorkflowsAsync();
            _lastLoadTime = DateTime.Now;
            _callsSinceLastLoad = 0;
        }

        /// <summary>
        /// Get current cache status for monitoring and debugging
        /// Returns information about cache performance and current state.
        /// </summary>
        public string GetCacheStatus()
        {
            var now = DateTime.Now;
            var minReloadInterval = MinReloadIntervalOverride > TimeSpan.Zero ? MinReloadIntervalOverride : MinReloadInterval;
            var maxCallsBeforeReload = MaxCallsBeforeReloadOverride > 0 ? MaxCallsBeforeReloadOverride : MaxCallsBeforeReload;
            var fileCheckInterval = FileCheckIntervalOverride > TimeSpan.Zero ? FileCheckIntervalOverride : FileCheckInterval;

            var status = new
            {
                lastLoadTime = _lastLoadTime == DateTime.MinValue ? "Never" : _lastLoadTime.ToString("yyyy-MM-dd HH:mm:ss"),
                callsSinceLastLoad = _callsSinceLastLoad,
                timeSinceLastLoad = _lastLoadTime == DateTime.MinValue ? "N/A" : (now - _lastLoadTime).ToString(@"hh\:mm\:ss"),
                lastFileCheckTime = _lastFileCheckTime == DateTime.MinValue ? "Never" : _lastFileCheckTime.ToString("yyyy-MM-dd HH:mm:ss"),
                trackedFiles = _fileModificationTimes.Count,
                configuration = new
                {
                    minReloadInterval = minReloadInterval.ToString(@"hh\:mm\:ss"),
                    maxCallsBeforeReload = maxCallsBeforeReload,
                    fileCheckInterval = fileCheckInterval.ToString(@"hh\:mm\:ss")
                },
                nextReloadTriggers = new
                {
                    callsRemaining = Math.Max(0, maxCallsBeforeReload - _callsSinceLastLoad),
                    timeUntilNextCheck = _lastLoadTime == DateTime.MinValue ? "On next call" :
                        Math.Max(0, (minReloadInterval - (now - _lastLoadTime)).TotalSeconds).ToString("F1") + " seconds"
                }
            };

            return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Load built-in workflows from the configuration directory
        /// Scans the configuration directory for workflow JSON files and registers them
        /// with the workflow engine for execution. Each valid workflow file is loaded
        /// and made available for MCP tool discovery and execution.
        /// Also updates file modification time cache for change detection.
        /// </summary>
        private async Task LoadBuiltinWorkflowsAsync()
        {
            try
            {
                var configDir = Path.Combine(AppContext.BaseDirectory, "Config", "WorkflowDefinitions");

                if (!Directory.Exists(configDir))
                {
                    _logger.LogWarning($"[WorkflowToolAdapter] Workflow definitions directory not found: {configDir}");
                    return;
                }

                var jsonFiles = Directory.GetFiles(configDir, "*.json");
                _logger.LogTrace($"[WorkflowToolAdapter] Found {jsonFiles.Length} workflow definition files");

                // Clear and rebuild file modification time cache
                _fileModificationTimes.Clear();

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(jsonFile);
                        var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(jsonContent, JsonUtils.JsonSerializerOptions);

                        if (workflow != null)
                        {
                            await _workflowEngine.RegisterWorkflowAsync(workflow);
                            _logger.LogTrace($"[WorkflowToolAdapter] Loaded workflow: {workflow.Name} ({workflow.Id})");
                        }

                        // Update file modification time cache
                        _fileModificationTimes[jsonFile] = File.GetLastWriteTime(jsonFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[WorkflowToolAdapter] Error loading workflow from {jsonFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorkflowToolAdapter] Error loading builtin workflows: {ex.Message}");
            }
        }

        private JsonElement CreateObjectSchema(string propertiesJson, string[] required)
        {
            var schemaObject = new
            {
                type = "object",
                properties = JsonSerializer.Deserialize<Dictionary<string, object>>(propertiesJson),
                required = required
            };

            // Convert to JsonElement using JsonDocument
            var json = JsonSerializer.Serialize(schemaObject);
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
#endif