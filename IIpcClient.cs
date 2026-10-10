using System.Threading.Tasks;

namespace IpcNetMq
{

    /// <summary>Defines the legacy packet-oriented client contract.</summary>
    public interface IIpcClient
    {

        /// <summary>Gets or sets the friendly client name.</summary>
        string ClientName { get; set; }

        /// <summary>Gets or sets the server endpoint.</summary>
        string ServerAddress { get; set; }

        /// <summary>Opens the legacy connection.</summary>
        /// <param name="reason">Receives a connection failure description, or an empty string on success.</param>
        /// <returns>true on success; otherwise, false.</returns>
        bool OpenConnection(out string reason);

        /// <summary>Closes the legacy connection.</summary>
        /// <param name="reason">Receives a close failure description, or an empty string on success.</param>
        /// <returns>true on success; otherwise, false.</returns>
        bool CloseConnection(out string reason);

        /// <summary>Receives the server reply packet.</summary>
        /// <returns>The deserialized reply packet.</returns>
        IpcPacket GetReplyPacket();

        /// <summary>Returns a task containing the server reply packet.</summary>
        /// <remarks>Implementations may complete socket work synchronously; observe their thread ownership requirements.</remarks>
        /// <returns>A task containing the reply.</returns>
        Task<IpcPacket> GetReplyPacketAsync();

        /// <summary>Sends a request packet to the server.</summary>
        /// <param name="packet">The request to send.</param>
        /// <returns>true on success; otherwise, false.</returns>
        bool PutRequestPacket(IpcPacket packet);
        //bool PutRequestPacketAsync(IpcPacket packet);

        /// <summary>Returns a task containing the result of sending a request.</summary>
        /// <remarks>Implementations may complete socket work synchronously; observe their thread ownership requirements.</remarks>
        /// <param name="packet">The request to send.</param>
        /// <returns>A task containing the send result.</returns>
        Task<bool> PutRequestPacketAsync(IpcPacket packet);
    }
}
