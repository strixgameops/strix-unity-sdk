using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class M_InitializationResponse
    {
        [JsonProperty("data")]
        public Data Data { get; set; }
    }

    public class Data
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("playerData")]
        public PlayerData PlayerData { get; set; }

        [JsonProperty("fcmData")]
        public FCMOptions FcmData { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("isNewPlayer")]
        public bool IsNewPlayer { get; set; }
    }

    public class FCMOptions
    {
        [JsonProperty("apiKey")]
        public string ApiKey { get; set; }

        [JsonProperty("appId")]
        public string AppId { get; set; }

        [JsonProperty("projectId")]
        public string ProjectId { get; set; }

        [JsonProperty("senderId")]
        public string SenderId { get; set; }

        [JsonProperty("storageBucket")]
        public string StorageBucket { get; set; }
    }
}