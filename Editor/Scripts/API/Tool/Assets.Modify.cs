#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Common;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets
    {
        [McpPluginTool
        (
            "Assets_Modify",
            Title = "Modify asset file"
        )]
        [Description(@"Modify asset in the project. Not allowed to modify asset in 'Packages/' folder. Please modify it in 'Assets/' folder.")]
        public string Modify
        (
            [Description("The asset content. It overrides the existing asset content.")]
            SerializedMember content,
            [Description("Path to the asset. See 'Assets_Search' for more details. Starts with 'Assets/'. Priority: 1. (Recommended)")]
            string? assetPath = null,
            [Description("GUID of the asset. Priority: 2.")]
            string? assetGuid = null
        )
        => MainThread.Instance.Run(() =>
        {
            if (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(assetGuid))
                return Error.NeitherProvided_AssetPath_AssetGuid();

            if (string.IsNullOrEmpty(assetPath))
                assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

            if (string.IsNullOrEmpty(assetGuid))
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

            if (assetPath.StartsWith("Packages/"))
                return Error.NotAllowedToModifyAssetInPackages(assetPath);

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                return Error.NotFoundAsset(assetPath, assetGuid);

            var obj = (object)asset;

            var result = Reflector.Instance.Populate(ref obj, content);

            // AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result.ToString();

            //             var instanceID = asset.GetInstanceID();
            //             return @$"[Success] Loaded asset.
            // # Asset path: {assetPath}
            // # Asset GUID: {assetGuid}
            // # Asset instanceID: {instanceID}";
        });
    }
}