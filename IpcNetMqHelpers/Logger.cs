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

        public static DateTime Timestamp { get; private set; }
        private static DateTime lastTimestamp = DateTime.Now;

        static Logger()
        {
            Timestamp = DateTime.Now;
            _logQueue.Add($"********** New Day: {Timestamp}");
        }

        public static void Initialize(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                filePath = Path.Combine(folderPath, "IpcNetMqServer.log");
            }

            _filePath = filePath;
        }

        /// <summary>
        /// Add to the log. If the day changed, a special date message is writtem.
        /// </summary>
        /// <param name="message"></param>
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

        /// <summary>
        /// Read logs asynchronously
        /// </summary>
        /// <returns></returns>
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

