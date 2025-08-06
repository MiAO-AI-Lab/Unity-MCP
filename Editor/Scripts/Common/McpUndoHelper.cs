using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.MCP;

namespace com.MiAO.Unity.MCP.Common
{
    /// <summary>
    /// MCP Undo Helper - Provides unified undo operations for MCP tools
    /// </summary>
    public static class McpUndoHelper
    {
        private const string MCP_PREFIX = "[MCP]";
        
        // Track consecutive operations on the same object
        private static int lastOperationInstanceID = 0;
        private static DateTime lastOperationTime = DateTime.MinValue;
        private static string lastOperationGuid = "";
        private const double OPERATION_GROUP_TIMEOUT_SECONDS = 5.0; // Operations within 5 seconds can be grouped

        /// <summary>
        /// Generate MCP group name with embedded GUID for duplicate detection
        /// </summary>
        private static string GenerateMcpGroupName(string groupName)
        {
            var operationGuid = System.Guid.NewGuid().ToString("N")[..8]; // Use first 8 characters for brevity
            return $"{MCP_PREFIX} [GUID:{operationGuid}] {groupName}";
        }

        /// <summary>
        /// Get GameObject instanceID from Unity Object
        /// </summary>
        private static int GetInstanceID(UnityEngine.Object obj)
        {
            if (obj == null) return 0;
            
            // For GameObject, return its instanceID directly
            if (obj is GameObject gameObject)
            {
                return gameObject.GetInstanceID();
            }
            
            // For Component, return the GameObject's instanceID
            if (obj is Component component && component.gameObject != null)
            {
                return component.gameObject.GetInstanceID();
            }
            
            // For other objects, return their own instanceID
            return obj.GetInstanceID();
        }

        /// <summary>
        /// Check if current operation should be grouped with the previous one
        /// </summary>
        private static bool ShouldGroupWithPreviousOperation(int currentInstanceID)
        {
            if (currentInstanceID == 0 || lastOperationInstanceID == 0)
                return false;
                
            // Same object and within time window
            bool sameObject = currentInstanceID == lastOperationInstanceID;
            bool withinTimeWindow = (DateTime.Now - lastOperationTime).TotalSeconds <= OPERATION_GROUP_TIMEOUT_SECONDS;
            
            return sameObject && withinTimeWindow;
        }

        /// <summary>
        /// Update operation tracking information
        /// </summary>
        private static void UpdateOperationTracking(int instanceID, string operationGuid)
        {
            lastOperationInstanceID = instanceID;
            lastOperationTime = DateTime.Now;
            lastOperationGuid = operationGuid;
        }

        /// <summary>
        /// Reset operation grouping state. Call this to force the next operation to start a new group.
        /// </summary>
        public static void ResetOperationGrouping()
        {
            lastOperationInstanceID = 0;
            lastOperationTime = DateTime.MinValue;
            lastOperationGuid = "";
        }

        /// <summary>
        /// Check if the current operation would be grouped with the previous one
        /// </summary>
        /// <param name="targetObject">The target object for the operation</param>
        /// <returns>True if the operation would be grouped</returns>
        public static bool WouldGroupWithPrevious(UnityEngine.Object targetObject)
        {
            if (targetObject == null) return false;
            var instanceID = GetInstanceID(targetObject);
            return ShouldGroupWithPreviousOperation(instanceID);
        }

        #region Creation Operations

        /// <summary>
        /// Register single object creation with undo
        /// </summary>
        /// <param name="createdObject">The created Unity object</param>
        /// <param name="operationName">Operation description (e.g., "Create GameObject", "Add Component")</param>
        /// <param name="targetName">Target object name for better description</param>
        public static void RegisterCreatedObject(UnityEngine.Object createdObject, string operationName, string targetName = null)
        {
            if (createdObject == null) return;

            // Creation operations typically start a new group
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(createdObject, $"{operationName}: {createdObject.name}");
            
            var groupName = string.IsNullOrEmpty(targetName) 
                ? $"{operationName}: {createdObject.name}"
                : $"{operationName} {createdObject.name} to {targetName}";
            
            var operationGuid = System.Guid.NewGuid().ToString("N")[..8];
            var finalGroupName = $"{MCP_PREFIX} [GUID:{operationGuid}] {groupName}";
            Undo.SetCurrentGroupName(finalGroupName);
            
            // Update tracking so subsequent operations on this object can be grouped
            var instanceID = GetInstanceID(createdObject);
            UpdateOperationTracking(instanceID, operationGuid);
        }

