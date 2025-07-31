#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using com.MiAO.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NLog;
using com.IvanMurzak.ReflectorNet.Utils;
using Microsoft.Extensions.Logging;
using com.MiAO.MCP.Server.Handlers;
using com.MiAO.MCP.Utils;
using com.MiAO.MCP.Server.Utils;

namespace com.MiAO.MCP.Server
{
    /// <summary>
    /// Enhanced ToolRouter with workflow support
    /// </summary>
    public static partial class ToolRouter
    {
        private static WorkflowHandler? _workflowHandler;
        private static MultiLevelCacheManager<ListToolsResult>? _unityToolsCacheManager;

        /// <summary>
        /// Initialize enhanced ToolRouter with workflow support
        /// </summary>
        public static void InitializeEnhanced(WorkflowHandler workflowHandler)
        {
            _workflowHandler = workflowHandler;

            // Initialize Unity tools cache manager
            var logger = LogManager.GetCurrentClassLogger();
            var msLogger = new NLogAdapter(logger);

            _unityToolsCacheManager = new MultiLevelCacheManager<ListToolsResult>(
                msLogger,
                "UnityTools",
                LoadUnityToolsFromRpcAsync,
                MultiLevelCacheConfig.HighFrequency // Use high frequency config since this will be called frequently
            );
        }

        /// <summary>
        /// Enhanced ListAll with workflow tools and intelligent caching
        /// </summary>
        public static async ValueTask<ListToolsResult> ListAllEnhanced(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
        {
            try
            {
                if (_unityToolsCacheManager != null)
                {
                    return await _unityToolsCacheManager.GetDataAsync();
                }
                else
                {
                    // Fallback to original implementation if cache manager not initialized
                    return await LoadAllToolsDirectlyAsync(request, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error in ListAllEnhanced");
                return new ListToolsResult().SetError($"Error listing tools: {ex.Message}");
            }
        }

        /// <summary>
        /// Load Unity tools from RPC - data loader for cache manager
        /// </summary>
        private static async Task<ListToolsResult> LoadUnityToolsFromRpcAsync()
        {
            try
            {
                var originalResult = await ListAll(CancellationToken.None);

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
                logger.Error(ex, "Error loading Unity tools from RPC");
                throw;
            }
        }

        /// <summary>
        /// Load all tools directly (fallback when cache is not available)
        /// </summary>
        private static async Task<ListToolsResult> LoadAllToolsDirectlyAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
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
        /// Force reload Unity tools cache - useful for development or when tools are known to have changed
        /// </summary>
        public static async Task ForceReloadUnityToolsAsync()
        {
            if (_unityToolsCacheManager != null)
            {
                await _unityToolsCacheManager.ForceReloadAsync();
            }
        }

        /// <summary>
        /// Get Unity tools cache status for monitoring and debugging
        /// </summary>
        public static string GetUnityToolsCacheStatus()
        {
            if (_unityToolsCacheManager != null)
            {
                var stats = _unityToolsCacheManager.GetStats();
                return stats.ToJson();
            }
            return "Cache manager not initialized";
        }

        /// <summary>
        /// Update Unity tools cache configuration at runtime
        /// </summary>
        public static void UpdateUnityToolsCacheConfig(Action<MultiLevelCacheConfig> configUpdater)
        {
            if (_unityToolsCacheManager != null)
            {
                _unityToolsCacheManager.UpdateConfig(configUpdater);
            }
        }

        /// <summary>
        /// Notify cache manager that Unity tools have changed
        /// This should be called when tools are added, removed, or modified
        /// </summary>
        public static void NotifyUnityToolsChanged()
        {
            if (_unityToolsCacheManager != null)
            {
                _unityToolsCacheManager.NotifyDataChanged();
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

            if (_unityToolsCacheManager != null)
            {
                // Cleanup cache manager if needed
                _unityToolsCacheManager = null;
            }

            await Task.CompletedTask;
        }
    }
}
#endif