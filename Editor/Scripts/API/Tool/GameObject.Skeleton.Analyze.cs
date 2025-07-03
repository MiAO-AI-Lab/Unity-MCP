#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_Skeleton_Analyze",
            Title = "Comprehensive Skeleton Analysis Tool"
        )]
        [Description(@"Comprehensive skeleton analysis tool that combines multiple analysis capabilities:
- getHierarchy: Extract detailed bone hierarchy structure with transform information
- getReferences: Analyze bone references, dependencies, and usage patterns
- standardizeNaming: Analyze and standardize bone naming conventions
- detectNamingSource: Detect the source DCC software from bone naming patterns
- all: Perform all analyses and generate comprehensive report")]
        public string AnalyzeSkeleton
        (
            [Description("Analysis operation type: 'getHierarchy', 'getReferences', 'standardizeNaming', 'detectNamingSource', or 'all'")]
            string operation = "all",
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
            [Description("Include detailed transform information (position, rotation, scale) - for getHierarchy")]
            bool includeTransformDetails = false,
            [Description("Maximum depth of bone hierarchy to display (-1 for unlimited) - for getHierarchy")]
            int maxDepth = -1,
            [Description("Include detailed reference information for each bone - for getReferences")]
            bool includeDetailedReferences = true,
            [Description("Analyze animation clips for bone dependencies - for getReferences")]
            bool analyzeAnimationClips = true,
            [Description("Include detailed mapping suggestions for each bone - for standardizeNaming")]
            bool includeDetailedSuggestions = true,
            [Description("Show only bones with issues or optimization opportunities - for getReferences and standardizeNaming")]
            bool showOnlyIssues = false,
            [Description("Maximum analysis depth (-1 for unlimited) - for getReferences")]
            int maxAnalysisDepth = -1
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    // Load target GameObject
                    var loadResult = LoadTargetGameObject(gameObjectPath, gameObjectName, gameObjectInstanceID, 
                        assetPathOrName, assetGuid);
                    if (!loadResult.success)
                        return $"[Error] {loadResult.errorMessage}";

                    var targetGameObject = loadResult.gameObject;
                    var sourceType = loadResult.sourceType;
                    var sourceName = loadResult.sourceName;

                    // Check if GameObject has skeleton data
                    var skinnedMeshRenderers = targetGameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                    if (skinnedMeshRenderers.Length == 0)
                    {
                        return $"[Warning] No skeleton data found in {sourceType} '{sourceName}'. Target does not contain SkinnedMeshRenderer components.";
                    }

                    var sb = new StringBuilder();
                    
                    switch (operation.ToLower())
                    {
                        case "gethierarchy":
                            return GetSkeletonHierarchyInternal(targetGameObject, sourceType, sourceName, 
                                includeTransformDetails, maxDepth);
                        
                        case "getreferences":
                            return GetSkeletonReferencesInternal(targetGameObject, sourceType, sourceName, 
                                includeDetailedReferences, analyzeAnimationClips, showOnlyIssues, maxAnalysisDepth);
                        
                        case "standardizenaming":
                            return AnalyzeBoneNamingInternal(targetGameObject, sourceType, sourceName, 
                                includeDetailedSuggestions, showOnlyIssues);
                        
                        case "detectnamingsource":
                            return DetectBoneNamingSourceInternal(targetGameObject, sourceType, sourceName);
                        
                        case "all":
                            return PerformComprehensiveAnalysis(targetGameObject, sourceType, sourceName,
                                includeTransformDetails, maxDepth, includeDetailedReferences, analyzeAnimationClips,
                                includeDetailedSuggestions, showOnlyIssues, maxAnalysisDepth);
                        
                        default:
                            return $"[Error] Invalid operation '{operation}'. Valid operations: 'getHierarchy', 'getReferences', 'standardizeNaming', 'detectNamingSource', 'all'";
                    }
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to analyze skeleton: {ex.Message}";
                }
            });
        }

        private string PerformComprehensiveAnalysis(GameObject gameObject, string sourceType, string sourceName,
            bool includeTransformDetails, int maxDepth, bool includeDetailedReferences, bool analyzeAnimationClips,
            bool includeDetailedSuggestions, bool showOnlyIssues, int maxAnalysisDepth)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== COMPREHENSIVE SKELETON ANALYSIS ===");
            sb.AppendLine($"Source: {sourceType} - '{sourceName}'");
            sb.AppendLine($"Target GameObject: '{gameObject.name}'");
            sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 1. Hierarchy Analysis
            sb.AppendLine("🏗️ ===== SKELETON HIERARCHY ANALYSIS =====");
            var hierarchyResult = GetSkeletonHierarchyInternal(gameObject, sourceType, sourceName, 
                includeTransformDetails, maxDepth);
            sb.AppendLine(hierarchyResult.Replace($"[Success] Skeleton hierarchy extracted from {sourceType} '{sourceName}':\n\n", ""));
            sb.AppendLine();

            // 2. Naming Source Detection
            sb.AppendLine("🔍 ===== NAMING SOURCE DETECTION =====");
            var namingSourceResult = DetectBoneNamingSourceInternal(gameObject, sourceType, sourceName);
            sb.AppendLine(namingSourceResult.Replace("=== BONE NAMING SOURCE DETECTION ===", "")
                .Replace($"Source: {sourceType} - '{sourceName}'", "")
                .Replace($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "").Trim());
            sb.AppendLine();

            // 3. Naming Standardization Analysis
            sb.AppendLine("📏 ===== NAMING STANDARDIZATION ANALYSIS =====");
            var namingAnalysisResult = AnalyzeBoneNamingInternal(gameObject, sourceType, sourceName, 
                includeDetailedSuggestions, showOnlyIssues);
            sb.AppendLine(namingAnalysisResult.Replace("=== BONE NAMING ANALYSIS REPORT ===", "")
                .Replace($"Source: {sourceType} - '{sourceName}'", "")
                .Replace($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "").Trim());
            sb.AppendLine();

            // 4. References Analysis
            sb.AppendLine("🔗 ===== BONE REFERENCES ANALYSIS =====");
            var referencesResult = GetSkeletonReferencesInternal(gameObject, sourceType, sourceName, 
                includeDetailedReferences, analyzeAnimationClips, showOnlyIssues, maxAnalysisDepth);
            sb.AppendLine(referencesResult.Replace("=== BONE REFERENCE ANALYSIS REPORT ===", "")
                .Replace($"Source: {sourceType} - '{sourceName}'", "")
                .Replace($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "").Trim());
            sb.AppendLine();

            // 5. Overall Summary and Recommendations
            sb.AppendLine("📊 ===== OVERALL SUMMARY =====");
            GenerateOverallSummary(sb, gameObject);

            return sb.ToString();
        }

        private void GenerateOverallSummary(StringBuilder sb, GameObject gameObject)
        {
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            var totalBones = 0;
            var totalVertices = 0;
            
            foreach (var smr in skinnedMeshRenderers)
            {
                if (smr.bones != null)
                    totalBones += smr.bones.Length;
                if (smr.sharedMesh != null)
                    totalVertices += smr.sharedMesh.vertexCount;
            }

            sb.AppendLine($"├─ Total SkinnedMeshRenderers: {skinnedMeshRenderers.Length}");
            sb.AppendLine($"├─ Total Bones: {totalBones}");
            sb.AppendLine($"├─ Total Vertices: {totalVertices}");
            sb.AppendLine();

            sb.AppendLine("🎯 FINAL RECOMMENDATIONS:");
            sb.AppendLine("├─ Review naming standardization suggestions for better Unity Humanoid compatibility");
            sb.AppendLine("├─ Address any bone reference issues to optimize performance");
            sb.AppendLine("├─ Consider bone hierarchy optimization if performance is critical");
            sb.AppendLine("└─ Verify all critical bones are properly mapped for animation systems");
        }

        private string GetSkeletonHierarchyInternal(GameObject gameObject, string sourceType, string sourceName, 
            bool includeTransformDetails, int maxDepth)
        {
            var skeletonInfo = ExtractSkeletonHierarchy(gameObject, includeTransformDetails, maxDepth, sourceType, sourceName);
            
            if (string.IsNullOrEmpty(skeletonInfo))
                return $"[Warning] No skeleton data found in {sourceType} '{sourceName}'. Target does not contain SkinnedMeshRenderer components with bone hierarchy.";

            return $"[Success] Skeleton hierarchy extracted from {sourceType} '{sourceName}':\n\n{skeletonInfo}";
        }

        private string GetSkeletonReferencesInternal(GameObject gameObject, string sourceType, string sourceName,
            bool includeDetailedReferences, bool analyzeAnimationClips, bool showOnlyIssues, int maxAnalysisDepth)
        {
            var analyzer = new BoneReferenceAnalyzer();
            var result = analyzer.AnalyzeBoneReferences(
                gameObject, 
                includeDetailedReferences, 
                analyzeAnimationClips, 
                showOnlyIssues,
                maxAnalysisDepth
            );
            
            return FormatReferenceReport(result, sourceType, sourceName);
        }

        private string AnalyzeBoneNamingInternal(GameObject gameObject, string sourceType, string sourceName,
            bool includeDetailedSuggestions, bool showOnlyIssues)
        {
            var analyzer = new BoneNamingAnalyzer();
            var analysisResult = analyzer.AnalyzeBoneNaming(gameObject, includeDetailedSuggestions, showOnlyIssues);
            
            if (analysisResult == null)
                return $"[Warning] No skeleton data found in {sourceType} '{sourceName}'. Target does not contain SkinnedMeshRenderer components.";

            return FormatAnalysisReport(analysisResult, sourceType, sourceName);
        }

        private string DetectBoneNamingSourceInternal(GameObject gameObject, string sourceType, string sourceName)
        {
            var detector = new BoneNamingSourceDetector();
            var bones = GetAllBones(gameObject);
            
            if (bones.Length == 0)
                return $"[Warning] No bones found in target GameObject.";

            var detectionResult = detector.DetectNamingSource(bones);
            return FormatDetectionReport(detectionResult, sourceType, sourceName);
        }

        private (bool success, GameObject gameObject, string sourceType, string sourceName, string errorMessage) LoadTargetGameObject(
            string gameObjectPath, string gameObjectName, int gameObjectInstanceID, string assetPathOrName, string assetGuid)
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
                    return (false, null, "", "", result.errorMessage);
                }
            }
            // Priority 2: Scene GameObject search
            else if (gameObjectInstanceID != 0)
            {
                targetGameObject = GameObjectUtils.FindByInstanceID(gameObjectInstanceID);
                if (targetGameObject == null)
                    return (false, null, "", "", Error.NotFoundGameObjectWithInstanceID(gameObjectInstanceID));
                sourceType = "Scene GameObject";
                sourceName = targetGameObject.name;
            }
            else if (!string.IsNullOrEmpty(gameObjectPath))
            {
                targetGameObject = GameObjectUtils.FindByPath(gameObjectPath);
                if (targetGameObject == null)
                    return (false, null, "", "", Error.NotFoundGameObjectAtPath(gameObjectPath));
                sourceType = "Scene GameObject";
                sourceName = gameObjectPath;
            }
            else if (!string.IsNullOrEmpty(gameObjectName))
            {
                targetGameObject = GameObject.Find(gameObjectName);
                if (targetGameObject == null)
                    return (false, null, "", "", Error.NotFoundGameObjectWithName(gameObjectName));
                sourceType = "Scene GameObject";
                sourceName = gameObjectName;
            }
            else
            {
                return (false, null, "", "", "[Error] Please provide either:\n" +
                       "- Asset info: assetPathOrName or assetGuid\n" +
                       "- Scene GameObject info: gameObjectPath, gameObjectName, or gameObjectInstanceID");
            }

            return (true, targetGameObject, sourceType, sourceName, "");
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

        private Transform[] GetAllBones(GameObject gameObject)
        {
            var bones = new List<Transform>();
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var smr in skinnedMeshRenderers)
            {
                if (smr.bones != null && smr.bones.Length > 0)
                {
                    bones.AddRange(smr.bones.Where(b => b != null));
                }
            }
            
            return bones.Distinct().ToArray();
        }

        private string FormatReferenceReport(BoneReferenceAnalysisResult result, string sourceType, string sourceName)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== BONE REFERENCE ANALYSIS REPORT ===");
            sb.AppendLine($"Source: {sourceType} - '{sourceName}'");
            sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // Summary
            sb.AppendLine("📊 ANALYSIS SUMMARY");
            sb.AppendLine($"├─ Total Bones Found: {result.TotalBonesCount}");
            sb.AppendLine($"├─ Referenced Bones: {result.ReferencedBonesCount}");
            sb.AppendLine($"├─ Unused Bones: {result.UnusedBonesCount}");
            sb.AppendLine($"├─ SkinnedMeshRenderers: {result.SkinnedMeshRenderersCount}");
            sb.AppendLine($"└─ Issues Found: {result.IssuesCount}");
            sb.AppendLine();
            
            // SkinnedMeshRenderer Analysis
            if (result.SkinnedMeshAnalysis.Count > 0)
            {
                sb.AppendLine("🎭 SKINNEDMESHRENDERER ANALYSIS");
                foreach (var smr in result.SkinnedMeshAnalysis)
                {
                    sb.AppendLine($"├─ {smr.RendererName}");
                    sb.AppendLine($"│  ├─ Bones Used: {smr.BonesUsed}");
                    sb.AppendLine($"│  ├─ Null Bones: {smr.NullBones}");
                    sb.AppendLine($"│  └─ Mesh Vertices: {smr.VertexCount}");
                }
                sb.AppendLine();
            }
            
            // Bone Reference Details
            if (result.BoneReferences.Count > 0 && !result.ShowOnlyIssues)
            {
                sb.AppendLine("🔗 BONE REFERENCE DETAILS");
                foreach (var bone in result.BoneReferences.OrderBy(b => b.BoneName))
                {
                    sb.AppendLine($"├─ {bone.BoneName}");
                    sb.AppendLine($"│  ├─ References: {bone.ReferenceCount}");
                    sb.AppendLine($"│  ├─ Used in SMR: {bone.UsedInSkinnedMeshRenderer}");
                    sb.AppendLine($"│  └─ Has Children: {bone.HasChildren}");
                }
                sb.AppendLine();
            }
            
            // Issues and Unused Bones
            if (result.Issues.Count > 0)
            {
                sb.AppendLine("⚠️ ISSUES AND UNUSED BONES");
                foreach (var issue in result.Issues)
                {
                    sb.AppendLine($"├─ {issue.Type}: {issue.Description}");
                    if (issue.AffectedBones.Count > 0)
                    {
                        foreach (var bone in issue.AffectedBones.Take(5))
                        {
                            sb.AppendLine($"│  └─ {bone}");
                        }
                        if (issue.AffectedBones.Count > 5)
                        {
                            sb.AppendLine($"│  └─ ... and {issue.AffectedBones.Count - 5} more");
                        }
                    }
                    sb.AppendLine();
                }
            }
            
            // Optimization Recommendations
            sb.AppendLine("💡 OPTIMIZATION RECOMMENDATIONS");
            foreach (var recommendation in result.OptimizationRecommendations)
            {
                sb.AppendLine($"├─ {recommendation}");
            }
            
            return sb.ToString();
        }

        private string FormatAnalysisReport(BoneNamingAnalysisResult result, string sourceType, string sourceName)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== BONE NAMING ANALYSIS REPORT ===");
            sb.AppendLine($"Source: {sourceType} - '{sourceName}'");
            sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // Detection Summary
            sb.AppendLine("📊 DETECTION SUMMARY");
            sb.AppendLine($"├─ Detected Naming Source: {result.DetectedSource} (Confidence: {result.SourceConfidence:P1})");
            sb.AppendLine($"├─ Total Bones Found: {result.TotalBonesCount}");
            sb.AppendLine($"├─ Successfully Mapped: {result.MappedBonesCount}");
            sb.AppendLine($"├─ Unmapped Bones: {result.UnmappedBonesCount}");
            sb.AppendLine($"└─ Issues Found: {result.IssuesCount}");
            sb.AppendLine();
            
            // Naming Convention Analysis
            sb.AppendLine("🔍 NAMING CONVENTION ANALYSIS");
            foreach (var pattern in result.DetectedPatterns)
            {
                sb.AppendLine($"├─ {pattern.Name}: {pattern.MatchCount} matches (Confidence: {pattern.Confidence:P1})");
            }
            sb.AppendLine();
            
            // Mapping Results
            if (result.MappedBones.Count > 0 && !result.ShowOnlyIssues)
            {
                sb.AppendLine("✅ SUCCESSFUL MAPPINGS");
                foreach (var mapping in result.MappedBones.OrderBy(m => m.StandardBoneType.ToString()))
                {
                    sb.AppendLine($"├─ {mapping.OriginalName} → {mapping.StandardBoneType} (Confidence: {mapping.Confidence:P1})");
                }
                sb.AppendLine();
            }
            
            // Issues and Recommendations
            if (result.Issues.Count > 0)
            {
                sb.AppendLine("⚠️ ISSUES AND RECOMMENDATIONS");
                foreach (var issue in result.Issues)
                {
                    sb.AppendLine($"├─ {issue.Type}: {issue.Description}");
                    if (issue.Suggestions.Count > 0)
                    {
                        foreach (var suggestion in issue.Suggestions)
                        {
                            sb.AppendLine($"│  └─ 💡 {suggestion}");
                        }
                    }
                    sb.AppendLine();
                }
            }
            
            // Unmapped Bones
            if (result.UnmappedBones.Count > 0)
            {
                sb.AppendLine("❓ UNMAPPED BONES");
                foreach (var bone in result.UnmappedBones)
                {
                    sb.AppendLine($"├─ '{bone}' - Requires manual classification");
                }
                sb.AppendLine();
            }
            
            // Overall Recommendations
            sb.AppendLine("🎯 OVERALL RECOMMENDATIONS");
            foreach (var recommendation in result.OverallRecommendations)
            {
                sb.AppendLine($"├─ {recommendation}");
            }
            
            return sb.ToString();
        }

        private string FormatDetectionReport(NamingSourceDetectionResult result, string sourceType, string sourceName)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== BONE NAMING SOURCE DETECTION ===");
            sb.AppendLine($"Source: {sourceType} - '{sourceName}'");
            sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            sb.AppendLine("🔍 DETECTION RESULTS");
            sb.AppendLine($"├─ Primary Source: {result.PrimarySource} (Confidence: {result.PrimaryConfidence:P1})");
            sb.AppendLine($"├─ Total Bones Analyzed: {result.TotalBonesAnalyzed}");
            sb.AppendLine();
            
            sb.AppendLine("📊 SOURCE CONFIDENCE SCORES");
            foreach (var score in result.SourceConfidences.OrderByDescending(s => s.Value))
            {
                var percentage = score.Value;
                var bar = new string('█', (int)(percentage * 20));
                sb.AppendLine($"├─ {score.Key,-12}: {percentage,6:P1} {bar}");
            }
            sb.AppendLine();
            
            sb.AppendLine("🎨 DETECTED PATTERNS");
            foreach (var pattern in result.DetectedPatterns)
            {
                sb.AppendLine($"├─ {pattern.Name}: {pattern.Examples.Count} examples");
                foreach (var example in pattern.Examples.Take(3))
                {
                    sb.AppendLine($"│  └─ '{example}'");
                }
                if (pattern.Examples.Count > 3)
                {
                    sb.AppendLine($"│  └─ ... and {pattern.Examples.Count - 3} more");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("💡 RECOMMENDATIONS");
            foreach (var recommendation in result.Recommendations)
            {
                sb.AppendLine($"├─ {recommendation}");
            }
            
            return sb.ToString();
        }
    }

    // Data structures for bone reference analysis
    public class BoneReferenceAnalysisResult
    {
        public int TotalBonesCount { get; set; }
        public int ReferencedBonesCount { get; set; }
        public int UnusedBonesCount { get; set; }
        public int SkinnedMeshRenderersCount { get; set; }
        public int IssuesCount { get; set; }
        public bool ShowOnlyIssues { get; set; }
        
        public List<BoneReferenceInfo> BoneReferences { get; set; } = new List<BoneReferenceInfo>();
        public List<SkinnedMeshRendererInfo> SkinnedMeshAnalysis { get; set; } = new List<SkinnedMeshRendererInfo>();
        public List<BoneIssue> Issues { get; set; } = new List<BoneIssue>();
        public List<string> OptimizationRecommendations { get; set; } = new List<string>();
    }

    public class BoneReferenceInfo
    {
        public string BoneName { get; set; } = "";
        public Transform BoneTransform { get; set; }
        public int ReferenceCount { get; set; }
        public bool UsedInSkinnedMeshRenderer { get; set; }
        public bool HasChildren { get; set; }
    }

    public class SkinnedMeshRendererInfo
    {
        public string RendererName { get; set; } = "";
        public int BonesUsed { get; set; }
        public int NullBones { get; set; }
        public int VertexCount { get; set; }
        public List<string> BoneNames { get; set; } = new List<string>();
    }

    public class BoneIssue
    {
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> AffectedBones { get; set; } = new List<string>();
        public string Severity { get; set; } = "Medium";
    }

    public class BoneReferenceAnalyzer
    {
        public BoneReferenceAnalysisResult AnalyzeBoneReferences(
            GameObject gameObject, 
            bool includeDetailedReferences, 
            bool analyzeAnimationClips, 
            bool showOnlyIssues,
            int maxDepth)
        {
            var result = new BoneReferenceAnalysisResult
            {
                ShowOnlyIssues = showOnlyIssues
            };

            // Get all bones in the hierarchy
            var allBones = GetAllBonesInHierarchy(gameObject, maxDepth);
            result.TotalBonesCount = allBones.Count;

            // Analyze SkinnedMeshRenderers
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            result.SkinnedMeshRenderersCount = skinnedMeshRenderers.Length;
            
            var referencedBones = new HashSet<Transform>();
            
            foreach (var smr in skinnedMeshRenderers)
            {
                var smrInfo = AnalyzeSkinnedMeshRenderer(smr);
                result.SkinnedMeshAnalysis.Add(smrInfo);
                
                if (smr.bones != null)
                {
                    foreach (var bone in smr.bones)
                    {
                        if (bone != null)
                            referencedBones.Add(bone);
                    }
                }
            }

            // Analyze bone references
            foreach (var bone in allBones)
            {
                var boneInfo = new BoneReferenceInfo
                {
                    BoneName = bone.name,
                    BoneTransform = bone,
                    UsedInSkinnedMeshRenderer = referencedBones.Contains(bone),
                    HasChildren = bone.childCount > 0,
                    ReferenceCount = referencedBones.Contains(bone) ? 1 : 0
                };

                result.BoneReferences.Add(boneInfo);
            }

            // Calculate statistics
            result.ReferencedBonesCount = result.BoneReferences.Count(b => b.ReferenceCount > 0 || b.UsedInSkinnedMeshRenderer);
            result.UnusedBonesCount = result.TotalBonesCount - result.ReferencedBonesCount;

            // Identify issues
            result.Issues = IdentifyBoneIssues(result);
            result.IssuesCount = result.Issues.Count;

            // Generate optimization recommendations
            result.OptimizationRecommendations = GenerateOptimizationRecommendations(result);

            return result;
        }

        private List<Transform> GetAllBonesInHierarchy(GameObject gameObject, int maxDepth)
        {
            var bones = new List<Transform>();
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var smr in skinnedMeshRenderers)
            {
                if (smr.bones != null)
                {
                    bones.AddRange(smr.bones.Where(b => b != null));
                }
            }
            
            // Also include Transform hierarchy if no SkinnedMeshRenderer bones found
            if (bones.Count == 0)
            {
                GetTransformHierarchy(gameObject.transform, bones, 0, maxDepth);
            }
            
            return bones.Distinct().ToList();
        }

        private void GetTransformHierarchy(Transform transform, List<Transform> bones, int currentDepth, int maxDepth)
        {
            if (maxDepth >= 0 && currentDepth > maxDepth)
                return;
                
            bones.Add(transform);
            
            for (int i = 0; i < transform.childCount; i++)
            {
                GetTransformHierarchy(transform.GetChild(i), bones, currentDepth + 1, maxDepth);
            }
        }

        private SkinnedMeshRendererInfo AnalyzeSkinnedMeshRenderer(SkinnedMeshRenderer smr)
        {
            var info = new SkinnedMeshRendererInfo
            {
                RendererName = smr.name,
                VertexCount = smr.sharedMesh?.vertexCount ?? 0
            };

            if (smr.bones != null)
            {
                info.BonesUsed = smr.bones.Count(b => b != null);
                info.NullBones = smr.bones.Count(b => b == null);
                info.BoneNames = smr.bones.Where(b => b != null).Select(b => b.name).ToList();
            }

            return info;
        }

        private List<BoneIssue> IdentifyBoneIssues(BoneReferenceAnalysisResult result)
        {
            var issues = new List<BoneIssue>();
            
            // Find unused bones
            var unusedBones = result.BoneReferences
                .Where(b => !b.UsedInSkinnedMeshRenderer && b.ReferenceCount == 0)
                .Select(b => b.BoneName)
                .ToList();
            
            if (unusedBones.Count > 0)
            {
                issues.Add(new BoneIssue
                {
                    Type = "Unused Bones",
                    Description = $"Found {unusedBones.Count} bones that are not referenced by any component",
                    AffectedBones = unusedBones,
                    Severity = "Low"
                });
            }

            // Find null bone references in SkinnedMeshRenderers
            var nullBoneCount = result.SkinnedMeshAnalysis.Sum(smr => smr.NullBones);
            if (nullBoneCount > 0)
            {
                issues.Add(new BoneIssue
                {
                    Type = "Null Bone References",
                    Description = $"Found {nullBoneCount} null bone references in SkinnedMeshRenderers",
                    Severity = "High"
                });
            }

            return issues;
        }

        private List<string> GenerateOptimizationRecommendations(BoneReferenceAnalysisResult result)
        {
            var recommendations = new List<string>();
            
            if (result.UnusedBonesCount > 0)
            {
                recommendations.Add($"🗑️ Consider removing {result.UnusedBonesCount} unused bones to optimize performance");
            }
            
            var nullBoneCount = result.SkinnedMeshAnalysis.Sum(smr => smr.NullBones);
            if (nullBoneCount > 0)
            {
                recommendations.Add($"⚠️ Fix {nullBoneCount} null bone references in SkinnedMeshRenderers");
            }
            
            if (result.BoneReferences.Count > 100)
            {
                recommendations.Add("📊 Consider bone hierarchy optimization for better performance");
            }
            
            return recommendations;
        }
    }

    // Supporting classes for bone naming analysis
    public enum BoneSource
    {
        Unknown,
        Mixamo,
        Blender,
        MaxBiped,
        Maya,
        Unity,
        Custom
    }

    public enum StandardBoneType
    {
        Hips, Spine, Chest, UpperChest, Neck, Head,
        LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand,
        RightShoulder, RightUpperArm, RightLowerArm, RightHand,
        LeftUpperLeg, LeftLowerLeg, LeftFoot, LeftToes,
        RightUpperLeg, RightLowerLeg, RightFoot, RightToes,
        // Fingers
        LeftThumbProximal, LeftThumbIntermediate, LeftThumbDistal,
        LeftIndexProximal, LeftIndexIntermediate, LeftIndexDistal,
        LeftMiddleProximal, LeftMiddleIntermediate, LeftMiddleDistal,
        LeftRingProximal, LeftRingIntermediate, LeftRingDistal,
        LeftLittleProximal, LeftLittleIntermediate, LeftLittleDistal,
        RightThumbProximal, RightThumbIntermediate, RightThumbDistal,
        RightIndexProximal, RightIndexIntermediate, RightIndexDistal,
        RightMiddleProximal, RightMiddleIntermediate, RightMiddleDistal,
        RightRingProximal, RightRingIntermediate, RightRingDistal,
        RightLittleProximal, RightLittleIntermediate, RightLittleDistal
    }

    public class BoneMappingRule
    {
        public string Pattern { get; }
        public BoneSource Source { get; }
        public float Confidence { get; }
        public bool IsRegex { get; }
        
        public BoneMappingRule(string pattern, BoneSource source, float confidence, bool isRegex = true)
        {
            Pattern = pattern;
            Source = source;
            Confidence = confidence;
            IsRegex = isRegex;
        }
    }

    public class BoneMapping
    {
        public string OriginalName { get; set; }
        public StandardBoneType StandardBoneType { get; set; }
        public float Confidence { get; set; }
        public BoneSource DetectedSource { get; set; }
    }

    public class BoneNamingIssue
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public List<string> Suggestions { get; set; } = new List<string>();
    }

    public class DetectedPattern
    {
        public string Name { get; set; }
        public int MatchCount { get; set; }
        public float Confidence { get; set; }
        public List<string> Examples { get; set; } = new List<string>();
    }

    public class BoneNamingAnalysisResult
    {
        public BoneSource DetectedSource { get; set; }
        public float SourceConfidence { get; set; }
        public int TotalBonesCount { get; set; }
        public int MappedBonesCount { get; set; }
        public int UnmappedBonesCount { get; set; }
        public int IssuesCount { get; set; }
        public bool ShowOnlyIssues { get; set; }
        public List<BoneMapping> MappedBones { get; set; } = new List<BoneMapping>();
        public List<string> UnmappedBones { get; set; } = new List<string>();
        public List<BoneNamingIssue> Issues { get; set; } = new List<BoneNamingIssue>();
        public List<DetectedPattern> DetectedPatterns { get; set; } = new List<DetectedPattern>();
        public List<string> OverallRecommendations { get; set; } = new List<string>();
    }

    public class NamingSourceDetectionResult
    {
        public BoneSource PrimarySource { get; set; }
        public float PrimaryConfidence { get; set; }
        public int TotalBonesAnalyzed { get; set; }
        public Dictionary<BoneSource, float> SourceConfidences { get; set; } = new Dictionary<BoneSource, float>();
        public List<DetectedPattern> DetectedPatterns { get; set; } = new List<DetectedPattern>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    public class BoneNamingAnalyzer
    {
        private readonly Dictionary<StandardBoneType, BoneMappingRule[]> mappingRules;

        public BoneNamingAnalyzer()
        {
            mappingRules = InitializeMappingRules();
        }

        public BoneNamingAnalysisResult AnalyzeBoneNaming(GameObject gameObject, bool includeDetailedSuggestions, bool showOnlyIssues)
        {
            var bones = GetAllBones(gameObject);
            if (bones.Length == 0) return null;

            var result = new BoneNamingAnalysisResult
            {
                TotalBonesCount = bones.Length,
                ShowOnlyIssues = showOnlyIssues
            };

            // Detect naming source
            var detector = new BoneNamingSourceDetector();
            var sourceDetection = detector.DetectNamingSource(bones);
            result.DetectedSource = sourceDetection.PrimarySource;
            result.SourceConfidence = sourceDetection.PrimaryConfidence;
            result.DetectedPatterns = sourceDetection.DetectedPatterns;

            // Map bones to standard types
            var mappedBones = new List<BoneMapping>();
            var unmappedBones = new List<string>();

            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                var mapping = FindBestMapping(bone.name, result.DetectedSource);
                if (mapping != null)
                {
                    mappedBones.Add(mapping);
                }
                else
                {
                    unmappedBones.Add(bone.name);
                }
            }

            result.MappedBones = mappedBones;
            result.UnmappedBones = unmappedBones;
            result.MappedBonesCount = mappedBones.Count;
            result.UnmappedBonesCount = unmappedBones.Count;

            // Analyze issues
            result.Issues = AnalyzeIssues(mappedBones, unmappedBones, result.DetectedSource);
            result.IssuesCount = result.Issues.Count;

            // Generate recommendations
            result.OverallRecommendations = GenerateOverallRecommendations(result);

            return result;
        }

        private Transform[] GetAllBones(GameObject gameObject)
        {
            var bones = new List<Transform>();
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var smr in skinnedMeshRenderers)
            {
                if (smr.bones != null && smr.bones.Length > 0)
                {
                    bones.AddRange(smr.bones.Where(b => b != null));
                }
            }
            
            return bones.Distinct().ToArray();
        }

        private BoneMapping FindBestMapping(string boneName, BoneSource detectedSource)
        {
            BoneMapping bestMapping = null;
            float bestConfidence = 0f;

            foreach (var kvp in mappingRules)
            {
                var boneType = kvp.Key;
                var rules = kvp.Value;

                foreach (var rule in rules)
                {
                    // Source bonus for matching detected source
                    float sourceBonus = rule.Source == detectedSource ? 0.2f : 0f;
                    
                    bool isMatch;
                    if (rule.IsRegex)
                    {
                        try
                        {
                            isMatch = Regex.IsMatch(boneName, rule.Pattern, RegexOptions.IgnoreCase);
                        }
                        catch
                        {
                            isMatch = false;
                        }
                    }
                    else
                    {
                        isMatch = string.Equals(boneName, rule.Pattern, StringComparison.OrdinalIgnoreCase);
                    }

                    if (isMatch)
                    {
                        float totalConfidence = rule.Confidence + sourceBonus;
                        if (totalConfidence > bestConfidence)
                        {
                            bestMapping = new BoneMapping
                            {
                                OriginalName = boneName,
                                StandardBoneType = boneType,
                                Confidence = totalConfidence,
                                DetectedSource = rule.Source
                            };
                            bestConfidence = totalConfidence;
                        }
                    }
                }
            }

            return bestConfidence > 0.5f ? bestMapping : null;
        }

        private List<BoneNamingIssue> AnalyzeIssues(List<BoneMapping> mappedBones, List<string> unmappedBones, BoneSource detectedSource)
        {
            var issues = new List<BoneNamingIssue>();

            // Check for missing critical bones
            var criticalBones = new[]
            {
                StandardBoneType.Hips, StandardBoneType.Spine, StandardBoneType.Head,
                StandardBoneType.LeftUpperArm, StandardBoneType.LeftLowerArm, StandardBoneType.LeftHand,
                StandardBoneType.RightUpperArm, StandardBoneType.RightLowerArm, StandardBoneType.RightHand,
                StandardBoneType.LeftUpperLeg, StandardBoneType.LeftLowerLeg, StandardBoneType.LeftFoot,
                StandardBoneType.RightUpperLeg, StandardBoneType.RightLowerLeg, StandardBoneType.RightFoot
            };

            var mappedTypes = mappedBones.Select(m => m.StandardBoneType).ToHashSet();
            var missingCritical = criticalBones.Where(cb => !mappedTypes.Contains(cb)).ToList();

            if (missingCritical.Count > 0)
            {
                issues.Add(new BoneNamingIssue
                {
                    Type = "Missing Critical Bones",
                    Description = $"Missing {missingCritical.Count} critical bones for humanoid setup",
                    Suggestions = missingCritical.Select(mb => $"Find and map bone for {mb}").ToList()
                });
            }

            // Check for low confidence mappings
            var lowConfidenceMappings = mappedBones.Where(m => m.Confidence < 0.7f).ToList();
            if (lowConfidenceMappings.Count > 0)
            {
                issues.Add(new BoneNamingIssue
                {
                    Type = "Low Confidence Mappings",
                    Description = $"{lowConfidenceMappings.Count} bones mapped with low confidence",
                    Suggestions = lowConfidenceMappings.Select(m => $"Verify mapping: '{m.OriginalName}' → {m.StandardBoneType}").ToList()
                });
            }

            // Check for naming inconsistencies
            if (detectedSource == BoneSource.Unknown)
            {
                issues.Add(new BoneNamingIssue
                {
                    Type = "Unknown Naming Convention",
                    Description = "Unable to clearly identify naming convention source",
                    Suggestions = new List<string>
                    {
                        "Consider standardizing bone names manually",
                        "Check if bones follow a specific naming pattern",
                        "Verify the source DCC software used for rigging"
                    }
                });
            }

            return issues;
        }

        private List<string> GenerateOverallRecommendations(BoneNamingAnalysisResult result)
        {
            var recommendations = new List<string>();

            float mappingSuccessRate = (float)result.MappedBonesCount / result.TotalBonesCount;

            if (mappingSuccessRate >= 0.9f)
            {
                recommendations.Add("✅ Excellent bone naming compliance - ready for humanoid setup");
            }
            else if (mappingSuccessRate >= 0.7f)
            {
                recommendations.Add("⚠️ Good bone mapping with minor issues - review unmapped bones");
            }
            else
            {
                recommendations.Add("❌ Significant naming issues detected - manual standardization recommended");
            }

            if (result.DetectedSource != BoneSource.Unknown)
            {
                recommendations.Add($"🎯 Apply {result.DetectedSource} naming convention standards");
            }

            if (result.UnmappedBonesCount > 0)
            {
                recommendations.Add($"📝 Review {result.UnmappedBonesCount} unmapped bones for manual classification");
            }

            return recommendations;
        }

        private Dictionary<StandardBoneType, BoneMappingRule[]> InitializeMappingRules()
        {
            return new Dictionary<StandardBoneType, BoneMappingRule[]>
            {
                { StandardBoneType.Hips, new[]
                {
                    new BoneMappingRule("mixamorig:Hips", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(pelvis|hips?)$", BoneSource.Blender, 0.9f),
                    new BoneMappingRule(@"^Bip\d+\s+Pelvis$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(Hips|pelvis|root)$", BoneSource.Maya, 0.85f)
                }},
                
                { StandardBoneType.Spine, new[]
                {
                    new BoneMappingRule("mixamorig:Spine", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^spine(\.\d+)?$", BoneSource.Blender, 0.9f),
                    new BoneMappingRule(@"^Bip\d+\s+Spine\d*$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(Spine\d*|spine_\d+)$", BoneSource.Maya, 0.85f)
                }},
                
                { StandardBoneType.Head, new[]
                {
                    new BoneMappingRule("mixamorig:Head", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^head$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+Head$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(Head|head)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.RightHand, new[]
                {
                    new BoneMappingRule("mixamorig:RightHand", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^hand\.R$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+R\s+Hand$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(RightHand|R_hand|hand_R)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.LeftHand, new[]
                {
                    new BoneMappingRule("mixamorig:LeftHand", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^hand\.L$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+L\s+Hand$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(LeftHand|L_hand|hand_L)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.RightUpperArm, new[]
                {
                    new BoneMappingRule("mixamorig:RightArm", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(upper_arm|arm)\.R$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+R\s+UpperArm$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(RightArm|R_arm|arm_R|upperarm_R)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.LeftUpperArm, new[]
                {
                    new BoneMappingRule("mixamorig:LeftArm", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(upper_arm|arm)\.L$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+L\s+UpperArm$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(LeftArm|L_arm|arm_L|upperarm_L)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.RightLowerArm, new[]
                {
                    new BoneMappingRule("mixamorig:RightForeArm", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(forearm|lower_arm)\.R$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+R\s+Forearm$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(RightForeArm|R_forearm|forearm_R|lowerarm_R)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.LeftLowerArm, new[]
                {
                    new BoneMappingRule("mixamorig:LeftForeArm", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(forearm|lower_arm)\.L$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+L\s+Forearm$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(LeftForeArm|L_forearm|forearm_L|lowerarm_L)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.RightUpperLeg, new[]
                {
                    new BoneMappingRule("mixamorig:RightUpLeg", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(thigh|upper_leg)\.R$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+R\s+Thigh$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(RightUpLeg|R_upleg|upleg_R|thigh_R)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.LeftUpperLeg, new[]
                {
                    new BoneMappingRule("mixamorig:LeftUpLeg", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(thigh|upper_leg)\.L$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+L\s+Thigh$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(LeftUpLeg|L_upleg|upleg_L|thigh_L)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.RightLowerLeg, new[]
                {
                    new BoneMappingRule("mixamorig:RightLeg", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(shin|lower_leg|calf)\.R$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+R\s+Calf$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(RightLeg|R_leg|leg_R|shin_R)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.LeftLowerLeg, new[]
                {
                    new BoneMappingRule("mixamorig:LeftLeg", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^(shin|lower_leg|calf)\.L$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+L\s+Calf$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(LeftLeg|L_leg|leg_L|shin_L)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.RightFoot, new[]
                {
                    new BoneMappingRule("mixamorig:RightFoot", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^foot\.R$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+R\s+Foot$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(RightFoot|R_foot|foot_R)$", BoneSource.Maya, 0.9f)
                }},
                
                { StandardBoneType.LeftFoot, new[]
                {
                    new BoneMappingRule("mixamorig:LeftFoot", BoneSource.Mixamo, 1.0f, false),
                    new BoneMappingRule(@"^foot\.L$", BoneSource.Blender, 0.95f),
                    new BoneMappingRule(@"^Bip\d+\s+L\s+Foot$", BoneSource.MaxBiped, 0.95f),
                    new BoneMappingRule(@"^(LeftFoot|L_foot|foot_L)$", BoneSource.Maya, 0.9f)
                }}
            };
        }
    }

    public class BoneNamingSourceDetector
    {
        public NamingSourceDetectionResult DetectNamingSource(Transform[] bones)
        {
            var result = new NamingSourceDetectionResult
            {
                TotalBonesAnalyzed = bones.Length
            };

            var sourceScores = new Dictionary<BoneSource, float>();
            var detectedPatterns = new List<DetectedPattern>();

            // Analyze each bone name
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                var boneName = bone.name;

                // Mixamo detection
                if (boneName.StartsWith("mixamorig:", StringComparison.OrdinalIgnoreCase))
                {
                    sourceScores[BoneSource.Mixamo] = sourceScores.GetValueOrDefault(BoneSource.Mixamo) + 1.0f;
                    AddToPattern(detectedPatterns, "Mixamo Prefix", boneName, BoneSource.Mixamo);
                }

                // Blender detection (.L/.R suffix)
                if (Regex.IsMatch(boneName, @"\.(L|R)$", RegexOptions.IgnoreCase))
                {
                    sourceScores[BoneSource.Blender] = sourceScores.GetValueOrDefault(BoneSource.Blender) + 0.8f;
                    AddToPattern(detectedPatterns, "Blender L/R Suffix", boneName, BoneSource.Blender);
                }

                // 3ds Max Biped detection
                if (Regex.IsMatch(boneName, @"^Bip\d+", RegexOptions.IgnoreCase))
                {
                    sourceScores[BoneSource.MaxBiped] = sourceScores.GetValueOrDefault(BoneSource.MaxBiped) + 1.0f;
                    AddToPattern(detectedPatterns, "3ds Max Biped", boneName, BoneSource.MaxBiped);
                }

                // Maya HumanIK detection
                if (Regex.IsMatch(boneName, @"^(Left|Right)(Arm|Leg|Hand|Foot)", RegexOptions.IgnoreCase))
                {
                    sourceScores[BoneSource.Maya] = sourceScores.GetValueOrDefault(BoneSource.Maya) + 0.7f;
                    AddToPattern(detectedPatterns, "Maya HumanIK", boneName, BoneSource.Maya);
                }

                // Maya custom naming detection
                if (Regex.IsMatch(boneName, @"^(L|R)_\w+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(boneName, @"\w+_(L|R)$", RegexOptions.IgnoreCase))
                {
                    sourceScores[BoneSource.Maya] = sourceScores.GetValueOrDefault(BoneSource.Maya) + 0.6f;
                    AddToPattern(detectedPatterns, "Maya Custom Naming", boneName, BoneSource.Maya);
                }
            }

            // Normalize scores to percentages
            var totalScore = sourceScores.Values.Sum();
            if (totalScore > 0)
            {
                foreach (var key in sourceScores.Keys.ToList())
                {
                    sourceScores[key] = sourceScores[key] / totalScore;
                }
            }

            result.SourceConfidences = sourceScores;
            result.DetectedPatterns = detectedPatterns;

            // Determine primary source
            if (sourceScores.Count > 0)
            {
                var primaryPair = sourceScores.OrderByDescending(x => x.Value).First();
                result.PrimarySource = primaryPair.Key;
                result.PrimaryConfidence = primaryPair.Value;
            }
            else
            {
                result.PrimarySource = BoneSource.Unknown;
                result.PrimaryConfidence = 0f;
            }

            // Generate recommendations
            result.Recommendations = GenerateSourceRecommendations(result);

            return result;
        }

        private void AddToPattern(List<DetectedPattern> patterns, string patternName, string boneName, BoneSource source)
        {
            var pattern = patterns.FirstOrDefault(p => p.Name == patternName);
            if (pattern == null)
            {
                pattern = new DetectedPattern { Name = patternName };
                patterns.Add(pattern);
            }
            
            pattern.MatchCount++;
            if (pattern.Examples.Count < 5)
            {
                pattern.Examples.Add(boneName);
            }
        }

        private List<string> GenerateSourceRecommendations(NamingSourceDetectionResult result)
        {
            var recommendations = new List<string>();

            switch (result.PrimarySource)
            {
                case BoneSource.Mixamo:
                    recommendations.Add("Strong Mixamo naming detected - should map well to Unity Humanoid");
                    recommendations.Add("Consider removing 'mixamorig:' prefix for cleaner bone names");
                    break;
                
                case BoneSource.Blender:
                    recommendations.Add("Blender Rigify naming detected - excellent for Unity Humanoid setup");
                    recommendations.Add("Left/Right suffix pattern is well-supported");
                    break;
                
                case BoneSource.MaxBiped:
                    recommendations.Add("3ds Max Biped naming detected - should map well to Unity");
                    recommendations.Add("Consider simplifying bone names by removing Biped prefixes");
                    break;
                
                case BoneSource.Maya:
                    recommendations.Add("Maya naming convention detected");
                    recommendations.Add("Verify Left/Right prefixes match Unity Humanoid expectations");
                    break;
                
                case BoneSource.Unknown:
                    recommendations.Add("Custom or unknown naming convention detected");
                    recommendations.Add("Manual bone mapping may be required");
                    recommendations.Add("Consider standardizing to a common naming convention");
                    break;
            }

            if (result.PrimaryConfidence < 0.7f)
            {
                recommendations.Add("Mixed naming conventions detected - consider standardization");
            }

            return recommendations;
        }
    }
} 