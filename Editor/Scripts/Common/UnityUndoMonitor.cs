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
            
            public string DisplayName => isMcpOperation 
                ? $"[MCP] {operationName}" 
                : $"[Manual] {operationName}";
                
            /// <summary>
            /// 解析操作名称为 [操作类型] [来源] 对象描述 的格式
            /// </summary>
            public string ParsedDisplayName
            {
                get
                {
                    var (operationType, objectDescription) = ParseOperationName(operationName);
                    var sourceType = isMcpOperation ? "MCP" : "Manual";
                    
                    if (string.IsNullOrEmpty(objectDescription))
                    {
                        return $"[{operationType}] [{sourceType}]";
                    }
                    else
                    {
                        return $"[{operationType}] [{sourceType}] {objectDescription}";
                    }
                }
            }
            
            /// <summary>
            /// 解析操作名称，提取操作类型和对象描述
            /// </summary>
            public static (string operationType, string objectDescription) ParseOperationName(string operationName)
            {
                if (string.IsNullOrEmpty(operationName))
                    return ("Unknown", "");
                
                var trimmedName = operationName.Trim();
                
                // 处理MCP操作
                if (trimmedName.StartsWith("[MCP]"))
                {
                    trimmedName = trimmedName.Substring(5).Trim();
                }
                
                // 解析各种操作类型
                
                // Rename操作: "Rename hhh" -> ("Rename", "hhh")
                if (trimmedName.StartsWith("Rename ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(7).Trim();
                    return ("Rename", objectName);
                }
                
                // Select操作: "Select hhh (GameObject)" -> ("Select", "hhh (GameObject)")
                // 或 "Select hhh (123) (GameObject)" -> ("Select", "hhh (123) (GameObject)")
                if (trimmedName.StartsWith("Select ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectDescription = trimmedName.Substring(7).Trim();
                    return ("Select", objectDescription);
                }
                
                // Create操作: "Create hhh" -> ("Create", "hhh")
                if (trimmedName.StartsWith("Create ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(7).Trim();
                    return ("Create", objectName);
                }
                
                // Delete操作: "Delete hhh" -> ("Delete", "hhh")
                if (trimmedName.StartsWith("Delete ", StringComparison.OrdinalIgnoreCase) || 
                    trimmedName.StartsWith("Destroy ", StringComparison.OrdinalIgnoreCase))
                {
                    var startIndex = trimmedName.StartsWith("Delete ", StringComparison.OrdinalIgnoreCase) ? 7 : 8;
                    var objectName = trimmedName.Substring(startIndex).Trim();
                    var operationType = trimmedName.StartsWith("Delete ", StringComparison.OrdinalIgnoreCase) ? "Delete" : "Destroy";
                    return (operationType, objectName);
                }
                
                // Drag and Drop操作: "Drag and Drop hhh" -> ("Drag", "hhh")
                if (trimmedName.StartsWith("Drag and Drop ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(14).Trim();
                    return ("Drag", objectName);
                }
                
                // Duplicate操作: "Duplicate hhh" -> ("Duplicate", "hhh")
                if (trimmedName.StartsWith("Duplicate ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(10).Trim();
                    return ("Duplicate", objectName);
                }
                
                // Copy操作: "Copy hhh" -> ("Copy", "hhh")
                if (trimmedName.StartsWith("Copy ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(5).Trim();
                    return ("Copy", objectName);
                }
                
                // Move操作: "Move hhh" -> ("Move", "hhh")
                if (trimmedName.StartsWith("Move ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(5).Trim();
                    return ("Move", objectName);
                }
                
                // Transform操作: "Transform hhh" -> ("Transform", "hhh")
                if (trimmedName.StartsWith("Transform ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(10).Trim();
                    return ("Transform", objectName);
                }
                
                // Modify操作: "Modify hhh" -> ("Modify", "hhh")
                if (trimmedName.StartsWith("Modify ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(7).Trim();
                    return ("Modify", objectName);
                }
                
                // Add Component操作: "Add Component hhh" -> ("Add Component", "hhh")
                if (trimmedName.StartsWith("Add Component ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(14).Trim();
                    return ("Add Component", objectName);
                }
                
                // Remove Component操作: "Remove Component hhh" -> ("Remove Component", "hhh")
                if (trimmedName.StartsWith("Remove Component ", StringComparison.OrdinalIgnoreCase))
                {
                    var objectName = trimmedName.Substring(17).Trim();
                    return ("Remove Component", objectName);
                }
                
                // Clear Selection操作: "Clear Selection" -> ("Clear Selection", "")
                if (trimmedName.Equals("Clear Selection", StringComparison.OrdinalIgnoreCase))
                {
                    return ("Clear Selection", "");
                }
                
                // 其他特殊操作模式
                
                // 检查是否包含常见的操作动词
                var lowerName = trimmedName.ToLower();
                var operationVerbs = new[]
                {
                    "create", "delete", "destroy", "duplicate", "copy", "paste", "clone",
                    "move", "rotate", "scale", "rename", "transform", "modify", "edit",
                    "drag", "drop", "instantiate", "spawn", "add", "place", "insert",
                    "remove", "change", "set", "update", "build", "generate"
                };
                
                foreach (var verb in operationVerbs)
                {
                    if (lowerName.Contains(verb))
                    {
                        // 找到操作动词，尝试提取操作类型
                        var verbIndex = lowerName.IndexOf(verb);
                        var operationType = char.ToUpper(verb[0]) + verb.Substring(1);
                        
                        // 提取对象描述（动词后面的部分）
                        var afterVerb = trimmedName.Substring(verbIndex + verb.Length).Trim();
                        if (afterVerb.StartsWith(" "))
                        {
                            afterVerb = afterVerb.Substring(1).Trim();
                        }
                        
                        return (operationType, afterVerb);
                    }
                }
                
                // 如果没有找到明确的操作模式，使用整个名称作为操作类型
                if (trimmedName.Length > 20)
                {
                    // 对于很长的名称，尝试提取前面的部分作为操作类型
                    var firstWords = trimmedName.Split(' ').Take(2).ToArray();
                    if (firstWords.Length >= 2)
                    {
                        var operationType = string.Join(" ", firstWords);
                        var objectDescription = trimmedName.Substring(operationType.Length).Trim();
                        return (operationType, objectDescription);
                    }
                }
                
                // 默认情况：使用整个名称作为操作类型
                return (trimmedName, "");
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
                
                // Detect new operations - Process immediately, rely on duplicate detection logic
                if (currentGroup > lastTrackedGroup)
                {
                    // Get current group name
                    var currentGroupName = "";
                    try
                    {
                        currentGroupName = Undo.GetCurrentGroupName();
                    }
                    catch { }
                    
                    // Process new operation immediately
                    ProcessNewOperation(currentGroup, currentGroupName);
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
                var extractedName = ExtractOperationName(groupName);
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
                    bool isDuplicate = IsDuplicateOperation(groupName);
                    
                    // For MCP operations, perform additional immediate duplicate check
                    if (!isDuplicate && groupName.StartsWith("[MCP]"))
                    {
                        // Check if identical to last few operations (not time-dependent)
                        var lastFewOps = allOperations.TakeLast(3).ToList();
                        var identicalCount = lastFewOps.Count(op => op.operationName == extractedName && op.isMcpOperation);
                        if (identicalCount >= 1) // If there's already a recent identical MCP operation
                        {
                            isDuplicate = true;
                        }
                    }
                    
                    // For delete and copy operations, check if temporary selection operations should be removed
                    if (!isDuplicate && (IsDeleteOperation(extractedName) || IsCopyOperation(extractedName)))
                    {
                        CheckAndRemoveTemporarySelection();
                    }
                    
                    if (!isDuplicate)
                    {
                        AddOperation(currentGroup, extractedName, groupName.StartsWith("[MCP]"));
                    }
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
                        AddOperation(currentGroup, ExtractOperationName(inferredName), false);
                    }
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
        /// Unified method for adding new operations
        /// </summary>
        private static void AddOperation(int groupId, string operationName, bool isMcpOperation)
        {
            var operation = new UndoOperation
            {
                groupId = groupId,
                operationName = operationName,
                isMcpOperation = isMcpOperation,
                timestamp = DateTime.Now
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
            
            var extractedName = ExtractOperationName(operationName);
            
            // For MCP operations, sequence-based duplicate detection
            if (operationName.StartsWith("[MCP]"))
            {
                // Check if there are identical MCP operations in the last 5 operations
                var recentOperations = allOperations.TakeLast(5).ToList();
                var identicalMcpOps = recentOperations.Where(op => op.operationName == extractedName && op.isMcpOperation).ToList();
                
                if (identicalMcpOps.Count > 0)
                {
                    return true;
                }
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
                // For other operations, check last 2 operations
                var recentOperations = allOperations.TakeLast(2).ToList();
                var identicalOps = recentOperations.Where(op => op.operationName == extractedName && !op.isMcpOperation).ToList();
                
                if (identicalOps.Count > 0)
                {
                    // For selection operations, only check the last operation
                    if (extractedName.StartsWith("Select ") || extractedName == "Clear Selection")
                    {
                        if (allOperations.Count > 0 && allOperations.Last().operationName == extractedName)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            
            return false;
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
                "new", "gameobject", "prefab", "asset", "import", "build", "generate"
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
                "unknown action", "internal", "system", "temp", "debug"
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
                !lowerName.Contains("unknown") &&
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
        /// Standardize operation name - Map Unity's internal operation names to more user-friendly names
        /// </summary>
        private static string StandardizeOperationName(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return operationName;
            
            var lowerName = operationName.ToLower();
            
            // Standardize Paste operations to Duplicate (exclude clipboard operations)
            if ((lowerName.StartsWith("paste ") || lowerName.Contains(" paste ") || lowerName.EndsWith(" paste")) 
                && !lowerName.Contains("clipboard"))
            {
                var standardizedName = operationName
                    .Replace("Paste ", "Duplicate ").Replace("paste ", "Duplicate ")
                    .Replace(" Paste ", " Duplicate ").Replace(" paste ", " Duplicate ")
                    .Replace(" Paste", " Duplicate").Replace(" paste", " Duplicate");
                return standardizedName;
            }
            
            // "Copy Game Objects" -> "Duplicate Game Objects"
            if (lowerName.Contains("copy ") && lowerName.Contains("game object"))
            {
                var standardizedName = operationName.Replace("Copy ", "Duplicate ").Replace("copy ", "Duplicate ");
                return standardizedName;
            }
            
            return operationName;
        }
        
        /// <summary>
        /// Extract operation name from group name
        /// </summary>
        private static string ExtractOperationName(string groupName)
        {
            string operationName;
            if (groupName.StartsWith("[MCP]"))
            {
                operationName = groupName.Substring(5).Trim();
            }
            else
            {
                operationName = groupName;
            }
            
            // Standardize operation name
            return StandardizeOperationName(operationName);
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
                    
                    // Process new operation immediately
                    ProcessNewOperation(currentGroup, currentGroupName);
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
    }
} 