        /// <summary>
        /// Register multiple objects creation with undo (batch operation)
        /// </summary>
        /// <param name="createdObjects">List of created Unity objects</param>
        /// <param name="operationName">Operation description (e.g., "Create GameObject", "Add Component")</param>
        /// <param name="targetName">Target object name for better description</param>
        public static void RegisterCreatedObjects<T>(IList<T> createdObjects, string operationName, string targetName = null) where T : UnityEngine.Object
        {
            if (createdObjects == null || createdObjects.Count == 0) return;

            Undo.IncrementCurrentGroup();

            foreach (var obj in createdObjects)
            {
                if (obj != null)
                {
                    Undo.RegisterCreatedObjectUndo(obj, $"{operationName}: {obj.name}");
                }
            }

            string groupName;
            if (createdObjects.Count == 1)
            {
                var obj = createdObjects[0];
                groupName = string.IsNullOrEmpty(targetName)
                    ? $"{operationName}: {obj.name}"
                    : $"{operationName} {obj.name} to {targetName}";
            }
            else
            {
                groupName = string.IsNullOrEmpty(targetName)
                    ? $"{operationName} {createdObjects.Count} objects"
                    : $"{operationName} {createdObjects.Count} objects to {targetName}";
            }

            Undo.SetCurrentGroupName(GenerateMcpGroupName(groupName));
        }

        #endregion

        #region Modification Operations

        /// <summary>
        /// Register single object modification with undo and component state tracking
        /// </summary>
        /// <param name="targetObject">The object to be modified</param>
        /// <param name="operationName">Operation description (e.g., "Modify GameObject", "Set Property")</param>
        /// <param name="details">Optional details about the modification</param>
        public static void RegisterModifiedObject(UnityEngine.Object targetObject, string operationName, string details = null)
        {
            RegisterModifiedObjectWithStateTracking(targetObject, operationName, details);
        }
        
        /// <summary>
        /// Register single object modification with undo and component state tracking
        /// </summary>
        /// <param name="targetObject">The object to be modified</param>
        /// <param name="operationName">Operation description (e.g., "Modify GameObject", "Set Property")</param>
        /// <param name="details">Optional details about the modification</param>
        public static void RegisterModifiedObjectWithStateTracking(UnityEngine.Object targetObject, string operationName, string details = null)
        {
            if (targetObject == null) return;

            var gameObject = targetObject as GameObject ?? (targetObject as Component)?.gameObject;
            var currentInstanceID = GetInstanceID(targetObject);
            var shouldGroup = ShouldGroupWithPreviousOperation(currentInstanceID);
            
            // Capture before state if it's a GameObject
            ComponentSnapshot beforeState = default;
            if (gameObject != null)
            {
                beforeState = new ComponentSnapshot(gameObject);
            }
            
            // Only create new group if not grouping with previous operation
            if (!shouldGroup)
            {
                Undo.IncrementCurrentGroup();
            }
            
            Undo.RegisterCompleteObjectUndo(targetObject, $"{operationName}: {targetObject.name}");

            var groupName = string.IsNullOrEmpty(details)
                ? $"{operationName}: {targetObject.name}"
                : $"{operationName} {targetObject.name}: {details}";
            
            string finalGroupName;
            string operationGuid;
            if (shouldGroup)
            {
                // Use the same GUID as the previous operation for grouping
                operationGuid = lastOperationGuid;
                finalGroupName = $"{MCP_PREFIX} [GUID:{operationGuid}] {groupName}";
            }
            else
            {
                // Generate new GUID and group name
                operationGuid = System.Guid.NewGuid().ToString("N")[..8];
                finalGroupName = $"{MCP_PREFIX} [GUID:{operationGuid}] {groupName}";
                UpdateOperationTracking(currentInstanceID, operationGuid);
            }
            
            Undo.SetCurrentGroupName(finalGroupName);
            
            // Schedule state snapshot recording after the operation completes
            if (gameObject != null)
            {
                EditorApplication.delayCall += () =>
                {
                    if (gameObject != null) // Check if still valid
                    {
                        UnityUndoMonitor.AddOperationWithStateSnapshot(operationName, gameObject, beforeState);
                    }
                };
            }
        }

