using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MIDI.Utils
{
    public static class Logger
    {
        private static readonly ConcurrentQueue<(string level, string message, Exception? ex)> logQueue = new ConcurrentQueue<(string, string, Exception?)>();
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static Task? logWriterTask;
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
        private static bool isInitialized = false;
        private static readonly object initLock = new object();

        static Logger()
        {
            Initialize();
        }

        private static void Initialize()
        {
            lock (initLock)
            {
                if (isInitialized) return;
                logWriterTask = Task.Run(async () => await ProcessLogQueue());
                isInitialized = true;
            }
        }

        private static async Task ProcessLogQueue()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await semaphore.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }


                await Task.Delay(500, cancellationTokenSource.Token).ContinueWith(t => { }).ConfigureAwait(false);


                if (logQueue.IsEmpty) continue;

                MidiConfiguration? config = null;
                try
                {
                    config = MidiConfiguration.Default;
                }
                catch
                {

                }

                if (config == null || !config.Debug.EnableLogging)
                {
                    while (logQueue.TryDequeue(out _)) { }
                    continue;
                }

                var logs = new StringBuilder();
                while (logQueue.TryDequeue(out var item))
                {
                    logs.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{item.level}] {item.message}");
                    if (item.ex != null && config.Debug.VerboseLogging)
                    {
                        logs.Append($"\n{item.ex}");
                    }
                    logs.AppendLine();
                }

                if (logs.Length > 0)
                {
                    try
                    {
                        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        if (string.IsNullOrEmpty(assemblyLocation)) continue;
                        var logPath = Path.Combine(assemblyLocation, config.Debug.LogFilePath);

                        var fileInfo = new FileInfo(logPath);
                        if (fileInfo.Exists && fileInfo.Length > config.Debug.MaxLogSizeKB * 1024)
                        {
                            var lines = await File.ReadAllLinesAsync(logPath, cancellationTokenSource.Token).ConfigureAwait(false);
                            var newLines = lines.Skip(lines.Length / 2);
                            await File.WriteAllLinesAsync(logPath, newLines, cancellationTokenSource.Token).ConfigureAwait(false);
                        }

                        await File.AppendAllTextAsync(logPath, logs.ToString(), cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {

                    }
                    catch (UnauthorizedAccessException)
                    {

                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }

        public static void Info(string messageWithLevel, params object[] args)
        {
            var (message, level) = ParseMessage(messageWithLevel);
            WriteLog("INFO", string.Format(message, args), level, null);
        }

        public static void Warn(string messageWithLevel, params object[] args)
        {
            var (message, level) = ParseMessage(messageWithLevel);
            WriteLog("WARN", string.Format(message, args), level, null);
        }

        public static void Error(string messageWithLevel, Exception? ex, params object[] args)
        {
            var (message, level) = ParseMessage(messageWithLevel);
            WriteLog("ERROR", string.Format(message, args), level, ex);
        }

        private static (string message, int level) ParseMessage(string messageWithLevel)
        {
            var lastComma = messageWithLevel.LastIndexOf(',');
            if (lastComma != -1 && int.TryParse(messageWithLevel.AsSpan(lastComma + 1), out int level))
            {
                return (messageWithLevel.Substring(0, lastComma).Trim(), level);
            }
            return (messageWithLevel, 3);
        }

        private static void WriteLog(string level, string message, int messageLevel, Exception? ex)
        {
            MidiConfiguration? config = null;
            try
            {
                config = MidiConfiguration.Default;
            }
            catch
            {

                return;
            }

            if (config == null || !config.Debug.EnableLogging || config.Debug.LogLevel > messageLevel)
            {
                return;
            }
            logQueue.Enqueue((level, message, ex));
            try
            {
                if (semaphore.CurrentCount == 0)
                {
                    semaphore.Release();
                }
            }
            catch (ObjectDisposedException) { }
        }

        public static void Stop()
        {
            if (cancellationTokenSource.IsCancellationRequested) return;

            cancellationTokenSource.Cancel();
            try
            {

                if (semaphore.CurrentCount == 0) semaphore.Release();

                logWriterTask?.Wait(1500);
            }
            catch (OperationCanceledException) { }
            catch (AggregateException ae) when (ae.InnerExceptions.Any(e => e is OperationCanceledException)) { }
            catch (ObjectDisposedException) { }
            finally
            {
                cancellationTokenSource.Dispose();
                semaphore.Dispose();
            }
        }
    }
}