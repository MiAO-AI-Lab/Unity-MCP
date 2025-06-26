#if !UNITY_5_3_OR_NEWER
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace com.IvanMurzak.Unity.MCP.Server.API
{
    public partial class Tool_Scene
    {
        [McpServerTool
        (
            Name = "Scene_Create",
            Title = "Create new scene"
        )]
        [Description("Create new scene in the project assets.")]
        public ValueTask<CallToolResponse> Create
        (
            [Description("Path to the scene file.")]
            string path
        )
        {
            return ToolRouter.Call("Scene_Create", arguments =>
            {
                arguments[nameof(path)] = path;
            });
        }
    }
}
#endif