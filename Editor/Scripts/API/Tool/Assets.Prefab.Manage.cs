#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Linq;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Editor.Utils;
using com.MiAO.Unity.MCP.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_Prefab
    {
        [McpPluginTool
        (
            "Assets_Prefab_Manage",
            Title = "Manage Prefabs - Create, Open, Close, Save, Read, Instantiate prefabs"
        )]
        [Description(@"Manage comprehensive prefab operations including:

- create: Create a prefab from a GameObject in a scene. The prefab will be saved in the project assets at the specified path.
- open: Open a prefab for editing. There are two options to open prefab:
  1. Open prefab from asset using 'prefabAssetPath'
  2. Open prefab from GameObject in loaded scene using 'instanceID' of the GameObject (the GameObject should be connected to a prefab)
  Note: Please use 'close' operation later to exit prefab editing mode.
- close: Close a prefab. Use it when you are in prefab editing mode in Unity Editor.
- save: Save a prefab. Use it when you are in prefab editing mode in Unity Editor.
- read: Read a prefab content. Use it for get started with prefab editing. There are two options to read prefab:
  1. Read prefab from asset using 'prefabAssetPath'
  2. Read prefab from GameObject in loaded scene using 'instanceID' of the GameObject (the GameObject should be connected to a prefab)
- instantiate: Instantiates prefab in a scene at the specified GameObject path.")]
        public string Management
        (
            [Description("Operation type: 'create', 'open', 'close', 'save', 'read', 'instantiate'")]
            string operation,
            [Description("Prefab asset path. Should be in the format 'Assets/Path/To/Prefab.prefab'. Required for: create, open, read, instantiate")]
            string? prefabAssetPath = null,
            [Description("GameObject instanceID in scene. Required for: create. Optional for: open, read")]
            int instanceID = 0,
            [Description("GameObject path in the current active scene. Required for: instantiate")]
            string? gameObjectPath = null,
            [Description("Transform position of the GameObject. For: create, instantiate")]
            Vector3? position = default,
            [Description("Transform rotation of the GameObject. Euler angles in degrees. For: create, instantiate")]
            Vector3? rotation = default,
            [Description("Transform scale of the GameObject. For: create, instantiate")]
            Vector3? scale = default,
            [Description("World or Local space of transform. For: create, instantiate")]
            bool isLocalSpace = false,
            [Description("If true, save prefab when closing. For: close")]
            bool save = true,
            [Description("If true, replace GameObject with prefab instance. For: create")]
            bool replaceGameObjectWithPrefab = true,
            [Description("Hierarchy depth to include in read operation. For: read")]
            int includeChildrenDepth = 3
        )
        {
            return operation.ToLower() switch
            {
                "create" => CreatePrefab(prefabAssetPath, instanceID, replaceGameObjectWithPrefab),
                "open" => OpenPrefab(prefabAssetPath, instanceID),
                "close" => ClosePrefab(save),
                "save" => SavePrefab(),
                "read" => ReadPrefab(prefabAssetPath, instanceID, includeChildrenDepth),
                "instantiate" => InstantiatePrefab(prefabAssetPath, gameObjectPath, position, rotation, scale, isLocalSpace),
                _ => "[Error] Invalid operation. Valid operations: 'create', 'open', 'close', 'save', 'read', 'instantiate'"
            };
        }

        private string CreatePrefab(string? prefabAssetPath, int instanceID, bool replaceGameObjectWithPrefab)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(prefabAssetPath))
                    return Error.PrefabPathIsEmpty();

                if (!prefabAssetPath.EndsWith(".prefab"))
                    return Error.PrefabPathIsInvalid(prefabAssetPath);

                var go = GameObjectUtils.FindByInstanceID(instanceID);
                if (go == null)
                    return Tool_GameObject.Error.NotFoundGameObjectWithInstanceID(instanceID);

                var prefabGo = replaceGameObjectWithPrefab
                    ? PrefabUtility.SaveAsPrefabAsset(go, prefabAssetPath)
                    : PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabAssetPath, InteractionMode.UserAction, out _);

                if (prefabGo == null)
                    return Error.NotFoundPrefabAtPath(prefabAssetPath);

                EditorUtility.SetDirty(go);
                EditorApplication.RepaintHierarchyWindow();

                var result = Reflector.Instance.Serialize(
                    prefabGo,
                    recursive: false,
                    logger: McpPlugin.Instance.Logger
                );

                return $"[Success] Prefab '{prefabAssetPath}' created from GameObject '{go.name}' (InstanceID: {instanceID}).\n" +
                       $"Prefab GameObject:\n{result}";
            });
        }

        private string OpenPrefab(string? prefabAssetPath, int instanceID)
        {
            return MainThread.Instance.Run(() =>
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

                if (string.IsNullOrEmpty(prefabAssetPath) && instanceID != 0)
                {
                    // Find prefab from GameObject in loaded scene
                    var go = GameObjectUtils.FindByInstanceID(instanceID);
                    if (go == null)
                        return Tool_GameObject.Error.NotFoundGameObjectWithInstanceID(instanceID);

                    prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                }

                if (string.IsNullOrEmpty(prefabAssetPath))
                    return Error.PrefabPathIsEmpty();

                var goInstance = instanceID != 0
                    ? GameObjectUtils.FindByInstanceID(instanceID)
                    : null;

                prefabStage = goInstance != null
                    ? PrefabStageUtility.OpenPrefab(prefabAssetPath, goInstance)
                    : PrefabStageUtility.OpenPrefab(prefabAssetPath);

                if (prefabStage == null)
                    return Error.PrefabStageIsNotOpened();

                return @$"[Success] Prefab '{prefabStage.assetPath}' opened. Use operation 'close' to close it.
# Prefab information:
{prefabStage.prefabContentsRoot.ToMetadata().Print()}";
            });
        }

        private string ClosePrefab(bool save)
        {
            return MainThread.Instance.Run(() =>
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                    return Error.PrefabStageIsNotOpened();

                var prefabGo = prefabStage.prefabContentsRoot;
                if (prefabGo == null)
                    return Error.PrefabStageIsNotOpened();

                var assetPath = prefabStage.assetPath;
                var goName = prefabGo.name;

                if (save)
                    PrefabUtility.SaveAsPrefabAsset(prefabGo, assetPath);

                StageUtility.GoBackToPreviousStage();

                return @$"[Success] Prefab at asset path '{assetPath}' closed. " +
                       $"Prefab with GameObject.name '{goName}' saved: {save}.";
            });
        }

        private string SavePrefab()
        {
            return MainThread.Instance.Run(() =>
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                    return Error.PrefabStageIsNotOpened();

                var prefabGo = prefabStage.prefabContentsRoot;
                if (prefabGo == null)
                    return Error.PrefabStageIsNotOpened();

                var assetPath = prefabStage.assetPath;
                var goName = prefabGo.name;

                PrefabUtility.SaveAsPrefabAsset(prefabGo, assetPath);

                return @$"[Success] Prefab at asset path '{assetPath}' saved. " +
                       $"Prefab with GameObject.name '{goName}'.";
            });
        }

        private string ReadPrefab(string? prefabAssetPath, int instanceID, int includeChildrenDepth)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(prefabAssetPath) && instanceID != 0)
                {
                    // Find prefab from GameObject in loaded scene
                    var go = GameObjectUtils.FindByInstanceID(instanceID);
                    if (go == null)
                        return Tool_GameObject.Error.NotFoundGameObjectWithInstanceID(instanceID);

                    prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                }

                if (string.IsNullOrEmpty(prefabAssetPath))
                    return Error.PrefabPathIsEmpty();

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
                if (prefab == null)
                    return Error.NotFoundPrefabAtPath(prefabAssetPath);

                var components = prefab.GetComponents<UnityEngine.Component>();
                var componentsPreview = components
                    .Select((c, i) => Reflector.Instance.Serialize(
                        c,
                        name: $"[{i}]",
                        recursive: false,
                        logger: McpPlugin.Instance.Logger
                    ))
                    .ToList();

                return @$"[Success] Found Prefab at '{prefabAssetPath}'.
