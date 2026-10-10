using IpcNetMq.IpcNetMqHelpers;
using NetMQ;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq
{
    public class IpcServerNetMq : IpcServerBaseNetMq
    {
        public string ServerName { get; set; }
        private bool _awaitingReply;
        private IpcPacket _request;
        public IpcServerNetMq(string name, string address) : base(address) { ServerName = name; }

        public void RunIpcServerLoop(Func<IpcPacket, IpcPacket> handleAction)
            => RunIpcServerLoopOnBackgroundThreadAsync(handleAction).GetAwaiter().GetResult();

        /// <summary>Run until cancellation or disposal. All socket operations stay on its dedicated thread.</summary>
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

        /// <summary>Poll on the owning thread. Invalid messages receive an error and return false.</summary>
        public bool TryGetRequest(out IpcPacket request)
        {
            EnsurePollingReady();
            request = ReceiveRequest(TimeSpan.Zero);
            return request != null;
        }
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
        protected override bool CloseSocket(out string reason)
        {
            var closed = base.CloseSocket(out reason);
            _awaitingReply = false;
            _request = null;
            return closed;
        }
    }
}
