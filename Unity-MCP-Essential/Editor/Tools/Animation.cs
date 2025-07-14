using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Animation
    {
        public static class Error
        {
            public static string ClipPathIsEmpty()
                => "[Error] Animation clip path is empty. Please provide a valid path. Sample: \"Assets/Animations/MyAnimation.anim\".";

            public static string ClipNotFound(string clipPath)
                => $"[Error] Animation clip not found at path '{clipPath}'. Please check if the animation exists in the project.";

            public static string InvalidTimeValue(float time)
                => $"[Error] Invalid time value '{time}'. Time must be >= 0.";

            public static string FunctionNameIsEmpty()
                => "[Error] Function name is empty. Please provide a valid function name for the animation event.";
        }
    }

    public class AnimationClipInfo
    {
        public string name;
        public float length;
        public float frameRate;
        public AnimationEvent[] events;
        public EditorCurveBinding[] curves;
    }
} 