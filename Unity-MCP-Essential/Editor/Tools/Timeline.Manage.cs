#pragma warning disable CS8632
using System.ComponentModel;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Timeline
    {
        [McpPluginTool
        (
            "Timeline_Manage",
            Title = "Manage Timeline - Create and Attach"
        )]
        [Description(@"Manage Timeline operations including:
- create: Create new Timeline asset in the project
- attach: Bind Timeline asset to PlayableDirector on GameObject")]
        public string Management
        (
            [Description("Operation type: 'create', 'attach'")]
            string operation,
            [Description("Timeline asset path. Starts with 'Assets/'. Ends with '.playable'.")]
            string timelineAssetPath,
            [Description("For attach: GameObject name (optional if instanceID provided)")]
            string? gameObjectName = null,
            [Description("For attach: GameObject instanceID (takes priority over name)")]
            int? gameObjectInstanceID = null
        )
        {
            return operation.ToLower() switch
            {
                "create" => CreateTimeline(timelineAssetPath),
                "attach" => AttachToPlayableDirector(timelineAssetPath, gameObjectName, gameObjectInstanceID),
                _ => "[Error] Invalid operation. Valid operations: 'create', 'attach'"
            };
        }

        private string CreateTimeline(string assetPath)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    return Error.TimelineAssetPathIsEmpty();

                if (!assetPath.StartsWith("Assets/"))
                    return Error.AssetPathMustStartWithAssets(assetPath);

                if (!assetPath.EndsWith(".playable"))
                    return Error.AssetPathMustEndWithPlayable(assetPath);

                // Auto create directory path
                string dir = System.IO.Path.GetDirectoryName(assetPath);
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string[] parts = dir.Split('/');
                    string current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string next = current + "/" + parts[i];
                        if (!AssetDatabase.IsValidFolder(next))
                        {
                            AssetDatabase.CreateFolder(current, parts[i]);
                        }
                        current = next;
                    }
                }

                // Create new Timeline asset
                TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                
                // Create the asset
                AssetDatabase.CreateAsset(timeline, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return $"[Success] Timeline asset created at '{assetPath}'. Asset ID: {timeline.GetInstanceID()}";
            });
        }

        private string AttachToPlayableDirector(string timelineAssetPath, string? gameObjectName, int? gameObjectInstanceID)
        {
            return MainThread.Instance.Run(() =>
            {
                // Parameter validation
                if (string.IsNullOrEmpty(timelineAssetPath))
                    return Error.TimelineAssetPathIsEmpty();
                if (!timelineAssetPath.StartsWith("Assets/"))
                    return Error.AssetPathMustStartWithAssets(timelineAssetPath);
                if (!timelineAssetPath.EndsWith(".playable"))
                    return Error.AssetPathMustEndWithPlayable(timelineAssetPath);

                // Find GameObject
                GameObject targetGO = null;
                if (gameObjectInstanceID.HasValue && gameObjectInstanceID.Value != 0)
                {
                    targetGO = EditorUtility.InstanceIDToObject(gameObjectInstanceID.Value) as GameObject;
                    if (targetGO == null)
                        return Tool_GameObject.Error.NotFoundGameObjectWithInstanceID(gameObjectInstanceID.Value);
                }
                else if (!string.IsNullOrEmpty(gameObjectName))
                {
                    targetGO = GameObject.Find(gameObjectName);
                    if (targetGO == null)
                        return Error.GameObjectNotFound(gameObjectName);
                }
                else
                {
                    return "[Error] Must specify GameObject name or instanceID.";
                }

                // Check/add PlayableDirector
                var director = targetGO.GetComponent<PlayableDirector>();
                if (director == null)
                {
                    director = targetGO.AddComponent<PlayableDirector>();
                }

                // Load Timeline asset
                var timeline = AssetDatabase.LoadAssetAtPath<PlayableAsset>(timelineAssetPath);
                if (timeline == null)
                    return Error.TimelineAssetNotFound(timelineAssetPath);

                // Assign
                director.playableAsset = timeline;
                EditorUtility.SetDirty(director);
                AssetDatabase.SaveAssets();

                var sb = new StringBuilder();
                sb.AppendLine($"[Success] Timeline '{timelineAssetPath}' attached to GameObject '{targetGO.name}' (InstanceID={targetGO.GetInstanceID()}) PlayableDirector component.");
                sb.AppendLine($"PlayableDirector status: {(director.playableAsset != null ? "Bound" : "Not bound")}");
                return sb.ToString();
            });
        }
    }
} 