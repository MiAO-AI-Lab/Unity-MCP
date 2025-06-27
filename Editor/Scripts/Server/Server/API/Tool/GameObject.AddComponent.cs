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
            Name = "GameObject_AddComponent",
            Title = "Add Component to a GameObject in opened Prefab or in a Scene"
        )]
        [Description("Add a component to a GameObject.")]
        public ValueTask<CallToolResponse> AddComponent
        (
            [Description("Full name of the Component. It should include full namespace path and the class name.")]
            string[] componentNames,
            GameObjectRef gameObjectRef
        )
        {
            return ToolRouter.Call("GameObject_AddComponent", arguments =>
            {
                arguments[nameof(componentNames)] = componentNames;
                arguments[nameof(gameObjectRef)] = gameObjectRef;
            });
        }
    }
}
#endif