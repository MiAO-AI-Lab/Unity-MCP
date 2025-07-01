#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using com.MiAO.Unity.MCP.Server.RpcGateway.Core;

namespace com.MiAO.Unity.MCP.Server.WorkflowOrchestration.Core
{
    /// <summary>
    /// Simple Workflow Engine Implementation - Core orchestration engine
    /// Provides a concrete implementation of the workflow execution engine with support
    /// for multi-step workflows, expression evaluation, conditional execution, and
    /// integration with multiple RPC gateways for external system communication.
    /// </summary>
    public class SimpleWorkflowEngine : IWorkflowEngine
    {
        private readonly ILogger<SimpleWorkflowEngine> _logger;
        private readonly Dictionary<string, WorkflowDefinition> _workflows = new();
        private readonly Dictionary<string, IRpcGateway> _gateways = new();

        public SimpleWorkflowEngine(ILogger<SimpleWorkflowEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Register an RPC gateway for external system communication
        /// Adds a new gateway to the engine's registry, enabling workflows to
        /// communicate with specific external systems through the registered gateway.
        /// </summary>
        public void RegisterGateway(IRpcGateway gateway)
        {
            _gateways[gateway.GatewayId] = gateway;
            _logger.LogInformation($"[SimpleWorkflowEngine] Registered gateway: {gateway.GatewayId}");
        }

        public async Task<WorkflowResult> ExecuteWorkflowAsync(WorkflowDefinition workflow, IDataFlowContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new WorkflowResult();

            try
            {
                _logger.LogInformation($"[SimpleWorkflowEngine] Starting workflow: {workflow.Name} ({workflow.Id})");

                // Execute steps in sequential order
                foreach (var step in workflow.Steps)
                {
                    var stepResult = await ExecuteStepAsync(step, context);
                    result.StepResults.Add(stepResult);
                    context.StepResults[step.Id] = stepResult;

                    if (!stepResult.IsSuccess)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Step '{step.Id}' failed: {stepResult.ErrorMessage}";
                        break;
                    }
                }

                // If all steps succeeded, process workflow outputs
                if (result.StepResults.All(s => s.IsSuccess))
                {
                    result.IsSuccess = true;
                    await ProcessOutputsAsync(workflow, context, result);
                }

                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;
                result.Metadata["workflowId"] = workflow.Id;
                result.Metadata["stepCount"] = workflow.Steps.Count;

                _logger.LogInformation($"[SimpleWorkflowEngine] Completed workflow: {workflow.Name}, Success: {result.IsSuccess}, Time: {result.ExecutionTime}");
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.IsSuccess = false;
                result.ErrorMessage = $"Workflow execution failed: {ex.Message}";
                result.ExecutionTime = stopwatch.Elapsed;

                _logger.LogError($"[SimpleWorkflowEngine] Workflow execution failed: {ex.Message}");
                return result;
            }
        }

