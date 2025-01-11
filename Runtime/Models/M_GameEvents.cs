using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class GameEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("occasions")]
        public List<string> Occasions { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("segmentsWhitelist")]
        public List<string> SegmentsWhitelist { get; set; } = new List<string>();

        [JsonProperty("segmentsBlacklist")]
        public List<string> SegmentsBlacklist { get; set; } = new List<string>();
    }
}