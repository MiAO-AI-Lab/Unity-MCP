#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using R3;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class ConnectionManager : IConnectionManager
    {
        public const string Version = "0.7.0";

        readonly string _guid = Guid.NewGuid().ToString();
        readonly ILogger<ConnectionManager> _logger;
        readonly ReactiveProperty<HubConnection?> _hubConnection = new();
        readonly Func<string, Task<HubConnection>> _hubConnectionBuilder;
        readonly ReactiveProperty<HubConnectionState> _connectionState = new(HubConnectionState.Disconnected);
        readonly ReactiveProperty<bool> _continueToReconnect = new(false);
        readonly CompositeDisposable _disposables = new();

        Task<bool>? connectionTask;
        HubConnectionLogger? hubConnectionLogger;
        HubConnectionObservable? hubConnectionObservable;
        CancellationTokenSource? internalCts;
        public ReadOnlyReactiveProperty<HubConnectionState> ConnectionState => _connectionState.ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<HubConnection?> HubConnection => _hubConnection.ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<bool> KeepConnected => _continueToReconnect.ToReadOnlyReactiveProperty();
        public string Endpoint { get; set; } = string.Empty;

        public ConnectionManager(ILogger<ConnectionManager> logger, Func<string, Task<HubConnection>> hubConnectionBuilder)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogTrace("{0} Ctor. Version: {1}", _guid, Version);

            _hubConnectionBuilder = hubConnectionBuilder ?? throw new ArgumentNullException(nameof(hubConnectionBuilder));
            _hubConnection.Subscribe(hubConnection =>
            {
                if (hubConnection == null)
                {
                    _connectionState.Value = HubConnectionState.Disconnected;
                    return;
                }

                hubConnection.ToObservable().State
                    .Subscribe(state => _connectionState.Value = state)
                    .AddTo(_disposables);
            })
            .AddTo(_disposables);

            _connectionState
                .Where(state => state == HubConnectionState.Reconnecting && _continueToReconnect.CurrentValue)
                .Subscribe(async state => await Connect())
                .AddTo(_disposables);
        }

        public async Task InvokeAsync<TInput>(string methodName, TInput input, CancellationToken cancellationToken = default)
        {
            if (_hubConnection.CurrentValue?.State != HubConnectionState.Connected && _continueToReconnect.CurrentValue)
            {
                await Connect(cancellationToken);
                if (_hubConnection.CurrentValue?.State != HubConnectionState.Connected)
                {
                    _logger.LogError("{0} Can't establish connection with Remote.", _guid);
                    return;
                }
            }
            if (_hubConnection.CurrentValue == null)
            {
                _logger.LogError("{0} HubConnection is null. Can't invoke method {1}.", _guid, methodName);
                return;
            }

            await _hubConnection.CurrentValue.InvokeAsync(methodName, input, cancellationToken).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                    return;

                _logger.LogError("{0} Failed to invoke method {1}: {2}", _guid, methodName, task.Exception?.Message);
            });
        }

        public async Task<TResult> InvokeAsync<TInput, TResult>(string methodName, TInput input, CancellationToken cancellationToken = default)
        {
            if (_hubConnection.CurrentValue?.State != HubConnectionState.Connected && _continueToReconnect.CurrentValue)
            {
                _logger.LogDebug("{0} Connection is not established. Attempting to connect...", _guid);
                // Attempt to connect if the connection is not established
                await Connect(cancellationToken);

                if (_hubConnection.CurrentValue?.State != HubConnectionState.Connected)
                {
                    _logger.LogError("{0} Can't establish connection with Remote.", _guid);
                    return default!;
                }
            }
            if (_hubConnection.CurrentValue == null)
            {
                _logger.LogError("{0} HubConnection is null. Can't invoke method {1}.", _guid, methodName);
                return default!;
            }

            return await _hubConnection.CurrentValue.InvokeAsync<TResult>(methodName, input, cancellationToken).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                    return task.Result;

                _logger.LogError("{0} Failed to invoke method {1}: {2}", _guid, methodName, task.Exception?.Message);
                return default!;
            });
        }

        public async Task<bool> Connect(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("{0} Connect.", _guid);

            if (_hubConnection.Value?.State == HubConnectionState.Connected)
            {
                _logger.LogDebug("{0} Already connected. Ignoring.", _guid);
                return true;
            }

            _continueToReconnect.Value = false;

            // Dispose the previous internal CancellationTokenSource if it exists
            CancelInternalToken(dispose: true);

            if (_hubConnection.Value != null)
                await _hubConnection.Value.StopAsync();

            _continueToReconnect.Value = true;

            internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (connectionTask != null)
            {
                _logger.LogDebug("{0} Connection task already exists. Waiting for the completion... {1}.", _guid, Endpoint);
                // Create a new task that waits for the existing task but can be canceled independently
                return await Task.Run(async () =>
                {
                    try
                    {
                        await connectionTask; // Wait for the existing connection task
                        return _hubConnection.Value?.State == HubConnectionState.Connected;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("{0} Connection task was canceled {1}.", _guid, Endpoint);
                        return false;
                    }
                }, internalCts.Token);
            }

            try
            {
                connectionTask = InternalConnect(internalCts.Token);
                return await connectionTask;
            }
            catch (Exception ex)
            {
                _logger.LogError("{0} Error during connection: {1}\n{2}", _guid, ex.Message, ex.StackTrace);
                return false;
            }
            finally
            {
                connectionTask = null;
            }
        }

        void CancelInternalToken(bool dispose = false)
        {
            if (internalCts != null)
            {
                if (!internalCts.IsCancellationRequested)
                    internalCts.Cancel();

                if (dispose)
                {
                    internalCts.Dispose();
                    internalCts = null;
                }
            }
        }

        async Task<bool> InternalConnect(CancellationToken cancellationToken)
        {
            if (_hubConnection.Value == null)
            {
                _logger.LogDebug("{0} Creating new HubConnection instance {1}.", _guid, Endpoint);
                var hubConnection = await _hubConnectionBuilder(Endpoint);
                if (hubConnection == null)
                {
                    _logger.LogError("{0} Can't create connection instance. Something may be wrong with Connection Config {1}.", _guid, Endpoint);
                    return false;
                }

                _hubConnection.Value = hubConnection;

                hubConnectionLogger?.Dispose();
                hubConnectionLogger = new(_logger, hubConnection, guid: _guid);

                hubConnectionObservable?.Dispose();
                hubConnectionObservable = new(hubConnection);
                hubConnectionObservable.Closed
                    .Where(_ => _continueToReconnect.CurrentValue)
                    .Subscribe(async _ =>
                    {
                        connectionTask = null;
                        if (cancellationToken.IsCancellationRequested)
                            return;
                        _logger.LogWarning("{0} Connection closed. Attempting to reconnect... {1}.", _guid, Endpoint);
                        await InternalConnect(cancellationToken);
                    })
                    .RegisterTo(cancellationToken);
            }

            _logger.LogDebug("{0} Connecting to {1}...", _guid, Endpoint);
            while (_continueToReconnect.CurrentValue && !cancellationToken.IsCancellationRequested)
            {
                var connection = _hubConnection.CurrentValue;
                if (connection == null)
                {
                    _logger.LogTrace("{0} Waiting before retry... {1}", _guid, Endpoint);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before retrying
                    continue;
                }

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var task = connection.StartAsync(cts.Token);
                try
                {
                    await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
                    if (!task.IsCompletedSuccessfully)
                    {
                        if (_continueToReconnect.CurrentValue && !cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogTrace("{0} Waiting before retry... {1}", _guid, Endpoint);
                            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before retrying
                        }
                        continue;
                    }
                    _logger.LogInformation("{0} Connection started successfully {1}.", _guid, Endpoint);
                    _connectionState.Value = HubConnectionState.Connected;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("{0} Failed to start connection. {1} - {2}\n{3}", _guid, Endpoint, ex.Message, ex.StackTrace);
                }
                finally
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                if (_continueToReconnect.CurrentValue && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogTrace("{0} Waiting before retry... {1}", _guid, Endpoint);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before retrying
                }
            }
            return false;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("{0} Disconnect.", _guid);
            connectionTask = null;
            _continueToReconnect.Value = false;

            // Cancel the internal token to stop any ongoing connection attempts
            CancelInternalToken(dispose: false);

            if (_hubConnection.Value == null)
                return Task.CompletedTask;

            return _hubConnection.Value.StopAsync(cancellationToken).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    _logger.LogInformation("{0} HubConnection stopped successfully.", _guid);
                }
                else if (task.Exception != null)
                {
                    _logger.LogError("{0} Error while stopping HubConnection: {1}\n{2}", _guid, task.Exception.Message, task.Exception.StackTrace);
                }
                _connectionState.Value = HubConnectionState.Disconnected;
            });
        }

        public void Dispose()
        {
#pragma warning disable CS4014
            DisposeAsync();
            // DisposeAsync().Wait();
            // Unity won't reload Domain if we call DisposeAsync().Wait() here.
#pragma warning restore CS4014
        }

        public async Task DisposeAsync()
        {
            _logger.LogTrace("{0} DisposeAsync.", _guid);

            connectionTask = null;
            _disposables.Dispose();

            if (!_continueToReconnect.IsDisposed)
                _continueToReconnect.Value = false;

            hubConnectionLogger?.Dispose();
            hubConnectionObservable?.Dispose();

            _connectionState.Dispose();
            _continueToReconnect.Dispose();

            CancelInternalToken(dispose: true);

            if (_hubConnection.CurrentValue != null)
            {
                try
                {
                    var tempHubConnection = _hubConnection.Value;

                    _hubConnection.Value = null;
                    _hubConnection.Dispose();

                    if (tempHubConnection != null)
                    {
                        await tempHubConnection.StopAsync()
                            .ContinueWith(task =>
                            {
                                try
                                {
                                    tempHubConnection.DisposeAsync();
                                }
                                catch { }
                            });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error during async disposal: {0}\n{1}", ex.Message, ex.StackTrace);
                }
            }

            _hubConnection.Dispose();
        }

        ~ConnectionManager() => Dispose();
    }
}