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
        }


        public static ModificationResult ProcessObjectModifications(object objToModify, SerializedMember serializedMember)
        {
            var result = new ModificationResult();
            var messages = new List<string>();
            var objType = objToModify.GetType();
            
            try
            {
                // Process fields
                if (serializedMember.fields != null && serializedMember.fields.Count > 0)
                {
                    foreach (var field in serializedMember.fields)
                    {
                        var fieldResult = ProcessFieldModification(objToModify, objType, field);
                        if (fieldResult.Success)
                        {
                            messages.Add($"Field '{field.name}' modified successfully");
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = fieldResult.Message;
                            return result;
                        }
                    }
                }

                // Process properties
                if (serializedMember.props != null && serializedMember.props.Count > 0)
                {
                    foreach (var prop in serializedMember.props)
                    {
                        var propResult = ProcessPropertyModification(objToModify, objType, prop);
                        if (propResult.Success)
                        {
                            messages.Add($"Property '{prop.name}' modified successfully");
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = propResult.Message;
                            return result;
                        }
                    }
                }

                result.Success = true;
                result.Message = messages.Count > 0 ? string.Join(", ", messages) : "Modified successfully";
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Exception during modification: {ex.Message}";
                return result;
            }
        }

        private static ModificationResult ProcessFieldModification(object objToModify, Type objType, SerializedMember field)
        {
            var result = new ModificationResult();
            
            try
            {
                var fieldInfo = objType.GetField(field.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo == null)
                {
                    result.Success = false;
                    result.Message = $"Field '{field.name}' not found. Make sure the name is correct and case sensitive.";
                    return result;
                }

                var fieldType = fieldInfo.FieldType;
                
                var convertedValue = TypeConversionUtils.ConvertValue(field, fieldType, field.typeName);
                
                if (convertedValue == null && fieldType.IsValueType && Nullable.GetUnderlyingType(fieldType) == null)
                {
                    result.Success = false;
                    result.Message = $"Cannot assign null to value type field '{field.name}' of type '{fieldType.Name}'";
                    return result;
                }

                fieldInfo.SetValue(objToModify, convertedValue);
                result.Success = true;
                result.Message = $"Field '{field.name}' set successfully";
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Failed to set field '{field.name}': {ex.Message}";
                return result;
            }
        }

        private static ModificationResult ProcessPropertyModification(object objToModify, Type objType, SerializedMember prop)
        {
            var result = new ModificationResult();
            
            try
            {
                var propertyInfo = objType.GetProperty(prop.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (propertyInfo == null)
                {
                    result.Success = false;
                    result.Message = $"Property '{prop.name}' not found. Make sure the name is correct and case sensitive.";
                    return result;
                }

                if (!propertyInfo.CanWrite)
                {
                    result.Success = false;
                    result.Message = $"Property '{prop.name}' is read-only";
                    return result;
                }

                var propertyType = propertyInfo.PropertyType;
                var convertedValue = TypeConversionUtils.ConvertValue(prop, propertyType, prop.typeName);
                
                if (convertedValue == null && propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
                {
                    result.Success = false;
                    result.Message = $"Cannot assign null to value type property '{prop.name}' of type '{propertyType.Name}'";
                    return result;
                }

                propertyInfo.SetValue(objToModify, convertedValue);
                result.Success = true;
                result.Message = $"Property '{prop.name}' set successfully";
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Failed to set property '{prop.name}': {ex.Message}";
                return result;
            }
        }


        /// <summary>
        /// Convert SerializedMember to target type with support for arrays, enums, and Unity objects
        /// </summary>
        public static object ConvertValue(SerializedMember member, Type targetType, string typeName)
        {
            if (member == null)
                return null;

            // Handle array types
            if (targetType.IsArray)
            {
                return ConvertArray(member, targetType, typeName);
            }

            // Handle Unity Object references by instance ID
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                return ConvertToUnityObject(member, targetType, enableTransformSpecialHandling: false);
            }

            // Handle enums with strict validation
            if (targetType.IsEnum)
            {
                // Extract the raw value from SerializedMember
                object rawValue = member.GetValue<object>();

                // Check if the enum value is valid
                if (!CheckEnum(rawValue, targetType, out string errorMessage))
                {
                    throw new InvalidCastException(errorMessage);
                }
            }

            // Handle basic types
            return ConvertFromSerializedMember(member, targetType);
        }

        /// <summary>
        /// Convert array from SerializedMember
        /// </summary>
        public static object ConvertArray(SerializedMember member, Type arrayType, string typeName)
        {
            var targetElementType = arrayType.GetElementType();
            
            // Strategy 1: JsonElement array (most common case)
            var jsonElementArray = TryGetJsonElementArray(member);
            if (jsonElementArray != null)
            {
                return ConvertToTypedArray(jsonElementArray, targetElementType);
            }
            
            // Strategy 2: Generic approach (handles object[], int[], and all other array types)
            var genericArray = TryGetGenericArray(member, arrayType);
            if (genericArray != null)
            {
                return ConvertToTypedArray(genericArray, targetElementType);
            }

            // Return empty array if all strategies fail
            return Array.CreateInstance(targetElementType, 0);
        }

        /// <summary>
        /// Try to get array from JsonElement
        /// </summary>
        private static object[] TryGetJsonElementArray(SerializedMember member)
        {
            try
            {
                var jsonElement = member.GetValue<JsonElement>();
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    return jsonElement.EnumerateArray()
                        .Select(ConvertJsonElementToObject)
                        .ToArray();
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Try to get generic array from SerializedMember
        /// </summary>
        private static object[] TryGetGenericArray(SerializedMember member, Type arrayType)
        {
            try
            {
                var methodInfo = typeof(SerializedMember).GetMethod("GetValue").MakeGenericMethod(arrayType);
                var result = methodInfo.Invoke(member, null);
                if (result is Array genericArray)
                {
                    var objectArray = new object[genericArray.Length];
                    for (int i = 0; i < genericArray.Length; i++)
                    {
                        objectArray[i] = genericArray.GetValue(i);
                    }
                    return objectArray;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Convert object array to typed array
        /// </summary>
        private static Array ConvertToTypedArray(object[] sourceArray, Type targetElementType)
        {
            var typedArray = Array.CreateInstance(targetElementType, sourceArray.Length);
            
            for (int i = 0; i < sourceArray.Length; i++)
            {
                var convertedElement = ConvertArrayElement(sourceArray[i], targetElementType);
                typedArray.SetValue(convertedElement, i);
            }
            
            return typedArray;
        }
        
        /// <summary>
        /// Convert array element to target type
        /// </summary>
        private static object ConvertArrayElement(object elementValue, Type targetElementType)
        {
            if (elementValue == null)
                return null;

            // Handle Unity Object references by instance ID
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetElementType))
            {
                var result = ConvertToUnityObject(elementValue, targetElementType);
                // Check if the result is an error string
                if (result is string errorString && errorString.StartsWith("[Error]"))
                {
                    throw new InvalidCastException(errorString);
                }
                return result;
            }

            // Handle basic types
            return ConvertToBasicType(elementValue, targetElementType);
        }

        /// <summary>
        /// Convert JsonElement to object
        /// </summary>
        private static object ConvertJsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return ExtractNumericValue(element);
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                    // Handle object with instanceID
                    if (element.TryGetProperty("instanceID", out var instanceIdProperty))
                    {
                        return instanceIdProperty.GetInt32();
                    }
                    return element;
                default:
                    return element;
            }
        }

        /// <summary>
        /// Extract instance ID from various value types
        /// </summary>
        private static int ExtractInstanceId(object value)
        {
            return value switch
            {
                int intValue => intValue,
                SerializedMember member => ExtractInstanceIdFromSerializedMember(member),
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number => jsonElement.GetInt32(),
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object &&
                    jsonElement.TryGetProperty("instanceID", out var instanceIdProperty) => instanceIdProperty.GetInt32(),
                string strValue when int.TryParse(strValue, out int parsedId) => parsedId,
                _ => 0
            };
        }

        /// <summary>
        /// Extract instance ID from SerializedMember
        /// </summary>
        private static int ExtractInstanceIdFromSerializedMember(SerializedMember member)
        {
            try
            {
                // First try to get the instanceID directly
                return member.GetValue<int>();
            }
            catch
            {
                try
                {
                    // Try to get from ObjectRef
                    var objectRef = member.GetValue<ObjectRef>();
                    return objectRef?.instanceID ?? 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Extract numeric value from JsonElement
        /// </summary>
        private static object ExtractNumericValue(JsonElement element)
        {
            if (element.TryGetInt32(out int intValue))
                return intValue;
            if (element.TryGetInt64(out long longValue))
                return longValue;
            if (element.TryGetDouble(out double doubleValue))
                return doubleValue;
            return element.GetDecimal();
        }

        /// <summary>
        /// Convert to Unity Object by instance ID
        /// </summary>
        private static object ConvertToUnityObject(object value, Type targetType, bool enableTransformSpecialHandling = true)
        {
            var instanceId = ExtractInstanceId(value);
            
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
        /// Convert to basic type
        /// </summary>
        private static object ConvertToBasicType(object value, Type targetType)
        {
            // Direct type assignment if compatible
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            // Handle enum conversion specially
            if (targetType.IsEnum)
            {
                return ConvertToEnum(value, targetType);
            }

            // Convert using System.Convert
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Convert value to enum with proper validation and conversion
        /// </summary>
        private static object ConvertToEnum(object value, Type enumType)
        {
            // Validate enum value first
            if (!CheckEnum(value, enumType, out string errorMessage))
            {
                throw new InvalidCastException(errorMessage);
            }

            // Extract actual value from JsonElement if needed
            value = ExtractActualValue(value);

            // Perform the actual conversion based on value type
            return value switch
            {
                string stringValue => ConvertStringToEnum(stringValue, enumType),
                var numericValue when IsNumericType(numericValue.GetType()) => Enum.ToObject(enumType, numericValue),
                _ => throw new InvalidCastException($"Cannot convert value '{value}' of type '{value.GetType().Name}' to enum type '{enumType.Name}'")
            };
        }

        /// <summary>
        /// Convert string to enum
        /// </summary>
        private static object ConvertStringToEnum(string stringValue, Type enumType)
        {
            var enumName = ExtractEnumName(stringValue);
            return Enum.Parse(enumType, enumName, true);
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
                return ConvertJsonElementToObject(jsonElement);
            }
            return value;
        }

        /// <summary>
        /// Check if type is numeric
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or 
                TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => true,
                _ => false
            };
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
        /// Convert from SerializedMember using reflection
        /// </summary>
        private static object ConvertFromSerializedMember(SerializedMember member, Type targetType)
        {
            try
            {
                var methodInfo = typeof(SerializedMember).GetMethod("GetValue").MakeGenericMethod(targetType);
                return methodInfo.Invoke(member, null);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TypeConversionUtils] Exception in ConvertFromSerializedMember: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return null;
            }
        }
    }
}
