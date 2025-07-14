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
            "Animation_AddEvent",
            Title = "Add event to animation clip"
        )]
        [Description("Adds a new event to an existing animation clip at the specified time.")]
        public string AddAnimationEvent
        (
            [Description("Path to the animation clip asset")]
            string clipPath,
            
            [Description("Time in seconds when the event should trigger")]
            float time,
            
            [Description("Name of the function to call")]
            string functionName,
            
            [Description("Optional string parameter for the event")]
            string stringParameter = "",
            
            [Description("Optional float parameter for the event")]
            float floatParameter = 0f,
            
            [Description("Optional integer parameter for the event")]
            int intParameter = 0,
            
            [Description("Optional object reference parameter for the event")]
            Object objectReferenceParameter = null
        )
        {
            if (string.IsNullOrEmpty(clipPath))
                throw new System.ArgumentException(Error.ClipPathIsEmpty());

            if (time < 0)
                throw new System.ArgumentException(Error.InvalidTimeValue(time));

            if (string.IsNullOrEmpty(functionName))
                throw new System.ArgumentException(Error.FunctionNameIsEmpty());

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
                throw new System.ArgumentException(Error.ClipNotFound(clipPath));

            var events = AnimationUtility.GetAnimationEvents(clip);
            var newEvent = new AnimationEvent
            {
                time = time,
                functionName = functionName,
                stringParameter = stringParameter,
                floatParameter = floatParameter,
                intParameter = intParameter,
                objectReferenceParameter = objectReferenceParameter
            };

            var newEvents = new AnimationEvent[events.Length + 1];
            events.CopyTo(newEvents, 0);
            newEvents[events.Length] = newEvent;

            AnimationUtility.SetAnimationEvents(clip, newEvents);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return $"Successfully added event '{functionName}' to animation clip '{clipPath}', trigger time: {time} seconds.";
        }
    }
} 