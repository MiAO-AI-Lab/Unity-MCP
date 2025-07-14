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

namespace com.MiAO.Unity.MCP.Essential.Tools
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
            [Description("Optional. Grid cell size in meters, affects query precision and performance, default 1.0 meters")]
            float? gridCellSizeOverride = null,
            [Description("Optional. Create an environment with a specific grid dimensions {x,y,z} and x * y * z = total cells")]
            Vector3Int? gridDimensionsOverride = null,
            [Description("Whether to force re-initialization even if environment already exists. Default true always reinitializes")]
            bool forceReinitialize = true,
            [Description("Custom region center position. Default is {\"x\":0,\"y\":0,\"z\":0}")]
            Vector3? customRegionCenter = null,
            [Description("Custom region size (width, height, depth). Default is {\"x\":10,\"y\":10,\"z\":10}")]
            Vector3? customRegionSize = null
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

                // Validate custom region parameters
                if (!customRegionCenter.HasValue)
                {
                    customRegionCenter = new Vector3(0, 0, 0);
                }
                if (!customRegionSize.HasValue)
                {
                    customRegionSize = new Vector3(10, 10, 10);
                }

                if (customRegionSize.HasValue && (customRegionSize.Value.x <= 0 || customRegionSize.Value.y <= 0 || customRegionSize.Value.z <= 0))
                {
                    return Error.InvalidCustomRegion("customRegionSize must have positive values for all dimensions");
                }

                // Generate hash value for current scene configuration for cache checking
                var currentConfigHash = GenerateConfigurationHash(targetScene, includeStaticGeometry, includeDynamicObjects, 
                    dynamicObjectTagsFilter, gridCellSizeOverride, gridDimensionsOverride, customRegionCenter, customRegionSize);
                
                // Check if re-initialization is needed
                if (!forceReinitialize && _currentEnvironment != null && _environmentHash == currentConfigHash)
                {
                    var isCustomRegion = customRegionCenter.HasValue && customRegionSize.HasValue;
                    var regionInfo = isCustomRegion 
                        ? $@"""customRegion"": {{
    ""center"": [{customRegionCenter.Value.x}, {customRegionCenter.Value.y}, {customRegionCenter.Value.z}],
    ""size"": [{customRegionSize.Value.x}, {customRegionSize.Value.y}, {customRegionSize.Value.z}]
  }},"
                        : @"""useEntireScene"": true,";

                    return @$"[Cache Hit] EQS environment has already been initialized, using cached data.
# Environment Information:
```json
{{
  ""sceneIdentifier"": ""{targetScene.name}"",
  ""environmentHash"": ""{_environmentHash}"",
  {regionInfo}
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

                // Calculate bounds
                var bounds = CalculateCustomRegionBounds(customRegionCenter.Value, customRegionSize.Value);
                
                // Create grid
                var grid = CreateGrid(bounds, gridCellSizeOverride, gridDimensionsOverride);
                
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
                
                // Create Gizmo-based visualization for all probes
                CreateGizmoProbeVisualization(grid);
                
                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                
                var statusMessage = forceReinitialize ? "[Force Reinitialize]" : "[Success]";
                var isCustomRegionFinal = customRegionCenter.HasValue && customRegionSize.HasValue;
                var regionInfoFinal = isCustomRegionFinal 
                    ? $@"""customRegion"": {{
    ""center"": [{customRegionCenter.Value.x}, {customRegionCenter.Value.y}, {customRegionCenter.Value.z}],
    ""size"": [{customRegionSize.Value.x}, {customRegionSize.Value.y}, {customRegionSize.Value.z}]
  }},"
                    : @"""useEntireScene"": true,";

                return @$"{statusMessage} EQS environment initialization succeeded.
# Environment Information:
```json
{{
  ""sceneIdentifier"": ""{targetScene.name}"",
  ""environmentHash"": ""{currentConfigHash}"",
  {regionInfoFinal}
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
  ""gizmoVisualizationCreated"": true
}}
```

# Grid Statistics:
- Total Cells: {grid.Cells.Length}
- Occupied Cells: {grid.Cells.Count(c => c.StaticOccupancy || c.DynamicOccupants.Count > 0)}
- Walkable Cells: {grid.Cells.Count(c => !c.StaticOccupancy)}

# Object Statistics:
- Static Geometry: {staticGeometry.Count}
- Dynamic Objects: {dynamicObjects.Count}

# Gizmo Visualization Information:
- Gizmo-based probe visualization created
- Uses Unity Gizmos system (no GameObjects created)
- Green spheres: Walkable cells
- Red spheres: Occupied cells  
- Yellow spheres: Cells with dynamic occupants
- Select the 'EQS Environment Visualization Group' in Hierarchy to view all probes
- Probes visible in Scene view when visualization group is selected
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
        private static Bounds CalculateCustomRegionBounds(Vector3 center, Vector3 size)
        {
            return new Bounds(center, size);
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
            string[]? dynamicObjectTagsFilter, float? gridCellSizeOverride, Vector3Int? gridDimensionsOverride,
            Vector3? customRegionCenter, Vector3? customRegionSize)
        {
            var configString = $"{scene.name}_{includeStaticGeometry}_{includeDynamicObjects}_" +
                               $"{string.Join(",", dynamicObjectTagsFilter ?? new string[0])}_{gridCellSizeOverride}_{gridDimensionsOverride}_" +
                               $"{customRegionCenter}_{customRegionSize}";
            
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
            Debug.Log("[EQS] Clearing all visualizations");
            
            int totalCleaned = 0;
            
            // Clear Gizmo-based Environment Visualization Groups
            var environmentGroups = GameObject.FindObjectsOfType<EQSEnvironmentVisualizationGroup>();
            foreach (var group in environmentGroups)
            {
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                    GameObject.DestroyImmediate(group.gameObject);
                else
                    GameObject.Destroy(group.gameObject);
                #else
                GameObject.Destroy(group.gameObject);
                #endif
                totalCleaned++;
                Debug.Log($"[EQS] Cleared EQS Environment Visualization Group: {group.name}");
            }
            
            // Clear legacy Probe environment objects (for backward compatibility)
            var probeParent = GameObject.Find("EQS_Probe_Environment");
            if (probeParent != null)
            {
                var probeCount = probeParent.transform.childCount;
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                    GameObject.DestroyImmediate(probeParent);
                else
                    GameObject.Destroy(probeParent);
                #else
                GameObject.Destroy(probeParent);
                #endif
                totalCleaned += probeCount;
                Debug.Log($"[EQS] Cleared legacy EQS_Probe_Environment with {probeCount} probe objects");
            }
            
            // Clear QueryResult aggregation objects
            var queryResultParent = GameObject.Find("EQS_QueryResult_Aggregation");
            if (queryResultParent != null)
            {
                var queryResultCount = queryResultParent.transform.childCount;
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                    GameObject.DestroyImmediate(queryResultParent);
                else
                    GameObject.Destroy(queryResultParent);
                #else
                GameObject.Destroy(queryResultParent);
                #endif
                totalCleaned += queryResultCount;
                Debug.Log($"[EQS] Cleared EQS_QueryResult_Aggregation with {queryResultCount} query result objects");
            }
            
            if (totalCleaned > 0)
            {
                Debug.Log($"[EQS] Successfully cleared {totalCleaned} total visualization objects");
                
                #if UNITY_EDITOR
                // Refresh editor
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
                #endif
            }
            else
            {
                Debug.Log("[EQS] No EQS visualization objects found to clean up");
            }
        }

        /// <summary>
        /// Create Gizmo-based visualization for all probes
        /// </summary>
        private static void CreateGizmoProbeVisualization(EQSGrid grid)
        {
            try
            {
                // Find or create EQS Environment Visualization Group
                var visualizationGroup = FindOrCreateEnvironmentVisualizationGroup();
                visualizationGroup.UpdateEnvironmentData(grid);

                Debug.Log($"[EQS] Created Gizmo-based environment visualization with {grid.Cells.Count(c => c != null && !c.StaticOccupancy)} probe markers");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EQS] Failed to create Gizmo environment visualization: {ex.Message}");
            }
        }

        /// <summary>
        /// Find or create EQS Environment Visualization Group
        /// </summary>
        private static EQSEnvironmentVisualizationGroup FindOrCreateEnvironmentVisualizationGroup()
        {
            // Try to find existing environment visualization group
            var existingGroup = GameObject.FindObjectOfType<EQSEnvironmentVisualizationGroup>();
            if (existingGroup != null)
            {
                return existingGroup;
            }

            // Create new environment visualization group
            var groupObj = new GameObject("EQS Environment Visualization Group");
            var group = groupObj.AddComponent<EQSEnvironmentVisualizationGroup>();
            
            // Mark as editor-only object
            groupObj.hideFlags = HideFlags.DontSave;
            
            return group;
        }
    }

    /// <summary>
    /// EQS Environment Visualization Group Component - Similar to Light Probe Group
    /// Uses Gizmos for visualization instead of creating GameObjects
    /// </summary>
    public class EQSEnvironmentVisualizationGroup : MonoBehaviour
    {
        [System.Serializable]
        public class ProbeData
        {
            public Vector3 position;
            public Vector3Int cellIndices;
            public bool isOccupied;
            public bool isWalkable;
            public int dynamicOccupantCount;
            public int cellIndex;
            public bool isSelected = false;

            public ProbeData(Tool_EQS.EQSCell cell, int index)
            {
                position = cell.WorldPosition;
                cellIndices = cell.Indices;
                isOccupied = cell.StaticOccupancy;
                isWalkable = !cell.StaticOccupancy;
                dynamicOccupantCount = cell.DynamicOccupants.Count;
                cellIndex = index;
            }
        }

        [Header("EQS Environment Information")]
        [HideInInspector]
        public string EnvironmentId = "EQS_Environment";
        
        [Header("Visualization Settings")]
        [Range(0.01f, 1.0f)]
        public float probeSize = 0.15f;
        [Range(0f, 1f)]
        public float selectedAlpha = 0.5f;
        [Range(0f, 1f)]
        public float unselectedAlpha = 0f;
        public bool showOnlyWalkable = true;
        
        [Header("Color Settings")]
        public Color walkableColor = Color.green;
        public Color occupiedColor = Color.red;
        public Color dynamicOccupantColor = Color.yellow;
        
        [HideInInspector]
        public List<ProbeData> probes = new List<ProbeData>();
        [HideInInspector]
        public Vector3 gridOrigin;
        [HideInInspector]
        public Vector3Int gridDimensions;
        [HideInInspector]
        public float cellSize;

        public void UpdateEnvironmentData(Tool_EQS.EQSGrid grid)
        {
            probes.Clear();
            gridOrigin = grid.Origin;
            gridDimensions = grid.Dimensions;
            cellSize = grid.CellSize;

            for (int i = 0; i < grid.Cells.Length; i++)
            {
                var cell = grid.Cells[i];
                if (cell == null) continue;

                // Only add walkable cells if showOnlyWalkable is true
                if (showOnlyWalkable && cell.StaticOccupancy) continue;

                probes.Add(new ProbeData(cell, i));
            }

            // Update object name to reflect probe count
            gameObject.name = $"EQS Environment Visualization Group ({probes.Count} probes)";
        }

        private void OnDrawGizmos()
        {
            if (probes == null || probes.Count == 0) return;

            // Only draw basic probes when this GameObject is not selected
            #if UNITY_EDITOR
            if (UnityEditor.Selection.Contains(gameObject)) return;
            #endif

            DrawBasicProbes();
        }

        private void OnDrawGizmosSelected()
        {
            if (probes == null || probes.Count == 0) return;

            // Draw all probes when group is selected
            DrawAllProbes();
        }

        private void DrawBasicProbes()
        {
            // Draw simple probes when group is not selected
            foreach (var probe in probes)
            {
                var color = GetProbeColor(probe);
                color.a = unselectedAlpha;
                Gizmos.color = color;
                Gizmos.DrawSphere(probe.position, probeSize * 0.8f); // Slightly smaller when not selected
            }
        }

        private void DrawAllProbes()
        {
            // Draw all probes with full detail when group is selected
            foreach (var probe in probes)
            {
                var color = GetProbeColor(probe);
                color.a = selectedAlpha;
                Gizmos.color = color;
                
                // Draw probe sphere
                Gizmos.DrawSphere(probe.position, probeSize);
            }
        }

        private Color GetProbeColor(ProbeData probe)
        {
            if (probe.dynamicOccupantCount > 0)
                return dynamicOccupantColor;
            else if (probe.isOccupied)
                return occupiedColor;
            else
                return walkableColor;
        }

        // Inspector display for debugging - single line summary
        public string GetSummaryText()
        {
            if (probes.Count == 0) return "No probes";
            
            var walkableCount = probes.Count(p => p.isWalkable);
            var occupiedCount = probes.Count(p => p.isOccupied);
            var dynamicCount = probes.Count(p => p.dynamicOccupantCount > 0);
            
            return $"Total: {probes.Count}, Walkable: {walkableCount}, Occupied: {occupiedCount}, Dynamic: {dynamicCount}";
        }
    }

    #if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(EQSEnvironmentVisualizationGroup))]
    public class EQSEnvironmentVisualizationGroupEditor : UnityEditor.Editor
    {
        private bool showProbeDetails = false;
        private Vector2 scrollPosition;

        public override void OnInspectorGUI()
        {
            var group = (EQSEnvironmentVisualizationGroup)target;

            // Basic information
            UnityEditor.EditorGUILayout.LabelField("Environment Information", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.LabelField($"Environment ID: {group.EnvironmentId}");
            UnityEditor.EditorGUILayout.LabelField($"Grid Origin: {group.gridOrigin}");
            UnityEditor.EditorGUILayout.LabelField($"Grid Dimensions: {group.gridDimensions}");
            UnityEditor.EditorGUILayout.LabelField($"Cell Size: {group.cellSize}");
            
            UnityEditor.EditorGUILayout.Space();

            // Probe statistics
            UnityEditor.EditorGUILayout.LabelField("Probe Statistics", UnityEditor.EditorStyles.boldLabel);
            
            // Display statistics in separate lines for better readability
            if (group.probes.Count == 0)
            {
                UnityEditor.EditorGUILayout.LabelField("No probes");
            }
            else
            {
                var walkableCount = group.probes.Count(p => p.isWalkable);
                var occupiedCount = group.probes.Count(p => p.isOccupied);
                var dynamicCount = group.probes.Count(p => p.dynamicOccupantCount > 0);
                
                UnityEditor.EditorGUILayout.LabelField($"Total: {group.probes.Count}");
                UnityEditor.EditorGUILayout.LabelField($"Walkable: {walkableCount}");
                UnityEditor.EditorGUILayout.LabelField($"Occupied: {occupiedCount}");
                UnityEditor.EditorGUILayout.LabelField($"Dynamic: {dynamicCount}");
            }
            
            UnityEditor.EditorGUILayout.Space();

            // Visualization settings
            DrawDefaultInspector();
            
            UnityEditor.EditorGUILayout.Space();

            // Probe details
            showProbeDetails = UnityEditor.EditorGUILayout.Foldout(showProbeDetails, $"Probe Details ({group.probes?.Count ?? 0})");
            if (showProbeDetails && group.probes != null && group.probes.Count > 0)
            {
                scrollPosition = UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition, UnityEngine.GUILayout.MaxHeight(200));
                
                for (int i = 0; i < Mathf.Min(group.probes.Count, 50); i++) // Limit to 50 for performance
                {
                    var probe = group.probes[i];
                    UnityEditor.EditorGUILayout.BeginVertical("box");
                    UnityEditor.EditorGUILayout.LabelField($"Probe #{probe.cellIndex}");
                    UnityEditor.EditorGUILayout.LabelField($"Position: {probe.position}");
                    UnityEditor.EditorGUILayout.LabelField($"Indices: {probe.cellIndices}");
                    UnityEditor.EditorGUILayout.LabelField($"Walkable: {probe.isWalkable}");
                    UnityEditor.EditorGUILayout.LabelField($"Dynamic Count: {probe.dynamicOccupantCount}");
                    UnityEditor.EditorGUILayout.EndVertical();
                    
                    if (i < group.probes.Count - 1)
                        UnityEditor.EditorGUILayout.Space();
                }
                
                if (group.probes.Count > 50)
                {
                    UnityEditor.EditorGUILayout.LabelField($"... and {group.probes.Count - 50} more probes");
                }
                
                UnityEditor.EditorGUILayout.EndScrollView();
            }

            UnityEditor.EditorGUILayout.Space();

            // Action buttons
            UnityEditor.EditorGUILayout.BeginHorizontal();
            if (UnityEngine.GUILayout.Button("Focus on Environment"))
            {
                FocusOnEnvironment(group);
            }
            if (UnityEngine.GUILayout.Button("Clear Visualization"))
            {
                if (UnityEditor.EditorUtility.DisplayDialog("Clear Visualization", 
                    "Are you sure you want to remove this EQS environment visualization?", "Yes", "Cancel"))
                {
                    DestroyImmediate(group.gameObject);
                }
            }
            UnityEditor.EditorGUILayout.EndHorizontal();
        }

        private void FocusOnEnvironment(EQSEnvironmentVisualizationGroup group)
        {
            if (group.probes == null || group.probes.Count == 0) return;
            
            // Calculate bounds of all probes
            var bounds = new Bounds(group.probes[0].position, Vector3.zero);
            foreach (var probe in group.probes)
            {
                bounds.Encapsulate(probe.position);
            }
            
            UnityEditor.SceneView.lastActiveSceneView.pivot = bounds.center;
            UnityEditor.SceneView.lastActiveSceneView.size = Mathf.Max(bounds.size.x, bounds.size.z) * 1.2f;
            UnityEditor.SceneView.lastActiveSceneView.Repaint();
        }
    }
    #endif
} 