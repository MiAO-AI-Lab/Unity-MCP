using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using com.MiAO.Unity.MCP;
using com.MiAO.Unity.MCP.Bootstrap;
using com.MiAO.Unity.MCP.ToolInjection;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Essential.Bootstrap
{
    /// <summary>
    /// Essential Tools Bootstrap - Automatically initializes and registers essential tools
    /// This class is automatically called when the Essential Tools package is loaded
    /// </summary>
    [InitializeOnLoad]
    public static class EssentialToolsBootstrap
    {
        private const string PackageName = "com.miao.unity.mcp.essential";
        private const string HubPackageName = "com.miao.unity.mcp";
        
        static EssentialToolsBootstrap()
        {
            // Initialize in next frame to ensure all assemblies are loaded
            EditorApplication.delayCall += Initialize;
        }
        
        private static void Initialize()
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} [EssentialTools] Initializing Essential Tools Bootstrap...");
                
                // Check if Hub package is available
                if (!IsHubPackageAvailable())
                {
                    Debug.LogError($"{Consts.Log.Tag} [EssentialTools] Unity MCP Hub package not found! Please ensure {HubPackageName} is installed.");
                    return;
                }
                
                // Register this tool package
                RegisterToolPackage();
                
                Debug.Log($"{Consts.Log.Tag} [EssentialTools] Essential Tools Bootstrap completed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} [EssentialTools] Failed to initialize Essential Tools Bootstrap: {ex.Message}");
            }
        }
        
        private static bool IsHubPackageAvailable()
        {
            try
            {
                // Try to find the McpPluginUnity class from the Hub package
                var hubType = Type.GetType("com.MiAO.Unity.MCP.McpPluginUnity, com.MiAO.Unity.MCP.Runtime");
                return hubType != null;
            }
            catch
            {
                return false;
            }
        }
        
        private static void RegisterToolPackage()
        {
            try
            {
                // Get the current assembly
                var currentAssembly = Assembly.GetExecutingAssembly();
                
                // Register with the Hub's tool injection system
                var toolCount = McpPluginUnity.ToolInjector.RegisterToolPackage(PackageName, currentAssembly);
                
                Debug.Log($"{Consts.Log.Tag} [EssentialTools] Registered {toolCount} tools from Essential Tools package");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} [EssentialTools] Failed to register tool package: {ex.Message}");
            }
        }
    }
} 