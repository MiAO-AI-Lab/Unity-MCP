#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public interface IMcpRunner : IToolRunner, IResourceRunner, IDisposable
    {
        bool HasTool(string name);
        bool HasResource(string name);
    }
}