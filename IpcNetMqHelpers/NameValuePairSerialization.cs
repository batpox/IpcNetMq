using IpcNetMq.IpcNetMqHelpers;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace IpcNetMq.IpcNetMqHelpers
{

    /// <summary>Serializes and reads name/value JSON payloads stored inside IPC packets.</summary>
    public static class NameValuePairJson
    {

        /// <summary>Serializes name/value pairs as a JSON array for a packet request or reply payload.</summary>
        /// <param name="pairs">The pairs to serialize, or null for an empty array.</param>
        /// <returns>The compact JSON array.</returns>
        public static string SerializeList(List<NameValuePair> pairs)
            => JsonSerializer.Serialize(pairs ?? new List<NameValuePair>(), IpcJson.Options);

        /// <summary>Reads a JSON array, single object, or once-double-encoded payload as a list.</summary>
        /// <typeparam name="T">The item type to deserialize.</typeparam>
        /// <param name="jsonText">The JSON payload; null, empty, or whitespace produces an empty list.</param>
        /// <returns>The deserialized items, or an empty list for an empty or null JSON payload.</returns>
        public static List<T> DeserializeListTolerant<T>(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
                return new List<T>();

            var s = jsonText.Trim();

            // Unwrap if this is JSON text encoded as a JSON string (double-encoded)
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                s = JsonSerializer.Deserialize<string>(s, IpcJson.Options) ?? "";
                s = s.Trim();
                if (string.IsNullOrWhiteSpace(s))
                    return new List<T>();
            }

            if (s.StartsWith("["))
            {
                return JsonSerializer.Deserialize<List<T>>(s, IpcJson.Options) ?? new List<T>();
            }

            // single object -> wrap
            var obj = JsonSerializer.Deserialize<T>(s, IpcJson.Options);
            return obj == null ? new List<T>() : new List<T> { obj };
        }
    }
}
