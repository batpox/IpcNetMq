using Newtonsoft.Json;

namespace IpcNetMq
{
    /// <summary>
    /// Holds the Name, Value-Hint, and Value (in string format)
    /// </summary>
    public class NameValuePair
    {
        /// <summary>
        /// The name of the value.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";


        /// <summary>
        /// An encoded value string.
        /// </summary>
        [JsonProperty("value")]
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


} // namespace