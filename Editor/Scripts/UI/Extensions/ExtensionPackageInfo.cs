#if UNITY_EDITOR
using System;
using UnityEngine;

namespace com.MiAO.MCP.Editor.Extensions
{
    /// <summary>
    /// Contains information about an MCP extension package
    /// Represents both available and installed packages
    /// </summary>
    [Serializable]
    public class ExtensionPackageInfo
    {
        [SerializeField] private string m_Id;
        [SerializeField] private string m_DisplayName;
        [SerializeField] private string m_Description;
        [SerializeField] private string m_Author;
        [SerializeField] private string m_LatestVersion;
        [SerializeField] private string m_InstalledVersion;
        [SerializeField] private string m_Category;
        [SerializeField] private string m_IconPath;
        [SerializeField] private string m_PackageUrl;
        [SerializeField] private string m_DocumentationUrl;
        [SerializeField] private string[] m_Dependencies;
        [SerializeField] private string[] m_Keywords;
        [SerializeField] private bool m_IsInstalled;
        [SerializeField] private bool m_HasUpdate;
        [SerializeField] private long m_InstallSize;
        [SerializeField] private DateTime m_LastUpdated;
        [SerializeField] private ExtensionCategory m_ExtensionCategory;

        // Properties
        public string Id => m_Id;
        public string DisplayName => m_DisplayName;
        public string Description => m_Description;
        public string Author => m_Author;
        public string LatestVersion => m_LatestVersion;
        public string InstalledVersion => m_InstalledVersion;
        public string Category => m_Category;
        public string IconPath => m_IconPath;
        public string PackageUrl => m_PackageUrl;
        public string DocumentationUrl => m_DocumentationUrl;
        public string[] Dependencies => m_Dependencies;
        public string[] Keywords => m_Keywords;
        public bool IsInstalled => m_IsInstalled;
        public bool HasUpdate => m_HasUpdate;
        public long InstallSize => m_InstallSize;
        public DateTime LastUpdated => m_LastUpdated;
        public ExtensionCategory ExtensionCategory => m_ExtensionCategory;

        /// <summary>
        /// Constructor for creating extension package info
        /// </summary>
        public ExtensionPackageInfo(string id, string displayName, string description, 
            string author, string latestVersion, ExtensionCategory category)
        {
            m_Id = id;
            m_DisplayName = displayName;
            m_Description = description;
            m_Author = author;
            m_LatestVersion = latestVersion;
            m_ExtensionCategory = category;
            m_Category = category.ToString();
            m_Dependencies = new string[0];
            m_Keywords = new string[0];
            m_IsInstalled = false;
            m_HasUpdate = false;
            m_LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Updates the installation status of the package
        /// </summary>
        public void UpdateInstallationStatus(bool isInstalled, string installedVersion = null)
        {
            m_IsInstalled = isInstalled;
            m_InstalledVersion = installedVersion ?? string.Empty;
            
            // Check if update is available
            if (isInstalled && !string.IsNullOrEmpty(installedVersion) && !string.IsNullOrEmpty(m_LatestVersion))
            {
                m_HasUpdate = CompareVersions(installedVersion, m_LatestVersion) < 0;
            }
            else
            {
                m_HasUpdate = false;
            }
        }

        /// <summary>
        /// Sets the package dependencies
        /// </summary>
        public void SetDependencies(string[] dependencies)
        {
            m_Dependencies = dependencies ?? new string[0];
        }

        /// <summary>
        /// Sets the package keywords
        /// </summary>
        public void SetKeywords(string[] keywords)
        {
            m_Keywords = keywords ?? new string[0];
        }

        /// <summary>
        /// Sets the package URLs
        /// </summary>
        public void SetUrls(string packageUrl, string documentationUrl = null, string iconPath = null)
        {
            m_PackageUrl = packageUrl;
            m_DocumentationUrl = documentationUrl;
            m_IconPath = iconPath;
        }

        /// <summary>
        /// Sets the package size information
        /// </summary>
        public void SetSizeInfo(long installSize)
        {
            m_InstallSize = installSize;
        }

        /// <summary>
        /// Compares two version strings
        /// Returns -1 if version1 < version2, 0 if equal, 1 if version1 > version2
        /// </summary>
        private int CompareVersions(string version1, string version2)
        {
            try
            {
                var v1 = new Version(version1);
                var v2 = new Version(version2);
                return v1.CompareTo(v2);
            }
            catch
            {
                // Fallback to string comparison if version parsing fails
                return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Gets formatted size string
        /// </summary>
        public string GetFormattedSize()
        {
            if (m_InstallSize <= 0) return "Unknown size";
            
            if (m_InstallSize < 1024) return $"{m_InstallSize} B";
            if (m_InstallSize < 1024 * 1024) return $"{m_InstallSize / 1024:F1} KB";
            if (m_InstallSize < 1024 * 1024 * 1024) return $"{m_InstallSize / (1024 * 1024):F1} MB";
            return $"{m_InstallSize / (1024 * 1024 * 1024):F1} GB";
        }

        /// <summary>
        /// Checks if this package matches a search query
        /// </summary>
        public bool MatchesSearch(string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            
            query = query.ToLowerInvariant();
            
            return m_DisplayName.ToLowerInvariant().Contains(query) ||
                   m_Description.ToLowerInvariant().Contains(query) ||
                   m_Author.ToLowerInvariant().Contains(query) ||
                   m_Category.ToLowerInvariant().Contains(query) ||
                   (m_Keywords != null && Array.Exists(m_Keywords, keyword => 
                       keyword.ToLowerInvariant().Contains(query)));
        }

        /// <summary>
        /// Creates a sample extension package for testing
        /// </summary>
        public static ExtensionPackageInfo CreateSampleExtension(string id, string name, 
            ExtensionCategory category, bool isInstalled = false)
        {
            var extension = new ExtensionPackageInfo(id, name, 
                $"Sample {category} extension for MCP Hub", 
                "MCP Team", "1.0.0", category);
            
            extension.SetUrls($"https://github.com/example/{id}", 
                $"https://docs.example.com/{id}");
            extension.SetSizeInfo(UnityEngine.Random.Range(1024 * 10, 1024 * 1024 * 5)); // 10KB to 5MB
            extension.SetKeywords(new[] { category.ToString().ToLower(), "mcp", "unity" });
            
            if (isInstalled)
            {
                extension.UpdateInstallationStatus(true, "1.0.0");
            }
            
            return extension;
        }
    }

    /// <summary>
    /// Extension package categories
    /// </summary>
    public enum ExtensionCategory
    {
        Essential,
        Vision,
        Programmer,
        Community,
        Experimental,
        Deprecated
    }
}
#endif