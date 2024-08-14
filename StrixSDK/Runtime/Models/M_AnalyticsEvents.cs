using Newtonsoft.Json;

namespace StrixSDK.Runtime.Models
{
    public class EventValue
    {
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class AnalyticEvent
    {
        [JsonProperty("codename")]
        public string Codename { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("values")]
        public EventValue[] Values { get; set; }
    }
}