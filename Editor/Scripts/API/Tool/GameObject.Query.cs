#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_Find",
            Title = "Find GameObject in opened Prefab or in a Scene"
        )]
        [Description(@"Finds specific GameObject by provided information.
First it looks for the opened Prefab, if any Prefab is opened it looks only there ignoring a scene.
If no opened Prefab it looks into current active scene.
Returns GameObject information and its children.
Also, it returns Components preview just for the target GameObject.")]
        public string Find
        (
            GameObjectRef gameObjectRef,
            [Description("Determines the depth of the hierarchy to include. 0 - means only the target GameObject. 1 - means to include one layer below.")]
            int includeChildrenDepth = 0,
            [Description("If true, it will print only brief data of the target GameObject.")]
            bool briefData = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
                if (error != null)
                    return error;

                var serializedGo = Reflector.Instance.Serialize(
                    go,
                    name: go.name,
                    recursive: !briefData,
                    logger: McpPlugin.Instance.Logger
                );
                var json = JsonUtils.Serialize(serializedGo);
                return @$"[Success] Found GameObject.
# Data:
```json
{JsonUtils.Serialize(serializedGo)}
```

# Bounds:
```json
{JsonUtils.Serialize(go.CalculateBounds())}
```

# Hierarchy:
{go.ToMetadata(includeChildrenDepth).Print()}
";
            });
        }
    }
} 