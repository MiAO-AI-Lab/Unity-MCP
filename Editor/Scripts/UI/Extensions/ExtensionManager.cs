#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using com.MiAO.MCP.Common;
using Debug = UnityEngine.Debug;

namespace com.MiAO.MCP.Editor.Extensions
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
                    "com.miao.mcp.essential",
                    new ExtensionRegistryEntry(
                        "com.miao.mcp.essential",
                        "Unity MCP Essential Tools",
                        "Essential tools for basic Unity MCP operations including GameObject, Scene, Assets, Component, and Editor manipulation.",
                        "MCP Team",
                        ExtensionCategory.Essential,
                        "https://github.com/MiAO-AI-LAB/Unity-MCP-Tools-Essential.git"
                    )
                },
                {
                    "com.miao.mcp.behavior-designer-tools",
                    new ExtensionRegistryEntry(
                        "com.miao.mcp.behavior-designer-tools",
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

                // Check if this is a local package installation
                if (IsLocalPackageInstallation(registryEntry))
                {
                    Debug.Log($"{Consts.Log.Tag} Installing as local package: {extension.DisplayName}");
                    
                    // Get the actual directory name for this package
                    var actualDirectoryName = GetPackageDirectoryName(extension.Id);
                    var packagePath = Path.Combine("Packages", actualDirectoryName);
                    
                    // Check if package already exists
                    if (Directory.Exists(packagePath))
                    {
                        Debug.Log($"{Consts.Log.Tag} Package already exists locally: {extension.DisplayName}");
                        extension.UpdateInstallationStatus(true, "local");
                        OnExtensionInstalled?.Invoke(extension, true);
                        OnExtensionsUpdated?.Invoke();
                        return true;
                    }
                    
                    // Clone the package from Git repository
                    Debug.Log($"{Consts.Log.Tag} Cloning package from Git: {registryEntry.PackageUrl}");
                    var cloneResult = await ClonePackageFromGit(registryEntry.PackageUrl, packagePath, actualDirectoryName);
                    
                    if (cloneResult)
                    {
                        Debug.Log($"{Consts.Log.Tag} Successfully cloned package: {extension.DisplayName}");
                        extension.UpdateInstallationStatus(true, "local");
                        OnExtensionInstalled?.Invoke(extension, true);
                        OnExtensionsUpdated?.Invoke();
                        return true;
                    }
                    else
                    {
                        throw new Exception($"Failed to clone package from Git: {registryEntry.PackageUrl}");
                    }
                }
                else
                {
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

                // Check if this is a local package (installed in Packages directory)
                if (IsLocalPackage(extension.Id))
                {
                    Debug.Log($"{Consts.Log.Tag} Detected local package: {extension.Id}, removing from Packages directory");
                    
                    // Get the actual directory name for this package
                    var actualDirectoryName = GetPackageDirectoryName(extension.Id);
                    var packagePath = Path.Combine("Packages", actualDirectoryName);
                    
                    if (Directory.Exists(packagePath))
                    {
                        try
                        {
                            // Check if this is a Git submodule
                            if (IsGitSubmodule(packagePath))
                            {
                                Debug.Log($"{Consts.Log.Tag} Detected Git submodule: {actualDirectoryName}");
                                
                                // For Git submodules, we need to use Git commands
                                // First, try to remove the submodule using Git
                                var gitRemoveResult = RemoveGitSubmodule(packagePath, actualDirectoryName);
                                if (gitRemoveResult)
                                {
                                    Debug.Log($"{Consts.Log.Tag} Successfully removed Git submodule: {extension.DisplayName}");
                                    
                                    // Update extension status
                                    extension.UpdateInstallationStatus(false);
                                    
                                    // Trigger events
                                    OnExtensionsUpdated?.Invoke();
                                    
                                    return true;
                                }
                                else
                                {
                                    throw new Exception("Failed to remove Git submodule");
                                }
                            }
                            else
                            {
                                // Use Unity's AssetDatabase to delete the package directory
                                // This is safer than direct file system operations
                                if (AssetDatabase.DeleteAsset($"Packages/{actualDirectoryName}"))
                                {
                                    Debug.Log($"{Consts.Log.Tag} Successfully removed local package: {extension.DisplayName}");
                                    
                                    // Update extension status
                                    extension.UpdateInstallationStatus(false);
                                    
                                    // Trigger events
                                    OnExtensionsUpdated?.Invoke();
                                    
                                    return true;
                                }
                                else
                                {
                                    throw new Exception("AssetDatabase.DeleteAsset failed");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // If AssetDatabase.DeleteAsset fails, try direct file system deletion
                            try
                            {
                                Debug.Log($"{Consts.Log.Tag} AssetDatabase deletion failed, trying direct file system deletion: {ex.Message}");
                                
                                // Remove the package directory
                                Directory.Delete(packagePath, true);
                                
                                // Also remove the .meta file if it exists
                                var metaPath = packagePath + ".meta";
                                if (File.Exists(metaPath))
                                {
                                    File.Delete(metaPath);
                                }
                                
                                Debug.Log($"{Consts.Log.Tag} Successfully removed local package via file system: {extension.DisplayName}");
                                
                                // Update extension status
                                extension.UpdateInstallationStatus(false);
                                
                                // Trigger events
                                OnExtensionsUpdated?.Invoke();
                                
                                return true;
                            }
                            catch (Exception fsEx)
                            {
                                throw new Exception($"Failed to remove local package directory: {fsEx.Message}. Original error: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"Local package directory not found: {packagePath}");
                    }
                }
                else
                {
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
                // Get list of installed packages synchronously
                var listRequest = Client.List(true); // Include built-in packages
                
                // Wait for completion
                while (!listRequest.IsCompleted)
                {
                    System.Threading.Thread.Sleep(10);
                }

                if (listRequest.Status == StatusCode.Success)
                {
                    
                    // Update installed packages cache
                    s_InstalledPackages.Clear();
                    foreach (var package in listRequest.Result)
                    {
                        s_InstalledPackages[package.name] = package;
                        if (package.name.StartsWith("com.miao.mcp"))
                        {
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
                if (installedPackage.name.StartsWith("com.miao.mcp") && 
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
                ("com.miao.mcp.vision", "Vision Pack", ExtensionCategory.Vision, false),
                ("com.miao.mcp.programmer", "Programmer Pack", ExtensionCategory.Programmer, false),
                ("com.miao.mcp.animation", "Animation Tools", ExtensionCategory.Essential, true),
                ("com.miao.mcp.physics", "Physics Tools", ExtensionCategory.Essential, false),
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

        /// <summary>
        /// Checks if a package is installed as a local package in the Packages directory
        /// </summary>
        private static bool IsLocalPackage(string packageId)
        {
            // Check if the package directory exists
            var packagePath = Path.Combine("Packages", packageId);
            if (Directory.Exists(packagePath))
            {
                return true;
            }
            
            // Check if it's a local package with a different directory name
            var actualDirectoryName = GetPackageDirectoryName(packageId);
            var actualPackagePath = Path.Combine("Packages", actualDirectoryName);
            if (Directory.Exists(actualPackagePath))
            {
                return true;
            }
            
            // Check if it's listed in packages-lock.json as a local package
            return IsPackageInLockFile(packageId);
        }

        /// <summary>
        /// Checks if a package is listed in packages-lock.json as a local package
        /// </summary>
        private static bool IsPackageInLockFile(string packageId)
        {
            try
            {
                var lockFilePath = Path.Combine("Packages", "packages-lock.json");
                if (!File.Exists(lockFilePath))
                {
                    return false;
                }
                
                var lockFileContent = File.ReadAllText(lockFilePath);
                // Simple check - if the package is mentioned with "file:" source, it's a local package
                return lockFileContent.Contains($"\"{packageId}\"") && 
                       lockFileContent.Contains($"\"source\": \"embedded\"");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Consts.Log.Tag} Error checking packages-lock.json: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a package directory is a Git submodule
        /// </summary>
        private static bool IsGitSubmodule(string packagePath)
        {
            var gitPath = Path.Combine(packagePath, ".git");
            return Directory.Exists(gitPath);
        }

        /// <summary>
        /// Gets the actual directory name for a package ID
        /// </summary>
        private static string GetPackageDirectoryName(string packageId)
        {
            // Map package IDs to actual directory names
            var packageIdToDirectoryMap = new Dictionary<string, string>
            {
                { "com.miao.mcp.behavior-designer-tools", "Unity-MCP-Tools-Behavior-Designer" },
                { "com.miao.mcp.essential", "Unity-MCP-Essential" },
                { "com.miao.mcp", "Unity-MCP" }
            };

            return packageIdToDirectoryMap.TryGetValue(packageId, out var directoryName) ? directoryName : packageId;
        }

        /// <summary>
        /// Checks if a registry entry represents a local package installation
        /// </summary>
        private static bool IsLocalPackageInstallation(ExtensionRegistryEntry registryEntry)
        {
            // Check if the package URL points to a local path or if it's a known local package
            return registryEntry.PackageUrl.Contains("github.com/MiAO-AI-LAB") || 
                   registryEntry.PackageUrl.Contains("github.com/MiAO-AI-Lab");
        }

        /// <summary>
        /// Removes a Git repository from the project
        /// </summary>
        private static bool RemoveGitSubmodule(string packagePath, string directoryName)
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} Removing Git repository: {directoryName}");
                
                // Check if the directory exists
                if (!Directory.Exists(packagePath))
                {
                    Debug.LogWarning($"{Consts.Log.Tag} Package directory does not exist: {packagePath}");
                    return true; // Consider it already removed
                }
                
                // First, try to remove read-only attributes to avoid permission issues
                try
                {
                    RemoveReadOnlyAttributes(packagePath);
                    Debug.Log($"{Consts.Log.Tag} Removed read-only attributes from: {directoryName}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{Consts.Log.Tag} Could not remove read-only attributes: {ex.Message}");
                }
                
                // Try to remove the directory directly
                try
                {
                    Directory.Delete(packagePath, true);
                    Debug.Log($"{Consts.Log.Tag} Successfully removed directory: {directoryName}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{Consts.Log.Tag} Could not remove directory directly: {ex.Message}");
                    
                    // Try alternative approach: remove files one by one
                    try
                    {
                        RemoveDirectoryRecursively(packagePath);
                        Debug.Log($"{Consts.Log.Tag} Successfully removed directory recursively: {directoryName}");
                        return true;
                    }
                    catch (Exception recursiveEx)
                    {
                        Debug.LogError($"{Consts.Log.Tag} Failed to remove directory recursively: {recursiveEx.Message}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to remove Git repository {directoryName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a directory recursively by deleting files one by one
        /// </summary>
        private static void RemoveDirectoryRecursively(string directoryPath)
        {
            try
            {
                // First, remove read-only attributes from all files and directories
                RemoveReadOnlyAttributes(directoryPath);
                
                // Delete all files first
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        Debug.Log($"{Consts.Log.Tag} Deleted file: {file}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"{Consts.Log.Tag} Could not delete file {file}: {ex.Message}");
                    }
                }
                
                // Delete all directories (in reverse order to handle nested directories)
                var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length) // Delete deepest directories first
                    .ToList();
                
                foreach (var dir in directories)
                {
                    try
                    {
                        Directory.Delete(dir);
                        Debug.Log($"{Consts.Log.Tag} Deleted directory: {dir}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"{Consts.Log.Tag} Could not delete directory {dir}: {ex.Message}");
                    }
                }
                
                // Finally, delete the main directory
                try
                {
                    Directory.Delete(directoryPath);
                    Debug.Log($"{Consts.Log.Tag} Deleted main directory: {directoryPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{Consts.Log.Tag} Could not delete main directory {directoryPath}: {ex.Message}");
                    throw; // Re-throw to indicate failure
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error removing directory recursively: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Removes read-only attributes from all files in a directory recursively
        /// </summary>
        private static void RemoveReadOnlyAttributes(string directoryPath)
        {
            try
            {
                // Remove read-only attribute from all files in the directory
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"{Consts.Log.Tag} Could not remove read-only attribute from {file}: {ex.Message}");
                    }
                }
                
                // Remove read-only attribute from all directories
                var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);
                foreach (var dir in directories)
                {
                    try
                    {
                        var attributes = File.GetAttributes(dir);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(dir, attributes & ~FileAttributes.ReadOnly);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"{Consts.Log.Tag} Could not remove read-only attribute from directory {dir}: {ex.Message}");
                    }
                }
                
                // Remove read-only attribute from the main directory
                try
                {
                    var attributes = File.GetAttributes(directoryPath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(directoryPath, attributes & ~FileAttributes.ReadOnly);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{Consts.Log.Tag} Could not remove read-only attribute from main directory {directoryPath}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error removing read-only attributes: {ex.Message}");
            }
        }

        /// <summary>
        /// Clones a package from Git repository to the Packages directory
        /// </summary>
        private static async Task<bool> ClonePackageFromGit(string gitUrl, string targetPath, string directoryName)
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} Starting Git clone: {gitUrl} -> {targetPath}");
                
                // Ensure the Packages directory exists
                var packagesDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(packagesDir))
                {
                    Directory.CreateDirectory(packagesDir);
                }
                
                // Remove target directory if it exists
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }
                
                // Execute Git clone command
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone {gitUrl} \"{directoryName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetFullPath("Packages")
                };
                
                Debug.Log($"{Consts.Log.Tag} Working directory: {Path.GetFullPath("Packages")}");
                Debug.Log($"{Consts.Log.Tag} Clone target: {directoryName}");
                
                using (var process = new System.Diagnostics.Process { StartInfo = startInfo })
                {
                    Debug.Log($"{Consts.Log.Tag} Executing: git clone {gitUrl} \"{targetPath}\"");
                    
                    process.Start();
                    
                    // Read output asynchronously
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    
                    // Wait for completion
                    await Task.Run(() => process.WaitForExit());
                    
                    var output = await outputTask;
                    var error = await errorTask;
                    
                    if (process.ExitCode == 0)
                    {
                        Debug.Log($"{Consts.Log.Tag} Git clone successful: {output}");
                        
                        // Refresh AssetDatabase to detect the new package
                        AssetDatabase.Refresh();
                        
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"{Consts.Log.Tag} Git clone failed: {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to clone package from Git: {ex.Message}");
                return false;
            }
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