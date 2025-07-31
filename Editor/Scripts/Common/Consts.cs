using System;

namespace com.MiAO.MCP.Editor.Common
{
    /// <summary>
    /// Visual Studio MCP 配置文件位置
    /// </summary>
    public enum VisualStudioConfigLocation
    {
        /// <summary>
        /// 用户全局配置 (%USERPROFILE%\.mcp.json)
        /// </summary>
        Global = 0,
        
        /// <summary>
        /// 解决方案级配置 (<SOLUTIONDIR>\.mcp.json)
        /// </summary>
        Solution = 1,
        
        /// <summary>
        /// Visual Studio特定配置 (<SOLUTIONDIR>\.vs\mcp.json)
        /// </summary>
        VisualStudioSpecific = 2
    }

    /// <summary>
    /// Project constants definition
    /// </summary>
    public static class Consts
    {
        /// <summary>
        /// Log related constants
        /// </summary>
        public static class Log
        {
            public const string Tag = "[MCP]";
            
            // Log level short names
            public const string Crit = "CRIT";
            public const string Fail = "FAIL";
            public const string Warn = "WARN";
            public const string Info = "INFO";
            public const string Dbug = "DBUG";
            public const string Trce = "TRCE";
            
            /// <summary>
            /// Log color related constants
            /// </summary>
            public static class Color
            {
                public const string LevelStart = "<color=#FFA500>";
                public const string LevelEnd = "</color>";
                public const string CategoryStart = "<color=#00FFFF>";
                public const string CategoryEnd = "</color>";
            }
        }

        /// <summary>
        /// MIME type constants
        /// </summary>
        public static class MimeType
        {
            public const string TextJson = "application/json";
            public const string TextPlain = "text/plain";
        }

        /// <summary>
        /// MCP related constants
        /// </summary>
        public static class MCP
        {
            public const int LinesLimit = 100;
        }

        /// <summary>
        /// MCP client configuration
        /// </summary>
        public static class MCP_Client
        {
            public static class ClaudeDesktop
            {
                public static string Config(string serverExecutablePath, string bodyName = "mcpServers", int port = 8080)
                {
                    return $@"{{
  ""{bodyName}"": {{
    ""Unity-MCP"": {{
      ""command"": ""{serverExecutablePath}"",
      ""args"": [
        ""--port"",
        ""{port}""
      ]
    }}
  }}
}}";
                }
            }
        }

        /// <summary>
        /// RPC method name constants
        /// </summary>
        public static class RPC
        {
            /// <summary>
            /// Client RPC methods
            /// </summary>
            public static class Client
            {
                public const string ForceDisconnect = "ForceDisconnect";
                public const string RunCallTool = "RunCallTool";
                public const string RunListTool = "RunListTool";
                public const string RunResourceContent = "RunResourceContent";
                public const string RunListResources = "RunListResources";
                public const string RunListResourceTemplates = "RunListResourceTemplates";
                public const string RequestModelUse = "RequestModelUse";
            }

            /// <summary>
            /// Server RPC methods
            /// </summary>
            public static class Server
            {
                public const string OnListToolsUpdated = "OnListToolsUpdated";
                public const string OnListResourcesUpdated = "OnListResourcesUpdated";
            }
        }

        /// <summary>
        /// Hub related constants
        /// </summary>
        public static class Hub
        {
            public const int DefaultPort = 8080;
            public const string DefaultEndpoint = "http://localhost:8080";
            public const string RemoteApp = "/remoteApp";
            public const int TimeoutSeconds = 30;
        }

        /// <summary>
        /// Environment variable constants
        /// </summary>
        public static class Env
        {
            public const string Port = "MCP_PORT";
        }

        /// <summary>
        /// GUID related constants
        /// </summary>
        public static class Guid
        {
            public static readonly System.Guid Zero = System.Guid.Empty;
        }

        /// <summary>
        /// Path related constants
        /// </summary>
        public static class Route
        {
            public const string GameObject_CurrentScene = "gameObject://currentScene/{path}";
        }

        /// <summary>
        /// Query parameter constants
        /// </summary>
        public const string AllRecursive = "**";
        public const string All = "*";
    }
} 