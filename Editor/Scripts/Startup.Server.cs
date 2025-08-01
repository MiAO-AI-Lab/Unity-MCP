#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.MiAO.MCP.Common;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using System;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
using com.MiAO.MCP.Editor.Utils;
using com.MiAO.MCP.Utils;
using System.Linq;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.MiAO.MCP.Editor
{
    using Consts = com.MiAO.MCP.Common.Consts;
    public static partial class Startup
    {
        public const string PackageName = "com.miao.mcp";
        public const string ServerProjectName = "com.miao.mcp.server";

        // Server source path
        public static string PackageCache => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library", "PackageCache"));
        public static string? ServerSourcePath
        {
            get
            {
                var sourceDir = new DirectoryInfo(PackageCache)
                    .GetDirectories()
                    .FirstOrDefault(d => d.Name.ToLowerInvariant().Contains(PackageName.ToLowerInvariant()))
                    ?.FullName;

                if (string.IsNullOrEmpty(sourceDir))
                {
                    var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Assets/MiAO-MCP-for-Unity/Editor/Scripts/Server"));
                    Debug.Log($"{Consts.Log.Tag} Set server path: <color=#8CFFD1>{path}</color>");
                    return path;
                }

                return sourceDir;
            }
        }

        // Server executable path
        public static string ServerExecutableRootPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library", ServerProjectName.ToLowerInvariant()));
        public static string ServerExecutableFolder => Path.Combine(ServerExecutableRootPath, "bin~", "Release", "net9.0");
        public static string ServerExecutableFile => Path.Combine(ServerExecutableFolder, $"{ServerProjectName}");

        // Log files
        public static string ServerLogsPath => Path.Combine(ServerExecutableFolder, "logs", "server-log.txt");
        public static string ServerErrorLogsPath => Path.Combine(ServerExecutableFolder, "logs", "server-log-error.txt");

        // Version files
        public static string ServerSourceVersionPath => Path.GetFullPath(Path.Combine(ServerSourcePath, "version"));
        public static string ServerExecutableVersionPath => Path.GetFullPath(Path.Combine(ServerExecutableFolder, "version"));

        // Versions
        public static string ServerSourceVersion => FileUtils.ReadFileContent(ServerSourceVersionPath)?.Trim() ?? "unknown";
        public static string ServerExecutableVersion => FileUtils.ReadFileContent(ServerExecutableVersionPath)?.Trim() ?? "unknown";

        // Verification
        public static bool IsServerCompiled => FileUtils.FileExistsWithoutExtension(ServerExecutableFolder, ServerProjectName);
        public static bool ServerVersionMatched => ServerSourceVersion == ServerExecutableVersion;

        // -------------------------------------------------------------------------------------------------------------------------------------------------

        public static string RawJsonConfiguration(int port, string bodyName = "mcpServers") => Consts.MCP_Client.ClaudeDesktop.Config(
            ServerExecutableFile.Replace('\\', '/'),
            bodyName,
            port
        );

        public static Task BuildServerIfNeeded(bool force = true)
        {
            if (IsServerCompiled && ServerVersionMatched)
                return Task.CompletedTask;

            return BuildServer(force);
        }

        public static async Task BuildServer(bool force = true)
        {
            var message = $"<b><color=yellow>Server Build</color></b>";
            Debug.Log($"{Consts.Log.Tag} {message} <color=orange>⊂(◉‿◉)つ</color>");
            Debug.Log($"{Consts.Log.Tag} Current Server version: <color=#8CFFD1>{ServerExecutableVersion}</color>. New Server version: <color=#8CFFD1>{ServerSourceVersion}</color>");

            await InstallDotNetIfNeeded();
            CopyServerSources();
            DeleteSlnFiles();

            Debug.Log($"{Consts.Log.Tag} Building server at <color=#8CFFD1>{ServerExecutableRootPath}</color>");

            (string output, string error) = await ProcessUtils.Run(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build -c Release {ServerProjectName}.csproj",
                WorkingDirectory = ServerExecutableRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            await MainThread.Instance.RunAsync(() => HandleBuildResult(output, error, force));
        }

        private static async Task HandleBuildResult(string output, string error, bool force)
        {
            var isError = !string.IsNullOrEmpty(error) ||
                output.Contains("Build FAILED") ||
                output.Contains("MSBUILD : error") ||
                output.Contains("error MSB");

            // Format the build output
            var formattedOutput = FormatBuildOutput(output);
            var formattedError = FormatBuildOutput(error);

            if (isError)
            {
                if (force)
                {
                    Debug.LogWarning($"{Consts.Log.Tag} <color=red>Build failed</color>. Check the output for details:\n{formattedOutput}\n{formattedError}\n");
                    if (ErrorUtils.ExtractProcessId(output, out var processId))
                    {
                        Debug.Log($"{Consts.Log.Tag} Detected another process which locks the file. Killing the process with ID: {processId}");
                        // Kill the process that locks the file
                        (string _output, string _error) = await ProcessUtils.Run(new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/PID {processId} /F",
                            WorkingDirectory = ServerExecutableRootPath,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        Debug.Log($"{Consts.Log.Tag} Trying to rebuild server one more time");
                        await BuildServer(force: false);
                    }
                    else
                    {
                        await BuildServer(force: false);
                    }
                }
                else
                {
                    Debug.LogError($"{Consts.Log.Tag} <color=red>Build failed</color>. Check the output for details:\n{formattedOutput}\n{formattedError}\n");
                }
            }
            else
            {
                Debug.Log($"{Consts.Log.Tag} <color=green>Build succeeded</color>. Check the output for details:\n{formattedOutput}");
            }
        }

        private static string FormatBuildOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
                return string.Empty;

            // Use Environment.NewLine to ensure correct line break handling
            var lines = output.Split(new[] { Environment.NewLine, "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Debug.Log($"{Consts.Log.Tag} FormatBuildOutput: Original length: {output.Length}, Lines count: {lines.Length}");
            
            var formattedLines = new string[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.Trim();
                
                // Keep empty lines unchanged
                if (string.IsNullOrEmpty(trimmedLine))
                {
                    formattedLines[i] = line;
                }
                else if (IsErrorLine(trimmedLine))
                {
                    formattedLines[i] = $"<color=red>[ERROR]</color> {line}";
                }
                else if (IsWarningLine(trimmedLine))
                {
                    formattedLines[i] = $"<color=orange>[WARNING]</color> {line}";
                }
                else
                {
                    formattedLines[i] = line;
                }
            }
            // Debug.Log($"{Consts.Log.Tag} FormatBuildOutput: lines count: {formattedLines.Length}");
            var result = string.Join("\n", formattedLines);

            
            return result;
        }

        private static bool IsErrorLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            var lowerLine = line.ToLowerInvariant();
            return lowerLine.Contains("error") ||
                   lowerLine.Contains("fatal") ||
                   lowerLine.Contains("failed") ||
                   lowerLine.Contains("exception") ||
                   lowerLine.Contains("msbuild : error") ||
                   lowerLine.Contains("build failed");
        }

        private static bool IsWarningLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            var lowerLine = line.ToLowerInvariant();
            return lowerLine.Contains("warning") ||
                   lowerLine.Contains("warn:");
        }

        public static void CopyServerSources()
        {
            Debug.Log($"{Consts.Log.Tag} Delete sources at: <color=#8CFFD1>{ServerExecutableRootPath}</color>");
            try
            {
                DirectoryUtils.Delete(ServerExecutableRootPath, recursive: true);
            }
            catch (UnauthorizedAccessException) { /* ignore */ }

            var sourceDir = ServerSourcePath;
            Debug.Log($"{Consts.Log.Tag} Copy sources from: <color=#8CFFD1>{sourceDir}</color>");
            try
            {
                DirectoryUtils.Copy(sourceDir, ServerExecutableRootPath, "*/bin~", "*/obj~", "*\\bin~", "*\\obj~", "*.meta");
            }
            catch (DirectoryNotFoundException ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Server source directory not found. Please check the path: <color=#8CFFD1>{PackageCache}</color>");
                Debug.LogException(ex);
            }
        }
        public static void DeleteSlnFiles()
        {
            var slnFiles = Directory.GetFiles(ServerExecutableRootPath, "*.sln*", SearchOption.TopDirectoryOnly);
            foreach (var slnFile in slnFiles)
            {
                try
                {
                    File.Delete(slnFile);
                }
                catch (UnauthorizedAccessException) { /* ignore */ }
            }
        }
    }
}