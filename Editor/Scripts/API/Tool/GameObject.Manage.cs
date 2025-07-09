#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Editor.API
{
    /// <summary>
    /// Simplified undo stack item
    /// </summary>
    public class SimpleUndoItem
    {
        public string operationName;
        public DateTime timestamp;
        public System.Action undoAction;
        public System.Action redoAction;
        
        public SimpleUndoItem(string name, System.Action undo, System.Action redo)
        {
            operationName = name;
            timestamp = DateTime.Now;
            undoAction = undo;
            redoAction = redo;
        }
    }

    /// <summary>
    /// Simplified undo stack manager
    /// </summary>
    public static class SimpleUndoStack
    {
        private static Stack<SimpleUndoItem> undoStack = new Stack<SimpleUndoItem>();
        private static Stack<SimpleUndoItem> redoStack = new Stack<SimpleUndoItem>();
        private const int MAX_STACK_SIZE = 20;

        public static void PushOperation(string operationName, System.Action undoAction, System.Action redoAction)
        {
            // New operation starts, clear redo stack (standard behavior)
            redoStack.Clear();
            
            var item = new SimpleUndoItem(operationName, undoAction, redoAction);
            undoStack.Push(item);
            
            // Limit stack size
            if (undoStack.Count > MAX_STACK_SIZE)
            {
                var items = undoStack.ToArray().Reverse().ToArray();
                undoStack.Clear();
                for (int i = 1; i < items.Length; i++)
                {
                    undoStack.Push(items[i]);
                }
            }
            
            Debug.Log($"🆕 Operation added to undo stack: {operationName} (Stack size: {undoStack.Count})");
        }

        public static bool Undo()
        {
            if (undoStack.Count == 0)
            {
                Debug.Log("📭 Undo stack is empty, cannot undo");
                return false;
            }

            var item = undoStack.Pop();
            try
            {
                Debug.Log($"↶ Executing undo: {item.operationName}");
                item.undoAction?.Invoke();
                redoStack.Push(item);
                
                EditorApplication.RepaintHierarchyWindow();
                Debug.Log($"✅ Undo successful: {item.operationName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Undo failed: {ex.Message}");
                undoStack.Push(item); // Put back
                return false;
            }
        }

        public static bool Redo()
        {
            if (redoStack.Count == 0)
            {
                Debug.Log("📭 Redo stack is empty, cannot redo");
                return false;
            }

            var item = redoStack.Pop();
            try
            {
                Debug.Log($"↷ Executing redo: {item.operationName}");
                item.redoAction?.Invoke();
                undoStack.Push(item);
                
                EditorApplication.RepaintHierarchyWindow();
                Debug.Log($"✅ Redo successful: {item.operationName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Redo failed: {ex.Message}");
                redoStack.Push(item); // Put back
                return false;
            }
        }

        public static int GetUndoCount() => undoStack.Count;
        public static int GetRedoCount() => redoStack.Count;
        
        public static List<SimpleUndoItem> GetUndoHistory() => undoStack.ToList();
        public static List<SimpleUndoItem> GetRedoHistory() => redoStack.ToList();
        
        public static void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
            Debug.Log("🧹 Undo stack cleared");
        }
        
        private static bool _isProcessingOperation = false;
        
        /// <summary>
        /// Undo specific operation by index (remove from undo stack and add to redo stack)
        /// </summary>
        /// <param name="index">Operation index (0 is the newest operation)</param>
        public static bool UndoSpecificOperation(int index)
        {
            if (_isProcessingOperation)
            {
                Debug.LogWarning("⚠️ Processing another operation, please try again later");
                return false;
            }
            
            _isProcessingOperation = true;
            
            try
            {
                var undoHistory = undoStack.ToArray(); // Get array, index 0 is newest
                
                if (index < 0 || index >= undoHistory.Length)
                {
                    Debug.LogError($"❌ Invalid operation index: {index}, valid range: 0-{undoHistory.Length - 1}");
                    return false;
                }
                
                var targetOperation = undoHistory[index];
                
                Debug.Log($"↶ Undoing specific operation: {targetOperation.operationName}");
                
                // Execute undo operation
                targetOperation.undoAction?.Invoke();
                
                // Remove operation from undo stack
                var newUndoStack = new Stack<SimpleUndoItem>();
                for (int i = undoHistory.Length - 1; i >= 0; i--) // Rebuild stack from oldest to newest
                {
                    if (i != index) // Skip the operation to remove
                    {
                        newUndoStack.Push(undoHistory[i]);
                    }
                }
                undoStack = newUndoStack;
                
                // Add undone operation to redo stack
                redoStack.Push(targetOperation);
                
                EditorApplication.RepaintHierarchyWindow();
                Debug.Log($"✅ Undo operation successful: {targetOperation.operationName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Undo operation failed: {ex.Message}");
                return false;
            }
            finally
            {
                _isProcessingOperation = false;
            }
        }
        
        /// <summary>
        /// Redo specific operation by index (remove from redo stack and add to undo stack)
        /// </summary>
        /// <param name="index">Operation index (0 is the newest operation)</param>
        public static bool RedoSpecificOperation(int index)
        {
            if (_isProcessingOperation)
            {
                Debug.LogWarning("⚠️ Processing another operation, please try again later");
                return false;
            }
            
            _isProcessingOperation = true;
            
            try
            {
                var redoHistory = redoStack.ToArray(); // Get array, index 0 is newest
                
                if (index < 0 || index >= redoHistory.Length)
                {
                    Debug.LogError($"❌ Invalid redo index: {index}, valid range: 0-{redoHistory.Length - 1}");
                    return false;
                }
                
                var targetOperation = redoHistory[index];
                
                Debug.Log($"↷ Redoing specific operation: {targetOperation.operationName}");
                
                // Execute redo operation
                targetOperation.redoAction?.Invoke();
                
                // Remove operation from redo stack
                var newRedoStack = new Stack<SimpleUndoItem>();
                for (int i = redoHistory.Length - 1; i >= 0; i--) // Rebuild stack from oldest to newest
                {
                    if (i != index) // Skip the operation to remove
                    {
                        newRedoStack.Push(redoHistory[i]);
                    }
                }
                redoStack = newRedoStack;
                
                // Add redone operation to undo stack
                undoStack.Push(targetOperation);
                
                EditorApplication.RepaintHierarchyWindow();
                Debug.Log($"✅ Redo operation successful: {targetOperation.operationName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Redo operation failed: {ex.Message}");
                return false;
            }
            finally
            {
                _isProcessingOperation = false;
            }
        }
    }

    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_Manage",
            Title = "Manage GameObjects - Create, Destroy, Duplicate, Modify, SetParent"
        )]
        [Description(@"Manage comprehensive GameObject operations including:
- create: Create a new GameObject at specific path
- destroy: Remove a GameObject and all nested GameObjects recursively
- duplicate: Clone GameObjects in opened Prefab or in a Scene
- modify: Update GameObjects and/or attached component's field and properties
- setParent: Assign parent GameObject for target GameObjects")]
        public string Operations
        (
            [Description("Operation type: 'create', 'destroy', 'duplicate', 'modify', 'setParent'")]
            string operation,
            [Description("GameObject reference for operations (required for destroy, duplicate, modify, setParent)")]
            GameObjectRef? gameObjectRef = null,
            [Description("List of GameObject references for operations that support multiple objects")]
            GameObjectRefList? gameObjectRefs = null,
            [Description("For create: Name of the new GameObject")]
            string? name = null,
            [Description("For create/setParent: Parent GameObject reference")]
            GameObjectRef? parentGameObjectRef = null,
            [Description("For create: Transform position of the GameObject")]
            Vector3? position = null,
            [Description("For create: Transform rotation of the GameObject in Euler angles")]
            Vector3? rotation = null,
            [Description("For create: Transform scale of the GameObject")]
            Vector3? scale = null,
            [Description("For create: Array of positions for batch creation")]
            Vector3[]? positions = null,
            [Description("For create: Array of rotations for batch creation")]
            Vector3[]? rotations = null,
            [Description("For create: World or Local space of transform")]
            bool isLocalSpace = false,
            [Description("For create: -1 - No primitive type; 0 - Cube; 1 - Sphere; 2 - Capsule; 3 - Cylinder; 4 - Plane; 5 - Quad")]
            int primitiveType = -1,
            [Description("For modify: GameObject modification data")]
            SerializedMemberList? gameObjectDiffs = null,
            [Description("For setParent: Whether GameObject's world position should remain unchanged when setting parent")]
            bool worldPositionStays = true
        )
        {
            return operation.ToLower() switch
            {
                "create" => CreateGameObject(name, parentGameObjectRef, position, rotation, scale, positions, rotations, isLocalSpace, primitiveType),
                "destroy" => DestroyGameObject(gameObjectRef),
                "duplicate" => DuplicateGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList())),
                "modify" => ModifyGameObjects(gameObjectDiffs, gameObjectRefs ?? (gameObjectRef != null ? GenerateGameObjectRefListFromSingleGameObjectRef(gameObjectRef, gameObjectDiffs?.Count ?? 0) : new GameObjectRefList())),
                "setparent" => SetParentGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList()), parentGameObjectRef, worldPositionStays),
                _ => "[Error] Invalid operation. Valid operations: 'create', 'destroy', 'duplicate', 'modify', 'setParent'"
            };
        }

        private GameObjectRefList GenerateGameObjectRefListFromSingleGameObjectRef(GameObjectRef gameObjectRef, int copiesCount)
        {
            var list = new GameObjectRefList();
            for (int i = 0; i < copiesCount; i++)
            {
                list.Add(gameObjectRef);
            }
            return list;
        }



        private GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
                
            var parts = path.Split('/');
            GameObject current = null;
            
            // Find root object
            foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootGo.name == parts[0])
                {
                    current = rootGo;
                    break;
                }
            }
            
            if (current == null)
                return null;
                
            // Find child objects in sequence
            for (int i = 1; i < parts.Length; i++)
            {
                var childTransform = current.transform.Find(parts[i]);
                if (childTransform == null)
                    return null;
                current = childTransform.gameObject;
            }
            
            return current;
        }

        private GameObject FindGameObjectByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
                
            // First try Unity's Find method (can only find root-level objects)
            var rootObj = GameObject.Find(name);
            if (rootObj != null)
                return rootObj;
                
            // If not found, traverse all objects in the scene
            foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindGameObjectByNameRecursive(rootGo, name);
                if (found != null)
                    return found;
            }
            
            return null;
        }

        private GameObject FindGameObjectByNameRecursive(GameObject obj, string name)
        {
            if (obj.name == name)
                return obj;
                
            foreach (Transform child in obj.transform)
            {
                var found = FindGameObjectByNameRecursive(child.gameObject, name);
                if (found != null)
                    return found;
            }
            
            return null;
        }

        private string CreateGameObject(string? name, GameObjectRef? parentGameObjectRef, Vector3? position, Vector3? rotation, Vector3? scale, Vector3[]? positions, Vector3[]? rotations, bool isLocalSpace, int primitiveType)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(name))
                    return Error.GameObjectNameIsEmpty();

                var parentGo = default(GameObject);
                if (parentGameObjectRef?.IsValid ?? false)
                {
                    parentGo = GameObjectUtils.FindBy(parentGameObjectRef, out var error);
                    if (error != null)
                        return error;
                }

                // Determine if this is batch creation
                bool isBatchCreate = (positions != null && positions.Length > 0) || (rotations != null && rotations.Length > 0);
                
                if (isBatchCreate)
                {
                    // Batch creation logic
                    var createdObjects = new List<GameObject>();
                    var stringBuilder = new StringBuilder();

                    // If using positions + rotation, and rotations is null, then rotations = [rotation * positions.Length]
                    if (rotation != null && rotations == null)
                    {
                        rotations = new Vector3[positions.Length];
                        for (int i = 0; i < positions.Length; i++)
                        {
                            rotations[i] = rotation ?? Vector3.zero;
                        }
                    }

                    // If using position + rotations, and positions is null, then positions = [position * rotations.Length]
                    if (positions == null && rotations != null)
                    {
                        positions = new Vector3[rotations.Length];
                        for (int i = 0; i < rotations.Length; i++)
                        {
                            positions[i] = position ?? Vector3.zero;
                        }
                    }
                    
                    // Check if rotations array length matches positions
                    if (rotations != null && positions != null && rotations.Length != positions.Length)
                    {
                        return $"[Error] The number of rotations ({rotations.Length}) must match the number of positions ({positions.Length}) or be null.";
                    }
                    
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var objectName = positions.Length > 1 ? $"{name}_{i + 1}" : name;
                        var objectPosition = positions[i];
                        var objectRotation = rotations?[i] ?? Vector3.zero;
                        
                        var go = primitiveType switch
                        {
                            0 => GameObject.CreatePrimitive(PrimitiveType.Cube),
                            1 => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                            2 => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                            3 => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                            4 => GameObject.CreatePrimitive(PrimitiveType.Plane),
                            5 => GameObject.CreatePrimitive(PrimitiveType.Quad),
                            _ => new GameObject(objectName)
                        };
                        
                        go.name = objectName;
                        go.SetTransform(objectPosition, objectRotation, scale, isLocalSpace);
                        
                        if (parentGo != null)
                            go.transform.SetParent(parentGo.transform, false);
                        
                        EditorUtility.SetDirty(go);
                        createdObjects.Add(go);
                        
                        stringBuilder.AppendLine($"Created: {go.name} (ID: {go.GetInstanceID()})");
                    }
                    
                    EditorApplication.RepaintHierarchyWindow();
                    
                    var result = $"[Success] Created {createdObjects.Count} GameObjects in batch.\n{stringBuilder}";
                    
                    // Add to undo stack
                    var createdObjectsData = createdObjects.Select(obj => new
                    {
                        InstanceID = obj.GetInstanceID(),
                        Name = obj.name,
                        Position = obj.transform.position,
                        Rotation = obj.transform.rotation,
                        Scale = obj.transform.localScale,
                        ParentPath = obj.transform.parent != null ? GetGameObjectPath(obj.transform.parent.gameObject) : null
                    }).ToList();
                    
                    SimpleUndoStack.PushOperation(
                        $"Create {createdObjects.Count} GameObjects",
                        // Undo action: delete all created objects
                        () => {
                            foreach (var objData in createdObjectsData)
                            {
                                var objectToDelete = EditorUtility.InstanceIDToObject(objData.InstanceID) as GameObject;
                                if (objectToDelete != null)
                                {
                                    UnityEngine.Object.DestroyImmediate(objectToDelete);
                                }
                                else
                                {
                                    // If can't find by instance ID, try to find by name
                                    var objectByName = FindGameObjectByName(objData.Name);
                                    if (objectByName != null)
                                    {
                                        UnityEngine.Object.DestroyImmediate(objectByName);
                                    }
                                }
                            }
                            Debug.Log($"🗑️ Undone batch creation operation, deleted {createdObjectsData.Count} objects");
                        },
                        // Redo action: recreate all objects
                        () => {
                            foreach (var objData in createdObjectsData)
                            {
                                // Check if object with same name already exists
                                var existingObject = FindGameObjectByName(objData.Name);
                                if (existingObject != null)
                                {
                                    Debug.LogWarning($"⚠️ Object already exists, skipping redo: {objData.Name}");
                                    continue;
                                }
                                
                                var newGo = primitiveType switch
                                {
                                    0 => GameObject.CreatePrimitive(PrimitiveType.Cube),
                                    1 => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                                    2 => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                                    3 => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                                    4 => GameObject.CreatePrimitive(PrimitiveType.Plane),
                                    5 => GameObject.CreatePrimitive(PrimitiveType.Quad),
                                    _ => new GameObject(objData.Name)
                                };
                                newGo.name = objData.Name;
                                newGo.transform.position = objData.Position;
                                newGo.transform.rotation = objData.Rotation;
                                newGo.transform.localScale = objData.Scale;
                                
                                // Reset parent object
                                if (!string.IsNullOrEmpty(objData.ParentPath))
                                {
                                    var parentObj = FindGameObjectByPath(objData.ParentPath);
                                    if (parentObj != null)
                                        newGo.transform.SetParent(parentObj.transform, false);
                                }
                                
                                EditorUtility.SetDirty(newGo);
                            }
                            Debug.Log($"🔄 Redone batch creation operation, recreated {createdObjectsData.Count} objects");
                        }
                    );
                    
                    return result;
                }
                else
                {
                    // Single object creation
                    var go = primitiveType switch
                    {
                        0 => GameObject.CreatePrimitive(PrimitiveType.Cube),
                        1 => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                        2 => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                        3 => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                        4 => GameObject.CreatePrimitive(PrimitiveType.Plane),
                        5 => GameObject.CreatePrimitive(PrimitiveType.Quad),
                        _ => new GameObject(name)
                    };
                    go.name = name;
                    go.SetTransform(position, rotation, scale, isLocalSpace);

                    if (parentGo != null)
                        go.transform.SetParent(parentGo.transform, false);

                    EditorUtility.SetDirty(go);
                    EditorApplication.RepaintHierarchyWindow();

                    var result = $"[Success] Created GameObject.\n{go.Print()}";
                    
                    // Add to undo stack
                    var goInstanceID = go.GetInstanceID();
                    var goName = go.name;
                    var goPrimitiveType = primitiveType;
                    var goPosition = go.transform.position;
                    var goRotation = go.transform.rotation;
                    var goScale = go.transform.localScale;
                    var goParent = go.transform.parent;
                    var goParentPath = goParent != null ? GetGameObjectPath(goParent.gameObject) : null;
                    
                    SimpleUndoStack.PushOperation(
                        $"Create GameObject: {goName}",
                        // Undo action: delete object
                        () => {
                            var objectToDelete = EditorUtility.InstanceIDToObject(goInstanceID) as GameObject;
                            if (objectToDelete != null)
                            {
                                UnityEngine.Object.DestroyImmediate(objectToDelete);
                                Debug.Log($"🗑️ Undone creation operation, deleted: {goName}");
                            }
                            else
                            {
                                // If can't find by instance ID, try to find by name
                                var objectByName = FindGameObjectByName(goName);
                                if (objectByName != null)
                                {
                                    UnityEngine.Object.DestroyImmediate(objectByName);
                                    Debug.Log($"🗑️ Undone creation operation, deleted: {goName} (found by name)");
                                }
                                else
                                {
                                    Debug.LogWarning($"⚠️ Object not found during undo: {goName}");
                                }
                            }
                        },
                        // Redo action: recreate object
                        () => {
                            // Check if object with same name already exists
                            var existingObject = FindGameObjectByName(goName);
                            if (existingObject != null)
                            {
                                Debug.LogWarning($"⚠️ Object already exists, skipping redo: {goName}");
                                return;
                            }
                            
                            var newGo = goPrimitiveType switch
                            {
                                0 => GameObject.CreatePrimitive(PrimitiveType.Cube),
                                1 => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                                2 => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                                3 => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                                4 => GameObject.CreatePrimitive(PrimitiveType.Plane),
                                5 => GameObject.CreatePrimitive(PrimitiveType.Quad),
                                _ => new GameObject(goName)
                            };
                            newGo.name = goName;
                            newGo.transform.position = goPosition;
                            newGo.transform.rotation = goRotation;
                            newGo.transform.localScale = goScale;
                            
                            // Reset parent object
                            if (!string.IsNullOrEmpty(goParentPath))
                            {
                                var parentObj = FindGameObjectByPath(goParentPath);
                                if (parentObj != null)
                                    newGo.transform.SetParent(parentObj.transform, false);
                            }
                            
                            EditorUtility.SetDirty(newGo);
                            Debug.Log($"🔄 Redone creation operation, recreated: {goName}");
                        }
                    );
                    
                    return result;
                }
            });
        }

        private string DestroyGameObject(GameObjectRef? gameObjectRef)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRef == null)
                    return "[Error] GameObject reference is required for destroy operation.";

                var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
                if (error != null)
                    return error;

                // Save object state before deletion for undo
                var goName = go.name;
                var goPosition = go.transform.position;
                var goRotation = go.transform.rotation;
                var goScale = go.transform.localScale;
                var goParentPath = go.transform.parent != null ? GetGameObjectPath(go.transform.parent.gameObject) : null;
                var goActive = go.activeInHierarchy;
                
                // Determine if it's a primitive
                var meshFilter = go.GetComponent<MeshFilter>();
                PrimitiveType? primitiveType = null;
                if (meshFilter?.sharedMesh != null)
                {
                    var meshName = meshFilter.sharedMesh.name.ToLower();
                    primitiveType = meshName switch
                    {
                        "cube" => PrimitiveType.Cube,
                        "sphere" => PrimitiveType.Sphere,
                        "capsule" => PrimitiveType.Capsule,
                        "cylinder" => PrimitiveType.Cylinder,
                        "plane" => PrimitiveType.Plane,
                        "quad" => PrimitiveType.Quad,
                        _ => null
                    };
                }

                // Execute deletion
                UnityEngine.Object.DestroyImmediate(go);
                
                // Add to undo stack
                SimpleUndoStack.PushOperation(
                    $"Delete GameObject: {goName}",
                    // Undo action: recreate object
                    () => {
                        GameObject newGo;
                        if (primitiveType.HasValue)
                        {
                            newGo = GameObject.CreatePrimitive(primitiveType.Value);
                        }
                        else
                        {
                            newGo = new GameObject();
                        }
                        
                        newGo.name = goName;
                        newGo.transform.position = goPosition;
                        newGo.transform.rotation = goRotation;
                        newGo.transform.localScale = goScale;
                        newGo.SetActive(goActive);
                        
                        // Reset parent object
                        if (!string.IsNullOrEmpty(goParentPath))
                        {
                            var parentObj = FindGameObjectByPath(goParentPath);
                            if (parentObj != null)
                                newGo.transform.SetParent(parentObj.transform, false);
                        }
                            
                        EditorUtility.SetDirty(newGo);
                        Debug.Log($"🔄 Undone deletion operation, recreated: {goName}");
                    },
                    // Redo action: delete object again
                    () => {
                        var objToDelete = FindGameObjectByName(goName);
                        if (objToDelete != null)
                        {
                            UnityEngine.Object.DestroyImmediate(objToDelete);
                            Debug.Log($"🗑️ Redone deletion operation, deleted: {goName}");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ Object not found during redo deletion: {goName}");
                        }
                    }
                );
                
                return $"[Success] Destroy GameObject: {goName}";
            });
        }

        private string DuplicateGameObjects(GameObjectRefList gameObjectRefs)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRefs.Count == 0)
                    return "[Error] No GameObject references provided for duplicate operation.";

                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                var gos = new List<GameObject>(gameObjectRefs.Count);

                for (int i = 0; i < gameObjectRefs.Count; i++)
                {
                    var go = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                    if (error != null)
                        return error;

                    gos.Add(go);
                }

                Selection.instanceIDs = gos
                    .Select(go => go.GetInstanceID())
                    .ToArray();

                Unsupported.DuplicateGameObjectsUsingPasteboard();

                var modifiedScenes = Selection.gameObjects
                    .Select(go => go.scene)
                    .Distinct()
                    .ToList();

                foreach (var scene in modifiedScenes)
                    EditorSceneManager.MarkSceneDirty(scene);

                var location = prefabStage != null ? "Prefab" : "Scene";
                var result = @$"[Success] Duplicated {gos.Count} GameObjects in opened {location}.
