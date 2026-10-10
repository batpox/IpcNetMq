using System.Text.Json;
using System.Text.Json.Serialization;

namespace IpcNetMq.IpcNetMqHelpers
{

    /// <summary>Provides shared JSON options for name/value payload serialization.</summary>
    public static class IpcJson
    {

        /// <summary>The shared compact JSON options that preserve explicitly specified property names.</summary>
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

        /// <summary>Creates a pair with an empty name and value.</summary>
        public NameValuePair() { }

        /// <summary>Creates a pair with the supplied name and encoded value.</summary>
        /// <param name="name">The value name.</param>
        /// <param name="value">The encoded value string.</param>
        public NameValuePair(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>Copies the name and value from another pair.</summary>
        /// <param name="template">The non-null pair to copy.</param>
        public NameValuePair(NameValuePair template)
        {
            Name = template.Name;
            Value = template.Value;
        }

    }

}
// namespace
