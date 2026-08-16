using System.Text.Json.Serialization;
using System;
using IpcNetMq.IpcNetMqHelpers;

namespace IpcNetMq
{

    /// <summary>
    /// The packet that is transferred between server and client.
    /// It holds whatever is neccessary for the IPC mechanism.
    /// Currently has been tested with:
    /// 1. Python IpcServer
    /// 
    /// Note that this is .NET 6, so json serialization is from System.Text.Json
    /// </summary>
    public class IpcPacket
    {
        /// <summary>
        /// The version is a quick test for the server to know that it
        /// and the client are talking the same language (compatible schemas).
        /// The format is "V", followed by yymmdd. E.g. V240301 for 1-Mar-2024
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "V260107";

        /// <summary>
        /// A unique string ID for the client. It is assigned by the client and should be unique to the Server.
        /// </summary>
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        /// <summary>
        /// An integer to make sure (for a given client) that the
        /// reply is going to the proper request.
        /// Requests are odd, and the reply is the next even
        /// After a connection, the first request is 1, with 2 as the reply.
        /// </summary>
        /// 
        [JsonInclude]
        [JsonPropertyName("sequence_number")]
        public int SequenceNumber { get; private set; }

        internal void SetSequenceNumber(int value)
        {
            SequenceNumber = value;
        }

        /// <summary>
        /// Time Packet was constructed. The world (utc) time in "o" format
        /// </summary>
        [JsonPropertyName("world_time")]
        public string WorldTime { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>
        /// The name of the procedure/method
        /// </summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = "";

        /// <summary>
        /// Status of the call. Empty means unqualified success.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        /// <summary>
        /// Serialized json string containing options such as logging level with Request
        /// Information about IPC transaction, such as server information included with Reply
        /// </summary>
        [JsonPropertyName("options_string")]
        public string OptionsString { get; set; } = "";

        /// <summary>
        /// Serialized json string containing context-information that you can employ
        /// to customize a REQ-REP implementation. For example, including the
        /// simulation time instead of having to pass it as arguments each time.
        /// </summary>
        [JsonPropertyName("context_string")]
        public string ContextString { get; set; } = "";

        /// <summary>
        /// json string for request. Serialized list of name,value pairs.
        /// Note that by convention a reply list is also included in
        /// the request to indicate expectations from the call.
        /// </summary>
        [JsonPropertyName("request_string")]
        public string RequestString { get; set; }

        /// <summary>
        /// json string for reply. Serialized list of name,value pairs
        /// </summary>
        [JsonPropertyName("reply_string")]
        public string ReplyString { get; set; }

        public IpcPacket() { }

        public string Check()
        {
            if (string.IsNullOrEmpty(Action))
                return $"Action is not set.";

            return "OK";
        }

        /// <summary>Override of ToString</summary>
        public override string ToString()
            => $"Packet #[{SequenceNumber}] ClientID={ClientId} Request({RequestString.Length})={RequestString.Trunc(15)}...  Reply({ReplyString.Length})={ReplyString.Trunc(15)}... ";

    } // IpcPacket

} // namespace