#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class FixedRetryPolicy : IRetryPolicy
    {
        private readonly TimeSpan _delay;

        public FixedRetryPolicy(TimeSpan delay)
        {
            _delay = delay;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return _delay;
        }
    }
}