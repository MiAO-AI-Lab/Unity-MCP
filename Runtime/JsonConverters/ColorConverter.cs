using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Json;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Common.Json.Converters
{
    public class ColorConverter : JsonConverter<Color>, IJsonSchemaConverter
    {
        public string Id => typeof(Color).FullName;
        public JsonNode GetScheme() => new JsonObject
        {
            ["id"] = Id,
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["r"] = new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                },
                ["g"] = new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                },
                ["b"] = new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                },
                ["a"] = new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                }
            },
            ["required"] = new JsonArray { "r", "g", "b", "a" }
        };
        public JsonNode GetSchemeRef() => new JsonObject
        {
            ["$ref"] = Id
        };

        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            float r = 0, g = 0, b = 0, a = 1;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Color(r, g, b, a);

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "r":
                            r = reader.GetSingle();
                            break;
                        case "g":
                            g = reader.GetSingle();
                            break;
                        case "b":
                            b = reader.GetSingle();
                            break;
                        case "a":
                            a = reader.GetSingle();
                            break;
                        default:
                            throw new JsonException($"Unexpected property name: {propertyName}. "
                                + $"Expected 'r', 'g', 'b', or 'a'.");
                    }
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
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
