#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    public static partial class SceneUtils
    {
        public static Scene GetActiveScene()
            => SceneManager.GetActiveScene();
        public static IEnumerable<Scene> GetAllLoadedScenes()
        {
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    yield return scene;
            }
        }
        public static SceneMetadata ToMetadata(this Scene scene, int includeChildrenDepth = 3)
            => SceneMetadata.FromScene(scene, includeChildrenDepth);
    }
}