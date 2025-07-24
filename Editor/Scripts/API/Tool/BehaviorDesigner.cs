#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.MiAO.Unity.MCP.Common;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorDesigner.Runtime;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;

namespace com.MiAO.Unity.MCP.Editor.API
{
    [McpPluginToolType]
    public partial class Tool_BehaviorDesigner
    {
        const float X_OFFSET = 200f;
        const float Y_OFFSET = 20f;
        
        public static class Error
        {

            public static string NotFoundBehaviorSource(string assetPath)
                => $"[Error] BehaviorSource not found. Path: '{assetPath}'.\n" +
                   $"Please check if the asset is in the project and the path is correct.";

            public static string NotFoundExternalBehavior(string assetPath)
                => $"[Error] ExternalBehavior not found. Path: '{assetPath}'.\n" +
                   $"Please check if the asset is in the project and the path is correct.";

            public static string SourcePathIsEmpty()
                => "[Error] Source path is empty. Please provide a valid path. Sample: \"Assets/Scripts/MyScript.cs\".";

            public static string AssetPathMustStartWithAssets(string assetPath)
                => $"[Error] Asset path must start with 'Assets/'. Path: '{assetPath}'.";

            // BehaviorSource Management Errors
            public static string InvalidOperation()
                => "[Error] Invalid operation. Valid operations: 'read', 'addNode', 'deleteNode', 'moveNode'";

            public static string FailedToReadBehaviorSource(string message)
                => $"[Error] Failed to read BehaviorSource: {message}";

            public static string ParentTaskIdRequired(string operation)
                => $"[Error] Parent task ID is required for {operation}. Please provide a valid parentTaskId.";

            public static string ParentTaskNotFound(int parentTaskId)
                => $"[Error] Parent task with ID {parentTaskId} not found.";

            public static string TaskTypeNotFound(string taskTypeName)
                => $"[Error] Task type '{taskTypeName}' not found.";

            public static string FailedToCreateTaskInstance(string taskTypeName)
                => $"[Error] Failed to create instance of task type '{taskTypeName}'.";

            public static string FailedToOperate(string operation, string message)
                => $"[Error] Failed to {operation}: {message}";

            public static string TaskIdRequire(string operation)
                => $"[Error] Task ID is required for {operation}.";

            public static string TaskNotFound(int taskId)
                => $"[Error] Task with ID {taskId} not found.";

            public static string TaskCannotAcceptChildren(int parentTaskId, string taskTypeName)
                => $"[Error] Task with ID {parentTaskId} ({taskTypeName}) cannot accept children. Only composite tasks (Sequence, Parallel, Selector, etc.) can have child tasks.";

            public static string CircularReferenceDetected()
                => "[Error] Cannot move task to its own descendant (would create circular reference).";

            public static string NoRootTaskFound()
                => "[Error] No root task found in the behavior source.";

        }

        public static (BehaviorSource, ExternalBehavior) LoadBehaviorSourceFromAssetPath(string assetPath, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrEmpty(assetPath))
            {
                errorMessage = Error.SourcePathIsEmpty();
                return (null, null);
            }

            if (!assetPath.StartsWith("Assets/"))
            {
                errorMessage = Error.AssetPathMustStartWithAssets(assetPath);
                return (null, null);
            }

            var externalBehavior = AssetDatabase.LoadAssetAtPath<ExternalBehavior>(assetPath);

            if (externalBehavior == null)
            {
                errorMessage = Error.NotFoundExternalBehavior(assetPath);
                return (null, null);
            }

            var behaviorSource = externalBehavior.GetBehaviorSource();

            if (behaviorSource == null)
            {
                errorMessage = Error.NotFoundBehaviorSource(assetPath);
                return (null, null);
            }

