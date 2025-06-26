#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using R3;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public interface IConnectionManager : IConnection, IDisposable
    {
        string Endpoint { get; set; }
        ReadOnlyReactiveProperty<HubConnection?> HubConnection { get; }
        Task InvokeAsync<TInput>(string methodName, TInput input, CancellationToken cancellationToken = default);
        Task<TResult> InvokeAsync<TInput, TResult>(string methodName, TInput input, CancellationToken cancellationToken = default);
    }
}