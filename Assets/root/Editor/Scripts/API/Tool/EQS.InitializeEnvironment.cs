#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Unity.MCP.Editor.API
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
                // 清理网格数据
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
                
                // 清理对象集合
                _currentEnvironment.StaticGeometry?.Clear();
                _currentEnvironment.DynamicObjects?.Clear();
                
                // 清理可视化相关状态（如果有的话）
                ClearAllVisualizations();
                
                Debug.Log("[EQS] Previous environment state cleaned up");
            }
        }

        private static void ClearAllVisualizations()
        {
            // 这里可以添加清理可视化标记的逻辑
            // 如果有其他EQS工具创建的可视化对象，在此清理
            Debug.Log("[EQS] Clearing all visualizations");
            try
            {
                // 方式1: 使用更可靠的方法查找场景中的GameObject
                var allGameObjects = new List<GameObject>();
                
                // 首先尝试FindObjectsOfType（包括非活动对象）
                var foundObjects = GameObject.FindObjectsOfType<GameObject>(true);
                allGameObjects.AddRange(foundObjects);
                
                // 然后通过场景根对象递归查找（防止遗漏）
                foreach (var rootGO in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var childObjects = rootGO.GetComponentsInChildren<Transform>(true)
                        .Select(t => t.gameObject)
                        .Where(go => !allGameObjects.Contains(go));
                    allGameObjects.AddRange(childObjects);
                }
                
                Debug.Log($"[EQS] Found {allGameObjects.Count} total GameObjects in active scene using combined method");
                
                // 过滤EQS相关对象
                var visualizationObjects = allGameObjects
                    .Where(go => go.name.StartsWith("EQS_Probe_") || go.name.StartsWith("EQS_QueryResult_"))
                    .ToArray();
                
                Debug.Log($"[EQS] Found {visualizationObjects.Length} EQS visualization objects to clean up");
                
                if (visualizationObjects.Length > 0)
                {
                    Debug.Log($"[EQS] First few objects: {string.Join(", ", visualizationObjects.Take(5).Select(go => go.name))}");
                }
                
                                // 方式3: 多重清理策略（确保彻底清理）
                int cleanedFromSearch = 0;
                foreach (var obj in visualizationObjects)
                {
                    if (obj != null)
                    {
                        try
                        {
                            // 策略1: 重置所有标志并立即销毁
                            obj.hideFlags = HideFlags.None;
                            
                            // 策略2: 先禁用对象
                            obj.SetActive(false);
                            
                            // 策略3: 使用 DestroyImmediate（Editor 模式推荐）
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
                            
                            // 备用策略: 如果销毁失败，至少标记为不可见
                            try
                            {
                                obj.SetActive(false);
                                obj.hideFlags = HideFlags.HideAndDontSave;
                                if (obj.GetComponent<Renderer>() != null)
                                    obj.GetComponent<Renderer>().enabled = false;
                            }
                            catch
                            {
                                // 忽略备用策略的错误
                            }
                        }
                    }
                }
                
                if (cleanedFromSearch > 0)
                {
                    Debug.Log($"[EQS] Total cleaned up: {cleanedFromSearch} visualization objects");
                    
                    // 强制编辑器和场景刷新
                    #if UNITY_EDITOR
                    // 刷新编辑器
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                    
                    // 强制刷新场景视图
                    UnityEditor.EditorApplication.RepaintHierarchyWindow();
                    
                    // 标记场景为脏状态并刷新
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    
                    // 延迟验证清理结果
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
        /// 创建所有probe的可视化标记
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

                // 创建标准材质（灰色）
                var probeMaterial = MaterialUtils.CreateMaterial(Color.gray);

                for (int i = 0; i < grid.Cells.Length; i++)
                {
                    var cell = grid.Cells[i];
                    if (cell == null) continue;

                    // 只为可通行的单元格创建probe（减少视觉混乱）
                    if (cell.StaticOccupancy) continue;

                    var probeObj = CreateProbeMarker(cell, probeMaterial, i);
                    probeVisualization.DebugObjects.Add(probeObj);
                }

                // 设置永久显示
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
        /// 创建单个probe标记
        /// </summary>
        private static GameObject CreateProbeMarker(EQSCell cell, Material material, int index)
        {
            var probeObj = new GameObject($"EQS_Probe_{index}");
            probeObj.transform.position = cell.WorldPosition;

            // 添加渲染组件
            var meshRenderer = probeObj.AddComponent<MeshRenderer>();
            var meshFilter = probeObj.AddComponent<MeshFilter>();
            
            // 使用Unity内置的球体网格
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            meshRenderer.material = material;

            // 设置小一点的size以避免过于显眼
            probeObj.transform.localScale = Vector3.one * Constants.ProbeScale;

            // 添加EQS probe标记组件
            var probeComponent = probeObj.AddComponent<EQSProbeMarker>();
            probeComponent.Initialize(cell, index);

            // 标记为编辑器专用对象
            probeObj.hideFlags = HideFlags.DontSave;

            return probeObj;
        }

        // /// <summary>
        // /// 清理probe可视化
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
    /// EQS Probe标记组件
    /// 用于显示网格单元的基本信息和属性
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

            // 基础Gizmos绘制
            Gizmos.color = Cell.StaticOccupancy ? Color.red : Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.2f);
        }

        private void OnDrawGizmosSelected()
        {
            if (Cell == null) return;

            // 选中时显示详细信息
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