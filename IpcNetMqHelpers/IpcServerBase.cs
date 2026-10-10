using NetMQ;
using NetMQ.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{
    /// <summary>Owns a REP socket without terminating other NetMQ connections.</summary>
    public abstract class IpcServerBaseNetMq : IDisposable
    {
        private bool _disposed;
        private readonly object _gate = new object();
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private Thread _loopThread;
        private int _ownerThread;
        private enum ServerMode { None, Polling, Loop }
        private ServerMode _mode;

        /// <summary>The ZeroMQ endpoint that the server binds.</summary>
        protected readonly string ServerAddress;

        /// <summary>The response socket, created, used, and disposed on its owner thread.</summary>
        protected ResponseSocket ServerSocket;

        /// <summary>Indicates whether the current socket successfully bound to the server endpoint.</summary>
        protected bool IsBound;

        /// <summary>Initializes a server base with the required endpoint.</summary>
        /// <param name="serverAddress">The non-empty ZeroMQ bind endpoint.</param>
        /// <exception cref="ArgumentNullException">The endpoint is null, empty, or whitespace.</exception>
        protected IpcServerBaseNetMq(string serverAddress)
        {
            if (string.IsNullOrWhiteSpace(serverAddress)) throw new ArgumentNullException(nameof(serverAddress));
            ServerAddress = serverAddress;
        }

        /// <summary>Writes a server diagnostic message to the console; override to supply another logger.</summary>
        /// <param name="msg">The diagnostic message.</param>
        protected virtual void Logit(string msg) => Console.WriteLine(msg);

        /// <summary>Throws ObjectDisposedException when the server has been disposed.</summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>Checks that the current thread owns the server socket, if one exists.</summary>
        /// <exception cref="InvalidOperationException">The socket belongs to another thread.</exception>
        protected void EnsureSocketOwner()
        {
            if (ServerSocket != null && _ownerThread != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("Polling socket operations and disposal must run on its owning thread.");
        }

        /// <summary>Replaces the current socket and binds a new response socket on the calling thread.</summary>
        /// <remarks>Uses zero linger and disposes partially opened sockets after a bind failure.</remarks>
        /// <param name="reason">An empty string on success, or the bind failure description.</param>
        /// <returns>true if the socket binds; otherwise, false.</returns>
        protected virtual bool OpenSocket(out string reason)
        {
            EnsureSocketOwner();
            CloseSocket(out _);
            reason = "";
            try
            {
                _ownerThread = Environment.CurrentManagedThreadId;
                ServerSocket = new ResponseSocket();
                ServerSocket.Options.Linger = TimeSpan.Zero;
                ServerSocket.Bind(ServerAddress);
                IsBound = true;
                return true;
            }
            catch (Exception ex)
            {
                CloseSocket(out _);
                reason = ex.Message;
                return false;
            }
        }

        /// <summary>Disposes the current response socket on its owner thread and clears binding state.</summary>
        /// <param name="reason">An empty string on success, or the disposal failure description.</param>
        /// <returns>true if the socket closes; otherwise, false.</returns>
        protected virtual bool CloseSocket(out string reason)
        {
            EnsureSocketOwner();
            reason = "";
            var socket = ServerSocket;
            ServerSocket = null;
            IsBound = false;
            try { socket?.Dispose(); return true; }
            catch (Exception ex) { reason = ex.Message; return false; }
        }

        /// <summary>Starts the dedicated server loop without an external cancellation token.</summary>
        /// <remarks>Disposal stops the loop. Delegates must keep all socket operations on the loop thread.</remarks>
        /// <param name="requestFunc">Receives a packet or returns null when no packet is available; must use bounded socket waits.</param>
        /// <param name="replyFunc">Processes and sends the reply to a received packet.</param>
        /// <param name="label">The label included in server diagnostics.</param>
        /// <returns>A task representing the loop lifetime.</returns>
        protected Task RunServerLoopAsync(Func<Task<IpcPacket>> requestFunc,
            Func<IpcPacket, Task> replyFunc, string label)
            => RunServerLoopAsync(requestFunc, replyFunc, label, CancellationToken.None);

        /// <summary>Starts a dedicated socket-owning loop with cancellation and capped bind retries.</summary>
        /// <remarks>Delegates must keep socket operations on the owner thread and return promptly enough to observe cancellation.</remarks>
        /// <param name="requestFunc">Receives a packet or returns null when no packet is available; must use bounded socket waits.</param>
        /// <param name="replyFunc">Processes and sends the reply to a received packet.</param>
        /// <param name="label">The label included in server diagnostics.</param>
        /// <param name="ct">Requests termination of the running loop.</param>
        /// <returns>A task representing the loop lifetime.</returns>
        protected Task RunServerLoopAsync(Func<Task<IpcPacket>> requestFunc,
            Func<IpcPacket, Task> replyFunc, string label, CancellationToken ct)
        {
            if (requestFunc == null) throw new ArgumentNullException(nameof(requestFunc));
            if (replyFunc == null) throw new ArgumentNullException(nameof(replyFunc));
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_mode != ServerMode.None) throw new InvalidOperationException("Polling and loop modes cannot be combined, and only one loop may run.");
                _mode = ServerMode.Loop;
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _loopThread = new Thread(() =>
                {
                    using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token))
                    {
                        var token = cancellation.Token;
                        try
                        {
                            int failures = 0;
                            while (!token.IsCancellationRequested && failures < 10)
                            {
                                try
                                {
                                    if (!OpenSocket(out var reason)) throw new InvalidOperationException(reason);
                                    failures = 0;
                                    while (!token.IsCancellationRequested)
                                    {
                                        var packet = requestFunc().GetAwaiter().GetResult();
                                        if (packet != null) replyFunc(packet).GetAwaiter().GetResult();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    failures++;
                                    Logit($"[{label}] Server error: {ex.Message}");
                                }
                                finally { CloseSocket(out _); }
                                if (!token.IsCancellationRequested)
                                    token.WaitHandle.WaitOne(Math.Min(failures * 1000, 5000));
                            }
                            if (!token.IsCancellationRequested)
                                throw new InvalidOperationException("Server could not recover after ten attempts.");

                        }
                        catch (Exception ex) { completion.TrySetException(ex); }
                        finally
                        {
                            CloseSocket(out _);
                            lock (_gate) { _mode = ServerMode.None; _loopThread = null; }
                            completion.TrySetResult(true);
                        }
                    }
                }) { IsBackground = true, Name = "IpcServerNetMq-IO" };
                _loopThread.Start();
                return completion.Task;
            }
        }

        /// <summary>Creates and binds the caller-owned polling socket if necessary.</summary>
        /// <remarks>All subsequent polling operations and disposal must use this thread. Cannot be called while a background loop is active.</remarks>
        /// <exception cref="InvalidOperationException">Loop mode is active, the socket belongs to another thread, or binding fails.</exception>
        /// <exception cref="ObjectDisposedException">The server has been disposed.</exception>
        public void EnsurePollingReady()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_mode == ServerMode.Loop) throw new InvalidOperationException("Server is running in loop mode; cannot switch to polling.");
                EnsureSocketOwner();
                _mode = ServerMode.Polling;
                if (ServerSocket != null && IsBound) return;
                if (!OpenSocket(out var reason)) throw new InvalidOperationException($"OpenSocket failed: {reason}");
            }
        }

        /// <summary>Stops the server and closes its socket without terminating unrelated NetMQ connections.</summary>
        /// <remarks>Waits for a background loop and its active handler to finish. A polling server must be disposed on its socket owner thread.</remarks>
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

        /// <summary>Requests loop shutdown and releases the owned socket when disposing managed state.</summary>
        /// <remarks>Never invokes process-wide NetMQ cleanup.</remarks>
        /// <param name="disposing">true to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            Thread worker;
            lock (_gate)
            {
                if (_disposed) return;
                if (_mode == ServerMode.Polling) EnsureSocketOwner();
                _disposed = true;
                if (!disposing) return;
                _stop.Cancel();
                worker = _loopThread;
                if (worker == null) CloseSocket(out _);
            }
            if (worker != null && worker != Thread.CurrentThread) worker.Join();
            // NetMQConfig.Cleanup belongs to the application, after all its sockets stop.
        }
    }
}
