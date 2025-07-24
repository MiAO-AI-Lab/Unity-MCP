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

                var convertedValue = ConvertValue(field, fieldInfo.FieldType, field.typeName);
                
                if (!ValidateAssignment(convertedValue, fieldInfo.FieldType, field.name, "field"))
                {
                    return ModificationResult.CreateFailure(
                        $"Cannot assign null to value type field '{field.name}' of type '{fieldInfo.FieldType.Name}'");
                }

                fieldInfo.SetValue(objToModify, convertedValue);
                return ModificationResult.CreateSuccess($"Field '{field.name}' set successfully");
            }
            catch (Exception ex)
            {
                return ModificationResult.CreateFailure($"Failed to set field '{field.name}': {ex.Message}");
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

                var convertedValue = ConvertValue(prop, propertyInfo.PropertyType, prop.typeName);
                
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

        /// <summary>
        /// Safe execution wrapper that catches exceptions and returns null
        /// </summary>
        private static T TryExecute<T>(Func<T> action) where T : class
        {
            try
            {
                return action();
            }
            catch
            {
                return null;
            }
        }

        #endregion


        /// <summary>
        /// Convert SerializedMember to target type with support for arrays, enums, and Unity objects
        /// </summary>
        public static object ConvertValue(SerializedMember member, Type targetType, string typeName)
        {
            if (member == null)
                return null;

            try
            {
                return ConvertValueInternal(member, targetType, typeName);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TypeConversionUtils] Conversion failed for type '{targetType.Name}': {ex.Message}");
                throw;
            }
        }

        private static object ConvertValueInternal(SerializedMember member, Type targetType, string typeName)
        {
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
                return ConvertToEnumWithValidation(member, targetType);
            }

            // Handle List and other generic types
            if (targetType.IsGenericType)
            {
                return ConvertGenericType(member, targetType);
            }

            // Handle custom types (classes/structs)
            if (IsCustomType(targetType))
            {
                return ConvertCustomType(member, targetType);
            }

            // Handle basic types
            return ConvertFromSerializedMember(member, targetType);
        }

        private static object ConvertToEnumWithValidation(SerializedMember member, Type targetType)
        {
            // Extract the raw value from SerializedMember
            object rawValue = member.GetValue<object>();

            // Check if the enum value is valid
            if (!CheckEnum(rawValue, targetType, out string errorMessage))
            {
                throw new InvalidCastException(errorMessage);
            }

            return ConvertToEnum(rawValue, targetType);
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
            return TryExecute(() =>
            {
                var jsonElement = member.GetValue<JsonElement>();
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    return jsonElement.EnumerateArray()
                        .Select(ConvertJsonElementToObject)
                        .ToArray();
                }
                return null;
            });
        }

        /// <summary>
        /// Try to get generic array from SerializedMember
        /// </summary>
        private static object[] TryGetGenericArray(SerializedMember member, Type arrayType)
        {
            return TryExecute(() =>
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
                return null;
            });
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
                // Try to get from ObjectRef
                var objectRef = TryExecute(() => member.GetValue<ObjectRef>());
                return objectRef?.instanceID ?? 0;
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
                LogConversionError("ConvertFromSerializedMember", targetType, ex);
                return null;
            }
        }

        /// <summary>
        /// Centralized logging for conversion errors
        /// </summary>
        private static void LogConversionError(string methodName, Type targetType, Exception ex)
        {
            UnityEngine.Debug.LogError($"[TypeConversionUtils] Exception in {methodName} for type '{targetType.Name}': {ex.Message}");
        }

        /// <summary>
        /// Check if type is a custom type (not primitive, not Unity built-in)
        /// </summary>
        private static bool IsCustomType(Type type)
        {
            return !type.IsPrimitive && 
                   !type.IsEnum && 
                   type != typeof(string) && 
                   type != typeof(decimal) && 
                   type != typeof(DateTime) &&
                   !typeof(UnityEngine.Object).IsAssignableFrom(type) &&
                   !IsUnityBuiltInType(type);
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
        /// Convert generic types (List, Dictionary, etc.)
        /// </summary>
        private static object ConvertGenericType(SerializedMember member, Type targetType)
        {
            try
            {
                // Handle List<T> specifically
                if (IsListType(targetType))
                {
                    return ConvertToList(member, targetType);
                }
                
                // For other generic types, try the default method
                return ConvertFromSerializedMember(member, targetType);
            }
            catch (Exception ex)
            {
                LogConversionError("ConvertGenericType", targetType, ex);
                return null;
            }
        }

        /// <summary>
        /// Check if type is List&lt;T&gt;
        /// </summary>
        private static bool IsListType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        /// <summary>
        /// Convert to List<T>
        /// </summary>
        private static object ConvertToList(SerializedMember member, Type listType)
        {
            var elementType = listType.GetGenericArguments()[0];
            var listInstance = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");

            // Try to get JsonElement array
            var jsonElementArray = TryGetJsonElementArray(member);
            if (jsonElementArray != null)
            {
                foreach (var element in jsonElementArray)
                {
                    var convertedElement = ConvertObjectToTargetType(element, elementType);
                    addMethod.Invoke(listInstance, new[] { convertedElement });
                }
                return listInstance;
            }

            // Try to get generic array
            var genericArray = TryGetGenericArray(member, elementType.MakeArrayType());
            if (genericArray != null)
            {
                foreach (var element in genericArray)
                {
                    var convertedElement = ConvertObjectToTargetType(element, elementType);
                    addMethod.Invoke(listInstance, new[] { convertedElement });
                }
                return listInstance;
            }

            return listInstance;
        }

        /// <summary>
        /// Convert custom types from JSON
        /// </summary>
        private static object ConvertCustomType(SerializedMember member, Type targetType)
        {
            try
            {
                // Try to get JsonElement
                var jsonElement = member.GetValue<JsonElement>();
                if (jsonElement.ValueKind == JsonValueKind.Object)
                {
                    var result = TryExecute(() => ConvertJsonElementToTargetType(jsonElement, targetType));
                    if (result != null) return result;
                }
            }
            catch { }

            // Fallback to default method
            return ConvertFromSerializedMember(member, targetType);
        }

        /// <summary>
        /// Convert JsonElement to target type with full recursive support
        /// </summary>
        private static object ConvertJsonElementToTargetType(JsonElement jsonElement, Type targetType)
        {
            // Handle null values
            if (jsonElement.ValueKind == JsonValueKind.Null)
                return null;

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
                return ConvertJsonToCustomType(jsonElement, targetType);
            }

            // Fallback to basic conversion
            return ConvertJsonElementToObject(jsonElement);
        }

        /// <summary>
        /// Convert object to target type
        /// </summary>
        private static object ConvertObjectToTargetType(object value, Type targetType)
        {
            if (value == null) return null;
            
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (value is JsonElement jsonElement)
                return ConvertJsonElementToTargetType(jsonElement, targetType);

            return ConvertToBasicType(value, targetType);
        }

        /// <summary>
        /// Convert JsonElement to Unity type
        /// </summary>
        private static object ConvertJsonToUnityType(JsonElement jsonElement, Type targetType)
        {
            if (targetType == typeof(Vector2))
            {
                return new Vector2(
                    jsonElement.GetProperty("x").GetSingle(),
                    jsonElement.GetProperty("y").GetSingle()
                );
            }
            
            if (targetType == typeof(Vector3))
            {
                return new Vector3(
                    jsonElement.GetProperty("x").GetSingle(),
                    jsonElement.GetProperty("y").GetSingle(),
                    jsonElement.GetProperty("z").GetSingle()
                );
            }

            if (targetType == typeof(Color))
            {
                return new Color(
                    jsonElement.GetProperty("r").GetSingle(),
                    jsonElement.GetProperty("g").GetSingle(),
                    jsonElement.GetProperty("b").GetSingle(),
                    jsonElement.TryGetProperty("a", out var aProperty) ? aProperty.GetSingle() : 1.0f
                );
            }

            // For other Unity types, try using JsonSerializer
            try
            {
                return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
            }
            catch
            {
                return Activator.CreateInstance(targetType);
            }
        }

        /// <summary>
        /// Set member (field or property) from JSON
        /// </summary>
        private static void SetMemberFromJson(object instance, Type instanceType, string memberName, JsonElement jsonValue)
        {
            try
            {
                // Try field first
                var fieldInfo = instanceType.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    var convertedValue = ConvertJsonElementToTargetType(jsonValue, fieldInfo.FieldType);
                    fieldInfo.SetValue(instance, convertedValue);
                    return;
                }

                // Try property
                var propertyInfo = instanceType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    var convertedValue = ConvertJsonElementToTargetType(jsonValue, propertyInfo.PropertyType);
                    propertyInfo.SetValue(instance, convertedValue);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Failed to set member '{memberName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Convert JsonElement to primitive type with flexible numeric conversion
        /// </summary>
        private static object ConvertJsonToPrimitive(JsonElement jsonElement, Type targetType)
        {
            try
            {
                // Handle numbers with flexible conversion
                if (jsonElement.ValueKind == JsonValueKind.Number && IsNumericType(targetType))
                {
                    return ConvertJsonNumberToType(jsonElement, targetType);
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
                return Convert.ChangeType(ConvertJsonElementToObject(jsonElement), targetType);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Failed to convert JSON to primitive type {targetType.Name}: {ex.Message}");
                return GetDefaultValue(targetType);
            }
        }

        /// <summary>
        /// Convert JSON number to specific numeric type with flexible handling
        /// </summary>
        private static object ConvertJsonNumberToType(JsonElement jsonElement, Type targetType)
        {
            try
            {
                // First try to get as double for maximum precision
                var doubleValue = jsonElement.GetDouble();
                
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
                    TypeCode.Single => Convert.ToSingle(doubleValue), // This handles double to float conversion
                    TypeCode.Double => doubleValue,
                    TypeCode.Decimal => Convert.ToDecimal(doubleValue),
                    _ => doubleValue
                };
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Failed to convert JSON number to {targetType.Name}: {ex.Message}");
                return GetDefaultValue(targetType);
            }
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
        /// Convert JsonElement to enum
        /// </summary>
        private static object ConvertJsonToEnum(JsonElement jsonElement, Type enumType)
        {
            try
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var enumName = ExtractEnumName(jsonElement.GetString());
                    return Enum.Parse(enumType, enumName, true);
                }
                else if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    var numericValue = jsonElement.GetInt64();
                    return Enum.ToObject(enumType, numericValue);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Failed to convert JSON to enum type {enumType.Name}: {ex.Message}");
            }
            
            return GetDefaultValue(enumType);
        }

        /// <summary>
        /// Convert JsonElement to array
        /// </summary>
        private static object ConvertJsonToArray(JsonElement jsonElement, Type arrayType)
        {
            var elementType = arrayType.GetElementType();
            
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                return Array.CreateInstance(elementType, 0);
            }
            
            var elements = jsonElement.EnumerateArray().ToArray();
            var result = Array.CreateInstance(elementType, elements.Length);
            
            for (int i = 0; i < elements.Length; i++)
            {
                var convertedElement = ConvertJsonElementToTargetType(elements[i], elementType);
                result.SetValue(convertedElement, i);
            }
            
            return result;
        }

        /// <summary>
        /// Convert JsonElement to List
        /// </summary>
        private static object ConvertJsonToList(JsonElement jsonElement, Type listType)
        {
            var elementType = listType.GetGenericArguments()[0];
            var listInstance = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");
            
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                return listInstance;
            }
            
            foreach (var element in jsonElement.EnumerateArray())
            {
                var convertedElement = ConvertJsonElementToTargetType(element, elementType);
                addMethod.Invoke(listInstance, new[] { convertedElement });
            }
            
            return listInstance;
        }

        /// <summary>
        /// Convert JsonElement to custom type with full recursive support
        /// </summary>
        private static object ConvertJsonToCustomType(JsonElement jsonElement, Type targetType)
        {
            try
            {
                var instance = Activator.CreateInstance(targetType);
                
                // Recursively set all fields and properties
                foreach (var property in jsonElement.EnumerateObject())
                {
                    SetMemberFromJsonRecursive(instance, targetType, property.Name, property.Value);
                }
                
                return instance;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Failed to create custom type {targetType.Name}: {ex.Message}");
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Exception details: {ex}");
                return GetDefaultValue(targetType);
            }
        }

        /// <summary>
        /// Set member with full recursive support
        /// </summary>
        private static void SetMemberFromJsonRecursive(object instance, Type instanceType, string memberName, JsonElement jsonValue)
        {
            try
            {
                // Try field first
                var fieldInfo = instanceType.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    var convertedValue = ConvertJsonElementToTargetType(jsonValue, fieldInfo.FieldType);
                    if (convertedValue != null || !fieldInfo.FieldType.IsValueType || Nullable.GetUnderlyingType(fieldInfo.FieldType) != null)
                    {
                        fieldInfo.SetValue(instance, convertedValue);
                    }
                    return;
                }

                // Try property
                var propertyInfo = instanceType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    var convertedValue = ConvertJsonElementToTargetType(jsonValue, propertyInfo.PropertyType);
                    if (convertedValue != null || !propertyInfo.PropertyType.IsValueType || Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null)
                    {
                        propertyInfo.SetValue(instance, convertedValue);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TypeConversionUtils] Failed to set member '{memberName}' recursively: {ex.Message}");
            }
        }

        /// <summary>
        /// Get default value for a type
        /// </summary>
        private static object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }
    }
}
