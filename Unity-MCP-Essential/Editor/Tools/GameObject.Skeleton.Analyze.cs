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

namespace com.MiAO.Unity.MCP.Essential.Tools
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
- visualize: Open a visualization window to display skeleton structure with gizmos
- controlVisualization: Control which body parts are visible in the visualization window
- all: Perform all analyses and generate comprehensive report")]
        public string AnalyzeSkeleton
        (
            [Description("Analysis operation type: 'getHierarchy', 'getReferences', 'standardizeNaming', 'detectNamingSource', 'visualize', 'controlVisualization', or 'all'")]
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
            int maxAnalysisDepth = -1,
            [Description("Body part to control or action to perform - for controlVisualization. Examples: 'right hand', 'left leg', 'spine', 'head', 'show all', 'hide all'")]
            string bodyPart = "",
            [Description("Whether to show (true) or hide (false) the specified body part - for controlVisualization. Default is true.")]
            bool showBodyPart = true,
            [Description("Whether to hide all other bones except the specified body part - for controlVisualization. Default is false.")]
            bool isolateBodyPart = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    // Handle controlVisualization operation separately (doesn't need GameObject)
                    if (operation.ToLower() == "controlvisualization")
                    {
                        return ControlVisualizationInternal(bodyPart, showBodyPart, isolateBodyPart);
                    }
                    
                    // Load target GameObject for other operations
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
                        
                        case "visualize":
                            return OpenSkeletonVisualizationWindow(targetGameObject, sourceType, sourceName);
                        
                        case "all":
                            return PerformComprehensiveAnalysis(targetGameObject, sourceType, sourceName,
                                includeTransformDetails, maxDepth, includeDetailedReferences, analyzeAnimationClips,
                                includeDetailedSuggestions, showOnlyIssues, maxAnalysisDepth);
                        
                        default:
                            return $"[Error] Invalid operation '{operation}'. Valid operations: 'getHierarchy', 'getReferences', 'standardizeNaming', 'detectNamingSource', 'visualize', 'controlVisualization', 'all'";
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

        private string OpenSkeletonVisualizationWindow(GameObject gameObject, string sourceType, string sourceName)
        {
            var bones = GetAllBones(gameObject);
            
            if (bones.Length == 0)
                return $"[Warning] No bones found in target GameObject for visualization.";

            // Open the visualization window
            var window = EditorWindow.GetWindow<SkeletonVisualizationWindow>("Skeleton Visualizer");
            window.SetSkeletonData(gameObject, bones, sourceType, sourceName);
            window.Show();
            
            return $"[Success] Skeleton visualization window opened for '{sourceName}' with {bones.Length} bones.";
                }
        
        private string ControlVisualizationInternal(string bodyPart, bool showBodyPart, bool isolateBodyPart)
        {
            try
            {
                // Find currently open visualization windows
                var windows = Resources.FindObjectsOfTypeAll<SkeletonVisualizationWindow>();
                if (windows == null || windows.Length == 0)
                {
                    return "[Error] No skeleton visualization window is currently open. Please open a visualization window first.";
                }
                
                var window = windows[0]; // Use the first (most recent) window
                var result = new StringBuilder();
                
                bodyPart = bodyPart?.ToLower().Trim() ?? "";
                
                // Handle special commands
                if (bodyPart == "show all" || bodyPart == "all")
                {
                    window.ShowAllBones();
                    return "[Success] All bones are now visible in the visualization window.";
                }
                else if (bodyPart == "hide all" || bodyPart == "none")
                {
                    window.HideAllBones();
                    return "[Success] All bones are now hidden in the visualization window.";
                }
                
                // Get bone patterns and hierarchy info for the specified body part
                var matchingResult = GetEnhancedBodyPartMatching(bodyPart, window);
                if (matchingResult.matchedBones.Count == 0)
                {
                    return $"[Error] Unknown body part: '{bodyPart}'. Supported parts: right hand, left hand, right arm, left arm, right leg, left leg, spine, head, torso, fingers, toes, etc.";
                }
                
                // Apply visibility changes using enhanced matching
                int affectedBones = window.SetBodyPartVisibilityEnhanced(matchingResult.matchedBones, showBodyPart, isolateBodyPart);
                
                if (affectedBones == 0)
                {
                    result.AppendLine($"[Warning] No bones found matching '{bodyPart}' in the current skeleton.");
                }
                else
                {
                    string action = showBodyPart ? "shown" : "hidden";
                    if (isolateBodyPart && showBodyPart)
                        action = "isolated (only this part visible)";
                    
                    result.AppendLine($"[Success] {affectedBones} bones for '{bodyPart}' have been {action}.");
                    result.AppendLine($"[Details] Matched bones: {string.Join(", ", matchingResult.matchedBoneNames.Take(10))}{(matchingResult.matchedBoneNames.Count > 10 ? "..." : "")}");
                    
                    if (matchingResult.includesChildren && matchingResult.childrenCount > 0)
                    {
                        result.AppendLine($"[Info] Includes {matchingResult.childrenCount} related child bones (fingers, etc.)");
                    }
                }
                
                return result.ToString().Trim();
            }
            catch (System.Exception ex)
            {
                return $"[Error] Failed to control skeleton visualization: {ex.Message}";
            }
        }
        
        // Enhanced body part matching result structure
        private struct BodyPartMatchingResult
        {
            public List<Transform> matchedBones;
            public List<string> matchedBoneNames;
            public bool includesChildren;
            public int childrenCount;
            
            public BodyPartMatchingResult(List<Transform> bones, List<string> names, bool children, int childCount)
            {
                matchedBones = bones;
                matchedBoneNames = names;
                includesChildren = children;
                childrenCount = childCount;
            }
        }
        
        private BodyPartMatchingResult GetEnhancedBodyPartMatching(string bodyPart, SkeletonVisualizationWindow window)
        {
            var matchedBones = new List<Transform>();
            var matchedNames = new List<string>();
            bool includesChildren = false;
            int childrenCount = 0;
            
            if (window.bones == null || window.bones.Length == 0)
            {
                return new BodyPartMatchingResult(matchedBones, matchedNames, includesChildren, childrenCount);
            }
            
            // Get basic bone patterns for the body part
            var bonePatterns = GetBonePatternsForBodyPart(bodyPart);
            
            // First, find primary bones that match the patterns
            var primaryBones = new List<Transform>();
            foreach (var bone in window.bones)
            {
                if (bone == null) continue;
                
                string boneName = bone.name.ToLower();
                bool matches = false;
                
                foreach (var pattern in bonePatterns)
                {
                    try
                    {
                        // Try regex match first
                        if (System.Text.RegularExpressions.Regex.IsMatch(boneName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            matches = true;
                            break;
                        }
                    }
                    catch
                    {
                        // If regex fails, fall back to simple contains
                        if (boneName.Contains(pattern, System.StringComparison.OrdinalIgnoreCase))
                        {
                            matches = true;
                            break;
                        }
                    }
                }
                
                if (matches)
                {
                    primaryBones.Add(bone);
                    matchedBones.Add(bone);
                    matchedNames.Add(bone.name);
                }
            }
            
            // For certain body parts, automatically include related child bones
            if (ShouldIncludeChildBones(bodyPart))
            {
                includesChildren = true;
                var allChildBones = new List<Transform>();
                
                foreach (var primaryBone in primaryBones)
                {
                    var children = GetAllChildBones(primaryBone, window.bones.ToList());
                    foreach (var child in children)
                    {
                        if (!matchedBones.Contains(child))
                        {
                            allChildBones.Add(child);
                            matchedBones.Add(child);
                            matchedNames.Add(child.name);
                        }
                    }
                }
                
                childrenCount = allChildBones.Count;
            }
            
            return new BodyPartMatchingResult(matchedBones, matchedNames, includesChildren, childrenCount);
        }
        
        private bool ShouldIncludeChildBones(string bodyPart)
        {
            // Body parts that should include their child bones
            var inclusiveParts = new[] {
                "hand", "arm", "leg", "foot", "spine", "torso", "finger", "thumb"
            };
            
            return inclusiveParts.Any(part => bodyPart.Contains(part));
        }
        
        private List<Transform> GetAllChildBones(Transform parent, List<Transform> allBones)
        {
            var children = new List<Transform>();
            
            foreach (var bone in allBones)
            {
                if (bone == null || bone == parent) continue;
                
                // Check if this bone is a descendant of the parent
                if (IsDescendantOf(bone, parent))
                {
                    children.Add(bone);
                }
            }
            
            return children;
        }
        
        private bool IsDescendantOf(Transform child, Transform potentialParent)
        {
            Transform current = child.parent;
            while (current != null)
            {
                if (current == potentialParent)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private List<string> GetBonePatternsForBodyPart(string bodyPart)
        {
            var patterns = new List<string>();
            
            switch (bodyPart.ToLower())
            {
                // Arms and hands
                case "right hand":
                case "righthand":
                    patterns.AddRange(new[] { "right.*hand", "r.*hand", ".*hand.*r", ".*hand.*right", "righthand", "rightfinger" });
                    break;
                case "left hand":
                case "lefthand":
                    patterns.AddRange(new[] { "left.*hand", "l.*hand", ".*hand.*l", ".*hand.*left", "lefthand", "leftfinger" });
                    break;
                case "right arm":
                case "rightarm":
                    patterns.AddRange(new[] { "rightarm", "rightforearm", "righthand", "rightfinger" });
                    break;
                case "left arm":
                case "leftarm":
                    patterns.AddRange(new[] { "leftarm", "leftforearm", "lefthand", "leftfinger" });
                    break;
                case "right fingers":
                case "rightfingers":
                    patterns.AddRange(new[] { "right.*(finger|thumb|index|middle|ring|pinky)", "r.*(finger|thumb|index|middle|ring|pinky)" });
                    break;
                case "left fingers":
                case "leftfingers":
                    patterns.AddRange(new[] { "left.*(finger|thumb|index|middle|ring|pinky)", "l.*(finger|thumb|index|middle|ring|pinky)" });
                    break;
                    
                // Legs and feet
                case "right leg":
                case "rightleg":
                    patterns.AddRange(new[] { "right.*(leg|thigh|shin|knee|foot|ankle)", "r.*(leg|thigh|shin|knee|foot|ankle)" });
                    break;
                case "left leg":
                case "leftleg":
                    patterns.AddRange(new[] { "left.*(leg|thigh|shin|knee|foot|ankle)", "l.*(leg|thigh|shin|knee|foot|ankle)" });
                    break;
                case "right foot":
                case "rightfoot":
                    patterns.AddRange(new[] { "right.*foot", "r.*foot", ".*foot.*r", ".*foot.*right" });
                    break;
                case "left foot":
                case "leftfoot":
                    patterns.AddRange(new[] { "left.*foot", "l.*foot", ".*foot.*l", ".*foot.*left" });
                    break;
                case "right toes":
                case "righttoes":
                    patterns.AddRange(new[] { "right.*toe", "r.*toe" });
                    break;
                case "left toes":
                case "lefttoes":
                    patterns.AddRange(new[] { "left.*toe", "l.*toe" });
                    break;
                    
                // Torso and spine
                case "spine":
                case "back":
                    patterns.AddRange(new[] { "spine", "vertebra", "back", ".*spine.*" });
                    break;
                case "chest":
                case "torso":
                    patterns.AddRange(new[] { "chest", "torso", "ribcage", "rib" });
                    break;
                case "pelvis":
                case "hips":
                    patterns.AddRange(new[] { "pelvis", "hip", ".*hip.*" });
                    break;
                    
                // Head and neck
                case "head":
                    patterns.AddRange(new[] { "head", "skull", ".*head.*" });
                    break;
                case "neck":
                    patterns.AddRange(new[] { "neck", ".*neck.*" });
                    break;
                case "face":
                    patterns.AddRange(new[] { "face", "jaw", "eye", ".*face.*" });
                    break;
                    
                // General patterns
                case "hands":
                    patterns.AddRange(new[] { ".*hand", ".*finger.*", ".*thumb.*" });
                    break;
                case "arms":
                    patterns.AddRange(new[] { ".*arm.*", ".*shoulder.*", ".*elbow.*" });
                    break;
                case "legs":
                    patterns.AddRange(new[] { ".*leg.*", ".*thigh.*", ".*knee.*", ".*shin.*" });
                    break;
                case "feet":
                    patterns.AddRange(new[] { ".*foot.*", ".*toe.*", ".*ankle.*" });
                    break;
                case "fingers":
                    patterns.AddRange(new[] { ".*finger.*", ".*thumb.*", ".*index.*", ".*middle.*", ".*ring.*", ".*pinky.*" });
                    break;
                case "toes":
                    patterns.AddRange(new[] { ".*toe.*" });
                    break;
                    
                // Allow direct bone name matching
                default:
                    // If it doesn't match any predefined pattern, treat it as a direct bone name pattern
                    patterns.Add($".*{bodyPart}.*");
                    break;
            }
            
            return patterns;
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

    public class SkeletonVisualizationWindow : EditorWindow
    {
        private GameObject targetGameObject;
        public Transform[] bones;
        private string sourceType;
        private string sourceName;
        
        // Visualization settings
        private bool showBoneNames = true;
        private bool showBoneConnections = true;
        private bool showBoneOrientations = true;
        private bool xRayMode = false;
        private bool hideEndBones = false;
        private Color boneColor = Color.cyan;
        private Color connectionColor = Color.yellow;
        private Color nameColor = Color.white;
        private float boneSize = 0.01f;
        private float connectionWidth = 2f;
        private Color backgroundColor = Color.black;
        private Color axisColorX = Color.red;
        private Color axisColorY = Color.green;
        private Color axisColorZ = Color.blue;
        
        private Vector2 boneScrollPosition;
        private Dictionary<Transform, bool> boneVisibility;
        private Dictionary<Transform, List<Transform>> boneHierarchy;
        
        // 3D View in window
        private Camera visualizationCamera;
        private RenderTexture renderTexture;
        private GameObject cameraObject;
        private Vector3 cameraPosition;
        private Vector3 cameraRotation;
        private float cameraDistance = 2f;
        private Vector3 targetCenter;
        private bool isDragging = false;
        private Vector2 lastMousePosition;
        
        // Model visualization
        private GameObject modelCopy;
        private SkinnedMeshRenderer[] originalSkinnedMeshRenderers;
        private SkinnedMeshRenderer[] copySkinnedMeshRenderers;
        
        public void SetSkeletonData(GameObject gameObject, Transform[] skeletonBones, string srcType, string srcName)
        {
            targetGameObject = gameObject;
            bones = skeletonBones;
            sourceType = srcType;
            sourceName = srcName;
            
            // Initialize bone visibility
            boneVisibility = new Dictionary<Transform, bool>();
            foreach (var bone in bones)
            {
                if (bone != null)
                    boneVisibility[bone] = true;
            }
            
            // Build bone hierarchy
            BuildBoneHierarchy();
            
            // Get original SkinnedMeshRenderers
            originalSkinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            // Calculate target center
            CalculateTargetCenter();
            
            // Setup 3D visualization
            SetupVisualizationCamera();
        }
        
        private void BuildBoneHierarchy()
        {
            boneHierarchy = new Dictionary<Transform, List<Transform>>();
            var boneSet = new HashSet<Transform>(bones);
            
            // Initialize hierarchy dictionary
            foreach (var bone in bones)
            {
                if (bone != null)
                    boneHierarchy[bone] = new List<Transform>();
            }
            
            // Build parent-child relationships
            foreach (var bone in bones)
            {
                if (bone != null && bone.parent != null && boneSet.Contains(bone.parent))
                {
                    if (boneHierarchy.ContainsKey(bone.parent))
                        boneHierarchy[bone.parent].Add(bone);
                }
            }
        }
        
        private void CalculateTargetCenter()
        {
            if (bones == null || bones.Length == 0) return;
            
            Vector3 center = Vector3.zero;
            int validBones = 0;
            
            foreach (var bone in bones)
            {
                if (bone != null)
                {
                    center += bone.position;
                    validBones++;
                }
            }
            
            if (validBones > 0)
            {
                targetCenter = center / validBones;
            }
        }
        
        private void SetupVisualizationCamera()
        {
            // Create camera object
            cameraObject = new GameObject("SkeletonVisualizationCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            
            // Setup camera
            visualizationCamera = cameraObject.AddComponent<Camera>();
            visualizationCamera.backgroundColor = backgroundColor;
            visualizationCamera.clearFlags = CameraClearFlags.SolidColor;
            visualizationCamera.orthographic = false;
            visualizationCamera.fieldOfView = 60f;
            visualizationCamera.nearClipPlane = 0.1f;
            visualizationCamera.farClipPlane = 1000f;
            visualizationCamera.enabled = false; // We'll render manually
            
            // Setup render texture
            renderTexture = new RenderTexture(512, 512, 24);
            renderTexture.Create();
            visualizationCamera.targetTexture = renderTexture;
            
            // Create model copy for visualization
            CreateModelCopy();
            
            // Position camera
            cameraPosition = targetCenter + new Vector3(0, 0, -cameraDistance);
            cameraRotation = Vector3.zero;
            UpdateCameraTransform();
        }
        
        private void UpdateCameraTransform()
        {
            if (visualizationCamera == null) return;
            
            // Apply rotation and distance
            Vector3 direction = Quaternion.Euler(cameraRotation.x, cameraRotation.y, 0) * Vector3.back;
            visualizationCamera.transform.position = targetCenter + direction * cameraDistance;
            visualizationCamera.transform.LookAt(targetCenter);
        }
        

                
        private void CreateModelCopy()
        {
            if (targetGameObject == null) return;
            
            // Create a copy of the target gameobject for visualization
            modelCopy = UnityEngine.Object.Instantiate(targetGameObject);
            modelCopy.name = "ModelCopy_" + targetGameObject.name;
            modelCopy.hideFlags = HideFlags.HideAndDontSave;
            
            // Get all SkinnedMeshRenderers from the copy
            copySkinnedMeshRenderers = modelCopy.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            // Make the copy only visible to our visualization camera
            int visualizationLayer = 31; // Use layer 31 for visualization
            SetLayerRecursively(modelCopy, visualizationLayer);
            
            // Ensure materials are properly set for visualization
            foreach (var renderer in copySkinnedMeshRenderers)
            {
                if (renderer != null)
                {
                    // Make sure the renderer is enabled
                    renderer.enabled = true;
                    
                    // Check if materials exist
                    bool hasValidMaterials = renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0;
                    if (hasValidMaterials)
                    {
                        hasValidMaterials = false;
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat != null)
                            {
                                hasValidMaterials = true;
                                break;
                            }
                        }
                    }
                    
                    if (!hasValidMaterials)
                    {
                        // Create basic materials if none exist
                        var basicMaterial = new Material(Shader.Find("Standard"));
                        basicMaterial.color = Color.white;
                        basicMaterial.name = "Basic Visualization Material";
                        renderer.sharedMaterial = basicMaterial;
                    }
                    else
                    {
                        // Ensure all materials are valid
                        var materials = renderer.sharedMaterials;
                        bool materialsChanged = false;
                        
                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (materials[i] == null)
                            {
                                var basicMaterial = new Material(Shader.Find("Standard"));
                                basicMaterial.color = Color.white;
                                basicMaterial.name = "Basic Visualization Material " + i;
                                materials[i] = basicMaterial;
                                materialsChanged = true;
                            }
                        }
                        
                        if (materialsChanged)
                        {
                            renderer.sharedMaterials = materials;
                        }
                    }
                }
            }
            
            // Set camera to only render visualization layer
            visualizationCamera.cullingMask = 1 << visualizationLayer;
            
            // Ensure the model copy is positioned correctly
            if (modelCopy.transform.position != targetGameObject.transform.position)
            {
                modelCopy.transform.position = targetGameObject.transform.position;
                modelCopy.transform.rotation = targetGameObject.transform.rotation;
                modelCopy.transform.localScale = targetGameObject.transform.localScale;
            }
        }
        
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        
        private void OnGUI()
        {
            if (targetGameObject == null)
            {
                EditorGUILayout.HelpBox("No skeleton data loaded.", UnityEditor.MessageType.Info);
                return;
            }
            
            // Header section
            EditorGUILayout.BeginVertical("box", GUILayout.Height(50));
            EditorGUILayout.LabelField("Skeleton Visualization", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Source: {sourceType} - {sourceName}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Target: {targetGameObject.name}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Bones: {bones?.Length ?? 0}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            // Main layout
            EditorGUILayout.BeginHorizontal();
            
            // Left panel - Controls
            EditorGUILayout.BeginVertical(GUILayout.Width(260));
            
            // Display options
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Display Options", EditorStyles.boldLabel);
            xRayMode = EditorGUILayout.Toggle("X-Ray Mode (Bones Always Visible)", xRayMode);
            showBoneNames = EditorGUILayout.Toggle("Show Bone Names", showBoneNames);
            showBoneConnections = EditorGUILayout.Toggle("Show Bone Connections", showBoneConnections);
            showBoneOrientations = EditorGUILayout.Toggle("Show Bone Orientations", showBoneOrientations);
            
            EditorGUILayout.Space();
            bool newHideEndBones = EditorGUILayout.Toggle("Hide End Bones (Fingers/Toes/Face)", hideEndBones);
            if (newHideEndBones != hideEndBones)
            {
                hideEndBones = newHideEndBones;
                ApplyEndBonesVisibility();
            }
            
            // Debug info
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Info:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Model Copy: {(modelCopy != null ? "✓" : "✗")}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Renderers: {(copySkinnedMeshRenderers?.Length ?? 0)}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Camera: {(visualizationCamera != null ? "✓" : "✗")}", EditorStyles.miniLabel);
            
            if (copySkinnedMeshRenderers != null && copySkinnedMeshRenderers.Length > 0)
            {
                int materialCount = 0;
                int enabledRenderers = 0;
                foreach (var renderer in copySkinnedMeshRenderers)
                {
                    if (renderer != null)
                    {
                        if (renderer.enabled) enabledRenderers++;
                        materialCount += renderer.sharedMaterials?.Length ?? 0;
                    }
                }
                EditorGUILayout.LabelField($"Enabled Renderers: {enabledRenderers}/{copySkinnedMeshRenderers.Length}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Total Materials: {materialCount}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            
            // Color settings
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
            boneColor = EditorGUILayout.ColorField("Bone Color", boneColor);
            connectionColor = EditorGUILayout.ColorField("Connection Color", connectionColor);
            nameColor = EditorGUILayout.ColorField("Name Color", nameColor);
            backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);
            
            EditorGUILayout.LabelField("Axis Colors", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            axisColorX = EditorGUILayout.ColorField("X", axisColorX, GUILayout.Width(70));
            axisColorY = EditorGUILayout.ColorField("Y", axisColorY, GUILayout.Width(70));
            axisColorZ = EditorGUILayout.ColorField("Z", axisColorZ, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            
            if (visualizationCamera != null)
                visualizationCamera.backgroundColor = backgroundColor;
            EditorGUILayout.EndVertical();
            
            // Size settings
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Size Settings", EditorStyles.boldLabel);
            boneSize = EditorGUILayout.Slider("Bone Size", boneSize, 0.01f, 0.3f);
            connectionWidth = EditorGUILayout.Slider("Connection Width", connectionWidth, 1f, 15f);
            cameraDistance = EditorGUILayout.Slider("Camera Distance", cameraDistance, 0.5f, 10f);
            EditorGUILayout.EndVertical();
            
            // Camera controls
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Camera Controls", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Left Mouse: Rotate", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Right Mouse: Pan", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Scroll: Zoom", EditorStyles.miniLabel);
            
            if (GUILayout.Button("Reset Camera"))
            {
                ResetCamera();
            }
            EditorGUILayout.EndVertical();
            
            // Bone visibility
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Bone Visibility", EditorStyles.boldLabel);
            
            if (bones != null && bones.Length > 0)
            {
                boneScrollPosition = EditorGUILayout.BeginScrollView(boneScrollPosition, GUILayout.Height(180));
                DrawBoneHierarchy();
                EditorGUILayout.EndScrollView();
                
                // Utility buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Show All", GUILayout.Height(20)))
                {
                    foreach (var bone in bones)
                    {
                        if (bone != null && boneVisibility.ContainsKey(bone))
                            boneVisibility[bone] = true;
                    }
                }
                if (GUILayout.Button("Hide All", GUILayout.Height(20)))
                {
                    foreach (var bone in bones)
                    {
                        if (bone != null && boneVisibility.ContainsKey(bone))
                            boneVisibility[bone] = false;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
            
            // Right panel - 3D View
            EditorGUILayout.BeginVertical();
            
            Draw3DView();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // Update camera transform if distance changed
            UpdateCameraTransform();
            
            // Repaint to update the view
            Repaint();
        }
        
        private void Draw3DView()
        {
            if (renderTexture == null || visualizationCamera == null) return;
            
            // 3D View section
            EditorGUILayout.BeginVertical("box");
            
            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("3D View", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Bones: {bones?.Length ?? 0}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            // Render the skeleton
            RenderSkeleton();
            
            // Display the render texture
            Rect viewRect = GUILayoutUtility.GetRect(512, 400, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            // Draw background
            EditorGUI.DrawRect(viewRect, backgroundColor);
            
            // Draw the render texture
            GUI.DrawTexture(viewRect, renderTexture, ScaleMode.ScaleToFit);
            
            // Draw bone names overlay
            if (showBoneNames)
            {
                DrawBoneNamesOverlay(viewRect);
            }
            
            // Handle mouse input for camera control
            HandleMouseInput(viewRect);
            
            // Draw border
            EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.y, viewRect.width, 1), Color.gray);
            EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.y, 1, viewRect.height), Color.gray);
            EditorGUI.DrawRect(new Rect(viewRect.x + viewRect.width - 1, viewRect.y, 1, viewRect.height), Color.gray);
            EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.y + viewRect.height - 1, viewRect.width, 1), Color.gray);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawBoneNamesOverlay(Rect viewRect)
        {
            if (bones == null || visualizationCamera == null) return;
            
            var style = new GUIStyle();
            style.normal.textColor = nameColor;
            style.fontSize = 10;
            style.fontStyle = FontStyle.Bold;
            
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                bool isVisible = boneVisibility.ContainsKey(bone) ? boneVisibility[bone] : true;
                if (!isVisible) continue;
                
                // Check if this bone should be hidden due to hideEndBones setting
                if (hideEndBones && IsEndBone(bone)) continue;
                
                // Convert world position to screen position
                Vector3 screenPos = visualizationCamera.WorldToScreenPoint(bone.position);
                
                // Check if bone is in front of camera
                if (screenPos.z > 0)
                {
                    // Convert camera screen space to GUI space
                    Vector2 guiPos = new Vector2(screenPos.x, renderTexture.height - screenPos.y);
                    
                    // Map to view rect
                    float scaleX = viewRect.width / renderTexture.width;
                    float scaleY = viewRect.height / renderTexture.height;
                    float scale = Mathf.Min(scaleX, scaleY);
                    
                    Vector2 offsetInRect = new Vector2(
                        (viewRect.width - renderTexture.width * scale) * 0.5f,
                        (viewRect.height - renderTexture.height * scale) * 0.5f
                    );
                    
                    Vector2 finalPos = viewRect.position + offsetInRect + guiPos * scale;
                    
                    // Only draw if within view rect
                    if (viewRect.Contains(finalPos))
                    {
                        GUI.Label(new Rect(finalPos.x + 5, finalPos.y - 10, 150, 20), bone.name, style);
                    }
                }
            }
        }
        
        private void RenderSkeleton()
        {
            if (visualizationCamera == null || bones == null) return;
            
            // Render the camera first (will render model)
            visualizationCamera.Render();
            
            // Then overlay the skeleton using GL
            RenderTexture.active = renderTexture;
            
            // Setup GL for rendering
            GL.PushMatrix();
            GL.LoadProjectionMatrix(visualizationCamera.projectionMatrix);
            GL.modelview = visualizationCamera.worldToCameraMatrix;
            
            // Create material for drawing
            Material lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            lineMaterial.SetPass(0);
            
            // Set depth test mode for X-Ray or normal mode
            if (xRayMode)
            {
                // Disable depth test to always show bones on top
                GL.Begin(GL.LINES);
                GL.Color(Color.white);
                GL.End();
                
                // Use a different approach for X-Ray mode - draw with no depth test
                Graphics.SetRenderTarget(renderTexture);
                
                // Draw bone connections first
                if (showBoneConnections)
                {
                    DrawBoneConnectionsGL_XRay();
                }
                
                // Draw bones
                DrawBonesGL_XRay();
            }
            else
            {
                // Normal mode with depth testing
                // Draw bone connections first
                if (showBoneConnections)
                {
                    DrawBoneConnectionsGL();
                }
                
                // Draw bones
                DrawBonesGL();
            }
            
            GL.PopMatrix();
            RenderTexture.active = null;
            
            // Force repaint
            if (lineMaterial != null)
                DestroyImmediate(lineMaterial);
        }
        
        private void DrawBoneConnectionsGL()
        {
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                bool isVisible = boneVisibility.ContainsKey(bone) ? boneVisibility[bone] : true;
                if (!isVisible) continue;
                
                // Check if this bone should be hidden due to hideEndBones setting
                if (hideEndBones && IsEndBone(bone)) continue;
                
                // Draw connection to parent
                if (bone.parent != null && bones.Contains(bone.parent))
                {
                    bool parentVisible = boneVisibility.ContainsKey(bone.parent) ? boneVisibility[bone.parent] : true;
                    if (parentVisible)
                    {
                        // Also check if parent should be hidden due to hideEndBones setting
                        if (hideEndBones && IsEndBone(bone.parent)) continue;
                        
                        DrawLineGL(bone.position, bone.parent.position, connectionColor);
                    }
                }
            }
        }
        
        private void DrawBonesGL()
        {
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                bool isVisible = boneVisibility.ContainsKey(bone) ? boneVisibility[bone] : true;
                if (!isVisible) continue;
                
                // Check if this bone should be hidden due to hideEndBones setting
                if (hideEndBones && IsEndBone(bone)) continue;
                
                var position = bone.position;
                
                // Draw bone point (simple wireframe sphere)
                DrawSphereGL(position, boneSize, boneColor);
                
                // Draw bone orientation
                if (showBoneOrientations)
                {
                    var size = boneSize * 3f;
                    DrawLineGL(position, position + bone.right * size, axisColorX);
                    DrawLineGL(position, position + bone.up * size, axisColorY);
                    DrawLineGL(position, position + bone.forward * size, axisColorZ);
                }
            }
        }
        
        private void DrawLineGL(Vector3 start, Vector3 end, Color color)
        {
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(start.x, start.y, start.z);
            GL.Vertex3(end.x, end.y, end.z);
            GL.End();
        }
        
        private void DrawSphereGL(Vector3 center, float radius, Color color)
        {
            // Draw a simple dot using GL.POINTS
            GL.Begin(GL.QUADS);
            GL.Color(color);
            
            // Create a small quad for the dot
            float dotSize = radius * 0.3f; // Make dot smaller than original radius
            
            Vector3 right = visualizationCamera.transform.right * dotSize;
            Vector3 up = visualizationCamera.transform.up * dotSize;
            
            // Draw a billboard quad facing the camera
            GL.Vertex3((center - right - up).x, (center - right - up).y, (center - right - up).z);
            GL.Vertex3((center + right - up).x, (center + right - up).y, (center + right - up).z);
            GL.Vertex3((center + right + up).x, (center + right + up).y, (center + right + up).z);
            GL.Vertex3((center - right + up).x, (center - right + up).y, (center - right + up).z);
            
            GL.End();
        }
        
        private void DrawBoneConnectionsGL_XRay()
        {
            // Create material with no depth test for X-Ray mode
            Material xrayMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            xrayMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            xrayMaterial.SetPass(0);
            
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                bool isVisible = boneVisibility.ContainsKey(bone) ? boneVisibility[bone] : true;
                if (!isVisible) continue;
                
                // Check if this bone should be hidden due to hideEndBones setting
                if (hideEndBones && IsEndBone(bone)) continue;
                
                // Draw connection to parent
                if (bone.parent != null && bones.Contains(bone.parent))
                {
                    bool parentVisible = boneVisibility.ContainsKey(bone.parent) ? boneVisibility[bone.parent] : true;
                    if (parentVisible)
                    {
                        // Also check if parent should be hidden due to hideEndBones setting
                        if (hideEndBones && IsEndBone(bone.parent)) continue;
                        
                        // Use brighter color for X-Ray mode
                        Color xrayColor = connectionColor;
                        xrayColor.a = 1.0f;
                        DrawLineGL(bone.position, bone.parent.position, xrayColor);
                    }
                }
            }
            
            DestroyImmediate(xrayMaterial);
        }
        
        private void DrawBonesGL_XRay()
        {
            // Create material with no depth test for X-Ray mode
            Material xrayMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            xrayMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            xrayMaterial.SetPass(0);
            
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                bool isVisible = boneVisibility.ContainsKey(bone) ? boneVisibility[bone] : true;
                if (!isVisible) continue;
                
                // Check if this bone should be hidden due to hideEndBones setting
                if (hideEndBones && IsEndBone(bone)) continue;
                
                var position = bone.position;
                
                // Use brighter color for X-Ray mode
                Color xrayBoneColor = boneColor;
                xrayBoneColor.a = 1.0f;
                
                // Draw bone point (simple wireframe sphere)
                DrawSphereGL(position, boneSize, xrayBoneColor);
                
                // Draw bone orientation
                if (showBoneOrientations)
                {
                    var size = boneSize * 3f;
                    Color xrayAxisX = axisColorX; xrayAxisX.a = 1.0f;
                    Color xrayAxisY = axisColorY; xrayAxisY.a = 1.0f;
                    Color xrayAxisZ = axisColorZ; xrayAxisZ.a = 1.0f;
                    
                    DrawLineGL(position, position + bone.right * size, xrayAxisX);
                    DrawLineGL(position, position + bone.up * size, xrayAxisY);
                    DrawLineGL(position, position + bone.forward * size, xrayAxisZ);
                }
            }
            
            DestroyImmediate(xrayMaterial);
        }
        
        private void HandleMouseInput(Rect viewRect)
        {
            Event e = Event.current;
            
            if (viewRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown)
                {
                    if (e.button == 0 || e.button == 1) // Left or right mouse button
                    {
                        isDragging = true;
                        lastMousePosition = e.mousePosition;
                        e.Use();
                    }
                }
                else if (e.type == EventType.MouseDrag && isDragging)
                {
                    Vector2 delta = e.mousePosition - lastMousePosition;
                    
                    if (e.button == 0) // Left mouse - rotate
                    {
                        cameraRotation.y += delta.x * 0.5f;
                        cameraRotation.x -= delta.y * 0.5f;
                        cameraRotation.x = Mathf.Clamp(cameraRotation.x, -90f, 90f);
                        UpdateCameraTransform();
                    }
                    else if (e.button == 1) // Right mouse - pan
                    {
                        Vector3 right = visualizationCamera.transform.right * delta.x * 0.01f;
                        Vector3 up = visualizationCamera.transform.up * delta.y * 0.01f;
                        targetCenter += (right - up);
                        UpdateCameraTransform();
                    }
                    
                    lastMousePosition = e.mousePosition;
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.MouseUp)
                {
                    isDragging = false;
                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    cameraDistance += e.delta.y * 0.1f;
                    cameraDistance = Mathf.Clamp(cameraDistance, 0.5f, 10f);
                    UpdateCameraTransform();
                    e.Use();
                    Repaint();
                }
            }
        }
        
        private void ResetCamera()
        {
            cameraRotation = Vector3.zero;
            cameraDistance = 2f;
            CalculateTargetCenter();
            UpdateCameraTransform();
        }
        
        private void DrawBoneHierarchy()
        {
            var rootBones = bones.Where(b => b != null && (b.parent == null || !bones.Contains(b.parent))).ToList();
            rootBones.Sort((a, b) => string.Compare(a.name, b.name));
            
            foreach (var rootBone in rootBones)
            {
                DrawBoneHierarchyRecursive(rootBone, 0);
            }
        }
        
        private void DrawBoneHierarchyRecursive(Transform bone, int depth)
        {
            if (bone == null) return;
            
            EditorGUILayout.BeginHorizontal();
            
            // Indentation
            GUILayout.Space(depth * 20);
            
            // Visibility toggle
            bool isVisible = boneVisibility.ContainsKey(bone) ? boneVisibility[bone] : true;
            bool newVisibility = EditorGUILayout.Toggle(isVisible, GUILayout.Width(20));
            if (newVisibility != isVisible && boneVisibility.ContainsKey(bone))
                boneVisibility[bone] = newVisibility;
            
            // Bone name
            EditorGUILayout.LabelField(bone.name, GUILayout.ExpandWidth(true));
            
            // Select button
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeTransform = bone;
                EditorGUIUtility.PingObject(bone);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Draw children
            if (boneHierarchy.ContainsKey(bone))
            {
                var children = boneHierarchy[bone];
                children.Sort((a, b) => string.Compare(a.name, b.name));
                
                foreach (var child in children)
                {
                    DrawBoneHierarchyRecursive(child, depth + 1);
                }
            }
                }
        
        public void ShowAllBones()
        {
            if (bones == null || boneVisibility == null) return;
            
            foreach (var bone in bones)
            {
                if (bone != null && boneVisibility.ContainsKey(bone))
                {
                    boneVisibility[bone] = true;
                }
            }
        }
        
        public void HideAllBones()
        {
            if (bones == null || boneVisibility == null) return;
            
            foreach (var bone in bones)
            {
                if (bone != null && boneVisibility.ContainsKey(bone))
                {
                    boneVisibility[bone] = false;
                }
            }
        }
        
        public int SetBodyPartVisibilityEnhanced(List<Transform> targetBones, bool isVisible, bool isolate)
        {
            if (targetBones == null || boneVisibility == null) return 0;
            
            int affectedCount = 0;
            
            // If isolating, first hide all bones
            if (isolate && isVisible)
            {
                HideAllBones();
            }
            
            // Set visibility for target bones
            foreach (var bone in targetBones)
            {
                if (bone != null && boneVisibility.ContainsKey(bone))
                {
                    boneVisibility[bone] = isVisible;
                    affectedCount++;
                }
            }
            
            return affectedCount;
        }
        
        public int SetBodyPartVisibility(List<string> bonePatterns, bool show, bool isolate)
        {
            if (bones == null || boneVisibility == null || bonePatterns == null) return 0;
            
            int affectedBones = 0;
            var matchedBones = new List<Transform>();
            
            // Find bones that match the patterns
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                bool matches = false;
                foreach (var pattern in bonePatterns)
                {
                    try
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(bone.name, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            matches = true;
                            break;
                        }
                    }
                    catch
                    {
                        // If regex fails, try simple contains match
                        if (bone.name.ToLower().Contains(pattern.ToLower().Replace(".*", "")))
                        {
                            matches = true;
                            break;
                        }
                    }
                }
                
                if (matches)
                {
                    matchedBones.Add(bone);
                }
            }
            
            // If isolate is true, first hide all bones
            if (isolate && show)
            {
                HideAllBones();
            }
            
            // Apply visibility to matched bones
            foreach (var bone in matchedBones)
            {
                if (boneVisibility.ContainsKey(bone))
                {
                    boneVisibility[bone] = show;
                    affectedBones++;
                }
            }
            
            return affectedBones;
        }
        
        private void ApplyEndBonesVisibility()
        {
            if (bones == null || boneVisibility == null) return;
            
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                
                bool isEndBone = IsEndBone(bone);
                if (isEndBone)
                {
                    boneVisibility[bone] = !hideEndBones;
                }
            }
        }
        
        private bool IsEndBone(Transform bone)
        {
            if (bone == null) return false;
            
            string boneName = bone.name.ToLower();
            
            // 手指骨骼模式
            if (IsFingerBone(boneName)) return true;
            
            // 脚趾骨骼模式
            if (IsToeBone(boneName)) return true;
            
            // 面部表情骨骼模式
            if (IsFacialBone(boneName)) return true;
            
            // 头发骨骼模式
            if (IsHairBone(boneName)) return true;
            
            // 其他末端骨骼模式
            if (IsOtherEndBone(boneName)) return true;
            
            return false;
        }
        
        private bool IsFingerBone(string boneName)
        {
            // 手指相关的骨骼
            string[] fingerPatterns = {
                "finger", "thumb", "index", "middle", "ring", "pinky", "little",
                "proximal", "intermediate", "distal", "metacarpal", "phalanx"
            };
            
            foreach (var pattern in fingerPatterns)
            {
                if (boneName.Contains(pattern)) return true;
            }
            
            // 检查数字模式 (如 RightFinger00, LeftFinger01 等)
            if (boneName.Contains("finger") && System.Text.RegularExpressions.Regex.IsMatch(boneName, @"\d{2}"))
            {
                return true;
            }
            
            return false;
        }
        
        private bool IsToeBone(string boneName)
        {
            // 脚趾相关的骨骼
            string[] toePatterns = {
                "toe", "toes", "bigtoe", "littletoe", "metatarsal"
            };
            
            foreach (var pattern in toePatterns)
            {
                if (boneName.Contains(pattern)) return true;
            }
            
            return false;
        }
        
        private bool IsFacialBone(string boneName)
        {
            // 面部表情相关的骨骼
            string[] facialPatterns = {
                "eye", "eyebrow", "eyelid", "mouth", "lip", "cheek", "jaw", "chin",
                "nose", "nostril", "forehead", "temple", "smile", "frown",
                "blink", "pupil", "iris", "tongue", "teeth", "gum"
            };
            
            foreach (var pattern in facialPatterns)
            {
                if (boneName.Contains(pattern)) return true;
            }
            
            return false;
        }
        
        private bool IsHairBone(string boneName)
        {
            // 头发相关的骨骼
            string[] hairPatterns = {
                "hair", "strand", "ponytail", "braid", "bang", "fringe"
            };
            
            foreach (var pattern in hairPatterns)
            {
                if (boneName.Contains(pattern)) return true;
            }
            
            return false;
        }
        
        private bool IsOtherEndBone(string boneName)
        {
            // 其他末端骨骼 (通常是叶子节点或装饰性骨骼)
            string[] otherEndPatterns = {
                "end", "tip", "nub", "effector", "ik", "pole", "target",
                "helper", "dummy", "null", "locator", "marker"
            };
            
            foreach (var pattern in otherEndPatterns)
            {
                if (boneName.Contains(pattern)) return true;
            }
            
            return false;
        }
        
        private void OnDestroy()
        {
            // Clean up resources
            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyImmediate(renderTexture);
            }
            
            if (cameraObject != null)
            {
                DestroyImmediate(cameraObject);
            }
            
            if (modelCopy != null)
            {
                DestroyImmediate(modelCopy);
            }
        }
    }
} 