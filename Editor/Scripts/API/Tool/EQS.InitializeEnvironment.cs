#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_EQS
    {
        [McpPluginTool
        (
            "EQS_InitializeEnvironment",
            Title = "Initialize EQS Environment"
        )]
        [Description(@"EQS Environment Initialization Tool - Builds the foundation for spatial queries
Converts Unity scenes into 3D grid spaces that EQS can query, collects static geometry and dynamic object information, providing data for subsequent spatial queries.
Returns information:
- Environment Hash (for cache validation)
- Grid Statistics (total cells, occupied cells, walkable cells)
- Object Statistics (static geometry count, dynamic object count)
- Execution Time (performance monitoring)")]
        public string InitializeEnvironment
        (
            [Description("Scene/level ID to process. If omitted, uses the current active scene.")]
            string? sceneIdentifier = null,
            [Description("Whether to include static geometry (buildings, terrain, etc.)")]
            bool includeStaticGeometry = true,
            [Description("Whether to include dynamic objects (characters, vehicles, etc.)")]
            bool includeDynamicObjects = true,
            [Description("Only include dynamic objects with these tags, e.g. ['Player', 'Enemy']")]
            string[]? dynamicObjectTagsFilter = null,
            [Description("Grid cell size in meters, affects query precision and performance, default 1.0 meters")]
            float? gridCellSizeOverride = null,
            [Description("Force specify grid dimensions {x,y,z}, overrides automatic calculation")]
            Vector3Int? gridDimensionsOverride = null,
            [Description("Whether to force re-initialization even if environment already exists. Default true always reinitializes")]
            bool forceReinitialize = true
        )
        => MainThread.Instance.Run(() =>
        {
            try
            {
                var startTime = DateTime.Now;
                
                // Get target scene
                Scene targetScene;
                if (string.IsNullOrEmpty(sceneIdentifier))
                {
                    targetScene = SceneManager.GetActiveScene();
                }
                else
                {
                    targetScene = SceneManager.GetSceneByName(sceneIdentifier);
                    if (!targetScene.IsValid())
                    {
                        return Error.InvalidSceneIdentifier(sceneIdentifier);
                    }
                }

                // Generate hash value for current scene configuration for cache checking
                var currentConfigHash = GenerateConfigurationHash(targetScene, includeStaticGeometry, includeDynamicObjects, 
                    dynamicObjectTagsFilter, gridCellSizeOverride, gridDimensionsOverride);
                
                // Check if re-initialization is needed
                if (!forceReinitialize && _currentEnvironment != null && _environmentHash == currentConfigHash)
                {
                    return @$"[Cache Hit] EQS environment has already been initialized, using cached data.
# Environment Information:
```json
{{
  ""sceneIdentifier"": ""{targetScene.name}"",
  ""environmentHash"": ""{_environmentHash}"",
  ""gridInfo"": {{
    ""cellSize"": {_currentEnvironment.Grid.CellSize},
    ""dimensions"": [{_currentEnvironment.Grid.Dimensions.x}, {_currentEnvironment.Grid.Dimensions.y}, {_currentEnvironment.Grid.Dimensions.z}],
    ""origin"": [{_currentEnvironment.Grid.Origin.x}, {_currentEnvironment.Grid.Origin.y}, {_currentEnvironment.Grid.Origin.z}],
    ""totalCells"": {_currentEnvironment.Grid.Cells.Length}
  }},
  ""staticGeometryCount"": {_currentEnvironment.StaticGeometry.Count},
  ""dynamicObjectsCount"": {_currentEnvironment.DynamicObjects.Count},
  ""lastUpdated"": ""{_currentEnvironment.LastUpdated:yyyy-MM-dd HH:mm:ss}"",
  ""fromCache"": true
}}
```

Note: If you need to force reinitialize, set forceReinitialize = true
";
                }

                // Clean up previous environment state
                CleanupPreviousEnvironment();

                // Calculate scene bounds
                var sceneBounds = CalculateSceneBounds(targetScene);
                
                // Create grid
                var grid = CreateGrid(sceneBounds, gridCellSizeOverride, gridDimensionsOverride);
                
                // Collect static geometry
                var staticGeometry = new List<EQSStaticGeometry>();
                if (includeStaticGeometry)
                {
                    staticGeometry = CollectStaticGeometry(targetScene);
                }
                
                // Collect dynamic objects
                var dynamicObjects = new List<EQSDynamicObject>();
                if (includeDynamicObjects)
                {
                    dynamicObjects = CollectDynamicObjects(targetScene, dynamicObjectTagsFilter);
                }
                
                // Initialize grid cells
                InitializeGridCells(grid, staticGeometry, dynamicObjects);
                
                // Create environment data
                var environmentData = new EQSEnvironmentData
                {
                    Grid = grid,
                    StaticGeometry = staticGeometry,
                    DynamicObjects = dynamicObjects,
                    Hash = GenerateEnvironmentHash(targetScene, staticGeometry, dynamicObjects),
                    LastUpdated = DateTime.Now
                };
                
                // Update global state
                _currentEnvironment = environmentData;
                _environmentHash = currentConfigHash;
                
                // Create visualization for all probes
                CreateProbeVisualization(grid);
                
                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                
                var statusMessage = forceReinitialize ? "[Force Reinitialize]" : "[Success]";
                return @$"{statusMessage} EQS environment initialization succeeded.
# Environment Information:
```json
{{
  ""sceneIdentifier"": ""{targetScene.name}"",
  ""environmentHash"": ""{currentConfigHash}"",
  ""gridInfo"": {{
    ""cellSize"": {grid.CellSize},
    ""dimensions"": [{grid.Dimensions.x}, {grid.Dimensions.y}, {grid.Dimensions.z}],
    ""origin"": [{grid.Origin.x}, {grid.Origin.y}, {grid.Origin.z}],
    ""totalCells"": {grid.Cells.Length}
  }},
  ""staticGeometryCount"": {staticGeometry.Count},
  ""dynamicObjectsCount"": {dynamicObjects.Count},
  ""executionTimeMs"": {executionTime:F2},
  ""forceReinitialize"": {forceReinitialize.ToString().ToLower()},
  ""probeVisualizationCreated"": true
}}
```

# Grid Statistics:
- Total Cells: {grid.Cells.Length}
- Occupied Cells: {grid.Cells.Count(c => c.StaticOccupancy || c.DynamicOccupants.Count > 0)}
- Walkable Cells: {grid.Cells.Count(c => !c.StaticOccupancy)}

# Object Statistics:
- Static Geometry: {staticGeometry.Count}
- Dynamic Objects: {dynamicObjects.Count}

# Visualization Information:
- All probe visualizations created
- Probe Material: Gray Standard Material
- All probes can be viewed in the Scene view
";
            }
            catch (Exception ex)
            {
                // Also clean up state when exceptions occur to avoid leaving incomplete data
                try
                {
                    CleanupPreviousEnvironment();
                    _currentEnvironment = null;
                    _environmentHash = null;
                }
                catch (Exception cleanupEx)
                {
                    Debug.LogError($"[EQS] Error cleaning up state: {cleanupEx.Message}");
                }
                
                return $"[Error] EQS environment initialization failed: {ex.Message}\nState automatically cleaned up, can try initializing again.";
            }
        });

        private static Bounds CalculateSceneBounds(Scene scene)
        {
            var bounds = new Bounds();
            var hasValidBounds = false;
            
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                var renderers = rootGO.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (!hasValidBounds)
                    {
                        bounds = renderer.bounds;
                        hasValidBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            
            // If no renderer found, use default bounds
            if (!hasValidBounds)
            {
                bounds = new Bounds(Vector3.zero, new Vector3(100, 10, 100));
            }
            
            // Expand bounds to ensure sufficient space
            bounds.Expand(Constants.DefaultBoundsExpansion);
            
            return bounds;
        }

        private static EQSGrid CreateGrid(Bounds sceneBounds, float? cellSizeOverride, Vector3Int? dimensionsOverride)
        {
            var cellSize = cellSizeOverride ?? Constants.DefaultCellSize;
            var origin = sceneBounds.min;
            
            Vector3Int dimensions;
            if (dimensionsOverride.HasValue)
            {
                dimensions = dimensionsOverride.Value;
            }
            else
            {
                var size = sceneBounds.size;
                dimensions = new Vector3Int(
                    Mathf.CeilToInt(size.x / cellSize),
                    Mathf.CeilToInt(size.y / cellSize),
                    Mathf.CeilToInt(size.z / cellSize)
                );
            }
            
            var totalCells = dimensions.x * dimensions.y * dimensions.z;
            var cells = new EQSCell[totalCells];
            
            return new EQSGrid
            {
                CellSize = cellSize,
                Origin = origin,
                Dimensions = dimensions,
                Cells = cells
            };
        }

        private static List<EQSStaticGeometry> CollectStaticGeometry(Scene scene)
        {
            var staticGeometry = new List<EQSStaticGeometry>();
            
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                // Collect all static objects (objects without Rigidbody)
                var staticObjects = rootGO.GetComponentsInChildren<Transform>()
                    .Where(t => t.gameObject.GetComponent<Rigidbody>() == null)
                    .Where(t => t.gameObject.GetComponent<Renderer>() != null);
                
                foreach (var staticObj in staticObjects)
                {
                    var renderer = staticObj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        staticGeometry.Add(new EQSStaticGeometry
                        {
                            Id = staticObj.gameObject.GetInstanceID().ToString(),
                            Name = staticObj.name,
                            Bounds = renderer.bounds,
                            Type = staticObj.gameObject.tag
                        });
                    }
                }
            }
            
            return staticGeometry;
        }

        private static List<EQSDynamicObject> CollectDynamicObjects(Scene scene, string[]? tagFilter)
        {
            var dynamicObjects = new List<EQSDynamicObject>();
            
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                // Collect all dynamic objects (objects with Rigidbody or specific components)
                var dynamicComps = rootGO.GetComponentsInChildren<Transform>()
                    .Where(t => t.gameObject.GetComponent<Rigidbody>() != null ||
                              t.gameObject.GetComponent<CharacterController>() != null);
                
                foreach (var dynamicComp in dynamicComps)
                {
                    var go = dynamicComp.gameObject;
                    
                    // Apply tag filtering
                    if (tagFilter != null && tagFilter.Length > 0 && !tagFilter.Contains(go.tag))
                        continue;
                    
                    var properties = new Dictionary<string, object>();
                    
                    // Add basic properties
                    if (go.GetComponent<Rigidbody>() != null)
                        properties["hasRigidbody"] = true;
                    if (go.GetComponent<CharacterController>() != null)
                        properties["hasCharacterController"] = true;
                    
                    dynamicObjects.Add(new EQSDynamicObject
                    {
                        Id = go.GetInstanceID().ToString(),
                        Name = go.name,
                        Position = go.transform.position,
                        Type = go.tag,
                        Properties = properties
                    });
                }
            }
            
            return dynamicObjects;
        }

        private static void InitializeGridCells(EQSGrid grid, List<EQSStaticGeometry> staticGeometry, List<EQSDynamicObject> dynamicObjects)
        {
            var totalCells = grid.Dimensions.x * grid.Dimensions.y * grid.Dimensions.z;
            
            for (int i = 0; i < totalCells; i++)
            {
                var indices = MathUtils.IndexToCoordinate(i, grid.Dimensions);
                var worldPos = grid.Origin + new Vector3(
                    indices.x * grid.CellSize + grid.CellSize * 0.5f,
                    indices.y * grid.CellSize + grid.CellSize * 0.5f,
                    indices.z * grid.CellSize + grid.CellSize * 0.5f
                );
                
                var cell = new EQSCell
                {
                    WorldPosition = worldPos,
                    Indices = indices,
                    StaticOccupancy = false,
                    DynamicOccupants = new List<string>(),
                    Properties = new Dictionary<string, object>()
                };
                
                // Check static geometry occupancy
                foreach (var staticGeo in staticGeometry)
                {
                    if (staticGeo.Bounds.Contains(worldPos))
                    {
                        cell.StaticOccupancy = true;
                        break;
                    }
                }
                
                // Check dynamic object occupancy
                foreach (var dynamicObj in dynamicObjects)
                {
                    if (Vector3.Distance(dynamicObj.Position, worldPos) < grid.CellSize)
                    {
                        cell.DynamicOccupants.Add(dynamicObj.Id);
                    }
                }
                
                // Set basic properties
                cell.Properties["isWalkable"] = !cell.StaticOccupancy;
                cell.Properties["hasCover"] = cell.StaticOccupancy;
                

                cell.Properties["terrainType"] = "default";
                
                grid.Cells[i] = cell;
            }
        }



        private static string GenerateEnvironmentHash(Scene scene, List<EQSStaticGeometry> staticGeometry, List<EQSDynamicObject> dynamicObjects)
        {
            var hashString = $"{scene.name}_{staticGeometry.Count}_{dynamicObjects.Count}_{DateTime.Now.Ticks}";
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashString));
                return Convert.ToBase64String(hash).Substring(0, 8);
            }
        }

        private static string GenerateConfigurationHash(Scene scene, bool includeStaticGeometry, bool includeDynamicObjects, 
            string[]? dynamicObjectTagsFilter, float? gridCellSizeOverride, Vector3Int? gridDimensionsOverride)
        {
            var configString = $"{scene.name}_{includeStaticGeometry}_{includeDynamicObjects}_" +
                               $"{string.Join(",", dynamicObjectTagsFilter ?? new string[0])}_{gridCellSizeOverride}_{gridDimensionsOverride}";
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(configString));
                return Convert.ToBase64String(hash).Substring(0, 8);
            }
        }

        private static void CleanupPreviousEnvironment()
        {
            if (_currentEnvironment != null)
            {
                // Clear grid data
                if (_currentEnvironment.Grid?.Cells != null)
                {
                    for (int i = 0; i < _currentEnvironment.Grid.Cells.Length; i++)
                    {
                        if (_currentEnvironment.Grid.Cells[i] != null)
                        {
                            _currentEnvironment.Grid.Cells[i].DynamicOccupants?.Clear();
                            _currentEnvironment.Grid.Cells[i].Properties?.Clear();
                        }
                    }
                }
                
                // Clear object collections
                _currentEnvironment.StaticGeometry?.Clear();
                _currentEnvironment.DynamicObjects?.Clear();
                
                // Clear visualization related state (if any)
                ClearAllVisualizations();
                
                Debug.Log("[EQS] Previous environment state cleaned up");
            }
        }

        private static void ClearAllVisualizations()
        {
            // Logic for clearing visualization markers can be added here
            // Clean up visualization objects created by other EQS tools here
            Debug.Log("[EQS] Clearing all visualizations");
            try
            {
                // Method 1: Use more reliable method to find GameObjects in scene
                var allGameObjects = new List<GameObject>();
                
                // First try FindObjectsOfType (including inactive objects)
                var foundObjects = GameObject.FindObjectsOfType<GameObject>(true);
                allGameObjects.AddRange(foundObjects);
                
                // Then recursively search through scene root objects (to prevent omissions)
                foreach (var rootGO in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var childObjects = rootGO.GetComponentsInChildren<Transform>(true)
                        .Select(t => t.gameObject)
                        .Where(go => !allGameObjects.Contains(go));
                    allGameObjects.AddRange(childObjects);
                }
                
                Debug.Log($"[EQS] Found {allGameObjects.Count} total GameObjects in active scene using combined method");
                
                // Filter EQS related objects
                var visualizationObjects = allGameObjects
                    .Where(go => go.name.StartsWith("EQS_Probe_") || go.name.StartsWith("EQS_QueryResult_"))
                    .ToArray();
                
                Debug.Log($"[EQS] Found {visualizationObjects.Length} EQS visualization objects to clean up");
                
                if (visualizationObjects.Length > 0)
                {
                    Debug.Log($"[EQS] First few objects: {string.Join(", ", visualizationObjects.Take(5).Select(go => go.name))}");
                }
                
                                // Method 3: Multiple cleanup strategies (ensure thorough cleanup)
                int cleanedFromSearch = 0;
                foreach (var obj in visualizationObjects)
                {
                    if (obj != null)
                    {
                        try
                        {
                            // Strategy 1: Reset all flags and destroy immediately
                            obj.hideFlags = HideFlags.None;
                            
                            // Strategy 2: Disable object first
                            obj.SetActive(false);
                            
                            // Strategy 3: Use DestroyImmediate (recommended for Editor mode)
                            #if UNITY_EDITOR
                            if (!UnityEditor.EditorApplication.isPlaying)
                            {
                                GameObject.DestroyImmediate(obj);
                            }
                            else
                            {
                                GameObject.Destroy(obj);
                            }
                            #else
                            GameObject.Destroy(obj);
                            #endif
                            
                            cleanedFromSearch++;
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[EQS] Failed to destroy object {obj.name}: {ex.Message}");
                            
                            // Fallback strategy: If destruction fails, at least mark as invisible
                            try
                            {
                                obj.SetActive(false);
                                obj.hideFlags = HideFlags.HideAndDontSave;
                                if (obj.GetComponent<Renderer>() != null)
                                    obj.GetComponent<Renderer>().enabled = false;
                            }
                            catch
                            {
                                // Ignore fallback strategy errors
                            }
                        }
                    }
                }
                
                if (cleanedFromSearch > 0)
                {
                    Debug.Log($"[EQS] Total cleaned up: {cleanedFromSearch} visualization objects");
                    
                    // Force editor and scene refresh
                    #if UNITY_EDITOR
                    // Refresh editor
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                    
                    // Force refresh scene view
                    UnityEditor.EditorApplication.RepaintHierarchyWindow();
                    
                    // Mark scene as dirty and refresh
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    
                    // Delayed verification of cleanup results
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        var remainingObjects = GameObject.FindObjectsOfType<GameObject>(true)
                            .Where(go => go.name.StartsWith("EQS_Probe_"))
                            .ToArray();
                        
                        if (remainingObjects.Length > 0)
                        {
                            Debug.LogWarning($"[EQS] Warning: {remainingObjects.Length} probe objects still remain after cleanup");
                        }
                        else
                        {
                            Debug.Log("[EQS] Cleanup verification: All probe objects successfully removed");
                        }
                    };
                    #endif
                }
                else
                {
                    Debug.LogWarning("[EQS] No EQS visualization objects found to clean up using all methods");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EQS] Exception cleaning up visualization objects: {ex.Message}");
            }
        }

        /// <summary>
        /// Create visualization markers for all probes
        /// </summary>
        private static void CreateProbeVisualization(EQSGrid grid)
        {
            try
            {
                var probeVisualization = new EQSVisualization
                {
                    QueryId = "EQS_Probes_Initial",
                    DebugObjects = new List<GameObject>()
                };

                // Create standard material (gray)
                var probeMaterial = MaterialUtils.CreateMaterial(Color.gray);

                for (int i = 0; i < grid.Cells.Length; i++)
                {
                    var cell = grid.Cells[i];
                    if (cell == null) continue;

                    // Only create probes for traversable cells (reduce visual clutter)
                    if (cell.StaticOccupancy) continue;

                    var probeObj = CreateProbeMarker(cell, probeMaterial, i);
                    probeVisualization.DebugObjects.Add(probeObj);
                }

                // Set to display permanently
                probeVisualization.ExpirationTime = DateTime.MaxValue;
                _activeVisualizations["EQS_Probes_Initial"] = probeVisualization;

                Debug.Log($"[EQS] Created {probeVisualization.DebugObjects.Count} probe visualization markers");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EQS] Failed to create probe visualization: {ex.Message}");
            }
        }



        /// <summary>
        /// Create a single probe marker
        /// </summary>
        private static GameObject CreateProbeMarker(EQSCell cell, Material material, int index)
        {
            var probeObj = new GameObject($"EQS_Probe_{index}");
            probeObj.transform.position = cell.WorldPosition;

            // Add rendering components
            var meshRenderer = probeObj.AddComponent<MeshRenderer>();
            var meshFilter = probeObj.AddComponent<MeshFilter>();
            
            // Use Unity's built-in sphere mesh
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            meshRenderer.material = material;

            // Set smaller size to avoid being too conspicuous
            probeObj.transform.localScale = Vector3.one * Constants.ProbeScale;

            // Add EQS probe marker component
            var probeComponent = probeObj.AddComponent<EQSProbeMarker>();
            probeComponent.Initialize(cell, index);

            // Mark as editor-only object
            probeObj.hideFlags = HideFlags.DontSave;

            return probeObj;
        }

        // /// <summary>
        // /// Clean up probe visualization
        // /// </summary>
        // private static void CleanupProbeVisualization()
        // {
        //     if (_activeVisualizations.ContainsKey("EQS_Probes_Initial"))
        //     {
        //         CleanupVisualization("EQS_Probes_Initial");
        //         _activeVisualizations.Remove("EQS_Probes_Initial");
        //     }
        // }
    }

    /// <summary>
    /// EQS Probe marker component
    /// Used to display basic information and properties of grid cells
    /// </summary>
    public class EQSProbeMarker : MonoBehaviour
    {
        public Tool_EQS.EQSCell Cell { get; private set; }
        public int CellIndex { get; private set; }

        public void Initialize(Tool_EQS.EQSCell cell, int index)
        {
            Cell = cell;
            CellIndex = index;
        }

        private void OnDrawGizmos()
        {
            if (Cell == null) return;

            // Basic Gizmos drawing
            Gizmos.color = Cell.StaticOccupancy ? Color.red : Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.2f);
        }

        private void OnDrawGizmosSelected()
        {
            if (Cell == null) return;

            // Show detailed information when selected
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

            #if UNITY_EDITOR
            var labelText = $"Probe #{CellIndex}\n" +
                           $"Position: {Cell.WorldPosition}\n" +
                           $"Indices: {Cell.Indices}\n" +
                           $"Static: {Cell.StaticOccupancy}\n" +
                           $"Dynamic: {Cell.DynamicOccupants.Count}";
            
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1f, labelText);
            #endif
        }
    }
} 