#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if !UNITY_5_3_OR_NEWER
using System;
using System.Threading.Tasks;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using com.MiAO.Unity.MCP.Server.RpcGateway.External;

namespace com.MiAO.Unity.MCP.Server
{
    public class RemoteApp : BaseHub<RemoteApp>, IRemoteApp
    {
        readonly EventAppToolsChange _eventAppToolsChange;

        public RemoteApp(ILogger<RemoteApp> logger, IHubContext<RemoteApp> hubContext, EventAppToolsChange eventAppToolsChange)
            : base(logger, hubContext)
        {
            _eventAppToolsChange = eventAppToolsChange ?? throw new ArgumentNullException(nameof(eventAppToolsChange));
        }

        public Task<IResponseData<string>> OnListToolsUpdated(string data)
        {
            _logger.LogTrace("RemoteApp OnListToolsUpdated. {0}. Data: {1}", _guid, data);
            _eventAppToolsChange.OnNext(new EventAppToolsChange.EventData
            {
                ConnectionId = Context.ConnectionId,
                Data = data
            });
            return ResponseData<string>.Success(data, string.Empty).TaskFromResult<IResponseData<string>>();
        }

        public Task<IResponseData<string>> OnListResourcesUpdated(string data)
        {
            _logger.LogTrace("RemoteApp OnListResourcesUpdated. {0}. Data: {1}", _guid, data);
            // _onListResourcesUpdated.OnNext(Unit.Default);
            return ResponseData<string>.Success(data, string.Empty).TaskFromResult<IResponseData<string>>();
        }

        // Added: Handle ModelUse requests from Unity Runtime using ModelUse RPC Gateway
        public async Task<IResponseData<ModelUseResponse>> RequestModelUse(RequestModelUse request)
        {
            _logger.LogTrace($"RemoteApp RequestModelUse. {_guid}. Request: {request.ModelType}");

            try
            {
                // Use ModelUse RPC Gateway instead of deleted ComplexServiceHandler
                var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ModelUseRpcGateway>();
                var modelUseGateway = new ModelUseRpcGateway(logger);
                
                // Build ModelUse request parameters
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["ModelType"] = request.ModelType,
                    ["Prompt"] = request.Prompt,
                    ["Parameters"] = request.Parameters
                };
                
                if (request.ImageData != null)
                {
                    parameters["ImageData"] = request.ImageData;
                }

                var result = await modelUseGateway.CallAsync<string>("ModelUse", parameters);
                
                if (!string.IsNullOrEmpty(result))
                {
                    var response = System.Text.Json.JsonSerializer.Deserialize<ModelUseResponse>(result);
                    
                    if (response != null)
                    {
                        var r = ResponseData<ModelUseResponse>.Success(request.RequestID, "Successfully processed the model request");
                        r.Value = response;
                        return r;
                    }
                }

                return ResponseData<ModelUseResponse>.Error(request.RequestID, "Failed to process model request");
            }
            catch (Exception ex)
            {
                _logger.LogError($"RemoteApp RequestModelUse error: {ex.Message}");
                return ResponseData<ModelUseResponse>.Error(request.RequestID, $"ModelUse request failed: {ex.Message}");
            }
        }
    }
}
#endif