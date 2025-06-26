#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using com.IvanMurzak.ReflectorNet.Model;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public static class ExtensionsNotificationData
    {
        public static IRequestNotification SetName(this IRequestNotification data, string name)
        {
            data.Name = name;
            return data;
        }
        public static IRequestNotification SetOrAddParameter(this IRequestNotification data, string name, object? value)
        {
            data.Parameters ??= new Dictionary<string, object?>();
            data.Parameters[name] = value;
            return data;
        }
        // public static IRequestCallTool Build(this IRequestNotification data)
        //     => new RequestData(data as RequestNotification ?? throw new System.InvalidOperationException("NotificationData is null"));
    }
}