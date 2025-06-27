#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    public static partial class ShaderUtils
    {
        public static IEnumerable<Shader> GetAllShaders()
        {
            var shaderGuids = AssetDatabase.FindAssets("t:Shader");
            return shaderGuids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<Shader>(path))
                .Where(shader => shader != null);
        }
    }
}
#endif