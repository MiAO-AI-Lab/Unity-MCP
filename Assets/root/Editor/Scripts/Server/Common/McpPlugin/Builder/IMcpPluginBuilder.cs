#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Reflection;
using com.IvanMurzak.ReflectorNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public interface IMcpPluginBuilder
    {
        IServiceCollection Services { get; }
        IMcpPluginBuilder WithTool(string name, Type classType, MethodInfo methodInfo);
        IMcpPluginBuilder AddTool(string name, IRunTool runner);
        IMcpPluginBuilder WithResource(Type classType, MethodInfo getContentMethod);
        IMcpPluginBuilder AddResource(IRunResource resourceParams);
        IMcpPluginBuilder AddLogging(Action<ILoggingBuilder> loggingBuilder);
        IMcpPluginBuilder WithConfig(Action<ConnectionConfig> config);
        IMcpPlugin Build(Reflector reflector);
    }
}