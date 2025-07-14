// #pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
// using System.ComponentModel;
// using System.Linq;
// using com.MiAO.Unity.MCP.Common;
// using com.MiAO.Unity.MCP.Utils; 
// using UnityEngine;
// using UnityEngine.Playables;
// using UnityEngine.Timeline;

// namespace com.MiAO.Unity.MCP.Essential.Tools
// {
//     public partial class Tool_Timeline
//     {
//         [McpPluginTool
//         (
//             "Timeline_PlayControl",
//             Title = "Control Timeline playback"
//         )]
//         [Description("Control the playback of Timeline (play, pause, stop, set time).")]
//         public string PlayControl
//         (
//             [Description("Control action: play, pause, stop, settime.")]
//             string action,
//             [Description("GameObject name with PlayableDirector component. If empty, will find first available.")]
//             string gameObjectName = "",
//             [Description("Time to set (only used with 'settime' action).")]
//             double time = 0.0
//         )
//         => MainThread.Run(() =>
//         {
//             // Find PlayableDirector
//             PlayableDirector director = null;
            
//             if (!string.IsNullOrEmpty(gameObjectName))
//             {
//                 GameObject targetGO = GameObject.Find(gameObjectName);
//                 if (targetGO == null)
//                     return Error.GameObjectNotFound(gameObjectName);
                
//                 director = targetGO.GetComponent<PlayableDirector>();
//             }
//             else
//             {
//                 // Find first PlayableDirector in scene
//                 director = Object.FindObjectOfType<PlayableDirector>();
//             }

//             if (director == null)
//                 return Error.PlayableDirectorNotFound();

//             // Execute control action
//             switch (action?.ToLower())
//             {
//                 case "play":
//                     director.Play();
//                     return $"[Success] Timeline playback started. Current time: {director.time:F2}s";
                    
//                 case "pause":
//                     director.Pause();
//                     return $"[Success] Timeline playback paused. Current time: {director.time:F2}s";
                    
//                 case "stop":
//                     director.Stop();
//                     return $"[Success] Timeline playback stopped. Time reset to 0";
                    
//                 case "settime":
//                     if (time < 0)
//                         return Error.InvalidTimeValue(time);
                    
//                     director.time = time;
//                     director.Evaluate();
//                     return $"[Success] Timeline time set to {time:F2}s";
                    
//                 default:
//                     return "[Error] Invalid action. Valid actions: play, pause, stop, settime";
//             }
//         });

//         [McpPluginTool
//         (
//             "Timeline_GetStatus",
//             Title = "Get Timeline status"
//         )]
//         [Description("Get the current status of Timeline playback.")]
//         public string GetStatus
//         (
//             [Description("GameObject name with PlayableDirector component. If empty, will find first available.")]
//             string gameObjectName = ""
//         )
//         => MainThread.Run(() =>
//         {
//             // Find PlayableDirector
//             PlayableDirector director = null;
            
//             if (!string.IsNullOrEmpty(gameObjectName))
//             {
//                 GameObject targetGO = GameObject.Find(gameObjectName);
//                 if (targetGO == null)
//                     return Error.GameObjectNotFound(gameObjectName);
                
//                 director = targetGO.GetComponent<PlayableDirector>();
//             }
//             else
//             {
//                 // Find first PlayableDirector in scene
//                 director = Object.FindObjectOfType<PlayableDirector>();
//             }

//             if (director == null)
//                 return Error.PlayableDirectorNotFound();

//             string playbackState = director.state == PlayState.Playing ? "Playing" :
//                                  director.state == PlayState.Paused ? "Paused" : "Stopped";

//             TimelineAsset timeline = director.playableAsset as TimelineAsset;
//             string timelineInfo = timeline != null ? $"Timeline: {timeline.name}, Tracks: {timeline.GetRootTracks().Count()}" : "No Timeline assigned";

//             return $"[Status] GameObject: {director.gameObject.name}\n" +
//                    $"State: {playbackState}\n" +
//                    $"Current Time: {director.time:F2}s\n" +
//                    $"Duration: {director.duration:F2}s\n" +
//                    $"{timelineInfo}";
//         });
//     }
// } 