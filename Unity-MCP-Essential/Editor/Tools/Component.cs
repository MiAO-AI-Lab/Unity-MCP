#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Component
    {
        static IEnumerable<Type> AllComponentTypes => AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(UnityEngine.Component).IsAssignableFrom(type) && !type.IsAbstract);

        public static class Error
        {
            static string ComponentsPrinted => string.Join("\n", AllComponentTypes.Select(type => type.FullName));

            public static string ComponentTypeIsEmpty()
                => "[Error] Component type is empty. Available components:\n" + ComponentsPrinted;
            public static string NotFoundComponentType(string typeName)
                => $"[Error] Component type '{typeName}' not found. Available components:\n" + ComponentsPrinted;

            public static string TypeMustBeComponent(string typeName)
                => $"[Error] Type '{typeName}' is not a component. Available components:\n" + ComponentsPrinted;
        }
    }
}