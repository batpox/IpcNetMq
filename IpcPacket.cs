using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace IpcNetMq
{
    /// <summary>
    /// A standalone test client for testing
    /// the IPC mechanisms.
    /// Currently has been tested with:
    /// 1. Python IpcServer
    /// 
    /// Note that this is .NET 6, so json serialization is from System.Text.Json
    /// </summary>
    public class IpcPacket
    {
        /// <summary>
        /// A constant value that can be used to indicate compatible schemas
        /// The format is "V", followed by yymmdd. E.g. V240301 for 1-Mar-2024
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; } = "V240301";

        /// <summary>
        /// A sequence is a request-response pair
        /// </summary>
        [JsonProperty("sequence_number")]
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Simulation time, which is fractional hours since simulation began
        /// </summary>
        [JsonProperty("sim_time")]
        public double SimTime { get; set; }

        /// <summary>
        /// The world time in "o" format
        /// </summary>
        [JsonProperty("world_time")]
        public string WorldTime { get; set; } = DateTime.UtcNow.ToString("o");

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
        /// json string for request
        /// </summary>
        [JsonProperty("request_string")]
        public string RequestString { get; set; }

        /// <summary>
        /// json string for response
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

        public static string SerializeToJsonString(IpcPacket? packet)
        {
            return JsonConvert.SerializeObject(packet);
        }

        public static IpcPacket? DeserializeFromJsonString(string json)
        {
            return JsonConvert.DeserializeObject<IpcPacket?>(json);
        }

        public static string? SerializePairListToJsonString(List<NameValuePair>? pairList)
        {
            return JsonConvert.SerializeObject(pairList) ;
        }

        public static List<NameValuePair>? DeserializePairListFromJsonString(string? jsonString)
        {
            List<NameValuePair>? pairList;
            try
            {
                pairList = JsonConvert.DeserializeObject<List<NameValuePair>?>(jsonString);
                return pairList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Deserialize PairList from={jsonString} Err={ex.Message}");
            }
        }

        /// <summary>
        /// Serialize a list of name-value pairs into a json string
        /// Used for Requests and Responses
        /// </summary>
        /// <param name="pairList"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string? PutSerializedList(List<NameValuePair>? pairList)
        {
            try
            {
                string? jsonString = JsonConvert.SerializeObject(pairList);
                return jsonString;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Serialize={pairList}. Err={ex.Message}");
            }
        }

    } // IpcPacket

    /// <summary>
    /// Holds the Name, suggested ValueType, and Value 
    /// The value type is N, T, B, or S
    /// </summary>
    public class NameValuePair
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("value")]
        public string Value { get; set; } = "";

        public NameValuePair() { }

        public NameValuePair(string name, string value)
        {
            Name = name;
            Value = value;
        }

    }


} // namespace

