#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.ReflectorNet;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class ToolRunnerCollection : Dictionary<string, IRunTool>
    {
        readonly Reflector reflector;
        readonly ILogger? _logger;

        public ToolRunnerCollection(Reflector reflector, ILogger? logger)
        {
            this.reflector = reflector ?? throw new ArgumentNullException(nameof(reflector));
            _logger = logger;
            _logger?.LogTrace("Ctor.");
        }
        public ToolRunnerCollection Add(IEnumerable<ToolMethodData> methods)
        {
            foreach (var method in methods.Where(resource => !string.IsNullOrEmpty(resource.Attribute?.Name)))
            {
                this[method.Attribute.Name!] = method.MethodInfo.IsStatic
                    ? RunTool.CreateFromStaticMethod(reflector, _logger, method.MethodInfo, method.Attribute.Title) as IRunTool
                    : RunTool.CreateFromClassMethod(reflector, _logger, method.ClassType, method.MethodInfo, method.Attribute.Title);
            }
            return this;
        }
        public ToolRunnerCollection Add(IDictionary<string, IRunTool> runners)
        {
            if (runners == null)
                throw new ArgumentNullException(nameof(runners));

            foreach (var runner in runners)
                Add(runner.Key, runner.Value);

            return this;
        }
    }
}