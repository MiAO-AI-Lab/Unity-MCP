using System;
using System.IO;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP
{
#pragma warning disable CA2235 // Mark all non-serializable fields
    public partial class McpPluginUnity
    {
        public static string ResourcesFileName => "Unity-MCP-ConnectionConfig";
        public static string AssetsFilePath => $"Assets/Resources/{ResourcesFileName}.json";
#if UNITY_EDITOR
        public static TextAsset AssetFile => UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(AssetsFilePath);
        public static void InvalidateAssetFile() => UnityEditor.AssetDatabase.ImportAsset(AssetsFilePath, UnityEditor.ImportAssetOptions.ForceUpdate);
#endif

        public static McpPluginUnity GetOrCreateInstance()
        {
            try
            {
#if UNITY_EDITOR
                var json = Application.isPlaying
                    ? Resources.Load<TextAsset>(ResourcesFileName).text
                    : File.Exists(AssetsFilePath)
                        ? File.ReadAllText(AssetsFilePath)
                        : null;
#else
                var json = Resources.Load<TextAsset>(ResourcesFileName).text;
#endif
                Data config = null;
                try { config = JsonUtility.FromJson<Data>(json); }
                catch (Exception e)
                {
                    Debug.LogError($"[McpPluginUnity] <color=red><b>{ResourcesFileName}</b> file is corrupted at <i>{AssetsFilePath}</i></color>");
                    Debug.LogException(e);
                }
                if (config == null)
                {
                    Debug.Log($"[McpPluginUnity] <color=orange><b>Creating {ResourcesFileName}</b> file at <i>{AssetsFilePath}</i></color>");
                    config = new Data();
                }
                return new McpPluginUnity() { data = config };
            }
            catch (Exception e)
            {
                Debug.LogError($"[McpPluginUnity] <color=red><b>{ResourcesFileName}</b> file can't be loaded from <i>{AssetsFilePath}</i></color>");
                Debug.LogException(e);
            }
            return null;
        }

        public static void Save()
        {
#if UNITY_EDITOR
            Validate();
            try
            {
                var directory = Path.GetDirectoryName(AssetsFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var data = Instance.data ??= new Data();
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(AssetsFilePath, json);

                var assetFile = AssetFile;
                if (assetFile != null)
                    UnityEditor.EditorUtility.SetDirty(assetFile);
                else
                    UnityEditor.AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"[McpPluginUnity] <color=red><b>{ResourcesFileName}</b> file can't be saved at <i>{AssetsFilePath}</i></color>");
                Debug.LogException(e);
            }
#else
            return;
#endif
        }
    }
#pragma warning restore CA2235 // Mark all non-serializable fields
}