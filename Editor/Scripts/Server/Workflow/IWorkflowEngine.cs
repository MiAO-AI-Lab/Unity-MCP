#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace com.MiAO.MCP.Server.WorkflowOrchestration.Core
{
    /// <summary>
    /// Workflow Engine Interface - Core orchestration and execution engine
    /// Defines the contract for workflow execution, management, and orchestration
    /// within the middleware architecture, providing a unified interface for
    /// complex multi-step operations across different external systems.
    /// </summary>
    public interface IWorkflowEngine
    {
        Task<WorkflowResult> ExecuteWorkflowAsync(WorkflowDefinition workflow, IDataFlowContext context);
        Task<List<WorkflowDefinition>> GetAvailableWorkflowsAsync();
        Task RegisterWorkflowAsync(WorkflowDefinition workflow);
        Task<WorkflowDefinition?> GetWorkflowAsync(string workflowId);
    }

    /// <summary>
    /// Workflow Definition - Complete specification of a workflow
    /// Contains all the metadata, parameters, steps, and output definitions
    /// required to execute a complex multi-step operation. Supports versioning,
    /// dependency management, and runtime requirements specification.
    /// </summary>
    public class WorkflowDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = string.Empty;

        public WorkflowMetadata? Metadata { get; set; }
        public List<WorkflowParameter> Parameters { get; set; } = new();
        public List<WorkflowStep> Steps { get; set; } = new();
        public Dictionary<string, WorkflowOutput> Outputs { get; set; } = new();
    }

    /// <summary>
    /// Workflow Metadata - Additional categorization and requirement information
    /// Provides categorization, tagging, runtime requirements, and plugin dependencies
    /// for proper workflow discovery, filtering, and execution environment validation.
    /// </summary>
    public class WorkflowMetadata
    {
        public string Category { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public List<string> RuntimeRequirements { get; set; } = new();
        public List<string> PluginDependencies { get; set; } = new();
    }

    /// <summary>
    /// Workflow Parameter - Input parameter definition with validation
    /// Defines the structure, type, validation rules, and default values
    /// for workflow input parameters, enabling proper parameter validation
    /// and documentation generation.
    /// </summary>
    public class WorkflowParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Required { get; set; } = false;
        public object? DefaultValue { get; set; }
        public List<WorkflowValidation> Validation { get; set; } = new();
    }

    /// <summary>
    /// Workflow Validation - Parameter validation rule specification
    /// Defines validation rules that can be applied to workflow parameters
    /// to ensure data integrity and proper input validation before execution.
    /// </summary>
    public class WorkflowValidation
    {
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Workflow Step - Individual execution unit within a workflow
    /// Represents a single operation that can be executed as part of a larger workflow,
    /// supporting different step types (RPC calls, model use, data transformation),
    /// conditional execution, retry policies, and timeout handling.
    /// </summary>
    public class WorkflowStep
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "rpc_call", "model_use", "data_transform"
        public string Connector { get; set; } = string.Empty; // "unity", "model_use"
        public string Operation { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string? Condition { get; set; } // Conditional expression for step execution
        public WorkflowRetryPolicy? RetryPolicy { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// Workflow Retry Policy - Error handling and retry configuration
    /// Defines how failed steps should be retried, including maximum attempts,
    /// delay strategies, and backoff algorithms for resilient workflow execution.
    /// </summary>
    public class WorkflowRetryPolicy
    {
        public int MaxAttempts { get; set; } = 1;
        public int DelaySeconds { get; set; } = 0;
        public string BackoffStrategy { get; set; } = "linear"; // "linear", "exponential"
    }

    /// <summary>
    /// Workflow Output - Output definition and data extraction specification
    /// Defines how to extract and format output data from workflow execution results,
    /// supporting expression-based data extraction from step results and context.
    /// </summary>
    public class WorkflowOutput
    {
        public string Source { get; set; } = string.Empty; // Expression like "${step.result}"
        public string Type { get; set; } = "string";
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Workflow Result - Complete execution result with metadata
    /// Contains the final result of workflow execution including success status,
    /// output values, individual step results, execution time, and metadata
    /// for monitoring and debugging purposes.
    /// </summary>
    public class WorkflowResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Outputs { get; set; } = new();
        public List<StepResult> StepResults { get; set; } = new();
        public TimeSpan ExecutionTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Step Result - Individual step execution result with timing
    /// Contains the result of executing a single workflow step including
    /// success status, result data, execution time, and error information
    /// for detailed workflow execution tracking.
    /// </summary>
    public class StepResult
    {
        public string StepId { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public object? Result { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Data Flow Context Interface - Workflow execution context and variable management
    /// Provides a comprehensive context for workflow execution including session management,
    /// variable storage, expression evaluation, and step result tracking. Supports
    /// complex data flow patterns with expression-based variable references.
    /// </summary>
    public interface IDataFlowContext
    {
        string SessionId { get; }
        Dictionary<string, object> GlobalVariables { get; }
        Dictionary<string, StepResult> StepResults { get; }
        Dictionary<string, object> InputParameters { get; }

        Task<T?> GetVariableAsync<T>(string variablePath); // Supports "${step.result.field}" syntax
        Task SetVariableAsync<T>(string key, T value);
        Task<T?> ResolveExpressionAsync<T>(string expression);
        Task<bool> EvaluateConditionAsync(string? condition);

        // Session management for stateful workflow execution
        Task<T?> GetSessionDataAsync<T>(string key);
        Task SetSessionDataAsync<T>(string key, T value);
        Task ClearSessionAsync();
    }
}
#endif