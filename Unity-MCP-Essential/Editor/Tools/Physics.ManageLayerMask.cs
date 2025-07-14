#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Physics
    {
        [McpPluginTool("Physics_ManageLayerMask", Title = "LayerMask Management Tool")]
        [Description(@"Unity LayerMask information management tool, providing complete Layer and LayerMask operation functionality.

Supported operation types:
- 'listAll': List all defined Layer names and indices
- 'calculate': Calculate LayerMask values based on Layer names or indices
- 'decode': Parse LayerMask values into Layer name lists
- 'sceneAnalysis': Analyze Layer usage in the current scene
- 'modifyLayer': Modify project Layer definitions (add, remove, rename)

Returns detailed Layer information, including names, indices, LayerMask values, etc.")]
        public string LayerMaskInfo
        (
            [Description("Operation type: 'listAll', 'calculate'(calculate LayerMask), 'decode'(parse LayerMask), 'sceneAnalysis', 'modifyLayer'(modify Layer definitions)")]
            string operation = "listAll",
            
            [Description("For calculate: Layer name array. Example: [\"Default\", \"Water\", \"UI\"]")]
            string[] layerNames = null,
            
            [Description("For calculate: Layer index array. Example: [0, 4, 5]")]
            int[] layerIndices = null,
            
            [Description("For decode: LayerMask value to parse")]
            int layerMaskValue = 0,
            
            [Description("For sceneAnalysis: Whether to include detailed usage statistics")]
            bool includeUsageStats = true,
            
            [Description("For modifyLayer: Layer modification operation type. Valid values: 'add', 'remove', 'rename'")]
            string modifyOperation = "add",
            
            [Description("For modifyLayer: Target Layer index. For 'add': specify index to add Layer at; For 'remove': specify index to remove; For 'rename': specify index to rename")]
            int targetLayerIndex = -1,
            
            [Description("For modifyLayer: New Layer name. For 'add' and 'rename' operations")]
            string newLayerName = null,
            
            [Description("For modifyLayer: Old Layer name. For 'rename' operation (optional, can use targetLayerIndex instead)")]
            string oldLayerName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(operation))
                    return Error.EmptyOperation();

                operation = operation.ToLower().Trim();
                var validOperations = new[] { "listall", "calculate", "decode", "sceneanalysis", "presets", "modifylayer" };
                if (System.Array.IndexOf(validOperations, operation) == -1)
                    return Error.InvalidOperation(operation);

                switch (operation)
                {
                    case "listall":
                        return ListAllLayers();
                    
                    case "calculate":
                        return CalculateLayerMask(layerNames, layerIndices);
                    
                    case "decode":
                        return DecodeLayerMask(layerMaskValue);
                    
                    case "sceneanalysis":
                        return AnalyzeSceneLayers(includeUsageStats);
                    
                    case "modifylayer":
                        return ModifyLayer(modifyOperation, targetLayerIndex, newLayerName, oldLayerName);
                    
                    default:
                        return Error.UnimplementedOperation(operation);
                }
            });
        }

        private static string ListAllLayers()
        {
            var layers = new List<object>();
            var usedLayers = new List<object>();
            var emptySlots = new List<int>();

            // Check all 32 Layer slots
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    var layerInfo = new
                    {
                        index = i,
                        name = layerName,
                        layerMaskValue = 1 << i,
                        layerMaskHex = "0x" + (1 << i).ToString("X"),
                        isBuiltIn = IsBuiltInLayer(i, layerName)
                    };
                    layers.Add(layerInfo);
                    usedLayers.Add(layerInfo);
                }
                else
                {
                    emptySlots.Add(i);
                }
            }

            var result = new
            {
                operation = "listAll",
                totalSlots = 32,
                usedSlots = usedLayers.Count,
                emptySlots = emptySlots.Count,
                layers = layers,
                emptySlotIndices = emptySlots,
                commonCalculations = new
                {
                    allLayers = -1,
                    allLayersHex = "0xFFFFFFFF",
                    defaultOnly = 1,
                    defaultOnlyHex = "0x1",
                    everything = ~0,
                    nothing = 0
                }
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] Layer information list retrieval completed.
# Layer usage:
Total slots: 32
Used slots: {usedLayers.Count}
Empty slots: {emptySlots.Count}

