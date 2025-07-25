using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using UnityEditor;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using System.Reflection;

namespace com.MiAO.Unity.MCP.Utils
{
    /// <summary>
    /// Utility class for type conversion operations, extracted from GameObject.Manage for reuse
    /// </summary>
    public static class TypeConversionUtils
    {

        public class ModificationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";

            public static ModificationResult CreateSuccess(string message = "Modified successfully")
            {
                return new ModificationResult { Success = true, Message = message };
            }

            public static ModificationResult CreateFailure(string message)
            {
                return new ModificationResult { Success = false, Message = message };
            }
        }


        public static ModificationResult ProcessObjectModifications(object objToModify, SerializedMember serializedMember)
        {
            var messages = new List<string>();
            var objType = objToModify.GetType();
            
            try
            {
                // Process fields
                var fieldResult = ProcessMemberCollection(objToModify, objType, serializedMember.fields, 
                    "Field", ProcessFieldModification, messages);
                if (!fieldResult.Success) return fieldResult;

                // Process properties
                var propResult = ProcessMemberCollection(objToModify, objType, serializedMember.props, 
                    "Property", ProcessPropertyModification, messages);
                if (!propResult.Success) return propResult;

                return ModificationResult.CreateSuccess(
                    messages.Count > 0 ? string.Join(", ", messages) : "Modified successfully");
            }
            catch (Exception ex)
            {
                return ModificationResult.CreateFailure($"Exception during modification: {ex.Message}");
            }
        }

        private static ModificationResult ProcessMemberCollection(object objToModify, Type objType, 
            List<SerializedMember> members, string memberType,
            Func<object, Type, SerializedMember, ModificationResult> processFunc, List<string> messages)
        {
            if (members == null || members.Count == 0) 
                return ModificationResult.CreateSuccess();

            foreach (var member in members)
            {
                var result = processFunc(objToModify, objType, member);
                if (result.Success)
                {
                    messages.Add($"{memberType} '{member.name}' modified successfully");
                }
                else
                {
                    return result;
                }
            }
            return ModificationResult.CreateSuccess();
        }

        private static ModificationResult ProcessFieldModification(object objToModify, Type objType, SerializedMember field)
        {
            try
            {
                var fieldInfo = GetFieldInfo(objType, field.name);
                if (fieldInfo == null)
                {
                    return ModificationResult.CreateFailure(
                        $"Field '{field.name}' not found. Make sure the name is correct and case sensitive.");
                }

                // Step 1: Try to get existing instance from the field
                object existingInstance = null;
                try
                {
                    existingInstance = fieldInfo.GetValue(objToModify);
                }
                catch
                {
                    // If getting existing value fails, existingInstance remains null
                }

                var convertedValue = ConvertValue(field, fieldInfo.FieldType, field.typeName, existingInstance);
                
                if (!ValidateAssignment(convertedValue, fieldInfo.FieldType, field.name, "field"))
                {
                    return ModificationResult.CreateFailure(
                        $"Cannot assign null to value type field '{field.name}' of type '{fieldInfo.FieldType.Name}'");
                }

                fieldInfo.SetValue(objToModify, convertedValue);


                return ModificationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                return ModificationResult.CreateFailure(
                    $"Field '{field.name}' modification failed: {ex.Message}");
            }
        }

