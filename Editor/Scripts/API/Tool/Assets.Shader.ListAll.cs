#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Linq;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.Unity.MCP.Utils;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_Shader
    {
        [McpPluginTool
        (
            "Assets_Shader_ListAll",
            Title = "List all shader names"
        )]
        [Description(@"Scans the project assets to find all shaders and to get the name from each of them. Returns the list of shader names.")]
        public string ListAll() => MainThread.Instance.Run(() =>
        {
            var shaderNames = ShaderUtils.GetAllShaders()
                .Where(shader => shader != null)
                .Select(shader => shader.name)
                .OrderBy(name => name)
                .ToList();

            return "[Success] List of all shader names in the project:\n" + string.Join("\n", shaderNames);
        });
    }
}