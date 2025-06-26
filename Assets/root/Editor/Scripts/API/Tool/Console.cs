#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Unity.MCP.Common;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    [McpPluginToolType]
    public partial class Tool_Console
    {
        public static class Error
        {
            public static string InitializationFailed()
                => "Unable to access console logs, reflection initialization failed";

            public static string ReadingLogEntriesFailed(string errorMessage)
                => $"Error reading log entries: {errorMessage}";

            public static string EndGettingEntriesFailed(string errorMessage)
                => $"Failed to call EndGettingEntries: {errorMessage}";
        }
    }
}