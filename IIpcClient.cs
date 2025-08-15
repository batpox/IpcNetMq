using System.Threading.Tasks;

namespace IpcNetMq
{
    public interface IIpcClient
    {
        string ClientName { get; set; }
        string ServerAddress { get; set; }

        bool OpenConnection(out string reason);
        bool CloseConnection(out string reason);

        IpcPacket GetResponsePacket();
        Task<IpcPacket> GetResponsePacketAsync();

        bool PutRequestPacket(IpcPacket packet);
        //bool PutRequestPacketAsync(IpcPacket packet);
        Task<bool> PutRequestPacketAsync(IpcPacket packet);
    }
}