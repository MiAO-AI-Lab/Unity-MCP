#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_EQS
    {
        // Internal state management
        private static EQSEnvironmentData? _currentEnvironment;
        private static Dictionary<string, EQSQueryResult> _queryCache = new();
        private static Dictionary<string, EQSVisualization> _activeVisualizations = new();
        private static string _environmentHash = "";

        public static class Error
        {
            public static string EnvironmentNotInitialized()
                => "[Error] EQS environment not initialized. Please call initialize_eqs_environment first to initialize the environment.";

            public static string InvalidSceneIdentifier(string sceneIdentifier)
                => $"[Error] Scene identifier '{sceneIdentifier}' is invalid. Currently loaded scenes:\n{string.Join("\n", GetLoadedSceneNames())}";

            public static string InvalidAreaOfInterest()
                => "[Error] Invalid area of interest definition. Please provide a valid bounding box or sphere definition.";

            public static string QueryExecutionFailed(string queryId, string reason)
                => $"[Error] Query '{queryId}' execution failed: {reason}";

            public static string ObjectNotFound(string objectId)
                => $"[Error] Object '{objectId}' not found.";

            public static string PrefabNotFound(string prefabName)
                => $"[Error] Prefab '{prefabName}' not found. Please check if the prefab name is correct.";

            public static string InvalidPosition(Vector3 position)
                => $"[Error] Invalid position coordinates: {position}. Please provide valid world coordinates.";

            public static string NoQueryResults(string queryId)
                => $"[Error] Query '{queryId}' returned no results.";

            public static string InvalidCustomRegion(string reason)
                => $"[Error] Invalid custom region configuration: {reason}";

            private static string[] GetLoadedSceneNames()
            {
                var sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
                var sceneNames = new string[sceneCount];
                for (int i = 0; i < sceneCount; i++)
                {
                    sceneNames[i] = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name;
                }
                return sceneNames;
            }
        }

        // EQS data structure definition
        public class EQSEnvironmentData
        {
            public EQSGrid Grid { get; set; } = new EQSGrid();
            public List<EQSStaticGeometry> StaticGeometry { get; set; } = new();
            public List<EQSDynamicObject> DynamicObjects { get; set; } = new();
            public string Hash { get; set; } = "";
            public DateTime LastUpdated { get; set; } = DateTime.Now;
        }

        public class EQSGrid
        {
            public float CellSize { get; set; } = 1.0f;
            public Vector3 Origin { get; set; } = Vector3.zero;
            public Vector3Int Dimensions { get; set; } = new Vector3Int(100, 10, 100);
            public EQSCell[] Cells { get; set; } = Array.Empty<EQSCell>();
        }

        public class EQSCell
        {
            public Vector3 WorldPosition { get; set; }
            public Vector3Int Indices { get; set; }
            public bool StaticOccupancy { get; set; }
            public List<string> DynamicOccupants { get; set; } = new();
            public Dictionary<string, object> Properties { get; set; } = new();
        }

        public class EQSStaticGeometry
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public Bounds Bounds { get; set; }
            public string Type { get; set; } = "";
        }

        public class EQSDynamicObject
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public Vector3 Position { get; set; }
            public string Type { get; set; } = "";
            public Dictionary<string, object> Properties { get; set; } = new();
        }

        public class EQSVisualization
        {
            public string QueryId { get; set; } = "";
            public List<GameObject> DebugObjects { get; set; } = new();
            public DateTime ExpirationTime { get; set; }
        }

        // Query-related data structures
        public class EQSQuery
        {
            public string QueryID { get; set; } = "";
            public string? TargetObjectType { get; set; }
            public EQSQueryContext QueryContext { get; set; } = new();
            public List<EQSCondition> Conditions { get; set; } = new();
            public List<EQSScoringCriterion> ScoringCriteria { get; set; } = new();
            public int DesiredResultCount { get; set; } = 10;
        }

        public class EQSQueryContext
        {
            public List<EQSReferencePoint> ReferencePoints { get; set; } = new();
            public EQSAreaOfInterest? AreaOfInterest { get; set; }
        }

        public class EQSReferencePoint
        {
            public string Name { get; set; } = "";
            public Vector3 Position { get; set; }
        }

        public class EQSAreaOfInterest
        {
            public string Type { get; set; } = ""; // "sphere", "box", "area"
            public Vector3 Center { get; set; }
            public float Radius { get; set; } // for sphere
            public Vector3 Size { get; set; } // for box
            public string AreaName { get; set; } = ""; // for named area
        }

        public class EQSCondition
        {
            public string ConditionType { get; set; } = ""; // DistanceTo, VisibilityOf, Clearance, etc.
            public Dictionary<string, object> Parameters { get; set; } = new();
            public float Weight { get; set; } = 1.0f;
            public bool Invert { get; set; } = false;
        }

        public class EQSScoringCriterion
        {
            public string CriterionType { get; set; } = ""; // ProximityTo, FarthestFrom, AngleTo, etc.
            public Dictionary<string, object> Parameters { get; set; } = new();
            public float Weight { get; set; } = 1.0f;
            public string NormalizationMethod { get; set; } = "linear";
        }

        public class EQSQueryResult
        {
            public string QueryID { get; set; } = "";
            public string Status { get; set; } = ""; // Success, PartialSuccess, Failure, Processing
            public string ErrorMessage { get; set; } = "";
            public List<EQSLocationCandidate> Results { get; set; } = new();
            public float ExecutionTimeMs { get; set; }
        }

        public class EQSLocationCandidate
        {
            public Vector3 WorldPosition { get; set; }
            public Quaternion? Rotation { get; set; }
            public float Score { get; set; }
            public Dictionary<string, float> BreakdownScores { get; set; } = new();
            public Vector3Int? CellIndices { get; set; }
            public List<string> AssociatedObjectIDs { get; set; } = new();
        }

        // Enum definitions
        public enum EQSConditionType
        {
            DistanceTo,
            VisibilityOf,
            Clearance,
            PathExists,
            CustomProperty,
            LineOfSight
        }

        public enum EQSScoringType
        {
            ProximityTo,
            FarthestFrom,
            AngleTo,
            DensityOfObjects,
            CustomScore,
            AlignmentWith
        }

        public enum EQSQueryStatus
        {
            Success,
            PartialSuccess,
            Failure,
            Processing
        }

        // ===== Utility classes and constants unified management =====

        /// <summary>
        /// EQS system constants configuration
        /// </summary>
        public static class Constants
        {
            public const float DefaultCellSize = 1.0f;
            public const float DefaultBoundsExpansion = 10.0f;
            public const float ProbeScale = 0.15f;
            public const float QueryResultScale = 0.2f;
            public const float DefaultEyeHeight = 1.7f;
            public const float DefaultClearanceHeight = 2f;
            public const float DefaultClearanceRadius = 0.5f;
            public const int DefaultResultCount = 10;
            public const float DefaultMaxDistance = 100f;
            public const float EmissionIntensity = 0.3f;
            public const float DefaultSmoothness = 0.3f;
            public static readonly int DefaultLayerMask = LayerMask.GetMask("Default");
        }

        /// <summary>
        /// Parameter parsing utilities
        /// </summary>
        public static class ParseUtils
        {
            public static float ParseFloat(object value, float defaultValue = 0f)
            {
                if (value == null) return defaultValue;
                try
                {
                    if (value is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.Number)
                        return (float)element.GetDouble();
                    if (value is float f) return f;
                    if (value is double d) return (float)d;
                    if (value is int i) return (float)i;
                    if (value is string s && float.TryParse(s, out float result))
                        return result;
                    return Convert.ToSingle(value);
                }
                catch { return defaultValue; }
            }

            public static bool ParseBool(object value, bool defaultValue = false)
            {
                if (value == null) return defaultValue;
                try
                {
                    if (value is System.Text.Json.JsonElement element)
                    {
                        if (element.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                        if (element.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                    }
                    if (value is bool b) return b;
                    if (value is string s && bool.TryParse(s, out bool result))
                        return result;
                    return Convert.ToBoolean(value);
                }
                catch { return defaultValue; }
            }

            public static int ParseInt(object value, int defaultValue = 0)
            {
                if (value == null) return defaultValue;
                try
                {
                    if (value is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.Number)
                        return element.GetInt32();
                    if (value is int i) return i;
                    if (value is float f) return (int)f;
                    if (value is double d) return (int)d;
                    if (value is string s && int.TryParse(s, out int result))
                        return result;
                    return Convert.ToInt32(value);
                }
                catch { return defaultValue; }
            }

            public static float[] ParseFloatArray(object arrayObj)
            {
                if (arrayObj == null) return new float[0];
                try
                {
                    if (arrayObj is string jsonStr)
                        return JsonUtils.Deserialize<float[]>(jsonStr);

                    if (arrayObj is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var result = new List<float>();
                        foreach (var item in element.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.Number)
                                result.Add((float)item.GetDouble());
                        }
                        return result.ToArray();
                    }
                    return JsonUtils.Deserialize<float[]>(arrayObj.ToString());
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Cannot parse as float array: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Object finding utilities
        /// </summary>
        public static class ObjectUtils
        {
            public static GameObject FindGameObject(string objectIdOrName)
            {
                if (string.IsNullOrEmpty(objectIdOrName))
                    return null;

                // Method 1: Find by InstanceID
                if (int.TryParse(objectIdOrName, out var instanceId))
                {
                    var objectFromId = UnityEditor.EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                    if (objectFromId != null)
                        return objectFromId;
                }

                // Method 2: Find by name in all scenes
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (!scene.IsValid()) continue;

                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var rootObj in rootObjects)
                    {
                        if (rootObj.name == objectIdOrName)
                            return rootObj;

                        var found = rootObj.GetComponentsInChildren<Transform>(true)
                            .FirstOrDefault(t => t.name == objectIdOrName);
                        if (found != null)
                            return found.gameObject;
                    }
                }

                // Method 3: Global search
                return GameObject.Find(objectIdOrName);
            }
        }

        /// <summary>
        /// Mathematical calculation utilities
        /// </summary>
        public static class MathUtils
        {
            public static float CalculateDistance(Vector3 point1, Vector3 point2, string distanceMode)
            {
                switch (distanceMode.ToLower())
                {
                    case "euclidean":
                        return Vector3.Distance(point1, point2);
                    case "manhattan":
                        var diff = point1 - point2;
                        return Mathf.Abs(diff.x) + Mathf.Abs(diff.y) + Mathf.Abs(diff.z);
                    case "chebyshev":
                        var delta = point1 - point2;
                        return Mathf.Max(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)), Mathf.Abs(delta.z));
                    case "horizontal":
                        var horizontalDiff = new Vector3(point1.x - point2.x, 0, point1.z - point2.z);
                        return horizontalDiff.magnitude;
                    case "vertical":
                        return Mathf.Abs(point1.y - point2.y);
                    case "squared":
                        return (point1 - point2).sqrMagnitude;
                    default:
                        return Vector3.Distance(point1, point2);
                }
            }

            public static Vector3Int IndexToCoordinate(int index, Vector3Int dimensions)
            {
                var z = index / (dimensions.x * dimensions.y);
                var remainder = index % (dimensions.x * dimensions.y);
                var y = remainder / dimensions.x;
                var x = remainder % dimensions.x;
                return new Vector3Int(x, y, z);
            }

            public static int CoordinateToIndex(Vector3Int coordinate, Vector3Int dimensions)
            {
                return coordinate.x + coordinate.y * dimensions.x + coordinate.z * dimensions.x * dimensions.y;
            }
        }
    }
} 