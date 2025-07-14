#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_Scene
    {
        [McpPluginTool
        (
            "Scene_Manage",
            Title = "Manage Scenes - Load, Unload, Save, Create scenes"
        )]
        [Description(@"Manage comprehensive scene operations including:
- load: Load scene from project assets
- unload: Unload currently loaded scene
- save: Save current or specified scene
- create: Create new scene in project assets")]
        public async Task<string> Management
        (
            [Description("Operation type: 'load', 'unload', 'save', 'create'")]
            string operation,
            [Description("Path to the scene file (required for load, save, create operations). Sample: 'Assets/Scenes/MyScene.unity'")]
            string? path = null,
            [Description("Scene name for unload operation or target scene name for save operation when multiple scenes are loaded")]
            string? sceneName = null,
            [Description("Load scene mode for load operation. 0 - Single, 1 - Additive")]
            int loadSceneMode = 0
        )
        {
            return operation.ToLower() switch
            {
                "load" => await LoadScene(path, loadSceneMode),
                "unload" => await UnloadScene(sceneName),
                "save" => SaveScene(path, sceneName),
                "create" => CreateScene(path),
                _ => "[Error] Invalid operation. Valid operations: 'load', 'unload', 'save', 'create'"
            };
        }

        private async Task<string> LoadScene(string? path, int loadSceneMode)
        {
            return await MainThread.Instance.Run(async () =>
            {
                if (string.IsNullOrEmpty(path))
                    return Error.ScenePathIsEmpty();

                if (path.EndsWith(".unity") == false)
                    return Error.FilePathMustEndsWithUnity();

                if (loadSceneMode < 0 || loadSceneMode > 1)
                    return Error.InvalidLoadSceneMode(loadSceneMode);

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    path,
                    loadSceneMode switch
                    {
                        0 => UnityEditor.SceneManagement.OpenSceneMode.Single,
                        1 => UnityEditor.SceneManagement.OpenSceneMode.Additive,
                        _ => throw new System.ArgumentOutOfRangeException(nameof(loadSceneMode), "Invalid open scene mode.")
                    });

                if (!scene.IsValid())
                    return $"[Error] Failed to load scene at '{path}'.\n{LoadedScenes}";

                return $"[Success] Scene loaded at '{path}'.\n{LoadedScenes}";
            });
        }

        private async Task<string> UnloadScene(string? sceneName)
        {
            return await MainThread.Instance.Run(async () =>
            {
                if (string.IsNullOrEmpty(sceneName))
                    return Error.SceneNameIsEmpty();

                var scene = SceneUtils.GetAllLoadedScenes()
                    .FirstOrDefault(scene => scene.name == sceneName);

                if (!scene.IsValid())
                    return Error.NotFoundSceneWithName(sceneName);

                var asyncOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);

                while (!asyncOperation.isDone)
                    await Task.Yield();

                return $"[Success] Scene '{sceneName}' unloaded.\n{LoadedScenes}";
            });
        }

        private string SaveScene(string? path, string? targetSceneName)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(path))
                    return Error.ScenePathIsEmpty();

                if (path.EndsWith(".unity") == false)
                    return Error.FilePathMustEndsWithUnity();

                var scene = string.IsNullOrEmpty(targetSceneName)
                    ? SceneUtils.GetActiveScene()
                    : SceneUtils.GetAllLoadedScenes()
                        .FirstOrDefault(scene => scene.name == targetSceneName);

                if (!scene.IsValid())
                    return Error.NotFoundSceneWithName(targetSceneName);

                bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
                if (!saved)
                    return $"[Error] Failed to save scene at '{path}'.\n{LoadedScenes}";

                return $"[Success] Scene saved at '{path}'.\n{LoadedScenes}";
            });
        }

        private string CreateScene(string? path)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(path))
                    return Error.ScenePathIsEmpty();

                if (path.EndsWith(".unity") == false)
                    return Error.FilePathMustEndsWithUnity();

                // Create a new empty scene
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                    UnityEditor.SceneManagement.NewSceneMode.Single);

                // Save the scene asset at the specified path
                bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
                if (!saved)
                    return $"[Error] Failed to save scene at '{path}'.\n{LoadedScenes}";

                return $"[Success] Scene created at '{path}'.\n{LoadedScenes}";
            });
        }
    }
} 