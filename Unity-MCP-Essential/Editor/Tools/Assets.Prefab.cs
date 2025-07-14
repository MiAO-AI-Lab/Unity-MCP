#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.MiAO.Unity.MCP.Common;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Assets_Prefab
    {
        public static class Error
        {
            static string PrefabsPrinted => string.Join("\n", AssetDatabase.FindAssets("t:Prefab"));

            public static string PrefabPathIsEmpty()
                => "[Error] Prefab path is empty. Available prefabs:\n" + PrefabsPrinted;

            public static string NotFoundPrefabAtPath(string path)
                => $"[Error] Prefab '{path}' not found. Available prefabs:\n" + PrefabsPrinted;

            public static string PrefabPathIsInvalid(string path)
                => $"[Error] Prefab path '{path}' is invalid.";

            public static string PrefabStageIsNotOpened()
                => "[Error] Prefab stage is not opened. Use 'Assets_Prefab_Open' to open it.";

            public static string PrefabStageIsAlreadyOpened()
                => "[Error] Prefab stage is already opened. Use 'Assets_Prefab_Close' to close it.";
        }
    }
}