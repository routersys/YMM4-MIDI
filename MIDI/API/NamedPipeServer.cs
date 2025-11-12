using System;
using System.IO;
using System.IO.Pipes;
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
        private readonly NamedPipeApiHandler apiHandler;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? serverTask;
        private Mutex? singleInstanceMutex;
        private bool isDisposed;

        public static bool IsClientConnected { get; private set; }
        public static event Action? ConnectionStatusChanged;

        public NamedPipeServer(MidiSettingsViewModel viewModel, MidiConfiguration configuration)
        {
            apiHandler = new NamedPipeApiHandler(viewModel, configuration);
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
                singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
                if (!createdNew)
                {
                    Logger.Warn(LogMessages.NamedPipeAlreadyRunning);
                    singleInstanceMutex?.Dispose();
                    singleInstanceMutex = null;
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error(LogMessages.NamedPipeMutexError, ex);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.NamedPipeMutexError, ex);
                return false;
            }


            cancellationTokenSource = new CancellationTokenSource();
            serverTask = Task.Run(() => ServerLoop(cancellationTokenSource.Token), cancellationTokenSource.Token);
            Logger.Info(LogMessages.NamedPipeServerStarted);
            return true;
        }


        private async Task ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                StreamReader? reader = null;
                StreamWriter? writer = null;

                try
                {
                    server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    Logger.Info(LogMessages.NamedPipeWaitingConnection);
                    await server.WaitForConnectionAsync(token);
                    UpdateConnectionStatus(true);
                    Logger.Info(LogMessages.NamedPipeClientConnected);

                    reader = new StreamReader(server, Encoding.UTF8);
                    writer = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true };

                    while (server.IsConnected && !token.IsCancellationRequested)
                    {
                        var requestJson = await reader.ReadLineAsync();
                        if (requestJson == null)
                        {
                            break;
                        }

                        if (!token.IsCancellationRequested)
                        {
                            var responseJson = await apiHandler.HandleRequest(requestJson);
                            await writer.WriteLineAsync(responseJson);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Info(LogMessages.NamedPipeOperationCancelled);
                    break;
                }
                catch (IOException ex) when (ex.Message.Contains("Pipe is broken") || ex.Message.Contains("パイプが壊れています"))
                {
                    Logger.Info(LogMessages.NamedPipeClientDisconnectedIO);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Logger.Error(LogMessages.NamedPipeServerError, ex, ex.Message);
                        await Task.Delay(1000, token);
                    }
                }
                finally
                {
                    Logger.Info(LogMessages.NamedPipeClientDisconnected);
                    UpdateConnectionStatus(false);
                    reader?.Dispose();
                    writer?.Dispose();
                    server?.Dispose();
                }
            }
            Logger.Info(LogMessages.NamedPipeServerStopped);
        }

        public void Stop()
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

            if (serverTask != null)
            {
                try
                {
                    serverTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (OperationCanceledException) { }
                catch (AggregateException ae) when (ae.InnerExceptions.Any(e => e is OperationCanceledException)) { }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.NamedPipeStopError, ex);
                }
                serverTask = null;
            }

            singleInstanceMutex?.ReleaseMutex();
            singleInstanceMutex?.Dispose();
            singleInstanceMutex = null;

            UpdateConnectionStatus(false);
            Logger.Info(LogMessages.NamedPipeServerExplicitStop);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    Stop();
                    cancellationTokenSource?.Dispose();
                }
                isDisposed = true;
            }
        }
    }
}