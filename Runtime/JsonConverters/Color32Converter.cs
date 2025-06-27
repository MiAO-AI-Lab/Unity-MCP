using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Json;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Common.Json.Converters
{
    public class Color32Converter : JsonConverter<Color32>, IJsonSchemaConverter
    {
        public string Id => typeof(Color32).FullName;
        public JsonNode GetScheme() => new JsonObject
        {
            ["id"] = Id,
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["r"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["minimum"] = 0,
                    ["maximum"] = 255
                },
                ["g"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["minimum"] = 0,
                    ["maximum"] = 255
                },
                ["b"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["minimum"] = 0,
                    ["maximum"] = 255
                },
                ["a"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["minimum"] = 0,
                    ["maximum"] = 255
                }
            },
            ["required"] = new JsonArray { "r", "g", "b", "a" }
        };
        public JsonNode GetSchemeRef() => new JsonObject
        {
            ["$ref"] = Id
        };

        public override Color32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            byte r = 0, g = 0, b = 0, a = 255;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Color32(r, g, b, a);

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "r":
                            r = reader.GetByte();
                            break;
                        case "g":
                            g = reader.GetByte();
                            break;
                        case "b":
                            b = reader.GetByte();
                            break;
                        case "a":
                            a = reader.GetByte();
                            break;
                        default:
                            throw new JsonException($"Unexpected property name: {propertyName}. "
                                + $"Expected 'r', 'g', 'b', or 'a'.");
                    }
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Color32 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("r", value.r);
            writer.WriteNumber("g", value.g);
            writer.WriteNumber("b", value.b);
            writer.WriteNumber("a", value.a);
            writer.WriteEndObject();
        }
    }
}
