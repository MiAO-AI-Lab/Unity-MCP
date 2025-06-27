#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Common;
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
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

        static Tool_Console()
        {
            InitializeReflection();
        }

        private static void InitializeReflection()
        {
            try
            {
                Type logEntriesType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntries"
                );
                if (logEntriesType == null)
                    throw new Exception("Unable to find internal type UnityEditor.LogEntries");

                BindingFlags staticFlags =
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags instanceFlags =
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _startGettingEntriesMethod = logEntriesType.GetMethod(
                    "StartGettingEntries",
                    staticFlags
                );
                _endGettingEntriesMethod = logEntriesType.GetMethod(
                    "EndGettingEntries",
                    staticFlags
                );
                _getCountMethod = logEntriesType.GetMethod("GetCount", staticFlags);
                _getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", staticFlags);

                Type logEntryType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntry"
                );
                if (logEntryType == null)
                    throw new Exception("Unable to find internal type UnityEditor.LogEntry");

                _modeField = logEntryType.GetField("mode", instanceFlags);
                _messageField = logEntryType.GetField("message", instanceFlags);
                _fileField = logEntryType.GetField("file", instanceFlags);
                _lineField = logEntryType.GetField("line", instanceFlags);
                _instanceIdField = logEntryType.GetField("instanceID", instanceFlags);
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
            
            // Check if reflection initialization was successful
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

                    // Debug.Log($"{mode}, {message}");
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
                try { _endGettingEntriesMethod.Invoke(null, null); } 
                catch { /* Ignore nested exceptions */ }
                
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

        // LogEntry.mode value mapping (based on actually observed values)
        private const int ModeInfo = 8406016;      // Info log
        private const int ModeError = 8405248;     // Error log
        private const int ModeWarning1 = 8405504;  // Warning log (type 1)
        private const int ModeWarning2 = 512;      // Warning log (type 2)
        
        // // Traditional bit flag definitions (kept for compatibility)
        // private const int ModeBitError = 1 << 0;          // 1
        // private const int ModeBitAssert = 1 << 1;         // 2
        // private const int ModeBitWarning = 1 << 2;        // 4
        // private const int ModeBitLog = 1 << 3;            // 8
        // private const int ModeBitException = 1 << 4;      // 16
        // private const int ModeBitScriptingError = 1 << 9; // 512
        // private const int ModeBitScriptingWarning = 1 << 10; // 1024
        // private const int ModeBitScriptingLog = 1 << 11;  // 2048
        // private const int ModeBitScriptingException = 1 << 18; // 262144
        // private const int ModeBitScriptingAssertion = 1 << 22; // 4194304

        private static LogType GetLogTypeFromMode(int mode)
        {
            // First check specific mode values
            if (mode == ModeError)
            {
                return LogType.Error;
            }
            else if (mode == ModeWarning1 || mode == ModeWarning2)
            {
                return LogType.Warning;
            }
            else if (mode == ModeInfo)
            {
                return LogType.Log;
            }
            return LogType.Log;
            
            // // If specific values don't match, use bit flag checking (backward compatibility)
            // if ((mode & (ModeBitError | ModeBitScriptingError | ModeBitException | ModeBitScriptingException)) != 0)
            // {
            //     return LogType.Error;
            // }
            // else if ((mode & (ModeBitAssert | ModeBitScriptingAssertion)) != 0)
            // {
            //     return LogType.Assert;
            // }
            // else if ((mode & (ModeBitWarning | ModeBitScriptingWarning)) != 0)
            // {
            //     return LogType.Warning;
            // }
            // else
            // {
            //     return LogType.Log;
            // }
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