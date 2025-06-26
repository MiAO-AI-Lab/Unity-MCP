#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

using com.IvanMurzak.ReflectorNet.Model;

namespace com.IvanMurzak.Unity.MCP.Common.Server
{
    public abstract class ServerTool : ServerCommand<IRequestCallTool, IResponseCallTool>, IServerTool
    {

    }
}