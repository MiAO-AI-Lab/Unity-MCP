#pragma warning disable CS8632
using System.ComponentModel;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using UnityEngine;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_ScriptableObject
    {
        [McpPluginTool
        (
            "ScriptableObject_Manage",
            Title = "Manage ScriptableObjects - Create, Delete, Find, Modify"
        )]
        [Description(@"Manage comprehensive ScriptableObject operations including:
- create: Create new ScriptableObject asset with default parameters
- delete: Remove ScriptableObject asset from the project
- find: Locate ScriptableObject asset by path and return its data
- modify: Update ScriptableObject asset's fields and properties")]
        public string Management
        (
            [Description("Operation type: 'create', 'delete', 'find', 'modify'")]
            string operation,
            [Description("Asset path. Starts with 'Assets/'. Ends with '.asset'.")]
            string assetPath,
            [Description("For create: Full name of the ScriptableObject type. Should include full namespace path and class name.")]
            string? typeName = null,
            [Description("For find: If true, it will print only brief data.")]
            bool briefData = false,
            [Description("For modify: Asset modification data")]
            SerializedMember? assetDiff = null
        )
        {
            return operation.ToLower() switch
            {
                "create" => CreateScriptableObject(assetPath, typeName),
                "delete" => DeleteScriptableObject(assetPath),
                "find" => FindScriptableObject(assetPath, briefData),
                "modify" => ModifyScriptableObject(assetPath, assetDiff),
                _ => "[Error] Invalid operation. Valid operations: 'create', 'delete', 'find', 'modify'"
            };
        }

        private string CreateScriptableObject(string assetPath, string? typeName)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                if (string.IsNullOrEmpty(typeName))
                    return "[Error] Type name is required for create operation.";

                var type = TypeUtils.GetType(typeName);
                if (type == null)
                    return Error.TypeNotFound(typeName);

                if (!typeof(ScriptableObject).IsAssignableFrom(type))
                    return Error.TypeNotScriptableObject(typeName);

                var instance = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return $"[Success] Created ScriptableObject asset at '{assetPath}'";
            });
        }

        private string DeleteScriptableObject(string assetPath)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                    return Error.AssetNotFound(assetPath);

                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();

                return $"[Success] Deleted ScriptableObject asset at '{assetPath}'";
            });
        }

        private string FindScriptableObject(string assetPath, bool briefData)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                    return Error.AssetNotFound(assetPath);

                var serializedAsset = Reflector.Instance.Serialize(
                    asset,
                    name: asset.name,
                    recursive: !briefData,
                    logger: McpPlugin.Instance.Logger
                );

                return @$"[Success] Found ScriptableObject asset.
# Data:
```json
{JsonUtils.Serialize(serializedAsset)}
```";
            });
        }

        private string ModifyScriptableObject(string assetPath, SerializedMember? assetDiff)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.AssetPathIsEmpty();

                if (assetDiff == null)
                    return "[Error] Asset modification data is required for modify operation.";

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                    return Error.AssetNotFound(assetPath);

                var stringBuilder = new StringBuilder();
                var objToModify = (object)asset;
                Reflector.Instance.Populate(ref objToModify, assetDiff, stringBuilder);

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return $"[Success] Modified ScriptableObject asset at '{assetPath}'.\n{stringBuilder}";
            });
        }
    }
} 