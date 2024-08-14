using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class ABTest
    {
        [JsonProperty("id")]
        public string InternalId { get; set; }

        [JsonProperty("codename")]
        public string Id { get; set; }

        [JsonProperty("segments")]
        public ABTestSegments DefaultValue { get; set; }

        [JsonProperty("subject")]
        public ABTestSubject Subject { get; set; }
    }

    public class ABTestSegments
    {
        [JsonProperty("control")]
        public string Control { get; set; }

        [JsonProperty("test")]
        public string Test { get; set; }

        [JsonProperty("testShare")]
        public string TestSegmentShare { get; set; }
    }

    public class ABTestSubject
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("itemID")]
        public string ItemId { get; set; }
    }
}