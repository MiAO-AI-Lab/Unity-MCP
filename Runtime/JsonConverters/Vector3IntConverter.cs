using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Json;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Common.Json.Converters
{
    public class Vector3IntConverter : JsonConverter<Vector3Int>, IJsonSchemaConverter
    {
        public string Id => typeof(Vector3Int).FullName;
        public JsonNode GetScheme() => new JsonObject
        {
            ["id"] = Id,
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["x"] = new JsonObject { ["type"] = "number" },
                ["y"] = new JsonObject { ["type"] = "number" },
                ["z"] = new JsonObject { ["type"] = "number" }
            },
            ["required"] = new JsonArray { "x", "y", "z" }
        };
        public JsonNode GetSchemeRef() => new JsonObject
        {
            ["$ref"] = Id
        };

        public override Vector3Int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            int x = 0, y = 0, z = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Vector3Int(x, y, z);

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "x":
                            x = reader.GetInt32();
                            break;
                        case "y":
                            y = reader.GetInt32();
                            break;
                        case "z":
                            z = reader.GetInt32();
                            break;
                        default:
                            throw new JsonException($"Unexpected property name: {propertyName}. "
                                + "Expected 'x', 'y', or 'z'.");
                    }
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Vector3Int value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.x);
            writer.WriteNumber("y", value.y);
            writer.WriteNumber("z", value.z);
            writer.WriteEndObject();
        }
    }
}
