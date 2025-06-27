#if !UNITY_5_3_OR_NEWER
using com.IvanMurzak.ReflectorNet.Model;
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
            Name = "ScriptableObject_Modify",
            Title = "Modify ScriptableObject asset"
        )]
        [Description("Modify ScriptableObject asset's fields and properties.")]
        public ValueTask<CallToolResponse> Modify
        (
            [Description("Asset path. Starts with 'Assets/'. Ends with '.asset'.")]
            string assetPath,
            [Description(@"Json Object with required readonly 'type' field.
Each field and property requires to have 'type' and 'name' fields to identify the exact modification target.
Follow the object schema to specify what to change, ignore values that should not be modified. Keep the original data structure.
Any unknown or wrong located fields and properties will be ignored.
Check the result of this command to see what was changed. The ignored fields and properties will be listed.")]
            SerializedMember assetDiff
        )
        {
            return ToolRouter.Call("ScriptableObject_Modify", arguments =>
            {
                arguments[nameof(assetPath)] = assetPath;
                arguments[nameof(assetDiff)] = assetDiff;
            });
        }
    }
}
#endif 