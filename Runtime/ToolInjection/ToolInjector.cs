using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using com.MiAO.MCP.Common;

namespace com.MiAO.MCP.ToolInjection
{
    /// <summary>
    /// Tool injection system implementation
    /// Dynamically registers and manages tools from tool packages
    /// </summary>
    public class ToolInjector : IToolInjector
    {
        private readonly Dictionary<string, Assembly> _registeredPackages = new Dictionary<string, Assembly>();
        private readonly Dictionary<string, ToolMetadata> _registeredTools = new Dictionary<string, ToolMetadata>();
        private readonly object _lock = new object();

        public event Action<ToolRegistrationEvent> ToolRegistrationChanged;

        /// <summary>
        /// Register a tool package with the system
        /// </summary>
        public int RegisterToolPackage(string packageName, Assembly assembly)
        {
            lock (_lock)
            {
                try
                {
                    // Check if package is already registered
                    if (_registeredPackages.ContainsKey(packageName))
                    {
                        Debug.LogWarning($"[ToolInjector] Package {packageName} is already registered. Skipping.");
                        return 0;
                    }

                    // Scan the assembly for classes marked with McpPluginToolTypeAttribute
                    var toolTypes = assembly.GetTypes()
                        .Where(t => t.GetCustomAttribute<McpPluginToolTypeAttribute>() != null)
                        .ToArray();

                    int registeredCount = 0;

                    foreach (var toolType in toolTypes)
                    {
                        // Get methods marked with McpPluginToolAttribute
                        var toolMethods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                            .Where(m => m.GetCustomAttribute<McpPluginToolAttribute>() != null)
                            .ToArray();

                        if (toolMethods.Length > 0)
                        {
                            var toolMetadata = new ToolMetadata
                            {
                                ToolName = toolType.Name,
                                PackageName = packageName,
                                ToolType = toolType,
                                Methods = toolMethods,
                                RegisteredAt = DateTime.Now
                            };

                            _registeredTools[toolType.Name] = toolMetadata;
                            registeredCount++;

                            // Fire event
                            ToolRegistrationChanged?.Invoke(new ToolRegistrationEvent
                            {
                                PackageName = packageName,
                                ToolName = toolType.Name,
                                EventType = "Registered",
                                Timestamp = DateTime.Now
                            });
                        }
                    }

                    _registeredPackages[packageName] = assembly;
                    // Debug.Log($"[ToolInjector] Successfully registered {registeredCount} tools from package: {packageName}");
                    return registeredCount;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ToolInjector] Error registering package {packageName}: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Unregister a tool package from the system
        /// </summary>
        public int UnregisterToolPackage(string packageName)
        {
            lock (_lock)
            {
                try
                {
                    if (!_registeredPackages.ContainsKey(packageName))
                    {
                        Debug.LogWarning($"[ToolInjector] Package {packageName} is not registered. Skipping.");
                        return 0;
                    }

                    // Find all tools from this package
                    var toolsToRemove = _registeredTools.Values
                        .Where(t => t.PackageName == packageName)
                        .ToArray();

                    int unregisteredCount = 0;

                    foreach (var toolMetadata in toolsToRemove)
                    {
                        _registeredTools.Remove(toolMetadata.ToolName);
                        unregisteredCount++;

                        // Fire event
                        ToolRegistrationChanged?.Invoke(new ToolRegistrationEvent
                        {
                            PackageName = packageName,
                            ToolName = toolMetadata.ToolName,
                            EventType = "Unregistered",
                            Timestamp = DateTime.Now
                        });
                    }

                    _registeredPackages.Remove(packageName);
                    Debug.Log($"[ToolInjector] Successfully unregistered {unregisteredCount} tools from package: {packageName}");
                    return unregisteredCount;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ToolInjector] Error unregistering package {packageName}: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Get all registered tools from a specific package
        /// </summary>
        public ToolMetadata[] GetRegisteredTools(string packageName)
        {
            lock (_lock)
            {
                return _registeredTools.Values
                    .Where(t => t.PackageName == packageName)
                    .ToArray();
            }
        }

        /// <summary>
        /// Get all registered tools from all packages
        /// </summary>
        public ToolMetadata[] GetAllRegisteredTools()
        {
            lock (_lock)
            {
                return _registeredTools.Values.ToArray();
            }
        }

        /// <summary>
        /// Check if a tool is registered
        /// </summary>
        public bool IsToolRegistered(string toolName)
        {
            lock (_lock)
            {
                return _registeredTools.ContainsKey(toolName);
            }
        }

        /// <summary>
        /// Get registered packages
        /// </summary>
        public string[] GetRegisteredPackages()
        {
            lock (_lock)
            {
                return _registeredPackages.Keys.ToArray();
            }
        }
    }
}