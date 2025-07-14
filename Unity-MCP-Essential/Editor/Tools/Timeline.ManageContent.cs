#pragma warning disable CS8632
using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Timeline
    {
        public class TimelineMarkerInfo
        {
            public string timelineAssetPath;
            public List<MarkerTrackInfo> markerTracks = new List<MarkerTrackInfo>();
        }

        public class MarkerTrackInfo
        {
            public string trackName;
            public List<MarkerInfo> markers = new List<MarkerInfo>();
        }

        public class MarkerInfo
        {
            public double time;
            public string markerType;
        }

        [McpPluginTool
        (
            "Timeline_ManageContent",
            Title = "Manage Timeline Content"
        )]
        [Description(@"Manage Timeline content operations including:
- addTrack: Add a new track to an existing Timeline asset
- addClip: Add a new clip to an existing track in Timeline asset
- listTracks: List all tracks in a Timeline asset
- addMarkerTrack: Create new marker track in Timeline asset
- addSignalMarker: Add a signal marker to an existing marker track
- getMarkers: Retrieve information about all markers in Timeline asset")]
        public string Content
        (
            [Description("Operation type: 'addTrack', 'addClip', 'listTracks', 'addMarkerTrack', 'addSignalMarker', 'getMarkers'")]
            string operation,
            [Description("Timeline asset path. Starts with 'Assets/'. Ends with '.playable'.")]
            string timelineAssetPath,
            [Description("For addTrack/addClip/addMarkerTrack/addSignalMarker: Track name (required)")]
            string? trackName = null,
            [Description("For addTrack: Track type. Valid types: AnimationTrack, AudioTrack, ActivationTrack, GroupTrack, PlayableTrack")]
            string? trackType = null,
            [Description("For addClip: Clip name")]
            string? clipName = null,
            [Description("For addClip: Start time of the clip in seconds. Leave empty to auto-append")]
            double? startTime = null,
            [Description("For addClip on AnimationTrack: Animation Clip asset path")]
            string? animationClipPath = null,
            [Description("For addClip on AudioTrack: Audio Clip asset path")]
            string? audioClipPath = null,
            [Description("For addSignalMarker: Time in seconds where to add the signal marker")]
            float? time = null,
            [Description("For addSignalMarker: Name for the new signal asset")]
            string? signalAssetName = null
        )
        {
            return operation.ToLower() switch
            {
                "addtrack" => AddTrack(timelineAssetPath, trackName, trackType),
                "addclip" => AddClip(timelineAssetPath, trackName, clipName, startTime, animationClipPath, audioClipPath),
                "listtracks" => ListTracks(timelineAssetPath),
                "addmarkertrack" => AddMarkerTrack(timelineAssetPath, trackName),
                "addsignalmarker" => AddSignalMarker(timelineAssetPath, trackName, time, signalAssetName),
                "getmarkers" => GetMarkers(timelineAssetPath),
                _ => "[Error] Invalid operation. Valid operations: 'addTrack', 'addClip', 'listTracks', 'addMarkerTrack', 'addSignalMarker', 'getMarkers'"
            };
        }

        private string AddTrack(string timelineAssetPath, string? trackName, string? trackType)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(timelineAssetPath))
                    return Error.TimelineAssetPathIsEmpty();

                if (string.IsNullOrEmpty(trackName))
                    return Error.TrackNameIsEmpty();

                // Load the Timeline asset
                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelineAssetPath);
                if (timeline == null)
                    return Error.TimelineAssetNotFound(timelineAssetPath);

                // Determine track type
                Type trackTypeToCreate = null;
                switch (trackType?.ToLower())
                {
                    case "animationtrack":
                        trackTypeToCreate = typeof(AnimationTrack);
                        break;
                    case "audiotrack":
                        trackTypeToCreate = typeof(AudioTrack);
                        break;
                    case "activationtrack":
                        trackTypeToCreate = typeof(ActivationTrack);
                        break;
                    case "grouptrack":
                        trackTypeToCreate = typeof(GroupTrack);
                        break;
                    case "playabletrack":
                        trackTypeToCreate = typeof(PlayableTrack);
                        break;
                    default:
                        return Error.TrackTypeIsInvalid(trackType ?? "null");
                }

                // Create and add the track
                TrackAsset track = timeline.CreateTrack(trackTypeToCreate, null, trackName);
                
                // Mark the timeline as dirty to save changes
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();

                return $"[Success] Added {trackType} track '{trackName}' to timeline '{timelineAssetPath}'. Track ID: {track.GetInstanceID()}";
            });
        }

        private string AddClip(string timelineAssetPath, string? trackName, string? clipName, double? startTime, string? animationClipPath, string? audioClipPath)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(timelineAssetPath))
                    return Error.TimelineAssetPathIsEmpty();

                if (string.IsNullOrEmpty(trackName))
                    return Error.TrackNameIsEmpty();

                if (string.IsNullOrEmpty(clipName))
                    return Error.ClipNameIsEmpty();

                // Load the Timeline asset
                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelineAssetPath);
                if (timeline == null)
                    return Error.TimelineAssetNotFound(timelineAssetPath);

                // Find the track
                TrackAsset track = timeline.GetRootTracks().FirstOrDefault(t => t.name == trackName);
                if (track == null)
                    return Error.TrackNotFound(trackName);

                TimelineClip clip = null;
                double finalDuration = 1.0; // Default duration 1 second
                double finalStartTime = startTime ?? -1;
                if (finalStartTime < 0)
                {
                    // Get the end time of the last clip on the track
                    var lastClip = track.GetClips().OrderBy(c => c.start + c.duration).LastOrDefault();
                    if (lastClip != null)
                        finalStartTime = lastClip.start + lastClip.duration;
                    else
                        finalStartTime = 0.0;
                }

                // Create clip based on track type
                if (track is AnimationTrack animTrack)
                {
                    if (!string.IsNullOrEmpty(animationClipPath))
                    {
                        AnimationClip animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationClipPath);
                        if (animClip != null)
                        {
                            clip = animTrack.CreateClip(animClip);
                            finalDuration = animClip.length;
                        }
                        else
                        {
                            return $"[Error] Animation clip not found at path '{animationClipPath}'";
                        }
                    }
                    else
                    {
                        clip = animTrack.CreateDefaultClip();
                    }
                }
                else if (track is AudioTrack audioTrack)
                {
                    if (!string.IsNullOrEmpty(audioClipPath))
                    {
                        AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipPath);
                        if (audioClip != null)
                        {
                            clip = audioTrack.CreateClip(audioClip);
                            finalDuration = audioClip.length;
                        }
                        else
                        {
                            return $"[Error] Audio clip not found at path '{audioClipPath}'";
                        }
                    }
                    else
                    {
                        clip = audioTrack.CreateDefaultClip();
                    }
                }
                else if (track is ActivationTrack activationTrack)
                {
                    clip = activationTrack.CreateDefaultClip();
                }
                else
                {
                    clip = track.CreateDefaultClip();
                }

                if (clip != null)
                {
                    clip.displayName = clipName;
                    clip.start = finalStartTime;
                    clip.duration = finalDuration;

                    EditorUtility.SetDirty(timeline);
                    AssetDatabase.SaveAssets();

                    return $"[Success] Added clip '{clipName}' to track '{trackName}' in timeline '{timelineAssetPath}'. " +
                           $"Start: {finalStartTime:F2}s, Duration: {finalDuration:F2}s";
                }
                else
                {
                    return $"[Error] Failed to create clip on track '{trackName}' of type {track.GetType().Name}";
                }
            });
        }

        private string ListTracks(string timelineAssetPath)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(timelineAssetPath))
                    return Error.TimelineAssetPathIsEmpty();

                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelineAssetPath);
                if (timeline == null)
                    return Error.TimelineAssetNotFound(timelineAssetPath);

                var tracks = timeline.GetRootTracks();
                if (!tracks.Any())
                {
                    return $"[Info] Timeline '{timelineAssetPath}' has no tracks.";
                }

                string result = $"[Timeline Tracks] {timelineAssetPath}\n";
                int index = 0;
                foreach (var track in tracks)
                {
                    var clipCount = track.GetClips().Count();
                    result += $"  [{index}] {track.name} ({track.GetType().Name}) - {clipCount} clip(s)\n";
                    
                    foreach (var clip in track.GetClips())
                    {
                        result += $"    - {clip.displayName} ({clip.start:F2}s - {(clip.start + clip.duration):F2}s)\n";
                    }
                    index++;
                }

                return result;
            });
        }

        private string AddMarkerTrack(string timelineAssetPath, string? trackName)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(timelineAssetPath))
                    return Error.TimelineAssetPathIsEmpty();

                if (string.IsNullOrEmpty(trackName))
                    return Error.TrackNameIsEmpty();

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelineAssetPath);
                if (timeline == null)
                    return Error.TimelineAssetNotFound(timelineAssetPath);

                var markerTrack = timeline.CreateTrack<MarkerTrack>(null, trackName);
                if (markerTrack == null)
                    return "[Error] Failed to create marker track.";

                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                return $"[Success] Added marker track '{trackName}' to timeline '{timelineAssetPath}'.";
            });
        }

        private string AddSignalMarker(string timelineAssetPath, string? trackName, float? time, string? signalAssetName)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(timelineAssetPath))
                    return Error.TimelineAssetPathIsEmpty();

                if (string.IsNullOrEmpty(trackName))
                    return Error.TrackNameIsEmpty();

                if (!time.HasValue)
                    return "[Error] Time value is required for addSignalMarker operation.";

                if (string.IsNullOrEmpty(signalAssetName))
                    return "[Error] Signal asset name is required for addSignalMarker operation.";

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelineAssetPath);
                if (timeline == null)
                    return Error.TimelineAssetNotFound(timelineAssetPath);

                var markerTrack = timeline.GetRootTracks().OfType<MarkerTrack>().FirstOrDefault(t => t.name == trackName);
                if (markerTrack == null)
                    return Error.TrackNotFound(trackName);

                // Create signal asset
                var signalAsset = ScriptableObject.CreateInstance<SignalAsset>();
                var signalPath = $"Assets/Signals/{signalAssetName}.signal";
                
                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(signalPath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
                
                AssetDatabase.CreateAsset(signalAsset, signalPath);

                // Add signal marker
                var marker = markerTrack.CreateMarker<SignalEmitter>(time.Value);
                if (marker != null)
                {
                    marker.asset = signalAsset;
                    EditorUtility.SetDirty(timeline);
                    AssetDatabase.SaveAssets();
                    return $"[Success] Added signal marker '{signalAssetName}' at time {time.Value}s to track '{trackName}'.";
                }

                return "[Error] Failed to create signal marker.";
            });
        }

        private string GetMarkers(string timelineAssetPath)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(timelineAssetPath))
                    return Error.TimelineAssetPathIsEmpty();

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelineAssetPath);
                if (timeline == null)
                    return Error.TimelineAssetNotFound(timelineAssetPath);

                var markerTracks = timeline.GetRootTracks().OfType<MarkerTrack>();
                var info = new TimelineMarkerInfo
                {
                    timelineAssetPath = timelineAssetPath
                };

                foreach (var track in markerTracks)
                {
                    var trackInfo = new MarkerTrackInfo
                    {
                        trackName = track.name
                    };

                    foreach (var marker in track.GetMarkers())
                    {
                        trackInfo.markers.Add(new MarkerInfo
                        {
                            time = marker.time,
                            markerType = marker.GetType().Name
                        });
                    }

                    info.markerTracks.Add(trackInfo);
                }

                return $"[Success] Retrieved marker information from timeline '{timelineAssetPath}'.\n{JsonUtility.ToJson(info, true)}";
            });
        }
    }
} 