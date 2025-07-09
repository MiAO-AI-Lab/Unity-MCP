#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEditor;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_Interact
    {
        [McpPluginTool(
            "Interact_ManageUserInput",
            Title = "Manage User Input"
        )]
        [Description("Manage creation, result retrieval, and closing operations of user input windows.")]
        public string ManageUserInput(
            [Description("Operation type: 'ask' (create window)")]
            string operation,
            [Description("Prompt message displayed to user (only for ask operation)")]
            string promptMessage = "",
            [Description("Unique identifier for window, used to manage multiple windows")]
            string windowId = "default"
        )
        {
            switch (operation.ToLower())
            {
                case "ask":
                    return AskUserInputWithTimeout(promptMessage, windowId);
                default:
                    return $"[Error] Invalid operation type '{operation}'. Supported operation types: 'ask', 'get', 'close'.";
            }
        }

        private string AskUserInputWithTimeout(string promptMessage, string windowId)
        {
            try
            {
                Task<string> task = null;
                
                MainThread.Instance.Run(() =>
                {
                    task = AskUserInputAsync(promptMessage, windowId);
                });
                
                if (task.Wait(TimeSpan.FromMinutes(5)))
                {
                    return task.Result;
                }
                else
                {
                    // Timeout handling
                    MainThread.Instance.Run(() =>
                    {
                        // Find and close the user input in main window
                        var window = EditorWindow.GetWindow<MainWindowEditor>();
                        if (window != null)
                        {
                            window.HideUserInputUI();
                        }
                        
                        if (_activeWindows.ContainsKey(windowId))
                        {
                            _activeWindows.Remove(windowId);
                        }
                    });
                    return "[Timeout] User input timeout (5 minutes)";
                }
            }
            catch (Exception ex)
            {
                return $"[Error] Error occurred while processing user input: {ex.Message}";
            }
        }

        private async Task<string> AskUserInputAsync(string promptMessage, string windowId)
        {
            if (string.IsNullOrEmpty(promptMessage))
            {
                return Error.PromptMessageIsEmpty();
            }

            // If there's already a window with the same ID, close it first
            if (_activeWindows.ContainsKey(windowId))
            {
                var window = EditorWindow.GetWindow<MainWindowEditor>();
                if (window != null)
                {
                    window.HideUserInputUI();
                }
                _activeWindows.Remove(windowId);
            }

            // Create TaskCompletionSource to wait for user input
            var tcs = new TaskCompletionSource<string>();
            
            // Store the completion source
            _activeWindows[windowId] = new UserInputSession { 
                WindowId = windowId, 
                CompletionSource = tcs 
            };
            
            // Show user input UI in main window
            var mainWindow = EditorWindow.GetWindow<MainWindowEditor>();
            if (mainWindow != null)
            {
                mainWindow.ShowUserInputUI(promptMessage, windowId, result => 
                {
                    tcs.SetResult(result);
                });
            }
            else
            {
                tcs.SetResult("[Error] Main window not found.");
            }
            
            // Asynchronously wait for user operation completion
            return await tcs.Task;
        }
    }
} 