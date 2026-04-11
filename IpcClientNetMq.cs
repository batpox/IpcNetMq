using IpcNetMq.IpcNetMqHelpers;
using IpcNetMqHelpers;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq
{
    /// <summary>
    /// A client connection object.
    /// Handles the bind/unbind, open/close, and put/get of packets
    /// </summary>
    public class IpcClientNetMq : IIpcClient, IDisposable
    {

        /// <summary>
        /// The friendly name of the Client
        /// </summary>
        public string ClientName { get; set; } = "DefaultClientName";

        /// <summary>
        /// The client-side uses a Request socket to send requests and receive replies.
        /// </summary>
        private RequestSocket ClientSocket { get; set; }

        /// <summary>
        /// Address of the server
        /// E.g. "tcp://127.0.0.1:5555"
        /// </summary>
        public string ServerAddress { get; set; }

        /// <summary>
        /// Track the binding
        /// </summary>
        private bool disposedValue;

        /// <summary>
        /// The level of logging
        /// </summary>
        public int LoggingLevel { get; set; }

        /// <summary>
        /// The integer that increases for each request sent from this client.
        /// The reply must have the same number.
        /// </summary>
        private int SequenceNbr { get; set; }

        /// <summary>
        /// Set when socke is (re)opened
        /// </summary>
        private int OwnerThreadId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IpcClientNetMq"/> class with the specified client name and server address.
        /// </summary>
        /// <param name="clientName">The friendly name of the client.</param>
        /// <param name="serverAddress">The address of the server (e.g., "tcp://127.0.0.1:5555").</param>
        public IpcClientNetMq(string clientName, string serverAddress)
        {
            SequenceNbr = 0;
            ClientName = clientName;
            ServerAddress = serverAddress;
            ClientSocket = null;
        }

        /// <summary>
        /// Used for the IO Dispatcher
        /// </summary>
        private class WorkItem
        {
            public IpcPacket Request;
            public TaskCompletionSource<IpcPacket> Tcs;
            public TimeSpan SendTimeout;
            public TimeSpan ReceiveTimeout;
            public CancellationTokenRegistration CancellationRegistration;
            public CancellationToken CancellationToken;
        }

        private readonly BlockingCollection<WorkItem> _queue =
            new BlockingCollection<WorkItem>(new ConcurrentQueue<WorkItem>(), 1024);
        private Thread _ioThread;
        private CancellationTokenSource _ioCts;
        private readonly TimeSpan _defaultSend = TimeSpan.FromSeconds(2);
        private readonly TimeSpan _defaultRecv = TimeSpan.FromSeconds(5);

        private readonly object _ioLock = new object();

        // call this before first use
        private void EnsureIoThread()
        {
            if (_ioThread != null) 
                return;

            lock (_ioLock)
            {
                if ((_ioThread != null))
                    return; // double-check to prevent two threads starting it

                _ioCts = new CancellationTokenSource();
                _ioThread = new Thread(IoLoop) { IsBackground = true, Name = "IpcClientNetMq-IO" };
                _ioThread.Start();
            }
        }

        private void IoLoop()
        {
            try
            {
                string reason;
                // open once here so the socket is owned by this thread
                if (!EnsureSocketReady(out reason))
                    throw new InvalidOperationException("Socket not ready: " + reason);

                foreach (var wi in _queue.GetConsumingEnumerable(_ioCts.Token))
                {
                    try
                    {
                        // avoids the subtle race where the token is canceled but Task.IsCanceled hasn’t transitioned yet.
                        if (wi.CancellationToken.IsCancellationRequested)
                        {
                            wi.Tcs.TrySetCanceled(wi.CancellationToken);
                            continue;
                        }

                        // sequence inside this single-threaded loop
                        SequenceNbr++;
                        wi.Request.SequenceNumber = SequenceNbr;

                        var json = JsonHelpers.SerializeToJsonString(wi.Request);

                        // send (timeout + single reconnect)
                        if (!TrySendJson(json, wi.SendTimeout, out reason))
                        {
                            if (!Reconnect(out reason) || !TrySendJson(json, wi.SendTimeout, out reason))
                                throw new TimeoutException($"Send failed={reason}");
                        }

                        // receive
                        if (!ClientSocket.TryReceiveFrameString(wi.ReceiveTimeout, out var replyJson))
                            throw new TimeoutException($"Receive timed out after={wi.ReceiveTimeout.TotalMilliseconds} ms.");

                        var reply = JsonHelpers.DeserializeFromJsonString(replyJson);

                        if (reply == null || reply.SequenceNumber != (wi.Request.SequenceNumber+1))
                            throw new InvalidOperationException(
                                $"Sequence mismatch: req={wi.Request.SequenceNumber}," 
                                + $" resp={(reply == null ? -1 : reply.SequenceNumber)}");
                        
                        wi.Tcs.TrySetResult(reply);
                    }
                    catch (Exception ex)
                    {
                        wi.Tcs.TrySetException(ex);
                    }
                    finally
                    {
                        wi.CancellationRegistration.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                // fail any pending waiters if the loop blows up
                while (_queue.TryTake(out var wi))
                {
                    wi.CancellationRegistration.Dispose();
                    wi.Tcs.TrySetException(ex);
                }
            }
        }

        /// <summary>
        /// Create/connect the request socket if not already created.
        /// If it exists, then Unbind/Close it.
        /// Regardless, attempt to bind to it.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool OpenConnection(out string reason)
        {
            reason = "";
            string marker = "Checking address socket.";

            if (string.IsNullOrEmpty(ServerAddress))
                throw new Exception($"No Server Address is specified");

            try
            {
                marker = "Checking socket.";
                if (ClientSocket != null)
                {
                    try { ClientSocket.Disconnect(ServerAddress); } catch { }
                    try { ClientSocket.Close(); } catch { }
                    try { ClientSocket.Dispose(); } catch { }
                    ClientSocket = null;
                }

                marker = "Creating new socket and connecting";
                ClientSocket = new RequestSocket();

                // Recommended socket options to avoid stalls
                ClientSocket.Options.Linger = TimeSpan.Zero;
                ClientSocket.Options.SendHighWatermark = 100;
                ClientSocket.Options.ReceiveHighWatermark = 100;

                ClientSocket.Connect(ServerAddress);
                OwnerThreadId = Environment.CurrentManagedThreadId; // remember our thread

                return true;
            }
            catch (Exception ex)
            {
                reason = $"IPC Client Connection={ClientName}. Marker={marker}. Err={ex.Message}";
                return false;
            }
        }

        private bool EnsureSocketReady(out string reason)
        {
            reason = "";
            if (ClientSocket != null && !ClientSocket.IsDisposed)
                return true;

            if (!OpenConnection(out reason))
                return false;

            OwnerThreadId = Environment.CurrentManagedThreadId;
            return true;
        }

        private bool Reconnect(out string reason)
        {
            reason = string.Empty;
            CloseConnection(out _);
            var ok = OpenConnection(out reason);
            if (ok) 
                OwnerThreadId = Environment.CurrentManagedThreadId;
            return ok;
        }

        private bool TrySendJson(string json, TimeSpan sendTimeout, out string reason)
        {
            reason = "";

            if (ClientSocket == null) { reason = "Socket is null."; return false; }
            if (ClientSocket.IsDisposed) { reason = "Socket is disposed."; return false; }

            // NetMQ sockets must be used on the thread they were created on
            if (Environment.CurrentManagedThreadId != OwnerThreadId)
            {
                reason = "Socket used from a different thread than it was created on.";
                return false;
            }

            bool ok;
            try { ok = ClientSocket.TrySendFrame(sendTimeout, json); }
            catch (Exception ex)
            {
                reason = $"Send Exception. Err={ex.Message}";
                return false;
            }

            if (!ok)
            {
                reason = $"Send timed out after {sendTimeout.TotalMilliseconds} ms.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Close the connection, unbind if possible
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool CloseConnection(out string reason)
        {
            reason = "";
            try
            {
                if (ClientSocket != null)
                {
                    ClientSocket.Close();
                    ClientSocket.Dispose();
                    ClientSocket = null;
                }
                return true;
            }
            catch (Exception ex)
            {
                reason = $"IPC Connection={ClientName} Address={ServerAddress} cannot close connection. Err={ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Get a reply packet from the Server and deserialize it. Return the deserialized Packet.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Obsolete]
        public IpcPacket GetReplyPacket()
        {
            IpcPacket replyPacket;
            try
            {
                // Assuming the server replies immediately after processing the message
                if (!ClientSocket.TryReceiveFrameString(TimeSpan.FromSeconds(5), out var replyJson))
                    throw new Exception($"Could not receive from Server within the timeout");

                // Deserialize the entire message
                replyPacket = JsonHelpers.DeserializeFromJsonString(replyJson);
                return replyPacket;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Receive Reply. Name={ClientName}. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Get the reply packet and deserialize it. Return the deserialized Packet.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Obsolete]
        public async Task<IpcPacket> GetReplyPacketAsync()
        {
            IpcPacket replyPacket = null;
            try
            {
                // Assuming the server replies immediately after processing the message
                var (replyJson, moreFrames) = await ClientSocket.ReceiveFrameStringAsync();
                if (!moreFrames)
                {
                    // Deserialize the entire message
                    replyPacket = JsonHelpers.DeserializeFromJsonString(replyJson);
                    return replyPacket;
                }
                else
                {
                    throw new Exception($"The async receive indicates more frames. We don't support multi-frame messages, so we'll fail this.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Receive Request. Name={ClientName}. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Send a serialized packet to the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Obsolete]
        public bool PutRequestPacket(IpcPacket packet)
        {
            if (packet == null)
                throw new Exception($"Cannot send a null packet");

            string checkMessage = packet.Check();

            if (checkMessage != "OK")
                throw new Exception($"Invalid packet. Reason={checkMessage}");

            try
            {
                string requestJson = JsonHelpers.SerializeToJsonString(packet);
                Logit($"Sending Packet. Data Bytes={requestJson.Length}. Sending...");
                ClientSocket.SendFrame(requestJson);
                Logit($"Sent Request. Serialized Length={requestJson.Length} bytes. ");

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot SendRequestPacket. Name={ClientName}. Err={ex.Message}");
            }

        }

        /// <summary>
        /// Send a serialized packet to the server in an async fashion.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Obsolete]
        public Task<bool> PutRequestPacketAsync(IpcPacket packet)
        {
            // Offload the synchronous send to the thread pool to avoid blocking the caller.
            return Task.Run(() => PutRequestPacket(packet));
        }

        /// <summary>
        /// Doing an IPC REQ-REP asynchronously.
        /// Include the client object and the request packet.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public Task<IpcPacket> CallIpcMethodAsync(
            IpcPacket request,
            TimeSpan? sendTimeout = null,
            TimeSpan? receiveTimeout = null,
            CancellationToken ct = default)
        {
            if (request is null) 
                throw new ArgumentNullException(nameof(request));

            EnsureIoThread();

            if (ct.IsCancellationRequested)
                return Task.FromCanceled<IpcPacket>(ct);

            var tcs = new TaskCompletionSource<IpcPacket>(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration ctr = default;
            if (ct.CanBeCanceled)
                ctr = ct.Register(() => tcs.TrySetCanceled(ct));

            var wi = new WorkItem
            {
                Request = request,
                Tcs = tcs,
                SendTimeout = sendTimeout ?? _defaultSend,
                ReceiveTimeout = receiveTimeout ?? _defaultRecv,
                CancellationRegistration = ctr,
                CancellationToken = ct
            };

            try
            {
                _queue.Add(wi, ct);
            }
            catch (OperationCanceledException)
            {
                ctr.Dispose();
                return Task.FromCanceled<IpcPacket>(ct);
            }

            return tcs.Task;
        }

        public IpcPacket CallIpcMethod(IpcPacket request, TimeSpan? sendTimeout = null, TimeSpan? receiveTimeout = null)
        {
            // block on the single implementation
            return CallIpcMethodAsync(request, sendTimeout, receiveTimeout).GetAwaiter().GetResult();
        }


        public void Logit(string message)
        {
            if (LoggingLevel > 0)
            {
                Logger.LogIt(message);
                Console.Write(message);
            }
        }
        public static void LogitStatic(int loggingLevel, string message)
        {
            if (loggingLevel > 0)
                Logger.LogIt(message);
        }

        private void StopIoThread()
        {
            try { _ioCts?.Cancel(); } catch { }
            try { _queue?.CompleteAdding(); } catch { }
            try { _ioThread?.Join(1500); } catch { }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    StopIoThread();
                    // Dispose managed state (managed objects)
                    CloseConnection(out _);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~IpcClientNetMq()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
