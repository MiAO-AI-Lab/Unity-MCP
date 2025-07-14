using System;
using System.Linq;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Convertor;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Common.Json;
using com.MiAO.Unity.MCP.Common.Json.Converters;
using com.MiAO.Unity.MCP.Common.Reflection.Convertor;
using com.MiAO.Unity.MCP.Reflection.Convertor;
using com.MiAO.Unity.MCP.Utils;
using com.MiAO.Unity.MCP.Bootstrap;
using com.MiAO.Unity.MCP.ToolInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace com.MiAO.Unity.MCP
{
    using LogLevelMicrosoft = Microsoft.Extensions.Logging.LogLevel;
    using LogLevel = Utils.LogLevel;
    using Consts = com.MiAO.Unity.MCP.Common.Consts;

    public partial class McpPluginUnity
    {
        private static IToolInjector _toolInjector;
        private static IPackageBootstrap _packageBootstrap;

        public static IToolInjector ToolInjector => _toolInjector ??= new ToolInjector();
        public static IPackageBootstrap PackageBootstrap => _packageBootstrap ??= Bootstrap.PackageBootstrap.Instance;

        public static void BuildAndStart()
        {
            McpPlugin.StaticDisposeAsync();
            MainThreadInstaller.Init();

            // Initialize tool injection system
            InitializeToolInjection();

            var mcpPlugin = new McpPluginBuilder()
                .WithAppFeatures()
                .WithConfig(config =>
                {
                    if (McpPluginUnity.LogLevel.IsActive(LogLevel.Info))
                        Debug.Log($"{Consts.Log.Tag} MCP server address: {McpPluginUnity.Host}");

                    config.Endpoint = McpPluginUnity.Host;
                })
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders(); // 👈 Clears the default providers
                    loggingBuilder.AddProvider(new UnityLoggerProvider());
                    loggingBuilder.SetMinimumLevel(McpPluginUnity.LogLevel switch
                    {
                        LogLevel.Trace => LogLevelMicrosoft.Trace,
                        LogLevel.Debug => LogLevelMicrosoft.Debug,
                        LogLevel.Info => LogLevelMicrosoft.Information,
                        LogLevel.Warning => LogLevelMicrosoft.Warning,
                        LogLevel.Error => LogLevelMicrosoft.Error,
                        LogLevel.Exception => LogLevelMicrosoft.Critical,
                        _ => LogLevelMicrosoft.Warning
                    });
                })
                .WithToolsFromAssembly(AppDomain.CurrentDomain.GetAssemblies())
                .WithPromptsFromAssembly(AppDomain.CurrentDomain.GetAssemblies())
                .WithResourcesFromAssembly(AppDomain.CurrentDomain.GetAssemblies())
                .Build(CreateDefaultReflector());

            if (McpPluginUnity.KeepConnected)
            {
                if (McpPluginUnity.LogLevel.IsActive(LogLevel.Info))
                {
                    var message = "<b><color=yellow>Connecting</color></b>";
                    Debug.Log($"{Consts.Log.Tag} {message} <color=orange>ಠ‿ಠ</color>");
                }
                mcpPlugin.Connect();
            }
        }

        /// <summary>
        /// Initialize tool injection system and register external tool packages
        /// </summary>
        private static void InitializeToolInjection()
        {
            try
            {
                Debug.Log($"{Consts.Log.Tag} Initializing tool injection system...");

                // Register built-in tools from current assembly (if any remain)
                ToolInjector.RegisterToolPackage("com.miao.unity.mcp.builtin", typeof(McpPluginUnity).Assembly);

                // Discover and register external tool packages
                var installedPackages = PackageBootstrap.GetInstalledPackages();
                foreach (var packageName in installedPackages)
                {
                    try
                    {
                        // Try to load assembly for each tool package
                        var assembly = GetAssemblyByPackageName(packageName);
                        if (assembly != null)
                        {
                            ToolInjector.RegisterToolPackage(packageName, assembly);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"{Consts.Log.Tag} Failed to register tools from package {packageName}: {ex.Message}");
                    }
                }

                // Listen for tool registration events
                ToolInjector.ToolRegistrationChanged += OnToolRegistrationChanged;

                Debug.Log($"{Consts.Log.Tag} Tool injection system initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Failed to initialize tool injection system: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle tool registration events
        /// </summary>
        private static void OnToolRegistrationChanged(ToolRegistrationEvent eventArgs)
        {
            if (McpPluginUnity.LogLevel.IsActive(LogLevel.Info))
            {
                Debug.Log($"{Consts.Log.Tag} Tool {eventArgs.EventType}: {eventArgs.ToolName} from package {eventArgs.PackageName}");
            }
        }

        /// <summary>
        /// Get assembly by package name
        /// </summary>
        private static System.Reflection.Assembly GetAssemblyByPackageName(string packageName)
        {
            try
            {
                // Try to find assembly by package name
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name?.Contains(packageName.Replace('.', '.')) == true);
            }
            catch
            {
                return null;
            }
        }

        static Reflector CreateDefaultReflector()
        {
            var reflector = new Reflector();

            // Remove converters that are not needed in Unity
            reflector.Convertors.Remove<GenericReflectionConvertor<object>>();
            reflector.Convertors.Remove<ArrayReflectionConvertor>();

            // Add Unity-specific converters
            reflector.Convertors.Add(new RS_GenericUnity<object>());
            reflector.Convertors.Add(new RS_ArrayUnity());

            // Unity types
            reflector.Convertors.Add(new RS_UnityEngineColor32());
            reflector.Convertors.Add(new RS_UnityEngineColor());
            reflector.Convertors.Add(new RS_UnityEngineMatrix4x4());
            reflector.Convertors.Add(new RS_UnityEngineQuaternion());
            reflector.Convertors.Add(new RS_UnityEngineVector2());
            reflector.Convertors.Add(new RS_UnityEngineVector2Int());
            reflector.Convertors.Add(new RS_UnityEngineVector3());
            reflector.Convertors.Add(new RS_UnityEngineVector3Int());
            reflector.Convertors.Add(new RS_UnityEngineVector4());

            // Components
            reflector.Convertors.Add(new RS_UnityEngineObject());
            reflector.Convertors.Add(new RS_UnityEngineGameObject());
            reflector.Convertors.Add(new RS_UnityEngineComponent());
            reflector.Convertors.Add(new RS_UnityEngineTransform());
            reflector.Convertors.Add(new RS_UnityEngineRenderer());
            reflector.Convertors.Add(new RS_UnityEngineMeshFilter());

            // Assets
            reflector.Convertors.Add(new RS_UnityEngineMaterial());
            reflector.Convertors.Add(new RS_UnityEngineSprite());

            return reflector;
        }

        public static void RegisterJsonConverters()
        {
            JsonUtils.AddConverter(new Color32Converter());
            JsonUtils.AddConverter(new ColorConverter());
            JsonUtils.AddConverter(new Matrix4x4Converter());
            JsonUtils.AddConverter(new QuaternionConverter());
            JsonUtils.AddConverter(new Vector2Converter());
            JsonUtils.AddConverter(new Vector2IntConverter());
            JsonUtils.AddConverter(new Vector3Converter());
            JsonUtils.AddConverter(new Vector3IntConverter());
            JsonUtils.AddConverter(new Vector4Converter());

            JsonUtils.AddConverter(new ObjectRefConverter());
        }
    }
}