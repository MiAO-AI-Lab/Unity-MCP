#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Collections.Generic;
using UnityEngine;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Physics
    {
        [McpPluginTool("Physics_RayCast", Title = "Universal Raycast Tool")]
        [Description(@"Universal Unity Physics raycasting tool supporting multiple ray types and detection modes.

Supported ray types:
- 'ray': Standard raycast, casts an infinitely thin ray from a point in a specified direction
- 'sphere': Sphere cast, a spherical ray with radius, suitable for detecting collisions in a larger area
- 'box': Box cast, uses box shape for scanning, suitable for detecting complex shapes
- 'capsule': Capsule cast, uses capsule shape, especially suitable for character collision detection
- 'checkSphere': Sphere overlap detection, detects all colliders within a specified spherical area, not a ray
- 'lineOfSight': Line of sight detection, specifically for detecting obstacles between two points
- 'multiRay': Multi-ray fan detection, casts multiple rays simultaneously from one point in different directions

Parameter usage instructions:
1. All ray types require startPoint (starting point)
2. ray/sphere can use endPoint or direction+maxDistance to define the ray
3. box requires halfExtents (box half-size) and direction
4. capsule requires point2 (capsule endpoint), radius and direction
5. checkSphere only requires center and radius
6. lineOfSight requires startPoint and targetPosition
7. multiRay requires centerDirection, spreadAngle and rayCount

Returns detailed collision information including hit points, normals, distances, collider details, etc.")]
        public string RayCast
        (
            [Description("Ray type. Valid values: 'ray'(standard raycast), 'sphere'(sphere cast), 'box'(box cast), 'capsule'(capsule cast), 'checkSphere'(sphere overlap check), 'lineOfSight'(line of sight check), 'multiRay'(multi-ray fan detection)")]
            string rayType = "ray",
            
            [Description("For checkSphere: detection center coordinates [x,y,z]. For lineOfSight: observer position coordinates [x,y,z]. For others: ray start point coordinates [x,y,z]")]
            Vector3 startPoint = default,
            
            [Description("Ray end point coordinates [x,y,z]. Used for ray/sphere types (optional, lower priority than direction); target position for lineOfSight type; capsule end point for capsule type")]
            Vector3? endPoint = null,
            
            [Description("Ray direction vector [x,y,z]. Used for ray/sphere/box/capsule types. If direction is specified, endPoint parameter will be ignored (except for lineOfSight)")]
            Vector3? direction = null,
            
            [Description("Ray maximum distance. Used for ray/sphere/box/capsule types, defaults to infinity. Takes effect when using direction")]
            float maxDistance = Mathf.Infinity,
            
            [Description("Sphere/capsule radius. Used for sphere/capsule/checkSphere types, must be positive")]
            float radius = 1.0f,
            
            [Description("Box half-extents for each axis [x,y,z]. Used for box type, all values must be positive")]
            Vector3? halfExtents = null,
            
            [Description("Box rotation [x,y,z] Euler angles. Used for box type, optional")]
            Vector3? orientation = null,
            
            [Description("Center direction vector [x,y,z]. Used for multiRay type, defines the center direction of fan detection")]
            Vector3? centerDirection = null,
            
            [Description("Fan angle in degrees. Used for multiRay type, defines the spread angle of the ray fan")]
            float spreadAngle = 90f,
            
            [Description("Number of rays. Used for multiRay type, defines the number of rays to cast")]
            int rayCount = 5,
            
            [Description("Layer mask for collision detection. -1 means all layers, can use LayerMask.GetMask(\"LayerName\") to calculate specific layers")]
            int layerMask = -1,
            
            [Description("Whether to include Trigger colliders. true=detect triggers, false=ignore triggers")]
            bool includeTriggers = false,
            
            [Description("Whether to return multiple hit points. Only for ray type, true=return all hits, false=return first hit only")]
            bool returnAllHits = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // Basic parameter validation
                if (string.IsNullOrEmpty(rayType))
                    return Error.EmptyRayType();

                rayType = rayType.ToLower().Trim();
                var validTypes = new[] { "ray", "sphere", "box", "capsule", "checksphere", "lineofsight", "multiray" };
                if (System.Array.IndexOf(validTypes, rayType) == -1)
                    return Error.InvalidRayType(rayType);

                // Execute different logic based on ray type
                switch (rayType)
                {
                    case "ray":
                        return PerformRayCast(startPoint, endPoint, direction, maxDistance, layerMask, includeTriggers, returnAllHits);
                    
                    case "sphere":
                        return PerformSphereCast(startPoint, endPoint, direction, maxDistance, radius, layerMask, includeTriggers);
                    
                    case "box":
                        return PerformBoxCast(startPoint, halfExtents, direction, orientation, maxDistance, layerMask, includeTriggers);
                    
                    case "capsule":
                        return PerformCapsuleCast(startPoint, endPoint, radius, direction, maxDistance, layerMask, includeTriggers);
                    
                    case "checksphere":
                        return PerformCheckSphere(startPoint, radius, layerMask, includeTriggers);
                    
                    case "lineofsight":
                        return PerformLineOfSight(startPoint, endPoint, layerMask, !includeTriggers);
                    
                    case "multiray":
                        return PerformMultiRay(startPoint, centerDirection, spreadAngle, rayCount, maxDistance, layerMask, includeTriggers);
                    
                    default:
                        return Error.UnimplementedRayType(rayType);
                }
            });
        }

        private static string PerformRayCast(Vector3 startPoint, Vector3? endPoint, Vector3? direction, float maxDistance, int layerMask, bool includeTriggers, bool returnAllHits)
        {
            // Parameter validation
            if (float.IsNaN(startPoint.x) || float.IsNaN(startPoint.y) || float.IsNaN(startPoint.z))
                return Error.InvalidStartPoint();

            Vector3 rayDirection;
            float rayDistance;

            // Determine ray direction and distance
            if (direction.HasValue)
            {
                rayDirection = direction.Value.normalized;
                if (rayDirection == Vector3.zero)
                    return Error.InvalidDirection();
                
                rayDistance = maxDistance;
                if (rayDistance <= 0)
                    return Error.InvalidMaxDistance(rayDistance);
            }
            else if (endPoint.HasValue)
            {
                if (float.IsNaN(endPoint.Value.x) || float.IsNaN(endPoint.Value.y) || float.IsNaN(endPoint.Value.z))
                    return Error.InvalidEndPoint();
                
                if (startPoint == endPoint.Value)
                    return Error.StartPointEqualsEndPoint();
                
                rayDirection = (endPoint.Value - startPoint).normalized;
                rayDistance = Vector3.Distance(startPoint, endPoint.Value);
            }
            else
            {
                return Error.MissingEndPointOrDirection();
            }

            var queryTriggerInteraction = includeTriggers ? QueryTriggerInteraction.UseGlobal : QueryTriggerInteraction.Ignore;
            List<object> results = new List<object>();
            bool hasHit = false;

            if (returnAllHits)
            {
                RaycastHit[] hits = Physics.RaycastAll(startPoint, rayDirection, rayDistance, layerMask, queryTriggerInteraction);
                hasHit = hits.Length > 0;
                foreach (var hit in hits)
                {
                    results.Add(CreateHitInfo(hit));
                }
            }
            else
            {
                if (Physics.Raycast(startPoint, rayDirection, out RaycastHit hit, rayDistance, layerMask, queryTriggerInteraction))
                {
                    hasHit = true;
                    results.Add(CreateHitInfo(hit));
                }
            }

            var raycastInfo = new
            {
                rayType = "ray",
                hasHit = hasHit,
                startPoint = startPoint,
                direction = rayDirection,
                distance = rayDistance,
                layerMask = layerMask,
                includeTriggers = includeTriggers,
                returnAllHits = returnAllHits,
                hitCount = results.Count,
                hits = results
            };

            var json = JsonUtils.Serialize(raycastInfo);
            return $@"[Success] Standard raycast completed.
# Ray information:
Type: Standard ray (ray)
Start point: {startPoint}
Direction: {rayDirection}
Distance: {rayDistance}

# Collision result:
Has hit: {hasHit}
Hit count: {results.Count}

# Detailed data:
```json
{json}
```";
        }

        private static string PerformSphereCast(Vector3 startPoint, Vector3? endPoint, Vector3? direction, float maxDistance, float radius, int layerMask, bool includeTriggers)
        {
            if (float.IsNaN(startPoint.x) || float.IsNaN(startPoint.y) || float.IsNaN(startPoint.z))
                return Error.InvalidStartPoint();

            if (radius <= 0)
                return Error.InvalidRadius(radius);

            Vector3 rayDirection;
            float rayDistance;

            if (direction.HasValue)
            {
                rayDirection = direction.Value.normalized;
                if (rayDirection == Vector3.zero)
                    return Error.InvalidDirection();
                rayDistance = maxDistance;
            }
            else if (endPoint.HasValue)
            {
                if (float.IsNaN(endPoint.Value.x) || float.IsNaN(endPoint.Value.y) || float.IsNaN(endPoint.Value.z))
                    return Error.InvalidEndPoint();
                if (startPoint == endPoint.Value)
                    return Error.StartPointEqualsEndPoint();
                rayDirection = (endPoint.Value - startPoint).normalized;
                rayDistance = Vector3.Distance(startPoint, endPoint.Value);
            }
            else
            {
                return Error.MissingEndPointOrDirection();
            }

            if (rayDistance <= 0)
                return Error.InvalidMaxDistance(rayDistance);

            var queryTriggerInteraction = includeTriggers ? QueryTriggerInteraction.UseGlobal : QueryTriggerInteraction.Ignore;
            bool hasHit = Physics.SphereCast(startPoint, radius, rayDirection, out RaycastHit hit, rayDistance, layerMask, queryTriggerInteraction);

            var sphereCastInfo = new
            {
                rayType = "sphere",
                hasHit = hasHit,
                startPoint = startPoint,
                radius = radius,
                direction = rayDirection,
                distance = rayDistance,
                layerMask = layerMask,
                includeTriggers = includeTriggers,
                hit = hasHit ? CreateHitInfo(hit) : null
            };

            var json = JsonUtils.Serialize(sphereCastInfo);
            return $@"[Success] Sphere cast completed.
# Ray information:
Type: Sphere cast (sphere)
Start point: {startPoint}
Radius: {radius}
Direction: {rayDirection}
Distance: {rayDistance}

# Collision result:
Has hit: {hasHit}

# Detailed data:
```json
{json}
```";
        }

        private static string PerformBoxCast(Vector3 center, Vector3? halfExtents, Vector3? direction, Vector3? orientation, float maxDistance, int layerMask, bool includeTriggers)
        {
            if (float.IsNaN(center.x) || float.IsNaN(center.y) || float.IsNaN(center.z))
                return Error.InvalidCenterPoint();

            if (!halfExtents.HasValue)
                return Error.MissingHalfExtents();

            if (halfExtents.Value.x <= 0 || halfExtents.Value.y <= 0 || halfExtents.Value.z <= 0)
                return Error.InvalidBoxSize(halfExtents.Value);

            if (!direction.HasValue)
                return Error.MissingDirectionForBox();

            var rayDirection = direction.Value.normalized;
            if (rayDirection == Vector3.zero)
                return Error.InvalidDirection();

            if (maxDistance <= 0)
                return Error.InvalidMaxDistance(maxDistance);

            var queryTriggerInteraction = includeTriggers ? QueryTriggerInteraction.UseGlobal : QueryTriggerInteraction.Ignore;
            var boxOrientation = orientation.HasValue ? Quaternion.Euler(orientation.Value) : Quaternion.identity;

            bool hasHit = Physics.BoxCast(center, halfExtents.Value, rayDirection, out RaycastHit hit, boxOrientation, maxDistance, layerMask, queryTriggerInteraction);

            var boxCastInfo = new
            {
                rayType = "box",
                hasHit = hasHit,
                center = center,
                halfExtents = halfExtents.Value,
                direction = rayDirection,
                orientation = boxOrientation.eulerAngles,
                distance = maxDistance,
                layerMask = layerMask,
                includeTriggers = includeTriggers,
                hit = hasHit ? CreateHitInfo(hit) : null
            };

            var json = JsonUtils.Serialize(boxCastInfo);
            return $@"[Success] Box cast completed.
# Ray information:
Type: Box cast (box)
Center point: {center}
Box size: {halfExtents.Value}
Direction: {rayDirection}
Rotation: {boxOrientation.eulerAngles}
Distance: {maxDistance}

# Collision result:
Has hit: {hasHit}

# Detailed data:
```json
{json}
```";
        }

        private static string PerformCapsuleCast(Vector3 point1, Vector3? point2, float radius, Vector3? direction, float maxDistance, int layerMask, bool includeTriggers)
        {
            if (float.IsNaN(point1.x) || float.IsNaN(point1.y) || float.IsNaN(point1.z))
                return Error.InvalidCapsuleStartPoint();

            if (!point2.HasValue)
                return Error.MissingEndPointForCapsule();

            if (float.IsNaN(point2.Value.x) || float.IsNaN(point2.Value.y) || float.IsNaN(point2.Value.z))
                return Error.InvalidCapsuleEndPoint();

            if (radius <= 0)
                return Error.InvalidRadius(radius);

            if (!direction.HasValue)
                return Error.MissingDirectionForCapsule();

            var rayDirection = direction.Value.normalized;
            if (rayDirection == Vector3.zero)
                return Error.InvalidDirection();

            if (maxDistance <= 0)
                return Error.InvalidMaxDistance(maxDistance);

            var queryTriggerInteraction = includeTriggers ? QueryTriggerInteraction.UseGlobal : QueryTriggerInteraction.Ignore;

            bool hasHit = Physics.CapsuleCast(point1, point2.Value, radius, rayDirection, out RaycastHit hit, maxDistance, layerMask, queryTriggerInteraction);

            var capsuleCastInfo = new
            {
                rayType = "capsule",
                hasHit = hasHit,
                point1 = point1,
                point2 = point2.Value,
                radius = radius,
                direction = rayDirection,
                distance = maxDistance,
                layerMask = layerMask,
                includeTriggers = includeTriggers,
                hit = hasHit ? CreateHitInfo(hit) : null
            };

            var json = JsonUtils.Serialize(capsuleCastInfo);
            return $@"[Success] Capsule cast completed.
# Ray information:
Type: Capsule cast (capsule)
Start point: {point1}
End point: {point2.Value}
Radius: {radius}
Direction: {rayDirection}
Distance: {maxDistance}

# Collision result:
Has hit: {hasHit}

# Detailed data:
```json
{json}
```";
        }

        private static string PerformCheckSphere(Vector3 center, float radius, int layerMask, bool includeTriggers)
        {
            if (float.IsNaN(center.x) || float.IsNaN(center.y) || float.IsNaN(center.z))
                return Error.InvalidCenterPoint();

            if (radius <= 0)
                return Error.InvalidRadius(radius);

            var queryTriggerInteraction = includeTriggers ? QueryTriggerInteraction.UseGlobal : QueryTriggerInteraction.Ignore;
            bool hasCollision = Physics.CheckSphere(center, radius, layerMask, queryTriggerInteraction);
            Collider[] overlapping = Physics.OverlapSphere(center, radius, layerMask, queryTriggerInteraction);

            var checkSphereInfo = new
            {
                rayType = "checkSphere",
                hasCollision = hasCollision,
                center = center,
                radius = radius,
                layerMask = layerMask,
                includeTriggers = includeTriggers,
                colliderCount = overlapping.Length,
                colliders = overlapping != null ? System.Array.ConvertAll(overlapping, collider => new
                {
                    name = collider.name,
                    gameObjectName = collider.gameObject.name,
                    tag = collider.gameObject.tag,
                    layer = collider.gameObject.layer,
                    instanceID = collider.GetInstanceID(),
                    isTrigger = collider.isTrigger,
                    bounds = collider.bounds,
                    position = collider.transform.position
                }) : new object[0]
            };

            var json = JsonUtils.Serialize(checkSphereInfo);
            return $@"[Success] Sphere overlap check completed.
# Detection information:
Type: Sphere overlap check (checkSphere)
Center point: {center}
Radius: {radius}

# Detection result:
Has collision: {hasCollision}
Collider count: {overlapping.Length}

# Detailed data:
```json
{json}
```";
        }

        private static string PerformLineOfSight(Vector3 observerPosition, Vector3? targetPosition, int layerMask, bool ignoreTriggers)
        {
            if (float.IsNaN(observerPosition.x) || float.IsNaN(observerPosition.y) || float.IsNaN(observerPosition.z))
                return Error.InvalidObserverPosition();

            if (!targetPosition.HasValue)
                return Error.MissingEndPointForLineOfSight();

            if (float.IsNaN(targetPosition.Value.x) || float.IsNaN(targetPosition.Value.y) || float.IsNaN(targetPosition.Value.z))
                return Error.InvalidTargetPosition();

            if (observerPosition == targetPosition.Value)
                return Error.ObserverEqualsTarget();

            var direction = (targetPosition.Value - observerPosition).normalized;
            var distance = Vector3.Distance(observerPosition, targetPosition.Value);
            var queryTriggerInteraction = ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.UseGlobal;

            bool hasObstacle = Physics.Raycast(observerPosition, direction, out RaycastHit hit, distance, layerMask, queryTriggerInteraction);
            bool clearLineOfSight = !hasObstacle;

            var lineOfSightInfo = new
            {
                rayType = "lineOfSight",
                clearLineOfSight = clearLineOfSight,
                hasObstacle = hasObstacle,
                observerPosition = observerPosition,
                targetPosition = targetPosition.Value,
                direction = direction,
                distance = distance,
                layerMask = layerMask,
                ignoreTriggers = ignoreTriggers,
                obstacle = hasObstacle ? CreateHitInfo(hit) : null
            };

            var json = JsonUtils.Serialize(lineOfSightInfo);
            return $@"[Success] Line of sight detection completed.
# Detection information:
Type: Line of sight detection (lineOfSight)
Observer position: {observerPosition}
Target position: {targetPosition.Value}
Detection distance: {distance:F2}

# Detection result:
Clear line of sight: {clearLineOfSight}
Has obstacle: {hasObstacle}

# Detailed data:
```json
{json}
```";
        }

        private static string PerformMultiRay(Vector3 startPoint, Vector3? centerDirection, float spreadAngle, int rayCount, float maxDistance, int layerMask, bool includeTriggers)
        {
            if (float.IsNaN(startPoint.x) || float.IsNaN(startPoint.y) || float.IsNaN(startPoint.z))
                return Error.InvalidStartPoint();

            if (!centerDirection.HasValue)
                return Error.MissingCenterDirection();

            var centerDir = centerDirection.Value.normalized;
            if (centerDir == Vector3.zero)
                return Error.InvalidDirection();

            if (rayCount <= 0)
                return Error.InvalidRayCount(rayCount);

            if (maxDistance <= 0)
                return Error.InvalidMaxDistance(maxDistance);

            var queryTriggerInteraction = includeTriggers ? QueryTriggerInteraction.UseGlobal : QueryTriggerInteraction.Ignore;
            List<object> rayResults = new List<object>();
            int hitCount = 0;

            for (int i = 0; i < rayCount; i++)
            {
                float angle = 0f;
                
                if (rayCount > 1)
                {
                    angle = Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, (float)i / (rayCount - 1));
                }

                Vector3 rayDirection;
                if (Mathf.Abs(Vector3.Dot(centerDir, Vector3.up)) < 0.9f)
                {
                    rayDirection = Quaternion.AngleAxis(angle, Vector3.up) * centerDir;
                }
                else
                {
                    rayDirection = Quaternion.AngleAxis(angle, Vector3.forward) * centerDir;
                }

                bool hasHit = Physics.Raycast(startPoint, rayDirection, out RaycastHit hit, maxDistance, layerMask, queryTriggerInteraction);
                
                if (hasHit)
                    hitCount++;

                rayResults.Add(new
                {
                    rayIndex = i,
                    direction = rayDirection,
                    angle = angle,
                    hasHit = hasHit,
                    hit = hasHit ? CreateHitInfo(hit) : null
                });
            }

            var multiRayInfo = new
            {
                rayType = "multiRay",
                startPoint = startPoint,
                centerDirection = centerDir,
                spreadAngle = spreadAngle,
                rayCount = rayCount,
                maxDistance = maxDistance,
                layerMask = layerMask,
                includeTriggers = includeTriggers,
                totalHits = hitCount,
                hitRate = rayCount > 0 ? (float)hitCount / rayCount : 0f,
                rays = rayResults
            };

            var json = JsonUtils.Serialize(multiRayInfo);
            return $@"[Success] Multi-ray cast completed.
# Ray information:
Type: Multi-ray fan detection (multiRay)
Start point: {startPoint}
Center direction: {centerDir}
Fan angle: {spreadAngle}°
Ray count: {rayCount}

# Detection result:
Hit count: {hitCount}/{rayCount}
Hit rate: {(rayCount > 0 ? (float)hitCount / rayCount * 100f : 0f):F1}%

# Detailed data:
```json
{json}
```";
        }

        private static object CreateHitInfo(RaycastHit hit)
        {
            return new
            {
                point = hit.point,
                normal = hit.normal,
                distance = hit.distance,
                collider = new
                {
                    name = hit.collider.name,
                    gameObjectName = hit.collider.gameObject.name,
                    tag = hit.collider.gameObject.tag,
                    layer = hit.collider.gameObject.layer,
                    instanceID = hit.collider.GetInstanceID(),
                    isTrigger = hit.collider.isTrigger,
                    bounds = hit.collider.bounds
                },
                transform = new
                {
                    position = hit.transform.position,
                    rotation = hit.transform.rotation,
                    scale = hit.transform.localScale
                },
                rigidbody = hit.rigidbody != null ? new
                {
                    name = hit.rigidbody.name,
                    mass = hit.rigidbody.mass,
                    velocity = hit.rigidbody.velocity,
                    isKinematic = hit.rigidbody.isKinematic
                } : null,
                textureCoord = hit.textureCoord,
                textureCoord2 = hit.textureCoord2,
                triangleIndex = hit.triangleIndex,
                barycentricCoordinate = hit.barycentricCoordinate
            };
        }
    }
}