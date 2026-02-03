using System.Threading.Tasks;

namespace IpcNetMq
{
    public interface IIpcClient
    {
        string ClientName { get; set; }
        string ServerAddress { get; set; }

        bool OpenConnection(out string reason);
        bool CloseConnection(out string reason);

        IpcPacket GetReplyPacket();
        Task<IpcPacket> GetReplyPacketAsync();

        bool PutRequestPacket(IpcPacket packet);
        //bool PutRequestPacketAsync(IpcPacket packet);
        Task<bool> PutRequestPacketAsync(IpcPacket packet);
    }
}