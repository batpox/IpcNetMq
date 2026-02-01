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

        public IpcServerNetMq(string name, string address)
            : base(address)
        {
            ServerName = name;
        }

        /// <summary>
        /// Run the Server loop async, using the provided 'action handler' method,
        /// that will process each incoming request packet and return a reply packet.
        /// </summary>
        /// <param name="handleAction"></param>
        public void RunIpcServerLoop(Func<IpcPacket, IpcPacket> handleAction)
        {
            RunServerLoopAsync(
                () => Task.FromResult(GetRequestPacket()),
                packet =>
                {
                    var reply = handleAction(packet);
                    if (reply != null)
                        PutReplyPacket(reply);
                    return Task.CompletedTask;
                },
                "Sync"
            ).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Todo: Test.
        /// </summary>
        /// <param name="handleAction"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task RunIpcServerLoopOnBackgroundThreadAsync(
            Func<IpcPacket, IpcPacket> handleAction,
            CancellationToken ct = default)
        {
            return Task.Run(() => RunIpcServerLoop(handleAction), ct);
        }

        private IpcPacket GetRequestPacket()
        {
            string json = ServerSocket.ReceiveFrameString();
            return JsonHelpers.DeserializeFromJsonString(json);
        }

        private void PutReplyPacket(IpcPacket packet)
        {
            string json = JsonHelpers.SerializeToJsonString(packet);
            ServerSocket.SendFrame(json);               // keep on the server loop thread (synchronous)
        }


        // ============================================================
        // NEW: POLLING / NON-BLOCKING SERVER API (for Update loops)
        // ============================================================

        private bool _awaitingReply;

        /// <summary>
        /// Non-blocking poll for the next request.
        /// Returns true only when a full request frame was received.
        /// Safe for tight loops (e.g., Stride Update).
        /// </summary>
        public bool TryGetRequest(out IpcPacket request)
        {
            request = null;

            EnsurePollingReady();

            if ( _awaitingReply)
                throw new InvalidOperationException("Must send a reply before receiving the next request.");

            // TimeSpan.Zero => do not block
            if (!ServerSocket.TryReceiveFrameString(TimeSpan.Zero, out var json))
                return false;

            try
            {
                request = JsonHelpers.DeserializeFromJsonString(json);
                if ( request == null)
                    return false;

                _awaitingReply = true;
                return true;
            }
            catch
            {
                // optionally log json length or first N chars
                return false;
            }
        }

        /// <summary>
        /// Send a reply for the most recently received request.
        /// Must be called exactly once for each successful TryGetRequest().
        /// </summary>
        public void SendReply(IpcPacket reply)
        {
            if (reply == null) 
                throw new ArgumentNullException(nameof(reply));

            if (!_awaitingReply)
                throw new InvalidOperationException("SendReply called without a preceding TryGetRequest().");

            string json = JsonHelpers.SerializeToJsonString(reply);
            ServerSocket.SendFrame(json);

            _awaitingReply = false;
        }
    }
}

