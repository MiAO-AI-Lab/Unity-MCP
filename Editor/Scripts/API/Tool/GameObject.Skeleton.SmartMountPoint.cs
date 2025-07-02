#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Editor.API
{
    [Serializable]
    public class MountPointInfo
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public string description;
        public bool isLocalSpace;
        public Color visualizationColor;
        
        public MountPointInfo(string name, Vector3 position, Vector3 rotation, Vector3 scale, string description = "", bool isLocalSpace = true)
        {
            this.name = name;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.description = description;
            this.isLocalSpace = isLocalSpace;
            this.visualizationColor = Color.white; // Default color, will be reassigned later
        }
        

    }

    [Serializable]
    public class SmartMountResult
    {
        public bool success;
        public string message;
        public List<MountPointInfo> mountPoints;
        
        public SmartMountResult()
        {
            mountPoints = new List<MountPointInfo>();
        }
    }
    
    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_Skeleton_SmartMountPoint",
            Title = "Skeleton - Smart Mount Point Tool - AI-Assisted Mount Point Placement"
        )]
        [Description(@"Smart mount point tool with AI-assisted mount point position analysis using visualization methods.
Features include:
1. Prefab analysis and mount point position calculation
2. Multi-angle scene screenshots and visualization (colored sphere markers for mount points)
3. AI image recognition verification of mount point position reasonableness (using built-in structured analysis templates)
4. Intelligent iterative optimization of mount point positions
5. Return final Transform position information (without creating GameObjects)

Workflow:
1. Load and analyze target prefab
2. Calculate optimal mount point positions based on requirements
3. Generate multi-angle visualization screenshots (temporarily create colored sphere markers)
4. Use AI structured analysis of current mount point position reasonableness
5. Intelligently adjust mount point positions based on AI's standardized suggestions
6. Return final mount point Transform position information, users can create IK GameObjects according to project structure

AI Analysis Features:
- Uses fixed structured prompt templates to ensure consistent output format
- AI provides clear YES/NO judgments and standardized adjustment suggestions
- Supports precise adjustments like UP/DOWN/LEFT/RIGHT/FORWARD/BACKWARD/CENTER/TO_GRIP/TO_TRIGGER")]
        public async Task<string> ExecuteSmartMount
        (
            [Description("Path to the prefab asset that needs mount points, format like 'Assets/Prefabs/Weapons/P1911_RECEIVER1.prefab'")]
            string prefabAssetPath,
            
            [Description("List of mount point information in JSON format. Example: [{\"name\":\"RightHandIK\",\"position\":{\"x\":0.02,\"y\":-0.08,\"z\":0.02},\"rotation\":{\"x\":0,\"y\":0,\"z\":0},\"scale\":{\"x\":1,\"y\":1,\"z\":1},\"description\":\"Right hand grip point\",\"isLocalSpace\":true}]")]
            string mountPointsJson,
            
            [Description("Detailed description of mount point requirements, example: 'This is a P1911 pistol, need to adjust left and right hand IK mount points to conform to standard gun-holding posture. Right hand should grip the handle, left hand should provide auxiliary support.'")]
            string mountInstructions,
            
            [Description("Additional analysis requirements, example: 'Focus on ergonomics' or 'Ensure standard gun-holding posture'. AI will use built-in structured analysis templates.")]
            string aiAnalysisPrompt,
            
            [Description("Whether to enable iterative optimization mode, if true then automatically adjust mount point positions based on AI analysis results")]
            bool enableIterativeOptimization = true,
            
            [Description("Maximum number of iterations to prevent infinite loops")]
            int maxIterations = 3,
            
            [Description("Screenshot resolution")]
            int captureResolution = 1024,
            
            [Description("Background color (hexadecimal format)")]
            string backgroundColorHex = "#E5E5E5"
        )
        {
            var result = new SmartMountResult();
            
            try
            {
                // 1. Validate input parameters
                if (string.IsNullOrEmpty(prefabAssetPath))
                {
                    result.success = false;
                    result.message = Error.PrefabPathIsEmpty();
                    return SerializeResult(result);
                }

                if (string.IsNullOrEmpty(mountPointsJson))
                {
                    result.success = false;
                    result.message = Error.MountPointsJsonIsEmpty();
                    return SerializeResult(result);
                }

                if (string.IsNullOrEmpty(mountInstructions))
                {
                    result.success = false;
                    result.message = Error.MountInstructionsIsEmpty();
                    return SerializeResult(result);
                }

                if (string.IsNullOrEmpty(aiAnalysisPrompt))
                {
                    result.success = false;
                    result.message = Error.AIAnalysisPromptIsEmpty();
                    return SerializeResult(result);
                }

                if (maxIterations <= 0 || maxIterations > 10)
                {
                    result.success = false;
                    result.message = Error.InvalidMaxIterations(maxIterations);
                    return SerializeResult(result);
                }

                if (captureResolution <= 0 || captureResolution > 4096)
                {
                    result.success = false;
                    result.message = Error.InvalidCaptureResolution(captureResolution);
                    return SerializeResult(result);
                }

                // 2. Parse mount point information
                List<MountPointInfo> targetMountPoints;
                try
                {
                    targetMountPoints = ParseMountPointsJson(mountPointsJson);
                }
                catch (Exception ex)
                {
                    result.success = false;
                    result.message = Error.MountPointsJsonParseFailed(ex.Message);
                    return SerializeResult(result);
                }

                // 3. Execute smart mount point process
                result = await ExecuteSmartMountProcess(
                    prefabAssetPath, 
                    targetMountPoints, 
                    mountInstructions, 
                    aiAnalysisPrompt,
                    enableIterativeOptimization,
                    maxIterations,
                    captureResolution,
                    backgroundColorHex
                );

                return SerializeResult(result);
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = Error.SmartMountProcessFailed(ex.Message);
                return SerializeResult(result);
            }
        }

        private async Task<SmartMountResult> ExecuteSmartMountProcess(
            string prefabAssetPath, 
            List<MountPointInfo> targetMountPoints, 
            string mountInstructions, 
            string aiAnalysisPrompt,
            bool enableIterativeOptimization,
            int maxIterations,
            int captureResolution,
            string backgroundColorHex)
        {
            var result = new SmartMountResult();
            GameObject sceneInstance = null;
            Camera captureCamera = null;
            
                        try
            {
                // 1. Instantiate prefab in scene for screenshot and analysis
                var instantiateResult = InstantiatePrefabInScene(prefabAssetPath);
                if (!instantiateResult.success)
                    {
                        result.success = false;
                    result.message = instantiateResult.message;
                        return result;
                    }
                sceneInstance = instantiateResult.instance;
                result.message += "Scene instantiated\n";
                var instanceName = MainThread.Instance.Run(() => sceneInstance.name);
                Debug.Log($"[SmartMountPoint] Prefab instantiated in scene: {instanceName}");

                // 2. Apply mount points to scene instance and add visualization markers
                var applySceneResult = ApplyMountPointsToSceneInstance(sceneInstance, targetMountPoints);
                if (!applySceneResult.success)
                    {
                        result.success = false;
                        result.message = applySceneResult.message;
                        return result;
                    }
                result.message += "Scene mount points applied\n";

                // 3. Create capture camera
                captureCamera = CreateCaptureCamera(sceneInstance, captureResolution, backgroundColorHex);

                // 4. Iterative optimization process
                int iteration = 0;
                var currentMountPoints = new List<MountPointInfo>(targetMountPoints);
                bool needsAdjustment = true;
                
                while (iteration < maxIterations && needsAdjustment)
                {
                    iteration++;
                    result.message += $"\n=== Iteration {iteration} ===\n";
                    
                    // Update scene instance mount point positions
                    UpdateSceneInstanceMountPoints(sceneInstance, currentMountPoints);
                    
                    // Capture multi-angle screenshots
                    var captureResult = CaptureMultipleViews(captureCamera, sceneInstance, captureResolution, $"Mount_Iteration_{iteration}");
                    if (captureResult.success)
                    {
                        result.message += $"Captured {captureResult.imagePaths.Count} images\n";
                    }

                    // AI analysis of mount point reasonableness
                    if (captureResult.imagePaths.Count > 0)
                    {
                        var analysisResult = await AnalyzeMountPointsWithAI(captureResult.imagePaths, $"{mountInstructions}. User hint: {aiAnalysisPrompt}", currentMountPoints);
                        
                        if (analysisResult.suggestions.Count > 0)
                        {
                            result.message += $"AI suggestions: {string.Join(", ", analysisResult.suggestions)}\n";
                        }

                        // If iterative optimization is not enabled, or AI thinks it's reasonable, stop iteration
                        if (!enableIterativeOptimization || analysisResult.isReasonable)
                        {
                            needsAdjustment = false;
                        }
                        else
                        {
                            // Adjust mount point positions based on AI suggestions
                            var adjustResult = AdjustMountPointsBasedOnAI(currentMountPoints, analysisResult.suggestions);
                            if (adjustResult.success)
                            {
                                currentMountPoints = adjustResult.adjustedMountPoints;
                                
                                // Show before and after position comparison
                                for (int i = 0; i < Math.Min(targetMountPoints.Count, currentMountPoints.Count); i++)
                                {
                                    var original = targetMountPoints[i];
                                    var adjusted = currentMountPoints[i];
                                    result.message += $"  {original.name}: ({original.position.x:F3},{original.position.y:F3},{original.position.z:F3}) → ({adjusted.position.x:F3},{adjusted.position.y:F3},{adjusted.position.z:F3})\n";
                                }
                            }
                            else
                            {
                                needsAdjustment = false;
                            }
                        }
                    }

                    if (iteration >= maxIterations)
                    {
                        break;
                    }
                }

                // 5. Complete analysis, return final mount point position information (do not modify prefab)
                result.mountPoints = currentMountPoints;
                    result.success = true;

                    return result;
                }
                catch (Exception ex)
                {
                    result.success = false;
                    result.message = Error.SmartMountProcessFailed(ex.Message);
                    return result;
                }
            finally
            {
                // Clean up temporary objects
                CleanupTemporaryObjects(sceneInstance, captureCamera);
            }
        }

        private List<MountPointInfo> ParseMountPointsJson(string mountPointsJson)
        {
            var result = new List<MountPointInfo>();
            
            try
            {
                // Use Unity's JsonUtility for better JSON parsing
                if (mountPointsJson.StartsWith("["))
                {
                    // Handle array format
                    var wrapper = $"{{\"items\":{mountPointsJson}}}";
                    var container = JsonUtility.FromJson<MountPointContainer>(wrapper);
                    if (container?.items != null)
                    {
                        result.AddRange(container.items);
                    }
                }
                
                // If JSON parsing fails, use simplified string matching as fallback
                if (result.Count == 0)
                {
                    if (mountPointsJson.Contains("RightHandIK") || mountPointsJson.Contains("righthand"))
                    {
                        result.Add(new MountPointInfo("RightHandIK", new Vector3(0.02f, -0.08f, 0.02f), Vector3.zero, Vector3.one, "Right hand grip point"));
                    }
                    if (mountPointsJson.Contains("LeftHandIK") || mountPointsJson.Contains("lefthand"))
                    {
                        result.Add(new MountPointInfo("LeftHandIK", new Vector3(-0.02f, -0.08f, 0.08f), Vector3.zero, Vector3.one, "Left hand support point"));
                    }
                }
                
                // Assign colors to mount points in sequence
                for (int i = 0; i < result.Count; i++)
                {
                    result[i].visualizationColor = GetColorForMountPoint(i);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SmartMountPoint] {Error.MountPointsJsonParseFailed(ex.Message)}");
                // Provide default mount points as fallback
                result.Add(new MountPointInfo("RightHandIK", new Vector3(0.02f, -0.08f, 0.02f), Vector3.zero, Vector3.one, "Right hand grip point"));
                result.Add(new MountPointInfo("LeftHandIK", new Vector3(-0.02f, -0.08f, 0.08f), Vector3.zero, Vector3.one, "Left hand support point"));
                
                // Also assign colors for default mount points
                for (int i = 0; i < result.Count; i++)
                {
                    result[i].visualizationColor = GetColorForMountPoint(i);
                }
            }
            
            return result;
        }

        [Serializable]
        private class MountPointContainer
        {
            public MountPointInfo[] items;
        }

        // Assign colors to mount points in sequence
        private static Color GetColorForMountPoint(int index)
        {
            Color[] colors = {
                Color.red,      // 1st mount point - Red
                Color.blue,     // 2nd mount point - Blue
                Color.green,    // 3rd mount point - Green
                Color.yellow,   // 4th mount point - Yellow
                Color.magenta,  // 5th mount point - Magenta
                Color.cyan,     // 6th mount point - Cyan
                Color.white,    // 7th mount point - White
                Color.gray      // 8th mount point - Gray
            };
            
            // If index exceeds range, cycle through colors
            return colors[index % colors.Length];
        }





        private void CreateVisualizationMarker(GameObject mountPoint, Color markerColor = default)
        {
            // Check if visualization marker already exists
            var existingMarker = mountPoint.transform.Find("_VisualizationMarker");
            if (existingMarker != null)
            {
                // Update existing marker color if provided
                if (markerColor != default(Color))
                {
                    var existingRenderer = existingMarker.GetComponent<Renderer>();
                    if (existingRenderer != null && existingRenderer.material != null)
                    {
                        existingRenderer.material.color = markerColor;
                    }
                }
                return; // Marker already exists
            }

            // Create small sphere as visualization marker
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "_VisualizationMarker";
            marker.transform.SetParent(mountPoint.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 0.02f; // Small sphere

            // Set to not save to prefab - only for editor visualization
            marker.hideFlags = HideFlags.DontSave;

            // Set material color
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = markerColor != default(Color) ? markerColor : Color.red;
                renderer.material = material;
                // Material also not saved
                material.hideFlags = HideFlags.DontSave;
            }

            // Remove collider (no need for physics interaction)
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private string SerializeResult(SmartMountResult result)
        {
            try
            {
                return JsonUtility.ToJson(result, true);
            }
            catch (Exception ex)
            {
                return $"{{\"success\":false,\"message\":\"{Error.SerializationFailed(ex.Message).Replace("\"", "\\\"")}\"}}";
            }
        }

        // Additional data structures
        [Serializable]
        private class InstantiateResult
        {
            public bool success;
            public string message;
            public GameObject instance;
        }

        [Serializable]
        private class CaptureResult
        {
            public bool success;
            public string message;
            public List<string> imagePaths = new List<string>();
        }

        [Serializable]
        private class AIAnalysisResult
        {
            public string analysis;
            public bool isReasonable;
            public List<string> suggestions = new List<string>();
        }

        [Serializable]
        private class AdjustmentResult
        {
            public bool success;
            public string message;
            public List<MountPointInfo> adjustedMountPoints = new List<MountPointInfo>();
        }

        [Serializable]
        private class ApplyResult
        {
            public bool success;
            public string message;
        }

        // Instantiate prefab in scene
        private InstantiateResult InstantiatePrefabInScene(string prefabAssetPath)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    if (!File.Exists(prefabAssetPath))
                        return new InstantiateResult { success = false, message = Error.PrefabFileNotFound(prefabAssetPath) };

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
                    if (prefab == null)
                        return new InstantiateResult { success = false, message = Error.PrefabLoadFailed(prefabAssetPath) };

                    var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (instance == null)
                        return new InstantiateResult { success = false, message = Error.PrefabInstantiateFailed(prefabAssetPath) };

                    // Move to origin position
                    instance.transform.position = Vector3.zero;
                    instance.transform.rotation = Quaternion.identity;
                    instance.name = $"SmartMount_{System.DateTime.Now:HHmmss}_{prefab.name}";

                    return new InstantiateResult { success = true, message = "[Success] Instantiation successful", instance = instance };
                }
                catch (Exception ex)
                {
                    return new InstantiateResult { success = false, message = Error.SmartMountProcessFailed(ex.Message) };
                }
            });
        }

        // Apply mount points to scene instance
        private ApplyResult ApplyMountPointsToSceneInstance(GameObject sceneInstance, List<MountPointInfo> mountPoints)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    if (sceneInstance == null)
                        return new ApplyResult { success = false, message = Error.SceneInstanceNotFound() };

                    if (mountPoints == null || mountPoints.Count == 0)
                        return new ApplyResult { success = false, message = Error.NoMountPointsToApply() };

                    foreach (var mountPoint in mountPoints)
                    {
                        var existingTransform = sceneInstance.transform.Find(mountPoint.name);
                        GameObject mountGameObject;

                        if (existingTransform != null)
                        {
                            mountGameObject = existingTransform.gameObject;
                        }
                        else
                        {
                            mountGameObject = new GameObject(mountPoint.name);
                            mountGameObject.transform.SetParent(sceneInstance.transform, false);
                        }

                        // Set Transform
                        if (mountPoint.isLocalSpace)
                        {
                            mountGameObject.transform.localPosition = mountPoint.position;
                            mountGameObject.transform.localEulerAngles = mountPoint.rotation;
                            mountGameObject.transform.localScale = mountPoint.scale;
                        }
                        else
                        {
                            mountGameObject.transform.position = mountPoint.position;
                            mountGameObject.transform.eulerAngles = mountPoint.rotation;
                            mountGameObject.transform.localScale = mountPoint.scale;
                        }

                        // Add visualization marker with specific color
                        try
                        {
                            CreateVisualizationMarker(mountGameObject, mountPoint.visualizationColor);
                        }
                        catch (Exception markerEx)
                        {
                            Debug.LogWarning($"Failed to create visualization marker for {mountPoint.name}: {markerEx.Message}");
                        }
                    }

                    return new ApplyResult { success = true, message = "[Success] Mount points applied successfully" };
                }
                catch (Exception ex)
                {
                    return new ApplyResult { success = false, message = Error.ApplyMountPointsFailed(ex.Message) };
                }
            });
        }

        // Create capture camera
        private Camera CreateCaptureCamera(GameObject target, int resolution, string backgroundColorHex)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    if (target == null)
                        throw new ArgumentNullException(nameof(target), "Target GameObject cannot be null");

                    var cameraGO = new GameObject("SmartMount_CaptureCamera");
                    var camera = cameraGO.AddComponent<Camera>();

                    // Set camera properties
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    if (ColorUtility.TryParseHtmlString(backgroundColorHex, out Color bgColor))
                        camera.backgroundColor = bgColor;
                    else
                    {
                        Debug.LogWarning($"Invalid background color: {backgroundColorHex}, using default");
                        camera.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1);
                    }

                    camera.orthographic = false;
                    camera.fieldOfView = 60f;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 100f;

                    // Calculate target bounds and position camera
                    var bounds = CalculateObjectBounds(target);
                    var distance = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 2f;
                    camera.transform.position = bounds.center + Vector3.back * distance + Vector3.up * distance * 0.3f;
                    camera.transform.LookAt(bounds.center);

                    return camera;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to create capture camera: {ex.Message}");
                    throw new InvalidOperationException(Error.CameraCreationFailed(ex.Message), ex);
                }
            });
        }

        // Calculate object bounds
        private Bounds CalculateObjectBounds(GameObject obj)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    if (obj == null)
                        throw new ArgumentNullException(nameof(obj), "GameObject cannot be null");

                    var renderers = obj.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0)
                    {
                        Debug.LogWarning($"No renderers found on {obj.name}, using default bounds");
                        return new Bounds(obj.transform.position, Vector3.one);
                    }

                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    return bounds;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to calculate object bounds: {ex.Message}");
                    throw new InvalidOperationException(Error.BoundsCalculationFailed(ex.Message), ex);
                }
            });
        }

        // Update scene instance mount point positions
        private void UpdateSceneInstanceMountPoints(GameObject sceneInstance, List<MountPointInfo> mountPoints)
        {
            MainThread.Instance.Run(() =>
            {
                foreach (var mountPoint in mountPoints)
                {
                    var mountTransform = sceneInstance.transform.Find(mountPoint.name);
                    if (mountTransform != null)
                    {
                        if (mountPoint.isLocalSpace)
                        {
                            mountTransform.localPosition = mountPoint.position;
                            mountTransform.localEulerAngles = mountPoint.rotation;
                            mountTransform.localScale = mountPoint.scale;
                        }
                        else
                        {
                            mountTransform.position = mountPoint.position;
                            mountTransform.eulerAngles = mountPoint.rotation;
                            mountTransform.localScale = mountPoint.scale;
                        }
                    }
                }
            });
        }

        // Capture multi-angle views
        private CaptureResult CaptureMultipleViews(Camera camera, GameObject target, int resolution, string baseName)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    var result = new CaptureResult { success = true };
                    var bounds = CalculateObjectBounds(target);
                    var distance = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 2f;

                    Debug.Log($"[SmartMountPoint] Starting multi-angle capture for {baseName}");
                    Debug.Log($"[SmartMountPoint] Target bounds: {bounds}, Distance: {distance}");

                    // Capture front view
                    var frontImagePath = CaptureFromDirection(camera, bounds.center, Vector3.back * distance + Vector3.up * distance * 0.3f, bounds.center, resolution, $"{baseName}_Front");
                    if (!string.IsNullOrEmpty(frontImagePath))
                    {
                        result.imagePaths.Add(frontImagePath);
                        Debug.Log($"[SmartMountPoint] Front view captured successfully");
                    }
                    else
                    {
                        Debug.LogWarning($"[SmartMountPoint] Front view capture failed");
                    }

                    // Capture side view
                    var sideImagePath = CaptureFromDirection(camera, bounds.center, Vector3.right * distance + Vector3.up * distance * 0.3f, bounds.center, resolution, $"{baseName}_Side");
                    if (!string.IsNullOrEmpty(sideImagePath))
                    {
                        result.imagePaths.Add(sideImagePath);
                        Debug.Log($"[SmartMountPoint] Side view captured successfully");
                    }
                    else
                    {
                        Debug.LogWarning($"[SmartMountPoint] Side view capture failed");
                    }

                    if (result.imagePaths.Count == 0)
                    {
                        result.success = false;
                        result.message = Error.NoImagesForAIAnalysis();
                        Debug.LogError("[SmartMountPoint] No images were captured successfully");
                    }
                    else
                    {
                        result.message = $"[Success] Successfully captured {result.imagePaths.Count} images";
                        Debug.Log($"[SmartMountPoint] Multi-angle capture completed: {result.imagePaths.Count} images");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SmartMountPoint] Screenshot process failed: {ex.Message}");
                    return new CaptureResult { success = false, message = Error.ScreenshotCaptureFailed(ex.Message) };
                }
            });
        }

        // Capture image from specified direction
        private string CaptureFromDirection(Camera camera, Vector3 center, Vector3 offset, Vector3 lookAt, int resolution, string fileName)
        {
            try
            {
                camera.transform.position = center + offset;
                camera.transform.LookAt(lookAt);

                var rt = new RenderTexture(resolution, resolution, 24);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                // Use a safer temp path and file name without special characters
                var tempPath = System.IO.Path.GetTempPath();
                var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var safeFileName = fileName.Replace(" ", "_").Replace("-", "_");
                var fullPath = System.IO.Path.Combine(tempPath, $"{safeFileName}_{timestamp}.png");

                var pngBytes = tex.EncodeToPNG();
                if (pngBytes != null && pngBytes.Length > 0)
                {
                    System.IO.File.WriteAllBytes(fullPath, pngBytes);
                    
                    // Ensure file is completely written
                    System.Threading.Thread.Sleep(100);
                    
                    // Verify file exists and has content
                    if (System.IO.File.Exists(fullPath))
                    {
                        var fileInfo = new System.IO.FileInfo(fullPath);
                        if (fileInfo.Length > 0)
                        {
                            Debug.Log($"[SmartMountPoint] Screenshot saved successfully: {fullPath} (Size: {fileInfo.Length} bytes)");
                            
                            camera.targetTexture = null;
                            rt.Release();
                            UnityEngine.Object.DestroyImmediate(rt);
                            UnityEngine.Object.DestroyImmediate(tex);
                            
                            return fullPath;
                        }
                        else
                        {
                            Debug.LogError($"[SmartMountPoint] Image file is empty: {fullPath}");
                        }
                    }
                                            else
                        {
                            Debug.LogError(Error.ImageFileVerificationFailed(fullPath));
                        }
                    }
                    else
                    {
                        Debug.LogError(Error.TemporaryFileCreationFailed(fileName, "Failed to encode PNG data"));
                    }

                camera.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError(Error.ScreenshotCaptureFailed(ex.Message));
                return null;
            }
        }

        // Use AI to analyze mount point positions
        private async Task<AIAnalysisResult> AnalyzeMountPointsWithAI(List<string> imagePaths, string userContext, List<MountPointInfo> mountPoints)
        {
            try
            {
                // Filter out null or invalid image paths
                var validImagePaths = imagePaths?.Where(path => !string.IsNullOrEmpty(path) && System.IO.File.Exists(path)).ToList();
                
                if (validImagePaths == null || validImagePaths.Count == 0)
                {
                    Debug.LogError("[SmartMountPoint] No valid image paths provided for AI analysis");
                    return new AIAnalysisResult
                    {
                        analysis = Error.NoImagesForAIAnalysis(),
                        isReasonable = true
                    };
                }

                Debug.Log($"[SmartMountPoint] Analyzing {validImagePaths.Count} images with AI");
                foreach (var path in validImagePaths)
                {
                    var fileInfo = new System.IO.FileInfo(path);
                    Debug.Log($"[SmartMountPoint] Image: {path} (Size: {fileInfo.Length} bytes)");
                }

                // Build color information for mount points
                var colorInfo = BuildColorInformation(mountPoints);

                // Build structured analysis prompt with color information
                var structuredPrompt = $@"Please analyze whether the IK mount point positions in the 3D model in the images are reasonable. Different colored spheres represent different mount points. Multiple images are provided from different angles for comprehensive analysis.

{colorInfo}

User requirement background: {userContext}

Please analyze each mount point separately and answer strictly in the following format:
ANALYSIS: [Your detailed analysis for each mount point, no more than 150 words]
REASONABLE: [YES/NO]
SUGGESTIONS: [Must provide suggestions for each mount point, format as: MountPointName:DIRECTION1,DIRECTION2]

Available directions: UP, DOWN, LEFT, RIGHT, FORWARD, BACKWARD, CENTER, NONE (do not move this mount point)

Example output format:
ANALYSIS: Red sphere (RightHandIK) position is too high and not properly positioned for grip. Blue sphere (LeftHandIK) needs to move forward for better support.
REASONABLE: NO
SUGGESTIONS: 
- RightHandIK:DOWN,LEFT
- LeftHandIK:FORWARD,UP

Now please analyze the images from multiple angles:";

                // Join multiple image paths with | separator as required by AI ImageRecognition
                var combinedImagePaths = string.Join("|", validImagePaths);
                
                var aiTool = new Tool_AI();
                var aiResult = await aiTool.ImageRecognition(combinedImagePaths, structuredPrompt, "none", 1000);
                
                // Debug.Log($"[SmartMountPoint] AI analysis result: {aiResult}");
                
                var result = new AIAnalysisResult
                {
                    analysis = aiResult
                };

                // Parse structured AI output
                result.isReasonable = ExtractReasonableStatus(aiResult);
                result.suggestions = ExtractStructuredSuggestions(aiResult);

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SmartMountPoint] AI analysis failed: {ex.Message}");
                return new AIAnalysisResult
                {
                    analysis = Error.AIAnalysisFailed(ex.Message),
                    isReasonable = true // Default to reasonable when failed to avoid infinite loop
                };
            }
        }

        // Build color information text for AI prompt
        private string BuildColorInformation(List<MountPointInfo> mountPoints)
        {
            var colorInfo = "Mount point color identification (by order):\n";
            for (int i = 0; i < mountPoints.Count; i++)
            {
                var mountPoint = mountPoints[i];
                var colorName = GetColorName(mountPoint.visualizationColor);
                colorInfo += $"- {colorName} sphere = {mountPoint.name} ({mountPoint.description}) [Order: {i + 1}]\n";
            }
            return colorInfo;
        }

        // Get color name for AI prompt
        private string GetColorName(Color color)
        {
            if (color == Color.red) return "Red";
            if (color == Color.blue) return "Blue";
            if (color == Color.green) return "Green";
            if (color == Color.yellow) return "Yellow";
            if (color == Color.magenta) return "Magenta";
            if (color == Color.cyan) return "Cyan";
            if (color == Color.white) return "White";
            if (color == Color.gray) return "Gray";
            if (color == Color.black) return "Black";
            return "Unknown";
        }

        // Extract reasonableness status
        private bool ExtractReasonableStatus(string aiResult)
        {
            try
            {
                var lines = aiResult.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("REASONABLE:", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = line.Substring("REASONABLE:".Length).Trim().ToUpper();
                        return status == "YES";
                    }
                }
                
                // If standard format not found, use keyword matching as fallback
                var lowerResult = aiResult.ToLower();
                return !lowerResult.Contains("reasonable: no") && 
                       !lowerResult.Contains("reasonable:no") &&
                       !lowerResult.Contains("not reasonable") &&
                       !lowerResult.Contains("unreasonable") &&
                       !lowerResult.Contains("incorrect") &&
                       !lowerResult.Contains("wrong");
            }
            catch
            {
                return true; // Default to reasonable when parsing fails
            }
        }

        // Extract structured suggestions for specific mount points
        private List<string> ExtractStructuredSuggestions(string aiResult)
        {
            var suggestions = new List<string>();
            
            try
            {
                var lines = aiResult.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                bool inSuggestionsSection = false;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Check if we're starting the SUGGESTIONS section
                    if (trimmedLine.StartsWith("SUGGESTIONS:", StringComparison.OrdinalIgnoreCase))
                    {
                        inSuggestionsSection = true;
                        
                        // Try to parse suggestions on the same line
                        var suggestionsText = trimmedLine.Substring("SUGGESTIONS:".Length).Trim();
                        if (!string.IsNullOrEmpty(suggestionsText))
                        {
                            ParseSuggestionLine(suggestionsText, suggestions);
                        }
                        continue;
                    }
                    
                    // If we're in the suggestions section, parse each line
                    if (inSuggestionsSection)
                    {
                        // Check if this line contains mount point suggestions
                        if (string.IsNullOrEmpty(trimmedLine) || 
                            trimmedLine.StartsWith("ANALYSIS:", StringComparison.OrdinalIgnoreCase) ||
                            trimmedLine.StartsWith("REASONABLE:", StringComparison.OrdinalIgnoreCase))
                        {
                            // End of suggestions section
                            break;
                        }
                        
                        // Parse different formats:
                        // Format 1: "- RightHandIK:UP,FORWARD"
                        // Format 2: "RightHandIK:UP,FORWARD"
                        // Format 3: "RightHandIK: UP, FORWARD"
                        var suggestionLine = trimmedLine;
                        if (suggestionLine.StartsWith("-"))
                        {
                            suggestionLine = suggestionLine.Substring(1).Trim();
                        }
                        
                        ParseSuggestionLine(suggestionLine, suggestions);
                    }
                }
                
                // If no structured suggestions found but judged unreasonable, add general adjustment
                if (suggestions.Count == 0 && !ExtractReasonableStatus(aiResult))
                {
                    suggestions.Add("general:no_movement");
                }
                
                // Debug log the parsed suggestions
                if (suggestions.Count > 0)
                {
                    Debug.Log($"[SmartMountPoint] Parsed {suggestions.Count} AI suggestions: {string.Join(", ", suggestions)}");
                }
                else
                {
                    Debug.Log("[SmartMountPoint] No AI suggestions parsed from response");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SmartMountPoint] Failed to parse AI suggestions: {ex.Message}");
            }
            
            return suggestions;
        }

        // Parse a single suggestion line and add to suggestions list
        private void ParseSuggestionLine(string suggestionLine, List<string> suggestions)
        {
            if (string.IsNullOrEmpty(suggestionLine)) return;
            
            // Handle multiple mount points in one line separated by space
            var mountPointSuggestions = suggestionLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var mountSuggestion in mountPointSuggestions)
            {
                if (mountSuggestion.Contains(":"))
                {
                    var parts = mountSuggestion.Split(new[] { ':' }, 2); // Only split on first ':'
                    if (parts.Length == 2)
                    {
                        var mountPointName = parts[0].Trim();
                        var directionsText = parts[1].Trim();
                        
                        // Handle different direction separators: comma, space, semicolon
                        var directions = directionsText.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var direction in directions)
                        {
                            var trimmedDirection = direction.Trim().ToUpper();
                            var action = ConvertDirectionToAction(trimmedDirection);
                            
                            if (!string.IsNullOrEmpty(action))
                            {
                                var suggestionKey = $"{mountPointName}:{action}";
                                if (!suggestions.Contains(suggestionKey)) // Avoid duplicates
                                {
                                    suggestions.Add(suggestionKey);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[SmartMountPoint] Invalid suggestion line: {suggestionLine}");
                }
            }
        }

        // Convert direction text to action
        private string ConvertDirectionToAction(string direction)
        {
            switch (direction)
            {
                case "UP": return "move_up";
                case "DOWN": return "move_down";
                case "LEFT": return "move_left";
                case "RIGHT": return "move_right";
                case "FORWARD": return "move_forward";
                case "BACKWARD": return "move_backward";
                case "CENTER": return "center_position";
                case "NONE": return "no_movement"; // AI indicates no movement needed
                case "STAY": return "no_movement"; // Alternative way to say no movement
                case "KEEP": return "no_movement"; // Another alternative
                default: return "";
            }
        }

        // Intelligently adjust mount points based on AI suggestions
        private AdjustmentResult AdjustMountPointsBasedOnAI(List<MountPointInfo> currentMountPoints, List<string> suggestions)
        {
            try
            {
                if (suggestions == null || suggestions.Count == 0)
                {
                    return new AdjustmentResult
                    {
                        success = false,
                        message = Error.NoValidSuggestionsForAdjustment()
                    };
                }
                
                var adjustedMountPoints = new List<MountPointInfo>();
                var adjustmentStep = 0.03f; // Adjustment step size
                
                // Group suggestions by mount point
                var suggestionsByMountPoint = new Dictionary<string, List<string>>();
                foreach (var suggestion in suggestions)
                {
                    var parts = suggestion.Split(':');
                    if (parts.Length == 2)
                    {
                        var mountPointName = parts[0];
                        var action = parts[1];
                        
                        if (!suggestionsByMountPoint.ContainsKey(mountPointName))
                        {
                            suggestionsByMountPoint[mountPointName] = new List<string>();
                        }
                        suggestionsByMountPoint[mountPointName].Add(action);
                    }
                }
                
                foreach (var mountPoint in currentMountPoints)
                {
                    var newPosition = mountPoint.position;
                    var adjustmentMade = false;
                    var adjustmentDescription = new List<string>();

                    // Check for specific suggestions for this mount point
                    if (suggestionsByMountPoint.ContainsKey(mountPoint.name))
                    {
                        var mountPointSuggestions = suggestionsByMountPoint[mountPoint.name];
                        foreach (var action in mountPointSuggestions)
                        {
                            adjustmentMade |= ApplyAdjustmentAction(ref newPosition, action, adjustmentStep, adjustmentDescription);
                        }
                    }
                    // Check for general suggestions that apply to all mount points
                    else if (suggestionsByMountPoint.ContainsKey("general"))
                    {
                        var generalSuggestions = suggestionsByMountPoint["general"];
                        foreach (var action in generalSuggestions)
                        {
                            adjustmentMade |= ApplyAdjustmentAction(ref newPosition, action, adjustmentStep, adjustmentDescription);
                        }
                    }

                    if (adjustmentMade)
                    {
                        var adjustedMountPoint = new MountPointInfo(
                            mountPoint.name,
                            newPosition,
                            mountPoint.rotation,
                            mountPoint.scale,
                            $"{mountPoint.description} (AI adjustment: {string.Join(", ", adjustmentDescription)})",
                            mountPoint.isLocalSpace
                        );
                        // Preserve the original color
                        adjustedMountPoint.visualizationColor = mountPoint.visualizationColor;
                        adjustedMountPoints.Add(adjustedMountPoint);
                    }
                    else
                    {
                        adjustedMountPoints.Add(mountPoint);
                    }
                }

                return new AdjustmentResult
                {
                    success = adjustedMountPoints.Count > 0,
                    message = $"[Success] Adjustment completed based on AI suggestions: {string.Join(", ", suggestions)}",
                    adjustedMountPoints = adjustedMountPoints
                };
            }
            catch (Exception ex)
            {
                return new AdjustmentResult
                {
                    success = false,
                    message = Error.MountPointAdjustmentFailed(ex.Message)
                };
            }
        }

        // Apply specific adjustment action to position
        private bool ApplyAdjustmentAction(ref Vector3 position, string action, float adjustmentStep, List<string> adjustmentDescription)
        {
            switch (action)
            {
                case "move_up":
                    position.y += adjustmentStep;
                    adjustmentDescription.Add("move up");
                    return true;
                    
                case "move_down":
                    position.y -= adjustmentStep;
                    adjustmentDescription.Add("move down");
                    return true;
                    
                case "move_left":
                    position.x -= adjustmentStep;
                    adjustmentDescription.Add("move left");
                    return true;
                    
                case "move_right":
                    position.x += adjustmentStep;
                    adjustmentDescription.Add("move right");
                    return true;
                    
                case "move_forward":
                    position.z += adjustmentStep;
                    adjustmentDescription.Add("move forward");
                    return true;
                    
                case "move_backward":
                    position.z -= adjustmentStep;
                    adjustmentDescription.Add("move backward");
                    return true;
                    
                case "center_position":
                    // Center adjustment towards origin
                    position = Vector3.Lerp(position, Vector3.zero, 0.3f);
                    adjustmentDescription.Add("center adjustment");
                    return true;
                case "no_movement":
                    // AI indicates this mount point is fine as is
                    adjustmentDescription.Add("position maintained (AI considers reasonable)");
                    return true; // Return true to indicate we processed the suggestion
                    
                default:
                    return false;
            }
        }



        // Clean up temporary objects
        private void CleanupTemporaryObjects(GameObject sceneInstance, Camera captureCamera)
        {
            MainThread.Instance.Run(() =>
            {
                try
                {
                    if (sceneInstance != null)
                    {
                        Debug.Log($"[SmartMountPoint] Cleaning up scene instance: {sceneInstance.name}");
                        UnityEngine.Object.DestroyImmediate(sceneInstance);
                    }
                    
                    if (captureCamera != null)
                    {
                        Debug.Log($"[SmartMountPoint] Cleaning up capture camera: {captureCamera.name}");
                        UnityEngine.Object.DestroyImmediate(captureCamera.gameObject);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SmartMountPoint] {Error.SmartMountProcessFailed($"cleanup failed: {ex.Message}")}");
                }
            });
        }
    }
} 