#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.Linq;
using com.MiAO.Unity.MCP.Common;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Component
    {
        [McpPluginTool
        (
            "Component_GetAll",
            Title = "Get list of all Components"
        )]
        [Description("Returns the list of all available components in the project.")]
        public string GetAll
        (
            [Description("Substring for searching components. Could be empty.")]
            string? search = null
        )
        {
            var componentTypes = AllComponentTypes
                .Select(type => type.FullName)
                .ToList();

            if (!string.IsNullOrEmpty(search))
            {
                componentTypes = componentTypes
                    .Where(typeName => typeName != null && typeName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return string.Join("\n", componentTypes);
        }
    }
}