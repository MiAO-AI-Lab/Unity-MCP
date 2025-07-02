#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_Skeleton_GetHierarchy",
            Title = "Get Skeleton Hierarchy (GameObject & Assets)"
        )]
        [Description(@"Get skeleton hierarchy from both GameObjects and Assets including:
- GameObjects in current scene with SkinnedMeshRenderer components
- Prefab assets and model files from project
- Returns hierarchical bone structure as formatted string
- Supports multiple input methods: GameObject reference, asset path, or asset name")]
        public string GetSkeletonHierarchy
        (
            [Description("GameObject path in hierarchy (e.g., 'Root/Character/Body')")]
            string? gameObjectPath = null,
            [Description("GameObject name to search for in scene")]
            string? gameObjectName = null,
            [Description("GameObject instance ID in scene")]
            int gameObjectInstanceID = 0,
            [Description("Asset path starting with 'Assets/' or asset name to search for")]
            string? assetPathOrName = null,
            [Description("Asset GUID (alternative to assetPathOrName)")]
            string? assetGuid = null,
            [Description("Include bone transforms detailed information (position, rotation, scale)")]
            bool includeTransformDetails = false,
            [Description("Maximum depth of bone hierarchy to display (-1 for unlimited)")]
            int maxDepth = -1
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    GameObject targetGameObject = null;
                    string sourceType = "";
                    string sourceName = "";

                    // Priority 1: Asset-based search
                    if (!string.IsNullOrEmpty(assetPathOrName) || !string.IsNullOrEmpty(assetGuid))
                    {
                        var result = LoadGameObjectFromAsset(assetPathOrName, assetGuid);
                        if (result.success)
                        {
                            targetGameObject = result.gameObject;
                            sourceType = "Asset";
                            sourceName = result.assetPath;
                        }
                        else
                        {
                            return result.errorMessage;
                        }
                    }
                    // Priority 2: Scene GameObject search
                    else if (gameObjectInstanceID != 0)
                    {
                        targetGameObject = GameObjectUtils.FindByInstanceID(gameObjectInstanceID);
                        if (targetGameObject == null)
                            return Error.NotFoundGameObjectWithInstanceID(gameObjectInstanceID);
                        sourceType = "Scene GameObject";
                        sourceName = targetGameObject.name;
                    }
                    else if (!string.IsNullOrEmpty(gameObjectPath))
                    {
                        targetGameObject = GameObjectUtils.FindByPath(gameObjectPath);
                        if (targetGameObject == null)
                            return Error.NotFoundGameObjectAtPath(gameObjectPath);
                        sourceType = "Scene GameObject";
                        sourceName = gameObjectPath;
                    }
                    else if (!string.IsNullOrEmpty(gameObjectName))
                    {
                        targetGameObject = GameObject.Find(gameObjectName);
                        if (targetGameObject == null)
                            return Error.NotFoundGameObjectWithName(gameObjectName);
                        sourceType = "Scene GameObject";
                        sourceName = gameObjectName;
                    }
                    else
                    {
                        return "[Error] Please provide either:\n" +
                               "- Asset info: assetPathOrName or assetGuid\n" +
                               "- Scene GameObject info: gameObjectPath, gameObjectName, or gameObjectInstanceID";
                    }

                    // Extract skeleton hierarchy
                    var skeletonInfo = ExtractSkeletonHierarchy(targetGameObject, includeTransformDetails, maxDepth, sourceType, sourceName);
                    
                    if (string.IsNullOrEmpty(skeletonInfo))
                        return $"[Warning] No skeleton data found in {sourceType} '{sourceName}'. Target does not contain SkinnedMeshRenderer components with bone hierarchy.";

                    return $"[Success] Skeleton hierarchy extracted from {sourceType} '{sourceName}':\n\n{skeletonInfo}";
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to extract skeleton hierarchy: {ex.Message}";
                }
            });
        }

        private (bool success, GameObject gameObject, string assetPath, string errorMessage) LoadGameObjectFromAsset(string assetPathOrName, string assetGuid)
        {
            try
            {
                string assetPath = string.Empty;
                
                // If GUID is provided, convert to path
                if (!string.IsNullOrEmpty(assetGuid))
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                }
                // If path is provided directly
                else if (!string.IsNullOrEmpty(assetPathOrName) && assetPathOrName.StartsWith("Assets/"))
                {
                    assetPath = assetPathOrName;
                }
                // If it's a name, search for it
                else if (!string.IsNullOrEmpty(assetPathOrName))
                {
                    assetPath = FindAssetByName(assetPathOrName);
                    if (string.IsNullOrEmpty(assetPath))
                        return (false, null, "", $"[Error] Asset with name '{assetPathOrName}' not found in project.");
                }

                if (string.IsNullOrEmpty(assetPath))
                    return (false, null, "", $"[Error] Asset not found. Path: '{assetPathOrName}'. GUID: '{assetGuid}'.");

                // Load the asset
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                    return (false, null, "", $"[Error] Failed to load asset at path '{assetPath}'.");

                // Try to load as GameObject (for prefabs)
                var gameObject = asset as GameObject;
                if (gameObject == null)
                {
                    // Try to load main asset if it's a model file
                    gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                }

                if (gameObject == null)
                    return (false, null, "", $"[Error] Asset at path '{assetPath}' is not a GameObject or does not contain skeleton data.");

                return (true, gameObject, assetPath, "");
            }
            catch (Exception ex)
            {
                return (false, null, "", $"[Error] Failed to load asset: {ex.Message}");
            }
        }

        private string FindAssetByName(string assetName)
        {
            // Search for assets with the given name
            var guids = AssetDatabase.FindAssets($"{assetName} t:GameObject");
            if (guids.Length == 0)
            {
                // Also search for model files
                guids = AssetDatabase.FindAssets($"{assetName} t:Model");
            }

            if (guids.Length > 0)
            {
                // Return first match
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            return string.Empty;
        }

        private string ExtractSkeletonHierarchy(GameObject gameObject, bool includeTransformDetails, int maxDepth, string sourceType, string sourceName)
        {
            var stringBuilder = new StringBuilder();
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            if (skinnedMeshRenderers.Length == 0)
            {
                return string.Empty;
            }

            stringBuilder.AppendLine("=== SKELETON HIERARCHY ===");
            stringBuilder.AppendLine($"Source: {sourceType} - '{sourceName}'");
            stringBuilder.AppendLine($"Target GameObject: '{gameObject.name}'");
            stringBuilder.AppendLine($"Found {skinnedMeshRenderers.Length} SkinnedMeshRenderer(s)");
            stringBuilder.AppendLine();

            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                var smr = skinnedMeshRenderers[i];
                stringBuilder.AppendLine($"[{i + 1}] SkinnedMeshRenderer: '{smr.name}'");
                stringBuilder.AppendLine($"    GameObject Path: '{GetGameObjectPath(smr.gameObject)}'");
                
                if (smr.bones != null && smr.bones.Length > 0)
                {
                    stringBuilder.AppendLine($"    Root Bone: {(smr.rootBone != null ? smr.rootBone.name : "None")}");
                    stringBuilder.AppendLine($"    Total Bones: {smr.bones.Length}");
                    stringBuilder.AppendLine();

                    // Build bone hierarchy
                    var boneHierarchy = BuildBoneHierarchy(smr.bones, smr.rootBone);
                    var formattedHierarchy = FormatBoneHierarchy(boneHierarchy, includeTransformDetails, maxDepth);
                    stringBuilder.AppendLine(formattedHierarchy);
                }
                else
                {
                    stringBuilder.AppendLine("    No bones found.");
                }
                
                if (i < skinnedMeshRenderers.Length - 1)
                    stringBuilder.AppendLine();
            }

            return stringBuilder.ToString();
        }

        private string GetGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null) return "";
            
            var path = gameObject.name;
            var parent = gameObject.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        private Dictionary<Transform, List<Transform>> BuildBoneHierarchy(Transform[] bones, Transform rootBone)
        {
            var hierarchy = new Dictionary<Transform, List<Transform>>();
            var boneSet = new HashSet<Transform>(bones);

            // Initialize hierarchy dictionary
            foreach (var bone in bones)
            {
                if (bone != null)
                    hierarchy[bone] = new List<Transform>();
            }

            // Build parent-child relationships
            foreach (var bone in bones)
            {
                if (bone != null && bone.parent != null && boneSet.Contains(bone.parent))
                {
                    if (hierarchy.ContainsKey(bone.parent))
                        hierarchy[bone.parent].Add(bone);
                }
            }

            return hierarchy;
        }

        private string FormatBoneHierarchy(Dictionary<Transform, List<Transform>> hierarchy, bool includeTransformDetails, int maxDepth)
        {
            var stringBuilder = new StringBuilder();
            var visited = new HashSet<Transform>();

            // Find root bones (bones without parents in the bone set)
            var rootBones = new List<Transform>();
            foreach (var bone in hierarchy.Keys)
            {
                if (bone.parent == null || !hierarchy.ContainsKey(bone.parent))
                    rootBones.Add(bone);
            }

            // Sort root bones by name for consistent output
            rootBones.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            // Format each root bone and its children
            foreach (var rootBone in rootBones)
            {
                FormatBoneRecursive(rootBone, hierarchy, stringBuilder, visited, 0, includeTransformDetails, maxDepth);
            }

            return stringBuilder.ToString();
        }

        private void FormatBoneRecursive(Transform bone, Dictionary<Transform, List<Transform>> hierarchy, 
            StringBuilder stringBuilder, HashSet<Transform> visited, int depth, bool includeTransformDetails, int maxDepth)
        {
            if (bone == null || visited.Contains(bone) || (maxDepth >= 0 && depth > maxDepth))
                return;

            visited.Add(bone);

            // Create indentation based on depth
            var indent = new string(' ', depth * 2);
            var connector = depth > 0 ? "├─ " : "";

            stringBuilder.Append($"{indent}{connector}{bone.name}");

            if (includeTransformDetails)
            {
                var pos = bone.localPosition;
                var rot = bone.localEulerAngles;
                var scale = bone.localScale;
                stringBuilder.Append($" [Pos: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}), ");
                stringBuilder.Append($"Rot: ({rot.x:F1}, {rot.y:F1}, {rot.z:F1}), ");
                stringBuilder.Append($"Scale: ({scale.x:F2}, {scale.y:F2}, {scale.z:F2})]");
            }

            stringBuilder.AppendLine();

            // Recursively format children
            if (hierarchy.ContainsKey(bone))
            {
                var children = hierarchy[bone];
                // Sort children by name for consistent output
                children.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
                
                foreach (var child in children)
                {
                    FormatBoneRecursive(child, hierarchy, stringBuilder, visited, depth + 1, includeTransformDetails, maxDepth);
                }
            }
        }
    }
} 