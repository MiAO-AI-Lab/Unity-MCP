namespace com.IvanMurzak.Unity.MCP.Utils
{
    public enum LogLevel
    {
        Trace = 0, // show all messages
        Debug = 1,  // show only Debug, Info, Warning, Error, Exception messages
        Info = 2,   // show only Info, Warning, Error, Exception messages
        Warning = 3,  // show only Warning, Error, Exception messages
        Error = 4,  // show only Error, Exception messages
        Exception = 5,  // show only Exception messages
        None = 6   // show no messages
    }
    public static class LogLevelEx
    {
        /// <summary>
        /// Check if the LogLevel is active
        /// If it is active the related message will be shown in the console
        /// </summary>
        public static bool IsActive(this LogLevel logLevel, LogLevel level) => logLevel <= level;
    }
}