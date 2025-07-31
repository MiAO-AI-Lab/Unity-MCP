using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;

namespace com.MiAO.MCP.Bootstrap
{
    /// <summary>
    /// Package bootstrap system implementation
    /// Manages automatic installation and dependency resolution for tool packages
    /// </summary>
    public class PackageBootstrap : IPackageBootstrap
    {
        private static PackageBootstrap _instance;
        private Dictionary<string, string> _packageVersionCache = new Dictionary<string, string>();
        private readonly object _lock = new object();

        public static PackageBootstrap Instance => _instance ??= new PackageBootstrap();

        /// <summary>
        /// Check if a specific tool package is available
        /// </summary>
        public bool IsPackageAvailable(string packageName)
        {
            lock (_lock)
            {
                try
                {
                    var listRequest = Client.List(true);
                    while (!listRequest.IsCompleted)
                    {
                        System.Threading.Thread.Sleep(10);
                    }

                    if (listRequest.Status == StatusCode.Success)
                    {
                        var package = listRequest.Result.FirstOrDefault(p => p.name == packageName);
                        return package != null;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PackageBootstrap] Error checking package availability: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Get the current version of an installed package
        /// </summary>
        public string GetPackageVersion(string packageName)
        {
            lock (_lock)
            {
                if (_packageVersionCache.TryGetValue(packageName, out var cachedVersion))
                    return cachedVersion;

                try
                {
                    var listRequest = Client.List(true);
                    while (!listRequest.IsCompleted)
                    {
                        System.Threading.Thread.Sleep(10);
                    }

                    if (listRequest.Status == StatusCode.Success)
                    {
                        var package = listRequest.Result.FirstOrDefault(p => p.name == packageName);
                        if (package != null)
                        {
                            _packageVersionCache[packageName] = package.version;
                            return package.version;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PackageBootstrap] Error getting package version: {ex.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Install a tool package automatically
        /// </summary>
        public async Task<bool> InstallPackageAsync(string packageName, string version = null)
        {
            try
            {
                var packageId = string.IsNullOrEmpty(version) ? packageName : $"{packageName}@{version}";

                var addRequest = Client.Add(packageId);
                while (!addRequest.IsCompleted)
                {
                    await Task.Delay(100);
                }

                if (addRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"[PackageBootstrap] Successfully installed package: {packageId}");
                    lock (_lock)
                    {
                        _packageVersionCache[packageName] = addRequest.Result.version;
                    }
                    return true;
                }
                else
                {
                    Debug.LogError($"[PackageBootstrap] Failed to install package: {packageId}. Error: {addRequest.Error?.message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PackageBootstrap] Exception installing package: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove a tool package
        /// </summary>
        public async Task<bool> RemovePackageAsync(string packageName)
        {
            try
            {
                var removeRequest = Client.Remove(packageName);
                while (!removeRequest.IsCompleted)
                {
                    await Task.Delay(100);
                }

                if (removeRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"[PackageBootstrap] Successfully removed package: {packageName}");
                    lock (_lock)
                    {
                        _packageVersionCache.Remove(packageName);
                    }
                    return true;
                }
                else
                {
                    Debug.LogError($"[PackageBootstrap] Failed to remove package: {packageName}. Error: {removeRequest.Error?.message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PackageBootstrap] Exception removing package: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all installed tool packages
        /// </summary>
        public string[] GetInstalledPackages()
        {
            lock (_lock)
            {
                try
                {
                    var listRequest = Client.List(true);
                    while (!listRequest.IsCompleted)
                    {
                        System.Threading.Thread.Sleep(10);
                    }

                    if (listRequest.Status == StatusCode.Success)
                    {
                        return listRequest.Result
                            .Where(p => p.name.StartsWith("com.miao.mcp.") && !p.name.Equals("com.miao.mcp"))
                            .Select(p => p.name)
                            .ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PackageBootstrap] Error getting installed packages: {ex.Message}");
                }

                return new string[0];
            }
        }
    }
}