#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public static partial class McpPluginBuilderExtensions
    {
        public static IMcpPluginBuilder WithPrompts(this IMcpPluginBuilder builder, params Type[] targetTypes)
            => WithPrompts(builder, targetTypes.ToArray());

        public static IMcpPluginBuilder WithPrompts(this IMcpPluginBuilder builder, IEnumerable<Type> targetTypes)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (targetTypes == null)
                throw new ArgumentNullException(nameof(targetTypes));

            foreach (var targetType in targetTypes)
            {
                if (targetType is not null)
                {
                    foreach (var method in targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        if (method.GetCustomAttribute<McpPluginPromptAttribute>() is not null)
                        {
                            // builder.Services.AddSingleton((Func<IServiceProvider, Prompt>)(method.IsStatic ?
                            //     services => McpServerPrompt.Create(method, options: new() { Services = services }) :
                            //     services => McpServerPrompt.Create(method, promptType, new() { Services = services })));
                        }
                    }
                }
            }

            return builder;
        }

        public static IMcpPluginBuilder WithPromptsFromAssembly(this IMcpPluginBuilder builder, IEnumerable<Assembly> assemblies)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            foreach (var assembly in assemblies)
                WithPromptsFromAssembly(builder, assembly);

            return builder;
        }
        public static IMcpPluginBuilder WithPromptsFromAssembly(this IMcpPluginBuilder builder, Assembly? assembly = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            assembly ??= Assembly.GetCallingAssembly();

            return builder.WithPrompts(
                from t in assembly.GetTypes()
                where t.GetCustomAttribute<McpPluginPromptTypeAttribute>() is not null
                select t);
        }
    }
}