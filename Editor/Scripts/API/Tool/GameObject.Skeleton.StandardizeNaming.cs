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

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_Skeleton_StandardizeNaming",
            Title = "Analyze and Standardize Bone Naming"
        )]
        [Description(@"Intelligent bone naming standardization analyzer that:
- Automatically detects naming conventions from different DCC software (Mixamo, Blender, 3ds Max, Maya)
- Maps bones to Unity Humanoid standard structure
- Provides standardization suggestions without making direct changes
- Generates detailed analysis report and recommendations")]
        public string AnalyzeBoneNaming
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
            [Description("Include detailed mapping suggestions for each bone")]
            bool includeDetailedSuggestions = true,
            [Description("Show only issues and recommendations (skip successful mappings)")]
            bool showOnlyIssues = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    GameObject targetGameObject = null;
                    string sourceType = "";
                    string sourceName = "";

                    // Load target GameObject using same logic as GetHierarchy
                    var loadResult = LoadTargetGameObject(gameObjectPath, gameObjectName, gameObjectInstanceID, 
                        assetPathOrName, assetGuid);
                    if (!loadResult.success)
                        return loadResult.errorMessage;
                    
                    targetGameObject = loadResult.gameObject;
                    sourceType = loadResult.sourceType;
                    sourceName = loadResult.sourceName;

                    // Analyze bone naming
                    var analyzer = new BoneNamingAnalyzer();
                    var analysisResult = analyzer.AnalyzeBoneNaming(targetGameObject, includeDetailedSuggestions, showOnlyIssues);
                    
                    if (analysisResult == null)
                        return $"[Warning] No skeleton data found in {sourceType} '{sourceName}'. Target does not contain SkinnedMeshRenderer components.";

                    var report = FormatAnalysisReport(analysisResult, sourceType, sourceName);
                    return report;
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to analyze bone naming: {ex.Message}";
                }
            });
        }

        [McpPluginTool
        (
            "GameObject_Skeleton_DetectNamingSource",
            Title = "Detect Bone Naming Convention"
        )]
        [Description(@"Detect and identify the source of bone naming conventions:
- Analyzes bone names to determine DCC software origin
- Provides confidence scores for different naming patterns
- Suggests the most likely naming convention source")]
        public string DetectBoneNamingSource
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
            string? assetGuid = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    var loadResult = LoadTargetGameObject(gameObjectPath, gameObjectName, gameObjectInstanceID, 
                        assetPathOrName, assetGuid);
                    if (!loadResult.success)
                        return loadResult.errorMessage;

                    var detector = new BoneNamingSourceDetector();
                    var bones = GetAllBones(loadResult.gameObject);
                    
                    if (bones.Length == 0)
                        return $"[Warning] No bones found in target GameObject.";

                    var detectionResult = detector.DetectNamingSource(bones);
                    return FormatDetectionReport(detectionResult, loadResult.sourceType, loadResult.sourceName);
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to detect naming source: {ex.Message}";
                }
            });
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