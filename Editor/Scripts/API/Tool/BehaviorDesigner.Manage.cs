#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.MiAO.Unity.MCP.Common;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Editor.API
{    
    public partial class Tool_BehaviorDesigner
    {   
        [McpPluginTool
        (
            "BehaviorDesigner_ManageBehaviorSource",
            Title = "Manage BehaviorDesigner BehaviorSource - Read, Add, Delete, Move nodes"
        )]
        [Description(@"Manage comprehensive BehaviorSource operations including:
- read: Read BehaviorDesigner content from asset path and return detailed node hierarchy
- addNode: Add a new node to the BehaviorSource with specified parent and elder-brother task IDs, automatically calculate node offset
- deleteNode: Delete a node by ID and recursively delete all child nodes
- moveNode: Move a node to a new parent with automatic offset calculation, recursively move all child nodes
- autoLayout: Auto layout the BehaviorSource, recursively layout all child nodes of the target task")]
        public string ManageBehaviorSource
        (
            [Description("Operation type: 'read', 'addNode', 'deleteNode', 'moveNode', 'autoLayout'")]
            string operation,
            [Description("Asset path to the BehaviorDesigner ExternalBehavior file. Starts with 'Assets/'. Ends with '.asset'.")]
            string assetPath,
            [Description("For addNode/deleteNode/moveNode/autoLayout: Target task ID")]
            int? taskId = null,
            [Description("For addNode/moveNode: the target parent task ID where the node should be attached")]
            int? parentTaskId = null,
            [Description("For addNode/moveNode: Elder-brother task ID (optional) - the node will be placed to the right of this elder-brother task. If not provided, the node will be placed as the leftmost child of the parent task.")]
            int? elderBrotherTaskId = null,
            [Description("For addNode: class name of the task type to create (e.g., 'BehaviorDesigner.Runtime.Tasks.Wait', 'Idle')")]
            string? taskTypeName = null,
            [Description("For addNode: Friendly name for the new task")]
            string? friendlyName = null,
            [Description("For read: Whether to include detailed task serialization information")]
            bool includeDetails = false
        )
        {
            return operation.ToLower() switch
            {
                "read" => ReadBehaviorSource(assetPath, includeDetails),
                "addnode" => AddNode(assetPath, parentTaskId, elderBrotherTaskId, taskTypeName, friendlyName),
                "deletenode" => DeleteNode(assetPath, taskId),
                "movenode" => MoveNode(assetPath, taskId, parentTaskId, elderBrotherTaskId),
                "autolayout" => AutoLayout(assetPath, taskId),
                _ => Error.InvalidOperation()
            };
        }
      
        #region BehaviorSource Management Methods
        
        private static string ReadBehaviorSource(string assetPath, bool includeDetails)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    var (behaviorSource, _) = LoadBehaviorSourceFromAssetPath(assetPath, out string errorMessage);
                    if (!string.IsNullOrEmpty(errorMessage))
                        throw new Exception(errorMessage);

                    var result = new System.Text.StringBuilder();
                    result.AppendLine($"[Success] BehaviorSource content from: {assetPath}");
                    result.AppendLine($"Entry Task: {GetTaskInfo(behaviorSource.EntryTask)}");
                    result.AppendLine($"Root Task: {GetTaskInfo(behaviorSource.RootTask)}");
                    result.AppendLine($"Detached Tasks Count: {behaviorSource.DetachedTasks?.Count ?? 0}");

                    // Check if BehaviorTree is empty
                    if (behaviorSource.RootTask == null && (behaviorSource.DetachedTasks == null || behaviorSource.DetachedTasks.Count == 0))
                    {
                        result.AppendLine("\n=== Empty BehaviorTree ===");
                        result.AppendLine("This BehaviorTree is empty. You can add the first node by using parentTaskId = -1 or null.");
                    }
                    else
                    {
                        if (behaviorSource.RootTask != null)
                        {
                            result.AppendLine("\n=== Task Hierarchy ===");
                            PrintTaskHierarchy(behaviorSource.RootTask, result, 0, includeDetails);
                        }

                        if (behaviorSource.DetachedTasks?.Count > 0)
                        {
                            result.AppendLine("\n=== Detached Tasks ===");
                            foreach (var task in behaviorSource.DetachedTasks)
                            {
                                PrintTaskHierarchy(task, result, 0, includeDetails);
                            }
                        }
                    }

                    return result.ToString();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex.StackTrace);
                    // Provide helpful error message for empty or corrupted BehaviorTree
                    if (ex.Message.Contains("character") || ex.Message.Contains("deserialization"))
                    {
                        return $"[Warning] BehaviorSource appears to be empty or corrupted: {ex.Message}\n" +
                               $"This might be an empty BehaviorTree. You can try adding the first node with parentTaskId = -1 or null.";
                    }
                    
                    return Error.FailedToReadBehaviorSource(ex.Message);
                }
            });
        }

        private static string AddNode(string assetPath, int? parentTaskId, int? elderBrotherTaskId, string? taskTypeName, string? friendlyName)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    var (behaviorSource, externalBehavior) = LoadBehaviorSourceFromAssetPath(assetPath, out string errorMessage);
                    if (!string.IsNullOrEmpty(errorMessage))
                        throw new Exception(errorMessage);

                    // Special handling for empty BehaviorTree
                    bool isEmptyBehaviorTree = behaviorSource.RootTask == null;
                    bool createAsRootTask = !parentTaskId.HasValue || parentTaskId.Value == -1 || isEmptyBehaviorTree;

                    BehaviorDesigner.Runtime.Tasks.Task parentTask = null;
                    
                    if (!createAsRootTask)
                    {
                        // Find parent task for normal node addition
                        parentTask = FindTaskById(behaviorSource, parentTaskId.Value);
                        if (parentTask == null)
                            return Error.ParentTaskNotFound(parentTaskId.Value);
                    }

                    // Create new task
                    var taskType = Type.GetType(taskTypeName);
                    if (taskType == null)
                    {
                        // Try to find in BehaviorDesigner assemblies
                        taskType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => a.GetTypes())
                            .FirstOrDefault(t => t.FullName == taskTypeName || t.Name == taskTypeName);
                    }

                    if (taskType == null)
                        return Error.TaskTypeNotFound(taskTypeName);

                    var newTask = Activator.CreateInstance(taskType) as BehaviorDesigner.Runtime.Tasks.Task;
                    if (newTask == null)
                        return Error.FailedToCreateTaskInstance(taskTypeName);

                    // Set task properties
                    newTask.ID = GetNextAvailableTaskId(behaviorSource);
                    newTask.FriendlyName = friendlyName ?? taskType.Name;
                    
                    // Create NodeData
                    newTask.NodeData = new NodeData();
                    
                    if (createAsRootTask)
                    {
                        // Creating as root task (for empty BehaviorTree or when parentTaskId is -1)
                        newTask.NodeData.Offset = new Vector2(0, 20); // Position at origin
                        
                        if (isEmptyBehaviorTree)
                        {
                            // Set as the root task
                            behaviorSource.RootTask = newTask;
                            
                            // Also create an entry task if it doesn't exist
                            if (behaviorSource.EntryTask == null)
                            {
                                var entryTaskType = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(a => a.GetTypes())
                                    .FirstOrDefault(t => t.FullName == "BehaviorDesigner.Runtime.Tasks.EntryTask" || t.Name == "EntryTask");
                                
                                if (entryTaskType != null)
                                {
                                    var entryTask = Activator.CreateInstance(entryTaskType) as BehaviorDesigner.Runtime.Tasks.Task;
                                    if (entryTask != null)
                                    {
                                        entryTask.ID = 0; // Entry task typically has ID 0
                                        entryTask.FriendlyName = "Entry";
                                        entryTask.NodeData = new NodeData();
                                        entryTask.NodeData.Offset = new Vector2(0, -100); // Position above root
                                        behaviorSource.EntryTask = entryTask;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Adding as detached task when parentTaskId is -1 but root exists
                            if (behaviorSource.DetachedTasks == null)
                                behaviorSource.DetachedTasks = new List<BehaviorDesigner.Runtime.Tasks.Task>();
                            
                            newTask.NodeData.Offset = new Vector2(300, 0); // Position to the right
                            behaviorSource.DetachedTasks.Add(newTask);
                        }
                    }
                    else
                    {
                        // Normal node addition to existing parent
                        newTask.NodeData.Offset = CalculateNodeOffset(parentTask, elderBrotherTaskId, behaviorSource);
                        AddTaskToParent(parentTask, newTask, elderBrotherTaskId);
                    }

                    // Save changes
                    DumpBehaviorSourceToAsset(externalBehavior, behaviorSource.EntryTask, behaviorSource.RootTask, behaviorSource.DetachedTasks);

                    string successMessage;
                    if (createAsRootTask && isEmptyBehaviorTree)
                    {
                        successMessage = $"[Success] Created first root task '{newTask.FriendlyName}' (ID: {newTask.ID}) in empty BehaviorTree.";
                    }
                    else if (createAsRootTask)
                    {
                        successMessage = $"[Success] Added detached task '{newTask.FriendlyName}' (ID: {newTask.ID}) to BehaviorTree.";
                    }
                    else
                    {
                        successMessage = $"[Success] Added new task '{newTask.FriendlyName}' (ID: {newTask.ID}) to parent task ID {parentTaskId}.";
                    }

                    return successMessage;
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex.StackTrace);
                    return Error.FailedToOperate("add a node", ex.Message);
                }
            });
        }

        private static string DeleteNode(string assetPath, int? taskId)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    if (!taskId.HasValue)
                        return Error.TaskIdRequire("deleting a node");

                    var (behaviorSource, externalBehavior) = LoadBehaviorSourceFromAssetPath(assetPath, out string errorMessage);
                    if (!string.IsNullOrEmpty(errorMessage))
                        throw new Exception(errorMessage);

                    var taskToDelete = FindTaskById(behaviorSource, taskId.Value);
                    if (taskToDelete == null)
                        return Error.TaskNotFound(taskId.Value);

                    // Count children that will be deleted
                    int deletedCount = CountAllChildTasks(taskToDelete) + 1;

                    // Remove task and all children
                    RemoveTaskFromBehaviorSource(behaviorSource, taskToDelete);

                    // Save changes
                    DumpBehaviorSourceToAsset(externalBehavior, behaviorSource.EntryTask, behaviorSource.RootTask, behaviorSource.DetachedTasks);

                    return $"[Success] Deleted task ID {taskId} and {deletedCount - 1} child tasks. Total deleted: {deletedCount}.";
                }
                catch (Exception ex)
                {
                    return Error.FailedToOperate("delete a node", ex.Message);
                }
            });
        }

        private static string MoveNode(string assetPath, int? taskId, int? parentTaskId, int? elderBrotherTaskId)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    if (!taskId.HasValue)
                        return Error.TaskIdRequire("moving a node");

                    if (!parentTaskId.HasValue)
                        return Error.ParentTaskIdRequired("moving a node");

                    var (behaviorSource, externalBehavior) = LoadBehaviorSourceFromAssetPath(assetPath, out string errorMessage);
                    if (!string.IsNullOrEmpty(errorMessage))
                        throw new Exception(errorMessage);

                    var taskToMove = FindTaskById(behaviorSource, taskId.Value);
                    if (taskToMove == null)
                        return Error.TaskNotFound(taskId.Value);

                    var newParentTask = FindTaskById(behaviorSource, parentTaskId.Value);
                    if (newParentTask == null)
                        return Error.ParentTaskNotFound(parentTaskId.Value);

                    // Check if the new parent task can accept children
                    if (!CanTaskAcceptChildren(newParentTask))
                        return Error.TaskCannotAcceptChildren(parentTaskId.Value, newParentTask.GetType().Name);

                    // Check if trying to move task to its own child (circular reference)
                    if (IsTaskDescendantOf(newParentTask, taskToMove))
                        return Error.CircularReferenceDetected();

                    // Remove from current parent
                    RemoveTaskFromBehaviorSource(behaviorSource, taskToMove);

                    // Calculate new offset and position
                    Vector2 newOffset = CalculateNodeOffset(newParentTask, elderBrotherTaskId, behaviorSource);
                    Vector2 offsetDelta = newOffset - taskToMove.NodeData.Offset;
                    
                    // Move task and all its children
                    MoveTaskAndChildren(taskToMove, offsetDelta);

                    // Add to new parent
                    AddTaskToParent(newParentTask, taskToMove, elderBrotherTaskId);

                    // Save changes
                    DumpBehaviorSourceToAsset(externalBehavior, behaviorSource.EntryTask, behaviorSource.RootTask, behaviorSource.DetachedTasks);

                    int movedCount = CountAllChildTasks(taskToMove) + 1;
                    return $"[Success] Moved task ID {taskId} and {movedCount - 1} child tasks to parent ID {parentTaskId}. Total moved: {movedCount}.";
                }
                catch (Exception ex)
                {
                    return Error.FailedToOperate("move a node", ex.Message);
                }
            });
        }

        private static string AutoLayout(string assetPath, int? taskId)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    var (behaviorSource, externalBehavior) = LoadBehaviorSourceFromAssetPath(assetPath, out string errorMessage);
                    if (!string.IsNullOrEmpty(errorMessage))
                        throw new Exception(errorMessage);

                    // Determine the root task for layout
                    BehaviorDesigner.Runtime.Tasks.Task rootTaskForLayout;
                    if (taskId.HasValue)
                    {
                        // Layout specific task and its children
                        rootTaskForLayout = FindTaskById(behaviorSource, taskId.Value);
                        if (rootTaskForLayout == null)
                            return Error.TaskNotFound(taskId.Value);
                    }
                    else
                    {
                        // Layout entire behavior tree starting from root
                        rootTaskForLayout = behaviorSource.RootTask;
                        if (rootTaskForLayout == null)
                            return Error.NoRootTaskFound();
                    }

                    // Perform auto layout
                    int layoutCount = PerformAutoLayout(rootTaskForLayout);

                    // Save changes
                    DumpBehaviorSourceToAsset(externalBehavior, behaviorSource.EntryTask, behaviorSource.RootTask, behaviorSource.DetachedTasks);

                    return $"[Success] Auto layout completed. {layoutCount} tasks were repositioned.";
                }
                catch (Exception ex)
                {
                    return Error.FailedToOperate("perform auto layout", ex.Message);
                }
            });
        }

        /// <summary>
        /// Performs auto layout on a task and all its children recursively
        /// </summary>
        /// <param name="task">The root task to layout</param>
        /// <returns>Number of tasks that were repositioned</returns>
        private static int PerformAutoLayout(BehaviorDesigner.Runtime.Tasks.Task task)
        {
            if (task == null) return 0;

            int layoutCount = 0;
            
            // Get children using the helper method
            var children = GetTaskChildren(task);
            
            // Layout this task's children first (bottom-up approach)
            if (children != null && children.Count > 0)
            {
                layoutCount += LayoutChildrenHorizontally(task);
            }

            // Recursively layout all children
            if (children != null)
            {
                foreach (var child in children)
                {
                    layoutCount += PerformAutoLayout(child);
                }
            }

            return layoutCount;
        }

        /// <summary>
        /// Layouts children of a task horizontally with equal spacing
        /// </summary>
        /// <param name="parentTask">The parent task whose children will be laid out</param>
        /// <returns>Number of tasks repositioned</returns>
        private static int LayoutChildrenHorizontally(BehaviorDesigner.Runtime.Tasks.Task parentTask, float spacing = X_OFFSET)
        {
            var children = GetTaskChildren(parentTask);
            if (children == null || children.Count == 0)
                return 0;

            int layoutCount = 0;
            List<BehaviorDesigner.Runtime.Tasks.Task> childrenList = children.ToList(); // Create a copy to avoid modification issues

            // Calculate total width needed for all children
            float totalLayoutWidth = spacing * (childrenList.Count - 1);

            totalLayoutWidth = Mathf.Max(totalLayoutWidth, 0f);

            Debug.Log($"totalLayoutWidth: {totalLayoutWidth}");

            // Calculate starting X position (center the layout under parent)
            float parentX = parentTask.NodeData?.Offset.x ?? 0f;
            float startX = parentX - (totalLayoutWidth / 2f) - spacing;

            Debug.Log($"NodeName: {parentTask.FriendlyName}, parentX, startX: {parentX}, {startX}");
            // Position each child
            float currentX = startX;
            float parentY = parentTask.NodeData?.Offset.y ?? 0f;
            float childY = parentY + Y_OFFSET; // Children are positioned below parent

            for (int i = 0; i < childrenList.Count; i++)
            {
                var child = childrenList[i];
                if (child?.NodeData != null)
                {
                    // Calculate child center position
                    float childCenterX = currentX + spacing;
                    
                    Debug.Log($"NodeName: {child.FriendlyName}, childCenterX: {childCenterX}");
                    // Update child position
                    Vector2 newOffset = new Vector2(childCenterX, childY);
                    
                    // Only count as layout if position actually changed
                    if (Vector2.Distance(child.NodeData.Offset, newOffset) > 0.1f)
                    {
                        child.NodeData.Offset = newOffset;
                        layoutCount++;
                    }

                    // Move to next child position
                    currentX += spacing;
                }
            }

            return layoutCount;
        }

        #endregion

    }
}