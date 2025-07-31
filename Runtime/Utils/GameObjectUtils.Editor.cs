#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if UNITY_EDITOR
using com.MiAO.MCP.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace com.MiAO.MCP.Utils
{
    public static partial class GameObjectUtils
    {
        /// <summary>
        /// Find Root GameObject in opened Prefab. Of array of GameObjects in a scene.
        /// </summary>
        /// <param name="scene">Scene for the search, if null the current active scene would be used</param>
        /// <returns>Array of root GameObjects</returns>
        public static GameObject[] FindRootGameObjects(Scene? scene = null)
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                return prefabStage.prefabContentsRoot.MakeArray();

            if (scene == null)
            {
                var rootGos = UnityEditor.SceneManagement.EditorSceneManager
                    .GetActiveScene()
                    .GetRootGameObjects();

                return rootGos;
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

            var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceID);
            if (obj is not GameObject go)
                return null;

            return go;
        }

        /// <summary>
        /// Get detailed information about a missing component/script
        /// </summary>
        /// <param name="gameObject">The GameObject containing the missing component</param>
        /// <param name="componentIndex">Index of the missing component</param>
        /// <returns>Detailed information about the missing component</returns>
        public static string GetMissingComponentInfo(GameObject gameObject, int componentIndex)
        {
            try
            {
                var serializedObject = new SerializedObject(gameObject);
                var serializedProperty = serializedObject.FindProperty("m_Component");
                
                if (serializedProperty != null && componentIndex < serializedProperty.arraySize)
                {
                    var componentProperty = serializedProperty.GetArrayElementAtIndex(componentIndex);
                    var componentRef = componentProperty.FindPropertyRelative("component");
                    
                    if (componentRef != null)
                    {
                        var fileID = componentRef.FindPropertyRelative("m_FileID");
                        var pathID = componentRef.FindPropertyRelative("m_PathID");
                        
                        // Try to get script reference information
                        var scriptProperty = componentRef.FindPropertyRelative("m_Script");
                        if (scriptProperty != null)
                        {
                            var scriptFileID = scriptProperty.FindPropertyRelative("m_FileID");
                            var scriptPathID = scriptProperty.FindPropertyRelative("m_PathID");
                            
                            return $"Component index {componentIndex}, FileID: {fileID?.longValue ?? 0}, PathID: {pathID?.longValue ?? 0}, Script FileID: {scriptFileID?.longValue ?? 0}, Script PathID: {scriptPathID?.longValue ?? 0}";
                        }
                        
                        return $"Component index {componentIndex}, FileID: {fileID?.longValue ?? 0}, PathID: {pathID?.longValue ?? 0}";
                    }
                }
                
                return $"Component index {componentIndex} - Unable to retrieve detailed information";
            }
            catch (System.Exception ex)
            {
                return $"Component index {componentIndex} - Error getting info: {ex.Message}";
            }
        }
    }
}
#endif