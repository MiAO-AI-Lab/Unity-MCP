#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    public static partial class SceneUtils
    {
        public static IEnumerable<Scene> GetAllLoadedScenesInUnityEditor()
        {
            var sceneCount = UnityEditor.SceneManagement.EditorSceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    yield return scene;
            }
        }
    }
}
#endif