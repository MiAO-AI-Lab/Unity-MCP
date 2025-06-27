using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Json;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Common.Json.Converters
{
    public class Matrix4x4Converter : JsonConverter<Matrix4x4>, IJsonSchemaConverter
    {
        public string Id => typeof(Matrix4x4).FullName;
        public JsonNode GetScheme() => new JsonObject
        {
            ["id"] = Id,
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["m00"] = new JsonObject { ["type"] = "number" },
                ["m01"] = new JsonObject { ["type"] = "number" },
                ["m02"] = new JsonObject { ["type"] = "number" },
                ["m03"] = new JsonObject { ["type"] = "number" },
                ["m10"] = new JsonObject { ["type"] = "number" },
                ["m11"] = new JsonObject { ["type"] = "number" },
                ["m12"] = new JsonObject { ["type"] = "number" },
                ["m13"] = new JsonObject { ["type"] = "number" },
                ["m20"] = new JsonObject { ["type"] = "number" },
                ["m21"] = new JsonObject { ["type"] = "number" },
                ["m22"] = new JsonObject { ["type"] = "number" },
                ["m23"] = new JsonObject { ["type"] = "number" },
                ["m30"] = new JsonObject { ["type"] = "number" },
                ["m31"] = new JsonObject { ["type"] = "number" },
                ["m32"] = new JsonObject { ["type"] = "number" },
                ["m33"] = new JsonObject { ["type"] = "number" }
            },
            ["required"] = new JsonArray
            {
                "m00", "m01", "m02", "m03",
                "m10", "m11", "m12", "m13",
                "m20", "m21", "m22", "m23",
                "m30", "m31", "m32", "m33"
            }
        };
        public JsonNode GetSchemeRef() => new JsonObject
        {
            ["$ref"] = Id
        };

        public override Matrix4x4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            float[] elements = new float[16];
            int index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Matrix4x4(
                        new Vector4(elements[0], elements[1], elements[2], elements[3]),
                        new Vector4(elements[4], elements[5], elements[6], elements[7]),
                        new Vector4(elements[8], elements[9], elements[10], elements[11]),
                        new Vector4(elements[12], elements[13], elements[14], elements[15])
                    );

                if (reader.TokenType == JsonTokenType.Number)
                {
                    elements[index++] = reader.GetSingle();
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Matrix4x4 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    writer.WriteNumber($"m{i}{j}", value[i, j]);
                }
            }
            writer.WriteEndObject();
        }
    }
}
