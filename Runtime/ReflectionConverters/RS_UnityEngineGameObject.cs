#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.MCP.Common.Reflection.Convertor;
using com.MiAO.MCP.Utils;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.MiAO.MCP.Reflection.Convertor
{
    public partial class RS_UnityEngineGameObject : RS_GenericUnity<UnityEngine.GameObject>
    {
        protected override IEnumerable<string> ignoredProperties => base.ignoredProperties
            .Concat(new[]
            {
                nameof(UnityEngine.GameObject.gameObject),
                nameof(UnityEngine.GameObject.transform),
                nameof(UnityEngine.GameObject.scene)
            });
        protected override SerializedMember InternalSerialize(Reflector reflector, object obj, Type type, string name = null, bool recursive = true,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            ILogger? logger = null)
        {
            var unityObject = obj as UnityEngine.GameObject;
            if (recursive)
            {
                return new SerializedMember()
                {
                    name = name,
                    typeName = type.FullName,
                    fields = SerializeFields(reflector, obj, flags),
                    props = SerializeProperties(reflector, obj, flags)
                }.SetValue(new ObjectRef(unityObject.GetInstanceID()));
            }
            else
            {
                var objectRef = new ObjectRef(unityObject.GetInstanceID());
                return SerializedMember.FromValue(type, objectRef, name);
            }
        }

        protected override List<SerializedMember> SerializeFields(Reflector reflector, object obj, BindingFlags flags, ILogger? logger = null)
        {
            var serializedFields = base.SerializeFields(reflector, obj, flags) ?? new();

            var go = obj as UnityEngine.GameObject;
            var components = go.GetComponents<UnityEngine.Component>();

            serializedFields.Capacity += components.Length;

            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                var componentType = component.GetType();
                var componentSerialized = reflector.Serialize(
                    obj: component,
                    type: componentType,
                    name: $"component_{i}",
                    recursive: true,
                    flags: flags,
                    logger: logger
                );
                serializedFields.Add(componentSerialized);
            }
            return serializedFields;
        }

        protected override bool SetValue(Reflector reflector, ref object obj, Type type, JsonElement? value, ILogger? logger = null)
        {
            return true;
        }

        protected override StringBuilder? ModifyField(Reflector reflector, ref object obj, SerializedMember fieldValue, StringBuilder? stringBuilder = null, int depth = 0,
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            ILogger? logger = null)
        {
            var go = obj as UnityEngine.GameObject;

            var type = TypeUtils.GetType(fieldValue.typeName);
            if (type == null)
                return stringBuilder?.AppendLine($"[Error] Type not found: {fieldValue.typeName}");

            // If not a component, use base method
            if (!typeof(UnityEngine.Component).IsAssignableFrom(type))
                return base.ModifyField(reflector, ref obj, fieldValue, stringBuilder, depth, flags);

            var index = -1;
            if (fieldValue.name.StartsWith("component_"))
                int.TryParse(fieldValue.name
                    .Replace("component_", "")
                    .Replace("[", "")
                    .Replace("]", ""), out index);

            var componentInstanceID = fieldValue.GetInstanceID();
            if (componentInstanceID == 0 && index == -1)
                return stringBuilder?.AppendLine($"[Error] Component 'instanceID' is not provided. Use 'instanceID' or name '[index]' to specify the component. '{fieldValue.name}' is not valid.");

            var allComponents = go.GetComponents<UnityEngine.Component>();
            var component = componentInstanceID == 0
                ? index >= 0 && index < allComponents.Length
                    ? allComponents[index]
                    : null
                : allComponents.FirstOrDefault(c => c.GetInstanceID() == componentInstanceID);

            if (component == null)
                return stringBuilder?.AppendLine($"[Error] Component not found. Use 'instanceID' or name 'component_[index]' to specify the component.");

            var componentObject = (object)component;
            return reflector.Populate(ref componentObject, fieldValue, logger: logger);
        }

        protected override StringBuilder? ModifyProperty(Reflector reflector, ref object obj, SerializedMember property, StringBuilder? stringBuilder = null, int depth = 0,
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            ILogger? logger = null)
        {
            var go = obj as UnityEngine.GameObject;
            if (go == null)
                return stringBuilder?.AppendLine($"[Error] Object is not a GameObject");

            try
            {
                // Use reflection to find the property on GameObject
                var propertyInfo = typeof(UnityEngine.GameObject).GetProperty(property.name, flags);
                
                if (propertyInfo == null)
                {
                    // If not found in GameObject, try base Object class (for properties like name)
                    propertyInfo = typeof(UnityEngine.Object).GetProperty(property.name, flags);
                }

                if (propertyInfo == null)
                {
                    // Property not found, delegate to base method
                    return base.ModifyProperty(reflector, ref obj, property, stringBuilder, depth, flags, logger);
                }

                // Check if the property is writable
                if (!propertyInfo.CanWrite)
                {
                    return stringBuilder?.AppendLine($"[Error] Property '{property.name}' is read-only and cannot be modified");
                }

                // Validate property type
                var expectedType = propertyInfo.PropertyType;
                var actualTypeName = property.typeName;
                
                if (!IsTypeCompatible(expectedType, actualTypeName))
                {
                    return stringBuilder?.AppendLine($"[Error] Property '{property.name}' expects type '{expectedType.FullName}' but got '{actualTypeName}'");
                }

                // Convert and set the property value
                var convertedValue = ConvertPropertyValue(property, expectedType);
                if (convertedValue == null && !IsNullableType(expectedType))
                {
                    return stringBuilder?.AppendLine($"[Error] Failed to convert value for property '{property.name}'");
                }

                // Set the property value
                propertyInfo.SetValue(go, convertedValue);
                
                return stringBuilder?.AppendLine($"[Success] GameObject property '{property.name}' changed to '{convertedValue}'");
            }
            catch (Exception ex)
            {
                return stringBuilder?.AppendLine($"[Error] Failed to modify property '{property.name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Check if the expected type is compatible with the provided type name
        /// </summary>
        private bool IsTypeCompatible(Type expectedType, string actualTypeName)
        {
            if (expectedType.FullName == actualTypeName)
                return true;

            // Handle common type aliases and conversions
            var typeMap = new Dictionary<string, string[]>
            {
                { "System.String", new[] { "string" } },
                { "System.Int32", new[] { "int", "System.Int32" } },
                { "System.Boolean", new[] { "bool", "System.Boolean" } },
                { "System.Single", new[] { "float", "System.Single" } },
                { "UnityEngine.LayerMask", new[] { "LayerMask", "UnityEngine.LayerMask", "System.Int32", "int" } }
            };

            if (typeMap.ContainsKey(expectedType.FullName))
            {
                return typeMap[expectedType.FullName].Contains(actualTypeName);
            }

            // Try to get the actual type and check if it's assignable
            var actualType = TypeUtils.GetType(actualTypeName);
            if (actualType != null)
            {
                return expectedType.IsAssignableFrom(actualType);
            }

            return false;
        }

        /// <summary>
        /// Convert the property value to the expected type
        /// </summary>
        private object? ConvertPropertyValue(SerializedMember property, Type expectedType)
        {
            try
            {
                if (expectedType == typeof(string))
                    return property.GetValue<string>();

                if (expectedType == typeof(int))
                    return property.GetValue<int>();

                if (expectedType == typeof(bool))
                    return property.GetValue<bool>();

                if (expectedType == typeof(float))
                    return property.GetValue<float>();

                if (expectedType == typeof(UnityEngine.LayerMask))
                {
                    // LayerMask can be set from int
                    var intValue = property.GetValue<int>();
                    return (UnityEngine.LayerMask)intValue;
                }

                // For complex types, try to use the generic GetValue
                var methodInfo = typeof(SerializedMember).GetMethod("GetValue").MakeGenericMethod(expectedType);
                return methodInfo.Invoke(property, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a type is nullable
        /// </summary>
        private bool IsNullableType(Type type)
        {
            return !type.IsValueType || (Nullable.GetUnderlyingType(type) != null);
        }
    }
}