# Components preview:
{JsonUtils.Serialize(componentsPreview)}

# GameObject bounds:
{JsonUtils.Serialize(prefab.CalculateBounds())}

# GameObject information:
{prefab.ToMetadata(includeChildrenDepth).Print()}";
            });
        }

        private string InstantiatePrefab(string? prefabAssetPath, string? gameObjectPath, Vector3? position, Vector3? rotation, Vector3? scale, bool isLocalSpace)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(prefabAssetPath))
                    return Error.PrefabPathIsEmpty();

                if (string.IsNullOrEmpty(gameObjectPath))
                    return "[Error] GameObject path is required for instantiate operation.";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
                if (prefab == null)
                    return Error.NotFoundPrefabAtPath(prefabAssetPath);

                var parentGo = default(GameObject);
                if (StringUtils.Path_ParseParent(gameObjectPath, out var parentPath, out var name))
                {
                    parentGo = GameObjectUtils.FindByPath(parentPath);
                    if (parentGo == null)
                        return Tool_GameObject.Error.NotFoundGameObjectAtPath(parentPath);
                }

                var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                go.name = name ?? prefab.name;
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, false);
                go.SetTransform(position, rotation, scale, isLocalSpace);

                var bounds = go.CalculateBounds();

                EditorUtility.SetDirty(go);
                EditorApplication.RepaintHierarchyWindow();

                return $"[Success] Prefab successfully instantiated.\n{go.Print()}";
            });
        }
    }
} 