        private async Task<StepResult> ExecuteStepAsync(WorkflowStep step, IDataFlowContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new StepResult { StepId = step.Id };

            try
            {
                _logger.LogDebug($"[SimpleWorkflowEngine] Executing step: {step.Id} ({step.Type})");

                // Check conditional execution requirements
                if (!await context.EvaluateConditionAsync(step.Condition))
                {
                    result.IsSuccess = true;
                    result.Result = "Skipped due to condition";
                    result.Metadata["skipped"] = true;
                    return result;
                }

                // Resolve parameter expressions using the data flow context
                var resolvedParameters = await ResolveParametersAsync(step.Parameters, context);

                // Execute step based on its type
                switch (step.Type.ToLower())
                {
                    case "rpc_call":
                        result.Result = await ExecuteRpcCallStepAsync(step, resolvedParameters);
                        break;
                    case "model_use":
                        result.Result = await ExecuteModelUseStepAsync(step, resolvedParameters);
                        break;
                    case "data_transform":
                        result.Result = await ExecuteDataTransformStepAsync(step, resolvedParameters);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown step type: {step.Type}");
                }

                result.IsSuccess = true;
                _logger.LogDebug($"[SimpleWorkflowEngine] Step completed: {step.Id}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError($"[SimpleWorkflowEngine] Step failed: {step.Id}, Error: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;
            }

            return result;
        }

        private async Task<object> ExecuteRpcCallStepAsync(WorkflowStep step, Dictionary<string, object> parameters)
        {
            if (!_gateways.TryGetValue(step.Connector, out var gateway))
            {
                throw new InvalidOperationException($"Gateway not found: {step.Connector}");
            }

            var operation = step.Operation;

            // Support dynamic tool discovery syntax
            if (operation.StartsWith("${discover:") && operation.EndsWith("}"))
            {
                var toolName = operation.Substring(11, operation.Length - 12);
                operation = toolName;
            }

            return await gateway.CallAsync<string>(operation, parameters);
        }

        private async Task<object> ExecuteModelUseStepAsync(WorkflowStep step, Dictionary<string, object> parameters)
        {
            if (!_gateways.TryGetValue("model_use", out var gateway))
            {
                throw new InvalidOperationException("ModelUse gateway not found");
            }

            return await gateway.CallAsync<string>(step.Operation, parameters);
        }

        private async Task<object> ExecuteDataTransformStepAsync(WorkflowStep step, Dictionary<string, object> parameters)
        {
            // Simple data transformation implementation
            var data = parameters.GetValueOrDefault("data", "");
            var transform = parameters.GetValueOrDefault("transform", "").ToString();

            switch (transform?.ToLower())
            {
                case "json_parse":
                    return JsonSerializer.Deserialize<object>(data.ToString() ?? "{}");
                case "json_stringify":
                    return JsonSerializer.Serialize(data);
                case "to_upper":
                    return data.ToString()?.ToUpper() ?? "";
                case "to_lower":
                    return data.ToString()?.ToLower() ?? "";
                default:
                    return data;
            }
        }

        private async Task<Dictionary<string, object>> ResolveParametersAsync(Dictionary<string, object> parameters, IDataFlowContext context)
        {
            var resolved = new Dictionary<string, object>();

            foreach (var kvp in parameters)
            {
                resolved[kvp.Key] = await ResolveParameterValueAsync(kvp.Value, context);
            }

            return resolved;
        }

        private async Task<object> ResolveParameterValueAsync(object value, IDataFlowContext context)
        {
            if (value == null)
                return null;

            // Handle JsonElement type
            if (value is JsonElement jsonElement)
            {
                return await ResolveJsonElementAsync(jsonElement, context);
            }

            // Handle regular string type
            if (value is string strValue)
            {
                return await ResolveStringExpressionAsync(strValue, context);
            }

            // Handle Dictionary type (recursive processing)
            if (value is Dictionary<string, object> dictValue)
            {
                var resolvedDict = new Dictionary<string, object>();
                foreach (var kvp in dictValue)
                {
                    resolvedDict[kvp.Key] = await ResolveParameterValueAsync(kvp.Value, context);
                }
                return resolvedDict;
            }

            // Handle List type (recursive processing)
            if (value is IEnumerable<object> listValue && !(value is string))
            {
                var resolvedList = new List<object>();
                foreach (var item in listValue)
                {
                    resolvedList.Add(await ResolveParameterValueAsync(item, context));
                }
                return resolvedList;
            }

            // Return other types directly
            return value;
        }

        private async Task<object> ResolveJsonElementAsync(JsonElement jsonElement, IDataFlowContext context)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    var stringValue = jsonElement.GetString();
                    return await ResolveStringExpressionAsync(stringValue, context);

                case JsonValueKind.Number:
                    return jsonElement.TryGetInt32(out var intValue) ? intValue : jsonElement.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                case JsonValueKind.Object:
                    var dictResult = new Dictionary<string, object>();
                    foreach (var property in jsonElement.EnumerateObject())
                    {
                        dictResult[property.Name] = await ResolveJsonElementAsync(property.Value, context);
                    }
                    return dictResult;

                case JsonValueKind.Array:
                    var listResult = new List<object>();
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        listResult.Add(await ResolveJsonElementAsync(item, context));
                    }
                    return listResult;