Duplicated instanceIDs:
{string.Join(", ", Selection.instanceIDs)}";
                
                // Add to undo stack
                var duplicatedObjects = Selection.gameObjects.ToList();
                var originalGameObjectRefs = gameObjectRefs.ToList(); // Save original reference info
                SimpleUndoStack.PushOperation(
                    $"Duplicate {gos.Count} GameObjects",
                    // Undo action: delete duplicated objects
                    () => {
                        foreach (var duplicatedGo in duplicatedObjects)
                        {
                            if (duplicatedGo != null)
                            {
                                UnityEngine.Object.DestroyImmediate(duplicatedGo);
                            }
                        }
                        Debug.Log($"🗑️ Undone duplication operation, deleted {duplicatedObjects.Count} objects");
                    },
                    // Redo action: duplicate objects again
                    () => {
                        // Re-get original object references
                        var originalObjects = new List<GameObject>();
                        foreach (var gameObjectRef in originalGameObjectRefs)
                        {
                            var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
                            if (error == null && go != null)
                                originalObjects.Add(go);
                        }
                        
                        if (originalObjects.Count > 0)
                        {
                            // Re-select original objects and duplicate
                            Selection.instanceIDs = originalObjects
                                .Select(go => go.GetInstanceID())
                                .ToArray();
                            Unsupported.DuplicateGameObjectsUsingPasteboard();
                            Debug.Log($"🔄 Redone duplication operation, re-duplicated {originalObjects.Count} objects");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ Original objects not found during redo duplication");
                        }
                    }
                );
                
                return result;
            });
        }

        private string ModifyGameObjects(SerializedMemberList? gameObjectDiffs, GameObjectRefList gameObjectRefs)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRefs.Count == 0)
                    return "[Error] No GameObject references provided for modify operation.";

                if (gameObjectDiffs == null || gameObjectDiffs.Count == 0)
                    return "[Error] No modification data provided for modify operation.";

                if (gameObjectDiffs.Count != gameObjectRefs.Count)
                    return $"[Error] The number of gameObjectDiffs and gameObjectRefs should be the same. " +
                        $"gameObjectDiffs: {gameObjectDiffs.Count}, gameObjectRefs: {gameObjectRefs.Count}";

                var stringBuilder = new StringBuilder();
                var successCount = 0;
                var errorCount = 0;
                
                // Save state before modification for undo
                var originalStates = new List<(GameObject go, object targetObj, SerializedMember originalData)>();

                for (int i = 0; i < gameObjectRefs.Count; i++)
                {
                    var go = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                    if (error != null)
                    {
                        stringBuilder.AppendLine($"[Error] GameObject {i}: {error}");
                        errorCount++;
                        continue;
                    }

                    try
                    {
                        var objToModify = (object)go;
                        var type = TypeUtils.GetType(gameObjectDiffs[i].typeName);
                        if (typeof(UnityEngine.Component).IsAssignableFrom(type))
                        {
                            var component = go.GetComponent(type);
                            if (component == null)
                            {
                                stringBuilder.AppendLine($"[Error] GameObject {i}: Component '{type.FullName}' not found on GameObject '{go.name}'.");
                                errorCount++;
                                continue;
                            }
                            objToModify = component;
                        }

                        // Save state before modification
                        var originalData = Reflector.Instance.Serialize(objToModify);
                        originalStates.Add((go, objToModify, originalData));

                        var populateResult = Reflector.Instance.Populate(ref objToModify, gameObjectDiffs[i]);
                        var populateResultString = populateResult.ToString().Trim();

                        // Check if the result contains error information
                        if (string.IsNullOrEmpty(populateResultString))
                        {
                            stringBuilder.AppendLine($"[Success] GameObject {i}: '{go.name}' modified successfully (no detailed feedback).");
                            successCount++;
                        }
                        else if (populateResultString.Contains("[Error]") || populateResultString.Contains("error", StringComparison.OrdinalIgnoreCase))
                        {
                            stringBuilder.AppendLine($"[Error] GameObject {i}: '{go.name}' - {populateResultString}");
                            errorCount++;
                        }
                        else
                        {
                            stringBuilder.AppendLine($"[Success] GameObject {i}: '{go.name}' - {populateResultString}");
                            successCount++;
                        }

                        // Mark the object as modified
                        if (objToModify is UnityEngine.Object unityObj)
                        {
                            EditorUtility.SetDirty(unityObj);
                        }
                    }
                    catch (Exception ex)
                    {
                        stringBuilder.AppendLine($"[Error] GameObject {i}: Exception occurred - {ex.Message}");
                        errorCount++;
                    }
                }

                // Generate summary
                var summary = new StringBuilder();
                if (successCount > 0 && errorCount == 0)
                {
                    summary.AppendLine($"[Success] All {successCount} GameObject(s) modified successfully.");
                }
                else if (successCount > 0 && errorCount > 0)
                {
                    summary.AppendLine($"[Partial Success] {successCount} GameObject(s) modified successfully, {errorCount} failed.");
                }
                else if (errorCount > 0)
                {
                    summary.AppendLine($"[Error] All {errorCount} GameObject(s) failed to modify.");
                }

                summary.AppendLine();
                summary.Append(stringBuilder);

                var result = summary.ToString();
                
                // If there are successful modifications, add to undo stack
                if (successCount > 0 && originalStates.Count > 0)
                {
                    var originalStatesCopy = new List<(GameObject go, object targetObj, SerializedMember originalData)>(originalStates);
                    var gameObjectDiffsCopy = new List<SerializedMember>(gameObjectDiffs);
                    var gameObjectRefsCopy = new List<GameObjectRef>(gameObjectRefs);
                    
                    SimpleUndoStack.PushOperation(
                        $"Modify GameObject: {string.Join(", ", originalStatesCopy.Select(s => s.go?.name ?? "Unknown"))}",
                        // Undo action: restore original state
                        () => {
                            try
                            {
                                foreach (var (go, targetObj, originalData) in originalStatesCopy)
                                {
                                    if (go != null && targetObj != null)
                                    {
                                        var objToRestore = targetObj;
                                        Reflector.Instance.Populate(ref objToRestore, originalData);
                                        EditorUtility.SetDirty(go);
                                    }
                                }
                                EditorApplication.RepaintHierarchyWindow();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Undo modification failed: {ex.Message}");
                            }
                        },
                        // Redo action: reapply modifications (re-get object references)
                        () => {
                            try
                            {
                                for (int i = 0; i < gameObjectRefsCopy.Count && i < gameObjectDiffsCopy.Count; i++)
                                {
                                    var gameObjectRef = gameObjectRefsCopy[i];
                                    var diff = gameObjectDiffsCopy[i];
                                    
                                    // Re-get GameObject reference
                                    var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
                                    if (error != null || go == null)
                                    {
                                        Debug.LogWarning($"GameObject not found during redo: {error}");
                                        continue;
                                    }
                                    
                                    // Re-get target object
                                    var objToModify = (object)go;
                                    var type = TypeUtils.GetType(diff.typeName);
                                    if (typeof(UnityEngine.Component).IsAssignableFrom(type))
                                    {
                                        var component = go.GetComponent(type);
                                        if (component == null)
                                        {
                                            Debug.LogWarning($"Component '{type.FullName}' not found on GameObject '{go.name}' during redo");
                                            continue;
                                        }
                                        objToModify = component;
                                    }
                                    
                                    // Apply modifications
                                    Reflector.Instance.Populate(ref objToModify, diff);
                                    EditorUtility.SetDirty(go);
                                }
                                EditorApplication.RepaintHierarchyWindow();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Redo modification failed: {ex.Message}");
                            }
                        }
                    );
                }
                
                return result;
            });
        }

        private string SetParentGameObjects(GameObjectRefList gameObjectRefs, GameObjectRef? parentGameObjectRef, bool worldPositionStays)
        {
            return MainThread.Instance.Run(() =>
            {
                if (gameObjectRefs.Count == 0)
                    return "[Error] No GameObject references provided for setParent operation.";

                if (parentGameObjectRef == null)
                    return "[Error] Parent GameObject reference is required for setParent operation.";

                var stringBuilder = new StringBuilder();
                int changedCount = 0;
                
                // Save parent state before modification
                var originalParents = new List<(GameObject go, string originalParentPath)>();

                for (var i = 0; i < gameObjectRefs.Count; i++)
                {
                    var targetGo = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                    if (error != null)
                    {
                        stringBuilder.AppendLine(error);
                        continue;
                    }

                    var parentGo = GameObjectUtils.FindBy(parentGameObjectRef, out error);
                    if (error != null)
                    {
                        stringBuilder.AppendLine(error);
                        continue;
                    }

                    // Save original parent path
                    var originalParentPath = targetGo.transform.parent != null ? GetGameObjectPath(targetGo.transform.parent.gameObject) : null;
                    originalParents.Add((targetGo, originalParentPath));

                    targetGo.transform.SetParent(parentGo.transform, worldPositionStays: worldPositionStays);
                    changedCount++;

                    stringBuilder.AppendLine(@$"[Success] Set parent of {gameObjectRefs[i]} to {parentGameObjectRef}.");
                }

                if (changedCount > 0)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                var result = stringBuilder.ToString();
                
                // If there are successful modifications, add to undo stack
                if (changedCount > 0 && originalParents.Count > 0)
                {
                    var originalParentsCopy = new List<(GameObject go, string originalParentPath)>(originalParents);
                    var newParentRef = parentGameObjectRef; // Save new parent reference info
                    
                    SimpleUndoStack.PushOperation(
                        $"Set parent for {changedCount} GameObjects",
                        // Undo action: restore original parent
                        () => {
                            try
                            {
                                foreach (var (go, originalParentPath) in originalParentsCopy)
                                {
                                    if (go != null)
                                    {
                                        Transform originalParent = null;
                                        if (!string.IsNullOrEmpty(originalParentPath))
                                        {
                                            var parentObj = FindGameObjectByPath(originalParentPath);
                                            if (parentObj != null)
                                                originalParent = parentObj.transform;
                                        }
                                        
                                        go.transform.SetParent(originalParent, worldPositionStays);
                                        EditorUtility.SetDirty(go);
                                    }
                                }
                                EditorApplication.RepaintHierarchyWindow();
                                Debug.Log($"🔄 Undone set parent operation, restored parent for {originalParentsCopy.Count} objects");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Undo set parent failed: {ex.Message}");
                            }
                        },
                        // Redo action: set parent again
                        () => {
                            try
                            {
                                // Re-get new parent object
                                var newParentGo = GameObjectUtils.FindBy(newParentRef, out var error);
                                if (error != null || newParentGo == null)
                                {
                                    Debug.LogWarning($"⚠️ Target parent not found during redo set parent: {error}");
                                    return;
                                }
                                
                                foreach (var (go, _) in originalParentsCopy)
                                {
                                    if (go != null)
                                    {
                                        go.transform.SetParent(newParentGo.transform, worldPositionStays);
                                        EditorUtility.SetDirty(go);
                                    }
                                }
                                EditorApplication.RepaintHierarchyWindow();
                                Debug.Log($"🔄 Redone set parent operation, reset parent for {originalParentsCopy.Count} objects");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Redo set parent failed: {ex.Message}");
                            }
                        }
                    );
                }
                
                return result;
            });
        }
    }
} 