# Common LayerMask values:
- All layers: -1 (0xFFFFFFFF)
- Default only: 1 (0x1)
- No layer: 0 (0x0)

# Detailed data:
```json
{json}
```";
        }

        private static string CalculateLayerMask(string[] layerNames, int[] layerIndices)
        {
            if ((layerNames == null || layerNames.Length == 0) && (layerIndices == null || layerIndices.Length == 0))
                return Error.NoLayersSpecified();

            int calculatedMask = 0;
            var validLayers = new List<object>();
            var invalidLayers = new List<object>();

            // Process Layer names
            if (layerNames != null && layerNames.Length > 0)
            {
                foreach (var layerName in layerNames)
                {
                    if (string.IsNullOrEmpty(layerName))
                        continue;

                    int layerIndex = LayerMask.NameToLayer(layerName);
                    if (layerIndex >= 0)
                    {
                        calculatedMask |= (1 << layerIndex);
                        validLayers.Add(new
                        {
                            type = "name",
                            value = layerName,
                            index = layerIndex,
                            maskValue = 1 << layerIndex
                        });
                    }
                    else
                    {
                        invalidLayers.Add(new
                        {
                            type = "name",
                            value = layerName,
                            error = "Layer name not found"
                        });
                    }
                }
            }

            // Process Layer indices
            if (layerIndices != null && layerIndices.Length > 0)
            {
                foreach (var layerIndex in layerIndices)
                {
                    if (layerIndex < 0 || layerIndex >= 32)
                    {
                        invalidLayers.Add(new
                        {
                            type = "index",
                            value = layerIndex,
                            error = "Layer index out of range (0-31)"
                        });
                        continue;
                    }

                    string layerName = LayerMask.LayerToName(layerIndex);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        calculatedMask |= (1 << layerIndex);
                        validLayers.Add(new
                        {
                            type = "index",
                            value = layerIndex,
                            name = layerName,
                            maskValue = 1 << layerIndex
                        });
                    }
                    else
                    {
                        // Even if Layer name is empty, the index is still valid
                        calculatedMask |= (1 << layerIndex);
                        validLayers.Add(new
                        {
                            type = "index",
                            value = layerIndex,
                            name = $"<Empty Layer {layerIndex}>",
                            maskValue = 1 << layerIndex
                        });
                    }
                }
            }

            var result = new
            {
                operation = "calculate",
                calculatedLayerMask = calculatedMask,
                layerMaskHex = "0x" + calculatedMask.ToString("X"),
                layerMaskBinary = System.Convert.ToString(calculatedMask, 2).PadLeft(32, '0'),
                validLayersCount = validLayers.Count,
                invalidLayersCount = invalidLayers.Count,
                validLayers = validLayers,
                invalidLayers = invalidLayers,
                unityAPIUsage = new
                {
                    physicsRaycast = $"Physics.Raycast(origin, direction, maxDistance, {calculatedMask})",
                    layerMaskGetMask = layerNames != null && layerNames.Length > 0 ? 
                        $"LayerMask.GetMask({string.Join(", ", layerNames.Where(n => !string.IsNullOrEmpty(n)).Select(n => $"\"{n}\""))})" : 
                        "N/A (no valid layer names provided)"
                }
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] LayerMask calculation completed.
# Calculation result:
LayerMask value: {calculatedMask}
Hexadecimal: 0x{calculatedMask:X}
Binary: {System.Convert.ToString(calculatedMask, 2).PadLeft(32, '0')}

# Layer statistics:
Valid layers: {validLayers.Count}
Invalid layers: {invalidLayers.Count}

# Unity API usage example:
Physics.Raycast(origin, direction, maxDistance, {calculatedMask})

