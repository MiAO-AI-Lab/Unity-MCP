#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Essential.Tools
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
- capture: Capture scene from different camera viewpoints. Include front, side, top, isometric, and scene view
- getSceneViewCamera: Get current Scene View camera position, rotation, and settings
- findByComponent: Find all GameObjects in scene that have a specific component type
- screenToRay: Convert screen coordinates to world space ray and optionally perform raycast")]
        public string Operations
        (
            [Description("Operation type: 'getLoaded', 'getHierarchy', 'cameraControl', 'capture', 'getSceneViewCamera', 'findByComponent', 'screenToRay'")]
            string operation,
            [Description("For getHierarchy: depth of hierarchy to include. For capture: capture resolution")]
            int depth = 3,
            [Description("For getHierarchy: scene name (empty for active scene). For cameraControl: camera position as 'x,y,z'")]
            string? sceneName = null,
            [Description("For cameraControl: camera rotation as 'x,y,z' in Euler angles")]
            string? rotation = null,
            [Description("For cameraControl: camera field of view value")]
            string? fieldOfView = "50",
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
            bool captureIsometricView = false,
            [Description("For capture: capture current scene view window directly (when true, ignores ALL camera-related capture options)")]
            bool captureSceneView = true,
            [Description("For findByComponent: component type name to search for (e.g., 'UnityEngine.Camera', 'Rigidbody', 'MeshRenderer')")]
            string? componentType = null,
            [Description("For screenToRay: X coordinate in screen/screenshot space")]
            float screenX = 0,
            [Description("For screenToRay: Y coordinate in screen/screenshot space")]
            float screenY = 0,
            [Description("For screenToRay: width of the screen/screenshot (used for coordinate conversion)")]
            float screenWidth = 1920,
            [Description("For screenToRay: height of the screen/screenshot (used for coordinate conversion)")]
            float screenHeight = 1080,
            [Description("For screenToRay: whether to perform raycast along the generated ray")]
            bool performRaycast = true,
            [Description("For screenToRay: maximum distance for raycast (only used when performRaycast=true)")]
            float rayMaxDistance = 100f,
            [Description("For screenToRay: layer mask for raycast, -1 means all layers (only used when performRaycast=true)")]
            int rayLayerMask = -1
        )
        {
            return operation.ToLower() switch
            {
                "getloaded" => GetLoadedScenes(),
                "gethierarchy" => GetSceneHierarchy(depth, sceneName),
                "cameracontrol" => ControlCamera(sceneName, rotation, fieldOfView, targetName, targetTag),
                "capture" => CaptureScene(depth, backgroundColorHex, fileName, captureFrontView, captureSideView, captureTopView, captureIsometricView, captureSceneView),
                "getsceneviewcamera" => GetSceneViewCameraInfo(),
                "findbycomponent" => FindGameObjectsByComponent(componentType, sceneName, depth),
                "screentoray" => ScreenToRay(screenX, screenY, screenWidth, screenHeight, performRaycast, rayMaxDistance, rayLayerMask),
                _ => "[Error] Invalid operation. Valid operations: 'getLoaded', 'getHierarchy', 'cameraControl', 'capture', 'getSceneViewCamera', 'findByComponent', 'screenToRay'"
            };
        }

        private string GetLoadedScenes()
        {
            return MainThread.Instance.Run(() =>
            {
                return $"[Success] " + LoadedScenes;
            });
        }

        private string GetSceneViewCameraInfo()
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    // Get the current Scene View
                    SceneView sceneView = SceneView.lastActiveSceneView;
                    if (sceneView == null)
                    {
                        // Try to get any available Scene View
                        var sceneViews = SceneView.sceneViews;
                        if (sceneViews.Count > 0)
                        {
                            sceneView = sceneViews[0] as SceneView;
                        }
                    }

                    if (sceneView == null)
                    {
                        return "[Error] No Scene View window found. Please open a Scene View window.";
                    }

                    // Get Scene View camera
                    Camera sceneCamera = sceneView.camera;
                    if (sceneCamera == null)
                    {
                        return "[Error] Scene View camera not found.";
                    }

                    // Get camera transform information
                    Vector3 position = sceneCamera.transform.position;
                    Vector3 eulerAngles = sceneCamera.transform.eulerAngles;
                    float fieldOfView = sceneCamera.fieldOfView;
                    float nearClipPlane = sceneCamera.nearClipPlane;
                    float farClipPlane = sceneCamera.farClipPlane;
                    bool isOrthographic = sceneCamera.orthographic;
                    float orthographicSize = sceneCamera.orthographicSize;

                    // Get Scene View specific settings
                    bool in2DMode = sceneView.in2DMode;

                    // Format the information as easily parsable parameters
                    string result = "[Success] Scene View Camera Information:\n";
                    result += $"Position: {position.x:F3},{position.y:F3},{position.z:F3}\n";
                    result += $"Rotation: {eulerAngles.x:F3},{eulerAngles.y:F3},{eulerAngles.z:F3}\n";
                    result += $"Field of View: {fieldOfView:F1}\n";
                    result += $"Near Clip Plane: {nearClipPlane:F3}\n";
                    result += $"Far Clip Plane: {farClipPlane:F3}\n";
                    result += $"Is Orthographic: {isOrthographic}\n";
                    if (isOrthographic)
                    {
                        result += $"Orthographic Size: {orthographicSize:F3}\n";
                    }
                    result += $"2D Mode: {in2DMode}\n";
                    
                    // Add ready-to-use parameter strings for camera control
                    result += "\n--- Ready-to-use Parameters ---\n";
                    result += $"For cameraControl position parameter: \"{position.x:F3},{position.y:F3},{position.z:F3}\"\n";
                    result += $"For cameraControl rotation parameter: \"{eulerAngles.x:F3},{eulerAngles.y:F3},{eulerAngles.z:F3}\"\n";
                    result += $"For cameraControl fieldOfView parameter: \"{fieldOfView:F1}\"\n";

                    return result;
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to get Scene View camera info: {ex.Message}";
                }
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

        private string FindGameObjectsByComponent(string? componentTypeName, string? loadedSceneName, int includeChildrenDepth)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    // Validate component type parameter
                    if (string.IsNullOrEmpty(componentTypeName))
                    {
                        return "[Error] Component type name is required. Examples: 'UnityEngine.Camera', 'Rigidbody', 'MeshRenderer', 'Transform'";
                    }

                    // Get the scene to search in
                    var scene = string.IsNullOrEmpty(loadedSceneName)
                        ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                        : UnityEngine.SceneManagement.SceneManager.GetSceneByName(loadedSceneName);

                    if (!scene.IsValid())
                        return Error.NotFoundSceneWithName(loadedSceneName);

                    // Try to get the component type
                    System.Type componentType = null;
                    
                    // First try direct type lookup
                    componentType = System.Type.GetType(componentTypeName);
                    
                    // If not found, try common Unity Engine types
                    if (componentType == null)
                    {
                        var unityEngineTypeName = componentTypeName.Contains('.') ? componentTypeName : $"UnityEngine.{componentTypeName}";
                        componentType = System.Type.GetType($"{unityEngineTypeName}, UnityEngine");
                        
                        // Also try UnityEngine.CoreModule for some types
                        if (componentType == null)
                        {
                            componentType = System.Type.GetType($"{unityEngineTypeName}, UnityEngine.CoreModule");
                        }
                    }

                    // If still not found, search through all loaded assemblies
                    if (componentType == null)
                    {
                        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                        {
                            componentType = assembly.GetTypes().FirstOrDefault(t => 
                                t.Name.Equals(componentTypeName, System.StringComparison.OrdinalIgnoreCase) ||
                                t.FullName.Equals(componentTypeName, System.StringComparison.OrdinalIgnoreCase));
                            
                            if (componentType != null)
                                break;
                        }
                    }

                    if (componentType == null)
                    {
                        return $"[Error] Component type '{componentTypeName}' not found. Please check the type name. Examples: 'Camera', 'Rigidbody', 'MeshRenderer', 'Transform'";
                    }

                    // Check if the type is actually a Component
                    if (!typeof(UnityEngine.Component).IsAssignableFrom(componentType))
                    {
                        return $"[Error] Type '{componentTypeName}' is not a Unity Component. Please specify a valid Component type.";
                    }

                    // Find all GameObjects in the scene with the specified component
                    var foundGameObjects = new System.Collections.Generic.List<GameObject>();
                    var rootGameObjects = scene.GetRootGameObjects();

                    foreach (var rootGO in rootGameObjects)
                    {
                        // Check root GameObject
                        if (rootGO.GetComponent(componentType) != null)
                        {
                            foundGameObjects.Add(rootGO);
                        }

                        // Check all children
                        var allChildren = rootGO.GetComponentsInChildren(componentType, true); // include inactive
                        foreach (var component in allChildren)
                        {
                            if (component != null && !foundGameObjects.Contains(component.gameObject))
                            {
                                foundGameObjects.Add(component.gameObject);
                            }
                        }
                    }

                    // Build the result
                    var result = new System.Text.StringBuilder();
                    result.AppendLine($"[Success] Found {foundGameObjects.Count} GameObject(s) with component '{componentType.Name}' in scene '{scene.name}':");
                    result.AppendLine();

                    if (foundGameObjects.Count == 0)
                    {
                        result.AppendLine($"No GameObjects found with component type '{componentType.Name}' in the specified scene.");
                        return result.ToString();
                    }

                    // Sort by hierarchy path for better readability
                    foundGameObjects.Sort((a, b) => GetGameObjectPath(a).CompareTo(GetGameObjectPath(b)));

                    // Display results with detailed information
                    for (int i = 0; i < foundGameObjects.Count; i++)
                    {
                        var go = foundGameObjects[i];
                        var components = go.GetComponents(componentType);
                        
                        result.AppendLine($"[{i + 1}] GameObject: {go.name}");
                        result.AppendLine($"    InstanceID: {go.GetInstanceID()}");
                        result.AppendLine($"    Path: {GetGameObjectPath(go)}");
                        result.AppendLine($"    Active: {go.activeInHierarchy}");
                        result.AppendLine($"    Tag: {go.tag}");
                        result.AppendLine($"    Layer: {LayerMask.LayerToName(go.layer)}");
                        result.AppendLine($"    Components of type '{componentType.Name}': {components.Length}");
                        
                        // Show component details if requested depth is > 0
                        if (includeChildrenDepth > 0)
                        {
                            foreach (var comp in components)
                            {
                                result.AppendLine($"      - {comp.GetType().Name} (InstanceID: {comp.GetInstanceID()})");
                            }
                        }
                        
                        result.AppendLine();
                    }

                    // Add summary statistics
                    result.AppendLine("=== Summary ===");
                    result.AppendLine($"Total GameObjects found: {foundGameObjects.Count}");
                    result.AppendLine($"Component type searched: {componentType.FullName}");
                    result.AppendLine($"Scene searched: {scene.name}");
                    result.AppendLine($"Include inactive GameObjects: Yes");

                    return result.ToString();
                }
                catch (System.Exception ex)
                {
                    return $"[Error] Failed to search for GameObjects with component '{componentTypeName}': {ex.Message}";
                }
            });
        }

        // Helper method to get the full hierarchy path of a GameObject
        private string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        private string ScreenToRay(float screenX, float screenY, float screenWidth, float screenHeight, bool performRaycast, float rayMaxDistance, int rayLayerMask)
        {
            return MainThread.Instance.Run(() =>
            {
                try
                {
                    // Parameter validation
                    if (screenWidth <= 0 || screenHeight <= 0)
                        return "[Error] Screen width and height must be positive values.";
                    
                    if (screenX < 0 || screenX > screenWidth || screenY < 0 || screenY > screenHeight)
                        return "[Error] Screen coordinates must be within the screen bounds.";

                    // Find the main camera or any active camera
                    Camera targetCamera = Camera.main;
                    if (targetCamera == null)
                    {
                        // Try to find any active camera
                        Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                        foreach (var cam in cameras)
                        {
                            if (cam.enabled && cam.gameObject.activeInHierarchy)
                            {
                                targetCamera = cam;
                                break;
                            }
                        }
                    }

                    if (targetCamera == null)
                        return "[Error] No active camera found in the scene.";

                    // Convert screenshot coordinates to Unity screen coordinates
                    // Unity screen space: (0,0) at bottom-left, (width,height) at top-right
                    // Screenshot coordinates: often (0,0) at top-left
                    
                    // Convert screenshot coordinates to normalized coordinates (0-1)
                    float normalizedX = screenX / screenWidth;
                    float normalizedY = 1f - (screenY / screenHeight); // Flip Y axis
                    
                    // Convert to camera screen coordinates
                    float cameraScreenX = normalizedX * targetCamera.pixelWidth;
                    float cameraScreenY = normalizedY * targetCamera.pixelHeight;
                    
                    Vector3 screenPoint = new Vector3(cameraScreenX, cameraScreenY, 0);
                    
                    // Generate ray from camera through screen point
                    Ray ray = targetCamera.ScreenPointToRay(screenPoint);
                    
                    string result = "[Success] Screen-to-ray conversion completed.\\n";
                    result += $"# Input Information:\\n";
                    result += $"Screenshot coordinates: ({screenX}, {screenY})\\n";
                    result += $"Screenshot size: {screenWidth} x {screenHeight}\\n";
                    result += $"Camera: {targetCamera.name} (InstanceID: {targetCamera.GetInstanceID()})\\n";
                    result += $"Camera screen coordinates: ({cameraScreenX:F1}, {cameraScreenY:F1})\\n";
                    result += $"\\n# Ray Information:\\n";
                    result += $"Ray origin: {ray.origin}\\n";
                    result += $"Ray direction: {ray.direction}\\n";
                    
                    // Optional raycast
                    if (performRaycast)
                    {
                        result += $"\\n# Raycast Results:\\n";
                        result += $"Max distance: {rayMaxDistance}\\n";
                        result += $"Layer mask: {rayLayerMask}\\n";
                        
                        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, rayMaxDistance, rayLayerMask);
                        if (hit)
                        {
                            result += $"✅ HIT DETECTED:\\n";
                            result += $"Hit point: {hitInfo.point}\\n";
                            result += $"Hit distance: {hitInfo.distance:F3}\\n";
                            result += $"Hit normal: {hitInfo.normal}\\n";
                            result += $"Hit object: {hitInfo.collider.gameObject.name}\\n";
                            result += $"Hit object tag: {hitInfo.collider.gameObject.tag}\\n";
                            result += $"Hit object layer: {hitInfo.collider.gameObject.layer}\\n";
                            result += $"Hit collider: {hitInfo.collider.name} (InstanceID: {hitInfo.collider.GetInstanceID()})\\n";
                            
                            if (hitInfo.rigidbody != null)
                            {
                                result += $"Rigidbody: {hitInfo.rigidbody.name}\\n";
                            }
                        }
                        else
                        {
                            result += $"❌ No collision detected within {rayMaxDistance} units.\\n";
                        }
                    }
                    
                    // Add coordinate conversion info for debugging
                    result += $"\\n# Coordinate Conversion Details:\\n";
                    result += $"Normalized coordinates: ({normalizedX:F3}, {normalizedY:F3})\\n";
                    result += $"Camera pixel dimensions: {targetCamera.pixelWidth} x {targetCamera.pixelHeight}\\n";
                    result += $"Camera field of view: {targetCamera.fieldOfView}°\\n";
                    result += $"Camera position: {targetCamera.transform.position}\\n";
                    result += $"Camera rotation: {targetCamera.transform.eulerAngles}\\n";
                    
                    return result;
                }
                catch (System.Exception ex)
                {
                    return $"[Error] Exception in ScreenToRay: {ex.Message}";
                }
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
            bool captureFrontView, bool captureSideView, bool captureTopView, bool captureIsometricView, bool captureSceneView)
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
                
                if (captureSceneView)
                {
                    // Scene View capture mode - ignores all camera-related parameters
                    resultMsg += CaptureCurrentSceneView(baseFileName, captureResolution);
                    
                    // Clean up any temporary cameras created for this purpose
                    resultMsg += CleanupTemporaryCameras();
                }
                else
                {
                    // Camera-based capture mode
                if (captureFrontView)
                        resultMsg += CaptureFromCamera(Vector3.forward, Vector3.up, "Front");
                if (captureSideView)
                        resultMsg += CaptureFromCamera(Vector3.right, Vector3.up, "Side");
                if (captureTopView)
                        resultMsg += CaptureFromCamera(Vector3.up, Vector3.forward, "Top");
                if (captureIsometricView)
                        resultMsg += CaptureFromCamera(new Vector3(1, 1, 1).normalized, Vector3.up, "Isometric");
                        
                    // Check if no camera capture options were selected
                    if (!captureFrontView && !captureSideView && !captureTopView && !captureIsometricView)
                    {
                        resultMsg = "[Warning] No capture options selected. Please enable at least one capture mode.";
                    }
                    else
                    {
                        // Clean up any remaining temporary cameras as additional safety measure
                        resultMsg += CleanupTemporaryCameras();
                    }
                }

                return resultMsg;

                // Internal method: capture specified viewpoint using temporary camera
                string CaptureFromCamera(Vector3 viewDir, Vector3 upDir, string viewName)
                {
                    // Get main camera for reference settings only
                    var referenceCam = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
                    if (referenceCam == null)
                    {
                        return $"\n[Error] No reference camera found in scene '{scene.name}'. Please add a camera to the scene.";
                    }

                    // Calculate scene bounds for optimal camera positioning
                    Bounds sceneBounds = CalculateSceneBounds();
                    if (sceneBounds.size == Vector3.zero)
                    {
                        // Default bounds if no objects found
                        sceneBounds = new Bounds(Vector3.zero, Vector3.one * 20f);
                    }

                    // Create temporary camera for this viewpoint
                    GameObject tempCameraGO = null;
                    Camera tempCamera = null;
                    RenderTexture rt = null;
                    Texture2D tex = null;

                    try
                    {
                        // Create and setup temporary camera
                        tempCameraGO = new GameObject($"TempCamera_{viewName}_{System.DateTime.Now:HHmmss}");
                        tempCamera = tempCameraGO.AddComponent<Camera>();
                        
                        // Configure camera with reference settings
                        ConfigureTemporaryCamera(tempCamera, referenceCam);
                        
                        // Position camera using intelligent algorithm
                        PositionCameraForView(tempCamera, sceneBounds, viewDir, upDir, viewName);

                        // Create render texture for capture
                        rt = new RenderTexture(captureResolution, captureResolution, 24, RenderTextureFormat.ARGB32);
                        rt.antiAliasing = 8; // Add anti-aliasing for better quality
                        
                        // Setup camera for rendering
                        tempCamera.targetTexture = rt;
                        tempCamera.backgroundColor = backgroundColor;
                        
                        // Render the scene
                        tempCamera.Render();

                        // Convert to Texture2D
                        RenderTexture.active = rt;
                        tex = new Texture2D(captureResolution, captureResolution, TextureFormat.RGBA32, false);
                        tex.ReadPixels(new Rect(0, 0, captureResolution, captureResolution), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;

                        // Save to file
                        var tempPath = System.IO.Path.GetTempPath();
                        var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var filename = $"{baseFileName}_{viewName}_{timestamp}.png";
                        var fullPath = System.IO.Path.Combine(tempPath, filename);

                        var pngBytes = tex.EncodeToPNG();
                        System.IO.File.WriteAllBytes(fullPath, pngBytes);

                        return $"\n[Success] {viewName} view captured to: {fullPath}";
                    }
                    catch (System.Exception ex)
                    {
                        return $"\n[Error] {viewName} view capture failed: {ex.Message}";
                    }
                    finally
                    {
                        // Clean up all resources in correct order to avoid Unity warnings
                        
                        // Step 1: Disconnect camera from render texture first
                        if (tempCamera != null && tempCamera.targetTexture != null)
                        {
                            tempCamera.targetTexture = null;
                        }
                        
                        // Step 2: Reset active render texture
                        RenderTexture.active = null;
                        
                        // Step 3: Clean up texture resources
                        if (tex != null)
                            UnityEngine.Object.DestroyImmediate(tex);
                            
                        if (rt != null)
                        {
                            rt.Release();
                            UnityEngine.Object.DestroyImmediate(rt);
                        }
                        
                        // Step 4: Finally destroy temporary camera
                        if (tempCameraGO != null)
                            UnityEngine.Object.DestroyImmediate(tempCameraGO);
                    }
                }

                // Helper method to calculate scene bounds
                Bounds CalculateSceneBounds()
                {
                    var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
                    if (renderers.Length == 0)
                        return new Bounds();

                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    return bounds;
                }

                // Configure temporary camera with optimal settings
                void ConfigureTemporaryCamera(Camera tempCamera, Camera referenceCamera)
                {
                    // Basic camera configuration
                    tempCamera.fieldOfView = 50f; // Human-comfortable FOV
                    tempCamera.nearClipPlane = referenceCamera.nearClipPlane;
                    tempCamera.farClipPlane = referenceCamera.farClipPlane;
                    
                    // Force perspective projection for better 3D visualization
                    tempCamera.orthographic = false;
                    
                    // Rendering settings
                    tempCamera.clearFlags = CameraClearFlags.SolidColor;
                    tempCamera.backgroundColor = backgroundColor;
                    tempCamera.cullingMask = referenceCamera.cullingMask;
                    
                    // Quality settings
                    tempCamera.renderingPath = RenderingPath.Forward; // Ensure compatibility
                    tempCamera.allowHDR = referenceCamera.allowHDR;
                    tempCamera.allowMSAA = true; // Enable multi-sampling for better quality
                    
                    // Display settings
                    tempCamera.targetDisplay = referenceCamera.targetDisplay;
                    
                    // Disable audio listener to avoid conflicts
                    var audioListener = tempCamera.GetComponent<AudioListener>();
                    if (audioListener != null)
                        UnityEngine.Object.DestroyImmediate(audioListener);
                }

                // Helper method to position camera for specific views using intelligent framing algorithm
                void PositionCameraForView(Camera camera, Bounds bounds, Vector3 viewDir, Vector3 upDir, string viewName)
                {
                    var cameraSetup = CalculateOptimalCameraPosition(bounds, viewName, camera.fieldOfView);
                    
                    camera.transform.position = cameraSetup.position;
                    camera.transform.LookAt(cameraSetup.lookTarget, cameraSetup.upVector);
                    
                    // Fine-tune FOV if needed for better framing
                    if (cameraSetup.recommendedFOV > 0)
                    {
                        camera.fieldOfView = cameraSetup.recommendedFOV;
                    }
                }

                // Advanced camera positioning algorithm for optimal framing
                CameraSetup CalculateOptimalCameraPosition(Bounds bounds, string viewName, float currentFOV)
                {
                    Vector3 center = bounds.center;
                    Vector3 size = bounds.size;
                    
                    // Ensure minimum size to avoid division by zero
                    size = Vector3.Max(size, Vector3.one * 0.1f);
                    
                    // Define viewing parameters for different angles
                    ViewingParameters viewParams = GetViewingParameters(viewName);
                    
                    // Calculate the primary and secondary dimensions based on viewing angle
                    Vector2 screenDimensions = CalculateScreenDimensions(size, viewParams);
                    
                    // Calculate optimal distance using FOV and screen dimensions
                    float optimalDistance = CalculateOptimalDistance(screenDimensions, currentFOV);
                    
                    // Apply comfort adjustments
                    optimalDistance = ApplyComfortAdjustments(optimalDistance, bounds, viewParams);
                    
                    // Calculate final camera position
                    Vector3 cameraPosition = center + viewParams.direction * optimalDistance;
                    
                    // Add height adjustment for better composition
                    cameraPosition += ApplyCompositionAdjustments(bounds, viewParams);
                    
                    return new CameraSetup
                    {
                        position = cameraPosition,
                        lookTarget = center,
                        upVector = viewParams.upVector,
                        recommendedFOV = OptimizeFOV(screenDimensions, optimalDistance, currentFOV)
                    };
                }

                // Define viewing parameters for different camera angles
                ViewingParameters GetViewingParameters(string viewName)
                {
                    return viewName.ToLower() switch
                    {
                        "front" => new ViewingParameters
                        {
                            direction = Vector3.back,
                            upVector = Vector3.up,
                            primaryAxis = new Vector3(1, 0, 0), // Width
                            secondaryAxis = new Vector3(0, 1, 0), // Height
                            comfortMultiplier = 1.2f,
                            heightBias = 0.1f // Slightly above center
                        },
                        "side" => new ViewingParameters
                        {
                            direction = Vector3.right,
                            upVector = Vector3.up,
                            primaryAxis = new Vector3(0, 0, 1), // Depth
                            secondaryAxis = new Vector3(0, 1, 0), // Height
                            comfortMultiplier = 1.3f,
                            heightBias = 0.15f
                        },
                        "top" => new ViewingParameters
                        {
                            direction = Vector3.up,
                            upVector = Vector3.forward,
                            primaryAxis = new Vector3(1, 0, 0), // Width
                            secondaryAxis = new Vector3(0, 0, 1), // Depth
                            comfortMultiplier = 1.5f,
                            heightBias = 0f
                        },
                        "isometric" => new ViewingParameters
                        {
                            direction = new Vector3(1, 1, -1).normalized,
                            upVector = Vector3.up,
                            primaryAxis = new Vector3(1, 0, 1).normalized, // Diagonal
                            secondaryAxis = new Vector3(0, 1, 0), // Height
                            comfortMultiplier = 1.4f,
                            heightBias = 0.2f
                        },
                        _ => new ViewingParameters
                        {
                            direction = Vector3.back,
                            upVector = Vector3.up,
                            primaryAxis = new Vector3(1, 0, 0),
                            secondaryAxis = new Vector3(0, 1, 0),
                            comfortMultiplier = 1.2f,
                            heightBias = 0.1f
                        }
                    };
                }

                // Calculate effective screen dimensions for the given viewing angle
                Vector2 CalculateScreenDimensions(Vector3 boundsSize, ViewingParameters viewParams)
                {
                    // Project bounds onto the viewing plane
                    float primaryDim = Vector3.Dot(boundsSize, viewParams.primaryAxis);
                    float secondaryDim = Vector3.Dot(boundsSize, viewParams.secondaryAxis);
                    
                    // Take absolute values and ensure minimum size
                    primaryDim = Mathf.Abs(primaryDim);
                    secondaryDim = Mathf.Abs(secondaryDim);
                    
                    // For isometric view, account for 3D projection
                    if (viewParams.direction.x != 0 && viewParams.direction.y != 0 && viewParams.direction.z != 0)
                    {
                        // Add depth contribution for isometric projection
                        float depthContribution = boundsSize.magnitude * 0.5f;
                        primaryDim += depthContribution * 0.3f;
                        secondaryDim += depthContribution * 0.3f;
                    }
                    
                    return new Vector2(primaryDim, secondaryDim);
                }

                // Calculate optimal distance based on FOV and screen dimensions
                float CalculateOptimalDistance(Vector2 screenDimensions, float fov)
                {
                    // Convert FOV to radians
                    float fovRad = fov * Mathf.Deg2Rad;
                    
                    // Calculate distance needed to fit both dimensions
                    float maxDimension = Mathf.Max(screenDimensions.x, screenDimensions.y);
                    
                    // Distance formula: d = (size/2) / tan(fov/2)
                    float distance = (maxDimension * 0.5f) / Mathf.Tan(fovRad * 0.5f);
                    
                    // Ensure minimum distance to avoid clipping
                    return Mathf.Max(distance, maxDimension * 0.5f);
                }

                // Apply comfort adjustments to avoid cramped or too distant views
                float ApplyComfortAdjustments(float baseDistance, Bounds bounds, ViewingParameters viewParams)
                {
                    // Apply comfort multiplier
                    float adjustedDistance = baseDistance * viewParams.comfortMultiplier;
                    
                    // Minimum distance based on object size to avoid distortion
                    float minComfortDistance = bounds.size.magnitude * 0.8f;
                    
                    // Maximum distance to maintain detail visibility
                    float maxComfortDistance = bounds.size.magnitude * 5.0f;
                    
                    // Clamp to comfort range
                    adjustedDistance = Mathf.Clamp(adjustedDistance, minComfortDistance, maxComfortDistance);
                    
                    return adjustedDistance;
                }

                // Apply composition adjustments for better visual appeal
                Vector3 ApplyCompositionAdjustments(Bounds bounds, ViewingParameters viewParams)
                {
                    Vector3 adjustment = Vector3.zero;
                    
                    // Height bias for more pleasing composition
                    if (viewParams.heightBias != 0)
                    {
                        adjustment += Vector3.up * bounds.size.y * viewParams.heightBias;
                    }
                    
                    // Golden ratio adjustment for side views
                    if (viewParams.direction == Vector3.right || viewParams.direction == Vector3.left)
                    {
                        adjustment += Vector3.up * bounds.size.y * 0.0618f; // Golden ratio offset
                    }
                    
                    return adjustment;
                }

                // Optimize FOV based on content and distance
                float OptimizeFOV(Vector2 screenDimensions, float distance, float currentFOV)
                {
                    // Calculate ideal FOV based on content
                    float maxDimension = Mathf.Max(screenDimensions.x, screenDimensions.y);
                    float idealFOV = 2f * Mathf.Atan(maxDimension / (2f * distance)) * Mathf.Rad2Deg;
                    
                    // Apply padding factor for comfortable viewing
                    idealFOV *= 1.3f;
                    
                    // Clamp to comfortable FOV range 
                    idealFOV = Mathf.Clamp(idealFOV, 35f, 75f);
                    
                    // If current FOV is already good, prefer it
                    if (Mathf.Abs(currentFOV - idealFOV) < 10f)
                    {
                        return currentFOV;
                    }
                    
                    return idealFOV;
                }

                // Capture current Scene View window directly
                string CaptureCurrentSceneView(string baseFileName, int resolution)
                {
                    try
                    {
                        // Get the current Scene View
                        SceneView sceneView = SceneView.lastActiveSceneView;
                        if (sceneView == null)
                        {
                            // Try to get any available Scene View
                            var sceneViews = SceneView.sceneViews;
                            if (sceneViews.Count > 0)
                            {
                                sceneView = sceneViews[0] as SceneView;
                            }
                        }

                        if (sceneView == null)
                        {
                            return "\n[Error] No Scene View window found. Please open a Scene View window.";
                        }

                        // Get Scene View camera
                        Camera sceneCamera = sceneView.camera;
                        if (sceneCamera == null)
                        {
                            return "\n[Error] Scene View camera not found.";
                        }

                        // Create render texture
                        RenderTexture rt = null;
                        Texture2D tex = null;
                        RenderTexture originalRT = null;

                        try
                        {
                            // Store original settings
                            originalRT = sceneCamera.targetTexture;
                            
                            // Create temporary render texture
                            rt = new RenderTexture(resolution, resolution, 24);
                            
                            // Set up camera for capture
                            sceneCamera.targetTexture = rt;
                            
                            // Force scene view to repaint and render
                            sceneView.Repaint();
                            sceneCamera.Render();

                            // Convert to Texture2D
                            RenderTexture.active = rt;
                            tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
                            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                            tex.Apply();
                            RenderTexture.active = null;

                            // Generate file path
                            var tempPath = System.IO.Path.GetTempPath();
                            var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            var filename = $"{baseFileName}_SceneView_{timestamp}.png";
                            var fullPath = System.IO.Path.Combine(tempPath, filename);

                            // Save PNG file
                            var pngBytes = tex.EncodeToPNG();
                            System.IO.File.WriteAllBytes(fullPath, pngBytes);

                            return $"\n[Success] Scene View captured to: {fullPath}";
                        }
                        finally
                        {
                            // Clean up resources in correct order to avoid Unity warnings
                            
                            // Step 1: Restore original camera settings first
                            if (sceneCamera != null)
                            {
                                sceneCamera.targetTexture = originalRT;
                            }
                            
                            // Step 2: Reset active render texture to be safe
                            RenderTexture.active = null;
                            
                            // Step 3: Cleanup texture resources
                            if (tex != null)
                                UnityEngine.Object.DestroyImmediate(tex);
                            if (rt != null)
                            {
                                rt.Release();
                                UnityEngine.Object.DestroyImmediate(rt);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        return $"\n[Error] Scene View capture failed: {ex.Message}";
                    }
                }
            });
        }

        // Helper method to clean up temporary cameras
        private string CleanupTemporaryCameras()
        {
            try
            {
                var allCameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                var tempCameras = new System.Collections.Generic.List<Camera>();
                var cleanupPatterns = new string[]
                {
                    "SceneViewCamera_Copy",     // Manually created scene view cameras
                    "TempCamera_",              // CaptureFromCamera temporary cameras
                    "_Copy",                    // Any copy cameras
                    "Capture_Camera",           // Additional capture camera pattern
                    "Temp_"                     // General temporary pattern
                };
                
                // Find cameras that match temporary naming patterns
                foreach (var cam in allCameras)
                {
                    if (cam == null || cam.gameObject == null) continue;
                    
                    string cameraName = cam.gameObject.name;
                    bool isTemporary = false;
                    
                    // Check against all cleanup patterns
                    foreach (var pattern in cleanupPatterns)
                    {
                        if (cameraName.Contains(pattern))
                        {
                            isTemporary = true;
                            break;
                        }
                    }
                    
                    // Additional safety check: exclude main cameras and scene cameras
                    if (isTemporary && 
                        !cameraName.Equals("Main Camera") && 
                        !cameraName.StartsWith("SceneCamera") &&
                        cam.cameraType != CameraType.SceneView)
                    {
                        tempCameras.Add(cam);
                    }
                }
                
                if (tempCameras.Count > 0)
                {
                    var cleanedCameraNames = new System.Collections.Generic.List<string>();
                    foreach (var tempCam in tempCameras)
                    {
                        if (tempCam != null && tempCam.gameObject != null)
                        {
                            cleanedCameraNames.Add(tempCam.gameObject.name);
                            UnityEngine.Object.DestroyImmediate(tempCam.gameObject);
                        }
                    }
                    
                    return $"\n[Cleanup] Removed {cleanedCameraNames.Count} temporary camera(s): {string.Join(", ", cleanedCameraNames)}";
                }
                
                return ""; // Silent when no cleanup needed to avoid noise
            }
            catch (System.Exception ex)
            {
                return $"\n[Cleanup Warning] Failed to clean up temporary cameras: {ex.Message}";
            }
        }

        // Data structures for camera positioning
        struct CameraSetup
        {
            public Vector3 position;
            public Vector3 lookTarget;
            public Vector3 upVector;
            public float recommendedFOV;
        }

        struct ViewingParameters
        {
            public Vector3 direction;        // Camera direction from target
            public Vector3 upVector;         // Camera up vector
            public Vector3 primaryAxis;      // Primary dimension axis
            public Vector3 secondaryAxis;    // Secondary dimension axis
            public float comfortMultiplier;  // Distance comfort factor
            public float heightBias;         // Vertical composition offset
        }
    }
} 