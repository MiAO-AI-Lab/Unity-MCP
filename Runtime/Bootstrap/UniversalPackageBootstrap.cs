using System;
using System.Reflection;
using UnityEngine;
using com.MiAO.MCP.Common;

namespace com.MiAO.MCP.Bootstrap
{
    /// <summary>
    /// Universal Package Bootstrap Framework - Provides common bootstrap functionality for all MCP extension packages
    /// Handles dependency detection, automatic Hub installation, and tool registration
    /// </summary>
    public static class UniversalPackageBootstrap
    {
        private const string HubPackageName = "com.miao.mcp";
        private const string HubClassName = "com.MiAO.MCP.McpPluginUnity";
        private const string HubAssemblyName = "com.MiAO.MCP.Runtime";

        /// <summary>
        /// Configuration for a package that uses the Universal Bootstrap Framework
        /// </summary>
        public class PackageConfig
        {
            public string PackageId { get; set; }
            public string DisplayName { get; set; }
            public Assembly Assembly { get; set; }
            public string[] RequiredPackages { get; set; }
            public Action OnHubAvailable { get; set; }
            public Action OnHubUnavailable { get; set; }
        }

        /// <summary>
        /// Bootstraps a package with the Universal Bootstrap Framework
        /// </summary>
        /// <param name="config">Package configuration</param>
        public static void Bootstrap(PackageConfig config)
        {
            try
            {
                // Check if Hub package is available
                if (IsHubPackageAvailable())
                {
                    config.OnHubAvailable?.Invoke();

                    // Register tools from the assembly
                    if (config.Assembly != null)
                    {
                        RegisterToolsFromAssembly(config.Assembly);
                    }
                }
                else
                {
                    Debug.LogWarning($"{Consts.Log.Tag} Hub package not found! Please ensure {HubPackageName} is installed.");
                    config.OnHubUnavailable?.Invoke();

                    // Optionally could trigger auto-installation here
                    // AutoInstallHubPackage();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error bootstrapping {config.DisplayName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a simple configuration for a package
        /// </summary>
        public static PackageConfig CreateSimpleConfig(string packageId, string displayName, Assembly assembly)
        {
            return new PackageConfig
            {
                PackageId = packageId,
                DisplayName = displayName,
                Assembly = assembly,
                RequiredPackages = new[] { HubPackageName },
                OnHubAvailable = () => Debug.Log($"{Consts.Log.Tag} {displayName} tools registered successfully"),
                OnHubUnavailable = () => Debug.LogWarning($"{Consts.Log.Tag} {displayName} requires MCP Hub to function")
            };
        }

        /// <summary>
        /// Checks if the Hub package is available
        /// </summary>
        private static bool IsHubPackageAvailable()
        {
            try
            {
                // Try to find the McpPluginUnity class from the Hub package
                var hubType = Type.GetType($"{HubClassName}, {HubAssemblyName}");
                return hubType != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Registers tools from an assembly using reflection
        /// </summary>
        private static void RegisterToolsFromAssembly(Assembly assembly)
        {
            try
            {
                McpPluginUnity.ToolInjector.RegisterToolPackage(assembly.GetName().Name, assembly);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error registering tools from assembly: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically installs the Hub package (placeholder for future implementation)
        /// </summary>
        private static void AutoInstallHubPackage()
        {
            Debug.LogWarning($"{Consts.Log.Tag} Auto-installation of Hub package not yet implemented");

            // Future implementation could use Unity Package Manager API:
            // UnityEditor.PackageManager.Client.Add(HubPackageName);
        }
    }
}