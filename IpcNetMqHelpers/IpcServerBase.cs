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
        protected readonly string ServerAddress;
        protected ResponseSocket ServerSocket;
        protected bool IsBound;

        protected IpcServerBaseNetMq(string serverAddress)
        {
            if (string.IsNullOrWhiteSpace(serverAddress)) throw new ArgumentNullException(nameof(serverAddress));
            ServerAddress = serverAddress;
        }
        protected virtual void Logit(string msg) => Console.WriteLine(msg);
        protected void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }
        protected void EnsureSocketOwner()
        {
            if (ServerSocket != null && _ownerThread != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("Polling socket operations and disposal must run on its owning thread.");
        }
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

        protected Task RunServerLoopAsync(Func<Task<IpcPacket>> requestFunc,
            Func<IpcPacket, Task> replyFunc, string label)
            => RunServerLoopAsync(requestFunc, replyFunc, label, CancellationToken.None);

        /// <summary>Run on one dedicated thread. Delegates must keep socket operations on that thread.</summary>
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

        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
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
