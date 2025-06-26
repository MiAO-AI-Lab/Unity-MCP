#if !UNITY_5_3_OR_NEWER
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace com.IvanMurzak.Unity.MCP.Server.API
{
    public partial class Tool_ScriptableObject
    {
        [McpServerTool
        (
            Name = "ScriptableObject_Delete",
            Title = "Delete ScriptableObject asset"
        )]
        [Description("Delete ScriptableObject asset from the project.")]
        public ValueTask<CallToolResponse> Delete
        (
            [Description("Asset path. Starts with 'Assets/'. Ends with '.asset'.")]
            string assetPath
        )
        {
            return ToolRouter.Call("ScriptableObject_Delete", arguments =>
            {
                arguments[nameof(assetPath)] = assetPath;
            });
        }
    }
}
#endif 