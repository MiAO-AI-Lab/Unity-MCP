#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if !UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    public static partial class ShaderUtils
    {
        public static IEnumerable<Shader> GetAllShaders()
            => Enumerable.Empty<Shader>();
    }
}
#endif