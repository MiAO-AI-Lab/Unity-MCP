#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.ReflectorNet.Model.Unity
{
    [System.Serializable]
    [Description("Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc.")]
    public class ObjectRef
    {
        [JsonInclude]
        [JsonPropertyName("instanceID")]
        public int instanceID;

        [JsonInclude]
        [JsonPropertyName("assetPath")]
        public string? assetPath;

        [JsonInclude]
        [JsonPropertyName("assetGuid")]
        public string? assetGuid;

        public ObjectRef() { }
        public ObjectRef(int id) => instanceID = id;
        public ObjectRef(string assetPath) => this.assetPath = assetPath;

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            if (instanceID != 0)
                stringBuilder.Append($"instanceID={instanceID}");

            if (!string.IsNullOrEmpty(assetPath))
            {
                if (stringBuilder.Length > 0)
                    stringBuilder.Append(", ");
                stringBuilder.Append($"assetPath={assetPath}");
            }

            if (!string.IsNullOrEmpty(assetGuid))
            {
                if (stringBuilder.Length > 0)
                    stringBuilder.Append(", ");
                stringBuilder.Append($"assetGuid={assetGuid}");
            }
            if (stringBuilder.Length == 0)
                return $"instanceID={instanceID}";

            return stringBuilder.ToString();
        }
    }
}