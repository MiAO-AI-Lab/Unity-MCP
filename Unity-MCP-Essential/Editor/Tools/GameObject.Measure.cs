 #pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_Measure",
            Title = "GameObject Measurement Tool - Distance, Bounds, and Projection Calculations"
        )]
        [Description(@"Comprehensive GameObject measurement tool for 3D space calculations including:
- pointToPoint: Calculate distance between two points in 3D space
- pointToEdge: Calculate distance from a point to closest edge of a GameObject
- colliderDistance: Calculate distance between two GameObjects' colliders
- groundProjection: Calculate object's OBB and AABB when projected to ground plane
- boundingBox: Calculate object's 3D bounding box dimensions (width, height, depth)")]
        public string Measure
        (
            [Description("Measurement type: 'pointToPoint', 'pointToEdge', 'colliderDistance', 'groundProjection', 'boundingBox'")]
            string measureType,
            [Description("Primary GameObject reference for measurement")]
            GameObjectRef? gameObjectRef = null,
            [Description("Secondary GameObject reference (for colliderDistance)")]
            GameObjectRef? targetGameObjectRef = null,
            [Description("Point coordinates [x,y,z] for point-based measurements")]
            Vector3? point = null,
            [Description("Second point coordinates [x,y,z] for pointToPoint measurement")]
            Vector3? targetPoint = null,
            [Description("Ground plane normal vector [x,y,z] (default: Vector3.up)")]
            Vector3? groundNormal = null,
            [Description("Ground plane position [x,y,z] (default: Vector3.zero)")]
            Vector3? groundPosition = null,
            [Description("Include child objects in measurement calculations")]
            bool includeChildren = true,
            [Description("Use world space coordinates (true) or local space (false)")]
            bool useWorldSpace = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(measureType))
                    return MeasureError.EmptyMeasureType();

                measureType = measureType.ToLower().Trim();
                var validTypes = new[] { "pointtopoint", "pointtoedge", "colliderdistance", "groundprojection", "boundingbox" };
                if (Array.IndexOf(validTypes, measureType) == -1)
                    return MeasureError.InvalidMeasureType(measureType);

                return measureType switch
                {
                    "pointtopoint" => MeasurePointToPoint(point, targetPoint),
                    "pointtoedge" => MeasurePointToEdge(gameObjectRef, point, includeChildren, useWorldSpace),
                    "colliderdistance" => MeasureColliderDistance(gameObjectRef, targetGameObjectRef, includeChildren),
                    "groundprojection" => MeasureGroundProjection(gameObjectRef, groundNormal, groundPosition, includeChildren, useWorldSpace),
                    "boundingbox" => MeasureBoundingBox(gameObjectRef, includeChildren, useWorldSpace),
                    _ => MeasureError.UnimplementedMeasureType(measureType)
                };
            });
        }

        private static string MeasurePointToPoint(Vector3? point1, Vector3? point2)
        {
            if (!point1.HasValue || !point2.HasValue)
                return MeasureError.MissingPointsForMeasurement();

            var distance = Vector3.Distance(point1.Value, point2.Value);
            var measurementInfo = new
            {
                measureType = "pointToPoint",
                point1 = point1.Value,
                point2 = point2.Value,
                distance = distance,
                direction = (point2.Value - point1.Value).normalized,
                measurements = new
                {
                    euclideanDistance = distance,
                    manhattanDistance = Mathf.Abs(point2.Value.x - point1.Value.x) + 
                                      Mathf.Abs(point2.Value.y - point1.Value.y) + 
                                      Mathf.Abs(point2.Value.z - point1.Value.z),
                    horizontalDistance = Vector3.Distance(new Vector3(point1.Value.x, 0, point1.Value.z), 
                                                        new Vector3(point2.Value.x, 0, point2.Value.z)),
                    verticalDistance = Mathf.Abs(point2.Value.y - point1.Value.y)
                }
            };

            var json = JsonUtils.Serialize(measurementInfo);
            return $@"[Success] Point to point distance measurement completed.
{json}";
        }

        private static string MeasurePointToEdge(GameObjectRef? gameObjectRef, Vector3? point, bool includeChildren, bool useWorldSpace)
        {
            if (!point.HasValue)
                return MeasureError.MissingPointForMeasurement();

            if (gameObjectRef == null)
                return MeasureError.GameObjectReferenceRequired();

            var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
            if (error != null)
                return error;

            var bounds = includeChildren ? go.CalculateBounds() : GetRendererBounds(go);
            if (bounds.size == Vector3.zero)
                return MeasureError.NoRenderersFound(go.name);

            var targetPoint = useWorldSpace ? point.Value : go.transform.TransformPoint(point.Value);
            var closestPoint = bounds.ClosestPoint(targetPoint);
            var distance = Vector3.Distance(targetPoint, closestPoint);

            var measurementInfo = new
            {
                measureType = "pointToEdge",
                gameObject = go.Print(includeBounds: false),
                point = point.Value,
                useWorldSpace = useWorldSpace,
                includeChildren = includeChildren,
                objectBounds = new
                {
                    center = bounds.center,
                    size = bounds.size,
                    min = bounds.min,
                    max = bounds.max
                },
                measurements = new
                {
                    distance = distance,
                    closestPoint = closestPoint,
                    isInside = bounds.Contains(targetPoint),
                    direction = (closestPoint - targetPoint).normalized
                }
            };

            var json = JsonUtils.Serialize(measurementInfo);
            return $@"[Success] Point to edge distance measurement completed.
{json}";
        }

        private static string MeasureColliderDistance(GameObjectRef? gameObjectRef1, GameObjectRef? gameObjectRef2, bool includeChildren)
        {
            if (gameObjectRef1 == null || gameObjectRef2 == null)
                return MeasureError.TwoGameObjectReferencesRequired();

            var go1 = GameObjectUtils.FindBy(gameObjectRef1, out var error1);
            if (error1 != null)
                return error1;

            var go2 = GameObjectUtils.FindBy(gameObjectRef2, out var error2);
            if (error2 != null)
                return error2;

            var colliders1 = includeChildren ? go1.GetComponentsInChildren<Collider>() : go1.GetComponents<Collider>();
            var colliders2 = includeChildren ? go2.GetComponentsInChildren<Collider>() : go2.GetComponents<Collider>();

            if (colliders1.Length == 0)
                return MeasureError.NoCollidersFound(go1.name);

            if (colliders2.Length == 0)
                return MeasureError.NoCollidersFound(go2.name);

            var minDistance = float.MaxValue;
            var closestPair = (collider1: (Collider)null, collider2: (Collider)null);
            var closestPoints = (point1: Vector3.zero, point2: Vector3.zero);

            foreach (var c1 in colliders1)
            {
                foreach (var c2 in colliders2)
                {
                    var distance = Vector3.Distance(c1.ClosestPoint(c2.bounds.center), c2.ClosestPoint(c1.bounds.center));
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPair = (c1, c2);
                        closestPoints = (c1.ClosestPoint(c2.bounds.center), c2.ClosestPoint(c1.bounds.center));
                    }
                }
            }

            var measurementInfo = new
            {
                measureType = "colliderDistance",
                gameObject1 = go1.Print(),
                gameObject2 = go2.Print(),
                includeChildren = includeChildren,
                colliders1Count = colliders1.Length,
                colliders2Count = colliders2.Length,
                measurements = new
                {
                    minDistance = minDistance,
                    closestCollider1 = closestPair.collider1?.name,
                    closestCollider2 = closestPair.collider2?.name,
                    closestPoint1 = closestPoints.point1,
                    closestPoint2 = closestPoints.point2,
                    areOverlapping = minDistance <= 0.001f
                }
            };

            var json = JsonUtils.Serialize(measurementInfo);
            return $@"[Success] Collider distance measurement completed.
{json}";
        }

        private static string MeasureGroundProjection(GameObjectRef? gameObjectRef, Vector3? groundNormal, Vector3? groundPosition, bool includeChildren, bool useWorldSpace)
        {
            if (gameObjectRef == null)
                return MeasureError.GameObjectReferenceRequired();

            var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
            if (error != null)
                return error;

            var bounds = includeChildren ? go.CalculateBounds() : GetRendererBounds(go);
            if (bounds.size == Vector3.zero)
                return MeasureError.NoRenderersFound(go.name);

            var normal = groundNormal ?? Vector3.up;
            var position = groundPosition ?? Vector3.zero;

            // Calculate AABB projection
            var aabbProjection = CalculateAABBProjection(bounds, normal, position);

            // Calculate OBB projection
            var obbProjection = CalculateOBBProjection(go, normal, position, includeChildren);

            var measurementInfo = new
            {
                measureType = "groundProjection",
                gameObject = go.Print(includeBounds: false),
                groundNormal = normal,
                groundPosition = position,
                includeChildren = includeChildren,
                useWorldSpace = useWorldSpace,
                originalBounds = new
                {
                    center = bounds.center,
                    size = bounds.size
                },
                aabbProjection = aabbProjection,
                obbProjection = obbProjection
            };

            var json = JsonUtils.Serialize(measurementInfo);
            return $@"[Success] Ground projection measurement completed.
{json}";
        }

        private static string MeasureBoundingBox(GameObjectRef? gameObjectRef, bool includeChildren, bool useWorldSpace)
        {
            if (gameObjectRef == null)
                return MeasureError.GameObjectReferenceRequired();

            var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
            if (error != null)
                return error;

            var bounds = includeChildren ? go.CalculateBounds() : GetRendererBounds(go);
            if (bounds.size == Vector3.zero)
                return MeasureError.NoRenderersFound(go.name);

            var measurementInfo = new
            {
                measureType = "boundingBox",
                gameObject = go.Print(includeBounds: false),
                includeChildren = includeChildren,
                useWorldSpace = useWorldSpace,
                bounds = new
                {
                    center = bounds.center,
                    size = bounds.size,
                    min = bounds.min,
                    max = bounds.max,
                    width = bounds.size.x,
                    height = bounds.size.y,
                    depth = bounds.size.z,
                    volume = bounds.size.x * bounds.size.y * bounds.size.z,
                    surfaceArea = 2 * (bounds.size.x * bounds.size.y + bounds.size.y * bounds.size.z + bounds.size.z * bounds.size.x)
                }
            };

            var json = JsonUtils.Serialize(measurementInfo);
            return $@"[Success] Bounding box measurement completed.
{json}";
        }

        private static Bounds GetRendererBounds(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                return renderer.bounds;

            return new Bounds(go.transform.position, Vector3.zero);
        }

        private static object CalculateAABBProjection(Bounds bounds, Vector3 normal, Vector3 position)
        {
            // Project bounds onto ground plane
            var planeRight = Vector3.Cross(normal, Vector3.up).normalized;
            if (planeRight == Vector3.zero)
                planeRight = Vector3.right;

            var planeForward = Vector3.Cross(planeRight, normal).normalized;

            var corners = new Vector3[8]
            {
                bounds.min,
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                bounds.max
            };

            var minRight = float.MaxValue;
            var maxRight = float.MinValue;
            var minForward = float.MaxValue;
            var maxForward = float.MinValue;

            foreach (var corner in corners)
            {
                var rightProjection = Vector3.Dot(corner - position, planeRight);
                var forwardProjection = Vector3.Dot(corner - position, planeForward);

                minRight = Mathf.Min(minRight, rightProjection);
                maxRight = Mathf.Max(maxRight, rightProjection);
                minForward = Mathf.Min(minForward, forwardProjection);
                maxForward = Mathf.Max(maxForward, forwardProjection);
            }

            return new
            {
                width = maxRight - minRight,
                length = maxForward - minForward,
                area = (maxRight - minRight) * (maxForward - minForward),
                center = position + planeRight * (minRight + maxRight) * 0.5f + planeForward * (minForward + maxForward) * 0.5f
            };
        }

        private static object CalculateOBBProjection(GameObject go, Vector3 normal, Vector3 position, bool includeChildren)
        {
            // For OBB, we use the object's transform orientation
            var transform = go.transform;
            var bounds = includeChildren ? go.CalculateBounds() : GetRendererBounds(go);

            // Get object's oriented axes
            var right = transform.right;
            var up = transform.up;
            var forward = transform.forward;

            // Project axes onto ground plane
            var projectedRight = Vector3.ProjectOnPlane(right, normal).normalized;
            var projectedForward = Vector3.ProjectOnPlane(forward, normal).normalized;

            if (projectedRight == Vector3.zero)
                projectedRight = Vector3.Cross(normal, Vector3.up).normalized;

            if (projectedForward == Vector3.zero)
                projectedForward = Vector3.Cross(projectedRight, normal).normalized;

            // Calculate OBB dimensions in projected space
            var size = bounds.size;
            var rightExtent = Mathf.Abs(Vector3.Dot(projectedRight, right)) * size.x * 0.5f;
            var forwardExtent = Mathf.Abs(Vector3.Dot(projectedForward, forward)) * size.z * 0.5f;

            return new
            {
                width = rightExtent * 2,
                length = forwardExtent * 2,
                area = rightExtent * forwardExtent * 4,
                center = Vector3.Project(bounds.center - position, normal) + position,
                orientation = new
                {
                    right = projectedRight,
                    forward = projectedForward
                }
            };
        }

        public static class MeasureError
        {
            public static string EmptyMeasureType()
                => "[Error] Measure type is empty. Valid types: 'pointToPoint', 'pointToEdge', 'colliderDistance', 'groundProjection', 'boundingBox'";

            public static string InvalidMeasureType(string measureType)
                => $"[Error] Invalid measure type '{measureType}'. Valid types: 'pointToPoint', 'pointToEdge', 'colliderDistance', 'groundProjection', 'boundingBox'";

            public static string UnimplementedMeasureType(string measureType)
                => $"[Error] Measure type '{measureType}' is not implemented yet.";

            public static string MissingPointsForMeasurement()
                => "[Error] Both point and targetPoint are required for pointToPoint measurement.";

            public static string MissingPointForMeasurement()
                => "[Error] Point is required for pointToEdge measurement.";

            public static string GameObjectReferenceRequired()
                => "[Error] GameObject reference is required for this measurement.";

            public static string TwoGameObjectReferencesRequired()
                => "[Error] Both gameObjectRef and targetGameObjectRef are required for colliderDistance measurement.";

            public static string NoRenderersFound(string gameObjectName)
                => $"[Error] No renderers found on GameObject '{gameObjectName}'. Cannot calculate bounds.";

            public static string NoCollidersFound(string gameObjectName)
                => $"[Error] No colliders found on GameObject '{gameObjectName}'. Cannot calculate collider distance.";
        }
    }
}