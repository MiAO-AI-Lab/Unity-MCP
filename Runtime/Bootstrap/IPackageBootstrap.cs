using System;
using System.Threading.Tasks;

namespace com.MiAO.Unity.MCP.Bootstrap
{
    /// <summary>
    /// Interface for package bootstrap system
    /// Handles automatic detection and installation of external tool packages
    /// </summary>
    public interface IPackageBootstrap
    {
        /// <summary>
        /// Check if a specific tool package is available
        /// </summary>
        /// <param name="packageName">Package name to check</param>
        /// <returns>True if package is available, false otherwise</returns>
        bool IsPackageAvailable(string packageName);

        /// <summary>
        /// Get the current version of an installed package
        /// </summary>
        /// <param name="packageName">Package name</param>
        /// <returns>Version string or null if not installed</returns>
        string GetPackageVersion(string packageName);

        /// <summary>
        /// Install a tool package automatically
        /// </summary>
        /// <param name="packageName">Package name to install</param>
        /// <param name="version">Optional version to install</param>
        /// <returns>True if installation succeeded</returns>
        Task<bool> InstallPackageAsync(string packageName, string version = null);

        /// <summary>
        /// Remove a tool package
        /// </summary>
        /// <param name="packageName">Package name to remove</param>
        /// <returns>True if removal succeeded</returns>
        Task<bool> RemovePackageAsync(string packageName);

        /// <summary>
        /// Get all installed tool packages
        /// </summary>
        /// <returns>Array of installed package names</returns>
        string[] GetInstalledPackages();
    }
}