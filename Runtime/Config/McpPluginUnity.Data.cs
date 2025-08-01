using System;
using com.MiAO.MCP.Common;
using com.MiAO.MCP.Utils;
using UnityEngine;

namespace com.MiAO.MCP
{
    public partial class McpPluginUnity
    {
        [Serializable]
        public class Data
        {
            public const int DefaultPort = 60606;
            public const string DefaultHost = "http://localhost:60606";

            [SerializeField] public string host = DefaultHost;
            [SerializeField] public int port = Consts.Hub.DefaultPort;
            [SerializeField] public bool keepConnected = true;
            [SerializeField] public LogLevel logLevel = LogLevel.Warning;

            public Data SetDefault()
            {
                host = DefaultHost;
                port = Consts.Hub.DefaultPort;
                keepConnected = true;
                logLevel = LogLevel.Warning;
                return this;
            }
        }
    }
}