using System.Text.Json;
using System.Text.Json.Serialization;

namespace IpcNetMq.IpcNetMqHelpers
{
    public static class IpcJson
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Holds the Name, Value-Hint, and Value (in string format)
    /// </summary>
    public class NameValuePair
    {
        /// <summary>
        /// The name of the value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";


        /// <summary>
        /// An encoded value string.
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; } = "";

        public NameValuePair() { }

        public NameValuePair(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public NameValuePair(NameValuePair template)
        {
            Name = template.Name;
            Value = template.Value;
        }

    }

}
// namespace