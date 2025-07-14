#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Linq;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    [McpPluginToolType]
    public partial class Tool_Editor_Selection
    {
        public static string SelectionPrint => @$"Editor Selection:
Selection.gameObjects: {Selection.gameObjects?.Select(go => go.GetInstanceID()).JoinString(", ")}
Selection.transforms: {Selection.transforms?.Select(t => t.GetInstanceID()).JoinString(", ")}
Selection.instanceIDs: {Selection.instanceIDs?.JoinString(", ")}
Selection.assetGUIDs: {Selection.assetGUIDs?.JoinString(", ")}
Selection.activeGameObject: {Selection.activeGameObject?.GetInstanceID()}
Selection.activeInstanceID: {Selection.activeInstanceID}
Selection.activeObject: {Selection.activeObject?.GetInstanceID()}
Selection.activeTransform: {Selection.activeTransform?.GetInstanceID()}";

        public static class Error
        {
            public static string ScriptPathIsEmpty()
                => "[Error] Script path is empty. Please provide a valid path. Sample: \"Assets/Scripts/MyScript.cs\".";
        }

        [McpPluginTool
        (
            "Editor_ManageSelection",
            Title = "Manage Unity Editor Selection"
        )]
        [Description(@"Manage Unity Editor selection operations:
- get: Retrieve current selection information (selected Assets or GameObjects)
- set: Configure selection in Unity Editor (Assets or GameObjects)")]
        public string SelectionManagement
        (
            [Description("Operation type: 'get', 'set'")]
            string operation,
            [Description("For set: The 'instanceID' array of the target GameObjects.")]
            int[]? instanceIDs = null,
            [Description("For set: The 'instanceID' of the target Object.")]
            int? activeInstanceID = null
        )
        {
            return operation.ToLower() switch
            {
                "get" => GetSelection(),
                "set" => SetSelection(instanceIDs, activeInstanceID),
                _ => "[Error] Invalid operation. Valid operations: 'get', 'set'"
            };
        }

        private string GetSelection()
        {
            return MainThread.Instance.Run(() => "[Success] " + SelectionPrint);
        }

        private string SetSelection(int[]? instanceIDs, int? activeInstanceID)
        {
            return MainThread.Instance.Run(() =>
            {
                UnityEditor.Selection.instanceIDs = instanceIDs ?? new int[0];
                if (activeInstanceID.HasValue)
                    UnityEditor.Selection.activeInstanceID = activeInstanceID.Value;

                return "[Success] " + SelectionPrint;
            });
        }
    }
}