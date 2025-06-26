#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.IvanMurzak.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
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
            [Description("Name of the new GameObject (for create operation)")]
            string? name = null,
            [Description("Parent GameObject reference (for create and setParent operations)")]
            GameObjectRef? parentGameObjectRef = null,
            [Description("Transform position of the GameObject (for create operation)")]
            Vector3? position = null,
            [Description("Transform rotation of the GameObject in Euler angles (for create operation)")]
            Vector3? rotation = null,
            [Description("Transform scale of the GameObject (for create operation)")]
            Vector3? scale = null,
            [Description("World or Local space of transform (for create operation)")]
            bool isLocalSpace = false,
            [Description("-1 - No primitive type; 0 - Cube; 1 - Sphere; 2 - Capsule; 3 - Cylinder; 4 - Plane; 5 - Quad (for create operation)")]
            int primitiveType = -1,
            [Description("GameObject modification data (for modify operation)")]
            SerializedMemberList? gameObjectDiffs = null,
            [Description("Whether GameObject's world position should remain unchanged when setting parent (for setParent operation)")]
            bool worldPositionStays = true
        )
        {
            return operation.ToLower() switch
            {
                "create" => CreateGameObject(name, parentGameObjectRef, position, rotation, scale, isLocalSpace, primitiveType),
                "destroy" => DestroyGameObject(gameObjectRef),
                "duplicate" => DuplicateGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList())),
                "modify" => ModifyGameObjects(gameObjectDiffs, gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList())),
                "setparent" => SetParentGameObjects(gameObjectRefs ?? (gameObjectRef != null ? new GameObjectRefList { gameObjectRef } : new GameObjectRefList()), parentGameObjectRef, worldPositionStays),
                _ => "[Error] Invalid operation. Valid operations: 'create', 'destroy', 'duplicate', 'modify', 'setParent'"
            };
        }

        private string CreateGameObject(string? name, GameObjectRef? parentGameObjectRef, Vector3? position, Vector3? rotation, Vector3? scale, bool isLocalSpace, int primitiveType)
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

                Object.DestroyImmediate(go);
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

                for (int i = 0; i < gameObjectRefs.Count; i++)
                {
                    var go = GameObjectUtils.FindBy(gameObjectRefs[i], out var error);
                    if (error != null)
                    {
                        stringBuilder.AppendLine(error);
                        continue;
                    }
                    var objToModify = (object)go;
                    var type = TypeUtils.GetType(gameObjectDiffs[i].typeName);
                    if (typeof(UnityEngine.Component).IsAssignableFrom(type))
                    {
                        var component = go.GetComponent(type);
                        if (component == null)
                        {
                            stringBuilder.AppendLine($"[Error] Component '{type.FullName}' not found on GameObject '{go.name}'.");
                            continue;
                        }
                        objToModify = component;
                    }
                    Reflector.Instance.Populate(ref objToModify, gameObjectDiffs[i], stringBuilder);
                }

                return stringBuilder.ToString();
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