using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK.Runtime.Models
{
    public class LeaderboardTimeframe
    {
        [JsonProperty("index")]
        public string TimeframeIndex { get; set; }

        [JsonProperty("top")]
        public List<LeaderboardTopPlayer> Top { get; set; }
    }

    public class LeaderboardTopPlayer
    {
        [JsonProperty("number")]
        public string Number { get; set; }

        [JsonProperty("scoreElement")]
        public LeaderboardScoreElement ScoreElement { get; set; }

        [JsonProperty("additionalElements")]
        public List<LeaderboardScoreElement> AdditionalElements { get; set; }
    }

    public class LeaderboardScoreElement
    {
        [JsonProperty("elementID")]
        public string ElementID { get; set; }

        [JsonProperty("elementValue")]
        public object ElementValue { get; set; }
    }
}