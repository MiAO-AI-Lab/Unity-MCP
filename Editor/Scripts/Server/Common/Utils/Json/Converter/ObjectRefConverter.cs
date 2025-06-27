#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Json;
using com.IvanMurzak.ReflectorNet.Model.Unity;

namespace com.IvanMurzak.Unity.MCP.Common.Json
{
    public class ObjectRefConverter : JsonConverter<ObjectRef>, IJsonSchemaConverter
    {
        public string Id => typeof(ObjectRef).FullName;
        public JsonNode GetScheme() => new JsonObject
        {
            ["id"] = Id,
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                [nameof(ObjectRef.instanceID)] = new JsonObject { ["type"] = "integer" },
                [nameof(ObjectRef.assetPath)] = new JsonObject { ["type"] = "string" },
                [nameof(ObjectRef.assetGuid)] = new JsonObject { ["type"] = "string" }
            },
            ["required"] = new JsonArray { "instanceID" }
        };
        public JsonNode GetSchemeRef() => new JsonObject
        {
            ["$ref"] = Id
        };

        public override ObjectRef? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var instanceID = new ObjectRef();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read(); // Move to the value token

                    switch (propertyName)
                    {
                        case nameof(ObjectRef.instanceID):
                            instanceID.instanceID = reader.GetInt32();
                            break;
                        case nameof(ObjectRef.assetPath):
                            instanceID.assetPath = reader.GetString();
                            break;
                        case nameof(ObjectRef.assetGuid):
                            instanceID.assetGuid = reader.GetString();
                            break;
                        default:
                            throw new JsonException($"Unexpected property name: {propertyName}. "
                                + $"Expected '{nameof(ObjectRef.instanceID)}', '{nameof(ObjectRef.assetPath)}', or '{nameof(ObjectRef.assetGuid)}'.");
                    }
                }
            }

            return instanceID;
        }

        public override void Write(Utf8JsonWriter writer, ObjectRef value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            // Write the "instanceID" property
            writer.WritePropertyName(nameof(ObjectRef.instanceID));
            writer.WriteNumberValue(value.instanceID);

            // Write the "assetPath" property
            if (!string.IsNullOrEmpty(value.assetPath))
            {
                writer.WritePropertyName(nameof(ObjectRef.assetPath));
                writer.WriteStringValue(value.assetPath);
            }

            // Write the "assetGuid" property
            if (!string.IsNullOrEmpty(value.assetGuid))
            {
                writer.WritePropertyName(nameof(ObjectRef.assetGuid));
                writer.WriteStringValue(value.assetGuid);
            }

            writer.WriteEndObject();
        }
    }
}