#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_EQS
    {
        [McpPluginTool
        (
            "EQS_GetObjectDetails",
            Title = "Get EQS Object Details"
        )]
        [Description(@"EQS object details retrieval tool - Query detailed information of game objects

Retrieves detailed information of specific game objects in the scene, providing object reference data for EQS queries. Supports multiple object finding methods and property extraction, commonly used for getting real-time status information of dynamic objects.

Finding methods: InstanceID lookup, name matching, scene traversal
Extracted properties: position, rotation, scale, name, tag, layer, health, speed, state, velocity, bounds, material
Use cases: Get player/enemy/NPC real-time position and status, query dynamic obstacle positions, collect environment object properties")]
        public string GetObjectDetails
        (
            [Description("Array of game object unique IDs. Supports InstanceID (numbers like '12345') or GameObject names (strings like 'Player')")]
            string[] objectIds,
            [Description("List of specific properties to retrieve. Available: position, rotation, scale, name, tag, layer, health, speed, state, velocity, angularVelocity, bounds, material. Leave empty to extract all basic properties.")]
            string[]? propertiesToRetrieve = null
        )
        => MainThread.Instance.Run(() =>
        {
            try
            {
                var objectDetails = new List<Dictionary<string, object>>();

                foreach (var objectId in objectIds)
                {
                    var objectInfo = new Dictionary<string, object>
                    {
                        ["id"] = objectId,
                        ["exists"] = false,
                        ["properties"] = new Dictionary<string, object>()
                    };

                    // Try to find object through different methods
                    GameObject gameObject = null;
                    
                    // Method 1: Find by InstanceID
                    if (int.TryParse(objectId, out var instanceId))
                    {
                        gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                    }
                    
                    // Method 2: Find by name
                    if (gameObject == null)
                    {
                        gameObject = GameObject.Find(objectId);
                    }
                    
                    // Method 3: Find in all loaded scenes
                    if (gameObject == null)
                    {
                        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                        {
                            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                            var rootObjects = scene.GetRootGameObjects();
                            foreach (var rootObj in rootObjects)
                            {
                                var found = rootObj.GetComponentsInChildren<Transform>()
                                    .FirstOrDefault(t => t.name == objectId || t.gameObject.GetInstanceID().ToString() == objectId);
                                if (found != null)
                                {
                                    gameObject = found.gameObject;
                                    break;
                                }
                            }
                            if (gameObject != null) break;
                        }
                    }

                    if (gameObject != null)
                    {
                        objectInfo["exists"] = true;
                        var properties = ExtractObjectProperties(gameObject, propertiesToRetrieve);
                        objectInfo["properties"] = properties;
                    }

                    objectDetails.Add(objectInfo);
                }

                return @$"[Success] Object details retrieved successfully.
# Object Details:
```json
{JsonUtils.Serialize(new { objects = objectDetails })}
```

# Summary:
- Queried objects: {objectIds.Length}
- Found objects: {objectDetails.Count(o => (bool)o["exists"])}
- Not found objects: {objectDetails.Count(o => !(bool)o["exists"])}
";
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to get object details: {ex.Message}";
            }
        });

        [McpPluginTool
        (
            "EQS_PlaceObjectAtLocation",
            Title = "Place Object at EQS Location"
        )]
        [Description(@"EQS object placement tool - Place game objects at query result locations

Places or moves game objects at specified locations based on EQS query results. Supports prefab instantiation, existing object movement, parent-child relationships, and coordinate system selection.

Supported operations: prefab instantiation, object movement, parent-child relationships, world/local coordinate systems
Finding strategies: Resources folder lookup, AssetDatabase search, scene object finding
Use cases: Place health packs/ammo based on EQS results, spawn enemies/NPCs at optimal positions, dynamically adjust object positions")]
        public string PlaceObjectAtLocation
        (
            [Description("Prefab name, path, or existing object ID to place. Examples: 'HealthPack' (prefab name), 'Assets/Prefabs/Enemy.prefab' (full path), '12345' (InstanceID for moving existing object)")]
            string objectPrefabIdOrName,
            [Description("Target world position coordinates [x,y,z]. Example: [10.5, 0, 20.3]")]
            float[] targetPosition,
            [Description("Target rotation Euler angles [x,y,z] in degrees. Example: [0, 90, 0] for 90-degree Y rotation. Optional.")]
            float[]? targetRotation = null,
            [Description("Custom name for the new game object instance. Optional. Example: 'PatrolGuard_01'")]
            string? instanceName = null,
            [Description("Parent object ID to attach the new instance to. Can be InstanceID (number) or GameObject name. Optional. Example: 'SpawnContainer' or '67890'")]
            string? parentObjectId = null,
            [Description("Whether to use world coordinate system. True = world space, False = local space relative to parent")]
            bool useWorldSpace = true
        )
        => MainThread.Instance.Run(() =>
        {
            try
            {
                if (targetPosition == null || targetPosition.Length != 3)
                {
                    return "[Error] Target position must be an array containing 3 elements [x,y,z].";
                }

                var position = new Vector3(targetPosition[0], targetPosition[1], targetPosition[2]);
                var rotation = Quaternion.identity;
                
                if (targetRotation != null && targetRotation.Length == 3)
                {
                    rotation = Quaternion.Euler(targetRotation[0], targetRotation[1], targetRotation[2]);
                }

                // Find prefab
                GameObject prefab = null;
                
                // Method 1: Direct path loading (if it looks like a full path)
                if (objectPrefabIdOrName.StartsWith("Assets/") && objectPrefabIdOrName.EndsWith(".prefab"))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(objectPrefabIdOrName);
                }
                
                // Method 2: Load through Resources
                if (prefab == null)
                {
                    prefab = Resources.Load<GameObject>(objectPrefabIdOrName);
                }
                
                // Method 3: Find through AssetDatabase by name
                if (prefab == null)
                {
                    // Extract just the file name for searching
                    var searchName = System.IO.Path.GetFileNameWithoutExtension(objectPrefabIdOrName);
                    var prefabPath = AssetDatabase.FindAssets($"t:GameObject {searchName}")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(path => path.Contains(searchName));
                    
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    }
                }
                
                // Method 4: Find existing objects in scene (for moving)
                GameObject existingObject = null;
                if (prefab == null)
                {
                    existingObject = GameObject.Find(objectPrefabIdOrName);
                    if (existingObject == null && int.TryParse(objectPrefabIdOrName, out var instanceId))
                    {
                        existingObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                    }
                }

                GameObject resultObject = null;
                string operationType = "";

                if (prefab != null)
                {
                    // Instantiate prefab
                    resultObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    operationType = "instantiated";
                }
                else if (existingObject != null)
                {
                    // Move existing object
                    resultObject = existingObject;
                    operationType = "moved";
                }
                else
                {
                    return Error.PrefabNotFound(objectPrefabIdOrName);
                }

                // Set position and rotation
                if (useWorldSpace)
                {
                    resultObject.transform.position = position;
                    resultObject.transform.rotation = rotation;
                }
                else
                {
                    resultObject.transform.localPosition = position;
                    resultObject.transform.localRotation = rotation;
                }

                // Set name
                if (!string.IsNullOrEmpty(instanceName))
                {
                    resultObject.name = instanceName;
                }

                // Set parent object
                if (!string.IsNullOrEmpty(parentObjectId))
                {
                    GameObject parentObject = null;
                    
                    if (int.TryParse(parentObjectId, out var parentInstanceId))
                    {
                        parentObject = EditorUtility.InstanceIDToObject(parentInstanceId) as GameObject;
                    }
                    else
                    {
                        parentObject = GameObject.Find(parentObjectId);
                    }

                    if (parentObject != null)
                    {
                        resultObject.transform.SetParent(parentObject.transform, useWorldSpace);
                    }
                }

                // Mark scene as modified
                EditorUtility.SetDirty(resultObject);
                if (resultObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(resultObject.scene);
                }

                var newObjectId = resultObject.GetInstanceID().ToString();

                return @$"[Success] Object placement successful.
# Operation Information:
- Operation Type: {operationType}
- Object Name: {resultObject.name}
- New Object ID: {newObjectId}
- Position: ({position.x:F2}, {position.y:F2}, {position.z:F2})
- Rotation: ({rotation.eulerAngles.x:F2}, {rotation.eulerAngles.y:F2}, {rotation.eulerAngles.z:F2})
- Parent Object: {(resultObject.transform.parent != null ? resultObject.transform.parent.name : "None")}

# Object Details:
```json
{JsonUtils.Serialize(Reflector.Instance.Serialize(resultObject, name: resultObject.name, recursive: false, logger: McpPlugin.Instance.Logger))}
```
";
            }
            catch (Exception ex)
            {
                return $"[Error] Object placement failed: {ex.Message}";
            }
        });

        [McpPluginTool
        (
            "EQS_GetEnvironmentStatus",
            Title = "Get EQS Environment Status"
        )]
        [Description(@"EQS environment status monitoring tool - Comprehensive understanding of EQS system runtime status

Provides complete status report of EQS system, including environment initialization status, grid information, cached queries, active visualizations, etc. Used for system monitoring, performance analysis, and troubleshooting.

Status information: environment initialization status, environment hash, grid statistics, object statistics, query cache, visualization status
Use cases: system health check, performance monitoring, debugging assistance, memory management
Key metrics: isInitialized, environmentHash, gridInfo, cachedQueriesCount, activeVisualizationsCount, staticGeometryCount, dynamicObjectsCount
Monitoring suggestions: regular status checks, cache size monitoring, grid occupancy optimization, query execution time tracking")]
        public string GetEnvironmentStatus()
        => MainThread.Instance.Run(() =>
        {
            try
            {
                var status = new Dictionary<string, object>
                {
                    ["isInitialized"] = _currentEnvironment != null,
                    ["environmentHash"] = _environmentHash,
                    ["cachedQueriesCount"] = _queryCache.Count,
                    ["activeVisualizationsCount"] = _activeVisualizations.Count
                };

                if (_currentEnvironment != null)
                {
                    status["lastUpdated"] = _currentEnvironment.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");
                    status["gridInfo"] = new Dictionary<string, object>
                    {
                        ["cellSize"] = _currentEnvironment.Grid.CellSize,
                        ["dimensions"] = new[] { _currentEnvironment.Grid.Dimensions.x, _currentEnvironment.Grid.Dimensions.y, _currentEnvironment.Grid.Dimensions.z },
                        ["origin"] = new[] { _currentEnvironment.Grid.Origin.x, _currentEnvironment.Grid.Origin.y, _currentEnvironment.Grid.Origin.z },
                        ["totalCells"] = _currentEnvironment.Grid.Cells.Length,
                        ["occupiedCells"] = _currentEnvironment.Grid.Cells.Count(c => c.StaticOccupancy || c.DynamicOccupants.Count > 0),
                        ["walkableCells"] = _currentEnvironment.Grid.Cells.Count(c => c.Properties.ContainsKey("isWalkable") && (bool)c.Properties["isWalkable"])
                    };
                    status["staticGeometryCount"] = _currentEnvironment.StaticGeometry.Count;
                    status["dynamicObjectsCount"] = _currentEnvironment.DynamicObjects.Count;
                }

                // Get cached queries summary
                var querySummary = new List<Dictionary<string, object>>();
                foreach (var kvp in _queryCache)
                {
                    querySummary.Add(new Dictionary<string, object>
                    {
                        ["queryId"] = kvp.Key,
                        ["status"] = kvp.Value.Status,
                        ["resultsCount"] = kvp.Value.Results.Count,
                        ["executionTime"] = kvp.Value.ExecutionTimeMs
                    });
                }
                status["cachedQueries"] = querySummary;

                // Get active visualizations summary
                var visualizationSummary = new List<Dictionary<string, object>>();
                foreach (var kvp in _activeVisualizations)
                {
                    visualizationSummary.Add(new Dictionary<string, object>
                    {
                        ["queryId"] = kvp.Key,
                        ["debugObjectsCount"] = kvp.Value.DebugObjects.Count,
                        ["expirationTime"] = kvp.Value.ExpirationTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                status["activeVisualizations"] = visualizationSummary;

                return @$"[Success] EQS environment status retrieved successfully.
# Environment Status:
```json
{JsonUtils.Serialize(status)}
```

# Status Summary:
- Environment Initialized: {(_currentEnvironment != null ? "Yes" : "No")}
- Environment Hash: {_environmentHash}
- Cached Queries Count: {_queryCache.Count}
- Active Visualizations Count: {_activeVisualizations.Count}
{(_currentEnvironment != null ? $@"
# Grid Information:
- Cell Size: {_currentEnvironment.Grid.CellSize}
- Grid Dimensions: {_currentEnvironment.Grid.Dimensions}
- Total Cells: {_currentEnvironment.Grid.Cells.Length}
- Occupied Cells: {_currentEnvironment.Grid.Cells.Count(c => c.StaticOccupancy || c.DynamicOccupants.Count > 0)}

# Object Statistics:
- Static Geometry: {_currentEnvironment.StaticGeometry.Count}
- Dynamic Objects: {_currentEnvironment.DynamicObjects.Count}" : "")}
";
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to get environment status: {ex.Message}";
            }
        });

        private static Dictionary<string, object> ExtractObjectProperties(GameObject gameObject, string[]? propertiesToRetrieve)
        {
            var properties = new Dictionary<string, object>();

            // Basic properties
            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("position"))
            {
                properties["position"] = new[] { gameObject.transform.position.x, gameObject.transform.position.y, gameObject.transform.position.z };
            }

            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("rotation"))
            {
                var euler = gameObject.transform.rotation.eulerAngles;
                properties["rotation"] = new[] { euler.x, euler.y, euler.z };
            }

            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("scale"))
            {
                properties["scale"] = new[] { gameObject.transform.localScale.x, gameObject.transform.localScale.y, gameObject.transform.localScale.z };
            }

            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("name"))
            {
                properties["name"] = gameObject.name;
            }

            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("tag"))
            {
                properties["tag"] = gameObject.tag;
            }

            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("layer"))
            {
                properties["layer"] = gameObject.layer;
            }

            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("active"))
            {
                properties["active"] = gameObject.activeInHierarchy;
            }

            // Component information
            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("components"))
            {
                var components = gameObject.GetComponents<UnityEngine.Component>()
                    .Select(c => c.GetType().Name)
                    .ToArray();
                properties["components"] = components;
            }

            // Bounds information
            if (propertiesToRetrieve == null || propertiesToRetrieve.Contains("bounds"))
            {
                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var bounds = renderer.bounds;
                    properties["bounds"] = new Dictionary<string, object>
                    {
                        ["center"] = new[] { bounds.center.x, bounds.center.y, bounds.center.z },
                        ["size"] = new[] { bounds.size.x, bounds.size.y, bounds.size.z },
                        ["min"] = new[] { bounds.min.x, bounds.min.y, bounds.min.z },
                        ["max"] = new[] { bounds.max.x, bounds.max.y, bounds.max.z }
                    };
                }
            }

            // Special component properties
            if (propertiesToRetrieve != null)
            {
                foreach (var property in propertiesToRetrieve)
                {
                    if (property.StartsWith("component."))
                    {
                        var componentName = property.Substring("component.".Length);
                        var component = gameObject.GetComponent(componentName);
                        if (component != null)
                        {
                            try
                            {
                                var serialized = Reflector.Instance.Serialize(component, recursive: false, logger: McpPlugin.Instance.Logger);
                                properties[property] = serialized;
                            }
                            catch
                            {
                                properties[property] = $"Failed to serialize {componentName}";
                            }
                        }
                    }
                }
            }

            return properties;
        }
    }
} 