# Detailed data:
```json
{json}
```";
        }

        private static string DecodeLayerMask(int layerMaskValue)
        {
            var decodedLayers = new List<object>();
            var layerIndices = new List<int>();

            // Parse all Layers contained in LayerMask
            for (int i = 0; i < 32; i++)
            {
                if ((layerMaskValue & (1 << i)) != 0)
                {
                    layerIndices.Add(i);
                    string layerName = LayerMask.LayerToName(i);
                    decodedLayers.Add(new
                    {
                        index = i,
                        name = !string.IsNullOrEmpty(layerName) ? layerName : $"<Empty Layer {i}>",
                        maskValue = 1 << i,
                        maskValueHex = "0x" + (1 << i).ToString("X"),
                        isEmpty = string.IsNullOrEmpty(layerName)
                    });
                }
            }

            var result = new
            {
                operation = "decode",
                inputLayerMask = layerMaskValue,
                inputLayerMaskHex = "0x" + layerMaskValue.ToString("X"),
                inputLayerMaskBinary = System.Convert.ToString(layerMaskValue, 2).PadLeft(32, '0'),
                layerCount = decodedLayers.Count,
                layerIndices = layerIndices,
                decodedLayers = decodedLayers,
                isSpecialValue = GetSpecialMaskDescription(layerMaskValue)
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] LayerMask parsing completed.
# Input value:
LayerMask: {layerMaskValue}
Hexadecimal: 0x{layerMaskValue:X}
Binary: {System.Convert.ToString(layerMaskValue, 2).PadLeft(32, '0')}

# Parsing result:
Layer count: {decodedLayers.Count}
Layer indices: [{string.Join(", ", layerIndices)}]

# Detailed data:
```json
{json}
```";
        }

        private static string AnalyzeSceneLayers(bool includeUsageStats)
        {
            var sceneObjects = Object.FindObjectsOfType<GameObject>();
            var layerUsage = new Dictionary<int, int>();
            var layerObjects = new Dictionary<int, List<string>>();

            // Statistics of Layer usage in the scene
            foreach (var obj in sceneObjects)
            {
                int layer = obj.layer;
                
                if (!layerUsage.ContainsKey(layer))
                {
                    layerUsage[layer] = 0;
                    layerObjects[layer] = new List<string>();
                }
                
                layerUsage[layer]++;
                
                if (includeUsageStats && layerObjects[layer].Count < 10) // Limit sample object count
                {
                    layerObjects[layer].Add(obj.name);
                }
            }

            var layerStats = layerUsage.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new
                {
                    layerIndex = kvp.Key,
                    layerName = !string.IsNullOrEmpty(LayerMask.LayerToName(kvp.Key)) ? 
                        LayerMask.LayerToName(kvp.Key) : $"<Empty Layer {kvp.Key}>",
                    objectCount = kvp.Value,
                    layerMaskValue = 1 << kvp.Key,
                    sampleObjects = includeUsageStats ? layerObjects[kvp.Key].Take(5).ToArray() : new string[0]
                }).ToList();

            var result = new
            {
                operation = "sceneAnalysis",
                totalGameObjects = sceneObjects.Length,
                uniqueLayersUsed = layerUsage.Count,
                includeUsageStats = includeUsageStats,
                layerStats = layerStats,
                mostUsedLayer = layerStats.FirstOrDefault(),
                leastUsedLayer = layerStats.LastOrDefault(),
                recommendations = GenerateLayerRecommendations(layerStats)
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] Scene Layer analysis completed.
# Scene statistics:
Total GameObjects: {sceneObjects.Length}
Used Layer count: {layerUsage.Count}

# Most used Layer:
{(layerStats.Any() ? $"{layerStats.First().layerName} (index {layerStats.First().layerIndex}): {layerStats.First().objectCount} objects" : "None")}

