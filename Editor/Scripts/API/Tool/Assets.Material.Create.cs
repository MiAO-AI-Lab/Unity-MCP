#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Common;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets
    {
        [McpPluginTool
        (
            "Assets_Material_Create",
            Title = "Create Material asset"
        )]
        [Description(@"Create new material asset with default parameters. Right 'shaderName' should be set. To find the shader, use 'Shader.Find' method.")]
        public string Create
        (
            [Description("Asset path. Starts with 'Assets/'. Ends with '.mat'.")]
            string assetPath,
            [Description("Name of the shader that need to be used to create the material.")]
            string shaderName
        )
        => MainThread.Instance.Run(() =>
        {
            if (string.IsNullOrEmpty(assetPath))
                return Error.EmptyAssetPath();

            if (!assetPath.StartsWith("Assets/"))
                return Error.AssetPathMustStartWithAssets(assetPath);

            if (!assetPath.EndsWith(".mat"))
                return Error.AssetPathMustEndWithMat(assetPath);

            var shader = UnityEngine.Shader.Find(shaderName);
            if (shader == null)
                return Error.ShaderNotFound(shaderName);

            var material = new UnityEngine.Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var result = Reflector.Instance.Serialize(
                material,
                name: material.name,
                logger: McpPlugin.Instance.Logger
            );
            return $"[Success] Material instanceID '{material.GetInstanceID()}' created at '{assetPath}'.\n{result}";
        });
    }
}