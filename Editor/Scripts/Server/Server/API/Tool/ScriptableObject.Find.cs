#if !UNITY_5_3_OR_NEWER
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace com.MiAO.Unity.MCP.Server.API
{
    public partial class Tool_ScriptableObject
    {
        [McpServerTool
        (
            Name = "ScriptableObject_Find",
            Title = "Find ScriptableObject asset"
        )]
        [Description("Find ScriptableObject asset by path and return its data.")]
        public ValueTask<CallToolResponse> Find
        (
            [Description("Asset path. Starts with 'Assets/'. Ends with '.asset'.")]
            string assetPath,
            [Description("If true, it will print only brief data of the ScriptableObject.")]
            bool briefData = false
        )
        {
            return ToolRouter.Call("ScriptableObject_Find", arguments =>
            {
                arguments[nameof(assetPath)] = assetPath;
                arguments[nameof(briefData)] = briefData;
            });
        }
    }
}
#endif 