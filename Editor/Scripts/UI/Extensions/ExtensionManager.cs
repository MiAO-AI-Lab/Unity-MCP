#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Editor.Extensions
{
    /// <summary>
    /// Manages MCP extension packages - installation, removal, updates, and discovery
    /// Integrates with Unity Package Manager for actual package operations
    /// </summary>
    public static class ExtensionManager
    {
        private static readonly Dictionary<string, ExtensionPackageInfo> s_KnownExtensions = 
            new Dictionary<string, ExtensionPackageInfo>();
        
        private static readonly Dictionary<string, UnityEditor.PackageManager.PackageInfo> s_InstalledPackages = 
            new Dictionary<string, UnityEditor.PackageManager.PackageInfo>();

        // Known MCP extension packages registry
        private static readonly Dictionary<string, ExtensionRegistryEntry> s_ExtensionRegistry = 
            new Dictionary<string, ExtensionRegistryEntry>
            {
                {
                    "com.miao.unity.mcp.essential",
                    new ExtensionRegistryEntry(
                        "com.miao.unity.mcp.essential",
                        "Unity MCP Essential Tools",
                        "Essential tools for basic Unity MCP operations including GameObject, Scene, Assets, Component, and Editor manipulation.",
                        "MCP Team",
                        ExtensionCategory.Essential,
                        "https://github.com/MiAO-AI-LAB/Unity-MCP-Tools-Essential.git"
                    )
                },
                {
                    "com.miao.unity.mcp.behavior-designer-tools",
                    new ExtensionRegistryEntry(
                        "com.miao.unity.mcp.behavior-designer-tools",
                        "Unity MCP Behavior Designer Tools",
                        "Behavior Designer Tools for Unity MCP",
                        "MCP Team",
                        ExtensionCategory.Essential,
                        "https://github.com/MiAO-AI-LAB/Unity-MCP-Tools-Behavior-Designer.git"
                    )
                }
            };

        /// <summary>
        /// Event triggered when extension list is updated
        /// </summary>
        public static event Action OnExtensionsUpdated;

        /// <summary>
        /// Event triggered when an extension installation completes
        /// </summary>
        public static event Action<ExtensionPackageInfo, bool> OnExtensionInstalled;

        /// <summary>
        /// Gets list of all available extensions (both installed and available)
        /// </summary>
        public static List<ExtensionPackageInfo> GetAvailableExtensions()
        {
            RefreshExtensionCache();
            
            // If no extensions found after refresh, create sample data for testing
            if (s_KnownExtensions.Count == 0)
            {
                Debug.Log($"{Consts.Log.Tag} No extensions found in registry, creating sample data for testing");
                CreateSampleData();
            }
            
            var result = s_KnownExtensions.Values.ToList();
            Debug.Log($"{Consts.Log.Tag} GetAvailableExtensions returning {result.Count} extensions");
            return result;
        }

        /// <summary>
        /// Gets list of installed extensions only
        /// </summary>
        public static List<ExtensionPackageInfo> GetInstalledExtensions()
        {
            RefreshExtensionCache();
            return s_KnownExtensions.Values.Where(ext => ext.IsInstalled).ToList();
        }

        /// <summary>
        /// Gets list of extensions that have updates available
        /// </summary>
        public static List<ExtensionPackageInfo> GetExtensionsWithUpdates()
        {
            RefreshExtensionCache();
            return s_KnownExtensions.Values.Where(ext => ext.HasUpdate).ToList();
        }

        /// <summary>
        /// Gets extensions by category
        /// </summary>
        public static List<ExtensionPackageInfo> GetExtensionsByCategory(ExtensionCategory category)
        {
            RefreshExtensionCache();
            return s_KnownExtensions.Values.Where(ext => ext.ExtensionCategory == category).ToList();
        }

        /// <summary>
        /// Installs an extension package asynchronously
        /// </summary>
        public static async Task<bool> InstallExtensionAsync(ExtensionPackageInfo extension)
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} Installing extension: {extension.DisplayName}");

                // Check if extension is in registry
                if (!s_ExtensionRegistry.TryGetValue(extension.Id, out var registryEntry))
                {
                    throw new InvalidOperationException($"Extension {extension.Id} not found in registry");
                }

                // Use Unity Package Manager to add the package
                var addRequest = Client.Add(registryEntry.PackageUrl);
                
                while (!addRequest.IsCompleted)
                {
                    await Task.Delay(100);
                }

                if (addRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"{Consts.Log.Tag} Successfully installed {extension.DisplayName}");
                    
                    // Update extension status
                    extension.UpdateInstallationStatus(true, addRequest.Result.version);
                    
                    // Trigger events
                    OnExtensionInstalled?.Invoke(extension, true);
                    OnExtensionsUpdated?.Invoke();
                    
                    return true;
                }
                else
                {
                    throw new Exception($"Package Manager error: {addRequest.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to install {extension.DisplayName}: {ex.Message}");
                OnExtensionInstalled?.Invoke(extension, false);
                throw;
            }
        }

        /// <summary>
        /// Uninstalls an extension package asynchronously
        /// </summary>
        public static async Task<bool> UninstallExtensionAsync(ExtensionPackageInfo extension)
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} Uninstalling extension: {extension.DisplayName}");

                // Use Unity Package Manager to remove the package
                var removeRequest = Client.Remove(extension.Id);
                
                while (!removeRequest.IsCompleted)
                {
                    await Task.Delay(100);
                }

                if (removeRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"{Consts.Log.Tag} Successfully uninstalled {extension.DisplayName}");
                    
                    // Update extension status
                    extension.UpdateInstallationStatus(false);
                    
                    // Trigger events
                    OnExtensionsUpdated?.Invoke();
                    
                    return true;
                }
                else
                {
                    throw new Exception($"Package Manager error: {removeRequest.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to uninstall {extension.DisplayName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Updates an extension package asynchronously
        /// </summary>
        public static async Task<bool> UpdateExtensionAsync(ExtensionPackageInfo extension)
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} Updating extension: {extension.DisplayName}");

                // Check if extension is in registry
                if (!s_ExtensionRegistry.TryGetValue(extension.Id, out var registryEntry))
                {
                    throw new InvalidOperationException($"Extension {extension.Id} not found in registry");
                }

                // Use Unity Package Manager to add the latest version
                var addRequest = Client.Add(registryEntry.PackageUrl);
                
                while (!addRequest.IsCompleted)
                {
                    await Task.Delay(100);
                }

                if (addRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"{Consts.Log.Tag} Successfully updated {extension.DisplayName} to {addRequest.Result.version}");
                    
                    // Update extension status
                    extension.UpdateInstallationStatus(true, addRequest.Result.version);
                    
                    // Trigger events
                    OnExtensionsUpdated?.Invoke();
                    
                    return true;
                }
                else
                {
                    throw new Exception($"Package Manager error: {addRequest.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to update {extension.DisplayName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a specific extension is installed
        /// </summary>
        public static bool IsExtensionInstalled(string extensionId)
        {
            RefreshExtensionCache();
            return s_KnownExtensions.TryGetValue(extensionId, out var extension) && extension.IsInstalled;
        }

        /// <summary>
        /// Gets information about a specific extension
        /// </summary>
        public static ExtensionPackageInfo GetExtensionInfo(string extensionId)
        {
            RefreshExtensionCache();
            s_KnownExtensions.TryGetValue(extensionId, out var extension);
            return extension;
        }

        /// <summary>
        /// Refreshes the extension cache by querying Unity Package Manager
        /// </summary>
        public static void RefreshExtensionCache()
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} Refreshing extension cache...");
                
                // Get list of installed packages synchronously
                var listRequest = Client.List(true); // Include built-in packages
                
                // Wait for completion
                while (!listRequest.IsCompleted)
                {
                    System.Threading.Thread.Sleep(10);
                }

                if (listRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"{Consts.Log.Tag} Found {listRequest.Result.Count()} installed packages");
                    
                    // Update installed packages cache
                    s_InstalledPackages.Clear();
                    foreach (var package in listRequest.Result)
                    {
                        s_InstalledPackages[package.name] = package;
                        if (package.name.StartsWith("com.miao.unity.mcp"))
                        {
                            Debug.Log($"{Consts.Log.Tag} Found MCP package: {package.name} v{package.version}");
                        }
                    }

                    // Update known extensions
                    UpdateKnownExtensions();
                }
                else
                {
                    Debug.LogWarning($"{Consts.Log.Tag} Failed to refresh package list: {listRequest.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error refreshing extension cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the known extensions list based on registry and installed packages
        /// </summary>
        private static void UpdateKnownExtensions()
        {
            s_KnownExtensions.Clear();

            // Add all registered extensions
            foreach (var registryEntry in s_ExtensionRegistry.Values)
            {
                var extension = new ExtensionPackageInfo(
                    registryEntry.Id,
                    registryEntry.DisplayName,
                    registryEntry.Description,
                    registryEntry.Author,
                    registryEntry.LatestVersion,
                    registryEntry.Category
                );

                extension.SetUrls(registryEntry.PackageUrl, registryEntry.DocumentationUrl);
                extension.SetKeywords(registryEntry.Keywords);
                extension.SetDependencies(registryEntry.Dependencies);

                // Check if installed
                if (s_InstalledPackages.TryGetValue(registryEntry.Id, out var installedPackage))
                {
                    extension.UpdateInstallationStatus(true, installedPackage.version);
                }

                s_KnownExtensions[extension.Id] = extension;
            }

            // Also add any installed MCP packages that might not be in our registry
            foreach (var installedPackage in s_InstalledPackages.Values)
            {
                if (installedPackage.name.StartsWith("com.miao.unity.mcp") && 
                    !s_KnownExtensions.ContainsKey(installedPackage.name))
                {
                    var extension = new ExtensionPackageInfo(
                        installedPackage.name,
                        installedPackage.displayName ?? installedPackage.name,
                        installedPackage.description ?? "MCP Extension Package",
                        installedPackage.author?.name ?? "Unknown",
                        installedPackage.version,
                        ExtensionCategory.Community
                    );

                    extension.UpdateInstallationStatus(true, installedPackage.version);
                    s_KnownExtensions[extension.Id] = extension;
                }
            }
        }

        /// <summary>
        /// Registers a new extension in the registry (for development/testing)
        /// </summary>
        public static void RegisterExtension(ExtensionRegistryEntry registryEntry)
        {
            s_ExtensionRegistry[registryEntry.Id] = registryEntry;
            RefreshExtensionCache();
        }

        /// <summary>
        /// Creates sample data for testing the Hub interface
        /// </summary>
        public static void CreateSampleData()
        {
            Debug.Log($"{Consts.Log.Tag} Creating sample data for testing...");
            
            // Don't clear existing registry extensions, only add samples if needed
            var samplesToAdd = new[]
            {
                ("com.miao.unity.mcp.vision", "Vision Pack", ExtensionCategory.Vision, false),
                ("com.miao.unity.mcp.programmer", "Programmer Pack", ExtensionCategory.Programmer, false),
                ("com.miao.unity.mcp.animation", "Animation Tools", ExtensionCategory.Essential, true),
                ("com.miao.unity.mcp.physics", "Physics Tools", ExtensionCategory.Essential, false),
            };

            foreach (var (id, name, category, installed) in samplesToAdd)
            {
                if (!s_KnownExtensions.ContainsKey(id))
                {
                    var sample = ExtensionPackageInfo.CreateSampleExtension(id, name, category, installed);
                    s_KnownExtensions[sample.Id] = sample;
                    Debug.Log($"{Consts.Log.Tag} Added sample extension: {sample.DisplayName}");
                }
            }

            Debug.Log($"{Consts.Log.Tag} Sample data creation complete. Total extensions: {s_KnownExtensions.Count}");
            OnExtensionsUpdated?.Invoke();
        }
    }

    /// <summary>
    /// Registry entry for an extension package
    /// </summary>
    [Serializable]
    public class ExtensionRegistryEntry
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string Author { get; }
        public string LatestVersion { get; }
        public ExtensionCategory Category { get; }
        public string PackageUrl { get; }
        public string DocumentationUrl { get; }
        public string[] Keywords { get; }
        public string[] Dependencies { get; }

        public ExtensionRegistryEntry(string id, string displayName, string description,
            string author, ExtensionCategory category, string packageUrl,
            string latestVersion = "1.0.0", string documentationUrl = null,
            string[] keywords = null, string[] dependencies = null)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Author = author;
            LatestVersion = latestVersion;
            Category = category;
            PackageUrl = packageUrl;
            DocumentationUrl = documentationUrl;
            Keywords = keywords ?? new string[0];
            Dependencies = dependencies ?? new string[0];
        }
    }
}
#endif