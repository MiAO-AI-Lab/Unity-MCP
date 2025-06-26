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
            Name = "GameObject_DestroyComponents",
            Title = "Destroy Components from a GameObject in opened Prefab or in a Scene"
        )]
        [Description("Destroy one or many components from target GameObject.")]
        public ValueTask<CallToolResponse> DestroyComponents
        (
            GameObjectRef gameObjectRef,
            ComponentRefList destroyComponentRefs
        )
        {
            return ToolRouter.Call("GameObject_DestroyComponents", arguments =>
            {
                arguments[nameof(gameObjectRef)] = gameObjectRef;
                arguments[nameof(destroyComponentRefs)] = destroyComponentRefs ?? new();
            });
        }
    }
}
#endif