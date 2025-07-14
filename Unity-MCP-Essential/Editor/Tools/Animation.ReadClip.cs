using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using System.ComponentModel;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Animation
    {
        [McpPluginTool
        (
            "Animation_ReadClip",
            Title = "Read animation clip information"
        )]
        [Description("Reads and returns information about an animation clip including name, length, frame rate, events and curves.")]
        public string ReadAnimationClip
        (
            [Description("Path to the animation clip asset")]
            string clipPath
        )
        {
            if (string.IsNullOrEmpty(clipPath))
                throw new System.ArgumentException(Error.ClipPathIsEmpty());

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
                throw new System.ArgumentException(Error.ClipNotFound(clipPath));

            var events = AnimationUtility.GetAnimationEvents(clip);
            var curves = AnimationUtility.GetCurveBindings(clip);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Name: {clip.name}");
            sb.AppendLine($"Length: {clip.length}");
            sb.AppendLine($"FrameRate: {clip.frameRate}");

            sb.AppendLine("Events:");
            if (events != null && events.Length > 0)
            {
                foreach (var e in events)
                    sb.AppendLine($"  - {e.functionName} at {e.time}s");
            }
            else
            {
                sb.AppendLine("  (none)");
            }

            sb.AppendLine("Curves:");
            if (curves != null && curves.Length > 0)
            {
                foreach (var c in curves)
                    sb.AppendLine($"  - {c.propertyName} ({c.type.Name})");
            }
            else
            {
                sb.AppendLine("  (none)");
            }

            return sb.ToString();
        }
    }
} 