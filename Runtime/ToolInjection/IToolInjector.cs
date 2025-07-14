using System;
using System.Collections.Generic;
using System.Reflection;

namespace com.MiAO.Unity.MCP.ToolInjection
{
    /// <summary>
    /// Tool metadata information
    /// </summary>
    public class ToolMetadata
    {
        public string ToolName { get; set; }
        public string PackageName { get; set; }
        public Type ToolType { get; set; }
        public MethodInfo[] Methods { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    /// <summary>
    /// Tool registration event information
    /// </summary>
    public class ToolRegistrationEvent
    {
        public string PackageName { get; set; }
        public string ToolName { get; set; }
        public string EventType { get; set; } // "Registered", "Unregistered"
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Interface for tool injection system
    /// Handles dynamic registration of tools from tool packages
    /// </summary>
    public interface IToolInjector
    {
        /// <summary>
        /// Register a tool package with the system
        /// </summary>
        /// <param name="packageName">Name of the tool package</param>
        /// <param name="assembly">Assembly containing the tools</param>
        /// <returns>Number of tools registered</returns>
        int RegisterToolPackage(string packageName, Assembly assembly);

        /// <summary>
        /// Unregister a tool package from the system
        /// </summary>
        /// <param name="packageName">Name of the tool package</param>
        /// <returns>Number of tools unregistered</returns>
        int UnregisterToolPackage(string packageName);

        /// <summary>
        /// Get all registered tools from a specific package
        /// </summary>
        /// <param name="packageName">Package name</param>
        /// <returns>Array of tool metadata</returns>
        ToolMetadata[] GetRegisteredTools(string packageName);

        /// <summary>
        /// Get all registered tools from all packages
        /// </summary>
        /// <returns>Array of tool metadata</returns>
        ToolMetadata[] GetAllRegisteredTools();

        /// <summary>
        /// Check if a tool is registered
        /// </summary>
        /// <param name="toolName">Tool name to check</param>
        /// <returns>True if tool is registered</returns>
        bool IsToolRegistered(string toolName);

        /// <summary>
        /// Get registered packages
        /// </summary>
        /// <returns>Array of registered package names</returns>
        string[] GetRegisteredPackages();

        /// <summary>
        /// Event fired when tools are registered or unregistered
        /// </summary>
        event Action<ToolRegistrationEvent> ToolRegistrationChanged;
    }
}