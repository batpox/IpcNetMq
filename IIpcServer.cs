using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IpcNetMq;
using IpcNetMq.IpcNetMqHelpers;

namespace IpcNetMq
{
    public interface IIpcServer
    {
        string ServerName { get; set; }
        string ServerAddress { get; set; }

        bool CloseSocket(out string reason);
        bool OpenSocket(out string reason);
        bool PutReplyPacket(IpcPacket packet);
        Task<bool> PutReplyPacketAsync(IpcPacket packet);
        IpcPacket GetRequestPacket();
        Task<IpcPacket> GetRequestPacketAsync();
    }
}