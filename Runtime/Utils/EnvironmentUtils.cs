using System;

namespace com.MiAO.MCP.Utils
{
    public static class EnvironmentUtils
    {
        public static string GITHUB_ACTIONS => Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        public static bool IsGitHubActions => GITHUB_ACTIONS == "true";
    }
}