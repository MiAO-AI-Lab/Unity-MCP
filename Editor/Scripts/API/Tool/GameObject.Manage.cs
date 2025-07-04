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
                    
                    return $"[Success] Created {createdObjects.Count} GameObjects in batch.\n{stringBuilder}";
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

                    return $"[Success] Created GameObject.\n{go.Print()}";
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

                UnityEngine.Object.DestroyImmediate(go);
                return $"[Success] Destroy GameObject.";
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
                return @$"[Success] Duplicated {gos.Count} GameObjects in opened {location}.
Duplicated instanceIDs:
{string.Join(", ", Selection.instanceIDs)}";
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

                return summary.ToString();
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

                    targetGo.transform.SetParent(parentGo.transform, worldPositionStays: worldPositionStays);
                    changedCount++;

                    stringBuilder.AppendLine(@$"[Success] Set parent of {gameObjectRefs[i]} to {parentGameObjectRef}.");
                }

                if (changedCount > 0)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                return stringBuilder.ToString();
            });
        }
    }
} 