using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace IpcNetMq.IpcNetMqHelpers
{
    public static class JsonHelpers
    {
        public static string SerializeToJsonString(IpcPacket packet)
        {
            return JsonConvert.SerializeObject(packet);
        }

        public static IpcPacket DeserializeFromJsonString(string json)
        {
            return JsonConvert.DeserializeObject<IpcPacket>(json);
        }

        public static string SerializePairListToJsonString(List<NameValuePair> PairList)
        {
            try
            {
                string jsonString = JsonConvert.SerializeObject(PairList.ToArray());
                return jsonString;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Serialize PairList. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Deserialize into a list. The string can be a single object (T)
        /// or an array. Checks for starting with square bracket ('[') to decide.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<T> DeserializePairListFromJsonString<T>(string jsonString)
        {
            try
            {
                // Check if the JSON represents an array or a single object
                if (jsonString.Trim().StartsWith("["))
                {
                    // JSON is an array, deserialize directly into an array
                    T[] pairArray = JsonConvert.DeserializeObject<T[]>(jsonString);
                    return new List<T>(pairArray);
                }
                else
                {
                    if ( string.IsNullOrEmpty(jsonString) )
                    {
                        // empty or null string. Return an empty list.
                        T pair = JsonConvert.DeserializeObject<T>(jsonString);
                        return new List<T>();
                    }
                    else
                    {
                        // JSON is a single object, deserialize into a single instance
                        T pair = JsonConvert.DeserializeObject<T>(jsonString);
                        return new List<T> { pair };
                    }
                }
                //T[] pairArray = JsonConvert.DeserializeObject<T[]>(jsonString);
                //List<T> PairList = new List<T>(pairArray);
                //return PairList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Deserialize PairList from={jsonString} Err={ex.Message}");
            }
        }

        /// <summary>
        /// Method to build a list of name-value pairs directly within the call
        /// and return a serialized json string.
        /// Example call: string result = BuildNameValuePairs( ("name", "Fred"), ("age", "23"), ("city", "franklin"));
        /// </summary>
        /// <param name="pairs"></param>
        /// <returns></returns>
        public static string BuildNameValuePairs(params (string name, string value)[] pairs)
        {
            var nameValuePairs = new Dictionary<string, string>();

            foreach (var pair in pairs)
            {
                nameValuePairs[pair.name] = pair.value;
            }

            // Serialize the dictionary into a JSON string
            return JsonConvert.SerializeObject(nameValuePairs);
        }

    }
}