        /// <summary>
        /// Register multiple objects modification with undo (batch operation)
        /// </summary>
        /// <param name="targetObjects">List of objects to be modified</param>
        /// <param name="operationName">Operation description</param>
        /// <param name="details">Optional details about the modification</param>
        public static void RegisterModifiedObjects<T>(IList<T> targetObjects, string operationName, string details = null) where T : UnityEngine.Object
        {
            if (targetObjects == null || targetObjects.Count == 0) return;

            bool shouldGroup = false;
            string operationGuid = "";
            
            // For single object, use smart grouping
            if (targetObjects.Count == 1)
            {
                var currentInstanceID = GetInstanceID(targetObjects[0]);
                shouldGroup = ShouldGroupWithPreviousOperation(currentInstanceID);
                
                if (!shouldGroup)
                {
                    operationGuid = System.Guid.NewGuid().ToString("N")[..8];
                    UpdateOperationTracking(currentInstanceID, operationGuid);
                }
            }
            
            // Only create new group if not grouping with previous operation
            if (!shouldGroup)
            {
                Undo.IncrementCurrentGroup();
            }

            foreach (var obj in targetObjects)
            {
                if (obj != null)
                {
                    Undo.RegisterCompleteObjectUndo(obj, $"{operationName}: {obj.name}");
                }
            }

            string groupName;
            if (targetObjects.Count == 1)
            {
                var obj = targetObjects[0];
                groupName = string.IsNullOrEmpty(details)
                    ? $"{operationName}: {obj.name}"
                    : $"{operationName} {obj.name}: {details}";
            }
            else
            {
                groupName = string.IsNullOrEmpty(details)
                    ? $"{operationName} {targetObjects.Count} objects"
                    : $"{operationName} {targetObjects.Count} objects: {details}";
            }

            string finalGroupName;
            if (shouldGroup)
            {
                // Use the same GUID as the previous operation for grouping
                finalGroupName = $"{MCP_PREFIX} [GUID:{lastOperationGuid}] {groupName}";
            }
            else
            {
                // Generate new GUID and group name
                if (string.IsNullOrEmpty(operationGuid))
                {
                    operationGuid = System.Guid.NewGuid().ToString("N")[..8];
                }
                finalGroupName = $"{MCP_PREFIX} [GUID:{operationGuid}] {groupName}";
            }
            
            Undo.SetCurrentGroupName(finalGroupName);
        }

        #endregion

        #region Destruction Operations

        /// <summary>
        /// Register single object destruction with undo
        /// </summary>
        /// <param name="targetObject">The object to be destroyed</param>
        /// <param name="operationName">Operation description (e.g., "Delete GameObject", "Remove Component")</param>
        /// <param name="destroyImmediately">Whether to destroy the object immediately</param>
        public static void RegisterDestroyedObject(UnityEngine.Object targetObject, string operationName, bool destroyImmediately = true)
        {
            if (targetObject == null) return;

            var objectName = targetObject.name;
            
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(targetObject, $"{operationName}: {objectName}");
            
            if (destroyImmediately)
            {
                Undo.DestroyObjectImmediate(targetObject);
            }

            Undo.SetCurrentGroupName(GenerateMcpGroupName($"{operationName}: {objectName}"));
        }

        /// <summary>
        /// Register multiple objects destruction with undo (batch operation)
        /// </summary>
        /// <param name="targetObjects">List of objects to be destroyed</param>
        /// <param name="operationName">Operation description</param>
        /// <param name="destroyImmediately">Whether to destroy the objects immediately</param>
        public static void RegisterDestroyedObjects<T>(IList<T> targetObjects, string operationName, bool destroyImmediately = true) where T : UnityEngine.Object
        {
            if (targetObjects == null || targetObjects.Count == 0) return;

            var objectNames = new List<string>();
            
            Undo.IncrementCurrentGroup();

            foreach (var obj in targetObjects)
            {
                if (obj != null)
                {
                    objectNames.Add(obj.name);
                    Undo.RegisterCompleteObjectUndo(obj, $"{operationName}: {obj.name}");
                    
                    if (destroyImmediately)
                    {
                        Undo.DestroyObjectImmediate(obj);
                    }
                }
            }

            var groupName = targetObjects.Count == 1
                ? $"{operationName}: {objectNames[0]}"
                : $"{operationName} {targetObjects.Count} objects";

            Undo.SetCurrentGroupName(GenerateMcpGroupName(groupName));
        }

        #endregion

        #region Special Operations

