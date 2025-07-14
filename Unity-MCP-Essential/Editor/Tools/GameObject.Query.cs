#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
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

                // Check for missing components
                var components = go.GetComponents<UnityEngine.Component>();
                var componentsPreview = new List<object>();
                var missingComponents = new List<string>();

                // Check each component and detect missing scripts
                for (int i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null)
                    {
                        // This is a missing component - use GameObjectUtils public method
                        var missingInfo = GameObjectUtils.GetMissingComponentInfo(go, i);
                        missingComponents.Add($"[{i}] Missing Component: {missingInfo}");
                        componentsPreview.Add(new { Name = $"[{i}]", Status = "Missing", Info = missingInfo });
                    }
                    else
                    {
                        // Normal component - serialize it (only if not briefData)
                        if (!briefData)
                        {
                            try
                            {
                                var serialized = Reflector.Instance.Serialize(
                                    component,
                                    name: $"[{i}]",
                                    recursive: false,
                                    logger: McpPlugin.Instance.Logger
                                );
                                componentsPreview.Add(serialized);
                            }
                            catch (System.Exception ex)
                            {
                                // If serialization fails, add basic info
                                componentsPreview.Add(new { Name = $"[{i}]", Type = component.GetType().Name, Status = "SerializationError", Error = ex.Message });
                            }
                        }
                        else
                        {
                            // Brief data - just add basic info
                            componentsPreview.Add(new { Name = $"[{i}]", Type = component.GetType().Name, Status = "OK" });
                        }
                    }
                }

                var result = @$"[Success] Found GameObject.";
                
                if (missingComponents.Count > 0)
                {
                    result += $"\n\n# ⚠️ Missing Components Detected ({missingComponents.Count}):\n" + string.Join("\n", missingComponents);
                }

                if (!briefData)
                {
                    try
                    {
                        var serializedGo = Reflector.Instance.Serialize(
                            go,
                            name: go.name,
                            recursive: true,
                            logger: McpPlugin.Instance.Logger
                        );
                        
                        result += @$"

# Data:
```json
{JsonUtils.Serialize(serializedGo)}
```

# Bounds:
```json
{JsonUtils.Serialize(go.CalculateBounds())}
```
";
                    }
                    catch (System.Exception ex)
                    {
                        result += @$"

# Data:
⚠️ GameObject serialization failed due to missing components: {ex.Message}
Basic GameObject info: instanceID={go.GetInstanceID()}, name={go.name}, active={go.activeInHierarchy}
";
                    }
                }

                result += @$"

# Components Preview:
```json
{JsonUtils.Serialize(componentsPreview)}
```

# Layer Info:
```json
{JsonUtils.Serialize(new { LayerIndex = go.layer, LayerName = LayerMask.LayerToName(go.layer) })}
```

# Hierarchy:
{go.ToMetadata(includeChildrenDepth).Print()}
";
                return result;
            });
        }
    }
} 