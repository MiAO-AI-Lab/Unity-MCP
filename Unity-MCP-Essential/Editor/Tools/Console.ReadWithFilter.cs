#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using com.IvanMurzak.ReflectorNet.Utils;
using com.MiAO.Unity.MCP.Common;
using UnityEditor;
using UnityEngine;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    // Ref: https://github.com/Unity-Technologies/UnityCsReference/blob/2022.2/Editor/Mono/LogEntries.bindings.cs 
    [Flags]
    internal enum LogMessageFlags : int
    {
        kNoLogMessageFlags = 0,
        kError = 1 << 0,
        kAssert = 1 << 1,
        kLog = 1 << 2,
        kFatal = 1 << 4,
        kAssetImportError = 1 << 6,
        kAssetImportWarning = 1 << 7,
        kScriptingError = 1 << 8,
        kScriptingWarning = 1 << 9,
        kScriptingLog = 1 << 10,
        kScriptCompileError = 1 << 11,
        kScriptCompileWarning = 1 << 12,
        kStickyLog = 1 << 13,
        kMayIgnoreLineNumber = 1 << 14,
        kReportBug = 1 << 15,
        kDisplayPreviousErrorInStatusBar = 1 << 16,
        kScriptingException = 1 << 17,
        kDontExtractStacktrace = 1 << 18,
        kScriptingAssertion = 1 << 21,
        kStacktraceIsPostprocessed = 1 << 22,
        kIsCalledFromManaged = 1 << 23
    }

    // LogMessageFlags extension methods
    internal static class LogMessageFlagsExtensions
    {
        public static bool IsInfo(this LogMessageFlags flags)
        {
            return (flags & (LogMessageFlags.kLog | LogMessageFlags.kScriptingLog)) != 0;
        }
        
        public static bool IsWarning(this LogMessageFlags flags)
        {
            return (flags & (LogMessageFlags.kScriptCompileWarning | LogMessageFlags.kScriptingWarning | LogMessageFlags.kAssetImportWarning)) != 0;
        }
        
        public static bool IsError(this LogMessageFlags flags)
        {
            return (flags & (LogMessageFlags.kFatal | LogMessageFlags.kAssert | LogMessageFlags.kError | LogMessageFlags.kScriptCompileError |
                            LogMessageFlags.kScriptingError | LogMessageFlags.kAssetImportError | LogMessageFlags.kScriptingAssertion | LogMessageFlags.kScriptingException)) != 0;
        }
    }

    public partial class Tool_Console
    {
        private static MethodInfo _startGettingEntriesMethod;
        private static MethodInfo _endGettingEntriesMethod;
        private static MethodInfo _getCountMethod;
        private static MethodInfo _getEntryMethod;
        private static FieldInfo _modeField;
        private static FieldInfo _messageField;
        private static FieldInfo _fileField;
        private static FieldInfo _lineField;
        private static FieldInfo _instanceIdField;

        private static bool _isInitialized = false;

        private static void InitializeReflection()
        {
            if (_isInitialized) return;
            
            try
            {
                Type logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
                Type logEntryType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntry");
                
                if (logEntriesType == null || logEntryType == null)
                    throw new Exception("Unable to find internal Unity types");

                BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", flags);
                _endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", flags);
                _getCountMethod = logEntriesType.GetMethod("GetCount", flags);
                _getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", flags);

                _modeField = logEntryType.GetField("mode", flags);
                _messageField = logEntryType.GetField("message", flags);
                _fileField = logEntryType.GetField("file", flags);
                _lineField = logEntryType.GetField("line", flags);
                _instanceIdField = logEntryType.GetField("instanceID", flags);
                
                _isInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Console_ReadWithFilter] Reflection initialization failed: {e.Message}");
                _startGettingEntriesMethod = _endGettingEntriesMethod = _getCountMethod = _getEntryMethod = null;
                _modeField = _messageField = _fileField = _lineField = _instanceIdField = null;
            }
        }
 
        [McpPluginTool
        (
            "Console_ReadWithFilter",
            Title = "Get Console logs with a filter"
        )]
        [Description(@"Get console log with a filter")]
        public string ReadConsoleWithFilter
        (
            [Description("Log type list, valid values: 'error', 'warning', 'log', 'all'")]
            string[] types = null,
            
            [Description("Maximum number of logs to return")]
            int? count = null,
            
            [Description("Filter text for log content")]
            string filterText = null,
            
            [Description("Whether to include stack trace information")]
            bool includeStacktrace = true,
            
            [Description("Return format, valid values: 'detailed', 'plain'")]
            string format = "detailed"
        ) => MainThread.Instance.Run(() =>
        {
            if (types == null || types.Length == 0)
            {
                types = new[] { "error", "warning", "log" };
            }
            
            // Lazy initialize reflection
            InitializeReflection();
            
            // Check if reflection initialization was successful.
            if (_startGettingEntriesMethod == null || _endGettingEntriesMethod == null ||
                _getCountMethod == null || _getEntryMethod == null || _modeField == null ||
                _messageField == null || _fileField == null || _lineField == null || _instanceIdField == null)
            {
                return JsonUtility.ToJson(new ResponseData
                {
                    success = false,
                    message = Error.InitializationFailed()
                });
            }

            List<object> entries = new List<object>();
            int retrievedCount = 0;

            try
            {
                _startGettingEntriesMethod.Invoke(null, null);

                int totalEntries = (int)_getCountMethod.Invoke(null, null);
                Type logEntryType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntry");
                object logEntryInstance = Activator.CreateInstance(logEntryType);

                var typesList = types.Select(t => t.ToLower()).ToList();
                if (typesList.Contains("all"))
                {
                    typesList = new List<string> { "error", "warning", "log" };
                }

                List<LogEntryData> logEntries = new List<LogEntryData>();

                for (int i = 0; i < totalEntries; i++)
                {
                    _getEntryMethod.Invoke(null, new object[] { i, logEntryInstance });

                    int mode = (int)_modeField.GetValue(logEntryInstance);
                    string message = (string)_messageField.GetValue(logEntryInstance);
                    string file = (string)_fileField.GetValue(logEntryInstance);
                    int line = (int)_lineField.GetValue(logEntryInstance);
                    int instanceId = (int)_instanceIdField.GetValue(logEntryInstance);

                    if (string.IsNullOrEmpty(message))
                        continue;

                    // Filter by type
                    string currentType = GetLogTypeFromMode(mode).ToString().ToLowerInvariant();
                    
                    if (!typesList.Contains(currentType))
                    {
                        continue;
                    }

                    // Filter by text
                    if (!string.IsNullOrEmpty(filterText) && 
                        message.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    string stackTrace = includeStacktrace ? ExtractStackTrace(message) : null;
                    string messageOnly = (includeStacktrace && !string.IsNullOrEmpty(stackTrace))
                        ? message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0]
                        : message;

                    if (format == "plain")
                    {
                        logEntries.Add(new LogEntryData
                        {
                            type = currentType,
                            message = messageOnly
                        });
                    }
                    else
                    {
                        logEntries.Add(new LogEntryData
                        {
                            type = currentType,
                            message = messageOnly,
                            file = file,
                            line = line,
                            instanceId = instanceId,
                            stackTrace = stackTrace
                        });
                    }

                    retrievedCount++;

                    if (count.HasValue && retrievedCount >= count.Value)
                    {
                        break;
                    }
                }

                return JsonUtility.ToJson(new ResponseData
                {
                    success = true,
                    message = $"Retrieved {logEntries.Count} log entries",
                    data = logEntries
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Console_ReadWithFilter] Error reading log entries: {e}");
                try 
                { 
                    _endGettingEntriesMethod.Invoke(null, null); 
                } 
                catch 
                { 
                    // Ignore nested exceptions 
                }
                
                return JsonUtility.ToJson(new ResponseData
                {
                    success = false,
                    message = Error.ReadingLogEntriesFailed(e.Message)
                });
            }
            finally
            {
                try { _endGettingEntriesMethod.Invoke(null, null); }
                catch (Exception e)
                {
                    Debug.LogError($"[Console_ReadWithFilter] {Error.EndGettingEntriesFailed(e.ToString())}");
                }
            }
        });

        [Serializable]
        private class ResponseData
        {
            public bool success;
            public string message;
            public List<LogEntryData> data;
        }

        [Serializable]
        private class LogEntryData
        {
            public string type;
            public string message;
            public string file;
            public int line;
            public int instanceId;
            public string stackTrace;
        }

        private static LogType GetLogTypeFromMode(int mode)
        {
            LogMessageFlags flags = (LogMessageFlags)mode;
            
            if (flags.IsError())
            {
                return LogType.Error;
            }
            else if (flags.IsWarning())
            {
                return LogType.Warning;
            }
            else if (flags.IsInfo())
            {
                return LogType.Log;
            }
            else
            {
                return LogType.Log;
            }
        }

        private static string ExtractStackTrace(string fullMessage)
        {
            if (string.IsNullOrEmpty(fullMessage))
                return null;

            string[] lines = fullMessage.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (lines.Length <= 1)
                return null;

            int stackStartIndex = -1;

            for (int i = 1; i < lines.Length; ++i)
            {
                string trimmedLine = lines[i].TrimStart();

                if (trimmedLine.StartsWith("at ") ||
                    trimmedLine.StartsWith("UnityEngine.") ||
                    trimmedLine.StartsWith("UnityEditor.") ||
                    trimmedLine.Contains("(at ") ||
                    (trimmedLine.Length > 0 && char.IsUpper(trimmedLine[0]) && trimmedLine.Contains('.')))
                {
                    stackStartIndex = i;
                    break;
                }
            }

            if (stackStartIndex > 0)
            {
                return string.Join("\n", lines.Skip(stackStartIndex));
            }

            return null;
        }
    }
}