#if !UNITY_5_3_OR_NEWER
using com.IvanMurzak.ReflectorNet.Model.Unity;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace com.IvanMurzak.Unity.MCP.Server.API
{
    public partial class Tool_GameObject
    {
        [McpServerTool
        (
            Name = "GameObject_Duplicate",
            Title = "Duplicate GameObjects in opened Prefab and in a Scene"
        )]
        [Description(@"Duplicate GameObjects in opened Prefab and in a Scene by 'instanceID' (int) array.")]
        public ValueTask<CallToolResponse> Duplicate
        (
            GameObjectRefList gameObjectRefs
        )
        {
            return ToolRouter.Call("GameObject_Duplicate", arguments =>
            {
                arguments[nameof(gameObjectRefs)] = gameObjectRefs;
            });
        }
    }
}
#endif