# Detailed data:
```json
{json}
```";
        }

        private static bool IsBuiltInLayer(int index, string name)
        {
            // Unity built-in Layers
            var builtInLayers = new Dictionary<int, string>
            {
                { 0, "Default" },
                { 1, "TransparentFX" },
                { 2, "Ignore Raycast" },
                { 4, "Water" },
                { 5, "UI" }
            };

            return builtInLayers.ContainsKey(index) && builtInLayers[index] == name;
        }

        private static string GetSpecialMaskDescription(int maskValue)
        {
            switch (maskValue)
            {
                case -1:
                    return "All Layers";
                case 0:
                    return "Nothing";
                case 1:
                    return "Default Layer Only";
                default:
                    if (maskValue == ~0)
                        return "Everything";
                    return "Custom LayerMask";
            }
        }

        private static List<string> GenerateLayerRecommendations(System.Collections.IEnumerable layerStats)
        {
            var recommendations = new List<string>();
            
            var statsList = layerStats.Cast<object>().ToList();
            
            if (statsList.Count > 10)
            {
                recommendations.Add("Scene uses many different Layers, consider consolidating objects with similar functions into the same Layer");
            }
            
            if (statsList.Any())
            {
                recommendations.Add("When using LayerMask for physics detection, recommend targeting specific Layers rather than all Layers to improve performance");
                recommendations.Add("Assign appropriate Layers to different types of objects for collision detection and rendering optimization");
            }

            return recommendations;
        }

        private static string ModifyLayer(string modifyOperation, int targetLayerIndex, string newLayerName, string oldLayerName)
        {
            try
            {
                if (string.IsNullOrEmpty(modifyOperation))
                    return Error.InvalidOperation("modifyOperation cannot be null or empty");

                modifyOperation = modifyOperation.ToLower().Trim();
                var validModifyOperations = new[] { "add", "remove", "rename" };
                if (System.Array.IndexOf(validModifyOperations, modifyOperation) == -1)
                    return Error.InvalidOperation($"modifyOperation '{modifyOperation}' not supported. Valid operations: {string.Join(", ", validModifyOperations)}");

                // Get TagManager asset
                var tagManager = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layersProp = tagManager.FindProperty("layers");

                if (layersProp == null)
                    return Error.TagManagerAccessFailed();

                switch (modifyOperation)
                {
                    case "add":
                        return AddLayer(layersProp, targetLayerIndex, newLayerName, tagManager);
                    
                    case "remove":
                        return RemoveLayer(layersProp, targetLayerIndex, oldLayerName, tagManager);
                    
                    case "rename":
                        return RenameLayer(layersProp, targetLayerIndex, newLayerName, oldLayerName, tagManager);
                    
                    default:
                        return Error.UnimplementedOperation(modifyOperation);
                }
            }
            catch (System.Exception ex)
            {
                return Error.LayerModificationFailed(ex.Message);
            }
        }

        private static string AddLayer(UnityEditor.SerializedProperty layersProp, int targetLayerIndex, string newLayerName, UnityEditor.SerializedObject tagManager)
        {
            if (string.IsNullOrEmpty(newLayerName))
                return Error.LayerNameRequired("add");

            // Validate layer name
            if (newLayerName.Length > 32)
                return Error.LayerNameTooLong();

            // Check if layer name already exists
            for (int i = 0; i < 32; i++)
            {
                string existingName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(existingName) && existingName.Equals(newLayerName, System.StringComparison.OrdinalIgnoreCase))
                    return Error.LayerNameAlreadyExists(newLayerName, i);
            }

            // Find target index
            int addIndex = targetLayerIndex;
            if (addIndex < 0)
            {
                // Find first available slot starting from index 8 (user-defined layers)
                addIndex = -1;
                for (int i = 8; i < 32; i++)
                {
                    string existingName = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(existingName))
                    {
                        addIndex = i;
                        break;
                    }
                }
            }

            if (addIndex < 0 || addIndex >= 32)
                return Error.InvalidTargetLayerIndex();

            // Check if target index is built-in layer (0-7)
            if (addIndex < 8 && !string.IsNullOrEmpty(LayerMask.LayerToName(addIndex)))
                return Error.CannotModifyBuiltInLayer(addIndex);

            // Check if target slot is already occupied
            string currentName = LayerMask.LayerToName(addIndex);
            if (!string.IsNullOrEmpty(currentName))
                return Error.LayerSlotOccupied(addIndex, currentName);

            // Set the layer name
            var layerProp = layersProp.GetArrayElementAtIndex(addIndex);
            layerProp.stringValue = newLayerName;
            tagManager.ApplyModifiedProperties();

            // Refresh AssetDatabase
            UnityEditor.AssetDatabase.Refresh();

            var result = new
            {
                operation = "modifyLayer",
                modifyOperation = "add",
                success = true,
                layerIndex = addIndex,
                layerName = newLayerName,
                layerMaskValue = 1 << addIndex,
                layerMaskHex = "0x" + (1 << addIndex).ToString("X")
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] Layer '{newLayerName}' added successfully.
# Layer information:
Index: {addIndex}
Name: {newLayerName}
LayerMask value: {1 << addIndex}
LayerMask hex: 0x{(1 << addIndex):X}

