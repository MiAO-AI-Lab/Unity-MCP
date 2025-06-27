#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Common
{
    /// <summary>
    /// Provides functionality to execute methods dynamically, supporting both static and instance methods.
    /// Allows for parameter passing by position or by name, with support for default parameter values.
    /// </summary>
    public partial class RunTool : MethodWrapper, IRunTool
    {
        public string? Title { get; protected set; }

        /// <summary>
        /// Initializes the Command with the target method information.
        /// </summary>
        /// <param name="type">The type containing the static method.</param>
        public static RunTool CreateFromStaticMethod(Reflector reflector, ILogger? logger, MethodInfo methodInfo, string? title = null)
            => new RunTool(reflector, logger, methodInfo) { Title = title };

        /// <summary>
        /// Initializes the Command with the target instance method information.
        /// </summary>
        /// <param name="targetInstance">The instance of the object containing the method.</param>
        /// <param name="methodInfo">The MethodInfo of the instance method to execute.</param>
        public static RunTool CreateFromInstanceMethod(Reflector reflector, ILogger? logger, object targetInstance, MethodInfo methodInfo, string? title = null)
            => new RunTool(reflector, logger, targetInstance, methodInfo) { Title = title };

        /// <summary>
        /// Initializes the Command with the target instance method information.
        /// </summary>
        /// <param name="targetInstance">The instance of the object containing the method.</param>
        /// <param name="methodInfo">The MethodInfo of the instance method to execute.</param>
        public static RunTool CreateFromClassMethod(Reflector reflector, ILogger? logger, Type classType, MethodInfo methodInfo, string? title = null)
            => new RunTool(reflector, logger, classType, methodInfo) { Title = title };

        public RunTool(Reflector reflector, ILogger? logger, MethodInfo methodInfo) : base(reflector, logger, methodInfo) { }
        public RunTool(Reflector reflector, ILogger? logger, object targetInstance, MethodInfo methodInfo) : base(reflector, logger, targetInstance, methodInfo) { }
        public RunTool(Reflector reflector, ILogger? logger, Type classType, MethodInfo methodInfo) : base(reflector, logger, classType, methodInfo) { }

        /// <summary>
        /// Executes the target static method with the provided arguments.
        /// </summary>
        /// <param name="parameters">The arguments to pass to the method.</param>
        /// <returns>The result of the method execution, or null if the method is void.</returns>
        public async Task<ResponseCallTool> Run(params object?[] parameters)
        {
            // Invoke the method (static or instance)
            var result = await Invoke(parameters);
            return result as ResponseCallTool ?? ResponseCallTool.Success(result?.ToString());
        }

        /// <summary>
        /// Executes the target method with named parameters.
        /// Missing parameters will be filled with their default values or the type's default value if no default is defined.
        /// </summary>
        /// <param name="namedParameters">A dictionary mapping parameter names to their values.</param>
        /// <returns>The result of the method execution, or null if the method is void.</returns>
        public async Task<ResponseCallTool> Run(IReadOnlyDictionary<string, JsonElement>? namedParameters)
        {
            var result = await InvokeDict(namedParameters
                ?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value));
            return result as ResponseCallTool ?? ResponseCallTool.Success(result?.ToString());
        }
    }
}