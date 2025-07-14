#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Linq;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Scene
    {
        public static string LoadedScenes
            => $"Loaded Scenes:\n{string.Join("\n", SceneUtils.GetAllLoadedScenes().Select(scene => scene.name))}";

        public static class Error
        {
            static string ScenesPrinted => string.Join("\n", SceneUtils.GetAllLoadedScenes().Select(scene => scene.name));

            public static string SceneNameIsEmpty()
                => $"[Error] Scene name is empty. Available scenes:\n{ScenesPrinted}";
            public static string NotFoundSceneWithName(string name)
                => $"[Error] Scene '{name}' not found. Available scenes:\n{ScenesPrinted}";
            public static string ScenePathIsEmpty()
                => "[Error] Scene path is empty. Please provide a valid path. Sample: \"Assets/Scenes/MyScene.unity\".";
            public static string FilePathMustEndsWithUnity()
                => "[Error] File path must end with '.unity'. Please provide a valid path. Sample: \"Assets/Scenes/MyScene.unity\".";
            public static string InvalidLoadSceneMode(int loadSceneMode)
                => $"[Error] Invalid load scene mode '{loadSceneMode}'. Valid values are 0 (Single) and 1 (Additive).";
        }
    }
}