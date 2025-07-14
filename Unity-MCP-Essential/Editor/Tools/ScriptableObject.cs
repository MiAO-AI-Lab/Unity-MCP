#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using UnityEngine;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_ScriptableObject
    {
        public static class Error
        {
            public static string AssetPathIsEmpty()
                => "[Error] ScriptableObject asset path is empty.";
            
            public static string AssetNotFound(string path)
                => $"[Error] ScriptableObject at path '{path}' not found.";
            
            public static string TypeNotFound(string typeName)
                => $"[Error] Type '{typeName}' not found.";
            
            public static string TypeNotScriptableObject(string typeName)
                => $"[Error] Type '{typeName}' is not a ScriptableObject.";
        }
    }
}
