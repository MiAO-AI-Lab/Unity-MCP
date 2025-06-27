#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using R3;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public interface IMcpPlugin : IConnection, IDisposableAsync
    {
        ILogger Logger { get; }
        IMcpRunner McpRunner { get; }
    }
    public interface IConnection : IDisposableAsync
    {
        ReadOnlyReactiveProperty<bool> KeepConnected { get; }
        ReadOnlyReactiveProperty<HubConnectionState> ConnectionState { get; }
        Task<bool> Connect(CancellationToken cancellationToken = default);
        Task Disconnect(CancellationToken cancellationToken = default);
    }

    public interface IDisposableAsync : IDisposable
    {
        Task DisposeAsync();
    }

    // -----------------------------------------------------------------

    public interface IToolRunner
    {
        Task<IResponseData<ResponseCallTool>> RunCallTool(IRequestCallTool requestData, CancellationToken cancellationToken = default);
        Task<IResponseData<ResponseListTool[]>> RunListTool(IRequestListTool requestData, CancellationToken cancellationToken = default);
    }

    public interface IResourceRunner
    {
        Task<IResponseData<ResponseResourceContent[]>> RunResourceContent(IRequestResourceContent requestData, CancellationToken cancellationToken = default);
        Task<IResponseData<ResponseListResource[]>> RunListResources(IRequestListResources requestData, CancellationToken cancellationToken = default);
        Task<IResponseData<ResponseResourceTemplate[]>> RunResourceTemplates(IRequestListResourceTemplates requestData, CancellationToken cancellationToken = default);
    }

    // -----------------------------------------------------------------
}