                default:
                    return jsonElement.ToString();
            }
        }

        private async Task<object> ResolveStringExpressionAsync(string value, IDataFlowContext context)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains("${"))
            {
                return value;
            }

            var resolvedValue = await context.ResolveExpressionAsync<object>(value);
            return resolvedValue ?? value;
        }

        private async Task ProcessOutputsAsync(WorkflowDefinition workflow, IDataFlowContext context, WorkflowResult result)
        {
            foreach (var kvp in workflow.Outputs)
            {
                var outputValue = await context.ResolveExpressionAsync<object>(kvp.Value.Source);
                result.Outputs[kvp.Key] = outputValue ?? "";
            }
        }

        public async Task<List<WorkflowDefinition>> GetAvailableWorkflowsAsync()
        {
            return await Task.FromResult(_workflows.Values.ToList());
        }

        public async Task RegisterWorkflowAsync(WorkflowDefinition workflow)
        {
            _workflows[workflow.Id] = workflow;
            _logger.LogInformation($"[SimpleWorkflowEngine] Registered workflow: {workflow.Name} ({workflow.Id})");
            await Task.CompletedTask;
        }

        public async Task<WorkflowDefinition?> GetWorkflowAsync(string workflowId)
        {
            _workflows.TryGetValue(workflowId, out var workflow);
            // FIXME: To support "workflow_" prefix which may called from mcp client
            if (workflow == null && workflowId.StartsWith("workflow_"))
            {
                workflowId = workflowId.Substring(9);
                _workflows.TryGetValue(workflowId, out workflow);
            }
            return await Task.FromResult(workflow);
        }

        private Task<object?> ExecuteUnityToolStep(WorkflowStep step, Dictionary<string, object> context)
        {
            // Unity tool step execution
            // TODO: Implement Unity tool execution via RPC Gateway
            return Task.FromResult<object?>("Unity tool execution placeholder");
        }

        private Task<object?> ExecuteModelUseStep(WorkflowStep step, Dictionary<string, object> context)
        {
            // ModelUse step execution
            // TODO: Implement ModelUse execution via RPC Gateway
            return Task.FromResult<object?>("ModelUse execution placeholder");
        }

        private Task<object?> ExecuteWorkflowStep(WorkflowStep step, Dictionary<string, object> context)
        {
            // Sub-workflow execution
            // TODO: Implement sub-workflow execution
            return Task.FromResult<object?>("Sub-workflow execution placeholder");
        }

        private Task<object?> ResolveStepResult(WorkflowStep step, Dictionary<string, object> context)
        {
            if (context.TryGetValue($"step_{step.Id}_result", out var result))
            {
                return Task.FromResult(result);
            }
            return Task.FromResult<object?>(null);
        }
    }

    /// <summary>
    /// Simple data flow context implementation
    /// </summary>
    public class SimpleDataFlowContext : IDataFlowContext
    {
        private readonly Dictionary<string, object> _sessionData = new();

        public string SessionId { get; }
        public Dictionary<string, object> GlobalVariables { get; } = new();
        public Dictionary<string, StepResult> StepResults { get; } = new();
        public Dictionary<string, object> InputParameters { get; }

        public SimpleDataFlowContext(string sessionId, Dictionary<string, object> inputParameters)
        {
            SessionId = sessionId;
            InputParameters = inputParameters;
        }

        public async Task<T?> GetVariableAsync<T>(string variablePath)
        {
            if (GlobalVariables.TryGetValue(variablePath, out var value))
            {
                return (T?)value;
            }
            return default;
        }

        public async Task SetVariableAsync<T>(string key, T value)
        {
            GlobalVariables[key] = value!;
            await Task.CompletedTask;
        }

        public async Task<T?> ResolveExpressionAsync<T>(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return default;

            // Support ${input.param} syntax
            if (expression.StartsWith("${input.") && expression.EndsWith("}"))
            {
                var paramName = expression.Substring(8, expression.Length - 9);
                if (InputParameters.TryGetValue(paramName, out var value))
                {
                    return (T?)value;
                }
            }

            // Support ${step.result} syntax
            var stepMatch = Regex.Match(expression, @"\$\{(\w+)\.result\}");
            if (stepMatch.Success)
            {
                var stepId = stepMatch.Groups[1].Value;
                if (StepResults.TryGetValue(stepId, out var stepResult))
                {
                    return (T?)stepResult.Result;
                }
            }

            // Support ${step.success} syntax
            var successMatch = Regex.Match(expression, @"\$\{(\w+)\.success\}");
            if (successMatch.Success)
            {
                var stepId = successMatch.Groups[1].Value;
                if (StepResults.TryGetValue(stepId, out var stepResult))
                {
                    return (T?)(object)stepResult.IsSuccess;
                }
            }

            // If no expression found, return original value directly
            return (T?)(object)expression;
        }

        public async Task<bool> EvaluateConditionAsync(string? condition)
        {
            if (string.IsNullOrEmpty(condition))
                return true;

            // Simple condition evaluation
            var result = await ResolveExpressionAsync<object>(condition);

            if (result is bool boolResult)
                return boolResult;

            if (result is string strResult)
                return !string.IsNullOrEmpty(strResult) && strResult != "false";

            return result != null;
        }

        public async Task<T?> GetSessionDataAsync<T>(string key)
        {
            if (_sessionData.TryGetValue(key, out var value))
            {
                return (T?)value;
            }
            return default;
        }

        public async Task SetSessionDataAsync<T>(string key, T value)
        {
            _sessionData[key] = value!;
            await Task.CompletedTask;
        }

        public async Task ClearSessionAsync()
        {
            _sessionData.Clear();
            GlobalVariables.Clear();
            StepResults.Clear();
            await Task.CompletedTask;
        }
    }
}
#endif