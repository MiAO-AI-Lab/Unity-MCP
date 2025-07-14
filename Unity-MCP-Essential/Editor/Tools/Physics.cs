#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.MiAO.Unity.MCP.Common;
using System.ComponentModel;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Physics
    {

        public static class Error
        {
            public static string InvalidLayerMask(int layerMask)
                => $"[Error] Invalid LayerMask '{layerMask}'. LayerMask should be a valid layer mask value.";
            
            public static string InvalidMaxDistance(float maxDistance)
                => $"[Error] Invalid max distance '{maxDistance}'. Max distance should be a positive value.";
            
            public static string InvalidDirection()
                => "[Error] Invalid direction. Direction vector cannot be zero.";
            
            public static string InvalidStartPoint()
                => "[Error] Invalid start point. Start point coordinates are not valid.";
            
            public static string InvalidEndPoint()
                => "[Error] Invalid end point. End point coordinates are not valid.";
            
            public static string StartPointEqualsEndPoint()
                => "[Error] Start point cannot be the same as end point.";

            // Ray type related errors
            public static string EmptyRayType()
                => "[Error] rayType parameter cannot be empty.";
            
            public static string InvalidRayType(string rayType)
                => $"[Error] Invalid ray type '{rayType}'. Valid types: ray, sphere, box, capsule, checksphere, lineofsight, multiray";
            
            public static string UnimplementedRayType(string rayType)
                => $"[Error] Unimplemented ray type '{rayType}'.";

            // Parameter missing errors
            public static string MissingEndPointOrDirection()
                => "[Error] Must specify either endPoint or direction parameter";
            
            public static string MissingHalfExtents()
                => "[Error] Box type must specify halfExtents parameter (box half-size).";
            
            public static string MissingDirectionForBox()
                => "[Error] Box type must specify direction parameter.";
            
            public static string MissingEndPointForCapsule()
                => "[Error] Capsule type must specify endPoint parameter as capsule endpoint.";
            
            public static string MissingDirectionForCapsule()
                => "[Error] Capsule type must specify direction parameter.";
            
            public static string MissingEndPointForLineOfSight()
                => "[Error] LineOfSight type must specify endPoint parameter as target position.";
            
            public static string MissingCenterDirection()
                => "[Error] MultiRay type must specify centerDirection parameter.";

            // Radius related errors
            public static string InvalidRadius(float radius)
                => $"[Error] Invalid radius '{radius}'. Radius must be positive.";

            // Coordinate related errors
            public static string InvalidCenterPoint()
                => "[Error] Invalid center point coordinates.";
            
            public static string InvalidCapsuleStartPoint()
                => "[Error] Invalid capsule start point coordinates.";
            
            public static string InvalidCapsuleEndPoint()
                => "[Error] Invalid capsule end point coordinates.";
            
            public static string InvalidObserverPosition()
                => "[Error] Invalid observer position coordinates.";
            
            public static string InvalidTargetPosition()
                => "[Error] Invalid target position coordinates.";
            
            public static string ObserverEqualsTarget()
                => "[Error] Observer position cannot be the same as target position.";

            // Size related errors
            public static string InvalidBoxSize(UnityEngine.Vector3 halfExtents)
                => $"[Error] Invalid box size '{halfExtents}'. All dimensions must be positive.";

            // Count related errors
            public static string InvalidRayCount(int rayCount)
                => $"[Error] Ray count must be greater than 0, current value: {rayCount}.";

            public static string EmptyOperation()
                => "[Error] operation parameter cannot be empty.";
            
            public static string InvalidOperation(string operation)
                => $"[Error] Invalid operation type '{operation}'. Valid types: listAll, calculate, decode, sceneAnalysis, presets";
            
            public static string UnimplementedOperation(string operation)
                => $"[Error] Unimplemented operation type '{operation}'.";
            
            public static string NoLayersSpecified()
                => "[Error] Must specify layerNames or layerIndices parameter to calculate LayerMask.";
            
            // LayerMask Management related errors
            public static string TagManagerAccessFailed()
                => "[Error] Failed to access TagManager layers property.";
            
            public static string LayerModificationFailed(string message)
                => $"[Error] Failed to modify Layer: {message}";
            
            public static string LayerNameRequired(string operation)
                => $"[Error] Layer name is required for {operation} operation.";
            
            public static string LayerNameTooLong()
                => "[Error] Layer name cannot exceed 32 characters.";
            
            public static string LayerNameAlreadyExists(string layerName, int index)
                => $"[Error] Layer name '{layerName}' already exists at index {index}.";
            
            public static string InvalidTargetLayerIndex()
                => "[Error] Invalid target layer index or no available slots found.";
            
            public static string CannotModifyBuiltInLayer(int index)
                => $"[Error] Cannot modify built-in layer at index {index}.";
            
            public static string LayerSlotOccupied(int index, string currentName)
                => $"[Error] Layer slot {index} is already occupied by '{currentName}'.";
            
            public static string LayerNotFound(string layerName)
                => $"[Error] Layer '{layerName}' not found.";
            
            public static string InvalidLayerIndex()
                => "[Error] Invalid target layer index specified.";
            
            public static string CannotRemoveBuiltInLayer(int index)
                => $"[Error] Cannot remove built-in layer at index {index}.";
            
            public static string LayerAlreadyEmpty(int index)
                => $"[Error] Layer at index {index} is already empty.";
            
            public static string LayerSlotEmpty(int index)
                => $"[Error] Layer at index {index} is empty.";
            
            public static string CannotRenameBuiltInLayer(string layerName, int index)
                => $"[Error] Cannot rename built-in layer '{layerName}' at index {index}.";
        }
    }
}