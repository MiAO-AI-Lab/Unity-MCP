#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.AspNetCore.SignalR.Client;
using R3;

namespace com.MiAO.Unity.MCP.Common
{
    public interface IRpcRouter : IDisposableAsync
    {
        ReadOnlyReactiveProperty<bool> KeepConnected { get; }
        ReadOnlyReactiveProperty<HubConnectionState> ConnectionState { get; }
        Task<bool> Connect(CancellationToken cancellationToken = default);
        Task Disconnect(CancellationToken cancellationToken = default);

        Task<ResponseData<string>> NotifyAboutUpdatedTools(CancellationToken cancellationToken = default);
        Task<ResponseData<string>> NotifyAboutUpdatedResources(CancellationToken cancellationToken = default);

        // New: Reverse ModelUse RPC method
        Task<ResponseData<ModelUseResponse>> RequestModelUse(RequestModelUse request, CancellationToken cancellationToken = default);
    }
}