#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Reflection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.Unity.MCP.Common.Reflection.Convertor;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.IvanMurzak.Unity.MCP.Reflection.Convertor
{
    public partial class RS_GenericUnityNoProperties<T> : RS_GenericUnity<T>
    {
        public override IEnumerable<PropertyInfo>? GetSerializableProperties(Reflector reflector, Type objType, BindingFlags flags, ILogger? logger = null)
            => null;
    }
}