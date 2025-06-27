using System;
using System.Threading;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    internal static class Safe
    {
        public static bool Run(Action action, LogLevel logLevel)
        {
            try
            {
                action?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                if (logLevel.IsActive(LogLevel.Exception))
                    Debug.LogException(e);

                return false;
            }
        }
        public static bool Run<T>(Action<T> action, T value, LogLevel logLevel)
        {
            try
            {
                action?.Invoke(value);
                return true;
            }
            catch (Exception e)
            {
                if (logLevel.IsActive(LogLevel.Exception))
                    Debug.LogException(e);

                return false;
            }
        }
        public static bool Run<T1, T2>(Action<T1, T2> action, T1 value1, T2 value2, LogLevel logLevel)
        {
            try
            {
                action?.Invoke(value1, value2);
                return true;
            }
            catch (Exception e)
            {
                if (logLevel.IsActive(LogLevel.Exception))
                    Debug.LogException(e);

                return false;
            }
        }
        public static TResult Run<TInput, TResult>(Func<TInput, TResult> action, TInput input, LogLevel logLevel)
        {
            try
            {
                return action.Invoke(input);
            }
            catch (Exception e)
            {
                if (logLevel.IsActive(LogLevel.Exception))
                    Debug.LogException(e);

                return default;
            }
        }
        public static bool Run(WeakAction action, LogLevel logLevel)
        {
            try
            {
                action?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                if (logLevel.IsActive(LogLevel.Exception))
                    Debug.LogException(e);

                return false;
            }
        }
        public static bool Run<T>(WeakAction<T> action, T value, LogLevel logLevel)
        {
            try
            {
                action?.Invoke(value);
                return true;
            }
            catch (Exception e)
            {
                if (logLevel.IsActive(LogLevel.Exception))
                    Debug.LogException(e);

                return false;
            }
        }
        public static bool RunCancel(CancellationTokenSource cts, LogLevel logLevel)
        {
            try
            {
                if (cts == null)
                    return false;

                if (cts.IsCancellationRequested)
                    return false;

                cts.Cancel();
                return true;
            }
            catch (Exception e)
            {
                if (logLevel.IsActive(LogLevel.Exception))
                    Debug.LogException(e);

                return false;
            }
        }
    }
}