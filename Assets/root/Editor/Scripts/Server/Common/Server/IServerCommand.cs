#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Threading.Tasks;

namespace com.IvanMurzak.Unity.MCP.Common.Server
{
    public interface IServerCommand<TRequest, TResponse> : IDisposable
    {
        string Class { get; }
        string? Method { get; }
        Task<TResponse> Call(Action<TRequest> configCommand);
    }
}