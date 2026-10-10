using IpcNetMq.IpcNetMqHelpers;
using NetMQ;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq
{

    /// <summary>Processes single-frame JSON request/reply exchanges in background-loop or caller-owned polling mode.</summary>
    public class IpcServerNetMq : IpcServerBaseNetMq
    {

        /// <summary>Gets or sets the friendly server name.</summary>
        public string ServerName { get; set; }
        private bool _awaitingReply;
        private IpcPacket _request;

        /// <summary>Creates a server for the specified endpoint without binding its socket.</summary>
        /// <param name="name">The friendly server name.</param>
        /// <param name="address">The ZeroMQ endpoint to bind, such as tcp://127.0.0.1:5555.</param>
        public IpcServerNetMq(string name, string address) : base(address) { ServerName = name; }

        /// <summary>Runs serial request processing on a dedicated thread and blocks until the server stops.</summary>
        /// <remarks>Dispose the server to stop this blocking call. Handler failures produce correlated error replies; handlers must return before shutdown can finish.</remarks>
        /// <param name="handleAction">Handles each request and returns its reply.</param>
        public void RunIpcServerLoop(Func<IpcPacket, IpcPacket> handleAction)
            => RunIpcServerLoopOnBackgroundThreadAsync(handleAction).GetAwaiter().GetResult();

        /// <summary>Starts serial request processing on a dedicated socket-owning thread.</summary>
        /// <remarks>Cancellation or disposal stops idle polling. An active handler must return before shutdown can finish. Polling and loop modes cannot be mixed.</remarks>
        /// <param name="handleAction">Handles each request and returns its reply.</param>
        /// <param name="ct">Requests shutdown of the running loop.</param>
        /// <returns>A task representing the server loop lifetime.</returns>
        public Task RunIpcServerLoopOnBackgroundThreadAsync(Func<IpcPacket, IpcPacket> handleAction,
            CancellationToken ct = default)
        {
            if (handleAction == null) throw new ArgumentNullException(nameof(handleAction));
            return RunServerLoopAsync(
                () => Task.FromResult(ReceiveRequest(TimeSpan.FromMilliseconds(25))),
                request =>
                {
                    IpcPacket reply;
                    try { reply = handleAction(request) ?? throw new InvalidOperationException("The action handler returned null."); }
                    catch (Exception ex) { reply = new IpcPacket { Status = "error: " + ex.Message, Action = request.Action }; }
                    SendReplyCore(reply);
                    return Task.CompletedTask;
                }, "Background", ct);
        }

        private IpcPacket ReceiveRequest(TimeSpan timeout)
        {
            if (_awaitingReply) throw new InvalidOperationException("Must send a reply before receiving the next request.");
            // Consume the entire message so unsupported multipart input cannot wedge REP.
            System.Collections.Generic.List<string> frames = null;
            if (!ServerSocket.TryReceiveMultipartStrings(timeout, ref frames)) return null;
            try
            {
                if (frames.Count != 1) throw new FormatException("Only single-frame requests are supported.");
                _request = JsonHelpers.DeserializeFromJsonString(frames[0]);
                if (_request == null) throw new FormatException("A request packet is required.");
                if (_request.SequenceNumber == int.MaxValue) throw new FormatException("Request sequence cannot be incremented.");
                _awaitingReply = true;
                return _request;
            }
            catch (Exception ex)
            {
                // Every received REP message needs a reply, including malformed input.
                _request = new IpcPacket();
                _awaitingReply = true;
                SendReplyCore(new IpcPacket { Status = "error: invalid request: " + ex.Message });
                return null;
            }
        }

        /// <summary>Polls the next request without blocking on the server socket.</summary>
        /// <remarks>Call on the polling socket owner thread. Invalid or multipart messages receive an error reply. Each successful call requires exactly one SendReply call.</remarks>
        /// <param name="request">The received packet, or null when no valid request is available.</param>
        /// <returns>true when a valid request is received; otherwise, false.</returns>
        public bool TryGetRequest(out IpcPacket request)
        {
            EnsurePollingReady();
            request = ReceiveRequest(TimeSpan.Zero);
            return request != null;
        }

        /// <summary>Sends a reply for the most recent polling request, copying its client ID and incrementing its sequence.</summary>
        /// <remarks>Call on the polling socket owner thread. Sending waits at most two seconds; a failed send closes the socket.</remarks>
        /// <param name="reply">The reply packet to send.</param>
        /// <exception cref="ArgumentNullException">The reply is null.</exception>
        /// <exception cref="InvalidOperationException">No request is awaiting a reply, or the socket belongs to another thread.</exception>
        /// <exception cref="TimeoutException">The reply cannot be sent within two seconds.</exception>
        public void SendReply(IpcPacket reply)
        {
            ThrowIfDisposed();
            EnsureSocketOwner();
            SendReplyCore(reply);
        }
        private void SendReplyCore(IpcPacket reply)
        {
            if (reply == null) throw new ArgumentNullException(nameof(reply));
            if (!_awaitingReply) throw new InvalidOperationException("SendReply requires a preceding request.");
            reply.SetSequenceNumber(checked(_request.SequenceNumber + 1));
            reply.ClientId = _request.ClientId;
            try
            {
                if (!ServerSocket.TrySendFrame(TimeSpan.FromSeconds(2), JsonHelpers.SerializeToJsonString(reply)))
                    throw new TimeoutException("Reply send timed out.");
            }
            catch { CloseSocket(out _); throw; }
            finally { _awaitingReply = false; _request = null; }
        }

        /// <summary>Closes the owned socket and clears any pending request/reply state.</summary>
        /// <param name="reason">An empty string on success, or the close failure description.</param>
        /// <returns>true if the socket closes; otherwise, false.</returns>
        protected override bool CloseSocket(out string reason)
        {
            var closed = base.CloseSocket(out reason);
            _awaitingReply = false;
            _request = null;
            return closed;
        }
    }
}
