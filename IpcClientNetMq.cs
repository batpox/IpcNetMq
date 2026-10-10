using IpcNetMq.IpcNetMqHelpers;
using IpcNetMqHelpers;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq
{
    /// <summary>A serialized REQ/REP client. Dispatcher sockets belong exclusively to its worker.</summary>
    public class IpcClientNetMq : IIpcClient, IDisposable
    {

        /// <summary>Gets or sets the friendly name used to identify this client.</summary>
        public string ClientName { get; set; } = "DefaultClientName";

        /// <summary>Gets or sets the ZeroMQ server endpoint, such as tcp://127.0.0.1:5555.</summary>
        /// <remarks>Set the address before submitting calls. Changing it does not reconnect an already open socket.</remarks>
        public string ServerAddress { get; set; }

        /// <summary>Gets or sets the logging level; values greater than zero enable client logging.</summary>
        public int LoggingLevel { get; set; }
        private RequestSocket ClientSocket;
        private int OwnerThreadId;
        private int SequenceNbr = -1;
        private bool disposedValue;
        private readonly object _ioLock = new object();
        private readonly BlockingCollection<WorkItem> _queue = new BlockingCollection<WorkItem>(1024);
        private readonly CancellationTokenSource _ioCts = new CancellationTokenSource();
        private Thread _ioThread;
        private bool _manualMode;
        private sealed class WorkItem
        {
            public IpcPacket Request;
            public TaskCompletionSource<IpcPacket> Tcs;
            public TimeSpan SendTimeout, ReceiveTimeout;
            public CancellationToken Token;
            public CancellationTokenRegistration Registration;
        }

        /// <summary>Creates a client for the specified endpoint without opening a socket.</summary>
        /// <param name="clientName">The friendly client name.</param>
        /// <param name="serverAddress">The ZeroMQ endpoint to connect to.</param>
        public IpcClientNetMq(string clientName, string serverAddress)
        {
            ClientName = clientName;
            ServerAddress = serverAddress;
        }

        private void ThrowIfDisposed()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(IpcClientNetMq));
        }

        private void CheckOwner()
        {
            if (ClientSocket != null && OwnerThreadId != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("The socket must be used and closed on its owning thread.");
        }

        private bool OpenSocket(out string reason)
        {
            CheckOwner();
            CloseSocket();
            reason = "";
            try
            {
                if (string.IsNullOrWhiteSpace(ServerAddress)) throw new ArgumentException("A server address is required.");
                OwnerThreadId = Environment.CurrentManagedThreadId;
                ClientSocket = new RequestSocket();
                ClientSocket.Options.Linger = TimeSpan.Zero;
                ClientSocket.Options.SendHighWatermark = 100;
                ClientSocket.Options.ReceiveHighWatermark = 100;
                ClientSocket.Connect(ServerAddress);
                return true;
            }
            catch (Exception ex)
            {
                CloseSocket();
                reason = ex.Message;
                return false;
            }
        }

        private void CloseSocket()
        {
            CheckOwner();
            var socket = ClientSocket;
            ClientSocket = null;
            socket?.Dispose();
        }

        /// <summary>Opens or replaces a legacy connection owned by the calling thread.</summary>
        /// <remarks>Do not mix a legacy connection with dispatcher calls. Its packet operations, close, and disposal must use the same thread.</remarks>
        /// <param name="reason">An empty string on success, or the connection failure description.</param>
        /// <returns>true if the connection opens; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Dispatcher mode is active, or the socket belongs to another thread.</exception>
        /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
        public bool OpenConnection(out string reason)
        {
            lock (_ioLock)
            {
                ThrowIfDisposed();
                if (_ioThread != null) throw new InvalidOperationException("Dispatcher mode owns its connection automatically.");
                _manualMode = true;
                return OpenSocket(out reason);
            }
        }

        /// <summary>Closes the caller-owned legacy connection and allows subsequent dispatcher use.</summary>
        /// <remarks>Use Dispose to close a dispatcher client.</remarks>
        /// <param name="reason">An empty string when the connection closes successfully.</param>
        /// <returns>true when the legacy connection closes.</returns>
        /// <exception cref="InvalidOperationException">Dispatcher mode is active, or the legacy socket belongs to another thread.</exception>
        /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
        public bool CloseConnection(out string reason)
        {
            lock (_ioLock)
            {
                ThrowIfDisposed();
                if (_ioThread != null) throw new InvalidOperationException("Dispose the dispatcher client to close its connection.");
                CheckOwner();
                CloseSocket();
                _manualMode = false;
                reason = "";
                return true;
            }
        }

        private void CheckManual()
        {
            ThrowIfDisposed();
            if (!_manualMode || ClientSocket == null) throw new InvalidOperationException("OpenConnection is required for legacy packet methods.");
            CheckOwner();
        }

        /// <summary>Receives and deserializes a reply on the legacy connection, waiting up to five seconds.</summary>
        /// <remarks>Call on the thread that opened the legacy connection. Prefer CallIpcMethodAsync for new code.</remarks>
        /// <returns>The server reply packet.</returns>
        /// <exception cref="TimeoutException">No reply arrives within the receive timeout.</exception>
        [Obsolete("Use CallIpcMethodAsync instead.")]
        public IpcPacket GetReplyPacket()
        {
            CheckManual();
            try
            {
                if (!ClientSocket.TryReceiveFrameString(TimeSpan.FromSeconds(5), out var json))
                    throw new TimeoutException("Reply timed out.");
                return JsonHelpers.DeserializeFromJsonString(json);
            }
            catch { CloseSocket(); throw; }
        }

        /// <summary>Receives a legacy reply synchronously on the calling thread and wraps it in a completed task.</summary>
        /// <remarks>This obsolete wrapper does not offload socket work. Use CallIpcMethodAsync for asynchronous dispatch.</remarks>
        /// <returns>A completed task containing the reply.</returns>
        [Obsolete("Use CallIpcMethodAsync instead. This legacy wrapper completes on the caller's thread.")]
        public Task<IpcPacket> GetReplyPacketAsync() => Task.FromResult(GetReplyPacket());

        /// <summary>Validates and sends a request on the caller-owned legacy connection.</summary>
        /// <remarks>Waits up to two seconds to send. Call on the thread that opened the legacy connection.</remarks>
        /// <param name="packet">The request packet with a non-empty action.</param>
        /// <returns>true if the request is queued for sending.</returns>
        /// <exception cref="ArgumentNullException">The packet is null.</exception>
        /// <exception cref="ArgumentException">The packet action is empty.</exception>
        /// <exception cref="TimeoutException">The request cannot be sent within two seconds.</exception>
        [Obsolete("Use CallIpcMethodAsync instead.")]
        public bool PutRequestPacket(IpcPacket packet)
        {
            CheckManual();
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            if (packet.Check() != "OK") throw new ArgumentException(packet.Check(), nameof(packet));
            if (!ClientSocket.TrySendFrame(TimeSpan.FromSeconds(2), JsonHelpers.SerializeToJsonString(packet)))
                throw new TimeoutException("Send timed out.");
            return true;
        }

        /// <summary>Sends a legacy request synchronously on the calling thread and wraps the result in a completed task.</summary>
        /// <remarks>This obsolete wrapper does not offload socket work. Use CallIpcMethodAsync for asynchronous dispatch.</remarks>
        /// <param name="packet">The request packet to send.</param>
        /// <returns>A completed task containing the send result.</returns>
        [Obsolete("Use CallIpcMethodAsync instead. This legacy wrapper completes on the caller's thread.")]
        public Task<bool> PutRequestPacketAsync(IpcPacket packet) => Task.FromResult(PutRequestPacket(packet));

        private static void ValidateTimeout(TimeSpan timeout, string name)
        {
            if (timeout < TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(name, "Use a finite non-negative timeout.");
        }

        /// <summary>Queues a request/reply exchange on the dedicated socket-owning worker.</summary>
        /// <remarks>Calls run serially. Failed requests are not replayed automatically. Cancellation cannot undo an action already executed by the server. Positive socket waits use whole milliseconds.</remarks>
        /// <param name="request">The request packet; the worker assigns its odd sequence number.</param>
        /// <param name="sendTimeout">A finite, non-negative send timeout; defaults to two seconds.</param>
        /// <param name="receiveTimeout">A finite, non-negative reply timeout; defaults to five seconds.</param>
        /// <param name="ct">Cancels the call and abandons an incomplete exchange.</param>
        /// <returns>A task containing the correlated reply, or the exchange failure.</returns>
        /// <exception cref="ArgumentNullException">The request is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A timeout is negative or exceeds Int32.MaxValue milliseconds.</exception>
        /// <exception cref="InvalidOperationException">Legacy mode is active, the queue is full, or the reply sequence is invalid.</exception>
        /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
        /// <exception cref="TimeoutException">The send or receive timeout expires.</exception>
        public Task<IpcPacket> CallIpcMethodAsync(IpcPacket request, TimeSpan? sendTimeout = null,
            TimeSpan? receiveTimeout = null, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var send = sendTimeout ?? TimeSpan.FromSeconds(2);
            var receive = receiveTimeout ?? TimeSpan.FromSeconds(5);
            ValidateTimeout(send, nameof(sendTimeout));
            ValidateTimeout(receive, nameof(receiveTimeout));
            lock (_ioLock)
            {
                ThrowIfDisposed();
                if (_manualMode) throw new InvalidOperationException("Close the legacy connection before using dispatcher mode.");
                if (ct.IsCancellationRequested) return Task.FromCanceled<IpcPacket>(ct);
                var wi = new WorkItem { Request = request, SendTimeout = send, ReceiveTimeout = receive,
                    Token = ct, Tcs = new TaskCompletionSource<IpcPacket>(TaskCreationOptions.RunContinuationsAsynchronously) };
                wi.Registration = ct.Register(() => wi.Tcs.TrySetCanceled(ct));
                if (!_queue.TryAdd(wi))
                {
                    wi.Registration.Dispose();
                    throw new InvalidOperationException("The client request queue is full.");
                }
                if (_ioThread == null)
                {
                    _ioThread = new Thread(IoLoop) { IsBackground = true, Name = "IpcClientNetMq-IO" };
                    _ioThread.Start();
                }
                return wi.Tcs.Task;
            }
        }

        private bool WaitFor(Func<TimeSpan, bool> operation, TimeSpan timeout, CancellationToken token)
        {
            var clock = Stopwatch.StartNew();
            do
            {
                _ioCts.Token.ThrowIfCancellationRequested();
                token.ThrowIfCancellationRequested();
                var remaining = timeout - clock.Elapsed;
                var slice = remaining <= TimeSpan.Zero ? TimeSpan.Zero :
                    // NetMQ truncates positive sub-millisecond waits to zero internally;
                    // that path can fail to expire. Use whole milliseconds, or exact zero.
                    TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(25, Math.Ceiling(remaining.TotalMilliseconds))));
                if (operation(slice)) return true;
            } while (clock.Elapsed < timeout);
            return false;
        }


        private void IoLoop()
        {
            Exception failure = null;
            try
            {
                foreach (var wi in _queue.GetConsumingEnumerable(_ioCts.Token))
                {
                    try
                    {
                        wi.Token.ThrowIfCancellationRequested();
                        _ioCts.Token.ThrowIfCancellationRequested();
                        if (ClientSocket == null && !OpenSocket(out var reason)) throw new InvalidOperationException(reason);
                        SequenceNbr = checked(SequenceNbr + 2);
                        wi.Request.SetSequenceNumber(SequenceNbr);
                        var json = JsonHelpers.SerializeToJsonString(wi.Request);
                        if (!WaitFor(slice => ClientSocket.TrySendFrame(slice, json), wi.SendTimeout, wi.Token))
                            throw new TimeoutException("Send timed out.");
                        System.Collections.Generic.List<string> replyFrames = null;
                        if (!WaitFor(slice => ClientSocket.TryReceiveMultipartStrings(slice, ref replyFrames), wi.ReceiveTimeout, wi.Token))
                            throw new TimeoutException("Receive timed out.");
                        if (replyFrames.Count != 1) throw new FormatException("Only single-frame replies are supported.");
                        var reply = JsonHelpers.DeserializeFromJsonString(replyFrames[0]);
                        if (reply == null || reply.SequenceNumber != checked(wi.Request.SequenceNumber + 1))
                            throw new InvalidOperationException("Reply sequence does not match the request.");
                        wi.Tcs.TrySetResult(reply);
                    }
                    catch (OperationCanceledException)
                    {
                        try { CloseSocket(); }
                        finally
                        {
                            if (wi.Token.IsCancellationRequested) wi.Tcs.TrySetCanceled(wi.Token);
                            else wi.Tcs.TrySetException(new ObjectDisposedException(nameof(IpcClientNetMq)));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Abandon an incomplete REQ exchange; never replay a possibly executed action.
                        try { CloseSocket(); }
                        finally { wi.Tcs.TrySetException(ex); }
                    }
                    finally { wi.Registration.Dispose(); }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try { CloseSocket(); }
                catch (Exception ex) { failure = failure ?? ex; }
                lock (_ioLock)
                {
                    if (failure != null) disposedValue = true;
                    _queue.CompleteAdding();
                    while (_queue.TryTake(out var wi))
                    {
                        wi.Registration.Dispose();
                        wi.Tcs.TrySetException(failure ?? new ObjectDisposedException(nameof(IpcClientNetMq)));
                    }
                }
            }
        }

        /// <summary>Queues a request/reply exchange and blocks until it completes.</summary>
        /// <remarks>Uses the same dispatcher and failure behavior as CallIpcMethodAsync.</remarks>
        /// <param name="request">The request packet; the worker assigns its sequence number.</param>
        /// <param name="sendTimeout">A finite, non-negative send timeout; defaults to two seconds.</param>
        /// <param name="receiveTimeout">A finite, non-negative receive timeout; defaults to five seconds.</param>
        /// <returns>The correlated server reply.</returns>
        public IpcPacket CallIpcMethod(IpcPacket request, TimeSpan? sendTimeout = null, TimeSpan? receiveTimeout = null)
            => CallIpcMethodAsync(request, sendTimeout, receiveTimeout).GetAwaiter().GetResult();

        /// <summary>Writes a message to the shared logger and console when LoggingLevel is greater than zero.</summary>
        /// <param name="message">The message to log.</param>
        public void Logit(string message) { if (LoggingLevel > 0) { Logger.LogIt(message); Console.Write(message); } }

        /// <summary>Writes a message to the shared logger when the specified logging level is greater than zero.</summary>
        /// <param name="loggingLevel">A value greater than zero enables logging.</param>
        /// <param name="message">The message to log.</param>
        public static void LogitStatic(int loggingLevel, string message) { if (loggingLevel > 0) Logger.LogIt(message); }

        /// <summary>Releases client resources and stops dispatcher work when disposing managed state.</summary>
        /// <remarks>Dispatcher sockets close on their worker. Legacy connections must be disposed on their owning thread.</remarks>
        /// <param name="disposing">true to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            Thread worker;
            lock (_ioLock)
            {
                if (disposedValue) return;
                if (_manualMode) CheckOwner();
                disposedValue = true;
                if (!disposing) return;
                _ioCts.Cancel();
                _queue.CompleteAdding();
                worker = _ioThread;
                if (worker == null) CloseSocket();
            }
            // Never close a dispatcher socket from the disposing thread.
            if (worker != null && worker != Thread.CurrentThread) worker.Join();
        }

        /// <summary>Stops the dispatcher, closes its socket on the owner thread, and fails pending calls.</summary>
        /// <remarks>Legacy connections must be disposed on the thread that opened them. Instance disposal never terminates the process-wide NetMQ context.</remarks>
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    }
}
