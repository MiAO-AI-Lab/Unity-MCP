#if !UNITY_5_3_OR_NEWER
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace com.MiAO.MCP.Server.API
{
    public partial class Tool_ScriptableObject
    {
        [McpServerTool
        (
            Name = "ScriptableObject_Create",
            Title = "Create a new ScriptableObject asset"
        )]
        [Description("Create new ScriptableObject asset with default parameters. The type name should include the full namespace path.")]
        public ValueTask<CallToolResponse> Create
        (
            [Description("Asset path. Starts with 'Assets/'. Ends with '.asset'.")]
            string assetPath,
            [Description("Full name of the ScriptableObject type. It should include full namespace path and the class name.")]
            string typeName
        )
        {
            return ToolRouter.Call("ScriptableObject_Create", arguments =>
            {
                arguments[nameof(assetPath)] = assetPath;
                arguments[nameof(typeName)] = typeName;
            });
        }
    }
}
#endif 