            if (behaviorSource.TaskData != null && !string.IsNullOrWhiteSpace(behaviorSource.TaskData.JSONSerialization))
            {
                try
                {
                    JSONDeserialization.Load(behaviorSource.TaskData, behaviorSource, true);
                }
                catch (Exception ex)
                {
                    // For empty or corrupted BehaviorTrees, continue without deserialization
                    Debug.LogWarning($"Failed to deserialize BehaviorSource JSON at {assetPath}: {ex.Message}. This might be an empty BehaviorTree.");
                }
            }

            return (behaviorSource, externalBehavior);
        }

        public static void DumpBehaviorSourceToAsset(ExternalBehavior externalBehavior, BehaviorDesigner.Runtime.Tasks.Task entryTask, BehaviorDesigner.Runtime.Tasks.Task rootTask, List<BehaviorDesigner.Runtime.Tasks.Task> detachedTaskList)
        {
            var behaviorSource = externalBehavior.GetBehaviorSource();
            if (behaviorSource == null)
            {
                throw new Exception(Error.NotFoundBehaviorSource(externalBehavior.name));
            }

            // Save the modified BehaviorSource
            behaviorSource.Save(entryTask, rootTask, detachedTaskList);

            // Check if re-serialization is needed
            bool check = behaviorSource.CheckForSerialization(force: true);

            if (check)
            {
                // Regenerate the JSONSerialization part in TaskData and save; cannot use the original JSONSerialization
                TaskSerializationData taskData = behaviorSource.TaskData;
                taskData.JSONSerialization = CustomToJson(entryTask,rootTask,detachedTaskList);

                EditorUtility.SetDirty(externalBehavior);

                // Debug.Log($"JSONSerialization: {taskData.JSONSerialization}");
            }
            // Force save all assets
            AssetDatabase.SaveAssets();
            
            // Refresh the asset database
            AssetDatabase.Refresh();

            JSONDeserialization.Load(behaviorSource.TaskData, behaviorSource, true);
        }

        private static string CustomToJson(BehaviorDesigner.Runtime.Tasks.Task entryTask, BehaviorDesigner.Runtime.Tasks.Task rootTask, List<BehaviorDesigner.Runtime.Tasks.Task> detachedTasks)
        {
            var jsonData = new Dictionary<string, object>();
            
            if (entryTask != null)
            {
                jsonData["EntryTask"] = SerializeTask(entryTask);
            }
            
            if (rootTask != null)
            {
                jsonData["RootTask"] = SerializeTask(rootTask);
            }
            
            if (detachedTasks != null && detachedTasks.Count > 0)
            {
                var detachedTasksList = new List<object>();
                foreach (var task in detachedTasks)
                {
                    detachedTasksList.Add(SerializeTask(task));
                }
                jsonData["DetachedTasks"] = detachedTasksList;
            }
            
            return MiniJSON.Serialize(jsonData);
        }

        private static Dictionary<string, object> SerializeNodeData(NodeData nodeData, BehaviorDesigner.Runtime.Tasks.Task task)  
        {
            var nodeDataDict = new Dictionary<string, object>();

            if (nodeData.Offset != Vector2.zero)
            {
                nodeDataDict["Offset"] = $"({nodeData.Offset.x},{nodeData.Offset.y})";
            }

            // if (!string.IsNullOrEmpty(task.FriendlyName))
            // {
            //     nodeDataDict["FriendlyName"] = task.FriendlyName;
            // }

            if (!string.IsNullOrEmpty(nodeData.Comment))
            {
                nodeDataDict["Comment"] = nodeData.Comment;
            }

            if (nodeData.IsBreakpoint)
            {
                nodeDataDict["IsBreakpoint"] = nodeData.IsBreakpoint;
            }

            if (nodeData.Collapsed)
            {
                nodeDataDict["Collapsed"] = nodeData.Collapsed;
            }

            if (nodeData.ColorIndex != 0)
            {
                nodeDataDict["ColorIndex"] = nodeData.ColorIndex;
            }

            if (nodeData.WatchedFieldNames != null && nodeData.WatchedFieldNames.Count > 0)
            {
                var watchedFieldNames = new List<string>();
                var watchedFields = new List<FieldInfo>();

                foreach (var fieldName in nodeData.WatchedFieldNames)
                {
                    var field = task.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field != null)
                    {
                        watchedFieldNames.Add(field.Name);
                        watchedFields.Add(field);
                    }
                }

                nodeDataDict["WatchedFieldNames"] = watchedFieldNames;
                nodeDataDict["WatchedFields"] = watchedFields;
            }

