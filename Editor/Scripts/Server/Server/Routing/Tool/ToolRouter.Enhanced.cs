#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NLog;
using com.IvanMurzak.ReflectorNet.Utils;
using Microsoft.Extensions.Logging;
using com.MiAO.Unity.MCP.Server.Handlers;
using com.MiAO.Unity.MCP.Utils;

namespace com.MiAO.Unity.MCP.Server
{
    /// <summary>
    /// Enhanced ToolRouter with workflow support
    /// </summary>
    public static partial class ToolRouter
    {
        private static WorkflowHandler? _workflowHandler;

        /// <summary>
        /// Initialize enhanced ToolRouter with workflow support
        /// </summary>
        public static void InitializeEnhanced(WorkflowHandler workflowHandler)
        {
            _workflowHandler = workflowHandler;
        }

        /// <summary>
        /// Enhanced ListAll with workflow tools
        /// </summary>
        public static async ValueTask<ListToolsResult> ListAllEnhanced(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
        {
            try
            {
                // Get original tools first
                var originalResult = await ListAll(request, cancellationToken);

                // Get workflow tools if available
                if (_workflowHandler != null)
                {
                    try
                    {
                        var workflowToolsList = await _workflowHandler.GetAvailableWorkflowToolsAsync();

                        // Create combined tools list
                        var allTools = new List<ModelContextProtocol.Protocol.Tool>();

                        // Add original tools
                        if (originalResult.Tools != null && originalResult.Tools.Count > 0)
                        {
                            allTools.AddRange(originalResult.Tools);
                        }

                        // Add workflow tools  
                        if (workflowToolsList != null && workflowToolsList.Count > 0)
                        {
                            allTools.AddRange(workflowToolsList);
                        }

                        return new ListToolsResult
                        {
                            Tools = allTools
                        };
                    }
                    catch (Exception workflowEx)
                    {
                        // If workflow tools fail, just return original result
                        var logger = LogManager.GetCurrentClassLogger();
                        logger.Warn($"Failed to get workflow tools: {workflowEx.Message}");
                        return originalResult;
                    }
                }

                return originalResult;
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error in ListAllEnhanced");
                return new ListToolsResult().SetError($"Error listing tools: {ex.Message}");
            }
        }

        /// <summary>
        /// Enhanced CallTool with workflow support
        /// </summary>
        public static async ValueTask<CallToolResponse> CallEnhanced(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
        {
            if (request?.Params?.Name == null)
            {
                return new CallToolResponse().SetError("[Error] Request or tool name is null");
            }

            var toolName = request.Params.Name;

            try
            {
                // Check if this is a workflow tool
                if (_workflowHandler != null && toolName.StartsWith("workflow_"))
                {
                    // Convert arguments to dictionary
                    var arguments = new Dictionary<string, JsonElement>();
                    if (request.Params.Arguments != null)
                    {
                        foreach (var arg in request.Params.Arguments)
                        {
                            arguments[arg.Key] = arg.Value;
                        }
                    }

                    var result = await _workflowHandler.ExecuteWorkflowAsync(toolName, arguments);
                    return result;
                }

                // Fall back to original tool handling
                return await Call(request, cancellationToken);
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, $"Error in CallEnhanced for tool: {toolName}");
                return new CallToolResponse().SetError($"Error executing tool '{toolName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Get workflow information for debugging
        /// </summary>
        public static async ValueTask<CallToolResponse> GetWorkflowInfo(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
        {
            try
            {
                if (_workflowHandler == null)
                {
                    var contentList = new List<ModelContextProtocol.Protocol.Content>
                    {
                        new ModelContextProtocol.Protocol.Content
                        {
                            Type = "text",
                            Text = "Workflow handler not initialized"
                        }
                    };

                    return new CallToolResponse
                    {
                        Content = contentList
                    };
                }

                var info = await _workflowHandler.GetWorkflowInfoAsync();

                var contentList2 = new List<ModelContextProtocol.Protocol.Content>
                {
                    new ModelContextProtocol.Protocol.Content
                    {
                        Type = "text",
                        Text = info
                    }
                };

                return new CallToolResponse
                {
                    Content = contentList2
                };
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error getting workflow info");
                return new CallToolResponse().SetError($"Error getting workflow info: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleanup workflow handler resources
        /// </summary>
        public static async Task DisposeAsync()
        {
            if (_workflowHandler != null)
            {
                // Cleanup workflow handler if needed
                _workflowHandler = null;
            }
            await Task.CompletedTask;
        }
    }
}
#endif