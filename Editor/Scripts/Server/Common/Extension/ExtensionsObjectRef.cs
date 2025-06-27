#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Text.Json;
using com.IvanMurzak.ReflectorNet.Model.Unity;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    public static class ExtensionsObjectRef
    {
        public static ObjectRef? ToObjectRef(this JsonElement? jsonElement)
        {
            if (jsonElement == null)
                return null;

            try
            {
                return jsonElement.HasValue
                    ? JsonSerializer.Deserialize<ObjectRef>(jsonElement.Value)
                    : null;
            }
            // catch (JsonException ex)
            // {
            //     if (McpPluginUnity.LogLevel.IsActive(LogLevel.Error))
            //         UnityEngine.Debug.LogError($"Failed to deserialize ObjectRef: {ex.Message}");
            //     return null;
            // }
            catch
            {
                return null;
            }
        }
    }
}