        private static ModificationResult ProcessPropertyModification(object objToModify, Type objType, SerializedMember prop)
        {
            try
            {
                var propertyInfo = GetPropertyInfo(objType, prop.name);
                if (propertyInfo == null)
                {
                    return ModificationResult.CreateFailure(
                        $"Property '{prop.name}' not found. Make sure the name is correct and case sensitive.");
                }

                if (!propertyInfo.CanWrite)
                {
                    return ModificationResult.CreateFailure($"Property '{prop.name}' is read-only");
                }

                // Step 1: Try to get existing instance from the property
                object existingInstance = null;
                try
                {
                    if (propertyInfo.CanRead)
                    {
                        existingInstance = propertyInfo.GetValue(objToModify);
                    }
                }
                catch
                {
                    // If getting existing value fails, existingInstance remains null
                }

                var convertedValue = ConvertValue(prop, propertyInfo.PropertyType, prop.typeName, existingInstance);
                
                if (!ValidateAssignment(convertedValue, propertyInfo.PropertyType, prop.name, "property"))
                {
                    return ModificationResult.CreateFailure(
                        $"Cannot assign null to value type property '{prop.name}' of type '{propertyInfo.PropertyType.Name}'");
                }

                propertyInfo.SetValue(objToModify, convertedValue);
                return ModificationResult.CreateSuccess($"Property '{prop.name}' set successfully");
            }
            catch (Exception ex)
            {
                return ModificationResult.CreateFailure($"Failed to set property '{prop.name}': {ex.Message}");
            }
        }

        #region Reflection and Validation Helpers

        private static FieldInfo GetFieldInfo(Type objType, string fieldName)
        {
            return objType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static PropertyInfo GetPropertyInfo(Type objType, string propertyName)
        {
            return objType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static bool ValidateAssignment(object value, Type targetType, string memberName, string memberType)
        {
            if (value == null && targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
            {
                UnityEngine.Debug.LogError($"Cannot assign null to value type {memberType} '{memberName}' of type '{targetType.Name}'");
                return false;
            }
            return true;
        }


        #endregion


        /// <summary>
        /// Convert SerializedMember to target type with support for arrays, enums, and Unity objects, optionally using existing instance
        /// </summary>
        public static object ConvertValue(SerializedMember member, Type targetType, string typeName, object existingInstance)
        {
            if (member == null)
                return null;

            try
            {
                return ConvertValueInternal(member, targetType, existingInstance);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TypeConversionUtils] Conversion failed for type '{targetType.Name}': {ex.Message}");
                throw;
            }
        }

        private static object ConvertValueInternal(SerializedMember member, Type targetType, object existingInstance = null)
        {

            var jsonElement = member.GetValue<JsonElement>();

            return ConvertJsonElementToTargetType(jsonElement, targetType, existingInstance);
        }


        #region Unified JSON Conversion Core

        /// <summary>
        /// Unified JsonElement basic value converter - handles common JsonValueKind patterns
        /// </summary>
        private static object ConvertJsonElementBasicValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => ExtractNumericValue(element),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object when element.TryGetProperty("instanceID", out var instanceIdProperty) => instanceIdProperty.GetInt32(),
                JsonValueKind.Object => element,
                JsonValueKind.Array => element,
                _ => element
            };
        }

        #endregion