            return nodeDataDict;
        }


        private static Dictionary<string, object> SerializeTask(BehaviorDesigner.Runtime.Tasks.Task task)
        {
            var taskData = new Dictionary<string, object>();
            
            // Basic task properties
            taskData["Type"] = task.GetType().FullName;
            taskData["ID"] = task.ID;
            taskData["Name"] = task.FriendlyName;
            taskData["Instant"] = task.IsInstant;
            
            // Add Disabled property if needed
            if (task.Disabled)
            {
                taskData["Disabled"] = task.Disabled;
            }
            
            // NodeData
            if (task.NodeData != null)
            {
                taskData["NodeData"] = SerializeNodeData(task.NodeData, task);
            }
            
            // Use TaskUtility.GetSerializableFields instead of GetFields directly
            var serializableFields = GetSerializableFields(task.GetType());
            
            foreach (var field in serializableFields)
            {
                // Skip basic properties that are already handled
                if (field.Name == "ID" || field.Name == "Name" || field.Name == "Instant" || 
                    field.Name == "NodeData" || field.Name == "Disabled")
                    continue;
                    
                var value = field.GetValue(task);
                if (value != null)
                {
                    // Use consistent field naming: FieldType.Name + FieldName
                    string fieldKey = field.FieldType.Name + field.Name;
                    
                    if (value is SharedVariable sharedVar)
                    {
                        taskData[fieldKey] = SerializeSharedVariable(sharedVar);
                    }
                    else if (value is List<BehaviorDesigner.Runtime.Tasks.Task> childTasks)
                    {
                        var childrenList = new List<object>();
                        foreach (var childTask in childTasks)
                        {
                            childrenList.Add(SerializeTask(childTask));
                        }
                        taskData["Children"] = childrenList;
                    }
                    else if (value is BehaviorDesigner.Runtime.Tasks.Task childTask)
                    {
                        // For single task references, store the ID instead of full serialization
                        // to avoid circular references and match deserialization expectations
                        taskData[fieldKey] = childTask.ID;
                    }
                    else if (value is System.Collections.IList list && 
                            list.Count > 0 && 
                            list[0] is BehaviorDesigner.Runtime.Tasks.Task)
                    {
                        // For task arrays/lists, store IDs
                        var taskIds = new List<int>();
                        foreach (BehaviorDesigner.Runtime.Tasks.Task taskItem in list)
                        {
                            taskIds.Add(taskItem.ID);
                        }
                        taskData[fieldKey] = taskIds;
                    }
                    else if (value is UnityEngine.Object unityObj)
                    {
                        // Handle Unity object references - would need unity objects list
                        // For now, just skip or handle specially
                        taskData[fieldKey] = -1; // Placeholder for Unity object reference
                    }
                    else if (value.GetType().IsPrimitive || value is string)
                    {
                        taskData[fieldKey] = value;
                    }
                    else
                    {
                        // Use SerializeValue for all other types (Vector2/3/4, Quaternion, Color, Rect, LayerMask, AnimationCurve, Enum, etc.)
                        var serializedValue = SerializeValue(value);
                        if (serializedValue == value && !value.GetType().IsPrimitive && !(value is string))
                        {
                            // If SerializeValue didn't handle it, try complex object serialization
                            taskData[fieldKey] = SerializeComplexObject(value);
                        }
                        else
                        {
                            taskData[fieldKey] = serializedValue;
                        }
                    }
                }
            }
            
            return taskData;
        }
        
        private static Dictionary<string, object> SerializeSharedVariable(SharedVariable sharedVar)
        {
            var sharedVarData = new Dictionary<string, object>();
            sharedVarData["Type"] = sharedVar.GetType().FullName;
            sharedVarData["Name"] = sharedVar.Name;
            sharedVarData["IsShared"] = sharedVar.IsShared;
            sharedVarData["IsGlobal"] = sharedVar.IsGlobal;
            sharedVarData["IsDynamic"] = sharedVar.IsDynamic;
            
            if (!string.IsNullOrEmpty(sharedVar.Tooltip))
            {
                sharedVarData["Tooltip"] = sharedVar.Tooltip;
            }
            
            if (!string.IsNullOrEmpty(sharedVar.PropertyMapping))
            {
                sharedVarData["PropertyMapping"] = sharedVar.PropertyMapping;
                // PropertyMappingOwner would need to be handled with Unity objects list
            }
            
            // Get the actual value using reflection
            var valueField = sharedVar.GetType().GetField("mValue", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (valueField != null)
            {
                var actualValue = valueField.GetValue(sharedVar);
                if (actualValue != null)
                {
                    var valueTypeName = valueField.FieldType.Name;
                    sharedVarData[$"{valueTypeName}mValue"] = SerializeValue(actualValue);
                }
            }
            
            return sharedVarData;
        }
        
        private static object SerializeValue(object value)
        {
            if (value == null) return null;
            
            if (value is Vector2 v2) return $"({v2.x},{v2.y})";
            if (value is Vector3 v3) return $"({v3.x},{v3.y},{v3.z})";
            if (value is Vector4 v4) return $"({v4.x},{v4.y},{v4.z},{v4.w})";
            if (value is Quaternion q) return $"({q.x},{q.y},{q.z},{q.w})";
            if (value is Color color) return $"RGBA({color.r},{color.g},{color.b},{color.a})";
            if (value is Rect rect) return $"(x:{rect.x}, y:{rect.y}, width:{rect.width}, height:{rect.height})";
            if (value is LayerMask layerMask) return layerMask.value;
            if (value is Enum enumValue) return enumValue.ToString();
            if (value is AnimationCurve curve) return SerializeAnimationCurve(curve);
            
            return value;
        }
        
        private static Dictionary<string, object> SerializeAnimationCurve(AnimationCurve curve)
        {
            var curveData = new Dictionary<string, object>();
            var keys = new List<object>();
            
            foreach (var keyframe in curve.keys)
            {
                var keyData = new List<object> { keyframe.time, keyframe.value, keyframe.inTangent, keyframe.outTangent };
                keys.Add(keyData);
            }
            
            curveData["Keys"] = keys;
            curveData["PreWrapMode"] = curve.preWrapMode.ToString();
            curveData["PostWrapMode"] = curve.postWrapMode.ToString();
            
            return curveData;
        }
        
        private static object SerializeComplexObject(object obj)
        {
            if (obj == null) return null;
            
            var objData = new Dictionary<string, object>();
            objData["Type"] = obj.GetType().FullName;
            
            var fields = GetSerializableFields(obj.GetType());
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                if (value != null)
                {
                    string fieldKey = field.FieldType.Name + field.Name;
                    objData[fieldKey] = SerializeValue(value);
                }
            }
            
            return objData;
        }
        
        // Helper method to get serializable fields (simplified version)
        private static FieldInfo[] GetSerializableFields(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => !f.IsNotSerialized && 
                                !f.IsLiteral && 
                                !f.IsInitOnly &&
                                !f.Name.StartsWith("k__BackingField"))
                    .ToArray();
        }

        #region Helper Methods

        private static string GetTaskInfo(BehaviorDesigner.Runtime.Tasks.Task task)
        {
            if (task == null) return "null";
            return $"{task.FriendlyName} (ID: {task.ID}, Type: {task.GetType().Name})";
        }

        private static void PrintTaskHierarchy(BehaviorDesigner.Runtime.Tasks.Task task, System.Text.StringBuilder sb, int depth, bool includeDetails)
        {
            if (task == null) return;

            string indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}├─ {GetTaskInfo(task)}");
            
            if (includeDetails && task.NodeData != null)
            {
                sb.AppendLine($"{indent}   Offset: ({task.NodeData.Offset.x}, {task.NodeData.Offset.y})");
                if (!string.IsNullOrEmpty(task.NodeData.Comment))
                    sb.AppendLine($"{indent}   Comment: {task.NodeData.Comment}");
            }

            // Print children if they exist
            var children = GetTaskChildren(task);
            foreach (var child in children)
            {
                PrintTaskHierarchy(child, sb, depth + 1, includeDetails);
            }
        }

        private static List<BehaviorDesigner.Runtime.Tasks.Task> GetTaskChildren(BehaviorDesigner.Runtime.Tasks.Task task)
        {
            var children = new List<BehaviorDesigner.Runtime.Tasks.Task>();
            if (task == null) return children;

            // Use reflection to find children field
            var fields = task.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(List<BehaviorDesigner.Runtime.Tasks.Task>))
                {
                    var value = field.GetValue(task) as List<BehaviorDesigner.Runtime.Tasks.Task>;
                    if (value != null)
                        children.AddRange(value);
                }
            }

            return children;
        }

        private static BehaviorDesigner.Runtime.Tasks.Task FindTaskById(BehaviorSource behaviorSource, int taskId)
        {
            // Search in entry task
            if (behaviorSource.EntryTask?.ID == taskId)
                return behaviorSource.EntryTask;

            // Search in root task and its children
            var found = FindTaskByIdRecursive(behaviorSource.RootTask, taskId);
            if (found != null) return found;

            // Search in detached tasks
            if (behaviorSource.DetachedTasks != null)
            {
                foreach (var task in behaviorSource.DetachedTasks)
                {
                    found = FindTaskByIdRecursive(task, taskId);
                    if (found != null) return found;
                }
            }

            return null;
        }

        private static BehaviorDesigner.Runtime.Tasks.Task FindTaskByIdRecursive(BehaviorDesigner.Runtime.Tasks.Task task, int taskId)
        {
            if (task == null) return null;
            if (task.ID == taskId) return task;

            var children = GetTaskChildren(task);
            foreach (var child in children)
            {
                var found = FindTaskByIdRecursive(child, taskId);
                if (found != null) return found;
            }

            return null;
        }

        private static int GetNextAvailableTaskId(BehaviorSource behaviorSource)
        {
            var usedIds = new HashSet<int>();
            CollectTaskIds(behaviorSource.EntryTask, usedIds);
            CollectTaskIds(behaviorSource.RootTask, usedIds);
            
            if (behaviorSource.DetachedTasks != null)
            {
                foreach (var task in behaviorSource.DetachedTasks)
                {
                    CollectTaskIds(task, usedIds);
                }
            }

            int nextId = 1;
            while (usedIds.Contains(nextId))
            {
                nextId++;
            }
            return nextId;
        }

        private static void CollectTaskIds(BehaviorDesigner.Runtime.Tasks.Task task, HashSet<int> usedIds)
        {
            if (task == null) return;
            usedIds.Add(task.ID);

            var children = GetTaskChildren(task);
            foreach (var child in children)
            {
                CollectTaskIds(child, usedIds);
            }
        }

        private static Vector2 CalculateNodeOffset(BehaviorDesigner.Runtime.Tasks.Task parentTask, int? elderBrotherTaskId, BehaviorSource behaviorSource)
        {
            if (parentTask?.NodeData == null)
                return Vector2.zero;

            var parentOffset = parentTask.NodeData.Offset;
            var children = GetTaskChildren(parentTask);

            if (!elderBrotherTaskId.HasValue || children.Count == 0)
            {
                // Place as the leftmost child
                float minX = children.Count > 0 ? children[0].NodeData.Offset.x : parentOffset.x;
                float minY = children.Count > 0 ? children[0].NodeData.Offset.y : parentOffset.y + Y_OFFSET;
                foreach (var child in children)
                {
                    if (child.NodeData != null && child.NodeData.Offset.x < minX)
                        minX = child.NodeData.Offset.x;
                    if (child.NodeData != null && child.NodeData.Offset.y < minY)
                        minY = child.NodeData.Offset.y;
                }
                return new Vector2(children.Count > 0 ? minX - X_OFFSET : minX, minY);
            }
            else
            {
                // Place to the right of elder brother
                var elderBrother = children.FirstOrDefault(c => c.ID == elderBrotherTaskId.Value);
                if (elderBrother?.NodeData != null)
                {
                    // Find the next sibling to the right of elder brother
                    var elderBrotherIndex = children.FindIndex(c => c.ID == elderBrotherTaskId.Value);
                    var nextSibling = elderBrotherIndex >= 0 && elderBrotherIndex + 1 < children.Count 
                        ? children[elderBrotherIndex + 1] 
                        : null;
                    
                    if (nextSibling?.NodeData != null)
                    {
                        // Place between elder brother and next sibling
                        float elderX = elderBrother.NodeData.Offset.x;
                        float nextX = nextSibling.NodeData.Offset.x;
                        float middleX = elderX + (nextX - elderX) * 0.5f;
                        
                        middleX = Mathf.Min(middleX, elderX + X_OFFSET);
                        
                        return new Vector2(middleX, elderBrother.NodeData.Offset.y);
                    }
                    else
                    {
                        // No next sibling, place to the right of elder brother
                        return new Vector2(elderBrother.NodeData.Offset.x + X_OFFSET, elderBrother.NodeData.Offset.y);
                    }
                }
                else
                {
                    // Elder brother not found, place at the end
                    return CalculateNodeOffset(parentTask, null, behaviorSource);
                }
            }
        }

        private static void AddTaskToParent(BehaviorDesigner.Runtime.Tasks.Task parentTask, BehaviorDesigner.Runtime.Tasks.Task newTask, int? elderBrotherTaskId)
        {
            // Find the children field in parent task
            var fields = parentTask.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(List<BehaviorDesigner.Runtime.Tasks.Task>))
                {
                    var children = field.GetValue(parentTask) as List<BehaviorDesigner.Runtime.Tasks.Task>;
                    if (children == null)
                    {
                        children = new List<BehaviorDesigner.Runtime.Tasks.Task>();
                        field.SetValue(parentTask, children);
                    }

                    if (!elderBrotherTaskId.HasValue)
                    {
                        children.Add(newTask);
                    }
                    else
                    {
                        int insertIndex = children.FindIndex(c => c.ID == elderBrotherTaskId.Value) + 1;
                        if (insertIndex <= 0) insertIndex = children.Count;
                        children.Insert(insertIndex, newTask);
                    }
                    break;
                }
            }
        }

        private static int CountAllChildTasks(BehaviorDesigner.Runtime.Tasks.Task task)
        {
            if (task == null) return 0;

            int count = 0;
            var children = GetTaskChildren(task);
            foreach (var child in children)
            {
                count += 1 + CountAllChildTasks(child);
            }
            return count;
        }

        private static void RemoveTaskFromBehaviorSource(BehaviorSource behaviorSource, BehaviorDesigner.Runtime.Tasks.Task taskToDelete)
        {
            // Remove from root task children
            RemoveTaskFromParentChildren(behaviorSource.RootTask, taskToDelete);

            // Remove from detached tasks
            if (behaviorSource.DetachedTasks != null)
            {
                if (behaviorSource.DetachedTasks.Contains(taskToDelete))
                {
                    behaviorSource.DetachedTasks.Remove(taskToDelete);
                }
                else
                {
                    foreach (var detachedTask in behaviorSource.DetachedTasks.ToList())
                    {
                        RemoveTaskFromParentChildren(detachedTask, taskToDelete);
                    }
                }
            }
        }

        private static void RemoveTaskFromParentChildren(BehaviorDesigner.Runtime.Tasks.Task parentTask, BehaviorDesigner.Runtime.Tasks.Task taskToRemove)
        {
            if (parentTask == null) return;

            var fields = parentTask.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(List<BehaviorDesigner.Runtime.Tasks.Task>))
                {
                    var children = field.GetValue(parentTask) as List<BehaviorDesigner.Runtime.Tasks.Task>;
                    if (children != null && children.Contains(taskToRemove))
                    {
                        children.Remove(taskToRemove);
                        return;
                    }
                }
            }

            // Recursively search in children
            var childTasks = GetTaskChildren(parentTask);
            foreach (var child in childTasks)
            {
                RemoveTaskFromParentChildren(child, taskToRemove);
            }
        }

        private static bool IsTaskDescendantOf(BehaviorDesigner.Runtime.Tasks.Task potentialDescendant, BehaviorDesigner.Runtime.Tasks.Task ancestor)
        {
            if (potentialDescendant == null || ancestor == null) return false;
            if (potentialDescendant.ID == ancestor.ID) return true;

            var children = GetTaskChildren(ancestor);
            foreach (var child in children)
            {
                if (IsTaskDescendantOf(potentialDescendant, child))
                    return true;
            }
            return false;
        }

        private static void MoveTaskAndChildren(BehaviorDesigner.Runtime.Tasks.Task task, Vector2 offsetDelta)
        {
            if (task?.NodeData == null) return;

            task.NodeData.Offset += offsetDelta;

            var children = GetTaskChildren(task);
            foreach (var child in children)
            {
                MoveTaskAndChildren(child, offsetDelta);
            }
        }

        private static bool CanTaskAcceptChildren(BehaviorDesigner.Runtime.Tasks.Task task)
        {
            if (task == null) return false;

            // Get the task type
            var taskType = task.GetType();

            // Check if it's a composite task that can accept children
            // Common composite task types in BehaviorDesigner
            var compositeTaskTypes = new[]
            {
                "BehaviorDesigner.Runtime.Tasks.Sequence",
                "BehaviorDesigner.Runtime.Tasks.Parallel",
                "BehaviorDesigner.Runtime.Tasks.Selector",
                "BehaviorDesigner.Runtime.Tasks.RandomSelector",
                "BehaviorDesigner.Runtime.Tasks.RandomSequence",
                "BehaviorDesigner.Runtime.Tasks.Interrupt",
                "BehaviorDesigner.Runtime.Tasks.ConditionalAbort",
                "BehaviorDesigner.Runtime.Tasks.UntilFailure",
                "BehaviorDesigner.Runtime.Tasks.UntilSuccess",
                "BehaviorDesigner.Runtime.Tasks.Repeat",
                "BehaviorDesigner.Runtime.Tasks.While",
                "BehaviorDesigner.Runtime.Tasks.ForEach",
                "BehaviorDesigner.Runtime.Tasks.ForEachList",
                "BehaviorDesigner.Runtime.Tasks.ForEachGameObject",
                "BehaviorDesigner.Runtime.Tasks.ForEachTransform",
                "BehaviorDesigner.Runtime.Tasks.ForEachVector3",
                "BehaviorDesigner.Runtime.Tasks.ForEachVector2",
                "BehaviorDesigner.Runtime.Tasks.ForEachInt",
                "BehaviorDesigner.Runtime.Tasks.ForEachFloat",
                "BehaviorDesigner.Runtime.Tasks.ForEachString",
                "BehaviorDesigner.Runtime.Tasks.ForEachBool",
                "BehaviorDesigner.Runtime.Tasks.ForEachColor",
                "BehaviorDesigner.Runtime.Tasks.ForEachQuaternion",
                "BehaviorDesigner.Runtime.Tasks.ForEachObject",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedVariable",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedGameObject",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedTransform",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedVector3",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedVector2",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedInt",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedFloat",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedString",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedBool",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedColor",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedQuaternion",
                "BehaviorDesigner.Runtime.Tasks.ForEachSharedObject"
            };

            var fullTypeName = taskType.FullName;
            return compositeTaskTypes.Contains(fullTypeName);
        }

        #endregion
    }
}