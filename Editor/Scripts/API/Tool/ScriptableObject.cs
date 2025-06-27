#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using com.IvanMurzak.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet;
using UnityEngine;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
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
