#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using com.MiAO.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.MCP.Editor.API
{
    [McpPluginToolType]
    public partial class Tool_Interact
    {
        private static Dictionary<string, UserInputSession> _activeWindows = new Dictionary<string, UserInputSession>();

        public static class Error
        {
            public static string WindowIdIsEmpty()
                => "[Error] Window ID cannot be empty.";

            public static string PromptMessageIsEmpty()
                => "[Error] Prompt message cannot be empty.";

            public static string WindowNotFound(string windowId)
                => $"[Error] No user input window found with ID '{windowId}'.";
        }
    }

    // Simplified structure for tracking user input sessions
    public class UserInputSession
    {
        public string WindowId { get; set; }
        public TaskCompletionSource<string> CompletionSource { get; set; }
    }


}
