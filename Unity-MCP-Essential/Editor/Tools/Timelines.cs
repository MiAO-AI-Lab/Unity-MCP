#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Linq;
using com.MiAO.Unity.MCP.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.Collections.Generic;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Timeline
    {
        public static class Error
        {
            public static string TimelineAssetPathIsEmpty()
                => "[Error] Timeline asset path is empty. Please provide a valid path. Sample: \"Assets/Timeline/MyTimeline.playable\".";

            public static string AssetPathMustStartWithAssets(string assetPath)
                => $"[Error] Asset path must start with 'Assets/'. Path: '{assetPath}'.";

            public static string AssetPathMustEndWithPlayable(string assetPath)
                => $"[Error] Timeline asset path must end with '.playable'. Path: '{assetPath}'.";

            public static string TimelineAssetNotFound(string assetPath)
                => $"[Error] Timeline asset not found at path '{assetPath}'. Please check if the timeline exists in the project.";

            public static string TrackNameIsEmpty()
                => "[Error] Track name is empty. Please provide a valid track name.";

            public static string TrackTypeIsInvalid(string trackType)
                => $"[Error] Track type '{trackType}' is invalid. Valid types: AnimationTrack, AudioTrack, ActivationTrack, GroupTrack, PlayableTrack.";

            public static string PlayableDirectorNotFound()
                => "[Error] PlayableDirector component not found in the scene. Please add a PlayableDirector component to a GameObject.";

            public static string GameObjectNotFound(string gameObjectName)
                => $"[Error] GameObject '{gameObjectName}' not found in the scene.";

            public static string ClipNameIsEmpty()
                => "[Error] Clip name is empty. Please provide a valid clip name.";

            public static string TrackNotFound(string trackName)
                => $"[Error] Track '{trackName}' not found in the timeline.";

            public static string InvalidTimeValue(double time)
                => $"[Error] Invalid time value '{time}'. Time must be >= 0.";

            public static string InvalidDurationValue(double duration)
                => $"[Error] Invalid duration value '{duration}'. Duration must be > 0.";
        }
    }


}