        /// <summary>
        /// Extract instance ID from various value types
        /// </summary>
        private static int ExtractInstanceId(JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.GetInt32(),
                JsonValueKind.Object when jsonElement.TryGetProperty("instanceID", out var instanceIdProperty) => instanceIdProperty.GetInt32(),
                _ => 0
            };
        }


        /// <summary>
        /// Unified numeric value extraction with optional target type conversion
        /// </summary>
        private static object ExtractNumericValue(JsonElement element, Type targetType = null)
        {
            // If target type is specified, convert directly to that type
            if (targetType != null && targetType != typeof(object))
            {
                return ConvertJsonNumberToSpecificType(element, targetType);
            }
            
            // General numeric extraction (best fit)
            if (element.TryGetInt32(out int intValue))
                return intValue;
            if (element.TryGetInt64(out long longValue))
                return longValue;
            if (element.TryGetDouble(out double doubleValue))
                return doubleValue;
            return element.GetDecimal();
        }

        /// <summary>
        /// Convert JsonElement number to specific target type with flexible handling
        /// </summary>
        private static object ConvertJsonNumberToSpecificType(JsonElement element, Type targetType)
        {
            try
            {
                var doubleValue = element.GetDouble();
                
                return Type.GetTypeCode(targetType) switch
                {
                    TypeCode.Byte => Convert.ToByte(doubleValue),
                    TypeCode.SByte => Convert.ToSByte(doubleValue),
                    TypeCode.Int16 => Convert.ToInt16(doubleValue),
                    TypeCode.UInt16 => Convert.ToUInt16(doubleValue),
                    TypeCode.Int32 => Convert.ToInt32(doubleValue),
                    TypeCode.UInt32 => Convert.ToUInt32(doubleValue),
                    TypeCode.Int64 => Convert.ToInt64(doubleValue),
                    TypeCode.UInt64 => Convert.ToUInt64(doubleValue),
                    TypeCode.Single => Convert.ToSingle(doubleValue),
                    TypeCode.Double => doubleValue,
                    TypeCode.Decimal => Convert.ToDecimal(doubleValue),
                    _ => doubleValue
                };
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Failed to convert JSON number to {targetType.Name}: {ex.Message}");
                throw new Exception($"Failed to convert JSON number to {targetType.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert to Unity Object by instance ID
        /// </summary>
        private static object ConvertToUnityObject(JsonElement jsonElement, Type targetType, bool enableTransformSpecialHandling = true)
        {
            var instanceId = ExtractInstanceId(jsonElement);
            
            if (instanceId == 0)
                return null;

            var foundObject = EditorUtility.InstanceIDToObject(instanceId);
            
            if (foundObject == null)
                throw new InvalidCastException($"[Error] GameObject with InstanceID '{instanceId}' not found.");

            // Special handling for Transform - the instanceID might refer to a GameObject
            if (enableTransformSpecialHandling && targetType == typeof(Transform))
            {
                if (foundObject is GameObject gameObject)
                    return gameObject.transform;
                else if (foundObject is Transform transform)
                    return transform;
            }
            
            // Check if the found object is compatible with the target type
            if (targetType.IsAssignableFrom(foundObject.GetType()))
                return foundObject;
            else
            {
                throw new InvalidCastException($"[Error] Object '{foundObject.name}' is not compatible with the target type '{targetType.Name}'.");
            }
        }

        /// <summary>
        /// Check enum value validity
        /// </summary>
        private static bool CheckEnum(object value, Type enumType, out string errorMessage)
        {
            errorMessage = null;
            
            // Unified null check
            value = ExtractActualValue(value);
            if (value == null)
            {
                errorMessage = $"Cannot use null value for enum type '{enumType.Name}'";
                UnityEngine.Debug.LogError($"[MCP Enum Debug] ERROR: {errorMessage}");
                return false;
            }

            return value switch
            {
                string stringValue => ValidateEnumString(stringValue, enumType, out errorMessage),
                var numericValue when IsNumericType(numericValue.GetType()) => ValidateEnumNumeric(numericValue, enumType, out errorMessage),
                _ => SetUnsupportedTypeError(value, enumType, out errorMessage)
            };
        }

        /// <summary>
        /// Extract actual value from JsonElement if needed
        /// </summary>
        private static object ExtractActualValue(object value)
        {
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElementBasicValue(jsonElement);
            }
            return value;
        }



        /// <summary>
        /// Validate enum string value
        /// </summary>
        private static bool ValidateEnumString(string stringValue, Type enumType, out string errorMessage)
        {
            var enumName = ExtractEnumName(stringValue);
            
            if (Enum.TryParse(enumType, enumName, true, out var enumValue))
            {
                errorMessage = null;
                return true;
            }

            var validNames = string.Join(", ", Enum.GetNames(enumType));
            errorMessage = $"'{stringValue}' is not a valid value for enum type '{enumType.Name}'. Valid values are: {validNames}";
            return false;
        }

        /// <summary>
        /// Validate enum numeric value
        /// </summary>
        private static bool ValidateEnumNumeric(object numericValue, Type enumType, out string errorMessage)
        {
            var longValue = Convert.ToInt64(numericValue);
            
            // Handle Flags enum
            if (enumType.IsDefined(typeof(System.FlagsAttribute), false))
            {
                return ValidateFlagsEnumValue(longValue, enumType, out errorMessage);
            }
            
            // Check regular enum values
            if (Enum.IsDefined(enumType, numericValue))
            {
                errorMessage = null;
                return true;
            }

            var validValues = Enum.GetValues(enumType).Cast<object>()
                .Select(v => $"{v} ({Convert.ToInt64(v)})")
                .ToArray();
            var validValuesStr = string.Join(", ", validValues);
            errorMessage = $"Numeric value '{longValue}' is not defined in enum type '{enumType.Name}'. Valid values are: {validValuesStr}";
            return false;
        }

        /// <summary>
        /// Extract enum name from string
        /// </summary>
        private static string ExtractEnumName(string stringValue)
        {
            // Remove possible type prefix (e.g., "MyEnum.Value1" -> "Value1")
            var lastDotIndex = stringValue.LastIndexOf('.');
            return lastDotIndex >= 0 && lastDotIndex < stringValue.Length - 1 
                ? stringValue.Substring(lastDotIndex + 1) 
                : stringValue;
        }

        /// <summary>
        /// Set unsupported type error
        /// </summary>
        private static bool SetUnsupportedTypeError(object value, Type enumType, out string errorMessage)
        {
            errorMessage = $"Cannot use value '{value}' of type '{value.GetType().Name}' for enum type '{enumType.Name}'. Supported types: string (enum name), integer (enum value)";
            return false;
        }

        /// <summary>
        /// Validate whether the flag enum value is valid
        /// </summary>
        private static bool ValidateFlagsEnumValue(long numericValue, Type enumType, out string errorMessage)
        {
            errorMessage = null;
            
            // Get all valid enum values
            var enumValues = Enum.GetValues(enumType).Cast<object>()
                .Select(Convert.ToInt64)
                .Where(v => v != 0) // Exclude None = 0 value as it usually doesn't participate in bit operations
                .ToArray();
            
            // If value is 0, check if there's an enum value defined as 0 (usually None)
            if (numericValue == 0)
            {
                if (Enum.IsDefined(enumType, (int)numericValue))
                {
                    return true;
                }
                else
                {
                    errorMessage = $"Value '0' is not defined for Flags enum type '{enumType.Name}'";
                    return false;
                }
            }
            
            // For non-zero values, check if all bits correspond to valid enum values
            var remainingValue = numericValue;
            var usedFlags = new List<string>();
            
            // Bit-wise check (from largest to smallest)
            var sortedValues = enumValues.OrderByDescending(v => v).ToArray();
            
            foreach (var enumValue in sortedValues)
            {
                if ((remainingValue & enumValue) == enumValue)
                {
                    remainingValue &= ~enumValue; // Clear this bit
                    var enumName = Enum.GetName(enumType, enumValue);
                    if (!string.IsNullOrEmpty(enumName))
                    {
                        usedFlags.Add($"{enumName} ({enumValue})");
                    }
                }
            }
            
            // If there are remaining bits, it means invalid flags are included
            if (remainingValue != 0)
            {
                var validValues = Enum.GetValues(enumType).Cast<object>()
                    .Select(v => $"{v} ({Convert.ToInt64(v)})")
                    .ToArray();
                var validValuesStr = string.Join(", ", validValues);
                
                errorMessage = $"Flags enum value '{numericValue}' contains invalid bits. " +
                    $"Valid individual flags are: {validValuesStr}. " +
                    $"You can combine them using bitwise OR (|) operation.";
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Check if type is Unity built-in type (Vector3, Color, etc.)
        /// </summary>
        private static bool IsUnityBuiltInType(Type type)
        {
            return type == typeof(Vector2) || 
                   type == typeof(Vector3) || 
                   type == typeof(Vector4) || 
                   type == typeof(Color) || 
                   type == typeof(Quaternion) ||
                   type == typeof(Rect) ||
                   type == typeof(Bounds) ||
                   type.IsValueType && type.Namespace == "UnityEngine";
        }

        /// <summary>
        /// Convert JsonElement to target type with full recursive support
        /// </summary>
        private static object ConvertJsonElementToTargetType(JsonElement jsonElement, Type targetType)
        {
            return ConvertJsonElementToTargetType(jsonElement, targetType, null);
        }

        /// <summary>
        /// Convert JsonElement to target type with full recursive support, optionally using existing instance
        /// </summary>
        private static object ConvertJsonElementToTargetType(JsonElement jsonElement, Type targetType, object existingInstance)
        {
            // Handle null values
            if (jsonElement.ValueKind == JsonValueKind.Null)
                return null;

            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                return ConvertToUnityObject(jsonElement, targetType);
            }

            // Handle primitive types first
            if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
            {
                return ConvertJsonToPrimitive(jsonElement, targetType);
            }

            // Handle Unity built-in types
            if (IsUnityBuiltInType(targetType))
            {
                return ConvertJsonToUnityType(jsonElement, targetType);
            }

            // Handle enums
            if (targetType.IsEnum)
            {
                return ConvertJsonToEnum(jsonElement, targetType);
            }

            // Handle arrays
            if (targetType.IsArray)
            {
                return ConvertJsonToArray(jsonElement, targetType);
            }

            // Handle Lists and generic collections
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return ConvertJsonToList(jsonElement, targetType);
            }

            // Handle custom types (classes/structs)
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                return ConvertJsonToCustomType(jsonElement, targetType, existingInstance);
            }

            // Fallback to basic conversion
            return ConvertJsonElementBasicValue(jsonElement);
        }



        #region Unified Unity Type Conversion
        /// <summary>
        /// Convert JsonElement to Unity type (now uses unified property extraction)
        /// </summary>
        private static object ConvertJsonToUnityType(JsonElement jsonElement, Type targetType)
        {
            if (targetType == typeof(Vector2))
            {
                return new Vector2((float)jsonElement.GetProperty("x").GetSingle(), (float)jsonElement.GetProperty("y").GetSingle());
            }
            if (targetType == typeof(Vector3))
            {
                return new Vector3((float)jsonElement.GetProperty("x").GetSingle(), (float)jsonElement.GetProperty("y").GetSingle(), (float)jsonElement.GetProperty("z").GetSingle());
            }
            if (targetType == typeof(Vector4))
            {
                return new Vector4((float)jsonElement.GetProperty("x").GetSingle(), (float)jsonElement.GetProperty("y").GetSingle(), (float)jsonElement.GetProperty("z").GetSingle(), (float)jsonElement.GetProperty("w").GetSingle());
            }
            if (targetType == typeof(Color))
            {
                return new Color((float)jsonElement.GetProperty("r").GetSingle(), (float)jsonElement.GetProperty("g").GetSingle(), (float)jsonElement.GetProperty("b").GetSingle(), (float)jsonElement.GetProperty("a").GetSingle());
            }
            if (targetType == typeof(Quaternion))
            {
                return new Quaternion((float)jsonElement.GetProperty("x").GetSingle(), (float)jsonElement.GetProperty("y").GetSingle(), (float)jsonElement.GetProperty("z").GetSingle(), (float)jsonElement.GetProperty("w").GetSingle());
            }
            if (targetType == typeof(Rect))
            {
                return new Rect((float)jsonElement.GetProperty("x").GetSingle(), (float)jsonElement.GetProperty("y").GetSingle(), (float)jsonElement.GetProperty("width").GetSingle(), (float)jsonElement.GetProperty("height").GetSingle());
            }

            throw new Exception($"Unsupported Unity type: {targetType.Name}");
        }

        #endregion

        #region Unified Member Setting

        /// <summary>
        /// Unified member setting method with optional null validation
        /// </summary>
        private static bool TrySetMemberFromJson(object instance, Type instanceType, string memberName, JsonElement jsonValue, bool validateNullable = false)
        {
            try
            {
                return TrySetField(instance, instanceType, memberName, jsonValue, validateNullable) ||
                       TrySetProperty(instance, instanceType, memberName, jsonValue, validateNullable);
            }
            catch (Exception ex)
            {
                var operation = validateNullable ? "recursively" : "";
                UnityEngine.Debug.LogError($"[TypeConversionUtils] Failed to set member '{memberName}' {operation}: {ex.Message}");
                // Re-throw the exception to ensure it's properly handled
                throw new Exception($"Failed to set member '{memberName}' {operation}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Try to set field value from JSON
        /// </summary>
        private static bool TrySetField(object instance, Type instanceType, string memberName, JsonElement jsonValue, bool validateNullable)
        {
            var fieldInfo = instanceType.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo == null) return false;

            // Try to get existing instance from the field
            object existingInstance = null;
            try
            {
                existingInstance = fieldInfo.GetValue(instance);
            }
            catch
            {
                // If getting existing value fails, existingInstance remains null
            }

            var convertedValue = ConvertJsonElementToTargetType(jsonValue, fieldInfo.FieldType, existingInstance);
            
            if (validateNullable && !CanAssignValue(convertedValue, fieldInfo.FieldType))
                return true; // Skip assignment but consider it handled

            fieldInfo.SetValue(instance, convertedValue);
            return true;
        }

        /// <summary>
        /// Try to set property value from JSON
        /// </summary>
        private static bool TrySetProperty(object instance, Type instanceType, string memberName, JsonElement jsonValue, bool validateNullable)
        {
            var propertyInfo = instanceType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null || !propertyInfo.CanWrite) return false;

            // Try to get existing instance from the property
            object existingInstance = null;
            try
            {
                if (propertyInfo.CanRead)
                {
                    existingInstance = propertyInfo.GetValue(instance);
                }
            }
            catch
            {
                // If getting existing value fails, existingInstance remains null
            }

            var convertedValue = ConvertJsonElementToTargetType(jsonValue, propertyInfo.PropertyType, existingInstance);
            
            if (validateNullable && !CanAssignValue(convertedValue, propertyInfo.PropertyType))
                return true; // Skip assignment but consider it handled

            propertyInfo.SetValue(instance, convertedValue);
            return true;
        }

        /// <summary>
        /// Check if value can be assigned to target type (handles nullable validation)
        /// </summary>
        private static bool CanAssignValue(object value, Type targetType)
        {
            return value != null || !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        }


        #endregion

        /// <summary>
        /// Convert JsonElement to primitive type with flexible numeric conversion (now uses unified exception handling)
        /// </summary>
        private static object ConvertJsonToPrimitive(JsonElement jsonElement, Type targetType)
        {
            return TryConvertWithLogging(() =>
            {
                // Handle numbers with flexible conversion
                if (jsonElement.ValueKind == JsonValueKind.Number && IsNumericType(targetType))
                {
                    return ConvertJsonNumberToSpecificType(jsonElement, targetType);
                }
                
                // Handle specific types
                if (targetType == typeof(bool))
                    return jsonElement.GetBoolean();
                
                if (targetType == typeof(string))
                    return jsonElement.GetString();
                
                if (targetType == typeof(char))
                {
                    var str = jsonElement.GetString();
                    return !string.IsNullOrEmpty(str) ? str[0] : '\0';
                }
                
                // Fallback to basic conversion
                return Convert.ChangeType(ConvertJsonElementBasicValue(jsonElement), targetType);
            }, "JSON to primitive conversion", targetType);
        }



        /// <summary>
        /// Check if type is numeric (including float, which was missing from original)
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or 
                TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
                TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,
                _ => false
            };
        }

        /// <summary>
        /// Convert JsonElement to enum (now uses unified exception handling)
        /// </summary>
        private static object ConvertJsonToEnum(JsonElement jsonElement, Type enumType)
        {
            return TryConvertWithLogging(() =>
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var enumName = ExtractEnumName(jsonElement.GetString());
                    // Validate the enum
                    if (!CheckEnum(enumName, enumType, out string errorMessage))
                    {
                        throw new Exception(errorMessage);
                    }
                    return Enum.Parse(enumType, enumName, true);
                }
                else if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    var numericValue = jsonElement.GetInt64();
                    // Validate the enum
                    if (!CheckEnum(numericValue, enumType, out string errorMessage))
                    {
                        throw new Exception(errorMessage);
                    }
                    return Enum.ToObject(enumType, numericValue);
                }
                
                throw new Exception($"Failed to convert JSON to enum {enumType.Name}. Got {jsonElement.GetRawText()} but available Enum values are: {string.Join(", ", Enum.GetNames(enumType))}");
            }, "JSON to enum conversion", enumType);
        }

        #region Unified Collection Conversion

        /// <summary>
        /// Unified collection conversion method for arrays and lists
        /// </summary>
        private static object ConvertJsonToCollection(JsonElement jsonElement, Type collectionType, Type elementType, 
            Func<Type, int, object> createCollection, Action<object, object, int> addElement)
        {
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                return createCollection(collectionType, 0);
            }

            var elements = jsonElement.EnumerateArray().ToArray();
            var collection = createCollection(collectionType, elements.Length);

            for (int i = 0; i < elements.Length; i++)
            {
                var convertedElement = ConvertJsonElementToTargetType(elements[i], elementType);
                addElement(collection, convertedElement, i);
            }

            return collection;
        }

        /// <summary>
        /// Convert JsonElement to array (now uses unified collection converter)
        /// </summary>
        private static object ConvertJsonToArray(JsonElement jsonElement, Type arrayType)
        {
            var elementType = arrayType.GetElementType();
            
            return ConvertJsonToCollection(
                jsonElement, arrayType, elementType,
                createCollection: (type, length) => Array.CreateInstance(elementType, length),
                addElement: (collection, element, index) => ((Array)collection).SetValue(element, index)
            );
        }

        /// <summary>
        /// Convert JsonElement to List (now uses unified collection converter)
        /// </summary>
        private static object ConvertJsonToList(JsonElement jsonElement, Type listType)
        {
            var elementType = listType.GetGenericArguments()[0];
            var addMethod = listType.GetMethod("Add");
            
            return ConvertJsonToCollection(
                jsonElement, listType, elementType,
                createCollection: (type, length) => Activator.CreateInstance(type),
                addElement: (collection, element, index) => addMethod.Invoke(collection, new[] { element })
            );
        }

        #endregion

        #region Unified Exception Handling

        /// <summary>
        /// Unified exception handling wrapper for JSON conversion operations
        /// </summary>
        private static T TryConvertWithLogging<T>(Func<T> conversionFunc, string operationName, Type targetType = null)
        {
            try
            {
                return conversionFunc();
            }
            catch (Exception ex)
            {
                var typeInfo = targetType != null ? $" for type '{targetType.Name}'" : "";
                UnityEngine.Debug.LogError($"[TypeConversionUtils] {operationName} failed{typeInfo}: {ex.Message}");
                throw new Exception($"Failed to {operationName} for type '{targetType.Name}': {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Convert JsonElement to custom type with full recursive support. 
        /// First tries to use existing instance, then creates new instance if needed.
        /// </summary>
        private static object ConvertJsonToCustomType(JsonElement jsonElement, Type targetType, object existingInstance = null)
        {
            return TryConvertWithLogging(() =>
            {
                object instance = null;
                
                // Step 1: Try to use existing instance
                if (existingInstance != null && targetType.IsAssignableFrom(existingInstance.GetType()))
                {
                    instance = existingInstance;
                }
                else
                {
                    // Step 2: Try to create new instance using constructor
                    try
                    {
                        // Try default constructor first
                        instance = Activator.CreateInstance(targetType);
                    }
                    catch (Exception ex)
                    {
                        // Step 3: If creation fails, throw clear error message
                        throw new InvalidOperationException(
                            $"Failed to create instance of type '{targetType.Name}'. " +
                            $"The type must have a parameterless constructor. " +
                            $"Constructor error: {ex.Message}");
                    }
                }
                
                // Recursively set all fields and properties
                foreach (var property in jsonElement.EnumerateObject())
                {
                    TrySetMemberFromJson(instance, targetType, property.Name, property.Value, validateNullable: true);
                }
                
                return instance;
            }, "Custom type conversion", targetType);
        }
    }
}


