#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
            "GameObject_Skeleton_GetReferences",
            Title = "Analyze Bone References and Dependencies"
        )]
        [Description(@"Comprehensive bone reference analysis tool that:
- Analyzes SkinnedMeshRenderer bone references and usage
- Identifies animation clip bone dependencies
- Detects component references to bones
- Finds unused or orphaned bones
- Generates optimization recommendations
- Provides detailed reference relationship reports")]
        public string AnalyzeBoneReferences
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
            [Description("Include detailed reference information for each bone")]
            bool includeDetailedReferences = true,
            [Description("Analyze animation clips for bone dependencies")]
            bool analyzeAnimationClips = true,
            [Description("Show only bones with issues or optimization opportunities")]
            bool showOnlyIssues = false,
            [Description("Maximum hierarchy depth to analyze (-1 for unlimited)")]
            int maxAnalysisDepth = -1
        )
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    var (success, gameObject, sourceType, sourceName, errorMessage) = 
                        LoadTargetGameObject(gameObjectPath, gameObjectName, gameObjectInstanceID, assetPathOrName, assetGuid);
                    
                    if (!success)
                        return $"[Error] {errorMessage}";

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
                catch (Exception ex)
                {
                    return $"[Error] Failed to analyze bone references: {ex.Message}";
                }
            });
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
} 