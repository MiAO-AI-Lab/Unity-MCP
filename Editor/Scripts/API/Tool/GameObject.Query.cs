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
            [Description("If true, show detailed GameObject data including serialized information. Default is false.")]
            bool showGameObjectSerializedDetails = false,
            [Description("If true, show GameObject bounds information. Default is false.")]
            bool showBounds = false,
            [Description("If true, show layer information. Default is false.")]
            bool showLayer = false,
            [Description("If true, show components preview with detailed serialization. Default is true.")]
            bool showComponentsPreview = true,
            [Description("If true, show related enum information for the GameObject. Default is false.")]
            bool showRelatedEnums = false
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
                        // Normal component - serialize it based on showComponentsPreview setting
                        if (showComponentsPreview)
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
                            // Basic info only
                            componentsPreview.Add(new { Name = $"[{i}]", Type = component.GetType().Name, Status = "OK" });
                        }
                    }
                }

                var result = @$"[Success] Found GameObject. Basic GameObject info: instanceID={go.GetInstanceID()}, name={go.name}, active={go.activeInHierarchy}";
                
                if (missingComponents.Count > 0)
                {
                    result += $"\n\n# ⚠️ Missing Components Detected ({missingComponents.Count}):\n" + string.Join("\n", missingComponents);
                }

                // Show GameObject details if requested
                if (showGameObjectSerializedDetails)
                {
                    try
                    {
                        Debug.Log($"go.GetType(): {go.GetType()}");
                        var serializedGo = Reflector.Instance.Serialize(
                            go,
                            type: go.GetType(),
                            name: go.name,
                            recursive: true,
                            logger: McpPlugin.Instance.Logger
                        );
                        
                        result += @$"

# GameObject Details:
```json
{JsonUtils.Serialize(serializedGo)}
```";
                    }
                    catch (System.Exception ex)
                    {
                        result += @$"

# GameObject Details:
⚠️ Object {go.name} serialization failed due to: {ex.Message}";
                    }
                }

                // Show bounds if requested
                if (showBounds)
                {
                    result += @$"

# Bounds:
```json
{JsonUtils.Serialize(go.CalculateBounds())}
```";
                }

                // Show components preview if requested
                if (showComponentsPreview && componentsPreview.Count > 0)
                {
                    result += @$"

# Components Preview:
```json
{JsonUtils.Serialize(componentsPreview)}
```";
                }

                // Show layer info if requested
                if (showLayer)
                {
                    result += @$"

# Layer Info:
```json
{JsonUtils.Serialize(new { LayerIndex = go.layer, LayerName = LayerMask.LayerToName(go.layer) })}
```";
                }

                // Show related enums if requested
                if (showRelatedEnums)
                {
                    var enumInfo = new List<object>();
                    
                    // Add enum fields from all components
                    for (int i = 0; i < components.Length; i++)
                    {
                        var component = components[i];
                        if (component == null) continue; // Skip missing components

                        var componentType = component.GetType();
                        var componentEnums = new List<object>();

                        // Get all fields
                        var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | 
                                                           System.Reflection.BindingFlags.NonPublic | 
                                                           System.Reflection.BindingFlags.Instance);
                        
                        foreach (var field in fields)
                        {
                            if (field.FieldType.IsEnum)
                            {
                                try
                                {
                                    var currentValue = field.GetValue(component);
                                    var enumValues = System.Enum.GetValues(field.FieldType);
                                    var enumNames = System.Enum.GetNames(field.FieldType);
                                    
                                    var enumDetails = new List<object>();
                                    for (int j = 0; j < enumValues.Length; j++)
                                    {
                                        enumDetails.Add(new { 
                                            Name = enumNames[j], 
                                            Value = (int)enumValues.GetValue(j),
                                            IsCurrent = currentValue.Equals(enumValues.GetValue(j))
                                        });
                                    }

                                    componentEnums.Add(new {
                                        FieldName = field.Name,
                                        FieldType = field.FieldType.Name,
                                        CurrentValue = currentValue?.ToString(),
                                        CurrentIntValue = (int)currentValue,
                                        AllValues = enumDetails
                                    });
                                }
                                catch (System.Exception ex)
                                {
                                    componentEnums.Add(new {
                                        FieldName = field.Name,
                                        FieldType = field.FieldType.Name,
                                        Error = $"Failed to read enum: {ex.Message}"
                                    });
                                }
                            }
                        }

                        // Get all properties
                        var properties = componentType.GetProperties(System.Reflection.BindingFlags.Public | 
                                                                   System.Reflection.BindingFlags.NonPublic | 
                                                                   System.Reflection.BindingFlags.Instance);
                        
                        foreach (var property in properties)
                        {
                            if (property.PropertyType.IsEnum && property.CanRead)
                            {
                                try
                                {
                                    var currentValue = property.GetValue(component);
                                    var enumValues = System.Enum.GetValues(property.PropertyType);
                                    var enumNames = System.Enum.GetNames(property.PropertyType);
                                    
                                    var enumDetails = new List<object>();
                                    for (int j = 0; j < enumValues.Length; j++)
                                    {
                                        enumDetails.Add(new { 
                                            Name = enumNames[j], 
                                            Value = (int)enumValues.GetValue(j),
                                            IsCurrent = currentValue.Equals(enumValues.GetValue(j))
                                        });
                                    }

                                    componentEnums.Add(new {
                                        PropertyName = property.Name,
                                        PropertyType = property.PropertyType.Name,
                                        CurrentValue = currentValue?.ToString(),
                                        CurrentIntValue = (int)currentValue,
                                        AllValues = enumDetails
                                    });
                                }
                                catch (System.Exception ex)
                                {
                                    componentEnums.Add(new {
                                        PropertyName = property.Name,
                                        PropertyType = property.PropertyType.Name,
                                        Error = $"Failed to read enum: {ex.Message}"
                                    });
                                }
                            }
                        }

                        // Add component enum info if any enums were found
                        if (componentEnums.Count > 0)
                        {
                            enumInfo.Add(new {
                                ComponentIndex = i,
                                ComponentType = componentType.Name,
                                ComponentFullName = componentType.FullName,
                                EnumFields = componentEnums
                            });
                        }
                    }

                    result += @$"

# Related Enums:
```json
{JsonUtils.Serialize(enumInfo)}
```";
                }

                result += @$"

# Hierarchy:
{go.ToMetadata(includeChildrenDepth).Print()}";
                
                return result;
            });
        }
    }
} 