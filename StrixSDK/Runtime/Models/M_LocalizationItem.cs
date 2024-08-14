using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class M_LocalizationItem
    {
        [JsonProperty("id")]
        public string Key { get; set; }

        [JsonProperty("translations")]
        public TranslationItem[] Items { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class TranslationItem
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}