# Detailed data:
```json
{json}
```";
        }

        private static string RemoveLayer(UnityEditor.SerializedProperty layersProp, int targetLayerIndex, string oldLayerName, UnityEditor.SerializedObject tagManager)
        {
            int removeIndex = targetLayerIndex;
            
            // If index not specified, try to find by name
            if (removeIndex < 0 && !string.IsNullOrEmpty(oldLayerName))
            {
                removeIndex = LayerMask.NameToLayer(oldLayerName);
                if (removeIndex < 0)
                    return Error.LayerNotFound(oldLayerName);
            }

            if (removeIndex < 0 || removeIndex >= 32)
                return Error.InvalidLayerIndex();

            // Check if it's a built-in layer
            if (removeIndex < 8 && IsBuiltInLayer(removeIndex, LayerMask.LayerToName(removeIndex)))
                return Error.CannotRemoveBuiltInLayer(removeIndex);

            string currentName = LayerMask.LayerToName(removeIndex);
            if (string.IsNullOrEmpty(currentName))
                return Error.LayerAlreadyEmpty(removeIndex);

            // Set the layer name to empty
            var layerProp = layersProp.GetArrayElementAtIndex(removeIndex);
            layerProp.stringValue = "";
            tagManager.ApplyModifiedProperties();

            // Refresh AssetDatabase
            UnityEditor.AssetDatabase.Refresh();

            var result = new
            {
                operation = "modifyLayer",
                modifyOperation = "remove",
                success = true,
                layerIndex = removeIndex,
                removedLayerName = currentName
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] Layer '{currentName}' removed successfully.
# Removed layer information:
Index: {removeIndex}
Name: {currentName}

# Detailed data:
```json
{json}
```";
        }

        private static string RenameLayer(UnityEditor.SerializedProperty layersProp, int targetLayerIndex, string newLayerName, string oldLayerName, UnityEditor.SerializedObject tagManager)
        {
            if (string.IsNullOrEmpty(newLayerName))
                return Error.LayerNameRequired("rename");

            // Validate new layer name
            if (newLayerName.Length > 32)
                return Error.LayerNameTooLong();

            int renameIndex = targetLayerIndex;
            
            // If index not specified, try to find by old name
            if (renameIndex < 0 && !string.IsNullOrEmpty(oldLayerName))
            {
                renameIndex = LayerMask.NameToLayer(oldLayerName);
                if (renameIndex < 0)
                    return Error.LayerNotFound(oldLayerName);
            }

            if (renameIndex < 0 || renameIndex >= 32)
                return Error.InvalidLayerIndex();

            string currentName = LayerMask.LayerToName(renameIndex);
            if (string.IsNullOrEmpty(currentName))
                return Error.LayerSlotEmpty(renameIndex);

            // Check if it's a built-in layer
            if (renameIndex < 8 && IsBuiltInLayer(renameIndex, currentName))
                return Error.CannotRenameBuiltInLayer(currentName, renameIndex);

            // Check if new name already exists
            for (int i = 0; i < 32; i++)
            {
                if (i == renameIndex) continue;
                string existingName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(existingName) && existingName.Equals(newLayerName, System.StringComparison.OrdinalIgnoreCase))
                    return Error.LayerNameAlreadyExists(newLayerName, i);
            }

            // Set the new layer name
            var layerProp = layersProp.GetArrayElementAtIndex(renameIndex);
            layerProp.stringValue = newLayerName;
            tagManager.ApplyModifiedProperties();

            // Refresh AssetDatabase
            UnityEditor.AssetDatabase.Refresh();

            var result = new
            {
                operation = "modifyLayer",
                modifyOperation = "rename",
                success = true,
                layerIndex = renameIndex,
                oldLayerName = currentName,
                newLayerName = newLayerName,
                layerMaskValue = 1 << renameIndex,
                layerMaskHex = "0x" + (1 << renameIndex).ToString("X")
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] Layer renamed successfully.
# Layer information:
Index: {renameIndex}
Old name: {currentName}
New name: {newLayerName}
LayerMask value: {1 << renameIndex}
LayerMask hex: 0x{(1 << renameIndex):X}

# Detailed data:
```json
{json}
```";
        }
    }
}