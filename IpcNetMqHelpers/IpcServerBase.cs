using NetMQ;
using NetMQ.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{
    public abstract class IpcServerBaseNetMq : IDisposable
    {
        private bool _disposed;
        //protected static readonly Mutex ReqRepMutex = new Mutex();

        protected readonly string ServerAddress;
        protected ResponseSocket ServerSocket;
        protected bool IsBound;

        //protected readonly NetMQRuntime Runtime;

        protected IpcServerBaseNetMq(string serverAddress)
        {
            ServerAddress = serverAddress;
            //Runtime = new NetMQRuntime();
        }

        protected virtual void Logit(string msg)
        {
            Console.WriteLine(msg);
        }

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
            Func<Task<IpcPacket>> receiveFunc,
            Func<IpcPacket, Task> respondFunc,
            string label ) =>
             RunServerLoopAsync(receiveFunc, respondFunc, label, CancellationToken.None);
        
        protected Task RunServerLoopAsync(
            Func<Task<IpcPacket>> receiveFunc,
            Func<IpcPacket, Task> respondFunc,
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
                                    var packet = await receiveFunc().ConfigureAwait(false);
                                    if (packet != null)
                                    {
                                        await respondFunc(packet).ConfigureAwait(false);
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
        private ServerMode _mode;

        /// <summary>
        /// Ensures that the server socket is created and bound to the server address, making it ready for polling operations.
        /// Throws an <see cref="InvalidOperationException"/> if the socket cannot be opened.
        /// </summary>
        public void EnsurePollingReady()
        {
            if (_mode == ServerMode.Loop)
                throw new InvalidOperationException("Server is running in loop mode; cannot switch to polling.");

            _mode = ServerMode.Polling;

            if (ServerSocket != null && IsBound) 
                return;

            if (!OpenSocket(out var reason))
                throw new InvalidOperationException($"OpenSocket failed: {reason}");
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
