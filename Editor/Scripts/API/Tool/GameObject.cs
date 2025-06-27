#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.IvanMurzak.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;

namespace com.IvanMurzak.Unity.MCP.Editor.API
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
        }
    }
}