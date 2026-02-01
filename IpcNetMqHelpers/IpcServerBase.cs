using NetMQ;
using NetMQ.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{
    /// <summary>
    /// The base class. Provides default Logit, OpenSocket, CloseSocket, and RunServerLoopAsync methods.
    /// </summary>
    public abstract class IpcServerBaseNetMq : IDisposable
    {
        private bool _disposed;

        protected readonly string ServerAddress;
        protected ResponseSocket ServerSocket;
        protected bool IsBound;

        /// <summary>
        /// Construct with a required address. e.g. "tcp://127.0.0.1:5555" loopback for same-machine.
        /// </summary>
        /// <param name="serverAddress"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected IpcServerBaseNetMq(string serverAddress)
        {
            if ( string.IsNullOrWhiteSpace(serverAddress))  
                throw new ArgumentNullException("Server Address cannot be null or empty");

            ServerAddress = serverAddress;
        }

        /// <summary>
        /// A log to the console. Override to implement custom logging.
        /// </summary>
        /// <param name="msg"></param>
        protected virtual void Logit(string msg)
        {
            Console.WriteLine(msg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        protected virtual bool OpenSocket(out string reason)
        {
            reason = null;

            try
            {
                if (ServerSocket != null)
                {
                    CloseSocket(out _);
                }

                ServerSocket = new ResponseSocket();
                ServerSocket.Bind(ServerAddress);
                IsBound = true;

                return true;
            }
            catch (Exception ex)
            {
                reason = $"OpenSocket failed: Err={ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Close the socket if open. Usually done as process exits.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        protected virtual bool CloseSocket(out string reason)
        {
            reason = "";

            try
            {
                if (IsBound && (ServerSocket != null))
                {
                    try { ServerSocket.Unbind(ServerAddress); } catch { /* best-effort */ }
                    try { ServerSocket.Dispose(); } catch { /* best-effort */ }
                    ServerSocket = null;
                    IsBound = false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = $"CloseSocket failed. Err={ex.Message}";
                return false;
            }
        }

        /// <summary>Backward compatible overload </summary>
        protected Task RunServerLoopAsync(
            Func<Task<IpcPacket>> requestFunc,
            Func<IpcPacket, Task> replyFunc,
            string label ) =>
             RunServerLoopAsync(requestFunc, replyFunc, label, CancellationToken.None);
        
        /// <summary>
        /// Runs the main asynchronous server loop, receiving and replying to IPC packets until cancellation is
        /// requested or the maximum number of retries (of opening socket) is reached.
        /// </summary>
        /// <remarks>The server loop attempts to bind and listen for incoming packets, automatically
        /// retrying on failure up to a fixed maximum number of attempts. The loop processes packets serially on a
        /// single thread. If an exception occurs during packet processing or socket operations, the server will log the
        /// error, close the socket, and attempt to restart after a short delay. The operation can be cancelled at any
        /// time via the provided cancellation token.</remarks>
        /// <param name="requestFunc">A delegate that asynchronously receives an incoming IPC packet. Called repeatedly to obtain packets to
        /// process. Must not be null.</param>
        /// <param name="replyFunc">A delegate that asynchronously processes and responds to a received IPC packet. Invoked for each non-null
        /// packet received. Must not be null.</param>
        /// <param name="label">A label used for logging and diagnostic messages to identify the server instance. Cannot be null.</param>
        /// <param name="ct">A cancellation token that can be used to request termination of the server loop.</param>
        /// <returns>A task that represents the lifetime of the server loop operation. The task completes when the loop exits due
        /// to cancellation or after the maximum number of retries.</returns>
        protected Task RunServerLoopAsync(
            Func<Task<IpcPacket>> requestFunc,
            Func<IpcPacket, Task> replyFunc,
            string label,
            CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                string reason = "";
                int retries = 0;
                const int maxRetries = 10;

                while (retries < maxRetries && !ct.IsCancellationRequested)
                {
                    try
                    {
                        retries++;

                        if (OpenSocket(out reason))
                        {
                            retries = 0;
                            Logit($"[{label}] Bound to {ServerAddress}. Listening...");

                            bool keepRunning = true;

                            while (keepRunning && !ct.IsCancellationRequested)
                            {
                                try
                                {
                                    // Single-threaded loop, so all socket I/O stay on this thread
                                    var packet = await requestFunc().ConfigureAwait(false);
                                    if (packet != null)
                                    {
                                        await replyFunc(packet).ConfigureAwait(false);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logit($"[{label}] Read error={ex.Message}");
                                    keepRunning = false;
                                    CloseSocket(out reason);
                                }
                            } // while keeprunning
                        }
                        else
                        {
                            Logit($"[{label}] Retry failed. Reason={reason}. Attempts={retries}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logit($"[{label}] Fatal Err={ex.Message}");
                    }

                    CloseSocket(out reason);

                    if (!ct.IsCancellationRequested)
                    {
                        // capped linear backoff to avoid hot loop
                        var delayMs = Math.Min(1000 * Math.Max(retries, 1), 5000);
                        Logit($"[{label}] Disconnected. Retrying in {delayMs} ms...");
                        try { await Task.Delay(delayMs, ct).ConfigureAwait(false); } catch { }
                    }
                }

                Logit($"[{label}] Exiting after Retries= {maxRetries}.");
            });
        }

        private enum ServerMode { None, Polling, Loop }
        private ServerMode serverMode;

        /// <summary>
        /// Ensures that the server socket is created and bound to the server address, making it ready for polling operations.
        /// Throws an <see cref="InvalidOperationException"/> if the socket cannot be opened.
        /// </summary>
        public void EnsurePollingReady()
        {
            if (serverMode == ServerMode.Loop)
                throw new InvalidOperationException("Server is running in loop mode; cannot switch to polling.");

            try
            {
                serverMode = ServerMode.Polling;

                if ( (ServerSocket != null) && IsBound) // already open
                    return;

                if (!OpenSocket(out var reason))
                    throw new InvalidOperationException($"OpenSocket failed={reason}");

            }
            catch (Exception ex)
            {
                throw new Exception("EnsurePollingReady failed. Err={ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try { CloseSocket(out _); } catch { /* best-effort */ }

                //try { Runtime.Dispose(); } catch { /* best-effort */ }

                // Global cleanup once, on dispose
                try { NetMQConfig.Cleanup(); } catch { /* best-effort */ }
            }

            _disposed = true;
        }
    }
}
