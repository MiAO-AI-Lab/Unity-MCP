#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using R3;

namespace com.MiAO.MCP.Common
{
    public class RpcRouter : IRpcRouter
    {
        readonly ILogger<RpcRouter> _logger;
        readonly IMcpRunner _mcpRunner;
        readonly IConnectionManager _connectionManager;
        readonly CompositeDisposable _serverEventsDisposables = new();
        readonly IDisposable _hubConnectionDisposable;

        public ReadOnlyReactiveProperty<HubConnectionState> ConnectionState => _connectionManager.ConnectionState;
        public ReadOnlyReactiveProperty<bool> KeepConnected => _connectionManager.KeepConnected;

        public RpcRouter(ILogger<RpcRouter> logger, IConnectionManager connectionManager, IMcpRunner mcpRunner)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogTrace("{0} Ctor.", nameof(RpcRouter));
            _mcpRunner = mcpRunner ?? throw new ArgumentNullException(nameof(mcpRunner));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

            _connectionManager.Endpoint = Consts.Hub.RemoteApp;

            _hubConnectionDisposable = connectionManager.HubConnection
                .Subscribe(SubscribeOnServerEvents);
        }

        public Task<bool> Connect(CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("{0} Connecting... (to RemoteApp: {1}).", nameof(RpcRouter), _connectionManager.Endpoint);
            return _connectionManager.Connect(cancellationToken);
        }
        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("{0} Disconnecting... (to RemoteApp: {1}).", nameof(RpcRouter), _connectionManager.Endpoint);
            return _connectionManager.Disconnect(cancellationToken);
        }

        void SubscribeOnServerEvents(HubConnection? hubConnection)
        {
            _logger.LogTrace("{0} Clearing server events disposables.", nameof(RpcRouter));
            _serverEventsDisposables.Clear();

            if (hubConnection == null)
                return;

            _logger.LogTrace("{0} Subscribing to server events.", nameof(RpcRouter));

            hubConnection.On(Consts.RPC.Client.ForceDisconnect, async () =>
            {
                _logger.LogDebug("{0}.{1}", nameof(RpcRouter), Consts.RPC.Client.ForceDisconnect);
                await _connectionManager.Disconnect();
            });

            hubConnection.On<RequestCallTool, IResponseData<ResponseCallTool>>(Consts.RPC.Client.RunCallTool, async data =>
                {
                    _logger.LogDebug("{0}.{1}", nameof(RpcRouter), Consts.RPC.Client.RunCallTool);
                    return await _mcpRunner.RunCallTool(data);
                })
                .AddTo(_serverEventsDisposables);

            hubConnection.On<RequestListTool, IResponseData<ResponseListTool[]>>(Consts.RPC.Client.RunListTool, async data =>
                {
                    _logger.LogDebug("{0}.{1}", nameof(RpcRouter), Consts.RPC.Client.RunListTool);
                    return await _mcpRunner.RunListTool(data);
                })
                .AddTo(_serverEventsDisposables);

            hubConnection.On<RequestResourceContent, IResponseData<ResponseResourceContent[]>>(Consts.RPC.Client.RunResourceContent, async data =>
                {
                    _logger.LogDebug("{0}.{1}", nameof(RpcRouter), Consts.RPC.Client.RunResourceContent);
                    return await _mcpRunner.RunResourceContent(data);
                })
                .AddTo(_serverEventsDisposables);

            hubConnection.On<RequestListResources, IResponseData<ResponseListResource[]>>(Consts.RPC.Client.RunListResources, async data =>
                {
                    _logger.LogDebug("{0}.{1}", nameof(RpcRouter), Consts.RPC.Client.RunListResources);
                    return await _mcpRunner.RunListResources(data);
                })
                .AddTo(_serverEventsDisposables);

            hubConnection.On<RequestListResourceTemplates, IResponseData<ResponseResourceTemplate[]>>(Consts.RPC.Client.RunListResourceTemplates, async data =>
                {
                    _logger.LogDebug("{0}.{1}", nameof(RpcRouter), Consts.RPC.Client.RunListResourceTemplates);
                    return await _mcpRunner.RunResourceTemplates(data);
                })
                .AddTo(_serverEventsDisposables);
        }

        public Task<ResponseData<string>> NotifyAboutUpdatedTools(CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("{0} Notify server about updated tools.", nameof(RpcRouter));
            return _connectionManager.InvokeAsync<string, ResponseData<string>>(Consts.RPC.Server.OnListToolsUpdated, string.Empty, cancellationToken);
        }

        public Task<ResponseData<string>> NotifyAboutUpdatedResources(CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("{0} Notify server about updated resources.", nameof(RpcRouter));
            return _connectionManager.InvokeAsync<string, ResponseData<string>>(Consts.RPC.Server.OnListResourcesUpdated, string.Empty, cancellationToken);
        }

        public Task<ResponseData<ModelUseResponse>> RequestModelUse(RequestModelUse request, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("{0} Request ModelUse from server.", nameof(RpcRouter));
            return _connectionManager.InvokeAsync<RequestModelUse, ResponseData<ModelUseResponse>>(
                Consts.RPC.Client.RequestModelUse,
                request,
                cancellationToken);
        }

        public void Dispose()
        {
            DisposeAsync().Wait();
        }

        public Task DisposeAsync()
        {
            _logger.LogTrace("{0} DisposeAsync.", nameof(RpcRouter));
            _serverEventsDisposables.Dispose();
            _hubConnectionDisposable.Dispose();

            return _connectionManager.DisposeAsync();
        }
    }
}