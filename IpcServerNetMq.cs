using NetMQ;
using IpcNetMq.IpcNetMqHelpers;
using System;
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

        public void StartIpcServerAsync(Func<IpcPacket, IpcPacket> handleAction)
        {
            var serverTask = RunServerLoopAsync(
                    GetRequestPacketAsync,
                    async packet =>
                    {
                        var reply = handleAction(packet);
                        if (reply != null)
                            await PutResponsePacketAsync(reply).ConfigureAwait(false);
                    },
                    "Async");
        }

        public void RunIpcServerLoop(Func<IpcPacket, IpcPacket> handleAction)
        {
            RunServerLoopAsync(
                () => Task.FromResult(GetRequestPacket()),
                packet =>
                {
                    var reply = handleAction(packet);
                    if (reply != null)
                        PutResponsePacketAsync(reply);
                    return Task.CompletedTask;
                },
                "Sync"
            ).GetAwaiter().GetResult();
        }

        public IpcPacket GetRequestPacket()
        {
            string json = ServerSocket.ReceiveFrameString();
            return JsonHelpers.DeserializeFromJsonString(json);
        }

        public async Task<IpcPacket> GetRequestPacketAsync()
        {
            var tuple = await ServerSocket.ReceiveFrameStringAsync().ConfigureAwait(false);
            var json = tuple.Item1;
            var more = tuple.Item2;

            if (more)
                throw new System.Exception("Multi-frame messages not supported.");

            return JsonHelpers.DeserializeFromJsonString(json);
        }

        public Task PutResponsePacketAsync(IpcPacket packet)
        {
            string json = JsonHelpers.SerializeToJsonString(packet);
            ServerSocket.SendFrame(json);               // keep on the server loop thread
            return Task.CompletedTask;                  // no Task.Run
        }

        ////public Task PutResponsePacketAsync(IpcPacket packet)
        ////{
        ////    return Task.Run(() =>
        ////    {
        ////        string json = JsonHelpers.SerializeToJsonString(packet);
        ////        ServerSocket.SendFrame(json);
        ////    });
        ////}
    }
}
