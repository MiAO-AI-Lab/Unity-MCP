using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unity.MCP
{
    /// <summary>
    /// Component state snapshot for Inspector diff comparison
    /// </summary>
    [System.Serializable]
    public struct ComponentSnapshot
    {
        public Dictionary<string, ComponentState> components; // Component type -> Component state
        public TransformState transformState; // Special handling for Transform
        public DateTime captureTime; // When this snapshot was taken
        
        public ComponentSnapshot(GameObject gameObject)
        {
            components = new Dictionary<string, ComponentState>();
            transformState = new TransformState();
            captureTime = DateTime.Now;
            
            if (gameObject != null)
            {
                CaptureGameObjectState(gameObject);
            }
        }
        
        private void CaptureGameObjectState(GameObject gameObject)
        {
            // Capture Transform state (most common)
            var transform = gameObject.transform;
            transformState = new TransformState
            {
                position = transform.position,
                rotation = transform.rotation,
                localScale = transform.localScale,
                parent = transform.parent != null ? transform.parent.GetInstanceID() : 0
            };
            
            // Capture all components
            var allComponents = gameObject.GetComponents<Component>();
            foreach (var component in allComponents)
            {
                if (component == null) continue;
                
                var componentType = component.GetType().Name;
                if (componentType == "Transform") continue; // Already handled
                
                var componentState = new ComponentState
                {
                    typeName = component.GetType().FullName,
                    enabled = true,
                    properties = new Dictionary<string, object>()
                };
                
                // Try to get enabled state for MonoBehaviour
                if (component is MonoBehaviour monoBehaviour)
                {
                    componentState.enabled = monoBehaviour.enabled;
                }
                else if (component is Renderer renderer)
                {
                    componentState.enabled = renderer.enabled;
                }
                else if (component is Collider collider)
                {
                    componentState.enabled = collider.enabled;
                }
                
                // Capture key properties using reflection
                CaptureComponentProperties(component, componentState);
                
                components[componentType] = componentState;
            }
        }
        
        private void CaptureComponentProperties(Component component, ComponentState state)
        {
            try
            {
                var componentType = component.GetType();
                
                // Capture common properties based on component type
                switch (component)
                {
                    case Rigidbody rb:
                        state.properties["mass"] = rb.mass;
                        state.properties["drag"] = rb.drag;
                        state.properties["angularDrag"] = rb.angularDrag;
                        state.properties["useGravity"] = rb.useGravity;
                        state.properties["isKinematic"] = rb.isKinematic;
                        break;
                        
                    case MeshRenderer mr:
                        state.properties["shadowCastingMode"] = mr.shadowCastingMode.ToString();
                        state.properties["receiveShadows"] = mr.receiveShadows;
                        if (mr.material != null)
                            state.properties["materialName"] = mr.material.name;
                        break;
                        
                    case BoxCollider bc:
                        state.properties["center"] = bc.center;
                        state.properties["size"] = bc.size;
                        state.properties["isTrigger"] = bc.isTrigger;
                        break;
                        
                    case SphereCollider sc:
                        state.properties["center"] = sc.center;
                        state.properties["radius"] = sc.radius;
                        state.properties["isTrigger"] = sc.isTrigger;
                        break;
                        
                    case Light light:
                        state.properties["type"] = light.type.ToString();
                        state.properties["color"] = light.color;
                        state.properties["intensity"] = light.intensity;
                        state.properties["range"] = light.range;
                        break;
                        
                    case Camera cam:
                        state.properties["fieldOfView"] = cam.fieldOfView;
                        state.properties["nearClipPlane"] = cam.nearClipPlane;
                        state.properties["farClipPlane"] = cam.farClipPlane;
                        state.properties["clearFlags"] = cam.clearFlags.ToString();
                        break;
                        
                    default:
                        // For other components, try to capture some common properties via reflection
                        CaptureCommonProperties(component, state);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ComponentSnapshot] Failed to capture properties for {component.GetType().Name}: {ex.Message}");
            }
        }
        
        private void CaptureCommonProperties(Component component, ComponentState state)
        {
            var componentType = component.GetType();
            var properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                try
                {
                    // Only capture readable, writable properties of simple types
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    if (prop.GetIndexParameters().Length > 0) continue; // Skip indexed properties
                    
                    var propType = prop.PropertyType;
                    if (IsSimpleType(propType))
                    {
                        var value = prop.GetValue(component);
                        if (value != null)
                        {
                            state.properties[prop.Name] = value;
                        }
                    }
                }
                catch
                {
                    // Ignore properties that can't be accessed
                }
            }
        }
        
        private bool IsSimpleType(System.Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(Vector3) || 
                   type == typeof(Vector2) || 
                   type == typeof(Quaternion) || 
                   type == typeof(Color) || 
                   type.IsEnum;
        }
    }
    
    /// <summary>
    /// Component state information
    /// </summary>
    [System.Serializable]
    public struct ComponentState
    {
        public string typeName;
        public bool enabled;
        public Dictionary<string, object> properties;
    }
    
    /// <summary>
    /// Transform state information
    /// </summary>
    [System.Serializable]
    public struct TransformState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public int parent; // Parent's InstanceID
    }

    /// <summary>
    /// Unity undo stack monitor - Real-time monitoring of all undo operations
    /// </summary>
    public static class UnityUndoMonitor
    {
        /// <summary>
        /// Undo operation data structure
        /// </summary>
        public struct UndoOperation
        {
            public int groupId;
            public string operationName;
            public bool isMcpOperation;
            public DateTime timestamp;
            public string operationGuid; // Unique identifier for this operation
            public int targetInstanceID; // GameObject instanceID for accurate object tracking
            public ComponentSnapshot beforeState; // Component state before operation
            public ComponentSnapshot afterState; // Component state after operation
            
            /// <summary>
            /// Generate operation signature for duplicate detection
            /// </summary>
            public string GenerateSignature()
            {
                return $"{groupId}:{operationName}:{isMcpOperation}";
            }
            
            /// <summary>
            /// Display name in format: 操作 -> ObjectName(instanceID)
            /// </summary>
            public string DisplayName
            {
                get
                {
                    var objectInfo = GetObjectDisplayInfo();
                    
                    return $"{operationName} -> {objectInfo}";
                }
            }
            
            /// <summary>
            /// Get object display information in format: ObjectName(instanceID)
            /// </summary>
            private string GetObjectDisplayInfo()
            {
                if (targetInstanceID == 0)
                {
                    return "Unknown";
                }
                
                try
                {
                    var targetObject = UnityEditor.EditorUtility.InstanceIDToObject(targetInstanceID);
                    if (targetObject == null)
                    {
                        return $"Deleted({targetInstanceID})";
                    }
                    
                    return $"{targetObject.name}({targetInstanceID})";
                }
                catch
                {
                    return $"Invalid({targetInstanceID})";
                }
            }
            
            /// <summary>
            /// Simply extract operation name from raw undo operation, removing MCP prefix if present
            /// </summary>
            public static string ExtractOperationName(string rawOperationName)
            {
                if (string.IsNullOrEmpty(rawOperationName))
                    return "Unknown Operation";
                
                var cleanName = rawOperationName.Trim();
                
                // Remove MCP prefix and GUID if present
                if (cleanName.StartsWith("[MCP]"))
                {
                    cleanName = cleanName.Substring(5).Trim();
                    
                    // Remove GUID part if present: [GUID:xxxxxxxx] operationName
                    var guidMatch = System.Text.RegularExpressions.Regex.Match(cleanName, @"^\[GUID:[a-fA-F0-9]{8}\]\s*(.*)$");
                    if (guidMatch.Success)
                    {
                        cleanName = guidMatch.Groups[1].Value.Trim();
                    }
                }
                
                return string.IsNullOrEmpty(cleanName) ? "Unknown Operation" : cleanName;
            }
        }
        
        private static int lastTrackedGroup = -1;
        private static readonly List<UndoOperation> allOperations = new List<UndoOperation>();
        private static readonly List<UndoOperation> redoOperations = new List<UndoOperation>(); // redo stack
        private static bool isInitialized = false;
        private static float lastCheckTime = 0f;
        private static bool isPerformingUndoRedo = false; // Prevent recursion during undo/redo
        private static bool isCustomUndoRedo = false; // Mark whether it's a custom undo/redo operation
        private static bool isRefreshingUI = false; // Mark whether UI is being refreshed
        
        // GUID-based operation uniqueness tracking
        private static readonly HashSet<string> processedOperationGuids = new HashSet<string>(); // Processed operation GUIDs to prevent duplicates
        
        // Manual operation duplicate detection - track last manual operation
        private static string lastManualOperationName = null; // Last manual operation name for consecutive duplicate detection
        private static int lastManualOperationTargetID = -1; // Last manual operation target GameObject InstanceID
        
        // Track selection state changes
        private static int lastSelectedInstanceID = -1;
        private static int lastSelectionCount = 0;
        private static int lastSceneObjectCount = 0;
        
        // Counter for detecting actual undo/redo operations
        private static int lastUnityUndoCount = 0;
        
        // Ignore window after Undo/Redo operations
        private static DateTime lastUndoRedoTime = DateTime.MinValue;
        private static readonly TimeSpan UNDO_REDO_IGNORE_THRESHOLD = TimeSpan.FromSeconds(3);
        
        /// <summary>
        /// Event triggered when all operations change
        /// </summary>
        public static event System.Action OnOperationsChanged;
        
        /// <summary>
        /// Initialize listener - Only monitor undoable operations
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!isInitialized)
            {
                // Initialize selection state tracking
                UpdateSelectionState();
                
                // Add main update listener
                EditorApplication.update += MonitorUndoStack;
                
                // Listen for undo/redo operations
                Undo.undoRedoPerformed += OnUndoRedoPerformed;
                
                // Listen for selection changes, as selection operations are also undoable
                Selection.selectionChanged += OnSelectionChanged;
                
                isInitialized = true;
                
                // Get current undo group and Unity's undo count during initialization
                lastTrackedGroup = Undo.GetCurrentGroup() - 1;
                lastUnityUndoCount = GetUnityUndoStackCount();
            }
        }
        
        /// <summary>
        /// Synchronize internal operation stacks with Unity's undo/redo state
        /// </summary>
        private static void SynchronizeStacksWithUnity()
        {
            try
            {
                var currentUnityUndoCount = GetUnityUndoStackCount();
                var undoCountDiff = currentUnityUndoCount - lastUnityUndoCount;
                
                if (undoCountDiff > 0)
                {
                    // Unity undo count increased = undo operation was performed
                    // Move operation from undo to redo stack
                    if (allOperations.Count > 0)
                    {
                        var operationToMove = allOperations[allOperations.Count - 1];
                        allOperations.RemoveAt(allOperations.Count - 1);
                        redoOperations.Add(operationToMove);
                    }
                }
                else if (undoCountDiff < 0)
                {
                    // Unity undo count decreased = redo operation was performed
                    // Move operation from redo to undo stack
                    if (redoOperations.Count > 0)
                    {
                        var operationToMove = redoOperations[redoOperations.Count - 1];
                        redoOperations.RemoveAt(redoOperations.Count - 1);
                        allOperations.Add(operationToMove);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityUndoMonitor] Error synchronizing stacks: {e.Message}");
            }
        }
        
        /// <summary>
        /// Triggered when undo/redo operation is executed
        /// </summary>
        private static void OnUndoRedoPerformed()
        {
            isPerformingUndoRedo = true;
            
            // Delay checking undo stack state and reset flags
            EditorApplication.delayCall += () =>
            {
                // Only synchronize for non-custom operations (Unity native undo/redo)
                if (!isCustomUndoRedo)
                {
                    // Synchronize internal operation stacks with Unity's undo/redo state
                    SynchronizeStacksWithUnity();
                }
                
                // Update selection state
                UpdateSelectionState();
                
                // Synchronize undo group state to prevent detecting internal group changes from system undo/redo operations
                lastTrackedGroup = Undo.GetCurrentGroup();
                lastUnityUndoCount = GetUnityUndoStackCount();
                lastUndoRedoTime = DateTime.Now; // Set ignore time window
                
                // Add additional delay to ensure Unity completes all related internal operations before resetting flags
                EditorApplication.delayCall += () =>
                {
                    // Final synchronization
                    lastTrackedGroup = Undo.GetCurrentGroup();
                    lastUnityUndoCount = GetUnityUndoStackCount();
                    isPerformingUndoRedo = false;
                    isCustomUndoRedo = false; // Reset custom flag
                    
                    // Trigger UI update
                    OnOperationsChanged?.Invoke();
                };
            };
        }
        
        /// <summary>
        /// Triggered when selection changes
        /// </summary>
        private static void OnSelectionChanged()
        {
            // If performing undo/redo operations, ignore selection changes to avoid recursion
            if (isPerformingUndoRedo)
            {
                return;
            }
            
            // Delay slightly to ensure possible undo groups have been created
            EditorApplication.delayCall += () =>
            {
                if (!isPerformingUndoRedo) // Check again to ensure no undo operation started during delay
                {
                    MonitorUndoStack();
                }
            };
        }
        
        /// <summary>
        /// Real-time monitoring of undo stack changes - Only listen for undoable operations
        /// </summary>
        private static void MonitorUndoStack()
        {
            try
            {
                // Limit check frequency to avoid excessive monitoring
                var currentTime = (float)EditorApplication.timeSinceStartup;
                if (currentTime - lastCheckTime < 0.1f) // Check at most once every 100ms
                {
                    return;
                }
                lastCheckTime = currentTime;
                
                var currentGroup = Undo.GetCurrentGroup();
                var currentUnityUndoCount = GetUnityUndoStackCount();
                
                // Skip detection logic if performing undo/redo operations or refreshing UI
                if (isPerformingUndoRedo || isRefreshingUI)
                {
                    lastTrackedGroup = currentGroup;
                    lastUnityUndoCount = currentUnityUndoCount;
                    return; // Don't execute logic for adding new operations during undo/redo or UI refresh
                }
                
                // Detect new operations - Process immediately, with early GUID filtering
                if (currentGroup > lastTrackedGroup)
                {
                    // Get current group name
                    var currentGroupName = "";
                    try
                    {
                        currentGroupName = Undo.GetCurrentGroupName();
                    }
                    catch { }
                    
                    
                    // Early duplicate detection for both MCP and manual operations
                    bool shouldProcess = true;
                    if (!string.IsNullOrEmpty(currentGroupName))
                    {
                        if (currentGroupName.StartsWith("[MCP]"))
                        {
                            // MCP operation: GUID-based duplicate detection
                            var operationGuid = ExtractGuidFromGroupName(currentGroupName);
                            
                            // Reset manual operation tracking when MCP operation occurs
                            lastManualOperationName = null;
                            lastManualOperationTargetID = -1;
                            
                            if (!string.IsNullOrEmpty(operationGuid) && processedOperationGuids.Contains(operationGuid))
                            {
                                // Operation already processed, skip entirely
                                shouldProcess = false;
                            }
                            else if (!string.IsNullOrEmpty(operationGuid))
                            {
                                // Mark this GUID as processed
                                processedOperationGuids.Add(operationGuid);
                            }
                        }
                        else
                        {
                            // Manual operation: consecutive duplicate detection
                            if (IsManualOperationDuplicate(currentGroupName))
                            {
                                shouldProcess = false;
                            }
                        }
                    }
                    
                    // Process new operation only if not filtered out
                    if (shouldProcess)
                    {
                        ProcessNewOperation(currentGroup, currentGroupName);
                    }
                    lastTrackedGroup = currentGroup;
                    
                    // Update Unity undo count tracking when adding new operations
                    currentUnityUndoCount = GetUnityUndoStackCount();
                    lastUnityUndoCount = currentUnityUndoCount;
                }
                
                // Normal undo/redo detection has been moved to PerformUndo/PerformRedo methods for direct handling
                // Only need to update count tracking here
                lastUnityUndoCount = currentUnityUndoCount;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityUndoMonitor] Monitoring error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Unified method for processing new operations
        /// </summary>
        private static void ProcessNewOperation(int currentGroup, string currentGroupName)
        {
            // Check if within ignore window after undo/redo operation
            if (DateTime.Now - lastUndoRedoTime < UNDO_REDO_IGNORE_THRESHOLD)
            {
                return;
            }
            
            string groupName = currentGroupName;
            
            // Process operations with explicit group names
            if (!string.IsNullOrEmpty(groupName) && IsValidUndoableOperation(groupName))
            {
                var extractedName = UndoOperation.ExtractOperationName(groupName);
                bool shouldRecord = true;
                
                // Delete operation detection has been moved to duplicate detection logic, no longer need time recording
                
                // For selection operations, must first check if selection state actually changed
                if (extractedName.StartsWith("Select ") || extractedName == "Clear Selection")
                {
                    var selectionChangeResult = InferSelectionOperationType(currentGroup);
                    if (string.IsNullOrEmpty(selectionChangeResult))
                    {
                        shouldRecord = false;
                    }
                    else
                    {
                        // Check if it's auto-selection after delete operation
                        if (IsAutoSelectionAfterDelete())
                        {
                            shouldRecord = false;
                        }
                    }
                }
                
                if (shouldRecord)
                {
                    // Extract GUID for MCP operations (for tracking purposes)
                    string operationGuid = null;
                    if (groupName.StartsWith("[MCP]"))
                    {
                        operationGuid = ExtractGuidFromGroupName(groupName);
                    }
                    
                    // For delete and copy operations, check if temporary selection operations should be removed
                    if (IsDeleteOperation(extractedName) || IsCopyOperation(extractedName))
                    {
                        CheckAndRemoveTemporarySelection();
                    }
                    
                    
                    // Add the operation with cleaned name
                    var cleanedName = UndoOperation.ExtractOperationName(groupName);
                    AddOperationSimple(currentGroup, cleanedName, groupName.StartsWith("[MCP]"), operationGuid);
                }
                else
                {
                    // Operation skipped (shouldRecord=false)
                }
            }
            else if (string.IsNullOrEmpty(groupName))
            {
                // For operations without explicit group names, perform limited inference
                // Mainly handle selection operations and other important undoable operations
                var inferredName = InferSelectionOperationType(currentGroup);
                
                if (!string.IsNullOrEmpty(inferredName) && IsValidUndoableOperation(inferredName))
                {
                    // Check if it's auto-selection after delete operation
                    if (IsAutoSelectionAfterDelete())
                    {
                        return;
                    }
                    
                    // Check if duplicate with recent operations
                    if (!IsDuplicateOperation(inferredName))
                    {
                        var cleanedName = UndoOperation.ExtractOperationName(inferredName);
                        AddOperationSimple(currentGroup, cleanedName, false, null);
                    }
                    else
                    {
                        // Operation is duplicate, skip
                    }
                }
                else
                {
                    // Operation is invalid or empty, skip
                }
            }
        }
        
        /// <summary>
        /// Check if it's a delete operation
        /// </summary>
        private static bool IsDeleteOperation(string operationName)
        {
            var lowerName = operationName.ToLower();
            return lowerName.Contains("delete") || 
                   lowerName.Contains("destroy") || 
                   lowerName.Contains("remove") ||
                   lowerName.Contains("clear");
        }
        
        /// <summary>
        /// Check if it's a copy operation
        /// </summary>
        private static bool IsCopyOperation(string operationName)
        {
            var lowerName = operationName.ToLower();
            return lowerName.Contains("copy") || 
                   lowerName.Contains("duplicate") || 
                   lowerName.Contains("paste") ||
                   lowerName.Contains("clone");
        }
        
        /// <summary>
        /// Check if it's auto-selection after delete operation (based on sequence detection, not time-dependent)
        /// </summary>
        private static bool IsAutoSelectionAfterDelete()
        {
            // Check if there's a delete operation in the last 2 operations
            var recentOps = allOperations.TakeLast(2).ToList();
            return recentOps.Any(op => IsDeleteOperation(op.operationName));
        }
        
        /// <summary>
        /// Check and remove temporary selection operations before delete operations
        /// When user selects an object and immediately deletes it, the selection operation should be considered temporary and removed
        /// </summary>
        private static void CheckAndRemoveTemporarySelection()
        {
            if (allOperations.Count == 0) return;
            
            // Check if the last operation is a selection operation
            var lastOp = allOperations[allOperations.Count - 1];
            if (lastOp.operationName.StartsWith("Select ") && !lastOp.isMcpOperation)
            {
                // Check if there's only 1 operation, or the previous operation is not a selection operation
                // This can avoid removing meaningful selection sequences
                bool shouldRemove = false;
                
                if (allOperations.Count == 1)
                {
                    // If there's only one selection operation followed by deletion, this selection is temporary
                    shouldRemove = true;
                }
                else
                {
                    // Check the previous operation, if it's not a selection operation, this is an isolated selection→delete operation
                    var previousOp = allOperations[allOperations.Count - 2];
                    if (!previousOp.operationName.StartsWith("Select ") && !previousOp.operationName.Equals("Clear Selection"))
                    {
                        shouldRemove = true;
                    }
                }
                
                if (shouldRemove)
                {
                    // Remove this temporary selection operation
                    allOperations.RemoveAt(allOperations.Count - 1);
                    
                    // Trigger UI update
                    OnOperationsChanged?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// Extract GUID from MCP group name
        /// </summary>
        /// <param name="groupName">The Unity undo group name</param>
        /// <returns>GUID if found, null otherwise</returns>
        private static string ExtractGuidFromGroupName(string groupName)
        {
            if (string.IsNullOrEmpty(groupName) || !groupName.StartsWith("[MCP]"))
                return null;
                
            // Pattern: [MCP] [GUID:xxxxxxxx] operationName
            var guidMatch = System.Text.RegularExpressions.Regex.Match(groupName, @"\[GUID:([a-fA-F0-9]{8})\]");
            return guidMatch.Success ? guidMatch.Groups[1].Value : null;
        }

        /// <summary>
        /// Get target object instanceID from current selection
        /// </summary>
        private static int GetTargetInstanceID()
        {
            var selectedObjects = UnityEditor.Selection.gameObjects;
            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                return selectedObjects[0].GetInstanceID();
            }
            
            return 0; // No selection
        }



        /// <summary>
        /// Unified method for adding new operations with component state snapshots
        /// </summary>
        private static void AddOperation(int groupId, string operationName, bool isMcpOperation, string operationGuid = null, int targetInstanceID = 0, ComponentSnapshot beforeState = default, ComponentSnapshot afterState = default)
        {
            // Generate GUID if not provided
            if (string.IsNullOrEmpty(operationGuid))
            {
                operationGuid = System.Guid.NewGuid().ToString();
            }
            
            // If targetInstanceID is not provided, try to get it from current selection
            if (targetInstanceID == 0)
            {
                targetInstanceID = GetTargetInstanceID();
            }
            
            // If no state snapshots provided, try to capture current state as "after" state
            if (afterState.Equals(default(ComponentSnapshot)) && targetInstanceID != 0)
            {
                var targetObject = UnityEditor.EditorUtility.InstanceIDToObject(targetInstanceID) as GameObject;
                if (targetObject != null)
                {
                    afterState = new ComponentSnapshot(targetObject);
                }
            }
            
            var operation = new UndoOperation
            {
                groupId = groupId,
                operationName = operationName,
                isMcpOperation = isMcpOperation,
                timestamp = DateTime.Now,
                operationGuid = operationGuid,
                targetInstanceID = targetInstanceID,
                beforeState = beforeState,
                afterState = afterState
            };
            
            allOperations.Add(operation);
            
            // When new operation is added, clear redo stack (standard undo/redo behavior)
            // But if performing undo/redo operations, don't clear redo stack
            if (redoOperations.Count > 0 && !isPerformingUndoRedo)
            {
                redoOperations.Clear();
            }
            
            // If it's a selection operation, update selection state tracking
            if (operationName.StartsWith("Select ") || operationName == "Clear Selection")
            {
                UpdateSelectionState();
            }
            
            // Immediately trigger event, UI layer will handle delayed refresh
            OnOperationsChanged?.Invoke();
        }
        
        /// <summary>
        /// Simplified method for adding operations without state snapshots (backward compatibility)
        /// </summary>
        private static void AddOperationSimple(int groupId, string operationName, bool isMcpOperation, string operationGuid = null, int targetInstanceID = 0)
        {
            // Call the full version with default snapshots
            AddOperation(groupId, operationName, isMcpOperation, operationGuid, targetInstanceID, default, default);
        }
        
        /// <summary>
        /// Public method for adding operations with component state snapshots (for MCP operations)
        /// </summary>
        public static void AddOperationWithStateSnapshot(string operationName, GameObject targetObject, ComponentSnapshot beforeState)
        {
            if (targetObject == null) return;
            
            var targetInstanceID = targetObject.GetInstanceID();
            var afterState = new ComponentSnapshot(targetObject);
            var operationGuid = System.Guid.NewGuid().ToString();
            
            AddOperation(Undo.GetCurrentGroup(), operationName, true, operationGuid, targetInstanceID, beforeState, afterState);
        }
        
        /// <summary>
        /// Get Unity's internal actual undo stack count
        /// </summary>
        private static int GetUnityUndoStackCount()
        {
            try
            {
                // Use safer reflection method to get Unity's internal undo count
                var undoType = typeof(Undo);
                
                // First try to get the parameterless GetRecords method
                MethodInfo getRecordsMethod = null;
                try
                {
                    // Try to get the parameterless version of GetRecords method
                    getRecordsMethod = undoType.GetMethod("GetRecords", 
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                        null, 
                        Type.EmptyTypes, 
                        null);
                }
                catch (AmbiguousMatchException)
                {
                    // If still ambiguous, try to get all GetRecords methods and find the most suitable one
                    var methods = undoType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                        .Where(m => m.Name == "GetRecords")
                        .ToArray();
                    
                    // Prefer parameterless method
                    getRecordsMethod = methods.FirstOrDefault(m => m.GetParameters().Length == 0);
                    
                    // If no parameterless method, choose the one with fewest parameters
                    if (getRecordsMethod == null && methods.Length > 0)
                    {
                        getRecordsMethod = methods.OrderBy(m => m.GetParameters().Length).First();
                    }
                }
                
                if (getRecordsMethod != null)
                {
                    var paramCount = getRecordsMethod.GetParameters().Length;
                    object records = null;
                    
                    if (paramCount == 0)
                    {
                        records = getRecordsMethod.Invoke(null, null);
                    }
                    else
                    {
                        // For methods with parameters, provide suitable default values
                        var parameters = new object[paramCount];
                        for (int i = 0; i < paramCount; i++)
                        {
                            var paramType = getRecordsMethod.GetParameters()[i].ParameterType;
                            parameters[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                        }
                        records = getRecordsMethod.Invoke(null, parameters);
                    }
                    
                    if (records != null)
                    {
                        // records should be an array or list
                        if (records is System.Array array)
                        {
                            return array.Length;
                        }
                        else if (records is System.Collections.ICollection collection)
                        {
                            return collection.Count;
                        }
                    }
                }
                
                // Fallback: Use current group ID as estimate
                var currentGroup = Undo.GetCurrentGroup();
                return Mathf.Max(0, currentGroup);
            }
            catch (Exception)
            {
                // Fallback: Use group ID as approximation
                var currentGroup = Undo.GetCurrentGroup();
                return Mathf.Max(0, currentGroup);
            }
        }
        
        /// <summary>
        /// Smart inference of operation type
        /// </summary>
        private static string InferOperationType(int groupId)
        {
            try
            {
                // Infer based on common Unity operation patterns
                var now = DateTime.Now;
                var lastOp = allOperations.LastOrDefault();
                var timeDiff = allOperations.Count > 0 ? now - lastOp.timestamp : TimeSpan.Zero;
                
                // Inference based on time intervals
                if (timeDiff.TotalMilliseconds < 50)
                {
                    return "Continuous Edit";
                }
                else if (timeDiff.TotalMilliseconds < 200)
                {
                    return "Quick Action";
                }
                
                // Inference based on recent operation patterns
                var recentOps = allOperations.TakeLast(5).Where(op => !op.isMcpOperation).ToList();
                
                // Check for repeated operation patterns
                if (recentOps.Count >= 2)
                {
                    var lastManualOp = recentOps.LastOrDefault();
                    if (recentOps.Count > 0)
                    {
                        if (lastManualOp.operationName.Contains("Selection"))
                        {
                            return "Inspector Change";
                        }
                        else if (lastManualOp.operationName.Contains("Transform") || lastManualOp.operationName.Contains("Position"))
                        {
                            return "Transform Edit";
                        }
                    }
                }
                
                // Inference based on current editor state
                if (Selection.activeGameObject != null)
                {
                    // Has selected object, might be related operation
                    var hasRecentSelection = recentOps.Any(op => 
                        op.operationName.Contains("Selection") || op.operationName.Contains("Select"));
                    
                    if (!hasRecentSelection)
                    {
                        return "Selection Change";
                    }
                    else
                    {
                        return "Object Edit";
                    }
                }
                
                // Check scene state
                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (activeScene.isDirty)
                {
                    // Scene has changes, infer type based on recent operations
                    if (recentOps.Any(op => op.operationName.Contains("Property")))
                    {
                        return "Component Edit";
                    }
                    else if (recentOps.Any(op => op.operationName.Contains("Transform")))
                    {
                        return "Position Change";
                    }
                    else
                    {
                        return "Scene Edit";
                    }
                }
                
                // Inference based on tool mode
                if (Tools.current == Tool.Move)
                {
                    return "Move Tool";
                }
                else if (Tools.current == Tool.Rotate)
                {
                    return "Rotate Tool";
                }
                else if (Tools.current == Tool.Scale)
                {
                    return "Scale Tool";
                }
                
                // Default category
                return $"Editor Action";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityUndoMonitor] Error inferring operation type: {ex.Message}");
                return $"Unknown Action #{groupId}";
            }
        }
        
        /// <summary>
        /// Check if undo group contains valid undoable content
        /// </summary>
        private static bool HasValidUndoContent(int groupId)
        {
            try
            {
                // Check if current undo group has actual content
                // Unity's undo system creates groups for many non-undoable operations, but these groups are usually empty
                
                // Method 1: Check undo count - but this check might be too strict
                var currentUndoCount = GetUndoCount();
                
                // Method 2: Try to get group name
                var groupName = "";
                try
                {
                    groupName = Undo.GetCurrentGroupName();
                }
                catch { }
                
                // If it's an MCP operation, always consider it valid
                if (!string.IsNullOrEmpty(groupName) && groupName.StartsWith("[MCP]"))
                {
                    return true;
                }
                
                // Method 3: Judge based on group name content - only filter explicitly invalid operations
                if (!string.IsNullOrEmpty(groupName))
                {
                    var lowerName = groupName.ToLower();
                    
                    // Only filter explicitly invalid UI operations
                    if (lowerName.Contains("console") || 
                        lowerName.Contains("log") ||
                        lowerName.Contains("window") ||
                        lowerName.Contains("tab") ||
                        lowerName.Contains("focus") ||
                        lowerName.Contains("click") && !lowerName.Contains("select"))
                    {
                        return false;
                    }
                }
                
                // Method 4: Lenient verification - default to valid
                // If has group name or undo count, consider it might be a valid operation
                if (!string.IsNullOrEmpty(groupName) || currentUndoCount > 0)
                {
                    return true;
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityUndoMonitor] Error checking undo content: {e.Message}");
                return false; // Consider invalid when error occurs
            }
        }
        
        /// <summary>
        /// Check if it's a duplicate operation - Enhanced version, specially handles delete, MCP and other key operations
        /// </summary>
        private static bool IsDuplicateOperation(string operationName)
        {
            if (allOperations.Count == 0)
                return false;
            
            var extractedName = UndoOperation.ExtractOperationName(operationName);
            
            // For MCP operations, more sophisticated duplicate detection
            if (operationName.StartsWith("[MCP]"))
            {
                // Check if there are identical MCP operations in the last 3 operations
                var recentOperations = allOperations.TakeLast(3).ToList();
                
                // For MCP operations, we need to check the complete operation name, not just the operation type
                // This allows multiple operations of the same type on different objects
                var identicalMcpOps = recentOperations.Where(op => 
                    op.operationName == extractedName && 
                    op.isMcpOperation).ToList();
                
                // Only consider it duplicate if the exact same operation name exists recently
                // AND it was within the last 2 seconds (to handle rapid identical operations)
                if (identicalMcpOps.Count > 0)
                {
                    var lastIdentical = identicalMcpOps.Last();
                    var timeDifference = DateTime.Now - lastIdentical.timestamp;
                    
                    // If the identical operation was very recent (within 2 seconds), it might be a duplicate
                    // But if it contains different object information, allow it
                    if (timeDifference.TotalSeconds < 2)
                    {
                        // Check if the operation contains object-specific information (GameObject names, IDs, etc.)
                        // If it does, and the information is different, it's not a duplicate
                        if (ContainsObjectSpecificInfo(extractedName))
                        {
                            return false; // Allow operations with different object-specific information
                        }
                        return true; // Consider it duplicate only if no object-specific info
                    }
                }
                
                return false; // Not a duplicate for MCP operations
            }
            // For delete operations, judge uniqueness based on scene object count change (each delete reduces objects, even if operation names are same)
            else if (IsDeleteOperation(extractedName))
            {
                // Delete operations always reduce object count, even if operation names are same
                // We confirm this is a genuine new operation by checking if scene object count decreased
                if (allOperations.Count > 0)
                {
                    var lastOp = allOperations.Last();
                    if (lastOp.operationName == extractedName && !lastOp.isMcpOperation)
                    {
                        // Check if object count in scene decreased
                        var currentObjectCount = GetSceneObjectCount();
                        var objectCountDecreased = currentObjectCount < lastSceneObjectCount;
                        
                        if (objectCountDecreased)
                        {
                            // Object count in scene decreased, indicating objects were deleted, this is a valid delete operation
                            lastSceneObjectCount = currentObjectCount; // Update count
                            return false; // Not duplicate, allow recording
                        }
                        else
                        {
                            // Object count unchanged, might be UI refresh causing duplicate
                            return true;
                        }
                    }
                }
                
                // Update object count for next comparison
                lastSceneObjectCount = GetSceneObjectCount();
                return false;
            }
            // For copy operations, judge uniqueness based on scene object count change (each copy creates new objects, even if source objects are same)
            else if (IsCopyOperation(extractedName))
            {
                // Copy operations always create new object instances, even if source objects are same
                // We confirm this is a genuine new operation by checking if scene object count increased
                if (allOperations.Count > 0)
                {
                    var lastOp = allOperations.Last();
                    if (lastOp.operationName == extractedName && !lastOp.isMcpOperation)
                    {
                        // Check if object count in scene increased
                        var currentObjectCount = GetSceneObjectCount();
                        var objectCountIncreased = currentObjectCount > lastSceneObjectCount;
                        
                        if (objectCountIncreased)
                        {
                            // Object count in scene increased, indicating new objects were created, this is a valid copy operation
                            lastSceneObjectCount = currentObjectCount; // Update count
                            return false; // Not duplicate, allow recording
                        }
                        else
                        {
                            // Object count unchanged, might be UI refresh causing duplicate
                            return true;
                        }
                    }
                }
                
                // Update object count for next comparison
                lastSceneObjectCount = GetSceneObjectCount();
                return false;
            }
            else
            {
                // For other operations, use more nuanced duplicate detection
                var recentOperations = allOperations.TakeLast(3).ToList();
                
                // For selection operations, only check the last operation
                if (extractedName.StartsWith("Select ") || extractedName == "Clear Selection")
                {
                    if (allOperations.Count > 0 && allOperations.Last().operationName == extractedName)
                    {
                        return true;
                    }
                }
                // For state-changing operations, use time-based duplicate detection
                else if (IsStateChangingOperation(extractedName))
                {
                    // State-changing operations should be allowed to repeat as they represent distinct user actions
                    // Only filter out operations that are identical and occur within a very short time window
                    if (allOperations.Count > 0)
                    {
                        var lastOp = allOperations.Last();
                        if (lastOp.operationName == extractedName && 
                            !lastOp.isMcpOperation &&
                            (DateTime.Now - lastOp.timestamp).TotalMilliseconds < 500) // 0.5 seconds
                        {
                            return true; // Same operation within 0.5 seconds - likely a duplicate
                        }
                    }
                    return false; // Allow distinct state-changing operations
                }
                // For other operations (non-transform, non-selection), use the old logic
                else
                {
                    var identicalOps = recentOperations.Where(op => op.operationName == extractedName && !op.isMcpOperation).ToList();
                    if (identicalOps.Count > 0)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if operation represents a state-changing action (not just viewing/selecting)
        /// Uses heuristic patterns to identify operations that modify object state
        /// </summary>
        private static bool IsStateChangingOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return false;
                
            var lowerName = operationName.ToLower();
            
            // Define patterns for state-changing operations (generic approach)
            var stateChangingPrefixes = new[]
            {
                "set ",        // Set Position, Set Rotation, Set Scale, Set Value, etc.
                "add ",        // Add Component, Add Item, etc.
                "remove ",     // Remove Component, Remove Item, etc.
                "create ",     // Create GameObject, Create Asset, etc.
                "delete ",     // Delete Object, Delete File, etc.
                "destroy ",    // Destroy GameObject, etc.
                "modify ",     // Modify Property, Modify Settings, etc.
                "change ",     // Change Value, Change Settings, etc.
                "update ",     // Update Value, Update Settings, etc.
                "edit ",       // Edit Property, Edit Value, etc.
                "move ",       // Move Object, Move Component, etc.
                "copy ",       // Copy Object, Copy Component, etc.
                "paste ",      // Paste Object, Paste Component, etc.
                "duplicate ",  // Duplicate Object, etc.
                "rename ",     // Rename Object, etc.
                "replace ",    // Replace Component, Replace Value, etc.
                "assign ",     // Assign Value, Assign Reference, etc.
                "apply ",      // Apply Changes, Apply Settings, etc.
                "toggle "      // Toggle Setting, Toggle Active, etc.
            };
            
            // Check if operation starts with any state-changing prefix
            foreach (var prefix in stateChangingPrefixes)
            {
                if (lowerName.StartsWith(prefix))
                {
                    return true;
                }
            }
            
            // Define patterns for non-state-changing operations (viewing/selection)
            var nonStateChangingPrefixes = new[]
            {
                "select ",
                "deselect ",
                "clear selection",
                "focus ",
                "view ",
                "show ",
                "hide ",
                "highlight ",
                "inspect ",
                "expand ",
                "collapse ",
                "scroll ",
                "zoom ",
                "pan "
            };
            
            // If it's explicitly a non-state-changing operation, return false
            foreach (var prefix in nonStateChangingPrefixes)
            {
                if (lowerName.StartsWith(prefix) || lowerName.Contains(prefix))
                {
                    return false;
                }
            }
            
            // For ambiguous cases, assume it's state-changing (safer approach)
            // This allows more operations to be recorded rather than filtered out
            return true;
        }

        /// <summary>
        /// Check if a word exists as a complete word in the text using word boundaries
        /// </summary>
        private static bool ContainsWholeWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
                return false;
            
            // Use regex with word boundaries to match complete words only
            var pattern = $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b";
            return System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        /// <summary>
        /// Determine if operation is a valid undoable operation
        /// </summary>
        private static bool IsValidUndoableOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
            {
                return false;
            }
            
            // Explicit MCP operations are always valid
            if (operationName.StartsWith("[MCP]"))
            {
                return true;
            }
            
            var lowerName = operationName.ToLower();
            
            // **Priority check for Unity native selection operation format**
            if (lowerName.StartsWith("select ") && lowerName.Contains("(gameobject)"))
            {
                return true;
            }
            
            // Clear selection operation
            if (lowerName == "clear selection")
            {
                return true;
            }
            
            // STEP 1: Check for clearly valid editing operations first (positive matching)
            var validEditingKeywords = new[]
            {
                "create", "delete", "destroy", "duplicate", "copy", "paste", "clone",
                "move", "rotate", "scale", "rename", "transform", "modify", "edit",
                "drag", "drop", "instantiate", "spawn", "add", "place", "insert",
                "new", "gameobject", "prefab", "asset", "import", "build", "generate","set"
            };
            
            foreach (var keyword in validEditingKeywords)
            {
                if (ContainsWholeWord(lowerName, keyword))
                {
                    return true;
                }
            }
            
            // STEP 2: Check for clearly invalid UI/system operations (negative matching)
            var invalidUIKeywords = new[]
            {
                "console", "window", "panel", "tab", "focus", "click", "ui",
                "inspector", "editor", "quick", "continuous", "selection change",
                "internal", "system", "temp", "debug"
            };
            
            foreach (var keyword in invalidUIKeywords)
            {
                if (ContainsWholeWord(lowerName, keyword) || lowerName.Contains(keyword))
                {
                    return false;
                }
            }
            
            // STEP 3: Check for composite operations (phrases that should be valid)
            var validCompositeOperations = new[]
            {
                "add component", "remove component", "component edit", "property change",
                "position change", "scene modification", "hierarchy change", "clear selection"
            };
            
            foreach (var operation in validCompositeOperations)
            {
                if (lowerName.Contains(operation))
                {
                    return true;
                }
            }
            
            // STEP 4: Conservative approach - If it contains "select" or "clear", it's likely valid
            if (ContainsWholeWord(lowerName, "select") || ContainsWholeWord(lowerName, "clear"))
            {
                return true;
            }
            
            // STEP 5: Lenient filter for operations that look like real editing operations
            // Must be longer than 3 characters and not obviously invalid
            if (lowerName.Length > 3 && 
                !lowerName.Contains("internal") &&
                !lowerName.Contains("system") &&
                !lowerName.Contains("temp") &&
                !lowerName.Contains("debug"))
            {
                return true; // Default to valid for ambiguous cases
            }
            
            // Default case: Only filter out obviously invalid operations
            return false;
        }

        /// <summary>
        /// Specifically infer selection-related operation types
        /// Only detect selection state changes, don't update state (state update is caller's responsibility)
        /// </summary>
        private static string InferSelectionOperationType(int groupId)
        {
            try
            {
                // Check current selection state
                var currentSelection = Selection.objects;
                var activeGameObject = Selection.activeGameObject;
                var currentInstanceID = activeGameObject != null ? activeGameObject.GetInstanceID() : -1;
                var currentSelectionCount = currentSelection.Length;
                
                // Check if selection state actually changed
                if (currentInstanceID != lastSelectedInstanceID || currentSelectionCount != lastSelectionCount)
                {
                    // Determine operation type, but don't update state
                    if (activeGameObject != null)
                    {
                        // Has selected object
                        if (currentSelectionCount == 1)
                        {
                            var operationName = $"Select {activeGameObject.name} ({activeGameObject.GetInstanceID()})";
                            return operationName;
                        }
                        else if (currentSelectionCount > 1)
                        {
                            var operationName = $"Select Multiple ({currentSelectionCount} objects)";
                            return operationName;
                        }
                    }
                    else if (currentSelectionCount == 0)
                    {
                        // No selected object, clear selection
                        var operationName = "Clear Selection";
                        return operationName;
                    }
                }
                
                return "";
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityUndoMonitor] Error in InferSelectionOperationType: {e.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Update selection state tracking
        /// </summary>
        private static void UpdateSelectionState()
        {
            var activeGameObject = Selection.activeGameObject;
            lastSelectedInstanceID = activeGameObject != null ? activeGameObject.GetInstanceID() : -1;
            lastSelectionCount = Selection.objects.Length;
            lastSceneObjectCount = GetSceneObjectCount(); // Also update scene object count
        }
        
        /// <summary>
        /// Get object count in current active scene
        /// </summary>
        private static int GetSceneObjectCount()
        {
            try
            {
                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    var rootObjects = activeScene.GetRootGameObjects();
                    int totalCount = 0;
                    
                    // Recursively count all objects (including child objects)
                    foreach (var rootObj in rootObjects)
                    {
                        totalCount += CountObjectsRecursive(rootObj);
                    }
                    
                    return totalCount;
                }
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityUndoMonitor] Error getting scene object count: {e.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Recursively count GameObject and all its child objects
        /// </summary>
        private static int CountObjectsRecursive(GameObject obj)
        {
            int count = 1; // Count current object
            
            // Recursively count all child objects
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                count += CountObjectsRecursive(obj.transform.GetChild(i).gameObject);
            }
            
            return count;
        }
        
        /// <summary>
        /// Clean up listener (when editor closes)
        /// </summary>
        private static void Cleanup()
        {
            if (isInitialized)
            {
                EditorApplication.update -= MonitorUndoStack;
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                Selection.selectionChanged -= OnSelectionChanged;
                
                isInitialized = false;
            }
        }
        
        /// <summary>
        /// Try to get undo group name (keep original method as backup)
        /// </summary>
        private static string TryGetUndoGroupName(int groupId)
        {
            try
            {
                // Unity's Undo.GetCurrentGroupName() can only get current group's name
                if (groupId == Undo.GetCurrentGroup())
                {
                    return Undo.GetCurrentGroupName();
                }
                else
                {
                    // For historical groups, use smart inference
                    return InferOperationType(groupId);
                }
            }
            catch
            {
                return $"Unknown Operation {groupId}";
            }
        }
        

        

        
        /// <summary>
        /// Get all operation history
        /// </summary>
        public static List<UndoOperation> GetAllOperations()
        {
            return allOperations.ToList();
        }
        
        /// <summary>
        /// Get latest N operations
        /// </summary>
        public static List<UndoOperation> GetRecentOperations(int count = 10)
        {
            return allOperations.TakeLast(count).ToList();
        }
        
        /// <summary>
        /// Get MCP operation count
        /// </summary>
        public static int GetMcpOperationCount()
        {
            return allOperations.Count(op => op.isMcpOperation);
        }
        
        /// <summary>
        /// Get manual operation count
        /// </summary>
        public static int GetManualOperationCount()
        {
            return allOperations.Count(op => !op.isMcpOperation);
        }
        
        /// <summary>
        /// Perform undo operation (using Unity native)
        /// </summary>
        public static void PerformUndo()
        {
            try
            {
                isPerformingUndoRedo = true;
                isCustomUndoRedo = true; // Mark as custom operation
                
                // First check if there are operations to undo
                if (allOperations.Count == 0)
                {
                    isPerformingUndoRedo = false;
                    isCustomUndoRedo = false;
                    return;
                }
                
                // Move operation from undo stack to redo stack (before Unity execution)
                var operationToUndo = allOperations[allOperations.Count - 1];
                allOperations.RemoveAt(allOperations.Count - 1);
                redoOperations.Add(operationToUndo);
                
                // Remove operation GUID from processed set to allow re-recording if operation is performed again
                if (!string.IsNullOrEmpty(operationToUndo.operationGuid))
                {
                    processedOperationGuids.Remove(operationToUndo.operationGuid);
                }
                
                // Record undo operation time for ignoring subsequent internal group changes
                lastUndoRedoTime = DateTime.Now;
                
                // Execute Unity's undo
                Undo.PerformUndo();
                
                // Trigger UI update
                OnOperationsChanged?.Invoke();
                
                // Delayed reset flags to ensure all related events are processed
                DelayedResetAfterUndoRedo();
            }
            catch (Exception e)
            {
                isPerformingUndoRedo = false;
                isCustomUndoRedo = false;
                Debug.LogError($"[UnityUndoMonitor] Error performing undo: {e.Message}");
            }
        }
        
        /// <summary>
        /// Perform redo operation (using Unity native)
        /// </summary>
        public static void PerformRedo()
        {
            try
            {
                isPerformingUndoRedo = true;
                isCustomUndoRedo = true; // Mark as custom operation
                
                // First check if there are operations to redo
                if (redoOperations.Count == 0)
                {
                    isPerformingUndoRedo = false;
                    isCustomUndoRedo = false;
                    return;
                }
                
                // Move operation from redo stack back to undo stack (before Unity execution)
                var operationToRedo = redoOperations[redoOperations.Count - 1];
                redoOperations.RemoveAt(redoOperations.Count - 1);
                allOperations.Add(operationToRedo);
                
                // Re-add operation GUID to processed set since operation is being redone
                if (!string.IsNullOrEmpty(operationToRedo.operationGuid))
                {
                    processedOperationGuids.Add(operationToRedo.operationGuid);
                }
                
                // Record redo operation time for ignoring subsequent internal group changes
                lastUndoRedoTime = DateTime.Now;
                
                // Execute Unity's redo
                Undo.PerformRedo();
                
                // Trigger UI update
                OnOperationsChanged?.Invoke();
                
                // Delayed reset flags to ensure all related events are processed
                DelayedResetAfterUndoRedo();
            }
            catch (Exception e)
            {
                isPerformingUndoRedo = false;
                isCustomUndoRedo = false;
                Debug.LogError($"[UnityUndoMonitor] Error performing redo: {e.Message}");
            }
        }
        
        /// <summary>
        /// Delayed reset state after undo/redo operations - Public method to avoid code duplication
        /// </summary>
        private static void DelayedResetAfterUndoRedo()
        {
            EditorApplication.delayCall += () =>
            {
                // Re-sync selection state tracking
                UpdateSelectionState();
                
                // Thoroughly sync undo group state to prevent detecting internal group changes from operations
                lastTrackedGroup = Undo.GetCurrentGroup();
                lastUnityUndoCount = GetUnityUndoStackCount();
                
                // Add additional delay to ensure Unity completes all related internal operations before resetting flags
                EditorApplication.delayCall += () =>
                {
                    // Final sync to ensure all Unity internal states are stable
                    lastTrackedGroup = Undo.GetCurrentGroup();
                    lastUnityUndoCount = GetUnityUndoStackCount();
                    isPerformingUndoRedo = false;
                    // isCustomUndoRedo is reset in OnUndoRedoPerformed
                };
            };
        }
        
        /// <summary>
        /// Clear listening history
        /// </summary>
        public static void ClearHistory()
        {
            allOperations.Clear();
            redoOperations.Clear();
            processedOperationGuids.Clear();
            lastManualOperationName = null; // Reset manual operation duplicate detection
            lastManualOperationTargetID = -1;
            lastTrackedGroup = Undo.GetCurrentGroup() - 1;
            lastUnityUndoCount = GetUnityUndoStackCount();
            OnOperationsChanged?.Invoke();
        }
        
        /// <summary>
        /// Set UI refresh state - Disable undo monitoring during UI refresh
        /// </summary>
        public static void SetUIRefreshState(bool isRefreshing)
        {
            isRefreshingUI = isRefreshing;
        }
        
        /// <summary>
        /// Get current undo stack status information
        /// </summary>
        public static string GetStatusInfo()
        {
            return $"Unity Undo Group: {Undo.GetCurrentGroup()}, " +
                   $"Tracked Operations: {allOperations.Count}, " +
                   $"MCP: {GetMcpOperationCount()}, " +
                   $"Manual: {GetManualOperationCount()}";
        }
        
        /// <summary>
        /// Get undo operation count (compatible with MainWindowEditor)
        /// </summary>
        public static int GetUndoCount()
        {
            return allOperations.Count;
        }
        
        /// <summary>
        /// Get redo operation count
        /// </summary>
        public static int GetRedoCount()
        {
            return redoOperations.Count;
        }
        
        /// <summary>
        /// Get undo history (compatible with MainWindowEditor)
        /// </summary>
        public static List<UndoOperation> GetUndoHistory()
        {
            return allOperations.ToList();
        }
        
        /// <summary>
        /// Get redo history (compatible with MainWindowEditor)
        /// </summary>
        public static List<UndoOperation> GetRedoHistory()
        {
            // Return copy of redo stack, note that redo stack is reversed (newest at the end)
            var redoHistory = redoOperations.ToList();
            redoHistory.Reverse(); // Reverse to put newest redo operations at the front
            return redoHistory;
        }
        
        /// <summary>
        /// Force check for new operations before critical operations (undo/redo/clear)
        /// This ensures we don't miss any operations due to polling delay
        /// </summary>
        public static void ForceCheckNewOperations()
        {
            try
            {
                // Skip if already performing undo/redo or refreshing UI
                if (isPerformingUndoRedo || isRefreshingUI)
                {
                    return;
                }
                
                var currentGroup = Undo.GetCurrentGroup();
                var currentUnityUndoCount = GetUnityUndoStackCount();
                
                // Check if there are new operations to process
                if (currentGroup > lastTrackedGroup)
                {
                    // Get current group name
                    var currentGroupName = "";
                    try
                    {
                        currentGroupName = Undo.GetCurrentGroupName();
                    }
                    catch { }
                    
                    // Early duplicate detection for both MCP and manual operations
                    bool shouldProcess = true;
                    if (!string.IsNullOrEmpty(currentGroupName))
                    {
                        if (currentGroupName.StartsWith("[MCP]"))
                        {
                            // MCP operation: GUID-based duplicate detection
                            var operationGuid = ExtractGuidFromGroupName(currentGroupName);
                            
                            // Reset manual operation tracking when MCP operation occurs
                            lastManualOperationName = null;
                            lastManualOperationTargetID = -1;
                            
                            if (!string.IsNullOrEmpty(operationGuid) && processedOperationGuids.Contains(operationGuid))
                            {
                                // Operation already processed, skip entirely
                                shouldProcess = false;
                            }
                            else if (!string.IsNullOrEmpty(operationGuid))
                            {
                                // Mark this GUID as processed
                                processedOperationGuids.Add(operationGuid);
                            }
                        }
                        else
                        {
                            // Manual operation: consecutive duplicate detection
                            if (IsManualOperationDuplicate(currentGroupName))
                            {
                                shouldProcess = false;
                            }
                        }
                    }
                    
                    // Process new operation only if not filtered out
                    if (shouldProcess)
                    {
                        ProcessNewOperation(currentGroup, currentGroupName);
                    }
                    lastTrackedGroup = currentGroup;
                    
                    // Update Unity undo count tracking
                    lastUnityUndoCount = GetUnityUndoStackCount();
                    
                    // Trigger UI update if operations were added
                    OnOperationsChanged?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityUndoMonitor] Error in ForceCheckNewOperations: {e.Message}");
            }
        }
        
        /// <summary>
        /// Check if manual operation is a consecutive duplicate
        /// </summary>
        private static bool IsManualOperationDuplicate(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return false;
            
            // Get current target GameObject (usually the selected one)
            int currentTargetID = Selection.activeInstanceID;
            var targetObject = EditorUtility.InstanceIDToObject(currentTargetID);
            var sceneIsDirty = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty;
            
            // Check if this operation is the same as the last manual operation
            // Consider it duplicate only if both operation name AND target GameObject are the same
            if (!string.IsNullOrEmpty(lastManualOperationName) && 
                operationName.Equals(lastManualOperationName, StringComparison.Ordinal) &&
                currentTargetID == lastManualOperationTargetID &&
                currentTargetID != 0 && 
                !sceneIsDirty) // Valid InstanceID
            {
                return true; // Consecutive duplicate detected (same operation, same target, object not dirty)
            }
            
            // Update last manual operation info
            lastManualOperationName = operationName;
            lastManualOperationTargetID = currentTargetID;
            // save scene clean dirty
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            return false; // Not a duplicate
        }
        

        
        /// <summary>
        /// Check if operation name contains object-specific information
        /// </summary>
        private static bool ContainsObjectSpecificInfo(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return false;
                
            var lowerName = operationName.ToLower();
            
            // Check for GameObject names or specific identifiers
            // Operations with object names like "Modify GameObject: North" should be considered unique
            // New format: "Modify North: position → (0.2, 0, 5.2)" should also be considered unique
            if (lowerName.Contains(": ") || lowerName.Contains(" - ") || 
                lowerName.Contains("→") || lowerName.Contains("->") || // Modification indicators
                lowerName.Contains("north") || lowerName.Contains("south") || 
                lowerName.Contains("east") || lowerName.Contains("west") ||
                lowerName.Contains("sphere") || lowerName.Contains("cube") ||
                lowerName.Contains("gameobject:") || lowerName.Contains("object:") ||
                lowerName.Contains("position") || lowerName.Contains("rotation") || lowerName.Contains("scale") || // Common properties
                lowerName.Contains("name") || lowerName.Contains("tag") || lowerName.Contains("layer") || // GameObject properties
                System.Text.RegularExpressions.Regex.IsMatch(lowerName, @"\b\w+\s*\(\d+\)") || // Pattern like "Object (123)"
                System.Text.RegularExpressions.Regex.IsMatch(operationName, @"\b[A-Z][a-z]+\b")) // Contains capitalized words (likely object names)
            {
                return true;
            }
            
            return false;
        }
    
        /// <summary>
        /// Check if operation name already contains detailed parameters
        /// </summary>
        private static bool AlreadyContainsDetailedParameters(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return false;
            
            // Check for common patterns that indicate detailed parameters
            return operationName.Contains("to (") ||     // "Set Position to (1.0, 2.0, 3.0)"
                   operationName.Contains(" in ") ||     // "Set Position in GameObject"
                   operationName.Contains("Pos:(") ||    // "Set Transform - Pos:(1,2,3)"
                   operationName.Contains("X:") ||       // "Set Rotation X:90"
                   operationName.Contains("Y:") ||       // "Set Rotation Y:90"  
                   operationName.Contains("Z:");         // "Set Rotation Z:90"
        }

    }
} 