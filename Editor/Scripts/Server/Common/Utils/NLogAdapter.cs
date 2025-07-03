#if !UNITY_5_3_OR_NEWER
using System;
using Microsoft.Extensions.Logging;
using NLog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogger = NLog.Logger;

namespace com.MiAO.Unity.MCP.Server.Utils
{
    /// <summary>
    /// Adapter to bridge NLog.Logger to Microsoft.Extensions.Logging.ILogger
    /// </summary>
    public class NLogAdapter : ILogger
    {
        private readonly NLogger _nLogger;

        public NLogAdapter(NLogger nLogger)
        {
            _nLogger = nLogger ?? throw new ArgumentNullException(nameof(nLogger));
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new NoOpDisposable();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _nLogger.IsEnabled(ConvertLogLevel(logLevel));
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var nLogLevel = ConvertLogLevel(logLevel);

            if (exception != null)
            {
                _nLogger.Log(nLogLevel, exception, message);
            }
            else
            {
                _nLogger.Log(nLogLevel, message);
            }
        }

        private static NLog.LogLevel ConvertLogLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => NLog.LogLevel.Trace,
                LogLevel.Debug => NLog.LogLevel.Debug,
                LogLevel.Information => NLog.LogLevel.Info,
                LogLevel.Warning => NLog.LogLevel.Warn,
                LogLevel.Error => NLog.LogLevel.Error,
                LogLevel.Critical => NLog.LogLevel.Fatal,
                LogLevel.None => NLog.LogLevel.Off,
                _ => NLog.LogLevel.Info
            };
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
                // No-op
            }
        }
    }
}
#endif