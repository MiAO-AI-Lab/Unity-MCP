#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using R3;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class HubConnectionLogger : HubConnectionObservable, IDisposable
    {
        readonly string? _guid;
        readonly ILogger _logger;
        readonly CompositeDisposable _disposables = new();

        public HubConnectionLogger(ILogger logger, HubConnection hubConnection, string? guid = null) : base(hubConnection)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _guid = guid;

            _logger.LogTrace("{0} HubConnectionLogger.Ctor.", _guid);

            Closed
                .Where(x => _logger.IsEnabled(LogLevel.Debug))
                .Subscribe(ex =>
                {
                    _logger.LogTrace("{0} HubConnectionLogger HubConnection OnClosed. Exception: {1}", _guid, ex?.Message);
                    if (ex != null)
                        _logger.LogError("{0} HubConnectionLogger Error in Closed event subscription: {1}", _guid, ex.Message);
                })
                .AddTo(_disposables);

            Reconnecting
                .Where(x => _logger.IsEnabled(LogLevel.Debug))
                .Subscribe(ex =>
                {
                    _logger.LogTrace("{0} HubConnectionLogger HubConnection OnReconnecting.", _guid);
                    if (ex != null)
                        _logger.LogError("{0} HubConnectionLogger Error during reconnecting: {1}", _guid, ex.Message);
                })
                .AddTo(_disposables);

            Reconnected
                .Where(x => _logger.IsEnabled(LogLevel.Debug))
                .Subscribe(connectionId =>
                {
                    _logger.LogTrace("{0} HubConnectionLogger HubConnection OnReconnected with id {1}.", _guid, connectionId);
                })
                .AddTo(_disposables);
        }

        public override void Dispose()
        {
            _logger.LogTrace("{0} HubConnectionLogger.Dispose.", _guid);
            base.Dispose();
            _disposables.Dispose();
        }
    }
}