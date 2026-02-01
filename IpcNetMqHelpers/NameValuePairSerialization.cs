using IpcNetMq.IpcNetMqHelpers;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace IpcNetMq.IpcNetMqHelpers
{

    public static class NameValuePairJson
    {
        /// <summary>
        /// Serialize canonical inner payload: JSON array of {name,value}.
        /// This string is intended to be stored in IpcPacket.RequestString / ReplyString.
        /// </summary>
        public static string SerializeList(List<NameValuePair> pairs)
            => JsonSerializer.Serialize(pairs ?? new List<NameValuePair>(), IpcJson.Options);

        /// <summary>
        /// Deserialize inner payload. Accepts:
        /// - null/empty -> empty list
        /// - JSON array -> List&lt;T&gt;
        /// - single object -> List with one item
        /// - double-encoded JSON text -> unwrap then parse
        /// </summary>
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