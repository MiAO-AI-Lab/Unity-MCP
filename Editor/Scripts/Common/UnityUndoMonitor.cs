using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Unity.MCP
{
    /// <summary>
    /// Unity撤销栈监听器 - 实时监控所有撤销操作
    /// </summary>
    public static class UnityUndoMonitor
    {
        /// <summary>
        /// 撤销操作数据结构
        /// </summary>
        public struct UndoOperation
        {
            public int groupId;
            public string operationName;
            public bool isMcpOperation;
            public DateTime timestamp;
            
            public string DisplayName => isMcpOperation 
                ? $"⭐ [MCP] {operationName}" 
                : $"🖱️ [Manual] {operationName}";
        }
        
        private static int lastTrackedGroup = -1;
        private static readonly List<UndoOperation> allOperations = new List<UndoOperation>();
        private static readonly List<UndoOperation> redoOperations = new List<UndoOperation>(); // redo栈
        private static bool isInitialized = false;
        private static float lastCheckTime = 0f;
        private static bool isPerformingUndoRedo = false; // 防止撤销/重做时的递归
        private static bool isCustomUndoRedo = false; // 标记是否是自定义的undo/redo操作
        
        // 跟踪选择状态变化
        private static int lastSelectedInstanceID = -1;
        private static int lastSelectionCount = 0;
        
        // 用于检测实际的undo/redo操作的计数器
        private static int lastUnityUndoCount = 0;
        
        // 跟踪删除操作后的自动选择
        private static DateTime lastDeleteOperationTime = DateTime.MinValue;
        private static readonly TimeSpan AUTO_SELECTION_THRESHOLD = TimeSpan.FromMilliseconds(500); // 500ms内的选择操作认为是自动的
        
        // Undo/Redo操作后的忽略窗口
        private static DateTime lastUndoRedoTime = DateTime.MinValue;
        private static readonly TimeSpan UNDO_REDO_IGNORE_THRESHOLD = TimeSpan.FromSeconds(3); // 3秒内忽略新组检测
        
        // 连续操作处理
        private static DateTime lastGroupProcessTime = DateTime.MinValue;
        private static int lastProcessedGroup = -1;
        private static readonly TimeSpan GROUP_BATCH_DELAY = TimeSpan.FromMilliseconds(100); // 100ms延迟来批处理连续组
        
        /// <summary>
        /// 所有操作发生变化时的事件
        /// </summary>
        public static event System.Action OnOperationsChanged;
        
        /// <summary>
        /// 初始化监听器 - 只监听可撤销操作
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!isInitialized)
            {
                // 初始化选择状态跟踪
                UpdateSelectionState();
                
                // 添加主要的更新监听器
                EditorApplication.update += MonitorUndoStack;
                
                // 监听撤销/重做操作
                Undo.undoRedoPerformed += OnUndoRedoPerformed;
                
                // 监听选择变化，因为选择操作也是可撤销的
                Selection.selectionChanged += OnSelectionChanged;
                
                isInitialized = true;
                
                // 初始化时获取当前撤销组和Unity的undo计数
                lastTrackedGroup = Undo.GetCurrentGroup() - 1;
                lastUnityUndoCount = GetUnityUndoStackCount();
                Debug.Log($"[UnityUndoMonitor] Initialized - Group: {Undo.GetCurrentGroup()}, Unity Count: {lastUnityUndoCount}");
            }
        }
        
        /// <summary>
        /// 同步内部操作栈与Unity的undo/redo状态
        /// </summary>
        private static void SynchronizeStacksWithUnity()
        {
            try
            {
                var currentUnityUndoCount = GetUnityUndoStackCount();
                var undoCountDiff = currentUnityUndoCount - lastUnityUndoCount;
                
                Debug.Log($"[UnityUndoMonitor] Synchronizing stacks - Unity count: {currentUnityUndoCount}, was: {lastUnityUndoCount}, diff: {undoCountDiff}");
                Debug.Log($"[UnityUndoMonitor] Current stacks - Undo: {allOperations.Count}, Redo: {redoOperations.Count}");
                
                if (undoCountDiff > 0)
                {
                    // Unity undo count increased = undo operation was performed
                    // Move operation from undo to redo stack
                    if (allOperations.Count > 0)
                    {
                        var operationToMove = allOperations[allOperations.Count - 1];
                        allOperations.RemoveAt(allOperations.Count - 1);
                        redoOperations.Add(operationToMove);
                        Debug.Log($"[UnityUndoMonitor] ↶ Moved to redo: {operationToMove.DisplayName}");
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
                        Debug.Log($"[UnityUndoMonitor] ↷ Moved to undo: {operationToMove.DisplayName}");
                    }
                }
                
                Debug.Log($"[UnityUndoMonitor] After sync - Undo: {allOperations.Count}, Redo: {redoOperations.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityUndoMonitor] Error synchronizing stacks: {e.Message}");
            }
        }
        
        /// <summary>
        /// 当撤销/重做操作执行时触发
        /// </summary>
        private static void OnUndoRedoPerformed()
        {
            isPerformingUndoRedo = true;
            Debug.Log($"[UnityUndoMonitor] System Undo/Redo performed (isCustom: {isCustomUndoRedo})");
            
            // 延迟检查撤销栈状态，并重置标志
            EditorApplication.delayCall += () =>
            {
                // 只有在非自定义操作时才进行同步（Unity原生的undo/redo）
                if (!isCustomUndoRedo)
                {
                    // 同步内部操作栈与Unity的undo/redo状态
                    SynchronizeStacksWithUnity();
                }
                else
                {
                    Debug.Log("[UnityUndoMonitor] Skipping synchronization for custom undo/redo");
                }
                
                // 更新选择状态
                UpdateSelectionState();
                
                // 同步撤销组状态，防止检测到系统undo/redo操作产生的内部组变化
                lastTrackedGroup = Undo.GetCurrentGroup();
                lastUnityUndoCount = GetUnityUndoStackCount();
                lastUndoRedoTime = DateTime.Now; // 设置忽略时间窗口
                
                // 增加额外的延迟，确保Unity完成所有相关的内部操作后再重置标志
                EditorApplication.delayCall += () =>
                {
                    // 最终同步
                    lastTrackedGroup = Undo.GetCurrentGroup();
                    lastUnityUndoCount = GetUnityUndoStackCount();
                    isPerformingUndoRedo = false;
                    isCustomUndoRedo = false; // 重置自定义标志
                    
                    // 触发UI更新
                    OnOperationsChanged?.Invoke();
                    Debug.Log("[UnityUndoMonitor] Stack synchronization complete");
                };
            };
        }
        
        /// <summary>
        /// 当选择发生变化时触发
        /// </summary>
        private static void OnSelectionChanged()
        {
            // 如果正在执行撤销/重做操作，忽略选择变化以避免递归
            if (isPerformingUndoRedo)
            {
                return;
            }
            
            // 延迟一点检查，确保可能的撤销组已经创建
            EditorApplication.delayCall += () =>
            {
                if (!isPerformingUndoRedo) // 再次检查，确保延迟期间没有开始撤销操作
                {
                    MonitorUndoStack();
                }
            };
        }
        
        /// <summary>
        /// 实时监控撤销栈变化 - 只监听可撤销的操作
        /// </summary>
        private static void MonitorUndoStack()
        {
            try
            {
                // 限制检查频率，避免过度监控
                var currentTime = (float)EditorApplication.timeSinceStartup;
                if (currentTime - lastCheckTime < 0.1f) // 每100ms最多检查一次
                {
                    return;
                }
                lastCheckTime = currentTime;
                
                var currentGroup = Undo.GetCurrentGroup();
                var currentUnityUndoCount = GetUnityUndoStackCount();
                
                // 如果正在执行撤销/重做操作，跳过检测逻辑（栈管理在PerformUndo/PerformRedo中处理）
                if (isPerformingUndoRedo)
                {
                    lastTrackedGroup = currentGroup;
                    lastUnityUndoCount = currentUnityUndoCount;
                    return; // 在undo/redo期间不执行添加新操作的逻辑
                }
                
                // 检测新的操作 - 立即处理，依赖重复检测逻辑
                if (currentGroup > lastTrackedGroup)
                {
                    // 获取当前组名称
                    var currentGroupName = "";
                    try
                    {
                        currentGroupName = Undo.GetCurrentGroupName();
                    }
                    catch { }
                    
                    // 立即处理新操作
                    ProcessNewOperation(currentGroup, currentGroupName);
                    lastTrackedGroup = currentGroup;
                    
                    // 在添加新操作时，同步更新Unity undo计数跟踪
                    currentUnityUndoCount = GetUnityUndoStackCount();
                    lastUnityUndoCount = currentUnityUndoCount;
                }
                
                // 正常情况下的undo/redo检测已移至PerformUndo/PerformRedo方法中直接处理
                // 这里只需要更新计数跟踪
                lastUnityUndoCount = currentUnityUndoCount;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityUndoMonitor] Monitoring error: {e.Message}");
            }
        }
        
        /// <summary>
        /// 处理新操作的统一方法
        /// </summary>
        private static void ProcessNewOperation(int currentGroup, string currentGroupName)
        {
            // 检查是否在undo/redo操作后的忽略窗口内
            if (DateTime.Now - lastUndoRedoTime < UNDO_REDO_IGNORE_THRESHOLD)
            {
                Debug.Log($"[UnityUndoMonitor] Ignoring operation within undo/redo window: {currentGroupName}");
                return;
            }
            
            string groupName = currentGroupName;
            
            // 处理有明确组名称的操作
            if (!string.IsNullOrEmpty(groupName) && IsValidUndoableOperation(groupName))
            {
                var extractedName = ExtractOperationName(groupName);
                bool shouldRecord = true;
                
                // 对于MCP操作，添加额外的日志
                if (groupName.StartsWith("[MCP]"))
                {
                    var currentTime = DateTime.Now;
                    var lastMcpOp = allOperations.LastOrDefault(op => op.isMcpOperation);
                    var timeSinceLastMcp = lastMcpOp.timestamp != default ? (currentTime - lastMcpOp.timestamp).TotalMilliseconds : -1;
                    Debug.Log($"[UnityUndoMonitor] Processing MCP operation: '{extractedName}' (group: {currentGroup}, time since last MCP: {timeSinceLastMcp:F0}ms)");
                }
                
                // 检查是否是删除操作
                if (IsDeleteOperation(extractedName))
                {
                    lastDeleteOperationTime = DateTime.Now;
                }
                
                // 对于选择操作，必须先检查选择状态是否真正变化
                if (extractedName.StartsWith("Select ") || extractedName == "Clear Selection")
                {
                    var selectionChangeResult = InferSelectionOperationType(currentGroup);
                    if (string.IsNullOrEmpty(selectionChangeResult))
                    {
                        shouldRecord = false;
                    }
                    else
                    {
                        // 检查是否是删除操作后的自动选择
                        if (IsAutoSelectionAfterDelete())
                        {
                            shouldRecord = false;
                            Debug.Log($"[UnityUndoMonitor] Skipped auto-selection after delete: {extractedName}");
                        }
                    }
                }
                
                if (shouldRecord)
                {
                    bool isDuplicate = IsDuplicateOperation(groupName);
                    
                    // 对于MCP操作，进行额外的即时重复检查
                    if (!isDuplicate && groupName.StartsWith("[MCP]"))
                    {
                        // 检查是否与最后几个操作完全相同（不依赖时间）
                        var lastFewOps = allOperations.TakeLast(3).ToList();
                        var identicalCount = lastFewOps.Count(op => op.operationName == extractedName && op.isMcpOperation);
                        if (identicalCount >= 1) // 如果最近已经有相同的MCP操作
                        {
                            isDuplicate = true;
                            Debug.Log($"[UnityUndoMonitor] Detected immediate consecutive MCP duplicate: {extractedName} (found {identicalCount} recent identical ops)");
                        }
                    }
                    
                    if (!isDuplicate)
                    {
                        AddOperation(currentGroup, extractedName, groupName.StartsWith("[MCP]"));
                    }
                    else if (groupName.StartsWith("[MCP]"))
                    {
                        Debug.Log($"[UnityUndoMonitor] Skipped duplicate MCP operation: {extractedName}");
                    }
                }
            }
            else if (string.IsNullOrEmpty(groupName))
            {
                // 对于没有明确组名称的操作，进行有限的推测
                // 主要处理选择操作等重要的可撤销操作
                var inferredName = InferSelectionOperationType(currentGroup);
                if (!string.IsNullOrEmpty(inferredName) && IsValidUndoableOperation(inferredName))
                {
                    // 检查是否是删除操作后的自动选择
                    if (IsAutoSelectionAfterDelete())
                    {
                        Debug.Log($"[UnityUndoMonitor] Skipped auto-selection after delete: {inferredName}");
                        return;
                    }
                    
                    // 检查是否与最近的操作重复
                    if (!IsDuplicateOperation(inferredName))
                    {
                        AddOperation(currentGroup, ExtractOperationName(inferredName), false);
                    }
                }
            }
        }
        
        /// <summary>
        /// 检查是否是删除操作
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
        /// 检查是否是删除操作后的自动选择
        /// </summary>
        private static bool IsAutoSelectionAfterDelete()
        {
            var timeSinceDelete = DateTime.Now - lastDeleteOperationTime;
            return timeSinceDelete <= AUTO_SELECTION_THRESHOLD;
        }
        
        /// <summary>
        /// 添加新操作的统一方法
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
            
            // 新操作添加时，清空redo栈（标准undo/redo行为）
            // 但如果正在执行undo/redo操作，不要清空redo栈
            if (redoOperations.Count > 0 && !isPerformingUndoRedo)
            {
                redoOperations.Clear();
            }
            
            // 如果是选择操作，更新选择状态跟踪
            if (operationName.StartsWith("Select ") || operationName == "Clear Selection")
            {
                UpdateSelectionState();
            }
            
            Debug.Log($"[UnityUndoMonitor] ✓ {operation.DisplayName}");
            OnOperationsChanged?.Invoke();
        }
        
        /// <summary>
        /// 获取Unity内部的实际undo栈计数
        /// </summary>
        private static int GetUnityUndoStackCount()
        {
            try
            {
                // 使用反射来获取Unity内部的undo计数
                // Unity内部维护一个undo列表，我们尝试通过反射访问它
                var undoType = typeof(Undo);
                var getRecordsMethod = undoType.GetMethod("GetRecords", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                
                if (getRecordsMethod != null)
                {
                    var records = getRecordsMethod.Invoke(null, new object[] { });
                    if (records != null)
                    {
                        // records应该是一个数组或列表
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
                
                // 如果反射方法失败，尝试另一种方法：使用当前组ID作为估算
                // 这不是完美的，但是一个备用方案
                var currentGroup = Undo.GetCurrentGroup();
                return Mathf.Max(0, currentGroup);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityUndoMonitor] Failed to get Unity undo count: {e.Message}");
                // 备用方案：使用组ID作为近似值
                var currentGroup = Undo.GetCurrentGroup();
                return Mathf.Max(0, currentGroup);
            }
        }
        
        /// <summary>
        /// 智能推测操作类型
        /// </summary>
        private static string InferOperationType(int groupId)
        {
            try
            {
                // 基于常见的Unity操作模式进行推测
                var now = DateTime.Now;
                var lastOp = allOperations.LastOrDefault();
                var timeDiff = allOperations.Count > 0 ? now - lastOp.timestamp : TimeSpan.Zero;
                
                // 基于时间间隔的推测
                if (timeDiff.TotalMilliseconds < 50)
                {
                    return "Continuous Edit";
                }
                else if (timeDiff.TotalMilliseconds < 200)
                {
                    return "Quick Action";
                }
                
                // 基于最近操作模式的推测
                var recentOps = allOperations.TakeLast(5).Where(op => !op.isMcpOperation).ToList();
                
                // 检查是否有重复的操作模式
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
                
                // 基于当前编辑器状态的推测
                if (Selection.activeGameObject != null)
                {
                    // 有选中对象，可能是相关操作
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
                
                // 检查场景状态
                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (activeScene.isDirty)
                {
                    // 场景有变化，根据最近操作推测类型
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
                
                // 基于工具模式的推测
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
                
                // 默认分类
                return $"Editor Action";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityUndoMonitor] Error inferring operation type: {ex.Message}");
                return $"Unknown Action #{groupId}";
            }
        }
        
        /// <summary>
        /// 检查撤销组是否包含有效的可撤销内容
        /// </summary>
        private static bool HasValidUndoContent(int groupId)
        {
            try
            {
                // 检查当前撤销组是否有实际内容
                // Unity的撤销系统会为很多不可撤销的操作也创建组，但这些组通常是空的
                
                // 方法1：检查撤销计数 - 但这个检查可能过于严格
                var currentUndoCount = GetUndoCount();
                
                // 方法2：尝试获取组名
                var groupName = "";
                try
                {
                    groupName = Undo.GetCurrentGroupName();
                }
                catch { }
                
                // 如果是MCP操作，总是认为有效
                if (!string.IsNullOrEmpty(groupName) && groupName.StartsWith("[MCP]"))
                {
                    return true;
                }
                
                // 方法3：基于组名内容判断 - 只过滤明确的无效操作
                if (!string.IsNullOrEmpty(groupName))
                {
                    var lowerName = groupName.ToLower();
                    
                    // 只过滤明确无效的UI操作
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
                
                // 方法4：宽松验证 - 默认认为有效
                // 如果有组名或者有撤销计数，就认为可能是有效操作
                if (!string.IsNullOrEmpty(groupName) || currentUndoCount > 0)
                {
                    return true;
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityUndoMonitor] Error checking undo content: {e.Message}");
                return false; // 出错时认为无效
            }
        }
        
        /// <summary>
        /// 检查是否为重复操作 - 强化版本，特别处理删除、MCP等关键操作
        /// </summary>
        private static bool IsDuplicateOperation(string operationName)
        {
            if (allOperations.Count == 0)
                return false;
            
            var extractedName = ExtractOperationName(operationName);
            
            // 对于MCP操作，采用更严格的重复检测
            if (operationName.StartsWith("[MCP]"))
            {
                // 检查最近10个操作，看是否有相同的MCP操作
                var recentOperations = allOperations.TakeLast(10).ToList();
                
                foreach (var recentOp in recentOperations)
                {
                    if (recentOp.operationName == extractedName && recentOp.isMcpOperation)
                    {
                        var timeDiff = DateTime.Now - recentOp.timestamp;
                        // MCP操作在5秒内重复认为是同一操作，并且特别检测连续的相同操作
                        if (timeDiff.TotalSeconds < 5.0)
                        {
                            Debug.Log($"[UnityUndoMonitor] Detected duplicate MCP operation: {extractedName} (within {timeDiff.TotalSeconds:F1}s)");
                            return true;
                        }
                    }
                }
                
                // 额外检查：如果最后一个操作就是相同的MCP操作，直接认为是重复
                if (allOperations.Count > 0)
                {
                    var lastOp = allOperations[allOperations.Count - 1];
                    if (lastOp.operationName == extractedName && lastOp.isMcpOperation)
                    {
                        var timeDiff = DateTime.Now - lastOp.timestamp;
                        if (timeDiff.TotalMilliseconds < 100) // 100ms内的连续相同MCP操作必定是重复
                        {
                            Debug.Log($"[UnityUndoMonitor] Detected immediate duplicate MCP operation: {extractedName} (within {timeDiff.TotalMilliseconds:F0}ms)");
                            return true;
                        }
                    }
                }
            }
            // 对于删除操作，采用更严格的重复检测
            else if (IsDeleteOperation(extractedName))
            {
                // 检查最近5个操作，看是否有相同的删除操作
                var recentOperations = allOperations.TakeLast(5).ToList();
                
                foreach (var recentOp in recentOperations)
                {
                    if (recentOp.operationName == extractedName)
                    {
                        var timeDiff = DateTime.Now - recentOp.timestamp;
                        // 删除操作在2秒内重复认为是同一操作
                        if (timeDiff.TotalSeconds < 2.0)
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                // 对于其他操作，检查最近3个操作
                var recentOperations = allOperations.TakeLast(3).ToList();
                
                foreach (var recentOp in recentOperations)
                {
                    if (recentOp.operationName == extractedName)
                    {
                        var timeDiff = DateTime.Now - recentOp.timestamp;
                        
                        // 对于非选择操作
                        if (!extractedName.StartsWith("Select ") && extractedName != "Clear Selection")
                        {
                            if (timeDiff.TotalMilliseconds < 500) // 500ms内的相同非选择操作认为是重复
                            {
                                return true;
                            }
                        }
                        else
                        {
                            // 对于选择操作，使用更短的时间窗口
                            if (timeDiff.TotalMilliseconds < 100) // 100ms内的相同选择操作认为是重复
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 判断操作是否为有效的可撤销操作
        /// </summary>
        private static bool IsValidUndoableOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
            {
                return false;
            }
            
            // 明确的MCP操作总是有效的
            if (operationName.StartsWith("[MCP]"))
            {
                return true;
            }
            
            // 过滤掉不可撤销的操作类型
            var lowerName = operationName.ToLower();
            
            // **优先检查Unity原生的选择操作格式**
            if (lowerName.StartsWith("select ") && lowerName.Contains("(gameobject)"))
            {
                return true;
            }
            
            // 清除选择操作
            if (lowerName == "clear selection")
            {
                return true;
            }
            
            // 明确的无效操作 - 界面和系统操作
            if (lowerName.Contains("console") || 
                lowerName.Contains("log") ||
                lowerName.Contains("selection change") ||
                lowerName.Contains("editor action") ||
                lowerName.Contains("quick action") ||
                lowerName.Contains("continuous edit") ||
                lowerName.Contains("window") ||
                lowerName.Contains("tab") ||
                lowerName.Contains("focus") ||
                lowerName.Contains("click") ||
                lowerName.Contains("ui") ||
                lowerName.Contains("panel") ||
                lowerName.Contains("inspector change") ||
                lowerName.StartsWith("unknown action"))
            {
                return false;
            }
            
            // 明确的有效操作 - 真正的编辑操作
            if (lowerName.Contains("create") || 
                lowerName.Contains("delete") || 
                lowerName.Contains("destroy") ||
                lowerName.Contains("duplicate") ||
                lowerName.Contains("move") ||
                lowerName.Contains("rotate") ||
                lowerName.Contains("scale") ||
                lowerName.Contains("rename") ||
                lowerName.Contains("transform") ||
                lowerName.Contains("modify") ||
                lowerName.Contains("edit") ||
                lowerName.Contains("select") ||  // 通用选择操作
                lowerName.Contains("clear") ||   // 通用清除操作
                lowerName.Contains("add component") ||
                lowerName.Contains("remove component") ||
                lowerName.Contains("component edit") ||
                lowerName.Contains("property change") ||
                lowerName.Contains("position change") ||
                lowerName.Contains("scene modification") ||
                lowerName.Contains("hierarchy change") ||
                lowerName.Contains("operation"))
            {
                return true;
            }
            
            // 默认情况：严格过滤，未知操作认为无效
            return false;
        }
        
        /// <summary>
        /// 推测可撤销操作类型（已废弃 - 推测经常不准确）
        /// 现在只处理有明确Unity组名称的操作，不再进行推测
        /// </summary>
        [System.Obsolete("No longer used - now only processing operations with explicit Unity group names")]
        private static string InferUndoableOperationType(int groupId)
        {
            // 此方法已不再使用，保留仅为兼容性
            return "";
        }

        /// <summary>
        /// 专门推测选择相关的操作类型
        /// 只检测选择状态变化，不更新状态（状态更新由调用者负责）
        /// </summary>
        private static string InferSelectionOperationType(int groupId)
        {
            try
            {
                // 检查当前选择状态
                var currentSelection = Selection.objects;
                var activeGameObject = Selection.activeGameObject;
                var currentInstanceID = activeGameObject != null ? activeGameObject.GetInstanceID() : -1;
                var currentSelectionCount = currentSelection.Length;
                
                // 检查选择状态是否真正发生了变化
                if (currentInstanceID != lastSelectedInstanceID || currentSelectionCount != lastSelectionCount)
                {
                    // 确定操作类型，但不更新状态
                    if (activeGameObject != null)
                    {
                        // 有选中对象
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
                        // 没有选中对象，清除选择
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
        /// 更新选择状态跟踪
        /// </summary>
        private static void UpdateSelectionState()
        {
            var activeGameObject = Selection.activeGameObject;
            lastSelectedInstanceID = activeGameObject != null ? activeGameObject.GetInstanceID() : -1;
            lastSelectionCount = Selection.objects.Length;
        }
        
        /// <summary>
        /// 清理监听器（当编辑器关闭时）
        /// </summary>
        private static void Cleanup()
        {
            if (isInitialized)
            {
                EditorApplication.update -= MonitorUndoStack;
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                Selection.selectionChanged -= OnSelectionChanged;
                
                isInitialized = false;
                Debug.Log("[UnityUndoMonitor] System cleaned up");
            }
        }
        
        /// <summary>
        /// 尝试获取撤销组名称（保留原方法作为备用）
        /// </summary>
        private static string TryGetUndoGroupName(int groupId)
        {
            try
            {
                // Unity的Undo.GetCurrentGroupName()只能获取当前组的名称
                if (groupId == Undo.GetCurrentGroup())
                {
                    return Undo.GetCurrentGroupName();
                }
                else
                {
                    // 对于历史组，使用智能推测
                    return InferOperationType(groupId);
                }
            }
            catch
            {
                return $"Unknown Operation {groupId}";
            }
        }
        
        /// <summary>
        /// 从组名中提取操作名称
        /// </summary>
        private static string ExtractOperationName(string groupName)
        {
            if (groupName.StartsWith("[MCP]"))
            {
                return groupName.Substring(5).Trim();
            }
            return groupName;
        }
        
        /// <summary>
        /// 获取所有操作历史
        /// </summary>
        public static List<UndoOperation> GetAllOperations()
        {
            return allOperations.ToList();
        }
        
        /// <summary>
        /// 获取最新的N个操作
        /// </summary>
        public static List<UndoOperation> GetRecentOperations(int count = 10)
        {
            return allOperations.TakeLast(count).ToList();
        }
        
        /// <summary>
        /// 获取MCP操作数量
        /// </summary>
        public static int GetMcpOperationCount()
        {
            return allOperations.Count(op => op.isMcpOperation);
        }
        
        /// <summary>
        /// 获取手动操作数量
        /// </summary>
        public static int GetManualOperationCount()
        {
            return allOperations.Count(op => !op.isMcpOperation);
        }
        
        /// <summary>
        /// 执行撤销操作（使用Unity原生）
        /// </summary>
        public static void PerformUndo()
        {
            try
            {
                isPerformingUndoRedo = true;
                isCustomUndoRedo = true; // 标记为自定义操作
                
                // 首先检查是否有操作可以撤销
                if (allOperations.Count == 0)
                {
                    Debug.Log("[UnityUndoMonitor] No operations to undo");
                    isPerformingUndoRedo = false;
                    isCustomUndoRedo = false;
                    return;
                }
                
                // 将操作从undo栈移动到redo栈（在Unity执行之前）
                var operationToUndo = allOperations[allOperations.Count - 1];
                allOperations.RemoveAt(allOperations.Count - 1);
                redoOperations.Add(operationToUndo);
                
                // 记录undo操作时间，用于忽略后续的内部组变化
                lastUndoRedoTime = DateTime.Now;
                
                // 执行Unity的undo
                Undo.PerformUndo();
                Debug.Log($"[UnityUndoMonitor] ↶ Undo: {operationToUndo.DisplayName}");
                
                // 触发UI更新
                OnOperationsChanged?.Invoke();
                
                // 延迟重置标志，确保所有相关事件都已处理
                EditorApplication.delayCall += () =>
                {
                    // 重新同步选择状态跟踪
                    UpdateSelectionState();
                    
                    // 彻底同步撤销组状态，防止检测到undo操作产生的内部组变化
                    lastTrackedGroup = Undo.GetCurrentGroup();
                    lastUnityUndoCount = GetUnityUndoStackCount();
                    
                    // 增加额外的延迟，确保Unity完成所有相关的内部操作后再重置标志
                    EditorApplication.delayCall += () =>
                    {
                        // 最终同步，确保所有Unity内部状态都已稳定
                        lastTrackedGroup = Undo.GetCurrentGroup();
                        lastUnityUndoCount = GetUnityUndoStackCount();
                        isPerformingUndoRedo = false;
                        // isCustomUndoRedo 在OnUndoRedoPerformed中重置
                    };
                };
            }
            catch (Exception e)
            {
                isPerformingUndoRedo = false;
                isCustomUndoRedo = false;
                Debug.LogError($"[UnityUndoMonitor] Error performing undo: {e.Message}");
            }
        }
        
        /// <summary>
        /// 执行重做操作（使用Unity原生）
        /// </summary>
        public static void PerformRedo()
        {
            try
            {
                isPerformingUndoRedo = true;
                isCustomUndoRedo = true; // 标记为自定义操作
                
                // 首先检查是否有操作可以重做
                if (redoOperations.Count == 0)
                {
                    Debug.Log("[UnityUndoMonitor] No operations to redo");
                    isPerformingUndoRedo = false;
                    isCustomUndoRedo = false;
                    return;
                }
                
                // 将操作从redo栈移动回undo栈（在Unity执行之前）
                var operationToRedo = redoOperations[redoOperations.Count - 1];
                redoOperations.RemoveAt(redoOperations.Count - 1);
                allOperations.Add(operationToRedo);
                
                // 记录redo操作时间，用于忽略后续的内部组变化
                lastUndoRedoTime = DateTime.Now;
                
                // 执行Unity的redo
                Undo.PerformRedo();
                Debug.Log($"[UnityUndoMonitor] ↷ Redo: {operationToRedo.DisplayName}");
                
                // 触发UI更新
                OnOperationsChanged?.Invoke();
                
                // 延迟重置标志，确保所有相关事件都已处理
                EditorApplication.delayCall += () =>
                {
                    // 重新同步选择状态跟踪
                    UpdateSelectionState();
                    
                    // 彻底同步撤销组状态，防止检测到redo操作产生的内部组变化
                    lastTrackedGroup = Undo.GetCurrentGroup();
                    lastUnityUndoCount = GetUnityUndoStackCount();
                    
                    // 增加额外的延迟，确保Unity完成所有相关的内部操作后再重置标志
                    EditorApplication.delayCall += () =>
                    {
                        // 最终同步，确保所有Unity内部状态都已稳定
                        lastTrackedGroup = Undo.GetCurrentGroup();
                        lastUnityUndoCount = GetUnityUndoStackCount();
                        isPerformingUndoRedo = false;
                        // isCustomUndoRedo 在OnUndoRedoPerformed中重置
                    };
                };
            }
            catch (Exception e)
            {
                isPerformingUndoRedo = false;
                isCustomUndoRedo = false;
                Debug.LogError($"[UnityUndoMonitor] Error performing redo: {e.Message}");
            }
        }
        
        /// <summary>
        /// 清除监听历史
        /// </summary>
        public static void ClearHistory()
        {
            allOperations.Clear();
            redoOperations.Clear();
            lastTrackedGroup = Undo.GetCurrentGroup() - 1;
            lastUnityUndoCount = GetUnityUndoStackCount();
            OnOperationsChanged?.Invoke();
            Debug.Log("[UnityUndoMonitor] History cleared");
        }
        
        /// <summary>
        /// 获取当前撤销栈状态信息
        /// </summary>
        public static string GetStatusInfo()
        {
            return $"Unity Undo Group: {Undo.GetCurrentGroup()}, " +
                   $"Tracked Operations: {allOperations.Count}, " +
                   $"MCP: {GetMcpOperationCount()}, " +
                   $"Manual: {GetManualOperationCount()}";
        }
        
        /// <summary>
        /// 获取撤销操作数量（兼容MainWindowEditor）
        /// </summary>
        public static int GetUndoCount()
        {
            return allOperations.Count;
        }
        
        /// <summary>
        /// 获取重做操作数量
        /// </summary>
        public static int GetRedoCount()
        {
            return redoOperations.Count;
        }
        
        /// <summary>
        /// 获取撤销历史（兼容MainWindowEditor）
        /// </summary>
        public static List<UndoOperation> GetUndoHistory()
        {
            return allOperations.ToList();
        }
        
        /// <summary>
        /// 获取重做历史（兼容MainWindowEditor）
        /// </summary>
        public static List<UndoOperation> GetRedoHistory()
        {
            // 返回redo栈的副本，注意redo栈是反向的（最新的在最后）
            var redoHistory = redoOperations.ToList();
            redoHistory.Reverse(); // 反转以使最新的redo操作在前面
            return redoHistory;
        }
    }
} 