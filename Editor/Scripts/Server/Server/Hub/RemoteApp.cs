#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if !UNITY_5_3_OR_NEWER
using System;
using System.Threading.Tasks;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Server
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

        // Added: Handle ModelUse requests from Unity Runtime
        public async Task<IResponseData<ModelUseResponse>> RequestModelUse(RequestModelUse request)
        {
            _logger.LogTrace($"RemoteApp RequestModelUse. {_guid}. Request: {request.ModelType}");

            try
            {
                // Create ComplexServiceHandler to handle ModelUse requests
                var handlerLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Handlers.ComplexServiceHandler>();
                var handler = new Handlers.ComplexServiceHandler(handlerLogger);

                // Build ModelUse request JSON
                var modelRequest = new ModelUseRequest
                {
                    ModelType = request.ModelType,
                    Prompt = request.Prompt,
                    ImageData = request.ImageData,
                    Parameters = request.Parameters
                };

                var requestJson = System.Text.Json.JsonSerializer.Serialize(modelRequest);
                var responseJson = await handler.HandleModelUseRequestAsync(requestJson);
                _logger.LogError("Model use response: {ResponseJson}", responseJson);

                var response = System.Text.Json.JsonSerializer.Deserialize<ModelUseResponse>(responseJson);

                if (response == null)
                {
                    return ResponseData<ModelUseResponse>.Error(request.RequestID, "Failed to process model request");
                }

                var r = ResponseData<ModelUseResponse>.Success(request.RequestID, "Sucessfully processed the model request");
                r.Value = response;
                return r;
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