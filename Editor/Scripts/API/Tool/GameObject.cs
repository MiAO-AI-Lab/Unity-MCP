#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;

namespace com.MiAO.Unity.MCP.Editor.API
{
    [McpPluginToolType]
    public partial class Tool_GameObject
    {
        public static class Error
        {
            static string RootGOsPrinted => GameObjectUtils.FindRootGameObjects().Print();

            public static string GameObjectPathIsEmpty()
                => $"[Error] GameObject path is empty. Root GameObjects in the active scene:\n{RootGOsPrinted}";
            public static string NotFoundGameObjectAtPath(string path)
                => $"[Error] GameObject '{path}' not found. Root GameObjects in the active scene:\n{RootGOsPrinted}";

            public static string GameObjectInstanceIDIsEmpty()
                => $"[Error] GameObject InstanceID is empty. Root GameObjects in the active scene:\n{RootGOsPrinted}";
            public static string GameObjectNameIsEmpty()
                => $"[Error] GameObject name is empty. Root GameObjects in the active scene:\n{RootGOsPrinted}";
            public static string NotFoundGameObjectWithName(string name)
                => $"[Error] GameObject with name '{name}' not found. Root GameObjects in the active scene:\n{RootGOsPrinted}";
            public static string NotFoundGameObjectWithInstanceID(int instanceID)
                => $"[Error] GameObject with InstanceID '{instanceID}' not found. Root GameObjects in the active scene:\n{RootGOsPrinted}";

            public static string TypeMismatch(string typeName, string expectedTypeName)
                => $"[Error] Type mismatch. Expected '{expectedTypeName}', but got '{typeName}'.";
            public static string InvalidComponentPropertyType(SerializedMember serializedProperty, PropertyInfo propertyInfo)
                => $"[Error] Invalid component property type '{serializedProperty.typeName}' for '{propertyInfo.Name}'. Expected '{propertyInfo.PropertyType.FullName}'.";
            public static string InvalidComponentFieldType(SerializedMember serializedProperty, FieldInfo propertyInfo)
                => $"[Error] Invalid component property type '{serializedProperty.typeName}' for '{propertyInfo.Name}'. Expected '{propertyInfo.FieldType.FullName}'.";
            public static string InvalidComponentType(string typeName)
                => $"[Error] Invalid component type '{typeName}'. It should be a valid Component Type.";
            public static string NotFoundComponent(int componentInstanceID, IEnumerable<UnityEngine.Component> allComponents)
            {
                var availableComponentsPreview = allComponents
                    .Select((c, i) => Reflector.Instance.Serialize(
                        c,
                        name: $"[{i}]",
                        recursive: false,
                        logger: McpPlugin.Instance.Logger
                    ))
                    .ToList();
                var previewJson = JsonUtils.Serialize(availableComponentsPreview);

                var instanceIdSample = JsonSerializer.Serialize(new { componentData = availableComponentsPreview[0] });
                var helpMessage = $"Use 'name=[index]' to specify the component. Or use 'instanceID' to specify the component.\n{instanceIdSample}";

                return $"[Error] No component with instanceID '{componentInstanceID}' found in GameObject.\n{helpMessage}\nAvailable components preview:\n{previewJson}";
            }
            public static string NotFoundComponents(ComponentRefList componentRefs, IEnumerable<UnityEngine.Component> allComponents)
            {
                var componentInstanceIDsString = string.Join(", ", componentRefs.Select(cr => cr.ToString()));
                var availableComponentsPreview = allComponents
                    .Select((c, i) => Reflector.Instance.Serialize(
                        c,
                        name: $"[{i}]",
                        recursive: false,
                        logger: McpPlugin.Instance.Logger
                    ))
                    .ToList();
                var previewJson = JsonUtils.Serialize(availableComponentsPreview);

                return $"[Error] No components with instanceIDs [{componentInstanceIDsString}] found in GameObject.\nAvailable components preview:\n{previewJson}";
            }
            public static string ComponentFieldNameIsEmpty()
                => $"[Error] Component field name is empty. It should be a valid field name.";
            public static string ComponentFieldTypeIsEmpty()
                => $"[Error] Component field type is empty. It should be a valid field type.";
            public static string ComponentPropertyNameIsEmpty()
                => $"[Error] Component property name is empty. It should be a valid property name.";
            public static string ComponentPropertyTypeIsEmpty()
                => $"[Error] Component property type is empty. It should be a valid property type.";

