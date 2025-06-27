#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_Physics
    {
        [McpPluginTool("Physics_GetLayerMaskInfo", Title = "LayerMask Information and Management Tool")]
        [Description(@"Unity LayerMask information management tool, providing complete Layer and LayerMask operation functionality.

Supported operation types:
- 'listAll': List all defined Layer names and indices
- 'calculate': Calculate LayerMask values based on Layer names or indices
- 'decode': Parse LayerMask values into Layer name lists
- 'sceneAnalysis': Analyze Layer usage in the current scene
- 'presets': Get common LayerMask preset values

Usage instructions:
1. Use 'listAll' to view all available Layers in the project
2. Use 'calculate' to calculate the required LayerMask value based on Layer names
3. Use 'decode' to see which Layers a LayerMask value contains
4. Use 'sceneAnalysis' to understand the Layer distribution in the current scene
5. Use 'presets' to get common LayerMask combinations

Returns detailed Layer information, including names, indices, LayerMask values, etc.")]
        public string LayerMaskInfo
        (
            [Description("Operation type: 'listAll'(list all Layers), 'calculate'(calculate LayerMask), 'decode'(parse LayerMask), 'sceneAnalysis'(scene analysis), 'presets'(common presets)")]
            string operation = "listAll",
            
            [Description("Layer name array, used for calculate operation. Example: [\"Default\", \"Water\", \"UI\"]")]
            string[] layerNames = null,
            
            [Description("Layer index array, used for calculate operation. Example: [0, 4, 5]")]
            int[] layerIndices = null,
            
            [Description("LayerMask value to parse, used for decode operation")]
            int layerMaskValue = 0,
            
            [Description("Whether to include detailed usage statistics, used for sceneAnalysis operation")]
            bool includeUsageStats = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(operation))
                    return Error.EmptyOperation();

                operation = operation.ToLower().Trim();
                var validOperations = new[] { "listall", "calculate", "decode", "sceneanalysis", "presets" };
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
                    
                    case "presets":
                        return GetLayerMaskPresets();
                    
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

        private static string GetLayerMaskPresets()
        {
            var presets = new List<object>
            {
                new { name = "All Layers", description = "Includes all layers", value = -1, hex = "0xFFFFFFFF", usage = "Detects all objects" },
                new { name = "Nothing", description = "Includes no layers", value = 0, hex = "0x0", usage = "Detects no objects" },
                new { name = "Default Only", description = "Only Default layer", value = 1, hex = "0x1", usage = "Detects only Default layer objects" },
                new { name = "Everything", description = "All layers (including user-defined layers)", value = ~0, hex = "0xFFFFFFFF", usage = "Detects all objects (same as All Layers)" }
            };

            // Add common Unity built-in Layer combinations
            var builtInCombinations = new List<object>();
            
            // Default + TransparentFX + Water + UI
            int commonLayers = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 5);
            builtInCombinations.Add(new 
            { 
                name = "Common Built-in Layers",
                description = "Default + TransparentFX + Water + UI",
                value = commonLayers,
                hex = "0x" + commonLayers.ToString("X"),
                layers = new[] { "Default", "TransparentFX", "Water", "UI" }
            });

            // Get actually defined Layers in current project
            var definedLayers = new List<object>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    definedLayers.Add(new
                    {
                        index = i,
                        name = layerName,
                        layerMaskValue = 1 << i,
                        hex = "0x" + (1 << i).ToString("X")
                    });
                }
            }

            var result = new
            {
                operation = "presets",
                commonPresets = presets,
                builtInCombinations = builtInCombinations,
                projectDefinedLayers = definedLayers,
                calculations = new
                {
                    howToCalculate = new
                    {
                        singleLayer = "LayerMask.GetMask(\"LayerName\") or (1 << layerIndex)",
                        multipleLayers = "LayerMask.GetMask(\"Layer1\", \"Layer2\") or (1 << index1) | (1 << index2)",
                        allExcept = "~LayerMask.GetMask(\"ExcludedLayer\")",
                        combine = "mask1 | mask2",
                        remove = "mask1 & ~mask2"
                    }
                }
            };

            var json = JsonUtils.Serialize(result);
            return $@"[Success] LayerMask preset information retrieval completed.
# Common presets:
- All layers: -1 (0xFFFFFFFF)
- No layer: 0 (0x0)  
- Default layer only: 1 (0x1)

# Project defined Layer count: {definedLayers.Count}

# LayerMask calculation method:
- Single Layer: LayerMask.GetMask(""LayerName"")
- Multiple Layers: LayerMask.GetMask(""Layer1"", ""Layer2"")
- Exclude Layer: ~LayerMask.GetMask(""ExcludedLayer"")

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
    }
}