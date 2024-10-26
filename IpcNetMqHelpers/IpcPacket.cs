using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;

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
        [JsonProperty("version")]
        public string Version { get; set; } = "V241007";

        /// <summary>
        /// A unique string ID for the client.
        /// You decide how to do this but examples would be time-based, guid, etc.
        /// The server is stateless, so this is for debugging purposes only.
        /// </summary>
        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        /// <summary>
        /// An integer to make sure (for a given client) that the
        /// response is going to the proper request.
        /// Requests are odd, and the response is the next even
        /// After a connection, the first request is 1, with 2 as the response.
        /// </summary>
        [JsonProperty("sequence_number")]
        public int SequenceNumber { get; set; }

        /// <summary>
        /// The world time in "o" format
        /// </summary>
        [JsonProperty("utc_time")]
        public string UtcTime { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>
        /// The name of the procedure/method
        /// </summary>
        [JsonProperty("action")]
        public string Action { get; set; } = "";

        /// <summary>
        /// Status of the call. Empty means unqualified success.
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; } = "";

        /// <summary>
        /// Serialized json string containing options such as logging level with Request
        /// Information about IPC transaction, such as server information included with Response
        /// </summary>
        [JsonProperty("options_string")]
        public string OptionsString { get; set; } = "";

        /// <summary>
        /// Serialized json string containing user-information that you can employ
        /// to customize a REQ-REP implementation.
        /// </summary>
        [JsonProperty("user_string")]
        public string UserString { get; set; } = "";

        /// <summary>
        /// json string for request. Serialized name,value pairs
        /// </summary>
        [JsonProperty("request_string")]
        public string RequestString { get; set; }

        /// <summary>
        /// json string for response. Serialized list of name,value pairs
        /// </summary>
        [JsonProperty("response_string")]
        public string ResponseString { get; set; }

        public IpcPacket() { }

        public string Check()
        {
            if (string.IsNullOrEmpty(Action))
                return $"Action is not set.";

            return "OK";
        }

        public override string ToString()
        {
            return $"Packet #[{SequenceNumber}] ClientID={ClientId} RequestSize={RequestString.Length} ResponseSize={ResponseString.Length}";
        }
        ////public static string SerializeToJsonString(IpcPacket packet)
        ////{
        ////    return JsonConvert.SerializeObject(packet);
        ////}

        ////public static IpcPacket DeserializeFromJsonString(string json)
        ////{
        ////    return JsonConvert.DeserializeObject<IpcPacket>(json);
        ////}

        ////public static string SerializePairListToJsonString(List<NameValuePair> PairList)
        ////{
        ////    try
        ////    {
        ////        string jsonString = JsonConvert.SerializeObject(PairList.ToArray());
        ////        return jsonString;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        throw new Exception($"Cannot Serialize PairList. Err={ex.Message}");
        ////    }
        ////}

        ////public static List<T> DeserializePairListFromJsonString<T>(string jsonString)
        ////{
        ////    try
        ////    {
        ////        T[] pairArray = JsonConvert.DeserializeObject<T[]>(jsonString);
        ////        List<T> PairList = new List<T>(pairArray);
        ////        return PairList;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        throw new Exception($"Cannot Deserialize PairList from={jsonString} Err={ex.Message}");
        ////    }
        ////}

        /////// <summary>
        /////// Create/bind the response socket if not already created.
        /////// If it exists, then Unbind/Close it.
        /////// Regardless, attempt to bind to it.
        /////// </summary>
        /////// <param name="reason"></param>
        /////// <returns></returns>
        ////public bool OpenConnection(out string reason)
        ////{
        ////    reason = "";
        ////    string marker = "Checking socket.";
        ////    try
        ////    {
        ////        if (ResponseSocket == null)
        ////        {
        ////            marker = "Creating new socket";
        ////            ResponseSocket = new ResponseSocket();
        ////        }
        ////        else
        ////        {
        ////            marker = "Unbinding socket";
        ////            if (UnbindSocket(out reason))
        ////            {
        ////                marker = "Closing socket";
        ////                ResponseSocket.Close();
        ////            }
        ////        }

        ////        ResponseSocket.Bind(Address);
        ////        return true;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        reason = $"IPC Connection={this.ConnectionName}. Marker={marker}. Err={ex.Message}";
        ////        return false;
        ////    }

        ////}

        /////// <summary>
        /////// Get a packet and deserialize it. Return the deserialized Packet.
        /////// </summary>
        /////// <returns></returns>
        ////public static IpcPacket GetResponsePacket(ResponseSocket responseSocket)
        ////{
        ////    IpcPacket receivedPacket;
        ////    string marker = string.Empty;
        ////    try
        ////    {
        ////        marker = $"Get with ReceiveFrameString()";
        ////        // processing the received packet, which is in json serialized form
        ////        string receivedJson = responseSocket.ReceiveFrameString();

        ////        marker = "Deserialize from Json";
        ////        receivedPacket = JsonHelpers.DeserializeFromJsonString(receivedJson);

        ////        return receivedPacket;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        throw new Exception($"Cannot Receive. Marker={marker}. Err={ex.Message}");
        ////    }
        ////}

        /////// <summary>
        /////// Serialize a list of name-value pairs into a json string
        /////// Used for Requests and Responses
        /////// </summary>
        /////// <param name="PairList"></param>
        /////// <returns></returns>
        /////// <exception cref="Exception"></exception>
        ////public static string PutSerializedList(List<NameValuePair> PairList)
        ////{
        ////    try
        ////    {
        ////        string jsonString = JsonConvert.SerializeObject(PairList);
        ////        return jsonString;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        throw new Exception($"Cannot Serialize. Err={ex} Data=[{PairList}].");
        ////    }
        ////}

    } // IpcPacket


} // namespace