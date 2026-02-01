
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IpcNetMq.IpcNetMqHelpers
{
    public static class JsonHelpers
    {
        // Shared options (tweak as you like)
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null
        };



        /// <summary>
        /// Deserialize into a list. The string can be:
        /// - null/empty/whitespace -> empty list
        /// - a single object: {...}
        /// - an array: [...]
        /// - double-encoded JSON string: "\"[...]\"" or "\"{...}\""
        /// </summary>
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


        /// <summary>
        /// Serialize an IpcPacket to JSON string (outer packet).
        /// </summary>
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

        /// <summary>
        /// Deserialize an IpcPacket from JSON string (outer packet).
        /// </summary>
        public static IpcPacket DeserializeFromJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string is null/empty.", nameof(json));

            var packet = JsonSerializer.Deserialize<IpcPacket>(json, JsonOpts);
            if (packet == null)
                throw new Exception("Cannot deserialize IpcPacket (result was null).");

            return packet;
        }

        /// <summary>
        /// Serialize a list of NameValuePair to JSON string (inner payload).
        /// Canonical form is a JSON array: [{ "name": "...", "value": "..." }, ...]
        /// </summary>
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

        /// <summary>
        /// Deserialize into a list. Accepts:
        /// - null/empty/whitespace => empty list
        /// - JSON array => List<T>
        /// - single JSON object => List<T> with one item
        /// - double-encoded JSON text (JSON string containing JSON) => unwrap then parse
        /// </summary>
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

        /// <summary>
        /// Build a dictionary from name/value tuples and serialize it as JSON string.
        /// (Legacy helper; your new preferred wire format is List&lt;NameValuePair&gt;.)
        /// </summary>
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
    


        /////// //////////////////////////////////////////////////////////////////////////////////
        /////// </summary>
        /////// <param name="packet"></param>
        /////// <returns></returns>
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

        /////// <summary>
        /////// Deserialize into a list. The string can be a single object (T)
        /////// or an array. Checks for starting with square bracket ('[') to decide.
        /////// </summary>
        /////// <typeparam name="T"></typeparam>
        /////// <param name="jsonString"></param>
        /////// <returns></returns>
        /////// <exception cref="Exception"></exception>
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

        /////// <summary>
        /////// Method to build a list of name-value pairs directly within the call
        /////// and return a serialized json string.
        /////// Example call: string result = BuildNameValuePairs( ("name", "Fred"), ("age", "23"), ("city", "franklin"));
        /////// </summary>
        /////// <param name="pairs"></param>
        /////// <returns></returns>
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
