using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityEngine;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Utils
{
    /// <summary>
    /// Custom reference equality comparer for object identity comparison
    /// </summary>
    internal class ObjectReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ObjectReferenceEqualityComparer Instance = new ObjectReferenceEqualityComparer();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return obj?.GetHashCode() ?? 0;
        }
    }

    /// <summary>
    /// Advanced object serialization utility with support for complex nested containers,
    /// Unity types, collections, and circular reference detection.
    /// </summary>
    public static class ObjectSerializationUtils
    {
        /// <summary>
        /// Serialization configuration
        /// </summary>
        public class SerializationConfig
        {
            public int MaxDepth { get; set; } = 20;
            public bool IncludePrivateFields { get; set; } = false;
            public bool IncludeProperties { get; set; } = true;
            public bool IncludeReadOnlyProperties { get; set; } = false;
            public bool IncludeUnityObjectReferences { get; set; } = true;
            public bool UseHumanReadableFormat { get; set; } = true;
            public bool DetectCircularReferences { get; set; } = true;
            public HashSet<string> IgnoredFields { get; set; } = new HashSet<string>();
            public HashSet<Type> IgnoredTypes { get; set; } = new HashSet<Type>();
            
            // GameObject-specific settings
            public bool IncludeGameObjectComponents { get; set; } = true;
            public bool IncludeTransformDetails { get; set; } = true;
            public bool IncludeDisabledComponents { get; set; } = true;
            public HashSet<string> IgnoredComponentTypes { get; set; } = new HashSet<string>();

            public static SerializationConfig Default => new SerializationConfig();
        }

        /// <summary>
        /// Serialization context for tracking depth and circular references
        /// </summary>
        private class SerializationContext
        {
            public int CurrentDepth { get; set; } = 0;
            public HashSet<object> VisitedObjects { get; set; } = new HashSet<object>(ObjectReferenceEqualityComparer.Instance);
            public SerializationConfig Config { get; set; }

            public SerializationContext(SerializationConfig config)
            {
                Config = config;
            }

            public bool ShouldSkipObject(object obj)
            {
                return obj == null || 
                       CurrentDepth >= Config.MaxDepth || 
                       (Config.DetectCircularReferences && VisitedObjects.Contains(obj));
            }

            public SerializationContext CreateChildContext()
            {
                return new SerializationContext(Config)
                {
                    CurrentDepth = CurrentDepth + 1,
                    VisitedObjects = new HashSet<object>(VisitedObjects, ObjectReferenceEqualityComparer.Instance)
                };
            }
        }

        /// <summary>
        /// Serialize any object to a JSON-compatible structure with advanced nested support
        /// </summary>
        /// <param name="obj">Object to serialize</param>
        /// <param name="config">Serialization configuration</param>
        /// <returns>JSON-compatible object structure</returns>
        public static object SerializeObject(object obj, SerializationConfig config = null)
        {
            config ??= SerializationConfig.Default;
            var context = new SerializationContext(config);
            return SerializeObjectRecursive(obj, context);
        }

        /// <summary>
        /// Serialize object to JSON string
        /// </summary>
        public static string SerializeToJson(object obj, SerializationConfig config = null)
        {
            var serializedObj = SerializeObject(obj, config);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(serializedObj, options);
        }

        /// <summary>
        /// Recursive serialization method
        /// </summary>
        private static object SerializeObjectRecursive(object obj, SerializationContext context)
        {
            // Handle null
            if (obj == null)
                return null;

            // Check depth and circular references
            if (context.ShouldSkipObject(obj))
            {
                if (context.CurrentDepth >= context.Config.MaxDepth)
                    return $"[MAX_DEPTH_REACHED: {context.Config.MaxDepth}]";
                if (context.Config.DetectCircularReferences)
                    return $"[CIRCULAR_REFERENCE: {obj.GetType().Name}]";
            }

            var objType = obj.GetType();

            // Handle primitive types and strings
            if (IsPrimitiveType(objType))
                return obj;

            // Handle enums
            if (objType.IsEnum)
                return new { 
                    __type = "enum",
                    typeName = objType.Name,
                    value = obj.ToString(),
                    numericValue = Convert.ToInt32(obj)
                };

            // Handle Unity primitive types
            if (IsUnityPrimitiveType(objType))
                return SerializeUnityPrimitive(obj);

            // Add to visited objects for circular reference detection
            if (context.Config.DetectCircularReferences && !IsPrimitiveType(objType))
                context.VisitedObjects.Add(obj);

            // Handle arrays
            if (objType.IsArray)
                return SerializeArray((Array)obj, context);

            // Handle generic collections (List<T>, Dictionary<K,V>, etc.)
            if (IsGenericCollection(objType))
                return SerializeCollection(obj, context);

            // Handle Unity Object references
            if (obj is UnityEngine.Object unityObj)
                return SerializeUnityObject(unityObj, context);

            // Handle custom objects and classes
            return SerializeCustomObject(obj, context);
        }

        /// <summary>
        /// Check if type is a primitive type that doesn't need further serialization
        /// </summary>
        private static bool IsPrimitiveType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(decimal) || 
                   type == typeof(DateTime) || 
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid);
        }

        /// <summary>
        /// Check if type is a Unity primitive type
        /// </summary>
        private static bool IsUnityPrimitiveType(Type type)
        {
            return type == typeof(Vector2) || 
                   type == typeof(Vector3) || 
                   type == typeof(Vector4) ||
                   type == typeof(Vector2Int) ||
                   type == typeof(Vector3Int) ||
                   type == typeof(Quaternion) ||
                   type == typeof(Color) ||
                   type == typeof(Color32) ||
                   type == typeof(Rect) ||
                   type == typeof(Bounds) ||
                   type == typeof(Matrix4x4) ||
                   type == typeof(LayerMask) ||
                   type == typeof(AnimationCurve);
        }

        /// <summary>
        /// Serialize Unity primitive types to readable format
        /// </summary>
        private static object SerializeUnityPrimitive(object obj)
        {
            return obj switch
            {
                Vector2 v2 => new { __type = "Vector2", x = v2.x, y = v2.y },
                Vector3 v3 => new { __type = "Vector3", x = v3.x, y = v3.y, z = v3.z },
                Vector4 v4 => new { __type = "Vector4", x = v4.x, y = v4.y, z = v4.z, w = v4.w },
                Vector2Int v2i => new { __type = "Vector2Int", x = v2i.x, y = v2i.y },
                Vector3Int v3i => new { __type = "Vector3Int", x = v3i.x, y = v3i.y, z = v3i.z },
                Quaternion q => new { __type = "Quaternion", x = q.x, y = q.y, z = q.z, w = q.w },
                Color c => new { __type = "Color", r = c.r, g = c.g, b = c.b, a = c.a },
                Color32 c32 => new { __type = "Color32", r = c32.r, g = c32.g, b = c32.b, a = c32.a },
                Rect r => new { __type = "Rect", x = r.x, y = r.y, width = r.width, height = r.height },
                Bounds b => new { __type = "Bounds", center = SerializeUnityPrimitive(b.center), size = SerializeUnityPrimitive(b.size) },
                LayerMask lm => new { __type = "LayerMask", value = lm.value },
                AnimationCurve ac => new { __type = "AnimationCurve", keys = ac.keys?.Select(k => new { time = k.time, value = k.value, inTangent = k.inTangent, outTangent = k.outTangent }).ToArray() },
                _ => obj.ToString()
            };
        }

        /// <summary>
        /// Check if type is a generic collection
        /// </summary>
        private static bool IsGenericCollection(Type type)
        {
            return type.IsGenericType && (
                typeof(IEnumerable).IsAssignableFrom(type) ||
                typeof(IDictionary).IsAssignableFrom(type));
        }

        /// <summary>
        /// Serialize arrays
        /// </summary>
        private static object SerializeArray(Array array, SerializationContext context)
        {
            var childContext = context.CreateChildContext();
            var result = new List<object>();

            for (int i = 0; i < array.Length; i++)
            {
                result.Add(SerializeObjectRecursive(array.GetValue(i), childContext));
            }

            return new
            {
                __type = "array",
                elementType = array.GetType().GetElementType()?.Name,
                length = array.Length,
                items = result
            };
        }

        /// <summary>
        /// Serialize generic collections (List<T>, Dictionary<K,V>, etc.)
        /// </summary>
        private static object SerializeCollection(object collection, SerializationContext context)
        {
            var collectionType = collection.GetType();
            var childContext = context.CreateChildContext();

            // Handle Dictionary<K,V>
            if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                return SerializeDictionary(collection, childContext);
            }

            // Handle IEnumerable (List<T>, etc.)
            if (collection is IEnumerable enumerable)
            {
                var items = new List<object>();
                foreach (var item in enumerable)
                {
                    items.Add(SerializeObjectRecursive(item, childContext));
                }

                var genericArgs = collectionType.IsGenericType ? collectionType.GetGenericArguments() : new Type[0];
                return new
                {
                    __type = "collection",
                    collectionType = collectionType.Name,
                    elementType = genericArgs.Length > 0 ? genericArgs[0].Name : "object",
                    count = items.Count,
                    items = items
                };
            }

            return collection.ToString();
        }

        /// <summary>
        /// Serialize Dictionary<K,V>
        /// </summary>
        private static object SerializeDictionary(object dictionary, SerializationContext context)
        {
            var dictType = dictionary.GetType();
            var keyType = dictType.GetGenericArguments()[0];
            var valueType = dictType.GetGenericArguments()[1];
            
            var entries = new List<object>();
            
            foreach (DictionaryEntry entry in (IDictionary)dictionary)
            {
                entries.Add(new
                {
                    key = SerializeObjectRecursive(entry.Key, context),
                    value = SerializeObjectRecursive(entry.Value, context)
                });
            }

            return new
            {
                __type = "dictionary",
                keyType = keyType.Name,
                valueType = valueType.Name,
                count = entries.Count,
                entries = entries
            };
        }

        /// <summary>
        /// Serialize Unity Object references
        /// </summary>
        private static object SerializeUnityObject(UnityEngine.Object unityObj, SerializationContext context)
        {
            if (unityObj == null)
                return null;

            var result = new Dictionary<string, object>
            {
                ["__type"] = "UnityObject",
                ["objectType"] = unityObj.GetType().Name,
                ["name"] = unityObj.name,
                ["instanceID"] = unityObj.GetInstanceID()
            };

            // Add asset path for assets
            var assetPath = AssetDatabase.GetAssetPath(unityObj);
            if (!string.IsNullOrEmpty(assetPath))
            {
                result["assetPath"] = assetPath;
                result["assetGUID"] = AssetDatabase.AssetPathToGUID(assetPath);
            }

            // Handle GameObject - serialize all components
            if (unityObj is GameObject go)
            {
                SerializeGameObject(go, result, context);
            }
            // Handle Components
            else if (unityObj is Component comp)
            {
                SerializeComponent(comp, result, context);
            }
            // Handle other Unity Objects (ScriptableObject, Assets, etc.)
            else
            {
                // If enabled, serialize the object's properties recursively
                if (context.Config.IncludeUnityObjectReferences && context.CurrentDepth < context.Config.MaxDepth - 1)
                {
                    var childContext = context.CreateChildContext();
                    result["properties"] = SerializeCustomObject(unityObj, childContext);
                }
            }

            return result;
        }

        /// <summary>
        /// Serialize GameObject with all its components
        /// </summary>
        private static void SerializeGameObject(GameObject go, Dictionary<string, object> result, SerializationContext context)
        {
            // Basic GameObject info
            result["hierarchyPath"] = GetGameObjectPath(go);
            result["tag"] = go.tag;
            result["layer"] = go.layer;
            result["layerName"] = LayerMask.LayerToName(go.layer);
            result["activeSelf"] = go.activeSelf;
            result["activeInHierarchy"] = go.activeInHierarchy;
            result["isStatic"] = go.isStatic;

            // Scene information
            result["sceneName"] = go.scene.name;
            result["sceneHandle"] = go.scene.handle;

            // Transform information (always included as it's fundamental)
            var transform = go.transform;
            if (context.Config.IncludeTransformDetails)
            {
                result["transform"] = SerializeTransform(transform, context);
            }

            // Serialize all components
            if (context.Config.IncludeGameObjectComponents && context.CurrentDepth < context.Config.MaxDepth - 1)
            {
                var components = go.GetComponents<Component>();
                var serializedComponents = new List<object>();

                foreach (var component in components)
                {
                    if (component == null) continue;

                    var componentType = component.GetType();
                    
                    // Skip ignored component types
                    if (context.Config.IgnoredComponentTypes.Contains(componentType.Name) ||
                        context.Config.IgnoredComponentTypes.Contains(componentType.FullName))
                        continue;

                    // Skip disabled components if configured
                    if (!context.Config.IncludeDisabledComponents && 
                        component is Behaviour behaviour && !behaviour.enabled)
                        continue;

                    // Don't serialize Transform twice (already handled above)
                    if (component is Transform)
                        continue;

                    var childContext = context.CreateChildContext();
                    var componentData = SerializeUnityObject(component, childContext);
                    serializedComponents.Add(componentData);
                }

                result["components"] = new
                {
                    __type = "componentArray",
                    count = serializedComponents.Count,
                    items = serializedComponents
                };
            }

            // Child GameObjects count
            result["childCount"] = go.transform.childCount;
            if (go.transform.childCount > 0 && context.CurrentDepth < context.Config.MaxDepth - 2)
            {
                var children = new List<object>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i).gameObject;
                    var childContext = context.CreateChildContext();
                    children.Add(SerializeUnityObject(child, childContext));
                }
                result["children"] = new
                {
                    __type = "gameObjectArray",
                    count = children.Count,
                    items = children
                };
            }
        }

        /// <summary>
        /// Serialize a Component with detailed information
        /// </summary>
        private static void SerializeComponent(Component comp, Dictionary<string, object> result, SerializationContext context)
        {
            result["gameObjectPath"] = GetGameObjectPath(comp.gameObject);
            result["gameObjectInstanceID"] = comp.gameObject.GetInstanceID();
            result["gameObjectName"] = comp.gameObject.name;

            // Always include basic component-specific information for Behaviour
            if (comp is Behaviour behaviour)
            {
                result["enabled"] = behaviour.enabled;
            }

            // Add Unity built-in component-specific quick info (this is additional, not exclusive)
            AddBuiltInComponentInfo(comp, result);

            // CRITICAL: Always serialize component properties - this is the key for custom components
            if (context.CurrentDepth < context.Config.MaxDepth - 1)
            {
                var childContext = context.CreateChildContext();
                
                if (context.Config.IncludeProperties)
                {
                    // Full serialization - include all properties and fields
                    result["properties"] = SerializeCustomObject(comp, childContext);
                }
                else
                {
                    // Brief mode - still serialize but with limited depth
                    var briefConfig = new SerializationConfig
                    {
                        MaxDepth = 3,
                        IncludeProperties = true,
                        IncludePrivateFields = false,
                        IncludeUnityObjectReferences = false,
                        DetectCircularReferences = true,
                        IgnoredFields = context.Config.IgnoredFields,
                        IgnoredTypes = context.Config.IgnoredTypes
                    };
                    var briefContext = new SerializationContext(briefConfig);
                    result["briefProperties"] = SerializeCustomObject(comp, briefContext);
                }
            }

            // Add component summary for easy identification
            result["componentSummary"] = $"{comp.GetType().Name} on {comp.gameObject.name}";
            result["componentType"] = comp.GetType().FullName;
        }

        /// <summary>
        /// Add built-in Unity component specific information
        /// </summary>
        private static void AddBuiltInComponentInfo(Component comp, Dictionary<string, object> result)
        {
            switch (comp)
            {
                // More specific types first to avoid unreachable code
                case MeshRenderer meshRenderer:
                    result["isVisible"] = meshRenderer.isVisible;
                    result["bounds"] = SerializeUnityPrimitive(meshRenderer.bounds);
                    if (meshRenderer.sharedMaterials != null)
                    {
                        result["materialCount"] = meshRenderer.sharedMaterials.Length;
                        result["materialNames"] = meshRenderer.sharedMaterials.Where(m => m != null).Select(m => m.name).ToArray();
                    }
                    break;

                case RectTransform rectTransform:
                    result["anchorMin"] = SerializeUnityPrimitive(rectTransform.anchorMin);
                    result["anchorMax"] = SerializeUnityPrimitive(rectTransform.anchorMax);
                    result["anchoredPosition"] = SerializeUnityPrimitive(rectTransform.anchoredPosition);
                    result["sizeDelta"] = SerializeUnityPrimitive(rectTransform.sizeDelta);
                    break;

                case MeshFilter meshFilter:
                    if (meshFilter.sharedMesh != null)
                    {
                        result["meshName"] = meshFilter.sharedMesh.name;
                        result["meshVertexCount"] = meshFilter.sharedMesh.vertexCount;
                        result["meshTriangleCount"] = meshFilter.sharedMesh.triangles.Length / 3;
                    }
                    break;

                case Light light:
                    result["lightType"] = light.type.ToString();
                    result["color"] = SerializeUnityPrimitive(light.color);
                    result["intensity"] = light.intensity;
                    result["range"] = light.range;
                    break;

                case Camera camera:
                    result["fieldOfView"] = camera.fieldOfView;
                    result["orthographic"] = camera.orthographic;
                    result["nearClipPlane"] = camera.nearClipPlane;
                    result["farClipPlane"] = camera.farClipPlane;
                    break;

                case AudioSource audioSource:
                    result["isPlaying"] = audioSource.isPlaying;
                    result["volume"] = audioSource.volume;
                    result["pitch"] = audioSource.pitch;
                    result["loop"] = audioSource.loop;
                    if (audioSource.clip != null)
                    {
                        result["clipName"] = audioSource.clip.name;
                    }
                    break;

                case Canvas canvas:
                    result["renderMode"] = canvas.renderMode.ToString();
                    result["sortingOrder"] = canvas.sortingOrder;
                    result["sortingLayerName"] = canvas.sortingLayerName;
                    break;

                case Rigidbody rb:
                    result["mass"] = rb.mass;
                    result["velocity"] = SerializeUnityPrimitive(rb.velocity);
                    result["isKinematic"] = rb.isKinematic;
                    result["useGravity"] = rb.useGravity;
                    break;

                case Collider collider:
                    result["isTrigger"] = collider.isTrigger;
                    result["bounds"] = SerializeUnityPrimitive(collider.bounds);
                    break;

                // General Renderer case (put after specific renderer types)
                case Renderer renderer:
                    result["isVisible"] = renderer.isVisible;
                    result["bounds"] = SerializeUnityPrimitive(renderer.bounds);
                    if (renderer.material != null)
                    {
                        result["materialName"] = renderer.material.name;
                        result["materialInstanceID"] = renderer.material.GetInstanceID();
                    }
                    break;
            }
        }

        /// <summary>
        /// Serialize Transform component with detailed information
        /// </summary>
        private static object SerializeTransform(Transform transform, SerializationContext context)
        {
            var result = new Dictionary<string, object>
            {
                ["__type"] = "UnityObject",
                ["objectType"] = "Transform",
                ["instanceID"] = transform.GetInstanceID(),
                ["position"] = SerializeUnityPrimitive(transform.position),
                ["localPosition"] = SerializeUnityPrimitive(transform.localPosition),
                ["rotation"] = SerializeUnityPrimitive(transform.rotation),
                ["localRotation"] = SerializeUnityPrimitive(transform.localRotation),
                ["localScale"] = SerializeUnityPrimitive(transform.localScale),
                ["eulerAngles"] = SerializeUnityPrimitive(transform.eulerAngles),
                ["localEulerAngles"] = SerializeUnityPrimitive(transform.localEulerAngles),
                ["right"] = SerializeUnityPrimitive(transform.right),
                ["up"] = SerializeUnityPrimitive(transform.up),
                ["forward"] = SerializeUnityPrimitive(transform.forward),
                ["childCount"] = transform.childCount,
                ["hierarchyDepth"] = GetTransformDepth(transform)
            };

            // Parent information
            if (transform.parent != null)
            {
                result["parentInstanceID"] = transform.parent.GetInstanceID();
                result["parentName"] = transform.parent.name;
                result["parentPath"] = GetGameObjectPath(transform.parent.gameObject);
            }

            return result;
        }

        /// <summary>
        /// Get the depth of a transform in the hierarchy
        /// </summary>
        private static int GetTransformDepth(Transform transform)
        {
            int depth = 0;
            var current = transform;
            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        /// <summary>
        /// Serialize custom objects and classes
        /// </summary>
        private static object SerializeCustomObject(object obj, SerializationContext context)
        {
            if (obj == null) return null;

            var objType = obj.GetType();
            var result = new Dictionary<string, object>
            {
                ["__type"] = "object",
                ["typeName"] = objType.Name,
                ["fullTypeName"] = objType.FullName
            };

            var childContext = context.CreateChildContext();

            // Serialize fields
            var fields = GetSerializableFields(objType, context.Config);
            foreach (var field in fields)
            {
                try
                {
                    var fieldValue = field.GetValue(obj);
                    result[field.Name] = SerializeObjectRecursive(fieldValue, childContext);
                }
                catch (Exception ex)
                {
                    result[field.Name] = $"[ERROR: {ex.Message}]";
                }
            }

            // Serialize properties
            if (context.Config.IncludeProperties)
            {
                var properties = GetSerializableProperties(objType, context.Config);
                foreach (var property in properties)
                {
                    try
                    {
                        var propertyValue = property.GetValue(obj);
                        result[property.Name] = SerializeObjectRecursive(propertyValue, childContext);
                    }
                    catch (Exception ex)
                    {
                        result[property.Name] = $"[ERROR: {ex.Message}]";
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get serializable fields for a type
        /// </summary>
        private static FieldInfo[] GetSerializableFields(Type type, SerializationConfig config)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
            if (config.IncludePrivateFields)
                bindingFlags |= BindingFlags.NonPublic;

            return type.GetFields(bindingFlags)
                .Where(f => !config.IgnoredFields.Contains(f.Name))
                .Where(f => !config.IgnoredTypes.Contains(f.FieldType))
                .Where(f => !f.IsNotSerialized)
                .ToArray();
        }

        /// <summary>
        /// Get serializable properties for a type
        /// </summary>
        private static PropertyInfo[] GetSerializableProperties(Type type, SerializationConfig config)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
            
            return type.GetProperties(bindingFlags)
                .Where(p => p.CanRead)
                .Where(p => config.IncludeReadOnlyProperties || p.CanWrite)
                .Where(p => !config.IgnoredFields.Contains(p.Name))
                .Where(p => !config.IgnoredTypes.Contains(p.PropertyType))
                .Where(p => p.GetIndexParameters().Length == 0) // Exclude indexers
                .ToArray();
        }

        /// <summary>
        /// Get GameObject hierarchy path
        /// </summary>
        private static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "";
            
            var path = go.name;
            var parent = go.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        /// <summary>
        /// Create a configuration optimized for ScriptableObject serialization
        /// </summary>
        public static SerializationConfig CreateScriptableObjectConfig()
        {
            var config = new SerializationConfig
            {
                MaxDepth = 25,
                IncludePrivateFields = false,
                IncludeProperties = true,
                IncludeReadOnlyProperties = false,
                IncludeUnityObjectReferences = true,
                UseHumanReadableFormat = true,
                DetectCircularReferences = true,
                IncludeGameObjectComponents = false, // Not relevant for ScriptableObjects
                IncludeTransformDetails = false,
                IncludeDisabledComponents = false
            };

            // Add commonly ignored Unity internal fields
            config.IgnoredFields.UnionWith(new[]
            {
                "m_CachedPtr",
                "m_InstanceID", 
                "m_UnityRuntimeErrorString",
                "m_ObjectHideFlags"
            });

            return config;
        }

        /// <summary>
        /// Create a configuration optimized for GameObject serialization
        /// </summary>
        public static SerializationConfig CreateGameObjectConfig()
        {
            var config = new SerializationConfig
            {
                MaxDepth = 15, // Reasonable depth for GameObject hierarchy
                IncludePrivateFields = true,
                IncludeProperties = true,
                IncludeReadOnlyProperties = true,
                IncludeUnityObjectReferences = true,
                UseHumanReadableFormat = true,
                DetectCircularReferences = true,
                IncludeGameObjectComponents = true,
                IncludeTransformDetails = true,
                IncludeDisabledComponents = true
            };

            // Add commonly ignored Unity internal fields
            config.IgnoredFields.UnionWith(new[]
            {
                "m_CachedPtr",
                "m_InstanceID", 
                "m_UnityRuntimeErrorString",
                "m_ObjectHideFlags",
                "runInEditMode",
                "useGUILayout"
            });

            // Commonly ignored component types for performance
            config.IgnoredComponentTypes.UnionWith(new[]
            {
                "ParticleSystemRenderer", // Usually too complex
                "CanvasRenderer", // UI internal
                "GraphicRaycaster" // UI internal
            });

            return config;
        }

        /// <summary>
        /// Create a brief configuration for GameObject serialization (components only, no deep properties)
        /// </summary>
        public static SerializationConfig CreateGameObjectBriefConfig()
        {
            var config = CreateGameObjectConfig();
            config.MaxDepth = 5; // Enough depth for components
            config.IncludeProperties = false; // Don't serialize deep component properties
            config.IncludeUnityObjectReferences = true; // Keep this true to serialize components
            config.IncludeTransformDetails = true; // Transform is important
            config.IncludeGameObjectComponents = true; // Most important: always include components
            return config;
        }

        /// <summary>
        /// Serialize ScriptableObject with optimized settings
        /// </summary>
        public static string SerializeScriptableObject(ScriptableObject scriptableObject)
        {
            var config = CreateScriptableObjectConfig();
            return SerializeToJson(scriptableObject, config);
        }

        /// <summary>
        /// Serialize GameObject with optimized settings
        /// </summary>
        public static string SerializeGameObject(GameObject gameObject, bool briefMode = false)
        {
            var config = briefMode ? CreateGameObjectBriefConfig() : CreateGameObjectConfig();
            return SerializeToJson(gameObject, config);
        }

        /// <summary>
        /// Test method to validate serialization with complex nested objects
        /// Can be called from Unity's console for testing
        /// </summary>
        [UnityEditor.MenuItem("Tools/MCP/Test Object Serialization")]
        public static void TestComplexSerialization()
        {
            try
            {
                Debug.Log("=== Testing Advanced Object Serialization ===");

                // Test ScriptableObject serialization
                var testAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/TestNestedData.asset");
                if (testAsset != null)
                {
                    Debug.Log("--- Testing ScriptableObject Serialization ---");
                    var serializedJson = SerializeScriptableObject(testAsset);
                    Debug.Log($"ScriptableObject serialization result:\n{serializedJson}");
                }

                // Test GameObject serialization
                TestGameObjectSerialization();
                
                if (testAsset == null)
                {
                    Debug.LogWarning("TestNestedData.asset not found. Creating test data...");
                    TestWithGeneratedData();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Serialization test failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Test GameObject serialization with various scenarios
        /// </summary>
        private static void TestGameObjectSerialization()
        {
            Debug.Log("--- Testing GameObject Serialization ---");

            // Find any GameObject in the scene
            var allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            if (allGameObjects.Length > 0)
            {
                // Test with the first GameObject that has components
                GameObject testGO = null;
                foreach (var go in allGameObjects)
                {
                    var components = go.GetComponents<Component>();
                    if (components.Length > 1) // More than just Transform
                    {
                        testGO = go;
                        break;
                    }
                }

                if (testGO != null)
                {
                    Debug.Log($"Testing GameObject serialization with: {testGO.name}");
                    
                    // Test full serialization
                    var fullJson = SerializeGameObject(testGO, false);
                    Debug.Log($"Full GameObject serialization:\n{fullJson}");

                    // Test brief serialization
                    var briefJson = SerializeGameObject(testGO, true);
                    Debug.Log($"Brief GameObject serialization:\n{briefJson}");
                }
                else
                {
                    Debug.LogWarning("No suitable GameObject found for testing (need GameObject with components)");
                }
            }
            else
            {
                Debug.LogWarning("No GameObjects found in scene for testing");
            }
        }

        /// <summary>
        /// Test with programmatically generated complex nested data
        /// </summary>
        private static void TestWithGeneratedData()
        {
            // Create test object similar to TestNestedScriptableObject structure
            var testData = new Dictionary<string, object>
            {
                ["testName"] = "Generated Test",
                ["testValue"] = 42,
                ["isActive"] = true,
                ["complexList"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = "Item 1",
                        ["position"] = new Vector3(1, 2, 3),
                        ["nestedData"] = new Dictionary<string, object>
                        {
                            ["description"] = "Nested Description",
                            ["color"] = Color.red,
                            ["deepList"] = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 1) }
                        }
                    }
                }
            };

            var serializedJson = SerializeToJson(testData);
            Debug.Log($"Generated test data serialization:\n{serializedJson}");
        }

        // /// <summary>
        // /// Validate serialization performance with different configurations
        // /// </summary>
        // [UnityEditor.MenuItem("Tools/MCP/Test Serialization Performance")]
        // public static void TestSerializationPerformance()
        // {
        //     var testAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/TestNestedData.asset");
        //     if (testAsset == null)
        //     {
        //         Debug.LogWarning("TestNestedData.asset not found for performance testing.");
        //         return;
        //     }

        //     var watch = System.Diagnostics.Stopwatch.StartNew();
            
        //     // Test different configurations
        //     var configs = new[]
        //     {
        //         new { Name = "Default", Config = CreateScriptableObjectConfig() },
        //         new { Name = "Brief", Config = new SerializationConfig { MaxDepth = 5, IncludeProperties = false, IncludeUnityObjectReferences = false } },
        //         new { Name = "Deep", Config = new SerializationConfig { MaxDepth = 50, IncludeProperties = true, IncludePrivateFields = true } },
        //         new { Name = "NoCircularCheck", Config = new SerializationConfig { DetectCircularReferences = false, MaxDepth = 15 } }
        //     };

        //     foreach (var configTest in configs)
        //     {
        //         watch.Restart();
        //         try
        //         {
        //             var result = SerializeToJson(testAsset, configTest.Config);
        //             watch.Stop();
        //             Debug.Log($"{configTest.Name} config: {watch.ElapsedMilliseconds}ms, {result.Length} characters");
        //         }
        //         catch (Exception ex)
        //         {
        //             watch.Stop();
        //             Debug.LogError($"{configTest.Name} config failed: {ex.Message}");
        //         }
        //     }
        // }
    }
}
