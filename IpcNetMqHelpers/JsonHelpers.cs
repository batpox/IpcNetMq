using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IpcNetMq.IpcNetMqHelpers
{

    /// <summary>Provides packet serialization and tolerant list payload helpers.</summary>
    public static class JsonHelpers
    {
        // Shared options (tweak as you like)
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null
        };

        /// <summary>Reads an array, single object, or once-double-encoded JSON payload as a list.</summary>
        /// <typeparam name="T">The item type to deserialize.</typeparam>
        /// <param name="jsonString">The JSON payload; null, empty, or whitespace produces an empty list.</param>
        /// <returns>The deserialized items, or an empty list for an empty or null JSON payload.</returns>
        public static List<T> DeserializeListFromJsonString<T>(string jsonString)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonString))
                    return new List<T>();

                var s = jsonString.Trim();

                // If double-encoded (JSON string containing JSON), unwrap once.
                if (s.Length >= 2 && s[0] == '"' && s[s.Length-1] == '"')
                {
                    s = JsonSerializer.Deserialize<string>(s, JsonOpts) ?? "";
                    s = s.Trim();
                    if (string.IsNullOrWhiteSpace(s))
                        return new List<T>();
                }

                if (s.StartsWith("["))
                {
                    var arr = JsonSerializer.Deserialize<T[]>(s, JsonOpts);
                    return arr is null ? new List<T>() : new List<T>(arr);
                }
                else
                {
                    var obj = JsonSerializer.Deserialize<T>(s, JsonOpts);
                    return obj == null ? new List<T>() : new List<T> { obj };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Deserialize list from json='{jsonString}'. Err={ex.Message}", ex);
            }
        }

        /// <summary>Serializes an IPC packet using its declared wire property names.</summary>
        /// <param name="packet">The non-null packet to serialize.</param>
        /// <returns>The packet as a JSON object.</returns>
        public static string SerializeToJsonString(IpcPacket packet)
        {
            if (packet == null) 
                throw new ArgumentNullException($"Null IpcPacket");

            try
            {
                return JsonSerializer.Serialize(packet, JsonOpts);
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot serialize IpcPacket={packet}. Err={ex.Message}");
            }
        }

        /// <summary>Deserializes a JSON object into an IPC packet.</summary>
        /// <param name="json">The non-empty JSON packet string.</param>
        /// <returns>The deserialized packet; a null result is rejected.</returns>
        public static IpcPacket DeserializeFromJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string is null/empty.", nameof(json));

            var packet = JsonSerializer.Deserialize<IpcPacket>(json, JsonOpts);
            if (packet == null)
                throw new Exception("Cannot deserialize IpcPacket (result was null).");

            return packet;
        }

        /// <summary>Serializes name/value pairs as the canonical inner JSON array.</summary>
        /// <param name="pairList">The pairs to serialize, or null for an empty array.</param>
        /// <returns>The serialized JSON array.</returns>
        public static string SerializePairListToJsonString(List<NameValuePair> pairList)
        {
            try
            {
                // Serialize the list directly (no need to ToArray()).
                return JsonSerializer.Serialize(pairList ?? new List<NameValuePair>(), JsonOpts);
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Serialize PairList. Err={ex.Message}", ex);
            }
        }

        /// <summary>Reads a name/value payload as a list, accepting arrays, single objects, and once-double-encoded JSON.</summary>
        /// <typeparam name="T">The item type to deserialize.</typeparam>
        /// <param name="jsonString">The JSON payload; null, empty, or whitespace produces an empty list.</param>
        /// <returns>The deserialized items, or an empty list for an empty or null JSON payload.</returns>
        public static List<T> DeserializePairListFromJsonString<T>(string jsonString)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonString))
                    return new List<T>();

                string s = jsonString.Trim();

                // If double-encoded (e.g. "\"[{\\\"name\\\":...}]\""), unwrap once.
                if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                {
                    s = JsonSerializer.Deserialize<string>(s, JsonOpts) ?? "";
                    s = s.Trim();

                    if (string.IsNullOrWhiteSpace(s))
                        return new List<T>();
                }

                // Array vs single object
                if (s.StartsWith("["))
                {
                    var list = JsonSerializer.Deserialize<List<T>>(s, JsonOpts);
                    return list ?? new List<T>();
                }
                else
                {
                    var obj = JsonSerializer.Deserialize<T>(s, JsonOpts);
                    return obj == null ? new List<T>() : new List<T> { obj };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Deserialize PairList from={jsonString} Err={ex.Message}", ex);
            }
        }

        /// <summary>Serializes name/value tuples as a legacy JSON dictionary.</summary>
        /// <remarks>For new packet payloads, prefer a JSON array of NameValuePair objects.</remarks>
        /// <param name="pairs">The pairs to convert; later duplicate names replace earlier values.</param>
        /// <returns>The serialized dictionary, or an empty JSON object when no pairs are supplied.</returns>
        public static string BuildNameValuePairs(params (string name, string value)[] pairs)
        {
            var dict = new Dictionary<string, string>();

            if (pairs != null)
            {
                foreach (var pair in pairs)
                    dict[pair.name] = pair.value;
            }

            return JsonSerializer.Serialize(dict, JsonOpts);
        }
    


        //// /////////////////////////////////////////////////
        //// </summary>
        //// <param name="packet"></param>
        //// <returns></returns>
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

        //// <summary>
        //// Deserialize into a list. The string can be a single object (T)
        //// or an array. Checks for starting with square bracket ('[') to decide.
        //// </summary>
        //// <typeparam name="T"></typeparam>
        //// <param name="jsonString"></param>
        //// <returns></returns>
        //// <exception cref="Exception"></exception>
        ////public static List<T> DeserializePairListFromJsonString<T>(string jsonString)
        ////{
        ////    try
        ////    {
        ////        // Check if the JSON represents an array or a single object
        ////        if (jsonString.Trim().StartsWith("["))
        ////        {
        ////            // JSON is an array, deserialize directly into an array
        ////            T[] pairArray = JsonConvert.DeserializeObject<T[]>(jsonString);
        ////            return new List<T>(pairArray);
        ////        }
        ////        else
        ////        {
        ////            if ( string.IsNullOrEmpty(jsonString) )
        ////            {
        ////                // empty or null string. Return an empty list.
        ////                T pair = JsonConvert.DeserializeObject<T>(jsonString);
        ////                return new List<T>();
        ////            }
        ////            else
        ////            {
        ////                // JSON is a single object, deserialize into a single instance
        ////                T pair = JsonConvert.DeserializeObject<T>(jsonString);
        ////                return new List<T> { pair };
        ////            }
        ////        }
        ////        //T[] pairArray = JsonConvert.DeserializeObject<T[]>(jsonString);
        ////        //List<T> PairList = new List<T>(pairArray);
        ////        //return PairList;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        throw new Exception($"Cannot Deserialize PairList from={jsonString} Err={ex.Message}");
        ////    }
        ////}

        //// <summary>
        //// Method to build a list of name-value pairs directly within the call
        //// and return a serialized json string.
        //// Example call: string result = BuildNameValuePairs( ("name", "Fred"), ("age", "23"), ("city", "franklin"));
        //// </summary>
        //// <param name="pairs"></param>
        //// <returns></returns>
        ////public static string BuildNameValuePairs(params (string name, string value)[] pairs)
        ////{
        ////    var nameValuePairs = new Dictionary<string, string>();

        ////    foreach (var pair in pairs)
        ////    {
        ////        nameValuePairs[pair.name] = pair.value;
        ////    }

        ////    // Serialize the dictionary into a JSON string
        ////    string ss =  JsonConvert.SerializeObject(nameValuePairs);
        ////    return ss;
        ////}

    }
}
