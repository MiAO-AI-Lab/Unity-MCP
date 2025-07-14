#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    using Consts = com.MiAO.Unity.MCP.Common.Consts;
    
    public partial class Tool_Assets
    {
        [McpPluginTool
        (
            "Assets_ManageFiles",
            Title = "Manage Asset Files"
        )]
        [Description(@"Manage asset file operations including:
- find: Search the asset database using search filter
- read: Read file asset content in the project
- copy: Copy assets at paths and store at new paths
- move: Move/rename assets at paths (includes renaming)
- delete: Delete assets at paths from the project
- createFolders: Create folders at specific locations
- refresh: Refresh the AssetDatabase")]
        public string FileOperations
        (
            [Description("Operation type: 'find', 'read', 'copy', 'move', 'delete', 'createFolders', 'refresh'")]
            string operation,
            [Description("For read: Asset path. Starts with 'Assets/'.")]
            string? assetPath = null,
            [Description("For read: Asset GUID.")]
            string? assetGuid = null,
            [Description("For find: Search filter. See documentation for details.")]
            string? filter = null,
            [Description("For find: Search folders. If null, search all folders.")]
            string[]? searchInFolders = null,
            [Description("For copy/move/delete/createFolders: Source paths array.")]
            string[]? sourcePaths = null,
            [Description("For copy/move: Destination paths array.")]
            string[]? destinationPaths = null,
            [Description("For createFolders: Folder paths array.")]
            string[]? folderPaths = null
        )
        {
            return operation.ToLower() switch
            {
                "find" => FindAssets(filter, searchInFolders),
                "read" => ReadAsset(assetPath, assetGuid),
                "copy" => CopyAssets(sourcePaths, destinationPaths),
                "move" => MoveAssets(sourcePaths, destinationPaths),
                "delete" => DeleteAssets(sourcePaths),
                "createfolders" => CreateFolders(folderPaths ?? sourcePaths),
                "refresh" => RefreshAssets(),
                _ => "[Error] Invalid operation. Valid operations: 'find', 'read', 'copy', 'move', 'delete', 'createFolders', 'refresh'"
            };
        }

        private string FindAssets(string? filter, string[]? searchInFolders)
        {
            return MainThread.Instance.Run(() =>
            {
                var assetGuids = (searchInFolders?.Length ?? 0) == 0
                    ? AssetDatabase.FindAssets(filter ?? string.Empty)
                    : AssetDatabase.FindAssets(filter ?? string.Empty, searchInFolders);

                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("instanceID | assetGuid                            | assetPath");
                stringBuilder.AppendLine("-----------+--------------------------------------+---------------------------------");

                for (var i = 0; i < assetGuids.Length; i++)
                {
                    if (i >= Consts.MCP.LinesLimit)
                    {
                        stringBuilder.AppendLine($"... and {assetGuids.Length - i} more assets. Use searchInFolders parameter to specify request.");
                        break;
                    }
                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                    var assetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    var instanceID = assetObject.GetInstanceID();
                    stringBuilder.AppendLine($"{instanceID,-10} | {assetGuids[i],-36} | {assetPath}");
                }

                return $"[Success] Assets found: {assetGuids.Length}.\n{stringBuilder.ToString()}";
            });
        }

        private string ReadAsset(string? assetPath, string? assetGuid)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(assetGuid))
                    return Error.NeitherProvided_AssetPath_AssetGuid();

                if (string.IsNullOrEmpty(assetPath))
                    assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

                if (string.IsNullOrEmpty(assetGuid))
                    assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                    return Error.NotFoundAsset(assetPath, assetGuid);

                var serialized = Reflector.Instance.Serialize(
                    asset,
                    name: asset.name,
                    recursive: true,
                    logger: McpPlugin.Instance.Logger
                );
                var json = JsonUtils.Serialize(serialized);

                return $"[Success] Loaded asset at path '{assetPath}'.\n{json}";
            });
        }

        private string CopyAssets(string[]? sourcePaths, string[]? destinationPaths)
        {
            return MainThread.Instance.Run(() =>
            {
                if (sourcePaths == null || sourcePaths.Length == 0)
                    return Error.SourcePathsArrayIsEmpty();

                if (destinationPaths == null || sourcePaths.Length != destinationPaths.Length)
                    return Error.SourceAndDestinationPathsArrayMustBeOfTheSameLength();

                var stringBuilder = new StringBuilder();

                for (var i = 0; i < sourcePaths.Length; i++)
                {
                    var sourcePath = sourcePaths[i];
                    var destinationPath = destinationPaths[i];

                    if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                    {
                        stringBuilder.AppendLine(Error.SourceOrDestinationPathIsEmpty());
                        continue;
                    }
                    if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    {
                        stringBuilder.AppendLine($"[Error] Failed to copy asset from {sourcePath} to {destinationPath}.");
                        continue;
                    }
                    stringBuilder.AppendLine($"[Success] Copied asset from {sourcePath} to {destinationPath}.");
                }
                AssetDatabase.Refresh();
                return stringBuilder.ToString();
            });
        }

        private string MoveAssets(string[]? sourcePaths, string[]? destinationPaths)
        {
            return MainThread.Instance.Run(() =>
            {
                if (sourcePaths == null || sourcePaths.Length == 0)
                    return Error.SourcePathsArrayIsEmpty();

                if (destinationPaths == null || sourcePaths.Length != destinationPaths.Length)
                    return Error.SourceAndDestinationPathsArrayMustBeOfTheSameLength();

                var stringBuilder = new StringBuilder();

                for (int i = 0; i < sourcePaths.Length; i++)
                {
                    var error = AssetDatabase.MoveAsset(sourcePaths[i], destinationPaths[i]);
                    if (string.IsNullOrEmpty(error))
                    {
                        stringBuilder.AppendLine($"[Success] Moved asset from {sourcePaths[i]} to {destinationPaths[i]}.");
                    }
                    else
                    {
                        stringBuilder.AppendLine($"[Error] Failed to move asset from {sourcePaths[i]} to {destinationPaths[i]}: {error}.");
                    }
                }
                AssetDatabase.Refresh();
                return stringBuilder.ToString();
            });
        }

        private string DeleteAssets(string[]? paths)
        {
            return MainThread.Instance.Run(() =>
            {
                if (paths == null || paths.Length == 0)
                    return Error.SourcePathsArrayIsEmpty();

                var outFailedPaths = new List<string>();
                var success = AssetDatabase.DeleteAssets(paths, outFailedPaths);
                if (!success)
                {
                    var stringBuilder = new StringBuilder();
                    foreach (var failedPath in outFailedPaths)
                        stringBuilder.AppendLine($"[Error] Failed to delete asset at {failedPath}.");
                    return stringBuilder.ToString();
                }

                AssetDatabase.Refresh();
                return "[Success] Deleted assets at paths:\n" + string.Join("\n", paths);
            });
        }

        private string CreateFolders(string[]? paths)
        {
            return MainThread.Instance.Run(() =>
            {
                if (paths == null || paths.Length == 0)
                    return Error.SourcePathsArrayIsEmpty();

                var stringBuilder = new StringBuilder();

                for (var i = 0; i < paths.Length; i++)
                {
                    if (string.IsNullOrEmpty(paths[i]))
                    {
                        stringBuilder.AppendLine(Error.SourcePathIsEmpty());
                        continue;
                    }
                    try
                    {
                        Directory.CreateDirectory(paths[i]);
                        stringBuilder.AppendLine($"[Success] Created folder at {paths[i]}.");
                    }
                    catch (Exception e)
                    {
                        stringBuilder.AppendLine($"[Error] Failed to create folder at {paths[i]}: {e.Message}");
                    }
                }

                AssetDatabase.Refresh();
                return stringBuilder.ToString();
            });
        }

        private string RefreshAssets()
        {
            return MainThread.Instance.Run(() =>
            {
                AssetDatabase.Refresh();
                return @$"[Success] AssetDatabase refreshed. {AssetDatabase.GetAllAssetPaths().Length} assets found. Use find operation for more details.";
            });
        }
    }
}
