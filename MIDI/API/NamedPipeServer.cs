using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MIDI.Utils;

namespace MIDI.API
{
    public class NamedPipeServer : IDisposable
    {
        private const string PipeName = "YMM4MidiPluginApi";
        private const string MutexName = "Global\\YMM4MidiPluginApiMutex";

        private readonly NamedPipeApiHandler _apiHandler;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _serverTask;
        private Mutex? _singleInstanceMutex;
        private bool _isDisposed;

        public static bool IsClientConnected { get; private set; }
        public static event Action? ConnectionStatusChanged;

        public NamedPipeServer(MidiSettingsViewModel viewModel, MidiConfiguration configuration)
        {
            _apiHandler = new NamedPipeApiHandler(viewModel, configuration);
        }

        private static void UpdateConnectionStatus(bool isConnected)
        {
            if (IsClientConnected != isConnected)
            {
                IsClientConnected = isConnected;
                ConnectionStatusChanged?.Invoke();
            }
        }

        public bool TryStartServer()
        {
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
                if (!createdNew)
                {
                    Logger.Warn(LogMessages.NamedPipeAlreadyRunning);
                    _singleInstanceMutex?.Dispose();
                    _singleInstanceMutex = null;
                    return false;
                }
            }
            catch
            {
                return false;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _serverTask = Task.Run(() => ServerLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            Logger.Info(LogMessages.NamedPipeServerStarted);
            return true;
        }

        private async Task ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    UpdateConnectionStatus(true);
                    Logger.Info(LogMessages.NamedPipeClientConnected);

                    using (var reader = new StreamReader(server, new UTF8Encoding(false), true, 1024, leaveOpen: true))
                    using (var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true })
                    {
                        while (server.IsConnected && !token.IsCancellationRequested)
                        {
                            var requestJson = await reader.ReadLineAsync();

                            if (requestJson == null)
                            {
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(requestJson))
                            {
                                continue;
                            }

                            var responseJson = await _apiHandler.HandleRequest(requestJson);

                            await writer.WriteLineAsync(responseJson);

                            await writer.FlushAsync();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    Logger.Info(LogMessages.NamedPipeClientDisconnectedIO);
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.NamedPipeServerError, ex);
                    if (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, token);
                    }
                }
                finally
                {
                    UpdateConnectionStatus(false);
                    server?.Dispose();
                }
            }
            Logger.Info(LogMessages.NamedPipeServerStopped);
        }

        public void Stop()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            if (_serverTask != null)
            {
                try
                {
                    _serverTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch { }
                _serverTask = null;
            }

            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;

            UpdateConnectionStatus(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stop();
                    _cancellationTokenSource?.Dispose();
                }
                _isDisposed = true;
            }
        }
    }
}