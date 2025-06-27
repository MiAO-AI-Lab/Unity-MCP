#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if !UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.IvanMurzak.Unity.MCP.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    public static partial class GameObjectUtils
    {
        public static GameObject[] FindRootGameObjects(Scene? scene = null)
        {
            if (scene == null)
            {
                // Not supported in runtime build
                return null;
            }
            else
            {
                return scene.Value.GetRootGameObjects();
            }
        }
        public static GameObject FindByInstanceID(int instanceID)
        {
            if (instanceID == 0)
                return null;

            // Not supported in runtime build
            return null;
        }
    }
}
#endif