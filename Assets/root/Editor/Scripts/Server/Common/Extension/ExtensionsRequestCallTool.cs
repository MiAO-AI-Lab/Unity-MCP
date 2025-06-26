#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using System.Text.Json;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public static class ExtensionsRequestCallTool
    {
        public static IRequestCallTool SetName(this IRequestCallTool data, string name)
        {
            data.Name = name;
            return data;
        }
        public static IRequestCallTool SetOrAddParameter(this IRequestCallTool data, string name, object? value)
        {
            data.Arguments ??= value == null
                ? new Dictionary<string, JsonElement>()
                : new Dictionary<string, JsonElement>() { [name] = JsonUtils.SerializeToElement(value) };
            return data;
        }
        // public static IRequestData BuildRequest(this IRequestTool data)
        //     => new RequestData(data as RequestTool ?? throw new System.InvalidOperationException("CommandData is null"));
    }
}