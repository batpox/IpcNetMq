using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMqHelpers
{
    /// <summary>
    /// A memory buffered Logger
    /// </summary>
    public static class Logger
    {
        private static readonly BlockingCollection<string> _logQueue = new BlockingCollection<string>();
        private static string _filePath = string.Empty;
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static readonly Task _logTask = Task.Run(() => ProcessLogQueue(_cancellationTokenSource.Token));

        /// <summary>Gets the local timestamp most recently assigned when the logger detected a date change.</summary>
        public static DateTime Timestamp { get; private set; }
        private static DateTime lastTimestamp = DateTime.Now;

        static Logger()
        {
            Timestamp = DateTime.Now;
            _logQueue.Add($"********** New Day: {Timestamp}");
        }

        /// <summary>Sets the log file path.</summary>
        /// <remarks>Call before writing messages. This legacy logger starts its processing task during static initialization.</remarks>
        /// <param name="filePath">The destination path, or null/empty to select IpcNetMqServer.log in the Documents folder.</param>
        public static void Initialize(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                filePath = Path.Combine(folderPath, "IpcNetMqServer.log");
            }

            _filePath = filePath;
        }

        /// <summary>Queues a message with a timestamp prefix and adds a date marker when the date changes.</summary>
        /// <param name="message">The message to append to the log.</param>
        public static void LogIt(string message)
        {
            DateTime dateTime = DateTime.Now;
            if (dateTime.Date != Timestamp.Date)
            {
                Timestamp = dateTime;
                _logQueue.Add($"********** New Day: {Timestamp}");
            }

            _logQueue.Add($"{Timestamp:HH:mm:ss.fff}: {message}");
        }

        private static async Task ProcessLogQueue(CancellationToken token)
        {
            using (var streamWriter = new StreamWriter(_filePath, append: true, encoding: Encoding.UTF8) { AutoFlush = true })
            {
                while (!token.IsCancellationRequested || !_logQueue.IsCompleted)
                {
                    if (_logQueue.TryTake(out var logMessage, Timeout.Infinite))
                    {
                        await streamWriter.WriteLineAsync(logMessage);
                    }
                }
            }
        }

        /// <summary>Requests completion of queued logging and then reads the configured log file.</summary>
        /// <remarks>This legacy method waits for the processing task; it is not a non-blocking snapshot of active logging.</remarks>
        /// <returns>A task containing the log file text.</returns>
        public static async Task<string> ReadLogsAsync()
        {
            await Task.Yield(); // Ensures this method can be awaited

            using (var semaphore = new SemaphoreSlim(0, 1))
            {
                _logQueue.Add(null); // Marker to flush and stop processing

                await _logTask; // Wait for the processing task to complete

                using (var reader = new StreamReader(_filePath, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        /// <summary>Stops the logging task and disposes its queue and cancellation resources.</summary>
        public static void Dispose()
        {
            _logQueue.CompleteAdding();
            _cancellationTokenSource.Cancel();
            _logTask.Wait();
            _cancellationTokenSource.Dispose();
            _logQueue.Dispose();
        }
    }
}

