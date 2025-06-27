#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.IO;
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_Scene
    {
        [McpPluginTool
        (
            "Scene_Operate",
            Title = "Operate on Scenes - Get info, control camera, capture scenes"
        )]
        [Description(@"Operate on comprehensive scene functions including:
- getLoaded: Retrieve list of currently loaded scenes
- getHierarchy: Extract scene hierarchy with specified depth
- cameraControl: Control scene camera position, rotation, and field of view
- capture: Capture scene from different viewpoints")]
        public string Operations
        (
            [Description("Operation type: 'getLoaded', 'getHierarchy', 'cameraControl', 'capture'")]
            string operation,
            [Description("For getHierarchy: depth of hierarchy to include. For capture: capture resolution")]
            int depth = 3,
            [Description("For getHierarchy: scene name (empty for active scene). For cameraControl: camera position as 'x,y,z'")]
            string? sceneName = null,
            [Description("For cameraControl: camera rotation as 'x,y,z' in Euler angles")]
            string? rotation = null,
            [Description("For cameraControl: camera field of view value")]
            string? fieldOfView = null,
            [Description("For cameraControl: target GameObject name to look at")]
            string? targetName = null,
            [Description("For cameraControl: target tag to look at (finds nearest)")]
            string? targetTag = null,
            [Description("For capture: background color in hex format like '#E5E5E5'")]
            string? backgroundColorHex = "#E5E5E5",
            [Description("For capture: file name for saved images")]
            string? fileName = "",
            [Description("For capture: capture front view")]
            bool captureFrontView = true,
            [Description("For capture: capture side view")]
            bool captureSideView = false,
            [Description("For capture: capture top view")]
            bool captureTopView = false,
            [Description("For capture: capture isometric view")]
            bool captureIsometricView = false
        )
        {
            return operation.ToLower() switch
            {
                "getloaded" => GetLoadedScenes(),
                "gethierarchy" => GetSceneHierarchy(depth, sceneName),
                "cameracontrol" => ControlCamera(sceneName, rotation, fieldOfView, targetName, targetTag),
                "capture" => CaptureScene(depth, backgroundColorHex, fileName, captureFrontView, captureSideView, captureTopView, captureIsometricView),
                _ => "[Error] Invalid operation. Valid operations: 'getLoaded', 'getHierarchy', 'cameraControl', 'capture'"
            };
        }

        private string GetLoadedScenes()
        {
            return MainThread.Instance.Run(() =>
            {
                return $"[Success] " + LoadedScenes;
            });
        }

        private string GetSceneHierarchy(int includeChildrenDepth, string? loadedSceneName)
        {
            return MainThread.Instance.Run(() =>
            {
                var scene = string.IsNullOrEmpty(loadedSceneName)
                    ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                    : UnityEngine.SceneManagement.SceneManager.GetSceneByName(loadedSceneName);

                if (!scene.IsValid())
                    return Error.NotFoundSceneWithName(loadedSceneName);

                return scene.ToMetadata(includeChildrenDepth: includeChildrenDepth).Print();
            });
        }

        private string ControlCamera(string? position, string? rotation, string? fieldOfView, string? targetName, string? targetTag)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    Camera cam = Camera.main;
                    if (cam == null)
                    {
                        var allCams = GameObject.FindObjectsOfType<Camera>();
                        if (allCams.Length == 1)
                        {
                            cam = allCams[0];
                            cam.tag = "MainCamera";
                        }
                    }
                    if (cam == null)
                        return "[Error] No main camera found in current scene and cannot auto-assign.";

                    if (!string.IsNullOrEmpty(position))
                    {
                        var posArr = Array.ConvertAll(position.Split(','), float.Parse);
                        if (posArr.Length == 3)
                            cam.transform.position = new Vector3(posArr[0], posArr[1], posArr[2]);
                    }
                    if (!string.IsNullOrEmpty(rotation))
                    {
                        var rotArr = Array.ConvertAll(rotation.Split(','), float.Parse);
                        if (rotArr.Length == 3)
                            cam.transform.eulerAngles = new Vector3(rotArr[0], rotArr[1], rotArr[2]);
                    }
                    if (!string.IsNullOrEmpty(fieldOfView))
                    {
                        if (float.TryParse(fieldOfView, out float fov))
                            cam.fieldOfView = fov;
                    }
                    string lookAtMsg = "";
                    if (!string.IsNullOrEmpty(targetName))
                    {
                        var target = GameObject.Find(targetName);
                        if (target != null)
                        {
                            cam.transform.LookAt(target.transform);
                            lookAtMsg = $"\nLooking at target: {targetName}";
                        }
                        else
                        {
                            lookAtMsg = $"\n[Warning] Target GameObject not found: {targetName}";
                        }
                    }
                    else if (!string.IsNullOrEmpty(targetTag))
                    {
                        var taggedObjects = GameObject.FindGameObjectsWithTag(targetTag);
                        if (taggedObjects != null && taggedObjects.Length > 0)
                        {
                            GameObject nearest = null;
                            float minDist = float.MaxValue;
                            foreach (var obj in taggedObjects)
                            {
                                float dist = Vector3.Distance(cam.transform.position, obj.transform.position);
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    nearest = obj;
                                }
                            }
                            if (nearest != null)
                            {
                                cam.transform.LookAt(nearest.transform);
                                lookAtMsg = $"\nLooking at nearest object with tag '{targetTag}': {nearest.name}";
                            }
                        }
                        else
                        {
                            lookAtMsg = $"\n[Warning] No objects found with tag '{targetTag}'";
                        }
                    }

                    return $"[Success] Camera updated\nPosition: {cam.transform.position}\nRotation: {cam.transform.eulerAngles}\nField of View: {cam.fieldOfView}{lookAtMsg}";
                }
                catch (Exception ex)
                {
                    return $"[Error] Camera control failed: {ex.Message}";
                }
            });
        }

        private string CaptureScene(int captureResolution, string? backgroundColorHex, string? fileName, 
            bool captureFrontView, bool captureSideView, bool captureTopView, bool captureIsometricView)
        {
            return MainThread.Instance.Run(() =>
            {
                var scene = SceneUtils.GetActiveScene();
                if (scene == null)
                    return "[Error] No active scene found.";

                // Parse background color
                Color backgroundColor = Color.white;
                if (!string.IsNullOrEmpty(backgroundColorHex) && !ColorUtility.TryParseHtmlString(backgroundColorHex, out backgroundColor))
                    backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1);

                string sceneName = scene.name;
                string timeStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseFileName = string.IsNullOrEmpty(fileName) ? $"{sceneName}_{timeStamp}" : fileName;

                // Capture different viewpoints
                string resultMsg = "";
                if (captureFrontView)
                    resultMsg += CaptureSceneView(Vector3.forward, Vector3.up, "Front");
                if (captureSideView)
                    resultMsg += CaptureSceneView(Vector3.right, Vector3.up, "Side");
                if (captureTopView)
                    resultMsg += CaptureSceneView(Vector3.up, Vector3.forward, "Top");
                if (captureIsometricView)
                    resultMsg += CaptureSceneView(new Vector3(1, 1, 1).normalized, Vector3.up, "Isometric");

                return resultMsg;

                // Internal method: capture specified viewpoint
                string CaptureSceneView(Vector3 viewDir, Vector3 upDir, string viewName)
                {
                    // Find camera in scene
                    var cam = Camera.main;
                    if (cam == null)
                    {
                        // If no main camera, find any camera
                        cam = UnityEngine.Object.FindObjectOfType<Camera>();
                    }
                    
                    if (cam == null)
                    {
                        return $"[Error] No camera found in scene '{scene.name}'. Please add a camera to the scene.";
                    }

                    // Save only the camera settings that need temporary modification
                    var originalTargetTexture = cam.targetTexture;

                    RenderTexture rt = null;
                    Texture2D tex = null;

                    try
                    {
                        // Use camera's current settings for screenshot, don't modify camera parameters
                        // Only temporarily set render target texture
                        rt = new RenderTexture(captureResolution, captureResolution, 24);
                        cam.targetTexture = rt;
                        cam.Render();

                        // Save as PNG file
                        RenderTexture.active = rt;
                        tex = new Texture2D(captureResolution, captureResolution, TextureFormat.RGBA32, false);
                        tex.ReadPixels(new Rect(0, 0, captureResolution, captureResolution), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;

                        // Generate file path
                        var tempPath = System.IO.Path.GetTempPath();
                        var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var filename = $"{baseFileName}_{viewName}_{timestamp}.png";
                        var fullPath = System.IO.Path.Combine(tempPath, filename);

                        // Save PNG file
                        var pngBytes = tex.EncodeToPNG();
                        System.IO.File.WriteAllBytes(fullPath, pngBytes);

                        return $"\n[Success] {viewName} view saved to: {fullPath}";
                    }
                    finally
                    {
                        // Always restore camera settings and cleanup resources
                        RestoreCameraSettings();
                        
                        if (tex != null)
                            UnityEngine.Object.DestroyImmediate(tex);
                        if (rt != null)
                        {
                            rt.Release();
                            UnityEngine.Object.DestroyImmediate(rt);
                        }
                    }

                    // Internal method to restore camera settings
                    void RestoreCameraSettings()
                    {
                        cam.targetTexture = originalTargetTexture;
                    }
                }
            });
        }
    }
} 