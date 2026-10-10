using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IpcNetMq
{

    /// <summary>Defines the legacy packet-oriented server contract.</summary>
    /// <remarks>The current IpcServerNetMq class exposes polling and background-loop APIs separately and does not implement this interface.</remarks>
    public interface IIpcServer
    {

        /// <summary>Gets or sets the friendly server name.</summary>
        string ServerName { get; set; }

        /// <summary>Gets or sets the bind endpoint.</summary>
        string ServerAddress { get; set; }

        /// <summary>Closes the response socket.</summary>
        /// <param name="reason">Receives a failure description, or an empty string on success.</param>
        /// <returns>true on success; otherwise, false.</returns>
        bool CloseSocket(out string reason);

        /// <summary>Opens and binds the response socket.</summary>
        /// <param name="reason">Receives a failure description, or an empty string on success.</param>
        /// <returns>true on success; otherwise, false.</returns>
        bool OpenSocket(out string reason);

        /// <summary>Sends a reply for the current request.</summary>
        /// <param name="packet">The reply to send.</param>
        /// <returns>true on success; otherwise, false.</returns>
        bool PutReplyPacket(IpcPacket packet);

        /// <summary>Sends a reply for the current request through the implementation's task-based API.</summary>
        /// <param name="packet">The reply to send.</param>
        /// <returns>A task containing the send result.</returns>
        Task<bool> PutReplyPacketAsync(IpcPacket packet);

        /// <summary>Receives and deserializes the next client request.</summary>
        /// <returns>The request packet.</returns>
        IpcPacket GetRequestPacket();

        /// <summary>Receives the next request through the implementation's task-based API.</summary>
        /// <returns>A task containing the deserialized request.</returns>
        Task<IpcPacket> GetRequestPacketAsync();
    }
}