            public static string InvalidInstanceID(Type holderType, string fieldName)
                => $"[Error] Invalid instanceID '{fieldName}' for '{holderType.FullName}'. It should be a valid field name.";

            // smart mount IK points
            public static string PrefabPathIsEmpty()
                => "[Error] Prefab path cannot be empty. Please provide a valid prefab path starting with 'Assets/'.";
            
            public static string MountPointsJsonIsEmpty()
                => "[Error] Mount point information cannot be empty. Please provide mount point data in JSON format.";
            
            public static string MountInstructionsIsEmpty()
                => "[Error] Mount instructions cannot be empty. Please provide detailed description of mount point requirements.";
            
            public static string AIAnalysisPromptIsEmpty()
                => "[Error] AI analysis prompt cannot be empty. Please provide analysis requirements.";
            
            public static string PrefabFileNotFound(string prefabPath)
                => $"[Error] Prefab file does not exist: '{prefabPath}'. Please check if the file exists and the path is correct.";
            
            public static string PrefabLoadFailed(string prefabPath)
                => $"[Error] Unable to load prefab: '{prefabPath}'. Please check if the file is a valid prefab asset.";
            
            public static string PrefabInstantiateFailed(string prefabPath)
                => $"[Error] Unable to instantiate prefab: '{prefabPath}'. The prefab may be corrupted or invalid.";
            
            public static string MountPointsJsonParseFailed(string error)
                => $"[Error] Failed to parse mount point information: {error}. Please check the JSON format.";
            
            public static string ApplyMountPointsFailed(string error)
                => $"[Error] Failed to apply mount points: {error}. Check if the scene instance is valid.";
            
            public static string ScreenshotCaptureFailed(string error)
                => $"[Error] Screenshot failed: {error}. Check camera settings and render target.";
            
            public static string NoImagesForAIAnalysis()
                => "[Error] No valid images available for AI analysis. Screenshot capture may have failed.";
            
            public static string AIAnalysisFailed(string error)
                => $"[Error] AI analysis failed: {error}. Check image files and AI service connectivity.";
            
            public static string MountPointAdjustmentFailed(string error)
                => $"[Error] Mount point adjustment failed: {error}. Check AI suggestions format.";
            
            public static string InvalidBackgroundColor(string colorHex)
                => $"[Error] Invalid background color format: '{colorHex}'. Please use hexadecimal format like '#E5E5E5'.";
            
            public static string InvalidCaptureResolution(int resolution)
                => $"[Error] Invalid capture resolution: {resolution}. Please use a positive value (recommended: 512-2048).";
            
            public static string SerializationFailed(string error)
                => $"[Error] Failed to serialize result: {error}. Internal serialization error.";
            
            public static string SmartMountProcessFailed(string error)
                => $"[Error] Smart mount point process execution failed: {error}";
            
            public static string CameraCreationFailed(string error)
                => $"[Error] Failed to create capture camera: {error}";
            
            public static string VisualizationMarkerFailed(string error)
                => $"[Error] Failed to create visualization marker: {error}";
            
            public static string BoundsCalculationFailed(string error)
                => $"[Error] Failed to calculate object bounds: {error}";
            
            public static string NoValidSuggestionsForAdjustment()
                => "[Error] No available adjustment suggestions. AI analysis may have failed or provided invalid suggestions.";
            
            public static string InvalidMaxIterations(int maxIterations)
                => $"[Error] Invalid max iterations: {maxIterations}. Please provide a positive value (recommended: 1-5).";
            
            public static string TemporaryFileCreationFailed(string fileName, string error)
                => $"[Error] Failed to create temporary file '{fileName}': {error}";
                
            public static string ImageFileVerificationFailed(string filePath)
                => $"[Error] Image file verification failed: '{filePath}'. File may be empty or corrupted.";
                
            public static string NoMountPointsToApply()
                => "[Error] No mount points provided to apply. Please check the mount points configuration.";
                
            public static string SceneInstanceNotFound()
                => "[Error] Scene instance not found. Please ensure the prefab was instantiated successfully.";
        }
    }
}