        /// <summary>
        /// Register transform parent change with undo
        /// </summary>
        /// <param name="target">Target transform</param>
        /// <param name="newParent">New parent transform (can be null)</param>
        /// <param name="targetName">Target object name for better description</param>
        public static void RegisterParentChange(Transform target, Transform newParent, string targetName = null)
        {
            if (target == null) return;

            var operationName = $"Set parent for {targetName ?? target.name}";
            Undo.SetTransformParent(target, newParent, operationName);
        }

        /// <summary>
        /// Register multiple transform parent changes with undo (batch operation)
        /// </summary>
        /// <param name="targets">List of target transforms</param>
        /// <param name="newParent">New parent transform (can be null)</param>
        /// <param name="operationName">Custom operation name</param>
        public static void RegisterParentChanges<T>(IList<T> targets, Transform newParent, string operationName = null) where T : Transform
        {
            if (targets == null || targets.Count == 0) return;

            Undo.IncrementCurrentGroup();

            foreach (var target in targets)
            {
                if (target != null)
                {
                    Undo.SetTransformParent(target, newParent, $"Set parent of {target.name}");
                }
            }

            var groupName = operationName ?? (targets.Count == 1
                ? $"Set parent for {targets[0].name}"
                : $"Set parent for {targets.Count} objects");

            Undo.SetCurrentGroupName(GenerateMcpGroupName(groupName));
        }

        /// <summary>
        /// Register object property change with undo (for EditorUtility.SetDirty scenario)
        /// </summary>
        /// <param name="targetObject">The object whose property will change</param>
        /// <param name="operationName">Operation description</param>
        /// <param name="details">Optional details about the change</param>
        public static void RegisterPropertyChange(UnityEngine.Object targetObject, string operationName, string details = null)
        {
            if (targetObject == null) return;

            var groupName = string.IsNullOrEmpty(details)
                ? $"{operationName}: {targetObject.name}"
                : $"{operationName} {targetObject.name}: {details}";

            Undo.RecordObject(targetObject, $"{MCP_PREFIX} {groupName}");
        }

        /// <summary>
        /// Register object state change with undo (using Undo.RecordObject directly)
        /// For UI state changes that don't need full group management
        /// </summary>
        /// <param name="targetObject">The object whose state will change</param>
        /// <param name="operationName">Operation description</param>
        public static void RegisterStateChange(UnityEngine.Object targetObject, string operationName)
        {
            if (targetObject == null) return;

            Undo.RecordObject(targetObject, $"{MCP_PREFIX} {operationName}");
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Start a new undo group without registering any objects
        /// Useful for custom undo scenarios
        /// </summary>
        /// <param name="groupName">The undo group name</param>
        public static void StartUndoGroup(string groupName)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(GenerateMcpGroupName(groupName));
        }

        /// <summary>
        /// Set the current undo group name with MCP prefix
        /// </summary>
        /// <param name="groupName">The undo group name</param>
        public static void SetUndoGroupName(string groupName)
        {
            Undo.SetCurrentGroupName(GenerateMcpGroupName(groupName));
        }

        /// <summary>
        /// Simplify complex values for better readability in undo group names
        /// </summary>
        /// <param name="value">The value to simplify</param>
        /// <returns>Simplified string representation</returns>
        public static string SimplifyValueForUndo(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Simplify Vector3 format: "(1.50, 2.00, 3.00)" -> "(1.5, 2, 3)"
            if (value.StartsWith("(") && value.EndsWith(")") && value.Contains(","))
            {
                var cleaned = value.Trim('(', ')');
                var parts = cleaned.Split(',');
                if (parts.Length == 3)
                {
                    var simplifiedParts = new string[3];
                    for (int i = 0; i < 3; i++)
                    {
                        if (float.TryParse(parts[i].Trim(), out var floatVal))
                        {
                            // Remove unnecessary trailing zeros
                            simplifiedParts[i] = floatVal == Math.Round(floatVal) 
                                ? Math.Round(floatVal).ToString() 
                                : floatVal.ToString("0.##");
                        }
                        else
                        {
                            simplifiedParts[i] = parts[i].Trim();
                        }
                    }
                    return $"({string.Join(", ", simplifiedParts)})";
                }
            }

            // Limit string length
            if (value.Length > 20)
            {
                return $"{value.Substring(0, 17)}...";
            }

            return value;
        }

        #endregion
    }
} 