#if !UNITY_5_3_OR_NEWER
using com.MiAO.Unity.MCP.Common;
using Microsoft.Extensions.DependencyInjection;

namespace com.MiAO.Unity.MCP.Server
{
    public static class McpServerBuilderExtensions
    {
        public static IMcpPluginBuilder WithServerFeatures(this IMcpPluginBuilder builder)
        {
            builder.Services.AddRouting();
            builder.Services.AddHostedService<McpServerService>();

            builder.Services.AddSingleton<EventAppToolsChange>();
            builder.Services.AddSingleton<IToolRunner, RemoteToolRunner>();
            builder.Services.AddSingleton<IResourceRunner, RemoteResourceRunner>();

            builder.AddMcpRunner();

            return builder;
        }
    }
}
#endif